using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using NotchWin.Models;
using NotchWin.Services;
using NotchWin.ViewModels;
using NotchWin.Widgets;
using Serilog;

namespace NotchWin.Views;

/// <summary>
/// Garis cahaya di sekeliling outline notch yang tertutup, meniru dua efek Poni:
/// fxTimerLine (sisa waktu countdown, hijau/amber/merah, berkedip saat selesai) dan
/// fxGlow (cahaya berjalan keliling selama alert tampil).
/// Tiga stroke berlapis dipakai menggantikan DropShadowEffect: notch adalah layered window
/// (AllowsTransparency=True) sehingga blur effect memaksa render-target per frame.
/// </summary>
public partial class NotchWindow
{
	private enum NotchOutlineMode
	{
		Off,
		TimerRun,
		StopwatchRun,
		Blink,
		AlertGlow
	}

	private static class NotchOutlinePalette
	{
		public static readonly SolidColorBrush TimerGreen = Create(0x30, 0xD1, 0x58);
		public static readonly SolidColorBrush TimerAmber = Create(0xFF, 0x9F, 0x2E);
		public static readonly SolidColorBrush TimerRed = Create(0xFF, 0x45, 0x3A);
		public static readonly SolidColorBrush StopwatchYellow = Create(0xFF, 0xD6, 0x0A);
		public static readonly SolidColorBrush AlertBlue = Create(0x5A, 0xC8, 0xFA);

		private static SolidColorBrush Create(byte r, byte g, byte b)
		{
			SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
			brush.Freeze();
			return brush;
		}
	}

	private const double AlertLoopMs = 1800.0;
	private const double ChargingLoopMs = 1400.0;
	private const int ChargingLoopCount = 2;
	private const double AlertMaxMs = 20000.0;
	private const double SmoothStepIgnoreThreshold = 0.005;
	private const double MinDashUnit = 0.0001;

	private double _outlineHalfLength;
	private double _outlineLastGap = -1.0;
	private NotchOutlineMode _outlineMode = NotchOutlineMode.Off;
	private SolidColorBrush? _outlineBrush;
	private double _outlineShown = -1.0;
	private double _outlineTargetFraction;
	private double _outlineLerpFrom;
	private DateTime _outlineLerpStartUtc;
	private DateTime _blinkUntilUtc;
	private DateTime _alertUntilUtc;
	private DateTime _alertLoopStartUtc;
	private double _alertLoopMs = AlertLoopMs;
	private int _alertLoopsLeft;
	private string _alertKey = string.Empty;
	private SolidColorBrush _alertBrush = NotchOutlinePalette.AlertBlue;
	private bool _isOutlineFxClosed;
	private bool _outlineSettling;
	private NotchState _outlineLastState = NotchState.Collapsed;
	private DispatcherTimer? _outlineSettleTimer;
	private DispatcherTimer? _outlineSmoothTimer;
	private DispatcherTimer? _outlineAlertTimer;

	private Path?[] OutlineLayers => new Path?[]
	{
		OutlineCoreLeftPath, OutlineCoreRightPath,
		OutlineNearLeftPath, OutlineNearRightPath,
		OutlineFarLeftPath, OutlineFarRightPath
	};

