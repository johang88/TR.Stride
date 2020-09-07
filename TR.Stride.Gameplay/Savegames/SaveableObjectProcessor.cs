using Stride.Core.Annotations;
using Stride.Engine;
using System;
using System.Collections.Generic;
using System.Text;

namespace TR.Stride.Gameplay.Savegames
{
    public class SaveableObjectProcessor : EntityProcessor<SaveableObjectComponent>
    {
        private SaveGameSystem _saveGameSystem = null;

        protected override void OnSystemAdd()
        {
            base.OnSystemAdd();

            var game = Services.GetService<Game>();
            _saveGameSystem = game.GetSaveGameSystem();
        }

        protected override void OnEntityComponentAdding(Entity entity, [NotNull] SaveableObjectComponent component, [NotNull] SaveableObjectComponent data)
        {
            base.OnEntityComponentAdding(entity, component, data);

            _saveGameSystem.TrackEntity(entity);
        }
    }
}
