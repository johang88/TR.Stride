using System;
using System.Collections.Generic;
using System.Text;
using Stride.Core;
using Stride.Rendering.Lights;

namespace TR.Stride.Atmosphere
{
    [DataContract(nameof(AtmosphereLightDirectional))]
    [Display("Sun")]
    public class AtmosphereLightDirectional : LightDirectional
    {
        public AtmosphereComponent Atmosphere { get; set; }
    }
}
