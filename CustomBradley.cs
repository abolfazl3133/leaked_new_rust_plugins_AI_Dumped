using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Random = UnityEngine.Random;
using Rust;
using HarmonyLib;

namespace Oxide.Plugins
{
    [Info("CustomBradley", "shaitobu", "1.5.2")]
    public class CustomBradley : RustPlugin
    {
        [PluginReference] Plugin ImageLibrary, Economics, ServerRewards, BankSystem;

        private static Harmony _harmony;
        public static CustomBradley Instance = null;
        public static CustomBradleyComponent controller;
        Dictionary<ulong, DataPlayer> _playerData = new Dictionary<ulong, DataPlayer>();

        class DataPlayer
        {
            public long Cooldown = 0;
        }

        #region Configuration
        private PluginConfig _config;
        private class PluginConfig
        {
            public class NPCConfig
            {
                public class ClothItem
                {
                    [JsonProperty("Shortname")]
                    public string Shortname;
                    [JsonProperty("SkinID")]
                    public ulong SkinID = 0;
                }

                public class SellItem
                {
                    [JsonProperty("DisplayName")]
                    public string DisplayName = "Vanilla Bradley";
                    [JsonProperty("Saleitem Image URL")]
                    public string url = "https://i.ibb.co/z6mr2kx/tanc01.png";

                    [JsonProperty("Description")]
                    public string BuyDescription = "Default Vanilla Bradley";
                    [JsonProperty("Permission")]
                    public string Permission = "";
                    [JsonProperty("ServerRewards costs")]
                    public int srCost = 10;
                    [JsonProperty("BankSystem costs")]
                    public int bkCost = 10;
                    [JsonProperty("Economics costs")]
                    public float ecCost = 10f;
                    [JsonProperty("Item displayname to pay with")]
                    public string itemName = "";
                    [JsonProperty("ItemID to pay with")]
                    public int itemPay = 0;
                    [JsonProperty("Item costs")]
                    public int scCost = 0;
                    [JsonProperty("Profilename")]
                    public string ProfileName = "easy";
                }

                [JsonProperty("Seller name")]
                public string DisplayName;
                [JsonProperty("Description in Shop")]
                public string Description;
                [JsonProperty("Local Position at Launchsite")]
                public Vector3 position;
                [JsonProperty("Rotation")]
                public float rotation;
                [JsonProperty("Clothing")]
                public List<ClothItem> Clothes;
                [JsonProperty("Tiers to sell")]
                public List<SellItem> ItemList;
            }
            public class UIConfig
            {
                [JsonProperty("Display countdown on spawn")]
                public bool UseCountdown = true;
                [JsonProperty("Countdown in seconds")]
                public float Countdown = 60;

                [JsonProperty("Countdown AnchorMin")]
                public string CountdownAnchorMin = "0 0.7694442";
                [JsonProperty("Countdown AnchorMax")]
                public string CountdownAnchorMax = "1 0.8249997";
                [JsonProperty("Timer AnchorMin")]
                internal string TimerPanelAnchorMin = "0.3445 0.1435";
                [JsonProperty("Timer AnchorMax")]
                internal string TimerPanelAnchorMax = "0.49 0.167";
            }

            [JsonProperty("Displayname for Economisc")]
            public string EconomicsName = "Eco";
            [JsonProperty("Displayname for ServerRewards")]
            public string ServerRewardsName = "RP";
            [JsonProperty("Displayname for BankSystem")]
            public string BankSystemName = "CR";
            [JsonProperty("Seller NPC")]
            public NPCConfig Seller;
            [JsonProperty("Broadcast spawns")]
            internal bool UseBroadcast = true;
            [JsonProperty("Use Countdown Sound")]
            internal bool UseCountdownSound = true;
            [JsonProperty("UI Settings")]
            internal UIConfig UI = new UIConfig();

            public static PluginConfig DefaultConfig => new PluginConfig
            {
                Seller = new NPCConfig()
                {
                    DisplayName = "Bradley Seller",
                    Description = "just doing my job",
                    position = new Vector3(263.8f, 3.5f, 56.5f),
                    rotation = 0.8f,
                    Clothes = new List<NPCConfig.ClothItem>()
                    {
                        new NPCConfig.ClothItem
                        {
                            Shortname = "hoodie",
                            SkinID = 1362361447
                        },
                        new NPCConfig.ClothItem
                        {
                            Shortname = "pants",
                            SkinID = 1320611965
                        },
                        new NPCConfig.ClothItem
                        {
                            Shortname = "mask.balaclava",
                            SkinID = 1362511189
                        },
                        new NPCConfig.ClothItem
                        {
                            Shortname = "shoes.boots",
                            SkinID = 1132775378
                        },
                        new NPCConfig.ClothItem
                        {
                            Shortname = "burlap.gloves",
                            SkinID = 1438131479
                        }
                    },
                    ItemList = new List<NPCConfig.SellItem>() {
                        new NPCConfig.SellItem(),
                        new NPCConfig.SellItem() { ProfileName = "normal", Permission = "CustomBradley.medium", url = "https://i.ibb.co/kcnMbPk/tanc02.png", DisplayName = "Always moving Bradley", BuyDescription = "Moving Vanilla Bradley with increased loot" }
                    }
                }
            };
        }

        public APCSettings[] _profiles;
        public Dictionary<string, LootSettings[]> _lootTables = new Dictionary<string, LootSettings[]>();

        public class LootSettings
        {
            public class ItemDefinition
            {
                [JsonProperty("Item Shortname")]
                public string Shortname = "scrap";
                [JsonProperty("Custom Displayname")]
                public string DisplayName = string.Empty;
                [JsonProperty("SkinID")]
                public ulong SkinID = 0;
                [JsonProperty("Item chance (0.01% - 100%)")]
                public float Chance = 100;
                [JsonProperty("Min. Amount")]
                public int AmountMin = 1;
                [JsonProperty("Max. Amount")]
                public int AmountMax = 1000;
            }

            [JsonProperty("LootTable")]
            public ItemDefinition[] Items = {
                new ItemDefinition()
                {
                    Shortname = "scrap",
                    DisplayName = "",
                    SkinID = 0,
                    AmountMin = 1,
                    AmountMax = 1000
                }
            };
        }

        public class APCSettings
        {
            public class BaseConfig
            {
                [JsonProperty("Profile name")]
                public string Name = "easy";
                [JsonProperty("Display name in chat")]
                internal object displayName = string.Empty;
                [JsonProperty("Cooldown")]
                public float Cooldown = 7200.0f;
                [JsonProperty("Cooldown modifier if failed to kill")]
                public float CooldownModifierOnFail = 0.5f;
                [JsonProperty("Time to kill")]
                public float DespawnTime = 900.0f;
                [JsonProperty("Time to loot after kill")]
                public float LootingTime = 600.0f;
                [JsonProperty("HP")]
                public float Health = 1000.0f;
                [JsonProperty("Recieved Damage modifier")]
                public float RecivedDamage = 1.0f;
                [JsonProperty("Accuracy")]
                public float Accuracy = 1.0f;
                [JsonProperty("Lock for players Team")]
                public bool LockForTeam = true;
                [JsonProperty("Allow players with cooldown to damage bradley")]
                public bool IgnoreCooldown = false;
                [JsonProperty("Destroy flame balls")]
                public bool DestroyFlames = true;
                [JsonProperty("Block FP NPCs from spawn")]
                public bool BlockNPCs = false;
            }

            public class LootConfig
            {
                [JsonProperty("Crate amount")]
                public int CrateAmount = 3;
                [JsonProperty("Loot scale per Attacker")]
                internal float AttackerScale = 0.0f;
                [JsonProperty("LootTable name")]
                public string LootTables;
            }

            public class MainTurretConfig
            {
                [JsonProperty("View distance")]
                public float ViewDistance = 100.0f;
                [JsonProperty("Can use basic rockets")]
                public bool UseRockets = false;
                [JsonProperty("Rocket speed modifier")]
                public float RocketSpeed = 1.0f;
                [JsonProperty("Delay between rocket bursts")]
                public float TimeBetweenRockets = 5.0f;
                [JsonProperty("Delay between each rocket in burst")]
                public float RocketDelay = 0.25f;
                [JsonProperty("Rockets amount per burst")]
                public int RocketsPerBurst = 4;
                [JsonProperty("Chance to launch homing missiles")]
                public float HomingChance = 60.0f;
                [JsonProperty("Lock to one target per burst")]
                internal bool lockOnce = true;
                [JsonProperty("Time rockets fly up")]
                public float FlyUpTime = 3f;
            }

            public class MachinegunConfig
            {
                [JsonProperty("Max. distance machine gun should shoot")]
                public float ShootDistance = 40f;
                [JsonProperty("Machinegun shots per burst")]
                public int ShotsPerBurst = 10;
                [JsonProperty("Delay between machinegun bursts")]
                public float TimeBetweenShots = 1.0f;
                [JsonProperty("Delay between each shot in burst")]
                public float ShotDelay = 0.06667f;
                [JsonProperty("Bullet damage")]
                public float BulletDamage = 15.0f;
            }

            public class BradleyBehaviorConfig
            {
                [JsonProperty("Lock the door at entrance Control point")]
                public bool BlockDoor = true;
                [JsonProperty("Prevent Bradley from hunting Players")]
                public bool AlwaysMove = false;
                [JsonProperty("Heat vision(ignore walls/buildings on targeting)")]
                public bool AlwaysShoot = false;
            }

