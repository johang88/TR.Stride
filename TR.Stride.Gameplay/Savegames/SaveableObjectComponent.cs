using Stride.Core;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.Engine.Design;
using System;
using System.Collections.Generic;
using System.Text;

namespace TR.Stride.Gameplay.Savegames
{
    /// <summary>
    /// Tracks entities that can have saveable state
    /// Transform is the only property that will be saved by default
    /// </summary>
    [DefaultEntityComponentProcessor(typeof(SaveableObjectProcessor), ExecutionMode = ExecutionMode.Runtime)]
    public class SaveableObjectComponent : ScriptComponent
    {
        /// <summary>
        /// Optional but must be set for entities that are instansiated from prefabs at runtime
        /// </summary>
        [DataMember] public UrlReference<Prefab> Prefab { get; set; }
    }
}
