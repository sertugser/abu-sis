using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.Integration;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Integration;

/// <summary>
/// Mock NVİ identity verification. Returns deterministic fake data based on the
/// student's real name and personal-info record. The configured mode
/// ("Integration.NviMode", default "Mock") selects behaviour:
/// Mock → fabricated result, Disabled → throws, Live → not yet implemented.
/// <see cref="IHttpClientFactory"/> is injected for the future Live path.
/// </summary>
public class MockNviIdentityService : IExternalIdentityService
{
    private const string ConfigKey = "Integration.NviMode";

    private readonly AppDbContext _context;
    private readonly ISystemConfigProvider _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public MockNviIdentityService(
        AppDbContext context,
        ISystemConfigProvider config,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IdentityVerificationResult> VerifyAsync(int studentId, CancellationToken ct = default)
    {
        var mode = await _config.GetStringAsync(ConfigKey, "Mock", ct);

        if (string.Equals(mode, "Disabled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("NVİ entegrasyonu kapalı.");

        if (string.Equals(mode, "Live", StringComparison.OrdinalIgnoreCase))
            throw new NotImplementedException("NVİ canlı entegrasyonu henüz yapılandırılmadı.");

        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.PersonalInfo)
            .FirstOrDefaultAsync(s => s.Id == studentId, ct)
            ?? throw new KeyNotFoundException($"Öğrenci bulunamadı: {studentId}");

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == student.UserId, ct);

        var fullName = user is not null
            ? $"{user.FirstName} {user.LastName}".Trim()
            : student.StudentNumber;

        var info = student.PersonalInfo;

        return new IdentityVerificationResult(
            IsMock: true,
            Matched: true,
            FullName: fullName,
            BirthDate: info?.BirthDate,
            BirthPlace: info?.BirthPlace ?? "Ankara",
            AddressProvince: info?.AddressProvince ?? "Ankara",
            AddressDistrict: info?.AddressDistrict ?? "Çankaya",
            Mode: "Mock",
            QueriedAt: DateTime.UtcNow,
            Message: "Test/mock verisi — gerçek NVİ sorgusu yapılmadı.");
    }
}
