using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TR.Stride.Ocean;
using TR.Stride.Terrain;
using Heightmap = Stride.Physics.Heightmap;

namespace TR.Stride
{
    /// <summary>
    /// Custom ocean material that creates foam near the terrain
    /// </summary>
    [DataContract]
    public class TerrainOceanMaterial : OceanMaterialBase
    {
        [DataMember] public Heightmap Heightmap { get; set; }
        private Texture _heightMapTexture = null;

        [DataMember] public Texture FoamTexture { get; set; }

        protected override MaterialDescriptor CreateDescriptor(int lod)
        {
            var desc = base.CreateDescriptor(lod);
            desc.Attributes.Emissive = new MaterialEmissiveMapFeature
            {
                EmissiveMap = new ComputeShaderClassColor
                {
                    MixinReference = "TerrainOceanEmissive"
                },
                Intensity = new ComputeFloat(1.0f),
                UseAlpha = false
            };

            return desc;
        }

        public override void UpdateMaterials(GraphicsDevice graphicsDevice, OceanComponent component, Material[] materials, WavesCascade[] cascades)
        {
            base.UpdateMaterials(graphicsDevice, component, materials, cascades);

            if (_heightMapTexture == null && Heightmap?.Shorts?.Length > 0)
            {
                var data = new float[Heightmap.Size.X * Heightmap.Size.Y];
                for (var y = 0; y < Heightmap.Size.Y; y++)
                {
                    for (var x = 0; x < Heightmap.Size.X; x++)
                    {
                        var index = y * Heightmap.Size.X + x;
                        data[index] = Heightmap.GetHeightAt(x, y);
                    }
                }

                _heightMapTexture = Texture.New2D(graphicsDevice, Heightmap.Size.X, Heightmap.Size.Y, PixelFormat.R32_Float, data);
                return; // Delay usage one frame
            }

            foreach (var material in materials)
            {
                if (_heightMapTexture != null)
                {
                    material.Passes[0].Parameters.Set(TerrainOceanEmissiveKeys.TerrainHeightMap, _heightMapTexture);
                }

                if (FoamTexture!= null)
                {
                    material.Passes[0].Parameters.Set(TerrainOceanEmissiveKeys.FoamTexture, FoamTexture);
                }
            }
        }
    }
}
