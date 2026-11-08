# Plan: Fullscreen Slim Bar — Garis Notch saat Aplikasi Fullscreen

> Indeks seluruh dokumentasi: [README.md](README.md)

> **Status:** ✅ Implemented (v0.4.0) + Hover-Peek (v0.5.0)
> **Tanggal:** 27 Oktober 2026
> **Target versi:** 0.4.0 → 0.5.0 (hover-peek)
> **Modul terkait:** [00-arsitektur-dan-notch-core.md](00-arsitektur-dan-notch-core.md), [10-pengaturan-dan-tema.md](10-pengaturan-dan-tema.md)

---

## 1. Latar Belakang & Tujuan

Saat ini, ketika **FullscreenDetector** mendeteksi aplikasi layar penuh (game borderless, video F11, presentasi), notch **disembunyikan total** (`Window.Visibility = Collapsed`) sehingga tidak ada penanda apa pun di layar.

**Tujuan perubahan:**
- Ganti perilaku "hilang total" menjadi **garis tipis horizontal** (slim bar) di tengah atas layar — bentuknya seperti notch yang dipadatkan menjadi satu garis.
- Garis tetap terlihat di atas aplikasi fullscreen (borderless), tetapi **100% click-through** sehingga tidak mengganggu game/video.
- Perilaku lama (sembunyi total) tetap tersedia sebagai opsi di Pengaturan.

---

## 2. Perilaku Baru (Ringkas)

| Kondisi | `IsFullscreenAutoHideEnabled` | `IsFullscreenSlimBarEnabled` | Hasil |
|:---|:---:|:---:|:---|
| Aplikasi normal (tidak fullscreen) | apa pun | apa pun | Notch normal |
| Fullscreen aktif | `false` | apa pun | Notch normal (tidak berubah) |
| Fullscreen aktif | `true` | `true` **(default baru)** | **Garis tipis** di atas layar |
| Fullscreen aktif | `true` | `false` | Sembunyi total (perilaku lama) |
| Manual hidden dari tray | apa pun | apa pun | Tidak ada yang tampil |
| Keluar dari fullscreen | — | — | Notch kembali (state collapsed) |

**State machine sederhana:**

```
                 fullscreen detected
   ┌──────────┐ ───────────────────────► ┌──────────────────┐
   │  NOTCH   │                          │  SLIM BAR MODE   │
   │ (normal) │ ◄─────────────────────── │ (garis 220×6 px) │
   └──────────┘    fullscreen exited     └──────────────────┘
        ▲                                          │
        │         tiny toggle dimatikan            │
        └──────────────────────────────────────────┘
                     (sembunyi total)
```

---

## 3. Mockup Visual

**Kondisi normal (collapsed):**

```
┌─────────────────────────────────────────────┐  ← bezel monitor
│            ▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄               │
│        ▄▄▀  09:41   ⛅ 28°  🔋 80%  ▀▄▄     │  ← notch collapsed
└─────────────────────────────────────────────┘
              Aplikasi fullscreen di belakang
```

**Kondisi fullscreen (baru):**

```
┌─────────────────────────────────────────────┐  ← bezel monitor
│                  ▄▄▄▄▄▄▄▄▄▄▄▄               │  ← slim bar 220×6 px
│                                             │
│          Game / Video Fullscreen            │
│                                             │
└─────────────────────────────────────────────┘
```

**Pilihan bentuk (rekomendasi: A):**

| Opsi | Deskripsi | Catatan |
|:---:|:---|:---|
| **A (rekomendasi)** | Kapsul garis: lebar 220px, tinggi 6px, sudut bawah membulat penuh, menempel di tepi atas | Paling bersih, mudah dianimasikan |
| B | Garis dengan "telinga" notch (fillet 12px) | Terlihat seperti notch mini, tapi di tinggi 6px lengkungan sulit dibaca |
| C | Kapsul + garis aksen tipis di bawahnya | Sedikit lebih premium; bisa jadi opsi tema nanti |

---

## 4. Spesifikasi Visual Slim Bar

