# Kuri (Dynamic Notch untuk Windows)

Aplikasi Dynamic Notch / Dynamic Island modern, elegan, dan berkinerja tinggi untuk Windows 10 & 11. Menampilkan notch hitam minimalis yang menempel di bagian tengah atas layar dan mengembang secara organik (*expand*) menjadi panel informasi interaktif saat disentuh kursor atau diklik.

---

## 📚 Indeks Dokumentasi

Seluruh dokumentasi proyek berada di folder `docs/` ini. Dokumen modul fitur adalah acuan wajib sebelum membaca, mendiagnosis, atau mengedit kode terkait.

### Modul & Fitur (00–14)

| No | Modul / Fitur | File | Deskripsi Ringkas |
|:---:|:---|:---|:---|
| 00 | **Notch Core & Window Lifecycle** | [00-arsitektur-dan-notch-core.md](00-arsitektur-dan-notch-core.md) | Arsitektur WPF MVVM, animasi geometri notch, navbar pill, sub-notch, overlay privasi, startup entrance, dan fullscreen slim bar. |
| 01 | **Pusat Kontrol** | [01-control-center.md](01-control-center.md) | Telemetri CPU/RAM/GPU/jaringan, slider volume & kecerahan, pemilih audio output, quick action, dan baterai. |
| 02 | **Pemutar Musik & Lirik** | [02-music-player-dan-lirik.md](02-music-player-dan-lirik.md) | GSMTC, kontrol media, lirik karaoke lrclib, tampilan 1-baris compact berisian progres, pengaturan tebal/ukuran lirik, pemilih versi, offset per-lagu, dan warna aksen album. |
| 03 | **Prakiraan Cuaca** | [03-cuaca.md](03-cuaca.md) | Data BMKG & Open-Meteo, parameter cuaca, prakiraan per jam, dan pencarian kota. |
| 04 | **Jadwal Sholat** | [04-jadwal-sholat.md](04-jadwal-sholat.md) | Hisab Kemenag, 6 kartu waktu sholat, countdown, chime adzan dengan banner persisten + snooze 5 menit. |
| 05 | **Pengingat Minum & Postur** | [05-pengingat-minum-dan-postur.md](05-pengingat-minum-dan-postur.md) | Pelacak hidrasi, alarm persisten dengan snooze, dan pelacak durasi duduk. |
| 06 | **Catatan & Alarm** | [06-catatan-dan-alarm.md](06-catatan-dan-alarm.md) | Catatan multi-tab rich text, alarm jam-menit, dan autosave debounce. |
| 07 | **Riwayat Papan Klip** | [07-clipboard-history.md](07-clipboard-history.md) | Listener clipboard Win32, riwayat teks & screenshot, pin, dan notifikasi. |
| 08 | **Timer & Stopwatch** | [08-timer-dan-stopwatch.md](08-timer-dan-stopwatch.md) | Timer countdown preset, stopwatch lap, dan sub-notch live pill. |
| 09 | **Shelf & Dock** | [09-shelf-dan-dock.md](09-shelf-dan-dock.md) | Penampungan file drag-and-drop dengan persistensi `shelf.json`, overlay drop 3 target (Shelf/Dock/Konversi Gambar), hapus-setelah-drag, dock pintasan aplikasi terpadu di menu Home. |
| 10 | **Pengaturan & Tema** | [10-pengaturan-dan-tema.md](10-pengaturan-dan-tema.md) | Material latar, warna aksen, autostart, fullscreen, multi-monitor, dan lokalisasi. |
| 11 | **Pet Companion (Maskot)** | [11-mochi-notch-pet.md](11-mochi-notch-pet.md) | Maskot vektor Pastel Mochi, Astro & Egg, 8 badge reaksi, gaze physics, auto-sleep, dan sapaan startup. |
| 12 | **Download Monitor** | [12-download-monitor.md](12-download-monitor.md) | Deteksi unduhan via file parsial, persen dari DB Chromium, pil + strip + toast. |
| 13 | **Spesifikasi Ekspresi Karakter** | [13-spesifikasi-ekspresi-karakter.md](13-spesifikasi-ekspresi-karakter.md) | Kontrak visual 13 state, 3 set mata, 8 badge, micro-expression, dan checklist karakter baru. |
| 14 | **Phone Share (Berbagi ke HP)** | [14-phone-share.md](14-phone-share.md) | Server HTTP LAN bawaan dengan kode QR per sesi: unggah file/teks dari HP ke Shelf & clipboard PC, unduh file Shelf ke HP, folder tujuan & auto-stop idle 15 menit. |

