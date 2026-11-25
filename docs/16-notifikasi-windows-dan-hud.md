# 🔔 Modul 16: Notifikasi Windows Dua Tingkat & Unified Hardware HUD

> Indeks seluruh dokumentasi: [README.md](README.md)

Modul ini mendokumentasikan dua inovasi utama pada Dynamic Notch:
1. **Two-Tiered Dropdown Island Notifications**: Sistem intersepsi notifikasi native Windows 10/11 tanpa UWP yang turun secara anggun di bawah notch tanpa menghentikan pemutaran musik atau lirik aktif.
2. **Unified Hardware HUD Indicators**: Indikator instan pada bilah notch tertutup untuk penyesuaian Volume Master, Kecerahan Layar, Caps Lock, dan Num Lock.

---

## 📂 File Terkait & Arsitektur

### 1. Sistem Notifikasi Windows Native
- **Layanan Hook SQLite Windows Native**:
  - [`IWindowsNotificationService.cs`](../NotchWin/Services/Notifications/IWindowsNotificationService.cs): Kontrak antarmuka notifikasi Windows.
  - [`WindowsNotificationService.cs`](../NotchWin/Services/Notifications/WindowsNotificationService.cs): Intersepsi berkas basis data SQLite notifikasi sistem Windows (`wpndatabase.db`) menggunakan `winsqlite3.dll`.
  - [`WinSqlite3Native.cs`](../NotchWin/Services/Notifications/WinSqlite3Native.cs): P/Invoke bindings Win32 native untuk `winsqlite3.dll` (bawaan Windows 10/11, tanpa ketergantungan library luar).
  - [`NotificationPayloadParser.cs`](../NotchWin/Services/Notifications/NotificationPayloadParser.cs): Parser XML Toast Windows untuk mengekstrak judul, pesan teks, timestamp, dan path gambar preview.
  - [`NotificationAppResolver.cs`](../NotchWin/Services/Notifications/NotificationAppResolver.cs): Resolusi nama aplikasi dan merek brand (WhatsApp, Telegram, Discord, Spotify, Chrome/Browser, dsb.) lengkap dengan warna aksen brand.
  - [`WindowsNotificationModel.cs`](../NotchWin/Services/Notifications/WindowsNotificationModel.cs): Model data notifikasi Windows.
- **ViewModel & Manajemen Antrean**:
  - [`NotificationViewModel.cs`](../NotchWin/ViewModels/NotificationViewModel.cs): Pengatur tampilan banner, ikon brand, gambar avatar, status adzan/minum/update, dan formatting timestamp relatif.
  - [`NotchViewModel.cs`](../NotchWin/ViewModels/NotchViewModel.cs): Logika antrean notifikasi (`_notificationQueue`), pemicu ekspansi dua tingkat (`IsNotificationDropdownActive`), dan timer durasi tampil.

### 2. Unified Hardware HUD
- **Model & Enumerasi**:
  - [`HudType.cs`](../NotchWin/Models/HudType.cs): Enum `Volume`, `Brightness`, `CapsLock`, `NumLock`.
- **ViewModel**:
  - [`HudViewModel.cs`](../NotchWin/ViewModels/HudViewModel.cs): State indikator HUD (tipe, judul, teks nilai, progress bar, status mute, status on/off).
- **Layanan & Detektor Hardware**:
  - [`VolumeService.cs`](../NotchWin/Services/VolumeService.cs): Callback COM `IAudioEndpointVolumeCallback` melalui NAudio.
  - [`BrightnessService.cs`](../NotchWin/Services/BrightnessService.cs): WMI event watcher `WmiMonitorBrightnessEvent` pada `root\wmi` + fallback polling.
  - [`KeyboardLockService.cs`](../NotchWin/Services/KeyboardLockService.cs): Win32 Low-Level Keyboard Hook (`WH_KEYBOARD_LL`) memantau `VK_CAPITAL` dan `VK_NUMLOCK`.

---

## 📬 1. Two-Tiered Dropdown Island Notifications

### Latar Belakang & Masalah
Pada implementasi Dynamic Island tradisional, ketika notifikasi toast sistem masuk saat pengguna sedang mendengarkan musik, pulau notch biasanya terpaksa mengorbankan tampilan media player: cover album mengecil dan lirik karaoke terhenti untuk memberi ruang pada teks notifikasi.

### Solusi Arsitektur Dua Tingkat (Two-Tiered Deck)
Kuri memisahkan tampilan notifikasi menjadi dua tingkat independen dalam satu jendela transparan:

```
┌────────────────────────────────────────────────────────┐
│  TIER 1 (Row 0 - Collapsed Core Island - 32 DIPs)     │
│  [🎵 Album Art]  "Look at the stars..."  [♪ 02:45]    │  <-- Musik & Lirik Berjalan Normal
└────────────────────────────────────────────────────────┘
┌────────────────────────────────────────────────────────┐
│  TIER 2 (Row 1 - Floating Dropdown Toast - 68 DIPs)    │
│  [🟢 WhatsApp]  John Doe                  [Baru saja]  │  <-- Dropdown Notifikasi
│  "Halo, proyek Dynamic Notch sudah siap dirilis ya?"   │
└────────────────────────────────────────────────────────┘
```

1. **Tier 1 (Row 0 - Pinned Island Core)**:
   - Tetap menampilkan widget aktif utama (Pemutar Musik & Lirik Karaoke, Jam, Cuaca, atau Maskot).
   - Lirik berjalan tidak pernah terhenti, terputus, atau terpotong oleh pesan masuk.
