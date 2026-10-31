using System;
using System.Windows;
using System.Windows.Media;

namespace WindowsBuddy.BuddyCapsule.Renderer
{
    public static class NotchGeometryBuilder
    {
        public const double IslandGap = 5.0;
        public const double ContentPadding = 12.0;

        public static Geometry BuildGeometry(string style, double width, double height, double cornerRadius)
        {
            return BuildSilhouetteCore(style, width, height, cornerRadius, isOutline: false);
        }

        public static Geometry BuildSilhouetteGeometry(string style, double width, double height, double cornerRadius)
        {
            return BuildSilhouetteCore(style, width, height, cornerRadius, isOutline: true);
        }

        public static Geometry BuildSilhouetteCore(string style, double width, double height, double cornerRadius, bool isOutline)
        {
            switch ((style ?? "Shouldered").ToLowerInvariant())
            {
                case "island":
                    return BuildIslandGeometry(width, height);
                case "compact":
                    return BuildCompactGeometry(width, height, cornerRadius, isOutline);
                case "wide":
                    return BuildWideGeometry(width, height, cornerRadius, isOutline);
                case "line":
                    return BuildLineGeometry(width, height, cornerRadius, isOutline);
                default:
                    return BuildShoulderedGeometry(width, height, cornerRadius, isOutline);
            }
        }

        public static Geometry BuildIslandGeometry(double width, double height)
        {
            double r = Math.Min(height / 2.0, 22.0);
            var rect = new Rect(0, IslandGap, Math.Max(40, width), Math.Max(16, height));
            var geo = new RectangleGeometry(rect, r, r);
            geo.Freeze();
            return geo;
        }

        public static Geometry BuildCompactGeometry(double width, double height, double cornerRadius, bool isOutline)
        {
            return BuildShoulderedGeometry(width, height, Math.Min(cornerRadius, 14.0), isOutline);
        }

        public static Geometry BuildWideGeometry(double width, double height, double cornerRadius, bool isOutline)
        {
            return BuildShoulderedGeometry(width, height, Math.Max(cornerRadius, 18.0), isOutline);
        }

        public static Geometry BuildLineGeometry(double width, double height, double cornerRadius, bool isOutline)
        {
            double h = Math.Min(height, 6.0);
            var geo = new RectangleGeometry(new Rect(0, 0, width, h), 3, 3);
            geo.Freeze();
            return geo;
        }

        /// <summary>
        /// Builds the signature Vayli top-docked MacBook-style notch with smooth top-left and top-right concave ears (EarUnitW = 12, EarUnitH = 10).
        /// </summary>
        public static Geometry BuildShoulderedGeometry(double width, double height, double bottomRadius, bool isOutline)
        {
            double earW = 12.0;
            double earH = Math.Min(10.0, height * 0.35);
            double r = Math.Max(6.0, Math.Min(bottomRadius, (height - earH) * 0.85));
            double bodyLeft = earW;
            double bodyRight = Math.Max(bodyLeft + 2 * r + 10, width - earW);

            var fig = new PathFigure
            {
                StartPoint = new Point(0, 0),
                IsClosed = true,
                IsFilled = true
            };

            // Top-left concave shoulder into vertical wall
            fig.Segments.Add(new BezierSegment(
                new Point(earW * 0.65, 0),
                new Point(bodyLeft, earH * 0.35),
                new Point(bodyLeft, earH),
                isStroked: true));

            // Left vertical wall down to bottom-left corner
            fig.Segments.Add(new LineSegment(new Point(bodyLeft, height - r), isStroked: true));

            // Bottom-left rounded corner
            fig.Segments.Add(new ArcSegment(
                new Point(bodyLeft + r, height),
                new Size(r, r),
                0,
                isLargeArc: false,
                SweepDirection.Counterclockwise,
                isStroked: true));

            // Bottom flat edge
            fig.Segments.Add(new LineSegment(new Point(bodyRight - r, height), isStroked: true));

            // Bottom-right rounded corner
            fig.Segments.Add(new ArcSegment(
                new Point(bodyRight, height - r),
                new Size(r, r),
                0,
                isLargeArc: false,
                SweepDirection.Counterclockwise,
                isStroked: true));

            // Right vertical wall up to top-right concave shoulder
            fig.Segments.Add(new LineSegment(new Point(bodyRight, earH), isStroked: true));

            // Top-right concave shoulder out to top edge
            fig.Segments.Add(new BezierSegment(
                new Point(bodyRight, earH * 0.35),
                new Point(width - earW * 0.65, 0),
                new Point(width, 0),
                isStroked: true));

            // Top flush edge along screen top - strictly unstroked so it stays pure black and blends seamlessly into the top screen bezel
            fig.Segments.Add(new LineSegment(new Point(0, 0), isStroked: false));

            var pathGeo = new PathGeometry(new[] { fig });
            pathGeo.Freeze();
            return pathGeo;
        }

        public static Geometry BuildExpandedGeometry(double width, double height, double cornerRadius)
        {
            return BuildShoulderedGeometry(width, height, Math.Max(cornerRadius, 22.0), isOutline: false);
        }

        public static Geometry BuildMorphedGeometry(
            string style,
            double collapsedW,
            double collapsedH,
            double expandedW,
            double expandedH,
            double progress)
        {
            double t = Math.Clamp(progress, 0.0, 1.0);
            // Smooth cubic ease out
            double ease = 1.0 - Math.Pow(1.0 - t, 3);
            double w = collapsedW + (expandedW - collapsedW) * ease;
            double h = collapsedH + (expandedH - collapsedH) * ease;
            double r = 15.0 + (24.0 - 15.0) * ease;
            return BuildSilhouetteCore(style, w, h, r, isOutline: false);
        }
    }
}
