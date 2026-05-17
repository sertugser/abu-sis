using System.Text.RegularExpressions;
using FluentAssertions;

namespace SIS.Tests.API;

public class SecurityGuardrailTests
{
    [Fact]
    public void Every_Controller_Endpoint_Has_Permission_Or_Public_Guardrails()
    {
        var failures = EndpointBlocks()
            .Where(endpoint =>
            {
                var isPublic = endpoint.MethodAttributes.Contains("AllowAnonymous")
                    || endpoint.ClassAttributes.Contains("AllowAnonymous");
                if (isPublic)
                {
                    return !endpoint.MethodAttributes.Contains("EnableRateLimiting")
                        || !endpoint.MethodAttributes.Contains("PublicEndpointJustification");
                }

                return !HasPermissionRequirement(endpoint.MethodAttributes)
                    && !HasPermissionRequirement(endpoint.ClassAttributes);
            })
            .Select(endpoint => endpoint.Location)
            .ToList();

        failures.Should().BeEmpty(
            "ASVS V4/V6/V8: every endpoint must either require a permission or be explicitly public with rate limiting and justification");
    }

    [Fact]
    public void File_Upload_Endpoints_Use_Central_Validation_And_Size_Limits()
    {
        var failures = EndpointBlocks()
            .Where(endpoint => endpoint.MethodAttributes.Contains("IFormFile") || endpoint.MethodAttributes.Contains("[FromForm]"))
            .Where(endpoint =>
            {
                // Pattern 1: Excel import endpoint'leri — sabit Xlsx limit + central helper
                var excelOk =
                       endpoint.Body.Contains("FileUploadValidation.ValidateXlsx")
                    && endpoint.MethodAttributes.Contains("RequestSizeLimit(FileUploadValidation.MaxExcelImportBytes");

                // Pattern 2: Generic attachment upload endpoint'leri — central service üzerinden
                // validate eder (magic byte + uzantı whitelist + boyut) + explicit RequestSizeLimit
                var attachmentOk =
                       endpoint.Body.Contains("_attachmentService.UploadAsync")
                    && Regex.IsMatch(endpoint.MethodAttributes, @"\[RequestSizeLimit\(");

                return !excelOk && !attachmentOk;
            })
            .Select(endpoint => endpoint.Location)
            .ToList();

        failures.Should().BeEmpty(
            "ASVS V5: file upload endpoints must use a central file validation helper (FileUploadValidation for Excel imports, IAnnouncementAttachmentService for generic uploads) and an explicit request size limit");
    }

