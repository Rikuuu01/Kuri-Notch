# 🌤️ Modul 03: Prakiraan Cuaca (Weather)

> Indeks seluruh dokumentasi: [README.md](README.md)

Modul ini menampilkan informasi cuaca real-time dan prakiraan per jam menggunakan data resmi BMKG (Indonesia) dengan fallback Open-Meteo untuk kota internasional.

---

## 📂 File Terkait
- **View XAML**: [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml#L3571-L4116) (`<!-- SLIDE 2: CUACA (WEATHER) -->`)
- **ViewModel**: [WeatherViewModel.cs](../NotchWin/ViewModels/WeatherViewModel.cs)
- **Services & Models**:
  - [WeatherService.cs](../NotchWin/Services/WeatherService.cs)
  - [LocationSearchService.cs](../NotchWin/Services/LocationSearchService.cs)
  - [WeatherData.cs](../NotchWin/Models/WeatherData.cs)
  - [SearchResultLocation.cs](../NotchWin/Models/SearchResultLocation.cs)

---

## 🌡️ Komponen & Alur Kerja

### 1. Sumber Data & Integrasi API
- **Wilayah Indonesia**: Mengambil data API resmi BMKG berdasarkan kode wilayah kabupaten/kota.
- **Internasional / Fallback**: Menggunakan Open-Meteo API berbasis koordinat latitude dan longitude.
- **Pembaruan Berkala**: Cuaca diperbarui otomatis setiap 30 menit di latar belakang.

### 2. Informasi Cuaca yang Ditampilkan
- **Tata Letak Header Kartu Utama**:
  - Baris 1: Ikon pin lokasi dan nama kota (`CityName`) dengan pemotongan teks elipsis halus dan tooltip nama lengkap.
  - Baris 2: Waktu pembaruan data (`LastUpdatedText`, e.g. "Updated 22:03" / "Diperbarui 22:03") berdampingan dengan badge sumber data (`DataSource`, e.g. `[BMKG]` atau `[Open-Meteo]`) berlatar biru malam dengan tooltip informasi penyedia data lengkap.
  - Kanan atas: Tombol Refresh mandiri untuk memperbarui cuaca secara manual.
- **Kondisi Utama**: Suhu saat ini (`TemperatureFormatted`, mis. `25°C` untuk Celsius dan `77°F` untuk Fahrenheit — satuan selalu ditampilkan eksplisit), Kondisi cuaca (`ConditionText`, e.g., Cerah, Hujan Ringan, Berawan), Ikon cuaca SVG dinamis (`WeatherIconPath`).
- **Detail Parameter**:
  - Suhu terasa dan rentang harian (`FeelsLikeAndRangeText`): Berformat `Feels like X° • ↓Min° ↑Max°` (atau `Terasa X° • ↓Min° ↑Max°`). Menggunakan simbol panah ke bawah `↓` untuk suhu terendah dan panah ke atas `↑` untuk suhu tertinggi, dengan pengambilan jendela 24 jam saat malam hari agar rentang harian tetap informatif dan tidak berulang.
  - Kelembaban udara (`HumidityFormatted`).
  - Kecepatan & arah angin (`WindSpeedFormatted`).
  - Peluang presipitasi/hujan (`PrecipitationFormatted`).
- **Prakiraan Per Jam**: Daftar horizontal irisan prakiraan 24 jam ke depan (`TodaySlices`), menampilkan jam, ikon, estimasi suhu, dan curah hujan (`PrecipText`) yang **hanya muncul jika > 0.05 mm** (`HasPrecipitation`).
- **Prakiraan 7 Hari**: Daftar harian (`DailyForecasts`) berisi nama hari, ikon, suhu min/max, bar rentang suhu, dan curah hujan harian. Label curah hujan (`PrecipText`, mm) disembunyikan saat kering agar tidak menampilkan "0 mm" yang membingungkan.

### 3. Pencarian & Pemilihan Kota Kustom
- Tombol pencarian kota membuka modal pencarian lokasi.
- `LocationSearchService` memfilter nama kota secara asinkron.
- Memilih kota menyimpan koordinat dan nama kota ke `AppSettings.WeatherCity`, `AppSettings.WeatherLatitude`, dan `AppSettings.WeatherLongitude`.

---

## 🚫 Panduan Modifikasi Aman (Do's & Don'ts)
- **JANGAN** menghapus konversi satuan Celcius / Fahrenheit yang diatur oleh `AppSettings.IsFahrenheit`.
- **JANGAN** membuat panggilan API cuaca tanpa timeout; sediakan penanganan offline agar UI tidak macet saat koneksi internet terputus.

- **HD Weather Typography & Vector Rendering**: Hindari penggunaan `DropShadowEffect` pada `TextBlock` suhu atau `Path` ikon cuaca karena memicu rasterisasi bitmap low-DPI di WPF yang menyebabkan angka suhu tampak bergerigi (*pixelated*). Gunakan vektor murni dengan `SnapsToDevicePixels="True"`, `TextOptions.TextFormattingMode="Display"`, dan `TextOptions.TextRenderingMode="ClearType"`.
