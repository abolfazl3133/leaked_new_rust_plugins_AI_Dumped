using System.Collections.Generic;
using UnityEngine;
using System;

namespace Oxide.Plugins
{
    [Info("Minicopter Seating", "Bazz3l", "1.1.5")]
    [Description("Spawns an extra seat each side of the minicopter.")]
    class MinicopterSeating : RustPlugin
    {
        #region Fields
        const string _chairPrefab = "assets/prefabs/vehicle/seats/passengerchair.prefab";
        SeatingManager _manager = new SeatingManager();
        #endregion

        #region Oxide
        void OnEntitySpawned(BaseVehicle mini)
        {
            if (mini.mountPoints.Count < 4 && mini.ShortPrefabName == "minicopter.entity")
            {
                _manager.Setup(mini);
            }
        }
        #endregion

        #region Seating
        class SeatingManager
        {
            public void Setup(BaseVehicle vehicle)
            {
                BaseVehicle.MountPointInfo pilot = vehicle.mountPoints[0];
                BaseVehicle.MountPointInfo passenger = vehicle.mountPoints[1];

                vehicle.mountPoints.Add(MakeMount(vehicle, new Vector3(0.6f, 0.2f, -0.3f)));
                vehicle.mountPoints.Add(MakeMount(vehicle, new Vector3(-0.6f, 0.2f, -0.3f)));

                MakeSeat(vehicle, new Vector3(0.6f, 0.2f, -0.5f));
                MakeSeat(vehicle, new Vector3(-0.6f, 0.2f, -0.5f));
            }

            void MakeSeat(BaseVehicle vehicle, Vector3 position)
            {
                BaseEntity entity = GameManager.server.CreateEntity(_chairPrefab, vehicle.transform.position);
                if (entity == null) return;
                entity.SetParent(vehicle);
                entity.transform.localPosition = position;
                entity.Spawn();
            }

            BaseVehicle.MountPointInfo MakeMount(BaseVehicle vehicle, Vector3 position)
            {
                return new BaseVehicle.MountPointInfo
                {
                    pos = position,
                    rot = vehicle.mountPoints[1].rot,
                    prefab = vehicle.mountPoints[1].prefab,
                    mountable = vehicle.mountPoints[1].mountable,
                };
            }
        }
        #endregion
    }
}
