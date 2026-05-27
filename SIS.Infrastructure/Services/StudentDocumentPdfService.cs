using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SIS.Application.Admin.Services;
using SIS.Application.Common.Services;
using SIS.Application.Common.Time;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Öğrenci Bilgi Formu + Öğrenci Belgesi PDF üreticisi. Veri akışı:
/// Student (+ StudentProgram→Program→Department→Faculty, Advisor, PersonalInfo)
/// + ApplicationUser (ad soyad) → bordered key-value panel'ler / resmî metin.
/// CourseHistoryPdfService deseniyle hizalı (başlık/üniversite adı/footer).
/// </summary>
public class StudentDocumentPdfService : IStudentDocumentPdfService
{
    private readonly AppDbContext _context;
    private readonly IStudentPhotoStorage _photoStorage;
    private readonly IDateTimeProvider _clock;

    private static readonly TimeZoneInfo TurkeyTimeZone = ResolveTurkeyTimeZone();

    private const string UniversityName = "Ankara Bilim Üniversitesi";
    private const string Watermark = "T.C.";
    private const string PlaceholderValue = "-";

    public StudentDocumentPdfService(
        AppDbContext context,
        IStudentPhotoStorage photoStorage,
        IDateTimeProvider clock)
    {
        _context = context;
        _photoStorage = photoStorage;
        _clock = clock;
    }

