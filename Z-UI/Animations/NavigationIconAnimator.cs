using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;

namespace ZUI.Animations;

// ─────────────────────────────────────────────────────────────────────────────
// NavigationIconAnimator — 205 иконок, 13 типов анимаций
// Каждая анимация подобрана под смысл иконки, срабатывает только по клику.
//
// Быстрый старт:
//   NavigationIconAnimator.Attach(NavSettings, "ic_settings"); // → Spin
//   NavigationIconAnimator.Attach(NavBell,     "ic_bell");     // → Ring
//
// Отключение (например из страницы настроек):
//   NavigationIconAnimator.Enabled = AppSettings.IconAnimations;
// ─────────────────────────────────────────────────────────────────────────────

public static class NavigationIconAnimator
{
    // ── Глобальный переключатель ──────────────────────────────────────────────
    /// <summary>
    /// Главный переключатель. false — все анимации подавлены.
    /// Синхронизируется с AppSettings.IconAnimationsEnabled.
    /// </summary>
    public static bool Enabled { get; set; } = true;

    // ── Типы анимаций ─────────────────────────────────────────────────────────
    public enum AnimationType
    {
        Spin,        // Полный оборот 360°        — settings, refresh, compass
        Ring,        // Маятник — звонок          — bell, phone, lock, warning
        Pop,         // Пружинный отскок          — home, plus, star, check
        DropBounce,  // Падение вниз              — download, save, map-pin
        LaunchUp,    // Взлёт вверх               — upload, mail, send, share
        Shake,       // Горизонтальная тряска     — trash, close, error, filter
        Pulse,       // Пульс — масштаб           — user, search, eye, wifi
        Grow,        // Рост данных по Y          — chart, activity, trending
        Tilt,        // Наклон — вставка          — key, edit, tag, scissors
        Sway,        // Медленное покачивание     — cloud, music, leaf, flag
        SlideRight,  // Скользит вправо           — arrow-right, skip-forward
        SlideLeft,   // Скользит влево            — arrow-left, skip-back
        Flip,        // Переворот по X            — toggle, switch, repeat
    }