| Properti | Nilai |
|:---|:---|
| Lebar | `220px × NotchScale` (dibulatkan ke integer) |
| Tinggi | `6px × NotchScale` (minimal 4px) |
| Corner radius | `0,0,6,6` (menempel di atas, membulat di bawah) |
| Background | `{DynamicResource NotchBackgroundBrush}` — ikut tema OLED / Tinted / Mica |
| Border | `{DynamicResource NotchStrokeBrush}`, tebal `1,0,1,1` |
| Shadow | `DropShadowEffect` blur 8, opacity 0.35 (hanya pada layer background, tanpa teks) |
| Posisi | Horizontal center, `VerticalAlignment=Top`, `Y=0` |
| Hit-test | **100% click-through** (`HTTRANSPARENT`) — tidak menangkap klik sama sekali |
| IsHitTestVisible | `False` |
| Rendering | `SnapsToDevicePixels=True`, `UseLayoutRounding=True` (anti-blur) |

**Opsional (fase lanjutan):**
- Pulse aksen 1× (800ms) saat notifikasi/timer/alarm masuk — tanpa teks, agar tidak mengganggu game.
- Garis menebal sementara saat volume berubah (HUD minimalis).

---

## 5. Keputusan Desain yang Perlu Dikonfirmasi

1. **Interaksi garis:** statis & click-through (rekomendasi) **atau** hover untuk membuka notch penuh?
   - Statis = paling aman untuk game, implementasi sederhana.
   - Hover = berisiko memicu notch terbuka saat kursor melewati atas layar.
2. **Default untuk pengguna lama:** langsung slim bar (rekomendasi, sesuai permintaan) atau tetap sembunyi total sampai diaktifkan manual?
3. **Bentuk visual:** kapsul polos (A, rekomendasi), notch mini (B), atau kapsul + garis aksen (C)?
4. **Pulse notifikasi:** perlu sejak versi pertama atau fase berikutnya?

---

## 6. Rancangan Teknis

### 6.1 Titik Integrasi yang Sudah Ada

| Komponen | Perilaku saat ini |
|:---|:---|
| `Services/FullscreenDetector.cs` | Polling 1500ms; event `FullscreenChanged`; pakai `SHQueryUserNotificationState` + cek geometri window foreground |
| `ViewModels/NotchViewModel.cs` | `IsFullscreenHidden`, `IsManuallyHidden`, `IsNotchVisible`; hook `FullscreenChanged` memanggil `Collapse()` + `VisibilityChanged` |
| `Views/NotchWindow.xaml.cs` → handler `VisibilityChanged` | Memanggil `AnimateVisibility(ViewModel.IsNotchVisible)` |
| `AnimateVisibility(false)` | Animasi pill menyusut + fade, lalu `Window.Visibility = Collapsed` |
| `WndProc` `case 132` (`WM_NCHITTEST`) | Mengembalikan `HTTRANSPARENT (-1)` jika `!IsNotchVisible` |
| `Views/SettingsWindow.xaml` panel Sistem | Toggle `IsFullscreenAutoHideEnabled` |
| `Models/AppSettings.cs` | Belum ada properti mode fullscreen |

### 6.2 Perubahan per File

#### A. `Models/AppSettings.cs`
```csharp
// true  = tampilkan garis tipis saat fullscreen
// false = sembunyikan total (perilaku lama)
public bool IsFullscreenSlimBarEnabled { get; set; } = true;
```
> Master switch tetap `IsFullscreenAutoHideEnabled`. Karena `SettingsService` melakukan deserialisasi ke objek baru, pengguna lama otomatis mendapat nilai default `true`.

#### B. `ViewModels/NotchViewModel.cs`

Ganti properti visibilitas menjadi:

```csharp
public bool IsFullscreenActive { get; private set; }

public bool IsFullscreenSlimBarEnabled =>
    _settingsService.CurrentSettings.IsFullscreenSlimBarEnabled;

// Notch normal hanya terlihat jika TIDAK fullscreen dan TIDAK manual-hidden
public bool IsNotchVisible => !IsFullscreenActive && !IsManuallyHidden;

// Garis tampil jika fullscreen + mode slim + tidak manual-hidden
public bool IsSlimBarVisible => IsFullscreenActive
    && IsFullscreenSlimBarEnabled
    && !IsManuallyHidden;

// Sembunyi total = fullscreen + mode hide
public bool IsFullscreenHidden => IsFullscreenActive && !IsFullscreenSlimBarEnabled;
```

Hook `FullscreenChanged`:
```csharp
_fullscreenDetector.FullscreenChanged += (s, isFullscreen) =>
{
    if (!_settingsService.CurrentSettings.IsFullscreenAutoHideEnabled) return;

    IsFullscreenActive = isFullscreen;
    if (isFullscreen) Collapse();
    VisibilityChanged?.Invoke(this, EventArgs.Empty);
};
```

