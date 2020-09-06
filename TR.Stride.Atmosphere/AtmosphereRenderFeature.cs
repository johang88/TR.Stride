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
using System.Runtime.CompilerServices;

namespace TR.Stride.Atmosphere
{
    public class AtmosphereRenderFeature : RootRenderFeature
    {
        // TODO: Implement RootEffectRenderFeature so we can get depth texture automatically assigned to us
        // OR maybe we could render last in opaque pass, but will still have issues with depth texture as some of these methods are only supposed 
        // to be called once per frame :/
        // Each texture generation can be done as a sub render feature, this will keep things nice and clean
        // we could probably use ImageEffect or some similar base class, should remove some of the pipeline management etc

        public TextureSettings2d TransmittanceLutSettings = new TextureSettings2d(256, 64, PixelFormat.R16G16B16A16_Float);
        public TextureSettingsSquare MultiScatteringTextureSettings = new TextureSettingsSquare(32, PixelFormat.R16G16B16A16_Float);
        public TextureSettings2d SkyViewLutSettings = new TextureSettings2d(192, 108, PixelFormat.R11G11B10_Float);
        public TextureSettingsVolume AtmosphereCameraScatteringVolumeSettings = new TextureSettingsVolume(32, 32, PixelFormat.R16G16B16A16_Float);

        public override Type SupportedRenderObjectType => typeof(AtmosphereRenderObject);

        private MutablePipelineState _renderSkyRayMarchingPipelineState = null;
        private DynamicEffectInstance _renderSkyRayMarchingEffect = null;

        private MutablePipelineState _renderTransmittanceLutPipelineState = null;
        private DynamicEffectInstance _renderTransmittanceLutEffect = null;

        private MutablePipelineState _renderSkyViewLutPipelineState = null;
        private DynamicEffectInstance _renderSkyViewLutEffect = null;

        private MutablePipelineState _renderAtmosphereScatteringVolumePipelineState = null;
        private DynamicEffectInstance _renderAtmosphereScatteringVolumeEffect = null;

        private ComputeEffectShader _renderNewMultiScattEffect = null;

        private Texture _transmittanceLutTexture = null;
        private Texture _multiScatteringTexture = null;
        private Texture _skyViewLutTexture = null;
        private Texture _atmosphereCameraScatteringVolumeTexture = null;

        private Texture depthStencilROCached;

        private SpriteBatch _spriteBatch;

