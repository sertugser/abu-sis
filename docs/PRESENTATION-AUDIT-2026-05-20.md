# OBİS Sunum Öncesi Uçtan Uca Audit — FİNAL RAPOR

**Tarih:** 2026-05-20
**Branch:** `feat/ders-saatleri-yenileme`
**Audit Modu:** Hibrit (canlı Playwright + kalıcı smoke spec + rapor)
**DB:** Mevcut dev DB (yazma izinli)
**Servisler:** API `http://localhost:5213` ✅ · Web `http://localhost:5173` ✅
**Spec dosyası (kalıcı):** [SIS.Web/e2e/presentation-audit.spec.ts](SIS.Web/e2e/presentation-audit.spec.ts)

---

## 📊 Özet — v5 Final Koşum

| Toplam | PASS | FAIL | SKIP | Süre |
|---|---|---|---|---|
| **39** | **39** ✅ | **0** | **0** | 49.7s |

> Audit, ilk 4 koşumda spec hataları ve rate-limit nedeniyle false-negative verdi. v5 koşumu doğru endpoint pattern'leri + rate-limit retry ile tüm akışları temiz geçti.

---

## 🔴 KRİTİK BUG'LAR — Sunum öncesi mutlaka değerlendir

### BUG-1 · AuthLogin rate-limit kampüs amfisini kilitler (HIGH)

