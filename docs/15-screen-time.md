# ⏱️ Modul 15: Screen Time (Waktu Layar) & Pelacakan Aktivitas Aplikasi

> Indeks seluruh dokumentasi: [README.md](README.md)

Modul ini menyediakan sistem pemantau durasi waktu layar (*Screen Time*) dan pelacakan aktivitas aplikasi secara real-time detik-demi-detik. Terinspirasi dari arsitektur aplikasi Poni (v1.6.0), modul ini menyajikan statistik komprehensif penggunaan PC dengan konsumsi daya dan beban CPU ultra-rendah (~0% idle).

---

## 📂 File Terkait & Arsitektur
- **Model Data & Skema Penyimpanan**:
  - [`ScreenTimeModel.cs`](../NotchWin/Models/ScreenTimeModel.cs): Model `DayRecord`, `AppRecord`, `HourlyBarItem`, `TopAppDisplayItem`, serta pustaka kategorisasi cerdas `ScreenTimeCategories` (Browsing, Development, Entertainment, Productivity, System, Other).
  - Persistensi: `%APPDATA%\NotchWin\screentime.json`.
- **Layanan Pemantau Latar Belakang**:
  - [`IScreenTimeService.cs`](../NotchWin/Services/IScreenTimeService.cs): Kontrak antarmuka layanan.
  - [`ScreenTimeService.cs`](../NotchWin/Services/ScreenTimeService.cs): Timer pelacak frekuensi 1 detik (`System.Threading.Timer`), inspeksi Win32 `GetForegroundWindow`, resolusi nama proses & metadata ikon, serta deteksi diam (*idle input detection*) via `GetLastInputInfo`.
- **ViewModel**:
  - [`ScreenTimeViewModel.cs`](../NotchWin/ViewModels/ScreenTimeViewModel.cs): Penghitungan total waktu aktif hari ini, jumlah peralihan aplikasi (*app switches*), distribusi histogram 24-jam, dan daftar aplikasi teratas (*top most-used apps*).
- **Antarmuka Pengguna (UI XAML)**:
  - [`NotchWindow.xaml`](../NotchWin/Views/NotchWindow.xaml): Kontainer kartu `SlideScreenTimeCard` dengan tata letak Bento-Box ganda (Histogram 24 Jam di sisi kiri, Daftar Aplikasi Teratas di sisi kanan).
  - [`SettingsWindow.xaml`](../NotchWin/Views/SettingsWindow.xaml): Seksi pengaturan Waktu Layar di Kategori Modul (sakelar aktif/nonaktif, slider ambang batas idle, dan tombol hapus riwayat).
- **Navbar Integration**:
  - NavTab10 (`screentime`) di bilah navigasi bawah notch (`NavTabsPanel`).

---

## ⚙️ Komponen & Alur Kerja

### 1. Pelacakan Latar Belakang Berperforma Tinggi (`ScreenTimeService`)
- **Deteksi Jendela Depan (Foreground Hook)**:
  - Setiap 1 detik, timer memeriksa window yang sedang aktif menggunakan `GetForegroundWindow` dan `GetWindowThreadProcessId`.
  - Cache nama proses (`_processCache`) berbasis PID mencegah pemanggilan berulang fungsi kernel `OpenProcess` dan `QueryFullProcessImageNameW`.
- **Deteksi Kursor & Keyboard Diam (*Idle Detection*)**:
  - Menggunakan API Win32 `GetLastInputInfo`.
  - Jika selisih waktu antara input terakhir pengguna dan waktu saat ini melampaui batas ambang idle (`IdleThresholdSeconds`, default 120 detik), status sistem dianggap **IDLE**.
  - Saat sistem idle, detik waktu layar **tidak diakumulasikan** ke aplikasi mana pun, menjamin integritas statistik penggunaan nyata.
- **Kategorisasi Cerdas Otomatis (`ScreenTimeCategories`)**:
  - Mengenali executable dan judul window secara instan:
    - **Browsing** (`#64D2FF`): Edge, Chrome, Firefox, Brave, Opera, Zen, Arc, dsb. (dilengkapi inspeksi konteks judul untuk memetakan YouTube ke Entertainment atau GitHub/ChatGPT ke Development).
    - **Development** (`#30D158`): VS Code, Visual Studio, JetBrains, Terminal, PowerShell, Git, Antigravity.
    - **Entertainment** (`#FF9F0A`): Spotify, VLC, Netflix, Steam, Games, Media Player.
    - **Productivity** (`#BF5AF2`): Microsoft Office (Word, Excel, PowerPoint), Notion, Obsidian, Slack, Telegram, WhatsApp, Discord.
    - **System** (`#8E8E93`): Explorer, Task Manager, Settings, Snipping Tool, Kuri/NotchWin.
    - **Other** (`#636366`): Aplikasi lainnya.