        protected override void InitializeCore()
        {
            base.InitializeCore();

            // Sky view lut
            _skyViewLutTexture = Texture.New2D(Context.GraphicsDevice, SkyViewLutSettings.Width, SkyViewLutSettings.Height, SkyViewLutSettings.Format, TextureFlags.RenderTarget | TextureFlags.ShaderResource);

            _renderSkyViewLutEffect = new DynamicEffectInstance("AtmosphereRenderSkyViewLutEffect");
            _renderSkyViewLutEffect.Initialize(Context.Services);

            _renderSkyViewLutPipelineState = new MutablePipelineState(Context.GraphicsDevice);
            _renderSkyViewLutPipelineState.State.SetDefaults();
            _renderSkyViewLutPipelineState.State.PrimitiveType = PrimitiveType.TriangleList;

            // Sky atmosphere volume
            _atmosphereCameraScatteringVolumeTexture = Texture.New3D(Context.GraphicsDevice, AtmosphereCameraScatteringVolumeSettings.Size, AtmosphereCameraScatteringVolumeSettings.Size, AtmosphereCameraScatteringVolumeSettings.Slices, AtmosphereCameraScatteringVolumeSettings.Format, TextureFlags.RenderTarget | TextureFlags.ShaderResource);

            _renderAtmosphereScatteringVolumeEffect = new DynamicEffectInstance("AtmosphereRenderScatteringCameraVolumeEffect");
            _renderAtmosphereScatteringVolumeEffect.Initialize(Context.Services);

            _renderAtmosphereScatteringVolumePipelineState = new MutablePipelineState(Context.GraphicsDevice);
            _renderAtmosphereScatteringVolumePipelineState.State.SetDefaults();
            _renderAtmosphereScatteringVolumePipelineState.State.PrimitiveType = PrimitiveType.TriangleList;

            // Multiscattering texture
            _renderNewMultiScattEffect = new ComputeEffectShader(Context) { ShaderSourceName = "AtmosphereNewMultiScattEffect" };
            _multiScatteringTexture = Texture.New2D(Context.GraphicsDevice, MultiScatteringTextureSettings.Size, MultiScatteringTextureSettings.Size, MultiScatteringTextureSettings.Format, TextureFlags.UnorderedAccess | TextureFlags.ShaderResource);

            // Transmittance lut
            _transmittanceLutTexture = Texture.New2D(Context.GraphicsDevice, TransmittanceLutSettings.Width, TransmittanceLutSettings.Height, TransmittanceLutSettings.Format, TextureFlags.UnorderedAccess | TextureFlags.RenderTarget | TextureFlags.ShaderResource);

            _renderTransmittanceLutEffect = new DynamicEffectInstance("AtmosphereRenderTransmittanceLutEffect");
            _renderTransmittanceLutEffect.Initialize(Context.Services);

            _renderTransmittanceLutPipelineState = new MutablePipelineState(Context.GraphicsDevice);
            _renderTransmittanceLutPipelineState.State.SetDefaults();
            _renderTransmittanceLutPipelineState.State.PrimitiveType = PrimitiveType.TriangleList;

            // Sky ray march effect
            _renderSkyRayMarchingEffect = new DynamicEffectInstance("AtmosphereRenderSkyRayMarchingEffect");
            _renderSkyRayMarchingEffect.Initialize(Context.Services);

            _renderSkyRayMarchingPipelineState = new MutablePipelineState(Context.GraphicsDevice);

            var state = _renderSkyRayMarchingPipelineState.State;

            state.SetDefaults();

            state.DepthStencilState = new DepthStencilStateDescription(false, false);

            state.BlendState.AlphaToCoverageEnable = false;
            state.BlendState.IndependentBlendEnable = false;

            ref var blendState0 = ref state.BlendState.RenderTarget0;

            blendState0.BlendEnable = true;

            blendState0.ColorSourceBlend = Blend.One;
            blendState0.ColorDestinationBlend = Blend.InverseSourceAlpha;
            blendState0.ColorBlendFunction = BlendFunction.Add;

            blendState0.AlphaSourceBlend = Blend.Zero;
            blendState0.AlphaDestinationBlend = Blend.One;
            blendState0.AlphaBlendFunction = BlendFunction.Add;

            state.PrimitiveType = PrimitiveType.TriangleList;

            // Debug utils
            _spriteBatch = new SpriteBatch(Context.GraphicsDevice);
        }

        public override void Unload()
        {
            _multiScatteringTexture?.Dispose();
            _multiScatteringTexture = null;

            _transmittanceLutTexture?.Dispose();
            _transmittanceLutTexture = null;

            _skyViewLutTexture?.Dispose();
            _skyViewLutTexture = null;

            _atmosphereCameraScatteringVolumeTexture?.Dispose();
            _atmosphereCameraScatteringVolumeTexture = null;

            base.Unload();
        }

