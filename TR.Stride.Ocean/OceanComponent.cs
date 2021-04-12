using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Ocean
{
    [DataContract, DefaultEntityComponentRenderer(typeof(OceanEntityProcessor))]
    public class OceanComponent : ScriptComponent
    {
        [DataMember, DefaultValue(256)] public int Size { get; set; } = 256;
        [DataMember] public WavesSettings WavesSettings { get; set; } = new();

        [DataMember, DefaultValue(250)] public float LengthScale0 { get; set; } = 250;
        [DataMember, DefaultValue(17)] public float LengthScale1 { get; set; } = 17;
        [DataMember, DefaultValue(5)] public float LengthScale2 { get; set; } = 5;
        [DataMember] public bool AlwaysRecalculateInitials { get; set; } = false;

        [DataMember] public IOceanMaterial Material { get; set; } = new DefaultOceanMaterial();
        [DataMember] public IOceanMesh Mesh { get; set; } = new DefaultOceanMesh();
    }
}
