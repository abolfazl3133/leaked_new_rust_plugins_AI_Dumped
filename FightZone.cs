using Rust;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using Rust.UI;
using System.Collections;
using Oxide.Core.Libraries.Covalence;
using System;
using Newtonsoft.Json;
using Oxide.Core;
using CompanionServer.Handlers;


namespace Oxide.Plugins
{
    [Info("FightZone", "https://discord.gg/TrJ7jnS233", "1.0.0")]
    public class FightZone : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary, IQRates;
        #region vars
        bool IsActive = false;
        string FightIMG = "https://i.imgur.com/hxCf3uU.png";
        public string stonewallOre = "assets/bundled/prefabs/autospawn/resource/ores/stone wall-ore.prefab";
        public string woodenwallOre = "assets/bundled/prefabs/autospawn/resource/ores/wooden wall-ore.prefab";
        public string icewallOre = "assets/bundled/prefabs/autospawn/resource/ores/ice wall-ore.prefab";
        HashSet<BaseEntity> OreList = new HashSet<BaseEntity>();

        #endregion


        #region Configuration


            private PluginConfig config;

            protected override void LoadDefaultConfig()
            {
                config = PluginConfig.DefaultConfig();
            }

            protected override void LoadConfig()
            {
                base.LoadConfig();
                config = Config.ReadObject<PluginConfig>();

                if (config.PluginVersion < Version)
                    UpdateConfigValues();

                Config.WriteObject(config, true);
            }

            private void UpdateConfigValues()
            {
                PluginConfig baseConfig = PluginConfig.DefaultConfig();
                if (config.PluginVersion < new VersionNumber(1, 0, 0))
                {
                    PrintWarning("Бомже-Фризен");
                }

                config.PluginVersion = Version;
            }

            protected override void SaveConfig()
            {
                Config.WriteObject(config);
            }
            
            private class PluginConfig
            {
                [JsonProperty("Раз во сколько времени будет файт-зона")]
                public int TimeToStartFightZone = 7200;

                [JsonProperty("Версия конфигурации")] 
                public VersionNumber PluginVersion = new VersionNumber();

                public static PluginConfig DefaultConfig()
                {
                    return new PluginConfig()
                    {
                        TimeToStartFightZone = 7200,
                        PluginVersion = new VersionNumber(),
                    };
                }
            }

            #endregion


        static FightZone ins;


        #region Commands
        [ChatCommand("fz")]
        void CreateFZCommand(BasePlayer player, string cmd, string[] Args)
        {
            if (player == null) return;
            if (!player.IsAdmin)
            {
                SendReply(player, $"Команда доступна только администраторам");
                return;
            }
            if (Args == null || Args.Length == 0 || Args[0] != "start" && Args[0] != "stop")
            {
                SendReply(player, $"Используйте /fz start или /fz stop");
                return;
            }
            switch (Args[0])
            {
                case "start":
                    if (OreList.Count > 0) { SendReply(player, "Активный ивент уже проводится"); return; }
                    else SendReply(player, "Вы в ручную запустили ивент");
                    CreateFZ(true);
                    return;
                case "cancel":
                    if (OreList.Count == 0) { SendReply(player, "Активного ивента нет"); return; }
                    else SendReply(player, "Вы принудительно остановили ивент");
                    DestroyFZ();
                    return;
            }
        }





        #endregion




        #region Hooks


      
        void OnPlayerConnected(BasePlayer player)
        {
            if (IsActive) MainGUIqQ(player);
        }



