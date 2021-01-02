using Stride.Core.Collections;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;
using Stride.Rendering.Lights;
using Stride.Rendering.Shadows;
using Stride.Shaders;
using System;
using System.Collections.Generic;
using System.Text;

namespace TR.Stride.Atmosphere
{
    /// <summary>
    /// Used to inject atmosphere parameters for a direcitonal light
    /// </summary>
    public class AtmosphereLightDirectionalGroupRenderer : LightGroupRendererShadow
    {
        public override Type[] LightTypes { get; } = { typeof(AtmosphereLightDirectional) };

        public override void Initialize(RenderContext context)
        {
        }

        public override LightShaderGroupDynamic CreateLightShaderGroup(RenderDrawContext context, ILightShadowMapShaderGroupData shadowShaderGroupData)
            => new DirectionalLightShaderGroup(context.RenderContext, shadowShaderGroupData);

        private class DirectionalLightShaderGroup : LightShaderGroupDynamic
        {
            private ValueParameterKey<int> _countKey;
            private ValueParameterKey<DirectionalLightData> _lightsKey;
            private FastListStruct<DirectionalLightData> _lightsData = new FastListStruct<DirectionalLightData>(8);
            private string _compositionName;

            public DirectionalLightShaderGroup(RenderContext renderContext, ILightShadowMapShaderGroupData shadowGroupData)
                : base(renderContext, shadowGroupData)
            {
            }

            public override void UpdateLayout(string compositionName)
            {
                base.UpdateLayout(compositionName);

                _countKey = DirectLightGroupPerViewKeys.LightCount.ComposeWith(compositionName);
                _lightsKey = AtmosphereLightDirectionalGroupKeys.Lights.ComposeWith(compositionName);
                _compositionName = compositionName;
            }

            protected override void UpdateLightCount()
            {
                base.UpdateLightCount();

                var mixin = new ShaderMixinSource();
                mixin.Mixins.Add(new ShaderClassSource("AtmosphereLightDirectionalGroup", LightCurrentCount, "PerView.Lighting"));
                ShadowGroup?.ApplyShader(mixin);

                ShaderSource = mixin;
            }

            private void SetAtmosphereParameters(RenderDrawContext context, int viewIndex, ParameterCollection parameters)
            {
                var lightRange = lightRanges[viewIndex];
                if (lightRange.Start == lightRange.End)
                    return;

                if (!(lights[lightRange.Start].Light.Type is AtmosphereLightDirectional light))
                    return;

                // Fetch compositor and find atmosphere render feature
                var sceneSystem = context.RenderContext.Services.GetService<SceneSystem>();
                var graphicsCompositor = sceneSystem.GraphicsCompositor;

                AtmosphereRenderFeature atmosphereRenderFeature = null;
                foreach (var feature in graphicsCompositor.RenderFeatures)
                {
                    if (feature is AtmosphereRenderFeature atmoshpere)
                        atmosphereRenderFeature = atmoshpere;
                }

                if (atmosphereRenderFeature == null || atmosphereRenderFeature.TransmittanceLutTexture == null)
                    return;

                atmosphereRenderFeature.SetParameters(null, light.Atmosphere, parameters, null);
                parameters.Set(AtmosphereParametersBaseKeys.TransmittanceLutTexture, atmosphereRenderFeature.TransmittanceLutTexture);
            }

            public override void ApplyViewParameters(RenderDrawContext context, int viewIndex, ParameterCollection parameters)
            {
                currentLights.Clear();
                var lightRange = lightRanges[viewIndex];
                for (int i = lightRange.Start; i < lightRange.End; ++i)
                    currentLights.Add(lights[i]);

                base.ApplyViewParameters(context, viewIndex, parameters);

                foreach (var lightEntry in currentLights)
                {
                    var light = lightEntry.Light;
                    _lightsData.Add(new DirectionalLightData
                    {
                        DirectionWS = light.Direction,
                        Color = light.Color,
                    });
                }

                parameters.Set(_countKey, _lightsData.Count);
                parameters.Set(_lightsKey, _lightsData.Count, ref _lightsData.Items[0]);
                _lightsData.Clear();

                SetAtmosphereParameters(context, viewIndex, parameters);
            }
        }
    }
}
