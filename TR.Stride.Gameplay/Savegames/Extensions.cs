using Stride.Engine;
using Stride.Games;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TR.Stride.Gameplay.Savegames
{
    public static class Extensions
    {
        public static SaveGameSystem GetSaveGameSystem(this Game game)
            => (SaveGameSystem)game.GameSystems.First(x => x is SaveGameSystem);
    }
}
