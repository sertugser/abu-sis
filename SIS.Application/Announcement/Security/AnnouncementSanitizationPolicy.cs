using System.Collections.Frozen;

namespace SIS.Application.Announcement.Security;

/// <summary>
/// Duyuru HTML sanitizasyon politikası — XSS güvenliği için sabit beyaz liste.
/// Bu politika kasıtlı olarak DB'den okunmaz: admin'in güvenlik sınırını gevşetmesini engeller.
/// </summary>
public static class AnnouncementSanitizationPolicy
{
    /// <summary>İçerik biçim tipleri — entity'deki <c>ContentFormat</c> kolonunun değerleri.</summary>
    public static class ContentFormats
    {
        public const string Plain = "plain";
        public const string Html = "html";
    }

    /// <summary>Her zaman güvenli kabul edilen yapısal ve metin etiketleri.</summary>
    public static readonly FrozenSet<string> AlwaysAllowedTags = new[]
    {
        "p", "br", "span", "div",
        "strong", "em", "b", "i", "s", "u", "sub", "sup",
        "code", "pre", "blockquote",
        "h2", "h3", "h4",
        "ul", "ol", "li",
        "a",
        "hr",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Yalnızca <c>allowImages = true</c> ile çağırıldığında izin verilen etiketler.</summary>
    public static readonly FrozenSet<string> ImageTags = new[] { "img", "figure", "figcaption" }
        .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Yalnızca <c>allowTables = true</c> ile çağırıldığında izin verilen etiketler.</summary>
    public static readonly FrozenSet<string> TableTags = new[]
    {
        "table", "thead", "tbody", "tfoot", "tr", "td", "th", "caption", "colgroup", "col",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Her etikette güvenli kabul edilen attribute'lar.</summary>
    public static readonly FrozenSet<string> AlwaysAllowedAttributes = new[]
    {
        "class", "id", "title", "lang", "dir",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Yalnızca <c>a</c> etiketinde geçerli attribute'lar.</summary>
    public static readonly FrozenSet<string> LinkAttributes = new[] { "href", "target", "rel" }
        .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Yalnızca <c>img</c> etiketinde geçerli attribute'lar.</summary>
    public static readonly FrozenSet<string> ImageAttributes = new[]
    {
        "src", "alt", "width", "height",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Yalnızca tablo etiketlerinde geçerli attribute'lar.</summary>
    public static readonly FrozenSet<string> TableAttributes = new[]
    {
        "colspan", "rowspan", "scope", "headers", "abbr",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>URL şema beyaz listesi — sadece bunlara izin verilir.</summary>
    public static readonly FrozenSet<string> AllowedUrlSchemes = new[]
    {
        "http", "https", "mailto", "tel",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Image src için izin verilen şemalar (data: URL'leri büyük blob riski içerir — kısıtlı).</summary>
    public static readonly FrozenSet<string> AllowedImageSchemes = new[]
    {
        "http", "https",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
