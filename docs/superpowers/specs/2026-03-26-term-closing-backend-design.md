# Dönem Kapatma Backend Tasarımı

## Amaç

Bölüm bazlı 4 adımlı dönem kapatma sürecini backend'de implemente etmek. Her bölüm kendi dönemini bağımsız kapatabilecek. Adımlar esnek — geri alınabilir.

## Mevcut Altyapı

Kullanılacak mevcut servisler:

| Servis | Metot | Kullanım |
|--------|-------|----------|
| `IAcademicProgressionService` | `CheckGradeLockReadinessAsync(departmentId, termId?)` | Adım 1 ön kontrol |
| `IAcademicProgressionService` | `RecalculateDepartmentGpaAsync(departmentId)` | Adım 2 |
| `IAcademicProgressionService` | `PromoteDepartmentStudentsAsync(departmentId, dryRun)` | Adım 3 |
| `IGpaCalculationService` | `RecalculateAndUpdateAsync(studentId)` | Adım 2 alt çağrı |

Mevcut entity'ler:
- `AcademicTerm` — `IsActive` flag'i var, dönem tarihleri var
- `CourseSection` — `IsFinalized` flag'i var
- `CourseSection → Course → DepartmentId` — bölüm bağlantısı Course üzerinden

## Yeni Entity: TermClosingProgress

```csharp
// SIS.Domain/Entities/TermClosingProgress.cs
namespace SIS.Domain.Entities;

public class TermClosingProgress
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;
    public TermClosingStep Step { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CompletedByUserId { get; set; }
    public string? Notes { get; set; }
}
```

```csharp
// SIS.Domain/Enums/TermClosingStep.cs
namespace SIS.Domain.Enums;

public enum TermClosingStep
{
    GradeLock = 1,
    GpaCalculation = 2,
    Promotion = 3,
    NewTerm = 4
}
```

## Yeni Servis: ITermClosingService

```csharp
// SIS.Application/TermClosing/Services/ITermClosingService.cs
public interface ITermClosingService
{
    Task<TermClosingStatusDto> GetStatusAsync(int departmentId, CancellationToken ct = default);
    Task<GradeLockResultDto> LockGradesAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<GradeLockResultDto> UnlockGradesAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<GpaRecalculationResultDto> CalculateGpaAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<BatchProgressionResultDto> PreviewPromotionAsync(int departmentId, CancellationToken ct = default);
    Task<BatchProgressionResultDto> PromoteAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<TermClosingStepResultDto> RollbackPromotionAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<NewTermResultDto> OpenNewTermAsync(int departmentId, int userId, NewTermRequestDto request, CancellationToken ct = default);
}
```

## DTOs

```csharp
// SIS.Application/TermClosing/DTOs/TermClosingDtos.cs

public class TermClosingStatusDto
{
    public int DepartmentId { get; set; }
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public List<TermClosingStepStatus> Steps { get; set; } = new();
}

public class TermClosingStepStatus
{
    public TermClosingStep Step { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedByName { get; set; }
    public string? Notes { get; set; }
}

public class TermClosingStepResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class NewTermRequestDto
{
    public string Name { get; set; } = string.Empty;           // ör: "2025-2026 Bahar"
    public int Year { get; set; }                               // ör: 2025
    public SemesterType Semester { get; set; }                  // Fall, Spring, Summer
    public DateTime RegistrationStart { get; set; }
    public DateTime RegistrationEnd { get; set; }
    public DateTime TermStart { get; set; }
    public DateTime TermEnd { get; set; }
}

public class NewTermResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? NewTermId { get; set; }
}
```

## Controller: TermClosingController

Route: `api/term-closing`
Yetki: `DepartmentManager,SystemAdministrator`

| Method | Route | İşlev |
|--------|-------|-------|
| GET | `{departmentId}/status` | 4 adımın durumunu getir |
| POST | `{departmentId}/lock-grades` | Tüm section'ları toplu kilitle |
| POST | `{departmentId}/unlock-grades` | Kilidi geri aç |
| POST | `{departmentId}/calculate-gpa` | Toplu GPA hesapla |
| GET | `{departmentId}/preview-promote` | Sınıf atlama önizleme (dry-run) |
| POST | `{departmentId}/promote` | Sınıf atlama çalıştır |
| POST | `{departmentId}/rollback-promote` | Sınıf atlamayı geri al |
| POST | `{departmentId}/open-new-term` | Yeni dönem oluştur |

