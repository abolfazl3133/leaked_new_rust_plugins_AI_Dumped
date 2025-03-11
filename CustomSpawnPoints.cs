
using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
[Info("CustomSpawnPoints", "sdapro", "1.1.4")]
class CustomSpawnPoints : RustPlugin
{
        #region Fields
private Dictionary<int, Vector3> spawnPoints = new Dictionary<int, Vector3>();
        private List<int> spawnPointIndexes = new List<int>();
private bool initialized;
        private int nextSpawnIndex = 1;
private const float checkRadius = 1.5f; // Радиус проверки для объектов фундаментов! большой не ставить! сервер твой умрет
        #endregion

        #region Oxide Hooks        
private void OnServerInitialized()
        {
            initialized = true;
        }

private object OnPlayerRespawn(BasePlayer player)
{
    if (!initialized || spawnPoints.Count == 0)
        return null;

object position = GetSpawnPoint();
    if (position is Vector3 spawnPosition)
    {
        // не удаляй гей!! уберет перекрывающие объекты в точке возрождения
        RemoveOverlappingObjects(spawnPosition);

        return new BasePlayer.SpawnPoint()
        {
            pos = spawnPosition,
            rot = Quaternion.identity
        };
    }
    return null;
}
        #endregion
private object GetSpawnPoint()
        {
            if (spawnPointIndexes.Count == 0)
                return null;

            int randomIndex = spawnPointIndexes.GetRandom();
            return spawnPoints[randomIndex];
        }

        private void RemoveOverlappingObjects(Vector3 position)
{
    Collider[] colliders = Physics.OverlapSphere(position, checkRadius);
foreach (var collider in colliders)
    {
        BaseEntity entity = collider.GetComponentInParent<BaseEntity>();
        if (entity != null && entity.IsValid())
        {
            // Фильтруем только объекты, которые можно безопасно. удалить можешь еще стены закинуть сюда
            if (entity.PrefabName.Contains("assets/prefabs/building core/foundation/foundation.prefab") || entity.PrefabName.Contains("barrel") || entity.PrefabName.Contains("wood"))
            {
                entity.Kill();
                Puts($"Removed overlapping entity: {entity.ShortPrefabName}");
            }
        }
    }
}

        #region Chat
		//Не сохранит в дату точки спавна! если нужно то пишите я добавлю
        [ChatCommand("spa")]
private void cmdSpawn(BasePlayer player, string command, string[] args)
        {
            if (!initialized)
            {
                return;
            }

            Vector3 spawnPosition = player.transform.position;

            RemoveOverlappingObjects(spawnPosition);

            spawnPoints[nextSpawnIndex] = spawnPosition;
            spawnPointIndexes.Add(nextSpawnIndex);
            nextSpawnIndex++;

            SendReply(player, $"Точка {nextSpawnIndex - 1} позиция {spawnPosition}");
        }

        [ChatCommand("sremove")]
private void cmdSRemove(BasePlayer player, string command, string[] args)
        {
            if (!initialized)
            {
                return;
            }

            if (args.Length != 1)
            {
                SendReply(player, "Используй: /sremove номер точки");
                return;
            }

            if (!int.TryParse(args[0], out int spawnIndex))
            {
                SendReply(player, "Хуйцо");
                return;
            }

            if (!spawnPoints.ContainsKey(spawnIndex))
            {
                SendReply(player, $"Такой точки как {spawnIndex} нет");
                return;
            }

            spawnPoints.Remove(spawnIndex);
            spawnPointIndexes.Remove(spawnIndex);

            SendReply(player, $"Школьники  сожрали точку {spawnIndex}");
        }
        #endregion
    }
}
