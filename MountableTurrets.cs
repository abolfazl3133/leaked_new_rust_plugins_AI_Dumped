//Requires: ImageLibrary

using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyLib;

namespace Oxide.Plugins
{
    [Info("MountableTurrets", "https://discord.gg/TrJ7jnS233", "1.2.1")]
    class MountableTurrets : RustPlugin
    {
        #region Dependencies

        [PluginReference] Plugin ImageLibrary, ServerRewards;

        static void AddImage(string url, string name) => Instance.ImageLibrary.Call("AddImage", url, name);
        static string GetImageID(string name) => (string)Instance.ImageLibrary.Call("GetImage", name) ?? "";

        static bool? AddPoints(ulong playerID, int amount) => Instance.ServerRewards.Call("AddPoints", playerID, amount) as bool?;
        static bool? TakePoints(ulong playerID, int amount) => Instance.ServerRewards.Call("TakePoints", playerID, amount) as bool?;
        static int? CheckPoints(ulong ID) => Instance.ServerRewards.Call("CheckPoints", ID) as int?;

        #endregion

        #region Constants

        const string GUN_PREFAB = "assets/prefabs/weapons/m249/m249.entity.prefab";
        const string COMP_STATION_PREFAB = "assets/prefabs/deployable/computerstation/computerstation.deployed.prefab";
        const string CCTV_PREFAB = "assets/prefabs/deployable/cctvcamera/cctv.static.prefab";
        const string ROCKET_LAUNCHER_PREFAB = "assets/prefabs/weapons/rocketlauncher/rocket_launcher.entity.prefab";
        const string MOUNTABLE_PREFAB = "assets/prefabs/vehicle/seats/standingdriver.prefab";
        const string SPINNER_PREFAB = "assets/prefabs/deployable/spinner_wheel/spinner.wheel.deployed.prefab";
        const string SIGN_PREFAB = "assets/prefabs/deployable/signs/sign.small.wood.prefab";
        const string LOOTABLE_PREFAB = "assets/prefabs/misc/item drop/item_drop.prefab";

        const string FLARE_PREFAB = "assets/prefabs/tools/flareold/flare.deployed.prefab";
        const string HV_ROCKET_PREFAB = "assets/prefabs/ammo/rocket/rocket_hv.prefab";

        const string EFFECT_CODELOCK_LOCK = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";
        const string EFFECT_CODELOCK_UNLOCK = "assets/prefabs/locks/keypad/effects/lock.code.lock.prefab";
        const string EFFECT_PAGER_BEEP = "assets/prefabs/tools/pager/effects/beep.prefab";

        const string FLARE_ICON = "assets/prefabs/tools/flareold/flare.png";

        const string CROSSHAIR_CUI = "MountableTurrets_crosshair";
        const string AA_CROSSHAIR_CUI = "MountableTurrets_aa_crosshair";
        const string ACQUIRING_TARGET_CUI = "MountableTurrets_acquiringTarget";
        const string ACQUIRING_TARGET_PARENT_CUI = "MountableTurrets_acquringTargetParent";
        const string TARGET_DISTANCE_CUI = "MountableTurrets_targetdistance";
        const string TARGET_INFO_CUI = "MountableTurrets_targetinfo";
        const string MODE_CUI = "MountableTurrets_mode";
        const string TARGET_LOST_CUI = "MountableTurrets_target_lost";
        const string RELOADING_CUI = "MountableTurrets_reloading";
        const string CONTROLS_HELP_CUI = "MountableTurrets_controlshelp";
        const string LOCKED_UI_CUI = "MountableTurrets_lockedui";
        const string AA_TURRET_CUI = "MountableTurrets_aaturretui";
        const string IRTRAP_CUI = "MountableTurrets_IRtrap";
        const string NO_AMMO_CUI = "MountableTurrets_noammo";
        const string CHANGEMODE_BTN_CUI = "MountableTurrets_changeModeBtn";
        const string FIRE_BTN_CUI = "MountableTurrets_fireBtn";

        const string FIRE_CCMD = "mountableturrets.fire";
        const string CHANGEMODE_CCMD = "mountableturrets.changemode";

        const string AA_TURRET_GIVE_PERMISSION = "mountableturrets.aaturret.give";
        const string AA_TURRET_FREE_PERMISSION = "mountableturrets.aaturret.free";
        const string MACHINEGUN_TURRET_FREE_PERMISSION = "mountableturrets.machinegun.free";
        const string MACHINEGUN_TURRET_GIVE_PERMISSION = "mountableturrets.machinegun.give";
        const string ADMIN_PERMISSION = "mountableturrets.admin";

        const string SAVES_DATAFILE_NAME = "MountableTurrets_PlayerGeneratedData";
        const string MONUMENT_DATAFILE_NAME = "MountableTurrets_MonumentData";

        const float MACHINEGUNTURRET_CLAMP_ANGLE = 40f;
        const int SPINNER_ITEMID = -1100422738;

        #endregion

        #region Fields

        static MountableTurrets Instance;
        static List<BaseEntity> TurretEntities;
        static Dictionary<BaseMountable, MachineGunTurret> TurretMountables;
        static Dictionary<string, AATurret> AATurrets;
        static Dictionary<BaseEntity, int> IRTrapCount;
        static Dictionary<BasePlayer, AATurret> PlayersCurrentlyControllingRCTurrets;
        static List<AARocket> AARockets;
        static List<MachineGunTurret> MachineGunTurrets;
        static List<BaseEntity> AASpinners;
        static List<BaseEntity> MachineGunSpinners;
        static List<ItemContainer> AATurretContainers;
        static List<ItemContainer> MachineGunTurretContainers;
        static Dictionary<BaseCombatEntity, float> LastIRTrapUse;
        static Dictionary<MonumentInfo, List<ISpawnable>> MonumentSpawnables;
        static ItemDefinition MachineGunTurretAmmoType;
        static List<BaseCombatEntity> IndestructibleEntities;
        static bool isPluginInitialized;
        static DataFileSystem dataFiles;

        #endregion

        #region Configuration

        class Configuration : SerializableConfiguration
        {
            [JsonProperty("PVE Mode (true/false)")]
            public bool PVE = false;

            [JsonProperty("[AA Turret] Price (set value to 0 to make it free, use ServerRewards as a key to use RP points)")]
            public KeyValuePair<string, int> AATurret_Price = new KeyValuePair<string, int>("scrap", 500);

            [JsonProperty("[AA Turret] Item Skin ID (Workshop ID)")]
            public ulong AATurret_Item_SkinID = 2849832768;

            [JsonProperty("[AA Turret] Item Name")]
            public string AATurret_Item_Name = "Anti-Aerial Turret";

            [JsonProperty("[AA Turret] Cooldown between shots (seconds)")]
            public float AATurret_Rocket_Cooldown = 10f;

            [JsonProperty("[AA Turret] Rocket fuse length (seconds)")]
            public float AATurret_Rocket_FuseLength = 10f;

            [JsonProperty("[AA Turret] Rocket fuse length (meters)")]
            public float AATurret_MaxLockOnDistance = 450f;

            [JsonProperty("[AA Turret] Entities that turret is able to lock on to (short prefab name)")]
            public string[] AATurret_AbleToLockOn = new string[]
            {
                "minicopter.entity",
                "scraptransporthelicopter",
                "hotairballoon"
            };

            [JsonProperty("[AA Turret] Infinite ammo (true/false)")]
            public bool AATurret_InfiniteAmmo = true;

            [JsonProperty("[AA Turret] Ammo item shortname")]
            public string AATurret_AmmoShortname = "ammo.rocket.hv";

            [JsonProperty("[AA Turret] Amount per shot")]
            public int AATurret_AmountPerShot = 1;

            [JsonProperty("[AA Turret] Target acquiring time (seconds)")]
            public float AATurret_TargetAcquiringTime = 0.6f;

            [JsonProperty("[AA Turret] Rocket initial velocity (meters per second)")]
            public float AATurret_Rocket_InitialVelocity = 30f;

            [JsonProperty("[AA Turret] Interval between shots in Burst mode (seconds)")]
            public float AATurret_Rocket_FireInterval = 0.4f;

            [JsonProperty("[AA Turret] Rocket explosion radius (meters)")]
            public float AATurret_Rocket_ExplosionRadius = 5f;

            [JsonProperty("[AA Turret] Movement Speed Fast")]
            public float AATurret_Speed_Fast = 3.7f;

            [JsonProperty("[AA Turret] Movement Speed Normal")]
            public float AATurret_Speed_Norm = 2f;

            [JsonProperty("[AA Turret] Movement Speed Slow")]
            public float AATurret_Speed_Slow = 1f;

            [JsonProperty("[AA Turret] Turret HP")]
            public float AATurret_HP = 50;

            [JsonProperty("[AA Turret] Damage multipliers (e.g. 1.0 - vanilla, 2.0 - 2x more damage, 0.5 - half of vanilla damage)")]
            public Dictionary<string, float> AATurret_DamageMultipliers = new Dictionary<string, float>()
            {
                { "vehicle", 1.0f },
                { "buildingblock", 1.0f },
                { "player", 1.0f },
                { "other", 1.0f }
            };

            [JsonProperty("[Machine Gun Turret] Price (set value to 0 to make it free, use ServerRewards as a key to use RP points)")]
            public KeyValuePair<string, int> MachinegunTurret_Price = new KeyValuePair<string, int>("scrap", 250);

            [JsonProperty("[Machine Gun Turret] Infinite ammo (true/false)")]
            public bool MachinegunTurret_InfiniteAmmo = true;

            [JsonProperty("[Machine Gun Turret] Ammo item shortname")]
            public string MachinegunTurret_AmmoShortname = "ammo.rifle";

            [JsonProperty("[Machine Gun Turret] Amount per shot")]
            public int MachinegunTurret_AmountPerShot = 1;

            [JsonProperty("[Machine Gun Turret] Item Skin ID (Workshop ID)")]
            public ulong MachinegunTurret_Item_SkinID = 2849176974;

            [JsonProperty("[Machine Gun Turret] Item Name")]
            public string MachinegunTurret_Item_Name = "Machine Gun Turret";

            [JsonProperty("[Machine Gun Turret] Ammo type (short prefab name, must be one of 5.56 Rifle ammo)")]
            public string MachinegunTurret_AmmoType = "ammo.rifle";

            [JsonProperty("[Machine Gun Turret] Turret HP")]
            public float MachinegunTurret_HP = 50;

            // [JsonProperty("[IR Trap] Stack size")] public int IRTrap_StackSize = 12;

            // [JsonProperty("[IR Trap] Amount per use")]
            // public int IRTrap_SpendPerUse = 2;

            // [JsonProperty("[IR Trap] Cooldown (seconds)")]
            // public float IRTrap_Cooldown = 3f;

            // [JsonProperty("[IR Trap] Initial velocity (meters per seconds)")]
            // public float IRTrap_InitialVelocity = 20f;

            // [JsonProperty("[IR Trap] Fuse length (seconds)")]
            // public float IRTrap_FuseLength = 10f;

            // [JsonProperty("[IR Trap] Affects SAM Sites (true/false)")]
            // public bool IRTrap_AffectSAMSites = true;

            // [JsonProperty("[IR Trap] SAM Site blind time (seconds)")]
            // public float IRTrap_SAMSiteBlindTime = 5f;

            // [JsonProperty("[IR Trap] Show IR-Trap hint UI (true/false)")]
            // public bool ShowIRTrapInfoText = true;

            // [JsonProperty("[IR Trap] Deploy positions")]
            // public Dictionary<string, List<PositionData>> IRTrap_DeployPositions = new Dictionary<string, List<PositionData>>
            // {
            //     {
            //         "minicopter.entity",
            //         new List<PositionData>
            //         {
            //             new PositionData(new Vector3(-0.582f, 0.6621f, -0.4615f), Vector3.left),
            //             new PositionData(new Vector3(0.556f, 0.6651f, -0.5f), Vector3.right)
            //         }
            //     },
            //     {
            //         "scraptransporthelicopter",
            //         new List<PositionData>
            //         {
            //             new PositionData(new Vector3(2.151f, 0.373f, -1.129f), Vector3.right),
            //             new PositionData(new Vector3(-2.43899f, 0.375f, -0.977f), Vector3.left)
            //         }
            //     },
            // };

            [JsonProperty("[Misc] Spawn positions on other entities (full prefab name)")]
            public Dictionary<string, List<SerializedConfigSaveEntry>> EntityTurrets = new Dictionary<string, List<SerializedConfigSaveEntry>>()
            {
                {
                    "assets/content/vehicles/modularcar/module_entities/2module_flatbed.prefab", new List<SerializedConfigSaveEntry>()
                    {
                        new SerializedConfigSaveEntry()
                        {
                            Type = "MachinegunTurret",
                            Position = new Vector3(-0.01828f, 0.16f, -1.6145f),
                            Rotation = new Vector3(0, 180f, 0)
                        }
                    }
                },
/*                { "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab", new List<SerializedConfigSaveEntry>() {
                    new SerializedConfigSaveEntry()
                    {
                        Type = "MachinegunTurret",
                        Position = new Vector3(0.84f, 0.8f, -0.629f),
                        Rotation = new Vector3(0, 90f, 0)
                    },
                    new SerializedConfigSaveEntry()
                    {
                        Type = "MachinegunTurret",
                        Position = new Vector3(-0.87f, 0.8f, 0.348f),
                        Rotation = new Vector3(0, 270f, 0)
                    },
                } }, */
                {
                    "assets/content/vehicles/trains/wagons/trainwagonb.entity.prefab", new List<SerializedConfigSaveEntry>()
                    {
                        new SerializedConfigSaveEntry()
                        {
                            Type = "MachinegunTurret",
                            Position = new Vector3(0.974f, 1.545f, 3.276f),
                            Rotation = new Vector3(0, 90f, 0)
                        },
                        new SerializedConfigSaveEntry()
                        {
                            Type = "MachinegunTurret",
                            Position = new Vector3(-1.035f, 1.545f, -3.35f),
                            Rotation = new Vector3(0, 270f, 0)
                        },
                    }
                },
                {
                    "assets/content/vehicles/trains/wagons/trainwagona.entity.prefab", new List<SerializedConfigSaveEntry>()
                    {
                        new SerializedConfigSaveEntry()
                        {
                            Type = "MachinegunTurret",
                            Position = new Vector3(-0.98385f, 1.528f, 2.98f),
                            Rotation = new Vector3(0, 270, 0)
                        }
                    }
                },
                {
                    "assets/content/vehicles/boats/cargoship/cargoshiptest.prefab", new List<SerializedConfigSaveEntry>()
                    {
                        new SerializedConfigSaveEntry()
                        {
                            Type = "MachinegunTurret",
                            Position = new Vector3(-11.52f, 6.463f, -4.375f),
                            Rotation = new Vector3(0, 270f, 0)
                        },
                        new SerializedConfigSaveEntry()
                        {
                            Type = "MachinegunTurret",
                            Position = new Vector3(-11.5193f, 6.475f, 25.335f),
                            Rotation = new Vector3(0, 270f, 0)
                        },
                        new SerializedConfigSaveEntry()
                        {
                            Type = "MachinegunTurret",
                            Position = new Vector3(11.5493f, 6.488f, -4.362f),
                            Rotation = new Vector3(0, 90f, 360f)
                        },
                        new SerializedConfigSaveEntry()
                        {
                            Type = "MachinegunTurret",
                            Position = new Vector3(11.552f, 6.5f, 25.329f),
                            Rotation = new Vector3(0, 90, 0f)
                        },
                        new SerializedConfigSaveEntry()
                        {
                            Type = "AATurret",
                            Position = new Vector3(0.791f, 9.472f, 75.623f),
                            Rotation = new Vector3(0, 180f, 0f)
                        },
                        new SerializedConfigSaveEntry()
                        {
                            Type = "AATurret",
                            Position = new Vector3(-5.977f, 27.495f, -45.049f),
                            Rotation = new Vector3(0, 0, 0)
                        },
                        new SerializedConfigSaveEntry()
                        {
                            Type = "AATurret",
                            Position = new Vector3(6, 27.495f, -45.003f),
                            Rotation = new Vector3(0, 0, 0)
                        },
                        new SerializedConfigSaveEntry()
                        {
                            Type = "ComputerStation",
                            Position = new Vector3(-4.791f, 24.498f, -44.161f),
                            Rotation = new Vector3(0, 0, 0)
                        },
                        new SerializedConfigSaveEntry()
                        {
                            Type = "ComputerStation",
                            Position = new Vector3(-0.007f, 3.5f, 51.7f),
                            Rotation = new Vector3(0, 0, 0)
                        },
                    }
                }
            };

