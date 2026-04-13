# Toplu Not Girişi (Excel Import) — Tasarım Dokümanı

**Tarih:** 2026-04-13
**Durum:** Onaylandı
**Modül:** Bulk Grade Import

---

## 1. Özet

Öğretim üyesinin Excel dosyasından toplu not girişi yapmasını sağlar. Mevcut tek tek not girişi (GradeEntryPage) korunur, yanına Excel şablon indirme ve dosya yükleme özelliği eklenir.

Akış: **Şablon İndir → Doldur → Yükle → Önizle → Onayla**

---

## 2. Mevcut Sistem Özeti (Değiştirilmeyecek)

```
Mevcut not girişi akışı:
  1. Öğretim üyesi GradeEntryPage'i açar
  2. Component seçer (Vize, Final, Ödev...)
  3. Her öğrencinin puanını tek tek girer (0-100)
  4. Kaydet → POST /api/instructor/section/{sectionId}/grades
     Payload: { componentId, grades: [{ studentId, score }] }
  5. Her component için ayrı kaydet çağrısı yapılır
  6. Tüm component'ler tamamlanınca → Kesinleştir

Mevcut validasyonlar:
  - Section instructor'a ait olmalı
  - Dönem durumu GradeEntry olmalı
  - Section finalize edilmemiş olmalı
  - Puan 0-100 arası
  - Öğrenci section'a kayıtlı olmalı (Active, PendingAdvisorApproval, Completed)
  - Mevcut puanlar üzerine yazılabilir (update)
```

**Bu akış aynen korunacak.** Excel import ek bir giriş yöntemi, mevcut sistemi değiştirmeyecek.

---

## 3. Akış

```
Adım 1: Şablon İndir
  Öğretim üyesi GradeEntryPage'de "Excel Şablonu İndir" butonuna tıklar
  → Sistem section'a özel Excel dosyası üretir:
  
  ┌─────────────┬──────────────┬──────────┬───────┬───────┬───────┐
  │ Öğrenci No  │ Ad Soyad     │ Durum    │ Vize  │ Final │ Ödev  │
  │ (salt okunur)│(salt okunur) │(salt ok.)│(boş)  │(boş)  │(boş)  │
  ├─────────────┼──────────────┼──────────┼───────┼───────┼───────┤
  │ 20210001    │ Ahmet Yılmaz │ Active   │       │       │       │
  │ 20210002    │ Ayşe Demir   │ Active   │       │       │       │
  │ 20210003    │ Mehmet Kaya  │ Active   │       │       │       │
  └─────────────┴──────────────┴──────────┴───────┴───────┴───────┘
  
  Dosya adı: CENG101_Section1_Notlar.xlsx
  
  Gizli metadata satırı (row 1, hidden):
    SectionId=42, ComponentIds=[1,2,3], ComponentNames=["Vize","Final","Ödev"]
    → Parse sırasında component eşleştirmesi için kullanılır
    → Hoca bu satırı görmez

Adım 2: Doldur
  Hoca Excel'i bilgisayarında açar
  Zaten elindeki notları boş sütunlara yazar
  Bazı öğrencilerin bazı bileşenlerini boş bırakabilir
  Kaydet, kapat

Adım 3: Yükle
  GradeEntryPage'de "Excel Yükle" butonuna tıklar
  Dosya seçer (drag & drop veya file picker)
  → Dosya backend'e gönderilir
  → Backend parse eder, validasyon yapar
  → Önizleme döner

Adım 4: Önizle
  Önizleme dialog'u açılır:
  
  ┌─────────────────────────────────────────────────────────────┐
  │  Excel Import Önizlemesi                                    │
  │                                                             │
  │  Dosya: CENG101_Section1_Notlar.xlsx                        │
  │  Bulunan: 30 öğrenci, 3 bileşen, 78 not                    │
  │  Boş bırakılan: 12 hücre                                   │
  │                                                             │
  │  ⚠ 2 Uyarı  ✗ 1 Hata                                      │
  │                                                             │
  │  ┌───────────┬────────────┬──────┬───────┬───────┬────────┐ │
  │  │ Öğrenci No│ Ad Soyad   │ Vize │ Final │ Ödev  │ Durum  │ │
  │  ├───────────┼────────────┼──────┼───────┼───────┼────────┤ │
  │  │ 20210001  │ Ahmet Yılmaz│  75 │   82  │   90  │ ✓      │ │
  │  │ 20210002  │ Ayşe Demir │  68 │   —   │   85  │ ✓      │ │
  │  │ 20210003  │ Mehmet Kaya │ 105 │   70  │   60  │ ✗ Vize │ │
  │  │           │            │     │       │       │ 0-100  │ │
  │  │           │            │     │       │       │ olmalı │ │
  │  │ 20210099  │ ???        │  80 │   75  │   65  │ ⚠ Bu   │ │
  │  │           │            │     │       │       │ öğrenci│ │
  │  │           │            │     │       │       │ kayıtlı│ │
  │  │           │            │     │       │       │ değil  │ │
  │  └───────────┴────────────┴──────┴───────┴───────┴────────┘ │
  │                                                             │
  │  Mevcut notları olan 5 öğrencinin notları güncellenecek     │
  │                                                             │
  │  [İptal]                    [Hataları Atla ve Kaydet]       │
  └─────────────────────────────────────────────────────────────┘

Adım 5: Onayla
  Hoca "Hataları Atla ve Kaydet" der
  → Geçerli satırlar component bazında gruplanır
  → Her component için mevcut SubmitGrades endpoint'i çağrılır
  → Sonuç: "78 not kaydedildi, 2 satır atlandı (1 hata, 1 kayıtsız öğrenci)"
```

