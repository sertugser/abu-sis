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
Student, Instructor, AcademicAdvisor, AdministrativeStaff, DepartmentManager, Dean, SystemAdministrator

## Commands
```bash
dotnet build                          # Build solution
dotnet test                           # Run tests
dotnet ef database update -p SIS.Infrastructure -s SIS.API  # Apply migrations
```

---

## A. BİR KERELIK SİSTEM KURULUMU

Aşağıdaki tanımlamalar sistem ilk kurulurken bir kez yapılır, sonra gerektiğinde güncellenir.

### A1. Personel ve Kullanıcı Tanımlama
1. **SystemAdmin** personeli sisteme tanımlar (ad, unvan, bölüm, fakülte)
2. Personel kaydı **sistem kullanıcısına** dönüştürülür
3. Kullanıcıya roller atanır (Instructor, AcademicAdvisor, DepartmentManager, Dean vb.)
4. Hangi modülleri göreceği/yöneteceği belirlenir
5. Zamana bağlı yetkiler tanımlanabilir (ör: sadece Planning aşamasında ders açma yetkisi — dönem durumu değişince otomatik kapanır)

### A2. Fiziksel Kaynaklar
1. **SystemAdmin** derslikleri tanımlar: Ad, BuildingName, Floor, Kapasite, Tip (Normal/Lab/Amfi)
2. **SystemAdmin** sabit zaman grid'ini tanımlar (TimeSlot):
   - Pattern bazlı: MWF (3×50dk), TR (2×75dk), MW (2×75dk), SingleDay Lab (1×180dk)
   - DayOfWeek alanı YOKTUR — günler Pattern'dan türetilir (MWF=Pzt+Çar+Cum, TR=Sal+Per)
   - MWF slotları: 09:00-09:50, 10:00-10:50, 11:00-11:50, 13:00-13:50, 14:00-14:50, 15:00-15:50
   - TR slotları: 09:00-10:15, 10:30-11:45, 13:00-14:15, 14:30-15:45
   - Lab slotları: 3 saatlik bitişik bloklar (ör: 14:00-17:00)
3. Grid bir kere tanımlanır, tüm dönemler aynı slotları kullanır

### A3. Müfredat Tanımlama
1. Hiyerarşi: Fakülte → Bölüm → Program → Müfredat → Dersler
2. Ders bir kez merkezi havuzda tanımlanır (kod, ad, kredi, AKTS, teori/lab saati)
3. Aynı ders farklı müfredatlarda farklı statüyle bağlanır (zorunlu / seçmeli / teknik seçmeli / non-departmental)
4. **Müfredat versiyonlu** — aynı program için birden fazla aktif müfredat paralel çalışır. Öğrenci kayıt yılındaki müfredatına bağlı kalır. Yeni başlayanlar yeni müfredata, eski öğrenciler eskiye göre devam eder.
5. Müfredat geçiş kuralları tanımlanır — eski dersle yeni ders eşleştirmesi (ör: "Computer" → "Python")

### A4. Sınıf Atlatma Kuralları
1. **SystemAdmin** her program için sınıf geçiş kurallarını GUI'den tanımlar (hardcoded değil):
   - Sınıf bazlı minimum CGPA (ör: 2. sınıfa geçmek için min 1.80)
   - Sınıf bazlı minimum tamamlanan AKTS (ör: 2. sınıf için min 60 AKTS)
   - Sınıf bazlı minimum ders sayısı
2. Sistem bu kuralları dönem sonunda otomatik uygular

### A5. Sistem Konfigürasyonu
1. Menü/modül görünürlüğü ayarları
2. Genel sistem aç/kapat kontrolleri
3. Not giriş zaman penceresi kontrolü
4. Tüm konfigürasyon değişiklikleri **audit loglanır** ve ilgili rollere **bildirim** gider

### A6. Konfigüre Edilebilir İş Kuralları
Üniversiteye özel değerler admin panelinden değiştirilebilir (hardcoded değil):
1. **ECTS limitleri** — CGPA aralıklarına göre max AKTS (şu an: <2.00→30, 2.00-2.49→42, ≥2.50→48)
2. **Sınıfta kalma CGPA eşiği** — (şu an: 1.70)
3. **Ders çekilme limitleri** — lisans ve önlisans ayrı (şu an: lisans 6, önlisans 3)
4. **YÖK unvan → MaxWeeklyHours eşleştirmesi** — Profesör, Doçent, Dr.Öğr.Üyesi, Öğr.Gör. bazlı otomatik atama
5. **Derslik tipi eşikleri** — kaç kişiden sonra Amfi zorunlu (şu an: 100+)
6. **Derslik doluluk oranı hedefi** — soft constraint eşikleri (şu an: %60-85)

**YÖK standardı olup sabit kalan değerler (konfigüre edilmez):**
- Not-GPA eşleştirmesi (AA=4.00, BA=3.50, BB=3.00, CB=2.50, CC=2.00, DC=1.50, DD=1.00, FF=0.00)

---

## B. HER DÖNEM TEKRARLANAN WORKFLOW

Dönem state machine: `Draft → Planning → Scheduling → Published → RegistrationOpen → AddDrop → WithdrawalPeriod → GradeEntry → Closed → Archived`

Geçiş endpoint: `POST /api/admin/terms/{id}/transition { "targetStatus": "Planning" }`

### ADIM 1 — Dönem Oluşturma [Draft]
**Rol: SystemAdmin**
1. Yeni akademik dönem oluşturulur (ad, başlangıç/bitiş tarihleri, akademik takvim etkinlikleri)
2. Sistem önceki yılın takvimini baz alarak, resmi tatil ve bayramlara göre kaydırma yaparak takvim **önerir** (opsiyonel kabul)
3. Önceki dönemin programı incremental scheduling için **başlangıç noktası** olarak tutulur
4. Dönem durumu: **Draft**

### ADIM 2 — Derslik Atama [Draft → Planning geçişi öncesi]
**Rol: SystemAdmin**
1. Sistem, fakültelerin öğrenci yoğunluğu ve ders sayısına göre hangi dersliklerin hangi fakülteye atanması gerektiğini **önerir**
2. SystemAdmin öneriyi kabul eder veya düzenler
3. ClassroomTermAssignment kayıtları oluşur (TermId, ClassroomId, FacultyId)
4. Her fakülte yalnızca kendine atanmış dersliklerden seçebilir

### ADIM 3 — Planning'e Geçiş [Draft → Planning]
**Rol: SystemAdmin**
1. SystemAdmin dönem durumunu **Planning**'e geçirir
2. Guard kontrolü: Takvim tarihleri girilmiş mi? Derslik atamaları yapılmış mı?
3. **Bu andan itibaren bölümler ders açabilir**

### ADIM 4 — Ders Açma ve Hoca Atama [Planning]
**Rol: DepartmentManager / Dean**
1. Sistem her ders için **enrollment forecast** gösterir:
   - Müfredatta henüz almamış öğrenci sayısı
   - Alttan alan (FF/NA) öğrenci sayısı
   - Geçen dönem kayıt sayısı
   - → Önerilen şube sayısı ve kontenjan
2. DepartmentManager şubeleri açar: ders kodu, şube sayısı, kontenjan, ders tipi (Lecture/Lab/Recitation)
3. Her şubeye **öğretim üyesi** atanır:
   - Sistem hocanın MaxWeeklyHours limitini kontrol eder (YÖK unvanına göre: Prof/Doç/Dr.Öğr.Üyesi)
   - Limit aşılıyorsa → **engel**, atama yapılamaz (H9)
   - Recitation/Lab şubelerine TA/Araştırma Görevlisi atanır
4. Her ders için **CourseAccessPolicy** tanımlanır:
   - "Bu dersi hangi bölüm/fakülte öğrencileri görebilsin?"
   - Varsayılan: sadece kendi bölümü
   - DepartmentManager isterse başka bölüm/fakültelere de açar
5. Bu aşamada zaman ve derslik ataması **YAPILMAZ** — sadece "hangi ders, kaç şube, kim verecek" belirlenir

### ADIM 5 — Scheduling'e Geçiş [Planning → Scheduling]
**Rol: SystemAdmin**
1. SystemAdmin dönem durumunu **Scheduling**'e geçirir
2. Guard kontrolü:
   - Tüm bölümler ders açmış mı? (en az zorunlu dersler)
   - Her şubede öğretim üyesi atanmış mı?
   - Hoca yük limitleri aşılmamış mı?
