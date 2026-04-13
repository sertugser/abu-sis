# Ders Degerlendirme Anketi + Ic Duyuru Sistemi — Tasarim Dokumani

**Tarih:** 2026-04-13
**Durum:** Onaylandi
**Modul:** Course Evaluation + Internal Announcements
**Atanan:** Tek kisi (iki bagimsiz alt modul)

---

## 1. Ozet

Iki bagimsiz ozellik, tek kisi tarafindan gelistirilecek:

1. **Ders Degerlendirme Anketi** — Donem sonunda ogrenci dersi/hocayi degerlendirir. Sonuclar yonetime raporlanir.
2. **Ic Duyuru Sistemi** — Admin/yonetici duyuru olusturur, kullanicilar dashboard'da ve ayri sayfada gorur.

Her iki modul tamamen yeni entity, controller, servis ve sayfa icerir. Mevcut dosyalara dokunma minimumdur.

---

## MODUL 1: Ders Degerlendirme Anketi

### 1.1 Amac

Donem sonunda ogrencilerin aldiklari dersleri ve ogretim uyelerini degerlendirmesi. Sonuclar bolum baskani ve admin tarafindan gorulebilir. Ogretim uyesi kendi derslerinin sonuclarini gorebilir (anonim).

### 1.2 Zamanlama

```
Donem lifecycle'inda yeri:
  ...AddDrop → WithdrawalPeriod → GradeEntry → [ANKET ACILIR] → Closed
  
Anket GradeEntry asamasinda acilir:
  → Notlar girilirken ogrenci degerlendirme yapar
  → Donem Closed oldugunda anket kapanir
  → Ogrenci notu gormeden ONCE degerlendirme yapmali
    (not aciklanmadan once doldurma motivasyonu)

Alternatif: Admin elle acip kapatabilir
```

### 1.3 Is Kurallari

```
- Ogrenci sadece o donem Active kaydinin oldugu dersleri degerlendirebilir
- Her ders icin bir kez doldurulur (guncelleme yok)
- Anket ANONIM: ogretim uyesi kimin ne yazdigini goremez
- Ogrenci notu gormeden once anketi doldurmali 
  (not yayinlanmadan onceki donemde acik)
- Minimum doluluk zorunlulugu yok (opsiyonel)
- Sonuclar yeterli katilim oldugunda gosterilir (min 5 ogrenci)
  → 5'ten az katilimda "Yetersiz katilim" gosterilir
  → Anonimligin korunmasi icin
```

### 1.4 Entity'ler

#### EvaluationPeriod

Degerlendirme donemini yonetir (acik/kapali).

| Alan | Tip | Aciklama |
|------|-----|----------|
| Id | int | PK |
| TermId | int | FK → AcademicTerm |
| OpenedAt | DateTime | Acilis zamani |
| ClosesAt | DateTime | Kapanis zamani |
| Status | EvaluationPeriodStatus | Open / Closed |
| OpenedBy | string? | Admin actiysa userId, otomatikse null |

**EvaluationPeriodStatus Enum:**
```
Open = 0
Closed = 1
```

#### EvaluationQuestion

Anket sorulari (admin tarafindan tanimlanir, donemler arasi tekrar kullanilir).

| Alan | Tip | Aciklama |
|------|-----|----------|
| Id | int | PK |
| Text | string | Soru metni (Turkce) |
| QuestionType | QuestionType | Rating / Text |
| Category | QuestionCategory | Course / Instructor |
| SortOrder | int | Siralama |
| IsActive | bool | Aktif mi (pasif sorular ankette gosterilmez) |

**QuestionType Enum:**
```
Rating = 0    // 1-5 arasi puan (yildiz)
Text = 1      // Serbest yorum
```

**QuestionCategory Enum:**
```
Course = 0       // Dersle ilgili ("Ders icerigi yeterliydi")
Instructor = 1   // Hocayla ilgili ("Ogretim uyesi etkili anlatti")
```

#### EvaluationResponse

Ogrencinin bir ders icin verdigi tum cevaplarin grubu.

| Alan | Tip | Aciklama |
|------|-----|----------|
| Id | int | PK |
| EvaluationPeriodId | int | FK → EvaluationPeriod |
| CourseSectionId | int | FK → CourseSection |
| StudentId | int | FK → Student (DB'de var ama API'de ASLA donmez) |
| SubmittedAt | DateTime | Gonderim zamani |

