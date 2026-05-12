using Microsoft.EntityFrameworkCore;
using SIS.Application.Announcement.Attachments;
using SIS.Application.Announcement.Configuration;
using SIS.Application.Announcement.DTOs;
using SIS.Application.Announcement.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Announcement;

public sealed class AnnouncementAttachmentService : IAnnouncementAttachmentService
{
    private readonly AppDbContext _context;
    private readonly IAnnouncementAttachmentStorage _storage;
    private readonly IAnnouncementSettingsProvider _settingsProvider;

    public AnnouncementAttachmentService(
        AppDbContext context,
        IAnnouncementAttachmentStorage storage,
        IAnnouncementSettingsProvider settingsProvider)
    {
        _context          = context;
        _storage          = storage;
        _settingsProvider = settingsProvider;
    }

    public async Task<AnnouncementAttachmentDto> UploadAsync(
        int announcementId,
        string originalFileName,
        Stream content,
        long sizeBytes,
        string? clientMimeType,
        string uploadedBy,
        CancellationToken ct = default)
    {
        var settings = await _settingsProvider.GetAsync(ct);

        if (!settings.AttachmentsEnabled)
            throw new InvalidOperationException("Dosya eki özelliği şu anda kapalı.");

        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new InvalidOperationException("Dosya adı boş olamaz.");

        var maxBytes = settings.AttachmentMaxSizeMB * 1024L * 1024L;
        if (sizeBytes <= 0)
            throw new InvalidOperationException("Dosya içeriği boş.");
        if (sizeBytes > maxBytes)
            throw new InvalidOperationException(
                $"Dosya boyutu {settings.AttachmentMaxSizeMB} MB'ı aşamaz.");

        // Duyuru var mı? + mevcut ek sayısı limiti
        var announcement = await _context.InternalAnnouncements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == announcementId, ct);
        if (announcement is null)
            throw new InvalidOperationException("Duyuru bulunamadı.");

        var currentCount = await _context.AnnouncementAttachments
            .CountAsync(a => a.AnnouncementId == announcementId, ct);
        if (currentCount >= settings.AttachmentMaxCount)
            throw new InvalidOperationException(
                $"Bu duyuruya en fazla {settings.AttachmentMaxCount} dosya eklenebilir.");

        // Uzantı + MIME whitelist
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!AnnouncementAttachmentPolicy.IsExtensionAllowed(extension))
            throw new InvalidOperationException(
                $"'{extension}' uzantısına izin verilmiyor. Politikadaki uzantılar için yönetici ayarlarına bakın.");

        var resolvedMime = AnnouncementAttachmentPolicy.ResolveMimeType(extension)!;

        // Storage'a yaz — burada exception olursa DB'ye yazılmaz, tutarlı kalır.
        var storageName = await _storage.StoreAsync(originalFileName, content, ct);

        var entity = new AnnouncementAttachment
        {
            AnnouncementId  = announcementId,
            FileName        = SanitizeFileName(originalFileName),
            StorageFileName = storageName,
            MimeType        = resolvedMime,
            SizeBytes       = sizeBytes,
            UploadedAt      = DateTime.UtcNow,
            UploadedBy      = uploadedBy,
        };

        _context.AnnouncementAttachments.Add(entity);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch
        {
            // DB yazımı başarısız — storage'taki yetim dosyayı temizle
            try { await _storage.DeleteAsync(storageName, ct); } catch { /* swallow cleanup */ }
            throw;
        }

        return new AnnouncementAttachmentDto(
            entity.Id, entity.AnnouncementId,
            entity.FileName, entity.MimeType, entity.SizeBytes,
            entity.UploadedAt, entity.UploadedBy);
    }

    public async Task<AttachmentDownloadDto?> OpenForDownloadAsync(int attachmentId, CancellationToken ct = default)
    {
        var entity = await _context.AnnouncementAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
        if (entity is null) return null;

        var result = await _storage.OpenReadAsync(entity.StorageFileName, ct);
        if (result is null) return null;

        return new AttachmentDownloadDto(
            result.Content, result.SizeBytes, entity.FileName, entity.MimeType);
    }

    public async Task<bool> DeleteAsync(int attachmentId, string requestingUserId, bool isAdminOverride, CancellationToken ct = default)
    {
        var entity = await _context.AnnouncementAttachments
            .Include(a => a.Announcement)
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
        if (entity is null) return false;

        // Sahibi (yükleyen) veya admin override silebilir;
        // duyurunun yazarı da silebilir (kendi duyurusunu yönetiyor).
        var isOwner       = string.Equals(entity.UploadedBy, requestingUserId, StringComparison.Ordinal);
        var isAuthor      = string.Equals(entity.Announcement.CreatedBy, requestingUserId, StringComparison.Ordinal);
        if (!isAdminOverride && !isOwner && !isAuthor)
            throw new UnauthorizedAccessException("Bu eki silme yetkiniz yok.");

        _context.AnnouncementAttachments.Remove(entity);
        await _context.SaveChangesAsync(ct);

        // Diskten temizle — DB silme başarılı olduktan sonra (yetim file kalsa da güvende)
        try { await _storage.DeleteAsync(entity.StorageFileName, ct); } catch { /* swallow */ }
        return true;
    }

    public async Task DeleteAllForAnnouncementAsync(int announcementId, CancellationToken ct = default)
    {
        var attachments = await _context.AnnouncementAttachments
            .Where(a => a.AnnouncementId == announcementId)
            .ToListAsync(ct);
        if (attachments.Count == 0) return;

        _context.AnnouncementAttachments.RemoveRange(attachments);
        await _context.SaveChangesAsync(ct);

        foreach (var att in attachments)
        {
            try { await _storage.DeleteAsync(att.StorageFileName, ct); } catch { /* swallow */ }
        }
    }

    public async Task<int?> GetParentAnnouncementIdAsync(int attachmentId, CancellationToken ct = default)
    {
        return await _context.AnnouncementAttachments
            .AsNoTracking()
            .Where(a => a.Id == attachmentId)
            .Select(a => (int?)a.AnnouncementId)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Path/control karakteri içeren ad'ları temizler — kullanıcının verdiği orijinal
    /// dosya adı sadece gösterim ve Content-Disposition için kullanılır.
    /// </summary>
    private static string SanitizeFileName(string raw)
    {
        var name = Path.GetFileName(raw); // path traversal kırpılır
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Where(c => !invalid.Contains(c) && !char.IsControl(c)).ToArray());
        if (string.IsNullOrWhiteSpace(clean))
            clean = "dosya" + Path.GetExtension(raw);
        return clean.Length > 200 ? clean[..200] : clean;
    }
}
