using Stride.Core;
using Stride.Engine;
using Stride.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stride.Core.Mathematics;
using System.ComponentModel;
using Stride.Graphics;
using Buffer = Stride.Graphics.Buffer;

namespace TR.Stride.Ocean
{
    /// <summary>
    /// Default ocean mesh with clip levels for lod
    /// </summary>
    [DataContract]
    public class DefaultOceanMesh : IOceanMesh, IDisposable
    {
        private int _clipLevels = 8;
        [DataMember, DefaultValue(8)] public int ClipLevels { get => _clipLevels; set { _clipLevels = value; _isDirty = true; } }

        private int _vertexDensity = 30;
        [DataMember, DefaultValue(30)] public int VertexDensity { get => _vertexDensity; set { _vertexDensity = value; _isDirty = true; } }

        private float _skirtSize = 55.4f;
        [DataMember, DefaultValue(55.4f)] public float SkirtSize { get => _skirtSize; set { _skirtSize = value; _isDirty = true; } }

        private float _lengthScale = 15;
        [DataMember, DefaultValue(15)] public float LengthScale { get => _lengthScale; set { _lengthScale = value; _isDirty = true; } }

        private int GridSize => 4 * VertexDensity + 1;

        private List<Entity> Rings { get; set; } = new();
        private List<Entity> Trims { get; set; } = new();
        private Entity Center { get; set; }
        private Entity Skirt { get; set; }

        private bool _isDirty = true;

        private Material[] _materials;
        private OceanComponent _component;
        private Entity _entity;

        private readonly Quaternion[] TrimRotations = new Quaternion[]
        {
            Quaternion.RotationAxis(Vector3.UnitY, MathUtil.DegreesToRadians(180)),
            Quaternion.RotationAxis(Vector3.UnitY, MathUtil.DegreesToRadians(90)),
            Quaternion.RotationAxis(Vector3.UnitY, MathUtil.DegreesToRadians(270)),
            Quaternion.Identity
        };

        public void SetOcean(OceanComponent component, Material[] materials)
        {
            _component = component;
            _entity = component.Entity;
            _materials = materials;
            _isDirty = true;
        }

        public void Update(GraphicsDevice graphicsDevice, CameraComponent camera)
        {
            var k = GridSize;

            if (_isDirty)
            {
                DestroyChildren();

                Center = CreateEntity(_entity, "Center", CreatePlaneMesh(graphicsDevice, 2 * k, 2 * k, 1, Seams.All), _materials.Last());

                var ring = CreateRingMesh(graphicsDevice, k, 1);
                var trim = CreateTrimMesh(graphicsDevice, k, 1);

                for (var i = 0; i < ClipLevels; i++)
                {
                    Rings.Add(CreateEntity(_entity, $"Ring_{i}", ring, _materials.Last()));
                    Trims.Add(CreateEntity(_entity, $"Trim_{i}", trim, _materials.Last()));
                }

                Skirt = CreateEntity(_entity, "Skirt", CreateSkirtMesh(graphicsDevice, k, SkirtSize), _materials.Last());

                _isDirty = false;
            }

            var cameraPosition = camera.GetWorldPosition();
            cameraPosition.Y -= _entity.Transform.WorldMatrix.TranslationVector.Y;

            // Update mesh positions
            var activeLevels = GetActiveLodLevels(cameraPosition);

            var scale = GetClipLevelScale(-1, activeLevels);

            var previousSnappedPosition = Snap(cameraPosition, scale * 2);

            Center.Transform.Position = previousSnappedPosition + OffsetFromCenter(-1, activeLevels);
            Center.Transform.Scale = new Vector3(scale, 1, scale);

            for (var i = 0; i < ClipLevels; i++)
            {
                if (i >= activeLevels)
                {
                    Rings[i].Get<ModelComponent>().Enabled = false;
                    Trims[i].Get<ModelComponent>().Enabled = false;

                    continue;
                }

                Rings[i].Get<ModelComponent>().Enabled = true;
                Trims[i].Get<ModelComponent>().Enabled = true;

                scale = GetClipLevelScale(i, activeLevels);
                var centerOffset = OffsetFromCenter(i, activeLevels);
                var snappedPosition = Snap(cameraPosition, scale * 2);

                var trimPosition = centerOffset + snappedPosition + scale * (k - 1) / 2 * new Vector3(1, 0, 1);
                var shiftX = previousSnappedPosition.X - snappedPosition.X < float.Epsilon ? 1 : 0;
                var shiftZ = previousSnappedPosition.Z - snappedPosition.Z < float.Epsilon ? 1 : 0;

                trimPosition += shiftX * (k + 1) * scale * Vector3.UnitX;
                trimPosition += shiftZ * (k + 1) * scale * Vector3.UnitZ;

                Trims[i].Transform.Position = trimPosition;
                Trims[i].Transform.Rotation = TrimRotations[shiftX + 2 * shiftZ];
                Trims[i].Transform.Scale = new Vector3(scale, 1, scale);

                Rings[i].Transform.Position = snappedPosition + centerOffset;
                Rings[i].Transform.Scale = new Vector3(scale, 1, scale);

                previousSnappedPosition = snappedPosition;
            }

            scale = LengthScale * 2 * MathF.Pow(2, ClipLevels);
            Skirt.Transform.Position = new Vector3(-1, 0, -1) * scale * (SkirtSize + 0.5f - 0.5f / GridSize) + previousSnappedPosition;
            Skirt.Transform.Scale = new Vector3(scale, 1, scale);

            SetMaterial(Center, GetMaterial(ClipLevels - activeLevels - 1));

            for (int i = 0; i < Rings.Count; i++)
            {
                SetMaterial(Rings[i], GetMaterial(ClipLevels - activeLevels + i));
                SetMaterial(Trims[i], GetMaterial(ClipLevels - activeLevels + i));
            }
        }

