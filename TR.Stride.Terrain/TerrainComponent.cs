using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Graphics;
using Stride.Physics;
using Stride.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;


namespace TR.Stride.Terrain
{
    /// <summary>
    /// Terrain data used by the TerrainProcessor, just attach to an entity and you are good to go.
    /// The terrain processor uses a hidden ModelComponent in order to support picking in the editor,
    /// its important that the Entity with the TerrainComponent does not contain an existing ModelComponent
    /// as only one can exist per Entity.
    /// 
    /// Also note that the generated mesh is offset compared HeightfieldCollider so one of them has to be offset (0.5, 0, 0.5)
    /// </summary>
    [DataContract(nameof(TerrainComponent))]
    [Display("Terrain", Expand = ExpandRule.Once)]
    [DefaultEntityComponentRenderer(typeof(TerrainProcessor))]
    public class TerrainComponent : ScriptComponent
    {
        [DataMember(0)]
        public Material Material { get; set; }

        /// <summary>
        /// Height map asset, currently only short conversion type is supported. Make sure this is correctly set on the asset or 
        /// you will get a null exception.
        /// </summary>
        [DataMember(10)]
        public Heightmap Heightmap { get; set; }

        [DataMember(20)]
        public float Size { get; set; }

        [DataMember(30)]
        public bool CastShadows { get; set; }

        [DataMemberIgnore]
        internal bool ShouldRecreateMesh { get; set; }

        public void RecreateMesh()
        {
            ShouldRecreateMesh = true;
        }

        public float GetHeightAt(float x, float z)
        {
            // Origin is located at center of terrain mesh so we offset
            x += Size / 2.0f;
            z += Size / 2.0f;

            // And scale
            x /= Size;
            z /= Size;

            x *= Heightmap.Size.X;
            z *= Heightmap.Size.Y;

            if (x < 0.0f || x >= Size || z < 0 || z >= Size)
                return -1;

            int xi = (int)x, zi = (int)z;
            float xpct = x - xi, zpct = z - zi;

            if (xi == Size - 1)
            {
                --xi;
                xpct = 1.0f;
            }
            if (zi == Size - 1)
            {
                --zi;
                zpct = 1.0f;
            }

            var heights = new float[]
            {
                Heightmap.GetHeightAt(xi, zi),
                Heightmap.GetHeightAt(xi, zi + 1),
                Heightmap.GetHeightAt(xi + 1, zi),
                Heightmap.GetHeightAt(xi + 1, zi + 1)
            };

            var w = new float[]
            {
                (1.0f - xpct) * (1.0f - zpct),
                (1.0f - xpct) * zpct,
                xpct * (1.0f - zpct),
                xpct * zpct
            };

            var height = w[0] * heights[0] + w[1] * heights[1] + w[2] * heights[2] + w[3] * heights[3];

            return height;
        }

        public Vector3 GetNormalAt(float x, float z)
        {
            var flip = 1;
            var here = new Vector3(x, GetHeightAt(x, z), z);
            var left = new Vector3(x - 1.0f, GetHeightAt(x - 1.0f, z), z);
            var down = new Vector3(x, GetHeightAt(x, z + 1.0f), z + 1.0f);

            if (left.X < 0.0f)
            {
                flip *= -1;
                left = new Vector3(x + 1.0f, GetHeightAt(x + 1.0f, z), z);
            }

            if (down.Z >= Size - 1)
            {
                flip *= -1;
                down = new Vector3(x, GetHeightAt(x, z - 1.0f), z - 1.0f);
            }

            left -= here;
            down -= here;

            var normal = Vector3.Cross(left, down) * flip;
            normal.Normalize();

            return normal;
        }
    }
}