---

## 4. Backend Tasarımı

### 4.1 Yeni Endpoint'ler

**GradeController'a eklenir** (`/api/instructor/section/{sectionId}/...`)

| Method | Route | Açıklama |
|--------|-------|----------|
| GET | `/grades/export-template` | Section'a özel Excel şablonu üret ve indir |
| POST | `/grades/import-preview` | Excel dosyasını parse et, önizleme döndür |
| POST | `/grades/import-apply` | Onaylanan notları kaydet |

### 4.2 Şablon Üretme (export-template)

```
Girdi: sectionId (URL'den)

İşlem:
  1. Section'ı doğrula (instructor'a ait, finalize edilmemiş)
  2. Section'a kayıtlı öğrencileri çek (Active, PendingAdvisorApproval, Completed)
  3. Bu ders+dönem için tanımlı component'leri çek (sıralı)
  4. Excel dosyası oluştur:
     - Row 0 (gizli): metadata JSON → {"sectionId":42,"components":[{"id":1,"name":"Vize"},{"id":2,"name":"Final"}]}
     - Row 1: başlık satırı → Öğrenci No | Ad Soyad | Durum | [Component adları...]
     - Row 2+: öğrenci satırları → studentNumber | firstName lastName | status | [boş hücreler]
  5. Mevcut notlar varsa hücrelere doldur (güncelleme senaryosu)
  6. Öğrenci No, Ad Soyad, Durum sütunlarını "korumalı" yap (değiştirilemez)
  7. Content-Disposition header ile dosya adı: {CourseCode}_Section{No}_Notlar.xlsx

Çıktı: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
```

### 4.3 Import Preview (import-preview)

```
Girdi: 
  - sectionId (URL'den)
  - IFormFile file (multipart/form-data)

İşlem:
  1. Section doğrula (instructor, GradeEntry durumu, finalize edilmemiş)
  2. Dosya validasyonu:
     - .xlsx uzantısı kontrolü
     - Maksimum dosya boyutu: 5MB
     - Excel açılabilir mi
  3. Metadata satırını oku (row 0):
     - sectionId eşleşmesi kontrol (farklı section'ın şablonu yüklenmemeli)
     - Component eşleştirmesi
  4. Metadata yoksa (hoca kendi Excel'ini yüklediyse):
     - Başlık satırından component adlarını bulmaya çalış
     - Öğrenci No sütununu bulmaya çalış
     - Eşleşemezse hata döndür: "Lütfen sistemden indirilen şablonu kullanın"
  5. Her satır için validasyon:
     - Öğrenci No → studentId eşleştirmesi (section'a kayıtlı mı?)
     - Her not hücresi: 
       - Boş → atla (sorun değil)
       - Sayı ve 0-100 arası → geçerli
       - Sayı ama 0-100 dışı → hata
       - Sayı değil (metin, formül) → hata
  6. Mevcut notlarla karşılaştırma:
     - Öğrencinin bu component'te zaten notu varsa → "güncelleme" işareti

Çıktı: ImportPreviewResult
```

