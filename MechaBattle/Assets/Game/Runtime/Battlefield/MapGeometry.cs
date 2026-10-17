using System;
namespace MechaBattle.Battlefield
{
    // Shared solid geometry for simulation movement, sight, projectiles and camera clipping.
    public static class MapGeometry
    {
        public static bool CircleOverlaps(ObstacleDefinition o, float x, float z, float radius)
        {
            double dx = Math.Max(Math.Abs(x - o.x) - o.width * 0.5, 0), dz = Math.Max(Math.Abs(z - o.z) - o.depth * 0.5, 0);
            return dx * dx + dz * dz < radius * (double)radius - 0.00000001;
        }
        public static double RayBox(ObstacleDefinition o, double x, double y, double z, double dx, double dy, double dz, float expansion = 0)
        {
            double enter = 0, exit = double.PositiveInfinity;
            if (!Slab(x, dx, o.x - o.width * 0.5 - expansion, o.x + o.width * 0.5 + expansion, ref enter, ref exit)
                || !Slab(y, dy, o.y - o.height * 0.5 - expansion, o.y + o.height * 0.5 + expansion, ref enter, ref exit)
                || !Slab(z, dz, o.z - o.depth * 0.5 - expansion, o.z + o.depth * 0.5 + expansion, ref enter, ref exit)) return double.PositiveInfinity;
            return exit >= 0 ? enter : double.PositiveInfinity;
        }
        private static bool Slab(double p, double d, double min, double max, ref double enter, ref double exit)
        {
            if (Math.Abs(d) < 0.000000001) return p >= min && p <= max;
            double a = (min - p) / d, b = (max - p) / d;
            enter = Math.Max(enter, Math.Min(a, b)); exit = Math.Min(exit, Math.Max(a, b)); return enter <= exit;
        }
    }
}
