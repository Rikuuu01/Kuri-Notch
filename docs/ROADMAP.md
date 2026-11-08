# Kuri (NotchWin) — Roadmap & Rencana Produk

> Indeks seluruh dokumentasi: [README.md](README.md)

> **Dokumen Perencanaan & Roadmap Produk**  

> *Dibuat:* 22 Oktober 2026 · *Terakhir Diperbarui:* 29 Oktober 2026 (Versi 0.5.0)  

> *Status:* **Fase 1 s/d Fase 16 Selesai (Aktif - Spicy Lyrics TTML Dual-Pipeline, Syllable Sync, Provider Badges, 60 FPS Anti-Stutter Karaoke, Phone Share, Pure Vector Mascot V2, Fullscreen Slim Bar)**

---

## 📌 Brief & Visi Produk

Membangun aplikasi Windows modern bernama **NotchWin** yang menghadirkan pengalaman Dynamic Notch / Dynamic Island ala Apple di Windows 10 dan Windows 11. Aplikasi ini menempel secara organik di bagian tengah atas layar monitor, berjalan sangat ringan di background (idle CPU ~0%), tidak mencuri fokus saat mengetik atau bermain game, dan dapat mengembang (*expand*) menjadi panel informasi serbaguna berbasis multi-slide carousel.

### Karakteristik Utama Produk

1. **Pill Kecil Menempel Bezel (*Collapsed*)**: Berada di tengah atas layar, menyatu dengan bezel monitor melalui sudut lengkung keluar (*outward top ears / fillet*), tembus klik di area luar (*click-through* via `WM_NCHITTEST`).

2. **Panel Kontrol Serbaguna (*Expanded*)**: Mengembang secara organik saat kursor di-hover atau diklik, menyediakan 8 kartu slide carousel interaktif: Pusat Kontrol, Pemutar Media & Lirik, Cuaca, Jadwal Sholat, Pelacak Minum/Duduk, To-Do List, Timer/Stopwatch, dan File Shelf.

3. **Shortcut Dock Mengambang**: Bar pintasan aplikasi/file tepat di bawah notch dengan dukungan drag-and-drop file untuk disimpan ke Shelf atau disematkan ke Dock.

4. **Ringan & Senyap**: Berjalan di System Tray, tidak muncul di taskbar maupun Alt+Tab (`WS_EX_TOOLWINDOW`), tidak menggunakan global hook berbahaya demi kompatibilitas penuh dengan sistem anti-cheat game.

---

## 💻 Tech Stack & Arsitektur

| Komponen | Teknologi Terpilih | Catatan & Peran |

| :--- | :--- | :--- |

| **Framework & Bahasa** | C# (.NET 8 LTS) + WPF | Target `net8.0-windows10.0.19041.0` untuk akses native WinRT API |

| **Pola Arsitektur** | MVVM (CommunityToolkit.Mvvm) | Pemisahan bersih antara View, ViewModel, dan Services |

| **Kontrol & Info Media** | `Windows.Media.Control` (WinRT SMTC) | Membaca Spotify, YouTube di browser, Apple Music, VLC tanpa polling |

| **Audio & Volume** | `NAudio.CoreAudioApi` | Volume Master riil, deteksi device audio, dan peak-level mikrofon |

| **Lirik Karaoke** | LRCLIB API + Custom LRC Parser | Pencarian lirik sinkron otomatis dengan sistem kompensasi latensi GSMTC |

| **Cuaca & Geocoding** | Open-Meteo API + Nominatim OSM | Cuaca real-time, prakiraan 7 hari, dan pencarian lokasi global |

| **Jadwal Sholat** | Aladhan API | Jadwal 5 waktu sholat harian + countdown waktu sholat berikutnya |

| **Sensor Privasi** | Windows Media Foundation + WASAPI | Deteksi hardware webcam (`mfsensorgroup.dll`) & mic tanpa polling boros |

| **Penyimpanan Setting** | `System.Text.Json` | File `%AppData%\NotchWin\settings.json` dengan pembaruan reaktif |

| **Logging** | Serilog | File rolling log di `%AppData%\NotchWin\logs` |
| **QR Code** | QRCoder 1.8.1 (`PngByteQRCode`) | QR lokal Phone Share, tanpa layanan eksternal |

| **Paket Instalasi** | Inno Setup 6 + PowerShell Builder | Installer Universal Setup `.exe` (x64/x86 auto-detect) & Portable ZIP |

---

## 📂 Struktur Proyek Terkini

