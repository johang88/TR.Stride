using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Compositing;
using Stride.Rendering.ComputeEffect;
using Stride.Rendering.Images;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Buffer = Stride.Graphics.Buffer;

namespace TR.Stride.PostProcess
{
    [DataContract]
    public class BloomSettings
    {
        public float Strength;
        public float Radius;

        public BloomSettings()
        {
            Strength = 1;
            Radius = 1;
        }
    }

    [DataContract]
    public class ExposureSettings
    {
        [DefaultValue(true)] public bool AutoKey { get; set; } = true;
        [DefaultValue(0.08f)] public float Key { get; set; }  = 0.08f;
        [DefaultValue(1.0f / 64.0f)] public float MinExposure { get; set; }  = 1.0f / 64.0f;
        [DefaultValue(64.0f)] public float MaxExposure { get; set; } = 64.0f;
        [DefaultValue(1.1f)] public float AdaptionSpeed { get; set; } = 1.1f;
    }

    [DataContract(nameof(CustomPostProcessingEffects))]
    [Display("Custom Post-processing effects")]
    public class CustomPostProcessingEffects : ImageEffect, IImageEffectRenderer, IPostProcessingEffects
    {
        public bool RequiresVelocityBuffer => false;
        public bool RequiresNormalBuffer => false;
        public bool RequiresSpecularRoughnessBuffer => false;

        [DataMember] public Guid Id { get; set; } = Guid.NewGuid();

        private ImageEffectShader _brightPassShader;
        private ImageEffectShader _bloomDownSample;
        private ImageEffectShader _bloomUpSample;
        private ImageEffectShader _toneMapShader;
        private ComputeEffectShader _histogramShader;
        private ComputeEffectShader _histogramReduceShader;

        private Buffer _histogram = null;
        private Buffer _exposure = null;

        [DataMemberIgnore] public List<BloomSettings> Bloom { get; set; } = new List<BloomSettings>();

        [DataMember("Exposure")] public ExposureSettings ExposureSettings { get; set; } = new ExposureSettings();

        private List<Texture> _bloomRenderTargets = new(5);

        public CustomPostProcessingEffects(IServiceRegistry services)
            : this(RenderContext.GetShared(services))
        {
        }

        public CustomPostProcessingEffects(RenderContext context)
            : this()
        {
            Initialize(context);
        }

        public CustomPostProcessingEffects()
        {
        }

        protected override void InitializeCore()
        {
            base.InitializeCore();

            _brightPassShader = ToLoadAndUnload(new ImageEffectShader("BloomBrightPass"));
            _bloomDownSample = ToLoadAndUnload(new ImageEffectShader("BloomDownSample"));
            _bloomUpSample = ToLoadAndUnload(new ImageEffectShader("BloomUpSample"));
            _toneMapShader = ToLoadAndUnload(new ImageEffectShader("ToneMapASEC"));
            _histogramShader = ToLoadAndUnload(new ComputeEffectShader(Context) { ShaderSourceName = "Histogram" });
            _histogramReduceShader = ToLoadAndUnload(new ComputeEffectShader(Context) { ShaderSourceName = "HistogramReduce" });

            Bloom = new List<BloomSettings>()
            {
                new BloomSettings { Strength = 0.5f, Radius = 1.0f },
                new BloomSettings { Strength = 1.0f, Radius = 2.0f },
                new BloomSettings { Strength = 2.0f, Radius = 2.0f },
                new BloomSettings { Strength = 1.0f, Radius = 4.0f },
                new BloomSettings { Strength = 2.0f, Radius = 4.0f }
            };

            float initalMinLog = -12.0f;
            float initialMaxLog = 2.0f;

            _histogram = Buffer.Raw.New(GraphicsDevice, new uint[256], BufferFlags.ShaderResource | BufferFlags.UnorderedAccess);
            //_histogram = Buffer.New(GraphicsDevice, 256 * sizeof(uint), 0, BufferFlags.ShaderResource | BufferFlags.UnorderedAccess, PixelFormat.R32_UInt);
            _exposure = Buffer.Structured.New(
                GraphicsDevice, 
                new float[]
                {
                    2.0f, 1.0f / 2.0f, 2.0f, 0.0f,
                    initalMinLog, initialMaxLog, initialMaxLog - initalMinLog, 1.0f / (initialMaxLog - initalMinLog)
                }, 
                true);
        }

        protected override void Unload()
        {
            base.Unload();

            _exposure?.Dispose();
            _histogram?.Dispose();
        }

        public void Collect(RenderContext context)
        {
        }

        public void Draw(RenderDrawContext drawContext, RenderOutputValidator outputValidator, Texture[] inputs, Texture inputDepthStencil, Texture outputTarget)
        {
            var colorIndex = outputValidator.Find<ColorTargetSemantic>();
            if (colorIndex < 0)
                return;

            SetInput(0, inputs[colorIndex]);
            SetInput(1, inputDepthStencil);

            var normalsIndex = outputValidator.Find<NormalTargetSemantic>();
            if (normalsIndex >= 0)
            {
                SetInput(2, inputs[normalsIndex]);
            }

            var specularRoughnessIndex = outputValidator.Find<SpecularColorRoughnessTargetSemantic>();
            if (specularRoughnessIndex >= 0)
            {
                SetInput(3, inputs[specularRoughnessIndex]);
            }

            SetOutput(outputTarget);
            Draw(drawContext);
        }

