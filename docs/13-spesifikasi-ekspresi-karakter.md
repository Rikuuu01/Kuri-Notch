# 🎨 Modul 13: Spesifikasi Ekspresi Karakter (Art Contract)

> Indeks seluruh dokumentasi: [README.md](README.md)
>
> Dokumen ini adalah **kontrak visual** untuk membuat karakter maskot baru yang kompatibel dengan sistem Pet Companion. Karakter apa pun yang memenuhi daftar ini bisa menggantikan Pastel Mochi / Astro Mascot / Egg Mascot tanpa mengubah logika layanan.

---

## 📊 Ringkasan Kebutuhan

| Kategori | Jumlah | Isi |
| :--- | :---: | :--- |
| State dasar | 3 | `Idle`, `Music`, `Sleep` |
| Reaksi alert | 10 | `Happy` + 8 badge event + `Welcome` |
| Set mata | 3 | Terbuka (pupil), Senang (`^ ^`), Tidur (garis) |
| Badge | 8 | Lonceng, weker, tetes air, stopwatch, bulan-bintang, bluetooth, clipboard, kamera |
| Layer efek | 5 | Blush, headphone, flash kamera, partikel (not/Z/sparkle), lengan lambai (opsional) |

Sumber kebenaran: [`MascotState.cs`](../NotchWin/Models/MascotState.cs), [`MascotControl.xaml.cs`](../NotchWin/Controls/MascotControl.xaml.cs).

---

## 1️⃣ Tiga State Dasar (Loop)

State dasar ditentukan otomatis oleh `PetCompanionService` berdasarkan aktivitas musik, idle kursor, dan status expand notch.

### Idle — terjaga

| Aspek | Ketentuan |
| :--- | :--- |
| Mata | `EyesOpen` + pupil mengikuti kursor |
| Pupil | `EyeWide (1,1)`, pupil ke tengah saat tidak ada kursor |
| Blush | 0 |
| Headphone | Tersembunyi |
| Badan | Napas halus **hanya saat notch expanded**: bob `-1.6px`, sway `1.5°`, skala `+2.5%`, ekor `8°`, periode 2.4 detik |
| Collapsed | Badan **diam total** (aturan performa) |
| Transisi | Kedip mikro tetap berjalan |

### Music — sedang memutar lagu

| Aspek | Ketentuan |
| :--- | :--- |
| Mata | `EyesOpen` + gaze kursor |
| Headphone | **Muncul** (pop-in 0.45 detik) |
| Badan | Groove upbeat periode 0.68 detik: bob `-2.6px`, sway `3.5°`, skala `+4.5%`, ekor `18°`, squish `0.14` |
| Partikel | 2 not musik (`NoteA`, `NoteB`) melayang naik + memudar, loop |
| Collapsed | **Tetap beranimasi** (pengecualian aturan performa) |
| Wake | Memutar musik membangunkan maskot dari tidur |

### Sleep — tidur lelap

| Aspek | Ketentuan |
| :--- | :--- |
| Mata | `EyesSleep` (dua garis datar), pupil ke tengah |
| Badan | Statis, tanpa napas/goyang |
| Partikel | 3 huruf `Z` melayang memudar, **hanya saat expanded** |
| Collapsed | Statis total, tanpa partikel |
| Pemicu | Kursor diam > 300 detik; bangun seketika saat kursor bergerak |

---

## 2️⃣ Sepuluh Reaksi Alert

Reaksi bersifat sementara, punya prioritas, dan kembali ke state dasar setelah durasi habis.

