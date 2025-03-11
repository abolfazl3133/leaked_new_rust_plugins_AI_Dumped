using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("WaterBases", "Nikedemos", "1.0.20")]
    [Description("Allows vanilla-like constructions on water surface - and underwater too!")]

    public class WaterBases : RustPlugin
    {
        private static WaterBases Instance;
        #region CONST & STATIC

        public const string PREFAB_SHARK_ALIVE = "assets/rust.ai/agents/fish/simpleshark.prefab";
        public const string PREFAB_SHARK_DEAD = "assets/rust.ai/agents/fish/shark.corpse.prefab";

        public const string PREFAB_FLOOR_SQUARE = "assets/prefabs/building core/floor/floor.prefab";
        public const string PREFAB_FLOOR_TRIANGLE = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab";
        public const string PREFAB_BARREL = "assets/prefabs/resource/diesel barrel/diesel_barrel_world.prefab";

        public const string PREFAB_NETTING = "assets/prefabs/building/wall.frame.netting/wall.frame.netting.prefab";

        public const string PREFAB_BARREL_COLLECTABLE = "assets/content/structures/excavator/prefabs/diesel_collectable.prefab";

        public const string PREFAB_OILRIG_1 = "OilrigAI";
        public const string PREFAB_OILRIG_2 = "OilrigAI2";

        public const string PREFAB_POOL_BIG = "assets/prefabs/misc/summer_dlc/abovegroundpool/abovegroundpool.deployed.prefab";
        public const string PREFAB_POOL_SMALL = "assets/prefabs/misc/summer_dlc/paddling_pool/paddlingpool.deployed.prefab";

        public const string PREFAB_BUILD_EFFECT = "assets/bundled/prefabs/fx/build/frame_place.prefab";

        public const string CMD_WB_GIVE = "wb.give";

        public const int ITEM_INNER_TUBE = -697981032;
        public const int ITEM_BUILDING_PLANNER = 1525520776;

        public const ulong ITEM_SKIN_FOUNDATION_SQUARE = 2484982352;
        public const ulong ITEM_SKIN_FOUNDATION_TRIANGLE = 2485021365;

        public static LayerMask DeployedMask = LayerMask.GetMask("Deployed");
        public static LayerMask ConstructionMask = LayerMask.GetMask("Construction");

        public static BaseEntity.Flags FLAG_ALREADY_APPLIED = BaseEntity.Flags.OnFire;

        private static object ReusableObject;
        private static bool ReusableBool;
        private static string ReusableString;
        private static float ReusableFloat;

        private static bool ReusableBool2;

        private static string ReusableString2;
        private static float ReusableFloat2;
        private static int ReusableInt2;
        private static ulong ReusableUlong2;

        public static readonly List<string> FramePrefabs = new List<string>
        {
            "assets/prefabs/building core/floor.frame/floor.frame.prefab",
            "assets/prefabs/building core/floor.triangle.frame/floor.triangle.frame.prefab",
            "assets/prefabs/building core/wall.frame/wall.frame.prefab"
        };

        public static readonly List<ulong> RelevantSkins = new List<ulong>
        {
            ITEM_SKIN_FOUNDATION_SQUARE,
            ITEM_SKIN_FOUNDATION_TRIANGLE,
        };

        public static List<Vector3> FloorBarrelPositionsSquare = new List<Vector3>
        {
            new Vector3(0F, -0.4F, 0.33F),
            new Vector3(0F, -0.4F, -0.33F),                                                        
            new Vector3(-0.33F, -0.4F, 1.12F),
            new Vector3(-0.33F, -0.4F, 0.36F),
            new Vector3(-0.33F, -0.4F, -0.36F),
            new Vector3(-0.33F, -0.4F, -1.12F),
            new Vector3(0.33F, -0.4F, 1.12F),
            new Vector3(0.33F, -0.4F, 0.36F),
            new Vector3(0.33F, -0.4F, -1.12F),
            new Vector3(0.33F, -0.4F, -0.36F)
        };

        public static List<Vector3> FloorBarrelRotationsSquare = new List<Vector3>
        {
            new Vector3(0F, 90F, 90F),
            new Vector3(0F, 90F, 270F),
            new Vector3(0F, 0F, 90F),
            new Vector3(0F, 0F, 90F),
            new Vector3(0F, 0F, 90F),
            new Vector3(0F, 0F, 90F),
            new Vector3(0F, 0F, 270F),
            new Vector3(0F, 0F, 270F),
            new Vector3(0F, 0F, 270F),
            new Vector3(0F, 0F, 270F)
        };

        public static List<Vector3> FloorBarrelPositionsTriangle= new List<Vector3>
        {
            new Vector3(0F, -0.4F, -0.6F+1.5F),
            new Vector3(0.42F, -0.4F, -0.42F+1.5F),
            new Vector3(-0.42F, -0.4F, -0.42F+1.5F),
            new Vector3(0F, -0.4F, -1.17F+1.5F),
            new Vector3(0F, -0.4F, -1.17F+1.5F)
        };

        public static List<Vector3> FloorBarrelRotationsTriangle = new List<Vector3>
        {
            new Vector3(0F, 90F, 90F),
            new Vector3(0F, 0F, 90F),
            new Vector3(0F, 180F, 90F),
            new Vector3(0F, 180F, 90F),
            new Vector3(0F, 0F, 90F)
        };
        #endregion

        #region LANG
        public const string MSG_ITEM_NAME_TRIANGLE = "MSG_ITEM_NAME_TRIANGLE";
        public const string MSG_ITEM_NAME_SQUARE = "MSG_ITEM_NAME_SQUARE";

        public const string MSG_CHAT_PREFIX = "MSG_CHAT_PREFIX";
        public const string MSG_DEPLOY_ERROR = "MSG_DEPLOY_ERROR";
        public const string MSG_CRAFT = "MSG_CRAFT";

        public const string MSG_DEPLOY_RESULT_BUILDING_BLOCKED = "MSG_DEPLOY_RESULT_BUILDING_BLOCKED";
        public const string MSG_DEPLOY_RESULT_TOPOLOGY_MISMATCH = "MSG_DEPLOY_RESULT_TOPOLOGY_MISMATCH";
        public const string MSG_DEPLOY_RESULT_NO_PERM_DEPLOY = "MSG_DEPLOY_RESULT_NO_PERM_DEPLOY";
        public const string MSG_DEPLOY_RESULT_NO_PERM_EXPAND = "MSG_DEPLOY_RESULT_NO_PERM_EXPAND";
        public const string MSG_DEPLOY_RESULT_TOO_SHALLOW = "MSG_DEPLOY_RESULT_TOO_SHALLOW";
        public const string MSG_DEPLOY_RESULT_TOO_DEEP = "MSG_DEPLOY_RESULT_TOO_DEEP";
        public const string MSG_DEPLOY_RESULT_OUTSIDE_GRID = "MSG_DEPLOY_RESULT_OUTSIDE_GRID";
        public const string MSG_DEPLOY_RESULT_TOO_CLOSE_TO_OILRIG = "MSG_DEPLOY_RESULT_TOO_CLOSE_TO_OILRIG";
        public const string MSG_DEPLOY_RESULT_TOO_CLOSE_TO_SHORE = "MSG_DEPLOY_RESULT_TOO_CLOSE_TO_SHORE";
        public const string MSG_DEPLOY_RESULT_TOO_FAR_FROM_SHORE = "MSG_DEPLOY_RESULT_TOO_FAR_FROM_SHORE";
        public const string MSG_DEPLOY_RESULT_TOO_MANY_FOUNDATIONS = "MSG_DEPLOY_RESULT_TOO_MANY_FOUNDATIONS";
        public const string MSG_DEPLOY_RESULT_IN_POOL = "MSG_DEPLOY_RESULT_IN_POOL";
        public const string MSG_DEPLOY_RESULT_LACKS_ITEM = "MSG_DEPLOY_RESULT_LACKS_ITEM";

        public const string MSG_DEPLOY_RESULT_PEBCAK = "MSG_DEPLOY_RESULT_PEBCAK";
        public const string MSG_DEPLOY_RESULT_OCEANLEVEL = "MSG_DEPLOY_RESULT_OCEANLEVEL";

        public const string MSG_EXPAND_REINFORCEMENT_NOT_ALLOWED = "MSG_EXPAND_REINFORCEMENT_NOT_ALLOWED";
        public const string MSG_REINFORCEMENT_NOT_ALLOWED = "MSG_REINFORCEMENT_NOT_ALLOWED";
        public const string MSG_REINFORCEMENT_LACKS_ITEM = "MSG_REINFORCEMENT_LACKS_ITEM";
        public const string MSG_EXPAND_REINFORCEMENT_WRONG_ABOVE = "MSG_EXPAND_REINFORCEMENT_WRONG_ABOVE";
        public const string MSG_EXPAND_REINFORCEMENT_LACKS_ITEM = "MSG_EXPAND_REINFORCEMENT_LACKS_ITEM";

        public const string MSG_UNDERWATER_NETTING_NO_PERM = "MSG_UNDERWATER_NETTING_NO_PERM";
        public const string MSG_UNDERWATER_NETTING_TOO_SHALLOW = "MSG_UNDERWATER_NETTING_TOO_SHALLOW";
        public const string MSG_CRAFTING_NO_PERM = "MSG_CRAFTING_NO_PERM";
        public const string MSG_CRAFTING_NO_MATERIALS = "MSG_CRAFTING_NO_MATERIALS";
        public const string MSG_CRAFTING_INSUFFICIENT_WB_LEVEL = "MSG_CRAFTING_INSUFFICIENT_WB_LEVEL";

        public const string MSG_MAX_TIER_REACHED_MAIN = "MSG_MAX_TIER_REACHED_MAIN";
        public const string MSG_MAX_TIER_REACHED_FRAME = "MSG_MAX_TIER_REACHED_FRAME";
        public const string MSG_MAX_TIER_REACHED_FOUNDATION = "MSG_MAX_TIER_REACHED_FOUNDATION";

        public const string MSG_CARGO_PATH_WARNING = "MSG_CARGO_PATH_WARNING";
        public const string MSG_CARGO_PATH_PREVENTION = "MSG_CARGO_PATH_PREVENTION";

        public const string MSG_VALUE_HAS_BEEN_SET = "MSG_VALUE_HAS_BEEN_SET";
        public const string MSG_CFG_RUNDOWN_FORMAT = "MSG_CFG_RUNDOWN_FORMAT";
        public const string MSG_CFG_DETAILS_FORMAT = "MSG_CFG_DETAILS_FORMAT";
        public const string MSG_CFG_NO_SETTING_FOUND = "MSG_CFG_NO_SETTING_FOUND";
        public const string MSG_CFG_DEFAULT = "MSG_CFG_DEFAULT";

        private Dictionary<string, string> LangMessages = new Dictionary<string, string>
        {
            [MSG_CHAT_PREFIX] = "<color=#4897ce>[Water Bases]</color>",
            [MSG_DEPLOY_ERROR] = "ERROR:",
            [MSG_ITEM_NAME_SQUARE] = "Water Foundation (SQUARE)",
            [MSG_ITEM_NAME_TRIANGLE] = "Water Foundation (TRIANGLE)",
            [MSG_CRAFT] = "Craft",

            [MSG_DEPLOY_RESULT_BUILDING_BLOCKED] = "You're building blocked.",
            [MSG_DEPLOY_RESULT_TOPOLOGY_MISMATCH] = "You're not allowed to build on this topology.",
            [MSG_DEPLOY_RESULT_NO_PERM_DEPLOY] = "You're not allowed to deploy new water foundations.",
            [MSG_DEPLOY_RESULT_NO_PERM_EXPAND] = "You're not allowed to expand existing water foundations.",
            [MSG_DEPLOY_RESULT_TOO_SHALLOW] = "You're not allowed to build in water this shallow.",
            [MSG_DEPLOY_RESULT_TOO_DEEP] = "You're not allowed to build in water this deep.",
            [MSG_DEPLOY_RESULT_OUTSIDE_GRID] = "You're not allowed to build outside of the map grid.",
            [MSG_DEPLOY_RESULT_TOO_CLOSE_TO_OILRIG] = "You're not allowed to build this close to the Oil Rig.",
            [MSG_DEPLOY_RESULT_TOO_CLOSE_TO_SHORE] = "You're not allowed to build this close to shore.",
            [MSG_DEPLOY_RESULT_TOO_FAR_FROM_SHORE] = "You're not allowed to build this far from shore.",
            [MSG_DEPLOY_RESULT_TOO_MANY_FOUNDATIONS] = "You're not allowed this many water foundations per building.",
            [MSG_DEPLOY_RESULT_IN_POOL] = "Nice try, but building in swimming pools would be a little bit much.",

            [MSG_MAX_TIER_REACHED_MAIN] = "You're not allowed to upgrade walls/floors/windows/doorways to tier this high.",
            [MSG_MAX_TIER_REACHED_FRAME] = "You're not allowed to upgrade floor frames/wall frames to tier this high.",
            [MSG_MAX_TIER_REACHED_FOUNDATION] = "You're not allowed to upgrade water foundations to tier this high.",

            [MSG_CARGO_PATH_PREVENTION] = "You're not allowed to build this close to the path of the Cargo Ship.",
            [MSG_CARGO_PATH_WARNING] = "You're building in the path of the Cargo Ship. Your structure is at risk.",

            [MSG_EXPAND_REINFORCEMENT_NOT_ALLOWED] = "You're not allowed to expand reinforcements like this. Use a hammer on the fully repaired Water Foundation instead.",
            [MSG_REINFORCEMENT_LACKS_ITEM] = "You need a proper Water Foundation item in your inventory to reinforce this water foundation.",
            [MSG_EXPAND_REINFORCEMENT_LACKS_ITEM] = "You need 2 proper Water Foundation items in your inventory to expand this reinforced water foundation.",
            [MSG_REINFORCEMENT_NOT_ALLOWED] = "You're not allowed to reinforce Water Foundations.",
            [MSG_UNDERWATER_NETTING_NO_PERM] = "You're not allowed to deploy underwater netting.",

            [MSG_UNDERWATER_NETTING_TOO_SHALLOW] = "The water needs to be at least {0} meters deep for this netting to work.",

            [MSG_CRAFTING_NO_MATERIALS] = "You need {0} to craft a {1}.",
            [MSG_CRAFTING_NO_PERM] = "You're not allowed to craft Water Foundations.",
            [MSG_CRAFTING_INSUFFICIENT_WB_LEVEL] = "You need a Level {0} Workbench to craft Water Foundations.",
            [MSG_DEPLOY_RESULT_LACKS_ITEM] = "You need a proper Water Foundation item in your inventory to expand this water foundation.",
            [MSG_EXPAND_REINFORCEMENT_WRONG_ABOVE] = "You can't expand like that. The shapes of the reinforcement and the water foundation above it must match.",

            [MSG_VALUE_HAS_BEEN_SET] = "Config value <color=yellow>{0}</color> has been set to {1} by an admin.",
            [MSG_CFG_DEFAULT] = "Here's the current settings. Type <color=yellow>/wb_cfg settingName</color> with no parameters to see the description, the current value and what the accepted arguments are. Type <color=yellow>/wb_cfg settingName acceptedValue</color> to change the setting.",
            [MSG_CFG_RUNDOWN_FORMAT] = "/wb_cfg <color=yellow>{0}</color> (currently: {1})",
            [MSG_CFG_DETAILS_FORMAT] = "<color=green>{0}</color>:\n{1} ({2})\nThis value is currently set to: {3}\n",
            [MSG_CFG_NO_SETTING_FOUND] = "No setting with that name has been found. Type /wb_cfg to get a rundown.",

            [MSG_DEPLOY_RESULT_PEBCAK] = "That's not how you expand your foundations! You only need to deploy one blue doughnut as a starter. Then take out your building plan, select a square or triangle floor (NOT foundation!) and build just like in vanilla.",

            [MSG_DEPLOY_RESULT_OCEANLEVEL] = "You can only build or expand Water Foundations when the ocean level is {0}."
        };

        private static string MSG(string msg, string userID = null, params object[] args)
        {
            if (args == null)
            {
                return Instance.lang.GetMessage(msg, Instance, userID);
            }
            else
            {
                return string.Format(Instance.lang.GetMessage(msg, Instance, userID), args);
            }

        }

        private static string MSG_RESULT(DeploymentResult result, string userID = null)
        {
            switch (result)
            {
                default:
                case DeploymentResult.Success:
                    {
                        return null;
                    }
                case DeploymentResult.BuildingBlocked:
                    {
                        return $"{MSG(MSG_DEPLOY_ERROR, userID)} {MSG(MSG_DEPLOY_RESULT_BUILDING_BLOCKED, userID)}";
                    }
                case DeploymentResult.TopologyMismatch:
                    {
                        return $"{MSG(MSG_DEPLOY_ERROR, userID)} {MSG(MSG_DEPLOY_RESULT_TOPOLOGY_MISMATCH, userID)}";
                    }
                case DeploymentResult.NoPermissionToDeploy:
                    {
                        return $"{MSG(MSG_DEPLOY_ERROR, userID)} {MSG(MSG_DEPLOY_RESULT_NO_PERM_DEPLOY, userID)}";
                    }
                case DeploymentResult.NoPermissionToExpand:
                    {
                        return $"{MSG(MSG_DEPLOY_ERROR, userID)} {MSG(MSG_DEPLOY_RESULT_NO_PERM_EXPAND, userID)}";
                    }
                case DeploymentResult.WaterTooShallow:
                    {
                        return $"{MSG(MSG_DEPLOY_ERROR, userID)} {MSG(MSG_DEPLOY_RESULT_TOO_SHALLOW, userID)}";
                    }
                case DeploymentResult.WaterTooDeep:
                    {
                        return $"{MSG(MSG_DEPLOY_ERROR, userID)} {MSG(MSG_DEPLOY_RESULT_TOO_DEEP, userID)}";
                    }
                case DeploymentResult.DeployedInPool:
                    {
                        return $"{MSG(MSG_DEPLOY_ERROR, userID)} {MSG(MSG_DEPLOY_RESULT_IN_POOL, userID)}";
                    }
                case DeploymentResult.TooCloseToOilrig:
                    {
                        return $"{MSG(MSG_DEPLOY_ERROR, userID)} {MSG(MSG_DEPLOY_RESULT_TOO_CLOSE_TO_OILRIG, userID)}";
                    }
                case DeploymentResult.TooCloseToShore:
                    {
                        return $"{MSG(MSG_DEPLOY_ERROR, userID)} {MSG(MSG_DEPLOY_RESULT_TOO_CLOSE_TO_SHORE, userID)}";
                    }
                case DeploymentResult.TooFarFromShore:
                    {
                        return $"{MSG(MSG_DEPLOY_ERROR, userID)} {MSG(MSG_DEPLOY_RESULT_TOO_FAR_FROM_SHORE, userID)}";
                    }
                case DeploymentResult.TooManyFoundations:
                    {
                        return $"{MSG(MSG_DEPLOY_ERROR, userID)} {MSG(MSG_DEPLOY_RESULT_TOO_MANY_FOUNDATIONS, userID)}";
                    }
                case DeploymentResult.OnCargoShipPath:
                    {
                        return $"{MSG(MSG_DEPLOY_ERROR, userID)} {MSG(Instance.configData.CargoShipPathHandling == CargoPathHandling.Prevent ? MSG_CARGO_PATH_PREVENTION : MSG_CARGO_PATH_WARNING, userID)}";
                    }
                case DeploymentResult.NoRequiredItem:
                    {
                        return $"{MSG(MSG_DEPLOY_ERROR, userID)} {MSG(MSG_DEPLOY_RESULT_LACKS_ITEM, userID)}";
                    }
                case DeploymentResult.OutsideMapGrid:
                    {
                        return $"{MSG(MSG_DEPLOY_ERROR, userID)} {MSG(MSG_DEPLOY_RESULT_OUTSIDE_GRID, userID)}";
                    }
                case DeploymentResult.ProblemExistsBetweenChairAndKeyboard:
                    {
                        return $"{MSG(MSG_DEPLOY_ERROR, userID)} {MSG(MSG_DEPLOY_RESULT_PEBCAK, userID)}";
                    }
                case DeploymentResult.OceanLevel:
                    {
                        return $"{MSG(MSG_DEPLOY_ERROR, userID)} {MSG(MSG_DEPLOY_RESULT_OCEANLEVEL, userID, Instance.configData.GlobalOceanLevelBaseline)}";
                    }
            }
        }
        #endregion

        #region PERMISSIONS
        public const string PERM_ADMIN = "waterbases.admin";
        public const string PERM_VIP1 = "waterbases.vip1";
        public const string PERM_VIP2 = "waterbases.vip2";
        public const string PERM_VIP3 = "waterbases.vip3";
        public const string PERM_VIP4 = "waterbases.vip4";
        public const string PERM_VIP5 = "waterbases.vip5";
        #endregion

        #region HOOKS
        void OnServerInitialized()
        {
            Instance = this;

            ToastsCached = new Dictionary<string, Translate.Phrase>();

            lang.RegisterMessages(LangMessages, this);

            permission.RegisterPermission(PERM_ADMIN, this);
            permission.RegisterPermission(PERM_VIP1, this);
            permission.RegisterPermission(PERM_VIP2, this);
            permission.RegisterPermission(PERM_VIP3, this);
            permission.RegisterPermission(PERM_VIP4, this);
            permission.RegisterPermission(PERM_VIP5, this);

            AddCovalenceCommand(CMD_WB_GIVE, nameof(CommandGiveFoundation));

            LoadConfigData();
            ProcessConfigData();

            ConfigValues = new Dictionary<string, InteractiveConfigValue>();

            GenerateAllInteractiveConfigValues();
            
            //ConvertBrokenFlags();
            //KillOldFlags();

            RecalculateUnderwaterAndSharkHarvestLootWeights();

            GuiManager.ReGenerateGUI();
            
            ActivateSpecialStuff();
        }

        void Unload()
        {
            var stopwatch = new Stopwatch();

            stopwatch.Start();

            foreach (var helper in UnityEngine.Object.FindObjectsOfType<NettingSharkHelper>())
            {
                UnityEngine.Object.DestroyImmediate(helper);
            }

            var elapsed1 = stopwatch.ElapsedMilliseconds;

            stopwatch.Restart();

            foreach (var helper in UnityEngine.Object.FindObjectsOfType<InverseFoundationHelper>())
            {
                UnityEngine.Object.DestroyImmediate(helper);
            }

            var elapsed2 = stopwatch.ElapsedMilliseconds;

            stopwatch.Restart();

            foreach (var helper in UnityEngine.Object.FindObjectsOfType<UnderwaterNetHelper>())
            {
                UnityEngine.Object.DestroyImmediate(helper);
            }

            var elapsed3 = stopwatch.ElapsedMilliseconds;

            stopwatch.Restart();

            foreach (var player in BasePlayer.activePlayerList)
            {
                GuiManager.HideGUI(player);
            }

            var elapsed4 = stopwatch.ElapsedMilliseconds;

            stopwatch.Restart();

            configData.OilrigPositions = null;
            configData.ProfileTopologyMask = null;
            configData.NettingSharks = null;

            GuiManager.CleanupGUI();

            ToastsCached = null;

            stopwatch.Stop();

            var elapsed5 = stopwatch.ElapsedMilliseconds;

            stopwatch.Stop();

            PrintError($"UNLOAD TIMING DEBUG: PHASE 1 = {elapsed1} ms, PHASE 2 = {elapsed2} ms, PHASE 3 = {elapsed3} ms, PHASE 4 = {elapsed4} ms, PHASE 5 = {elapsed5} ms.");

            Instance = null;
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            GuiManager.HideGUI(player);
        }

        object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (!configData.SharkHarvestingLootEnabled)
            {
                return null;
            }

            if (dispenser.baseEntity.PrefabName != PREFAB_SHARK_DEAD)
            {
                return null;
            }

            float chance = dispenser.baseEntity.Health() / dispenser.baseEntity.MaxHealth();

            if (UnityEngine.Random.Range(0F, 1F) < chance)
            {
                var randomItem = CreateRandomSharkHarvestItem();

                if (randomItem == null)
                {
                    return null;
                }

                player.GiveItem(randomItem, BaseEntity.GiveItemReason.ResourceHarvested);
            }

            return null;
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (item.info.itemid != ITEM_BUILDING_PLANNER)
            {
                return;
            }

            var player = container.GetOwnerPlayer();

            if (player == null)
            {
                return;
            }

            if (player.svActiveItemID == item.uid)
            {
                GuiManager.HideGUI(player);
            }
        }

        void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (newItem != null)
            {
                if (newItem.info.itemid == ITEM_BUILDING_PLANNER)
                {
                    if (oldItem?.info.itemid != ITEM_BUILDING_PLANNER || oldItem == null)
                    {
                        GuiManager.ShowGUI(player);
                    }
                }
                else
                {
                    if (oldItem != null)
                    {
                        if (oldItem.info.itemid == ITEM_BUILDING_PLANNER)
                        {
                            GuiManager.HideGUI(player);
                        }
                    }
                }
            }
            else
            {
                if (oldItem != null)
                {
                    if (oldItem.info.itemid == ITEM_BUILDING_PLANNER)
                    {
                        GuiManager.HideGUI(player);
                    }
                }
            }


        }

        object OnItemRecycle(Item item, Recycler recycler)
        {
            if (!IsSkinRelevant(item.skin))
            {
                return null;
            }

            //var costList = Facepunch.Pool.GetList<SerializableItemAmount>();

            List<SerializableItemAmount> costList;

            if (item.skin == ITEM_SKIN_FOUNDATION_SQUARE)
            {
                costList = Instance.configData.CraftingCostSquare;
            }
            else
            {
                costList = Instance.configData.CraftingCostTriangle;
            }

            foreach (var cost in costList)
            {
                recycler.MoveItemToOutput(ItemManager.CreateByName(cost.Shortname, Mathf.CeilToInt((float)cost.Amount / 2F)));
            }

            //Facepunch.Pool.FreeList<SerializableItemAmount>(ref costList);

            item.UseItem(1);
            item.MarkDirty();

            return true;
        }
        
        object OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (IsInWaterBase(block))
            {
                var prof = GetPermissionProfile(player);

                bool isFrame = false;
                bool isWaterFoundation = false;

                var maxTier = prof.MaxBuildingGradeGeneric;

                if (IsWaterFoundation(block))
                {
                    maxTier = prof.MaxBuildingGradeWaterFoundations;
                    isWaterFoundation = true;
                }
                else
                {
                    if (FramePrefabs.Contains(block.PrefabName))
                    {
                        maxTier = prof.MaxBuildingGradeFrames;
                        isFrame = true;
                    }
                }

                //check if the grade has not exceeded config max
                if ((int)grade > (int)maxTier)
                {
                    TellMessage(player, MSG(isWaterFoundation ? MSG_MAX_TIER_REACHED_FOUNDATION : isFrame ? MSG_MAX_TIER_REACHED_FRAME : MSG_MAX_TIER_REACHED_MAIN, player.UserIDString), false, true);
                    return true;
                }

            }

            return null;
        }

        void OnEntitySpawned(SimpleShark shark)
        {
            if (Instance == null)
            {
                return;
            }

            if (configData == null)
            {
                return;
            }

            if (shark == null)
            {
                return;
            }

            if (!IsInternal(shark))
            {
                return;
            }

            if (shark.IsDestroyed)
            {
                return;
            }

            if (shark.net == null)
            {
                return;
            }

            if (configData.NettingSharks == null)
            {
                return;
            }

            var newNettingSharkCompo = shark.gameObject.AddComponent<NettingSharkHelper>();

            newNettingSharkCompo.Prepare(shark);

            if (configData.NettingSharks.ContainsKey(shark.net.ID.Value))
            {
                configData.NettingSharks.Remove(shark.net.ID.Value);
            }

            if (configData.NettingSharks.Count >= configData.UnderwaterNetsSharkPopulationLimitGlobal)
            {
                shark.Invoke(() => shark.Kill(), 0.02F);
            }

            configData.NettingSharks.Add(shark.net.ID.Value, shark);
        }

        void OnEntityKill(SimpleShark shark)
        {
            if (Instance == null)
            {
                return;
            }

            if (configData == null)
            {
                return;
            }

            if (!IsInternal(shark))
            {
                return;
            }

            if (!configData.NettingSharks.ContainsKey(shark.net.ID.Value))
            {
                configData.NettingSharks.Remove(shark.net.ID.Value);
            }

        }

        object OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (player.IsBuildingBlocked()) return null;

            //see if the player has enough resources to create inverse...

            if (info.HitEntity == null) return null;

            if (info.HitEntity.IsDestroyed) return null;

            var buildingBlock = info.HitEntity as BuildingBlock;

            if (buildingBlock == null)
            {
                return null;
            }

            if (buildingBlock.health != buildingBlock.MaxHealth())
            {
                return null;
            }

            if (!IsWaterFoundation(buildingBlock))
            {
                return null;
            }

            var allChildren = buildingBlock.GetComponentsInChildren<BuildingBlock>();

            BuildingBlock inverseFloorAlready = null;

            foreach (var block in allChildren)
            {
                if (block.net.ID == buildingBlock.net.ID) continue;

                inverseFloorAlready = block;
                break;
            }

            if (inverseFloorAlready == null)
            {
                var needed = FindNeededFoundationItemsInInventory(player, buildingBlock);

                if (needed == null)
                {
                    TellMessage(player, $"{MSG(MSG_DEPLOY_ERROR)} {MSG(MSG_REINFORCEMENT_LACKS_ITEM, player.UserIDString)}", false, true);
                    return true;
                }

                ConsumeFromSpecificItemList(ref needed);

                BuildInverseFoundationAtWaterFoundation(player, buildingBlock);
                return true;
            }

            return null;
        }

        void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            var baseEntity = gameObject.GetComponent<BaseEntity>();

            var maybeInflatable = baseEntity as WaterInflatable;

            //normal building, not from inflatable
            if (maybeInflatable == null)
            {
                var maybeDecayEntity = baseEntity as DecayEntity;

                if (maybeDecayEntity == null)
                {
                    return;
                }

                var newBuildingBlock = baseEntity as BuildingBlock;


                var player = planner.GetOwnerPlayer();

                if (newBuildingBlock != null)
                {
                    if (IsInWaterBase(newBuildingBlock))
                    {
                        //should this BECOME a water foundation? or an inverse foundation?
                        if (newBuildingBlock.PrefabName == PREFAB_FLOOR_SQUARE || newBuildingBlock.PrefabName == PREFAB_FLOOR_TRIANGLE)
                        {
                            bool isInverse;
                            var nearby = TryGetAdjacentSpecialFoundation(newBuildingBlock, out isInverse);

                            if (nearby == null)
                            {
                                //for now, maybe let's leave it as generic?

                                if (Mathf.Abs(newBuildingBlock.transform.localEulerAngles.z) < 0.1F)
                                {
                                    MarkAsInternalGenericDecayEntity(newBuildingBlock);
                                }
                                else
                                {
                                    MarkAsInternalGenericDecayEntityInversed(newBuildingBlock);
                                }

                                return;
                            }

                            if (!isInverse)
                            {
                                if (newBuildingBlock.PrefabName == PREFAB_FLOOR_SQUARE)
                                {
                                    MarkAsInternalFoundationSquare(newBuildingBlock);
                                }
                                else
                                {
                                    MarkAsInternalFoundationTriangle(newBuildingBlock);
                                }

                                TurnFloorIntoWaterFoundation(newBuildingBlock, player, false);
                                return;
                            }
                            else
                            {
                                //Instance.PrintWarning("PLACING INVERSE FOUNDATION...");

                                if (newBuildingBlock.PrefabName == PREFAB_FLOOR_SQUARE)
                                {
                                    MarkAsInternalFoundationSquareInversed(newBuildingBlock);
                                }
                                else
                                {
                                    MarkAsInternalFoundationTriangleInversed(newBuildingBlock);
                                }

                                var maybeAboveYou = TryFindWaterFoundationAboveYou(newBuildingBlock);

                                bool fatalErrorOccured = false;

                                if (maybeAboveYou == null)
                                {
                                    //Instance.PrintWarning("MAYBE ABOVE YOU = NULL");

                                    var prof = GetPermissionProfile(player);
                                    //first and foremost: how many water foundation does the building have?
                                    //if you don't do this check, you're gonna have a REALLY bad time

                                    var waterFoundations = CountBuildingWaterFoundations(newBuildingBlock);

                                    if (waterFoundations +1 >= prof.MaxWaterFoundationsPerBuilding)
                                    {
                                        TellMessage(player, $"{MSG(MSG_DEPLOY_ERROR, player.UserIDString)} {MSG(MSG_DEPLOY_RESULT_TOO_MANY_FOUNDATIONS, player.UserIDString)}", false, true);
                                        DelayedKillWithGibs(newBuildingBlock);
                                        return;
                                    }

                                    if (!prof.CanExpandWaterFoundations)
                                    {
                                        TellMessage(player, $"{MSG(MSG_DEPLOY_ERROR, player.UserIDString)} {MSG(MSG_EXPAND_REINFORCEMENT_NOT_ALLOWED, player.UserIDString)}", false, true);
                                        DelayedKillWithGibs(newBuildingBlock);
                                        return;
                                    }

                                    //do you have enough of needed kind? you need two!

                                    var neededItems = FindNeededFoundationItemsInInventory(player, newBuildingBlock, 2);

                                    if (neededItems == null)
                                    {
                                        TellMessage(player, $"{MSG(MSG_DEPLOY_ERROR, player.UserIDString)} {MSG(MSG_EXPAND_REINFORCEMENT_LACKS_ITEM, player.UserIDString)}", false, true);
                                        DelayedKillWithGibs(newBuildingBlock);
                                        return;
                                    }

                                    //consume the item
                                    ConsumeFromSpecificItemList(ref neededItems);

                                    //let's make a foundation first. relative please.
                                    var transf = GetWorldTransformRelativeTo(newBuildingBlock.transform, Vector3.down * 1.5F, new Vector3(0F, 0F, -180F));

                                    //Instance.PrintError("ATTEMPTING TO BUILD A NORMAL FOUNDATION FIRST...");

                                    maybeAboveYou = BuildWaterFoundationAt(player, false, newBuildingBlock.PrefabName == PREFAB_FLOOR_SQUARE ? ITEM_SKIN_FOUNDATION_SQUARE : ITEM_SKIN_FOUNDATION_TRIANGLE, transf.Item1, transf.Item2);

                                    if (maybeAboveYou == null)
                                    {
                                        //Instance.PrintError("FATAL ERROR: BUILDING A NORMAL WATER FOUNDATION ABOVE HAS FAILED, IT'S NULL!");
                                        fatalErrorOccured = true;
                                    }
                                    else
                                    {
                                        if (maybeAboveYou.net == null)
                                        {
                                            //Instance.PrintError("FATAL ERROR: BUILDING A NORMAL WATER FOUNDATION ABOVE HAS FAILED, IT HAS .NET THAT IS NULL!");
                                            fatalErrorOccured = true;
                                        }
                                    }

                                    maybeAboveYou.AttachToBuilding(nearby);


                                    if (newBuildingBlock.PrefabName == PREFAB_FLOOR_SQUARE)
                                    {
                                        MarkAsInternalFoundationSquareInversed(newBuildingBlock);
                                    }
                                    else
                                    {
                                        MarkAsInternalFoundationTriangleInversed(newBuildingBlock);
                                    }

                                    newBuildingBlock.AttachToBuilding(nearby);

                                }
                                else
                                {
                                    //the foundation above you is not null. ARE YOU CATCHING YOURSELF HERE?!


                                    //Instance.PrintError("MAYBE ABOVE YOU = NOT NULL");

                                    if (maybeAboveYou == newBuildingBlock)
                                    {
                                        //Instance.PrintError("FATAL ERROR: INVERSE FOUNDATION FOUND ITSELF AS THE NORMAL FOUNDATION ABOVE");
                                        fatalErrorOccured = true;
                                    }
                                    else
                                    {
                                        //Instance.PrintError("OK: MAYBE ABOVE YOU IS NOT THE SAME AS NEW BLOCK");
                                    }

                                    if (maybeAboveYou.net == null)
                                    {
                                        //Instance.PrintError("FATAL ERROR: MAYBE ABOVE YOU HAS NULL NET");
                                        fatalErrorOccured = true;
                                    }
                                    else
                                    {
                                        //Instance.PrintError($"OK: MAYBE ABOVE YOU NET IS NOT NULL. NET ID = {maybeAboveYou.net.ID}");
                                    }

                                    //is the foundation above you the same shape as you?


                                    var expectedType = GetSpecificType(maybeAboveYou) == InternalEntityType.FoundationSquare ? InternalEntityType.FoundationSquareInversed : InternalEntityType.FoundationTriangleInversed;

                                    var yourActualType = GetSpecificType(newBuildingBlock);


                                    if (expectedType != yourActualType)
                                    {
                                        TellMessage(player, $"{MSG(MSG_EXPAND_REINFORCEMENT_WRONG_ABOVE, player.UserIDString)}", false, true);
                                        DelayedKillWithGibs(newBuildingBlock);
                                        return;
                                    }

                                    //do you have enough of needed kind?

                                    var neededItems = FindNeededFoundationItemsInInventory(player, newBuildingBlock);

                                    if (neededItems == null)
                                    {
                                        TellMessage(player, $"{MSG(MSG_DEPLOY_ERROR, player.UserIDString)} {MSG(MSG_EXPAND_REINFORCEMENT_LACKS_ITEM, player.UserIDString)}", false, true);
                                        DelayedKillWithGibs(newBuildingBlock);
                                        return;
                                    }

                                    //consume the item
                                    ConsumeFromSpecificItemList(ref neededItems);
                                }

                                if (fatalErrorOccured)
                                {
                                    //Instance.PrintError($"FATAL ERROR DETECTED. PLEASE COPY THE TEXT FROM THE CONSOLE AND GIVE IT TO NIKEDEMOS!");

                                    //TellMessage(player, "Fatal error detected. Attempting to NOT crash the server... success. At least there's that. Tell the  admin to check the server console.");
                                    if (newBuildingBlock != null)
                                    {
                                        DelayedKillWithGibs(newBuildingBlock);
                                    }
                                    
                                    return;
                                }


                                //Instance.PrintError($"NO ERRORS, ATTEMPTING TO TURN UPSIDE DOWN FLOOR INTO INVERSE FOUNDATION...");

                                TurnFloorIntoInverseFoundation(newBuildingBlock, maybeAboveYou, player);
                                return;

                            }


                        }
                        else
                        {
                            //give it generic skin - but still, inverse or not?
                            //for now, maybe let's leave it as generic?

                            if (Mathf.Abs(newBuildingBlock.transform.localEulerAngles.z) < 0.1F)
                            {
                                MarkAsInternalGenericDecayEntity(newBuildingBlock);
                            }
                            else
                            {
                                MarkAsInternalGenericDecayEntityInversed(newBuildingBlock);
                            }

                            //still a building block
                            ApplyUpkeepMultiplier(newBuildingBlock);
                        }
                    }

                }
                else
                {
                    //not a building block, so it must be a regular decay entity
                    if (IsInWaterBase(maybeDecayEntity))
                    {
                        var maybeStability = maybeDecayEntity as StabilityEntity;

                        if (maybeStability != null)
                        {
                            if (IsUnderwaterNetting(maybeStability, true))
                            {
                                if (Instance.configData.UnderwaterNetsCollectJunk)
                                {
                                    var prof = GetPermissionProfile(player);
                                    if (prof.CanDeployUnderwaterNets)
                                    {
                                        if (UnderwaterNetHelper.IsWaterDeepEnoughForNettings(maybeStability.transform.position))
                                        {
                                            MonoiseUnderwaterNetting(maybeStability);
                                        }
                                        else
                                        {
                                            TellMessage(player, $"{MSG(MSG_DEPLOY_ERROR, player.UserIDString)} {MSG(MSG_UNDERWATER_NETTING_TOO_SHALLOW, player.UserIDString, Instance.configData.UnderwaterNetsMinWaterDepth)}", false, true);

                                            DelayedKillWithGibs(maybeStability);
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        TellMessage(player, $"{MSG(MSG_DEPLOY_ERROR, player.UserIDString)} {MSG(MSG_UNDERWATER_NETTING_NO_PERM, player.UserIDString)}", false, true);
                                        DelayedKillWithGibs(maybeStability);
                                        return;
                                    }
                                }
                            }
                        }
                        ApplyUpkeepMultiplier(maybeDecayEntity);
                    }
                }
            }
            else //from inflatable
            {
                if (!IsSkinRelevant(maybeInflatable.skinID))
                {
                    return;
                }

                var player = planner.GetOwnerPlayer();

                Instance.NextTick(() =>
                {
                    var position = maybeInflatable.transform.position;

                    if (Instance.configData.AdjustDeployedWaterLevel)
                    {
                        var waterHeight = WaterLevel.GetWaterSurface(position, false, false);
                        position.y = waterHeight;
                    }

                    BuildWaterFoundationAt(player, true, maybeInflatable.skinID, position,
                        Quaternion.Euler(maybeInflatable.transform.eulerAngles.WithX(0).WithZ(0)));

                    maybeInflatable.Kill(BaseNetworkable.DestroyMode.None);
                });
            }
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (Instance == null) return;
            if (container == null) return;
            if (item == null) return;
            if (item.info.itemid != ITEM_INNER_TUBE) return;

            if (!IsSkinRelevant(item.skin)) return;

            EnsureFoundationItemName(item);
        }
        #endregion

        #region MONO

        public class NettingSharkHelper : MonoBehaviour
        {
            public float LastCheckAt = float.MinValue;
            public float CheckEvery = 1F;

            public bool SeemsToBeAsleepCurrent = false;
            public bool SeemsToBeAsleepPrevious = false;

            public float WentToSleepAt = float.MinValue;

            public SimpleShark OwnerShark;

            public bool NoNeedToUpdateAnymore = false;

            public void Prepare(SimpleShark shark)
            {
                OwnerShark = shark;

                if (!shark.HasFlag(FLAG_ALREADY_APPLIED))
                {
                    shark.SetHealth(Instance.configData.UnderwaterNetsSharkHealthMultiplier * shark.health);
                    shark.SetMaxHealth(Instance.configData.UnderwaterNetsSharkHealthMultiplier * shark.health);

                    shark.maxSpeed = shark.maxSpeed * Instance.configData.UnderwaterNetsSharkSpeedMultiplier;
                    shark.minSpeed = shark.minSpeed * Instance.configData.UnderwaterNetsSharkSpeedMultiplier;

                    shark.maxTurnSpeed = shark.maxTurnSpeed * Instance.configData.UnderwaterNetsSharkSpeedMultiplier;
                    shark.minTurnSpeed = shark.minTurnSpeed * Instance.configData.UnderwaterNetsSharkSpeedMultiplier;

                    shark.SetFlag(FLAG_ALREADY_APPLIED, true, false, true);
                    shark.SendChildrenNetworkUpdateImmediate();
                }

                //might aswell be reapplied
                shark.aggroRange = Instance.configData.UnderwaterNetsSharkAggroRange;
            }

            private void FixedUpdate()
            {
                if (NoNeedToUpdateAnymore)
                {
                    return;
                }

                if (Time.time < LastCheckAt + CheckEvery)
                {
                    return;
                }

                SeemsToBeAsleepPrevious = SeemsToBeAsleepCurrent;
                SeemsToBeAsleepCurrent = !BaseNetworkable.HasCloseConnections(transform.position, 100F);

                //did something change?
                if (SeemsToBeAsleepCurrent != SeemsToBeAsleepPrevious)
                {
                    //yes it did.
                    //did you just go to sleep fron non-sleep, or did you just wake up?
                    if (SeemsToBeAsleepCurrent)
                    {
                        //you just went to sleep
                        WentToSleepAt = Time.time;
                    }
                    else
                    {
                        //you just woke up
                        WentToSleepAt = float.MinValue;
                    }
                }
                else
                {
                    //nope. check.
                    if (SeemsToBeAsleepCurrent)
                    {
                        if (Time.time > WentToSleepAt + Instance.configData.UnderwaterNetsSharkSleepDespawnTimer)
                        {
                            //that's it for you. Kill yourself.
                            NoNeedToUpdateAnymore = true;
                            OwnerShark.Kill();
                        }
                    }
                }

            }
        }

        public class UnderwaterNetHelper : MonoBehaviour
        {
            //reapproacchi. keep TimerSetOn separately to SecondsAdded.

            public StabilityEntity OwnerNetting;

            public float NextCreateUnderwaterJunk;

            public float TimerSetWithTimestamp = float.MinValue;
            public float TimerSetWithSecondsAdded = 0;

            public bool TimerRunning = false;

            public void Prepare(StabilityEntity netting)
            {
                OwnerNetting = netting;

                //when the first one spawns, it will set off the timer.
                NextRandomTimeSpawnJunk();
            }

            public static bool IsWaterDeepEnoughForNettings(Vector3 position)
            {
                if (Instance.configData.UnderwaterNetsMinWaterDepth <= 0F)
                {
                    return true;
                }

                var depth = WaterLevel.GetOverallWaterDepth(position, false, false);

                if (depth < Instance.configData.UnderwaterNetsMinWaterDepth)
                {
                    return false;
                }

                return true;
            }

            public void SetupSharkTimer()
            {
                TimerSetWithTimestamp = Time.realtimeSinceStartup;
                TimerSetWithSecondsAdded = UnityEngine.Random.Range(Instance.configData.UnderwaterNetsSharkRandomTimerMin, Instance.configData.UnderwaterNetsRandomTimerMax);
                TimerRunning = true;
            }

            public void ShortenSharkTimer()
            {
                TimerRunning = true; //make sure the value is between 0 and 100
                TimerSetWithSecondsAdded *= (100F - Mathf.Clamp(Instance.configData.UnderwaterNetsSharkTimerShortenByPercent, 0F, 100F)) / 100F; //normalize back to 0 - 1 range
            }

            public void NextRandomTimeSpawnJunk()
            {
                float nextRandomTime = NextRandomTime(Instance.configData.UnderwaterNetsRandomTimerMin, Instance.configData.UnderwaterNetsRandomTimerMax);
                NextCreateUnderwaterJunk = nextRandomTime;
            }

            private void TryCreateRandomUnderwaterJunk()
            {
                var countBeforeAdding = CountNettingItems(OwnerNetting);

                if (countBeforeAdding >= Instance.configData.UnderwaterNetsItemLimit)
                {
                    //Instance.PrintWarning($"{countBeforeAdding} is too many alreaaaadyyyYYY, no timer action!!!! Making sure the timer is stopped.");
                    TimerRunning = false;
                    return;
                }

                var newItem = CreateRandomItemFromTable(Instance.configData.UnderwaterLoot, Instance.configData.UnderwaterLootWeightSum);

                if (newItem == null)
                {
                    return;
                }

                //figure out where to drop...
                var transf = GetWorldTransformRelativeTo(OwnerNetting.transform, new Vector3(UnityEngine.Random.Range(-0.2F, 0.2F), UnityEngine.Random.Range(0.1F, 2.9F), UnityEngine.Random.Range(-1.4F, 1.4F)), new Vector3(UnityEngine.Random.Range(0, 360F), UnityEngine.Random.Range(0, 360F), UnityEngine.Random.Range(0, 360F)));

                var dropped = newItem.Drop(transf.Item1, Vector3.zero, transf.Item2) as DroppedItem;

                var rigid = dropped.gameObject.GetComponent<Rigidbody>();
                rigid.useGravity = false;
                rigid.isKinematic = true;

                //parent...
                dropped.SetParent(OwnerNetting, true, false);

                if (countBeforeAdding == 0)
                {
                    SetupSharkTimer();
                }
                else
                {
                    ShortenSharkTimer();
                }
            }

            private void FixedUpdate()
            {
                //which one is happening sooner?
                if (Time.realtimeSinceStartup > NextCreateUnderwaterJunk)
                {
                    NextRandomTimeSpawnJunk();
                    TryCreateRandomUnderwaterJunk(); //if succeeded, this will decrease the timer (if not running already)
                }

                if (!Instance.configData.UnderwaterNetsSpawnSharks)
                {
                    return;
                }

                if (!TimerRunning)
                {
                    return;
                }

                if (Time.realtimeSinceStartup > TimerSetWithTimestamp + TimerSetWithSecondsAdded)
                {
                    //well, it's time. to spawn sum sharkzors.
                    TrySpawnSharkNearNetting(OwnerNetting);

                    //next item will trigger it back to true - but why?
                    TimerRunning = false;
                }

            }

            public static float NextRandomTime(float timeMin, float timeMax)
            {
                return UnityEngine.Time.realtimeSinceStartup + UnityEngine.Random.Range(timeMin, timeMax);
            }
        }

        public class InverseFoundationHelper : MonoBehaviour
        {
            public BuildingBlock inverseFoundation;
            public BuildingBlock waterFoundationParent;

            public static float UPDATE_RATE_MIN = 3F;
            public static float UPDATE_RATE_MAX = 5F;

            public float UpdateRateCurrent = 3F;
            public float UpdatedLast = float.MinValue;

            public BuildingManager.Building myBuilding;
            public BuildingManager.Building otherBuilding;

            public void Prepare(BuildingBlock inverseFoundation)
            {
                UpdateRateCurrent = RandomUpdateRate();
                this.inverseFoundation = inverseFoundation;
                this.waterFoundationParent = inverseFoundation.GetParentEntity() as BuildingBlock;
            }

            private void FixedUpdate()
            {
                if (UnityEngine.Time.realtimeSinceStartup < UpdatedLast + UpdateRateCurrent)
                {
                    return;
                }

                UpdatedLast = UnityEngine.Time.realtimeSinceStartup;


                if (inverseFoundation == null) return;
                if (waterFoundationParent == null) return;

                if (inverseFoundation.buildingID != waterFoundationParent.buildingID)
                {
                    myBuilding = inverseFoundation.GetBuilding();

                    if (myBuilding == null) return;

                    otherBuilding = waterFoundationParent.GetBuilding();

                    if (otherBuilding == null) return;

                    while (myBuilding.HasDecayEntities())
                    {
                        myBuilding.decayEntities[0].AttachToBuilding(otherBuilding.ID);
                    }

                    if (ConVar.AI.nav_carve_use_building_optimization)
                    {
                        myBuilding.isNavMeshCarvingDirty = true;
                        otherBuilding.isNavMeshCarvingDirty = true;
                        int ticks = 3;
                        BuildingManager.server.UpdateNavMeshCarver(myBuilding, ref ticks, 0);
                        BuildingManager.server.UpdateNavMeshCarver(otherBuilding, ref ticks, 0);
                    }
                    UpdateRateCurrent = RandomUpdateRate();
                }
            }

            public static float RandomUpdateRate()
            {
                return UnityEngine.Random.Range(UPDATE_RATE_MIN, UPDATE_RATE_MAX);
            }
        }
        #endregion

        #region GUI
        [ConsoleCommand("wb_craft.square")]
        private void cmdConsoleWBCraftSquare(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            if (player == null)
            {
                return;
            }

            TryCraftingWaterFoundation(player, ITEM_SKIN_FOUNDATION_SQUARE);
        }
        [ConsoleCommand("wb_craft.triangle")]
        private void cmdConsoleWBCraftTriangle(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            if (player == null)
            {
                return;
            }

            TryCraftingWaterFoundation(player, ITEM_SKIN_FOUNDATION_TRIANGLE);
        }

        public class ColorCode
        {
            public string hexValue;
            public UnityEngine.Color rustValue;
            public string rustString;
            public ColorCode(string hex)
            {
                hex = hex.ToUpper();

                hexValue = "#" + hex;

                //extract the R, G, B
                var r = (float)short.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255;
                var g = (float)short.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255;
                var b = (float)short.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255;

                rustValue = new UnityEngine.Color(r, g, b);
                rustString = $"{r} {g} {b}";
            }
        }


        public const float ANCHOR_X = 0.5F;
        public const float ANCHOR_Y = 0F;

        public const float SCREEN_WIDTH_IN_PIXELS = 1280;
        public const float SCREEN_HEIGHT_IN_PIXELS = 720;

        public static string ANCHOR_DEFAULT = ANCHOR_X.ToString() + " " + ANCHOR_Y.ToString();

        public const float ANCHOR_DEFAULT_PIXEL_X = ANCHOR_X * SCREEN_WIDTH_IN_PIXELS;
        public const float ANCHOR_DEFAULT_PIXEL_Y = 0;

        public static CuiRectTransformComponent GetCuiRectTransformFromNewGuiConfig()
        {
            return new CuiRectTransformComponent
            {
                AnchorMin = $"{Instance.configData.GuiNEW1AnchorMinX} {Instance.configData.GuiNEW2AnchorMinY}",
                AnchorMax = $"{Instance.configData.GuiNEW3AnchorMaxX} {Instance.configData.GuiNEW4AnchorMaxY}",

                OffsetMin = $"{Instance.configData.GuiNEW5OffsetMinX} {Instance.configData.GuiNEW6OffsetMinY}",
                OffsetMax = $"{Instance.configData.GuiNEW7OffsetMaxX} {Instance.configData.GuiNEW8OffsetMaxY}",
            };
        }
            

        public static CuiRectTransformComponent GetAnchorFromScreenBoxOld(float leftmostX, float bottommostY, float rightmostX, float topmostY, out float calculatedOffsetMinX, out float calculatedOffsetMinY, out float calculatedOffsetMaxX, out float calculatedOffsetMaxY)
        {

            string alignedTo = ANCHOR_DEFAULT;

            leftmostX *= SCREEN_WIDTH_IN_PIXELS;
            bottommostY *= SCREEN_HEIGHT_IN_PIXELS;

            rightmostX *= SCREEN_WIDTH_IN_PIXELS;
            topmostY *= SCREEN_HEIGHT_IN_PIXELS;

            leftmostX = (leftmostX - ANCHOR_DEFAULT_PIXEL_X);
            bottommostY = (bottommostY - ANCHOR_DEFAULT_PIXEL_Y);

            rightmostX = (rightmostX - ANCHOR_DEFAULT_PIXEL_X);
            topmostY = (topmostY - ANCHOR_DEFAULT_PIXEL_Y);

            calculatedOffsetMinX = Mathf.Ceil(leftmostX) + 1; //correction, don't ask me why
            calculatedOffsetMinY = Mathf.Ceil(bottommostY) -2; 

            calculatedOffsetMaxX = Mathf.Ceil(rightmostX);
            calculatedOffsetMaxY = Mathf.Ceil(topmostY);

            var result = new CuiRectTransformComponent
            {
                AnchorMin = alignedTo,
                AnchorMax = alignedTo,
                OffsetMin = $"{calculatedOffsetMinX} {calculatedOffsetMinY}",
                OffsetMax = $"{calculatedOffsetMaxX} {calculatedOffsetMaxY}",
            };

            return result;
        }

        public static class GuiManager
        {
            public static CuiElementContainer ContainerMain;
            public static string ContainerMainJSON;
            public static CuiRectTransformComponent Anchor;

            public static void ReGenerateGUI()
            {
                //move this to legacy gen
                ContainerMain = new CuiElementContainer();

                float rubbish1, rubbish2, rubbish3, rubbish4;

                Anchor = Instance.configData.GuiUseNewPositioning ? GetCuiRectTransformFromNewGuiConfig() : GetAnchorFromScreenBoxOld(Instance.configData.GuiAnchorALeft, Instance.configData.GuiAnchorBBottom, Instance.configData.GuiAnchorCRight, Instance.configData.GuiAnchorDTop, out rubbish1, out rubbish2, out rubbish3, out rubbish4);

                ContainerMain.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0 0 0 0",
                    },
                    RectTransform =
                    {
                        AnchorMin = Anchor.AnchorMin,
                        AnchorMax = Anchor.AnchorMax,
                        OffsetMin = Anchor.OffsetMin,
                        OffsetMax = Anchor.OffsetMax
                    }
                }, "Overlay", "wbgui.panel");

                ContainerMain.Add(new CuiButton
                {
                    Button =
                    {
                        Color = $"{new ColorCode(Instance.configData.GuiButtonColor).rustString} {Instance.configData.GuiButtonAlpha}",
                        Command = "wb_craft.square",
                    },
                    Text =
                    {
                        FontSize = Instance.configData.GuiTextSize,
                        Color = $"{new ColorCode(Instance.configData.GuiTextColor).rustString} {Instance.configData.GuiTextAlpha}",
                        Align = TextAnchor.MiddleCenter,
                        Text = $"{MSG(MSG_CRAFT)} {MSG(MSG_ITEM_NAME_SQUARE)}",
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.5"
                    }

                }, "wbgui.panel", "wbgui.button.craft.square");

                ContainerMain.Add(new CuiButton
                {
                    Button =
                    {
                        Color = $"{new ColorCode(Instance.configData.GuiButtonColor).rustString} {Instance.configData.GuiButtonAlpha}",
                        Command = "wb_craft.triangle",
                    },
                    Text =
                    {
                        FontSize = Instance.configData.GuiTextSize,
                        Color = $"{new ColorCode(Instance.configData.GuiTextColor).rustString} {Instance.configData.GuiTextAlpha}",
                        Align = TextAnchor.MiddleCenter,
                        Text = $"{MSG(MSG_CRAFT)} {MSG(MSG_ITEM_NAME_TRIANGLE)}"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0.5",
                        AnchorMax = "1 1"
                    }

                }, "wbgui.panel", "wbgui.button.craft.triangle");

                ContainerMainJSON = ContainerMain.ToJson();
            }

            public static void CleanupGUI()
            {
                ContainerMain = null;
                ContainerMainJSON = null;
                Anchor = null;
            }

            public static void ShowGUI(BasePlayer player)
            {
                HideGUI(player);

                if (!GetPermissionProfile(player).CanCraftWaterFoundations)
                {
                    return;
                }

                CuiHelper.AddUi(player, ContainerMainJSON);
            }

            public static void HideGUI(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "wbgui.panel");
            }
        }
        #endregion

        #region CHAT & CONSOLE
        [ChatCommand("copypaste_prepare")]
        private void ChatCommandCopypastePrepare(BasePlayer player, string command, string[] args)
        {
            if (!HasAdminPermission(player)) return;

            var tc = player.GetBuildingPrivilege();

            if (tc == null)
            {
                player.ChatMessage("You're not near a TC.");
                return;
            }

            if (!IsInWaterBase(tc))
            {
                player.ChatMessage("This is not a Water Base.");
                return;
            }

            foreach (var ent in tc.GetBuilding().buildingBlocks)
            {
                if (!IsWaterFoundation(ent) && !IsInverseFoundation(ent))
                {
                    continue;
                }

                var bebes = ent.GetComponentsInChildren<CollectibleEntity>();

                foreach (var bebe in bebes)
                {
                    bebe.Kill(BaseNetworkable.DestroyMode.None);
                }

            }

            player.ChatMessage("Killed all barrels. Try copying now.");
        }

        [ChatCommand("wb_cfg")]
        private void ChatCommandWBCFG(BasePlayer player, string command, string[] args)
        {
            if (!HasAdminPermission(player)) return;

            if (args.Length == 0)
            {
                //display all possible config keys and their values
                ReusableString = $"{MSG(MSG_CFG_DEFAULT, player.UserIDString)}\n";
                foreach (var entry in ConfigValues)
                {
                    ReusableString += $"{MSG(MSG_CFG_RUNDOWN_FORMAT, player.UserIDString, entry.Key, entry.Value.formatter(entry.Value.GetSet))}\n";
                }

                TellMessage(player, ReusableString, true);
            }
            else
            {

                if (ConfigValues.ContainsKey(args[0]))
                {
                    //has argument[1] been provided? if not, display the full description.
                    if (args.Length > 1)
                    {
                        ConfigValues[args[0]].GetSet = args[1];
                    }
                    else
                    {
                        TellMessage(player, $"{MSG(MSG_CFG_DETAILS_FORMAT, player.UserIDString, ConfigValues[args[0]].name, ConfigValues[args[0]].description, ConfigValues[args[0]].valueType, ConfigValues[args[0]].formatter(ConfigValues[args[0]].GetSet))}. Accepted values are {ConfigValues[args[0]].FormatAcceptable()}", true);
                    }

                }
                else
                {
                    TellMessage(player, MSG(MSG_CFG_NO_SETTING_FOUND, player.UserIDString), true);
                }
            }
        }
        [ChatCommand("draw_cargo")]
        private void ChatCommandDrawCargo(BasePlayer player, string command, string[] args)
        {
            //if (!HasAdminPermission(player)) return;

            //stagger to avoid kicking

            float timer = 0.02F;

            foreach (var oceanNode in TerrainMeta.Path.OceanPatrolFar)
            {
                Instance.timer.Once(timer, () =>
                {
                    bool setAdmin = false;

                    if (!player.HasPlayerFlag(BasePlayer.PlayerFlags.IsAdmin))
                    {
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                        player.SendNetworkUpdateImmediate();
                        setAdmin = true;
                    }

                    player.SendConsoleCommand("ddraw.sphere", 15F, Color.red, oceanNode, Instance.configData.MinDistanceFromCargoShipNode);

                    if (setAdmin)
                    {
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                        player.SendNetworkUpdateImmediate();
                    }
                });

                timer += 0.02F;

            }
        }

        [ChatCommand("grid_pos")]
        private void ChatCommandGridPos(BasePlayer player, string command, string[] args)
        {
            if (!HasAdminPermission(player)) return;

            player.ChatMessage($"OUTSIDE MAP GRID: {IsPositionOutsideMapGrid(player.transform.position)}");
        }

        [ChatCommand("shore_distance")]
        private void ChatCommandShoreDistance(BasePlayer player, string command, string[] args)
        {
            if (!HasAdminPermission(player)) return;

            player.ChatMessage(GetCoarseDistanceToShore(player.transform.position).ToString());
        }

        [ChatCommand("give_square")]
        private void ChatCommandGiveSquare(BasePlayer player, string command, string[] args)
        {
            if (!HasAdminPermission(player)) return;

            int amount = 1;

            if (args.Length > 0)
            {
                if (!int.TryParse(args[0], out amount))
                {
                    amount = 1;
                }
            }

            if (amount<0)
            {
                amount = 1;
            }

            player.GiveItem(MakeFoundationItem(ITEM_SKIN_FOUNDATION_SQUARE, amount), BaseEntity.GiveItemReason.Generic);
        }

        [ChatCommand("give_triangle")]
        private void ChatCommandGiveTriangle(BasePlayer player, string command, string[] args)
        {
            if (!HasAdminPermission(player)) return;

            int amount = 1;

            if (args.Length > 0)
            {
                if (!int.TryParse(args[0], out amount))
                {
                    amount = 1;
                }
            }

            if (amount < 0)
            {
                amount = 1;
            }

            player.GiveItem(MakeFoundationItem(ITEM_SKIN_FOUNDATION_TRIANGLE, amount), BaseEntity.GiveItemReason.Generic);

        }

        private void CommandGiveFoundation(IPlayer iplayer, string command, string[] args)
        {
            if (!HasAdminPermission(iplayer)) return;

            if (args.Length < (iplayer.IsServer ? 2 : 1))
            {
                iplayer.Reply($"USAGE: {CMD_WB_GIVE} [square/triangle] [partial player name or full steam ID]. If a player executes it in the chat or console in-game and they don't specify a recipient, it will be given to the player executing this command. Executing from the server console requires specifying the player.\n");
                return;
            }

            string foundationShape = args[0];

            ulong foundationSkin = 0;

            switch (foundationShape)
            {
                case "square":
                    {
                        foundationSkin = ITEM_SKIN_FOUNDATION_SQUARE;
                    }
                    break;
                case "triangle":
                    {
                        foundationSkin = ITEM_SKIN_FOUNDATION_TRIANGLE;
                    }
                    break;
            }

            if (foundationSkin == 0)
            {
                iplayer.Reply("You need to specify a valid foundation shape: square or triangle");
                return;
            }

            BasePlayer playerToBequeef;

            if (args.Length > 1)
            {
                playerToBequeef = covalence.Players.FindPlayer(args[1])?.Object as BasePlayer ?? null;
            }
            else
            {
                playerToBequeef = iplayer.Object as BasePlayer;
            }

            if (playerToBequeef == null)
            {
                iplayer.Reply($"No player matching \"{args[1]}\" was found");
                return;
            }

            var newItem = MakeFoundationItem(foundationSkin, 1);

            playerToBequeef.GiveItem(newItem);

            iplayer.Reply($"{playerToBequeef.displayName} ({playerToBequeef.userID}) was given a {newItem.name}");

        }
        #endregion

        #region CONFIG
        public class InteractiveConfigValue
        {
            private Func<object> getter;
            private Action<object> setter;

            private Func<object, bool> validator;
            public Func<object, string> formatter;
            private Func<object, object> parser;

            public Type valueType;

            private double lowerLimit;
            private double upperLimit;

            //name also identifies it in the dic
            public string name;
            public string description;

            public InteractiveConfigValue(string name, string description, Func<object> getter, Action<object> setter, Type validatorType = null, double lowerLimit = double.NegativeInfinity, double upperLimit = double.PositiveInfinity)
            {
                this.getter = getter;
                this.setter = setter;
                this.name = name;
                this.description = description;
                this.lowerLimit = lowerLimit;
                this.upperLimit = upperLimit;

                formatter = FormatDefault;

                if (validatorType == null || validatorType == typeof(bool))
                {
                    validator = ValidateBool;
                    parser = ParseBool;
                    formatter = FormatBool;
                }
                else if (validatorType == typeof(float))
                {
                    validator = ValidateFloat;
                    parser = ParseFloat;
                }
                else if (validatorType == typeof(int))
                {
                    validator = ValidateInt;
                    parser = ParseInt;
                }
                else if (validatorType == typeof(ulong))
                {
                    validator = ValidateUlong;
                    parser = ParseUlong;
                }
                else if (validatorType == typeof(string))
                {
                    validator = ValidateString;
                    parser = ParseString;
                    formatter = FormatString;
                }
                else if (validatorType == typeof(CargoPathHandling))
                {
                    validator = ValidateEnumCargo;
                    parser = ParseEnumCargo;
                    formatter = FormatEnumCargo;
                }

                valueType = validatorType;
            }

            public object GetSet
            {
                get
                {
                    return getter();
                }
                set
                {
                    //validate first
                    if (validator(value))
                    {
                        ReusableObject = parser(value);
                        setter(ReusableObject);
                        //tell the players

                        TellMessage(null, MSG(MSG_VALUE_HAS_BEEN_SET, null, name, formatter(ReusableObject)), true);
                        Instance.SaveConfigData();

                    }
                    else
                    {
                        ReusableString = upperLimit != double.PositiveInfinity || lowerLimit != double.NegativeInfinity ? $"The value for {name} is either too low or too high. Try {FormatAcceptable()}" : $"Incorrect value for {name}. You need to enter {FormatAcceptable()}";

                        TellMessage(null, $"{ReusableString} \nThe value remains as {formatter(getter())}.", true);
                    }

                }
            }

            public string FormatAcceptable()
            {
                return valueType == typeof(CargoPathHandling) ? "Warn, Prevent or None" : valueType == typeof(bool) ? "logical values (<color=green>true</color> or <color=red>false</color>)" : valueType == typeof(string) ? FormatStrings() : FormatNumericLimits();
            }

            private string FormatNumericLimits()
            {
                ReusableString2 = valueType == typeof(float) ? "<color=green>fractions (like 1.2345)</color>" : "<color=purple>integers (like 12345)</color>";

                return $"{ReusableString2} between <color=red>{lowerLimit.ToString("0.00")}</color> and <color=blue>{upperLimit.ToString("0.00")}";
            }

            private string FormatStrings()
            {
                return "hexadecimal numbers WITHOUT preceeding # (like 3db4b3)";
                //return "strings (like ThisIsAString) - if they have a space, in quotes (like \"This Is A String\")";
            }

            private string FormatString(object value)
            {
                return $"<color=yellow>{value?.ToString() ?? "NULL"}</color>";
            }

            private string FormatEnumCargo(object value)
            {
                return $"<color=red>{value.ToString() ?? "NULL"}</color>";
            }

            private string FormatBool(object value)
            {
                return value.Equals(true) ? "<color=green>true</color>" : "<color=red>false</color>";
            }

            private object ParseBool(object value)
            {
                ReusableString2 = value.ToString().ToLower();

                ReusableBool2 = !(ReusableString2.Contains("f") || ReusableString2.Contains("0") || ReusableString2.Contains("no"));

                return ReusableBool2;
            }

            private object ParseEnumCargo(object value)
            {
                ReusableString2 = value.ToString().ToLower();

                if (ReusableString2.Contains("none"))
                {
                    return CargoPathHandling.None;
                }

                if (ReusableString2.Contains("warn"))
                {
                    return CargoPathHandling.Warn;
                }

                if (ReusableString2.Contains("prevent"))
                {
                    return CargoPathHandling.Prevent;
                }

                return null;
            }
            private bool ValidateBool(object value)
            {
                //whatever it is, it can be always treated as bool
                return true;
            }

            private object ParseFloat(object value)
            {
                //it's already in ReusableFloat! neat, huh.
                //since we're using it, it must've been validated, so it's still there eh.
                return ReusableFloat2;
            }

            private object ParseString(object value)
            {
                return ReusableString;
            }

            private object ParseInt(object value)
            {
                return ReusableInt2;
            }

            private object ParseUlong(object value)
            {
                return ReusableUlong2;
            }

            private bool ValidateFloat(object value)
            {
                if (float.TryParse(value.ToString(), out ReusableFloat2))
                {
                    return (ReusableFloat2 >= lowerLimit && ReusableFloat2 <= upperLimit);
                }
                else return false;
            }

            private bool ValidateInt(object value)
            {
                if (int.TryParse(value.ToString(), out ReusableInt2))
                {
                    return (ReusableInt2 >= lowerLimit && ReusableInt2 <= upperLimit);
                }
                else return false;
            }

            private bool ValidateUlong(object value)
            {
                if (ulong.TryParse(value.ToString(), out ReusableUlong2))
                {
                    return (ReusableUlong2 >= lowerLimit && ReusableUlong2 <= upperLimit);
                }
                else return false;
            }

            private bool ValidateString(object value)
            {
                ReusableString = value.ToString();

                if (ReusableString.ToLower() == "null")
                {
                    ReusableString = null;
                }
                return true;
            }

            private bool ValidateEnumCargo(object value)
            {
                ReusableString = value.ToString().ToLower();

                if (ReusableString == "" || ReusableString == string.Empty)
                {
                    return false;
                }

                if (!(ReusableString.Contains("warn") || ReusableString.Contains("prevent") || ReusableString.Contains("none")))
                {
                    return false;
                }

                return true;

            }

            private string FormatDefault(object value)
            {
                return $"<color=#00FFFF>{value.ToString()}</color>";
            }

        }

        private static Dictionary<string, InteractiveConfigValue> ConfigValues = null;

        private static void AddInteractiveConfigValue(string name, string description, Func<object> getter, Action<object> setter, Type validator = null, double lowerLimit = double.NegativeInfinity, double upperLimit = double.PositiveInfinity)
        {
            ConfigValues.Add(name, new InteractiveConfigValue(name, description, getter, setter, validator, lowerLimit, upperLimit));
        }


        private static void GenerateAllInteractiveConfigValues()
        {
            AddInteractiveConfigValue("RelyOnShoreDistance", $"If true, shore distance will be checked when players try to place water foundation. Maps, especially custom, might not have topology maps defined correctly - if you see deployment errors that don't make sense according to your config, disable this check and rely on water depth instead (the farther from shore, the deeper).", () => Instance.configData.RelyOnShoreDistance, val => { Instance.configData.RelyOnShoreDistance = val.Equals(true); }, typeof(bool));

            //new in 1.0.7
            AddInteractiveConfigValue("PlayerMessagesAsToasts", $"If true, all messages directed at players will display as Rust-native Toasts (briefly flashing messages at the bottom of the screen). If false, those messages will go in the chat like they have been before 1.0.7", () => Instance.configData.PlayerMessagesAsToasts, val => { Instance.configData.PlayerMessagesAsToasts = val.Equals(true); }, typeof(bool));

            //new in 1.0.8
            AddInteractiveConfigValue("GlobalOceanLevelCheckBeforePlacing", $"If true, players won't be able to place new Water Foundations if the ocean level is currently above or below the GlobalOceanLevelBaseline value", () => Instance.configData.GlobalOceanLevelCheckBeforePlacing, val => { Instance.configData.GlobalOceanLevelCheckBeforePlacing = val.Equals(true); }, typeof(bool));
            AddInteractiveConfigValue("GlobalOceanLevelBaseline", $"Set this value to whatever oceanlevel convar your server normally runs at. Typically it's 0. This value is used in conjunction with GlobalOceanLevelCheckBeforePlacing.", () => Instance.configData.GlobalOceanLevelBaseline, val => { Instance.configData.GlobalOceanLevelBaseline = (float)val; }, typeof(float), -1000F, 1000F);


            AddInteractiveConfigValue("CargoShipPathHandling", $"If a player tries building/deploying a Water Foundation too close to the path of the Cargo Ship, they will get, respectively, a warning (but the foundation will stay there anyway), they will be prevented from doing so, or nothing will happen.", () => Instance.configData.CargoShipPathHandling, val => { Instance.configData.CargoShipPathHandling = (CargoPathHandling)val; }, typeof(CargoPathHandling));

            AddInteractiveConfigValue("MinDistanceFromCargoShipNode", $"The lower this value (in meters), the closer to Cargo Ship paths players will be able to build water foundations (they will just get a warning, won't be able to build, or nothing will happen - see CargoShipPathHandling).", () => Instance.configData.MinDistanceFromCargoShipNode, val => { Instance.configData.MinDistanceFromCargoShipNode = (float)val; }, typeof(float), 0F, 10000F);

            AddInteractiveConfigValue("RestrictBuildingToMapGrid", $"If true, players will only be able to deploy water foundations within the boundaries of the named squares of the map grid", () => Instance.configData.RestrictBuildingToMapGrid, val => { Instance.configData.RestrictBuildingToMapGrid = val.Equals(true); }, typeof(bool));

            AddInteractiveConfigValue("UnderwaterNetsCollectJunk", $"If true, Nettings deployed in wall frames underwater will accumulate random low-tier items. The full loot table (items and their weights) can be edited in the config file.", () => Instance.configData.UnderwaterNetsCollectJunk, val => { Instance.configData.UnderwaterNetsCollectJunk = val.Equals(true); }, typeof(bool));

            AddInteractiveConfigValue("UnderwaterNetsRandomTimerMin", $"Minimum duration of time before the next random item spawns in a Netting (in seconds)", () => Instance.configData.UnderwaterNetsRandomTimerMin, val => { Instance.configData.UnderwaterNetsRandomTimerMin = (float)val; }, typeof(float), 1F, 100000F);

            AddInteractiveConfigValue("UnderwaterNetsRandomTimerMax", $"Maximum duration of time before the next random item spawns in a Netting (in seconds)", () => Instance.configData.UnderwaterNetsRandomTimerMax, val => { Instance.configData.UnderwaterNetsRandomTimerMax = (float)val; }, typeof(float), 1F, 100000F);

            AddInteractiveConfigValue("UnderwaterNetsItemLimit", $"If the amount of items currently caught in the Netting is larger than this number, don't catch new items until room is made (or one of the items despawns)", () => Instance.configData.UnderwaterNetsItemLimit, val => { Instance.configData.UnderwaterNetsItemLimit = (int)val; }, typeof(int), 0, 100);


            //new in 1.0.5: sharks.

            AddInteractiveConfigValue("UnderwaterNetsSpawnSharks", $"If true, Nettings deployed in wall frames underwater will spawn Sharks nearby. The full loot table (items and their weights) can be edited in the config file.", () => Instance.configData.UnderwaterNetsSpawnSharks, val => { Instance.configData.UnderwaterNetsSpawnSharks = val.Equals(true); }, typeof(bool));

            AddInteractiveConfigValue("UnderwaterNetsSharkRandomTimerMin", $"When the first item in the netting is caught, minimum duration of time before the next random Shark has a chance of spawning near a Netting (in seconds)", () => Instance.configData.UnderwaterNetsSharkRandomTimerMin, val => { Instance.configData.UnderwaterNetsSharkRandomTimerMin = (float)val; }, typeof(float), 1F, 100000F);

            AddInteractiveConfigValue("UnderwaterNetsSharkRandomTimerMax", $"When the first item in the netting is caught, maximum duration of time before the next random Shark has a chance of spawning near a Netting (in seconds)", () => Instance.configData.UnderwaterNetsSharkRandomTimerMax, val => { Instance.configData.UnderwaterNetsSharkRandomTimerMax = (float)val; }, typeof(float), 1F, 100000F);

            AddInteractiveConfigValue("UnderwaterNetsSharkTimerShortenByPercent", $"If this value is greater than 0%, every time an additional item is caught, the currently running timer to spawn the Shark gets shortened by this percentage.", () => Instance.configData.UnderwaterNetsSharkTimerShortenByPercent, val => { Instance.configData.UnderwaterNetsSharkTimerShortenByPercent = (float)val; }, typeof(float), 0F, 100F);

            AddInteractiveConfigValue("UnderwaterNetsSharkSpawningMinDistance", $"Minimum distance from the Netting for a random Shark position relative to the Netting it spawns from. Sharks have big colliders, so make sure you set this value to at least 2 meters.", () => Instance.configData.UnderwaterNetsSharkSpawningMinDistance, val => { Instance.configData.UnderwaterNetsSharkSpawningMinDistance = (float)val; }, typeof(float), 2F, 100000F);

            AddInteractiveConfigValue("UnderwaterNetsSharkSpawningMaxDistance", $"Maximum distance from the Netting for a random Shark position relative to the Netting it spawns from. Over 100 meters, Rust makes Shark automatically go to sleep if no nearby players are found, so don't set this over 100 meters.", () => Instance.configData.UnderwaterNetsSharkSpawningMaxDistance, val => { Instance.configData.UnderwaterNetsSharkSpawningMaxDistance = (float)val; }, typeof(float), 5F, 100F);

            AddInteractiveConfigValue("UnderwaterNetsSharkPopulationLimitLocal", $"Useful for limiting how many Sharks max can spawn around a given Netting (in a radius of 100 meters from that Netting). Many Nettings close together share the same space and thus the same limit.", () => Instance.configData.UnderwaterNetsSharkPopulationLimitLocal, val => { Instance.configData.UnderwaterNetsSharkPopulationLimitLocal = (int)val; }, typeof(int), 1, 1000);

            AddInteractiveConfigValue("UnderwaterNetsSharkPopulationLimitGlobal", $"No more Sharks will spawn on the server AT ALL (around the nettings or in the wild) if this limit had been exceeded. Useful for balancing server performance.", () => Instance.configData.UnderwaterNetsSharkPopulationLimitGlobal, val => { Instance.configData.UnderwaterNetsSharkPopulationLimitGlobal = (int)val; }, typeof(int), 1, 1000);

            AddInteractiveConfigValue("UnderwaterNetsSharkSleepDespawnTimer", $"When no players are found within a 100 radius meters of a shark, a timer starts with this value. When it wakes up, the timer is cancelled. Adjust it to let sleeping sharks live longer.", () => Instance.configData.UnderwaterNetsSharkSleepDespawnTimer, val => { Instance.configData.UnderwaterNetsSharkSleepDespawnTimer = (float)val; }, typeof(float), 0F, 100000F);

            AddInteractiveConfigValue("UnderwaterNetsSharkHealthMultiplier", $"Use values lower than 1 to give them less health, and higher than 1 to give them more health than they'd normally have as vanilla Sharks", () => Instance.configData.UnderwaterNetsSharkHealthMultiplier, val => { Instance.configData.UnderwaterNetsSharkHealthMultiplier = (float)val; }, typeof(float), 0.001F, 100F);

            AddInteractiveConfigValue("UnderwaterNetsSharkSpeedMultiplier", $"Use values lower than 1 to give them less speed, and higher than 1 to give them more speed than they'd normally have as vanilla Sharks", () => Instance.configData.UnderwaterNetsSharkSpeedMultiplier, val => { Instance.configData.UnderwaterNetsSharkSpeedMultiplier = (float)val; }, typeof(float), 0.001F, 100F);

            AddInteractiveConfigValue("UnderwaterNetsSharkAggroRange", $"When a player underwater gets closer (in meters) to a Shark than this value, the Shark might pursue them. Default Rust vanilla is 15", () => Instance.configData.UnderwaterNetsSharkAggroRange, val => { Instance.configData.UnderwaterNetsSharkAggroRange = (float)val; }, typeof(float), 0.001F, 100F);

            AddInteractiveConfigValue("SharkHarvestingLootEnabled", $"If true, harvesting a Shark corpse with a proper tool will have a chance of giving random low-tier items. The full loot table (items and their weights) can be edited in the config file.", () => Instance.configData.SharkHarvestingLootEnabled, val => { Instance.configData.SharkHarvestingLootEnabled = val.Equals(true); }, typeof(bool));

            //DEPRECATED
            /*
            AddInteractiveConfigValue("GuiAnchorALeft", $"Building plan crafting GUI anchor min x (left)", () => Instance.configData.GuiAnchorALeft, val => { Instance.configData.GuiAnchorALeft = (float)val; GuiManager.ReGenerateGUI(); }, typeof(float), 0F, 1F);

            AddInteractiveConfigValue("GuiAnchorBBottom", $"Building plan crafting GUI anchor min y (bottom)", () => Instance.configData.GuiAnchorBBottom, val => { Instance.configData.GuiAnchorBBottom = (float)val; GuiManager.ReGenerateGUI(); }, typeof(float), 0F, 1F);

            AddInteractiveConfigValue("GuiAnchorCRight", $"Building plan crafting GUI anchor max x (right)", () => Instance.configData.GuiAnchorCRight, val => { Instance.configData.GuiAnchorCRight = (float)val; GuiManager.ReGenerateGUI(); }, typeof(float), 0F, 1F);

            AddInteractiveConfigValue("GuiAnchorDTop", $"Building plan crafting GUI anchor max y (top)", () => Instance.configData.GuiAnchorDTop, val => { Instance.configData.GuiAnchorDTop = (float)val; GuiManager.ReGenerateGUI(); }, typeof(float), 0F, 1F);
            */

            //replaced with...
            AddInteractiveConfigValue("GuiNEW1AnchorMinX", $"Building plan crafting GUI anchor min x (left)", () => Instance.configData.GuiNEW1AnchorMinX, val => { Instance.configData.GuiNEW1AnchorMinX = (float)val; GuiManager.ReGenerateGUI(); }, typeof(float), 0F, 1F);

            AddInteractiveConfigValue("GuiNEW2AnchorMinY", $"Building plan crafting GUI anchor min y (bottom)", () => Instance.configData.GuiNEW2AnchorMinY, val => { Instance.configData.GuiNEW2AnchorMinY = (float)val; GuiManager.ReGenerateGUI(); }, typeof(float), 0F, 1F);

            AddInteractiveConfigValue("GuiNEW3AnchorMaxX", $"Building plan crafting GUI anchor max x (right)", () => Instance.configData.GuiNEW3AnchorMaxX, val => { Instance.configData.GuiNEW3AnchorMaxX = (float)val; GuiManager.ReGenerateGUI(); }, typeof(float), 0F, 1F);

            AddInteractiveConfigValue("GuiNEW4AnchorMaxY", $"Building plan crafting GUI anchor max y (top)", () => Instance.configData.GuiNEW4AnchorMaxY, val => { Instance.configData.GuiNEW4AnchorMaxY = (float)val; GuiManager.ReGenerateGUI(); }, typeof(float), 0F, 1F);

            AddInteractiveConfigValue("GuiNEW5OffsetMinX", $"Building plan crafting GUI offset from anchor min x (left)", () => Instance.configData.GuiNEW5OffsetMinX, val => { Instance.configData.GuiNEW5OffsetMinX = (float)val; GuiManager.ReGenerateGUI(); }, typeof(float), 0F, 1F);

            AddInteractiveConfigValue("GuiNEW6OffsetMinY", $"Building plan crafting GUI offset from anchor min y (bottom)", () => Instance.configData.GuiNEW6OffsetMinY, val => { Instance.configData.GuiNEW6OffsetMinY = (float)val; GuiManager.ReGenerateGUI(); }, typeof(float), 0F, 1F);

            AddInteractiveConfigValue("GuiNEW7OffsetMaxX", $"Building plan crafting GUI offset from anchor max x (right)", () => Instance.configData.GuiNEW7OffsetMaxX, val => { Instance.configData.GuiNEW7OffsetMaxX = (float)val; GuiManager.ReGenerateGUI(); }, typeof(float), 0F, 1F);

            AddInteractiveConfigValue("GuiNEW8OffsetMaxY", $"Building plan crafting GUI offset from anchor max y (top)", () => Instance.configData.GuiNEW8OffsetMaxY, val => { Instance.configData.GuiNEW8OffsetMaxY = (float)val; GuiManager.ReGenerateGUI(); }, typeof(float), 0F, 1F);

            AddInteractiveConfigValue("GuiTextSize", $"Building plan crafting GUI text size", () => Instance.configData.GuiTextSize, val => { Instance.configData.GuiTextSize = (int)val; GuiManager.ReGenerateGUI(); }, typeof(int), 4, 72);

            AddInteractiveConfigValue("GuiButtonColor", $"Building plan crafting GUI button background colour (hex number, no preceeding hash)", () => Instance.configData.GuiButtonColor, val => { Instance.configData.GuiButtonColor = (string)val; GuiManager.ReGenerateGUI(); }, typeof(string));

            AddInteractiveConfigValue("GuiTextColor", $"Building plan crafting GUI text colour (hex number, no preceeding hash)", () => Instance.configData.GuiTextColor, val => { Instance.configData.GuiTextColor = (string)val; GuiManager.ReGenerateGUI(); }, typeof(string));

            AddInteractiveConfigValue("GuiButtonAlpha", $"Building plan crafting GUI button alpha (0 = fully transparent, 1 = fully opaque)", () => Instance.configData.GuiButtonAlpha, val => { Instance.configData.GuiButtonAlpha = (float)val; GuiManager.ReGenerateGUI(); }, typeof(float), 0F, 1F);

            AddInteractiveConfigValue("GuiTextAlpha", $"Building plan crafting GUI text alpha (0 = fully transparent, 1 = fully opaque)", () => Instance.configData.GuiTextAlpha, val => { Instance.configData.GuiTextAlpha = (float)val; GuiManager.ReGenerateGUI(); }, typeof(float), 0F, 1F);

            AddInteractiveConfigValue("EnableBarrelEntities", $"If true, water foundation and reinforcements will spawn barrels for visuals. If you're worried about potential server lag with extra entities, set to false, but it will fully expose your foundation/reinforcement soft sides. Changes will take effect for newly deployed foundations or after server restart.", () => Instance.configData.EnableBarrelEntities, val => { Instance.configData.EnableBarrelEntities = val.Equals(true); }, typeof(bool));

            //new in 1.0.7
            AddInteractiveConfigValue("UnderwaterNetsMinWaterDepth", $"The minimum water depth to have functional nettings that collect items and attract sharks.", () => Instance.configData.UnderwaterNetsMinWaterDepth, val => { Instance.configData.UnderwaterNetsMinWaterDepth = (float)val; }, typeof(float), 0F, 500F);


            //new in 1.0.14
            AddInteractiveConfigValue("AdjustDeployedWaterLevel", $"If true, the when deployed from an inflatable, the starter foundation will auto-adjust itself to be at the water level (classic behaviour). If false, it will stay at the depth you placed it at.", () => Instance.configData.AdjustDeployedWaterLevel, val => { Instance.configData.AdjustDeployedWaterLevel = val.Equals(true); }, typeof(bool));

            //new in 1.0.15
            AddInteractiveConfigValue("MapGridBoundsTolerance", $"If RestricBuildingToMapGrid is enabled, this setting lets you set the padding around the border where building will be permitted (in meters). Useful when you want to limit to a bit more than just the map grid. ", () => Instance.configData.MapGridBoundsTolerance, val => { Instance.configData.MapGridBoundsTolerance = (float)val; }, typeof(float), 0F, 10000F);

        }
        public enum DeploymentResult
        {
            Success,
            BuildingBlocked,
            DeployedInPool,

            TooManyFoundations,
            TooCloseToOilrig,

            NoPermissionToDeploy,
            NoPermissionToExpand,

            TopologyMismatch,

            WaterTooShallow,
            WaterTooDeep,

            TooFarFromShore,
            TooCloseToShore,

            OnCargoShipPath,
            NoRequiredItem,

            OutsideMapGrid,

            ProblemExistsBetweenChairAndKeyboard,

            OceanLevel
        }

        public enum CargoPathHandling
        {
            None,
            Warn,
            Prevent
        }

        public enum Topology
        {
            Field = 1,
            Cliff = 2,
            Summit = 4,
            Beachside = 8,
            Beach = 0x10,
            Forest = 0x20,
            Forestside = 0x40,
            Ocean = 0x80,
            Oceanside = 0x100,
            Decor = 0x200,
            Monument = 0x400,
            Road = 0x800,
            Roadside = 0x1000,
            Swamp = 0x2000,
            River = 0x4000,
            Riverside = 0x8000,
            Lake = 0x10000,
            Lakeside = 0x20000,
            Offshore = 0x40000,
            Powerline = 0x80000,
            Plain = 0x100000,
            Building = 0x200000,
            Cliffside = 0x400000,
            Mountain = 0x800000,
            Clutter = 0x1000000,
            Alt = 0x2000000,
            Tier0 = 0x4000000,
            Tier1 = 0x8000000,
            Tier2 = 0x10000000,
            Mainland = 0x20000000,
            Hilltop = 0x40000000
        }

        private class SerializableItemAmount
        {
            public string Shortname = null;
            public int Amount = 1;
        }

        private class PermissionProfile
        {
            public string PermissionRequired = "";

            [JsonConverter(typeof(StringEnumConverter))]
            public BuildingGrade.Enum MaxBuildingGradeGeneric = BuildingGrade.Enum.Wood;

            [JsonConverter(typeof(StringEnumConverter))]
            public BuildingGrade.Enum MaxBuildingGradeFrames = BuildingGrade.Enum.Stone;

            [JsonConverter(typeof(StringEnumConverter))]
            public BuildingGrade.Enum MaxBuildingGradeWaterFoundations = BuildingGrade.Enum.Metal;

            public float WaterDepthMin = 1F;
            public float WaterDepthMax = 250F;

            public int MaxWaterFoundationsPerBuilding = 25;

            public float MinDistanceFromOilrig = 100F;

            public float MinDistanceFromShore = 10F;
            public float MaxDistanceFromShore = 1000F;

            public bool CanDeployWaterFoundations = true;
            public bool CanReinforceWaterFoundations = true;
            public bool CanExpandWaterFoundations = true;
            public bool CanExpandReinforcedFoundations = true;
            public bool CanCraftWaterFoundations = false;

            public bool RequireMaterialsForCrafting = true;

            public float WorkbenchLevelRequired = 1F;

            public bool CanDeployUnderwaterNets = true;

            public Dictionary<Topology, bool> AllowedTopologies = new Dictionary<Topology, bool>();
        }

        private class UnderwaterLootDefinition
        {
            public string Shortname = null;
            public string CustomName = null;
            public ulong SkinID = 0;
            public float RandomChanceWeight = 1F;
            public int MinRandomAmount = 1;
            public int MaxRandomAmount = 1;
        }

        private class ConfigData
        {
            public string Version = "1.0.15";

            public bool RelyOnShoreDistance = false;

            public bool PlayerMessagesAsToasts = true;

            public bool GlobalOceanLevelCheckBeforePlacing = true;
            public float GlobalOceanLevelBaseline = 0F;

            public bool AdjustDeployedWaterLevel = true;

            [JsonConverter(typeof(StringEnumConverter))]
            public CargoPathHandling CargoShipPathHandling = CargoPathHandling.Warn;

            public float MinDistanceFromCargoShipNode = 40F;
            public bool RestrictBuildingToMapGrid = false;
            public bool EnableBarrelEntities = true;
            public float UpkeepMultiplier = 1F;

            public bool UnderwaterNetsCollectJunk = true;
            public float UnderwaterNetsRandomTimerMin = 600;
            public float UnderwaterNetsRandomTimerMax = 1200;
            public int UnderwaterNetsItemLimit = 10;

            public List<SerializableItemAmount> CraftingCostSquare = new List<SerializableItemAmount>();
            public List<SerializableItemAmount> CraftingCostTriangle = new List<SerializableItemAmount>();

            public Dictionary<string, PermissionProfile> PermissionProfiles = new Dictionary<string, PermissionProfile>();

            //deprecated, will be moved to const soon
            //
            public float GuiAnchorALeft = 0.645833F;
            public float GuiAnchorBBottom = 0.02592F;
            public float GuiAnchorCRight = 0.83125F;
            public float GuiAnchorDTop = 0.108333F;

            public float GuiNEW1AnchorMinX = float.PositiveInfinity;
            public float GuiNEW2AnchorMinY = float.PositiveInfinity;
            public float GuiNEW3AnchorMaxX = float.PositiveInfinity;
            public float GuiNEW4AnchorMaxY = float.PositiveInfinity;

            public float GuiNEW5OffsetMinX = float.PositiveInfinity;
            public float GuiNEW6OffsetMinY = float.PositiveInfinity;
            public float GuiNEW7OffsetMaxX = float.PositiveInfinity;
            public float GuiNEW8OffsetMaxY = float.PositiveInfinity;

            public int GuiTextSize = 14;
            public string GuiButtonColor = "4897ce";
            public string GuiTextColor = "f6eae0";
            public float GuiButtonAlpha = 1.0F;
            public float GuiTextAlpha = 1.0F;

            public bool GuiUseNewPositioning = true;

            public bool SharkHarvestingLootEnabled = true;

            public bool UnderwaterNetsSpawnSharks = true;

            public float UnderwaterNetsMinWaterDepth = 5F;

            public float UnderwaterNetsSharkRandomTimerMin = 1800; //between 30 mins
            public float UnderwaterNetsSharkRandomTimerMax = 3600; //to an hour

            public float UnderwaterNetsSharkTimerShortenByPercent = 8F; //so after 10 spawns, it goes down to roughly 0.38 (23/46 minutes from 60/120 minutes, respectively)

            public float UnderwaterNetsSharkSpawningMinDistance = 5F;
            public float UnderwaterNetsSharkSpawningMaxDistance = 30F;

            public int UnderwaterNetsSharkPopulationLimitLocal = 3; //max 3 sharks in 100 meter radius
            public int UnderwaterNetsSharkPopulationLimitGlobal = 30;

            public float UnderwaterNetsSharkSleepDespawnTimer = 600; //10 minutes

            public float UnderwaterNetsSharkHealthMultiplier = 1F;

            public float UnderwaterNetsSharkSpeedMultiplier = 1F;

            public float UnderwaterNetsSharkAggroRange = 15F;

            public float MapGridBoundsTolerance = 400F;

            public List<UnderwaterLootDefinition> SharkHarvestLoot = new List<UnderwaterLootDefinition>();


            public List<UnderwaterLootDefinition> UnderwaterLoot = new List<UnderwaterLootDefinition>();

            [JsonIgnore]
            public Dictionary<string, int> ProfileTopologyMask = new Dictionary<string, int>();

            [JsonIgnore]
            public Dictionary<Vector3, string> OilrigPositions = new Dictionary<Vector3, string>();

            [JsonIgnore]
            public Dictionary<ulong, SimpleShark> NettingSharks = new Dictionary<ulong, SimpleShark>();

            //needs recalculation
            [JsonIgnore]
            public float UnderwaterLootWeightSum;

            [JsonIgnore]
            public float SharkHarvestLootWeightSum;

        }

        private ConfigData configData;

        protected override void LoadDefaultConfig()
        {
            RestoreDefaultConfig();
        }

        private void ProcessConfigData()
        {
            bool needsSave = false;

            if (configData.PermissionProfiles?.Count == 0)
            {
                configData.PermissionProfiles = new Dictionary<string, PermissionProfile>
                {
                    [PERM_ADMIN] = AdminPermissionProfile(),

                    ["default"] = DefaultPermissionProfile(),

                    [PERM_VIP1] = VIPPermissionProfile(),

                };
                needsSave = true;
            }

            if (float.IsPositiveInfinity(configData.GuiNEW1AnchorMinX) || float.IsPositiveInfinity(configData.GuiNEW2AnchorMinY) || float.IsPositiveInfinity(configData.GuiNEW3AnchorMaxX) || float.IsPositiveInfinity(configData.GuiNEW4AnchorMaxY) || float.IsPositiveInfinity(configData.GuiNEW5OffsetMinX) || float.IsPositiveInfinity(configData.GuiNEW7OffsetMaxX) || float.IsPositiveInfinity(configData.GuiNEW8OffsetMaxY))
            {
                float minOffsetX, minOffsetY, maxOffsetX, maxOffsetY;

                GetAnchorFromScreenBoxOld(Instance.configData.GuiAnchorALeft, Instance.configData.GuiAnchorBBottom, Instance.configData.GuiAnchorCRight, Instance.configData.GuiAnchorDTop, out minOffsetX, out minOffsetY, out maxOffsetX, out maxOffsetY);

                Instance.configData.GuiNEW1AnchorMinX = ANCHOR_X;
                Instance.configData.GuiNEW2AnchorMinY = ANCHOR_Y;
                Instance.configData.GuiNEW3AnchorMaxX = ANCHOR_X;
                Instance.configData.GuiNEW4AnchorMaxY = ANCHOR_Y;

                Instance.configData.GuiNEW5OffsetMinX = minOffsetX;
                Instance.configData.GuiNEW6OffsetMinY = minOffsetY;
                Instance.configData.GuiNEW7OffsetMaxX = maxOffsetX;
                Instance.configData.GuiNEW8OffsetMaxY = maxOffsetY;

                needsSave = true;
            }

            if (configData.UnderwaterLoot?.Count == 0)
            {
                configData.UnderwaterLoot = DefaultUnderwaterLoot();
                needsSave = true;
            }

            if (configData.SharkHarvestLoot?.Count == 0)
            {
                //copy underwater loot from the current config and multiply amounts by 5
                configData.SharkHarvestLoot = new List<UnderwaterLootDefinition>();

                for (var i=0; i<configData.UnderwaterLoot.Count; i++)
                {
                    var copyFrom = configData.UnderwaterLoot[i];

                    var copyTo = new UnderwaterLootDefinition
                    {
                        Shortname = copyFrom.Shortname,
                        SkinID = copyFrom.SkinID,
                        CustomName = copyFrom.CustomName,
                        MaxRandomAmount = copyFrom.MaxRandomAmount*5,
                        MinRandomAmount = copyFrom.MinRandomAmount*5
                    };

                    configData.SharkHarvestLoot.Add(copyTo);
                }

                needsSave = true;
            }

            if (configData.CraftingCostSquare?.Count == 0)
            {
                configData.CraftingCostSquare = DefaultCostSquare();
                needsSave = true;
            }

            if (configData.CraftingCostTriangle?.Count == 0)
            {
                configData.CraftingCostTriangle = DefaultCostTriangle();
                needsSave = true;
            }

            if (configData.Version != "1.0.15")
            {
                var version = configData.Version;
                configData.Version = "1.0.15";
                Instance.PrintWarning($"\n\nYou have succesfully updated from {version} to 1.0.16\n");
                needsSave = true;
            }

            if (needsSave)
            {

                SaveConfigData();
            }

            GenerateOilrigPositionCache();
            RegenerateProfileTopologyCache();
        }

        private void LoadConfigData()
        {
            PrintWarning("Loading configuration file...");
            try
            {
                configData = Config.ReadObject<ConfigData>();
                PrintWarning("Success.");
            }
            catch
            {
                RestoreDefaultConfig();
                PrintWarning("Loading failed, generating new...");

            }

        }
        private void SaveConfigData()
        {
            PrintWarning("Saving config...");
            Config.WriteObject(configData, true);
        }

        private void RestoreDefaultConfig()
        {
            PrintWarning("Generating default config...");

            configData = new ConfigData();

            SaveConfigData();
        }

        private void GenerateOilrigPositionCache()
        {
            configData.OilrigPositions.Clear();

            foreach (var info in TerrainMeta.Path.Monuments)
            {
                var nameToLower = info.name.ToLower();

                if (!nameToLower.Contains("oilrigai"))
                {
                    continue;
                }

                configData.OilrigPositions.Add(info.transform.position, info.name);
            }
        }

        private void RegenerateProfileTopologyCache()
        {
            configData.ProfileTopologyMask.Clear();

            foreach (var entry in configData.PermissionProfiles)
            {
                configData.ProfileTopologyMask.Add(entry.Key, GetTopologyMaskFromDictionary(entry.Value.AllowedTopologies));
            }
        }
        private List<SerializableItemAmount> DefaultCostSquare()
        {
            return new List<SerializableItemAmount>
            {
                new SerializableItemAmount
                {
                    Shortname = "wood",
                    Amount = 100
                },
                new SerializableItemAmount
                {
                    Shortname = "metal.fragments",
                    Amount = 200
                }
            };
        }

        private List<SerializableItemAmount> DefaultCostTriangle()
        {
            return new List<SerializableItemAmount>
            {
                new SerializableItemAmount
                {
                    Shortname = "wood",
                    Amount = 50
                },
                new SerializableItemAmount
                {
                    Shortname = "metal.fragments",
                    Amount = 100
                }
            };
        }

        private List<UnderwaterLootDefinition> DefaultUnderwaterLoot()
        {
            List<UnderwaterLootDefinition> result = new List<UnderwaterLootDefinition>
            {
                new UnderwaterLootDefinition
                {
                    Shortname = "scrap",
                    MinRandomAmount = 1,
                    MaxRandomAmount = 20,
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "metalblade",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "rope",
                    MinRandomAmount = 1,
                    MaxRandomAmount = 2,
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "propanetank",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "tarp",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "sewingkit",
                    MinRandomAmount = 3,
                    MaxRandomAmount = 4,
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "fuse",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "roadsigns",
                    MinRandomAmount = 2,
                    MaxRandomAmount = 3,
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "sheetmetal",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "metalspring",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "gears",
                    MinRandomAmount = 1,
                    MaxRandomAmount = 2,
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "metalpipe",
                    MinRandomAmount = 1,
                    MaxRandomAmount = 4,
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "semibody",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "barricade.wood",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "planter.large",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "paddle",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "bucket.water",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "mailbox",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "hat.cap",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "fun.guitar",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "can.tuna.empty",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "fireplace.stone",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "mask.bandana",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "barricade.woodwire",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "sign.wooden.large",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "electric.igniter",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "hat.boonie",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "spinner.wheel",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "rug.bear",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "table",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "shelves",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "water.barrel",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "spikes.floor",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "shirt.tanktop",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "pants.shorts",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "shutter.wood.a",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "barricade.stone",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "tool.binoculars",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "burlap.gloves",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "mask.balaclava",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "sign.wooden.huge",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "hat.beenie",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "rug",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "fish.minnows",
                    MinRandomAmount = 1,
                    MaxRandomAmount = 32
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "fish.troutsmall",
                    MinRandomAmount = 1,
                    MaxRandomAmount = 8,
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "innertube",
                    CustomName = null,
                    SkinID = ITEM_SKIN_FOUNDATION_SQUARE,
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "innertube",
                    CustomName = null,
                    SkinID = ITEM_SKIN_FOUNDATION_TRIANGLE,
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "diving.fins",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "diving.mask",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "diving.tank",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "diving.wetsuit",
                },
                new UnderwaterLootDefinition
                {
                    Shortname = "smallwaterbottle",
                }
            };

            return result;
        }

        private PermissionProfile AdminPermissionProfile()
        {
            return new PermissionProfile
            {
                PermissionRequired = PERM_ADMIN,
                CanDeployWaterFoundations = true,
                CanExpandWaterFoundations = true,
                CanExpandReinforcedFoundations = true,
                CanReinforceWaterFoundations = true,
                AllowedTopologies = DefaultAllowedTopologies(),
                MinDistanceFromShore = float.NegativeInfinity,
                MaxDistanceFromShore = float.PositiveInfinity,
                MinDistanceFromOilrig = float.NegativeInfinity,
                MaxBuildingGradeGeneric = BuildingGrade.Enum.TopTier,
                MaxBuildingGradeFrames = BuildingGrade.Enum.TopTier,
                MaxBuildingGradeWaterFoundations = BuildingGrade.Enum.TopTier,
                MaxWaterFoundationsPerBuilding = int.MaxValue,
                WaterDepthMin = float.NegativeInfinity,
                WaterDepthMax = float.PositiveInfinity,
                RequireMaterialsForCrafting = false,
                CanCraftWaterFoundations = true,
                WorkbenchLevelRequired = 0F
            };
        }

        private PermissionProfile DefaultPermissionProfile()
        {
            return new PermissionProfile
            {
                PermissionRequired = "default",
                CanDeployWaterFoundations = true,
                CanExpandWaterFoundations = true,
                CanExpandReinforcedFoundations = true,
                CanReinforceWaterFoundations = true,
                AllowedTopologies = DefaultAllowedTopologies(),
                MinDistanceFromShore = 2F,
                MaxDistanceFromShore = 20F,
                MinDistanceFromOilrig = 200F,
                MaxBuildingGradeGeneric = BuildingGrade.Enum.Wood,
                MaxBuildingGradeFrames = BuildingGrade.Enum.Stone,
                MaxBuildingGradeWaterFoundations = BuildingGrade.Enum.Metal,
                MaxWaterFoundationsPerBuilding = 25,
                WaterDepthMin = 2F,
                WaterDepthMax = 100F,
                RequireMaterialsForCrafting = true,
                CanCraftWaterFoundations = true,
                WorkbenchLevelRequired = 2F
            };
        }

        private PermissionProfile VIPPermissionProfile()
        {
            return new PermissionProfile
            {
                PermissionRequired = PERM_VIP1,
                CanDeployWaterFoundations = true,
                CanExpandWaterFoundations = true,
                CanExpandReinforcedFoundations = true,
                CanReinforceWaterFoundations = true,
                AllowedTopologies = DefaultAllowedTopologies(),
                MinDistanceFromShore = 1F,
                MaxDistanceFromShore = 30F,
                MinDistanceFromOilrig = 100F,
                MaxBuildingGradeGeneric = BuildingGrade.Enum.Stone,
                MaxBuildingGradeFrames = BuildingGrade.Enum.Metal,
                MaxBuildingGradeWaterFoundations = BuildingGrade.Enum.TopTier,
                MaxWaterFoundationsPerBuilding = 100,
                WaterDepthMin = 1F,
                WaterDepthMax = 250F,
                RequireMaterialsForCrafting = true,
                CanCraftWaterFoundations = true,
                WorkbenchLevelRequired = 1F
            };
        }

        private Dictionary<Topology, bool> DefaultAllowedTopologies()
        {
            return new Dictionary<Topology, bool>
            {
                [Topology.Alt] = true,
                [Topology.Beach] = true,
                [Topology.Beachside] = true,
                [Topology.Building] = false,
                [Topology.Cliff] = true,
                [Topology.Cliffside] = true,
                [Topology.Clutter] = true,
                [Topology.Decor] = true,
                [Topology.Field] = true,
                [Topology.Forest] = true,
                [Topology.Forestside] = true,
                [Topology.Hilltop] = true,
                [Topology.Lake] = true,
                [Topology.Lakeside] = true,
                [Topology.Mainland] = true,
                [Topology.Monument] = false,
                [Topology.Mountain] = true,
                [Topology.Ocean] = true,
                [Topology.Oceanside] = true,
                [Topology.Offshore] = true,
                [Topology.Plain] = true,
                [Topology.Powerline] = true,
                [Topology.River] = true,
                [Topology.Riverside] = true,
                [Topology.Road] = true,
                [Topology.Roadside] = true,
                [Topology.Summit] = true,
                [Topology.Swamp] = true,
                [Topology.Tier0] = true,
                [Topology.Tier1] = true,
                [Topology.Tier2] = true
            };
        }
        #endregion

        #region HELPERS


        #region EXTENDED FLAGS
        [Flags]
        public enum FlagsExtended
        {
            Internal = 0x40000000,

            //use to identify which internal entity type that is
            Bit1 = 0x8000000,
            Bit2 = 0x10000000,
            Bit3 = 0x20000000,
        }
        [Flags]
        public enum OldFlagsExtended
        {
            // Internal = 0x2000000,
        
            //use to identify which internal entity type that is
            Bit1 = 0x4000000,
            Bit2 = 0x8000000,
            Bit3 = 0x10000000,
        }
        //store this in Bit1, Bit2, Bit 3

        public enum InternalEntityType
        {
            Barrel = 0,
            NettingOrShark = 1,
            FoundationSquare = 2,
            FoundationTriangle = 3,
            FoundationSquareInversed = 4,
            FoundationTriangleInversed = 5,
            GenericDecayEntity = 6,
            GenericDecayEntityInversed = 7,

            //
            NotInternal = -1
        }

        public static void MarkAsSpecificType(BaseEntity entity, InternalEntityType entityType)
        {
            MarkAsInternal(entity);

            //cast entity type to int and store the first 3 bits of it in the
            //3 FlagsExtended bits: Bit1, Bit2, Bit3 (so that the least significant bit of the entityType is Bit1, and the most significant is Bit3)

            var intValue = (int)entityType;

            bool bit1 = (intValue & 1) != 0;
            bool bit2 = (intValue & 2) != 0;
            bool bit3 = (intValue & 4) != 0;

            SetFlagExtended(entity, FlagsExtended.Bit1, bit1);
            SetFlagExtended(entity, FlagsExtended.Bit2, bit2);
            SetFlagExtended(entity, FlagsExtended.Bit3, bit3);
        }

        public static InternalEntityType GetSpecificType(BaseEntity entity)
        {
            var intValue = (int)entity.flags;

            if (!IsInternal(entity))
            {
                return InternalEntityType.NotInternal;
            }

            bool bit1 = HasFlagExtended(entity, FlagsExtended.Bit1);
            bool bit2 = HasFlagExtended(entity, FlagsExtended.Bit2);
            bool bit3 = HasFlagExtended(entity, FlagsExtended.Bit3);

            int buildFlagIntValue = 0;

            if (bit1)
            {
                buildFlagIntValue += 1;
            }

            if (bit2)
            {
                buildFlagIntValue += 2;
            }

            if (bit3)
            {
                buildFlagIntValue += 4;
            }

            var result = (InternalEntityType)buildFlagIntValue;

            return result;
        }

        public static void MarkAsInternal(BaseEntity entity)
        {
            SetFlagExtended(entity, FlagsExtended.Internal, true);
        }

        public static FlagsExtended ConvertFromOldFlagsExtended(OldFlagsExtended oldFlags)
        {
            var newFlags = (FlagsExtended)oldFlags;

            newFlags |= FlagsExtended.Internal;

            if ((oldFlags & OldFlagsExtended.Bit1) == OldFlagsExtended.Bit1)
            {
                newFlags |= FlagsExtended.Bit1;
            }

            if ((oldFlags & OldFlagsExtended.Bit2) == OldFlagsExtended.Bit2)
            {
                newFlags |= FlagsExtended.Bit2;
            }

            if ((oldFlags & OldFlagsExtended.Bit3) == OldFlagsExtended.Bit3)
            {
                newFlags |= FlagsExtended.Bit3;
            }

            return newFlags;
        }

        public static void MarkAsInternalBarrel(CollectibleEntity barrel) => MarkAsSpecificType(barrel, InternalEntityType.Barrel);
        public static void MarkAsInternalNettingOrShark(BaseCombatEntity nettingOrShark) => MarkAsSpecificType(nettingOrShark, InternalEntityType.NettingOrShark);
        public static void MarkAsInternalFoundationSquare(StabilityEntity squareFloor) => MarkAsSpecificType(squareFloor, InternalEntityType.FoundationSquare);
        public static void MarkAsInternalFoundationTriangle(StabilityEntity triangleFloor) => MarkAsSpecificType(triangleFloor, InternalEntityType.FoundationTriangle);
        public static void MarkAsInternalFoundationSquareInversed(StabilityEntity squareFloor) => MarkAsSpecificType(squareFloor, InternalEntityType.FoundationSquareInversed);
        public static void MarkAsInternalFoundationTriangleInversed(StabilityEntity triangleFloor) => MarkAsSpecificType(triangleFloor, InternalEntityType.FoundationTriangleInversed);
        public static void MarkAsInternalGenericDecayEntity(DecayEntity decayEntity) => MarkAsSpecificType(decayEntity, InternalEntityType.GenericDecayEntity);
        public static void MarkAsInternalGenericDecayEntityInversed(DecayEntity decayEntity) => MarkAsSpecificType(decayEntity, InternalEntityType.GenericDecayEntityInversed);

        public static bool IsInternal(BaseEntity entity)
        {
            return HasFlagExtended(entity, FlagsExtended.Internal);
        }

        public static bool IsOldInternal(BaseEntity entity)
        {
            return HasFlagExtended(entity, (FlagsExtended)0x1000000);
        }

        public static bool IsBuildingGrounded(BuildingBlock entity)
        {
            var building = entity.GetBuilding();
            if (building == null)
                return true;

            foreach (var block in building.buildingBlocks)
            {
                if (block.grounded)
                    return true;
            }

            return false;
        }


        public static bool HasFlagExtended(BaseEntity entity, FlagsExtended f)
        {
            return ((int)entity.flags & (int)f) == (int)f;
        }

        public static void SetFlagExtended(BaseEntity entity, FlagsExtended f, bool b)
        {
            if (b)
            {
                if (HasFlagExtended(entity, f))
                {
                    return;
                }

                entity.flags |= (BaseEntity.Flags)(int)f;
            }
            else
            {
                if (!HasFlagExtended(entity, f))
                {
                    return;
                }

                entity.flags &= (BaseEntity.Flags)(int)~f;
            }
        }

        #endregion

        public static string SpliceText(string inputText, int lineLength)
        {
            StringBuilder builder = new StringBuilder();

            string[] stringSplit = inputText.Split(' ');
            int charCounter = 0;

            for (int i = 0; i < stringSplit.Length; i++)
            {
                builder.Append(stringSplit[i]);
                builder.Append(' ');
                charCounter += stringSplit[i].Length;

                if (charCounter > lineLength)
                {
                    builder.Append('\n');
                    charCounter = 0;
                }
            }
            return builder.ToString();
        }

        public static void DelayedKillWithGibs(BaseEntity entity)
        {
            entity.Invoke(() => entity.Kill(BaseNetworkable.DestroyMode.Gib), 0.01F);
        }

        public static Dictionary<string, Translate.Phrase> ToastsCached;

        public const int BREAK_TOASTS_EVERY_N_CHARACTERS = 80;

        public static void ToastPlayer(BasePlayer player, string toast, GameTip.Styles style = GameTip.Styles.Blue_Normal)
        {
            string toastKeyForPlayer = string.Format("{0}.{1}", toast, player.UserIDString);

            if (!ToastsCached.TryGetValue(toastKeyForPlayer, out var translatePhrase))
            {
                if (toast.Length > BREAK_TOASTS_EVERY_N_CHARACTERS)
                {
                    toast = SpliceText(toast, BREAK_TOASTS_EVERY_N_CHARACTERS);
                }

                translatePhrase = new Translate.Phrase(toastKeyForPlayer, toast);

                ToastsCached.Add(toastKeyForPlayer, translatePhrase);
            }

            player.ShowToast(style, translatePhrase, true);
        }

        private static bool HasAdminPermission(IPlayer player)
        {
            if (player.IsServer) return true;
            if (player.IsAdmin) return true;
            if (Instance.permission.UserHasPermission(player.Id, PERM_ADMIN)) return true;

            return false;
        }

        private static bool HasAdminPermission(BasePlayer player)
        {
            if (player.IsDeveloper) return true;
            if (player.IsAdmin) return true;
            if (Instance.permission.UserHasPermission(player.UserIDString, PERM_ADMIN)) return true;

            return false;
        }

        private static Tuple<Vector3, Quaternion> GetWorldTransformRelativeTo(Transform transform, Vector3 localPosition, Vector3 localRotation)
        {
            return new Tuple<Vector3, Quaternion>(transform.TransformPoint(localPosition), transform.rotation * Quaternion.Euler(localRotation));
        }

        private static bool TryTakeNeededMaterialsFromPlayer(BasePlayer player, ulong foundationSkin, int amount = 1)
        {
            List<SerializableItemAmount> itemAmounts = foundationSkin == ITEM_SKIN_FOUNDATION_SQUARE ? Instance.configData.CraftingCostSquare : Instance.configData.CraftingCostTriangle;

            ItemDefinition def;

            bool soFarSoGood = true;

            string needed = "";

            int counter = 0;

            foreach (var itemEntry in itemAmounts)
            {
                def = ItemManager.FindItemDefinition(itemEntry.Shortname);

                if (def == null)
                {
                    //ignore this item
                    continue;
                }

                needed += $"{def.displayName.translated} x {itemEntry.Amount * amount}";

                if (counter != itemAmounts.Count-1)
                {
                    needed += $", ";
                }

                if (player.inventory.GetAmount(def.itemid) < itemEntry.Amount * amount)
                {
                    soFarSoGood = false;
                }

                counter++;
            }

            if (!soFarSoGood)
            {
                TellMessage(player, MSG(MSG_CRAFTING_NO_MATERIALS, player.UserIDString, needed, $"{amount} {(foundationSkin == ITEM_SKIN_FOUNDATION_SQUARE ? MSG(MSG_ITEM_NAME_SQUARE, player.UserIDString) : MSG(MSG_ITEM_NAME_TRIANGLE, player.UserIDString))}"), false, true);
                return false;
            }
            else
            {
                //now you can take
                foreach (var itemEntry in itemAmounts)
                {
                    def = ItemManager.FindItemDefinition(itemEntry.Shortname);

                    if (def == null)
                    {
                        continue;
                    }

                    player.inventory.Take(null, def.itemid, itemEntry.Amount*amount);
                    player.Command("note.inv", def.itemid, -itemEntry.Amount*amount);
                }

                return true;

            }
        }

        private static bool TryCraftingWaterFoundation(BasePlayer player, ulong skin, bool skipChecks = false, int amount = 1)
        {
            bool result = false;

            if (!skipChecks)
            {
                var prof = GetPermissionProfile(player);

                if (!prof.CanCraftWaterFoundations)
                {
                    TellMessage(player, MSG(MSG_CRAFTING_NO_PERM, player.UserIDString), false, true);
                    result = false;
                }

                if (player.currentCraftLevel < prof.WorkbenchLevelRequired)
                {
                    TellMessage(player, MSG(MSG_CRAFTING_INSUFFICIENT_WB_LEVEL, player.UserIDString, prof.WorkbenchLevelRequired.ToString("0")), false, true);
                    result = false;
                }
                else
                {
                    if (prof.RequireMaterialsForCrafting)
                    {
                        if (TryTakeNeededMaterialsFromPlayer(player, skin, amount))
                        {
                            //this already displays notification
                            result = true;
                        }
                        else
                        {
                            result = false;
                        }
                    }
                    else
                    {
                        result = true;
                    }
                }
            }

            if (result == true)
            {
                player.GiveItem(MakeFoundationItem(skin), BaseEntity.GiveItemReason.Crafted);
            }

            return result;
        }

        private static Item CreateRandomItemFromTable(List<UnderwaterLootDefinition> table, float weightSum)
        {
            Item newItem = null;

            var diceThrow = UnityEngine.Random.Range(0F, weightSum);

            var sumSoFar = 0F;

            int foundID = -1;

            for (var i = 0; i < table.Count; i++)
            {
                sumSoFar += table[i].RandomChanceWeight;

                if (sumSoFar >= diceThrow)
                {
                    foundID = i;
                    break;
                }
            }

            if (foundID != -1)
            {
                var tableEntry = table[foundID];

                newItem = ItemManager.CreateByName(tableEntry.Shortname, tableEntry.MinRandomAmount == tableEntry.MaxRandomAmount ? tableEntry.MinRandomAmount : UnityEngine.Random.Range(tableEntry.MinRandomAmount, tableEntry.MaxRandomAmount), tableEntry.SkinID);

                if (tableEntry.CustomName != null)
                {
                    newItem.name = tableEntry.CustomName;
                }

                newItem.MarkDirty();
            }

            return newItem;
        }

        private static Item CreateRandomSharkHarvestItem()
        {
            return CreateRandomItemFromTable(Instance.configData.SharkHarvestLoot, Instance.configData.SharkHarvestLootWeightSum);
        }

        private static SimpleShark SpawnNettingShark(Vector3 positionForAppearingAt, Vector3 positionToBeFixatedAbout)
        {
            var newSharkboi = GameManager.server.CreateEntity(PREFAB_SHARK_ALIVE, positionToBeFixatedAbout, Quaternion.Euler(0F, UnityEngine.Random.Range(0, 360F), 0F)) as SimpleShark;
            newSharkboi.limitNetworking = true;

            MarkAsInternalNettingOrShark(newSharkboi);

            newSharkboi.Spawn();

            newSharkboi.Invoke(() =>
            {
                //reset the shark spawnPos to positionToBeFixatedAbout
                newSharkboi.transform.position = positionForAppearingAt;
                newSharkboi.transform.hasChanged = true;
                newSharkboi.SendChildrenNetworkUpdateImmediate();
                newSharkboi.limitNetworking = false;
            }, 0.02F);

            return newSharkboi;
        }

        private static void TrySpawnSharkNearNetting(StabilityEntity netting)
        {
            if (!BaseNetworkable.HasCloseConnections(netting.transform.position, 100F))
            {
                return;
            }

            //how many sharks total?
            if (Instance.configData.NettingSharks.Count >= Instance.configData.UnderwaterNetsSharkPopulationLimitGlobal)
            {
                return;
            }

            //do a vis. entities. how many sharks around the radius?
            //do a pebcak check. Do a Vis.Entites
            List<SimpleShark> listToCheck = Facepunch.Pool.Get<List<SimpleShark>>();

            Vis.Entities(netting.transform.position, 100F, listToCheck, int.MaxValue, QueryTriggerInteraction.Ignore);

            if (listToCheck.Count < Instance.configData.UnderwaterNetsSharkPopulationLimitLocal)
            {
                //keep trying to find a valid spawn position.
                //try a random distance from config times random on unity sphere.
                //keep doing that while...

                Vector3 potentialPosition;
                const int MAX_ATTEMPS_SPAWNING_SHARKS = 64;

                int currentAttemps = 0;

                do
                {
                    if (currentAttemps == MAX_ATTEMPS_SPAWNING_SHARKS)
                    {
                        break; //failed. try next time.
                    }
                    Vector3 onUnitSphere = UnityEngine.Random.onUnitSphere;

                    if (onUnitSphere.y > 0)
                    {
                        onUnitSphere *= -1; //change sign to negative if it's positive, this creates a hemisphere with a pole pointing up
                    }

                    potentialPosition = netting.transform.position + UnityEngine.Random.Range(Instance.configData.UnderwaterNetsSharkSpawningMinDistance, Instance.configData.UnderwaterNetsSharkSpawningMaxDistance) * onUnitSphere;

                    //over water?
                    var waterAndTerrainHeight = WaterLevel.GetWaterAndTerrainSurface(potentialPosition, false, true);
                    var waterHeight = waterAndTerrainHeight.Item1;
                    var terrainHeight = waterAndTerrainHeight.Item2;

                    //water must be covering land...

                    if (waterHeight < terrainHeight)
                    {
                        currentAttemps++;
                        continue;
                    }

                    //and be at least 5 m deep...

                    if (Mathf.Abs(terrainHeight - waterHeight) < 5F)
                    {
                        currentAttemps++;
                        continue;
                    }

                    //must not be under terrain...
                    if (potentialPosition.y < terrainHeight)
                    {
                        currentAttemps++;
                        continue;
                    }

                    //or over water...
                    if (potentialPosition.y > waterHeight)
                    {
                        currentAttemps++;
                        continue;
                    }

                    //overlapping colliders?

                    if (Physics.CheckSphere(potentialPosition, 2F, Rust.Layers.Solid, QueryTriggerInteraction.Ignore))
                    {
                        currentAttemps++;
                        continue;
                    }

                    //ok, we good to go!

                    //spawning it will also auto-apply compo
                    var newShark = SpawnNettingShark(potentialPosition, netting.transform.position);

                    break;
                }
                while (true);

            }

            Facepunch.Pool.FreeUnmanaged(ref listToCheck);
        }

        private static int CountNettingItems(StabilityEntity netting)
        {
            return netting.gameObject.GetComponentsInChildren<DroppedItem>().Length;
        }

        private static void ConsumeFromSpecificItemList(ref List<Item> specificList, int amount = 1)
        {
            var consumedSoFar = 0;

            foreach (var item in specificList)
            {
                for (var i=0; i<item.amount; i++)
                {
                    item.UseItem(1);
                    consumedSoFar++;

                    if (consumedSoFar >= amount)
                    {
                        break;
                    }
                }
            }

            Facepunch.Pool.FreeUnmanaged(ref specificList);
        }

        private static List<Item> FindNeededFoundationItemsInInventory(BasePlayer player, BuildingBlock buildingBlock, int neededAmount = 1)
        {
            ulong foundationSkin = 0;

            var getType = GetSpecificType(buildingBlock);

            switch (getType)
            {
                case InternalEntityType.FoundationSquare:
                case InternalEntityType.FoundationSquareInversed:
                    {
                        foundationSkin = ITEM_SKIN_FOUNDATION_SQUARE;
                        break;
                    }

                case InternalEntityType.FoundationTriangle:
                case InternalEntityType.FoundationTriangleInversed:
                    {
                        foundationSkin = ITEM_SKIN_FOUNDATION_TRIANGLE;
                        break;
                    }
            }

            List<Item> result = Facepunch.Pool.Get<List<Item>>();

            var foundSoFar = 0;

            foreach (var item in player.inventory.containerMain.itemList)
            {
                if (item.info.itemid != ITEM_INNER_TUBE) continue;
                if (item.skin == foundationSkin)
                {
                    foundSoFar += item.amount;
                    result.Add(item);

                    if (foundSoFar >= neededAmount)
                    {

                        return result;
                    }

                }
            }

            foreach (var item in player.inventory.containerBelt.itemList)
            {
                if (item.info.itemid != ITEM_INNER_TUBE) continue;

                if (item.skin == foundationSkin)
                {
                    foundSoFar += item.amount;
                    result.Add(item);

                    if (foundSoFar >= neededAmount)
                    {
                        return result;
                    }

                }
            }

            return null;
        }
        private static BuildingBlock TryFindWaterFoundationAboveYou(BuildingBlock inverseFoundation)
        {
            BuildingBlock result = null;

            var trans = GetWorldTransformRelativeTo(inverseFoundation.transform, Vector3.forward*0.2F, Vector3.zero);

            var hits = Physics.RaycastAll(trans.Item1, Vector3.up, 2F, ConstructionMask);

            BaseEntity ent;
            BuildingBlock bui;
            RaycastHit hit;

            if (hits != null)
            {
                for (var h = 0; h < hits.Length; h++)
                {
                    hit = hits[h];
                    ent = hit.GetEntity();
                    if (ent == null)
                    {
                        continue;
                    }

                    if (ent.net == null)
                    {
                        continue;
                    }

                    if (ent.IsDestroyed)
                    {
                        continue;
                    }

                    if (ent.net.ID == inverseFoundation.net.ID)
                    {
                        continue;
                    }

                    bui = ent as BuildingBlock;

                    if (bui == null)
                    {
                        continue;
                    }

                    if (IsWaterFoundation(bui))
                    {
                        result = bui;
                        break;
                    }
                }
            }

            return result;
        }

        private static void MonoiseUnderwaterNetting(StabilityEntity underwaterNetting)
        {
            if (underwaterNetting.gameObject.HasComponent<UnderwaterNetHelper>())
            {
                return;
            }

            underwaterNetting.gameObject.AddComponent<UnderwaterNetHelper>().Prepare(underwaterNetting);
        }

        private static void MonoiseInverseFoundation(BuildingBlock inverseFoundation)
        {
            if (inverseFoundation.gameObject.HasComponent<InverseFoundationHelper>())
            {
                return;
            }

            inverseFoundation.gameObject.AddComponent<InverseFoundationHelper>().Prepare(inverseFoundation);
        }

        private static void EnsureCorrectNumberOfBarrels(BuildingBlock specialFoundationBlock)
        {
            List<CollectibleEntity> barrelsCurrentlyParented = null;
            //check if there's any barrels, and if so, kill them.
            barrelsCurrentlyParented = Facepunch.Pool.Get<List<CollectibleEntity>>();

            //only direct children, not children of children
            barrelsCurrentlyParented.AddRange(specialFoundationBlock.GetComponentsInChildren<CollectibleEntity>().Where(e => e.GetParentEntity() == specialFoundationBlock));

            if (!Instance.configData.EnableBarrelEntities)
            {
                if (barrelsCurrentlyParented.Count == 0)
                {
                    //no barrels. good. return.
                    Facepunch.Pool.FreeUnmanaged(ref barrelsCurrentlyParented);
                    return;
                }
                else
                {
                    foreach (var oldBarrel in barrelsCurrentlyParented)
                    {
                        oldBarrel.Kill(BaseNetworkable.DestroyMode.None);
                    }

                    //now there's no barrels. return.
                    Facepunch.Pool.FreeUnmanaged(ref barrelsCurrentlyParented);
                    return;
                }
            }


                //ok, barrels are enabled...

                //barrels enabled. expected?
                var expected = specialFoundationBlock.PrefabName == PREFAB_FLOOR_SQUARE ? 10 : 5;

            if (expected == barrelsCurrentlyParented.Count)
            {
                //all good. you have correct numbers. return.

                //ensure correct layer and stability
                foreach (var oldBarrel in barrelsCurrentlyParented)
                {
                    MarkAsInternalBarrel(oldBarrel);
                    MakeStable(oldBarrel, true);

                }

                Facepunch.Pool.FreeUnmanaged(ref barrelsCurrentlyParented);
                return;
            }

            //too much? not enough? get rid of them all.

            foreach (var oldBarrel in barrelsCurrentlyParented)
            {
                oldBarrel.Kill(BaseNetworkable.DestroyMode.None);
            }

            //don't need the list.
            Facepunch.Pool.FreeUnmanaged(ref barrelsCurrentlyParented);


            List<Vector3> listToUsePos = FloorBarrelPositionsSquare;
            List<Vector3> listToUseRot = FloorBarrelRotationsSquare;

            if (specialFoundationBlock.PrefabName == PREFAB_FLOOR_TRIANGLE)
            {
                listToUsePos = FloorBarrelPositionsTriangle;
                listToUseRot = FloorBarrelRotationsTriangle;
            }

            if (listToUsePos.Count != listToUseRot.Count)
            {
                if (barrelsCurrentlyParented != null)
                {
                    Facepunch.Pool.FreeUnmanaged(ref barrelsCurrentlyParented);
                }
                return;
            }

            CollectibleEntity barrel;

            Tuple<Vector3, Quaternion> transf;

            for (var i = 0; i < listToUsePos.Count; i++)
            {
                transf = GetWorldTransformRelativeTo(specialFoundationBlock.transform, listToUsePos[i], listToUseRot[i]);

                barrel = GameManager.server.CreateEntity(PREFAB_BARREL_COLLECTABLE, transf.Item1, transf.Item2) as CollectibleEntity;

                barrel.Spawn();

                MarkAsInternalBarrel(barrel);

                MakeStable(barrel, true);
                //barrel.gameObject.SetLayerRecursive((int)Rust.Layer.Prevent_Building);

                barrel.itemList = null;

                barrel.SetParent(specialFoundationBlock, true, false);
            }

            if (barrelsCurrentlyParented != null)
            {
                Facepunch.Pool.FreeUnmanaged(ref barrelsCurrentlyParented);
            }

        }

        private static void RecalculateUnderwaterAndSharkHarvestLootWeights()
        {
            Instance.configData.UnderwaterLootWeightSum = 0F;

            foreach (var entry in Instance.configData.UnderwaterLoot.ToArray())
            {
                //remove non existing while you're at it
                if (!ItemManager.FindItemDefinition(entry.Shortname))
                {
                    Instance.configData.UnderwaterLoot.Remove(entry);
                    continue;
                }

                Instance.configData.UnderwaterLootWeightSum += entry.RandomChanceWeight;
            }

            Instance.configData.SharkHarvestLootWeightSum = 0F;

            foreach (var entry in Instance.configData.SharkHarvestLoot.ToArray())
            {
                //remove non existing while you're at it
                if (!ItemManager.FindItemDefinition(entry.Shortname))
                {
                    Instance.configData.SharkHarvestLoot.Remove(entry);
                    continue;
                }

                Instance.configData.SharkHarvestLootWeightSum += entry.RandomChanceWeight;
            }
        }

        private static bool IsUnderwaterNetting(StabilityEntity stabilityEntity, bool inWaterBaseAlreadyChecked = false)
        {
            if (stabilityEntity.PrefabName != PREFAB_NETTING)
            {
                return false;
            }
            
            if (Mathf.Abs(stabilityEntity.transform.eulerAngles.z) <= 0.1F)
            {
                return false;
            }

            if (inWaterBaseAlreadyChecked)
            {
                return true;
            }

            if (!IsInWaterBase(stabilityEntity))
            {
                return false;
            }

            return true;
        }
        /*
        private static void ConvertBrokenFlags()
        {
            var changed = 0;
            foreach (var entity in BaseNetworkable.serverEntities.OfType<BuildingBlock>())
            {
                var prefabName = entity.PrefabName;
                if (prefabName != PREFAB_FLOOR_SQUARE && prefabName != PREFAB_FLOOR_TRIANGLE)
                    continue;

                var position = entity.transform.position;
                var waterHeight = TerrainMeta.WaterMap.GetHeight(position);
                if (Math.Abs(position.y - waterHeight) > 0.1f && Math.Abs(position.y + 1.5f - waterHeight) > 0.1f)
                    continue;

                if (IsInternal(entity) || IsBuildingGrounded(entity))
                    continue;

                entity.flags = (BaseEntity.Flags)ConvertFromOldFlagsExtended((OldFlagsExtended)entity.flags);
                changed++;
            }

            if (changed > 0)
                Instance.Puts($"Updated {changed} foundations to new flags");
        }

        private static void KillOldFlags()
        {
            var changed = 0;
            foreach (var entity in BaseNetworkable.serverEntities.OfType<BaseEntity>())
            {
                if (!(entity is StabilityEntity))
                    continue;

                if (!IsOldInternal(entity))
                    continue;

                var prefabName = entity.PrefabName;
                if (prefabName != PREFAB_FLOOR_SQUARE && prefabName != PREFAB_FLOOR_TRIANGLE)
                    continue;

                var position = entity.transform.position;
                var waterHeight = TerrainMeta.WaterMap.GetHeight(position);
                if (Math.Abs(position.y - waterHeight) > 0.1f && Math.Abs(position.y + 1.5f - waterHeight) > 0.1f)
                    continue;

                entity.Kill();
                changed++;
            }

            if (changed > 0)
                Instance.Puts($"Killed {changed} invalid old entities");
        }
        */
        private static void ActivateSpecialStuff()
        {
            foreach (var shark in BaseNetworkable.serverEntities.OfType<SimpleShark>())
            {
                //this will count already existing sharks
                Instance.OnEntitySpawned(shark);
            }

            foreach (var stability in BaseNetworkable.serverEntities.OfType<StabilityEntity>())
            {
                var buildingBlock = stability as BuildingBlock;

                if (buildingBlock == null)
                {
                    if (Instance.configData.UnderwaterNetsCollectJunk)
                    {
                        if (IsUnderwaterNetting(stability))
                        {
                            MonoiseUnderwaterNetting(stability);
                        }
                    }

                    continue;
                }

                if (!IsWaterFoundation(buildingBlock))
                {
                    if (IsInverseFoundation(buildingBlock))
                    {
                        MakeStable(buildingBlock);
                        MonoiseInverseFoundation(buildingBlock);
                        EnsureCorrectNumberOfBarrels(buildingBlock);
                    }
                    continue;
                }

                MakeStable(buildingBlock);
                EnsureCorrectNumberOfBarrels(buildingBlock);
            }
        }

        private static void ApplyUpkeepMultiplier(DecayEntity decayEntity)
        {
            //decayEntity.upkeep.upkeepMultiplier = Instance.configData.UpkeepMultiplier;
        }

        private static bool IsPositionOutsideMapGrid(Vector3 pos)
        {
            var mapHalfExtentMax = (TerrainMeta.Size.x) / 2F + Instance.configData.MapGridBoundsTolerance;

            var absX = Mathf.Abs(pos.x);

            if (absX > mapHalfExtentMax)
            {
                return true;
            }

            var absZ = Mathf.Abs(pos.z);

            if (absZ > mapHalfExtentMax)
            {
                return true;
            }

            return false;
        }

        private static float GetCoarseDistanceToShore(Vector3 pos) => TerrainMeta.Texturing.GetCoarseDistanceToShore(pos);
        private static float GetDistanceToNearestOilrig(Vector3 pos)
        {
            float minDistance = float.MaxValue;
            float curDistance;
            foreach (var entry in Instance.configData.OilrigPositions)
            {
                curDistance = Vector3.Distance(entry.Key, pos);
                if (curDistance < minDistance)
                {
                    minDistance = curDistance;
                }
            }
            return minDistance;
        }

        private static PermissionProfile GetPermissionProfile(BasePlayer player)
        {
            foreach (var prof in Instance.configData.PermissionProfiles)
            {
                if (prof.Key == "default") continue;

                if (Instance.permission.UserHasPermission(player.UserIDString, prof.Key))
                {
                    return prof.Value;
                }
            }

            //we got here so that means nothing found.

            if (!Instance.configData.PermissionProfiles.ContainsKey("default"))
            {
                //oops, data must be fucked.
                Instance.configData.PermissionProfiles.Add("default", Instance.DefaultPermissionProfile());
                Instance.SaveConfigData();
            }

            return Instance.configData.PermissionProfiles["default"];
        }

        private static void TellMessage(BasePlayer player, string message, bool forceInChatOnly = false, bool isErrorMessage = false)
        {
            bool shouldGoInChat = forceInChatOnly || !Instance.configData.PlayerMessagesAsToasts || player == null;

            if (shouldGoInChat)
            {
                string result = $"{MSG(MSG_CHAT_PREFIX)} {message}";

                if (player == null)
                {
                    Instance.PrintToChat(result);
                }
                else
                {
                    player.ChatMessage(result);
                }
            }
            else
            {
                //toast the player
                ToastPlayer(player, message, isErrorMessage ? GameTip.Styles.Red_Normal : GameTip.Styles.Blue_Long);
            }

        }

        public static int CountBuildingWaterFoundations(BuildingBlock blockInBuilding)
        {
            if (!IsInWaterBase(blockInBuilding)) return 0;

            var building = blockInBuilding.GetBuilding();
            if (building == null) return 0;

            var count = 0;

            foreach (var block in building.buildingBlocks)
            {
                if (IsWaterFoundation(block))
                {
                    count++;
                }
            }

            return count;
        }

        public static bool IsInWaterBase(DecayEntity decayEntity)
        {
            //remove from context - instead, AreFlagsRelevant
            if (IsInternal(decayEntity))
            {
                return true;
            }

            var maybeBuildingBlock = decayEntity as BuildingBlock;
            if (maybeBuildingBlock != null)
            {
                if (IsWaterFoundation(maybeBuildingBlock))
                {
                    return true;
                }
            }

            var maybeBuilding = decayEntity.GetBuilding();

            if (maybeBuilding != null)
            {
                foreach (var thisBlock in maybeBuilding.buildingBlocks)
                {
                    if (thisBlock.net.ID == decayEntity.net.ID)
                    {
                        continue;
                    }

                    if (IsInternal(thisBlock))
                    {
                        return true;
                    }

                    if (IsWaterFoundation(thisBlock))
                    {
                        return true;
                    }

                }
            }

            return false;
        }

        //easy, use 3 flags: for "is water foundation", "is inverse = true", "shape: triangle or square"
        public static bool IsInverseFoundation(BuildingBlock block)
        {
            var getType = GetSpecificType(block);

            switch (getType)
            {
                case InternalEntityType.FoundationSquareInversed:
                case InternalEntityType.FoundationTriangleInversed:
                    {
                        return true;
                    }
                default:
                    {
                        return false;
                    }
            }
        }

        //easy, use 3 flags: for "is water foundation", "is inverse = false", "shape: triangle or square"
        public static bool IsWaterFoundation(BuildingBlock block)
        {
            var getType = GetSpecificType(block);
            switch (getType)
            {
                case InternalEntityType.FoundationSquare:
                case InternalEntityType.FoundationTriangle:
                    {
                        return true;
                    }
                default:
                    {
                        return false;
                    }
            }
        }

        public static bool TopologyCheckMasks(int inputMask, int comparisonMask)
        {
            for (var bitPos = 0; bitPos<32; bitPos++)
            {
                int topologyValue = 2 ^ bitPos;

                int bitValueInput = inputMask & topologyValue;

                if (bitValueInput == 0)
                {
                    //can't have that

                    int bitValueComparison = comparisonMask & topologyValue;

                    if (bitValueComparison == 1)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public static int GetTopologyMaskFromDictionary(Dictionary<Topology, bool> input)
        {
            int result = 0;

            foreach (var entry in input)
            {
                result |= entry.Value ? (int)entry.Key : 0;
            }

            return result;
        }

        public static BuildingBlock TryGetAdjacentSpecialFoundation(BuildingBlock checker, out bool isInverse)
        {
            isInverse = false;

            float num = float.MaxValue;
            BuildingBlock result = null;
            Vector3 position = checker.PivotPoint();
            List<BuildingBlock> obj = Facepunch.Pool.Get<List<BuildingBlock>>();
            Vis.Entities(position, 3F, obj, /*2097152*/ConstructionMask);
            for (int i = 0; i < obj.Count; i++)
            {
                BuildingBlock buildingBlock = obj[i];

                if (buildingBlock == checker)
                {
                    continue;
                }

                if (!(buildingBlock.PrefabName == PREFAB_FLOOR_SQUARE || buildingBlock.PrefabName == PREFAB_FLOOR_TRIANGLE))
                {
                    continue;
                }


                //only take into account stuff with the same orientation
                if (Mathf.Abs(buildingBlock.transform.eulerAngles.z - buildingBlock.transform.eulerAngles.z) > 170F)
                {
                    continue;
                }

                if (Mathf.Abs(checker.transform.position.y - buildingBlock.transform.position.y) > 0.1F)
                {
                    continue;
                }

                if (!IsWaterFoundation(buildingBlock))
                {
                    if (!IsInverseFoundation(buildingBlock))
                    {
                        continue;
                    }
                    else
                    {
                        isInverse = true;
                    }
                }

                if (buildingBlock.isServer == checker.isServer)
                {
                    float num2 = buildingBlock.SqrDistance(position);
                    if (!buildingBlock.grounded)
                    {
                        num2 += 1f;
                    }
                    if (num2 < num)
                    {
                        num = num2;
                        result = buildingBlock;
                        break;
                    }
                }
            }
            Facepunch.Pool.FreeUnmanaged(ref obj);
            return result;
        }
        public static void EnsureFoundationItemName(Item item)
        {
            if (!IsSkinRelevant(item.skin)) return;

            var properSkinName = item.skin == ITEM_SKIN_FOUNDATION_SQUARE ? MSG(MSG_ITEM_NAME_SQUARE) : MSG(MSG_ITEM_NAME_TRIANGLE);

            if (item.name == properSkinName)
            {
                return;
            }

            item.name = properSkinName;
            item.MarkDirty();

        }

        public static Item MakeFoundationItem(ulong skin, int amount = 1)
        {
            if (!IsSkinRelevant(skin))
            {
                return null;
            }

            var newItem = ItemManager.CreateByItemID(ITEM_INNER_TUBE, amount, skin);

            EnsureFoundationItemName(newItem);

            return newItem;
        }

        //make sure this is only used in the context of items, not skins
        public static bool IsSkinRelevant(ulong skin)
        {
            return RelevantSkins.Contains(skin);
        }

        public static void MakeStable(BaseEntity entity, bool removeColliders = false)
        {
            //make it 100% stable, remove groundwatch and destroy on ground missing.
            var groundWatch = entity.gameObject.GetComponent<GroundWatch>();

            if (groundWatch != null)
            {
                UnityEngine.Object.DestroyImmediate(groundWatch);
            }

            var groundMissing = entity.gameObject.GetComponent<DestroyOnGroundMissing>();

            if (groundMissing != null)
            {
                UnityEngine.Object.DestroyImmediate(groundMissing);
            }

            var maybeStability = entity as StabilityEntity;
            if (maybeStability != null)
            {
                maybeStability.grounded = true;
            }

            if (removeColliders)
            {
                foreach (var colliderCompo in entity.gameObject.GetComponentsInChildren<Collider>())
                {
                    UnityEngine.Object.DestroyImmediate(colliderCompo);
                }
            }

        }

        public static void PostDeploymentChecks(BasePlayer player, BuildingBlock newWaterFoundation, bool deployedFromInflatable, out DeploymentResult result, int neededItemAmount = 1)
        {
            result = DeploymentResult.Success;

            if (Instance.configData.GlobalOceanLevelCheckBeforePlacing)
            {
                if (ConVar.Env.oceanlevel != Instance.configData.GlobalOceanLevelBaseline)
                {
                    result = DeploymentResult.OceanLevel;
                    return;
                }
            }

            if (player.IsBuildingBlocked())
            {
                result = DeploymentResult.BuildingBlocked;
                return;
            }

            if (Instance.configData.RestrictBuildingToMapGrid)
            {
                if (IsPositionOutsideMapGrid(newWaterFoundation.transform.position))
                {
                    result = DeploymentResult.OutsideMapGrid;
                    return;
                }
            }

            var prof = GetPermissionProfile(player);

            bool consume = false;
            List<Item> neededItems = null;

            if (deployedFromInflatable)
            {
                if (!prof.CanDeployWaterFoundations)
                {
                    result = DeploymentResult.NoPermissionToDeploy;
                    return;
                }

                //do a pebcak check. Do a Vis.Entites
                List<BuildingBlock> listToCheck = Facepunch.Pool.Get<List<BuildingBlock>>();

                Vis.Entities<BuildingBlock>(newWaterFoundation.transform.position, 2F, listToCheck, ConstructionMask, QueryTriggerInteraction.Ignore);

                //you're gonna find yourself, so iterate...

                bool foundSomethingOtherThanYou = false;
                BuildingBlock currentBlockChecked;

                for (var f = 0; f<listToCheck.Count; f++)
                {
                    currentBlockChecked = listToCheck[f];

                    //ignore inverse

                    if (currentBlockChecked.EqualNetID(newWaterFoundation))
                    {
                        //that's you.
                        continue;
                    }


                    foundSomethingOtherThanYou = true;
                    break;
                }

                if (foundSomethingOtherThanYou)
                {
                    result = DeploymentResult.ProblemExistsBetweenChairAndKeyboard;
                    return;
                }

                Facepunch.Pool.FreeUnmanaged(ref listToCheck);
            }
            else
            {
                if (!prof.CanExpandWaterFoundations)
                {
                    result = DeploymentResult.NoPermissionToExpand;
                    return;
                }

                if (neededItemAmount > 0)
                {
                    neededItems = FindNeededFoundationItemsInInventory(player, newWaterFoundation, neededItemAmount);

                    if (neededItems == null)
                    {
                        result = DeploymentResult.NoRequiredItem;
                        return;
                    }
                    else
                    {
                        consume = true;
                    }
                }

            }

            if (!TopologyCheckMasks(Instance.configData.ProfileTopologyMask[prof.PermissionRequired], TerrainMeta.TopologyMap.GetTopologyFast(new Vector2(newWaterFoundation.transform.position.x, newWaterFoundation.transform.position.z))))
            {
                result = DeploymentResult.TopologyMismatch;
                return;
            }

            var depth = WaterLevel.GetOverallWaterDepth(newWaterFoundation.transform.position, false, false);

            if (prof.WaterDepthMin != float.NegativeInfinity)
            {
                if (depth < prof.WaterDepthMin)
                {
                    result = DeploymentResult.WaterTooShallow;
                    return;
                }
            }

            if (prof.WaterDepthMax != float.PositiveInfinity)
            {
                if (depth > prof.WaterDepthMax)
                {
                    result = DeploymentResult.WaterTooDeep;
                    return;
                }
            }

            if (Instance.configData.RelyOnShoreDistance)
            {
                var distanceToShore = GetCoarseDistanceToShore(newWaterFoundation.transform.position);
                if (prof.MinDistanceFromShore != float.NegativeInfinity)
                {
                    if (distanceToShore < prof.MinDistanceFromShore)
                    {
                        result = DeploymentResult.TooCloseToShore;
                        return;
                    }
                }

                if (prof.MaxDistanceFromShore != float.PositiveInfinity)
                {
                    if (distanceToShore > prof.MaxDistanceFromShore)
                    {
                        result = DeploymentResult.TooFarFromShore;
                        return;
                    }
                }
            }


            var isInWaterVolume = false;
            var hits = Physics.RaycastAll(newWaterFoundation.transform.position, Vector3.down, 3F, DeployedMask);

            BaseEntity ent;
            RaycastHit hit;

            if (hits != null)
            {
                for (var h = 0; h < hits.Length; h++)
                {
                    hit = hits[h];
                    ent = hit.GetEntity();
                    if (ent == null)
                    {
                        continue;
                    }

                    if (ent.PrefabName == PREFAB_POOL_BIG || ent.PrefabName == PREFAB_POOL_SMALL)
                    {
                        isInWaterVolume = true;
                        break;
                    }
                }
            }

            if (isInWaterVolume)
            {
                result = DeploymentResult.DeployedInPool;
                return;
            }

            if (prof.MinDistanceFromOilrig != float.NegativeInfinity)
            {
                if (GetDistanceToNearestOilrig(newWaterFoundation.transform.position) < prof.MinDistanceFromOilrig)
                {
                    result = DeploymentResult.TooCloseToOilrig;
                    return;
                }
            }

            if (prof.MaxWaterFoundationsPerBuilding != int.MaxValue)
            {
                if (CountBuildingWaterFoundations(newWaterFoundation) > prof.MaxWaterFoundationsPerBuilding)
                {
                    result = DeploymentResult.TooManyFoundations;
                    return;
                }
            }

            if (Instance.configData.CargoShipPathHandling != CargoPathHandling.None)
            {
                if (TerrainMeta.Path.OceanPatrolFar?.Count > 0)
                {
                    foreach (var oceanNode in TerrainMeta.Path.OceanPatrolFar)
                    {
                        if (Vector3Ex.Distance2D(oceanNode, newWaterFoundation.transform.position) < Instance.configData.MinDistanceFromCargoShipNode)
                        {
                            result = DeploymentResult.OnCargoShipPath;
                            return;
                        }
                    }
                }
            }

            if (consume)
            {
                if (neededItems != null)
                {
                    ConsumeFromSpecificItemList(ref neededItems, neededItemAmount);
                }
            }
        }

        public static void TurnFloorIntoInverseFoundation(BuildingBlock inverseFloor, BuildingBlock waterFoundationAbove, BasePlayer buildingPlayer)
        {
            MakeStable(inverseFloor);

            var getTypeAbove = GetSpecificType(waterFoundationAbove);

            if (getTypeAbove == InternalEntityType.FoundationSquare)
            {
                MarkAsInternalFoundationSquareInversed(inverseFloor);
            }
            else
            {
                MarkAsInternalFoundationTriangleInversed(inverseFloor);
            }

            inverseFloor.SetParent(waterFoundationAbove, true, true);

            MonoiseInverseFoundation(inverseFloor);

            //are post deployment checks even needed?

            EnsureCorrectNumberOfBarrels(inverseFloor);
        }

        public static void TurnFloorIntoWaterFoundation(BuildingBlock floor, BasePlayer buildingPlayer, bool deployedFromInflatable)
        {
            MakeStable(floor);
            //parent some barrels on the bottom, make those stable.

            DeploymentResult result;
            PostDeploymentChecks(buildingPlayer, floor, deployedFromInflatable, out result);

            bool actuallySuccess = false;

            if (result != DeploymentResult.Success)
            {
                //if the result is OnCargoShip, but the handling is Warn, don't kill
                if (result == DeploymentResult.OnCargoShipPath && Instance.configData.CargoShipPathHandling == CargoPathHandling.Warn)
                {
                    actuallySuccess = true;
                }
            }
            else
            {
                actuallySuccess = true;
            }

            if (!actuallySuccess)
            {
                TellMessage(buildingPlayer, MSG_RESULT(result, buildingPlayer.UserIDString), false, true);

                if (deployedFromInflatable)
                {
                    //inverse foundation give you a double refund
                    buildingPlayer.GiveItem(MakeFoundationItem(floor.PrefabName == PREFAB_FLOOR_SQUARE ? ITEM_SKIN_FOUNDATION_SQUARE : ITEM_SKIN_FOUNDATION_TRIANGLE, IsInverseFoundation(floor) ? 2 : 1));
                }

                DelayedKillWithGibs(floor);
                //floor.Kill(BaseNetworkable.DestroyMode.Gib);
            }
            else
            {
                //must be warning, so still display
                if (result == DeploymentResult.OnCargoShipPath)
                {
                    TellMessage(buildingPlayer, MSG_RESULT(result, buildingPlayer.UserIDString), false, true);
                }
                //now if success spawn some stuff

                EnsureCorrectNumberOfBarrels(floor);

                ApplyUpkeepMultiplier(floor);
            }
        }

        public static BuildingBlock BuildInverseFoundationAtWaterFoundation(BasePlayer deployingPlayer, BuildingBlock waterFoundation)
        {

            var transf = GetWorldTransformRelativeTo(waterFoundation.transform, Vector3.down * 1.5F, new Vector3(0F, 0F, 180F));
            //spawn either a square or triangle floor...

            string prefabName;

            var getTypeAbove = GetSpecificType(waterFoundation);

            if (getTypeAbove == InternalEntityType.FoundationSquare)
            {
                prefabName = PREFAB_FLOOR_SQUARE;
            }
            else
            {
                prefabName = PREFAB_FLOOR_TRIANGLE;
            }

            BuildingBlock inverseFloor = GameManager.server.CreateEntity(prefabName, transf.Item1, transf.Item2) as BuildingBlock;

            inverseFloor.OwnerID = deployingPlayer.userID;

            inverseFloor.AttachToBuilding(waterFoundation);

            inverseFloor.Spawn();
            inverseFloor.SetHealthToMax();

            Effect.server.Run(PREFAB_BUILD_EFFECT, inverseFloor, 0u, Vector3.zero, Vector3.zero);

            TurnFloorIntoInverseFoundation(inverseFloor, waterFoundation, deployingPlayer);

            return inverseFloor;
        }

        public static BuildingBlock BuildWaterFoundationAt(BasePlayer deployingPlayer, bool deployedFromInflatable, ulong foundationSkin, Vector3 position, Quaternion rotation)
        {
            if (!IsSkinRelevant(foundationSkin))
            {
                return null;
            }

            //spawn either a square or triangle floor...
            BuildingBlock newFloor = GameManager.server.CreateEntity(foundationSkin == ITEM_SKIN_FOUNDATION_SQUARE ? PREFAB_FLOOR_SQUARE : PREFAB_FLOOR_TRIANGLE, position, rotation) as BuildingBlock;

            newFloor.OwnerID = deployingPlayer.userID;

            if (foundationSkin == ITEM_SKIN_FOUNDATION_SQUARE)
            {
                MarkAsInternalFoundationSquare(newFloor);
            }
            else
            {
                MarkAsInternalFoundationTriangle(newFloor);
            }

            newFloor.AttachToBuilding(BuildingManager.server.NewBuildingID());

            newFloor.Spawn();
            newFloor.SetHealthToMax();

            Effect.server.Run(PREFAB_BUILD_EFFECT, newFloor, 0u, Vector3.zero, Vector3.zero);

            //var building = new BuildingManager.Building { ID = newFloor.buildingID };

            //BuildingManager.server.buildingDictionary.Add(newFloor.buildingID, building);

            //building.AddBuildingBlock(newFloor);
            //building.Dirty();

            TurnFloorIntoWaterFoundation(newFloor, deployingPlayer, deployedFromInflatable);
            return newFloor;
        }
        #endregion

        #region API
        [HookMethod(nameof(CheckIfInsideWaterBase))]
        public object CheckIfInsideWaterBase(DecayEntity entity)
        {
            if (entity == null)
            {
                return null;
            }

            return IsInWaterBase(entity);
        }
        #endregion

    }
}
