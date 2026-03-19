# Student Information System (SIS)

Ankara Bilim University - Student Information System

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

`SIS.API/appsettings.Development.json.example` dosyasını kopyalayıp kendi bilgilerinle düzenle:

```bash
cp SIS.API/appsettings.Development.json.example SIS.API/appsettings.Development.json
```

Dosyada PostgreSQL şifreni ve JWT key'ini güncelle.

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
