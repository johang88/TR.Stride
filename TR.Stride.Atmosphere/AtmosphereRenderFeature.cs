using Stride.Graphics;
using Stride.Rendering;
using System;
using System.Collections.Generic;
using System.Text;
using Stride.Core.Mathematics;
using Stride.Rendering.Lights;
using Stride.Engine;
using Stride.Rendering.ComputeEffect;
using Stride.Core;
using System.Threading;
using System.Linq;
using Stride.Core.Storage;
using Stride.Rendering.Images;

namespace TR.Stride.Atmosphere
{
    public class AtmosphereRenderFeature : RootEffectRenderFeature
    {
        [DataMember, Display("Transmittance LUT", "Texture Settings")]
        public TextureSettings2d TransmittanceLutSettings { get; set; } = new TextureSettings2d(256, 64, PixelFormat.R16G16B16A16_Float);
        [DataMember, Display("Multiscattering", "Texture Settings")]
        public TextureSettingsSquare MultiScatteringTextureSettings { get; set; } = new TextureSettingsSquare(32, PixelFormat.R16G16B16A16_Float);
        [DataMember, Display("Sky View LUT", "Texture Settings")]
        public TextureSettings2d SkyViewLutSettings { get; set; } = new TextureSettings2d(192, 108, PixelFormat.R11G11B10_Float);
        [DataMember, Display("Atmosphere scattering volume", "Texture Settings")]
        public TextureSettingsVolume AtmosphereCameraScatteringVolumeSettings { get; set; } = new TextureSettingsVolume(32, 32, PixelFormat.R16G16B16A16_Float);

        [DataMember, Display("Fast Sky", "Performance")]
        public bool FastSky { get; set; } = false;
        [DataMember, Display("Fast Aerial Perspective", "Performance")]
        public bool FastAerialPerspectiveEnabled { get; set; } = false;

        [DataMember, Display("Draw Textures", "Debug")]
        public bool DrawDebugTextures { get; set; } = false;

        public Texture TransmittanceLutTexture { get; private set; }
        private Texture _multiScatteringTexture = null;
        private Texture _skyViewLutTexture = null;
        private Texture _atmosphereCameraScatteringVolumeTexture = null;

        private ImageEffectShader _transmittanceLutEffect = null;
        private ImageEffectShader _skyViewLutEffect = null;
        private ComputeEffectShader _renderMultipleScatteringTextureEffect = null;

        private MutablePipelineState _renderAtmosphereScatteringVolumePipelineState = null;
        private DynamicEffectInstance _renderAtmosphereScatteringVolumeEffect = null;

        private DescriptorSet[] _descriptorSets = null;

        private LogicalGroupReference _atmosphereLogicalGroupKey;

        private ObjectId _atmopshereLayoutHash;
        private ParameterCollection _atmosphereParameters = new ParameterCollection();

        public override Type SupportedRenderObjectType => typeof(AtmosphereRenderObject);

        private SpriteBatch _spriteBatch;

        protected override void InitializeCore()
        {
            base.InitializeCore();

            _skyViewLutTexture = Texture.New2D(Context.GraphicsDevice, SkyViewLutSettings.Width, SkyViewLutSettings.Height, SkyViewLutSettings.Format, TextureFlags.RenderTarget | TextureFlags.ShaderResource);
            _atmosphereCameraScatteringVolumeTexture = Texture.New3D(Context.GraphicsDevice, AtmosphereCameraScatteringVolumeSettings.Size, AtmosphereCameraScatteringVolumeSettings.Size, AtmosphereCameraScatteringVolumeSettings.Slices, AtmosphereCameraScatteringVolumeSettings.Format, TextureFlags.RenderTarget | TextureFlags.ShaderResource);
            _multiScatteringTexture = Texture.New2D(Context.GraphicsDevice, MultiScatteringTextureSettings.Size, MultiScatteringTextureSettings.Size, MultiScatteringTextureSettings.Format, TextureFlags.UnorderedAccess | TextureFlags.ShaderResource);
            TransmittanceLutTexture = Texture.New2D(Context.GraphicsDevice, TransmittanceLutSettings.Width, TransmittanceLutSettings.Height, TransmittanceLutSettings.Format, TextureFlags.UnorderedAccess | TextureFlags.RenderTarget | TextureFlags.ShaderResource);

            _transmittanceLutEffect = new ImageEffectShader("AtmosphereRenderTransmittanceLutEffect");
            _skyViewLutEffect = new ImageEffectShader("AtmosphereRenderSkyViewLutEffect");
            _renderMultipleScatteringTextureEffect = new ComputeEffectShader(Context) { ShaderSourceName = "AtmosphereMultipleScatteringTextureEffect" };

            _renderAtmosphereScatteringVolumeEffect = new DynamicEffectInstance("AtmosphereRenderScatteringCameraVolumeEffect");
            _renderAtmosphereScatteringVolumeEffect.Initialize(Context.Services);

            _renderAtmosphereScatteringVolumePipelineState = new MutablePipelineState(Context.GraphicsDevice);
            _renderAtmosphereScatteringVolumePipelineState.State.SetDefaults();
            _renderAtmosphereScatteringVolumePipelineState.State.PrimitiveType = PrimitiveType.TriangleList;

            _atmosphereLogicalGroupKey = CreateDrawLogicalGroup("Atmosphere");

            _spriteBatch = new SpriteBatch(Context.GraphicsDevice);
        }

