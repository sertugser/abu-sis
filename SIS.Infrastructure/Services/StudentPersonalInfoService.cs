using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class StudentPersonalInfoService : IStudentPersonalInfoService
{
    private readonly AppDbContext _context;

    public StudentPersonalInfoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<StudentPersonalInfoDto?> GetAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var studentExists = await _context.Students
            .AsNoTracking()
            .AnyAsync(s => s.Id == studentId, cancellationToken);
        if (!studentExists)
            return null;

        var info = await _context.StudentPersonalInfos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId, cancellationToken);

        // No row yet → return an "empty" but valid DTO.
        if (info is null)
            return EmptyDto(studentId);

        return ToDto(info);
    }

    public async Task UpsertAsync(int studentId, UpdateStudentPersonalInfoDto dto, CancellationToken cancellationToken = default)
    {
        var studentExists = await _context.Students
            .AnyAsync(s => s.Id == studentId, cancellationToken);
        if (!studentExists)
            throw new KeyNotFoundException($"Öğrenci bulunamadı: {studentId}");

        // Parse enums first so an invalid value aborts before any mutation.
        var gender = ParseEnum<Gender>(dto.Gender, nameof(dto.Gender));
        var maritalStatus = ParseEnum<MaritalStatus>(dto.MaritalStatus, nameof(dto.MaritalStatus));
        var militaryStatus = ParseEnum<MilitaryServiceStatus>(dto.MilitaryStatus, nameof(dto.MilitaryStatus));

        var info = await _context.StudentPersonalInfos
            .FirstOrDefaultAsync(p => p.StudentId == studentId, cancellationToken);

        var now = DateTime.UtcNow;
        var isNew = info is null;
        if (info is null)
        {
            info = new StudentPersonalInfo
            {
                StudentId = studentId,
                CreatedAt = now,
            };
            _context.StudentPersonalInfos.Add(info);
        }
        else
        {
            info.UpdatedAt = now;
        }

        // Contact
        info.MobilePhone = dto.MobilePhone;
        info.Phone = dto.Phone;
        info.SecondaryEmail = dto.SecondaryEmail;
        info.AddressProvince = dto.AddressProvince;
        info.AddressDistrict = dto.AddressDistrict;
        info.AddressLine = dto.AddressLine;
        info.PostalCode = dto.PostalCode;
        info.EmergencyContactName = dto.EmergencyContactName;
        info.EmergencyContactPhone = dto.EmergencyContactPhone;
        info.EmergencyContactRelation = dto.EmergencyContactRelation;

        // Demographic
        info.BirthDate = dto.BirthDate;
        info.BirthPlace = dto.BirthPlace;
        info.Gender = gender;
        info.Nationality = dto.Nationality;
        info.BloodType = dto.BloodType;
        info.MaritalStatus = maritalStatus;
        info.PassportNo = dto.PassportNo;

        // Military
        info.MilitaryStatus = militaryStatus;
        info.MilitaryDeferralUntil = dto.MilitaryDeferralUntil;
        info.MilitaryOffice = dto.MilitaryOffice;

        // Photo
        info.PhotoExtension = dto.PhotoExtension;

        _ = isNew; // CreatedAt set on insert; UpdatedAt left null for new rows.

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static TEnum? ParseEnum<TEnum>(string? value, string fieldName)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;

        throw new InvalidOperationException($"Geçersiz {fieldName} değeri: {value}");
    }

    private static StudentPersonalInfoDto EmptyDto(int studentId) => new(
        StudentId: studentId,
        MobilePhone: null,
        Phone: null,
        SecondaryEmail: null,
        AddressProvince: null,
        AddressDistrict: null,
        AddressLine: null,
        PostalCode: null,
        EmergencyContactName: null,
        EmergencyContactPhone: null,
        EmergencyContactRelation: null,
        BirthDate: null,
        BirthPlace: null,
        Gender: null,
        Nationality: null,
        BloodType: null,
        MaritalStatus: null,
        PassportNo: null,
        MilitaryStatus: null,
        MilitaryDeferralUntil: null,
        MilitaryOffice: null,
        PhotoExtension: null);

    private static StudentPersonalInfoDto ToDto(StudentPersonalInfo info) => new(
        StudentId: info.StudentId,
        MobilePhone: info.MobilePhone,
        Phone: info.Phone,
        SecondaryEmail: info.SecondaryEmail,
        AddressProvince: info.AddressProvince,
        AddressDistrict: info.AddressDistrict,
        AddressLine: info.AddressLine,
        PostalCode: info.PostalCode,
        EmergencyContactName: info.EmergencyContactName,
        EmergencyContactPhone: info.EmergencyContactPhone,
        EmergencyContactRelation: info.EmergencyContactRelation,
        BirthDate: info.BirthDate,
        BirthPlace: info.BirthPlace,
        Gender: info.Gender?.ToString(),
        Nationality: info.Nationality,
        BloodType: info.BloodType,
        MaritalStatus: info.MaritalStatus?.ToString(),
        PassportNo: info.PassportNo,
        MilitaryStatus: info.MilitaryStatus?.ToString(),
        MilitaryDeferralUntil: info.MilitaryDeferralUntil,
        MilitaryOffice: info.MilitaryOffice,
        PhotoExtension: info.PhotoExtension);
}
