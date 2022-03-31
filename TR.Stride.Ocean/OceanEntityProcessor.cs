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
using Stride.Rendering.ComputeEffect;
using Stride.Rendering.Images;

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

        public OceanEntityProcessor()
        {
            Order = -200;
        }

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

            var sceneSystem = context.Services.GetService<SceneSystem>();

            var camera = sceneSystem.TryGetMainCamera();
            if (camera == null)
                return;

            var time = (float)sceneSystem.Game.UpdateTime.Total.TotalSeconds;
            var deltaTime = (float)sceneSystem.Game.UpdateTime.Elapsed.TotalSeconds;

            if (_calculateInitialSpectrumShader == null)
            {
                // TODO: DISPOSE AT SYSTEM REMOVAL!
                _calculateInitialSpectrumShader = new ComputeEffectShader(context) { ShaderSourceName = "OceanCalculateInitialSpectrum", Name = "OceanCalculateInitialSpectrum" };
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

                using var profileContext = renderDrawContext.QueryManager.BeginProfile(Color4.White, ProfilingKeys.Ocean);

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

                // Read back wave data
                if (data.Readback == null)
                {
                    data.Readback = new();
                }

                data.Readback.FrameDelayCount = component.DisplacmentReadBackFrameDelay;
                data.Readback.SetInput(data.Cascades[0].Displacement);
                data.Readback.Draw(renderDrawContext);

                if (data.Readback.IsResultAvailable)
                {
                    component.Displacement = data.Readback.Result;
                }

                // Create materials
                var materialsDirty = false;
                if (data.Materials == null || component.Material != data.Material)
                {
                    materialsDirty = true;
                    data.Material = component.Material;

                    if (data.Material == null)
                    {
                        data.Materials = null;
                    }
                    else
                    {
                        var materialLod0 = data.Material.CreateMaterial(graphicsDevice, 0);
                        var materialLod1 = data.Material.CreateMaterial(graphicsDevice, 1);
                        var materialLod2 = data.Material.CreateMaterial(graphicsDevice, 2);

                        if (materialLod0 == null || materialLod1 == null || materialLod2 == null)
                            data.Materials = null;
                        else
                            data.Materials = new Material[] { materialLod0, materialLod1, materialLod2 };
                    }
                }

                // Bail out if no material set
                if (data.Materials == null)
                {
                    if (data.Mesh != null)
                    {
                        data.DestroyMesh();
                        data.Mesh = null;
                    }

                    return;
                }

                if (data.Mesh != component.Mesh || materialsDirty)
                {
                    data.DestroyMesh();

                    data.Mesh = component.Mesh;

                    if (data.Mesh != null)
                    {
                        data.Mesh.SetOcean(component, data.Materials);
                    }
                }

                // We kinda need a mesh
                if (data.Mesh == null)
                    return;

                // Update mesh and materials
                data.Material.UpdateMaterials(graphicsDevice, component, data.Materials, data.Cascades);
                data.Mesh.Update(graphicsDevice, camera);
            });
        }
    }

    public class OceanRenderData : IDisposable
    {
        public Material[] Materials { get; set; }
        public IOceanMaterial Material { get; set; }
        public IOceanMesh Mesh { get; set; }

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

        public ImageReadback<Vector4> Readback { get; set; }

        public void DestroyMesh()
        {
            if (Mesh is IDisposable disposable)
            {
                disposable.Dispose();
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
            DestroyMesh();
            DestroyCascades();
            GaussianNoise?.Dispose();
            Readback?.Dispose();
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
