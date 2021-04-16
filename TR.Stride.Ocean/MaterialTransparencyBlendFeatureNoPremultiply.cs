using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Shaders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Ocean
{
    /// <summary>
    /// Uses a non pre multiply alpha blend mode, works just like regular alpha blend otherwise.
    /// </summary>
    [DataContract]
    public class MaterialTransparencyBlendFeatureNoPremultiply : MaterialFeature, IMaterialTransparencyFeature
    {
        public const int ShadingColorAlphaFinalCallbackOrder = MaterialGeneratorContext.DefaultFinalCallbackOrder;

        private static readonly MaterialStreamDescriptor AlphaBlendStream = new MaterialStreamDescriptor("DiffuseSpecularAlphaBlend", "matDiffuseSpecularAlphaBlend", MaterialKeys.DiffuseSpecularAlphaBlendValue.PropertyType);

        private static readonly MaterialStreamDescriptor AlphaBlendColorStream = new MaterialStreamDescriptor("DiffuseSpecularAlphaBlend - Color", "matAlphaBlendColor", MaterialKeys.AlphaBlendColorValue.PropertyType);

        private static readonly PropertyKey<bool> HasFinalCallback = new PropertyKey<bool>("MaterialTransparencyAdditiveFeature.HasFinalCallback", typeof(MaterialTransparencyAdditiveFeature));

        /// <summary>
        /// Initializes a new instance of the <see cref="MaterialTransparencyBlendFeature"/> class.
        /// </summary>
        public MaterialTransparencyBlendFeatureNoPremultiply()
        {
            Alpha = new ComputeFloat(1f);
            Tint = new ComputeColor(Color.White);
        }

        /// <summary>
        /// Gets or sets the alpha.
        /// </summary>
        /// <value>The alpha.</value>
        /// <userdoc>An additional factor that can be used to modulate original alpha of the material.</userdoc>
        [NotNull]
        [DataMember(10)]
        [DataMemberRange(0.0, 1.0, 0.01, 0.1, 2)]
        public IComputeScalar Alpha { get; set; }

        /// <summary>
        /// Gets or sets the tint color.
        /// </summary>
        /// <value>The tint.</value>
        /// <userdoc>The tint color to apply on the material during the blend.</userdoc>
        [NotNull]
        [DataMember(20)]
        public IComputeColor Tint { get; set; }

        public override void GenerateShader(MaterialGeneratorContext context)
        {
            var alpha = Alpha ?? new ComputeFloat(1f);
            var tint = Tint ?? new ComputeColor(Color.White);

            //alpha.ClampFloat(0, 1); // TODO: Material utility is internal ....

            // Use pre-multiplied alpha to support both additive and alpha blending
            if (context.MaterialPass.BlendState == null)
                context.MaterialPass.BlendState = BlendStates.NonPremultiplied;
            context.MaterialPass.HasTransparency = true;
            // Disable alpha-to-coverage. We wanna do alpha blending, not alpha testing.
            context.MaterialPass.AlphaToCoverage = false;
            // TODO GRAPHICS REFACTOR
            //context.Parameters.SetResourceSlow(Effect.BlendStateKey, BlendState.NewFake(blendDesc));

            context.SetStream(AlphaBlendStream.Stream, alpha, MaterialKeys.DiffuseSpecularAlphaBlendMap, MaterialKeys.DiffuseSpecularAlphaBlendValue, Color.White);
            context.SetStream(AlphaBlendColorStream.Stream, tint, MaterialKeys.AlphaBlendColorMap, MaterialKeys.AlphaBlendColorValue, Color.White);

            context.MaterialPass.Parameters.Set(MaterialKeys.UsePixelShaderWithDepthPass, true);

            if (!context.Tags.Get(HasFinalCallback))
            {
                context.Tags.Set(HasFinalCallback, true);
                context.AddFinalCallback(MaterialShaderStage.Pixel, AddDiffuseSpecularAlphaBlendColor, ShadingColorAlphaFinalCallbackOrder);
            }
        }

        private void AddDiffuseSpecularAlphaBlendColor(MaterialShaderStage stage, MaterialGeneratorContext context)
        {
            context.AddShaderSource(MaterialShaderStage.Pixel, new ShaderClassSource("MaterialSurfaceDiffuseSpecularAlphaBlendColor"));
        }
    }
}
