# 🐾 Modul 11: Interactive Notch Pet Companion & Mascots (V2)

> Indeks seluruh dokumentasi: [README.md](README.md)

Modul ini menyediakan sistem karakter animasi interaktif (**Pet Companion**) untuk Dynamic Notch yang 100% berbasis grafis vektor murni (*pure vector XAML*), ringan, tajam di semua resolusi DPI, dan reaktif terhadap seluruh event sistem Windows.

> Membuat karakter maskot baru? Ikuti kontrak visual di [Spesifikasi Ekspresi Karakter](13-spesifikasi-ekspresi-karakter.md).

---

## 📂 File Terkait & Arsitektur
- **UI Controls**:
  - [`MascotControl.xaml`](../NotchWin/Controls/MascotControl.xaml) & [`MascotControl.xaml.cs`](../NotchWin/Controls/MascotControl.xaml.cs):
    Kontrol independen maskot *Pastel Mochi (V2)* berukuran 28×20 px dengan deformasi badan elastis (*squash & stretch*), 3 set mata ekspresi, partikel melayang (not musik, huruf Z tidur, kilau bintang), blitz kilat kamera, serta **8 badge notifikasi dinamis**.
  - [`MochiPetControl.xaml`](../NotchWin/Controls/MochiPetControl.xaml) & [`MochiPetControl.xaml.cs`](../NotchWin/Controls/MochiPetControl.xaml.cs):
    Kontainer adaptif tingkat atas yang menaungi keempat gaya maskot (*PastelMochi*, *AstroMascot*, *EggMascot*, *NotchEyes*) dengan penanganan klik sentuh elastis (*CubicEase* bounce) dan API `PlayWelcome(...)` / `HideGreeting()` untuk sapaan startup.
  - [`AstroMascotControl.xaml`](../NotchWin/Controls/AstroMascotControl.xaml) & [`AstroMascotControl.xaml.cs`](../NotchWin/Controls/AstroMascotControl.xaml.cs):
    Kontrol independen maskot *Astro Mascot* berukuran 28×20 px: astronot berhelm putih dengan visor gelap bermata amber menyala, headphone groove reaktif, partikel not/Z/kilau, flash kamera, serta 8 badge notifikasi dinamis yang mengikuti event bus yang sama.
  - [`EggMascotControl.xaml`](../NotchWin/Controls/EggMascotControl.xaml) & [`EggMascotControl.xaml.cs`](../NotchWin/Controls/EggMascotControl.xaml.cs):
    Kontrol independen maskot *Egg Mascot* berukuran 28×20 px: telur putih minimalis dengan pipi merona, lengan lambai (`ArmWave`) untuk reaksi `Welcome`, 3 set mata, headphone, partikel not/Z/kilau, flash kamera, dan 8 badge notifikasi pada event bus yang sama.
- **Data Models**:
  - [`PetStyle.cs`](../NotchWin/Models/PetStyle.cs):
    Enum 4 gaya maskot murni vektor: `PastelMochi`, `AstroMascot`, `EggMascot`, dan `NotchEyes`.
  - [`MascotState.cs`](../NotchWin/Models/MascotState.cs):
    Enum status maskot: dasar (`Idle`, `Music`, `Sleep`) dan alert reaktif (`Happy`, `Notification`, `Alarm`, `Drink`, `Timer`, `Adzan`, `Bluetooth`, `Copied`, `Screenshot`, `Welcome`).
  - [`AppSettings.cs`](../NotchWin/Models/AppSettings.cs):
    Penyimpanan konfigurasi `IsPetEnabled` dan `PetStyle`.
- **Service & Event Bus**:
  - [`PetCompanionService.cs`](../NotchWin/Services/PetCompanionService.cs):
    Event bus terpusat untuk pelacakan kursor global (*gaze physics*), deteksi inaktivitas tidur, sinkronisasi pemutaran musik, dan pendistribusian alert notifikasi ke maskot via `TriggerAlert(...)`.
