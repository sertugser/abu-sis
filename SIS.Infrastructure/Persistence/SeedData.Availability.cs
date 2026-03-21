using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    private static async Task SeedInstructorAvailabilitiesAsync(AppDbContext context)
    {
        if (await context.InstructorAvailabilities.AnyAsync())
        {
            return;
        }

        var instructorIds = await context.Instructors
            .AsNoTracking()
            .Select(instructor => instructor.Id)
            .ToListAsync();

        if (instructorIds.Count == 0)
        {
            return;
        }

        var template = new (DayOfWeek Day, int StartHour, int EndHour, bool IsPreferred)[]
        {
            (DayOfWeek.Monday, 9, 12, true),
            (DayOfWeek.Monday, 13, 17, false),
            (DayOfWeek.Tuesday, 9, 12, true),
            (DayOfWeek.Tuesday, 13, 17, false),
            (DayOfWeek.Wednesday, 9, 12, true),
            (DayOfWeek.Wednesday, 13, 17, false),
            (DayOfWeek.Thursday, 9, 12, true),
            (DayOfWeek.Thursday, 13, 17, false),
            (DayOfWeek.Friday, 9, 12, true),
            (DayOfWeek.Friday, 13, 17, false)
        };

        var entities = instructorIds
            .SelectMany(instructorId => template.Select(window => new InstructorAvailability
            {
                InstructorId = instructorId,
                DayOfWeek = window.Day,
                StartTime = new TimeOnly(window.StartHour, 0),
                EndTime = new TimeOnly(window.EndHour, 0),
                IsAvailable = true,
                IsPreferred = window.IsPreferred
            }))
            .ToList();

        await context.InstructorAvailabilities.AddRangeAsync(entities);
        await context.SaveChangesAsync();
    }
}
