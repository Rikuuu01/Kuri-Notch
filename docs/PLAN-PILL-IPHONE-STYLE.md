# 📱 Plan: Gaya Pill ala iPhone 17 Pro Max (Dynamic Island)

> Status: **RENCANA — belum diimplementasikan.** Dokumen ini disusun 30 Oktober 2026 setelah audit `NotchViewModel` (geometri collapsed/expanded), `UpdateNotchGeometry`/`BuildFillGeometry`, dan struktur Pengaturan. Minta persetujuan sebelum eksekusi.

---

## 1. Ringkasan

Tambahkan pengaturan **"Gaya Notch"** di Pengaturan → Tampilan & Umum dengan dua pilihan:

| Gaya | Deskripsi |
|---|---|
| **Klasik** (default) | Bentuk sekarang: notch menempel di tepi atas layar, sudut atas 12 px, sudut bawah 16 px. |
| **Pill iPhone** | Kapsul melayang ala **Dynamic Island iPhone 17 Pro Max**: ujung kiri-kanan bulat penuh (radius = tinggi/2), ada celah ±6 px dari tepi atas layar, plus titik lensa kamera kecil di sisi kanan dalam pill. |

Tujuan: memberi opsi tampilan "ponsel premium" tanpa mengubah perilaku notch (hover, expand, lirik dinamis, pet) dan tanpa menambah beban render idle. Default tetap **Klasik** agar tampilan pengguna lama tidak berubah sampai mereka memilih sendiri.

---

## 2. Perilaku yang diinginkan (UX)

1. **Collapsed (pill)**: kapsul penuh dengan celah atas ±6 DIP (skala mengikuti `NotchScale`, clamp 4–10 px). Lebar mengikuti widget aktif persis seperti sekarang; tinggi tetap `CollapsedHeight = 32`.
2. **Lensa kamera**: titik gelap Ø ±7 DIP (fill `#050507`, ring `#1E1E24`, opacity 0.9) di sisi kanan dalam pill, ala kamera depan iPhone. Hanya tampil di state collapsed saat gaya pill; ikut mengecil/membesar bersama `ApplyNotchScale`.
3. **Hover stretch**: pill melebar sedikit seperti sekarang, bentuk tetap kapsul (radius tetap tinggi/2).
4. **Expanded**: panel tumbuh ke bawah dari pill dengan celah atas yang sama; sudut panel memakai radius 26 (bukan radius tinggi/2) agar tidak jadi kapsul raksasa. Konten collapsed & expanded ikut bergeser turun sebesar celah.
5. **Transisi gaya**: memilih gaya di Pengaturan langsung diterapkan (tanpa restart) dengan animasi radius/offset ±240 ms, memakai jalur animasi yang sudah ada.
6. **Tidak berubah**: widget (clock/media/battery/volume/notifikasi), lirik dinamis, pet, shadow/glow, window shrink (904×180 ↔ 904×880), dan **slim bar fullscreen** (bukan island — tetap seperti sekarang).

---

## 3. Desain teknis

### 3.1 Model & pengaturan

| Perubahan | Detail |
|---|---|
| `Models/AppSettings.cs` | Properti baru `NotchShapeStyle` (string, default `"Classic"`, nilai valid: `Classic` / `IPhonePill`). |
| `ViewModels/SettingsViewModel.cs` | `ObservableCollection<string> AvailableNotchShapes` + `SelectedNotchShape` (pola identik dengan `AvailableMaterials`/`SelectedMaterial`), load dari settings, autosave (`OnSelectedNotchShapeChanged → AutoSave`), reset default, dan ikut tersimpan di jalur save yang ada. |
| `Views/SettingsWindow.xaml` | Tambah `ComboBox` "Gaya Notch" di kartu **Posisi & Ukuran Notch** (di bawah slider Notch Scale, sebelum divider Hover Collapse Delay), memakai `ModernComboBox`. |
| `Services/LocalizationService.cs` | Key EN/ID: `Settings.NotchShapeTitle`, `Settings.NotchShapeDesc`, `Settings.NotchShapeClassic`, `Settings.NotchShapeIPhonePill`. |
| `ViewModels/NotchViewModel.cs` | Properti `NotchShapeStyle` hasil load settings; helper `IsIPhonePillStyle`. Nilai `TargetCornerRadius` (baris ~1304/1349) menjadi kondisional: expanded `pill ? 26 : ExpandedCornerRadius(22)`, collapsed `pill ? CollapsedHeight/2 : CollapsedCornerRadius(16)`. |