### Panduan & Referensi

| Dokumen | Deskripsi |
|:---|:---|
| [CARA-MENJALANKAN.md](CARA-MENJALANKAN.md) | Panduan instalasi, menjalankan, dan checklist pengujian manual seluruh fitur. |
| [ROADMAP.md](ROADMAP.md) | Visi produk, tech stack, struktur proyek, riwayat fase, dan rencana berikutnya. |
| [DECISIONS.md](DECISIONS.md) | Log keputusan arsitektur (ADR) beserta alasan teknisnya. |
| [PLAN-FULLSCREEN-SLIM-BAR.md](PLAN-FULLSCREEN-SLIM-BAR.md) | Rancangan lengkap mode slim bar & hover-peek saat fullscreen. |
| [PLAN-PILL-IPHONE-STYLE.md](PLAN-PILL-IPHONE-STYLE.md) | Rancangan opsi gaya notch "Pill iPhone" (kapsul ala Dynamic Island) di Pengaturan. |
| [PLAN-OPTIMASI-PERFORMA.md](PLAN-OPTIMASI-PERFORMA.md) | Plan optimasi CPU/RAM beserta status implementasi dan hasil ukur. |
| [STYLE-SCROLLBAR.md](STYLE-SCROLLBAR.md) | Gaya scrollbar resmi (`ModernSlimScrollViewer`) yang wajib dipakai semua daftar scrollable. |

> Aturan kerja pengembangan (baca modul dulu, karantina kode, verifikasi hanya dengan `dotnet build`) ada di bagian akhir dokumen ini.

---

## 🌟 Fitur Utama Lengkap

### 1. Halaman Home Terpadu (Dashboard Utama Notch)
* **Miniplayer Berkelas**: Pemutar musik mini beresolusi tinggi dengan cover album 86×86 DIPs (sudut membulat 16px via `OpacityMask`), fallback grafis sunset bertingkat, seekbar interaktif ramping 14px, serta tombol Prev, circular white Play/Pause (34×34 DIPs dengan margin 22px), dan Next beranimasi sentuh lembut.
* **Kalender Mingguan Ringkas**: Strip visual 7 hari per-minggu (M S S R K J S) dengan tinggi seragam 120 DIPs yang menyala hijau emerald pada tanggal hari ini dan merah cerah pada hari Minggu, lengkap dengan ringkasan agenda harian.
* **Dock Pintasan Aplikasi Terkonsolidasi**: Kontainer dock aplikasi favorit terintegrasi eksklusif di halaman Home (bersih dari tab lain), mendukung drag-and-drop file/shortcut dan dialog penambahan aplikasi, dengan tinggi 44 DIPs dan margin proporsional anti-clipping.
* **Banner Notifikasi Cerdas Dinamis**: Banner notifikasi situasional yang melayang otomatis di bawah dock hanya saat ada adzan/waktu sholat tiba, alarm berdering, timer selesai, atau jadwal minum air, dengan ekspansi tinggi jendela dinamis (+60 DIPs).

### 2. Desain Notch Organik & Animasi Fluida
* **Menempel Alami di Bezel Layar**: Siluet kurva *fillet* (lengkungan keluar / *outward ears*) di kedua sudut atas yang menyatu sempurna dengan bezel fisik monitor.
* **Animasi Organik**: Animasi perubahan dimensi (*width*, *height*, *corner radius*) menggunakan kurva *CubicEase* yang sangat halus ala Apple Dynamic Island.
* **Tidak Mencuri Fokus (*Non-Activating Window*)**: Dikonfigurasi dengan `WS_EX_NOACTIVATE` dan intersepsi `WM_MOUSEACTIVATE`. Klik pada notch tidak akan pernah mengganggu ketikan Anda di Word, browser, maupun saat bermain game.
* **Click-Through Cerdas**: Area transparan di luar bentuk fisik notch 100% tembus klik (*click-through* via `WM_NCHITTEST`), sehingga Anda tetap bisa mengklik tab browser atau title bar di belakang notch.
* **Dukungan Skala Ukuran (Terkalibrasi 100%)**: Ukuran fisik notch diperbesar secara alami sebagai baseline 100% baru yang ideal, dan tetap dapat diatur fleksibel (75% hingga 140%) lewat Pengaturan.

