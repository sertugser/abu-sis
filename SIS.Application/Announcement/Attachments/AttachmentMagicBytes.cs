namespace SIS.Application.Announcement.Attachments;

/// <summary>
/// Dosya içeriğinin baş kısmındaki "magic byte" imzalarını uzantıyla eşleştirerek
/// uzantı sahteciliğini engeller. Örneğin <c>evil.exe</c>'yi <c>evil.pdf</c> olarak
/// yükleme girişimleri burada yakalanır.
/// </summary>
/// <remarks>
/// Kod-içi politikadır — güvenlik sınırı olduğu için DB'den yönetilmez.
/// Bilinen format imzaları için bkz. https://en.wikipedia.org/wiki/List_of_file_signatures
/// </remarks>
public static class AttachmentMagicBytes
{
    /// <summary>
    /// Verilen <paramref name="extension"/> için izin verilen header byte dizilerini
    /// döner. Null = uzantı için imza kontrolü yapma (örn. text/csv).
    /// </summary>
    public static byte[][]? GetSignaturesForExtension(string extension)
    {
        extension = extension.ToLowerInvariant();
        return extension switch
        {
            ".pdf"  => [[0x25, 0x50, 0x44, 0x46]],                                 // %PDF
            ".png"  => [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],         // \x89PNG
            ".jpg"  or ".jpeg" => [[0xFF, 0xD8, 0xFF]],                            // JPEG SOI
            ".gif"  => [[0x47, 0x49, 0x46, 0x38, 0x37, 0x61],                      // GIF87a
                         [0x47, 0x49, 0x46, 0x38, 0x39, 0x61]],                    // GIF89a
            ".webp" => [[0x52, 0x49, 0x46, 0x46]],                                 // RIFF (sonra WEBP)
            ".zip"  or ".docx" or ".xlsx" or ".pptx"
                    => [[0x50, 0x4B, 0x03, 0x04],                                  // PK\x03\x04
                         [0x50, 0x4B, 0x05, 0x06],                                  // PK\x05\x06 (empty zip)
                         [0x50, 0x4B, 0x07, 0x08]],                                 // PK\x07\x08 (spanned)
            ".doc"  or ".xls" or ".ppt"
                    => [[0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]],         // OLE/CFB
            // .txt ve .csv için magic byte yok — metin dosyalarının imzası standart değil.
            // İçerik kontrolü gereksiz; uzantı + MIME whitelist yeterli savunma.
            ".txt"  or ".csv" => null,
            _ => [],  // bilinmeyen uzantı → asla eşleşmez → reddedilir
        };
    }

    /// <summary>
    /// Dosyanın ilk N byte'ı (header) verilen uzantı için politikadaki imzalardan
    /// herhangi biriyle eşleşiyor mu kontrol eder.
    /// </summary>
    /// <returns>
    /// <c>true</c>: imza eşleşti veya bu uzantı için imza kontrolü yok (örn. txt).
    /// <c>false</c>: hiçbir imza eşleşmedi → dosya sahteciliği şüphesi.
    /// </returns>
    public static bool HeaderMatches(string extension, ReadOnlySpan<byte> header)
    {
        var signatures = GetSignaturesForExtension(extension);
        if (signatures is null) return true;        // imza kontrolü atlanır (txt/csv)
        if (signatures.Length == 0) return false;   // bilinmeyen uzantı

        foreach (var sig in signatures)
        {
            if (header.Length < sig.Length) continue;
            if (header[..sig.Length].SequenceEqual(sig))
                return true;
        }
        return false;
    }
}
