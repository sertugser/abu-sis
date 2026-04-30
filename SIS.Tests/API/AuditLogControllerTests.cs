using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Controllers;
using SIS.Application.Admin.DTOs;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.API;

public class AuditLogControllerTests
{
    [Fact]
    public async Task GetAll_IncludesOldAndNewValuesInSummaryItems()
    {
        await using var context = CreateContext();
        context.Users.Add(new ApplicationUser
        {
            Id = 41,
            UserName = "sysadmin",
            NormalizedUserName = "SYSADMIN",
            FirstName = "System",
            LastName = "Admin"
        });
        context.AuditLogs.Add(new AuditLog
        {
            Id = 7,
            UserId = 41,
            Action = "Modified",
            EntityType = "WebModuleSetting",
            EntityId = "15",
            OldValue = "{\"Value\":\"false\",\"EffectiveFrom\":null,\"EffectiveTo\":null}",
            NewValue = "{\"Value\":\"true\",\"EffectiveFrom\":\"2026-04-30T08:00:00Z\",\"EffectiveTo\":null}",
            Timestamp = new DateTime(2026, 4, 30, 8, 24, 0, DateTimeKind.Utc),
            IpAddress = "127.0.0.1"
        });
        await context.SaveChangesAsync();

        var controller = new AuditLogController(context);

        var result = await controller.GetAll(
            entityType: "WebModuleSetting",
            entityId: "15",
            userId: null,
            action: "Modified",
            search: null,
            from: null,
            to: null,
            sortBy: null,
            sortDir: null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<AuditLogPagedResponse>().Subject;
        payload.Items.Should().ContainSingle();
        payload.Items[0].OldValue.Should().Be("{\"Value\":\"false\",\"EffectiveFrom\":null,\"EffectiveTo\":null}");
        payload.Items[0].NewValue.Should().Be("{\"Value\":\"true\",\"EffectiveFrom\":\"2026-04-30T08:00:00Z\",\"EffectiveTo\":null}");
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