| # | State | Ekspresi Mata | Gaze / Pupil | Blush | Badge & Efek | Animasi Badan | Durasi | Prioritas |
| :-: | :--- | :--- | :--- | :-: | :--- | :--- | :---: | :---: |
| 1 | **Happy** | `EyesHappy` (`^ ^`) | — | 0.8 | 2 sparkle bintang 4 sisi | Bounce/squash/tilt elastis | 3 detik | 0 |
| 2 | **Notification** | `EyesOpen`, Wide `1.12 × 1.2` | LookAt `(1.4, -1.6)` kanan-atas | — | Lonceng berayun kiri-kanan | Hop kecil | 3.5 detik | 1 |
| 3 | **Alarm** | `EyesOpen`, Wide `1.2 × 1.3` | `(0, 0)` + **jitter pupil `0.7`** | — | Jam weker shake + badge pulse | Badan shake cepat | 10 detik | 3 |
| 4 | **Drink** | `EyesOpen`, Wide `1.05 × 1.1` | LookAt `(1.4, -1.6)` | — | Tetes air bob vertikal | Tilt + squash lembut | 6 detik | 2 |
| 5 | **Timer** | `EyesOpen`, Wide `1.1 × 1.2` | LookAt `(1.4, -1.6)` | — | Stopwatch, jarum berputar 360° + badge pulse | Sway lembut | 8 detik | 2 |
| 6 | **Adzan** | `EyesOpen`, Wide `1.0 × 0.55` (setengah terpejam) | LookAt `(0, -1.2)` atas | — | Bulan sabit + bintang berkedip | Sway tenang; **headphone dilepas** | 15 detik | 3 |
| 7 | **Bluetooth** | `EyesHappy` (`^ ^`) | — | 0.6 | Simbol bluetooth + cincin radar mengembang | Hop riang; **headphone muncul** | 3.5 detik | 1 |
| 8 | **Copied** | `EyesHappy` (`^ ^`) | — | — | Clipboard + centang | Angguk/tilt cepat | 1.8 detik | 1 |
| 9 | **Screenshot** | `EyesOpen` + kedip kejut (Wide Y turun lalu normal) | Gaze kursor | — | Kamera + **flash putih overlay** | Pop badan singkat | 1.8 detik | 1 |
| 10 | **Welcome** | `EyesOpen` → `EyesHappy` (crossfade 0.5 detik) | `(0, 0)` | 0.8 | Lengan melambai 4× + 2 sparkle berkedip | Hop squash, lalu sway lembut `±3°` | 3.0 detik (overlay) / 3.4 detik bawaan | 1 |

**Aturan antrean:** prioritas lebih tinggi menyela langsung; state setara/lebih rendah masuk antrean (maks 8, duplikat state yang sama diabaikan) dan ditampilkan berurutan. Alert selesai → kembali ke state dasar atau alert berikutnya. `Welcome` tidak dikirim lewat event bus layanan; overlay startup memanggil `PlayWelcome(...)` langsung pada instance pet sementara (lihat §5).

---

## 3️⃣ Micro-Expression (Selalu Aktif)

| Mikro-ekspresi | Detail |
| :--- | :--- |
| **Kedip** | Interval acak 2–5.5 detik; 20% peluang double-blink; `EyeBlink` scale Y turun ke `0.08` lalu kembali `1` |
| **Gaze kursor** | Pusat mata `(14, 11)` untuk Pastel Mochi/Astro, `(14, 9.3)` untuk Egg, di kanvas 28×20; pergeseran maksimum `±1.4px` horizontal / `±1.8px` vertikal (level kontrol) |
| **Gaze layanan** | Kompresi jarak `min(distance/160, 1)`; maks elips `2.2 / 3.4px`; lerp `0.28` (expanded) / `0.45` snap (collapsed) |
| **Wide** | Skala mata berbeda per state (lihat tabel reaksi) |
| **Jitter** | Hanya `Alarm`, amplitudo `0.7` |
| **Blush** | 0.8 (Happy), 0.6 (Bluetooth), 0 selain itu |
| **Headphone** | Pop-in 0.45 detik / pop-out 0.22 detik; tampil saat Music & Bluetooth; disembunyikan saat Adzan |
| **Interaksi wrapper** | Hover → skala `1.05` + Happy; tekan → squash `1.10 × 0.90` + poke wink; lepas → elastic bounce + Happy |

---

## 4️⃣ Layer & Aset Wajib

Karakter baru minimal harus menyediakan struktur berikut (nama elemen mengikuti kontrak `MascotControl` bila ingin memakai code-behind yang sama):