    [Fact]
    public void New_FromBody_Input_Types_Have_Validators_Or_Are_In_Legacy_Baseline()
    {
        var baseline = File.ReadLines(Path.Combine(RepoRoot(), "docs", "security", "asvs-input-validation-baseline.txt"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal);

        var validators = Directory.GetFiles(RepoRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText)
            .SelectMany(text => Regex.Matches(text, @"AbstractValidator\s*<\s*(?<type>[A-Za-z0-9_]+)\s*>"))
            .Select(match => match.Groups["type"].Value)
            .ToHashSet(StringComparer.Ordinal);

        var bodyTypes = Directory.GetFiles(Path.Combine(RepoRoot(), "SIS.API", "Controllers"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .SelectMany(text => Regex.Matches(text, @"\[FromBody\]\s*(?<type>[A-Za-z0-9_<>\?]+)\s+"))
            .Select(match => NormalizeInputType(match.Groups["type"].Value))
            .Where(type => type.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var missing = bodyTypes
            .Where(type => !validators.Contains(type) && !baseline.Contains(type))
            .OrderBy(type => type)
            .ToList();

        missing.Should().BeEmpty(
            "ASVS V2/V4: new request DTO/input types must get a validator, or the legacy baseline must be deliberately updated with justification");
    }

    [Fact]
    public void Frontend_Does_Not_Introduce_Common_Xss_Or_Token_Storage_Sinks()
    {
        var sourceFiles = Directory.GetFiles(Path.Combine(RepoRoot(), "SIS.Web", "src"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var failures = new List<string>();
        foreach (var file in sourceFiles)
        {
            var text = File.ReadAllText(file);
            var relative = Path.GetRelativePath(RepoRoot(), file);

            if (text.Contains("dangerouslySetInnerHTML", StringComparison.Ordinal)
                || Regex.IsMatch(text, @"\.innerHTML\s*="))
            {
                // İstisna: dosyada DOMPurify ile sanitize edildiği görünür ise raw HTML sink kabul edilir.
                // Sanitize çağrısı sink'ten önce yapılmalıdır — pattern dosya seviyesinde kontrol edilir.
                var hasDocumentedSanitizer =
                       text.Contains("DOMPurify.sanitize", StringComparison.Ordinal)
                    || text.Contains("DOMPurify(", StringComparison.Ordinal)
                    || text.Contains("from 'dompurify'", StringComparison.Ordinal)
                    || text.Contains("from \"dompurify\"", StringComparison.Ordinal);

                if (!hasDocumentedSanitizer)
                    failures.Add($"{relative}: avoid raw HTML sinks without a documented sanitizer");
            }

            foreach (Match match in Regex.Matches(text, @"window\.open\((?<args>[^\n;]+)\)"))
            {
                if (!match.Groups["args"].Value.Contains("noopener", StringComparison.OrdinalIgnoreCase))
                    failures.Add($"{relative}: window.open must include noopener,noreferrer");
            }

            if (!relative.EndsWith(Path.Combine("lib", "authStorage.ts"), StringComparison.OrdinalIgnoreCase)
                && (text.Contains("sis_token", StringComparison.Ordinal)
                    || text.Contains("AUTH_STORAGE_KEYS.token", StringComparison.Ordinal)))
            {
                failures.Add($"{relative}: access-token storage must stay centralized in authStorage.ts");
            }
        }

        failures.Should().BeEmpty("ASVS V3/V7: frontend changes must not add unsafe HTML sinks or scattered token storage");
    }

    private static bool HasPermissionRequirement(string text) =>
        text.Contains("RequirePermission", StringComparison.Ordinal)
        || text.Contains("RequireAnyPermission", StringComparison.Ordinal);

    private static IEnumerable<EndpointBlock> EndpointBlocks()
    {
        foreach (var file in Directory.GetFiles(Path.Combine(RepoRoot(), "SIS.API", "Controllers"), "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);
            var classIndex = Array.FindIndex(lines, line => line.Contains("class ", StringComparison.Ordinal)
                && line.Contains("Controller", StringComparison.Ordinal));
            var classAttributes = classIndex >= 0
                ? string.Join('\n', lines[Math.Max(0, classIndex - 12)..classIndex])
                : string.Empty;

            var httpIndexes = lines
                .Select((line, index) => (line, index))
                .Where(item => Regex.IsMatch(item.line, @"\[Http(Get|Post|Put|Delete|Patch)"))
                .Select(item => item.index)
                .ToList();

            for (var i = 0; i < httpIndexes.Count; i++)
            {
                var httpIndex = httpIndexes[i];
                var methodStart = FindAttributeBlockStart(lines, httpIndex);
                var nextHttp = i + 1 < httpIndexes.Count ? httpIndexes[i + 1] : lines.Length;
                var methodEnd = Math.Min(nextHttp, lines.Length);
                var bodyEnd = Math.Min(nextHttp, lines.Length);
                var methodAttributes = string.Join('\n', lines[methodStart..Math.Min(methodEnd, httpIndex + 12)]);
                var body = string.Join('\n', lines[methodStart..bodyEnd]);

                yield return new EndpointBlock(
                    Path.GetRelativePath(RepoRoot(), file) + ":" + (httpIndex + 1),
                    classAttributes,
                    methodAttributes,
                    body);
            }
        }
    }

    private static int FindAttributeBlockStart(string[] lines, int httpIndex)
    {
        var index = httpIndex;
        while (index > 0)
        {
            var previous = lines[index - 1].TrimStart();
            if (previous.StartsWith('[') || previous.StartsWith("//") || previous.Length == 0)
            {
                index--;
                continue;
            }

            break;
        }

        return index;
    }

    private static string NormalizeInputType(string raw)
    {
        var type = raw.Trim().TrimEnd('?');
        var genericMatch = Regex.Match(type, @"^[A-Za-z0-9_]+<(?<inner>[A-Za-z0-9_]+)>");
        return genericMatch.Success ? genericMatch.Groups["inner"].Value : type;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "SIS.API"))
                && Directory.Exists(Path.Combine(directory.FullName, "SIS.Web")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed record EndpointBlock(
        string Location,
        string ClassAttributes,
        string MethodAttributes,
        string Body);
}
