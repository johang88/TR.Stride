using Stride.Rendering;
using System;
using System.Collections.Generic;
using System.Text;

namespace TR.Stride.Atmosphere
{
    public static class ParameterKeyExtensions
    {
        public static T TryComposeWith<T>(this T key, string name) where T : ParameterKey
        {
            if (name == null) return key;
            return key.ComposeWith(name);
        }
    }
}
