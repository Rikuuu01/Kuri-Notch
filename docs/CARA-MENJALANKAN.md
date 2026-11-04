# Cara Menjalankan NotchWin

Berikut adalah cara tercepat untuk membuka aplikasi NotchWin di komputer Anda:

### Cara 1: Menggunakan File Batch (Paling Mudah)
1. Cukup klik ganda (double-click) file **`run.bat`** yang ada di dalam folder ini.
2. Pill hitam kecil (**NotchWin**) akan langsung muncul di tengah atas layar Anda.

---

### Cara 2: Lewat Terminal / Command Prompt
Jika Anda ingin menjalankannya lewat terminal:
```powershell
.\run.bat
```
Atau jika ingin menjalankan langsung dari folder hasil build:
```powershell
& ".\NotchWin\bin\Release\net8.0-windows10.0.19041.0\NotchWin.exe"
```

---

### Cara Menguji Fitur (Fase 1)
1. **Perhatikan Layar Atas**: Di tengah atas layar monitor utama, akan terlihat pill hitam melayang bertuliskan *NotchWin* dengan indikator hijau.
2. **Hover Kursor**: Arahkan kursor mouse ke pill tersebut. Pill akan membesar (*expand*) dengan animasi halus menjadi panel informasi.
3. **Geser Kursor Keluar**: Arahkan kursor menjauh dari pill. Setelah jeda singkat (~450ms), pill akan mengecil kembali (*collapse*) secara otomatis.
4. **Klik Pill**: Saat kursor diarahkan ke notch (terbuka), klik kiri sekali untuk **mengunci panel (pin)** agar tetap terbuka meskipun kursor dijauhkan. Klik sekali lagi untuk menutupnya kembali.
5. **Menutup Aplikasi**: Tekan tombol **Esc** pada keyboard. (Menu klik kanan dihilangkan sesuai permintaan dan akan disediakan via System Tray di Fase 4).
