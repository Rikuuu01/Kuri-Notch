# 🎨 Panduan Gaya Scrollbar — Modern Slim Scroll

> Indeks seluruh dokumentasi: [README.md](README.md)

Panduan ini adalah acuan resmi gaya scrollbar untuk seluruh UI Dynamic Notch Windows. Semua daftar/popup yang scrollable **wajib** memakai style ini agar tampilan konsisten dengan tema notch yang gelap dan ramping. Jangan memakai scrollbar default WPF (terang, tebal, berpanah) di dalam window aplikasi.

---

## 📂 Lokasi Definisi

Style didefinisikan di `FrameworkElement.Resources` masing-masing window:

| Window | File | Baris (perkiraan) |
|:---|:---|:---|
| Notch (termasuk semua popup-nya) | [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml) | ~13–137 |
| Pengaturan | [SettingsWindow.xaml](../NotchWin/Views/SettingsWindow.xaml) | ~25–110 |

> **Penting**: `SettingsWindow` memiliki **salinan** definisi yang sama (window terpisah punya `ResourceDictionary` sendiri). Setiap perubahan warna/ukuran/animasi wajib diterapkan di **kedua file** agar tidak terjadi ketidakcocokan gaya.

---

## 🧩 Anatomi Style

Tiga style saling terkait:

1. **`ModernSlimScrollBarThumb`** (`Thumb`)
   - `Border` rounded (`CornerRadius="2.5"`) berwarna `#38384A` dengan `Opacity="0.6"` dan `Margin="0,1"` (memberi jarak dari tepi track).
   - `IsMouseOver` → `Background` = `{DynamicResource AccentLightBrush}`, `Opacity` = `0.9`.
   - `IsDragging` → `Background` = `{DynamicResource AccentLightBrush}`, `Opacity` = `1.0`.
2. **`ModernSlimScrollBar`** (`ScrollBar` vertikal)
   - `Width="5"`, background transparan, `OverridesDefaultStyle="True"`.
   - Tombol panah `Track.DecreaseRepeatButton` / `IncreaseRepeatButton` memakai `ScrollBar.LineUpCommand` / `LineDownCommand` dengan `Opacity="0" Height="0" Width="0"` → tersembunyi total.
3. **`ModernSlimScrollViewer`** (`ScrollViewer`)
   - Template `Grid` dua kolom: `ScrollContentPresenter` + `PART_VerticalScrollBar`.
   - `ScrollBar` memakai `Style="{StaticResource ModernSlimScrollBar}"`, `Margin="2,0,0,0"`, `Value/Maximum/ViewportSize` di-*template-bind* ke `VerticalOffset`/`ScrollableHeight`/`ViewportHeight`.
   - `Visibility` mengambil `ComputedVerticalScrollBarVisibility` → bar hanya tampil saat konten overflow.

Kode lengkap (versi `NotchWindow.xaml`, format dirapatkan untuk keterbacaan):

```xml
<Style x:Key="ModernSlimScrollBarThumb" TargetType="{x:Type Thumb}">
  <Setter Property="OverridesDefaultStyle" Value="True" />
  <Setter Property="IsTabStop" Value="False" />
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="{x:Type Thumb}">
        <Border Name="thumbBorder" Background="#38384A" CornerRadius="2.5" Opacity="0.6" Margin="0,1" />
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter TargetName="thumbBorder" Property="Background" Value="{DynamicResource AccentLightBrush}" />
            <Setter TargetName="thumbBorder" Property="Opacity" Value="0.9" />
          </Trigger>
          <Trigger Property="IsDragging" Value="True">
            <Setter TargetName="thumbBorder" Property="Background" Value="{DynamicResource AccentLightBrush}" />
            <Setter TargetName="thumbBorder" Property="Opacity" Value="1.0" />
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>

<Style x:Key="ModernSlimScrollBar" TargetType="{x:Type ScrollBar}">
  <Setter Property="OverridesDefaultStyle" Value="True" />
  <Setter Property="Width" Value="5" />
  <Setter Property="Background" Value="Transparent" />
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="{x:Type ScrollBar}">
        <Grid Background="Transparent" SnapsToDevicePixels="True">
          <Track Name="PART_Track" IsDirectionReversed="True">
            <Track.DecreaseRepeatButton>
              <RepeatButton Command="{x:Static ScrollBar.LineUpCommand}" Opacity="0" Height="0" Width="0" Focusable="False" />
            </Track.DecreaseRepeatButton>
            <Track.Thumb>
              <Thumb Style="{StaticResource ModernSlimScrollBarThumb}" />
            </Track.Thumb>
            <Track.IncreaseRepeatButton>
              <RepeatButton Command="{x:Static ScrollBar.LineDownCommand}" Opacity="0" Height="0" Width="0" Focusable="False" />
            </Track.IncreaseRepeatButton>
          </Track>
        </Grid>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>

<Style x:Key="ModernSlimScrollViewer" TargetType="{x:Type ScrollViewer}">
  <Setter Property="OverridesDefaultStyle" Value="True" />
  <Setter Property="SnapsToDevicePixels" Value="True" />
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="{x:Type ScrollViewer}">
        <Grid Background="{TemplateBinding Background}">
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="Auto" />
          </Grid.ColumnDefinitions>
          <ScrollContentPresenter Grid.Column="0" CanContentScroll="{TemplateBinding CanContentScroll}" />
          <ScrollBar Name="PART_VerticalScrollBar" Grid.Column="1"
                     Value="{TemplateBinding VerticalOffset}"
                     Maximum="{TemplateBinding ScrollableHeight}"
                     ViewportSize="{TemplateBinding ViewportHeight}"
                     Visibility="{TemplateBinding ComputedVerticalScrollBarVisibility}"
                     Margin="2,0,0,0"
                     Style="{StaticResource ModernSlimScrollBar}" />
        </Grid>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

---

## ✅ Cara Memakai

### Daftar/popup vertikal (paling umum)

```xml
<ScrollViewer MaxHeight="180"
              Style="{StaticResource ModernSlimScrollViewer}"
              VerticalScrollBarVisibility="Auto"
              HorizontalScrollBarVisibility="Disabled">
    <ItemsControl ItemsSource="{Binding ...}" />
