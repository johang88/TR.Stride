using Stride.Engine;
using System;
using System.Collections.Generic;
using System.Text;

namespace TR.Stride
{
    public static class Hacks
    {
        /// <summary>
        /// Hack used to fix entities after hot reload
        /// </summary>
        public static TComponent RelinkComponent<TComponent>(EntityManager entityManager, Guid entityId)
            where TComponent : EntityComponent
        {
            foreach (var entity in entityManager)
            {
                if (entity.Id == entityId)
                {
                    return entity.Get<TComponent>();
                }
            }

            return default;
        }
    }
}
