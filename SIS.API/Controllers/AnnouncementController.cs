using System.Net;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnnouncementController : ControllerBase
{
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AnnouncementController> _logger;
    private const string CacheKey = "announcements";
    private const string SourceUrl = "https://ankarabilim.edu.tr/muhendislik-ve-mimarlik-fakultesi/duyurular";

    public AnnouncementController(IMemoryCache cache, IHttpClientFactory httpClientFactory, ILogger<AnnouncementController> logger)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAnnouncements()
    {
        if (_cache.TryGetValue(CacheKey, out List<AnnouncementDto>? cached) && cached != null)
            return Ok(cached);

        try
        {
            var announcements = await ScrapeAnnouncements();
            _cache.Set(CacheKey, announcements, TimeSpan.FromHours(6));
            return Ok(announcements);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Duyuru çekme başarısız");
            return Ok(new List<AnnouncementDto>());
        }
    }

    private async Task<List<AnnouncementDto>> ScrapeAnnouncements()
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
        var html = await client.GetStringAsync(SourceUrl);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var results = new List<AnnouncementDto>();

        // Find announcement links — they point to /duyuru/ subpath
        var links = doc.DocumentNode.SelectNodes("//a[contains(@href, '/duyuru/')]");
        if (links == null) return results;

        var seen = new HashSet<string>();

        foreach (var link in links)
        {
            var href = link.GetAttributeValue("href", "");

            // Get title from h3 first, then title attribute, then full text
            var h3 = link.SelectSingleNode(".//h3");
            var title = h3?.InnerText?.Trim() ?? link.GetAttributeValue("title", "")?.Trim() ?? "";

            if (string.IsNullOrEmpty(title))
                title = link.InnerText?.Trim() ?? "";

            title = WebUtility.HtmlDecode(title);

            // Skip empty or duplicate titles
            if (string.IsNullOrEmpty(title) || !seen.Add(title))
                continue;

            // Extract date — look for Turkish date pattern in text nodes before h3
            var dateText = "";
            var allText = link.InnerText ?? "";
            if (h3 != null)
            {
                var h3Text = h3.InnerText?.Trim() ?? "";
                var beforeH3 = allText.IndexOf(h3Text, StringComparison.Ordinal);
                if (beforeH3 > 0)
                {
                    dateText = WebUtility.HtmlDecode(allText[..beforeH3]).Trim();
                }
            }

            // Fallback: look for date pattern in text nodes
            if (string.IsNullOrEmpty(dateText))
            {
                var textNodes = link.SelectNodes(".//text()");
                if (textNodes != null)
                {
                    foreach (var node in textNodes)
                    {
                        var text = WebUtility.HtmlDecode(node.InnerText).Trim();
                        if (text.Length > 5 && char.IsDigit(text[0]) && text.Contains(' ')
                            && !text.Equals(title, StringComparison.OrdinalIgnoreCase))
                        {
                            dateText = text;
                            break;
                        }
                    }
                }
            }

            results.Add(new AnnouncementDto
            {
                Title = title,
                Date = dateText,
                Url = href.StartsWith("http") ? href : $"https://ankarabilim.edu.tr{href}"
            });

            if (results.Count >= 5) break;
        }

        return results;
    }
}

public class AnnouncementDto
{
    public string Title { get; set; } = "";
    public string Date { get; set; } = "";
    public string Url { get; set; } = "";
}
