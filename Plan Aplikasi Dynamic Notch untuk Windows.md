# Plan Aplikasi Dynamic Notch untuk Windows

Oct 22, 2026 · @Rikuu01

## Brief untuk agent

Bangun aplikasi Windows bernama **NotchWin** (nama sementara) yang menampilkan pill hitam di tengah atas layar dan mengembang menjadi panel info ala Dynamic Island. Kerjakan per fase di bawah, dan berhenti untuk konfirmasi di akhir tiap fase.

**Produk**

- Pill kecil (collapsed) yang selalu di atas semua window, di tengah atas layar utama
- Mengembang (expanded) saat hover atau klik, menampilkan widget: media, baterai, volume, jam/timer
- Berjalan di system tray, ringan, tidak muncul di taskbar maupun Alt+Tab

**Batasan**

- Target OS: Windows 10 (build 19041 ke atas) dan Windows 11
- Idle harus ringan: CPU mendekati 0% dan RAM serendah mungkin
- Hindari global keyboard/mouse hook yang tidak perlu, supaya tidak bermasalah dengan anti-cheat game
- Semua posisi dan ukuran pill harus bisa dikustomisasi, karena Windows tidak punya notch fisik

## Tech stack dan prasyarat

Gunakan C# dengan WPF di .NET LTS terbaru: ini pilihan paling mudah untuk window transparan, selalu di atas, dan beranimasi halus.

| Komponen | Pilihan |
| --- | --- |
| Bahasa dan UI | C# + WPF, target `net<versi LTS>-windows10.0.19041.0` |
| Pola arsitektur | MVVM dengan CommunityToolkit.Mvvm |
| Info media | `Windows.Media.Control` (SMTC, `GlobalSystemMediaTransportControlsSessionManager`) |
| Volume dan audio | NAudio (Core Audio API) |
| Tray icon | `H.NotifyIcon.Wpf` atau `NotifyIcon` dari Windows Forms |
| Penyimpanan setting | `System.Text.Json`, file di `%AppData%` |
| Logging | Serilog ke file |
| Installer | Inno Setup |
| Version control | Git |

**Prasyarat environment**

- [ ] Visual Studio 2022 atau lebih baru (opsional), workload ".NET desktop development"
- [ ] .NET SDK LTS terbaru
- [ ] Git dan repository GitHub
- [ ] Mesin Windows 10 atau 11 untuk testing, idealnya dengan 2 monitor
- [ ] Figma (opsional) untuk mendesain ukuran dan bentuk pill di tiap state

## Panduan untuk vibecoding

Pemilik proyek tidak menulis kode dan tidak menguasai C#: kamu yang menulis, menjalankan, dan menguji semuanya, jadi tangani setup sendiri dan jelaskan dengan bahasa awam.

**Lingkungan kerja**

- Pastikan kamu berjalan di PC Windows pemilik proyek dan punya akses terminal. WPF hanya bisa di-build dan dijalankan di Windows; jika kamu tidak bisa menjalankan `dotnet build` dan membuka aplikasinya, katakan itu di awal dan berhenti.
- Cek `dotnet --version` dan `git --version`. Pasang .NET SDK LTS terbaru jika belum ada. Build cukup lewat `dotnet build`, jadi Visual Studio tidak wajib.
- Siapkan repository Git sejak awal dan buat `CARA-MENJALANKAN.md` berisi cara membuka aplikasi dalam satu atau dua langkah.

**Cara berkomunikasi**

- Pakai bahasa awam. Istilah teknis yang tidak bisa dihindari dijelaskan dalam satu kalimat.
- Sebelum menjalankan perintah yang mengubah komputer (memasang software, mengubah registry, menghapus file), sebutkan apa yang akan dilakukan dan alasannya.
- Jangan meminta pemilik mengedit kode atau file konfigurasi secara manual. Kerjakan sendiri.

**Setelah setiap fase**

1. Jalankan `dotnet build` dan perbaiki semua error sebelum melapor.
2. Jalankan aplikasinya sendiri untuk memastikan benar-benar terbuka.
3. Tulis **Cara mengujinya**: 3 sampai 5 langkah konkret yang bisa diikuti pemilik, misalnya "arahkan kursor ke pill di tengah atas, harusnya membesar".
4. Tulis **Yang seharusnya terlihat** dan **Yang belum jadi**.
5. Tunggu pemilik menjawab OK atau melaporkan masalah, baru lanjut ke fase berikutnya.

**Jika ada masalah**

- Pemilik akan mengirim pesan error, penjelasan tampilan, atau screenshot. Cari penyebabnya sendiri, perbaiki, dan jelaskan perbaikannya dalam satu atau dua kalimat.
- Jika dua kali perbaikan tidak berhasil, berhenti menebak: tambahkan logging, jalankan ulang, lalu baca lognya.
- Lakukan commit Git sebelum perubahan besar agar bisa kembali ke versi yang berfungsi.

