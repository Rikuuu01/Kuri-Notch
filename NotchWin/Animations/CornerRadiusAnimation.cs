using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace NotchWin.Animations;

/// <summary>
/// Animasi interpolasi untuk <see cref="CornerRadius"/>. WPF tidak menyediakan
/// animasi bawaan untuk tipe ini, sedangkan morph slim bar butuh radius yang
/// berubah mulus agar sudut tidak terlihat melompat.
/// </summary>
public sealed class CornerRadiusAnimation : AnimationTimeline
{
    public CornerRadius? From { get; set; }

    public CornerRadius? To { get; set; }

    public IEasingFunction? EasingFunction { get; set; }

    public override Type TargetPropertyType => typeof(CornerRadius);

    protected override Freezable CreateInstanceCore() => new CornerRadiusAnimation();

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        double progress = animationClock.CurrentProgress ?? 1.0;
        if (EasingFunction != null)
        {
            progress = EasingFunction.Ease(progress);
        }

        CornerRadius from = From ?? (defaultOriginValue is CornerRadius origin ? origin : default);
        CornerRadius to = To ?? (defaultDestinationValue is CornerRadius destination ? destination : default);

        return new CornerRadius(
            Lerp(from.TopLeft, to.TopLeft, progress),
            Lerp(from.TopRight, to.TopRight, progress),
            Lerp(from.BottomRight, to.BottomRight, progress),
            Lerp(from.BottomLeft, to.BottomLeft, progress));
    }

    private static double Lerp(double from, double to, double progress)
        => from + ((to - from) * progress);
}
