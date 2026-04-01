# Ders Programlama Sistemi — Kapsamli Teknik Spesifikasyon

Bu dokuman, profesyonel bir Ogrenci Bilgi Sistemi'nin (OBS) ders programlama modulunun
tum akisini, parametrelerini, constraint'lerini, algoritmasini ve edge case'lerini tanimlar.

Arastirma kaynaklari: UniTime (MIT, Purdue), UConn classroom optimization,
USC section scheduling, Iowa time patterns, YOK 2547 sayili kanun,
Turk universitesi OBS uygulamalari.

---

## 1. AKADEMIK TAKVIM VE DONEM YASAM DONGUSU

### 1.1 Akademik Takvim

Akademik takvim yilin basinda SystemAdmin tarafindan tanimlanir ve TUM sureclerin tek kaynagi olur.
Takvimde su etkinlikler tanimlidir (TermCalendarEvent):

| Etkinlik Tipi | Ornek | Tetikledigi Islem |
|--------------|-------|-------------------|
| CourseRegistration | 16-20 Subat | RegistrationOpen gecisi |
| ClassesStart | 23 Subat | Derslerin baslamasi |
| AddDrop | 2-6 Mart | AddDrop gecisi |
| CourseWithdrawal | 3 Nisan (son gun) | WithdrawalPeriod sonu |
| MidtermExam | 6-11 Nisan | Bilgilendirme |
| ClassesEnd | 5 Haziran | Derslerin bitmesi |
| FinalExam | 8-19 Haziran | GradeEntry gecisi |
| FinalGradePublication | 24 Haziran | Not yayimlama |
| MakeupExam | 29 Haziran - 4 Temmuz | Butunleme |
| MakeupGradePublication | 8 Temmuz | Donem kapanisi |
| NationalHoliday | 23 Nisan, 1 Mayis, 19 Mayis | Ders yapilmayan gunler |
| ReligiousHoliday | Ramazan/Kurban Bayrami | Ders yapilmayan gunler |

### 1.2 Donem Durum Gecisleri

Donem durumu: `Draft -> Planning -> Scheduling -> Published -> RegistrationOpen -> AddDrop -> WithdrawalPeriod -> GradeEntry -> Closed -> Archived`

**Gecis Tetikleyicileri:**

| Gecis | Tetikleyen | Otomasyon |
|-------|-----------|-----------|
| Draft -> Planning | SystemAdmin manuel | Guard: tarihler + derslik atamasi |
| Planning -> Scheduling | SystemAdmin manuel | Guard: tum section'larda hoca atanmis |
| Scheduling -> Published | Dekan onayi sonrasi SystemAdmin | Guard: 0 hard conflict |
| Published -> RegistrationOpen | `RegistrationStart` tarihi gelince **otomatik** | BackgroundService |
| RegistrationOpen -> AddDrop | `AddDropStart` tarihi gelince **otomatik** | BackgroundService |
| AddDrop -> WithdrawalPeriod | `AddDropEnd` tarihi gelince **otomatik** | BackgroundService |
| WithdrawalPeriod -> GradeEntry | `WithdrawalDeadline` gecince **otomatik** | BackgroundService |
| GradeEntry -> Closed | SystemAdmin manuel (tum notlar girildikten sonra) | Guard: tum section'lar finalize |
| Closed -> Archived | SystemAdmin manuel (butunleme sonrasi) | - |

**SystemAdmin Override:**
- Herhangi bir otomatik gecisi erteleyebilir (tarihi uzatir)
- Herhangi bir gecisi erken tetikleyebilir (manuel transition)
- Kapanmis bir donemi gecici olarak geri acabilir (sistem hatasi durumunda)
- Override islemleri AuditLog'a kaydedilir

**BackgroundService Tasarimi:**
```
TermTransitionBackgroundService:
  Her 1 dakikada aktif donemin takvim tarihlerini kontrol eder
  Tarih gelmis ve gecis yapilmamissa otomatik transition yapar
  Gecis guard'lari basarisiz olursa SystemAdmin'e bildirim gonderir
  Loglama: her kontrol ve gecis kaydedilir
```

### 1.3 Zamanlanmis Background Job'lar

| Job | Zamanlama | Islem |
|-----|----------|-------|
| TermTransitionJob | Her 1 dk | Takvim tarihlerine gore otomatik gecis |
| DraftRegistrationCleanup | Her gece 00:00 | Onaylanmamis Draft kayitlari iptal + kapasite serbest |
| ExpiredHoldCleanup | Her gece 00:00 | Suresi dolmus soft hold'lari temizle |
| GpaBatchRecalculation | Donem kapanisinda | Toplu GPA yeniden hesaplama |

---

## 2. KAYNAK TAHSISI (RESOURCE ALLOCATION)

### 2.1 Derslik-Fakulte Atamasi

**Surec:**
1. SystemAdmin donem Draft'tayken derslik atamasini yapar
2. Sistem akilli oneri sunar (asagidaki algoritmaya gore)
3. SystemAdmin oneriyi kabul eder veya elle duzeltir
4. Draft -> Planning guard'i: en az 1 ClassroomTermAssignment zorunlu

