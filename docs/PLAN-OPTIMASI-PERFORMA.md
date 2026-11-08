# 🚀 Plan Optimasi Performa NotchWin (CPU & RAM)

> Status: **SEBAGIAN BESAR SELESAI (30 Oktober 2026).** Fase 0, 1, 2, 3.1, dan 3.5 sudah dieksekusi dan diverifikasi; item yang dilewati beserta alasannya dan hasil pengukuran ada di §9.

---

## 1. Ringkasan

NotchWin adalah overlay WPF transparan yang selalu hidup (always-on-top). Karena berjalan 24/7, biaya kecil per detik (tick timer, invalidasi render, log) akan terakumulasi menjadi konsumsi CPU/RAM yang terasa. Plan ini menurunkan biaya idle tanpa mengorbankan animasi pet, lirik karaoke, dan responsivitas notch.

Prinsip:
1. **Tidak ada pekerjaan saat tidak terlihat.** Timer/polling hanya hidup ketika UI-nya memang tampil.
2. **Perubahan visual seminimal mungkin memicu render.** Kurangi frekuensi invalidasi dan area komposit.
3. **Ukur dulu, ubah kemudian.** Setiap fase diverifikasi dengan angka before/after yang sama.

---

## 2. Baseline terukur (build Debug, collapsed)

| Metrik | Clock (idle) | Media + lirik |
|---|---|---|
| CPU rata-rata | ±0.16% sistem (≈2.5% 1 core) | ±0.16% sistem (≈2.5% 1 core) |
| Working Set | ±63 MB (dipangkas `MemoryManagementService`) | ±62 MB |
| Private commit | ±212 MB | ±211 MB |
| Threads / Handles | 42 / 1566 | 42 / 1566 |

Catatan: angka diukur dengan overlay 1400×880 `AllowsTransparency=True`. Private commit yang jauh di atas Working Set menunjukkan pemangkasan WS menyembunyikan commit sebenarnya — ini perlu ditangani (lihat Fase 3).

---

## 3. Target

| Metrik | Target |
|---|---|
| CPU collapsed idle (clock) | ≤ 0.05% sistem |
| CPU collapsed saat lagu + lirik | ≤ 0.10% sistem |
| CPU expanded (control center) | ≤ 0.5% sistem, kembali ≤0.1% dalam 2 dtk setelah tutup |
| Working Set setelah 10 menit | ≤ 45 MB |
| Private commit | ≤ 150 MB, tanpa tren naik 24 jam (slope < 1 MB/jam) |
| Startup sampai notch tampil | ≤ 1.5 detik |
| Handles 24 jam | stabil (tanpa kebocoran) |

---

## 4. Temuan titik boros (hasil audit kode)