### 3. Multi-Bahasa Dinamis (English / Bahasa Indonesia)
* **Default Bahasa Inggris**: Seluruh antarmuka secara bawaan menggunakan Bahasa Inggris.
* **Dukungan Bahasa Indonesia**: Dapat beralih ke Bahasa Indonesia kapan saja melalui menu Pengaturan.
* **Reaktif Tanpa Restart**: Mengubah bahasa akan memperbarui seluruh teks aplikasi secara seketika dan otomatis tersimpan.

### 4. Widget Media Cerdas & Karaoke Lirik Sinkron (Spicy Lyrics + LRCLIB)
* **Integrasi Native SMTC**: Membaca sesi media Windows (`Windows.Media.Control`) secara *event-driven* tanpa polling (Spotify, YouTube, Apple Music, browser).
* **Artwork & Kontrol Musik Penuh**: Menampilkan thumbnail album, judul lagu, artis, seekbar interaktif, dan tombol Prev, Play/Pause, Next.
* **Integrasi Penuh Spicy Lyrics & Syllable Sync (Presisi Suku Kata)**:
  * **Dual-Pipeline Spicy Lyrics**: Mengambil lirik TTML suku kata presisi per kata via Official Developer API (`api.spicylyrics.org/v1`) jika token pengembang diisi, dengan fallback otomatis ke mirror publik terbuka (`api.betterlyrics.org/getLyrics`) dan LRCLIB.
  * **Preservasi Spasi Antar-Kata**: XML parser menjaga integritas spasi antar-elemen TTML sehingga lirik terpisah rapi (`Look at the stars`) tanpa kata menyatu.
  * **Animasi 60 FPS Bebas Stutter / Anti Patah-patah**: `KaraokeClipConverter` presisi dengan kompensasi *left-offset* teks yang berpusat di tengah, timer 16ms stabil pada `DispatcherPriority.Render`, dan perlindungan *drift* SMTC (1.2 detik) agar tidak terjadi lonjakan atau hentakan mundur di tengah lagu.
* **Panel Karaoke Lirik Sinkron Standar LRC**:
  * **Tampilan Utuh Tanpa Terpotong**: Teks lirik ditampilkan penuh dan natural sepanjang lantunan bait penyanyi tanpa pemotongan prematur buatan.
  * **Pencarian Cerdas (`ScoreCandidate`)**: Memilih kandidat lirik tersinkronisasi terbaik dengan prioritas utama Spicy Lyrics (+2500/+2000) dan LRCLIB (+1000).
  * **Pemilih Versi Lirik Interaktif**: Dropdown popup dengan indikator sumber yang jelas (`[Spicy Lyrics]` berwarna rose/merah muda atau `[LRCLIB]` abu-abu) beserta tag tipe lirik (`SYLLABLE`, `SYNCED`, `PLAIN`, atau `INSTRUMENTAL`).
  * **Memori Offset Per-Lagu Persisten**: Kalibrasi offset manual otomatis tersimpan per-lagu (`SongOffsets.json`) dan dimuat seketika saat lagu diputar kembali.
  * **Badge Sumber & Tipe Lirik**: Pojok kanan-bawah panel lirik menunjukkan provider aktif: `Spicy Lyrics · SYLLABLE`, `lrclib.net · SYNCED`, dll.
  * **Ikon Musik Statis (`♫`)**: Jeda instrumen/intro tampil tenang dengan simbol musik tanpa animasi hitung mundur mengganggu.
  * **Karaoke 1-Baris Compact di Notch Tertutup**: Pill collapsed menampilkan satu baris lirik aktif dengan animasi slide + fade antar-baris dan **isian progresif ala Dynamic Island** — teks memudar dari abu-abu menjadi putih seirama nyanyian (`ActiveLyricProgress` + `KaraokeClipConverter`), identik dengan isian pada panel besar.
  * **Pengaturan Tampilan Lirik**: Toggle karaoke-fill, toggle Syllable Sync, input API key Spicy Lyrics, pilihan **bold** baris aktif, dan slider **ukuran teks 10–14** di Pengaturan → Musik.
  * Animasi geser kartu pemutar musik yang sangat halus saat panel lirik dibuka/ditutup.
* **Warna Aksen Dinamis Album**: Opsi menyelaraskan warna aksen notch dengan warna dominan cover album lagu yang sedang berputar.

