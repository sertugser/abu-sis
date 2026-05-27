namespace SIS.Application.Common.Services;

/// <summary>
/// Öğrenci belgesi yükleme doğrulaması: content-type whitelist (PDF/JPEG/PNG) +
/// magic-byte (dosya imzası) eşleşmesi + boyut sınırı. Bildirilen content-type ile
/// gerçek dosya içeriğinin (sihirli baytlar) uyuştuğunu doğrular — uzantı/header
/// sahteciliğini engeller.
/// </summary>
public static class StudentDocumentValidator
{
    public const string PdfContentType = "application/pdf";
    public const string JpegContentType = "image/jpeg";
    public const string PngContentType = "image/png";

    // PDF: 25 50 44 46 ("%PDF")
    private static readonly byte[] PdfMagic = { 0x25, 0x50, 0x44, 0x46 };

    // JPEG: FF D8 FF
    private static readonly byte[] JpegMagic = { 0xFF, 0xD8, 0xFF };

    // PNG: 89 50 4E 47 0D 0A 1A 0A
    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>
    /// Verilen baytların okunması için gereken minimum header uzunluğu (8 byte → PNG).
    /// </summary>
    public const int HeaderSize = 8;

    public static bool IsContentTypeAllowed(string? contentType)
    {
        var normalized = contentType?.ToLowerInvariant();
        return normalized is PdfContentType or JpegContentType or PngContentType;
    }

    /// <summary>
    /// Doğrulamayı çalıştırır. Hata varsa Türkçe mesaj döner; geçerliyse null döner.
    /// </summary>
    public static string? Validate(bool hasFile, long sizeBytes, long maxBytes, string? contentType, ReadOnlySpan<byte> header)
    {
        if (!hasFile)
            return "Dosya yüklenmedi.";

        if (sizeBytes <= 0)
            return "Dosya içeriği boş.";

        if (sizeBytes > maxBytes)
            return $"Dosya boyutu üst sınırı ({maxBytes} byte) aşıyor.";

        if (!IsContentTypeAllowed(contentType))
            return "Yalnızca PDF, JPEG veya PNG belgeler yüklenebilir.";

        if (!MagicBytesMatch(contentType!, header))
            return "Dosya içeriği bildirilen tip ile uyuşmuyor (geçersiz veya yeniden adlandırılmış dosya).";

        return null;
    }

    /// <summary>
    /// Header baytlarının, bildirilen content-type'ın sihirli imzasıyla başlayıp
    /// başlamadığını kontrol eder.
    /// </summary>
    public static bool MagicBytesMatch(string contentType, ReadOnlySpan<byte> header)
    {
        var expected = contentType?.ToLowerInvariant() switch
        {
            PdfContentType => PdfMagic,
            JpegContentType => JpegMagic,
            PngContentType => PngMagic,
            _ => null,
        };

        if (expected is null)
            return false;

        if (header.Length < expected.Length)
            return false;

        for (var i = 0; i < expected.Length; i++)
        {
            if (header[i] != expected[i])
                return false;
        }

        return true;
    }
}
