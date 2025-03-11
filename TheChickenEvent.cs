using System;
using System.Collections.Generic;
using Oxide.Core.Configuration;
using UnityEngine;
using System.Linq;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("TheChickenEvent", "RustFlash", "2.0.0")]
    [Description("The funny chicken event")]

    class TheChickenEvent : RustPlugin
    {
        private PluginConfig config;

        private bool isEventActive = false;
        private Timer eventTimer;
        private List<BaseEntity> chickenList = new List<BaseEntity>();
        private Dictionary<ulong, int> chickenKills = new Dictionary<ulong, int>();

        private class PluginConfig
        {
            public int EventDuration = 60;
            public float ChickenSpawnRadius = 20.0f;
            public bool EnableChickenMovement = true;
            public string WinItemShortname = "rock";
            public int WinItemCount = 3;
            public ulong WinItemSkinId = 2843316584;
            public string VipGroup = "vip";
            public int VipDuration = 7;
            public bool RemoveDeadChickensAfterEvent = true;
        }

        private void Init()
        {
            permission.RegisterPermission("thechickenevent.use", this);
        }

        private void OnServerInitialized()
        {
            LoadConfig();
            if (config.EventDuration <= 0)
            {
                PrintWarning("The event duration must be greater than 0. Change the EventDuration in the configuration file.");
                return;
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig();
        }

        private void LoadConfig()
        {
            config = Config.ReadObject<PluginConfig>();
            SaveConfig();
        }

        private void SaveConfig() => Config.WriteObject(config);

        [ConsoleCommand("flashchickenevent")]
        private void StartFlashChickenEventConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                arg.ReplyWith("You must be an admin to start the Chicken Event from the console.");
                return;
            }

            if (isEventActive)
            {
                arg.ReplyWith("The Chicken Event is already running.");
                return;
            }

            ScheduleEventStart();
        }

        [ChatCommand("flashchickenevent")]
        private void StartChickenEventCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "thechickenevent.use"))
            {
                SendReply(player, "<color=#ffed00>ChickenEvent:</color> You do not have permission to start the Chicken Event.");
                return;
            }

            if (isEventActive)
            {
                SendReply(player, "<color=#ffed00>ChickenEvent:</color> The Chicken Event is already running.");
                return;
            }

            ScheduleEventStart();
        }

        private void ScheduleEventStart()
        {
            isEventActive = true;
            PrintToChat("<color=#ffed00>ChickenEvent:</color> The Chicken Event will start in 5 minutes!");
            timer.Once(120f, () => PrintToChat("<color=#ffed00>ChickenEvent:</color> The Chicken Event will start in 3 minutes!"));
            timer.Once(240f, () => PrintToChat("<color=#ffed00>ChickenEvent:</color> The Chicken Event will start in 60 seconds!"));
            timer.Once(297f, () => PrintToChat("<color=#ffed00>ChickenEvent:</color> 3..."));
            timer.Once(298f, () => PrintToChat("<color=#ffed00>ChickenEvent:</color> 2..."));
            timer.Once(299f, () => PrintToChat("<color=#ffed00>ChickenEvent:</color> 1..."));
            timer.Once(300f, StartChickenEvent);
        }

        private void StartChickenEvent()
        {
            if (!isEventActive)
            {
                PrintToChat("<color=#ffed00>ChickenEvent:</color> The Chicken Event has been cancelled.");
                return;
            }

            PrintToChat("<color=#ffed00>ChickenEvent:</color> The Chicken Event has started! Shoot as many chickens as possible!");

            eventTimer = timer.Once(config.EventDuration, EndChickenEvent);

            foreach (var onlinePlayer in BasePlayer.activePlayerList)
            {
                for (int i = 0; i < 10; i++)
                {
                    Vector3 spawnPosition = onlinePlayer.transform.position + UnityEngine.Random.insideUnitSphere * config.ChickenSpawnRadius;
                    float terrainHeight = TerrainMeta.HeightMap.GetHeight(spawnPosition);
                    Vector3 validPosition = new Vector3(spawnPosition.x, terrainHeight + 1f, spawnPosition.z);

                    if (IsValidSpawnPosition(validPosition))
                    {
                        BaseEntity chicken = GameManager.server.CreateEntity("assets/rust.ai/agents/chicken/chicken.prefab", validPosition);
                        if (chicken != null)
                        {
                            chicken.Spawn();
                            chickenList.Add(chicken);
                            if (config.EnableChickenMovement)
                                MoveRandomly(chicken);
                        }
                        else
                        {
                            PrintWarning("Failed to create chicken entity.");
                        }
                    }
                    else
                    {
                        PrintWarning("Invalid spawn position for chicken: " + validPosition);
                    }
                }
            }
        }

        private bool IsValidSpawnPosition(Vector3 position)
        {
            RaycastHit hit;
            if (Physics.Raycast(position, Vector3.down, out hit, 5f, LayerMask.GetMask("Terrain")))
            {
                return hit.collider != null;
            }
            return false;
        }

        private void EndChickenEvent()
        {
            isEventActive = false;

            var topPlayer = chickenKills.OrderByDescending(pair => pair.Value).FirstOrDefault();

            if (topPlayer.Value > 0)
            {
                var playerId = topPlayer.Key;
                var player = BasePlayer.FindByID(playerId);

                if (player != null)
                {
                    string itemName = config.WinItemShortname;
                    int amount = config.WinItemCount;
                    ulong skinId = config.WinItemSkinId;
                    int chickenKills = topPlayer.Value;

                    GiveItemWithSkin(player, itemName, amount, skinId);
                    PrintToChat($"<color=#ffed00>ChickenEvent:</color> {player.displayName} has win by killing {chickenKills} chickens and received {amount} x {itemName}!");

                    if (!string.IsNullOrEmpty(config.VipGroup) && config.VipDuration > 0)
                    {
                        string command = $"addgroup {player.UserIDString} {config.VipGroup} {config.VipDuration}d";
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, command);
                        PrintToChat($"<color=#ffed00>ChickenEvent:</color> {player.displayName} has been given the '{config.VipGroup}' group for {config.VipDuration} days.");
                    }
                }
            }
            else
            {
                PrintToChat("<color=#ffed00>ChickenEvent:</color> The Chicken Event has ended, but no one win this time.");
            }

            foreach (var chicken in chickenList)
            {
                chicken.Kill();
            }
            chickenList.Clear();
            chickenKills.Clear();

            if (config.RemoveDeadChickensAfterEvent)
            {
                var deadChickens = BaseNetworkable.serverEntities
                                    .OfType<BaseCorpse>()
                                    .Where(c => c.ShortPrefabName == "chicken.corpse")
                                    .ToList();
                foreach (var deadChicken in deadChickens)
                {
                    deadChicken.Kill();
                }
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "ChickenKillCounter");
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            CuiHelper.DestroyUi(player, "ChickenKillCounter");
        }

        private void UpdateKillCountUI(BasePlayer player, int kills)
        {
            CuiHelper.DestroyUi(player, "ChickenKillCounter");
            var container = new CuiElementContainer();
            var label = new CuiLabel
            {
                Text = { Text = $"Kills: {kills}", FontSize = 20, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.383 0.06", AnchorMax = "0.6 0.20" },
                FadeOut = 0.5f
            };
            container.Add(label, "Overlay", "ChickenKillCounter");
            CuiHelper.AddUi(player, container);
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (isEventActive && entity is Chicken)
            {
                var initiator = info.InitiatorPlayer;
                if (initiator != null)
                {
                    var playerId = initiator.userID;
                    if (chickenKills.ContainsKey(playerId))
                    {
                        chickenKills[playerId]++;
                    }
                    else
                    {
                        chickenKills[playerId] = 1;
                    }

                    UpdateKillCountUI(initiator, chickenKills[playerId]);

                    RespawnChickenNearPlayer(initiator);
                }

                chickenList.Remove(entity);
            }
        }

        private void MoveRandomly(BaseEntity chicken)
        {
            timer.Repeat(1f, 0, () =>
            {
                if (chicken == null || chicken.IsDestroyed || !isEventActive)
                {
                    return;
                }

                Vector3 newPosition = chicken.transform.position + UnityEngine.Random.insideUnitSphere * 3f;
                chicken.transform.Translate(newPosition - chicken.transform.position);
            });
        }

        private void RespawnChickenNearPlayer(BasePlayer player)
        {
            Vector3 spawnPosition = player.transform.position + UnityEngine.Random.insideUnitSphere * config.ChickenSpawnRadius;
            float terrainHeight = TerrainMeta.HeightMap.GetHeight(spawnPosition);
            Vector3 chickenSpawnPosition = new Vector3(spawnPosition.x, terrainHeight + UnityEngine.Random.Range(10f, 20f), spawnPosition.z);
            BaseEntity newChicken = GameManager.server.CreateEntity("assets/rust.ai/agents/chicken/chicken.prefab", chickenSpawnPosition);
            if (newChicken != null)
            {
                newChicken.Spawn();
                chickenList.Add(newChicken);
                if (config.EnableChickenMovement)
                    MoveRandomly(newChicken);
            }
        }

        private void GiveItemWithSkin(BasePlayer player, string shortname, int amount, ulong skinId)
        {
            Item item = ItemManager.CreateByName(shortname, amount, skinId);
            if (item != null)
            {
                if (item.MoveToContainer(player.inventory.containerMain, -1, true))
                {
                    item.MarkDirty();
                    player.Command("note.inv", item.info.itemid, -1, item.amount, item.skin);
                }
                else
                {
                    item.Remove();
                }
            }
        }
    }
}