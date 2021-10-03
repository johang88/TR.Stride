using Stride.Core.Mathematics;
using Stride.Physics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Terrain
{
    public static class HeightmapExtensions
    {
        public static Vector3 GetNormal(this Heightmap heightmap, int x, int y)
        {
            var heightL = GetHeightAt(heightmap, x - 1, y);
            var heightR = GetHeightAt(heightmap, x + 1, y);
            var heightD = GetHeightAt(heightmap, x, y - 1);
            var heightU = GetHeightAt(heightmap, x, y + 1);

            var normal = new Vector3(heightL - heightR, 2.0f, heightD - heightU);
            normal.Normalize();

            return normal;
        }

        public static Vector3 GetTangent(this Heightmap heightmap, int x, int z)
        {
            var flip = 1;
            var here = new Vector3(x, GetHeightAt(heightmap, x, z), z);
            var left = new Vector3(x - 1, GetHeightAt(heightmap, x - 1, z), z);
            if (left.X < 0.0f)
            {
                flip *= -1;
                left = new Vector3(x + 1, GetHeightAt(heightmap, x + 1, z), z);
            }

            left -= here;

            var tangent = left * flip;
            tangent.Normalize();

            return tangent;
        }

        public static bool IsValidCoordinate(this Heightmap heightmap, int x, int y)
            => x >= 0 && x < heightmap.Size.X && y >= 0 && y < heightmap.Size.Y;

        public static int GetHeightIndex(this Heightmap heightmap, int x, int y)
            => y * heightmap.Size.X + x;

        public static float GetHeightAt(this Heightmap heightmap, int x, int y)
        {
            if (!IsValidCoordinate(heightmap, x, y) || x == 0 || y == 0 || x == heightmap.Size.X - 1 || y == heightmap.Size.Y - 1)
            {
                return -heightmap.HeightRange.Y;
            }

            var index = GetHeightIndex(heightmap, x, y);
            var heightData = heightmap.Shorts[index];

            var height = HeightmapUtils.ConvertToFloatHeight(short.MinValue, short.MaxValue, heightData);
            height *= heightmap.HeightRange.Y;

            return height;
        }

        public static float GetHeightAt(this Heightmap heightmap, float x, float z)
        {
            if (x < 0.0f || x >= heightmap.Size.X || z < 0 || z >= heightmap.Size.Y)
                return -1;

            int xi = (int)x, zi = (int)z;
            float xpct = x - xi, zpct = z - zi;

            if (xi == heightmap.Size.X - 1)
            {
                --xi;
                xpct = 1.0f;
            }
            if (zi == heightmap.Size.Y - 1)
            {
                --zi;
                zpct = 1.0f;
            }

            var heights = new float[]
            {
                GetHeightAt(heightmap, xi, zi),
                GetHeightAt(heightmap, xi, zi + 1),
                GetHeightAt(heightmap, xi + 1, zi),
                GetHeightAt(heightmap, xi + 1, zi + 1)
            };

            var w = new float[]
            {
                (1.0f - xpct) * (1.0f - zpct),
                (1.0f - xpct) * zpct,
                xpct * (1.0f - zpct),
                xpct * zpct
            };

            var height = w[0] * heights[0] + w[1] * heights[1] + w[2] * heights[2] + w[3] * heights[3];

            return height;
        }

        public static bool Intersects(this Heightmap heightmap, Ray ray, out Vector3 point)
        {
            var bounds = new BoundingBox(Vector3.Zero, new Vector3(heightmap.Size.X, heightmap.HeightRange.Y, heightmap.Size.Y));

            point = ray.Position;

            // Check if we intersect at all
            if (bounds.Contains(ref point) == ContainmentType.Disjoint)
            {
                if (ray.Intersects(ref bounds, out float distance))
                {
                    point += ray.Direction * distance;
                }
                else
                {
                    return false;
                }
            }

            // Trace along the ray until we leave bounds or intersect the terrain
            while (true)
            {
                var height = GetHeightAt(heightmap, point.X, point.Z);
                if (point.Y <= height)
                {
                    point.Y = height;
                    return true;
                }

                point += ray.Direction;

                if (point.X < bounds.Minimum.X || point.X > bounds.Maximum.X || point.Z < bounds.Minimum.Z || point.Z > bounds.Maximum.Z)
                {
                    return false;
                }
            }
        }
    }
}