### 5. Pusat Kontrol (Control Center - Desain Ulang Modern)
* **Dark Slate Glass Card Containers**: Tata letak grid kartu modern (`#161622`, border `#28283C`, sudut bulat 11px) dengan jam & kalender tipografi 21pt bold.
* **Capsule Sliders**: Slider volume master dan kecerahan layar bergaya kapsul dengan track 6 DIPs, thumb putih bulat interaktif, dan slider kecerahan beraksen kuning hangat (`#FBBF24`).
* **Pemilih Output Audio Anti-Collapse**: Chip pemilih speaker interaktif dengan dropdown popup yang dilindungi agar notch tidak tertutup sendiri saat memilih perangkat suara.
* **Ikon Baterai HD Vektor Presisi**: Bodi baterai 22×11 DIPs dengan terminal anoda, persentase tebal 12.5pt, pengisian daya poligon 7-titik tajam tanpa bayangan blur, serta telemetri kapasitas (mAh/Wh) dan watt daya real-time.
* **Penjajaran Optik Konektivitas**: Ikon Wi-Fi/Ethernet dan Bluetooth diselaraskan tepat di sumbu tengah (x = 8.0 DIP) dengan lencana kecepatan unduh/unggah mikro yang simetris.
* **Telemetri Hardware & Quick Actions**: Metrik CPU, RAM, dan GPU dengan progress bar kapsul berwarna (Cyan, Amber, Violet) yang terhubung ke Task Manager, serta 4 tombol pintasan aksi cepat berikon outline vektor 1.1 px stroke.

### 6. Informasi Cuaca & Prakiraan 7 Hari
* **Kondisi Real-Time**: Menampilkan suhu, kelembapan, kecepatan angin, indeks UV, dan curah hujan.
* **Prakiraan Cuaca**: Prediksi cuaca harian 7 hari ke depan.
* **Pencarian Kota Mandiri / Sinkron**: Pencarian kota manual di seluruh dunia atau pilihan cepat kota-kota besar.

### 7. Jadwal & Pengingat Sholat
* **Jadwal 5 Waktu Sholat**: Subuh, Terbit, Dzuhur, Ashar, Maghrib, dan Isya.
* **Countdown Interaktif**: Penghitung waktu mundur menuju waktu sholat berikutnya.
* **Notifikasi Audio & Banner**: Nada chime adzan disintesis lokal saat waktu sholat tiba dan tampil sebagai **banner Home persisten** (tidak auto-dismiss) dengan tombol **Hentikan** dan **Tunda 5 menit**; banner ulang otomatis setelah snooze selama modul aktif.

### 8. Pengingat Minum & Layar Aktif (Sedentary Flip Card)
* **Pengingat Minum**: Pelacak asupan air harian dalam satuan Liter (`Today: 0.5L of 2.0L`) dengan tombol tambah cepat +250ml.
* **Pengingat Duduk Cerdas**:
  * Sensor pintar otomatis menjeda timer saat komputer ditinggalkan (*standby*) dan melanjutkan saat mouse digerakkan.
  * **Interactive Flip Card**: Klik pada kartu untuk membalik kartu dan melihat statistik duduk harian (durasi aktif total, sesi terlama, dan frekuensi berdiri).

### 9. To-Do List (Daftar Tugas)
* **Modern Slim Scrollbar**: Scrollbar dark capsule 5px transparan yang minimalis dan elegan.
* Kolom input ketik tugas baru dengan tombol enter atau tombol `+`.
* Lingkaran checklist modern dengan animasi coret saat tugas diselesaikan.

### 10. Timer & Stopwatch
* **Tab Timer**: Preset cepat (1 Mnt, 5 Mnt, 10 Mnt, 25 Mnt Pomodoro) dengan visualisasi countdown melingkar.
* **Tab Stopwatch**: Penghitung waktu presisi milidetik dengan fitur pencatatan putaran (*Lap*) dan scrollbar modern.
* **Garis Cahaya Outline Notch (Poni-style)**: Di notch tertutup, garis tipis emerald mengelilingi outline menampilkan sisa waktu timer/pomodoro dan putaran menit stopwatch (dipakai juga sebagai *alert glow* berjalan keliling maks 20 detik saat notifikasi penting tampil). Bisa dimatikan terpisah lewat Pengaturan (`IsNotchTimerGlowEnabled` / `IsNotchAlertGlowEnabled`) — nol biaya CPU karena dirender dengan stroke berlapis tanpa `Effect`.

