using SIS.Domain.Entities;

namespace SIS.Infrastructure.Services;

/// <summary>
/// İntibak (müfredat geçiş eşleştirmesi — <see cref="CurriculumCourse.PreviousCourseId"/>) çözümleyici.
///
/// Yön: <c>CourseId</c> (yeni müfredat dersi) → <c>PreviousCourseId</c> (eski/önceki ders).
/// Kural: öğrenci ESKİ dersi geçtiyse, YENİ ders karşılanmış sayılır (tek yönlü).
/// Ters yön (yeniyi geçince eski sayılsın) GEÇERLİ DEĞİL.
///
/// <see cref="CourseEquivalency"/>'den farklı: o katalog düzeyinde + simetrik; bu müfredata
/// özel + tek yönlü. Bu yüzden harita her zaman ÖĞRENCİNİN müfredatının
/// <see cref="CurriculumCourse"/> satırlarından kurulmalı.
/// </summary>
internal static class PreviousCourseResolver
{
    /// <summary>
    /// (yeni CourseId → eski PreviousCourseId) haritası kurar. PreviousCourseId null olan
    /// satırlar atlanır. Aynı CourseId birden fazla satırda olursa ilki alınır
    /// (CurriculumCourse PK (CurriculumId, CourseId) olduğundan tek müfredatta tekildir).
    /// </summary>
    internal static Dictionary<int, int> BuildMap(IEnumerable<CurriculumCourse> curriculumCourses) =>
        curriculumCourses
            .Where(cc => cc.PreviousCourseId.HasValue)
            .GroupBy(cc => cc.CourseId)
            .ToDictionary(g => g.Key, g => g.First().PreviousCourseId!.Value);

    /// <summary>
    /// <paramref name="courseId"/> öğrenci için "geçilmiş/karşılanmış" sayılır mı?
    /// Doğrudan geçtiyse VEYA intibak tanımlı eski dersini (PreviousCourse) geçtiyse true.
    /// </summary>
    internal static bool IsCovered(
        int courseId,
        IReadOnlySet<int> passedCourseIds,
        IReadOnlyDictionary<int, int> previousMap) =>
        passedCourseIds.Contains(courseId)
        || (previousMap.TryGetValue(courseId, out var previousId) && passedCourseIds.Contains(previousId));
}
