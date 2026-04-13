# Akıllı Ders Önerisi ve Niyet Anketi Sistemi — Tasarım Dokümanı

**Tarih:** 2026-04-13
**Durum:** Onaylandı
**Modül:** Smart Course Recommendation + Course Intention Survey

---

## 1. Özet

İki bağımsız ama birbirini tamamlayan sistem:

1. **Niyet Anketi** — Kayıt döneminden önce öğrencilerin ders niyetlerini toplar. Amacı akademik yönetim: şube planlama ve talep tahmini (forecast) güçlendirme.

2. **Akıllı Ders Önerisi** — Kayıt sırasında öğrenciye kişiselleştirilmiş ders önerisi sunar. Mevcut eligibility, müfredat, AGNO verilerini zorluk skoru ve kritik yol analizi ile birleştirerek en uygun programı önerir.

Ek olarak **Danışman Kısıt Sistemi** her iki sistemi de etkiler: danışman öğrenci bazlı ders kısıtları koyabilir.

---

## 2. Sistem 1: Niyet Anketi

### 2.1 Amaç

- Bölüm başkanına doğru şube sayısı açtırmak
- Mevcut EnrollmentForecastService'e gerçek öğrenci niyeti verisi sağlamak
- Öğrenciye doğrudan fayda sağlamaz — dolaylı fayda (doğru şube açılırsa yer bulur)

### 2.2 Zamanlama

```
Tetiklenme: Bütünleme/Yaz okulu not sonuçları sisteme girilip kesinleştiğinde
            → Otomatik açılır
            → Admin isterse elle de açabilir ("Anketi Aç" butonu)

Süre: 3 gün açık kalır → otomatik kapanır

Neden sonuçlardan sonra:
  - AGNO kesinleşmiş olur → ECTS limiti doğru hesaplanır
  - Ön koşul durumları kesin → eligibility doğru
  - Alttan ders listesi kesin
  
Lifecycle'daki yeri:
  ...GradeEntry → Closed → [bütünleme/yaz okulu] → Sonuç İlan
  → [3 GÜN ANKET] → Anket kapanır → Forecast güncellenir 
  → Planning → Scheduling → Published → RegistrationOpen
```

### 2.3 Anket İçeriği

Öğrenciye gösterilen:

```
Müfredatından alabileceği dersler (eligibility kontrolü yapılmış):
  ├── Alttan dersler (FF/NA/U) — kırmızı etiket, öncelikli
  ├── Dönem dersleri (müfredat sırasına göre) — normal
  ├── Seçmeli havuzundaki uygun dersler — seçmeli etiketi
  └── Üst dönem ama ön koşulu sağlanan dersler — üst dönem etiketi

Her ders için öğrenci seçer:
  ├── "Kesin alacağım"
  ├── "Belki alacağım"  
  └── (işaretlemez = almayı düşünmüyor)

Ek tercih bilgileri:
  ├── Hangi günler uygun (Pazartesi-Cuma checkbox)
  ├── Tercih edilen zaman dilimi: Sabah / Öğleden sonra / Fark etmez
  └── Bu bilgiler forecast raporunda bölüm başkanına gösterilir
```

### 2.4 Anket Kuralları

- **Zorunlu değil.** Doldurmayan öğrenci kayıt sırasında sorun yaşamaz
- **Ders bazlı.** Section (şube) henüz yok, öğrenci ders seçer
- **Kapasite rezervasyonu yok.** Anket doldurmak yer ayırtmaz
- **Bir kez doldurulur.** 3 günlük pencere içinde düzenlenebilir
- **Danışman onayı yok.** Anket sadece niyet beyanı

### 2.5 Forecast Entegrasyonu

Anket verisi mevcut forecast'ın yerine geçmez, **yanında** gösterilir:

```
Bölüm başkanının gördüğü (CourseOpeningPage):

  MATH101 - Calculus I
  ┌──────────────────────────────────────────────┐
  │ Rule-Based Tahmin:  72 öğrenci  (mevcut)     │
  │ Anket Verisi:       45 kesin + 18 belki = 63 │
  │                                               │
  │ Ankete katılım: %60 (120/200 öğrenci)        │
  │ Doldurmayan 80 kişi için rule-based geçerli   │
  └──────────────────────────────────────────────┘

İki veri yan yana gösterilir.
Bölüm başkanı ikisini de değerlendirip kendi kararını verir.
Sistem tek bir "blended" sonuç üretmez.
```

### 2.6 Entity: CourseIntention