            public BaseConfig Base = new BaseConfig();
            public BradleyBehaviorConfig Behavior = new BradleyBehaviorConfig();
            public MachinegunConfig Machinegun = new MachinegunConfig();
            public MainTurretConfig Cannon = new MainTurretConfig();
            public LootConfig Loot = new LootConfig();

            [JsonIgnore]
            public static APCSettings Default = new APCSettings();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
            }
            catch (Exception ex)
            {
                PrintError("Failed to load config file(is the config file corrupt ?)(" + ex.Message + ")");
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = PluginConfig.DefaultConfig;
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TextOnTimedKill"] = "Time's up. You've missed your chance.",
                ["TextOnSpawn"] = "You have {0} minutes to kill this Bradley",
                ["TeamLocked"] = "This Bradley is teamlocked and you've joined after its spawn.",
                ["Spawn broadcast"] = "<color=#63ff64>{0}</color> calls {1} in.",
                ["NotOwner"] = "This bradley is called for {0} and his/her team.",
                ["CooldownDamage"] = "You will be able to damage bradley in {0}",
                ["CooldownCall"] = "You will be able to call next bradley in {0}",
                ["CallInLable"] = "Call bradley",
                ["NoPermission"] = "No Permission",
                ["CountdownText"] = "Bradley will spawn in {0}",
                ["LeaveTimerText"] = "Bradley will leave in",
                ["LootTimerText"] = "Loot will dismiss in",
                ["LootTimeStarted"] = "You have {0} minutes to loot your bradley.",
                ["LockOnMessage"] = "Bradley is locking rockets on you."
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TextOnTimedKill"] = "Время вышло, танк уехал.",
                ["TextOnSpawn"] = "У вас есть {0} минут на то, чтобы уничтожить танк.",
                ["TeamLocked"] = "Вы вступили в команду после появления танка и не можете наносить урон.",
                ["Spawn broadcast"] = "<color=#63ff64>{0}</color> вызывает {1}.",
                ["NotOwner"] = "Танк вызван для {0} и команды.",
                ["CooldownDamage"] = "Вы можете атаковать танк через {0}",
                ["CooldownCall"] = "Новый танк можно вызвать через {0}",
                ["CallInLable"] = "ВЫЗВАТЬ",
                ["NoPermission"] = "НЕТ ПРАВ",
                ["CountdownText"] = "Танк появится через {0}",
                ["LeaveTimerText"] = "Танк уедет через",
                ["LootTimerText"] = "Лут исчезнет через",
                ["LootTimeStarted"] = "У вас есть {0} минут чтобы залутать танк.",
                ["LockOnMessage"] = "Танк наводит на вас снаряды."
            }, this, "ru");
        }
        public string _(string key, string userId, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, userId), args);
        }
        #endregion

        #region Oxide-Hooks
        bool wasBradley = true;
        public static GameObject gO = null;
        private void OnServerInitialized()
        {
            if (TerrainMeta.Path.Monuments.FirstOrDefault(p => p.name.Contains("launch_site_1")) == null)
            {
                Interface.Oxide.LogError("launch_site_1 not found. unloading...");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }

            LoadProfiles();
            LoadLootTables();

            Instance = this;
            foreach (var p in BaseNetworkable.serverEntities.OfType<VendingMachineMapMarker>().Where(i => i.markerShopName == _config.Seller.DisplayName))
                p.Kill();

            foreach (var p in BaseNetworkable.serverEntities.OfType<BasePlayer>().Where(i => !i.userID.IsSteamId() && i.displayName == _config.Seller.DisplayName))
                p.Kill();

            _playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, DataPlayer>>($"{Title}{System.IO.Path.DirectorySeparatorChar}playerData");
            timer.Once(2f, () =>
            {
                wasBradley = ConVar.Bradley.enabled;
                ConVar.Bradley.enabled = false;
                if (_harmony == null) _harmony = new Harmony("com.o_o.CustomBradley");
                _harmony.Patch(AccessTools.Method(typeof(BradleyAPC), nameof(BradleyAPC.DoWeapons)), new HarmonyMethod(typeof(BradleyPatch), "Prefix"));
                _harmony.Patch(AccessTools.Method(typeof(BradleyAPC), "TrySpawnScientists"), new HarmonyMethod(typeof(BradleyPatch), "Spawn"));                

                foreach (var i in _config.Seller.ItemList)
                {
                    if (!string.IsNullOrEmpty(i.Permission) && !permission.PermissionExists(i.Permission))
                        permission.RegisterPermission(i.Permission.ToLower(), this);

                    if (!string.IsNullOrEmpty(i.url))
                        AddImage(i.url, $"{i.url.GetHashCode()}");

                }
                gO = new GameObject("CustomBradleySpawner");
                controller = gO.AddComponent<CustomBradleyComponent>();
                SpawnSeller();
                SpawnDoor();
            });

        }

        BaseEntity door = null;
        private void SpawnDoor()
        {
            MonumentInfo monument = TerrainMeta.Path.Monuments.FirstOrDefault(x => x.name.Contains("launch_site_1"));
            var pos = monument.transform.TransformPoint(new Vector3(97.2f, 3.4f, -20.5f));
            var rot = monument.transform.rotation;

            door = GameManager.server.CreateEntity("assets/bundled/prefabs/static/door.hinged.industrial_a_f.prefab", pos, rot);
            door.Spawn();
            door.SetFlag(BaseEntity.Flags.Busy, true);
            door.SendNetworkUpdate();
        }

        private void LoadLootTables()
        {
            try
            {
                foreach (string s in Interface.Oxide.DataFileSystem.GetFiles($"{Interface.Oxide.DataDirectory}{System.IO.Path.DirectorySeparatorChar}{Title}{System.IO.Path.DirectorySeparatorChar}Loot"))
                {
                    string fileName = s.Split(System.IO.Path.DirectorySeparatorChar).Last().Split('.').First();

                    LootSettings[] d = Interface.Oxide.DataFileSystem.ReadObject<LootSettings[]>($"{Interface.Oxide.DataDirectory}{System.IO.Path.DirectorySeparatorChar}{Title}{System.IO.Path.DirectorySeparatorChar}Loot{System.IO.Path.DirectorySeparatorChar}{fileName}");
                    if (!_lootTables.ContainsKey(fileName))
                        _lootTables.Add(fileName, d);

                }
            }
            catch (Exception ex)
            {
                PrintError(ex.Message);
                _lootTables = new Dictionary<string, LootSettings[]>() {
                    {
                        "normal",
                        new LootSettings[] {
                            new LootSettings()
                            {
                                Items = new LootSettings.ItemDefinition[] {
                                    new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rifle",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 128,
        AmountMax= 300
      },new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rifle.incendiary",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 60,
        AmountMax= 200
      },new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rifle.explosive",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 60,
        AmountMax= 100
      },new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rifle.hv",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 80,
        AmountMax= 150
      },new LootSettings.ItemDefinition()
      {
        Shortname= "techparts",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 15,
        AmountMax= 25
      },new LootSettings.ItemDefinition()
      {
        Shortname= "explosive.timed",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 2,
        AmountMax= 4
      },new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rocket.fire",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 5,
        AmountMax= 8
      },new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rocket.hv",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 3,
        AmountMax= 5
      },new LootSettings.ItemDefinition()
      {
        Shortname= "rifle.l96",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 0,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "weapon.mod.8x.scope",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "lmg.m249",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 0,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "smg.thompson",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "smg.mp5",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 0,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "rifle.ak",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 0,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "rifle.bolt",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "rifle.lr300",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "hazmatsuit",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 0,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "hazmatsuit.spacesuit",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 0,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "hazmatsuit.arcticsuit",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 0,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "metal.refined",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 50,
        AmountMax= 100
      },new LootSettings.ItemDefinition()
      {
        Shortname= "crude.oil",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 100,
        AmountMax= 150
      },new LootSettings.ItemDefinition()
      {
        Shortname= "supply.signal",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 0,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "explosive.satchel",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 4,
        AmountMax= 6
      },new LootSettings.ItemDefinition()
      {
        Shortname= "metalpipe",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 12,
        AmountMax= 25
      },new LootSettings.ItemDefinition()
      {
        Shortname= "explosives",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 5,
        AmountMax= 10
      },new LootSettings.ItemDefinition()
      {
        Shortname= "gunpowder",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 500,
        AmountMax= 1000
      }
                                }
                            }
                        }
                    },
                    {
                        "hard",
                        new LootSettings[] {
                            new LootSettings()
                            {
                                Items = new LootSettings.ItemDefinition[] {
                                    new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rifle",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 300,
        AmountMax= 500
      },new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rifle.incendiary",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 200,
        AmountMax= 300
      },new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rifle.explosive",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 150,
        AmountMax= 200
      },new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rifle.hv",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 150,
        AmountMax= 250
      },new LootSettings.ItemDefinition()
      {
        Shortname= "techparts",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 25,
        AmountMax= 35
      },new LootSettings.ItemDefinition()
      {
        Shortname= "explosive.timed",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 10,
        AmountMax= 15
      },new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rocket.fire",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 10,
        AmountMax= 15
      },new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rocket.hv",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 10,
        AmountMax= 15
      },new LootSettings.ItemDefinition()
      {
        Shortname= "rifle.l96",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "weapon.mod.8x.scope",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "lmg.m249",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "smg.thompson",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "smg.mp5",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "rifle.ak",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "rifle.ak",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "rifle.lr300",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "hazmatsuit",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "hazmatsuit.spacesuit",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 0,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "hazmatsuit.arcticsuit",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "metal.refined",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 200,
        AmountMax= 300
      },new LootSettings.ItemDefinition()
      {
        Shortname= "crude.oil",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1000,
        AmountMax= 1500
      },new LootSettings.ItemDefinition()
      {
        Shortname= "supply.signal",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "explosive.satchel",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 8,
        AmountMax= 15
      },new LootSettings.ItemDefinition()
      {
        Shortname= "metalpipe",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 30,
        AmountMax= 50
      },new LootSettings.ItemDefinition()
      {
        Shortname= "explosives",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 30,
        AmountMax= 45
      },new LootSettings.ItemDefinition()
      {
        Shortname= "gunpowder",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 3500,
        AmountMax= 5000
      },new LootSettings.ItemDefinition()
      {
        Shortname= "metal.facemask",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "metal.facemask",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "metal.plate.torso",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "metal.plate.torso",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      }
                                }
                            }
                        }
                    },
                    {
                        "extreme",
                        new LootSettings[] {
                            new LootSettings()
                            {
                                Items = new LootSettings.ItemDefinition[] {
                                    new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rifle",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 300,
        AmountMax= 500
      },new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rifle",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 300,
        AmountMax= 500
      },new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rifle.incendiary",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 300,
        AmountMax= 500
      },new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rifle.explosive",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 250,
        AmountMax= 400
      },new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rifle.hv",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 250,
        AmountMax= 450
      },new LootSettings.ItemDefinition()
      {
        Shortname= "techparts",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 35,
        AmountMax= 50
      },new LootSettings.ItemDefinition()
      {
        Shortname= "explosive.timed",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 20,
        AmountMax= 25
      },new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rocket.fire",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 20,
        AmountMax= 25
      },new LootSettings.ItemDefinition()
      {
        Shortname= "ammo.rocket.hv",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 20,
        AmountMax= 25
      },new LootSettings.ItemDefinition()
      {
        Shortname= "launcher",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "rifle.l96",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "weapon.mod.8x.scope",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "lmg.m249",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "lmg.m249",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "rifle.ak",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "rifle.ak",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "rifle.lr300",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "hazmatsuit",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "hazmatsuit.spacesuit",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "hazmatsuit.arcticsuit",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "metal.refined",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 300,
        AmountMax= 500
      },new LootSettings.ItemDefinition()
      {
        Shortname= "crude.oil",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 3000,
        AmountMax= 4500
      },new LootSettings.ItemDefinition()
      {
        Shortname= "supply.signal",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "supply.signal",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "explosive.satchel",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 8,
        AmountMax= 15
      },new LootSettings.ItemDefinition()
      {
        Shortname= "explosive.satchel",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 8,
        AmountMax= 15
      },new LootSettings.ItemDefinition()
      {
        Shortname= "explosive.satchel",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 8,
        AmountMax= 15
      },new LootSettings.ItemDefinition()
      {
        Shortname= "metalpipe",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 40,
        AmountMax= 60
      },new LootSettings.ItemDefinition()
      {
        Shortname= "explosives",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 30,
        AmountMax= 45
      },new LootSettings.ItemDefinition()
      {
        Shortname= "explosives",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 30,
        AmountMax= 45
      },new LootSettings.ItemDefinition()
      {
        Shortname= "gunpowder",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 3500,
        AmountMax= 5000
      },new LootSettings.ItemDefinition()
      {
        Shortname= "gunpowder",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 3500,
        AmountMax= 5000
      },new LootSettings.ItemDefinition()
      {
        Shortname= "metal.facemask",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "metal.facemask",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "metal.plate.torso",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      },new LootSettings.ItemDefinition()
      {
        Shortname= "metal.plate.torso",
        DisplayName= "",
        SkinID= 0,
        AmountMin= 1,
        AmountMax= 1
      }
                                }
                            }
                        }
                    }
                };

                foreach (var l in _lootTables)
                    Interface.Oxide.DataFileSystem.WriteObject<LootSettings[]>($"{Interface.Oxide.DataDirectory}{System.IO.Path.DirectorySeparatorChar}{Title}{System.IO.Path.DirectorySeparatorChar}Loot{System.IO.Path.DirectorySeparatorChar}{l.Key}", l.Value);
            }
        }

        private void LoadProfiles()
        {
            try
            {
                _profiles = Interface.Oxide.DataFileSystem.ReadObject<APCSettings[]>($"{Title}{System.IO.Path.DirectorySeparatorChar}profiles");
            }
            catch
            {
                _profiles = new APCSettings[] { APCSettings.Default,
                        new APCSettings()
                        {
                            Base = new APCSettings.BaseConfig
                            {
                                Name = "normal",
                                displayName = "lazy bradley",
                                Cooldown = 7200.0f,
                                DespawnTime = 900.0f,
                                LootingTime = 600.0f,
                                Health = 2000.0f,
                                RecivedDamage = 1f,
                                Accuracy = 1f,
                                LockForTeam = true
                            },
                            Behavior = new APCSettings.BradleyBehaviorConfig
                            {
                                AlwaysMove = true,
                                AlwaysShoot = false,
                                BlockDoor = false
                            },
                            Cannon = new APCSettings.MainTurretConfig
                            {
                                ViewDistance = 100.0f,
                                UseRockets = false,
                                HomingChance = 0f,
                                lockOnce = false,
                                RocketSpeed = 1.0f,
                                TimeBetweenRockets = 5.0f,
                                RocketDelay = 0.25f,
                                RocketsPerBurst = 3
                            },
                            Machinegun = new APCSettings.MachinegunConfig
                            {
                                ShotsPerBurst = 10,
                                TimeBetweenShots = 1f,
                                ShotDelay = 0.06667f,
                                BulletDamage = 15.0f,
                                ShootDistance = 80f
                            },
                            Loot = new APCSettings.LootConfig
                            {
                                CrateAmount = 3,
                                AttackerScale = 0f,
                                LootTables = "normal"
                            }
                        }
                    };
            }
            Interface.Oxide.DataFileSystem.WriteObject<APCSettings[]>($"{Title}{System.IO.Path.DirectorySeparatorChar}profiles", _profiles);
        }

        void Unload()
        {
            if (gO)
                UnityEngine.Object.DestroyImmediate(gO);

            if (_harmony != null)
                _harmony.UnpatchAll("com.o_o.CustomBradley");

            DestroyGUI();
            if (startCorutine != null)
            {
                ServerMgr.Instance.StopCoroutine(startCorutine);
                startCorutine = null;
            }

            foreach (var b in BaseNetworkable.serverEntities.OfType<BradleyAPC>().Where(x => x.skinID == 1231236555))
                b.Kill();

            foreach (var p in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(p, Layer + p.userID);

            foreach (var p in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(p, "BradleyUIPanel");

            if (seller)
                seller.Kill();
            if (door)
                door.Kill();

            Interface.Oxide.DataFileSystem.WriteObject($"{Title}{System.IO.Path.DirectorySeparatorChar}playerData", _playerData);
            ConVar.Bradley.enabled = wasBradley;
        }
        #endregion

        #region Hooks
        object CanPopulateLoot(LootContainer container)
        {
            if (controller != null && controller.currentAPC && controller.IsBradleyCrate(container))
                return false;
            return null;
        }

        void OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (info == null || !entity) return;
            if (seller == entity)
                info?.damageTypes.ScaleAll(0f);

            var bradley = (info.WeaponPrefab?.creatorEntity as BradleyAPC ?? info.Initiator as BradleyAPC);
            if (info != null && bradley && bradley == controller.currentAPC)
            {
                if (!(entity is BasePlayer) || !(entity as BasePlayer).userID.IsSteamId())
                    info.damageTypes.ScaleAll(0f);
            }
        }

        void OnEntityTakeDamage(BradleyAPC entity, HitInfo info)
        {
            if (entity == null || info == null)
                return;
            if (info.Initiator is BradleyAPC)
            {
                info.damageTypes.ScaleAll(0f);
                return;
            }

            var player = info.InitiatorPlayer;
            if (player == null || player.IsAdmin)
                return;

            if (!player.userID.IsSteamId())
            {
                info.damageTypes.ScaleAll(0f);
                return;
            }
            if (!controller || controller.currentAPC != entity)
                return;

            if (controller.apcSettings.Base.LockForTeam && !CanInteractWith(player, entity))
            {
                info.damageTypes.ScaleAll(0);

                if (player.Team != null && player.Team.members.Contains(controller.currentOwner.userID))
                {
                    SendReply(player, _("TeamLocked", player.UserIDString));
                    return;
                }

                BasePlayer owner = controller.currentOwner;
                if (owner)
                    SendReply(player, _("NotOwner", player.UserIDString, owner.displayName));
                return;
            }

            DataPlayer f;
            if (controller.apcSettings.Base.IgnoreCooldown || CanDamageBradley(player, entity, out f))
            {
                info.damageTypes.ScaleAll(controller.apcSettings.Base.RecivedDamage);

                return;
            }
            info.damageTypes.ScaleAll(0f);
            SendReply(player, _("CooldownDamage", player.UserIDString, TimeSpan.FromTicks(f.Cooldown - DateTime.Now.Ticks).ToString(@"hh\:mm\:ss")));
        }

        private object OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (info.HitEntity is ServerGib && attacker && !attacker.IsNpc)
            {
                if (controller.IsBradleyPart(info.HitEntity) && controller.apcSettings.Base.LockForTeam)
                {
                    if (!CanInteractWith(attacker, info.HitEntity))
                    {
                        BasePlayer owner = controller.currentOwner;
                        if (owner)
                            SendReply(attacker, _("NotOwner", attacker.UserIDString, owner.displayName));
                        info.damageTypes.ScaleAll(0);
                        return false;
                    }
                }
            }

            return null;
        }

        void OnEntityDeath(BradleyAPC entity, HitInfo info)
        {
            if (entity == null) return;
            if (entity == controller.currentAPC)
            {
                controller.OnAPCDeath();
            }
        }

        object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (player == null || container == null || controller is null) return null;
            if (controller.IsBradleyCrate(container))
            {
                if (!CanInteractWith(player, container))
                {
                    BasePlayer owner = controller.currentOwner;
                    if (owner)
                        SendReply(player, _("NotOwner", player.UserIDString, owner.displayName));
                    return false;
                }
            }
            return null;
        }
        object CanLootEntity(BasePlayer player, LiquidContainer container)
        {
            if ($"{_config.Seller.DisplayName}/{_config.Seller.DisplayName.GetHashCode()}" == container.name)
            {
                InterfaceInit(player);
                return false;
            }

            return null;
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == _currentPlayer)
                CloseUI();
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == _currentPlayer)
                CloseUI();
        }

        object OnNpcTarget(BaseEntity npc, BaseEntity target)
        {
            if (target == seller)
            {
                return false;
            }
            return null;
        }

        private object CanBradleyApcTarget(BradleyAPC apc, BasePlayer player)
        {
            if (controller?.currentAPC != apc)
                return null;

            return CanInteractWith(player, apc) ? controller.apcSettings.Behavior.AlwaysShoot ? true : (object)null : false;
        }

        object OnBradleyApcThink(BradleyAPC bradley)
        {
            if (controller && bradley == controller.currentAPC && controller.apcSettings.Behavior.AlwaysMove)
            {
                return DoBradleyAI(bradley);
            }
            return null;
        }
        #endregion

        object DoBradleyAI(BradleyAPC apc)
        {
            foreach (var target in apc.targetList)
            {
                if (target.IsValid() && target.IsVisible())
                {
                    apc.mainGunTarget = target.entity as BaseCombatEntity;
                    break;
                }
                else
                {
                    apc.mainGunTarget = null;
                }
            }

            apc.UpdateMovement_Patrol();

            apc.AdvancePathMovement(false);
            float num = Vector3.Distance(apc.transform.position, apc.destination);
            float value = Vector3.Distance(apc.transform.position, apc.finalDestination);
            if (num > apc.stoppingDist)
            {
                Vector3 lhs = BradleyAPC.Direction2D(apc.destination, apc.transform.position);
                float num2 = Vector3.Dot(lhs, apc.transform.right);
                float num3 = Vector3.Dot(lhs, apc.transform.right);
                float num4 = Vector3.Dot(lhs, -apc.transform.right);
                if (Vector3.Dot(lhs, -apc.transform.forward) > num2)
                {
                    if (num3 >= num4)
                    {
                        apc.turning = 1f;
                    }
                    else
                    {
                        apc.turning = -1f;
                    }
                }
                else
                {
                    apc.turning = Mathf.Clamp(num2 * 3f, -1f, 1f);
                }
                float num5 = 1f - Mathf.InverseLerp(0f, 0.3f, Mathf.Abs(apc.turning));
                float num6 = Mathf.InverseLerp(0.1f, 0.4f, Vector3.Dot(apc.transform.forward, Vector3.up));
                apc.throttle = (0.1f + Mathf.InverseLerp(0f, 20f, value) * 4f) * num5 + num6;
            }
            apc.DoWeaponAiming();
            apc.SendNetworkUpdate();

            return false;
        }

        [ConsoleCommand("CustomBradley")]
        private void CallBradley(ConsoleSystem.Arg a)
        {
            var player = a.Player();
            if (player && a.Args != null)
            {
                var type = a.Args[0];
                switch (type)
                {
                    case "close":
                        CloseUI();
                        break;
                    case "page":
                        Open(player, int.Parse(a.Args[1]));
                        break;
                    case "buy":
                        APCSettings tank = APCSettings.Default;
                        PluginConfig.NPCConfig.SellItem fItem = null;
                        if (a.Args.Length > 1)
                        {
                            fItem = _config.Seller.ItemList.FirstOrDefault(x => $"{x.GetHashCode()}" == a.Args[1]);
                            if (fItem != null)
                            {
                                tank = _profiles.FirstOrDefault(x => x.Base.Name == fItem.ProfileName);
                            }
                        }

                        if (fItem != null)
                        {
                            if (!string.IsNullOrEmpty(fItem.Permission) && permission.UserHasPermission(player.UserIDString, fItem.Permission))
                            {
                                SpawnBradley(player, tank);
                                return;
                            }
                            else if (fItem.scCost > 0 && player.inventory.GetAmount(fItem.itemPay) >= fItem.scCost)
                            {
                                player.inventory.Take(null, fItem.itemPay, fItem.scCost);
                                SpawnBradley(player, tank);
                                return;
                            }
                            else if (Economics && fItem.ecCost > 0 && (double)Economics.Call("Balance", (ulong)player.userID) >= fItem.ecCost)
                            {
                                Economics.Call("Withdraw", (ulong)player.userID, (double)fItem.ecCost);
                                SpawnBradley(player, tank);
                                return;
                            }
                            else if (ServerRewards && fItem.srCost > 0 && (int)ServerRewards.Call("CheckPoints", (ulong)player.userID) >= fItem.srCost)
                            {
                                ServerRewards.Call("TakePoints", (ulong)player.userID, fItem.srCost);
                                SpawnBradley(player, tank);
                                return;
                            }
                            else if (BankSystem && fItem.bkCost > 0 && (int)BankSystem.Call("Balance", (ulong)player.userID) >= fItem.bkCost)
                            {
                                BankSystem.Call("Withdraw", (ulong)player.userID, fItem.srCost);
                                SpawnBradley(player, tank);
                                return;
                            }

                            if (string.IsNullOrEmpty(fItem.Permission) && (!BankSystem || fItem.bkCost == 0) && (!ServerRewards || fItem.srCost == 0) && (!Economics || fItem.ecCost == 0) && fItem.scCost == 0)
                            {
                                SpawnBradley(player, tank);
                                return;
                            }
                        }
                        break;
                }
            }
        }

        float nextSpawn = 0; bool requested = false;
        Coroutine startCorutine = null;
        BasePlayer currentBuyer = null;
        private void SpawnBradley(BasePlayer player, APCSettings tank)
        {
            timer.In(0.3f, () =>
            {
                if (!requested)
                {
                    requested = true;
                    currentBuyer = player;
                    CloseUI();
                    ToggleSeller();
                    nextSpawn = Time.time + (_config.UI.UseCountdown ? _config.UI.Countdown : 1);
                    if (_config.UI.UseCountdown)
                        startCorutine = ServerMgr.Instance.StartCoroutine(CustomBradley_DrawGUI());

                    if (_config.UseBroadcast)
                    {
                        foreach (var p in BasePlayer.activePlayerList)
                            p.ChatMessage(_("Spawn broadcast", p.UserIDString, player.displayName, tank.Base.displayName));
                    }

                    timer.In(_config.UI.UseCountdown ? _config.UI.Countdown : 1f, () =>
                    {
                        requested = false;
                        nextSpawn = 0;
                        if (startCorutine != null)
                            ServerMgr.Instance.StopCoroutine(startCorutine);

                        DestroyGUI();
                        currentBuyer = null;
                        startCorutine = null;

                        foreach (var b in BaseNetworkable.serverEntities.OfType<BradleyAPC>().Where(x => x.skinID == 1231236555))
                            b.Kill();

                        var position = BradleySpawner.singleton.path.interestZones[Random.Range(0, BradleySpawner.singleton.path.interestZones.Count)].transform.position;
                        var bradleyApc = GameManager.server.CreateEntity(BradleySpawner.singleton.bradleyPrefab.resourcePath, position).GetComponent<BradleyAPC>();
                        bradleyApc.InstallPatrolPath(BradleySpawner.singleton.path);
                        bradleyApc.skinID = 1231236555;
                        bradleyApc.maxCratesToSpawn = tank.Loot.CrateAmount;
                        bradleyApc.Spawn();
                        bradleyApc.InitializeHealth(tank.Base.Health, tank.Base.Health);
                        NextTick(() => controller.InitBradley(player, bradleyApc, tank));
                        bradleyApc.gameObject.AddComponent<AuraController>();
                        bradleyApc.searchRange = tank.Cannon.ViewDistance;
                        bradleyApc.viewDistance = tank.Cannon.ViewDistance;
                        bradleyApc.coaxBurstLength = tank.Machinegun.ShotsPerBurst;
                        bradleyApc.coaxFireRate = tank.Machinegun.ShotDelay;
                        bradleyApc.topTurretFireRate = tank.Machinegun.ShotDelay;
                        bradleyApc.bulletDamage = tank.Machinegun.BulletDamage;

                        if (!_playerData.ContainsKey(player.userID))
                            _playerData[player.userID] = new DataPlayer();

                        Instance.Puts($"{player.displayName} calls bradley - {tank.Base.displayName}.");
                        player.ChatMessage(string.Format(_("TextOnSpawn", player.UserIDString, tank.Base.DespawnTime / 60)));
                        Interface.Oxide.DataFileSystem.WriteObject($"{Title}{System.IO.Path.DirectorySeparatorChar}playerData", _playerData);
                    });
                }
            });
        }

        private bool CanInteractWith(BasePlayer player, BaseEntity entity)
        {
            if (!controller.apcSettings.Base.LockForTeam || !entity.OwnerID.IsSteamId()) return true;

            if (player == controller.currentOwner || player.Team != null && controller.GetAllPlayersInTeam().Contains(player))
                return true;
            return false;
        }

        private bool CanDamageBradley(BasePlayer player, BradleyAPC bradley, out DataPlayer f)
        {
            f = null;
            if (!player.userID.IsSteamId()) return false;
            if (!_playerData.TryGetValue(player.userID, out f))
                f = _playerData[player.userID] = new DataPlayer();

            if (f.Cooldown < DateTime.Now.Ticks)
                return true;

            return false;
        }

        BasePlayer seller = null;
        VendingMachineMapMarker marker = null;
        void SpawnSeller()
        {
            MonumentInfo monument = TerrainMeta.Path.Monuments.FirstOrDefault(x => x.name.Contains("launch_site_1"));
            var pos = monument.transform.TransformPoint(_config.Seller.position);
            var rot = monument.transform.rotation;

            seller = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", pos) as BasePlayer;
            seller.Spawn();
            seller.OverrideViewAngles(rot.eulerAngles * _config.Seller.rotation);

            marker = (VendingMachineMapMarker)GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", pos);
            marker.markerShopName = _config.Seller.DisplayName;
            marker.Spawn();
            marker.InvokeRepeating(() => { marker.markerShopName = _config.Seller.DisplayName; marker.SendNetworkUpdate(); }, 3f, 3f);

            var trigger = GameManager.server.CreateEntity("assets/prefabs/deployable/waterpurifier/waterstorage.prefab", seller.transform.position + new Vector3(0, 0.85f, 0), rot);
            UnityEngine.Object.Destroy(trigger.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.Destroy(trigger.GetComponent<GroundWatch>());
            trigger.name = $"{_config.Seller.DisplayName}/{_config.Seller.DisplayName.GetHashCode()}";
            trigger.SetParent(seller, true, true);
            trigger.Spawn();

            trigger = GameManager.server.CreateEntity("assets/prefabs/deployable/waterpurifier/waterstorage.prefab", seller.transform.position + new Vector3(0, 1.2f, 0), rot);
            UnityEngine.Object.Destroy(trigger.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.Destroy(trigger.GetComponent<GroundWatch>());
            trigger.name = $"{_config.Seller.DisplayName}/{_config.Seller.DisplayName.GetHashCode()}";
            trigger.SetParent(seller, true, true);
            trigger.Spawn();

            foreach (var npcCloth in _config.Seller.Clothes)
                seller.inventory.containerWear.AddItem(ItemManager.FindItemDefinition(npcCloth.Shortname), 1, npcCloth.SkinID);

            seller.displayName = _config.Seller.DisplayName;
            seller.SendNetworkUpdateImmediate();
        }

        void ToggleSeller()
        {
            if (seller)
            {
                marker.Kill();
                seller.Kill();
            }
            else
                SpawnSeller();
        }

        double GetBradleyCooldown(ulong userID)
        {
            double retValue = 0;
            if (_playerData.ContainsKey(userID))
                retValue = TimeSpan.FromTicks(Math.Max(_playerData[userID].Cooldown - DateTime.Now.Ticks, 0)).TotalSeconds;
            return retValue;
        }

        #region UI
        private static string Layer = "CustomBradley";
        private static string LayerTimer = "CustomBradley_Timer";
        private static string regular = "robotocondensed-regular.ttf";

        CuiPanel _mainPanel = new CuiPanel()
        {
            CursorEnabled = true,
            RectTransform =
            {
                AnchorMin = "0 0",
                AnchorMax = "1 1"
            },
            Image =
            {
                Png = "assets/standard assets/effects/imageeffects/textures/noise.png",
                Color = HexToRustFormat("#303038F3"),
                Material = "assets/icons/greyout.mat"
            }
        };

        CuiButton Close = new CuiButton()
        {
            RectTransform =
            {
                AnchorMin = "0.935 0.9",
                AnchorMax = "0.98 0.98"
            },
            Button =
            {
                Sprite = "assets/icons/close.png",
                Color = "0.75 0.75 0.75 0.75",
                Command = "CustomBradley close"
            },
            Text =
            {
                Text = ""
            }
        };

        BasePlayer _currentPlayer = null;
        void CloseUI()
        {
            if (_currentPlayer)
            {
                CuiHelper.DestroyUi(_currentPlayer, Layer + _currentPlayer.userID);
                _currentPlayer = null;
            }
        }

        void InterfaceInit(BasePlayer player)
        {
            if (_currentPlayer)
            {
                player.ChatMessage("Oops. I'm currently busy.");
                return;
            }

            if (controller?.currentAPC)
            {
                SendReply(player, "Someone already called Bradley in.");
                return;
            }

            DataPlayer f;
            if (_playerData.TryGetValue(player.userID, out f))
            {
                var cd = Math.Max(f.Cooldown - DateTime.Now.Ticks, 0);
                if (cd > 0 && !player.IsAdmin)
                {
                    SendReply(player, _("CooldownCall", player.UserIDString, TimeSpan.FromTicks(cd).ToString(@"hh\:mm\:ss")));
                    return;
                }
            }

            _currentPlayer = player;
            CuiHelper.DestroyUi(player, Layer + player.userID);
            var cont = new CuiElementContainer();
            cont.Add(new CuiPanel()
            {
                CursorEnabled = true,
                RectTransform = {
                    AnchorMin = "0.2 0.2",
                    AnchorMax = "0.8 0.8"
                },
                Image = { Color = "0.25 0.25 0.25 0" }
            }, "Hud", Layer + player.userID);
            cont.Add(_mainPanel, Layer + player.userID, Layer + "MAIN2");
            cont.Add(Close, Layer + player.userID);
            cont.Add(new CuiElement()
            {
                Parent = Layer + "MAIN2",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = _config.Seller.DisplayName,
                        Align = TextAnchor.MiddleCenter,
                        FontSize = 20
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = $"0.02 0.84",
                        AnchorMax = $"0.984 0.96"
                    }
                }
            });

            cont.Add(new CuiElement()
            {
                Parent = Layer + "MAIN2",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Color = HexToRustFormat("#a6b9ca"),
                        Font = regular,
                        Text = _config.Seller.Description,
                        Align = TextAnchor.MiddleCenter,
                        FontSize = 14
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.02 0.81",
                        AnchorMax = "0.984 0.88"
                    }
                }
            });
            CuiHelper.AddUi(player, cont);
            Open(player);
        }

        void Open(BasePlayer player, int page = 0)
        {
            var width = 0.2f;
            var spacing = 0.03f;
            CuiHelper.DestroyUi(player, Layer + "MAIN2_panel");

            var cont = new CuiElementContainer();
            cont.Add(new CuiPanel()
            {
                RectTransform = {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                Image = { Color = "0.25 0.25 0.25 0" }
            }, Layer + "MAIN2", Layer + "MAIN2_panel");

            var itemsList = _config.Seller.ItemList.Skip(page).Take(4);
            if (page > 0)
            {
                cont.Add(new CuiButton
                {
                    Button =
                    {
                        Color = HexToRustFormat("#3a585a"),
                        Command = $"CustomBradley page {page-1}",
                    },
                    Text =
                    {
                        Text = "<--",
                        Align = TextAnchor.MiddleCenter,
                        Color = HexToRustFormat("#01cdd4")
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.77 0.9", AnchorMax = "0.82 0.95"
                    }
                }, Layer + "MAIN2_panel");
            }

            if (page + 4 < _config.Seller.ItemList.Count)
            {
                cont.Add(new CuiButton
                {
                    Button =
                    {
                        Color = HexToRustFormat("#3a585a"),
                        Command = $"CustomBradley page {page+1}",
                    },
                    Text =
                    {
                        Text = "-->",
                        Align = TextAnchor.MiddleCenter,
                        Color = HexToRustFormat("#01cdd4")
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.85 0.9", AnchorMax = "0.9 0.95"
                    }
                }, Layer + "MAIN2_panel");
            }


            var pos = (1f - (itemsList.Count() * width + (itemsList.Count() - 1) * spacing)) / 2;
            for (int i = 0; i < itemsList.Count(); i++)
            {
                var fItem = itemsList.ElementAt(i);
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "MAIN2_panel",
                    Name = Layer + "Fon" + i,
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = HexToRustFormat("#2c2c33F6")
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = $"{pos} 0.0725925",
                            AnchorMax = $"{pos + width} 0.7877776"
                        }
                    }
                });

                cont.Add(new CuiPanel()
                {
                    Image =
                    {
                        Color = "0 0 0 0.6"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.035 0.55",
                        AnchorMax = "0.95 0.98"
                    }
                }, Layer + "Fon" + i, Layer + "Fon2" + i);
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Fon2" + i,
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Png = GetImage($"{fItem.url.GetHashCode()}")
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                });

                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Fon" + i,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Align = TextAnchor.MiddleCenter,
                            FontSize = 11,
                            Font = regular,
                            Text = fItem.DisplayName
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.05 0.48",
                            AnchorMax = "0.95 0.52"
                        }
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Fon" + i,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Color = "0.65 0.65 0.65 1",
                            Align = TextAnchor.UpperCenter,
                            FontSize = 9,
                            Font = regular,
                            Text = fItem.BuyDescription
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.05 0.175",
                            AnchorMax = "0.95 0.46"
                        }
                    }
                });

                if (CanAccess(player, fItem))
                {
                    cont.Add(new CuiButton()
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.05 0.018",
                            AnchorMax = "0.95 0.15"
                        },
                        Button =
                        {
                            Color = HexToRustFormat("#3a585a"),
                            Command = $"CustomBradley buy {fItem.GetHashCode()}"
                        },
                        Text =
                        {
                            Color = HexToRustFormat("#01cdd4"),
                            Text = !string.IsNullOrEmpty(fItem.Permission) || fItem.itemPay != 0 && fItem.scCost > 0 || fItem.srCost > 0 && ServerRewards || fItem.bkCost > 0 && BankSystem || fItem.ecCost > 0 && Economics ?
                                (
                                    !string.IsNullOrEmpty(fItem.Permission) && permission.UserHasPermission(player.UserIDString, fItem.Permission) ? _("CallInLable", player.UserIDString)
                                    : fItem.itemPay != 0 && fItem.scCost > 0 && player.inventory.GetAmount(fItem.itemPay) >= fItem.scCost ? $"{fItem.scCost} {fItem.itemName}"
                                    : fItem.ecCost > 0 && Economics && (double)Economics.Call("Balance", (ulong)player.userID) >= fItem.ecCost ? $"{fItem.ecCost} {_config.EconomicsName}"
                                    : fItem.srCost > 0 && ServerRewards && (int)ServerRewards.Call("CheckPoints", (ulong)player.userID) >= fItem.srCost ? $"{fItem.srCost} {_config.ServerRewardsName}"
                                    : fItem.bkCost > 0 && BankSystem && !BankSystem.Call<bool>("HasCard", (ulong)player.userID) ? "NO CARD FOUND"
                                    : fItem.bkCost > 0 && BankSystem && (int)BankSystem.Call("Balance", (ulong)player.userID) >= fItem.bkCost ? $"{fItem.bkCost} {_config.BankSystemName}"
                                    : "How the hell you get here?"
                                )
                            : _("CallInLable", player.UserIDString),
                            FontSize = 9,
                            Align = TextAnchor.MiddleCenter
                        }
                    }, Layer + "Fon" + i);
                }
                else
                {
                    cont.Add(new CuiButton()
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.05 0.018",
                            AnchorMax = "0.95 0.15"
                        },
                        Button =
                        {
                            Color = HexToRustFormat("#181816"),
                            Command = $"CustomBradley none"
                        },
                        Text =
                        {
                            Color = HexToRustFormat("#ccccc8"),
                            Text =
                                    ((!string.IsNullOrEmpty(fItem.Permission) ? _("NoPermission", player.UserIDString) : string.Empty)
                                    + (fItem.itemPay != 0 && fItem.scCost > 0 ? $"\n{fItem.scCost} {fItem.itemName}" : string.Empty)
                                    + (fItem.ecCost > 0 && Economics ? $"\n{fItem.ecCost} {_config.EconomicsName}" : string.Empty)
                                    + (fItem.srCost > 0 && ServerRewards ? $"\n{fItem.srCost} {_config.ServerRewardsName}" : string.Empty)
                                    + (fItem.bkCost > 0 && BankSystem ? $"\n{fItem.bkCost} {_config.BankSystemName}" : string.Empty)).Trim(),
                            FontSize = 9,
                            Align = TextAnchor.MiddleCenter
                        }
                    }, Layer + "Fon" + i);
                }
                pos += width + spacing;
            }
            CuiHelper.AddUi(player, cont);
        }

        private bool CanAccess(BasePlayer player, PluginConfig.NPCConfig.SellItem fItem)
        {
            return string.IsNullOrEmpty(fItem.Permission) && (fItem.scCost == 0) && (fItem.srCost == 0 || !ServerRewards) && (fItem.ecCost == 0 || !Economics) && (fItem.bkCost == 0 || !BankSystem)
                ||
                !string.IsNullOrEmpty(fItem.Permission) && permission.UserHasPermission(player.UserIDString, fItem.Permission)
                ||
                (fItem.scCost > 0 && player.inventory.GetAmount(fItem.itemPay) >= fItem.scCost)
                ||
                (Economics && fItem.ecCost > 0 && (double)Economics.Call("Balance", (ulong)player.userID) >= fItem.ecCost)
                ||
                (ServerRewards && fItem.srCost > 0 && (int)ServerRewards.Call("CheckPoints", (ulong)player.userID) >= fItem.srCost)
                ||
                (BankSystem && BankSystem.Call<bool>("HasCard", (ulong)player.userID) && fItem.bkCost > 0 && (int)BankSystem.Call("Balance", (ulong)player.userID) >= fItem.bkCost);
        }

        private IEnumerator CustomBradley_DrawGUI()
        {
            var text = currentBuyer ? _("CountdownText", currentBuyer.UserIDString, "#timer") : string.Empty;
            while (nextSpawn - Time.time > 0)
            {
                var sec = nextSpawn - Time.time;
                if (currentBuyer && currentBuyer.IsConnected)
                {
                    CuiElementContainer container = new CuiElementContainer
                    {
                        {
                            new CuiPanel
                            {
                                Image = { Color = HexToRustFormat("#00000070") },
                                RectTransform = { AnchorMin = _config.UI.CountdownAnchorMin, AnchorMax = _config.UI.CountdownAnchorMax}
                            }, "Hud", LayerTimer, LayerTimer
                        },
                        new CuiElement
                        {
                            Parent = LayerTimer,
                            Components =
                            {
                                new CuiTextComponent{ Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 20, Text = text.Replace("#timer", TimeSpan.FromSeconds(sec).ToString(@"mm\:ss"))},
                                new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.7 0.7" },
                                new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = "1 1" }
                            }
                        }
                    };
                    CuiHelper.AddUi(currentBuyer, container);

                    if (_config.UseCountdownSound)
                        EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/beartrap/arm.prefab", currentBuyer, 0, Vector3.zero, Vector3.forward), currentBuyer.net.connection);
                }
                yield return CoroutineEx.waitForSecondsRealtime(1f);
            }
            DestroyGUI();
        }
        private void DestroyGUI()
        {
            CuiHelper.DestroyUi(currentBuyer, LayerTimer);
        }

        private static string HexToRustFormat(string hex, float opacity = 1f)
        {
            Color color = Color.black;
            if (!string.IsNullOrEmpty(hex))
            {
                var str = hex.Trim('#');

                var op = byte.Parse(string.Format($"{Mathf.RoundToInt(byte.MaxValue * opacity)}"), NumberStyles.Float);

                var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

                if (str.Length == 8)
                    op = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

                color = new Color32(r, g, b, op);
            }
            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary.Call("GetImage", shortname, skin);

        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary.Call("AddImage", url, shortname, skin);
        #endregion

        public class AuraController : MonoBehaviour
        {
            BradleyAPC apc;
            private void Awake()
            {
                apc = GetComponent<BradleyAPC>();
                InitAura();
            }

            void OnTriggerEnter(Collider col)
            {
                var player = col.ToBaseEntity() as BasePlayer;

                if (!player || player.IsNpc)
                    return;

                player.Hurt(new HitInfo(apc, player, DamageType.Collision, 1000f));
            }

            public void InitAura()
            {
                SphereCollider col = apc.gameObject.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.enabled = true;
                col.radius = 4;
                col.gameObject.layer = (int)Rust.Layer.Trigger;
            }
        }

        public class CustomBradleyComponent : MonoBehaviour
        {
            List<LootContainer> crates;
            List<ServerGib> gibs;
            List<BasePlayer> bots;
            Dictionary<ulong, float> damageEntries = new Dictionary<ulong, float>();
            float lifeTime = 0;
            ulong bradleyID = 0;

            public bool isInitialised = false;
            public BradleyAPC currentAPC;
            public APCSettings apcSettings = null;
            public BasePlayer currentOwner;

            public void InitBradley(BasePlayer owner, BradleyAPC apc, APCSettings config)
            {
                crates = Pool.GetList<LootContainer>();
                gibs = Pool.GetList<ServerGib>();
                bots = Pool.GetList<BasePlayer>();
                apcSettings = config;

                if (Instance.door && !apcSettings.Behavior.BlockDoor)
                {
                    Instance.door.SetFlag(BaseEntity.Flags.Open, true);
                    Instance.door.SetFlag(BaseEntity.Flags.Busy, false);
                    Instance.door.SendNetworkUpdate();
                }

                currentAPC = apc;
                bradleyID = apc.net.ID.Value;
                currentOwner = owner;
                lifeTime = Time.time + apcSettings.Base.DespawnTime;
                isInitialised = true;
                StartCoroutine("DoTick");
            }

            private IEnumerator DoTick()
            {
                while (bradleyID > 0)
                {
                    if (lifeTime <= Time.time /*|| (forceCleapupTime > 0 && Time.time >= forceCleapupTime)*/)
                    {
                        var cd = TimeSpan.FromSeconds(apcSettings.Base.Cooldown).Ticks;
                        if (currentAPC && currentOwner.IsValid())
                        {
                            cd = TimeSpan.FromSeconds(apcSettings.Base.Cooldown * apcSettings.Base.CooldownModifierOnFail).Ticks;
                            currentOwner.ChatMessage(Instance._("TextOnTimedKill", currentOwner.UserIDString));
                        }

                        Instance._playerData[currentOwner.userID].Cooldown = DateTime.Now.Ticks + cd;
                        foreach (var pid in damageEntries.Keys)
                            Instance._playerData[pid].Cooldown = DateTime.Now.Ticks + cd;

                        Interface.Oxide.DataFileSystem.WriteObject($"{Instance.Title}{System.IO.Path.DirectorySeparatorChar}playerData", Instance._playerData);
                        CleanUp();
                        Instance.ToggleSeller();
                    }
                    else
                    {
                        //if (!currentAPC || currentAPC.IsDestroyed)
                        //{
                        //    if (forceCleapupTime == 0 && crates.All(i => i == null || i.IsDestroyed || i.inventory.itemList.Count == 0))
                        //        forceCleapupTime = Time.time + 60 * 5;ldsda
                        //    else if (forceCleapupTime > 0 && gibs.All(i => i == null || i.IsDestroyed))
                        //        forceCleapupTime = Time.time + 1;
                        //}
                        RefreshUI();
                    }
                    yield return CoroutineEx.waitForSeconds(1f);
                }
                CleanUp();
            }

            void OnDestroy()
            {
                CleanUp();
            }

            private void CleanUp()
            {
                StopCoroutine("DoTick");

                if (currentAPC && !currentAPC.IsDestroyed)
                    currentAPC.Kill();
                if (Instance.door)
                {
                    Instance.door.Kill();
                    Instance.SpawnDoor();
                }
                isInitialised = false;
                currentAPC = null;
                apcSettings = null;
                currentOwner = null;
                fullTeam.Clear();
                bradleyID = 0;
                damageEntries.Clear();

                foreach (var p in BasePlayer.activePlayerList)
                    CuiHelper.DestroyUi(p, "BradleyUIPanel");

                if (crates != null)
                {
                    foreach (var c in crates)
                        if (c.IsValid() && !c.IsDestroyed)
                            c.Kill();
                    Pool.FreeList(ref crates);
                }

                if (gibs != null)
                {
                    foreach (var g in gibs)
                        if (g.IsValid() && !g.IsDestroyed)
                            g.Kill();
                    gibs.Clear();
                    Pool.FreeList(ref gibs);
                }

                if (bots != null)
                {
                    foreach (var b in bots)
                        if (b.IsValid() && !b.IsDestroyed)
                            b.Kill();
                    bots.Clear();
                    Pool.FreeList(ref bots);
                }
            }

            List<BasePlayer> fullTeam = new List<BasePlayer>();
            public List<BasePlayer> GetAllPlayersInTeam()
            {
                if (apcSettings != null && apcSettings.Base.LockForTeam && currentOwner)
                {
                    if (fullTeam.Count == 0)
                    {
                        fullTeam.Add(currentOwner);
                        if (currentOwner.Team != null)
                        {
                            foreach (var player in BasePlayer.activePlayerList)
                                if (currentOwner.Team.members.Contains(player.userID))
                                    fullTeam.Add(player);
                        }
                    }
                }
                else if (currentAPC && currentAPC.isSpawned)
                {
                    fullTeam.Clear();
                    foreach (var bp in BasePlayer.activePlayerList)
                        if (Vector3.Distance(currentAPC.transform.position, bp.transform.position) <= apcSettings.Cannon.ViewDistance)
                            fullTeam.Add(bp);
                }
                return fullTeam;
            }

            HashSet<BasePlayer> uiPlayers = new HashSet<BasePlayer>();
            private void RefreshUI()
            {
                foreach (var p in uiPlayers)
                    CuiHelper.DestroyUi(p, "BradleyUIPanel");

                uiPlayers.Clear();
                List<BasePlayer> team = GetAllPlayersInTeam();
                var seconds = $"{TimeSpan.FromSeconds(lifeTime - Time.time).Minutes}m. {TimeSpan.FromSeconds(lifeTime - Time.time).Seconds} s.";
                foreach (var p in team)
                {
                    if (p && p.IsValid() && !p.IsDead() && p.IsConnected)
                    {
                        uiPlayers.Add(p);
                        var cont = new CuiElementContainer();
                        cont.Add(new CuiPanel()
                        {
                            RectTransform = { AnchorMin = Instance._config.UI.TimerPanelAnchorMin, AnchorMax = Instance._config.UI.TimerPanelAnchorMax },
                            Image = { Color = HexToRustFormat("#811837") }
                        }, "Hud", "BradleyUIPanel", "BradleyUIPanel");

                        cont.Add(new CuiElement()
                        {
                            Parent = "BradleyUIPanel",
                            Components =
                            {
                                new CuiTextComponent()
                                {
                                    Color = HexToRustFormat("#FD357E"),
                                    Text = $"!",
                                    FontSize = 10,
                                    Align = TextAnchor.MiddleCenter,
                                    Font = "RobotoCondensed-Regular.ttf"
                                },
                                new CuiRectTransformComponent () { AnchorMin = "0 0", AnchorMax = "0.08 1" }
                            }
                        });

                        cont.Add(new CuiElement()
                        {
                            Parent = "BradleyUIPanel",
                            Components =
                            {
                                new CuiTextComponent()
                                {
                                    Color = HexToRustFormat("#D3C6BE"),
                                    Text = Instance._(currentAPC && !currentAPC.IsDead() && !currentAPC.IsDestroyed ? "LeaveTimerText" : "LootTimerText", p.UserIDString),
                                    FontSize = 10,
                                    Align = TextAnchor.MiddleLeft,
                                    Font = "RobotoCondensed-Regular.ttf"
                                },
                                new CuiRectTransformComponent () { AnchorMin = "0.1 0", AnchorMax = "0.7 1" }
                            }
                        });

                        cont.Add(new CuiElement()
                        {
                            Parent = "BradleyUIPanel",
                            Components =
                            {
                                new CuiTextComponent()
                                {
                                    Color = HexToRustFormat("#FF327A"),
                                    Text = seconds,
                                    FontSize = 10,
                                    Align = TextAnchor.MiddleRight,
                                    Font = "RobotoCondensed-Regular.ttf"
                                },
                                new CuiRectTransformComponent () { AnchorMin = "0.645 0", AnchorMax = "0.9 1" }
                            }
                        });

                        CuiHelper.AddUi(p, cont);
                    }
                }
            }

            public void OnAPCDeath()
            {
                currentAPC.maxCratesToSpawn = apcSettings.Loot.CrateAmount;
                LockInRadius(currentAPC.transform.position, 30f, currentOwner.userID);

                lifeTime = Time.time + apcSettings.Base.LootingTime + 1;
                if (currentOwner.IsValid() && apcSettings.Base.LockForTeam)
                    currentOwner.ChatMessage(Instance._("LootTimeStarted", currentOwner.UserIDString, apcSettings.Base.LootingTime / 60));
            }

            private void LockInRadius(Vector3 position, float radius, ulong id)
            {
                Instance.NextTick(() =>
                {
                    if (apcSettings.Base.DestroyFlames)
                    {
                        var flames = Pool.GetList<FireBall>();
                        Vis.Entities(position, radius, flames);
                        foreach (var e in flames)
                        {
                            if (e.IsValid() && e?.IsDestroyed == false)
                                e.Kill();
                        }
                        Pool.FreeList(ref flames);
                    }
                });

                Instance.timer.In(1f, () =>
                {
                    Vis.Entities(position, radius, gibs);
                    if (apcSettings.Base.DestroyFlames)
                    {
                        foreach (var ent in gibs)
                        {
                            if (ent is HelicopterDebris)
                                (ent as HelicopterDebris).tooHotUntil = 0f;
                            if (apcSettings.Base.LockForTeam)
                                ent.OwnerID = id;
                        };
                    }

                    Vis.Entities(position, radius, crates);
                    crates.RemoveAll(x => !x.ShortPrefabName.Contains("bradley_crate"));

                    foreach (var crate in crates)
                    {
                        if (apcSettings.Base.DestroyFlames)
                        {
                            try
                            {
                                var e = (crate as LockedByEntCrate)?.lockingEnt?.ToBaseEntity();
                                if (e.IsValid() && e?.IsDestroyed == false)
                                    e.Kill();
                            }
                            catch { }
                        }

                        if (apcSettings.Base.LockForTeam)
                            crate.OwnerID = id;

                        if (apcSettings != null && !string.IsNullOrEmpty(apcSettings.Loot.LootTables))
                        {
                            List<Item> toDelete = new List<Item>();
                            foreach (var i in crate.inventory.itemList)
                                if (i.skin == 0)
                                    toDelete.Add(i);
                            foreach (var i in toDelete)
                                crate.inventory.itemList.Remove(i);

                            crate.inventory.MarkDirty();
                        }
                    }

                    if (apcSettings != null && !string.IsNullOrEmpty(apcSettings.Loot.LootTables))
                    {
                        var table = Instance._lootTables[apcSettings.Loot.LootTables].GetRandom();
                        table.Items.Shuffle((uint)UnityEngine.Random.Range(0, int.MaxValue));

                        var itemsQueue = new Queue<LootSettings.ItemDefinition>(table.Items.Where(i => (10000 - UnityEngine.Random.Range(0, 10000) <= i.Chance * 100) && UnityEngine.Random.Range(i.AmountMin, i.AmountMax + 1) > 0));
                        var avg = Mathf.CeilToInt((crates.Sum(x => x.inventory.itemList.Count) + itemsQueue.Count) * 1.0f / crates.Count);
                        Instance.Puts($"Crates Found: {crates.Count}\nFrom: {table.Items.Count()}\nTake: {itemsQueue.Count}\nTaken slots:{crates.Sum(x => x.inventory.itemList.Count)}\nAVG: {avg}");
                        foreach (var crate in crates)
                        {
                            int c = crate.inventory.itemList.Count;
                            while (c < avg && itemsQueue.Count > 0)
                            {
                                var item = itemsQueue.Dequeue();
                                var amount = UnityEngine.Random.Range(Mathf.Max(1, item.AmountMin), item.AmountMax + 1);

                                var toAdd = ItemManager.CreateByName(item.Shortname, amount, item.SkinID);
                                if (toAdd != null)
                                {
                                    if (toAdd.info.stackable > 1 && damageEntries.Keys.Count > 1)
                                    {
                                        toAdd.amount = Mathf.Max(amount, Mathf.CeilToInt(amount * apcSettings.Loot.AttackerScale * damageEntries.Keys.Count));
                                        toAdd.MarkDirty();
                                    }

                                    if (!string.IsNullOrEmpty(item.DisplayName))
                                    {
                                        toAdd.name = item.DisplayName;
                                        toAdd.MarkDirty();
                                    }
                                    if (!toAdd.MoveToContainer(crate.inventory))
                                        break;
                                    c++;
                                }
                            }
                            crate.SendNetworkUpdate();
                        }
                    }
                });
            }

            public bool IsBradleyCrate(StorageContainer v)
            {
                return v && crates != null && crates.Contains(v);
            }

            public bool IsBradleyPart(BaseEntity v)
            {
                return v && gibs != null && gibs.Contains(v);
            }
        }

        #region Harmony
        private static class BradleyPatch
        {
            internal static bool Prefix(BradleyAPC __instance)
            {
                if (__instance && controller && __instance == controller.currentAPC && controller.isInitialised && controller.apcSettings != null && controller.apcSettings.Base.Name != APCSettings.Default.Base.Name)
                {
                    var apc = __instance;
                    if (apc.mainGunTarget != null && Vector3.Dot(apc.turretAimVector, (apc.GetAimPoint(apc.mainGunTarget) - apc.mainTurretEyePos.transform.position).normalized) >= 0.99f)
                    {
                        float num = Vector3.Distance(apc.mainGunTarget.transform.position, apc.transform.position);
                        bool flag = controller.apcSettings.Behavior.AlwaysShoot && num < apc.viewDistance || apc.VisibilityTest(apc.mainGunTarget);
                        if (Time.time > apc.nextCoaxTime && flag && num <= controller.apcSettings.Machinegun.ShootDistance)
                        {
                            apc.numCoaxBursted++;
                            apc.FireGun(apc.GetAimPoint(apc.mainGunTarget), 3f * Mathf.Max(0, 3 - controller.apcSettings.Base.Accuracy), isCoax: true);
                            apc.nextCoaxTime = Time.time + apc.coaxFireRate;
                            if (apc.numCoaxBursted >= apc.coaxBurstLength)
                            {
                                apc.nextCoaxTime = Time.time + controller.apcSettings.Machinegun.TimeBetweenShots;
                                apc.numCoaxBursted = 0;
                            }
                        }

                        if (flag)
                        {
                            FireGunTest(apc);
                        }
                    }
                    return false;
                }

                return true;
            }

            const string rocketPrefab = "assets/prefabs/ammo/rocket/rocket_basic.prefab";
            static bool vertical = false;
            static BasePlayer target = null;
            const float rocketspeed = 100f;
            static void FireGunTest(BradleyAPC apc)
            {
                if (Time.time < apc.nextFireTime)
                {
                    return;
                }

                List<string> rockets = new List<string>() { apc.mainCannonProjectile.resourcePath };
                if (controller.apcSettings.Cannon.UseRockets)
                    rockets.Add(rocketPrefab);

                apc.nextFireTime = Time.time + controller.apcSettings.Cannon.RocketDelay;
                apc.numBursted++;
                if (apc.numBursted >= controller.apcSettings.Cannon.RocketsPerBurst)
                {
                    apc.nextFireTime = Time.time + controller.apcSettings.Cannon.TimeBetweenRockets;
                    apc.numBursted = 0;
                    vertical = 10000 - UnityEngine.Random.Range(0, 10000) < controller.apcSettings.Cannon.HomingChance * 100;
                    target = null;
                }

                if (!vertical)
                {
                    Vector3 modifiedAimConeDirection = AimConeUtil.GetModifiedAimConeDirection(2f * Mathf.Max(0, 3 - controller.apcSettings.Base.Accuracy), apc.CannonMuzzle.rotation * Vector3.forward);
                    Vector3 normalized = (apc.CannonPitch.transform.rotation * Vector3.back + apc.transform.up * -1f).normalized;
                    apc.myRigidBody.AddForceAtPosition(normalized * apc.recoilScale, apc.CannonPitch.transform.position, ForceMode.Impulse);
                    Effect.server.Run(apc.mainCannonMuzzleFlash.resourcePath, apc, StringPool.Get(apc.CannonMuzzle.gameObject.name), Vector3.zero, Vector3.zero);
                    var rocket = rockets.GetRandom();

                    BaseEntity baseEntity = GameManager.server.CreateEntity(rocket, apc.CannonMuzzle.transform.position, Quaternion.LookRotation(modifiedAimConeDirection));
                    if (!(baseEntity == null))
                    {
                        ServerProjectile component = baseEntity.GetComponent<ServerProjectile>();
                        if ((bool)component)
                        {
                            component.gravityModifier = Random.Range(0.5f, component.gravityModifier);
                            component.InitializeVelocity(modifiedAimConeDirection * rocketspeed * controller.apcSettings.Cannon.RocketSpeed);
                        }
                        baseEntity.creatorEntity = apc;
                        baseEntity.Spawn();
                    }
                }
                else
                {
                    var targetPlayer = controller.GetAllPlayersInTeam().Where(x => x.IsConnected && !x.IsDead() && Vector3.Distance(x.transform.position, apc.transform.position) <= apc.viewDistance).ToList();

                    if (!target && controller.apcSettings.Cannon.lockOnce && targetPlayer.Count > 0)
                    {
                        target = targetPlayer.GetRandom();
                        target.ChatMessage(Instance._("LockOnMessage", target.UserIDString));
                    }

                    if (target || targetPlayer.Count > 0)
                    {
                        Vector3 normalized = (apc.CannonPitch.transform.rotation * Vector3.back + apc.transform.up * -1f).normalized;
                        apc.myRigidBody.AddForceAtPosition(normalized * apc.recoilScale, apc.CannonPitch.transform.position, ForceMode.Impulse);
                        Effect.server.Run(apc.mainCannonMuzzleFlash.resourcePath, apc, StringPool.Get(apc.CannonMuzzle.gameObject.name), Vector3.zero, Vector3.zero);
                        var rocket = rockets.GetRandom();

                        BaseEntity baseEntity = GameManager.server.CreateEntity(rocket, apc.transform.position, Quaternion.LookRotation(Vector3.up));
                        if (!(baseEntity == null))
                        {
                            ServerProjectile component = baseEntity.GetComponent<ServerProjectile>();
                            if ((bool)component)
                            {
                                component.gravityModifier = Random.Range(0.5f, component.gravityModifier);
                                component.InitializeVelocity(Vector3.up * controller.apcSettings.Cannon.RocketSpeed * 8f);
                            }
                            baseEntity.creatorEntity = apc;
                            baseEntity.Spawn();
                        }

                        apc.Invoke(() =>
                        {
                            if (baseEntity && !baseEntity.IsDestroyed)
                            {
                                var pos = baseEntity.transform.position;
                                baseEntity.Kill();

                                Vector3 targetPosition = (target ?? targetPlayer.GetRandom()).transform.position - pos;
                                List<Vector3> pool = new List<Vector3>();
                                for(int i = 0; i < 30; i++)
                                    pool.Add(targetPosition + Random.insideUnitSphere * Mathf.Max(1, 3 - controller.apcSettings.Base.Accuracy));

                                targetPosition = pool.GetRandom();
                                Pool.FreeList(ref pool);
                                BaseEntity baseEntityRocket = GameManager.server.CreateEntity(rocket, pos, Quaternion.LookRotation(targetPosition));
                                if (!(baseEntityRocket == null))
                                {
                                    ServerProjectile component = baseEntityRocket.GetComponent<ServerProjectile>();
                                    if ((bool)component)
                                    {
                                        component.gravityModifier = 0f;
                                        component.InitializeVelocity(targetPosition.normalized * rocketspeed * controller.apcSettings.Cannon.RocketSpeed);
                                    }
                                    baseEntityRocket.creatorEntity = apc;
                                    baseEntityRocket.Spawn();
                                }
                            }
                        }, controller.apcSettings.Cannon.FlyUpTime);
                    }
                }
            }

            internal static bool Spawn(BradleyAPC __instance, BasePlayer triggeringPlayer)
            {
                if(__instance && controller && __instance == controller.currentAPC && controller.isInitialised && controller.apcSettings != null && controller.apcSettings.Base.Name != APCSettings.Default.Base.Name && controller.apcSettings.Base.BlockNPCs)
                    return false;
                return true;
            }
        }
        #endregion
    }
}
