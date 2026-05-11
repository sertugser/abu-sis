# Student Information System (SIS)

Ankara Bilim University - Student Information System

## Kullanılan Teknolojiler

| Kategori | Teknolojiler |
|----------|-------------|
| **Backend** | ![.NET](https://img.shields.io/badge/.NET_10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white) ![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white) ![EF Core](https://img.shields.io/badge/EF_Core_10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white) |
| **Frontend** | ![React](https://img.shields.io/badge/React-20232A?style=for-the-badge&logo=react&logoColor=61DAFB) ![TypeScript](https://img.shields.io/badge/TypeScript-007ACC?style=for-the-badge&logo=typescript&logoColor=white) ![Vite](https://img.shields.io/badge/Vite-646CFF?style=for-the-badge&logo=vite&logoColor=white) ![Tailwind CSS](https://img.shields.io/badge/Tailwind_CSS-38B2AC?style=for-the-badge&logo=tailwind-css&logoColor=white) |
| **Veritabanı** | ![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=for-the-badge&logo=postgresql&logoColor=white) |
| **Auth** | ![JWT](https://img.shields.io/badge/JWT-000000?style=for-the-badge&logo=jsonwebtokens&logoColor=white) |

## Gereksinimler

| Araç | Versiyon |
|------|----------|
| .NET SDK | 10.0+ |
| Node.js | 18+ |
| PostgreSQL | 15+ |

## Kurulum

### 1. Repo'yu klonla

```bash
git clone <repo-url>
cd Student-Information-System
```

### 2. PostgreSQL veritabanını oluştur

```sql
CREATE DATABASE "StudentInfoSystem";
```

### 3. Backend ayarları

Development secret değerlerini repoya yazmayın. `SIS.API/appsettings.Development.example.json`
yalnızca güvenli örnek dosyadır; gerçek connection string ve JWT key için ASP.NET User Secrets
veya environment variable kullanın:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=StudentInfoSystem;Username=postgres;Password=<strong-password>" --project SIS.API
dotnet user-secrets set "Jwt:Key" "<at-least-32-byte-random-secret>" --project SIS.API
```

Environment variable alternatifi:

```bash
set ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=StudentInfoSystem;Username=postgres;Password=<strong-password>
set Jwt__Key=<at-least-32-byte-random-secret>
```

### 4. Veritabanı migration'larını uygula

```bash
dotnet ef database update -p SIS.Infrastructure -s SIS.API
```

### 5. Backend'i başlat

```bash
cd SIS.API
dotnet run
```

API varsayılan olarak `http://localhost:5213` adresinde çalışır.

### 6. Frontend bağımlılıklarını yükle ve başlat

```bash
cd SIS.Web
npm install
npm run dev
```

## Varsayılan Kullanıcılar (Development)

| Rol | E-posta | Şifre |
|-----|---------|-------|
| Admin | admin@ankarabilim.edu.tr | Admin123! |
| Öğretim Üyesi | ogretmen@ankarabilim.edu.tr | Ogretmen123! |
| Öğrenci | Seed data ile oluşturulur | Ogrenci123! |

## Proje Yapısı

```
SIS.Domain/           → Entities, Enums, ValueObjects (sıfır bağımlılık)
SIS.Application/      → Services, DTOs, Validators
SIS.Infrastructure/   → EF Core, DbContext, Repositories
SIS.API/              → Controllers, Middleware, DI
SIS.Web/              → React frontend (Vite + TypeScript + Tailwind)
SIS.Tests/            → Unit testler
scripts/              → Seed scriptleri
```

## Komutlar

```bash
dotnet build                          # Tüm çözümü derle
dotnet test                           # Testleri çalıştır
dotnet run --project SIS.API          # Backend'i başlat
cd SIS.Web && npm run dev             # Frontend'i başlat
```
