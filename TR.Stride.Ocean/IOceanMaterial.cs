using Stride.Core;
using Stride.Graphics;
using Stride.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Ocean
{
    public interface IOceanMaterial
    {
        Material CreateMaterial(GraphicsDevice graphicsDevice, int lod);
        void UpdateMaterials(GraphicsDevice graphicsDevice, OceanComponent component, Material[] materials, WavesCascade[] cascades);
    }
}