3. Eksik varsa → hata listesi, geçiş engellenir

### ADIM 6 — Otomatik Scheduling [Scheduling]
**Rol: Sistem (otomatik)**
1. Sistem tüm şubelere zaman slotu + derslik atamasını **otomatik** yapar
2. Önceki dönemin programı varsa **başlangıç noktası** olarak kullanılır (incremental scheduling)
3. Yerleştirme sırası (heuristik): büyük amfi dersleri → zorunlu dersler → seçmeli → seminer

**Hard Constraints — ihlal edilemez, kesin engel (H1-H12):**
- H1: Hoca aynı slot'ta iki derse atanamaz
- H2: Derslik aynı slot'ta iki derse atanamaz
- H3: Derslik kapasitesi >= şube kontenjanı
- H4: Derslik tipi uyumu — Lab → Lab dersliği, büyük ders (100+) → Amfi, seminer → küçük sınıf
- H5: Aynı bölümün aynı sınıf seviyesindeki zorunlu dersleri aynı slot'ta olamaz
- H6: Aynı dersin bileşenleri (lecture+recitation+lab) birbiriyle çakışmamalı
- H7: Cross-listed ders tek zaman, tek mekân alır
- H8: Aynı dersin farklı şubeleri aynı slot'ta olamaz
- H9: Hocanın toplam haftalık saati MaxWeeklyHours'u aşamaz
- H10: Fakülteye atanmamış derslik o fakültenin dersleri için kullanılamaz
- H11: Corequisite derslerde (lecture + bağlı lab) en az bir kombinasyon çakışmasız olmalı
- H12: Dersin kredi/saat gereksinimine uygun pattern atanmalı (3 kredi → MWF veya TR)

**Soft Constraints — uyarı, best-effort (S1-S4):**
- S1: Derslik doluluk oranı %60-85 arası olmalı
- S2: Hocanın günlük ders yoğunluğu dengeli olmalı
- S3: Slot'lar arası şube dağılım dengesi (prime-time'a yığılma önlenmeli)
- S4: Cross-department sık birlikte alınan ders çiftlerinin çakışma uyarısı

### ADIM 7 — Review ve Düzeltme [Scheduling]
**Rol: SystemAdmin / DepartmentManager**
1. Otomatik atama sonucu incelenir
2. Sistem cross-department çakışmaları ve soft constraint uyarılarını raporlar
3. Gerekirse manuel düzeltme yapılır — **her düzeltmede H1-H12 yeniden kontrol edilir**
4. Review-Revision döngüsü birkaç kez tekrarlanabilir

### ADIM 8 — Yayımlama [Scheduling → Published]
**Rol: SystemAdmin**
1. SystemAdmin dönem durumunu **Published**'a geçirir
2. Guard kontrolü:
   - Tüm şubelerde hoca + slot + derslik atanmış mı?
   - Hard constraint ihlali var mı? (varsa hata listesi gösterilir, geçiş engellenir)
3. **Bu andan itibaren öğrenciler ders kataloğunu görebilir**
4. Öğrenci kataloğunda: ders adı, şubeler, hoca, zaman, derslik, kontenjan, önkoşullar, CourseAccessPolicy'ye göre filtrelenmiş
5. Henüz kayıt **yapılamaz**, sadece görüntüleme ve planlama

### ADIM 9 — Kayıt Açılışı [Published → RegistrationOpen]
**Rol: SystemAdmin**
1. Akademik takvimde belirlenen kayıt başlangıç tarihinde geçiş yapılır
2. Dönem durumu: **RegistrationOpen**
3. Tüm öğrenciler aynı anda kayıt yapabilir (priority registration yok)
4. **Öğrencinin önünde "Derse Kaydol" butonu artık aktiftir**