```text

DynamicNotchWindows/
├── NotchWin/
│   ├── Assets/              # Ikon audio adzan, notifikasi audio, app.ico, & PhoneShare/index.html (embedded)
│   ├── Converters/          # Value converter XAML (Visibility, Boolean, WidgetType, KaraokeClip, dll.)
│   ├── Interop/             # P/Invoke Win32, WindowStyleHelper, QuickActionHelper
│   ├── Models/              # Model data: WeatherData, PrayerSchedule, ShelfItem/ShelfState, ShortcutItem, dll.
│   ├── Services/            # Logika bisnis sistem, integrasi hardware, & API eksternal
│   │   ├── MediaService.cs, LyricsService.cs, AlbumColorExtractor.cs
│   │   ├── WeatherService.cs, PrayerTimesService.cs, WaterTrackerService.cs
│   │   ├── ShelfService.cs, ShortcutService.cs, TodoService.cs
│   │   ├── NoteService.cs, ClipboardService.cs
│   │   ├── PhoneShareService.cs, ImageConvertService.cs (fitur HP → Shelf & konversi gambar)
│   │   ├── VolumeService.cs, BrightnessService.cs, BatteryService.cs
│   │   ├── PrivacyService.cs, FullscreenDetector.cs, DisplayService.cs
│   │   ├── LocalizationService.cs, SettingsService.cs, SoundNotificationService.cs
│   │   └── TrayService.cs, AutostartService.cs, SystemMonitorService.cs
│   ├── ViewModels/          # ViewModel tiap modul & NotchViewModel (Orchestrator utama)
│   ├── Views/
│   │   ├── NotchWindow.xaml, NotchWindow.xaml.cs   # Jendela utama notch
│   │   ├── NotchWindow.OutlineFx.cs                # Garis cahaya outline timer/alert (Keputusan 78)
│   │   └── SettingsWindow.xaml, SettingsWindow.xaml.cs # Jendela Pengaturan Fluent
│   ├── Widgets/             # Abstraksi INotchWidget & WidgetHost
│   └── app.manifest         # Konfigurasi Per-Monitor V2 DPI Awareness
├── installer/               # Skrip instalasi Inno Setup (.iss)
├── release/                 # Hasil build installer & paket portable
├── build-installers.ps1     # Skrip otomatisasi build installer x64/x86/Universal
├── run.bat                  # Skrip cepat compile & run untuk development
└── docs/                    # Seluruh dokumentasi proyek (.md)
    ├── README.md            # Dokumentasi utama & indeks
    ├── CARA-MENJALANKAN.md  # Panduan instalasi & pengujian lengkap
    ├── ROADMAP.md           # Dokumen ini (rencana & roadmap produk)
    ├── DECISIONS.md         # Catatan keputusan arsitektur (ADR)
    ├── PLAN-FULLSCREEN-SLIM-BAR.md
    ├── STYLE-SCROLLBAR.md
    └── 00-…14-…md           # Spesifikasi teknis per modul fitur
```

---

## 🚀 Riwayat & Roadmap Pengembangan

### ✅ Fase 1: MVP Pill & Animasi Geometri (Selesai)

- [x] Window transparan, `Topmost=True`, tanpa tombol taskbar/Alt+Tab (`WS_EX_TOOLWINDOW`).

- [x] Geometri vektor kustom dengan lengkungan cekung keluar (*outward top ears / fillet* r: 12px) yang menyatu alami dengan bezel monitor.

- [x] State machine `Collapsed` (32px) dan `Expanded` (140-160px) dengan animasi `CubicEase`.

- [x] Klik tembus transparan (*click-through*) menggunakan `WM_NCHITTEST` dan `Geometry.FillContains`.

- [x] Non-activating window (`WS_EX_NOACTIVATE`) agar tidak mencuri fokus ketikan.

### ✅ Fase 2: Widget Media & Kontrol Musik SMTC (Selesai)

- [x] Membaca metadata lagu (judul, artis, album art, durasi, timeline) via Windows SMTC.

- [x] Tombol interaktif Play/Pause, Previous, Next, dan Interactive Seekbar Slider.

- [x] Ekstraksi warna dominan cover album secara dinamis untuk aksen tema notch.

- [x] Mode collapsed dinamis: cover mini + judul lagu bergerak saat ada media aktif.

### ✅ Fase 3: Widget Sistem & Prioritas Transien (Selesai)

- [x] Arsitektur `WidgetHost` untuk memprioritaskan widget aktif.

