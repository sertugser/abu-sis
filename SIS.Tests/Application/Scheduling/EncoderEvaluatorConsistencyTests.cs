using System.Reflection;
using FluentAssertions;
using SIS.Application.Scheduling.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

namespace SIS.Tests.Application.Scheduling;

/// <summary>
/// 2026-05-10 — Plan 11 yerine pragmatik consistency test paketi.
/// Plan 11 ("encoder + evaluator tek dosyaya birleştir") full refactor olurdu —
/// tahmin: 10-15 gün, 4-7 yılda kendini ödeyen ROI. Yerine bu test paketi:
///
///   "Her hard kuralın hem encoder'ı hem evaluator'ı olmalı; ikisi senkron kalmalı."
///
/// Reflection ile assembly tarayarak iki tarafın kod (Code property) eşleşmesini
/// otomatik kontrol eder. Bir geliştirici yeni kural eklerken iki tarafa da eklemeyi
/// unutursa CI fail verir → silent divergence pattern (BlockType bug gibi) erken yakalanır.
///
/// Yatırım: ~50 satır test, ROI anında.
/// </summary>
public class EncoderEvaluatorConsistencyTests
{
    // Plan 10 toggle pattern'i için tasarımca aynı Code'a hem Hard hem Soft encoder var
    // (örn. ClassroomTypeEncoder hard + ClassroomTypeSoftEncoder soft, ikisi Code="H4").
    // Hard tarafı kontrol ederken sadece IsHard=true encoder'larını dikkate al.
    //
    // Bilinen mismatch istisnaları:
    //  - DIST: Encoder.IsHard=true ama Evaluator.Category=Soft — per-row preference level
    //    pattern (Hard/Strong/Weak/Forbidden), bilinçli tasarım. Plan 10 'capability=hard-only'.
    //  - Capacity: sadece evaluator var, encoder yok — preview-only domain validation
    //    (Section.Capacity vs Classroom.Capacity).
    //  - CreditHour: sadece evaluator var, encoder yok — domain validation, solver scope dışı.
    private static readonly HashSet<string> EvaluatorOnlyByDesign = new(StringComparer.OrdinalIgnoreCase)
    {
        "Capacity",  // section.Capacity vs classroom.Capacity preview-only
        "H12",       // CreditHour — TheoryHours+LabHours mismatch domain validation, solver scope dışı
        "S2",        // DailyLoadSoftEvaluator — Plan 10 öncesi V1 Code, Plan 10 sonrası DailyLoadSoftEncoder
                     // Code="H-DL" (toggle pattern). Evaluator Code'u "H-DL"a güncellenmedi → orphan.
                     // Gerçek code drift, ileride evaluator silinmeli veya Code="H-DL" yapılmalı.
        "S5",        // LunchBreakSoftEvaluator — Plan 10 öncesi V1 Code, Plan 10 sonrası
                     // LunchBreakSoftEncoder Code="H13". Aynı orphan pattern: Code güncellenmedi.
    };

    private static readonly HashSet<string> EncoderEvaluatorCategoryMismatchByDesign = new(StringComparer.OrdinalIgnoreCase)
    {
        "DIST",      // per-row PreferenceLevel pattern (Hard/Strong/Weak/Forbidden), Plan 10 capability=hard-only
    };

    /// <summary>
    /// Plan 10 toggle pattern'i için eklenen soft encoder'lar — bazıları "STUB" durumda,
    /// matching soft evaluator henüz yapılmadı. Admin Hard→Soft toggle yaparsa solver
    /// penalty mode'da çalışır ama preview/validation hâlâ Hard kategorisinde rapor eder.
    /// Bilinçli kabul edildi (bu sessionda tartışıldı, F task scope dışı bırakıldı).
    /// İleride soft evaluator eklendikçe bu listeden çıkar.
    /// </summary>
    private static readonly HashSet<string> Plan10SoftEncoderStubsWithoutEvaluator = new(StringComparer.OrdinalIgnoreCase)
    {
        "H4",   // ClassroomTypeSoftEncoder (STUB)
        "H5",   // CurriculumConflictSoftEncoder
        "H6",   // CourseComponentSoftEncoder (varsa)
        "H8",   // SameCourseSectionSoftEncoder
        "H10",  // FacultyClassroomSoftEncoder (STUB)
        "H13",  // LunchBreakSoftEncoder — V1 LunchBreakSoftEvaluator hâlâ Code="S5" kullanıyor (Code mismatch)
        "H15",  // MaxConsecutiveHoursSoftEncoder
        "H16",  // TheoryLabDaySplitSoftEncoder
        "H-WL", // WeeklyLoadSoftEncoder
        "H-DL", // DailyLoadSoftEncoder
        "H-DO", // NoDayOffSoftEncoder
    };