    // ── Маппинг: 205 иконок → тип анимации ───────────────────────────────────
    private static readonly Dictionary<string, AnimationType> _map = new(
        StringComparer.OrdinalIgnoreCase)
    {
        // Navigation
        {"ic_home",             AnimationType.Pop},
        {"ic_home-2",           AnimationType.Pop},
        {"ic_menu",             AnimationType.Pop},
        {"ic_sidebar",          AnimationType.SlideRight},
        {"ic_layout",           AnimationType.Pop},
        {"ic_maximize",         AnimationType.Pop},
        {"ic_minimize",         AnimationType.Pop},
        {"ic_external-link",    AnimationType.LaunchUp},
        {"ic_corner-up-left",   AnimationType.Tilt},
        {"ic_corner-up-right",  AnimationType.Tilt},
        // Arrows
        {"ic_arrow-up",         AnimationType.LaunchUp},
        {"ic_arrow-down",       AnimationType.DropBounce},
        {"ic_arrow-left",       AnimationType.SlideLeft},
        {"ic_arrow-right",      AnimationType.SlideRight},
        {"ic_arrow-up-right",   AnimationType.LaunchUp},
        {"ic_arrow-down-left",  AnimationType.DropBounce},
        {"ic_arrows-left-right",AnimationType.Flip},
        {"ic_arrows-up-down",   AnimationType.Flip},
        {"ic_chevrons-left",    AnimationType.SlideLeft},
        {"ic_chevrons-right",   AnimationType.SlideRight},
        {"ic_move",             AnimationType.Pulse},
        // Actions
        {"ic_search",           AnimationType.Pulse},
        {"ic_plus",             AnimationType.Pop},
        {"ic_close",            AnimationType.Shake},
        {"ic_check",            AnimationType.Pop},
        {"ic_edit",             AnimationType.Tilt},
        {"ic_copy",             AnimationType.Tilt},
        {"ic_trash",            AnimationType.Shake},
        {"ic_save",             AnimationType.DropBounce},
        {"ic_refresh",          AnimationType.Spin},
        {"ic_upload",           AnimationType.LaunchUp},
        {"ic_download",         AnimationType.DropBounce},
        {"ic_share",            AnimationType.LaunchUp},
        {"ic_filter",           AnimationType.Shake},
        {"ic_link",             AnimationType.Tilt},
        {"ic_paperclip",        AnimationType.Tilt},
        {"ic_scissors",         AnimationType.Tilt},
        {"ic_crop",             AnimationType.Tilt},
        {"ic_rotate-cw",        AnimationType.Spin},
        {"ic_rotate-ccw",       AnimationType.Spin},
        {"ic_zoom-in",          AnimationType.Pop},
        {"ic_zoom-out",         AnimationType.Shake},
        {"ic_wand",             AnimationType.Spin},
        {"ic_tool",             AnimationType.Tilt},
        {"ic_wrench",           AnimationType.Tilt},
        // Files
        {"ic_file",             AnimationType.Pop},
        {"ic_file-text",        AnimationType.Tilt},
        {"ic_file-code",        AnimationType.Tilt},
        {"ic_file-image",       AnimationType.Pop},
        {"ic_file-zip",         AnimationType.DropBounce},
        {"ic_folder",           AnimationType.Pop},
        {"ic_folder-open",      AnimationType.Pop},
        {"ic_folder-plus",      AnimationType.Pop},
        {"ic_clipboard",        AnimationType.Tilt},
        {"ic_clipboard-check",  AnimationType.Pop},
        {"ic_database",         AnimationType.Grow},
        {"ic_hard-drive",       AnimationType.DropBounce},
        {"ic_server",           AnimationType.Pulse},
        {"ic_package",          AnimationType.DropBounce},
        {"ic_layers",           AnimationType.Grow},
        // Communication
        {"ic_bell",             AnimationType.Ring},
        {"ic_bell-off",         AnimationType.Shake},
        {"ic_mail",             AnimationType.LaunchUp},
        {"ic_inbox",            AnimationType.DropBounce},
        {"ic_send",             AnimationType.LaunchUp},
        {"ic_message",          AnimationType.Pop},
        {"ic_message-circle",   AnimationType.Pop},
        {"ic_phone",            AnimationType.Ring},
        {"ic_phone-call",       AnimationType.Ring},
        {"ic_video",            AnimationType.Pop},
        {"ic_video-off",        AnimationType.Shake},
        {"ic_at-sign",          AnimationType.Spin},
        {"ic_rss",              AnimationType.Pulse},
        {"ic_globe",            AnimationType.Spin},
        {"ic_wifi",             AnimationType.Pulse},
        {"ic_bluetooth",        AnimationType.Pulse},
        // People
        {"ic_user",             AnimationType.Pulse},
        {"ic_users",            AnimationType.Pulse},
        {"ic_user-plus",        AnimationType.Pop},
        {"ic_user-check",       AnimationType.Pop},
        {"ic_user-x",           AnimationType.Shake},
        {"ic_award",            AnimationType.Pop},
        {"ic_badge",            AnimationType.Pop},
        {"ic_graduation-cap",   AnimationType.Tilt},
        {"ic_briefcase",        AnimationType.DropBounce},
        // Media
        {"ic_image",            AnimationType.Pop},
        {"ic_camera",           AnimationType.Pop},
        {"ic_film",             AnimationType.Spin},
        {"ic_tv",               AnimationType.Pulse},
        {"ic_monitor",          AnimationType.Pulse},
        {"ic_printer",          AnimationType.DropBounce},
        {"ic_music",            AnimationType.Sway},
        {"ic_headphones",       AnimationType.Sway},
        {"ic_mic",              AnimationType.Pulse},
        {"ic_mic-off",          AnimationType.Shake},
        {"ic_volume",           AnimationType.Pulse},
        {"ic_volume-2",         AnimationType.Pulse},
        {"ic_volume-x",         AnimationType.Shake},
        {"ic_play",             AnimationType.Pop},
        {"ic_pause",            AnimationType.Pop},
        {"ic_stop",             AnimationType.Pop},
        {"ic_skip-forward",     AnimationType.SlideRight},
        {"ic_skip-back",        AnimationType.SlideLeft},
        {"ic_repeat",           AnimationType.Spin},
        {"ic_shuffle",          AnimationType.Spin},
        // Dev
        {"ic_code",             AnimationType.Tilt},
        {"ic_code-2",           AnimationType.Tilt},
        {"ic_terminal",         AnimationType.Tilt},
        {"ic_git-branch",       AnimationType.Grow},
        {"ic_git-commit",       AnimationType.Pop},
        {"ic_git-merge",        AnimationType.Pop},
        {"ic_git-pull-request", AnimationType.LaunchUp},
        {"ic_bug",              AnimationType.Shake},
        {"ic_cpu",              AnimationType.Pulse},
        {"ic_zap",              AnimationType.Grow},
        {"ic_sliders",          AnimationType.Tilt},
        {"ic_switch",           AnimationType.Flip},
        {"ic_switch-on",        AnimationType.Flip},
        {"ic_toggle-left",      AnimationType.Flip},
        {"ic_toggle-right",     AnimationType.Flip},
        // Data
        {"ic_chart",            AnimationType.Grow},
        {"ic_bar-chart-2",      AnimationType.Grow},
        {"ic_pie-chart",        AnimationType.Spin},
        {"ic_activity",         AnimationType.Grow},
        {"ic_trending-up",      AnimationType.LaunchUp},
        {"ic_trending-down",    AnimationType.DropBounce},
        {"ic_percent",          AnimationType.Spin},
        {"ic_grid",             AnimationType.Pop},
        {"ic_list",             AnimationType.Pop},
        {"ic_tag",              AnimationType.Tilt},
        // Finance
        {"ic_credit-card",      AnimationType.Tilt},
        {"ic_shopping-cart",    AnimationType.SlideRight},
        {"ic_shopping-bag",     AnimationType.DropBounce},
        {"ic_dollar-sign",      AnimationType.Grow},
        // Location
        {"ic_map",              AnimationType.Pulse},
        {"ic_map-pin",          AnimationType.DropBounce},
        {"ic_navigation",       AnimationType.LaunchUp},
        {"ic_compass",          AnimationType.Spin},
        // Weather
        {"ic_sun",              AnimationType.Spin},
        {"ic_moon",             AnimationType.Sway},
        {"ic_cloud",            AnimationType.Sway},
        {"ic_cloud-rain",       AnimationType.Sway},
        {"ic_cloud-snow",       AnimationType.Sway},
        {"ic_wind",             AnimationType.Sway},
        {"ic_thermometer",      AnimationType.Grow},
        {"ic_droplet",          AnimationType.DropBounce},
        {"ic_umbrella",         AnimationType.Sway},
        // Time
        {"ic_calendar",         AnimationType.Pop},
        {"ic_clock",            AnimationType.Spin},
        {"ic_clock-history",    AnimationType.Spin},
        // Security
        {"ic_lock",             AnimationType.Ring},
        {"ic_unlock",           AnimationType.Ring},
        {"ic_shield",           AnimationType.Pulse},
        {"ic_shield-check",     AnimationType.Pop},
        {"ic_key",              AnimationType.Tilt},
        {"ic_eye",              AnimationType.Pulse},
        {"ic_eye-off",          AnimationType.Shake},
        // Status
        {"ic_info",             AnimationType.Pulse},
        {"ic_info_filled",      AnimationType.Pulse},
        {"ic_warning",          AnimationType.Ring},
        {"ic_warning_filled",   AnimationType.Ring},
        {"ic_error",            AnimationType.Shake},
        {"ic_success",          AnimationType.Pop},
        {"ic_alert-circle",     AnimationType.Ring},
        {"ic_help-circle",      AnimationType.Pulse},
        {"ic_power",            AnimationType.Spin},
        // System
        {"ic_settings",         AnimationType.Spin},
        // Misc
        {"ic_heart",            AnimationType.Pop},
        {"ic_heart-pulse",      AnimationType.Pulse},
        {"ic_star",             AnimationType.Pop},
        {"ic_bookmark",         AnimationType.Pop},
        {"ic_bookmark-check",   AnimationType.Pop},
        {"ic_flag",             AnimationType.Sway},
        {"ic_thumbs-up",        AnimationType.Pop},
        {"ic_thumbs-down",      AnimationType.DropBounce},
        {"ic_pocket",           AnimationType.Pop},
        {"ic_gift",             AnimationType.Pop},
        {"ic_smile",            AnimationType.Pop},
        {"ic_meh",              AnimationType.Sway},
        {"ic_frown",            AnimationType.Shake},
        {"ic_anchor",           AnimationType.DropBounce},
        {"ic_feather",          AnimationType.Sway},
        {"ic_hash",             AnimationType.Pop},
        {"ic_type",             AnimationType.Tilt},
        {"ic_align-left",       AnimationType.Pop},
        {"ic_align-center",     AnimationType.Pop},
        {"ic_bold",             AnimationType.Pop},
        {"ic_italic",           AnimationType.Tilt},
        {"ic_book",             AnimationType.Tilt},
        {"ic_book-open",        AnimationType.Pop},
        {"ic_newspaper",        AnimationType.SlideRight},
        // Transport
        {"ic_truck",            AnimationType.SlideRight},
        {"ic_car",              AnimationType.SlideRight},
        {"ic_bike",             AnimationType.SlideRight},
        // Nature / Health
        {"ic_leaf",             AnimationType.Sway},
        {"ic_tree",             AnimationType.Sway},
        {"ic_dumbbell",         AnimationType.Tilt},
        // Places
        {"ic_building",         AnimationType.Pulse},
        // Brands
        {"ic_discord",          AnimationType.Pulse},
        {"ic_youtube",          AnimationType.Pop},
        {"ic_google",           AnimationType.Spin},
        {"ic_github",           AnimationType.Sway},
        {"ic_telegram",         AnimationType.LaunchUp},
        {"ic_vk",               AnimationType.Pop},
        {"ic_x-twitter",        AnimationType.Shake},
    };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Подключить анимацию по имени иконки. Тип подбирается автоматически.
    /// </summary>
    public static void Attach(NavigationViewItem item, string iconName)
    {
        var type = _map.TryGetValue(iconName, out var t) ? t : AnimationType.Pop;
        Attach(item, type);
    }