| Alan | Tip | Açıklama |
|------|-----|----------|
| Id | int | PK |
| StudentId | int | FK → Student |
| TermId | int | FK → AcademicTerm (bir sonraki dönem) |
| CourseId | int | FK → Course (ders bazlı, section değil) |
| IntentionLevel | IntentionLevel enum | Certain / Maybe |
| CreatedAt | DateTime | Oluşturma zamanı |
| UpdatedAt | DateTime | Son güncelleme |

### 2.7 Entity: CourseIntentionSurvey

| Alan | Tip | Açıklama |
|------|-----|----------|
| Id | int | PK |
| TermId | int | FK → AcademicTerm (hangi dönem için) |
| OpenedAt | DateTime | Anketin açıldığı an |
| ClosesAt | DateTime | Anketin kapanacağı an (açılış + 3 gün) |
| OpenedBy | string? | Admin açtıysa userId, otomatikse null |
| Status | SurveyStatus enum | Open / Closed |

**SurveyStatus Enum:**
```
Open = 0     // Anket açık, öğrenciler doldurabilir
Closed = 1   // Süre doldu veya admin kapattı
```

**IntentionLevel Enum:**
```
Certain = 0  // Kesin alacağım
Maybe = 1    // Belki alacağım
```

### 2.8 Entity: StudentSurveyPreference

| Alan | Tip | Açıklama |
|------|-----|----------|
| Id | int | PK |
| StudentId | int | FK → Student |
| SurveyId | int | FK → CourseIntentionSurvey |
| PreferredDays | string | JSON: ["Monday","Tuesday","Wednesday"] |
| PreferredTimeSlot | TimeSlotPreference enum | Morning / Afternoon / NoPreference |

**TimeSlotPreference Enum:**
```
Morning = 0      // Sabah tercihi
Afternoon = 1    // Öğleden sonra tercihi
NoPreference = 2 // Fark etmez
```

---

## 3. Sistem 2: Akıllı Ders Önerisi

### 3.1 Amaç

Kayıt sırasında öğrenciye kişiselleştirilmiş ders önerisi:
- Hangi dersleri alması gerekiyor (zorunlu)
- Hangi dersleri alması öneriliyor (optimal)
- Hangi dersleri alabilir (opsiyonel)
- Her önerinin arkasındaki sebebi açıklama

### 3.2 Zamanlama

```
Aktif olduğu dönem: RegistrationOpen ve AddDrop aşamalarında
Öğrenci ders katalog sayfasında öneri panelini görür
```

### 3.3 Mevcut Servislerden Beslenen Veriler

Yeni hesaplama yapmadan mevcut servislerin zaten sağladığı:

| Veri | Kaynak Servis | Açıklama |
|------|--------------|----------|
| Hangi dersi alabilir | StudentEligibilityService | Ön koşul, AGNO, ECTS, durum kontrolü |
| İlkDefa/Alttan/ÖncedenAlındı | CourseController (StudentCatalog) | TakeStatus alanı |
| Geçilen/kalan dersler | CurriculumStatus | Müfredat haritası |
| AGNO, ECTS limiti | GpaCalculationService | ECTS limiti: <2.00→30, 2.00-2.49→42, ≥2.50→48 |
| Ön koşul + eşdeğerlik | RegistrationValidationService | Zincir kontrolü |
| Alttan ders önceliği | FR-55 validasyonu | Alttan seçmeden yeni seçemez |
| Zaman çakışması | ScheduleConflict validasyonu | Section bazlı çakışma kontrolü |

**Bu servisler tekrar yazılmayacak, doğrudan çağrılacak.**

### 3.4 Yeni Hesaplamalar

#### 3.4.1 Ders Zorluk Skoru (CourseDifficultyScore)

5 faktörün ağırlıklı bileşimi. Her faktör 0-1 arasında normalize edilir.

**Faktör 1: Başarısızlık Oranı (FailRate)**
```
Veri: Son 3 dönemdeki kayıtlar
Hesaplama: (FF + NA + U sayısı) / (toplam tamamlanan kayıt sayısı)
Normalize: Oran zaten 0-1 arası
Örnek: 200 alan, 50 FF/NA/U → FailRate = 0.25
```

**Faktör 2: Not Dağılımı + Standart Sapma (GradeDistribution)**
```
Veri: Son 3 dönemdeki harf notları → puana çevir (AA=4.0, BA=3.5, ... FF=0)
Hesaplama: 
  mean = ortalama puan
  stddev = standart sapma
  score = (4.0 - mean) / 4.0 × 0.7 + min(stddev / 1.5, 1.0) × 0.3
Normalize: 0-1 arası (yüksek = zor)
Açıklama: 
  - Düşük ortalama = zor ders
  - Yüksek standart sapma = tahmin edilemez ders (bazıları AA, bazıları FF)
  - İkisinin bileşimi
```

