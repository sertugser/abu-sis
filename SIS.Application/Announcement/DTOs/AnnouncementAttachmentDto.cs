namespace SIS.Application.Announcement.DTOs;

/// <summary>Detay endpoint'inde duyuruya iliştirilmiş ekler için özet bilgi.</summary>
public record AnnouncementAttachmentSummaryDto(
    int Id,
    string FileName,
    string MimeType,
    long SizeBytes,
    DateTime UploadedAt);

/// <summary>Upload sonucu — yeni eklenen ekin bilgileri.</summary>
public record AnnouncementAttachmentDto(
    int Id,
    int AnnouncementId,
    string FileName,
    string MimeType,
    long SizeBytes,
    DateTime UploadedAt,
    string UploadedBy);
