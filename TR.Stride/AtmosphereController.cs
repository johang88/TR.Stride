using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TR.Stride.Atmosphere;

namespace TR.Stride
{
    public class AtmosphereController : SyncScript
    {
        public override void Update()
        {
            var atmosphere = Entity.Get<AtmosphereComponent>();

            var compositor = SceneSystem.GraphicsCompositor;
            var atmospehereRenderFeature = (AtmosphereRenderFeature)compositor.RenderFeatures.First(x => x is AtmosphereRenderFeature);

            DebugText.Print($"[J] FastSky: {atmospehereRenderFeature.FastSky}", new Int2(20, 600));
            DebugText.Print($"[K] FastAerialPerspectiveEnabled: {atmospehereRenderFeature.FastAerialPerspectiveEnabled}", new Int2(20, 620));
            DebugText.Print($"[L] RenderSunDisk: {atmosphere.RenderSunDisk}", new Int2(20, 640));

            if (Input.IsKeyPressed(Keys.J))
            {
                atmospehereRenderFeature.FastSky = !atmospehereRenderFeature.FastSky;
            }
            if (Input.IsKeyPressed(Keys.K))
            {
                atmospehereRenderFeature.FastAerialPerspectiveEnabled = !atmospehereRenderFeature.FastAerialPerspectiveEnabled;
            }
            if (Input.IsKeyPressed(Keys.L))
            {
                atmosphere.RenderSunDisk = !atmosphere.RenderSunDisk;
            }
        }
    }
}
