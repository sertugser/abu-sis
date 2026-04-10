# SIS Refactoring & Stabilizasyon Plani

**Toplam Madde:** 88

---

## Takım Dağılımı

| Kisi | Alan | Faz 1 | Faz 2 | Faz 3 | Toplam |
|------|------|-------|-------|-------|--------|
| **A(SFG)** | Ders Acma + Ders Onay | 10 | 7 | — | 17 madde |
| **B (SS)** | Ders Programi + Derslik + Scheduling Refactoring +  | 11 | 8 | 13 | 32 madde |
| **C(EE)** | Donem Kapatma + Sinif Listeleri + Akademik Islemler | 20 | 3 | — | 23 madde |
| **D(HBE)** | Sistem Ayarlari + Denetim Kayitlari + Altyapi | 16 | 1 | — | 17 madde |

---

## Bagimlilik Kurallari

- Kisi B, Faz 2C'ye baslamadan once Kisi D'nin cache invalidation (D1) fix'ini bitirmis olmasi lazim
- Kisi A, Faz 2A-2B'ye baslamadan once kendi Faz 1 fix'lerini bitirmis olmasi lazim
- Faz 3, Faz 2C tamamen bitmeden baslamamali
- **Faz 3'te bagimlilk YOK** — tamamı Kisi B'de, kimse kimseyi beklemiyor

---

# FAZ 1 - Stabilizasyon

> Amac: Mevcut sistemi stabil, tutarli, guvenilir hale getir.
> Yapisal degisiklik yok, bug fix + UX iyilestirme.

## Kisi A - Ders Acma + Ders Onay (Faz 1)

| # | Sorun | Dosya | Aciklama | Oncelik |
|---|-------|-------|----------|---------|
| A1 | UpdateSection hoca validasyonu yok | `CourseOpeningService.cs` | OpenCourse'daki bolum kontrolu UpdateSection'a da eklenmeli | Kritik |
| A2 | BulkOpenCourses transaction yok | `CourseOpeningService.cs` | `BeginTransactionAsync` ile sarilmali, ya hep ya hic | Kritik |
| A3 | Kapasite azaltmada RegisteredCount yok | `CourseOpeningService.cs` | `Capacity < RegisteredCount` kontrolu eklenmeli | Kritik |
| A4 | Section kapatilinca access policy temizlenmiyor | `CourseOpeningService.cs` | Tum section'lar kapaninca `CourseAccessPolicy` silinmeli | Orta |
| A5 | Section kapatma dialogunda ogrenci bilgisi yok | `CourseOpeningPage.tsx` | Kac Draft/Pending kayit dusurulecek gosterilmeli | Orta |
| A6 | Toplu acmada hoca atanamıyor | `CourseOpeningPage.tsx` (BulkOpenDialog) | Her derse dropdown ile hoca secimi eklenmeli | Orta |
| A7 | Toplu acmada tarihsel veri yok | `CourseOpeningPage.tsx` (BulkOpenDialog) | Talep sutununa tooltip ile gecmis donem verisi | Dusuk |
| A8 | Sayfa 1022 satir tek dosya | `CourseOpeningPage.tsx` | `AddCourseDialog`, `BulkOpenDialog`, `SemesterSectionGroup`, `WorkloadBar` ayri dosyalara | Orta |
| A9 | Forecast gorunumu tekrar eden kod | `CourseOpeningPage.tsx` | Ortak `<ForecastSummaryPanel>` componenti cikarilmali | Dusuk |
| A10 | Durum badge'i sadece ilk section | `CourseOpeningPage.tsx` | Ders grubu basligina tasinmali | Dusuk |

## Kisi B (Sen) - Ders Programi + Derslik Doluluk (Faz 1)