        public override void Draw(RenderDrawContext context, RenderView renderView, RenderViewStage renderViewStage, int startIndex, int endIndex)
        {
            base.Draw(context, renderView, renderViewStage, startIndex, endIndex);

            var graphicsDevice = context.GraphicsDevice;
            var graphicsContext = context.GraphicsContext;
            var commandList = context.GraphicsContext.CommandList;

            var depthStencilSRV = GetDepthSRV(context);

            using (context.QueryManager.BeginProfile(Color.Blue, ProfilingKeys.Atmosphere))
            {
                for (int index = startIndex; index < endIndex; index++)
                {
                    var renderNodeReference = renderViewStage.SortedRenderNodes[index].RenderNode;
                    var renderNode = GetRenderNode(renderNodeReference);
                    var renderObject = (AtmosphereRenderObject)renderNode.RenderObject;

                    if (renderObject.Component.Sun == null)
                        continue;

                    if (!(renderObject.Component.Sun.Type is LightDirectional light))
                        continue;

                    RenderTransmittanceLut(context, renderView, renderObject.Component);
                    RenderNewMultiScattering(context, renderView, renderObject.Component);
                    RenderSkyViewLut(context, renderView, renderObject.Component);
                    RenderAtmosphereCameraScatteringVolume(context, renderView, renderObject.Component);

                    using (context.QueryManager.BeginProfile(Color4.Black, ProfilingKeys.RayMarching))
                    {
                        commandList.ResourceBarrierTransition(_multiScatteringTexture, GraphicsResourceState.PixelShaderResource);
                        commandList.ResourceBarrierTransition(_transmittanceLutTexture, GraphicsResourceState.PixelShaderResource);
                        commandList.ResourceBarrierTransition(_skyViewLutTexture, GraphicsResourceState.PixelShaderResource);
                        commandList.ResourceBarrierTransition(_atmosphereCameraScatteringVolumeTexture, GraphicsResourceState.PixelShaderResource);

                        // Refresh shader, might have changed during runtime
                        _renderSkyRayMarchingEffect.UpdateEffect(graphicsDevice);

                        _renderSkyRayMarchingPipelineState.State.RootSignature = _renderSkyRayMarchingEffect.RootSignature;
                        _renderSkyRayMarchingPipelineState.State.EffectBytecode = _renderSkyRayMarchingEffect.Effect.Bytecode;

                        _renderSkyRayMarchingPipelineState.State.Output.CaptureState(commandList);
                        _renderSkyRayMarchingPipelineState.Update();

                        commandList.SetPipelineState(_renderSkyRayMarchingPipelineState.CurrentState);

                        var parameters = _renderSkyRayMarchingEffect.Parameters;
                        SetParameters(renderView, renderObject.Component, parameters);

                        parameters.Set(AtmosphereRenderSkyCommonKeys.MultiScatTexture, _multiScatteringTexture);
                        parameters.Set(AtmosphereRenderSkyCommonKeys.TransmittanceLutTexture, _transmittanceLutTexture);
                        parameters.Set(AtmosphereRenderSkyCommonKeys.SkyViewLutTexture, _skyViewLutTexture);
                        parameters.Set(AtmosphereRenderSkyCommonKeys.AtmosphereCameraScatteringVolume, _atmosphereCameraScatteringVolumeTexture);
                        parameters.Set(AtmosphereRenderSkyCommonKeys.ViewDepthTexture, depthStencilSRV);

                        _renderSkyRayMarchingEffect.Apply(graphicsContext);

                        commandList.Draw(3, 0);
                    }
                }

                // Debug Draw
                _spriteBatch.Begin(graphicsContext, SpriteSortMode.Immediate);

                var y = renderView.ViewSize.Y - 20 - _transmittanceLutTexture.Height;
                _spriteBatch.Draw(_transmittanceLutTexture, new Vector2(20, y));

                y -= 20 + _multiScatteringTexture.Height;
                _spriteBatch.Draw(_multiScatteringTexture, new Vector2(20, y));

                y -= 20 + _skyViewLutTexture.Height;
                _spriteBatch.Draw(_skyViewLutTexture, new Vector2(20, y));

                _spriteBatch.End();
            }

            context.Resolver.ReleaseDepthStenctilAsShaderResource(depthStencilSRV);
        }

        private void RenderNewMultiScattering(RenderDrawContext context, RenderView renderView, AtmosphereComponent component)
        {
            using (context.QueryManager.BeginProfile(Color4.Black, ProfilingKeys.MultiScatteringTexture))
            {
                var commandList = context.GraphicsContext.CommandList;

                commandList.ResourceBarrierTransition(_multiScatteringTexture, GraphicsResourceState.UnorderedAccess);
                commandList.ResourceBarrierTransition(_transmittanceLutTexture, GraphicsResourceState.PixelShaderResource);

                var parameters = _renderNewMultiScattEffect.Parameters;
                SetParameters(renderView, component, parameters);

                parameters.Set(AtmosphereNewMultiScattCSKeys.OutputTexture, _multiScatteringTexture);
                parameters.Set(AtmosphereParametersBaseKeys.SunIlluminance, new Vector3(1, 1, 1));
                parameters.Set(AtmosphereRenderSkyCommonKeys.TransmittanceLutTexture, _transmittanceLutTexture);

                _renderNewMultiScattEffect.ThreadGroupCounts = new Int3(_multiScatteringTexture.Width, _multiScatteringTexture.Height, 1);
                _renderNewMultiScattEffect.ThreadNumbers = new Int3(1, 1, 64);

                _renderNewMultiScattEffect.Draw(context);
            }
        }

