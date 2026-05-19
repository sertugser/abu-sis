using System.Collections.Frozen;

namespace SIS.Application.Announcement.Attachments;

/// <summary>
/// Dosya eki için güvenlik politikası — MIME / uzantı whitelist'i.
/// Sanitizer politikası gibi DB'den okunmaz: admin'in tehlikeli dosya tipini (örn. .exe, .js, .html)
/// gevşetmesi engellenir. Genişletmek için yalnızca koddan değişiklik gerekir.
/// </summary>
public static class AnnouncementAttachmentPolicy
{
    /// <summary>İzin verilen MIME tiplerinin uzantı eşlemesi. Lowercase normalize edilmiş.</summary>
    public static readonly FrozenDictionary<string, string> AllowedMimeByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Belge
        [".pdf"]  = "application/pdf",
        [".doc"]  = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"]  = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"]  = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".txt"]  = "text/plain",
        [".csv"]  = "text/csv",

        // Resim
        [".png"]  = "image/png",
        [".jpg"]  = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"]  = "image/gif",
        [".webp"] = "image/webp",

        // Arşiv (basit)
        [".zip"]  = "application/zip",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>İzin verilen MIME tiplerinin değer kümesi (whitelisting kontrolü için).</summary>
    public static readonly FrozenSet<string> AllowedMimeTypes =
        AllowedMimeByExtension.Values.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Verilen uzantı politikaca izinli mi?</summary>
    public static bool IsExtensionAllowed(string extension) =>
        AllowedMimeByExtension.ContainsKey(extension);

    /// <summary>Verilen uzantıya karşılık gelen MIME tipini döner (politika dışıysa null).</summary>
    public static string? ResolveMimeType(string extension) =>
        AllowedMimeByExtension.TryGetValue(extension, out var mime) ? mime : null;
}
