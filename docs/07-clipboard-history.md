# 📋 Modul 07: Riwayat Papan Klip (Clipboard History)

> Indeks seluruh dokumentasi: [README.md](README.md)

Modul ini memantau dan menyimpan riwayat teks maupun gambar (screenshot) yang baru saja disalin atau ditangkap oleh pengguna untuk disalin kembali secara instan dari notch, dengan dukungan penuh lokalisasi multibahasa dan tipografi HD ClearType.

---

## 📂 File Terkait
- **View XAML**: [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml#L6189-L6670) (`<!-- SLIDE 6: CLIPBOARD HISTORY -->`)
- **ViewModel**: [ClipboardViewModel.cs](../NotchWin/ViewModels/ClipboardViewModel.cs)
- **Services & Models**:
  - [ClipboardService.cs](../NotchWin/Services/ClipboardService.cs) (Win32 clipboard hook & caching)
  - [LocalizationService.cs](../NotchWin/Services/LocalizationService.cs) (Penyedia lokalisasi toast & notifikasi)
  - [ClipboardItem.cs](../NotchWin/Models/ClipboardItem.cs)

---

## 📑 Komponen & Alur Kerja

### 1. Win32 Clipboard Hook Listener
- Saat window di-load, handle window didaftarkan ke Win32 API `AddClipboardFormatListener(hwnd)` di [NotchWindow.xaml.cs](../NotchWin/Views/NotchWindow.xaml.cs).
- Setiap kali pengguna menekan `Ctrl+C` atau melakukan screenshot layar, pesan sistem `WM_CLIPBOARDUPDATE (0x031D)` ditangkap di fungsi `WndProc`.
- `ClipboardService` memproses isi clipboard di thread latar belakang untuk menghindari freezing.

### 2. Penanganan Teks & Gambar (Screenshots)
- **Tata Letak Terpisah (Split Layout)**:
  - **Sisi Kiri (Screenshots)**: Menampilkan galeri horizontal thumbnail gambar / screenshot hasil Snipping Tool dengan tombol hapus semua gambar (`Clear`), badge jumlah item, tombol salin instan, dan tombol hapus individual.
  - **Sisi Kanan (Copied Text)**: Menampilkan daftar vertikal potongan teks yang baru disalin, tombol hapus semua teks (`Clear`), badge jumlah teks, cuplikan isi teks, jumlah karakter (`X chars`), dan waktu penyalinan (`Xm ago`, `Xh ago`).
- **Bebas Background Putih pada Hover**:
  - Tombol aksi kartu menggunakan template transparan penuh (`ClipboardContentButtonStyle`), menghilangkan efek background putih default bawaan tombol WPF Aero2 saat kursor melayang di atas thumbnail atau teks.
  - Efek hover kartu kini menggunakan highlight border halus (biru muda `#38BDF8` untuk gambar, hijau mint `#34D399` untuk teks).
- **Hapus Per Kategori (Independent Category Clearing)**:
  - Pengguna bebas memilih untuk menghapus seluruh riwayat sekaligus melalui tombol `Clear All` di header atas, atau menghapus per kategori:
    - Tombol `Clear` di atas kolom Screenshots (menjalankan `ClearImagesCommand`).
    - Tombol `Clear` di atas kolom Copied Text (menjalankan `ClearTextsCommand`).
- **Lokalisasi Dinamis & Multibahasa (Keputusan 75)**:
  - Teks antarmuka, format waktu, tooltip, dan toast notifikasi beradaptasi secara otomatis mengikuti preferensi bahasa (`id` / `en`):
    - Toast Salin: `"Tersalin ke Papan Klip!"` (ID) vs `"Copied to Clipboard!"` (EN).
    - Notifikasi Screenshot: `"Tangkapan Layar Diambil"` (ID) vs `"Screenshot Captured"` (EN).
    - Notifikasi Teks: `"Teks Disalin"` (ID) vs `"Text Copied"` (EN).
    - Navbar Tab: `"Papan Klip"` (ID) vs `"Clipboard"` (EN).
- **Tipografi HD & ClearType Display (Keputusan 73)**:
  - Menggunakan font resmi `Segoe UI Variable Text, Segoe UI, sans-serif` dengan `TextOptions.TextFormattingMode="Display"` dan `TextOptions.TextRenderingMode="ClearType"`.
  - Cuplikan teks dan angka karakter tampil tajam, hitam pekat, berdefinisi tinggi, dan bebas blur.
- **Empty State Transparan**:
  - Saat salah satu kategori kosong (misalnya tangkapan layar kosong atau teks kosong), tampilan placeholder (`No screenshots yet` / `No copied text yet`) kini transparan penuh tanpa kotak border maupun background kartu, sehingga tampak bersih dan menyatu dengan ruang notch.
- **Notifikasi Notch Otomatis (Live Clipboard Notification)**:
  - Setiap kali ada tangkapan layar baru yang diambil (Snipping Tool / Win+Shift+S / PrtScn), notch langsung memunculkan notifikasi animasi dengan ikon `📸`, judul `Notification.ScreenshotCaptured`, dan resolusi gambar (`W × H px`).
  - Setiap kali ada teks baru yang disalin di Windows (Ctrl+C / Copy), notch memunculkan notifikasi animasi dengan ikon `📋`, judul `Notification.TextCopied`, dan cuplikan teks yang disalin.
  - Jika notch sedang dalam kondisi idle/collapsed, pil notch akan mengembang secara dinamis. Jika notch sedang dibuka/expanded, notifikasi meluncur sebagai *Sub-Notch Pill* di bawah badan notch.
- **Scroll Tanpa Pindah Slide (Smart Scroll)**:
  - Scroll wheel mouse yang diarahkan ke daftar teks vertikal atau galeri gambar akan menggulir (*scroll*) konten clipboard tersebut secara independen tanpa memicu perpindahan slide notch.
- **Pencegahan Duplikasi Screenshot**: Memeriksa hash byte atau dimensi gambar terakhir dalam rentang 1 detik untuk memastikan satu aksi screenshot dari Snipping Tool tidak tersimpan ganda (2 kali).
- **Optimasi Memori Thumbnail (`DecodePixelWidth = 160`)**:
  - Pada `ClipboardItem.cs`, pembuatan `Thumbnail` untuk item tangkapan layar dibatasi dengan lebar decode 160 piksel. Tangkapan layar beresolusi 4K (yang dapat berukuran 33 MB jika didekode utuh) hanya didekode menjadi thumbnail ringan (~100 KB), menghemat memori secara signifikan saat riwayat clipboard menampung banyak gambar.

### 3. Aksi pada Kartu Clipboard
- **Salin Kembali**: Mengklik kartu item langsung menyalin teks atau gambar kembali ke clipboard sistem dan menampilkan toast notifikasi.
- **Sematkan (Pin)**: Item yang disematkan memiliki ikon pin aktif dan tidak akan terhapus saat tombol "Bersihkan Semua" ditekan.
- **Hapus Item**: Tombol tempat sampah kecil pada sudut kanan kartu untuk menghapus item individual.
- **Bersihkan Riwayat**: Mengosongkan seluruh riwayat clipboard yang tidak disematkan.

---

## 🚫 Panduan Modifikasi Aman (Do's & Don'ts)
- **JANGAN** memanggil `Clipboard.GetText()` atau `Clipboard.GetImage()` langsung dari thread non-STA; selalu bungkus dalam dispatcher atau thread apartemen tunggal.
- **JANGAN** menghapus pemeriksaan deduplikasi gambar saat menangani pesan `WM_CLIPBOARDUPDATE` agar tangkapan layar tidak tercatat dobel.
- **JANGAN** menggunakan hardcoded string toast bahasa Inggris; selalu gunakan `LocalizationService.Instance.Get("Notification.ClipboardCopied")`.