- **Window View Integration ([`NotchWindow.xaml`](../NotchWin/Views/NotchWindow.xaml))**:
  - **Widget Jam (Clock)**: Samping kanan indikator baterai di bilah collapsed.
  - **Widget Media Player**: Samping kanan visualizer audio (`MusicVisualizer`) saat musik sedang berputar.
  - **Widget Notifikasi (Notification)**: Menggantikan ikon/emoji statis di sebelah kiri teks notifikasi dengan maskot animasi dan badge reaksinya.
  - **Banner Notifikasi Melayang (Floating Toast)**: Kolom kiri banner notifikasi toast.
  - **Bilah Header Terbuka (Expanded Notch)**: Samping kiri tombol Pin.
  - **Sapaan Startup (`WelcomeOverlay`)**: Lapisan sementara di dalam `PillContainer` yang menampilkan pet kedua berskala 1.3× di tengah notch saat aplikasi dijalankan, lalu meluncur ke posisi pet collapsed sambil konten notch fade-in.

---

## 🎭 Tiga Karakter Maskot Vektor Pilihan

Pengguna dapat memilih maskot aktif melalui menu **Pengaturan $\rightarrow$ Pet Companion**:

### 1. Pastel Mochi V2 (`PetStyle.PastelMochi` / `MascotControl`)
* Karakter mochi kelinci pastel lavender lembut dengan telinga dalam merah muda.
* **Hierarki Transformasi Tubuh (`BodyScale`, `BodyTilt`, `BodyShift`)** dengan pivot di bawah (`0.5, 0.9`) untuk pantulan kenyal alami.
* **Anatomi Mata Multi-Ekspresi**:
  * `EyesOpen`: Pupil berkliping halus melirik kursor mouse secara dinamis (*gaze tracking*).
  * `EyesHappy`: Lengkungan senyum gembira saat dielus atau teks disalin.
  * `EyesSleep`: Lengkungan mata terpejam saat tidur lelap.
* **Partikel Melayang**:
  * Not musik tunggal (♪) dan ganda (♫) melayang ke atas saat lagu diputar.
  * Huruf `z`, `Z`, `Z` berurutan saat maskot tidur lelap.
  * Kilauan bintang 4 sisi (`Spark1`, `Spark2`) saat dielus.
* **Flash Kamera (`FlashOverlay`)**: Lapisan putih tembus pandang yang menyala sekejap menyimulasikan blitz kamera saat tangkapan layar diambil.

