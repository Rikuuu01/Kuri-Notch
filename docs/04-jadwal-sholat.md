# 🕌 Modul 04: Jadwal Sholat & Adzan

> Indeks seluruh dokumentasi: [README.md](README.md)

Modul ini menyajikan jadwal sholat 6 waktu harian (Subuh, Dhuha, Dzuhur, Ashar, Maghrib, Isya) beserta waktu Imsak menggunakan standar perhitungan hisab Kementerian Agama Republik Indonesia, lengkap dengan hitung mundur langsung dan pengingat audio adzan.

---

## 📂 File Terkait
- **View XAML**: [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml#L4117-L4768) (`<!-- SLIDE 3: JADWAL SHOLAT -->`)
- **ViewModel**: [PrayerTimesViewModel.cs](../NotchWin/ViewModels/PrayerTimesViewModel.cs)
- **Services & Models**:
  - [PrayerTimesService.cs](../NotchWin/Services/PrayerTimesService.cs)
  - [SoundNotificationService.cs](../NotchWin/Services/SoundNotificationService.cs)
  - [PrayerSchedule.cs](../NotchWin/Models/PrayerSchedule.cs)

---

## 🕋 Komponen & Alur Kerja

### 1. Perhitungan Hisab Waktu Sholat
- Beroperasi secara offline penuh tanpa bergantung pada API eksternal menggunakan rumus astronomi hisab Kemenag:
  - Subuh: Sudut depresi matahari $-20^\circ$. Ditampilkan secara resmi dengan label **Subuh** (bukan Fajr).
  - Terbit: Piringan atas matahari menyentuh ufuk ($-0.833^\circ$).
  - Dhuha: Matahari naik setinggi satu tombak ($+4.5^\circ$), sekitar 15-20 menit setelah terbit. Ditampilkan sebagai kartu khusus sholat Dhuha.
  - Ashar: Bayangan benda sama dengan panjang benda ditambah panjang bayangan saat zawal (Madzhab Syafi'i).
  - Maghrib: Piringan atas matahari terbenam di bawah ufuk ($-0.833^\circ$).
  - Isya: Sudut depresi matahari $-18^\circ$.
  - Imsak: 10 menit sebelum waktu Subuh.

### 2. Deretan Kartu Waktu Sholat (6 Kolom)
- Kartu `SlidePrayerCard` menampilkan 6 kolom jadwal sholat:
  1. **Subuh** (Ikon bulan sabit dan bintang fajar `#818CF8`)
  2. **Dhuha** (Ikon matahari terbit pagi keemasan `#F59E0B`)
  3. **Dzuhur** (Ikon matahari siang terik `#FBBF24`)
  4. **Ashar** (Ikon matahari sore berawan jingga `#FB923C`)
  5. **Maghrib** (Ikon matahari terbenam senja `#F472B6`)
  6. **Isya** (Ikon bulan malam berbintang `#A78BFA`)
- Kartu waktu sholat yang **akan datang** (*upcoming prayer*) otomatis disorot (*highlighted*) dengan garis batas aksen tema (misal saat ini waktu Dhuha telah lewat, maka kartu Dzuhur yang akan tiba berikutnya yang aktif disorot).

### 3. Hitung Mundur & Penanda Sholat Aktif
- Menampilkan waktu sholat yang sedang berlangsung dan waktu sholat berikutnya (`NextPrayerName`).
- Timer 1 detik (`StartLiveCountdown`) menampilkan hitung mundur jam:menit:detik menuju waktu sholat berikutnya.

### 4. Pengingat & Suara Adzan Chime
- Sebelum waktu sholat tiba (default 10 menit sebelumnya, diatur oleh `PrayerAlertBeforeMinutes`), notifikasi notch visual muncul.
- Saat waktu sholat masuk, jika `PrayerSoundEnabled` aktif, sistem membunyikan nada chime harmonik adzan yang disintesis secara lokal via `SoundNotificationService.GeneratePrayerCallWav()` dan diputar lewat `PlayPrayerCall()` (`SoundPlayer` asinkron; berkas adzan kustom milik pengguna dipakai bila tersedia).
- **Alarm Persisten dengan Banner Home (bukan toast biasa)**:
  - Saat waktu sholat tiba, notifikasi tampil sebagai **banner Home** (`HomeNotificationType = "prayer"`) yang **tidak auto-dismiss** oleh `NotificationDurationSeconds` dan tetap hidup sampai pengguna bereaksi.
  - **Hentikan (Dismiss)** → `DismissHomeNotification()` memanggil `SoundNotificationService.StopPrayerCall()` (menghentikan suara adzan yang masih berjalan) dan menutup banner.
  - **Tunda (Snooze)** → `SnoozeHomeNotification()` juga memanggil `StopPrayerCall()`, lalu `DispatcherTimer` 5 menit mengaktifkan kembali banner + adzan selama modul sholat masih diizinkan (`IsPrayerReminderEnabled`).
  - `StopPrayerCall()` adalah API baru `ISoundNotificationService`; pemanggilan aman meskipun tidak ada suara yang berjalan (`_prayerPlayer?.Stop()` dalam try/catch).

### 5. Dampak Pengaktifan Modul terhadap Indeks Navigasi Notch
- Modul ini dapat dinonaktifkan di Pengaturan (`AppSettings.IsPrayerReminderEnabled`).
- **PENTING**: Ketika modul sholat dinonaktifkan, tab `NavTab3` disembunyikan dan seluruh slide setelahnya bergeser indeksnya 1 langkah ke kiri (lihat [00-arsitektur-dan-notch-core.md](00-arsitektur-dan-notch-core.md)).

---

## 🚫 Panduan Modifikasi Aman (Do's & Don'ts)
- **JANGAN** mengubah indeks slide di `SlidePrayerCard` tanpa memperbarui properti `IsPrayerEnabled`, `TotalActiveSlides`, dan metode `MapSlideToPhysicalTabIndex` di [NotchViewModel.cs](../NotchWin/ViewModels/NotchViewModel.cs).
