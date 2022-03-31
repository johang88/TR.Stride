using Stride.Core.Annotations;
using Stride.Core.Collections;
using Stride.Core.Diagnostics;
using Stride.Core.Mathematics;
using Stride.Core.Threading;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders.Compiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TR.Stride.Terrain
{
    public class TerrainVegetationProcessor : EntityProcessor<TerrainVegetationComponent, TerrainVegetationRenderData>, IEntityComponentRenderProcessor
    {
        public VisibilityGroup VisibilityGroup { get; set; }

        private FastList<TerrainVegetationPage> _activesPages = new FastList<TerrainVegetationPage>();

        public int ActivePageCount { get; set; }
        public int ActiveInstanceCount { get; set; }
        public bool CullPages { get; set; } = false;
        public bool CullInstances { get; set; } = false;

        public TerrainVegetationProcessor()
            : base(typeof(ModelComponent), typeof(InstancingComponent))
        {
            // Run before the instancing processor
            Order = -101;
        }

        protected override void OnEntityComponentRemoved(Entity entity, [NotNull] TerrainVegetationComponent component, [NotNull] TerrainVegetationRenderData data)
        {
            base.OnEntityComponentRemoved(entity, component, data);
        }

        protected override TerrainVegetationRenderData GenerateComponentData([NotNull] Entity entity, [NotNull] TerrainVegetationComponent component)
        {
            return new TerrainVegetationRenderData
            {
            };
        }

        public override void Draw(RenderContext context)
        {
            base.Draw(context);

            ActivePageCount = 0;
            ActiveInstanceCount = 0;

            var camera = Services.GetService<SceneSystem>().TryGetMainCamera();
            if (camera == null)
                return;

            foreach (var pair in ComponentDatas)
            {
                var component = pair.Key;
                var renderData = pair.Value;

                ProcessComponent(component, renderData, camera);
            }

            //Dispatcher.ForEach(ComponentDatas, (pair) =>
            //{
            //    var component = pair.Key;
            //    var renderData = pair.Value;

            //    ProcessComponent(component, renderData, camera);
            //});
        }

        private void ProcessComponent(TerrainVegetationComponent component, TerrainVegetationRenderData renderData, CameraComponent camera)
        {
            if (component.Terrain == null || component.Terrain.Heightmap == null || component.Density <= 0.0f)
            {
                return;
            }

            var instancingComponent = component.Entity.Get<InstancingComponent>();
            if (!(instancingComponent.Type is InstancingUserArray instancingUserArray))
                return;

            UpdatePages(component.Terrain, component, renderData);
            CollectVisiblePages(component.Terrain, renderData, component, camera);

            instancingUserArray.UpdateWorldMatrices(renderData.TransformData, renderData.Count);
            ActiveInstanceCount += renderData.Count;
        }

        /// <summary>
        /// Update and recreate a page if necessary
        /// </summary>
        private void UpdatePages(TerrainComponent terrain, TerrainVegetationComponent component, TerrainVegetationRenderData renderData)
        {
            if (renderData.Pages != null && !component.IsDirty)
                return;

            // Calculate terrain center offset
            var terrainOffset = terrain.Size / 2.0f;

            // Create vegetation pages
            var pagesPerRow = (int)terrain.Size / component.PageSize;

            renderData.Pages = new TerrainVegetationPage[pagesPerRow * pagesPerRow];

            for (var pz = 0; pz < pagesPerRow; pz++)
            {
                for (var px = 0; px < pagesPerRow; px++)
                {
                    var radius = component.PageSize * 0.5f;

                    var pagePosition = new Vector3(px * component.PageSize - terrainOffset, 0, pz * component.PageSize - terrainOffset);

                    var page = new TerrainVegetationPage();
                    renderData.Pages[pz * pagesPerRow + px] = page;
                    page.WorldPosition = pagePosition + new Vector3(radius, 0, radius);
                    page.PagePosition = new Int2(px, pz);
                }
            }

            component.IsDirty = false;
        }

        private void LoadPage(TerrainComponent terrain, TerrainVegetationComponent component, TerrainVegetationPage page)
        {
            page.Instances = new FastList<Matrix>();

            // Calculate terrain center offset
            var terrainOffset = terrain.Size / 2.0f;

            var pagePosition = new Vector3(page.PagePosition.X * component.PageSize - terrainOffset, 0, page.PagePosition.Y * component.PageSize - terrainOffset);

            var instancesPerRow = (int)(component.PageSize * component.Density);
            var distancePerInstance = component.PageSize / (float)instancesPerRow;
            var scaleRange = component.MaxScale - component.MinScale;

            var seed = (page.PagePosition.X << 16 | page.PagePosition.Y) + component.Seed;
            var rng = new RandomSeed((uint)seed);
            uint index = 0;

            for (var iz = 0; iz < instancesPerRow; iz++)
            {
                for (var ix = 0; ix < instancesPerRow; ix++)
                {
                    var position = pagePosition;

                    position.X += ix * distancePerInstance;
                    position.Z += iz * distancePerInstance;

                    position.X += rng.GetFloat(index++) * distancePerInstance * 2.0f - distancePerInstance;
                    position.Z += rng.GetFloat(index++) * distancePerInstance * 2.0f - distancePerInstance;

                    position.Y = terrain.GetHeightAt(position.X, position.Z);

                    if (position.Y < component.MinHeight || position.Y > component.MaxHeight)
                        continue;

                    var normal = terrain.GetNormalAt(position.X, position.Z);
                    var slope = 1.0f - Math.Abs(normal.Y);
                    if (slope < component.MinSlope || slope > component.MaxSlope)
                        continue;

                    var scale = rng.GetFloat(index++) * scaleRange + component.MinScale;

                    var rotation = Quaternion.RotationAxis(Vector3.UnitY, rng.GetFloat(index++) * MathUtil.TwoPi);
                    if (component.RotateToTerrainNormal)
                        rotation *= Quaternion.BetweenDirections(Vector3.UnitY, normal);

                    var scaling = new Vector3(scale);

                    Matrix.Transformation(ref scaling, ref rotation, ref position, out var transformation);

                    page.Instances.Add(transformation);
                }
            }
        }

        private static int CrossProduct(Int2 v1, Int2 v2)
            => v1.X * v2.Y - v1.Y * v2.X;

        private void CollectVisiblePages(TerrainComponent terrain, TerrainVegetationRenderData renderData, TerrainVegetationComponent component, CameraComponent camera)
        {
            renderData.Count = 0;

            if (camera == null || renderData.Pages == null)
                return;

            var cameraPosition = camera.GetWorldPosition();
            cameraPosition.Y = 0.0f; // Only cull in xz plane

            var cameraFrustum = camera.Frustum;

            var maxPageDistance = component.ViewDistance + component.PageSize;

            var bounds = new BoundingBoxExt
            {
                Extent = new Vector3(component.PageSize * 0.5f, camera.FarClipPlane - camera.NearClipPlane, component.PageSize * 0.5f)
            };

            _activesPages.Clear();
            var maxInstanceCount = 0;
            for (var i = 0; i < renderData.Pages.Length; i++)
            {
                var page = renderData.Pages[i];

                var distance = (cameraPosition - page.WorldPosition).Length();
                if (distance < maxPageDistance)
                {
                    bounds.Center = page.WorldPosition;

                    if (!CullPages || VisibilityGroup.FrustumContainsBox(ref cameraFrustum, ref bounds, true))
                    {
                        _activesPages.Add(page);
                    }
                }
            }

            foreach (var page in _activesPages)
            {
                if (page.Instances == null)
                    LoadPage(terrain, component, page);

                maxInstanceCount += page.Instances.Count;
            }

            ActivePageCount += _activesPages.Count;

            // Reset camera position for individual instance culling
            cameraPosition = camera.GetWorldPosition();

            float maxDistance = component.ViewDistance;
            float minDistance = maxDistance * 0.8f;
            float distanceRange = maxDistance - minDistance;

            // Make sure we have enough capacity for all instances
            if (maxInstanceCount > renderData.TransformData.Length)
            {
                renderData.TransformData = new Matrix[MathUtil.NextPowerOfTwo(maxInstanceCount)];
            }

            var modelComponent = component.Entity.Get<ModelComponent>();
            var modelBounds = new BoundingBoxExt(modelComponent.BoundingBox);

            var maxInstanceDistanceSquared = component.ViewDistance * component.ViewDistance;
            Dispatcher.ForEach(_activesPages, page =>
            {
                
                for (var p = 0; p < page.Instances.Count; p++)
                {
                    var distance = (cameraPosition - page.Instances[p].TranslationVector).LengthSquared();
                    //if (distance < maxInstanceDistanceSquared)
                    {
                        var worldMatrix = page.Instances[p];

                        if (component.UseDistanceScaling)
                        {
                            // Fade out the mesh by scaling it, this could be done in the shader for more speeeed
                            var distanceToCamera = Math.Max(0.0f, (cameraPosition - worldMatrix.TranslationVector).Length() - minDistance);
                            var relativeScale = Math.Min(1.0f, distanceToCamera / distanceRange);

                            var distanceScale = (float)MathUtil.Lerp(1.0f, 0.0f, Math.Pow(relativeScale, 2.0f));
                            var scale = Matrix.Scaling(distanceScale);

                            worldMatrix = scale * worldMatrix;
                        }

                        var bounds = modelBounds;
                        bounds.Center = Vector3.Zero;

                        bounds.Transform(worldMatrix);

                        if (!CullInstances || VisibilityGroup.FrustumContainsBox(ref cameraFrustum, ref bounds, true))
                        {
                            var index = Interlocked.Increment(ref renderData.Count) - 1;
                            renderData.TransformData[index] = worldMatrix;
                        }
                    }
                }
            });
        }
    }

    public class TerrainVegetationRenderData
    {
        public Matrix[] TransformData = new Matrix[0];
        public int Count = 0;
        public TerrainVegetationPage[] Pages;
    }

    public class TerrainVegetationPage
    {
        public Vector3 WorldPosition;
        public Int2 PagePosition;
        public FastList<Matrix> Instances;
        
        public override string ToString()
             => WorldPosition.ToString();
    }
}