        public override void Unload()
        {
            base.Unload();

            _multiScatteringTexture?.Dispose();
            _multiScatteringTexture = null;

            TransmittanceLutTexture?.Dispose();
            TransmittanceLutTexture = null;

            _skyViewLutTexture?.Dispose();
            _skyViewLutTexture = null;

            _atmosphereCameraScatteringVolumeTexture?.Dispose();
            _atmosphereCameraScatteringVolumeTexture = null;

            _transmittanceLutEffect?.Dispose();
            _transmittanceLutEffect = null;

            _skyViewLutEffect?.Dispose();
            _skyViewLutEffect = null;

            _renderMultipleScatteringTextureEffect?.Dispose();
            _renderMultipleScatteringTextureEffect = null;

            _renderAtmosphereScatteringVolumeEffect?.Dispose();
            _renderAtmosphereScatteringVolumeEffect = null;

            _spriteBatch?.Dispose();
            _spriteBatch = null;
        }

        protected override void ProcessPipelineState(RenderContext context, RenderNodeReference renderNodeReference, ref RenderNode renderNode, RenderObject renderObject, PipelineStateDescription pipelineState)
        {
            base.ProcessPipelineState(context, renderNodeReference, ref renderNode, renderObject, pipelineState);

            pipelineState.DepthStencilState = new DepthStencilStateDescription(false, false);

            pipelineState.BlendState.AlphaToCoverageEnable = false;
            pipelineState.BlendState.IndependentBlendEnable = false;

            ref var blendState0 = ref pipelineState.BlendState.RenderTarget0;

            blendState0.BlendEnable = true;

            blendState0.ColorSourceBlend = Blend.One;
            blendState0.ColorDestinationBlend = Blend.InverseSourceAlpha;
            blendState0.ColorBlendFunction = BlendFunction.Add;

            blendState0.AlphaSourceBlend = Blend.Zero;
            blendState0.AlphaDestinationBlend = Blend.One;
            blendState0.AlphaBlendFunction = BlendFunction.Add;

            pipelineState.PrimitiveType = PrimitiveType.TriangleList;
        }

        public override void PrepareEffectPermutationsImpl(RenderDrawContext context)
        {
            base.PrepareEffectPermutationsImpl(context);

            var renderEffects = RenderData.GetData(RenderEffectKey);
            var effectSlotCount = EffectPermutationSlotCount;

            foreach (AtmosphereRenderObject renderObject in RenderObjects)
            {
                var staticObjectNode = renderObject.StaticObjectNode;

                for (int i = 0; i < effectSlotCount; ++i)
                {
                    var staticEffectObjectNode = staticObjectNode * effectSlotCount + i;
                    var renderEffect = renderEffects[staticEffectObjectNode];

                    // Skip effects not used during this frame
                    if (renderEffect == null || !renderEffect.IsUsedDuringThisFrame(RenderSystem))
                        continue;

                    renderEffect.EffectValidator.ValidateParameter(AtmosphereParameters.FastSkyEnabled, FastSky);
                    renderEffect.EffectValidator.ValidateParameter(AtmosphereParameters.FastAerialPerspectiveEnabled, FastAerialPerspectiveEnabled);
                    renderEffect.EffectValidator.ValidateParameter(AtmosphereParameters.RenderSunDisk, renderObject.Component.RenderSunDisk);
                }
            }
        }