## Struktur proyek dan arsitektur

Hanya `Services` dan `Interop` yang menyentuh API Windows; View hanya bicara ke ViewModel, dan ViewModel hanya ke Services.

&#91;embedded content: arsitektur berlapis · 4 lapisan\]

Panah menunjukkan arah panggilan; event dari Windows naik lewat Services ke ViewModel, dan widget baru cukup didaftarkan ke `WidgetHost`.

Struktur folder yang diharapkan:

```text
NotchWin/
  App.xaml, App.xaml.cs, app.manifest
  Views/
    NotchWindow.xaml
    SettingsWindow.xaml
    Widgets/            (MediaView, BatteryView, VolumeView, ClockView)
  ViewModels/
    NotchViewModel.cs   (state Collapsed/Expanded, widget aktif)
    MediaViewModel.cs, ...
  Widgets/
    INotchWidget.cs, WidgetHost.cs
  Services/
    MediaService.cs, BatteryService.cs, VolumeService.cs
    SettingsService.cs, AutostartService.cs, FullscreenDetector.cs
  Interop/
    NativeMethods.cs, WindowStyleHelper.cs
  Models/
    AppSettings.cs
DECISIONS.md
README.md
```

## Fase 1: MVP pill dan animasi

Fase 1 menghasilkan pill hitam transparan yang melayang di tengah atas layar dan bisa expand/collapse dengan animasi halus.

1. Buat solution WPF dan aktifkan per-monitor DPI awareness (`PerMonitorV2`) lewat `app.manifest`.
2. Buat `NotchWindow` dengan `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent`, `Topmost=True`, `ShowInTaskbar=False`, `ResizeMode=NoResize`.
3. Lewat Win32 (`SetWindowLongPtr`), set `WS_EX_TOOLWINDOW` agar tidak muncul di Alt+Tab dan `WS_EX_NOACTIVATE` agar tidak mencuri fokus.
4. Posisikan di tengah horizontal layar utama, menempel di tepi atas dengan jarak yang bisa diatur. Hitung posisi dengan memperhitungkan DPI.
5. Bentuk pill: `Border` hitam pekat dengan `CornerRadius` setengah tinggi. Ukuran collapsed dan expanded disimpan sebagai konstanta yang nanti pindah ke setting.
6. Buat state machine `Collapsed` dan `Expanded`. Pemicunya hover (dengan delay singkat sebelum collapse) dan klik.
7. Animasikan lebar, tinggi, dan corner radius dengan easing (misalnya `CubicEase` atau `ElasticEase` ringan). Konten isi fade-in setelah ukuran hampir selesai.

`AllowsTransparency` di WPF memakai software rendering. Jika animasi patah-patah, evaluasi alternatif (layered window manual atau DWM) dan catat keputusannya.

**Kriteria selesai Fase 1**

- [ ] Pill muncul di tengah atas pada skala layar 100%, 125%, dan 150%
- [ ] Tidak muncul di taskbar atau Alt+Tab, dan tidak mencuri fokus saat diklik
- [ ] Animasi mulus tanpa artefak di tepi transparan
- [ ] Aplikasi bisa ditutup (sementara lewat klik kanan atau Esc)

## Fase 2: widget media

Fase 2 menambahkan widget yang membaca dan mengontrol lagu atau video yang sedang diputar lewat SMTC.

1. Buat `IMediaService` dan implementasinya dengan `GlobalSystemMediaTransportControlsSessionManager`.
2. Ambil judul, artis, thumbnail, status play/pause, posisi, dan durasi.
3. Berlangganan event (`MediaPropertiesChanged`, `PlaybackInfoChanged`, `CurrentSessionChanged`) dan jangan memakai polling.
4. Tambahkan kontrol play/pause, next, dan previous.
5. Tampilan collapsed: thumbnail kecil dan indikator sederhana saat media diputar. Tampilan expanded: thumbnail, judul, artis, progress bar, dan tombol kontrol.
6. Tangani kasus tepi: tidak ada sesi media, ada beberapa sesi sekaligus (pilih yang aktif), dan thumbnail kosong.

**Kriteria selesai Fase 2**

- [ ] Info dan kontrol bekerja dengan Spotify dan dengan YouTube di browser
- [ ] Pill kembali ke tampilan normal saat tidak ada media yang diputar
- [ ] Tidak ada polling dan tidak ada kebocoran memori saat lagu berganti berkali-kali

## Fase 3: widget sistem

Fase 3 menambahkan baterai, volume, dan jam/timer, dengan pola widget yang bisa ditambah tanpa mengubah `NotchWindow`.

