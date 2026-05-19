using System.Collections.Frozen;
using System.Text;
using HtmlAgilityPack;
using SIS.Application.Announcement.Security;

namespace SIS.Infrastructure.Services.Announcement;

/// <summary>
/// HtmlAgilityPack tabanlı whitelist sanitizer. Politika dışı tüm tag/attribute/URL şemaları kaldırılır.
/// Server-side ikinci savunma hattı — frontend DOMPurify'ı atlatılsa bile burada güvenli hale gelir.
/// </summary>
public sealed class AnnouncementHtmlSanitizer : IAnnouncementHtmlSanitizer
{
    public string Sanitize(string html, bool allowImages = true, bool allowTables = true)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var doc = new HtmlDocument
        {
            OptionAutoCloseOnEnd       = true,
            OptionWriteEmptyNodes      = true,
            OptionFixNestedTags        = true,
            OptionUseIdAttribute       = false,
        };
        doc.LoadHtml(html);

        var allowedTags = BuildAllowedTagSet(allowImages, allowTables);
        SanitizeNode(doc.DocumentNode, allowedTags);

        return doc.DocumentNode.InnerHtml;
    }

    private static FrozenSet<string> BuildAllowedTagSet(bool allowImages, bool allowTables)
    {
        var set = new HashSet<string>(AnnouncementSanitizationPolicy.AlwaysAllowedTags, StringComparer.OrdinalIgnoreCase);
        if (allowImages)
            set.UnionWith(AnnouncementSanitizationPolicy.ImageTags);
        if (allowTables)
            set.UnionWith(AnnouncementSanitizationPolicy.TableTags);
        return set.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void SanitizeNode(HtmlNode node, FrozenSet<string> allowedTags)
    {
        // Recursive: önce çocukları topla (mutation sırasında collection bozulmasın diye snapshot)
        var children = node.ChildNodes.ToList();
        foreach (var child in children)
        {
            if (child.NodeType == HtmlNodeType.Comment)
            {
                child.Remove();
                continue;
            }

            if (child.NodeType == HtmlNodeType.Text)
                continue;

            if (child.NodeType != HtmlNodeType.Element)
                continue;

            if (!allowedTags.Contains(child.Name))
            {
                // Tag yasak: çocukları üst seviyeye taşı, kendini kaldır (içerik kaybolmasın)
                UnwrapNode(child);
                continue;
            }

            SanitizeAttributes(child);
            SanitizeNode(child, allowedTags);
        }
    }

    private static void UnwrapNode(HtmlNode node)
    {
        var parent = node.ParentNode;
        foreach (var grandchild in node.ChildNodes.ToList())
        {
            node.RemoveChild(grandchild);
            parent.InsertBefore(grandchild, node);
        }
        parent.RemoveChild(node);
    }

    private static void SanitizeAttributes(HtmlNode node)
    {
        var allowedForNode = ResolveAllowedAttributes(node.Name);
        var toRemove = new List<HtmlAttribute>();

        foreach (var attr in node.Attributes)
        {
            if (!allowedForNode.Contains(attr.Name))
            {
                toRemove.Add(attr);
                continue;
            }

            // URL şema kontrolü href/src için
            if (IsUrlAttribute(node.Name, attr.Name))
            {
                if (!IsSafeUrl(node.Name, attr.Value))
                {
                    toRemove.Add(attr);
                    continue;
                }
            }

            // Yeni pencerede açılan linklere noopener noreferrer eklenir (tabnabbing önlemi)
            if (string.Equals(node.Name, "a", StringComparison.OrdinalIgnoreCase)
                && string.Equals(attr.Name, "target", StringComparison.OrdinalIgnoreCase)
                && string.Equals(attr.Value, "_blank", StringComparison.OrdinalIgnoreCase))
            {
                var rel = node.GetAttributeValue("rel", string.Empty);
                if (!rel.Contains("noopener", StringComparison.OrdinalIgnoreCase)
                    || !rel.Contains("noreferrer", StringComparison.OrdinalIgnoreCase))
                {
                    node.SetAttributeValue("rel", "noopener noreferrer");
                }
            }
        }

        foreach (var attr in toRemove)
            node.Attributes.Remove(attr);
    }

    private static FrozenSet<string> ResolveAllowedAttributes(string tagName)
    {
        var union = new HashSet<string>(AnnouncementSanitizationPolicy.AlwaysAllowedAttributes, StringComparer.OrdinalIgnoreCase);

        if (string.Equals(tagName, "a", StringComparison.OrdinalIgnoreCase))
            union.UnionWith(AnnouncementSanitizationPolicy.LinkAttributes);
        else if (string.Equals(tagName, "img", StringComparison.OrdinalIgnoreCase))
            union.UnionWith(AnnouncementSanitizationPolicy.ImageAttributes);
        else if (AnnouncementSanitizationPolicy.TableTags.Contains(tagName))
            union.UnionWith(AnnouncementSanitizationPolicy.TableAttributes);

        return union.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsUrlAttribute(string tagName, string attrName)
    {
        return (string.Equals(tagName, "a", StringComparison.OrdinalIgnoreCase)
                && string.Equals(attrName, "href", StringComparison.OrdinalIgnoreCase))
            || (string.Equals(tagName, "img", StringComparison.OrdinalIgnoreCase)
                && string.Equals(attrName, "src", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSafeUrl(string tagName, string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var trimmed = url.Trim();

        // Relative URL (örn. /api/files/x.pdf) güvenli kabul edilir
        if (trimmed.StartsWith('/') || trimmed.StartsWith('#'))
            return true;

        if (!Uri.TryCreate(trimmed, UriKind.RelativeOrAbsolute, out var uri))
            return false;

        if (!uri.IsAbsoluteUri)
            return true;

        var allowedSchemes = string.Equals(tagName, "img", StringComparison.OrdinalIgnoreCase)
            ? AnnouncementSanitizationPolicy.AllowedImageSchemes
            : AnnouncementSanitizationPolicy.AllowedUrlSchemes;

        return allowedSchemes.Contains(uri.Scheme);
    }
}