### 11. Indikator Privasi, Volume HUD & Notifikasi USB
* **Titik Privasi Kamera (🟢 Hijau)** dan **Mikrofon (🟠 Oranye)** menyala otomatis saat webcam atau mikrofon aktif, melayang di ujung kanan notch (saat notch dibuka: melayang di kanan-atas notch dengan inset tetap, tidak terikat posisi karakter) — meluncur turun dari atas layar saat muncul, fade-out saat nonaktif.
* **Volume HUD Popup**: Bar volume dinamis muncul seketika saat tombol volume ditekan.
* **Notifikasi USB Flashdisk**: Banner dinamis saat flashdisk dicolok atau dicabut.
* **Mode Fullscreen Slim Bar**: Saat bermain game atau menonton video fullscreen, notch berubah menjadi **garis tipis** di tengah atas layar yang tetap terlihat namun 100% *click-through* (tidak mengganggu input). **Hover garis selama 3 detik** untuk memunculkan pill notch dengan animasi *morph* halus (garis → pill). Saat fullscreen, panel penuh **hanya terbuka lewat klik pill** — hover saja tidak membuka panel. Notch bermorph balik menjadi garis tipis saat kursor keluar. Tersedia opsi untuk kembali sembunyi total lewat Pengaturan.

### 12. Maskot Interaktif & Pet Companion (Pure Vector Mascots)
* **100% Vektor Murni & Tajam**: Tidak menggunakan GIF bitmap buram, sepenuhnya dirender dengan grafis vektor XAML yang tajam di semua resolusi dan DPI scaling.
* **Tiga Pilihan Karakter Eksklusif**:
  * **Pastel Mochi (V2)**: Karakter kelinci pastel lavender lembut dengan animasi tubuh elastis (*squash & stretch*), 3 set mata ekspresif, pipi merona (*blush*), partikel mengambang (not musik, huruf Z tidur, bintang kelap-kelip), dan kilatan blitz kamera.
  * **Astro Mascot**: Astronot berhelm putih glossy dengan visor navy dan mata amber menyala, headphone groove reaktif, flash kamera, serta 8 badge reaksi yang sama.
  * **Egg Mascot**: Telur putih minimalis dengan pipi merona dan lengan vektor yang melambai saat sapaan startup, lengkap dengan seluruh reaksi badge dan ekspresi.
* **Kehadiran Dinamis Lintas Widget**:
  * **Di Widget Jam**: Berdiri manis di samping indikator baterai di bilah collapsed.
  * **Di Widget Musik**: Ikut bergoyang dengan headphone terpasang dan not balok melayang saat lagu diputar di Spotify/YouTube.
  * **Di Widget Notifikasi**: Menggantikan ikon/emoji statis lama dengan maskot animasi dan badge reaksinya.
  * **Di View Expanded**: Menemani pengguna di sudut kanan atas panel samping tombol Pin.
* **8 Badge Reaksi Sistem**:
  * 📸 **Screenshot**: Kilatan blitz putih pada tubuh maskot + badge kamera.
  * 📋 **Text Copied**: Anggukan kepala riang + badge clipboard centang hijau neon.
  * 🎧 **Bluetooth**: Cincin radar sonar mengembang + rona pipi kemerahan.
  * ⏱️ **Timer**: Jarum stopwatch berputar 360° tanpa henti.
  * 💧 **Drink Water**: Tetesan air biru langit mengayun vertikal.
  * 🌙 **Adzan**: Bulan sabit dan bintang emas berkedip dengan pose khidmat.
  * ⏰ **Alarm**: Goncangan getar cepat jam weker dan pupil mata panik menggemaskan.
  * 🔔 **Notifikasi**: Lonceng emas berayun kiri-kanan.
* **Fisika Gaze & Interaktivitas**: Pupil mata melirik kursor mouse secara dinamis (*gaze physics*), interval kedipan acak dengan peluang kedipan ganda (*double-blink*), klik mouse untuk mengelus (*happy bounce*), dan tidur otomatis setelah ±5 menit tidak ada gerakan kursor. Saat musik berputar, maskot mempertahankan ekspresi yang sama (headphone & goyangan) baik di notch tertutup maupun panel penuh.
* **Sapaan Startup**: Saat Kuri dijalankan, maskot tampil di tengah notch dan melambai (Egg) selama ±3 detik, lalu meluncur ke posisi pet collapsed sambil jam, cuaca, dan baterai fade-in. Sapaan otomatis dibatalkan bila notch dibuka, disembunyikan, atau masuk mode slim bar.