`ApplyCurrentSettings` saat setting berubah ketika sedang fullscreen:
```csharp
if (IsFullscreenActive)
    VisibilityChanged?.Invoke(this, EventArgs.Empty);
```
Jika master toggle dimatikan → `IsFullscreenActive = false`, lalu fire event.

Tambahkan juga evaluasi awal saat `Start()` detector (lihat 6.4).

#### C. `Views/NotchWindow.xaml`

Tambahkan elemen **saudara** `PillContainer` di root `Grid` (bukan di dalam pill):

```xml
<Grid x:Name="FullscreenSlimBarHost"
      HorizontalAlignment="Center" VerticalAlignment="Top"
      Visibility="Collapsed" Opacity="0"
      IsHitTestVisible="False"
      SnapsToDevicePixels="True" UseLayoutRounding="True">

  <Border x:Name="FullscreenSlimBar"
          Width="220" Height="6"
          CornerRadius="0,0,6,6"
          Background="{DynamicResource NotchBackgroundBrush}"
          BorderBrush="{DynamicResource NotchStrokeBrush}"
          BorderThickness="1,0,1,1"
          RenderTransformOrigin="0.5,0">
    <Border.RenderTransform>
      <ScaleTransform x:Name="FullscreenSlimBarScale" ScaleX="0.6" ScaleY="1"/>
    </Border.RenderTransform>
    <Border.Effect>
      <DropShadowEffect BlurRadius="8" ShadowDepth="2" Color="#000000" Opacity="0.35"/>
    </Border.Effect>
  </Border>
</Grid>
```

#### D. `Views/NotchWindow.xaml.cs`

1. **Handler `VisibilityChanged`** — bercabang:
```csharp
private void UpdateWindowVisibility()
{
    if (ViewModel.IsSlimBarVisible)
        EnterSlimBarMode();
    else
    {
        ExitSlimBarMode();
        AnimateVisibility(ViewModel.IsNotchVisible);
    }
}
```

2. **`EnterSlimBarMode()`**
   - Guard: jika sudah di mode slim, return.
   - Set `_isSlimBarMode = true`, `Visibility = Visible`, `Topmost = true`.
   - Animasi pill keluar **tanpa** memanggil jalur yang meng-collapse window (refactor bagian hide pada `AnimateVisibility` menjadi `AnimatePillHide(bool collapseWindow)`).
   - Tampilkan bar: `Opacity 0→1` (180ms), `ScaleX 0.6→1` (220ms, `CubicEase EaseOut`), mulai setelah delay 60ms.
   - `PillContainer.IsHitTestVisible = false`.

3. **`ExitSlimBarMode()`**
   - Guard: jika bukan mode slim, return.
   - Fade-out bar 140ms → `Visibility = Collapsed`.
   - `_isSlimBarMode = false`; `PillContainer.IsHitTestVisible` dipulihkan oleh `AnimateVisibility(true)`.

4. **Perbaikan race animation:** pada `Completed` milik hide-animation, ubah guard menjadi:
```csharp
if (_isHiding && !ViewModel.IsNotchVisible && !ViewModel.IsSlimBarVisible)
    Visibility = Visibility.Collapsed;
```
Agar hide animation yang masih berjalan tidak menutup window setelah bar muncul.

5. **`WM_NCHITTEST` (`case 132`)** — eksplisit untuk mode slim:
```csharp
if (ViewModel.IsSlimBarVisible)
{
    handled = true;
    return new IntPtr(NativeMethods.HTTRANSPARENT); // -1
}
```
> Sebenarnya guard `!IsNotchVisible` sudah mengembalikan -1, tetapi pengecekan eksplisit membuat niat kode jelas dan aman jika nanti bar dibuat interaktif.

6. **`ApplyNotchScale`** — sekaligus perbarui ukuran bar:
```csharp
double s = scale * 1.2; // mengikuti BaseScaleFactor notch
FullscreenSlimBar.Width  = Math.Round(220 * s);
FullscreenSlimBar.Height = Math.Max(4, Math.Round(6 * s));
```
(Pastikan tetap integer pixel agar tajam.)

