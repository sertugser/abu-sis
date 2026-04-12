using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class GpaCalculationServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly GpaCalculationService _service;

    // Ayşe — 2 dönem, tüm GPA dersleri BB
    private const int AyseId = 1;
    // Emre — 4 dönem, karışık notlar, 2 FF
    private const int EmreId = 2;

    private const int Term1Id = 1; // 2024-2025 Güz
    private const int Term2Id = 2; // 2024-2025 Bahar
    private const int Term3Id = 3; // 2023-2024 Güz (Emre)
    private const int Term4Id = 4; // 2023-2024 Bahar (Emre)

    public GpaCalculationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _service = new GpaCalculationService(_context);

        SeedBaseData();
    }

    // ══════════════════════════════════════════════════════════
    //  SGPA TESTLERİ
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task CalculateSgpa_AyseSemester1_AllBB_Returns3()
    {
        // Ayşe'nin 1. dönemi: 5 GPA dersi hep BB (3.00), 4 non-GPA dersi S
        // AKTS: CENG101=6, MATH101=6, PHY101=5, PHY103=1, BIO101=5 → toplam 23 GPA AKTS
        // T.AKTS: 23 × 3.00 = 69
        // SGPA = 69 / 23 = 3.00
        var sgpa = await _service.CalculateSgpaAsync(AyseId, Term1Id);

        sgpa.Should().Be(3.00m);
    }

    [Fact]
    public async Task CalculateSgpa_EmreSemester1_MixedGrades()
    {
        // Emre'nin 1. dönemi (2023-2024 Güz):
        // CENG101=CB(2.50)×6=15, MATH101=CC(2.00)×6=12, PHY101=CB(2.50)×5=12.5,
        // PHY103=BB(3.00)×1=3, BIO101=CB(2.50)×5=12.5
        // S notları (OHS101, ENG101, HIS101, TUR101) → GPA dışı
        // Toplam AKTS = 6+6+5+1+5 = 23
        // Toplam T.AKTS = 15+12+12.5+3+12.5 = 55
        // SGPA = 55/23 = 2.39 (yuvarlanmış)
        var sgpa = await _service.CalculateSgpaAsync(EmreId, Term3Id);

        sgpa.Should().Be(2.39m);
    }

    [Fact]
    public async Task CalculateSgpa_EmreSemester3_WithFF()
    {
        // Emre'nin 3. dönemi (2024-2025 Güz):
        // EEE203=DC(1.50)×5=7.5, EEE281=FF(0)×5=0,
        // CENG213=CC(2.00)×5=10, CENG223=DC(1.50)×5=7.5,
        // CENG241=DC(1.50)×6=9
        // ENG201=S → GPA dışı
        // Toplam AKTS = 5+5+5+5+6 = 26
        // Toplam T.AKTS = 7.5+0+10+7.5+9 = 34
        // SGPA = 34/26 = 1.31 (yuvarlanmış)
        var sgpa = await _service.CalculateSgpaAsync(EmreId, Term1Id);

        sgpa.Should().Be(1.31m);
    }

    // ══════════════════════════════════════════════════════════
    //  CGPA TESTLERİ
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task CalculateCgpa_Ayse_AllBB_Returns3()
    {
        // Ayşe'nin 2 dönemi — tüm GPA dersleri BB
        // Tüm dersler farklı, tekrar yok
        // Her ders BB = 3.00
        // CGPA = 3.00
        var cgpa = await _service.CalculateCgpaAsync(AyseId);

        cgpa.Should().Be(3.00m);
    }

    [Fact]
    public async Task CalculateCgpa_Emre_FourSemesters_NoRetakes()
    {
        // Emre 4 dönem boyunca hiçbir dersi tekrar almamış (tüm dersler farklı)
        // Elle hesaplama:
        // Sem1 GPA dersleri: CB×6 + CC×6 + CB×5 + BB×1 + CB×5 = 15+12+12.5+3+12.5 = 55 (AKTS=23)
        // Sem2 GPA dersleri: DC×8 + DC×6 + CC×5 + DC×1 = 12+9+10+1.5 = 32.5 (AKTS=20) — wait
        // Sem2: CENG102=DC(1.50)×8=12, MATH102=DC(1.50)×6=9, PHY102=CC(2.00)×5=10, PHY104=DC(1.50)×1=1.5
        //       OHS102=S, ENG102=S, HIS102=S, TUR102=S → GPA dışı
        //       Toplam = 32.5, AKTS = 20
        // Sem3: 34 (AKTS=26) — yukarıdaki hesaptan
        // Sem4: CENG214=DC(1.50)×6=9, CENG256=FF(0)×6=0, MATH224=CC(2.00)×5=10,
        //       MATH206=DC(1.50)×5=7.5, SENG264=DC(1.50)×6=9
        //       Toplam = 35.5, AKTS = 28
        // Genel: (55+32.5+34+35.5) / (23+20+26+28) = 157/97 = 1.62 (yuvarlanmış)
        var cgpa = await _service.CalculateCgpaAsync(EmreId);

        cgpa.Should().Be(1.62m);
    }

    [Fact]
    public async Task CalculateCgpa_WithRetake_OnlyLatestGradeCounts()
    {
        // Yeni öğrenci oluştur — bir dersi tekrar almış
        var studentId = 99;
        _context.Students.Add(new Student
        {
            Id = studentId, UserId = 999, StudentNumber = "9999999",
            NationalId = "99999999999", Status = StudentStatus.Active,
            Cgpa = 0, Sgpa = 0, CompletedSemesters = 2
        });

        // Ders: AKTS=6
        var course = new Course { Id = 900, Code = "TEST900", Name = "Test Course", Ects = 6, IsNonGpa = false };
        _context.Courses.Add(course);

        // Dönem 1'de FF aldı
        var section1 = AddSection(900, course.Id, Term3Id);
        AddRegistration(900, studentId, section1.Id, RegistrationStatus.Active, GradeCode.FF, Term3Id);

        // Dönem 2'de tekrar alıp BB aldı
        var section2 = AddSection(901, course.Id, Term4Id);
        AddRegistration(901, studentId, section2.Id, RegistrationStatus.Active, GradeCode.BB, Term4Id);

        // Başka bir ders: AKTS=5, CC aldı (tek sefer)
        var course2 = new Course { Id = 901, Code = "TEST901", Name = "Test Course 2", Ects = 5, IsNonGpa = false };
        _context.Courses.Add(course2);
        var section3 = AddSection(902, course2.Id, Term3Id);
        AddRegistration(902, studentId, section3.Id, RegistrationStatus.Active, GradeCode.CC, Term3Id);

        _context.SaveChanges();

        var cgpa = await _service.CalculateCgpaAsync(studentId);

        // FF silinmeli, sadece BB geçerli:
        // TEST900: BB(3.00) × 6 = 18 (FF olan 0×6=0 yok sayılır)
        // TEST901: CC(2.00) × 5 = 10
        // Toplam: 28 / 11 = 2.55 (yuvarlanmış)
        cgpa.Should().Be(2.55m);
    }

    [Fact]
    public async Task CalculateCgpa_WithEquivalentCourseRetake_OnlyLatestEquivalentCounts()
    {
        var studentId = 97;
        _context.Students.Add(new Student
        {
            Id = studentId, UserId = 997, StudentNumber = "9999997",
            NationalId = "99999999997", Status = StudentStatus.Active,
            Cgpa = 0, Sgpa = 0, CompletedSemesters = 2
        });

        var oldCourse = new Course { Id = 960, Code = "TEST960", Name = "Legacy Course", Ects = 6, IsNonGpa = false };
        var newCourse = new Course { Id = 961, Code = "TEST961", Name = "Equivalent Course", Ects = 6, IsNonGpa = false };
        var extraCourse = new Course { Id = 962, Code = "TEST962", Name = "Extra Course", Ects = 4, IsNonGpa = false };
        _context.Courses.AddRange(oldCourse, newCourse, extraCourse);
        _context.CourseEquivalencies.Add(new CourseEquivalency
        {
            CourseId = oldCourse.Id,
            EquivalentCourseId = newCourse.Id
        });

        var section1 = AddSection(960, oldCourse.Id, Term3Id);
        AddRegistration(960, studentId, section1.Id, RegistrationStatus.Active, GradeCode.FF, Term3Id);

        var section2 = AddSection(961, newCourse.Id, Term4Id);
        AddRegistration(961, studentId, section2.Id, RegistrationStatus.Active, GradeCode.BB, Term4Id);

        var section3 = AddSection(962, extraCourse.Id, Term3Id);
        AddRegistration(962, studentId, section3.Id, RegistrationStatus.Active, GradeCode.CC, Term3Id);

        _context.SaveChanges();

        var cgpa = await _service.CalculateCgpaAsync(studentId);

        // TEST960/TEST961 eşdeğer zinciri: sadece BB(3.00) × 6 = 18
        // TEST962: CC(2.00) × 4 = 8
        // Toplam: 26 / 10 = 2.60
        cgpa.Should().Be(2.60m);
    }

    [Fact]
    public async Task CalculateCgpa_NonGpaCourses_Excluded()
    {
        // Sadece S/U notlu dersleri olan öğrenci → CGPA = 0
        var studentId = 98;
        _context.Students.Add(new Student
        {
            Id = studentId, UserId = 998, StudentNumber = "9999998",
            NationalId = "99999999998", Status = StudentStatus.Active,
            Cgpa = 0, Sgpa = 0, CompletedSemesters = 1
        });

        var course = new Course { Id = 950, Code = "TEST950", Name = "Non-GPA Course", Ects = 2, IsNonGpa = true };
        _context.Courses.Add(course);
        var section = AddSection(950, course.Id, Term1Id);
        AddRegistration(950, studentId, section.Id, RegistrationStatus.Active, GradeCode.S, Term1Id);

        _context.SaveChanges();

        var cgpa = await _service.CalculateCgpaAsync(studentId);

        cgpa.Should().Be(0m);
    }

    [Fact]
    public async Task GetTermGpaHistory_Emre_ReturnsChronologicalAnoAndAgno()
    {
        var history = await _service.GetTermGpaHistoryAsync(EmreId);

        history.Should().HaveCount(4);
        history.Select(item => item.TermId).Should().ContainInOrder(Term3Id, Term4Id, Term1Id, Term2Id);

        history[0].Sgpa.Should().Be(2.39m);
        history[0].Cgpa.Should().Be(2.39m);
        history[1].Sgpa.Should().Be(1.63m);
        history[1].Cgpa.Should().Be(2.03m);
        history[2].Sgpa.Should().Be(1.31m);
        history[2].Cgpa.Should().Be(1.76m);
        history[3].Sgpa.Should().Be(1.27m);
        history[3].Cgpa.Should().Be(1.62m);
    }

    [Fact]
    public async Task GetTermGpaHistory_IncludesWithdrawnRegistrationsWithGrades()
    {
        var studentId = 96;
        _context.Students.Add(new Student
        {
            Id = studentId, UserId = 996, StudentNumber = "9999996",
            NationalId = "99999999996", Status = StudentStatus.Active,
            Cgpa = 0, Sgpa = 0, CompletedSemesters = 2
        });

        var course1 = new Course { Id = 970, Code = "TEST970", Name = "History Term 1", Ects = 6, IsNonGpa = false };
        var course2 = new Course { Id = 971, Code = "TEST971", Name = "History Term 2", Ects = 5, IsNonGpa = false };
        _context.Courses.AddRange(course1, course2);

        var section1 = AddSection(970, course1.Id, Term3Id);
        AddRegistration(970, studentId, section1.Id, RegistrationStatus.Withdrawn, GradeCode.BB, Term3Id);

        var section2 = AddSection(971, course2.Id, Term4Id);
        AddRegistration(971, studentId, section2.Id, RegistrationStatus.Active, GradeCode.CC, Term4Id);

        _context.SaveChanges();

        var history = await _service.GetTermGpaHistoryAsync(studentId);

        history.Should().HaveCount(2);
        history[0].TermId.Should().Be(Term3Id);
        history[0].Sgpa.Should().Be(3.00m);
        history[0].Cgpa.Should().Be(3.00m);
        history[1].TermId.Should().Be(Term4Id);
        history[1].Sgpa.Should().Be(2.00m);
        history[1].Cgpa.Should().Be(2.55m);
    }

    // ══════════════════════════════════════════════════════════
    //  RecalculateAndUpdate TESTİ
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task RecalculateAndUpdate_UpdatesStudentRecord()
    {
        var result = await _service.RecalculateAndUpdateAsync(AyseId);

        // Ayşe'nin Student kaydı güncellenmeli
        var student = await _context.Students.FindAsync(AyseId);

        student!.Cgpa.Should().Be(result.Cgpa);
        student.Sgpa.Should().Be(result.Sgpa);
        result.Cgpa.Should().Be(3.00m);
        result.TotalAkts.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RecalculateAndUpdate_UsesLatestTermStartForSgpa()
    {
        const int oldTermId = 80;
        const int newTermId = 70;
        const int studentId = 95;

        _context.AcademicTerms.AddRange(
            new AcademicTerm
            {
                Id = oldTermId,
                Name = "2024 Yaz",
                Year = 2024,
                Semester = SemesterType.Summer,
                RegistrationStart = new DateTime(2024, 6, 1),
                RegistrationEnd = new DateTime(2024, 6, 10),
                AddDropStart = new DateTime(2024, 6, 1),
                AddDropEnd = new DateTime(2024, 6, 10),
                WithdrawalDeadline = new DateTime(2024, 7, 1),
                TermStart = new DateTime(2024, 6, 15),
                TermEnd = new DateTime(2024, 8, 1),
                IsActive = false
            },
            new AcademicTerm
            {
                Id = newTermId,
                Name = "2025 Bahar",
                Year = 2025,
                Semester = SemesterType.Spring,
                RegistrationStart = new DateTime(2025, 1, 1),
                RegistrationEnd = new DateTime(2025, 1, 10),
                AddDropStart = new DateTime(2025, 1, 1),
                AddDropEnd = new DateTime(2025, 1, 10),
                WithdrawalDeadline = new DateTime(2025, 3, 1),
                TermStart = new DateTime(2025, 2, 10),
                TermEnd = new DateTime(2025, 6, 1),
                IsActive = false
            });

        _context.Students.Add(new Student
        {
            Id = studentId, UserId = 995, StudentNumber = "9999995",
            NationalId = "99999999995", Status = StudentStatus.Active,
            Cgpa = 0, Sgpa = 0, CompletedSemesters = 2
        });

        var oldCourse = new Course { Id = 980, Code = "TEST980", Name = "Old Term Course", Ects = 6, IsNonGpa = false };
        var newCourse = new Course { Id = 981, Code = "TEST981", Name = "New Term Course", Ects = 6, IsNonGpa = false };
        _context.Courses.AddRange(oldCourse, newCourse);

        var oldSection = AddSection(980, oldCourse.Id, oldTermId);
        AddRegistration(980, studentId, oldSection.Id, RegistrationStatus.Active, GradeCode.BB, oldTermId);

        var newSection = AddSection(981, newCourse.Id, newTermId);
        AddRegistration(981, studentId, newSection.Id, RegistrationStatus.Active, GradeCode.FF, newTermId);

        _context.SaveChanges();

        var result = await _service.RecalculateAndUpdateAsync(studentId);

        result.Sgpa.Should().Be(0m);
        result.Cgpa.Should().Be(1.50m);

        var student = await _context.Students.FindAsync(studentId);
        student!.Sgpa.Should().Be(0m);
        student.Cgpa.Should().Be(1.50m);
    }

    // ══════════════════════════════════════════════════════════
    //  SEED DATA
    // ══════════════════════════════════════════════════════════

    private void SeedBaseData()
    {
        var term3Start = new DateTime(2023, 9, 18);
        var term4Start = new DateTime(2024, 2, 19);
        var term1Start = new DateTime(2024, 9, 16);
        var term2Start = new DateTime(2025, 2, 17);

        // Dönemler
        _context.AcademicTerms.AddRange(
            new AcademicTerm { Id = Term1Id, Name = "2024-2025 Güz", Year = 2024, Semester = SemesterType.Fall,
                RegistrationStart = term1Start.AddDays(-14), RegistrationEnd = term1Start.AddDays(-7), AddDropStart = term1Start,
                AddDropEnd = term1Start.AddDays(7), WithdrawalDeadline = term1Start.AddDays(49), TermStart = term1Start, TermEnd = term1Start.AddMonths(4), IsActive = false },
            new AcademicTerm { Id = Term2Id, Name = "2024-2025 Bahar", Year = 2024, Semester = SemesterType.Spring,
                RegistrationStart = term2Start.AddDays(-14), RegistrationEnd = term2Start.AddDays(-7), AddDropStart = term2Start,
                AddDropEnd = term2Start.AddDays(7), WithdrawalDeadline = term2Start.AddDays(49), TermStart = term2Start, TermEnd = term2Start.AddMonths(4), IsActive = true },
            new AcademicTerm { Id = Term3Id, Name = "2023-2024 Güz", Year = 2023, Semester = SemesterType.Fall,
                RegistrationStart = term3Start.AddDays(-14), RegistrationEnd = term3Start.AddDays(-7), AddDropStart = term3Start,
                AddDropEnd = term3Start.AddDays(7), WithdrawalDeadline = term3Start.AddDays(49), TermStart = term3Start, TermEnd = term3Start.AddMonths(4), IsActive = false },
            new AcademicTerm { Id = Term4Id, Name = "2023-2024 Bahar", Year = 2023, Semester = SemesterType.Spring,
                RegistrationStart = term4Start.AddDays(-14), RegistrationEnd = term4Start.AddDays(-7), AddDropStart = term4Start,
                AddDropEnd = term4Start.AddDays(7), WithdrawalDeadline = term4Start.AddDays(49), TermStart = term4Start, TermEnd = term4Start.AddMonths(4), IsActive = false }
        );

        // Öğrenciler
        _context.Students.AddRange(
            new Student { Id = AyseId, UserId = 100, StudentNumber = "2101001", NationalId = "11111111111",
                Status = StudentStatus.Active, Cgpa = 2.85m, Sgpa = 3.00m, CompletedSemesters = 2 },
            new Student { Id = EmreId, UserId = 200, StudentNumber = "2201001", NationalId = "22222222222",
                Status = StudentStatus.Active, Cgpa = 1.62m, Sgpa = 1.27m, CompletedSemesters = 4 }
        );

        // ── Ayşe'nin Dersleri (Seed data ile aynı — tüm GPA dersleri BB) ──
        // Semester 1 dersleri
        var ayseCourses1 = new (int id, string code, string name, int ects, bool nonGpa)[]
        {
            (1, "CENG101", "Algorithms and Programming with Java I", 6, false),
            (2, "OHS101", "Occupational Health and Safety I", 1, true),
            (3, "ENG101", "Academic English I", 2, true),
            (4, "MATH101", "Calculus I", 6, false),
            (5, "PHY101", "Physics I", 5, false),
            (6, "PHY103", "Physics I Lab", 1, false),
            (7, "BIO101", "Molecular Biology", 5, false),
            (8, "HIS101", "Ataturk History I", 2, true),
            (9, "TUR101", "Turkish I", 2, true),
        };

        foreach (var (id, code, name, ects, nonGpa) in ayseCourses1)
        {
            _context.Courses.Add(new Course { Id = id, Code = code, Name = name, Ects = ects, IsNonGpa = nonGpa });
            var section = AddSection(id, id, Term1Id);
            // GPA dışı dersler S, GPA dersleri BB
            var grade = nonGpa ? GradeCode.S : GradeCode.BB;
            AddRegistration(id, AyseId, section.Id, RegistrationStatus.Active, grade, Term1Id);
        }

        // Semester 2 dersleri
        var ayseCourses2 = new (int id, string code, string name, int ects, bool nonGpa)[]
        {
            (10, "CENG102", "Algorithms and Programming with Java II", 8, false),
            (11, "OHS102", "Occupational Health and Safety II", 1, true),
            (12, "ENG102", "Academic English II", 2, true),
            (13, "MATH102", "Calculus II", 6, false),
            (14, "PHY102", "Physics II", 5, false),
            (15, "PHY104", "Physics II Lab", 1, false),
            (16, "HIS102", "Ataturk History II", 2, true),
            (17, "TUR102", "Turkish II", 2, true),
        };

        foreach (var (id, code, name, ects, nonGpa) in ayseCourses2)
        {
            _context.Courses.Add(new Course { Id = id, Code = code, Name = name, Ects = ects, IsNonGpa = nonGpa });
            var section = AddSection(id + 100, id, Term2Id);
            var grade = nonGpa ? GradeCode.S : GradeCode.BB;
            AddRegistration(id + 100, AyseId, section.Id, RegistrationStatus.Active, grade, Term2Id);
        }

        // ── Emre'nin Dersleri (Seed data ile aynı) ──
        // Semester 1 (2023-2024 Güz): CB, S, S, CC, CB, BB, CB, S, S
        var emreSem1 = new (string code, GradeCode grade)[]
        {
            ("CENG101", GradeCode.CB), ("OHS101", GradeCode.S),
            ("ENG101", GradeCode.S),   ("MATH101", GradeCode.CC),
            ("PHY101", GradeCode.CB),  ("PHY103", GradeCode.BB),
            ("BIO101", GradeCode.CB),  ("HIS101", GradeCode.S),
            ("TUR101", GradeCode.S)
        };

        int emreSectionId = 200;
        int emreRegId = 200;
        foreach (var (code, grade) in emreSem1)
        {
            var course = _context.Courses.Local.First(c => c.Code == code);
            var section = AddSection(emreSectionId++, course.Id, Term3Id);
            AddRegistration(emreRegId++, EmreId, section.Id, RegistrationStatus.Active, grade, Term3Id);
        }

        // Semester 2 (2023-2024 Bahar): DC, S, S, DC, CC, DC, S, S
        var emreSem2 = new (string code, GradeCode grade)[]
        {
            ("CENG102", GradeCode.DC), ("OHS102", GradeCode.S),
            ("ENG102", GradeCode.S),   ("MATH102", GradeCode.DC),
            ("PHY102", GradeCode.CC),  ("PHY104", GradeCode.DC),
            ("HIS102", GradeCode.S),   ("TUR102", GradeCode.S)
        };

        foreach (var (code, grade) in emreSem2)
        {
            var course = _context.Courses.Local.First(c => c.Code == code);
            var section = AddSection(emreSectionId++, course.Id, Term4Id);
            AddRegistration(emreRegId++, EmreId, section.Id, RegistrationStatus.Active, grade, Term4Id);
        }

        // Semester 3 (2024-2025 Güz): DC, FF, CC, DC, DC, S
        var emreSem3Courses = new (int id, string code, string name, int ects, bool nonGpa)[]
        {
            (30, "EEE203", "Digital Design", 5, false),
            (31, "EEE281", "Electrical Circuits", 5, false),
            (32, "CENG213", "Data Structures", 5, false),
            (33, "CENG223", "Discrete Math", 5, false),
            (34, "CENG241", "Programming Languages", 6, false),
            (35, "ENG201", "Academic English III", 2, true),
        };

        foreach (var (id, code, name, ects, nonGpa) in emreSem3Courses)
            _context.Courses.Add(new Course { Id = id, Code = code, Name = name, Ects = ects, IsNonGpa = nonGpa });

        var emreSem3Grades = new (string code, GradeCode grade)[]
        {
            ("EEE203", GradeCode.DC),  ("EEE281", GradeCode.FF),
            ("CENG213", GradeCode.CC), ("CENG223", GradeCode.DC),
            ("CENG241", GradeCode.DC), ("ENG201", GradeCode.S)
        };

        foreach (var (code, grade) in emreSem3Grades)
        {
            var course = _context.Courses.Local.First(c => c.Code == code);
            var section = AddSection(emreSectionId++, course.Id, Term1Id);
            AddRegistration(emreRegId++, EmreId, section.Id, RegistrationStatus.Active, grade, Term1Id);
        }

        // Semester 4 (2024-2025 Bahar): DC, FF, CC, DC, DC
        var emreSem4Courses = new (int id, string code, string name, int ects, bool nonGpa)[]
        {
            (40, "CENG214", "Algorithms II", 6, false),
            (41, "CENG256", "Formal Languages", 6, false),
            (42, "MATH224", "Linear Algebra", 5, false),
            (43, "MATH206", "Probability", 5, false),
            (44, "SENG264", "Intro to AI", 6, false),
        };

        foreach (var (id, code, name, ects, nonGpa) in emreSem4Courses)
            _context.Courses.Add(new Course { Id = id, Code = code, Name = name, Ects = ects, IsNonGpa = nonGpa });

        var emreSem4Grades = new (string code, GradeCode grade)[]
        {
            ("CENG214", GradeCode.DC), ("CENG256", GradeCode.FF),
            ("MATH224", GradeCode.CC), ("MATH206", GradeCode.DC),
            ("SENG264", GradeCode.DC)
        };

        foreach (var (code, grade) in emreSem4Grades)
        {
            var course = _context.Courses.Local.First(c => c.Code == code);
            var section = AddSection(emreSectionId++, course.Id, Term2Id);
            AddRegistration(emreRegId++, EmreId, section.Id, RegistrationStatus.Active, grade, Term2Id);
        }

        _context.SaveChanges();
    }

    private CourseSection AddSection(int id, int courseId, int termId)
    {
        var section = new CourseSection
        {
            Id = id, CourseId = courseId, TermId = termId,
            Capacity = 40, EnrolledCount = 1, IsActive = false
        };
        _context.CourseSections.Add(section);
        return section;
    }

    private void AddRegistration(int id, int studentId, int sectionId,
        RegistrationStatus status, GradeCode? grade, int termId)
    {
        _context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = id, StudentId = studentId, CourseSectionId = sectionId,
            Status = status, Grade = grade,
            IsRepeated = false, MandatoryAttendance = false,
            CourseWithdrawalCount = 0, CreatedAt = DateTime.UtcNow
        });
    }

    public void Dispose() => _context.Dispose();
}