### 4.4 Import Apply (import-apply)

```
Girdi:
  - sectionId (URL'den)
  - ImportApplyRequest (onaylanan satırlar)

İşlem:
  1. Section doğrulamaları tekrar (GradeEntry, finalize değil, instructor)
  2. Onaylanan notları component bazında grupla
  3. Her component grubu için mevcut SubmitGrades mantığını çağır:
     → Aynı validasyonlar, aynı save işlemi
     → Mevcut kodu tekrar yazmak YOK, dahili çağrı
  4. Başarılı/başarısız sayıları topla

Çıktı: ImportApplyResult
  ├── TotalProcessed: int
  ├── SuccessCount: int
  ├── SkippedCount: int (hatalı veya kayıtsız öğrenci)
  ├── UpdatedCount: int (üzerine yazılan mevcut notlar)
  └── Errors: List<ImportError> (başarısız satırlar ve sebepleri)
```

### 4.5 DTO'lar

```csharp
// Şablon metadata (Excel'in gizli satırına yazılır)
public record TemplateMetadata(
    int SectionId,
    List<TemplateComponent> Components
);

public record TemplateComponent(
    int Id,
    string Name,
    decimal Weight
);

// Preview isteği → dosya multipart olarak gelir, ek DTO yok

// Preview sonucu
public record ImportPreviewResult(
    string FileName,
    int TotalStudents,
    int TotalComponents,
    int TotalGrades,         // dolu hücre sayısı
    int EmptyCells,          // boş bırakılan hücre sayısı
    int ErrorCount,
    int WarningCount,
    int UpdateCount,         // mevcut notu güncelleyecek sayı
    List<ImportPreviewRow> Rows
);

public record ImportPreviewRow(
    int RowNumber,
    string StudentNumber,
    string StudentName,
    int? StudentId,          // null ise eşleşemedi
    RowStatus Status,        // Valid, Error, Warning
    string? StatusMessage,
    List<ImportPreviewCell> Cells
);

public record ImportPreviewCell(
    int ComponentId,
    string ComponentName,
    decimal? Score,          // null ise boş bırakılmış
    CellStatus Status,       // Valid, Empty, Error, Update
    string? ErrorMessage,
    decimal? ExistingScore   // güncelleme ise mevcut not
);

public enum RowStatus { Valid, Error, Warning }
public enum CellStatus { Valid, Empty, Error, Update }

// Apply isteği
public record ImportApplyRequest(
    List<ImportApplyRow> Rows
);

public record ImportApplyRow(
    int StudentId,
    List<ImportApplyScore> Scores
);

public record ImportApplyScore(
    int ComponentId,
    decimal Score
);

// Apply sonucu
public record ImportApplyResult(
    int TotalProcessed,
    int SuccessCount,
    int SkippedCount,
    int UpdatedCount,
    List<ImportError> Errors
);

public record ImportError(
    int RowNumber,
    string StudentNumber,
    string ComponentName,
    string ErrorMessage
);
```

### 4.6 Excel Kütüphanesi

**ClosedXML** (MIT lisans, .NET native, dependency az)

```xml
<!-- SIS.API.csproj veya SIS.Infrastructure.csproj -->
<PackageReference Include="ClosedXML" Version="0.104.*" />
```

Neden ClosedXML:
- MIT lisans (EPPlus artık ticari lisans)
- .NET 10 uyumlu
- Basit API, öğrenme eğrisi düşük
- Sadece .xlsx formatı (eski .xls desteklemiyor — sorun değil)

### 4.7 Servis Yapısı

```csharp
public interface IGradeImportService
{
    /// <summary>
    /// Section'a özel Excel şablonu üretir.
    /// Mevcut notlar varsa hücrelere doldurur.
    /// </summary>
    Task<byte[]> GenerateTemplateAsync(int sectionId);
    
    /// <summary>
    /// Yüklenen Excel dosyasını parse eder ve önizleme döndürür.
    /// Dosyayı kaydetmez, sadece analiz eder.
    /// </summary>
    Task<ImportPreviewResult> PreviewImportAsync(int sectionId, Stream fileStream, string fileName);
    
    /// <summary>
    /// Onaylanan notları mevcut grade submission mantığı üzerinden kaydeder.
    /// Her component için ayrı SubmitGrades çağrısı yapar.
    /// </summary>
    Task<ImportApplyResult> ApplyImportAsync(int sectionId, ImportApplyRequest request, int instructorUserId);
}
```