2. **Tier 2 (Row 1 - Floating Dropdown Card)**:
   - Jendela notch memanjang secara organik ke bawah (*height expansion* dari 32 DIPs menjadi ~100 DIPs) menggunakan animasi *QuarticEase*.
   - Menyajikan kartu squircle gelap modern (`#14141E`, border `#2A2A3C`, radius 14px):
     - Lencana logo merek aplikasi (ikon WhatsApp hijau, Telegram biru muda, Discord ungu, atau ikon aplikasi Windows).
     - Nama pengirim/aplikasi dalam teks tebal (*bold*).
     - Isi pesan notifikasi bersih tanpa tag XML mentah.
     - Timestamp relatif ("now" / "baru saja").
     - Avatar/gambar preview pengirim jika tersedia.
3. **Sistem Antrean Cerdas (*Queue Management*)**:
   - Jika beberapa notifikasi masuk sekaligus (misal obrolan grup WhatsApp aktif), notifikasi berikutnya disimpan di antrean memori.
   - Setiap pesan ditampilkan selama `NotificationDurationSeconds` (default 3 detik), lalu berganti ke pesan berikutnya dengan transisi fade & slide lembut tanpa kedipan window.

### Hook Native SQLite Tanpa UWP (`wpndatabase.db`)
Kuri membaca notifikasi Windows tanpa memerlukan registrasi aplikasi UWP/MSIX:
- Windows 10 dan 11 menyimpan notifikasi sistem di `%LOCALAPPDATA%\Microsoft\Windows\Notifications\wpndatabase.db`.
- `WindowsNotificationService` memantau berkas tersebut via `FileSystemWatcher`.
- Saat berkas diperbarui, service membuka database secara read-only menggunakan `winsqlite3.dll` bawaan sistem Windows.
- Query SQL membaca entri notifikasi baru dari tabel `Notification` dan memparsing payload XML toast.

---

## 🎚️ 2. Unified Hardware HUD Indicators

Ketika pengguna menekan tombol fisik keyboard (Volume Up/Down/Mute, Kecerahan Layar, Caps Lock, Num Lock), bilah notch dalam keadaan tertutup (*collapsed*) bertransisi menjadi indikator HUD modern terpadu:

```
┌────────────────────────────────────────────────────────┐
│  [🔊 Speaker]  Volume                   [████████░░] 78│  <-- Volume HUD
└────────────────────────────────────────────────────────┘
┌────────────────────────────────────────────────────────┐
│  [☀️ Sun]      Brightness               [██████████]100│  <-- Kecerahan HUD
└────────────────────────────────────────────────────────┘
┌────────────────────────────────────────────────────────┐
│  [🔤 CAPS]     Caps Lock                       [  ON  ]│  <-- Caps Lock HUD
└────────────────────────────────────────────────────────┘
```

### Karakteristik & Spesifikasi Visual
- **Dimensi**: Melebar dinamis ke lebar 290 DIPs (tinggi tetap 32 DIPs) dengan animasi pegas (*spring morphing*).
- **Sisi Kiri**:
  - Ikon vektor murni resolusi tinggi (Speaker horn, Sun rays, Caps arrow, NumLock grid).
  - Teks label judul (`#D4D4D8`, e.g. "Volume", "Brightness", "Caps Lock", "Num Lock").
- **Sisi Kanan**:
  - **Volume & Kecerahan**: Progress bar kapsul ramping (lebar 96 DIPs, tebal 4.5 DIPs, latar `#27272E`, isian putih `#FFFFFF`) + angka nilai desimal bulat tanpa simbol persen.
  - **Caps Lock & Num Lock**: Status pill badge yang tegas (`ON` / `OFF` atau `Aktif` / `Mati`).
- **Siklus Auto-Dismiss 1.6 Detik**:
  - Setelah 1.6 detik tanpa perubahan input tombol lebih lanjut, HUD otomatis memudar dan notch kembali secara mulus ke status sebelumnya (Lirik Musik, Jam, Cuaca, atau Maskot).

---

## ⚙️ Opsi Pengaturan Terkait

1. **Sinkronisasi Notifikasi Windows (`IsWindowsNotificationSyncEnabled`)**:
   - Dapat diaktifkan / dinonaktifkan di Pengaturan. Saat dinonaktifkan, Kuri tidak memantau `wpndatabase.db`.
2. **Durasi Notifikasi (`NotificationDurationSeconds`)**:
   - Mengatur durasi tampilnya banner notifikasi pulau (rentang 2 hingga 10 detik, default 3 detik).

---

## 🚫 Panduan Modifikasi Aman (Do's & Don'ts)
- **JANGAN** membuka `wpndatabase.db` dengan mode baca-tulis (*read-write*); selalu buka dengan mode `SQLITE_OPEN_READONLY` agar tidak mengunci database notifikasi internal Windows.
- **JANGAN** menjalankan hook keyboard `WH_KEYBOARD_LL` yang memblokir pesan; hook hanya boleh memeriksa parameter `wParam` dan segera meneruskannya via `CallNextHookEx`.
- **JANGAN** membuat HUD menggantikan mode *Expanded*; jika notch sedang dibuka oleh pengguna, HUD hardware tidak boleh memaksa notch menjadi collapsed.
