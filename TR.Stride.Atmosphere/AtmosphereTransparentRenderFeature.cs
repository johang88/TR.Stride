using Stride.Core.Storage;
using Stride.Core.Mathematics;
using Stride.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Atmosphere
{
    /// <summary>
    /// Manages atmosphere parameters for transparent objects
    /// </summary>
    public class AtmosphereTransparentRenderFeature : SubRenderFeature
    {
        private AtmosphereRenderFeature _atmosphereRenderFeature;
        private bool _shouldRenderAtmosphere = false;

        private LogicalGroupReference _atmosphereLogicalGroupKey;

        public class RenderViewAtmosphereData
        {
            internal ObjectId ViewLayoutHash;
            internal ParameterCollectionLayout ViewParameterLayout;
            internal ParameterCollection ViewParameters = new ParameterCollection();
        }

        private readonly Dictionary<RenderView, RenderViewAtmosphereData> renderViewDatas = new Dictionary<RenderView, RenderViewAtmosphereData>();

        protected override void InitializeCore()
        {
            base.InitializeCore();

            _atmosphereLogicalGroupKey = ((RootEffectRenderFeature)RootRenderFeature).CreateViewLogicalGroup("Atmosphere");
        }

        public override void Collect()
        {
            base.Collect();

            foreach (var renderView in RenderSystem.Views)
            {
                if (!renderViewDatas.ContainsKey(renderView))
                {
                    var renderViewAtmosphereData = new RenderViewAtmosphereData();
                    renderViewDatas.Add(renderView, renderViewAtmosphereData);
                }
            }
        }

        public override void PrepareEffectPermutations(RenderDrawContext context)
        {
            base.PrepareEffectPermutations(context);

            // Try finding root atmosphere render feature
            foreach (var renderFeature in ((RootEffectRenderFeature)RootRenderFeature).RenderSystem.RenderFeatures)
            {
                if (renderFeature is AtmosphereRenderFeature atmosphereRenderFeature)
                {
                    _atmosphereRenderFeature = atmosphereRenderFeature;
                }
            }

            if (_atmosphereRenderFeature == null)
            {
                _shouldRenderAtmosphere = false;
                return;
            }

            _shouldRenderAtmosphere = _atmosphereRenderFeature.Atmosphere != null;

            var renderEffectKey = ((RootEffectRenderFeature)RootRenderFeature).RenderEffectKey;

            var renderEffects = RootRenderFeature.RenderData.GetData(renderEffectKey);
            int effectSlotCount = ((RootEffectRenderFeature)RootRenderFeature).EffectPermutationSlotCount;

            foreach (var renderObject in RootRenderFeature.RenderObjects)
            {
                var staticObjectNode = renderObject.StaticObjectNode;
                
                if (renderObject is not RenderMesh renderMesh)
                    continue;

                var material = renderMesh.MaterialPass;
                var shouldRenderAtmosphereForRenderObject = material.HasTransparency && _shouldRenderAtmosphere;

                for (int i = 0; i < effectSlotCount; ++i)
                {
                    var staticEffectObjectNode = staticObjectNode * effectSlotCount + i;
                    var renderEffect = renderEffects[staticEffectObjectNode];

                    // Skip effects not used during this frame
                    if (renderEffect == null || !renderEffect.IsUsedDuringThisFrame(RenderSystem))
                        continue;

                    renderEffect.EffectValidator.ValidateParameter(AtmosphereForwardShadingEffectParameters.RenderAerialPerspective, shouldRenderAtmosphereForRenderObject);
                }
            }
        }

        public override void Prepare(RenderDrawContext context)
        {
            base.Prepare(context);

            if (!_shouldRenderAtmosphere)
                return;

            foreach (var view in RenderSystem.Views)
            {
                var viewFeature = view.Features[RootRenderFeature.Index];

                RenderViewAtmosphereData renderViewData;
                if (!renderViewDatas.TryGetValue(view, out renderViewData) || viewFeature.Layouts.Count == 0)
                    continue;

                // Find a PerView layout from an effect in normal state
                ViewResourceGroupLayout firstViewLayout = null;
                foreach (var viewLayout in viewFeature.Layouts)
                {
                    // Only process view layouts in normal state
                    if (viewLayout.State != RenderEffectState.Normal)
                        continue;

                    var viewAtmosphere = viewLayout.GetLogicalGroup(_atmosphereLogicalGroupKey);
                    if (viewAtmosphere.Hash != ObjectId.Empty)
                    {
                        firstViewLayout = viewLayout;
                        break;
                    }
                }

                // Nothing found for this view (no effects in normal state)
                if (firstViewLayout == null)
                    continue;

                var viewParameterLayout = renderViewData.ViewParameterLayout;
                var viewParameters = renderViewData.ViewParameters;
                var firstViewAtmosphere = firstViewLayout.GetLogicalGroup(_atmosphereLogicalGroupKey);

                // Prepare layout (should be similar for all PerView)
                if (firstViewAtmosphere.Hash != renderViewData.ViewLayoutHash)
                {
                    renderViewData.ViewLayoutHash = firstViewAtmosphere.Hash;

                    // Generate layout
                    viewParameterLayout = renderViewData.ViewParameterLayout = new ParameterCollectionLayout();
                    viewParameterLayout.ProcessLogicalGroup(firstViewLayout, ref firstViewAtmosphere);

                    viewParameters.UpdateLayout(viewParameterLayout);
                }

                var component = _atmosphereRenderFeature.Atmosphere;
                var atmosphereCameraScatteringVolumeTexture = _atmosphereRenderFeature.AtmosphereCameraScatteringVolumeTexture;

                viewParameters.Set(AtmosphereForwardRenderKeys.BottomRadius, component.PlanetRadius);
                viewParameters.Set(AtmosphereForwardRenderKeys.ScaleToSkyUnit, component.StrideToAtmosphereUnitScale);
                viewParameters.Set(AtmosphereForwardRenderKeys.AerialPerspectiveSlicesAndDistancePerSlice,
                    new Vector4(
                        atmosphereCameraScatteringVolumeTexture.Depth, component.AtmosphereScatteringVolumeKmPerSlice,
                        1.0f / atmosphereCameraScatteringVolumeTexture.Depth, 1.0f / component.AtmosphereScatteringVolumeKmPerSlice
                    ));

                viewParameters.Set(AtmosphereForwardRenderKeys.AtmosphereCameraScatteringVolume, atmosphereCameraScatteringVolumeTexture);

                // Update PerView
                foreach (var viewLayout in viewFeature.Layouts)
                {
                    // Only process view layouts in normal state
                    if (viewLayout.State != RenderEffectState.Normal)
                        continue;

                    var viewAtmosphere = viewLayout.GetLogicalGroup(_atmosphereLogicalGroupKey);
                    if (viewAtmosphere.Hash == ObjectId.Empty)
                        continue;

                    if (viewAtmosphere.Hash != firstViewAtmosphere.Hash)
                        throw new InvalidOperationException("PerView Atmosphere layout differs between different RenderObject in the same RenderView");

                    var resourceGroup = viewLayout.Entries[view.Index].Resources;

                    // Update resources
                    resourceGroup.UpdateLogicalGroup(ref viewAtmosphere, viewParameters);
                }
            }
        }
    }
}