	private void RegisterOutlineFxHandlers()
	{
		if (ViewModel == null)
		{
			return;
		}

		ViewModel.TimerStopwatch.TimerFinished += (object? _, EventArgs _) =>
		{
			// Kedip merah adalah sinyal "timer selesai" (Poni). Notifikasi selesai dikirim lebih
			// dulu, jadi glow alert-nya harus diputus di sini supaya kedip tidak pernah tertimpa.
			_blinkUntilUtc = DateTime.UtcNow.AddMilliseconds(1850.0);
			_alertUntilUtc = DateTime.MinValue;
			_alertKey = string.Empty;
			StopAlertLoop();
			_outlineMode = NotchOutlineMode.Off;
			SyncNotchOutlineFx();
		};

		ViewModel.TimerStopwatch.PropertyChanged += (object? _, PropertyChangedEventArgs e) =>
		{
			switch (e.PropertyName)
			{
				case nameof(TimerStopwatchViewModel.IsTimerRunning):
				case nameof(TimerStopwatchViewModel.IsStopwatchRunning):
				case nameof(TimerStopwatchViewModel.TimerProgress):
				case nameof(TimerStopwatchViewModel.TotalTimerSeconds):
				case nameof(TimerStopwatchViewModel.StopwatchOutlineFraction):
					Dispatcher.Invoke(SyncNotchOutlineFx);
					break;
			}
		};

		ViewModel.WidgetHost.PropertyChanged += (object? _, PropertyChangedEventArgs e) =>
		{
			if (e.PropertyName == nameof(WidgetHost.IsTransientActive) || e.PropertyName == nameof(WidgetHost.CurrentWidgetType))
			{
				Dispatcher.Invoke(SyncNotchOutlineFx);
			}
		};

		ViewModel.PropertyChanged += (object? _, PropertyChangedEventArgs e) =>
		{
			switch (e.PropertyName)
			{
				case nameof(NotchViewModel.CurrentState):
				case nameof(NotchViewModel.IsNotchVisible):
				case nameof(NotchViewModel.IsSlimBarVisible):
				case nameof(NotchViewModel.IsNotchTimerGlowEnabled):
				case nameof(NotchViewModel.IsNotchAlertGlowEnabled):
					Dispatcher.Invoke(SyncNotchOutlineFx);
					break;
			}
		};
	}

	/// <summary>
	/// Dipanggil dari UpdateNotchGeometry() setiap bentuk notch berubah, jadi garis selalu
	/// berimpit dengan stroke notch yang sedang digambar.
	/// </summary>
	private void UpdateOutlineFxGeometry(double bodyWidth, double height, double earRadius, double bottomRadius)
	{
		if (_isOutlineFxClosed || OutlineCoreLeftPath == null)
		{
			return;
		}

		Geometry left = BuildOutlineHalf(leftSide: true, bodyWidth, height, earRadius, bottomRadius);
		Geometry right = BuildOutlineHalf(leftSide: false, bodyWidth, height, earRadius, bottomRadius);
		_outlineHalfLength = GetOutlineHalfLength(bodyWidth, height, earRadius, bottomRadius);

		foreach (Path? layer in OutlineLayers)
		{
			if (layer == null)
			{
				continue;
			}

			bool isLeft = ReferenceEquals(layer, OutlineCoreLeftPath)
				|| ReferenceEquals(layer, OutlineNearLeftPath)
				|| ReferenceEquals(layer, OutlineFarLeftPath);
			layer.Data = isLeft ? left : right;
		}

		_outlineShown = -1.0;
		_outlineLastGap = -1.0;
		ClearOutlineSettling();
		SyncNotchOutlineFx();
	}

