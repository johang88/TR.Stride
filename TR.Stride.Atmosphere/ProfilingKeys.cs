using Stride.Core.Diagnostics;
using System;
using System.Collections.Generic;
using System.Text;

namespace TR.Stride.Atmosphere
{
    static class ProfilingKeys
    {
        public static readonly ProfilingKey Atmosphere = new ProfilingKey("Atmosphere");
        public static readonly ProfilingKey TransmittanceLut = new ProfilingKey(Atmosphere, "Transmittance LUT");
        public static readonly ProfilingKey SkyViewLut = new ProfilingKey(Atmosphere, "Sky view LUT");
        public static readonly ProfilingKey ScatteringCameraVolume = new ProfilingKey(Atmosphere, "Scattering Camera Volume");
        public static readonly ProfilingKey MultiScatteringTexture = new ProfilingKey(Atmosphere, "MultiScattering Texture");
        public static readonly ProfilingKey RayMarching = new ProfilingKey(Atmosphere, "Ray marching");
        public static readonly ProfilingKey CubeMap = new ProfilingKey(Atmosphere, "Cube map");
        public static readonly ProfilingKey CubeMapPreFilter = new ProfilingKey(Atmosphere, "Cube map Pre Filter");
    }
}
