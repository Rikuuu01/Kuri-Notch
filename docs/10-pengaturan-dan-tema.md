# ⚙️ Modul 10: Pengaturan & Kustomisasi Tema

> Indeks seluruh dokumentasi: [README.md](README.md)

Modul ini bertanggung jawab atas jendela preferensi pengguna (`SettingsWindow`), kustomisasi material latar belakang notch, manajemen warna aksen tema, dan integrasi tingkat sistem Windows.

---

## 📂 File Terkait
- **View XAML**: [SettingsWindow.xaml](../NotchWin/Views/SettingsWindow.xaml)
- **View Code-Behind**: [SettingsWindow.xaml.cs](../NotchWin/Views/SettingsWindow.xaml.cs)
- **ViewModel**: [SettingsViewModel.cs](../NotchWin/ViewModels/SettingsViewModel.cs)
- **Services & Helpers**:
  - [SettingsService.cs](../NotchWin/Services/SettingsService.cs) (Penyimpanan JSON di `%APPDATA%\NotchWin\settings.json`)
  - [ThemeManager.cs](../NotchWin/Services/ThemeManager.cs) (Logika material & palet warna gradasi)
  - [FontScaleService.cs](../NotchWin/Services/FontScaleService.cs) (Pengaturan skala ukuran font & teks notch secara real-time)
  - [LocalizationService.cs](../NotchWin/Services/LocalizationService.cs) (Kamus bahasa Indonesia & Inggris)
  - [AutostartService.cs](../NotchWin/Services/AutostartService.cs) (Scheduled Task logon + fallback Registry `HKCU\...\Run`)
  - [FullscreenDetector.cs](../NotchWin/Services/FullscreenDetector.cs) (Deteksi aplikasi game/video layar penuh)
  - [DisplayService.cs](../NotchWin/Services/DisplayService.cs) (Deteksi multi-monitor)

---

## 🎨 Komponen & Alur Kerja

### 1. Pilihan Material Latar Belakang Notch
Terdapat 3 pilihan material visual yang diatur via `ThemeManager`:
1. **True OLED Black**: Warna hitam pekat solid (`#0A0A0C` hingga `#121216`) dengan kontras tinggi untuk layar OLED.
2. **Tinted Glass**: Gradasi gelap tembus pandang semi-transparan dengan efek kaca modern.
3. **Fluent Mica / Acrylic**: Nuansa akrilik transparan lembut khas Windows 11.

#### Transisi Warna Mulus Bebas Glitch Hitam
- Animasi pergantian tema diatur di `AnimateThemeTransition` pada [NotchWindow.xaml.cs](../NotchWin/Views/NotchWindow.xaml.cs).
- **Teknik**: Menerapkan `ColorAnimation` langsung ke 4 titik `GradientStop` (`Offset: 0.0, 0.35, 0.70, 1.0`) pada satu brush aktif tanpa menumpuk dua layer path semi-transparan (yang sebelumnya menyebabkan efek hitam sesaat akibat perkalian opasitas ganda).

### 2. Manajemen Warna Aksen Tema
- **Opsi Aktifkan Warna Aksen (`IsAccentColorEnabled`)**: Pengguna dapat mematikan warna aksen tema jika menginginkan tampilan monokrom/netral (*sleek dark neutral monochrome*) tanpa saturasi warna. Saat dimatikan, notch, slider, dan tombol menggunakan palet zinc/silver yang elegan dan gradasi latar belakang bebas dari warna aksen.
- **Palet Preset**: Biru, Ungu, Zamrud, Mawar, Amber, Sian.
- **Warna Hex Kustom**: Kotak input kode heksadesimal 6 digit (contoh `#3B82F6`) dengan validasi regex otomatis.
- **Penyelarasan Warna Cover Musik**:
  - Jika "Warna Aksen Dinamis Album" diaktifkan dan pemutar musik sedang berjalan, warna cover album diutamakan (hanya berlaku jika warna aksen diaktifkan).
  - Jika user mengganti warna aksen di pengaturan saat musik sedang berjalan, warna aksen kustom yang dipilih disimpan ke pengaturan dan baru aktif secara visual ketika musik berhenti berputar.

