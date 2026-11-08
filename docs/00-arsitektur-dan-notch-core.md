# 🧩 Modul 00: Arsitektur & Notch Core

> Indeks seluruh dokumentasi: [README.md](README.md)

Dokumen ini membedah pondasi dasar window, sistem animasi geometri notch, standar tipografi HD ClearType, arsitektur anti-mojibake, dan sistem navigasi slide navbar bawah.

---

## 📂 File Terkait
- **View XAML**: [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml)
- **View Code-Behind**:
  - [NotchWindow.xaml.cs](../NotchWin/Views/NotchWindow.xaml.cs)
  - [NotchWindow.OutlineFx.cs](../NotchWin/Views/NotchWindow.OutlineFx.cs) (parsial: garis cahaya outline timer/alert — Keputusan 78, detail di [08-timer-dan-stopwatch.md](08-timer-dan-stopwatch.md))
- **Core ViewModel**: [NotchViewModel.cs](../NotchWin/ViewModels/NotchViewModel.cs)
- **ViewModel Home**: [HomeCalendarViewModel.cs](../NotchWin/ViewModels/HomeCalendarViewModel.cs) (strip kalender mingguan `SlideHomeCard`, minggu mulai Minggu, huruf hari & bulan mengikuti bahasa, cek pergantian tanggal tiap menit)
- **Win32 & Interop Helper**:
  - [NativeMethods.cs](../NotchWin/Interop/NativeMethods.cs)
  - [WindowStyleHelper.cs](../NotchWin/Interop/WindowStyleHelper.cs)
- **App Lifecycle & Global Resources**:
  - [App.xaml](../NotchWin/App.xaml) (ClearType global styles & bitmap scaling)
  - [App.xaml.cs](../NotchWin/App.xaml.cs) (Single instance mutex & silent wake event)
- **Typography & Scale**:
  - [FontScaleService.cs](../NotchWin/Services/FontScaleService.cs)

---

## ⚡ Siklus Hidup Aplikasi & Single-Instance Lifecycle

NotchWin adalah aplikasi utilitas latar belakang (background overlay) yang hanya boleh berjalan 1 instans aktif:
1. **Mutex Tunggal (`_singleInstanceMutex`)**:
   - `Local\NotchWin_SingleInstance_Mutex_56ab85f1` memastikan hanya 1 proses utama yang menjalankan overlay notch.
2. **Silent Secondary Launch & Wake Signal (`_wakeEvent`)**:
   - Jika pengguna sengaja meluncurkan NotchWin kedua kalinya (atau jika sistem startup Windows memicu peluncuran ulang/ganda), instans kedua **TIDAK MENAMPILKAN MODAL POPUP** (`MessageBox.Show` dilarang).
   - Instans kedua mengirimkan sinyal ke instans pertama melalui `EventWaitHandle` (`Local\NotchWin_WakeEvent_56ab85f1`), kemudian langsung keluar secara senyap (*silent exit*).
   - Instans pertama mendengarkan sinyal melalui `ThreadPool.RegisterWaitForSingleObject` dan secara otomatis memanggil `notchWindow.Show()`, `notchWindow.Activate()`, dan `vm.Expand()`. Dengan ini, pengguna mendapatkan interaksi responsif tanpa kotak dialog yang memblokir layar desktop.

---

## 🖥️ Arsitektur Tipografi HD & Rendering Subpixel (Keputusan 73 & 74)

Dynamic Notch Windows menerapkan arsitektur rendering teks definisi tinggi (*High-Definition Text Rendering Engine*) untuk mengatasi kelemahan bawaan WPF pada window transparan (`AllowsTransparency="True"`):