| # | Sorun | Dosya | Aciklama | Oncelik |
|---|-------|-------|----------|---------|
| B1 | Doluluk orani hesaplama tutarsizligi | `SchedulingService.cs` | Summary card ve heatmap ayni metrik kullanmali | Orta |
| B2 | Manuel atamada cakisma on kontrolu yok | `FacultyPlanningPage.tsx` | Submit oncesi validate API cagrisi | Kritik |
| B3 | Derslik secimi ham ID girisi | `FacultyPlanningPage.tsx` | Dropdown/autocomplete ile derslik secimi | Kritik |
| B4 | Zaman slotu derslik musaitligini kontrol etmiyor | `FacultyPlanningPage.tsx` | Secilen dersligin o saatte dolu olup olmadigini goster | Kritik |
| B5 | Auto-schedule geri alma yok | `FacultyPlanningPage.tsx` + backend | Batch undo endpoint + "Geri Al" butonu | Orta |
| B6 | Instructor title dropdown'da yok | `FacultyPlanningPage.tsx` | Hoca adinin yanina unvan eklenmeli | Dusuk |
| B7 | Cakisma cozumune link yok | `ClassroomManagementPage.tsx` | Conflict item'inda "Programlamaya Git" butonu | Orta |
| B8 | Heatmap drill-down yok | `ClassroomManagementPage.tsx` | Hucreye tiklayinca detay listesi acilmali | Orta |
| B9 | Saat araligi sabit 8-17 | `ClassroomManagementPage.tsx` | Config'den okunmali | Dusuk |
| B10 | Ogretim uyesi filtresi yok | `ClassroomManagementPage.tsx` | Sol panele hoca bazli filtre eklenmeli | Orta |
| B11 | Gecmis donem karsilastirmasi yok | `ClassroomManagementPage.tsx` | Onceki donem doluluk orani overlay/badge | Dusuk |

## Kisi C - Donem Kapatma + Sinif Listeleri (Faz 1)

| # | Sorun | Dosya | Aciklama | Oncelik |
|---|-------|-------|----------|---------|
| C1 | Donem kapatma transaction yok | `TermClosingService.cs` | Her adim `BeginTransactionAsync` ile sarilmali | Kritik |
| C2 | Not girisi kontrolsuz kilitleme | `TermClosingService.cs` | Eksik not orani gosterilmeli, onay istenmeli | Kritik |
| C3 | Adim sirasi backend'de zorlanmiyor | `TermClosingService.cs` | Onceki adim complete mi kontrolu | Kritik |
| C4 | Tarih validasyonu yok | `TermClosingService.cs` | Start < End, future date kontrolu | Kritik |
| C5 | Grading criteria bos olusturma bug | `TermClosingService.cs` | `criteria ??= new()` yerine exception firlat | Kritik |
| C6 | Snapshot yaris durumu | `TermClosingService.cs` | Snapshot promotion'dan ONCE olusturulmali | Kritik |
| C7 | SGPA stale kalabilir | `AcademicProgressionService.cs` | latestTermId yoksa SGPA sifirlanmali veya recalculate | Orta |
| C8 | Coklu sinif atlama tek +1 | `AcademicProgressionService.cs` | newLevel - oldLevel farki kadar artirilmali | Orta |
| C9 | Promotion rules opsiyonel uyarisiz | `AcademicProgressionService.cs` | Kural yoksa fallback kullanildigini logla + UI'da uyar | Orta |
| C10 | Concurrent kullanim korumasi yok | `TermClosingService.cs` | Pessimistic lock veya "in-progress" flag | Orta |
| C11 | Sinif listeleri bolum izolasyonu zayif | `AcademicOperationsService.cs` | Sorgu sikilastirilmali | Kritik |
| C12 | EnrolledCount/ActiveStudentCount tutarsizligi | `AcademicOperationsService.cs` | Kullanilmayan alan kaldirilmali veya hesaplanmali | Dusuk |
| C13 | Arama placeholder yaniltici | `AcademicOperationsPage.tsx` | "Ders veya derslik..." veya hoca aramasi eklenmeli | Dusuk |
| C14 | Sinif listeleri siralama/filtreleme yok | `AcademicOperationsPage.tsx` | Sutun basligina tikla -> sirala, filtre dropdown'lari | Orta |
| C15 | Ogrenci adina gore arama yok | `AcademicOperationsPage.tsx` | Expanded section icinde arama input'u | Dusuk |
| C16 | Bos section badge highlight etmiyor | `AcademicOperationsPage.tsx` | Amber background ekle | Dusuk |
| C17 | Donem kapatma onay dialoglari yok | `TermClosingWizard.tsx` | Her adimda "Emin misiniz?" + etki ozeti | Orta |
| C18 | Ilerleme gostergesi sahte | `TermClosingWizard.tsx` | Server-sent events veya polling ile gercek ilerleme | Dusuk |
| C19 | Sinif listeleri export yok | Backend + `AcademicOperationsPage.tsx` | CSV/Excel endpoint + indirme butonu | Orta |
| C20 | Toplu e-posta/contact list yok | `AcademicOperationsPage.tsx` | mailto link veya toplu liste kopyalama | Dusuk |

