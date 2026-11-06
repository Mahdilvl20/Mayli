using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI
{
    /// <summary>
    /// Crisp, hardware-accelerated vector geometries and icon factory for TimerToolView.
    /// 100% vector-based: zero missing-font tofu boxes, perfectly scaled on any DPI, theme-reactive.
    /// </summary>
    public static class TimerIcons
    {
        // -------------------------------------------------------------
        // FROZEN VECTOR GEOMETRIES (Standard 24x24 coordinate box)
        // -------------------------------------------------------------

        // Focus / Pomodoro: Precision Focus Timer
        public static readonly Geometry PomodoroGeometry = Freeze(Geometry.Parse(
            "M 12,2 V 5 M 12,21 A 8,8 0 1 0 12,5 A 8,8 0 1 0 12,21 Z M 12,8 V 12 L 15,14"));

        // Countdown: Elegant Hourglass
        public static readonly Geometry HourglassGeometry = Freeze(Geometry.Parse(
            "M 5,3 H 19 M 5,21 H 19 M 6.5,3 L 12,12 L 17.5,3 M 6.5,21 L 12,12 L 17.5,21"));

        // Alarm Clock: Twin-bell Clock
        public static readonly Geometry AlarmClockGeometry = Freeze(Geometry.Parse(
            "M 12,13 L 12,9 M 12,13 L 15,14 M 5,3 L 2,6 M 19,3 L 22,6 M 6.4,19.6 L 4,22 M 17.6,19.6 L 20,22 M 12,21 A 8,8 0 1 0 12,5 A 8,8 0 1 0 12,21 Z"));

        // Stopwatch: Precision Chronometer with top pusher
        public static readonly Geometry StopwatchGeometry = Freeze(Geometry.Parse(
            "M 12,9 V 13 L 15,15 M 10,2 H 14 M 12,2 V 5 M 12,22 A 8.5,8.5 0 1 0 12,5 A 8.5,8.5 0 1 0 12,22 Z"));

        // Hydration: Water Droplet
        public static readonly Geometry WaterDropGeometry = Freeze(Geometry.Parse(
            "M 12,2.5 C 12,2.5 5.5,10.5 5.5,15.5 A 6.5,6.5 0 0 0 18.5,15.5 C 18.5,10.5 12,2.5 12,2.5 Z"));

        // Play: Solid Triangle
        public static readonly Geometry PlayGeometry = Freeze(Geometry.Parse(
            "M 8,5 L 18,12 L 8,19 Z"));

        // Pause: Twin Vertical Bars
        public static readonly Geometry PauseGeometry = Freeze(Geometry.Parse(
            "M 7,5 H 10 V 19 H 7 Z M 14,5 H 17 V 19 H 14 Z"));

        // Reset / Reload: Circular Arrow
        public static readonly Geometry ResetGeometry = Freeze(Geometry.Parse(
            "M 3.5,12 A 8.5,8.5 0 1 0 6,5.8 L 3.5,8.5 M 3.5,3.5 V 8.5 H 8.5"));

        // Skip Next Phase: Triangle + Bar
        public static readonly Geometry SkipGeometry = Freeze(Geometry.Parse(
            "M 5,5 L 14,12 L 5,19 Z M 16,5 H 18.5 V 19 H 16 Z"));

        // Loop / Repeat: Dual Curved Arrows
        public static readonly Geometry LoopGeometry = Freeze(Geometry.Parse(
            "M 17,2 L 21,6 L 17,10 M 3,11 V 9 C 3,7.3 4.3,6 6,6 H 21 M 7,22 L 3,18 L 7,14 M 21,13 V 15 C 21,16.7 19.7,18 18,18 H 3"));

        // Lap: Flag
        public static readonly Geometry FlagGeometry = Freeze(Geometry.Parse(
            "M 5,14 C 8,12.5 11,16 15,14 C 18,12.5 19,13.5 19,13.5 V 4 C 19,4 18,3 15,4.5 C 11,6 8,2.5 5,4 V 21"));

        // Copy: Overlapping Documents
        public static readonly Geometry CopyGeometry = Freeze(Geometry.Parse(
            "M 9,3 H 19 V 17 H 9 Z M 5,7 H 15 V 21 H 5 Z"));

        // Chevron Down
        public static readonly Geometry ChevronDownGeometry = Freeze(Geometry.Parse(
            "M 3,5 L 8,10 L 13,5"));

        // Bell: Notification / Sound Tone
        public static readonly Geometry BellGeometry = Freeze(Geometry.Parse(
            "M 18,8 A 6,6 0 0 0 6,8 C 6,15 3,17 3,17 H 21 S 18,15 18,8 M 13.7,21 A 2,2 0 0 1 10.3,21"));

        // Moon: Snooze
        public static readonly Geometry MoonGeometry = Freeze(Geometry.Parse(
            "M 21,12.8 A 9,9 0 1 1 11.2,3 A 7,7 0 0 0 21,12.8 Z"));

        // Speaker: Test Sound Tone
        public static readonly Geometry SpeakerGeometry = Freeze(Geometry.Parse(
            "M 11,5 L 6,9 H 2 V 15 H 6 L 11,19 V 5 Z M 15.5,8.5 A 5,5 0 0 1 15.5,15.5"));

        // Plus
        public static readonly Geometry PlusGeometry = Freeze(Geometry.Parse(
            "M 12,5 V 19 M 5,12 H 19"));

        // Minus
        public static readonly Geometry MinusGeometry = Freeze(Geometry.Parse(
            "M 5,12 H 19"));

        // Close / Remove
        public static readonly Geometry CloseGeometry = Freeze(Geometry.Parse(
            "M 5,5 L 19,19 M 19,5 L 5,19"));

        // Code: Brackets
        public static readonly Geometry CodeGeometry = Freeze(Geometry.Parse(
            "M 7,8 L 3,12 L 7,16 M 17,8 L 21,12 L 17,16 M 14,4 L 10,20"));

        // Study: Open Book
        public static readonly Geometry BookGeometry = Freeze(Geometry.Parse(
            "M 4,19.5 C 6,19 9,19 12,21 C 15,19 18,19 20,19.5 V 4.5 C 18,4 15,4 12,6 C 9,4 6,4 4,4.5 Z"));

        // Focus: Crosshair / Target
        public static readonly Geometry TargetGeometry = Freeze(Geometry.Parse(
            "M 12,2 V 5 M 12,19 V 22 M 2,12 H 5 M 19,12 H 22 M 12,18 A 6,6 0 1 0 12,6 A 6,6 0 1 0 12,18 Z M 12,14 A 2,2 0 1 0 12,10 A 2,2 0 1 0 12,14 Z"));

        // Write: Pencil / Quill
        public static readonly Geometry PencilGeometry = Freeze(Geometry.Parse(
            "M 14,3 L 18,7 L 7,18 L 3,19 L 4,15 Z"));

        private static Geometry Freeze(Geometry geom)
        {
            if (geom.CanFreeze) geom.Freeze();
            return geom;
        }

        // -------------------------------------------------------------
        // FACTORY METHODS
        // -------------------------------------------------------------

        public static Viewbox CreateIcon(
            Geometry geometry,
            double size = 16,
            Brush stroke = null,
            Brush fill = null,
            double strokeThickness = 1.6)
        {
            var p = CapsuleTheme.Current;
            var path = new Path
            {
                Data = geometry,
                Stroke = stroke ?? CapsuleTheme.Brush(p.Text),
                Fill = fill ?? Brushes.Transparent,
                StrokeThickness = strokeThickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            var vb = new Viewbox
            {
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                Child = path,
                IsHitTestVisible = false
            };
            return vb;
        }

        public static Border CreateCircleButton(
            Geometry geometry,
            double size,
            string toolTip,
            Action onClick,
            out Path iconPathOut,
            bool isFilled = false,
            Brush customBrush = null)
        {
            var p = CapsuleTheme.Current;
            var strokeBrush = customBrush ?? CapsuleTheme.Brush(p.Text);

            var path = new Path
            {
                Data = geometry,
                Stroke = isFilled ? Brushes.Transparent : strokeBrush,
                Fill = isFilled ? strokeBrush : Brushes.Transparent,
                StrokeThickness = isFilled ? 0 : 1.6,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            iconPathOut = path;

            var vb = new Viewbox
            {
                Width = size > 32 ? 14 : 12,
                Height = size > 32 ? 14 : 12,
                Stretch = Stretch.Uniform,
                Child = path,
                IsHitTestVisible = false
            };

            var btn = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = toolTip,
                Child = vb
            };

            btn.MouseEnter += (_, _) => btn.Background = CapsuleTheme.Brush(p.Surface2);
            btn.MouseLeave += (_, _) => btn.Background = CapsuleTheme.Brush(p.Surface);
            btn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick?.Invoke();
            };

            return btn;
        }
    }
}
