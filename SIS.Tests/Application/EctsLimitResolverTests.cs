using FluentAssertions;
using SIS.Application.Common.Services;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class EctsLimitResolverTests
{
    private sealed class FakeConfig(IReadOnlyDictionary<string, int> values) : ISystemConfigProvider
    {
        public Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
            => Task.FromResult(values.TryGetValue(key, out var v) ? v : fallback);
        public Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default) => Task.FromResult(fallback);
        public Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default) => Task.FromResult(fallback);
        public Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default) => Task.FromResult(fallback);
        public void InvalidateCache(string key) { }
    }

    private static EctsLimitResolver CreateResolver(params (string key, int value)[] config)
        => new(new FakeConfig(config.ToDictionary(c => c.key, c => c.value)));

    [Theory]
    [InlineData(1.50, 30)]
    [InlineData(2.00, 42)]
    [InlineData(2.49, 42)]
    [InlineData(2.50, 48)]
    [InlineData(3.80, 48)]
    public async Task ResolveAsync_UsesConfigThresholds_WhenNoCurriculumOverride(decimal cgpa, int expected)
    {
        var resolver = CreateResolver(
            ("Registration.EctsLimit.CgpaBelow200", 30),
            ("Registration.EctsLimit.Cgpa200To249", 42),
            ("Registration.EctsLimit.CgpaAbove250", 48));
        var limit = await resolver.ResolveAsync(cgpa, curriculum: null);
        limit.Should().Be(expected);
    }

    [Fact]
    public async Task ResolveAsync_PrefersCurriculumOverride_OverConfig()
    {
        var resolver = CreateResolver(("Registration.EctsLimit.CgpaAbove250", 48));
        var curriculum = new Curriculum { EctsLimitAbove250 = 50 };
        var limit = await resolver.ResolveAsync(cgpa: 3.00m, curriculum);
        limit.Should().Be(50);
    }

    [Fact]
    public async Task ResolveAsync_FallsBackToConfig_WhenOverrideIsNull()
    {
        var resolver = CreateResolver(("Registration.EctsLimit.Cgpa200To249", 42));
        var curriculum = new Curriculum { EctsLimit200To249 = null };
        var limit = await resolver.ResolveAsync(cgpa: 2.20m, curriculum);
        limit.Should().Be(42);
    }

    [Fact]
    public async Task ResolveAsync_UsesHardDefaults_WhenConfigMissing()
    {
        var resolver = CreateResolver();
        (await resolver.ResolveAsync(1.0m, null)).Should().Be(30);
        (await resolver.ResolveAsync(2.2m, null)).Should().Be(42);
        (await resolver.ResolveAsync(3.0m, null)).Should().Be(48);
    }
}