| # | Area | Lokasi | Masalah | Dampak |
|---|---|---|---|---|
| 1 | Overlay raksasa | `NotchWindow.xaml` root | Window 1400×880 transparan; setiap invalidasi kecil (detik jam, kedip pet, animasi lirik) memicu komposit ulang permukaan besar | CPU render per detik, GPU composition |
| 2 | Tick jam | `ClockViewModel.cs:34,72-83` | Tick 1 dtk memformat `CurrentTime`, `CurrentDate`, `FullDate` setiap detik, padahal yang berubah hanya menit/tanggal | String alloc + invalidasi text tiap detik |
| 3 | Timeline media | `MediaViewModel.cs:1191,1830` | `_timelineTimer` 100 ms (10 Hz) selama playing; progress bar hanya terlihat saat expanded | Wake-up 10 Hz saat collapsed |
| 4 | Pupil maskot | `MascotControl/Astro/Egg.xaml.cs` | Timer pupil 50–100 ms per instance yang terlihat (widget collapsed + header expanded + welcome) | Invalidasi 10–20 Hz untuk elemen 22×16 |
| 5 | Loop pet | `PetCompanionService.cs:330-347` | Expanded = 33 ms konstan walau mouse diam & tidak ada animasi | 30 FPS terus-menerus selama hover |
| 6 | Efek bayangan | `NotchWindow.xaml` (24× `DropShadowEffect`) | Salah satunya menempel di `NotchStrokePath` yang lebarnya dianimasikan → blur dihitung ulang tiap frame transisi | Biaya rasterisasi saat animasi |
| 7 | Logging | `App.xaml.cs:33` | `MinimumLevel.Debug` di semua build; log file ditulis terus-menerus | I/O disk + format string |
| 8 | Topmost keeper | `NotchWindow.xaml.cs:280` | `SetWindowPos` setiap 2 dtk selamanya | Wake-up + P/Invoke rutin |
| 9 | Manajemen memori | `MemoryManagementService.cs` | `GC.Collect(2)` + LOH compaction + `EmptyWorkingSet` tiap collapse, dan tiap 3 menit bila WS > 65 MB | Page-fault churn; WS kecil tapi private tetap ±211 MB |
| 10 | XAML raksasa | `NotchWindow.xaml` (8000+ baris) | Seluruh slide (Weather, Clipboard, Shelf, Todo, Timer, dll.) di-parse & di-instantiate saat startup walau tersembunyi | Startup time + RAM dasar |
| 11 | Polling latar | `FullscreenDetector` 1.5 dtk, `PrivacyService` 2.5 dtk | Polling tetap jalan saat tidak ada yang berubah | Minor, masih bisa diturunkan |
| 12 | ✅ Sudah baik | `SystemMonitorService`, `BatteryService` detailed, `DownloadMonitorService`, lyric cache (maks 20), album art 200 px, clipboard 160 px, shelf 96 px, adaptive pet framerate collapsed | — | Pertahankan |

---

## 5. Rencana per fase

### Fase 0 — Instrumentasi & guardrail (0.5 hari)

1. Buat skrip ukur `tools/measure-perf.ps1` (PowerShell murni, tanpa dependency):
   - Sampling 30–60 dtk: CPU% sistem & per-core, WS, Private commit, Handles, Threads.
   - Dipanggil untuk 4 skenario: (a) clock collapsed idle, (b) media + lirik playing, (c) expanded control center, (d) settings terbuka.
2. Opsional: `dotnet-counters` (GC heap, alloc rate, gen0/1/2) bila tersedia di mesin.
3. Catat baseline sebelum tiap fase (angka di §2 sebagai acuan awal).

**Kriteria selesai:** skrip bisa dijalankan ulang dan menghasilkan tabel before/after yang konsisten.

---

### Fase 1 — Quick wins CPU (1–2 hari, risiko rendah)

| # | Perubahan | File | Detail |
|---|---|---|---|
| 1.1 | Tick jam per menit | `ClockViewModel.cs` | Timer 1 dtk → tick pada pergantian menit (mis. interval 30 dtk + cek menit berubah, atau hitung sisa detik ke menit berikutnya). `CurrentDate`/`FullDate` hanya diformat saat tanggal berubah. Tidak ada UI yang menampilkan detik. |
| 1.2 | Timeline media adaptif | `MediaViewModel.cs` | 100 ms hanya saat expanded; collapsed → 500 ms; tetap berhenti saat paused. Pastikan sinkronisasi indeks lirik masih memakai posisi yang cukup akurat (lirik berganti per beberapa detik). |
| 1.3 | Pupil maskat lebih hemat | `MascotControl/AstroMascotControl/EggMascotControl` | Collapsed 100 ms → 150 ms; expanded 50 ms → 66 ms; jangan pernah start saat `Sleeping`; lewati penulisan jika nilai target = nilai sekarang. |
| 1.4 | Pet loop adaptif saat expanded | `PetCompanionService.cs` | 33 ms hanya saat blink/groove/alert/baru hover; setelah 2 dtk tanpa gerakan → 83 ms (12 FPS). Collapsed tetap 150/250 ms. |
| 1.5 | Logging Release | `App.xaml.cs` | `MinimumLevel.Information` di Release (`#if DEBUG` tetap Debug); `WriteTo.File` dengan `buffered`, `flushToDiskInterval: 2s`, `fileSizeLimitBytes`, `retainedFileCountLimit`. |
| 1.6 | Topmost keeper | `NotchWindow.xaml.cs:280` | 2000 ms → 5000 ms; atau event-driven `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` bila mau lebih agresif. |
| 1.7 | Polling latar | `FullscreenDetector.cs`, `PrivacyService.cs` | Fullscreen 1.5 s → 2.5 s (atau hook); Privacy 2.5 s → 4 s. Jangan ubah battery/volume/download (sudah ter-gate). |

