# Akademik Takvim Otomatik Oluşturma Sistemi — Tasarım Dokümanı

**Tarih:** 2026-04-13
**Durum:** Onaylandı
**Modül:** Academic Calendar Generator

---

## 1. Özet

Sistem, bir akademik yıl için (Güz + Bahar + Yaz Okulu) tüm takvim etkinliklerini otomatik oluşturur. Admin oluşturma öncesi kısıtlar tanımlar, sistem bu kısıtlara uygun takvimi üretir, admin sonucu düzenler ve onaylar. Düzenleme sırasında bir etkinlik değiştirildiğinde domino etkisi (what-if) gösterilir.

## 2. Genel Akış

```
1. Admin "2026-2027 Akademik Yılı Oluştur" der
2. Kısıt ekranı açılır → tercihler, yasaklar, sabit tarihler, süre ayarları girer
3. Sistem 3 dönemi birlikte üretir (Güz + Bahar + Yaz)
4. Sonuç ekranı: sol takvim + sağ etkinlik listesi
5. Admin düzenler → bir etkinliği değiştirince domino etkisi gösterilir
6. Kısıt ihlalleri uyarı olarak gösterilir
7. Admin onaylar → TermCalendarEvent'lere kaydedilir
```

## 3. Mimari

### 3.1 Yeni Domain Entity'leri

#### CalendarTemplate (Etkinlik Şablonu)

Bir dönem tipinde olması gereken etkinlik zincirlerinin tanımı.

| Alan | Tip | Açıklama |
|------|-----|----------|
| Id | int | PK |
| Name | string | Şablon adı (örn: "Ana Akademik Süreç") |
| ChainCode | string | Zincir kodu: MAIN, TRANSFER, GRADUATE, PREP, FINANCE, DOUBLEMAJOR, INTERNATIONAL |
| IsActive | bool | Şablon aktif mi |

#### CalendarTemplateItem (Şablon Satırı)

Şablon içindeki tek bir etkinliğin tanımı ve bağımlılıkları.

| Alan | Tip | Açıklama |
|------|-----|----------|
| Id | int | PK |
| TemplateId | int | FK → CalendarTemplate |
| EventType | CalendarEventType | Mevcut enum (25 tip) |
| Title | string | Varsayılan başlık (Türkçe) |
| DefaultDuration | int | Varsayılan süre (gün) |
| AnchorType | AnchorType enum | Bağlantı tipi |
| AnchorItemId | int? | Bağımlı olduğu template item (nullable) |
| OffsetDays | int | Çapaya göre offset (pozitif=sonra, negatif=önce) |
| ApplicableSemesters | SemesterType flags | Hangi dönemlerde geçerli (Fall, Spring, Summer) |
| IsRequired | bool | Zorunlu mu, opsiyonel mi |
| SortOrder | int | Zincir içi sıralama |

**AnchorType Enum:**
```
FromTermStart = 0    // Dönem başlangıcına göre offset
FromTermEnd = 1      // Dönem bitişine göre offset
AfterEvent = 2       // Başka bir etkinliğin bitişinden sonra
BeforeEvent = 3      // Başka bir etkinliğin başlangıcından önce
Fixed = 4            // Sabit tarih (admin Pin kısıtı ile)
FromPreviousTerm = 5 // Önceki dönemin bir etkinliğine göre
```

#### HolidayDefinition (Tatil Tanımları)

| Alan | Tip | Açıklama |
|------|-----|----------|
| Id | int | PK |
| Name | string | Tatil adı (örn: "Ramazan Bayramı") |
| EnglishName | string | İngilizce adı |
| Type | HolidayType enum | Fixed veya HijriCalculated |
| FixedMonth | int? | Sabit tatiller: ay |
| FixedDay | int? | Sabit tatiller: gün |
| HijriMonth | int? | Dini bayramlar: Hicri ay |
| HijriDay | int? | Dini bayramlar: Hicri gün |
| DurationDays | decimal | Süre (örn: 4.5 gün = Kurban) |
| IsHalfDayStart | bool | Arife günü yarım gün mü |
| IsActive | bool | Aktif mi |