1. **Aktivasi Paksa Subpixel ClearType (`RenderOptions.ClearTypeHint="Enabled"`)**:
   - Secara default, WPF menonaktifkan ClearType subpixel rendering pada transparent window dan jatuh ke grayscale antialiasing yang membuat teks pudar dan berbayang buram di latar belakang gelap.
   - dynamic notch memaksa ClearType di level `<Window>`, `PillContainer`, `ContentHost`, `CollapsedContentGrid`, `ExpandedContentGrid`, serta gaya global di `App.xaml` via:
     ```xml
     TextOptions.TextRenderingMode="ClearType"
     TextOptions.TextFormattingMode="Display"
     RenderOptions.ClearTypeHint="Enabled"
     RenderOptions.BitmapScalingMode="HighQuality"
     ```
   - Hasilnya, DirectWrite merender tepian huruf menggunakan subpixel RGB fisik monitor, menghasilkan teks yang tajam, pekat, dan jernih.

2. **Standardisasi Font Stack UI (`Segoe UI Variable Text`)**:
   - Menggantikan seluruh penggunaan `Segoe UI Variable Display` dengan `Segoe UI Variable Text, Segoe UI, sans-serif`.
   - Varian *Display* dirancang untuk judul besar (>36pt) sehingga batang hurufnya terlalu tipis (<0.5 px) pada teks UI normal (9pt–13pt). Varian *Text* menjamin batang huruf minimal 1.0 piksel fisik, celah huruf terbuka (*open counters*), dan kontras tinggi.
   - Pada Windows 10, font stack ini dengan mulus jatuh (*fallback*) ke `Segoe UI` standar yang terbukti tajam.

3. **Penjajaran Grid Piksel Fisik (`SnapsToDevicePixels` & `UseLayoutRounding`)**:
   - Seluruh kontainer dan blok teks mengaktifkan `SnapsToDevicePixels="True"` dan `UseLayoutRounding="True"`.
   - Garis dasar teks (*baseline*) selalu terkunci pada koordinat piksel integer monitor dan tidak terdistribusi di antara dua baris piksel layar.

4. **Penjajaran Half-Point Grid pada `FontScaleService`**:
   - Perhitungan penskalaan font di `FontScaleService.cs` dibulatkan ke kelipatan 0.5pt terdekat:
     ```csharp
     Math.Round(baseSize * scale * 2.0) / 2.0
     ```
   - Mencegah ukuran font pecahan ganjil (misal 9.4pt atau 10.2pt) yang meleset dari tabel *font hinting* DirectWrite.

5. **Larangan Penggunaan `DropShadowEffect` Langsung pada Teks**:
   - Menghapus efek bayangan shader dari elemen teks (misal angka suhu cuaca `25°C`).
   - Di WPF, efek shader memaksa DirectX me-raster teks ke *intermediate bitmap cache* resolusi rendah yang mematikan ClearType dan menghasilkan teks bergerigi (*aliasing*).

---

## ⚙️ Arsitektur State & Geometri Notch

### 1. Status Notch (`NotchState`)
Notch beroperasi dalam dua status utama di [NotchViewModel.cs](../NotchWin/ViewModels/NotchViewModel.cs):
- **`NotchState.Collapsed`**: Mode notch mengecil yang menempel di bagian tengah atas layar monitor utama. Menampilkan widget prioritas (Jam, Media Mini, Baterai, atau Volume).
  - Lebar dasar: `250px` hingga `335px` (menyesuaikan adanya baterai dan status lirik aktif). Saat widget Media menampilkan lirik, lebar diukur dinamis dari baris lirik aktif (`chrome + lebar teks + 2px`) dan boleh tumbuh hingga `620px` (`MaxExpandedWidth`); batas bawah tetap lebar default sehingga notch tidak menyempit.
  - Tinggi: `32px` (`CollapsedHeight`).
  - Radius sudut: `16px` (`CollapsedCornerRadius`).
