using Stride.Core;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Ocean
{
    /// <summary>
    /// Ocean mesh using a user defined model component
    /// </summary>
    [DataContract(nameof(ModelOceanMesh))]
    public class ModelOceanMesh : IOceanMesh
    {
        private OceanComponent _ocean;
        private Material[] _materials;
        private bool _isDirty = false;

        [DataMember] public ModelComponent Model { get; set; }

        public void SetOcean(OceanComponent ocean, Material[] materials)
        {
            _ocean = ocean;
            _materials = materials;
            _isDirty = true;
        }

        public void Update(GraphicsDevice graphicsDevice, CameraComponent camera)
        {
            if (_materials == null || _ocean == null || Model == null || Model.Model == null)
                return;

            if (_isDirty)
            {
                Model.Model.Materials.Clear();
                foreach (var material in _materials)
                {
                    Model.Model.Materials.Add(new MaterialInstance(material)
                    {
                        IsShadowCaster = false
                    });
                }
            }
        }
    }
}
