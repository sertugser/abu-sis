using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.OrTools;

/// <summary>
/// Solver çağrısından önce her section için "en az 1 uygun classroom var mı?" kontrolü.
/// CP-SAT'in 'all-or-nothing' davranışını engelleyen pre-filter.
///
/// Lab component için: ClassroomType.Lab veya ClassroomType.Computer + Capacity ≥ section.Capacity gerekir.
/// Lecture component için: Capacity ≥ section.Capacity yeterli (tip kısıtı yok — H4 encoder zaten lab tipini sınırlıyor).
/// </summary>
public sealed class PreflightFeasibilityChecker
{
    public PreflightResult Check(
        IReadOnlyList<SectionInput> sections,
        IReadOnlyList<ClassroomInput> classrooms)
    {
        var feasible = new List<SectionInput>();
        var infeasible = new List<InfeasibleSection>();

        foreach (var section in sections)
        {
            // Lab component check
            if (section.LabHours > 0)
            {
                var labCandidates = classrooms.Where(c =>
                    (c.Type == ClassroomType.Lab || c.Type == ClassroomType.Computer)
                    && c.Capacity >= section.Capacity).ToList();
                if (labCandidates.Count == 0)
                {
                    infeasible.Add(new InfeasibleSection(section,
                        $"Lab component için uygun derslik yok (kapasite ≥{section.Capacity}, tip Lab/Computer)."));
                    continue;
                }
            }

            // Lecture component check
            if (section.TheoryHours > 0)
            {
                var lectureCandidates = classrooms.Where(c => c.Capacity >= section.Capacity).ToList();
                if (lectureCandidates.Count == 0)
                {
                    infeasible.Add(new InfeasibleSection(section,
                        $"Lecture component için uygun derslik yok (kapasite ≥{section.Capacity})."));
                    continue;
                }
            }

            feasible.Add(section);
        }

        return new PreflightResult(feasible, infeasible);
    }
}

/// <summary>Pre-flight check'te eleğin altına düşen section + sebep.</summary>
public sealed record InfeasibleSection(SectionInput Section, string Reason);

/// <summary>Pre-flight check sonucu — feasible vs infeasible section ayrımı.</summary>
public sealed record PreflightResult(
    IReadOnlyList<SectionInput> FeasibleSections,
    IReadOnlyList<InfeasibleSection> InfeasibleSections);