        private int GetActiveLodLevels(Vector3 cameraPosition)
            => ClipLevels - MathUtil.Clamp((int)MathF.Log((1.7f * MathF.Abs(cameraPosition.Y) + 1) / LengthScale, 2), 0, ClipLevels);

        private float GetClipLevelScale(int level, int activeLevels)
            => LengthScale / GridSize * MathF.Pow(2, ClipLevels - activeLevels + level + 1);

        private Vector3 OffsetFromCenter(int level, int activeLevels)
            => (MathF.Pow(2, ClipLevels) + GeometricProgressionSum(2, 2, ClipLevels - activeLevels + level + 1, ClipLevels - 1))
                   * LengthScale / GridSize * (GridSize - 1) / 2 * new Vector3(-1, 0, -1);

        private float GeometricProgressionSum(float b0, float q, int n1, int n2)
            => b0 / (1 - q) * (MathF.Pow(q, n2) - MathF.Pow(q, n1));

        private static Vector3 Snap(Vector3 coords, float scale)
        {
            if (coords.X >= 0)
                coords.X = MathF.Floor(coords.X / scale) * scale;
            else
                coords.X = MathF.Ceiling((coords.X - scale + 1) / scale) * scale;

            if (coords.Z < 0)
                coords.Z = MathF.Floor(coords.Z / scale) * scale;
            else
                coords.Z = MathF.Ceiling((coords.Z - scale + 1) / scale) * scale;

            coords.Y = 0;
            return coords;
        }

        private static void SetMaterial(Entity entity, Material material)
        {
            var modelComponent = entity.Get<ModelComponent>();
            if (modelComponent.Model.Materials[0].Material != material)
            {
                modelComponent.Model.Materials[0] = material;
            }
        }

        private Material GetMaterial(int lodLevel)
        {
            if (lodLevel - 2 <= 0)
                return _materials[0];

            if (lodLevel - 2 <= 2)
                return _materials[1];

            return _materials[2];
        }

        private static Mesh CreateRingMesh(GraphicsDevice graphicsDevice, int k, float lengthScale)
        {
            return CreateMergedMesh(graphicsDevice,
                (CreatePlaneMeshData(2 * k, (k - 1) / 2, lengthScale, Seams.Bottom | Seams.Right | Seams.Left), Matrix.Translation(Vector3.Zero)),
                (CreatePlaneMeshData(2 * k, (k - 1) / 2, lengthScale, Seams.Top | Seams.Right | Seams.Left), Matrix.Translation(new Vector3(0, 0, k + 1 + (k - 1) / 2) * lengthScale)),
                (CreatePlaneMeshData((k - 1) / 2, k + 1, lengthScale, Seams.Left), Matrix.Translation(new Vector3(0, 0, (k - 1) / 2) * lengthScale)),
                (CreatePlaneMeshData((k - 1) / 2, k + 1, lengthScale, Seams.Right), Matrix.Translation(new Vector3(k + 1 + (k - 1) / 2, 0, (k - 1) / 2) * lengthScale))
                );
        }