### 3. Kustomisasi Notch Tertutup (Status Bar)
Secara default, bilah notch saat tertutup (*collapsed state*) hanya menampilkan **Hari/Tanggal** (sisi kiri) dan **Jam Digital** (sisi kanan) dengan lebar ringkas (`186 DIPs`). Elemen tambahan dapat diaktifkan melalui Pengaturan (Panel 2 - Widget Notch):
1. **Tampilkan Cuaca di Notch Tertutup (`ShowWeatherInCollapsedNotch`)**: Menampilkan ikon kondisi cuaca dan suhu di samping tanggal (menambah lebar +58 DIPs).
2. **Tampilkan Baterai di Notch Tertutup (`ShowBatteryInCollapsedNotch`)**: Menampilkan persentase dan bar indikator baterai di samping jam (menambah lebar +52 DIPs).
3. **Tampilkan Maskot di Notch Tertutup (`ShowPetInCollapsedNotch`)**: Menampilkan animasi maskot di sisi kanan (menambah lebar +28 DIPs).
4. **Widget Media Bilah Tertutup (`IsMediaWidgetEnabled`)**: Default dimatikan agar musik tidak otomatis mengambil alih bilah notch tertutup kecuali pengguna mengaktifkannya.

### 4. Integrasi Sistem Windows
- **Start dengan Windows (Autostart)**: 
  - Menggunakan **Scheduled Task** `Kuri Autostart_<SID>` dengan trigger *At log on* per pengguna sebagai sumber kebenaran utama, sehingga Kuri diluncurkan Task Scheduler lebih awal tanpa menunggu antrean startup Explorer (Run key).
  - Pendaftaran/penghapusan task dilakukan oleh helper `NotchWin.exe --autostart-task=enable|disable|status` (dipakai juga oleh installer & uninstaller) dan memerlukan persetujuan Administrator (UAC). XML task memakai `ExecutionTimeLimit=PT0S` agar tidak dihentikan paksa setelah 72 jam, `MultipleInstancesPolicy=IgnoreNew`, serta `DisallowStartIfOnBatteries=false`.
  - Jika UAC ditolak atau task tidak dapat diverifikasi untuk pengguna saat ini, `AutostartService` jatuh ke fallback Registry `HKEY_CURRENT_USER\...\Run` nama `NotchWin` dan UI menampilkan peringatan. Saat task valid, nilai Run lama dihapus agar tidak ada peluncuran ganda.
  - Melakukan pembersihan otomatis (*auto-cleanup*) terhadap berkas shortcut redundan di direktori `Startup` (`%APPDATA%\...\Startup` dan `%ProgramData%\...\Startup`) saat status autostart diubah, guna mencegah peluncuran instans ganda saat proses login Windows.
- **Auto-Hide saat Layar Penuh**: `FullscreenDetector` memindai jendela aktif di foreground; jika jendela tersebut mencakup seluruh resolusi layar (game atau video fullscreen), notch otomatis disembunyikan agar tidak menutupi gameplay.
  - **Sub-opsi `IsFullscreenSlimBarEnabled` (default `true`)**: alih-alih sembunyi total, notch berubah menjadi **garis tipis** di tengah atas layar yang 100% click-through. Matikan sub-opsi ini untuk kembali ke perilaku sembunyi total.
  - **Hover-peek**: arahkan kursor ke garis tipis selama **3 detik** untuk membuka notch kembali. Garis akan membesar sebagai indikator, lalu bermorph menjadi pill notch. Saat fullscreen, **klik pill** untuk membuka panel penuh — hover saja hanya mempertahankan pill (berbeda dari notch biasa yang terbuka otomatis saat hover). Notch bermorph balik menjadi garis tipis saat kursor keluar, kecuali sedang dipin.
  - Detektor melakukan pengecekan langsung saat `Start()` sehingga status fullscreen yang sudah aktif terdeteksi tanpa jeda 1,5 detik.
  - Saat foreground window adalah proses `NotchWin` sendiri (misalnya ketika notch di-hover atau diklik), detektor mempertahankan status fullscreen terakhir agar peek tidak langsung ditutup.
- **Pemilihan Monitor**: `DisplayService` mendeteksi seluruh monitor yang terhubung ke PC; pengguna dapat memilih di layar monitor mana notch ingin ditampilkan.

### 4. Lokalisasi Bahasa (Bilingual)
- Mendukung pergantian bahasa instan secara runtime antara **Bahasa Indonesia** dan **English** tanpa perlu me-restart aplikasi melalui `LocalizationService.Instance`.
- Kamus terus diperluas per modul; cakupan terkini termasuk seluruh kunci **Phone Share** (`Phone.*` untuk panel/notifikasi PC, `PhonePage.*` untuk menyuntik teks halaman web HP), **Konversi Gambar** (`DropAction.ConvertTitle`, `Convert.SuccessTitle/SuccessMessage/FailedTitle/FailedMessage/NothingTitle/NothingMessage`), **Shelf** (`Shelf.EmptyCount/OneItem/ItemsCount/PhoneButton/PhoneTooltip/AddButton/ClearButton/SelectFilesTitle`), **glow outline** (`Settings.NotchTimerGlowTitle/Desc`, `Settings.NotchAlertGlowTitle/Desc`), dan **tampilan lirik** (`Settings.LyricsKaraokeTitle/Desc`, `Settings.LyricsBoldTitle/Desc`, `Settings.LyricsTextSizeTitle/Desc`). ViewModels yang menampilkan teks dihitung-ulang lewat event `LanguageChanged` (mis. `HomeCalendarViewModel.UpdateWeekView()` mengganti huruf hari M-S-S-R-K-J-S ↔ S-M-T-W-T-F-S).

