using UnityEngine;
using System.Numerics;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Libraries;
using System;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("ScavengerHunt", "Fruster", "1.0.5")]
    [Description("ScavengerHunt")]
    class ScavengerHunt : CovalencePlugin
    {
        [PluginReference] Plugin ImageLibrary, SimpleLootTable, ServerRewards;
        private ConfigData Configuration;
        private int remain;
        const int layerS1 = ~(1 << 2 | 1 << 3 | 1 << 4 | 1 << 10 | 1 << 18 | 1 << 28 | 1 << 29);
        private int layerS = LayerMask.GetMask("Water", "Construction", "Prevent Building", "Construction Trigger", "Trigger", "Deployed", "Default", "Ragdoll", "Terrain", "Tree", "Resource", "World");
        private BaseEntity mainCrate = null;
        private List<Vector3> objectList = new List<Vector3>();
        private List<BaseEntity> cupboardList = new List<BaseEntity>();
        private Timer eventTimer;
        private Timer crateTimer;
        private bool findFlag = false;
        class ConfigData
        {
            [JsonProperty("Autostart event")]
            public bool eventAuto = true;
            [JsonProperty("End the event immediately after someone finds a crate")]
            public bool eventFindStop = false;
            [JsonProperty("Minimum time to event start(in seconds)")]
            public int minimumRemainToEvent = 3000;
            [JsonProperty("Maximum time to event start(in seconds)")]
            public int maximumRemainToEvent = 5000;
            [JsonProperty("Minimum amount of online players to trigger the event")]
            public int minOnline = 1;
            [JsonProperty("Crate prefab")]
            public string cratePrefab = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
            [JsonProperty("Crate skin")]
            public ulong crateSkin = 0;
            [JsonProperty("Event duration")]
            public int crateLifeTime = 600;
            [JsonProperty("Minimum number of items in a crate")]
            public int minItems = 6;
            [JsonProperty("Maximum number of items in a crate")]
            public int maxItems = 12;
            [JsonProperty("Simple loot table name")]
            public string tableName = "exampleTable";
            [JsonProperty("Pre-event message")]
            public string preEventMessage = "Scavenger hunt event will start in a minute";
            [JsonProperty("Pre-event message time(in seconds)")]
            public int preEventMessageTime = 60;
            [JsonProperty("Event message")]
            public string eventMessage = "The scavenger hunt event has begun, follow the compass and find the crate first";
            [JsonProperty("Find message(message when someone found the crate)")]
            public string findMessage = "Someone found the crate";
            [JsonProperty("Not find message(event if no one found the box)")]
            public string nobodyMessage = "Nobody found the box crate";
            [JsonProperty("End event message")]
            public string endEventMessage = "Scavenger hunt event ended";
            [JsonProperty("Icon AnchorMin")]
            public string anchorMin = "0.02 0.92";
            [JsonProperty("Icon AnchorMax")]
            public string anchorMax = "0.07 0.994";
            [JsonProperty("North icon")]
            public string northIcon = "https://i.imgur.com/myBNiHd.png";
            [JsonProperty("South icon")]
            public string southIcon = "https://i.imgur.com/UsUrH80.png";
            [JsonProperty("West icon")]
            public string westIcon = "https://i.imgur.com/QiSH0Xx.png";
            [JsonProperty("East icon")]
            public string eastIcon = "https://i.imgur.com/10RljdU.png";
            [JsonProperty("NorthWest icon")]
            public string northWest = "https://i.imgur.com/RC9W0rV.png";
            [JsonProperty("NorthEast icon")]
            public string northEast = "https://i.imgur.com/Nh6wmlo.png";
            [JsonProperty("SouthWest icon")]
            public string southWest = "https://i.imgur.com/KJ8YiU5.png";
            [JsonProperty("SouthEast icon")]
            public string southEast = "https://i.imgur.com/l6HDfzQ.png";
            [JsonProperty("The number of points the player will receive if he opens the crate first (only for ServerRewards plugin)")]
            public int rewardPoints = 0;
            [JsonProperty("Maximum water depth at which a crate can spawn")]
            public float waterDepth = 0.5f;
            
        }

        void SaveConfig() => Config.WriteObject(Configuration, true);

        void LoadConfig()
        {
            Configuration = Config.ReadObject<ConfigData>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            Configuration = new ConfigData();
            SaveConfig();
        }

        private void OnServerInitialized()
        {
            LoadConfig();
            RemoveEvent(true);

            foreach (BaseEntity item in BaseNetworkable.serverEntities)
                if (item.PrefabName == "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab")
                    objectList.Add(item.transform.position);

            foreach (var item in TerrainMeta.Path.Monuments)
                objectList.Add(item.transform.position);

            ImageLibrary?.Call<bool>("AddImage", Configuration.northIcon, "ScavengerHuntN");
            ImageLibrary?.Call<bool>("AddImage", Configuration.southIcon, "ScavengerHuntS");
            ImageLibrary?.Call<bool>("AddImage", Configuration.westIcon, "ScavengerHuntW");
            ImageLibrary?.Call<bool>("AddImage", Configuration.eastIcon, "ScavengerHuntE");
            ImageLibrary?.Call<bool>("AddImage", Configuration.northWest, "ScavengerHuntNW");
            ImageLibrary?.Call<bool>("AddImage", Configuration.northEast, "ScavengerHuntNE");
            ImageLibrary?.Call<bool>("AddImage", Configuration.southWest, "ScavengerHuntSW");
            ImageLibrary?.Call<bool>("AddImage", Configuration.southEast, "ScavengerHuntSE");

            CalcTime();

            if (Configuration.eventAuto)
                timer.Every(1f, () =>
                    {
                        remain--;

                        if (remain == Configuration.preEventMessageTime)
                            if (BasePlayer.activePlayerList.Count > Configuration.minOnline - 1)
                                MessageBroadcast(Configuration.preEventMessage);


                        if (remain < 0)
                        {
                            if (BasePlayer.activePlayerList.Count > Configuration.minOnline - 1)
                                EventStart();
                            else
                            {
                                Puts("Not enough online players on the server, event will not start!");
                                CalcTime();
                            }

                        }
                    });
        }

        private void MessageBroadcast(string message)
        {
            if (message != "")
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    player.ChatMessage(message);
        }

        private void CalcTime()
        {
            remain = UnityEngine.Random.Range(Configuration.minimumRemainToEvent, Configuration.maximumRemainToEvent);
            Puts("Next event will start in " + remain.ToString() + " seconds");
        }

        private void EventStart()
        {
            RemoveEvent(true);
            CalcTime();
            eventTimer?.Destroy();
            CreateEventTimer();
            Puts("Event started");
            Interface.CallHook("ScavengerHuntStarted");
            findFlag = false;
            MessageBroadcast(Configuration.eventMessage);

            RaycastHit check;
            float x;
            float z;
            int mapSize = (int)TerrainMeta.Size.x/2;
            Vector3 startPoint;
            int radius = 100;
            bool flag;
            Vector3 cratePosition = Vector3.zero;



            for (int i = 0; i < 999; i++)
            {
                x = UnityEngine.Random.Range(-mapSize, mapSize);
                z = UnityEngine.Random.Range(-mapSize, mapSize);
                startPoint = new Vector3(x, 500, z);
                flag = true;


                if (Physics.Raycast(startPoint, Vector3.down, out check, 999, layerS))
                    if (check.collider.name == "Terrain" && WaterLevel.GetWaterDepth(check.point, false, false) <= Configuration.waterDepth)
                    {
                        cratePosition = check.point;

                        foreach (var item in objectList)
                            if (Vector3.Distance(check.point, item) < radius)
                            {

                                flag = false;
                                break;
                            };

                        if (flag)
                        {
                            SpawnCrate(check.point);
                            return;
                        }

                    }
            }

            SpawnCrate(cratePosition);

        }

        private void CreateEventTimer()
        {
            CuiElementContainer container;
            PlayerEyes eyes;
            Vector3 temp;
            Vector3 first;
            Vector3 second;
            Vector3 side;
            float angle;
            string icon;

            eventTimer = timer.Every(5, () =>
            {
                if (mainCrate)
                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                    {
                        eyes = player.GetComponent<PlayerEyes>();
                        eyes.transform.rotation = Quaternion.Euler(eyes.rotation.eulerAngles.x, eyes.rotation.eulerAngles.y, eyes.rotation.eulerAngles.z);
                        temp = eyes.transform.position + eyes.transform.forward;
                        first = new Vector3(temp.x - eyes.transform.position.x, 0, temp.z - eyes.transform.position.z);
                        second = new Vector3(mainCrate.transform.position.x - eyes.transform.position.x, 0, mainCrate.transform.position.z - eyes.transform.position.z);
                        first.Normalize();
                        second.Normalize();
                        side = Vector3.Cross(first, second);
                        angle = Vector3.Angle(first, second);
                        icon = "ScavengerHuntN";

                        if (side.y < 0)
                        {
                            if (angle > 30)
                                icon = "ScavengerHuntNW";
                            if (angle > 70)
                                icon = "ScavengerHuntW";
                            if (angle > 120)
                                icon = "ScavengerHuntSW";
                            if (angle > 160)
                                icon = "ScavengerHuntS";
                        }
                        else
                        {
                            if (angle > 30)
                                icon = "ScavengerHuntNE";
                            if (angle > 70)
                                icon = "ScavengerHuntE";
                            if (angle > 120)
                                icon = "ScavengerHuntSE";
                            if (angle > 160)
                                icon = "ScavengerHuntS";
                        }

                        container = new CuiElementContainer();
                        DrawImage(player, container, Configuration.anchorMin, Configuration.anchorMax, icon);

                    }


            });
        }

        private void SpawnCrate(Vector3 pos)
        {
            var crate = GameManager.server.CreateEntity(Configuration.cratePrefab, pos);
            crate.skinID = Configuration.crateSkin;
            crate.name = "scavengerhuntcrate";
            crate.Spawn();
            mainCrate = crate;
            BaseCombatEntity crateBase = crate as BaseCombatEntity;
            crateBase.SetMaxHealth(99999);
            crateBase.SetHealth(99999);
            SimpleLootTable?.Call("GetSetItems", crate, Configuration.tableName, Configuration.minItems, Configuration.maxItems, 1f);

            crateTimer?.Destroy();
            crateTimer = timer.Once(Configuration.crateLifeTime, () => { RemoveEvent(true); });
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            DrawImage(player, container, "0 0", "0 0", "ScavengerHuntN");
            DrawImage(player, container, "0 0", "0 0", "ScavengerHuntS");
            DrawImage(player, container, "0 0", "0 0", "ScavengerHuntW");
            DrawImage(player, container, "0 0", "0 0", "ScavengerHuntE");
            DrawImage(player, container, "0 0", "0 0", "ScavengerHuntNV");
            DrawImage(player, container, "0 0", "0 0", "ScavengerHuntNE");
            DrawImage(player, container, "0 0", "0 0", "ScavengerHuntSW");
            DrawImage(player, container, "0 0", "0 0", "ScavengerHuntSE");
            container.Clear();

        }

        private void DrawImage(BasePlayer player, CuiElementContainer container, string anchorMin, string anchorMax, string icon)
        {
            container.Clear();
            CuiHelper.DestroyUi(player, "Compas");
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax, OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Hud", "Compas");
            container.Add(new CuiElement
            {
                Parent = "Compas",
                Components =
                    {
                        new CuiRawImageComponent { Png = (string)ImageLibrary?.Call("GetImage", icon) },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
            });
            CuiHelper.AddUi(player, container);
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container.name == "scavengerhuntcrate")
            {
                if (!findFlag)
                {
                    findFlag = true;
                    ServerRewards?.Call("AddPoints", player.userID, Configuration.rewardPoints);
                    MessageBroadcast(Configuration.findMessage);
                    Puts("Someone found the crate");
                    if (Configuration.eventFindStop)
                        RemoveEvent(false);
                }
            }
            return null;
        }

        private object CanPickupEntity(BasePlayer player, StorageContainer container)
        {
            if (container.name == "scavengerhuntcrate")
                return false;

            return true;
        }

        private void Unload()
        {
            RemoveEvent(true);
        }

        private void RemoveEvent(bool killCrate)
        {
            if (mainCrate)
            {
                if (!findFlag)
                    MessageBroadcast(Configuration.nobodyMessage);

                MessageBroadcast(Configuration.endEventMessage);
            }

            if (mainCrate && killCrate)
                mainCrate.Kill();

            CuiElementContainer container;
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                container = new CuiElementContainer();
                DrawImage(player, container, "0 0", "0 0", "icon");
            }



            if (eventTimer != null)
            {
                Puts("Event ended");
                Interface.CallHook("ScavengerHuntEnded");
            }


            if (eventTimer != null)
            {
                eventTimer.Destroy();
                eventTimer = null;
            }

            if (crateTimer != null)
            {
                crateTimer.Destroy();
                crateTimer = null;
            }


        }

        [Command("sch_start")]
        private void sch_start(IPlayer iplayer)
        {
            var player = (BasePlayer)iplayer.Object;
            if (iplayer.IsAdmin)
            {

                EventStart();

            }
        }

        [Command("sch_stop")]
        private void sch_stop(IPlayer iplayer)
        {
            var player = (BasePlayer)iplayer.Object;
            if (iplayer.IsAdmin)
            {

                RemoveEvent(true);

            }
        }
    }
}