#### EvaluationAnswer

Tek bir soruya verilen cevap.

| Alan | Tip | Aciklama |
|------|-----|----------|
| Id | int | PK |
| ResponseId | int | FK → EvaluationResponse |
| QuestionId | int | FK → EvaluationQuestion |
| RatingValue | int? | 1-5 arasi puan (Rating tipi sorular icin) |
| TextValue | string? | Serbest yorum (Text tipi sorular icin) |

### 1.5 Anonimlik Guvencesi

```
KRITIK: Ogretim uyesi asla kimin ne yazdigini goremez.

Nasil saglanir:
  - EvaluationResponse'da StudentId var (tekrar doldurmayi engellemek icin)
  - AMA sonuc endpoint'leri ASLA StudentId dondurmez
  - Sonuclar sadece aggregated gosterilir:
    → Ortalama puan, dagilim (kac kisi 5 verdi, kac kisi 4 verdi...)
    → Serbest yorumlar listelenironu ama isimsiz
  - Min 5 katilim kuralioranin: 5'ten az doldurduysa hic gostermez
  - Ogretim uyesi kendi dersinin sonuclarini gorebilir (anonim aggregate)
  - Bolum baskani/admin tum dersleri gorebilir (anonim aggregate)
```

### 1.6 Varsayilan Sorular (Seed Data)

```
Ders Kategorisi (Course):
  1. [Rating] Ders icerigi ogrenme hedeflerine uygundu
  2. [Rating] Ders materyalleri (kitap, sunum, kaynak) yeterliydi
  3. [Rating] Ders is yuku (odev, proje, sinav) dengeliydi
  4. [Rating] Degerlendirme yontemleri (sinav, odev) adildi
  5. [Text]   Dersle ilgili eklemek istediginiz gorusler

Ogretim Uyesi Kategorisi (Instructor):
  6. [Rating] Ogretim uyesi konulari anlasilir bicimde anlatti
  7. [Rating] Ogretim uyesi dersi duzenli ve hazirlikli isledi
  8. [Rating] Ogretim uyesi sorulara ve geri bildirime acikti
  9. [Rating] Ogretim uyesi ogrencilere saygi ve esitlikle davrandio
  10. [Text]  Ogretim uyesiyle ilgili eklemek istediginiz gorusler
```

### 1.7 API Endpoint'leri

**CourseEvaluationController** (`/api/course-evaluation`)

| Method | Route | Yetki | Aciklama |
|--------|-------|-------|----------|
| GET | `/period/active` | Student | Aktif degerlendirme donemi bilgisi |
| GET | `/my-courses` | Student | Degerlendirilebilecek derslerim (doldurulmus/doldurulmamis) |
| GET | `/questions` | Student | Aktif anket sorulari |
| POST | `/submit` | Student | Degerlendirme gonder |
| GET | `/results/section/{sectionId}` | Instructor, DeptManager, Dean, Admin | Anonim sonuclar (aggregate) |
| GET | `/results/department/{deptId}` | DeptManager, Dean, Admin | Bolum geneli ozet |
| POST | `/period/open` | SystemAdmin | Degerlendirme donemini ac |
| POST | `/period/close` | SystemAdmin | Degerlendirme donemini kapat |
| GET | `/questions/all` | SystemAdmin | Tum sorular (aktif+pasif) |
| POST | `/questions` | SystemAdmin | Soru ekle |
| PUT | `/questions/{id}` | SystemAdmin | Soru guncelle |
| DELETE | `/questions/{id}` | SystemAdmin | Soru sil (pasif yap) |

### 1.8 DTO'lar

