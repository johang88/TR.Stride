using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Ocean
{
    [DataContract, DefaultEntityComponentRenderer(typeof(OceanEntityProcessor))]
    public class OceanComponent : ScriptComponent
    {
        [DataMember, DefaultValue(256)] public int Size { get; set; } = 256;
        [DataMember] public WavesSettings WavesSettings { get; set; } = new();

        [DataMember, DefaultValue(250)] public float LengthScale0 { get; set; } = 250;
        [DataMember, DefaultValue(17)] public float LengthScale1 { get; set; } = 17;
        [DataMember, DefaultValue(5)] public float LengthScale2 { get; set; } = 5;
        [DataMember] public bool AlwaysRecalculateInitials { get; set; } = false;

        [DataMember] public IOceanMaterial Material { get; set; } = new DefaultOceanMaterial();
        [DataMember] public IOceanMesh Mesh { get; set; } = new DefaultOceanMesh();

        /// <summary>
        /// Delay in frames used for gpu image readback for the displacement map,
        /// lower values can cause stalling while higher are inefficient
        /// </summary>
        [DataMember, DefaultValue(16)] public int DisplacmentReadBackFrameDelay { get; set; } = 16;

        /// <summary>
        /// Displacemnet vectors for lowest cascade, set through GPU readback
        /// this means there is a delay before these are updated and it can be NULL
        /// 
        /// It's preferable to use TryGetWaterHeight to sample
        /// </summary>
        [DataMemberIgnore] public Vector4[] Displacement { get; set; }

        private Vector3 GetWaterDisplacement(Vector3 position)
        {
            var u = position.X / LengthScale0;
            var v = position.Z / LengthScale0;

            var gx = u * Size;
            var gy = v * Size;

            var gxi = (int)gx;
            var gyi = (int)gy;

            var c00 = SampleRepeat(Displacement, Size, Size, gyi + 0, gxi + 0);
            var c10 = SampleRepeat(Displacement, Size, Size, gyi + 0, gxi + 1);
            var c01 = SampleRepeat(Displacement, Size, Size, gyi + 1, gxi + 0);
            var c11 = SampleRepeat(Displacement, Size, Size, gyi + 1, gxi + 1);

            return Bilinear(gx - gxi, gy - gyi, c00, c10, c01, c11).XYZ();

            static Vector4 SampleRepeat(Vector4[] source, int w, int h, int x, int y)
            {
                x = x % w;
                y = y & h;

                return source[y * w + x];
            }

            static Vector4 Bilinear(float tx, float ty, Vector4 c00, Vector4 c10, Vector4 c01, Vector4 c11)
            {
                return (1 - tx) * (1 - ty) * c00 +
                    tx * (1 - ty) * c10 +
                    (1 - tx) * ty * c01 +
                    tx * ty * c11;
            }
        }

        public bool TryGetWaterDisplacement(Vector3 position, out Vector3 displacement)
        {
            displacement = default;

            if (Displacement == null)
                return false;

            displacement = GetWaterDisplacement(position);

            return true;
        }

        public bool TryGetWaterHeight(Vector3 position, out float height)
        {
            height = default;

            if (Displacement == null)
                return false;

            var displacement = GetWaterDisplacement(position);
            displacement = GetWaterDisplacement(position - displacement);
            displacement = GetWaterDisplacement(position - displacement);

            height = GetWaterDisplacement(position - displacement).Y;

            return true;
        }
    }
}