**HolidayType Enum:**
```
Fixed = 0           // Her yıl aynı tarih (23 Nisan, 29 Ekim...)
HijriCalculated = 1 // Hicri takvimden hesaplanır (Ramazan, Kurban)
```

#### CalendarConstraint (Admin Kısıtları)

| Alan | Tip | Açıklama |
|------|-----|----------|
| Id | int | PK |
| AcademicYear | string | "2026-2027" |
| ConstraintType | ConstraintType enum | Kısıt tipi |
| TargetEventType | CalendarEventType? | Hangi etkinliği etkiler (null=genel) |
| TargetChainCode | string? | Hangi zinciri etkiler (null=tümü) |
| ParameterJson | string | Kısıt parametreleri (JSON) |
| IsHard | bool | Hard: ihlal edilemez, Soft: tercih |
| Priority | int | Öncelik (çakışan soft kısıtlarda) |
| Description | string? | Admin'in yazdığı açıklama |
| CreatedAt | DateTime | Oluşturma tarihi |

**ConstraintType Enum:**
```
Timing = 0        // "Vize 7. haftada olsun" → {"week": 7}
Preference = 1    // "Sınav Pazartesi başlasın" → {"preferredDay": "Monday"}
Prohibition = 2   // "Şu aralıkta etkinlik olmasın" → {"from": "2026-12-25", "to": "2027-01-02"}
Pin = 3           // "Oryantasyon 15 Eylül" → {"date": "2026-09-15"}
Duration = 4      // "Final 12 gün olsun" → {"days": 12}
Dependency = 5    // "Bütünleme finalden 5 gün sonra" → {"afterEvent": "FinalExam", "offsetDays": 5}
MinWeeks = 6      // "Minimum 14 ders haftası" → {"minWeeks": 14}
Buffer = 7        // "Dönemler arası en az 2 hafta" → {"minDays": 14}
```

### 3.2 Tatil Hesaplama Motoru

**HolidayCalculatorService**

```
Girdi: int year (Miladi yıl)
Çıktı: List<ResolvedHoliday> { Name, StartDate, EndDate, DurationDays }

Sabit Tatiller (her yıl aynı gün):
  - 1 Ocak → Yılbaşı (1 gün)
  - 23 Nisan → Ulusal Egemenlik ve Çocuk Bayramı (1 gün)
  - 1 Mayıs → Emek ve Dayanışma Günü (1 gün)
  - 19 Mayıs → Atatürk'ü Anma, Gençlik ve Spor Bayramı (1 gün)
  - 15 Temmuz → Demokrasi ve Milli Beraberlik Günü (1 gün)
  - 30 Ağustos → Zafer Bayramı (1 gün)
  - 28-29 Ekim → Cumhuriyet Bayramı (1.5 gün, 28 öğleden sonra + 29 tam gün)

Dini Bayramlar (Hicri takvim hesaplaması):
  - Ramazan Bayramı → Şevval 1-3 + arife (3.5 gün)
  - Kurban Bayramı → Zilhicce 10-13 + arife (4.5 gün)
  
  Hicri→Miladi dönüşüm algoritması:
    1. Miladi yıldan yaklaşık Hicri yılı hesapla
    2. Kuwaiti algoritması ile Hicri ay/gün → Miladi tarih
    3. Sonuç: kesin Miladi tarihler

Üniversite Özel:
  - 10 Kasım → Atatürk'ü Anma (ders yapılmaz, tatil değil ama etkinlik konmaz)
```

### 3.3 Oluşturma Algoritması

**CalendarGeneratorService**

#### Girdi

```
GenerateCalendarRequest:
  ├── AcademicYear: string ("2026-2027")
  ├── FallStartDate: DateTime? (null ise sistem önersin)
  ├── SpringStartDate: DateTime? (null ise Güz'e göre hesaplansın)
  ├── IncludeSummer: bool (yaz okulu dahil mi)
  ├── Constraints: List<CalendarConstraint>
  └── TemplateOverrides: Dictionary<int, int>? (template item → yeni süre)
```