            [JsonProperty("[Misc] UI Images (leave names intact)")]
            public Dictionary<string, string> CUI_Images = new Dictionary<string, string>()
            {
                { @"https://i.imgur.com/lXTORE5.png", "MountableTurrets_crosshair" },
                { @"https://i.imgur.com/7VyNdEh.png", "MountableTurrets_aa_crosshair" },
                { @"https://i.imgur.com/IH0KzHK.png", "MountableTurrets_aa_crosshair_locked" },
                { @"https://i.imgur.com/ZvjYx8J.png", "MountableTurrets_line" },
            };
        }

        class SerializedConfigSaveEntry
        {
            public string Type;
            public Vector3 Position;
            public Vector3 Rotation;
        }

        #endregion

        #region Configuration Boilerplate

        static Configuration config;

        private class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                            .ToDictionary(prop => prop.Name,
                                prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool ValidateConfig(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            var changed = false;
            var oldKeys = new List<string>();

            foreach (var key in currentRaw.Keys)
            {
                if (currentWithDefaults.Keys.Contains(key))
                {
                    continue;
                }

                changed = true;
                oldKeys.Add(key);
            }

            foreach (var key in oldKeys)
            {
                currentRaw.Remove(key);
            }

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                            continue;
                        }

                        if (ValidateConfig(defaultDictValue, currentDictValue))
                        {
                            changed = true;
                        }
                    }

                    continue;
                }

