using FluentAssertions;
using SIS.Infrastructure.Services.Scheduling;

namespace SIS.Tests.Scheduling;

/// <summary>
/// 2026-05-17 — CohortBuilder komşu yıl HARD pair davranışını doğrular.
///
/// Eski davranış (Soft): AdjacentYearSoftPairs'a sadece modulo eşleştirme (sec_i ↔ sec_i).
/// Yeni davranış (Hard): ConflictPairs'a section-aware kural (AddCoursePairConflicts):
///   - Bir taraf tek section ise tüm çiftler yasak
///   - Her ikisi çok section ise modulo
/// Yalnızca her iki tarafı da zorunlu olan ders çiftleri kapsanır.
/// </summary>
public class CohortBuilderAdjacentYearHardTests
{
    [Fact]
    public void Build_TwoCompulsoryCoursesInAdjacentYears_TopulatesHardConflictPairs()
    {
        var sections = new List<CohortSectionRef>
        {
            // 1. sınıf (year=0) zorunlu ders A — 3 section
            new(SectionId: 1, SectionNumber: 1, CourseId: 100, IsCompulsory: true),
            new(SectionId: 2, SectionNumber: 2, CourseId: 100, IsCompulsory: true),
            new(SectionId: 3, SectionNumber: 3, CourseId: 100, IsCompulsory: true),
            // 2. sınıf (year=1) zorunlu ders B — 1 section
            new(SectionId: 4, SectionNumber: 1, CourseId: 200, IsCompulsory: true),
        };

        var courseSemesters = new Dictionary<int, Dictionary<int, int>>
        {
            [100] = new() { [1] = 1 }, // dept 1, semester 1 → year 0
            [200] = new() { [1] = 3 }, // dept 1, semester 3 → year 1 (komşu)
        };
        var courseDepts = new Dictionary<int, HashSet<int>>
        {
            [100] = new() { 1 },
            [200] = new() { 1 },
        };

        var result = new CohortBuilder().Build(sections, courseSemesters, courseDepts);

        // Tek section + çok section kuralı: B'nin tek section'ı A'nın 3 section'ı ile pair olmalı
        // → 3 hard pair ConflictPairs içinde
        result.ConflictPairs.Should().Contain((1, 4));
        result.ConflictPairs.Should().Contain((2, 4));
        result.ConflictPairs.Should().Contain((3, 4));

        // Eski Soft pair listesi artık boş — yeni davranışta üretilmiyor
        result.AdjacentYearSoftPairs.Should().BeEmpty();
    }

    [Fact]
    public void Build_AdjacentYearElectiveCourse_NotIncludedInHardPairs()
    {
        var sections = new List<CohortSectionRef>
        {
            // 1. sınıf SEÇMELI ders — 2 section
            new(SectionId: 1, SectionNumber: 1, CourseId: 100, IsCompulsory: false),
            new(SectionId: 2, SectionNumber: 2, CourseId: 100, IsCompulsory: false),
            // 2. sınıf zorunlu ders — 1 section
            new(SectionId: 3, SectionNumber: 1, CourseId: 200, IsCompulsory: true),
        };

        var courseSemesters = new Dictionary<int, Dictionary<int, int>>
        {
            [100] = new() { [1] = 1 },
            [200] = new() { [1] = 3 },
        };
        var courseDepts = new Dictionary<int, HashSet<int>>
        {
            [100] = new() { 1 },
            [200] = new() { 1 },
        };

        var result = new CohortBuilder().Build(sections, courseSemesters, courseDepts);

        // Seçmeli ders kapsam dışı → hiçbir hard pair üretilmez
        result.ConflictPairs.Should().NotContain((1, 3));
        result.ConflictPairs.Should().NotContain((2, 3));
        result.AdjacentYearSoftPairs.Should().BeEmpty();
    }

    [Fact]
    public void Build_BothMultiSectionAdjacentYearCompulsory_ProducesModuloHardPairs()
    {
        var sections = new List<CohortSectionRef>
        {
            // 1. sınıf zorunlu — 2 section
            new(SectionId: 1, SectionNumber: 1, CourseId: 100, IsCompulsory: true),
            new(SectionId: 2, SectionNumber: 2, CourseId: 100, IsCompulsory: true),
            // 2. sınıf zorunlu — 2 section
            new(SectionId: 3, SectionNumber: 1, CourseId: 200, IsCompulsory: true),
            new(SectionId: 4, SectionNumber: 2, CourseId: 200, IsCompulsory: true),
        };

        var courseSemesters = new Dictionary<int, Dictionary<int, int>>
        {
            [100] = new() { [1] = 1 },
            [200] = new() { [1] = 3 },
        };
        var courseDepts = new Dictionary<int, HashSet<int>>
        {
            [100] = new() { 1 },
            [200] = new() { 1 },
        };

        var result = new CohortBuilder().Build(sections, courseSemesters, courseDepts);

        // Çok + çok modulo: sec_1↔sec_1, sec_2↔sec_2 yasak; diğerleri serbest
        result.ConflictPairs.Should().Contain((1, 3));
        result.ConflictPairs.Should().Contain((2, 4));
        // sec_1↔sec_2 ve sec_2↔sec_1 modulo değil → serbest
        result.ConflictPairs.Should().NotContain((1, 4));
        result.ConflictPairs.Should().NotContain((2, 3));
    }

