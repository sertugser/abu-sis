namespace SIS.Application.Common.Services;

public sealed record InstructorWeeklyHoursPolicy(
    int Professor,
    int AssociateProfessor,
    int AssistantProfessor,
    int Lecturer)
{
    public static async Task<InstructorWeeklyHoursPolicy> LoadAsync(
        ISystemConfigProvider config,
        CancellationToken ct = default)
    {
        return new InstructorWeeklyHoursPolicy(
            await config.GetIntAsync("Instructor.MaxWeeklyHours.Professor", 12, ct),
            await config.GetIntAsync("Instructor.MaxWeeklyHours.AssociateProfessor", 16, ct),
            await config.GetIntAsync("Instructor.MaxWeeklyHours.AssistantProfessor", 20, ct),
            await config.GetIntAsync("Instructor.MaxWeeklyHours.Lecturer", 24, ct));
    }

    public int Resolve(string? title, int fallback)
    {
        if (string.IsNullOrWhiteSpace(title))
            return fallback;

        var normalized = title.Trim().ToLowerInvariant();

        if (normalized.Contains("doç") || normalized.Contains("associate professor") || normalized.Contains("assoc. prof"))
            return AssociateProfessor;

        if (normalized.Contains("assistant professor")
            || normalized.Contains("assistant prof")
            || normalized.Contains("doktor öğretim")
            || normalized.StartsWith("dr. öğr"))
            return AssistantProfessor;

        if (normalized.Contains("öğr. gör") || normalized.Contains("lecturer"))
            return Lecturer;

        if (normalized.Contains("prof.") || normalized.Contains("profesör") || normalized.Contains("professor"))
            return Professor;

        return fallback;
    }
}