- [x] Deteksi baterai laptop cerdas (persentase, status charging, sembunyi di PC desktop).

- [x] Slider volume master Windows via NAudio dan HUD dinamis saat tombol volume fisik ditekan.

- [x] Jam digital presisi tinggi dan pemantau sumber daya sistem.

### ✅ Fase 4: Pengaturan, System Tray, Autostart, & Multi-Monitor (Selesai)

- [x] Jendela Pengaturan bergaya Fluent Dark Modern (posisi, skala, bahasa, preferensi widget).

- [x] Integrasi System Tray dengan menu klik kanan (Settings, Exit).

- [x] Autostart saat Windows menyala via scheduled task logon (fallback Registry Run key).

- [x] Single instance protection menggunakan `Named Mutex`.

- [x] Deteksi game / video layar penuh cerdas (`SHQueryUserNotificationState`) untuk sembunyi otomatis.

- [x] Penanganan multi-monitor dan reposisi otomatis saat konfigurasi monitor berubah.

### ✅ Fase 5: Privasi, Efisiensi Daya, & Rilis Installer v0.1 (Selesai)

- [x] Indikator privasi webcam (🟢 Hijau) via Windows Media Foundation callback.

- [x] Indikator privasi mikrofon (🟠 Oranye) via WASAPI session audio meter.

- [x] Penanganan sleep/standby laptop (`WM_POWERBROADCAST`) untuk menghentikan timer saat tidur.

- [x] Notifikasi banner dinamis saat flashdisk USB dicolok / dicabut.

- [x] Paket rilis self-contained portable dan installer Inno Setup.

---

### ✅ Fase 6: Multi-Slide Carousel & Widget Produktivitas (Selesai)

- [x] **8-Slide Carousel Hub**:

  1. *Slide 0 - Control Center*: Slider volume, slider kecerahan, tombol screenshot, lock PC, cuaca, settings.

  2. *Slide 1 - Media & Synced Lyrics*: Lirik lagu sinkron karaoke (LRCLIB), offset kalibrasi lirik, lirik otomatis memecah 2 baris saat panjang.

  3. *Slide 2 - Cuaca 7 Hari*: Suhu real-time, efek visual rintik hujan dinamis (*physics particle*), prakiraan mingguan, pencarian lokasi.

  4. *Slide 3 - Jadwal Sholat*: 5 waktu sholat harian, hitung mundur sholat berikutnya, notifikasi suara adzan.

  5. *Slide 4 - Water & Sedentary Tracker*: Target minum harian (+250ml), kartu 3D flip untuk statistik aktif di depan layar dengan sensor jeda cerdas.

  6. *Slide 5 - To-Do List*: Daftar tugas dengan modern slim capsule scrollbar, input tugas baru, coret selesai.

  7. *Slide 6 - Timer & Stopwatch*: Preset Pomodoro/timer cepat, stopwatch putaran (Lap), multitasking sub-notch pills.

  8. *Slide 7 - File Shelf*: Tempat penampungan file sementara.

- [x] **Dukungan Multi-Bahasa Dinamis (English / Bahasa Indonesia)**: Pergantian bahasa reaktif tanpa perlu restart.

---

### ✅ Fase 7: Shortcut Dock, Shelf & Drag-and-Drop Terpadu (Selesai)

- [x] **Dock Bar Mengambang di Bawah Notch**:

  - Tampilan dock modern minimalis dengan ikon bersih (tanpa label teks yang mengganggu).

  - Pembeda visual otomatis: badge folder untuk folder, badge file untuk file dokumen, badge panah untuk shortcut aplikasi.

  - Menu klik kanan pada ikon dock untuk menghapus shortcut tanpa membuat notch macet terbuka (*hover lock fix*).

- [x] **Interaksi Drag & Drop Cerdas**:

  - Drag file/folder/shortcut apa pun ke arah notch tertutup otomatis membuka notch.

  - Memunculkan panel pilihan tujuan drop: **Simpan di Shelf** (penyimpanan sementara) vs **Tambahkan ke Dock** (pintasan permanen).

  - Notifikasi toast halus langsung di dalam notch saat file berhasil ditambahkan.

---

### ✅ Fase 8: Penyempurnaan Tampilan Notch Tertutup & Animasi Lirik (Selesai - v0.2.0)

- [x] **Layout Notch Tertutup Harmonis**:

  - Informasi suhu cuaca di sisi paling kiri (`[Ikon Cuaca] [Suhu]` misal: `⛅ 28°C`).

  - Pemisah garis vertikal rapi (`|`) antara Cuaca dan Media, serta antara Media dan Jam/Baterai saat media diputar.

