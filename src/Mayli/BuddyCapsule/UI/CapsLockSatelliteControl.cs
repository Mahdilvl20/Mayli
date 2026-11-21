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
    /// Outermost right-side satellite capsule indicating active Caps Lock state.
    /// Emerges as an expanded "Caps Lock ON" pill (134x40) and smoothly shrinks
    /// into a compact circular 'A' indicator (40x40) that remains visible while Caps Lock is ON.
    /// </summary>
    public sealed class CapsLockSatelliteControl : Border
    {
        public const double CompactCircleSize = 32.0;
        public const double ExpandedPillWidth = 128.0;

        private readonly StackPanel _expandedRow;
        private readonly Border _dot;
        private readonly TextBlock _labelText;
        private readonly Grid _circleGlyph;
        private readonly Ellipse _circleRing;
        private readonly Path _circleLetterPath;
        private readonly TranslateTransform _translate = new(0, 0);
        private readonly ScaleTransform _scale = new(0.8, 0.8);

        public event Action ExpandRequested;

        public string DisplayText => _labelText.Text ?? "";
        public string CircleGlyphText => "A";
        public TranslateTransform CapsuleTranslate => _translate;

        public CapsLockSatelliteControl()
        {
            var p = CapsuleTheme.Current;

            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(_scale);
            transformGroup.Children.Add(_translate);

            var rootGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                ClipToBounds = true
            };

            _expandedRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 14, 0),
                IsHitTestVisible = false
            };

            _dot = new Border
            {
                Width = 7,
                Height = 7,
                CornerRadius = new CornerRadius(3.5),
                Background = new SolidColorBrush(Color.FromRgb(255, 159, 10)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 7, 0)
            };

            _labelText = new TextBlock
            {
                Text = "Caps Lock ON",
                FontSize = 12.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };

            _expandedRow.Children.Add(_dot);
            _expandedRow.Children.Add(_labelText);

            _circleGlyph = new Grid
            {
                Width = CompactCircleSize,
                Height = CompactCircleSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.0,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0.85, 0.85)
            };

            _circleRing = new Ellipse
            {
                Width = 24,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Stroke = new SolidColorBrush(Color.FromArgb(165, 255, 159, 10)),
                Fill = new SolidColorBrush(Color.FromArgb(30, 255, 159, 10)),
                StrokeThickness = 1.6
            };

            // Mathematically centered vector 'A' (12x12 canvas centered at exact (6.0, 6.0)) - immune to font descender padding
            _circleLetterPath = new Path
            {
                Width = 12,
                Height = 12,
                Data = Geometry.Parse("M 2.4,10.2 L 6.0,1.8 L 9.6,10.2 M 3.6,7.4 L 8.4,7.4"),
                Stroke = CapsuleTheme.Brush(p.Text),
                StrokeThickness = 1.65,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _circleGlyph.Children.Add(_circleRing);
            _circleGlyph.Children.Add(_circleLetterPath);

            rootGrid.Children.Add(_expandedRow);
            rootGrid.Children.Add(_circleGlyph);

            Width = ExpandedPillWidth;
            Height = CompactCircleSize;
            CornerRadius = new CornerRadius(CompactCircleSize / 2.0);
            Background = CapsuleTheme.Brush(p.Bg);
            BorderBrush = Brushes.Transparent;
            BorderThickness = new Thickness(0);
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Top;
            Margin = new Thickness(0, 3.5, 0, 0);
            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = transformGroup;
            Cursor = Cursors.Hand;
            ClipToBounds = true;
            Visibility = Visibility.Collapsed;
            ToolTip = "Caps Lock ON";
            Child = rootGrid;

            MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                ExpandRequested?.Invoke();
            };
        }

        public void ApplyTheme(ThemePalette palette, Brush outlineBrush, Thickness outlineThickness)
        {
            Background = CapsuleTheme.Brush(palette.Bg);
            BorderBrush = outlineBrush;
            BorderThickness = outlineThickness;
            _labelText.Foreground = CapsuleTheme.Brush(palette.Text);
            _circleLetterPath.Stroke = CapsuleTheme.Brush(palette.Text);
        }

        public void UpdatePlacementAndMorph(
            double pillProgress,
            double capsFactor,
            double baseH,
            double rightEdgeCursor,
            double scale,
            double islandGap,
            bool isTucked)
        {
            // Constant smooth capsule size (32x32 circle / 128x32 pill) — never jumps size when main notch switches modes
            double pillEase = 1.0 - Math.Pow(1.0 - Math.Clamp(pillProgress, 0.0, 1.0), 3);
            double curBaseW = CompactCircleSize + (ExpandedPillWidth - CompactCircleSize) * pillEase;
            double satTop = baseH > 38.0
                ? (islandGap + (baseH - CompactCircleSize) / 2.0)
                : (islandGap + Math.Max(0.0, Math.Round((baseH - CompactCircleSize) / 2.0)));

            Width = curBaseW;
            Height = CompactCircleSize;
            CornerRadius = new CornerRadius(CompactCircleSize / 2.0);
            Margin = new Thickness(0, satTop, 0, 0);

            double textFade = Math.Clamp((pillEase - 0.35) / 0.65, 0.0, 1.0);
            _expandedRow.Opacity = textFade;
            _expandedRow.Visibility = textFade <= 0.01 ? Visibility.Collapsed : Visibility.Visible;

            double glyphFade = Math.Clamp(1.0 - pillEase * 1.45, 0.0, 1.0);
            _circleGlyph.Opacity = glyphFade;
            _circleGlyph.Visibility = glyphFade <= 0.01 ? Visibility.Collapsed : Visibility.Visible;
            if (_circleGlyph.RenderTransform is ScaleTransform glyphScale)
            {
                double gs = 0.82 + 0.18 * glyphFade;
                glyphScale.ScaleX = gs;
                glyphScale.ScaleY = gs;
            }

            double capsEase = 1.0 - Math.Pow(1.0 - capsFactor, 3);
            double gap = 8.0 * scale;
            double startRightX = +(rightEdgeCursor - curBaseW / 2.0 - 8.0);
            double fullRightX = +(rightEdgeCursor + gap + curBaseW / 2.0);
            _translate.X = startRightX + (fullRightX - startRightX) * capsEase;

            double capsScale = 0.78 + 0.22 * capsEase;
            _scale.ScaleX = capsScale;
            _scale.ScaleY = capsScale;

            if (!isTucked)
            {
                Opacity = Math.Clamp(capsEase * 1.15, 0.0, 1.0);
                IsHitTestVisible = capsFactor > 0.6;
            }
        }
    }
}
