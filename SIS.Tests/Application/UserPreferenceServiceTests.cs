using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.UserPreferences.DTOs;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class UserPreferenceServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UserPreferenceService _service;

    public UserPreferenceServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _service = new UserPreferenceService(_context);
    }

    [Fact]
    public async Task GetAsync_ReturnsEmptyWithHasCustomizedFalse_WhenNoRowExists()
    {
        var result = await _service.GetAsync(userId: 42);

        result.Favorites.Should().BeEmpty();
        result.HasCustomized.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_CreatesRow_WhenNoneExists()
    {
        var routes = new[] { "/dashboard", "/ders-kayit" };

        var result = await _service.UpdateAsync(
            userId: 42,
            new UpdateUserPreferenceRequest(routes));

        result.Favorites.Should().BeEquivalentTo(routes, opt => opt.WithStrictOrdering());
        result.HasCustomized.Should().BeTrue();

        (await _context.UserPreferences.CountAsync(p => p.UserId == 42)).Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_OverwritesExistingRow()
    {
        await _service.UpdateAsync(42, new UpdateUserPreferenceRequest(new[] { "/a", "/b" }));

        var second = await _service.UpdateAsync(42, new UpdateUserPreferenceRequest(new[] { "/c" }));

        second.Favorites.Should().BeEquivalentTo(new[] { "/c" });
        (await _context.UserPreferences.CountAsync(p => p.UserId == 42)).Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_Rejects_WhenExceedsMax()
    {
        var tooMany = Enumerable.Range(0, 20).Select(i => $"/route-{i}").ToArray();

        var act = async () => await _service.UpdateAsync(42,
            new UpdateUserPreferenceRequest(tooMany));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*en fazla*");
    }

    [Fact]
    public async Task UpdateAsync_Rejects_WhenRouteIsEmpty()
    {
        var act = async () => await _service.UpdateAsync(42,
            new UpdateUserPreferenceRequest(new[] { "/dashboard", "" }));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*bos*");
    }

    [Fact]
    public async Task UpdateAsync_Rejects_WhenRouteDoesNotStartWithSlash()
    {
        var act = async () => await _service.UpdateAsync(42,
            new UpdateUserPreferenceRequest(new[] { "dashboard" }));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*baslamali*");
    }

    [Fact]
    public async Task UpdateAsync_DeduplicatesRoutes()
    {
        var result = await _service.UpdateAsync(42,
            new UpdateUserPreferenceRequest(new[] { "/a", "/a", "/b" }));

        result.Favorites.Should().BeEquivalentTo(new[] { "/a", "/b" }, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public async Task UpdateAsync_IsolatesUsers()
    {
        await _service.UpdateAsync(1, new UpdateUserPreferenceRequest(new[] { "/a" }));
        await _service.UpdateAsync(2, new UpdateUserPreferenceRequest(new[] { "/b" }));

        var u1 = await _service.GetAsync(1);
        var u2 = await _service.GetAsync(2);

        u1.Favorites.Should().BeEquivalentTo(new[] { "/a" });
        u2.Favorites.Should().BeEquivalentTo(new[] { "/b" });
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