        object OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.ToPlayer();
            if(Vector3.Distance(position, player.transform.position) < 100)
            {
                item.amount = item.amount * 20;
            }
            return null;
        }






       

        object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            var player = planner.GetOwnerPlayer();
            if (prefab.fullName.Contains("wall.external.high.wood") || prefab.fullName.Contains("wall.external.high.ice") || prefab.fullName.Contains("wall.external.high.stone"))
                return null;
            if (Vector3.Distance(position, player.transform.position) < 100)
            { 
                return false;      
            }
            return null;

        }



        private void OnPlayerAttack(BasePlayer attacker, HitInfo hitInfo)
        {
            if (attacker == null || hitInfo?.HitEntity == null) return;
            var target = hitInfo.HitEntity as BasePlayer;

            if (hitInfo?.HitEntity is BasePlayer)
            {
                if (Vector3.Distance(position, attacker.transform.position) > 100)
                {
                    if(Vector3.Distance(position, target.transform.position) < 100)
                    {
                        hitInfo?.damageTypes?.ScaleAll(0);
                        return;
                    }
                }
                if (Vector3.Distance(position, attacker.transform.position) < 100)
                {
                    if (Vector3.Distance(position, target.transform.position) > 100)
                    {
                        hitInfo?.damageTypes?.ScaleAll(0);
                        return;
                    }
                }
            }
        }


        void OnServerInitialized()
        {
            timer.Every(7200f, () => { CreateFZ(false); });
            ImageLibrary.Call("AddImage", FightIMG, FightIMG);
            AddCovalenceCommand("openinfoqQQ", nameof(CmdMenuOpen1q));
            AddCovalenceCommand("closeinfoqQQ", nameof(CmdMenuClose1q));


        }


            void Unload()
        {
            if (OreList.Count > 0)
                DestroyFZ();
        }

        #endregion

        #region Methods


        string quad = string.Empty;
        Vector3 position = Vector3.zero;

        void CreateFZ(bool AdminCreated)
        {
            if (!AdminCreated && BasePlayer.activePlayerList.Count < 40)
            {
                PrintWarning("Не достаточно игроков для старта ивента");
                return;
            }

            if (OreList.Count > 0)
                DestroyFZ();

            Vector3 pos;
            pos.x = 0;
            pos.y = 0;
            pos.z = 0;
            var success = CreateSpawnPosition();
            pos = (Vector3)success;
            pos.y = GetGroundPosition(pos);
            SpawnStones(pos);
            quad = GetGrid(pos);
            position = pos;
            foreach (var pl in BasePlayer.activePlayerList)
            {
                MainGUIqQ(pl);
            }
            IsActive = true;
            Server.Broadcast($"<color=red>[ FIGHT ZONE]</color>\nИвент начался в квадрате {quad}!");
            CreatePrivateMap(pos);
            SpawnSphere(pos);
            InvokeHandler.Instance.InvokeRepeating(UpdateUI, 1f, 1f);
        }

        private Quaternion rot = new Quaternion();
        private string strPrefab = "assets/prefabs/visualization/sphere.prefab";
        private BaseEntity sphere;
        void SpawnSphere(Vector3 pos)
        {
            sphere = GameManager.server.CreateEntity(strPrefab, pos, rot, true);
            SphereEntity ball = sphere.GetComponent<SphereEntity>();
            ball.currentRadius = 1f;
            ball.lerpRadius = 2.0f * 100;
            ball.lerpSpeed = 100f;
            sphere.Spawn();
        }

        void DestroySphere()
        {
            sphere.Kill();
        }
        public Dictionary<BasePlayer, bool> openPanel2 = new Dictionary<BasePlayer, bool>();

        private void CmdMenuOpen1q(IPlayer user, string cmd, string[] args)
        {
            var player = user?.Object as BasePlayer;
            if (player == null) return;

            consoleopenqQ(player);
            openPanel2[player] = true;
        }

        private void CmdMenuClose1q(IPlayer user, string cmd, string[] args)
        {
            var player = user?.Object as BasePlayer;
            if (player == null) return;

            CuiHelper.DestroyUi(player, "infoqQ");
            openPanel2[player] = false;
        }
        public void consoleopenqQ(BasePlayer player)
        {
            InfoMenuq(player);
            timerUIq(player, timerq);
        }

        public void MainGUIqQ(BasePlayer player)
        {
            var c = new CuiElementContainer();
            UI.AddImage(ref c, "Overlay", "MainGUIqQ", "0 0 0 0", "", "", "1 0.50", "1 0.50", $"-44.182 -12.661", $"-3.618 25.735");
            UI.AddImage(ref c, "MainGUIqQ", "mainqQ", "0.5 0.5 0.5 0.25", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "-20.282 -20.715", "20.283 20.035");
            UI.AddRawImage(ref c, "mainqQ", "iconsqQ", ImageLibrary?.Call<string>("GetImage", FightIMG), "1 1 1 0.9", "", "", "0 0", "1 1", "6 7", "-7 -6");
            UI.AddImage(ref c, "MainGUIqQ", "linesqQ", "0 0 0 0", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "-20.282 -20.715", "21.283 -18.585");
            UI.AddButton(ref c, "mainqQ", "openqQ", "openinfoqQQ", "", "0 0 0 0", "", "", "0 0", "1 1", "", "");
            CuiHelper.DestroyUi(player, "MainGUIqQ");
            CuiHelper.AddUi(player, c);
        }
        public void InfoMenuq(BasePlayer player)
        {
            var c = new CuiElementContainer();
            UI.AddImage(ref c, "MainGUIqQ", "infoqQ", "0.5 0.5 0.5 0.25", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "-223.521 -20.715", "-24.017 20.035");
            UI.AddText(ref c, "infoqQ", "textqQ", "1 1 1 0.9", $"В квадрате {quad} начался ивент\nскорее поспеши туда!", TextAnchor.UpperLeft, 10, "0.5 0.5", "0.5 0.5", "-94.187 -20", "91.911 6.411");
            UI.AddButton(ref c, "infoqQ", "closeqQ", "closeinfoqQQ", "", "0.70 0.00 0.00 0.8", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "85.23 5.691", "99.752 19.875");
            UI.AddText(ref c, "closeqQ", "closesqQ", "1 1 1 0.9", $"☓", TextAnchor.MiddleCenter, 10, "0.5 0.5", "0.5 0.5", "-6.737 -6.201", "6.737 6.202");
            CuiHelper.DestroyUi(player, "infoqQ");
            CuiHelper.AddUi(player, c);
        }
        public void timerUIq(BasePlayer player, double time)
        {
            var c = new CuiElementContainer();

            UI.AddText(ref c, "infoqQ", "titleqQ", "1 1 1 0.9", $"До конца  FIGHT ZONE [{FormatTimes(TimeSpan.FromSeconds(time))}]", TextAnchor.UpperLeft, 12, "0.5 0.5", "0.5 0.5", "-94.187 -4.775", "85.183 19.875");
            CuiHelper.DestroyUi(player, "titleqQ");
            CuiHelper.AddUi(player, c);
        }
        public void linesqQ(BasePlayer player, double time)
        {
            var c = new CuiElementContainer();
            double timeLines = (time / 900);

            UI.AddImage(ref c, "linesqQ", "lineqQ", "1 1 1 1", "", "assets/icons/greyout.mat", "0 0", $"{timeLines} 1", "", "");
            CuiHelper.DestroyUi(player, "lineqQ");
            CuiHelper.AddUi(player, c);
        }

        double timerq = 900;
        void UpdateUI()
        {
            
            foreach (var p in BasePlayer.activePlayerList)
            {
                linesqQ(p, timerq);
                if (openPanel2.ContainsKey(p))
                    if (openPanel2.ContainsValue(true))
                    {
                        timerUIq(p, timerq);
                    }

                if (timerq <= 0) 
                { 
                    DestroyFZ();
                }
                
            }
            timerq--;
        }


        private static string FormatTimes(TimeSpan time)
        {
            return ($"{FormatMinutes(time.Minutes)}:{FormatSeconds(time.Seconds)}");
        }
        private static string FormatMinutes(int minutes) => FormatUnits2(minutes);

        private static string FormatSeconds(int seconds) => FormatUnits(seconds);

        private static string FormatUnits2(int units)
        {
            var tmp = units % 10;

            if (units >= 10)
                return $"{units}";

            if (units >= 0 && units <= 10)
                return $"{units}";

            return $"{units}";
        }

        private static string FormatUnits(int units)
        {
            var tmp = units % 10;

            if (units >= 10)
                return $"{units}";

            if (units >= 0 && units <= 10)
                return $"0{units}";

            return $"0{units}";
        }

        private MapMarkerGenericRadius mapMarker;
        const string markerEnt = "assets/prefabs/tools/map/genericradiusmarker.prefab";
        private void CreatePrivateMap(Vector3 pos)
        {
            mapMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", pos).GetComponent<MapMarkerGenericRadius>();
            vendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", pos).GetComponent<VendingMachineMapMarker>();
            mapMarker.radius = 1f;
            mapMarker.color1 = Color.red;
            mapMarker.alpha = 0.5f;
            mapMarker.enabled = true;
            vendingMarker.markerShopName = " FIGHTZONE";
            vendingMarker.Spawn();
            vendingMarker.enabled = false;
            mapMarker.Spawn();
            mapMarker.SendUpdate();
        }
        VendingMachineMapMarker vendingMarker;

        void DestroyFZ()
        {
            if (OreList.Count > 0)
            {
                foreach (var bases in OreList)
                {
                    if (bases != null && !bases.IsDestroyed)
                        bases.Kill();
                }
                OreList?.Clear();

                foreach (var p in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(p, "MainGUIqQ");
                }
                InvokeHandler.Instance.CancelInvoke(UpdateUI);
                IsActive = false;
                if (mapMarker != null && !mapMarker.IsDestroyed) mapMarker.Kill();
                if (vendingMarker != null && !vendingMarker.IsDestroyed) vendingMarker.Kill();
                Server.Broadcast("<color=red>[ FIGHT ZONE]</color>\nИвент завершён!");
                DestroySphere();
            } 
        }

        private void SpawnStones(Vector3 pos)
        {
            for (int i = 0; i < 30; i++)
            {
                var entity = GameManager.server.CreateEntity(stonewallOre, RandomCircle(pos, UnityEngine.Random.Range(-60, 60)));
                entity.enableSaving = false;
                entity.Spawn();
                OreList.Add(entity);
            }
            for (int i = 0; i < 40; i++)
            {
                var entity = GameManager.server.CreateEntity(icewallOre, RandomCircle(pos, UnityEngine.Random.Range(-80, 80)));
                entity.enableSaving = false;
                entity.Spawn();
                OreList.Add(entity);
            }
            for (int i = 0; i < 60; i++)
            {
                var entity = GameManager.server.CreateEntity(woodenwallOre, RandomCircle(pos, UnityEngine.Random.Range(-100, 100)));
                entity.enableSaving = false;
                entity.Spawn();
                OreList.Add(entity);
            }
        }

        private static string GetGrid(Vector3 pos)
        {
            float ang = UnityEngine.Random.value * 360;
            pos.x = center.x + radius * Mathf.Sin(ang * Mathf.Deg2Rad);
            pos.z = center.z + radius * Mathf.Cos(ang * Mathf.Deg2Rad);
            pos.y = TerrainMeta.HeightMap.GetHeight(pos);
            return pos;
        }

        static float GetGroundPosition(Vector3 pos)
        {
            float y = TerrainMeta.HeightMap.GetHeight(pos);
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 200f, pos.z), Vector3.down, out hit, Mathf.Infinity, LayerMask.GetMask(new[] {
          "Terrain",
          "World",
          "Default",
          "Construction",
          "Deployed"
        })) && !hit.collider.name.Contains("rock_cliff")) return Mathf.Max(hit.point.y, y);
            return y;
        }
        Vector3 RandomCircle(Vector3 center, float radius = 2)
        {
            float ang = UnityEngine.Random.value * 360;
            Vector3 pos;
            pos.x = center.x + radius * Mathf.Sin(ang * Mathf.Deg2Rad);
            pos.z = center.z + radius * Mathf.Cos(ang * Mathf.Deg2Rad);
            pos.y = center.y;
            pos.y = GetGroundPosition(pos);
            return pos;
        }
        const float HeightToRaycast = 250f;
        const float RaycastDistance = 500f;
        const float PlayerHeight = 1.3f;
        const float DefaultCupboardZoneRadius = 20f;
        const int MaxTrials = 150;
        private Vector3? CreateSpawnPosition()
        {
            for (int i = 0; i < MaxTrials; i++)
            {
                Vector3 randomPos = new Vector3(UnityEngine.Random.Range(-TerrainMeta.Size.x / 2, TerrainMeta.Size.x / 2), HeightToRaycast, UnityEngine.Random.Range(-TerrainMeta.Size.z / 2, TerrainMeta.Size.z / 2));
                if (ValidPosition(ref randomPos)) return randomPos;
            }
            return null;
        }
        private bool ValidPosition(ref Vector3 randomPos)
        {
            RaycastHit hitInfo;
            if (Physics.Raycast(randomPos, Vector3.down, out hitInfo, RaycastDistance, Layers.Solid)) randomPos.y = hitInfo.point.y;
            else return false;
            if (WaterLevel.Test(randomPos + new Vector3(0, PlayerHeight, 0))) return false;
            var colliders = new List<Collider>();
            Vis.Colliders(randomPos, 200f, colliders);
            if (colliders.Where(col => col.name.ToLower().Contains("prevent") && col.name.ToLower().Contains("building")).Count() > 0) return false;
            var entities = new List<BaseEntity>();
            Vis.Entities(randomPos, 3f, entities);
            if (entities.Where(ent => ent is BaseVehicle || ent is CargoShip || ent is BaseHelicopter || ent is BradleyAPC).Count() > 0) return false;
            var cupboards = new List<BuildingPrivlidge>();
            Vis.Entities(randomPos, DefaultCupboardZoneRadius + 10f, cupboards);
            if (cupboards.Count > 0) return false;
            return true;
        }
        public static class UI
        {
            public static void AddImage(ref CuiElementContainer container, string parrent, string name, string color, string sprite, string mat, string aMin, string aMax, string oMin, string oMax, string outline = "", string dist = "")
            {
                if (string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiImageComponent{Color = color, Material = "assets/icons/greyout.mat"},
                            new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                        }
                    });

                if (string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiImageComponent{Color = color},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });
            }

            public static void AddRawImage(ref CuiElementContainer container, string parrent, string name, string png, string color, string sprite, string mat, string aMin, string aMax, string oMin, string oMax)
            {
                if (string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiRawImageComponent{Color = color, Png = png},
                        new CuiOutlineComponent{Color = "0 0 0 1", Distance = "0.15 0.15"},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });
            }

            public static void AddTextRegular(ref CuiElementContainer container, string parrent, string name, string color, string text, TextAnchor align, int size, string aMin, string aMax, string oMin, string oMax, string outColor = "0 0 0 0", string font = "robotocondensed-bold.ttf", string dist = "0.5 0.5", float FadeIN = 0f, float FadeOut = 0f)
            {
                container.Add(new CuiElement()
                {
                    Parent = parrent,
                    Name = name,
                    FadeOut = FadeOut,
                    Components =
                    {
                        new CuiTextComponent{Color = color,Text = text, Align = align, FontSize = size, Font = "robotocondensed-regular.ttf", FadeIn = FadeIN},
                        new CuiOutlineComponent{Color = "0 0 0 1", Distance = "0.35 0.35"},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                });

            }


            public static void AddText(ref CuiElementContainer container, string parrent, string name, string color, string text, TextAnchor align, int size, string aMin, string aMax, string oMin, string oMax, string outColor = "0 0 0 0", string font = "robotocondensed-bold.ttf", string dist = "0.5 0.5", float FadeIN = 0f, float FadeOut = 0f)
            {
                container.Add(new CuiElement()
                {
                    Parent = parrent,
                    Name = name,
                    FadeOut = FadeOut,
                    Components =
                    {
                        new CuiTextComponent{Color = color,Text = text, Align = align, FontSize = size, Font = font, FadeIn = FadeIN},
                        new CuiOutlineComponent{Color = "0 0 0 1", Distance = "0.35 0.35"},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                });

            }

            public static void AddButton(ref CuiElementContainer container, string parrent, string name, string cmd, string close, string color, string sprite, string mat, string aMin, string aMax, string oMin, string oMax, string outline = "", string dist = "")
            {
                if (!string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiButtonComponent{Command = cmd, Color = color, Close = close, Sprite = sprite, Material = "assets/icons/greyout.mat", },
                            new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                        }
                    });

                if (!string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat) && !string.IsNullOrEmpty(outline))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiButtonComponent{Command = cmd, Color = color, Close = close, Sprite = sprite, Material = "assets/icons/greyout.mat", },
                            new CuiOutlineComponent{Color = outline, Distance = dist},
                            new CuiRectTransformComponent{ AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax }
                        }
                    });

                if (string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiButtonComponent{Command = cmd, Color = color, Close = close, Material = "assets/icons/greyout.mat", },
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });

                if (!string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiButtonComponent{Command = cmd, Color = color, Close = close, Sprite = sprite},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });

                if (string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiButtonComponent{Command = cmd, Color = color, Close = close, },
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });
            }
        }
        #endregion
    }
}