## Kisi D - Sistem Ayarlari + Denetim Kayitlari (Faz 1)

| # | Sorun | Dosya | Aciklama | Oncelik |
|---|-------|-------|----------|---------|
| D1 | SystemConfig cache invalidation yok | `SystemConfigProvider.cs` | Update sonrasi `IMemoryCache.Remove()` eklenmeli | Kritik |
| D2 | SystemConfig input validation yok | `SystemConfigController.cs` | DataType'a gore tip kontrolu | Kritik |
| D3 | SystemConfig aralik/sinir validasyonu yok | `SystemConfigController.cs` | Her key icin min/max tanimi | Kritik |
| D4 | Bool parsing sinirli | `SystemConfigProvider.cs` | "1", "yes", "on" da kabul edilmeli | Dusuk |
| D5 | Decimal locale karisikligi | `SystemConfigPage.tsx` | UI'da uyari veya otomatik donusum | Dusuk |
| D6 | Config degisikligi audit loglanmiyor | `SystemConfigController.cs` | Eski -> yeni deger kaydedilmeli | Kritik |
| D7 | Frontend-backend rol uyumsuzlugu | `App.tsx` + `SystemConfigController.cs` | Route guard sadece SystemAdministrator | Orta |
| D8 | Config rollback/gecmis yok | `SystemConfigPage.tsx` | Son 5 degisikligi gosterme + "Geri Al" butonu | Orta |
| D9 | Kullanici yonetimi loglanmiyor | `AuditInterceptor.cs` | `ApplicationUser` audited entity listesine eklenmeli | Kritik |
| D10 | Basarili giris loglanmiyor | `AuthController.cs` | Login success sonrasi audit kaydi | Orta |
| D11 | SchedulingAuditLog ayri tablo | `SchedulingService.cs` + `AuditInterceptor.cs` | Ana audit sistemine entegre edilmeli | Orta |
| D12 | Retention policy yok | Yeni: `AuditRetentionService.cs` | Configurable sure sonrasi arsivle/sil | Dusuk |
| D13 | Tamper protection yok | DB migration | Append-only constraint veya trigger | Dusuk |
| D14 | Derslik doluluk export | Backend + `ClassroomManagementPage.tsx` | CSV/Excel endpoint + indirme butonu | Orta |
| D15 | Denetim kayitlari export | Backend + `AuditLogPage.tsx` | CSV endpoint + indirme butonu | Orta |
| D16 | Config import/export | `SystemConfigController.cs` + frontend | JSON export/import | Dusuk |

---

# FAZ 2 - Mimari Birlestirme

> Amac: Sayfalar arasi kopukluklari gider, cift algoritmayi birlestir, SchedulingService'i parcala.
> Faz 1 tamamlandiktan sonra baslanir.

## Kisi A - Forecast Birlestirme + Onay-Acma Entegrasyonu (Faz 2)

| # | Sorun | Degisecek Dosyalar | Aciklama |
|---|-------|-------------------|----------|
| A11 | Iki ayri forecast mekanizmasi | `CourseOpeningService.cs`, `EnrollmentForecastService.cs` | Inline hesaplama kaldirilir, `EnrollmentForecastService` tek kaynak olur |
| A12 | Cross-dept tahmini %20 sabit | `EnrollmentForecastService.cs` | Curriculum mapping'den gercek ogrenci sayisi hesaplanir |
| A13 | Mevsimsellik eksik | `EnrollmentForecastService.cs` | Guz/bahar ayrimi, yillik patern tespiti |
| A14 | ECTS simulasyonu sirali | `EnrollmentForecastService.cs` | Ogrenci davranis modeli iyilestirilmeli |
| A15 | Onay kararlari forecast'e geri bildirim | `EnrollmentForecastService.cs` | Yonetici 3->5 section onayladiysa gelecek tahmine yansisin |
| A16 | Ders acma onay tablosunu kontrol etmiyor | `CourseOpeningService.cs` | Onaylanmamis ders acmaya calisinca uyari/engel |
| A17 | Onay sayfasi acilan dersleri gormuyor | `AcademicOperationsService.cs` + frontend | Acilmis dersler "Tamamlandi" statusune gecsin |

## Kisi B (Sen) - SchedulingService Refactoring (Faz 2)

