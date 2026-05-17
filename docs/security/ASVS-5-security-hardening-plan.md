# OWASP ASVS 5.0 Güvenlik Sertleştirme Planı

Kaynak standart: [`OWASP_ASVS_5.0.csv`](OWASP_ASVS_5.0.csv).

## Hedef

Bu sistem için güvenlik hedefi iki aşamalıdır:

1. **ASVS L1 tabanı:** Web/API uygulamaları için temel üretim güvenliği. Bu çalışma ilk L1 sertleştirmelerini uygular.
2. **ASVS L2 hedefi:** Öğrenci, not, yoklama, danışmanlık ve idari veriler kişisel/veri hassasiyeti taşıdığı için üretim yayını öncesinde L2 kontrolleri kapatılmalıdır.

## Bu Turda Uygulanan Sertleştirmeler

| Alan | ASVS bağı | Güncelleme |
| --- | --- | --- |
| Yapılandırma ve gizli bilgiler | V13, V11 | JWT anahtarı, CORS origin, bağlantı dizesi ve `AllowedHosts` başlangıçta doğrulanır; varsayılan JWT anahtarı ve üretimde varsayılan DB parolası engellenir. |
| Kimlik doğrulama | V6, V9 | JWT doğrulamasında imza, expiration ve düşük clock skew zorunlu hale getirildi; login endpoint'i rate limit altına alındı. |
| Yetkilendirme | V8 | Global authorization fallback eklendi; açıkça `[AllowAnonymous]` olmayan endpoint'ler kimlik doğrulama ister. |
| API kötüye kullanım koruması | V4, V6 | Login, anonim duyuru ve anonim QR yoklama endpoint'leri IP bazlı rate limit ile sınırlandı. |
| Güvenli iletişim ve tarayıcı başlıkları | V3, V12, V13 | `nosniff`, `DENY frame`, `Referrer-Policy`, `Permissions-Policy`, üretimde CSP/no-store ve HSTS eklendi. |
| Hata yönetimi | V16 | Üretimde genel problem response döndürülür; audit kaydında stack trace saklama kaldırıldı, trace id ile korelasyon bırakıldı. |
| Dosya yükleme | V5 | Excel import için boyut sınırı, `.xlsx` uzantısı ve ZIP tabanlı imza kontrolü merkezi hale getirildi. |
| URL güvenliği | V1 | Dış duyuru scraping çıktısındaki URL'ler sadece beklenen HTTPS host'una normalize edilir. |
| Kod yazma guardrail'leri | V2, V3, V4, V5, V6, V8 | Yeni endpoint permission, public endpoint rate limit/gerekçe, dosya yükleme validasyonu, frontend XSS/token sink kontrolleri ve input validation baseline testleri eklendi. |
| CI güvenlik kapıları | V13, V15 | ASVS guardrail testleri, dependency audit, Gitleaks secret scan ve PR checklist zorunlu hale getirildi. |

## Kod Yazarken Zorunlu Güvenlik Kapıları

- Yeni API endpoint: `RequirePermission` veya `RequireAnyPermission` olmadan merge edilmemeli.
- Public endpoint: yalnızca gerçek gerekçe varsa `AllowAnonymous`; yanında `EnableRateLimiting` ve `PublicEndpointJustification` zorunlu.
- Yeni input/DTO: `AbstractValidator<T>` veya eşdeğer validasyon eklenmeli; baseline dosyası yeni borç için kullanılmamalı.
- File upload: `FileUploadValidation` ve açık `RequestSizeLimit` kullanılmalı.
- Frontend: raw HTML sink (`dangerouslySetInnerHTML`, `innerHTML`) ve dağınık access-token storage yasak.
- CI: `.github/workflows/security-checks.yml` ASVS guardrail testlerini, dependency audit'i ve PR checklist kapısını çalıştırır; `.github/workflows/secret-scan.yml` Gitleaks taraması yapar.

## Uçtan Uca Yol Haritası

### Faz 0 - Kapsam ve Envanter

- Veri sınıflandırması: öğrenci PII, not bilgisi, yoklama, audit log, sistem konfigürasyonu.
- Endpoint envanteri: her controller/action için auth, permission, rate limit ve veri hassasiyet etiketi.
- ASVS CSV izlenebilirliği: her `req_id` için `Uygun`, `Kısmi`, `Uygun değil`, `Uygulanamaz` durumu.