## Adım Detayları

### Adım 1: Not Kilitle (LockGrades)

1. Aktif dönemi bul
2. `CourseSection` tablosunda `Course.DepartmentId == departmentId && TermId == activeTermId` olan tüm section'ları bul
3. Henüz finalize edilmemiş section'larda not hesaplaması yap (mevcut `Finalize` mantığı: component ağırlıkları kontrol, harf notu hesapla, eksik not → NA)
4. Tüm section'ları `IsFinalized = true` yap
5. `TermClosingProgress` kaydı oluştur/güncelle
6. Sonuç: kaç section kilitlendi, kaç tanesinde eksik not vardı

**Geri alma (UnlockGrades):**
- Tüm section'ları `IsFinalized = false` yap
- `TermClosingProgress.IsCompleted = false` yap
- Sonraki adımlar (GPA, Promotion) da completed ise uyarı ver ama engelleme

### Adım 2: GPA Hesapla (CalculateGpa)

1. Mevcut `AcademicProgressionService.RecalculateDepartmentGpaAsync(departmentId)` çağır
2. `TermClosingProgress` kaydı oluştur/güncelle
3. Sonuç: mevcut `GpaRecalculationResultDto` döndür

**Geri alma:** Yeniden hesaplama ile kendi kendini düzeltir, ayrı rollback gerekmez.

### Adım 3: Sınıf Atla (Promote)

1. Önizleme: `PromoteDepartmentStudentsAsync(departmentId, dryRun: true)`
2. Gerçek çalıştırma: `PromoteDepartmentStudentsAsync(departmentId, dryRun: false)`
3. Promotion öncesi her öğrencinin `CompletedSemesters` değerini `Notes` alanına JSON olarak kaydet (rollback için)
4. `TermClosingProgress` kaydı oluştur/güncelle

**Geri alma (RollbackPromotion):**
- `Notes` alanındaki JSON'dan eski `CompletedSemesters` değerlerini oku
- Her öğrenciyi eski değerine geri döndür
- `TermClosingProgress.IsCompleted = false` yap

### Adım 4: Yeni Dönem Aç (OpenNewTerm)

1. `NewTermRequestDto` ile yeni `AcademicTerm` entity'si oluştur
2. Mevcut aktif dönemin `IsActive = false` yap
3. Yeni dönemin `IsActive = true` yap
4. `TermClosingProgress` kaydı oluştur/güncelle
5. AddDrop tarihlerini RegistrationStart/End ile aynı yap (sonra düzenlenebilir)

**Not:** Bu adım üniversite genelini etkiler (IsActive). Birden fazla bölüm aynı termi kapatıyorsa, son bölüm yeni dönem açar. Yeni dönem zaten varsa (başka bölüm açtıysa) tekrar oluşturmaz, mevcut olanı kullanır.

## EF Core Migration

- `TermClosingProgress` entity'si `AppDbContext`'e eklenecek
- Composite unique index: `(DepartmentId, TermId, Step)` — her adımdan sadece 1 kayıt
- Navigation property'ler: Department, Term

## Test Planı

Servis testleri (InMemory DB ile):
- LockGrades: tüm section'lar finalize ediliyor mu
- UnlockGrades: finalize geri alınıyor mu
- CalculateGpa: mevcut servis çağrılıyor mu, progress kaydı oluşuyor mu
- Promote: dryRun ve gerçek çalıştırma, snapshot Notes'a yazılıyor mu
- RollbackPromotion: eski değerler restore ediliyor mu
- OpenNewTerm: yeni term oluşuyor mu, eski term deactive oluyor mu, aynı term tekrar oluşturulmuyor mu
- GetStatus: tüm adımların durumu doğru dönüyor mu

Controller testleri (reflection):
- Route, authorize attribute, HTTP method doğrulaması
