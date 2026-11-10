using NotchWin.Animations;
using NotchWin.Widgets;

using System.Windows.Documents;

using System;







using System.CodeDom.Compiler;







using System.Collections.Specialized;







using System.ComponentModel;







using System.Diagnostics;







using System.IO;







using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;







using System.Windows;







using System.Windows.Controls;







using System.Windows.Input;







using System.Windows.Interop;







using System.Windows.Markup;







using System.Windows.Media;







using System.Windows.Media.Animation;







using System.Windows.Shapes;







using System.Windows.Threading;







using NotchWin.Interop;







using NotchWin.Models;







using NotchWin.Services;







using NotchWin.ViewModels;







using Serilog;















namespace NotchWin.Views;















public partial class NotchWindow : Window







{







	public const double EarRadius = 12.0;















	private NotchState _lastAnimatedState;
	private bool _geometryUpdateQueued;
	private double _lastGeometryWidth = double.NaN;
	private double _lastGeometryHeight = double.NaN;
	private double _lastGeometryRadius = double.NaN;
	private double _lastAnimatedTargetWidth = double.NaN;
	private double _lastAnimatedTargetHeight = double.NaN;
	private double _lastAnimatedTargetRadius = double.NaN;















	private bool _isHiding;
	private bool _isSlimBarMode;
	private readonly DispatcherTimer _slimBarHoverTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
	private long _slimBarHoverStartedTick;
	private long _slimBarHoverLastInsideTick;
	private bool _slimBarHoverCharging;
	private bool _slimBarHoverTriggered;
	private const int SlimBarHoverOpenMs = 3000;
	private const int SlimBarHoverExitGraceMs = 240;
	private const int SlimBarMorphGrowMs = 360;
	private const int SlimBarMorphTotalMs = 440;

	private const int SlimBarMorphFromNotchShrinkMs = 360;
	private const int SlimBarMorphFromNotchHandoffMs = 80;
	private const int SlimBarMorphFromNotchTotalMs = 440;
	private const int SlimBarMorphFromNotchCollapseWaitMs = 300;
	private readonly DispatcherTimer _notchToSlimMorphDelayTimer = new() { Interval = TimeSpan.FromMilliseconds(SlimBarMorphFromNotchCollapseWaitMs) };
	private bool _notchToSlimMorphActive;
	private int _notchInteractionRestoreGeneration;
	private bool _lastSlimBarPeekActive;
	private bool _slimBarMorphToNotchActive;
	private int _slimBarMorphToNotchGeneration;
	private Rect _slimBarHoverRect;
	private bool _slimBarHoverRectValid;

	private const int TopmostKeeperIntervalMs = 5000;
	private const int TopmostAssertThrottleMs = 500;
	private readonly DispatcherTimer _topmostKeeperTimer = new() { Interval = TimeSpan.FromMilliseconds(TopmostKeeperIntervalMs) };

	// Overlay HWND hanya sebesar konten notch agar permukaan layered window tidak
	// selalu 1400x880 saat collapsed (software rasterization ikut mengecil).
	private const double OverlayCollapsedHeight = 180.0;
	private const double OverlayExpandedHeight = 880.0;
	private const double OverlayMaxWidth = 1400.0;
	private DispatcherTimer? _overlayWindowSettleTimer;
	private DispatcherTimer? _shelfDropEffectTimer;
	private IntPtr _notchHwnd;
	// _lyricsVersionPopupHwnd removed
	private IntPtr _devicePickerPopupHwnd;
	private bool _privacyOverlayActive;
	private IntPtr _foregroundWinEventHook;
	private NativeMethods.WinEventDelegate? _foregroundWinEventDelegate;
	private bool _topmostKeeperActive;
	private bool _topmostAssertQueued;
	private long _lastTopmostAssertTick;















	private ITrayService? _trayService;















	private SettingsWindow? _settingsWindow;















	private nint _displayPowerNotifyHandle = IntPtr.Zero;















	public static readonly DependencyProperty NotchWidthProperty = DependencyProperty.Register("NotchWidth", typeof(double), typeof(NotchWindow), new PropertyMetadata(260.0, OnGeometryChanged));















	public static readonly DependencyProperty NotchHeightProperty = DependencyProperty.Register("NotchHeight", typeof(double), typeof(NotchWindow), new PropertyMetadata(32.0, OnGeometryChanged));















	public static readonly DependencyProperty NotchBottomRadiusProperty = DependencyProperty.Register("NotchBottomRadius", typeof(double), typeof(NotchWindow), new PropertyMetadata(16.0, OnGeometryChanged));















	private ScaleTransform? _pillLayoutScaleTransform;















	private string? _lastPositionedMonitor;















	private int _displayedSlideIndex;















	private DateTime _lastSlideWheelTime = DateTime.MinValue;















	private int _accumulatedWheelDelta;















	private int _slideTransitionCounter;















	private bool _isWeatherAnimRunning;















	private int _lastAnimatedLyricIndex = -999;















	private bool _isLyricSlotA = true;
	private bool _collapsedLyricsWidthRetryQueued;
	// Lebar minimum area lirik di pill; baris yang lebih panjang membuat notch ikut melebar.
	private const double MinCollapsedLyricTextWidth = 100.0;















	private bool _isTimerSubNotchShowing;















	private bool _isStopwatchSubNotchShowing;



	private bool _isNotificationSubNotchShowing;
	private bool _isDownloadSubNotchShowing;















	private bool _isCardFlipping;















	private bool _wasAutoExpandedByDrag;















	private bool _isInternalDragOut;

	private bool _shelfDropInsideNotch;















	private bool _isContextMenuOpen;















	private bool _wasCursorInsideNotch;















	private Point _shelfDragStartPoint;















	private Button[]? _navTabs;















	private TextBlock[]? _navLabels;















	private Shape[]? _navIcons;
	private bool _navPillSyncing;
	private bool _navPillFollowQueued;
	private DoubleAnimation? _navPillAnimX;
	private DoubleAnimation? _navPillAnimWidth;
	private FrameworkElement[]? _navContents;
	private Ellipse[]? _navDots;
	private bool _navDockAnimating;
	private int _navPillTabIndex = -1;
	private bool _navDockExpanded;
	private DispatcherTimer? _navDockCloseTimer;
	private static readonly Duration NavDockFadeInDuration = new Duration(TimeSpan.FromMilliseconds(150.0));
	private static readonly Duration NavDockFadeOutDuration = new Duration(TimeSpan.FromMilliseconds(140.0));
	private static readonly Duration NavDockGrowDuration = new Duration(TimeSpan.FromMilliseconds(220.0));
	private static readonly Duration NavDockShrinkDuration = new Duration(TimeSpan.FromMilliseconds(180.0));
	private static readonly TimeSpan NavDockCloseGrace = TimeSpan.FromMilliseconds(1000.0);
	private static readonly CubicEase NavDockEase = CreateNavDockEase();
	private static readonly Duration NavPillGlideDuration = new Duration(TimeSpan.FromMilliseconds(320.0));
	private static readonly QuinticEase NavPillGlideEase = CreateNavPillGlideEase();
	private static readonly SolidColorBrush NavIconIdleBrush = CreateFrozenBrush(Color.FromRgb(156, 163, 175));
	private static readonly SolidColorBrush NavIconWaterBrush = CreateFrozenBrush(Color.FromRgb(56, 189, 248));
	private static readonly SolidColorBrush NavDotIdleBrush = CreateFrozenBrush(Color.FromRgb(113, 113, 122));
	private static readonly SolidColorBrush NavTextActiveBrush = CreateFrozenBrush(Color.FromRgb(255, 255, 255));
	private static readonly SolidColorBrush NavTextIdleBrush = CreateFrozenBrush(Color.FromRgb(156, 163, 175));
	private const double NavCompactButtonWidth = 20.0;






























	private static readonly SolidColorBrush ShelfNormalBg = new SolidColorBrush(Color.FromRgb(19, 19, 32));















	private static readonly SolidColorBrush ShelfHoverBg = new SolidColorBrush(Color.FromRgb(24, 37, 58));















	private static readonly SolidColorBrush ShelfNormalBorder = new SolidColorBrush(Color.FromRgb(37, 37, 56));















	private static readonly SolidColorBrush ShelfHoverBorder = new SolidColorBrush(Color.FromRgb(56, 189, 248));















	private static readonly SolidColorBrush DockNormalBg = new SolidColorBrush(Color.FromRgb(19, 19, 32));















	private static readonly SolidColorBrush DockHoverBg = new SolidColorBrush(Color.FromRgb(36, 26, 56));

	private static readonly SolidColorBrush ConvertNormalBg = new SolidColorBrush(Color.FromRgb(19, 19, 32));
	private static readonly SolidColorBrush ConvertHoverBg = new SolidColorBrush(Color.FromRgb(20, 44, 37));
	private static readonly SolidColorBrush ConvertNormalBorder = new SolidColorBrush(Color.FromRgb(37, 37, 56));
	private static readonly SolidColorBrush ConvertHoverBorder = new SolidColorBrush(Color.FromRgb(52, 211, 153));

	private static LocalizationService L10n => LocalizationService.Instance;















	private static readonly SolidColorBrush DockNormalBorder = new SolidColorBrush(Color.FromRgb(37, 37, 56));















	private static readonly SolidColorBrush DockHoverBorder = new SolidColorBrush(Color.FromRgb(167, 139, 250));























	public NotchViewModel ViewModel { get; }















	public double NotchWidth







	{







		get







		{







			return (double)GetValue(NotchWidthProperty);







		}







		set







		{







			SetValue(NotchWidthProperty, value);







		}







	}















	public double NotchHeight







	{







		get







		{







			return (double)GetValue(NotchHeightProperty);







		}







		set







		{







			SetValue(NotchHeightProperty, value);







		}







	}















	public double NotchBottomRadius







	{







		get







		{







			return (double)GetValue(NotchBottomRadiusProperty);







		}







		set







		{







			SetValue(NotchBottomRadiusProperty, value);







		}







	}