**Akilli Oneri Algoritmasi:**
```
Her fakulte icin:
  1. FacultyLoad = toplam acilmasi beklenen section * haftalik saat
     (onceki donem verisinden veya mufredat analizinden)
  2. StudentDensity = fakultedeki aktif ogrenci sayisi
  3. LabNeed = lab dersi section orani

Derslik dagilimi:
  - Amfi (100+): en yogun fakulteye once
  - Lab/Computer: ilgili bolumu olan fakulteye
  - Normal sinif (30-80): FacultyLoad oraninda dagit
  - Kucuk sinif (<30): seminer/tutorial yogunluguna gore

Ayni binadaki derslikler mumkunse ayni fakulteye (gecis suresi minimizasyonu)
```

**Endpoint:**
- `POST /api/admin/terms/{termId}/classroom-assignments/suggest` — sistem oneri uretir
- `GET/POST/DELETE /api/admin/classroom-assignments` — CRUD (MEVCUT)

### 2.2 Derslik Tipleri

| Tip | Kapasite Araligi | Kullanim |
|-----|-----------------|----------|
| Lecture | 30-80 | Normal ders |
| Amphitheater | 80-400 | Buyuk ders (kontenjan >= SystemConfig AmphiThreshold) |
| Lab | 20-40 | Laboratuvar dersi |
| Computer | 20-40 | Bilgisayar laboratuvari |
| Seminar | 10-25 | Seminer, tutorial |

**NOT:** `ClassroomType` enum'ina `Amphitheater` ve `Seminar` eklenmeli (mevcut: Lecture, Lab, Computer).

### 2.3 Zaman Slot Grid'i

