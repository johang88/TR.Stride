using System;
using System.Collections.Generic;
using System.ComponentModel;
using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Shaders;

namespace TR.Stride.Ocean
{
    /// <summary>
    /// We use the emissive map as it gives us a good extension point for most streams and we can override the final shadingColor after lighting has been computed
    /// </summary>
    [DataContract("MaterialOceanEmissiveMapFeature")]
    [Display("Ocean Emissive Map")]
    public class MaterialOceanEmissiveMapFeature : MaterialFeature, IMaterialEmissiveFeature, IMaterialStreamProvider
    {
        private static readonly MaterialStreamDescriptor EmissiveStream = new MaterialStreamDescriptor("Emissive", "matEmissive", MaterialKeys.EmissiveValue.PropertyType);

        public MaterialOceanEmissiveMapFeature() : this(new ComputeTextureColor())
        {
        }

        public MaterialOceanEmissiveMapFeature(IComputeColor emissiveMap)
        {
            if (emissiveMap == null) throw new ArgumentNullException("emissiveMap");
            EmissiveMap = emissiveMap;
            Intensity = new ComputeFloat(1.0f);
        }

        /// <summary>
        /// Gets or sets the diffuse map.
        /// </summary>
        /// <value>The diffuse map.</value>
        /// <userdoc>The map specifying the color emitted by the material.</userdoc>
        [Display("Emissive Map")]
        [NotNull]
        [DataMember(10)]
        public IComputeColor EmissiveMap { get; set; }

        /// <summary>
        /// Gets or sets the intensity.
        /// </summary>
        /// <value>The intensity.</value>
        /// <userdoc>The map specifying the intensity of the light emitted by the material. This scales the color value specified by emissive map.</userdoc>
        [Display("Intensity")]
        [NotNull]
        [DataMember(20)]
        public IComputeScalar Intensity { get; set; }

        public override void GenerateShader(MaterialGeneratorContext context)
        {
            context.SetStream(EmissiveStream.Stream, EmissiveMap, MaterialKeys.EmissiveMap, MaterialKeys.EmissiveValue);
            context.SetStream("matEmissiveIntensity", Intensity, MaterialKeys.EmissiveIntensityMap, MaterialKeys.EmissiveIntensity);

            var shaderBuilder = context.AddShading(this);
            shaderBuilder.ShaderSources.Add(new ShaderClassSource("MaterialOceanSurfaceEmissiveShading"));
        }

        public bool Equals(IMaterialShadingModelFeature other)
        {
            return other is MaterialEmissiveMapFeature;
        }

        public IEnumerable<MaterialStreamDescriptor> GetStreams()
        {
            yield return EmissiveStream;
        }
    }
}
