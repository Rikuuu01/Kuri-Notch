# ⏱️ Modul 08: Timer & Stopwatch

> Indeks seluruh dokumentasi: [README.md](README.md)

Modul ini menyediakan alat manajemen waktu produktivitas (Pomodoro, Timer Fokus, dan Stopwatch Presisi) yang terintegrasi langsung dengan pil apung (*sub-notch pill*) saat notch dalam keadaan mengecil.

---

## 📂 File Terkait
- **View XAML**:
  - [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml#L6671-L7328) (`<!-- SLIDE 7: TIMER & STOPWATCH -->`)
  - [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml#L478-L535) (Floating Sub-Notch Live Pills)
- **View Code-Behind**:
  - [NotchWindow.xaml.cs](../NotchWin/Views/NotchWindow.xaml.cs#L8000-L8150) (`AnimateTimerSubNotch` & click handler)
  - [NotchWindow.OutlineFx.cs](../NotchWin/Views/NotchWindow.OutlineFx.cs#L244-L330) (`SyncNotchOutlineFx` — garis cahaya outline notch)
- **ViewModel**: [TimerStopwatchViewModel.cs](../NotchWin/ViewModels/TimerStopwatchViewModel.cs)
- **Services**:
  - [SoundNotificationService.cs](../NotchWin/Services/SoundNotificationService.cs)

---

## ⏳ Komponen & Alur Kerja

### 1. Mode Timer Countdown
- **Arsitektur Waktu Anti-Stuck & Anti-Drift (Target Timestamp)**:
  - Menggunakan perhitungan target waktu akhir nyata berbasis `DateTime.UtcNow` (`_timerTargetEndTime`), bukan pengurangan delta diskrit `RemainingTimerSeconds--`.
  - Berapapun jeda antrean UI thread, sisa detik dihitung presisi `Math.Ceiling(timeLeft.TotalSeconds)` dengan toleransi maksimal 80 ms, sehingga pergantian detik stabil 1.000 detik sekali dan tidak pernah stuck/tertahan.
- **Pengaturan Waktu**: Tombol `-1m` dan `+1m` untuk menyetel durasi timer secara fleksibel.
- **Tombol Preset Cepat**:
  - `Cepat`: 1 menit.
  - `Istirahat`: 5 menit.
  - `Fokus`: 15 menit.
  - `Pomodoro`: 25 menit.
- **Indikator Progres**: `ProgressBar` horizontal halus yang menunjukkan sisa waktu proporsional terhadap total durasi.
- **Nada Alarm Selesai**: Ketika waktu timer habis, sistem memutar nada beep alarm akustik berulang via `SoundNotificationService.GenerateTimerAlarmWav()`.

### 2. Mode Stopwatch & Pencatatan Lap
- **Tampilan Waktu Presisi (DispatcherPriority.Render)**:
  - Menampilkan Menit, Detik, dan Milidetik (Centiseconds).
  - Loop tick berjalan pada prioritas render komposisi GPU (`DispatcherPriority.Render`) interval 40 ms (~25 fps) dengan deduplikasi event `PropertyChanged` pada menit/detik, menghasilkan perputaran centiseconds yang mulus tanpa lag.
- **Pencatatan Lap**: Tombol **Lap** (`AddLapCommand`) aktif saat stopwatch berjalan, merekam waktu putaran dan menyajikannya dalam daftar riwayat lap yang dapat di-scroll.
- **Tombol Reset**: Mengembalikan waktu ke 00:00.00 dan mengosongkan riwayat lap.

### 3. Tampilan Live Activity & Alternasi 3 Detik Pada Collapsed Notch
- Mengikuti arsitektur rotasi live activity dan integrasi tanpa gangguan lirik/musik:
  - **Mekanisme Alternasi 3 Detik (*In-Place Slot Alternation*)**:
    - Ketika Timer atau Stopwatch sedang berjalan (`IsAnyTimerRunning == true`) dan notch dalam keadaan **Collapsed**, sistem menjalankan `_timerAlternationTimer` dengan interval **3 detik**.
    - Notch secara berkala bergantian setiap 3 detik antara konten reguler dan live activity Timer/Stopwatch.
  - **Saat Media Player Aktif (Widget Media)**:
    - **Tengah (Judul Lagu & Lirik Karaoke)**: Tetap berjalan dan mengalir secara kontinu **tanpa interupsi sedikit pun**.
    - **Sisi Paling Kiri (Slot Album Art 20×20 px)**: Bergantian antara cover art album musik dan ikon vektor langsung tanpa lingkaran luar (*frameless vector icon*) untuk Timer (`#FF9F0A` hourglass) atau Stopwatch (`#FFD60A` stopwatch).
    - **Sisi Kanan (Slot Waktu)**: Bergantian antara jam reguler (`Clock.CurrentTime`) dan sisa waktu Timer (`TimerStopwatch.TimerDisplay`) atau menit-detik Stopwatch (`TimerStopwatch.StopwatchMinutesSeconds`).
  - **Saat Mode Jam Aktif (Widget Clock)**:
    - **Sisi Kiri**: Bergantian antara tanggal (`Clock.CurrentDate`) dan ikon vektor langsung Timer/Stopwatch (tanpa lingkaran luar) dengan label terjemahan.
    - **Sisi Kanan**: Bergantian antara jam reguler (`Clock.CurrentTime`) dan waktu Timer/Stopwatch.
  - **Animasi Transisi Pegas Bergaya Poni (*Spring Swap-In Animation*)**:
    - Mengadopsi kurva animasi Poni (`swapIn 0.45s cubic-bezier(0.32, 1.28, 0.54, 1)`):
      - **Elemen Keluar (*Outgoing Slot*)**: Bergeser ke atas sejauh `-6 px`, mengecil ke skala `0.85`, dan memudar keluar (`1.0 -> 0.0`) dalam 180 ms (`CubicEase(EaseIn)`).
      - **Elemen Masuk (*Incoming Slot*)**: Berawal dari posisi `+6 px` dan skala `0.85`, lalu melompat ke posisi semula `0 px` dan skala `1.0` dengan pantulan pegas halus (`BackEase(EaseOut, Amplitude = 0.35)`) selama 420 ms, dipadukan fade-in halus (`0.0 -> 1.0`, 240 ms, delay awal 40 ms).
      - Menghasilkan efek *spring pop* yang sangat lentur dan memuaskan secara visual tanpa getaran layout (*zero layout shift*).
  - **Rotasi Dual-Activity**:
    - Jika Timer dan Stopwatch berjalan bersamaan, fase timer berotasi secara adil di setiap siklus aktif: `Normal -> Timer -> Normal -> Stopwatch -> Normal -> Timer ...`.
  - **Klik-untuk-Buka Cerdas**:
    - Mengklik notch saat fase timer/stopwatch aktif langsung memicu `NavigateToTimerSlide()`, membuka slide Timer & Stopwatch secara terkunci dengan tab yang tepat (Timer atau Stopwatch).
    - Jika notch diklik di luar fase timer, notch membuka perilaku default.
  - **Pencegahan Redundansi Sub-Notch Pill**:
    - Pil apung bawah (`TimerSubNotchPill` & `StopwatchSubNotchPill`) otomatis disembunyikan saat notch utama sedang mengaktifkan rotasi alternasi collapsed timer.

### 4. Floating Sub-Notch Pill (Eksklusif Mode Expanded)
- **Hanya Muncul Saat Mode Expanded**:
  - Pil apung bawah (`TimerSubNotchPill` & `StopwatchSubNotchPill`) dikonfigurasi secara eksklusif hanya muncul ketika notch berada dalam kondisi terbuka/melebar (**Expanded**).
  - Ketika notch berada dalam keadaan **Collapsed**, sub-notch pill secara otomatis **disembunyikan** (`Visibility.Collapsed`), karena informasi live activity timer/stopwatch sudah berotasi langsung di badan notch utama melalui slot in-place alternasi 3 detik.
  - Saat notch di-Expand, sub-notch pill meluncur turun secara mulus dari balik notch (TranslateY `-24 -> 0`, Opacity `0 -> 1` dengan `BackEase`).
  - Saat notch di-Collapse, sub-notch pill meluncur naik dan memudar keluar (TranslateY `0 -> -24`, Opacity `1 -> 0` dalam 180 ms) sebelum diset menjadi `Visibility.Collapsed`.
  - Mengklik pil bawah ini tetap mengarahkan pengguna langsung ke slide Timer & Stopwatch.

### 5. Garis Cahaya Outline Notch (*Outline Glow*)
Saat notch **Collapsed**, outline-nya bisa dialiri garis cahaya tipis (dipetakan dari perilaku Poni 1.5.0, `fxTimerLine` dan `fxGlow`). Semuanya dihitung di file parsial [NotchWindow.OutlineFx.cs](../NotchWin/Views/NotchWindow.OutlineFx.cs) dan digambar pada `OutlineFxLayer` (enam `Path`: dua setengah outline × tiga lapisan ketebalan).

- **Timer berjalan** (`TimerRun`): garis menyala sebesar **sisa waktu**, memanjang dari tengah bawah ke kedua sudut, lalu naik ke sisi dan busur `ear`.
  - Warna mengikuti Poni: hijau `#30D158` (> 50 %), kuning `#FF9F2E` (> 25 %), merah `#FF453A` (≤ 25 %).
  - Timer pendek (total < 200 s, yaitu langkah per detik ≥ 0.005) di-*lerp* 80 ms lewat `DispatcherTimer` 40 ms supaya ujungnya bergerak mulus; timer panjang hanya menerima satu pembaruan per detik.
- **Selesai** (`Blink`): seluruh outline berkedip merah 4 × 450 ms (±1,85 s) dipicu event `TimerFinished`, lalu padam. Notifikasi "Timer Finished" yang biasanya muncul bersamaan **tidak** menyalakan glow alert selama jendela kedip ini, supaya kedip tidak pernah tertimpa.
- **Stopwatch berjalan** (`StopwatchRun`): garis kuning `#FFD60A` **memanjang** tiap menit, `f = (detik_total % 60) / 60`, lalu reset saat menit bergulir. Nilainya lewat properti VM baru `StopwatchOutlineFraction` yang naik satu kali per detik (bukan 20× per detik seperti `StopwatchCentiseconds`).
- **Alert** (`AlertGlow`): cahaya berjalan keliling untuk pengingat, sholat, download, charger, dsb. Satu putaran 1800 ms: 0–45 % garis memanjang dari tengah, 45–80 % ekornya habis ke tepi luar (`StrokeDashoffset`), 80–100 % memudar. Dibatasi **20 s per alert** (`AlertMaxMs`), dan charger diwarnai hijau dengan 2 putaran 1400 ms.
- **Prioritas**: `AlertGlow` > `Blink` > `TimerRun` > `StopwatchRun` — sama seperti `outlineSync()` Poni.
- **Gerbang**: layer disembunyikan dan semua animasi dihentikan saat notch Expanded/Hidden, slim bar, fullscreen, notch manual-off, `IsPillVisiblyShown()` false, atau salah satu setting di bawah mati.
- **Setting**: `IsNotchTimerGlowEnabled` (garis timer + stopwatch) dan `IsNotchAlertGlowEnabled` (cahaya alert) — dua toggle di kartu *Position & Display* pada Settings, tersimpan di `settings.json`.

---

## 🚫 Panduan Modifikasi Aman (Do's & Don'ts)
- **JANGAN** mematikan DispatcherTimer saat notch beralih status antara Collapsed dan Expanded; timer harus tetap berdetak konsisten di latar belakang.
- **JANGAN** mengubah orientasi vertikal sub-notch transform tanpa memastikan `ClipToBounds` pada container induk tidak memotong rendering pil apung.
- **JANGAN** memakai `DropShadowEffect`/`BlurEffect` pada `OutlineFxLayer`. Jendela notch memakai `AllowsTransparency="True"` (layered window), sehingga setiap `Effect` memaksa render-target penuh + `UpdateLayeredWindow` per frame untuk SELURUH jendela — bukan hanya pil. Cahaya outline digambar dengan 3 `Path` berlapis (1.8/3.6/7.0 px, opasitas 1.0/0.34/0.16) yang secara visual setara blur tanpa biaya tersebut.
- **JANGAN** mengubah `StrokeThickness` lapisan outline dari kode: `StrokeDashArray` dan `StrokeDashOffset` diekspresikan dalam kelipatan `StrokeThickness`, sehingga mengubah ketebalan otomatis menggeser panjang garis.
- **JANGAN** menambah polling 1 Hz untuk menyegarkan garis outline. Seluruh pembaruan dipicu event (`TimerProgress`, `StopwatchOutlineFraction`, `TimerFinished`, `TransientTriggered`, `CurrentState`, `UpdateNotchGeometry`).
- **INGAT** `OutlineFxLayer` harus tetap menjadi anak TERAKHIR `PillContainer` di XAML; kalau diletakkan sebelum `ContentHost`, konten notch menutupi garisnya dan hanya busur `ear` yang tersisa.
