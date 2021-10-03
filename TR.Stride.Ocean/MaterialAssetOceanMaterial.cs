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
    /// <summary>
    /// Creates an ocean material using predefined material assets
    /// </summary>
    [DataContract]
    public class MaterialAssetOceanMaterial : BaseOceanMaterial
    {
        /// <summary>
        /// Need three materials
        /// </summary>
        [DataMember(50), Display(name: "Materials", category: "Assets")] public List<Material> Materials { get; set; } = new List<Material>();

        public override Material CreateMaterial(GraphicsDevice graphicsDevice, int lod)
        {
            if (Materials == null || Materials.Count < 3 || Materials[lod] == null || Materials[lod].Passes.Count == 0)
                return null;

            var material = Materials[lod];
            material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Lod, lod);

            return material;
        }
    }
}