        public override void Draw(RenderDrawContext context, RenderView renderView, RenderViewStage renderViewStage, int startIndex, int endIndex)
        {
            base.Draw(context, renderView, renderViewStage, startIndex, endIndex);

            var commandList = context.GraphicsContext.CommandList;

            // Only one atmosphere is supported, so we can cheat
            if (startIndex == endIndex)
                return;

            var index = startIndex;

            var renderNodeReference = renderViewStage.SortedRenderNodes[index].RenderNode;
            var renderNode = GetRenderNode(renderNodeReference);
            var renderObject = (AtmosphereRenderObject)renderNode.RenderObject;

            if (renderObject.Component.Sun == null)
                return;

            if (!(renderObject.Component.Sun.Type is LightDirectional light))
                return;

            var renderEffect = GetRenderNode(renderNodeReference).RenderEffect;
            if (renderEffect.Effect == null)
                return;

            // Update parameters
            var drawLayout = renderNode.RenderEffect.Reflection?.PerDrawLayout;
            if (drawLayout == null)
                return;

            var drawAtmosphere = drawLayout.GetLogicalGroup(_atmosphereLogicalGroupKey);
            if (drawAtmosphere.Hash == ObjectId.Empty)
                return;

            if (_atmopshereLayoutHash != drawAtmosphere.Hash)
            {
                _atmopshereLayoutHash = drawAtmosphere.Hash;

                var atmosphereParameterLayout = new ParameterCollectionLayout();
                atmosphereParameterLayout.ProcessLogicalGroup(drawLayout, ref drawAtmosphere);

                _atmosphereParameters.UpdateLayout(atmosphereParameterLayout);
            }

            SetParameters(renderView, renderObject.Component, _atmosphereParameters, null);
            _atmosphereParameters.Set(AtmosphereParametersBaseKeys.TransmittanceLutTexture, TransmittanceLutTexture);

            renderNode.Resources.UpdateLogicalGroup(ref drawAtmosphere, _atmosphereParameters);

            // Set texture resources
            renderNode.Resources.DescriptorSet.SetShaderResourceView(drawAtmosphere.DescriptorSlotStart + 0, TransmittanceLutTexture);
            renderNode.Resources.DescriptorSet.SetShaderResourceView(drawAtmosphere.DescriptorSlotStart + 1, _skyViewLutTexture);
            renderNode.Resources.DescriptorSet.SetShaderResourceView(drawAtmosphere.DescriptorSlotStart + 2, _multiScatteringTexture);
            renderNode.Resources.DescriptorSet.SetShaderResourceView(drawAtmosphere.DescriptorSlotStart + 3, _atmosphereCameraScatteringVolumeTexture);

            // Update cbuffer
            var resourceGroupOffset = ComputeResourceGroupOffset(renderNodeReference);
            renderEffect.Reflection.BufferUploader.Apply(commandList, ResourceGroupPool, resourceGroupOffset);

            // Bind descriptor sets
            if (_descriptorSets == null || _descriptorSets.Length < EffectDescriptorSetSlotCount)
            {
                _descriptorSets = new DescriptorSet[EffectDescriptorSetSlotCount];
            }

            for (int i = 0; i < _descriptorSets.Length; ++i)
            {
                var resourceGroup = ResourceGroupPool[resourceGroupOffset++];
                if (resourceGroup != null)
                {
                    _descriptorSets[i] = resourceGroup.DescriptorSet;
                }
            }

            // Transmittance LUT
            using (context.PushRenderTargetsAndRestore())
            {
                SetParameters(renderView, renderObject.Component, _transmittanceLutEffect.Parameters, null);

                _transmittanceLutEffect.Parameters.Set(AtmosphereParametersBaseKeys.Resolution, new Vector2(TransmittanceLutTexture.Width, TransmittanceLutTexture.Height));

                _transmittanceLutEffect.SetOutput(TransmittanceLutTexture);
                _transmittanceLutEffect.Draw(context, "Atmosphere.Transmittance LUT");
            }

            commandList.ResourceBarrierTransition(TransmittanceLutTexture, GraphicsResourceState.PixelShaderResource);

            // Multi scattering texture
            using (context.QueryManager.BeginProfile(Color4.Black, ProfilingKeys.MultiScatteringTexture))
            {
                commandList.ResourceBarrierTransition(_multiScatteringTexture, GraphicsResourceState.UnorderedAccess);

                SetParameters(renderView, renderObject.Component, _renderMultipleScatteringTextureEffect.Parameters, null);

                _renderMultipleScatteringTextureEffect.Parameters.Set(AtmosphereMultipleScatteringTextureEffectCSKeys.OutputTexture, _multiScatteringTexture);
                _renderMultipleScatteringTextureEffect.Parameters.Set(AtmosphereParametersBaseKeys.SunIlluminance, new Vector3(1, 1, 1));
                _renderMultipleScatteringTextureEffect.Parameters.Set(AtmosphereParametersBaseKeys.TransmittanceLutTexture, TransmittanceLutTexture);

                _renderMultipleScatteringTextureEffect.ThreadGroupCounts = new Int3(_multiScatteringTexture.Width, _multiScatteringTexture.Height, 1);
                _renderMultipleScatteringTextureEffect.ThreadNumbers = new Int3(1, 1, 64);

                _renderMultipleScatteringTextureEffect.Draw(context);
            }

            commandList.ResourceBarrierTransition(_multiScatteringTexture, GraphicsResourceState.PixelShaderResource);

            // Sky view LUT
            using (context.PushRenderTargetsAndRestore())
            {
                SetParameters(renderView, renderObject.Component, _skyViewLutEffect.Parameters, null);

                _skyViewLutEffect.Parameters.Set(AtmosphereParametersBaseKeys.Resolution, new Vector2(_skyViewLutTexture.Width, _skyViewLutTexture.Height));
                _skyViewLutEffect.Parameters.Set(AtmosphereParametersBaseKeys.TransmittanceLutTexture, TransmittanceLutTexture);
                _skyViewLutEffect.Parameters.Set(AtmosphereParametersBaseKeys.MultiScatTexture, _multiScatteringTexture);

                _skyViewLutEffect.SetOutput(_skyViewLutTexture);
                _skyViewLutEffect.Draw(context, "Atmosphere.Sky View LUT");
            }

            commandList.ResourceBarrierTransition(_skyViewLutTexture, GraphicsResourceState.PixelShaderResource);

            // Atmosphere camera scattering volume
            using (context.QueryManager.BeginProfile(Color4.Black, ProfilingKeys.ScatteringCameraVolume))
            {
                RenderAtmosphereCameraScatteringVolume(context, renderView, renderObject.Component);
            }

            commandList.ResourceBarrierTransition(_atmosphereCameraScatteringVolumeTexture, GraphicsResourceState.PixelShaderResource);

            // Ray march atmosphere render
            using (context.QueryManager.BeginProfile(Color4.Black, ProfilingKeys.RayMarching))
            {
                commandList.SetPipelineState(renderEffect.PipelineState);
                commandList.SetDescriptorSets(0, _descriptorSets);

                commandList.Draw(3, 0);
            }

            if (DrawDebugTextures)
            {
                _spriteBatch.Begin(context.GraphicsContext, SpriteSortMode.Immediate);

                var y = renderView.ViewSize.Y - 20 - TransmittanceLutTexture.Height;
                _spriteBatch.Draw(TransmittanceLutTexture, new Vector2(20, y));

                y -= 20 + _multiScatteringTexture.Height;
                _spriteBatch.Draw(_multiScatteringTexture, new Vector2(20, y));

                y -= 20 + _skyViewLutTexture.Height;
                _spriteBatch.Draw(_skyViewLutTexture, new Vector2(20, y));

                _spriteBatch.End();
            }
        }