- [x] **Animasi Lirik Berurutan (*Sequential Karaoke Transition*)**:

  - Pergantian bait lirik yang mulus:

    $$\text{Kalimat 1 muncul} \longrightarrow \text{Kalimat 1 fade out meluncur ke atas} \longrightarrow \text{Jeda 130ms} \longrightarrow \text{Kalimat 2 fade in meluncur naik dari bawah}$$

  - Alternasi slot ganda (Slot A dan Slot B) tanpa kedipan (*flicker*) atau lonjakan teks statis.

  - Indikator 3-titik gelombang dinamis otomatis saat lagu memasuki bait jeda instrumen (*interlude*).

- [x] **Paket Rilis Versi 0.2.0**:

  - Pembaruan installer Inno Setup Universal (`NotchWin-Setup-v0.2.0-Universal.exe`) yang mendukung Windows 10 & 11 (64-bit dan 32-bit).

---

### ✅ Fase 9: Kustomisasi Material, Custom Hex Accent, Clipboard History & Note Suite (Selesai - v0.2.1)

- [x] **Kustomisasi Material Latar Belakang Notch**:

  - Pilihan material visual: **True OLED Black** (hitam pekat murni), **Tinted Glass** (kaca gelap transparan elegan ~85%), dan **Fluent Mica/Acrylic** (semi-transparan bergaya Windows 11 ~72%).

  - Kartu komponen dan border internal notch otomatis beradaptasi dengan material yang dipilih.

- [x] **Pilihan Warna Aksen Kustom Manual (HEX Code)**:

  - Opsi `"Custom (Hex)"` pada pengaturan warna tema dengan input kode hex (`#RRGGBB`) dan kotak live preview warna.

  - Parsing dinamis varian primary, light tint (+45 RGB), dan stroke glow.

- [x] **Clipboard History / Pengelola Papan Klip**:

  - Win32 API listener hemat daya (`WM_CLIPBOARDUPDATE`) tanpa polling (0% CPU idle).

  - Riwayat teks dan gambar screenshot dengan tombol salin kembali 1-klik, pencarian real-time, pin, dan hapus riwayat.

- [x] **Transformasi Note & Productivity Suite (Sesuai Screenshot)**:

  - Penggantian Todo list menjadi Note & Productivity Suite dengan layout split 600px:

    - **Sisi Kiri**: Editor catatan multi-tab dengan autosave instan (`notes.json`), placeholder identik, dan toolbar format lengkap (`B`, `I`, `S`, `</>`, Bullet list, Numbered list, Checklist, Done mark, Remind, Copy, Delete).

    - **Sisi Kanan**: Focus Circular Countdown Timer (`5:00` ring display, tombol Start/Pause, Custom, quick presets 5, 10, 15, 25, 45 min) dan Smart Natural Reminders input (deteksi otomatis *"in 20 minutes"*, *"tomorrow at 9"*, dll.).

---

### ✅ Fase 10: Interactive Vector Pet Companion & Reaktivitas Notifikasi (Selesai - v0.2.2)
- [x] **Transisi ke Pure-Vector Dual Mascots**:
  - Eliminasi total aset raster pihak ketiga yang buram / ber-glitch, digantikan oleh grafis vektor XAML 100% tajam dan terakselerasi hardware.
  - **Astro Mascot** (`AstroMascot` / `AstroMascotControl`): Astronot berhelm putih glossy dengan visor navy bermata amber, headphone groove reaktif, dan 8 badge notifikasi.
  - **Pastel Mochi (V2)** (`PastelMochi` / `MascotControl`): Maskot kelinci mochi lavender dengan transformasi elastis (*squash & stretch*), 3 set mata ekspresi, rona pipi (*blush*), partikel mengambang (not musik, huruf Z tidur, bintang), dan efek blitz kamera.
- [x] **Arsitektur MascotControl Mandiri & Event Bus Terpusat**:
  - Logika animasi procedural keyframes, sistem antrean prioritas alert 4 level, pelacakan kursor global (*gaze physics* via Win32 `GetCursorPos`), dan kedipan organik (*double-blink engine*).
  - Terkoneksi ke `PetCompanionService.cs` untuk pendistribusian alert dan status pemutaran media secara reaktif tanpa polling.
