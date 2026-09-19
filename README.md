# 🚀 PC Launcher & Gaming Button Box Server

**PC Launcher & Button Box**, Windows bilgisayarınızda çalışan modern bir masaüstü uygulaması (.NET 8 WPF) ve aynı Wi-Fi/LAN ağındaki telefon tarayıcınızdan bilgisayarınızı yönetmenizi sağlayan tam teşekküllü bir uzaktan kontrol sistemidir.

İki ana kullanım senaryosunu tek bir merkezde birleştirir:
1. **Program Başlatıcı (App Launcher):** Bilgisayardaki oyunları veya uygulamaları (Discord, Steam, Spotify, VS Code vb.) uzaktan tek tıkla açma.
2. **Oyun Tuş Takımı / Button Box (Macro Deck):** Euro Truck Simulator 2, Microsoft Flight Simulator, Assetto Corsa veya MMO/FPS oyunlarında kullanabileceğiniz, özel ikonlu ve renkli tuş/kısayol atamaları.

---

## 📱 Mobil Arayüz (Native Mobil Uygulama Deneyimi)

- **Header ve Footer İçermez:** Standart web sitesi kalıplarından arındırılmış, tam ekran, mobil uygulama hissi veren modern arayüz.
- **Modlar Arası Hızlı Geçiş:** Üst kısımdaki minimalist anahtar ile **🎮 Tuş Takımı** ve **🚀 Programlar** arasında anında geçiş.
- **Dokunsal / Haptik Geri Bildirim:** Tuşlara basıldığında mekanik basma animasyonu, anlık tepki ve titreşim (haptics).
- **Özel İkonlar & Renkler:** Her oyun tuşu için emojiler (💡, ⚡, 🛑, 📻, 🗺️, 📸, vb.), yüklenen özel resimler ve neon renk temaları.
- **Güvenlik:** İsteğe bağlı PIN koruması.

---

## 🕹️ Windows Oyun Tuş Simülasyonu (Win32 SendInput)

Oyunların (DirectX, Vulkan, Unreal Engine, Unity) tuş vuruşlarını arka planda ve oyun içi pencerede sorunsuz algılaması için Windows `SendInput` ve donanım `ScanCode` eşlemesi kullanılır.

- **Tekli Tuşlar:** A-Z, 0-9, F1-F12, Space, Enter, Tab, Esc, Yön Tuşları, NumPad vb.
- **Kombinasyonlar:** `Ctrl+Shift+S`, `Alt+F4`, `Ctrl+C` vb.
- **Medya Tuşları:** `Mute`, `PlayPause`, `VolumeUp`, `VolumeDown` vb.

---

## 📂 Proje Mimarisi

```
Button box/
├── PCLauncher.sln               # Visual Studio Solution
├── PCLauncher.slnx              # Modern .NET Solution
├── publish.bat                  # Tek tıkla Single-File .exe derleyici
│
├── PCLauncher.Core/             # Çekirdek Kütüphane
│   ├── Models/
│   │   ├── AppItem.cs           # Başlatılacak program modeli
│   │   ├── KeyActionItem.cs     # Button Box tuş/kısayol modeli
│   │   ├── AppSettings.cs       # Port, PIN, Başlangıç ayarları
│   │   └── ServerStatus.cs      # Canlı sunucu durumu
│   └── Services/
│       ├── KeySimulatorService.cs # Win32 SendInput tuş simülatörü
│       ├── AppLauncherService.cs  # Güvenli Windows süreç başlatıcı
│       ├── JsonStorageService.cs  # JSON veri tabanı
│       ├── NetworkService.cs      # Otomatik LAN IPv4 tespiti
│       ├── QrCodeService.cs       # Dinamik QR kod üretimi
│       └── FirewallService.cs     # Windows Güvenlik Duvarı kuralı
│
├── PCLauncher.Server/           # Kestrel HTTP Sunucusu & REST API
│   ├── LauncherServer.cs        # Gömülü sunucu ve endpointler
│   └── wwwroot/                 # Mobil Arayüz (Header/Footer'sız, Fullscreen)
│       ├── index.html
│       ├── css/style.css
│       └── js/app.js
│
├── PCLauncher.App/              # Modern Masaüstü Arayüzü (WPF)
│   ├── Views/
│   │   ├── DashboardView.xaml   # Sunucu, QR Kod, IP, Loglar
│   │   ├── AppsView.xaml        # Program ekleme/düzenleme/silme
│   │   ├── KeyActionsView.xaml  # Tuş takımı buton yönetimi
│   │   ├── AddEditKeyDialog.xaml# Tuş ve ikon atama penceresi
│   │   ├── AddEditAppDialog.xaml# Program tanımlama penceresi
│   │   └── SettingsView.xaml    # Port, PIN, Windows Başlangıç
│   └── Services/
│       └── TrayIconService.cs   # Windows Sistem Tepsisi (Tray)
│
└── PCLauncher.Tests/            # xUnit Test Paketi
    ├── CoreServicesTests.cs
    └── ServerIntegrationTests.cs
```

---

## ⚡ Hızlı Başlangıç

### 1. Masaüstü Uygulamasını Çalıştırma
```powershell
dotnet run --project PCLauncher.App\PCLauncher.App.csproj
```

### 2. Tek Dosyalık (.exe) Çıktı Alma
`publish.bat` dosyasını çalıştırın veya:
```powershell
dotnet publish PCLauncher.App\PCLauncher.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o bin\publish-singlefile
```
`bin\publish-singlefile\PCLauncher.App.exe` olarak tüm bağımlılıkları içeren tek bir dosya oluşur.

### 3. Telefonla Bağlanma
1. PC ve telefonunuzun aynı Wi-Fi / ağa bağlı olduğundan emin olun.
2. PC uygulamasının Dashboard ekranında görünen **QR kodu telefonunuzun kamerasıyla okutun**.
3. Açılan web uygulamasında üstteki butonlarla ister **Tuş Takımı** ile oyun kontrollerini yönetin, ister **Programlar** sekmesinden PC'nizdeki uygulamaları başlatın!
