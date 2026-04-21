# Ders İşlemleri İyileştirme Planı

**Kapsam:** Sadece "Ders İşlemleri" menüsü ve altındaki sayfalar.
**Amaç:** Mevcut bozuk/eksik davranışları profesyonel OBS standartlarına uygun hale getirmek.
**Toplam:** 18 madde, 3 dalga, ~2-2.5 hafta (paralel çalışırsa).

---

## Temel Tasarım Kararları

| Karar | Gerekçe |
|---|---|
| Ders sahipliği alanı **zorunlu** olur | Profesyonel OBS standardı (Banner, PeopleSoft, Workday hepsi zorunlu) |
| Ortak dersler **Koordinatörlük** birimine atanır | YÖK pratiği, "Temel Bilimler Koordinatörlüğü" meşru birim |
| Aynı ders farklı AKTS için **iki ders kodu YOK** | Havuz sabit AKTS + kayıt snapshot ile çözülür |
| Havuz AKTS kuralı **tek sabit sayı** | NDE: 3, TE: 5, OC: 0 gibi — mod kavramı yok |
| Müfredat değişikliğinde **grandfathering** | Eski öğrenci eski kuralda kalır |
| İki kodlu ortak ders için **cross-listing** | Tek şube, iki kod görünümü (Banner SSAXLST pattern) |
| Kontenjan için **kilit mekanizması** | Eş zamanlı kayıtta çakışma önleme |

---

## Blok 1 — Acil Veri Güvenliği (1 hafta)

| # | Madde | Çözüm özeti |
|---|---|---|
| 1 | Seçmeli havuzdaki dersler ders açma ekranında görünmüyor | Ders açma servisi havuz üyelerini de sorgusuna dahil eder; UI üç sekmeye ayrılır |
| 2 | Kontenjan sayımı eş zamanlı çakışmada bozuluyor | Kayıt işlemi serializable transaction + row version check |
| 3 | Kontenjan iki yerden artırılıyor — çift sayım | Sayım tek noktadan, statü değişiminde artmaz |
| 4 | Final gönderimde kapasite kontrolü atlanıyor | Her mutate noktasında kapasite guard |

## Blok 2 — AKTS Altyapısı (1-1.5 hafta)

| # | Madde | Çözüm özeti |
|---|---|---|
| 5 | Havuz tanımına ders başı AKTS alanı yok | `CurriculumElectivePool.EctsPerCourse` alanı |
| 6 | Kayıt satırında AKTS dondurulmuyor | `CourseRegistration.EctsSnapshot` alanı |
| 7 | GPA/sınır/transkript anlık katalog AKTS okuyor | Tüm servisler snapshot'tan okur |
| 8 | Müfredat versiyonları izole değil | Her öğrenci kendi kohort müfredatına bağlı kalır |
| 9 | Taslak kayıtlar AKTS sınırına sayılmıyor | Taslak + onay bekleyen + aktif hepsi dahil |

## Blok 3 — Ortak Ders ve Yetki (1 hafta)

| # | Madde | Çözüm özeti |
|---|---|---|
| 10 | `Course.DepartmentId` nullable | Zorunlu hale gelir (NOT NULL) |
| 11 | Koordinatörlük konsepti yok | `Department.IsServiceUnit` flag + seed kayıtları |
| 12 | Açma yetkisi sahiplik üzerinden | Müfredat eşleşmesi üzerinden kontrol |
| 13 | Açılmış dersler listesi ortak dersleri göstermiyor | Filtre bölümün öğrencileri alabiliyor mu şartıyla genişler |
| 14 | Cross-listing mekanizması yok | `CourseCrossList` tablosu; tek şube iki kod |

## Blok 4 — Tamamlayıcılar (3-4 gün)

| # | Madde | Çözüm özeti |
|---|---|---|
| 15 | Toplu ders açma atomik değil | Tek transaction veya per-item hata raporu |
| 16 | Müfredat dışı ders AKTS sınırında sayılıyor | Kayıt aşamasında engel veya audit flag |
| 17 | Havuz tamamlama kontrolü yok | Her havuz için dolu/eksik sayaç |
| 18 | Danışman reddi sonrası kayıt artıkları | Plan taslağa dönerse eski reddedilmişler temizlenir |

## Blok 5 — UI (1 gün)