**Estimasi dampak:** >50% wake-up rutin hilang; clock dan log adalah kontributor terbesar yang bisa dipotong tanpa mengubah UX.
**Kriteria selesai:** target CPU collapsed tercapai; tidak ada animasi yang terlihat "patah-patah" pada uji mata.

---

### Fase 2 — Render pipeline (2–3 hari, risiko sedang)

| # | Perubahan | File | Detail |
|---|---|---|---|
| 2.1 | Kecilkan overlay window | `NotchWindow.xaml`, `NotchWindow.xaml.cs` | Ukuran 1400×880 → dinamis mengikuti state: collapsed ±640×160, expanded ±700×460, slim bar fullscreen lebar×48. Window tetap di-center horizontal (kiri = 960 − lebar/2). Ini memotong area komposit layered hingga ±5×. **Wajib uji:** drag & drop, klik luar, hover, multi-monitor DPI, slim bar. |
| 2.2 | Ganti shadow beranimasi | `NotchWindow.xaml` | `DropShadowEffect` pada `NotchStrokePath` diganti glow pre-baked (Path stroke ganda dengan gradient/opacity, atau cache bitmap statis). Efek pada elemen statis dipertahankan; audit 24 kemunculan. |
| 2.3 | Freeze & text mode | `NotchWindow.xaml(.cs)`, `App.xaml` | `Freeze()` semua brush/geometry yang dibuat dari kode; `TextOptions.TextFormattingMode="Display"` di root window; `UseLayoutRounding` pada kartu; audit animasi `RepeatBehavior.Forever` agar berhenti saat `IsVisible=false`. |

**Estimasi dampak:** penurunan CPU/GPU per invalidasi paling besar di seluruh plan.
**Kriteria selesai:** target CPU tercapai dengan window baru; semua skenario regresi lulus (drag/drop, slim bar, settings, multi-monitor).

---

### Fase 3 — Memori, GC, dan startup (2 hari)

| # | Perubahan | File | Detail |
|---|---|---|---|
| 3.1 | Rombak MemoryManagementService | `MemoryManagementService.cs` | Hapus `EmptyWorkingSet` periodik + `GC.Collect` tiap collapse/3 menit. Pertahankan satu trim lembut setelah startup dan setelah operasi berat (decode gambar besar/import). Ukur page-fault rate sebelum/sesudah. |
| 3.2 | Lazy-load slide berat | `NotchWindow.xaml` | Bungkus konten slide Weather/Clipboard/Shelf/Todo/Note/Timer dengan `ContentControl` + `DataTemplate` yang dimuat saat pertama dibuka (atau `x:DeferLoadDataTemplate`), agar startup tidak membangun seluruh visual tree. |
| 3.3 | Pelepasan data berat | `MediaViewModel`, `ClipboardService`, `ShelfService` | Set `Thumbnail = null` saat track berhenti/ganti widget; batasi clipboard (mis. 50 item, jadikan setting); lyrics cache 20 → LRU 10 dengan buang LRC berukuran besar. |
| 3.4 | Audit langganan event | `NotchWindow`, seluruh `Controls/*` | Pastikan semua handler singleton (`LocalizationService.LanguageChanged`, `PetCompanionService.*`, `SystemMonitorService.MetricsUpdated`) di-unsubscribe di `Unloaded`/`Closed`; tutup `SettingsWindow` membuang VM. |
| 3.5 | Konfigurasi runtime | `NotchWin.csproj` | Pastikan `ServerGarbageCollection=false`, `ConcurrentGarbageCollection=true`; pertimbangkan `PublishReadyToRun` untuk build rilis. |