    /// <summary>Подключить конкретный тип анимации явно.</summary>
    public static void Attach(NavigationViewItem item, AnimationType type)
    {
        EnsureTransform(item);
        item.PointerPressed += (_, _) => { if (Enabled) Play(item, type); };
    }

    /// <summary>Подключить сразу несколько иконок одним вызовом.</summary>
    public static void AttachAll(Dictionary<NavigationViewItem, string> map)
    {
        foreach (var (item, name) in map) Attach(item, name);
    }

    /// <summary>Узнать тип анимации для иконки (для отображения в UI).</summary>
    public static AnimationType GetAnimationType(string iconName) =>
        _map.TryGetValue(iconName, out var t) ? t : AnimationType.Pop;

    // ── Transform setup ───────────────────────────────────────────────────────

    private static void EnsureTransform(NavigationViewItem item)
    {
        if (item.Icon is null || item.Icon.RenderTransform is TransformGroup) return;
        var g = new TransformGroup();
        g.Children.Add(new RotateTransform { CenterX = 12, CenterY = 12 });
        g.Children.Add(new CompositeTransform());
        item.Icon.RenderTransform       = g;
        item.Icon.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
    }

    private static (RotateTransform R, CompositeTransform C) T(NavigationViewItem item)
    {
        var g = (TransformGroup)item.Icon!.RenderTransform;
        return ((RotateTransform)g.Children[0], (CompositeTransform)g.Children[1]);
    }

