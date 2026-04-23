# Student Information System (SIS)

## Project Overview
Ankara Bilim University - Student Information System

## Tech Stack
- **Backend:** ASP.NET Core 10 Web API (.NET 10 SDK, targeting net10.0)
- **Frontend:** React + TypeScript + Vite + shadcn/ui + TanStack Query
- **Database:** PostgreSQL (localhost:5432)
- **ORM:** Entity Framework Core 10
- **Auth:** JWT Bearer Tokens + RBAC
- **Validation:** FluentValidation

## Architecture: Simplified Clean Architecture
```
SIS.Domain/           → Entities, Enums, ValueObjects, Exceptions (zero dependencies)
SIS.Application/      → Services, DTOs, Validators (depends on Domain)
SIS.Infrastructure/   → EF Core, DbContext, Services (depends on Domain + Application)
SIS.API/              → Controllers, Middleware, DI (depends on Application + Infrastructure)
SIS.Tests/            → Unit tests (depends on Application + Domain)
```
Dependency direction: API → Application → Domain ← Infrastructure

## Key Business Rules (Course Registration)
All thresholds below are configurable via SystemConfig (admin panel), values shown are defaults:
- ECTS caps: CGPA-based limits (defaults: <2.00 → 30, 2.00-2.49 → 42, ≥2.50 → 48)
- Retention threshold (default: 1.70) → student retained after 1st semester, cannot take upper-level courses
- FF/NA/U courses must be re-selected before new courses
- Full overlap for first-time courses → categorically blocked, no override
- Full/Partial overlap for repeated (FF) courses → allowed with advisor + dept manager approval
- Instructor cannot be assigned to overlapping sections
- Registration plan: soft hold on capacity, cleared at midnight if unconfirmed

## Term Lifecycle (Faz 1 revize — otomatik model)
Inactive → Active → Cancelled

- 3 durum, **gösterim** için enum (stored değil). Durum `term.GetStatus(now)` ile hesaplanır.
- Stored alan: sadece `IsCancelled: bool` — admin'in tek manuel aksiyonu.
- Active: `!IsCancelled && TermStart ≤ now ≤ TermEnd` (otomatik, tarih bazlı)
- Inactive: tarih aralığı dışı (otomatik)
- Cancelled: admin iptal etmiş (manuel). Cascade: tüm section'lar Cancelled olur.
- Unique constraint: `(Year, TermType)` — aynı yıl/tip kombinasyonu tek kayıt.

## TermType (9 değer — Ankara Bilim pattern)
Fall, Spring, Summer, FallResit, SpringResit, SummerResit, SpringOther, SummerOther, Exemption

- Eski `SemesterType` enum silindi; semester bilgisi TermType'a birleşti.
- TermType default: Fall.

## Section Lifecycle (Faz 1 — otomatik model)
Draft ↔ Active ↔ Cancelled

- 3 durumlu enum `CourseSectionStatus`, **stored** (index ile sorgulanır).
- Draft ↔ Active otomatik: hoca + derslik + slot dolunca Active, biri eksilince Draft.
- Cancelled: admin'in manuel aksiyonu (CloseSection / ReactivateSection endpoint'leri).
- Geriye uyumluluk: `IsActive` computed property `=> Status == Active`. LINQ sorgularında `cs.Status == CourseSectionStatus.Active` kullanılır (EF translate için).

## Term Phases (computed, state değil)
- IsRegistrationOpen: IsActive(now) + (override ?? now ∈ [RegStart, RegEnd])
- IsAddDropOpen: IsActive(now) + (override ?? now ∈ [AddDropStart, AddDropEnd])
- IsWithdrawalOpen: IsActive(now) + (override ?? TermStart ≤ now ≤ WithdrawalDeadline)
- IsGradeEntryOpen: IsActive(now) + (override ?? now ∈ [GradeEntryStart, GradeEntryEnd])
- Override null → takvim konuşur; true/false → admin override
- Cancelled dönemde override değiştirilemez.

## Admin Term API (Faz 1 revize)
- `GET /api/admin/terms` — tüm dönemler
- `GET /api/admin/terms/{id}` — tek dönem
- `POST /api/admin/terms` — yeni dönem (TimeSlot grid'i otomatik oluşturulur)
- `PUT /api/admin/terms/{id}` — düzenleme (Cancelled dönem düzenlenemez)
- `POST /api/admin/terms/{id}/cancel` — iptal + section cascade
- `POST /api/admin/terms/{id}/uncancel` — iptali geri al
- `POST /api/admin/terms/{id}/override` — faz override
- Silinmiş endpoint'ler: `/activate`, `/deactivate`, `/transition` (manuel state machine yok)

## AcademicTerm Yeni Alanlar (Faz 1)
- `ShortEnglishName: string` — kısa İngilizce ad (zorunlu, default empty)
- `OtherName: string?` — diğer adı (opsiyonel)
- `YoksisStatus: YoksisStatus` — YÖKSİS raporlama durumu (Inactive/Active)

## Sidebar "Akademik İşlemler" grubu
- Akademik Dönem Tanımları (TermManagementPage)
- Fakülte / Bölüm / Program (AcademicStructurePage)
- Akademik Takvim (TermCalendarPage — öğrenci/hoca)
- Takvim Oluşturucu (AcademicCalendarGeneratorPage)
- Sınıf Atlatma Kuralları (PromotionRulesPage)

Eski gruplar ("Dönem Yönetimi", "Akademik Yapı") silindi.

## Code Conventions
- Domain entities: plain C# classes, no EF Core imports
- ApplicationUser (IdentityUser) lives in SIS.Infrastructure/Persistence/
- Schedule data stored in SectionSchedule table
- Composite PKs (CoursePrerequisite, CourseEquivalency, CurriculumCourse) configured in OnModelCreating
- SystemConfig table for configurable business rules (no hardcoded values)
- ISystemConfigProvider with 5-minute memory cache for config reads

## Database
- PostgreSQL, Host: localhost, Port: 5432, Database: StudentInfoSystem
- appsettings.json: placeholder değerler (CHANGE_ME) — GitHub'a gider
- appsettings.Development.json: gerçek şifreler — .gitignore'da, GitHub'a gitmez

## Commands
```bash
dotnet build                          # Build solution
dotnet test                           # Run tests
dotnet ef database update -p SIS.Infrastructure -s SIS.API  # Apply migrations
```

