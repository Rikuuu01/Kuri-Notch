# 🎛️ Modul 01: Pusat Kontrol (Control Center)

> Indeks seluruh dokumentasi: [README.md](README.md)

Pusat Kontrol adalah slide Pusat Kontrol (`SlideControlCenterCard`, Indeks 1 pada navbar) yang menyajikan ubin pintasan cepat (*Quick Actions*) Windows dan pengatur intensitas perangkat (*dual slim neon sliders* untuk kecerahan dan volume suara).

---

## 🎨 Desain Antarmuka & Redesain Visual (Keputusan 85)
Pusat Kontrol menggunakan estetika *Dark Glass Slate Minimalis* modern yang ringkas, terorganisir dalam 2 bagian utama:

### 1. Grid Pintasan Cepat (Quick Settings 5-Kolom x 2-Baris)
Disusun dalam 5 kolom simetris selebar 584px dengan ubin membulat (`CornerRadius="12"`, tinggi 46px):

* **Baris 1**:
  1. **Wi-Fi (Split Tile)**:
     - Kiri: Tombol toggle radio Wi-Fi dengan ikon gelombang sinyal (putih saat pasif, hitam saat aktif).
     - Pembatas vertikal halus (`Width="1"`).
     - Kanan: Tombol chevron (`>`) untuk membuka daftar jaringan nirkabel Windows (`ms-availablenetworks:`).
     - Label status bawah: Menampilkan SSID jaringan aktif atau status Wi-Fi (`WifiStatusLabel`).
  2. **Bluetooth (Split Tile)**:
     - Kiri: Tombol toggle radio Bluetooth dengan latar aksen sky blue `#38BDF8` dan ikon gelap `#09090B` saat aktif.
     - Pembatas vertikal halus.
     - Kanan: Tombol chevron (`>`) membuka pengaturan Bluetooth Windows (`ms-settings:bluetooth`).
     - Label status bawah: Menampilkan nama perangkat atau status Bluetooth (`BluetoothStatusLabel`, e.g. "Not connected").
  3. **Airplane Mode (Single Tile)**:
     - Ikon pesawat siluet putih. Latar aktif berubah menjadi sky blue `#38BDF8`.
     - Label bawah: `Airplane mode`.
  4. **Keep Awake (Single Tile)**:
     - Ikon cangkir kopi dengan uap hangat.
     - Mencegah sistem dan layar masuk ke mode tidur menggunakan Win32 `SetThreadExecutionState`.
     - Label bawah: `Keep awake`.
  5. **Dark Mode (Single Tile)**:
     - Ikon kontras lingkaran setengah penuh.
     - Mengubah tema Windows secara instan via Registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` dan notifikasi `WM_SETTINGCHANGE`.
     - Label bawah: `Dark mode`.

* **Baris 2**:
  1. **Microphone (Single Tile)**:
     - Ikon mikrofon kapsul studio.
     - Mengubah status mute mikrofon sistem default secara instan menggunakan NAudio CoreAudio capture endpoint.
     - Label bawah: `Microphone`.
  2. **Eye Breaks (Single Tile)**:
     - Ikon mata dengan pupil.
     - Mengaktifkan pengingat istirahat mata aturan 20-20-20.
     - Label bawah: `Eye breaks`.
  3. **Screenshot (Single Tile)**:
     - Ikon crop snip presisi.
     - Menjalankan Windows Snipping Tool (`LaunchSnippingTool()`).
     - Label bawah: `Screenshot`.
  4. **Lock (Single Tile)**:
     - Ikon gembok keamanan.
     - Mengunci komputer secara instan via `LockWorkStation()`.
     - Label bawah: `Lock`.
  5. **Slot Kosong**:
     - Kolom ke-5 sengaja dibiarkan kosong untuk estetika minimalis sesuai desain referensi.

---

### 2. Wadah Slider Ramping (Dual Slim Neon Green Sliders)
Dibungkus dalam satu kontainer kartu gelap membulat (`Background="#121218"`, `CornerRadius="14"`, `Padding="14,8"`):

* **Interaksi Geser & Knob Bundar (Hybrid Fluid Drag & Interactive Thumb)**:
  - **Knob Bundar ("Bulet") Visual**: Elemen `Thumb` berwujud lingkaran putih elegan 12x12 DIPs dengan drop shadow lembut (`Color="#000000" Opacity="0.4"`). Knob membesar dinamis menjadi 14x14 saat hover dan 15x15 saat sedang ditarik (`Thumb.IsDragging`), tetap kontras putih di atas bar hijau neon `#22C55E`.
  - **Dukungan Hybrid Drag**:
    1. *Tarik Knob Langsung*: Menekan langsung pada knob bundar memanfaatkan mekanisme native WPF `Thumb` (`DragDelta`), menghasilkan gerakan geser 100% mulus dan presisi persis seperti `MediaSeekSlider`.
    2. *Klik & Geser di Track Bar*: Menekan sembarang titik pada bar track melakukan lompat nilai (*jump-to-point*) seketika dan mengaktifkan continuous tracking, sehingga pengguna dapat langsung menahan dan menggeser bar secara bebas.
  - **Proteksi `WM_NCHITTEST` & Anti-Collapse**: Saat `ViewModel.IsSliderDragging == true`, window otomatis mengembalikan `HTCLIENT (1)` dan mencegah notch tertutup otomatis (`PillBorder_MouseLeave`), sehingga kursor yang melesat keluar dari batas notch saat menggeser slider tidak pernah kehilangan fokus.
  - **Sinkronisasi Cache & Threading**: Nilai di-cache secara instan di ViewModel (`_cachedVolumePercent`, `_cachedBrightnessPercent`) dan diproteksi dengan flag `IsSliderDragging` sehingga polling background tidak menimpa posisi slider saat sedang digeser. Pemanggilan hardware COM Audio berjalan asinkron di latar belakang (`Task.Run`).