        private static Mesh CreateTrimMesh(GraphicsDevice graphicsDevice, int k, float lengthScale)
        {
            return CreateMergedMesh(graphicsDevice,
                (CreatePlaneMeshData(k + 1, 1, lengthScale, Seams.None, 1), Matrix.Translation(new Vector3(-k - 1, 0, -1) * lengthScale)),
                (CreatePlaneMeshData(1, k, lengthScale, Seams.None, 1), Matrix.Translation(new Vector3(-1, 0, -k - 1) * lengthScale))
                );
        }

        private static Mesh CreateSkirtMesh(GraphicsDevice graphicsDevice, int k, float outerBorderScale)
        {
            var quad = CreatePlaneMeshData(1, 1, 1, Seams.None);
            var hStrip = CreatePlaneMeshData(k, 1, 1, Seams.None);
            var vStrip = CreatePlaneMeshData(1, k, 1, Seams.None);

            Vector3 cornerQuadScale = new Vector3(outerBorderScale, 1, outerBorderScale);
            Vector3 midQuadScaleVert = new Vector3(1f / k, 1, outerBorderScale);
            Vector3 midQuadScaleHor = new Vector3(outerBorderScale, 1, 1f / k);

            return CreateMergedMesh(graphicsDevice,
                (quad, CreateTransform(Vector3.Zero, cornerQuadScale)),
                (hStrip, CreateTransform(Vector3.UnitX * outerBorderScale, midQuadScaleVert)),
                (quad, CreateTransform(Vector3.UnitX * (outerBorderScale + 1), cornerQuadScale)),
                (vStrip, CreateTransform(Vector3.UnitZ * outerBorderScale, midQuadScaleHor)),
                (vStrip, CreateTransform(Vector3.UnitX * (outerBorderScale + 1) + Vector3.UnitZ * outerBorderScale, midQuadScaleHor)),
                (quad, CreateTransform(Vector3.UnitZ * (outerBorderScale + 1), cornerQuadScale)),
                (hStrip, CreateTransform(Vector3.UnitX * outerBorderScale + Vector3.UnitZ * (outerBorderScale + 1), midQuadScaleVert)),
                (quad, CreateTransform(Vector3.UnitX * (outerBorderScale + 1) + Vector3.UnitZ * (outerBorderScale + 1), cornerQuadScale))
                );

            Matrix CreateTransform(Vector3 position, Vector3 scale)
                => Matrix.Transformation(Vector3.Zero, Quaternion.Identity, scale, Vector3.Zero, Quaternion.Identity, position);
        }

        private static Mesh CreateMergedMesh(GraphicsDevice graphicsDevice, params ((VertexPositionNormalTexture[] vertices, int[] indices) mesh, Matrix transform)[] meshes)
        {
            var totalVertexCount = meshes.Sum(x => x.mesh.vertices.Length);
            var totalIndexCount = meshes.Sum(x => x.mesh.indices.Length);

            var mergedVertices = new VertexPositionNormalTexture[totalVertexCount];
            var mergedIndices = new int[totalIndexCount];

            var vertexOffset = 0;
            var indexOffset = 0;

            // Merge meshes
            foreach (var ((vertices, indices), transform) in meshes)
            {
                for (var i = 0; i < vertices.Length; i++)
                {
                    mergedVertices[vertexOffset + i] = vertices[i];
                    mergedVertices[vertexOffset + i].Position = Vector3.TransformCoordinate(mergedVertices[vertexOffset + i].Position, transform);
                }

                for (var i = 0; i < indices.Length; i++)
                {
                    mergedIndices[indexOffset + i] = indices[i] + vertexOffset;
                }

                vertexOffset += vertices.Length;
                indexOffset += indices.Length;
            }

            var vertexBuffer = Buffer.Vertex.New(graphicsDevice, mergedVertices, GraphicsResourceUsage.Dynamic);
            var indexBuffer = Buffer.Index.New(graphicsDevice, mergedIndices);

            return new Mesh
            {
                Draw = new MeshDraw
                {
                    PrimitiveType = PrimitiveType.TriangleList,
                    DrawCount = mergedIndices.Length,
                    IndexBuffer = new IndexBufferBinding(indexBuffer, true, mergedIndices.Length),
                    VertexBuffers = new[] { new VertexBufferBinding(vertexBuffer, VertexPositionNormalTexture.Layout, vertexBuffer.ElementCount) },
                }
            };
        }

