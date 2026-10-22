# Log Keputusan Arsitektur dan Desain (DECISIONS.md)

Dokumen ini mencatat keputusan teknis, arsitektur, dan desain yang diambil selama pengembangan NotchWin.

---

### [2026-10-22] Fase 1: MVP Pill dan Animasi

#### Keputusan 1: Target Framework `net8.0-windows10.0.19041.0`
- **Pilihan**: Menggunakan .NET 8 LTS dengan target Windows SDK 10.0.19041.0.
- **Alasan**: Diperlukan untuk mengakses API modern Windows Runtime (WinRT) seperti `Windows.Media.Control` (SMTC) di Fase 2 tanpa dependensi wrapper eksternal yang berat.

#### Keputusan 2: Penanganan Window Transparan dan Click-Through (WM_NCHITTEST)
- **Pilihan**: NotchWindow memiliki ukuran kanvas yang cukup untuk menampung status expanded (440x200), dengan `Background="{x:Null}"` dan pencegatan `WM_NCHITTEST`. Jika kursor berada di luar border pill hitam, `WM_NCHITTEST` mengembalikan `HTTRANSPARENT (-1)`.
- **Alasan**: Menghindari pemanggilan `SetWindowPos` / resize OS window pada setiap frame animasi (yang biasanya menyebabkan patah-patah pada transparent window di WPF). Animasi ukuran pill berjalan di dalam kanvas dengan akselerasi hardware penuh, sementara area transparan di luar pill tetap 100% click-through ke aplikasi di bawahnya (game, browser, desktop).

#### Keputusan 3: Penanganan Focus (WS_EX_NOACTIVATE & WM_MOUSEACTIVATE)
- **Pilihan**: Mengatur extended style `WS_EX_NOACTIVATE` dan merespons `WM_MOUSEACTIVATE` dengan `MA_NOACTIVATE`.
- **Alasan**: Mencegah notch mencuri fokus keyboard/kursor dari aplikasi yang sedang aktif (misalnya saat mengetik di editor atau bermain game), namun tetap menerima interaksi mouse (klik dan hover).

#### Keputusan 4: Animasi Easing dan Corner Radius
- **Pilihan**: Menggunakan `CubicEase` (EaseOut untuk expand, EaseIn/EaseOut untuk collapse) pada `Width`, `Height`, dan `CornerRadius`.
- **Alasan**: Memberikan sensasi gerak fluida dan organik yang mirip dengan Dynamic Island, dengan transisi konten fade-in yang tertunda agar konten tidak terpotong saat transisi ukuran berlangsung.
