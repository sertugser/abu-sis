# Fakülte Planlama Sistemi — Tasarım Dokümanı

**Tarih:** 2026-03-25
**Durum:** Onaylandı
**Kapsam:** İki aşamalı planlama mimarisi — bölüm bazlı şube onayı + fakülte geneli derslik/saat ataması

---

## 1. Problem

Mevcut sistemde her bölüm kendi şubelerini bağımsız planlıyor. Derslikler ve ders saatleri üniversite genelinde paylaşılan kaynaklar olmasına rağmen, çakışma kontrolü sadece bölüm bazlı yapılıyor. Bilgisayar Mühendisliği ve Yazılım Mühendisliği aynı dersliği aynı saatte planlarsa, hiçbiri çakışma görmüyor.

## 2. Çözüm

İki aşamalı planlama:

- **Aşama 1 (Şube Onayı):** Bölüm başkanı hangi dersten kaç şube açılacağına karar verir. Derslik/saat ataması yapılmaz. Mevcut Planlama Merkezi bu işi zaten yapıyor — değişmez.
- **Aşama 2 (Derslik & Saat Ataması):** Yeni "Fakülte Planlama" sayfasında, fakülte genelindeki tüm şubeler bir arada görülür. Hibrit yaklaşım: önce otomatik algoritma atar, sonra yetkili kişi elle düzeltir.

## 3. Planlama Yaşam Döngüsü

```
Aşama 1: ŞUBE ONAYI          Aşama 2: ZAMANLAMA           Aşama 3: KESİNLEŞME
(Planlama Merkezi)            (Fakülte Planlama)           (IsFinalized=true)
Bölüm başkanı karar verir    Otomatik + elle atama        Kayıt dönemi açılır
CourseSection oluşur          Derslik/saat/hoca atanır     Değişiklik kilitlenir
(Derslik=NULL, Hoca=NULL)     Çakışma kontrolü yapılır
```

Aşama 2'ye geçiş esnek: bir bölüm onaylarını bitirdiğinde o bölümün şubeleri atamaya hazır olur. Henüz onay tamamlanmamış bölümler varsa sistem uyarı gösterir.

## 4. Sayfa Sorumlulukları