**Faktör 3: Tekrar Alma Başarı Oranı (RetakeSuccessRate) — ters çevrilmiş**
```
Veri: Dersi 2+ kez alan öğrenciler
Hesaplama: (ikinci seferde geçen sayısı) / (ikinci kez alan sayısı)
Normalize: score = 1.0 - RetakeSuccessRate (ters: başarı düşükse zorluk yüksek)
Örnek: 30 kişi tekrar aldı, 20'si geçti → RetakeSuccess=0.67 → score=0.33
Açıklama: Tekrar alanların çoğu geçiyorsa ders öğrenilebilir,
          azı geçiyorsa gerçekten zor
Özel durum: Tekrar alan yoksa (yeni ders) → score = 0.5 (nötr)
```

**Faktör 4: ECTS Ağırlığı (EctsWeight)**
```
Veri: Dersin ECTS değeri
Hesaplama: ECTS / maxECTS (bölümdeki en yüksek ECTS'li ders)
Normalize: 0-1 arası
Örnek: 6 ECTS, max 8 ECTS → score = 0.75
Açıklama: Yüksek ECTS = daha fazla iş yükü
```

**Faktör 5: Son Dönem Trendi (RecentTrend)**
```
Veri: Her dönemin ortalama notu (son 4 dönem)
Hesaplama: 
  Ağırlıklı ortalama: [%15, %20, %25, %40] (en son dönem en ağırlıklı)
  Basit ortalama: eşit ağırlık
  trend = weightedAvg / simpleAvg
  score = eğer trend < 1.0 → (1.0 - trend) → zorlaşıyor
          eğer trend > 1.0 → 0 → kolaylaşıyor
Normalize: 0-1 arası, clamp
Açıklama: Son dönemlerde not ortalaması düşüyorsa ders zorlaşıyor demek
Özel durum: 2'den az dönem verisi → score = 0 (nötr, trend hesaplanamaz)
```

**Final Skor:**
```
DifficultyScore = w1 × FailRate 
                + w2 × GradeDistribution 
                + w3 × RetakeSuccessRate
                + w4 × EctsWeight 
                + w5 × RecentTrend

Ağırlıklar (başlangıç değerleri, SystemConfig'den ayarlanabilir):
  w1 = 0.30  (başarısızlık oranı en güçlü sinyal)
  w2 = 0.25  (not dağılımı)
  w3 = 0.20  (tekrar alma başarısı)
  w4 = 0.15  (ECTS yükü)
  w5 = 0.10  (trend)

Sonuç: 0-1 arası (0 = çok kolay, 1 = çok zor)
Gösterim: 1-5 yıldız veya "Kolay / Orta / Zor / Çok Zor" etiketi
```

#### 3.4.2 Kritik Yol Analizi (CriticalPathAnalysis)

Ön koşul zinciri üzerinde topolojik analiz:

```
Girdi: Öğrencinin kalan dersleri + ön koşul grafiği
Hesaplama:
  1. Kalan derslerden ön koşul grafiği oluştur (DAG)
  2. Her ders için "en uzun yol" hesapla (mezuniyete kadar kaç dönem)
  3. Kritik yol = en uzun zincir
  4. Bu dönem alınmazsa mezuniyeti uzatan dersler = kritik dersler

Çıktı:
  ├── IsCritical: bool (bu dönem alınmazsa mezuniyet uzar mı)
  ├── ChainLength: int (bu dersten sonra kaç bağımlı ders var)
  ├── DelayImpact: int (alınmazsa kaç dönem uzar)
  └── DependentCourses: string[] (bağımlı ders kodları)

Örnek:
  CENG201 → CENG301 → CENG401 → CENG499
  Öğrenci CENG201'i almadıysa:
    IsCritical = true
    ChainLength = 3
    DelayImpact = 3 dönem (her ders 1 dönem)
    DependentCourses = ["CENG301", "CENG401", "CENG499"]
```

### 3.5 Öneri Motoru (RecommendationEngine)

#### 3.5.1 Girdi

```
RecommendationRequest:
  ├── StudentId: int
  ├── TermId: int
  │
  │ Mod ayarları (öğrenci seçer):
  ├── GraduationSpeedWeight: decimal (0-1, slider sol tarafı)
  │     0 = tamamen dengeli, 1 = tamamen hızlı mezuniyet
  ├── MaxCourseCount: int? (null = ECTS limitine kadar, sayı = hard limit)
  ├── ExcludedDays: List<DayOfWeek>? (müsait olmayan günler)
  └── PreferredTimeSlot: TimeSlotPreference? (sabah/öğleden sonra/fark etmez)
```