### ADIM 10 — Öğrenci Ders Seçimi [RegistrationOpen]
**Rol: Student**
1. Öğrenci sisteme girer, ders kataloğunu görür (CourseAccessPolicy'ye göre filtrelenmiş)
2. Her ders eklediğinde sistem **anında** kontrol eder:
   - Önkoşul sağlanıyor mu?
   - AKTS limiti aşılıyor mu? (CGPA'ya göre: <2.00→30, 2.00-2.49→42, ≥2.50→48 AKTS)
   - Zaman çakışması var mı?
   - Kontenjan dolu mu?
   - FF/NA dersler önce seçilmiş mi? (alttan dersler zorunlu öncelik)
   - Sınıf atlatma kuralları karşılanıyor mu?
3. Öğrenci tüm derslerini seçer → **RegistrationPlan** oluşur (Status: Draft)
4. Öğrenci "Danışmana Gönder" butonuna basar → Status: **Submitted**
5. Kontenjan üzerinde **soft hold** oluşur (gece yarısı onaylanmazsa serbest bırakılır)

### ADIM 11 — Danışman Onayı [RegistrationOpen]
**Rol: AcademicAdvisor**
1. Danışman ekranında öğrencinin **tüm planını** tek ekranda görür:
   - Seçilen dersler ve AKTS toplamı
   - Alttan dersler (FF/NA) vurgulanmış
   - Müfredat uyumu (hangi dersler eksik)
   - Önkoşul durumu
   - Transkript, sınav notları, genel durum
2. Danışman karar verir:
   - **Onayla** → Tüm dersler **Active** olur, soft hold kesinleşir
   - **Reddet + Not** → Plan öğrenciye döner (Status: Rejected), soft hold serbest bırakılır
3. Öğrenci reddedilen planı düzenleyip **tekrar gönderebilir** (kayıt dönemi açık olduğu sürece)
4. Onay = öğrencinin o dönemki **tüm planı bir arada** onaylanır, tek tek section değil
5. Danışman onayı **her zaman zorunlu** — otomatik onay yok

### ADIM 12 — Add/Drop Dönemi [RegistrationOpen → AddDrop]
**Rol: SystemAdmin → Student → AcademicAdvisor**
1. SystemAdmin dönem durumunu **AddDrop**'a geçirir
2. Geçişte: Submitted/Draft durumunda kalan planlar **otomatik iptal** edilir
3. Öğrenci mevcut kayıtlarından ders bırakabilir veya yeni ders ekleyebilir
4. Her değişiklik aynı kontrollere tabidir (önkoşul, AKTS, çakışma, kontenjan)
5. Değişiklikler yine **danışman onayı** gerektirir
6. Add/Drop'ta bırakılan dersler transkripte **görünmez**

### ADIM 13 — Ders Çekilme Dönemi [AddDrop → WithdrawalPeriod]
**Rol: SystemAdmin → Student → AcademicAdvisor**
1. SystemAdmin dönem durumunu **WithdrawalPeriod**'a geçirir
2. Öğrenci ders çekilme talebi yapabilir
3. Danışman onayı gerekli
4. Çekilinen derslerde **W notu** transkripte işlenir

### ADIM 14 — Not Girişi [WithdrawalPeriod → GradeEntry]
**Rol: SystemAdmin → Instructor**
1. SystemAdmin dönem durumunu **GradeEntry**'e geçirir
2. Not giriş zaman penceresi açılır (sistem konfigürasyonunda tanımlı)
3. **Instructor** kendi derslerinin notlarını girer
4. Not giriş/düzeltme işlemleri **audit loglanır**

### ADIM 15 — Dönem Kapatma [GradeEntry → Closed → Archived]
**Rol: SystemAdmin**
1. Not giriş süresi biter, SystemAdmin dönemi **Closed**'a geçirir
2. Sistem sınıf atlatma kurallarını otomatik uygular (A4'te tanımlanan kurallar)
3. CGPA < 1.70 olan öğrenciler sınıfta kalır, üst dönem dersleri alamaz
4. Not itiraz süresi bittikten sonra dönem **Archived**'a geçirilir

---

## C. ÇAPRAZ KESİŞEN ALTYAPI

### C1. Audit Log
Tüm kritik işlemler ayrıntılı ve sorgulanabilir loglanır:
- Yetki değişiklikleri (rol atama/kaldırma)
- Ders açma/kapama, kontenjan değişimi
- Müfredat değişiklikleri
- Danışman onay/red işlemleri
- Not giriş/düzeltme
- Sistem ayarı değişiklikleri
- Format: kim, ne zaman, ne yaptı, eski değer → yeni değer

### C2. Bildirim Sistemi
İlgili kişilere otomatik bildirim:
- Dönem durum geçişlerinde ilgili rollere
- Danışman onay/red durumunda öğrenciye
- Kontenjan değişikliğinde kayıtlı öğrencilere
- Sistem konfigürasyon değişikliklerinde ilgili rollere

### C3. Bütünleme Katılım Anketi
- GradeEntry sonrasında öğrenci bütünleme sınavına girip girmeyeceğini önceden işaretler
- Sistem katılım tahmini üretir, gereksiz operasyonel yük azaltılır

---

## D. İŞ KURALLARI REFERANSİ

### Kayıt Kuralları
- AKTS limitleri: CGPA <2.00 → 30, 2.00-2.49 → 42, ≥2.50 → 48 AKTS
- CGPA < 1.70 (1. dönem sonrası) → sınıfta kalır, üst dönem dersleri alamaz
- FF/NA/U dersler yeni derslerden önce seçilmeli
- İlk kez alınan dersler arası tam çakışma → kesin engel, override yok
- Tekrar alınan (FF) dersler arası çakışma → danışman + bölüm başkanı onayı ile izin
- Soft hold: gece yarısı onaylanmazsa serbest bırakılır
- Danışman onayı her zaman zorunlu — otomatik onay yok
- Onay = tüm plan bir arada, tek tek section değil
- Tüm öğrenciler aynı anda kayıt yapar (priority registration yok)

### Scheduling Kuralları
- Sabit zaman grid'i — rastgele saat seçimi yok
- Otomatik atama — sistem H1-H12'ye uygun yerleştirir, insan inceler/düzeltir
- Tek kampüs — passing time hesaplanmaz
- Incremental scheduling — önceki dönemin programı başlangıç noktası
- Yerleştirme sırası: büyük amfi → zorunlu → seçmeli → seminer

### Erişim ve Yetki Kuralları
- Ders açma: SystemAdmin, DepartmentManager, Dean yapabilir
- CourseAccessPolicy: her ders için tanımlanır, varsayılan sadece kendi bölümü
- Derslik ataması: SystemAdmin fakültelere atar, sistem yoğunluğa göre öneri sunar
- Personel önce tanımlanır, sonra kullanıcıya dönüştürülür

### Müfredat Kuralları
- Müfredat versiyonlu — paralel aktif müfredatlar
- Sınıf atlatma kuralları admin tarafından tanımlanır (min CGPA, min AKTS, min ders sayısı)
- Aynı ders farklı müfredatlarda farklı statüyle bağlanabilir (zorunlu/seçmeli/teknik seçmeli)

---

## E. MEVCUT ENTITY DURUMU VE EKSİKLER

- Classroom: Name, Capacity, Type — **EKSİK:** BuildingName, Floor
- Course: DepartmentId nullable (ortak dersler için) — **EKSİK:** CourseAccessPolicy
- CourseSection: ScheduleJson string — **DEĞİŞECEK:** SectionSchedule(CourseSectionId, TimeSlotId, ClassroomId) entity'ye geçilecek
- Instructor: MaxWeeklyHours var, Availabilities var — Availabilities kullanılmayacak (YÖK bazlı yük yeterli)
- RegistrationStatus: Draft/Active/PendingAdvisorApproval/Dropped/Withdrawn — **EKSİK:** Rejected
- Curriculum: tek versiyonlu — **EKSİK:** versiyonlama desteği
- TimeSlot: **YENİ** entity — Pattern bazlı sabit zaman grid'i
- ClassroomTermAssignment: **YENİ** entity — dönemlik derslik-fakülte ataması
- RegistrationPlan: **YENİ** entity — öğrencinin dönemlik kayıt planı üst entity'si
- AuditLog: **YENİ** — kritik işlemlerin sorgulanabilir logu
- SystemConfig: **YENİ** — konfigüre edilebilir iş kuralları (ECTS limitleri, CGPA eşikleri, çekilme limitleri, unvan→MaxWeeklyHours, derslik eşikleri)
- PromotionRule: **YENİ** — program bazlı sınıf atlatma kuralları (min CGPA, min AKTS, min ders sayısı)
- InstructorAvailability: **KALDIRILACAK** — entity, endpoint'ler ve SchedulingService bağımlılığı temizlenecek
- CourseRegistration: **EKSİK:** RowVersion (concurrency), AdvisorNote alanı
- CourseSection: **EKSİK:** SectionNumber alanı (şube numarası)
- Tüm ana entity'ler: **EKSİK:** CreatedAt/UpdatedAt audit alanları
- ClassroomEquipment, PreRegistrationSurvey: **SİLİNECEK** — boş placeholder dosyalar
- AppDbContext: **EKSİK:** Performans index'leri (CourseRegistration, CourseSection, Student.AdvisorId)

---

## F. TAM UYGULAMA YOL HARİTASI (Audit Sonucu — 2026-03-31)

### FAZ 0 — KRİTİK GÜVENLİK VE BUG FIX'LER
Yeni entity gerekmez. Mevcut koddaki veri bütünlüğü ve güvenlik sorunlarını düzeltir.

**0.1 — Güvenlik Açıkları**
- [ ] IDOR düzelt: AcademicLevelController ve EligibilityController'da Student rolü başka öğrencinin verisini görebiliyor → studentId == authenticatedStudentId kontrolü ekle
- [ ] GradeController yetki düzelt: Bir hoca başka hocanın section'ındaki notları değiştirebiliyor → section bazlı instructor kontrolü ekle (GetAuthorizedSectionAsync)
- [ ] AuthService refresh token güvenlik açığı: ValidateLifetime=false → süresi dolmuş token ile sonsuz yenileme mümkün → düzelt
- [ ] StudentController'a [Authorize(Roles="Student")] ekle (şu an tüm authenticated kullanıcılara açık)

**0.2 — Dean Rolü Düzeltmeleri**
- [ ] CourseOpeningController.HasDepartmentAccessAsync: Dean için fakülte bazlı erişim dalı ekle
- [ ] FacultyPlanningController: facultyId'nin Dean'in kendi fakültesine ait olduğunu kontrol et
- [ ] TermClosingController, AcademicProgressionController, ForecastController: Dean'in sadece kendi fakültesindeki bölümlere erişimini kontrol et

**0.3 — DepartmentManager Erişim Kontrolü**
- [ ] FacultyPlanningController: DepartmentManager'ın facultyId kontrolü ekle
- [ ] TermClosingController: departmentId doğrulama ekle
- [ ] AcademicProgressionController: departmentId doğrulama ekle
- [ ] ForecastController: departmentId doğrulama ekle
- [ ] SchedulingController (validate, suggest, apply): erişim kontrolü ekle
- [ ] StudentManagementController, InstructorManagementController: bölüm filtreleme ekle

**0.4 — İş Kuralı Bug'ları**
- [ ] Burslu öğrenci danışman atlama kaldır (CourseRegistrationService.SubmitRegistrationAsync) — danışman onayı her zaman zorunlu
- [ ] Ortak ders açma bug'ı düzelt (CourseOpeningService.OpenCourseAsync satır 180) — DepartmentId null kontrolü
- [ ] GpaCalculationService: IsNonGpa dersleri GPA'ya dahil etme → Course.IsNonGpa kontrolü ekle
- [ ] AcademicProgressionService: CompletedSemesters tek sayı kaybı düzelt, SGPA güncellenmemesi düzelt
- [ ] AdminCourseCatalogService: Önkoşul döngüsü kontrolü ekle (A→B→C→A engeli), denklik simetri kontrolü
- [ ] AdminTermService: Tarih validasyonu ekle (RegStart < RegEnd, TermStart < TermEnd, vb.)
- [ ] TermClosingService.OpenNewTermAsync: Çoklu bölüm sorunu düzelt (bir bölüm yeni dönem açınca diğerlerinin aktif dönemi kapanıyor)

**0.5 — Kayıt Akışı Bug'ları**
- [ ] RegistrationStatus.Rejected enum değeri ekle
- [ ] RejectStudentRegistrationsAsync: Dropped yerine Rejected kullan, AdvisorNote kaydet
- [ ] AddToPlanAsync: Kapasite kontrolü ekle (EnrolledCount >= Capacity → engel)
- [ ] InstructorAvailability kaldır (entity, endpoint'ler, SchedulingService bağımlılığı)

**0.6 — Ders Açma Validasyonları**
- [ ] Duplikat section kontrolü (aynı ders, aynı dönem, aynı bölüm)
- [ ] InstructorId geçerlilik kontrolü (var mı, doğru departmanda mı)
- [ ] Capacity > 0 ve SectionCount üst sınır kontrolü
- [ ] Semester parity backend'de enforce et (güz döneminde bahar dersi açılmasın)
- [ ] UpdateSection: capacity < enrolledCount kontrolü, IsActive false ise güncelleme engeli
- [ ] CloseSectionAsync: Draft/PendingAdvisorApproval kayıtları temizle
- [ ] BulkOpenCourseAsync: transactional yap veya duplikat courseId kontrolü ekle
- [ ] FluentValidation: CourseOpening DTO'larına validator ekle

### FAZ 1 — TERM GEÇİŞ GUARD'LARI VE TERM STATUS TUTARLILIK
Dönem lifecycle'ın veri bütünlüğünü koruyan guard'lar.

**1.1 — Geçiş Guard'ları (AdminTermService.TransitionStatusAsync)**
- [ ] Draft → Planning: Takvim tarihleri girilmiş mi? Derslik atamaları yapılmış mı?
- [ ] Planning → Scheduling: En az bir CourseSection açılmış mı? Her section'da Instructor atanmış mı?
- [ ] Scheduling → Published: Tüm aktif section'larda slot+derslik+hoca atanmış mı? Hard constraint ihlali var mı?
- [ ] GradeEntry → Closed: Tüm aktif section'lar finalize edilmiş mi? Not girilmemiş ders var mı?
- [ ] RegistrationOpen → AddDrop: Submitted/Draft kayıtları otomatik iptal et, kapasite serbest bırak
- [ ] ActivateTermAsync düzelt: Draft statusundeki dönem aktif edilemesin

**1.2 — Servis Seviyesi Term Status Guard'ları**
- [ ] SubmitRegistrationAsync: term.Status RegistrationOpen/AddDrop kontrolü
- [ ] DropCourseAsync: term.Status AddDrop kontrolü (tarih yerine veya tarihle birlikte)
- [ ] WithdrawCourseAsync: term.Status WithdrawalPeriod kontrolü
- [ ] ApproveStudentRegistrationsAsync: term.Status RegistrationOpen/AddDrop kontrolü
- [ ] RejectStudentRegistrationsAsync: term.Status RegistrationOpen/AddDrop kontrolü
- [ ] UnassignSectionAsync: term.Status sadece Planning/Scheduling'de izin ver
- [ ] GradeController (SubmitGrades, Finalize, Unfinalize): term.Status GradeEntry kontrolü
- [ ] GradeController (UpsertComponent, DeleteComponent): term.Status Planning/Scheduling/GradeEntry kontrolü
- [ ] AcademicOperationsService (Approve/Reject): term.Status Planning kontrolü
- [ ] TermClosingService (LockGrades, CalculateGpa, Promote): adım sırası zorunluluğu ekle + term.Status GradeEntry/Closed kontrolü
- [ ] AcademicProgressionService: term.Status Closed kontrolü
- [ ] CalendarEvent CRUD: Archived dönemde düzenleme engeli
- [ ] UpdateTermAsync: Closed/Archived dönemler düzenlenemesin

**1.3 — IsActive / TermStatus Tutarlılık**
- [ ] RegistrationValidationService: tarih bazlı kontrol yerine (veya yanına) TermStatus kontrolü ekle
- [ ] CourseController.GetActiveTerm: IsRegistrationOpen/IsAddDropOpen değerlerini TermStatus'tan türet
- [ ] Birden fazla aktif dönem riski: FirstOrDefaultAsync(t => t.IsActive) kullanan tüm yerleri tespit et, tutarlılık sağla

### FAZ 2 — YENİ ENTITY'LER VE MİGRATION (Altyapı Temeli)

**2.1 — Entity Güncellemeleri**
- [ ] Classroom: BuildingName (string), Floor (int) ekle
- [ ] CourseSection: SectionNumber (int) ekle, (CourseId, TermId, SectionNumber) unique constraint
- [ ] CourseRegistration: RowVersion ekle (concurrency), RegistrationPlanId FK ekle
- [ ] Tüm ana entity'lere CreatedAt/UpdatedAt audit alanları ekle (Faculty, Department, Program, Curriculum, Course, CourseSection, Student, Instructor, Classroom, AcademicTerm)
- [ ] SchedulingAuditLog → CourseSection cascade delete'i Restrict'e çevir
- [ ] Boş dosyaları sil: ClassroomEquipment.cs, PreRegistrationSurvey.cs

**2.2 — Yeni Entity'ler**
- [ ] TimeSlot: Id, Pattern (MWF/TR/MW/SingleDay), StartTime, EndTime, SlotType (Normal/Lab), DisplayName + CRUD + seed data
- [ ] SectionSchedule: Id, CourseSectionId (FK), TimeSlotId (FK), ClassroomId (FK)
- [ ] ClassroomTermAssignment: Id, TermId (FK), ClassroomId (FK), FacultyId (FK)
- [ ] CourseAccessPolicy: Id, CourseId (FK), TermId (FK), FacultyId? (FK), DepartmentId? (FK)
- [ ] RegistrationPlan: Id, StudentId (FK), TermId (FK), Status (Draft/Submitted/Approved/Rejected), SubmittedAt, AdvisorNote, CreatedAt, UpdatedAt
- [ ] AuditLog: Id, UserId, Action, EntityType, EntityId, OldValue (JSON), NewValue (JSON), Timestamp, IpAddress
- [ ] SystemConfig: Id, Key, Value, Description, Category, DataType + seed data (ECTS limitleri, CGPA eşikleri, çekilme limitleri, unvan→MaxWeeklyHours, derslik eşikleri)
- [ ] PromotionRule: Id, ProgramId (FK), FromYear, ToYear, MinCgpa, MinEcts, MinCourseCount

**2.3 — InstructorAvailability Kaldırma**
- [ ] Entity, DbSet, OnModelCreating konfigürasyonu kaldır
- [ ] InstructorController availability endpoint'leri kaldır
- [ ] SchedulingService.SuggestAsync'ten availability bağımlılığını kaldır
- [ ] Migration: InstructorAvailability tablosu drop

**2.4 — Performans Index'leri**
- [ ] CourseRegistration: (StudentId), (CourseSectionId), (StudentId, CourseSectionId) index
- [ ] CourseSection: (CourseId, TermId) composite index
- [ ] Student: (AdvisorId) index
- [ ] Curriculum: (ProgramId, Version) unique constraint
- [ ] Classroom: (Name) unique index
- [ ] GradeComponent: (CourseId, AcademicTermId, Name) unique constraint
- [ ] Migration oluştur ve uygula

### FAZ 3 — SCHEDULİNG MİMARİSİ YENİDEN YAPI

**3.1 — ScheduleJson → SectionSchedule Geçişi**
- [ ] Mevcut ScheduleJson verilerini parse → SectionSchedule + TimeSlot eşleştirme migration script'i
- [ ] CourseSection.ScheduleJson alanını kaldır
- [ ] ScheduleSlot value object'i kaldır (SIS.Domain/ValueObjects/ScheduleSlot.cs)
- [ ] Code Conventions'dan ScheduleSlot referansını kaldır

**3.2 — SchedulingService Yeniden Yazım (TimeSlot bazlı)**
- [ ] ValidateAsync: H1-H12 tüm constraint'leri SectionSchedule+TimeSlot üzerinden kontrol et
- [ ] SuggestAsync: TimeSlot pattern'larını kullan, çakışma bazlı eleme (availability'siz)
- [ ] ApplyAsync: SectionSchedule kayıtları oluştur
- [ ] BatchAutoScheduleAsync: Yeni yapıya uyumlu + sıralama heuristiği (büyük amfi → zorunlu → seçmeli → seminer)

**3.3 — H4-H12 Constraint İmplementasyonu**
- [ ] H4: Derslik tipi uyumu (Classroom.Type kontrolü)
- [ ] H5: Müfredat çakışma (CurriculumCourse'dan aynı bölüm/yıl zorunlu dersler → farklı slot)
- [ ] H6: Ders bileşen çakışma (aynı Course'un lecture vs lab section'ları)
- [ ] H7: Cross-listed ders (CourseEquivalency üzerinden tek zaman/mekân)
- [ ] H8: Aynı ders farklı şube → farklı slot
- [ ] H10: Fakülte derslik kısıtı (ClassroomTermAssignment kontrolü)
- [ ] H11: Corequisite (lecture + bağlı lab çakışmasız kombinasyon)
- [ ] H12: Kredi → pattern uyumu (TheoryHours+LabHours → uygun TimeSlot pattern)

**3.4 — Soft Constraint'ler ve İyileştirmeler**
- [ ] S1: Derslik doluluk oranı (SystemConfig'den eşik)
- [ ] S2: Hoca günlük yoğunluk dengesi
- [ ] S3: Slot dağılım dengesi
- [ ] S4: Cross-department sık birlikte alınan ders çiftleri çakışma uyarısı
- [ ] Incremental scheduling: önceki dönem SectionSchedule verilerini başlangıç noktası olarak kopyala

### FAZ 4 — KAYIT AKIŞI İYİLEŞTİRME

**4.1 — RegistrationPlan Entegrasyonu**
- [ ] AddToPlanAsync: İlk ders eklendiğinde RegistrationPlan(Status:Draft) oluştur
- [ ] SubmitRegistrationAsync: RegistrationPlan.Status=Submitted, SubmittedAt=now
- [ ] ApproveStudentRegistrationsAsync: RegistrationPlan.Status=Approved
- [ ] RejectStudentRegistrationsAsync: RegistrationPlan.Status=Rejected, AdvisorNote kaydedilir
- [ ] Red sonrası düzeltme döngüsü: Öğrenci Rejected planı düzenleyip tekrar gönderin

**4.2 — CourseAccessPolicy Entegrasyonu**
- [ ] Ders açma sırasında otomatik kendi bölümü eklenir, ek bölüm/fakülte seçilebilir
- [ ] CourseController → student-catalog: Öğrencinin bölümüne/fakültesine açık dersler filtresi
- [ ] RegistrationValidationService: Müfredatta olmayan + CourseAccessPolicy'de olmayan derse kayıt engeli
- [ ] EnrollmentForecastService: Cross-department talep aggregation (CourseAccessPolicy bazlı)
- [ ] DepartmentId null dersler için özel handling (ortak dersler her bölümde görünsün)

**4.3 — Danışman Dashboard Genişletme**
- [ ] GetPendingApprovalsAsync: Alttan dersler listesi, müfredat uyumu, önkoşul zinciri durumu, transkript özeti ekle

**4.4 — Hardcoded Değerleri SystemConfig'e Taşı**
- [ ] RegistrationRulePolicy: ECTS limitleri, CGPA eşiği → SystemConfig'den oku
- [ ] CourseRegistrationService: Çekilme limitleri → SystemConfig'den oku
- [ ] SchedulingService: Derslik tipi eşikleri, doluluk oranı → SystemConfig'den oku
- [ ] YÖK unvan → MaxWeeklyHours eşleştirmesi → SystemConfig'den oku, Instructor oluşturulurken otomatik set

### FAZ 5 — PERFORMANS VE KOD KALİTESİ

**5.1 — N+1 ve Memory Sorunları**
- [ ] AdminUserService: DB seviyesinde sayfalama, GetRolesAsync batch hale getir
- [ ] AdminInstructorService: DB seviyesinde sayfalama ekle
- [ ] StudentEligibilityService: Tüm veriyi belleğe yükleme yerine filtrelenmiş sorgu
- [ ] EnrollmentForecastService: GPA batch hesaplama (N+1 çöz), cache key'i departmentId+termId bazlı yap
- [ ] GpaCalculationService: Batch GPA hesaplama metodu ekle

**5.2 — Transaction Eksiklikleri**
- [ ] BulkOpenCoursesAsync: Transactional yap
- [ ] TermClosingService.LockGradesAsync: Çift SaveChanges sorunu düzelt
- [ ] AcademicOperationsService.BulkApproveAsync: Transactional yap
- [ ] AcademicProgressionService.PromoteDepartmentStudentsAsync: Explicit transaction

**5.3 — Kod Kalitesi**
- [ ] CourseController.GetStudentCatalog: 190 satır iş mantığını servis katmanına taşı
- [ ] Hata mesajı format tutarlılığı: Tüm controller'larda { error = "..." } standardize et
- [ ] FacultyPlanningController: GetUserId exception handling düzelt (500 yerine 401)
- [ ] AnnouncementController: Exception yutmayı düzelt, loglama ekle
- [ ] RegistrationController: AppDbContext doğrudan kullanımını servis katmanına taşı
- [ ] Seed data düzelt: Geçmiş dönemlere doğru TermStatus ata, BSM-L çift tanım düzelt, MaxWeeklyHours unvana göre farklılaştır
- [ ] DateTime.UtcNow tutarlılığı: Tüm servislerde UTC kullanımını standardize et

### FAZ 6 — MÜFREDAT VE KURAL MOTORU

- [ ] Curriculum versiyonlama servisi: Paralel aktif müfredat yönetimi
- [ ] Müfredat geçiş kuralları: CurriculumTransitionMapping entity'si (eski ders → yeni ders)
- [ ] Sınıf atlatma kuralları: PromotionRule entity'sinden kural oku, AcademicProgressionService güncelle
- [ ] Danışman dashboard genişletme: Transkript, müfredat durumu, staj bilgileri tek ekranda

### FAZ 7 — ÇAPRAZ KESİŞEN ALTYAPI

- [ ] Genel audit log: Middleware/interceptor ile otomatik loglama, admin filtrelenebilir endpoint
- [ ] Bildirim sistemi: Notification entity, in-app bildirim, tetikleyiciler (dönem geçişi, danışman onay/red, kontenjan değişikliği)
- [ ] Derslik akıllı atama önerisi: Fakülte yoğunluğu analizi → SystemAdmin'e öneri

### FAZ 8 — FRONTEND (React)

- [ ] Admin term lifecycle UI
- [ ] Derslik atama ekranı (akıllı öneri ile)
- [ ] Ders açma ekranı (CourseAccessPolicy + enrollment forecast)
- [ ] Programlama ekranı (otomatik atama sonucu + manuel düzeltme + constraint uyarılar)
- [ ] Öğrenci ders seçim ekranı (katalog → plan → gönder)
- [ ] Danışman ekranı (tam dashboard + plan onay/red)
- [ ] Müfredat kural tanımlama UI
- [ ] SystemConfig yönetim paneli
- [ ] Audit log görüntüleme ekranı

### FAZ 9 — İLERİ SEVİYE ÖZELLİKLER

- [ ] Akademik takvim öneri motoru (önceki yıldan kaydırma)
- [ ] Bütünleme katılım anketi
- [ ] Zamana bağlı yetki mekanizması
- [ ] Personel → kullanıcı dönüşüm workflow'u
- [ ] Sistem konfigürasyon paneli (menü görünürlük, aç/kapat)

### BAĞIMLILIK GRAFİĞİ

```
FAZ 0 ──── bağımsız, HEMEN başla
FAZ 1 ──── bağımsız, FAZ 0 ile paralel
FAZ 2 ──── bağımsız, FAZ 0-1 ile paralel
FAZ 3 ──── FAZ 2'ye bağımlı (TimeSlot, SectionSchedule gerekli)
FAZ 4 ──── FAZ 2'ye bağımlı (RegistrationPlan, CourseAccessPolicy, SystemConfig gerekli)
FAZ 5 ──── bağımsız, FAZ 0-1 ile paralel başlanabilir
FAZ 6 ──── bağımsız
FAZ 7 ──── FAZ 2'ye kısmen bağımlı (AuditLog entity)
FAZ 8 ──── FAZ 0-7 tamamlanmış olmalı (backend hazır)
FAZ 9 ──── FAZ 8 sonrası
```

---

## G. DERS PROGRAMLAMA SİSTEMİ — KAPSAMLI YOL HARİTASI

Bu bölüm profesyonel bir OBS'nin ders programlama akışını uçtan uca tanımlar.
Scheduling, sistemin kalbidir — tüm diğer modüller (kayıt, not girişi, danışmanlık) buna bağımlıdır.

---

### G.0 MEVCUT DURUM ENVANTERİ

**Çalışan (tekrar yazılmayacak):**
- H1 (hoca çakışma), H2 (derslik çakışma), H3 (kapasite), H5 (aynı bölüm/yıl zorunlu ders), H6 (lecture vs lab bileşen), H8 (aynı ders farklı şube), H9 (haftalık yük), H10 (fakülte derslik kısıtı)
- S1 (derslik doluluk oranı), S2 (hoca günlük yük), S3 (slot yığılma) — üçü de SystemConfig'den konfigüre edilebilir
- TimeSlot grid generation (SystemConfig parametrelerinden, dönem başında otomatik)
- SectionSchedule dual-write (ApplyAsync hem ScheduleJson hem SectionSchedule yazar)
- EnrollmentForecastService (retaker FF/NA/U dahil, RecommendedSectionCount, SectionDistribution, ConfidenceRange, tarihsel veri)
- CourseOpeningService (müfredat dersleri listeleme, ders açma, instructor validasyonu)
- CourseAccessPolicy (ders açmada otomatik oluşur, CRUD endpoint var)
- ClassroomTermAssignment (entity + CRUD endpoint var)
- ValidateAsync, SuggestAsync, ApplyAsync (tekil section bazlı çalışıyor)
- ClassroomDashboard (derslik bazlı schedule görünümü)

**Kısmen çalışan (düzeltilecek):**
- H4: Lab derslik kontrolü var ama 100+ öğrenci → Amfi zorunluluğu yok
- BatchAutoScheduleAsync: var ama greedy, sıralama yanlış (EnrolledCount=0), backtracking yok
- SuggestAsync: PreferenceScore var ama first-fit, ClassroomTermAssignment ön-filtresi yok
- Published guard: atama kontrolü var ama ValidateExistingAsync çağrılmıyor (çakışmalı program yayınlanabilir)
- Draft→Planning guard: tarih kontrolü var ama derslik ataması kontrolü yok

**Hiç yok (yeni yazılacak):**
- H7, H11, H12, H13, H14, H15 constraint'leri
- S4, S5, S7, S8, S9, S10, S12 soft constraint'leri
- Forecast ↔ ders açma entegrasyonu (ayrı endpoint'ler, birleşik değil)
- Cross-department talep bilgisi ("bu dersi hangi bölümler alacak")
- Scoring bazlı scheduling (soft constraint ağırlıkları)
- Limited backtracking
- Incremental scheduling (önceki dönem referansı)
- Swap, lock, timetable endpoint'leri
- IsCorequisite alanı (CoursePrerequisite'te)
- IsScheduleLocked alanı (CourseSection'da)

---

### G.1 GUARD DÜZELTMELERİ VE ALTYAPI

Bu adım mevcut kodun yanlış çalışan kısımlarını düzeltir. Yeni özellik değil, mevcut hataların giderilmesi.

**G.1.1 — Draft → Planning guard'ına derslik atama kontrolü ekle**
- `AdminTermService.RunTransitionGuardsAsync`, Planning case'ine:
  - `ClassroomTermAssignment` tablosunda bu dönem için en az 1 kayıt var mı?
  - Yoksa: "Derslik ataması yapılmadan Planning'e geçilemez"

**G.1.2 — Published guard'ına hard constraint doğrulama ekle**
- `AdminTermService.RunTransitionGuardsAsync`, Published case'ine:
  - `ValidateExistingAsync(termId)` çağır
  - `conflicts.Count > 0` ise geçişi engelle, conflict listesini döndür
  - ClassroomId null olan section kontrolü (derslik atanmamış)
  - Soft constraint uyarılarını notes olarak döndür (engellemez)
  - Müfredattaki zorunlu derslerden açılmamış var mı? (uyarı)

**G.1.3 — H4 constraint'ini tamamla**
- `EvaluateExistingConflicts` ve `EvaluateCandidateConflicts`:
  - Lab section → Lab/Computer derslik (MEVCUT)
  - **EKLE:** kontenjan >= SystemConfig("Classroom.AmphiThreshold", 100) → ClassroomType.Amphitheater zorunlu
- `ClassroomType` enum'ına `Amphitheater` ekle (yoksa kontrol et)

**G.1.4 — SuggestAsync'te ClassroomTermAssignment ön-filtresi**
- Derslik sorgusuna filtre ekle:
  ```
  section'ın course'unun department'ının faculty'si → o faculty'ye atanmış derslikler
  VEYA ClassroomTermAssignment boşsa (henüz atama yoksa) → tüm derslikler
  ```
- Bu filtreyle gereksiz deneme azalır, performans artar

**G.1.5 — Entity güncellemeleri**
- `CoursePrerequisite` entity'sine `bool IsCorequisite` alanı ekle
- `CourseSection` entity'sine `bool IsScheduleLocked` alanı ekle
- Migration oluştur

---

### G.2 DERS AÇMA AKIŞI ZENGİNLEŞTİRME

Bölüm başkanı ders açma ekranına girdiğinde tüm karar verme bilgisini tek yerde görmeli.

**G.2.1 — Birleşik ders açma + forecast endpoint**
- YENİ endpoint: `GET /api/course-opening/{departmentId}/terms/{termId}/courses-with-forecast`
- Mevcut `GetCurriculumCoursesForOpeningAsync` + `GetCourseDemandForecastAsync` birleştirilir
- Her ders satırında:
  - Ders bilgisi (kod, ad, AKTS, teori/lab saat)
  - Müfredattaki yeri (yarıyıl, zorunlu/seçmeli)
  - Tahmini talep (toplam, retake breakdown)
  - Önerilen section sayısı ve kontenjan
  - Mevcut açılmış section sayısı (zaten açtıysa)
  - Önceki dönem kayıt istatistikleri

**G.2.2 — Cross-department talep bilgisi**
- Her ders için: "Bu dersi hangi bölümler müfredatında taşıyor?"
- Sorgu: `CurriculumCourse → Curriculum → Program → Department` join
- DTO'da: `List<CrossDepartmentDemandDto> { DepartmentName, ProgramName, ExpectedStudentCount }`
- Bölüm başkanı bunu görerek CourseAccessPolicy'yi doğru ayarlar

**G.2.3 — Section sayısı önerisi derslik envanteriyle eşleşme**
- Tahmin edilen talep + fakülteye atanmış derslik kapasiteleri → optimum section dağılımı
- Örnek: 200 kişi talep, fakültenin amfisi 120 kişilik, normal sınıfları 40-60 kişilik
  → Öneri: 1 amfi section (120) + 2 normal section (40+40) = 200
- Endpoint: mevcut forecast DTO'suna `SuggestedSectionDistribution` eklenir

**G.2.4 — Hoca yük bilgisi endpoint**
- YENİ endpoint: `GET /api/course-opening/instructors/{instructorId}/workload?termId=X`
- Dönüş: atanmış section sayısı, toplam haftalık saat, MaxWeeklyHours, kalan kapasite
- DepartmentManager hoca atarken bu bilgiyi görür

---

### G.3 SCHEDULING ALGORİTMASI YENİDEN YAPI

BatchAutoScheduleAsync'in tamamen yeniden yazılması. Mevcut greedy yaklaşım → scoring + limited backtracking.

**G.3.1 — Öncelik sıralaması düzeltme**
Mevcut `EnrolledCount` sıralaması anlamsız (ders açma aşamasında 0). Yeni sıralama:
```
Priority 1: Büyük dersler (kontenjan >= AmphiThreshold)
  → En az derslik seçeneği var, önce yerleştirilmeli
Priority 2: Lab dersleri (Course.LabHours > 0 && TheoryHours == 0)
  → Sadece Lab/Computer derslik kullanabilir, kısıtlı
Priority 3: Zorunlu dersler (CurriculumCourse.IsCompulsory = true)
  → Aynı bölüm/yıl çakışması riski yüksek, erken yerleştir
Priority 4: Aynı dersin farklı şubeleri
  → H8 gereği farklı slotlara, ama birlikte ele alınmalı
Priority 5: Seçmeli dersler
Priority 6: Ortak dersler (DepartmentId = null)
  → En esnek, en son yerleştir

Aynı öncelikte: daha az uygun slot seçeneği olan önce (MRV — Minimum Remaining Values)
```

**G.3.2 — Scoring bazlı slot+derslik seçimi**
Her section için tüm olası (slot_kombinasyonu, derslik) çiftleri üretilir. Her çift skorlanır:
```
score = 0
if hard_constraint_violation: SKIP (bu çift elenir)

// Soft constraint skorları (düşük = iyi)
score += S1_violation * weight_S1    // derslik doluluk sapması
score += S2_violation * weight_S2    // hoca günlük yük fazlası
score += S3_violation * weight_S3    // slot yığılması
score += S4_violation * weight_S4    // cross-dept çakışma
score += S5_violation * weight_S5    // öğle arası ihlali
score += S7_violation * weight_S7    // öğrenci boşluk
score += S8_violation * weight_S8    // hoca gün sayısı
score += S9_violation * weight_S9    // erken sabah / geç akşam
score += S10_violation * weight_S10  // farklı bina
score += S12_violation * weight_S12  // hafta içi dengesizlik

// Bonus (negatif skor = daha iyi)
if incremental_match: score -= incremental_bonus  // önceki dönemle aynı slot

En düşük skorlu çift seçilir.
```

Tüm ağırlıklar SystemConfig'den okunur. Varsayılanlar:
```
SoftConstraint.Weight.S1 = 3
SoftConstraint.Weight.S2 = 2
SoftConstraint.Weight.S3 = 2
SoftConstraint.Weight.S4 = 3
SoftConstraint.Weight.S5 = 2
SoftConstraint.Weight.S7 = 2
SoftConstraint.Weight.S8 = 1
SoftConstraint.Weight.S9 = 1
SoftConstraint.Weight.S10 = 0.5
SoftConstraint.Weight.S12 = 1
SoftConstraint.Weight.IncrementalBonus = 5
```

**G.3.3 — Limited backtracking**
Eğer bir section hiçbir çifte yerleştirilemezse:
```
1. Bu section'la çakışan tüm section'ları bul
2. Bunlardan en düşük öncelikli olanı seç
3. O section'ın atamasını geri al (slot+derslik boşalt)
4. Geri alınan section'ı farklı bir slota atamayı dene
5. Başarılıysa: orijinal section'ı tekrar dene
6. Başarısızsa: bir sonraki düşük öncelikli section'la dene
7. Max 3 seviye derinlik (performans için)
8. Hâlâ yerleştirilemezse: "Bu section yerleştirilemedi" raporla
```

**G.3.4 — Fakülte bazlı bütüncül çalıştırma**
- `BatchAutoScheduleAsync` artık `facultyId` parametresi alır (departmentId yerine veya ek olarak)
- Fakültedeki TÜM bölümlerin section'larını toplar
- Global önceliğe göre sıralar
- Böylece:
  - H5 (aynı bölüm/yıl zorunlu ders çakışması) doğru çalışır
  - H10 (fakülte derslik kısıtı) ön-filtre olarak uygulanır
  - Cross-department ortak dersler bütüncül ele alınır

**G.3.5 — Incremental scheduling**
- Önceki dönemin SectionSchedule kayıtlarını yükle
- Aynı course + aynı instructor → önceki dönemdeki slot "preferred"
- Scoring'de preferred slot'a `IncrementalBonus` kadar bonus
- `IsScheduleLocked = true` olan section'lar BatchAutoSchedule'dan hariç
- Sadece yeni eklenen veya değişen section'lar sıfırdan yerleştirilir

---

### G.4 EKSİK HARD CONSTRAINT'LER

Bu constraint'ler ihlal edildiğinde atama kesinlikle engellenir. Tümü `EvaluateExistingConflicts` ve `EvaluateCandidateConflicts`'te kontrol edilir.

| Kod | Constraint | Detaylı Açıklama | Gerekli Veri |
|-----|-----------|-----------------|-------------|
| H7 | Cross-listed ders tek zaman/mekân | CourseEquivalency ile eşdeğer sayılan iki ders aynı dönemde farklı section'lar olarak açılmışsa, bunlar aynı slot+dersliğe atanmalı (fiziksel olarak tek ders). Farklı slota atanırsa → conflict | `CourseEquivalency` tablosu |
| H11 | Corequisite çakışmasızlık | Bir dersin lecture section'ı ile bağlı lab/recitation section'ı arasında en az bir çakışmasız kombinasyon olmalı. Örnek: Fizik I Lecture (Sal 09:00) varsa, Fizik I Lab section'larından en az biri Salı 09:00 dışında olmalı ki öğrenci ikisini de alabilsin | `CoursePrerequisite.IsCorequisite` (YENİ alan) |
| H12 | Kredi-saat uyumu | Dersin TheoryHours+LabHours toplamına göre doğru sayıda ardışık slot bloğu atanmalı. 3 saat ders = 3 ardışık slot, 4 saat = 2 gün × 2 slot, 5 saat = 3+2. Yanlış blok boyutu → conflict | `Course.TheoryHours`, `Course.LabHours` |
| H13 | Öğle arası koruması | SystemConfig'den okunan öğle arası saatlerinde (varsayılan 11:40-12:20) ders atanamaz. SystemAdmin bu kuralı hard veya soft olarak ayarlayabilir | `SystemConfig: Schedule.LunchBreakStart/End, Schedule.LunchBreak.IsHard` |
| H14 | Ardışık ders arası geçiş süresi | Aynı hocanın art arda iki dersi arasında en az 1 slot boşluk olmalı (5 dakikalık mola zaten slot grid'inde var ama aynı slot'un bitişi ile sonraki slot'un başı arasında boşluk kontrolü) | TimeSlot grid, Instructor schedule |
| H15 | Max günlük ardışık ders bloğu | Bir hocanın aralıksız en fazla N saat (varsayılan 4) art arda dersi olabilir. Ara vermeden 5+ saat ders fiziksiz olarak sürdürülemez | `SystemConfig: Schedule.MaxConsecutiveHours` |

---

### G.5 EKSİK SOFT CONSTRAINT'LER

Her biri SystemConfig'den enable/disable edilebilir, ağırlığı (weight) ayarlanabilir.
Soft constraint'ler ihlalinde atama engellenmez, scoring'de ceza puanı verilir.

| Kod | Constraint | Detaylı Açıklama | Varsayılan Ağırlık | SystemConfig Key |
|-----|-----------|-----------------|-------------------|-----------------|
| S4 | Cross-department çakışma | Tarihsel kayıt verisinden: iki dersi aynı dönemde birlikte alan öğrenci sayısı yüksekse, bu iki ders aynı slot'ta olmamalı. Örnek: Matematik I ve Fizik I tüm mühendislik bölümleri alıyor → çakışırsa 300+ öğrenci etkilenir | w=3 | `SoftConstraint.S4.Enabled`, `SoftConstraint.Weight.S4` |
| S5 | Öğle arası tercihi (soft mod) | H13 hard ise burada S5 devre dışı kalır. H13 kapalıysa, öğle arası saatlerine ders atanmaması tercih edilir ama zorunlu değildir | w=2 | `SoftConstraint.S5.Enabled` |
| S7 | Öğrenci ders arası boşluk | Aynı bölüm/yıl öğrencilerinin bir gün içinde 3+ ardışık boş slot'u olmaması. Örnek: 1. sınıf BSM öğrencisinin dersleri 09:00 ve 15:00'te ise arada 5 saat boşluk var → kötü | w=2 | `SoftConstraint.S7.Enabled`, `SoftConstraint.S7.MaxGapSlots` (varsayılan: 3) |
| S8 | Hoca gün sayısı minimizasyonu | Hocanın dersleri mümkün olduğunca az güne sıkıştırılır. 12 saat yük: 3 güne 4'er saat > 5 güne 2-3'er saat | w=1 | `SoftConstraint.S8.Enabled` |
| S9 | Erken sabah / geç akşam cezası | Günün ilk slotu (09:00) ve son slotlarına (17:00+) atama cezası. Bu slotlar son çare | w=1 | `SoftConstraint.S9.Enabled` |
| S10 | Aynı bina tercihi | Hocanın tüm dersleri mümkünse aynı binada (Classroom.BuildingName). Farklı bina → geçiş süresi | w=0.5 | `SoftConstraint.S10.Enabled` |
| S12 | Hafta içi dengeli dağılım | Haftanın her gününe yaklaşık eşit section sayısı. Pazartesi'ye 50, Cuma'ya 10 section → dengesiz | w=1 | `SoftConstraint.S12.Enabled` |

---

### G.6 MANUEL DÜZELTME ARAÇLARI

Otomatik scheduling sonrası bölüm başkanı/SystemAdmin programı inceleyip düzeltebilmeli.

**G.6.1 — Timetable görüntüleme endpoint'leri**
```
GET /api/scheduling/instructor/{id}/timetable?termId=X
  → Hocanın haftalık ders programı (gün × slot matrisi)

GET /api/scheduling/classroom/{id}/timetable?termId=X
  → Dersliğin haftalık doluluk tablosu

GET /api/scheduling/department/{id}/timetable?termId=X&year=Y
  → Bölümün belirli sınıf seviyesinin haftalık programı

GET /api/scheduling/faculty/{id}/timetable?termId=X
  → Fakülte geneli özet: toplam section, çakışma sayısı, doluluk oranı
```

**G.6.2 — Swap endpoint**
```
POST /api/scheduling/swap
Body: { sectionId1, sectionId2, swapSlots: true, swapClassrooms: false }

İşlem:
1. İki section'ın mevcut slot ve/veya dersliğini takas et
2. Her iki section için hard constraint kontrolü yap
3. İhlal varsa → swap iptal, hata döndür
4. İhlal yoksa → uygula, SectionSchedule güncelle
```

**G.6.3 — Lock/Unlock**
```
POST /api/scheduling/lock/{sectionId}
POST /api/scheduling/unlock/{sectionId}

- CourseSection.IsScheduleLocked = true/false
- Kilitli section: BatchAutoScheduleAsync atlamaz, swap'a izin vermez
- Manuel ApplyAsync ile hâlâ değiştirilebilir (bilinçli karar)
```

**G.6.4 — Auto-resolve**
```
POST /api/scheduling/auto-resolve?termId=X

İşlem:
1. ValidateExistingAsync çağır → çakışma listesi al
2. Her çakışan section için alternatif slot bul (SuggestAsync)
3. En az toplam çakışma bırakan atamayı seç
4. Uygula ve sonucu raporla
5. Çözülemeyen çakışmaları listele
```

**G.6.5 — What-if analizi**
```
POST /api/scheduling/what-if
Body: { sectionId, proposedSlots: [...], proposedClassroomId }

Dönüş: Bu atama yapılsa hangi yeni çakışmalar oluşur, hangi mevcut çakışmalar çözülür
(mevcut ValidateAsync bunu kısmen yapıyor, genişletilecek)
```

---

### G.7 SCHEDULING → KAYIT ENTEGRASYONU

Scheduling tamamlanıp program yayınlandıktan sonra öğrenci kayıt akışıyla entegrasyon.

**G.7.1 — Öğrenci ders kataloğunda program bilgisi**
- Öğrenci kataloğunda her section'ın: gün, saat, derslik, hoca bilgisi SectionSchedule'dan gösterilmeli
- Mevcut ScheduleJson'dan okuyan kısım SectionSchedule'a geçirilmeli

**G.7.2 — Kayıt sırasında çakışma kontrolü SectionSchedule bazlı**
- RegistrationValidationService'te ScheduleJson parse yerine SectionSchedule join
- Öğrencinin seçtiği derslerin TimeSlot çakışması kontrolü
- Mevcut ScheduleSlot value object kullanımı → SectionSchedule sorgusu

**G.7.3 — ScheduleJson → SectionSchedule tam geçiş**
Tüm servisler SectionSchedule'a geçtikten sonra:
- CourseSection.ScheduleJson alanını kaldır
- ScheduleSlot value object'i kaldır (SIS.Domain/ValueObjects/ScheduleSlot.cs)
- ScheduleJson referansı olan 19 dosyayı temizle
- Migration oluştur

---

### G.8 UYGULAMA SIRASI VE BAĞIMLILIKLAR

```
G.1 → Guard düzeltmeleri + entity güncellemeleri (IsCorequisite, IsScheduleLocked)
  ↓
G.2 → Ders açma akışı zenginleştirme (forecast entegrasyonu)
  ↓  (bağımsız, G.1 ile paralel yapılabilir)
G.3 → Scheduling algoritması yeniden yapı (sıralama + scoring + backtracking)
  ↓
G.4 → Eksik hard constraint'ler (H7, H11, H12, H13-H15)
  ↓  (G.1'deki IsCorequisite alanı lazım)
G.5 → Eksik soft constraint'ler (S4, S5, S7-S12)
  ↓  (G.3'teki scoring sistemi lazım)
G.6 → Manuel düzeltme araçları (swap, lock, timetable, auto-resolve)
  ↓  (G.3'ten sonra)
G.7 → Scheduling → kayıt entegrasyonu + ScheduleJson kaldırma
  ↓  (G.3-G.6 tamamlandıktan sonra)
```

### AKIŞ DİYAGRAMI

```
[1. Dönem Oluştur (Draft)]
  SystemAdmin yeni dönem oluşturur (tarihler, akademik takvim)
       ↓
[2. Derslik-Fakülte Ataması (Draft)]
  SystemAdmin derslik → fakülte eşleştirmesi yapar
  Sistem öğrenci yoğunluğuna göre öneri sunar
       ↓
[3. Draft → Planning geçişi]
  Guard: tarihler girilmiş + derslik ataması yapılmış
  TimeSlot grid'i otomatik oluşturulur
       ↓
[4. Ders Açma (Planning)]
  DepartmentManager:
  ├── Müfredat derslerini forecast verileriyle görüntüler
  │   ├── Tahmini talep (retaker dahil)
  │   ├── Önerilen section sayısı ve kontenjan
  │   └── Cross-department talep: "Bu dersi BSM(45), EEM(30) da alacak"
  ├── Section sayısı ve kontenjan belirler
  ├── Her section'a hoca atar (MaxWeeklyHours kontrolü)
  ├── CourseAccessPolicy otomatik oluşur (kendi bölümü)
  └── İsterse başka bölüm/fakültelere de açar
       ↓
[5. Planning → Scheduling geçişi]
  Guard: en az 1 section + her section'da instructor atanmış
       ↓
[6. Otomatik Programlama (Scheduling)]
  Sistem fakülte bazlı bütüncül program oluşturur:
  ├── Öncelik sırası: büyük amfi → lab → zorunlu → seçmeli → ortak
  ├── Her section için en iyi (slot, derslik) çifti → scoring ile seçilir
  ├── H1-H15 hard constraint: ihlal eden çift elenir
  ├── S1-S12 soft constraint: ağırlıklı skor hesaplanır
  ├── Yerleştirilemezse → limited backtracking (max 3 seviye)
  └── Incremental: önceki dönemle aynı slot bonus alır
       ↓
[7. Review + Manuel Düzeltme (Scheduling)]
  DepartmentManager/SystemAdmin:
  ├── Conflict listesini inceler (hard: çözülmeli, soft: uyarı)
  ├── Timetable görüntüler (hoca/derslik/bölüm bazlı)
  ├── Manuel düzeltme: Apply (yeni slot ata), Swap (iki ders takas), Lock (kilitle)
  ├── Auto-resolve: sistem çakışmaları otomatik çözmeyi dener
  └── Döngü: düzelt → validate → tekrar düzelt (0 conflict olana kadar)
       ↓
[8. Scheduling → Published geçişi]
  Guard:
  ├── Tüm aktif section'larda instructor + classroom + schedule atanmış
  ├── ValidateExistingAsync → 0 hard conflict (zorunlu)
  ├── Soft constraint uyarıları raporlanır (engellemez)
  └── Açılmamış zorunlu ders uyarısı
       ↓
[9. Öğrenci Kataloğu (Published)]
  Öğrenciler ders kataloğunu görür (read-only, kayıt henüz kapalı)
  Her section: ders adı, hoca, gün+saat, derslik, kontenjan
       ↓
[10. Kayıt (RegistrationOpen)]
  Öğrenci ders seçer → çakışma kontrolü SectionSchedule bazlı
  → RegistrationPlan oluşur → Danışman onayı → Active
```
```
