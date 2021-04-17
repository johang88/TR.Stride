using Stride.Engine;
using Stride.Core.Mathematics;

namespace TR.Stride
{
    public static class CameraExtensions
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
