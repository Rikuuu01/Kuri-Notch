using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using NotchWin.Interop;
using Serilog;

namespace NotchWin.Services
{
    public class TrayService : ITrayService
    {
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private ContextMenu? _contextMenu;
        private MenuItem? _settingsItem;
        private MenuItem? _toggleItem;
        private MenuItem? _exitItem;
        private IntPtr _windowHandle;

        public event EventHandler? OpenSettingsRequested;
        public event EventHandler? ToggleVisibilityRequested;
        public event EventHandler? ExitRequested;

        public void Initialize(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;

            try
            {
                _notifyIcon = new System.Windows.Forms.NotifyIcon
                {
                    Text = "Kuri - Dynamic Notch"
                };

                // Extract executable icon or use system application icon
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    try
                    {
                        _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Could not extract associated icon from exe");
                    }
                }

                if (_notifyIcon.Icon == null)
                {
                    _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                }

                _notifyIcon.Visible = true;

                _notifyIcon.MouseClick += (s, e) =>
                {
                    if (e.Button == System.Windows.Forms.MouseButtons.Left)
                    {
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
                        });
                    }
                    else if (e.Button == System.Windows.Forms.MouseButtons.Right)
                    {
                        ShowContextMenu();
                    }
                };

                _notifyIcon.DoubleClick += (s, e) =>
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
                    });
                };

                BuildContextMenu();

                LocalizationService.Instance.LanguageChanged += (s, lang) =>
                {
                    Application.Current?.Dispatcher.Invoke(UpdateMenuHeaders);
                };

                Log.Information("System Tray Icon initialized successfully via NotifyIcon.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize System Tray Icon");
            }
        }

        private void BuildContextMenu()
        {
            _contextMenu = new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1B)), // #18181B Dark Zinc
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2A)), // #27272A
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF5))
            };

            if (Application.Current?.TryFindResource("DarkContextMenu") is Style menuStyle)
            {
                _contextMenu.Style = menuStyle;
            }
            else
            {
                _contextMenu.Template = CreateDarkContextMenuTemplate();
            }

            _settingsItem = CreateMenuItem(LocalizationService.Instance["Tray.Settings"], (s, e) =>
            {
                OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
            });

            _toggleItem = CreateMenuItem(LocalizationService.Instance["Tray.Toggle"], (s, e) =>
            {
                ToggleVisibilityRequested?.Invoke(this, EventArgs.Empty);
            });

            _exitItem = CreateMenuItem(LocalizationService.Instance["Tray.Exit"], (s, e) =>
            {
                ExitRequested?.Invoke(this, EventArgs.Empty);
            });

            var separator = new Separator();
            if (Application.Current?.TryFindResource("DarkMenuSeparator") is Style sepStyle)
            {
                separator.Style = sepStyle;
            }
            else
            {
                separator.Template = CreateDarkSeparatorTemplate();
            }

            _contextMenu.Items.Add(_settingsItem);
            _contextMenu.Items.Add(_toggleItem);
            _contextMenu.Items.Add(separator);
            _contextMenu.Items.Add(_exitItem);
        }

        private void UpdateMenuHeaders()
        {
            if (_settingsItem != null) _settingsItem.Header = LocalizationService.Instance["Tray.Settings"];
            if (_toggleItem != null) _toggleItem.Header = LocalizationService.Instance["Tray.Toggle"];
            if (_exitItem != null) _exitItem.Header = LocalizationService.Instance["Tray.Exit"];
        }

        private static MenuItem CreateMenuItem(string header, RoutedEventHandler clickHandler)
        {
            var item = new MenuItem
            {
                Header = header,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF5)),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            if (Application.Current?.TryFindResource("DarkMenuItem") is Style itemStyle)
            {
                item.Style = itemStyle;
            }
            else
            {
                item.Template = CreateDarkMenuItemTemplate();
            }

            item.Click += clickHandler;
            return item;
        }

        private static ControlTemplate CreateDarkMenuItemTemplate()
        {
            var template = new ControlTemplate(typeof(MenuItem));

            var borderFactory = new FrameworkElementFactory(typeof(Border), "ItemBorder");
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(12, 7, 12, 7));
            borderFactory.SetValue(Border.MarginProperty, new Thickness(2, 1, 2, 1));
            borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderFactory.SetValue(Border.BorderBrushProperty, Brushes.Transparent);

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Left);
            contentPresenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);

            borderFactory.AppendChild(contentPresenter);
            template.VisualTree = borderFactory;

            var highlightTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
            highlightTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2A)), "ItemBorder"));
            highlightTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)), "ItemBorder"));
            highlightTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            template.Triggers.Add(highlightTrigger);

            return template;
        }

        private static ControlTemplate CreateDarkContextMenuTemplate()
        {
            var template = new ControlTemplate(typeof(ContextMenu));

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1B)));
            borderFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2A)));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(4));

            var stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.IsItemsHostProperty, true);

            borderFactory.AppendChild(stackPanelFactory);
            template.VisualTree = borderFactory;

            return template;
        }

        private static ControlTemplate CreateDarkSeparatorTemplate()
        {
            var template = new ControlTemplate(typeof(Separator));

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.HeightProperty, 1.0);
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2A)));
            borderFactory.SetValue(Border.MarginProperty, new Thickness(4, 3, 4, 3));

            template.VisualTree = borderFactory;
            return template;
        }

        public void HandleTrayMessage(IntPtr wParam, IntPtr lParam)
        {
            // WinForms NotifyIcon automatically handles its own message pump loop
        }

        private void ShowContextMenu()
        {
            if (_contextMenu == null)
                return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_windowHandle != IntPtr.Zero)
                {
                    NativeMethods.SetForegroundWindow(_windowHandle);
                }

                _contextMenu.Placement = PlacementMode.MousePoint;
                _contextMenu.IsOpen = true;
            });
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
                Log.Information("System Tray Icon disposed.");
            }
        }
    }
}
