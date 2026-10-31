using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WindowsBuddy.Tools.Weather
{
    public static class WeatherIcons
    {
        public static FrameworkElement CreateIcon(string type, double size = 24, Brush stroke = null)
        {
            stroke ??= Brushes.White;
            var canvas = new Canvas
            {
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            double scale = size / 24.0;
            var transform = new ScaleTransform(scale, scale);

            type = type?.ToLowerInvariant() ?? "cloud";

            switch (type)
            {
                case "sun":
                    // Sun circle
                    var sunCircle = new Ellipse
                    {
                        Width = 10,
                        Height = 10,
                        Stroke = stroke,
                        StrokeThickness = 1.6,
                        RenderTransform = transform
                    };
                    Canvas.SetLeft(sunCircle, 7 * scale);
                    Canvas.SetTop(sunCircle, 7 * scale);
                    canvas.Children.Add(sunCircle);

                    // Rays
                    AddRay(canvas, 12, 1, 12, 4, stroke, scale);
                    AddRay(canvas, 12, 20, 12, 23, stroke, scale);
                    AddRay(canvas, 1, 12, 4, 12, stroke, scale);
                    AddRay(canvas, 20, 12, 23, 12, stroke, scale);
                    AddRay(canvas, 4.5, 4.5, 6.8, 6.8, stroke, scale);
                    AddRay(canvas, 17.2, 17.2, 19.5, 19.5, stroke, scale);
                    AddRay(canvas, 4.5, 19.5, 6.8, 17.2, stroke, scale);
                    AddRay(canvas, 17.2, 6.8, 19.5, 4.5, stroke, scale);
                    break;

                case "moon":
                    var moonPath = new Path
                    {
                        Data = Geometry.Parse("M 15 3 A 9 9 0 1 1 5 17 A 7 7 0 0 0 15 3 Z"),
                        Stroke = stroke,
                        StrokeThickness = 1.6,
                        Fill = Brushes.Transparent,
                        RenderTransform = transform
                    };
                    Canvas.SetLeft(moonPath, 2 * scale);
                    Canvas.SetTop(moonPath, 2 * scale);
                    canvas.Children.Add(moonPath);
                    break;

                case "moon_cloud":
                    // Small crescent moon behind
                    var crescent = new Path
                    {
                        Data = Geometry.Parse("M 14 3 A 5 5 0 0 1 18 9 A 4 4 0 0 0 14 3 Z"),
                        Stroke = stroke,
                        StrokeThickness = 1.4,
                        Fill = Brushes.Transparent,
                        RenderTransform = transform
                    };
                    Canvas.SetLeft(crescent, 2 * scale);
                    Canvas.SetTop(crescent, 1 * scale);
                    canvas.Children.Add(crescent);

                    // Cloud in front
                    AddCloud(canvas, stroke, scale, 0, 3);
                    break;

                case "sun_cloud":
                    // Sun rays peeking top-right
                    AddRay(canvas, 15, 2, 16, 4, stroke, scale);
                    AddRay(canvas, 19, 5, 17.5, 6.5, stroke, scale);
                    AddRay(canvas, 21, 9, 19, 9.5, stroke, scale);

                    var sunArc = new Path
                    {
                        Data = Geometry.Parse("M 12 7 A 4.5 4.5 0 0 1 18 12"),
                        Stroke = stroke,
                        StrokeThickness = 1.5,
                        RenderTransform = transform
                    };
                    canvas.Children.Add(sunArc);

                    AddCloud(canvas, stroke, scale, 0, 3);
                    break;

                case "rain":
                    AddCloud(canvas, stroke, scale, 0, 1);
                    // Rain drops
                    AddRainDrop(canvas, 7, 18, 5, 22, stroke, scale);
                    AddRainDrop(canvas, 12, 18, 10, 22, stroke, scale);
                    AddRainDrop(canvas, 17, 18, 15, 22, stroke, scale);
                    break;

                case "cloud":
                default:
                    AddCloud(canvas, stroke, scale, 0, 2);
                    break;
            }

            return canvas;
        }

        private static void AddCloud(Canvas canvas, Brush stroke, double scale, double ox, double oy)
        {
            var cloud = new Path
            {
                Data = Geometry.Parse("M 19.35 10.04 C 18.67 6.59 15.64 4 12 4 C 9.11 4 6.6 5.64 5.35 8.04 C 2.34 8.36 0 10.91 0 14 C 0 17.31 2.69 20 6 20 L 19 20 C 21.76 20 24 17.76 24 15 C 24 12.36 21.95 10.22 19.35 10.04 Z"),
                Stroke = stroke,
                StrokeThickness = 1.6,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeStartLineCap = PenLineCap.Round,
                Fill = Brushes.Transparent,
                RenderTransform = new ScaleTransform(scale * 0.9, scale * 0.9)
            };
            Canvas.SetLeft(cloud, (ox + 1.2) * scale);
            Canvas.SetTop(cloud, (oy - 1) * scale);
            canvas.Children.Add(cloud);
        }

        private static void AddRay(Canvas canvas, double x1, double y1, double x2, double y2, Brush stroke, double scale)
        {
            var line = new Line
            {
                X1 = x1 * scale,
                Y1 = y1 * scale,
                X2 = x2 * scale,
                Y2 = y2 * scale,
                Stroke = stroke,
                StrokeThickness = 1.4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            canvas.Children.Add(line);
        }

        private static void AddRainDrop(Canvas canvas, double x1, double y1, double x2, double y2, Brush stroke, double scale)
        {
            var line = new Line
            {
                X1 = x1 * scale,
                Y1 = y1 * scale,
                X2 = x2 * scale,
                Y2 = y2 * scale,
                Stroke = stroke,
                StrokeThickness = 1.4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            canvas.Children.Add(line);
        }
    }
}