	private static void OnGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)







	{







		if (d is NotchWindow notchWindow)







		{







			notchWindow.QueueNotchGeometryUpdate();







		}







	}















	public NotchWindow()







	{







		InitializeComponent();
		_slimBarHoverTimer.Tick += SlimBarHoverTimer_Tick;
		_notchToSlimMorphDelayTimer.Tick += NotchToSlimMorphDelayTimer_Tick;







		ViewModel = new NotchViewModel();

		ViewModel.Shelf.Items.CollectionChanged += OnShelfItemsCollectionChanged;

		if (ShelfItemsControl != null)
		{
			ShelfItemsControl.IsVisibleChanged += (_, _) =>
			{
				if (ShelfItemsControl.IsVisible)
				{
					ScheduleShelfContainerSync();
				}
			};
		}







		DataContext = ViewModel;
		SyncNavbarModuleConfiguration();
		PrepareStartupEntrance();







		ViewModel.RequestCloseEvent += (object? _, EventArgs _) =>







		{







			Log.Information("Close requested from ViewModel");







			Close();







		};







		ViewModel.StateAnimationRequested += (object? _, EventArgs _) =>







		{







			AnimateToCurrentState();

		};



		ViewModel.WidgetHost.ActiveWidgetChanged += (object? _, WidgetType widgetType) =>

		{

			Dispatcher.Invoke(() =>

			{

				bool showInExpanded = widgetType == WidgetType.Notification && ViewModel.CurrentState == NotchState.Expanded;

				AnimateNotificationSubNotch(showInExpanded);

			});

		};



		ViewModel.WidgetHost.TransientTriggered += (object? _, WidgetType widgetType) =>

		{

			Dispatcher.Invoke(() =>

			{

				if (widgetType == WidgetType.Notification && ViewModel.CurrentState == NotchState.Expanded)

				{

					AnimateNotificationSubNotch(true, forceRestart: true);

				}

				StartOutlineAlertGlow();

			});

		};







		ViewModel.VisibilityChanged += (object? _, EventArgs _) =>







		{







			Dispatcher.Invoke(() =>







			{







				UpdateWindowVisibility();
				UpdateDownloadSubNotchVisibility();
				SyncNotchOutlineFx();







			});







		};







		ViewModel.PositionUpdateRequested += (object? _, EventArgs _) =>







		{







			Dispatcher.Invoke(() =>







			{







				ApplyNotchScale(ViewModel.NotchScale);







				PositionOnMonitor();







			});







		};







		ThemeManager.ThemeTransitionRequested += OnThemeTransitionRequested;







		ViewModel.SlideChanged += OnSlideChanged;







		ViewModel.PropertyChanged += OnViewModelPropertyChanged;
		ViewModel.Download.PropertyChanged += OnDownloadPropertyChanged;
		RegisterOutlineFxHandlers();







		if (ViewModel.IsPrivacyActive)







		{







			if (PrivacyOverlayHost != null)







			{







				PrivacyOverlayHost.Visibility = Visibility.Visible;







				PrivacyOverlayHost.Opacity = 1.0;







			}







			if (ExpandedPrivacyOverlayHost != null)







			{







				ExpandedPrivacyOverlayHost.Visibility = Visibility.Visible;







				ExpandedPrivacyOverlayHost.Opacity = 1.0;







			}







		}







		_privacyOverlayActive = ViewModel.IsPrivacyActive;







		ViewModel.HoverStretchChanged += (object? _, bool isStretching) =>







		{







			Dispatcher.Invoke(() =>







			{







				AnimateHoverStretch(isStretching);







			});







		};







		ViewModel.OpenSettingsRequested += (object? _, EventArgs _) =>







		{







			Dispatcher.Invoke(ShowSettingsWindow);







		};







		ViewModel.Media.ActiveLyricChanged += (object? s, int idx) =>







		{







			Dispatcher.Invoke(() =>







			{







				if (LyricsTranslateTransform != null && idx >= 0)







				{







					double num = 36.0;







					double num2 = (double)(1 - idx) * num;

						double scrollDurationMs = 320.0;
						if (ViewModel.Media.IsCurrentLyricsUntimed)
						{
							double remainingMs = 4000.0;
							if (idx + 1 < ViewModel.Media.LyricsLines.Count)
							{
								remainingMs = (ViewModel.Media.LyricsLines[idx + 1].Time - ViewModel.Media.Position).TotalMilliseconds;
							}

							scrollDurationMs = Math.Clamp(remainingMs, 500.0, 12000.0);
						}







					if (Math.Abs(LyricsTranslateTransform.Y - num2) > 0.5)







					{







						DoubleAnimation animation = new DoubleAnimation







						{







							To = num2,







							Duration = TimeSpan.FromMilliseconds(scrollDurationMs),







							EasingFunction = new CubicEase







							{







								EasingMode = EasingMode.EaseOut







							}







						};







						LyricsTranslateTransform.BeginAnimation(TranslateTransform.YProperty, animation);







					}







				}







				if (ViewModel.Media.IsCollapsedLyricsActive && !ViewModel.Media.IsActiveLyricInterlude && idx >= 0)







				{







					string lyricText = (idx < ViewModel.Media.LyricsLines.Count) ? ViewModel.Media.LyricsLines[idx].Text : ViewModel.Media.CollapsedDisplayLyricText;







					AnimateCollapsedLyricChange(lyricText, idx);







				}







			});







		};







		ViewModel.Media.LyricsExpansionChanged += (object? s, bool isExpanded) =>







		{







			Dispatcher.Invoke(() =>







			{







				AnimateLyricsExpansion(isExpanded);







			});







		};







		ViewModel.Media.PropertyChanged += (object? s, PropertyChangedEventArgs e) =>







		{







			if (e.PropertyName == "IsCollapsedLyricsActive")







			{







				Dispatcher.Invoke(() =>







				{







					if (ViewModel.Media.IsCollapsedLyricsActive && !ViewModel.Media.IsActiveLyricInterlude)







					{







						ResetCollapsedLyricText(ViewModel.Media.CollapsedDisplayLyricText, ViewModel.Media.CurrentLyricIndex);







					}
					else
					{
						ViewModel.SetCollapsedLyricsWidthOverride(null);
					}







				});







			}







			else if (e.PropertyName == nameof(MediaViewModel.IsLyricsVersionPickerOpen))
			{
				Dispatcher.Invoke(() =>
				{
					AnimateLyricsVersionPicker(ViewModel.Media.IsLyricsVersionPickerOpen);
				});
			}
			else if (e.PropertyName == "IsActiveLyricInterlude" && ViewModel.Media.IsActiveLyricInterlude)







			{







				Dispatcher.Invoke(() =>







				{







					ResetCollapsedLyricText(string.Empty, -1);







				});







			}







		};







		ViewModel.TimerStopwatch.PropertyChanged += (object? s, PropertyChangedEventArgs e) =>







		{







			if (e.PropertyName == "IsTimerRunning")







			{







				Dispatcher.Invoke(() =>







				{







					AnimateTimerSubNotch(ViewModel.TimerStopwatch.IsTimerRunning);







				});







			}







			else if (e.PropertyName == "IsStopwatchRunning")







			{







				Dispatcher.Invoke(() =>







				{







					AnimateStopwatchSubNotch(ViewModel.TimerStopwatch.IsStopwatchRunning);







				});







			}







		};







		ApplyNotchScale(ViewModel.NotchScale, animate: false);







		NotchWidth = ViewModel.TargetWidth;







		NotchHeight = ViewModel.TargetHeight;







		NotchBottomRadius = ViewModel.TargetCornerRadius;







		UpdateNotchGeometry();







		ResetSlideViews(ViewModel?.CurrentSlideIndex ?? 0);







	}















	private void Window_Loaded(object sender, RoutedEventArgs e)







	{







		ApplyNotchScale(ViewModel.NotchScale, animate: false);







		Width = GetOverlayWindowWidth();
		Height = ViewModel.CurrentState == NotchState.Expanded ? OverlayExpandedHeight : OverlayCollapsedHeight;
		PositionOnMonitor(force: true);
		PrepareStartupEntrance();
		if (ViewModel.IsSlimBarVisible)
		{
			UpdateWindowVisibility();
		}
		if (ExpandedContentGrid.IsVisible)
		{
			Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => ExpandNavDock(animate: false));
		}
		ResetCollapsedTimerSlotStates(false);
		Services.MemoryManagementService.Instance.Initialize();







        try







        {







            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;







            if (DataContext is NotchViewModel vm)



            {



                vm.Clipboard.Initialize(hwnd);



                if (vm.Note != null)



                {



                    vm.Note.PropertyChanged += NoteViewModel_PropertyChanged;



                    LoadNoteIntoRichTextBox(vm.Note.SelectedNote?.Content ?? "");



                }



            }







        }







        catch { }







		nint handle = new WindowInteropHelper(this).Handle;







		WindowStyleHelper.ApplyNotchWindowStyles(handle);

		StartTopmostKeeper(handle);







		_trayService = new TrayService();







		_trayService.Initialize(handle);







		_trayService.OpenSettingsRequested += (object? _, EventArgs _) =>







		{







			Dispatcher.Invoke(ShowSettingsWindow);







		};







		_trayService.ToggleVisibilityRequested += (object? _, EventArgs _) =>







		{







			Dispatcher.Invoke(ViewModel.ToggleManualVisibility);







		};







		_trayService.ExitRequested += (object? _, EventArgs _) =>







		{







			Dispatcher.Invoke(Close);







		};







		HwndSource.FromHwnd(handle)?.AddHook(WndProc);







		try







		{







			_displayPowerNotifyHandle = NativeMethods.RegisterPowerSettingNotification(handle, ref NativeMethods.GUID_CONSOLE_DISPLAY_STATE, 0u);







		}







		catch (Exception exception)







		{







			Log.Warning(exception, "Could not register for power setting notifications");







		}







		NotchWidth = ViewModel.TargetWidth;







		NotchHeight = ViewModel.TargetHeight;







		NotchBottomRadius = ViewModel.TargetCornerRadius;







		UpdateNotchGeometry();







		ResetSlideViews(ViewModel.CurrentSlideIndex);



		ExpandedContentGrid.IsVisibleChanged += (s, e) =>

		{

			if (!ExpandedContentGrid.IsVisible)
			{
			}

			ResetNavDockState();

			if (ExpandedContentGrid.IsVisible && ViewModel != null)

			{

				Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>

				{

					ExpandNavDock(animate: false);

				});

			}

		};

		NavTabsPanel.SizeChanged += (s, e) =>
		{
			if (_navPillSyncing || _navDockAnimating || !e.WidthChanged)
			{
				return;
			}
			if (_navPillAnimX != null)
			{
				RetargetNavPillGlide();
				return;
			}
			QueueNavPillFollow();
		};

		ResetNavDockState();







		if (ViewModel.Media.IsCollapsedLyricsActive)







		{







			ResetCollapsedLyricText(ViewModel.Media.CollapsedDisplayLyricText, ViewModel.Media.CurrentLyricIndex);







		}







		if (ViewModel.TimerStopwatch.IsTimerRunning)







		{







			AnimateTimerSubNotch(isRunning: true);







		}







		if (ViewModel.TimerStopwatch.IsStopwatchRunning)







		{







			AnimateStopwatchSubNotch(isRunning: true);







		}

		UpdateDownloadSubNotchVisibility();







		PlayStartupEntranceAnimation();








		Log.Information("NotchWindow Loaded. Left={Left}, Top={Top}, InitialWidth={InitialWidth}", Left, Top, NotchWidth);
		FontScaleService.ApplyScaleToWindow(this, ViewModel?.FontScale ?? 1.0);








	}















	/// <summary>
	/// Menentukan tampilan window saat status visibilitas berubah:
	/// - Mode slim bar fullscreen: tetap tampilkan window, sembunyikan pill, tampilkan garis tipis.
	/// - Mode normal / sembunyi total: gunakan animasi visibilitas yang sudah ada.
	/// </summary>
	private bool _startupEntrancePlayed;
	private bool _startupGreetingPlanned;
	private bool _startupGreetingActive;
	private CancellationTokenSource? _startupGreetingCts;

	private void PrepareStartupEntrance()
	{
		if (ViewModel == null || !ViewModel.IsNotchVisible || ViewModel.IsSlimBarVisible)
		{
			return;
		}
		if (PillContainer == null || PillTranslateTransform == null || PillScaleTransform == null || ContentHost == null)
		{
			return;
		}
		PillContainer.Opacity = 0.0;
		PillTranslateTransform.Y = -28.0;
		PillScaleTransform.ScaleX = 0.9;
		PillScaleTransform.ScaleY = 0.9;
		ContentHost.Opacity = 0.0;
		_startupGreetingPlanned = CanPlayStartupGreeting();
		if (_startupGreetingPlanned)
		{
			if (ViewModel != null)
			{
				ViewModel.IsWelcomeGreetingActive = true;
			}
			ContentHost.IsHitTestVisible = false;
			HideGreetingTargetPets();
			if (WelcomeOverlay != null)
			{
				WelcomeOverlay.Visibility = Visibility.Visible;
				WelcomeOverlay.Opacity = 0.0;
			}
			if (WelcomeOverlayTranslate != null)
			{
				WelcomeOverlayTranslate.X = 0.0;
				WelcomeOverlayTranslate.Y = 0.0;
			}
			if (WelcomePetScale != null)
			{
				WelcomePetScale.ScaleX = 1.05;
				WelcomePetScale.ScaleY = 1.05;
			}
		}
	}
	private void PlayStartupEntranceAnimation()
	{
		if (_startupEntrancePlayed || ViewModel == null || !ViewModel.IsNotchVisible || ViewModel.IsSlimBarVisible || !IsLoaded)
		{
			return;
		}
		_startupEntrancePlayed = true;
		UpdatePillLayoutTransformCenter();
		CubicEase easeOut = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		QuarticEase dropEase = new QuarticEase
		{
			EasingMode = EasingMode.EaseOut
		};
		BackEase popEase = new BackEase
		{
			EasingMode = EasingMode.EaseOut,
			Amplitude = 0.22
		};
		DoubleAnimation slideDown = new DoubleAnimation
		{
			To = 0.0,
			Duration = TimeSpan.FromMilliseconds(400.0),
			EasingFunction = dropEase
		};
		DoubleAnimation fadeIn = new DoubleAnimation
		{
			To = 1.0,
			Duration = TimeSpan.FromMilliseconds(280.0),
			EasingFunction = easeOut
		};
		DoubleAnimation popX = new DoubleAnimation
		{
			To = 1.0,
			Duration = TimeSpan.FromMilliseconds(460.0),
			EasingFunction = popEase
		};
		DoubleAnimation popY = new DoubleAnimation
		{
			To = 1.0,
			Duration = TimeSpan.FromMilliseconds(460.0),
			EasingFunction = popEase
		};
		DoubleAnimation contentFade = new DoubleAnimation
		{
			To = 1.0,
			BeginTime = TimeSpan.FromMilliseconds(100.0),
			Duration = TimeSpan.FromMilliseconds(240.0),
			EasingFunction = easeOut
		};
		PillTranslateTransform.BeginAnimation(TranslateTransform.YProperty, slideDown);
		PillContainer.BeginAnimation(UIElement.OpacityProperty, fadeIn);
		PillScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, popX);
		PillScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, popY);
		if (_startupGreetingPlanned && CanPlayStartupGreeting())
		{
			// Sapaan maskot menggantikan fade konten: jam & widget muncul setelah maskot bergeser.
			_ = RunStartupGreetingAsync();
		}
		else
		{
			ContentHost.BeginAnimation(UIElement.OpacityProperty, contentFade);
		}
		Log.Information("Startup entrance animation started. GreetingPlanned={GreetingPlanned}", _startupGreetingPlanned);
	}

	private bool CanPlayStartupGreeting()
	{
		return ViewModel != null
			&& ViewModel.IsNotchVisible
			&& !ViewModel.IsSlimBarVisible
			&& ViewModel.CurrentState == NotchState.Collapsed
			&& ViewModel.IsPetEnabled
			&& WelcomeOverlay != null
			&& WelcomePet != null
			&& ContentHost != null;
	}

	private void ViewModel_StartupGreetingPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(NotchViewModel.CurrentState) or nameof(NotchViewModel.IsSlimBarVisible) or nameof(NotchViewModel.IsNotchVisible))
		{
			if (!CanPlayStartupGreeting())
			{
				CancelStartupGreeting();
			}
		}
	}

	private async Task RunStartupGreetingAsync()
	{
		if (!_startupGreetingPlanned || _startupGreetingActive) return;
		if (!CanPlayStartupGreeting())
		{
			ContentHost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(240)));
			return;
		}

		_startupGreetingActive = true;
		var cts = new CancellationTokenSource();
		_startupGreetingCts = cts;
		ViewModel!.PropertyChanged += ViewModel_StartupGreetingPropertyChanged;

		try
		{
			ContentHost.IsHitTestVisible = false;
			HideGreetingTargetPets();
			WelcomeOverlay.Visibility = Visibility.Visible;
			WelcomeOverlay.Opacity = 0.0;
			WelcomeOverlayTranslate.X = 0.0;
			WelcomeOverlayTranslate.Y = 0.0;
			WelcomePetScale.ScaleX = 1.05;
			WelcomePetScale.ScaleY = 1.05;

			CubicEase easeOut = new() { EasingMode = EasingMode.EaseOut };
			WelcomeOverlay.BeginAnimation(UIElement.OpacityProperty,
				new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = easeOut });

			BackEase pop = new() { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };
			WelcomePetScale.BeginAnimation(ScaleTransform.ScaleXProperty,
				new DoubleAnimation(1.3, TimeSpan.FromMilliseconds(520)) { EasingFunction = pop, BeginTime = TimeSpan.FromMilliseconds(60) });
			WelcomePetScale.BeginAnimation(ScaleTransform.ScaleYProperty,
				new DoubleAnimation(1.3, TimeSpan.FromMilliseconds(520)) { EasingFunction = pop, BeginTime = TimeSpan.FromMilliseconds(60) });

			WelcomePet.PlayWelcome(TimeSpan.FromSeconds(3.0));
			Log.Information("Startup greeting started.");

			await Task.Delay(TimeSpan.FromMilliseconds(3000), cts.Token).ConfigureAwait(true);
			if (cts.IsCancellationRequested) return;

			bool hasTarget = TryGetGreetingSlideOffset(out double offsetX, out double offsetY);
			FrameworkElement? targetPet = GetGreetingTargetPet();
			Log.Information("Startup greeting sliding to pet. HasTarget={HasTarget}, OffsetX={OffsetX:0.0}, OffsetY={OffsetY:0.0}", hasTarget, offsetX, offsetY);
			CubicEase slideEase = new() { EasingMode = EasingMode.EaseInOut };
			var slideDuration = TimeSpan.FromMilliseconds(560);

			if (hasTarget)
			{
				double finalScale = GetGreetingTargetScale(targetPet);
				WelcomeOverlayTranslate.BeginAnimation(TranslateTransform.XProperty,
					new DoubleAnimation(offsetX, slideDuration) { EasingFunction = slideEase });
				WelcomeOverlayTranslate.BeginAnimation(TranslateTransform.YProperty,
					new DoubleAnimation(offsetY, slideDuration) { EasingFunction = slideEase });
				WelcomePetScale.BeginAnimation(ScaleTransform.ScaleXProperty,
					new DoubleAnimation(finalScale, slideDuration) { EasingFunction = slideEase });
				WelcomePetScale.BeginAnimation(ScaleTransform.ScaleYProperty,
					new DoubleAnimation(finalScale, slideDuration) { EasingFunction = slideEase });
			}

			// Pet tujuan baru crossfade setelah overlay mendarat, agar tidak ada dua maskot sekaligus.
			WelcomeOverlay.BeginAnimation(UIElement.OpacityProperty,
				new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200))
				{
					BeginTime = TimeSpan.FromMilliseconds(hasTarget ? 560 : 60),
					EasingFunction = slideEase
				});
			if (hasTarget && targetPet != null)
			{
				targetPet.BeginAnimation(UIElement.OpacityProperty,
					new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180))
					{
						BeginTime = TimeSpan.FromMilliseconds(560),
						EasingFunction = slideEase
					});
			}
			ContentHost.BeginAnimation(UIElement.OpacityProperty,
				new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(360))
				{
					BeginTime = TimeSpan.FromMilliseconds(120),
					EasingFunction = slideEase
				});

			await Task.Delay(TimeSpan.FromMilliseconds(hasTarget ? 820 : 640), cts.Token).ConfigureAwait(true);
		}
		catch (TaskCanceledException)
		{
		}
		finally
		{
			FinishStartupGreeting();
		}
	}

	/// <summary>Pet collapsed yang mewakili widget aktif, atau null untuk widget tanpa pet.</summary>
	private FrameworkElement? GetGreetingTargetPet()
	{
		return ViewModel?.WidgetHost.CurrentWidgetType switch
		{
			WidgetType.Clock => ClockCollapsedPet,
			WidgetType.Media => MediaCollapsedPet,
			WidgetType.Notification => NotificationCollapsedPet,
			_ => null
		};
	}

	/// <summary>Skala akhir overlay agar isi maskot (kanvas 28×20) seukuran persis pet widget tujuan.</summary>
	private double GetGreetingTargetScale(FrameworkElement? pet)
	{
		if (pet == null || pet.ActualWidth <= 0.0 || pet.ActualHeight <= 0.0)
			return 1.0;
		return Math.Min(pet.ActualWidth / 28.0, pet.ActualHeight / 20.0);
	}

	/// <summary>Sembunyikan pet collapsed selama sapaan agar tidak muncul di posisi tujuan sebelum overlay tiba.</summary>
	private void HideGreetingTargetPets()
	{
		foreach (FrameworkElement? pet in new FrameworkElement?[] { ClockCollapsedPet, MediaCollapsedPet, TimerCollapsedPet, NotificationCollapsedPet })
		{
			if (pet == null) continue;
			pet.BeginAnimation(UIElement.OpacityProperty, null);
			pet.Opacity = 0.0;
			pet.IsHitTestVisible = false;
		}
	}

	private void RestoreGreetingTargetPets()
	{
		foreach (FrameworkElement? pet in new FrameworkElement?[] { ClockCollapsedPet, MediaCollapsedPet, TimerCollapsedPet, NotificationCollapsedPet })
		{
			if (pet == null) continue;
			pet.BeginAnimation(UIElement.OpacityProperty, null);
			pet.Opacity = 1.0;
			pet.IsHitTestVisible = true;
		}
	}

	/// <summary>Pusat pet collapsed yang sedang tampil, dalam koordinat PillContainer.</summary>
	private bool TryGetGreetingSlideOffset(out double offsetX, out double offsetY)
	{
		offsetX = 0.0;
		offsetY = 0.0;

		FrameworkElement? pet = GetGreetingTargetPet();

		if (pet == null || !pet.IsVisible || pet.ActualWidth <= 0.0 || pet.ActualHeight <= 0.0)
			return false;
		if (PillContainer == null || WelcomeOverlay == null || WelcomeOverlay.ActualHeight <= 0.0)
			return false;

		try
		{
			Point center = pet.TranslatePoint(new Point(pet.ActualWidth / 2.0, pet.ActualHeight / 2.0), PillContainer);
			offsetX = center.X - PillContainer.ActualWidth / 2.0;
			offsetY = center.Y - WelcomeOverlay.ActualHeight / 2.0;
			return true;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	private void CancelStartupGreeting()
	{
		_startupGreetingCts?.Cancel();
		FinishStartupGreeting();
	}

	private void FinishStartupGreeting()
	{
		if (!_startupGreetingActive && !(ViewModel?.IsWelcomeGreetingActive ?? false)) return;
		_startupGreetingActive = false;
		Log.Information("Startup greeting finished.");

		if (ViewModel != null)
		{
			ViewModel.PropertyChanged -= ViewModel_StartupGreetingPropertyChanged;
			ViewModel.IsWelcomeGreetingActive = false;
		}

		if (WelcomeOverlay != null)
		{
			WelcomeOverlay.BeginAnimation(UIElement.OpacityProperty, null);
			WelcomeOverlay.Opacity = 0.0;
			WelcomeOverlay.Visibility = Visibility.Collapsed;
		}
		if (WelcomeOverlayTranslate != null)
		{
			WelcomeOverlayTranslate.BeginAnimation(TranslateTransform.XProperty, null);
			WelcomeOverlayTranslate.BeginAnimation(TranslateTransform.YProperty, null);
			WelcomeOverlayTranslate.X = 0.0;
			WelcomeOverlayTranslate.Y = 0.0;
		}
		if (WelcomePetScale != null)
		{
			WelcomePetScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			WelcomePetScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			WelcomePetScale.ScaleX = 1.3;
			WelcomePetScale.ScaleY = 1.3;
		}
		if (WelcomePet != null)
			WelcomePet.HideGreeting();
		RestoreGreetingTargetPets();
		if (ContentHost != null)
		{
			ContentHost.BeginAnimation(UIElement.OpacityProperty, null);
			ContentHost.Opacity = 1.0;
			ContentHost.IsHitTestVisible = true;
		}

		_startupGreetingCts?.Dispose();
		_startupGreetingCts = null;
	}
	private void UpdateWindowVisibility()
	{
		if (ViewModel == null)
		{
			return;
		}

		bool wasSlimBarPeekActive = _lastSlimBarPeekActive;
		_lastSlimBarPeekActive = ViewModel.IsSlimBarPeekActive;

		Log.Debug("UpdateWindowVisibility: slim={Slim}, notch={Notch}, peek={Peek}, wasPeek={WasPeek}, pillShown={Pill}, pillOpacity={Opacity:0.00}, pillSize={Width:0.0}x{Height:0.0}, isSlimMode={Mode}",
			ViewModel.IsSlimBarVisible,
			ViewModel.IsNotchVisible,
			ViewModel.IsSlimBarPeekActive,
			wasSlimBarPeekActive,
			IsPillVisiblyShown(),
			PillContainer?.Opacity ?? -1.0,
			PillContainer?.ActualWidth ?? -1.0,
			PillContainer?.ActualHeight ?? -1.0,
			_isSlimBarMode);

		if (_slimBarMorphToNotchActive && !ViewModel.IsSlimBarVisible)
		{
			if (ViewModel.IsNotchVisible)
			{
				// Morph masuk masih memiliki presentasi; event visibilitas lain
				// tidak boleh memulai animasi notch normal di tengah morph.
				return;
			}

			CancelSlimBarMorphToNotch();
			if (FullscreenSlimBarHost != null)
			{
				FullscreenSlimBarHost.BeginAnimation(UIElement.OpacityProperty, null);
				FullscreenSlimBarHost.Opacity = 0.0;
				FullscreenSlimBarHost.Visibility = Visibility.Collapsed;
			}
		}
		if (ViewModel.IsSlimBarVisible)
		{
			// Morph dari pill/panel yang sedang tampil menjadi slim bar. Saat peek
			// baru ditutup, pill sudah collapsed; saat masuk fullscreen dari notch
			// biasa, pill bisa masih collapsed atau sedang collapse dari panel.
			bool fromPeek = wasSlimBarPeekActive && !ViewModel.IsSlimBarPeekActive;
			EnterSlimBarMode(morphFromNotch: fromPeek || IsPillVisiblyShown());
			return;
		}

		if (PillContainer != null)
		{
			PillContainer.IsHitTestVisible = true;
		}

		// Transisi slim bar -> notch (peek atau keluar fullscreen) memakai morph
		// yang sama agar bentuk bar benar-benar tumbuh menjadi badan notch.
		bool morphToNotch = _isSlimBarMode && ViewModel.IsNotchVisible;
		ExitSlimBarMode(morphToNotch);

		if (morphToNotch)
		{
			if (ViewModel.IsSlimBarPeekActive)
			{
				SchedulePostMorphHoverCheck();
			}
		}
		else
		{
			AnimateVisibility(ViewModel.IsNotchVisible);
		}
	}
	/// <summary>
	/// Menampilkan garis tipis di atas layar saat aplikasi fullscreen aktif,
	/// tanpa meng-collapse window agar garis tetap ter-render.
	/// </summary>
	private void EnterSlimBarMode(bool morphFromNotch = false)
	{
		if (_isSlimBarMode)
		{
			Log.Debug("EnterSlimBarMode skipped: already in slim mode (morphFromNotch={Morph}, morphActive={Active})", morphFromNotch, _notchToSlimMorphActive);
			return;
		}

		_isSlimBarMode = true;
		_notchInteractionRestoreGeneration++;
		Visibility = Visibility.Visible;
		Topmost = true;
		AssertTopmost(force: true);

		Log.Debug("EnterSlimBarMode: morphFromNotch={Morph}, pillShown={Pill}, pillOpacity={Opacity:0.00}",
			morphFromNotch, IsPillVisiblyShown(), PillContainer?.Opacity ?? -1.0);

		// Timer hover tidak boleh aktif selama transisi. Timer baru dinyalakan
		// setelah bentuk pill benar-benar sudah menjadi slim bar.
		CancelSlimBarMorphToNotch();
		_slimBarHoverTimer.Stop();
		_notchToSlimMorphDelayTimer.Stop();
		_slimBarHoverCharging = false;
		_slimBarHoverTriggered = false;
		_slimBarHoverStartedTick = 0;
		_slimBarHoverLastInsideTick = 0;
		InvalidateSlimBarHoverRect();
		if (morphFromNotch && IsPillVisiblyShown())
		{
			StartNotchToSlimBarMorph();
			return;
		}

		ResetSlimBarPresentation();
		StartSlimBarHoverTimer();

		// Fallback saat pill tidak terlihat (mis. aplikasi baru dibuka dalam
		// fullscreen): tampilkan garis dengan fade + scale dari tengah.
		if (FullscreenSlimBarHost != null && FullscreenSlimBarScale != null)
		{
			FullscreenSlimBarHost.Visibility = Visibility.Visible;
			FullscreenSlimBarHost.BeginAnimation(UIElement.OpacityProperty, null);
			FullscreenSlimBarHost.Opacity = 0.0;
			FullscreenSlimBarScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			FullscreenSlimBarScale.ScaleX = 0.6;
			var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
			var barFade = new DoubleAnimation
			{
				To = 1.0,
				Duration = TimeSpan.FromMilliseconds(180.0),
				EasingFunction = easeOut
			};
			var barScale = new DoubleAnimation
			{
				To = 1.0,
				Duration = TimeSpan.FromMilliseconds(220.0),
				EasingFunction = easeOut
			};
			FullscreenSlimBarHost.BeginAnimation(UIElement.OpacityProperty, barFade);
			FullscreenSlimBarScale.BeginAnimation(ScaleTransform.ScaleXProperty, barScale);
		}

		HideNotchPillAfterMorph();
	}

	private bool IsPillVisiblyShown()
	{
		return PillContainer != null
			&& IsLoaded
			&& Visibility == Visibility.Visible
			&& PillContainer.Visibility == Visibility.Visible
			&& PillContainer.Opacity > 0.5
			&& PillContainer.ActualWidth > 1.0
			&& PillContainer.ActualHeight > 1.0;
	}

	private void StartTopmostKeeper(IntPtr handle)
	{
		if (_topmostKeeperActive || handle == IntPtr.Zero)
		{
			return;
		}

		_notchHwnd = handle;
		_topmostKeeperActive = true;
		NativeMethods.WinEventDelegate foregroundDelegate = OnForegroundWinEvent;
		_foregroundWinEventDelegate = foregroundDelegate;
		_foregroundWinEventHook = NativeMethods.SetWinEventHook(
			NativeMethods.EVENT_SYSTEM_FOREGROUND,
			NativeMethods.EVENT_SYSTEM_FOREGROUND,
			IntPtr.Zero,
			foregroundDelegate,
			0,
			0,
			NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

		_topmostKeeperTimer.Tick += OnTopmostKeeperTick;
		_topmostKeeperTimer.Start();
		AssertTopmost(force: true);

		Log.Debug("Topmost keeper installed (hook={HookActive}).", _foregroundWinEventHook != IntPtr.Zero);
	}

	private void StopTopmostKeeper()
	{
		if (!_topmostKeeperActive)
		{
			return;
		}

		_topmostKeeperActive = false;
		_topmostAssertQueued = false;
		_topmostKeeperTimer.Stop();
		_topmostKeeperTimer.Tick -= OnTopmostKeeperTick;

		if (_foregroundWinEventHook != IntPtr.Zero)
		{
			NativeMethods.UnhookWinEvent(_foregroundWinEventHook);
			_foregroundWinEventHook = IntPtr.Zero;
		}

		_foregroundWinEventDelegate = null;
		_notchHwnd = IntPtr.Zero;
	}

	private void OnTopmostKeeperTick(object? sender, EventArgs e)
	{
		AssertTopmost(force: false);
	}

	private void OnForegroundWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
	{
		if (!_topmostKeeperActive || _topmostAssertQueued)
		{
			return;
		}

		_topmostAssertQueued = true;
		try
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
			{
				_topmostAssertQueued = false;
				AssertTopmost(force: true);
			}));
		}
		catch
		{
			_topmostAssertQueued = false;
		}
	}

	private void AssertTopmost(bool force)
	{
		if (!_topmostKeeperActive || !IsLoaded || _notchHwnd == IntPtr.Zero
			|| Visibility != Visibility.Visible || WindowState == WindowState.Minimized)
		{
			return;
		}

		long now = Environment.TickCount64;
		if (!force && now - _lastTopmostAssertTick < TopmostAssertThrottleMs)
		{
			return;
		}
		_lastTopmostAssertTick = now;

		if (!WindowStyleHelper.EnsureTopmost(_notchHwnd))
		{
			Log.Debug("Topmost keeper: SetWindowPos(HWND_TOPMOST) failed.");
		}


		if (_devicePickerPopupHwnd != IntPtr.Zero)
		{
			WindowStyleHelper.EnsureTopmost(_devicePickerPopupHwnd);
		}
	}

	private void StartNotchToSlimBarMorph()
	{
		_notchToSlimMorphDelayTimer.Stop();

		bool geometryStillMoving = ViewModel != null
			&& (Math.Abs(NotchWidth - ViewModel.TargetWidth) > 0.5
				|| Math.Abs(NotchHeight - ViewModel.TargetHeight) > 0.5);
		bool expandedContentVisible = ExpandedContentGrid != null
			&& ExpandedContentGrid.Visibility == Visibility.Visible
			&& ExpandedContentGrid.Opacity > 0.01;

		Log.Debug("StartNotchToSlimBarMorph: geometryMoving={Geom}, expandedVisible={Expanded}, notchWidth={NotchWidth:0.0}, targetWidth={TargetWidth:0.0}, notchHeight={NotchHeight:0.0}, targetHeight={TargetHeight:0.0}",
			geometryStillMoving, expandedContentVisible, NotchWidth, ViewModel?.TargetWidth ?? -1.0, NotchHeight, ViewModel?.TargetHeight ?? -1.0);

		if (geometryStillMoving || expandedContentVisible)
		{
			// Kalau panel baru collapse, biarkan animasi collapse selesai dulu
			// agar pill tidak menyusut dua kali (geometri + skala).
			_notchToSlimMorphDelayTimer.Start();
			Log.Debug("StartNotchToSlimBarMorph: delayed by {Delay} ms", SlimBarMorphFromNotchCollapseWaitMs);
			return;
		}

		AnimateSlimBarMorphFromNotch();
	}

	private void NotchToSlimMorphDelayTimer_Tick(object? sender, EventArgs e)
	{
		_notchToSlimMorphDelayTimer.Stop();
		Log.Debug("NotchToSlimMorphDelayTimer_Tick: isSlimMode={Mode}, slimVisible={Slim}, peek={Peek}", _isSlimBarMode, ViewModel?.IsSlimBarVisible ?? false, ViewModel?.IsSlimBarPeekActive ?? false);
		if (!_isSlimBarMode || ViewModel == null || !ViewModel.IsSlimBarVisible || ViewModel.IsSlimBarPeekActive)
		{
			return;
		}

		AnimateSlimBarMorphFromNotch();
	}

	private void StartSlimBarHoverTimer()
	{
		_slimBarHoverTimer.Start();
	}
	/// <summary>
	/// Morph pill (beserta kontennya) menjadi slim bar. Pill menyusut sebagai
	/// satu bentuk utuh sampai seukuran bar; konten tetap terlihat selama proses
	/// sehingga tidak ada momen notch kosong. Setelah itu bar mengambil alih
	/// lewat crossfade singkat pada ukuran yang sama.
	/// </summary>
	private void AnimateSlimBarMorphFromNotch()
	{
		_notchToSlimMorphDelayTimer.Stop();

		if (FullscreenSlimBar == null || FullscreenSlimBarHost == null || PillContainer == null
			|| ContentHost == null || PillScaleTransform == null)
		{
			ShowSlimBarImmediately();
			return;
		}

		CancelSlimBarMorphToNotch();
		_notchToSlimMorphActive = true;
		PillContainer.IsHitTestVisible = false;

		// Bekukan skala render saat ini (termasuk sisa hover-stretch) sebagai
		// titik awal penyusutan.
		double startScaleX = PillScaleTransform.ScaleX;
		double startScaleY = PillScaleTransform.ScaleY;
		PillScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		PillScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		PillScaleTransform.ScaleX = startScaleX;
		PillScaleTransform.ScaleY = startScaleY;

		if (PillTranslateTransform != null)
		{
			PillTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
			PillTranslateTransform.Y = 0.0;
		}

		// Konten tetap opaque; bar yang nanti menutupinya pada ukuran akhir.
		PillContainer.BeginAnimation(UIElement.OpacityProperty, null);
		PillContainer.Opacity = 1.0;
		ContentHost.BeginAnimation(UIElement.OpacityProperty, null);
		ContentHost.Opacity = 1.0;

		// Bar disembunyikan selama pill menyusut.
		FullscreenSlimBarHost.BeginAnimation(UIElement.OpacityProperty, null);
		FullscreenSlimBarHost.Opacity = 0.0;
		FullscreenSlimBarHost.Visibility = Visibility.Collapsed;
		FullscreenSlimBar.BeginAnimation(FrameworkElement.WidthProperty, null);
		FullscreenSlimBar.BeginAnimation(FrameworkElement.HeightProperty, null);
		FullscreenSlimBar.BeginAnimation(Border.CornerRadiusProperty, null);
		if (SlimBarHoverScale != null)
		{
			SlimBarHoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			SlimBarHoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			SlimBarHoverScale.ScaleX = 1.0;
			SlimBarHoverScale.ScaleY = 1.0;
		}
		if (FullscreenSlimBarScale != null)
		{
			FullscreenSlimBarScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			FullscreenSlimBarScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			FullscreenSlimBarScale.ScaleX = 1.0;
			FullscreenSlimBarScale.ScaleY = 1.0;
		}

		if (!TryGetNotchBodyLayoutSize(out double bodyLayoutWidth, out double bodyLayoutHeight))
		{
			ShowSlimBarImmediately();
			return;
		}

		// Pastikan poros skala render mengikuti lebar dan skala layout terkini
		// sebelum pill mulai menyusut.
		UpdatePillLayoutTransformCenter();

		double layoutScaleX = _pillLayoutScaleTransform?.ScaleX ?? 1.0;
		double layoutScaleY = _pillLayoutScaleTransform?.ScaleY ?? layoutScaleX;
		double effectiveWidth = Math.Max(1.0, bodyLayoutWidth * layoutScaleX);
		double effectiveHeight = Math.Max(1.0, bodyLayoutHeight * layoutScaleY);

		double clampedScale = Math.Max(0.75, Math.Min(2.0, ViewModel?.NotchScale ?? 1.0));
		double barWidth = Math.Max(80.0, Math.Round(220.0 * clampedScale));
		double barHeight = Math.Max(4.0, Math.Round(6.0 * clampedScale));

		// Samakan geometri bar dengan target morph sekarang juga, supaya crossfade
		// di akhir penyusutan tidak menimbulkan lompatan ukuran.
		FullscreenSlimBar.Width = barWidth;
		FullscreenSlimBar.Height = barHeight;
		FullscreenSlimBar.CornerRadius = new CornerRadius(0.0, 0.0, 6.0, 6.0);

		double targetScaleX = Math.Max(0.001, barWidth / effectiveWidth);
		double targetScaleY = Math.Max(0.001, barHeight / effectiveHeight);

		Log.Debug("AnimateSlimBarMorphFromNotch: start=({StartX:0.000},{StartY:0.000}), layout=({LayoutX:0.000},{LayoutY:0.000}), pill={PillW:0.0}x{PillH:0.0}, effective={EffW:0.0}x{EffH:0.0}, target=({TargetX:0.000},{TargetY:0.000}), bar={BarW:0.0}x{BarH:0.0}",
			startScaleX, startScaleY, layoutScaleX, layoutScaleY, bodyLayoutWidth, bodyLayoutHeight, effectiveWidth, effectiveHeight, targetScaleX, targetScaleY, barWidth, barHeight);

		var shrinkEase = new QuarticEase { EasingMode = EasingMode.EaseInOut };
		var duration = TimeSpan.FromMilliseconds(SlimBarMorphFromNotchShrinkMs);
		var scaleXAnimation = new DoubleAnimation(startScaleX, targetScaleX, duration)
		{
			EasingFunction = shrinkEase
		};
		var scaleYAnimation = new DoubleAnimation(startScaleY, targetScaleY, duration)
		{
			EasingFunction = shrinkEase
		};
		scaleYAnimation.Completed += (s, e) => BeginSlimBarHandoff();
		PillScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
		PillScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);
	}

	private void BeginSlimBarHandoff()
	{
		Log.Debug("BeginSlimBarHandoff: active={Active}, isSlimMode={Mode}, slimVisible={Slim}", _notchToSlimMorphActive, _isSlimBarMode, ViewModel?.IsSlimBarVisible ?? false);

		if (!_notchToSlimMorphActive || !_isSlimBarMode || ViewModel == null
			|| !ViewModel.IsSlimBarVisible || FullscreenSlimBarHost == null || PillContainer == null)
		{
			return;
		}

		FullscreenSlimBarHost.Visibility = Visibility.Visible;
		FullscreenSlimBarHost.BeginAnimation(UIElement.OpacityProperty, null);
		FullscreenSlimBarHost.Opacity = 0.0;
		try
		{
			Point pillTopCenter = ContentHost.PointToScreen(new Point(ContentHost.Width / 2.0, 0.0));
			Point barTopCenter = FullscreenSlimBar.PointToScreen(new Point(FullscreenSlimBar.Width / 2.0, 0.0));
			Log.Debug("SlimBar handoff alignment: pillTopCenter=({PillX:0.0},{PillY:0.0}), barTopCenter=({BarX:0.0},{BarY:0.0}), delta=({DeltaX:0.0},{DeltaY:0.0})",
				pillTopCenter.X, pillTopCenter.Y, barTopCenter.X, barTopCenter.Y, barTopCenter.X - pillTopCenter.X, barTopCenter.Y - pillTopCenter.Y);
		}
		catch
		{
		}

		var handoffEase = new CubicEase { EasingMode = EasingMode.EaseOut };
		var duration = TimeSpan.FromMilliseconds(SlimBarMorphFromNotchHandoffMs);
		var barFade = new DoubleAnimation
		{
			From = 0.0,
			To = 1.0,
			Duration = duration,
			EasingFunction = handoffEase
		};
		var pillFade = new DoubleAnimation
		{
			From = 1.0,
			To = 0.0,
			Duration = duration,
			EasingFunction = handoffEase
		};
		barFade.Completed += (s, e) => FinishSlimBarMorph();
		FullscreenSlimBarHost.BeginAnimation(UIElement.OpacityProperty, barFade);
		PillContainer.BeginAnimation(UIElement.OpacityProperty, pillFade);
	}

	private void FinishSlimBarMorph()
	{
		Log.Debug("FinishSlimBarMorph: isSlimMode={Mode}", _isSlimBarMode);
		_notchToSlimMorphActive = false;
		if (!_isSlimBarMode)
		{
			return;
		}

		if (FullscreenSlimBarHost != null)
		{
			FullscreenSlimBarHost.BeginAnimation(UIElement.OpacityProperty, null);
			FullscreenSlimBarHost.Opacity = 1.0;
			FullscreenSlimBarHost.Visibility = Visibility.Visible;
		}
		HideNotchPillAfterMorph();

		_slimBarHoverCharging = false;
		_slimBarHoverTriggered = false;
		_slimBarHoverStartedTick = 0;
		_slimBarHoverLastInsideTick = 0;
		InvalidateSlimBarHoverRect();
		StartSlimBarHoverTimer();
	}

	private void ShowSlimBarImmediately()
	{
		Log.Warning("ShowSlimBarImmediately: morph fallback used");
		_notchToSlimMorphDelayTimer.Stop();
		_notchToSlimMorphActive = false;
		ResetSlimBarPresentation();
		if (FullscreenSlimBarHost != null)
		{
			FullscreenSlimBarHost.BeginAnimation(UIElement.OpacityProperty, null);
			FullscreenSlimBarHost.Opacity = 1.0;
			FullscreenSlimBarHost.Visibility = Visibility.Visible;
		}
		if (FullscreenSlimBarScale != null)
		{
			FullscreenSlimBarScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			FullscreenSlimBarScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			FullscreenSlimBarScale.ScaleX = 1.0;
			FullscreenSlimBarScale.ScaleY = 1.0;
		}
		HideNotchPillAfterMorph();
		StartSlimBarHoverTimer();
	}

	private void HideNotchPillAfterMorph()
	{
		if (PillContainer != null)
		{
			PillContainer.BeginAnimation(UIElement.OpacityProperty, null);
			PillContainer.Opacity = 0.0;
			PillContainer.IsHitTestVisible = false;
		}
		if (PillScaleTransform != null)
		{
			PillScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			PillScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			PillScaleTransform.ScaleX = 1.0;
			PillScaleTransform.ScaleY = 1.0;
		}
		if (PillTranslateTransform != null)
		{
			PillTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
			PillTranslateTransform.Y = 0.0;
		}
		if (ContentHost != null)
		{
			ContentHost.BeginAnimation(UIElement.OpacityProperty, null);
			ContentHost.Opacity = 1.0;
		}
	}

	private void CancelNotchToSlimMorph()
	{
		_notchToSlimMorphDelayTimer.Stop();
		if (!_notchToSlimMorphActive)
		{
			return;
		}

		_notchToSlimMorphActive = false;
		if (PillScaleTransform != null)
		{
			PillScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			PillScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			PillScaleTransform.ScaleX = 1.0;
			PillScaleTransform.ScaleY = 1.0;
		}
		if (PillTranslateTransform != null)
		{
			PillTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
			PillTranslateTransform.Y = 0.0;
		}
		if (PillContainer != null)
		{
			PillContainer.BeginAnimation(UIElement.OpacityProperty, null);
			PillContainer.Opacity = 1.0;
			PillContainer.IsHitTestVisible = true;
		}
		if (ContentHost != null)
		{
			ContentHost.BeginAnimation(UIElement.OpacityProperty, null);
			ContentHost.Opacity = 1.0;
		}
	}
	private void ExitSlimBarMode(bool morphToNotch = false)
	{
		if (!_isSlimBarMode)
		{
			return;
		}

		_isSlimBarMode = false;
		_notchToSlimMorphDelayTimer.Stop();
		CancelNotchToSlimMorph();
		StopSlimBarHover(resetScale: !morphToNotch);

		if (FullscreenSlimBarHost == null)
		{
			return;
		}

		if (morphToNotch)
		{
			AnimateSlimBarMorphToNotch();
			return;
		}

		var fadeOut = new DoubleAnimation
		{
			To = 0.0,
			Duration = TimeSpan.FromMilliseconds(140.0),
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
		};
		fadeOut.Completed += (s, e) =>
		{
			if (!_isSlimBarMode)
			{
				FullscreenSlimBarHost.BeginAnimation(UIElement.OpacityProperty, null);
				FullscreenSlimBarHost.Opacity = 0.0;
				FullscreenSlimBarHost.Visibility = Visibility.Collapsed;
			}
		};
		FullscreenSlimBarHost.BeginAnimation(UIElement.OpacityProperty, fadeOut);
	}

	/// <summary>
	/// Menyesuaikan ukuran garis tipis mengikuti skala notch, tetap pada pixel bulat.
	/// </summary>
	private void UpdateSlimBarGeometry(double effectiveScale)
	{
		if (FullscreenSlimBar == null)
		{
			return;
		}

		InvalidateSlimBarHoverRect();
		double clamped = Math.Max(0.75, Math.Min(2.0, effectiveScale));
		FullscreenSlimBar.BeginAnimation(Border.CornerRadiusProperty, null);
		FullscreenSlimBar.Width = Math.Max(80.0, Math.Round(220.0 * clamped));
		FullscreenSlimBar.Height = Math.Max(4.0, Math.Round(6.0 * clamped));
		FullscreenSlimBar.CornerRadius = new CornerRadius(0.0, 0.0, 6.0, 6.0);
	}

	/// <summary>
	/// Menyiapkan ulang elemen slim bar ke kondisi awal (setelah morph atau exit).
	/// </summary>
	private void ResetSlimBarPresentation()
	{
		if (FullscreenSlimBar == null || FullscreenSlimBarHost == null)
		{
			return;
		}

		FullscreenSlimBar.BeginAnimation(FrameworkElement.WidthProperty, null);
		FullscreenSlimBar.BeginAnimation(FrameworkElement.HeightProperty, null);
		FullscreenSlimBarHost.BeginAnimation(UIElement.OpacityProperty, null);
		FullscreenSlimBarHost.Opacity = 0.0;
		FullscreenSlimBarHost.Visibility = Visibility.Collapsed;

		if (SlimBarHoverScale != null)
		{
			SlimBarHoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			SlimBarHoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			SlimBarHoverScale.ScaleX = 1.0;
			SlimBarHoverScale.ScaleY = 1.0;
		}

		if (FullscreenSlimBarScale != null)
		{
			FullscreenSlimBarScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			FullscreenSlimBarScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			FullscreenSlimBarScale.ScaleX = 0.6;
			FullscreenSlimBarScale.ScaleY = 1.0;
		}

		UpdateSlimBarGeometry(ViewModel?.NotchScale ?? 1.0);
	}

	/// <summary>
	/// Polling posisi kursor: slim bar tetap click-through, jadi hover tidak bisa
	/// dideteksi lewat event mouse WPF. Timer ini hanya membaca posisi kursor.
	/// </summary>
	private void SlimBarHoverTimer_Tick(object? sender, EventArgs e)
	{
		if (!_isSlimBarMode || ViewModel == null || !ViewModel.IsSlimBarVisible || _slimBarHoverTriggered)
		{
			StopSlimBarHover();
			return;
		}

		if (!NativeMethods.GetCursorPos(out var cursor) || !TryGetSlimBarHoverRect(out var hoverRect))
		{
			CancelSlimBarHoverCharge();
			return;
		}

		long now = Environment.TickCount64;
		bool isInside = cursor.X >= hoverRect.Left && cursor.X <= hoverRect.Right
			&& cursor.Y >= hoverRect.Top && cursor.Y <= hoverRect.Bottom;

		if (!isInside)
		{
			// Beri toleransi singkat agar gerakan kursor kecil tidak mereset
			// hitungan hover 3 detik dari awal.
			if (_slimBarHoverCharging && _slimBarHoverLastInsideTick != 0
				&& now - _slimBarHoverLastInsideTick < SlimBarHoverExitGraceMs)
			{
				return;
			}

			CancelSlimBarHoverCharge();
			return;
		}

		_slimBarHoverLastInsideTick = now;
		if (!_slimBarHoverCharging)
		{
			_slimBarHoverCharging = true;
			_slimBarHoverStartedTick = now;
			AnimateSlimBarHoverCharge();
			return;
		}

		if (now - _slimBarHoverStartedTick >= SlimBarHoverOpenMs)
		{
			_slimBarHoverTriggered = true;
			StopSlimBarHover(resetScale: false);
			ViewModel.OpenSlimBarPeek();
		}
	}
	/// <summary>Animasi kecil saat garis sedang di-hover (indikator 3 detik).</summary>
	private void AnimateSlimBarHoverCharge()
	{
		if (SlimBarHoverScale == null)
		{
			return;
		}

		var ease = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
		var scaleX = new DoubleAnimation
		{
			To = 1.15,
			Duration = TimeSpan.FromMilliseconds(SlimBarHoverOpenMs),
			EasingFunction = ease
		};
		var scaleY = new DoubleAnimation
		{
			To = 1.55,
			Duration = TimeSpan.FromMilliseconds(SlimBarHoverOpenMs),
			EasingFunction = ease
		};

		SlimBarHoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
		SlimBarHoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
	}

	private void CancelSlimBarHoverCharge()
	{
		if (!_slimBarHoverCharging)
		{
			return;
		}

		_slimBarHoverCharging = false;
		_slimBarHoverStartedTick = 0;
		_slimBarHoverLastInsideTick = 0;
		if (SlimBarHoverScale == null)
		{
			return;
		}

		var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
		var scaleX = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180.0))
		{
			EasingFunction = ease
		};
		var scaleY = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180.0))
		{
			EasingFunction = ease
		};

		SlimBarHoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
		SlimBarHoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
	}

	private void StopSlimBarHover(bool resetScale = true)
	{
		_slimBarHoverTimer.Stop();
		_slimBarHoverCharging = false;
		_slimBarHoverStartedTick = 0;
		_slimBarHoverLastInsideTick = 0;

		if (!resetScale || SlimBarHoverScale == null)
		{
			return;
		}

		SlimBarHoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		SlimBarHoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		SlimBarHoverScale.ScaleX = 1.0;
		SlimBarHoverScale.ScaleY = 1.0;
	}

	/// <summary>
	/// Area hover fisik (piksel layar) di sekitar slim bar. Dibuat seukuran
	/// notch collapsed agar kursor yang memicu peek tetap berada di atas notch.
	/// </summary>
	private bool TryGetSlimBarHoverRect(out Rect rect)
	{
		rect = Rect.Empty;
		if (FullscreenSlimBar == null || !FullscreenSlimBar.IsVisible)
		{
			return false;
		}

		if (_slimBarHoverRectValid)
		{
			rect = _slimBarHoverRect;
			return true;
		}

		try
		{
			double barLayoutWidth = FullscreenSlimBar.Width;
			double barLayoutHeight = FullscreenSlimBar.Height;
			if (double.IsNaN(barLayoutWidth) || barLayoutWidth <= 0.0)
			{
				barLayoutWidth = FullscreenSlimBar.ActualWidth;
			}
			if (double.IsNaN(barLayoutHeight) || barLayoutHeight <= 0.0)
			{
				barLayoutHeight = FullscreenSlimBar.ActualHeight;
			}
			if (barLayoutWidth <= 0.0 || barLayoutHeight <= 0.0)
			{
				return false;
			}

			Point topLeft = FullscreenSlimBar.PointToScreen(new Point(0.0, 0.0));
			Point bottomRight = FullscreenSlimBar.PointToScreen(new Point(barLayoutWidth, barLayoutHeight));
			double dpiX = 1.0;
			double dpiY = 1.0;
			PresentationSource? source = PresentationSource.FromVisual(this);
			if (source?.CompositionTarget != null)
			{
				dpiX = source.CompositionTarget.TransformToDevice.M11;
				dpiY = source.CompositionTarget.TransformToDevice.M22;
			}

			double barWidth = Math.Max(1.0, bottomRight.X - topLeft.X);
			double barHeight = Math.Max(1.0, bottomRight.Y - topLeft.Y);
			double notchScale = Math.Max(0.5, Math.Min(2.0, ViewModel?.NotchScale ?? 1.0)) * 1.2;
			double targetWidth = Math.Max(ViewModel?.TargetWidth ?? 295.0, 220.0) * notchScale;
			double width = Math.Max(barWidth, targetWidth * dpiX) + (8.0 * dpiX);
			double height = Math.Max(barHeight, 32.0 * notchScale * dpiY) + (4.0 * dpiY);
			double centerX = topLeft.X + (barWidth / 2.0);
			rect = new Rect(centerX - (width / 2.0), topLeft.Y - (2.0 * dpiY), width, height);
			_slimBarHoverRect = rect;
			_slimBarHoverRectValid = true;
			return true;
		}
		catch
		{
			return false;
		}
	}
	private bool IsCursorOverNotch()
	{
		if (PillContainer == null || !PillContainer.IsVisible)
		{
			return false;
		}

		if (!NativeMethods.GetCursorPos(out var cursor))
		{
			return false;
		}

		try
		{
			Point topLeft = PillContainer.PointToScreen(new Point(0.0, 0.0));
			Point bottomRight = PillContainer.PointToScreen(new Point(PillContainer.ActualWidth, PillContainer.ActualHeight));
			return cursor.X >= topLeft.X && cursor.X <= bottomRight.X
				&& cursor.Y >= topLeft.Y && cursor.Y <= bottomRight.Y;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Setelah morph selesai, lanjutkan perilaku hover normal (pill → panel)
	/// jika kursor masih di atas notch; jika tidak, kembalikan ke slim bar.
	/// </summary>
	private void SchedulePostMorphHoverCheck()
	{
		Task.Delay(SlimBarMorphTotalMs + 20).ContinueWith(_ =>
		{
			Dispatcher.Invoke(() =>
			{
				if (!IsLoaded || ViewModel == null || !ViewModel.IsSlimBarPeekActive)
				{
					return;
				}

				if (IsCursorOverNotch())
				{
					ViewModel.OnMouseEnter();
				}
				else
				{
					ViewModel.OnMouseLeave();
				}
			});
		}, TaskScheduler.Default);
	}

	/// <summary>
	/// Morph slim bar menjadi notch collapsed: pill (beserta kontennya) sudah
	/// disiapkan tepat seukuran visual bar, lalu tumbuh sebagai satu bentuk
	/// sampai ukuran notch penuh selama 360 ms. Bar di atasnya memudar lebih
	/// dulu (80 ms) pada ukuran yang sama, sehingga konten pill langsung terlihat
	/// dan tidak ada momen notch kosong. Total 440 ms.
	/// </summary>
	private void AnimateSlimBarMorphToNotch()
	{
		if (FullscreenSlimBar == null || FullscreenSlimBarHost == null || PillContainer == null
			|| ContentHost == null || PillScaleTransform == null)
		{
			return;
		}

		_isHiding = false;
		Visibility = Visibility.Visible;
		Topmost = true;
		AssertTopmost(force: true);

		int morphGeneration = ++_slimBarMorphToNotchGeneration;
		_slimBarMorphToNotchActive = true;

		// Pill beserta kontennya disiapkan tepat seukuran visual slim bar saat
		// ini, lalu tumbuh sebagai satu bentuk sampai ukuran notch penuh. Konten
		// sudah ikut terlihat sejak awal sehingga tidak ada momen notch kosong.
		bool pillCurrentlyVisible = PillContainer.Opacity > 0.01;
		double currentPillScaleX = PillScaleTransform.ScaleX;
		double currentPillScaleY = PillScaleTransform.ScaleY;
		PillScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		PillScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		PillScaleTransform.ScaleX = 1.0;
		PillScaleTransform.ScaleY = 1.0;
		if (PillTranslateTransform != null)
		{
			PillTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
			PillTranslateTransform.Y = 0.0;
		}
		PillContainer.BeginAnimation(UIElement.OpacityProperty, null);
		PillContainer.Opacity = 1.0;
		ContentHost.BeginAnimation(UIElement.OpacityProperty, null);
		ContentHost.Opacity = 1.0;
		PillContainer.IsHitTestVisible = false;

		if (!TryGetNotchBodyLayoutSize(out double bodyLayoutWidth, out double bodyLayoutHeight))
		{
			_slimBarMorphToNotchActive = false;
			_slimBarMorphToNotchGeneration++;
			PillContainer.IsHitTestVisible = true;
			return;
		}

		// Pastikan poros skala render mengikuti lebar dan skala layout terkini
		// sebelum pill mulai tumbuh.
		UpdatePillLayoutTransformCenter();

		double layoutScaleX = _pillLayoutScaleTransform?.ScaleX ?? 1.0;
		double layoutScaleY = _pillLayoutScaleTransform?.ScaleY ?? layoutScaleX;
		double effectiveWidth = Math.Max(1.0, bodyLayoutWidth * layoutScaleX);
		double effectiveHeight = Math.Max(1.0, bodyLayoutHeight * layoutScaleY);

		double barScaleX = FullscreenSlimBarScale?.ScaleX ?? 1.0;
		double barScaleY = FullscreenSlimBarScale?.ScaleY ?? 1.0;
		double hoverScaleX = SlimBarHoverScale?.ScaleX ?? 1.0;
		double hoverScaleY = SlimBarHoverScale?.ScaleY ?? 1.0;
		double barWidth = FullscreenSlimBar.Width > 0.0 ? FullscreenSlimBar.Width : FullscreenSlimBar.ActualWidth;
		double barHeight = FullscreenSlimBar.Height > 0.0 ? FullscreenSlimBar.Height : FullscreenSlimBar.ActualHeight;
		double startWidth = Math.Max(1.0, barWidth * barScaleX * hoverScaleX);
		double startHeight = Math.Max(1.0, barHeight * barScaleY * hoverScaleY);

		double startScaleX = Math.Max(0.001, startWidth / effectiveWidth);
		double startScaleY = Math.Max(0.001, startHeight / effectiveHeight);
		if (pillCurrentlyVisible)
		{
			startScaleX = Math.Max(startScaleX, currentPillScaleX);
			startScaleY = Math.Max(startScaleY, currentPillScaleY);
		}
		PillScaleTransform.ScaleX = startScaleX;
		PillScaleTransform.ScaleY = startScaleY;

		Log.Debug("AnimateSlimBarMorphToNotch: start=({StartX:0.000},{StartY:0.000}), effective=({EffW:0.0}x{EffH:0.0}), barVisual={BarW:0.0}x{BarH:0.0}, grow={Grow}ms",
			startScaleX, startScaleY, effectiveWidth, effectiveHeight, startWidth, startHeight, SlimBarMorphGrowMs);

		// Bar tetap tampil di atas pill selama crossfade 80 ms pada ukuran visual
		// yang sama, lalu memudar. Setelah itu hanya pill yang terlihat dan terus
		// membesar sampai ukuran notch penuh.
		FullscreenSlimBarHost.Visibility = Visibility.Visible;
		FullscreenSlimBarHost.BeginAnimation(UIElement.OpacityProperty, null);
		FullscreenSlimBarHost.Opacity = 1.0;

		var handoffEase = new CubicEase { EasingMode = EasingMode.EaseOut };
		var handoff = new DoubleAnimation
		{
			From = 1.0,
			To = 0.0,
			Duration = TimeSpan.FromMilliseconds(SlimBarMorphFromNotchHandoffMs),
			EasingFunction = handoffEase
		};
		handoff.Completed += (s, e) =>
		{
			if (!_slimBarMorphToNotchActive || morphGeneration != _slimBarMorphToNotchGeneration || _isSlimBarMode)
			{
				return;
			}
			FullscreenSlimBarHost.BeginAnimation(UIElement.OpacityProperty, null);
			FullscreenSlimBarHost.Opacity = 0.0;
			FullscreenSlimBarHost.Visibility = Visibility.Collapsed;
			if (SlimBarHoverScale != null)
			{
				SlimBarHoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
				SlimBarHoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
				SlimBarHoverScale.ScaleX = 1.0;
				SlimBarHoverScale.ScaleY = 1.0;
			}
			if (FullscreenSlimBarScale != null)
			{
				FullscreenSlimBarScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
				FullscreenSlimBarScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
				FullscreenSlimBarScale.ScaleX = 1.0;
				FullscreenSlimBarScale.ScaleY = 1.0;
			}
		};
		FullscreenSlimBarHost.BeginAnimation(UIElement.OpacityProperty, handoff);

		var growEase = new QuarticEase { EasingMode = EasingMode.EaseInOut };
		var growDuration = TimeSpan.FromMilliseconds(SlimBarMorphGrowMs);
		var growX = new DoubleAnimation(startScaleX, 1.0, growDuration)
		{
			BeginTime = TimeSpan.FromMilliseconds(SlimBarMorphFromNotchHandoffMs),
			EasingFunction = growEase
		};
		var growY = new DoubleAnimation(startScaleY, 1.0, growDuration)
		{
			BeginTime = TimeSpan.FromMilliseconds(SlimBarMorphFromNotchHandoffMs),
			EasingFunction = growEase
		};
		growY.Completed += (s, e) =>
		{
			Log.Debug("AnimateSlimBarMorphToNotch completed: peek={Peek}, notchVisible={Notch}, slimMode={Mode}", ViewModel?.IsSlimBarPeekActive ?? false, ViewModel?.IsNotchVisible ?? false, _isSlimBarMode);
			if (morphGeneration != _slimBarMorphToNotchGeneration || _isSlimBarMode)
			{
				return;
			}

			_slimBarMorphToNotchActive = false;
			if (ViewModel == null || !ViewModel.IsNotchVisible)
			{
				return;
			}
			PillScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			PillScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			PillScaleTransform.ScaleX = 1.0;
			PillScaleTransform.ScaleY = 1.0;
			PillContainer.IsHitTestVisible = true;
		};
		PillScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, growX);
		PillScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, growY);
		ScheduleNotchInteractionRestore();
	}
	private void CancelSlimBarMorphToNotch()
	{
		if (!_slimBarMorphToNotchActive)
		{
			return;
		}

		_slimBarMorphToNotchActive = false;
		_slimBarMorphToNotchGeneration++;

		if (FullscreenSlimBarHost != null)
		{
			FullscreenSlimBarHost.BeginAnimation(UIElement.OpacityProperty, null);
			FullscreenSlimBarHost.Opacity = 0.0;
			FullscreenSlimBarHost.Visibility = Visibility.Collapsed;
		}
		if (SlimBarHoverScale != null)
		{
			SlimBarHoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			SlimBarHoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			SlimBarHoverScale.ScaleX = 1.0;
			SlimBarHoverScale.ScaleY = 1.0;
		}
		if (FullscreenSlimBarScale != null)
		{
			FullscreenSlimBarScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			FullscreenSlimBarScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			FullscreenSlimBarScale.ScaleX = 1.0;
			FullscreenSlimBarScale.ScaleY = 1.0;
		}
		InvalidateSlimBarHoverRect();
	}

	private bool TryGetNotchBodyLayoutSize(out double width, out double height)
	{
		width = 0.0;
		height = 0.0;
		if (ContentHost == null)
		{
			return false;
		}

		width = ContentHost.Width;
		height = ContentHost.Height;
		if (double.IsNaN(width) || width <= 1.0)
		{
			width = NotchWidth;
		}
		if (double.IsNaN(height) || height <= 1.0)
		{
			height = NotchHeight;
		}
		if (double.IsNaN(width) || width <= 1.0)
		{
			width = ContentHost.ActualWidth;
		}
		if (double.IsNaN(height) || height <= 1.0)
		{
			height = ContentHost.ActualHeight;
		}
		return width > 1.0 && height > 1.0;
	}

	private void InvalidateSlimBarHoverRect()
	{
		_slimBarHoverRectValid = false;
	}
	private void ScheduleNotchInteractionRestore()
	{
		int generation = ++_notchInteractionRestoreGeneration;
		Task.Delay(SlimBarMorphFromNotchTotalMs + 60).ContinueWith(_ =>
		{
			Dispatcher.Invoke(() =>
			{
				if (generation != _notchInteractionRestoreGeneration)
				{
					return;
				}
				if (!IsLoaded || ViewModel == null || _isSlimBarMode || _notchToSlimMorphActive)
				{
					return;
				}
				if (!ViewModel.IsNotchVisible || PillContainer == null)
				{
					return;
				}
				if (_slimBarMorphToNotchActive)
				{
					// Completion animasi bisa terlewat; pastikan morph tidak
					// meninggalkan skala/hit-test dalam kondisi setengah jadi.
					_slimBarMorphToNotchActive = false;
					_slimBarMorphToNotchGeneration++;
					if (PillScaleTransform != null)
					{
						PillScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
						PillScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
						PillScaleTransform.ScaleX = 1.0;
						PillScaleTransform.ScaleY = 1.0;
					}
				}
				if (!PillContainer.IsHitTestVisible)
				{
					PillContainer.IsHitTestVisible = true;
					Log.Debug("Notch interaction restore fallback: hit test re-enabled");
				}
			});
		}, TaskScheduler.Default);
	}

	private void UpdatePillLayoutTransformCenter(double? layoutScaleOverride = null, bool updateRenderCenter = true)
	{
		if (_pillLayoutScaleTransform == null || PillContainer == null)
		{
			return;
		}

		double width = PillContainer.Width;
		if (double.IsNaN(width) || width <= 0.0)
		{
			width = PillContainer.ActualWidth;
		}

		if (width <= 0.0)
		{
			return;
		}

		double layoutScale = layoutScaleOverride
			?? (_pillLayoutScaleTransform.ScaleX > 0.0 ? _pillLayoutScaleTransform.ScaleX : 1.0);
		if (layoutScale <= 0.0)
		{
			layoutScale = 1.0;
		}

		// LayoutTransform men-scale konten terhadap CenterX lokal, lalu menggeser
		// hasilnya sehingga konten mulai dari 0 pada ruang layout. Akibatnya pusat
		// konten berada di (layoutScale * width / 2), bukan width / 2. Skala render
		// morph harus berporos tepat di titik itu agar tidak ada offset horizontal.
		double layoutCenterX = width / 2.0;
		if (Math.Abs(_pillLayoutScaleTransform.CenterX - layoutCenterX) > 0.01)
		{
			_pillLayoutScaleTransform.CenterX = layoutCenterX;
		}

		if (Math.Abs(_pillLayoutScaleTransform.CenterY) > 0.01)
		{
			_pillLayoutScaleTransform.CenterY = 0.0;
		}

		// Saat update per-frame, poros render hanya perlu diinisialisasi sekali;
		// setelah itu nilainya hanya berubah ketika skala notch berubah.
		if (PillScaleTransform != null && (updateRenderCenter || PillScaleTransform.CenterX <= 0.01))
		{
			double renderCenterX = width * layoutScale / 2.0;
			if (Math.Abs(PillScaleTransform.CenterX - renderCenterX) > 0.01)
			{
				PillScaleTransform.CenterX = renderCenterX;
			}

			if (Math.Abs(PillScaleTransform.CenterY) > 0.01)
			{
				PillScaleTransform.CenterY = 0.0;
			}
		}
	}

