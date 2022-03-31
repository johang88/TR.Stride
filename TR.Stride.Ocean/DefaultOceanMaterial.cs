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
    /// <summary>
    /// Creates an ocean material at runtime
    /// </summary>
    [DataContract]
    public class DefaultOceanMaterial : BaseOceanMaterial
    {
        protected virtual MaterialDescriptor CreateDescriptor(int lod)
        {
            return new MaterialDescriptor
            {
                Attributes = new MaterialAttributes
                {
                    // Setup shaders
                    Emissive = new MaterialOceanEmissiveMapFeature
                    {
                        EmissiveMap = new ComputeShaderClassColor
                        {
                            MixinReference = "OceanEmissive" // TODO: Use transaparent feature instead
                        },
                        Intensity = new ComputeFloat(1.0f)
                    },
                    Displacement = new MaterialDisplacementMapFeature
                    {
                        ScaleAndBias = false,
                        Intensity = new ComputeFloat(0),
                        DisplacementMap = new ComputeShaderClassScalar
                        {
                            MixinReference = "OceanDisplacement"
                        }
                    },
                    // Rest is just to make sure we get the render features we want
                    // Actual values are overriden in emissive shader
                    MicroSurface = new MaterialGlossinessMapFeature
                    {
                        GlossinessMap = new ComputeFloat(0.9f)
                    },
                    Diffuse = new MaterialDiffuseMapFeature
                    {
                        DiffuseMap = new ComputeColor(new Color4(0, 0, 0.0f, 1))
                    },
                    DiffuseModel = new MaterialDiffuseLambertModelFeature(),
                    Specular = new MaterialMetalnessMapFeature
                    {
                        MetalnessMap = new ComputeFloat(0.0f)
                    },
                    SpecularModel = new MaterialSpecularMicrofacetModelFeature
                    {
                        Environment = new MaterialSpecularMicrofacetEnvironmentGGXPolynomial() // TODO: Use lookup, need to find a way to locate the lookup texture first as the AttachedReferenceManager does not manage this at runtime ...
                    },
                    Transparency = new MaterialTransparencyBlendFeatureNoPremultiply
                    {
                        Alpha = new ComputeFloat(1),
                        Tint = new ComputeColor(new Color4(1, 1, 1, 1))
                    }
                }
            };
        }

        public override Material CreateMaterial(GraphicsDevice graphicsDevice, int lod)
        {
            var material = Material.New(graphicsDevice, CreateDescriptor(lod));

            material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Lod, lod);

            return material;
        }
    }
}