- **`NotchState.Expanded`**: Mode kartu lebar interaktif ketika notch di-hover atau diklik.
  - Lebar: `620px` (`MaxExpandedWidth`).
  - Tinggi: `264px` (`ExpandedHeight`) hingga `326px` (Catatan/Cuaca memakai `ExpandedWeatherHeight = 326px`), ditambah baris navigasi hover; Todo memakai rumus dinamis hingga `634px`. Saat berada di tab Home dan ada notifikasi aktif (adzan/alarm), tinggi mengembang otomatis menjadi `324px` (`ExpandedHeight + 60px`).
  - Home Card & Dock Clearance: Kartu Miniplayer & Kalender (tinggi 120px) dipadukan dengan App Shortcut Dock (tinggi 44px) dengan jarak 10px (total 174px), memberikan ruang lega vertikal tanpa terpotong (*clipped*) oleh lengkungan bawah notch.
  - Radius sudut: `22px` (`ExpandedCornerRadius`).
  - Header: hari dan tanggal penuh rata kiri (`Clock.FullDate`, mis. "Friday, 30 October 2026"), sedangkan sisi kanan berisi jam, baterai HD vektor, tombol Settings, tombol pin, dan maskot.

### 2. Animasi Geometri (Path & Ear Curve)
Bentuk notch Windows dibuat dari kurva path kustom (`NotchBackgroundPath` dan `NotchStrokePath`) dengan telinga lengkung luar (`EarRadius = 12.0px`) di kiri-kanan atas notch:
- Perhitungan geometri dilakukan di fungsi `UpdateNotchGeometry()` di [NotchWindow.xaml.cs](../NotchWin/Views/NotchWindow.xaml.cs).
- Animasi perpindahan status dikendalikan oleh `AnimateToCurrentState()` menggunakan `DoubleAnimation` dengan `QuarticEase (EaseOut)` berdurasi ~340ms (expand) dan ~270ms (collapse).

### 3. Kalibrasi Skala Fisik Baseline 100% Notch
- Skala fisik notch dikendalikan melalui `ApplyNotchScale(scale, animate)` di [NotchWindow.xaml.cs](../NotchWin/Views/NotchWindow.xaml.cs) yang menggerakkan `PillContainer.LayoutTransform = new ScaleTransform(...)`.
- **Rekalibrasi Baseline 1.2x**:
  - Ukuran fisik notch pada skala 1.2x (120%) kini dijadikan patokan ukuran standar dasar (**100%**) melalui pengali konstanta:
    ```csharp
    const double BaseScaleFactor = 1.2;
    double effectiveScale = scale * BaseScaleFactor;
    ```
  - Nilai preferensi pengguna `NotchScale` di `settings.json` tersimpan sebagai `1.0` (100% pada slider pengaturan), menghasilkan ukuran fisik yang ideal, proporsional, dan tajam secara default pada monitor modern.

---

## 🧠 Arsitektur Manajemen Memori & Optimasi RAM (MemoryManagementService)

Aplikasi mengimplementasikan layanan terpusat `MemoryManagementService.cs` yang memangkas konsumsi RAM dari **~245 MB** ke kisaran **~54 MB – 62 MB** (penurunan hingga 75%):

1. **Penyetelan Runtime GC (.NET 8 di `NotchWin.csproj`)**:
   - `<ServerGarbageCollection>false</ServerGarbageCollection>`: Memastikan Workstation GC dengan satu heap alih-alih heap per-core CPU.
   - `<ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>`: Koleksi memori berjalan secara paralel di latar belakang tanpa membekukan animasi 60fps.
   - `<RetainVMGarbageCollection>false</RetainVMGarbageCollection>`: Memaksa runtime segera memulangkan halaman memori virtual yang bebas ke Windows.
2. **Siklus Pembersihan Otomatis**:
   - **Post-Startup Flush (T+4.5s)**: Setelah `Window_Loaded`, membersihkan lonjakan memori inisialisasi awal WPF (parsing ribuan tag XAML, resource dictionary, dan font glyph cache) dari ~230 MB ke <55 MB.
   - **Idle Collapsed Notch Trim (T+8s)**: Saat notch berada di posisi *collapsed* tanpa hover kursor, mengeksekusi kompresi LOH (`GCLargeObjectHeapCompactionMode.CompactOnce`), GC Gen-2 non-blocking, dan `EmptyWorkingSet`.
   - **Periodic Maintenance**: Setiap 3 menit, jika notch dalam keadaan *collapsed* dan working set melebihi 65 MB, dilakukan pembersihan ringan otomatis.

