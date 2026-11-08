# 📁 Modul 09: Shelf File & Dock Pintasan

> Indeks seluruh dokumentasi: [README.md](README.md)

Modul ini menyediakan tempat penampungan file sementara (Shelf) untuk mempermudah transfer file antar-folder/aplikasi, arsitektur anti-duplikasi file, serta dock pintasan peluncur aplikasi favorit.

---

## 📂 File Terkait
- **View XAML**:
  - [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml#L7329-L8150) (`<!-- SLIDE 8: SHELF FILE & DOCK -->`)
  - [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml#L8040-L8150) (Drag-and-Drop Overlay Grid & Targets)
- **View Code-Behind**:
  - [NotchWindow.xaml.cs](../NotchWin/Views/NotchWindow.xaml.cs#L20205-L21750) (Drag-and-drop event handlers, debouncing, & drop guards)
- **ViewModels**:
  - [ShelfViewModel.cs](../NotchWin/ViewModels/ShelfViewModel.cs)
  - [ShortcutViewModel.cs](../NotchWin/ViewModels/ShortcutViewModel.cs)
- **Services & Models**:
  - [ShelfService.cs](../NotchWin/Services/ShelfService.cs)
  - [ShortcutService.cs](../NotchWin/Services/ShortcutService.cs)
  - [ImageConvertService.cs](../NotchWin/Services/ImageConvertService.cs) (konversi JPG↔PNG via drop overlay)
  - [ShelfItem.cs](../NotchWin/Models/ShelfItem.cs)
  - [ShelfState.cs](../NotchWin/Models/ShelfState.cs) (skema JSON persistensi shelf)
  - [ShortcutItem.cs](../NotchWin/Models/ShortcutItem.cs)
- **Modul terkait**: tombol & panel **Phone Share** tinggal di slide Shelf — spesifikasi lengkap di [14-phone-share.md](14-phone-share.md).

---

## 📦 Komponen & Alur Kerja

### 1. Shelf (Penampungan File Sementara)
- **Konsep Penggunaan**: Mirip konsep Shelf di macOS (Yoink/Dropover). Pengguna men-drag file dari folder A ke notch, file tersimpan di shelf sementara, kemudian user berpindah ke folder B atau jendela browser dan men-drag file tersebut keluar dari shelf.
- **Ekstraksi Ikon & Ukuran**: `ShelfService` mengekstrak icon native file dan menghitung ukuran file (KB/MB).
- **Aksi Cepat**:
  - Drag-out: Mengklik dan menyeret file dari item shelf memulai operasi drag Win32 `DragDrop.DoDragDrop`. Bila opsi **Hapus setelah dikeluarkan (ShelfRemoveAfterDrag)** aktif di Pengaturan dan efek drag bukan `None` serta file tidak dijatuhkan di dalam notch (`!_shelfDropInsideNotch`), item otomatis hilang dari shelf setelah berhasil diseret keluar.
  - Klik ganda: Membuka file dengan program default Windows.
  - Hapus item: Menghapus item dari daftar penampungan tanpa menghapus file asli di hard disk. Menggunakan ikon silang vektor (`Path`) tajam anti-mojibake.
- **Persistensi Lintas Restart (`shelf.json`)**:
  - Seluruh isi shelf disimpan di `%APPDATA%\NotchWin\shelf.json` (`IShelfService` memanggil `SaveShelf()` setiap kali item bertambah/berpindah/hilang; skema `ShelfState { Version, List<ShelfEntryRecord> }` di `Models/ShelfState.cs` menyimpan `Id`, `Path`, `AddedAt`, `IsDirectory`).
  - `LoadShelf()` dijalankan saat konstruksi service: entri dimuat urut turun-menurut `AddedAt` (terbaru di indeks 0, sama seperti drop baru).
  - Berkas korup tidak dibuang diam-diam — dipindah ke `<FilePath>.bad` lalu shelf dimulai kosong; penulisan memakai pola temp + replace agar tidak setengah tulis saat crash.
- **Status Item Hilang (`IsMissing`)**:
  - `ShelfItem` kini `ObservableObject` dengan properti `IsMissing` + `StatusText`. `ShelfService.RefreshMissingStates()` memeriksa keberadaan file fisik dan menandai item yang dipindah/dihapus dari luar (dipanggil saat shelf dibuka, setelah load, dan saat bahasa berganti).
  - Item `IsMissing` tidak dapat di-double-click/drag-out dan tidak muncul di daftar unduhan Phone Share; kartu tampil redup dengan teks status lokal.
- **Judul & Teks Terlokalisasi**: `ItemsCountText` memakai kunci `Shelf.EmptyCount` / `Shelf.OneItem` / `Shelf.ItemsCount` (parameter jumlah), judul dialog pilih file `Shelf.SelectFilesTitle`, dan di-refresh via event `LanguageChanged` tanpa restart.
- **Kolom Kanan Kartu Shelf (lebar 144 DIPs, Anti-Clipping & Tipografi Tajam)**:
  - **Baris Atas Terpadu (Tinggi 26 px)**:
    - Tombol **`Phone`** (`ShowPhoneShareCommand`, tooltip `Shelf.PhoneTooltip`, titik hijau saat server Phone Share berjalan — tampil hanya bila `IsPhoneShareEnabled`, latar dan border aksen menyala saat panel QR terbuka).
    - Tombol **Tambah Berkas `+`** (`Shelf.PickFilesCommand`, tooltip `Shelf.AddButton`, ukuran 26×26 px).
    - Tombol **Bersihkan Semua `🗑`** (`Shelf.ClearAllCommand`, tooltip `Shelf.ClearButton`, ukuran 26×26 px, otomatis disabled/redup saat shelf kosong).
  - **Panel QR Phone Share di Bawah Tombol**: Saat panel dibuka (`PhoneShare.IsPanelOpen`), kartu QR barcode kompak berlatar `#12121E` (QR 70×70 px, petunjuk scan tajam font 10 px `E4E4E7`, link URL font 9.5 px `#38BDF8`, tombol Salin Tautan & Stop font 9.5 px, dan status) muncul di bawah baris tombol aksi. Dilengkapi `RenderOptions.ClearTypeHint="Enabled"`, `TextOptions.TextFormattingMode="Display"`, dan font `Segoe UI Variable Text` sehingga 100% tajam dan bebas blur. Dengan tinggi total baris tombol (26 px) + panel (160 px) = 186 px, seluruh elemen muat sempurna di dalam batas notch tanpa ada yang terpotong. Perilaku Phone Share lengkap ada di [14-phone-share.md](14-phone-share.md).
- **Animasi Drop "Ledakan"**: Saat file dilepas ke shelf, `PlayShelfDropEffects` memainkan burst partikel sekali-pakai di `ShelfBurstCanvas` (overlay `Canvas` di dalam kartu items, `IsHitTestVisible=False`, `Panel.ZIndex=-1` sehingga efek berada **di belakang konten kartu item**): ring shockwave 480 ms (skala 0.4 → 5.2, diameter akhir ±156 px, fade `QuadraticEase EaseIn`) + 22 partikel radial (campuran `AccentLightBrush` dan spark putih, arah acak dengan bias ke atas, 420–700 ms, easing `QuarticEase`) yang menghapus dirinya setelah selesai, plus pop `ScaleTransform` 0.55 → 1.08 → 1.0 (300 ms) pada setiap container item baru lewat `ShelfItemsControl.ItemContainerGenerator`. Kartu item memakai `Background="Transparent"` (bukan `#161624` opak) agar ledakan terlihat menembus area kartu; border `#28283E`, ikon, nama file, dan tombol tetap tampil, dan area transparan tetap menerima hit-test untuk drag-out.
  - **Posisi & jumlah ledakan**: setiap file yang **baru ditambahkan** mendapat satu ledakan di belakang kartunya sendiri, berurutan kiri→kanan dengan stagger 90 ms. Item terbaru ada di indeks 0 (paling kiri); saat beberapa file dijatuhkan sekaligus, semuanya (maksimal 6 kartu) ikut meledak, bukan hanya kartu pertama. Jumlah file yang benar-benar masuk dikembalikan oleh `ShelfService.AddFiles`/`ShelfViewModel.AddFiles` (`int`) dan diteruskan ke `PlayShelfDropEffects(addedCount, ...)`, sehingga path yang dilewati (file tidak valid/duplikat) tidak ikut dihitung. Posisi tiap kartu dihitung via `TransformToAncestor` ke parent grid bersama `ShelfBurstCanvas` lalu dikurangi origin canvas (canvas bukan ancestor dari container item); jika container belum ter-generate, dicoba ulang hingga 3× dengan jeda 70 ms sebelum fallback ke tengah canvas.
  - **Waktu**: drop langsung di kartu dijadwalkan pada `DispatcherPriority.Loaded`; drop lewat overlay target (yang menavigasi ke slide Shelf) memakai jeda 320 ms agar slide sudah terlihat. Stagger antar kartu memakai `BeginTime` pada animasi (bukan timer tambahan). Tidak ada animasi `Forever` — efek hanya hidup selama ±1 dtk dan nol biaya saat idle.
  - Catatan implementasi: animasi `Canvas` tidak bisa ditaruh sebelum `<Grid.RowDefinitions>` (properti harus mendahului konten), jadi canvas berada setelah `ScrollViewer` dengan `Grid.RowSpan="2"`; untuk drop pertama (shelf kosong → berisi) efek dijadwalkan pada `DispatcherPriority.Loaded` karena border items baru berubah `Visible` setelah binding `Shelf.HasItems` diperbarui.

---

### 🛡️ Arsitektur Anti-Duplikasi File Shelf (Keputusan 76)
Pengguna sesekali mengalami masalah di mana 1 file yang dijatuhkan (drag-and-drop) tiba-tiba terduplikasi menjadi 2 item di Shelf. Masalah ini diselesaikan melalui 4 lapis penjagaan (*multi-layer deduplication guard*):

1. **Normalisasi Path Kanonikal (`ShelfService.NormalizePath` & `IsPathEqual`)**:
   - Seluruh path berkas dibersihkan dari spasi, tanda kutip ganda (`"`) dan tunggal (`'`).
   - Dikonversi ke path absolut penuh kanonikal melalui `Path.GetFullPath(trimmed)`.
   - Trailing directory separator (`\` dan `/`) dipangkas secara konsisten, sehingga variasi path seperti `C:\Folder\` dan `C:\Folder` dijamin identik.
   - Pengecekan kesamaan path menggunakan `IsPathEqual(a, b)` dengan `StringComparison.OrdinalIgnoreCase`.

2. **Deduplikasi Batch Input (`HashSet`)**:
   - Sebelum memproses daftar berkas dari payload `DataFormats.FileDrop`, seluruh path difilter menggunakan `HashSet<string>(StringComparer.OrdinalIgnoreCase)`.
   - Mencegah payload yang mengandung path ganda dari aplikasi sumber luar.

3. **Debounce Waktu & Kunci Sinkronisasi (`_recentlyAdded` & `lock (_lock)`)**:
   - `ShelfService` memelihara kamus `_recentlyAdded` (`Dictionary<string, DateTime>`) yang dilindungi thread-safe lock `_lock`.
   - Jika path kanonikal yang sama masuk kembali dalam kurun waktu **kurang dari 600 milidetik**, panggilan drop kedua langsung diabaikan (*debounced*).
   - Kamus secara otomatis membersihkan entri kadaluwarsa (> 2000 ms) agar konsumsi memori tetap minimal.
   - **Pembersihan Proaktif Sebelum Penyisipan**: Sebelum menyisipkan item baru di indeks 0 (`Items.Insert(0, item)`), seluruh item yang sudah ada di `Items` yang memiliki kesamaan path kanonikal (`IsPathEqual`) dihapus terlebih dahulu (`Items.Remove(existing)`). Hal ini menjamin tidak ada duplikasi akibat jeda async pembuatan thumbnail Shell API (`SHGetFileInfo`).

4. **Penjagaan Event Drop Ganda pada Level UI (`if (e.Handled) return;`)**:
   - Elemen jendela induk `<Window>` memiliki tunneling event `PreviewDrop="Window_PreviewDrop"`.
   - Elemen anak memiliki bubbling event `Drop`: `DropTargetShelf_Drop`, `ShelfDropZone_Drop`, `DropTargetDock_Drop`, dan `DockBorder_Drop`.
   - Seluruh handler event drop tersebut kini diawali dengan penjagaan tegas:
     ```csharp
     if (e.Handled) return;
     e.Handled = true;
     ```
   - Ketika event drop telah diproses di level jendela (`Window_PreviewDrop`), event ditandai `e.Handled = true`, sehingga handler anak di bawahnya tidak akan pernah memproses berkas yang sama untuk kedua kalinya.

---

### 2. Dock Pintasan Aplikasi (Terkonsolidasi Eksklusif di Menu Home)
- **Penempatan Eksklusif**: Sejak pembaruan antarmuka Home, Dock Pintasan (`ShortcutDockBorder`) dipindahkan dan ditempatkan **secara eksklusif di baris 1 Menu Home (`SlideHomeCard`)**, tepat di bawah Miniplayer & Kalender Mingguan. Dock dihilangkan dari menu-menu slide lainnya agar konten masing-masing tab tetap fokus, bersih, dan lapang.
- Menampilkan ikon aplikasi favorit yang disematkan user.
- Mengklik ikon meluncurkan proses aplikasi (`Process.Start`).
- Mengklik tombol `+` membuka dialog pemilihan file `.exe` atau shortcut `.lnk` untuk ditambahkan ke dock. Drag-and-drop file atau shortcut langsung ke kontainer dock juga otomatis menambahkan pintasan baru.
- **Tampilan Dock**:
  - Background container semi-transparan modern dengan sudut membulat penuh (`Width="584"`, `Height="44"`, `HorizontalAlignment="Center"`, `CornerRadius="16"`, `Background="#161622"`, `BorderBrush="#252538"`, `BorderThickness="1"`). Desain floating dock ini menyatu harmonis dengan kontainer Miniplayer dan Kalender di atasnya (jarak pemisah 10 DIPs).
  - Padding internal simetris (tombol `+` di sisi kiri memiliki `Margin="8,0,0,0"`, dan `ScrollViewer` ikon di sisi kanan memiliki `Margin="6,0,8,0"`) memberikan ruang bernapas yang proporsional di dalam lengkungan sudut dock.
  - Menu konteks klik kanan pada ikon: "Buka / Jalankan" dan "Hapus dari Dock".
  - Data pintasan disimpan secara persisten di `settings.json` (`AppSettings.Shortcuts`).
  - Ikon aplikasi diperbesar mendekati ukuran taskbar Windows (`26×26` px dengan rendering `BitmapScalingMode="HighQuality"`), item button berukuran `36×34` px dengan rounded corners. Seluruh lencana penanda tipe (badge panah shortcut overlay) ditiadakan, dan target berkas `.lnk` di-resolve langsung ke executable (`.exe`) aslinya agar ikon tampil murni, bersih, dan elegan tanpa watermark panah pintasan Windows.
  - **Pemberantasan Pemotongan Dock (*Anti-Clipping*)**: Ukuran `ExpandedHeight` notch ditetapkan menjadi `264.0 DIPs`. Tinggi total konten Home (kartu atas 120 DIPs + gap 10 DIPs + dock 44 DIPs = 174 DIPs) ditambah margin dan baris navigasi menghasilkan ruang lega vertikal 20 DIPs di atas batas bawah jendela notch. Hal ini menjamin border dan bayangan dock tampil utuh 100% tanpa terpotong oleh lengkungan sudut luar notch.
- **Baris Navigasi di Atas Konten**: `NavDockHost` menempati `Grid.Row="1"` (tinggi 24 px + margin bawah 10 px) di bawah header dan di atas konten slide, sementara `ShortcutDockBorder` tetap di `Grid.Row="3"`. Setiap tombol selalu tampil sebagai ikon + label (navbar tidak menciut menjadi titik); pill aktif mengikuti tab yang dipilih, dan tab Shelf menampilkan badge biru berisi jumlah file selama shelf tidak kosong. Detail perilaku ada di [00-arsitektur-dan-notch-core.md](00-arsitektur-dan-notch-core.md) bagian "Sistem Navigasi Navbar Bawah".

---

### 3. Drag-and-Drop Overlay Anti-Glitch
- Saat pengguna men-drag file dari luar mendekati area notch, overlay target muncul:
  - Kotak 1: **Simpan di Shelf** (`DropTargetShelf`).
  - Kotak 2: **Tambahkan ke Dock** (`DropTargetDock`).
  - Kotak 3: **Konversi Gambar** (`DropTargetConvert`, judul lokal `DropAction.ConvertTitle`, badge hijau emerald `#122A24`): menjalankan `ImageConvertService.ConvertFiles` pada file yang dijatuhkan dan menampilkan hasil via `ShowConvertResultToast` (ikon `🔄`, pesan sukses `Convert.SuccessTitle`/`Convert.SuccessMessage` dengan nama berkas keluaran terakhir, gagal `Convert.FailedTitle`, non-gambar `Convert.NothingTitle`). Handler di level jendela (`Window_PreviewDrop`) juga memeriksa hit-test area `DropTargetConvert` lebih dulu sebelum target lain.
- **Perilaku `ImageConvertService`**:
  - Menerima `.png .jpg .jpeg .bmp .gif`; berkas lain / direktori di-*skip* (dihitung sebagai `Skipped`).
  - JPG/JPEG → PNG; PNG/BMP/GIF → JPG kualitas 92 dengan **transparansi/diratakan ke latar putih** (`FlattenToWhite` via `DrawingVisual` + `RenderTargetBitmap`) agar tidak rusak jadi hitam.
  - Hanya frame pertama GIF yang dikonversi. Output ditulis di folder sumber dengan sufiks `-converted` (`nama-converted.png`, bentrok → `nama-converted-1.png`, dst) — **file asli tidak diubah sama sekali**.
  - Service stateless (di-`new` inline di handler drop), tidak menyimpan konfigurasi format tujuan.
- **Debouncing & Hit-Testing**:
  - Seluruh elemen anak dalam drop target memiliki `IsHitTestVisible="False"` agar kursor tidak berkedip (*glitch*) saat melintasi teks atau ikon di dalam drop target.
  - Variabel `_currentDropHighlight` di code-behind mengunci status hover drop target agar border highlight tidak berkedip cepat.
- **Anti-Mojibake Karakter Target**:
  - Karakter roket dan baki unduhan menggunakan XML entities terstandarisasi (`&#x1F680;` dan `&#x1F4E5;`) untuk mencegah distorsi encoding font.

---

## 🚫 Panduan Modifikasi Aman (Do's & Don'ts)
- **JANGAN** mengaktifkan `IsHitTestVisible="True"` pada elemen anak di dalam `DropTargetShelf` atau `DropTargetDock` karena akan memicu rentetan event `DragLeave`/`DragEnter` yang menyebabkan UI berkedip.
- **JANGAN** menghapus operasi pembacaan `DataFormats.FileDrop` pada event drop handler.
- **JANGAN** menghapus guard `if (e.Handled) return;` dan penandaan `e.Handled = true;` pada handler drop manapun, agar tidak terjadi eksekusi dobel dari bubbling event WPF.
- **JANGAN** membandingkan `FilePath` secara langsung tanpa melewati `IsPathEqual()` atau `NormalizePath()` karena perbedaan trailing slash dan format direktori dapat menimbulkan duplikasi item di Shelf.
