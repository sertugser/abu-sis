using FluentAssertions;
using SIS.Domain.Entities;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

/// <summary>
/// İntibak (CurriculumCourse.PreviousCourse) resolver birim testleri.
/// Yön KRİTİK: CourseId (yeni) → PreviousCourseId (eski). "Öğrenci eski dersi
/// geçtiyse yeni ders karşılandı sayılır" — ters yön (yeni geçince eski sayılsın) GEÇERSİZ.
/// </summary>
public class PreviousCourseResolverTests
{
    private static CurriculumCourse Cc(int courseId, int? previousCourseId) =>
        new() { CurriculumId = 1, CourseId = courseId, PreviousCourseId = previousCourseId };

    [Fact]
    public void BuildMap_maps_new_course_to_previous_and_skips_null()
    {
        // Arrange: MAT201(new)→MAT101(old), FIZ102 intibaksız
        var courses = new[] { Cc(201, 101), Cc(102, null) };

        // Act
        var map = PreviousCourseResolver.BuildMap(courses);

        // Assert
        map.Should().ContainKey(201).WhoseValue.Should().Be(101);
        map.Should().NotContainKey(102);
        map.Should().HaveCount(1);
    }

    [Fact]
    public void IsCovered_true_when_new_course_passed_directly()
    {
        var passed = new HashSet<int> { 201 };
        var map = PreviousCourseResolver.BuildMap(new[] { Cc(201, 101) });

        PreviousCourseResolver.IsCovered(201, passed, map).Should().BeTrue();
    }

    [Fact]
    public void IsCovered_true_when_previous_old_course_passed()
    {
        // Öğrenci eski MAT101'i geçti → yeni MAT201 karşılandı sayılır
        var passed = new HashSet<int> { 101 };
        var map = PreviousCourseResolver.BuildMap(new[] { Cc(201, 101) });

        PreviousCourseResolver.IsCovered(201, passed, map).Should().BeTrue();
    }

    [Fact]
    public void IsCovered_false_when_neither_passed()
    {
        var passed = new HashSet<int> { 999 };
        var map = PreviousCourseResolver.BuildMap(new[] { Cc(201, 101) });

        PreviousCourseResolver.IsCovered(201, passed, map).Should().BeFalse();
    }

    [Fact]
    public void IsCovered_is_one_directional_passing_new_does_not_cover_old()
    {
        // Öğrenci yeni MAT201'i geçti; eski MAT101 için intibak GEÇERLİ DEĞİL (ters yön).
        var passed = new HashSet<int> { 201 };
        var map = PreviousCourseResolver.BuildMap(new[] { Cc(201, 101) });

        PreviousCourseResolver.IsCovered(101, passed, map).Should().BeFalse();
    }

    [Fact]
    public void IsCovered_false_when_no_mapping_and_not_passed()
    {
        var passed = new HashSet<int> { 101 };
        var map = PreviousCourseResolver.BuildMap(System.Array.Empty<CurriculumCourse>());

        PreviousCourseResolver.IsCovered(201, passed, map).Should().BeFalse();
    }
}
