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
    public class WavesCascade : IDisposable
    {
        private const int LOCAL_WORK_GROUPS_X = 8;
        private const int LOCAL_WORK_GROUPS_Y = 8;

        public Texture Displacement { get; set; }
        public Texture Derivatives { get; set; }
        public Texture Turbulence { get; set; }

        public Texture PrecomputedData { get; set; }
        public Texture InitialSpectrum { get; set; }

        public Texture GaussianNoise { get; set; }

        private readonly FastFourierTransform _fft;
        private readonly int _size;

        private readonly Texture _buffer;
        private readonly Texture _dxDz;
        private readonly Texture _dyDxz;
        private readonly Texture _dyxDyz;
        private readonly Texture _dxxDzz;

        private readonly Texture _mipStagingTexture;

        private float _lambda;

        public WavesCascade(GraphicsDevice graphicsDevice, int size, FastFourierTransform fft, Texture gaussianNoise)
        {
            _size = size;
            _fft = fft ?? throw new ArgumentNullException(nameof(fft));
            GaussianNoise = gaussianNoise;

            InitialSpectrum = CreateRenderTexture(size, PixelFormat.R32G32B32A32_Float);
            PrecomputedData = CreateRenderTexture(size, PixelFormat.R32G32B32A32_Float);
            Displacement = CreateRenderTexture(size, PixelFormat.R32G32B32A32_Float);
            Derivatives = CreateRenderTextureMips(size, PixelFormat.R32G32B32A32_Float);
            Turbulence = CreateRenderTextureMips(size, PixelFormat.R32G32B32A32_Float);

            _mipStagingTexture = CreateRenderTextureMips(size, PixelFormat.R32G32B32A32_Float);

            _buffer = CreateRenderTexture(size, PixelFormat.R32G32_Float);
            _dxDz = CreateRenderTexture(size, PixelFormat.R32G32_Float);
            _dyDxz = CreateRenderTexture(size, PixelFormat.R32G32_Float);
            _dyxDyz = CreateRenderTexture(size, PixelFormat.R32G32_Float);
            _dxxDzz = CreateRenderTexture(size, PixelFormat.R32G32_Float);

            Texture CreateRenderTexture(int size, PixelFormat format)
                => Texture.New2D(graphicsDevice, size, size, format, TextureFlags.UnorderedAccess | TextureFlags.ShaderResource);

            Texture CreateRenderTextureMips(int size, PixelFormat format)
                => Texture.New2D(graphicsDevice, size, size, MipMapCount.Auto, format, TextureFlags.UnorderedAccess | TextureFlags.ShaderResource);
        }

        public void CalculateInitials(RenderDrawContext context, ComputeEffectShader initialSpectrumShader, ComputeEffectShader conjugatedSpectrumShader, WavesSettings wavesSettings, float lengthScale, float cutoffLow, float cutoffHigh)
        {
            _lambda = wavesSettings.Lambda;

            var commandList = context.CommandList;

            commandList.ResourceBarrierTransition(_buffer, GraphicsResourceState.UnorderedAccess);
            commandList.ResourceBarrierTransition(PrecomputedData, GraphicsResourceState.UnorderedAccess);
            commandList.ResourceBarrierTransition(GaussianNoise, GraphicsResourceState.UnorderedAccess);

            initialSpectrumShader.Parameters.Set(OceanInitialSpectrumCommonKeys.Size, (uint)_size);
            initialSpectrumShader.Parameters.Set(OceanInitialSpectrumCommonKeys.LengthScale, lengthScale);
            initialSpectrumShader.Parameters.Set(OceanInitialSpectrumCommonKeys.CutoffHigh, cutoffHigh);
            initialSpectrumShader.Parameters.Set(OceanInitialSpectrumCommonKeys.CutoffLow, cutoffLow);

            initialSpectrumShader.Parameters.Set(OceanInitialSpectrumCommonKeys.GravityAcceleration, wavesSettings.G);
            initialSpectrumShader.Parameters.Set(OceanInitialSpectrumCommonKeys.Depth, wavesSettings.Depth);
            initialSpectrumShader.Parameters.Set(OceanInitialSpectrumCommonKeys.Spectrums, wavesSettings.Spectrums);

            initialSpectrumShader.Parameters.Set(OceanInitialSpectrumCommonKeys.H0K, _buffer);
            initialSpectrumShader.Parameters.Set(OceanInitialSpectrumCommonKeys.WavesData, PrecomputedData);
            initialSpectrumShader.Parameters.Set(OceanInitialSpectrumCommonKeys.Noise, GaussianNoise);

            initialSpectrumShader.ThreadGroupCounts = new Int3(_size / LOCAL_WORK_GROUPS_X, _size / LOCAL_WORK_GROUPS_Y, 1);
            initialSpectrumShader.ThreadNumbers = new Int3(LOCAL_WORK_GROUPS_X, LOCAL_WORK_GROUPS_Y, 1);
            initialSpectrumShader.Draw(context);

            conjugatedSpectrumShader.Parameters.Set(OceanInitialSpectrumCommonKeys.H0, InitialSpectrum);
            conjugatedSpectrumShader.Parameters.Set(OceanInitialSpectrumCommonKeys.H0K, _buffer);
            conjugatedSpectrumShader.Parameters.Set(OceanInitialSpectrumCommonKeys.Size, (uint)_size);

            conjugatedSpectrumShader.ThreadGroupCounts = new Int3(_size / LOCAL_WORK_GROUPS_X, _size / LOCAL_WORK_GROUPS_Y, 1);
            conjugatedSpectrumShader.ThreadNumbers = new Int3(LOCAL_WORK_GROUPS_X, LOCAL_WORK_GROUPS_Y, 1);
            conjugatedSpectrumShader.Draw(context);
        }

        public void CalculateWavesAtTime(RenderDrawContext context, ComputeEffectShader timeDependantSpectrumShader, ComputeEffectShader fillResultTexturesShader, ComputeEffectShader generateMipsShader, float time, float deltaTime)
        {
            var commandList = context.CommandList;

            commandList.ResourceBarrierTransition(_dxDz, GraphicsResourceState.UnorderedAccess);
            commandList.ResourceBarrierTransition(_dyDxz, GraphicsResourceState.UnorderedAccess);
            commandList.ResourceBarrierTransition(_dyxDyz, GraphicsResourceState.UnorderedAccess);
            commandList.ResourceBarrierTransition(_dxxDzz, GraphicsResourceState.UnorderedAccess);
            commandList.ResourceBarrierTransition(Displacement, GraphicsResourceState.UnorderedAccess);
            commandList.ResourceBarrierTransition(Derivatives, GraphicsResourceState.UnorderedAccess);
            commandList.ResourceBarrierTransition(Turbulence, GraphicsResourceState.UnorderedAccess);

            // Calculating complex amplitudes
            timeDependantSpectrumShader.Parameters.Set(OceanTimeDependentSpectrumKeys.Dx_Dz, _dxDz);
            timeDependantSpectrumShader.Parameters.Set(OceanTimeDependentSpectrumKeys.Dy_Dxz, _dyDxz);
            timeDependantSpectrumShader.Parameters.Set(OceanTimeDependentSpectrumKeys.Dyx_Dyz, _dyxDyz);
            timeDependantSpectrumShader.Parameters.Set(OceanTimeDependentSpectrumKeys.Dxx_Dzz, _dxxDzz);
            timeDependantSpectrumShader.Parameters.Set(OceanTimeDependentSpectrumKeys.H0, InitialSpectrum);
            timeDependantSpectrumShader.Parameters.Set(OceanTimeDependentSpectrumKeys.WavesData, PrecomputedData);

            timeDependantSpectrumShader.Parameters.Set(OceanTimeDependentSpectrumKeys.Time, time);

            timeDependantSpectrumShader.ThreadGroupCounts = new Int3(_size / LOCAL_WORK_GROUPS_X, _size / LOCAL_WORK_GROUPS_Y, 1);
            timeDependantSpectrumShader.ThreadNumbers = new Int3(LOCAL_WORK_GROUPS_X, LOCAL_WORK_GROUPS_Y, 1);
            timeDependantSpectrumShader.Draw(context);

            // Calculating IFFTs of complex amplitudes
            _fft.IFFT2D(context, _dxDz, _buffer, true, false, true);
            _fft.IFFT2D(context, _dyDxz, _buffer, true, false, true);
            _fft.IFFT2D(context, _dyxDyz, _buffer, true, false, true);
            _fft.IFFT2D(context, _dxxDzz, _buffer, true, false, true);

            // Filling displacement and normals textures
            fillResultTexturesShader.Parameters.Set(OceanFillResultTexturesKeys.DeltaTime, deltaTime);
            fillResultTexturesShader.Parameters.Set(OceanFillResultTexturesKeys.Lambda, _lambda);

            fillResultTexturesShader.Parameters.Set(OceanFillResultTexturesKeys.Dx_Dz, _dxDz);
            fillResultTexturesShader.Parameters.Set(OceanFillResultTexturesKeys.Dy_Dxz, _dyDxz);
            fillResultTexturesShader.Parameters.Set(OceanFillResultTexturesKeys.Dyx_Dyz, _dyxDyz);
            fillResultTexturesShader.Parameters.Set(OceanFillResultTexturesKeys.Dxx_Dzz, _dxxDzz);
            fillResultTexturesShader.Parameters.Set(OceanFillResultTexturesKeys.Displacement, Displacement);
            fillResultTexturesShader.Parameters.Set(OceanFillResultTexturesKeys.Derivatives, Derivatives);
            fillResultTexturesShader.Parameters.Set(OceanFillResultTexturesKeys.Turbulence, Turbulence);

            fillResultTexturesShader.ThreadGroupCounts = new Int3(_size / LOCAL_WORK_GROUPS_X, _size / LOCAL_WORK_GROUPS_Y, 1);
            fillResultTexturesShader.ThreadNumbers = new Int3(LOCAL_WORK_GROUPS_X, LOCAL_WORK_GROUPS_Y, 1);
            fillResultTexturesShader.Draw(context);

            // TODO: Mipsmaps dont look great, could potentially be used by using better filtering
            // Not using them for now, uncomment to enable and switch Sample methods in OceanEmissive.sdsl
            //ResetState();

            //// Generate mip maps
            //GenerateMipsMaps(Derivatives);
            //GenerateMipsMaps(Turbulence);

            //ResetState();

            commandList.ResourceBarrierTransition(Displacement, GraphicsResourceState.PixelShaderResource);
            commandList.ResourceBarrierTransition(Derivatives, GraphicsResourceState.PixelShaderResource);
            commandList.ResourceBarrierTransition(Turbulence, GraphicsResourceState.PixelShaderResource);

            void GenerateMipsMaps(Texture texture)
            {
                for (var i = 0; i < texture.MipLevels - 1; i++)
                {
                    // Copy source mip to staging texture
                    commandList.CopyRegion(texture, i, null, _mipStagingTexture, i);

                    using var targetMip = texture.ToTextureView(ViewType.MipBand, 0, i + 1);

                    generateMipsShader.Parameters.Set(OceanGenerateMipsKeys.SrcMip, _mipStagingTexture);
                    generateMipsShader.Parameters.Set(OceanGenerateMipsKeys.OutMip, targetMip);

                    generateMipsShader.Parameters.Set(OceanGenerateMipsKeys.InvOutTexelSize, new Vector2(1.0f / targetMip.Width, 1.0f / targetMip.Height));
                    generateMipsShader.Parameters.Set(OceanGenerateMipsKeys.SrcMipIndex, (uint)i);

                    generateMipsShader.ThreadGroupCounts = new Int3(targetMip.Width / LOCAL_WORK_GROUPS_X, targetMip.Height / LOCAL_WORK_GROUPS_Y, 1);
                    generateMipsShader.ThreadNumbers = new Int3(LOCAL_WORK_GROUPS_X, LOCAL_WORK_GROUPS_Y, 1);
                    generateMipsShader.Draw(context);
                }
            }

            void ResetState()
            {
                // This is to solve an issue where child resources (texture views) bound as UAV wont be properly
                // reset when binding the parent texture as an SRV and thus resulting in the SRV potentionally failing to bind
                using (context.PushRenderTargetsAndRestore())
                    commandList.ClearState();
            }
        }

        public void Dispose()
        {
            _buffer.Dispose();
            _dxDz.Dispose();
            _dyDxz.Dispose();
            _dyxDyz.Dispose();
            _dxxDzz.Dispose();

            InitialSpectrum.Dispose();
            PrecomputedData.Dispose();
            Displacement.Dispose();
            Derivatives.Dispose();
            Turbulence.Dispose();
        }
    }
}