    public async Task<byte[]?> GenerateStudentInfoFormAsync(int studentId, CancellationToken ct = default)
    {
        var data = await LoadAsync(studentId, ct);
        if (data is null) return null;

        var photoBytes = await TryLoadPhotoAsync(studentId, data.PhotoExtension, ct);
        var generatedAt = LocalNowText();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().Element(c => RenderHeader(c, "Öğrenci Bilgi Formu", generatedAt));

                page.Content().PaddingVertical(6).Column(col =>
                {
                    col.Spacing(8);

                    // Üst satır: kimlik paneli sol, fotoğraf (varsa) sağ üst.
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Element(c => RenderIdentitySection(c, data));
                        if (photoBytes is not null)
                        {
                            row.ConstantItem(110)
                                .PaddingLeft(8)
                                .Border(0.5f).BorderColor(Colors.Grey.Medium)
                                .Padding(2)
                                .Height(132)
                                .Image(photoBytes).FitArea();
                        }
                    });

                    col.Item().Element(c => RenderProgramSection(c, data));
                    col.Item().Element(c => RenderContactSection(c, data));
                    col.Item().Element(c => RenderMilitarySection(c, data));
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.CurrentPageNumber().FontSize(8);
                    t.Span(" / ").FontSize(8);
                    t.TotalPages().FontSize(8);
                });
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]?> GenerateEnrollmentCertificateAsync(int studentId, CancellationToken ct = default)
    {
        var data = await LoadAsync(studentId, ct);
        if (data is null) return null;

        var generatedAt = LocalNowText();
        var issueDate = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, TurkeyTimeZone)
            .ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        var faculty = string.IsNullOrWhiteSpace(data.FacultyName) ? PlaceholderValue : data.FacultyName!;
        var program = string.IsNullOrWhiteSpace(data.ProgramName) ? PlaceholderValue : data.ProgramName!;
        var statusTr = StudentStatusLabel(data.Status);

        var body =
            $"{data.FullName} ({data.StudentNumber}) adlı öğrenci, {faculty} {program} programında " +
            $"{statusTr} olarak kayıtlı öğrencimizdir. İşbu belge ilgili öğrencinin talebi üzerine düzenlenmiştir.";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(11));

                page.Header().Element(c => RenderHeader(c, "Öğrenci Belgesi", generatedAt));

                page.Content().PaddingTop(40).Column(col =>
                {
                    col.Spacing(24);

                    col.Item().PaddingHorizontal(10).Text(body)
                        .LineHeight(1.6f).FontSize(12);

                    col.Item().PaddingTop(10).AlignRight()
                        .Text($"Belge Tarihi: {issueDate}").FontSize(10);

                    col.Item().PaddingTop(50).AlignRight().Column(sign =>
                    {
                        sign.Item().AlignCenter().Text("Öğrenci İşleri Daire Başkanlığı").Bold().FontSize(11);
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.CurrentPageNumber().FontSize(8);
                    t.Span(" / ").FontSize(8);
                    t.TotalPages().FontSize(8);
                });
            });
        });

        return document.GeneratePdf();
    }

    // ── Header (T.C. / üniversite / başlık + sağda tarih) ───────────────────
    private static void RenderHeader(IContainer container, string title, string generatedAt)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text(Watermark).FontSize(9);
            col.Item().AlignCenter().Text(UniversityName).FontSize(11).Bold();
            col.Item().AlignCenter().Text(title).FontSize(10).SemiBold();
            col.Item().PaddingTop(2).Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(120).AlignRight().Text(generatedAt).FontSize(8);
            });
            col.Item().PaddingTop(4).LineHorizontal(0.8f);
        });
    }

    // ── Bordered key-value bölümler ─────────────────────────────────────────
    private static void RenderSection(IContainer container, string heading, Action<ColumnDescriptor> rows)
    {
        container.Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(8).Column(col =>
        {
            col.Spacing(3);
            col.Item().Text(heading).Bold().FontSize(10);
            col.Item().PaddingBottom(2).LineHorizontal(0.4f);
            rows(col);
        });
    }

    private static void RenderIdentitySection(IContainer container, StudentDocumentData d)
    {
        RenderSection(container, "Kimlik", col =>
        {
            RenderInfoLine(col, "Öğrenci No", d.StudentNumber);
            RenderInfoLine(col, "Ad Soyad", Display(d.FullName));
            RenderInfoLine(col, "Doğum Tarihi", FormatDate(d.BirthDate));
            RenderInfoLine(col, "Doğum Yeri", Display(d.BirthPlace));
            RenderInfoLine(col, "Cinsiyet", GenderLabel(d.Gender));
            RenderInfoLine(col, "Uyruk", Display(d.Nationality));
            RenderInfoLine(col, "Kan Grubu", Display(d.BloodType));
            RenderInfoLine(col, "Medeni Durum", MaritalStatusLabel(d.MaritalStatus));
        });
    }

    private static void RenderProgramSection(IContainer container, StudentDocumentData d)
    {
        RenderSection(container, "Program", col =>
        {
            RenderInfoLine(col, "Fakülte", Display(d.FacultyName));
            RenderInfoLine(col, "Bölüm", Display(d.DepartmentName));
            RenderInfoLine(col, "Program", Display(d.ProgramName));
            RenderInfoLine(col, "Durum", StudentStatusLabel(d.Status));
            RenderInfoLine(col, "Sınıf", d.ClassYear?.ToString(CultureInfo.InvariantCulture) ?? PlaceholderValue);
            RenderInfoLine(col, "Danışman", Display(d.AdvisorName));
        });
    }

    private static void RenderContactSection(IContainer container, StudentDocumentData d)
    {
        RenderSection(container, "İletişim", col =>
        {
            RenderInfoLine(col, "Cep Tel", Display(d.MobilePhone));
            RenderInfoLine(col, "Telefon", Display(d.Phone));
            RenderInfoLine(col, "E-posta", Display(d.SecondaryEmail));
            RenderInfoLine(col, "Adres", Display(d.Address));
            RenderInfoLine(col, "Acil Durum", Display(d.EmergencyContact));
        });
    }

    private static void RenderMilitarySection(IContainer container, StudentDocumentData d)
    {
        RenderSection(container, "Askerlik", col =>
        {
            RenderInfoLine(col, "Durum", MilitaryStatusLabel(d.MilitaryStatus));
        });
    }

    private static void RenderInfoLine(ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(95).Text(label).Bold().FontSize(9);
            row.ConstantItem(10).Text(":").FontSize(9);
            row.RelativeItem().Text(value).FontSize(9);
        });
    }

    // ── Veri yükleme ────────────────────────────────────────────────────────
    private async Task<StudentDocumentData?> LoadAsync(int studentId, CancellationToken ct)
    {
        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.Faculty)
            .Include(s => s.Advisor)
            .Include(s => s.PersonalInfo)
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student is null) return null;

        var fullName = await ResolveFullNameAsync(student.UserId, ct);

        var activeProgram = student.StudentPrograms
            .FirstOrDefault(sp => sp.IsActive && sp.Type == StudentProgramType.Main)
            ?? student.StudentPrograms.FirstOrDefault(sp => sp.IsActive);

        string? advisorName = null;
        if (student.Advisor is not null)
        {
            var advisorUser = await _context.Set<ApplicationUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == student.Advisor.UserId, ct);
            if (advisorUser is not null)
                advisorName = $"{student.Advisor.Title} {advisorUser.FirstName} {advisorUser.LastName}".Trim();
        }

        var info = student.PersonalInfo;

        return new StudentDocumentData
        {
            StudentNumber = student.StudentNumber,
            FullName = fullName,
            Status = student.Status,
            ClassYear = (student.CompletedSemesters / 2) + 1,
            FacultyName = activeProgram?.Program.Department.Faculty.Name,
            DepartmentName = activeProgram?.Program.Department.Name,
            ProgramName = activeProgram?.Program.Name,
            AdvisorName = advisorName,
            PhotoExtension = info?.PhotoExtension,
            BirthDate = info?.BirthDate,
            BirthPlace = info?.BirthPlace,
            Gender = info?.Gender,
            Nationality = info?.Nationality,
            BloodType = info?.BloodType,
            MaritalStatus = info?.MaritalStatus,
            MobilePhone = info?.MobilePhone,
            Phone = info?.Phone,
            SecondaryEmail = info?.SecondaryEmail,
            Address = ComposeAddress(info),
            EmergencyContact = ComposeEmergencyContact(info),
            MilitaryStatus = info?.MilitaryStatus,
        };
    }

    private async Task<string> ResolveFullNameAsync(int userId, CancellationToken ct)
    {
        var user = await _context.Set<ApplicationUser>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is null ? string.Empty : $"{user.FirstName} {user.LastName}".Trim();
    }

    private async Task<byte[]?> TryLoadPhotoAsync(int studentId, string? extension, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(extension)) return null;

        var photo = await _photoStorage.GetAsync(studentId, extension, ct);
        if (photo is null) return null;

        await using var stream = photo.Value.Stream;
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    private static string? ComposeAddress(StudentPersonalInfo? info)
    {
        if (info is null) return null;
        var parts = new[] { info.AddressLine, info.AddressDistrict, info.AddressProvince, info.PostalCode }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var joined = string.Join(" / ", parts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private static string? ComposeEmergencyContact(StudentPersonalInfo? info)
    {
        if (info is null) return null;
        var parts = new[] { info.EmergencyContactName, info.EmergencyContactRelation, info.EmergencyContactPhone }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var joined = string.Join(" - ", parts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    // ── Türkçe etiketler ────────────────────────────────────────────────────
    private static string Display(string? value) =>
        string.IsNullOrWhiteSpace(value) ? PlaceholderValue : value!;

    private static string FormatDate(DateTime? value) =>
        value.HasValue ? value.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : PlaceholderValue;

    private static string GenderLabel(Gender? gender) => gender switch
    {
        Gender.Male => "Erkek",
        Gender.Female => "Kadın",
        _ => PlaceholderValue,
    };

    private static string MaritalStatusLabel(MaritalStatus? status) => status switch
    {
        MaritalStatus.Single => "Bekar",
        MaritalStatus.Married => "Evli",
        _ => PlaceholderValue,
    };

    private static string MilitaryStatusLabel(MilitaryServiceStatus? status) => status switch
    {
        MilitaryServiceStatus.NotApplicable => "Tabi Değil",
        MilitaryServiceStatus.Liable => "Yapması Gerekiyor",
        MilitaryServiceStatus.Deferred => "Tecilli",
        MilitaryServiceStatus.Exempt => "Muaf",
        MilitaryServiceStatus.Completed => "Yapıldı",
        _ => PlaceholderValue,
    };

    private static string StudentStatusLabel(StudentStatus status) => status switch
    {
        StudentStatus.Active => "Aktif",
        StudentStatus.Passive => "Pasif",
        StudentStatus.Frozen => "Donduruldu",
        StudentStatus.Suspended => "Uzaklaştırıldı",
        StudentStatus.Dismissed => "İlişiği Kesildi",
        StudentStatus.Graduated => "Mezun",
        _ => status.ToString(),
    };

    private string LocalNowText() =>
        TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, TurkeyTimeZone)
            .ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    private static TimeZoneInfo ResolveTurkeyTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Local; }
    }

    // ── İç veri taşıyıcısı ──────────────────────────────────────────────────
    private sealed class StudentDocumentData
    {
        public string StudentNumber { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public StudentStatus Status { get; init; }
        public int? ClassYear { get; init; }
        public string? FacultyName { get; init; }
        public string? DepartmentName { get; init; }
        public string? ProgramName { get; init; }
        public string? AdvisorName { get; init; }
        public string? PhotoExtension { get; init; }
        public DateTime? BirthDate { get; init; }
        public string? BirthPlace { get; init; }
        public Gender? Gender { get; init; }
        public string? Nationality { get; init; }
        public string? BloodType { get; init; }
        public MaritalStatus? MaritalStatus { get; init; }
        public string? MobilePhone { get; init; }
        public string? Phone { get; init; }
        public string? SecondaryEmail { get; init; }
        public string? Address { get; init; }
        public string? EmergencyContact { get; init; }
        public MilitaryServiceStatus? MilitaryStatus { get; init; }
    }
}
