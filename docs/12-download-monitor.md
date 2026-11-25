# 📥 Modul 12: Download Monitor (Unduhan Browser & Aplikasi)

> Indeks seluruh dokumentasi: [README.md](README.md)

Modul ini menampilkan unduhan yang sedang berjalan dari browser dan aplikasi lain. Browser dan aplikasi generik dideteksi lewat *partial file* di folder unduhan. Saat notch mengecil (collapsed), tampilan unduhan bergantian secara mulus dengan widget jam/musik (`CollapsedDownloadHost`) menggantikan seluruh isi notch, menampilkan nama target berkas asli dari tabel `downloads` History Chromium (`target_path`), persentase & kecepatan unduhan, garis progres 2px di bibir bawah, dan ikon panah unduhan yang beranimasi bergerak (*bobbing pulse arrow*). Pil sub-notch (`DownloadSubNotchPill`) hanya muncul ketika notch sedang dibuka/diperluas (*Expanded*) agar tidak mengganggu kesederhanaan notch tertutup. Saat unduhan selesai, toast notifikasi muncul.

---

## 📂 File Terkait
- **Model**: [DownloadSnapshot.cs](../NotchWin/Models/DownloadSnapshot.cs)
- **Service**:
  - [IDownloadMonitorService.cs](../NotchWin/Services/IDownloadMonitorService.cs)
  - [DownloadMonitorService.cs](../NotchWin/Services/DownloadMonitorService.cs)
- **ViewModel**:
  - [DownloadViewModel.cs](../NotchWin/ViewModels/DownloadViewModel.cs)
  - [NotchViewModel.cs](../NotchWin/ViewModels/NotchViewModel.cs) (wiring, `NavigateToDownloadsPanel`, `Shutdown`)
- **View**:
  - [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml) (`DownloadSubNotchPill` di `SubNotchContainer`, strip baris kedua `SlideControlCenterCard`)
  - [NotchWindow.xaml.cs](../NotchWin/Views/NotchWindow.xaml.cs) (`AnimateDownloadSubNotch`, hit-test `WM_NCHITTEST`, klik pil, gating fullscreen)
- **Interop**: [NativeMethods.cs](../NotchWin/Interop/NativeMethods.cs) (`SHGetKnownFolderPath`, `FOLDERID_Downloads`), [SqliteNative.cs](../NotchWin/Interop/SqliteNative.cs) (bindings read-only `winsqlite3.dll`)
- **Progress browser**: [ChromiumDownloadProgressReader.cs](../NotchWin/Services/ChromiumDownloadProgressReader.cs)
- **Pengaturan**: [AppSettings.cs](../NotchWin/Models/AppSettings.cs) (`IsDownloadMonitorEnabled`), [SettingsViewModel.cs](../NotchWin/ViewModels/SettingsViewModel.cs), [SettingsWindow.xaml](../NotchWin/Views/SettingsWindow.xaml) (Panel WIDGET NOTCH)

---

## 🔍 Deteksi

| Ekstensi parsial | Sumber |
|:---|:---|
| `.crdownload` | Chromium (Chrome, Edge, Brave, Vivaldi) |
| `.part` | Firefox / aplikasi generik |
| `.partial` | Legacy / IE |
| `.opdownload` | Opera / Opera GX |
| `.download` | Generic |

- Nama target = nama file tanpa ekstensi parsial.
- Folder yang dipantau: `FOLDERID_Downloads` (fallback `%USERPROFILE%\Downloads`) + folder kustom `download.default_directory` dari `Preferences` Chromium dan `browser.download.dir` dari `prefs.js` Firefox (hanya bila `browser.download.folderList=2`). Maksimal 12 root, dideduplikasi case-insensitive, hanya folder yang ada.
- `FileSystemWatcher` per root (`IncludeSubdirectories`, tanpa event `Changed`) + scan rekursif saat start/resume, sehingga unduhan yang sudah berjalan sebelum aplikasi dibuka tetap terdeteksi.
- Watcher event di-marshal ke `Application.Current.Dispatcher`; seluruh mutasi state terjadi di UI thread.

**Rantai rename Chromium** (wajib dipahami sebelum mengubah `HandleRenamed`):

```text
<guid>.tmp  ──rename──▶  Unconfirmed N.crdownload  ──rename──▶  <nama final>
```