```
// Ogrenci icin - degerlendirme yapmak
EvaluableCourseDto:
  ├── CourseSectionId: int
  ├── CourseCode, CourseName: string
  ├── InstructorName: string
  ├── IsCompleted: bool (zaten doldurmus mu)
  └── SubmittedAt: DateTime? (doldurduysa ne zaman)

SubmitEvaluationDto:
  ├── CourseSectionId: int
  └── Answers: List<AnswerDto>
        ├── QuestionId: int
        ├── RatingValue: int? (1-5)
        └── TextValue: string?

// Sonuc goruntuleme - anonim aggregate
SectionEvaluationResultDto:
  ├── CourseCode, CourseName, InstructorName: string
  ├── TotalResponses: int
  ├── ResponseRate: decimal (katilim orani %)
  ├── IsVisible: bool (5+ katilim var mi)
  ├── QuestionResults: List<QuestionResultDto>
  │     ├── QuestionId, QuestionText: string
  │     ├── Category: QuestionCategory
  │     ├── QuestionType: QuestionType
  │     ├── AverageRating: decimal? (Rating sorulari icin)
  │     ├── RatingDistribution: Dictionary<int, int>? (1→15, 2→3, 3→8...)
  │     └── TextResponses: List<string>? (Text sorulari icin, isimsiz)
  └── OverallAverage: decimal (tum Rating sorularinin ortalamasi)

DepartmentEvaluationSummaryDto:
  ├── DepartmentName: string
  ├── TermName: string
  ├── TotalSections, EvaluatedSections: int
  ├── OverallResponseRate: decimal
  ├── AverageRating: decimal
  └── SectionSummaries: List<SectionSummaryDto>
        ├── CourseCode, CourseName, InstructorName
        ├── ResponseCount, ResponseRate
        ├── AverageRating
        └── IsVisible: bool
```

### 1.9 Frontend

**Ogrenci Sayfasi:** `CourseEvaluationPage` (`/ders-degerlendirme`)

```
┌─────────────────────────────────────────────────────────────┐
│  Ders Degerlendirme Anketi — 2025-2026 Bahar                │
│  Kalan sure: 5 gun                                          │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ CENG201 - Veri Yapilari           Dr. Yilmaz         │    │
│  │ ✓ Degerlendirildi (12.01.2026)               [Gor]  │    │
│  ├──────────────────────────────────────────────────────┤    │
│  │ MATH201 - Lineer Cebir           Prof. Ozkan         │    │
│  │ ○ Degerlendirilmedi                    [Degerlendir] │    │
│  ├──────────────────────────────────────────────────────┤    │
│  │ PHYS102 - Fizik II                Dr. Aksoy          │    │
│  │ ○ Degerlendirilmedi                    [Degerlendir] │    │
│  └──────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘

[Degerlendir] tiklaninca dialog acilir:

┌─────────────────────────────────────────────────────────────┐
│  MATH201 - Lineer Cebir  │  Prof. Ozkan                    │
│                                                              │
│  Ders Degerlendirmesi                                        │
│  ─────────────────────                                       │
│  Ders icerigi ogrenme hedeflerine uygundu                    │
│  ☆☆☆☆☆  →  ★★★★☆ (4/5)                                     │
│                                                              │
│  Ders materyalleri yeterliydi                                │
│  ☆☆☆☆☆  →  ★★★☆☆ (3/5)                                     │
│                                                              │
│  Dersle ilgili gorusleriniz (istege bagli):                  │
│  ┌──────────────────────────────────────────────────────┐    │
│  │                                                      │    │
│  └──────────────────────────────────────────────────────┘    │
│                                                              │
│  Ogretim Uyesi Degerlendirmesi                               │
│  ────────────────────────────                                │
│  Ogretim uyesi konulari anlasilir anlatti                     │
│  ☆☆☆☆☆  →  ★★★★★ (5/5)                                     │
│  ...                                                         │
│                                                              │
│  ⓘ Bu degerlendirme tamamen anonimdir.                       │
│    Ogretim uyesi kimligizinizi goremez.                       │
│                                                              │
│  [Iptal]                                        [Gonder]     │
└─────────────────────────────────────────────────────────────┘
```

**Sonuc Sayfasi:** `EvaluationResultsPage` (`/degerlendirme-sonuclari`)
- Ogretim uyesi: kendi derslerinin anonim sonuclari
- Bolum baskani: bolum geneli ozet + ders bazli detay
- Admin: tum bolumler

