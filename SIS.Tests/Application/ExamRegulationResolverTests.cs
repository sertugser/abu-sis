using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Grading;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class ExamRegulationResolverTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ExamRegulationResolver _resolver;

    private const int FacultyId = 1;
    private const int DeptId = 1;
    private const int ProgramAId = 1;
    private const int ProgramBId = 2;
    private const int CurriculumAId = 1;
    private const int CurriculumBId = 2;
    private const int CourseId = 1;
    private const int StudentId = 1;
    private const int TermId = 1;
    private const int RegAId = 1;
    private const int RegBId = 2;

    public ExamRegulationResolverTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _resolver = new ExamRegulationResolver(_context);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private void SeedFacultyDept()
    {
        if (_context.Faculties.Local.All(f => f.Id != FacultyId))
            _context.Faculties.Add(new Faculty { Id = FacultyId, Name = "Test Faculty", Code = "TF" });

        if (_context.Departments.Local.All(d => d.Id != DeptId))
            _context.Departments.Add(new Department { Id = DeptId, Name = "Test Dept", Code = "TD", FacultyId = FacultyId });
    }

    private void SeedFacultyDeptProgram(int programId = ProgramAId)
    {
        SeedFacultyDept();

        if (_context.AcademicPrograms.Local.All(p => p.Id != programId))
            _context.AcademicPrograms.Add(new AcademicProgram
            {
                Id = programId,
                Name = $"Program {programId}",
                Code = $"P{programId}",
                DepartmentId = DeptId,
                DegreeLevel = DegreeLevel.Bachelor,
                EducationType = EducationType.Semester,
                MaxStudyDurationSemesters = 8,
                TotalRequiredEcts = 240
            });
    }

    private void SeedCurriculumAndCourse(int programId, int curriculumId)
    {
        if (_context.Courses.Local.All(c => c.Id != CourseId))
            _context.Courses.Add(new Course { Id = CourseId, Code = "TST101", Name = "Test Course", Ects = 3 });

        if (_context.Curricula.Local.All(c => c.Id != curriculumId))
            _context.Curricula.Add(new Curriculum { Id = curriculumId, ProgramId = programId, Name = "Müfredat", Version = "2024" });

        if (_context.CurriculumCourses.Local.All(cc => !(cc.CurriculumId == curriculumId && cc.CourseId == CourseId)))
            _context.CurriculumCourses.Add(new CurriculumCourse
            {
                CurriculumId = curriculumId,
                CourseId = CourseId,
                IsCompulsory = true,
                SuggestedSemester = 1
            });
    }

    private void SeedTerm()
    {
        if (_context.AcademicTerms.Local.All(t => t.Id != TermId))
            _context.AcademicTerms.Add(new AcademicTerm
            {
                Id = TermId,
                Name = "2024-2025 Güz",
                ShortEnglishName = "Fall 2024",
                TermType = TermType.Fall,
                Year = 2024,
                TermStart = new DateTime(2024, 10, 1),
                TermEnd = new DateTime(2025, 1, 31)
            });
    }

    private void SeedStudentWithMainProgram(int programId = ProgramAId, int curriculumId = CurriculumAId)
    {
        if (_context.Students.Local.All(s => s.Id != StudentId))
            _context.Students.Add(new Student
            {
                Id = StudentId,
                UserId = 100,
                StudentNumber = "2024001",
                NationalId = "11111111111",
                Status = StudentStatus.Active,
                Cgpa = 3.00m,
                Sgpa = 3.00m,
                CompletedSemesters = 1
            });

        if (_context.StudentPrograms.Local.All(sp => sp.StudentId != StudentId))
            _context.StudentPrograms.Add(new StudentProgram
            {
                StudentId = StudentId,
                ProgramId = programId,
                CurriculumId = curriculumId,
                Type = StudentProgramType.Main,
                IsActive = true,
                EnrollmentDate = new DateTime(2024, 9, 15)
            });
    }

    private void SeedExamRegulation(int id, int programId,
        decimal minMid = 40m, decimal maxMid = 60m,
        decimal minFin = 40m, decimal maxFin = 60m)
    {
        _context.ExamRegulations.Add(new ExamRegulation
        {
            Id = id,
            ProgramId = programId,
            DegreeLevel = DegreeLevel.Bachelor,
            TermType = TermType.Fall,
            Year = 2024,
            IsActive = true,
            Name = $"Yönerge {id}",
            MidtermCount = 1,
            FinalCount = 1,
            MinMidtermWeightPercent = minMid,
            MaxMidtermWeightPercent = maxMid,
            MinFinalWeightPercent = minFin,
            MaxFinalWeightPercent = maxFin,
            CreatedAt = DateTime.UtcNow
        });
    }

    private void SeedExamRegulationWithLetters(int id, int programId,
        int aaMin = 91, int baMin = 81, int bbMin = 71, int cbMin = 61,
        int ccMin = 55, int dcMin = 50, int ddMin = 45,
        bool letterRangesEditable = true)
    {
        _context.ExamRegulations.Add(new ExamRegulation
        {
            Id = id,
            ProgramId = programId,
            DegreeLevel = DegreeLevel.Bachelor,
            TermType = TermType.Fall,
            Year = 2024,
            IsActive = true,
            Name = $"Yönerge {id}",
            MidtermCount = 1,
            FinalCount = 1,
            MinMidtermWeightPercent = 40m,
            MaxMidtermWeightPercent = 60m,
            MinFinalWeightPercent = 40m,
            MaxFinalWeightPercent = 60m,
            AAMin = aaMin,
            BAMin = baMin,
            BBMin = bbMin,
            CBMin = cbMin,
            CCMin = ccMin,
            DCMin = dcMin,
            DDMin = ddMin,
            LetterRangesEditable = letterRangesEditable,
            CreatedAt = DateTime.UtcNow
        });
    }

    // ── tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveForStudentAsync_ReturnsRegulation_WhenStudentMainProgramMatches()
    {
        // Arrange
        SeedFacultyDeptProgram(ProgramAId);
        SeedCurriculumAndCourse(ProgramAId, CurriculumAId);
        SeedTerm();
        SeedStudentWithMainProgram(ProgramAId, CurriculumAId);
        SeedExamRegulation(RegAId, ProgramAId);
        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.ResolveForStudentAsync(StudentId, TermId, default);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(RegAId);
    }

    [Fact]
    public async Task ResolveForStudentAsync_ReturnsNull_WhenNoRegulation()
    {
        // Arrange — no ExamRegulation seeded
        SeedFacultyDeptProgram(ProgramAId);
        SeedCurriculumAndCourse(ProgramAId, CurriculumAId);
        SeedTerm();
        SeedStudentWithMainProgram(ProgramAId, CurriculumAId);
        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.ResolveForStudentAsync(StudentId, TermId, default);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveCourseWeightBoundsAsync_ReturnsBounds_ForSingleProgram()
    {
        // Arrange
        SeedFacultyDeptProgram(ProgramAId);
        SeedCurriculumAndCourse(ProgramAId, CurriculumAId);
        SeedTerm();
        SeedExamRegulation(RegAId, ProgramAId, minMid: 40m, maxMid: 60m, minFin: 40m, maxFin: 60m);
        await _context.SaveChangesAsync();

        // Act
        var bounds = await _resolver.ResolveCourseWeightBoundsAsync(CourseId, TermId, default);

        // Assert
        bounds.Should().NotBeNull();
        bounds!.MinMidterm.Should().Be(40m);
        bounds.MaxMidterm.Should().Be(60m);
        bounds.MinFinal.Should().Be(40m);
        bounds.MaxFinal.Should().Be(60m);
        bounds.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveCourseWeightBoundsAsync_ReturnsIntersection_ForTwoProgramsWithDifferentBounds()
    {
        // Arrange — progA midterm [40,60], progB midterm [30,50]
        // intersection: MinMidterm=max(40,30)=40, MaxMidterm=min(60,50)=50 → IsEmpty=false
        SeedFacultyDeptProgram(ProgramAId);
        SeedFacultyDeptProgram(ProgramBId);
        SeedCurriculumAndCourse(ProgramAId, CurriculumAId);

        // second curriculum linking same course to program B
        _context.Curricula.Add(new Curriculum { Id = CurriculumBId, ProgramId = ProgramBId, Name = "Müfredat B", Version = "2024" });
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumBId,
            CourseId = CourseId,
            IsCompulsory = true,
            SuggestedSemester = 1
        });

        SeedTerm();
        SeedExamRegulation(RegAId, ProgramAId, minMid: 40m, maxMid: 60m, minFin: 40m, maxFin: 60m);
        SeedExamRegulation(RegBId, ProgramBId, minMid: 30m, maxMid: 50m, minFin: 35m, maxFin: 55m);
        await _context.SaveChangesAsync();

        // Act
        var bounds = await _resolver.ResolveCourseWeightBoundsAsync(CourseId, TermId, default);

        // Assert
        bounds.Should().NotBeNull();
        bounds!.MinMidterm.Should().Be(40m);  // max-of-mins
        bounds.MaxMidterm.Should().Be(50m);  // min-of-maxes
        bounds.MinFinal.Should().Be(40m);    // max(40,35)
        bounds.MaxFinal.Should().Be(55m);    // min(60,55)
        bounds.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveCourseWeightBoundsAsync_IsEmptyTrue_WhenIntersectionImpossible()
    {
        // Arrange — progA midterm [40,60], progB midterm [10,30]
        // intersection: MinMidterm=max(40,10)=40, MaxMidterm=min(60,30)=30 → 40>30 → IsEmpty=true
        SeedFacultyDeptProgram(ProgramAId);
        SeedFacultyDeptProgram(ProgramBId);
        SeedCurriculumAndCourse(ProgramAId, CurriculumAId);

        _context.Curricula.Add(new Curriculum { Id = CurriculumBId, ProgramId = ProgramBId, Name = "Müfredat B", Version = "2024" });
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumBId,
            CourseId = CourseId,
            IsCompulsory = true,
            SuggestedSemester = 1
        });

        SeedTerm();
        SeedExamRegulation(RegAId, ProgramAId, minMid: 40m, maxMid: 60m, minFin: 40m, maxFin: 60m);
        SeedExamRegulation(RegBId, ProgramBId, minMid: 10m, maxMid: 30m, minFin: 40m, maxFin: 60m);
        await _context.SaveChangesAsync();

        // Act
        var bounds = await _resolver.ResolveCourseWeightBoundsAsync(CourseId, TermId, default);

        // Assert
        bounds.Should().NotBeNull();
        bounds!.MinMidterm.Should().Be(40m);
        bounds.MaxMidterm.Should().Be(30m);
        bounds.IsEmpty.Should().BeTrue();
    }

    /// <summary>
    /// Two students in different Main programs: student 1 in ProgramA (has a matching regulation),
    /// student 2 in ProgramB (no regulation) → dict maps each correctly.
    /// </summary>
    [Fact]
    public async Task ResolveForStudentsAsync_ReturnsPerStudentRegulations()
    {
        // Arrange
        const int student2Id = 2;
        const int program2Id = 20;
        const int curriculum2Id = 20;

        SeedFacultyDeptProgram(ProgramAId);

        // Second program — different dept/faculty hierarchy not needed, just another program
        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = program2Id, Name = "Program B", Code = "PB", DepartmentId = DeptId,
            DegreeLevel = DegreeLevel.Bachelor, EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 8, TotalRequiredEcts = 240
        });
        _context.Curricula.Add(new Curriculum
        {
            Id = curriculum2Id, ProgramId = program2Id, Name = "Müfredat B2", Version = "2024"
        });

        SeedTerm();
        SeedStudentWithMainProgram(ProgramAId, CurriculumAId);  // StudentId in ProgramA

        // Second student in Program B
        _context.Students.Add(new Student
        {
            Id = student2Id, UserId = 101,
            StudentNumber = "2024002", NationalId = "33333333333",
            Status = StudentStatus.Active, Cgpa = 3.0m, Sgpa = 3.0m, CompletedSemesters = 1
        });
        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = student2Id, ProgramId = program2Id, CurriculumId = curriculum2Id,
            Type = StudentProgramType.Main, IsActive = true,
            EnrollmentDate = new DateTime(2024, 9, 15)
        });

        // Only ProgramA has a regulation; ProgramB has none
        SeedExamRegulation(RegAId, ProgramAId);
        await _context.SaveChangesAsync();

        // Act
        var dict = await _resolver.ResolveForStudentsAsync(
            new[] { StudentId, student2Id }, TermId, default);

        // Assert
        dict.Should().ContainKey(StudentId);
        dict[StudentId].Should().NotBeNull("student 1 is in ProgramA which has a matching regulation");
        dict[StudentId]!.Id.Should().Be(RegAId);

        dict.Should().ContainKey(student2Id);
        dict[student2Id].Should().BeNull("student 2 is in ProgramB which has no regulation");
    }

    // ── ResolveCourseLetterPolicyAsync tests ─────────────────────────────────

    [Fact]
    public async Task ResolveCourseLetterPolicyAsync_ReturnsThresholdsAndFlag_ForSingleProgram()
    {
        // Arrange
        SeedFacultyDeptProgram(ProgramAId);
        SeedCurriculumAndCourse(ProgramAId, CurriculumAId);
        SeedTerm();
        SeedExamRegulationWithLetters(RegAId, ProgramAId,
            aaMin: 91, baMin: 81, bbMin: 71, cbMin: 61, ccMin: 55, dcMin: 50, ddMin: 45,
            letterRangesEditable: false);
        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.ResolveCourseLetterPolicyAsync(CourseId, TermId, default);

        // Assert
        result.Should().NotBeNull();
        result!.AAMin.Should().Be(91);
        result.BAMin.Should().Be(81);
        result.BBMin.Should().Be(71);
        result.CBMin.Should().Be(61);
        result.CCMin.Should().Be(55);
        result.DCMin.Should().Be(50);
        result.DDMin.Should().Be(45);
        result.Editable.Should().BeFalse();
        result.HasUniqueThresholds.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveCourseLetterPolicyAsync_EditableFalse_WhenAnyRegulationLocked()
    {
        // Arrange — regA: Editable=true, regB: Editable=false, same thresholds
        SeedFacultyDeptProgram(ProgramAId);
        SeedFacultyDeptProgram(ProgramBId);
        SeedCurriculumAndCourse(ProgramAId, CurriculumAId);

        _context.Curricula.Add(new Curriculum { Id = CurriculumBId, ProgramId = ProgramBId, Name = "Müfredat B", Version = "2024" });
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumBId,
            CourseId = CourseId,
            IsCompulsory = true,
            SuggestedSemester = 1
        });

        SeedTerm();
        SeedExamRegulationWithLetters(RegAId, ProgramAId,
            aaMin: 91, baMin: 81, bbMin: 71, cbMin: 61, ccMin: 55, dcMin: 50, ddMin: 45,
            letterRangesEditable: true);
        SeedExamRegulationWithLetters(RegBId, ProgramBId,
            aaMin: 91, baMin: 81, bbMin: 71, cbMin: 61, ccMin: 55, dcMin: 50, ddMin: 45,
            letterRangesEditable: false);
        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.ResolveCourseLetterPolicyAsync(CourseId, TermId, default);

        // Assert
        result.Should().NotBeNull();
        result!.Editable.Should().BeFalse("any locked regulation locks the whole policy");
        result.HasUniqueThresholds.Should().BeTrue("both regulations have identical thresholds");
    }

    [Fact]
    public async Task ResolveCourseLetterPolicyAsync_HasUniqueThresholdsFalse_WhenThresholdsDiffer()
    {
        // Arrange — two programs with DIFFERENT thresholds
        SeedFacultyDeptProgram(ProgramAId);
        SeedFacultyDeptProgram(ProgramBId);
        SeedCurriculumAndCourse(ProgramAId, CurriculumAId);

        _context.Curricula.Add(new Curriculum { Id = CurriculumBId, ProgramId = ProgramBId, Name = "Müfredat B", Version = "2024" });
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumBId,
            CourseId = CourseId,
            IsCompulsory = true,
            SuggestedSemester = 1
        });

        SeedTerm();
        SeedExamRegulationWithLetters(RegAId, ProgramAId,
            aaMin: 91, baMin: 81, bbMin: 71, cbMin: 61, ccMin: 55, dcMin: 50, ddMin: 45);
        SeedExamRegulationWithLetters(RegBId, ProgramBId,
            aaMin: 90, baMin: 80, bbMin: 70, cbMin: 60, ccMin: 50, dcMin: 45, ddMin: 40);
        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.ResolveCourseLetterPolicyAsync(CourseId, TermId, default);

        // Assert
        result.Should().NotBeNull();
        result!.HasUniqueThresholds.Should().BeFalse("the two regulations have different thresholds");
    }

    [Fact]
    public async Task ResolveCourseLetterPolicyAsync_ReturnsNull_WhenNoRegulation()
    {
        // Arrange — course seeded but no ExamRegulation
        SeedFacultyDeptProgram(ProgramAId);
        SeedCurriculumAndCourse(ProgramAId, CurriculumAId);
        SeedTerm();
        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.ResolveCourseLetterPolicyAsync(CourseId, TermId, default);

        // Assert
        result.Should().BeNull();
    }

    public void Dispose() => _context.Dispose();
}
