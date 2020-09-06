using Stride.Core.Annotations;
using Stride.Engine;
using Stride.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TR.Stride.Atmosphere
{
    public class AtmosphereEntityProcessor : EntityProcessor<AtmosphereComponent, AtmosphereRenderObject>, IEntityComponentRenderProcessor
    {
        public VisibilityGroup VisibilityGroup { get; set; }
        private AtmosphereRenderObject _activeAtmosphere;

        protected override AtmosphereRenderObject GenerateComponentData([NotNull] Entity entity, [NotNull] AtmosphereComponent component)
             => new AtmosphereRenderObject { RenderGroup = RenderGroup.Group30, Component = component };

        protected override bool IsAssociatedDataValid([NotNull] Entity entity, [NotNull] AtmosphereComponent component, [NotNull] AtmosphereRenderObject associatedData)
            => associatedData.Component == component;

        protected override void OnEntityComponentRemoved(Entity entity, [NotNull] AtmosphereComponent component, [NotNull] AtmosphereRenderObject data)
        {
            base.OnEntityComponentRemoved(entity, component, data);

            VisibilityGroup.RenderObjects.Remove(data);
            if (_activeAtmosphere == data)
            {
                _activeAtmosphere = null;
            }
        }

        public override void Draw(RenderContext context)
        {
            base.Draw(context);

            if (_activeAtmosphere != null)
                return;

            // Add first enabled atmosphere to render objects as we only support one atmosphere
            var first = false;
            foreach (var pair in ComponentDatas)
            {
                var component = pair.Value;
                if (component.Enabled && !first)
                {
                    first = true;

                    VisibilityGroup.RenderObjects.Add(pair.Value);
                    _activeAtmosphere = pair.Value;
                }
            }
        }
    }
}
