using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Graphics;
using Stride.Physics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace TR.Stride.Terrain
{
    /// <summary>
    /// Manages a single layer of vegetation. 
    /// Requires a model component with the desired model as well as an instancing component
    /// 
    /// Note: Instancing component should be added after the model component or game studio might crash next time you restart it.
    /// This is due to an ordering issue on initialization.
    /// </summary>
    [DataContract(nameof(TerrainVegetationComponent))]
    [Display("Terrain Vegetation", Expand = ExpandRule.Once)]
    [DefaultEntityComponentRenderer(typeof(TerrainVegetationProcessor))]
    public class TerrainVegetationComponent : StartupScript
    {
        private TerrainComponent _terrain;
        [DataMember(10)] public TerrainComponent Terrain { get { return _terrain; } set { _terrain = value; IsDirty = true; } }

        private float _density = 1.0f;
        [DataMember(50), DefaultValue(1.0f)] public float Density { get { return _density; } set { _density = value; IsDirty = true; } }

        private float _minScale = 0.5f;
        [DataMember(60), DefaultValue(0.5f)] public float MinScale { get { return _minScale; } set { _minScale = value; IsDirty = true; } }

        private float _maxScale = 1.5f;
        [DataMember(70), DefaultValue(1.5f)] public float MaxScale { get { return _maxScale; } set { _maxScale = value; IsDirty = true; } }

        private float _minSlope = 0.0f;
        [DataMember(80), DefaultValue(0.0f)] public float MinSlope { get { return _minSlope; } set { _minSlope = value; IsDirty = true; } }

        private float _maxSlope = 1.0f;
        [DataMember(90), DefaultValue(1.0f)] public float MaxSlope { get { return _maxSlope; } set { _maxSlope = value; IsDirty = true; } }

        private float _minHeight = 0.0f;
        [DataMember(95), DefaultValue(1.0f)] public float MinHeight { get { return _minHeight; } set { _minHeight = value; IsDirty = true; } }

        private float _maxHeight = 1024.0f;
        [DataMember(95), DefaultValue(1.0f)] public float MaxHeight { get { return _maxHeight; } set { _maxHeight = value; IsDirty = true; } }

        private int _seed;
        [DataMember(100)] public int Seed { get { return _seed; } set { _seed = value; IsDirty = true; } }

        [DataMember(120), DefaultValue(64.0f)]
        public float ViewDistance { get; set; } = 64.0f;

        [DataMember(130), DefaultValue(true)]
        public bool UseDistanceScaling { get; set; } = true;

        private bool _rotateToTerrainNormal = true;
        [DataMember(140), DefaultValue(true)]
        public bool RotateToTerrainNormal { get => _rotateToTerrainNormal; set { _rotateToTerrainNormal = value; IsDirty = true; } }

        private int _pageSize = 16;
        [DataMember(150), DefaultValue(16)]
        public int PageSize { get => _pageSize; set { _pageSize = value; IsDirty = true; } }

        [DataMemberIgnore] public bool IsDirty { get; internal set; }
    }
}