### 13. Efisiensi Memori Ekstrem (Konsumsi RAM Ringan ~54 MB)
* **Pengurangan RAM Hingga 75%**: Dari kondisi awal ~245 MB dipangkas menjadi hanya **~54 MB – 62 MB** saat memutar musik aktif.
* **Workstation GC & Non-Blocking Trimmer**: Runtime .NET 8 dikonfigurasi khusus desktop companion tanpa menahan memori virtual yang tidak terpakai.
* **Layanan Pemangkas Memori Otomatis (`MemoryManagementService`)**:
  * *Post-Startup Flush (T+4.5s)*: Otomatis membuang lonjakan memori startup awal WPF.
  * *Idle Auto-Trim (T+8s)*: Membersihkan alokasi memori tidur saat notch dalam mode *collapsed* tanpa mengganggu animasi 60fps.
  * *Decode Pixel Contraints*: Thumbnail album art dan tangkapan layar didekode ke resolusi pas tanpa membebani Large Object Heap (LOH).

### 14. Suara Selamat Datang saat Startup
* **Welcome Chime**: Nada arpeggio lembut (C5–E5–G5–C6, ~1.9 detik) diputar sekali saat Kuri selesai dijalankan, termasuk peluncuran otomatis.
* **Bisa Dimatikan**: Toggle **Startup Welcome Sound** di Pengaturan → Sistem & Perilaku; menghidupkannya kembali akan memutar pratinjau nada.

### 15. Animasi Masuk saat Startup
* **Mendarat Halus**: Saat Kuri dijalankan, notch tidak lagi muncul statis — notch meluncur turun dari tepi atas monitor (400 ms) dengan pop skala lembut berporos atas-tengah (460 ms `BackEase`), fade-in badan notch (280 ms), lalu konten menyusul fade-in (240 ms).
* **Sapaan Maskot**: Bila Pet Companion aktif, konten notch digantikan sapaan maskot melambai di tengah (3 detik) yang bergeser ke posisi pet dan memudar sambil konten fade-in; tanpa pet, konten langsung fade-in seperti biasa.
* **Sekali per Proses**: Entrance hanya diputar pada kemunculan pertama; saat masuk fullscreen (slim bar) atau notch disembunyikan, animasi dilewati dan transisi normal yang berlaku.

### 16. Download Monitor (Unduhan Browser & Aplikasi)
* **Deteksi Universal**: Memantau file parsial `.crdownload`, `.part`, `.partial`, `.opdownload`, dan `.download` di folder Downloads serta folder kustom Chrome/Edge/Brave/Vivaldi/Opera/Firefox.
* **Pil & Panel**: Pil di bawah notch menampilkan label `Downloading...`, lalu `terunduh / total | kecepatan` (mis. `12.4 MB / 30.0 MB | 1.8 MB/s`) plus garis progres tipis; ringkasan agregat (persen, ukuran, kecepatan) dengan tombol buka folder di baris tambahan Control Center. Klik pil membuka panel.
* **Persen Akurat untuk Browser Chromium**: Total ukuran dibaca read-only dari tabel `downloads` di History Chrome/Edge/Brave/Vivaldi/Opera (tanpa URL/riwayat); sumber lain (Firefox/aplikasi) tetap menampilkan ukuran terunduh + kecepatan.
* **Tanpa Beban Saat Idle**: Timer polling hanya hidup selama ada unduhan (1 detik aktif, 5 detik saat dijeda); toast `📥` muncul saat unduhan selesai. Bisa dimatikan lewat toggle **Download Monitor**.