#### Algoritma: Constraint-Based Placement + Propagation

```
Faz 1: Hazırlık
  1.1 Tatilleri hesapla (HolidayCalculatorService)
  1.2 Şablonları yükle (tüm aktif CalendarTemplate + Item'ları)
  1.3 Admin kısıtlarını yükle ve öncelikle sırala (Hard > Soft, Priority asc)
  1.4 Template item'ları topolojik sıraya koy (bağımlılık grafiğine göre)

Faz 2: Güz Dönemi Yerleştirme
  2.1 Çapa noktalarını yerleştir:
      - Dönem başlangıç/bitiş (admin girdi veya varsayılan)
      - Pin kısıtları (sabit tarihli etkinlikler)
      - Tatiller (hesaplanmış)
  
  2.2 Topolojik sırayla her template item'ı yerleştir:
      a) AnchorType'a göre ideal tarihi hesapla
      b) Tatil çakışma kontrolü:
         → Çakışıyorsa → kaydır (ileri veya geri, hangisi daha az domino etkisi yaratır)
      c) Hard kısıt kontrolü:
         → İhlal varsa → alternatif tarih dene (±1, ±2, ... ±7 gün aralığında)
         → Hiçbiri geçmiyorsa → backtrack (önceki etkinliği de kaydır)
         → BACKTRACK SINIRI: max 3 adım geriye gidilebilir
         → 3 adımda da çözülemezse → etkinliği "Failed" olarak işaretle,
           admin'e uyarı göster ("Bu etkinlik yerleştirilemedi, manuel ayar gerekli")
           ve sonraki etkinliklere devam et (greedy fallback)
         → Neden 3 adım yeterli: zincir içi bağımlılık max 5-6 event
           (kayıt→ders→vize→final→bütünleme→not), 3 adım çoğu çakışmayı çözer
      d) Hafta sonu kontrolü:
         → Başlangıç Cumartesi/Pazar'a denk geliyorsa → Pazartesi'ye kaydır
      e) Yerleştir, bağımlı etkinliklerin ideal tarihlerini güncelle

  2.3 Minimum ders haftası kontrolü:
      - DersBaşlangıç → VizeHaftası arası + VizeSonrası → DersBitiş arası
      - Tatil günlerini çıkar
      - < 14 hafta ise → dönem bitişini uzat veya uyarı üret

Faz 3: Bahar Dönemi Yerleştirme
  3.1 Güz.Bütünleme.Bitiş + yarıyıl_tatili_süresi = Bahar.ÜcretÖdeme.Başlangıç
  3.2 Aynı algoritma (Faz 2) ile Bahar'ı yerleştir
  3.3 Dönemler arası buffer kısıtını kontrol et

Faz 4: Yaz Okulu Yerleştirme (opsiyonel)
  4.1 Bahar.Bütünleme.Bitiş + buffer = Yaz.ÜcretÖdeme.Başlangıç
  4.2 Yaz okulu SUMMER zincirinden yerleştirilir (ayrı şablon, ~6 item):
      - Yaz ücret ödeme + kayıt (3 gün)
      - Yaz dersleri başlangıç/bitiş (7 hafta)
      - Yaz final sınavları (1 hafta)
      - Yaz mazeret sınavları (2 gün)
      - Yaz not ilan (1 gün)
  4.3 Aynı algoritma ile yerleştir

Faz 5: Soft Kısıt Değerlendirme
  5.1 Her soft kısıtın ihlal puanını hesapla:
      - Timing: |gerçek_hafta - hedef_hafta| × ağırlık
      - Preference: gün tercihi uyumsuzluğu × ağırlık
      - Buffer: buffer < istenen ise fark × ağırlık
  5.2 Toplam QualityScore = 100 - toplam_ihlal_puanı (min 0)
  5.3 QualityScore TEK BAŞINA gösterilmez. Admin'e SOMUT ihlal listesi sunulur:
      Örnek:
        "Kalite Skoru: 87/100 — 2 soft kısıt ihlali:
         • Ara sınav 8. haftaya kaydı (hedef: 7. hafta, fark: 1 hafta)
         • Final-bütünleme arası 5 gün (hedef: 7 gün, fark: 2 gün)"
      Admin sayısal skora değil, somut ihlal açıklamalarına bakarak karar verir.

Faz 6: What-If Desteği
  6.1 Admin bir etkinliğin tarihini değiştirir
  6.2 Bağımlılık grafiğinde cascade propagation:
      - Değişen etkinliğe bağımlı tüm etkinlikleri yeniden hesapla
      - Her seviyede tatil/kısıt kontrolü tekrarla
      - CASCADE SINIRI: propagation sadece AYNI DÖNEM içinde yapılır
        Dönemler arası etki TEK YÖNLÜ:
          Güz değişti → Bahar başlangıcı güncellenir → Bahar içi yeniden hesaplanır
          AMA Bahar'ın değişmesi Güz'ü geri ETKİLEMEZ (döngü oluşmaz)
        Bir dönem içinde max 1 tur propagation (recursive cascade yok)
  6.3 Döndür: etkilenen etkinlik listesi + yeni ihlaller + yeni skor
```

