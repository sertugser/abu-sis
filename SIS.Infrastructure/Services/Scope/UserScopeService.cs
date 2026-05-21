using Microsoft.EntityFrameworkCore;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Persistence.Extensions;

namespace SIS.Infrastructure.Services.Scope;

public class UserScopeService : IUserScopeService
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissionService;

    public UserScopeService(AppDbContext db, IPermissionService permissionService)
    {
        _db = db;
        _permissionService = permissionService;
    }

    public async Task<UserScopeDto?> GetForUserAsync(int userId, CancellationToken ct = default)
    {
        var scope = await _db.UserScopes
            .WithoutScope()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId, ct);

        return scope is null
            ? null
            : new UserScopeDto(scope.UserId, scope.Kind, scope.FacultyId, scope.CreatedAt, scope.UpdatedAt);
    }

    public async Task<UserScopeDto> SetForUserAsync(int userId, SetUserScopeDto dto, CancellationToken ct = default)
    {
        if (dto.Kind == ScopeKind.Faculty && dto.FacultyId is null)
        {
            throw new InvalidOperationException("FacultyId is required for faculty scope.");
        }

        if (dto.FacultyId is not null)
        {
            var facultyExists = await _db.Faculties
                .WithoutScope()
                .AnyAsync(faculty => faculty.Id == dto.FacultyId.Value, ct);
            if (!facultyExists)
            {
                throw new KeyNotFoundException($"Fakülte bulunamadı: {dto.FacultyId.Value}");
            }
        }

        var scope = await _db.UserScopes
            .WithoutScope()
            .FirstOrDefaultAsync(item => item.UserId == userId && item.Kind == dto.Kind, ct);

        if (scope is null)
        {
            scope = new UserScope
            {
                UserId = userId,
                Kind = dto.Kind,
                FacultyId = dto.FacultyId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _db.UserScopes.Add(scope);
        }
        else
        {
            scope.FacultyId = dto.FacultyId;
            scope.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        _permissionService.InvalidateUserCache(userId);

        return new UserScopeDto(scope.UserId, scope.Kind, scope.FacultyId, scope.CreatedAt, scope.UpdatedAt);
    }

    public async Task ClearForUserAsync(int userId, CancellationToken ct = default)
    {
        var rows = await _db.UserScopes
            .WithoutScope()
            .Where(item => item.UserId == userId)
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            return;
        }

        _db.UserScopes.RemoveRange(rows);
        await _db.SaveChangesAsync(ct);
        _permissionService.InvalidateUserCache(userId);
    }
}