        private void RenderAtmosphereCameraScatteringVolume(RenderDrawContext context, RenderView renderView, AtmosphereComponent component)
        {
            // Not using ImageEffectShader as we have custom geometry shader and need to use DrawInstanced with custom parameters

            var graphicsDevice = context.GraphicsDevice;
            var graphicsContext = context.GraphicsContext;
            var commandList = context.GraphicsContext.CommandList;

            _renderAtmosphereScatteringVolumeEffect.UpdateEffect(graphicsDevice);

            _renderAtmosphereScatteringVolumePipelineState.State.RootSignature = _renderAtmosphereScatteringVolumeEffect.RootSignature;
            _renderAtmosphereScatteringVolumePipelineState.State.EffectBytecode = _renderAtmosphereScatteringVolumeEffect.Effect.Bytecode;

            using (context.PushRenderTargetsAndRestore())
            {
                commandList.ResourceBarrierTransition(_atmosphereCameraScatteringVolumeTexture, GraphicsResourceState.RenderTarget);

                commandList.SetRenderTarget(null, _atmosphereCameraScatteringVolumeTexture);

                var oldViewport = commandList.Viewport;
                commandList.SetViewport(new Viewport(0, 0, _atmosphereCameraScatteringVolumeTexture.Width, _atmosphereCameraScatteringVolumeTexture.Height));

                _renderAtmosphereScatteringVolumePipelineState.State.Output.CaptureState(commandList);
                _renderAtmosphereScatteringVolumePipelineState.Update();

                commandList.SetPipelineState(_renderAtmosphereScatteringVolumePipelineState.CurrentState);

                var parameters = _renderAtmosphereScatteringVolumeEffect.Parameters;

                SetParameters(renderView, component, parameters, null);

                parameters.Set(AtmosphereParametersBaseKeys.Resolution, new Vector2(_atmosphereCameraScatteringVolumeTexture.Width, _atmosphereCameraScatteringVolumeTexture.Height));

                parameters.Set(AtmosphereParametersBaseKeys.TransmittanceLutTexture, TransmittanceLutTexture);
                parameters.Set(AtmosphereParametersBaseKeys.MultiScatTexture, _multiScatteringTexture);
                parameters.Set(AtmosphereParametersBaseKeys.SkyViewLutTexture, _skyViewLutTexture);

                _renderAtmosphereScatteringVolumeEffect.Apply(graphicsContext);

                commandList.DrawInstanced(3, _atmosphereCameraScatteringVolumeTexture.Depth);

                commandList.SetViewport(oldViewport);
            }
        }