```
┌─────────────────────────────────────────────────────────────┐
│  Degerlendirme Sonuclari — 2025-2026 Bahar                  │
│                                                              │
│  Bolum Ozeti: Bilgisayar Muhendisligi                        │
│  Katilim: %68 (245/360)  │  Genel Ortalama: 3.8/5           │
│                                                              │
│  ┌─────────────┬───────────┬────────┬──────────┬──────────┐  │
│  │ Ders        │ Ogr. Uyesi│Katilim │ Ort Puan │ Detay    │  │
│  ├─────────────┼───────────┼────────┼──────────┼──────────┤  │
│  │ CENG201     │ Dr.Yilmaz │ 28/35  │ 4.2/5    │ [Gor]    │  │
│  │ CENG301     │ Prof.Ak   │ 22/30  │ 3.5/5    │ [Gor]    │  │
│  │ CENG101     │ Dr.Demir  │ 3/45   │ —        │ Yetersiz │  │
│  └─────────────┴───────────┴────────┴──────────┴──────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## MODUL 2: Ic Duyuru Sistemi

### 2.1 Amac

Admin ve yoneticilerin sistem ici duyuru olusturmasi. Mevcut AnnouncementController harici web scrape yapiyor, o korunacak. Bu tamamen ayri, ic duyuru sistemi.

### 2.2 Is Kurallari

```
- Admin, Dean, DeptManager duyuru olusturabilir
- Duyuru hedef kitlesi secilir: Herkes / Ogrenciler / Ogretim Uyeleri / Belirli bolum
- Duyuru tarih aralikli olabilir (baslangic - bitis, bitis opsiyonel)
- Bitis tarihi gecmis duyurular otomatik gosterilmez
- Duyurular dashboard'larda card olarak ve ayri sayfada liste olarak gosterilir
- Sabitleme (pin): onemli duyurular ustte sabitlenebilir
- Duyuru duzenlenebilir, silinebilir
```

### 2.3 Entity

#### InternalAnnouncement

| Alan | Tip | Aciklama |
|------|-----|----------|
| Id | int | PK |
| Title | string | Duyuru basligi |
| Content | string | Duyuru icerigi (duz metin) |
| TargetAudience | AnnouncementAudience | Hedef kitle |
| TargetDepartmentId | int? | Belirli bolum icin (nullable) |
| IsPinned | bool | Ustte sabitlenmis mi |
| StartsAt | DateTime | Gosterim baslangici |
| ExpiresAt | DateTime? | Gosterim bitisi (null = suresiz) |
| CreatedBy | string | Olusturanin userId'si |
| CreatedByName | string | Olusturanin adi (gosterim icin) |
| CreatedAt | DateTime | Olusturulma zamani |
| UpdatedAt | DateTime? | Son guncelleme |
| IsActive | bool | Aktif mi (soft delete) |

**AnnouncementAudience Enum:**
```
All = 0              // Herkes
Students = 1         // Sadece ogrenciler
Instructors = 2      // Sadece ogretim uyeleri
Department = 3       // Belirli bolum (TargetDepartmentId ile)
Management = 4       // Yonetim kadrosu
```

### 2.4 API Endpoint'leri

**InternalAnnouncementController** (`/api/announcements`)

| Method | Route | Yetki | Aciklama |
|--------|-------|-------|----------|
| GET | `/active` | Authenticated | Kullanicinin gorebilecegi aktif duyurular |
| GET | `/{id}` | Authenticated | Tek duyuru detayi |
| POST | `/` | Admin, Dean, DeptManager | Yeni duyuru olustur |
| PUT | `/{id}` | Admin, Dean, DeptManager | Duyuru guncelle |
| DELETE | `/{id}` | Admin, Dean, DeptManager | Duyuru sil (soft delete) |
| GET | `/all` | Admin | Tum duyurular (aktif+pasif, yonetim icin) |

**GET /active filtre mantigi:**
```
1. IsActive = true
2. StartsAt <= now
3. ExpiresAt == null VEYA ExpiresAt > now
4. Hedef kitle kontrolu:
   - All → herkes gorur
   - Students → kullanicinin Student rolu varsa
   - Instructors → kullanicinin Instructor rolu varsa
   - Department → kullanicinin bolumu eslesiyor
   - Management → Dean, DeptManager, Admin rolleri
5. Siralama: IsPinned desc, CreatedAt desc
```

### 2.5 DTO'lar

```
CreateAnnouncementDto:
  ├── Title: string (max 200 karakter)
  ├── Content: string (max 5000 karakter)
  ├── TargetAudience: AnnouncementAudience
  ├── TargetDepartmentId: int? (audience=Department ise zorunlu)
  ├── IsPinned: bool
  ├── StartsAt: DateTime (varsayilan: simdi)
  └── ExpiresAt: DateTime? (null = suresiz)

