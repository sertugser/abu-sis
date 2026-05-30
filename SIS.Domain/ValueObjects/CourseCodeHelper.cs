namespace SIS.Domain.ValueObjects;

/// <summary>
/// Ders kodu üzerinden sınıf yılı çıkarımı (1xx→1, 2xx→2, 3xx→3, 4xx→4).
/// SuggestedSemester=0 olan curriculum kursları için fallback.
/// Memory: project_course_code_year_rule.
/// </summary>
public static class CourseCodeHelper
{
    /// <summary>
    /// Ders kodunun ilk 1-4 rakamından sınıf yılını döner.
    /// Hiçbir 1-4 rakam yoksa 1 döner (en güvenli varsayım).
    /// </summary>
    public static int DeriveClassYearFromCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return 1;
        foreach (var ch in code)
        {
            if (ch >= '1' && ch <= '4') return ch - '0';
            if (ch >= '0' && ch <= '9') break; // ilk rakam 1-4 değilse fallback'e düş
        }
        return 1;
    }

    /// <summary>
    /// SuggestedSemester (1-8) → ClassYear (1-4) dönüşümü.
    /// SuggestedSemester=0 ise ders kodu prefix fallback'i kullanılır.
    /// </summary>
    public static int ResolveClassYear(int suggestedSemester, string courseCode)
    {
        if (suggestedSemester > 0)
            return (suggestedSemester - 1) / 2 + 1;
        return DeriveClassYearFromCode(courseCode);
    }
}
