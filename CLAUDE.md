# Student Information System (SIS)

## Project Overview
Ankara Bilim University - Production Student Information System
13 modules, 139 functional requirements (SRS-based)

## Tech Stack
- **Backend:** ASP.NET Core 10 Web API (.NET 10 SDK, targeting net10.0)
- **Frontend:** React (not yet started)
- **Database:** PostgreSQL (localhost:5432)
- **ORM:** Entity Framework Core 10
- **Auth:** JWT Bearer Tokens + RBAC
- **Validation:** FluentValidation

## Architecture: Simplified Clean Architecture
```
SIS.Domain/           → Entities, Enums, ValueObjects, Exceptions (zero dependencies)
SIS.Application/      → Services, DTOs, Validators (depends on Domain)
SIS.Infrastructure/   → EF Core, DbContext, Repositories (depends on Domain + Application)
SIS.API/              → Controllers, Middleware, DI (depends on Application + Infrastructure)
SIS.Tests/            → Unit tests (depends on Application + Domain)
```
Dependency direction: API → Application → Domain ← Infrastructure

## Key Business Rules (Course Registration)
- ECTS caps: CGPA <2.00 → 30 ECTS, 2.00-2.49 → 42 ECTS, ≥2.50 → 48 ECTS
- CGPA < 1.70 (after 1st semester) → student is retained (sinifta kalir), cannot take upper-level courses
- FF/NA/U courses must be re-selected before new courses (FR-55)
- Full overlap for first-time courses → categorically blocked, no override
- Full/Partial overlap for repeated (FF) courses → allowed with advisor + dept manager approval
- Instructor cannot be assigned to overlapping sections (FR-36)
- Registration plan: soft hold on capacity, cleared at midnight if unconfirmed (FR-44)

## Code Conventions
- Domain entities: plain C# classes, no EF Core imports
- ApplicationUser (IdentityUser) lives in SIS.Infrastructure/Persistence/
- ScheduleSlot (value object) lives in SIS.Domain/ValueObjects/
- Composite PKs (CoursePrerequisite, CourseEquivalency, CurriculumCourse) configured in OnModelCreating
- Solution file format: .slnx (modern XML format from .NET 10 SDK)

## Database
- Host: localhost, Port: 5432, Database: StudentInfoSystem
- Username: postgres, Password: configured in appsettings.json
- YOKSİS integration: Mock/Simulation (no real API)

## User Roles
Student, Instructor, AcademicAdvisor, AdministrativeStaff, DepartmentManager, SystemAdministrator

## Commands
```bash
dotnet build                          # Build solution
dotnet test                           # Run tests
dotnet ef database update -p SIS.Infrastructure -s SIS.API  # Apply migrations
```
