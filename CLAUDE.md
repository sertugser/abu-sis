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

## Akademik Takvim (Faz 2 — entity split)
7 tarih + 4 override alanı `AcademicTerm`'den ayrılıp yeni `TermAcademicCalendar` entity'sine taşındı (1-1 ilişki, cascade delete, unique FK).

- **Stored `AcademicTerm` alanları**: kimlik + `TermStart`/`TermEnd` + `IsCancelled` + `YoksisStatus`
- **Stored `TermAcademicCalendar` alanları**: 7 tarih (RegistrationStart/End, AddDropStart/End, WithdrawalDeadline, GradeEntryStart/End) + 4 override (RegistrationOverride, AddDropOverride, WithdrawalOverride, GradeEntryOverride) + CreatedAt/UpdatedAt
- **Navigation**: `AcademicTerm.AcademicCalendar?` (nullable — takvim opsiyonel; term oluşunca yok, admin sonradan ekler)
- **Include zorunlu**: `term.IsRegistrationOpen(now)` vb. çağıran her query `.Include(t => t.AcademicCalendar)` içermeli. AcademicCalendar null ise phase method'ları `false` döner (silent close — veri tutarsızlığı kullanıcıya "faz kapalı" olarak yansır).

## Term Phases (computed, state değil)
- IsRegistrationOpen: IsActive(now) + AcademicCalendar != null + (override ?? now ∈ [RegStart, RegEnd])
- IsAddDropOpen: IsActive(now) + AcademicCalendar != null + (override ?? now ∈ [AddDropStart, AddDropEnd])
- IsWithdrawalOpen: IsActive(now) + AcademicCalendar != null + (override ?? TermStart ≤ now ≤ WithdrawalDeadline)
- IsGradeEntryOpen: IsActive(now) + AcademicCalendar != null + (override ?? now ∈ [GradeEntryStart, GradeEntryEnd])
- Override null → takvim konuşur; true/false → admin override
- Cancelled dönemde override değiştirilemez.

## Admin Term API (Faz 1 revize + Faz 2)
- `GET /api/admin/terms` — tüm dönemler
- `GET /api/admin/terms/{id}` — tek dönem
- `POST /api/admin/terms` — yeni dönem (TimeSlot grid'i otomatik oluşturulur, AcademicCalendar null kalır)
- `PUT /api/admin/terms/{id}` — düzenleme (Cancelled dönem düzenlenemez)
- `POST /api/admin/terms/{id}/cancel` — iptal + section cascade
- `POST /api/admin/terms/{id}/uncancel` — iptali geri al
- Silinmiş endpoint'ler: `/activate`, `/deactivate`, `/transition`, `/override` (override Akademik Takvim API'ye taşındı)

## Academic Calendar API (Faz 2 — yeni)
Admin endpoints:
- `GET /api/admin/academic-calendar` — tüm dönemlerin takvim özet listesi (Auth: SystemAdministrator, AdministrativeStaff)
- `GET /api/admin/academic-calendar/{termId}` — belirli dönemin takvimi (404 if no calendar)
- `PUT /api/admin/academic-calendar/{termId}` — upsert (Auth: SystemAdministrator)
- `POST /api/admin/academic-calendar/{termId}/override` — faz override (Auth: SystemAdministrator)

Public endpoints (any authenticated):
- `GET /api/academic-calendar/active` — aktif dönemin öğrenci görünümü
- `GET /api/academic-calendar/{termId}` — belirli dönem öğrenci görünümü
- `GET /api/academic-calendar/terms` — dönem listesi (hasCalendar flag'iyle)

Cancelled dönem: upsert ve override `InvalidOperationException` fırlatır (400).

## ÖSYM + Kontenjan API (Faz 4 — stub CRUD)

`/api/admin/osym-registration-calendars` — systemAdmin only
- `GET` (query: year?)
- `POST` (CreateOsymRegistrationCalendarDto)
- `PUT /{id}` (UpdateOsymRegistrationCalendarDto)
- `DELETE /{id}`

`/api/admin/program-quotas` — systemAdmin + dean
- `GET` (query: programId?, year?)
- `POST` (CreateProgramQuotaDto — ProgramId dahil)
- `PUT /{id}` (UpdateProgramQuotaDto — ProgramId değiştirilemez)
- `DELETE /{id}`

Unique constraint: `ProgramQuota (ProgramId, Year, Type)` — aynı program×yıl×tip kombinasyonu tek kayıt.

**Operasyonel entegrasyon YOK.** Bu iki özellik sadece veri tabanı — öğrenci kayıt akışı, `CourseSection.Capacity`, veya başka bir kısıt mantığı ile bağlı değil. İleride entegrasyon eklenebilir.

## AcademicTerm Yeni Alanlar (Faz 1)
- `ShortEnglishName: string` — kısa İngilizce ad (zorunlu, default empty)
- `OtherName: string?` — diğer adı (opsiyonel)
- `YoksisStatus: YoksisStatus` — YÖKSİS raporlama durumu (Inactive/Active)

## Sidebar "Akademik İşlemler" grubu (Faz 4 güncel)
- Akademik Dönem Tanımları (`/donem-yonetimi` — TermManagementPage, form 11 alan: kimlik + TermStart/TermEnd)
- Fakülte Tanımları (`/fakulte-tanimlari` — FacultyDefinitionsPage) **Faz 3**
- Bölüm Tanımları (`/bolum-tanimlari` — DepartmentDefinitionsPage) **Faz 3**
- Program Tanımları (`/program-tanimlari` — ProgramDefinitionsPage) **Faz 3**
- Akademik Takvim Yönetimi (`/akademik-takvim-yonetimi` — AcademicCalendarAdminPage) **Faz 2**
- Akademik Takvim (Görünüm) (`/akademik-takvim` — AcademicCalendarPage, tabs: Liste + Takvim) **Faz 2**
- Takvim Oluşturucu (`/akademik-takvim-olusturma` — AcademicCalendarGeneratorPage)
- ÖSYM Kayıt Takvimi (`/osym-kayit-takvimi` — OsymRegistrationCalendarPage) **Faz 4 YENİ**
- Program Kontenjan Bilgileri (`/program-kontenjan` — ProgramQuotaPage) **Faz 4 YENİ**
- Sınıf Atlatma Kuralları (`/sinif-atlatma-kurallari` — PromotionRulesPage)

Dean grubunda akademik-islemler: sadece Program Kontenjan Bilgileri (dean endpoint'e erişiyor).

Eski gruplar ("Dönem Yönetimi", "Akademik Yapı", "Fakülte / Bölüm / Program" tek sayfa) silindi.

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

## Git Rules
- **NEVER add Co-Authored-By lines to commits.** No co-author trailers of any kind.
- Do not include `Co-Authored-By: Claude`, `Co-Authored-By: claude`, or any similar trailer in commit messages.

## Commands
```bash
dotnet build                          # Build solution
dotnet test                           # Run tests
dotnet ef database update -p SIS.Infrastructure -s SIS.API  # Apply migrations
```