#### E. `ViewModels/SettingsViewModel.cs`
- Tambah `[ObservableProperty] private bool _isFullscreenSlimBarEnabled;`
- `partial void OnIsFullscreenSlimBarEnabledChanged(bool value) => AutoSave();`
- Muat di `LoadSettings`: `IsFullscreenSlimBarEnabled = settings.IsFullscreenSlimBarEnabled;`
- Simpan di `ToSettings`: `IsFullscreenSlimBarEnabled = IsFullscreenSlimBarEnabled,`
- Reset default: ikut `defaults.IsFullscreenSlimBarEnabled`.

#### F. `Views/SettingsWindow.xaml`
Di bawah toggle fullscreen (panel Sistem), tambahkan sub-opsi yang hanya muncul saat master toggle aktif:

```xml
<StackPanel Margin="0,0,0,12"
            Visibility="{Binding IsFullscreenAutoHideEnabled,
                         Converter={StaticResource BoolToVis}}">
    <CheckBox Style="{StaticResource FluentToggleSwitch}"
              IsChecked="{Binding IsFullscreenSlimBarEnabled, Mode=TwoWay}">
        <StackPanel>
            <TextBlock Text="{Binding L10n[Settings.FullscreenSlimBarTitle]}"
                       FontWeight="SemiBold" Foreground="#F4F4F5"/>
            <TextBlock Text="{Binding L10n[Settings.FullscreenSlimBarDesc]}"
                       FontSize="11" Foreground="#A1A1AA" Margin="0,2,0,0"/>
        </StackPanel>
    </CheckBox>
</StackPanel>
```

#### G. `Services/LocalizationService.cs`
Tambahkan kunci EN & ID:

| Key | English | Bahasa Indonesia |
|:---|:---|:---|
| `Settings.FullscreenSlimBarTitle` | Show Slim Line in Fullscreen | Tampilkan Garis Tipis saat Layar Penuh |
| `Settings.FullscreenSlimBarDesc` | Keep a small notch line at the top of the screen instead of hiding completely | Tampilkan garis notch kecil di atas layar, tidak sembunyi total |

Opsional: perbarui `Settings.FullscreenHideDesc` agar menyebut kedua mode.

#### H. `Services/FullscreenDetector.cs` (peningkatan kecil)
- Panggil `OnPollTick` sekali di `Start()` agar kondisi fullscreen yang sudah aktif terdeteksi **segera**, bukan setelah 1500ms (mencegah kedipan notch saat app dijalankan di tengah fullscreen).

