using Stride.Core;
using Stride.Engine;
using Stride.Engine.Design;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Terrain
{
    [DataContract(nameof(ProceduralHeightMapComponent))]
    [Display("Procedural Heightmap", Expand = ExpandRule.Once)]
    [DefaultEntityComponentProcessor(typeof(ProceduralHeightMapProcessor))]
    public class ProceduralHeightMapComponent : ScriptComponent
    {
        [DataMember(0)] public TerrainComponent Terrain { get; set; }

        private float _scale = 1f;
        [DataMember(1)] public float Scale { get => _scale; set { _scale = value; IsDirty = true; } }

        internal bool IsDirty { get; set; } = true;
    }
}