---

## 🧭 Sistem Navigasi Navbar Bawah (`NavDockHost`, `NavTabsHost` & `NavActivePill`)

Ketika notch berada di mode `Expanded`, bar navigasi menempati baris khusus (`Grid.Row="1"`, tinggi 26 px + margin bawah 8 px) **di bawah header dan tepat di atas konten slide** (urutan `RowDefinitions` = `Auto, Auto, *`). Kapsul baris navigasi kini dibingkai dalam track kontainer yang rapi (`NavDockTrack`), dengan padding simetris dan hirarki visual teks yang jelas antara tab aktif dan inaktif. Navbar **selalu tampil penuh sebagai ikon + label selama notch expanded** — tidak lagi menciut menjadi titik saat idle maupun saat kursor keluar dari area navbar. Tombol Settings berada di header di antara maskot dan tombol pin, bukan di navbar.

- Kontainer baris: `NavDockHost` (Grid tinggi 26) membungkus kapsul track `NavDockTrack` (`CornerRadius="13"`, background transparan halus `#3512121A`, border `#252535`, padding 2 px) yang menyatukan seluruh 10 tombol navigasi menjadi segmented dock yang elegan. Tombol memakai `NavTabUnifiedStyle` dengan padding simetris `Padding="6,0,7,0"`, margin label `4,0,0,0`. Seluruh 10 ikon menggunakan sistem ikon vektor modern terstandarisasi (Apple SF Symbols / Lucide style) dengan bobot visual seragam 11×11 DIPs, stroke 1.2 px, serta round caps/joins yang harmonis.
- Hirarki visual & skala: `ApplyNavTabVisualState` otomatis mewarnai tab aktif dengan teks putih `#FFFFFF` (SemiBold) dan stroke ikon warna aksen (warna fresh water `#38BDF8` untuk tab hidrasi aktif), serta tab inaktif dengan warna lembut `#9CA3AF` (Normal). Integrasi `InvalidateNavFullWidths()` memastikan navbar tetap proporsional saat ukuran font diubah di pengaturan.

### Pemetaan Indeks Slide & Struktur XAML (10 Tab)
| Tab | Fitur | Indeks Jika Sholat Aktif | Indeks Jika Sholat Nonaktif | Lokasi XAML |
|:---:|:---|:---:|:---:|:---|
| `NavTab0` | **Home** (Miniplayer, Kalender Mingguan, App Dock, Notifikasi) | 0 | 0 | [SlideHomeCard](../NotchWin/Views/NotchWindow.xaml) |
| `NavTab1` | Sedang Memutar (Media Player & Lirik Karaoke) | 1 | 1 | [SlideMediaCard](../NotchWin/Views/NotchWindow.xaml) |
| `NavTab2` | Pusat Kontrol (Control Center) | 2 | 2 | [SlideControlCenterCard](../NotchWin/Views/NotchWindow.xaml) |
| `NavTab3` | Cuaca (Weather) | 3 | 3 | [SlideWeatherCard](../NotchWin/Views/NotchWindow.xaml) |
| `NavTab4` | Jadwal Sholat (Prayer) | 4 | *(Hidden)* | [SlidePrayerCard](../NotchWin/Views/NotchWindow.xaml) |
| `NavTab5` | Pengingat Minum (Hydration) | 5 | 4 | [SlideWaterCard](../NotchWin/Views/NotchWindow.xaml) |
| `NavTab6` | Catatan & Alarm (Notes) | 6 | 5 | [SlideNoteCard](../NotchWin/Views/NotchWindow.xaml) |
| `NavTab7` | Riwayat Papan Klip (Clipboard) | 7 | 6 | [SlideClipboardCard](../NotchWin/Views/NotchWindow.xaml) |
| `NavTab8` | Timer & Stopwatch | 8 | 7 | [SlideTimerCard](../NotchWin/Views/NotchWindow.xaml) |
| `NavTab9` | Shelf File & Phone Share | 9 | 8 | [SlideShelfCard](../NotchWin/Views/NotchWindow.xaml) |

