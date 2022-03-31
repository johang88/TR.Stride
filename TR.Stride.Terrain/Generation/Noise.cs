using Stride.Physics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Terrain.Generation
{
    public static class Noise
    {
        public static void GenerateNoiseMap(Heightmap heightmap, float scale)
        {
            var width = heightmap.Size.X;
            var height = heightmap.Size.Y;

            var perlin = new Perlin();

            if (scale <= 0.0f)
            {
                scale = 0.0001f;
            }

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var sampleX = x / scale;
                    var sampleY = y / scale;
                    var index = y * width + x;

                    var sample = (float)perlin.Perlin3D(sampleX, sampleY, 0);
                    heightmap.Shorts[index] = (short)(short.MinValue + sample * short.MaxValue);
                }
            }
        }
    }
}
