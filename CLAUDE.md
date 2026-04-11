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

## Term Lifecycle
Draft → Planning → Scheduling → Published → RegistrationOpen → AddDrop → WithdrawalPeriod → GradeEntry → Closed → Archived

- Tüm geçişler admin tarafından manuel yapılabilir (guard koşulları dahilinde)
- Published → GradeEntry arası geçişler ayrıca takvim tarihlerine göre otomatik tetiklenir
- Geri dönüş izinleri: Planning→Draft, Scheduling→Planning, Published→Scheduling

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