	private void ArmOutlineSettleFallback()
	{
		if (_outlineSettleTimer == null)
		{
			_outlineSettleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(520.0) };
			_outlineSettleTimer.Tick += (object? _, EventArgs _) =>
			{
				_outlineSettleTimer!.Stop();
				ClearOutlineSettling();
				SyncNotchOutlineFx();
			};
		}
		_outlineSettleTimer.Stop();
		_outlineSettleTimer.Start();
	}

	private void ClearOutlineSettling()
	{
		_outlineSettling = false;
		_outlineSettleTimer?.Stop();
	}

	/// <summary>
	/// Satu setengah outline, dimulai dari tengah bawah (tempat kedua sisi bertemu) dan berakhir
	/// di sudut luar atas. Dash yang dihitung dari awal path karenanya selalu menempel di tengah
	/// bawah. Segmentenya kebalikan urutan BuildStrokeGeometry() dengan sweep yang dibalik.
	/// </summary>
	private static Geometry BuildOutlineHalf(bool leftSide, double bodyWidth, double height, double earRadius, double bottomRadius)
	{
		StreamGeometry half = new StreamGeometry();
		using (StreamGeometryContext ctx = half.Open())
		{
			ctx.BeginFigure(new Point(earRadius + bodyWidth / 2.0, height), isFilled: false, isClosed: false);
			if (leftSide)
			{
				ctx.LineTo(new Point(earRadius + bottomRadius, height), isStroked: true, isSmoothJoin: true);
				ctx.ArcTo(new Point(earRadius, height - bottomRadius), new Size(bottomRadius, bottomRadius), 0.0,
					isLargeArc: false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);
				ctx.LineTo(new Point(earRadius, earRadius), isStroked: true, isSmoothJoin: true);
				ctx.ArcTo(new Point(0.0, 0.0), new Size(earRadius, earRadius), 0.0,
					isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: true);
			}
			else
			{
				ctx.LineTo(new Point(earRadius + bodyWidth - bottomRadius, height), isStroked: true, isSmoothJoin: true);
				ctx.ArcTo(new Point(earRadius + bodyWidth, height - bottomRadius), new Size(bottomRadius, bottomRadius), 0.0,
					isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: true);
				ctx.LineTo(new Point(earRadius + bodyWidth, earRadius), isStroked: true, isSmoothJoin: true);
				ctx.ArcTo(new Point(bodyWidth + 2.0 * earRadius, 0.0), new Size(earRadius, earRadius), 0.0,
					isLargeArc: false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);
			}
		}
		half.Freeze();
		return half;
	}

	/// <summary>
	/// Panjang satu setengah outline, dihitung analitik: garis bawah + busur sudut bawah +
	/// sisi tegak + busur ear. Outline simetris terhadap tengah bawah jadi satu angka cukup.
	/// </summary>
	private static double GetOutlineHalfLength(double bodyWidth, double height, double earRadius, double bottomRadius)
	{
		double ear = Math.Clamp(earRadius, 0.0, Math.Max(0.0, height));
		double bottom = Math.Clamp(bottomRadius, 0.0, Math.Max(0.0, height - ear));
		double bottomRun = Math.Max(0.0, bodyWidth / 2.0 - bottom);
		double side = Math.Max(0.0, height - bottom - ear);
		return bottomRun + Math.PI / 2.0 * bottom + side + Math.PI / 2.0 * ear;
	}

	private void SyncNotchOutlineFx()
	{
		NotchState state = ViewModel?.CurrentState ?? NotchState.Collapsed;
		if (state != _outlineLastState)
		{
			_outlineLastState = state;
			// Bentuk notch masih dianimasikan. Geometri baru benar setelah Completed memanggil
			// UpdateNotchGeometry(), jadi tahan dulu supaya garis tidak menempel di ukuran lama.
			_outlineSettling = true;
			ArmOutlineSettleFallback();
		}

		if (_outlineSettling || !CanShowNotchOutlineFx())
		{
			TurnOutlineFxOff();
			return;
		}

		NotchViewModel? vm = ViewModel;
		if (vm == null)
		{
			TurnOutlineFxOff();
			return;
		}

		TimerStopwatchViewModel ts = vm.TimerStopwatch;
		bool alertGlowOn = vm.IsNotchAlertGlowEnabled && DateTime.UtcNow < _alertUntilUtc;

		NotchOutlineMode mode;
		if (alertGlowOn)
		{
			mode = NotchOutlineMode.AlertGlow;
		}
		else if (DateTime.UtcNow < _blinkUntilUtc && !ts.IsTimerRunning)
		{
			mode = NotchOutlineMode.Blink;
		}
		else if (vm.IsNotchTimerGlowEnabled && ts.IsTimerRunning && ts.TotalTimerSeconds > 0)
		{
			mode = ts.TimerProgress < 0.004 ? NotchOutlineMode.Off : NotchOutlineMode.TimerRun;
		}
		else if (vm.IsNotchTimerGlowEnabled && ts.IsStopwatchRunning)
		{
			mode = ts.StopwatchOutlineFraction < 0.004 ? NotchOutlineMode.Off : NotchOutlineMode.StopwatchRun;
		}
		else
		{
			mode = NotchOutlineMode.Off;
		}

		if (mode == NotchOutlineMode.Off)
		{
			TurnOutlineFxOff();
			return;
		}

		bool modeChanged = mode != _outlineMode;
		if (modeChanged && mode != NotchOutlineMode.Off)
		{
			Log.Debug("NotchOutlineFx: {From} -> {To}", _outlineMode, mode);
		}
		_outlineMode = mode;

		switch (mode)
		{
			case NotchOutlineMode.AlertGlow:
				StopOutlineSmoothing();
				SetOutlineBrush(_alertBrush);
				SetOutlineFxVisible(true);
				if (modeChanged)
				{
					StartAlertLoop();
				}
				break;

			case NotchOutlineMode.Blink:
				StopAlertLoop();
				StopOutlineSmoothing();
				SetOutlineBrush(NotchOutlinePalette.TimerRed);
				SetOutlineFxVisible(true);
				ForceOutlineTrace(1.0, 0.0);
				if (modeChanged)
				{
					StartOutlineBlink();
				}
				break;

			case NotchOutlineMode.StopwatchRun:
				double swFraction = Math.Clamp(ts.StopwatchOutlineFraction, 0.0, 1.0);
				StopAlertLoop();
				SetOutlineFxVisible(true);
				SetOutlineBrush(NotchOutlinePalette.StopwatchYellow);
				UpdateOutlineFraction(swFraction, 1.0 / 60.0, modeChanged);
				break;

			default:
				StopAlertLoop();
				double fraction = Math.Clamp(ts.TimerProgress, 0.0, 1.0);
				SetOutlineFxVisible(true);
				SetOutlineBrush(fraction > 0.5 ? NotchOutlinePalette.TimerGreen
					: fraction > 0.25 ? NotchOutlinePalette.TimerAmber
					: NotchOutlinePalette.TimerRed);
				UpdateOutlineFraction(fraction, 1.0 / Math.Max(1, ts.TotalTimerSeconds), modeChanged);
				break;
		}
	}

	/// <summary>
	/// Fraksi ditulis langsung; lerp 80 ms hanya dipakai bila langkah per detik cukup besar untuk
	/// terlihat (timer pendek) — sama seperti Poni mematikan transisi untuk timer panjang.
	/// </summary>
	private void UpdateOutlineFraction(double fraction, double stepPerTick, bool forceSnap)
	{
		_outlineTargetFraction = fraction;
		if (forceSnap || stepPerTick < SmoothStepIgnoreThreshold)
		{
			StopOutlineSmoothing();
			ApplyOutlineTrace(fraction, 0.0);
		}
		else if (_outlineSmoothTimer == null)
		{
			_outlineLerpFrom = _outlineShown < 0.0 ? fraction : _outlineShown;
			_outlineLerpStartUtc = DateTime.UtcNow;
			_outlineSmoothTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80.0) };
			_outlineSmoothTimer.Tick += (object? _, EventArgs _) =>
			{
				double elapsed = (DateTime.UtcNow - _outlineLerpStartUtc).TotalMilliseconds;
				if (elapsed >= 1000.0)
				{
					ApplyOutlineTrace(_outlineTargetFraction, 0.0);
					StopOutlineSmoothing();
					return;
				}
				ApplyOutlineTrace(_outlineLerpFrom + (_outlineTargetFraction - _outlineLerpFrom) * (elapsed / 1000.0), 0.0);
			};
			_outlineSmoothTimer.Start();
		}
	}

	private void ForceOutlineTrace(double dashFraction, double gapFraction)
	{
		_outlineShown = -1.0;
		_outlineLastGap = -1.0;
		ApplyOutlineTrace(dashFraction, gapFraction);
	}

	/// <summary>
	/// Dash pattern dan offset sama-sama dalam kelipatan StrokeThickness (konvensi WPF, sudah
	/// diverifikasi lewat probe render), sehingga tiap layer butuh konversi sendiri.
	/// Array [dash, 2*panjang] membuat hanya satu segmen terang; offset menggeser awalnya
	/// ke arah tepi luar — persis stroke-dashoffset Poni.
	/// </summary>
	private void ApplyOutlineTrace(double dashFraction, double offsetFraction)
	{
		if (_outlineHalfLength <= 1.0)
		{
			return;
		}

		dashFraction = Math.Clamp(dashFraction, 0.0, 1.0);
		offsetFraction = Math.Clamp(offsetFraction, 0.0, 1.0);

		if (Math.Abs(_outlineShown - dashFraction) < 0.0005 && Math.Abs(_outlineLastGap - offsetFraction) < 0.0005)
		{
			return;
		}

		_outlineShown = dashFraction;
		_outlineLastGap = offsetFraction;

		foreach (Path? layer in OutlineLayers)
		{
			if (layer == null)
			{
				continue;
			}

			double units = _outlineHalfLength / Math.Max(0.1, layer.StrokeThickness);
			DoubleCollection dashes = new DoubleCollection
			{
				Math.Max(MinDashUnit, dashFraction * units),
				2.0 * units
			};
			dashes.Freeze();
			layer.StrokeDashArray = dashes;
			layer.StrokeDashOffset = offsetFraction * units;
		}
	}

	/// <summary>Satu ronde alert: cahaya memanjang dari tengah, lalu ekornya habis ke tepi luar.</summary>
	private void AdvanceAlertLoop()
	{
		double elapsed = (DateTime.UtcNow - _alertLoopStartUtc).TotalMilliseconds;
		if (elapsed >= _alertLoopMs)
		{
			_alertLoopsLeft--;
			if (_alertLoopsLeft <= 0 || DateTime.UtcNow >= _alertUntilUtc)
			{
				StopAlertLoop();
				_outlineMode = NotchOutlineMode.Off;
				SyncNotchOutlineFx();
				return;
			}
			_alertLoopStartUtc = DateTime.UtcNow;
			elapsed = 0.0;
		}

		double p = Math.Clamp(elapsed / _alertLoopMs, 0.0, 1.0);
		double dash;
		double gap;
		double opacity;
		if (p <= 0.45)
		{
			dash = p / 0.45;
			gap = 0.0;
			opacity = p < 0.08 ? p / 0.08 : 1.0;
		}
		else if (p <= 0.8)
		{
			dash = 1.0;
			gap = (p - 0.45) / 0.35;
			opacity = 1.0;
		}
		else
		{
			dash = 1.0;
			gap = 1.0;
			opacity = 1.0 - (p - 0.8) / 0.2;
		}

		if (OutlineFxLayer != null)
		{
			OutlineFxLayer.BeginAnimation(UIElement.OpacityProperty, null);
			OutlineFxLayer.Opacity = opacity;
		}

		ForceOutlineTrace(dash, gap);
	}

	private void StartAlertLoop()
	{
		_alertLoopStartUtc = DateTime.UtcNow;
		StopOutlineSmoothing();
		if (_outlineAlertTimer == null)
		{
			_outlineAlertTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40.0) };
			_outlineAlertTimer.Tick += (object? _, EventArgs _) => AdvanceAlertLoop();
		}
		_outlineAlertTimer.Start();
	}

	private void StopAlertLoop()
	{
		_outlineAlertTimer?.Stop();
	}

	/// <summary>
	/// Dipanggil setiap alert baru muncul (WidgetHost.TransientTriggered). Warna ditentukan dari
	/// jenis widget/ikon, dan putarannya dibatasi 20 s seperti Poni walau alert-nya persisten.
	/// </summary>
	private void StartOutlineAlertGlow()
	{
		if (_isOutlineFxClosed || ViewModel == null || !ViewModel.IsNotchAlertGlowEnabled)
		{
			return;
		}

		bool isBattery = ViewModel.WidgetHost.CurrentWidgetType == WidgetType.Battery;
		string icon = ViewModel.Notification?.Icon ?? string.Empty;
		SolidColorBrush brush = NotchOutlinePalette.AlertBlue;
		double loopMs = AlertLoopMs;
		int loops = int.MaxValue;

		// Selesai Timer bukan jenis glow di Poni; biarkan berkedip merah dulu, jangan ditimpa alert.
		if (DateTime.UtcNow < _blinkUntilUtc && MatchesAny(icon, "⏱"))
		{
			return;
		}

		if (isBattery)
		{
			brush = NotchOutlinePalette.TimerGreen;
			loopMs = ChargingLoopMs;
			loops = ChargingLoopCount;
		}
		else if (MatchesAny(icon, "🕌", "⏱", "⏰", "📝"))
		{
			brush = NotchOutlinePalette.TimerAmber;
		}
		else if (MatchesAny(icon, "📱"))
		{
			brush = NotchOutlinePalette.TimerRed;
		}

		string key = isBattery
			? "battery"
			: icon + (ViewModel.Notification?.Title ?? string.Empty);

		DateTime now = DateTime.UtcNow;
		if (key != _alertKey)
		{
			_alertKey = key;
			_alertBrush = brush;
			_alertLoopMs = loopMs;
			_alertLoopsLeft = loops;
			_outlineMode = NotchOutlineMode.Off;
		}
		_alertUntilUtc = now.AddMilliseconds(AlertMaxMs);
		SyncNotchOutlineFx();
	}

	private static bool MatchesAny(string text, params string[] candidates)
	{
		foreach (string candidate in candidates)
		{
			if (text.IndexOf(candidate, StringComparison.Ordinal) >= 0)
			{
				return true;
			}
		}
		return false;
	}

	private void StartOutlineBlink()
	{
		if (OutlineFxLayer == null)
		{
			return;
		}

		DoubleAnimationUsingKeyFrames blink = new DoubleAnimationUsingKeyFrames
		{
			RepeatBehavior = new RepeatBehavior(4.0),
			FillBehavior = FillBehavior.Stop
		};
		blink.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
		blink.KeyFrames.Add(new EasingDoubleKeyFrame(0.1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(225.0)))
		{
			EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
		});
		blink.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(450.0))));
		blink.Completed += (object? _, EventArgs _) =>
		{
			if (OutlineFxLayer == null)
			{
				return;
			}
			OutlineFxLayer.BeginAnimation(UIElement.OpacityProperty, null);
			OutlineFxLayer.Opacity = 1.0;
			// Jendela kedip ditutup sendiri; kalau tidak, sync berikutnya (masih di dalam 1850 ms)
			// akan melihat mode Off dan mengulang kedip dari awal.
			_blinkUntilUtc = DateTime.MinValue;
			_outlineMode = NotchOutlineMode.Off;
			SyncNotchOutlineFx();
		};

		OutlineFxLayer.BeginAnimation(UIElement.OpacityProperty, null);
		OutlineFxLayer.Opacity = 1.0;
		OutlineFxLayer.BeginAnimation(UIElement.OpacityProperty, blink);
	}

	private void StopOutlineSmoothing()
	{
		if (_outlineSmoothTimer != null)
		{
			_outlineSmoothTimer.Stop();
			_outlineSmoothTimer = null;
		}
	}

	private void SetOutlineBrush(SolidColorBrush brush)
	{
		if (ReferenceEquals(_outlineBrush, brush))
		{
			return;
		}
		_outlineBrush = brush;
		foreach (Path? layer in OutlineLayers)
		{
			if (layer != null)
			{
				layer.Stroke = brush;
			}
		}
	}

	private void SetOutlineFxVisible(bool show)
	{
		if (OutlineFxLayer == null)
		{
			return;
		}

		if (!show)
		{
			OutlineFxLayer.BeginAnimation(UIElement.OpacityProperty, null);
			OutlineFxLayer.Opacity = 0.0;
			OutlineFxLayer.Visibility = Visibility.Collapsed;
			return;
		}

		if (OutlineFxLayer.Visibility != Visibility.Visible)
		{
			OutlineFxLayer.BeginAnimation(UIElement.OpacityProperty, null);
			OutlineFxLayer.Opacity = 0.0;
			OutlineFxLayer.Visibility = Visibility.Visible;
			DoubleAnimation fadeIn = new DoubleAnimation
			{
				To = 1.0,
				Duration = TimeSpan.FromMilliseconds(300.0),
				EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
			};
			OutlineFxLayer.BeginAnimation(UIElement.OpacityProperty, fadeIn);
		}
	}

	private void TurnOutlineFxOff()
	{
		if (_outlineMode == NotchOutlineMode.Off && OutlineFxLayer != null && OutlineFxLayer.Visibility != Visibility.Visible)
		{
			return;
		}

		if (_outlineMode != NotchOutlineMode.Off)
		{
			Log.Debug("NotchOutlineFx: {From} -> Off", _outlineMode);
		}
		_outlineMode = NotchOutlineMode.Off;
		_outlineShown = -1.0;
		_outlineLastGap = -1.0;
		StopAlertLoop();
		StopOutlineSmoothing();
		SetOutlineFxVisible(false);
	}

	private bool CanShowNotchOutlineFx()
	{
		return !_isOutlineFxClosed
			&& ViewModel != null
			&& ViewModel.IsNotchVisible
			&& !ViewModel.IsSlimBarVisible
			&& ViewModel.CurrentState == NotchState.Collapsed
			&& !_isHiding
			&& IsPillVisiblyShown()
			&& _outlineHalfLength > 1.0
			&& ThemeManager.IsAccentColorEnabled
			&& (ViewModel.IsNotchTimerGlowEnabled || ViewModel.IsNotchAlertGlowEnabled);
	}

	private void ShutdownOutlineFx()
	{
		_isOutlineFxClosed = true;
		_outlineSettling = false;
		_outlineSettleTimer?.Stop();
		StopAlertLoop();
		StopOutlineSmoothing();
		TurnOutlineFxOff();
	}
}