        private static Mesh CreatePlaneMesh(GraphicsDevice graphicsDevice, int width, int height, float lengthScale, Seams seams, int trianglesShift = 0)
        {
            var (vertices, indices) = CreatePlaneMeshData(width, height, lengthScale, seams, trianglesShift);

            var vertexBuffer = Buffer.Vertex.New(graphicsDevice, vertices, GraphicsResourceUsage.Dynamic);
            var indexBuffer = Buffer.Index.New(graphicsDevice, indices);

            return new Mesh
            {
                Draw = new MeshDraw
                {
                    PrimitiveType = PrimitiveType.TriangleList,
                    DrawCount = indices.Length,
                    IndexBuffer = new IndexBufferBinding(indexBuffer, true, indices.Length),
                    VertexBuffers = new[] { new VertexBufferBinding(vertexBuffer, VertexPositionNormalTexture.Layout, vertexBuffer.ElementCount) },
                }
            };
        }

        private static (VertexPositionNormalTexture[] vertices, int[] indices) CreatePlaneMeshData(int width, int height, float lengthScale, Seams seams, int trianglesShift = 0)
        {
            var vertexCount = (width + 1) * (height + 1);
            var indexCount = width * height * 2 * 3;

            var vertices = new VertexPositionNormalTexture[vertexCount];
            var indices = new int[indexCount];
            for (int i = 0; i < height + 1; i++)
            {
                for (int j = 0; j < width + 1; j++)
                {
                    int x = j;
                    int z = i;

                    if ((i == 0 && seams.HasFlag(Seams.Bottom)) || (i == height && seams.HasFlag(Seams.Top)))
                        x = x / 2 * 2;
                    if ((j == 0 && seams.HasFlag(Seams.Left)) || (j == width && seams.HasFlag(Seams.Right)))
                        z = z / 2 * 2;

                    var index = j + i * (width + 1);
                    vertices[index].Position = new Vector3(x, 0, z) * lengthScale;
                    vertices[index].Normal = Vector3.UnitY;
                }
            }

            int tris = 0;
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    int k = j + i * (width + 1);
                    if ((i + j + trianglesShift) % 2 == 0)
                    {
                        indices[tris++] = k;
                        indices[tris++] = k + width + 2;
                        indices[tris++] = k + width + 1;

                        indices[tris++] = k;
                        indices[tris++] = k + 1;
                        indices[tris++] = k + width + 2;
                    }
                    else
                    {
                        indices[tris++] = k;
                        indices[tris++] = k + 1;
                        indices[tris++] = k + width + 1;

                        indices[tris++] = k + 1;
                        indices[tris++] = k + width + 2;
                        indices[tris++] = k + width + 1;
                    }
                }
            }

            return (vertices, indices);
        }

        private static Entity CreateEntity(Entity parent, string name, Mesh mesh, Material material)
        {
            var entity = new Entity(name);

            var modelComponent = new ModelComponent
            {
                Model = new Model
                {
                    mesh,
                    material
                }
            };

            modelComponent.IsShadowCaster = false;

            entity.Add(modelComponent);

            parent.AddChild(entity);

            return entity;
        }

        private void DestroyChildren()
        {
            foreach (var entity in Rings)
                Destroy(entity);

            foreach (var entity in Trims)
                Destroy(entity);

            if (Center != null)
                Destroy(Center);

            if (Skirt != null)
                Destroy(Skirt);

            Rings.Clear();
            Trims.Clear();

            Center = null;
            Skirt = null;

            static void Destroy(Entity entity)
            {
                if (entity == null)
                    return;

                entity.Transform.Parent = null;

                var modelComponent = entity.Get<ModelComponent>();
                if (modelComponent?.Model?.Meshes?.Count > 0)
                {
                    var meshDraw = modelComponent.Model.Meshes[0].Draw;
                    meshDraw.IndexBuffer.Buffer.Dispose();
                    meshDraw.VertexBuffers[0].Buffer.Dispose();
                }
            }
        }

        public void Dispose()
        {
            DestroyChildren();
        }
    }
}
