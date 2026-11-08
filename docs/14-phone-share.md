# 📱 Modul 14: Phone Share (Berbagi ke HP)

> Indeks seluruh dokumentasi: [README.md](README.md)

Modul ini memungkinkan transfer file dan teks dua arah antara HP dan PC tanpa aplikasi pihak ketiga. Notch menyajikan kode QR; setelah dipindai, HP membuka halaman web lokal ringan (satu Wi-Fi yang sama) untuk mengunggah file ke Shelf, mengunduh file dari Shelf, serta mengirim teks/link langsung ke clipboard PC.

---

## 📂 File Terkait
- **Service**: [PhoneShareService.cs](../NotchWin/Services/PhoneShareService.cs) (server HTTP TCP, QR, endpoint, validasi upload)
- **Halaman web HP**: `NotchWin/Assets/PhoneShare/index.html` (di-embed sebagai `EmbeddedResource` via csproj)
- **View XAML**:
  - [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml) — `ShelfPhonePanel` (panel QR di slide Shelf) & tombol `HP` kolom kanan shelf
- **View Code-Behind**: [NotchWindow.xaml.cs](../NotchWin/Views/NotchWindow.xaml.cs) (`ShelfPhoneCopyLink_Click`, `ShelfPhoneStop_Click`)
- **ViewModel**: [NotchViewModel.cs](../NotchWin/ViewModels/NotchViewModel.cs) (`ShowPhoneShare`, `OpenPhoneSharePanel`, `PhoneShare` property, wiring toast)
- **Settings**: [SettingsViewModel.cs](../NotchWin/ViewModels/SettingsViewModel.cs) (`IsPhoneShareEnabled`, `PhoneFolder`, `BrowsePhoneFolder`), [SettingsWindow.xaml](../NotchWin/Views/SettingsWindow.xaml)
- **Dependensi**: paket NuGet `QRCoder` (PngByteQRCode)

---

## 📦 Komponen & Alur Kerja

### 1. Server HTTP Lokal (Tanpa Koneksi Internet)
- `PhoneShareService.Start()` membuka `TcpListener` pada `IPAddress.Any` **port acak** (ephemeral) dan melayani HTTP/1.1 murni hasil parsing manual (`ReadHeadersAsync`, tanpa `HttpListener`/ASP.NET).
- **Tokenisasi URL**: setiap sesi mendapat token acak 16 byte (`RandomNumberGenerator`), sehingga basis URL selalu `http://<LAN-IP>:<port>/t/<token>/`. Permintaan di luar prefix token dibalas `404`; server tidak punya endpoint tanpa token.
- **Pemilihan IP LAN**: `GetLanAddress()` menyaring antarmuka fisik aktif (`OperationalStatus.Up`, non-loopback) dan memprioritaskan alamat privat RFC1918 (`10.x`, `172.16–31.x`, `192.168.x`) agar URL dapat dibuka HP; fallback alamat publik → loopback.
- **Watchdog idle**: `IdleWatchdogAsync` cek aktivitas tiap 30 detik; tidak ada permintaan selama **15 menit** (`IdleTimeout`) → server berhenti sendiri (`Stop()`), listener & semua `TcpClient` ditutup.
- Batas proteksi: header ≤ 16 KB, file ≤ **2 GB** (`MaxFileBytes`), teks ≤ **64 KB** (`MaxTextBytes`); `Transfer-Encoding: chunked` ditolak `411` (wajib `Content-Length`); `Expect: 100-continue` dijawab bila diminta.

### 2. Endpoint
| Method | Route (setelah `/t/<token>/`) | Fungsi |
|:---|:---|:---|
| `GET` | `` (kosong) | Halaman web HP (HTML embed `Assets/PhoneShare/index.html`, string UI disuntik via placeholder `__STRINGS__`, `__MAX_FILE__`, `__MAX_TEXT__`) |
| `GET` | `list` | JSON daftar file Shelf yang masih ada (`id`, `name`, `size`) — skip direktori/item hilang |
| `GET` | `file/{guid}` | Unduh satu file Shelf (`Content-Disposition: attachment`, streaming `CopyToAsync`) atau `404` bila hilang |
| `PUT` | `upload?name=...` | Unggah file dari HP → disimpan ke folder HP lalu **masuk otomatis ke Shelf** (`ShelfService.AddFiles`) + toast `📥` |
| `POST` | `text` | Kirim teks/link → disalin ke **clipboard PC** (`Clipboard.SetText`) + toast `🔗` (link) / `📋` (teks), preview 60 karakter |

