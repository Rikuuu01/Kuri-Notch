# Panduan Menjalankan, Menginstal & Menguji Kuri

> Indeks seluruh dokumentasi: [README.md](README.md)

Dokumen ini berisi panduan praktis untuk menginstal dan menjalankan aplikasi **Kuri** di komputer Anda (mendukung **Windows 10** dan **Windows 11**, baik **64-bit (x64)** maupun **32-bit (x86)**), serta cara menguji seluruh fitur lengkapnya.

---

## 🚀 Pilihan Cara Menjalankan Aplikasi

### Opsi 1: Menggunakan Paket Installer Setup (.exe) — *Direkomendasikan*
Installer setup modern berbasis Inno Setup dengan ikon resmi, wizard modern, opsi autostart, pintasan Desktop, dan uninstaller otomatis:

1. Buka folder:
   [`release\installer\`](../release/installer)
2. Pilih file instalasi sesuai kebutuhan:
   * **`Kuri-Setup-v1.0-Universal.exe`** (*All-in-One*):
     Mendukung Windows 10 & 11 baik 64-bit maupun 32-bit dalam 1 file setup. Installer akan otomatis mendeteksi arsitektur Windows Anda dan menginstal versi native yang paling optimal.
   * **`Kuri-Setup-v1.0-x64.exe`**:
     Khusus untuk Windows 10 (64-bit) dan Windows 11 (64-bit).
   * **`Kuri-Setup-v1.0-x86.exe`**:
     Khusus untuk Windows 10 (32-bit) atau mesin x86.
3. Ikuti wizard instalasi. Kuri dapat diinstal untuk satu pengguna (tanpa perlu hak Administrator) maupun untuk seluruh sistem.
4. Di halaman Tasks, centang **Start Kuri quickly on Windows sign-in (scheduled task, may require Administrator approval)** bila ingin Kuri otomatis berjalan lebih cepat saat login (default: tidak dicentang). Windows akan meminta persetujuan Administrator satu kali; bila dibatalkan, Kuri memakai metode klasik yang bisa lebih lambat. Opsi ini juga bisa diubah kapan saja di Pengaturan.
5. Di halaman terakhir, centang **Launch Kuri** agar Kuri langsung berjalan setelah instalasi.

---

### Opsi 2: Menggunakan Paket Portable Siap Pakai (Tanpa Instalasi)
Aplikasi sudah dikemas secara *self-contained* (tidak memerlukan instalasi .NET runtime tambahan):
* **Versi 64-bit (x64)**:
  1. Buka folder: [`release\Kuri-v1.0-win-x64\`](../release/Kuri-v1.0-win-x64)
  2. Klik ganda file **`NotchWin.exe`** (atau jalankan **`Jalankan-Kuri.bat`**).
  3. *(Opsional)* Klik ganda **`Buat-Shortcut-Desktop.bat`** untuk membuat icon di Desktop.
* **Versi 32-bit (x86)**:
  1. Buka folder: [`release\Kuri-v1.0-win-x86\`](../release/Kuri-v1.0-win-x86)
  2. Klik ganda file **`NotchWin.exe`**.

Arsip ZIP untuk dibagikan ke PC lain:
* `release\Kuri-v1.0-win-x64.zip`
* `release\Kuri-v1.0-win-x86.zip`

---

### Opsi 3: Menjalankan Mode Development
Jika Anda ingin menjalankan langsung dari folder proyek melalui terminal:
```powershell
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"; $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
dotnet run --project NotchWin\NotchWin.csproj -c Release
```

---

## 🧪 Panduan Menguji Fitur Lengkap

### 1. Interaksi Dasar (Hover, Expand, Pin) & Menu Home
* **Hover (Arahkan Kursor)**:
  Arahkan kursor mouse ke notch di bagian tengah atas layar. Notch akan membesar (*expand*) secara halus menampilkan halaman **Home (Dashboard Utama)**.
* **Keluar Hover**:
  Jauhkan kursor dari notch. Notch akan mengecil kembali (*collapse*) secara otomatis setelah jeda singkat (~400 ms).
* **Pin / Kunci Panel**:
  Klik tombol pin di pojok kanan atas notch untuk mengunci posisinya agar tetap terbuka meskipun kursor dijauhkan. Klik sekali lagi untuk melepas kuncian (*unpin*).

---

### 2. Menu Home (Dashboard Utama Kuri)
1. Buka notch (otomatis berada di tab pertama: ikon Rumah).
2. Periksa komponen-komponen utama Home:
   * **Miniplayer Card**: Menampilkan cover album proporsional 86×86 (sudut membulat 16px via `OpacityMask`), judul lagu tegas, nama artis/status adzan hijau emerald `#34D399`, seekbar ramping 14px dengan label waktu berjalan, dan tombol Prev (28×28), Circular White Play/Pause (34×34 dengan margin 22px), serta Next (28×28).
   * **Kalender Mingguan**: Menampilkan strip 7 hari (M S S R K J S) dengan tinggi seragam 120 DIPs, lingkaran penanda tanggal hari ini (`IsToday`), dan hari libur berwarna merah.
   * **App Shortcut Dock**: Terletak di bawah miniplayer dan kalender dengan tinggi 44 DIPs, menampilkan ikon aplikasi favorit tanpa terpotong kurva bawah notch. Coba klik `+` atau drag file `.exe` / `.lnk` ke dock untuk menambahkan pintasan.
   * **Banner Notifikasi Cerdas**: Muncul secara dinamis di baris paling bawah saat ada adzan, alarm, atau timer yang selesai (+60 DIPs ekspansi tinggi), lengkap dengan tombol penanganan cepat (Tunda / Matikan).

---

### 3. Pengaturan Bahasa (Language: English / Bahasa Indonesia)
1. Buka **Pengaturan** (klik kanan icon di System Tray -> *Settings* / *Pengaturan*, atau klik tombol Settings di slide Pusat Kontrol).
2. Di bagian paling atas terdapat kartu **LANGUAGE / BAHASA**:
   * Pilihan default: **English (Default)**.
   * Pilih **Bahasa Indonesia** untuk mengubah seluruh teks aplikasi.
3. Seluruh UI aplikasi akan langsung berubah secara instan tanpa perlu restart.

---

### 4. Widget Media & Karaoke Lirik Sinkron (Spicy Lyrics + LRCLIB)
1. Putar lagu di Spotify, YouTube, Apple Music, atau browser media lainnya.
2. Saat notch dibuka:
   * Menampilkan cover art, judul lagu, nama artis, seekbar interaktif, dan tombol Prev, Play/Pause, Next.
   * Klik ikon **Lirik (Karaoke)** di kanan bawah panel media:
     * Area media akan bergeser ke kiri dengan animasi halus memberi ruang pada panel lirik.
     * **Syllable Sync ala Spicy Lyrics**: Sapuan warna putih pada baris aktif berjalan presisi kata-demi-kata (suku kata) mengikuti vokal penyanyi secara real-time, membeku (*freeze*) pada jeda antar-kata saat penyanyi mengambil napas.
     * **Pemilih Versi Lirik & Badge Provider**: Klik chip versi di pojok kanan bawah (`1/x ▾`) untuk membuka daftar versi lirik. Perhatikan lencana visual:
       - 🟥 **`[Spicy Lyrics]`**: Lirik suku kata TTML dengan tag `SYLLABLE` (prioritas utama).
       - ◻️ **`[LRCLIB]`**: Lirik baris standar dengan tag `SYNCED` atau `PLAIN`.
     * **Tampilan Penuh Tanpa Terpotong**: Setiap baris lirik tampil utuh dan natural sampai baris berikutnya berbunyi (tanpa pemotongan prematur).
     * **Ikon Musik Statis (`♫`)**: Jeda instrumen dan intro tampil tenang dengan simbol nada musik tanpa hitung mundur mengganggu.
     * **Penyimpanan Offset Otomatis**: Tombol kalibrasi offset (+ / - 0.5s) otomatis menyimpan offset ke `%AppData%\NotchWin\SongOffsets.json` per lagu, sehingga Anda tidak perlu mengatur ulang saat lagu yang sama diputar kembali.
     * **Badge Sumber & Tipe Lirik**: Di pojok kanan-bawah panel lirik tampil teks kecil tanpa background yang menandai sumber aktif: `Spicy Lyrics · SYLLABLE`, `lrclib.net · SYNCED`, dll.
     * Saat lirik ditutup, cover dan judul bergeser kembali ke kanan dengan mulus.

---

### 5. Pusat Kontrol (Control Center)
* Geser slide ke arah kanan/kiri menggunakan carousel dots atau roda mouse.
* Menampilkan slider Volume Master dan Kecerahan Layar (*Screen Brightness*).
* Menampilkan tombol aksi cepat: **Screenshot**, **Lock PC**, **Weather**, dan **Settings**.

---

### 6. Informasi Cuaca & Prakiraan 7 Hari
* Menampilkan suhu real-time, kondisi cuaca, kelembapan, kecepatan angin, indeks UV, dan kemungkinan hujan.
* Menampilkan prakiraan cuaca 7 hari ke depan.
* Lokasi dapat disesuaikan lewat Pengaturan (bisa dicari manual atau memilih kota preset).

---

### 7. Jadwal & Pengingat Sholat
* Menampilkan 5 waktu sholat (Subuh, Terbit, Dzuhur, Ashar, Maghrib, Isya).
* Kartu countdown dinamis menuju waktu sholat berikutnya.
* Notifikasi audio halus dan banner saat waktu sholat tiba.

---

### 8. Pengingat Minum & Layar Aktif (Sedentary Flip Card)
* **Pengingat Minum**:
  * Menampilkan asupan air harian dalam Liter (misal: `Today: 0.5L of 2.0L`).
  * Tombol `+250 ml` dan tombol `Reset` yang rapi.
* **Pengingat Duduk (Sedentary Tracker)**:
  * Sensor pintar: Timer duduk otomatis dijeda (*standby*) saat komputer tidak digunakan/kursor diam, dan berjalan kembali saat ada aktivitas.
  * **Interactive Flip Card**: Klik pada kartu untuk membalik kartu dan melihat **Statistik Duduk Harian** (Total durasi aktif depan layar, sesi duduk terlama, dan frekuensi berdiri). Klik `↩ Back` untuk membalik kartu kembali.

---

### 9. To-Do List (Daftar Tugas)
* Dilengkapi **Modern Slim Scrollbar** kapsul 5px transparan yang minimalis dan elegan.
* Kolom input ketik tugas baru dengan tombol `+`.
* Checklist lingkaran modern untuk menandai tugas selesai dan tombol hapus.

---

### 10. Timer & Stopwatch
* **Tab Timer**: Pilihan preset cepat (1 Menit, 5 Menit, 10 Menit, 25 Menit Pomodoro) dengan animasi countdown melingkar.
* **Tab Stopwatch**: Penghitung waktu presisi milidetik dengan fitur pencatatan putaran (Lap) dan modern slim scrollbar.
* Jika Timer dan Stopwatch berjalan bersamaan, keduanya berbagi slot slide secara harmonis tanpa menduplikasi jendela notch.

---

### 11. Indikator Privasi, Volume HUD & Notifikasi USB
* **Titik Privasi Kamera (🟢 Hijau Glowing)** dan **Mikrofon (🟠 Oranye Glowing)** menyala otomatis saat webcam atau mikrofon sedang digunakan oleh aplikasi apa pun.
* **Volume HUD Popup**: Bar dinamis muncul seketika saat tombol volume ditekan.
* **Notifikasi USB Flashdisk**: Muncul banner elegan saat flashdisk dicolok atau dicabut.
* **Mode Fullscreen Slim Bar**: Saat bermain game atau menonton video fullscreen (borderless / F11), notch berubah menjadi **garis tipis 220×6 px** di tengah atas layar. Garis ini 100% tembus klik sehingga input game tidak terganggu. Lewat **Pengaturan → Sistem → Auto-hide on Fullscreen Apps**, sub-opsi *Show Slim Line in Fullscreen* bisa dimatikan untuk kembali ke perilaku sembunyi total.
  * **Menguji hover-peek**: arahkan kursor tepat di atas garis tipis dan tahan selama **3 detik**. Garis akan membesar halus sebagai indikator, lalu notch muncul sebagai pill (*morph* garis → pill). **Klik pill** untuk membuka panel penuh; hover saja akan membiarkan pill tetap tampil dan tidak membuka panel. Di notch biasa (non-fullscreen), hover tetap otomatis membuka panel.
  * **Kembali ke garis**: pindahkan kursor keluar dari notch. Notch akan collapse, lalu bermorph balik menjadi garis tipis (pill memudar sambil bar muncul, lalu bar menyusut) setelah animasi selesai. Jika notch dipin (`IsLockedExpanded`), notch tetap terbuka sampai pin dilepas.
  * **Catatan**: jangan menekan tombol mouse saat garis di-hover — garis tetap click-through, jadi klik diteruskan ke aplikasi fullscreen di bawahnya.
  * Keterbatasan: pada game *exclusive fullscreen* (Direct3D exclusive), overlay Windows tidak dapat ditampilkan di atas game; garis akan muncul kembali setelah keluar fullscreen.

---

### 12. Menguji Maskot Interaktif & Pet Companion
* **Memilih Maskot di Pengaturan**:
  1. Buka jendela **Pengaturan** dan cari seksi **Pet Companion**.
  2. Pastikan toggle **Enable Pet Companion** aktif.
  3. Pilih gaya maskot yang Anda inginkan:
     * **Astro Mascot**: Astronot berhelm putih dengan visor navy dan mata amber menyala, plus headphone groove reaktif.
     * **Pastel Mochi**: Karakter kelinci lavender pastel dengan 8 badge notifikasi dinamis dan efek melayang.
* **Menguji Interaksi Sentuh (*Petting*)**:
  * Gerakkan mouse di sekitar layar: perhatikan pupil mata maskot melirik mengikuti arah kursor.
  * Klik langsung pada badan maskot: maskot akan memantul kegirangan (*Happy bounce*) dengan rona pipi merah muda dan bintang kelap-kelip.
* **Menguji di Pemutar Musik (Media Widget)**:
  * Putar lagu di Spotify atau YouTube.
  * Bilah notch akan beralih ke widget musik: perhatikan maskot otomatis muncul di sebelah kanan visualizer audio, mengenakan headphone, bergoyang mengikuti ketukan ritme, dan mengeluarkan not balok melayang ke atas.
  * Jeda (*pause*) musik: headphone otomatis terlepas dengan halus dan maskot kembali bernapas santai.
  * Konsistensi ekspresi: saat musik masih berputar, jauhkan kursor hingga notch menutup — maskot di bilah notch kecil harus tetap tampil dengan headphone dan bergoyang, identik dengan ekspresi di panel penuh.
* **Menguji Reaksi Badge Notifikasi**:
  * **Teks Disalin (Clipboard)**: Salin (*Ctrl+C*) teks apa pun di browser/Word $\rightarrow$ maskot tersenyum dengan badge clipboard dan centang hijau.
  * **Tangkapan Layar (Screenshot)**: Tekan shortcut screenshot atau tombol Screenshot di Pusat Kontrol $\rightarrow$ tubuh maskot menyala putih (blitz) dengan badge kamera.
  * **Pengingat Minum**: Klik tombol `+250ml` di pelacak air minum $\rightarrow$ maskot bergoyang dengan badge tetesan air.
* **Menguji Siklus Tidur (*Idle Auto-Sleep*)**:
  * Biarkan mouse diam selama > 5 menit tanpa interaksi.
  * Maskot akan memejamkan mata dan tertidur lelap dengan partikel huruf `Z` melayang.
  * Gerakkan kursor mouse sedikit saja: maskot langsung terbangun dan membuka mata dengan kedipan segar.

---

### 13. Skala Ukuran Notch & Efisiensi Memori (RAM Ringan ~54 MB)
* **Menguji Skala Notch Terkalibrasi**:
  1. Buka jendela **Pengaturan** dan cari slider **Notch Size (Scale)** / **Ukuran Notch (Skala)**.
  2. Nilai default **100%** kini mewakili ukuran fisik notch yang lebih lega dan proporsional (setara 120% sebelumnya).
  3. Anda dapat menggeser slider dari 75% hingga 140% sesuai kenyamanan resolusi layar monitor.
* **Memverifikasi Penggunaan RAM di Task Manager**:
  1. Buka Windows Task Manager (*Ctrl+Shift+Esc*).
  2. Cari proses **NotchWin.exe**.
  3. Perhatikan konsumsi memori: NotchWin berjalan sangat efisien di kisaran **~54 MB – 62 MB** bahkan saat aktif memutar lagu ber-cover resolusi tinggi, lirik berjalan, dan maskot aktif.
  4. Saat notch didiamkan (*collapsed idle*), layanan `MemoryManagementService` secara otomatis memangkas memori tidur (*trimming*).

---

---

### 14. Menguji Pusat Kontrol & Pemilih Output Audio (Keputusan 70, 71, 72)
1. **Slider Kapsul Volume & Kecerahan**:
   - Buka slide Pusat Kontrol (Slide 0).
   - Geser slider volume: perhatikan track kapsul 6 DIPs tebal dan thumb lingkaran putih dengan bayangan halus.
   - Geser slider kecerahan: warna slider kuning hangat matahari (`#FBBF24`).
2. **Pemilih Perangkat Audio Speaker (Anti-Collapse)**:
   - Klik chip dropdown perangkat audio (misal: `FxSound Speakers... ▾`).
   - Perhatikan daftar dropdown `DevicePickerPopup` terbuka ke bawah.
   - **Verifikasi Kritis**: Arahkan kursor mouse masuk dan keluar dari daftar dropdown perangkat; notch **TIDAK BOLEH MENUTUP OTOMATIS** saat dropdown terbuka.
   - Pilih salah satu soundcard / headphone: perangkat terpilih berganti secara instan dengan tanda centang aksen dan dropdown menutup normal.
3. **Penjajaran Ikon & Baterai HD**:
   - Perhatikan ikon Ethernet/Wi-Fi dan ikon Bluetooth sejajar persis di sumbu tengah.
   - Ikon baterai 22×11 DIPs memiliki garis tepi metalik tegas, indikator terminal anoda solid di kanan, dan petir tajam tanpa halo hitam blur.

---

### 15. Menguji Anti-Duplikasi File Shelf (Keputusan 76)
1. Buka slide **Shelf** (Slide 8) atau siapkan berkas di Desktop / File Explorer.
2. Seret (*drag*) berkas apa pun dari File Explorer menuju Dynamic Notch.
3. Lepaskan (*drop*) berkas ke kotak target **Simpan di Shelf**:
   - Perhatikan berkas masuk ke daftar Shelf dan animasi ledakan partikel shockwave berbunyi di belakang kartu.
4. Uji coba drop cepat ganda:
   - Seret berkas yang sama dan lepaskan kembali secara berturut-turut atau jatuhkan sekelompok berkas sekaligus.
   - **Verifikasi Kritis**: Tepat 1 item berkas yang tersimpan di Shelf. Berkas **TIDAK AKAN PERNAH** terduplikasi menjadi 2 item untuk 1 file fisik yang sama.

---

### 16. Menguji Notifikasi & Toast Multibahasa Bebas Mojibake (Keputusan 73, 74, 75)
1. **Pilihan Bahasa Indonesia**:
   - Pastikan Bahasa Indonesia aktif di Pengaturan.
   - Buka tab **Catatan**: perhatikan nama tab tertulis **Catatan** (bukan Note), dan tab Clipboard tertulis **Papan Klip**.
   - Setel waktu alarm di kolom pengingat lalu klik tombol set: toast sub-notch meluncur turun dengan pesan bahasa Indonesia bersih: `⏰ Alarm diatur: HH:mm (Besok)`.
   - **Verifikasi Anti-Mojibake**: Perhatikan tombol tutup silang di ujung kanan notifikasi sub-notch — tombol berupa tanda silang vektor tajam murni, tidak ada karakter rusak `Ā¢Å"â€¢`.
2. **Ketajaman Teks HD**:
   - Perhatikan seluruh tulisan pada jam, lirik lagu, teks catatan, dan suhu cuaca (`25°C`).
   - Seluruh huruf dan angka tampil tajam, hitam pekat berdefinisi tinggi (HD) berkat subpixel ClearType DirectWrite tanpa gerigi aliasing.

---

### 17. Menguji Phone Share (QR, Upload, Download, Teks)
1. **Aktifkan Panel**: Buka notch → tab Shelf → klik tombol **`HP`** di kolom kanan. Panel `ShelfPhonePanel` terbuka menampilkan QR, URL `http://<IP-LAN>:<port>/t/<token>/`, status "Menunggu HP...", dan titik hijau hidup.
2. **Koneksi HP**: Pastikan HP & PC satu Wi-Fi. Pindai QR dengan kamera HP (atau salin tautan lalu buka manual). Izinkan Kuri bila Windows Firewall bertanya. Status panel berubah menjadi **"HP terhubung"** saat halaman pertama dimuat.
3. **Upload HP → PC**: Di halaman web HP, pilih file (foto/dokumen) → progresupload selesai → **toast `📥` "File dari HP"** muncul di notch dan file otomatis terlihat sebagai item baru di Shelf (dengan animasi ledakan drop). File tersimpan di folder HP (`Downloads\Kuri Shelf` bila tidak diubah di Pengaturan).
4. **Download Shelf → HP**: Kartu "File di PC Anda" di halaman HP menampilkan seluruh file Shelf; klik **Unduh** → file terunduh ke HP. Hapus item dari Shelf → item lenyap dari daftar HP (item `IsMissing` tidak muncul sama sekali).
5. **Teks/Link HP → Clipboard PC**: Ketik/letakkan teks di kartu "Kirim teks", kirim → toast `📋`/`🔗` muncul dengan preview; tempel (Ctrl+V) di aplikasi mana pun di PC — isi clipboard persis teks dari HP.
6. **Batasan & Proteksi**: Coba upload file > 2 GB → ditolak `413` dengan pesan "terlalu besar" di HP. URL tanpa token → `404`.
7. **Stop & Auto-Idle**: Klik **Stop** di panel → titik hijau mati, status "Server berhenti", HP terputus. Untuk idle, server mati sendiri setelah ±15 menit tanpa permintaan. Matikan toggle **Phone Share** di Pengaturan → tombol `HP` hilang dan server yang sedang jalan langsung berhenti.

---

### 18. Menguji Persistensi Shelf, Hapus-Setelah-Drag & Konversi Gambar
1. **Persistensi `shelf.json`**: Masukkan 2–3 file ke Shelf → tutup Kuri sepenuhnya (exit dari tray) → jalankan lagi → buka tab Shelf: item lama masih ada di posisi yang sama. Ubah/hapus salah satu file asli di Explorer → buka lagi → item ditandai redup "hilang", bukan hilang diam-diam.
2. **Berkas Korup**: (opsional) Rusak isi `%APPDATA%\NotchWin\shelf.json` → start → shelf mulai kosong tanpa crash; file lama dipindah ke `shelf.json.bad`.
3. **Hapus Setelah Drag**: Aktifkan **Hapus setelah dikeluarkan** di Pengaturan → seret item Shelf keluar ke Explorer/browser → lepas → item hilang dari Shelf (file asli tetap utuh). Dropping yang *batal* (ESC) tetap menyimpan item.
4. **Konversi Gambar**: Seret 1 file `.jpg` dan 1 `.png` dari Explorer ke notch → overlay 3 kotak muncul → jatuhkan ke kotak **Konversi Gambar**: toast `🔄` muncul dengan nama output. Cek folder sumber: `nama-converted.png` dari JPG, `nama-converted.jpg` (latar putih, bukan hitam) dari PNG/GIF/BMP. File asli tidak berubah. Seret file non-gambar (mis. `.txt`) → toast "tidak ada yang dikonversi".

---

### 19. Menguji Karaoke Compact, Glow Outline, Adzan Snooze & Bluetooth Radio
1. **Karaoke 1-Baris di Pill**: Putar lagu bersubtitel di Spotify → enable lirik → jauhkan kursor sampai notch collapsed. Pill menampilkan **satu baris** yang berganti dengan animasi naik/fade halus, dan teks terisi abu → putih seirama nyanyian. Matikan toggle **Karaoke Fill** di Pengaturan → baris tetap tampil tapi tanpa isian progres.
2. **Ukuran & Bold Lirik**: Ubah slider **ukuran teks lirik** (10–14) dan toggle **bold** → panel lirik expanded langsung berubah; baris aktif = basis + 2.5.
3. **Glow Outline Timer**: Start timer Pomodoro → notch tertutup menampilkan garis cahaya tipis emerald menyusut di sepanjang outline; jalankan stopwatch → garis berputar tiap detik (bukan 20×/detik). Memicu alert (mis. timer selesai) → cahaya berjalan keliling maks 20 detik. Matikan kedua toggle Pengaturan → garis lenyap, konsumsi CPU tetap sama.
4. **Adzan Snooze**: Set pengingat sholat (atau tunggu waktunya) → saat adzan, **banner Home** muncul persisten (tidak hilang sendiri). Klik **Tunda** → suara berhenti, banner kembali ±5 menit kemudian; klik **Hentikan** → banner & suara lenyap total.
5. **Status Bluetooth Radio**: Di Pusat Kontrol, baris Bluetooth menampilkan perangkat audio BT aktif, `Bluetooth Ready` (radio nyala) atau `Bluetooth Off` (radio mati) — state dibaca via WinRT radio + polling 8 detik dan saat `WM_DEVICECHANGE`. Ubah radio dari Windows Settings → toast `Bluetooth Aktif/Nonaktif` muncul. (Perintah toggle `QuickToggleBluetoothCommand` sudah tersedia di ViewModel tetapi **belum dipasangkan ke tombol UI** — jangan diuji sebagai tombol.)

---

## 🛠️ Cara Membangun Installer Kembali
Jika Anda membuat perubahan kode di masa mendatang dan ingin memperbarui installer:
```powershell
powershell -ExecutionPolicy Bypass -File .\build-installers.ps1
```
Script tersebut akan otomatis mengompilasi rilis x64, x86, dan menghasilkan installer setup `.exe` lengkap di folder `release\installer\`.