- [x] **Kehadiran Penuh Lintas Widget**:
  - **Widget Jam**: Bersanding dengan baterai di bilah collapsed.
  - **Widget Media Player**: Bersanding dengan visualizer audio (`MusicVisualizer`), mengenakan headphone dan bergoyang mengikuti ketukan musik.
  - **Widget Notifikasi & Toast Banner**: Menggantikan emoji/ikon teks statis dengan maskot animasi dan 8 badge reaksi (screenshot blitz, copied clipboard, bluetooth radar, timer 360°, drink bob, adzan crescent, alarm jitter, notification bell).
  - **Expanded Header**: Menemani pengguna di samping tombol Pin.
  - **Konsistensi Ekspresi Collapsed ↔ Expanded**: Ekspresi musik (headphone, goyangan, not balok) tetap tampil di bilah notch kecil saat lagu berputar; tidak lagi dibekukan saat notch menutup.
  - **Auto-Sleep 5 Menit**: Ambang tidur idle diubah dari 45 detik menjadi 5 menit tanpa gerakan kursor (`PetCompanionService.IdleSleepAfterSeconds`).

---

### ✅ Fase 11: Standardisasi Lirik LRC, Kalibrasi Skala 100%, & Optimasi RAM Ekstrem (Selesai - v0.2.3)
- [x] **Standardisasi Lirik Sinkron Standar LRC**:
  - Menghapus aturan pemotongan buatan 3.5 detik (`gap >= 7.5s`) agar lirik lagu tampil utuh dan natural hingga timestamp berikutnya.
  - Mengganti hitung mundur titik tiga (`•••`) dengan simbol musik statis (`♫`) pada bait intro dan jeda instrumental.
  - Algoritma penilaian cerdas (`ScoreCandidate`) di `LyricsService.cs` dengan penalti lagu live/remix dan filter durasi.
  - Parser tag regex universal (`[mm:ss.xx]`, `[mm:ss:xx]`, dan multi-tag per baris).
  - Penyimpanan offset kalibrasi manual per-lagu secara persisten di `%AppData%\NotchWin\SongOffsets.json`.
  - Badge sumber & tipe lirik (indikator teks `lrclib.net · SYNCED` / `· PLAIN` / `· INSTRUMENTAL` di kanan-bawah panel lirik) agar pengguna tahu varian mana yang diambil dari database lrclib.
- [x] **Rekalibrasi Skala Fisik Baseline 100% Notch**:
  - Ukuran fisik notch 1.2x (120%) kini menjadi baseline standar 100% (`BaseScaleFactor = 1.2` di `ApplyNotchScale`).
  - Slider preferensi `NotchScale` diatur pada default `100%` (`1.0`) dengan rentang pengaturan fleksibel 75% s/d 140%.
- [x] **Optimasi Memori Ekstrem & Layanan Manajemen RAM**:
  - Mengonfigurasi Workstation GC tunggal dan pelepasan memori virtual di `NotchWin.csproj`.
  - Membatasi resolusi decode bitmap (`DecodePixelWidth = 200` pada album art, `160` pada clipboard, `96` pada pet avatar).
  - Menyusutkan cache RAM lirik menjadi 20 lagu.
  - `MemoryManagementService.cs` otomatis melakukan *post-startup flush* (T+4.5s) dan *idle collapsed notch trim* (T+8s), memangkas working set dari ~245 MB ke **~54 MB – 62 MB** (-75%).

---

### ✅ Fase 12: Fullscreen Slim Bar (Selesai - v0.5.0)
- [x] **Garis Notch saat Fullscreen**:
  - Saat aplikasi fullscreen (borderless game/video), notch tidak lagi hilang total; pill dianimasikan keluar dan digantikan garis kapsul `220×6 px` di tengah atas layar.
  - Garis mengikuti material tema dan `NotchScale`, serta 100% click-through (`HTTRANSPARENT`) agar tidak mengganggu input.
  - Opsi **Show Slim Line in Fullscreen** di Pengaturan (default aktif); mematikannya mengembalikan perilaku sembunyi total.
  - `FullscreenDetector` melakukan pengecekan langsung saat start untuk menghindari kedipan notch.
- [x] **Hover-Peek 3 Detik (v0.5.0)**:
  - Hover kontinu di atas garis selama 3 detik membuka notch dengan animasi morph garis → pill.
  - Setelah pill muncul, panel penuh hanya terbuka lewat klik pill (hover saja tidak membuka panel saat fullscreen); kursor keluar membuat notch bermorph balik menjadi garis tipis.
  - Deteksi hover memakai polling `GetCursorPos` karena garis tetap click-through.
  - Interaksi dengan notch diabaikan oleh `FullscreenDetector` agar tidak memicu exit fullscreen palsu.

