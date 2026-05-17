using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class InternalAnnouncement
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// İçerik biçimi: <c>"plain"</c> (düz metin, eski kayıtlar) veya <c>"html"</c> (rich text).
    /// Geriye uyumluluk için default "plain", yeni kayıtlar "html" olabilir.
    /// İzin verilen değerler için bkz. <see cref="SIS.Application.Announcement.Security.AnnouncementSanitizationPolicy.ContentFormats"/>.
    /// </summary>
    public string ContentFormat { get; set; } = "plain";

    /// <summary>Kategori (Akademik / İdari / Sosyal / Ders, vb.). Null = kategorisiz.</summary>
    public int? CategoryId { get; set; }
    public AnnouncementCategory? Category { get; set; }

    public AnnouncementAudience TargetAudience { get; set; }

    /// <summary>
    /// Öğrenci hedefli yayınlarda opsiyonel sınıf filtresi.
    /// 1 = 1. sınıf, 2 = 2. sınıf, ... null = tüm sınıflar.
    /// Hesaplama: <c>(Student.CompletedSemesters / 2) + 1</c>.
    /// Yalnızca öğrenciye yönelik audience'larda etkilidir.
    /// </summary>
    public int? TargetStudentYear { get; set; }

    // ── Department-scoped targeting ───────────────────────────────────────────
    public int? TargetDepartmentId { get; set; }
    public Department? TargetDepartment { get; set; }

    // ── Faculty-scoped targeting (Dean) ───────────────────────────────────────
    public int? TargetFacultyId { get; set; }
    public Faculty? TargetFaculty { get; set; }

    // ── Course-section-scoped targeting (Instructor) ──────────────────────────
    public int? TargetSectionId { get; set; }
    public CourseSection? TargetSection { get; set; }

    /// <summary>Bu duyuruya iliştirilmiş dosya ekleri (Madde 4).</summary>
    public ICollection<AnnouncementAttachment> Attachments { get; set; } = new List<AnnouncementAttachment>();

    public bool IsPinned { get; set; }

    /// <summary>
    /// Login sonrası kullanıcının ekranına blocking dialog olarak çıkar (Madde 6).
    /// Kritik duyurular (final programı, kayıt yenileme, vb.) için.
    /// </summary>
    public bool IsPopup { get; set; }

    /// <summary>
    /// <c>true</c> ise kullanıcı pop-up'ı kapatamaz — açıkça "Okudum, anladım" butonuna basmalı.
    /// <see cref="AnnouncementRead.AcknowledgedAt"/> set edilir; hukuki sorumluluk için kullanılır.
    /// Yalnızca <see cref="IsPopup"/> = true iken anlamlıdır.
    /// </summary>
    public bool RequiresAcknowledgement { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