public void ApplyNotchScale(double scale, bool animate = true)
	{
		if (scale < 0.5 || scale > 2.0)
		{
			scale = 1.0;
		}

		// Baseline scale factor: the 120% (1.2) physical notch size is now standard 100% (1.0)
		const double BaseScaleFactor = 1.2;
		double effectiveScale = scale * BaseScaleFactor;
		UpdateSlimBarGeometry(scale);

		if (PillContainer == null)
		{
			return;
		}
		if (_pillLayoutScaleTransform == null)
		{
			if (PillContainer.LayoutTransform is ScaleTransform pillLayoutScaleTransform)
			{
				_pillLayoutScaleTransform = pillLayoutScaleTransform;
			}
			else
			{
				_pillLayoutScaleTransform = new ScaleTransform(effectiveScale, effectiveScale)
			{
				CenterX = (PillContainer.Width > 0.0 ? PillContainer.Width : 0.0) / 2.0,
				CenterY = 0.0
			};
				PillContainer.LayoutTransform = _pillLayoutScaleTransform;
			}
		}
		if (!animate)
		{
			_pillLayoutScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			_pillLayoutScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			_pillLayoutScaleTransform.ScaleX = effectiveScale;
			_pillLayoutScaleTransform.ScaleY = effectiveScale;
		}
		else if (!(Math.Abs(_pillLayoutScaleTransform.ScaleX - effectiveScale) < 0.002))
		{
			CubicEase easingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			TimeSpan timeSpan = TimeSpan.FromMilliseconds(260.0);
			DoubleAnimation animation = new DoubleAnimation
			{
				To = effectiveScale,
				Duration = timeSpan,
				EasingFunction = easingFunction
			};
			DoubleAnimation animation2 = new DoubleAnimation
			{
				To = effectiveScale,
				Duration = timeSpan,
				EasingFunction = easingFunction
			};
			_pillLayoutScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation, HandoffBehavior.SnapshotAndReplace);
			_pillLayoutScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation2, HandoffBehavior.SnapshotAndReplace);
		}

		// Pakai skala target, bukan nilai animasi yang sedang berjalan, supaya poros
		// skala render sudah benar begitu animasi layout selesai.
		UpdatePillLayoutTransformCenter(effectiveScale);

		UpdateOverlayWindowForState();
	}

	private void AnimateVisibility(bool isVisible)







	{







		if (PillContainer == null || PillTranslateTransform == null || PillScaleTransform == null || ContentHost == null)







		{







			Visibility = ((!isVisible) ? Visibility.Collapsed : Visibility.Visible);







			return;







		}







		if (isVisible)







		{







			_isHiding = false;







			Visibility = Visibility.Visible;







			Topmost = true;

			AssertTopmost(force: true);







			BackEase easingFunction = new BackEase







			{







				EasingMode = EasingMode.EaseOut,







				Amplitude = 0.22







			};







			CubicEase easingFunction2 = new CubicEase







			{







				EasingMode = EasingMode.EaseOut







			};







			DoubleAnimation animation = new DoubleAnimation







			{







				To = 1.0,







				Duration = TimeSpan.FromMilliseconds(350.0),







				EasingFunction = easingFunction







			};







			DoubleAnimation animation2 = new DoubleAnimation







			{







				To = 1.0,







				Duration = TimeSpan.FromMilliseconds(330.0),







				EasingFunction = easingFunction







			};







			DoubleAnimation animation3 = new DoubleAnimation







			{







				To = 0.0,







				Duration = TimeSpan.FromMilliseconds(300.0),







				EasingFunction = easingFunction2







			};







			DoubleAnimation animation4 = new DoubleAnimation







			{







				To = 1.0,







				Duration = TimeSpan.FromMilliseconds(180.0),







				EasingFunction = easingFunction2







			};







			DoubleAnimation animation5 = new DoubleAnimation







			{







				To = 1.0,







				BeginTime = TimeSpan.FromMilliseconds(120.0),







				Duration = TimeSpan.FromMilliseconds(220.0),







				EasingFunction = easingFunction2







			};







			PillScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);







			PillScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation2);







			PillTranslateTransform.BeginAnimation(TranslateTransform.YProperty, animation3);







			PillContainer.BeginAnimation(UIElement.OpacityProperty, animation4);







			ContentHost.BeginAnimation(UIElement.OpacityProperty, animation5);







			return;







		}







		_isHiding = true;







		DoubleAnimation animation6 = new DoubleAnimation







		{







			To = 0.0,







			Duration = TimeSpan.FromMilliseconds(90.0),







			EasingFunction = new CubicEase







			{







				EasingMode = EasingMode.EaseOut







			}







		};







		ContentHost.BeginAnimation(UIElement.OpacityProperty, animation6);







		CubicEase easingFunction3 = new CubicEase







		{







			EasingMode = EasingMode.EaseIn







		};







		DoubleAnimation animation7 = new DoubleAnimation







		{







			To = 0.26,







			Duration = TimeSpan.FromMilliseconds(260.0),







			EasingFunction = easingFunction3







		};







		DoubleAnimation animation8 = new DoubleAnimation







		{







			To = 0.05,







			Duration = TimeSpan.FromMilliseconds(240.0),







			EasingFunction = easingFunction3







		};







		DoubleAnimation animation9 = new DoubleAnimation







		{







			To = -16.0,







			Duration = TimeSpan.FromMilliseconds(260.0),







			EasingFunction = easingFunction3







		};







		DoubleAnimation doubleAnimation = new DoubleAnimation







		{







			To = 0.0,







			Duration = TimeSpan.FromMilliseconds(230.0),







			EasingFunction = easingFunction3







		};







		doubleAnimation.Completed += (object? _, EventArgs _) =>







		{







			if (_isHiding && !ViewModel.IsNotchVisible && !ViewModel.IsSlimBarVisible)







			{







				Visibility = Visibility.Collapsed;







			}







		};







		PillScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation7);







		PillScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation8);







		PillTranslateTransform.BeginAnimation(TranslateTransform.YProperty, animation9);







		PillContainer.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);







	}















	private double GetOverlayWindowWidth()
	{
		double effectiveScale = Math.Clamp(ViewModel?.NotchScale ?? 1.0, 0.75, 1.4) * 1.2;
		// Kebutuhan terlebar: notch collapsed sampai 620 DIP + hover stretch 1.15 + bayangan.
		double width = (NotchViewModel.MaxExpandedWidth * effectiveScale * 1.15) + 48.0;
		return Math.Min(OverlayMaxWidth, Math.Max(640.0, width));
	}

	private void UpdateOverlayWindowForState()
	{
		if (!IsLoaded || ViewModel == null) return;

		if (ViewModel.CurrentState == NotchState.Expanded)
		{
			_overlayWindowSettleTimer?.Stop();
			ApplyOverlayWindowSize(collapsed: false);
			return;
		}

		// Shrink setelah animasi collapse selesai agar pill yang sedang menyusut tidak terpotong.
		if (_overlayWindowSettleTimer == null)
		{
			_overlayWindowSettleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(380) };
			_overlayWindowSettleTimer.Tick += (s, e) =>
			{
				_overlayWindowSettleTimer!.Stop();
				if (ViewModel?.CurrentState == NotchState.Collapsed)
				{
					ApplyOverlayWindowSize(collapsed: true);
				}
			};
		}
		_overlayWindowSettleTimer.Stop();
		_overlayWindowSettleTimer.Start();
	}

	private void ApplyOverlayWindowSize(bool collapsed)
	{
		double targetWidth = GetOverlayWindowWidth();
		double targetHeight = collapsed ? OverlayCollapsedHeight : OverlayExpandedHeight;
		if (Math.Abs(Width - targetWidth) < 0.5 && Math.Abs(Height - targetHeight) < 0.5)
		{
			return;
		}

		Width = targetWidth;
		Height = targetHeight;
		PositionOnMonitor(force: true);
	}

	private void PositionOnMonitor(bool force = false)







	{







		try







		{







			MonitorDetails targetMonitor = ViewModel.DisplayService.GetTargetMonitor(ViewModel.SettingsService.CurrentSettings.TargetMonitorDeviceName);







			if (force || !(_lastPositionedMonitor == targetMonitor.DeviceName) || !IsLoaded)







			{







				_lastPositionedMonitor = targetMonitor.DeviceName;
				InvalidateSlimBarHoverRect();







				int num = (int)targetMonitor.Bounds.Left;







				int num2 = (int)targetMonitor.Bounds.Width;







				int num3 = (int)targetMonitor.Bounds.Top;







				double num4 = ((targetMonitor.DpiScale > 0.0) ? targetMonitor.DpiScale : 1.0);







				int num5 = (int)Math.Round(Width * num4);







				int num6 = (int)Math.Round(Height * num4);







				int num7 = num + (num2 - num5) / 2;







				int num8 = num3;







				nint handle = new WindowInteropHelper(this).Handle;







				if (handle != IntPtr.Zero)







				{







					NativeMethods.SetWindowPos(handle, NativeMethods.HWND_TOPMOST, num7, num8, num5, num6, 80u);







				}







				Log.Information("PositionOnMonitor: Target={Monitor} ({Device}), Bounds=[L:{ScreenLeft}, T:{ScreenTop}, W:{ScreenWidth}, H:{ScreenHeight}], Scale={Scale}, Placed at PhysX={PhysX}, PhysY={PhysY}, PhysW={PhysW}, PhysH={PhysH}", targetMonitor.DisplayName, targetMonitor.DeviceName, num, num3, num2, targetMonitor.Bounds.Height, num4, num7, num8, num5, num6);







			}







		}







		catch (Exception exception)







		{







			Log.Warning(exception, "Failed to position notch window on monitor, falling back to primary screen");







		}







	}















	private void SettingsButton_Click(object sender, RoutedEventArgs e)
	{
		ViewModel?.Collapse();
	}

	public void ShowSettingsWindow()







	{







		if (_settingsWindow == null)







		{







			SettingsViewModel viewModel = new SettingsViewModel(ViewModel.SettingsService, new AutostartService(), ViewModel.DisplayService);







			_settingsWindow = new SettingsWindow(viewModel);







		}







		_settingsWindow.Show();







		_settingsWindow.WindowState = WindowState.Normal;







		_settingsWindow.Activate();







	}















	/// <summary>
	/// Menjadwalkan satu pembaruan geometri per frame. Animasi width/height/radius
	/// mengubah tiga dependency property sekaligus; tanpa koalesensi, path notch
	/// dibangun ulang hingga tiga kali dalam satu tick animasi.
	/// </summary>
	private void QueueNotchGeometryUpdate()
	{
		if (_geometryUpdateQueued)
		{
			return;
		}

		_geometryUpdateQueued = true;
		Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
		{
			_geometryUpdateQueued = false;
			UpdateNotchGeometry();
		}));
	}

	private void UpdateNotchGeometry()







	{







		if (NotchBackgroundPath == null || NotchStrokePath == null || PillContainer == null || ContentHost == null)







		{







			return;







		}







		double notchWidth = NotchWidth;







		double notchHeight = NotchHeight;







		double num = 12.0;







		double notchBottomRadius = NotchBottomRadius;







		if (!(notchWidth <= 0.0) && !(notchHeight <= 0.0))







		{







			if (Math.Abs(notchWidth - _lastGeometryWidth) < 0.001
				&& Math.Abs(notchHeight - _lastGeometryHeight) < 0.001
				&& Math.Abs(notchBottomRadius - _lastGeometryRadius) < 0.001)
			{
				return;
			}

			_lastGeometryWidth = notchWidth;
			_lastGeometryHeight = notchHeight;
			_lastGeometryRadius = notchBottomRadius;

			Geometry data = BuildFillGeometry(notchWidth, notchHeight, num, notchBottomRadius);







			Geometry data2 = BuildStrokeGeometry(notchWidth, notchHeight, num, notchBottomRadius);







			NotchBackgroundPath.Data = data;







			if (NotchBackgroundFadePath != null)







			{







				NotchBackgroundFadePath.Data = data;







			}







			NotchStrokePath.Data = data2;







			if (NotchStrokeFadePath != null)







			{







				NotchStrokeFadePath.Data = data2;







			}







			UpdateOutlineFxGeometry(notchWidth, notchHeight, num, notchBottomRadius);



			double width = notchWidth + 2.0 * num;







			if (double.IsNaN(PillContainer.Width) || Math.Abs(PillContainer.Width - width) > 0.01)
			{
				PillContainer.Width = width;
			}
			UpdatePillLayoutTransformCenter(updateRenderCenter: false);







			if (double.IsNaN(PillContainer.Height) || Math.Abs(PillContainer.Height - notchHeight) > 0.01)
			{
				PillContainer.Height = notchHeight;
			}







			if (double.IsNaN(ContentHost.Width) || Math.Abs(ContentHost.Width - notchWidth) > 0.01)
			{
				ContentHost.Width = notchWidth;
			}







			if (double.IsNaN(ContentHost.Height) || Math.Abs(ContentHost.Height - notchHeight) > 0.01)
			{
				ContentHost.Height = notchHeight;
			}







			RectangleGeometry rectangleGeometry = new RectangleGeometry(new Rect(0.0, 0.0, notchWidth, notchHeight), notchBottomRadius, notchBottomRadius);







			rectangleGeometry.Freeze();







			ContentHost.Clip = rectangleGeometry;







			if (SubNotchBaseTranslate != null)







			{







				double subNotchY = Math.Round(notchHeight - 1.0);
				if (Math.Abs(SubNotchBaseTranslate.Y - subNotchY) > 0.01)
				{
					SubNotchBaseTranslate.Y = subNotchY;
				}







			}







		}







	}















	private static Geometry BuildFillGeometry(double bodyWidth, double height, double earRadius, double bottomRadius)







	{







		StreamGeometry streamGeometry = new StreamGeometry();







		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())







		{







			streamGeometryContext.BeginFigure(new Point(0.0, 0.0), isFilled: true, isClosed: true);







			streamGeometryContext.ArcTo(new Point(earRadius, earRadius), new Size(earRadius, earRadius), 0.0, isLargeArc: false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);







			streamGeometryContext.LineTo(new Point(earRadius, height - bottomRadius), isStroked: true, isSmoothJoin: true);







			streamGeometryContext.ArcTo(new Point(earRadius + bottomRadius, height), new Size(bottomRadius, bottomRadius), 0.0, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: true);







			streamGeometryContext.LineTo(new Point(earRadius + bodyWidth - bottomRadius, height), isStroked: true, isSmoothJoin: true);







			streamGeometryContext.ArcTo(new Point(earRadius + bodyWidth, height - bottomRadius), new Size(bottomRadius, bottomRadius), 0.0, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: true);







			streamGeometryContext.LineTo(new Point(earRadius + bodyWidth, earRadius), isStroked: true, isSmoothJoin: true);







			streamGeometryContext.ArcTo(new Point(bodyWidth + 2.0 * earRadius, 0.0), new Size(earRadius, earRadius), 0.0, isLargeArc: false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);







		}







		streamGeometry.Freeze();







		return streamGeometry;







	}















	private static Geometry BuildStrokeGeometry(double bodyWidth, double height, double earRadius, double bottomRadius)







	{







		StreamGeometry streamGeometry = new StreamGeometry();







		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())







		{







			streamGeometryContext.BeginFigure(new Point(0.0, 0.0), isFilled: false, isClosed: false);







			streamGeometryContext.ArcTo(new Point(earRadius, earRadius), new Size(earRadius, earRadius), 0.0, isLargeArc: false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);







			streamGeometryContext.LineTo(new Point(earRadius, height - bottomRadius), isStroked: true, isSmoothJoin: true);







			streamGeometryContext.ArcTo(new Point(earRadius + bottomRadius, height), new Size(bottomRadius, bottomRadius), 0.0, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: true);







			streamGeometryContext.LineTo(new Point(earRadius + bodyWidth - bottomRadius, height), isStroked: true, isSmoothJoin: true);







			streamGeometryContext.ArcTo(new Point(earRadius + bodyWidth, height - bottomRadius), new Size(bottomRadius, bottomRadius), 0.0, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: true);







			streamGeometryContext.LineTo(new Point(earRadius + bodyWidth, earRadius), isStroked: true, isSmoothJoin: true);







			streamGeometryContext.ArcTo(new Point(bodyWidth + 2.0 * earRadius, 0.0), new Size(earRadius, earRadius), 0.0, isLargeArc: false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);







		}







		streamGeometry.Freeze();







		return streamGeometry;







	}















	private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)







	{







		switch (msg)







		{







		case NativeMethods.WM_CLIPBOARDUPDATE:







			if (DataContext is NotchViewModel notchClipboardVm)







			{







				notchClipboardVm.Clipboard.ProcessClipboardUpdate();







			}







			handled = false;







			return IntPtr.Zero;







		case 736:







			handled = true;







			PositionOnMonitor(force: true);







			return IntPtr.Zero;







		case 1125:







			_trayService?.HandleTrayMessage(wParam, lParam);







			handled = true;







			return IntPtr.Zero;







		case 536:







		{







			nint num = wParam;







			switch (((IntPtr)num).ToInt32())







			{







			case 4:







				ViewModel.SuspendPower();







				break;







			case 7:







			case 18:







				ViewModel.ResumePower();







				break;







			case 32787:







				if (lParam == IntPtr.Zero)







				{







					break;







				}







				try







				{







					NativeMethods.POWERBROADCAST_SETTING pOWERBROADCAST_SETTING = Marshal.PtrToStructure<NativeMethods.POWERBROADCAST_SETTING>(lParam);







					if (pOWERBROADCAST_SETTING.PowerSetting == NativeMethods.GUID_CONSOLE_DISPLAY_STATE || pOWERBROADCAST_SETTING.PowerSetting == NativeMethods.GUID_SESSION_DISPLAY_STATUS)







					{







						if (pOWERBROADCAST_SETTING.Data == 0)







						{







							ViewModel.SuspendPower();







						}







						else if (pOWERBROADCAST_SETTING.Data == 1)







						{







							ViewModel.ResumePower();







						}







					}







				}







				catch







				{







				}







				break;







			}







			ViewModel.RefreshBatteryState();







			break;







		}







		}







		if (msg == 537)







		{







			nint num = wParam;







			ViewModel.RefreshBluetoothRadioState();







			switch (((IntPtr)num).ToInt32())







			{







			case 32768:







				if (lParam != IntPtr.Zero && Marshal.ReadInt32(lParam, 4) == NativeMethods.DBT_DEVTYP_VOLUME)







				{







					ViewModel.TriggerUsbNotification(isConnected: true);







				}







				break;







			case 32772:







				if (lParam != IntPtr.Zero && Marshal.ReadInt32(lParam, 4) == NativeMethods.DBT_DEVTYP_VOLUME)







				{







					ViewModel.TriggerUsbNotification(isConnected: false);







				}







				break;







			}







		}







		switch (msg)







		{







		case 33:







			handled = true;







			if (ViewModel.CurrentState == NotchState.Expanded)







			{







				return new IntPtr(1);







			}







			return new IntPtr(3);







		case 132:







			if (ViewModel.IsSlimBarVisible || !ViewModel.IsNotchVisible || Visibility != Visibility.Visible || _isHiding)







			{







				handled = true;







				return new IntPtr(-1);







			}

			if (ViewModel?.IsSliderDragging == true)
			{
				handled = true;
				return new IntPtr(1);
			}







			if (PillContainer == null || !IsLoaded || NotchBackgroundPath?.Data == null)







			{







				break;







			}







			try







			{







				nint num = lParam;







				int num2 = (short)(((IntPtr)num).ToInt64() & 0xFFFF);







				num = lParam;







				int num3 = (short)((((IntPtr)num).ToInt64() >> 16) & 0xFFFF);







				Point point = new Point(num2, num3);







				Point hitPoint = PillContainer.PointFromScreen(point);







				bool flag = false;







				if (hitPoint.Y >= NotchHeight - 1.5)







				{







					if (TimerSubNotchPill != null && TimerSubNotchPill.Visibility == Visibility.Visible && _isTimerSubNotchShowing)







					{







						try







						{







							Point point2 = TimerSubNotchPill.PointFromScreen(point);







							if (point2.X >= 0.0 && point2.X <= TimerSubNotchPill.ActualWidth && point2.Y >= 0.0 && point2.Y <= TimerSubNotchPill.ActualHeight)







							{







								flag = true;







							}







						}







						catch







						{







						}







					}







					if (!flag && StopwatchSubNotchPill != null && StopwatchSubNotchPill.Visibility == Visibility.Visible && _isStopwatchSubNotchShowing)







					{







						try







						{







							Point point3 = StopwatchSubNotchPill.PointFromScreen(point);







							if (point3.X >= 0.0 && point3.X <= StopwatchSubNotchPill.ActualWidth && point3.Y >= 0.0 && point3.Y <= StopwatchSubNotchPill.ActualHeight)







							{







								flag = true;







							}







						}







						catch







						{







						}







					}



					if (!flag && NotificationSubNotchPill != null && NotificationSubNotchPill.Visibility == Visibility.Visible && _isNotificationSubNotchShowing)

					{

						try

						{

							Point pointNotif = NotificationSubNotchPill.PointFromScreen(point);

							if (pointNotif.X >= 0.0 && pointNotif.X <= NotificationSubNotchPill.ActualWidth && pointNotif.Y >= 0.0 && pointNotif.Y <= NotificationSubNotchPill.ActualHeight)

							{

								flag = true;

							}

						}

						catch

						{

						}

					}







				}







				if (!flag && DownloadSubNotchPill != null && DownloadSubNotchPill.Visibility == Visibility.Visible && _isDownloadSubNotchShowing)

				{

					try

					{

						Point pointDownload = DownloadSubNotchPill.PointFromScreen(point);

						if (pointDownload.X >= 0.0 && pointDownload.X <= DownloadSubNotchPill.ActualWidth && pointDownload.Y >= 0.0 && pointDownload.Y <= DownloadSubNotchPill.ActualHeight)

						{

							flag = true;

						}

					}

					catch

					{

					}

				}

				if (NotchBackgroundPath.Data.FillContains(hitPoint) | flag)







				{







					if (!_wasCursorInsideNotch)







					{







						_wasCursorInsideNotch = true;







					}







					handled = true;







					return new IntPtr(1);







				}







				if (_wasCursorInsideNotch)







				{







					_wasCursorInsideNotch = false;







					if (!_isContextMenuOpen && ViewModel.CurrentState == NotchState.Expanded && !ViewModel.IsLockedExpanded && !_wasAutoExpandedByDrag && !(ViewModel?.Volume?.IsDevicePickerOpen ?? false) && !(ViewModel?.Media?.IsLyricsVersionPickerOpen ?? false) && !(ViewModel?.IsSliderDragging ?? false))







					{







						ViewModel.OnMouseLeave();







					}







				}







				handled = true;







				return new IntPtr(-1);







			}







			catch







			{







			}







			break;







		}







		return IntPtr.Zero;







	}















	private void AnimateToCurrentState()







	{







		NotchState currentState = ViewModel.CurrentState;
		if (currentState != NotchState.Collapsed)
		{
			SetOutlineFxVisible(false);
		}







		UpdateOverlayWindowForState();

		bool flag = currentState != _lastAnimatedState;







		_lastAnimatedState = currentState;







		bool flag2 = currentState == NotchState.Expanded;







		if (!flag2)







		{







			_isContextMenuOpen = false;







			_wasCursorInsideNotch = false;







			HideDragChoiceOverlay();
			StopWeatherAnimations();
		}







		nint handle = new WindowInteropHelper(this).Handle;







		if (handle != IntPtr.Zero)







		{







			WindowStyleHelper.SetNoActivate(handle, !flag2);







		}







		Log.Debug("AnimateToCurrentState: State={State}, StateChanged={StateChanged}", currentState, flag);



		bool isNotifActive = ViewModel.WidgetHost.CurrentWidgetType == WidgetType.Notification && ViewModel.WidgetHost.IsTransientActive;

		AnimateNotificationSubNotch(flag2 && isNotifActive);
		AnimateTimerSubNotch(ViewModel?.TimerStopwatch?.IsTimerRunning ?? false);
		AnimateStopwatchSubNotch(ViewModel?.TimerStopwatch?.IsStopwatchRunning ?? false);
		UpdateDownloadSubNotchVisibility();
		SyncNotchOutlineFx();







		double targetWidth = ViewModel.TargetWidth;







		double targetHeight = ViewModel.TargetHeight;







		double targetCornerRadius = ViewModel.TargetCornerRadius;

		bool geometryChanged = Math.Abs(NotchWidth - targetWidth) > 0.001
			|| Math.Abs(NotchHeight - targetHeight) > 0.001
			|| Math.Abs(NotchBottomRadius - targetCornerRadius) > 0.001;

		bool targetUnchanged = Math.Abs(_lastAnimatedTargetWidth - targetWidth) < 0.01
			&& Math.Abs(_lastAnimatedTargetHeight - targetHeight) < 0.01
			&& Math.Abs(_lastAnimatedTargetRadius - targetCornerRadius) < 0.01;

		// Saat state dan target geometri tidak berubah, tidak ada yang perlu
		// dianimasikan. Ini mencegah konten expanded ikut fade ulang ketika
		// pengguna hanya berpindah slide atau mengubah tinggi panel.
		if (!flag && !geometryChanged && targetUnchanged)
		{
			return;
		}

		_lastAnimatedTargetWidth = targetWidth;
		_lastAnimatedTargetHeight = targetHeight;
		_lastAnimatedTargetRadius = targetCornerRadius;







		TimeSpan timeSpan = TimeSpan.FromMilliseconds(flag2 ? 360 : 230);







		IEasingFunction easingFunction = (flag && flag2)
			? (IEasingFunction)new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.28 }
			: new QuarticEase { EasingMode = EasingMode.EaseOut };







		if (Math.Abs(NotchWidth - targetWidth) > 0.5)







		{







			DoubleAnimation animation = new DoubleAnimation(targetWidth, timeSpan)







			{







				EasingFunction = easingFunction







			};







			BeginAnimation(NotchWidthProperty, animation);







		}







		if (Math.Abs(NotchHeight - targetHeight) > 0.5)







		{







			DoubleAnimation animation2 = new DoubleAnimation(targetHeight, timeSpan)







			{







				EasingFunction = easingFunction







			};

			animation2.Completed += (object? _, EventArgs _) =>
			{
				UpdateNotchGeometry();
			};





			BeginAnimation(NotchHeightProperty, animation2);







		}







		if (Math.Abs(NotchBottomRadius - targetCornerRadius) > 0.5)







		{







			DoubleAnimation animation3 = new DoubleAnimation(targetCornerRadius, timeSpan)







			{







				EasingFunction = easingFunction







			};







			BeginAnimation(NotchBottomRadiusProperty, animation3);







		}







		if (!flag)







		{







			return;







		}







		if (flag2)







		{







			ResetHoverStretch();







			if (PillScaleTransform != null)







			{







				PillScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);







				PillScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);







				PillScaleTransform.ScaleY = 1.0;







				PillScaleTransform.ScaleX = 1.0;

			}



			ExpandedContentGrid.IsHitTestVisible = true;

			ExpandedContentGrid.Visibility = Visibility.Visible;
			ExpandedHeaderPet?.UpdateStyleVisibility();




			if (flag)

			{

				ResetSlideViews(ViewModel.CurrentSlideIndex, updateNavPill: false);



				if (!ExpandedContentGrid.IsMeasureValid || !ExpandedContentGrid.IsArrangeValid)



				{



					ExpandedContentGrid.UpdateLayout();



				}



				UpdateNavPill(ViewModel.CurrentSlideIndex, animate: false);

			}







			DoubleAnimation animation4 = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(110.0))







			{







				EasingFunction = easingFunction







			};







			CollapsedContentGrid.BeginAnimation(UIElement.OpacityProperty, animation4);







			if (CollapsedContentScale != null)







			{







				DoubleAnimation animation5 = new DoubleAnimation(0.85, TimeSpan.FromMilliseconds(110.0))







				{







					EasingFunction = easingFunction







				};







				CollapsedContentScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation5);







				CollapsedContentScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation5);







			}







			DoubleAnimation animation6 = new DoubleAnimation







			{







				From = 0.0,







				To = 1.0,







				BeginTime = TimeSpan.FromMilliseconds(70.0),







				Duration = TimeSpan.FromMilliseconds(250.0),







				EasingFunction = easingFunction







			};







			ExpandedContentGrid.BeginAnimation(UIElement.OpacityProperty, animation6);







			if (ExpandedContentTranslate != null)







			{







				DoubleAnimation animation7 = new DoubleAnimation(-18.0, 0.0, TimeSpan.FromMilliseconds(340.0))







				{







					EasingFunction = new QuarticEase







					{







						EasingMode = EasingMode.EaseOut







					}







				};







				ExpandedContentTranslate.BeginAnimation(TranslateTransform.YProperty, animation7);







			}







			if (ExpandedContentScale != null)







			{







				DoubleAnimation animation8 = new DoubleAnimation(0.93, 1.0, TimeSpan.FromMilliseconds(340.0))







				{







					EasingFunction = easingFunction







				};







				ExpandedContentScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation8);







				ExpandedContentScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation8);
				if (ViewModel.CurrentSlideIndex == 2)
				{
					StartWeatherAnimations();
				}







			}







			return;







		}







		ExpandedContentGrid.IsHitTestVisible = false;







		DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(50.0))







		{







			EasingFunction = new CubicEase







			{







				EasingMode = EasingMode.EaseOut







			}







		};







		doubleAnimation.Completed += (object? _, EventArgs _) =>







		{







			if (ViewModel?.Volume != null)
			{
				ViewModel.Volume.IsDevicePickerOpen = false;
			}

			if (ViewModel.CurrentState == NotchState.Collapsed)







			{







				StopWeatherAnimations();







				ExpandedContentGrid.Visibility = Visibility.Collapsed;

				_navPillTabIndex = -1;







				ExpandedContentGrid.BeginAnimation(UIElement.OpacityProperty, null);







				ExpandedContentGrid.Opacity = 0.0;







				if (ExpandedContentTranslate != null)







				{







					ExpandedContentTranslate.BeginAnimation(TranslateTransform.YProperty, null);







					ExpandedContentTranslate.Y = 0.0;







				}







				if (ExpandedContentScale != null)







				{







					ExpandedContentScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);







					ExpandedContentScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);







					ExpandedContentScale.ScaleX = 1.0;







					ExpandedContentScale.ScaleY = 1.0;







				}







			}







		};







		ExpandedContentGrid.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);







		if (ViewModel.Media.IsCollapsedLyricsActive && !ViewModel.Media.IsActiveLyricInterlude)







		{







			ResetCollapsedLyricText(ViewModel.Media.CollapsedDisplayLyricText, ViewModel.Media.CurrentLyricIndex);







		}







		DoubleAnimation animation9 = new DoubleAnimation







		{







			From = 0.0,







			To = 1.0,







			BeginTime = TimeSpan.FromMilliseconds(50.0),







			Duration = TimeSpan.FromMilliseconds(200.0),







			EasingFunction = easingFunction







		};







		CollapsedContentGrid.BeginAnimation(UIElement.OpacityProperty, animation9);







		if (CollapsedContentScale != null)







		{







			DoubleAnimation animation10 = new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(220.0))







			{







				BeginTime = TimeSpan.FromMilliseconds(50.0),







				EasingFunction = new BackEase







				{







					Amplitude = 0.2,







					EasingMode = EasingMode.EaseOut







				}







			};







			CollapsedContentScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation10);







			CollapsedContentScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation10);







		}







		if (CollapsedContentTranslate != null)







		{







			DoubleAnimation animation11 = new DoubleAnimation(-6.0, 0.0, TimeSpan.FromMilliseconds(220.0))







			{







				BeginTime = TimeSpan.FromMilliseconds(50.0),







				EasingFunction = easingFunction







			};







			CollapsedContentTranslate.BeginAnimation(TranslateTransform.YProperty, animation11);







		}







		if (PillScaleTransform == null)







		{







			return;







		}







		DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames = new DoubleAnimationUsingKeyFrames();







		DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames2 = new DoubleAnimationUsingKeyFrames();







		KeySpline keySpline = new KeySpline(0.2, 0.8, 0.4, 1.0);







		KeySpline keySpline2 = new KeySpline(0.2, 1.0, 0.3, 1.0);







		doubleAnimationUsingKeyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0.0))));







		doubleAnimationUsingKeyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(190.0))));







		doubleAnimationUsingKeyFrames.KeyFrames.Add(new SplineDoubleKeyFrame(0.94, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(265.0)), keySpline));







		doubleAnimationUsingKeyFrames.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(370.0)), keySpline2));







		doubleAnimationUsingKeyFrames2.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0.0))));







		doubleAnimationUsingKeyFrames2.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(190.0))));







		doubleAnimationUsingKeyFrames2.KeyFrames.Add(new SplineDoubleKeyFrame(1.015, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(265.0)), keySpline));







		doubleAnimationUsingKeyFrames2.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(370.0)), keySpline2));







		doubleAnimationUsingKeyFrames.Completed += (object? _, EventArgs _) =>







		{







			if (_notchToSlimMorphActive || _slimBarMorphToNotchActive)
			{
				Log.Debug("Collapse pill bounce completed: skipped reset because a slim-bar morph is active");
				return;
			}
			if (ViewModel.CurrentState == NotchState.Collapsed)







			{







				PillScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);







				PillScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);







				PillScaleTransform.ScaleY = 1.0;







				PillScaleTransform.ScaleX = 1.0;







			}







		};







		PillScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, doubleAnimationUsingKeyFrames);







		PillScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, doubleAnimationUsingKeyFrames2);







	}















	private void PillBorder_MouseEnter(object sender, MouseEventArgs e)







	{







		_wasCursorInsideNotch = true;







		ViewModel.OnMouseEnter();







	}

	private void DevicePickerPopup_Opened(object? sender, EventArgs e)
	{
		_devicePickerPopupHwnd = IntPtr.Zero;
		ViewModel?.CancelPendingCollapse();
		if (sender is not System.Windows.Controls.Primitives.Popup popup || popup.Child == null)
		{
			return;
		}

		if (PresentationSource.FromVisual(popup.Child) is HwndSource source && source.Handle != IntPtr.Zero)
		{
			_devicePickerPopupHwnd = source.Handle;
			WindowStyleHelper.EnsureTopmost(source.Handle);
		}
	}

	private void DevicePickerPopup_Closed(object? sender, EventArgs e)
	{
		_devicePickerPopupHwnd = IntPtr.Zero;
		if (!IsLoaded || ViewModel == null) return;
		if (!PillContainer.IsMouseOver && !IsCursorOverNotch())
		{
			ViewModel.OnMouseLeave();
		}
	}

	private Slider? _draggedSlider;

	private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
	{
		while (child != null)
		{
			if (child is T parent)
			{
				return parent;
			}
			child = VisualTreeHelper.GetParent(child);
		}
		return null;
	}

	private void SlimSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (ViewModel != null)
		{
			ViewModel.IsSliderDragging = true;
		}

		if (sender is Slider slider && slider.IsEnabled && (e.LeftButton == MouseButtonState.Pressed || e.ChangedButton == MouseButton.Left))
		{
			// If user clicked directly on the Thumb ("bulet"), let native WPF Thumb handle capture & dragging
			if (e.OriginalSource is DependencyObject dep && FindVisualParent<System.Windows.Controls.Primitives.Thumb>(dep) != null)
			{
				_draggedSlider = null;
				return;
			}

			// If clicked on the track bar outside the Thumb, jump to point and capture mouse for continuous dragging
			_draggedSlider = slider;
			slider.CaptureMouse();
			UpdateSlimSliderFromPosition(slider, e.GetPosition(slider).X);
		}
	}

	private void SlimSlider_PreviewMouseMove(object sender, MouseEventArgs e)
	{
		if (_draggedSlider != null && _draggedSlider.IsMouseCaptured)
		{
			UpdateSlimSliderFromPosition(_draggedSlider, e.GetPosition(_draggedSlider).X);
		}
	}

	private void SlimSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
	{
		if (_draggedSlider != null)
		{
			if (_draggedSlider.IsMouseCaptured)
			{
				_draggedSlider.ReleaseMouseCapture();
			}
			_draggedSlider = null;
		}

		if (ViewModel != null)
		{
			ViewModel.IsSliderDragging = false;
		}
	}

	private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
	{
		if (_draggedSlider != null && _draggedSlider.IsMouseCaptured)
		{
			UpdateSlimSliderFromPosition(_draggedSlider, e.GetPosition(_draggedSlider).X);
		}
	}

	private void Window_PreviewMouseUp(object sender, MouseButtonEventArgs e)
	{
		if (_draggedSlider != null)
		{
			if (_draggedSlider.IsMouseCaptured)
			{
				_draggedSlider.ReleaseMouseCapture();
			}
			_draggedSlider = null;
		}

		if (ViewModel != null && ViewModel.IsSliderDragging)
		{
			ViewModel.IsSliderDragging = false;
		}

		if (PillContainer != null && !PillContainer.IsMouseOver)
		{
			ViewModel?.OnMouseLeave();
		}
	}

	private static void UpdateSlimSliderFromPosition(Slider slider, double mouseX)
	{
		double width = slider.ActualWidth;
		if (width <= 0) return;
		double fraction = Math.Clamp(mouseX / width, 0.0, 1.0);
		double newValue = slider.Minimum + fraction * (slider.Maximum - slider.Minimum);
		int rounded = (int)Math.Round(newValue);
		if ((int)slider.Value != rounded)
		{
			Log.Information(">>> UpdateSlimSliderFromPosition: Value changed from {Old} to {New}", slider.Value, rounded);
			slider.Value = rounded;
		}
	}








	private void PillBorder_MouseLeave(object sender, MouseEventArgs e)







	{







		_wasCursorInsideNotch = false;







		if (!_isContextMenuOpen && !(ViewModel?.Media?.IsLyricsVersionPickerOpen ?? false) && !(ViewModel?.Volume?.IsDevicePickerOpen ?? false) && !(ViewModel?.IsSliderDragging ?? false))







		{







			ViewModel.OnMouseLeave();







		}







	}















	private void PillBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (ViewModel.CurrentState == NotchState.Collapsed)
		{
			if (ViewModel.IsCollapsedDownloadPhaseActive && ViewModel.Download?.HasActiveDownloads == true)
			{
				ViewModel.NavigateToDownloadsPanel();
				return;
			}

			if (ViewModel.TimerStopwatch.IsAnyTimerRunning && ViewModel.IsCollapsedTimerPhaseActive)
			{
				ViewModel.TimerStopwatch.IsTimerMode = !ViewModel.IsCollapsedStopwatchDisplay;
				ViewModel.NavigateToTimerSlide();
				return;
			}

			if (ViewModel.WidgetHost.CurrentWidgetType == WidgetType.Timer)
			{
				ViewModel.TimerStopwatch.IsTimerMode = ViewModel.TimerStopwatch.ShowTimerInCollapsed;
				ViewModel.NavigateToTimerSlide();
				return;
			}

			ViewModel.OnPillClicked();
		}
	}

	private void AnimateCollapsedTimerAlternation(bool toTimerPhase)
	{
		if (ViewModel?.CurrentState != NotchState.Collapsed || MediaLeftNormalSlot == null)
		{
			ResetCollapsedTimerSlotStates(toTimerPhase);
			return;
		}

		if (ViewModel?.TimerStopwatch?.IsAnyTimerRunning != true)
		{
			ResetCollapsedTimerSlotStates(false);
			return;
		}

		var springEase = new BackEase
		{
			EasingMode = EasingMode.EaseOut,
			Amplitude = 0.35
		};

		var fadeEase = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};

		var outEase = new CubicEase
		{
			EasingMode = EasingMode.EaseIn
		};

		var outDuration = TimeSpan.FromMilliseconds(180);
		var inDuration = TimeSpan.FromMilliseconds(420);
		var inFadeDuration = TimeSpan.FromMilliseconds(240);
		var inBeginTime = TimeSpan.FromMilliseconds(40);

		FrameworkElement[] outgoingSlots;
		TranslateTransform[] outgoingTranslates;
		ScaleTransform[] outgoingScales;

		FrameworkElement[] incomingSlots;
		TranslateTransform[] incomingTranslates;
		ScaleTransform[] incomingScales;

		if (toTimerPhase)
		{
			outgoingSlots = new FrameworkElement[] { MediaLeftNormalSlot, MediaRightNormalSlot, ClockLeftNormalSlot, ClockRightNormalSlot };
			outgoingTranslates = new TranslateTransform[] { MediaLeftNormalTranslate, MediaRightNormalTranslate, ClockLeftNormalTranslate, ClockRightNormalTranslate };
			outgoingScales = new ScaleTransform[] { MediaLeftNormalScale, MediaRightNormalScale, ClockLeftNormalScale, ClockRightNormalScale };

			incomingSlots = new FrameworkElement[] { MediaLeftTimerSlot, MediaRightTimerSlot, ClockLeftTimerSlot, ClockRightTimerSlot };
			incomingTranslates = new TranslateTransform[] { MediaLeftTimerTranslate, MediaRightTimerTranslate, ClockLeftTimerTranslate, ClockRightTimerTranslate };
			incomingScales = new ScaleTransform[] { MediaLeftTimerScale, MediaRightTimerScale, ClockLeftTimerScale, ClockRightTimerScale };
		}
		else
		{
			outgoingSlots = new FrameworkElement[] { MediaLeftTimerSlot, MediaRightTimerSlot, ClockLeftTimerSlot, ClockRightTimerSlot };
			outgoingTranslates = new TranslateTransform[] { MediaLeftTimerTranslate, MediaRightTimerTranslate, ClockLeftTimerTranslate, ClockRightTimerTranslate };
			outgoingScales = new ScaleTransform[] { MediaLeftTimerScale, MediaRightTimerScale, ClockLeftTimerScale, ClockRightTimerScale };

			incomingSlots = new FrameworkElement[] { MediaLeftNormalSlot, MediaRightNormalSlot, ClockLeftNormalSlot, ClockRightNormalSlot };
			incomingTranslates = new TranslateTransform[] { MediaLeftNormalTranslate, MediaRightNormalTranslate, ClockLeftNormalTranslate, ClockRightNormalTranslate };
			incomingScales = new ScaleTransform[] { MediaLeftNormalScale, MediaRightNormalScale, ClockLeftNormalScale, ClockRightNormalScale };
		}

		for (int i = 0; i < outgoingSlots.Length; i++)
		{
			var slot = outgoingSlots[i];
			var trans = outgoingTranslates[i];
			var scale = outgoingScales[i];

			var fadeOut = new DoubleAnimation(0.0, outDuration) { EasingFunction = outEase };
			var slideOut = new DoubleAnimation(-6.0, outDuration) { EasingFunction = outEase };
			var scaleOut = new DoubleAnimation(0.85, outDuration) { EasingFunction = outEase };

			slot.BeginAnimation(UIElement.OpacityProperty, fadeOut);
			trans.BeginAnimation(TranslateTransform.YProperty, slideOut);
			scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
			scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOut);
		}

		for (int i = 0; i < incomingSlots.Length; i++)
		{
			var slot = incomingSlots[i];
			var trans = incomingTranslates[i];
			var scale = incomingScales[i];

			trans.Y = 6.0;
			scale.ScaleX = 0.85;
			scale.ScaleY = 0.85;
			slot.Opacity = 0.0;

			var fadeIn = new DoubleAnimation(0.0, 1.0, inFadeDuration) { BeginTime = inBeginTime, EasingFunction = fadeEase };
			var springIn = new DoubleAnimation(6.0, 0.0, inDuration) { BeginTime = inBeginTime, EasingFunction = springEase };
			var scaleIn = new DoubleAnimation(0.85, 1.0, inDuration) { BeginTime = inBeginTime, EasingFunction = springEase };

			slot.BeginAnimation(UIElement.OpacityProperty, fadeIn);
			trans.BeginAnimation(TranslateTransform.YProperty, springIn);
			scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
			scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);
		}
	}

	private void AnimateCollapsedTimerSubSwap()
	{
		if (ViewModel?.CurrentState != NotchState.Collapsed || MediaLeftTimerSlot == null) return;
		if (ViewModel.IsCollapsedTimerPhaseActive != true) return;

		var springEase = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };
		var fadeEase = new CubicEase { EasingMode = EasingMode.EaseOut };
		var inDuration = TimeSpan.FromMilliseconds(420);
		var inFadeDuration = TimeSpan.FromMilliseconds(240);

		FrameworkElement[] timerSlots = { MediaLeftTimerSlot, MediaRightTimerSlot, ClockLeftTimerSlot, ClockRightTimerSlot };
		TranslateTransform[] timerTranslates = { MediaLeftTimerTranslate, MediaRightTimerTranslate, ClockLeftTimerTranslate, ClockRightTimerTranslate };
		ScaleTransform[] timerScales = { MediaLeftTimerScale, MediaRightTimerScale, ClockLeftTimerScale, ClockRightTimerScale };

		for (int i = 0; i < timerSlots.Length; i++)
		{
			var slot = timerSlots[i];
			var trans = timerTranslates[i];
			var scale = timerScales[i];

			trans.Y = 6.0;
			scale.ScaleX = 0.85;
			scale.ScaleY = 0.85;
			slot.Opacity = 0.3;

			slot.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.3, 1.0, inFadeDuration) { EasingFunction = fadeEase });
			trans.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(6.0, 0.0, inDuration) { EasingFunction = springEase });
			scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.85, 1.0, inDuration) { EasingFunction = springEase });
			scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.85, 1.0, inDuration) { EasingFunction = springEase });
		}
	}

	private void ResetCollapsedTimerSlotStates(bool isTimerPhase)
	{
		if (MediaLeftNormalSlot == null) return;

		FrameworkElement[] normalSlots = { MediaLeftNormalSlot, MediaRightNormalSlot, ClockLeftNormalSlot, ClockRightNormalSlot };
		TranslateTransform[] normalTranslates = { MediaLeftNormalTranslate, MediaRightNormalTranslate, ClockLeftNormalTranslate, ClockRightNormalTranslate };
		ScaleTransform[] normalScales = { MediaLeftNormalScale, MediaRightNormalScale, ClockLeftNormalScale, ClockRightNormalScale };

		FrameworkElement[] timerSlots = { MediaLeftTimerSlot, MediaRightTimerSlot, ClockLeftTimerSlot, ClockRightTimerSlot };
		TranslateTransform[] timerTranslates = { MediaLeftTimerTranslate, MediaRightTimerTranslate, ClockLeftTimerTranslate, ClockRightTimerTranslate };
		ScaleTransform[] timerScales = { MediaLeftTimerScale, MediaRightTimerScale, ClockLeftTimerScale, ClockRightTimerScale };

		for (int i = 0; i < normalSlots.Length; i++)
		{
			normalSlots[i].BeginAnimation(UIElement.OpacityProperty, null);
			normalTranslates[i].BeginAnimation(TranslateTransform.YProperty, null);
			normalScales[i].BeginAnimation(ScaleTransform.ScaleXProperty, null);
			normalScales[i].BeginAnimation(ScaleTransform.ScaleYProperty, null);

			normalSlots[i].Opacity = isTimerPhase ? 0.0 : 1.0;
			normalTranslates[i].Y = isTimerPhase ? -6.0 : 0.0;
			normalScales[i].ScaleX = isTimerPhase ? 0.85 : 1.0;
			normalScales[i].ScaleY = isTimerPhase ? 0.85 : 1.0;
		}

		for (int i = 0; i < timerSlots.Length; i++)
		{
			timerSlots[i].BeginAnimation(UIElement.OpacityProperty, null);
			timerTranslates[i].BeginAnimation(TranslateTransform.YProperty, null);
			timerScales[i].BeginAnimation(ScaleTransform.ScaleXProperty, null);
			timerScales[i].BeginAnimation(ScaleTransform.ScaleYProperty, null);

			timerSlots[i].Opacity = isTimerPhase ? 1.0 : 0.0;
			timerTranslates[i].Y = isTimerPhase ? 0.0 : 6.0;
			timerScales[i].ScaleX = isTimerPhase ? 1.0 : 0.85;
			timerScales[i].ScaleY = isTimerPhase ? 1.0 : 0.85;
		}
	}















	private void PillBorder_MouseRightButtonDown(object sender, MouseButtonEventArgs e)







	{







		e.Handled = true;







		if (ViewModel.CurrentState == NotchState.Collapsed)







		{







			ShowSettingsWindow();







		}







	}















	private static ScrollViewer? FindParentScrollViewer(DependencyObject? child)
	{
		while (child != null)
		{
			if (child is ScrollViewer sv)
			{
				return sv;
			}
			child = VisualTreeHelper.GetParent(child);
		}
		return null;
	}

	private void ExpandedContentGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (ViewModel.CurrentState != NotchState.Expanded || ViewModel.Media.IsSeeking)
		{
			return;
		}

		if (e.OriginalSource is DependencyObject source)
		{
			ScrollViewer? sv = FindParentScrollViewer(source);
			if (sv != null)
			{
				if (sv.ScrollableHeight > 0.5)
				{
					sv.ScrollToVerticalOffset(sv.VerticalOffset - (e.Delta * 0.4));
					e.Handled = true;
					return;
				}
				if (sv.ScrollableWidth > 0.5)
				{
					if (e.Delta < 0)
					{
						sv.LineRight();
						sv.LineRight();
					}
					else
					{
						sv.LineLeft();
						sv.LineLeft();
					}
					e.Handled = true;
					return;
				}
			}
		}

		DateTime utcNow = DateTime.UtcNow;
		if ((utcNow - _lastSlideWheelTime).TotalMilliseconds < 270.0)
		{
			e.Handled = true;
			return;
		}
		_accumulatedWheelDelta += e.Delta;
		if (Math.Abs(_accumulatedWheelDelta) >= 80)
		{
			_lastSlideWheelTime = utcNow;
			int accumulatedWheelDelta = _accumulatedWheelDelta;
			_accumulatedWheelDelta = 0;
			if (accumulatedWheelDelta < 0)
			{
				ViewModel.NextSlide();
			}
			else
			{
				ViewModel.PrevSlide();
			}
			e.Handled = true;
		}
	}















	private void PillContainer_MouseWheel(object sender, MouseWheelEventArgs e)







	{







		ExpandedContentGrid_PreviewMouseWheel(sender, e);







	}















	private void Window_KeyDown(object sender, KeyEventArgs e)







	{







		if (e.Key == Key.Escape)







		{







			// Esc saat mode fullscreen/slim/peek adalah milik aplikasi fullscreen







			// (biasanya untuk keluar fullscreen). NotchWin tidak boleh ikut menutup diri.







			if (_isSlimBarMode || ViewModel?.IsFullscreenActive == true || ViewModel?.IsSlimBarPeekActive == true)







			{







				if (ViewModel?.IsSlimBarPeekActive == true)







				{







					ViewModel.CloseSlimBarPeek();







				}







				e.Handled = true;







				return;







			}







			// Abaikan Esc yang tidak ditujukan ke notch (fokus dapat berpindah







			// sesaat ketika aplikasi lain keluar dari fullscreen).







			if (!IsActive)







			{







				return;







			}







			Log.Information("Escape key pressed -> Closing NotchWin");







			Close();







		}







	}















	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(NotchViewModel.IsCollapsedTimerPhaseActive))
		{
			Dispatcher.Invoke(() =>
			{
				if (ViewModel != null)
				{
					AnimateCollapsedTimerAlternation(ViewModel.IsCollapsedTimerPhaseActive);
				}
			});
		}
		else if (e.PropertyName == nameof(NotchViewModel.IsCollapsedDownloadPhaseActive))
		{
			Dispatcher.Invoke(() =>
			{
				if (ViewModel != null)
				{
					AnimateCollapsedDownloadAlternation(ViewModel.IsCollapsedDownloadPhaseActive);
				}
			});
		}
		else if (e.PropertyName == nameof(NotchViewModel.IsCollapsedStopwatchDisplay))
		{
			Dispatcher.Invoke(() =>
			{
				AnimateCollapsedTimerSubSwap();
			});
		}
		else if (e.PropertyName == nameof(NotchViewModel.CurrentState))
		{
			Dispatcher.Invoke(() =>
			{
				if (ViewModel?.CurrentState != NotchState.Collapsed)
				{
					ResetCollapsedTimerSlotStates(false);
				}
				AnimateTimerSubNotch(ViewModel?.TimerStopwatch?.IsTimerRunning ?? false);
				AnimateStopwatchSubNotch(ViewModel?.TimerStopwatch?.IsStopwatchRunning ?? false);
			});
		}

		if (e.PropertyName == nameof(NotchViewModel.ShowWeatherInCollapsedNotch) ||
		    e.PropertyName == nameof(NotchViewModel.ShowBatteryInCollapsedNotch) ||
		    e.PropertyName == nameof(NotchViewModel.ShowPetInCollapsedNotch) ||
		    e.PropertyName == nameof(NotchViewModel.IsPetEnabled))
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
			{
				if (ViewModel?.Media.IsCollapsedLyricsActive == true)
				{
					UpdateCollapsedLyricsWidth(ViewModel?.Media.CollapsedDisplayLyricText);
				}
			}));
		}

		if (e.PropertyName == "IsPrayerEnabled" || e.PropertyName == "TotalActiveSlides" || e.PropertyName == "ActiveModuleIds")
		{
			Dispatcher.Invoke(() =>
			{
				SyncNavbarModuleConfiguration();
			});
		}

		if (e.PropertyName == nameof(NotchViewModel.IsPrivacyActive))







		{







			Dispatcher.InvokeAsync(() =>







			{







				bool isActive = ViewModel.IsPrivacyActive;







				if (_privacyOverlayActive == isActive) return;







				_privacyOverlayActive = isActive;







				AnimatePrivacyIndicators(isActive);







			});







		}







	}







	private void AnimatePrivacyIndicators(bool show)







	{







		AnimatePrivacyOverlay(PrivacyOverlayHost, PrivacyOverlayTranslate, show);







		AnimatePrivacyOverlay(ExpandedPrivacyOverlayHost, ExpandedPrivacyOverlayTranslate, show);







	}















	private void AnimatePrivacyOverlay(FrameworkElement? host, TranslateTransform? translate, bool show)







	{







		if (host == null || translate == null) return;















		if (show)







		{







			host.Visibility = Visibility.Visible;







			host.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation







			{







				From = 0.0,







				To = 1.0,







				Duration = TimeSpan.FromMilliseconds(160.0)







			});







			translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation







			{







				From = -10.0,







				To = 0.0,







				Duration = TimeSpan.FromMilliseconds(340.0),







				EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }







			});







		}







		else







		{







			DoubleAnimation fadeOut = new DoubleAnimation







			{







				From = 1.0,







				To = 0.0,







				Duration = TimeSpan.FromMilliseconds(220.0),







				EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }







			};







			fadeOut.Completed += (s2, e2) =>







			{







				if (!ViewModel.IsPrivacyActive)







				{







					host.BeginAnimation(UIElement.OpacityProperty, null);







					host.Opacity = 0.0;







					translate.BeginAnimation(TranslateTransform.YProperty, null);







					translate.Y = 0.0;







					host.Visibility = Visibility.Collapsed;







				}







			};







			host.BeginAnimation(UIElement.OpacityProperty, fadeOut);







		}







	}















	private FrameworkElement?[] GetAllPossibleCards()







	{







		return new FrameworkElement[11] { SlideHomeCard, SlideControlCenterCard, SlideMediaCard, SlideWeatherCard, SlidePrayerCard, SlideWaterCard, SlideNoteCard, SlideClipboardCard, SlideTimerCard, SlideShelfCard, SlideScreenTimeCard };







	}















	private FrameworkElement? GetCardForModuleId(string moduleId) => moduleId switch
	{
		"home" => SlideHomeCard,
		"control" => SlideControlCenterCard,
		"media" => SlideMediaCard,
		"weather" => SlideWeatherCard,
		"prayer" => SlidePrayerCard,
		"water" => SlideWaterCard,
		"note" => SlideNoteCard,
		"clipboard" => SlideClipboardCard,
		"timer" => SlideTimerCard,
		"shelf" => SlideShelfCard,
		"screentime" => SlideScreenTimeCard,
		_ => null
	};

	private FrameworkElement?[] GetCards()
	{
		if (ViewModel == null || ViewModel.ActiveModuleIds == null || ViewModel.ActiveModuleIds.Count == 0)
		{
			return GetAllPossibleCards();
		}

		var list = new System.Collections.Generic.List<FrameworkElement>();
		foreach (var id in ViewModel.ActiveModuleIds)
		{
			var card = GetCardForModuleId(id);
			if (card != null)
			{
				list.Add(card);
			}
		}
		return list.ToArray();
	}















	public void ResetSlideViews(int activeIndex = 0, bool updateNavPill = true)







	{







		_displayedSlideIndex = activeIndex;
		if (ViewModel != null && ViewModel.IsScreenTimeActive)
		{
			ViewModel.ScreenTime?.Refresh();
		}







		FrameworkElement[] cards = GetCards();







		FrameworkElement frameworkElement = ((activeIndex >= 0 && activeIndex < cards.Length) ? cards[activeIndex] : null);







		FrameworkElement[] allPossibleCards = GetAllPossibleCards();







		foreach (FrameworkElement frameworkElement2 in allPossibleCards)







		{







			if (frameworkElement2 != null)







			{







				frameworkElement2.BeginAnimation(UIElement.OpacityProperty, null);







				var (translateTransform, scaleTransform) = EnsureCardTransforms(frameworkElement2);







				translateTransform.BeginAnimation(TranslateTransform.XProperty, null);







				translateTransform.X = 0.0;







				scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);







				scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);







				scaleTransform.ScaleX = 1.0;







				scaleTransform.ScaleY = 1.0;







				if (frameworkElement2 == frameworkElement)







				{







					frameworkElement2.Visibility = Visibility.Visible;







					frameworkElement2.Opacity = 1.0;







				}







				else







				{







					frameworkElement2.Visibility = Visibility.Collapsed;







					frameworkElement2.Opacity = 0.0;







				}







			}







		}







		if (activeIndex == 2)







		{







			StartWeatherAnimations();







		}







		else







		{







			StopWeatherAnimations();







		}







		if (MediaPlayerContainer != null && LyricsContainer != null && LyricsDivider != null)







		{







			bool isLyricsExpanded = ViewModel.Media.IsLyricsExpanded;







			if (MediaHubGrid != null)







			{







				MediaHubGrid.BeginAnimation(FrameworkElement.WidthProperty, null);







				MediaHubGrid.Width = (isLyricsExpanded ? 584.0 : 358.0);







			}







			MediaPlayerContainer.BeginAnimation(FrameworkElement.WidthProperty, null);







			MediaPlayerContainer.Width = (isLyricsExpanded ? 215.0 : 358.0);







			LyricsContainer.BeginAnimation(UIElement.OpacityProperty, null);







			LyricsContainer.Opacity = (isLyricsExpanded ? 1.0 : 0.0);







			if (!isLyricsExpanded)
			{
				ViewModel.Media.IsLyricsVersionPickerOpen = false;
				if (LyricsContainer != null)
				{
					LyricsContainer.BeginAnimation(FrameworkElement.HeightProperty, null);
					LyricsContainer.Height = 150.0;
				}
		if (ViewModel?.Volume != null)
		{
			ViewModel.Volume.IsDevicePickerOpen = false;
		}
			}







			LyricsContainer.Visibility = ((!isLyricsExpanded) ? Visibility.Collapsed : Visibility.Visible);







			LyricsContainer.IsHitTestVisible = isLyricsExpanded;







			if (LyricsContainerTranslate != null)







			{







				LyricsContainerTranslate.BeginAnimation(TranslateTransform.XProperty, null);







				LyricsContainerTranslate.X = (isLyricsExpanded ? 0.0 : 60.0);







			}







			LyricsDivider.BeginAnimation(UIElement.OpacityProperty, null);







			LyricsDivider.Opacity = (isLyricsExpanded ? 1.0 : 0.0);







		}







		if (updateNavPill)
		{
			UpdateNavPill(activeIndex, animate: false);
		}







	}















	private void OnSlideChanged(object? sender, int newIndex)







	{







		Dispatcher.Invoke(() =>







		{







			if (ViewModel.CurrentState == NotchState.Collapsed)







			{







				_displayedSlideIndex = newIndex;







				ResetSlideViews(newIndex);







			}







			else







			{







				int displayedSlideIndex = _displayedSlideIndex;







				if (displayedSlideIndex != newIndex)







				{







					_displayedSlideIndex = newIndex;







					AnimateSlideTransition(displayedSlideIndex, newIndex);







				}







			}







		});







	}















	private void AnimateLiquidDot(int fromIndex, int toIndex)







	{







		UpdateNavPill(toIndex);







	}















	private static QuinticEase CreateNavPillGlideEase()
	{
		QuinticEase quinticEase = new QuinticEase
		{
			EasingMode = EasingMode.EaseInOut
		};
		quinticEase.Freeze();
		return quinticEase;
	}

	private static CubicEase CreateNavDockEase()
	{
		CubicEase cubicEase = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		cubicEase.Freeze();
		return cubicEase;
	}

	private static SolidColorBrush CreateFrozenBrush(Color color)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(color);
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	private readonly System.Collections.Generic.Dictionary<string, (Button Button, TextBlock Label, Shape Icon, FrameworkElement Content, Ellipse Dot)> _moduleNavMap = new();

	private void InitNavModuleMap()
	{
		if (_moduleNavMap.Count > 0)
		{
			return;
		}
		_moduleNavMap["home"] = (NavTab0, NavLabel0, NavIcon0, NavGroup0, NavDot0);
		_moduleNavMap["control"] = (NavTab1, NavLabel1, NavIcon1, NavGroup1, NavDot1);
		_moduleNavMap["media"] = (NavTab2, NavLabel2, NavIcon2, NavGroup2, NavDot2);
		_moduleNavMap["weather"] = (NavTab3, NavLabel3, NavIcon3, NavGroup3, NavDot3);
		_moduleNavMap["prayer"] = (NavTab4, NavLabel4, NavIcon4, NavGroup4, NavDot4);
		_moduleNavMap["water"] = (NavTab5, NavLabel5, NavIcon5, NavGroup5, NavDot5);
		_moduleNavMap["note"] = (NavTab6, NavLabel6, NavIcon6, NavGroup6, NavDot6);
		_moduleNavMap["clipboard"] = (NavTab7, NavLabel7, NavIcon7, NavGroup7, NavDot7);
		_moduleNavMap["timer"] = (NavTab8, NavLabel8, NavIcon8, NavGroup8, NavDot8);
		_moduleNavMap["shelf"] = (NavTab9, NavLabel9, NavIcon9, NavGroup9, NavDot9);
		_moduleNavMap["screentime"] = (NavTab10, NavLabel10, NavIcon10, NavGroup10, NavDot10);
	}

	public void SyncNavbarModuleConfiguration()
	{
		InitNavModuleMap();
		if (NavTabsPanel == null)
		{
			return;
		}

		var activeIds = ViewModel?.ActiveModuleIds;
		if (activeIds == null || activeIds.Count == 0)
		{
			activeIds = new string[11] { "home", "control", "media", "weather", "prayer", "water", "note", "clipboard", "timer", "shelf", "screentime" };
		}

		NavTabsPanel.Children.Clear();

		var activeButtons = new System.Collections.Generic.List<Button>();
		var activeLabels = new System.Collections.Generic.List<TextBlock>();
		var activeIcons = new System.Collections.Generic.List<Shape>();
		var activeContents = new System.Collections.Generic.List<FrameworkElement>();
		var activeDots = new System.Collections.Generic.List<Ellipse>();

		for (int i = 0; i < activeIds.Count; i++)
		{
			string id = activeIds[i];
			if (_moduleNavMap.TryGetValue(id, out var items))
			{
				items.Button.Visibility = Visibility.Visible;
				items.Button.CommandParameter = id;
				items.Button.Margin = (i == activeIds.Count - 1) ? new Thickness(0) : new Thickness(0, 0, 2, 0);

				NavTabsPanel.Children.Add(items.Button);

				activeButtons.Add(items.Button);
				activeLabels.Add(items.Label);
				activeIcons.Add(items.Icon);
				activeContents.Add(items.Content);
				activeDots.Add(items.Dot);
			}
		}

		// Hide any disabled buttons
		foreach (var kvp in _moduleNavMap)
		{
			if (!activeIds.Contains(kvp.Key))
			{
				kvp.Value.Button.Visibility = Visibility.Collapsed;
			}
		}

		_navTabs = activeButtons.ToArray();
		_navLabels = activeLabels.ToArray();
		_navIcons = activeIcons.ToArray();
		_navContents = activeContents.ToArray();
		_navDots = activeDots.ToArray();

		if (ViewModel != null && ViewModel.CurrentSlideIndex >= _navTabs.Length)
		{
			ViewModel.CurrentSlideIndex = 0;
		}

		int activeIdx = ViewModel?.CurrentSlideIndex ?? 0;
		ResetSlideViews(activeIdx, updateNavPill: true);
		UpdateNavPill(activeIdx, animate: false);
	}

	private void EnsureNavPillComponents()
	{
		if (_navTabs != null && _navLabels != null && _navIcons != null && _navContents != null && _navDots != null)
		{
			return;
		}
		SyncNavbarModuleConfiguration();
	}

	private void OnShelfItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		ScheduleShelfContainerSync();
		if (!_navDockExpanded || _navDockAnimating || ViewModel == null)
		{
			return;
		}
		EnsureNavFullWidthsMeasured();
		ApplyNavDockExpandedState();
		UpdateNavPill(ViewModel.CurrentSlideIndex, animate: true);
	}

	private bool _shelfContainerSyncScheduled;

	/// <summary>
	/// WPF kadang menyisakan ContentPresenter tambahan di items host ketika collection berubah
	/// sambil kontrol beralih visibilitas, sehingga satu file tampil dua kali. Setelah layout,
	/// jumlah container dibandingkan dengan jumlah item; bila tidak sama, generator di-reset.
	/// </summary>
	private void ScheduleShelfContainerSync()
	{
		if (_shelfContainerSyncScheduled || ShelfItemsControl == null)
		{
			return;
		}

		_shelfContainerSyncScheduled = true;
		Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
		{
			_shelfContainerSyncScheduled = false;
			try
			{
				Panel? host = FindShelfItemsHost();
				if (host == null || host.Children.Count == ShelfItemsControl.Items.Count)
				{
					return;
				}

				Log.Information("Shelf container desync: {Containers} containers for {Items} items, resyncing",
					host.Children.Count, ShelfItemsControl.Items.Count);
				ShelfItemsControl.Items.Refresh();
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Shelf container resync skipped");
			}
		}));
	}

	private Panel? FindShelfItemsHost()
	{
		if (ShelfItemsControl == null)
		{
			return null;
		}

		var pending = new Stack<DependencyObject>();
		pending.Push(ShelfItemsControl);
		while (pending.Count > 0)
		{
			DependencyObject current = pending.Pop();
			if (current is Panel panel && panel.IsItemsHost)
			{
				return panel;
			}

			int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(current);
			for (int i = 0; i < count; i++)
			{
				pending.Push(System.Windows.Media.VisualTreeHelper.GetChild(current, i));
			}
		}

		return null;
	}

	public void InvalidateNavFullWidths()
	{
		if (!_navDockExpanded || _navDockAnimating || ViewModel == null)
		{
			return;
		}
		EnsureNavFullWidthsMeasured();
		ApplyNavDockExpandedState();
		UpdateNavPill(ViewModel.CurrentSlideIndex, animate: false);
	}

	private void StartNavDockCloseTimer()
	{
		if (_navDockCloseTimer == null)
		{
			_navDockCloseTimer = new DispatcherTimer(DispatcherPriority.Input)
			{
				Interval = NavDockCloseGrace
			};
			_navDockCloseTimer.Tick += NavDockCloseTimer_Tick;
		}
		_navDockCloseTimer.Stop();
		_navDockCloseTimer.Start();
	}

	private void StopNavDockCloseTimer()
	{
		_navDockCloseTimer?.Stop();
	}

	private void NavDockCloseTimer_Tick(object? sender, EventArgs e)
	{
		StopNavDockCloseTimer();
		if (NavDockHost != null && NavDockHost.IsMouseOver)
		{
			return;
		}
		CollapseNavDock(animate: true);
	}

	private void ExpandNavDock(bool animate)
	{
		_navDockExpanded = true;
		_navDockAnimating = false;
		StopNavDockCloseTimer();
		EnsureNavPillComponents();
		if (ViewModel != null)
		{
			UpdateNavPill(ViewModel.CurrentSlideIndex, animate);
		}
	}

	private void FinishNavDockExpand()
	{
		_navDockAnimating = false;
		if (ViewModel != null)
		{
			UpdateNavPill(ViewModel.CurrentSlideIndex, animate: false);
		}
		if (NavActivePill != null && NavActivePill.Opacity < 1.0)
		{
			NavActivePill.Opacity = 1.0;
		}
	}

	private bool AnimateNavPillExpand()
	{
		return false;
	}

	private void AnimateNavPillCollapse()
	{
	}

	private void StartNavPillShapeAnimation(double fromX, double fromWidth, double targetX, double targetWidth, Duration duration)
	{
		NavActivePillTranslate.BeginAnimation(TranslateTransform.XProperty, null);
		NavActivePill.BeginAnimation(FrameworkElement.WidthProperty, null);
		_navPillAnimX = null;
		_navPillAnimWidth = null;
		NavActivePillTranslate.X = targetX;
		NavActivePill.Width = targetWidth;
		DoubleAnimation xAnimation = new DoubleAnimation
		{
			From = fromX,
			To = targetX,
			Duration = duration,
			EasingFunction = NavDockEase,
			FillBehavior = FillBehavior.Stop
		};
		DoubleAnimation widthAnimation = new DoubleAnimation
		{
			From = fromWidth,
			To = targetWidth,
			Duration = duration,
			EasingFunction = NavDockEase,
			FillBehavior = FillBehavior.Stop
		};
		NavActivePillTranslate.BeginAnimation(TranslateTransform.XProperty, xAnimation);
		NavActivePill.BeginAnimation(FrameworkElement.WidthProperty, widthAnimation);
	}

	private void CollapseNavDock(bool animate)
	{
		_navDockExpanded = true;
		StopNavDockCloseTimer();
		EnsureNavPillComponents();
		_navDockAnimating = false;
		if (ViewModel != null)
		{
			UpdateNavPill(ViewModel.CurrentSlideIndex, animate);
		}
	}

	private void ResetNavDockState()
	{
		_navDockExpanded = true;
		StopNavDockCloseTimer();
		EnsureNavPillComponents();
		_navDockAnimating = false;
		if (ViewModel != null)
		{
			UpdateNavPill(ViewModel.CurrentSlideIndex, animate: false);
		}
	}

	private void ApplyNavDockCompactState()
	{
		if (ViewModel != null)
		{
			UpdateNavPill(ViewModel.CurrentSlideIndex, animate: false);
		}
	}

	private void ApplyNavDockExpandedState()
	{
		if (ViewModel != null)
		{
			UpdateNavPill(ViewModel.CurrentSlideIndex, animate: false);
		}
	}

	private void EnsureNavFullWidthsMeasured()
	{
		EnsureNavPillComponents();
	}

	private void AnimateNavDockContents(bool expand)
	{
	}

	private void AnimateNavDockWidths(bool expand, Action onCompleted)
	{
		onCompleted?.Invoke();
	}

	private static void StartNavButtonWidthAnimation(Button button, double targetWidth, Duration duration, Action onCompleted)
	{
		onCompleted?.Invoke();
	}

	private static void AnimateNavOpacity(UIElement element, double targetOpacity, Duration duration, TimeSpan beginTime)
	{
		if (element != null) element.Opacity = targetOpacity;
	}

	private static void SetNavOpacity(UIElement element, double opacity)
	{
		if (element != null)
		{
			element.BeginAnimation(UIElement.OpacityProperty, null);
			element.Opacity = opacity;
		}
	}

	private int MapSlideToPhysicalTabIndex(int slideIndex)
	{
		if (_navTabs == null || _navTabs.Length == 0)
		{
			return Math.Clamp(slideIndex, 0, 9);
		}
		return Math.Clamp(slideIndex, 0, _navTabs.Length - 1);
	}

	private const double NavTabInactiveWidth = 28.0;
	private const double NavTabActiveWidth = 84.0;
	private static readonly Duration NavTabTransitionDuration = new Duration(TimeSpan.FromMilliseconds(170.0));
	private static readonly CubicEase NavTabEase = new CubicEase { EasingMode = EasingMode.EaseOut };

	private void ApplyNavTabVisualState(int physicalIndex, int prevPhysicalIndex = -1, bool animate = false)
	{
		Brush accent = (Brush)FindResource("AccentLightBrush");
		bool shouldAnimate = animate && prevPhysicalIndex >= 0 && prevPhysicalIndex < _navTabs!.Length && prevPhysicalIndex != physicalIndex;

		for (int i = 0; i < _navTabs!.Length; i++)
		{
			bool isActive = i == physicalIndex;

			Button button = _navTabs[i];
			if (button != null)
			{
				if (isActive)
				{
					button.Tag = "Active";

					if (shouldAnimate)
					{
						DoubleAnimation expandAnim = new DoubleAnimation
						{
							To = NavTabActiveWidth,
							Duration = NavTabTransitionDuration,
							EasingFunction = NavTabEase
						};
						button.BeginAnimation(FrameworkElement.WidthProperty, expandAnim);
					}
					else
					{
						button.BeginAnimation(FrameworkElement.WidthProperty, null);
						button.Width = NavTabActiveWidth;
					}
				}
				else
				{
					button.Tag = null;

					if (shouldAnimate && i == prevPhysicalIndex)
					{
						DoubleAnimation shrinkAnim = new DoubleAnimation
						{
							To = NavTabInactiveWidth,
							Duration = NavTabTransitionDuration,
							EasingFunction = NavTabEase
						};
						button.BeginAnimation(FrameworkElement.WidthProperty, shrinkAnim);
					}
					else
					{
						button.BeginAnimation(FrameworkElement.WidthProperty, null);
						button.Width = NavTabInactiveWidth;
					}
				}
			}

			if (_navContents != null && i < _navContents.Length)
			{
				FrameworkElement content = _navContents[i];
				if (content != null)
				{
					content.Opacity = 1.0;
					content.Visibility = Visibility.Visible;
				}
			}

			if (_navDots != null && i < _navDots.Length)
			{
				Ellipse dot = _navDots[i];
				if (dot != null)
				{
					dot.Visibility = Visibility.Collapsed;
				}
			}

			Shape icon = _navIcons![i];
			if (icon != null)
			{
				if (icon == NavIcon0)
				{
					Brush iconFill = isActive ? NavTextActiveBrush : NavIconIdleBrush;
					if (icon.Fill != iconFill)
					{
						icon.Fill = iconFill;
					}
					if (icon.Stroke != null)
					{
						icon.Stroke = null;
					}
				}
				else
				{
					Brush iconBrush = (icon == NavIcon5 && isActive) ? NavIconWaterBrush : (isActive ? accent : NavIconIdleBrush);
					if (icon.Stroke != iconBrush)
					{
						icon.Stroke = iconBrush;
					}
					if (icon.Fill != null)
					{
						icon.Fill = null;
					}
				}
			}

			if (_navLabels != null && i < _navLabels.Length)
			{
				TextBlock label = _navLabels[i];
				if (label != null)
				{
					if (isActive)
					{
						label.Visibility = Visibility.Visible;
						if (label.Foreground != NavTextActiveBrush)
						{
							label.Foreground = NavTextActiveBrush;
						}
						FontWeight targetWeight = FontWeights.SemiBold;
						if (label.FontWeight != targetWeight)
						{
							label.FontWeight = targetWeight;
						}

						if (shouldAnimate)
						{
							DoubleAnimation fadeAnim = new DoubleAnimation
							{
								From = 0.0,
								To = 1.0,
								Duration = NavTabTransitionDuration,
								EasingFunction = NavTabEase
							};
							label.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
						}
						else
						{
							label.BeginAnimation(UIElement.OpacityProperty, null);
							label.Opacity = 1.0;
						}
					}
					else
					{
						label.BeginAnimation(UIElement.OpacityProperty, null);
						label.Opacity = 0.0;
						label.Visibility = Visibility.Collapsed;
						if (label.Foreground != NavTextIdleBrush)
						{
							label.Foreground = NavTextIdleBrush;
						}
						FontWeight targetWeight = FontWeights.Normal;
						if (label.FontWeight != targetWeight)
						{
							label.FontWeight = targetWeight;
						}
					}
				}
			}
		}

		if (NavActivePill != null)
		{
			NavActivePill.Visibility = Visibility.Collapsed;
		}
	}

	private bool IsNavPillTargetMeasured(int physicalIndex)
	{
		Button button = _navTabs![physicalIndex];
		return button != null && (button.ActualWidth > 0.0 || button.Width > 0.0);
	}

	private bool TryMeasureNavPillTarget(int physicalIndex, out double x, out double width)
	{
		x = 0.0;
		width = NavTabActiveWidth;
		Button button = _navTabs![physicalIndex];
		if (button == null || NavTabsHost == null)
		{
			return false;
		}
		if (!NavTabsHost.IsMeasureValid || !NavTabsHost.IsArrangeValid)
		{
			NavTabsHost.UpdateLayout();
		}
		if (!IsNavPillTargetMeasured(physicalIndex))
		{
			return false;
		}
		try
		{
			x = button.TransformToAncestor(NavTabsHost).Transform(new Point(0.0, 0.0)).X;
		}
		catch
		{
			return false;
		}
		width = button.ActualWidth > 0.0 ? button.ActualWidth : NavTabActiveWidth;
		return width > 0.0;
	}

	private void QueueNavPillFollow()
	{
		if (_navPillFollowQueued || !IsLoaded || ViewModel == null || NavTabsHost == null || !NavTabsHost.IsVisible)
		{
			return;
		}
		_navPillFollowQueued = true;
		Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate
		{
			_navPillFollowQueued = false;
			if (ViewModel == null)
			{
				return;
			}
			UpdateNavPill(ViewModel.CurrentSlideIndex, animate: true);
		}));
	}

	private void RetargetNavPillGlide()
	{
		if (_navPillAnimX == null || _navPillAnimWidth == null || !IsLoaded || ViewModel == null || _navTabs == null)
		{
			return;
		}
		int num = MapSlideToPhysicalTabIndex(ViewModel.CurrentSlideIndex);
		if (num < 0 || num >= _navTabs.Length)
		{
			return;
		}
		double x;
		double width;
		if (!TryMeasureNavPillTarget(num, out x, out width))
		{
			return;
		}
		_navPillAnimX.To = x;
		_navPillAnimWidth.To = width;
	}

	private void SnapNavPill(double targetX, double targetWidth)
	{
		NavActivePillTranslate.BeginAnimation(TranslateTransform.XProperty, null);
		NavActivePill.BeginAnimation(FrameworkElement.WidthProperty, null);
		_navPillAnimX = null;
		_navPillAnimWidth = null;
		NavActivePillTranslate.X = targetX;
		NavActivePill.Width = targetWidth;
	}

	private void StartNavPillGlide(double targetX, double targetWidth)
	{
		double fromX = NavActivePillTranslate.X;
		double fromWidth = ((NavActivePill.ActualWidth > 0.0) ? NavActivePill.ActualWidth : ((NavActivePill.Width > 0.0) ? NavActivePill.Width : targetWidth));
		DoubleAnimation animation = new DoubleAnimation
		{
			From = fromX,
			To = targetX,
			Duration = NavPillGlideDuration,
			EasingFunction = NavPillGlideEase
		};
		DoubleAnimation animation2 = new DoubleAnimation
		{
			From = fromWidth,
			To = targetWidth,
			Duration = NavPillGlideDuration,
			EasingFunction = NavPillGlideEase
		};
		_navPillAnimX = animation;
		_navPillAnimWidth = animation2;
		animation.Completed += delegate
		{
			if (_navPillAnimX != animation)
			{
				return;
			}
			_navPillAnimX = null;
			_navPillAnimWidth = null;
			QueueNavPillFollow();
		};
		NavActivePillTranslate.BeginAnimation(TranslateTransform.XProperty, animation);
		NavActivePill.BeginAnimation(FrameworkElement.WidthProperty, animation2);
	}

	public void UpdateNavPill(int slideIndex, bool animate = true)
	{
		if (!IsLoaded || NavTabsHost == null || NavActivePill == null || NavActivePillTranslate == null)
		{
			return;
		}
		EnsureNavPillComponents();
		if (_navTabs == null || _navLabels == null || _navIcons == null || _navDots == null)
		{
			return;
		}
		if (_navDockAnimating)
		{
			return;
		}
		int num = MapSlideToPhysicalTabIndex(slideIndex);
		if (num < 0 || num >= _navTabs.Length || _navTabs[num] == null)
		{
			return;
		}
		bool flag = num != _navPillTabIndex;
		int prevIndex = _navPillTabIndex;
		_navPillSyncing = true;
		try
		{
			ApplyNavTabVisualState(num, prevIndex, flag && animate);
		}
		finally
		{
			_navPillSyncing = false;
		}
		_navPillTabIndex = num;
	}

	private void LyricLine_MouseDown(object sender, MouseButtonEventArgs e)







	{







		if (sender is FrameworkElement { DataContext: LyricsLineViewModel dataContext })







		{







			ViewModel.Media.SeekToLyricLineAsync(dataContext);







		}







	}















	private void Lyrics_PreviewMouseWheel(object sender, MouseWheelEventArgs e)







	{







		e.Handled = true;







	}















	private static (TranslateTransform tt, ScaleTransform st) EnsureCardTransforms(FrameworkElement card)







	{







		card.RenderTransformOrigin = new Point(0.5, 0.5);







		if (card.RenderTransform is TransformGroup transformGroup && transformGroup.Children.Count >= 2 && transformGroup.Children[0] is TranslateTransform item && transformGroup.Children[1] is ScaleTransform item2)







		{







			return (tt: item, st: item2);







		}







		TranslateTransform translateTransform = new TranslateTransform();







		ScaleTransform scaleTransform = new ScaleTransform(1.0, 1.0);







		TransformGroup transformGroup2 = new TransformGroup();







		transformGroup2.Children.Add(translateTransform);







		transformGroup2.Children.Add(scaleTransform);







		card.RenderTransform = transformGroup2;







		return (tt: translateTransform, st: scaleTransform);







	}















	private void AnimateSlideTransition(int fromIndex, int toIndex)







	{







		FrameworkElement[] cards = GetCards();







		int totalActiveSlides = ViewModel.TotalActiveSlides;







		if (toIndex < 0 || toIndex >= cards.Length || toIndex >= totalActiveSlides)







		{







			return;







		}







		if (fromIndex < 0 || fromIndex >= cards.Length || fromIndex >= totalActiveSlides)







		{







			ResetSlideViews(toIndex);







			return;







		}







		int currentTransitionId = ++_slideTransitionCounter;







		FrameworkElement[] allPossibleCards = GetAllPossibleCards();







		foreach (FrameworkElement frameworkElement in allPossibleCards)







		{







			if (frameworkElement != null && frameworkElement != cards[fromIndex] && frameworkElement != cards[toIndex])







			{







				frameworkElement.BeginAnimation(UIElement.OpacityProperty, null);







				var (translateTransform, scaleTransform) = EnsureCardTransforms(frameworkElement);







				translateTransform.BeginAnimation(TranslateTransform.XProperty, null);







				translateTransform.X = 0.0;







				scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);







				scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);







				scaleTransform.ScaleX = 1.0;







				scaleTransform.ScaleY = 1.0;







				frameworkElement.Visibility = Visibility.Collapsed;







				frameworkElement.Opacity = 0.0;







			}







		}







		if (toIndex == 2)







		{







			StartWeatherAnimations();







		}







		else if (fromIndex == 2)







		{







			StopWeatherAnimations();







		}







		AnimateLiquidDot(fromIndex, toIndex);







		bool num = (toIndex > fromIndex && (fromIndex != 0 || toIndex != totalActiveSlides - 1)) || (fromIndex == totalActiveSlides - 1 && toIndex == 0);







		double num2 = 110.0;







		double num3 = (num ? num2 : (0.0 - num2));







		double toValue = (num ? (-80.0) : 80.0);







		TimeSpan timeSpan = TimeSpan.FromMilliseconds(350.0);







		CubicEase easingFunction = new CubicEase







		{







			EasingMode = EasingMode.EaseOut







		};







		FrameworkElement fromCard = cards[fromIndex];







		FrameworkElement toCard = cards[toIndex];







		if (fromCard == null || toCard == null)







		{







			return;







		}







		var (fromTT, scaleTransform2) = EnsureCardTransforms(fromCard);







		var (toTT, scaleTransform3) = EnsureCardTransforms(toCard);







		fromTT.BeginAnimation(TranslateTransform.XProperty, null);







		scaleTransform2.BeginAnimation(ScaleTransform.ScaleXProperty, null);







		scaleTransform2.BeginAnimation(ScaleTransform.ScaleYProperty, null);







		fromCard.BeginAnimation(UIElement.OpacityProperty, null);







		scaleTransform2.ScaleX = 1.0;







		scaleTransform2.ScaleY = 1.0;







		toTT.BeginAnimation(TranslateTransform.XProperty, null);







		scaleTransform3.BeginAnimation(ScaleTransform.ScaleXProperty, null);







		scaleTransform3.BeginAnimation(ScaleTransform.ScaleYProperty, null);







		toCard.BeginAnimation(UIElement.OpacityProperty, null);







		scaleTransform3.ScaleX = 1.0;







		scaleTransform3.ScaleY = 1.0;







		fromCard.Visibility = Visibility.Visible;







		DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, toValue, TimeSpan.FromMilliseconds(240.0))







		{







			EasingFunction = easingFunction







		};







		DoubleAnimation animation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200.0))







		{







			EasingFunction = easingFunction







		};







		doubleAnimation.Completed += (object? _, EventArgs _) =>







		{







			if (_displayedSlideIndex != fromIndex || _slideTransitionCounter == currentTransitionId)







			{







				fromCard.BeginAnimation(UIElement.OpacityProperty, null);







				fromTT.BeginAnimation(TranslateTransform.XProperty, null);







				fromTT.X = 0.0;







				fromCard.Opacity = 0.0;







				fromCard.Visibility = Visibility.Collapsed;







			}







		};







		fromTT.BeginAnimation(TranslateTransform.XProperty, doubleAnimation);







		fromCard.BeginAnimation(UIElement.OpacityProperty, animation);







		toCard.Visibility = Visibility.Visible;







		toCard.Opacity = 0.0;







		toTT.X = num3;







		DoubleAnimation doubleAnimation2 = new DoubleAnimation(num3, 0.0, timeSpan)







		{







			EasingFunction = easingFunction







		};







		DoubleAnimation animation2 = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(320.0))







		{







			EasingFunction = easingFunction







		};







		doubleAnimation2.Completed += (object? _, EventArgs _) =>







		{







			FinalizeToCard();







		};







		toTT.BeginAnimation(TranslateTransform.XProperty, doubleAnimation2);







		toCard.BeginAnimation(UIElement.OpacityProperty, animation2);







		void FinalizeToCard()







		{







			if (_slideTransitionCounter == currentTransitionId)







			{







				toTT.BeginAnimation(TranslateTransform.XProperty, null);







				toCard.BeginAnimation(UIElement.OpacityProperty, null);







				toTT.X = 0.0;







				toCard.Opacity = 1.0;







				toCard.Visibility = Visibility.Visible;







				FrameworkElement[] allPossibleCards2 = GetAllPossibleCards();







				foreach (FrameworkElement frameworkElement2 in allPossibleCards2)







				{







					if (frameworkElement2 != null && frameworkElement2 != toCard)







					{







						frameworkElement2.BeginAnimation(UIElement.OpacityProperty, null);







						TranslateTransform item = EnsureCardTransforms(frameworkElement2).tt;







						item.BeginAnimation(TranslateTransform.XProperty, null);







						item.X = 0.0;







						frameworkElement2.Visibility = Visibility.Collapsed;







						frameworkElement2.Opacity = 0.0;







					}







				}







			}







		}







	}















	private void StartWeatherAnimations()







	{







		if (!_isWeatherAnimRunning)







		{







			_isWeatherAnimRunning = true;







			if (RainFgTranslate != null)







			{







				DoubleAnimation animation = new DoubleAnimation(-50.0, 150.0, TimeSpan.FromMilliseconds(380.0))







				{







					RepeatBehavior = RepeatBehavior.Forever







				};







				DoubleAnimation animation2 = new DoubleAnimation(0.0, -18.0, TimeSpan.FromMilliseconds(380.0))







				{







					RepeatBehavior = RepeatBehavior.Forever







				};







				RainFgTranslate.BeginAnimation(TranslateTransform.YProperty, animation);







				RainFgTranslate.BeginAnimation(TranslateTransform.XProperty, animation2);







			}







			if (RainMidTranslate != null)







			{







				DoubleAnimation animation3 = new DoubleAnimation(-45.0, 155.0, TimeSpan.FromMilliseconds(580.0))







				{







					RepeatBehavior = RepeatBehavior.Forever







				};







				DoubleAnimation animation4 = new DoubleAnimation(0.0, -14.0, TimeSpan.FromMilliseconds(580.0))







				{







					RepeatBehavior = RepeatBehavior.Forever







				};







				RainMidTranslate.BeginAnimation(TranslateTransform.YProperty, animation3);







				RainMidTranslate.BeginAnimation(TranslateTransform.XProperty, animation4);







			}







			if (RainBgTranslate != null)







			{







				DoubleAnimation animation5 = new DoubleAnimation(-40.0, 160.0, TimeSpan.FromMilliseconds(860.0))







				{







					RepeatBehavior = RepeatBehavior.Forever







				};







				DoubleAnimation animation6 = new DoubleAnimation(0.0, -10.0, TimeSpan.FromMilliseconds(860.0))







				{







					RepeatBehavior = RepeatBehavior.Forever







				};







				RainBgTranslate.BeginAnimation(TranslateTransform.YProperty, animation5);







				RainBgTranslate.BeginAnimation(TranslateTransform.XProperty, animation6);







			}







			if (SplashScale1 != null && RainSplash1 != null)







			{







				DoubleAnimation animation7 = new DoubleAnimation(0.2, 1.4, TimeSpan.FromMilliseconds(480.0))







				{







					RepeatBehavior = RepeatBehavior.Forever







				};







				DoubleAnimation animation8 = new DoubleAnimation(0.7, 0.0, TimeSpan.FromMilliseconds(480.0))







				{







					RepeatBehavior = RepeatBehavior.Forever







				};







				SplashScale1.BeginAnimation(ScaleTransform.ScaleXProperty, animation7);







				SplashScale1.BeginAnimation(ScaleTransform.ScaleYProperty, animation7);







				RainSplash1.BeginAnimation(UIElement.OpacityProperty, animation8);







			}







			if (SplashScale2 != null && RainSplash2 != null)







			{







				DoubleAnimation animation9 = new DoubleAnimation(0.2, 1.5, TimeSpan.FromMilliseconds(560.0))







				{







					BeginTime = TimeSpan.FromMilliseconds(180.0),







					RepeatBehavior = RepeatBehavior.Forever







				};







				DoubleAnimation animation10 = new DoubleAnimation(0.7, 0.0, TimeSpan.FromMilliseconds(560.0))







				{







					BeginTime = TimeSpan.FromMilliseconds(180.0),







					RepeatBehavior = RepeatBehavior.Forever







				};







				SplashScale2.BeginAnimation(ScaleTransform.ScaleXProperty, animation9);







				SplashScale2.BeginAnimation(ScaleTransform.ScaleYProperty, animation9);







				RainSplash2.BeginAnimation(UIElement.OpacityProperty, animation10);







			}







			if (SplashScale3 != null && RainSplash3 != null)







			{







				DoubleAnimation animation11 = new DoubleAnimation(0.2, 1.3, TimeSpan.FromMilliseconds(520.0))







				{







					BeginTime = TimeSpan.FromMilliseconds(320.0),







					RepeatBehavior = RepeatBehavior.Forever







				};







				DoubleAnimation animation12 = new DoubleAnimation(0.7, 0.0, TimeSpan.FromMilliseconds(520.0))







				{







					BeginTime = TimeSpan.FromMilliseconds(320.0),







					RepeatBehavior = RepeatBehavior.Forever







				};







				SplashScale3.BeginAnimation(ScaleTransform.ScaleXProperty, animation11);







				SplashScale3.BeginAnimation(ScaleTransform.ScaleYProperty, animation11);







				RainSplash3.BeginAnimation(UIElement.OpacityProperty, animation12);







			}







			if (SunGlowScale != null)







			{







				DoubleAnimation animation13 = new DoubleAnimation







				{







					From = 0.9,







					To = 1.15,







					Duration = TimeSpan.FromMilliseconds(3200.0),







					AutoReverse = true,







					RepeatBehavior = RepeatBehavior.Forever,







					EasingFunction = new SineEase







					{







						EasingMode = EasingMode.EaseInOut







					}







				};







				SunGlowScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation13);







				SunGlowScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation13);







			}







		}







	}















	private void StopWeatherAnimations()







	{







		if (_isWeatherAnimRunning)







		{







			_isWeatherAnimRunning = false;







			if (RainFgTranslate != null)







			{







				RainFgTranslate.BeginAnimation(TranslateTransform.YProperty, null);







				RainFgTranslate.BeginAnimation(TranslateTransform.XProperty, null);







			}







			if (RainMidTranslate != null)







			{







				RainMidTranslate.BeginAnimation(TranslateTransform.YProperty, null);







				RainMidTranslate.BeginAnimation(TranslateTransform.XProperty, null);







			}







			if (RainBgTranslate != null)







			{







				RainBgTranslate.BeginAnimation(TranslateTransform.YProperty, null);







				RainBgTranslate.BeginAnimation(TranslateTransform.XProperty, null);







			}







			if (SplashScale1 != null)







			{







				SplashScale1.BeginAnimation(ScaleTransform.ScaleXProperty, null);







				SplashScale1.BeginAnimation(ScaleTransform.ScaleYProperty, null);







			}







			if (SplashScale2 != null)







			{







				SplashScale2.BeginAnimation(ScaleTransform.ScaleXProperty, null);







				SplashScale2.BeginAnimation(ScaleTransform.ScaleYProperty, null);







			}







			if (SplashScale3 != null)







			{







				SplashScale3.BeginAnimation(ScaleTransform.ScaleXProperty, null);







				SplashScale3.BeginAnimation(ScaleTransform.ScaleYProperty, null);







			}







			if (RainSplash1 != null)







			{







				RainSplash1.BeginAnimation(UIElement.OpacityProperty, null);







			}







			if (RainSplash2 != null)







			{







				RainSplash2.BeginAnimation(UIElement.OpacityProperty, null);







			}







			if (RainSplash3 != null)







			{







				RainSplash3.BeginAnimation(UIElement.OpacityProperty, null);







			}







			if (SunGlowScale != null)







			{







				SunGlowScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);







				SunGlowScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);







			}







		}







	}















	private void MediaSeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)







	{







		ViewModel.Media.IsSeeking = true;







	}















	private void MediaSeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
	{
		ViewModel.Media.IsSeeking = false;
		if (sender is Slider slider)
		{
			ViewModel.Media.SeekToSecondsAsync(slider.Value);
		}
		else if (MediaSeekSlider != null)
		{
			ViewModel.Media.SeekToSecondsAsync(MediaSeekSlider.Value);
		}







	}















	private void OnThemeTransitionRequested(object? sender, ThemeTransitionEventArgs e)







	{







		Dispatcher.Invoke(() =>







		{







			AnimateThemeTransition(e.NewBackground, e.NewStroke, e.Light, e.Animate);







		});







	}















	public void AnimateThemeTransition(Brush newBackground, Brush newStroke, Color newLight, bool animate)

	{

		if (NotchBackgroundPath == null || NotchStrokePath == null)

		{

			return;

		}



		if (!animate || !IsLoaded)

		{

			NotchBackgroundPath.Fill = newBackground;

			NotchStrokePath.Stroke = newStroke;

			if (NotchBackgroundFadePath != null) NotchBackgroundFadePath.Opacity = 0.0;

			if (NotchStrokeFadePath != null) NotchStrokeFadePath.Opacity = 0.0;

			return;

		}



		if (NotchBackgroundFadePath != null)

		{

			NotchBackgroundFadePath.BeginAnimation(UIElement.OpacityProperty, null);

			NotchBackgroundFadePath.Opacity = 0.0;

		}

		if (NotchStrokeFadePath != null)

		{

			NotchStrokeFadePath.BeginAnimation(UIElement.OpacityProperty, null);

			NotchStrokeFadePath.Opacity = 0.0;

		}



		if (newBackground is LinearGradientBrush targetBg && newStroke is LinearGradientBrush targetStroke)

		{

			LinearGradientBrush activeBg;

			if (NotchBackgroundPath.Fill is LinearGradientBrush currentBg && currentBg.GradientStops.Count == targetBg.GradientStops.Count)

			{

				activeBg = currentBg.IsFrozen ? currentBg.Clone() : currentBg;

			}

			else

			{

				activeBg = targetBg.Clone();

				NotchBackgroundPath.Fill = activeBg;

			}



			LinearGradientBrush activeStroke;

			if (NotchStrokePath.Stroke is LinearGradientBrush currentStroke && currentStroke.GradientStops.Count == targetStroke.GradientStops.Count)

			{

				activeStroke = currentStroke.IsFrozen ? currentStroke.Clone() : currentStroke;

			}

			else

			{

				activeStroke = targetStroke.Clone();

				NotchStrokePath.Stroke = activeStroke;

			}



			NotchBackgroundPath.Fill = activeBg;

			NotchStrokePath.Stroke = activeStroke;



			TimeSpan duration = TimeSpan.FromMilliseconds(350.0);

			var ease = new CubicEase { EasingMode = EasingMode.EaseOut };



			for (int i = 0; i < Math.Min(activeBg.GradientStops.Count, targetBg.GradientStops.Count); i++)

			{

				var anim = new ColorAnimation

				{

					From = activeBg.GradientStops[i].Color,

					To = targetBg.GradientStops[i].Color,

					Duration = duration,

					EasingFunction = ease

				};

				activeBg.GradientStops[i].BeginAnimation(GradientStop.ColorProperty, anim);

			}



			for (int i = 0; i < Math.Min(activeStroke.GradientStops.Count, targetStroke.GradientStops.Count); i++)

			{

				var anim = new ColorAnimation

				{

					From = activeStroke.GradientStops[i].Color,

					To = targetStroke.GradientStops[i].Color,

					Duration = duration,

					EasingFunction = ease

				};

				activeStroke.GradientStops[i].BeginAnimation(GradientStop.ColorProperty, anim);

			}

		}

		else

		{

			NotchBackgroundPath.Fill = newBackground;

			NotchStrokePath.Stroke = newStroke;

		}

	}















	private void AnimateHoverStretch(bool isStretching)







	{
		if (_notchToSlimMorphActive || _slimBarMorphToNotchActive)
		{
			Log.Debug("AnimateHoverStretch skipped: slim-bar morph active (stretching={Stretch})", isStretching);
			return;
		}








		if (PillScaleTransform != null && ViewModel.CurrentState != NotchState.Expanded)







		{







			if (isStretching)







			{







				// Poros skala render harus mengikuti lebar dan skala layout terkini
				// sebelum stretch dimulai, karena skala render kini berporos eksplisit.
				UpdatePillLayoutTransformCenter();
				DoubleAnimation animation = new DoubleAnimation







				{







					To = 1.15,







					Duration = TimeSpan.FromMilliseconds(260.0),







					EasingFunction = new CubicEase







					{







						EasingMode = EasingMode.EaseOut







					}







				};







				DoubleAnimation animation2 = new DoubleAnimation







				{







					To = 1.02,







					Duration = TimeSpan.FromMilliseconds(260.0),







					EasingFunction = new CubicEase







					{







						EasingMode = EasingMode.EaseOut







					}







				};







				PillScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);







				PillScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation2);







			}







			else







			{







				BackEase easingFunction = new BackEase







				{







					Amplitude = 0.35,







					EasingMode = EasingMode.EaseOut







				};







				DoubleAnimation animation3 = new DoubleAnimation







				{







					To = 1.0,







					Duration = TimeSpan.FromMilliseconds(220.0),







					EasingFunction = easingFunction







				};







				DoubleAnimation animation4 = new DoubleAnimation







				{







					To = 1.0,







					Duration = TimeSpan.FromMilliseconds(200.0),







					EasingFunction = new CubicEase







					{







						EasingMode = EasingMode.EaseOut







					}







				};







				PillScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation3);







				PillScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation4);







			}







		}







	}















	private void ResetHoverStretch()







	{







		if (_notchToSlimMorphActive || _slimBarMorphToNotchActive)
		{
			Log.Debug("ResetHoverStretch skipped: slim-bar morph active");
			return;
		}
		if (PillScaleTransform != null)







		{







			DoubleAnimation animation = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180.0))







			{







				EasingFunction = new CubicEase







				{







					EasingMode = EasingMode.EaseOut







				}







			};







			PillScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);







			PillScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);







		}







	}















	private void AnimateCollapsedLyricChange(string newLyric, int lyricIndex)







	{







		if (CollapsedLyricTextA == null || CollapsedLyricTextB == null || string.IsNullOrWhiteSpace(newLyric))







		{







			return;







		}















		if (lyricIndex == _lastAnimatedLyricIndex && lyricIndex >= 0)







		{







			return;







		}







		_lastAnimatedLyricIndex = lyricIndex;















		TextBlock outgoingText = _isLyricSlotA ? CollapsedLyricTextA : CollapsedLyricTextB;
		FrameworkElement outgoingSlot = _isLyricSlotA ? CollapsedLyricSlotA : CollapsedLyricSlotB;
		TextBlock outgoingOverlay = _isLyricSlotA ? CollapsedLyricOverlayA : CollapsedLyricOverlayB;







		TranslateTransform outgoingTT = _isLyricSlotA ? CollapsedLyricTransformA : CollapsedLyricTransformB;















		TextBlock incomingText = _isLyricSlotA ? CollapsedLyricTextB : CollapsedLyricTextA;
		FrameworkElement incomingSlot = _isLyricSlotA ? CollapsedLyricSlotB : CollapsedLyricSlotA;
		TextBlock incomingOverlay = _isLyricSlotA ? CollapsedLyricOverlayB : CollapsedLyricOverlayA;







		TranslateTransform incomingTT = _isLyricSlotA ? CollapsedLyricTransformB : CollapsedLyricTransformA;















		_isLyricSlotA = !_isLyricSlotA;















		const double slideDistance = 16.0;







		bool hasOutgoing = !string.IsNullOrWhiteSpace(outgoingText.Text);















		if (hasOutgoing)







		{







			// 1. Kalimat satu fade lalu bergerak keatas







			var outSlide = new DoubleAnimation(0.0, -slideDistance, TimeSpan.FromMilliseconds(200))







			{







				EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }







			};















			var outFade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(180))







			{







				EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }







			};















			outSlide.Completed += (s, e) =>







			{







				outgoingTT.BeginAnimation(TranslateTransform.YProperty, null);







				outgoingSlot.BeginAnimation(UIElement.OpacityProperty, null);







				outgoingText.Text = string.Empty;
				outgoingOverlay.Text = string.Empty;







				outgoingTT.Y = slideDistance;







				outgoingSlot.Opacity = 0.0;







			};















			outgoingTT.BeginAnimation(TranslateTransform.YProperty, outSlide);







			outgoingSlot.BeginAnimation(UIElement.OpacityProperty, outFade);







		}







		else







		{







			outgoingText.Text = string.Empty;
			outgoingOverlay.Text = string.Empty;







			outgoingTT.Y = slideDistance;







			outgoingSlot.Opacity = 0.0;







		}















		// 2. Lalu masuk kalimat berikutnya fade bergerak dari bawah







		incomingTT.BeginAnimation(TranslateTransform.YProperty, null);







		incomingSlot.BeginAnimation(UIElement.OpacityProperty, null);







		incomingText.Text = newLyric;
		incomingOverlay.Text = newLyric;

		UpdateCollapsedLyricsWidth(newLyric);







		incomingTT.Y = slideDistance;







		incomingSlot.Opacity = 0.0;















		TimeSpan delay = hasOutgoing ? TimeSpan.FromMilliseconds(80) : TimeSpan.Zero;















		var inSlide = new DoubleAnimation(slideDistance, 0.0, TimeSpan.FromMilliseconds(250))







		{







			BeginTime = delay,







			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }







		};















		var inFade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220))







		{







			BeginTime = delay,







			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }







		};















		inSlide.Completed += (s, e) =>







		{







			incomingTT.BeginAnimation(TranslateTransform.YProperty, null);







			incomingSlot.BeginAnimation(UIElement.OpacityProperty, null);







			incomingTT.Y = 0.0;







			incomingSlot.Opacity = 1.0;







		};















		incomingTT.BeginAnimation(TranslateTransform.YProperty, inSlide);







		incomingSlot.BeginAnimation(UIElement.OpacityProperty, inFade);







	}















	private void ResetCollapsedLyricText(string text, int lyricIndex)







	{







		if (CollapsedLyricTextA != null && CollapsedLyricTextB != null)







		{







			_lastAnimatedLyricIndex = lyricIndex;







			_isLyricSlotA = true;















			CollapsedLyricTransformA.BeginAnimation(TranslateTransform.YProperty, null);







			CollapsedLyricTransformB.BeginAnimation(TranslateTransform.YProperty, null);







			CollapsedLyricSlotA.BeginAnimation(UIElement.OpacityProperty, null);







			CollapsedLyricSlotB.BeginAnimation(UIElement.OpacityProperty, null);















			CollapsedLyricTextA.Text = text ?? string.Empty;
			CollapsedLyricOverlayA.Text = text ?? string.Empty;







			CollapsedLyricTransformA.Y = 0.0;







			CollapsedLyricSlotA.Opacity = string.IsNullOrWhiteSpace(text) ? 0.0 : 1.0;















			CollapsedLyricTextB.Text = string.Empty;
			CollapsedLyricOverlayB.Text = string.Empty;







			CollapsedLyricTransformB.Y = 16.0;







			CollapsedLyricSlotB.Opacity = 0.0;

			UpdateCollapsedLyricsWidth(text);







		}







	}















	private void UpdateCollapsedLyricsWidth(string? lyricText)
	{
		if (ViewModel == null)
		{
			return;
		}

		if (!ViewModel.Media.IsCollapsedLyricsActive || ViewModel.Media.IsActiveLyricInterlude || string.IsNullOrWhiteSpace(lyricText))
		{
			ViewModel.SetCollapsedLyricsWidthOverride(null);
			return;
		}

		if (CollapsedLyricTextA == null || CollapsedLyricsContainer == null)
		{
			return;
		}

		double containerWidth = CollapsedLyricsContainer.ActualWidth;
		double notchWidth = (ContentHost != null && ContentHost.ActualWidth > 0.0) ? ContentHost.ActualWidth : NotchWidth;
		if (containerWidth <= 0.0 || notchWidth <= 0.0)
		{
			if (!_collapsedLyricsWidthRetryQueued)
			{
				_collapsedLyricsWidthRetryQueued = true;
				Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate
				{
					_collapsedLyricsWidthRetryQueued = false;
					UpdateCollapsedLyricsWidth(ViewModel?.Media.CollapsedDisplayLyricText);
				}));
			}
			return;
		}

		TextBlock textBlock = CollapsedLyricTextA;

		double MeasureWidth(string text, double fontSize)
		{
			TextBlock probe = new TextBlock
			{
				FontFamily = textBlock.FontFamily,
				FontSize = fontSize,
				FontStyle = textBlock.FontStyle,
				FontWeight = textBlock.FontWeight,
				FontStretch = textBlock.FontStretch,
				Text = text,
				TextWrapping = TextWrapping.NoWrap
			};
			TextOptions.SetTextFormattingMode(probe, TextOptions.GetTextFormattingMode(textBlock));
			TextOptions.SetTextRenderingMode(probe, TextOptions.GetTextRenderingMode(textBlock));
			probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
			return probe.DesiredSize.Width;
		}

		double mainWidth = MeasureWidth(lyricText, textBlock.FontSize);

		double measuredChrome = (containerWidth > 0.0 && notchWidth > containerWidth) ? (notchWidth - containerWidth) : 0.0;
		double dynamicChrome = 104.0;
		if (ViewModel.ShowWeatherInCollapsedNotch) dynamicChrome += 68.0;
		if (ViewModel.ShowBatteryInCollapsedNotch && ViewModel.Battery?.HasBattery == true) dynamicChrome += 68.0;
		if (ViewModel.ShowPetInCollapsedNotch && ViewModel.IsPetEnabled) dynamicChrome += 34.0;
		double chrome = Math.Max(dynamicChrome, measuredChrome);
		double targetWidth = chrome + Math.Max(MinCollapsedLyricTextWidth, mainWidth) + 14.0;
		ViewModel.SetCollapsedLyricsWidthOverride(targetWidth);
	}

	private void AnimateLyricsVersionPicker(bool isOpen)
	{
		if (LyricsContainer == null) return;

		double targetHeight = isOpen ? 222.0 : 150.0;
		TimeSpan duration = TimeSpan.FromMilliseconds(isOpen ? 360 : 280);
		IEasingFunction easing = new QuarticEase { EasingMode = EasingMode.EaseOut };

		DoubleAnimation heightAnim = new DoubleAnimation(targetHeight, duration)
		{
			EasingFunction = easing
		};
		LyricsContainer.BeginAnimation(FrameworkElement.HeightProperty, heightAnim);
	}

	private void AnimateLyricsExpansion(bool isExpanded)







	{

		Log.Information("AnimateLyricsExpansion: isExpanded={IsExpanded}, MediaHubGrid.Width={Width}, MediaPlayerContainer.Width={PlayerWidth}",

			isExpanded, MediaHubGrid?.Width, MediaPlayerContainer?.Width);







		if (MediaPlayerContainer == null || LyricsContainer == null || LyricsDivider == null)







		{







			return;







		}







		TimeSpan timeSpan = TimeSpan.FromMilliseconds(isExpanded ? 320 : 260);







		CubicEase easingFunction = new CubicEase







		{







			EasingMode = EasingMode.EaseOut







		};







		if (MediaHubGrid != null)







		{







			DoubleAnimation animation = new DoubleAnimation(isExpanded ? 584.0 : 358.0, timeSpan)







			{







				EasingFunction = easingFunction







			};







			MediaHubGrid.BeginAnimation(FrameworkElement.WidthProperty, animation);







		}







		DoubleAnimation animation2 = new DoubleAnimation(isExpanded ? 215.0 : 358.0, timeSpan)







		{







			EasingFunction = easingFunction







		};







		MediaPlayerContainer.BeginAnimation(FrameworkElement.WidthProperty, animation2);







		if (isExpanded)







		{







			if (LyricsContainer.Visibility == Visibility.Collapsed)

			{

				LyricsContainer.Opacity = 0.0;

				if (LyricsContainerTranslate != null && LyricsContainerTranslate.X == 0.0)

				{

					LyricsContainerTranslate.X = 60.0;

				}

			}



			LyricsContainer.Visibility = Visibility.Visible;







			LyricsContainer.IsHitTestVisible = true;







			DoubleAnimation animation3 = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(280.0))







			{







				BeginTime = TimeSpan.FromMilliseconds(40.0),







				EasingFunction = easingFunction







			};







			LyricsContainer.BeginAnimation(UIElement.OpacityProperty, animation3);







			if (LyricsContainerTranslate != null)







			{







				DoubleAnimation animation4 = new DoubleAnimation(0.0, timeSpan)







				{







					EasingFunction = easingFunction







				};







				LyricsContainerTranslate.BeginAnimation(TranslateTransform.XProperty, animation4);







			}







			DoubleAnimation animation5 = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(250.0))







			{







				BeginTime = TimeSpan.FromMilliseconds(60.0)







			};







			LyricsDivider.BeginAnimation(UIElement.OpacityProperty, animation5);







			return;







		}







		ViewModel.Media.IsLyricsVersionPickerOpen = false;







		LyricsContainer.IsHitTestVisible = false;







		DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(180.0));







		doubleAnimation.Completed += (object? s, EventArgs e) =>







		{







			if (!ViewModel.Media.IsLyricsExpanded)







			{







				LyricsContainer.Visibility = Visibility.Collapsed;







			}







		};







		LyricsContainer.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);







		if (LyricsContainerTranslate != null)







		{







			DoubleAnimation animation6 = new DoubleAnimation(60.0, timeSpan)







			{







				EasingFunction = easingFunction







			};







			LyricsContainerTranslate.BeginAnimation(TranslateTransform.XProperty, animation6);







		}







		DoubleAnimation animation7 = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(160.0));







		LyricsDivider.BeginAnimation(UIElement.OpacityProperty, animation7);







	}















	protected override void OnClosed(EventArgs e)







	{







		ThemeManager.ThemeTransitionRequested -= OnThemeTransitionRequested;







		if (ViewModel != null)







		{







			ViewModel.SlideChanged -= OnSlideChanged;







			ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
				ViewModel.Download.PropertyChanged -= OnDownloadPropertyChanged;
				ShutdownOutlineFx();
				ViewModel.Shutdown();







		}







		StopTopmostKeeper();

		_trayService?.Dispose();







		_trayService = null;







		if (_settingsWindow != null)







		{







			_settingsWindow.Close();







			_settingsWindow = null;







		}







		if (_displayPowerNotifyHandle != IntPtr.Zero)







		{







			try







			{







				NativeMethods.UnregisterPowerSettingNotification(_displayPowerNotifyHandle);







			}







			catch







			{







			}







			_displayPowerNotifyHandle = IntPtr.Zero;







		}







		base.OnClosed(e);







	}















	private void AnimateTimerSubNotch(bool isRunning)
	{
		bool shouldShow = isRunning && (ViewModel?.CurrentState == NotchState.Expanded);
		if (TimerSubNotchPill == null || TimerSubNotchAnimTranslate == null || _isTimerSubNotchShowing == shouldShow)
		{
			return;
		}
		_isTimerSubNotchShowing = shouldShow;
		if (shouldShow)







		{







			TimerSubNotchPill.Visibility = Visibility.Visible;







			DoubleAnimation animation = new DoubleAnimation







			{







				From = TimerSubNotchAnimTranslate.Y,







				To = 0.0,







				Duration = TimeSpan.FromMilliseconds(380.0),







				EasingFunction = new BackEase







				{







					Amplitude = 0.18,







					EasingMode = EasingMode.EaseOut







				}







			};







			DoubleAnimation animation2 = new DoubleAnimation







			{







				From = TimerSubNotchPill.Opacity,







				To = 1.0,







				Duration = TimeSpan.FromMilliseconds(220.0),







				EasingFunction = new CubicEase







				{







					EasingMode = EasingMode.EaseOut







				}







			};







			TimerSubNotchAnimTranslate.BeginAnimation(TranslateTransform.YProperty, animation);







			TimerSubNotchPill.BeginAnimation(UIElement.OpacityProperty, animation2);







			return;







		}







		DoubleAnimation doubleAnimation = new DoubleAnimation







		{







			From = TimerSubNotchAnimTranslate.Y,







			To = -24.0,







			Duration = TimeSpan.FromMilliseconds(240.0),







			EasingFunction = new CubicEase







			{







				EasingMode = EasingMode.EaseIn







			}







		};







		DoubleAnimation animation3 = new DoubleAnimation







		{







			From = TimerSubNotchPill.Opacity,







			To = 0.0,







			Duration = TimeSpan.FromMilliseconds(180.0),







			EasingFunction = new CubicEase







			{







				EasingMode = EasingMode.EaseIn







			}







		};







		doubleAnimation.Completed += (object? _, EventArgs _) =>







		{







			if (!_isTimerSubNotchShowing)







			{







				TimerSubNotchPill.Visibility = Visibility.Collapsed;







			}







		};







		TimerSubNotchAnimTranslate.BeginAnimation(TranslateTransform.YProperty, doubleAnimation);







		TimerSubNotchPill.BeginAnimation(UIElement.OpacityProperty, animation3);







	}















	private void AnimateStopwatchSubNotch(bool isRunning)
	{
		bool shouldShow = isRunning && (ViewModel?.CurrentState == NotchState.Expanded);
		if (StopwatchSubNotchPill == null || StopwatchSubNotchAnimTranslate == null || _isStopwatchSubNotchShowing == shouldShow)
		{
			return;
		}
		_isStopwatchSubNotchShowing = shouldShow;
		if (shouldShow)







		{







			StopwatchSubNotchPill.Visibility = Visibility.Visible;







			DoubleAnimation animation = new DoubleAnimation







			{







				From = StopwatchSubNotchAnimTranslate.Y,







				To = 0.0,







				Duration = TimeSpan.FromMilliseconds(380.0),







				EasingFunction = new BackEase







				{







					Amplitude = 0.18,







					EasingMode = EasingMode.EaseOut







				}







			};







			DoubleAnimation animation2 = new DoubleAnimation







			{







				From = StopwatchSubNotchPill.Opacity,







				To = 1.0,







				Duration = TimeSpan.FromMilliseconds(220.0),







				EasingFunction = new CubicEase







				{







					EasingMode = EasingMode.EaseOut







				}







			};







			StopwatchSubNotchAnimTranslate.BeginAnimation(TranslateTransform.YProperty, animation);







			StopwatchSubNotchPill.BeginAnimation(UIElement.OpacityProperty, animation2);







			return;







		}







		DoubleAnimation doubleAnimation = new DoubleAnimation







		{







			From = StopwatchSubNotchAnimTranslate.Y,







			To = -24.0,







			Duration = TimeSpan.FromMilliseconds(240.0),







			EasingFunction = new CubicEase







			{







				EasingMode = EasingMode.EaseIn







			}







		};







		DoubleAnimation animation3 = new DoubleAnimation







		{







			From = StopwatchSubNotchPill.Opacity,







			To = 0.0,







			Duration = TimeSpan.FromMilliseconds(180.0),







			EasingFunction = new CubicEase







			{







				EasingMode = EasingMode.EaseIn







			}







		};







		doubleAnimation.Completed += (object? _, EventArgs _) =>







		{







			if (!_isStopwatchSubNotchShowing)







			{







				StopwatchSubNotchPill.Visibility = Visibility.Collapsed;







			}







		};







		StopwatchSubNotchAnimTranslate.BeginAnimation(TranslateTransform.YProperty, doubleAnimation);







		StopwatchSubNotchPill.BeginAnimation(UIElement.OpacityProperty, animation3);







	}















	private void AnimateDownloadSubNotch(bool show)

	{

		if (DownloadSubNotchPill == null || DownloadSubNotchAnimTranslate == null || _isDownloadSubNotchShowing == show)

		{

			return;

		}

		_isDownloadSubNotchShowing = show;

		if (show)

		{

			DownloadSubNotchPill.Visibility = Visibility.Visible;

			DoubleAnimation animation = new DoubleAnimation

			{

				From = DownloadSubNotchAnimTranslate.Y,

				To = 0.0,

				Duration = TimeSpan.FromMilliseconds(380.0),

				EasingFunction = new BackEase { Amplitude = 0.18, EasingMode = EasingMode.EaseOut }

			};

			DoubleAnimation animation2 = new DoubleAnimation

			{

				From = DownloadSubNotchPill.Opacity,

				To = 1.0,

				Duration = TimeSpan.FromMilliseconds(220.0),

				EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }

			};

			DownloadSubNotchAnimTranslate.BeginAnimation(TranslateTransform.YProperty, animation);

			DownloadSubNotchPill.BeginAnimation(UIElement.OpacityProperty, animation2);

			return;

		}

		DoubleAnimation doubleAnimation = new DoubleAnimation

		{

			From = DownloadSubNotchAnimTranslate.Y,

			To = -24.0,

			Duration = TimeSpan.FromMilliseconds(240.0),

			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }

		};

		DoubleAnimation animation3 = new DoubleAnimation

		{

			From = DownloadSubNotchPill.Opacity,

			To = 0.0,

			Duration = TimeSpan.FromMilliseconds(180.0),

			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }

		};

		doubleAnimation.Completed += (_, _) =>

		{

			if (!_isDownloadSubNotchShowing)

			{

				DownloadSubNotchPill.Visibility = Visibility.Collapsed;

			}

		};

		DownloadSubNotchAnimTranslate.BeginAnimation(TranslateTransform.YProperty, doubleAnimation);

		DownloadSubNotchPill.BeginAnimation(UIElement.OpacityProperty, animation3);

	}

	private void UpdateDownloadSubNotchVisibility()

	{

		if (ViewModel == null) return;

		bool shouldShow = ViewModel.Download != null

			&& ViewModel.Download.HasActiveDownloads

			&& ViewModel.CurrentState == NotchState.Expanded

			&& !ViewModel.IsFullscreenActive

			&& !ViewModel.IsSlimBarVisible

			&& !ViewModel.IsManuallyHidden;

		AnimateDownloadSubNotch(shouldShow);

	}

	private void CollapsedDownloadHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
		ViewModel?.NavigateToDownloadsPanel();
	}

	private void AnimateCollapsedDownloadAlternation(bool toDownloadPhase)
	{
		if (CollapsedNormalWidgetsHost == null || CollapsedDownloadHost == null) return;
		if (ViewModel?.CurrentState != NotchState.Collapsed)
		{
			ResetCollapsedDownloadSlotStates(toDownloadPhase);
			return;
		}

		var springEase = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };
		var fadeEase = new CubicEase { EasingMode = EasingMode.EaseOut };
		var outEase = new CubicEase { EasingMode = EasingMode.EaseIn };

		var outDuration = TimeSpan.FromMilliseconds(180);
		var inDuration = TimeSpan.FromMilliseconds(420);
		var inFadeDuration = TimeSpan.FromMilliseconds(240);
		var inBeginTime = TimeSpan.FromMilliseconds(40);

		FrameworkElement outgoingSlot = toDownloadPhase ? CollapsedNormalWidgetsHost : CollapsedDownloadHost;
		TranslateTransform outgoingTrans = toDownloadPhase ? CollapsedNormalWidgetsTranslate : CollapsedDownloadTranslate;
		ScaleTransform outgoingScale = toDownloadPhase ? CollapsedNormalWidgetsScale : CollapsedDownloadScale;

		FrameworkElement incomingSlot = toDownloadPhase ? CollapsedDownloadHost : CollapsedNormalWidgetsHost;
		TranslateTransform incomingTrans = toDownloadPhase ? CollapsedDownloadTranslate : CollapsedNormalWidgetsTranslate;
		ScaleTransform incomingScale = toDownloadPhase ? CollapsedDownloadScale : CollapsedNormalWidgetsScale;

		var fadeOut = new DoubleAnimation(0.0, outDuration) { EasingFunction = outEase };
		var slideOut = new DoubleAnimation(-6.0, outDuration) { EasingFunction = outEase };
		var scaleOut = new DoubleAnimation(0.85, outDuration) { EasingFunction = outEase };

		fadeOut.Completed += (_, _) =>
		{
			if (outgoingSlot != null)
			{
				outgoingSlot.Visibility = Visibility.Collapsed;
			}
		};

		outgoingSlot.BeginAnimation(UIElement.OpacityProperty, fadeOut);
		outgoingTrans.BeginAnimation(TranslateTransform.YProperty, slideOut);
		outgoingScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
		outgoingScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOut);

		incomingSlot.Visibility = Visibility.Visible;
		incomingTrans.Y = 6.0;
		incomingScale.ScaleX = 0.85;
		incomingScale.ScaleY = 0.85;
		incomingSlot.Opacity = 0.0;

		var fadeIn = new DoubleAnimation(0.0, 1.0, inFadeDuration) { BeginTime = inBeginTime, EasingFunction = fadeEase };
		var springIn = new DoubleAnimation(6.0, 0.0, inDuration) { BeginTime = inBeginTime, EasingFunction = springEase };
		var scaleIn = new DoubleAnimation(0.85, 1.0, inDuration) { BeginTime = inBeginTime, EasingFunction = springEase };

		incomingSlot.BeginAnimation(UIElement.OpacityProperty, fadeIn);
		incomingTrans.BeginAnimation(TranslateTransform.YProperty, springIn);
		incomingScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
		incomingScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);
	}

	private void ResetCollapsedDownloadSlotStates(bool isDownloadPhase)
	{
		if (CollapsedNormalWidgetsHost == null || CollapsedDownloadHost == null) return;

		CollapsedNormalWidgetsHost.BeginAnimation(UIElement.OpacityProperty, null);
		CollapsedNormalWidgetsTranslate?.BeginAnimation(TranslateTransform.YProperty, null);
		CollapsedNormalWidgetsScale?.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		CollapsedNormalWidgetsScale?.BeginAnimation(ScaleTransform.ScaleYProperty, null);

		CollapsedDownloadHost.BeginAnimation(UIElement.OpacityProperty, null);
		CollapsedDownloadTranslate?.BeginAnimation(TranslateTransform.YProperty, null);
		CollapsedDownloadScale?.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		CollapsedDownloadScale?.BeginAnimation(ScaleTransform.ScaleYProperty, null);

		if (isDownloadPhase)
		{
			CollapsedNormalWidgetsHost.Opacity = 0.0;
			CollapsedNormalWidgetsHost.Visibility = Visibility.Collapsed;
			if (CollapsedNormalWidgetsTranslate != null) CollapsedNormalWidgetsTranslate.Y = -6.0;
			if (CollapsedNormalWidgetsScale != null) { CollapsedNormalWidgetsScale.ScaleX = 0.85; CollapsedNormalWidgetsScale.ScaleY = 0.85; }

			CollapsedDownloadHost.Opacity = 1.0;
			CollapsedDownloadHost.Visibility = Visibility.Visible;
			if (CollapsedDownloadTranslate != null) CollapsedDownloadTranslate.Y = 0.0;
			if (CollapsedDownloadScale != null) { CollapsedDownloadScale.ScaleX = 1.0; CollapsedDownloadScale.ScaleY = 1.0; }
		}
		else
		{
			CollapsedNormalWidgetsHost.Opacity = 1.0;
			CollapsedNormalWidgetsHost.Visibility = Visibility.Visible;
			if (CollapsedNormalWidgetsTranslate != null) CollapsedNormalWidgetsTranslate.Y = 0.0;
			if (CollapsedNormalWidgetsScale != null) { CollapsedNormalWidgetsScale.ScaleX = 1.0; CollapsedNormalWidgetsScale.ScaleY = 1.0; }

			CollapsedDownloadHost.Opacity = 0.0;
			CollapsedDownloadHost.Visibility = Visibility.Collapsed;
			if (CollapsedDownloadTranslate != null) CollapsedDownloadTranslate.Y = 6.0;
			if (CollapsedDownloadScale != null) { CollapsedDownloadScale.ScaleX = 0.85; CollapsedDownloadScale.ScaleY = 0.85; }
		}
	}

	private void OnDownloadPropertyChanged(object? sender, PropertyChangedEventArgs e)

	{

		if (e.PropertyName == nameof(DownloadViewModel.HasActiveDownloads))

		{

			Dispatcher.Invoke(UpdateDownloadSubNotchVisibility);

		}

	}

	private void DownloadSubNotchPill_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)

	{

		e.Handled = true;

		ViewModel.NavigateToDownloadsPanel();

	}

	private void TimerSubNotchPill_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)







	{







		e.Handled = true;







		ViewModel.TimerStopwatch.IsTimerMode = true;







		ViewModel.NavigateToTimerSlide();







	}















	private void StopwatchSubNotchPill_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)







	{







		e.Handled = true;







		ViewModel.TimerStopwatch.IsTimerMode = false;







		ViewModel.NavigateToTimerSlide();







	}



	private void AnimateNotificationSubNotch(bool show, bool forceRestart = false)

	{

		if (NotificationSubNotchPill == null || NotificationSubNotchAnimTranslate == null)

		{

			return;

		}



		if (!forceRestart && _isNotificationSubNotchShowing == show)

		{

			return;

		}



		_isNotificationSubNotchShowing = show;

		if (show)

		{

			NotificationSubNotchPill.Visibility = Visibility.Visible;

			DoubleAnimation animation = new DoubleAnimation

			{

				From = NotificationSubNotchAnimTranslate.Y < -5.0 ? NotificationSubNotchAnimTranslate.Y : -28.0,

				To = 0.0,

				Duration = TimeSpan.FromMilliseconds(380.0),

				EasingFunction = new BackEase

				{

					Amplitude = 0.18,

					EasingMode = EasingMode.EaseOut

				}

			};

			DoubleAnimation animation2 = new DoubleAnimation

			{

				From = NotificationSubNotchPill.Opacity < 0.2 ? NotificationSubNotchPill.Opacity : 0.0,

				To = 1.0,

				Duration = TimeSpan.FromMilliseconds(220.0),

				EasingFunction = new CubicEase

				{

					EasingMode = EasingMode.EaseOut

				}

			};

			NotificationSubNotchAnimTranslate.BeginAnimation(TranslateTransform.YProperty, animation);

			NotificationSubNotchPill.BeginAnimation(UIElement.OpacityProperty, animation2);

			return;

		}



		DoubleAnimation doubleAnimation = new DoubleAnimation

		{

			From = NotificationSubNotchAnimTranslate.Y,

			To = -28.0,

			Duration = TimeSpan.FromMilliseconds(240.0),

			EasingFunction = new CubicEase

			{

				EasingMode = EasingMode.EaseIn

			}

		};

		DoubleAnimation animation3 = new DoubleAnimation

		{

			From = NotificationSubNotchPill.Opacity,

			To = 0.0,

			Duration = TimeSpan.FromMilliseconds(180.0),

			EasingFunction = new CubicEase

			{

				EasingMode = EasingMode.EaseIn

			}

		};

		animation3.Completed += (object? s, EventArgs e) =>

		{

			if (!_isNotificationSubNotchShowing)

			{

				NotificationSubNotchPill.Visibility = Visibility.Collapsed;

			}

		};

		NotificationSubNotchAnimTranslate.BeginAnimation(TranslateTransform.YProperty, doubleAnimation);

		NotificationSubNotchPill.BeginAnimation(UIElement.OpacityProperty, animation3);

	}



	private void NotificationSubNotchPill_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)

	{

		e.Handled = true;

		if (ViewModel?.Notification?.IsWaterAlarm == true)

		{

			return;

		}

		AnimateNotificationSubNotch(false);

		ViewModel?.WidgetHost?.DismissTransient();

	}















	private void SittingCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)







	{







		if (e.OriginalSource is DependencyObject dependencyObject)







		{







			DependencyObject dependencyObject2 = dependencyObject;







			while (dependencyObject2 != null && dependencyObject2 != sender)







			{







				if (dependencyObject2 is Button)







				{







					return;







				}







				dependencyObject2 = VisualTreeHelper.GetParent(dependencyObject2);







			}







		}







		FlipSittingCard();







	}















	public void FlipSittingCard()







	{







		if (_isCardFlipping)







		{







			return;







		}







		_isCardFlipping = true;







		DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(130.0))







		{







			EasingFunction = new QuadraticEase







			{







				EasingMode = EasingMode.EaseIn







			}







		};







		doubleAnimation.Completed += (object? _, EventArgs _) =>







		{







			ViewModel.Water.ToggleSittingFlip();







			DoubleAnimation doubleAnimation2 = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(130.0))







			{







				EasingFunction = new QuadraticEase







				{







					EasingMode = EasingMode.EaseOut







				}







			};







			doubleAnimation2.Completed += (object? obj, EventArgs e) =>







			{







				_isCardFlipping = false;







			};







			SittingCardScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, doubleAnimation2);







		};







		SittingCardScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, doubleAnimation);







	}















	private void TaskInputCapsule_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)







	{







		Activate();







	}















	private void TaskInputTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)







	{







		Activate();







	}















	private void Window_PreviewDragEnter(object sender, DragEventArgs e)







	{







		if (!_isInternalDragOut && e.Data.GetDataPresent(DataFormats.FileDrop))







		{







			e.Effects = DragDropEffects.Copy;







			if (ViewModel.CurrentState == NotchState.Collapsed)







			{







				_wasAutoExpandedByDrag = true;







				ViewModel.Expand();







			}







			ShowDragChoiceOverlay(show: true);







			e.Handled = true;







		}







	}















	private void Window_PreviewDragOver(object sender, DragEventArgs e)







	{







		if (_isInternalDragOut || !e.Data.GetDataPresent(DataFormats.FileDrop))







		{







			return;







		}







		e.Effects = DragDropEffects.Copy;







		if (DragChoiceOverlay != null && DragChoiceOverlay.Visibility != Visibility.Visible)







		{







			ShowDragChoiceOverlay(show: true);







		}







		if (DropTargetShelf != null && DropTargetConvert != null && DropTargetDock != null && DragChoiceOverlay != null && DragChoiceOverlay.Visibility == Visibility.Visible)







		{







			Point position = e.GetPosition(DropTargetShelf);







			Point position2 = e.GetPosition(DropTargetDock);
			Point position3 = e.GetPosition(DropTargetConvert);







			bool num = position.X >= 0.0 && position.Y >= 0.0 && position.X <= DropTargetShelf.ActualWidth && position.Y <= DropTargetShelf.ActualHeight;







			bool flag = position2.X >= 0.0 && position2.Y >= 0.0 && position2.X <= DropTargetDock.ActualWidth && position2.Y <= DropTargetDock.ActualHeight;
			bool flag2 = position3.X >= 0.0 && position3.Y >= 0.0 && position3.X <= DropTargetConvert.ActualWidth && position3.Y <= DropTargetConvert.ActualHeight;







			if (num)







			{







				HighlightDropTarget(shelf: true);







			}







			else if (flag2)
			{
				HighlightConvertTarget();
			}
			else if (flag)







			{







				HighlightDropTarget(shelf: false);







			}







			else







			{







				ResetDropTargetsHighlight();







			}







		}







		e.Handled = true;







	}















	private void Window_PreviewDragLeave(object sender, DragEventArgs e)







	{







		if (_isInternalDragOut)







		{







			return;







		}







		Point position = e.GetPosition(this);







		if (position.X < 0.0 || position.Y < 0.0 || position.X > ActualWidth || position.Y > ActualHeight)







		{







			HideDragChoiceOverlay();
			StopWeatherAnimations();







			if (_wasAutoExpandedByDrag && ViewModel.CurrentState == NotchState.Expanded)







			{







				_wasAutoExpandedByDrag = false;







				ViewModel.Collapse();







			}







		}







	}















	private void Window_PreviewDrop(object sender, DragEventArgs e)







	{







		if (_isInternalDragOut)
		{
			_shelfDropInsideNotch = true;
			return;
		}

		if (e.Handled || !e.Data.GetDataPresent(DataFormats.FileDrop))







		{







			return;







		}







		if (e.Data.GetData(DataFormats.FileDrop) is string[] array && array.Length != 0)







		{







			if (DropTargetConvert != null)
			{
				Point convertPoint = e.GetPosition(DropTargetConvert);
				if (convertPoint.X >= 0.0 && convertPoint.Y >= 0.0 && convertPoint.X <= DropTargetConvert.ActualWidth && convertPoint.Y <= DropTargetConvert.ActualHeight)
				{
					var convertResult = new ImageConvertService().ConvertFiles(array);
					ShowConvertResultToast(convertResult);
					HideDragChoiceOverlay();
					_wasAutoExpandedByDrag = false;
					e.Handled = true;
					return;
				}
			}

			Point point = ((DropTargetDock != null) ? e.GetPosition(DropTargetDock) : new Point(-1.0, -1.0));







			if ((DropTargetDock != null && point.X >= 0.0 && point.Y >= 0.0 && point.X <= DropTargetDock.ActualWidth && point.Y <= DropTargetDock.ActualHeight) || (ShortcutDockBorder != null && ShortcutDockBorder.IsMouseOver))







			{







				ViewModel.Shortcuts.AddFromDrop(array);







			}







			else







			{







				int addedCount = ViewModel.Shelf.AddFiles(array);







				ViewModel.NavigateToShelfSlide();
				PlayShelfDropEffects(addedCount, delayed: true);







			}







		}







		HideDragChoiceOverlay();







		_wasAutoExpandedByDrag = false;







		e.Handled = true;







	}















	private void DropTargetShelf_PreviewDragEnter(object sender, DragEventArgs e)







	{







		HighlightDropTarget(shelf: true);







		e.Effects = DragDropEffects.Copy;







		e.Handled = true;







	}















	private void DropTargetShelf_PreviewDragOver(object sender, DragEventArgs e)







	{







		HighlightDropTarget(shelf: true);







		e.Effects = DragDropEffects.Copy;







		e.Handled = true;







	}















		private void DropTargetShelf_PreviewDragLeave(object sender, DragEventArgs e)

	{

		if (DropTargetShelf != null)

		{

			Point p = e.GetPosition(DropTargetShelf);

			if (p.X >= 0 && p.Y >= 0 && p.X <= DropTargetShelf.ActualWidth && p.Y <= DropTargetShelf.ActualHeight)

			{

				return;

			}

		}

		ResetDropTargetsHighlight();

	}



