# NotchWin (Dynamic Notch untuk Windows)

Aplikasi Dynamic Notch / Dynamic Island modern untuk Windows 10 & 11 yang menampilkan pill hitam minimalis di tengah atas layar dan dapat mengembang (*expand*) saat disentuh kursor atau diklik.

---

## Fitur Fase 1 (MVP Selesai)

- [x] **Pill Minimalis Selalu di Atas (Always on Top)**: Menempel di tepi tengah atas monitor utama tanpa menutupi konten penting saat mengecil.
- [x] **Animasi Fluida & Organik**: Animasi perubahan ukuran (*width*, *height*, dan *corner radius*) menggunakan kurva *CubicEase* yang halus menyerupai Dynamic Island.
- [x] **Tidak Mencuri Fokus (Non-Activating Window)**: Dikonfigurasi dengan `WS_EX_NOACTIVATE` dan intersepsi `WM_MOUSEACTIVATE` sehingga saat diklik, Anda tidak akan kehilangan fokus mengetik di aplikasi lain atau game.
- [x] **Bersih dari Taskbar & Alt+Tab**: Menggunakan `WS_EX_TOOLWINDOW` sehingga tidak mengotori taskbar maupun menu Alt+Tab.
- [x] **Transparansi & Click-Through Cerdas**: Area di luar pill hitam 100% transparan dan tidak memblokir klik kursor ke aplikasi di baliknya (*click-through* via `WM_NCHITTEST`).
- [x] **Dukungan Multi-DPI**: Siap untuk skala layar 100%, 125%, 150%, dan monitor resolusi tinggi dengan deklarasi `PerMonitorV2`.
- [x] **Logging Terintegrasi**: Menggunakan Serilog untuk mencatat status aplikasi ke file log di `%LocalAppData%\NotchWin\logs`.

---

## Persyaratan Sistem

- Windows 10 (Build 19041 / 20H1 ke atas) atau Windows 11
- .NET 8.0 SDK / Runtime (Desktop)

---

## Cara Menjalankan

1. **Jalankan langsung**: Cukup dobel-klik file [`run.bat`](run.bat).
2. **Build ulang**: Dobel-klik file [`build.bat`](build.bat) atau jalankan `dotnet build -c Release`.

Untuk panduan pengujian lengkap, baca [`CARA-MENJALANKAN.md`](CARA-MENJALANKAN.md).

---

## Log Keputusan Arsitektur

Semua keputusan desain teknis (Win32 interop, DPI, animasi, dan click-through) dicatat di dokumen [`DECISIONS.md`](DECISIONS.md).

---

## Roadmap Pengembangan

- [x] **Fase 1: MVP pill dan animasi**
- [ ] **Fase 2: Widget media (Spotify, browser, YouTube via SMTC)**
- [ ] **Fase 3: Widget sistem (Baterai, Volume NAudio, Jam & Timer)**
- [ ] **Fase 4: System tray, pengaturan, autostart, & deteksi game fullscreen**
- [ ] **Fase 5: Polish performa, stabilitas, & installer Inno Setup**
