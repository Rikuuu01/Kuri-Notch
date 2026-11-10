# 💧 Modul 05: Pengingat Minum & Postur Duduk

> Indeks seluruh dokumentasi: [README.md](README.md)

Modul ini membantu menjaga kesehatan dan ergonomi pengguna saat bekerja di depan PC melalui pelacakan asupan air harian serta pemantauan durasi duduk kontinu.

---

## 📂 File Terkait
- **View XAML**: [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml#L4769-L5432) (`<!-- SLIDE 4: PENGINGAT MINUM & POSTUR -->`)
- **ViewModel**: [WaterTrackerViewModel.cs](../NotchWin/ViewModels/WaterTrackerViewModel.cs)
- **Services & Models**:
  - [WaterTrackerService.cs](../NotchWin/Services/WaterTrackerService.cs)
  - [WaterTrackerState.cs](../NotchWin/Models/WaterTrackerState.cs)
  - [SoundNotificationService.cs](../NotchWin/Services/SoundNotificationService.cs)

---

## 🥤 Komponen & Alur Kerja

### 1. Pelacak Asupan Minum (Hydration Tracker)
- **Target Harian**: Default 2000ml (`WaterDailyGoalMl`).
- **Tombol Tambah Cepat**: Tombol pintas untuk menambah +150ml, +250ml, atau +500ml ke total harian.
- **Tombol Undo & Reset**: Tombol "Undo" (atau "Urungkan" dalam bahasa Indonesia) untuk membatalkan entri penambahan air terakhir secara instan, serta tombol "Reset" harian.
- **Progress Bar & Visualisasi**:
  - Menampilkan persentase pencapaian target harian (`PercentageText`).
  - Menghitung jumlah gelas air yang telah dikonsumsi (`GlassesCountText`).
- **Reset Harian Otomatis**: Data konsumsi air otomatis di-reset ke 0 pada pukul 00:00 tengah malam setiap hari.
- **Interval Pengingat Minum**: Timer interval berkala (default tiap 45 menit) yang memunculkan alarm pengingat menetap di notch saat tiba waktunya minum.
- **Alarm Pengingat Persisten**:
  - Notifikasi tidak menutup sendiri: tetap tampil di bilah collapsed maupun pill expanded sampai pengguna memilih aksi.
  - Dua tombol aksi: **Tunda 2m** (snooze) dan **Minum** (mencatat +250 ml sekaligus mereset timer duduk).
  - Suara pengingat (`PlayWaterReminder`) diulang tiap 15 detik selama alarm tampil, menghormati toggle `WaterSoundEnabled`.
  - Snooze hanya menunda 2 menit dan tidak mengubah interval di Pengaturan; setelah 2 menit alarm muncul kembali selama pengguna masih aktif.
  - Alarm berhenti otomatis saat pengguna idle >= 5 menit (dianggap istirahat), saat minum dicatat, atau saat pengingat dimatikan.
  - HUD volume/baterai yang sempat menimpa alarm akan dipulihkan kembali ke alarm setelah HUD selesai.
  - Selama alarm aktif, maskot mempertahankan ekspresi minum (persistent alert) hingga alarm selesai, bukan hanya beberapa detik.
  - Hover pada notch collapsed tidak membuka panel selama alarm (klik tetap membuka), agar dua tombol aksi tetap terjangkau.

### 2. Pelacak Durasi Duduk & Peregangan Postur (Sitting Ergonomics)
- **Deteksi Waktu Duduk**: Timer melacak berapa lama user aktif bekerja tanpa jeda (`SittingDurationText`).
- **Pendeteksi Keheningan / Istirahat**: Jika user menjauh dari komputer (idle tanpa input keyboard/mouse selama $\ge 5$ menit), timer durasi duduk otomatis di-reset.
- **Kartu Flip Peregangan Postur**:
  - Menampilkan kartu dua sisi (`IsSittingCardFlipped`).
  - Sisi belakang menyajikan tips peregangan singkat (leher, bahu, pinggang) untuk meredakan pegal bekerja.

---

## 🚫 Panduan Modifikasi Aman (Do's & Don'ts)
- **JANGAN** menghapus pemeriksaan reset tengah malam di [WaterTrackerService.cs](../NotchWin/Services/WaterTrackerService.cs), agar riwayat hari sebelumnya tidak menumpuk ke hari berikutnya.