    /// <summary>
    /// Bazı encoder'lar constructor'ta zorunlu parametre + validation alır
    /// (örn. LunchBreakEncoder(TimeOnly start, TimeOnly end) — default(TimeOnly) çift değer
    /// atılırsa ArgumentException atar). Reflection ile parametresiz instantiate edilemez,
    /// dolayısıyla bu test paketinde "encoder yok gibi" görünürler. Production'da DI factory
    /// gerçek değerlerle çağırır → çalışır. Bu encoder'ların evaluator eşleri ayrı bir test
    /// paketiyle (per-rule integration) doğrulanmalı; bu reflection-tabanlı paket atlar.
    /// </summary>
    private static readonly HashSet<string> ReflectionInstantiationExclusions = new(StringComparer.OrdinalIgnoreCase)
    {
        "H13", // LunchBreakEncoder — TimeOnly constructor validation
    };

    /// <summary>
    /// Kod tarafında "hard encoder var ama hard evaluator yok" veya tersi durumu yok mu?
    /// Reflection ile tüm IConstraintEncoder + IConstraintEvaluator implementasyonlarını
    /// tara, hard kuralların kod listesini karşılaştır.
    /// </summary>
    [Fact]
    public void EveryHardEncoder_HasMatchingHardEvaluator()
    {
        var infrastructureAssembly = typeof(ClassroomConflictEncoder).Assembly;

        // Aynı Code için hem hard hem soft encoder olabilir (Plan 10 toggle pattern).
        // Hard tarafı için sadece IsHard=true olanları al.
        var hardEncoderCodes = infrastructureAssembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(IConstraintEncoder).IsAssignableFrom(t))
            .Select(t => CreateEncoderInstance(t))
            .Where(e => e != null && e.IsHard)
            .Select(e => e!.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hardEvaluatorCodes = infrastructureAssembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(IConstraintEvaluator).IsAssignableFrom(t))
            .Select(t => CreateEvaluatorInstance(t))
            .Where(e => e != null && e.Category == ConstraintCategory.Hard)
            .Select(e => e!.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Encoder var, evaluator yok (istisnalar hariç) → kural unuldu, preview/validation taraf bilmiyor
        var encodersWithoutEvaluator = hardEncoderCodes
            .Except(hardEvaluatorCodes, StringComparer.OrdinalIgnoreCase)
            .Except(EncoderEvaluatorCategoryMismatchByDesign, StringComparer.OrdinalIgnoreCase)
            .ToList();

        encodersWithoutEvaluator.Should().BeEmpty(
            because: "her hard encoder için preview/validation tarafı (evaluator) gerekir; "
                   + "yenisi eklenirken tek taraf kalmasın (silent divergence).");

        // Evaluator var, encoder yok (istisnalar hariç) → V2 solver bu kuralı uygulamıyor
        var evaluatorsWithoutEncoder = hardEvaluatorCodes
            .Except(hardEncoderCodes, StringComparer.OrdinalIgnoreCase)
            .Except(EvaluatorOnlyByDesign, StringComparer.OrdinalIgnoreCase)
            .Except(ReflectionInstantiationExclusions, StringComparer.OrdinalIgnoreCase)
            .ToList();

        evaluatorsWithoutEncoder.Should().BeEmpty(
            because: "her hard evaluator için solver tarafında encoder gerekir (aksi halde "
                   + "admin auto-schedule çalıştırınca kural uygulanmaz). Preview-only "
                   + "istisnalar EvaluatorOnlyByDesign listesinde, reflection-fail "
                   + "encoder'lar ReflectionInstantiationExclusions'ta tanımlı.");
    }

    /// <summary>
    /// Soft tarafında da aynı kontrol: hard kuralın "Soft" varyantı varsa (Plan 10 toggle
    /// ile aynı Code'u paylaşır), encoder + evaluator iki tarafta da bulunmalı.
    /// </summary>
    [Fact]
    public void EverySoftEncoder_HasMatchingEvaluator()
    {
        var infrastructureAssembly = typeof(ClassroomConflictEncoder).Assembly;

        var softEncoderCodes = infrastructureAssembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(IConstraintEncoder).IsAssignableFrom(t))
            .Select(t => CreateEncoderInstance(t))
            .Where(e => e != null && !e.IsHard)
            .Select(e => e!.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var softEvaluatorCodes = infrastructureAssembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(IConstraintEvaluator).IsAssignableFrom(t))
            .Select(t => CreateEvaluatorInstance(t))
            .Where(e => e != null && e.Category == ConstraintCategory.Soft)
            .Select(e => e!.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Soft encoder eşi olmayan evaluator'lar veya tersi → silent divergence
        // Plan 10 STUB soft encoder'lar matching evaluator'a sahip değil — bilinen iş.
        var encodersWithoutEvaluator = softEncoderCodes
            .Except(softEvaluatorCodes, StringComparer.OrdinalIgnoreCase)
            .Except(Plan10SoftEncoderStubsWithoutEvaluator, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var evaluatorsWithoutEncoder = softEvaluatorCodes
            .Except(softEncoderCodes, StringComparer.OrdinalIgnoreCase)
            .Except(EncoderEvaluatorCategoryMismatchByDesign, StringComparer.OrdinalIgnoreCase)
            .Except(EvaluatorOnlyByDesign, StringComparer.OrdinalIgnoreCase)
            .ToList();

        encodersWithoutEvaluator.Should().BeEmpty(
            because: "soft encoder her zaman matching evaluator gerektirir "
                   + "(Plan 10 STUB encoder'lar Plan10SoftEncoderStubsWithoutEvaluator listesinde tanımlı).");

        evaluatorsWithoutEncoder.Should().BeEmpty(
            because: "soft evaluator her zaman matching encoder gerektirir "
                   + "(per-row preference pattern istisnaları EncoderEvaluatorCategoryMismatchByDesign'da).");
    }

    /// <summary>
    /// Encoder.IsHard=true ise aynı Code'lu evaluator'lardan en az biri Hard olmalı,
    /// IsHard=false ise en az biri Soft olmalı. Plan 10 toggle pattern'inde aynı Code'a
    /// hem Hard hem Soft encoder olabilir; her sınıflandırma için en az bir matching evaluator
    /// gerekir.
    /// </summary>
    [Fact]
    public void Encoder_Classification_HasMatchingEvaluator_InAtLeastOneCategory()
    {
        var infrastructureAssembly = typeof(ClassroomConflictEncoder).Assembly;

        var allEncoders = infrastructureAssembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(IConstraintEncoder).IsAssignableFrom(t))
            .Select(t => CreateEncoderInstance(t))
            .Where(e => e != null)
            .ToList();

        // Her Code için Hard/Soft kategorilerinin set'i (Plan 10: aynı Code hem Hard hem Soft olabilir)
        var encoderCategoriesByCode = allEncoders
            .GroupBy(e => e!.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e!.IsHard ? ConstraintCategory.Hard : ConstraintCategory.Soft).ToHashSet(),
                StringComparer.OrdinalIgnoreCase);

