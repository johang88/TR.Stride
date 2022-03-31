using Stride.Core.Annotations;
using Stride.Engine;
using Stride.Games;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Terrain
{
    public class ProceduralHeightMapProcessor : EntityProcessor<ProceduralHeightMapComponent>
    {
        public ProceduralHeightMapProcessor()  : base()
        {
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            foreach (var componentData in ComponentDatas)
            {
                var component = componentData.Key;
                if (component.IsDirty)
                {
                    //UpdateHeightmap(component);
                    component.IsDirty = false;
                }
            }
        }

        private void UpdateHeightmap(ProceduralHeightMapComponent component)
        {
            var terrainComponent = component.Terrain;
            if (terrainComponent == null)
                return;

            var heightmap = terrainComponent.Heightmap;
            if (heightmap == null)
                return;

            Generation.Noise.GenerateNoiseMap(heightmap, component.Scale);
            terrainComponent.RecreateMesh();
        }
    }
}