AnnouncementListDto:
  ├── Id: int
  ├── Title: string
  ├── Content: string (ilk 200 karakter preview)
  ├── TargetAudience: string
  ├── IsPinned: bool
  ├── StartsAt: DateTime
  ├── ExpiresAt: DateTime?
  ├── CreatedByName: string
  └── CreatedAt: DateTime

AnnouncementDetailDto:
  ├── (ListDto'nun tum alanlari)
  └── Content: string (tam icerik)
```

### 2.6 Frontend

**Duyuru Yonetim Sayfasi:** `AnnouncementManagementPage` (`/duyuru-yonetimi`)
- Admin/Dean/DeptManager erisebilir
- Duyuru listesi + olusturma + duzenleme + silme

```
┌─────────────────────────────────────────────────────────────┐
│  Duyuru Yonetimi                           [+ Yeni Duyuru]  │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ 📌 Bahar Donemi Kayit Tarihleri Degisikligi          │    │
│  │ Herkes │ 10.02.2026 - 28.02.2026 │ Admin Mehmet      │    │
│  │                                    [Duzenle] [Sil]   │    │
│  ├──────────────────────────────────────────────────────┤    │
│  │ Kutuphane Calisma Saatleri                            │    │
│  │ Ogrenciler │ 05.02.2026 - suresiz │ Admin Mehmet     │    │
│  │                                    [Duzenle] [Sil]   │    │
│  └──────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

**Duyuru Goruntuleme Sayfasi:** `AnnouncementsPage` (`/duyurular`)
- Tum roller erisebilir
- Kullanicinin gorebilecegi aktif duyurular listelenir

**Dashboard Entegrasyonu:**
- Her dashboard'a (Student, Instructor, Management, Admin) duyuru card'i eklenir
- Son 3 aktif duyuru gosterilir
- "Tum Duyurular" linki ile /duyurular sayfasina gider

```
Dashboard'daki duyuru card'i:
┌──────────────────────────────────────────┐
│ 📢 Duyurular                    [Tumu →]│
│                                          │
│ 📌 Kayit Tarihleri Degisikligi           │
│    10 Subat 2026                         │
│                                          │
│ Kutuphane Calisma Saatleri               │
│    5 Subat 2026                          │
│                                          │
│ Yaz Okulu Basvurulari Acildi             │
│    1 Subat 2026                          │
└──────────────────────────────────────────┘
```

---

## 3. Dosya Yapisi

### Backend

```
SIS.Domain/
  Entities/
    EvaluationPeriod.cs
    EvaluationQuestion.cs
    EvaluationResponse.cs
    EvaluationAnswer.cs
    InternalAnnouncement.cs
  Enums/
    EvaluationPeriodStatus.cs
    QuestionType.cs
    QuestionCategory.cs
    AnnouncementAudience.cs

SIS.Application/
  CourseEvaluation/
    DTOs/
      EvaluableCourseDto.cs
      SubmitEvaluationDto.cs
      SectionEvaluationResultDto.cs
      DepartmentEvaluationSummaryDto.cs
      EvaluationQuestionDto.cs
    Services/
      ICourseEvaluationService.cs
  
  Announcement/
    DTOs/
      CreateAnnouncementDto.cs
      AnnouncementListDto.cs
      AnnouncementDetailDto.cs
    Services/
      IAnnouncementService.cs

SIS.Infrastructure/
  Services/
    CourseEvaluation/
      CourseEvaluationService.cs
    Announcement/
      AnnouncementService.cs
  Persistence/
    SeedData.EvaluationQuestions.cs

SIS.API/
  Controllers/
    CourseEvaluationController.cs
    InternalAnnouncementController.cs
```

### Frontend

```
SIS.Web/src/
  pages/
    CourseEvaluationPage.tsx          ← Ogrenci anket doldurma
    EvaluationResultsPage.tsx        ← Sonuc goruntuleme (hoca/yonetim)
    AnnouncementManagementPage.tsx   ← Duyuru CRUD (admin)
    AnnouncementsPage.tsx            ← Duyuru listeleme (herkes)
  components/
    course-evaluation/
      EvaluationDialog.tsx           ← Anket doldurma dialog
      StarRating.tsx                 ← 1-5 yildiz bileşeni
      QuestionCard.tsx               ← Tek soru goruntuleme
      ResultChart.tsx                ← Sonuc dagilim grafigi
    announcements/
      AnnouncementCard.tsx           ← Duyuru card (dashboard + liste)
      AnnouncementFormDialog.tsx     ← Duyuru olusturma/duzenleme formu
      DashboardAnnouncementWidget.tsx ← Dashboard'a eklenen widget
  hooks/
    useCourseEvaluation.ts
    useAnnouncements.ts
  api/
    courseEvaluation.api.ts
    announcements.api.ts
  types/
    courseEvaluation.ts
    announcements.ts
```

---

## 4. Mevcut Sistemle Entegrasyon

### 4.1 Dokunulmayacak Dosyalar

```
- AnnouncementController.cs (mevcut harici scrape — AYNEN kalir)
- LoginPage.tsx (mevcut harici duyurular — AYNEN kalir)
- Scheduling motoru, kayit sistemi, not sistemi — HIC dokunulmaz
- Mevcut tum servisler — HIC dokunulmaz
```

### 4.2 Degistirilecek Dosyalar

| Dosya | Degisiklik | Risk |
|-------|-----------|------|
| AppDbContext.cs | 5 yeni entity icin DbSet eklenir | DUSUK — yeni satirlar |
| App.tsx | 4 yeni route eklenir | DUSUK — yeni satirlar |
| sidebarMenuGroups.ts | 2 yeni link eklenir (degerlendirme + duyurular) | DUSUK |
| DashboardPage.tsx | DashboardAnnouncementWidget eklenir | DUSUK — 1 component import |
| InstructorDashboardPage.tsx | DashboardAnnouncementWidget eklenir | DUSUK — 1 component import |
| ManagementDashboardPage.tsx | DashboardAnnouncementWidget eklenir | DUSUK — 1 component import |
| SystemAdminDashboardPage.tsx | DashboardAnnouncementWidget eklenir | DUSUK — 1 component import |
| Program.cs | 2 yeni servis DI kaydi | DUSUK |

### 4.3 Cakisma Riski (Diger Gorevlerle)

```
Akademik Takvim gorevi ile: SIFIR — farkli entity, controller, sayfa
Akilli Ders Onerisi ile: SIFIR — farkli entity, controller, sayfa
Excel Import ile: SIFIR — farkli controller, farkli sayfa
PDF gorevi ile: SIFIR — farkli controller, farkli sayfa
Admin sayfalari (senin isin) ile: Dashboard'lara widget ekleme
  → Kucuk risk: dashboard sayfalarinda ayni satira ekleme olabilir
  → Cozum: widget'i ayri component olarak yaz, dashboard'a tek satirla ekle
```

---

## 5. Seed Data

### Degerlendirme Sorulari (10 soru)

```
Ders Kategorisi:
  1. [Rating] "Dersin icerigi, ogrenme hedeflerine uygundu"
  2. [Rating] "Ders materyalleri (kitap, sunum, kaynak) yeterli ve gunceldi"
  3. [Rating] "Ders is yuku (odev, proje, sinav) dengeliydi"
  4. [Rating] "Degerlendirme yontemleri (sinav, odev) adil ve seffafti"
  5. [Text]   "Ders hakkinda eklemek istediginiz gorusleriniz"

Ogretim Uyesi Kategorisi:
  6. [Rating] "Ogretim uyesi konulari anlasilir ve etkili bicimde isledi"
  7. [Rating] "Ogretim uyesi derslere hazirlikli ve duzenli geldi"
  8. [Rating] "Ogretim uyesi soru ve geri bildirimlere acikti"
  9. [Rating] "Ogretim uyesi ogrencilere saygi ve esitlikle yaklasti"
  10. [Text]  "Ogretim uyesi hakkinda eklemek istediginiz gorusleriniz"
```

---

## 6. Migration

Tek migration ile 5 yeni tablo:
- EvaluationPeriods
- EvaluationQuestions
- EvaluationResponses
- EvaluationAnswers
- InternalAnnouncements

Mevcut tablolara degisiklik yok.

---

## 7. Kapsam Disi

- Bildirim sistemi (event-driven, kisisel bildirimler) — ayri is
- E-posta ile duyuru gonderimi — e-posta servisi yok
- Duyuru ekleri (dosya yukleme) — dosya sistemi yok
- Degerlendirme sonuclarina gore otomatik aksiyon (uyari, tesvikoku) — manuel inceleme yeterli
- Rich text editor (duyuru icerigi duz metin)
- Push notification — altyapi yok
- Mevcut AnnouncementController degisikligi — harici scrape AYNEN kalir