**ApplyImportAsync iç implementasyonu:**
```
Mevcut GradeController.SubmitGrades mantığını DOĞRUDAN kullanır.
Yeni validasyon yazmaz. Notları component bazında gruplar ve
mevcut kayıt mantığını çağırır:

  rows.GroupBy(r => componentId)
    .ForEach(group => {
        // Mevcut SubmitGrades ile aynı iş:
        // StudentGrade entity oluştur/güncelle, SaveChanges
    });
```

---

## 5. Frontend Tasarımı

### 5.1 Mevcut GradeEntryPage'e Eklenen Butonlar

```
Mevcut sayfa yapısı korunur. Sadece üst toolbar'a 2 buton eklenir:

┌─────────────────────────────────────────────────────────────┐
│ CENG101 - Section 1  │  Dönem: 2026 Güz                    │
│                                                              │
│ [Excel Şablonu İndir ⬇]  [Excel Yükle ⬆]  [Kesinleştir ✓] │
│                                                              │
│ ┌────────┬────────┬────────┐                                │
│ │  Vize  │ Final  │  Ödev  │  ← mevcut component tab'ları  │
│ └────────┴────────┴────────┘                                │
│                                                              │
│  (mevcut not giriş tablosu aynen devam eder)                │
└─────────────────────────────────────────────────────────────┘
```

### 5.2 Excel Şablonu İndir

```
Buton: "Excel Şablonu İndir"
İkon: Download (lucide-react)
Tıklanınca:
  → GET /api/instructor/section/{sectionId}/grades/export-template
  → Blob olarak indirilir (mevcut download.ts utility kullanılır)
  → Dosya adı header'dan alınır
  
Disabled koşulları:
  - Component tanımlı değilse (bileşen yokken şablon anlamsız)
  - Section finalize edilmişse
```

### 5.3 Excel Yükle Dialog

```
Buton: "Excel Yükle"
İkon: Upload (lucide-react)
Tıklanınca → ImportDialog açılır

Disabled koşulları:
  - Component tanımlı değilse
  - Section finalize edilmişse
  - Dönem GradeEntry durumunda değilse

┌─────────────────────────────────────────────────────────────┐
│  Excel Not Yükleme                                    [✕]  │
│                                                             │
│  ┌─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┐   │
│  │                                                     │   │
│  │   📄 Excel dosyanızı buraya sürükleyin              │   │
│  │      veya [Dosya Seç] tıklayın                      │   │
│  │                                                     │   │
│  │   Kabul edilen format: .xlsx (max 5MB)              │   │
│  └─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┘   │
│                                                             │
│  (dosya seçildikten sonra → preview yükleniyor...)          │
└─────────────────────────────────────────────────────────────┘
```

Dosya seçilince:
```
→ POST /api/instructor/section/{sectionId}/grades/import-preview
  (multipart/form-data)
→ Loading spinner
→ Sonuç gelince → Preview tablosunu göster
```

### 5.4 Önizleme Tablosu

```
┌─────────────────────────────────────────────────────────────┐
│  Import Önizlemesi                                    [✕]  │
│                                                             │
│  📊 30 öğrenci  │  3 bileşen  │  78 not  │  12 boş         │
│  ✓ 27 geçerli   │  ⚠ 2 uyarı  │  ✗ 1 hata                 │
│  ↻ 5 güncelleme (mevcut notların üzerine yazılacak)        │
│                                                             │
│  Filtre: [Tümü ▾] [Sadece hatalar] [Sadece güncellemeler]  │
│                                                             │
│  ┌───────────┬─────────────┬───────┬───────┬───────┬─────┐ │
│  │Öğrenci No │ Ad Soyad    │ Vize  │ Final │ Ödev  │Durum│ │
│  ├───────────┼─────────────┼───────┼───────┼───────┼─────┤ │
│  │ 20210001  │ Ahmet Yılmaz│  75   │  82   │  90   │  ✓  │ │
│  │ 20210002  │ Ayşe Demir  │  68   │  —    │  85   │  ✓  │ │
│  │ 20210005  │ Can Özkan   │ 72→80 │  65   │  —    │  ↻  │ │
│  │           │             │(güncl)│       │       │     │ │
│  │ 20210003  │ Mehmet Kaya │ 105   │  70   │  60   │  ✗  │ │
│  │           │             │ ↑0-100│       │       │     │ │
│  │ 20210099  │ (bulunamadı)│  80   │  75   │  65   │  ⚠  │ │
│  │           │             │       │       │       │kayıt│ │
│  │           │             │       │       │       │ yok │ │
│  └───────────┴─────────────┴───────┴───────┴───────┴─────┘ │
│                                                             │
│  Hatalı ve uyarılı satırlar atlanacak.                     │
│  27 öğrencinin 73 notu kaydedilecek.                       │
│                                                             │
│  [İptal]                       [Onayla ve Kaydet (73 not)] │
└─────────────────────────────────────────────────────────────┘
```

