using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NotchWin.ViewModels;

namespace NotchWin.Views
{
    public partial class SettingsWindow : Window
    {
        private int _lastCategoryIndex = 0;

        public SettingsWindow(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            _lastCategoryIndex = viewModel.SelectedCategoryIndex;

            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.SelectedCategoryIndex))
                {
                    Dispatcher.Invoke(() => AnimateCategorySwitch(viewModel.SelectedCategoryIndex));
                }
            };
        }

        private void AnimateCategorySwitch(int newIndex)
        {
            if (CategoryContentHost == null || ContentTranslate == null) return;

            bool isNavigatingDown = newIndex >= _lastCategoryIndex;
            _lastCategoryIndex = newIndex;

            SettingsScrollViewer?.ScrollToTop();

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(240);

            // Subtle vertical direction based on navigation direction (up vs down)
            double startOffset = isNavigatingDown ? 14.0 : -14.0;

            // 1. Content Panel Transition: Fade in (0.0 -> 1.0) and gentle slide into place
            var contentFadeAnim = new DoubleAnimation(0.0, 1.0, duration) { EasingFunction = ease };
            var contentSlideAnim = new DoubleAnimation(startOffset, 0.0, duration) { EasingFunction = ease };

            CategoryContentHost.BeginAnimation(UIElement.OpacityProperty, contentFadeAnim, HandoffBehavior.SnapshotAndReplace);
            ContentTranslate.BeginAnimation(TranslateTransform.YProperty, contentSlideAnim, HandoffBehavior.SnapshotAndReplace);

            // 2. Header Title Transition: Fade in + slight slide
            if (CategoryHeaderHost != null && HeaderTranslate != null)
            {
                var headerDuration = TimeSpan.FromMilliseconds(200);
                var headerFadeAnim = new DoubleAnimation(0.2, 1.0, headerDuration) { EasingFunction = ease };
                var headerSlideAnim = new DoubleAnimation(isNavigatingDown ? 8.0 : -8.0, 0.0, headerDuration) { EasingFunction = ease };

                CategoryHeaderHost.BeginAnimation(UIElement.OpacityProperty, headerFadeAnim, HandoffBehavior.SnapshotAndReplace);
                HeaderTranslate.BeginAnimation(TranslateTransform.YProperty, headerSlideAnim, HandoffBehavior.SnapshotAndReplace);
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Instead of closing and disposing, cancel close and hide the window
            // so that it can be quickly toggled from the tray context menu
            e.Cancel = true;
            Hide();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                Hide();
            }
        }
    }
}