1. **Badan/karakter utama** — terbaca di kanvas 28×20, termasuk saat collapsed (~22×16).
2. **`EyesOpen`** — dua mata putih + pupil gelap + highlight, dengan clip agar pupil tidak keluar; `PupilTranslate` untuk gaze.
3. **`EyesHappy`** — dua lengkungan `^ ^`.
4. **`EyesSleep`** — dua garis datar `— —`.
5. **`EyeWide` & `EyeBlink`** — scale transform pada grup mata.
6. **`BlushL` / `BlushR`** — pipi merona, opacity default 0.
7. **Headphone** — band + dua earcup, `HeadphoneScale` default 0.
8. **`FlashOverlay`** — lapisan putih di atas badan, opacity default 0.
9. **Transform badan** — `BodyScale`, `BodyTilt`, `BodyShift`; pivot bawah `(0.5, 0.9)` untuk pantulan natural.
10. **Lengan lambai (opsional)** — kanvas `ArmWave` (untuk karakter berbadan bulat) dengan `ArmRotate` berporos bahu + `ArmScale`; default tersembunyi `opacity 0` / rotasi istirahat, dipakai reaksi `Welcome`.
11. **Piringan badge** — kanvas 9×9 di `(20, 0)` dengan `BadgeScale`, `BadgePulse`, `BadgeShift`.
12. **Delapan ikon badge** — lonceng, weker, tetes air, stopwatch, bulan+bintang, bluetooth, clipboard+centang, kamera.
13. **Partikel** — 2 not musik, 3 huruf `Z`, 2 sparkle; semua `IsHitTestVisible="False"`, opacity default 0.

**Urutan layer:** lengan (di belakang badan) → badan → wajah (mata/blush) → flash → partikel → badge (paling atas).

---

## 5️⃣ Kontrak Teknis & Performa

| Aturan | Ketentuan |
| :--- | :--- |
| Format | **Vektor murni** (Path/Ellipse/Border/Rectangle), tanpa bitmap |
| Kanvas | 28×20 px, diskalakan `Viewbox Stretch="Uniform"` |
| Legibilitas | Semua ekspresi harus dikenali pada ukuran collapsed |
| Collapsed | Idle & Sleep **wajib statis** (tidak ada storyboard infinite); Music boleh beranimasi |
| Expanded | Animasi penuh 30 FPS |
| Timer adaptif | Expanded 33ms; collapsed 150ms (kursor bergerak) / 250ms (diam); tidur 300ms |
| Interaksi | Latar transparan, `Focusable="False"`, event mouse ditangani wrapper |
| Auto-sleep | 300 detik tanpa gerak kursor; bangun saat kursor bergerak atau musik mulai |
| Sapaan startup | Overlay `WelcomeOverlay` di `PillContainer` memuat `MochiPetControl` kedua berskala `1.3×`; melambai `3.0` detik, lalu meluncur `560ms` ke pusat pet collapsed sambil mengecil ke ukuran persis pet tujuan (`min(W/28, H/20)`), konten notch fade-in; pet tujuan disembunyikan selama slide lalu crossfade `180ms` tepat saat overlay mendarat; dibatalkan saat expand/hide/slim |

---

## ✅ Checklist Kesiapan Karakter Baru

- [ ] 3 set mata lengkap dan aligned dengan pivot wajah
- [ ] Pupil bisa digeser dan ter-clip di dalam mata
- [ ] Blush kiri/kanan tersedia
- [ ] Headphone bisa muncul/hilang tanpa merusak siluet
- [ ] Flash overlay menutup area tubuh
- [ ] 8 ikon badge tersedia dan terbaca di 9×9
- [ ] Partikel not, Z, dan sparkle tersedia
- [ ] Lengan lambai `Welcome` (opsional) kembali tersembunyi setelah animasi
- [ ] Ekspresi tetap jelas di ukuran collapsed
- [ ] Badan tidak beranimasi saat idle/sleep collapsed
- [ ] Transform badan memakai pivot bawah dan tidak memotong kanvas
- [ ] Mendukung seluruh 13 state tanpa elemen yang saling tumpang tindih
