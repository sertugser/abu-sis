using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Infrastructure.Services.Scheduling.OrTools;

/// <summary>
/// Solver çağrısından önce her section için "en az 1 uygun classroom var mı?" kontrolü.
/// CP-SAT'in 'all-or-nothing' davranışını engelleyen pre-filter.
///
/// Lab component için (Plan 4 paralel split): tek oda yetmiyorsa
/// groupCount = ⌈capacity / max_lab_capacity⌉ kadar paralel grup gerekir;
/// her grubun en az ⌈capacity / groupCount⌉ kapasiteli ayrı bir Lab/Computer odasıyla eşlenmesi mümkün olmalı.
/// Lecture component için: Capacity ≥ section.Capacity yeterli (tip kısıtı yok — H4 encoder zaten lab tipini sınırlıyor).
/// </summary>
public sealed class PreflightFeasibilityChecker
{
    public PreflightResult Check(
        IReadOnlyList<SectionInput> sections,
        IReadOnlyList<ClassroomInput> classrooms,
        IReadOnlyList<TimeSlotInput> timeSlots,
        int maxLabBlockHours = 2)
    {
        var feasible = new List<SectionInput>();
        var infeasible = new List<InfeasibleSection>();

        foreach (var section in sections)
        {
            // Plan 7 — Slot block validity check (cross-day + slot-gap).
            if (section.TheoryHours > 0)
            {
                var validStarts = SlotBlockValidator.ComputeValidBlockStarts(timeSlots, section.TheoryHours);
                if (validStarts.Count == 0)
                {
                    infeasible.Add(new InfeasibleSection(section,
                        $"{section.TheoryHours} saatlik teori için geçerli zaman aralığı yok " +
                        $"(gün sınırı veya >10dk gap kuralları nedeniyle)."));
                    continue;
                }
            }
            if (section.LabHours > 0)
            {
                // Plan 7b Task 8 — Multi-interval lab time-split: when LabHours > maxLabBlockHours,
                // each block has at most maxLabBlockHours duration. Validate using the largest block,
                // not the full LabHours (which may be too long to fit any single contiguous range).
                // SplitLabIntoBlocks always returns ≥1 block when LabHours > 0 (guaranteed by helper contract).
                var blockSizes = LabBlockSplitter.SplitLabIntoBlocks(section.LabHours, maxLabBlockHours);
                var largestBlock = blockSizes.Max();

                var validStarts = SlotBlockValidator.ComputeValidBlockStarts(timeSlots, largestBlock);
                if (validStarts.Count == 0)
                {
                    infeasible.Add(new InfeasibleSection(section,
                        $"{largestBlock} saatlik lab bloğu için geçerli zaman aralığı yok " +
                        $"(gün sınırı veya >10dk gap kuralları nedeniyle)."));
                    continue;
                }

                // Plan 7b Task 8 — When lab is split into N blocks, each block must land on
                // a different day (AddAllDifferent). The slot grid must therefore offer at
                // least N distinct days; otherwise no feasible assignment exists.
                if (blockSizes.Count > 1)
                {
                    var distinctDays = timeSlots.Select(t => t.DayOfWeek).Distinct().Count();
                    if (distinctDays < blockSizes.Count)
                    {
                        infeasible.Add(new InfeasibleSection(section,
                            $"{section.LabHours} saatlik lab {blockSizes.Count} bloğa bölündü ama " +
                            $"slot grid'inde sadece {distinctDays} farklı gün var; her blok farklı güne düşmeli."));
                        continue;
                    }
                }
            }

            // Lab component check — paralel split destekli (Plan 4 Task 6)
            if (section.TeachingMethod == TeachingMethod.Online)
            {
                feasible.Add(section);
                continue;
            }

            if (section.LabHours > 0)
            {
                var labRooms = classrooms.Where(c =>
                    c.Type == ClassroomType.Lab || c.Type == ClassroomType.Computer).ToList();
                if (labRooms.Count == 0)
                {
                    infeasible.Add(new InfeasibleSection(section,
                        $"Lab component için Lab/Computer tipi derslik yok."));
                    continue;
                }

                var maxCap = labRooms.Max(c => c.Capacity);
                if (maxCap == 0)
                {
                    infeasible.Add(new InfeasibleSection(section, "Lab odaları kapasitesi 0."));
                    continue;
                }

                // section.Capacity ≤ 0 invariant ihlali; defensive guard — groupCount=1 ile devam.
                var groupCount = section.Capacity <= 0
                    ? 1
                    : (int)Math.Ceiling((double)section.Capacity / maxCap);
                var minCapPerGroup = section.Capacity <= 0
                    ? 0
                    : (int)Math.Ceiling((double)section.Capacity / groupCount);

                var qualifyingRooms = labRooms.Count(c => c.Capacity >= minCapPerGroup);
                if (qualifyingRooms < groupCount)
                {
                    infeasible.Add(new InfeasibleSection(section,
                        $"Lab component için yeterli derslik yok: {groupCount} paralel grup gerekli (her biri kapasite ≥{minCapPerGroup}), mevcut sadece {qualifyingRooms}."));
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