    // ── Animation dispatcher ──────────────────────────────────────────────────

    private static void Play(NavigationViewItem item, AnimationType type)
    {
        if (item.Icon is null) return;
        var (rot, comp) = T(item);
        var sb = new Storyboard();

        switch (type)
        {
            case AnimationType.Spin:
                Rot(sb, rot, 0, 360, 520, new CubicEase { EasingMode = EasingMode.EaseOut });
                break;
            case AnimationType.Ring:
                RotK(sb, rot, (0,0),(70,22),(170,-17),(270,12),(360,-7),(440,4),(520,0));
                break;
            case AnimationType.Pop:
                SclK(sb, comp, (0,1,1),(90,1.32,1.32),(210,.90,.90),(310,1.08,1.08),(420,1,1));
                break;
            case AnimationType.DropBounce:
                TrYK(sb, comp, (0,0),(130,7),(250,-3.5),(350,1.5),(420,0));
                break;
            case AnimationType.LaunchUp:
                TrYK(sb, comp, (0,0),(120,-8),(260,2.5),(360,-1),(430,0));
                break;
            case AnimationType.Shake:
                TrXK(sb, comp, (0,0),(55,-5),(110,5),(170,-4),(230,4),(290,-2.5),(350,2),(400,0));
                break;
            case AnimationType.Pulse:
                SclK(sb, comp, (0,1,1),(180,1.20,1.20),(420,1,1));
                break;
            case AnimationType.Grow:
                SclYK(sb, comp, (0,1),(90,.72),(230,1.28),(350,.94),(430,1));
                break;
            case AnimationType.Tilt:
                RotK(sb, rot, (0,0),(85,-22),(195,18),(295,-10),(385,6),(480,0));
                break;
            case AnimationType.Sway:
                RotK(sb, rot, (0,0),(175,-14),(350,14),(525,-7),(700,0));
                break;
            case AnimationType.SlideRight:
                TrXK(sb, comp, (0,0),(110,7),(230,-2),(320,1),(380,0));
                break;
            case AnimationType.SlideLeft:
                TrXK(sb, comp, (0,0),(110,-7),(230,2),(320,-1),(380,0));
                break;
            case AnimationType.Flip:
                SclXK(sb, comp, (0,1),(100,0),(200,1),(280,.5),(400,1));
                break;
        }

        sb.Begin();
    }

