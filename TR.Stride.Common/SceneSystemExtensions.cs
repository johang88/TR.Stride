using Stride.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride
{
    public static class SceneSystemExtensions
    {
        /// <summary>
        /// Try to get the main camera in a game, this is mainly used
        /// to prevent linking directly with a camera component and to locate
        /// the camera in the editor scene.
        /// 
        /// Proper usage should use render features instead of this though where you get 
        /// the camera information through the render view instead. This does makes it simpler and 
        /// also allows to make some easier optimizations as no logic to reduce redundant calcualtions over mulitple view (shadow casting etc)
        /// is neeed. 
        /// 
        /// Examples from TR.Stride projects:
        /// * Ocean LOD selection and mesh placement (would have to rewrite buffers written by Transformation feature, or change ordering).
        /// * Vegation instancing placement, would require multiple buffer uploads and calculations of relatively heavy data for each view
        /// </summary>
        /// <param name="sceneSystem"></param>
        /// <returns></returns>
        public static CameraComponent TryGetMainCamera(this SceneSystem sceneSystem)
        {
            CameraComponent camera = null;
            if (sceneSystem.GraphicsCompositor.Cameras.Count == 0)
            {
                // The compositor wont have any cameras attached if the game is running in editor mode
                // Search through the scene systems until the camera entity is found
                // This is what you might call "A Hack"
                foreach (var system in sceneSystem.Game.GameSystems)
                {
                    if (system is SceneSystem editorSceneSystem)
                    {
                        foreach (var entity in editorSceneSystem.SceneInstance.RootScene.Entities)
                        {
                            if (entity.Name == "Camera Editor Entity")
                            {
                                camera = entity.Get<CameraComponent>();
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                camera = sceneSystem.GraphicsCompositor.Cameras[0].Camera;
            }

            return camera;
        }
    }
}