### 2. Astro Mascot (`PetStyle.AstroMascot` / `AstroMascotControl`)
* Karakter astronot berjas antariksa putih glossy (`SuitBrush` #FFFFFF → #E3E7EF) dengan helm bulat dan visor gelap navy (`VisorBrush`).
* **Mata Visor Menyala**: Dua oval mata amber hangat (`EyeCoreBrush` #FFFBE0 → #FFDE5C) dengan halo cahaya lembut di dalam visor.
* **Headphone Astronot**: Band dan earcup `HeadphoneBrush` yang muncul serta bergoyang mengikuti ritme saat musik diputar (*music groove*).
* **Lampu Dada Oranye (`AccentBrush` #FF8A5C)**: Aksen khas suit antariksa dengan efek glow.
* Mendukung seluruh siklus hidup maskot: *gaze tracking*, kedip mata, pose tidur `Z`/`z`, reaksi alert, flash kamera, dan 8 badge notifikasi lewat event bus yang sama.

### 3. Egg Mascot (`PetStyle.EggMascot` / `EggMascotControl`)
* Karakter telur putih minimalis dengan bayangan lembut, bintik highlight, dan pipi merona merah muda.
* **Lengan Lambai (`ArmWave`)**: Lengan vektor berporos bahu (`ArmRotate`, `ArmScale`) yang tersembunyi di balik badan saat istirahat, lalu muncul dan melambai 4× pada reaksi `Welcome`.
* **Mata Ekspresif**: `EyesOpen` dengan pupil mengikuti kursor (pusat wajah `(14, 9.3)`), `EyesHappy`, dan `EyesSleep`, lengkap dengan blush, headphone, flash kamera, dan partikel yang sama.
* Mendukung seluruh 13 state, termasuk sapaan startup, lewat API `PlayWelcome(...)` yang dipanggil overlay sapaan.

---

### 4. Notch Eyes (`PetStyle.NotchEyes` / `NotchEyesControl`)
* Karakter maskot sepasang mata minimalis dinamis di dalam pil hitam notch dengan animasi transisi keyframe halus (`BeginAnimation`).
* **Mode Adaptif (`IsCompact`)**: Mendukung mode compact (skala 28×20 px di slot pet notch standar) dan native pill (236×45 px).
* **Animasi & Reaktivitas Lengkap**:
  - **Idle**: Mata berkedip, melirik ke kiri-kanan secara alami, dan bernapas perlahan.
  - **Music**: Mengangguk mengikuti ketukan tempo musik (BPM), mata berkedip bergantian, dan 4 bar equalizer audio bergerak acak.
  - **Sleep / WakeUp**: Mata mengantuk menyipit tipis dengan huruf 'z' & 'Z' melayang ke atas.
  - **Welcome**: Mata membesar riang, bentuk melengkung senang (^ ^), dan tangan vektor melambai 3 kali.
  - **Alarm & Timer**: Mata terkejut melotot, pil bergetar, dan ikon jam alarm bergoyang.
  - **Adzan / Salat**: Mata terpejam damai (‿ ‿) dengan aksen mint, glow lembut, dan ikon bulan sabit bernapas.
  - **Notification & Bluetooth**: Mata melompat kecil dan melirik ke ikon pesan.
  - **Drink**: Ikon tetes air bergoyang, mata mengikuti tetes air, dan terisi penuh saat dikonfirmasi.
  - **Clipboard & Screenshot**: Ikon clipboard dengan tanda centang, serta kilatan flash ring dan kamera saat screenshot diambil.

---

## 🔔 Sistem 8 Badge Notifikasi Dinamis (Corner Overlay)

Pada sudut kanan atas tubuh maskot (Pastel Mochi, Astro Mascot, maupun Egg Mascot), terdapat kanvas badge berukuran 9×9 px yang aktif seketika saat event sistem terkait terjadi:

| Badge | Icon Visual | Animasi Khas | Pemicu di Dynamic Notch |
| :--- | :--- | :--- | :--- |
| **Screenshot** | 📸 Kamera foto minimalis | Kilatan blitz putih + mata menyipit | Tangkapan layar diambil (via shortcut/quick action/clipboard) |
| **Copied** | 📋 Clipboard + centang neon | Anggukan kepala riang + mata senyum | Teks disalin ke clipboard Windows |
| **Bluetooth** | 📡 Simbol Bluetooth | Cincin radar mengembang (*BtRing*) + blush | Perangkat Bluetooth tersambung |
| **Timer** | ⏱️ Stopwatch mint | Jarum berputar 360° terus-menerus | Countdown timer atau stopwatch selesai |
| **Drink** | 💧 Tetesan air biru langit | Tetesan air mengayun vertikal (*DropShift*) | Pengingat hidrasi air minum berkala |
| **Adzan** | 🌙 Bulan sabit + bintang emas | Bintang berkedip + ekspresi tenang hormat | Waktu sholat/adzan tiba |
| **Alarm** | ⏰ Jam weker karang merah muda | Goncangan getar cepat + pupil jitter panik | Alarm berbunyi |
| **Notification**| 🔔 Lonceng emas | Lonceng berayun kiri-kanan (*BellRotate*) | Notifikasi sistem Windows, catatan, atau USB flashdisk |

---

## ⚡ Mesin Fisika & Logika Interaksi

1. **Pelacakan Kursor Global (*Gaze Physics*)**:
   * Membaca posisi kursor global via Win32 `GetCursorPos` dengan timer presisi 33ms (~30 FPS).
   * Pergeseran pupil dibatasi batas mata secara logaritmik dan diperhalus dengan *lerp factor 0.25*.
2. **Mesin Kedipan Organik (*Double-Blink Engine*)**:
   * Interval kedipan acak antara 2 hingga 5.5 detik.
   * Terdapat peluang 20% melakukan kedipan ganda cepat beruntun (*double-blink*) layaknya makhluk hidup.
3. **Sistem Antrean Prioritas Alert**:
   * Prioritas 3 (Tertinggi): `Alarm` dan `Adzan` (langsung menyela tampilan alert apa pun).
   * Prioritas 2: `Timer` dan `Drink`.
   * Prioritas 1: `Notification`, `Bluetooth`, `Copied`, `Screenshot`, dan `Welcome`.
   * Prioritas 0: `Happy` (reaksi sentuhan pengguna).
   * Alert berprioritas setara/lebih rendah otomatis masuk antrean hingga 8 slot dan ditampilkan berurutan.
4. **Siklus Tidur Otomatis (*Auto-Sleep*)**:
   * Jika tidak ada gerakan kursor selama > 5 menit, maskot otomatis tertidur lelap.
   * Gerakan kursor sekecil apa pun atau pemutaran musik langsung membangunkannya kembali.
5. **Sentuhan Interaktif (*Petting*)**:
   * Klik mouse pada maskot akan membangunkannya jika sedang tidur, atau memicu lompatan gembira (*Happy bounce*) lengkap dengan pipi merona dan bintang kelap-kelip.
6. **Sapaan Startup (*Welcome Greeting*)**:
   * Saat Kuri pertama kali tampil dan pet aktif, `WelcomeOverlay` memunculkan pet kedua di tengah notch dengan skala `1.3×`, memutar `PlayWelcome(3.0s)`, lalu meluncur `560ms` ke pusat pet collapsed yang sedang terlihat (`ClockCollapsedPet`, `MediaCollapsedPet`, atau `NotificationCollapsedPet`) sambil mengecil ke ukuran persis pet widget tujuan (`min(W/28, H/20)`); konten notch fade-in dan overlay memudar. Pet tujuan disembunyikan (`Opacity 0`) selama slide agar tidak muncul duluan, lalu crossfade `180ms` tepat saat overlay mendarat.
   * Egg Mascot melambai dengan lengan selama sapaan; Pastel Mochi dan Astro Mascot memakai fallback `ApplyHappy()` dengan sparkle.
   * Sapaan dibatalkan seketika jika notch di-expand, disembunyikan, atau masuk mode slim bar; bila widget aktif tidak punya pet (Volume/Battery), overlay memudar di tempat. Pet yang dinonaktifkan melewati sapaan dan konten langsung fade-in.
7. **Konsistensi Ekspresi Lintas Ukuran (Collapsed ↔ Expanded)**:
   * Ekspresi dasar maskot (Idle, Music, Sleep) harus tampil identik di bilah notch kecil maupun panel penuh; pengguna boleh mengenali status media tanpa membuka notch.
   * Saat musik berputar, animasi goyang badan, tilt, headphone, dan partikel not balok **tetap berjalan di notch collapsed** (berlaku sama untuk Pastel Mochi, Astro Mascot, dan Egg Mascot).
   * `OnServiceNotchExpandedChanged` hanya memanggil `ResetBody()/StopFx()` saat status dasar **bukan** musik (idle/tidur), agar storyboard tidak membekukan ekspresi musik ketika notch menutup. Idle dan Sleep tetap dibekukan di notch kecil demi hemat rasterisasi window transparan.