    // ── Storyboard helpers ────────────────────────────────────────────────────

    static EasingDoubleKeyFrame F(int ms, double v) => new()
    {
        KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms)),
        Value = v,
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
    };

    static void Rot(Storyboard sb, RotateTransform t, double f, double to,
        int ms, EasingFunctionBase? e = null)
    {
        var a = new DoubleAnimation { From=f, To=to, Duration=TimeSpan.FromMilliseconds(ms), EasingFunction=e };
        Storyboard.SetTarget(a, t); Storyboard.SetTargetProperty(a, "Angle");
        sb.Children.Add(a);
    }

    static void RotK(Storyboard sb, RotateTransform t, params (int ms, double v)[] kf)
    {
        var a = new DoubleAnimationUsingKeyFrames();
        Storyboard.SetTarget(a, t); Storyboard.SetTargetProperty(a, "Angle");
        foreach (var (ms, v) in kf) a.KeyFrames.Add(F(ms, v));
        sb.Children.Add(a);
    }

    static void SclK(Storyboard sb, CompositeTransform t, params (int ms, double sx, double sy)[] kf)
    {
        var ax = new DoubleAnimationUsingKeyFrames();
        var ay = new DoubleAnimationUsingKeyFrames();
        Storyboard.SetTarget(ax, t); Storyboard.SetTargetProperty(ax, "ScaleX");
        Storyboard.SetTarget(ay, t); Storyboard.SetTargetProperty(ay, "ScaleY");
        foreach (var (ms, sx, sy) in kf) { ax.KeyFrames.Add(F(ms,sx)); ay.KeyFrames.Add(F(ms,sy)); }
        sb.Children.Add(ax); sb.Children.Add(ay);
    }

    static void SclYK(Storyboard sb, CompositeTransform t, params (int ms, double v)[] kf)
    {
        var a = new DoubleAnimationUsingKeyFrames();
        Storyboard.SetTarget(a, t); Storyboard.SetTargetProperty(a, "ScaleY");
        foreach (var (ms, v) in kf) a.KeyFrames.Add(F(ms, v));
        sb.Children.Add(a);
    }

    static void SclXK(Storyboard sb, CompositeTransform t, params (int ms, double v)[] kf)
    {
        var a = new DoubleAnimationUsingKeyFrames();
        Storyboard.SetTarget(a, t); Storyboard.SetTargetProperty(a, "ScaleX");
        foreach (var (ms, v) in kf) a.KeyFrames.Add(F(ms, v));
        sb.Children.Add(a);
    }

    static void TrYK(Storyboard sb, CompositeTransform t, params (int ms, double v)[] kf)
    {
        var a = new DoubleAnimationUsingKeyFrames();
        Storyboard.SetTarget(a, t); Storyboard.SetTargetProperty(a, "TranslateY");
        foreach (var (ms, v) in kf) a.KeyFrames.Add(F(ms, v));
        sb.Children.Add(a);
    }

    static void TrXK(Storyboard sb, CompositeTransform t, params (int ms, double v)[] kf)
    {
        var a = new DoubleAnimationUsingKeyFrames();
        Storyboard.SetTarget(a, t); Storyboard.SetTargetProperty(a, "TranslateX");
        foreach (var (ms, v) in kf) a.KeyFrames.Add(F(ms, v));
        sb.Children.Add(a);
    }
}