        private void RenderAtmosphereCameraScatteringVolume(RenderDrawContext context, RenderView renderView, AtmosphereComponent component)
        {
            using (context.QueryManager.BeginProfile(Color4.Black, ProfilingKeys.ScatteringCameraVolume))
            {
                var graphicsDevice = context.GraphicsDevice;
                var graphicsContext = context.GraphicsContext;
                var commandList = context.GraphicsContext.CommandList;

                _renderAtmosphereScatteringVolumeEffect.UpdateEffect(graphicsDevice);

                _renderAtmosphereScatteringVolumePipelineState.State.RootSignature = _renderAtmosphereScatteringVolumeEffect.RootSignature;
                _renderAtmosphereScatteringVolumePipelineState.State.EffectBytecode = _renderAtmosphereScatteringVolumeEffect.Effect.Bytecode;

                using (context.PushRenderTargetsAndRestore())
                {
                    commandList.ResourceBarrierTransition(_atmosphereCameraScatteringVolumeTexture, GraphicsResourceState.RenderTarget);
                    commandList.ResourceBarrierTransition(_transmittanceLutTexture, GraphicsResourceState.PixelShaderResource);
                    commandList.ResourceBarrierTransition(_multiScatteringTexture, GraphicsResourceState.PixelShaderResource);
                    commandList.ResourceBarrierTransition(_skyViewLutTexture, GraphicsResourceState.PixelShaderResource);

                    commandList.SetRenderTarget(null, _atmosphereCameraScatteringVolumeTexture);

                    var oldViewport = commandList.Viewport;
                    commandList.SetViewport(new Viewport(0, 0, _atmosphereCameraScatteringVolumeTexture.Width, _atmosphereCameraScatteringVolumeTexture.Height));

                    _renderAtmosphereScatteringVolumePipelineState.State.Output.CaptureState(commandList);
                    _renderAtmosphereScatteringVolumePipelineState.Update();

                    commandList.SetPipelineState(_renderAtmosphereScatteringVolumePipelineState.CurrentState);

                    var parameters = _renderAtmosphereScatteringVolumeEffect.Parameters;

                    SetParameters(renderView, component, parameters);

                    parameters.Set(AtmosphereCommonKeys.Resolution, new Vector2(_atmosphereCameraScatteringVolumeTexture.Width, _atmosphereCameraScatteringVolumeTexture.Height));

                    parameters.Set(AtmosphereRenderSkyCommonKeys.TransmittanceLutTexture, _transmittanceLutTexture);
                    parameters.Set(AtmosphereRenderSkyCommonKeys.MultiScatTexture, _multiScatteringTexture);
                    parameters.Set(AtmosphereRenderSkyCommonKeys.SkyViewLutTexture  , _skyViewLutTexture);

                    _renderAtmosphereScatteringVolumeEffect.Apply(graphicsContext);

                    commandList.DrawInstanced(3, _atmosphereCameraScatteringVolumeTexture.Depth);

                    commandList.SetViewport(oldViewport);
                }
            }
        }

        private void RenderSkyViewLut(RenderDrawContext context, RenderView renderView, AtmosphereComponent component)
        {
            using (context.QueryManager.BeginProfile(Color4.Black, ProfilingKeys.SkyViewLut))
            {
                var graphicsDevice = context.GraphicsDevice;
                var graphicsContext = context.GraphicsContext;
                var commandList = context.GraphicsContext.CommandList;

                _renderSkyViewLutEffect.UpdateEffect(graphicsDevice);

                _renderSkyViewLutPipelineState.State.RootSignature = _renderSkyViewLutEffect.RootSignature;
                _renderSkyViewLutPipelineState.State.EffectBytecode = _renderSkyViewLutEffect.Effect.Bytecode;

                using (context.PushRenderTargetsAndRestore())
                {
                    commandList.ResourceBarrierTransition(_skyViewLutTexture, GraphicsResourceState.RenderTarget);
                    commandList.ResourceBarrierTransition(_transmittanceLutTexture, GraphicsResourceState.PixelShaderResource);
                    commandList.ResourceBarrierTransition(_multiScatteringTexture, GraphicsResourceState.PixelShaderResource);

                    commandList.SetRenderTarget(null, _skyViewLutTexture);

                    var oldViewport = commandList.Viewport;
                    commandList.SetViewport(new Viewport(0, 0, _skyViewLutTexture.Width, _skyViewLutTexture.Height));

                    _renderSkyViewLutPipelineState.State.Output.CaptureState(commandList);
                    _renderSkyViewLutPipelineState.Update();

                    commandList.SetPipelineState(_renderSkyViewLutPipelineState.CurrentState);

                    var parameters = _renderSkyViewLutEffect.Parameters;

                    SetParameters(renderView, component, parameters);
                    
                    parameters.Set(AtmosphereCommonKeys.Resolution, new Vector2(_skyViewLutTexture.Width, _skyViewLutTexture.Height));

                    parameters.Set(AtmosphereRenderSkyCommonKeys.TransmittanceLutTexture, _transmittanceLutTexture);
                    parameters.Set(AtmosphereRenderSkyCommonKeys.MultiScatTexture, _multiScatteringTexture);

                    _renderSkyViewLutEffect.Apply(graphicsContext);

                    commandList.Draw(3, 0);

                    commandList.SetViewport(oldViewport);
                }
            }
        }