#### Çıktı

```
GenerateCalendarResult:
  ├── Events: List<GeneratedCalendarEvent>
  │     ├── TemplateItemId (hangi şablon satırından üretildi)
  │     ├── EventType (CalendarEventType)
  │     ├── Title (string)
  │     ├── StartDate, EndDate (DateTime)
  │     ├── ChainCode (hangi zincire ait)
  │     ├── Semester (Fall/Spring/Summer)
  │     ├── Status: Placed | Shifted | Failed
  │     └── ShiftReason: string? (neden kaydırıldı)
  │
  ├── Holidays: List<ResolvedHoliday> (yerleştirilen tatiller)
  ├── Violations: List<ConstraintViolation>
  │     ├── ConstraintId
  │     ├── Severity: Hard | Soft
  │     ├── Message (Türkçe açıklama)
  │     └── AffectedEvents (etkilenen etkinlik ID'leri)
  │
  ├── QualityScore: int (0-100)
  └── TermSummaries: List<TermSummary>
        ├── Semester
        ├── TotalWeeks, TeachingWeeks
        ├── HolidayDaysInTerm
        └── ExamDays
```

### 3.4 API Endpoint'leri

**CalendarGeneratorController** (`/api/admin/calendar-generator`)

| Method | Route | Açıklama |
|--------|-------|----------|
| GET | `/templates` | Tüm aktif şablonları listele |
| GET | `/templates/{id}/items` | Şablon detayı (item'ları ile) |
| PUT | `/templates/{id}/items` | Şablon item'larını güncelle |
| GET | `/holidays/{year}` | Yıla göre hesaplanmış tatiller |
| GET | `/holidays/definitions` | Tatil tanımları listesi |
| POST | `/holidays/definitions` | Tatil tanımı ekle/güncelle |
| GET | `/constraints/{academicYear}` | Yıla göre kısıtlar |
| POST | `/constraints` | Kısıt ekle |
| PUT | `/constraints/{id}` | Kısıt güncelle |
| DELETE | `/constraints/{id}` | Kısıt sil |
| POST | `/generate` | Takvim oluştur (ana endpoint) |
| POST | `/what-if` | What-if: etkinlik değişikliğinin etkisini hesapla |
| POST | `/apply` | Onaylanan takvimi TermCalendarEvent'lere kaydet |

**Yetkilendirme:** Tüm endpoint'ler `SystemAdministrator` rolü gerektirir.

### 3.5 Frontend Arayüzü

**Yeni Sayfa:** `AcademicCalendarGeneratorPage` (`/akademik-takvim-olusturma`)

**Adım 1 — Kısıt Girişi (Dialog veya üst panel)**
```
┌─────────────────────────────────────────────────────────┐
│  Akademik Yıl: [2026-2027 ▾]                            │
│                                                          │
│  Güz Başlangıç: [21 Eylül 2026]  (opsiyonel)           │
│  Bahar Başlangıç: [    —    ]     (otomatik hesaplansın) │
│  Yaz Okulu: [✓ Dahil]                                   │
│                                                          │
│  Kısıtlar:                                               │
│  ┌──────────────────────────────────────────────────┐    │
│  │ [Hard] Zamanlama: Vize 7. haftada         [× Sil]│    │
│  │ [Hard] Min Hafta: En az 14 ders haftası   [× Sil]│    │
│  │ [Soft] Tercih: Sınav Pazartesi başlasın   [× Sil]│    │
│  │ [Hard] Süre: Final en az 10 gün           [× Sil]│    │
│  │ [+ Kısıt Ekle]                                    │    │
│  └──────────────────────────────────────────────────┘    │
│                                                          │
│  [Takvim Oluştur]                                        │
└─────────────────────────────────────────────────────────┘
```

**Adım 2 — Sonuç (Split View)**
```
┌──────────────────────────┬──────────────────────────────┐
│     TAKVIM GÖRÜNÜMÜ      │     ETKİNLİK LİSTESİ        │
│                          │                               │
│  ◀ Eylül 2026 ▶          │  Kalite Skoru: 87/100        │
│  ┌──┬──┬──┬──┬──┬──┬──┐ │  ⚠ 2 soft kısıt ihlali      │
│  │Pt│Sa│Ça│Pe│Cu│Ct│Pz│ │                               │
│  ├──┼──┼──┼──┼──┼──┼──┤ │  ▼ GÜZ DÖNEMİ               │
│  │  │  │  │  │  │  │  │ │                               │
│  │  │▓▓▓▓▓▓▓▓▓▓│  │  │ │  ▼ Ana Akademik Süreç        │
│  │  │ Kayıt    │  │  │ │  ├ Ücret Ödeme  01.09-12.09   │
│  │  │▓▓▓▓▓▓▓▓▓▓│  │  │ │  ├ Ders Kayıt   15.09-19.09  │
│  │  │ Dersler ████████│ │  ├ Ders Başla   22.09    ✓    │
│  │  │          │  │  │ │  ├ Ekleme-Bır   29.09-03.10   │
│  │  │          │  │  │ │  ├ Çekilme Son  07.11    ✓    │
│  └──┴──┴──┴──┴──┴──┴──┘ │  ├ Ara Sınav   09.11-14.11   │
│                          │  ├ ...                        │
│  ◀ Ekim 2026 ▶           │  ├ Final       04.01-15.01   │
│  ...                     │  └ Bütünleme   26.01-31.01   │
│                          │                               │
│  [Tatiller: 🔴]          │  ▼ Yatay Geçiş Süreci        │
│  [Çakışma:  ⚠]           │  ├ Başvuru     01.08-15.08   │
│  [Normal:   🔵]          │  ├ Değerlendir 18.08-01.09   │
│                          │  └ Kayıt       08.09-12.09   │
│                          │                               │
│                          │  ▼ BAHAR DÖNEMİ              │
│                          │  ...                          │
│                          │                               │
│                          │  [Onayla ve Kaydet]           │
│                          │  [Yeniden Oluştur]            │
└──────────────────────────┴──────────────────────────────┘
```

**What-If Davranışı:**
- Admin sağ panelde bir etkinliğin tarihini değiştirir
- Sistem what-if API'sini çağırır
- Etkilenen etkinlikler sarı highlight olur
- Yeni kısıt ihlalleri kırmızı uyarı olarak gösterilir
- Kalite skoru güncellenir
- Sol takvim canlı güncellenir

### 3.6 Mevcut Sisteme Entegrasyon

**Değişmeyen yapılar:**
- `TermCalendarEvent` entity → veri deposu olarak aynen kalır
- `CalendarEventType` enum → yeni tipler eklenebilir (mevcut 25 korunur)
- `AcademicCalendarPage` → öğrenci/herkes takvimi görmek için aynen kalır
- `AcademicCalendarService` → mevcut okuma servisi aynen kalır
- `AcademicTerm` entity → tarih alanları generator tarafından da kullanılır

**Yeni eklenenler:**
- 4 yeni entity (CalendarTemplate, CalendarTemplateItem, HolidayDefinition, CalendarConstraint)
- 3 yeni servis (HolidayCalculatorService, CalendarGeneratorService, CalendarTemplateService)
- 1 yeni controller (CalendarGeneratorController)
- 1 yeni frontend sayfası (AcademicCalendarGeneratorPage)
- Migration: yeni tablolar
- Seed data: şablonlar + tatil tanımları + varsayılan kısıtlar

### 3.7 Seed Data

İlk kurulumda aşağıdaki veriler oluşturulur:

**Şablonlar (7 zincir, ~50 item):**

| Zincir | Etkinlik Sayısı | Örnek İçerik |
|--------|----------------|--------------|
| MAIN | ~15 | Ücret ödeme, kayıt, dersler, vize, final, bütünleme, not girişi/ilan |
| TRANSFER | ~5 | Yatay geçiş başvuru, değerlendirme, sonuç, asil kayıt, yedek kayıt |
| GRADUATE | ~4 | Lisansüstü başvuru, sonuç, ücret, kayıt |
| PREP | ~4 | Hazırlık muafiyet, ara sınav, final, atlama sınavı |
| FINANCE | ~4 | Ücret ödeme, burs başvuru, burs sonuç, burs iade |
| DOUBLEMAJOR | ~3 | ÇAP/yandal başvuru, sonuç, kayıt |
| INTERNATIONAL | ~3 | Uluslararası öğrenci başvuru, sonuç, kayıt |
| SUMMER | ~6 | Yaz okulu ücret ödeme, kayıt, dersler, final, mazeret, not ilan |

**Tatil tanımları (9 kayıt):**

| Tatil | Tip | Süre |
|-------|-----|------|
| Yılbaşı | Fixed (1 Ocak) | 1 gün |
| Ulusal Egemenlik | Fixed (23 Nisan) | 1 gün |
| Emek Günü | Fixed (1 Mayıs) | 1 gün |
| Gençlik Bayramı | Fixed (19 Mayıs) | 1 gün |
| Demokrasi Günü | Fixed (15 Temmuz) | 1 gün |
| Zafer Bayramı | Fixed (30 Ağustos) | 1 gün |
| Cumhuriyet Bayramı | Fixed (28-29 Ekim) | 1.5 gün |
| Ramazan Bayramı | HijriCalculated (Şevval 1) | 3.5 gün |
| Kurban Bayramı | HijriCalculated (Zilhicce 10) | 4.5 gün |

**Varsayılan kısıtlar (YÖK kuralları, Hard):**

| Kısıt | Tip | Parametre |
|-------|-----|-----------|
| Minimum ders haftası | MinWeeks | {"minWeeks": 14} |
| Final minimum süre | Duration | {"days": 10} |
| Bütünleme finalden sonra | Dependency | {"afterEvent": "FinalExam", "minDays": 7} |
| Vize tercihi | Timing (Soft) | {"week": 7} |

### 3.8 CalendarEventType Enum Güncellemesi

Mevcut 25 tipe ek olarak gerekebilecek yeni tipler:

```csharp
// Mevcut tipler korunur (1-25)
// Yeni eklemeler:
ExcuseExamApplication = 26,    // Mazeret sınav başvurusu
ExcuseExam = 27,               // Mazeret sınavları
ExcuseExamResult = 28,         // Mazeret sınav sonuçları
Orientation = 29,              // Oryantasyon
SemesterBreak = 30,            // Yarıyıl tatili
BursApplication = 31,          // Burs başvurusu
BursResult = 32,               // Burs sonucu
CourseExemption = 33,          // Ders muafiyeti
PrepSchoolExam = 34,           // Hazırlık okulu sınavı
EarlyPayment = 35,             // Erken ödeme
RegistrationFreeze = 36,       // Kayıt dondurma son gün
SummerSchoolRegistration = 37, // Yaz okulu kayıt
MinorApplication = 38,         // Yandal başvurusu
```

## 4. Dosya Yapısı

### Backend

```
SIS.Domain/
  Entities/
    CalendarTemplate.cs
    CalendarTemplateItem.cs
    HolidayDefinition.cs
    CalendarConstraint.cs
  Enums/
    AnchorType.cs
    HolidayType.cs
    CalendarConstraintType.cs
    CalendarEventType.cs (güncelleme)

SIS.Application/
  CalendarGenerator/
    DTOs/
      GenerateCalendarRequestDto.cs
      GenerateCalendarResultDto.cs
      GeneratedCalendarEventDto.cs
      ConstraintViolationDto.cs
      WhatIfRequestDto.cs
      WhatIfResultDto.cs
      CalendarTemplateDto.cs
      HolidayDefinitionDto.cs
      CalendarConstraintDto.cs
    Services/
      ICalendarGeneratorService.cs
      IHolidayCalculatorService.cs
      ICalendarTemplateService.cs

SIS.Infrastructure/
  Services/
    CalendarGenerator/
      CalendarGeneratorService.cs
      HolidayCalculatorService.cs
      CalendarTemplateService.cs
      HijriDateConverter.cs
      EventPlacer.cs
      PropagationEngine.cs
      ConstraintChecker.cs
  Persistence/
    SeedData.CalendarTemplates.cs
    SeedData.Holidays.cs

SIS.API/
  Controllers/
    CalendarGeneratorController.cs
```

### Frontend

```
SIS.Web/src/
  pages/
    AcademicCalendarGeneratorPage.tsx
  components/
    calendar-generator/
      ConstraintPanel.tsx
      ConstraintFormDialog.tsx
      CalendarPreview.tsx
      EventListPanel.tsx
      EventChainGroup.tsx
      EventRow.tsx
      QualityScoreBar.tsx
      WhatIfHighlight.tsx
      SemesterTab.tsx
      HolidayBadge.tsx
  hooks/
    useCalendarGenerator.ts
  api/
    calendarGenerator.api.ts
  types/
    calendarGenerator.ts
```

## 5. Versiyonlama ve State Management

Takvim onaylanana kadar veritabanına YAZILMAZ:
- `POST /generate` → in-memory hesaplama → sonuç DTO olarak frontend'e döner
- Admin beğenmediyse "Yeniden Oluştur" → yeni DTO üretilir, önceki kaybolur (persist yok)
- Admin what-if ile düzenleme yapar → her düzenleme yeni DTO döner (yine in-memory)
- Admin "Onayla ve Kaydet" dediğinde → TermCalendarEvent'lere yazılır

Bu sayede versiyonlama gerekmez çünkü onaylanmamış takvim hiçbir zaman DB'de tutulmaz.
Frontend tarafında React state ile tutulan DTO üzerinde çalışılır.

## 6. Şablon Yönetimi UX Notu

Şablonlar (CalendarTemplateItem) teknik alanlar içerir (AnchorType, AnchorItemId, OffsetDays).
MVP'de şablon düzenleme UI'ı YOK — şablonlar seed data olarak gelir.

Admin'in etkileşim noktası şablon DEĞİL, KISITLAR:
- Kısıt UI'ı basit ve anlaşılır: "Vize 7. haftada olsun", "Final 10 gün sürsün"
- Şablon düzenleme ileri fazda eklenebilir (admin-friendly UX ile)

## 7. Kapsam Dışı (Bu Spec'te Yok)

- Ders programı (scheduling) motoru ile entegrasyon
- Birden fazla alternatif takvim üretme (tek en iyi + what-if ile düzenleme)
- Şablon düzenleme UI'ı (MVP'de seed data yeterli, ileri fazda eklenebilir)
- Takvim PDF export
- Öğrenci/öğretim üyesi takvim bildirimleri
- Takvim onay workflow (dekan onayı vb.)


