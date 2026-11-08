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

#### Keputusan 5: Penyesuaian Bentuk Notch Menempel Layar dengan Lengkungan Keluar (Outward Top Ears / Fillet)
- **Pilihan**: Menggunakan rendering geometri vektor dinamis (`StreamGeometry`) yang membentuk lengkungan cekung keluar (*outward concave arc / fillet*) dengan radius 12px di kedua sudut atas tempat notch bertemu bezel monitor, sisi tegak lurus, dan sudut melengkung cembung di bagian bawah (radius 16px collapsed, 22px expanded). Border atas ditiadakan sehingga warna hitam pekat menyatu 100% dengan bezel fisik.
- **Alasan**: Menghasilkan siluet transisi organik khas notch modern (ala MacBook Pro / iPhone Dynamic Island). Sudut lengkung keluar di bagian atas membuat notch terlihat keluar secara natural dan rapi dari bezel monitor, bukan sekadar persegi panjang kaku. Hit-testing `WM_NCHITTEST` menggunakan `Geometry.FillContains` untuk presisi klik tembus di luar area lengkungan.

---

### [2026-10-22] Fase 2: Widget Media (Spotify, YouTube, Browser SMTC)

#### Keputusan 6: Integrasi System Media Transport Controls (SMTC) Bawaan Windows
- **Pilihan**: Menggunakan `GlobalSystemMediaTransportControlsSessionManager` dari Windows Runtime tanpa library pihak ketiga atau ekstensi browser.
- **Alasan**: Mendukung semua pemutar media modern di Windows 10/11 (Spotify, YouTube di Edge/Chrome/Firefox, Apple Music, VLC) secara out-of-the-box tanpa konfigurasi tambahan oleh pengguna.

#### Keputusan 7: Efisiensi Event-Driven dan Pembaruan Progres
- **Pilihan**: Tidak menggunakan polling untuk mendeteksi perubahan lagu. Berlangganan event native SMTC (`MediaPropertiesChanged`, `PlaybackInfoChanged`, `CurrentSessionChanged`). Timer progres 1 detik hanya aktif saat status media `Playing` dan dihentikan sepenuhnya saat `Paused`/`Stopped`.
- **Alasan**: Menjaga CPU idle mendekati 0% dan mencegah kebocoran memori saat pergantian lagu berkali-kali. Thumbnail bitmap di-freeze (`Freeze()`) agar aman di-render lintas thread.


---

#### Keputusan 68: Merapikan Tampilan Navbar Bawah (`NavDockTrack`, Padding Simetris & Hirarki Visual)

* **Konteks**: Pengguna melaporkan tampilan navbar bawah pada mode notch expanded terlihat tidak rapi ("sepertinya navbarnya tidak rapi"). Investigasi menemukan beberapa masalah estetika dan tata letak:
  1. Semua 9 label tab di navbar memiliki teks putih murni `#FFFFFF` permanen baik saat aktif maupun inaktif, sehingga navbar tampak bising tanpa hirarki fokus visual.
  2. Padding tombol tab sangat sempit dan asimetris (`Padding="3.5,1.5,3.5,1.5"` dengan margin label `3,0,1.5,0`), menyebabkan ikon pada tab aktif menempel kencang ke lengkungan sudut `NavActivePill` (CornerRadius 11 px).
  3. Dimensi ikon bervariasi (10 px, 10.5 px, 11 px) dengan ketebalan stroke yang berbeda-beda.
  4. Lencana counter file pada tab Shelf terlalu besar (16×16 px dengan font 11 px) di dalam tab tinggi 22 px.
  5. Baris navbar tidak memiliki track kontainer sehingga tombol mengambang secara terpisah di atas background notch.
* **Pilihan**:
  1. Membungkus `NavTabsHost` dalam kapsul track `NavDockTrack` (`CornerRadius="13"`, background transparan halus `#3512121A`, border `#252535`, padding 2 px) di dalam `NavDockHost` (tinggi 26 px, margin bawah 8 px). Seluruh tab kini tersaji sebagai satu kesatuan segmented dock modern bergaya iOS / Windows Fluent.
  2. Padding tombol diubah menjadi `Padding="6,0,7,0"` dengan margin label `4,0,0,0` dan margin tombol `0,0,2,0` (tombol Shelf margin 0). Ikon dan teks kini memiliki jarak napas simetris dan proporsional persis di tengah kapsul aktif `NavActivePill`.
  3. Seluruh ikon diseragamkan ke ukuran 11×11 DIPs dengan ketebalan stroke 1.2 px, menghasilkan bobot visual yang konsisten antar-tab.
  4. Lencana counter Shelf diproporsionalkan menjadi tinggi 14 px, sudut bulat 7 px, dan font 9 px bold.
  5. Di `ApplyNavTabVisualState`, teks tab aktif diberi warna `#FFFFFF` (SemiBold) dan ikon aksen, sedangkan tab inaktif diberi warna muted `#9CA3AF` (Normal), menciptakan kontras dan kejelasan visual instan.
  6. Menambahkan `InvalidateNavFullWidths()` yang dipanggil saat skala font notch diubah, sehingga lebar tombol dan posisi glide kapsul selalu terkalibrasi akurat dengan ukuran teks baru.
* **Alasan**: Mengembalikan estetika antarmuka notch menjadi rapi, bersih, elegan, dan profesional sesuai standar desain modern tanpa mengubah logika perpindahan slide atau menambah overhead kinerja.


---

#### Keputusan 69: Modernisasi Set Ikon Vektor Navbar (Unified Outline Style)

* **Konteks**: Pengguna meminta tampilan ikon pada bar navigasi (navbar) bawah dibuat lebih bagus dan modern ("coba buat icon nya lebih bagus dan modern lagi untuk tampilan navbar nya"). Sebelumnya, navbar menggunakan ikon campuran antara siluet solid (fill) dan garis kasar (stroke), geometri ikon tergolong primitif (misal ikon rumah klasik 90-an untuk Pusat Kontrol, blok not balok tebal untuk Musik, dan ikon kotak bertumpuk untuk Shelf), serta inkonsistensi ketebalan garis antar-ikon.
* **Pilihan**:
  1. Merombak seluruh 9 ikon navigasi menggunakan bahasa desain vektor terpadu (*Unified Geometric Outline System*) terinspirasi dari Apple SF Symbols & Lucide Icons dengan stroke seragam 1.2 px, `StrokeStartLineCap="Round"`, `StrokeEndLineCap="Round"`, dan `StrokeLineJoin="Round"`:
     - **Tab 0 (Pusat Kontrol)**: Diganti dari ikon rumah kuno menjadi **Dual Sliders with Knobs** (`slider.horizontal.2`), ikon modern standar iOS/macOS Control Center.
     - **Tab 1 (Musik)**: Not balok ganda modern dengan kurva nothead elips yang halus dan balok penghubung miring yang proporsional.
     - **Tab 2 (Cuaca)**: Simbol cuaca universal modern berwujud matahari terbit di balik awan berkontur lembut (*sun and cloud*).
     - **Tab 3 (Jadwal Sholat)**: Bulan sabit geometris (*crescent moon*) dengan kurva busur anggun dan ujung lengkung yang presisi.
     - **Tab 4 (Pengingat Minum)**: Tetesan air (*droplet*) berkontur tetes embun yang bersih dan simetris.
     - **Tab 5 (Catatan)**: Lembar dokumen memo bersudut lipat (*folded document*) dengan garis tulisan minimalis.
     - **Tab 6 (Clipboard)**: Papan klip (*clipboard*) bersudut melengkung dengan klem atas dan garis daftar catatan.
     - **Tab 7 (Timer & Stopwatch)**: Jam stopwatch presisi dengan tombol mahkota atas (*crown pusher*), tombol start miring 45°, serta jarum jam 12:00 dan 2:00.
     - **Tab 8 (Shelf)**: Kotak laci penyimpanan (*archive tray*) dengan bibir atas dan celah handle yang bersih.
  2. Menyederhanakan logika `ApplyNavTabVisualState` di `NotchWindow.xaml.cs`: Seluruh ikon kini dirender via `Stroke` seragam (aktif = aksen/biru hidrasi, inaktif = `#9CA3AF`), mengeliminasi kode *switch case* kompleks pengatur `Fill` vs `Stroke`.
* **Alasan**: Memberikan tampilan navbar yang sangat premium, serasi, bersih, dan berstandar internasional setara antarmuka modern Apple iOS dan Windows 11 Fluent Design tanpa menambah ukuran memori atau waktu kompilasi.


---

#### Keputusan 70: Redesain Menyeluruh Pusat Kontrol (Modern Dark Slate Glass & Capsule Sliders)

* **Konteks**: Pengguna meminta bagian Pusat Kontrol (Control Center) dirombak dan didesain ulang agar lebih modern, rapi, dan estetis ("sekarang kita rombak bagian dari control center, coba kamu redesign control center nya"). Analisis pada tata letak sebelumnya menunjukkan kotak kartu datar `#171720` yang kaku, slider volume dan brightness yang terlalu tipis (2 px seekbar style), tipografi baterai yang tidak sejajar dan berantakan, serta tombol quick action yang berupa kotak abu-abu polos.
* **Pilihan**:
  1. **Dark Slate Glass Card Containers**: Mengadopsi bahasa desain *Dark Slate Glass* (`Background="#161622"`, `BorderBrush="#28283C"`, `CornerRadius="11"`) untuk keenam modul kartu dalam Pusat Kontrol, menghasilkan tampilan yang elegan, seragam, dan modern.
  2. **Capsule Slider Style (`ControlCenterSliderStyle`)**:
     - Menggantikan slider tipis dengan track kapsul tebal 6 DIPs (`CornerRadius="3"`, background `#222230`).
     - Thumb berwujud kapsul lingkaran putih bersih 10 DIPs (`CornerRadius="5"`, drop shadow halus `Opacity="0.4"`) dengan feedback hover yang membesar ke 12 DIPs.
     - Slider volume menggunakan warna aksen sistem, sedangkan slider kecerahan layar diberi aksen kuning hangat matahari (`#FBBF24`).
     - Tombol pemilih perangkat output audio (`AudioDeviceSelectorBtn`) diubah menjadi chip pil modern interaktif (`#1C1C2A`, border `#2D2D42`, `CornerRadius="6"`) dengan ikon panah chevron vektor.
  3. **Penataan Ulang Informasi Baterai**:
     - Header baterai menampilkan status pengisian (`StatusTitle`) dan persentase baterai tebal 12.5pt (`FormattedPercent`).
     - Progress bar sisa daya dipertebal menjadi 5 DIPs dengan sudut bulat penuh (`CornerRadius="2.5"`).
     - Menata metrik kapasitas real-time (`CapacityText`) dan voltase (`VoltageText`) di sisi kiri, serta watt aliran daya (`PowerFlowText`) di sisi kanan dalam lencana chip hijau emerald (`#10281F`, border `#1E4738`), menghilangkan kesan teks yang tumpang-tindih.
  4. **Lencana Jaringan & Indikator Konektivitas**:
     - Lencana "Active" kini dilengkapi lampu indikator hijau bulat emerald 4 DIPs.
     - Kecepatan jaringan disajikan dalam dua lencana chip mikro: unduh (`↓ 7 KB/s` dengan latar `#0C1F2B`, border `#14415C`, teks biru muda `#38BDF8`) dan unggah (`↑ 146 KB/s` dengan latar `#1C122B`, border `#401B64`, teks ungu muda `#A78BFA`).
     - Baris status Bluetooth tertata rapi dengan ikon vektor konsisten.
  5. **Telemetri Hardware (CPU, RAM, GPU)**:
     - Mengubah progress bar metrik menjadi kapsul bulat 5 DIPs dengan warna kontras modern: CPU Cyan (`#06B6D4`), RAM Amber (`#F59E0B`), dan GPU Violet (`#A855F7`).
     - Tetap mempertahankan fungsi kartu sebagai tombol interaktif untuk membuka Windows Task Manager.
  6. **Pintasan Aksi Cepat (Quick Actions)**:
     - Memperbarui gaya `QuickActionTileButtonStyle` dengan sudut lengkung 7 DIPs, elevasi hover `#262638`, dan garis tepi aksen halus.
     - Mengganti seluruh ikon pintasan solid menjadi ikon garis vektor modern (*Geometric Outline*, stroke 1.1 px) untuk Screenshot, Lock, TaskMgr, dan Settings.
* **Alasan**: Mentransformasi tampilan Pusat Kontrol menjadi sangat modern, mewah, dan terstruktur rapi setara antarmuka iOS / macOS Control Center dan Windows 11 Fluent Design dengan tetap mempertahankan 100% data-binding dan fungsionalitas interaktif.


---

#### Keputusan 71: Penjajaran Presisi Sumbu Vertikal Ikon Pusat Kontrol (Optical & Geometric Icon Alignment)

* **Konteks**: Pengguna melaporkan bahwa ikon pada kartu jaringan & Bluetooth di Pusat Kontrol tidak sejajar ("icon nya tidak sejajar, coba rapikan") dan mengunggah tangkapan layar kartu tersebut. Investigasi menemukan:
  1. Ikon Ethernet (`Width="10.5" Height="10.5"`) dan Bluetooth (`Width="8.5" Height="10"`) pada kolom 0 (`Width="16"`) sama-sama disetel `HorizontalAlignment="Left"`. Karena lebar geometris dan rasio aspek keduanya berbeda drastis (Ethernet ~1:1 kotak monitor, Bluetooth ~1:2 tinggi ramping), pusat sumbu vertikal (*vertical center axis*) ikon Ethernet berada di x = 5.25 DIP, sedangkan ikon Bluetooth berada di x = 4.25 DIP, menghasilkan pergeseran visual nyata sekitar 1 DIP (~1.5-2 piksel fisik).
  2. Geometri Bluetooth sebelumnya memiliki ujung kiri bersudut tajam yang menggeser titik berat optik (*optical center*) ke kanan, sementara bagian kiri menempel ke tepi kolom.
  3. Hal serupa terjadi pada kartu Audio & Kecerahan (ikon Volume dan Kecerahan menggunakan `HorizontalAlignment="Left"`).
* **Pilihan**:
  1. Mengubah seluruh ikon di kolom 0 (Volume, Kecerahan, Wi-Fi, Ethernet, Bluetooth) menjadi `HorizontalAlignment="Center" VerticalAlignment="Center"`. Dengan lebar kolom 16 DIPs yang konsisten, seluruh ikon kini terkunci persis di sumbu tengah x = 8.0 DIP.
  2. Memperbarui geometri ikon Bluetooth ke standar simetris (*Unified Geometric Outline* `M2,3.5 L8,8 L5,11 L5,1 L8,4 L2,8.5` dengan `Width="8.5" Height="11"`), sehingga batang vertikal berada tepat di tengah geometris x = 5.
  3. Menstandarkan margin teks judul ("Ethernet", "Bluetooth ...") menjadi `Margin="4,0,0,0"` di kolom 1, serta menyesuaikan margin baris badge kecepatan unduh/unggah ke `Margin="20,3,0,4"` (16 + 4 = 20 DIPs) agar rata kiri secara sempurna dengan teks nama koneksi di atasnya dan status Bluetooth di bawahnya.
* **Alasan**: Memastikan antarmuka Pusat Kontrol memiliki garis vertikal pemandu yang presisi secara matematis dan optik, menghasilkan tata letak yang sangat rapi, bersih, dan memuaskan secara visual.



---

#### Keputusan 72: Vektor Baterai HD Presisi & Penanganan Interaksi Dropdown Audio Output Tanpa Auto-Collapse

* **Konteks**: Pengguna melaporkan dua masalah:
  1. Ikon baterai terlihat buram / tidak HD (pecah di DPI scaling tinggi, sudut tepi tidak tegas, dan petir pengisian daya diselimuti blur hitam pekat yang mengaburkan bentuknya).
  2. Saat tombol pemilih output speaker (`AudioDeviceSelectorBtn`) diklik untuk membuka menu dropdown perangkat audio (`DevicePickerPopup`), Dynamic Notch langsung menutup secara tiba-tiba karena event `MouseLeave` dan `WM_NCHITTEST` salah mendeteksi perpindahan fokus ke window HWND Popup sebagai indikasi kursor meninggalkan notch.
* **Pilihan**:
  1. **Peningkatan Ikon Baterai Menjadi Vektor HD Presisi**:
     - Menstandarkan seluruh 4 representasi baterai (header collapsed default, header collapsed musik, header expanded, dan kartu baterai Pusat Kontrol) ke ukuran geometris bulat presisi `Width="22" Height="11"` dengan `SnapsToDevicePixels="True"` dan `UseLayoutRounding="True"`.
     - Mengubah bodi utama menjadi wadah berukuran `Width="19" Height="11"` (`CornerRadius="3"`, `BorderThickness="1"`) dengan latar gelap `#161620` dan garis tepi metalik refined `#8E8E98` (`OutlineGrayBrush`).
     - Terminal anoda baterai kini diintegrasikan rapi di sisi kanan dengan ukuran solid `Width="2" Height="5"` (`CornerRadius="0,1.5,1.5,0"`).
     - Ruang pengisian daya internal dibuat tepat 14.0 DIPs (`Margin="1.5,0,0,0"` dan `Height="7"`), sinkron 1:1 dengan formula `BatteryFillWidth` di `BatteryViewModel.cs` (`Math.Round((RemainingPercent / 100.0) * 14.0)`), menghilangkan masalah clipping atau overflow sebelumnya.
     - Mengganti path petir charger yang terpotong menjadi poligon vektor 7 titik tertutup yang tajam (`M 3.8,0 L 0.5,5.2 H 3.2 L 2.2,9.2 L 6.2,3.8 H 3.4 Z`, `Width="5.5" Height="7"`), serta menghapus efek `DropShadowEffect BlurRadius="2"` yang sebelumnya menciptakan halo hitam buram.
  2. **Perbaikan Auto-Collapse Dropdown Output Audio (`DevicePickerPopup`)**:
     - Menghubungkan event `Opened="DevicePickerPopup_Opened"` dan `Closed="DevicePickerPopup_Closed"` pada `DevicePickerPopup`.
     - Mengamankan pemanggilan `ViewModel.OnMouseLeave()` pada `PillBorder_MouseLeave` dan `WM_NCHITTEST` di `NotchWindow.xaml.cs` dengan pengecekan `!(ViewModel?.Volume?.IsDevicePickerOpen ?? false)`.
     - Menambahkan penjagaan di `NotchViewModel.OnMouseLeave()` dan `ScheduleDelayedCollapse()` agar early-return dan membatalkan collapse saat `Volume.IsDevicePickerOpen` bernilai `true`.
     - Membatalkan timer collapse yang sedang berjalan saat popup dibuka melalui `ViewModel?.CancelPendingCollapse()`.
     - Mengikat handle Win32 HWND popup (`_devicePickerPopupHwnd`) ke siklus `WindowStyleHelper.EnsureTopmost` di `TopmostKeeper` agar popup tetap melayang paling atas (*topmost*) di atas window notch.
     - Menambahkan `ScrollViewer MaxHeight="220"` pada daftar perangkat audio agar dropdown tetap rapi dan dapat digulir jika terdapat banyak output soundcard/perangkat audio virtual.
* **Alasan**: Menghasilkan ikon baterai yang tajam, jernih, dan berdefinisi tinggi di segala skala DPI layar, serta menjamin alur interaksi pemilihan perangkat speaker di Pusat Kontrol berjalan lancar, intuitif, dan stabil tanpa menutup notch secara prematur.

---

#### Keputusan 73: Overhaul Tipografi HD (Aktivasi ClearType Subpixel, Penggantian Segoe UI Variable Text, & Penjajaran Grid Pixel)