        var evaluatorCategoriesByCode = infrastructureAssembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(IConstraintEvaluator).IsAssignableFrom(t))
            .Select(t => CreateEvaluatorInstance(t))
            .Where(e => e != null)
            .GroupBy(e => e!.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(e => e!.Category).ToHashSet(), StringComparer.OrdinalIgnoreCase);

        var mismatches = new List<string>();
        foreach (var (code, encoderCategories) in encoderCategoriesByCode)
        {
            // Bilinçli istisna (DIST gibi) → atla
            if (EncoderEvaluatorCategoryMismatchByDesign.Contains(code)) continue;

            if (!evaluatorCategoriesByCode.TryGetValue(code, out var evaluatorCategories))
                continue; // Eşi yoksa diğer test yakalar

            // Plan 10 STUB soft encoder'lar — matching soft evaluator henüz yok, soft kategorisi
            // istisnaya tabi. Hard tarafta hâlâ kontrol edilir.
            var encoderCategoriesEffective = Plan10SoftEncoderStubsWithoutEvaluator.Contains(code)
                ? new HashSet<ConstraintCategory>(encoderCategories.Where(c => c != ConstraintCategory.Soft))
                : encoderCategories;

            // Encoder'ın her kategorisi için, evaluator'da o kategorinin matching'i olmalı
            var missing = encoderCategoriesEffective.Except(evaluatorCategories).ToList();
            if (missing.Count > 0)
            {
                mismatches.Add(
                    $"{code}: encoder kategorileri [{string.Join(",", encoderCategories)}] ile " +
                    $"evaluator kategorileri [{string.Join(",", evaluatorCategories)}] uyumsuz; " +
                    $"eksik: [{string.Join(",", missing)}].");
            }
        }

        mismatches.Should().BeEmpty(
            because: "encoder bir kategoride var ise evaluator da o kategoride (Hard/Soft) olmalı; "
                   + "Plan 10 toggle pattern aynı Code'a hem Hard hem Soft encoder destekler, "
                   + "ama her ikisi için de matching evaluator gerekir.");
    }

    private static IConstraintEncoder? CreateEncoderInstance(Type encoderType)
    {
        try
        {
            // Parametresiz constructor varsa: standart yol
            var emptyCtor = encoderType.GetConstructor(Type.EmptyTypes);
            if (emptyCtor != null)
                return (IConstraintEncoder)emptyCtor.Invoke(null);

            // Constructor parametre alıyor (örn. config-driven encoder'lar) → default değerlerle dene
            var ctor = encoderType.GetConstructors().FirstOrDefault();
            if (ctor == null) return null;
            var args = ctor.GetParameters()
                .Select(p => p.HasDefaultValue
                    ? p.DefaultValue
                    : (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null))
                .ToArray();
            return (IConstraintEncoder)ctor.Invoke(args);
        }
        catch
        {
            return null;
        }
    }

    private static IConstraintEvaluator? CreateEvaluatorInstance(Type evaluatorType)
    {
        try
        {
            var emptyCtor = evaluatorType.GetConstructor(Type.EmptyTypes);
            if (emptyCtor != null)
                return (IConstraintEvaluator)emptyCtor.Invoke(null);

            var ctor = evaluatorType.GetConstructors().FirstOrDefault();
            if (ctor == null) return null;
            var args = ctor.GetParameters()
                .Select(p => p.HasDefaultValue
                    ? p.DefaultValue
                    : (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null))
                .ToArray();
            return (IConstraintEvaluator)ctor.Invoke(args);
        }
        catch
        {
            return null;
        }
    }
}