### 3.2 Geometri (`NotchWindow.xaml.cs`)

- `UpdateNotchGeometry` saat ini memanggil `BuildFillGeometry(notchWidth, notchHeight, earRadius: 12, bottomRadius: NotchBottomRadius)`. Ubah `earRadius` menjadi parameter style:
  - **Klasik**: `earRadius = 12` (tetap).
  - **Pill collapsed**: `earRadius = notchHeight / 2` (16) → kapsul penuh; `bottomRadius = notchHeight / 2` juga.
  - **Pill expanded**: `earRadius = 26`, `bottomRadius = 26`.
- Lebar visual pill bertambah `2 × (radius − 12)` = **8 DIP** saat collapsed; `PillContainer.Width`/`ContentHost.Width` sudah dihitung dari `notchWidth + 2 × earRadius` sehingga otomatis ikut. Tidak perlu mengubah rumus lebar per widget di `UpdateGeometryForState`; cukup tambahkan +8 px pada basis `PillContainer` dijalur yang sama (sudah otomatis lewat `earRadius`).
- `NotchStrokePath`, `NotchStrokeFadePath`, `NotchGlowPath`, `NotchBackgroundFadePath` memakai `data/data2` yang sama → ikut berubah tanpa perubahan tambahan.

### 3.3 Celah atas (gap) dan posisi konten

- DP baru `NotchTopGap` (0 = klasik, `6 × faktor skala` = pill, clamp 4–10).
- Satu sumber pergeseran: host visual notch (path + stroke + glow) dan `PillContainer` digeser turun sebesar `NotchTopGap`, dan `ExpandedContentGrid`/`ContentHost` mendapat offset yang sama saat expanded. Ini menjaga:
  - bentuk dan konten selalu berimpit;
  - hit-area hover (`NotchBackgroundPath.Data.FillContains` + `PillContainer` bounds) persis mengikuti bentuk kapsul, bukan area gap;
  - area gap di atas pill tetap transparan/click-through karena window background `null`.
- Tinggi window collapsed 180 px masih cukup: 32 (pill) + 6 (gap) + hover stretch + shadow ≈ < 100 px.

### 3.4 Animasi & render

- Transisi gaya memakai jalur yang sudah ada: `AnimateToCurrentState` + `BeginAnimation(NotchBottomRadiusProperty)`; tambahkan animasi `NotchTopGap`/offset 0 ↔ gap bersamaan (240–260 ms, `QuarticEase`).
- Tidak ada animasi `Forever` baru. Lensa kamera hanya `Opacity` 0/1 dengan fade 150 ms saat ganti gaya — biaya render idle nol.
- Fade/glow mengikuti geometri baru tanpa rasterisasi tambahan.

### 3.5 Hit-testing & input

- `NotchBackgroundPath.Data.FillContains(hitPoint)` (baris ~7684/22589/22797) otomatis mengikuti data baru karena memakai `Data` yang sama.
- `PillContainer` ikut digeser sehingga `MouseEnter/Leave/Click` hanya aktif di area kapsul.
- Drag & drop (`AllowDrop`) tetap di window level; tidak ada perubahan.

### 3.6 Sisi yang **tidak** disentuh

- Slim bar fullscreen (`FullscreenSlimBarHost`, morph 440 ms) — gaya pill tidak berlaku di sana.
- Window shrink (`GetOverlayWindowWidth`/`ApplyOverlayWindowSize`) — lebar 904 px sudah aman untuk pill.
- Material latar (`NotchMaterial`) — fill brush tidak berubah; pill memakai brush yang sama.

---

## 4. File yang tersentuh