**Renk kodları:**
- Geçerli hücre: normal
- Boş hücre: gri tire (—)
- Hatalı hücre: kırmızı arka plan + hata mesajı tooltip
- Güncelleme hücresi: sarı arka plan + "eskiNot→yeniNot" gösterimi
- Kayıtsız öğrenci satırı: turuncu arka plan

### 5.5 Sonuç Bildirimi

Onayladıktan sonra dialog kapanır, toast mesajı gösterilir:

```
✓ 73 not başarıyla kaydedildi (5 güncelleme, 3 satır atlandı)
```

Mevcut not girişi tablosu otomatik yenilenir (React Query invalidation).

---

## 6. Validasyon Kuralları

### 6.1 Dosya Validasyonları

| Kontrol | Hata Mesajı |
|---------|-------------|
| Uzantı .xlsx değil | "Sadece .xlsx formatı kabul edilir" |
| Boyut > 5MB | "Dosya boyutu 5MB'ı aşamaz" |
| Dosya açılamıyor | "Dosya okunamadı, bozuk olabilir" |
| Metadata satırı yok + başlık eşleşmiyor | "Lütfen sistemden indirilen şablonu kullanın" |
| SectionId uyuşmuyor | "Bu şablon farklı bir section için oluşturulmuş" |

### 6.2 Satır Validasyonları

| Kontrol | Durum | Davranış |
|---------|-------|----------|
| Öğrenci No boş | Hata | Satır atlanır |
| Öğrenci section'a kayıtlı değil | Uyarı | Satır atlanır |
| Öğrenci durumu Dropped/Draft | Uyarı | Satır atlanır |
| Tüm not hücreleri boş | — | Satır atlanır (sessizce) |

### 6.3 Hücre Validasyonları

| Kontrol | Durum | Davranış |
|---------|-------|----------|
| Hücre boş | Normal | Atlanır, mevcut not korunur |
| Değer sayı değil (metin, formül) | Hata | Hücre atlanır |
| Değer < 0 | Hata | Hücre atlanır |
| Değer > 100 | Hata | Hücre atlanır |
| Değer ondalıklı (75.5) | Geçerli | Kabul edilir (decimal) |
| Mevcut not var, yeni değer farklı | Güncelleme | Sarı işaret, üzerine yazılacak |
| Mevcut not var, aynı değer | Geçerli | Normal işlem (idempotent) |

---

## 7. Güvenlik

- Şablon indirme ve import sadece section'ın instructor'ına açık
- Section finalize edildiyse tüm import işlemleri reddedilir
- Dönem GradeEntry durumunda değilse tüm import işlemleri reddedilir
- Yüklenen dosya disk'e yazılmaz, memory stream olarak işlenir
- Dosya boyutu 5MB ile sınırlı (DoS koruması)
- Metadata satırındaki sectionId ile URL'deki sectionId eşleşmesi zorunlu

---

## 8. Dosya Yapısı

### Backend

```
SIS.Infrastructure/
  Services/
    GradeImport/
      GradeImportService.cs         ← IGradeImportService implementasyonu
      ExcelTemplateGenerator.cs     ← ClosedXML ile şablon üretme
      ExcelParser.cs                ← Yüklenen Excel'i parse etme
      ImportValidator.cs            ← Satır/hücre validasyonları

SIS.Application/
  GradeImport/
    DTOs/
      ImportPreviewResultDto.cs     ← Preview sonucu
      ImportApplyRequestDto.cs      ← Onay isteği
      ImportApplyResultDto.cs       ← Onay sonucu
    Services/
      IGradeImportService.cs        ← Interface

SIS.API/
  Controllers/
    GradeController.cs              ← 3 yeni endpoint EKLENİR (mevcut controller)
```

