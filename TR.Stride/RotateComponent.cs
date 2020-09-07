using Stride.Engine;
using System;
using System.Collections.Generic;
using System.Text;
using Stride.Core.Mathematics;
using Stride.Core;
using Stride.Input;

namespace TR.Stride
{
    public class RotateComponent : SyncScript
    {
        [DataMember] public Vector3 Axis { get; set; } = Vector3.UnitY;
        [DataMember] public float Speed { get; set; } = 1.0f;

        public override void Update()
        {
            var dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;

            Entity.Transform.Rotation *= Quaternion.RotationAxis(Axis, Speed * dt);

            DebugText.Print($"[I/O] Sun rotation speed: {Speed}", new Int2(20, 680));

            if (Input.IsKeyDown(Keys.I))
            {
                Speed += 0.1f * dt;
            }
            else if (Input.IsKeyDown(Keys.O))
            {
                Speed -= 0.1f * dt;
            }
        }
    }
}