| File | Perubahan |
|---|---|
| `NotchWin/Models/AppSettings.cs` | + `NotchShapeStyle` |
| `NotchWin/ViewModels/SettingsViewModel.cs` | + `AvailableNotchShapes`, `SelectedNotchShape`, load/save/reset |
| `NotchWin/ViewModels/NotchViewModel.cs` | + `NotchShapeStyle`, radius kondisional per state |
| `NotchWin/Views/SettingsWindow.xaml` | + ComboBox "Gaya Notch" |
| `NotchWin/Views/NotchWindow.xaml` | + `Ellipse` lensa kamera; DP `NotchTopGap`; offset host visual |
| `NotchWin/Views/NotchWindow.xaml.cs` | `BuildFillGeometry`/`BuildStrokeGeometry` ber-parameter style, `UpdateNotchGeometry`, `AnimateToCurrentState`, animasi gap, listener perubahan settings (pola `ApplyNotchScale` + `PositionOnMonitor`) |
| `NotchWin/Services/LocalizationService.cs` | + 4 key EN/ID |
| `docs/DECISIONS.md` | + Keputusan baru setelah implementasi |
| `docs/PLAN-PILL-IPHONE-STYLE.md` | dokumen ini |

---

## 5. Fase implementasi

| Fase | Isi | Estimasi |
|---|---|---|
| 1 | Model + Settings UI + localisasi + persist & live-apply (tanpa bentuk dulu; gaya pill sementara sama dengan klasik) | 0.5 hari |
| 2 | Geometri kapsul + gap + offset konten + animasi transisi | 1 hari |
| 3 | Lensa kamera + polish (hover stretch, expanded radius, skala 0.75–1.40) | 0.5 hari |
| 4 | Verifikasi matriks uji + update docs/DECISIONS | 0.5 hari |

**Kriteria selesai:** kedua gaya tampil benar di collapsed & expanded, transisi mulus, settings persist setelah restart, dan tidak ada regresi pada checklist §6.

---

## 6. Checklist uji

- [ ] Ganti gaya Klasik ↔ Pill dari Pengaturan: langsung berubah, animasi radius/gap mulus, persist setelah restart.
- [ ] Collapsed: clock, media (dengan/tanpa lirik), battery, volume, notifikasi (juga varian air alarm 340 px), pet.
- [ ] Hover stretch pill dan hover expand → collapse.
- [ ] Lirik panjang (dynamic width) di gaya pill tidak ter-ellipsis dan tidak melebihi 620 DIP.
- [ ] Expanded: control center, cuaca, todo (panel tinggi 712), download strip, settings.
- [ ] Skala notch 0.75 / 1.0 / 1.40 + DPI 100% / 125% / 150% + multi-monitor.
- [ ] Slim bar fullscreen tidak terpengaruh.
- [ ] Drag & drop file ke notch, klik kanan context menu, topmost, lock/unlock, suspend/resume.
- [ ] Area gap di atas pill click-through ke desktop.
- [ ] Idle CPU tidak naik dibanding baseline gaya klasik (`tools/measure-perf.ps1`).

---

## 7. Risiko & mitigasi

| Risiko | Mitigasi |
|---|---|
| Offset gap membuat konten & bentuk tidak sejajar saat animasi state | Satu DP `NotchTopGap` + satu transform untuk path, pill, dan konten; uji transisi cepat expand/collapse berulang |
| Lebar pill bertambah 8 px membuat lirik dinamis terpotong | Lebar sudah otomatis ikut `earRadius`; uji lirik panjang di skala maksimum |
| Hit-area hover bergeser (gap) | `FillContains` memakai `Data` baru; uji hover di tepi atas kapsul dan di area gap |
| Gaya pill terlihat aneh pada material transparan (Tinted Glass/Mica) | Uji ketiga material; fallback tetap gaya Klasik |
| Menambah opsi menambah beban config | Satu string setting dengan default; rollback cukup ganti ke Klasik |

---

## 8. Yang tidak dilakukan

- Tidak mengubah ukuran/logika window shrink (hasil optimasi Fase 2 dipertahankan).
- Tidak menyentuh slim bar fullscreen, morph, dan peek.
- Tidak menambah dependency atau animasi `Forever` baru.
- Tidak mengubah bentuk khusus seperti notch MacBook lama — hanya dua gaya di atas.
