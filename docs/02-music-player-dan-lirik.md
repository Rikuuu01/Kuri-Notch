# 🎵 Modul 02: Pemutar Musik & Lirik Sinkron

> Indeks seluruh dokumentasi: [README.md](README.md)

Modul ini menangani integrasi pemutar musik sistem Windows (GSMTC), lirik lagu karaoke sinkron real-time (lrclib), dan pewarnaan aksen dinamis dari cover album lagu.

---

## 📂 File Terkait
- **View XAML**:
  - [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml#L2873-L3570) (`<!-- SLIDE 1: SEDANG MEMUTAR (MUSIC PLAYER) -->`)
  - [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml#L590-L634) (Mini-Player & Lirik saat Collapsed)
- **View Code-Behind**:
  - [NotchWindow.xaml.cs](../NotchWin/Views/NotchWindow.xaml.cs#L845-L955) (Animasi scroll lirik & penanganan lirik collapsed)
  - [NotchWindow.xaml.cs](../NotchWin/Views/NotchWindow.xaml.cs#L7480-L7760) (`AnimateLyricsExpansion`)
- **ViewModels**:
  - [MediaViewModel.cs](../NotchWin/ViewModels/MediaViewModel.cs)
  - [LyricsLineViewModel.cs](../NotchWin/ViewModels/LyricsLineViewModel.cs)
  - [LyricsCandidateViewModel.cs](../NotchWin/ViewModels/LyricsCandidateViewModel.cs) (baris popup pemilih versi)
  - [NotchViewModel.cs](../NotchWin/ViewModels/NotchViewModel.cs)
- **Services & Models**:
  - [MediaService.cs](../NotchWin/Services/MediaService.cs) (GSMTC WinRT listener)
  - [LyricsService.cs](../NotchWin/Services/LyricsService.cs) (lrclib HTTP client, LRC parser & candidate aggregator)
  - [ITtmlLyricsService.cs](../NotchWin/Services/ITtmlLyricsService.cs) (Kontrak TTML syllable provider)
  - [TtmlLyricsService.cs](../NotchWin/Services/TtmlLyricsService.cs) (Apple Music TTML XML parser & syllable timing extractor)
  - [AlbumColorExtractor.cs](../NotchWin/Services/AlbumColorExtractor.cs) (Color quantization album art)
  - [LyricsModel.cs](../NotchWin/Models/LyricsModel.cs)
- **Converters**:
  - [KaraokeClipConverter.cs](../NotchWin/Converters/KaraokeClipConverter.cs) (`IMultiValueConverter` penghasil `RectangleGeometry` clip progres karaoke — dipakai pill collapsed & panel expanded)

---

## 🎶 Komponen & Alur Kerja

### 1. Integrasi Media Windows (GSMTC)
- Menangkap sesi aktif dari Spotify, YouTube di browser (Chrome, Edge, Firefox), Apple Music, VLC, dsb. melalui `GlobalSystemMediaTransportControlsSessionManager`.
- Melacak perubahan lagu (`MediaTrackChanged`), status putar (`PlaybackStateChanged`), durasi, dan posisi seek.
- **Sinkronisasi Posisi Playback SMTC Saat Startup & Eliminasi Latensi Decode (`LastUpdatedTime`)**:
  - GSMTC Windows menyimpan timestamp `LastUpdatedTime` saat pemutar media memperbarui posisi terakhir. Ketika lagu sudah berjalan di tengah dan aplikasi NotchWin baru dijalankan (*startup*), posisi awal dihitung dinamis dengan formula: `Position + (DateTimeOffset.UtcNow - LastUpdatedTime)` saat status `Playing`.
  - **Re-Anchoring Realtime Sebelum `ApplyTrackInfo`**: Pembacaan stream thumbnail cover album secara asinkron (`OpenReadAsync` & decode bitmap) membutuhkan 50–250ms. Tepat sebelum memancarkan event `ApplyTrackInfo(newTrack)`, posisi `newTrack.Position` dihitung ulang dari selisih waktu `LastUpdatedTime` saat itu juga, sehingga posisi audio yang sampai ke ViewModel terjamin akurat hingga tingkat milidetik tanpa terdistorsi jeda I/O gambar.
  - **Anti-Jitter SMTC Drift (Ambang 1000ms)**: Durante playback normal berkelanjutan, pemutar media seperti Spotify sesekali mengirim sinyal SMTC dengan fluktuasi latensi threading 100–300ms. Nilai toleransi di [MediaViewModel.cs](../NotchWin/ViewModels/MediaViewModel.cs) diset ke `> 1000ms`. Hal ini mencegah stopwatch internal melompat mendadak (*abrupt snap*) saat pemutaran stabil, sementara lompatan nyata dari seek manual pengguna tetap terdeteksi secara instan.
- **Interval Timer Presisi Tinggi (100ms Expanded / 150ms Collapsed)**:
  - Di [MediaViewModel.cs](../NotchWin/ViewModels/MediaViewModel.cs), timer timeline pada mode collapsed notch dioptimasi ke interval **150ms** (dari sebelumnya 500ms). Pergantian baris lirik mini pada pill notch kini berlangsung secara instan dan responsif sesuai ketukan musik tanpa menambah beban CPU.
- **Transisi Otomatis Notch Tanpa Media Saat Musik Berhenti (3 Detik)**:
  - Ketika lagu di-pause atau berhenti selama ≥ 3 detik, pill notch dalam status collapsed secara otomatis beralih kembali ke mode tanpa media (`WidgetType.Clock`) dengan ukuran ringkas (250px).
  - **Data Pemutar Musik Tetap Ada di Dalam Notch**: Seluruh metadata lagu (`Title`, `Artist`, `Thumbnail`, seekbar `Position`, dan lirik) **tetap dipertahankan di dalam memori**. Saat notch di-expand ke Slide 1 (Sedang Memutar), tampilan pemutar musik tetap utuh dan siap diputar kembali.
  - Saat musik kembali diputar (`IsPlaying = true`), notch collapsed langsung kembali menampilkan mini-player media secara instan.
- Menampilkan visualizer gelombang audio (`MusicVisualizer`) yang bergerak saat musik aktif berputar.

### 2. Lirik Lagu Sinkron (Karaoke Real-Time)
- **Sumber Lirik**: Diambil otomatis dari API `lrclib.net` berdasarkan judul, artis, dan durasi audio.
- **Pencarian Kandidat Cerdas (`ScoreCandidate`)**:
  - `LyricsService.cs` memfilter hasil pencarian API menggunakan algoritma skoring:
    - Memberikan bobot tinggi (+1000) untuk hasil yang memiliki lirik tersinkronisasi (`syncedLyrics`).
    - Penalti berat (-500) untuk versi *live*, *remix*, atau *acoustic* jika lagu yang sedang diputar adalah versi studio.
    - Penalti selisih durasi audio (`|candidateDuration - trackDuration| * 10`) untuk memastikan kesesuaian tempo.
- **Parser LRC Universal**:
  - Menggunakan regex `TimestampRegex = @"\[(\d{1,2}):(\d{2})(?:[.:](\d{1,3}))?\]"` yang mendukung separator desimal titik (`.`) maupun titik dua (`:`), serta mengenali multiple timestamp per baris (misalnya `[00:12.34][00:45.67]Lirik yang sama`).
- **Durasi Alami Baris Lirik & Eliminasi Pemotongan Prematur**:
  - Sesuai standar LRC, setiap baris lirik tetap aktif dan ditampilkan secara penuh sejak timestamp-nya berbunyi hingga timestamp baris berikutnya tiba.
  - Aturan lama yang memotong lirik aktif setelah 3.5 detik pada jeda $\ge 7.5$ detik **telah dihapus**. Hal ini memastikan kalimat panjang atau lagu bertempo lambat (misalnya *ballad*) tidak terpotong sebelum vokalis selesai bernyanyi.
- **Ikon Interlude/Intro Statis (`♫`)**:
  - Untuk bagian intro awal lagu sebelum vokal pertama (jeda $\ge 5$ detik) atau bait instrumen kosong yang ditandai oleh pembuat LRC, notch menampilkan ikon musik statis `♫` tanpa animasi hitung mundur titik tiga (`•••`), sehingga tampilan tetap tenang dan elegan.
- **Auto-Scroll & Klik Lirik**:
  - Canvas lirik bergerak otomatis vertikal via `LyricsTranslateTransform` agar baris yang aktif selalu berada di tengah.
  - Pengguna dapat mengklik baris lirik mana pun untuk melompatkan (seek) audio ke menit/detik tersebut (`SeekToLyricLineAsync`).
- **Penyimpanan Offset Per-Lagu Persisten (`SongOffsets.json`)**:
  - Pengguna dapat mengatur offset lirik per 100ms menggunakan tombol kalibrasi.
  - Setiap perubahan offset otomatis disimpan secara persisten ke `%AppData%\NotchWin\SongOffsets.json` berdasarkan kunci `Artist:::Title`. Saat lagu yang sama diputar kembali di kemudian hari, kalibrasi lirik langsung diterapkan secara instan.
- **Badge Sumber & Tipe Lirik (`LyricsSourceInfo`)**:
  - API lrclib mengembalikan dua varian lirik per lagu: `syncedLyrics` (ber-tag waktu `[mm:ss.xx]`, untuk karaoke sinkron) dan `plainLyrics` (teks statis tanpa waktu). Sistem selalu memprioritaskan yang tersinkron (skor +1000 vs +50 pada `ScoreCandidate`).
  - Panel lirik menampilkan indikator teks kecil tanpa background di **pojok kanan-bawah** yang menandai sumber dan varian yang akhirnya dipakai: `lrclib.net · SYNCED`, `lrclib.net · PLAIN`, atau `lrclib.net · INSTRUMENTAL`.
  - Properti `LyricsSourceInfo` / `HasLyricsSourceInfo` di [MediaViewModel.cs](../NotchWin/ViewModels/MediaViewModel.cs) diset dari flag `LyricsResult.IsSynced` / `IsInstrumental` hasil `ParseLrcLibDto()`, dan otomatis dibersihkan saat ganti lagu, lirik tidak ditemukan, atau layanan tidak tersedia.
  - Kegunaan utama: saat lirik tampak tidak bergerak sinkron, badge langsung menjelaskan bahwa hanya versi PLAIN yang tersedia di database lrclib untuk lagu tersebut.
- **Tampilan Karaoke 1-Baris Compact di Notch Collapsed (Double-Slot Slide/Fade)**:
  - Notch tertutup menampilkan **satu baris lirik aktif** (bukan carousel 3 baris) di dalam pill, dengan teks terpusat, `TextTrimming="CharacterEllipsis"`, dan transisi antar-baris berupa **slide vertikal + fade**: slot A/B (`CollapsedLyricSlotA/B`) bergantian muncul lewat `TranslateTransform` (Y = 16 → 0) yang dibarengi animasi `Opacity`, sehingga baris lama naik-memudar dan baris baru masuk dari bawah.
  - **Isian karaoke progressif (word-mask ala Dynamic Island)**: saat `Media.IsCollapsedKaraokeActive` (`IsCollapsedLyricsActive && LyricsKaraokeEnabled && HasLyrics && !IsCurrentLyricsUntimed`), teks dasar berwarna abu-abu `#94949E` dan overlay putih identik di-clip `RectangleGeometry` selebar `Media.ActiveLyricProgress` (0..1 fraksi durasi baris) via `KaraokeClipConverter` — bagian yang sudah "dinyanyikan" menjadi putih. Timer diperbarui dengan frekuensi **60 FPS (16 ms)** pada prioritas `DispatcherPriority.Render`, sehingga sapuan teks lirik berjalan sangat halus tanpa efek patah-patah (*stuttering*). Saat karaoke mati / lirik plain (untimed) / nonaktif, clip bernilai 0 dan teks tampil putih normal tanpa isian.
  - Binding converter menerima 6 nilai: `ActualWidth`, `ActualHeight`, `IsCollapsedKaraokeActive`, `ActiveLyricProgress`, `LyricsKaraokeEnabled`, `IsCurrentLyricsUntimed`; geometry selalu di-`Freeze()` (dibuat baru per update, tanpa mutasi Freezable).
  - Mekanika yang sama dipakai baris aktif panel expanded (MULTIBINDING `KaraokeClipConverter` dengan `DataContext.Media.*`), sehingga pill collapsed dan panel besar menampilkan isian karaoke yang identik.
- **Pengaturan Tampilan Lirik (Pengaturan → Musik)**:
  - **Karaoke Fill** (`AppSettings.IsLyricsKaraokeEnabled`, default `true`, kunci UI `Settings.LyricsKaraokeTitle/Desc`): menyalakan/mematikan efek isian progres pada pill collapsed dan baris aktif panel; tersalurkan ke `MediaViewModel.LyricsKaraokeEnabled`.
  - **Bold Lyrics** (`AppSettings.LyricsBold`, default `true`): baris aktif memakai `FontWeights.Bold` atau `Normal`.
  - **Text Size** (`AppSettings.LyricsTextSize`, slider `10–14`, default `12.0`): ukuran baris non-aktif; baris aktif = `base + 2.5`. Diterapkan lewat `LyricsLineViewModel.Configure(baseFontSize, useBold)` yang di-clamp `Math.Clamp(base, 10, 14)` dan langsung menyegarkan seluruh baris yang tampil ( collapsed memakai FontSize 12 SemiBold tetap sesuai desain pill).
  - Semua nilai di auto-save ke `settings.json` dan langsung berlaku tanpa restart.

### 3. Pemilih Versi Lirik (Lyrics Version Picker)
- **Vektorisasi Glif Dropdown Lirik (Keputusan 75)**: Seluruh glif teks pada tombol pemilih versi lirik (seperti panah dropdown `▾` dan tanda centang kandidat aktif `✓`) digantikan dengan elemen vektor native `Path` WPF untuk mengeliminasi total kesalahan encoding karakter / mojibake.
- **Latar Belakang**: lrclib sering menyimpan banyak entri untuk satu judul lagu (versi studio, live, remix, album berbeda). Sebelumnya `LyricsService` hanya mengembalikan satu kandidat terbaik sehingga user tidak punya cara mengganti bila pilihan otomatis salah.
- **Pengumpulan Semua Kandidat (`GetLyricsCandidatesAsync`)**:
  - `/api/get` exact dan search #1 (`track_name` + `artist_name`) dijalankan **paralel** (`Task.WhenAll`) pada cache miss, sehingga daftar versi terisi tanpa menambah latensi.
  - Semua hasil dengan skor > 0 di-dedupe berdasarkan `LrcLibDto.Id` (fallback `TrackName|ArtistName|Duration`), diurutkan skor menurun (tie-break: synced dulu), dan dibatasi maksimal 20 entri.
  - `AutoCandidateId` memakai presedensi lama: exact synced/instrumental → kandidat search skor tertinggi → strategi search #2/#3 (bila perlu) → exact plain. Auto tidak dihitung ulang dari urutan skor agar pemilihan bawaan tidak berubah.
- **Cache Kandidat v4**: seluruh daftar kandidat disimpan di `%AppData%\NotchWin\LyricsCache\v4-{sha256}.json` (RAM maksimal 20 lagu). File cache versi lama (v1/v2/v3) otomatis ditinggalkan untuk menghindari struktur model kadaluarsa.
- **Chip `x/y ▾`, Badge Provider & Popup**: di pojok kanan-bawah panel lirik, di samping badge sumber, muncul chip kecil berisi posisi/pilihan (mis. `2/4`) yang hanya tampil bila kandidat > 1 (`HasLyricsCandidateChoices`). Klik membuka popup (`MinWidth="250" MaxWidth="330"`) berisi baris **Auto (terbaik)** dan daftar semua entri valid:
  - **Judul Lagu & Subtitle**: `TrackName` dan `ArtistName · AlbumName`.
  - **Lencana Provider**: Badge berlatar warna khusus yang membedakan asal lirik secara visual:
    - 🟥 **`Spicy Lyrics`**: Background `#33E11D48` dengan teks `#FDA4AF` untuk lirik presisi TTML suku kata.
    - ◻️ **`LRCLIB`**: Background `#1AFFFFFF` dengan teks `#94A3B8` untuk lirik baris standar LRCLIB.
  - **Durasi & Tipe**: Durasi (`m:ss`), tag tipe lirik (`SYLLABLE` beraksen ungu/biru, `SYNCED`, `PLAIN`, atau `INSTRUMENTAL`), serta checkmark vektor pada pilihan aktif. Daftar dibungkus `ScrollViewer` dengan gaya resmi [`ModernSlimScrollViewer`](STYLE-SCROLLBAR.md) (`MaxHeight="180"`).
- **Pilihan Persisten Per Lagu (`LyricsSelection.json`)**:
  - Memilih versi menyimpan pin `Dictionary<string, int>` di `%AppData%\NotchWin\LyricsSelection.json` dengan kunci `Artist:::Title` (pola sama seperti `SongOffsets.json`).
  - Saat lagu yang sama diputar lagi (termasuk setelah restart), pin dipakai otomatis. Memilih **Auto** menghapus pin.
  - Bila pin menunjuk entri yang tidak lagi ada di daftar API, sistem fallback ke Auto tanpa menghapus pin.
- **Ganti Versi Tanpa Network & Reset State**:
  - Memilih kandidat hanya menerapkan `LyricsResult` yang sudah di-cache: `ApplyLyricsResult()` selalu mereset `LyricsLines`, indeks baris aktif, teks Previous/Active/Upcoming, dan `_baseLrcOffsetMs` (agar offset bawaan versi synced lama tidak terbawa saat pindah ke versi plain).
  - Offset kalibrasi manual per lagu (`SongOffsets.json`) tetap berlaku lintas versi.
- **Guard UI**: popup ditutup saat memilih versi, menutup panel lirik, ganti lagu, panel lirik disembunyikan, dan notch collapse. `PillBorder_MouseLeave` tidak men-collapse notch selama popup terbuka; handler `Popup.Closed` mengevaluasi ulang kondisi kursor agar collapse tetap normal setelah klik luar atau memilih versi.
- **Popup di Atas Panel (Topmost per-HWND)**: window `Popup` WPF adalah HWND terpisah yang tidak ikut dipaksa topmost, sementara window notch di-`SetWindowPos(HWND_TOPMOST)` berkala oleh topmost keeper. Karena itu, saat `Opened` popup mengambil HWND-nya (`PresentationSource.FromVisual(popup.Child)`) dan memanggil `WindowStyleHelper.EnsureTopmost`, lalu keeper meng-assert ulang HWND popup setelah setiap assert notch selama popup terbuka (`_lyricsVersionPopupHwnd`). Tanpa ini, dropdown tertutup piksel opaque panel notch yang diperluas.

### 4. Logika Lirik Aktif vs Tidak Aktif
- **Tombol Toggle Lirik (`LyricsToggleButton`)**:
  - Menyalakan atau mematikan status lirik (`IsLyricsExpanded`).
  - Pilihan ini disimpan di `AppSettings.IsLyricsEnabled`.
- **Perilaku di Notch Mode Collapsed (`IsCollapsedLyricsActive`)**:
  ```csharp
  public bool IsCollapsedLyricsActive => IsLyricsExpanded 
      && HasLyrics 
      && LyricsLines.Count > 0 
      && !IsLoadingLyrics;
  ```
  - **Jika Lirik Tidak Aktif (`IsLyricsExpanded = false`)**: Notch saat mengecil hanya menampilkan **Judul Lagu & Nama Artis** dengan ukuran notch ringkas (`baseMediaW = 240px` + widget aktif).
  - **Jika Lirik Aktif (`IsLyricsExpanded = true`)**: Notch saat mengecil menampilkan **lirik sinkron berjalan** secara live. Lebar notch **tidak dikunci statis**, melainkan **dinamis adaptif**:
    $$\text{TargetWidth} = \text{dynamicChrome} + \text{panjang teks lirik} + 14\text{px}$$
    - `dynamicChrome` dasar: 104px (thumbnail lagu 20px, pembatas, jam, dan margin).
    - Jika Cuaca aktif di notch terlipat: +68px.
    - Jika Baterai aktif di notch terlipat: +68px.
    - Jika Pet / Mochi aktif di notch terlipat: +34px.
    - Panjang teks lirik (`mainWidth`): diukur real-time (`MeasureWidth`, minimal 100px).
    - Tambahan toleransi +14px memastikan teks tidak pernah terpotong elipsis (`...`) saat widget lain diaktifkan.
    - Lebar dibatasi maksimal hingga `MaxExpandedWidth` (620px).
  - **Proteksi Warna Album**: Ketika toggle widget dihidup-matikan atau setting disimpan, palet warna cover album lagu yang aktif selalu dipertahankan secara instan tanpa jeda atau kembali ke warna aksen default biru.

### 5. Animasi Geser Pemutar Musik saat Lirik Dibuka (`AnimateLyricsExpansion`)
- **Pergeseran ke Kiri**:
  - Ketika tombol lirik ditekan (`IsLyricsExpanded = true`), lebar `MediaHubGrid` dianimasikan dari 358px ke 584px (320ms, `CubicEase` `EaseOut`), dan lebar `MediaPlayerContainer` dianimasikan dari 358px ke 215px.
  - Karena `MediaHubGrid` memiliki `HorizontalAlignment="Center"` pada notch berlebar 620px, pembesaran lebar ini menggeser seluruh blok pemutar musik (cover album, judul lagu, artis, seekbar, dan kontrol pemutar) mulus ke sebelah kiri sejauh 113px.
  - Secara bersamaan, `LyricsContainer` bergeser masuk dari posisi `X = 60` ke `X = 0` dan bertransisi dari transparan (opacity 0) menjadi penuh (opacity 1).
- **Pergeseran Balik ke Tengah**:
  - Saat lirik ditutup (`IsLyricsExpanded = false`), `MediaHubGrid` dianimasikan kembali ke 358px dan `MediaPlayerContainer` kembali ke 358px (260ms), meluncurkan seluruh kartu pemutar musik kembali mulus ke tengah notch.
  - `LyricsContainer` memudar (opacity 0 dalam 180ms) dan bergeser ke `X = 60`, lalu disetel ke `Visibility.Collapsed`.

### 6. Ekstraksi Warna Cover Album Dinamis
- Jika fitur "Warna Aksen Dinamis Album" diaktifkan di Pengaturan:
  - `AlbumColorExtractor` memindai gambar thumbnail album, mencari palet warna paling dominan dan kontras.
  - Warna tersebut diterapkan ke resource `AccentLightBrush` dan gradasi notch secara otomatis via `ThemeManager`.
  - **Retensi Aksen Saat Media Jeda (Pause)**: Ketika lagu di-pause dan notch beralih ke posisi idle (jam / tanpa media), warna aksen dinamis dari cover album lagu **tetap dipertahankan** dan tidak di-reset ke warna default. Aksen hanya dikembalikan ke warna default apabila sama sekali tidak ada sesi media di sistem (`!Media.HasMedia`).
  - **Pencegahan Kedipan Warna Default Saat Perpindahan Sesi (Seamless Session Handoff)**:
    - Ketika suatu aplikasi media ditutup (misalnya Spotify di-close) sementara media lain sedang dalam status pause (misalnya YouTube di browser), `MediaService` secara cerdas mencari sesi fallback melalui `FindFallbackSession()` sebelum memancarkan `MediaTrackInfo.Empty`. Hal ini mencegah kekosongan data media secara instan.
    - Pada [MediaViewModel.cs](../NotchWin/ViewModels/MediaViewModel.cs), pemulihan warna default (`ThemeManager.RestoreDefaultAccent()`) diberi penundaan *debounce* 500ms (`_restoreAccentCts`). Jika sesi media fallback masuk dalam rentang waktu tersebut, pemulihan dibatalkan sehingga warna aksen langsung bertransisi (*crossfade*) dari cover album lama ke cover album baru tanpa ada kedipan sesaat dari warna aksen default biru.
    - Pada `ApplyAlbumArtAccentIfAvailable()`, pemanggilan prematur `RestoreDefaultAccent()` saat stream thumbnail masih diproses dihilangkan demi menjaga kelancaran warna transisi.

### 7. Optimasi Efisiensi RAM Modul Media
- **Pembatasan Resolusi Decode Album Art (`DecodePixelWidth = 200`)**:
  - `MediaService.cs` membatasi decode thumbnail GSMTC Windows dengan `DecodePixelWidth = 200`. Cover art berukuran besar (1200x1200px hingga 3000x3000px) yang sebelumnya dapat memakan hingga 36 MB pada *Large Object Heap* (LOH) kini hanya memakan ~160 KB.
  - Gambar di-freeze (`bitmap.Freeze()`) agar aman di-render lintas thread dan terhindar dari kebocoran memori.
- **Penyusutan Batas In-Memory Cache Lirik**:
  - `LyricsService.cs` membatasi cache RAM `ConcurrentDictionary` hingga maksimal 20 lagu (sebelumnya 100). Seluruh hasil pencarian lirik tetap tersimpan permanen di disk offline cache (`%AppData%\NotchWin\LyricsCache\`) sehingga pembacaan ulang tetap instan (<1ms) tanpa membebani memori.

### 8. Mini-Player Terintegrasi di Menu Home (`SlideHomeCard`)
- **Tujuan**: Menghadirkan kontrol musik cepat dan visualisasi cover lagu secara langsung begitu notch dibuka tanpa harus berpindah ke Tab Sedang Memutar (Tab 2).
- **Tata Letak & Estetika Presisi**:
  - **Dimensi & Keselarasan**: Ditempatkan di baris 0 tab Home (`SlideHomeCard`), berdampingan simetris dengan Kalender Mingguan dengan tinggi seragam `120 DIPs` (lebar 338 DIPs), background `#161622`, border `#252538`, padding `12 DIPs`, dan radius sudut `16 px`.
  - **Cover Album (86×86 DIPs)**: Sudut membulat `16 px` via `OpacityMask VisualBrush`. Mengisi proporsi vertikal kartu secara seimbang. Dilengkapi grafis vektor pemandangan sunset bertingkat (`#1E1738` dengan matahari salmon oranye `#F87171` dan siluet gunung violet `#2B214C` & `#16112C`) sebagai fallback artistik saat sesi media tidak menyediakan bitmap album art.
  - **Tipografi & Status**: Judul lagu tebal (`13.5 pt`, putih `#FFFFFF`), nama artis / status adzan beraksen hijau emerald (`11 pt`, `#34D399`) dengan jarak vertikal rapi (`Margin="0,2,0,8"`).
  - **Seekbar Presisi**: Slider waktu interaktif ramping (tinggi 14px) dengan tampilan menit:detik berjalan (`FormattedPosition`) dan total durasi (`FormattedDuration`) berwarna `#71717A`.
  - **Kontrol Playback**: Tombol Previous (28×28 DIPs), tombol bundar putih Play/Pause (`TogglePlayPauseCommand`, ukuran `34×34 DIPs` dengan `Margin="22,0"` dan ikon gelap `#0F172A`), serta tombol Next (28×28 DIPs) dengan efek sentuh/hover sirkular halus (`#25FFFFFF`).
- **Penyelarasan MVVM**:
  - Didukung oleh properti observasi `HasThumbnail => Thumbnail != null` pada `MediaViewModel` dan alias command (`PreviousCommand`, `PlayPauseCommand`, `NextCommand`) untuk kompatibilitas penuh.

### 9. Mesin Sinkronisasi Presisi & Karaoke Dinamis (Adopsi Poni Engine)
Berdasarkan hasil bedah kode aplikasi Poni v1.5.0:
- **Kompensasi Latensi Lead Time (+300 ms)**:
  - Nilai `DefaultAutoLatencyCompensationMs` pada `MediaViewModel.cs` ditingkatkan dari 200 ms menjadi **300 ms**.
  - Mengimbangi latensi buffer output audio Windows/Bluetooth dan memberi jeda persepsi visual bagi mata manusia sehingga baris lirik muncul tepat saat vokal bersuara.
- **Kalkulasi Durasi Karaoke Berbasis Ketukan / Suku Kata (`karSpan`)**:
  - `UpdateKaraokeProgress()` pada `MediaViewModel.cs` kini menggunakan formula ritmis alami:
    - **Aturan 90% (`intervalSec * 0.9`)**: Sapuan warna teks selesai saat mencapai 90% durasi menuju baris berikutnya, menyisakan 10% jeda napas alami sebelum transisi baris baru.
    - **Pemberian Batas Alami Jeda Melodi (`naturalDurSec`)**: Menghitung jumlah kata (atau suku kata pada bahasa CJK Mandarin/Jepang/Korea dengan pengali 0.6). Durasi sapuan dibatasi ke `Math.Max(4.0, 1.0 + beats * 0.45)` detik, menghilangkan bug di mana lirik merayap lambat seperti siput saat ada jeda instrumen / solo gitar panjang (5–20 detik).
    - **Filter Ambang Perubahan (0.001 Delta)**: Menghindari pemborosan layout pass ketika perubahan waktu mikroskopis saat playback idle/paused.
- **Pembersihan Metadata Lagu Tingkat Lanjut (*Sanitization*)**:
  - `CleanTitleAndArtist()` pada `LyricsService.cs` kini secara otomatis menghapus suffix `- Topic` pada nama artis/judul (standar lagu otomatis YouTube Music) serta regex tag video klip seperti `[MV]`, `[M/V]`, `[4K]`, `[HQ]`, `visualiser`, `clip`, dan `vevo` demi meningkatkan tingkat kecocokan lirik LRCLIB.
- **Perbaikan Stuttering & Dead Margin Sapuan Karaoke di XAML**:
  - Mengganti `HorizontalAlignment="Stretch"` pada kontainer slot lirik collapsed (`CollapsedLyricSlotA/B`, `CollapsedLyricTextA/B`, `CollapsedLyricOverlayA/B`) dan expanded (`LyricText`, `LyricKaraokeText`) menjadi `HorizontalAlignment="Center"`.
  - Mengikat `MaxWidth` slot collapsed ke `ActualWidth` dari `CollapsedLyricsContainer` agar penataan teks berpusat di tengah dengan ukuran elemen yang presisi mengikuti lebar glyph huruf.
  - `KaraokeClipConverter` kini memotong pas dari huruf pertama (`X = 0`) hingga huruf terakhir (`X = textWidth`), menghapus jeda kosong (*dead space*) di sisi kiri dan mencegah percepatan tidak wajar di tengah baris.

### 10. Desain Kartu Lirik Simetris & Eliminasi Dead Space Vertikal
- **Eliminasi Void Vertikal Notch Media (`ExpandedMediaHeight = 210.0px`)**:
  - Pada [NotchViewModel.cs](../NotchWin/ViewModels/NotchViewModel.cs), tab pemutar media kini secara khusus menggunakan `ExpandedMediaHeight = 210.0px` (bukan fallback umum 264px).
  - Mengeliminasi ruang hitam kosong sebesar ~54px di atas dan di bawah pemutar media, membungkus kartu dan dock dengan pas dan rapi.
- **Kartu Lirik 136px Simetris dengan Pemutar Media**:
  - Tinggi `LyricsContainer` pada [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml) dinaikkan menjadi `Height="136px"` dengan sudut lengkung `CornerRadius="14"`, sejajar presisi dengan kartu kontrol pemutar musik di sisi kiri.
- **Pemisahan Toolbar Header (Baris 0) & Canvas Lirik (Baris 1)**:
  - **Baris 0 (`Height="20px"`)**: Menampung tombol versi lirik (`LyricsVersionChipBtn`), label sumber (`Media.LyricsSourceInfo`), serta kapsul offset otomatis/manual (`-` `🕒 Auto` `+`) dalam desain tipis modern.
  - **Baris 1 (`Height="108px"`)**: 100% dialokasikan murni untuk canvas 3 baris lirik (`Canvas Height="108"`), bebas total dari elemen overlay atau tombol yang menutupi teks lagu.
- **Gradasi Transparansi Lembut (8% Cutoff)**:
  - Pemudaran teks tepi atas dan bawah diubah dari 22% menjadi 8% (`Offset="0.08"` dan `Offset="0.92"`), menjaga baris sebelum dan sesudah lirik aktif tetap terbaca jernih.

### 11. Integrasi Penuh Spicy Lyrics TTML & Syllable Sync (Presisi Suku Kata)
- **Arsitektur Dual-Pipeline Spicy Lyrics**:
  - `TtmlLyricsService.cs` mendukung dua mesin pengambilan lirik berjenjang:
    1. **Official Developer API (`api.spicylyrics.org/v1`)**: Jika pengguna memasukkan token pengembang pada Pengaturan (`AppSettings.SpicyLyricsApiKey`), sistem melakukan pencarian via `/v1/tracks/search` dan mengambil TTML presisi via `/v1/lyrics/{id}` dengan header `Authorization: Bearer <key>`. Kandidat ini diberi bobot skor tertinggi **+2500**.
    2. **Public TTML Mirror (`api.betterlyrics.org/getLyrics`)**: Beroperasi secara zero-config tanpa API key untuk lagu-lagu populer yang telah ter-cache di mirror publik. Kandidat ini diberi bobot skor **+2000**.
    3. **Universal Fallback (LRCLIB)**: Jika lagu tidak ditemukan di Spicy Lyrics atau terjadi kendala jaringan, sistem secara mulus beralih ke LRCLIB (+1000 untuk synced, +50 untuk plain).
- **Parser XML TTML Preservasi Whitespace (Solusi Kata Menyatu)**:
  - Pemrosesan XML TTML menggunakan `XDocument.Parse(ttmlXml, LoadOptions.PreserveWhitespace)` untuk mempertahankan node teks spasi antar-elemen `<span>`.
  - Sistem secara cerdas menggabungkan whitespace antar-span sehingga kalimat tersusun sempurna (`Look at the stars`, bukan kata menyatu seperti `Lookatthestars`), sekaligus menormalkan indeks karakter suku kata (`CharStartIndex` dan `CharLength`).
- **Kalkulasi Sapuan Non-Linear Berbasis Karakter**:
  - Saat lagu berstatus syllable-synced (`IsSyllableSynced = true` dan `IsSyllableSyncEnabled = true`), formula `UpdateKaraokeProgress()` di `MediaViewModel.cs` menghitung fraksi klip berdasarkan posisi karakter aktif:
    $$\text{CharPos} = \text{CharStartIndex} + (\text{SyllableProgress} \times \text{CharLength})$$
    $$\text{Progress} = \frac{\text{CharPos}}{\text{TotalPanjangKarakterBaris}}$$
  - **Efek Jeda Antar-Kata (Apple Music / Spotify Style)**: Saat penyanyi selesai menyuarakan satu kata dan mengambil napas sebelum kata berikutnya, klip karaoke **membeku (freeze)** di akhir kata sebelumnya, bukan merayap lurus.
- **Sistem Karaoke Bebas Stutter / Anti Patah-patah**:
  - **Sinkronisasi Text Measurement & Left Offset**:
    - `KaraokeClipConverter.cs` mengevaluasi elemen `TextBlock` dan mengukur lebar aktual teks (`FormattedText` dengan thread-safe LRU cache).
    - Jika teks berposisi di tengah (`TextAlignment.Center` atau `HorizontalAlignment.Center`), converter menghitung offset kiri nyata:
      $$\text{LeftOffset} = \frac{\text{ActualWidth} - \text{MeasuredWidth}}{2}$$
      $$\text{RightEdge} = \text{LeftOffset} + (\text{Progress} \times \text{MeasuredWidth})$$
    - Hal ini mengeliminasi total *dead margin* di 35% awal durasi, lonjakan cepat (*sprint 3.5x*) di tengah, dan status macet di 35% akhir durasi.
  - **Anti-Jitter SMTC Drift Protection (Toleransi 1.2 Detik)**:
    - Sinyal pembaruan posisi latar belakang dari Windows Media Controls (SMTC) yang memiliki latency 200–500ms tidak lagi me-restart stopwatch atau menganulir posisi lagu secara acak jika selisih waktu $< 1.2$ detik. Posisi lagu bergeser mulus tanpa loncatan mundur/maju sesaat.
  - **Timer Stabil 60 FPS (16.0 ms)**:
    - Timer dipatok stabil pada `16.0 ms` pada `DispatcherPriority.Render` selama lagu aktif, tanpa fluktuasi interval bolak-balik saat jeda antar-baris.
    - Ambang pembaruan `ActiveLyricProgress` diperhalus ke `0.0005` untuk sapuan sub-pixel yang sangat mulus.
- **Integrasi Pemilih Versi Lirik (`LyricsVersionChipBtn`)**:
  - Kandidat Spicy Lyrics otomatis masuk dengan tag **`SYLLABLE`** dan lencana provider merah muda **`[Spicy Lyrics]`**.
  - Badge sumber di pojok kanan-bawah otomatis menampilkan **`Spicy Lyrics · SYLLABLE`** atau **`lrclib.net · SYNCED`**.
- **Pengaturan Kustomisasi (Pengaturan → Musik)**:
  - Toggle **"Spicy Lyrics (Sinkronisasi suku kata)"** (`AppSettings.IsSyllableSyncEnabled`, default `true`).
  - Kolom **"Spicy Lyrics API Key"** (`AppSettings.SpicyLyricsApiKey`): token opsional dari `developers.spicylyrics.org` untuk mengakses seluruh pustaka lagu tanpa batas.
  - Pengguna bebas beralih antara sapuan kata-demi-kata (Syllable) atau sapuan baris alami standar (Poni Engine).

---

## 🚫 Panduan Modifikasi Aman (Do's & Don'ts)
- **JANGAN** memanggil `StateAnimationRequested` dari event `LyricsExpansionChanged` di [NotchViewModel.cs](../NotchWin/ViewModels/NotchViewModel.cs), karena akan memicu `AnimateToCurrentState() -> ResetSlideViews()` yang langsung mematikan (`BeginAnimation(..., null)`) animasi geser pemutar musik dan lirik.
- **JANGAN** memanggil `ResetSlideViews` di [NotchWindow.xaml.cs](../NotchWin/Views/NotchWindow.xaml.cs) ketika notch sudah dalam status `Expanded` (`flag == false`), karena `ResetSlideViews` akan menghapus semua animasi lirik dan kartu yang sedang berjalan.
- **JANGAN** memicu penyimpanan disk sinkron secara instan saat tombol lirik ditekan; gunakan penundaan minimal 400ms (`Task.Delay`) agar event `SettingsChanged` tidak membatalkan animasi UI di tengah jalan.
- **JANGAN** menghapus pemeriksaan `IsLyricsExpanded` dari properti `IsCollapsedLyricsActive` di [MediaViewModel.cs](../NotchWin/ViewModels/MediaViewModel.cs), agar lirik tidak muncul saat tombol lirik sedang dimatikan oleh user.
- **JANGAN** memblokir thread UI saat mengambil lirik dari lrclib; selalu gunakan `CancellationTokenSource` dan `Task.Run` asinkron.
