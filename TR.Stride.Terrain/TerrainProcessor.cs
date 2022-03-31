using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Physics;
using Stride.Rendering;
using Stride.Rendering.LightProbes;
using System;
using System.Collections.Generic;
using System.Text;
using Buffer = Stride.Graphics.Buffer;

namespace TR.Stride.Terrain
{
    /// <summary>
    /// Generates height map models for any terrain components,
    /// 
    /// </summary>
    public class TerrainProcessor : EntityProcessor<TerrainComponent, TerrainRenderData>, IEntityComponentRenderProcessor
    {
        public VisibilityGroup VisibilityGroup { get; set; }

        protected override TerrainRenderData GenerateComponentData([NotNull] Entity entity, [NotNull] TerrainComponent component)
        {
            return new TerrainRenderData
            {
            };
        }

        private void DestroyMesh(TerrainRenderData data)
        {
            data.ModelComponent?.Entity?.Remove(data.ModelComponent);

            if (data.Mesh != null)
            {
                var meshDraw = data.Mesh.Draw;
                meshDraw.IndexBuffer.Buffer.Dispose();
                meshDraw.VertexBuffers[0].Buffer.Dispose();
            }
        }

        protected override void OnEntityComponentRemoved(Entity entity, [NotNull] TerrainComponent component, [NotNull] TerrainRenderData data)
        {
            base.OnEntityComponentRemoved(entity, component, data);

            DestroyMesh(data);
        }

        public override void Draw(RenderContext context)
        {
            base.Draw(context);

            var graphicsDevice = Services.GetService<IGraphicsDeviceService>().GraphicsDevice;

            // Update mesh if dirty
            foreach (var pair in ComponentDatas)
            {
                var component = pair.Key;
                var data = pair.Value;

                if (component.Material == null || component.Heightmap == null || component.Size <= 0.0f || component.Material.Passes.Count == 0)
                {
                    DestroyMesh(data);
                    continue;
                }

                // Sync properties
                data.ModelComponent.Model.Materials.Clear();
                data.ModelComponent.Model.Materials.Add(component.Material);

                data.ModelComponent.IsShadowCaster = component.CastShadows;

                // Regenerate mesh if needed
                if (data.IsDirty(component))
                {
                    component.ShouldRecreateMesh = false;
                    data.Update(component);

                    DestroyMesh(data);

                    data.Mesh = CreateMeshFromHeightMap(graphicsDevice, component.Size, component.Heightmap);
                    data.ModelComponent.Model.Meshes[0] = data.Mesh;
                    component.Entity.Add(data.ModelComponent);
                }
            }
        }

        /// <summary>
        /// Creates a tesselated plane for the terrain with normals and tangents
        /// Vertex Y position is retrieved from the height map
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="size"></param>
        /// <param name="heightmap"></param>
        /// <returns></returns>
        private Mesh CreateMeshFromHeightMap(GraphicsDevice graphicsDevice, float size, Heightmap heightmap)
        {
            var tessellationX = heightmap.Size.X;
            var tessellationY = heightmap.Size.Y;
            var columnCount = (tessellationX + 1);
            var rowCount = (tessellationY + 1);
            var vertices = new VertexPositionNormalTangentTexture[columnCount * rowCount];
            var indices = new int[tessellationX * tessellationY * 6];
            var deltaX = size / tessellationX;
            var deltaY = size / tessellationY;

            size /= 2.0f;

            var vertexCount = 0;
            var indexCount = 0;

            var points = new Vector3[columnCount * rowCount];

            for (var y = 0; y < (tessellationY + 1); y++)
            {
                for (var x = 0; x < (tessellationX + 1); x++)
                {
                    var height = heightmap.GetHeightAt(x, y);

                    var position = new Vector3(-size + deltaX * x, height, -size + deltaY * y);
                    var normal = heightmap.GetNormal(x, y);
                    var tangent = heightmap.GetTangent(x, y);
                    var texCoord = new Vector2(x / (float)tessellationX, y / (float)tessellationY);

                    points[vertexCount] = position;
                    vertices[vertexCount++] = new VertexPositionNormalTangentTexture(position, normal, tangent, texCoord);
                }
            }

            for (var y = 0; y < tessellationY; y++)
            {
                for (var x = 0; x < tessellationX; x++)
                {
                    var vbase = columnCount * y + x;
                    indices[indexCount++] = (vbase + 1);
                    indices[indexCount++] = (vbase + 1 + columnCount);
                    indices[indexCount++] = (vbase + columnCount);
                    indices[indexCount++] = (vbase + 1);
                    indices[indexCount++] = (vbase + columnCount);
                    indices[indexCount++] = (vbase);
                }
            }

            var vertexBuffer = Buffer.Vertex.New(graphicsDevice, vertices, GraphicsResourceUsage.Dynamic);
            var indexBuffer = Buffer.Index.New(graphicsDevice, indices);

            return new Mesh
            {
                Draw = new MeshDraw
                {
                    PrimitiveType = PrimitiveType.TriangleList,
                    DrawCount = indices.Length,
                    IndexBuffer = new IndexBufferBinding(indexBuffer, true, indices.Length),
                    VertexBuffers = new[] { new VertexBufferBinding(vertexBuffer, VertexPositionNormalTangentTexture.Layout, vertexBuffer.ElementCount) },
                },
                BoundingBox = BoundingBox.FromPoints(points),
                BoundingSphere = BoundingSphere.FromPoints(points)
            };
        }
    }

    public class TerrainRenderData
    {
        public float Size { get; set; }
        public Material Material { get; set; }
        public Heightmap Heightmap { get; set; }
        public Mesh Mesh { get; set; }
        public ModelComponent ModelComponent { get; set; } = new ModelComponent();

        public TerrainRenderData()
        {
            ModelComponent.Model = new Model
                {
                    new Mesh()
                };
        }

        public void Update(TerrainComponent component)
        {
            Size = component.Size;
            Material = component.Material;
            Heightmap = component.Heightmap;
        }

        public bool IsDirty(TerrainComponent component)
            => Material != component.Material || Size != component.Size || Heightmap != component.Heightmap || Mesh == null || component.ShouldRecreateMesh;
    }
}