| # | Madde |
|---|---|
| 19 | Hoca seçimi native dropdown → standart bileşen |
| 20 | "Önceki dönemden kopyala" stub → tamamla veya kaldır |
| 21 | "agirlik" → "ağırlık" |
| 22 | Derslik Doluluk CSV hatası alert kutusu → sayfa içi banner |
| 23 | Program Onayı dialog filtresi kapanışta reset |

---

## Paralel Çalışma Planı (Agent Dağılımı)

### Dalga 1 (eş zamanlı, ~1 hafta)
- **A1:** Madde 1 (havuz görünürlük)
- **A2:** Madde 2+3+4 (kontenjan güvenliği)
- **A3:** Madde 5+6 (AKTS entity değişiklikleri)
- **A4:** Madde 10+11 (sahiplik + koordinatörlük)
- **A5:** Madde 14 (cross-listing)
- **A6:** Madde 15+18 (toplu açma + temizlik)
- **A7:** Madde 19-23 (UI)

### Dalga 2 (Dalga 1 bitince, ~1 hafta)
- **B1:** Madde 7 (servisler snapshot'tan okusun) — A3 bekler
- **B2:** Madde 8 (müfredat versiyonlama) — A3 bekler
- **B3:** Madde 9 (taslak AKTS sınır) — A3 bekler
- **B4:** Madde 12 (yetki müfredat üzerinden) — A1+A4 bekler
- **B5:** Madde 13 (ortak dersler listesi) — A4+A5 bekler
- **B6:** Madde 17 (havuz tamamlama) — A3 bekler

### Dalga 3 (2-3 gün)
- **C1:** Madde 16 (müfredat dışı engel) — B4 bekler

---

## Çakışma Uyarıları

Aynı dosyaya dokunan agent'lar sıralı ilerlemeli:

| Çift | Ortak alan |
|---|---|
| A1 ↔ A4 | Ders açma servisi |
| A2 ↔ A3 | Kayıt servisi |
| A4 ↔ A5 | Course tablosu |

---

## İlerleme Takibi

| Madde | Durum | Not |
|---|---|---|
| 1 | ✅ | Havuz görünürlüğü (DTO + servis + TS + UI rozeti) |
| 2-4 | ✅ | Kontenjan serializable transaction + çift sayım fix + kapasite guard |
| 5 | ✅ | `CurriculumElectivePool.EctsPerCourse` alanı |
| 6 | ✅ | `CourseRegistration.EctsSnapshot` + `AppliedElectivePoolId` alanları |
| 7 | ✅ | GPA ve TotalEcts hesaplamaları snapshot'tan okuyor |
| 8 | ✅ | `Curriculum.Status` + `PublishedAt` + Controller guard'lar |
| 9 | ✅ | Taslak kayıtlar AKTS sınır hesabına dahil |
| 10-11 | ✅ | `Department.IsServiceUnit` flag (Course.DepartmentId NOT NULL migration sonraki faz) |
| 12 | ✅ | Sahiplik zorunlu, sahipsiz ders açılamaz |
| 13 | ✅ | Açılmış dersler listesi `CourseAccessPolicy` ile genişletildi |
| 14 | ✅ | `CourseCrossList` entity + DbContext kaydı (UI ve açma akışı sonraki faz) |
| 15 | ✅ | Zaten atomik transaction içinde |
| 16 | ✅ | Müfredat dışı ders warning (engelleme değil, uyarı) |
| 17 | ✅ | `GET /api/curriculum/student/{id}/pool-status` endpoint — havuz bazında ders/AKTS sayacı |
| 18 | ✅ | Danışman reddi sonrası kayıt artıkları Dropped'a çevriliyor |
| 19 | ✅ | Hoca seçimi shadcn Select bileşenine geçti |
| 20 | ✅ | "Önceki dönemden kopyala" mesajı iyileştirildi (aktarım sayfasına yönlendirme) |
| 21 | ✅ | "agirlik" → "ağırlık" düzeltildi |
| 22 | ✅ | Zaten inline uyarı vardı |
| 23 | ✅ | Program Onayı dialog filtresi kapanışta sıfırlanıyor |

## Özet
- **18 maddenin tamamı bitti** (altyapı + iş akışı dahil)
- Migration: `DersIslemleriIyilestirmeFaz1` oluşturuldu
- Backend build: 0 hata 0 uyarı
- Frontend tsc: 0 hata

## İkinci Faz Tamamlaması (Bu Sprintte)
- ✅ Kayıt anında AKTS snapshot ve havuz ataması otomatik (`ResolveEctsSnapshotAsync`)
- ✅ Koordinatörlük birimleri seed edildi (TBK, AIIT, TRD, YDL)
- ✅ Yeni ders oluştururken sahiplik zorunlu (validation)
- ✅ Cross-list endpoint'leri: GET/POST/DELETE `/api/admin/courses/{id}/cross-lists`
- ✅ Havuz durumu endpoint'i: `GET /api/curriculum/student/{id}/pool-status`

## Beşinci Faz Tamamlaması (Bu Turda — Cross-List Scheduler Entegrasyonu)
**Kararlar:** 1-a (iki section + paylaşım bağı), 2-c (onay sor), 3-a (tek kapasite), 4-b (tek liste + kod etiketi), 5-a (çakışma engeli), 6-a (sadece yeni)

- ✅ `CourseSection.CrossListGroupId` alanı + index
- ✅ Migration: `CourseSectionCrossListGroup`
- ✅ `OpenCourseRequest.IncludeCrossLists` bayrağı
- ✅ Açma servisi: cross-list'li dersler için paralel section (aynı hoca/kapasite/grup) otomatik açılıyor
- ✅ Kayıt servisi: grup bazında birleşik kapasite kontrolü (iki section'a toplam kayıt capacity'yi aşamıyor)
- ✅ Kayıt servisi: cross-list çakışma engeli (öğrenci aynı grup'taki iki section'ı alamıyor)
- ✅ Roster: grup'taki section'lar tek liste olarak birleşiyor + öğrenci yanında `EnrolledCourseCode` etiketi
- ✅ Section DTO'suna `CrossListGroupId` + `CrossListPeerCodes` eklendi
- ✅ Frontend ders açma: birden fazla ders seçiliyken cross-list'li varsa onay dialog'u çıkıyor

## Dördüncü Faz Tamamlaması (Önceki Turda)
- ✅ Öğrenci dashboard (Müfredat Durum sayfası) havuz tamamlama widget'ı — tüm havuzları tamamlama yüzdesiyle gösteriyor
- ✅ `useStudentPoolStatus` hook'u eklendi, pool-status endpoint'ine bağlı
- ✅ Course.DepartmentId NOT NULL migration: `CourseDepartmentIdNotNull` — null course'ları koordinatörlüğe taşıyıp sütunu NOT NULL yapıyor
- ✅ SeedData'da null Course.DepartmentId cleanup (TBK'ya ata)