### 3. Proteksi Nama Berkas Upload
- `SanitizeFileName`: ambil `FileName` saja (anti path traversal), ganti karakter invalid Windows dengan `_`, pangkas titik/spasi ujung, dan nama tercadang Win32 (`CON`, `PRN`, `AUX`, `NUL`, `COM1–9`, `LPT1–9`) diberi prefix `_`.
- `GetUniquePath`: bentrok nama → `nama (1).ext`, `nama (2).ext`, dst (maks 10.000 iterasi, fallback GUID).
- **Deduplikasi retry browser**: kunci `nama|ukuran` yang sama dalam 10 detik dianggap retry — body di-drain, file **tidak dibuat dua kali**, dibalas `{ok, name, duplicate:true}`.
- Gagal tulis di tengah (connection drop / disk penuh) → file parsial dihapus, balas `507`/`400`; tidak ada sampah di folder HP.

### 4. Panel QR di Slide Shelf
- **Tombol masuk & aksi atas**: kolom kanan (lebar 144 DIPs) di slide Shelf berisi baris kontrol terpadu (tinggi 26 px): tombol `Phone` (ikon vektor smartphone `#38BDF8`, lencana titik hijau `#34D399` saat server berjalan, border menyala saat panel terbuka), tombol tambah berkas `+` (26×26 px), dan tombol bersihkan semua `🗑` (26×26 px). Tombol Phone **disembunyikan total** bila `IsPhoneShareEnabled = false`.
- **Alur `ShowPhoneShare()`** (toggle): panel terbuka → tutup; panel tertutup → `Start()` dulu bila server mati (gagal start memunculkan toast `Phone.Error`), baru buka panel.
- Isi panel (`ShelfPhoneUnderBarcode`, `#12121E`, radius 8, padding 6×6): kartu QR tajam muncul di bawah baris tombol aksi, memuat kartu QR putih `NearestNeighbor` 70×70, hint pindai (font 10 SemiBold `#E4E4E7`), URL `http://LAN:port/t/token/` (font 9.5 Medium `#38BDF8`), tombol **Salin Tautan** dan **Stop** (tinggi 21 px, font 9.5 SemiBold), status hidup-titik-hijau (font 9 Medium). Dilengkapi `RenderOptions.ClearTypeHint="Enabled"`, `TextOptions.TextFormattingMode="Display"`, dan font variabel `Segoe UI Variable Text` anti-blur. Tinggi total panel (~160 DIPs) ditambah baris tombol (26 DIPs) menghasilkan total 186 DIPs yang muat utuh di dalam ruang notch 190 DIPs tanpa terpotong (*anti-clipping*).
- QR digenerate `PngByteQRCode` (ECC level Q, scale 8) → `BitmapImage` frozen; kegagalan QR tidak menghentikan server (URL tetap bisa disalin manual).

### 5. Pengaturan Terkait
- **Phone Share** (`AppSettings.IsPhoneShareEnabled`, default `true`, toggle di Pengaturan → tab Shelf & Phone Share). Mematikannya saat berjalan langsung memanggil `PhoneShare.Stop()` via sinkronisasi settings di `NotchViewModel` (baris `IsPhoneShareEnabled = settings.IsPhoneShareEnabled`).
- **Folder HP** (`AppSettings.PhoneFolder`, default kosong = `<Downloads>\Kuri Shelf`): dialog `FolderBrowserDialog` (`BrowsePhoneFolder`, judul `Settings.PhoneFolderTitle`); nilainya dipakai `ResolvePhoneFolder()` setiap upload (perubahan langsung berlaku tanpa restart server).

