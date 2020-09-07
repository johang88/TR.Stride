using System;
using System.Collections.Generic;
using System.Text;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Engine;

namespace TR.Stride.Gameplay.Savegames
{
    [DataContract]
    public class SaveState
    {
        [DataMember] public Dictionary<Guid, SceneSaveState> Scenes { get; set; } = new Dictionary<Guid, SceneSaveState>();
        
        public SceneSaveState GetSceneSaveState(Scene scene)
        {
            if (Scenes.TryGetValue(scene.Id, out var sceneState))
                return sceneState;

            var state = new SceneSaveState();
            Scenes.Add(scene.Id, state);

            return state;
        }
    }

    [DataContract]
    public class SceneSaveState
    {
        [DataMember] public HashSet<Guid> DeletedEntities { get; set; } = new HashSet<Guid>();
        [DataMember] public Dictionary<Guid, EntitySaveState> Entities { get; set; } = new Dictionary<Guid, EntitySaveState>();
    }

    [DataContract]
    public class EntitySaveState
    {
        [DataMember] public Vector3 Position { get; set; }
        [DataMember] public Quaternion Rotation { get; set; }
        [DataMember] public Vector3 Scale { get; set; }
        [DataMember] public UrlReference<Prefab> Prefab { get; set; }
    }
}
