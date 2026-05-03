# KSafety

**KSafety**, Windows işletim sistemi üzerinde belirlediğiniz uygulamaları PIN koruması ve zaman kuralları ile denetlemenizi sağlayan profesyonel bir güvenlik çözümüdür. **Kanser INC.** tarafından geliştirilen bu uygulama, gizliliğinizi ve çalışma disiplininizi en üst seviyeye çıkarmayı hedefler.

---

## Öne Çıkan Özellikler

*   **Modern Arayüz:** Daha dengeli, göz yormayan kurumsal **North-Dark** arayüz tasarımı.
*   **Gelişmiş Çizim Teknolojisi:** Stabil custom buton ve panel çizimleri ile minimum flicker (titreme) ve maksimum görsel kararlılık.
*   **Sıkı Güvenlik:** 
    *   Koruma kapatma, uygulama çıkışı ve tray menü işlemleri için **PIN doğrulaması**.
    *   15 dakikalık geçici duraklatma modu (PIN korumalı).
    *   SHA256 tabanlı güvenli PIN saklama altyapısı.
*   **Akıllı Denetim:**
    *   Uygulamaya her odaklanıldığında (Focus) PIN sorma seçeneği.
    *   Bilgisayar başından uzaklaşıldığında (Idle) uygulamaları kapatmadan otomatik kilitleme.
    *   Windows ile otomatik başlatma desteği.
*   **Esnek Zaman Yönetimi:** Dakika bazlı özelleştirilebilir kilit saatleri ve geceyi aşan zaman aralıkları desteği.

---

## Başlangıç

Uygulamayı kullanmaya başlamak için şu adımları izleyin:

1.  **Kurulum:** `Release` klasörü içerisindeki `KSafety.exe` dosyasını çalıştırın.
2.  **İlk Yapılandırma:** Uygulama ilk açıldığında sizden bir güvenlik PIN'i belirlemenizi isteyecektir.
3.  **Uygulama Ekleme:** "Korunan Uygulamalar" bölümünden kısıtlamak istediğiniz `.exe` dosyalarını seçin.
4.  **Zaman Kuralları:** Her uygulama için hangi saat aralıklarında kilitli kalacağını "Zaman Kuralları" bölümünden tanımlayın.
5.  **Hızlı Ayarlar:** Davranışları (Otomatik başlatma, uzak kalınca kilitleme vb.) panel üzerinden anlık olarak yönetin.

---

## Teknik Detaylar

KSafety, performans ve düşük kaynak tüketimi için **C# .NET** ve **Win32 API** entegrasyonu ile geliştirilmiştir.

*   **Native Entegrasyon:** `user32.dll` üzerinden aktif pencere takibi.
*   **Veri Yönetimi:** XML tabanlı konfigürasyon yapısı.
*   **Güvenlik:** `RNGCryptoServiceProvider` ile salt ve `SHA256` ile şifreleme.
*   **Dosya Yolu:** Ayarlarınız `%AppData%\KSafety` klasöründe güvenle saklanır.

---

## Dağıtım

Son kullanıcılar için sadece `Release` klasörü altındaki dosyaların verilmesi yeterlidir. Ek bir kurulum gerektirmez (Portable çalışabilir).

---

## ⚖️ Lisans ve Yapımcı

Bu proje **Kanser INC.** tarafından geliştirilmiştir. Tüm hakları saklıdır.

> **Not:** Gizliliğiniz her şeyden önce gelir. KSafety, verilerinizi yerel cihazınızda tutar ve dışarıya aktarmaz.
