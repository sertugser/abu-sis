using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;
using Xunit;

namespace SIS.Tests.Infrastructure;

/// <summary>
/// AuditInterceptor davranış testleri: otomatik kayıt üretimi, hassas alan maskeleme
/// ve aynı entity için manuel log varsa otomatik kaydı bastırma (çift-loglama önleme).
/// </summary>
public class AuditInterceptorTests
{
    private static AppDbContext CreateContext()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditInterceptor(accessor))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Created_entity_writes_audit_log_and_masks_sensitive_identifiers()
    {
        await using var ctx = CreateContext();
        ctx.Set<StudentPersonalInfo>().Add(new StudentPersonalInfo
        {
            StudentId = 1,
            Phone = "5551112233",
            PassportNo = "U12345678",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var log = await ctx.AuditLogs.SingleAsync(a => a.EntityType == nameof(StudentPersonalInfo));
        log.Action.Should().Be("Created");
        log.NewValue.Should().NotBeNull();
        log.NewValue!.Should().Contain("5551112233");      // normal alan değeri loglanır
        log.NewValue!.Should().Contain("\"phone\"");       // sözlük anahtarı camelCase (DictionaryKeyPolicy)
        log.NewValue!.Should().NotContain("\"Phone\"");    // PascalCase anahtar kalmamalı
        log.NewValue!.Should().NotContain("assportNo");    // hassas kimlik alanı maskelenir (anahtar yok)
        log.NewValue!.Should().NotContain("U12345678");    // ham değer asla görünmemeli
    }

    [Fact]
    public async Task Modified_entity_records_old_and_new_values()
    {
        await using var ctx = CreateContext();
        var spi = new StudentPersonalInfo { StudentId = 2, Phone = "111", CreatedAt = DateTime.UtcNow };
        ctx.Set<StudentPersonalInfo>().Add(spi);
        await ctx.SaveChangesAsync();

        spi.Phone = "222";
        await ctx.SaveChangesAsync();

        var modified = await ctx.AuditLogs
            .SingleAsync(a => a.Action == "Modified" && a.EntityType == nameof(StudentPersonalInfo));
        modified.OldValue.Should().Contain("111");
        modified.NewValue.Should().Contain("222");
    }

    [Fact]
    public async Task Manual_audit_log_suppresses_auto_audit_for_same_entity()
    {
        await using var ctx = CreateContext();
        var spi = new StudentPersonalInfo { StudentId = 3, Phone = "111", CreatedAt = DateTime.UtcNow };
        ctx.Set<StudentPersonalInfo>().Add(spi);
        await ctx.SaveChangesAsync(); // Created audit

        // Aynı save içinde: hem entity değişsin hem de elle zengin bir AuditLog eklensin.
        spi.Phone = "999";
        ctx.AuditLogs.Add(new AuditLog
        {
            Action = "ScheduleAssigned",
            EntityType = nameof(StudentPersonalInfo),
            EntityId = spi.Id.ToString(),
            Timestamp = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        // Otomatik "Modified" üretilmemeli — manuel log onu bastırır (çift-loglama önleme).
        (await ctx.AuditLogs.CountAsync(a => a.Action == "Modified" && a.EntityType == nameof(StudentPersonalInfo)))
            .Should().Be(0);
        // Manuel kayıt korunmalı.
        (await ctx.AuditLogs.CountAsync(a => a.Action == "ScheduleAssigned")).Should().Be(1);
    }

    [Fact]
    public async Task Deleted_entity_records_old_values()
    {
        await using var ctx = CreateContext();
        var spi = new StudentPersonalInfo { StudentId = 4, Phone = "abc", CreatedAt = DateTime.UtcNow };
        ctx.Set<StudentPersonalInfo>().Add(spi);
        await ctx.SaveChangesAsync();

        ctx.Set<StudentPersonalInfo>().Remove(spi);
        await ctx.SaveChangesAsync();

        var deleted = await ctx.AuditLogs
            .SingleAsync(a => a.Action == "Deleted" && a.EntityType == nameof(StudentPersonalInfo));
        deleted.OldValue.Should().Contain("abc");
    }
}