---

---

### ✅ Fase 13: Redesain Pusat Kontrol, Tipografi HD ClearType, & Keandalan Shelf (Selesai - v0.5.1)
- [x] **Redesain Menyeluruh Pusat Kontrol (Modern Dark Slate Glass & Capsule Sliders - Keputusan 70 & 71)**:
  - Kartu kontainer *Dark Slate Glass* (`#161622`, border `#28283C`, corner radius 11 DIPs).
  - Slider volume dan kecerahan bergaya kapsul (`ControlCenterSliderStyle`, track 6 DIPs, thumb 10 DIPs) dengan feedback hover dan aksen kuning hangat pada kecerahan.
  - Penjajaran presisi sumbu vertikal (*optical & geometric alignment*) untuk seluruh ikon kolom 0 (Volume, Kecerahan, Wi-Fi, Ethernet, Bluetooth) di x = 8.0 DIP.
  - Lencana kecepatan jaringan mikro (unduh cyan `#38BDF8`, unggah ungu `#A78BFA`) rata kiri dengan teks koneksi.
  - Quick action tiles 1.1 px stroke outline vektor modern (Screenshot, Lock, TaskMgr, Settings).
- [x] **Ikon Baterai HD Vektor Presisi & Guard Dropdown Output Audio (Keputusan 72)**:
  - Wadah baterai 22×11 DIPs bulat presisi, anoda solid 2×5 DIPs, formula pengisian tepat 14.0 DIPs, dan petir tajam poligon 7-titik tanpa `DropShadowEffect`.
  - Guard anti-auto-collapse pada `DevicePickerPopup`: penanganan `Opened`/`Closed`, penjagaan `PillBorder_MouseLeave` dan `WM_NCHITTEST`, pembatalan pending collapse, dan penegakan topmost HWND popup.
- [x] **Overhaul Tipografi HD & Subpixel ClearType (Keputusan 73 & 74)**:
  - Aktivasi paksa subpixel ClearType (`RenderOptions.ClearTypeHint="Enabled"`, `TextOptions.TextRenderingMode="ClearType"`, `TextOptions.TextFormattingMode="Display"`) pada seluruh window, kontainer, dan gaya implisit `App.xaml`.
  - Migrasi menyeluruh dari `Segoe UI Variable Display` (hairline) ke `Segoe UI Variable Text, Segoe UI, sans-serif` untuk ketajaman teks UI di semua monitor.
  - Penjajaran half-point grid pada `FontScaleService` (kelipatan 0.5pt) dan penguncian piksel fisik (`SnapsToDevicePixels="True"`).
  - Eliminasi efek `DropShadowEffect` pada angka suhu cuaca (`25°C`) untuk rendering kurva vektor DirectWrite murni yang bebas dari gerigi raster bitmap.
- [x] **Eliminasi Mojibake UTF-8 & Lokalisasi Menyeluruh Notifikasi/Toast (Keputusan 75)**:
  - Vektorisasi seluruh glif teks interaktif (`✕`, `▾`, `✓`) menjadi elemen `Path` native WPF yang 100% bebas dari ketergantungan encoding font.
  - Lokalisasi bilingual dinamis (`LocalizationService`) mencakup seluruh toast Catatan (alarm, reminder, save, delete), Papan Klip, dan nama-nama tab navbar (`Catatan`, `Papan Klip`).
- [x] **Pencegahan Duplikasi Item Shelf (Keputusan 76)**:
  - Normalisasi path kanonikal (`ShelfService.NormalizePath` & `IsPathEqual`) memangkas kutip, spasi, dan trailing separators.
  - Deduplikasi batch input via `HashSet<string>`.
  - Debounce waktu 600ms dengan kamus `_recentlyAdded` dilindungi thread lock `_lock`.
  - Pembersihan proaktif item lama dengan path serupa dari `Items` sebelum penyisipan.
  - Guard penanganan drop ganda `if (e.Handled) return;` dan `e.Handled = true;` pada level jendela (`Window_PreviewDrop`) dan seluruh handler elemen anak.

---

### ✅ Fase 14: Rombak Antarmuka Home, Miniplayer, Kalender Mingguan, & Dock Konsolidasian (Selesai - v1.1.0)
- [x] **Halaman Dashboard Home Terpadu (`NavTab0` / `SlideHomeCard`)**:
  - Menghadirkan tab Home di urutan pertama navbar (10 tab total) sebagai ringkasan harian saat notch dibuka.
