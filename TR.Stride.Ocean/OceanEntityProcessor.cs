using Stride.Core.Annotations;
using Stride.Core.Threading;
using Stride.Engine;
using Stride.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Buffer = Stride.Graphics.Buffer;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Rendering.ComputeEffect;

namespace TR.Stride.Ocean
{
    public class OceanEntityProcessor : EntityProcessor<OceanComponent, OceanRenderData>, IEntityComponentRenderProcessor
    {
        private FastFourierTransformShaders _fastFourierTransformShaders;
        private ComputeEffectShader _calculateInitialSpectrumShader;
        private ComputeEffectShader _calculateConjugatedSpectrumShader;
        private ComputeEffectShader _timeDependantSpectrumShader;
        private ComputeEffectShader _fillResultTexturesShader;
        private ComputeEffectShader _generateMipsShader;

        public VisibilityGroup VisibilityGroup { get; set; }

        protected override OceanRenderData GenerateComponentData([NotNull] Entity entity, [NotNull] OceanComponent component)
            => new OceanRenderData();

        protected override void OnEntityComponentRemoved(Entity entity, [NotNull] OceanComponent component, [NotNull] OceanRenderData data)
        {
            base.OnEntityComponentRemoved(entity, component, data);
            base.OnEntityComponentRemoved(entity, component, data);

            data?.Dispose();
        }