        public void SetParameters(RenderView renderView, AtmosphereComponent component, ParameterCollection parameters, string compositionName)
        {
            // Convert component data to physical data
            // Mie
            var mieScattering = component.MieScatteringCoefficient.ToVector3() * component.MieScatteringScale;
            var mieExctinction = mieScattering + component.MieAbsorptionCoefficient.ToVector3() * component.MieAbsorptionScale;

            var mieAbsoprtion = mieExctinction - mieScattering;
            mieAbsoprtion.X = Math.Max(0.0f, mieAbsoprtion.X);
            mieAbsoprtion.Y = Math.Max(0.0f, mieAbsoprtion.Y);
            mieAbsoprtion.Z = Math.Max(0.0f, mieAbsoprtion.Z);

            // Rayleigh
            var rayleighScattering = component.RayleighScatteringCoefficient.ToVector3() * component.RayleighScatteringScale;

            // Absorption
            var absorptionExtinction = component.AbsorptionExctinctionCoefficient.ToVector3() * component.AbsorptionExctinctionScale;

            // Not needed when rendering direcitonal light so can be null
            var cameraPos = Vector3.Zero;
            if (renderView != null)
            {
                var inverseViewMatrix = Matrix.Invert(renderView.View);
                var eye = inverseViewMatrix.Row4;
                cameraPos = new Vector3(eye.X, eye.Y, eye.Z);
            }

            var lightDirection = Vector3.TransformNormal(-Vector3.UnitZ, component.Sun.Entity.Transform.WorldMatrix);
            lightDirection.Normalize();

            var sunColor = component.Sun.GetColor() * component.Sun.Intensity;

            // Atmosphere parameters
            parameters.Set(AtmosphereParametersBaseKeys.RayleighDensityExpScale.TryComposeWith(compositionName), -1.0f / component.RayleighScaleHeight);
            parameters.Set(AtmosphereParametersBaseKeys.MieDensityExpScale.TryComposeWith(compositionName), -1.0f / component.MieScaleHeight);

            parameters.Set(AtmosphereParametersBaseKeys.AbsorptionExtinction.TryComposeWith(compositionName), absorptionExtinction);
            parameters.Set(AtmosphereParametersBaseKeys.AbsorptionDensity0LayerWidth.TryComposeWith(compositionName), component.AbsorptionDensity0LayerWidth);
            parameters.Set(AtmosphereParametersBaseKeys.AbsorptionDensity0ConstantTerm.TryComposeWith(compositionName), component.AbsorptionDensity0ConstantTerm);
            parameters.Set(AtmosphereParametersBaseKeys.AbsorptionDensity0LinearTerm.TryComposeWith(compositionName), component.AbsorptionDensity0LinearTerm);
            parameters.Set(AtmosphereParametersBaseKeys.AbsorptionDensity1ConstantTerm.TryComposeWith(compositionName), component.AbsorptionDensity1ConstantTerm);
            parameters.Set(AtmosphereParametersBaseKeys.AbsorptionDensity1LinearTerm.TryComposeWith(compositionName), component.AbsorptionDensity1LinearTerm);

            parameters.Set(AtmosphereParametersBaseKeys.RayleighScattering.TryComposeWith(compositionName), rayleighScattering);

            parameters.Set(AtmosphereParametersBaseKeys.MiePhaseG.TryComposeWith(compositionName), component.MiePhase);
            parameters.Set(AtmosphereParametersBaseKeys.MieScattering.TryComposeWith(compositionName), mieScattering);
            parameters.Set(AtmosphereParametersBaseKeys.MieAbsorption.TryComposeWith(compositionName), mieAbsoprtion);
            parameters.Set(AtmosphereParametersBaseKeys.MieExtinction.TryComposeWith(compositionName), mieExctinction);

            parameters.Set(AtmosphereParametersBaseKeys.GroundAlbedo.TryComposeWith(compositionName), component.GroundAlbedo.ToVector3());
            parameters.Set(AtmosphereParametersBaseKeys.BottomRadius.TryComposeWith(compositionName), component.PlanetRadius);
            parameters.Set(AtmosphereParametersBaseKeys.TopRadius.TryComposeWith(compositionName), component.PlanetRadius + component.AtmosphereHeight);

            parameters.Set(AtmosphereParametersBaseKeys.AerialPespectiveViewDistanceScale.TryComposeWith(compositionName), component.AerialPerspectiveDistanceScale);

            // Lut settings
            parameters.Set(AtmosphereParametersBaseKeys.MultipleScatteringFactor.TryComposeWith(compositionName), component.MultipleScatteringFactor);

            parameters.Set(AtmosphereParametersBaseKeys.MultiScatteringLutResolution.TryComposeWith(compositionName), CalculateResolutionVector(_multiScatteringTexture));
            parameters.Set(AtmosphereParametersBaseKeys.SkyViewLutResolution.TryComposeWith(compositionName), CalculateResolutionVector(_skyViewLutTexture));
            parameters.Set(AtmosphereParametersBaseKeys.TransmittanceLutResolution.TryComposeWith(compositionName), CalculateResolutionVector(TransmittanceLutTexture));

            parameters.Set(AtmosphereParametersBaseKeys.AerialPerspectiveSlicesAndDistancePerSlice.TryComposeWith(compositionName),
                new Vector4(
                    _atmosphereCameraScatteringVolumeTexture.Depth, component.AtmosphereScatteringVolumeKmPerSlice,
                    1.0f / _atmosphereCameraScatteringVolumeTexture.Depth, 1.0f / component.AtmosphereScatteringVolumeKmPerSlice
                    ));

            // Misc
            parameters.Set(AtmosphereParametersBaseKeys.SunDirection.TryComposeWith(compositionName), -lightDirection);

            if (renderView != null)
            {
                parameters.Set(AtmosphereParametersBaseKeys.InvViewProjectionMatrix.TryComposeWith(compositionName), Matrix.Invert(renderView.ViewProjection));
                parameters.Set(AtmosphereParametersBaseKeys.Resolution.TryComposeWith(compositionName), renderView.ViewSize);
            }

            parameters.Set(AtmosphereParametersBaseKeys.CameraPositionWS.TryComposeWith(compositionName), cameraPos);
            parameters.Set(AtmosphereParametersBaseKeys.RayMarchMinMaxSPP.TryComposeWith(compositionName), new Vector2(4, 14));
            parameters.Set(AtmosphereParametersBaseKeys.ScaleToSkyUnit.TryComposeWith(compositionName), component.StrideToAtmosphereUnitScale);
            parameters.Set(AtmosphereParametersBaseKeys.SunIlluminance.TryComposeWith(compositionName), new Vector3(sunColor.R, sunColor.G, sunColor.B));
            parameters.Set(AtmosphereParametersBaseKeys.SunLuminanceFactor.TryComposeWith(compositionName), component.SunLuminanceFactor);
        }

        static Vector4 CalculateResolutionVector(Texture texutre)
            => new Vector4(texutre.Width, texutre.Height, 1.0f / texutre.Width, 1.0f / texutre.Height);
    }
}
