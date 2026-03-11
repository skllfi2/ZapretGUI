using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;

namespace ZUI.Animations
{
    /// <summary>
    /// Лёгкий аниматор для обычных кнопок и иконок на страницах.
    /// Срабатывает по нажатию, управляется AppSettings.AnimButtons.
    /// </summary>
    public static class ButtonAnimator
    {
        /// <summary>
        /// Подключить pop-анимацию к кнопке.
        /// </summary>
        public static void Attach(Button button)
        {
            EnsureTransform(button);
            button.Click += (_, _) =>
            {
                if (AppSettings.AnimButtons)
                    PlayPop(button);
            };
        }

        /// <summary>
        /// Подключить анимацию к FontIcon внутри Border (карточки сервисов).
        /// </summary>
        public static void AttachToIcon(FrameworkElement element, string animType = "pop")
        {
            EnsureTransform(element);
            element.PointerPressed += (_, _) =>
            {
                if (AppSettings.AnimCards)
                    Play(element, animType);
            };
        }

        // ── Pop (пружинный отскок) ───────────────────────────────────────────

        private static void PlayPop(FrameworkElement el)
        {
            var ct = (CompositeTransform)el.RenderTransform;
            var sb = new Storyboard();

            var kfX = new DoubleAnimationUsingKeyFrames();
            var kfY = new DoubleAnimationUsingKeyFrames();
            Storyboard.SetTarget(kfX, ct); Storyboard.SetTargetProperty(kfX, "ScaleX");
            Storyboard.SetTarget(kfY, ct); Storyboard.SetTargetProperty(kfY, "ScaleY");

            foreach (var (ms, v) in new[] { (0, 1.0), (80, 0.92), (180, 1.06), (280, 0.97), (360, 1.0) })
            {
                kfX.KeyFrames.Add(Frame(ms, v));
                kfY.KeyFrames.Add(Frame(ms, v));
            }

            sb.Children.Add(kfX);
            sb.Children.Add(kfY);
            sb.Begin();
        }

        // ── Pulse (плавный пульс) ────────────────────────────────────────────

        private static void PlayPulse(FrameworkElement el)
        {
            var ct = (CompositeTransform)el.RenderTransform;
            var sb = new Storyboard();

            var kfX = new DoubleAnimationUsingKeyFrames();
            var kfY = new DoubleAnimationUsingKeyFrames();
            Storyboard.SetTarget(kfX, ct); Storyboard.SetTargetProperty(kfX, "ScaleX");
            Storyboard.SetTarget(kfY, ct); Storyboard.SetTargetProperty(kfY, "ScaleY");

            foreach (var (ms, v) in new[] { (0, 1.0), (160, 1.18), (380, 1.0) })
            {
                kfX.KeyFrames.Add(Frame(ms, v));
                kfY.KeyFrames.Add(Frame(ms, v));
            }

            sb.Children.Add(kfX);
            sb.Children.Add(kfY);
            sb.Begin();
        }

        private static void Play(FrameworkElement el, string type)
        {
            switch (type)
            {
                case "pulse": PlayPulse(el); break;
                default:      PlayPop(el);   break;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void EnsureTransform(FrameworkElement el)
        {
            if (el.RenderTransform is CompositeTransform) return;
            el.RenderTransform       = new CompositeTransform();
            el.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        }

        private static EasingDoubleKeyFrame Frame(int ms, double value) => new()
        {
            KeyTime       = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms)),
            Value         = value,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
    }
}