### 5. Pengaturan Durasi Notifikasi (`NotificationDurationSeconds`)
- Slider di jendela preferensi untuk mengatur durasi tampilnya pop-up notifikasi notch:
  - Rentang: `2` hingga `10` detik (Default: `3` detik).
  - Pengecualian: Notifikasi Jadwal Sholat dan Adzan dikecualikan dari durasi umum ini karena memiliki countdown dan durasi khusus tersendiri.
  - Nilai tersimpan di `AppSettings.NotificationDurationSeconds` dan dikonsumsi oleh `NotchViewModel.TriggerTransientNotification` serta `NotchViewModel.TriggerUsbNotification`.

### 6. Pengaturan Skala Notch & Ukuran Fisik (`NotchScale`)
- Slider di jendela preferensi untuk menyesuaikan ukuran fisik notch:
  - Rentang: `75%` (`0.75`) hingga `140%` (`1.40`) dengan kenaikan step `5%` (`0.05`).
  - **Baseline 100% Terkalibrasi**: Ukuran 100% (`1.0`) pada slider kini mewakili ukuran fisik optimal (setara 1.2x dari dimensi awal), sehingga notch langsung terlihat proporsional tanpa harus digeser manual.
  - Nilai tersimpan di `AppSettings.NotchScale` dan secara reaktif memicu `ApplyNotchScale` pada `NotchWindow`.

### 7. Efisiensi Memori Latar Belakang (RAM Optimization)
- Aplikasi dirancang untuk berjalan sangat hemat di background (~54 MB – 62 MB).
- Menghadirkan pemangkasan otomatis saat idle tanpa mengorbankan performa 60fps melalui `MemoryManagementService`.

### 8. Suara Selamat Datang saat Startup (`StartupSoundEnabled`)
- Chime selamat datang (~1.9 detik, arpeggio C5–E5–G5–C6) diputar sekali setelah notch utama tampil melalui `NotchViewModel.PlayStartupSoundIfEnabled()` di `App.OnStartup`; hanya instance utama yang memutar suara (mode helper autostart dan instance kedua yang langsung keluar tidak memutar).
- Toggle **Startup Welcome Sound** di Pengaturan → Sistem & Perilaku (default aktif). Menghidupkan toggle memutar pratinjau chime; nilainya tersimpan di `AppSettings.StartupSoundEnabled`.
- WAV digenerate runtime (16-bit PCM mono 44.1 kHz) di `SoundNotificationService.GenerateStartupChimeWav()` sehingga tidak menambah berkas aset.

### 9. Pengaturan Ukuran Font / Skala Teks (`FontScale`) & Tipografi HD (Keputusan 73)
- **Penjajaran Grid Half-Point (`FontScaleService`)**:
  - Ukuran font dihitung dalam kelipatan 0.5pt (`Math.Round(baseSize * scale * 2.0) / 2.0`) untuk menjamin keselarasan glif dengan tabel *font hinting* DirectWrite.
  - Mengeliminasi blur teks akibat koordinat render piksel pecahan pada DPI scaling tinggi.
- **Standar Tipografi Fluent Windows 11**:
  - Seluruh teks antarmuka menggunakan font resmi `Segoe UI Variable Text, Segoe UI, sans-serif` dengan `TextOptions.TextFormattingMode="Display"`, `TextOptions.TextRenderingMode="ClearType"`, dan `RenderOptions.ClearTypeHint="Enabled"`.
- Fitur penyesuaian ukuran teks seluruh antarmuka notch secara real-time via `FontScaleService`:
  - Rentang skala: `80%` (`0.80`) hingga `130%` (`1.30`) dengan step `5%` (`0.05`). Default `100%` (`1.0`).
  - Tombol stepper cepat `A−` dan `A+` serta chip preset instan: `Kecil (85%)`, `Standar (100%)`, dan `Besar (115%)`.
  - Dilengkapi **Live Preview Card** interaktif di jendela Pengaturan yang menampilkan contoh tampilan collapsed notch (suhu cuaca, judul lagu, nama artis, jam, dan baterai) yang langsung berubah ukurannya sesuai pengaturan skala font.
  - Perubahan skala tersimpan di `AppSettings.FontScale` dan diterapkan secara instan ke seluruh elemen teks pada notch tanpa perlu me-restart aplikasi.