**Parametreler (tumu SystemConfig'den okunur):**

| Parametre | Varsayilan | Aciklama |
|-----------|-----------|----------|
| Schedule.SlotDurationMinutes | 50 | Bir ders slotunun suresi |
| Schedule.BreakBetweenSlotsMinutes | 5 | Iki ders arasi mola |
| Schedule.MorningStartTime | 09:00 | Sabah ilk ders |
| Schedule.LunchBreakStart | 11:40 | Ogle arasi baslangic |
| Schedule.LunchBreakEnd | 12:20 | Ogle arasi bitis |
| Schedule.EveningStartTime | 20:10 | Aksam dersleri baslangic |
| Schedule.EveningSlotCount | 2 | Aksam ders slot sayisi |
| Schedule.WorkDays | Mon-Fri | Ders yapilan gunler |

**Uretilen Grid Ornegi (varsayilan parametrelerle):**
```
Sabah:   09:00-09:50, 09:55-10:45, 10:50-11:40
         [11:40-12:20 OGLE ARASI]
Ogleden sonra: 12:20-13:10, 13:15-14:05, 14:10-15:00, 15:05-15:55, 16:00-16:50, 16:55-17:45
         [17:45-20:10 BOS]
Aksam:   20:10-21:00, 21:05-21:55
```

**Grid donem bazli:** Planning'e geciste otomatik olusturulur, donem boyunca degismez.
SystemConfig degisikligi sadece sonraki doneme yansir.

**Tatil gunleri:** Akademik takvimdeki NationalHoliday ve ReligiousHoliday gunlerinde
TimeSlot olusturulmaz. Ancak telafi dersleri icin SystemAdmin o gunun slotlarini
manuel ekleyebilir.

---

## 3. DERS ACMA SURECI (PLANNING ASAMASI)

### 3.1 Rollover (Onceki Donemden Devretme)

Planning'e gecildiginde sistem onceki yilin ayni doneminin (ornegin onceki guz) ders acma
verilerini referans olarak gosterir:

```
Onceki donem: 2025-2026 Guz
  - BSM101 Programlama: 3 section, 40+40+40 kontenjan, toplam 108 kayit
  - BSM201 Veri Yapilari: 2 section, 60+60 kontenjan, toplam 95 kayit
  
Bu donem icin oneri:
  - BSM101: tahmin 115 ogrenci -> 3 section x 40 kontenjan onerisi
  - BSM201: tahmin 100 ogrenci -> 2 section x 50 kontenjan onerisi
```

### 3.2 DepartmentManager Ders Acma Ekrani

**Birlesik Endpoint:** `GET /api/course-opening/{departmentId}/terms/{termId}/courses-with-forecast`

Her ders icin tek ekranda gosterilen bilgiler:

```
+-----------+------------------+------+--------+------------------+-------------------------+
| Ders Kodu | Ders Adi         | AKTS | Donem  | Talep Tahmini    | Oneri                   |
+-----------+------------------+------+--------+------------------+-------------------------+
| BSM101    | Programlama I    | 6    | 1.Guz  | 115 kisi         | 3 section x 40          |
|           |                  |      |        |  - Normal: 80    |                         |
|           |                  |      |        |  - Alttan: 35    |                         |
|           |                  |      |        |  (FF:20 NA:10 U:5)|                        |
+-----------+------------------+------+--------+------------------+-------------------------+
| MATH101   | Calculus I       | 6    | 1.Guz  | 280 kisi         | 1 amfi(120) + 2x80      |
|           |                  |      |        |  - BSM: 80       | veya 4 section x 70     |
|           |                  |      |        |  - EEM: 60       |                         |
|           |                  |      |        |  - MAT: 40       |                         |
|           |                  |      |        |  - Alttan: 100   |                         |
|           | CROSS-DEPT: EEM, |      |        |                  |                         |
|           | MAT, FIZ de alir |      |        |                  |                         |
+-----------+------------------+------+--------+------------------+-------------------------+
```

**Cross-Department Talep Bilgisi:**
- Sorgu: `CurriculumCourse -> Curriculum -> Program -> Department`
- Ayni dersi mufredatinda tasiyan TUM bolumleri ve o bolumlerin ogrenci sayilarini gosterir
- DepartmentManager bunu gorup CourseAccessPolicy'yi dogru ayarlar

**Akilli Asistan (Section Sayisi Onerisi):**
```
Oneri algoritmasi:
  1. Toplam talep = normal + alttan + cross-department
  2. Fakulteye atanmis derslik kapasitelerini al
  3. Olasi kombinasyonlar:
     - 280 kisi: 1x Amfi(120) + 2x Normal(80) = 280 ✓
     - 280 kisi: 4x Normal(70) = 280 ✓
     - 280 kisi: 3x Normal(80) = 240 ✗ (yetersiz)
  4. En az section sayisini oncelikli oner
  5. Derslik tipi uyumunu kontrol et (lab dersi -> lab derslik)
```

### 3.3 Hoca Atama

Hoca atamasi **Planning asamasinda** yapilir. DepartmentManager her section'a hoca atar.

**Hoca Atama Sirasinda Gosterilen Bilgiler:**
- `GET /api/course-opening/instructors/{instructorId}/workload?termId=X`
- Hocanin mevcut atanmis ders saati / MaxWeeklyHours
- Hocanin bu donem verdigi diger dersler
- Uyari: MaxWeeklyHours asiliyor mu?

**YOK 2547 Ders Yuku Tablosu (SystemConfig'den):**

| Unvan | Zorunlu Saat | Max Ek Ders | Toplam Max |
|-------|-------------|------------|-----------|
| Profesor | 10 | 20 | 40 |
| Docent | 10 | 16 | 36 |
| Dr. Ogr. Uyesi | 10 | 12 | 32 |
| Ogr. Gor. | 12 | 8 | 30 |

### 3.4 CourseAccessPolicy (Cross-Department Erisim)

Ders acildiginda otomatik olarak kendi bolumune policy olusur.
DepartmentManager cross-department talep bilgisini gorup ek bolum/fakultelere acar.

```
Ornek:
  MATH101 Calculus I aciliyor (Matematik Bolumu)
  Sistem gosteriyor: "Bu dersi BSM(80), EEM(60), FIZ(40) bolumleri de alacak"
  DepartmentManager: BSM, EEM, FIZ bolumlerine CourseAccessPolicy ekler
  -> Bu bolumlerin ogrencileri artik MATH101'i katalogda gorur ve kayit olabilir
```

### 3.5 Planning -> Scheduling Gecis Guard'lari

```
Kontrol listesi:
  [ ] En az 1 aktif CourseSection var mi?
  [ ] Her aktif section'da instructor atanmis mi?
  [ ] MaxWeeklyHours asilan hoca var mi? (uyari, engel degil)
  [ ] Mufredattaki zorunlu derslerden acilmamis var mi? (uyari)
  [ ] Her dersin yeterli section sayisi var mi? (forecast ile karsilastirma, uyari)
```

---

## 4. OTOMATIK PROGRAMLAMA ALGORITMASI (SCHEDULING ASAMASI)

### 4.1 Algoritma Mimarisi: 2 Fazli (ilk versiyon)

```
+-------------------+     +-------------------+
| Faz 1             |     | Faz 2             |
| Greedy            | --> | Manuel Review     |
| Construction      |     | + Duzeltme        |
| + Backtracking    |     |                   |
+-------------------+     +-------------------+
  Feasible cozum            Insan inceleme
  Hard constraint'ler       Swap, Lock, Apply
  + Soft scoring            Dekan onayi
```

**NOT:** Performans yetmezse veya soft constraint optimizasyonu zayif kalirsa,
Faz 1 ile Faz 2 arasina Tabu Search (local search) eklenebilir. Ilk versiyonda
Greedy + Backtracking yeterli — anlasilir, debug edilebilir, 500-1000 section icin hizli.

### 4.2 Faz 1: Greedy Construction (Ilk Feasible Cozum)

**Girdi:**
- Tum aktif section'lar (facultyId bazli)
- TimeSlot grid'i (donem icin olusturulmus)
- Classroom listesi (fakulteye atanmis — ClassroomTermAssignment)
- Constraint seti (H1-H13, H15, S1-S9, S12)
- Onceki donem section atamalari (incremental scheduling icin)

**Adim 1: Section Onceliklendirme**

Section'lar su sirada yerlestirilir (zorluktan kolaya — MRV prensibi):

```
PriorityScore hesaplama:

  // Derslik kisitliligi (dar domain = once yerlesir)
  availableClassrooms = ClassroomTermAssignment ile fakulteye atanmis
                        VE capacity >= section.Capacity
                        VE type uyumlu (lab->lab, 100+->amfi)
  domainSize = availableClassrooms.Count * availableTimeSlots.Count
  
  score = 0
  
  // 1. Derslik kisitliligi (en onemli)
  score += (1.0 / domainSize) * 1000
  
  // 2. Zorunlu ders (cakisma riski yuksek)
  if CurriculumCourse.IsCompulsory: score += 500
  
  // 3. Buyuk ders (amfi gerektirir, az derslik secenegi)
  if section.Capacity >= AmphiThreshold: score += 400
  
  // 4. Lab dersi (sadece lab derslik kullanabilir)
  if course.LabHours > 0 && course.TheoryHours == 0: score += 300
  
  // 5. Cok bilesenli ders (lecture + lab birlikte ele alinmali)
  if ayni course'un baska section tipleri (lecture+lab) varsa: score += 200
  
Section'lar PriorityScore AZALAN sirada siralanir (yuksek = once)
```

**Adim 2: Her Section Icin Slot+Derslik Secimi**

```
Her section icin:
  1. DOMAIN OLUSTUR:
     slotKombinasyonlari = dersin saat gereksinimine gore ardisik slot bloklari
       - TheoryHours=3 -> tek gunde 3 ardisik slot
       - TheoryHours=4 -> 2 gunde 2'ser ardisik slot
       - TheoryHours=3, LabHours=2 -> 3 ardisik slot (theory) + 2 ardisik slot (lab, farkli gun)
       - TheoryHours=5 -> 3+2 ardisik slot (2 farkli gun)
     
     uygunDerslikler = ClassroomTermAssignment'tan fakulteye atanmis
                       VE capacity >= section.Capacity
                       VE tip uyumlu
     
     domain = her (slotKombinasyonu, derslik) cifti

  2. HARD CONSTRAINT FILTRELE:
     domain'den H1-H13, H15 ihlal edenleri cikar
     (detay: Bolum 5)

  3. SOFT CONSTRAINT SKORLA:
     Kalan her (slot, derslik) cifti icin penalty hesapla
     (detay: Bolum 6)
     
     // Incremental bonus
     if oncekiDonem.ayniCourse.ayniInstructor.ayniSlot:
       penalty -= IncrementalBonus (SystemConfig, varsayilan: 5)

  4. EN IYI CIFTI SEC:
     penalty ARTAN sirada sirala
     Ilk cifti sec ve ata

  5. ATANAMAZSA (domain bos):
     section'i "unassigned" listesine ekle
     Basit backtracking dene:
       - Bu section'la cakisan en dusuk oncelikli section'i bul
       - O section'in atamasini geri al
       - Ikisini de farkli sirada tekrar yerlestirmeyi dene
       - Max 3 seviye derinlik
       - Basarisizsa: "yerlestirilmedi" raporla
```

**Adim 3: Ayni Dersin Bilesenlerini Birlikte Ele Alma**

```
Ayni Course'un lecture + lab section'lari varsa:
  1. Once lecture section'i yerlesir (oncelik skoru daha yuksek)
  2. Lab section'(lar)i lecture ile CAKISMAYACAK sekilde yerlestirilir (H6)
  3. Corequisite (H11): en az 1 lecture-lab kombinasyonu cakismasiz olmali
     Ornek: Fizik I Lecture(Sal 09:00) + Fizik I Lab Sec1(Car 14:00) = OK
            Fizik I Lecture(Sal 09:00) + Fizik I Lab Sec2(Sal 09:00) = CAKISMA
            En az 1 lab section cakismasiz olduğu surece OK
```

### 4.3 Faz 2: Manuel Review ve Duzeltme

Otomatik scheduling sonrasi insan incelemesi.

**Review Ekrani Icin Endpoint'ler:**

```
GET /api/scheduling/faculty/{facultyId}/summary?termId=X
  -> Toplam section, atanan, atanamayan, conflict sayisi, soft skor

GET /api/scheduling/faculty/{facultyId}/unassigned?termId=X
  -> Yerlestirilemyen section'lar + neden yerlesmedi bilgisi

GET /api/scheduling/department/{departmentId}/timetable?termId=X&year=Y
  -> Bolumun belirli sinif seviyesinin haftalik programi

GET /api/scheduling/instructor/{instructorId}/timetable?termId=X
  -> Hocanin haftalik programi

GET /api/scheduling/classroom/{classroomId}/timetable?termId=X
  -> Dersligin haftalik doluluk tablosu

GET /api/scheduling/conflicts?termId=X
  -> Tum cakismalar (hard + soft), gruplandirmali

GET /api/scheduling/conflicts/{sectionId}/alternatives
  -> Bu section icin uygun alternatif slot+derslik listesi
```

**Manuel Duzeltme Endpoint'leri:**

```
POST /api/scheduling/apply
  Body: { sectionId, classroomId, scheduleSlots: [...] }
  -> Tekil section'a slot+derslik ata (MEVCUT, constraint kontrolu ile)

POST /api/scheduling/swap
  Body: { sectionId1, sectionId2, swapSlots: true, swapClassrooms: true }
  -> Iki section'in atamalarini takas et
  -> Her iki section icin hard constraint kontrolu
  -> Ihlal varsa: swap iptal, hata dondur

POST /api/scheduling/lock/{sectionId}
POST /api/scheduling/unlock/{sectionId}
  -> Section programini kilitle/ac
  -> Kilitli section: BatchAutoSchedule atlat, swap'a izin verme
  -> Manuel ApplyAsync ile hala degistirilebilir (bilincli karar)

POST /api/scheduling/auto-resolve?termId=X
  -> Sistem cakismalari otomatik cozmeyi dener
  -> Her cakisan section icin alternatif slot bul
  -> En az toplam cakisma birakan atamayi sec
  -> Cozulemeyenleri listele

POST /api/scheduling/batch-auto-schedule
  Body: { termId, facultyId?, departmentId?, forceRegenerate: false }
  -> Fakulte veya bolum bazli toplu otomatik programlama
  -> forceRegenerate=true: kilitli olmayan tum section'lari sifirla ve tekrar yerlesir
  -> forceRegenerate=false: sadece atamasiz section'lari yerlesir (varsayilan)
```

### 4.5 Incremental Scheduling (Onceki Donemden Devretme)

```
Islem:
  1. Onceki yilin ayni doneminin (ornegin 2024-2025 Guz) SectionSchedule kayitlarini yukle
  2. Her section icin eslesme: ayni CourseId + ayni InstructorId
  3. Eslesen section'lar icin onceki donemteki slot+derslik "preferred" olarak isaretlenir
  4. Scoring'de preferred slot'a IncrementalBonus kadar dusuk penalty verilir
  5. IsScheduleLocked = true olan section'lar otomatik scheduling'den haric tutulur
  6. Sadece yeni eklenen veya degisen (hoca degisikligi vb.) section'lar sifirdan yerlestirilir
```

### 4.6 Fakulte Bazli vs Bolum Bazli

**Uretim sistemlerinden ogrenilen:**
Purdue modeli gibi decompose:
1. Buyuk amfi dersleri (fakulte geneli, sinirli kaynak) -> ONCE yerlesir
2. Bolum bazli (her bolum bagimsiz) -> PARALEL yerlesebilir
3. Cross-department ortak dersler -> SON (en esnek)
4. Cross-constraint check -> TUM yerlestirmeler sonunda

**Bizim sistem icin:**
- `BatchAutoScheduleAsync` `facultyId` parametresi alir
- Fakultedeki TUM bolumlerin section'larini toplar
- Global oncelik sirasina gore siralar
- H5 (ayni bolum/yil zorunlu ders cakismasi) boylece dogru calisir
- H10 (fakulte derslik kisiti) on-filtre olarak uygulanir

---

## 5. HARD CONSTRAINT'LER (IHLAL EDILEMEZ)

Hard constraint ihlal eden atama KESINLIKLE yapilmaz. Scheduling sonucunda 0 hard conflict olmali.

### 5.1 Mevcut (Implemente Edilmis)

| Kod | Constraint | Kontrol Yeri | Durum |
|-----|-----------|-------------|-------|
| H1 | Hoca cakismasi: ayni hoca ayni slotta iki derse atanamaz | EvaluateCandidateConflicts | TAMAM |
| H2 | Derslik cakismasi: ayni derslik ayni slotta iki derse atanamaz | EvaluateCandidateConflicts | TAMAM |
| H3 | Derslik kapasitesi >= section kontenjani | EvaluateCandidateConflicts | TAMAM |
| H4 | Derslik tipi: Lab -> Lab derslik | EvaluateCandidateConflicts | KISMI |
| H5 | Ayni bolum/sinif zorunlu ders cakismasi | EvaluateCandidateConflicts | TAMAM |
| H6 | Ayni dersin lecture vs lab bilesen cakismasi | EvaluateCandidateConflicts | TAMAM |
| H8 | Ayni dersin farkli subeleri ayni slotta olamaz | EvaluateCandidateConflicts | TAMAM |
| H9 | Hoca haftalik saat limiti (MaxWeeklyHours) | EvaluateCandidateConflicts | TAMAM |
| H10 | Fakulteye atanmamis derslik kullanilamaz | EvaluateCandidateConflicts | TAMAM |

### 5.2 Eklenmesi Gereken

| Kod | Constraint | Detayli Aciklama | Gerekli Degisiklik |
|-----|-----------|-----------------|-------------------|
| H4+ | Amfi zorunlulugu | kontenjan >= SystemConfig("Classroom.AmphiThreshold", 100) -> Amphitheater tipi derslik zorunlu | ClassroomType enum'a Amphitheater ekle, H4 kontrolunu genislet |
| H11 | Corequisite cakismasizlik | Bir dersin lecture section'i ile bagli lab/recitation section'i arasinda en az bir cakismasiz kombinasyon olmali | CoursePrerequisite'e IsCorequisite bool alani ekle |
| H12 | Saat-blok uyumu | Dersin TheoryHours+LabHours toplamina gore dogru sayida ardisik slot blogu atanmali. 3 saat = 3 ardisik, 4 saat = 2 gun x 2, 5 saat = 3+2 | ResolveDayBlocks mantigi kontrol olarak eklenir |
| H13 | Ogle arasi korumasi | SystemConfig'den okunan ogle arasi saatlerinde (LunchBreakStart/End) ders atanamaz. **SystemAdmin her donem basinda bu saatleri ve hard/soft modunu degistirebilir.** Ornek: guz doneminde 12:30-13:00 hard, bahar doneminde 13:00-13:30 soft. Degisiklik yapilirsa TimeSlot grid'i yeniden uretilmeli. | SystemConfig: Schedule.LunchBreakStart, Schedule.LunchBreakEnd, Schedule.LunchBreak.IsHard (varsayilan: true) |
| H15 | Max gunluk ardisik blok | Bir hocanin araliksiz max N saat (SystemConfig, varsayilan: 4) art arda dersi. Daha fazlasi fiziksiz surudurulemez | SystemConfig: Schedule.MaxConsecutiveHours |

---

## 6. SOFT CONSTRAINT'LER (BEST-EFFORT, KONFIGURE EDILEBILIR)

Soft constraint ihlalinde atama engellenmez, scoring'de ceza puani verilir.
Her biri SystemConfig'den enable/disable edilebilir, agirligi (weight) ayarlanabilir.

### 6.1 Mevcut (Implemente Edilmis)

| Kod | Constraint | SystemConfig Key | Varsayilan Agirlik |
|-----|-----------|-----------------|-------------------|
| S1 | Derslik doluluk orani (%60-85 arasi ideal) | SoftConstraint.S1.Enabled, Classroom.OccupancyTarget.Min/Max | w=3 |
| S2 | Hoca gunluk ders yogunlugu (max N saat/gun) | SoftConstraint.S2.Enabled, SoftConstraint.S2.MaxDailyHours | w=2 |
| S3 | Slot dagilim dengesi (prime-time yigilma) | SoftConstraint.S3.Enabled, SoftConstraint.S3.MaxSectionsPerSlot | w=2 |

### 6.2 Eklenmesi Gereken

| Kod | Constraint | Aciklama | Varsayilan Agirlik | SystemConfig Key |
|-----|-----------|----------|-------------------|-----------------|
| S4 | Cross-department cakisma | Tarihsel veriden sik birlikte alinan ders ciftleri cakismamali. Son 3 donemde ayni ogrencinin birlikte aldigi ders ciftlerini analiz et, yuksek frekanslilari penalti | w=3 | SoftConstraint.S4.Enabled, SoftConstraint.S4.MinCoPairThreshold (varsayilan: 20 ogrenci) |
| S5 | Ogle arasi tercihi | H13 hard ise S5 devre disi. H13 kapali ise, ogle arasi saatlerine ders atanmamasi tercih edilir | w=2 | SoftConstraint.S5.Enabled |
| S7 | Ogrenci ders arasi bosluk | Ayni bolum/yil ogrencilerinin gun icerisinde 3+ ardisik bos slotu olmamasi. Zorunlu derslerin mumkunse ardisik veya yakin slotlara yerlestirilmesi | w=2 | SoftConstraint.S7.Enabled, SoftConstraint.S7.MaxGapSlots (varsayilan: 3) |
| S8 | Hoca gun sayisi minimizasyonu | Hocanin dersleri mumkun oldugunca az gune sikistirilir. 12 saat yuk: 3 gune 4'er saat > 5 gune 2-3'er saat | w=1 | SoftConstraint.S8.Enabled |
| S9 | Erken sabah / gec aksam cezasi | Gunun ilk slotu ve son slotlarina atama cezasi. Bu slotlar son care | w=1 | SoftConstraint.S9.Enabled |
| S12 | Hafta ici dengeli dagilim | Haftanin her gunune yaklasik esit section sayisi. Pazartesi'ye 50, Cuma'ya 10 section -> dengesiz | w=1 | SoftConstraint.S12.Enabled |

### 6.3 Soft Constraint Penalty Hesaplama

```
totalPenalty = 0

for each enabledConstraint in softConstraints:
  violation = calculateViolation(constraint, currentAssignment)
  weight = SystemConfig("SoftConstraint.Weight." + constraint.code)
  totalPenalty += violation * weight

// Incremental bonus (negatif penalty = iyi)
for each section with oncekiDonemMatch:
  totalPenalty -= SystemConfig("SoftConstraint.Weight.IncrementalBonus", 5)

// Dusuk totalPenalty = daha iyi cozum
```

**Varsayilan Agirliklar (SystemConfig seed data):**
```
SoftConstraint.Weight.S1 = 3
SoftConstraint.Weight.S2 = 2
SoftConstraint.Weight.S3 = 2
SoftConstraint.Weight.S4 = 3
SoftConstraint.Weight.S5 = 2
SoftConstraint.Weight.S7 = 2
SoftConstraint.Weight.S8 = 1
SoftConstraint.Weight.S9 = 1
SoftConstraint.Weight.S12 = 1
SoftConstraint.Weight.IncrementalBonus = 5
```

---

## 7. YAYIMLAMA (PUBLISHED) GUARD'LARI

### 7.1 Scheduling -> Published Gecis Kontrolleri

```
ZORUNLU (gecisi engeller):
  [ ] Tum aktif section'larda instructor atanmis mi?
  [ ] Tum aktif section'larda classroom atanmis mi?
  [ ] Tum aktif section'larda schedule (slot) atanmis mi?
  [ ] ValidateExistingAsync(termId) -> 0 hard conflict mi?
  [ ] Atanamayan section var mi? (varsa uyari, admin override edebilir)

UYARI (gecisi engellemez, raporlanir):
  [ ] Soft constraint uyarilari (S1-S9, S12 ihlalleri ve sayilari)
  [ ] Mufredattaki zorunlu derslerden acilmamis var mi?
  [ ] Derslik doluluk orani hedef disinda olan section'lar
  [ ] Cross-department ortak ders cakisma uyarilari

ONAY:
  [ ] Dekan onayi (Dean rolu ile onay endpoint'i)
  [ ] Dekan onaylamadan Published'a gecilemez
```

### 7.2 Dekan Onayi

```
POST /api/scheduling/faculty/{facultyId}/approve?termId=X
  Roles: Dean
  
  Guard: Dean'in kendi fakultesi mi?
  Islem: FacultyScheduleApproval kaydi olusturur
  
GET /api/scheduling/faculty/{facultyId}/approval-status?termId=X
  -> Onaylandi mi, kim onalyadi, ne zaman
  
Published gecis guard'i: FacultyScheduleApproval kaydi var mi kontrol eder
```

---

## 8. KAYIT ENTEGRASYONU

### 8.1 Ogrenci Ders Katalogunda Program Bilgisi

Published'a gecildiginde ogrenciler katalogu gorur. Her section icin:
- Ders adi, kodu, AKTS
- Hoca adi
- Gun + saat (SectionSchedule -> TimeSlot'tan)
- Derslik adi
- Mevcut kontenjan / toplam kontenjan
- Onkosul durumu (karsilaniyor mu?)

### 8.2 Kayit Sirasinda Cakisma Kontrolu

Ogrenci ders eklediginde anlik kontrol:
```
1. Ogrencinin mevcut plani (RegistrationPlan) yuklenir
2. Yeni eklenecek dersin SectionSchedule'daki slotlari alinir
3. Plandaki diger derslerin slotlariyla karsilastirilir
4. Cakisma varsa:
   - Ilk kez alinan ders cakismasi -> KESIN ENGEL (override yok)
   - Tekrar alinan (FF) ders cakismasi -> UYARI (danisman + bolum baskani onayi ile izin)
```

### 8.3 ScheduleJson -> SectionSchedule Tam Gecis

Tum servisler SectionSchedule'a gectikten sonra:
1. CourseSection.ScheduleJson alanini kaldir
2. ScheduleSlot value object'i kaldir
3. ScheduleJson referansi olan ~19 dosyayi temizle
4. Migration olustur

Bu gecis SCHEDULING ALGORITMASI tamamlandiktan sonra yapilir.

---

## 9. ENTITY DEGISIKLIKLERI

### 9.1 Yeni Alanlar

```csharp
// CoursePrerequisite — corequisite iliskisi
public bool IsCorequisite { get; set; }  // true = ayni donemde birlikte alinmali

// CourseSection — schedule lock
public bool IsScheduleLocked { get; set; }  // true = otomatik scheduling degistiremez

// ClassroomType enum — yeni tipler
Amphitheater,  // 80+ kisilik amfi
Seminar        // 10-25 kisilik seminer odasi
```

### 9.2 Yeni Entity'ler

```csharp
// Dekan programi onayi
public class FacultyScheduleApproval
{
    public int Id { get; set; }
    public int FacultyId { get; set; }
    public Faculty Faculty { get; set; } = null!;
    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;
    public int ApprovedByUserId { get; set; }
    public DateTime ApprovedAt { get; set; }
    public string? Note { get; set; }
}
```

### 9.3 Yeni SystemConfig Kayitlari

```
// Scheduling algoritmasi parametreleri
Scheduling.MaxBacktrackingDepth = 3

// Soft constraint agirliklari
SoftConstraint.Weight.S4 = 3
SoftConstraint.Weight.S5 = 2
SoftConstraint.Weight.S7 = 2
SoftConstraint.Weight.S8 = 1
SoftConstraint.Weight.S9 = 1
SoftConstraint.Weight.S12 = 1
SoftConstraint.Weight.IncrementalBonus = 5

// Soft constraint enable/disable
SoftConstraint.S4.Enabled = true
SoftConstraint.S4.MinCoPairThreshold = 20
SoftConstraint.S5.Enabled = true
SoftConstraint.S7.Enabled = true
SoftConstraint.S7.MaxGapSlots = 3
SoftConstraint.S8.Enabled = true
SoftConstraint.S9.Enabled = true
SoftConstraint.S12.Enabled = true

// Hard constraint toggle
Schedule.LunchBreak.IsHard = true
Schedule.MaxConsecutiveHours = 4
```

---

## 10. UYGULAMA SIRASI

```
ADIM 1: Entity degisiklikleri + migration
  - CoursePrerequisite.IsCorequisite
  - CourseSection.IsScheduleLocked
  - ClassroomType enum guncellemesi (Amphitheater, Seminar)
  - FacultyScheduleApproval entity
  - Yeni SystemConfig seed data
    |
ADIM 2: Guard duzeltmeleri
  - Draft->Planning: derslik atama kontrolu
  - Published: ValidateExistingAsync cagir + Dekan onayi
  - H4: Amfi zorunlulugu
  - SuggestAsync: ClassroomTermAssignment on-filtresi
    |
ADIM 3: Ders acma akisi zenginlestirme
  - Birlesik forecast+ders acma endpoint
  - Cross-department talep bilgisi
  - Section sayisi akilli asistani
  - Hoca yuk bilgisi endpoint
    |
ADIM 4: Eksik hard constraint'ler
  - H11 (corequisite) — IsCorequisite alanina bagli
  - H12 (saat-blok uyumu)
  - H13 (ogle arasi)
  - H15 (max ardisik blok)
    |
ADIM 5: Scheduling algoritmasi yeniden yazi
  - SectionPriorityCalculator (MRV bazli siralama)
  - Greedy Construction (domain filtreleme + skorlama)
  - Limited Backtracking (max 3 seviye)
  - Fakulte bazli calistirma
    |
ADIM 6: Eksik soft constraint'ler
  - S4 (cross-dept cakisma)
  - S5 (ogle arasi soft)
  - S7 (ogrenci bosluk)
  - S8 (hoca gun sayisi)
  - S9 (erken/gec ceza)
  - S12 (hafta ici denge)
    |
ADIM 7: Manuel duzeltme araclari
  - Swap endpoint
  - Lock/Unlock
  - Timetable gorunturleme endpoint'leri
  - Auto-resolve
  - Dekan onayi endpoint
    |
ADIM 8: Otomatik donem gecisleri
  - TermTransitionBackgroundService
  - Takvim tarihlerine gore otomatik gecis
  - Admin override mekanizmasi
    |
ADIM 9: ScheduleJson -> SectionSchedule tam gecis
  - Tum servisleri SectionSchedule'a gecir
  - ScheduleJson ve ScheduleSlot kaldir
  - Migration
    |
ADIM 10: Derslik oneri algoritmasi
  - Akilli derslik-fakulte atama onerisi
  - Ogrenci yogunlugu + ders sayisi analizi
```

---

## 11. AKIS DIYAGRAMI

```
[1. Akademik Takvim Yayimlanir]
  SystemAdmin yillik takvimi tanimlar
  Tum donem tarihleri ve etkinlikler girilir
       |
[2. Donem Otomatik Olusur (Draft)]
  Takvimden turetilir
       |
[3. Derslik-Fakulte Atamasi (Draft)]
  Sistem akilli oneri sunar (yogunluk + kapasite analizi)
  SystemAdmin oneriyi kabul eder veya duzeltir
       |
[4. Draft -> Planning Gecisi]
  Guard: tarihler girilmis + derslik atamasi yapilmis
  TimeSlot grid'i otomatik olusturulur
       |
[5. Ders Acma (Planning)]
  Her bolumun DepartmentManager'i:
  +-- Mufredat dersleri + forecast + cross-dept talep tek ekranda gorur
  +-- Akilli asistan section sayisi ve kontenjan onerir
  +-- Section'lar olusturulur
  +-- Her section'a hoca atanir (MaxWeeklyHours kontrolu ile)
  +-- CourseAccessPolicy otomatik olusur + cross-dept erisim acilir
       |
[6. Planning -> Scheduling Gecisi]
  Guard: tum section'larda hoca atanmis + zorunlu ders kontrolu
       |
[7. Otomatik Programlama (Scheduling)]
  Sistem fakulte bazli butuncul program olusturur:
  +-- Faz 1: Greedy Construction
  |   +-- Section onceliklendirme (MRV + domain-specific)
  |   +-- Her section icin en iyi (slot, derslik) secimi
  |   +-- H1-H13, H15 hard constraint kontrolu
  |   +-- Soft constraint skorlama
  |   +-- Backtracking (max 3 seviye)
  |   +-- Incremental: onceki donemle ayni slot bonus
  +-- Faz 2: Tabu Search Optimizasyonu
  |   +-- Move + Swap komsu cozumler
  |   +-- Soft constraint skor iyilestirme
  |   +-- Max 60 saniye veya 10000 iterasyon
  +-- Sonuc: Feasible cozum + soft skor raporu + atanamayan listesi
       |
[8. Review + Manuel Duzeltme (Scheduling)]
  DepartmentManager / SystemAdmin:
  +-- Timetable goruntuleme (hoca/derslik/bolum bazli)
  +-- Conflict listesi inceleme
  +-- Manuel duzeltme: Apply (slot degistir), Swap (iki ders takas), Lock (kilitle)
  +-- Auto-resolve: sistem cakismalari otomatik cozmeyi dener
  +-- Dongu: duzelt -> validate -> tekrar duzelt (0 hard conflict olana kadar)
       |
[9. Dekan Onayi]
  Dean programi inceler ve onaylar
  FacultyScheduleApproval kaydi olusur
       |
[10. Scheduling -> Published Gecisi]
  Guard:
  +-- Tum section'larda instructor + classroom + schedule atanmis
  +-- ValidateExistingAsync -> 0 hard conflict (zorunlu)
  +-- FacultyScheduleApproval var (Dekan onayi)
  +-- Soft constraint uyarilari raporlanir (engellemez)
       |
[11. Ogrenci Katalogu (Published)]
  Ogrenciler ders katalogun gorur (read-only, kayit henuz kapali)
  Her section: ders adi, hoca, gun+saat, derslik, kontenjan
       |
[12. Published -> RegistrationOpen (OTOMATIK)]
  RegistrationStart tarihi gelince BackgroundService otomatik gecirir
  Ogrenciler ders secmeye baslar
       |
[13. Ders Secimi + Danisman Onayi (RegistrationOpen)]
  Ogrenci:
  +-- Ders katalogundan ders secer (CourseAccessPolicy filtreli)
  +-- Her ders eklemede anlik kontrol: onkosul, AKTS limiti, cakisma, kontenjan
  +-- Tum dersleri sectikten sonra "Danismana Gonder" (RegistrationPlan.Status=Submitted)
  Danisman:
  +-- Tum plani tek ekranda gorur (alttan dersler, mufredat ozeti, cakismalar)
  +-- Onaylar (Approved) veya reddeder (Rejected + not)
  +-- Red sonrasi ogrenci duzeltip tekrar gonderebilir
       |
[14. RegistrationOpen -> AddDrop (OTOMATIK)]
  AddDropStart tarihi gelince otomatik gecis
  Submitted/Draft kayitlar otomatik iptal + kapasite serbest
       |
[15. AddDrop -> WithdrawalPeriod (OTOMATIK)]
  AddDropEnd tarihi gelince otomatik gecis
       |
[16. WithdrawalPeriod -> GradeEntry (OTOMATIK)]
  WithdrawalDeadline gecince otomatik gecis
       |
[17. Not Girisi (GradeEntry)]
  Instructor not girer, section finalize eder
       |
[18. GradeEntry -> Closed (MANUEL)]
  SystemAdmin: tum notlar girildi mi kontrol et, donemi kapat
  Sinif atlatma kurallari otomatik uygulanir
       |
[19. Closed -> Archived (MANUEL)]
  Butunleme sonrasi arsivle
```