### 2. Persistensi & Auto-Migrasi dari Poni
- Riwayat aktivitas disimpan secara privat di `%APPDATA%\NotchWin\screentime.json`.
- **Auto-Import Mulus**: Saat pertama kali dijalankan, jika `screentime.json` NotchWin belum ada, service otomatis mendeteksi keberadaan `%APPDATA%\Poni\screentime.json` dan menyalin riwayatnya secara transparan sehingga pengguna lama Poni tidak kehilangan riwayat waktu layar mereka.
- **Pemangkasan Otomatis (*Auto-Pruning*)**: Data hari yang berusia lebih dari 35 hari otomatis dibersihkan agar ukuran berkas JSON tetap kecil (< 500 KB) dan waktu muat tetap kilat.

---

## 📊 Desain Antarmuka Kartu Expanded (`SlideScreenTimeCard`)

Kartu `SlideScreenTimeCard` dirancang dengan layout Bento-Box modern selebar 584 DIPs yang dipasangkan dengan kartu **Posture Monitor (`SittingCardContainer`)** di baris bawah:

1. **Kotak Kiri (Waktu Total & Histogram 24-Jam)**:
   - **Header**: Ikon grafik bar vektor mini beraksen warna tema + label `SCREEN TIME / WAKTU LAYAR` serta badge jumlah perpindahan aplikasi (`AppSwitches`).
   - **Angka Total Besar**: Tipografi tebal 23pt (`TotalTimeTodayText`, e.g. `4h 12m` atau `04j 12m`) berdampingan dengan lencana `Idle excluded`.
   - **Histogram 24 Batang (`BarHeight`)**: 24 kolom batang vertikal dalam `UniformGrid` mewakili jam `00:00` hingga `23:00` dengan label `00`, `06`, `12`, `18`, `23` sejajar langsung di bawah kolom batangnya masing-masing. Tinggi batang diskalakan proporsional terhadap jam tersibuk hingga 42 DIPs (`BarHeight`) tanpa terjepit batas 32px palsu.
2. **Kotak Kanan (Aplikasi Paling Sering Digunakan)**:
   - Menampilkan top 4 aplikasi teratas hari ini dengan titik indikator kategori berwarna, nama aplikasi rata kiri (`HorizontalAlignment="Left"`), progress bar proporsional penuh selebar kolom (`PercentOfTotal` 0–100%), durasi, persentase, dan tooltip rincian kategori.
3. **Kartu Bawah (`SittingCardContainer` - Posture Monitor)**:
   - Menampilkan durasi duduk aktif, target interval duduk dinamis (`Water.StandardIntervalText`), progress bar postur, label hitung mundur (`Next break` + badge waktu), serta tombol **Stand / Berdiri** (`Water.ResetSittingCommand`) dan **Snooze 2m** (`Water.SnoozeCommand`). Klik area kartu membalik kartu ke tampilan statistik duduk harian (`SittingBackView`).
   - Pelacakan durasi duduk tetap berjalan aktif secara pasif untuk kartu Screen Time meskipun pengingat pop-up minum air (`IsWaterReminderEnabled`) sedang dimatikan.

---

## ⚙️ Opsi Pengaturan (Settings $\rightarrow$ Modul Navbar)

Pada jendela Pengaturan Kuri (Kategori 6: Modul Navbar):
1. **Aktifkan Waktu Layar (`IsScreenTimeEnabled`)**:
   Sakelar toggle untuk menyalakan atau menonaktifkan pelacakan waktu layar di background.
2. **Ambang Batas Waktu Diam (`ScreenTimeIdleSeconds`)**:
   Slider penyesuaian toleransi inaktivitas pengguna (rentang 30 detik hingga 300 detik, default 120 detik).
3. **Bersihkan Riwayat Waktu Layar (`ClearScreenTimeHistoryCommand`)**:
   Tombol satu-klik untuk mengosongkan berkas `screentime.json` jika pengguna ingin mengatur ulang seluruh statistik.

---

## 🚫 Panduan Modifikasi Aman (Do's & Don'ts)
- **JANGAN** menggunakan polling interval lebih cepat dari 1 detik; frekuensi 1000ms sudah optimal untuk pelacakan waktu layar tanpa membebani scheduler thread Windows.
- **JANGAN** membaca `FileVersionInfo` atau icon aplikasi secara berulang setiap detik; selalu gunakan struktur cache `_processCache` berbasis PID.
- **JANGAN** mengunci thread UI saat menyimpan file JSON; lakukan operasi disk I/O di latar belakang atau secara berkala (*debounced periodic save*).
- **JANGAN** menghapus pemeriksaan `GetLastInputInfo`; tanpa pengecekan idle, waktu tidur/ditinggal PC akan dianggap sebagai waktu aktif aplikasi dan mengacaukan statistik.