---

## 🔔 Sub-Notch Container & Notification Pill

Di bawah badan notch utama, terdapat kontainer dinamis `SubNotchContainer` yang menampung kartu/pill mini tambahan (seperti timer running, stopwatch, dan notifikasi):

### 1. Posisi Dinamis Sub-Notch (`SubNotchBaseTranslate.Y`)
- Koordinat Y Sub-Notch dihitung secara dinamis di [NotchWindow.xaml.cs](../NotchWin/Views/NotchWindow.xaml.cs):
  `SubNotchBaseTranslate.Y = Math.Round(notchHeight - 1.0);`
- Nilai dibulatkan ke bilangan bulat (integer pixel) untuk mencegah blur interpolasi bilinear pada jendela transparan. Hal ini memastikan `SubNotchContainer` selalu menempel persis di bibir bawah notch baik saat berukuran `Collapsed` (32px) maupun `Expanded` (258px – 310px, Todo dinamis hingga 634px).

### 2. Notifikasi Sub-Notch Saat Notch Expanded (`NotificationSubNotchPill`)
- Notifikasi storage/USB (`TriggerUsbNotification`) hanya dipicu untuk `WM_DEVICECHANGE` dengan `dbch_devicetype == DBT_DEVTYP_VOLUME`; event device-interface lain (mis. Bluetooth dimatikan) diabaikan.
- Ketika notch sedang dibuka/expanded (atau slide apapun dibuka) dan ada notifikasi masuk (USB, reminder, clipboard, alarm, dll.), notifikasi tidak menggantikan layar slide, melainkan meluncur turun di bawah notch sebagai **Sub-Notch Pill**:
  - **Visual & Tipografi**: Tinggi `28px`, sudut bawah melengkung (`CornerRadius="0,0,12,12"`), border semi-transparan, ikon kategori, judul tebal (`11.5pt`), pesan (`11pt`), dan tombol tutup silang vektor.
  - **Arsitektur Anti-Mojibake (Keputusan 75)**:
    - Tombol tutup (`NotificationDismissButton`) menggunakan elemen vektor `Path` native:
      ```xml
      <Path Name="NotificationDismissPath" Data="M 0,0 L 7,7 M 7,0 L 0,7" 
            Stroke="#8E8E98" StrokeThickness="1.2" StrokeStartLineCap="Round" 
            StrokeEndLineCap="Round" SnapsToDevicePixels="True" UseLayoutRounding="True" />
      ```
    - Menghilangkan sepenuhnya ketergantungan encoding karakter teks yang sebelumnya memicu mojibake `Ā¢Å"â€¢`.
  - **Arsitektur Layer Terisolasi (Anti-Blur)**:
    - Efek `DropShadowEffect` **hanya** ditempatkan pada layer background kosong (`NotificationPillBg`).
    - Konten teks dan ikon ditempatkan di layer terpisah tanpa `Effect`. Hal ini mencegah pipeline WPF me-raster teks menjadi bitmap beresolusi rendah dan menjaga ClearType RGB subpixel tetap aktif.
    - Menggunakan font `Segoe UI Variable Text, Segoe UI, sans-serif`, `TextOptions.TextFormattingMode="Display"`, `TextOptions.TextRenderingMode="ClearType"`, serta `SnapsToDevicePixels="True"` dan `UseLayoutRounding="True"`.
  - **Animasi (`AnimateNotificationSubNotch`)**: Slide turun dari `Y = -30px` dengan `BackEase` (overshoot halus) dan fade-in opasitas. Saat ditutup/selesai durasi, pill meluncur naik kembali ke belakang badan notch.
  - **Interaksi Mouse & Hit-Testing**: Posisi pill didaftarkan ke hit-test cursor window (`FilterMessage` / hover tracking) sehingga kursor yang berada di atas pill sub-notch tidak menyebabkan notch menutup sendiri.

