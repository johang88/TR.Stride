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
using Stride.Rendering.ComputeEffect.LambertianPrefiltering;
using Stride.Rendering.Skyboxes;
using Stride.Shaders;
using Stride.Rendering.ComputeEffect.GGXPrefiltering;

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
        public bool FastSky { get; set; } = true;
        [DataMember, Display("Fast Aerial Perspective", "Performance")]
        public bool FastAerialPerspectiveEnabled { get; set; } = true;

        [DataMember, Display("Draw Textures", "Debug")]
        public bool DrawDebugTextures { get; set; } = false;

        public Texture TransmittanceLutTexture { get; private set; }
        private Texture _multiScatteringTexture = null;
        private Texture _skyViewLutTexture = null;
        public Texture AtmosphereCameraScatteringVolumeTexture { get; private set; }
        private Texture _atmosphereCubeMapRenderTarget = null;
        private Texture _atmosphereCubeMap = null;
        private Texture _atmosphereCubeMapSpecular = null;

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

        private LambertianPrefilteringSH _lamberFiltering = null;
        private RadiancePrefilteringGGX _specularRadiancePrefilterGGX = null;

        public AtmosphereComponent Atmosphere { get; private set; }

        protected override void InitializeCore()
        {
            base.InitializeCore();

            SortKey = 0; // Render first in transparent queue

            _skyViewLutTexture = Texture.New2D(Context.GraphicsDevice, SkyViewLutSettings.Width, SkyViewLutSettings.Height, SkyViewLutSettings.Format, TextureFlags.RenderTarget | TextureFlags.ShaderResource);
            AtmosphereCameraScatteringVolumeTexture = Texture.New3D(Context.GraphicsDevice, AtmosphereCameraScatteringVolumeSettings.Size, AtmosphereCameraScatteringVolumeSettings.Size, AtmosphereCameraScatteringVolumeSettings.Slices, AtmosphereCameraScatteringVolumeSettings.Format, TextureFlags.RenderTarget | TextureFlags.ShaderResource);
            _multiScatteringTexture = Texture.New2D(Context.GraphicsDevice, MultiScatteringTextureSettings.Size, MultiScatteringTextureSettings.Size, MultiScatteringTextureSettings.Format, TextureFlags.UnorderedAccess | TextureFlags.ShaderResource);
            TransmittanceLutTexture = Texture.New2D(Context.GraphicsDevice, TransmittanceLutSettings.Width, TransmittanceLutSettings.Height, TransmittanceLutSettings.Format, TextureFlags.UnorderedAccess | TextureFlags.RenderTarget | TextureFlags.ShaderResource);
            _atmosphereCubeMapRenderTarget = Texture.New2D(Context.GraphicsDevice, 64, 64, PixelFormat.R16G16B16A16_Float, TextureFlags.RenderTarget | TextureFlags.ShaderResource);
            _atmosphereCubeMap = Texture.NewCube(Context.GraphicsDevice, 64, PixelFormat.R16G16B16A16_Float, TextureFlags.ShaderResource);
            _atmosphereCubeMapSpecular = Texture.NewCube(Context.GraphicsDevice, 64, MipMapCount.Auto, PixelFormat.R16G16B16A16_Float, TextureFlags.ShaderResource | TextureFlags.UnorderedAccess);

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

            _specularRadiancePrefilterGGX?.Dispose();
            _specularRadiancePrefilterGGX = null;

            _lamberFiltering?.Dispose();
            _lamberFiltering = null;

            _atmosphereCubeMapRenderTarget?.Dispose();
            _atmosphereCubeMapRenderTarget = null;

            _atmosphereCubeMap?.Dispose();
            _atmosphereCubeMap = null;

            _multiScatteringTexture?.Dispose();
            _multiScatteringTexture = null;

            TransmittanceLutTexture?.Dispose();
            TransmittanceLutTexture = null;

            _skyViewLutTexture?.Dispose();
            _skyViewLutTexture = null;

            AtmosphereCameraScatteringVolumeTexture?.Dispose();
            AtmosphereCameraScatteringVolumeTexture = null;

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
                    renderEffect.EffectValidator.ValidateParameter(AtmosphereParameters.EnableClouds, renderObject.Component.EnableClouds);
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

            Atmosphere = null;

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

            Atmosphere = renderObject.Component;

            if (_atmopshereLayoutHash != drawAtmosphere.Hash)
            {
                _atmopshereLayoutHash = drawAtmosphere.Hash;

                var atmosphereParameterLayout = new ParameterCollectionLayout();
                atmosphereParameterLayout.ProcessLogicalGroup(drawLayout, ref drawAtmosphere);

                _atmosphereParameters.UpdateLayout(atmosphereParameterLayout);
            }

            // Transmittance LUT
            using (context.PushRenderTargetsAndRestore())
            {
                SetParameters(context.RenderContext, renderView, renderObject.Component, _transmittanceLutEffect.Parameters, null);

                _transmittanceLutEffect.Parameters.Set(AtmosphereCommonKeys.Resolution, new Vector2(TransmittanceLutTexture.Width, TransmittanceLutTexture.Height));

                _transmittanceLutEffect.SetOutput(TransmittanceLutTexture);
                _transmittanceLutEffect.Draw(context, "Atmosphere.Transmittance LUT");
            }

            commandList.ResourceBarrierTransition(TransmittanceLutTexture, GraphicsResourceState.PixelShaderResource);

            // Multi scattering texture
            using (context.QueryManager.BeginProfile(Color4.Black, ProfilingKeys.MultiScatteringTexture))
            {
                commandList.ResourceBarrierTransition(_multiScatteringTexture, GraphicsResourceState.UnorderedAccess);

                SetParameters(context.RenderContext, renderView, renderObject.Component, _renderMultipleScatteringTextureEffect.Parameters, null);

                _renderMultipleScatteringTextureEffect.Parameters.Set(AtmosphereMultipleScatteringTextureEffectCSKeys.OutputTexture, _multiScatteringTexture);
                _renderMultipleScatteringTextureEffect.Parameters.Set(AtmosphereCommonKeys.SunIlluminance, new Vector3(1, 1, 1));
                _renderMultipleScatteringTextureEffect.Parameters.Set(AtmosphereParametersBaseKeys.TransmittanceLutTexture, TransmittanceLutTexture);

                _renderMultipleScatteringTextureEffect.ThreadGroupCounts = new Int3(_multiScatteringTexture.Width, _multiScatteringTexture.Height, 1);
                _renderMultipleScatteringTextureEffect.ThreadNumbers = new Int3(1, 1, 64);

                _renderMultipleScatteringTextureEffect.Draw(context);
            }

            commandList.ResourceBarrierTransition(_multiScatteringTexture, GraphicsResourceState.PixelShaderResource);

            // Sky view LUT
            using (context.PushRenderTargetsAndRestore())
            {
                SetParameters(context.RenderContext, renderView, renderObject.Component, _skyViewLutEffect.Parameters, null);

                _skyViewLutEffect.Parameters.Set(AtmosphereCommonKeys.Resolution, new Vector2(_skyViewLutTexture.Width, _skyViewLutTexture.Height));
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

            commandList.ResourceBarrierTransition(AtmosphereCameraScatteringVolumeTexture, GraphicsResourceState.PixelShaderResource);

            // Render cube map for ambient
            if (renderObject.Component.Sky != null && renderObject.Component.Sky.Type is LightSkybox lightSkybox && lightSkybox.Skybox != null)
            {
                using (context.QueryManager.BeginProfile(Color4.Black, ProfilingKeys.CubeMap))
                {
                    RenderCubeMap(context, renderView, commandList, renderEffect, drawAtmosphere, renderObject, renderNode, renderNodeReference);
                }

                // Apply prefiltering for diffuse and specular environment maps
                using (context.QueryManager.BeginProfile(Color4.Black, ProfilingKeys.CubeMapPreFilter))
                {
                    if (_lamberFiltering == null)
                    {
                        _lamberFiltering = new LambertianPrefilteringSH(context.RenderContext)
                        {
                            HarmonicOrder = 3
                        };
                    }

                    _lamberFiltering.RadianceMap = _atmosphereCubeMap;
                    using (context.PushRenderTargetsAndRestore())
                    {
                        _lamberFiltering.Draw(context);
                    }

                    var coefficients = _lamberFiltering.PrefilteredLambertianSH.Coefficients;
                    for (int i = 0; i < coefficients.Length; i++)
                    {
                        coefficients[i] = coefficients[i] * SphericalHarmonics.BaseCoefficients[i];
                    }

                    lightSkybox.Skybox.DiffuseLightingParameters.Set(SphericalHarmonicsEnvironmentColorKeys.SphericalColors, coefficients);

                    if (_specularRadiancePrefilterGGX == null)
                    {
                        _specularRadiancePrefilterGGX = new RadiancePrefilteringGGX(context.RenderContext);
                    }

                    _specularRadiancePrefilterGGX.RadianceMap = _atmosphereCubeMap;
                    _specularRadiancePrefilterGGX.PrefilteredRadiance = _atmosphereCubeMapSpecular;

                    using (context.PushRenderTargetsAndRestore())
                    {
                        commandList.ResourceBarrierTransition(_atmosphereCubeMapSpecular, GraphicsResourceState.UnorderedAccess);
                        _specularRadiancePrefilterGGX.Draw(context);

                        // This is to solve an issue where child resources (texture views) bound as UAV wont be properly
                        // reset when binding the parent texture as an SRV and thus resulting in the SRV potentionally failing to bind
                        commandList.ClearState();
                    }

                    commandList.ResourceBarrierTransition(_atmosphereCubeMapSpecular, GraphicsResourceState.PixelShaderResource);

                    lightSkybox.Skybox.SpecularLightingParameters.Set(SkyboxKeys.CubeMap, _atmosphereCubeMapSpecular);
                }
            }

            // Ray march atmosphere render
            using (context.QueryManager.BeginProfile(Color4.Black, ProfilingKeys.RayMarching))
            {
                SetParameters(context.RenderContext, renderView, renderObject.Component, _atmosphereParameters, null);
                UpdateCBuffers(commandList, renderNodeReference, renderNode, renderEffect, ref drawAtmosphere);

                RenderAtmosphere(commandList, renderEffect);
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

        private void UpdateCBuffers(CommandList commandList, RenderNodeReference renderNodeReference, RenderNode renderNode, RenderEffect renderEffect, ref LogicalGroup drawAtmosphere)
        {
            renderNode.Resources.UpdateLogicalGroup(ref drawAtmosphere, _atmosphereParameters);

            // Update cbuffer
            var resourceGroupOffset = ComputeResourceGroupOffset(renderNodeReference);
            renderEffect.Reflection.BufferUploader.Apply(commandList, ResourceGroupPool, resourceGroupOffset);

            // Set texture resources
            renderNode.Resources.DescriptorSet.SetShaderResourceView(drawAtmosphere.DescriptorSlotStart + 0, TransmittanceLutTexture);
            renderNode.Resources.DescriptorSet.SetShaderResourceView(drawAtmosphere.DescriptorSlotStart + 1, _skyViewLutTexture);
            renderNode.Resources.DescriptorSet.SetShaderResourceView(drawAtmosphere.DescriptorSlotStart + 2, _multiScatteringTexture);
            renderNode.Resources.DescriptorSet.SetShaderResourceView(drawAtmosphere.DescriptorSlotStart + 3, AtmosphereCameraScatteringVolumeTexture);

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
        }

        private void RenderCubeMap(RenderDrawContext context, RenderView renderView, CommandList commandList, RenderEffect renderEffect, LogicalGroup drawAtmosphere, AtmosphereRenderObject renderObject, RenderNode renderNode, RenderNodeReference renderNodeReference)
        {
            var inverseViewMatrix = Matrix.Invert(renderView.View);
            var eye = inverseViewMatrix.Row4;
            var cameraPos = new Vector3(eye.X, eye.Y, eye.Z);

            commandList.ResourceBarrierTransition(_atmosphereCubeMap, GraphicsResourceState.CopyDestination);

            using (context.PushRenderTargetsAndRestore())
            {
                commandList.ResourceBarrierTransition(_atmosphereCubeMapRenderTarget, GraphicsResourceState.RenderTarget);
                commandList.SetRenderTarget(null, _atmosphereCubeMapRenderTarget);

                for (int face = 0; face < 6; ++face)
                {
                    Matrix viewMatrix;
                    switch ((CubeMapFace)face)
                    {
                        case CubeMapFace.PositiveX:
                            viewMatrix = Matrix.LookAtRH(cameraPos, cameraPos + Vector3.UnitX, Vector3.UnitY);
                            break;
                        case CubeMapFace.NegativeX:
                            viewMatrix = Matrix.LookAtRH(cameraPos, cameraPos - Vector3.UnitX, Vector3.UnitY);
                            break;
                        case CubeMapFace.PositiveY:
                            viewMatrix = Matrix.LookAtRH(cameraPos, cameraPos + Vector3.UnitY, Vector3.UnitZ);
                            break;
                        case CubeMapFace.NegativeY:
                            viewMatrix = Matrix.LookAtRH(cameraPos, cameraPos - Vector3.UnitY, -Vector3.UnitZ);
                            break;
                        case CubeMapFace.PositiveZ:
                            viewMatrix = Matrix.LookAtRH(cameraPos, cameraPos - Vector3.UnitZ, Vector3.UnitY);
                            break;
                        case CubeMapFace.NegativeZ:
                            viewMatrix = Matrix.LookAtRH(cameraPos, cameraPos + Vector3.UnitZ, Vector3.UnitY);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    var projectionMatrix = Matrix.PerspectiveFovRH(MathUtil.DegreesToRadians(90.0f), 1.0f, renderView.NearClipPlane, renderView.FarClipPlane);

                    SetParameters(context.RenderContext, renderView, renderObject.Component, _atmosphereParameters, null);
                    _atmosphereParameters.Set(AtmosphereCommonKeys.Resolution, new Vector2(_atmosphereCubeMapRenderTarget.Width, _atmosphereCubeMapRenderTarget.Height));
                    _atmosphereParameters.Set(AtmosphereCommonKeys.RenderStage, 1);
                    _atmosphereParameters.Set(AtmosphereCommonKeys.InvViewProjectionMatrix, Matrix.Invert(Matrix.Multiply(viewMatrix, projectionMatrix)));

                    UpdateCBuffers(commandList, renderNodeReference, renderNode, renderEffect, ref drawAtmosphere);

                    RenderAtmosphere(commandList, renderEffect);

                    commandList.ResourceBarrierTransition(context.CommandList.RenderTarget, GraphicsResourceState.CopySource);
                    context.CommandList.CopyRegion(context.CommandList.RenderTarget, 0, null, _atmosphereCubeMap, face);
                }
            }

            commandList.ResourceBarrierTransition(_atmosphereCubeMap, GraphicsResourceState.PixelShaderResource);
            commandList.ResourceBarrierTransition(_atmosphereCubeMapRenderTarget, GraphicsResourceState.PixelShaderResource);
        }

        private void RenderAtmosphere(CommandList commandList, RenderEffect renderEffect)
        {
            commandList.SetPipelineState(renderEffect.PipelineState);
            commandList.SetDescriptorSets(0, _descriptorSets);

            commandList.Draw(3, 0);
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
                commandList.ResourceBarrierTransition(AtmosphereCameraScatteringVolumeTexture, GraphicsResourceState.RenderTarget);

                commandList.SetRenderTarget(null, AtmosphereCameraScatteringVolumeTexture);

                var oldViewport = commandList.Viewport;
                commandList.SetViewport(new Viewport(0, 0, AtmosphereCameraScatteringVolumeTexture.Width, AtmosphereCameraScatteringVolumeTexture.Height));

                _renderAtmosphereScatteringVolumePipelineState.State.Output.CaptureState(commandList);
                _renderAtmosphereScatteringVolumePipelineState.Update();

                commandList.SetPipelineState(_renderAtmosphereScatteringVolumePipelineState.CurrentState);

                var parameters = _renderAtmosphereScatteringVolumeEffect.Parameters;

                SetParameters(context.RenderContext, renderView, component, parameters, null);

                parameters.Set(AtmosphereCommonKeys.Resolution, new Vector2(AtmosphereCameraScatteringVolumeTexture.Width, AtmosphereCameraScatteringVolumeTexture.Height));

                parameters.Set(AtmosphereParametersBaseKeys.TransmittanceLutTexture, TransmittanceLutTexture);
                parameters.Set(AtmosphereParametersBaseKeys.MultiScatTexture, _multiScatteringTexture);
                parameters.Set(AtmosphereParametersBaseKeys.SkyViewLutTexture, _skyViewLutTexture);

                _renderAtmosphereScatteringVolumeEffect.Apply(graphicsContext);

                commandList.DrawInstanced(3, AtmosphereCameraScatteringVolumeTexture.Depth);

                commandList.SetViewport(oldViewport);
            }
        }

        public void SetAtmoshpereParameters(AtmosphereComponent component, ParameterCollection parameters, string compositionName)
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
                    AtmosphereCameraScatteringVolumeTexture.Depth, component.AtmosphereScatteringVolumeKmPerSlice,
                    1.0f / AtmosphereCameraScatteringVolumeTexture.Depth, 1.0f / component.AtmosphereScatteringVolumeKmPerSlice
                    ));

            parameters.Set(AtmosphereParametersBaseKeys.ScaleToSkyUnit.TryComposeWith(compositionName), component.StrideToAtmosphereUnitScale);
        }

        private void SetParameters(RenderContext renderContext, RenderView renderView, AtmosphereComponent component, ParameterCollection parameters, string compositionName)
        {
            SetAtmoshpereParameters(component, parameters, compositionName);

            var cameraPos = Vector3.Zero;
            if (renderView != null)
            {
                var inverseViewMatrix = Matrix.Invert(renderView.View);
                var eye = inverseViewMatrix.Row4;
                cameraPos = new Vector3(eye.X, eye.Y, eye.Z);
            }

            var lightDirection = Vector3.TransformNormal(-Vector3.UnitZ, component.Sun.Entity.Transform.WorldMatrix);
            lightDirection.Normalize();

            var colorLight = component.Sun.Type as IColorLight;
            var sunColor = colorLight.ComputeColor(ColorSpace.Linear, component.Sun.Intensity);

            // Misc
            parameters.Set(AtmosphereCommonKeys.SunDirection.TryComposeWith(compositionName), -lightDirection);

            if (renderView != null)
            {
                parameters.Set(AtmosphereCommonKeys.InvViewProjectionMatrix.TryComposeWith(compositionName), Matrix.Invert(renderView.ViewProjection));
                parameters.Set(AtmosphereCommonKeys.Resolution.TryComposeWith(compositionName), renderView.ViewSize);
            }

            parameters.Set(AtmosphereCommonKeys.CameraPositionWS.TryComposeWith(compositionName), cameraPos);
            parameters.Set(AtmosphereCommonKeys.RayMarchMinMaxSPP.TryComposeWith(compositionName), new Vector2(4, 14));
            parameters.Set(AtmosphereCommonKeys.SunIlluminance.TryComposeWith(compositionName), new Vector3(sunColor.R, sunColor.G, sunColor.B));
            parameters.Set(AtmosphereCommonKeys.SunLuminanceFactor.TryComposeWith(compositionName), component.SunLuminanceFactor);
            parameters.Set(AtmosphereCommonKeys.RenderStage, 0);

            parameters.Set(AtmosphereRenderSkyRayMarchingKeys.CloudScale, component.CloudScale);
            parameters.Set(AtmosphereRenderSkyRayMarchingKeys.CloudSpeed, component.CloudSpeed);
            parameters.Set(AtmosphereRenderSkyRayMarchingKeys.Cloudiness, component.Cloudiness);

            if (renderContext != null)
                parameters.Set(AtmosphereRenderSkyRayMarchingKeys.Time, (float)renderContext.Time.Total.TotalSeconds);
        }

        static Vector4 CalculateResolutionVector(Texture texutre)
            => new Vector4(texutre.Width, texutre.Height, 1.0f / texutre.Width, 1.0f / texutre.Height);
    }
}