- [x] **Miniplayer Presisi & Kalender Mingguan Berdampingan**:
  - Miniplayer card (lebar 338 DIPs, tinggi 120 DIPs) dengan artwork 86×86 DIPs (`OpacityMask`), fallback pemandangan sunset bertingkat, judul bold 13.5 DIPs, artis/status hijau emerald `#34D399`, seekbar ramping 14px dengan label waktu, dan kontrol playback (Prev 28×28, Circular White Play/Pause 34×34 dengan margin 22px, Next 28×28) dengan sentuhan hover lembut.
  - Kalender mingguan card (lebar 236 DIPs, tinggi 120 DIPs) dengan strip 7 hari (M S S R K J S), penanda tanggal hari ini (`IsToday`), dan ringkasan agenda.
- [x] **App Shortcut Dock Terkonsolidasi & Anti-Clipping**:
  - Dock pintasan aplikasi (`ShortcutDockBorder`, tinggi 44 DIPs, CornerRadius 16px) dipusatkan secara eksklusif di baris 1 Menu Home.
  - Dihilangkan dari menu-menu slide lainnya untuk menjaga kebersihan visual dan fokus tiap tab.
  - Tinggi notch disempurnakan menjadi `ExpandedHeight = 264.0 DIPs`, memberikan ruang bernapas 20 DIPs sehingga dock tidak pernah terpotong oleh lengkungan bawah notch.
- [x] **Banner Notifikasi Cerdas Situasional**:
  - Banner notifikasi otomatis muncul di baris 2 Home hanya saat ada notifikasi aktif (adzan/sholat, alarm, timer, pengingat minum).
  - Notch mengembang secara organik (`ExpandedHeight + 60.0 DIPs` = 324.0 DIPs) saat notifikasi aktif dan kembali ke tinggi standar (264.0 DIPs) saat selesai.

---

### ✅ Fase 15: Phone Share, Persistensi Shelf, Glow Outline & Karaoke Compact (Selesai)

- [x] **Phone Share (Modul 14)**: Server HTTP LAN tertanam (`PhoneShareService`, TcpListener port acak + token per sesi) dengan panel QR di slide Shelf — unggah file HP → Shelf (folder `PhoneFolder`, default `Downloads\Kuri Shelf`), unduh file Shelf → HP, kirim teks/link → clipboard PC. Halaman web HP self-contained ter-embed dengan teks mengikuti bahasa EN/ID, dedup retry browser, sanitasi nama tercadang, batas 2 GB / 64 KB, watchdog idle 15 menit, toggle `IsPhoneShareEnabled` di Pengaturan.
- [x] **Upgrade Shelf**: persistensi `shelf.json` (`ShelfState`, karantina berkas korup, urutan `AddedAt`), status item hilang (`IsMissing` + `RefreshMissingStates`), opsi **hapus setelah drag-out** (`ShelfRemoveAfterDrag`), target overlay drop ke-3 **Konversi Gambar** (`ImageConvertService`: JPG↔PNG, flatten latar putih, output `-converted` + toast hasil).
- [x] **Garis Cahaya Outline Notch (Keputusan 78)**: `NotchWindow.OutlineFx.cs` — stroke berlapis tanpa `Effect` untuk sisa waktu timer/stopwatch dan alert keliling, dua toggle pengaturan, nol biaya CPU idle.
- [x] **Karaoke 1-Baris Compact di Pill Collapsed**: double-slot slide/fade + isian progresif `KaraokeClipConverter` (`ActiveLyricProgress`), pengaturan karaoke-fill / bold / ukuran teks lirik 10–14.
- [x] **Banner Adzan Persisten dengan Snooze 5 Menit**: `SoundNotificationService.StopPrayerCall()` + alur Dismiss/Snooze pada banner Home.
- [x] **Lokalisasi Menyeluruh Fitur Baru**: kunci `Phone.*`, `PhonePage.*`, `Convert.*`, `DropAction.ConvertTitle`, `Shelf.*`, `Settings.Notch*Glow*`, `Settings.Lyrics*` (EN/ID) dan halaman HP bilingual.
- [x] **Bluetooth Radio**: `VolumeService.ToggleBluetoothRadioAsync()` via WinRT `Radio.SetStateAsync` + notifikasi perubahan state radio (dok 01). Perintah `QuickToggleBluetoothCommand` tersedia di ViewModel; *hook* tombol UI masih terbuka untuk fase berikutnya.