### Faz 1 - Yapılandırma ve Dağıtım Güvenliği

- Üretimde tüm sırları ortam değişkeni, secret manager veya CI/CD secret store üzerinden ver.
- `appsettings.json` içinde gerçek parola/anahtar tutma; `appsettings.Development.json` lokal ve git dışında kalmalı.
- `AllowedHosts`, `Cors:AllowedOrigins`, HTTPS sertifikaları ve reverse proxy header ayarlarını ortam bazlı doğrula.
- CI'da secret scan ve dependency audit zorunlu hale getir.

### Faz 2 - Kimlik Doğrulama ve Oturum

- Parola politikası ve seed kullanıcı parolalarını L2 seviyesine taşı: uzunluk, karmaşıklık, tekrar kullanım ve rotasyon.
- Refresh token tasarımını netleştir: ya kaldır ya da sunucu tarafı rotasyon, iptal ve cihaz takibiyle uygula.
- Frontend token saklama modelini değerlendir: localStorage XSS etkisini azaltmak için HttpOnly/SameSite cookie alternatifi.
- Hesap kilitleme, login anomali alarmı ve kullanıcıya güvenli hata mesajı standardını tamamla.

### Faz 3 - Yetkilendirme ve İş Kuralı Güvenliği

- RBAC permission matrisini endpoint envanteriyle otomatik karşılaştır.
- Öğrenci, danışman, öğretim elemanı ve admin veri kapsamı kontrollerini testlerle sabitle.
- `SystemAdministrator` kısa yolunun audit izini ve kullanım gerekçesini belgeye bağla.
- Toplu işlemler için çift onay, kapsam önizleme ve geri alma kontrolleri ekle.

### Faz 4 - Girdi, Dosya ve Enjeksiyon Kontrolleri

- DTO validasyonlarını merkezi hale getir; FluentValidation veya ASP.NET model validation ile tüm write endpoint'lerini kapsa.
- Import dosyalarında satır sayısı, sheet adı, beklenen kolonlar ve hücre uzunluk sınırlarını uygulama seviyesinde doğrula.
- Dinamik sorgu/sıralama/arama alanlarında allowlist standardını genişlet.
- HTML scraping ve dış kaynak verilerinde protokol/host allowlist, timeout ve maksimum cevap boyutu uygula.

### Faz 5 - Frontend ve Tarayıcı Güvenliği

- React tarafında dinamik `href`, `mailto`, `tel`, dosya indirme ve print window akışlarını URL encoding/allowlist ile gözden geçir.
- CSP'yi üretim frontend sunucusunda gerçek script/style kaynaklarına göre sıkılaştır.
- XSS etkisini azaltmak için token saklama, role cache ve localStorage kullanımını veri hassasiyetine göre ayır.

### Faz 6 - Veri Koruma ve Kriptografi

- Kişisel veri saklama/maskeleme standardı belirle: audit export, öğrenci listeleri, not ve yoklama ekranları.
- Veritabanı yedekleri ve export dosyaları için şifreleme ve saklama süresi politikası uygula.
- JWT anahtar rotasyonu ve eski anahtar geçiş planını ekle.

### Faz 7 - Loglama, İzleme ve Olay Müdahalesi

- Audit log aksiyon sözlüğünü standartlaştır: login, yetki reddi, kritik veri okuma, import/export, toplu değişiklik.
- Alarm eşikleri: başarısız login, 401/403 artışı, export hacmi, toplu silme, sistem hata oranı.
- Incident playbook: kim bilgilendirilecek, token/secrets nasıl döndürülecek, audit nasıl korunacak.

### Faz 8 - Test ve Sürekli Güvence

- Security unit/integration testleri: fallback auth, permission attribute, rate limit, file validation, unsafe URL reddi.
- CI kapıları: `dotnet test`, `dotnet build`, frontend build/lint, dependency audit, secret scan.
- ASVS izlenebilirlik raporunu her release öncesi güncelle.

## Önceliklendirilmiş Kalan İşler

1. Endpoint-permission envanterini otomatik rapora dönüştür.
2. DTO validasyon kapsamını write endpoint'lerinin tamamına yay.
3. Refresh token kullanım kararını ver ve localStorage token riskini azalt.
4. Üretim frontend CSP/header ayarlarını dağıtım katmanına ekle.
5. ASVS L2 izlenebilirlik matrisini CSV'deki tüm maddeler için tamamla.