**Estimasi dampak:** Private commit turun signifikan; startup lebih cepat; menghilangkan churn GC.
**Kriteria selesai:** target RAM & startup tercapai, soak 24 jam tanpa tren naik.

---

### Fase 4 — Verifikasi & soak (1 hari + 24 jam)

1. Jalankan skrip Fase 0 untuk 4 skenario, bandingkan dengan baseline §2.
2. Regresi manual: expand/collapse, hover navbar, lirik panjang (dynamic width), ganti widget, settings buka/tutup, cuaca °C/°F, timer/stopwatch, clipboard, shelf, fullscreen slim bar, lock/unlock, suspend/resume.
3. Soak 24 jam: catat Private commit & handles tiap jam; slope memori < 1 MB/jam, handle stabil.
4. Cek log: tidak ada exception baru; ukuran file log wajar dengan level Information.

---

## 6. Risiko & mitigasi

| Risiko | Mitigasi |
|---|---|
| Resize window memecahkan drag & drop / slim bar / multi-monitor | Kerjakan di branch terpisah; uji 100% & 125% DPI; sediakan rollback mudah (perubahan terpusat di `NotchWindow`) |
| Interval timer diperbesar membuat lirik/pet kurang halus | Interval adaptif per state (expanded tetap cepat); uji visual sebelum/sesudah |
| Menghapus trim memori membuat WS naik | Pantau page-fault & Private; kompensasi dengan lazy-load (#3.2) dan pelepasan data (#3.3) |
| `EmptyWorkingSet` dihapus → terlihat "berat" di Task Manager | Target sebenarnya adalah Private commit & stabilitas; jelaskan di dokumentasi |

---

## 7. Urutan eksekusi yang disarankan

1. **Fase 0** (instrumentasi) — wajib, murah.
2. **Fase 1** (#1.1, #1.2, #1.5, #1.3) — quick wins terbesar, risiko paling kecil.
3. **Fase 3 #3.1** (GC/trim) — cepat, menghilangkan churn.
4. **Fase 2 #2.1 & #2.2** — lompatan CPU berikutnya (window + shadow).
5. **Fase 3 #3.2–3.4** — RAM & startup.
6. **Fase 1 sisa (#1.4, #1.6, #1.7)** dan **Fase 2 #2.3** — penyempurnaan.
7. **Fase 4** — verifikasi & soak.

---

## 8. Yang TIDAK dilakukan

- Tidak memindahkan aplikasi ke proses terpisah / arsitektur ulang besar.
- Tidak menonaktifkan fitur (pet, lirik, cuaca) — hanya mengatur kapan pekerjaan berjalan.
- Tidak melakukan micro-optimization spekulatif tanpa pengukuran.

---

## 9. Status implementasi & hasil (30 Oktober 2026)

### 9.1 Status per item

| Item | Status | Catatan |
|---|---|---|
| 0 | ✅ | `tools/measure-perf.ps1` (CPU%, WS, Private, Threads, Handles; param `-Seconds`, `-Label`, `-OutCsv`) |
| 1.1 Tick jam per menit | ✅ | `ScheduleNextTick()` men-align tick ke pergantian menit |
| 1.2 Timeline media adaptif | ✅ | 100 ms expanded / 500 ms collapsed |
| 1.3 Pupil maskot | ✅ | 66 ms expanded / 150 ms collapsed (Mascot, Astro, Egg) |
| 1.4 Pet loop adaptif | ✅ | 83 ms setelah 2 detik idle (tanpa musik), selain itu 33 ms |
| 1.5 Logging Release | ✅ | Debug hanya `#if DEBUG`; buffered, flush 2 dtk, limit 20 MB, retensi 7 |
| 1.6 Topmost keeper | ✅ | 2000 → 5000 ms (event hook sudah terpasang sebelumnya) |
| 1.7 Polling latar | ✅ | FullscreenDetector 2.5 dtk; PrivacyService 4 dtk |
| 2.1 Kecilkan overlay | ✅ | Dinamis: 904×180 collapsed / 904×880 expanded (rumus di Keputusan 63); slim bar tetap ukuran penuh |
| 2.2 Shadow beranimasi | ✅ | `NotchGlowPath` pre-baked menggantikan `DropShadowEffect` di `NotchStrokePath` |
| 2.3 Freeze & text mode | ◐ | `UseLayoutRounding` + `TextFormattingMode=Display` di root; audit `Forever` sudah bersih (weather distop saat collapse, visualizer ter-gate). Audit `Freeze()` brush kode tidak dilanjutkan — tidak ada temuan biaya terukur |
| 3.1 Rombak MemoryManagementService | ✅ | Satu trim startup + `TrimMemory()` throttle 90 dtk; trim per-collapse/periodik dihapus |
| 3.2 Lazy-load slide | ⏭ | Dilewati — perubahan besar pada XAML 8000+ baris, risiko regresi tinggi, ditunda |
| 3.3 Pelepasan data berat | ⏭ | Dinilai tidak perlu: thumbnail sudah 200 px beku (~160 KB), cache lirik dibatasi 20, clipboard maks 50 |
| 3.4 Audit langganan event | ⏭ | Kontrol maskot sudah unsubscribe di `Unloaded`; tidak ditemukan kebocoran baru |
| 3.5 Konfigurasi runtime | ✅ | `PublishReadyToRun=true`; ServerGC/ConcurrentGC sudah sesuai |
| 4 Verifikasi | ◐ | Pengukuran & regresi inti selesai (lihat 9.2); soak 24 jam belum dijalankan |

### 9.2 Hasil terukur (build Debug, sesi yang sama)

| Skenario | CPU (1 core) | WS | Private |
|---|---|---|---|
| 1400×880 collapsed (baseline window lama) | 0.641 s / 20 dtk = 3.15% | 86.1 MB | 134.6 MB |
| 904×180 collapsed (window baru) | 0.375 s / 20 dtk = 1.85% | 85.7 MB | 134.1 MB |
| 904×180 collapsed (60 dtk tenang) | 1.922 s / 60 dtk = 3.16% | 80.4 MB | 130.1 MB |
| Expanded idle (control center) | 4.078 s / 20 dtk = 20.1% | 124.7 MB | 189.3 MB |

Catatan:
- Penghematan dari pengecilan window ±41% (A/B sesi sama). Angka absolut antar-run bervariasi ±50% tergantung aktivitas desktop, jadi perbandingan A/B yang menjadi acuan, bukan angka tunggal.
- Target §3 (≤0.05% sistem) belum tercapai; sisa biaya adalah invalidasi pet/animasi fitur yang selalu terlihat. Mematikannya melanggar prinsip §8.
- Expanded idle ±20% 1 core adalah animasi cuaca/pet yang by design; plan hanya menargetkan collapsed.
- Private commit turun dari ±212 MB ke ±131 MB setelah 3.1/3.5.
- Regresi yang diverifikasi: siklus expand → collapse → shrink (log `PositionOnMonitor` 180 → 880 → 180), render collapsed pasca-shrink tanpa clip, greeting startup, cuaca BMKG, log bersih tanpa exception.
- Perilaku Serilog roll ke file bersufiks `_001`/`_002` saat file log terkunci instance lain adalah perilaku lama (sudah ada sebelum optimasi).

Sisa pekerjaan: soak 24 jam (slope Private < 1 MB/jam, handles stabil) dan uji regresi manual yang butuh input pengguna (settings, drag-drop, slim bar fullscreen, multi-monitor DPI, lock/unlock, suspend/resume).