        private void RenderTransmittanceLut(RenderDrawContext context, RenderView renderView, AtmosphereComponent component)
        {
            using (context.QueryManager.BeginProfile(Color4.Black, ProfilingKeys.TransmittanceLut))
            {
                var graphicsDevice = context.GraphicsDevice;
                var graphicsContext = context.GraphicsContext;
                var commandList = context.GraphicsContext.CommandList;

                // Refresh shader, might have changed during runtime
                _renderTransmittanceLutEffect.UpdateEffect(graphicsDevice);

                _renderTransmittanceLutPipelineState.State.RootSignature = _renderTransmittanceLutEffect.RootSignature;
                _renderTransmittanceLutPipelineState.State.EffectBytecode = _renderTransmittanceLutEffect.Effect.Bytecode;

                using (context.PushRenderTargetsAndRestore())
                {
                    commandList.ResourceBarrierTransition(_transmittanceLutTexture, GraphicsResourceState.RenderTarget);
                    commandList.SetRenderTarget(null, _transmittanceLutTexture);

                    var oldViewport = commandList.Viewport;
                    commandList.SetViewport(new Viewport(0, 0, TransmittanceLutSettings.Width, TransmittanceLutSettings.Height));

                    _renderTransmittanceLutPipelineState.State.Output.CaptureState(commandList);
                    _renderTransmittanceLutPipelineState.Update();

                    commandList.SetPipelineState(_renderTransmittanceLutPipelineState.CurrentState);

                    var parameters = _renderTransmittanceLutEffect.Parameters;

                    SetParameters(renderView, component, parameters);
                    parameters.Set(AtmosphereCommonKeys.Resolution, new Vector2(TransmittanceLutSettings.Width, TransmittanceLutSettings.Height));

                    _renderTransmittanceLutEffect.Apply(graphicsContext);

                    commandList.Draw(3, 0);

                    commandList.SetViewport(oldViewport);
                }
            }
        }

        private Texture GetDepthSRV(RenderDrawContext context)
        {
            // We require depth not to be exposed to transparent render targets as this fails otherwise
            // TODO: Remove once we implement RootEffectRenderFeature
            var depthStencil = context.CommandList.DepthStencilBuffer;
            var depthStencilSRV = context.Resolver.ResolveDepthStencil(context.CommandList.DepthStencilBuffer);

            context.CommandList.SetRenderTargets(null, context.CommandList.RenderTargetCount, context.CommandList.RenderTargets);

            depthStencilROCached = context.Resolver.GetDepthStencilAsRenderTarget(depthStencil, depthStencilROCached);
            context.CommandList.SetRenderTargets(depthStencilROCached, context.CommandList.RenderTargetCount, context.CommandList.RenderTargets);

            return depthStencilSRV;
        }

        private void SetParameters(RenderView renderView, AtmosphereComponent component, ParameterCollection parameters)
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

            Matrix inverseViewMatrix = Matrix.Invert(renderView.View);
            Vector4 eye = inverseViewMatrix.Row4;
            Vector3 cameraPos = new Vector3(eye.X, eye.Y, eye.Z);

            var lightDirection = Vector3.TransformNormal(-Vector3.UnitZ, component.Sun.Entity.Transform.WorldMatrix);
            lightDirection.Normalize();

            var sunColor = component.Sun.GetColor() * component.Sun.Intensity;