</ScrollViewer>
```

- Set `MaxHeight` (atau biarkan tinggi mengikuti parent ber-`Height` tetap) agar bar hanya muncul saat konten benar-benar overflow.
- Jangan menulis `Width` pada `ScrollViewer`; lebar bar sudah tetap 5 px + margin 2 px.

### Daftar horizontal (Clipboard/Shelf/Dock)

```xml
<ScrollViewer Style="{StaticResource ModernSlimScrollViewer}"
              HorizontalScrollBarVisibility="Auto"
              VerticalScrollBarVisibility="Disabled">
    <!-- konten horizontal -->
</ScrollViewer>
```

Template hanya menggambar bar **vertikal**; untuk daftar horizontal, scroll ditangani oleh konten/behavior yang sudah ada (sama seperti implementasi Clipboard, Shelf, dan Shortcut Dock saat ini).

---

## 📏 Aturan Wajib

1. **Jangan pakai scrollbar default WPF** di window notch, popup, maupun Settings. Selalu pasang `Style="{StaticResource ModernSlimScrollViewer}"`.
2. Selalu set axis yang tidak dipakai ke `"Disabled"` secara eksplisit.
3. Track dan tombol panah harus tetap transparan/tersembunyi; jangan menambahkan background track solid agar panel gelap tidak terlihat "dipotong" garis terang.
4. Warna hover/drag thumb wajib memakai `{DynamicResource AccentLightBrush}` supaya otomatis mengikuti tema aksen user.
5. Thumb memakai `Margin="0,1"` agar tidak menempel ke sudut border panel.
6. Jika mengubah nilai (`Width`, `CornerRadius`, warna dasar, opacity), perbarui **kedua salinan** (NotchWindow + SettingsWindow) **dan** tabel di dokumen ini.

---

## 🗂️ Daftar Penggunaan Saat Ini

| Area | File | Catatan |
|:---|:---|:---|
| Popup versi lirik (daftar kandidat) | [NotchWindow.xaml](../NotchWin/Views/NotchWindow.xaml) ~L3167 | `MaxHeight="180"`, vertikal; ditambahkan 2026-10-28 |
| Riwayat Clipboard — gambar | NotchWindow.xaml ~L6195 | Horizontal |
| Riwayat Clipboard — teks | NotchWindow.xaml ~L6367 | Vertikal |
| Lap Stopwatch | NotchWindow.xaml ~L7089 | Vertikal |
| Shelf | NotchWindow.xaml ~L7245 | Horizontal |
| Shortcut Dock | NotchWindow.xaml ~L7457 | Horizontal |
| Settings — konten utama | [SettingsWindow.xaml](../NotchWin/Views/SettingsWindow.xaml) ~L616 | Vertikal |
| Settings — sub-daftar | SettingsWindow.xaml ~L303, ~L1045, ~L1170 | Vertikal |

Perbarui tabel ini setiap kali memakai style di area baru.

---

## 📝 Riwayat Perubahan
- **2026-10-28** — Popup "Versi Lirik" beralih dari scrollbar default WPF ke `ModernSlimScrollViewer`; dokumen panduan ini dibuat.
