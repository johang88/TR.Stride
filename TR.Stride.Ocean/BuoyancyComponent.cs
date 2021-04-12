using Stride.Core;
using Stride.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Ocean
{
    [DataContract]
    public class BuoyancyComponent : SyncScript
    {
        [DataMember] public OceanComponent Ocean { get; set; }

        public override void Update()
        {
            if (Ocean == null)
                return;

            var position = Entity.Transform.Position;
            if (Ocean.TryGetWaterHeight(position, out var waterHeight))
            {
                Entity.Transform.Position.Y = waterHeight;
            }
        }
    }
}