### 10. Sakelar Grup Baru: Glow Outline, Tampilan Lirik, Shelf & Phone Share
Kelompok toggle berikut lahir bersama fitur-fitur v1.1+ dan disimpan di `settings.json`; detail perilaku tiap fitur ada di modulnya masing-masing.
- **Notch Outline Glow** (tab Notch/Tampilan — [08-timer-dan-stopwatch.md](08-timer-dan-stopwatch.md), Keputusan 78):
  - `IsNotchTimerGlowEnabled` (default `true`): garis cahaya sisa waktu timer/pomodoro & detik stopwatch di outline notch tertutup.
  - `IsNotchAlertGlowEnabled` (default `true`): cahaya berjalan keliling outline saat alert tampil (maks 20 detik).
- **Tampilan Lirik & Spicy Lyrics** (tab Musik — [02-music-player-dan-lirik.md](02-music-player-dan-lirik.md)):
  - `IsLyricsKaraokeEnabled` (default `true`) — isian progresif karaoke pada pill collapsed & baris aktif panel.
  - `IsSyllableSyncEnabled` (default `true`) — sinkronisasi suku kata presisi kata-demi-kata via Spicy Lyrics & TTML.
  - `SpicyLyricsApiKey` (default `""`) — token pengembang dari `developers.spicylyrics.org` untuk pencarian tanpa batas pustaka lagu lengkap.
  - `LyricsBold` (default `true`) — baris aktif tebal.
  - `LyricsTextSize` (slider `10–14`, default `12.0`) — ukuran basis baris lirik; baris aktif = basis + 2.5.
- **Shelf & Phone Share** (tab khusus — [09-shelf-dan-dock.md](09-shelf-dan-dock.md) & [14-phone-share.md](14-phone-share.md)):
  - `ShelfRemoveAfterDrag` (default `false`): item shelf otomatis terhapus dari daftar setelah berhasil di-drag keluar dari notch.
  - `IsPhoneShareEnabled` (default `true`): menampilkan tombol `HP` di slide Shelf; mematikan toggle saat server berjalan langsung memanggil `PhoneShare.Stop()`.
  - `PhoneFolder` (default kosong = `<Downloads>\Kuri Shelf`): folder tujuan file yang diunggah dari HP, dipilih via dialog `FolderBrowser` (`BrowsePhoneFolderCommand`).
- **Penjagaan registrasi**: setiap properti baru `AppSettings` WAJIB didaftarkan pada konstruksi `new AppSettings { ... }` di `SettingsViewModel.SaveCurrentSettingsAsync()` dan `ResetToDefaultsAsync()`; tanpa itu nilainya hilang diam-diam saat save/reset (kesalahan yang sudah tiga kali terjadi di proyek ini).

### 11. Pengaturan Kustomisasi Modul Navbar (`NavbarModules` - Keputusan 97)
Kategori baru **🧭 Modul Navbar** di sidebar Pengaturan memungkinkan pengguna mengatur susunan tata letak menu notch mirip fitur kustomisasi pada aplikasi Poni:
- **10 Modul Standar**: Home (`home`), Control Center (`control`), Media Player (`media`), Weather (`weather`), Prayer Times (`prayer`), Water & Posture (`water`), Quick Notes (`note`), Clipboard History (`clipboard`), Timer & Stopwatch (`timer`), Drop Shelf & Phone Share (`shelf`).
- **Pemindahan Urutan (Reorder)**: Tombol panah stepper `▲` dan `▼` di sebelah kiri kartu modul untuk memindahkan posisi modul ke kiri atau ke kanan pada navbar notch.
- **Aktifkan / Nonaktifkan (Enable / Disable)**: Sakelar toggle `FluentToggleSwitch` untuk menyembunyikan atau menampilkan tab modul. Modul `home` (Beranda/Dashboard) terkunci permanen (`CanDisable = false`) sebagai jangkar utama.
- **Tombol Pulihkan Urutan**: Tombol `↺ Pulihkan Urutan` di pojok kanan atas untuk mengembalikan susunan dan status modul ke urutan bawaan pabrikan dalam 1-klik.
- **Sinkronisasi Dinamis**: Perubahan modul disimpan secara persisten di `AppSettings.NavbarModules` dan langsung diadaptasi oleh `NotchWindow.SyncNavbarModuleConfiguration()` secara real-time tanpa jeda atau perlu me-restart aplikasi.

---

## 🚫 Panduan Modifikasi Aman (Do's & Don'ts)
- **JANGAN** menggunakan tumpukan dua path animasi silang (`CrossFade`) pada material transparan Tinted/Mica karena akan menimbulkan kedipan warna hitam pekat saat nilai alpha bertemu.
- **JANGAN** mengubah kunci string `L10n[...]` di XAML tanpa mendaftarkannya pada kamus bahasa Inggris dan Indonesia di [LocalizationService.cs](../NotchWin/Services/LocalizationService.cs).
