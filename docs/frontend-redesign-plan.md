# Frontend Yeniden Tasarım Planı

Referans: Ankara Bilim OBS (Proliz) + Hamiyet Ayaz transkripti + Cansu Yörük sidebar

## Tasarım Prensibi
- Rol bazlı sidebar: kullanıcının TÜM rollerine göre menü birleşir
- Çalışan sayfalar normal, backend'i olmayan sayfalar disabled/kırmızı hover (IMPLEMENTED_ROUTES)
- Mevcut OBS'nin yapısı referans ama birebir kopya değil — bizim backend'e uygun modern UI
- Türkçe labels, profesyonel OBS akışı

---

## SystemAdmin Paneli

```
⭐ Sık Kullanılanlar
  ├── Ana Sayfa (dashboard — dönem durumu, istatistikler)
  ├── Akademik Takvim

⚙️ Dönem Yönetimi
  ├── Dönem İşlemleri (oluştur/düzenle/transition — TermStatus badge + butonlar)
  ├── Akademik Takvim Etkinlikleri (takvim CRUD)

🏫 Kaynak Yönetimi
  ├── Derslik Yönetimi (CRUD — bina, kat, kapasite, tip)
  ├── Derslik-Fakülte Ataması (dönemlik atama + akıllı öneri)

👥 Kullanıcı Yönetimi
  ├── Kullanıcı Listesi (rol atama, şifre sıfırlama, durum)
  ├── Öğrenci Rehberi (arama, filtreleme, detay kartı)
  ├── Personel Listesi (hoca ekleme, unvan, bölüm)

🏛️ Akademik Yapı
  ├── Fakülte / Bölüm / Program (hiyerarşik CRUD)
  ├── Ders Kataloğu (ders CRUD, ön koşul, eşdeğerlik)
  ├── Sınıf Atlatma Kuralları (PromotionRule — program bazlı)

📊 Ders İşlemleri
  ├── Ders Açma İşlemleri (forecast + cross-dept + section)
  ├── Planlama Merkezi (onay akışı)
  ├── Fakülte Planlama (scheduling overview)
  ├── Sınıf Listeleri
  ├── Dönem Kapatma İşlemleri

🔧 Sistem
  ├── Sistem Ayarları (SystemConfig — kategorili)
  ├── Denetim Kayıtları (AuditLog — filtreli)
```

Backend Durumu:
- Dönem İşlemleri: ✅ endpoint var, ✅ sayfa var (TermStatus + transition güncel)
- Akademik Takvim: ✅ endpoint var, ✅ sayfa var
- Derslik Yönetimi: ✅ endpoint var, ✅ sayfa var (yeni eklendi)
- Derslik-Fakülte Ataması: ✅ endpoint var, ✅ sayfa var (öneri endpoint dahil)
- Kullanıcı Listesi: ✅ endpoint var, ✅ sayfa var
- Öğrenci Rehberi: ✅ endpoint var, ✅ sayfa var
- Personel Listesi: ✅ endpoint var, ✅ sayfa var
- Fakülte/Bölüm/Program: ✅ endpoint var, ✅ sayfa var
- Ders Kataloğu: ✅ endpoint var, ✅ sayfa var
- Sınıf Atlatma Kuralları: ✅ endpoint var, ✅ sayfa var
- Ders Açma: ✅ endpoint var, ✅ sayfa var (forecast zenginleştirildi)
- Planlama Merkezi: ✅ endpoint var, ✅ sayfa var
- Fakülte Planlama: ✅ endpoint var, ✅ sayfa var (dinamik fakülte seçici)
- Sınıf Listeleri: ✅ endpoint var, ✅ sayfa var
- Dönem Kapatma: ✅ endpoint var, ✅ sayfa var
- Sistem Ayarları: ✅ endpoint var, ✅ sayfa var
- Denetim Kayıtları: ✅ endpoint var, ✅ sayfa var

---

## Dean Paneli

```
⭐ Sık Kullanılanlar
  ├── Ana Sayfa
  ├── Fakülte Planlama

🏛️ Fakülte Yönetimi
  ├── Ders Açma İşlemleri
  ├── Program Onayı (ders programı dekan onay/red)
  ├── Sınıf Listeleri
  ├── Dönem Kapatma İşlemleri

👥 Öğrenci İşlemleri
  ├── Öğrenci Rehberi
  ├── Sınıf Atlatma İşlemleri
  ├── Öğrenci Başarı Hesaplamaları

📚 Derslerim (Instructor rolü varsa)
  ├── Derslerim
  ├── Ders Programım
  ├── Not Girişi

👨‍🏫 Danışmanlık (AcademicAdvisor rolü varsa)
  ├── Onay Bekleyenler
  ├── Danışman Öğrencilerim
```

Backend Durumu:
- Program Onayı: ✅ endpoint var (ScheduleApprovalController), ✅ sayfa var
- Diğerleri: ✅ tümü mevcut

---

## DepartmentManager Paneli

```
⭐ Sık Kullanılanlar
  ├── Ana Sayfa
  ├── Planlama Merkezi

🎓 Bölüm Yönetimi
  ├── Ders Açma İşlemleri
  ├── Sınıf Listeleri
  ├── Dönem Kapatma İşlemleri

👥 Öğrenci İşlemleri
  ├── Öğrenci Rehberi
  ├── Toplu Danışman Atama
  ├── Sınıf Atlatma İşlemleri

📚 Derslerim (Instructor rolü varsa)
  ├── Derslerim
  ├── Ders Programım
  ├── Not Girişi

👨‍🏫 Danışmanlık (AcademicAdvisor rolü varsa)
  ├── Onay Bekleyenler
  ├── Danışman Öğrencilerim
```

---

## Instructor Paneli

```
⭐ Sık Kullanılanlar
  ├── Ana Sayfa
  ├── Derslerim
  ├── Ders Programım

📚 Ders İşlemleri
  ├── Not Girişi

👨‍🏫 Danışmanlık (AcademicAdvisor rolü varsa)
  ├── Onay Bekleyenler
  ├── Danışman Öğrencilerim
```

---

## Student Paneli

```
📋 Genel İşlemler
  ├── Ana Sayfa
  ├── Akademik Takvim
  ├── Alınan Dersler

📚 Ders ve Dönem İşlemleri
  ├── Ders Kayıt
  ├── Dönem Ortalamaları
  ├── Müfredat Durum

👤 Kullanıcı İşlemleri
  ├── Şifre Değiştir
```

---

## Uygulama Sırası

1. Sidebar menü gruplarını güncelle (SidebarMenuGroups.ts)
2. Eksik route'ları App.tsx'e ekle
3. Eksik sayfalar için placeholder oluştur (backend yoksa disabled)
4. Mevcut sayfaları OBS referansına göre iyileştir
5. Dashboard'ları role göre özelleştir