            // Atmosphere parameters
            parameters.Set(AtmosphereParametersBaseKeys.RayleighDensityExpScale, -1.0f / component.RayleighScaleHeight);
            parameters.Set(AtmosphereParametersBaseKeys.MieDensityExpScale, -1.0f / component.MieScaleHeight);

            parameters.Set(AtmosphereParametersBaseKeys.AbsorptionExtinction, absorptionExtinction);
            parameters.Set(AtmosphereParametersBaseKeys.AbsorptionDensity0LayerWidth, component.AbsorptionDensity0LayerWidth);
            parameters.Set(AtmosphereParametersBaseKeys.AbsorptionDensity0ConstantTerm, component.AbsorptionDensity0ConstantTerm);
            parameters.Set(AtmosphereParametersBaseKeys.AbsorptionDensity0LinearTerm, component.AbsorptionDensity0LinearTerm);
            parameters.Set(AtmosphereParametersBaseKeys.AbsorptionDensity1ConstantTerm, component.AbsorptionDensity1ConstantTerm);
            parameters.Set(AtmosphereParametersBaseKeys.AbsorptionDensity1LinearTerm, component.AbsorptionDensity1LinearTerm);
            
            parameters.Set(AtmosphereParametersBaseKeys.RayleighScattering, rayleighScattering);

            parameters.Set(AtmosphereParametersBaseKeys.MiePhaseG, component.MiePhase);
            parameters.Set(AtmosphereParametersBaseKeys.MieScattering, mieScattering);
            parameters.Set(AtmosphereParametersBaseKeys.MieAbsorption, mieAbsoprtion);
            parameters.Set(AtmosphereParametersBaseKeys.MieExtinction, mieExctinction);

            parameters.Set(AtmosphereParametersBaseKeys.GroundAlbedo, component.GroundAlbedo.ToVector3());
            parameters.Set(AtmosphereParametersBaseKeys.BottomRadius, component.PlanetRadius);
            parameters.Set(AtmosphereParametersBaseKeys.TopRadius, component.PlanetRadius + component.AtmosphereHeight);

            parameters.Set(AtmosphereParametersBaseKeys.AerialPespectiveViewDistanceScale, component.AerialPerspectiveDistanceScale);

            // Lut settings
            parameters.Set(AtmosphereParametersBaseKeys.MultipleScatteringFactor, component.MultipleScatteringFactor);

            parameters.Set(AtmosphereParametersBaseKeys.MultiScatteringLutResolution, CalculateResolutionVector(_multiScatteringTexture));
            parameters.Set(AtmosphereParametersBaseKeys.SkyViewLutResolution, CalculateResolutionVector(_skyViewLutTexture));
            parameters.Set(AtmosphereParametersBaseKeys.TransmittanceLutResolution, CalculateResolutionVector(_transmittanceLutTexture));

            parameters.Set(AtmosphereParametersBaseKeys.AerialPerspectiveSlicesAndDistancePerSlice,
                new Vector4(
                    _atmosphereCameraScatteringVolumeTexture.Depth, component.AtmosphereScatteringVolumeKmPerSlice,
                    1.0f / _atmosphereCameraScatteringVolumeTexture.Depth, 1.0f / component.AtmosphereScatteringVolumeKmPerSlice
                    ));

            // Misc
            parameters.Set(AtmosphereParametersBaseKeys.SunDirection, -lightDirection);
            
            parameters.Set(AtmosphereCommonKeys.InvViewProjectionMatrix, Matrix.Invert(renderView.ViewProjection));
            parameters.Set(AtmosphereCommonKeys.CameraPositionWS, cameraPos);
            parameters.Set(AtmosphereCommonKeys.Resolution, renderView.ViewSize);
            parameters.Set(AtmosphereCommonKeys.RayMarchMinMaxSPP, new Vector2(4, 14));
            parameters.Set(AtmosphereCommonKeys.ScaleToSkyUnit, component.StrideToAtmosphereUnitScale);
            parameters.Set(AtmosphereParametersBaseKeys.SunIlluminance, new Vector3(sunColor.R, sunColor.G, sunColor.B));
            parameters.Set(AtmosphereParametersBaseKeys.SunLuminanceFactor, component.SunLuminanceFactor);
        }

        static Vector4 CalculateResolutionVector(Texture texutre)
            => new Vector4(texutre.Width, texutre.Height, 1.0f / texutre.Width, 1.0f / texutre.Height);
    }
}