---

## 🟢 Overlay Indikator Privasi (Ujung Kanan Notch)

Dot privasi kamera (hijau `#22C55E`) dan mikrofon (oranye `#F97316`) adalah **overlay mengambang** di ujung kanan notch, bukan bagian dari layout widget:
- **Dua host, satu state**: `PrivacyOverlayHost` adalah child terakhir `CollapsedContentGrid`, sedangkan `ExpandedPrivacyOverlayHost` adalah overlay `Grid.ColumnSpan=3` di header expanded di [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml). Keduanya di-animasikan bersama oleh `AnimatePrivacyOverlay`, dan karena `CollapsedContentGrid` di-fade ke opacity 0 saat expanded (sebaliknya `ExpandedContentGrid` tersembunyi saat collapsed), hanya satu yang terlihat. Keduanya ter-clip oleh bentuk notch.
- **Posisi**: collapsed — `HorizontalAlignment="Right" VerticalAlignment="Top"` dengan margin kanan `5px` dan margin atas `2.5px` (dot 5×5, glow `DropShadowEffect` blur 4), menempel di bibir ujung kanan notch (di atas area karakter maskot, bukan tepat di pusat karakter); expanded — `Grid.ColumnSpan=3` + `HorizontalAlignment="Right"` dengan margin kanan `16px` dan atas `-3.5px` (kompensasi margin 6px `ExpandedContentGrid`, jadi y absolut tetap 2.5), melayang di kanan-atas notch dengan posisi tetap yang tidak terikat karakter maskot. Jika Pet Companion dimatikan, posisi collapsed tetap di ujung kanan. `IsHitTestVisible="False"` agar klik/drag area kanan notch tidak terganggu.
- **Animasi sekali jalan**: saat `IsPrivacyActive` menjadi true, dot meluncur turun dari `Y=-10` (di luar clip layar, karena notch menempel di tepi atas monitor) ke `Y=0` (340 ms `QuarticEase` EaseOut) sambil fade-in 160 ms.

---

## 🚀 Animasi Masuk saat Startup & Sapaan Maskot (Startup Entrance - Keputusan 77)

Saat Kuri dijalankan (termasuk autostart), notch muncul secara sangat fluida dan organik tanpa kedipan jam/baterai prematur:
- **State Awal Bersih di XAML & Konstruktor Window**:
  - `PillContainer` dan `ContentHost` didefinisikan dengan `Opacity="0.0"`, `PillTranslateTransform.Y="-28.0"`, dan `PillScaleTransform ScaleX/Y="0.9"` sejak awal di XAML.
  - `PrepareStartupEntrance()` dipanggil langsung di dalam konstruktor `public NotchWindow()` (sebelum `window.Show()`), mengunci seluruh konten agar benar-benar tidak terlihat (*invisible*) dan menyembunyikan target pet (`HideGreetingTargetPets()`) sebelum frame pertama dikirim ke DWM Windows.
  - `WelcomeOverlay` disiapkan di tengah notch (`Visibility = Visible`, `Opacity = 0.0`, skala 1.05) siap beranimasi.
