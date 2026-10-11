using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace CodexTokenMonitor
{
    // Real 3D geometry: perspective projection, rotated surface normals,
    // diffuse/specular lighting and depth-sorted opaque mesh faces.
    internal static class ContextSculpture
    {
        private struct Vector
        {
            internal double X, Y, Z;
            internal Vector(double x, double y, double z) { X = x; Y = y; Z = z; }
        }
        private sealed class Face
        {
            internal PointF[] Points;
            internal double Depth;
            internal Color Color;
        }

        private static Vector Rotate(Vector v, double yaw, double pitch)
        {
            double y = v.Y * Math.Cos(pitch) - v.Z * Math.Sin(pitch);
            double z = v.Y * Math.Sin(pitch) + v.Z * Math.Cos(pitch);
            return new Vector(v.X * Math.Cos(yaw) + z * Math.Sin(yaw), y,
                -v.X * Math.Sin(yaw) + z * Math.Cos(yaw));
        }

        private static PointF Project(Vector v, Rectangle viewport, double scale)
        {
            double perspective = 4.5 / (4.5 - v.Z);
            return new PointF((float)(viewport.X + viewport.Width / 2d + v.X * scale * perspective),
                (float)(viewport.Y + viewport.Height * 0.46 + v.Y * scale * perspective));
        }

        private static int Channel(double value) { return (int)Math.Max(0, Math.Min(255, value)); }

        private static void AddFace(List<Face> faces, Vector[] vertices, Vector normal,
            Color material, Rectangle viewport, double scale, double yaw, double pitch)
        {
            Vector litNormal = Rotate(normal, yaw, pitch);
            double diffuse = Math.Max(0, -litNormal.X * 0.35 - litNormal.Y * 0.55 + litNormal.Z * 0.76);
            double reflection = Math.Pow(Math.Max(0, -litNormal.X * 0.18 - litNormal.Y * 0.28 + litNormal.Z * 0.94), 24) * 110;
            double brightness = 0.30 + diffuse * 0.85;
            Face face = new Face { Points = new PointF[vertices.Length], Color = Color.FromArgb(
                Channel(material.R * brightness + reflection), Channel(material.G * brightness + reflection), Channel(material.B * brightness + reflection)) };
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector transformed = Rotate(vertices[i], yaw, pitch);
                face.Depth += transformed.Z / vertices.Length;
                face.Points[i] = Project(transformed, viewport, scale);
            }
            faces.Add(face);
        }

        private static Vector Torus(double u, double v)
        {
            double radius = 1 + 0.24 * Math.Cos(v);
            return new Vector(radius * Math.Cos(u), radius * Math.Sin(u), 0.24 * Math.Sin(v));
        }

        private static Vector OrbitPoint(double angle, int satellite)
        {
            double radius = 1.37 + (satellite % 2) * .05;
            double inclination = -.65 + satellite * .30;
            return new Vector(radius * Math.Cos(angle), radius * Math.Sin(angle) * Math.Cos(inclination),
                radius * Math.Sin(angle) * Math.Sin(inclination));
        }

        private static void DrawOrbit(Graphics g, Rectangle viewport, double scale,
            double yaw, double pitch, int satellite, Color color)
        {
            if (ViewSettings.Current.Intensity == 0) return;
            PointF[] path = new PointF[81];
            for (int i = 0; i < path.Length; i++)
            {
                double t = i * Math.PI * 2 / (path.Length - 1);
                Vector point = OrbitPoint(t, satellite);
                path[i] = Project(Rotate(point, yaw, pitch), viewport, scale);
            }
            int intensity = ViewSettings.Current.Intensity;
            using (Pen glow = new Pen(Color.FromArgb(12 * intensity / 100, color), 2.5f)) g.DrawLines(glow, path);
            using (Pen rim = new Pen(Color.FromArgb(75 * intensity / 100, color), .7f)) g.DrawLines(rim, path);
        }

        private static Vector SpherePoint(double longitude, double latitude)
        {
            return new Vector(Math.Cos(latitude) * Math.Cos(longitude), Math.Sin(latitude),
                Math.Cos(latitude) * Math.Sin(longitude));
        }

        private static void AddSatellite(List<Face> faces, Rectangle viewport, double scale,
            double yaw, double pitch, int satellite, Color color)
        {
            // Orbit and axial spin keep moving even when the central task sculpture rests.
            double phase = SpectrumFx.Phase;
            double angle = .4 + satellite * Math.PI * 2 / 5 + phase * (.32 + satellite * .025);
            Vector center = OrbitPoint(angle, satellite);
            double spin = phase * (1.15 + satellite * .14) + satellite * .6;
            double tilt = .25 + satellite * .22;
            double radius = .125 + (satellite % 2) * .008;
            int meridians = SpectrumFx.EconomyActive ? 12 : 20;
            int parallels = SpectrumFx.EconomyActive ? 8 : 12;
            for (int row = 0; row < parallels; row++)
            for (int column = 0; column < meridians; column++)
            {
                double u0 = column * Math.PI * 2 / meridians;
                double u1 = (column + 1) * Math.PI * 2 / meridians;
                double v0 = -Math.PI / 2 + row * Math.PI / parallels;
                double v1 = -Math.PI / 2 + (row + 1) * Math.PI / parallels;
                Vector[] vertices = { SpherePoint(u0, v0), SpherePoint(u1, v0), SpherePoint(u1, v1), SpherePoint(u0, v1) };
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector local = Rotate(vertices[i], spin, tilt);
                    vertices[i] = new Vector(center.X + radius * local.X, center.Y + radius * local.Y, center.Z + radius * local.Z);
                }
                Vector normal = Rotate(SpherePoint((u0 + u1) / 2, (v0 + v1) / 2), spin, tilt);
                // Contrasting meridian bands make axial rotation visible on a round surface.
                bool stripe = column == 0 || column == meridians / 2;
                Color material = stripe ? Color.FromArgb((color.R + 255) / 2, (color.G + 255) / 2, (color.B + 255) / 2) : color;
                AddFace(faces, vertices, normal, material, viewport, scale, yaw, pitch);
            }
        }

        internal static void DrawToken(Graphics g, Rectangle viewport, Color color, double phase)
        {
            if (viewport.Width < 4 || viewport.Height < 4) return;
            GraphicsState saved = g.Save();
            try
            {
                g.SetClip(viewport, CombineMode.Intersect);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                SpectrumFx.Halo(g, viewport, color, 52);
                Vector[] cube = {
                    new Vector(-.48,-.48,-.48), new Vector(.48,-.48,-.48),
                    new Vector(.48,.48,-.48), new Vector(-.48,.48,-.48),
                    new Vector(-.48,-.48,.48), new Vector(.48,-.48,.48),
                    new Vector(.48,.48,.48), new Vector(-.48,.48,.48)
                };
                int[][] sides = { new[] {0,1,2,3}, new[] {4,7,6,5}, new[] {0,4,5,1}, new[] {3,2,6,7}, new[] {0,3,7,4}, new[] {1,5,6,2} };
                Vector[] normals = { new Vector(0,0,-1), new Vector(0,0,1), new Vector(0,-1,0), new Vector(0,1,0), new Vector(-1,0,0), new Vector(1,0,0) };
                List<Face> faces = new List<Face>(6);
                for (int side = 0; side < sides.Length; side++)
                {
                    Vector[] vertices = new Vector[4];
                    for (int i = 0; i < 4; i++) vertices[i] = cube[sides[side][i]];
                    AddFace(faces, vertices, normals[side], color, viewport,
                        Math.Min(viewport.Width, viewport.Height) / 1.9,
                        .52 + Math.Sin(phase) * .22, -.35 + Math.Cos(phase) * .12);
                }
                faces.Sort((a, b) => a.Depth.CompareTo(b.Depth));
                foreach (Face face in faces)
                using (SolidBrush fill = new SolidBrush(face.Color))
                using (Pen edge = new Pen(face.Color, .5f))
                {
                    g.FillPolygon(fill, face.Points);
                    g.DrawPolygon(edge, face.Points);
                }
            }
            finally { g.Restore(saved); }
        }

        internal static void Draw(Graphics graphics, Rectangle viewport, double usage,
            bool available, Color accent, float pointerYaw, float pointerPitch)
        {
            if (viewport.Width < 20 || viewport.Height < 20) return;
            GraphicsState saved = graphics.Save();
            try
            {
                graphics.SetClip(viewport, CombineMode.Intersect);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                double scale = Math.Min(viewport.Width / 3.3, viewport.Height / 3.1);
                double yaw = 0.24 + pointerYaw + Math.Sin(SpectrumFx.ContextPhase * .55) * .13;
                double pitch = 0.64 + pointerPitch + Math.Cos(SpectrumFx.ContextPhase * .4) * .065;
                SpectrumFx.Halo(graphics, new RectangleF(viewport.X, viewport.Y, viewport.Width, viewport.Height), Theme.Cyan, 28);
                Color[] satellites = { SpectrumFx.Blue, Theme.Cyan, Color.FromArgb(78, 231, 139), SpectrumFx.Gold, Theme.Orange };
                for (int satellite = 0; satellite < satellites.Length; satellite++)
                    DrawOrbit(graphics, viewport, scale, yaw, pitch, satellite, satellites[satellite]);
                // Soft contact shadow locates the floating sculpture in space.
                for (int layer = 7; layer >= 1; layer--)
                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(8, 0, 0, 0)))
                    graphics.FillEllipse(shadow, (float)(viewport.X + viewport.Width / 2d - scale - layer),
                        (float)(viewport.Y + viewport.Height * 0.82 - layer / 2d),
                        (float)(scale * 2 + layer * 2), (float)(scale * 0.20 + layer));
                List<Face> faces = new List<Face>(800);
                int segments = SpectrumFx.EconomyActive ? 48 : 96;
                int tubeSides = SpectrumFx.EconomyActive ? 10 : 16;
                for (int segment = 0; segment < segments; segment++)
                {
                    double u0 = -Math.PI / 2 + segment * Math.PI * 2 / segments;
                    double u1 = -Math.PI / 2 + (segment + 1) * Math.PI * 2 / segments;
                    Color material = available && (segment + 0.5) / segments <= usage
                        ? accent : Color.FromArgb(49, 76, 87);
                    for (int side = 0; side < tubeSides; side++)
                    {
                        double v0 = side * Math.PI * 2 / tubeSides;
                        double v1 = (side + 1) * Math.PI * 2 / tubeSides;
                        double u = (u0 + u1) / 2, v = (v0 + v1) / 2;
                        AddFace(faces, new[] { Torus(u0, v0), Torus(u1, v0), Torus(u1, v1), Torus(u0, v1) },
                            new Vector(Math.Cos(v) * Math.Cos(u), Math.Cos(v) * Math.Sin(u), Math.Sin(v)),
                            material, viewport, scale * .9, yaw, pitch);
                    }
                }
                // A faceted token occupies the ring's centre; it shares the camera
                // and depth sorting rather than being a flat icon overlay.
                Vector[] cube = {
                    new Vector(-.28,-.28,-.15), new Vector(.28,-.28,-.15),
                    new Vector(.28,.28,-.15), new Vector(-.28,.28,-.15),
                    new Vector(-.28,-.28,.41), new Vector(.28,-.28,.41),
                    new Vector(.28,.28,.41), new Vector(-.28,.28,.41)
                };
                int[][] indices = { new[] {0,1,2,3}, new[] {4,7,6,5}, new[] {0,4,5,1}, new[] {3,2,6,7}, new[] {0,3,7,4}, new[] {1,5,6,2} };
                Vector[] normals = { new Vector(0,0,-1), new Vector(0,0,1), new Vector(0,-1,0), new Vector(0,1,0), new Vector(-1,0,0), new Vector(1,0,0) };
                for (int f = 0; f < indices.Length; f++)
                {
                    Vector[] vertices = new Vector[4];
                    for (int i = 0; i < 4; i++) vertices[i] = cube[indices[f][i]];
                    AddFace(faces, vertices, normals[f], Theme.Cyan, viewport, scale * .9, yaw, pitch);
                }
                for (int satellite = 0; satellite < satellites.Length; satellite++)
                    AddSatellite(faces, viewport, scale, yaw, pitch, satellite, satellites[satellite]);
                faces.Sort((a, b) => a.Depth.CompareTo(b.Depth));
                foreach (Face face in faces)
                using (SolidBrush fill = new SolidBrush(face.Color))
                using (Pen seam = new Pen(face.Color, 0.55f))
                {
                    graphics.FillPolygon(fill, face.Points);
                    graphics.DrawPolygon(seam, face.Points);
                }
            }
            finally { graphics.Restore(saved); }
        }
    }
}
