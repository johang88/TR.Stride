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
        [DataMember, DefaultValue(8)] public int ClipLevels { get; set; } = 8;
        [DataMember, DefaultValue(30)] public int VertexDensity { get; set; } = 30;
        [DataMember, DefaultValue(15)] public float LengthScale { get; set; } = 15;
        [DataMember, DefaultValue(55.4f)] public float SkirtSize { get; set; } = 55.4f;
        [DataMember, DefaultValue(256)] public int Size { get; set; } = 256;
        [DataMember] public WavesSettings WavesSettings { get; set; } = new();

        [DataMember, DefaultValue(250)] public float LengthScale0 { get; set; } = 250;
        [DataMember, DefaultValue(17)] public float LengthScale1 { get; set; } = 17;
        [DataMember, DefaultValue(5)] public float LengthScale2 { get; set; } = 5;
        [DataMember] public bool AlwaysRecalculateInitials { get; set; } = false;

        [DataMemberIgnore] public int GridSize => 4 * VertexDensity + 1;

        [DataMember] public OceanMaterialSettings Material { get; set; } = new();

        [DataMember] public LightComponent Sun { get; set; }

        public int GetActiveLodLevels(Vector3 cameraPosition)
            => ClipLevels - MathUtil.Clamp((int)MathF.Log((1.7f * MathF.Abs(cameraPosition.Y) + 1) / LengthScale, 2), 0, ClipLevels);

        public float GetClipLevelScale(int level, int activeLevels)
            => LengthScale / GridSize * MathF.Pow(2, ClipLevels - activeLevels + level + 1);

        public Vector3 OffsetFromCenter(int level, int activeLevels)
            => (MathF.Pow(2, ClipLevels) + GeometricProgressionSum(2, 2, ClipLevels - activeLevels + level + 1, ClipLevels - 1))
                   * LengthScale / GridSize * (GridSize - 1) / 2 * new Vector3(-1, 0, -1);

        private float GeometricProgressionSum(float b0, float q, int n1, int n2)
            => b0 / (1 - q) * (MathF.Pow(q, n2) - MathF.Pow(q, n1));
    }

    [DataContract]
    public class OceanMaterialSettings
    {
        private const string CategorySSS = "SSS";
        private const string CategoryFoam = "Foam";
        private const string CategorySurface = "Surface";

        [DataMember(0), DataMemberRange(0, 100, 0.01f, 0.1f, 4), DefaultValue(7.13f)] public float LodScale { get; set; } = 7.13f;

        [DataMember(10), Display(name: "Base", category: CategorySSS), DataMemberRange(-1, 1, 0.01f, 0.1f, 4), DefaultValue(-0.1f)] public float SSSBase { get; set; } = -0.1f;
        [DataMember(11), Display(name: "Scale", category: CategorySSS), DataMemberRange(0, 10, 0.01f, 0.1f, 4), DefaultValue(4.8f)] public float SSSScale { get; set; } = 4.8f;
        [DataMember(12), Display(name: "Strength", category: CategorySSS), DataMemberRange(0, 1, 0.01f, 0.1f, 4), DefaultValue(0.133f)] public float SSSStrength { get; set; } = 0.133f;
        [DataMember(13), Display(name: "Color", category: CategorySSS)] public Color4 SSSColor { get; set; } = new Color4(0.1541919f, 0.8857628f, 0.990566f, 1.0f);

        [DataMember(20), Display(name: "Bias LOD 0", category: CategoryFoam), DataMemberRange(0, 10, 0.01f, 0.1f, 4), DefaultValue(0.84f)] public float FoamBiasLOD0 { get; set; } = 0.84f;
        [DataMember(21), Display(name: "Bias LOD 1", category: CategoryFoam), DataMemberRange(0, 10, 0.01f, 0.1f, 4), DefaultValue(1.83f)] public float FoamBiasLOD1 { get; set; } = 1.83f;
        [DataMember(22), Display(name: "Bias LOD 2", category: CategoryFoam), DataMemberRange(0, 10, 0.01f, 0.1f, 4), DefaultValue(2.72f)] public float FoamBiasLOD2 { get; set; } = 2.72f;
        [DataMember(23), Display(name: "Scale", category: CategoryFoam), DataMemberRange(0, 10, 0.01f, 0.1f, 4), DefaultValue(2.4f)] public float FoamScale { get; set; } = 2.4f;
        [DataMember(24), Display(name: "Color", category: CategoryFoam)] public Color4 FoamColor { get; set; } = new Color4(1, 1, 1, 1);

        [DataMember(30), Display(name: "Roguhness", category: CategorySurface), DataMemberRange(0, 1, 0.01f, 0.1f, 4), DefaultValue(0.311f)] public float Roughness { get; set; } = 0.311f;
        [DataMember(31), Display(name: "Roguhness scale", category: CategorySurface), DataMemberRange(0, 1, 0.01f, 0.1f, 4), DefaultValue(0.0044f)] public float RoughnessScale { get; set; } = 0.0044f;
        [DataMember(32), Display(name: "Max gloss", category: CategorySurface), DataMemberRange(0, 1, 0.01f, 0.1f, 4), DefaultValue(0.91f)] public float MaxGloss { get; set; } = 0.91f;

        [DataMember(33), Display(name: "Color", category: CategorySurface)] public Color3 Color { get; set; } = new Color3(0.03457636f, 0.1229746f, 0.1981132f);
    }
}
