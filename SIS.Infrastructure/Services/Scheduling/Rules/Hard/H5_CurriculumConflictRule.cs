using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.5 — H5 Müfredat Çakışması.
/// 3 mod (V1 ile parite): "enrollment", "cohort", "curriculum" (default).
///   - enrollment: <c>Snapshot.EnrollmentConflicts.HardConflictPairs</c> üzerinden
///   - cohort: <c>Snapshot.CohortConflictPairs</c> üzerinden
///   - curriculum: SuggestedSemestersByDepartment + AccessibleDepartmentIds üzerinden
///     aynı (Dept, Year) cohort'unda farklı CourseId'ler.
///
/// Mesaj formatı kullanıcıya somut bilgi verir: ders kodları + çakışan gün/saat + sınıf yılı
/// (varsa). "Aynı kohortta" gibi belirsiz ifadeler yerine "X. sınıf öğrencileri her iki dersi
/// de alır" gibi sebep + sonuç açıklar. Tüm değerler DB'deki o anki state'ten dinamik.
/// </summary>
public sealed class H5_CurriculumConflictRule : IRulePredicate
{
    public string Code => "H5";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var snapshot = ctx.Snapshot;
        var violations = new List<RuleViolation>();

        // Enrollment mode — gerçek kayıt verisi, her pair direkt etkilenir, alternatif check yok.
        if (snapshot.EnrollmentConflicts != null)
        {
            foreach (var existing in snapshot.Sections)
            {
                if (existing.SectionId == c.SectionId) continue;
                if (existing.CourseId == c.CourseId) continue;

                var pair = c.SectionId < existing.SectionId
                    ? (c.SectionId, existing.SectionId)
                    : (existing.SectionId, c.SectionId);
                if (!snapshot.EnrollmentConflicts.HardConflictPairs.Contains(pair)) continue;

                AddOverlapViolations(c, existing, "CurriculumConflict",
                    (day, ovS, ovE) => BuildMessage(
                        "Enrollment",
                        c, existing, day, ovS, ovE),
                    violations);
            }
            return violations;
        }

        // Cohort mode — pair'ler CohortBuilder modulo ile. Alternatif kombinasyon check'i:
        // course pair (X, Y) için en az bir (xSec, ySec) çakışmıyorsa öğrenci için yol var
        // → uyarı yutulur. Tüm N×M kombinasyon çakışıyorsa gerçek darboğaz, uyarı verilir.
        if (snapshot.CohortConflictPairs.Count > 0)
        {
            foreach (var existing in snapshot.Sections)
            {
                if (existing.SectionId == c.SectionId) continue;
                if (existing.CourseId == c.CourseId) continue;

                var pair = c.SectionId < existing.SectionId
                    ? (c.SectionId, existing.SectionId)
                    : (existing.SectionId, c.SectionId);
                if (!snapshot.CohortConflictPairs.Contains(pair)) continue;

                if (HasAlternativePairing(c, existing, snapshot)) continue;

                AddOverlapViolations(c, existing, "CurriculumConflict",
                    (day, ovS, ovE) => BuildMessage(
                        "Cohort",
                        c, existing, day, ovS, ovE),
                    violations);
            }
            return violations;
        }

        // Curriculum mode (default) — aynı alternatif mantığı.
        // 2026-05-20: Sadece her iki tarafı da ZORUNLU olan ders çiftleri raporlanır.
        // En az biri seçmeli ise öğrenci o seçmeliyi pas geçebilir → uyarı verilmez.
        // (CohortBuilder aynı yıl pair üretiminde simetrik filtreyle uyumlu davranış.)
        foreach (var existing in snapshot.Sections)
        {
            if (existing.SectionId == c.SectionId) continue;
            if (existing.CourseId == c.CourseId) continue;

            if (!c.IsCompulsory || !existing.IsCompulsory) continue;

            if (!ShareCurriculumDepartment(c, existing)) continue;

            if (HasAlternativePairing(c, existing, snapshot)) continue;

            AddOverlapViolations(c, existing, "CurriculumConflict",
                (day, ovS, ovE) => BuildMessage(
                    "Curriculum",
                    c, existing, day, ovS, ovE),
                violations);
        }