| Sayfa | Rolü | Değişiklik |
|-------|------|-----------|
| Planlama Merkezi | Şube onayı (Aşama 1) | Değişmez |
| Fakülte Planlama | Derslik/saat ataması (Aşama 2) | **YENİ SAYFA** |
| Merkezi Derslik Yönetimi | Dashboard/izleme | Değişmez |
| Program Planlama | Hoca müsaitliği + ders programı görünümü | Derslik ataması kaldırılır |
| Toplu Programlama | — | **KALDIRILIR** (işlevi Fakülte Planlama'ya taşınır) |

## 5. Fakülte Planlama Sayfası — UI Tasarımı

**Route:** `/fakulte-planlama`
**Sidebar:** Bölüm İşlemleri → "Fakülte Planlama"
**Roller:** DepartmentManager, AdministrativeStaff, SystemAdministrator

### Tab 1: Genel Bakış

- İlerleme çubuğu: "23/146 şube zamanlandı (%16)"
- Bölüm bazlı ilerleme kartları (her bölüm için zamanlanmış/toplam şube)
- Onay durumu uyarısı: henüz şube onayını tamamlamamış bölümler listelenir
- Çakışma özeti: türe göre çakışma sayıları
- Son işlemler listesi (audit trail): kim, ne zaman, hangi şubeye ne atadı

### Tab 2: Şube Listesi (Atama)

- Filtrelenebilir tablo:
  - Filtreler: Bölüm, Durum (Zamanlanmamış / Zamanlanmış / Çakışmalı / Kısmi), Ders tipi
  - Sütunlar: Ders Kodu, Ders Adı, Bölüm, Kapasite, Derslik, Saat, Hoca, Durum
- Durum badge'leri:
  - Zamanlanmamış — derslik/saat atanmamış
  - Zamanlanmış — atama yapılmış, çakışma yok
  - Çakışmalı — atama var ama çakışma tespit edilmiş
  - Kısmi — hoca var ama derslik yok, veya tersi
- Satıra tıklayınca atama paneli (form) açılır:
  - Derslik seçimi: uygun derslikler filtrelenir (tip uyumu + kapasite uyumu + müsaitlik)
  - Gün + saat seçimi: çakışma varsa kırmızı uyarı gösterilir
  - Hoca seçimi: opsiyonel, bölümün hocaları listelenir
  - "Kaydet" butonu: çakışma varsa onay ister
  - "Atamayı Kaldır" butonu: mevcut atamayı geri alır

### Tab 3: Otomatik Zamanlama

- Kapsam seçimi:
  - Tüm fakülte (tüm zamanlanmamış şubeler)
  - Tek bölüm
  - Seçili şubeler
- "Otomatik Ata" butonu → algoritma çalışır → önizleme tablosu gösterilir:
  - Her satırda: Ders, Önerilen Derslik, Önerilen Saat, Uyum skoru
  - Satır bazında onaylama/değiştirme imkanı
  - "Tümünü Uygula" ve "Seçilenleri Uygula" butonları
- İstatistik: başarılı/başarısız atama sayıları
- Atanamayan şubeler için neden açıklaması: "Lab dersliği yok", "Hoca müsait değil" vb.

### Tab 4: Çakışma Analizi

- Fakülte geneli tüm çakışmalar (bölümler arası dahil)
- Çakışma türüne göre gruplama: Derslik, Hoca, Kapasite, Müsaitlik
- Her çakışmada: hangi 2 şube çakışıyor, neden, çözüm önerisi
- "Çöz" butonu: ilgili şubenin atama formunu açar (Tab 2'ye yönlendirir)

## 6. Backend API

### Yeni Endpoint'ler

```
GET    /api/faculty-planning/overview?facultyId=X&termId=Y
       Response: FacultyPlanningOverviewDto
       → İlerleme, bölüm bazlı durum, çakışma özeti, son işlemler

GET    /api/faculty-planning/sections?facultyId=X&termId=Y&departmentId=&status=
       Response: FacultyPlanningSectionDto[]
       → Filtrelenebilir şube listesi (tüm bölümler veya tek bölüm)

POST   /api/faculty-planning/assign
       Request: { sectionId, classroomId?, scheduleSlots[]?, instructorId? }
       Response: AssignmentResultDto
       → Tek şubeye derslik/saat/hoca ata (kısmi atama desteklenir, çakışma kontrolü dahil)

POST   /api/faculty-planning/auto-schedule
       Request: { facultyId, termId, departmentId?, sectionIds[]? }
       Response: AutoSchedulePreviewDto
       → Otomatik atama çalıştır, önizleme döndür (kaydetmez). Hoca atanmamış şubeler de dahil edilir (bkz. §13.4)

POST   /api/faculty-planning/apply-schedule
       Request: { assignments: [{ sectionId, classroomId, scheduleSlots[], instructorId? }] }
       Response: ApplyScheduleResultDto
       → Önizlenen atamaları toplu kaydet

DELETE /api/faculty-planning/unassign/{sectionId}
       Response: 204 No Content
       → Şubeden derslik/saat atamasını kaldır

GET    /api/faculty-planning/available-classrooms?termId=Y&day=&startTime=&endTime=&minCapacity=&type=
       Response: AvailableClassroomDto[]
       → Belirli zaman diliminde müsait derslikleri getir

GET    /api/faculty-planning/conflicts?facultyId=X&termId=Y
       Response: SchedulingConflictDto[]
       → Fakülte geneli çakışma analizi (departmentId filtresi YOK)
```

### DTO'lar

```csharp
// Genel bakış
public class FacultyPlanningOverviewDto
{
    public int FacultyId { get; set; }
    public string FacultyName { get; set; }
    public int TermId { get; set; }
    public string TermName { get; set; }
    public int TotalSections { get; set; }
    public int ScheduledSections { get; set; }
    public int UnscheduledSections { get; set; }
    public int ConflictCount { get; set; }
    public List<DepartmentProgressDto> DepartmentProgress { get; set; }
    public List<PendingApprovalWarningDto> PendingApprovalWarnings { get; set; }
    public List<RecentAssignmentDto> RecentAssignments { get; set; }
}

public class DepartmentProgressDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; }
    public string DepartmentCode { get; set; }
    public int TotalSections { get; set; }
    public int ScheduledSections { get; set; }
    public int PendingApprovals { get; set; }
}

public class PendingApprovalWarningDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; }
    public int PendingCount { get; set; }
}

public class RecentAssignmentDto
{
    public int SectionId { get; set; }
    public string CourseCode { get; set; }
    public string ClassroomName { get; set; }
    public string ScheduleSummary { get; set; }
    public string AssignedByUserName { get; set; }
    public DateTime AssignedAt { get; set; }
}

// Şube listesi
public class FacultyPlanningSectionDto
{
    public int SectionId { get; set; }
    public int CourseId { get; set; }
    public string CourseCode { get; set; }
    public string CourseName { get; set; }
    public int DepartmentId { get; set; }
    public string DepartmentCode { get; set; }
    public int Capacity { get; set; }
    public int? ClassroomId { get; set; }
    public string? ClassroomName { get; set; }
    public string? ClassroomType { get; set; }
    public int? InstructorId { get; set; }
    public string? InstructorName { get; set; }
    public string ScheduleJson { get; set; }
    public string ScheduleSummary { get; set; }
    public string Status { get; set; } // Unscheduled, Scheduled, Conflicted, Partial
    public int TheoryHours { get; set; }
    public int LabHours { get; set; }
}

// Atama (kısmi atama desteklenir — bkz. §12.3)
public class AssignSectionRequest
{
    public int SectionId { get; set; }
    public int? ClassroomId { get; set; }                  // nullable — sadece hoca atanabilir
    public List<ScheduleSlotDto>? ScheduleSlots { get; set; } // nullable — derslik yoksa slot da yok
    public int? InstructorId { get; set; }
}

public class AssignmentResultDto
{
    public bool Success { get; set; }
    public List<string> Warnings { get; set; } // tip uyumsuzluğu, kapasite uyarısı vb.
    public List<SchedulingConflictDto> Conflicts { get; set; } // varsa
}

// Otomatik zamanlama önizleme
public class AutoSchedulePreviewDto
{
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<AutoSchedulePreviewItemDto> Assignments { get; set; }
    public List<AutoScheduleFailureDto> Failures { get; set; }
}

public class AutoSchedulePreviewItemDto
{
    public int SectionId { get; set; }
    public string CourseCode { get; set; }
    public string CourseName { get; set; }
    public string DepartmentCode { get; set; }
    public int ClassroomId { get; set; }
    public string ClassroomName { get; set; }
    public List<ScheduleSlotDto> ScheduleSlots { get; set; }
    public string ScheduleSummary { get; set; }
    public int? InstructorId { get; set; }
    public string? InstructorName { get; set; }
    public decimal FitScore { get; set; } // 0-1, derslik tip+kapasite uyumu
}

public class AutoScheduleFailureDto
{
    public int SectionId { get; set; }
    public string CourseCode { get; set; }
    public string CourseName { get; set; }
    public string Reason { get; set; } // "Lab dersliği yok", "Hoca müsait değil" vb.
}

// Müsait derslik
public class AvailableClassroomDto
{
    public int ClassroomId { get; set; }
    public string Name { get; set; }
    public int Capacity { get; set; }
    public string Type { get; set; }
    public decimal FitScore { get; set; } // kapasite + tip uyumu skoru
}
```

## 7. Otomatik Zamanlama Algoritması

Mevcut `batch-auto-schedule` bölüm bazlı çalışıyor. Yeni algoritma fakülte genelinde çalışır.

### Öncelik Sıralaması

Şubeler şu sırayla işlenir (kısıtlı kaynaklar önce yerleşsin):

1. Lab/Computer dersleri > Lecture/Seminar (kısıtlı derslik)
2. Zorunlu dersler > Seçmeli dersler
3. Büyük kapasiteli şubeler > Küçük (büyük derslik az, önce yerleşsin)

### Derslik Seçim Sırası

Her şube için uygun derslikler şu öncelikle sıralanır:

1. **Tip uyumu:** Lab dersi → Lab dersliği (tam eşleşme en yüksek puan)
2. **Kapasite uyumu:** Şube kapasitesine en yakın derslik (fazla boş koltuk = düşük puan)
3. Tip uyumsuz ama kapasite uygun derslikler de kabul edilir (uyarı ile)

### Slot Seçimi

1. Hoca müsaitliği varsa → tercih edilen slotlar önce
2. Ders saatleri: TheoryHours + LabHours'a göre slot uzunluğu belirlenir
3. Çakışma kontrolü: aynı derslik+saat veya aynı hoca+saat → geç

### Algoritma Akışı

```
1. Zamanlanmamış şubeleri getir (fakülte geneli veya seçili kapsam)
2. Öncelik sırala (yukarıdaki kurallara göre)
3. Her şube için:
   a. Dersin tip gereksinimini belirle (TheoryHours>0 → Lecture, LabHours>0 → Lab)
   b. Uygun derslikleri filtrele ve sırala (tip + kapasite)
   c. Hoca müsaitliğini kontrol et (varsa)
   d. Her derslik için uygun zaman slotu ara
   e. Çakışma kontrolü yap (tüm fakülte geneli)
   f. İlk uygun kombinasyonu seç → önizlemeye ekle
   g. Bulunamazsa → failures listesine neden ile ekle
4. Önizleme olarak döndür (henüz kaydetme)
```

## 8. Yetkilendirme

| Endpoint | DepartmentManager | AdministrativeStaff | SystemAdmin |
|----------|:-:|:-:|:-:|
| Overview (read) | Kendi fakültesi | Tümü | Tümü |
| Section list (read) | Kendi fakültesi | Tümü | Tümü |
| Assign (write) | Sadece kendi bölüm şubeleri | Tümü | Tümü |
| Auto-schedule (write) | Sadece kendi bölüm | Tümü | Tümü |
| Apply schedule (write) | Sadece kendi bölüm şubeleri | Tümü | Tümü |
| Unassign (write) | Sadece kendi bölüm şubeleri | Tümü | Tümü |
| Available classrooms (read) | Fakülte geneli | Tümü | Tümü |
| Conflicts (read) | Kendi fakültesi | Tümü | Tümü |

DepartmentManager: Tüm fakültenin derslik havuzunu görür ama sadece kendi bölümünün şubelerine atama yapabilir.

## 9. Çakışma Kontrolü Değişikliği

Mevcut `SchedulingService.ValidateExistingByDepartmentAsync` departmentId ile filtreler. Yeni yaklaşım:

- `GET /faculty-planning/conflicts` → `BuildTermSnapshotAsync(termId, null, null)` çağrılır (departmentId=null, tüm sistem)
- Sonuçlar fakülteye göre filtrelenir (Course.DepartmentId → Department.FacultyId)
- Bölümler arası derslik çakışmaları artık görünür hale gelir

## 10. Edge Case'ler

| Durum | Davranış |
|-------|---------|
| Şube onayı geri alınırsa (re-reject) | Atanmış şubeler silinir, Fakülte Planlama otomatik güncellenir |
| Otomatik atama slot bulamazsa | Neden açıklaması ile "Atanamadı" listesine eklenir |
| DeptManager başka bölümün şubesine atamaya çalışırsa | 403 Forbidden |
| Aynı dersliğe 2 kişi eşzamanlı atama yaparsa | Backend'de SaveChanges öncesi çakışma kontrolü, ikinci istek reddedilir |
| Hoca müsaitliği tanımlı değilse | Atama yapılabilir, uyarı gösterilir |
| Lab dersi var ama Lab dersliği yoksa | Lecture sınıfına atanabilir, "Derslik tipi uyumsuz" uyarısı |
| IsFinalized=true olan şubeye atama | Reddedilir, "Bu şube kesinleşmiş, değişiklik yapılamaz" |

## 11. Mevcut Sayfalarda Değişiklikler

### Program Planlama (InstructorSchedulingPage)
- Derslik ataması kaldırılır (bu sorumluluk Fakülte Planlama'ya taşındı)
- Hoca müsaitlik yönetimi ve ders programı görünümü kalır
- Haftalık görünüm (timetable) kalır

### Toplu Programlama (BatchSchedulingPage)
- Tamamen kaldırılır
- Route, sidebar menü girişi ve App.tsx kaydı silinir
- `batch-auto-schedule` endpoint'i kalır (Fakülte Planlama backend'i bu logic'i yeniden kullanır)

### Sidebar Menü
- "Bölüm İşlemleri" altına "Fakülte Planlama" eklenir
- "Toplu Programlama" kaldırılır

### Constants
- `ROUTES.FACULTY_PLANNING = '/fakulte-planlama'` eklenir
- `ROUTES.MANAGEMENT_BATCH_SCHEDULING` kaldırılır
- `IMPLEMENTED_ROUTES` güncellenir

## 12. Veri Modeli Değişiklikleri (Detay)

Spec review sonucu tespit edilen eksiklikler:

### 12.1 Audit Trail — Yeni Entity: `SchedulingAuditLog`

Tab 1 "Son İşlemler" paneli için kimin ne zaman hangi atamayı yaptığını kaydetmemiz gerekiyor. Mevcut `CourseSection`'da audit alanı yok.

```csharp
public class SchedulingAuditLog
{
    public int Id { get; set; }
    public int SectionId { get; set; }
    public CourseSection Section { get; set; } = null!;
    public string Action { get; set; } = string.Empty; // "Assign", "Unassign", "AutoSchedule"
    public string UserId { get; set; } = string.Empty;
    public int? ClassroomId { get; set; }
    public string? ScheduleJson { get; set; }
    public int? InstructorId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

Her `assign`, `unassign`, `apply-schedule` işleminde bir log kaydı oluşturulur. Overview tab'da son N log gösterilir.

### 12.2 Concurrency Control — `CourseSection`'a `RowVersion` eklenmesi

Eşzamanlı atama yarış koşulunu (TOCTOU) önlemek için `CourseSection`'a concurrency token eklenir:

```csharp
// CourseSection'a eklenir:
[Timestamp]
public byte[] RowVersion { get; set; }
```

PostgreSQL'de `xmin` system column kullanılır. EF Core `DbUpdateConcurrencyException` fırlatır, backend "Bu şube başka bir kullanıcı tarafından güncellendi, lütfen yenileyin" hatası döner.

### 12.3 `AssignSectionRequest` — Nullable alanlar

Kısmi atama desteği için (hoca var ama derslik yok, veya tersi):

```csharp
public class AssignSectionRequest
{
    public int SectionId { get; set; }
    public int? ClassroomId { get; set; }        // nullable — sadece hoca atanabilir
    public List<ScheduleSlotDto>? ScheduleSlots { get; set; } // nullable — derslik yoksa slot da yok
    public int? InstructorId { get; set; }        // nullable — sadece derslik atanabilir
}
```

Kural: ClassroomId veriliyorsa ScheduleSlots da zorunlu. InstructorId tek başına verilebilir.

### 12.4 `ScheduleSlot` ve `ScheduleSlotDto` Genişletmesi

Teori+Lab dersleri için slot bazında farklı derslik atayabilmek üzere (bkz. §13.3):

```csharp
// SIS.Domain/ValueObjects/ScheduleSlot.cs — yeni alan:
public int? ClassroomId { get; set; }  // null ise CourseSection.ClassroomId kullanılır

// SIS.Application ScheduleSlotDto — yeni alan:
public int? ClassroomId { get; set; }
public string? ClassroomName { get; set; } // read-only, response'da döner
```

Geriye uyumluluk: `ClassroomId = null` olan slotlar mevcut davranışı korur (tek derslik).

## 13. Algoritma Açıklamaları

### 13.1 Derslik Havuzu Kapsamı

Derslikler fakülteye bağlı DEĞİLDİR. Tüm üniversite derslikleri ortak havuzdadır. `available-classrooms` endpoint'i fakülte filtresi uygulamaz; yalnızca tip ve kapasite filtresi uygular. "Fakülte geneli derslik havuzu" ifadesi "tüm üniversite derslikleri" anlamındadır.

### 13.2 Zorunlu/Seçmeli Ders Belirleme

Bir ders, fakülte içindeki herhangi bir aktif müfredatta `CurriculumCourse.IsCompulsory = true` ise zorunlu sayılır. Birden fazla müfredatta farklı statüde olan dersler (bir programda zorunlu, diğerinde seçmeli) zorunlu olarak kabul edilir — kısıtlı kaynağa öncelik prensibi.

### 13.3 Teori + Lab Dersleri

Mevcut `CourseSection` tek `ClassroomId` tutar. `TheoryHours > 0` ve `LabHours > 0` olan dersler için:

- Teori ve lab **ayrı slot grupları** olarak `ScheduleJson`'da saklanır
- Her slot grubuna farklı derslik atanabilmesi için `ScheduleJson` formatı genişletilir:

```json
[
  { "day": 1, "startTime": "09:00", "endTime": "12:00", "classroomId": 5 },
  { "day": 3, "startTime": "14:00", "endTime": "16:00", "classroomId": 12 }
]
```

- `CourseSection.ClassroomId` birincil (teori) dersliği tutar, lab dersliği `ScheduleJson` içinde slot bazında belirtilir
- Otomatik algoritma: teori slotlarına Lecture dersliği, lab slotlarına Lab dersliği atar

### 13.4 Hoca Ataması ve Otomatik Zamanlama İlişkisi

Mevcut `SuggestAsync` hoca müsaitliğine bağımlı. Fakülte Planlama'da hoca atanmamış şubeler de zamanlanabilmeli. İki mod:

- **Hoca atanmış:** Mevcut mantık kullanılır — hoca müsaitliği slot seçimini etkiler
- **Hoca atanmamış:** Sadece derslik müsaitliği ve tip/kapasite uyumuna bakılır, hoca kısıtı yok sayılır. Uyarı: "Hoca atanmamış, müsaitlik kontrolü yapılamadı"

## 14. Fakülte Kapsamı Çözümleme

DepartmentManager'ın fakülte kapsamını belirlemek için:

```
User → Instructor (UserId ile) → Department (DepartmentId ile) → Faculty (FacultyId ile)
```

Yeni bir `ResolveFacultyForUserAsync` metodu `FacultyPlanningService`'e eklenir. Bu metod:
1. Kullanıcının Instructor kaydını bulur
2. Instructor.DepartmentId → Department.FacultyId zinciriyle fakülteyi çözümler
3. AdminStaff/SystemAdmin için tüm fakülteler erişilebilir

## 15. Ek Endpoint Parametreleri

### Pagination
`GET /faculty-planning/sections` endpoint'ine pagination eklenir:
- `page` (default: 1)
- `pageSize` (default: 50, max: 200)
- Response wrapper: `{ items: [...], totalCount: N, page: N, pageSize: N }`

### Term Doğrulama
`POST /faculty-planning/assign` ve `DELETE /faculty-planning/unassign` işlemlerinde backend section'ın term'ini doğrular:
- Term aktif mi?
- IsFinalized=false mu?
- Backend sectionId'den termId'yi türetir, ayrı parametre gerekmez

### Batch Authorization
`POST /faculty-planning/apply-schedule` batch isteğinde DepartmentManager yetkisi kontrol edilir:
- Her sectionId için yetki kontrolü yapılır
- Yetkisiz section'lar atlanır, sonuçta per-item hata listesi döner
- Tüm batch reddedilmez, kısmi başarı mümkündür

## 16. Çakışma Özeti (Overview DTO Güncellemesi)

`FacultyPlanningOverviewDto`'ya çakışma türü dağılımı eklenir:

```csharp
public Dictionary<string, int> ConflictCountByType { get; set; } = new();
// Örnek: { "ClassroomConflict": 3, "InstructorConflict": 1 }
```

Tab 1'de tek API çağrısıyla hem toplam hem tür bazlı çakışma gösterilebilir.

## 17. Şube Tanımlama

Aynı ders için birden fazla şube olduğunda ayırt edebilmek için `FacultyPlanningSectionDto`'ya eklenir:

```csharp
public int SectionIndex { get; set; } // 1, 2, 3... (aynı CourseId+TermId için sıra numarası)
```

UI'da: "CENG101 - Şube 1", "CENG101 - Şube 2" olarak gösterilir.