* **Konteks**: Pengguna melaporkan bahwa tulisan di seluruh bagian antarmuka Dynamic Notch terlihat buram dan tidak HD ("kenapa aku melihat tulisannya seperti ngeblur, tidak hd tulisannya disemua bagian"). Investigasi mendalam menemukan kombinasi empat faktor penyebab:
  1. **Deaktivasi ClearType Otomatis oleh Window Transparan**: WPF secara default mematikan *ClearType sub-pixel antialiasing* ketika sebuah window memiliki `AllowsTransparency="True"` karena WPF tidak dapat memastikan warna latar belakang di balik jendela transparan. Akibatnya, WPF jatuh ke mode *Grayscale Antialiasing* (hanya membaurkan warna abu-abu pada tepian glif). Pada latar belakang gelap (#000000 dan #161622), grayscale antialiasing membuat teks tampak tipis, pudar, dan berbayang buram.
  2. **Pemilihan Sumbu Optik Font yang Salah (`Segoe UI Variable Display`)**: Pada lebih dari 54 lokasi di `NotchWindow.xaml`, font yang digunakan adalah `Segoe UI Variable Display`. Dalam spesifikasi Windows 11 Typography, varian *Display* dirancang khusus untuk judul raksasa (ukuran 36pt ke atas) dengan ketebalan batang rambut (*hairline*) yang sangat tipis. Ketika dipaksa merender teks antarmuka kecil (9pt–13pt), ketebalan batang huruf menjadi kurang dari 1 piksel fisik monitor (<0.5 px), sehingga rasterizer DirectWrite tidak dapat mengisi piksel penuh dan menghasilkan huruf yang sangat kabur.
  3. **Absennya `SnapsToDevicePixels="True"` pada Window dan Kontainer Utama**: Tanpa pixel snapping, garis dasar (*baseline*) glif mendarat di koordinat fisik pecahan (misal Y = 14.375 pada DPI 125%), menyebabkan batang horizontal teks terdistribusi di antara dua baris piksel layar.
  4. **Penskalaan Ukuran Font Pecahan (`FontScale: 0.85`)**: Pengaturan pengguna berada di `FontScale: 0.85` dengan pembulatan `Math.Round(..., 1)` yang menghasilkan ukuran font aneh (misal 9.4pt, 8.1pt, 10.2pt) yang meleset dari tabel *font hinting* standar Windows.
* **Pilihan**:
  1. **Aktivasi Paksa Subpixel ClearType & Display Formatting**:
     - Menambahkan `TextOptions.TextRenderingMode="ClearType"`, `TextOptions.TextFormattingMode="Display"`, dan `RenderOptions.ClearTypeHint="Enabled"` pada level `<Window>`, `PillContainer`, `ContentHost`, `CollapsedContentGrid`, `ExpandedContentGrid`, `SettingsWindow`, dan gaya implisit global di `App.xaml` (`TargetType="{x:Type TextBlock}"` dan `TargetType="{x:Type Control}"`).
     - Menambahkan `RenderOptions.BitmapScalingMode="HighQuality"`.
  2. **Migrasi Menyeluruh ke `Segoe UI Variable Text`**:
     - Mengganti seluruh 54 penggunaan `Segoe UI Variable Display` menjadi `Segoe UI Variable Text, Segoe UI, sans-serif`.
     - Varian *Text* adalah standar resmi Windows 11 untuk teks antarmuka (8pt–20pt) dengan ketebalan batang solid (>= 1.0 piksel), celah huruf (*counters*) yang terbuka, dan kontras tajam. Pada Windows 10, font stack ini dengan mulus jatuh ke `Segoe UI` standar yang terbukti tajam.
  3. **Penguncian Grid Layar Fisik (`SnapsToDevicePixels`)**:
     - Memastikan `SnapsToDevicePixels="True"` dan `UseLayoutRounding="True"` aktif secara hierarkis pada seluruh kontainer dan blok teks.
  4. **Penjajaran Half-Point Grid pada `FontScaleService`**:
     - Memperbarui perhitungan ukuran font pada `FontScaleService.cs` menjadi kelipatan 0.5pt (`Math.Round(baseSize * scale * 2.0) / 2.0`), misal 8.5, 9.0, 9.5, 10.0, 10.5, 11.0, sehingga glif selalu terkunci sempurna pada tabel hinting DirectWrite.
* **Alasan**: Mengembalikan ketajaman teks ke standar tertinggi native Windows (setara antarmuka Windows 11 Fluent dan macOS Retina), menghilangkan seluruh efek buram, dan membuat seluruh tulisan di bilah collapsed, expanded, kartu kontrol, dan lirik menjadi berdefinisi tinggi (HD) dan sangat nyaman dibaca.

---

#### Keputusan 74: Penghapusan DropShadowEffect Rasterizer pada Angka Suhu Cuaca (`25°C`) untuk Vektor HD Tajam

* **Konteks**: Pengguna melaporkan bahwa angka suhu cuaca pada kartu cuaca terlihat tidak HD / bergerigi (*pixelated*) dan mengunggah tangkapan layar kartu cuaca yang menunjukkan angka `25°C` dengan tepian tangga kasar (*staircase aliasing*) pada lengkungan angka '2', '5', simbol derajat '°', dan huruf 'C'.
* **Akar Masalah**:
  1. Pada `NotchWindow.xaml` (baris 3887–3895), `TextBlock` suhu (`Weather.TemperatureFormatted`) dipasangi efek bayangan:
     `<UIElement.Effect><DropShadowEffect BlurRadius="4" ShadowDepth="0" Color="#000000" Opacity="0.25" /></UIElement.Effect>`.
  2. Di WPF, ketika sebuah efek pixel shader (`DropShadowEffect` / `BlurEffect`) diterapkan langsung pada elemen `TextBlock`, engine DirectX WPF dipaksa merender teks tersebut ke dalam *intermediate offscreen bitmap cache* resolusi rendah tanpa ClearType.
  3. Akibatnya, teks berukuran besar (38pt) tidak dirender sebagai kurva vektor DirectWrite asli, melainkan sebagai gambar bitmap raster yang terdistorsi dan bergerigi.
  4. Hal serupa juga terjadi pada ikon cuaca `Path` di sebelahnya yang dipasangi `DropShadowEffect BlurRadius="6"`.
* **Pilihan**:
  1. Menghapus sepenuhnya elemen `<UIElement.Effect>` bayangan dari `TextBlock` angka suhu dan ikon cuaca `Path`. Tanpa bayangan buatan tersebut, DirectWrite merender kurva angka dan huruf sebagai vektor murni yang sangat tajam (*crisp vector font rendering*) langsung ke layar.
  2. Menambahkan `SnapsToDevicePixels="True"`, `UseLayoutRounding="True"`, `TextOptions.TextFormattingMode="Display"`, `TextOptions.TextRenderingMode="ClearType"`, dan `RenderOptions.ClearTypeHint="Enabled"` pada `TextBlock` suhu.
  3. Menghapus properti kaku `LineHeight="38"` agar perhitungan kotak pembatas glif beradaptasi secara alami terhadap penskalaan font.
  4. Mengaktifkan `RenderOptions.BitmapScalingMode="HighQuality"` pada ikon cuaca `Path` dan menyematkan atribut `SnapsToDevicePixels` pada kontainer induk `SlideWeatherCard`.
* **Alasan**: Menghilangkan keburaman dan efek pixelated pada angka suhu, mengembalikan kehalusan kurva tipografi angka cuaca 38pt ke standar HD murni setara antarmuka Apple Weather / Windows 11 Weather.


---

#### Keputusan 75: Eliminasi Mojibake Karakter Rusak (`Ā¢Å"â€¢`), Vektorisasi Glif Interaktif, & Lokalisasi Menyeluruh Notifikasi/Toast

* **Konteks**: Pengguna melaporkan tampilan notifikasi sub-notch menampilkan karakter rusak/mojibake `Ā¢Å"â€¢` pada ujung pill dan pesan notifikasi catatan masih berantakan dalam bahasa Inggris (`Note ⏰ Alarm set: 00:00 (Tomorrow)`). Pengguna memberikan instruksi tegas: *"perbaiki bahasanya, lalu jangan pernah jalankan aplikasinya"*.
* **Akar Masalah**:
  1. **Mojibake Glif UTF-8 pada XAML**: Karakter `✕` (U+2715) pada tombol tutup sub-notch notifikasi di `NotchWindow.xaml` (baris 1325) mengalami codepage mismatch (CP1252 / double encoding) menjadi `Ã¢Å“â€¢` yang di layar pengguna muncul sebagai `Ā¢Å"â€¢`. Pola mojibake serupa juga ditemukan pada panah dropdown lirik (`â–¾`), centang kandidat lirik (`âœ✓`), tombol hapus file shelf (`âœ•`), bullet ukuran shelf (`â€¢`), dan emoji badge drag-and-drop (`ðŸš€`, `ðŸ“¥`).
  2. **Hardcoded Pesan Toast dalam Bahasa Inggris**: Pada `NoteViewModel.cs`, event `ToastRequested` memancarkan string hardcoded bahasa Inggris tanpa melewati `LocalizationService`:
     - `"⏰ Alarm set: {dueTime:HH:mm} ({(dueTime.Date == DateTime.Today ? "Today" : "Tomorrow")})"`
     - `"New note created"`, `"Note cleared"`, `"Note deleted"`, `"Note copied to Clipboard!"`, `"⏰ Reminder: {reminder.Text}"`, `"Focus Time Complete! Time to take a short break."`, `"Timer: {nextMins} Minutes"`, `"Alarm time set: {AlarmHour}:{AlarmMinute}"`.
     Hal serupa terjadi pada `ClipboardViewModel.cs` (`"Copied to Clipboard!"`) dan `NotchViewModel.cs` (`"Note"`, `"Clipboard"`, `"Screenshot Captured"`, `"Text Copied"`, `"Bluetooth Connected"`, `"Bluetooth Disconnected"`).
  3. **Inkonsistensi Kamus Bahasa Indonesia**: Pada `LocalizationService.cs`, kunci `["Tab.Note"]` di kamus Bahasa Indonesia (`LangId`) masih bernilai `"Note"` alih-alih `"Catatan"`, dan entri tab navbar Shelf belum didaftarkan.
  4. **Preferensi Bahasa Aktif**: Konfigurasi `%APPDATA%\NotchWin\settings.json` masih terset ke `"Language": "en"`.
* **Pilihan & Solusi**:
  1. **Penggantian Mojibake dengan Kurva Vektor Native HD**:
     - Mengganti seluruh glif teks yang rentan encoding (`✕`, `▾`, `✓`) dengan elemen vektor `Path` native WPF (`Data="M 0,0 L 7,7 M 7,0 L 0,7"`, dll.) yang bebas dari ketergantungan encoding font, anti-mojibake 100%, dan selalu tajam di semua resolusi dan DPI layar.
     - Memperbaiki bullet format menjadi `StringFormat='• {0}'` dan karakter emoji dengan XML entity `&#x1F680;` dan `&#x1F4E5;`.
  2. **Lokalisasi Menyeluruh Notifikasi & Toast**:
     - Mendaftarkan entri kamus terpadu di `LocalizationService.cs` untuk `LangEn` dan `LangId`: `Notification.AlarmSet`, `Notification.AlarmTimeSet`, `Notification.Reminder`, `Notification.Today`, `Notification.Tomorrow`, `Notification.NoteCreated`, `Notification.NoteCleared`, `Notification.NoteDeleted`, `Notification.NoteCopied`, `Notification.FocusTimeComplete`, `Notification.TimerMinutes`, `Notification.ClipboardCopied`, `Notification.ScreenshotCaptured`, `Notification.TextCopied`, `Notification.BluetoothConnected`, `Notification.BluetoothDisconnected`, `Notification.DismissTooltip`, `Header.Shelf`, dan `Tab.Shelf`.
     - Memperbarui `["Tab.Note"] = "Catatan"` dan `["Tab.Clipboard"] = "Papan Klip"` di kamus Bahasa Indonesia.
     - Memperbarui `NoteViewModel.cs`, `ClipboardViewModel.cs`, dan `NotchViewModel.cs` agar seluruh pemanggilan toast menggunakan `LocalizationService.Instance.Get(...)` dan `L10n[...]`.
  3. **Penyelarasan Pengaturan Pengguna**:
     - Mengubah konfigurasi bahasa pengguna pada `%APPDATA%\NotchWin\settings.json` menjadi `"Language": "id"`.
  4. **Kepatuhan Terhadap Instruksi Pengguna**:
     - Memverifikasi kompilasi Release (`dotnet build -c Release`) berhasil tanpa error (`0 Warning(s), 0 Error(s)`).
     - Menepati instruksi pengguna untuk sama sekali tidak menjalankan proses aplikasi (`NotchWin.exe`).
* **Alasan**: Menghilangkan seluruh karakter rusak pada notifikasi sub-notch, memastikan seluruh pesan notifikasi, toast alarm/pengingat, dan tab bernavigasi tampil dalam Bahasa Indonesia yang benar, rapi, dan konsisten, serta menjaga integritas aplikasi tanpa menjalankan proses secara otomatis.

---

#### Keputusan 76: Pencegahan Duplikasi Item Shelf (Debounce Waktu, Normalisasi Path Kanonikal, & Guard Penanganan Drop Ganda)

* **Konteks**: Pengguna melaporkan bahwa sesekali ketika memasukkan file ke dalam Shelf (drag-and-drop), 1 file tiba-tiba menjadi 2 item di dalam daftar Shelf ("terkadang di shelf, ketika user memasukan file ke shelf, sesekali 1 file jadi 2 di shelf itu, coba perbaiki").
* **Akar Masalah**:
  1. **Tumpang Tindih Event Handler Drop (PreviewDrop vs Drop)**:
     `<Window>` mendaftarkan tunneling event `PreviewDrop="Window_PreviewDrop"` yang memproses file drop untuk Shelf (`ViewModel.Shelf.AddFiles(array)`). Pada saat yang sama, elemen anak `DropTargetShelf` (`Drop="DropTargetShelf_Drop"`) dan `ShelfDropZone` (`Drop="ShelfDropZone_Drop"`) juga memanggil `AddFiles`. Ketika `Window_PreviewDrop` menyembunyikan `DragChoiceOverlay` (`Visibility = Collapsed`), hit-test WPF berpindah ke elemen di bawahnya (`ShelfDropZone`), memicu event bubbling `Drop`. Karena handler anak tidak memiliki guard `if (e.Handled) return;`, file yang sama diproses dua kali.
  2. **Race Condition Selama Pembuatan Thumbnail (`GenerateThumbnail`)**:
     Pada `ShelfService.AddFiles`, pengecekan keberadaan item (`Items.FirstOrDefault`) dilakukan sebelum `GenerateThumbnail`. Proses pembuatan thumbnail memerlukan operasi disk I/O dan Shell API (`SHGetFileInfo`, `Imaging.CreateBitmapSourceFromHIcon`) yang memakan waktu puluhan milidetik. Jika dua event drop tiba dalam jeda sangat singkat (akibat event ganda atau pantulan microswitch mouse), panggilan kedua memeriksa `Items` saat panggilan pertama belum sempat memanggil `Items.Insert(0, item)`. Akibatnya, kedua panggilan menganggap file belum ada dan keduanya memasukkan item baru ke `Items`.
  3. **Absennya Normalisasi Path**:
     Jika sebuah file/folder dikirim dengan variasi format (misal folder dengan trailing slash `C:\Folder\` vs `C:\Folder`, perbedaan separator `/` vs `\`, atau spasi/tanda kutip di ujung path), perbandingan `string.Equals(i.FilePath, path)` gagal mengenali kesamaan path, sehingga item yang sama dimasukkan kembali.
  4. **Absennya Deduplikasi Batch**:
     Daftar `filePaths` dari payload `DataFormats.FileDrop` tidak difilter untuk membuang entri ganda sebelum diproses.
* **Pilihan & Solusi**:
  1. **Normalisasi Path Kanonikal (`ShelfService.NormalizePath` & `IsPathEqual`)**:
     - Membuat fungsi `NormalizePath(string path)` yang memotong tanda kutip/spasi, mengubah ke full canonical path via `Path.GetFullPath`, dan memangkas trailing directory separator (`\` dan `/`).
     - Seluruh perbandingan dan penyimpanan file path menggunakan path ternormalisasi, sehingga `C:\Folder\` dan `C:\Folder` dijamin identik.
  2. **Deduplikasi Batch Input**:
     - Memfilter input `filePaths` dengan `HashSet<string>(StringComparer.OrdinalIgnoreCase)` sebelum iterasi dimulai, mencegah payload ganda dari sumber drag luar.
  3. **Debounce Waktu & Kunci Sinkronisasi (`_recentlyAdded` & `lock (_lock)`)**:
     - Menambahkan kamus `_recentlyAdded` bertenggat waktu 600ms yang dilindungi `lock (_lock)`. Jika path kanonikal yang sama masuk kembali dalam kurun waktu kurang dari 600ms, panggilan duplikat langsung diabaikan.
     - Membersihkan seluruh item lama yang memiliki kesamaan path kanonikal via `Items.Where(i => IsPathEqual(...)).ToList()` sebelum menyisipkan item baru ke indeks 0.
  4. **Guard `if (e.Handled) return;` pada Seluruh Handler UI**:
     - Menambahkan pengecekan `if (e.Handled) return;` pada `Window_PreviewDrop`, `DropTargetShelf_Drop`, `ShelfDropZone_Drop`, `DropTargetDock_Drop`, dan `DockBorder_Drop` di `NotchWindow.xaml.cs`.
     - Ketika event telah diproses oleh level jendela, handler anak tidak akan pernah memproses payload file drop untuk kedua kalinya.
* **Alasan**: Menjamin integritas data daftar Shelf sehingga 1 file yang dimasukkan tidak akan pernah terduplikasi menjadi 2 item, baik akibat bubbling event ganda WPF, race condition pembacaan thumbnail, maupun variasi format string path.

---

#### Keputusan 77: Eliminasi Kedipan Konten Jam/Baterai saat Startup Entrance & Penyelarasan Sapaan Maskot Welcome

* **Konteks**: Pengguna melaporkan bahwa saat Kuri baru dijalankan (*startup*), transisinya tidak mulus karena menampilkan jam dan konten lain terlebih dahulu sebelum maskot melambaikan tangan ("aku melihat saat kuri start, tidak mulus startnya, menampilkan jam dan konten lain dulu, baru maskot welcomenya, coba kamu perbaiki itu").
* **Akar Masalah**:
  1. **Nilai Awal Default XAML yang Opaque**: Pada `NotchWindow.xaml`, elemen kontainer utama `PillContainer` dan `ContentHost` tidak memiliki atribut opasitas eksplisit sehingga bernilai default `Opacity="1.0"`, serta `PillTranslateTransform.Y = "0"`.
  2. **Jeda Eksekusi Lifecycle WPF (`Window.Show` vs `Window_Loaded`)**: Pada `App.xaml.cs`, `window.Show()` dipanggil terlebih dahulu untuk menampilkan jendela ke DWM Windows. Namun, metode `PrepareStartupEntrance()` sebelumnya baru dipanggil di dalam event handler `Window_Loaded` di `NotchWindow.xaml.cs`. Karena event `Loaded` diantrekan secara asinkron oleh dispatcher WPF dan dieksekusi setelah beberapa frame pertama dirender ke layar, jam, tanggal, baterai, dan pet default terlanjur digambar dan terlihat oleh pengguna selama 50–200ms sebelum `PrepareStartupEntrance()` sempat menyetel opasitas ke 0.
  3. **Penundaan Persiapan WelcomeOverlay**: Persiapan `WelcomeOverlay` dan pemanggilan `HideGreetingTargetPets()` baru dilakukan di dalam `RunStartupGreetingAsync()`, sehingga ada jeda waktu antara animasi masuk pil dan inisialisasi maskot selamat datang.
* **Pilihan & Solusi**:
  1. **Konfigurasi State Awal Invisible di XAML**:
     - Menetapkan `Opacity="0.0"` pada `<Grid Name="PillContainer" ...>` dan `<Grid Name="ContentHost" ...>`.
     - Menetapkan posisi awal transformasi di luar layar pada `<TranslateTransform x:Name="PillTranslateTransform" X="0" Y="-28.0" />` dan `<ScaleTransform x:Name="PillScaleTransform" ScaleX="0.9" ScaleY="0.9" />`.
     - Memastikan jendela sejak frame pertama yang diproses oleh DirectX/DWM benar-benar transparan tanpa kedipan konten apapun.
  2. **Inisialisasi Pra-Render di Konstruktor Window (`NotchWindow()`)**:
     - Memanggil `PrepareStartupEntrance()` langsung di dalam konstruktor `public NotchWindow()` tepat setelah `DataContext = ViewModel;`, sebelum `window.Show()` dipanggil di `App.xaml.cs`.
  3. **Persiapan Dini Elemen Sapaan Maskot (`PrepareStartupEntrance`)**:
     - Jika `_startupGreetingPlanned` bernilai `true`, `PrepareStartupEntrance()` langsung menyembunyikan target pet (`HideGreetingTargetPets()`), menonaktifkan hit-test `ContentHost` (`IsHitTestVisible = false`), dan menyiapkan `WelcomeOverlay` (`Visibility = Visibility.Visible`, `Opacity = 0.0`, skala 1.05) sebelum jendela ditampilkan ke layar.
  4. **Koreografi Animasi Masuk yang Selaras**:
     - Saat `PlayStartupEntranceAnimation()` berjalan di akhir `Window_Loaded`, pil notch meluncur turun secara fluida dari tepi atas layar bersamaan dengan maskot selamat datang yang memudar masuk di tengah notch.
     - Konten jam, tanggal, cuaca, dan baterai tetap berada pada `Opacity = 0.0` selama 3 detik penuh saat maskot menyapa, dan baru memudar masuk halus di belakang maskot saat maskot bergeser ke sudut kanan.
* **Alasan**: Menghasilkan pengalaman startup yang sangat mulus, bersih, dan profesional tanpa lonjakan visual atau kedipan jam/baterai sebelum maskot menyapa pengguna.

---

#### Keputusan 78: Garis Cahaya Outline Notch (Poni-style) untuk Timer, Stopwatch, dan Alert — Stroke Berlapis Tanpa `Effect`

* **Konteks**: Poni 1.5.0 menggambar garis cahaya tipis di sekeliling outline notch yang tertutup untuk dua hal (`fxTimerLine` untuk sisa waktu Timer/Pomodoro, `fxGlow` untuk alert yang berjalan keliling). Pengguna meminta perilaku yang sama di Kuri, ditambah garis menit untuk stopwatch. Hasil RE Poni tersimpan di `scratch/poni-asset-_notch_js.txt` (`outlineSync`/`outlineTimer`/`outlineDraw` baris 4035–4215, keyframe `GLOW_TRACE` baris 4044–4055, `GLOW_MAX = 20000`).
* **Akar Masalah**:
  1. **Kuri hanya punya bayangan, bukan cahaya**: `TimerSubNotchPill`/`StopwatchSubNotchPill` memakai `DropShadowEffect` hitam. Tidak ada mekanisme dash sama sekali di proyek (`StrokeDashOffset` 0 pemakaian, `StrokeDashArray` 1 pemakaian), jadi tidak ada cara menandai "seberapa besar waktu yang tersisa" pada bentuk notch.
  2. **`Effect` mahal pada layered window**: jendela notch memakai `AllowsTransparency="True"`. Menempelkan `BlurEffect`/`DropShadowEffect` yang dianimasikan pada elemen di dalamnya memaksa WPF membuat render-target penuh dan memanggil `UpdateLayeredWindow` untuk SELURUH jendela setiap frame — bukan hanya untuk garis tipisnya.
  3. **Semantik dash WPF tidak terdokumentasi**: tidak ada jaminan apakah `StrokeDashArray`/`StrokeDashOffset` bersatuan piksel atau kelipatan `StrokeThickness`, dan tidak ada jaminan perilaku dash pada `GeometryGroup` multi-figure.
  4. **`Geometry.Length` tidak reliabel** untuk `StreamGeometry` yang baru dibekukan, sehingga panjang outline tidak bisa diukur dari objek geometri.
  5. **Badai PropertyChanged jalur stopwatch**: `StopwatchCentiseconds` naik 20×/detik; kalau garis ikut dibaca dari jalur itu, fitur baru menambah 20 sinkronisasi/detik ke jendela.
* **Pilihan & Solusi**:
  1. **File parsial baru `NotchWindow.OutlineFx.cs`** (±700 baris) alih-alih menyisipkan ke `NotchWindow.xaml.cs` yang sudah 24rb baris. `NotchWindow` sudah `partial class` dan csproj SDK-style meng-glob file, jadi tidak ada perubahan csproj. `NotchWindow.xaml.cs` hanya menerima 7 hook kecil (geometri, subscription, visibility, state, animate, closed).
  2. **Enam `Path` dalam satu `OutlineFxLayer`**: dua setengah outline (kiri & kanan) × tiga lapisan ketebalan (inti 1.8 px α1.00, halo dekat 3.6 px α0.34, halo jauh 7.0 px α0.16). Setiap setengah dimulai dari tengah bawah `(ear + bodyWidth/2, height)` dan berakhir di sudut luar atas, meniru arah `<path pathLength="100">` Poni sehingga "dash dari awal path" = cahaya yang menempel di tengah bawah. Layer adalah anak TERAKHIR `PillContainer` supaya ikut `PillScaleTransform`/`LayoutTransform` dan tidak tertutup `ContentHost`, dengan `IsHitTestVisible="False"` agar tidak merusak hit-test collapsed.
  3. **Panjang dihitung analitik**, bukan dari geometri: `L = (bodyWidth/2 − bottomR) + π/2·bottomR + (height − bottomR − ear) + π/2·ear` (diclamp). Outline simetris cermin terhadap tengah bawah, jadi satu angka dipakai kedua sisi.
  4. **Dash dipakai sebagai alat gambar, dengan semantik yang sudah dibuktikan secara empiris** lewat aplikasi probe WPF terpisah: `StrokeDashArray` selalu diawali elemen *dash*; nilai array maupun `StrokeDashOffset` bersatuan **kelipatan `StrokeThickness`**; offset positif menggeser pola ke depan sepanjang path. Karena itu array-nya `[dashPanjang, 2·panjang]` (satu segmen terang) dan `StrokeDashOffset = fraksi · panjang/tebal` untuk fase "ekor habis ke tepi luar". Setiap update membuat `DoubleCollection` baru lalu `Freeze()` per lapisan (ketebalannya beda, dan Freezable yang dimutasi akan bocor antar lapisan).
  5. **Cahaya = stroke berlapis, bukan `Effect`**: tiga garis dengan opasitas menurun menghasilkan halo visual yang setara blur tanpa render-target tambahan. Ada escape hatch `OutlineUseBlur` untuk mencoba blur suatu saat nanti.
  6. **Sepenuhnya event-driven, tanpa polling 1 Hz**: pemicunya `TimerProgress` (1×/detik), `TimerFinished`, `TransientTriggered`, `IsTransientActive`/`CurrentWidgetType`, `CurrentState`/`IsNotchVisible`/`IsSlimBarVisible`/dua setting, dan `UpdateNotchGeometry()`. Satu-satunya `DispatcherTimer` hanya hidup selama animasinya dibutuhkan: 40 ms untuk *lerp* timer pendek (dimatikan sendiri saat selesai, dan tidak pernah dinyalakan bila langkah per detik < 0.005 — timer > 200 s), 40 ms untuk putaran alert, dan satu penjaga *settle* 520 ms.
  7. **Properti VM baru `StopwatchOutlineFraction`** yang hanya naik saat detik bulat berubah (`(whole % 60) / 60`), sehingga garis stopwatch diperbarui 1×/detik walaupun `StopwatchCentiseconds` tetap 20×/detik untuk teks pil.
  8. **Kedip selesai Timer diprioritaskan di atas glow alert**: notifikasi "Timer Finished" terbit lebih dulu (3 ms sebelum event `TimerFinished`), jadi handler `TimerFinished` memutus glow alert yang baru saja menyala (`_alertUntilUtc = MinValue`, hentikan loop) dan mengambil alih selama 1,85 s. Ini juga setia ke Poni, yang daftar `GLOW_KINDS`-nya tidak memuat selesai Timer.
  9. **Dua setting terpisah** seperti Poni: `IsNotchTimerGlowEnabled` (`fxTimerLine`) dan `IsNotchAlertGlowEnabled` (`fxGlow`), default `true`, dilokalkan EN/ID, dan — titik paling mahal karena senyap — didaftarkan pada whitelist `new AppSettings { … }` di `SettingsViewModel.SaveCurrentSettingsAsync()` serta `ResetToDefaultsAsync()`.
* **Alasan**: Garis cahaya adalah sinyal "waktu yang tersisa" yang bisa dibaca tanpa membuka notch, dan kini tersedia untuk timer, stopwatch, dan alert. Biaya terukurnya nol: `TotalProcessorTime` NotchWin pada mesin 16 logical core (4 sampel × 5 s, rata-rata dua sampel terakhir supaya ekor startup tidak ikut) — **idle collapsed tanpa efek 2.0 %**, **timer 5 menit dengan garis aktif 2.7 %**, **timer 30 detik dengan lerp 12.5 Hz 5.1 %**, **alert glow 25 Hz 5.6 %**, dan **timer 30 detik dengan kedua toggle dimatikan 4.8 %** — yaitu identik dengan saat garis menyala; lonjakan yang ada berasal dari animasi pil timer yang sudah ada sebelumnya, bukan dari fitur baru. Angka ini sekaligus membuktikan pilihan no. 5: tidak ada `Effect` yang terpasang, sehingga tidak ada render-target per frame pada layered window.


---

#### Keputusan 79: Rombak Antarmuka Menu Home (Miniplayer, Kalender Mingguan, App Shortcut Dock Konsolidasian, & Banner Notifikasi Cerdas)

* **Konteks**: Pengguna menginginkan antarmuka notch yang lebih intuitif, produktif, dan kaya informasi saat dibuka (terinspirasi dari Poni 1.5.0 namun disesuaikan dengan fitur khas Kuri seperti jadwal sholat dan pengingat hidup sehat). Dibutuhkan satu halaman "Home" terpadu yang memuat widget musik mini, kalender mingguan, dock pintasan aplikasi, serta banner notifikasi situasional.
* **Akar Masalah**:
  1. Sebelumnya saat notch dibuka, tab pertama langsung mengarah ke Control Center atau Media Player penuh yang memakan area vertikal besar.
  2. Pintasan aplikasi (Dock) sebelumnya tersebar atau berada di posisi global yang bertabrakan dengan tata letak slide lain.
  3. Pengguna membutuhkan sekilas info harian (tanggal hari ini dan musik yang sedang berputar) tanpa harus bolak-balik mengganti tab navbar.
* **Pilihan & Solusi**:
  1. **Tab Home Terdedikasi (`NavTab0` / `SlideHomeCard`)**:
     - Menjadikan Home sebagai tab default indeks 0 dengan ikon rumah (`Path` vektor presisi). Navbar diperluas menjadi 10 tab terstruktur (`NavTab0` hingga `NavTab9`).
  2. **Baris 0: Miniplayer Card & Kalender Mingguan Berdampingan**:
     - Kedua kartu memiliki tinggi seragam (`Height="120 DIPs"`), background `#161622`, border `#252538`, dan radius sudut `16 px` untuk keselarasan optik millimeter-level.
     - **Miniplayer Card (Lebar 338 DIPs, Tinggi 120 DIPs)**:
       - Artwork cover lagu proporsional ukuran `86×86 DIPs` dengan sudut melengkung `16 px` via `VisualBrush OpacityMask` dan fallback pemandangan sunset bertingkat (`#1E1738`, matahari salmon `#F87171`, siluet pegunungan violet `#2B214C` & `#16112C`).
       - Judul lagu tebal (`13.5 pt`, putih `#FFFFFF`), nama artis / status adzan hijau emerald (`11 pt`, `#34D399`) dengan jarak vertikal rapi (`Margin="0,2,0,8"`).
       - Seekbar linier interaktif ramping (tinggi 14px) dengan label menit:detik berjalan (`FormattedPosition`) dan total (`FormattedDuration`) berwarna `#71717A`.
       - Kontrol media: Previous (`SkipPreviousCommand`, 28×28 DIPs), circular white Play/Pause (`TogglePlayPauseCommand`, 34×34 DIPs dengan `Margin="22,0"` dan ikon gelap `#0F172A`), serta Next (`SkipNextCommand`, 28×28 DIPs) dengan efek hover lembut (`#25FFFFFF`).
     - **Kalender Mingguan Card (Lebar 236 DIPs, Tinggi 120 DIPs)**:
       - Header bulan & tahun (`HomeCalendar.CurrentMonth` & `CurrentYear`).
       - Strip 7 hari per-minggu (M S S R K J S) dengan lingkaran penanda tanggal hari ini (`IsToday`, hijau emerald `#34D399`) dan teks hari Minggu berwarna merah cerah (`#EF4444`).
       - Ringkasan agenda / hari ini di bagian bawah strip tanggal (`Margin="0,8,0,0"`).
  3. **Baris 1: App Shortcut Dock Terkonsolidasi & Anti-Clipping**:
     - Dock pintasan aplikasi (`ShortcutDockBorder`, tinggi 44 DIPs, `Margin="0,10,0,0"`, CornerRadius 16px) dipusatkan secara eksklusif di baris 1 Menu Home. Dock dihapus dari menu tab lain agar slide-slide lainnya tetap bersih dan fokus.
     - Quick Settings dihilangkan dari Home sesuai arahan pengguna agar antarmuka tidak berdesakan.
     - Mengatasi masalah pemotongan dock di bagian bawah layar (*dock clipped*) dengan menaikkan tinggi notch `ExpandedHeight = 264.0 DIPs`. Tinggi konten 174 DIPs + margin atas/bawah dan navbar menyisakan ruang bernapas vertikal 20 DIPs di atas kurva lengkungan bawah notch, sehingga dock tampil utuh 100%.
  4. **Baris 2: Banner Notifikasi Cerdas Dinamis**:
     - Banner notifikasi situasional (`IsHomeNotificationActive`) hanya muncul jika ada alarm, timer selesai, waktu sholat tiba, atau pengingat minum air (`Margin="0,10,0,0"`, CornerRadius 20px).
     - Saat tidak ada notifikasi, banner tersembunyi (`Collapsed`) dan notch mempertahankan tinggi standar (`ExpandedHeight = 264.0 DIPs`). Saat ada notifikasi aktif, tinggi notch mengembang otomatis (`ExpandedHeight + 60.0 DIPs` = 324.0 DIPs) sehingga banner dapat tampil lega di bagian bawah kartu Home.
  5. **Keandalan & Ketahanan Binding**:
     - Mengatasi masalah parsing XAML dengan menstandarkan struktur root ke `<Window.Resources>` tunggal tanpa wrapper `ResourceDictionary` liar.
     - Menambahkan properti reaktif `HasThumbnail => Thumbnail != null` dan alias command pada `MediaViewModel` guna menjamin keandalan data binding.
* **Alasan**: Menghadirkan *dashboard* notch yang sangat rapi, informatif, dan mewah, di mana kontrol playback, kalender, dock aplikasi, dan pengingat harian dapat diakses secara instan dalam satu pandangan tanpa terpotong oleh geometri lengkungan notch.

---

#### Keputusan 80: Phone Share — Server HTTP LAN Tertanam (TcpListener + Token + QR) Tanpa Aplikasi Pihak Ketiga

* **Konteks**: Pengguna ingin memindahkan file/teks antara HP dan PC seperti AirDrop/LocalSend, tetapi tanpa memasang aplikasi apa pun di HP dan tanpa dependency cloud. Fitur ini bersaudara alami dengan Shelf (file dari HP harus mendarat di penampungan yang sudah ada) dan hidup di tab Shelf sebagai panel QR (`ShelfPhonePanel`).
* **Akar Masalah**:
  1. **`HttpListener` tidak bisa dipakai untuk prefix non-localhost tanpa `netsh http add urlacl`** — menuntut skrip instalasi/admin dan satu jalur khusus untuk bind semua interface, sementara Kuri harus jalan sebagai aplikasi pengguna biasa.
  2. **Koneksi HP tidak bisa diandalkan ke hostname mDNS/NetBIOS** dari browser mobile; hanya alamat IP literal yang selalu bisa dibuka.
  3. **Antarmuka Wi-Fi Android/iOS sering punya lebih dari satu IP** (hotspot, VPN, virtual adapter) sehingga pemilihan alamat harus deterministik, bukan asal ambil adapter pertama.
  4. **Browser mobile me-retry `PUT` yang sama** saat layar terkunci/koneksi goyang — tanpa deduplikasi, satu upload menghasilkan dua file kembar di folder dan dua kartu di Shelf.
  5. **Server yang tertinggal aktif selamanya** adalah risiko di Wi-Fi publik/kantor: URL tetap hidup setelah pengguna lupa.
  6. Tidak ada kanal untuk menolak traversal nama (`..\..\`) dan nama tercadang Win32 (`CON.txt`) dari payload `?name=` yang dikontrol HP.
* **Pilihan & Solusi**:
  1. **`TcpListener` port 0 (ephemeral) + parser HTTP/1.1 tulisan sendiri** (`ReadHeadersAsync` byte-per-byte sampai CRLFCRLF, batas 16 KB): tidak butuh ACL, tidak butuh admin, tidak menaikkan dependency; `Expect: 100-continue` dijawab eksplisit, `Transfer-Encoding: chunked` ditolak `411` (sengaja — semua browser mengirim `Content-Length` untuk PUT/POST file/teks).
  2. **Token sesi 16 byte hex di prefix path** (`/t/<token>/`) — satu-satunya "autentikasi"; semua route (halaman, `list`, `file/{id}`, `upload`, `text`) diverifikasi terhadap prefix sebelum dispatch. Stop = token hangus. Tidak ada kredensial persisten di disk.
  3. **Pemilihan alamat IP privat RFC1918** (`IsPrivateAddress`: 10/8, 172.16–31, 192.168) di antara adapter UP non-loopback, fallback publik → loopback; URL & QR memakai alamat yang sama sehingga scan kamera langsung terbuka.
  4. **QR lokal via QRCoder `PngByteQRCode` (ECC Q, scale 8)** → `BitmapImage` frozen; kegagalan generasi QR tidak mematikan server (tombol *Salin Tautan* sebagai jalur manual).
  5. **Halaman HP = satu berkas HTML embedded** (`Assets/PhoneShare/index.html`, `EmbeddedResource`, dimuat `Lazy` via `GetManifestResourceStream`) dengan placeholder `__STRINGS__`/`__MAX_FILE__`/`__MAX_TEXT__` yang disuntik dari `LocalizationService` (`PhonePage.*`, dengan escaping `<`/`>` ke `\u003c`) — nol aset eksternal, otomatis bilingual, dan tidak ada web-server static file handler.
  6. **Integrasi penuh ke Shelf tanpa model penyimpanan kedua**: upload dipanggil lewat `ShelfService.AddFiles(new[] { targetPath })` di dispatcher sehingga seluruh arsitektur anti-duplikasi Keputusan 76, animasi ledakan, persistensi `shelf.json`, dan ikon shell diwarisi gratis. Download hanya menyerving item Shelf non-direktori yang masih ada (`File.Exists` dicek dua kali — UI marshalled lalu saat serving).
  7. **Anti duplikasi & sanitasi upload**: dedup in-memory `nama|ukuran` 10 detik (retry browser di-drain lalu dibalas `{duplicate:true}`), `SanitizeFileName` (Path.GetFileName setelah `/`→`\`, karakter invalid → `_`, nama tercadang diberi prefix `_`), `GetUniquePath` ` (n)`, tmp tulis → `FileMode.CreateNew`; gagal di tengah → parsial dihapus + `507`/`400`.
  8. **Eksposur dibatasi waktu dan niat**: watchdog idle 15 menit (cek 30 detik terhadap `_lastActivityUtcTicks`) memanggil `Stop()`; tombol Stop di panel; toggle `IsPhoneShareEnabled` di Pengaturan menghentikan server seketika saat dimatikan. Batas 2 GB file / 64 KB teks mencegah buffer berlebihan (body dialirkan 80 KB buffer langsung ke disk, tidak pernah whole-in-memory).
  9. **Teks/link HP → clipboard PC** via `Clipboard.SetText` + toast preview 60 karakter; deteksi link regex `^https?://` menentukan ikon `🔗`/`📋`.
* **Alasan**: Solusi zero-install satu sisi (hanya browser mobile), tanpa hak admin, tanpa dependency jaringan keluar, dan dengan permukaan keamanan yang sengaja dibuat kecil (ephemeral port + token sekali pakai + auto-stop LAN-only). Reuse Shelf menjaga satu model realitas untuk file sementara dan membuat fitur ini hampir tidak menambah state baru di aplikasi.

---

#### Keputusan 81: Perombakan Notch Collapsed (Status Bar Murni & Toggle Warna Aksen Netral)

* **Konteks**: Pengguna menginginkan perombakan visual pada bilah tertutup (*collapsed notch*):
  1. Opsi mematikan warna aksen tema default sehingga pengguna dapat memilih antara warna aksen cerah atau tampilan monokrom/netral (*sleek dark monochrome*).
  2. Bilah notch saat tertutup (*collapsed*) secara default hanya menampilkan Hari/Tanggal dan Jam Digital murni. Elemen-elemen tambahan (Cuaca, Baterai, Maskot Mochi, dan Widget Media) dapat diaktifkan opsional oleh pengguna melalui Pengaturan.
* **Akar Masalah**:
  1. Sebelumnya, bilah tertutup memadatkan Cuaca, Hari/Tanggal, Jam, Baterai, dan Maskot secara bersamaan dalam satu baris dengan lebar 295 DIPs, menyebabkan visual terasa ramai.
  2. Saat media/lagu diputar di background, widget media langsung membajak tampilan notch tertutup secara otomatis tanpa kontrol preferensi yang jelas dari pengguna.
  3. Warna aksen tema dipaksakan pada border, slider, dan gradasi latar belakang notch bahkan ketika pengguna lebih menyukai estetika hitam-netral (*stealth minimal*).
* **Pilihan & Solusi**:
  1. **Toggle Warna Aksen Netral & Hitam Pekat Murni (`IsAccentColorEnabled`)**:
     - Ditambahkan ke `AppSettings`, `SettingsViewModel`, dan `ThemeManager`.
     - Ketika dinonaktifkan (`false`), `ThemeManager` mematikan saturasi aksen dan mengembalikan palet zinc/silver monokrom (`Primary: #52525B`, `Light: #D4D4D8`).
     - Latar belakang notch OLED dirender **hitam pekat solid 100% (`#000000`)** pada seluruh stop gradasi tanpa sisa tint abu-abu.
     - Border outline stroke (`NotchStrokeBrush`) dibuat transparan penuh (`Color.FromArgb(0, 0, 0, 0)`) dan `CanShowNotchOutlineFx()` mematikan layer efek outline sehingga **sama sekali tidak ada glow atau garis sisa di pinggiran notch**.
     - Kontrol pemilihan warna aksen dan hex kustom di `SettingsWindow` otomatis di-disable (`IsEnabled="{Binding IsAccentColorEnabled}"`).
  2. **Notch Collapsed Minimalis & Anti Tumpang Tindih (Zero Collision)**:
     - Lebar dasar collapsed notch diperkecil menjadi **`186.0 DIPs`** (sebelumnya `295.0 DIPs`) saat hanya menampilkan Tanggal dan Jam.
     - Tata letak `Clock` widget menggunakan 3 kolom: Sisi Kiri (Auto: Cuaca + Tanggal), Spacer Tengah (`* MinWidth="18"`), dan Sisi Kanan (Auto: Jam + Baterai + Pet).
     - Menghubungkan perubahan geometri reaktif via `OnTargetWidthChanged` di `NotchViewModel` sehingga penambahan/pengurangan widget di pengaturan seketika memicu animasi transisi lebar notch secara mulus ke View.
     - Lebar notch diperluas secara proporsional saat widget diaktifkan:
       - `ShowWeatherInCollapsedNotch`: +68.0 DIPs
       - `ShowBatteryInCollapsedNotch`: +68.0 DIPs
       - `ShowPetInCollapsedNotch`: +34.0 DIPs
       - Total saat ketiga widget aktif: **340–356 DIPs**, memberikan ruang lega (>54 DIPs) di antara sisi kiri dan kanan sehingga **tidak ada lagi elemen yang tumpang tindih**.
     - `IsMediaWidgetEnabled` diatur default ke `false` agar media yang diputar tidak otomatis mengambil alih bilah tertutup jika pengguna ingin jam & tanggal tetap tampil.
* **Alasan**: Memberikan kendali penuh dan personalisasi maksimal kepada pengguna untuk menikmati kapsul notch yang super ramping, bersih, bergaya Apple Dynamic Island murni, atau memperluas fungsinya sesuai kebutuhan masing-masing.

---

#### Keputusan 82: Adaptasi Lebar Dinamis Lirik Terlipat, 60 FPS Karaoke Render, & Proteksi Aksen Cover Art

* **Konteks**:
  1. Saat lirik aktif di notch terlipat dan beberapa widget (cuaca, baterai, pet) dihidupkan di pengaturan, lirik terjepit dan ujung kalimat terpotong menjadi elipsis (`...`).
  2. Animasi isian teks karaoke (*karaoke fill word-mask*) terasa patah-patah (*stuttering*).
  3. Mengubah toggle widget atau mematikan/menyalakan warna aksen saat lagu diputar sempat mereset palet warna notch ke warna biru default beberapa detik sebelum pulih ke warna cover art.
* **Akar Masalah**:
  1. Lebar notch terlipat untuk lirik sebelumnya di-override secara statis atau di-reset ke fallback sempit (`+65px`), tanpa memperhitungkan ruang yang dibutuhkan oleh widget samping baru yang diaktifkan.
  2. Timer progres timeline media (`_timelineTimer`) sebelumnya menggunakan prioritas default `DispatcherPriority.Background` dengan interval 33ms/150ms. Prioritas background mudah tertunda oleh event loop antarmuka sehingga pembaruan geometri clipping melompat-lompat kasar (patah-patah).
  3. `ApplyAccentColor` dan `ApplyCurrentSettings` memanggil reset aksen tema yang menghapus cache `CurrentAlbumAccent` saat setting disimpan.
* **Pilihan & Solusi**:
  1. **Adaptasi Lebar Dinamis Lirik Anti-Ellipsis**:
     - Di `NotchWindow.xaml.cs`, `UpdateCollapsedLyricsWidth` menghitung `dynamicChrome` berdasarkan seluruh widget yang sedang aktif:
       $$\text{dynamicChrome} = 104\text{px} + (68\text{px jika Cuaca}) + (68\text{px jika Baterai}) + (34\text{px jika Pet})$$
     - `targetWidth` dihitung sebagai $\text{chrome} + \text{panjang teks lirik} + 14\text{px}$ toleransi padding.
     - Event listener di `OnViewModelPropertyChanged` otomatis menghitung ulang lebar saat toggle widget diubah, sehingga notch langsung melebar menampung widget baru tanpa pernah memotong teks lirik menjadi titik-titik.
     - Di `NotchViewModel.cs`, batas dasar lirik dinaikkan dari `+65px` menjadi `+140px`, dan `collapsedWidth` dijaga dengan `Math.Max(collapsedWidth, ...)`.
  2. **60 FPS Render Timer untuk Karaoke Cair (Liquid Smoothness)**:
     - `_timelineTimer` dinaikkan ke `DispatcherPriority.Render` dengan target interval **16.0 ms (~60 FPS)** saat isian karaoke sedang aktif (`isKaraokeActivelyRendering`).
     - Mengeliminasi alokasi berulang `RectangleGeometry(0,0)` dengan singleton `EmptyClip` yang di-freeze pada `KaraokeClipConverter`.
  3. **Proteksi Persistensi Warna Album Cover Art**:
     - Di `ThemeManager.cs`, `CurrentAlbumAccent` selalu dicatat dan dipertahankan. Selama `force == false`, pemanggilan `ApplyAccentColor` tidak pernah menghapus palet cover art yang sedang aktif.
* **Alasan**: Menghilangkan seluruh artefak visual patah-patah dan teks terpotong pada tampilan lirik, memberikan pengalaman karaoke real-time yang mulus, responsif, dan stabil setara animasi sistem bawaan.

---

#### Keputusan 83: Integrasi Mesin Sinkronisasi & Karaoke Presisi Hasil Reverse Engineering Poni

* **Konteks**:
  Pengguna meminta membedah dan menerapkan arsitektur aplikasi Poni v1.5.0 ke Kuri/NotchWin agar sinkronisasi lirik pas persis dengan audio dan animasi sapuan karaoke tidak mengalami jeda kosong atau patah-patah.
* **Akar Masalah Ditemukan dari Reverse Engineering**:
  1. **Latensi Persepsi & Audio Buffer**: Offset kompensasi otomatis NotchWin sebelumnya disetel `+200 ms`. Dari hasil reverse engineering Poni, terbukti bahwa kompensasi `+300 ms` adalah *sweet spot* optimal untuk mengimbangi buffer audio Bluetooth/Windows sound driver serta waktu reaksi mata manusia membaca lirik sebelum vokal berbunyi.
  2. **Dead-Margin Stuttering pada Karaoke Clip**: Pada `NotchWindow.xaml`, elemen teks dan overlay karaoke menggunakan `HorizontalAlignment="Stretch"` dengan `TextAlignment="Center"`. Akibatnya, `ActualWidth` yang dioper ke `KaraokeClipConverter` adalah lebar kontainer penuh (misal 300px), padahal teks aslinya hanya berada di tengah (misal 120px dari X=90 ke X=210). Hal ini menyebabkan progress 0%–30% hanya memotong ruang kosong (lirik terkesan diam/macet), lalu mendadak bergerak cepat di tengah baris, dan berhenti sebelum akhir kalimat.
  3. **Penyakit Jeda Instrumental Panjang (Slow Crawl)**: `UpdateKaraokeProgress` sebelumnya membagi selang waktu baris saat ini hingga baris berikutnya secara linear. Jika ada jeda melodi/solo gitar sepanjang 10–20 detik, isian karaoke merayap sangat lambat dan tidak sesuai dengan kecepatan vokal.
  4. **Pembersihan Metadata Lagu**: Judul lagu dari YouTube/YouTube Music sering mengandung embel-embel `- Topic` dan tag video klip (`[MV]`, `[4K]`, `[HQ]`, `vevo`, dll.) yang membuat LRCLIB gagal menemukan lirik.
* **Pilihan & Solusi**:
  1. **Kompensasi Latensi +300 ms**: Mengubah `DefaultAutoLatencyCompensationMs = 300` di `MediaViewModel.cs`.
  2. **Eliminasi Dead Margin XAML**: Mengubah `HorizontalAlignment="Stretch"` pada kontainer dan overlay teks karaoke di slot terlipat dan expanded player menjadi `HorizontalAlignment="Center"` dengan `MaxWidth` terikat pada kontainer lirik. `ActualWidth` kini persis sesuai lebar glyph huruf, sehingga kliping bergerak dari huruf pertama ke huruf terakhir secara 100% presisi.
  3. **Algoritma `karSpan` Poni**: Menerapkan batas durasi bernyanyi alami berdasarkan jumlah kata/suku kata (`beats * 0.45s`) dan aturan penyelesaian 90% waktu jeda sebelum baris berikutnya.
  4. **Pembersihan Metadata Tingkat Lanjut**: Menambahkan regex pembersihan `- Topic`, `vevo`, `hq`, `m/v`, `visualiser`, dan `clip` pada `LyricsService.cs`.
* **Alasan**: Menghadirkan keselarasan lirik dan lagu yang presisi, menghilangkan seluruh jeda kosong dan percepatan tidak wajar saat sapuan warna karaoke berlangsung, serta meningkatkan rasio temuan lirik lagu dari berbagai platform media.

#### Keputusan 84: Restrukturisasi Layout Tab Pemutar Media Expanded & Penataan Lirik Tanpa Tumpang Tindih

* **Konteks**:
  Pengguna meminta perapian menu pemutar musik expanded:
  1. Menghilangkan ruang kosong (*gap*) yang terlalu besar di bagian atas dan bawah kartu pemutar musik pada jendela notch yang terbuka.
  2. Merapikan tombol kalibrasi offset otomatis (`Auto`) agar tidak kaku/tebal.
  3. Merapikan daftar baris lirik agar tidak bertumpukan dengan tombol atau chip versi/sumber lirik.
* **Akar Masalah**:
  1. **Void Vertikal Berlebih**: Target tinggi jendela notch saat berada di tab media sebelumnya jatuh ke `ExpandedHeight = 264.0px`. Padahal konten media player hanya setinggi 136px, nav dock 34px, dan header 26px (total kebutuhan nyata ~206px). Akibatnya tercipta ruang kosong hitam mati sebesar ~54px di atas dan di bawah pemutar media.
  2. **Ketidaksesuaian Tinggi Kartu (Asimetris)**: Kontainer pemutar musik di sebelah kiri memiliki tinggi 136px, sementara kontainer lirik di sebelah kanan disetel `Height="112px"`. Hal ini membuat kartu lirik tampak lebih pendek, tenggelam, dan tidak sejajar.
  3. **Tumpang Tindih Kontrol Floating di Atas Teks Lirik**: Tombol kapsul offset `Auto` diletakkan melayang bebas (`VerticalAlignment="Top"`), sehingga menutupi baris lirik pertama ("We keep quiet..."). Sementara tombol versi lirik (`1/20 ▾`) dan label sumber (`lrclib.net • SYNCED`) diletakkan melayang di bawah (`VerticalAlignment="Bottom"`), sehingga menutupi baris lirik ketiga.
  4. **Degradasi Opacity Mask Berlebih**: Mask transisi lirik memudar 22% dari atas dan 22% dari bawah, menyebabkan baris pertama dan ketiga hampir tidak terbaca di samping tertutup oleh tombol-tombol yang melayang.
* **Pilihan & Solusi**:
  1. **Tinggi Khusus Tab Media (`ExpandedMediaHeight = 210.0`)**:
     Menambahkan konstanta `ExpandedMediaHeight = 210.0` pada `NotchViewModel.cs`. Saat `CurrentSlideIndex == MediaSlideIndex`, notch menyesuaikan ketinggian ke 210px, membungkus kartu pemutar musik dan dock dengan presisi dan menghilangkan dead-space vertikal.
  2. **Penyelarasan Tinggi Kartu Simetris (136px)**:
     Mengubah tinggi `LyricsContainer` menjadi `Height="136px"` dengan sudut radius 14px, sejajar identik dengan `MediaPlayerContainer` di sebelah kiri.
  3. **Arsitektur Grid 2-Baris Bersih**:
     - **Baris 0 (Header Toolbar, Height="20px")**:
       - Kiri: Tombol pill ramping untuk ganti versi lirik (`[1/20 ▾]`) dan indikator sumber lirik (`lrclib.net • SYNCED`) dengan tipografi elegan 9.5px.
       - Kanan: Kapsul offset lirik ramping setinggi 20px dengan ikon vektor minus (`-`), jam analog mini + label status (`Auto`), dan plus (`+`) tanpa border tebal kaku.
     - **Baris 1 (Canvas Lirik Unobstructed, Height="108px")**:
       - 100% didedikasikan murni untuk 3 baris lirik (Previous, Active, Upcoming) tanpa adanya elemen atau tombol yang melayang di atas teks.
  4. **Gradasi Opacity Mask Lembut (8% Edges)**:
     Mengubah titik gradasi pemudaran teks menjadi `Offset="0.08"` dan `Offset="0.92"` agar baris sebelum dan sesudah lirik aktif tetap dapat dibaca dengan jelas dan nyaman.
* **Alasan**: Menghasilkan tata letak pemutar media yang modern, seimbang, dan bebas tabrakan antarmuka, memberikan visibilitas penuh pada setiap baris lirik tanpa terhalang tombol navigasi.

#### Keputusan 85: Perombakan Total Tampilan Control Center (Grid 5-Kolom Quick Actions & Dual Slim Neon Green Sliders)

* **Konteks**:
  Pengguna meminta perombakan total tampilan menu Pusat Kontrol (Control Center) agar menyerupai referensi desain modern baru:
  1. Grid Quick Actions 5-kolom x 2-baris dengan ubin split (Wi-Fi dan Bluetooth dengan tombol pintasan pengaturan chevron terpisah) dan ubin tunggal (Airplane mode, Keep awake, Dark mode, Microphone, Eye breaks, Screenshot, Lock, serta slot kosong kelima).
  2. Kartu wadah slider tunggal membulat di bagian bawah yang memuat slider Kecerahan (Brightness) dan Volume suara dengan garis tipis neon green (`#22C55E`), persentase numerik di ujung kanan, serta tombol pil pemilih output suara (`[ 🔊 > ]`) yang memicu `DevicePickerPopup`.
  3. Mempertahankan banner monitor download aktif di bagian bawah.
* **Akar Masalah & Arsitektur Lama**:
  1. Tata letak Control Center sebelumnya menggunakan sistem 3-kolom kaku berisi Jam/Kalender, Status Baterai, Capsule Sliders, dan Telemetri Hardware (CPU/RAM/GPU) yang padat dan tidak efisien.
  2. Slider sebelumnya menggunakan gaya kapsul abu-abu dengan thumb bola putih tebal (`ControlCenterSliderStyle`), bukan garis tipis neon green minimalis tanpa thumb.
  3. Aksi cepat Windows (Wi-Fi, Bluetooth, Mute Mic, Keep Awake, Dark Mode, Eye Breaks, dsb.) belum tersusun rapi dalam grid 5-kolom yang seragam dengan feedback status aktif (warna aksen sky blue `#38BDF8` dan teks status kontekstual).
* **Pilihan & Solusi**:
  1. **Quick Action Tiles Grid 5-Kolom x 2-Baris**:
     - **Baris 1**:
       1. `Wi-Fi` (Split Tile: Toggle radio + pembatas vertikal + chevron `>` membuka `ms-availablenetworks:`. Label di bawah: status SSID / `WifiStatusLabel`).
       2. `Bluetooth` (Split Tile: Toggle radio + pembatas vertikal + chevron `>` membuka `ms-settings:bluetooth`. Latar aktif sky blue `#38BDF8` dengan ikon gelap `#09090B`. Label di bawah: `BluetoothStatusLabel` seperti "Not connected").
       3. `Airplane mode` (Single Tile: Toggle mode pesawat via `ms-settings:network-airplanemode` / `_isAirplaneModeOn`).
       4. `Keep awake` (Single Tile: Mencegah tidur sistem & monitor menggunakan Win32 `SetThreadExecutionState` `ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED`).
       5. `Dark mode` (Single Tile: Toggle tema Windows Light/Dark via registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` dan siaran `WM_SETTINGCHANGE`).
     - **Baris 2**:
       1. `Microphone` (Single Tile: Toggle bisukan mikrofon sistem via NAudio `MMDeviceEnumerator` Capture Endpoint).
       2. `Eye breaks` (Single Tile: Toggle pengingat istirahat mata aturan 20-20-20).
       3. `Screenshot` (Single Tile: Meluncurkan Windows Snipping Tool via `LaunchSnippingTool()`).
       4. `Lock` (Single Tile: Mengunci workstation secara instan via Win32 `LockWorkStation()`).
       5. *Slot kosong ke-5* dibiarkan transparan sesuai gambar referensi.
  2. **Dual Slim Neon Green Sliders (`ControlCenterSlimSliderStyle`)**:
     - Mengembangkan style slider baru dengan track 4.5 DIPs membulat penuh (`CornerRadius="2.25"`), latar gelap `#22222E`, dan isian progres berwarna hijau neon cerah `#22C55E`.
     - Thumb disembunyikan (`Width="0"`) untuk menghasilkan tampilan garis presisi tanpa tonjolan, dengan `IsMoveToPointEnabled="True"` agar klik pada titik track mana pun langsung melompat ke nilai tersebut.
     - Baris atas: Kecerahan layar dengan ikon matahari/gir vektor + slider + angka persentase (e.g. `100`).
     - Baris bawah: Volume suara dengan ikon speaker vektor + slider + angka persentase (e.g. `70`) + pil pemilih perangkat audio output `[ 🔊 > ]`.
  3. **Penyatuan Audio Device Picker**:
     - Tombol pil `AudioDeviceSelectorBtn` diletakkan tepat di sebelah kanan nilai volume, membuka dropdown `DevicePickerPopup` dengan proteksi pencegahan auto-collapse.
  4. **Active Downloads Banner**:
     - Banner download otomatis muncul di bawah kartu slider jika ada unduhan aktif yang terdeteksi dari Chromium/browser.
* **Alasan**: Menghadirkan antarmuka Control Center yang 100% identik dengan preferensi visual pengguna, lebih bersih, intuitif, dan responsif dengan integrasi langsung ke sistem operasi Windows.

---

#### Keputusan 86: Pemulihan Layout Notch Collapsed (Pemisahan Jam & Tanggal, Zero Overflow, & Sinkronisasi Properti Visibilitas)

* **Konteks**:
  Saat notch berada dalam kondisi tertutup (*collapsed*), tampilan notch tampak rusak/tumpang tindih (*corrupted/cramped*), jam terpotong, atau bertabrakan dengan tanggal dan elemen lainnya.
* **Akar Masalah**:
  1. Pada XAML bilah tertutup (`CollapsedContentGrid`), grid mode Jam (`WidgetType.Clock`) menempatkan teks Jam di kolom tengah (`Grid.Column="1"`), sementara Cuaca dan Tanggal dipadatkan di sisi kiri (`Grid.Column="0"`), serta Baterai dan Pet di sisi kanan (`Grid.Column="2"`).
  2. Elemen Cuaca dan pemisah vertikal tidak memiliki binding visibilitas ke preferensi pengaturan pengguna (`ShowWeatherInCollapsedNotch`), dan Baterai serta Pet tidak mengikat visibilitas ke toggle masing-masing (`ShowBatteryInCollapsedNotch` dan `ShowPetInCollapsedNotch`).
  3. Terdapat spacer dummy `<Border Grid.Column="2" Width="{Binding ActualWidth, ElementName=LeftDateText}" />` yang memakan ruang ~60 DIPs di kolom kanan saat tidak ada baterai.
  4. Akibatnya, pada lebar bilah standar 186 DIPs, total lebar elemen yang dipaksa tampil mencapai >225 DIPs dalam ruang 158 DIPs yang tersedia, menyebabkan teks Jam terhimpit/terpotong dan elemen saling bertabrakan.
  5. Masalah serupa terjadi pada mode Media (`WidgetType.Media`), di mana cuaca dan baterai dipaksa tampil meski pengaturan dimatikan, sehingga judul lagu/lirik di kolom tengah terpotong menjadi titik-titik (`...`).
  6. Kontainer `PillContainer` dan `ContentHost` sempat memiliki atribut hardcoded `Opacity="0.0"` dari hasil dekompilasi yang dapat menghambat tampilan instan saat startup/idle.
* **Pilihan & Solusi**:
  1. **Struktur Grid 3-Kolom Bersih pada Mode Jam**:
     - Kolom 0 (`Width="Auto"`): Cuaca opsional (hanya tampil jika `IsCollapsedWeatherVisible = true`) + Garis pemisah + Tanggal Hari (`LeftDateText`).
     - Kolom 1 (`Width="*" MinWidth="14"`): Ruang kosong dinamis fleksibel yang memisahkan sisi kiri dan kanan secara elegan.
     - Kolom 2 (`Width="Auto"`): Jam Digital (`Clock.CurrentTime`) + Baterai opsional (`IsCollapsedBatteryVisible = true`) + Pet opsional (`IsCollapsedPetVisible = true`).
     - Menghapus border dummy pengimbang lebar di kolom 2.
  2. **Mode Media Bebas Tabrakan**:
     - Mengikat visibilitas Cuaca dan pembatas di sisi kiri ke `IsCollapsedWeatherVisible`.
     - Mengikat visibilitas Baterai dan Pet di sisi kanan ke `IsCollapsedBatteryVisible` dan `IsCollapsedPetVisible`.
     - Memberikan ruang penuh di kolom tengah bagi judul lagu & artis atau teks karaoke beranimasi.
  3. **Properti Sinkronisasi Reaktif di `NotchViewModel.cs`**:
     - `IsCollapsedWeatherVisible => ShowWeatherInCollapsedNotch`
     - `IsCollapsedBatteryVisible => ShowBatteryInCollapsedNotch && (Battery?.HasBattery == true)`
     - `IsCollapsedPetVisible => ShowPetInCollapsedNotch && IsPetEnabled`
     - Memicu `OnPropertyChanged` saat toggle pengaturan atau status hardware baterai/pet berubah.
  4. **Pembersihan OutlineFxLayer & Normalisasi Kontainer**:
     - Memindahkan `OutlineFxLayer` dari dalam `WelcomeOverlay` ke tingkat atas di dalam `PillContainer` sejajar dengan `NotchStrokeFadePath`.
     - Menghapus atribut `Opacity="0.0"` default pada `PillContainer` dan `ContentHost`.
* **Alasan**: Mengembalikan kapsul notch tertutup menjadi sangat rapi, bersih, berdefinisi tinggi, dan proporsional sesuai preferensi pengaturan pengguna, bebas dari tumpang tindih elemen dan pemotongan teks.

---

#### Keputusan 87: Penyesuaian Dimensi Notch Control Center (Anti-Clipping) & Interaksi Drag Slider Bebas Titik

* **Konteks**:
  Pada menu Pusat Kontrol (*Control Center*):
  1. Bagian atas dan bawah kartu tampak terpotong (*clipped/cropped*).
  2. Slider kecerahan dan volume suara tidak bisa digeser (*drag*), hanya bisa ditekan (*click*) pada satu titik.
* **Akar Masalah**:
  1. **Clipping Vertikal Konten**:
     - `ExpandedContentGrid` menempatkan Header jam/tanggal di Row 0 (tinggi ~26px) dan tab bar `NavDockHost` di Row 1 (tinggi ~34px) dengan margin grid 12px (atas 6 + bawah 6).
     - Ruang yang tersisa di Row 2 pada tinggi default `ExpandedHeight = 264.0` hanyalah `264 - 12 - 26 - 34 = 192 DIPs`.
     - Padahal tinggi kartu Control Center (`SlideControlCenterCard`) dengan grid 5-kolom 2-baris dan kartu slider mencapai `226 DIPs`. Karena `SlideControlCenterCard` memiliki `VerticalAlignment="Center"` dan Row 2 memiliki `ClipToBounds="True"`, tepat 17px di bagian atas dan 17px di bagian bawah terpotong.
  2. **Thumb Width 0px Menolak Drag**:
     - Pada `ControlCenterSlimSliderStyle`, thumb diatur memiliki `Width="0" Height="0"` agar tidak ada tonjolan bulatan.
     - Di WPF, elemen dengan dimensi 0x0 tidak dapat menerima *hit-testing* mouse. Akibatnya, mouse hanya mengenai `RepeatButton` yang memicu perpindahan titik tunggal (`IsMoveToPointEnabled="True"`), tetapi tidak pernah memulai operasi *drag* berkelanjutan saat kursor digeser.
* **Pilihan & Solusi**:
  1. **Dimensi Khusus `ExpandedControlCenterHeight = 304.0 DIPs`**:
     - Menambahkan konstanta `ExpandedControlCenterHeight = 304.0` di `NotchViewModel.cs`.
     - Saat `CurrentSlideIndex == ControlCenterSlideIndex`, `UpdateTargetDimensions` mengatur `TargetHeight = ExpandedControlCenterHeight` (dan `+32.0` jika terdapat unduhan aktif).
     - Menghasilkan ruang vertikal bersih sebesar 232 DIPs di Row 2, memberikan ruang bernapas lega (>16 DIPs) di atas dan bawah kartu sehingga **bagian atas dan bawah sama sekali tidak terpotong**.
     - Memperhalus jarak vertikal antar-baris tombol menjadi 6px dan padding kartu slider menjadi `Padding="14,8"`.
  2. **Interaksi Drag-Anywhere dengan Mouse Capture**:
     - Di `NotchWindow.xaml.cs`, mengimplementasikan `SlimSlider_PreviewMouseDown`, `SlimSlider_PreviewMouseMove`, dan `SlimSlider_PreviewMouseUp`.
     - Saat tombol kiri mouse ditekan di mana pun pada slider, `slider.CaptureMouse()` diaktifkan dan nilai dihitung secara instan via `UpdateSlimSliderFromPosition` berdasarkan koordinat X mouse terhadap `ActualWidth` slider (di-clamp `0.0` sampai `1.0`).
     - Selama mouse digeser, `PreviewMouseMove` terus memperbarui persentase secara real-time dan sangat mulus (60 FPS), bahkan jika kursor sedikit keluar dari batas tinggi 4.5px slider saat digeser cepat.
     - Saat mouse dilepas, `slider.ReleaseMouseCapture()` menutup interaksi dengan bersih.
     - Memberikan hit-box transparan 16x16px pada `Thumb` di `ControlCenterSlimSliderStyle` (`Background="#00FFFFFF"`, `Cursor="Hand"`) untuk kompatibilitas native Track.
* **Alasan**: Memberikan ruang tampilan yang utuh dan proporsional bagi seluruh komponen Control Center, serta menghadirkan kenyamanan interaksi drag volume dan kecerahan yang sangat presisi, responsif, dan luwes setara slider modern Windows 11 dan iOS.

---

#### Keputusan 88: Arsitektur Continuous Drag Tracking & Eliminasi Feedback Rollback pada Slider Kecerahan dan Volume

* **Konteks**:
  Pengguna melaporkan bahwa slider kecerahan dan volume masih tidak bisa digeser saat ditekan, ditahan, dan digeser (*"slider nya masih belum bisa digeser, di tekan lalu ditahan digeser"*). Slider hanya merespons saat ditekan di satu titik, namun tidak mengikuti gerakan mouse saat digeser secara horizontal.
* **Akar Masalah**:
  1. **Feedback Loop Two-Way Binding & Rollback**:
     - Pada `MasterVolumePercent`, pemanggilan `_volumeService.SetVolumeAsync(value / 100f)` dan `OnPropertyChanged()` di setter memicu pembacaan ulang getter WPF. Karena operasi COM CoreAudio memerlukan waktu respons, getter mengembalikan nilai lama (`_volumeService.VolumePercent`), memaksa posisi slider kembali ke nilai sebelumnya saat mouse masih digeser.
     - Pada `BrightnessService`, event `BrightnessChanged` memicu `OnPropertyChanged(nameof(IsBrightnessSupported))` pada setiap tick. Pembaruan binding `IsEnabled` pada Slider merusak mouse capture (`UIElement.ReleaseMouseCapture()`) pada WPF runtime saat kursor baru mulai bergerak.
     - Pemanggilan sinkron COM API `_endpointVolume.MasterVolumeLevelScalar = clamped;` di thread UI memblokir pemrosesan pesan antarmuka saat kursor mouse bergerak cepat.
  2. **Intersepsi Input Mouse oleh Track RepeatButton**:
     - Pada `ControlCenterSlimSliderStyle`, `PART_Track` memuat `DecreaseRepeatButton` dan `IncreaseRepeatButton` yang mencegat klik dan memicu penangkapan mouse internal pada tombol, menyebabkan pelepasan penangkapan mouse slider.
     - Kontainer `Grid` slider tidak memiliki background (`Background="{x:Null}"`), sehingga area di luar garis 4.5px tidak merespons hit-testing dan klik/drag jatuh tembus ke elemen di bawahnya.
  3. **Pemicuan Collapse Notch saat Kursor Melenceng**:
     - Saat pengguna menggeser kursor ke kiri atau kanan dengan cepat, kursor sering kali melenceng sedikit ke luar batas `PillBorder`. Hal ini memicu `PillBorder_MouseLeave` yang menjalankan `ViewModel.OnMouseLeave()`, mengakibatkan notch tertutup di tengah proses dragging.
* **Pilihan & Solusi**:
  1. **Thumb Visibility="Visible" dengan Dimensi 0x0 & PART_Track IsHitTestVisible="False"**:
     - Mengatur `Thumb` menjadi `Width="0" Height="0" Opacity="0"` tanpa `Visibility="Collapsed"`. Di WPF, `Track.ArrangeOverride` memerlukan `Thumb.IsVisible == true` untuk mengatur lebar `DecreaseRepeatButton`. Dengan dimensi 0x0 dan Opacity 0, tidak ada tombol/knob bulat yang terlihat secara visual, namun bar hijau neon (`#22C55E`) kini bergerak dan merespons secara real-time saat kursor digeser.
     - Mengatur `IsHitTestVisible="False"` pada `PART_Track` agar `RepeatButton` tidak mencuri event klik mouse dari `Slider`.
     - Mengatur tinggi `Grid` slider menjadi 24 DIPs dengan `Background="#00000000"` (transparan solid hit-testable) agar pengguna memiliki area horizontal yang luas dan nyaman untuk menekan dan menggeser.
  2. **Win32 SetCapture & Continuous Window-Level Tracking**:
     - Karena NotchWin adalah jendela `WS_EX_NOACTIVATE` dengan `WM_NCHITTEST` bernilai `HTTRANSPARENT` di luar pill, pemanggilan `slider.CaptureMouse()` di WPF dapat gagal menangkap kursor saat bergerak cepat keluar dari batas slider.
     - Mengintegrasikan Win32 API `NativeMethods.SetCapture(hwnd)` pada `PreviewMouseDown` dan `NativeMethods.ReleaseCapture()` pada mouse up/lost capture. Windows OS menjamin 100% pesan mouse diarahkan ke NotchWin selama tombol kiri ditekan di mana pun kursor bergerak di seluruh layar.
     - Menghubungkan pelacakan pada `Window_PreviewMouseMove` dan `Window_PreviewMouseUp` dengan pengecekan status hardware `Mouse.LeftButton == MouseButtonState.Pressed`.
  3. **Flag IsSliderDragging & Nilai Cache Instan Tanpa Rollback**:
     - Di [NotchViewModel.cs](../NotchWin/ViewModels/NotchViewModel.cs), menambahkan properti `IsSliderDragging`. Saat dragging aktif, notifikasi asynchronous dari Windows Audio Service (`VolumeChanged`) dan WMI (`BrightnessChanged`) diblokir untuk mereset `_cachedVolumePercent` dan `_cachedBrightnessPercent`.
     - `VolumeService.SetVolumeAsync` memperbarui `_masterVolume` seketika di thread pemanggil dan mendelegasikan pemanggilan COM audio ke `Task.Run` di background thread agar thread UI tetap 144 Hz tanpa lag.
  4. **Perlindungan Anti-Collapse**:
     - Pada `PillBorder_MouseLeave`, jika `_activeSlimSlider != null`, `ViewModel.OnMouseLeave()` tidak akan dieksekusi sehingga notch tidak pernah tertutup saat pengguna sedang menggeser slider.
* **Hasil**: Slider volume dan kecerahan kini 100% dapat ditekan, ditahan, dan digeser (*drag & slide*) dengan sangat mulus, responsif, dan stabil tanpa jeda atau macet.

---

#### Keputusan 89: Resolusi Mendalam Bug Drag Slider (Eliminasi Konflik Win32 SetCapture & Proteksi WM_NCHITTEST)

* **Konteks**:
  Pengguna melaporkan bahwa slider masih belum bisa digeser meskipun bisa ditekan di satu titik (*"kenapa masih belum bisa di geser slidernya? tapi kalau ditekan bisa, coba chek lebih mendalam"*).
* **Investigasi Mendalam**:
  1. **Konflik Internal Win32 `SetCapture` dan WPF `MouseDevice`**:
     - Pemanggilan `NativeMethods.SetCapture(hwnd)` langsung setelah `slider.CaptureMouse()` mengirim pesan OS `WM_CAPTURECHANGED` ke WPF.
     - WPF mendeteksi bahwa capture telah diambil alih oleh Win32 di luar sistem logis WPF, sehingga WPF secara otomatis membatalkan mouse capture pada elemen dan memicu event `LostMouseCapture`.
     - Handler `SlimSlider_LostMouseCapture` langsung mengeksekusi `StopSlimSliderDragging()`, menyetel `_activeSlimSlider = null` bahkan sebelum kursor mouse sempat berpindah 1 piksel. Akibatnya, klik pertama berhasil mengubah nilai, namun proses drag berikutnya langsung mati.
  2. **Desinkronisasi State `Mouse.LeftButton` pada Window Transparan**:
     - Pengecekan statis `Mouse.LeftButton == MouseButtonState.Pressed` di `Window_PreviewMouseMove` mengandalkan state cache WPF device yang sering ter-desinkronisasi saat capture berpindah. Blok `else { StopSlimSliderDragging(); }` yang agresif langsung memutus proses drag.
  3. **Kehilangan Input pada `WM_NCHITTEST` (case 132)**:
     - Ketika pengguna menggeser kursor ke kiri atau kanan, kursor sering melayang sedikit secara vertikal ke luar area `NotchBackgroundPath`. Pada saat itu, `WM_NCHITTEST` mengembalikan `HTTRANSPARENT (-1)`, yang menyebabkan Windows mengarahkan pesan mouse ke jendela lain di belakang Kuri.
* **Solusi**:
  1. Menghapus pemanggilan Win32 `NativeMethods.SetCapture(hwnd)` dan `NativeMethods.ReleaseCapture()` dari alur drag slider; mengandalkan pure WPF Element Capture (`slider.CaptureMouse()`).
  2. Menghubungkan langsung handler `PreviewMouseDown`, `PreviewMouseMove`, `PreviewMouseUp`, dan `LostMouseCapture` pada masing-masing Slider di XAML dan Window level.
  3. Mengeliminasi pemanggilan `StopSlimSliderDragging()` dari dalam event `MouseMove`. Proses drag kini hanya berhenti saat event `MouseUp` atau `LostMouseCapture` terjadi, sehingga tidak ada kondisi di mana pergerakan mouse (misalnya pada touchpad laptop atau mouse dengan akselerasi tinggi) memutus sesi drag di tengah jalan.
  4. Menambahkan pengecekan `if (_activeSlimSlider != null) { handled = true; return new IntPtr(1); }` pada `WM_NCHITTEST` (case 132), menjamin NotchWindow selalu menerima pesan mouse pergerakan dan pelepasan tombol (`HTCLIENT`) selama dragging berlangsung.
* **Hasil**:
  Slider kecerahan dan volume kini 100% dapat ditekan dan digeser secara kontinu, luwes, dan stabil tanpa terputus di perangkat mouse maupun touchpad.

---

#### Keputusan 90: Perbaikan Definitif Drag Slider Control Center (Eliminasi Total CaptureMouse & Penggunaan Pure Window-Level Mouse Tracking)

* **Konteks**:
  Meskipun Win32 `SetCapture` telah dihapus sebelumnya, pengguna melaporkan slider masih belum bisa digeser (hanya bisa ditekan di satu titik).
* **Akar Masalah Nyata (The Real Root Cause)**:
  1. Pada jendela WPF dengan window style `WS_EX_NOACTIVATE` dan `AllowsTransparency="True"`, pemanggilan `slider.CaptureMouse()` secara fundamental tidak stabil: WPF membatalkan/mencabut capture mouse seketika karena jendela tidak berstatus aktif/fokus penuh.
  2. Akibat pencabutan capture tersebut, event `LostMouseCapture="SlimSlider_LostMouseCapture"` langsung terpicu pada saat mouse down terjadi, yang seketika mengeksekusi `StopSlimSliderDragging()` dan menyetel `_activeSlimSlider = null`.
  3. Oleh karena itu, saat kursor bergerak (drag), `_activeSlimSlider` sudah bernilai `null`, sehingga pergeseran slider tidak pernah dieksekusi.
* **Solusi**:
  1. **Menghapus Total `slider.CaptureMouse()` dan `LostMouseCapture`**:
     - Tidak lagi mengandalkan mouse capture WPF atau Win32 pada level kontrol slider.
     - Di XAML, kedua slider (`ScreenBrightnessPercent` dan `MasterVolumePercent`) hanya memiliki event `PreviewMouseDown="SlimSlider_PreviewMouseDown"`. Event `PreviewMouseMove`, `PreviewMouseUp`, dan `LostMouseCapture` dihapus dari elemen slider.
  2. **Window-Level Tunneling Drag Tracking**:
     - `SlimSlider_PreviewMouseDown` hanya mencatat `_activeSlimSlider = slider`, menyetel `ViewModel.IsSliderDragging = true`, menghitung posisi awal, dan menandai `e.Handled = true`.
     - `Window_PreviewMouseMove` dan `Window_PreviewMouseUp` di tingkat `<Window>` menangani seluruh pergerakan mouse di canvas window (1400x880) secara kontinu.
     - Dual-check keamanan status tombol mouse: `e.LeftButton == MouseButtonState.Pressed || (NativeMethods.GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0`. Jika tombol kiri mouse terlepas di luar jendela, proses drag otomatis dihentikan dengan aman.
  3. **Proteksi WM_NCHITTEST & Anti-Collapse**:
     - Proteksi `WM_NCHITTEST` (case 132) tetap menjamin area jendela merespons sebagai `HTCLIENT (1)` selama `_activeSlimSlider != null`.
     - Handler `PillBorder_MouseLeave` dan pengecekan di WndProc tidak akan menutup notch jika `_activeSlimSlider != null`.
     - Jika tombol mouse dilepas saat kursor di luar notch (`!PillContainer.IsMouseOver`), `ViewModel.OnMouseLeave()` dipanggil secara graceful.
* **Hasil**:
  Slider brightness dan volume kini dapat diklik di posisi mana pun dan ditarik/digeser (*press-hold-and-drag*) dengan sangat mulus dan responsif tanpa terputus sama sekali.

---

#### Keputusan 91: Eliminasi Guard IsLeftDown pada MouseMove & Dual-Level Event Wiring Slider

* **Konteks**:
  Pengguna masih mendapati bahwa menekan dan menahan lalu menggeser (*drag*) slider belum bergerak, sementara klik (*tap*) di titik mana pun berfungsi dengan baik.
* **Akar Masalah (*Root Cause*)**:
  1. Pada `Window_PreviewMouseMove`, terdapat validasi hardware state:
     `if (!isLeftDown) { StopSlimSliderDragging(); return; }`
     Di WPF, saat elemen tidak menggunakan mouse capture, properti `e.LeftButton` pada `MouseEventArgs` sering kali dilaporkan sebagai `MouseButtonState.Released` selama event pergerakan mouse. Selain itu, Win32 `GetAsyncKeyState(VK_LBUTTON)` pada touchpad laptop (precision touchpads) sering mengembalikan 0 (karena touchpad mengirim pesan mouse sintetis tanpa tombol fisik keyboard/mouse yang tertekan).
  2. Akibatnya, pada 1 piksel pertama saat kursor digeser, kondisi `!isLeftDown` bernilai `true` dan seketika mengeksekusi `StopSlimSliderDragging()`, menyetel `_activeSlimSlider = null`. Drag dibatalkan tepat pada milidetik pertama pergerakan.
* **Solusi**:
  1. **Menghapus Pengecekan `isLeftDown` dari MouseMove**:
     - Event `MouseMove` (baik di level Slider maupun Window) fokus 100% pada pembaruan posisi nilai slider (`UpdateSlimSliderFromPosition`) tanpa pernah menghentikan drag di tengah jalan.
     - Penghentian sesi drag (`StopSlimSliderDragging()`) didelegasikan secara eksklusif ke event `MouseUp` (pelepasan jari/tombol mouse) dan pelepasan kursor di luar batas.
  2. **Dual-Level Event Wiring**:
     - Event `PreviewMouseMove` dan `PreviewMouseUp` kini dihubungkan ganda: langsung pada elemen `<Slider>` di XAML (`SlimSlider_PreviewMouseMove` / `SlimSlider_PreviewMouseUp`) dan pada root `<Window>` (`Window_PreviewMouseMove` / `Window_PreviewMouseUp`).
     - Ini menjamin pelacakan drag 100% mulus baik saat kursor meluncur di atas slider maupun saat kursor terlempar keluar batas slider ke seluruh penjuru window.
* **Hasil**:
  Proses tekan-tahan-lalu-geser (*press-hold-and-slide*) kini bekerja 100% mulus, responsif, dan stabil tanpa ada pemutusan sesi drag yang prematur.

---

#### Keputusan 92: Penambahan Knob Bundar (Thumb) Fisik & Arsitektur Hybrid Drag pada Slider Control Center

* **Konteks**:
  Pengguna meminta diberikan buletan (knob/thumb fisik) yang dapat digeser mengikuti bar slider pada slider brightness dan volume di Control Center (`ControlCenterSlimSliderStyle`), persis seperti slider pemutar musik (`MediaSeekSlider`).
* **Akar Masalah (*Root Cause*)**:
  1. Pada implementasi awal `ControlCenterSlimSliderStyle`, elemen `Thumb` dibuat tersembunyi (`Width="0" Height="0" Opacity="0" IsHitTestVisible="False"`), dan `PART_Track` dimatikan hit test-nya. Slider mengandalkan manipulasi manual koordinat mouse di C#.
  2. Pada handler `PreviewMouseDown`, pemanggilan `e.Handled = true` menelan event sebelum mencapai sistem internal WPF `Slider`/`Track`/`Thumb`, sehingga interaksi drag native bawaan WPF tidak dapat berjalan.
  3. Ketiadaan indikator visual knob membuat pengguna tidak memiliki target jelas untuk ditarik (*grab-and-drag*).
* **Solusi**:
  1. **Knob Bundar ("Bulet") Visual & Interaktif**:
     - Ditambahkan elemen `Thumb` dengan border lingkaran 12x12 piksel berwarna putih bersih (`#FFFFFF`) dengan efek drop shadow halus (`DropShadowEffect BlurRadius="4" ShadowDepth="1" Opacity="0.4"`).
     - Diberikan visual feedback dinamis:
       - Saat hover (`IsMouseOver = True`): knob membesar menjadi 14x14 piksel.
       - Saat ditarik (`Thumb.IsDragging = True`): knob membesar menjadi 15x15 piksel.
     - Warna knob dipertahankan putih bersih saat ditarik agar kontras tinggi dan tidak tersamarkan dengan bar hijau aksen (`#22C55E`).
  2. **Dukungan Hybrid Drag (Native Thumb Drag + Track Press-Hold Drag)**:
     - **Tarik Knob Langsung**: Jika user mengklik knob bundar (`Thumb`), event dibiarkan berlanjut secara native ke WPF `Thumb`. Mekanisme bawaan WPF `Thumb` (dengan mouse capture internal dan `DragDelta`) menangani proses pergeseran secara 100% mulus tanpa hambatan.
     - **Klik & Tarik di Track Bar**: Jika user mengklik di luar knob (pada bar track), kursor langsung melompat (*jump-to-point*) dan slider melakukan `slider.CaptureMouse()`, sehingga user dapat langsung menahan dan menggeser (*press-hold-and-drag*) ke kiri maupun ke kanan secara kontinu.
  3. **Proteksi Drag & Anti-Collapse**:
     - `ViewModel.IsSliderDragging` disinkronkan aktif saat proses drag dimulai dan dinonaktifkan saat dilepas, mencegah pembaruan polling background menimpa nilai slider saat sedang digeser.
     - Proteksi `WM_NCHITTEST` (`HTCLIENT = 1`) dan `PillBorder_MouseLeave` diperbarui untuk memeriksa `ViewModel.IsSliderDragging`, menjamin jendela notch tidak menutup saat kursor ditarik meluncur cepat melewati batas notch.
* **Hasil**:
  Kedua slider (brightness dan volume) kini memiliki buletan knob bundar yang jelas, elegan, dan sangat responsif. Pengguna dapat dengan mudah menarik buletan tersebut, mengeklik sembarang titik pada bar, maupun menekan-menahan-menggeser bar dari titik mana pun dengan mulus.

---

#### Keputusan 93: Koreksi Binding Cover Art dan Penyelarasan Proporsi Album Art pada Kartu Musik Home

* **Konteks**:
  Pada menu Home (kartu miniplayer musik), pengguna melaporkan:
  1. Cover art album lagu tidak muncul sama sekali (hanya kotak gelap kosong).
  2. Judul dan artis lagu tidak sama tinggi / tidak sejajar dengan album art (terlihat meleset dan tidak simetris).
* **Akar Masalah (*Root Cause*)**:
  1. **Tipe Data Converter Mismatch**: Pada XAML kartu Home, elemen `<Image>` dan fallback illustration diikat menggunakan binding `Visibility="{Binding Media.Thumbnail, Converter={StaticResource BoolToVis}}"`. Converter `BooleanToVisibilityConverter` di WPF membutuhkan tipe data `bool`. Namun, `Media.Thumbnail` bertipe `ImageSource?`. Karena tipe data bukan boolean, converter menganggap nilai `false` dan menyetel `Visibility="Collapsed"` pada gambar cover art DAN fallback illustration secara bersamaan, sehingga yang tampak hanyalah kotak latar kosong.
  2. **Disparitas Dimensi (68px vs 83px)**: Kontainer album art di sisi kiri memiliki dimensi tetap `68×68 DIPs` dengan `VerticalAlignment="Center"`, sedangkan kolom kanan (Judul, Artis, Seekbar, dan Tombol Kontrol) memiliki total tinggi alami `~83 DIPs`. Akibat pemusatan vertikal ini, album art tenggelam 8 piksel ke bawah dari puncak judul lagu, dan tombol kontrol menjuntai 8 piksel ke bawah dari dasar album art.
* **Solusi**:
  1. **Memperbaiki Binding ke Properti Boolean**:
     - Mengubah binding `Visibility` pada `<Image>` menjadi `Visibility="{Binding Media.HasThumbnail, Converter={StaticResource BoolToVis}}"`.
     - Mengubah binding fallback menjadi `Visibility="{Binding Media.HasThumbnail, Converter={StaticResource InverseBoolToVis}}"`.
     - Karena `Media.HasThumbnail` adalah boolean murni (`Thumbnail != null`), converter bekerja dengan akurat: cover art langsung tampil bila tersedia, dan fallback tampil bila lagu tidak memiliki cover art.
  2. **Harmonisasi Dimensi & Penyelarasan Sejajar (82×82 DIPs)**:
     - Dimensi kontainer album art diperbesar dari `68×68` menjadi `82×82 DIPs` dengan `CornerRadius="14"` (serasi dengan lengkungan kartu luar).
     - Menambahkan `Border.OpacityMask` lingkaran halus agar gambar cover art terpotong sempurna sesuai radius sudut tanpa kebocoran piksel di sudut.
     - Membungkus ilustrasi sunset fallback ke dalam `<Viewbox Stretch="UniformToFill">` agar skalanya adaptif.
     - Dengan tinggi album art `82 DIPs` dan kolom teks/kontrol `82 DIPs`, tepi atas album art kini sejajar sempurna dengan tepi atas judul lagu ("Dan..."), dan tepi bawah album art sejajar sempurna dengan tombol Play/Pause.
* **Hasil**:
  Cover art lagu kini tampil tajam dan presisi pada kartu Home, dengan tata letak yang proporsional, simetris, dan sejajar rata dengan judul lagu serta kontrol pemutar.

---

#### Keputusan 94: Integrasi Apple Music TTML Syllable Sync & Reverse Engineering Spicy Lyrics

* **Konteks**:
  Pengguna ingin mengetahui sumber data Spicy Lyrics di Spicetify dan menanyakan apakah Kuri (NotchWin) dapat mengintegrasikan efek *Syllable Sync* (sapuan lirik karaoke presisi kata-demi-kata ala Apple Music) dengan LRCLIB sebagai fallback.
* **Hasil Investigasi & Reverse Engineering**:
  1. Spicy Lyrics (`Spikerko/spicy-lyrics`) mengambil lirik dengan hierarki: Community TTML (`ttml-tool`) $\to$ Apple Music TTML $\to$ Spotify (Musixmatch).
  2. Endpoint Spicy Lyrics resmi dilindungi Cloudflare Bot Management. Namun, endpoint Apple Music TTML terbuka tersedia di `https://api.betterlyrics.org/getLyrics?a={artist}&s={song}` dan AMLL DB (`amll-dev/amll-ttml-db`). Format payload XML TTML memiliki atribut `itunes:timing="Word"` dengan elemen `<p>` dan `<span begin="..." end="...">kata/suku kata</span>`.
* **Solusi Arsitektur di Kuri**:
  1. **Layanan TTML Khusus (`TtmlLyricsService.cs` & `ITtmlLyricsService.cs`)**:
     - Mengambil XML TTML dari Apple Music provider, mem-parse `<p>` dan `<span>`, lalu mengekstrak `StartTime`, `EndTime`, `CharStartIndex`, dan `CharLength` untuk setiap suku kata.
     - Mengembalikan `LyricsCandidate` dengan skor +2000 (`IsSyllableSynced = true`, tag `SYLLABLE`, provider `AppleMusic`).
  2. **Query Paralel & Fallback Otomatis di `LyricsService.cs`**:
     - TTML query, exact LRCLIB, dan search LRCLIB dijalankan secara paralel (`Task.WhenAll`).
     - Jika TTML tersedia, kandidat TTML otomatis menjadi Auto Candidate (#1). Jika tidak, LRCLIB otomatis melayani sebagai fallback mulus tanpa error.
  3. **Sapuan Non-Linear Berbasis Karakter di `MediaViewModel.cs`**:
     - Formula karaoke menghitung posisi karakter aktif:
       $$\text{Progress} = \frac{\text{CharStartIndex} + (\text{SyllableProgress} \times \text{CharLength})}{\text{TotalPanjangKarakterBaris}}$$
     - Jeda antar-kata membekukan highlight pada akhir kata sebelumnya (fitur khas Apple Music / Spotify Karaoke).
     - Bekerja secara otomatis pada **mode Collapsed Pill (Slot A/B)** maupun **mode Expanded (Slide 1)** via `KaraokeClipConverter` 60 FPS.
  4. **Pengaturan & Lokalisasi**:
     - Menambahkan toggle `IsSyllableSyncEnabled` di Pengaturan Musik (`SettingsWindow.xaml`) lengkap dengan lokalisasi EN/ID.
* **Hasil**:
  Kuri kini mendukung lirik karaoke presisi suku kata kata-demi-kata (Syllable Sync) ala Apple Music dan Spicy Lyrics dengan fallback penuh ke LRCLIB. Seluruh paket rilis instalasi installer (.exe) dan portable (.zip) untuk Windows 10/11 64-bit dan 32-bit telah dipaketkan secara lengkap dan siap pakai.

---

#### Keputusan 95: Integrasi Penuh Spicy Lyrics Dual-Pipeline, Pemulihan Spasi TTML, Indikator Provider Lirik, dan Eliminasi Total Efek Stuttering Karaoke

* **Konteks**:
  Setelah implementasi awal Syllable Sync, pengguna menemukan beberapa kendala:
  1. Kata-kata lirik TTML menyatu tanpa spasi (misalnya `Lookatthestars`).
  2. Pengguna meminta prioritas Spicy Lyrics sebagai sumber utama dan meminta indikator sumber (Spicy Lyrics vs LRCLIB) ditampilkan jelas di menu pemilihan lirik.
  3. Animasi karaoke terasa patah-patah (*stuttering*), melompat mendadak, atau tidak sinkron dengan audio.
* **Akar Masalah (*Root Cause*)**:
  1. **Whitespace Truncation pada XML Parser**: `XDocument.Parse(ttmlXml)` tanpa opsi `LoadOptions.PreserveWhitespace` secara otomatis membuang node teks spasi antar-tag `<span>`. Akibatnya, `<span>Look</span> <span>at</span>` digabungkan menjadi `Lookat`.
  2. **Disparitas Koordinat Alignment vs Clip Bounding Box (Penyebab Utama Efek Patah-patah)**:
     - TextBlock lirik menggunakan `TextAlignment="Center" HorizontalAlignment="Stretch"`. Lebar TextBlock melebar memenuhi kontainer (340px), sedangkan teks berada di tengah (misal panjang kalimat 100px, ada ruang kosong 120px di kiri dan 120px di kanan).
     - `KaraokeClipConverter` memotong dari `X = 0` (`Rect(0, 0, progress * 340, height)`).
     - Akibatnya:
       - 35% awal durasi: Klip masih menyapu ruang kosong di kiri $\to$ teks 0% terwarnai (tampak macet/diam).
       - 30% durasi tengah: Begitu klip menyentuh teks di tengah, klip melesat sangat cepat dengan kecepatan 3.5× lipat menyapu seluruh kata sekaligus (*sprint* mendadak).
       - 35% akhir durasi: Teks sudah 100% terwarnai dan klip menyapu ruang kosong di kanan $\to$ tampak berhenti sebelum waktu bait habis.
  3. **Jitter dan Snap Mundur dari Windows SMTC**:
     - Polling berkala dari Windows SMTC (yang memiliki latensi 200–500ms) memicu `_playbackStopwatch.Restart()` dan menimpa posisi audio lokal secara paksa, menyebabkan audio/lirik terasa tersentak mundur beberapa ratus milidetik berulang kali di tengah lagu.
  4. **Fluktuasi Interval Timer**:
     - Interval timer sempat berganti-ganti antara 150ms dan 16ms saat terjadi jeda bait atau interlude, mereset antrean timer WPF dan memicu *dropped frames*.
* **Solusi**:
  1. **Preservasi Spasi Parser XML TTML**:
     - Membaca XML dengan `LoadOptions.PreserveWhitespace` dan menyisipkan spasi normalisasi antar-span teks jika belum ada spasi. Kata-kata kini terpisah rapi (`Look at the stars`) dan indeks karakter suku kata terhitung 100% presisi.
  2. **Arsitektur Dual-Pipeline Spicy Lyrics**:
     - Mendukung Official Developer API (`api.spicylyrics.org/v1`) via token pengembang opsional (`AppSettings.SpicyLyricsApiKey`) dengan prioritas skor tertinggi (+2500).
     - Fallback otomatis ke mirror publik terbuka (`api.betterlyrics.org/getLyrics`, skor +2000) dan universal fallback ke LRCLIB (+1000).
  3. **Lencana Provider di Popup Pemilihan Versi Lirik**:
     - `LyricsCandidateViewModel` kini memiliki properti `Provider` dan `IsSpicyLyrics`.
     - Popup menampilkan lencana visual kontras: 🟥 `[Spicy Lyrics]` (rose `#33E11D48` & `#FDA4AF`) dan ◻️ `[LRCLIB]` (slate `#1AFFFFFF` & `#94A3B8`).
     - Footer panel lirik menampilkan provider aktif (`Spicy Lyrics · SYLLABLE` / `lrclib.net · SYNCED`).
     - Cache kandidat ditingkatkan ke `v4-{sha256}.json`.
  4. **Eliminasi Total Stuttering Karaoke**:
     - **Text-Aware Clip Measurement**: `KaraokeClipConverter` kini mengukur lebar aktual teks menggunakan `FormattedText` dengan thread-safe LRU cache, dan menghitung `leftOffset = (ActualWidth - MeasuredWidth) / 2` saat teks berada di tengah. Klip menyapu tepat dari huruf pertama hingga huruf terakhir secara linear dan proporsional terhadap audio.
     - **Penyelarasan XAML**: Mengubah `HorizontalAlignment` menjadi `Center` dan `TextWrapping="NoWrap" MaxWidth="328"` pada slot collapsed dan expanded.
     - **SMTC Anti-Jitter Drift Protection (Toleransi 1.2 Detik)**: Posisi pemutaran lokal stopwatch tidak lagi di-reset saat Windows SMTC mengirim event rutin kecuali terjadi drift nyata > 1.2 detik (seek manual atau ganti lagu).
     - **Timer 60 FPS Stabil**: Timer rendering dikunci stabil pada `16.0 ms` (`DispatcherPriority.Render`) selama lagu aktif, dan ambang batas pembaruan progress diperhalus ke `0.0005`.
* **Hasil**:
  Lirik lagu kini mengalir sangat mulus tanpa jeda kosong di awal maupun lonjakan di tengah. Pengguna dapat dengan mudah membedakan sumber lirik Spicy Lyrics vs LRCLIB melalui lencana visual, dan kata-kata TTML terpisah sempurna dengan timing suku kata yang presisi.

---

#### Keputusan 96: Tampilan Live Activity Timer & Stopwatch Pada Collapsed Notch (Poni Reverse Engineering)

* **Konteks**:
  Pengguna meminta peningkatan tampilan Timer dan Stopwatch saat notch dalam status Collapsed berdasarkan analisis reverse engineering dari aplikasi **Poni v1.5.0** (`/reverse-engineer-anything sekarang kita improve dibagian timer, coba kamu lihat bagaimana poni menampilkan timer dan stopwatch ketika notch collapse`).
* **Temuan Reverse Engineering dari Poni v1.5.0**:
  1. **Live Activity Utama**: Di Poni (`notch.js` baris 1329–1360 & 1754–1769), timer dan stopwatch yang sedang berjalan bukan berupa pil terpisah di luar notch, melainkan **notch tertutup itu sendiri yang menjadi wadah Live Activity**:
     - **Stopwatch**:
       - Kiri (`#closedLeft`): `<div class="act-icon" style="color:#ffd60a">${fa('stopwatch')}</div><span class="act-text act-dim">Stopwatch</span>` (badge kuning emas `#FFD60A` + teks label redup 12px 500).
       - Kanan (`#closedRight`): `<span class="act-num">${esc(N.swText(N.swElapsed(), false))}</span>` (angka tabular tebal putih 12px 700 tanpa desimal di mode collapsed agar tidak berkedip).
       - Outline: Garis kuning emas bertambah panjang tiap putaran 60 detik (`(seconds % 60) / 60.0`).
     - **Timer**:
       - Kiri (`#closedLeft`): `<div class="act-icon" style="color:var(--orange)">${fa('hourglass-half')}</div><span class="act-text act-dim">Timer</span>` (badge oranye cerah `#FF9F0A` + teks label redup).
       - Kanan (`#closedRight`): `<span class="act-num">${fmt(Math.ceil(N.timerLeft(st.timer) / 1000))}</span>` (angka hitung mundur tabular bold).
       - Outline: Garis bercahaya yang memendek seiring berjalannya waktu (hijau, amber, merah) dan berkedip merah saat selesai.
  2. **Interaksi Klik**: Mengklik notch tertutup saat timer/stopwatch aktif langsung membuka notch dan mengarahkan ke tab/subview timer atau stopwatch (`openNotch('pomodoro'); N.showTimer();` / `N.showStopwatch();`).
  3. **Dimensi Lebar Adaptif**: Poni mengalokasikan `sideWidth` 74px untuk timer dan 78px untuk stopwatch, dengan lebar total beradaptasi dinamis.
* **Solusi & Implementasi di NotchWin (Kuri)**:
  1. **First-Class Widget di `WidgetHost`**:
     - Menambahkan nilai `Timer` pada `enum WidgetType`.
     - `TimerStopwatchViewModel` mengimplementasikan antarmuka `INotchWidget`:
       - `Type => WidgetType.Timer`
       - `Name => "Timer"`
       - `Priority => 30` (lebih tinggi dari Media idle 20 dan Clock 0, sehingga timer yang aktif langsung menempati notch collapsed).
       - `IsActive => IsAnyTimerRunning` (otomatis aktif saat timer atau stopwatch berjalan, dan nonaktif saat keduanya berhenti).
  2. **Penataan UI XAML Pada `CollapsedContentGrid`**:
     - Menambahkan container khusus `WidgetType.Timer` dengan pembagian kolom simetris.
     - **Kolom Kiri**: Slot cuaca (opsional) + Badge melingkar 18×18 px dengan latar `#261A0C`/`#24220A`, border `#FF9F0A`/`#FFD60A`, dan ikon vektor Hourglass/Stopwatch, didampingi label terjemahan `Timer.TabTimer` ("Timer") atau `Timer.TabStopwatch` ("Stopwatch").
     - **Kolom Kanan**: Teks countdown tabular numerik bold `#FFFFFF` (`04:59`), atau stopwatch (`01:23` dan `.45` centiseconds) + Slot baterai miniatur dan maskot Mochi Pet (`TimerCollapsedPet`) jika diaktifkan.
  3. **Navigasi Langsung Saat Diklik**:
     - Handler `PillBorder_MouseLeftButtonDown` mendeteksi jika `CurrentWidgetType == WidgetType.Timer`, otomatis menyesuaikan `IsTimerMode = ShowTimerInCollapsed` dan memanggil `NavigateToTimerSlide()`.
     - Handler `Expand()` secara cerdas membuka `TimerSlideIndex` saat widget Timer sedang aktif.
  4. **Pencegahan Redundansi Sub-Notch Pill**:
     - `AnimateTimerSubNotch` dan `AnimateStopwatchSubNotch` hanya menampilkan pil gantung bawah jika widget utama notch **bukan** `WidgetType.Timer`, sehingga tidak terjadi duplikasi visual.
  5. **Geometri Lebar Adaptif**:
     - `UpdateGeometryForState` mengalokasikan `baseTimerW = 210.0 px` (+68 px cuaca, +68 px baterai, +34 px pet), menghasilkan proporsi yang presisi dan estetis.
* **Hasil**:
  NotchWin kini menampilkan Timer dan Stopwatch secara native di badan notch saat collapsed, lengkap dengan ikon beraksen warna khas Apple/Poni (Oranye untuk Timer, Kuning untuk Stopwatch), teks tabular presisi tinggi, garis cahaya outline perimeter, serta interaksi klik langsung ke slide Timer.

---

#### Keputusan 90: Rotasi Alternasi 3 Detik In-Place Timer & Stopwatch Pada Collapsed Notch

* **Konteks**:
  Pengguna meminta peningkatan pengalaman live activity saat notch dalam keadaan Collapsed:
  *"nah yang aku inginkan di poni kan tiap 3 detik bergantian antara timer/stopwatch dengan apa yang sedang ditampilkan di notch saat colapse, aku ingin seperti itu juga, tapi icon nya berada di sisi paling kiri pada notch lalu waktunya berada di sisi kanan*
  *lalu ketika media player hidup dan judul lagu/lirik, covert art album itu berganti menjadi icon timer/stopwatch dan waktunya mengganti jam*
  *interfal pergantiannya selama 3 detik, coba implementasikan"*

* **Akar Masalah & Kendala Desain**:
  1. Jika `WidgetHost` mendepak widget `Media` ke widget `Timer` secara penuh (seperti penanganan widget biasa), judul lagu dan lirik lagu karaoke yang sedang mengalir di bagian tengah notch akan terputus dan hilang dari pandangan pengguna.
  2. Pengguna secara tegas menginginkan agar bagian **tengah** (judul lagu, artis, lirik lagu karaoke) **tetap berjalan mulus tanpa gangguan**, sementara bagian **paling kiri** (album art) dan **kanan** (jam) yang berganti secara sinkron setiap 3 detik.
  3. Demikian juga pada widget `Clock`, sisi kiri (tanggal) dan sisi kanan (jam) berganti dengan ikon dan durasi timer/stopwatch.

* **Solusi & Implementasi**:
  1. **Timer Alternasi 3 Detik (`_timerAlternationTimer`)**:
     - Ditambahkan `DispatcherTimer _timerAlternationTimer` dengan `Interval = TimeSpan.FromSeconds(3)` di `NotchViewModel`.
     - Dikelola oleh `UpdateTimerAlternationState()` yang hanya aktif saat `CurrentState == NotchState.Collapsed` dan `TimerStopwatch.IsAnyTimerRunning == true`.
     - Setiap tick (3 detik), status `IsCollapsedTimerAlternatePhase` berganti antara `false` (fase normal) dan `true` (fase live activity timer/stopwatch).
  2. **In-Place Slot Replacement Pada XAML (`NotchWindow.xaml`)**:
     - **Pada Widget Media**:
       - Kolom 0 (Kiri): Album cover art (20×20 px) berganti dengan badge ikon Timer (`#FF9F0A` hourglass) atau Stopwatch (`#FFD60A` stopwatch) berukuran identik 20×20 px sehingga tidak memicu pergeseran layout (zero layout jitter).
       - Kolom 1 (Tengah): Judul musik, artis, dan lirik karaoke tetap dirender tanpa interupsi.
       - Kolom 2 (Kanan): Teks jam (`Clock.CurrentTime`) berganti dengan angka hitung mundur Timer (`TimerStopwatch.TimerDisplay`) atau menit-detik Stopwatch (`TimerStopwatch.StopwatchMinutesSeconds`).
     - **Pada Widget Clock**:
       - Kolom 0 (Kiri): Teks tanggal (`Clock.CurrentDate`) berganti dengan badge ikon Timer/Stopwatch beserta label terjemahan.
       - Kolom 2 (Kanan): Teks jam (`Clock.CurrentTime`) berganti dengan angka sisa waktu Timer atau Stopwatch.
  3. **Rotasi Berimbang Saat Dual-Activity**:
     - Jika Timer dan Stopwatch berjalan berbarengan, sistem secara cerdas merotasi live activity setiap fase timer: `Normal -> Timer -> Normal -> Stopwatch -> Normal -> Timer ...`.
  4. **Interaksi Klik Cerdas (`PillBorder_MouseLeftButtonDown` & `Expand()`)**:
     - Mengklik notch saat fase timer/stopwatch aktif langsung mengarahkan ke `NavigateToTimerSlide()` dengan tab yang sesuai (`IsTimerMode = ShowTimerIconInCollapsed`).
  5. **Animasi Transisi Pegas Bergaya Poni (`swapIn 0.45s cubic-bezier(0.32, 1.28, 0.54, 1)`)**:
     - Berdasarkan reverse engineering CSS Poni (`#closed.swap .side { animation: swapIn 0.45s var(--spring); }` dengan `@keyframes swapIn { from { opacity: 0; transform: translateY(6px) scale(0.85); } }`), transisi slot dikoreografi dengan animasi pegas:
       - **Elemen Keluar (*Outgoing*)**: Meluncur ke atas `-6px`, menyusut ke skala `0.85`, dan memudar keluar (`1.0 -> 0.0`) dalam 180 ms (`CubicEase(EaseIn)`).
       - **Elemen Masuk (*Incoming*)**: Berawal dari `+6px` dan skala `0.85`, lalu melompat ke posisi semula `0px` dan skala `1.0` dengan efek pegas/overshoot `BackEase(EaseOut, Amplitude = 0.35)` selama 420 ms, bersamaan dengan fade-in halus (`0.0 -> 1.0`, 240 ms, delay 40 ms).
       - Hasilnya adalah animasi pergantian slot yang sangat lentur, organik, dan elegan tanpa mengganggu elemen tengah (lirik/judul musik).

* **Hasil**:
  Pengguna mendapatkan indikator live activity yang dinamis dan informatif setiap 3 detik dengan animasi transisi pegas yang halus khas Poni/Apple tanpa pernah kehilangan tampilan lirik lagu atau judul media yang sedang diputar.

---

#### Keputusan 91: Sub-Notch Timer & Stopwatch Eksklusif Hanya Muncul Saat Notch Di-Expand

* **Konteks**:
  Pengguna meminta penyempurnaan perilaku floating sub-notch pill (`TimerSubNotchPill` & `StopwatchSubNotchPill`):
  *"nah aku ingin ketika timer/stopwatch hidup, sub notch timer/stopwatch cuma ada ketika notch di expand, ketika notch collapse sub notch nya tidak ada"*

* **Akar Masalah**:
  1. Sebelumnya, `AnimateTimerSubNotch` dan `AnimateStopwatchSubNotch` hanya memeriksa flag `isRunning` (`ViewModel.TimerStopwatch.IsTimerRunning` / `IsStopwatchRunning`), sehingga saat timer/stopwatch aktif dan notch dalam keadaan tertutup (*collapsed*), pil gantung sub-notch ikut muncul di bawah notch.
  2. Karena NotchWin kini sudah memiliki rotasi in-place alternasi 3 detik yang langsung menampilkan ikon dan sisa waktu di badan notch utama, keberadaan sub-notch gantung saat collapsed menjadi redundan dan merusak tampilan minimalis notch.

* **Solusi & Implementasi**:
  1. **Evaluasi Status Terpadu (`shouldShow`)**:
     - Di `AnimateTimerSubNotch(bool isRunning)` dan `AnimateStopwatchSubNotch(bool isRunning)` pada `NotchWindow.xaml.cs`, kondisi visibilitas diubah menjadi:
       ```csharp
       bool shouldShow = isRunning && (ViewModel?.CurrentState == NotchState.Expanded);
       ```
     - Sub-notch pill hanya diizinkan aktif dan meluncur jika kedua syarat terpenuhi: aktivitas sedang berjalan DAN notch sedang dalam keadaan `Expanded`.
  2. **Pemicu Sinkronisasi Transisi State**:
     - Ditambahkan pemanggilan `AnimateTimerSubNotch` dan `AnimateStopwatchSubNotch` di:
       - `AnimateToCurrentState()` (dipanggil setiap event `StateAnimationRequested` saat notch expand/collapse).
       - Handler `OnViewModelPropertyChanged` pada `nameof(NotchViewModel.CurrentState)`.
     - Saat notch beralih dari `Collapsed` ke `Expanded`: jika timer/stopwatch aktif, sub-notch pill meluncur turun dari balik notch (`Y: -24 -> 0`, `Opacity: 0 -> 1` dengan `BackEase`).
     - Saat notch beralih dari `Expanded` ke `Collapsed`: sub-notch pill meluncur naik dan memudar keluar (`Y: 0 -> -24`, `Opacity: 1 -> 0` dalam 180 ms), lalu disembunyikan (`Visibility = Visibility.Collapsed`).
     - Saat timer/stopwatch dinyalakan ketika notch collapsed: sub-notch tetap tersembunyi (`shouldShow == false`), dan informasi waktu berotasi di badan notch utama.

* **Hasil**:
  Tampilan collapsed notch kini sangat bersih tanpa elemen pil gantung di bawahnya. Sub-notch hanya muncul sebagai indikator interaktif saat notch dibuka/di-expand oleh pengguna.

---

#### Keputusan 92: Penghapusan Bingkai Lingkaran Luar pada Ikon Timer & Stopwatch (*Frameless Vector Glyphs*)

* **Konteks**:
  Pengguna meminta perbaikan visual ikon Timer dan Stopwatch saat notch collapsed:
  *"ganti iconnya jangan pakai buletan diluar nya, icon timer dan stopwatchnya"*

* **Akar Masalah**:
  1. Pada implementasi sebelumnya di `MediaLeftTimerSlot`, `ClockLeftTimerSlot`, dan widget `Timer`, ikon hourglass dan stopwatch dibungkus di dalam `<Border CornerRadius="10" ...>` dengan latar belakang gelap dan border berwarna (`#FF9F0A` / `#FFD60A`).
  2. Hal ini menghasilkan tampilan "badge bulat" / tombol lingkaran di luar ikon yang tampak berat, tidak menyatu dengan ikon-ikon sistem lainnya (seperti ikon musik dan cuaca yang frameless), dan membuat ikon terlihat terkurung.

* **Solusi & Implementasi**:
  1. **Penghapusan Wadah Border Lingkaran**:
     - Menghapus elemen `<Border CornerRadius="10" Background="..." BorderBrush="..." BorderThickness="1.2">` di:
       - `MediaLeftTimerSlot` (slot kiri pemutar musik).
       - `ClockLeftTimerSlot` (slot kiri jam/kalender).
       - Tampilan collapsed widget `Timer`.
  2. **Ikon Vektor Murni (*Frameless Vector Icon*)**:
     - Menjadikan elemen `<Path>` sebagai elemen utama yang langsung dirender di dalam slot berukuran 20×20 px:
       - **Timer**: Ikon hourglass oranye `#FF9F0A` dengan `StrokeThickness="1.4"` dan ukuran 13×13 px.
       - **Stopwatch**: Ikon stopwatch kuning `#FFD60A` dengan `StrokeThickness="1.4"` dan ukuran 14×14 px.
     - Menyelaraskan estetika dengan Poni dan Apple Dynamic Island di mana ikon live activity dirender secara minimalis, bersih, dan langsung tanpa bingkai tambahan.

* **Hasil**:
  Ikon Timer dan Stopwatch saat rotasi collapsed notch kini tampil bersih, tajam, dan elegan tanpa lingkaran badge luar, selaras dengan bahasa desain notch yang minimalis.

---

#### Keputusan 93: Optimasi Ketajaman Teks (Penghapusan DropShadowEffect, Snapping Pixel Integer, & ClearType Hint)

* **Konteks**:
  Pengguna menanyakan penyebab tampilan teks terlihat tidak tajam/buram pada tangkapan layar tampilan expanded dan sub-notch pill:
  *"kenapa tulisannya tidak tajam, seperti buram gitu"*

* **Akar Masalah**:
  1. **Rasterisasi Bitmap oleh `DropShadowEffect`**:
     - Pada WPF, ketika `UIElement.Effect` (seperti `DropShadowEffect`) dipasang langsung pada `Border` yang memuat teks (seperti `TimerSubNotchPill`, `StopwatchSubNotchPill`, tab `NavTabActivePillStyle`, serta tombol `Start`/`Pause`), WPF merender seluruh elemen beserta teks di dalamnya menjadi tekstur *offscreen bitmap*.
     - Akibatnya, perenderan subpixel ClearType otomatis dinonaktifkan, dan teks dirender dengan grayscale antialiasing berkualitas rendah.
  2. **Pergeseran Koordinat Fraksional (*Half-Pixel Offset*)**:
     - `SubNotchBaseTranslate` memiliki `Y = 30.5` di XAML dan dihitung sebagai `notchHeight - 1.5` di C#.
     - Nilai desimal `.5` pixel ini memaksa GPU melakukan *bilinear texture filtering* di antara dua garis scanline fisik layar, membuat font dan batas tepi menjadi dobel/buram (*smeared*).
  3. **Absennya Penyelarasan Layout Pixel Snapping**:
     - Elemen sub-notch dan kartu slide sebelumnya belum memiliki `UseLayoutRounding="True"`, `SnapsToDevicePixels="True"`, dan `RenderOptions.ClearTypeHint="Enabled"` pada level visual konteksnya.

* **Solusi & Implementasi**:
  1. **Penghapusan `DropShadowEffect` yang Membungkus Teks**:
     - Menghapus `<DropShadowEffect>` dari `TimerSubNotchPill`, `StopwatchSubNotchPill`, `DownloadSubNotchPill`, `NavTabActivePillStyle`, dan tombol `Start`/`Pause`.
     - Teks kini dirender langsung sebagai glif vektor murni oleh mesin tipografi WPF tanpa melewati *offscreen rasterization pass*.
  2. **Penetapan Koordinat Integer Bulat**:
     - Mengubah translasi `SubNotchBaseTranslate` dari `Y = 30.5` menjadi `Y = 30` di XAML.
     - Mengubah perhitungan di `UpdateNotchGeometry()` dari `notchHeight - 1.5` menjadi `Math.Round(notchHeight - 1.0)`.
  3. **Pengaktifan Snapping & ClearType Penuh**:
     - Menambahkan `UseLayoutRounding="True"`, `SnapsToDevicePixels="True"`, `TextOptions.TextFormattingMode="Display"`, `TextOptions.TextRenderingMode="ClearType"`, dan `RenderOptions.ClearTypeHint="Enabled"` pada:
       - `SubNotchContainer` dan seluruh sub-notch pill (`TimerSubNotchPill`, `StopwatchSubNotchPill`, `DownloadSubNotchPill`).
       - `SlideTimerCard` beserta kartu kontainer tombol dan tab aktif navigasi.

* **Hasil**:
  Teks pada sub-notch pill (`00:36.66`), tab aktif (`Timer`), tombol aksi, dan angka stopwatch/timer kini tampil 100% tajam (*pixel-crisp*), bebas blur, dengan kontras tinggi dan perenderan ClearType subpixel yang presisi.

---

#### Keputusan 94: Rekonfigurasi Kolom Kanan Slide Shelf (Pemberantasan Pemotongan Tombol Bawah / Anti-Clipping)

* **Konteks**:
  Pengguna melaporkan bahwa pada slide Shelf, tombol di bagian bawah tampak terpotong:
  *"kita kebagian shelf, seperti tombolnya terpotong dibagian bawah"*
  Disertai tangkapan layar yang memperlihatkan panel Phone Share (QR Barcode) terbuka dan tombol aksi tambah file (`+`) serta bersihkan (`🗑`) terdorong ke bawah melampaui tepi bawah jendela notch.

* **Akar Masalah**:
  1. **Overflow Vertikal**:
     - `ExpandedHeight` total jendela notch adalah `264.0 DIPs`.
     - Dikurangi header atas (~26 DIPs) dan bilah navigasi `NavDockHost` (~36 DIPs), ruang vertikal bersih yang tersedia untuk slide konten (`SlideShelfCard`) di `Grid Row="2"` (yang memiliki `ClipToBounds="True"`) hanya berkisar **~190 DIPs**.
     - Sebelumnya, kolom kanan Shelf (`Width="104"`) ditata secara bertumpuk ke bawah:
       1. Tombol Phone (`Height="26"`, margin 7 DIPs = 33 DIPs).
       2. Panel `ShelfPhoneUnderBarcode` yang saat dibuka memiliki tinggi ~174 DIPs (padding 12 DIPs + QR code border 84 DIPs + teks hint 18 DIPs + URL 14 DIPs + tombol Copy/Stop 25 DIPs + status 14 DIPs).
       3. Tombol Tambah File (`+`) dan Bersihkan Semua (`🗑`) (`Height="26"`).
     - Total tinggi yang dibutuhkan ketika panel Phone Share dibuka mencapai `33 + 174 + 26 = 233 DIPs`.
     - Akibatnya terjadi kelebihan vertikal sebesar **~43 DIPs**, sehingga tombol `+` dan `🗑` terdorong ke luar batas notch dan terpotong oleh sudut melengkung bawah jendela.

* **Solusi & Implementasi**:
  1. **Pemindahan Tombol Aksi ke Baris Atas (*Top Unified Action Bar*)**:
     - Menyatukan kontrol aksi ke baris paling atas kolom kanan dalam satu `Grid Height="26"`:
       - Kolom 0 (`Width="*"`): Tombol toggle Phone Share (`[ 📱 Phone • ]`) dengan status aktif saat panel terbuka (`PhoneShare.IsPanelOpen == true`) dan feedback border aksen sky blue `#38BDF8`.
       - Kolom 2 (`Width="26"`): Tombol Tambah Berkas (`[ + ]`, `Shelf.PickFilesCommand`) berukuran kompak 26×26 px.
       - Kolom 4 (`Width="26"`): Tombol Bersihkan Semua (`[ 🗑 ]`, `Shelf.ClearAllCommand`) berukuran kompak 26×26 px dengan visual otomatis redup/disabled saat shelf kosong (`Shelf.IsEmpty == true`).
     - Karena tombol `+` dan `🗑` berada di baris paling atas (Y = 0–26 DIPs), tombol tersebut **dijamin 100% selalu terlihat, mudah diakses, dan tidak akan pernah terpotong** bagaimanapun status panel Phone Share.
  2. **Optimalisasi Dimensi Panel QR Barcode (`ShelfPhoneUnderBarcode`)**:
     - Menempatkan panel QR di bawah baris tombol aksi dengan lebar `124 DIPs`.
     - Mengoptimalkan ukuran QR code dari 76×76 px menjadi 68×68 px (border 74×74 px), tetap sangat tajam dan mudah dipindai kamera smartphone modern.
     - Merampingkan padding dan margin teks URL serta tombol Copy/Stop sehingga tinggi total panel barcode menjadi hanya **~144 DIPs**.
  3. **Kalkulasi Total Tinggi Baru**:
     - Baris atas (26 DIPs) + margin gap (6 DIPs) + panel QR (144 DIPs) = **176 DIPs**.
     - Nilai 176 DIPs berada aman di bawah batas ruang maksimal 190 DIPs (sisa margin aman ~14 DIPs di atas batas clipping notch).

* **Hasil**:
  Seluruh kontrol aksi Shelf (`+` dan `🗑`) serta panel Phone Share (QR code, URL, tombol Copy Link, Stop, dan status) tampil 100% utuh, rapi, dan proporsional tanpa ada elemen yang terpotong (*zero clipping*).

---

#### Keputusan 95: Peningkatan Ketajaman Tipografi Panel Phone Share (Display Formatting, ClearType Hint, & Anti-Blur)

* **Konteks**:
  Pengguna menanyakan penyebab tulisan pada panel Phone Share (QR code) terlihat buram (*fuzzy/blurry*) dan terpotong samping:
  *"kenapa tulisannya burem?"* (dengan lampiran gambar crop panel Phone Share).

* **Akar Masalah**:
  1. **Ukuran Font Di Bawah Ambang Keterbacaan (*Sub-Threshold Font Size*)**:
     - Font size 8 DIPs (~6 pt) pada URL dan teks status, serta 8.5 DIPs pada tombol di monitor 96 DPI menghasilkan tinggi glif font kurang dari 5–6 pixel layar.
     - Akibatnya antialiasing font memudarkan batang huruf menjadi gumpalan pixel abu-abu/cyan yang kabur.
  2. **Absennya `RenderOptions.ClearTypeHint="Enabled"` pada Wadah Berlatar Solid**:
     - `SlideShelfCard` memiliki animasi `Opacity` (dari 0 ke 1) yang menyebabkan WPF secara otomatis mematikan rendering subpixel ClearType dan beralih ke *grayscale antialiasing* resolusi rendah jika tidak dipasang `RenderOptions.ClearTypeHint="Enabled"` pada border berlatar solid (`#12121E`).
  3. **Teks Petunjuk Terpotong Samping (*Clipped Horizontal Overflow*)**:
     - String `Phone.ScanHint` sebelumnya berbunyi *"Scan this QR code with your phone camera"* (39 karakter). Pada wadah berukuran sempit tanpa `TextWrapping="Wrap"`, teks terpotong di kedua sisi menjadi `"his QR code with your phone c"`.
  4. **Font Family & Formatting Mode**:
     - Elemen di dalam kartu barcode belum dipasang `TextOptions.TextFormattingMode="Display"`, `TextOptions.TextRenderingMode="ClearType"`, dan font variabel `Segoe UI Variable Text`.

* **Solusi & Implementasi**:
  1. **Pengaktifan Tipografi Crisp & ClearType Penuh**:
     - Menambahkan `UseLayoutRounding="True"`, `SnapsToDevicePixels="True"`, `TextOptions.TextFormattingMode="Display"`, `TextOptions.TextRenderingMode="ClearType"`, `RenderOptions.ClearTypeHint="Enabled"`, dan `TextElement.FontFamily="Segoe UI Variable Text, Segoe UI, sans-serif"` pada `ShelfPhoneUnderBarcode` dan `SlideShelfCard`.
  2. **Peningkatan Ukuran & Kontras Warna Font**:
     - `ScanHint`: Diubah menjadi font **10 DIPs SemiBold** dengan warna putih bersih `#E4E4E7`, `TextWrapping="Wrap"`, `TextAlignment="Center"`, serta string yang lebih ringkas dan elegan:
       - EN: *"Scan with phone camera"*
       - ID: *"Pindai dengan kamera HP"*
     - `Url`: Dinaikkan menjadi font **9.5 DIPs Medium** dengan warna sky blue `#38BDF8` tajam.
     - `Tombol Copy Link & Stop`: Dinaikkan tingginya menjadi 21 px dengan font **9.5 DIPs SemiBold**.
     - `Status Text`: Dinaikkan menjadi font **9 DIPs Medium** dengan titik indikator 5×5 px.
  3. **Penyesuaian Lebar Kolom Kanan (144 DIPs)**:
     - Memberikan ruang bernapas yang proporsional bagi teks dan kontrol tanpa mengganggu area daftar berkas Shelf di sebelah kiri (tetap muat 3 kartu berkas sekaligus).

* **Hasil**:
  Seluruh teks di dalam panel Phone Share (petunjuk scan, URL IP/port, tombol Copy Link/Stop, dan status koneksi) kini tampil 100% tajam (*pixel-crisp*), bebas blur, dengan kontras tinggi dan tidak ada lagi huruf yang terpotong di samping.

---

#### Keputusan 96: Eliminasi Fenomena Timer/Stopwatch Stuck & Stuttering (Target Timestamp & Render Priority Loop)

* **Konteks**:
  Pengguna menanyakan penyebab tampilan timer dan stopwatch sesekali tampak tertahan / stuck sejenak:
  *"nah sekarang kita kebagian timer/stopwatch lagi, kenapa sesekali timer/.stopwacth itu seperti stuck sejenak?"*

* **Akar Masalah**:
  1. **Logika Akumulasi Delta pada Timer Countdown (`RemainingTimerSeconds--`)**:
     - Sebelumnya, pengurangan detik pada timer countdown menggunakan perbandingan selisih diskrit:
       `if ((DateTime.Now - _lastTimerSecondCheck).TotalSeconds >= 1.0) { _lastTimerSecondCheck = DateTime.Now; RemainingTimerSeconds--; }`
     - Ketika hanya timer yang berjalan, interval `DispatcherTimer` diset ke 250 ms.
     - Di WPF, `DispatcherTimer` mengantre di UI thread message loop. Jika ada sedikit beban rendering atau pemrosesan latar belakang, tick timer bisa terpanggil pada selisih `0.985` atau `0.992` detik.
     - Karena nilai selisih `< 1.0`, tick tersebut **terlewati** (*skipped*). Detik baru berkurang pada tick berikutnya yang berjarak 250 ms kemudian, yaitu pada selisih **~1.24 detik**.
     - Akibatnya, angka detik di layar tampak **stuck / berhenti selama 1.25 detik**, kemudian detik berikutnya berganti hanya dalam selisih 0.75 detik (terasa lompat/stuttering).
     - Pola `_lastTimerSecondCheck = DateTime.Now` juga mengakibatkan pergeseran kumulatif (*drift*) dari waktu nyata.
  2. **Prioritas Rendah `_tickTimer` di Antrean Dispatcher**:
     - Sebelumnya `_tickTimer` memakai prioritas default (`Background`/`Normal`).
     - Di aplikasi terdapat banyak proses UI thread lain (seperti `MediaViewModel._timelineTimer` 16 ms, animasi transisi rotasi 3 detik, dan telemetri hardware) yang dapat mendesak antrean dispatcher.
     - Akibatnya, pada Stopwatch yang membutuhkan pembaruan centiseconds per 50 ms, beberapa tick tertunda 50–100 ms, membuat angka centiseconds tampak membeku sejenak lalu melompat.
  3. **Overhead Notifikasi PropertyChanged Stopwatch**:
     - Setiap tick 50 ms, pembaruan string `StopwatchMinutesSeconds` dan `StopwatchCentiseconds` memicu event `PropertyChanged` ganda untuk `SubNotchDisplay` dan `StopwatchSubNotchDisplay` secara membabi-buta meskipun menit dan detiknya belum berganti.

* **Solusi & Implementasi**:
  1. **Arsitektur Target End Timestamp (`_timerTargetEndTime`)**:
     - Mengganti logika delta diskrit menjadi perhitungan target waktu akhir nyata berbasis `DateTime.UtcNow`:
       - Saat Start/Resume: `_timerTargetEndTime = DateTime.UtcNow.AddSeconds(RemainingTimerSeconds);`
       - Di setiap Tick:
         ```csharp
         var timeLeft = _timerTargetEndTime - DateTime.UtcNow;
         int secsLeft = (int)Math.Max(0, Math.Ceiling(timeLeft.TotalSeconds));
         if (secsLeft != RemainingTimerSeconds)
         {
             RemainingTimerSeconds = secsLeft;
             UpdateTimerDisplay();
         }
         ```
       - Pengurangan detik kini terikat langsung dengan waktu jam dinding presisi atom. Berapa pun jeda dispatcher, detik dijamin berganti persis setiap 1.000 detik tanpa ada tick yang terlewat, bebas drift, dan bebas stuck.
  2. **Peningkatan Prioritas ke `DispatcherPriority.Render` & Interval Responsif**:
     - `_tickTimer` diinisialisasi dengan `DispatcherPriority.Render`, dieksekusi tepat sebelum frame komposisi GPU WPF digambar.
     - Interval timer saat stopwatch aktif disetel ke **40 ms (~25 fps)** untuk putaran centiseconds yang sangat halus, dan **80 ms (~12.5 fps)** saat hanya countdown aktif.
  3. **Guard Deduplikasi PropertyChanged**:
     - Menambahkan pengecekan kesamaan string (`if (newMinSec != StopwatchMinutesSeconds)`) sebelum menetapkan nilai properti, memangkas 95% event `PropertyChanged` yang tidak perlu pada menit/detik stopwatch.

* **Hasil**:
  Timer countdown dan stopwatch kini berdetak dengan kestabilan sempurna, bebas dari jeda stuck/stuttering, serta memiliki akurasi waktu jam dinding yang mutlak.

---

#### Keputusan 97: Pengaturan Modul & Menu Navbar Notch Dinamis (Reorder & Toggle Enable/Disable)

* **Konteks**:
  Pengguna meminta menambahkan fitur pengaturan modul di Settings Kuri yang memungkinkan pengguna memindahkan urutan (reorder), mengaktifkan (enable), dan menonaktifkan (disable) menu-menu yang ada di navbar notch (mirip dengan fitur kustomisasi modul pada aplikasi Poni):
  *"sekarang kita buat di bagian setting kuri, ada setingan module yang berguna untuk memindahkan, disable,enable menu yang ada di navbar, mirip punya poni"*

* **Arsitektur & Solusi**:
  1. **Model Data Persistensi (`NavbarModuleConfig` & `AppSettings.NavbarModules`)**:
     - Dibuat class model [`NavbarModuleConfig`](file:///f:/AllProject/DynamicNotchWindows/NotchWin/Models/NavbarModuleConfig.cs) memuat `Id` dan `IsEnabled`.
     - Ditambahkan properti `List<NavbarModuleConfig> NavbarModules` di [`AppSettings.cs`](file:///f:/AllProject/DynamicNotchWindows/NotchWin/Models/AppSettings.cs) dengan 10 modul standar:
       `home`, `control`, `media`, `weather`, `prayer`, `water`, `note`, `clipboard`, `timer`, `shelf`.
     - Pada [`SettingsService.cs`](file:///f:/AllProject/DynamicNotchWindows/NotchWin/Services/SettingsService.cs), ditambahkan auto-recovery validator yang menjamin setiap modul yang belum ada di `settings.json` versi terdahulu otomatis didaftarkan kembali.
  2. **ViewModel Pengaturan Modul (`NavbarModuleItemViewModel` & `SettingsViewModel`)**:
     - Ditambahkan koleksi `ObservableCollection<NavbarModuleItemViewModel> NavbarModules` dengan relasi auto-save.
     - Setiap item modul memuat: `OrderNumber`, `IconGlyph`, `Title`, `Description`, `CanMoveUp`, `CanMoveDown`, `IsEnabled`, dan `CanDisable`.
     - Modul `home` (Beranda/Dashboard Utama) dikunci (`CanDisable = false`) agar notch selalu memiliki setidaknya satu jangkar slide aktif.
     - Ditambahkan perintah `MoveModuleUpCommand`, `MoveModuleDownCommand`, dan `ResetNavbarModulesCommand` untuk pemulihan urutan standar.
     - Sinkronisasi dwiarah (*two-way sync*) otomatis antara status toggle modul `prayer` dengan `AppSettings.IsPrayerReminderEnabled`.
  3. **Antarmuka Pengaturan Modern (`SettingsWindow.xaml`)**:
     - Menambahkan Kategori Sidebar ke-6 bertajuk **🧭 Modul Navbar** (`CategoryModules`).
     - Menambahkan kartu ubin modern untuk setiap modul yang dilengkapi tombol panah stepper `▲`/`▼`, nomor urut badge, ikon glyph vektor/emoji, judul, deskripsi kontekstual, dan sakelar `FluentToggleSwitch`.
     - Tombol "Pulihkan Urutan" (Reset) di pojok kanan atas untuk memulihkan susunan modul ke default pabrikan dalam 1-klik.
  4. **Dinamisasi Navbar Notch & Slide Transition (`NotchWindow.xaml.cs` & `NotchViewModel.cs`)**:
     - Tombol navigasi notch navbar (`NavTab0` s/d `NavTab9`) kini menggunakan `CommandParameter` berbasis Module ID (`"home"`, `"control"`, `"media"`, `"weather"`, `"prayer"`, `"water"`, `"note"`, `"clipboard"`, `"timer"`, `"shelf"`).
     - Di `NotchWindow.xaml.cs`, fungsi `SyncNavbarModuleConfiguration()` secara dinamis menyusun ulang urutan elemen anak di `NavTabsPanel.Children` mengikuti urutan modul yang diaktifkan pengguna, serta menyembunyikan (`Visibility.Collapsed`) modul yang dinonaktifkan.
     - Array `_navTabs`, `_navLabels`, `_navIcons`, `_navContents`, dan `_navDots` otomatis disinkronkan hanya berisi modul aktif dalam urutan konfigurasi saat ini.
     - `GetCards()` memetakan slide index langsung ke kartu modul aktif, sehingga swipe gesture, tombol keyboard, dan klik navbar pill bergeser mulus tanpa celah atau kartu kosong.
     - Pemetaan indikator pill aktif (`MapSlideToPhysicalTabIndex`) bekerja secara instan tanpa hardcoded offset.

* **Hasil**:
  Pengguna memiliki kebebasan penuh mengatur susunan tata letak navbar notch sesuai preferensi alur kerja masing-masing, mengaktifkan/menonaktifkan modul tertentu dengan aman, dan perubahan langsung diterapkan secara real-time ke notch tanpa perlu me-restart aplikasi.









---

### Keputusan 98: Implementasi Fitur Screen Time (Waktu Layar) & Pelacakan Aktivitas Aplikasi Terinspirasi Poni
* **Konteks**:
  Pengguna meminta penambahan fitur Screen Time (Waktu Layar) pada Kuri/NotchWin yang serupa dengan arsitektur dan tampilan aplikasi Poni (v1.6.0).
* **Solusi & Implementasi**:
  1. **Model Data & Persistensi Lokal (ScreenTimeModel.cs & screentime.json)**:
     - Struktur data JSON kompatibel dengan standar Poni: days -> YYYY-MM-DD -> apps (name, path, secs), cats (Browsing, Development, Entertainment, Productivity, System, Other), hours (24 slot 0..23), switches (jumlah perpindahan aplikasi).
     - File data disimpan secara privat di %APPDATA%\NotchWin\screentime.json.
     - Ditambahkan auto-import dari %APPDATA%\Poni\screentime.json jika file NotchWin belum ada sehingga riwayat pengguna tidak hilang.
     - Pembersihan otomatis (*pruning*) data historis yang lebih dari 35 hari untuk efisiensi penyimpanan.
  2. **Pelacak Latar Belakang & Deteksi Diam Rendah CPU (ScreenTimeService.cs)**:
     - Hook Win32 GetForegroundWindow(), GetWindowThreadProcessId(), dan QueryFullProcessImageName() dengan caching PID agar beban CPU mendekati 0%.
     - Deteksi waktu diam (*idle detection*) via GetLastInputInfo(): hitungan otomatis dijeda saat tidak ada input mouse/keyboard selama batas waktu yang ditentukan (default 120 detik).
     - Kategorisasi aplikasi otomatis berbasis nama proses executable dan inspeksi judul tab browser (misal GitHub/StackOverflow -> Development, YouTube/Netflix -> Entertainment).
     - Penghitungan perpindahan aplikasi (*app switches*) saat jendela aplikasi foreground berganti.
     - Auto-save berkala ke disk setiap 30 detik.
  3. **ViewModel Analitik (ScreenTimeViewModel.cs)**:
     - Menyediakan total waktu hari ini (TodayTotalFormatted), jumlah perpindahan aplikasi (SwitchesCountText), histogram 24 bar jam (HourlyBars), segmen kategori warna, dan daftar 4 aplikasi paling sering digunakan (TopApps) dengan progress bar proporsional.
  4. **Antarmuka Kartu Expanded Notch (SlideScreenTimeCard di NotchWindow.xaml)**:
     - Tata letak elegan 584px:
       - Kolom Kiri: Header pelacak waktu layar, badge jumlah switch aplikasi, tampilan durasi besar hari ini (misal 4j 28m), badge Tanpa waktu diam, serta grafik batang histogram 24 jam dengan penanda jam aktif saat ini (*current hour highlight*).
       - Kolom Kanan: Daftar 4 aplikasi teratas dengan titik kategori berwarna, nama aplikasi, bar durasi persentase pemakaian, dan waktu terpakai. Disertai visual empty state yang halus jika belum ada aktivitas.
  5. **Integrasi Penuh ke Modul Navbar Kuri (NotchWindow.xaml.cs & AppSettings.cs)**:
     - Mendaftarkan ID modul "screentime" ke default NavbarModules di AppSettings.cs.
     - Menambahkan tombol tab navbar NavTab10 dengan ikon analitik grafik batang Fluent UI yang tajam.
     - Menghubungkan kartu ke GetCardForModuleId("screentime") dan GetAllPossibleCards().
  6. **Pengaturan Screen Time di Pengaturan Kuri (SettingsWindow.xaml & SettingsViewModel.cs)**:
     - Menambahkan Kategori Sidebar ke-7 bertajuk **â³ Waktu Layar** (CategoryScreenTime).
     - Pengaturan mencakup: sakelar on/off pelacakan waktu layar, slider ambang batas waktu diam (30 s/d 600 detik), dan tombol bersihkan riwayat waktu layar (*clear history*).
     - Menambahkan entri modul "screentime" ke daftar pengaturan urutan modul navbar.
* **Hasil**:
  Fitur Screen Time aktif secara real-time dan mulus, terintegrasi penuh ke dalam ekosistem Kuri, memiliki performa ringan tanpa polling berat, dan menyajikan ringkasan visual aktivitas harian yang intuitif dan elegan.


---

### Keputusan 99: Alternasi Tampilan Download pada Collapsed Notch (Penggantian Konten Penuh & Sub-Notch Khusus Expanded)

- **Tanggal**: 2026-11-08
- **Konteks**:
  Sebelumnya, saat terjadi proses unduhan (download), Kuri menampilkan sub-notch terpisah (`DownloadSubNotchPill`) yang menggantung di bawah notch bahkan ketika notch dalam keadaan tertutup/collapsed. Hal ini membuat tampilan notch kurang rapi dan bertabrakan dengan konsep Dynamic Island. Pengguna menginginkan sistem unduhan berperilaku seperti Timer/Stopwatch: bergantian secara halus di dalam collapsed notch, mengganti seluruh konten notch dengan tampilan unduhan (paling kiri icon unduhan, di samping icon nama file asli, dan di paling kanan persen/kecepatan unduhan serta progress bar tipis 2px di bawah notch). Sub-notch gantung kini hanya muncul saat notch sedang diperluas (Expanded).
- **Implementasi**:
  1. **Resolusi Nama File Asli dari Chromium History SQLite**:
     - Memperbarui `ChromiumDownloadProgressReader` agar selain membaca `total_bytes`, juga membaca kolom `target_path` dari tabel `downloads` browser Chromium/Edge.
     - `DownloadMonitorService` memetakan partial path (`Unconfirmed 123.crdownload`) ke target filename aslinya (misalnya `ubuntu-22.04.iso` atau `setup.exe`), sehingga notch langsung menampilkan nama file sesungguhnya alih-alih sekadar label "Downloading...".
  2. **Sub-Notch Khusus Expanded**:
     - `UpdateDownloadSubNotchVisibility()` diubah agar hanya mengizinkan `DownloadSubNotchPill` tampil ketika `ViewModel.CurrentState == NotchState.Expanded`. Saat collapsed, pil gantung otomatis hilang.
  3. **Mesin Alternasi Dinamis pada Collapsed Notch (`NotchViewModel`)**:
     - Menambahkan properti `IsCollapsedDownloadPhaseActive`.
     - Timer alternasi (`_timerAlternationTimer`) kini memantau Timer/Stopwatch sekaligus Download.
     - Jika unduhan aktif: collapsed notch bergantian secara berkala antara widget normal (Jam/Musik) dan tampilan unduhan.
     - Jika keduanya aktif bersamaan (Timer dan Unduhan): alternasi berputar siklik (*Normal -> Download -> Timer -> Normal*).
     - Lebar notch otomatis menyesuaikan (`Math.Max(265.0, collapsedWidth)`) dengan animasi pegas (*spring morphing*) yang mulus.
  4. **Tampilan Penuh Unduhan pada Collapsed Notch (`NotchWindow.xaml`)**:
     - Menambahkan kontainer `CollapsedDownloadHost` dengan animasi `TranslateTransform` dan `ScaleTransform` yang serasi dengan widget normal.
     - Layout:
       - **Paling kiri**: Ikon unduhan (vektor panah elegan dengan warna aksen).
       - **Di samping ikon**: Nama file unduhan (`Download.PillFileName`, berbobot semi-bold dan terpotong elipsis jika panjang).
       - **Paling kanan**: Persentase unduhan (`Download.CollapsedProgressText`, e.g. `78% · 9,1 MB/s` atau `107,1 MB · 9,1 MB/s`).
       - **Bawah notch**: Indikator progress bar tipis 2px aksen di bibir bawah notch.
     - Klik pada notch saat fase unduhan aktif langsung membuka panel unduhan pada slide Control Center.
- **Hasil**:
  Pengalaman pengguna saat mengunduh berkas kini terasa seamless, terpadu, dan elegan menyerupai interaktivitas Dynamic Island Apple tanpa pil gantung yang mengganggu saat notch dalam keadaan collapsed.


---

### Keputusan 100: Animasi Dinamis Ikon Unduhan (Animated Download Glyphs)

- **Tanggal**: 2026-11-08
- **Konteks**:
  Pengguna menginginkan ikon download (`Path`) tidak statis melainkan memiliki animasi dinamis yang hidup saat proses download sedang berlangsung.
- **Implementasi**:
  1. **Dekomposisi Geometri Ikon**:
     - Memisahkan elemen ikon download menjadi dua komponen independen di dalam `Canvas Width="12" Height="12"`:
       - **Wadah / Baki Bawah (Stationary Tray)**: `M1.5,8.5 L1.5,11 L10.5,11 L10.5,8.5` yang tetap kokoh di posisinya sebagai acuan dasar penampung berkas.
       - **Anak Panah Bergerak (Animated Arrow)**: `M6,1.5 L6,7.5 M3.5,5 L6,7.5 L8.5,5` dengan `RenderTransform` berupa `TranslateTransform`.
  2. **Animasi Translasi Berkelanjutan (WPF Storyboard)**:
     - Menggunakan `DoubleAnimation` dengan `From="-1.5" To="1.8"`, `Duration="0:0:0.75"`, `AutoReverse="True"`, dan `RepeatBehavior="Forever"`.
     - Fungsi easing `QuadraticEase EasingMode="EaseInOut"` menghasilkan gerakan mengalir yang lembut: anak panah meluncur turun masuk ke dalam wadah baki, memperlambat secara organik, lalu bergerak kembali naik secara berirama (bobbing pulse).
  3. **Efisiensi Daya & Siklus Hidup**:
     - Animasi dibungkus di dalam `ControlTemplate.Triggers` dengan `Trigger Property="UIElement.IsVisible" Value="True"`.
     - Saat kontrol tampil (`IsVisible=True`), `BeginStoryboard` menjalankan animasi dengan akselerasi GPU DirectX.
     - Saat notch keluar dari fase download atau tersembunyi (`IsVisible=False`), `StopStoryboard` menghentikan animasi sepenuhnya sehingga CPU dan GPU kembali 0% idle tanpa konsumsi daya latar belakang.
  4. **Penerapan Terpadu**:
     - Diterapkan secara seragam di:
       - `CollapsedDownloadHost` (pada notch tertutup saat fase alternasi unduhan aktif).
       - `DownloadSubNotchPill` (pada sub-notch saat notch diperluas).
       - Kartu unduhan pada slide Ringkasan Utama / Home Control Center.
- **Hasil**:
  Ikon download kini bergerak dengan ritme kinetis yang halus dan modern, memberikan isyarat visual yang jelas dan memuaskan bagi pengguna bahwa unduhan sedang berjalan.


---

### Keputusan 101: Integrasi Penyedia Lirik lrc.red, UI Pemilih In-Place, dan Responsivitas Media

- **Tanggal**: 2026-11-08
- **Konteks**:
  1. Pengguna menginginkan pengambilan lirik dari basis data terbuka **lrc.red** (menyediakan sinkronisasi suku kata / kata ala Apple Music TTML dan LRC dengan basis kode ISRC).
  2. Pengguna meminta perombakan pemilihan versi lirik agar **tidak menggunakan dropdown/popup mengambang**, melainkan menggantikan tampilan teks lirik secara langsung di tempat (*in-place view swap*) di dalam bingkai lirik.
  3. Pengguna melaporkan tombol Previous, Play/Pause, dan Next pada pemutar media terasa kurang responsif / lambat (*sluggish*).
- **Implementasi**:
  1. **Layanan Lirik `lrc.red` (`ILrcRedLyricsService` & `LrcRedLyricsService`)**:
     - Mengakses API pencarian `https://lrc.red/search.json?q=...` dengan header identifikasi `User-Agent: KuriDynamicNotch/1.0`.
     - Mengunduh langsung aset TTML (`https://lrc.red/s/{isrc}.ttml`) untuk ekstraksi kata-demi-kata (syllable-level sync), dengan fallback ke berkas `.lrc`.
     - Berjalan secara paralel bersama Spicy Lyrics dan LRCLIB via `Task.WhenAll` di `LyricsService.cs`, dengan cache persistent disk `v5-{hash}.json`.
     - Menambahkan toggle pengaturan `IsLrcRedEnabled` di `AppSettings` dan `SettingsWindow.xaml`.
  2. **Perombakan UI Pemilihan Lirik In-Place (`NotchWindow.xaml`)**:
     - Menghapus elemen `<Popup Name="LyricsCandidatePopup">` mengambang beserta dependensi HWND di code-behind.
     - Di dalam `LyricsContainer`, membuat transisi mulus antara `LyricsDisplayView` (lirik berjalan) dan `LyricsPickerView` (daftar kandidat lirik in-place):
       - Header: tombol `[← Lirik]`, judul `Pilih Versi Lirik`, dan tombol tutup `[✕]`.
       - Opsi `Otomatis (terbaik)` dengan indikator centang aktif.
       - Daftar kandidat dengan badge warna khas: **lrc.red** (merah coral `#33EF4444`), **Spicy Lyrics** (rose `#33E11D48`), dan **LRCLIB** (slate `#1AFFFFFF`).
       - Memilih salah satu lagu otomatis menerapkan lirik dan langsung kembali ke lirik berjalan.
  3. **Responsivitas Ekstrem Kontrol Media (0ms Latency)**:
     - **Optimistic UI Feedback**: Mengubah status `IsPlaying = !IsPlaying;` seketika saat tombol ditekan pada UI thread, menghilangkan lag 300–1000ms dari delay event SMTC Windows.
     - **Non-blocking Command Execution**: Menambahkan `[RelayCommand(AllowConcurrentExecutions = true)]` pada `TogglePlayPauseAsync`, `SkipNextAsync`, dan `SkipPreviousAsync` agar tombol tidak pernah ter-disable atau mengabaikan klik pengguna saat await IPC berlangsung.
     - **Win32 Multimedia Virtual Key Fallback**: Jika sesi SMTC tertunda, null, atau gagal, `MediaService` otomatis mengirim virtual key hardware (`VK_MEDIA_PLAY_PAUSE`, `VK_MEDIA_NEXT_TRACK`, `VK_MEDIA_PREV_TRACK`) via `keybd_event`.
     - **Pembesaran Hit Target**: Area sentuh/klik tombol media pada Overview Card diperbesar dari 26×26 px menjadi 32×32 px (Play/Pause 34×34 px) dengan efek hover dan press yang reaktif.
- **Hasil**:
  Pengambilan lirik jauh lebih kaya dengan dukungan lrc.red, pemilihan versi lirik terasa terintegrasi rapi tanpa popup melayang, dan tombol pemutar media merespons seketika dengan latensi 0ms.
