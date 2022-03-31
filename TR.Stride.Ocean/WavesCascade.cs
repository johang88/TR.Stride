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

        private TextureAndMips _derivatives;
        private TextureAndMips _turbulence;

        public Texture Displacement { get; set; }
        public Texture Derivatives => _derivatives.Texture;
        public Texture Turbulence => _turbulence.Texture;

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

        private float _lambda;

        public WavesCascade(GraphicsDevice graphicsDevice, int size, FastFourierTransform fft, Texture gaussianNoise)
        {
            _size = size;
            _fft = fft ?? throw new ArgumentNullException(nameof(fft));
            GaussianNoise = gaussianNoise;

            InitialSpectrum = CreateRenderTexture(size, PixelFormat.R32G32B32A32_Float);
            PrecomputedData = CreateRenderTexture(size, PixelFormat.R32G32B32A32_Float);
            Displacement = CreateRenderTexture(size, PixelFormat.R32G32B32A32_Float);
            _derivatives = new TextureAndMips(CreateRenderTextureMips(size, PixelFormat.R32G32B32A32_Float));
            _turbulence = new TextureAndMips(CreateRenderTextureMips(size, PixelFormat.R32G32B32A32_Float));

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
            using var profileContext = context.QueryManager.BeginProfile(Color4.White, ProfilingKeys.CalculateInitials);

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
            using var profileContext = context.QueryManager.BeginProfile(Color4.White, ProfilingKeys.CalculateWavesAtTime);

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

            // Generate mip maps
            ResetState();
            
            GenerateMipsMaps(_derivatives);
            GenerateMipsMaps(_turbulence);

            ResetState();

            commandList.ResourceBarrierTransition(Displacement, GraphicsResourceState.PixelShaderResource);
            commandList.ResourceBarrierTransition(Derivatives, GraphicsResourceState.PixelShaderResource);
            commandList.ResourceBarrierTransition(Turbulence, GraphicsResourceState.PixelShaderResource);

            void GenerateMipsMaps(TextureAndMips texture)
            {
                var mipLevels = texture.Texture.MipLevels;
                for (var topMip = 0; topMip < mipLevels - 1;)
                {
                    var SrcWidth = texture.Texture.Width >> topMip;
                    var SrcHeight = texture.Texture.Height >> topMip;
                    var DstWidth = SrcWidth >> 1;
                    var DstHeight = SrcHeight >> 1;

                    var numMips = 4;
                    if (topMip + numMips > mipLevels)
                        numMips = mipLevels - topMip;

                    generateMipsShader.Parameters.Set(OceanGenerateMipsKeys.SrcMip, texture.Mips[0]);
                    generateMipsShader.Parameters.Set(OceanGenerateMipsKeys.OutMip1, texture.Mips[topMip + 1]);
                    generateMipsShader.Parameters.Set(OceanGenerateMipsKeys.OutMip2, texture.Mips[topMip + 2]);
                    generateMipsShader.Parameters.Set(OceanGenerateMipsKeys.OutMip3, texture.Mips[topMip + 3]);
                    generateMipsShader.Parameters.Set(OceanGenerateMipsKeys.OutMip4, texture.Mips[topMip + 4]);

                    generateMipsShader.Parameters.Set(OceanGenerateMipsKeys.SrcMipLevel, (uint)topMip);
                    generateMipsShader.Parameters.Set(OceanGenerateMipsKeys.NumMipLevels, (uint)numMips);
                    generateMipsShader.Parameters.Set(OceanGenerateMipsKeys.TexelSize, new Vector2(1.0f / DstWidth, 1.0f / DstHeight));
                    
                    generateMipsShader.ThreadGroupCounts = new Int3(DstWidth, DstHeight, 1);
                    generateMipsShader.ThreadNumbers = new Int3(LOCAL_WORK_GROUPS_X, LOCAL_WORK_GROUPS_Y, 1);
                    generateMipsShader.Draw(context);

                    topMip += numMips;
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
            _derivatives.Dispose();
            _turbulence.Dispose();
        }

        public class TextureAndMips : IDisposable
        {
            public Texture Texture;
            public Texture[] Mips;

            public TextureAndMips(Texture texture)
            {
                Texture = texture;

                Mips = new Texture[Texture.MipLevels];
                for (var i = 0; i < Texture.MipLevels; i++)
                {
                    Mips[i] = Texture.ToTextureView(ViewType.MipBand, 0, i);
                }
            }

            public void Dispose()
            {
                foreach (var mip in Mips)
                {
                    mip.Dispose();
                }

                Texture.Dispose();
            }
        }
    }
}