private void DropTargetShelf_Drop(object sender, DragEventArgs e)







	{







		if (e.Handled) return;
		if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] array && array.Length != 0)







		{







			int addedCount = ViewModel.Shelf.AddFiles(array);







			ViewModel.NavigateToShelfSlide();
				PlayShelfDropEffects(addedCount, delayed: true);







		}







		HideDragChoiceOverlay();







		_wasAutoExpandedByDrag = false;







		e.Handled = true;







	}















	private void DropTargetDock_PreviewDragEnter(object sender, DragEventArgs e)







	{







		HighlightDropTarget(shelf: false);







		e.Effects = DragDropEffects.Copy;







		e.Handled = true;







	}















	private void DropTargetDock_PreviewDragOver(object sender, DragEventArgs e)







	{







		HighlightDropTarget(shelf: false);







		e.Effects = DragDropEffects.Copy;







		e.Handled = true;







	}















		private void DropTargetDock_PreviewDragLeave(object sender, DragEventArgs e)

	{

		if (DropTargetDock != null)

		{

			Point p = e.GetPosition(DropTargetDock);

			if (p.X >= 0 && p.Y >= 0 && p.X <= DropTargetDock.ActualWidth && p.Y <= DropTargetDock.ActualHeight)

			{

				return;

			}

		}

		ResetDropTargetsHighlight();

	}