        protected override void DrawCore(RenderDrawContext context)
        {
            var input = GetInput(0);
            var output = GetOutput(0);
            if (input == null || output == null)
            {
                return;
            }

            if (input == output)
            {
                var newInput = NewScopedRenderTarget2D(input.Description);
                context.CommandList.Copy(input, newInput);
                input = newInput;
            }

            var currentInput = input;

            context.CommandList.ClearReadWrite(_histogram, UInt4.Zero);

            // Calculate histogram
            _histogramShader.ThreadNumbers = new Int3(16, 16, 1);
            _histogramShader.ThreadGroupCounts = new Int3((int)Math.Ceiling(currentInput.Width / (float)16), (int)Math.Ceiling(currentInput.Height / (float)16), 1);
            _histogramShader.Parameters.Set(HistogramKeys.ColorInput, currentInput);
            _histogramShader.Parameters.Set(HistogramKeys.HistogramBuffer, _histogram);
            _histogramShader.Parameters.Set(HistogramKeys.Exposure, _exposure);
            _histogramShader.Draw(context);

            // Reduce histogram to get average luminance
            _histogramReduceShader.ThreadNumbers = new Int3(256, 1, 1);
            _histogramReduceShader.ThreadGroupCounts = new Int3(1, 1, 1);
            _histogramReduceShader.Parameters.Set(HistogramReduceKeys.Histogram, _histogram);
            _histogramReduceShader.Parameters.Set(HistogramReduceKeys.Exposure, _exposure);
            _histogramReduceShader.Parameters.Set(HistogramReduceKeys.PixelCount, (uint)(currentInput.Width * currentInput.Height));
            _histogramReduceShader.Parameters.Set(HistogramReduceKeys.Tau, ExposureSettings.AdaptionSpeed);
            _histogramReduceShader.Parameters.Set(HistogramReduceKeys.AutoKey, ExposureSettings.AutoKey);
            _histogramReduceShader.Parameters.Set(HistogramReduceKeys.TargetLuminance, ExposureSettings.Key);
            _histogramReduceShader.Parameters.Set(HistogramReduceKeys.MinExposure, ExposureSettings.MinExposure);
            _histogramReduceShader.Parameters.Set(HistogramReduceKeys.MaxExposure, ExposureSettings.MaxExposure);
            _histogramReduceShader.Parameters.Set(HistogramReduceKeys.TimeDelta, (float)context.RenderContext.Time.Elapsed.TotalSeconds);
            _histogramReduceShader.Draw(context);

            // Bright Pass
            var brightPassTexture = NewScopedRenderTarget2D(currentInput.Width, currentInput.Height, currentInput.Format, 1);
            _brightPassShader.Parameters.Set(ExposureCommonKeys.Exposure, _exposure);
            _brightPassShader.SetInput(0, currentInput);
            _brightPassShader.SetOutput(brightPassTexture);
            _brightPassShader.Draw(context, "Bright pass");

            // Bloom down sample
            var bloomInput = brightPassTexture;
            _bloomRenderTargets.Add(bloomInput);
            for (var i = 0; i < Bloom.Count; i++)
            {
                var bloomRenderTarget = NewScopedRenderTarget2D(bloomInput.Width / 2, bloomInput.Height / 2, bloomInput.Format, 1);
                _bloomRenderTargets.Add(bloomRenderTarget);

                _bloomDownSample.SetInput(0, bloomInput);
                _bloomDownSample.SetOutput(bloomRenderTarget);
                _bloomDownSample.Draw(context, $"Bloom Down Sample {i}");

                bloomInput = bloomRenderTarget;
            }

            // Up sample
            for (var i = Bloom.Count - 1; i >= 0; i--)
            {
                var bloomRenderTarget = _bloomRenderTargets[i];

                _bloomUpSample.SetInput(0, bloomInput);
                _bloomUpSample.SetOutput(bloomRenderTarget);

                _bloomUpSample.Parameters.Set(BloomUpSampleKeys.Strength, Bloom[i].Strength);
                _bloomUpSample.Parameters.Set(BloomUpSampleKeys.Radius, Bloom[i].Radius);

                _bloomUpSample.Draw(context, $"Bloom up Sample {i}");

                bloomInput = bloomRenderTarget;
            }

            _bloomRenderTargets.Clear();

            var bloomOutput = bloomInput;

            // Tone map
            _toneMapShader.Parameters.Set(ExposureCommonKeys.Exposure, _exposure);
            _toneMapShader.SetInput(0, currentInput);
            _toneMapShader.SetInput(1, bloomOutput);
            _toneMapShader.SetOutput(output);
            _toneMapShader.Draw(context, "ToneMap ASEC");
        }
    }
}