1. Definisikan interface `INotchWidget` (id, prioritas, view collapsed, view expanded) dan satu `WidgetHost` yang memilih widget yang tampil.
2. Baterai: persentase dan status charging. Tampilkan indikator singkat saat charger dicolok. Pada perangkat tanpa baterai, widget ini disembunyikan.
3. Volume: baca dan ubah volume master lewat NAudio. Saat volume berubah, pill expand sementara lalu collapse otomatis.
4. Jam, timer, dan stopwatch sederhana.
5. Aturan prioritas: bila beberapa widget aktif, tampilkan yang berprioritas tertinggi. Widget transien (volume, charger) menimpa sementara lalu kembali ke widget sebelumnya.

**Kriteria selesai Fase 3**

- [ ] Menambah widget baru cukup dengan membuat satu kelas dan mendaftarkannya
- [ ] Indikator volume dan charger muncul lalu hilang sendiri tanpa mengganggu widget media
- [ ] Widget baterai tidak muncul di PC tanpa baterai

## Fase 4: tray, settings, autostart

Fase 4 mengubah prototipe menjadi aplikasi harian yang bisa diatur, jalan otomatis, dan tidak mengganggu game atau video fullscreen.

1. Tray icon dengan menu Settings, Pause/Resume, dan Exit.
2. Jendela Settings: offset dari tepi atas, ukuran pill, monitor target, warna dan opacity, widget aktif, serta delay hover.
3. Simpan setting di `%AppData%\NotchWin\settings.json`, muat saat start, dan terapkan langsung tanpa restart.
4. Autostart lewat registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, dengan toggle di Settings.
5. Single instance memakai `Mutex`: menjalankan aplikasi kedua tidak membuka salinan baru.
6. Deteksi aplikasi fullscreen (game atau video) dengan `SHQueryUserNotificationState` atau perbandingan ukuran window foreground dengan monitor, lalu sembunyikan notch otomatis.
7. Multi-monitor: pilih monitor target dan tangani monitor yang dicabut atau dipasang lewat `SystemEvents.DisplaySettingsChanged`.

**Kriteria selesai Fase 4**

- [ ] Setting bertahan setelah aplikasi ditutup dan dibuka lagi
- [ ] Aplikasi menyala sendiri saat login jika toggle autostart aktif
- [ ] Notch sembunyi saat game atau video fullscreen dan muncul lagi setelahnya
- [ ] Mencabut monitor target tidak membuat notch hilang atau keluar layar

## Fase 5: polish, testing, installer

Fase 5 membuat aplikasi stabil, ringan, dan siap dibagikan lewat installer.

1. Performa: ukur CPU dan RAM saat idle, hentikan timer dan animasi saat notch tidak terlihat, dan pakai event, bukan polling.
2. Stabilitas: pasang global exception handler dan catat error ke file log di `%AppData%\NotchWin\logs`.
3. Uji pada kombinasi berikut: Windows 10 dan 11, skala DPI 100%, 125%, 150%, satu dan dua monitor, game fullscreen, serta sleep dan resume.
4. Installer dengan Inno Setup: shortcut, opsi autostart, dan uninstall yang bersih.
5. Rilis: publish self-contained untuk `win-x64`. Jika ada sertifikat code signing, tanda tangani installer. Jika tidak, tulis peringatan SmartScreen di README.
6. README: cara build, daftar fitur, screenshot, dan cara memakai.

**Kriteria selesai Fase 5**

- [ ] Seluruh matriks uji di atas lulus tanpa crash
- [ ] Installer terpasang dan terhapus bersih di mesin yang belum pernah dipakai development
- [ ] README selesai

## Aturan kerja untuk AI agent

Kerjakan satu fase sekali, dan jangan lanjut ke fase berikutnya sebelum fase berjalan dan dikonfirmasi.

- Di akhir tiap fase: jalankan build, pastikan aplikasi berjalan tanpa error, ringkas apa yang selesai dalam beberapa baris, lalu tunggu konfirmasi.
- Commit kecil dengan pesan jelas di akhir tiap tugas.
- Pisahkan UI (View), logika tampilan (ViewModel), dan akses sistem (Services). Semua kode Win32 interop berada di folder `Interop`.
- Jangan menambah library di luar tabel tech stack tanpa menyebut alasannya lebih dulu.
- Jangan memakai global keyboard atau mouse hook tanpa bertanya.
- Jika ada keputusan desain yang ambigu, pilih yang paling sederhana, catat di `DECISIONS.md`, lalu lanjut.
- Beri komentar pada bagian Win32 yang tidak intuitif, dan jangan berlebihan di bagian lain.

## Definition of done

Proyek dianggap selesai bila semua kriteria fase terpenuhi dan daftar berikut tercentang.

- [ ] Build Release berhasil tanpa warning serius
- [ ] CPU idle mendekati 0% dan RAM stabil setelah dijalankan selama satu jam, tanpa tanda kebocoran memori
- [ ] Tidak mengganggu game fullscreen dan tidak pernah mencuri fokus
- [ ] Berjalan benar di Windows 10 dan 11, di satu maupun dua monitor
- [ ] Installer berfungsi dan uninstall bersih
- [ ] `README.md` dan `DECISIONS.md` lengkap