### Frontend

```
SIS.Web/src/
  components/
    grade-import/
      ImportDialog.tsx              ← Ana dialog (yükleme + önizleme + onay)
      FileDropZone.tsx              ← Drag & drop dosya seçici
      PreviewTable.tsx              ← Önizleme tablosu
      PreviewSummaryBar.tsx         ← Özet bilgiler (sayılar, hatalar)
      CellStatusBadge.tsx           ← Hücre durum göstergesi
  hooks/
    useGradeImport.ts               ← import preview + apply hooks
  api/
    gradeImport.api.ts              ← API çağrıları (template download, preview, apply)
```

---

## 9. Mevcut Sisteme Entegrasyon

### 9.1 Değiştirilecek Dosyalar

| Dosya | Değişiklik | Risk |
|-------|-----------|------|
| `GradeController.cs` | 3 yeni endpoint eklenir | DÜŞÜK — mevcut endpoint'ler dokunulmaz |
| `GradeEntryPage.tsx` | 2 buton + ImportDialog import'u eklenir | DÜŞÜK — mevcut UI korunur |
| `AppDbContext.cs` | Değişiklik yok (yeni entity yok) | YOK |
| `Program.cs` | IGradeImportService DI kaydı eklenir | DÜŞÜK |
| `SIS.API.csproj` veya `SIS.Infrastructure.csproj` | ClosedXML NuGet paketi eklenir | DÜŞÜK |

### 9.2 Yeni Entity Yok

Bu özellik yeni veritabanı tablosu gerektirmez. Mevcut entity'ler kullanılır:
- `GradeComponent` — component bilgileri
- `StudentGrade` — not kayıtları
- `CourseRegistration` — öğrenci kayıt durumu
- `CourseSection` — section bilgileri

### 9.3 Çakışma Riski (Diğer Görevlerle)

```
Akademik Takvim görevi ile: SIFIR — farklı controller, farklı sayfa
Akıllı Ders Önerisi ile: SIFIR — farklı controller, farklı sayfa
PDF görevi ile: SIFIR — farklı controller, farklı sayfa
Senin admin sayfaların ile: SIFIR — GradeController sadece Instructor rolü
Haftalık Program + Şifre ile: SIFIR — farklı controller, farklı sayfa
```

---

## 10. Edge Case'ler

| Senaryo | Davranış |
|---------|----------|
| Hoca şablonu indirir, sonra yeni öğrenci eklenir | Preview'da "bu öğrenci Excel'de yok" uyarısı. Yeni öğrencinin notu boş kalır, elle girilir |
| Hoca şablonu indirir, sonra component eklenir/silinir | Metadata'daki component listesi uyuşmazsa uyarı: "Vize bileşeni artık mevcut değil, bu sütun atlanacak" |
| İki hoca aynı section'a aynı anda import yapar | Son yazan kazanır (mevcut davranış, tek tek girişte de böyle) |
| Hoca yanlış section'ın şablonunu yükler | Metadata sectionId kontrolü → "Bu şablon farklı bir section için oluşturulmuş" |
| Hoca kendi Excel'ini yükler (şablon kullanmadan) | Metadata yoksa başlık satırından eşleştirme denenir. Eşleşemezse: "Lütfen şablonu kullanın" |
| Excel'de fazla satır var (section'a kayıtlı olmayan) | Uyarı ile atlanır, diğerleri işlenir |
| Excel'de fazla sütun var | Bilinmeyen sütunlar sessizce atlanır |
| Section finalize edildikten sonra import denenir | Hata: "Bu section kesinleştirilmiş, not girişi yapılamaz" |

---

## 11. Kapsam Dışı

- CSV format desteği (sadece .xlsx)
- Eski .xls format desteği
- Birden fazla section'ı tek Excel'de import etme
- Excel'den component oluşturma (component'ler önceden tanımlı olmalı)
- Finalize işlemini Excel'den tetikleme
- Import geçmişi/log tutma (audit log mevcut sistemde zaten var)
