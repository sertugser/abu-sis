using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using Xunit;

namespace SIS.Tests.Application;

/// <summary>
/// "Kullanıma Aç" (ActivateTermAsync) ve dönem oluşturma için davranış testleri.
/// Yeni model (Proliz/Banner): tek-aktif değişmezi + yeni dönem kapalı başlar +
/// aktiflik tarihten bağımsız manuel bayrağa bağlı.
/// </summary>
public class AdminTermServiceActivationTests
{
    private static readonly DateTime Now = new(2026, 5, 23, 12, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AdminTermService CreateService(AppDbContext context) =>
        new(context, new NoopTimeSlotService(), new FakeClock { UtcNow = Now });

    private static AcademicTerm Term(int id, int year, TermType type, bool activated,
        DateTime start, DateTime end, bool cancelled = false) => new()
    {
        Id = id,
        Name = $"{year} {type}",
        ShortName = $"{year}-{type}",
        EnglishName = $"{year} {type}",
        ShortEnglishName = $"{year} {type}",
        Year = year,
        TermType = type,
        TermStart = start,
        TermEnd = end,
        IsActivated = activated,
        IsCancelled = cancelled,
        CreatedAt = Now,
    };

    [Fact]
    public async Task ActivateTermAsync_DeactivatesAllOtherActivatedTerms()
    {
        await using var ctx = CreateContext();
        // İki dönem birden açık (legacy / tutarsız durum)
        ctx.AcademicTerms.Add(Term(1, 2025, TermType.Spring, activated: true,
            new DateTime(2026, 2, 23, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc)));
        ctx.AcademicTerms.Add(Term(2, 2026, TermType.Spring, activated: false,
            new DateTime(2027, 2, 22, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 6, 4, 0, 0, 0, DateTimeKind.Utc)));
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        await service.ActivateTermAsync(2);

        var terms = await ctx.AcademicTerms.OrderBy(t => t.Id).ToListAsync();
        terms.Single(t => t.Id == 1).IsActivated.Should().BeFalse("tek-aktif: yeni açılan diğerlerini kapatır");
        terms.Single(t => t.Id == 2).IsActivated.Should().BeTrue();
        terms.Count(t => t.IsActivated).Should().Be(1, "her an en çok bir dönem açık olmalı");
    }

    [Fact]
    public async Task ActivateTermAsync_FutureTerm_IsActiveRegardlessOfDate()
    {
        await using var ctx = CreateContext();
        // Bugünden (May 2026) sonra başlayan dönem
        ctx.AcademicTerms.Add(Term(2, 2026, TermType.Spring, activated: false,
            new DateTime(2027, 2, 22, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 6, 4, 0, 0, 0, DateTimeKind.Utc)));
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        await service.ActivateTermAsync(2);

        var term = await ctx.AcademicTerms.SingleAsync();
        term.IsActivated.Should().BeTrue();
        term.IsActive(Now).Should().BeTrue("aktiflik tarihe değil bayrağa bağlı");

        // Merkezi "aktif dönem" sorgusu bu dönemi bulmalı (üst bar / kayıt vb. tutarlı)
        var active = await ctx.AcademicTerms.ActiveTerm().FirstOrDefaultAsync();
        active.Should().NotBeNull();
        active!.Id.Should().Be(2);
    }

    [Fact]
    public async Task ActivateTermAsync_CancelledTerm_Throws()
    {
        await using var ctx = CreateContext();
        ctx.AcademicTerms.Add(Term(3, 2024, TermType.Fall, activated: false,
            new DateTime(2024, 9, 16, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            cancelled: true));
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var act = () => service.ActivateTermAsync(3);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateTermAsync_DefaultsToInactive()
    {
        await using var ctx = CreateContext();
        var service = CreateService(ctx);

        var dto = new CreateTermDto(
            Name: "2027-2028 Güz",
            ShortName: "2027-G",
            EnglishName: "2027-2028 Fall",
            ShortEnglishName: "2027 Fall",
            OtherName: null,
            Year: 2027,
            TermType: nameof(TermType.Fall),
            YoksisStatus: "Inactive",
            TermStart: new DateTime(2027, 9, 21, 0, 0, 0, DateTimeKind.Utc),
            TermEnd: new DateTime(2028, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            OverrideStart: null,
            OverrideEnd: null);

        var created = await service.CreateTermAsync(dto);

        created.IsActivated.Should().BeFalse("yeni dönem kapalı başlar; admin Kullanıma Aç ile açar");
        var stored = await ctx.AcademicTerms.SingleAsync();
        stored.IsActivated.Should().BeFalse();
    }

    // ActivateTermAsync yalnızca GenerateTimeSlotsForTermAsync çağırır; diğer üyeler kullanılmaz.
    private sealed class NoopTimeSlotService : ITimeSlotService
    {
        public Task<int> GenerateTimeSlotsForTermAsync(int termId, CancellationToken ct = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<SIS.Application.Scheduling.DTOs.TimeSlotDto>> GetByTermAsync(int termId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<SIS.Application.Scheduling.DTOs.TimeSlotDto> CreateAsync(SIS.Application.Scheduling.DTOs.CreateTimeSlotDto dto, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<SIS.Application.Scheduling.DTOs.TimeSlotDto> UpdateAsync(int id, SIS.Application.Scheduling.DTOs.UpdateTimeSlotDto dto, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<SIS.Application.Scheduling.DTOs.TimeSlotDeleteResultDto> DeleteAsync(int id, bool force, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<SIS.Application.Scheduling.DTOs.RegenerateTimeSlotsResultDto> RegenerateForTermAsync(int termId, bool force, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