| # | Sorun | Yeni/Degisecek Dosyalar | Aciklama |
|---|-------|------------------------|----------|
| B12 | 3700 satir monolitik servis | 15+ yeni evaluator dosyasi | Her constraint ayri `IConstraintEvaluator` sinifina |
| B13 | Ayni mantik 3 yerde tekrarlaniyor | `ConstraintEvaluationService.cs` | Tek orkestrator, evaluator'lari cagirir |
| B14 | FacultyPlanning transaction kirik | `FacultyPlanningService.cs` | Tek transaction ile sarilmali |
| B15 | ApplySchedule transaction yok | `FacultyPlanningService.cs` | Atomic batch uygulama |
| B16 | Derslik tipi sadece uyari | `ClassroomTypeEvaluator.cs` | Hard evaluator olarak dogru seviyede |
| B17 | Kapasite asimi sadece uyari | `CapacityEvaluator.cs` | Hard evaluator olarak dogru seviyede |
| B18 | Curriculum conflict sadece zorunlu | `CurriculumConflictEvaluator.cs` | Secmelileri de kapsayacak sekilde genislet |
| B19 | Soft/Hard tutarsizligi | Evaluator siniflandirmasi | Her evaluator kendi category'sini bilir |

## Kisi C - Ek Iyilestirmeler (Faz 2)

| # | Sorun | Dosya | Aciklama |
|---|-------|-------|----------|
| C21 | Hoca workload view yok | `FacultyPlanningPage.tsx` + backend | Hoca bazli haftalik yuk ozet paneli |
| C22 | Schedule copy/clone yok | Backend + frontend | Onceki donem programini sablona cevirme |
| C23 | Bulk unassign yok | Backend + frontend | Bolum/hoca bazli toplu kaldirma |

## Kisi D - Kisi B'ye Destek (Faz 2)

| # | Is | Aciklama |
|---|---|----------|
| D17 | Evaluator unit test'leri | Her evaluator icin ayri test sinifi yaz |

---

# FAZ 3 - Constraint Yonetim Sistemi (Tamami Kisi B)

> Amac: Admin UI'dan constraint ac/kapat, parametre degistir, per-entity istisna tanimla.
> Faz 2C tamamen bittikten sonra baslanir.
> **Sifir bagimlilk** — tamamı Kisi B'de, diger kisiler code review + test yapar.

## Kisi B (Sen) - Domain + DB

| # | Is | Aciklama |
|---|---|----------|
| B20 | `ConstraintDefinition` entity | Code, Name, Category, IsEnabled, Parameters (JSON), Scope |
| B21 | `ConstraintException` entity | ConstraintId, InstructorId?, StudentId?, DepartmentId?, CourseSectionId?, IsExempt, Reason |
| B22 | Migration | Iki tablo + index'ler |
| B23 | Seed data | Mevcut 21 constraint'i tabloya aktar |

## Kisi B (Sen) - Backend

| # | Is | Aciklama |
|---|---|----------|
| B24 | `ConstraintController.cs` | List, Toggle, UpdateParams, AddException, RemoveException |
| B25 | `ConstraintEvaluationService`'e istisna kontrolu ekle | Evaluate oncesi exception tablosunu kontrol et |
| B26 | Hard <-> Soft gecis mekanizmasi | Admin category degistirince evaluator davranisi degissin |
| B27 | Per-entity parametre override | Exception tablosunda alternatif parametre JSON'i |
| B28 | Constraint evaluation preview | Dry-run endpoint: "Bu atamayi yapsam ne olur?" |

## Kisi B (Sen) - Frontend

| # | Is | Aciklama |
|---|---|----------|
| B29 | Constraint yonetim sayfasi | Constraint listesi, toggle, parametre duzenleme |
| B30 | Istisna yonetimi formu | Hoca/bolum/section secici, istisna ekleme/kaldirma |
| B31 | Constraint preview paneli | Ders programi sayfasinda atama oncesi simulasyon |
| B32 | SystemConfig'den scheduling tasima | Eski key-value'lar yeni UI'a migrate edilmeli |

## Kisi A, C, D - Faz 3 Sirasinda

Diger kisiler Faz 3 surecinde:
- Kisi B'nin PR'larini code review eder
- Integration test senaryolari yazar ve calistirir
- Kendi alanlarindaki dokumantasyonu tamamlar
- Faz 1-2'den kalan polish isleri varsa bitirir

