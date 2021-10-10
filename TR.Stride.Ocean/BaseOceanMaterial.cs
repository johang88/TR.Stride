using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Ocean
{
    [DataContract]
    public abstract class BaseOceanMaterial : IOceanMaterial
    {
        private const string CategorySSS = "SSS";
        private const string CategoryFoam = "Foam";
        private const string CategorySurface = "Surface";

        [DataMember(0), DataMemberRange(0, 100, 0.01f, 0.1f, 4), DefaultValue(1.0f)] public float LodScale { get; set; } = 1.0f;

        [DataMember(10), Display(name: "Base", category: CategorySSS), DataMemberRange(-1, 1, 0.01f, 0.1f, 4), DefaultValue(-0.1f)] public float SSSBase { get; set; } = -0.1f;
        [DataMember(11), Display(name: "Scale", category: CategorySSS), DataMemberRange(0, 10, 0.01f, 0.1f, 4), DefaultValue(4.8f)] public float SSSScale { get; set; } = 4.8f;
        [DataMember(12), Display(name: "Strength", category: CategorySSS), DataMemberRange(0, 1, 0.01f, 0.1f, 4), DefaultValue(0.133f)] public float SSSStrength { get; set; } = 0.133f;
        [DataMember(13), Display(name: "Color", category: CategorySSS)] public Color4 SSSColor { get; set; } = new Color4(0.1541919f, 0.8857628f, 0.990566f, 1.0f);

        [DataMember(20), Display(name: "Bias LOD 0", category: CategoryFoam), DataMemberRange(0, 10, 0.01f, 0.1f, 4), DefaultValue(0.84f)] public float FoamBiasLOD0 { get; set; } = 0.84f;
        [DataMember(21), Display(name: "Bias LOD 1", category: CategoryFoam), DataMemberRange(0, 10, 0.01f, 0.1f, 4), DefaultValue(1.83f)] public float FoamBiasLOD1 { get; set; } = 1.83f;
        [DataMember(22), Display(name: "Bias LOD 2", category: CategoryFoam), DataMemberRange(0, 10, 0.01f, 0.1f, 4), DefaultValue(2.72f)] public float FoamBiasLOD2 { get; set; } = 2.72f;
        [DataMember(23), Display(name: "Scale", category: CategoryFoam), DataMemberRange(0, 10, 0.01f, 0.1f, 4), DefaultValue(2.4f)] public float FoamScale { get; set; } = 2.4f;
        [DataMember(24), Display(name: "Color", category: CategoryFoam)] public Color4 FoamColor { get; set; } = new Color4(1, 1, 1, 1);
        [DataMember(25), Display(name: "Contact Foam", category: CategoryFoam)] public float ContactFoam { get; set; } = 1.0f;
        [DataMember(26), Display(name: "Foam Texture", category: CategoryFoam)] public Texture FoamTexture { get; set; }

        [DataMember(30), Display(name: "Roguhness", category: CategorySurface), DataMemberRange(0, 1, 0.01f, 0.1f, 4), DefaultValue(0.311f)] public float Roughness { get; set; } = 0.311f;
        [DataMember(31), Display(name: "Roguhness scale", category: CategorySurface), DataMemberRange(0, 1, 0.01f, 0.1f, 4), DefaultValue(0.0044f)] public float RoughnessScale { get; set; } = 0.0044f;
        [DataMember(32), Display(name: "Max gloss", category: CategorySurface), DataMemberRange(0, 1, 0.01f, 0.1f, 4), DefaultValue(0.91f)] public float MaxGloss { get; set; } = 0.91f;

        [DataMember(33), Display(name: "Color", category: CategorySurface)] public Color3 Color { get; set; } = new Color3(0.03457636f, 0.1229746f, 0.1981132f);

        [DataMember(34), Display(name: "Shore Color", category: CategorySurface)] public Color3 ShoreColor { get; set; } = new Color3(0.03457636f, 0.1229746f, 0.1981132f);
        [DataMember(34), Display(name: "Refraction Strength", category: CategorySurface), DataMemberRange(0, 1000, 0.01f, 1.0f, 2), DefaultValue(0.02f)] public float RefractionStrength { get; set; } = 50.0f;
        [DataMember(34), Display(name: "Refraction Distance Multiplier", category: CategorySurface), DataMemberRange(0, 1, 0.01f, 0.1f, 4), DefaultValue(0.02f)] public float RefractionDistanceMultiplier { get; set; } = 0.02f;

        [DataMember(40)] public LightComponent Sun { get; set; }

        public abstract Material CreateMaterial(GraphicsDevice graphicsDevice, int lod);

        public void UpdateMaterials(GraphicsDevice graphicsDevice, OceanComponent component, Material[] materials, WavesCascade[] cascades)
        {
            foreach (var material in materials)
            {
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Displacement_c0, cascades[0].Displacement);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Displacement_c1, cascades[1].Displacement);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Displacement_c2, cascades[2].Displacement);

                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Turbulence_c0, cascades[0].Turbulence);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Turbulence_c1, cascades[1].Turbulence);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Turbulence_c2, cascades[2].Turbulence);

                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Derivatives_c0, cascades[0].Derivatives);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Derivatives_c1, cascades[1].Derivatives);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Derivatives_c2, cascades[2].Derivatives);

                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.LengthScale0, component.LengthScale0);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.LengthScale1, component.LengthScale1);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.LengthScale2, component.LengthScale2);

                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.LodScale, LodScale);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.SSSBase, SSSBase);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.SSSScale, SSSScale);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.SSSStrength, SSSStrength);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.SSSColor, SSSColor);

                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.FoamBiasLOD0, FoamBiasLOD0);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.FoamBiasLOD1, FoamBiasLOD1);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.FoamBiasLOD2, FoamBiasLOD2);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.FoamScale, FoamScale);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.FoamColor, FoamColor);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.ContactFoam, ContactFoam);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.FoamTexture, FoamTexture);

                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Roughness, Roughness);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.RoughnessScale, RoughnessScale);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.MaxGloss, MaxGloss);

                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Color, Color);

                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.ShoreColor, ShoreColor);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.RefractionStrength, RefractionStrength);
                material.Passes[0].Parameters.Set(OceanShadingCommonKeys.RefractionDistanceMultiplier, RefractionDistanceMultiplier);

                if (Sun != null)
                {
                    var lightDirection = Vector3.TransformNormal(-Vector3.UnitZ, Sun.Entity.Transform.WorldMatrix);
                    lightDirection.Normalize();

                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.LightDirectionWS, lightDirection);
                }
            }
        }
    }
}
