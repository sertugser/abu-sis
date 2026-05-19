using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class GradingCriteria
{
    public int Id { get; set; }

    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public int AcademicTermId { get; set; }
    public AcademicTerm AcademicTerm { get; set; } = null!;

    // Absolute = mutlak değerlendirme, Relative = bağıl değerlendirme / çan eğrisi
    public string CalculationMethod { get; set; } = "Absolute";

    // Varsayılan aralıklar
    public int AAMin { get; set; } = 91;
    public int BAMin { get; set; } = 81;
    public int BBMin { get; set; } = 71;
    public int CBMin { get; set; } = 61;
    public int CCMin { get; set; } = 55;
    public int DCMin { get; set; } = 50;
    public int DDMin { get; set; } = 45;
    // FF = DDMin altı

    public GradeCode ResolveGrade(int score) => score switch
    {
        _ when score >= AAMin => GradeCode.AA,
        _ when score >= BAMin => GradeCode.BA,
        _ when score >= BBMin => GradeCode.BB,
        _ when score >= CBMin => GradeCode.CB,
        _ when score >= CCMin => GradeCode.CC,
        _ when score >= DCMin => GradeCode.DC,
        _ when score >= DDMin => GradeCode.DD,
        _                     => GradeCode.FF,
    };
}