using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("No Planting Seeds", "vinokurov detka", "1.0.0")]
    class NoPlantingSeeds : RustPlugin
    {
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is global::BaseEntity)
            {
                var plant = entity as global::BaseEntity;
                if (plant != null)
                {
                    if (plant is global::GrowableEntity)
                    {
                        var growable = plant as global::GrowableEntity;
                        if (growable.Properties.state == PlantProperties.State.Planted)
                        {
                            growable.Kill();
                        }
                    }
                }                               
            }
        }
    }
}