## Üçüncü Faz Tamamlaması (Önceki Turda)
- ✅ Ders açma ekranı 4 sekmeli tab bar (Tümü / Zorunlu / Havuzlar / Seçmeli) + sayaç
- ✅ Curriculum Publish endpoint: `POST /api/curriculum/{id}/publish`
- ✅ Curriculum Archive endpoint: `POST /api/curriculum/{id}/archive`
- ✅ Elective pool Create/Update/Delete Published guard'ları
- ✅ Cross-list UI: Ders kataloğu detay paneli → "İlişkiler" tabına cross-list bölümü eklendi
- ⚪ Course.DepartmentId NOT NULL migration atlandı — 30+ dosyada null check var, breaking change riski. Validation katmanında zorunluluk sağlanıyor (AdminCourseCatalogService), açma servisinde sahipsiz ders reddediliyor. Veri modeli açısından nullable kalsa da pratikte her yeni ders sahipli oluşturulmak zorunda.

## Gelecek İterasyon (Opsiyonel — Scheduler Etkisi Gerektiren)
- Ders açma akışında cross-list farkındalığı: iki kod gerçekten tek şubede öğretilirken otomatik paylaşım (şu anda iki ayrı section açılıyor)
- Öğrenci dashboard'ında havuz tamamlama widget'ı (endpoint hazır, UI bağlanmadı)

### Madde 1 — Detay
**Yapıldı:**
- DTO: `CurriculumCourseForOpeningDto` + havuz bilgisi (Source, ElectivePoolId, ElectiveGroupCode, ElectiveGroupName, ElectiveSubtype)
- Servis: `GetCurriculumCoursesForOpeningAsync` iki kaynaktan (CurriculumCourses + CurriculumElectivePool→ElectiveGroup→ElectiveGroupCourse) besleniyor
- TS tipi güncellendi
- `CurriculumCourseRow` havuz dersi için kod rozeti (NDE/TE/OC vs.) gösteriyor
- Backend build: 0 hata 0 uyarı
- Frontend tsc: 0 hata

Güncellemeler bu dosyada tutulur.