#### 3.5.2 Algoritma

```
Adım 1: Aday dersları topla
  ├── StudentEligibilityService'den alınabilecek dersler
  ├── CurriculumStatus'dan kalan dersler
  ├── Her ders için TakeStatus (İlkDefa/Alttan/ÖncedenAlındı)
  └── Danışman kısıtlarını filtrele (Blok olanları çıkar)

Adım 2: Her derse skor hesapla
  ├── Zorunluluk skoru:
  │     Alttan (FF/NA/U) → 100 puan (en yüksek öncelik)
  │     Kritik yol dersi → 70 + (ChainLength × 5) puan
  │     Müfredat dönem dersi → 50 puan
  │     Üst dönem dersi → 20 puan
  │     Seçmeli → 30 puan
  │
  ├── Mod ağırlıklı skor:
  │     Hızlı mezuniyet (weight=1): zorunluluk skoru × 1.0
  │     Dengeli (weight=0.5): zorunluluk skoru × 0.5 + (1-zorluk) × 0.5
  │     Hafif yük (weight=0): (1-zorluk) × 0.7 + (1-ECTS/maxECTS) × 0.3
  │
  └── Final skor = Mod ağırlıklı skor

Adım 3: ECTS limiti içinde en yüksek skorlu dersleri seç
  ├── Skora göre sırala (yüksek → düşük)
  ├── Sırayla ekle, ECTS limitini aşana kadar
  ├── MaxCourseCount varsa ders sayısını da kontrol et
  └── Alttan dersler her zaman dahil (FR-55 zorunluluğu)

Adım 4: Kategorize et
  🔴 Zorunlu Al:
    ├── Alttan dersler (FF/NA/U aldığın, tekrar almalısın)
    ├── Kritik yol dersleri (almazsan mezuniyet uzar)
    └── Sebep açıklaması eklenir

  🟡 Önerilen:
    ├── Müfredat sırasındaki dersler (ECTS limitine sığan)
    ├── Mod'a göre seçilmiş dersler
    └── Sebep açıklaması eklenir

  ⚪ Alabilirsin:
    ├── ECTS limitine sığmayan ama uygun dersler
    ├── Üst dönem dersleri
    ├── Ekstra seçmeliler
    └── Sebep açıklaması eklenir

Adım 5: Ek bilgiler ekle
  ├── Her dersin zorluk skoru (1-5)
  ├── Kritik yol bilgisi ("Bu dersi almazsan CENG301-401 zinciri 2 dönem uzar")
  ├── Danışman uyarısı (varsa)
  ├── Ankette seçilmişse "Ankette seçmiştin" etiketi
  └── Gün/saat filtresi uygulanmış section bilgisi (varsa)
```

#### 3.5.3 Çıktı

```
RecommendationResult:
  ├── StudentSummary:
  │     ├── Cgpa, Sgpa, EctsLimit, CompletedSemesters
  │     ├── RemainingCourses, RemainingEcts (mezuniyete kalan)
  │     ├── EstimatedGraduationSemester (kritik yol bazlı)
  │     └── ActiveMode (slider değeri)
  │
  ├── MustTake: List<RecommendedCourse> (🔴)
  ├── Recommended: List<RecommendedCourse> (🟡)
  ├── Optional: List<RecommendedCourse> (⚪)
  │
  └── Warnings: List<string> (genel uyarılar)

RecommendedCourse:
  ├── CourseId, CourseCode, CourseName, Ects, Credits
  ├── Category: MustTake | Recommended | Optional
  ├── Reason: string (neden önerildiği, Türkçe)
  │     Örnekler:
  │     "FF aldın, tekrar almalısın"
  │     "Bu dersi almazsan CENG301→CENG401 zinciri 2 dönem uzar"
  │     "3. dönem müfredat dersin, ön koşullar tamam"
  │     "Seçmeli kotandan 2 ders kaldı"
  │
  ├── TakeStatus: IlkDefa | Alttan | OncedenAlindi
  ├── DifficultyScore: decimal (0-1)
  ├── DifficultyLabel: string (Kolay/Orta/Zor/Çok Zor)
  ├── CriticalPath: CriticalPathInfo? (null ise kritik yolda değil)
  │     ├── ChainLength: int
  │     ├── DelayImpact: int
  │     └── DependentCourses: string[]
  │
  ├── AdvisorConstraint: AdvisorConstraintInfo? (null ise kısıt yok)
  │     ├── Type: Block | Warning
  │     └── Message: string (danışmanın yazdığı not)
  │
  ├── SurveySelected: bool (ankette seçmişti mi)
  ├── AvailableSections: List<SectionBrief> (açılmış şubeler)
  │     ├── SectionId, SectionNumber
  │     ├── Instructor, Schedule, Classroom
  │     ├── Capacity, EnrolledCount
  │     └── ConflictsWithSelected: bool (seçilen derslerle çakışıyor mu)
  │
  └── PrerequisitesMet: bool
```

