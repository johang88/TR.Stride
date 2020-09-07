using Stride.Core;
using Stride.Core.Serialization;
using Stride.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TR.Stride.Gameplay.Savegames
{
    public class SaveGameSystem : GameSystem
    {
        public SaveState State { get; private set; } = new SaveState();

        public SaveGameSystem(IServiceRegistry registry) 
            : base(registry)
        {
        }

        /// <summary>
        /// Restore a save state from a stream
        /// </summary>
        /// <param name="stream"></param>
        public void Restore(Stream stream)
        {
            Reset();

            throw new NotImplementedException();
        }

        /// <summary>
        /// Serialize the current save state
        /// </summary>
        /// <param name="stream"></param>
        public void Save(Stream stream)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Clear the current save state
        /// </summary>
        public void Reset()
        {
            State = new SaveState();
        }

        /// <summary>
        /// Save the state of a scene
        /// </summary>
        /// <param name="scene"></param>
        public void SaveScene(Scene scene)
        {
            var sceneSaveState = State.GetSceneSaveState(scene);

            // Gather active entities
            var activeEntities = new Dictionary<Guid, Entity>();
            foreach (var entity in scene.Entities)
            {
                if (entity.Get<SaveableObjectComponent>() != null)
                    activeEntities.Add(entity.Id, entity);
            }

            // Detect deleted entities
            sceneSaveState.DeletedEntities.Clear();
            foreach (var entityState in sceneSaveState.Entities)
            {
                if (!activeEntities.ContainsKey(entityState.Key))
                {
                    sceneSaveState.DeletedEntities.Add(entityState.Key);
                }
            }

            // Remove deleted entity state
            foreach (var deletedEntityId in sceneSaveState.DeletedEntities)
            {
                sceneSaveState.Entities.Remove(deletedEntityId);
            }

            // Save the state of all active entites
            foreach (var activeEntity in sceneSaveState.Entities)
            {
                var entity = activeEntities[activeEntity.Key];
                var entitySaveState = activeEntity.Value;

                var transform = entity.Transform;

                entitySaveState.Position = transform.Position;
                entitySaveState.Rotation = transform.Rotation;
                entitySaveState.Scale = transform.Scale;
                entitySaveState.Prefab = entity.Get<SaveableObjectComponent>().Prefab;
            }
        }

        /// <summary>
        /// Restore the state of a scene
        /// </summary>
        /// <param name="scene"></param>
        public void RestoreScene(Scene scene)
        {
            var sceneSaveState = State.GetSceneSaveState(scene);

            // Remove entities that have been deleted and gather existing entities
            var entitiesToRemove = new List<Entity>();
            var entitiesById = new Dictionary<Guid, Entity>();

            foreach (var entity in scene.Entities)
            {
                if (sceneSaveState.DeletedEntities.Contains(entity.Id))
                {
                    entitiesToRemove.Add(entity);
                }
                else if (entity.Get<SaveableObjectComponent>() != null)
                {
                    entitiesById.Add(entity.Id, entity);
                }
            }

            foreach (var entity in entitiesToRemove)
            {
                scene.Entities.Remove(entity);
            }

            // Instansiate runtime created entities
            foreach (var entityState in sceneSaveState.Entities)
            {
                if (entitiesById.ContainsKey(entityState.Key))
                    continue; // Skip as entitiy already exists in scene

                var entitySaveState = entityState.Value;

                if (entitySaveState.Prefab == null)
                    continue; // TODO: Log warning

                var prefab = Content.Load<Prefab>(entitySaveState.Prefab.Url);

                var entity = prefab.Instantiate()[0];
                entity.Id = entityState.Key;

                scene.Entities.Add(entity);
                entitiesById.Add(entity.Id, entity);
            }

            // Restore state of any active entities
            foreach (var entityToRestore in entitiesById)
            {
                if (!sceneSaveState.Entities.TryGetValue(entityToRestore.Key, out var entitySaveState))
                    continue; // TODO: Log warning, also ... this should never happen ...

                var transform = entityToRestore.Value.Transform;
                transform.Position = entitySaveState.Position;
                transform.Rotation = entitySaveState.Rotation;
                transform.Scale = entitySaveState.Scale;
            }
        }

        internal void TrackEntity(Entity entity)
        {
            var sceneSaveState = State.GetSceneSaveState(entity.Scene);
            if (!sceneSaveState.Entities.ContainsKey(entity.Id))
            {
                sceneSaveState.Entities.Add(entity.Id, new EntitySaveState());
            }
        }
    }
}
