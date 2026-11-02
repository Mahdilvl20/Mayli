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

        public static void GetRestingShapeProfile(string style, string shape, int scalePercent, out double width, out double height, out double cornerRadius, out double earWidth)
        {
            double baseW = 228.0;
            double baseH = 35.0;
            double baseR = 14.0;
            double baseEar = 12.0;

            switch ((shape ?? "Compact").ToLowerInvariant())
            {
                case "ultra-compact":
                    baseW = 194.0;
                    baseH = 31.0;
                    baseR = 12.0;
                    baseEar = 9.0;
                    break;
                case "compact":
                    baseW = 224.0;
                    baseH = 34.0;
                    baseR = 14.0;
                    baseEar = 11.0;
                    break;
                case "classic":
                    baseW = 242.0;
                    baseH = 36.0;
                    baseR = 16.0;
                    baseEar = 13.0;
                    break;
                case "wide":
                    baseW = 274.0;
                    baseH = 36.0;
                    baseR = 18.0;
                    baseEar = 14.0;
                    break;
            }

            if (string.Equals(style, "Line", StringComparison.OrdinalIgnoreCase))
            {
                baseH = Math.Max(26.0, baseH - 4.0);
                baseR = 8.0;
                baseEar = 0.0;
            }
            else if (string.Equals(style, "Island", StringComparison.OrdinalIgnoreCase))
            {
                baseEar = 0.0;
            }

            double scale = Math.Clamp(scalePercent, 60, 140) / 100.0;
            width = Math.Round(baseW * scale);
            height = Math.Round(baseH * scale);
            cornerRadius = Math.Round(baseR * scale, 1);
            earWidth = Math.Round(baseEar * scale, 1);
        }

        public static Geometry BuildLineGeometry(double width, double height, double cornerRadius, bool isOutline)
        {
            double w = Math.Max(24.0, width);
            double h = Math.Max(8.0, height);
            double r = Math.Clamp(cornerRadius * 0.55, 5.0, Math.Max(5.0, h * 0.45));

            var streamGeo = new StreamGeometry();
            using (StreamGeometryContext ctx = streamGeo.Open())
            {
                ctx.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);
                ctx.LineTo(new Point(0, h - r), isStroked: true, isSmoothJoin: true);
                ctx.ArcTo(
                    new Point(r, h),
                    new Size(r, r),
                    0,
                    isLargeArc: false,
                    SweepDirection.Counterclockwise,
                    isStroked: true,
                    isSmoothJoin: true);
                ctx.LineTo(new Point(w - r, h), isStroked: true, isSmoothJoin: true);
                ctx.ArcTo(
                    new Point(w, h - r),
                    new Size(r, r),
                    0,
                    isLargeArc: false,
                    SweepDirection.Counterclockwise,
                    isStroked: true,
                    isSmoothJoin: true);
                ctx.LineTo(new Point(w, 0), isStroked: true, isSmoothJoin: true);
                ctx.LineTo(new Point(0, 0), isStroked: false, isSmoothJoin: false);
            }
            streamGeo.Freeze();
            return streamGeo;
        }

        /// <summary>
        /// Builds the signature Vayli top-docked MacBook-style notch with smooth top-left and top-right concave ears (EarUnitW = 12, EarUnitH = 10).
        /// Uses low-allocation StreamGeometry for zero-lag 60/120fps morphing.
        /// </summary>
        public static Geometry BuildShoulderedGeometry(double width, double height, double bottomRadius, bool isOutline)
        {
            double earW = 12.0;
            double earH = Math.Min(10.0, height * 0.35);
            double r = Math.Max(6.0, Math.Min(bottomRadius, (height - earH) * 0.85));
            double bodyLeft = earW;
            double bodyRight = Math.Max(bodyLeft + 2 * r + 10, width - earW);

            var streamGeo = new StreamGeometry();
            using (StreamGeometryContext ctx = streamGeo.Open())
            {
                ctx.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);

                // Top-left concave shoulder into vertical wall
                ctx.BezierTo(
                    new Point(earW * 0.65, 0),
                    new Point(bodyLeft, earH * 0.35),
                    new Point(bodyLeft, earH),
                    isStroked: true,
                    isSmoothJoin: true);

                // Left vertical wall down to bottom-left corner
                ctx.LineTo(new Point(bodyLeft, height - r), isStroked: true, isSmoothJoin: true);

                // Bottom-left rounded corner
                ctx.ArcTo(
                    new Point(bodyLeft + r, height),
                    new Size(r, r),
                    0,
                    isLargeArc: false,
                    SweepDirection.Counterclockwise,
                    isStroked: true,
                    isSmoothJoin: true);

                // Bottom flat edge
                ctx.LineTo(new Point(bodyRight - r, height), isStroked: true, isSmoothJoin: true);

                // Bottom-right rounded corner
                ctx.ArcTo(
                    new Point(bodyRight, height - r),
                    new Size(r, r),
                    0,
                    isLargeArc: false,
                    SweepDirection.Counterclockwise,
                    isStroked: true,
                    isSmoothJoin: true);

                // Right vertical wall up to top-right concave shoulder
                ctx.LineTo(new Point(bodyRight, earH), isStroked: true, isSmoothJoin: true);

                // Top-right concave shoulder out to top edge
                ctx.BezierTo(
                    new Point(bodyRight, earH * 0.35),
                    new Point(width - earW * 0.65, 0),
                    new Point(width, 0),
                    isStroked: true,
                    isSmoothJoin: true);

                // Top flush edge along screen top - strictly unstroked so it stays pure black and blends seamlessly into the top screen bezel
                ctx.LineTo(new Point(0, 0), isStroked: false, isSmoothJoin: false);
            }

            streamGeo.Freeze();
            return streamGeo;
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
            double easedProgress)
        {
            double t = Math.Clamp(easedProgress, 0.0, 1.0);
            double w = collapsedW + (expandedW - collapsedW) * t;
            double h = collapsedH + (expandedH - collapsedH) * t;
            double r = 15.0 + (24.0 - 15.0) * t;
            return BuildSilhouetteCore(style, w, h, r, isOutline: false);
        }
    }
}