---

# Kisi Bazli Ozet

## Kisi A (Ders Acma + Ders Onay)
- **Faz 1:** A1-A10 (10 madde)
- **Faz 2:** A11-A17 (7 madde)
- **Faz 3:** Code review + test
- **Toplam: 17 madde**

## Kisi B - SEN (Ders Programi + Derslik + Scheduling + TUM FAZ 3)
- **Faz 1:** B1-B11 (11 madde)
- **Faz 2:** B12-B19 (8 madde) — SchedulingService refactoring
- **Faz 3:** B20-B32 (13 madde) — Entity + migration + API + UI + test (tek basina)
- **Toplam: 32 madde** (en agir yuk, sifir bagimlilk)

## Kisi C (Donem Kapatma + Sinif Listeleri)
- **Faz 1:** C1-C20 (20 madde)
- **Faz 2:** C21-C23 (3 madde)
- **Faz 3:** Code review + test
- **Toplam: 23 madde**

## Kisi D (Sistem Ayarlari + Denetim Kayitlari + Altyapi)
- **Faz 1:** D1-D16 (16 madde)
- **Faz 2:** D17 (1 madde - test yazma)
- **Faz 3:** Code review + test
- **Toplam: 17 madde**

---

# Hedef Mimari (Faz 2-3 Sonrasi)

```
SIS.Domain/
├── Entities/
│   ├── ConstraintDefinition.cs       <- YENi
│   └── ConstraintException.cs        <- YENi
│
├── Interfaces/
│   └── IConstraintEvaluator.cs       <- YENi

SIS.Infrastructure/
├── Services/
│   ├── Scheduling/
│   │   ├── SchedulingService.cs            <- ~800 satira duser
│   │   ├── ConstraintEvaluationService.cs  <- YENi orkestrator
│   │   │
│   │   ├── Evaluators/Hard/               <- YENi
│   │   │   ├── ClassroomConflictEvaluator.cs        (H1)
│   │   │   ├── InstructorConflictEvaluator.cs       (H2)
│   │   │   ├── CapacityEvaluator.cs                 (H3)
│   │   │   ├── ClassroomTypeEvaluator.cs            (H4)
│   │   │   ├── CurriculumConflictEvaluator.cs       (H5)
│   │   │   ├── SameCourseComponentEvaluator.cs      (H6)
│   │   │   ├── SameCourseSectionEvaluator.cs        (H8)
│   │   │   ├── FacultyClassroomEvaluator.cs         (H10)
│   │   │   ├── CorequisiteEvaluator.cs              (H11)
│   │   │   ├── CreditHourEvaluator.cs               (H12)
│   │   │   ├── LunchBreakEvaluator.cs               (H13)
│   │   │   └── MaxConsecutiveHoursEvaluator.cs      (H15)
│   │   │
│   │   ├── Evaluators/Soft/               <- YENi
│   │   │   ├── OccupancyRateEvaluator.cs            (S1)
│   │   │   ├── DailyLoadEvaluator.cs                (S2)
│   │   │   ├── SlotCongestionEvaluator.cs           (S3)
│   │   │   ├── CoEnrollmentEvaluator.cs             (S4)
│   │   │   ├── LunchBreakSoftEvaluator.cs           (S5)
│   │   │   ├── StudentGapEvaluator.cs               (S7)
│   │   │   ├── InstructorDaySpreadEvaluator.cs      (S8)
│   │   │   ├── EarlyLateEvaluator.cs                (S9)
│   │   │   └── WeekdayBalanceEvaluator.cs           (S12)
│   │   │
│   │   └── ConstraintConfigProvider.cs     <- YENi
│   │
│   ├── EnrollmentForecastService.cs        <- TEK forecast kaynagi
│   ├── CourseOpeningService.cs             <- Inline forecast kaldirilir
│   └── AcademicOperationsService.cs        <- Onay-Acma entegrasyonu

SIS.API/
├── Controllers/
│   └── ConstraintController.cs             <- YENi

SIS.Web/
├── pages/
│   └── ConstraintManagementPage.tsx        <- YENi
├── components/
│   └── course-opening/                     <- Ayristirilmis componentler
│       ├── AddCourseDialog.tsx
│       ├── BulkOpenDialog.tsx
│       ├── SemesterSectionGroup.tsx
│       ├── WorkloadBar.tsx
│       └── ForecastSummaryPanel.tsx
```