* **Baris Kecerahan (Brightness)**:
  - Kiri: Ikon matahari / roda gir vektor presisi (`#FFFFFF`).
  - Tengah: Slider ramping bergaya garis tipis `ControlCenterSlimSliderStyle` dengan track 4.5 DIPs (`CornerRadius="2.25"`), latar gelap `#22222E`, dan isian progres hijau neon cerah `#22C55E`.
  - Kanan: Teks persentase numerik (e.g. `100`).
* **Baris Volume Suara (Volume)**:
  - Kiri: Ikon speaker dengan gelombang suara vektor presisi (`#FFFFFF`).
  - Tengah: Slider ramping neon green identik yang terhubung ke `MasterVolumePercent`.
  - Kanan: Teks persentase numerik (e.g. `70`).
  - Ujung Kanan: **Tombol Pil Output Audio (`AudioDeviceSelectorBtn`)**:
    - Tombol kapsul membulat `[ 🔊 > ]` (`Background="#22222E"`, hover `#2D2D3D`).
    - Membuka popup pemilih perangkat output suara (`DevicePickerPopup`) lengkap dengan penanganan anti-auto-collapse.

* **Dimensi Vertikal Notch Terbuka (`ExpandedControlCenterHeight = 304.0`)**:
  - Tinggi notch secara khusus diatur ke `304.0 DIPs` saat berada di slide Pusat Kontrol (dengan tambahan `+32.0 DIPs` saat ada unduhan aktif). Hal ini menjamin seluruh kartu grid 5-kolom dan kartu slider tampil utuh tanpa terpotong di bagian atas maupun bawah.

---

### 3. Banner Monitor Unduhan Aktif
Jika terdeteksi ada file yang sedang diunduh via browser (Chromium/Edge/Chrome/IDM), banner monitor unduhan ramping otomatis muncul di bawah wadah slider lengkap dengan judul file, kecepatan, dan tombol cepat membuka folder unduhan.

---

## 📂 File Terkait
- **View XAML**: [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml) (`<!-- SlideControlCenterCard -->`)
- **ViewModels**:
  - [NotchViewModel.cs](../NotchWin/ViewModels/NotchViewModel.cs) (State toggle & RelayCommands)
  - [VolumeViewModel.cs](../NotchWin/ViewModels/VolumeViewModel.cs) (Audio endpoint, Master Volume & Devices)
  - [DownloadViewModel.cs](../NotchWin/ViewModels/DownloadViewModel.cs) (Unduhan aktif)
- **Services & Helpers**:
  - [QuickActionHelper.cs](../NotchWin/Interop/QuickActionHelper.cs) (Win32 APIs, Registry Themes, NAudio Mic)
  - [VolumeService.cs](../NotchWin/Services/VolumeService.cs)
  - [BrightnessService.cs](../NotchWin/Services/BrightnessService.cs)

---

## 🚫 Panduan Modifikasi Aman (Do's & Don'ts)
- **DevicePickerPopup Anchor**: `DevicePickerPopup` wajib menggunakan `PlacementTarget="{Binding ElementName=AudioDeviceSelectorBtn}"` dan event `Opened`/`Closed` terhubung agar notch tidak tertutup saat memilih sound card.
- **Slim Slider Line**: Jangan menambahkan thumb bola putih pada `ControlCenterSlimSliderStyle` agar karakter minimalis garis neon green tetap terjaga sesuai desain referensi.
- **Commands Binding**: Perhatikan pemanggilan relay command: `QuickToggleWifiCommand`, `QuickToggleBluetoothCommand`, `OpenWifiSettingsCommand`, `OpenBluetoothSettingsCommand`, `ToggleAirplaneModeCommand`, `ToggleKeepAwakeCommand`, `ToggleDarkModeCommand`, `ToggleMicrophoneMuteCommand`, `ToggleEyeBreaksCommand`, `CaptureScreenCommand`, dan `LockComputerCommand`.