### 3.6 Gün/Saat Filtresi

```
Gün/saat filtresi sadece KAYIT SIRASINDA (Sistem 2) kullanılır:
  - Ankette (Sistem 1) gün/saat tercihi toplanır ama forecast içindir
  - Kayıtta öğrenci "Cuma müsait değilim" derse:
    → Dersler hâlâ gösterilir (filtre önerileri etkiler, dersi gizlemez)
    → Cuma günü section'ı olan dersler sarı uyarı alır
    → Alternatif section varsa önerilir
    → Tüm section'ları Cuma ise ders gösterilir ama uyarı ile
```

---

## 4. Danışman Kısıt Sistemi

### 4.1 Genel Kurallar

- **Dönemlik:** Her kısıt bir döneme aittir. Dönem sonunda otomatik sıfırlanır
- **Her zaman girilebilir:** Kayıt öncesi, sırası, sonrası — danışman istediği zaman koyar/kaldırır
- **İki tip:**
  - **Blok:** Öğrenci bu dersi seçemez. RegistrationPlan'a ekleyemez
  - **Uyarı:** Öğrenci seçebilir ama "Danışmanınız bu dersi almanızı önermemektedir" uyarısı görür
- **Görünürlük:** Kısıt konulduğu anda öğrencinin ekranında görünür
- **Danışman değişimi:** Eski danışmanın kısıtları dönem sonuna kadar geçerli kalır
- **Öğrenci bazlı:** Her kısıt belirli bir öğrenci + belirli bir ders içindir

### 4.2 Entity: AdvisorCourseConstraint

| Alan | Tip | Açıklama |
|------|-----|----------|
| Id | int | PK |
| AdvisorId | int | FK → Instructor (danışman) |
| StudentId | int | FK → Student |
| CourseId | int | FK → Course |
| TermId | int | FK → AcademicTerm |
| ConstraintType | AdvisorConstraintType enum | Block / Warning |
| Reason | string | Danışmanın yazdığı sebep |
| CreatedAt | DateTime | Oluşturulma zamanı |
| IsActive | bool | Aktif mi (danışman kaldırabilir) |

**AdvisorConstraintType Enum:**
```
Block = 0    // Öğrenci bu dersi seçemez
Warning = 1  // Öğrenci seçebilir ama uyarı alır
```

### 4.3 Entegrasyon Noktaları

```
1. Öneri motoru (Sistem 2):
   → Blok kısıtlı dersler aday listesinden çıkarılır
   → Uyarı kısıtlı dersler gösterilir ama AdvisorConstraint bilgisi eklenir

2. Kayıt planı (mevcut RegistrationPlan):
   → AddToPlanAsync çağrılırken AdvisorCourseConstraint kontrol edilir
   → Blok varsa → hata döner, plana eklenemez
   → Uyarı varsa → eklenir ama response'da uyarı mesajı döner

3. Danışman onay ekranı (mevcut AdvisorDashboardPage):
   → Danışman kendi koyduğu kısıtları listede görür
   → Öğrenci uyarılı derse rağmen seçtiyse kırmızı işaret

4. Niyet anketi (Sistem 1):
   → Blok kısıtlı dersler ankette gösterilmez
   → Uyarı kısıtlı dersler uyarı ile gösterilir
```

### 4.4 Danışman Arayüzü

```
Mevcut Danışman paneline ek tab veya bölüm:

"Ders Kısıtları" sekmesi:
  ┌─────────────────────────────────────────────────┐
  │ Öğrenci: [Ahmet Yılmaz - 2021001 ▾]            │
  │ Dönem:   [2026-2027 Güz ▾]                      │
  │                                                  │
  │ Aktif Kısıtlar:                                  │
  │ ┌───────────────────────────────────────────┐    │
  │ │ CENG401 - Blok                            │    │
  │ │ "Önce stajını tamamlaması gerekiyor"      │    │
  │ │                              [Kaldır]     │    │
  │ ├───────────────────────────────────────────┤    │
  │ │ MATH301 - Uyarı                           │    │
  │ │ "Lineer Cebir'i tekrar etmeni öneririm"   │    │
  │ │                              [Kaldır]     │    │
  │ └───────────────────────────────────────────┘    │
  │                                                  │
  │ [+ Kısıt Ekle]                                   │
  │   Ders: [CENG499 - Mezuniyet Projesi ▾]         │
  │   Tip:  ◉ Blok  ○ Uyarı                         │
  │   Sebep: [________________________]              │
  │   [Kaydet]                                       │
  └─────────────────────────────────────────────────┘
```