        public override void Draw(RenderContext context)
        {
            base.Draw(context);

            var graphicsDevice = Services.GetService<IGraphicsDeviceService>().GraphicsDevice;
            var camera = GetCamera();
            var cameraPosition = GetCameraPosition(camera);
            var sceneSystem = context.Services.GetService<SceneSystem>();
            var time = (float)sceneSystem.Game.UpdateTime.Total.TotalSeconds;
            var deltaTime = (float)sceneSystem.Game.UpdateTime.Elapsed.TotalSeconds;

            if (_calculateInitialSpectrumShader == null)
            {
                // TODO: DISPOSE AT SYSTEM REMOVAL!
                _calculateInitialSpectrumShader = new ComputeEffectShader(context) { ShaderSourceName = "OceanCalculateInitialSpectrum", Name = "OceanCalculateInitialSpectrum"};
                _calculateConjugatedSpectrumShader = new ComputeEffectShader(context) { ShaderSourceName = "OceanCalculateConjugatedSpectrum", Name = "OceanCalculateConjugatedSpectrum" };
                _timeDependantSpectrumShader = new ComputeEffectShader(context) { ShaderSourceName = "OceanTimeDependentSpectrum", Name = "OceanTimeDependentSpectrum" };
                _fillResultTexturesShader = new ComputeEffectShader(context) { ShaderSourceName = "OceanFillResultTextures", Name = "OceanFillResultTextures" };
                _generateMipsShader = new ComputeEffectShader(context) { ShaderSourceName = "OceanGenerateMips", Name = "OceanGenerateMips" };
                _fastFourierTransformShaders = new FastFourierTransformShaders(context);
            }

            Dispatcher.ForEach(ComponentDatas, (pair) =>
            {
                var component = pair.Key;
                var data = pair.Value;
                var entity = component.Entity;

                var k = component.GridSize;

                var renderDrawContext = context.GetThreadContext();
                var commandList = renderDrawContext.CommandList;

                // Update shader parameters for wave settings
                component.WavesSettings.UpdateShaderParameters();

                // Create cascades if dirty
                var calculateInitials = component.AlwaysRecalculateInitials;
                if (data.Size != component.Size || data.Cascades == null)
                {
                    data.DestroyCascades();

                    data.Size = component.Size;

                    // Create noise texture
                    data.GaussianNoise?.Dispose();

                    var rng = new Random();

                    var noise = new Vector2[data.Size * data.Size];
                    for (int y = 0; y < data.Size; y++)
                    {
                        for (int x = 0; x < data.Size; x++)
                        {
                            var index = y * data.Size + x;
                            noise[index] = new Vector2(NormalRandom(rng), NormalRandom(rng));
                        }
                    }

                    data.GaussianNoise = Texture.New2D(graphicsDevice, data.Size, data.Size, PixelFormat.R32G32_Float, noise, TextureFlags.ShaderResource | TextureFlags.UnorderedAccess);

                    static float NormalRandom(Random rng)
                    {
                        return MathF.Cos(2 * MathF.PI * (float)rng.NextDouble()) * MathF.Sqrt(-2 * MathF.Log((float)rng.NextDouble()));
                    }

                    data.FFT?.Dispose();
                    data.FFT = new FastFourierTransform(renderDrawContext, data.Size, _fastFourierTransformShaders);

                    data.Cascades = new WavesCascade[]
                    {
                        new WavesCascade(graphicsDevice, data.Size, data.FFT, data.GaussianNoise),
                        new WavesCascade(graphicsDevice, data.Size, data.FFT, data.GaussianNoise),
                        new WavesCascade(graphicsDevice, data.Size, data.FFT, data.GaussianNoise)
                    };

                    calculateInitials = true;
                }

                // Calculate initial spectrums
                if (calculateInitials)
                {
                    float boundary1 = 2 * MathF.PI / component.LengthScale1 * 6f;
                    float boundary2 = 2 * MathF.PI / component.LengthScale2 * 6f;

                    data.Cascades[0].CalculateInitials(renderDrawContext, _calculateInitialSpectrumShader, _calculateConjugatedSpectrumShader, component.WavesSettings, component.LengthScale0, 0.0001f, boundary1);
                    data.Cascades[1].CalculateInitials(renderDrawContext, _calculateInitialSpectrumShader, _calculateConjugatedSpectrumShader, component.WavesSettings, component.LengthScale1, boundary1, boundary2);
                    data.Cascades[2].CalculateInitials(renderDrawContext, _calculateInitialSpectrumShader, _calculateConjugatedSpectrumShader, component.WavesSettings, component.LengthScale2, boundary2, 9999);
                }

                // Update time dependant waves
                foreach (var cascade in data.Cascades)
                {
                    cascade.CalculateWavesAtTime(renderDrawContext, _timeDependantSpectrumShader, _fillResultTexturesShader, _generateMipsShader, time, deltaTime);
                }

                // (re)create meshes if needed
                if (data.Rings.Count != component.ClipLevels
                    || data.Trims.Count != component.ClipLevels
                    || data.VertexDensity != component.VertexDensity
                    || data.SkirtSize != component.SkirtSize)
                {
                    data.DestroyChildren();

                    if (data.Materials == null)
                    {
                        data.Materials = new Material[]
                        {
                            CreateMaterial(graphicsDevice),
                            CreateMaterial(graphicsDevice),
                            CreateMaterial(graphicsDevice)
                        };

                        data.Materials[0].Passes[0].Parameters.Set(OceanShadingCommonKeys.Lod, 0);
                        data.Materials[1].Passes[0].Parameters.Set(OceanShadingCommonKeys.Lod, 1);
                        data.Materials[2].Passes[0].Parameters.Set(OceanShadingCommonKeys.Lod, 2);
                    }

                    data.VertexDensity = component.VertexDensity;
                    data.SkirtSize = component.SkirtSize;

                    data.Center = CreateEntity(entity, "Center", CreatePlaneMesh(graphicsDevice, 2 * k, 2 * k, 1, Seams.All), data.Materials.Last());

                    var ring = CreateRingMesh(graphicsDevice, k, 1);
                    var trim = CreateTrimMesh(graphicsDevice, k, 1);

                    for (var i = 0; i < component.ClipLevels; i++)
                    {
                        data.Rings.Add(CreateEntity(entity, $"Ring_{i}", ring, data.Materials.Last()));
                        data.Trims.Add(CreateEntity(entity, $"Trim_{i}", trim, data.Materials.Last()));
                    }

                    data.Skirt = CreateEntity(entity, "Skirt", CreateSkirtMesh(graphicsDevice, k, data.SkirtSize), data.Materials.Last());
                }

                // Update mesh positions
                var activeLevels = component.GetActiveLodLevels(cameraPosition);

                var scale = component.GetClipLevelScale(-1, activeLevels);

                var previousSnappedPosition = Snap(cameraPosition, scale * 2);

                data.Center.Transform.Position = previousSnappedPosition + component.OffsetFromCenter(-1, activeLevels);
                data.Center.Transform.Scale = new Vector3(scale, 1, scale);

                for (var i = 0; i < component.ClipLevels; i++)
                {
                    if (i >= activeLevels)
                    {
                        data.Rings[i].Transform.Parent = null;
                        data.Trims[i].Transform.Parent = null;

                        continue;
                    }

                    if (data.Rings[i].Transform.Parent == null)
                        entity.AddChild(data.Rings[i]);

                    if (data.Trims[i].Transform.Parent == null)
                        entity.AddChild(data.Trims[i]);

                    scale = component.GetClipLevelScale(i, activeLevels);
                    var centerOffset = component.OffsetFromCenter(i, activeLevels);
                    var snappedPosition = Snap(cameraPosition, scale * 2);

                    var trimPosition = centerOffset + snappedPosition + scale * (k - 1) / 2 * new Vector3(1, 0, 1);
                    var shiftX = previousSnappedPosition.X - snappedPosition.X < float.Epsilon ? 1 : 0;
                    var shiftZ = previousSnappedPosition.Z - snappedPosition.Z < float.Epsilon ? 1 : 0;

                    trimPosition += shiftX * (k + 1) * scale * Vector3.UnitX;
                    trimPosition += shiftZ * (k + 1) * scale * Vector3.UnitZ;

                    data.Trims[i].Transform.Position = trimPosition;
                    data.Trims[i].Transform.Rotation = data.TrimRotations[shiftX + 2 * shiftZ];
                    data.Trims[i].Transform.Scale = new Vector3(scale, 1, scale);

                    data.Rings[i].Transform.Position = snappedPosition + centerOffset;
                    data.Rings[i].Transform.Scale = new Vector3(scale, 1, scale);

                    previousSnappedPosition = snappedPosition;
                }

                scale = component.LengthScale * 2 * MathF.Pow(2, component.ClipLevels);
                data.Skirt.Transform.Position = new Vector3(-1, 0, -1) * scale * (component.SkirtSize + 0.5f - 0.5f / component.GridSize) + previousSnappedPosition;
                data.Skirt.Transform.Scale = new Vector3(scale, 1, scale);

                // Update materials

                foreach (var material in data.Materials)
                {
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Displacement_c0, data.Cascades[0].Displacement);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Displacement_c1, data.Cascades[1].Displacement);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Displacement_c2, data.Cascades[2].Displacement);

                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Turbulence_c0, data.Cascades[0].Turbulence);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Turbulence_c1, data.Cascades[1].Turbulence);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Turbulence_c2, data.Cascades[2].Turbulence);

                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Derivatives_c0, data.Cascades[0].Derivatives);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Derivatives_c1, data.Cascades[1].Derivatives);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Derivatives_c2, data.Cascades[2].Derivatives);

                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.LengthScale0, component.LengthScale0);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.LengthScale1, component.LengthScale1);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.LengthScale2, component.LengthScale2);

                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.LodScale, component.Material.LodScale);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.SSSBase, component.Material.SSSBase);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.SSSScale, component.Material.SSSScale);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.SSSStrength, component.Material.SSSStrength);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.SSSColor, component.Material.SSSColor);

                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.FoamBiasLOD0, component.Material.FoamBiasLOD0);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.FoamBiasLOD1, component.Material.FoamBiasLOD1);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.FoamBiasLOD2, component.Material.FoamBiasLOD2);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.FoamScale, component.Material.FoamScale);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.FoamColor, component.Material.FoamColor);

                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Roughness, component.Material.Roughness);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.RoughnessScale, component.Material.RoughnessScale);
                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.MaxGloss, component.Material.MaxGloss);

                    material.Passes[0].Parameters.Set(OceanShadingCommonKeys.Color, component.Material.Color);
                    
                    if (component.Sun != null)
                    {
                        var lightDirection = Vector3.TransformNormal(-Vector3.UnitZ, component.Sun.Entity.Transform.WorldMatrix);
                        lightDirection.Normalize();

                        material.Passes[0].Parameters.Set(OceanShadingCommonKeys.LightDirectionWS, lightDirection);
                    }
                }

                //data.Materials[0].Passes[0].Parameters.Set(OceanShadingCommonKeys.Color, new Color3(1, 0, 0));
                //data.Materials[1].Passes[0].Parameters.Set(OceanShadingCommonKeys.Color, new Color3(0, 1, 0));
                //data.Materials[2].Passes[0].Parameters.Set(OceanShadingCommonKeys.Color, new Color3(0, 0, 1));

                SetMaterial(data.Center, data.GetMaterial(component.ClipLevels - activeLevels - 1));

                for (int i = 0; i < data.Rings.Count; i++)
                {
                    SetMaterial(data.Rings[i], data.GetMaterial(component.ClipLevels - activeLevels + i));
                    SetMaterial(data.Trims[i], data.GetMaterial(component.ClipLevels - activeLevels + i));
                }
            });

            static void SetMaterial(Entity entity, Material material)
            {
                var modelComponent = entity.Get<ModelComponent>();
                if (modelComponent.Model.Materials[0].Material != material)
                {
                    modelComponent.Model.Materials[0] = material;
                }
            }

            static Vector3 Snap(Vector3 coords, float scale)
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

            static Material CreateMaterial(GraphicsDevice graphicsDevice)
            {
                return Material.New(graphicsDevice, new MaterialDescriptor
                {
                    Attributes = new MaterialAttributes
                    {
                        MicroSurface = new MaterialGlossinessMapFeature
                        {
                            GlossinessMap = new ComputeFloat(0.9f)
                        },
                        Diffuse = new MaterialDiffuseMapFeature
                        {
                            DiffuseMap = new ComputeColor(new Color4(0, 0, 0.0f, 1))
                        },
                        DiffuseModel = new MaterialDiffuseLambertModelFeature(),
                        Specular = new MaterialMetalnessMapFeature
                        {
                            MetalnessMap = new ComputeFloat(0.0f)
                        },
                        SpecularModel = new MaterialSpecularMicrofacetModelFeature
                        {
                            Environment = new MaterialSpecularMicrofacetEnvironmentGGXPolynomial() // TODO: Use lookup, need to find a way to locate the lookup texture first as the AttachedReferenceManager does not manage this at runtime ...
                        },
                        Emissive = new MaterialEmissiveMapFeature
                        {
                            EmissiveMap = new ComputeShaderClassColor
                            {
                                MixinReference = "OceanEmissive"
                            },
                            Intensity = new ComputeFloat(1.0f),
                            UseAlpha = false
                        },
                        Displacement = new MaterialDisplacementMapFeature
                        {
                            ScaleAndBias = false,
                            Intensity = new ComputeFloat(0),
                            DisplacementMap = new ComputeShaderClassScalar
                            {
                                MixinReference = "OceanDisplacement"
                            }
                        },
                        //Transparency = new MaterialTransparencyBlendFeature
                        //{
                        //    Alpha = new ComputeFloat(1),
                        //    Tint = new ComputeColor(new Color4(0, 0, 0, 0))
                        //}
                    }
                });
            }
        }

        private Mesh CreateRingMesh(GraphicsDevice graphicsDevice, int k, float lengthScale)
        {
            return CreateMergedMesh(graphicsDevice,
                (CreatePlaneMeshData(2 * k, (k - 1) / 2, lengthScale, Seams.Bottom | Seams.Right | Seams.Left), Matrix.Translation(Vector3.Zero)),
                (CreatePlaneMeshData(2 * k, (k - 1) / 2, lengthScale, Seams.Top | Seams.Right | Seams.Left), Matrix.Translation(new Vector3(0, 0, k + 1 + (k - 1) / 2) * lengthScale)),
                (CreatePlaneMeshData((k - 1) / 2, k + 1, lengthScale, Seams.Left), Matrix.Translation(new Vector3(0, 0, (k - 1) / 2) * lengthScale)),
                (CreatePlaneMeshData((k - 1) / 2, k + 1, lengthScale, Seams.Right), Matrix.Translation(new Vector3(k + 1 + (k - 1) / 2, 0, (k - 1) / 2) * lengthScale))
                );
        }

        private Mesh CreateTrimMesh(GraphicsDevice graphicsDevice, int k, float lengthScale)
        {
            return CreateMergedMesh(graphicsDevice,
                (CreatePlaneMeshData(k + 1, 1, lengthScale, Seams.None, 1), Matrix.Translation(new Vector3(-k - 1, 0, -1) * lengthScale)),
                (CreatePlaneMeshData(1, k, lengthScale, Seams.None, 1), Matrix.Translation(new Vector3(-1, 0, -k - 1) * lengthScale))
                );
        }

        private Mesh CreateSkirtMesh(GraphicsDevice graphicsDevice, int k, float outerBorderScale)
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

        private Mesh CreateMergedMesh(GraphicsDevice graphicsDevice, params ((VertexPositionNormalTexture[] vertices, int[] indices) mesh, Matrix transform)[] meshes)
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

        private Mesh CreatePlaneMesh(GraphicsDevice graphicsDevice, int width, int height, float lengthScale, Seams seams, int trianglesShift = 0)
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

        private Entity CreateEntity(Entity parent, string name, Mesh mesh, Material material)
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

            modelComponent.Model.Materials[0].IsShadowCaster = false;
            modelComponent.IsShadowCaster = false;

            entity.Add(modelComponent);

            parent.AddChild(entity);

            return entity;
        }

        private Vector3 GetCameraPosition(CameraComponent camera)
        {
            var viewMatrix = camera.ViewMatrix;
            viewMatrix.Invert();

            var cameraPosition = viewMatrix.TranslationVector;

            return cameraPosition;
        }

        /// <summary>
        /// Try to get the main camera, this can probably be done waaaaaaay better
        /// Contains a work around to get stuff working in the editor
        /// 
        /// Might not be needed if we switch to some kind of render feature instead 
        /// but will leave for now as we only really need to support the main camera 
        /// and it works ... usually
        /// </summary>
        private CameraComponent GetCamera()
        {
            var sceneSystem = Services.GetService<SceneSystem>();

            CameraComponent camera = null;
            if (sceneSystem.GraphicsCompositor.Cameras.Count == 0)
            {
                // The compositor wont have any cameras attached if the game is running in editor mode
                // Search through the scene systems until the camera entity is found
                // This is what you might call "A Hack"
                foreach (var system in sceneSystem.Game.GameSystems)
                {
                    if (system is SceneSystem editorSceneSystem)
                    {
                        foreach (var entity in editorSceneSystem.SceneInstance.RootScene.Entities)
                        {
                            if (entity.Name == "Camera Editor Entity")
                            {
                                camera = entity.Get<CameraComponent>();
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                camera = sceneSystem.GraphicsCompositor.Cameras[0].Camera;
            }

            return camera;
        }
    }

    public class OceanRenderData : IDisposable
    {
        public List<Entity> Rings { get; set; } = new();
        public List<Entity> Trims { get; set; } = new();
        public Entity Center { get; set; }
        public Entity Skirt { get; set; }

        public int VertexDensity { get; set; }
        public float SkirtSize { get; set; }

        public Material[] Materials { get; set; }

        public Quaternion[] TrimRotations = new Quaternion[]
        {
            Quaternion.RotationAxis(Vector3.UnitY, MathUtil.DegreesToRadians(180)),
            Quaternion.RotationAxis(Vector3.UnitY, MathUtil.DegreesToRadians(90)),
            Quaternion.RotationAxis(Vector3.UnitY, MathUtil.DegreesToRadians(270)),
            Quaternion.Identity
        };

        public int Size { get; set; }
        public WavesCascade[] Cascades { get; set; }

        public Texture GaussianNoise { get; set; }

        public FastFourierTransform FFT { get; set; }

        public Material GetMaterial(int lodLevel)
        {
            if (lodLevel - 2 <= 0)
                return Materials[0];

            if (lodLevel - 2 <= 2)
                return Materials[1];

            return Materials[2];
        }

        public void DestroyChildren()
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

        public void DestroyCascades()
        {
            if (Cascades != null)
            {
                foreach (var cascade in Cascades)
                {
                    cascade.Dispose();
                }

                Cascades = null;
            }
        }

        public void Dispose()
        {
            DestroyChildren();
            DestroyCascades();
            GaussianNoise?.Dispose();
        }
    }

    [Flags]
    public enum Seams
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8,
        All = Left | Right | Top | Bottom
    };
}