#### I. Dokumentasi
- `README.md` — bagian Auto-Hide Fullscreen.
- `CARA-MENJALANKAN.md` — langkah uji mode slim bar.
- `00-arsitektur-dan-notch-core.md` — bagian sub-notch / visibility.
- `10-pengaturan-dan-tema.md` — setting baru.
- `DECISIONS.md` — keputusan baru (#26: Fullscreen Slim Bar).
- `ROADMAP.md` — fase terkait.

---

## 7. Animasi & Timing

| Transisi | Durasi | Easing |
|:---|:---:|:---|
| Masuk fullscreen: pill keluar | 160ms | `CubicEase EaseIn` |
| Masuk fullscreen: bar muncul (fade + scaleX) | 220ms (delay 60ms) | `CubicEase EaseOut` |
| Keluar fullscreen: bar menghilang | 140ms | `CubicEase EaseIn` |
| Keluar fullscreen: notch kembali | 350ms (existing) | `BackEase EaseOut` (existing) |

> Jika `SystemParameters.ClientAreaAnimation == false` (aksesibilitas), langsung set state tanpa animasi.

---

## 8. Matriks Perilaku & Edge Case

| # | Skenario | Perilaku yang Diharapkan |
|:---:|:---|:---|
| 1 | YouTube F11 / video borderless | Garis tampil; klik di area garis tembus ke video |
| 2 | Game borderless | Garis tampil; input game tidak terganggu |
| 3 | Game exclusive fullscreen (D3D) | Garis tidak terlihat (keterbatasan OS); notch kembali saat keluar |
| 4 | Presentasi PowerPoint | Sama seperti fullscreen biasa |
| 5 | Fullscreen di monitor sekunder | **V1:** garis di monitor target; **V2 (opsional):** deteksi monitor fullscreen |
| 6 | DPI 125% / 150% / 200% | Bar tetap tajam & center (pixel snap) |
| 7 | Ganti tema/material saat bar tampil | Warna bar ikut berubah via `DynamicResource` |
| 8 | Slider skala notch diubah | Ukuran bar ikut berubah proporsional |
| 9 | Master toggle dimatikan saat fullscreen | Notch langsung kembali normal |
| 10 | Mode diganti (slim ↔ hide) saat fullscreen | Transisi langsung tanpa restart |
| 11 | Manual hidden dari tray | Bar & notch sama-sama tidak tampil |
| 12 | App start saat sudah fullscreen | Detector cek langsung (tidak ada kedipan notch) |
| 13 | Multi-monitor dicabut / resolusi berubah | `PositionOnMonitor` reposisi; bar tetap center |
| 14 | Hover garis < 3 detik lalu keluar | Charge dibatalkan, garis kembali ke ukuran normal, notch tidak terbuka |
| 15 | Hover garis 3 detik kontinu | Garis bermorph menjadi pill notch (collapsed); hover hanya mempertahankan pill, panel tidak terbuka otomatis |
| 16 | Kursor keluar setelah peek | Notch collapse dan kembali menjadi garis tipis (delay collapse + 340 ms) |
| 17 | Peek dipin (`IsLockedExpanded`) | Notch tetap terbuka saat kursor keluar sampai pin dilepas |
| 18 | Klik pill saat peek | Panel penuh terbuka tanpa mengunci; kursor keluar mengembalikan ke garis |
| 19 | Hover pill saat peek tanpa klik | Pill tetap tampil; panel penuh tidak terbuka |
| 20 | Tekan `Esc` untuk keluar fullscreen | NotchWin tetap berjalan; Esc tidak pernah menutup aplikasi saat fullscreen/slim/peek (hanya menutup peek bila aktif) |

---

## 9. Rencana Pengujian

**Build (sesuai aturan repo, tanpa menjalankan app):**
```powershell
dotnet build NotchWin.sln -c Release
```

**Manual test checklist:**
1. Buka YouTube di Edge → tekan `F11` → garis muncul, klik video tetap berfungsi.
2. Buka game borderless → garis muncul; mainkan 1–2 menit, tidak ada input terblokir.
3. Tekan `Esc` keluar fullscreen → notch kembali dengan animasi normal **dan aplikasi tetap berjalan** (tidak tertutup).
4. Matikan toggle "Auto-hide on Fullscreen Apps" → notch tetap tampil saat fullscreen.
5. Aktifkan master toggle, matikan sub-toggle slim → perilaku lama (hilang total).
6. Ubah tema OLED → Tinted Glass → Mica saat garis tampil → warna ikut berubah.
7. Ubah skala notch 75% / 100% / 140% → ukuran garis proporsional.
8. Restart aplikasi → setting tersimpan.
9. Multi-monitor: pilih monitor 2 → garis muncul di monitor 2.
10. Notifikasi/timer saat fullscreen → tidak memunculkan popup yang mengganggu.
11. Hover garis < 3 detik lalu geser keluar → charge batal, notch tidak terbuka.
12. Hover garis 3 detik → garis membesar lalu bermorph menjadi pill notch.
13. Hover di atas pill tanpa klik → pill tetap tampil dan panel **tidak** terbuka. Klik pill → panel penuh terbuka; geser kursor keluar → notch collapse lalu kembali menjadi garis.
14. Buka panel dari peek lalu klik Pin → notch tetap terbuka saat kursor keluar; lepas Pin → kembali ke garis.
15. Klik area di sekitar garis (bukan hover) → klik tetap diteruskan ke aplikasi fullscreen.

---

## 10. Urutan Implementasi

| # | Langkah | Estimasi |
|:---:|:---|:---:|
| 1 | `AppSettings` + `SettingsViewModel` + kunci lokalisasi | 45 menit |
| 2 | Properti state di `NotchViewModel` + hook fullscreen | 45 menit |
| 3 | Elemen visual slim bar di `NotchWindow.xaml` | 30 menit |
| 4 | Animasi & branching di `NotchWindow.xaml.cs` | 1.5–2 jam |
| 5 | UI sub-toggle di `SettingsWindow.xaml` | 30 menit |
| 6 | Edge case, hit-test, race guard, pixel snapping | 1 jam |
| 7 | Build + manual testing | 1 jam |
| 8 | Dokumentasi + bump versi + rebuild installer | 45 menit |
| | **Total** | **±6 jam** |

---

## 11. Risiko & Mitigasi

| Risiko | Dampak | Mitigasi |
|:---|:---|:---|
| Window transparan tetap menangkap klik | Input game terblokir | `WM_NCHITTEST` eksplisit `HTTRANSPARENT` di mode slim + `IsHitTestVisible=False` |
| Race animasi hide lama menutup window | Bar tidak muncul | Guard `_isSlimBarVisible` pada callback `Completed` |
| Anti-cheat game mendeteksi overlay | Game menolak | Sediakan opsi "sembunyikan total"; bar sangat kecil & non-interaktif |
| Garis terlihat di atas video yang terang | Mengganggu secara visual | Tinggi hanya 6px, material gelap, bisa dimatikan |
| Fullscreen exclusive tidak bisa di-overlay | Garis tidak muncul | Dokumentasikan sebagai keterbatasan OS |
| Deteksi fullscreen telat 1.5s | Notch sempat terlihat | Pengecekan langsung saat `Start()` detector |

---

## 12. Definisi Selesai (Definition of Done)

- [ ] Saat aplikasi fullscreen (borderless), garis tipis tampil di tengah atas layar.
- [ ] Garis 100% click-through; tidak mengganggu input game/video.
- [ ] Notch kembali normal saat keluar fullscreen.
- [ ] Toggle di Pengaturan berfungsi dan tersimpan setelah restart.
- [ ] Perilaku lama (sembunyi total) masih bisa dipilih.
- [ ] Tema, skala notch, DPI, dan multi-monitor tidak merusak posisi/ukuran garis.
- [ ] Hover 3 detik membuka notch dengan morph halus; hover < 3 detik tidak membuka.
- [ ] Saat peek fullscreen, hover di atas pill tidak membuka panel; panel hanya terbuka setelah klik pill.
- [ ] Di notch biasa (non-fullscreen), hover tetap otomatis membuka panel seperti sebelumnya.
- [ ] Notch kembali menjadi slim bar saat kursor keluar, kecuali sedang dipin.
- [ ] Interaksi dengan notch tidak membuat `FullscreenDetector` mengira fullscreen telah berakhir.
- [ ] `dotnet build` sukses tanpa error.
- [ ] Dokumentasi diperbarui dan installer versi baru berhasil dibangun.

---

## 13. Update v0.5.0 — Hover-Peek 3 Detik

### 13.1 Perilaku

| Tahap | Aksi | Implementasi |
|:---|:---|:---|
| Deteksi | Kursor masuk area garis | `DispatcherTimer` 100 ms → `GetCursorPos` → `TryGetSlimBarHoverRect` |
| Charge | Kursor bertahan | `SlimBarHoverScale` ScaleX `1 → 1.15`, ScaleY `1 → 1.55` selama 3000 ms |
| Batal | Kursor keluar sebelum 3 detik | Grace 240 ms untuk gerakan kecil; setelah itu `CancelSlimBarHoverCharge()` mengembalikan skala dalam 180 ms |
| Buka | 3 detik tercapai | `OpenSlimBarPeek()` → `IsSlimBarPeekActive = true` |
| Morph | `UpdateWindowVisibility()` | `ExitSlimBarMode(morphToNotch: true)` → `AnimateSlimBarMorphToNotch()`: pill beserta kontennya disiapkan tepat seukuran visual slim bar, bar di-fade-out 80 ms pada ukuran yang sama, lalu pill tumbuh sebagai satu bentuk ke ukuran notch penuh selama 360 ms (konten tetap terlihat); total 440 ms |
| Lanjut | 460 ms setelah morph | `SchedulePostMorphHoverCheck()` → `OnMouseEnter` bila kursor di atas notch, atau `OnMouseLeave` bila di luar. Saat peek, `OnMouseEnter` hanya mempertahankan pill (tidak expand) |
| Panel | Klik pill | `OnPillClicked()` → `Expand(isLock: false)`; hanya klik yang membuka panel penuh saat peek |
| Tutup | Kursor keluar | Collapse normal (bila panel sempat terbuka), lalu `ScheduleSlimBarPeekClose(delay + 340 ms untuk panel Expanded; delay saja untuk hover-peek)` → `IsSlimBarPeekActive = false` |
| Morph balik | `UpdateWindowVisibility()` | `EnterSlimBarMode(morphFromNotch: true)` → `AnimateSlimBarMorphFromNotch()`: seluruh pill beserta kontennya disusutkan sebagai satu bentuk ke ukuran slim bar selama 360 ms (konten tetap opaque), lalu crossfade ke bar 80 ms; total 440 ms. Bila panel baru collapse, morph ditunda 300 ms agar geometri selesai lebih dulu |

### 13.2 Guard & Edge Case

- `IsSlimBarPeekActive` membuat `IsNotchVisible` tetap `true` saat fullscreen, tetapi `IsSlimBarVisible` menjadi `false` agar garis tidak dirender bersamaan dengan notch.
- `OpenSlimBarPeek()` hanya berjalan bila `IsFullscreenActive`, mode slim aktif, dan tidak `IsManuallyHidden`.
- Saat peek, `OnMouseEnter()` mengembalikan lebih awal: hanya membatalkan timer tutup dan memberi feedback stretch (`HoverStretchChanged`), tidak memanggil `ScheduleDelayedExpand()`. Hanya `OnPillClicked()` → `Expand(isLock: false)` yang membuka panel penuh.
- Klik pill saat peek memakai `Expand(isLock: false)`; pin eksplisit tetap mencegah auto-close.
- Perilaku hover-untuk-expand di notch biasa tidak berubah karena guard hanya aktif ketika `IsSlimBarPeekActive` bernilai `true`.
- `FullscreenDetector` mengembalikan status fullscreen terakhir bila foreground window adalah proses `NotchWin` sendiri, sehingga hover/klik notch tidak memicu exit fullscreen palsu.
- Saat keluar fullscreen, `IsSlimBarPeekActive` direset dan peek-close timer dibatalkan.
- Jika sub-opsi slim dimatikan saat peek aktif, peek juga direset.
- Saat morph balik (`AnimateSlimBarMorphFromNotch`) aktif, completion keyframe bounce pill dari animasi collapse (370 ms) dan animasi hover-stretch tidak boleh menimpa skala pill. Keduanya diberi guard `_notchToSlimMorphActive`, sehingga shrink 360 ms tidak dibatalkan di tengah jalan dan handoff ke slim bar selalu tercapai.
- Saat morph masuk (`AnimateSlimBarMorphToNotch`) selesai, `PillContainer.IsHitTestVisible` diaktifkan bila `IsNotchVisible` bernilai true, bukan hanya saat `IsSlimBarPeekActive`. Ini memastikan notch kembali bisa di-hover setelah keluar dari fullscreen, bukan hanya setelah peek. Fallback `ScheduleNotchInteractionRestore()` memulihkan hit-test 500 ms kemudian bila completion animasi terlewat.
- `PillContainer.LayoutTransform` memakai `CenterX = lebar / 2` dan `CenterY = 0` (titik tengah-atas). Sebelumnya origin `(0, 0)` membuat badan notch bergeser horizontal terhadap slim bar, sehingga terlihat melompat saat handoff. `UpdatePillLayoutTransformCenter()` memperbarui origin setiap geometri notch berubah.
- `PillContainer.RenderTransformOrigin` kini `0, 0` dengan `PillScaleTransform.CenterX = layoutScale * lebar / 2` dan `CenterY = 0`. Layout transform menggeser konten sehingga pusatnya berada di `layoutScale * lebar / 2`, bukan `lebar / 2`; poros render yang lama (`0.5, 0`) karena itu menghasilkan offset 19–26 px saat handoff. Poros render diperbarui di awal kedua morph, di `ApplyNotchScale()`, dan sebelum hover stretch mulai (stretch hanya aktif saat state bukan `Expanded`, termasuk saat lebar notch masih beranimasi setelah collapse).
- Saat expand, `ResetSlideViews(..., updateNavPill: false)` dijalankan lebih dulu, lalu satu `ExpandedContentGrid.UpdateLayout()` (kondisional), lalu `UpdateNavPill(..., animate: false)`. Ini menghindari layout pass kedua yang sebelumnya dipicu oleh perubahan visibilitas kartu dan lebar media setelah layout paksa pertama.
- **Topmost keeper (v0.5.1)**: `NotchWindow` memasang hook `EVENT_SYSTEM_FOREGROUND` + fallback `DispatcherTimer` 2000 ms yang memanggil `WindowStyleHelper.EnsureTopmost(hwnd)` (`HWND_TOPMOST` tanpa `SWP_SHOWWINDOW`). Assert dipaksa setelah window/slim bar tampil, dan hook dilepas di `OnClosed`. Ini mengatasi notch yang tertutup aplikasi yang menaikkan dirinya ke topmost setelah startup (video player fullscreen, game borderless, Task Manager always-on-top).
- **Deteksi fullscreen (v0.5.1)**: `FullscreenDetector` membandingkan `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)` (fallback `GetWindowRect`) dengan `rcMonitor` milik monitor window foreground dalam piksel fisik (toleransi 2 px), bukan DIP primary screen. Window maximized di scaling >100% tidak lagi dianggap fullscreen; window minimized diabaikan.
- **Guard Esc (v0.5.1)**: `Window_KeyDown` tidak pernah menutup aplikasi saat `_isSlimBarMode`, `IsFullscreenActive`, atau `IsSlimBarPeekActive` aktif. Esc hanya menutup peek bila aktif; di luar mode tersebut, penutupan lewat Esc mensyaratkan window `IsActive`. Ini mencegah tombol Esc yang dipakai aplikasi fullscreen untuk keluar mode ikut menutup NotchWin (terlihat seperti crash; sebelumnya tercatat sebagai `Escape key pressed -> Closing NotchWin` tepat setelah slim bar tampil).

### 13.3 Timing

| Transisi | Durasi | Easing |
|:---|:---:|:---|
| Charge hover | 3000 ms | `QuadraticEase EaseInOut` |
| Batal charge | 180 ms | `CubicEase EaseOut` |
| Morph garis → notch (pill grow) | 360 ms, mulai 80 ms | `QuarticEase EaseInOut` |
| Bar fade-out (handoff ke pill) | 80 ms | `CubicEase EaseOut` |
| Total morph masuk | 440 ms | — |
| Post-morph hover check | 460 ms | — |
| Pill → bar: pill + konten menyusut sebagai satu bentuk | 360 ms | `QuarticEase EaseInOut` |
| Pill → bar: crossfade (bar fade-in + pill fade-out) | 80 ms | `CubicEase EaseOut` |
| Total morph balik | 440 ms | — |
| Tunda morph balik saat panel baru collapse | 300 ms | — |
| Guard peek saat morph masuk | 700 ms sejak peek dibuka | — |
| Kembali ke slim setelah collapse | 340 ms setelah collapse bila panel sempat Expanded; delay collapse biasa bila peek belum diklik | — |

## 14. Optimasi v0.5.0 - Performa Hover & Morph

Timing dan easing tidak berubah; optimasi hanya menghilangkan kerja layout berulang dan memperkuat guard transisi.

- `UpdateLayout()` paksa di kedua morph dihapus. Ukuran badan notch diambil dari `ContentHost.Width/Height` yang sudah disinkronkan oleh `UpdateNotchGeometry`, dengan fallback ke `NotchWidth/Height` lalu `ActualWidth/Height`.
- Rect hover slim bar di-cache (`_slimBarHoverRectValid`) dan hanya dihitung ulang saat geometri bar, skala notch, atau posisi window berubah. Ini menghindari dua panggilan `PointToScreen` dan lookup `PresentationSource` setiap 100 ms.
- Hover memakai `Environment.TickCount64` (monotonic) agar perubahan jam sistem tidak memengaruhi durasi 3 detik. Guard peek di `NotchViewModel` juga memakai sumber waktu yang sama.
- Ditambahkan grace 240 ms saat kursor keluar area hover sebelum charge 3 detik dibatalkan, sehingga gerakan kursor kecil tidak memulai hitungan dari nol.
- Timer hover dan timer delay morph di-subscribe sekali di constructor; `Start()`/`Stop()` tidak lagi melepas dan memasang handler berulang.
- Morph masuk memakai `_slimBarMorphToNotchActive` dan generation counter. Event `VisibilityChanged` lain tidak bisa memulai `AnimateVisibility()` di tengah morph, completion dari morph lama tidak bisa menimpa state morph baru, dan reset collapse-bounce serta animasi hover-stretch ditahan agar skala pill tidak bertabrakan.
- `CancelSlimBarMorphToNotch()` merapikan bar host, skala hover, dan cache saat morph masuk dibatalkan oleh perubahan mode.
- Fallback `ScheduleNotchInteractionRestore()` tetap memulihkan skala dan hit-test bila completion grow terlewat.
- Konstanta fade morph yang tidak terpakai (`SlimBarMorphPillFadeMs`, `SlimBarMorphBarFadeMs`) dihapus.