### 6. Halaman Web HP (`Assets/PhoneShare/index.html`)
- Satu berkas HTML self-contained (CSS/JS inline) bertema gelap, dioptimalkan sentuh, **tanpa aset eksternal** — di-load dari `Assembly.GetManifestResourceStream` dan di-cache `Lazy<string>`.
- Tiga kartu: kirim file (progress per-file PUT), kirim teks/link (POST), dan daftar "File di PC Anda" (fetch `/list`, tombol unduh per item ke `/file/{id}`).
- Seluruh teks UI disuntik dari kamus `LocalizationService` kunci `PhonePage.*` (EN/ID) — halaman HP otomatis mengikuti bahasa yang dipilih pengguna Kuri.
- Status connection/online badge sederhana; pesan error memakai kunci `TooLarge`, `UploadFailed`, `EmptyText`, `TextTooLong`.

### 7. Lokalisasi
- Kunci EN/ID di `LocalizationService`: `Phone.*` (WindowTitle, ScanHint, OpenHint, CopyLink, LinkCopied, Start, Stop, StatusStopped, StatusRunning, StatusConnected, Error, Upload/Text/LinkNotificationTitle, UploadFolderUnavailable) dan `PhonePage.*` (19 kunci halaman HP), `Shelf.PhoneButton`, `Shelf.PhoneTooltip`, `Settings.PhoneShareTitle/Desc`, `Settings.PhoneFolderTitle`.
- `PhoneShareService` mengekspos `L10n` dan memakai `LocalizationService.Instance.Get(...)` saat status/toast dibangkitkan, sehingga pergantian bahasa runtime langsung tercermin pada panel & notifikasi.

### 8. Keamanan & Batasan Desain
- **Hanya LAN**: tidak ada relay/pengalamatan awan; port acak + token acak per sesi; token hilang saat stop.
- **Tidak ada autentikasi tambahan** — siapa pun satu Wi-Fi yang tahu URL lengkap (dengan token) bisa memakai sesi. Karena itu server sengaja mati sendiri setelah 15 menit idle dan Stop manual tersedia satu klik dari panel.
- Firewall: halaman hint (`Phone.OpenHint`) mengingatkan pengguna mengizinkan Kuri saat Windows Defender Firewall bertanya (koneksi masuk ke `TcpListener` memerlukan izin tersebut di jaringan privat).
- Tidak ada enkripsi TLS (HTTP biasa di LAN privat) — desain sadar untuk kesederhanaan; hindarkan dari Wi-Fi publik.

---

## 🚫 Panduan Modifikasi Aman (Do's & Don'ts)
- **JANGAN** memindahkan `IsPanelOpen`/`StatusText`/`Qrcode` ke thread selain dispatcher — semua setter UI wajib lewat `OnUi(...)` (`Dispatcher.Invoke`), karena `AcceptLoop` berjalan di background task.
- **JANGAN** menghapus prefix token `/t/<token>/` dari route; halaman dan semua endpoint memvalidasi prefix sebelum routing.
- **JANGAN** mengganti `TcpListener` dengan `HttpListener` tanpa uji ulang binding port ephemeral dan perilaku `100-continue` browser mobile; parser header manual ini sengaja agar tidak perlu rights `netsh http`.
- **JANGAN** menyimpan file upload langsung ke Shelf tanpa `GetUniquePath` (bentrok nama) atau tanpa dedup retry (browser mengirim ulang PUT yang sama).
- **JANGAN** lupa mendaftarkan `IsPhoneShareEnabled` dan `PhoneFolder` pada whitelist objek `new AppSettings { ... }` di `SaveCurrentSettingsAsync()`/`ResetToDefaultsAsync()` `SettingsViewModel` — tanpa itu nilai hilang saat menyimpan.
- **JANGAN** menampilkan panel saat `PhoneShare.Start()` gagal (periksa `IsRunning` lagi setelah `Start()`), karena panel tanpa QR/URL membingungkan.
