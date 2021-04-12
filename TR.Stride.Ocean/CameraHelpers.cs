using Stride.Engine;
using Stride.Core.Mathematics;

namespace TR.Stride.Ocean
{
    public static class CameraHelpers
    {
        public static Vector3 GetWorldPosition(this CameraComponent camera)
        {
            var viewMatrix = camera.ViewMatrix;
            viewMatrix.Invert();

            var cameraPosition = viewMatrix.TranslationVector;

            return cameraPosition;
        }
    }
}
