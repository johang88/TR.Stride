using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.ComputeEffect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Ocean
{
    public class FastFourierTransformShaders : IDisposable
    {
        public ComputeEffectShader PrecomputeTwiddleFactorsAndInputIndices { get; private set; }
        public ComputeEffectShader HorizontalStepFFT { get; private set; }
        public ComputeEffectShader VerticalStepFFT { get; private set; }
        public ComputeEffectShader HorizontalStepInverseFFT { get; private set; }
        public ComputeEffectShader VerticalStepInverseFFT { get; private set; }
        public ComputeEffectShader Scale { get; private set; }
        public ComputeEffectShader Permute { get; private set; }

        public FastFourierTransformShaders(RenderContext context)
        {
            PrecomputeTwiddleFactorsAndInputIndices = new ComputeEffectShader(context) { ShaderSourceName = "OceanPrecomputeTwiddleFactorsAndInputIndices" };
            HorizontalStepFFT = new ComputeEffectShader(context) { ShaderSourceName = "OceanHorizontalStepFFT" };
            VerticalStepFFT = new ComputeEffectShader(context) { ShaderSourceName = "OceanVerticalStepFFT" };
            HorizontalStepInverseFFT = new ComputeEffectShader(context) { ShaderSourceName = "OceanHorizontalStepInverseFFT" };
            VerticalStepInverseFFT = new ComputeEffectShader(context) { ShaderSourceName = "OceanVerticalStepInverseFFT" };
            Scale = new ComputeEffectShader(context) { ShaderSourceName = "OceanScale" };
            Permute = new ComputeEffectShader(context) { ShaderSourceName = "OceanPermute" };
        }

        public void Dispose()
        {
            PrecomputeTwiddleFactorsAndInputIndices?.Dispose();
            HorizontalStepFFT?.Dispose();
            VerticalStepFFT?.Dispose();
            HorizontalStepInverseFFT?.Dispose();
            VerticalStepInverseFFT?.Dispose();
            Scale?.Dispose();
            Permute?.Dispose();
        }
    }

    public class FastFourierTransform : IDisposable
    {
        private const int LOCAL_WORK_GROUPS_X = 8;
        private const int LOCAL_WORK_GROUPS_Y = 8;

        private readonly int _size;
        private readonly FastFourierTransformShaders _shaders;
        private readonly Texture _precomputedData;

        public FastFourierTransform(RenderDrawContext context, int size, FastFourierTransformShaders shaders)
        {
            _size = size;
            _shaders = shaders;
            _precomputedData = PrecomputeTwiddleFactorsAndInputIndices(context);
        }

        public void Dispose()
        {
            _precomputedData?.Dispose();
        }

        private Texture PrecomputeTwiddleFactorsAndInputIndices(RenderDrawContext context)
        {
            var logSize = (int)MathF.Log(_size, 2);
            var texture = Texture.New2D(context.GraphicsDevice, logSize, _size, PixelFormat.R32G32B32A32_Float, TextureFlags.ShaderResource | TextureFlags.UnorderedAccess);

            _shaders.PrecomputeTwiddleFactorsAndInputIndices.Parameters.Set(OceanFastFourierTransformBaseKeys.Size, (uint)_size);
            _shaders.PrecomputeTwiddleFactorsAndInputIndices.Parameters.Set(OceanPrecomputeTwiddleFactorsAndInputIndicesKeys.PrecomputeBuffer, texture);

            _shaders.PrecomputeTwiddleFactorsAndInputIndices.ThreadGroupCounts = new Int3(logSize, _size / 2 / LOCAL_WORK_GROUPS_Y, 1);
            _shaders.PrecomputeTwiddleFactorsAndInputIndices.ThreadNumbers = new Int3(1, LOCAL_WORK_GROUPS_Y, 1);
            _shaders.PrecomputeTwiddleFactorsAndInputIndices.Draw(context);

            return texture;
        }

        internal void IFFT2D(RenderDrawContext context, Texture input, Texture buffer, bool outputToInput = false, bool scale = true, bool permute = false)
        {
            var logSize = (int)MathF.Log(_size, 2);
            var pingPong = false;

            // Horizontal
            _shaders.HorizontalStepInverseFFT.Parameters.Set(OceanFastFourierTransformBaseKeys.PrecomputedData, _precomputedData);
            _shaders.HorizontalStepInverseFFT.Parameters.Set(OceanFastFourierTransformBaseKeys.Buffer0, input);
            _shaders.HorizontalStepInverseFFT.Parameters.Set(OceanFastFourierTransformBaseKeys.Buffer1, buffer);

            for (var i = 0; i < logSize; i++)
            {
                pingPong = !pingPong;

                _shaders.HorizontalStepInverseFFT.Parameters.Set(OceanFastFourierTransformBaseKeys.Step, (uint)i);
                _shaders.HorizontalStepInverseFFT.Parameters.Set(OceanFastFourierTransformBaseKeys.PingPong, pingPong);

                _shaders.HorizontalStepInverseFFT.ThreadGroupCounts = new Int3(_size / LOCAL_WORK_GROUPS_X, _size / LOCAL_WORK_GROUPS_Y, 1);
                _shaders.HorizontalStepInverseFFT.ThreadNumbers = new Int3(LOCAL_WORK_GROUPS_X, LOCAL_WORK_GROUPS_Y, 1);
                _shaders.HorizontalStepInverseFFT.Draw(context);
            }

            // Vertical
            _shaders.VerticalStepInverseFFT.Parameters.Set(OceanFastFourierTransformBaseKeys.PrecomputedData, _precomputedData);
            _shaders.VerticalStepInverseFFT.Parameters.Set(OceanFastFourierTransformBaseKeys.Buffer0, input);
            _shaders.VerticalStepInverseFFT.Parameters.Set(OceanFastFourierTransformBaseKeys.Buffer1, buffer);

            for (var i = 0; i < logSize; i++)
            {
                pingPong = !pingPong;

                _shaders.VerticalStepInverseFFT.Parameters.Set(OceanFastFourierTransformBaseKeys.Step, (uint)i);
                _shaders.VerticalStepInverseFFT.Parameters.Set(OceanFastFourierTransformBaseKeys.PingPong, pingPong);

                _shaders.VerticalStepInverseFFT.ThreadGroupCounts = new Int3(_size / LOCAL_WORK_GROUPS_X, _size / LOCAL_WORK_GROUPS_Y, 1);
                _shaders.VerticalStepInverseFFT.ThreadNumbers = new Int3(LOCAL_WORK_GROUPS_X, LOCAL_WORK_GROUPS_Y, 1);
                _shaders.VerticalStepInverseFFT.Draw(context);
            }

            if (pingPong && outputToInput)
            {
                context.CommandList.Copy(buffer, input);
            }

            if (!pingPong && !outputToInput)
            {
                context.CommandList.Copy(input, buffer);
            }

            if (permute)
            {
                _shaders.Permute.Parameters.Set(OceanFastFourierTransformBaseKeys.Size, (uint)_size);
                _shaders.Permute.Parameters.Set(OceanFastFourierTransformBaseKeys.Buffer0, outputToInput ? input : buffer);

                _shaders.Permute.ThreadGroupCounts = new Int3(_size / LOCAL_WORK_GROUPS_X, _size / LOCAL_WORK_GROUPS_Y, 1);
                _shaders.Permute.ThreadNumbers = new Int3(LOCAL_WORK_GROUPS_X, LOCAL_WORK_GROUPS_Y, 1);
                _shaders.Permute.Draw(context);
            }

            if (scale)
            {
                _shaders.Scale.Parameters.Set(OceanFastFourierTransformBaseKeys.Size, (uint)_size);
                _shaders.Scale.Parameters.Set(OceanFastFourierTransformBaseKeys.Buffer0, outputToInput ? input : buffer);

                _shaders.Scale.ThreadGroupCounts = new Int3(_size / LOCAL_WORK_GROUPS_X, _size / LOCAL_WORK_GROUPS_Y, 1);
                _shaders.Scale.ThreadNumbers = new Int3(LOCAL_WORK_GROUPS_X, LOCAL_WORK_GROUPS_Y, 1);
                _shaders.Scale.Draw(context);
            }
        }
    }
}
