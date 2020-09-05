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

        protected override AtmosphereRenderObject GenerateComponentData([NotNull] Entity entity, [NotNull] AtmosphereComponent component)
             => new AtmosphereRenderObject { RenderGroup = RenderGroup.Group30, Component = component };

        protected override bool IsAssociatedDataValid([NotNull] Entity entity, [NotNull] AtmosphereComponent component, [NotNull] AtmosphereRenderObject associatedData)
            => associatedData.Component == component;

        public override void Draw(RenderContext context)
        {
            base.Draw(context);

            foreach (var pair in ComponentDatas)
            {
                var component = pair.Value;

                if (!component.Enabled && VisibilityGroup.RenderObjects.Contains(pair.Value))
                {
                    VisibilityGroup.RenderObjects.Remove(pair.Value);
                }
                else if (component.Enabled && !VisibilityGroup.RenderObjects.Contains(pair.Value))
                {
                    VisibilityGroup.RenderObjects.Add(pair.Value);
                }
            }
        }
    }
}