---

## 5. API Endpoint'leri

### 5.1 Niyet Anketi API

**CourseIntentionController** (`/api/course-intention`)

| Method | Route | Yetki | Açıklama |
|--------|-------|-------|----------|
| GET | `/survey/active` | Student | Aktif anket var mı, bilgileri |
| GET | `/survey/{surveyId}/courses` | Student | Ankette seçilebilecek dersler (eligibility uygulanmış) |
| POST | `/survey/{surveyId}/submit` | Student | Niyet bildir (ders listesi + tercihler) |
| PUT | `/survey/{surveyId}/update` | Student | Niyeti güncelle (3 gün içinde) |
| POST | `/survey/open` | SystemAdmin | Anketi manuel aç |
| POST | `/survey/close` | SystemAdmin | Anketi manuel kapat |
| GET | `/survey/{surveyId}/results` | DeptManager, Dean, Admin | Anket sonuçları (ders bazlı talep) |
| GET | `/survey/{surveyId}/results/department/{deptId}` | DeptManager | Bölüm bazlı anket sonuçları |

### 5.2 Öneri API

**RecommendationController** (`/api/recommendation`)

| Method | Route | Yetki | Açıklama |
|--------|-------|-------|----------|
| POST | `/generate` | Student | Öneri üret (mod ayarları body'de) |
| GET | `/difficulty/{courseId}` | Student | Tek dersin zorluk detayı |
| GET | `/critical-path` | Student | Öğrencinin kritik yol analizi |

### 5.3 Danışman Kısıt API

**AdvisorConstraintController** (`/api/advisor/constraints`)

| Method | Route | Yetki | Açıklama |
|--------|-------|-------|----------|
| GET | `/student/{studentId}` | Advisor | Öğrencinin aktif kısıtları |
| POST | `/` | Advisor | Yeni kısıt ekle |
| DELETE | `/{id}` | Advisor | Kısıt kaldır |
| GET | `/my-constraints` | Advisor | Danışmanın tüm aktif kısıtları |

---

## 6. Frontend Sayfaları ve Bileşenler

### 6.1 Niyet Anketi (Yeni sayfa)

**Sayfa:** `CourseIntentionSurveyPage` (`/ders-niyet-anketi`)

```
┌─────────────────────────────────────────────────────┐
│  Ders Niyet Anketi — 2026-2027 Güz                  │
│  Kalan süre: 2 gün 14 saat                          │
│                                                      │
│  AGNO: 2.85  │  ECTS Limit: 48  │  Dönem: 5         │
│                                                      │
│  ▼ Alttan Dersler (öncelikli)                        │
│  ┌──────────────────────────────────────────────┐    │
│  │ ☑ MATH101 Calculus I (6 ECTS) - FF [Kesin]   │    │
│  └──────────────────────────────────────────────┘    │
│                                                      │
│  ▼ 5. Dönem Dersleri                                 │
│  ┌──────────────────────────────────────────────┐    │
│  │ ☑ CENG301 Algorithms (5 ECTS)       [Kesin]  │    │
│  │ ☑ CENG311 Database (4 ECTS)         [Belki]  │    │
│  │ ☐ CENG321 Networks (4 ECTS)                   │    │
│  └──────────────────────────────────────────────┘    │
│                                                      │
│  ▼ Seçmeli Dersler                                   │
│  ┌──────────────────────────────────────────────┐    │
│  │ ☑ CENG3XX AI Intro (3 ECTS)         [Belki]  │    │
│  │ ☐ CENG3XX Web Dev (3 ECTS)                    │    │
│  └──────────────────────────────────────────────┘    │
│                                                      │
│  ▼ Tercihler                                         │
│  Uygun günler: [✓Pzt] [✓Sal] [✓Çar] [✓Per] [☐Cum]  │
│  Zaman dilimi: ◉ Sabah  ○ Öğleden sonra  ○ Fark etmez│
│                                                      │
│  Seçilen: 4 ders, 18 ECTS / 48 ECTS limit           │
│                                                      │
│  [Kaydet]                                            │
└─────────────────────────────────────────────────────┘
```

### 6.2 Öneri Paneli (Mevcut ders katalog sayfasına ek)

**Bileşen:** `RecommendationPanel` — CourseCatalogPage'e sidebar veya üst panel olarak eklenir

```
┌─────────────────────────────────────────────────────┐
│  Akıllı Ders Önerisi                                 │
│                                                      │
│  ⚙ Ayarlar                                          │
│  Mezuniyet hızı [████████░░] Ders yükü dengesi       │
│  Max ders: [  6  ]  Müsait olmayan gün: [☐Cum]      │
│                                                      │
│  Tahmini mezuniyet: 2028 Bahar (4 dönem kaldı)      │
│                                                      │
│  🔴 ZORUNLU AL                                       │
│  ┌──────────────────────────────────────────────┐    │
│  │ MATH101 Calculus I (6 ECTS) ⭐⭐⭐⭐ Zor       │    │
│  │ FF aldın, tekrar almalısın                    │    │
│  │ 3 şube mevcut  [Planıma Ekle]                │    │
│  ├──────────────────────────────────────────────┤    │
│  │ CENG201 Data Struct (5 ECTS) ⭐⭐⭐ Orta       │    │
│  │ Almazsan CENG301→401→499 zinciri 3 dönem uzar│    │
│  │ 2 şube mevcut  [Planıma Ekle]                │    │
│  └──────────────────────────────────────────────┘    │
│                                                      │
│  🟡 ÖNERİLEN                                        │
│  ┌──────────────────────────────────────────────┐    │
│  │ PHYS102 Physics II (4 ECTS) ⭐⭐ Kolay         │    │
│  │ 3. dönem dersin, ön koşullar tamam            │    │
│  │ 📋 Ankette seçmiştin                          │    │
│  │ 2 şube mevcut  [Planıma Ekle]                │    │
│  ├──────────────────────────────────────────────┤    │
│  │ ENG201 English II (3 ECTS) ⭐ Kolay            │    │
│  │ Seçmeli kotandan 2 ders kaldı                 │    │
│  │ ⚠ Danışman uyarısı: "Önce ENG102 notunu      │    │
│  │   yükseltmeni öneririm"                       │    │
│  │ 1 şube mevcut  [Planıma Ekle]                │    │
│  └──────────────────────────────────────────────┘    │
│                                                      │
│  ⚪ ALABİLİRSİN                                     │
│  ┌──────────────────────────────────────────────┐    │
│  │ CENG302 OS (4 ECTS) ⭐⭐⭐ Orta                 │    │
│  │ Üst dönem ama ön koşulun var                  │    │
│  │ 1 şube mevcut  [Planıma Ekle]                │    │
│  └──────────────────────────────────────────────┘    │
│                                                      │
│  🚫 BLOKLANAN (Danışman kısıtı)                     │
│  ┌──────────────────────────────────────────────┐    │
│  │ CENG401 Senior Project ✗ BLOK                 │    │
│  │ "Önce stajını tamamlaması gerekiyor"          │    │
│  └──────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────┘
```

### 6.3 Danışman Kısıt Yönetimi (Mevcut danışman paneline ek)

Bölüm 4.4'te detaylı gösterildi.

---

## 7. Dosya Yapısı

### Backend

```
SIS.Domain/
  Entities/
    CourseIntention.cs
    CourseIntentionSurvey.cs
    StudentSurveyPreference.cs
    AdvisorCourseConstraint.cs
  Enums/
    IntentionLevel.cs
    SurveyStatus.cs
    AdvisorConstraintType.cs
    TimeSlotPreference.cs

SIS.Application/
  CourseIntention/
    DTOs/
      SurveyDto.cs
      CourseIntentionDto.cs
      SurveyResultDto.cs
      StudentSurveyPreferenceDto.cs
    Services/
      ICourseIntentionService.cs
  
  Recommendation/
    DTOs/
      RecommendationRequestDto.cs
      RecommendationResultDto.cs
      RecommendedCourseDto.cs
      CourseDifficultyDto.cs
      CriticalPathDto.cs
    Services/
      IRecommendationService.cs
      ICourseDifficultyService.cs
      ICriticalPathService.cs

  AdvisorConstraint/
    DTOs/
      AdvisorCourseConstraintDto.cs
      CreateConstraintDto.cs
    Services/
      IAdvisorConstraintService.cs

SIS.Infrastructure/
  Services/
    Recommendation/
      RecommendationService.cs
      CourseDifficultyService.cs
      CriticalPathService.cs
    CourseIntention/
      CourseIntentionService.cs
      SurveyAutoOpenService.cs  (anket otomatik açma/kapama)
    AdvisorConstraint/
      AdvisorConstraintService.cs

SIS.API/
  Controllers/
    CourseIntentionController.cs
    RecommendationController.cs
    AdvisorConstraintController.cs
```

### Frontend

```
SIS.Web/src/
  pages/
    CourseIntentionSurveyPage.tsx
  components/
    recommendation/
      RecommendationPanel.tsx
      RecommendedCourseCard.tsx
      ModeSettingsBar.tsx
      DifficultyBadge.tsx
      CriticalPathTag.tsx
    advisor-constraint/
      ConstraintListPanel.tsx
      AddConstraintDialog.tsx
    course-intention/
      IntentionCourseRow.tsx
      PreferenceForm.tsx
      SurveyCountdown.tsx
  hooks/
    useRecommendation.ts
    useCourseIntention.ts
    useAdvisorConstraints.ts
    useCourseDifficulty.ts
  api/
    recommendation.api.ts
    courseIntention.api.ts
    advisorConstraint.api.ts
  types/
    recommendation.ts
    courseIntention.ts
    advisorConstraint.ts
```

---

## 8. Mevcut Sistemle Entegrasyon

### 8.1 Dokunulmayacak Dosyalar

```
- Scheduling motoru (SIS.Infrastructure/Services/Scheduling/) → tamamen ayrı
- RegistrationPlan entity ve akışı → mevcut yapı korunur
- GpaCalculationService → sadece çağrılır, değiştirilmez
- StudentEligibilityService → sadece çağrılır, değiştirilmez
- RegistrationValidationService → sadece çağrılır, değiştirilmez
- EnrollmentForecastService → değiştirilmez, anket verisi ayrı gösterilir
```

### 8.2 Değiştirilecek Dosyalar

| Dosya | Değişiklik | Risk |
|-------|-----------|------|
| `CourseRegistrationService.cs` | `AddToPlanAsync` içine AdvisorConstraint kontrolü ekle | ORTA — mevcut validasyon zincirinin sonuna eklenir |
| `CourseCatalogPage.tsx` | RecommendationPanel bileşenini sidebar olarak ekle | DÜŞÜK — mevcut sayfaya yan panel ekleme |
| `AdvisorDashboardPage.tsx` | Kısıt yönetimi tab'ı ekle | DÜŞÜK — yeni tab ekleme |
| `CourseOpeningPage.tsx` | Anket sonuçlarını forecast yanında göster | DÜŞÜK — mevcut forecast paneline ek veri |
| `AppDbContext.cs` | 4 yeni entity için DbSet ekle | DÜŞÜK — yeni satırlar |
| `App.tsx` | 1 yeni route (anket sayfası) | DÜŞÜK |
| `Sidebar / menuGroups` | 1 yeni link (anket) | DÜŞÜK |

### 8.3 Çakışma Riski (Diğer Görevlerle)

```
Akademik Takvim görevi ile: SIFIR çakışma
  → Tamamen farklı entity, controller, sayfa

Senin admin sayfaların ile: 
  → CourseOpeningPage'de minor ek (anket verisi gösterimi)
  → Aynı anda çalışılırsa iletişim gerekir ama risk düşük

Diğer görevlerle (PDF, Excel, Şifre): SIFIR çakışma
```

---

## 9. Background Servis: SurveyAutoOpenService

```
Tetiklenme: Dönem durumu "Closed" olduğunda 
  (bütünleme notları kesinleşmiş demektir)

İş mantığı:
  1. Bir sonraki dönem için CourseIntentionSurvey oluştur
  2. Status = Open, ClosesAt = now + 3 gün
  3. 3 gün sonra otomatik Status = Closed

Alternatif tetiklenme: 
  Admin "/api/course-intention/survey/open" endpoint'ini çağırır
```

---

## 10. Seed Data

```
İlk kurulumda:
  - CourseDifficultyService ağırlıkları SystemConfig'e eklenir
    (w1=0.30, w2=0.25, w3=0.20, w4=0.15, w5=0.10)
  - Zorluk etiket eşikleri SystemConfig'e eklenir
    (0-0.25=Kolay, 0.25-0.50=Orta, 0.50-0.75=Zor, 0.75-1.0=Çok Zor)
```

---

## 11. Kapsam Dışı (Bu Spec'te Yok)

- Ders zorluk skorunda AGNO korelasyonu (Faktör 6 — çıkarıldı)
- Seçmeli ders sıralama/önceliklendirme (filtreleme yeterli, sıralama yok)
- Bekleme listesi (waitlist) sistemi
- Forecast'a anket verisini entegre etme (yan yana gösterim, blend yok)
- Danışman kısıtlarının kalıcı olması (dönemlik, her dönem sıfırlanır)
- Çoklu danışman durumu (çift anadal vb.)
- Anket zorunluluğu (opsiyonel kalacak)
- Anket hatırlatma bildirimi (bildirim sistemi henüz yok)
