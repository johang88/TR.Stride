using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Ocean
{
    public interface IOceanMesh
    {
        void SetOcean(OceanComponent component, Material[] materials);
        void Update(GraphicsDevice graphicsDevice, CameraComponent camera);
    }
}