- `<guid>.tmp` sengaja **diabaikan** (bukan file parsial yang dikenali).
- Rename apa pun **ke** path parsial (`.tmp` → `.crdownload`) harus `Upsert` — tanpa ini unduhan Chrome/Edge asli tidak pernah terlacak karena file parsial pertama kali muncul lewat rename, bukan event `Created`.
- Rename parsial → parsial lain berarti file masih berjalan: path & nama target diperbarui, item tetap dilacak.
- Rename parsial → final = selesai; nama toast memakai nama file final (bukan `Unconfirmed N`).

## 📊 Progress (Persen)

- File parsial tidak menyimpan ukuran total, sehingga untuk **Chrome/Edge/Brave/Vivaldi/Opera** total dibaca dari tabel `downloads` (`state = 0`) di database History masing-masing profil.
- Akses memakai `winsqlite3.dll` bawaan Windows dengan mode **read-only** + `busy_timeout` 40 ms; hanya kolom `current_path`, `target_path`, `total_bytes` yang dibaca — **tidak ada URL, riwayat, cookie, atau data lain**.
- Pencarian total hanya berjalan selama ada item Chromium/Opera yang belum punya `TotalBytes`; setelah total diketahui, database tidak disentuh lagi (hemat CPU/disk).
- Pencocokan: `current_path` = path file parsial, atau `target_path` = `DirectoryPath + FileName` (normalisasi `\\?\`, case-insensitive).
- Persen dihitung dari ukuran file aktual ÷ total dari DB, sehingga bar progres tetap halus walau DB di-update browser tertunda.
- **Firefox** dan aplikasi generik tidak menyediakan total, jadi hanya menampilkan ukuran terunduh + kecepatan; bar progres otomatis disembunyikan.
- ETA belum ditampilkan (butuh smoothing tambahan di v3).

## ⚙️ State & Polling

- **Downloading**: ukuran file bertambah pada tick; kecepatan memakai EMA `0.35 × instan + 0.65 × lama`.
- **Paused**: tidak ada pertumbuhan ≥ 3 detik; kecepatan meluruh menuju 0.
- **Stale**: paused ≥ 10 menit; item disembunyikan dari pil/kartu tetapi tetap dilacak (timer 5 detik) dan muncul lagi bila file tumbuh.
- Interval `DispatcherTimer`: 1 detik saat ada item Downloading, 5 detik saat semua Paused, berhenti total saat daftar kosong.
- **Selesai** = file parsial di-rename ke nama final → event `DownloadCompleted` (di-koalesensi 750 ms menjadi satu toast, `{0} downloads complete` bila beberapa sekaligus).
- **Batal** = file parsial dihapus tanpa file final → item hilang tanpa toast.
- Buffer watcher overflow → watcher dibangun ulang dan folder di-scan ulang.

## 🖥️ Tampilan

1. **Tampilan Notch Tertutup Bergantian (`CollapsedDownloadHost` - Keputusan 99 & 100)**:
   - Saat notch tertutup/collapsed dan unduhan aktif, notch **tidak memunculkan pil gantung**.
   - Isi notch bergantian secara siklik (*Jam/Musik -> Download -> Timer*) dengan transisi *spring morphing*.
   - **Tata Letak**:
     - Paling kiri: Ikon unduhan vektor dengan **animasi anak panah bergerak** (*animated arrow glyph bobbing* yang meluncur turun dan naik secara lembut dengan `QuadraticEase`).
     - Di samping ikon: Nama file asli (`Download.PillFileName`), dipecahkan langsung dari `target_path` SQLite Chromium History (e.g. `ubuntu-22.04.iso` alih-alih `Unconfirmed N`).
     - Paling kanan: Persen dan kecepatan unduhan (`78% · 9.1 MB/s`).
     - Bibir bawah notch: Garis progress bar aksen 2px.
   - Klik pada notch saat fase unduhan langsung membuka panel Control Center.
2. **Pil Sub-Notch Khusus Expanded (`DownloadSubNotchPill`)**:
   - Pil sub-notch hanya meluncur turun menggantung di bawah notch **ketika notch sedang dibuka (Expanded)**.
   - Menampilkan ringkasan status unduhan aktif dan persentase.
3. **Strip Control Center**: Baris di bawah kartu Control Center (`Height=24`) berisi ikon animasi, nama file/`+N`, `persen · kecepatan`, tooltip daftar seluruh unduhan, dan tombol buka folder. Selama unduhan aktif, tinggi panel diperluas dengan `DownloadStripExtraHeight` (32 px).
4. **Toast Selesai**: Ikon `📥`, judul `Download complete` dengan nama final file unduhan.

## ⚠️ Batasan

- **Persen** hanya tersedia untuk browser berbasis Chromium (Chrome/Edge/Brave/Vivaldi/Opera) dari DB History; Firefox/aplikasi generik tetap tanpa persen. Bila DB sedang terkunci, persen menyusul pada tick berikutnya.
- Aplikasi yang menulis langsung ke nama final atau memakai folder temp privat (IDM dan sebagian download manager) tidak terdeteksi.
- File parsial sisa crash tampil sebagai Paused lalu disembunyikan setelah 10 menit.

## 🚫 Panduan Modifikasi Aman (Do's & Don'ts)

- **JANGAN** memutasi `_items`/`ObservableCollection` dari thread watcher; selalu lewat `Post`/`Dispatcher`.
- **JANGAN** menambahkan event `Changed` ke watcher; pertumbuhan file cukup lewat polling adaptif.
- **JANGAN** memanggil `Start()` berulang dari `ApplyCurrentSettings` tanpa guard; method start/stop/suspend/resume harus idempoten.
- **JANGAN** lupa menambahkan field setting baru ke `SettingsViewModel.SaveCurrentSettingsAsync`; objek `AppSettings` di sana dibuat manual dan field yang terlewat akan hilang saat auto-save.
- Saat menambah kontrol baru di pil, perbarui blok hit-test `DownloadSubNotchPill` di `WM_NCHITTEST` agar notch tidak collapse saat kursor berada di atas pil.
- **JANGAN** menghapus `DownloadStripExtraHeight` dari `UpdateGeometryForState`; baris slide Control Center hanya ~138 px, sehingga strip di bawah kartu 136 px akan ter-clip oleh `ClipToBounds` dan tidak terlihat.
- **JANGAN** memperluas query History di `ChromiumDownloadProgressReader` ke tabel/kolom lain (URL, riwayat, cookie). Hanya `downloads` (`state = 0`) dan hanya ukuran/path yang boleh dibaca, read-only.
- **JANGAN** menampilkan ukuran byte terunduh di pil/strip sebagai teks utama — tampilkan **persen** (agregat bila semua item punya total); ukuran byte adalah fallback saat total tidak diketahui dan tetap tersedia lengkap di tooltip. Browser memakai pembulatan dengan satu desimal di bawah 10% agar unduhan besar tetap terlihat bergerak.


## ✅ Verifikasi Manual

1. Unduh file besar di Chrome/Edge ke `Downloads` → pil menampilkan `Downloading...`, `persen | kecepatan`, dan garis progres; buka notch → strip menampilkan `persen · kecepatan` (ukuran byte di tooltip); saat selesai → toast dengan nama final. Log harus memuat `tracking Unconfirmed ...` lalu `total size for ...` (kalau tidak, cek rantai rename di atas).
2. Chrome dengan folder unduhan kustom → folder terbaca dari `Preferences`.
3. Firefox (`.part`) dan Opera (`.opdownload`) → terdeteksi.
4. Dua unduhan bersamaan → pil `+1`, strip agregat, tooltip memuat keduanya.
5. Batalkan unduhan → pil hilang tanpa toast.
6. Pause (jeda browser) → label `Paused`, kecepatan 0; resume → pulih.
7. Matikan toggle **Download Monitor** → pil/strip hilang, watcher berhenti; hidupkan → scan ulang.
8. Ganti bahasa EN ↔ ID → label berubah tanpa restart.
9. Fullscreen game/video → pil tersembunyi; keluar fullscreen → muncul lagi bila masih aktif.
10. Klik pil → notch expand ke slide Control Center; hover pil tidak membuat notch collapse.
11. Idle tanpa unduhan → tidak ada timer aktif (cek log `%AppData%\NotchWin\logs`).

Simulasi tanpa browser (PowerShell, saat aplikasi berjalan):

```powershell
$dir = Join-Path $env:USERPROFILE 'Downloads'
$partial = Join-Path $dir 'kuri-test.bin.crdownload'
$fs = [System.IO.File]::Create($partial)
$buf = New-Object byte[] (1024*1024)
for ($i=0; $i -lt 30; $i++) { $fs.Write($buf,0,$buf.Length); $fs.Flush(); Start-Sleep -Milliseconds 500 }
$fs.Close()
Rename-Item $partial (Join-Path $dir 'kuri-test.bin')
```