                currentRaw[key] = currentWithDefaults[key];
                changed = true;
            }


            return changed;
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                var currentWithDefaults = config.ToDictionary();
                var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);

                if (ValidateConfig(currentWithDefaults, currentRaw))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AATurret_ControlsHelp"] = "W A S D - move camera\nCTRL - move slowly\nSHIFT -   move fast\nSPACE -    dismount",
                ["IRTrap_InfoText"] = "You can add IR-traps (flares) to your vehicle to avoid getting shot by anti-aerial missiles.\n\nPress [USE] to deploy IR-traps",
                ["noPerms"] = "You don't have permission to use this command",
                ["noMonument"] = "You must be standing in a monument to use this command",
                ["playerDataWiped"] = "Player data is wiped!",
                ["mtUsage"] = "Usage: /mt add/remove/reset",
                ["mtAddUsage"] = "Usage: /mt add aa/mg/comp",
                ["lookAtTurret"] = "You are not looking at a turret",
                ["lookAtSurface"] = "Look at the surface you want to spawn on",
                ["allRemoved"] = "Wiped all data for {0}",
                ["addedToMonument"] = "Added to {0}",
                ["removedFromMonument"] = "Removed from {0}",
                ["notEnoughPoints"] = "You don't have enough points to buy this turret",
                ["notEnoughResources"] = "You don't have enough {0} to buy this turret",
                ["mgTurretGiven"] = "Machinegun turret has been given to you",
                ["aaTurretGiven"] = "Anti-Aerial turret has been given to you",
                ["reloading"] = "RELOADING...",
                ["targetLost"] = "TARGET LOST",
                ["targetAcquired"] = "TARGET ACQUIRED",
                ["acquiringTarget"] = "ACQUIRING TARGET...",
                ["lockedOnTarget"] = "LOCKED ON TARGET",
                ["waitingForTarget"] = "WAITING FOR TARGET",
                ["manualSingle"] = "MANUAL (SINGLE)",
                ["manualBurst"] = "MANUAL (BURST)",
                ["aaTurretIDIs"] = "AATurret ID is: {0}",
                ["noAmmo"] = "NO AMMO",
                ["irTrapStorage"] = "IR-TRAP STORAGE",
                ["noIRTraps"] = "You've ran out of IR-Traps",
                ["fire"] = "FIRE",
                ["changeMode"] = "CHANGE MODE",
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AATurret_ControlsHelp"] = "W A S D - двигать камерой\nCTRL - двигаться медленно\nSHIFT -   двигаться быстро\nПРОБЕЛ -    выйти",
                ["IRTrap_InfoText"] = "Вы можете добавить ИК-ловушки, чтобы исбежать попадания ракет.\n\nНажмите [Е] чтобы выпустить ИК-ловушки",
                ["noPerms"] = "У вас нет доступа к этой команде",
                ["noMonument"] = "Вы должны стоять на РТ для использования этой команды",
                ["playerDataWiped"] = "Вся информация сброшена!",
                ["mtUsage"] = "Использование: /mt add/remove/reset",
                ["mtAddUsage"] = "Использование: /mt add aa/mg/comp",
                ["lookAtTurret"] = "Вы не смотрите на турель",
                ["lookAtSurface"] = "Смотрите на поверхность на которой хотите заспаунить турель",
                ["allRemoved"] = "Вся информация для {0} очищена!",
                ["addedToMonument"] = "Добавлено в {0}",
                ["removedFromMonument"] = "Удалено с {0}",
                ["notEnoughPoints"] = "У вас не хватает очков для покупки этой турели",
                ["notEnoughResources"] = "У вас не хватает {0} для покупки этой турели",
                ["mgTurretGiven"] = "Пулемётная турель была выдана вам",
                ["aaTurretGiven"] = "ПВО была выдана вам",
                ["reloading"] = "ПЕРЕЗАРЯДКА...",
                ["targetLost"] = "ЦЕЛЬ ПОТЕРЯНА",
                ["targetAcquired"] = "ЦЕЛЬ ЗАХВАЧЕНА",
                ["acquiringTarget"] = "ЗАХВАТ ЦЕЛИ...",
                ["lockedOnTarget"] = "ЦЕЛЬ ЗАХВАЧЕНА",
                ["waitingForTarget"] = "ОЖИДАНИЕ ЦЕЛИ",
                ["manualSingle"] = "РУЧНОЙ (ОДИНОЧНЫЙ)",
                ["manualBurst"] = "РУЧНОЙ (ОЧЕРЕДЬ)",
                ["aaTurretIDIs"] = "ID турели - {0}",
                ["noAmmo"] = "НЕТ СНАРЯДОВ",
                ["irTrapStorage"] = "ИК-ЛОВУШКИ",
                ["noIRTraps"] = "У вас закончились ИК-ловушки",
                ["fire"] = "ВЫСТРЕЛ",
                ["changeMode"] = "ИЗМЕНИТЬ РЕЖИМ",
            }, this, "ru");
        }

        static string GetText(string textName, BasePlayer player) => Instance.lang.GetMessage(textName, Instance, player?.UserIDString);

        #endregion

        #region Harmony Boilerplate

        static Harmony harmonyInstance;

        void InitHarmony()
        {
            harmonyInstance = new Harmony($"ru.senyaa.{Name}");
            harmonyInstance.PatchAll();
        }

        void UnloadHarmony()
        {
            harmonyInstance.UnpatchAll();
        }
        #endregion

        #region Types

        public struct PositionData
        {
            public Vector3 Position;
            public Vector3 Direction;

            public PositionData(Vector3 position, Vector3 direction)
            {
                Position = position;
                Direction = direction;
            }
        }

        public sealed class AARocket : FacepunchBehaviour
        {
            public BaseEntity Target { get; set; }
            TimedExplosive explosive;

            void Awake()
            {
                explosive = GetComponent<TimedExplosive>();
                explosive.SetFuse(config.AATurret_Rocket_FuseLength);
                explosive.explosionRadius = config.AATurret_Rocket_ExplosionRadius;
                AARockets.Add(this);
            }

            void OnDestroy()
            {
                AARockets?.Remove(this);
            }

            void FixedUpdate()
            {
                if (Target == null || Target.IsDestroyed)
                {
                    return;
                }

                var direction = (Target.transform.position - transform.position).normalized;
                var lookRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, 10f);
                if (Vector3.Distance(transform.position, Target.transform.position) <= config.AATurret_Rocket_ExplosionRadius)
                {
                    explosive.Explode();
                }
            }
        }

        public sealed class AATurret : FacepunchBehaviour, ISpawnable
        {
            public enum Modes
            {
                Automatic,
                Standby,
                Manual_single,
                Manual_burst
            }

            public BaseEntity Target { get; private set; }
            public bool IsManuallyPlaced { get; private set; }
            public string CCTV_ID { get; set; }
            public DroppedItemContainer AmmoStorage { get; private set; }
            public int AmmoItemID { get; private set; }
            public List<BasePlayer> CurrentlyControlling;

            CCTV_RC camera;
            BaseEntity pivotY;
            BaseEntity pivotX;
            BaseEntity spinner;
            List<BaseEntity> rocketLaunchers;

            int targetHits;

            Modes _mode;

            int currentLauncherID = 0;

            bool hasRecentlyFired;


            public Modes Mode
            {
                get { return _mode; }
                set
                {
                    _mode = value;
                    targetHits = 0;

                    foreach (var player in CurrentlyControlling)
                    {
                        DrawMode(player);
                        SendEffect("assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab", player, camera.transform.position);
                    }
                }
            }

            public static Item CreateItem()
            {
                var item = ItemManager.CreateByItemID(SPINNER_ITEMID, 1, config.AATurret_Item_SkinID);
                item.name = config.AATurret_Item_Name;
                return item;
            }

            // ReSharper disable Unity.PerformanceAnalysis
            public static AATurret Spawn(Vector3 position, Quaternion rotation, string CCTV_ID = "", bool isManual = false, int ammoAmount = 0)
            {
                if (AATurrets.ContainsKey(CCTV_ID))
                {
                    return null;
                }

                var spinner = GameManager.server.CreateEntity(SPINNER_PREFAB, position, rotation) as SpinnerWheel;

                UnityEngine.Object.DestroyImmediate(spinner.gameObject.GetComponent<DestroyOnGroundMissing>());

                if (isManual)
                {
                    Instance.NextFrame(() =>
                    {
                        if (spinner == null || spinner.IsDestroyed)
                        {
                            return;
                        }

                        spinner.gameObject.AddComponent<DestroyOnGroundMissing>();
                    });
                }
                else
                {
                    IndestructibleEntities.Add(spinner);
                }

                AASpinners.Add(spinner);
                spinner.Spawn();
                spinner.EnableSaving(false);

                spinner.SetMaxHealth(config.AATurret_HP);
                spinner.SetHealth(config.AATurret_HP);

                var comp = spinner.gameObject.AddComponent<AATurret>();

                comp.IsManuallyPlaced = isManual;
                comp.Mode = Modes.Standby;

                if (CCTV_ID == "")
                {
                    while (true)
                    {
                        var newId = "AATURRET" + UnityEngine.Random.Range(100, 10000).ToString();
                        if (!AATurrets.ContainsKey(newId))
                        {
                            comp.CCTV_ID += newId;
                            break;
                        }
                    }
                }
                else
                {
                    comp.CCTV_ID = CCTV_ID;
                }

                TurretEntities.Add(spinner);

                var nail = AttachWorldmodel(spinner, -2097376851, Vector3.zero, Quaternion.identity);
                comp.pivotY = nail;
                comp.spinner = spinner;

                AttachWorldmodel(nail, 95950017, new Vector3(-0.130317688f, 0.414741516f, -0.0435333252f), Quaternion.Euler(4.05267572f, 180f, 285.532654f));
                AttachWorldmodel(nail, 95950017, new Vector3(0.103477478f, 0.414741516f, -0.0435333252f), Quaternion.Euler(4.05267811f, 0, 285.532593f));
                AttachWorldmodel(nail, 95950017, new Vector3(0.0182723999f, 0.375617981f, 0.155380249f), Quaternion.Euler(0, 270f, 285.532654f));


                AttachWorldmodel(nail, -1673693549, new Vector3(-0.158760071f, 0.594429016f, -0.0280914307f), Quaternion.Euler(308.687531f, 246.939484f, 22.0384579f));

                TurretEntities.Add(AttachEntity(nail, "assets/prefabs/deployable/playerioents/tunnel/cabletunnel.prefab", new Vector3(0.107048035f, 0.56287384f, -0.127761841f), new Vector3(285.572113f, 303.333679f, 55.675457f)));
                TurretEntities.Add(AttachEntity(nail, "assets/prefabs/deployable/playerioents/gates/branch/electrical.branch.deployed.prefab", new Vector3(0.165611267f, 0.219795227f, -0.132400513f), new Vector3(358.176453f, 180.388336f, 177.677612f)));


                var pivotx = AttachWorldmodel(nail, -2097376851, new Vector3(-0.0199966431f, 0.858099997f, -0.0903000012f), Quaternion.Euler(0, 0, 180));
                comp.pivotX = pivotx;

                comp.camera = AttachEntity(pivotx, CCTV_PREFAB, new Vector3(-0.0358581543f, -0.214081705f, 0.157484442f), new Vector3(0, 0, 180f)) as CCTV_RC;
                comp.camera.rcIdentifier = comp.CCTV_ID;
                comp.camera.SendNetworkUpdateImmediate();
                TurretEntities.Add(comp.camera);

                comp.rocketLaunchers = new List<BaseEntity>();

                comp.rocketLaunchers.Add(AttachEntity(pivotx, ROCKET_LAUNCHER_PREFAB, new Vector3(0.0537109375f, -0.00244230032f, 0.159132391f), new Vector3(278.907288f, 40.0738029f, 357.223999f)));
                comp.rocketLaunchers.Add(AttachEntity(pivotx, ROCKET_LAUNCHER_PREFAB, new Vector3(0.189483643f, -0.00244230032f, 0.159132391f), new Vector3(278.907288f, 40.0738029f, 357.223999f)));
                comp.rocketLaunchers.Add(AttachEntity(pivotx, ROCKET_LAUNCHER_PREFAB, new Vector3(0.189483643f, 0.129210532f, 0.161268622f), new Vector3(278.907288f, 40.0738029f, 357.223999f)));
                comp.rocketLaunchers.Add(AttachEntity(pivotx, ROCKET_LAUNCHER_PREFAB, new Vector3(0.0537109375f, 0.129210532f, 0.161268622f), new Vector3(278.907288f, 40.0738029f, 357.223999f)));
                comp.rocketLaunchers.Add(AttachEntity(pivotx, ROCKET_LAUNCHER_PREFAB, new Vector3(-0.0769999996f, 0.129210532f, 0.161268622f), new Vector3(278.907288f, 40.0738029f, 357.223999f)));
                comp.rocketLaunchers.Add(AttachEntity(pivotx, ROCKET_LAUNCHER_PREFAB, new Vector3(-0.0769999996f, -0.00244230032f, 0.159132391f), new Vector3(278.907288f, 40.0738029f, 357.223999f)));

                AttachWorldmodel(pivotx, 596469572, new Vector3(0.249267578f, -0.0306710601f, 0.00263825804f), Quaternion.Euler(1.17321372f, 88.8424377f, 182.353027f));


                AttachWorldmodel(pivotx, 1882709339, new Vector3(0.225280762f, -0.0946664214f, 0.115065016f), Quaternion.Euler(270.607178f, 84.0603714f, 4.42901897f));
                AttachWorldmodel(pivotx, 1882709339, new Vector3(-0.265167236f, -0.0946664214f, 0.115065016f), Quaternion.Euler(272.126343f, 271.872528f, 177.131348f));
                AttachWorldmodel(pivotx, 1882709339, new Vector3(-0.0316467285f, -0.242188394f, 0.101149f), Quaternion.Euler(0, 90f, 179.111053f));

                AttachWorldmodel(pivotx, 317398316, new Vector3(0.107055664f, -0.219910562f, 0.0945572034f), Quaternion.Euler(0, 90f, 179.625061f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(0.0217285156f, -0.21930021f, 0.0944961682f), Quaternion.Euler(0f, 90f, 179.625061f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(-0.155487061f, 0.0394278169f, -0.203050226f), Quaternion.Euler(0f, 270f, 0.374949962f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(-0.0701599121f, 0.0387869477f, -0.203111261f), Quaternion.Euler(0f, 270f, 0.374949962f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(0.103393555f, 0.0387869477f, -0.203111261f), Quaternion.Euler(0, 270f, 0.374949962f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(0.107055664f, -0.221833169f, -0.201280206f), Quaternion.Euler(0f, 90f, 179.625061f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(0.188110352f, -0.040009439f, -0.20274505f), Quaternion.Euler(270.374298f, 180f, 90f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(0.186889648f, -0.141205728f, -0.202195734f), Quaternion.Euler(270.374298f, 180f, 90f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(-0.23828125f, -0.0395516753f, -0.20250091f), Quaternion.Euler(89.6257019f, -0.00130411517f, 89.9986954f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(-0.239532471f, -0.140717447f, -0.201890558f), Quaternion.Euler(89.6257019f, -0.00130411517f, 89.9986954f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(0.0217285156f, -0.2211923f, -0.201219171f), Quaternion.Euler(0, 90f, 179.625061f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(-0.151824951f, -0.2211923f, -0.201219171f), Quaternion.Euler(0, 90f, 179.625061f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(-0.151824951f, -0.21930021f, 0.0944961682f), Quaternion.Euler(0, 90f, 179.625061f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(-0.239532471f, -0.138794839f, 0.0940078869f), Quaternion.Euler(89.6257019f, -0.00130411517f, 89.9986954f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(-0.23828125f, -0.0376290679f, 0.0933365002f), Quaternion.Euler(89.6257019f, -0.00130411517f, 89.9986954f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(0.186889648f, -0.139283121f, 0.0937637463f), Quaternion.Euler(270.374298f, 180f, 90f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(0.188110352f, -0.038056314f, 0.0930313244f), Quaternion.Euler(270.374298f, 180f, 90f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(0.103393555f, 0.0407095551f, 0.0929092541f), Quaternion.Euler(0, 270f, 0.374949962f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(-0.0701599121f, 0.0407095551f, 0.0929092541f), Quaternion.Euler(0, 270f, 0.374949962f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(-0.155487061f, 0.0413504243f, 0.0930313244f), Quaternion.Euler(0, 270f, 0.374949962f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(-0.155487061f, 0.0435171723f, 0.428602606f), Quaternion.Euler(0, 270f, 0.374949962f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(-0.0701599121f, 0.0429068208f, 0.428602606f), Quaternion.Euler(0, 270f, 0.374949962f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(0.103393555f, 0.0429068208f, 0.428602606f), Quaternion.Euler(0, 270f, 0.374949962f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(0.107055664f, -0.217713296f, 0.430250555f), Quaternion.Euler(0f, 90f, 179.625061f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(0.188110352f, -0.0358590484f, 0.428785712f), Quaternion.Euler(270.374298f, 180f, 90f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(0.186889648f, -0.137085855f, 0.429273993f), Quaternion.Euler(270.374298f, 180f, 90f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(-0.23828125f, -0.0354318023f, 0.429151922f), Quaternion.Euler(89.6257019f, -0.00130411517f, 89.9986954f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(-0.239532471f, -0.136597574f, 0.429579169f), Quaternion.Euler(89.6257019f, -0.00130411517f, 89.9986954f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(0.0217285156f, -0.217072427f, 0.430250555f), Quaternion.Euler(0, 90f, 179.625061f));
                AttachWorldmodel(pivotx, 317398316, new Vector3(-0.151824951f, -0.217072427f, 0.430250555f), Quaternion.Euler(0, 90, 179.625061f));

                if (!config.AATurret_InfiniteAmmo)
                {
                    var itemContainer = GameManager.server.CreateEntity(LOOTABLE_PREFAB, spinner.transform.position, spinner.transform.rotation) as DroppedItemContainer;

                    itemContainer.transform.localPosition = new Vector3(0, 0.1f, 0);
                    itemContainer.transform.localRotation = Quaternion.Euler(0, 0, 0);
                    itemContainer.SetParent(spinner);

                    UnityEngine.Object.DestroyImmediate(itemContainer.gameObject.GetComponent<Rigidbody>());

                    itemContainer.inventory = new ItemContainer();
                    itemContainer.inventory.ServerInitialize(null, 6);
                    itemContainer.inventory.GiveUID();
                    itemContainer.inventory.entityOwner = spinner;
                    itemContainer.inventory.SetFlag(ItemContainer.Flag.NoItemInput, false);

                    itemContainer.Spawn();
                    IndestructibleEntities.Add(itemContainer);
                    itemContainer.CancelInvoke(itemContainer.RemoveMe);

                    itemContainer.EnableSaving(false);
                    itemContainer.SendNetworkUpdateImmediate();

                    comp.AmmoStorage = itemContainer;
                    AATurretContainers.Add(itemContainer.inventory);

                    var ammoDef = ItemManager.FindItemDefinition(config.AATurret_AmmoShortname);

                    itemContainer.inventory.AddItem(ammoDef, ammoAmount);
                    comp.AmmoItemID = ammoDef.itemid;
                }


                comp.Invoke(() =>
                {
                    if (comp?.rocketLaunchers == null)
                    {
                        return;
                    }

                    foreach (var rocketLauncher in comp.rocketLaunchers)
                    {
                        rocketLauncher.transform.localRotation = Quaternion.Euler(278.907288f, 40.0738029f, 357.223999f);
                        rocketLauncher.SendNetworkUpdate_Position();
                    }
                }, 0.15f);

                AATurrets.Add(comp.CCTV_ID, comp);


                comp.InvokeRepeating(comp.RefreshGunPosition, 0f, 5f);
                comp.InvokeRepeating(comp.UITick, 0f, config.AATurret_TargetAcquiringTime / 4f);

                return comp;
            }

            void Awake()
            {
                CurrentlyControlling = new List<BasePlayer>();
            }

            void OnDestroy()
            {
                foreach (var player in CurrentlyControlling)
                {
                    OnControlEnd(player, false);
                }

                OnKilled();
            }

            void FixedUpdate()
            {
                if (Mode == Modes.Automatic && (Target == null || Target.IsDestroyed))
                {
                    LoseTarget();
                    return;
                }

                if (Target?.transform == null || Target.IsDestroyed)
                {
                    return;
                }

                var rotX = pivotX.transform.eulerAngles;
                pivotX.transform.LookAt(Target.transform);
                rotX.x = pivotX.transform.eulerAngles.x - camera.transform.localEulerAngles.x;
                pivotX.transform.eulerAngles = rotX;

                var rotY = pivotY.transform.eulerAngles;
                pivotY.transform.LookAt(Target.transform);
                rotY.y = pivotX.transform.eulerAngles.y;
                pivotY.transform.eulerAngles = rotY;
                
                pivotY.SendChildrenNetworkUpdateImmediate();
                pivotX.SendChildrenNetworkUpdateImmediate();
                spinner.SendChildrenNetworkUpdateImmediate();
            }

            void RefreshGunPosition()
            {
                if (spinner == null || spinner.IsDestroyed || rocketLaunchers == null)
                {
                    return;
                }

                foreach (var rocketLauncher in rocketLaunchers)
                {
                    rocketLauncher.SendNetworkUpdate_Position();
                }
            }

            void UITick()
            {
                if (CurrentlyControlling == null)
                {
                    return;
                }

                if (Mode == Modes.Manual_single || Mode == Modes.Manual_burst)
                {
                    return;
                }

                if (camera == null || camera.IsDestroyed)
                {
                    return;
                }

                if (Target != null && !Target.IsDestroyed)
                {
                    var dist = Vector3.Distance(camera.transform.position, Target.transform.position);
                    if (dist >= config.AATurret_MaxLockOnDistance)
                    {
                        LoseTarget();
                        return;
                    }

                    foreach (var player in CurrentlyControlling)
                    {
                        RedrawUI(player, dist);
                    }

                    return;
                }

                RaycastHit hit;
                if (Physics.Raycast(camera.transform.position, camera.transform.rotation * Vector3.forward, out hit, config.AATurret_MaxLockOnDistance))
                {
                    var ent = hit.GetEntity();
                    if (ent != null && config.AATurret_AbleToLockOn.Contains(ent.ShortPrefabName))
                    {
                        targetHits++;

                        foreach (var player in CurrentlyControlling)
                        {
                            DrawAcquiringTargetUI(player, targetHits / 4f);
                            DrawDefaultCrosshair(player, "0.55 0.78 0.24 0.8");
                            SendEffect(EFFECT_CODELOCK_LOCK, player, camera.transform.position);
                        }

                        if (targetHits > 3)
                        {
                            LockTarget(ent);
                        }

                        return;
                    }
                }

                targetHits = 0;

                foreach (var player in CurrentlyControlling)
                {
                    DrawDefaultCrosshair(player);
                    CuiHelper.DestroyUi(player, ACQUIRING_TARGET_PARENT_CUI);
                    CuiHelper.DestroyUi(player, TARGET_DISTANCE_CUI);
                }
            }

            void OnKilled()
            {
                AATurrets?.Remove(CCTV_ID);

                CancelInvoke(RefreshGunPosition);
                CancelInvoke(UITick);
                CancelInvoke(FiringAction);

                if (pivotY != null && !pivotY.IsDestroyed)
                {
                    pivotY.AdminKill();
                }

                if (spinner != null && !spinner.IsDestroyed)
                {
                    spinner.AdminKill();
                }

                if (rocketLaunchers != null)
                {
                    Facepunch.Pool.FreeList(ref rocketLaunchers);
                }
            }

            public void RemoveColliders()
            {
                UnityEngine.Object.DestroyImmediate(camera.gameObject.GetComponent<MeshCollider>());
                UnityEngine.Object.DestroyImmediate(spinner.gameObject.GetComponent<MeshCollider>());
            }

            public BaseEntity GetEntity() => spinner;

            public void OnControlStart(BasePlayer player)
            {
                if (CurrentlyControlling.Contains(player))
                {
                    return;
                }

                CurrentlyControlling.Add(player);
                DrawInitialUI(player);
            }

            public void OnControlEnd(BasePlayer player, bool deletePlayer = true)
            {
                if (!CurrentlyControlling.Contains(player))
                {
                    return;
                }

                if (deletePlayer)
                {
                    CurrentlyControlling.Remove(player);
                }

                CuiHelper.DestroyUi(player, AA_TURRET_CUI);
            }

            public void ChangeMode()
            {
                switch (Mode)
                {
                    case Modes.Automatic:
                        LoseTarget();
                        Mode = Modes.Standby;
                        break;
                    case Modes.Standby:
                        foreach (var player in CurrentlyControlling)
                        {
                            CuiHelper.DestroyUi(player, ACQUIRING_TARGET_PARENT_CUI);
                            CuiHelper.DestroyUi(player, LOCKED_UI_CUI);
                            DrawDefaultCrosshair(player);
                        }

                        Mode = Modes.Manual_single;
                        break;
                    case Modes.Manual_single:
                        Mode = Modes.Manual_burst;
                        break;
                    case Modes.Manual_burst:
                        Mode = Modes.Standby;
                        break;
                }
            }

            public void OnPlayerInput(BasePlayer player)
            {
                var inputState = player.serverInput;

                if (Target != null)
                {
                    return;
                }

                var speed = inputState.IsDown(BUTTON.DUCK) ? config.AATurret_Speed_Slow : inputState.IsDown(BUTTON.SPRINT) ? config.AATurret_Speed_Fast : config.AATurret_Speed_Norm;

                pivotY.transform.localEulerAngles += new Vector3(0, inputState.IsDown(BUTTON.LEFT) ? -speed : inputState.IsDown(BUTTON.RIGHT) ? speed : 0, 0);
                pivotX.transform.localEulerAngles += new Vector3(inputState.IsDown(BUTTON.FORWARD) ? -speed : inputState.IsDown(BUTTON.BACKWARD) ? speed : 0, 0, 0);
                // Clamp
                pivotX.transform.localEulerAngles = new Vector3(
                    pivotX.transform.localEulerAngles.x < 180 ? Mathf.Clamp(pivotX.transform.localEulerAngles.x, -1, 40) : Mathf.Clamp(pivotX.transform.localEulerAngles.x, 310, 361),
                    pivotX.transform.localEulerAngles.y,
                    pivotX.transform.localEulerAngles.z);
                
                pivotY.SendChildrenNetworkUpdateImmediate();
                pivotX.SendChildrenNetworkUpdateImmediate();
                spinner.SendChildrenNetworkUpdateImmediate();
            }

            public void Kill()
            {
                if (IndestructibleEntities != null && IndestructibleEntities.Contains(spinner))
                {
                    IndestructibleEntities.Remove(spinner as BaseCombatEntity);
                }

                spinner.AdminKill();
            }

            public void Fire()
            {
                if (!config.AATurret_InfiniteAmmo && AmmoStorage.inventory.GetAmount(AmmoItemID, true) < config.AATurret_AmountPerShot)
                {
                    foreach (var player in CurrentlyControlling)
                    {
                        DrawNoAmmoUI(player);
                    }

                    Invoke(() =>
                    {
                        foreach (var player in CurrentlyControlling)
                        {
                            CuiHelper.DestroyUi(player, NO_AMMO_CUI);
                        }
                    }, 5f);

                    return;
                }

                if (Mode == Modes.Standby || hasRecentlyFired)
                {
                    return;
                }

                if (Target != null && (Target is BaseVehicle))
                {
                    Instance.timer.Repeat(0.2f, 15, () =>
                    {
                        if (Target == null || Target.IsDestroyed)
                        {
                            return;
                        }

                        var driver = (Target as BaseVehicle)?.GetDriver();
                        if (driver == null)
                        {
                            return;
                        }

                        SendEffect(EFFECT_CODELOCK_UNLOCK, driver, driver.transform.position);
                    });

                    Instance.timer.Repeat(0.4f, 15, () =>
                    {
                        if (Target == null || Target.IsDestroyed)
                        {
                            return;
                        }

                        var driver = (Target as BaseVehicle)?.GetDriver();
                        if (driver == null)
                        {
                            return;
                        }

                        SendEffect(EFFECT_CODELOCK_LOCK, driver, driver.transform.position);
                    });
                }

                if (!hasRecentlyFired)
                {
                    hasRecentlyFired = true;
                    InvokeRepeating(FiringAction, 0f, config.AATurret_Rocket_FireInterval);

                    foreach (var player in CurrentlyControlling)
                    {
                        DrawReloadingUI(player);
                    }

                    Invoke(() =>
                    {
                        hasRecentlyFired = false;
                        foreach (var player in CurrentlyControlling)
                        {
                            CuiHelper.DestroyUi(player, RELOADING_CUI);
                        }
                    }, config.AATurret_Rocket_Cooldown);
                }
            }

            void FiringAction()
            {
                var rocketLauncher = rocketLaunchers[currentLauncherID];

                var rocket = GameManager.server.CreateEntity(HV_ROCKET_PREFAB, rocketLauncher.transform.position);

                if (Target != null)
                {
                    rocket.transform.LookAt(Target.transform);
                }
                else
                {
                    rocket.transform.rotation = camera.transform.rotation;
                }

                var projectile = rocket.GetComponent<ServerProjectile>();
                projectile.InitializeVelocity(rocket.transform.forward * config.AATurret_Rocket_InitialVelocity);
                rocket.Spawn();

                if (Target != null)
                {
                    var comp = rocket.gameObject.AddComponent<AARocket>();
                    comp.Target = Target;
                }

                currentLauncherID++;

                if (!config.AATurret_InfiniteAmmo)
                {
                    AmmoStorage.inventory.Take(null, AmmoItemID, config.AATurret_AmountPerShot);
                }

                if (currentLauncherID >= rocketLaunchers.Count || Mode == Modes.Standby || Mode == Modes.Manual_single || (!config.AATurret_InfiniteAmmo && AmmoStorage.inventory.GetAmount(AmmoItemID, true) < config.AATurret_AmountPerShot))
                {
                    currentLauncherID = 0;
                    CancelInvoke(FiringAction);
                }
            }

            public void LoseTarget()
            {
                Target = null;
                Mode = Modes.Standby;

                foreach (var player in CurrentlyControlling)
                {
                    DrawDefaultCrosshair(player);
                    CuiHelper.DestroyUi(player, LOCKED_UI_CUI);
                    CuiHelper.DestroyUi(player, ACQUIRING_TARGET_PARENT_CUI);
                    DrawTargetLost(player);
                    SendEffect("assets/prefabs/npc/autoturret/effects/targetlost.prefab", player, camera.transform.position);
                }
            }

            public void LockTarget(BaseEntity ent)
            {
                targetHits = 0;
                Target = ent;
                Mode = Modes.Automatic;

                foreach (var player in CurrentlyControlling)
                {
                    Invoke(() => { CuiHelper.DestroyUi(player, ACQUIRING_TARGET_PARENT_CUI); }, 1f);

                    CuiHelper.DestroyUi(player, AA_CROSSHAIR_CUI);
                    SendEffect("assets/prefabs/npc/autoturret/effects/targetacquired.prefab", player, camera.transform.position);
                }
            }

            #region UI

            void DrawInitialUI(BasePlayer player)
            {
                if (player == null)
                {
                    return;
                }

                var elements = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = AA_TURRET_CUI,
                        Parent = "Overlay"
                    },
                    new CuiElement
                    {
                        Name = CONTROLS_HELP_CUI,
                        Parent = AA_TURRET_CUI,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = GetText("AATurret_ControlsHelp", player),
                                FontSize = 16,
                                Font = "droidsansmono.ttf",
                                Align = TextAnchor.LowerRight,
                                Color = "0.756 0.749 0.756 1"
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 1",
                                Distance = "1 1"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.557 0.113",
                                AnchorMax = "0.8425 0.438",
                            }
                        }
                    },
                    new CuiElement
                    {
                        Name = CHANGEMODE_BTN_CUI,
                        Parent = AA_TURRET_CUI,
                        Components =
                        {
                            new CuiButtonComponent
                            {
                                Color = "0.756 0.749 0.756 1",
                                Command = CHANGEMODE_CCMD
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "130 455",
                                OffsetMax = "367 486"
                            }
                        }
                    },
                    new CuiElement
                    {
                        Name = FIRE_BTN_CUI,
                        Parent = AA_TURRET_CUI,
                        Components =
                        {
                            new CuiButtonComponent
                            {
                                Color = "0.756 0.749 0.756 1",
                                Command = FIRE_CCMD
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "130 490",
                                OffsetMax = "367 521"
                            }
                        }
                    },
                    new CuiElement
                    {
                        Name = CHANGEMODE_BTN_CUI + "_text",
                        Parent = CHANGEMODE_BTN_CUI,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Color = "0 0 0 1",
                                FontSize = 20,
                                Align = TextAnchor.MiddleCenter,
                                Text = GetText("changeMode", player)
                            },
                        }
                    },
                    new CuiElement
                    {
                        Name = FIRE_BTN_CUI + "_text",
                        Parent = FIRE_BTN_CUI,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Color = "0 0 0 1",
                                FontSize = 20,
                                Align = TextAnchor.MiddleCenter,
                                Text = GetText("fire", player)
                            },
                        }
                    },
                };

                CuiHelper.DestroyUi(player, AA_TURRET_CUI);
                CuiHelper.AddUi(player, elements);

                DrawMode(player);

                if (Target == null)
                {
                    DrawDefaultCrosshair(player);
                }
            }

            void DrawMode(BasePlayer player)
            {
                if (player == null)
                {
                    return;
                }

                string text;
                var color = "1 1 1 1";

                switch (Mode)
                {
                    case Modes.Automatic:
                        text = GetText("lockedOnTarget", player);
                        color = "0.55 0.78 0.24 1";
                        break;
                    case Modes.Standby:
                        text = GetText("waitingForTarget", player);
                        color = "0.866 0.521 0.06 1";
                        break;
                    case Modes.Manual_single:
                        text = GetText("manualSingle", player);
                        break;
                    case Modes.Manual_burst:
                        text = GetText("manualBurst", player);
                        break;
                    default:
                        text = "-";
                        break;
                }


                var elements = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = MODE_CUI,
                        Parent = AA_TURRET_CUI,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = text,
                                Color = color,
                                Align = TextAnchor.MiddleCenter,
                                FontSize = 25,
                                Font = "droidsansmono.ttf",
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 1",
                                Distance = "1 1"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.184 0.85",
                                AnchorMax = "0.824 0.896"
                            }
                        }
                    }
                };

                CuiHelper.DestroyUi(player, MODE_CUI);
                CuiHelper.AddUi(player, elements);
            }

            void DrawAcquiringTargetUI(BasePlayer player, float progress)
            {
                if (player == null)
                {
                    return;
                }

                var elements = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = ACQUIRING_TARGET_PARENT_CUI,
                        Parent = AA_TURRET_CUI
                    },
                    new CuiElement
                    {
                        Name = ACQUIRING_TARGET_CUI,
                        Parent = ACQUIRING_TARGET_PARENT_CUI,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "0.015 0.011 0.015 0.9",
                                Material = "assets/content/ui/uibackgroundblur.mat",
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5",
                                AnchorMax = "0.5 0.5",
                                OffsetMin = "125 -18",
                                OffsetMax = "400 18"
                            }
                        }
                    },

                    new CuiElement
                    {
                        Name = $"{ACQUIRING_TARGET_CUI}_text",
                        Parent = ACQUIRING_TARGET_CUI,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = GetText("acquiringTarget", player),
                                FontSize = 10,
                                Color = "0.756 0.749 0.756 1",
                                Font = "droidsansmono.ttf",
                                Align = TextAnchor.UpperCenter
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.1 0.1",
                                AnchorMax = "0.9 0.95"
                            }
                        }
                    },
                    new CuiElement
                    {
                        Name = $"{ACQUIRING_TARGET_CUI}_percentage",
                        Parent = ACQUIRING_TARGET_PARENT_CUI,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = progress == 1 ? GetText("targetAcquired", player) : $"{(int)(progress * 100)} %",
                                FontSize = 14,
                                Color = "0.756 0.749 0.756 1",
                                Align = TextAnchor.LowerCenter,
                                Font = "droidsansmono.ttf",
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0.015 0.011 0.015 1",
                                Distance = "1 1"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5",
                                AnchorMax = "0.5 0.5",
                                OffsetMin = "125 -18",
                                OffsetMax = "400 18"
                            }
                        }
                    },

                    new CuiElement
                    {
                        Name = $"{ACQUIRING_TARGET_CUI}_progressbar",
                        Parent = ACQUIRING_TARGET_CUI,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "0.756 0.749 0.756 1"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = $"{progress} 0.52"
                            }
                        }
                    }
                };

                CuiHelper.DestroyUi(player, ACQUIRING_TARGET_PARENT_CUI);
                CuiHelper.AddUi(player, elements);
            }

            void DrawDefaultCrosshair(BasePlayer player, string color = "1 1 1 0.8")
            {
                if (player == null)
                {
                    return;
                }

                var elements = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = AA_CROSSHAIR_CUI,
                        Parent = AA_TURRET_CUI,
                        Components =
                        {
                            new CuiImageComponent() 
                            {
                                Png = GetImageID("MountableTurrets_aa_crosshair"),
                                Color = color
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 0.8",
                                Distance = "1 1",
                                UseGraphicAlpha = true
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.49 0.48",
                                AnchorMax = "0.51 0.52",
                            }
                        }
                    }
                };

                CuiHelper.DestroyUi(player, AA_CROSSHAIR_CUI);
                CuiHelper.AddUi(player, elements);
            }

            void DrawTargetLost(BasePlayer player)
            {
                if (player == null)
                {
                    return;
                }

                var elements = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = TARGET_LOST_CUI,
                        Parent = AA_TURRET_CUI,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = GetText("targetLost", player),
                                Color = "0.8 0.28 0.2 0.8",
                                FontSize = 20,
                                Font = "droidsansmono.ttf",
                                Align = TextAnchor.LowerCenter
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 0.8",
                                Distance = "1 1",
                                UseGraphicAlpha = true
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.4 0.42",
                                AnchorMax = "0.6 0.46"
                            }
                        }
                    }
                };

                CuiHelper.DestroyUi(player, TARGET_LOST_CUI);
                CuiHelper.AddUi(player, elements);

                Invoke(() =>
                {
                    if (player == null)
                    {
                        return;
                    }

                    CuiHelper.DestroyUi(player, TARGET_LOST_CUI);
                }, 2.5f);
            }

            void DrawReloadingUI(BasePlayer player)
            {
                if (player == null)
                {
                    return;
                }

                var elements = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = RELOADING_CUI,
                        Parent = AA_TURRET_CUI,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = GetText("reloading", player),
                                Color = "0.28 0.28 0.87 0.9",
                                FontSize = 22,
                                Font = "droidsansmono.ttf",
                                Align = TextAnchor.LowerCenter
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 0.8",
                                Distance = "1 1",
                                UseGraphicAlpha = true
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.4 0.36",
                                AnchorMax = "0.6 0.42"
                            }
                        }
                    }
                };
                CuiHelper.DestroyUi(player, RELOADING_CUI);
                CuiHelper.AddUi(player, elements);
            }

            void DrawNoAmmoUI(BasePlayer player)
            {
                if (player == null)
                {
                    return;
                }

                var elements = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = NO_AMMO_CUI,
                        Parent = AA_TURRET_CUI,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = GetText("noAmmo", player),
                                Color = "0.8 0.28 0.2 0.8",
                                FontSize = 22,
                                Font = "droidsansmono.ttf",
                                Align = TextAnchor.LowerCenter
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 0.8",
                                Distance = "1 1",
                                UseGraphicAlpha = true
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.4 0.28",
                                AnchorMax = "0.6 0.35"
                            }
                        }
                    }
                };
                CuiHelper.DestroyUi(player, NO_AMMO_CUI);
                CuiHelper.AddUi(player, elements);
            }

            void RedrawUI(BasePlayer player, float distance)
            {
                if (player == null)
                {
                    return;
                }

                var lineSize = Mathf.Clamp(2f / distance, 0.01f, 0.02f);

                var info = $"HP: {(int)Target.Health()} / {(int)Target.MaxHealth()}";

                var crosshairSize = Mathf.Clamp(2f / distance, 0.01f, 0.28f);

                var elements = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = LOCKED_UI_CUI,
                        Parent = AA_TURRET_CUI
                    },

                    new CuiElement
                    {
                        Name = AA_CROSSHAIR_CUI,
                        Parent = LOCKED_UI_CUI,
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Png = GetImageID("MountableTurrets_aa_crosshair_locked"),
                                Color = "0.8 0.28 0.2 0.8"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{0.5f - crosshairSize} {0.5f - crosshairSize}",
                                AnchorMax = $"{0.5f + crosshairSize} {0.5f + crosshairSize}",
                            }
                        }
                    },

                    new CuiElement
                    {
                        Name = TARGET_DISTANCE_CUI,
                        Parent = LOCKED_UI_CUI,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Png = GetImageID("MountableTurrets_line"),
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{0.495f + lineSize} {0.51f - lineSize}",
                                AnchorMax = $"{0.5f + (lineSize * 6f)} {0.505f + lineSize}"
                            }
                        }
                    },

                    new CuiElement
                    {
                        Name = $"{TARGET_DISTANCE_CUI}_distance",
                        Parent = TARGET_DISTANCE_CUI,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = $"{(int)distance} m",
                                FontSize = distance < 100 ? 15 : distance < 130 ? 11 : 9,
                                Align = TextAnchor.LowerCenter,
                                Font = "droidsansmono.ttf",
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.2 1",
                                AnchorMax = "0.8 2"
                            }
                        }
                    },

                    new CuiElement
                    {
                        Name = TARGET_INFO_CUI,
                        Parent = LOCKED_UI_CUI,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "0.015 0.011 0.015 0.9",
                                Material = "assets/content/ui/uibackgroundblur.mat",
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5",
                                AnchorMax = "0.5 0.5",
                                OffsetMin = "-400 -18",
                                OffsetMax = "-125 18"
                            }
                        }
                    },

                    new CuiElement
                    {
                        Name = $"{TARGET_INFO_CUI}_text",
                        Parent = TARGET_INFO_CUI,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = Target.ShortPrefabName.ToUpper(),
                                FontSize = 12,
                                Color = "0.756 0.749 0.756 1",
                                Font = "droidsansmono.ttf",
                                Align = TextAnchor.UpperCenter
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.1 0.1",
                                AnchorMax = "0.92 0.99"
                            }
                        }
                    },

                    new CuiElement
                    {
                        Name = $"{TARGET_INFO_CUI}_line",
                        Parent = TARGET_INFO_CUI,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "0.756 0.749 0.756 1"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.1 0.55",
                                AnchorMax = "0.9 0.57"
                            }
                        }
                    },

                    new CuiElement
                    {
                        Name = $"{TARGET_INFO_CUI}_info",
                        Parent = TARGET_INFO_CUI,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = info,
                                FontSize = 12,
                                Color = "0.756 0.749 0.756 1",
                                Align = TextAnchor.LowerCenter,
                                Font = "droidsansmono.ttf",
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0.015 0.011 0.015 1",
                                Distance = "1 1"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1"
                            }
                        }
                    }
                };

                CuiHelper.DestroyUi(player, LOCKED_UI_CUI);
                CuiHelper.AddUi(player, elements);
            }

            #endregion
        }

        public sealed class MachineGunTurret : FacepunchBehaviour, ISpawnable
        {
            public List<BaseProjectile> Guns;
            public bool IsManuallyPlaced { get; private set; }
            public BasePlayer CurrentlyMounted { get; set; }
            public BaseMountable Mountable { get; set; }
            public DroppedItemContainer AmmoStorage { get; private set; }
            public int AmmoItemID { get; private set; }

            List<BaseEntity> gunPipes;
            BaseEntity spinner;
            BaseEntity sign1;
            BaseEntity sign2;

            bool fireFlag = false;

            public static Item CreateItem()
            {
                var item = ItemManager.CreateByItemID(SPINNER_ITEMID, 1, config.MachinegunTurret_Item_SkinID);
                item.name = config.MachinegunTurret_Item_Name;
                return item;
            }

            public static MachineGunTurret Spawn(Vector3 position, Quaternion rotation, bool isManual = false, int ammoAmount = 0)
            {
                var rot = rotation.eulerAngles;
                var spinner = GameManager.server.CreateEntity(SPINNER_PREFAB, position, Quaternion.Euler(rot.x, rot.y + 180f, rot.z)) as SpinnerWheel;
                UnityEngine.Object.DestroyImmediate(spinner.gameObject.GetComponent<DestroyOnGroundMissing>());

                if (isManual)
                {
                    Instance.NextFrame(() =>
                    {
                        if (spinner == null || spinner.IsDestroyed)
                        {
                            return;
                        }

                        spinner.gameObject.AddComponent<DestroyOnGroundMissing>();
                    });
                }
                else
                {
                    IndestructibleEntities.Add(spinner);
                }

                spinner.Spawn();
                spinner.EnableSaving(false);
                spinner.SetMaxHealth(config.MachinegunTurret_HP);
                spinner.SetHealth(config.MachinegunTurret_HP);

                TurretEntities.Add(spinner);
                MachineGunSpinners.Add(spinner);

                var comp = spinner.gameObject.AddComponent<MachineGunTurret>();
                comp.spinner = spinner;

                var mountable = AttachEntity(spinner, MOUNTABLE_PREFAB, new Vector3(0, 0, -1.1f));

                comp.Mountable = mountable as BaseMountable;
                TurretMountables.Add(comp.Mountable, comp);

                comp.IsManuallyPlaced = isManual;
                comp.Guns = new List<BaseProjectile>();
                comp.gunPipes = new List<BaseEntity>();

                comp.Guns.Add(AttachEntity(mountable, GUN_PREFAB, new Vector3(-0.185f, 1.2f, 1.05f), new Vector3(75f, 170f, 90f)) as BaseProjectile);
                comp.Guns.Add(AttachEntity(mountable, GUN_PREFAB, new Vector3(0.22f, 1.2f, 1.05f), new Vector3(75f, 170f, 90f)) as BaseProjectile);

                foreach (var entry in comp.Guns)
                {
                    TurretEntities.Add(entry);
                }

                comp.gunPipes.Add(AttachWorldmodel(mountable, 95950017, new Vector3(-0.214200005f, 0.999800026f, 1.04340005f), Quaternion.Euler(358.549927f, 296.170074f, 93.3828354f)));
                comp.gunPipes.Add(AttachWorldmodel(mountable, 95950017, new Vector3(0.196199998f, 1.00240004f, 1.06159997f), Quaternion.Euler(358.549927f, 296.170074f, 93.3828354f)));

                AttachWorldmodel(mountable, 95950017, new Vector3(-0.0221999995f, 0.618900001f, 1.07029998f), Quaternion.Euler(272.916382f, 186.168625f, 171.15332f));

                AttachWorldmodel(mountable, 95950017, new Vector3(-0.470062256f, 0.31463623f, 1.06530762f), Quaternion.Euler(342.903992f, 322.703857f, 72.1478958f));
                AttachWorldmodel(mountable, 95950017, new Vector3(0.348800004f, 0.32159999f, 1.08290005f), Quaternion.Euler(3.92509413f, 185.853104f, 63.247654f));
                AttachWorldmodel(mountable, 95950017, new Vector3(-0.00741577148f, 0.324584961f, 0.953613281f), Quaternion.Euler(3.69534063f, 280.289368f, 70.2239761f));
                AttachWorldmodel(mountable, 95950017, new Vector3(-0.0303000007f, 0.332199991f, 1.27900004f), Quaternion.Euler(35.316021f, 195.122696f, 99.852272f));

                AttachWorldmodel(mountable, -1994909036, new Vector3(-0.0429992676f, 0.486175537f, 1.35366943f), Quaternion.Euler(64.5f, 0, 0));

                AttachWorldmodel(mountable, 1199391518, new Vector3(0.434967041f, 0.193389893f, 1.31152344f), Quaternion.Euler(293.490662f, 265.162415f, 354.207245f));
                AttachWorldmodel(mountable, 1199391518, new Vector3(-0.249938965f, 0.642608643f, 0.989990234f), Quaternion.Euler(339.973389f, 278.344421f, 78.2604675f));

                comp.sign1 = AttachEntity(mountable, SIGN_PREFAB, new Vector3(-0.272000551f, 0.507080078f, 1.1f), new Vector3(0, 0, -90f));
                comp.sign2 = AttachEntity(mountable, SIGN_PREFAB, new Vector3(0.272000551f, 0.507080078f, 1.1f), new Vector3(0, 180f, -90f));
                TurretEntities.Add(comp.sign1);
                TurretEntities.Add(comp.sign2);
                IndestructibleEntities.Add(comp.sign1 as BaseCombatEntity);
                IndestructibleEntities.Add(comp.sign2 as BaseCombatEntity);

                MachineGunTurrets.Add(comp);

                if (!config.MachinegunTurret_InfiniteAmmo)
                {
                    var itemContainer = GameManager.server.CreateEntity(LOOTABLE_PREFAB, spinner.transform.position, spinner.transform.rotation) as DroppedItemContainer;

                    itemContainer.transform.localPosition = new Vector3(0, 0.1f, 0);
                    itemContainer.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    itemContainer.SetParent(spinner);

                    UnityEngine.Object.DestroyImmediate(itemContainer.gameObject.GetComponent<Rigidbody>());

                    itemContainer.inventory = new ItemContainer();
                    itemContainer.inventory.ServerInitialize(null, 6);
                    itemContainer.inventory.GiveUID();
                    itemContainer.inventory.entityOwner = spinner;
                    itemContainer.inventory.SetFlag(ItemContainer.Flag.NoItemInput, false);

                    itemContainer.Spawn();
                    IndestructibleEntities.Add(itemContainer);
                    itemContainer.CancelInvoke(itemContainer.RemoveMe);

                    itemContainer.EnableSaving(false);
                    itemContainer.SendNetworkUpdateImmediate();

                    comp.AmmoStorage = itemContainer;
                    MachineGunTurretContainers.Add(itemContainer.inventory);

                    var ammoDef = ItemManager.FindItemDefinition(config.MachinegunTurret_AmmoShortname);

                    itemContainer.inventory.AddItem(ammoDef, ammoAmount);
                    comp.AmmoItemID = ammoDef.itemid;
                }

                comp.Invoke(() =>
                {
                    if (comp?.Guns == null)
                    {
                        return;
                    }

                    foreach (var gun in comp.Guns)
                    {
                        gun.primaryMagazine.ammoType = MachineGunTurretAmmoType;
                        gun.transform.localRotation = Quaternion.Euler(75f, 170f, 90f);
                        gun.SendNetworkUpdate_Position();
                    }

                    comp.InvokeRepeating(comp.RefreshPositions, 0f, 5f);
                }, 0.15f);

                return comp;
            }

            void OnKilled()
            {
                CancelInvoke(RefreshPositions);
                if (CurrentlyMounted != null)
                {
                    Dismount(CurrentlyMounted);
                }

                if (MachineGunTurrets != null && MachineGunTurrets.Contains(this))
                {
                    MachineGunTurrets.Remove(this);
                }
            }

            public BaseEntity GetEntity() => spinner;

            public void RemoveColliders()
            {
                UnityEngine.Object.DestroyImmediate(sign1.gameObject.GetComponent<MeshCollider>());
                UnityEngine.Object.DestroyImmediate(sign2.gameObject.GetComponent<MeshCollider>());
                UnityEngine.Object.DestroyImmediate(spinner.gameObject.GetComponent<MeshCollider>());
            }

            public void Kill()
            {
                if (IndestructibleEntities != null && IndestructibleEntities.Contains(spinner))
                {
                    IndestructibleEntities.Remove(spinner as BaseCombatEntity);
                }

                IndestructibleEntities.Remove(sign1 as BaseCombatEntity);
                IndestructibleEntities.Remove(sign2 as BaseCombatEntity);
                spinner.AdminKill();
            }

            void OnDestroy()
            {
                if (CurrentlyMounted != null)
                {
                    Dismount(CurrentlyMounted);
                }

                if (TurretMountables != null)
                {
                    TurretMountables.Remove(Mountable);
                }

                OnKilled();
            }

            void RefreshPositions()
            {
                if (spinner == null || spinner.IsDestroyed || Guns == null)
                {
                    return;
                }

                foreach (var gun in Guns)
                {
                    gun.SendNetworkUpdate_Position();
                }


                foreach (var subscriber in Mountable.net.group.subscribers)
                {
                }

                if (AmmoStorage == null)
                {
                    return;
                }

                AmmoStorage.transform.position = spinner.transform.position + new Vector3(0, 0.1f, 0);
                AmmoStorage.SendNetworkUpdate_Position();
            }

            void RefreshPlayerPosition()
            {
                if (Mountable == null || Mountable.IsDestroyed)
                {
                    return;
                }

                CurrentlyMounted?.Teleport(Mountable.transform.position);
            }

            public void Mount(BasePlayer player)
            {
                if (CurrentlyMounted != null)
                {
                    return;
                }

                CurrentlyMounted = player;
                player.EnsureDismounted();
                InvokeRepeating(RefreshPlayerPosition, 0f, 1f);
                player.MountObject(Mountable);
                OnMounted(player);
            }

            public void Dismount(BasePlayer player)
            {
                if (player == null)
                {
                    return;
                }

                if (Mountable?.dismountPositions != null)
                {
                    Mountable.dismountPositions = new Transform[] { player.transform };
                }

                CancelInvoke(RefreshPlayerPosition);
                CurrentlyMounted = null;
                player.EnsureDismounted();
                player.DismountObject();

                if (Mountable != null)
                {
                    player.Teleport(Mountable.transform.position);
                }

                OnDismounted(player);
            }

            public void OnPlayerInput(BasePlayer player)
            {
                fireFlag = !fireFlag;
                if (player == null)
                {
                    return;
                }

                foreach (var gun in Guns)
                {
                    var y_angle = player.eyes.rotation.eulerAngles.y - transform.rotation.eulerAngles.y;
                    var y_angle_normalized = y_angle - (180f * Mathf.Floor((y_angle + 180f) / 180f));

                    y_angle = y_angle > 180 || y_angle < -180 ? -360 <= y_angle && y_angle <= -100 ? 180 - Mathf.Abs(y_angle_normalized) : y_angle_normalized : y_angle;
                    y_angle = y_angle > 0 && y_angle >= MACHINEGUNTURRET_CLAMP_ANGLE ? MACHINEGUNTURRET_CLAMP_ANGLE : y_angle < 0 && y_angle <= -MACHINEGUNTURRET_CLAMP_ANGLE ? -MACHINEGUNTURRET_CLAMP_ANGLE : y_angle;

                    var x_angle = player.eyes.rotation.eulerAngles.x;
                    x_angle = x_angle < 100 ? Mathf.Clamp(x_angle, 0, 40) : x_angle;


                    gun.transform.localRotation = Quaternion.Euler(x_angle + 106f, y_angle, 270f);
                    gun.SendNetworkUpdate_Position();

                    foreach (var pipe in gunPipes)
                    {
                        pipe.transform.localRotation = Quaternion.Euler(pipe.transform.localRotation.eulerAngles.x, y_angle + 120f, pipe.transform.localRotation.eulerAngles.z);
                        pipe.SendNetworkUpdate_Position();
                    }

                    if (!player.InSafeZone() && player.serverInput.IsDown(BUTTON.FIRE_PRIMARY) && Guns.IndexOf(gun) % 2 == 0 == fireFlag
                        && (config.MachinegunTurret_InfiniteAmmo || AmmoStorage?.inventory?.GetAmount(AmmoItemID, true) > 0))
                    {
                        gun.ServerUse();
                        gun.primaryMagazine.contents = gun.primaryMagazine.capacity;

                        if (!config.MachinegunTurret_InfiniteAmmo)
                        {
                            AmmoStorage.inventory.Take(null, AmmoItemID, config.MachinegunTurret_AmountPerShot);
                        }
                    }
                }
            }

            public void OnMounted(BasePlayer player)
            {
                var elements = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = CROSSHAIR_CUI,
                        Parent = "Hud",
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Png = GetImageID("MountableTurrets_crosshair"),
                                FadeIn = 0.2f
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 1",
                                Distance = "1 1"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5",
                                AnchorMax = "0.5 0.5",
                                OffsetMin = "-7 -7",
                                OffsetMax = "7 7"
                            }
                        }
                    }
                };
                CuiHelper.AddUi(player, elements);
            }

            public void OnDismounted(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, CROSSHAIR_CUI);
            }
        }

        public sealed class MonumentCompStation : FacepunchBehaviour, ISpawnable
        {
            public bool IsManuallyPlaced => false;

            MonumentInfo _monument;
            ComputerStation _compStation;

            public static MonumentCompStation Spawn(Vector3 position, Quaternion rotation)
            {
                var station = GameManager.server.CreateEntity(COMP_STATION_PREFAB, position, rotation) as ComputerStation;
                station.Spawn();
                TurretEntities.Add(station);
                IndestructibleEntities.Add(station);
                var comp = station.gameObject.AddComponent<MonumentCompStation>();
                return comp;
            }

            void Awake()
            {
                _compStation = GetComponent<ComputerStation>();
                _compStation.EnableSaving(false);

                _monument = GetMonumentOnPosition(transform.position);
                if (_monument == null)
                {
                    Instance.PrintWarning("Static computer station spawned NOT on a monument!");
                }

                UpdateTurretList();
            }

            void OnDestroy()
            {
                TurretEntities?.Remove(_compStation);
                IndestructibleEntities?.Remove(_compStation);
            }

            public void RemoveColliders()
            {
                UnityEngine.Object.DestroyImmediate(_compStation.gameObject.GetComponent<MeshCollider>());
            }

            public BaseEntity GetEntity() => _compStation;

            public void Kill()
            {
                if (IndestructibleEntities != null && IndestructibleEntities.Contains(_compStation))
                {
                    IndestructibleEntities.Remove(_compStation);
                }

                _compStation.AdminKill();
            }

            public void UpdateTurretList()
            {
                _compStation.controlBookmarks.Clear();
                foreach (var entry in MonumentSpawnables[_monument])
                {
                    if (entry is AATurret)
                    {
                        var turret = entry as AATurret;
                        _compStation.ForceAddBookmark(turret.CCTV_ID);
                    }
                }

                _compStation.SendNetworkUpdateImmediate();
            }
        }

        public sealed class EntityComputerStation : FacepunchBehaviour, ISpawnable
        {
            ComputerStation _compStation;
            BaseNetworkable parentNetworkable;

            public bool IsManuallyPlaced => false;

            public static EntityComputerStation Spawn(Vector3 position, Quaternion rotation, BaseNetworkable parent)
            {
                var station = GameManager.server.CreateEntity(COMP_STATION_PREFAB, position, rotation) as ComputerStation;
                UnityEngine.Object.DestroyImmediate(station.gameObject.GetComponent<MeshCollider>());
                station.Spawn();

                TurretEntities.Add(station);
                IndestructibleEntities.Add(station);

                var comp = station.gameObject.AddComponent<EntityComputerStation>();
                comp.parentNetworkable = parent;
                comp.UpdateTurretList();

                return comp;
            }

            void Awake()
            {
                _compStation = GetComponent<ComputerStation>();
                _compStation.EnableSaving(false);
            }

            void OnDestroy()
            {
                TurretEntities?.Remove(_compStation);
                IndestructibleEntities?.Remove(_compStation);
            }

            public void Kill()
            {
                if (IndestructibleEntities != null && IndestructibleEntities.Contains(_compStation))
                {
                    IndestructibleEntities.Remove(_compStation);
                }

                _compStation.AdminKill();
            }

            public BaseEntity GetEntity() => _compStation;

            public void UpdateTurretList()
            {
                _compStation.controlBookmarks.Clear();

                foreach (var child in parentNetworkable.children)
                {
                    if (!AASpinners.Contains(child))
                    {
                        continue;
                    }

                    var turret = child.gameObject.GetComponent<AATurret>();
                    _compStation.ForceAddBookmark(turret.CCTV_ID);
                }

                _compStation.SendNetworkUpdateImmediate();
            }
        }

        public interface ISpawnable
        {
            bool IsManuallyPlaced { get; }
            BaseEntity GetEntity();
            void Kill();
        }

        #endregion

        #region Hooks

        void Init()
        {
            InitHarmony();
            Instance = this;
            TurretEntities = new List<BaseEntity>();
            AARockets = new List<AARocket>();
            AATurrets = new Dictionary<string, AATurret>();
            MachineGunTurrets = new List<MachineGunTurret>();
            TurretMountables = new Dictionary<BaseMountable, MachineGunTurret>();
            PlayersCurrentlyControllingRCTurrets = new Dictionary<BasePlayer, AATurret>();
            IRTrapCount = new Dictionary<BaseEntity, int>();
            AASpinners = new List<BaseEntity>();
            MachineGunSpinners = new List<BaseEntity>();
            MachineGunTurretContainers = new List<ItemContainer>();
            AATurretContainers = new List<ItemContainer>();
            IndestructibleEntities = new List<BaseCombatEntity>();
            LastIRTrapUse = new Dictionary<BaseCombatEntity, float>();
            MonumentSpawnables = new Dictionary<MonumentInfo, List<ISpawnable>>();

            dataFiles = new DataFileSystem($"{Interface.Oxide.DataDirectory}\\MountableTurrets");
            isPluginInitialized = false;

            permission.RegisterPermission(AA_TURRET_GIVE_PERMISSION, this);
            permission.RegisterPermission(AA_TURRET_FREE_PERMISSION, this);
            permission.RegisterPermission(MACHINEGUN_TURRET_GIVE_PERMISSION, this);
            permission.RegisterPermission(MACHINEGUN_TURRET_FREE_PERMISSION, this);
            permission.RegisterPermission(ADMIN_PERMISSION, this);
        }

        void Unload()
        {
            SavePlayerData();

            foreach (var turret in TurretMountables.Values)
            {
                if (turret.CurrentlyMounted != null)
                {
                    CuiHelper.DestroyUi(turret.CurrentlyMounted, CROSSHAIR_CUI);
                }

                turret.Kill();
            }

            foreach (var turret in AATurrets.Values)
            {
                turret.Kill();
            }

            foreach (var entity in TurretEntities)
            {
                if (entity != null && !entity.IsDestroyed)
                {
                    entity.Kill();
                }
            }

            UnloadHarmony();

            TurretEntities = null;
            AARockets = null;
            MachineGunSpinners = null;
            AASpinners = null;
            TurretMountables = null;
            MachineGunTurrets = null;
            AATurrets = null;
            AATurretContainers = null;
            MachineGunTurretContainers = null;
            PlayersCurrentlyControllingRCTurrets = null;
            IRTrapCount = null;
            IndestructibleEntities = null;
            LastIRTrapUse = null;
            MonumentSpawnables = null;
            config = null;
            dataFiles = null;
            isPluginInitialized = false;
            Instance = null;
        }

        void OnServerInitialized()
        {
            if (ImageLibrary == null)
            {
                PrintError("ImageLibrary is not installed! Plugin is unloaded!");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }

            MachineGunTurretAmmoType = ItemManager.FindItemDefinition(config.MachinegunTurret_AmmoType);
            if (MachineGunTurretAmmoType == null || !config.MachinegunTurret_AmmoType.StartsWith("ammo.rifle"))
            {
                PrintError("Machine gun turret ammo type is invalid. Plugin is unloaded!");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }

            foreach (var entry in config.CUI_Images)
            {
                AddImage(entry.Key, entry.Value);
            }

            TurretDataLoader.Instantiate().BeginLoadingData();
        }

        void OnServerSave() => SavePlayerData();

        void OnNewSave(string filename)
        {
            PrintWarning("Server wiped! Clearing turret data...");
            ClearPlayerData();
        }

        object OnItemPickup(Item item, BasePlayer player)
        {
            var ent = item.GetWorldEntity();
            if (ent == null)
            {
                return null;
            }

            if (!TurretEntities.Contains(ent))
            {
                return null;
            }

            var parent = ent.GetParentEntity() as BaseMountable;
            if (parent == null || !TurretMountables.ContainsKey(parent))
            {
                return false;
            }

            TurretMountables[parent].Mount(player);

            return false;
        }
        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null)
            {
                return;
            }

            if (!player.isMounted)
            {
                return;
            }

            var mounted = player.GetMounted();
            if (mounted == null)
            {
                return;
            }

            if (TurretMountables.ContainsKey(mounted))
            {
                if (input.WasJustPressed(BUTTON.JUMP))
                {
                    TurretMountables[mounted].Dismount(player);
                    return;
                }

                TurretMountables[mounted].OnPlayerInput(player);
                return;
            }

            var parentEntity = mounted?.parentEntity;
            var prefabName = mounted.parentEntity.Get(true)?.ShortPrefabName;

            // if (parentEntity != null && prefabName != null && input.WasJustPressed(BUTTON.USE) && config.IRTrap_DeployPositions.ContainsKey(prefabName))
            // {
            //     var ent = mounted.parentEntity.Get(true) as BaseVehicle;
            //     if (ent == null)
            //     {
            //         return;
            //     }

            //     var drivers = Facepunch.Pool.GetList<BasePlayer>();
            //     ent.GetDrivers(drivers);
            //     if (drivers.Contains(player))
            //     {
            //         LaunchIRTraps(ent, player);
            //     }

            //     Facepunch.Pool.FreeList(ref drivers);
            //     return;
            // }
        }

        object CanMountEntity(BasePlayer player, BaseMountable entity)
        {
            if (!TurretMountables.ContainsKey(entity))
            {
                return null;
            }

            TurretMountables[entity].CurrentlyMounted = player;
            TurretMountables[entity].OnMounted(player);
            return null;
        }

        void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            if (!TurretMountables.ContainsKey(entity))
            {
                return;
            }

            TurretMountables[entity].CurrentlyMounted = null;
            TurretMountables[entity].OnDismounted(player);
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!isPluginInitialized)
            {
                return;
            }

            if (entity == null)
            {
                return;
            }

            MaybeAddTurrets(entity);
        }

        object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainer, int targetSlot, int amount)
        {
            var container = playerLoot.FindContainer(targetContainer);
            if (container == null)
            {
                return null;
            }

            if (MachineGunTurretContainers.Contains(container) && item.info.shortname != config.MachinegunTurret_AmmoShortname)
            {
                return false;
            }

            if (AATurretContainers.Contains(container) && item.info.shortname != config.AATurret_AmmoShortname)
            {
                return false;
            }
            return null;

            // if (item.info.shortname != "flare")
            // {
            //     return null;
            // }

            // if (container?.entityOwner?.ShortPrefabName == null)
            // {
            //     return null;
            // }

            // var parentEntity = container?.entityOwner?.parentEntity.Get(true);

            // if (parentEntity == null)
            // {
            //     return null;
            // }

            // if (!config.IRTrap_DeployPositions.ContainsKey(parentEntity.ShortPrefabName))
            // {
            //     return null;
            // }

            // if (!IRTrapCount.ContainsKey(parentEntity))
            // {
            //     IRTrapCount.Add(parentEntity, 0);
            // }

            // var toTake = Mathf.Clamp(amount + IRTrapCount[parentEntity], 0, config.IRTrap_StackSize) - IRTrapCount[parentEntity];
            // var movedAmount = playerLoot.Take(null, item.info.itemid, toTake);
            // IRTrapCount[parentEntity] += movedAmount;

            // DrawFlareUI(playerLoot.GetComponent<BasePlayer>(), IRTrapCount[parentEntity]);

            // return false;
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (plan == null)
            {
                return;
            }

            var item = plan.GetItem();
            if (item == null)
            {
                return;
            }

            var pos = go.transform.position;
            var rot = go.transform.rotation;
            var doDestroy = false;

            if (item.info.itemid == SPINNER_ITEMID && item.skin == config.MachinegunTurret_Item_SkinID && item.name == config.MachinegunTurret_Item_Name)
            {
                MachineGunTurret.Spawn(pos, rot, isManual: true);
                doDestroy = true;
            }

            if (item.info.itemid == SPINNER_ITEMID && item.skin == config.AATurret_Item_SkinID && item.name == config.AATurret_Item_Name)
            {
                var aaturret = AATurret.Spawn(pos, rot, isManual: true);
                doDestroy = true;

                var player = plan.GetOwnerPlayer();
                if (player != null)
                {
                    PrintToChat(player, string.Format(GetText("aaTurretIDIs", player), aaturret.CCTV_ID));
                }
            }

            if (doDestroy)
            {
                NextFrame(() => { go.GetComponent<BaseEntity>().AdminKill(); });
            }
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null)
            {
                return null;
            }

            if (config.PVE && (
                    entity is BuildingBlock ||
                    entity is DecayEntity ||
                    entity is BaseMountable ||
                    entity is BasePlayer))
            {
                if (info?.Initiator != null && info.Initiator.ShortPrefabName == "standingdriver" &&
                    info?.WeaponPrefab != null && info.WeaponPrefab.ShortPrefabName == "m249.entity")
                {
                    return false;
                }

                if (info.WeaponPrefab != null && info.WeaponPrefab.ShortPrefabName == "rocket_hv" && info.Initiator == null)
                {
                    return false;
                }
            }

            if (info.WeaponPrefab != null && info.WeaponPrefab.ShortPrefabName == "rocket_hv" && info.Initiator == null)
            {
                var entityType = "other";
                if (entity is BaseMountable)
                {
                    entityType = "vehicle";
                }

                if (entity is BuildingBlock)
                {
                    entityType = "buildingblock";
                }

                if (entity is BasePlayer)
                {
                    entityType = "player";
                }

                info.damageTypes.ScaleAll(config.AATurret_DamageMultipliers[entityType]);
            }

            if (!IndestructibleEntities.Contains(entity))
            {
                return null;
            }

            return false;
        }

        object OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null)
            {
                return null;
            }

            if (config.EntityTurrets.ContainsKey(entity.PrefabName))
            {
                var toRemove = Facepunch.Pool.GetList<ISpawnable>();

                foreach (var child in entity.children)
                {
                    var spawnable = child.gameObject.GetComponent<ISpawnable>();
                    if (spawnable == null)
                    {
                        continue;
                    }

                    toRemove.Add(spawnable);
                }

                foreach (var spawnable in toRemove)
                {
                    spawnable.Kill();
                }

                Facepunch.Pool.FreeList(ref toRemove);
                return null;
            }

            if (!IndestructibleEntities.Contains(entity))
            {
                return null;
            }

            var baseEntity = entity as BaseEntity;
            if (baseEntity != null && !entity.globalBroadcast && !ValidBounds.Test(baseEntity, entity.transform.position))
            {
                return null;
            }

            return false;
        }

        bool? CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity == null || player == null)
            {
                return null;
            }

            if (!TurretEntities.Contains(entity))
            {
                return null;
            }

            if (entity.PrefabName == SPINNER_PREFAB && player.IsBuildingAuthed() && entity.gameObject.GetComponent<ISpawnable>().IsManuallyPlaced)
            {
                entity.AdminKill();

                if (AASpinners.Contains(entity))
                {
                    player.GiveItem(AATurret.CreateItem());
                }
                else if (MachineGunSpinners.Contains(entity))
                {
                    player.GiveItem(MachineGunTurret.CreateItem());
                }
            }

            return false;
        }

        object OnSpinWheel(BasePlayer player, SpinnerWheel wheel)
        {
            if (!TurretEntities.Contains(wheel))
            {
                return null;
            }

            return false;
        }

        object OnBookmarkAdd(ComputerStation computerStation, BasePlayer player, string bookmarkName)
        {
            if (!TurretEntities.Contains(computerStation))
            {
                return null;
            }

            return false;
        }


        object OnBookmarkInput(ComputerStation computerStation, BasePlayer player, InputState inputState)
        {
            if (!PlayersCurrentlyControllingRCTurrets.ContainsKey(player))
            {
                return null;
            }

            PlayersCurrentlyControllingRCTurrets[player].OnPlayerInput(player);
            return false;
        }

        object OnBookmarkDelete(ComputerStation computerStation, BasePlayer player, string bookmarkName)
        {
            if (!TurretEntities.Contains(computerStation))
            {
                return null;
            }

            return false;
        }

        void OnBookmarkControlEnded(ComputerStation computerStation, BasePlayer player, BaseEntity controlledEntity)
        {
            if (!PlayersCurrentlyControllingRCTurrets.ContainsKey(player))
            {
                return;
            }

            PlayersCurrentlyControllingRCTurrets[player]?.OnControlEnd(player);
            PlayersCurrentlyControllingRCTurrets.Remove(player);
        }

        void OnBookmarkControlStarted(ComputerStation computerStation, BasePlayer player, string bookmarkName, IRemoteControllable remoteControllable)
        {
            if (!AATurrets.ContainsKey(bookmarkName))
            {
                return;
            }

            PlayersCurrentlyControllingRCTurrets.Add(player, AATurrets[bookmarkName]);
            AATurrets[bookmarkName].OnControlStart(player);
        }

        [HarmonyPatch(typeof(DroppedItem), "ShouldUpdateNetworkPosition")]
        private class DroppedItemShouldUpdateNetworkPositionPatch
        {
            static bool Prefix(DroppedItem __instance, bool __result)
            {
                try
                {
                    if(TurretEntities?.Contains(__instance) != true) return true;

                    var rB = typeof(DroppedItem).GetField("rB", BindingFlags.Instance).GetValue(__instance) as Rigidbody;
                    if (rB == null)
                    {
                        __result = true;
                        return false;
                    }
                    return true;
                }
                catch (NullReferenceException)
                {
                    return false;
                }
            }
        }

        [HarmonyPatch(typeof(DroppedItem), "BecomeActive")]
        private class DroppedItemBecomeActivePatch
        {
            static bool Prefix(DroppedItem __instance)
            {
                try
                {
                    if(TurretEntities?.Contains(__instance) != true) return true;

                    var rB = typeof(DroppedItem).GetField("rB", BindingFlags.Instance).GetValue(__instance) as Rigidbody;
                    if (rB == null)
                        return false;
                    return true;
                }
                catch (NullReferenceException)
                {
                    return false;
                }
            }
        }

        [HarmonyPatch(typeof(DroppedItem), "BecomeInactive")]
        private class DroppedItemBecomeInActivePatch
        {
            static bool Prefix(DroppedItem __instance)
            {
                try
                {
                    if(TurretEntities?.Contains(__instance) != true) return true;

                    var rB = typeof(DroppedItem).GetField("rB", BindingFlags.Instance).GetValue(__instance) as Rigidbody;
                    if (rB == null)
                        return false;
                    return true;
                }
                catch (NullReferenceException)
                {
                    return false;
                }
            }
        }


        [HarmonyPatch(typeof(DroppedItem), "SleepCheck")]
        private class DroppedItemSleepCheckPatch
        {
            static bool Prefix(DroppedItem __instance)
            {
                try
                {
                    if(TurretEntities?.Contains(__instance) != true) return true;

                    var rb = typeof(DroppedItem).GetField("rB")?.GetValue(__instance) as Rigidbody;

                    if (rb == null)
                        return false;

                    return true;
                }
                catch (NullReferenceException)
                {
                    return false;
                }
            }
        }

        #endregion

        #region Attach Methods

        public static BaseEntity AttachEntity(BaseEntity parent, string prefab, Vector3 position, Vector3 rotation = new Vector3())
        {
            var newEnt = GameManager.server.CreateEntity(prefab, parent.transform.position);

            if (!newEnt)
            {
                return null;
            }

            newEnt.Spawn();

            newEnt.transform.localPosition = position;
            newEnt.transform.localRotation = Quaternion.Euler(rotation);
            newEnt.SetParent(parent);

            UnityEngine.Object.DestroyImmediate(newEnt.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(newEnt.GetComponent<GroundWatch>());

            newEnt.OwnerID = 0;

            var combatEnt = newEnt as BaseCombatEntity;
            if (combatEnt)
            {
                combatEnt.pickup.enabled = false;
            }

            newEnt.EnableSaving(false);
            newEnt.SendNetworkUpdateImmediate();

            return newEnt;
        }

        public static BaseEntity AttachWorldmodel(BaseEntity parent, int item_id, Vector3 position, Quaternion rotation)
        {
            var item = ItemManager.CreateByItemID(item_id, 1);

            var ent = item.CreateWorldObject(position, rotation, parent) as DroppedItem;
            ent.EnableSaving(false);
            ent.CancelInvoke(ent.IdleDestroy);

            UnityEngine.Object.DestroyImmediate(ent.gameObject.GetComponent<Rigidbody>());

            TurretEntities.Add(ent);
            return ent;
        }

        public static GameObject AttachPrefab(GameObject parent, string prefab, Vector3 position, Vector3 rotation = new Vector3())
        {
            var newGO = GameManager.server.CreatePrefab(prefab, parent.transform.position, parent.transform.rotation);

            newGO.transform.localPosition = position;
            newGO.transform.localEulerAngles = rotation;
            newGO.transform.SetParent(parent.transform);

            return newGO;
        }

        #endregion

        #region IR-Trap

        // void OnLootEntity(BasePlayer player, BaseEntity entity)
        // {
        //     if (entity == null || !entity.ShortPrefabName.Contains("fuel_storage"))
        //     {
        //         return;
        //     }

        //     var parentEntity = entity?.parentEntity.Get(true);
        //     if (parentEntity == null || !config.IRTrap_DeployPositions.ContainsKey(parentEntity.ShortPrefabName))
        //     {
        //         return;
        //     }

        //     var flareCount = IRTrapCount.ContainsKey(parentEntity) ? IRTrapCount[parentEntity] : 0;
        //     DrawFlareUI(player, flareCount);
        // }

        // void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        // {
        //     var parentEntity = entity?.parentEntity.Get(true);
        //     if (parentEntity == null || !config.IRTrap_DeployPositions.ContainsKey(parentEntity.ShortPrefabName))
        //     {
        //         return;
        //     }

        //     CuiHelper.DestroyUi(player, IRTRAP_CUI);
        // }

        // object OnSamSiteTarget(SamSite samSite, BaseCombatEntity target)
        // {
        //     if (!config.IRTrap_AffectSAMSites)
        //     {
        //         return null;
        //     }

        //     if (!LastIRTrapUse.ContainsKey(target))
        //     {
        //         return null;
        //     }

        //     if (Time.time - LastIRTrapUse[target] > config.IRTrap_SAMSiteBlindTime)
        //     {
        //         return null;
        //     }

        //     return false;
        // }

        // void LaunchIRTraps(BaseEntity ent, BasePlayer driver = null)
        // {
        //     if (!IRTrapCount.ContainsKey(ent))
        //     {
        //         IRTrapCount.Add(ent, 0);
        //     }

        //     if (IRTrapCount[ent] < config.IRTrap_SpendPerUse)
        //     {
        //         if (driver != null)
        //         {
        //             driver.ShowToast(GameTip.Styles.Red_Normal, GetText("noIRTraps", driver));
        //         }

        //         return;
        //     }

        //     var combatEnt = ent as BaseCombatEntity;

        //     if (LastIRTrapUse.ContainsKey(combatEnt) && Time.time - LastIRTrapUse[combatEnt] < config.IRTrap_Cooldown)
        //     {
        //         return;
        //     }

        //     IRTrapCount[ent] -= config.IRTrap_SpendPerUse;

        //     var flares = Facepunch.Pool.GetList<BaseEntity>();

        //     if (ent == null || ent.IsDestroyed)
        //     {
        //         return;
        //     }

        //     foreach (var posData in config.IRTrap_DeployPositions[ent.ShortPrefabName])
        //     {
        //         var flare = GameManager.server.CreateEntity(FLARE_PREFAB, ent.transform.TransformPoint(posData.Position));
        //         flare.Spawn();
        //         flare.GetComponent<Rigidbody>().AddForce(ent.transform.TransformDirection(posData.Direction) * config.IRTrap_InitialVelocity);
        //         flare.GetComponent<TimedExplosive>().SetFuse(config.IRTrap_FuseLength);

        //         flares.Add(flare);
        //     }

        //     foreach (var rocket in AARockets)
        //     {
        //         if (rocket.Target == ent)
        //         {
        //             rocket.Target = flares.GetRandom();
        //         }
        //     }

        //     foreach (var turret in AATurrets.Values)
        //     {
        //         if (turret.Target == ent)
        //         {
        //             turret.LoseTarget();
        //         }
        //     }

        //     if (LastIRTrapUse.ContainsKey(combatEnt))
        //     {
        //         LastIRTrapUse[combatEnt] = Time.time;
        //     }
        //     else
        //     {
        //         LastIRTrapUse.Add(combatEnt, Time.time);
        //     }

        //     Facepunch.Pool.FreeList(ref flares);

        //     SendEffect(EFFECT_PAGER_BEEP, driver, driver.transform.position);
        // }

        // void DrawFlareUI(BasePlayer player, int flareCount)
        // {
        //     var elements = new CuiElementContainer
        //     {
        //         new CuiElement
        //         {
        //             Name = IRTRAP_CUI,
        //             Parent = "Overlay",
        //         },
        //         new CuiElement
        //         {
        //             Parent = IRTRAP_CUI,
        //             Name = IRTRAP_CUI + "_label",
        //             Components =
        //             {
        //                 new CuiTextComponent
        //                 {
        //                     Text = GetText("irTrapStorage", player),
        //                     FontSize = 13,
        //                     Align = TextAnchor.MiddleRight,
        //                     Color = "0.870 0.831 0.796 1"
        //                 },
        //                 new CuiRectTransformComponent
        //                 {
        //                     AnchorMin = "0.5 0",
        //                     AnchorMax = "0.5 0",
        //                     OffsetMin = "192 173",
        //                     OffsetMax = "560 195"
        //                 }
        //             }
        //         },
        //         new CuiElement
        //         {
        //             Parent = IRTRAP_CUI,
        //             Name = IRTRAP_CUI + "_icon",
        //             Components =
        //             {
        //                 new CuiRawImageComponent
        //                 {
        //                     Sprite = FLARE_ICON
        //                 },
        //                 new CuiRectTransformComponent
        //                 {
        //                     AnchorMin = "0.5 0",
        //                     AnchorMax = "0.5 0",
        //                     OffsetMin = "476 110",
        //                     OffsetMax = "534 168"
        //                 }
        //             }
        //         },
        //         new CuiElement
        //         {
        //             Parent = IRTRAP_CUI,
        //             Name = IRTRAP_CUI + "_count",
        //             Components =
        //             {
        //                 new CuiTextComponent
        //                 {
        //                     Text = "x" + flareCount.ToString(),
        //                     Color = "0.9686 0.9216 0.8824 0.5",
        //                     FontSize = 13,
        //                     Align = TextAnchor.LowerRight
        //                 },
        //                 new CuiRectTransformComponent
        //                 {
        //                     AnchorMin = "0.5 0",
        //                     AnchorMax = "0.5 0",
        //                     OffsetMin = "476 110",
        //                     OffsetMax = "534 168"
        //                 }
        //             }
        //         }
        //     };

        //     if (config.ShowIRTrapInfoText)
        //     {
        //         elements.Add(new CuiElement
        //         {
        //             Name = IRTRAP_CUI + "_infobox",
        //             Parent = IRTRAP_CUI,
        //             Components =
        //             {
        //                 new CuiImageComponent
        //                 {
        //                     Color = "0.3137 0.302 0.2824 0.6",
        //                     Material = "assets/content/ui/uibackgroundblur.mat"
        //                 },
        //                 new CuiRectTransformComponent
        //                 {
        //                     AnchorMin = "0.5 0",
        //                     AnchorMax = "0.5 0",
        //                     OffsetMin = "192 230",
        //                     OffsetMax = "572 310"
        //                 }
        //             }
        //         });
        //         elements.Add(new CuiElement
        //         {
        //             Name = IRTRAP_CUI + "_infobox_text",
        //             Parent = IRTRAP_CUI + "_infobox",
        //             Components =
        //             {
        //                 new CuiTextComponent
        //                 {
        //                     Text = GetText("IRTrap_InfoText", player),
        //                     FontSize = 14,
        //                     Font = "robotocondensed-regular.ttf",
        //                     Align = TextAnchor.UpperCenter,
        //                     Color = "0.9686 0.9215 0.8823 0.5235"
        //                 },
        //                 new CuiRectTransformComponent
        //                 {
        //                     AnchorMin = "0.01 0.05",
        //                     AnchorMax = "0.99 0.95",
        //                 }
        //             }
        //         });
        //     }

        //     CuiHelper.DestroyUi(player, IRTRAP_CUI);
        //     CuiHelper.AddUi(player, elements);
        // }

        #endregion

        #region Save/Load

        class TurretDataLoader : MonoBehaviour
        {
            public static TurretDataLoader Instantiate()
            {
                var go = new GameObject();
                return go.AddComponent<TurretDataLoader>();
            }

            public void BeginLoadingData()
            {
                StartCoroutine(LoadPlayerData());
                StartCoroutine(LoadMonumentData());
                StartCoroutine(SpawnTurretsOnEntities());
            }

            IEnumerator LoadPlayerData()
            {
                var sPlayerData = dataFiles.ReadObject<SerializedPlayerGeneratedData>(SAVES_DATAFILE_NAME);

                if (sPlayerData == null)
                {
                    yield break;
                }

                // foreach (var ent in BaseNetworkable.serverEntities)
                // {
                //     if (!config.IRTrap_DeployPositions.Keys.Contains(ent.ShortPrefabName))
                //     {
                //         continue;
                //     }

                //     var fuelSystemUID = (ent as BaseVehicle).GetFuelSystem().fuelStorageInstance.uid.Value;
                //     if (!sPlayerData.SerializedIRTrapCount.ContainsKey(fuelSystemUID))
                //     {
                //         continue;
                //     }

                //     IRTrapCount.Add(ent as BaseEntity, sPlayerData.SerializedIRTrapCount[fuelSystemUID]);
                // }

                // Instance.Puts("Restored IR-Trap containers");

                foreach (var sTurret in sPlayerData.SerializedTurrets)
                {
                    switch (sTurret.Type)
                    {
                        case "AATurret":
                            AATurret.Spawn(sTurret.Position, Quaternion.Euler(sTurret.Rotation), sTurret.CCTV_ID, true, sTurret.AmmoAmount);
                            break;
                        case "MachinegunTurret":
                            MachineGunTurret.Spawn(sTurret.Position, Quaternion.Euler(sTurret.Rotation), true, sTurret.AmmoAmount);
                            break;
                    }

                    yield return null;
                }

                Instance.Puts("Finished loading player data");
            }

            IEnumerator LoadMonumentData()
            {
                var sMonumentData = dataFiles.ReadObject<SerializedMonumentData>(MONUMENT_DATAFILE_NAME);
                if (sMonumentData?.SerializedMonumentDataEntries == null)
                {
                    yield break;
                }

                foreach (var sEntry in sMonumentData.SerializedMonumentDataEntries)
                {
                    foreach (var monument in TerrainMeta.Path.Monuments)
                    {
                        var monumentName = monument.displayPhrase.english;
                        if (monumentName != sEntry.Monument)
                        {
                            continue;
                        }

                        if (!MonumentSpawnables.ContainsKey(monument))
                        {
                            MonumentSpawnables.Add(monument, new List<ISpawnable>());
                        }

                        var position = monument.transform.TransformPoint(sEntry.Position);
                        var rotation = sEntry.Rotation + monument.transform.rotation.eulerAngles;

                        switch (sEntry.Type)
                        {
                            case "AATurret":
                                MonumentSpawnables[monument].Add(AATurret.Spawn(position, Quaternion.Euler(rotation), isManual: false));
                                break;
                            case "MachinegunTurret":
                                MonumentSpawnables[monument].Add(MachineGunTurret.Spawn(position, Quaternion.Euler(rotation), false));
                                break;
                            case "ComputerStation":
                                MonumentSpawnables[monument].Add(MonumentCompStation.Spawn(position, Quaternion.Euler(rotation)));
                                break;
                        }

                        yield return null;
                    }
                }

                foreach (var key in MonumentSpawnables.Keys)
                {
                    foreach (var spawnable in MonumentSpawnables[key])
                    {
                        (spawnable as MonumentCompStation)?.UpdateTurretList();
                    }
                }

                Instance.Puts("Finished loading monument data");
            }

            IEnumerator SpawnTurretsOnEntities()
            {
                foreach (var networkable in BaseNetworkable.serverEntities)
                {
                    Instance.MaybeAddTurrets(networkable);
                }

                yield return null;

                isPluginInitialized = true;
                Instance.Puts("Finished spawning turrets on entities");
            }
        }

        class SerializedPlayerGeneratedData
        {
            public List<SerializedPlayerTurret> SerializedTurrets = new List<SerializedPlayerTurret>();
            public Dictionary<ulong, int> SerializedIRTrapCount = new Dictionary<ulong, int>();
        }

        class SerializedPlayerTurret
        {
            public string Type;
            public string CCTV_ID;
            public int AmmoAmount;
            public Vector3 Position;
            public Vector3 Rotation;
        }

        void SavePlayerData()
        {
            var sData = new SerializedPlayerGeneratedData();

            foreach (var aaTurret in AATurrets.Values)
            {
                if (!aaTurret.IsManuallyPlaced)
                {
                    continue;
                }

                var sTurret = new SerializedPlayerTurret();
                sTurret.Type = "AATurret";
                sTurret.CCTV_ID = aaTurret.CCTV_ID;
                sTurret.Position = aaTurret.transform.position;
                sTurret.Rotation = aaTurret.transform.rotation.eulerAngles;
                if (aaTurret.AmmoStorage != null)
                {
                    sTurret.AmmoAmount = aaTurret.AmmoStorage.inventory.GetAmount(aaTurret.AmmoItemID, true);
                }
                else
                {
                    sTurret.AmmoAmount = 0;
                }

                sData.SerializedTurrets.Add(sTurret);
            }

            foreach (var machineGunTurret in MachineGunTurrets)
            {
                if (!machineGunTurret.IsManuallyPlaced)
                {
                    continue;
                }

                var sTurret = new SerializedPlayerTurret();
                sTurret.Type = "MachinegunTurret";
                sTurret.Position = machineGunTurret.transform.position;
                sTurret.Rotation = machineGunTurret.transform.rotation.eulerAngles - new Vector3(0, 180f, 0);
                if (machineGunTurret.AmmoStorage != null)
                {
                    sTurret.AmmoAmount = machineGunTurret.AmmoStorage.inventory.GetAmount(machineGunTurret.AmmoItemID, true);
                }
                else
                {
                    sTurret.AmmoAmount = 0;
                }

                sData.SerializedTurrets.Add(sTurret);
            }

         /*   foreach (var entry in IRTrapCount.Keys)
            {
                if (entry.IsDestroyed)
                {
                    continue;
                }

                sData.SerializedIRTrapCount.Add((entry as BaseVehicle).GetFuelSystem().fuelStorageInstance.uid.Value, IRTrapCount[entry]);
            }*/


            dataFiles.WriteObject(SAVES_DATAFILE_NAME, sData);
        }

        void ClearPlayerData()
        {
            foreach (var turret in TurretMountables.Values)
            {
                if (turret.IsManuallyPlaced)
                {
                    turret.Kill();
                }
            }

            foreach (var turret in AATurrets.Values)
            {
                if (turret.IsManuallyPlaced)
                {
                    turret.Kill();
                }
            }

            MachineGunTurrets.Clear();
            TurretEntities.Clear();
            TurretMountables.Clear();
            AASpinners.Clear();
            MachineGunSpinners.Clear();
            IRTrapCount.Clear();
            AATurrets.Clear();
            PlayersCurrentlyControllingRCTurrets.Clear();

            dataFiles.WriteObject(SAVES_DATAFILE_NAME, new SerializedPlayerGeneratedData());
        }

        class SerializedMonumentData
        {
            public List<SerializedMonumentDataEntry> SerializedMonumentDataEntries = new List<SerializedMonumentDataEntry>();
        }

        class SerializedMonumentDataEntry
        {
            public string Type;
            public string Monument;
            public Vector3 Position;
            public Vector3 Rotation;
        }

        void SaveMonumentData()
        {
            var sData = new SerializedMonumentData();
            var savedMonuments = Facepunch.Pool.GetList<string>();

            foreach (var monument in MonumentSpawnables.Keys)
            {
                var monumentName = monument.displayPhrase.english;
                if (savedMonuments.Contains(monumentName))
                {
                    continue;
                }

                foreach (var spawnable in MonumentSpawnables[monument])
                {
                    var ent = spawnable.GetEntity();
                    if (ent == null || ent.IsDestroyed)
                    {
                        continue;
                    }

                    var sEntry = new SerializedMonumentDataEntry();

                    sEntry.Position = monument.transform.InverseTransformPoint(ent.transform.position);
                    sEntry.Rotation = ent.transform.rotation.eulerAngles - monument.transform.rotation.eulerAngles;
                    sEntry.Monument = monumentName;

                    if (AASpinners.Contains(ent))
                    {
                        sEntry.Type = "AATurret";
                    }
                    else if (MachineGunSpinners.Contains(ent))
                    {
                        sEntry.Type = "MachinegunTurret";
                        sEntry.Rotation -= new Vector3(0, 180f, 0);
                    }
                    else
                    {
                        sEntry.Type = "ComputerStation";
                    }

                    sData.SerializedMonumentDataEntries.Add(sEntry);
                }

                savedMonuments.Add(monumentName);
            }

            Facepunch.Pool.FreeList(ref savedMonuments);
            dataFiles.WriteObject(MONUMENT_DATAFILE_NAME, sData);
        }

        void MaybeAddTurrets(BaseNetworkable networkable)
        {
            if (!config.EntityTurrets.ContainsKey(networkable.PrefabName))
            {
                return;
            }

            foreach (var entry in config.EntityTurrets[networkable.PrefabName])
            {
                BaseEntity child = null;
                switch (entry.Type.ToLower())
                {
                    case "aaturret":
                    case "aa":
                        var aaturret = AATurret.Spawn(networkable.transform.position + entry.Position, Quaternion.Euler(networkable.transform.rotation.eulerAngles + entry.Rotation), isManual: false);
                        child = aaturret.GetEntity();
                        aaturret.RemoveColliders();
                        break;
                    case "machinegunturret":
                    case "mgturret":
                        var mgturret = MachineGunTurret.Spawn(networkable.transform.position + entry.Position, Quaternion.Euler(networkable.transform.rotation.eulerAngles + entry.Rotation), isManual: false);
                        child = mgturret.GetEntity();
                        mgturret.RemoveColliders();
                        break;
                    case "compstation":
                    case "computerstation":
                    case "comp":
                        child = EntityComputerStation.Spawn(networkable.transform.position + entry.Position, Quaternion.Euler(networkable.transform.rotation.eulerAngles + entry.Rotation), networkable).GetEntity();
                        break;
                }

                if (child == null)
                {
                    PrintError($"Could not spawn {entry.Type} turret for {networkable.PrefabName}!");
                    continue;
                }


                child.transform.localPosition = entry.Position;
                child.transform.localRotation = Quaternion.Euler(entry.Rotation);
                child.SetParent(networkable as BaseEntity);

                child.OwnerID = 0;

                child.EnableSaving(false);

                var combatEnt = child as BaseCombatEntity;
                if (combatEnt)
                {
                    combatEnt.pickup.enabled = false;
                }

                child.SendNetworkUpdateImmediate();
            }
        }

        #endregion

        #region Misc Methods

        public static void SendEffect(string effectPath, BasePlayer player, Vector3 position)
        {
            if (player == null)
            {
                return;
            }

            if (!Net.sv.IsConnected())
            {
                return;
            }

            var effect = new Effect(effectPath, position, position);
            effect.pooledstringid = StringPool.Get(effect.pooledString);

            var netWrite = Net.sv.StartWrite();
            netWrite.PacketID(Message.Type.Effect);
            effect.WriteToStream(netWrite);
            netWrite.Send(new SendInfo(player.net.connection));
        }

        public static void WriteLine(BasePlayer player, string message)
        {
            if (player == null)
            {
                Instance.Puts(message);
                return;
            }

            Instance.PrintToConsole(player, "[MountableTurrets] " + message);
        }

        public static MonumentInfo GetMonumentOnPosition(Vector3 pos)
        {
            foreach (var monument in TerrainMeta.Path.Monuments)
            {
                if (Vector3.Distance(pos, monument.transform.position) < monument.Bounds.size.x)
                {
                    return monument;
                }
            }

            return null;
        }

        public static void UpdateMonumentComputerStations(MonumentInfo monument)
        {
            foreach (var spawnable in MonumentSpawnables[monument])
            {
                (spawnable as MonumentCompStation)?.UpdateTurretList();
            }
        }

        #endregion

        #region Chat Commands

        [ChatCommand("mt")]
        void MTCommand(BasePlayer player, string command, string[] args) => MountableTurretsAdminCommand(player, command, args);

        [ChatCommand("mountableturrets")]
        void MountableTurretsAdminCommand(BasePlayer player, string command, string[] args)
        {
            if (!(player.IsAdmin || (!player.IsAdmin && permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))))
            {
                PrintToChat(player, GetText("noPerms", player));
                return;
            }

            if (args.Length == 0)
            {
                PrintToChat(player, GetText("mtUsage", player));
                return;
            }

            var monument = GetMonumentOnPosition(player.transform.position);

            if (monument == null)
            {
                PrintToChat(player, GetText("noMonument", player));
                return;
            }

            if (!MonumentSpawnables.ContainsKey(monument))
            {
                MonumentSpawnables.Add(monument, new List<ISpawnable>());
            }

            var monumentName = monument.displayPhrase.english;

            var dataChanged = false;

            switch (args[0].ToLower())
            {
                case "add":
                    if (args.Length == 1)
                    {
                        PrintToChat(player, GetText("mtAddUsage", player));
                        return;
                    }

                    var type = args[1].ToLower();

                    if (type != "aa" && type != "machinegun" && type != "mg" && type != "comp")
                    {
                        PrintToChat(player, GetText("mtAddUsage", player));
                        return;
                    }

                    RaycastHit spawnHit;
                    if (!Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out spawnHit, 50f))
                    {
                        PrintToChat(player, GetText("lookAtSurface", player));
                        return;
                    }

                    var globalPosition = spawnHit.point;
                    var positionOffset = monument.transform.InverseTransformPoint(globalPosition);
                    var globalRotation = new Vector3(0, player.eyes.rotation.eulerAngles.y + 180f, 0);
                    var rotationOffset = globalRotation - monument.transform.rotation.eulerAngles;

                    foreach (var monumentToSpawnOn in TerrainMeta.Path.Monuments)
                    {
                        if (monumentToSpawnOn.displayPhrase.english != monumentName)
                        {
                            continue;
                        }

                        if (!MonumentSpawnables.ContainsKey(monumentToSpawnOn))
                        {
                            MonumentSpawnables.Add(monumentToSpawnOn, new List<ISpawnable>());
                        }

                        var spawnPos = monumentToSpawnOn.transform.TransformPoint(positionOffset);
                        var spawnRot = Quaternion.Euler(monumentToSpawnOn.transform.rotation.eulerAngles + rotationOffset);

                        switch (args[1].ToLower())
                        {
                            case "aa":
                            case "aaturret":
                                MonumentSpawnables[monumentToSpawnOn].Add(AATurret.Spawn(spawnPos, spawnRot, isManual: false));
                                UpdateMonumentComputerStations(monumentToSpawnOn);
                                break;
                            case "mg":
                            case "machinegun":
                            case "machinegunturret":
                                MonumentSpawnables[monumentToSpawnOn].Add(MachineGunTurret.Spawn(spawnPos, spawnRot, false));
                                break;
                            case "comp":
                            case "compstation":
                            case "computerstation":
                                MonumentSpawnables[monumentToSpawnOn].Add(MonumentCompStation.Spawn(spawnPos, spawnRot));
                                break;
                        }

                        dataChanged = true;
                    }

                    PrintToChat(player, string.Format(GetText("addedToMonument", player), monumentName));

                    break;

                case "remove":
                    RaycastHit hit;
                    if (Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out hit, 50f))
                    {
                        var ent = hit.GetEntity();
                        if (ent == null)
                        {
                            PrintToChat(player, GetText("lookAtTurret", player));
                            return;
                        }

                        var pos = monument.transform.InverseTransformPoint(ent.transform.position);

                        foreach (var key in MonumentSpawnables.Keys)
                        {
                            if (monumentName != key.displayPhrase.english)
                            {
                                continue;
                            }

                            var isMonumentAffected = false;
                            var toRemove = Facepunch.Pool.GetList<ISpawnable>();

                            foreach (var spawnable in MonumentSpawnables[key])
                            {
                                var monumentEnt = spawnable.GetEntity();
                                var entPos = key.transform.InverseTransformPoint(monumentEnt.transform.position);
                                if (Vector3.Distance(pos, entPos) > 0.01f)
                                {
                                    continue;
                                }

                                toRemove.Add(spawnable);

                                isMonumentAffected = true;
                                dataChanged = true;
                            }

                            foreach (var entry in toRemove)
                            {
                                MonumentSpawnables[key].Remove(entry);
                                entry.Kill();
                            }

                            Facepunch.Pool.FreeList(ref toRemove);

                            if (isMonumentAffected)
                            {
                                UpdateMonumentComputerStations(key);
                            }
                        }

                        if (dataChanged)
                        {
                            PrintToChat(player, string.Format(GetText("removedFromMonument", player), monumentName));
                        }
                        else
                        {
                            PrintToChat(player, GetText("lookAtTurret", player));
                        }
                    }
                    else
                    {
                        PrintToChat(player, GetText("lookAtTurret", player));
                    }

                    break;
                case "reset":
                    foreach (var monumentToReset in TerrainMeta.Path.Monuments)
                    {
                        if (monumentToReset.displayPhrase.english != monumentName)
                        {
                            continue;
                        }

                        if (MonumentSpawnables.ContainsKey(monumentToReset))
                        {
                            foreach (var spawnable in MonumentSpawnables[monumentToReset])
                            {
                                spawnable.Kill();
                            }

                            MonumentSpawnables[monumentToReset].Clear();
                            dataChanged = true;
                        }
                    }

                    PrintToChat(player, string.Format(GetText("allRemoved", player), monumentName));
                    break;
                default:
                    PrintToChat(player, GetText("mtUsage", player));
                    break;
            }

            if (dataChanged)
            {
                SaveMonumentData();
            }
        }

        [ChatCommand("machinegunturret")]
        void GiveMachineGunTurretChatCommand(BasePlayer player) => GiveMGChatCommand(player);

        [ChatCommand("mgturret")]
        void GiveMGChatCommand(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, MACHINEGUN_TURRET_GIVE_PERMISSION))
            {
                PrintToChat(player, GetText("noPerms", player));
                return;
            }

            if (config.MachinegunTurret_Price.Key.ToLower() == "serverrewards" && ServerRewards == null)
            {
                PrintError("ServerRewards is not installed, but it is used in the config. Execution aborted!");
                PrintToChat(player, "ServerRewards is not installed, but it is used in the config. Execution aborted!");
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, MACHINEGUN_TURRET_FREE_PERMISSION) && config.MachinegunTurret_Price.Value > 0)
            {
                if (config.MachinegunTurret_Price.Key.ToLower() == "serverrewards")
                {
                    var points = CheckPoints(player.userID);

                    if (points == null || points < config.MachinegunTurret_Price.Value)
                    {
                        PrintToChat(player, GetText("notEnoughPoints", player));
                        return;
                    }

                    TakePoints(player.userID, config.MachinegunTurret_Price.Value);
                }
                else
                {
                    var itemDef = ItemManager.FindItemDefinition(config.MachinegunTurret_Price.Key);

                    if (player.inventory.GetAmount(itemDef.itemid) < config.MachinegunTurret_Price.Value)
                    {
                        PrintToChat(player, string.Format(GetText("notEnoughResources", player), itemDef.displayName.english.ToLower()));
                        return;
                    }

                    player.inventory.Take(null, itemDef.itemid, config.MachinegunTurret_Price.Value);
                }
            }

            player.GiveItem(MachineGunTurret.CreateItem());
            PrintToChat(player, GetText("mgTurretGiven", player));
        }

        [ChatCommand("antiaerialturret")]
        void GiveAntiAerialTurretChatCommand(BasePlayer player) => GiveAATurretChatCommand(player);

        [ChatCommand("aaturret")]
        void GiveAATurretChatCommand(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, AA_TURRET_GIVE_PERMISSION))
            {
                PrintToChat(player, GetText("noPerms", player));
                return;
            }

            if (config.AATurret_Price.Key.ToLower() == "serverrewards" && ServerRewards == null)
            {
                PrintError("ServerRewards is not installed, but it is used in the config. Execution aborted!");
                PrintToChat(player, "ServerRewards is not installed, but it is used in the config. Execution aborted!");
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, AA_TURRET_FREE_PERMISSION) && config.AATurret_Price.Value > 0)
            {
                if (config.AATurret_Price.Key.ToLower() == "serverrewards")
                {
                    var points = CheckPoints(player.userID);

                    if (points == null || points < config.AATurret_Price.Value)
                    {
                        PrintToChat(player, GetText("notEnoughPoints", player));
                        return;
                    }

                    TakePoints(player.userID, config.AATurret_Price.Value);
                }
                else
                {
                    var itemDef = ItemManager.FindItemDefinition(config.AATurret_Price.Key);

                    if (player.inventory.GetAmount(itemDef.itemid) < config.AATurret_Price.Value)
                    {
                        PrintToChat(player, string.Format(GetText("notEnoughResources", player), itemDef.displayName.english.ToLower()));
                        return;
                    }

                    player.inventory.Take(null, itemDef.itemid, config.AATurret_Price.Value);
                }
            }

            player.GiveItem(AATurret.CreateItem());
            PrintToChat(player, GetText("aaTurretGiven", player));
        }

        #endregion

        #region Console Commands

        [ConsoleCommand(CHANGEMODE_CCMD)]
        void AATurretChangeModeCCmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
            {
                return;
            }

            if (!PlayersCurrentlyControllingRCTurrets.ContainsKey(player))
            {
                return;
            }

            PlayersCurrentlyControllingRCTurrets[player].ChangeMode();
        }

        [ConsoleCommand(FIRE_CCMD)]
        void AATurretFireCCmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
            {
                return;
            }

            if (!PlayersCurrentlyControllingRCTurrets.ContainsKey(player))
            {
                return;
            }

            PlayersCurrentlyControllingRCTurrets[player].Fire();
        }


        [ConsoleCommand("giveaaturret")]
        void GiveAATurretCCmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            if (player != null && !permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
            {
                WriteLine(player, "You don't have permission to use this command!");
                return;
            }

            var args = arg.Args;
            if (args == null || args?.Length == 0)
            {
                WriteLine(player, "Usage: giveaaturret <player name>");
                return;
            }


            var target = BasePlayer.Find(string.Join(" ", arg.Args));
            if (target == null)
            {
                WriteLine(player, "Player not found!");
                return;
            }

            target.GiveItem(AATurret.CreateItem());
            PrintToChat(target, GetText("aaTurretGiven", target));

            WriteLine(player, "AA turret was given to " + target.displayName);
        }

        [ConsoleCommand("givemgturret")]
        void GiveMachineGunTurretCCmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            if (player != null && !permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
            {
                WriteLine(player, "You don't have permission to use this command!");
                return;
            }

            var args = arg.Args;
            if (args == null || args?.Length == 0)
            {
                WriteLine(player, "Usage: givemgturret <player name>");
                return;
            }


            var target = BasePlayer.Find(string.Join(" ", arg.Args));
            if (target == null)
            {
                WriteLine(player, "Player not found!");
                return;
            }

            target.GiveItem(MachineGunTurret.CreateItem());
            PrintToChat(target, GetText("mgTurretGiven", target));

            WriteLine(player, "Machine gun turret was given to " + target.displayName);
        }

        #endregion

        #region API

        [HookMethod("SpawnAATurret")]
        public BaseEntity SpawnAATurret(Vector3 position, Quaternion rotation) => AATurret.Spawn(position, rotation).GetEntity();

        [HookMethod("SpawnMachinegunTurret")]
        public BaseEntity SpawnMachinegunTurret(Vector3 position, Quaternion rotation) => MachineGunTurret.Spawn(position, rotation).GetEntity();

        [HookMethod("DestroyTurret")]
        public void Destroy(BaseEntity turret) => turret?.gameObject?.GetComponent<ISpawnable>()?.Kill();

        #endregion
    }
}