---

### ✅ Fase 16: Integrasi Spicy Lyrics TTML, Syllable Sync, & Eliminasi Stutter Karaoke (Selesai - v1.2.0)

- [x] **Integrasi Penuh Spicy Lyrics Dual-Pipeline (`TtmlLyricsService.cs`)**:
  - Mesin pencarian lirik berjenjang: Official Developer API (`api.spicylyrics.org/v1`) via Bearer token opsional (`SpicyLyricsApiKey`), dengan fallback otomatis ke mirror publik terbuka (`api.betterlyrics.org/getLyrics`) dan LRCLIB.
  - Kandidat Spicy Lyrics diprioritaskan otomatis dengan skor tertinggi (+2500 API / +2000 mirror).
- [x] **Preservasi Spasi Parser TTML (`LoadOptions.PreserveWhitespace`)**:
  - Menjaga keutuhan node teks spasi antar elemen XML `<span>` sehingga kata tidak lagi menyatu (`Look at the stars`, bukan `Lookatthestars`).
- [x] **Indikator Provider di Popup Versi Lirik (`LyricsVersionChipBtn`)**:
  - Penambahan badge visual khusus pada setiap kandidat: `[Spicy Lyrics]` (warna rose/merah muda) vs `[LRCLIB]` (slate/abu-abu netral) dengan tag tipe lirik (`SYLLABLE`, `SYNCED`, `PLAIN`, `INSTRUMENTAL`).
  - Tampilan footer provider aktif: `Spicy Lyrics · SYLLABLE` atau `lrclib.net · SYNCED`.
  - Cache ditingkatkan ke versi `v4-{sha256}.json`.
- [x] **Eliminasi Total Stuttering / Patah-patah pada Animasi Karaoke**:
  - **Sinkronisasi Left-Offset Text Measurement**: `KaraokeClipConverter` mengukur lebar aktual teks (`FormattedText` dengan LRU cache) dan memperhitungkan `leftOffset = (ActualWidth - MeasuredWidth) / 2` saat teks berada di tengah. Menghapus total jeda kosong (*dead margin*) di awal dan lonjakan (*sprint*) mendadak di tengah.
  - **Anti-Jitter SMTC Drift (1.2 detik)**: Menghindari reset stopwatch tiba-tiba akibat polling Windows SMTC berkala, menjaga pemutaran lokal tetap halus dan kontinu.
  - **Timer Stabil 60 FPS (16ms)**: Timer rendering dipatok stabil pada `16.0 ms` (`DispatcherPriority.Render`) selama lagu aktif tanpa thrashing interval.
- [x] **Pengaturan & Lokalisasi**:
  - Toggle Syllable Sync (`IsSyllableSyncEnabled`) dan input API Key (`SpicyLyricsApiKey`) di Pengaturan Musik dengan lokalisasi lengkap EN/ID.

---

### 🔮 Fase 17: Rencana Pengembangan Masa Depan (Roadmap Berikutnya)

- [ ] **Integrasi Kalender & Google Calendar / Outlook**:
  - Kartu agenda harian untuk menampilkan meeting/jadwal terdekat.
- [ ] **Widget Pemantau Kinerja Hardware Mendalam (Hardware Monitor)**:
  - Grafik mini penggunaan CPU, GPU, RAM, dan kecepatan unduh/unggah jaringan secara real-time.
- [ ] **Dukungan Plugin / Ekstensi Modular**:
  - API terbuka bagi komunitas untuk membuat slide widget pihak ketiga sendiri.

---

## 🎯 Definition of Done (Status Saat Ini)

- [x] Build Release berhasil 100% tanpa error (`0 Error(s)`).

- [x] Penggunaan CPU idle stabil mendekati 0% dan konsumsi RAM efisien.

- [x] Tidak mencuri fokus aplikasi kerja atau game (`WS_EX_NOACTIVATE`).

- [x] Otomatis sembunyi saat game atau video berlayar penuh (*fullscreen detection*).

- [x] Tampilan notch tertutup memiliki pemisah garis rapi, suhu cuaca, jam, dan lirik beranimasi fluida.

- [x] Dock shortcut dan Shelf file bekerja mulus dengan drag-and-drop.

- [x] Installer setup v0.2.0 Inno Setup Universal dan paket portabel siap pakai.

- [x] Dokumentasi lengkap terbarui: [README.md](README.md), [CARA-MENJALANKAN.md](CARA-MENJALANKAN.md), dan [DECISIONS.md](DECISIONS.md).