private void DropTargetDock_Drop(object sender, DragEventArgs e)







	{







		if (e.Handled) return;
		if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] array && array.Length != 0)







		{







			ViewModel.Shortcuts.AddFromDrop(array);







		}







		HideDragChoiceOverlay();







		_wasAutoExpandedByDrag = false;







		e.Handled = true;







	}















	private void HighlightConvertTarget()
	{
		if (_currentDropHighlight == 2) return;
		_currentDropHighlight = 2;

		if (DropTargetShelf == null || DropTargetConvert == null || DropTargetDock == null) return;

		DropTargetShelf.Background = ShelfNormalBg;
		DropTargetShelf.BorderBrush = ShelfNormalBorder;
		if (DropTargetShelfBadgeText != null) DropTargetShelfBadgeText.Text = L10n["DropAction.ShelfBadge"];
		if (DropTargetShelfBadge != null) DropTargetShelfBadge.Background = new SolidColorBrush(Color.FromRgb(22, 34, 53));

		DropTargetConvert.Background = ConvertHoverBg;
		DropTargetConvert.BorderBrush = ConvertHoverBorder;
		if (DropTargetConvertBadgeText != null) DropTargetConvertBadgeText.Text = L10n["DropAction.ConvertBadgeActive"];
		if (DropTargetConvertBadge != null) DropTargetConvertBadge.Background = new SolidColorBrush(Color.FromArgb(64, 52, 211, 153));

		DropTargetDock.Background = DockNormalBg;
		DropTargetDock.BorderBrush = DockNormalBorder;
		if (DropTargetDockBadgeText != null) DropTargetDockBadgeText.Text = L10n["DropAction.DockBadge"];
		if (DropTargetDockBadge != null) DropTargetDockBadge.Background = new SolidColorBrush(Color.FromRgb(37, 26, 56));
	}

	private void ResetConvertTarget()
	{
		if (DropTargetConvert == null) return;

		DropTargetConvert.Background = ConvertNormalBg;
		DropTargetConvert.BorderBrush = ConvertNormalBorder;
		if (DropTargetConvertBadgeText != null) DropTargetConvertBadgeText.Text = L10n["DropAction.ConvertBadge"];
		if (DropTargetConvertBadge != null) DropTargetConvertBadge.Background = new SolidColorBrush(Color.FromRgb(18, 42, 36));
	}

	private void ShowConvertResultToast(ImageConvertResult result)
	{
		string title;
		string message;

		if (result.Converted > 0)
		{
			title = L10n["Convert.SuccessTitle"];
			message = result.LastOutputName ?? L10n.Get("Convert.SuccessMessage", result.Converted);
		}
		else if (result.Failed > 0)
		{
			title = L10n["Convert.FailedTitle"];
			message = L10n.Get("Convert.FailedMessage", result.Failed);
		}
		else
		{
			title = L10n["Convert.NothingTitle"];
			message = L10n["Convert.NothingMessage"];
		}

		ViewModel.TriggerTransientNotification("🔄", title, message);
	}

	private void DropTargetConvert_PreviewDragEnter(object sender, DragEventArgs e)
	{
		HighlightConvertTarget();
		e.Effects = DragDropEffects.Copy;
		e.Handled = true;
	}

	private void DropTargetConvert_PreviewDragOver(object sender, DragEventArgs e)
	{
		HighlightConvertTarget();
		e.Effects = DragDropEffects.Copy;
		e.Handled = true;
	}

	private void DropTargetConvert_PreviewDragLeave(object sender, DragEventArgs e)
	{
		if (DropTargetConvert != null)
		{
			Point p = e.GetPosition(DropTargetConvert);
			if (p.X >= 0 && p.Y >= 0 && p.X <= DropTargetConvert.ActualWidth && p.Y <= DropTargetConvert.ActualHeight)
			{
				return;
			}
		}

		ResetDropTargetsHighlight();
	}

	private void DropTargetConvert_Drop(object sender, DragEventArgs e)
	{
		if (e.Handled) return;
		if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] array && array.Length != 0)
		{
			var result = new ImageConvertService().ConvertFiles(array);
			ShowConvertResultToast(result);
		}

		HideDragChoiceOverlay();
		_wasAutoExpandedByDrag = false;
		e.Handled = true;
	}

		private int _currentDropHighlight = 0;



	private void HighlightDropTarget(bool shelf)

	{

		int target = shelf ? 1 : 3;

		if (_currentDropHighlight == target) return;

		_currentDropHighlight = target;
		ResetConvertTarget();







		if (DropTargetShelf == null || DropTargetDock == null)







		{







			return;







		}







		if (shelf)







		{







			DropTargetShelf.Background = ShelfHoverBg;







			DropTargetShelf.BorderBrush = ShelfHoverBorder;







			if (DropTargetShelfBadgeText != null)







			{







				DropTargetShelfBadgeText.Text = L10n["DropAction.ShelfBadgeActive"];







			}







			if (DropTargetShelfBadge != null)







			{







				DropTargetShelfBadge.Background = new SolidColorBrush(Color.FromArgb(64, 56, 189, 248));







			}







			DropTargetDock.Background = DockNormalBg;







			DropTargetDock.BorderBrush = DockNormalBorder;







			if (DropTargetDockBadgeText != null)







			{







				DropTargetDockBadgeText.Text = L10n["DropAction.DockBadge"];







			}







			if (DropTargetDockBadge != null)







			{







				DropTargetDockBadge.Background = new SolidColorBrush(Color.FromRgb(37, 26, 56));







			}







		}







		else







		{







			DropTargetDock.Background = DockHoverBg;







			DropTargetDock.BorderBrush = DockHoverBorder;







			if (DropTargetDockBadgeText != null)







			{







				DropTargetDockBadgeText.Text = L10n["DropAction.DockBadgeActive"];







			}







			if (DropTargetDockBadge != null)







			{







				DropTargetDockBadge.Background = new SolidColorBrush(Color.FromArgb(64, 167, 139, 250));







			}







			DropTargetShelf.Background = ShelfNormalBg;







			DropTargetShelf.BorderBrush = ShelfNormalBorder;







			if (DropTargetShelfBadgeText != null)







			{







				DropTargetShelfBadgeText.Text = L10n["DropAction.ShelfBadge"];







			}







			if (DropTargetShelfBadge != null)







			{







				DropTargetShelfBadge.Background = new SolidColorBrush(Color.FromRgb(22, 34, 53));







			}







		}







	}















		private void ResetDropTargetsHighlight()

	{

		if (_currentDropHighlight == 0) return;

		_currentDropHighlight = 0;
		ResetConvertTarget();







		if (DropTargetShelf != null)







		{







			DropTargetShelf.Background = ShelfNormalBg;







			DropTargetShelf.BorderBrush = ShelfNormalBorder;







			if (DropTargetShelfBadgeText != null)







			{







				DropTargetShelfBadgeText.Text = L10n["DropAction.ShelfBadge"];







			}







			if (DropTargetShelfBadge != null)







			{







				DropTargetShelfBadge.Background = new SolidColorBrush(Color.FromRgb(22, 34, 53));







			}







		}







		if (DropTargetDock != null)







		{







			DropTargetDock.Background = DockNormalBg;







			DropTargetDock.BorderBrush = DockNormalBorder;







			if (DropTargetDockBadgeText != null)







			{







				DropTargetDockBadgeText.Text = L10n["DropAction.DockBadge"];







			}







			if (DropTargetDockBadge != null)







			{







				DropTargetDockBadge.Background = new SolidColorBrush(Color.FromRgb(37, 26, 56));







			}







		}







	}















	private void ShowDragChoiceOverlay(bool show)







	{







		if (DragChoiceOverlay != null)







		{







			if (show)







			{







				ResetDropTargetsHighlight();







				DragChoiceOverlay.Visibility = Visibility.Visible;







			}







			else







			{







				DragChoiceOverlay.Visibility = Visibility.Collapsed;







				ResetDropTargetsHighlight();







			}







		}







	}















	private void HideDragChoiceOverlay()







	{







		ShowDragChoiceOverlay(show: false);







	}















	private void ShelfDropZone_Drop(object sender, DragEventArgs e)







	{







		if (e.Handled) return;
		if (e.Data.GetDataPresent(DataFormats.FileDrop))







		{







			if (e.Data.GetData(DataFormats.FileDrop) is string[] array && array.Length != 0)







			{







				int addedCount = ViewModel.Shelf.AddFiles(array);
				PlayShelfDropEffects(addedCount);







			}







			e.Handled = true;







		}







	}















	private void PlayShelfDropEffects(int addedCount = 1, bool delayed = false)
	{
		if (addedCount <= 0)
		{
			return;
		}

		int burstCount = Math.Clamp(addedCount, 1, 6);
		if (delayed)
		{
			_shelfDropEffectTimer?.Stop();
			_shelfDropEffectTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
			_shelfDropEffectTimer.Tick += (s, e) =>
			{
				_shelfDropEffectTimer!.Stop();
				PlayShelfDropEffectForNewItems(burstCount, attempt: 0);
			};
			_shelfDropEffectTimer.Start();
			return;
		}

		// Tunggu satu putaran layout agar border item yang baru muncul (shelf kosong -> berisi)
		// sudah Visible dan container item sudah digenerate.
		Dispatcher.BeginInvoke(new Action(() => PlayShelfDropEffectForNewItems(burstCount, attempt: 0)), DispatcherPriority.Loaded);
	}

	private void PlayShelfDropEffectForNewItems(int count, int attempt)
	{
		if (ShelfBurstCanvas == null || !ShelfBurstCanvas.IsVisible || ShelfItemsControl == null)
		{
			return;
		}

		ShelfItemsControl.UpdateLayout();
		var containers = new FrameworkElement?[count];
		int found = 0;
		for (int i = 0; i < count; i++)
		{
			containers[i] = ShelfItemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
			if (containers[i] != null)
			{
				found++;
			}
		}

		if (found < count && attempt < 3)
		{
			var retryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(70) };
			retryTimer.Tick += (s, e) =>
			{
				retryTimer.Stop();
				PlayShelfDropEffectForNewItems(count, attempt + 1);
			};
			retryTimer.Start();
			return;
		}

		// Satu ledakan per file yang baru masuk (indeks 0..count-1, terbaru paling kiri)
		// dengan stagger 90 ms kiri->kanan. Canvas memakai Panel.ZIndex="-1" agar
		// seluruh ledakan tampil di belakang kartu item.
		ShelfBurstCanvas.Children.Clear();

		bool spawned = false;
		var stagger = TimeSpan.Zero;
		for (int i = 0; i < containers.Length; i++)
		{
			FrameworkElement? container = containers[i];
			if (container == null)
			{
				continue;
			}

			Point? center = GetShelfBurstCenter(container);
			if (center == null)
			{
				continue;
			}

			SpawnShelfBurst(center.Value, stagger);
			AnimateShelfItemContainer(container, stagger);
			spawned = true;
			stagger += TimeSpan.FromMilliseconds(90);
		}

		if (!spawned)
		{
			var fallback = new Point(ShelfBurstCanvas.ActualWidth * 0.5, ShelfBurstCanvas.ActualHeight * 0.55);
			SpawnShelfBurst(fallback, TimeSpan.Zero);
		}
	}

	private Point? GetShelfBurstCenter(FrameworkElement container)
	{
		if (ShelfBurstCanvas.Parent is not Visual canvasParent)
		{
			return null;
		}

		try
		{
			// Canvas bukan ancestor container (keduanya saudara di grid yang sama),
			// jadi hitung lewat parent bersama lalu kurangi origin canvas.
			Point inParent = container.TransformToAncestor(canvasParent).Transform(new Point(container.ActualWidth * 0.5, container.ActualHeight * 0.5));
			Point canvasOrigin = ShelfBurstCanvas.TransformToAncestor(canvasParent).Transform(new Point(0, 0));
			var center = new Point(inParent.X - canvasOrigin.X, inParent.Y - canvasOrigin.Y);
			center.X = Math.Clamp(center.X, 4.0, Math.Max(4.0, ShelfBurstCanvas.ActualWidth - 4.0));
			center.Y = Math.Clamp(center.Y, 4.0, Math.Max(4.0, ShelfBurstCanvas.ActualHeight - 4.0));
			return center;
		}
		catch (InvalidOperationException)
		{
			return null;
		}
	}

	private void SpawnShelfBurst(Point center, TimeSpan beginTime)
	{
		Brush accentBrush = TryFindResource("AccentLightBrush") as Brush ?? Brushes.DeepSkyBlue;
		var sparkBrush = new SolidColorBrush(Color.FromArgb(235, 240, 249, 255));
		sparkBrush.Freeze();

		var ring = new Ellipse
		{
			Width = 30,
			Height = 30,
			Stroke = accentBrush,
			StrokeThickness = 1.8,
			IsHitTestVisible = false,
			RenderTransformOrigin = new Point(0.5, 0.5)
		};
		Canvas.SetLeft(ring, center.X - 15.0);
		Canvas.SetTop(ring, center.Y - 15.0);
		var ringScale = new ScaleTransform(0.4, 0.4);
		ring.RenderTransform = ringScale;
		ShelfBurstCanvas.Children.Add(ring);

		// Skala akhir 5.2 -> diameter ~156 px, cukup besar untuk keluar dari tepi
		// kartu item 108x90 yang menutupi bagian tengah ledakan. Fade sengaja lambat
		// di awal (QuadraticEase EaseIn) agar ring masih terlihat saat menyembul.
		var ringDuration = TimeSpan.FromMilliseconds(480);
		var ringEase = new QuarticEase { EasingMode = EasingMode.EaseOut };
		ringScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.4, 5.2, ringDuration) { BeginTime = beginTime, EasingFunction = ringEase });
		ringScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.4, 5.2, ringDuration) { BeginTime = beginTime, EasingFunction = ringEase });
		var ringFade = new DoubleAnimation(0.85, 0.0, ringDuration) { BeginTime = beginTime, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
		ringFade.Completed += (s, e) => ShelfBurstCanvas.Children.Remove(ring);
		ring.BeginAnimation(UIElement.OpacityProperty, ringFade);

		for (int i = 0; i < 22; i++)
		{
			double size = 3.0 + Random.Shared.NextDouble() * 4.5;
			var particle = new Ellipse
			{
				Width = size,
				Height = size,
				IsHitTestVisible = false,
				RenderTransformOrigin = new Point(0.5, 0.5),
				Fill = Random.Shared.NextDouble() < 0.35 ? sparkBrush : accentBrush
			};
			Canvas.SetLeft(particle, center.X - size / 2.0);
			Canvas.SetTop(particle, center.Y - size / 2.0);

			double angle = Random.Shared.NextDouble() * Math.PI * 2.0;
			double distance = 46.0 + Random.Shared.NextDouble() * 66.0;
			double targetX = Math.Cos(angle) * distance;
			double targetY = Math.Sin(angle) * distance - 9.0;

			var translate = new TranslateTransform(0.0, 0.0);
			var scale = new ScaleTransform(1.0, 1.0);
			var transformGroup = new TransformGroup();
			transformGroup.Children.Add(scale);
			transformGroup.Children.Add(translate);
			particle.RenderTransform = transformGroup;
			ShelfBurstCanvas.Children.Add(particle);

			var duration = TimeSpan.FromMilliseconds(420.0 + Random.Shared.NextDouble() * 280.0);
			var moveEase = new QuarticEase { EasingMode = EasingMode.EaseOut };
			translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, targetX, duration) { BeginTime = beginTime, EasingFunction = moveEase });
			translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0.0, targetY, duration) { BeginTime = beginTime, EasingFunction = moveEase });
			scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, 0.2, duration) { BeginTime = beginTime });
			scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, 0.2, duration) { BeginTime = beginTime });

			var fade = new DoubleAnimation(1.0, 0.0, duration)
			{
				BeginTime = beginTime + TimeSpan.FromMilliseconds(40.0),
				EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
			};
			fade.Completed += (s, e) => ShelfBurstCanvas.Children.Remove(particle);
			particle.BeginAnimation(UIElement.OpacityProperty, fade);
		}
	}

	private static void AnimateShelfItemContainer(FrameworkElement? container, TimeSpan beginTime)
	{
		if (container == null)
		{
			return;
		}

		var scale = new ScaleTransform(1.0, 1.0);
		container.RenderTransformOrigin = new Point(0.5, 0.5);
		container.RenderTransform = scale;

		var pop = new DoubleAnimationUsingKeyFrames { BeginTime = beginTime };
		pop.KeyFrames.Add(new EasingDoubleKeyFrame(0.55, KeyTime.FromTimeSpan(TimeSpan.Zero)));
		pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.08, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
		pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
		scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
		scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
	}

	private void ShelfDropZone_DragOver(object sender, DragEventArgs e)







	{







		if (e.Data.GetDataPresent(DataFormats.FileDrop))







		{







			e.Effects = DragDropEffects.Copy;







			e.Handled = true;







		}







	}















	private void ShelfEmptyState_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)







	{







		ViewModel.Shelf.PickFiles();



	}

	private void ShelfPhoneCopyLink_Click(object sender, RoutedEventArgs e)
	{
		string url = ViewModel.PhoneShare.Url;
		if (string.IsNullOrWhiteSpace(url)) return;

		try
		{
			Clipboard.SetText(url);
			ViewModel.TriggerTransientNotification("📱", LocalizationService.Instance.Get("Phone.WindowTitle"), LocalizationService.Instance.Get("Phone.LinkCopied"));
		}
		catch
		{
			// Clipboard bisa sementara terkunci aplikasi lain; URL tetap terlihat di panel.
		}
	}

	private void ShelfPhoneStop_Click(object sender, RoutedEventArgs e)
	{
		ViewModel.PhoneShare.Stop();
	}















	private void ShelfItemCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)







	{







		_shelfDragStartPoint = e.GetPosition(this);







	}















	private void ShelfItemCard_PreviewMouseMove(object sender, MouseEventArgs e)







	{







		if (e.LeftButton != MouseButtonState.Pressed || !(sender is FrameworkElement { DataContext: ShelfItem dataContext } frameworkElement))







		{







			return;







		}







		Point position = e.GetPosition(this);







		Vector vector = _shelfDragStartPoint - position;







		if ((Math.Abs(vector.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(vector.Y) > SystemParameters.MinimumVerticalDragDistance) && !string.IsNullOrWhiteSpace(dataContext.FilePath) && (File.Exists(dataContext.FilePath) || Directory.Exists(dataContext.FilePath)))







		{







			_isInternalDragOut = true;
			_shelfDropInsideNotch = false;







			try







			{







				new StringCollection().Add(dataContext.FilePath);







				DataObject data = new DataObject(DataFormats.FileDrop, new string[1] { dataContext.FilePath });







				DragDropEffects effects = DragDrop.DoDragDrop(frameworkElement, data, DragDropEffects.Copy);

				if (effects != DragDropEffects.None && !_shelfDropInsideNotch && ViewModel.SettingsService.CurrentSettings.ShelfRemoveAfterDrag)
				{
					ViewModel.Shelf.RemoveItem(dataContext);
				}







			}







			finally







			{







				_isInternalDragOut = false;







			}







		}







	}















	private void DockBorder_PreviewDragOver(object sender, DragEventArgs e)







	{







		if (e.Data.GetDataPresent(DataFormats.FileDrop))







		{







			e.Effects = DragDropEffects.Copy;







			e.Handled = true;







		}







	}















	private void DockBorder_Drop(object sender, DragEventArgs e)







	{







		if (e.Handled) return;
		if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] array && array.Length != 0)







		{







			ViewModel.Shortcuts.AddFromDrop(array);







			e.Handled = true;







		}







	}















	private void ShelfItemDeleteButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)







	{







		e.Handled = true;







		if (sender is FrameworkElement { DataContext: ShelfItem dataContext })







		{







			ViewModel.Shelf.RemoveItem(dataContext);







		}







	}















	private void ShelfItemDeleteButton_Click(object sender, RoutedEventArgs e)







	{







		e.Handled = true;







		if (sender is FrameworkElement { DataContext: ShelfItem dataContext })







		{







			ViewModel.Shelf.RemoveItem(dataContext);







		}







	}















	private void Window_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)







	{







		if (ViewModel.CurrentState == NotchState.Expanded)







		{







			ViewModel.CancelPendingCollapse();







		}







	}















	private void ShortcutContextMenu_Opened(object sender, RoutedEventArgs e)







	{







		_isContextMenuOpen = true;







		ViewModel.CancelPendingCollapse();







	}















	private void ShortcutContextMenu_Closed(object sender, RoutedEventArgs e)







	{







		_isContextMenuOpen = false;







		Dispatcher.BeginInvoke((Action)(() =>







		{







			if (ViewModel.CurrentState == NotchState.Expanded && !ViewModel.IsLockedExpanded && NativeMethods.GetCursorPos(out var lpPoint))







			{







				Point hitPoint = PillContainer.PointFromScreen(new Point(lpPoint.X, lpPoint.Y));







				if (!(_wasCursorInsideNotch = NotchBackgroundPath?.Data != null && NotchBackgroundPath.Data.FillContains(hitPoint)))







				{







					ViewModel.OnMouseLeave();







				}







			}







		}), DispatcherPriority.Input);







	}















	private void ShortcutDeleteMenuItem_Click(object sender, RoutedEventArgs e)







	{







		e.Handled = true;







		_isContextMenuOpen = false;







		ShortcutItem shortcutItem = (sender as MenuItem)?.DataContext as ShortcutItem;







		if (shortcutItem == null && (sender as MenuItem)?.Parent is ContextMenu { PlacementTarget: FrameworkElement placementTarget })







		{







			shortcutItem = placementTarget.DataContext as ShortcutItem;







		}







		if (shortcutItem != null)







		{







			ViewModel.Shortcuts.RemoveShortcut(shortcutItem);







		}







		Dispatcher.BeginInvoke((Action)(() =>







		{







			if (ViewModel.CurrentState == NotchState.Expanded && !ViewModel.IsLockedExpanded && NativeMethods.GetCursorPos(out var lpPoint))







			{







				Point hitPoint = PillContainer.PointFromScreen(new Point(lpPoint.X, lpPoint.Y));







				if (!(_wasCursorInsideNotch = NotchBackgroundPath?.Data != null && NotchBackgroundPath.Data.FillContains(hitPoint)))







				{







					ViewModel.OnMouseLeave();







				}







			}







		}), DispatcherPriority.Input);







	}















	private void ShortcutLaunchMenuItem_Click(object sender, RoutedEventArgs e)







	{







		e.Handled = true;







		_isContextMenuOpen = false;







		ShortcutItem shortcutItem = (sender as MenuItem)?.DataContext as ShortcutItem;







		if (shortcutItem == null && (sender as MenuItem)?.Parent is ContextMenu { PlacementTarget: FrameworkElement placementTarget })







		{







			shortcutItem = placementTarget.DataContext as ShortcutItem;







		}







		if (shortcutItem != null)







		{







			ViewModel.Shortcuts.Launch(shortcutItem);







		}







	}



























	#region Note Editor Formatting & Synchronization



	private bool _isUpdatingRichText = false;







	private void NoteViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)



	{



		if (e.PropertyName == nameof(NoteViewModel.SelectedNote))



		{



			Dispatcher.Invoke(() =>



			{



				LoadNoteIntoRichTextBox(ViewModel?.Note?.SelectedNote?.Content ?? "");



			});



		}



	}







	private void LoadNoteIntoRichTextBox(string content)



	{



		if (NoteRichTextBox == null) return;



		_isUpdatingRichText = true;



		try



		{



			NoteRichTextBox.Document.Blocks.Clear();



			if (string.IsNullOrWhiteSpace(content))



			{



				var p = new Paragraph { Margin = new Thickness(0) };



				NoteRichTextBox.Document.Blocks.Add(p);



				if (NotePlaceholder != null) NotePlaceholder.Visibility = Visibility.Visible;



				return;



			}







			string trimmed = content.TrimStart();



			if (trimmed.StartsWith("<Section") || trimmed.StartsWith("<FlowDocument"))



			{



				try



				{



					var range = new TextRange(NoteRichTextBox.Document.ContentStart, NoteRichTextBox.Document.ContentEnd);



					using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));



					range.Load(ms, DataFormats.Xaml);



					if (NotePlaceholder != null)



					{



						NotePlaceholder.Visibility = string.IsNullOrWhiteSpace(range.Text) ? Visibility.Visible : Visibility.Collapsed;



					}



					return;



				}



				catch



				{



					// Fallback to plain text on error



				}



			}







			var para = new Paragraph { Margin = new Thickness(0) };



			para.Inlines.Add(new Run(content));



			NoteRichTextBox.Document.Blocks.Add(para);



			if (NotePlaceholder != null)



			{



				NotePlaceholder.Visibility = string.IsNullOrWhiteSpace(content) ? Visibility.Visible : Visibility.Collapsed;



			}



		}



		catch { }



		finally



		{



			_isUpdatingRichText = false;



		}



	}







	private void NoteRichTextBox_TextChanged(object sender, TextChangedEventArgs e)



	{



		if (NoteRichTextBox == null) return;







		var range = new TextRange(NoteRichTextBox.Document.ContentStart, NoteRichTextBox.Document.ContentEnd);



		string plainText = range.Text;



		bool isEmpty = string.IsNullOrWhiteSpace(plainText);







		if (NotePlaceholder != null)



		{



			NotePlaceholder.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;



		}







		if (_isUpdatingRichText) return;







		try



		{



			using var ms = new MemoryStream();



			range.Save(ms, DataFormats.Xaml);



			string xaml = System.Text.Encoding.UTF8.GetString(ms.ToArray());



			ViewModel?.Note?.UpdateContentFromEditor(xaml, plainText);



		}



		catch { }



	}







	private void FormatBold_Click(object sender, RoutedEventArgs e)



	{



		if (NoteRichTextBox == null) return;



		EditingCommands.ToggleBold.Execute(null, NoteRichTextBox);



		NoteRichTextBox.Focus();



	}







	private void FormatItalic_Click(object sender, RoutedEventArgs e)



	{



		if (NoteRichTextBox == null) return;



		EditingCommands.ToggleItalic.Execute(null, NoteRichTextBox);



		NoteRichTextBox.Focus();



	}







	private void FormatStrike_Click(object sender, RoutedEventArgs e)



	{



		if (NoteRichTextBox == null) return;



		var selection = NoteRichTextBox.Selection;



		if (!selection.IsEmpty)



		{



			var current = selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;



			if (current != null && current.Count > 0 && current.Any(d => d.Location == TextDecorationLocation.Strikethrough))



			{



				selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);



			}



			else



			{



				selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Strikethrough);



			}



		}



		NoteRichTextBox.Focus();



	}







	private void FormatCode_Click(object sender, RoutedEventArgs e)



	{



		if (NoteRichTextBox == null) return;



		var selection = NoteRichTextBox.Selection;



		if (!selection.IsEmpty)



		{



			var currentFont = selection.GetPropertyValue(TextElement.FontFamilyProperty) as FontFamily;



			if (currentFont != null && currentFont.Source.Contains("Consolas"))



			{



				selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily("Segoe UI"));



				selection.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);



			}



			else



			{



				selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily("Consolas"));



				selection.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)));



			}



		}



		NoteRichTextBox.Focus();



	}







	private void FormatBullet_Click(object sender, RoutedEventArgs e)



	{



		if (NoteRichTextBox == null) return;



		EditingCommands.ToggleBullets.Execute(null, NoteRichTextBox);



		NoteRichTextBox.Focus();



	}







	private void FormatNumber_Click(object sender, RoutedEventArgs e)



	{



		if (NoteRichTextBox == null) return;



		EditingCommands.ToggleNumbering.Execute(null, NoteRichTextBox);



		NoteRichTextBox.Focus();



	}







		private void AddNote_Click(object sender, RoutedEventArgs e)

	{

		if (ViewModel?.Note != null)

		{

			ViewModel.Note.AddNoteCommand.Execute(null);

			LoadNoteIntoRichTextBox("");

			NoteRichTextBox?.Focus();

			if (NotePlaceholder != null)

			{

				NotePlaceholder.Visibility = Visibility.Visible;

			}

		}

	}







		private void CopyNote_Click(object sender, RoutedEventArgs e)



	{



		if (NoteRichTextBox == null) return;



		var text = new TextRange(NoteRichTextBox.Document.ContentStart, NoteRichTextBox.Document.ContentEnd).Text;



		if (!string.IsNullOrWhiteSpace(text))



		{



			try



			{



				Clipboard.SetText(text.TrimEnd());



				ViewModel?.Note?.TriggerToast("Catatan disalin ke Clipboard!");



			}



			catch { }



		}



	}



	#endregion







}



