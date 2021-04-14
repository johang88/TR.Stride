using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Ocean
{
    public struct SpectrumSettings
    {
        public float Scale;
        public float Angle;
        public float SpreadBlend;
        public float Swell;
        public float Alpha;
        public float PeakOmega;
        public float Gamma;
        public float ShortWavesFade;
    }

    [DataContract]
    public struct DisplaySpectrumSettings
    {
        [DataMember, DataMemberRange(0, 1)] public float Scale;
        [DataMember] public float WindSpeed;
        [DataMember] public float WindDirection;
        [DataMember] public float Fetch;
        [DataMember, DataMemberRange(0, 1)] public float SpreadBlend;
        [DataMember, DataMemberRange(0, 1)] public float Swell;
        [DataMember] public float PeakEnhancement;
        [DataMember] public float ShortWavesFade;
    }

    [DataContract]
    public class WavesSettings
    {
        [DataMember, DefaultValue(9.81f)] public float G { get; set; } = 9.81f;
        [DataMember, DefaultValue(500.0f)] public float Depth { get; set; } = 500.0f;
        [DataMember, DataMemberRange(0, 1), DefaultValue(1.0f)] public float Lambda { get; set; } = 1.0f;
        [DataMember]
        public DisplaySpectrumSettings Local { get; set; } = new()
        {
            Scale = 1.0f,
            WindSpeed = 0.5f,
            WindDirection = 90,
            Fetch = 100000,
            SpreadBlend = 1.0f,
            Swell = 0.2f,
            PeakEnhancement = 3.3f,
            ShortWavesFade = 0.01f
        };

        [DataMember]
        public DisplaySpectrumSettings Swell { get; set; } = new()
        {
            Scale = 0.0f,
            WindSpeed = 1.0f,
            WindDirection = 0.0f,
            Fetch = 300000.0f,
            SpreadBlend = 1.0f,
            Swell = 1.0f,
            PeakEnhancement = 3.3f,
            ShortWavesFade = 0.01f
        };

        internal readonly SpectrumSettings[] Spectrums = new SpectrumSettings[2];

        public void UpdateShaderParameters()
        {
            FillSettingsStruct(Local, ref Spectrums[0]);
            FillSettingsStruct(Swell, ref Spectrums[1]);
        }

        private void FillSettingsStruct(DisplaySpectrumSettings display, ref SpectrumSettings settings)
        {
            settings.Scale = display.Scale;
            settings.Angle = display.WindDirection / 180 * MathF.PI;
            settings.SpreadBlend = display.SpreadBlend;
            settings.Swell = MathUtil.Clamp(display.Swell, 0.01f, 1);
            settings.Alpha = JonswapAlpha(G, display.Fetch, display.WindSpeed);
            settings.PeakOmega = JonswapPeakFrequency(G, display.Fetch, display.WindSpeed);
            settings.Gamma = display.PeakEnhancement;
            settings.ShortWavesFade = display.ShortWavesFade;
        }

        private float JonswapAlpha(float g, float fetch, float windSpeed)
            => 0.076f * MathF.Pow(g * fetch / windSpeed / windSpeed, -0.22f);

        private float JonswapPeakFrequency(float g, float fetch, float windSpeed)
            => 22 * MathF.Pow(windSpeed * fetch / g / g, -0.33f);
    }
}
