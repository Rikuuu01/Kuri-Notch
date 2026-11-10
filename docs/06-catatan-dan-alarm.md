# 📝 Modul 06: Catatan & Alarm (Notes & Reminders)

> Indeks seluruh dokumentasi: [README.md](README.md)

Modul ini menggabungkan editor catatan produktivitas multi-tab dengan pemformatan teks kaya (Rich Text Editor), sistem pengingat alarm berbasis waktu, serta dukungan lokalisasi multibahasa penuh.

---

## 📂 File Terkait
- **View XAML**: [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml#L5433-L6188) (`<!-- SLIDE 5: NOTE & PRODUCTIVITY -->`)
- **View Code-Behind**:
  - [NotchWindow.xaml.cs](../NotchWin/Views/NotchWindow.xaml.cs) (Event handler toolbar formatting & RichTextBox)
- **ViewModel**: [NoteViewModel.cs](../NotchWin/ViewModels/NoteViewModel.cs)
- **Services & Models**:
  - [NoteService.cs](../NotchWin/Services/NoteService.cs) (Penyimpanan JSON di `%APPDATA%\NotchWin\notes.json`)
  - [LocalizationService.cs](../NotchWin/Services/LocalizationService.cs) (Penyedia teks multibahasa untuk toast dan notifikasi)
  - [NoteItem.cs](../NotchWin/Models/NoteItem.cs)
  - [ReminderItem.cs](../NotchWin/Models/ReminderItem.cs)

---

## 🖋️ Komponen & Alur Kerja

### 1. Sistem Multi-Tab Catatan
- **Catatan Default**: Selalu diawali dengan tepat 1 catatan (`Note 1`). Catatan kosong berlebih otomatis dirapikan saat pemuatan file.
- **Tambah Catatan Baru**: Tombol `+` di kanan tab bar menjalankan `AddNoteCommand`, membuat catatan bernomor urut berikutnya (misal `Note 2`), mengosongkan editor, dan memfokuskan kursor.
- **Hover Tombol Silang (`closeBtn`)**:
  - Diatur oleh `MultiDataTrigger` di [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml):
    1. Tab sedang di-hover mouse (`IsMouseOver == True`).
    2. Jumlah catatan lebih dari 1 (`CanDeleteNotes == True`).
  - Tombol silang vektor (`Path` native) muncul rapi di ujung kanan nama tab tanpa risiko kerusakan encoding teks/mojibake.
- **Konfirmasi Hapus Interaktif**:
  - Mengklik silang mengubah tampilan tab tersebut menjadi panel konfirmasi: tombol merah **`[ 🗑 Delete ]`** dan tombol batal **`[ ✕ ]`**.
  - Status konfirmasi bertahan hingga user secara sadar menekan **Delete** (menghapus catatan permanen) atau menekan **Batal** / berpindah ke tab lain.

### 2. Toolbar Pemformatan Rich Text
Terletak di bawah editor catatan:
- **Bold (`B`)**: `EditingCommands.ToggleBold`.
- **Italic (`I`)**: `EditingCommands.ToggleItalic`.
- **Strikethrough (`S`)**: Menambahkan/menghapus `TextDecorations.Strikethrough` pada teks yang diblok.
- **Monospace Code (`</>`)**: Mengubah jenis font teks ke `Consolas` dengan latar belakang semi-transparan.
- **Bullet (`•`)**: `EditingCommands.ToggleBullets`.
- **Nomor (`1.`)**: `EditingCommands.ToggleNumbering`.
- **Salin (`Copy`)**: Menyalin seluruh isi catatan ke clipboard sistem.
- **Pembersihan Tombol Remind**: Tombol "Remind" lama di toolbar catatan telah dihapus karena fungsinya sudah sepenuhnya terintegrasi dalam panel Reminders & Alarm di kolom kanan.

### 3. Kolom Kanan: Pengingat & Alarm Berbasis Waktu
- **Pengaturan Jam & Menit**: Dua kotak input (`AlarmHour` dan `AlarmMinute`) untuk menetapkan waktu alarm berdering.
- **Tombol Pintas Preset**:
  - `+15m`: Menambah 15 menit dari sekarang.
  - `+30m`: Menambah 30 menit dari sekarang.
  - `+1j`: Menambah 1 jam dari sekarang.
  - `Besok`: Menyetel waktu yang sama pada hari berikutnya.
- **Tampilan Daftar Pengingat**:
  - Desain rapi dan bersih: badge waktu di kiri, teks pengingat di tengah (dengan tooltip dan *text trimming*), serta tombol hapus vektor (`✕`) di kanan tanpa lingkaran/radio button yang tidak diperlukan.
- **Pemicu Alarm & Auto-Cleanup (5 Menit)**:
  - Timer latar belakang memeriksa waktu pengingat secara periodik. Jika jam alarm tercapai, sistem menyuarakan nada alarm chime via `SoundNotificationService.PlayTimerAlarm()` dan memunculkan notifikasi notch.
  - **Pembersihan Otomatis**: 5 menit setelah waktu alarm tiba (`now >= DueTime + 5m`), pengingat tersebut otomatis dihapus dari antarmuka dan file penyimpanan `%APPDATA%\NotchWin\reminders.json`.

### 🌐 Lokalisasi Menyeluruh Notifikasi & Toast (Keputusan 75)
Seluruh pesan toast dan notifikasi sub-notch kini 100% multibahasa melalui `LocalizationService.Instance.Get(...)` dan `L10n[...]`:
- **Alarm Set**:
  - ID: `"⏰ Alarm diatur: {dueTime:HH:mm} (Besok)"` / `"(Hari ini)"`
  - EN: `"⏰ Alarm set: {dueTime:HH:mm} (Tomorrow)"` / `"(Today)"`
- **Catatan**:
  - ID: `"Catatan baru dibuat"`, `"Catatan dikosongkan"`, `"Catatan dihapus"`, `"Catatan tersalin ke Papan Klip!"`
  - EN: `"New note created"`, `"Note cleared"`, `"Note deleted"`, `"Note copied to Clipboard!"`
- **Pengingat**:
  - ID: `"⏰ Pengingat: {reminder.Text}"`
  - EN: `"⏰ Reminder: {reminder.Text}"`
- **Waktu Fokus**:
  - ID: `"Waktu Fokus Selesai! Saatnya istirahat sejenak."`
  - EN: `"Focus Time Complete! Time to take a short break."`
- **Navbar Tab**: Diterjemahkan menjadi `"Catatan"` pada Bahasa Indonesia (`LangId["Tab.Note"] = "Catatan"`) dan `"Note"` pada Bahasa Inggris.
- **Vektor Tombol Tutup Sub-Notch**: Tombol tutup pada sub-notch notifikasi menggunakan elemen vektor `Path` (`NotificationDismissPath` `M 0,0 L 7,7 M 7,0 L 0,7`) yang kebal terhadap kesalahan encoding karakter (`Ā¢Å"â€¢`).

### 4. Debounced Autosave
- Mengetik pada `NoteRichTextBox` mengaktifkan timer debouncing (350ms).
- Catatan disimpan otomatis dalam format XAML package dan teks polos ke `%APPDATA%\NotchWin\notes.json` tanpa membekukan antarmuka.

---

## 🚫 Panduan Modifikasi Aman (Do's & Don'ts)
- **JANGAN** memasukkan kembali widget Timer ke dalam kartu catatan karena Timer telah memiliki slide khususnya sendiri ([08-timer-dan-stopwatch.md](08-timer-dan-stopwatch.md)).
- **JANGAN** menambahkan event `MouseLeave` pada tab konfirmasi hapus agar user tidak frustrasi saat kursor sedikit keluar dari area tombol.
- **JANGAN** melakukan hardcoding pesan notifikasi/toast dalam string literal bahasa Inggris murni; selalu panggil `LocalizationService.Instance.Get(...)` dengan kunci kamus yang relevan.