- **Koreografi Animasi Masuk Terpadu (`PlayStartupEntranceAnimation`)**:
  - Pil notch meluncur turun `Y -28→0` (400 ms `QuarticEase` EaseOut) dan memudar masuk `PillContainer.Opacity 0→1` (280 ms).
  - Skala pil melakukan pop elastis `0.9→1` (460 ms `BackEase` Amplitude 0.22).
  - Maskot selamat datang (`WelcomePet`) memudar masuk di tengah notch dan melambaikan tangan selama 3 detik penuh. Selama periode ini, konten jam, tanggal, cuaca, dan baterai (`ContentHost`) tetap berada pada `Opacity = 0.0` sehingga notch hanya fokus menyapa pengguna.
  - Setelah 3 detik, maskot meluncur mulus ke posisi sudut kanan pet (`TryGetGreetingSlideOffset`, 560 ms `CubicEase`), dan `ContentHost` memudar masuk halus di belakangnya (360 ms) membawa jam, tanggal, dan baterai ke pandangan.

---

## 🎯 Mode Fullscreen Slim Bar (v0.5.0)

Saat **FullscreenDetector** mendeteksi aplikasi layar penuh (borderless game, video F11, presentasi) dan `AppSettings.IsFullscreenAutoHideEnabled` aktif:
1. **Mode default (`IsFullscreenSlimBarEnabled = true`)**:
   - `NotchViewModel.IsSlimBarVisible` bernilai `true`.
   - Window **tidak di-collapse**. Pill notch dianimasikan keluar (scale + fade), lalu elemen `FullscreenSlimBarHost` di [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml) menampilkan garis kapsul `220×6 px` (mengikuti `NotchScale`) di tengah atas layar.
   - Garis menggunakan material tema (`NotchBackgroundBrush` / `NotchStrokeBrush`), `IsHitTestVisible="False"`, dan `WM_NCHITTEST` mengembalikan `HTTRANSPARENT` sehingga 100% click-through.
2. **Hover-Peek 3 detik**:
   - Arahkan kursor ke area garis tipis selama 3 detik. `SlimBarHoverScale` membesar sebagai indikator, lalu memicu `AnimateSlimBarMorphToNotch()`.
   - Saat peek aktif, panel penuh **hanya dibuka via klik** (`OnPillClicked()` -> `Expand(isLock: false)`). Hover saja tidak membuka panel agar tidak mengganggu aktivitas fullscreen.
   - Pindahkan kursor keluar untuk memicu morph kembali ke garis kapsul tipis.

---

## 🚫 Panduan Modifikasi Aman (Do's & Don'ts)
- **JANGAN** menempatkan `DropShadowEffect` atau efek bitmap langsung pada elemen `TextBlock` atau geometri teks karena akan mematikan ClearType dan me-raster teks menjadi bitmap buram beresolusi rendah.
- **JANGAN** menggunakan koordinat pecahan (seperti `Y = notchHeight - 1.5` atau `Y = 30.5`) pada elemen visual; selalu bulatkan ke pixel bulat (`Math.Round(...)`).
- **JANGAN** menggunakan karakter glif teks mentah (seperti `✕`, `▾`, `✓`) di XAML yang rentan rusak encoding menjadi mojibake (`Ā¢Å"â€¢`); gunakan elemen geometri `Path` vektor native.
- **JANGAN** mengubah urutan tombol `NavTab0` - `NavTab8` di XAML tanpa menyelaraskan `EnsureNavPillComponents` dan `MapSlideToPhysicalTabIndex` di [NotchWindow.xaml.cs](../NotchWin/Views/NotchWindow.xaml.cs).
- **JANGAN** memanggil `UpdateNavPill` sebelum window selesai di-load (`IsLoaded == true`).
- **JANGAN** menghapus gaya `WS_EX_TOOLWINDOW` dan `WS_EX_NOACTIVATE` di [WindowStyleHelper.cs](../NotchWin/Interop/WindowStyleHelper.cs) agar notch tidak mencuri fokus keyboard dari aplikasi lain.
- **JANGAN** mengandalkan assignment `Topmost = true` untuk menjaga z-order; WPF tidak me-restack HWND bila nilainya sudah `true`. Gunakan `WindowStyleHelper.EnsureTopmost()` dan biarkan topmost keeper (`NotchWindow`) berjalan.