    [Fact]
    public void Build_NonAdjacentYears_NoAdjacentPairs()
    {
        var sections = new List<CohortSectionRef>
        {
            // year 0
            new(SectionId: 1, SectionNumber: 1, CourseId: 100, IsCompulsory: true),
            // year 2 (mesafe = 2, komşu değil)
            new(SectionId: 2, SectionNumber: 1, CourseId: 200, IsCompulsory: true),
        };

        var courseSemesters = new Dictionary<int, Dictionary<int, int>>
        {
            [100] = new() { [1] = 1 }, // year 0
            [200] = new() { [1] = 5 }, // year 2
        };
        var courseDepts = new Dictionary<int, HashSet<int>>
        {
            [100] = new() { 1 },
            [200] = new() { 1 },
        };

        var result = new CohortBuilder().Build(sections, courseSemesters, courseDepts);

        // Mesafe = 2 → komşu yıl kuralı uygulanmaz
        result.ConflictPairs.Should().NotContain((1, 2));
    }

    // ── Aynı yıl seçmeli filtresi (2026-05-20) ────────────────────────────
    // Sebep: H5 cohort mode'da aynı sınıfın iki SEÇMELI dersini "öğrenci ikisini de alır"
    // diye işaretliyordu → yanlış pozitif uyarı. Komşu yıl pair'iyle simetrik mantık:
    // sadece her iki tarafı da zorunlu ders çiftleri pair'lenir.

    [Fact]
    public void Build_SameYear_BothElective_NoConflictPair()
    {
        // Aynı sınıf (year 0) iki seçmeli ders — öğrenci ikisinden birini seçer.
        var sections = new List<CohortSectionRef>
        {
            new(SectionId: 1, SectionNumber: 1, CourseId: 100, IsCompulsory: false),
            new(SectionId: 2, SectionNumber: 1, CourseId: 200, IsCompulsory: false),
        };
        var courseSemesters = new Dictionary<int, Dictionary<int, int>>
        {
            [100] = new() { [1] = 1 },
            [200] = new() { [1] = 1 },
        };
        var courseDepts = new Dictionary<int, HashSet<int>>
        {
            [100] = new() { 1 },
            [200] = new() { 1 },
        };

        var result = new CohortBuilder().Build(sections, courseSemesters, courseDepts);
        // Seçmeli + seçmeli aynı yıl → conflict pair üretilmemeli
        result.ConflictPairs.Should().BeEmpty();
    }

    [Fact]
    public void Build_SameYear_CompulsoryAndElective_NoConflictPair()
    {
        // Aynı sınıf: 1 zorunlu + 1 seçmeli — öğrenci seçmeliyi başkasıyla değiştirebilir.
        var sections = new List<CohortSectionRef>
        {
            new(SectionId: 1, SectionNumber: 1, CourseId: 100, IsCompulsory: true),
            new(SectionId: 2, SectionNumber: 1, CourseId: 200, IsCompulsory: false),
        };
        var courseSemesters = new Dictionary<int, Dictionary<int, int>>
        {
            [100] = new() { [1] = 1 },
            [200] = new() { [1] = 1 },
        };
        var courseDepts = new Dictionary<int, HashSet<int>>
        {
            [100] = new() { 1 },
            [200] = new() { 1 },
        };

        var result = new CohortBuilder().Build(sections, courseSemesters, courseDepts);
        // Karışık (zorunlu + seçmeli) → conflict pair üretilmemeli
        result.ConflictPairs.Should().BeEmpty();
    }

    [Fact]
    public void Build_SameYear_BothCompulsory_ProducesConflictPair()
    {
        // Aynı sınıf iki zorunlu ders — öğrenci ikisini de almak zorunda → çakışma gerçek sorun.
        var sections = new List<CohortSectionRef>
        {
            new(SectionId: 1, SectionNumber: 1, CourseId: 100, IsCompulsory: true),
            new(SectionId: 2, SectionNumber: 1, CourseId: 200, IsCompulsory: true),
        };
        var courseSemesters = new Dictionary<int, Dictionary<int, int>>
        {
            [100] = new() { [1] = 1 },
            [200] = new() { [1] = 1 },
        };
        var courseDepts = new Dictionary<int, HashSet<int>>
        {
            [100] = new() { 1 },
            [200] = new() { 1 },
        };

        var result = new CohortBuilder().Build(sections, courseSemesters, courseDepts);
        result.ConflictPairs.Should().Contain((1, 2));
    }
}