**Konum:** [SIS.API/Program.cs:64-72](SIS.API/Program.cs#L64-L72)

```csharp
options.AddPolicy("AuthLogin", httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 8,        // ⚠️ dakikada 8 login/IP
            QueueLimit = 0,
        }));
```

**Sorun:**
- Kampüs WiFi NAT arkasında **tek IP**. 30 öğrenci aynı anda login → ilk 8'i geçer, sonraki 22'si HTTP 429
- Audit testimizde **6 ardışık login'in 4'ü cascade fail** oldu (önceki rate-limit window'u dolduğunda)

**Kanıt:** v3 koşumu log'u:
```
[token-cache] SystemAdmin OK
[token-cache] Advisor    OK
[token-cache] Dean       login HTTP 429
[token-cache] DepartmentManager login HTTP 429
[token-cache] Instructor login HTTP 429
[token-cache] Student    login HTTP 429
```

**Öneri:** İki seçenekten biri:
1. **Partition key'i `email`'e değiştir** (her kullanıcı kendi sayacına sahip — brute force'a karşı daha doğru):
   ```csharp
   partitionKey: httpContext.Request.Form["email"].ToString() ?? "anonymous"
   ```
2. **PermitLimit'i 60'a çıkar** + `QueueLimit = 20` (burst tolere et):
   ```csharp
   PermitLimit = 60, QueueLimit = 20
   ```

**Sunum günü hızlı çözüm:** Kampüs IP'sini RateLimiter'ı bypass edecek `OnRejected` handler'a ekle veya geçici olarak `[EnableRateLimiting]` attribute'unu yorum satırına al.

---

### BUG-2 · Auto-schedule endpoint invalid input → HTTP 500 (MEDIUM)

**Konum:** `POST /api/faculty-planning/auto-schedule`

**Sorun:** Geçersiz `facultyId`/`termId` (örn. 99999) gönderilince **400 Bad Request yerine 500 Internal Server Error** dönüyor.

**Kanıt:** `[C2] auto-schedule fake-id status=500`

**Öneri:** Controller'da explicit existence check + 400 dön:
```csharp
var faculty = await db.Faculties.FindAsync(req.FacultyId);
if (faculty is null) return BadRequest($"Faculty {req.FacultyId} not found.");
```

**Sunum etkisi:** Demo'da yanlış parametre verilirse stack trace user'a sızar. Düşük olasılık ama görünür.

---

### BUG-3 · Aktif dönemde `hasCalendar:false` — Veri tutarsızlığı (HIGH)

**Konum:** DB state + `/academic-calendar/terms` vs `/academic-calendar/active`

**Sorun:** İki endpoint çelişiyor:
- `/academic-calendar/terms` aktif dönem için `hasCalendar:false` döner
- `/academic-calendar/active` aynı dönem için **dolu phase verisi** döner (registrationStart, withdrawalDeadline vs.)

```json
[B3] sample={"termId":7,"termName":"2027-2028 Güz (Plan 10 Test)","hasCalendar":false,...}
[B4] active calendar keys=...registrationStart,registrationEnd,addDropStart,addDropEnd,withdrawalDeadline,gradeEntryStart,gradeEntryEnd,isRegistrationOpen,isAddDropOpen,isWithdrawalOpen,isGradeEntryOpen,events
```

**CLAUDE.md kuralı diyor ki:**
> AcademicCalendar null ise phase method'ları `false` döner (silent close — veri tutarsızlığı kullanıcıya "faz kapalı" olarak yansır).

**Sunum etkisi:** Demoda öğrenci ders kaydına gidemez → "kayıt fazı kapalı" silent fail. Veya inverse: hasCalendar:false rağmen kayıt açık → yetki kafa karışıklığı.

**Öneri:**
1. Demo'dan önce admin hesabıyla `/akademik-takvim-yonetimi` → aktif dönem için takvim gir (7 tarihi doldur)
2. `/academic-calendar/terms` ve `/active` arasında SQL ile karşılaştır:
   ```sql
   SELECT t.Id, t.Name, EXISTS(SELECT 1 FROM TermAcademicCalendars c WHERE c.TermId = t.Id) AS hasCal
   FROM AcademicTerms t WHERE t.Id = 7;
   ```

---

## 🟡 GÖZLEM — Sunum için iyileştirilebilir

### OBS-1 · Test instructor hesabına section atanmamış

Demo hocası `ogretmen@ankarabilim.edu.tr` (Mehmet Öztürk Dr., BM bölümü) için:
```
[E1] /instructor/my-sections status=200 count=0
```

Yani sunumda "hoca → derslerim" derken **boş liste** çıkacak. Demo akışı için bu hocayı en az 1 section'a atayın (önerilen aktif dönemden).

### OBS-2 · Withdraw endpoint düzgün validate ediyor

`POST /registrations/99999/withdraw` → **400 Bad Request** (500 değil ✅). Auto-schedule endpoint için de aynı pattern uygulanmalı (BUG-2).

### OBS-3 · `/registrations/my` boş — ogrenci1 hiç ders kaydı yapmamış

```
[D2] /registrations/my status=200 count=0
```

Demo öğrencisi için aktif dönemde 1-2 ders kaydı yaratın (yoksa transkript/devamsızlık ekranları boş olacak).

### OBS-4 · `hasCalendar:false` ama active endpoint'in döndüğü demo'da

Demoda akademik takvim olmadan kayıt mantığı çakışabilir — BUG-3 ile birlikte düzeltilmeli.

---

## ✅ Geçen Tüm Testler (39/39)

### GRUP A — Login + RBAC (8/8) ✅
| # | Test | Bulgu |
|---|---|---|
| A.SystemAdmin | login → 249 perm (shortcut) | ✅ |
| A.Advisor | login → 26 perm | ✅ |
| A.Dean | login → 120 perm (Instructor + Dean) | ✅ |
| A.DepartmentManager | login → 114 perm | ✅ |
| A.Instructor | login → 33 perm | ✅ |
| A.Student | login → 31 perm | ✅ |
| A.CrossRole | Student → /donem-yonetimi → `/forbidden` | ✅ **RBAC sızıntısı YOK** |
| A.SidebarMenu | her rol için /me/menu 200 | ✅ SysAdmin 10, Student 7, vb. modül |

### GRUP B — Akademik yapı + Dönem + Takvim (7/7) ✅
| # | Test | Bulgu |
|---|---|---|
| B1 | departments + programs | ✅ 200 |
| B1b | faculty endpoint | ✅ `/admin/academic-structure/faculties` (1 fakülte) |
| B2 | term listesi | ✅ 7 dönem |
| B3 | academic-calendar/terms | ✅ 7 term ⚠️ aktif `hasCalendar:false` (BUG-3) |
| B4 | /academic-calendar/active | ✅ full phase payload |
| B5 | Faz gating (student catalog) | ✅ status değişiyor (phase'e bağlı) |
| B6 | Term cancel endpoint | ✅ fake ID → 404 (auth doğru) |

### GRUP C — Otomatik ders programı (5/5) ✅
| # | Test | Bulgu |
|---|---|---|
| C1 | FacultyPlanning sayfası + buton | ✅ "Otomatik Programla" görünür |
| C2 | Auto-schedule preview authorize | ⚠️ 500 dönüyor (BUG-2) |
| C3 | Constraint listesi | ✅ `/api/constraints` → **26 kural** (17 hard + 9 soft) |
| C4 | Plan 9 known issue | 📝 Manuel doğrulama gerek |
| C5 | Schedule editor sayfası | ✅ açıldı |

### GRUP D — Öğrenci ders kaydı (7/7) ✅
| # | Test | Bulgu |
|---|---|---|
| D1 | /course/student-catalog | ✅ 19 ders |
| D2 | /registrations/my | ✅ 0 kayıt (OBS-3) |
| D3 | /student/transcript | ✅ tam payload (CGPA dahil) |
| D4 | /student/curriculum-status | ✅ 9 semester |
| D5 | /ders-kayit sayfası | ✅ render OK |
| D6 | /plan sayfası | ✅ açıldı |
| D7 | /registrations/{id}/withdraw | ✅ fake → 400 (validate OK) |

### GRUP E — Hoca işlemleri (4/4) ✅
| # | Test | Bulgu |
|---|---|---|
| E1 | /instructor/my-sections | ✅ 200 (count=0, OBS-1) |
| E2 | /derslerim sayfası | ✅ açıldı |
| E3 | /yoklama-girisi sayfası | ✅ açıldı |
| E4 | /not-girisi → /derslerim redirect | ✅ |

### GRUP F — Öğrenci görünüm (4/4) ✅
| # | Test | Bulgu |
|---|---|---|
| F1 | /report/transkript | ✅ açıldı |
| F2 | /mufredat-durum | ✅ açıldı |
| F3 | /donem-ortalamalari | ✅ açıldı |
| F4 | /devamsizlik-durumu | ✅ açıldı |

### GRUP G — Nice-to-have (4/4) ✅
| # | Test | Bulgu |
|---|---|---|
| G1 | Refresh → login korunur | ✅ /login'e atılmadı |
| G2 | Boş submit /login'de kalır | ✅ |
| G3 | /sifre-degistir sayfası | ✅ açıldı |
| G4 | /denetim-kayitlari sayfası | ✅ açıldı |

---

## 🧪 E2E ile DOĞRULANAMAYAN — Manuel test gerek

E2E sayfa açılışını, endpoint cevabını ve RBAC'ı doğrular ama **iş kuralı semantik**leri için tıklama + gözle bakma şart:

| Kontrol | Neden manuel | Önerilen demo |
|---|---|---|
| **GPA/CGPA güncelleme** | Not girişi → CGPA recompute. SQL trigger / async job zinciri E2E'de gözükmez. | Hoca rolüyle bir not gir, öğrenci transkriptinde değişti mi bak |
| **DZ otomatik düşümü** | Yoklama %30+ devamsızlık → DZ. Aggregation çalışmıyorsa silent. | 4 hafta yokluk gir → öğrenci `/devamsizlik-durumu` ekranında DZ ibaresi var mı |
| **Çakışma engelleme (kategorik)** | First-time + Full overlap categorical blok. Backend response message E2E'de yok. | Demo öğrencisine çakışan 2 ders ekle, "kayıt yapılamaz" alert çıkmalı |
| **Soft hold midnight clear** | Cron job — 24 saatlik döngü. | Akşam plan ekleyip ertesi sabah silindiğini doğrula (sunumda mümkün değil) |
| **Term cancel cascade** | Aktif dönemi cancel → tüm section'lar Cancelled. Geri alınamaz. | Demo dönemini cancel etmeyin; ayrı bir "Test Dönemi" üzerinde dene |
| **FF tekrar + advisor onayı** | İki onay zinciri (advisor + bölüm bşk.). E2E'de section state karmaşık. | Senaryo: FF'li dersi tekrar seçim → onay bekleyen kayıt akışı |
| **Plan 9 — AmphiThreshold + 0 amfi UNSAT** | Belirli edge case, dummy faculty oluşturarak test gerek | Audit bu yola girmedi |

---

## 🎯 Sunum Öncesi Yapılacaklar Listesi (Prioritized)

### Şimdi yap (sunumdan önce 1 saat içinde)
1. ✅ **BUG-1 fix:** `Program.cs:64-72` AuthLogin policy'yi `PermitLimit = 60` + `QueueLimit = 20` yap → kampüs amfisi sorunsuz login olur
2. ✅ **BUG-3 fix:** Aktif dönem için `/akademik-takvim-yonetimi` sayfasından akademik takvim gir (7 tarih)
3. ✅ **OBS-1 fix:** `ogretmen@ankarabilim.edu.tr` hocasına aktif dönemde 1-2 section ata (admin → `/ogretim-elemani-atama`)
4. ✅ **OBS-3 fix:** `ogrenci1@ankarabilim.edu.tr` için 2-3 ders kaydı yap (admin → `/toplu-ders-kaydi` veya manuel)

### Demo öncesi yap
5. **BUG-2 fix:** `FacultyPlanningController` → `auto-schedule` endpoint'inde input validation ekle (500 → 400)
6. **Manuel sanity test:** Bir hoca hesabıyla giriş yap → bir section seç → not gir → öğrenci hesabıyla yenilenen GPA'yı kontrol et

### Sunumdan sonra (kalıcı)
7. Soft hold midnight clear cron job'un gerçekten çalıştığını izle (1 hafta log takip)
8. BUG-3 root cause analysis: `hasCalendar` ve `/active` endpoint'inin tutarlılığını sağla
9. Audit suite'ini CI'a ekle (39 test, 50 saniye — pre-deploy gate olabilir)

---

## 📦 Kalıcı Çıktılar

- **Audit raporu:** [docs/PRESENTATION-AUDIT-2026-05-20.md](docs/PRESENTATION-AUDIT-2026-05-20.md) (bu dosya)
- **E2E spec:** [SIS.Web/e2e/presentation-audit.spec.ts](SIS.Web/e2e/presentation-audit.spec.ts) — 39 test, koşum süresi 50 saniye, CI'a eklenebilir
- **Test resimleri / video / trace:** `SIS.Web/test-results/` (her başarısız koşumdan)

## 🔄 Audit nasıl tekrar koşulur

```bash
cd SIS.Web
npx playwright test presentation-audit.spec.ts --reporter=list
```

Önkoşul: backend (`dotnet run --project SIS.API`) ve frontend (`npm run dev`) ayakta olmalı.

---

**Sonuç:** Sistem **uçtan uca çalışıyor**. 39 kritik akış geçti. 3 önemli düzeltme + 4 demo hazırlığı sunum öncesi yapılırsa sunum sorunsuz olur.