        return violations;
    }

    /// <summary>
    /// Course pair (X = c.CourseId, Y = existing.CourseId) için öğrencinin alabileceği
    /// en az bir (xSec, ySec) kombinasyonu çakışmıyorsa true döner. Bu sayede modulo pair
    /// (sec_1↔sec_1) çakışıyor olsa bile, başka section'lar arası uyumlu bir kombinasyon
    /// varsa uyarı verilmez (kullanıcı beklentisi: "en az 1 alternatif → sorun değil").
    /// Atanmamış (Blocks boş) section'lar dikkate alınmaz.
    /// </summary>
    private static bool HasAlternativePairing(RuntimeCandidate c, RuntimeCandidate existing, RuntimeSnapshot snapshot)
    {
        var sectionsX = snapshot.Sections.Where(s => s.CourseId == c.CourseId && s.Blocks.Count > 0).ToList();
        var sectionsY = snapshot.Sections.Where(s => s.CourseId == existing.CourseId && s.Blocks.Count > 0).ToList();
        if (sectionsX.Count == 0 || sectionsY.Count == 0) return false;

        foreach (var x in sectionsX)
        {
            foreach (var y in sectionsY)
            {
                if (x.SectionId == y.SectionId) continue;
                if (!HasBlockOverlap(x.Blocks, y.Blocks)) return true;
            }
        }
        return false;
    }

    private static bool HasBlockOverlap(IReadOnlyList<RuntimeBlock> a, IReadOnlyList<RuntimeBlock> b)
    {
        foreach (var x in a)
            foreach (var y in b)
                if (x.Day == y.Day && x.StartTime < y.EndTime && x.EndTime > y.StartTime)
                    return true;
        return false;
    }

    private static bool ShareCurriculumDepartment(RuntimeCandidate a, RuntimeCandidate b)
    {
        foreach (var deptId in a.AccessibleDepartmentIds)
        {
            if (!b.AccessibleDepartmentIds.Contains(deptId)) continue;
            if (a.SuggestedSemestersByDepartment.TryGetValue(deptId, out var aSem)
                && b.SuggestedSemestersByDepartment.TryGetValue(deptId, out var bSem)
                && aSem == bSem)
                return true;
        }
        return false;
    }

    private enum CohortRelation { SameYear, AdjacentYear, Unknown }

    /// <summary>
    /// İki section arasındaki cohort ilişkisini belirler:
    ///   - SameYear: ortak bölümde aynı yarıyıl (her ikisi de aynı sınıfın zorunlusu)
    ///   - AdjacentYear: ortak bölümde komşu yıl (alttan/üstten ders senaryosu — 2026-05-17 hard kural)
    ///   - Unknown: ortak dept yok veya bilgi eksik
    /// Birlikte (year etiketi, ilişki türü) döner; etiket null ise dept-year bilgisi mesaja konulmaz.
    /// </summary>
    private static (string? Label, CohortRelation Relation) FindSharedCohortInfo(RuntimeCandidate a, RuntimeCandidate b)
    {
        int? sameYear = null;
        int? aYearAdj = null;
        int? bYearAdj = null;

        foreach (var deptId in a.AccessibleDepartmentIds)
        {
            if (!b.AccessibleDepartmentIds.Contains(deptId)) continue;
            if (!a.SuggestedSemestersByDepartment.TryGetValue(deptId, out var aSem)) continue;
            if (!b.SuggestedSemestersByDepartment.TryGetValue(deptId, out var bSem)) continue;
            if (aSem <= 0 || bSem <= 0) continue;

            var aYear = (aSem + 1) / 2;
            var bYear = (bSem + 1) / 2;

            if (aYear == bYear) { sameYear = aYear; break; }
            if (Math.Abs(aYear - bYear) == 1 && aYearAdj == null)
            {
                aYearAdj = aYear;
                bYearAdj = bYear;
            }
        }

        if (sameYear.HasValue)
            return ($"{sameYear}. sınıf öğrencileri", CohortRelation.SameYear);
        if (aYearAdj.HasValue && bYearAdj.HasValue)
            return ($"{aYearAdj}. sınıf ↔ {bYearAdj}. sınıf", CohortRelation.AdjacentYear);

        return (null, CohortRelation.Unknown);
    }

    private static string BuildMessage(
        string mode,
        RuntimeCandidate c, RuntimeCandidate existing,
        int day, TimeOnly ovS, TimeOnly ovE)
    {
        var (label, relation) = FindSharedCohortInfo(c, existing);
        var dayLabel = DayLabel(day);
        var when = $"{dayLabel} {ovS:HH\\:mm}-{ovE:HH\\:mm}";

        string reason = relation switch
        {
            CohortRelation.SameYear =>
                $"{label} hem '{c.CourseCode}' hem '{existing.CourseCode}' derslerini aynı yarıyılda alır",
            CohortRelation.AdjacentYear =>
                $"{label} — '{c.CourseCode}' bir yılın zorunlusu, '{existing.CourseCode}' komşu yılın zorunlusu. Alttan kalan/üstten alan öğrenciler için çakışma riski",
            _ when mode == "Enrollment" =>
                $"'{c.CourseCode}' ve '{existing.CourseCode}' derslerinin ikisine de kayıtlı öğrenciler var",
            _ =>
                $"Aynı müfredattaki öğrenciler '{c.CourseCode}' ve '{existing.CourseCode}' derslerini birlikte alır",
        };

        return
            $"{reason} — aynı saatte ikisini birden alamazlar. " +
            $"Çakışan zaman: {when}. " +
            $"({c.CourseCode}: '{c.CourseName}'  vs  {existing.CourseCode}: '{existing.CourseName}')";
    }

    private static void AddOverlapViolations(
        RuntimeCandidate c, RuntimeCandidate existing, string conflictType,
        Func<int, TimeOnly, TimeOnly, string> messageBuilder,
        List<RuleViolation> bag)
    {
        foreach (var a in c.Blocks)
        foreach (var b in existing.Blocks)
        {
            if (a.Day != b.Day) continue;
            if (a.StartTime < b.EndTime && a.EndTime > b.StartTime)
            {
                var ovS = a.StartTime > b.StartTime ? a.StartTime : b.StartTime;
                var ovE = a.EndTime < b.EndTime ? a.EndTime : b.EndTime;
                bag.Add(new RuleViolation(
                    Code: "H5", ConflictType: conflictType,
                    Message: messageBuilder(a.Day, ovS, ovE),
                    SectionId: c.SectionId, RelatedSectionId: existing.SectionId,
                    Day: a.Day, StartTime: ovS, EndTime: ovE));
            }
        }
    }

    private static string DayLabel(int day) => day switch
    {
        1 => "Pazartesi",
        2 => "Salı",
        3 => "Çarşamba",
        4 => "Perşembe",
        5 => "Cuma",
        6 => "Cumartesi",
        0 => "Pazar",
        _ => $"Gün {day}",
    };
}