### 17. Shelf & Phone Share: Transfer File Modern
* **Shelf Persisten**: Isi shelf tersimpan di `shelf.json` dan selamat dari restart aplikasi; file yang dipindah/dihapus dari luar ditandai redup dengan status "hilang" (`IsMissing`) alih-alih lenyap diam-diam.
* **Overlay Drop 3 Target**: Seret file ke notch → pilih **Simpan di Shelf**, **Tambahkan ke Dock**, atau **Konversi Gambar** (JPG↔PNG; PNG/BMP/GIF → JPG latar putih kualitas 92, file asli tak tersentuh, hasil `-converted` di folder sumber, dengan toast ringkasan).
* **Hapus Setelah Drag**: Opsi Pengaturan agar item shelf otomatis terangkat dari daftar setelah berhasil diseret keluar (perilaku clipboard-manager klasik).
* **Phone Share (Berbagi ke HP)**: Klik tombol `HP` di slide Shelf → notch memunculkan **kode QR**; pindai dari HP (satu jaringan Wi-Fi) tanpa aplikasi tambahan untuk:
  * **Mengirim file dari HP ke PC** — mendarat otomatis di Shelf (folder tujuan dapat diatur, default `Downloads\Kuri Shelf`), dengan proteksi nama file tercadang, anti-duplikasi retry browser, dan batas 2 GB.
  * **Mengunduh file Shelf ke HP** lewat daftar "File di PC Anda".
  * **Mengirim teks/link dari HP** — langsung masuk ke clipboard PC dengan toast pratinjau.
  * Server HTTP lokal berjalan di **port acak + token acak per sesi**, mati sendiri setelah 15 menit idle, dan bisa dihentikan satu klik dari panel. Halaman web HP ikut berbahasa EN/ID.

---

## 💻 Persyaratan Sistem

* **Sistem Operasi**: Windows 10 (Build 19041 / Version 2004 ke atas) atau Windows 11.
* **Arsitektur**: Mendukung penuh **64-bit (x64)** maupun **32-bit (x86)**.
* **Runtime**: Tidak memerlukan instalasi runtime tambahan (seluruh paket rilis sudah *Self-Contained*).

---

## 🚀 Cara Menjalankan & Menginstal

> Panduan langkah demi langkah beserta checklist pengujian lengkap tersedia di [CARA-MENJALANKAN.md](CARA-MENJALANKAN.md).

### Cara 1: Menggunakan Installer Setup (.exe) — *Paling Mudah*
Tersedia di folder [`release\installer\`](../release/installer):
* **`Kuri-Setup-v1.0-Universal.exe`** (*Rekomendasi*):
  Satu installer untuk semua jenis PC. Otomatis mendeteksi apakah Windows Anda 64-bit atau 32-bit dan memasang versi terbaik.
* **`Kuri-Setup-v1.0-x64.exe`**: Khusus Windows 10/11 64-bit.
* **`Kuri-Setup-v1.0-x86.exe`**: Khusus Windows 10 32-bit.
* Di halaman akhir wizard, centang **Launch Kuri** agar Kuri langsung berjalan setelah instalasi.
* Di halaman Tasks, centang **Start Kuri quickly on Windows sign-in (scheduled task, may require Administrator approval)** bila ingin Kuri otomatis berjalan lebih cepat saat login (default: tidak dicentang). Windows akan meminta persetujuan Administrator satu kali; bila ditolak, installer memakai metode klasik yang bisa lebih lambat.

### Cara 2: Menggunakan Paket Portable Siap Pakai (.zip / folder)
Cukup ekstrak dan jalankan **`NotchWin.exe`**:
* **64-bit**: [`release\Kuri-v1.0-win-x64\`](../release/Kuri-v1.0-win-x64) atau `release\Kuri-v1.0-win-x64.zip`
* **32-bit**: [`release\Kuri-v1.0-win-x86\`](../release/Kuri-v1.0-win-x86) atau `release\Kuri-v1.0-win-x86.zip`

### Cara 3: Menjalankan dari Kode Sumber (Development)
```powershell
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"; $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
dotnet run --project NotchWin\NotchWin.csproj -c Release
```

---

## 🛠️ Cara Membangun Seluruh Installer & Paket Rilis

Jalankan perintah berikut di PowerShell untuk mengompilasi rilis x64, x86, dan menghasilkan seluruh installer `.exe`:
```powershell
powershell -ExecutionPolicy Bypass -File .\build-installers.ps1
```

---

## 🧭 Aturan Kerja Saat Mengembangkan / Memperbaiki Fitur

1. **Baca file modul terkait dahulu** — selalu periksa spesifikasi fitur pada tabel indeks di atas sebelum mengubah baris kode apa pun.
2. **Karantina kode** — jangan mengubah ViewModel, Model, atau Service modul lain jika bug hanya terjadi pada modul spesifik.
3. **Patuhi batasan build** — jangan pernah menjalankan `dotnet run` atau mengeksekusi aplikasi secara langsung. Verifikasi sintaks dan BAML hanya dengan `dotnet build`.
