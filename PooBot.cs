using System;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Facepunch;
using Rust;
using Oxide.Game.Rust.Cui;


namespace Oxide.Plugins
{
    [Info("PooBot", "https://discord.gg/TrJ7jnS233", "1.1.1")]
    [Description("This plugin was fixed by Инкуб to order [Rust Plugin Sliv]: https://discord.gg/pFgKw6Dyyq")]
    class PooBot : RustPlugin
    {

        //fix for entity movement jerks
        //fixed rocket speed
        //fixed rockets working
        //changed looks to prevent culling of parts as much as possible

        #region Load

        BaseEntity robotEntity;

        void Loaded()
        {
            LoadVariables();
            permission.RegisterPermission("poobot.spawn", this);
            permission.RegisterPermission("poobot.craft", this);
            permission.RegisterPermission("poobot.repack", this);
            data = Interface.Oxide.DataFileSystem.GetFile("poobot_data");
        }

        #endregion

        #region Configuration

        static bool AllowNormalDamage = false;
        static bool AllowPlayerDamage = false;
        static bool DoKnockbackEffect = true;
        static bool AllowResourceGather = true;
        static bool RequireFuel = false;
        static bool AllowRepackLowHealth = false;
        static bool AllowRepackUnderCooldown = false;

        static int FuelUsagePerTick = 5;
        static float FuelTickRate = 60;
        static int FuelItemID = -946369541;

        static int WoodGatherAmount = 100;
        static int WoodGatherCycles = 5;

        static int OreGatherAmount = 100;
        static int OreGatherCycles = 5;

        static int CraftItemAmount = 10000;
        static int CraftItemID = 69511070;

        static float RepairAmountPerHit = 1000f;
        static int RepairItemAmountNeeded = 100;
        static int RepairItemID = 69511070;

        static float RobotStartHealth = 10000f;
        static float MinRepairHealth = 2000f;

        static float PunchDamageRadius = 1.3f;
        static float RocketReuseTime = 120f;
        static float DamageToNonRobots = 50f;
        static float DamageToRobots = 500f;
        static float ConditionLossByHittingRobots = 10f;
        static float ConditionLossByHittingNonRobots = 50f;

        static bool EnabledNonRobotConditionLoss = false;
        static bool EnableRobotConditionLoss = false;

        static int KnockdownChance = 5;

        string messagetxt1 = "You Cannot repack Robot when Health is not Full";
        string messagetxt2 = "You Cannot repack Robot if weapons cooldown is active";
        string messagetxt3 = "Repacking robot puts it back in your inventory as a item";
        string messagetxt4 = "Press your RELOAD key with arm extended to Fire Weapons";
        string messagetxt5 = "Spar Mode allows you to practice fighting a robot by yourself";
        string messagetxt6 = "Jump out of seat to dismount Robot";
        string messagetxt7 = "Damage to player is redirected to Robot when seated in Robot";
        string messagetxt8 = "Robots can gather wood and ore if enabled by admin";
        string messagetxt9 = "Robots can attack players, npcs, buildings, etc... if enabled by admin";
        string messagetxt10 = "Hitting robot with hammer and proper materials will repair robot";

        bool Changed;

        void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            Config.Clear();
            LoadVariables();
        }

        void LoadConfigVariables()
        {

            CheckCfg("Global - Damage - Allow Robot to damage players/buildings ? ", ref AllowNormalDamage);
            CheckCfg("Global - Damage - Allow Player to damage players/buildings while operating Poobot (weapons on player)? ", ref AllowPlayerDamage);
            CheckCfg("Global - Do Knockback effects when robots hit each other ", ref DoKnockbackEffect);
            CheckCfg("Global - Allow Robot to gather resources from Tree/Ore ", ref AllowResourceGather);
            CheckCfg("Global - Require Fuel to run Robot ?", ref RequireFuel);
            CheckCfg("Global - Allow Robot Repack if Robot Health is not 100% ", ref AllowRepackLowHealth);
            CheckCfg("Global - Allow Robot Repack if Robot is under Weapons reload Cooldown ", ref AllowRepackUnderCooldown);

            CheckCfg("Fuel - Item ID of Fuel needed to run Robot ", ref FuelItemID);
            CheckCfg("Fuel - Amount of fuel needed per tick to run Robot ", ref FuelUsagePerTick);
            CheckCfgFloat("Fuel - Seconds between fuel ticks ", ref FuelTickRate);

            CheckCfg("Global - Enable Condition Loss when hitting other Robots ? ", ref EnableRobotConditionLoss);
            CheckCfg("Global - Enable Condition Loss when hitting other Non Robots (players, buildings, ets..) ? ", ref EnabledNonRobotConditionLoss);

            CheckCfg("Gather - Wood - Amount gathered per cycle when hitting Trees ", ref WoodGatherAmount);
            CheckCfg("Gather - Ore - Amount gathered per cycle when hitting Ore nodes ", ref OreGatherAmount);
            CheckCfg("Gather - Wood - Number of cycles before Tree is empty ", ref WoodGatherCycles);
            CheckCfg("Global - Ore - Number of cycles before Ore nodes are empty ", ref OreGatherCycles);

            CheckCfgFloat("Health - Starting / Max Health of all Robots ", ref RobotStartHealth);
            CheckCfgFloat("Health - Minimum Health needed when repairing to stand robot back up ", ref MinRepairHealth);

            CheckCfg("Craft - Amount of Materials needed to craft a full robot ", ref CraftItemAmount);
            CheckCfg("Craft - Item ID of the material needed to craft robot (default Metal Fragments) ", ref CraftItemID);

            CheckCfgFloat("Repair - Amount of health restored by one hit when repairing ", ref RepairAmountPerHit);
            CheckCfg("Repair - Amount of Materials needed to repair per hit ", ref RepairItemAmountNeeded);
            CheckCfg("Repair - Item ID of the material needed to repair (default Metal Fragments) ", ref RepairItemID);

            CheckCfgFloat("Punch - Radius - how wide of a fist will the robot have when punching and looking for something to damage ", ref PunchDamageRadius);
            CheckCfgFloat("Punch - Damge - Damage to Non robots entities when being hit by robot punch ", ref DamageToNonRobots);
            CheckCfgFloat("Punch - Damage - Damage to Robots when hit by other robots ", ref DamageToRobots);

            CheckCfgFloat("Punch - Condition Loss - How much condidtion loss damage will robot take when punching other robots ", ref ConditionLossByHittingRobots);
            CheckCfgFloat("Punch - Condition Loss - How much condidtion loss damage will robot take when punching NON robots (players, buildings..etc) ", ref ConditionLossByHittingNonRobots);

            CheckCfg("Punch - Knowndown - Chances a landed robot punch will knock down a roboat for a few seconds ", ref KnockdownChance);

            CheckCfgFloat("Rocket - Cooldown - How long must player wait to reuse the rockets on the robot ", ref RocketReuseTime);
        }

        void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        void CheckCfgFloat(string Key, ref float var)
        {

            if (Config[Key] != null)
                var = System.Convert.ToSingle(Config[Key]);
            else
                Config[Key] = var;
        }

        void CheckCfgUlong(string Key, ref ulong var)
        {

            if (Config[Key] != null)
                var = Convert.ToUInt64(Config[Key]);
            else
                Config[Key] = var;
        }

        #endregion

        #region Data

        static List<ulong> storedPooBots = new List<ulong>();
        private DynamicConfigFile data;
        private bool initialized;

        private void OnServerInitialized()
        {
            initialized = true;
            LoadData();
            timer.In(3, RestorePooBots);
        }
        private void OnServerSave()
        {
            SaveData();
        }

        private void RestorePooBots()
        {
            if (storedPooBots.Count > 0)
            {
                BaseEntity[] objects = BaseEntity.saveList.Where(x => x is BaseEntity).ToArray();
                if (objects != null)
                {
                    foreach (var obj in objects)
                    {
                        if (!obj.IsValid() || obj.IsDestroyed)
                            continue;

                        if (storedPooBots.Contains(obj.net.ID.Value))
                        {
                            var spawnpos = obj.transform.position;
                            var spawnrot = obj.transform.rotation;
                            var userid = obj.OwnerID;

                            storedPooBots.Remove(obj.net.ID.Value);
                            obj.Invoke("KillMessage", 0.1f);
                            timer.Once(2f, () => RespawnPooBot(spawnpos, spawnrot, userid));
                        }
                    }
                }
                PrintWarning("All Saved PooBots have be respawned.");
            }
        }

        void SaveData() => data.WriteObject(storedPooBots.ToList());
        void LoadData()
        {
            try
            {
                storedPooBots = data.ReadObject<List<ulong>>();
            }
            catch
            {
                storedPooBots = new List<ulong>();
            }
        }

        #endregion

        #region Commands

        [ChatCommand("poobot")]
        void chatPooBot(BasePlayer player, string command, string[] args)
        {
            SendReply(player, messagetxt1);
            SendReply(player, messagetxt2);
            SendReply(player, messagetxt3);
            SendReply(player, messagetxt4);
            SendReply(player, messagetxt5);
            SendReply(player, messagetxt6);
            SendReply(player, messagetxt7);
            SendReply(player, messagetxt8);
            SendReply(player, messagetxt9);
            SendReply(player, messagetxt10);

        }

        [ChatCommand("poobot.craft")]
        void chatPooBotCraft(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "poobot.craft")) { SendReply(player, "You do not have permssion to use that command"); return; }
            CraftPooBot(player);
        }

        [ChatCommand("poobot.spawn")]
        void chatPooBotSpawn(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "poobot.spawn")) { SendReply(player, "You do not have permssion to use that command"); return; }
            if (player.IsOnGround())
            {
                SpawnPooBotOnPlayer(player);
                return;
            }
            else
            {
                SendReply(player, "You must be standing on ground to spawn a Poobot !!");
            }
        }

        [ConsoleCommand("givepoobot")]
        void cmdConsolePoobotGive(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player != null)
            {
                if (!permission.UserHasPermission(player.UserIDString, "poobot.spawn")) return;
                GivePooBot(player);
                return;
            }
            if (arg.Args.Length > 0)
            {
                ulong id = Convert.ToUInt64(arg.Args[0]);
                GivePooBot(BasePlayer.FindByID(id));
            }
        }

        [ChatCommand("poobot.repack")]
        void chatPooBotRepack(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "poobot.repack")) { SendReply(player, "You do not have permssion to use that command"); return; }
            if (!player.isMounted) return;
            var poobot = player.GetMounted().GetComponentInParent<PooBotEntity>();
            if (poobot == null) return;
            if (poobot.botentity.OwnerID != player.userID) return;
            if (!AllowRepackLowHealth && poobot.health < RobotStartHealth) { SendInfoMessage(player, "<color=red>Cannot repack Robot when Health is not Full !!</color>", 10f); return; }
            if (!AllowRepackUnderCooldown && !poobot.canfireleft || !poobot.canfireright) { SendInfoMessage(player, "<color=red>Cannot repack Robot with a Weapons Cooldown active !!</color>", 10f); return; }
            DestroyStaticCui(player);
            DestroyRefreshCui(player);
            if (storedPooBots.Contains(poobot.botentity.net.ID.Value))
            {
                storedPooBots.Remove(poobot.botentity.net.ID.Value);
                SaveData();
            }
            GameObject.Destroy(poobot);
            GivePooBot(player);
        }

        [ConsoleCommand("poobotrepack")]
        void cmdConsolePooBotRepack(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player != null)
            {
                if (!permission.UserHasPermission(player.UserIDString, "poobot.repack")) { SendReply(player, "You do not have permssion to use that command"); return; }
                if (!player.isMounted) return;
                var poobot = player.GetMounted().GetComponentInParent<PooBotEntity>();
                if (poobot == null) return;
                if (poobot.botentity.OwnerID != player.userID) return;
                if (!AllowRepackLowHealth && poobot.health < RobotStartHealth) { SendInfoMessage(player, "<color=red>Cannot repack Robot when Health is not Full !!</color>", 10f); return; }
                if (!AllowRepackUnderCooldown && !poobot.canfireleft || !poobot.canfireright) { SendInfoMessage(player, "<color=red>Cannot repack Robot with a Weapons Cooldown active !!</color>", 10f); return; }
                DestroyStaticCui(player);
                DestroyRefreshCui(player);
                if (storedPooBots.Contains(poobot.botentity.net.ID.Value))
                {
                    storedPooBots.Remove(poobot.botentity.net.ID.Value);
                    SaveData();
                }
                GameObject.Destroy(poobot);
                GivePooBot(player);
            }
        }


        [ConsoleCommand("poobotswapweapon")]
        void cmdConsolePooBotSwapWeapon(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player != null)
            {
                if (!player.isMounted) return;
                var poobot = player.GetMounted().GetComponentInParent<PooBotEntity>();
                if (poobot == null) return;
                SendInfoMessage(player, "Robot Weapon Swap Feature Coming soon !!", 10f);
            }
        }

        [ConsoleCommand("poobotactivatesentry")]
        void cmdConsolePooBotSentryMode(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player != null)
            {
                if (!player.isMounted) return;
                var poobot = player.GetMounted().GetComponentInParent<PooBotEntity>();
                if (poobot == null) return;

                if (!poobot.activatesentry) { poobot.activatesentry = true; poobot.RobotButtons(player); SendInfoMessage(player, "Robot Sentry Mode Coming Soon !!", 10f); return; }
                else { poobot.activatesentry = false; poobot.StopPooBot(); poobot.RobotButtons(player); }
            }
        }

        [ChatCommand("poobot.dismount")]
        void chatPooBotDisMount(BasePlayer player, string command, string[] args)
        {
            DestroyStaticCui(player);
            DestroyRefreshCui(player);
            var dismountloc = player.transform.position + (Vector3.down * 2f) + (Vector3.forward);
            player?.EnsureDismounted();
            player.MovePosition(dismountloc);
            player.SendNetworkUpdateImmediate(false);
            player.ClientRPCPlayer<Vector3>(null, player, "ForcePositionTo", dismountloc);
            Interface.CallHook("OnEntityDismounted", this, player);
        }

        [ChatCommand("poobot.repair")]
        void chatPooBotRepair(BasePlayer player, string command, string[] args)
        {
            if (!player.isMounted) return;
            var poobot = player.GetMounted().GetComponentInParent<PooBotEntity>();
            if (poobot == null) return;
            if (!permission.UserHasPermission(player.UserIDString, "poobot.spawn")) return;
            poobot.RestoreHealth();
        }

        [ChatCommand("poobot.activatenpc")]
        void chatPooBotActivateNPC(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "poobot.spawn")) { SendReply(player, "You do not have permssion to use that command"); return; }
            if (!player.isMounted) return;
            var poobot = player.GetMounted().GetComponentInParent<PooBotEntity>();
            if (poobot == null) return;
            if (!poobot.activatenpc) { poobot.activatenpc = true; poobot.RobotButtons(player); SendInfoMessage(player, "Robot Spar Mode Activated", 10f); return; }
            else { poobot.activatenpc = false; poobot.StopPooBot(); poobot.RobotButtons(player); }
        }

        [ConsoleCommand("poobotactivatenpc")]
        void cmdConsolePoobotActivateNPC(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player != null)
            {
                if (!permission.UserHasPermission(player.UserIDString, "poobot.spawn")) { SendReply(player, "You do not have permssion to use that command"); return; }
                if (!player.isMounted) return;
                var poobot = player.GetMounted().GetComponentInParent<PooBotEntity>();
                if (poobot == null) return;
                if (!poobot.activatenpc) { poobot.activatenpc = true; poobot.RobotButtons(player); SendInfoMessage(player, "Robot Spar Mode Activated", 10f); return; }
                else { poobot.activatenpc = false; poobot.StopPooBot(); poobot.RobotButtons(player); }
            }
        }

        #endregion

        #region Hooks

        private void SpawnPooBotOnPlayer(BasePlayer player)
        {
            Vector3 startloc = player.transform.position;
            float groundy = TerrainMeta.HeightMap.GetHeight(startloc);
            string sphereprefab = "assets/prefabs/deployable/chair/chair.deployed.prefab";
            robotEntity = GameManager.server.CreateEntity(sphereprefab, new Vector3(startloc.x, groundy + 4.1f, startloc.z), player.transform.rotation, true);
            var mount = robotEntity.GetComponent<BaseMountable>();
            mount.isMobile = true;
            robotEntity.OwnerID = player.userID;
            robotEntity.skinID = 112233;
            robotEntity.Spawn();
            var addbody = robotEntity.gameObject.AddComponent<PooBotEntity>();
            storedPooBots.Add(robotEntity.net.ID.Value);
            SaveData();
        }

        private void RespawnPooBot(Vector3 spawnpos, Quaternion spawnrot, ulong userid)
        {
            Vector3 startloc = spawnpos;
            float groundy = TerrainMeta.HeightMap.GetHeight(startloc);
            string sphereprefab = "assets/prefabs/deployable/chair/chair.deployed.prefab";
            robotEntity = GameManager.server.CreateEntity(sphereprefab, new Vector3(startloc.x, groundy + 4.1f, startloc.z), spawnrot, true);
            var mount = robotEntity.GetComponent<BaseMountable>();
            mount.isMobile = true;
            robotEntity.OwnerID = userid;
            robotEntity.skinID = 112233;
            robotEntity.Spawn();
            var addbody = robotEntity.gameObject.AddComponent<PooBotEntity>();
            storedPooBots.Add(robotEntity.net.ID.Value);
            SaveData();
        }

        private object CanPickupEntity(BasePlayer player, BaseCombatEntity entity)
        {
            if (entity == null || player == null) return null;
            var ispoobot = entity.GetComponentInParent<PooBotEntity>() ?? null;
            if (ispoobot != null) return false;
            return null;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;
            if (storedPooBots.Contains(entity.net.ID.Value))
            {
                storedPooBots.Remove(entity.net.ID.Value);
                SaveData();
            }
        }

        private void CraftPooBot(BasePlayer player)
        {
            if (!TakeItem1Mats(player, CraftItemID, CraftItemAmount))
            {
                ItemDefinition itemDefinition = ItemManager.FindItemDefinition(CraftItemID);
                { SendReply(player, "You need " + CraftItemAmount.ToString() + " " + itemDefinition.shortname + "to craft a Robot !!"); return; }
            }
            GivePooBot(player);
        }

        private void GivePooBot(BasePlayer player)
        {
            var item = ItemManager.CreateByItemID(833533164, 1, 1428017435);
            player.inventory.GiveItem(item);
        }

        private void OnEntityBuilt(Planner plan, GameObject obj)
        {
            var entity = obj.GetComponent<BaseEntity>();
            if (entity != null && entity.ShortPrefabName == "box.wooden.large")
            {
                if (entity.GetComponent<BaseEntity>().skinID == 1428017435L)
                {
                    BasePlayer player = plan.GetOwnerPlayer();

                    if (player != null && player.IsOnGround())
                    {
                        ServerMgr.Instance.StartCoroutine(PoobotSpawnProcess(player, entity));
                        return;
                    }
                    else
                    {
                        SendReply(player, "You Must deploy the Poobot on ground !!!!");
                        timer.Once(1f, () => DestroyBox(entity));
                        GivePooBot(player);
                    }

                }
            }
        }

        private IEnumerator PoobotSpawnProcess(BasePlayer player, BaseEntity entity)
        {
            yield return new WaitForSeconds(0.5f);
            entity.Kill();
            yield return new WaitForSeconds(0.5f);
            SpawnPooBotOnPlayer(player);
        }

        private void DestroyBox(BaseEntity boxEntity)
        {
            if (boxEntity != null)
            {
                boxEntity.Kill();
            }
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            var ispoobot = entity.GetComponentInParent<PooBotEntity>();
            if (ispoobot) MountPooBot(player, ispoobot);
        }

        private void MountPooBot(BasePlayer player, PooBotEntity poobot)
        {
            if (!poobot.botentity.GetComponent<BaseMountable>()._mounted) poobot.botentity.GetComponent<BaseMountable>().MountPlayer(player);
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!player.isMounted || input == null) return;
            var poobot = player.GetMounted().GetComponentInParent<PooBotEntity>();
            if (poobot)
            {
                if (player.isMounted && input.IsDown(BUTTON.FORWARD)) poobot.moveforward = true;
                else poobot.moveforward = false;

                if (player.isMounted && input.IsDown(BUTTON.BACKWARD)) poobot.movebackward = true;
                else poobot.movebackward = false;

                if (player.isMounted && input.IsDown(BUTTON.SPRINT)) poobot.sprinting = true;
                else poobot.sprinting = false;

                if (input.IsDown(BUTTON.RIGHT)) poobot.rotright = true;
                else poobot.rotright = false;

                if (input.IsDown(BUTTON.LEFT)) poobot.rotleft = true;
                else poobot.rotleft = false;

                if (input.WasJustPressed(BUTTON.FIRE_PRIMARY)) poobot.moveleftarm = true;
                if (input.WasJustReleased(BUTTON.FIRE_PRIMARY)) { poobot.moveleftarm = false; poobot.lefthandready = true; }
                if (input.WasJustPressed(BUTTON.FIRE_SECONDARY)) poobot.moverightarm = true;
                if (input.WasJustReleased(BUTTON.FIRE_SECONDARY)) { poobot.moverightarm = false; poobot.righthandready = true; }
                if (input.WasJustPressed(BUTTON.FIRE_THIRD)) poobot.docharge = true;
                if (input.WasJustPressed(BUTTON.RELOAD)) FireOrReloadRockets(player, poobot);
                if (input.WasJustReleased(BUTTON.RELOAD)) poobot.fireweapon = false;
                if (input.WasJustPressed(BUTTON.DUCK)) poobot.docrouch = true;
                if (input.WasJustReleased(BUTTON.DUCK)) poobot.docrouch = false;

                if (input == null)
                {
                    poobot.moveforward = false;
                    poobot.movebackward = false;
                    poobot.sprinting = false;
                    poobot.rotright = false;
                    poobot.rotleft = false;
                    poobot.moveleftarm = false;
                    poobot.moverightarm = false;
                    poobot.fireweapon = false;
                }
            }
        }

        private void FireOrReloadRockets(BasePlayer player, PooBotEntity pooBot)
        {
            if (!pooBot.moveleftarm && !pooBot.moverightarm) { SendReply(player, "Robot arm must be extended to fire Rockets !!"); return; }
            if ((pooBot.canfireleft && pooBot.moveleftarm) || (pooBot.canfireright && pooBot.moverightarm))
            {
                if (TakeItem1Mats(player, -742865266, 1)) { pooBot.fireweapon = true; pooBot.rocketprefab = "assets/prefabs/ammo/rocket/rocket_basic.prefab"; return; }
                if (TakeItem1Mats(player, 1638322904, 1)) { pooBot.fireweapon = true; pooBot.rocketprefab = "assets/prefabs/ammo/rocket/rocket_fire.prefab"; return; }
                if (TakeItem1Mats(player, -1841918730, 1)) { pooBot.fireweapon = true; pooBot.rocketprefab = "assets/prefabs/ammo/rocket/rocket_hv.prefab"; return; }
                else { SendReply(player, "You need to have Rocket Ammo in your inventory"); pooBot.fireweapon = false; return; }
            }
            else
                SendReply(player, "Rocket launcher is under cooldown.. please wait !!");
        }

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            var entity = info.HitEntity;
            var ispoobot = entity.GetComponentInParent<PooBotEntity>();
            if (!ispoobot) return;
            if (ispoobot)
            {
                RepairRobot(player, ispoobot);
            }
        }

        private void RepairRobot(BasePlayer player, PooBotEntity pooBot)
        {
            var currenthealth = pooBot.health;
            if (currenthealth >= RobotStartHealth) { SendReply(player, "Robot is at Max Health Already !!"); return; }
            if (pooBot.botdefeated && currenthealth >= MinRepairHealth) { pooBot.botdefeated = false; pooBot.StandBotUp(); }
            if (TakeItem1Mats(player, RepairItemID, RepairItemAmountNeeded))
            {
                pooBot.health = currenthealth + RepairAmountPerHit;
                SendReply(player, "Robot repaired... Current health is : " + pooBot.health);
            }
            else
                SendReply(player, "You do not have the required materials to repair this Robot !!");
        }

        public bool HasRockets(BasePlayer player)
        {
            int HasReq1 = player.inventory.GetAmount(-742865266);
            int HasReq2 = player.inventory.GetAmount(1638322904);
            int HasReq3 = player.inventory.GetAmount(-1841918730);
            if (HasReq1 > 0 || HasReq2 > 0 || HasReq3 > 0) return true;
            return false;
        }

        private bool TakeItem1Mats(BasePlayer player, int itemID, int itemamount)
        {
            int HasReq1 = player.inventory.GetAmount(itemID);

            if (HasReq1 >= itemamount)
            {
                player.inventory.Take(null, itemID, itemamount);
                player.Command("note.inv", itemID, -itemamount);
                return true;
            }
            return false;
        }

        private void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            if (player == null) return;
            DestroyStaticCui(player);
            DestroyRefreshCui(player);
        }

        private void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            if (player == null || mountable == null) return;
            var poobot = mountable.GetComponentInParent<PooBotEntity>();
            if (poobot == null) return;
            GiveRandomHelpMessage(player);
            poobot.RobotButtons(player);
        }

        private object OnEntityGroundMissing(BaseEntity entity)
        {
            var isbot = entity.GetComponentInParent<PooBotEntity>() ?? null;
            if (isbot != null) return false;
            return null;
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null) return;
            if (!AllowPlayerDamage && hitInfo.Initiator is BasePlayer)
            {
                var attacker = (BasePlayer)hitInfo.Initiator ?? null;
                if (attacker != null && !(entity is BaseNpc))
                {
                    var inpoobot = attacker.GetMounted()?.GetComponentInParent<PooBotEntity>() ?? null;
                    if (inpoobot) hitInfo.damageTypes.ScaleAll(0);
                    return;
                }
            }
            if (hitInfo.HitEntity is BasePlayer)
            {
                var victim = (BasePlayer)hitInfo.HitEntity;
                if (!victim.isMounted) return;
                var poobot = victim.GetMounted().GetComponentInParent<PooBotEntity>();
                if (poobot == null) return;
                poobot.health = poobot.health - Convert.ToSingle(hitInfo.damageTypes.Total());
                hitInfo.damageTypes.ScaleAll(0);
                return;
            }
            var ispoobot = entity.GetComponentInParent<PooBotEntity>() ?? null;
            if (ispoobot != null)
            {
                ispoobot.health = ispoobot.health - Convert.ToSingle(hitInfo.damageTypes.Total());
                hitInfo.damageTypes.ScaleAll(0);
                return;
            }
        }

        public void DestroyRefreshCui(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "HealthGui");
            CuiHelper.DestroyUi(player, "RightRocketGui");
            CuiHelper.DestroyUi(player, "LeftRocketGui");
            CuiHelper.DestroyUi(player, "FuelGui");
            CuiHelper.DestroyUi(player, "FuelStr");
        }

        public void DestroyStaticCui(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "RepackButton");
            CuiHelper.DestroyUi(player, "NPCButton");
            CuiHelper.DestroyUi(player, "SentryButton");
            CuiHelper.DestroyUi(player, "SwapWeaponButton");
        }

        static void DestroyAll<T>()
        {
            var objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }

        #endregion

        #region Info Panel

        void SendInfoMessage(BasePlayer player, string message, float time)
        {
            player?.SendConsoleCommand("gametip.showgametip", message);
            timer.Once(time, () => player?.SendConsoleCommand("gametip.hidegametip"));
        }

        void GiveRandomHelpMessage(BasePlayer player)
        {
            player?.SendConsoleCommand("gametip.hidegametip");
            int getmessagenum = UnityEngine.Random.Range(0, 10);
            if (getmessagenum == 0) { SendInfoMessage(player, messagetxt1, 10f); return; }
            if (getmessagenum == 1) { SendInfoMessage(player, messagetxt2, 10f); return; }
            if (getmessagenum == 2) { SendInfoMessage(player, messagetxt3, 10f); return; }
            if (getmessagenum == 3) { SendInfoMessage(player, messagetxt4, 10f); return; }
            if (getmessagenum == 4) { SendInfoMessage(player, messagetxt5, 10f); return; }
            if (getmessagenum == 5) { SendInfoMessage(player, messagetxt6, 10f); return; }
            if (getmessagenum == 6) { SendInfoMessage(player, messagetxt7, 10f); return; }
            if (getmessagenum == 7) { SendInfoMessage(player, messagetxt8, 10f); return; }
            if (getmessagenum == 8) { SendInfoMessage(player, messagetxt9, 10f); return; }
            if (getmessagenum == 9) { SendInfoMessage(player, messagetxt10, 10f); return; }
        }

        #endregion

        #region SentryMode 

        class SentryMode : MonoBehaviour
        {

            PooBotEntity poobot;
            ulong poobotownerid;

            void Awake()
            {
                poobot = GetComponentInParent<PooBotEntity>();
                poobotownerid = poobot.ownerid;
            }

            void OnDestroy()
            {
                GameObject.Destroy(this);
            }
        }

        #endregion

        #region PooBotEntity

        class PooBotEntity : BaseEntity
        {
            #region Awake

            PooBot instance;
            public BaseEntity botentity;
            BasePlayer botplayer;
            BaseEntity bodytop, bodybottom, armRightShoulder, armLeftShoulder, armrightupper, armrighthand, armleftupper, armlefthand, legRight, legLeft;
            BaseEntity flamechunkerleft, flamechunkerright, currentweaponright, currentweaponleft, chainsaw1, armspikes, chainsaw3, jackhammer;

            public string rocketprefab;

            int counter;
            int npccounter;
            float fireleftcooldown;
            float firerightcoolodwn;
            int refreshcounter;
            int gathercounter;
            float fueltickcounter;
            int chargecooldowntimer;
            public bool moveforward, movebackward, ismoving, sprinting, docharge, chargecooldown, rotright, rotleft, docrouch;
            public bool moverightarm, moveleftarm, fireweapon, canfireright, canfireleft, righthandready, lefthandready, botdefeated, botstandup, botfalldown, activatenpc, activatesentry;
            bool setactive;

            ulong armskin;
            public ulong ownerid;

            Vector3 movedirection, rotdirection, startpos;
            float steps;
            public float health;

            private float secsToTake, secsTaken;

            private float initialRot;
            private Vector3 startRot;
            private Vector3 endRot;

            void Awake()
            {
                instance = new PooBot();
                botentity = GetComponentInParent<BaseEntity>();
                startpos = botentity.transform.position;
                botplayer = botentity.GetComponent<BaseMountable>()._mounted as BasePlayer;
                ownerid = botentity.OwnerID;
                rocketprefab = "assets/prefabs/ammo/rocket/rocket_basic.prefab";
                gameObject.name = "PooBot";
                counter = 0;
                npccounter = 0;
                gathercounter = 0;
                fueltickcounter = 0f;
                chargecooldowntimer = 0;
                setactive = false;
                moveforward = false;
                movebackward = false;
                sprinting = false;
                docharge = false;
                chargecooldown = false;
                rotright = false;
                rotleft = false;
                moverightarm = false;
                moveleftarm = false;
                ismoving = false;
                fireweapon = false;
                docrouch = false;

                canfireright = true;
                canfireleft = true;

                righthandready = true;
                lefthandready = true;
                botdefeated = false;
                botfalldown = false;
                botstandup = false;
                activatenpc = false;
                activatesentry = false;
                health = RobotStartHealth;
                fireleftcooldown = RocketReuseTime;
                firerightcoolodwn = RocketReuseTime;
                refreshcounter = 0;

                movedirection = botentity.transform.position + (botentity.transform.forward * steps * Time.deltaTime);
                steps = 10f;

                initialRot = botentity.transform.localEulerAngles.x;
                secsToTake = 2f;
                startRot = new Vector3(0.01f, botentity.transform.localEulerAngles.y, botentity.transform.localEulerAngles.z);
                endRot = new Vector3(85.99f, botentity.transform.localEulerAngles.y, botentity.transform.localEulerAngles.z);

                botentity.transform.position = startpos;
                botentity.transform.hasChanged = true;
                botentity.SendNetworkUpdateImmediate();
                botentity.UpdateNetworkGroup();
                SpawnPooBot();
            }

            #endregion

            #region Spawn PooBot

            private void SpawnPooBot()
            {
                string prefabMissleLauncher = "assets/prefabs/npc/sam_site_turret/sam_static.prefab";

                string prefabbbq = "assets/prefabs/deployable/bbq/bbq.deployed.prefab";

                string prefabOilRef = "assets/prefabs/deployable/oil refinery/refinery_small_deployed.prefab";

                string prefabflamechunker = "assets/prefabs/npc/flame turret/flameturret.deployed.prefab";

                bodytop = GameManager.server.CreateEntity(prefabOilRef, botentity.transform.position, Quaternion.identity, setactive);
                bodytop.enableSaving = false;
                bodytop.SetFlag(BaseEntity.Flags.Busy, true, false);
                bodytop.Spawn();
                bodytop.SetParent(botentity, 0);
                bodytop.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                bodytop.transform.localEulerAngles = new Vector3(0, 0, 0);

                bodybottom = GameManager.server.CreateEntity(prefabOilRef, botentity.transform.position, Quaternion.identity, setactive);
                bodybottom.enableSaving = false;
                bodybottom.SetFlag(BaseEntity.Flags.Busy, true, false);
                bodybottom.Spawn();
                bodybottom.SetParent(botentity, 0);
                bodybottom.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                bodybottom.transform.localEulerAngles = new Vector3(0, 0, 180);

                armRightShoulder = GameManager.server.CreateEntity(prefabbbq, botentity.transform.position, Quaternion.identity, setactive);
                armRightShoulder.enableSaving = false;
                armRightShoulder.SetFlag(BaseEntity.Flags.Busy, true, false);
                armRightShoulder.Spawn();
                armRightShoulder.SetParent(bodytop, 0);
                armRightShoulder.transform.localPosition = new Vector3(1.5f, 0f, -0.5f);
                armRightShoulder.transform.localEulerAngles = new Vector3(90, 0, 0);

                armrightupper = GameManager.server.CreateEntity(prefabOilRef, botentity.transform.position, Quaternion.identity, setactive);
                armrightupper.enableSaving = false;
                armrightupper.SetFlag(BaseEntity.Flags.Busy, true, false);
                armrightupper.Spawn();
                armrightupper.SetParent(armRightShoulder, 0);
                armrightupper.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                armrightupper.transform.localEulerAngles = new Vector3(90, 30, 0);

                armrighthand = GameManager.server.CreateEntity(prefabbbq, botentity.transform.position, Quaternion.identity, setactive);
                armrighthand.enableSaving = false;
                armrighthand.Spawn();
                armrighthand.SetParent(armrightupper, 0);
                armrighthand.transform.localPosition = new Vector3(0f, 3.5f, 0f);
                armrighthand.transform.localEulerAngles = new Vector3(180, 0, 0);

                armLeftShoulder = GameManager.server.CreateEntity(prefabbbq, botentity.transform.position, Quaternion.identity, setactive);
                armLeftShoulder.enableSaving = false;
                armLeftShoulder.SetFlag(BaseEntity.Flags.Busy, true, false);
                armLeftShoulder.Spawn();
                armLeftShoulder.SetParent(bodytop, 0);
                armLeftShoulder.transform.localPosition = new Vector3(-1.5f, 0f, -0.5f);
                armLeftShoulder.transform.localEulerAngles = new Vector3(90, 0, 0);

                armleftupper = GameManager.server.CreateEntity(prefabOilRef, botentity.transform.position, Quaternion.identity, setactive);
                armleftupper.enableSaving = false;
                armleftupper.SetFlag(BaseEntity.Flags.Busy, true, false);
                armleftupper.Spawn();
                armleftupper.SetParent(armLeftShoulder, 0, true, true);
                armleftupper.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                armleftupper.transform.localEulerAngles = new Vector3(90, -30, 0);

                armlefthand = GameManager.server.CreateEntity(prefabbbq, botentity.transform.position, Quaternion.identity, setactive);
                armlefthand.enableSaving = false;
                armlefthand.Spawn();
                armlefthand.SetParent(armleftupper, 0);
                armlefthand.transform.localPosition = new Vector3(0f, 3.5f, 0f);
                armlefthand.transform.localEulerAngles = new Vector3(180, 0, 0);

                ////////////////////////////////////////////////////////////////////////////////////////////////////////

                legRight = GameManager.server.CreateEntity(prefabOilRef, botentity.transform.position, Quaternion.identity, setactive);
                legRight.enableSaving = false;
                legRight.SetFlag(BaseEntity.Flags.Busy, true, false);
                legRight.Spawn();
                legRight.SetParent(bodybottom, 0);
                legRight.transform.localPosition = new Vector3(1.25f, 4f, 0f);
                legRight.transform.localEulerAngles = new Vector3(0, 180, 180);

                ////////////////////////////////////////////////////////////////////////////////////////////////////////


                legLeft = GameManager.server.CreateEntity(prefabOilRef, botentity.transform.position, Quaternion.identity, setactive);
                legLeft.enableSaving = false;
                legLeft.SetFlag(BaseEntity.Flags.Busy, true, false);
                legLeft.Spawn();
                legLeft.SetParent(bodybottom, 0);
                legLeft.transform.localPosition = new Vector3(-1.25f, 4f, 0f);
                legLeft.transform.localEulerAngles = new Vector3(0, 0, 180);

                flamechunkerleft = GameManager.server.CreateEntity(prefabflamechunker, botentity.transform.position, Quaternion.identity, setactive);
                flamechunkerleft.enableSaving = false;
                flamechunkerleft.SetFlag(BaseEntity.Flags.Busy, true, false);
                flamechunkerleft.Spawn();
                flamechunkerleft.SetParent(bodybottom, 0);
                flamechunkerleft.transform.localPosition = new Vector3(0.2f, 1f, 0.2f);
                flamechunkerleft.transform.localEulerAngles = new Vector3(0, 45, 0);

                RefreshMovement();
            }

            #endregion

            #region Hooks

            public void StopPooBot()
            {
                moveforward = false;
                movebackward = false;
                sprinting = false;
                rotright = false;
                rotleft = false;
                moveleftarm = false;
                moverightarm = false;
                fireweapon = false;
                activatenpc = false;
                activatesentry = false;
            }

            public void RestoreHealth()
            {
                if (health > 0) return;
                botdefeated = false;
                botfalldown = false;
                botstandup = true;
                health = 10000f;
            }

            private void PooBotDefeated()
            {
                botdefeated = true;
                activatenpc = false;
                activatesentry = false;
                npccounter = 0;
                botfalldown = true;
                botstandup = false;
                counter = 0;
            }

            public void StandBotUp()
            {
                botdefeated = false;
                botfalldown = false;
                botstandup = true;
            }

            private bool PlayerIsMounted()
            {
                bool flag = botentity.GetComponentInParent<BaseMountable>().IsMounted();
                return flag;
            }

            private bool hitSomething(Vector3 position)
            {
                var directioncheck = new Vector3();
                if (moveforward) directioncheck = (position + Vector3.down) + (transform.forward * 3);
                if (movebackward) directioncheck = (position + Vector3.down) - (transform.forward * 3);
                if (GamePhysics.CheckSphere(directioncheck, 2f, UnityEngine.LayerMask.GetMask("World", "Construction", "Default", "Debris", "Clutter", "Deployed", "Tree"), 0)) return true;
                return false;
            }

            private void OnDestroy()
            {
                if (botplayer != null) instance.DestroyRefreshCui(botplayer);
                if (botplayer != null) instance.DestroyStaticCui(botplayer);
                if (botentity != null && !botentity.IsDestroyed) { botentity.Invoke("KillMessage", 0.1f); }
            }

            #endregion

            #region Damage and Weapons

            private void ToggleWeaponsRight()
            {
                AddJackHammer(armrighthand);
                AddChainSaws(armrighthand);
            }

            private void NPCSwingArm()
            {
                int moveroll = UnityEngine.Random.Range(0, 2);
                if (moveroll == 0) { moverightarm = true; moveleftarm = false; }
                if (moveroll == 1) { moverightarm = false; moveleftarm = true; }
            }

            private void AddChainSaws(BaseEntity entity)
            {
                string prefabchainsaw = "assets/prefabs/weapons/chainsaw/chainsaw.entity.prefab";
                chainsaw1 = GameManager.server.CreateEntity(prefabchainsaw, entity.transform.position, Quaternion.identity, false);
                chainsaw1.enableSaving = false;
                chainsaw1.Spawn();
                chainsaw1.SetParent(entity, 0);
                chainsaw1.transform.localPosition = new Vector3(0f, 0f, 0f);
                chainsaw1.transform.localEulerAngles = new Vector3(0, 90, 0);
            }

            private void AddJackHammer(BaseEntity entity)
            {
                string prefabjackhammer = "assets/prefabs/tools/jackhammer/jackhammer.entity.prefab";
                jackhammer = GameManager.server.CreateEntity(prefabjackhammer, entity.transform.position, Quaternion.identity, true) as AttackEntity;
                jackhammer.enableSaving = false;
                var hammer = jackhammer.GetComponent<Jackhammer>();
                //hammer.SetHeld(true);
                //hammer.SetFlag(BaseEntity.Flags.Reserved8, true, false);
                jackhammer.Spawn();
                jackhammer.SetParent(entity, 0);
                jackhammer.transform.localPosition = new Vector3(0f, 0f, 0f);
                jackhammer.transform.localEulerAngles = new Vector3(0, 90, 0);
            }

            private void AddSpikes(BaseEntity entity)
            {
                string prefabspikes = "assets/prefabs/deployable/floor spikes/spikes.floor.prefab";
                armspikes = GameManager.server.CreateEntity(prefabspikes, entity.transform.position, Quaternion.identity, true);
                armspikes.enableSaving = false;
                armspikes.Spawn();
                armspikes.SetParent(entity, 0);
                armspikes.transform.localPosition = new Vector3(0f, 0f, 0f);
                armspikes.transform.localEulerAngles = new Vector3(0, 0, 0);
            }

            private void FireMisslesRight(BaseEntity entity, Vector3 startpos)
            {
                if (!canfireright) return;
                var missle = GameManager.server.CreateEntity(rocketprefab, startpos + new Vector3(0f, 1.4f, 0f), Quaternion.identity, true);
                missle.GetComponent<ServerProjectile>().gravityModifier = 0f;
                missle.GetComponent<ServerProjectile>().speed = 30;
                missle.creatorEntity = botplayer;
                missle.SendMessage("InitializeVelocity", (Vector3)(entity.transform.up * 60f));
                missle.Spawn();
                canfireright = false;
            }

            private void FireMisslesLeft(BaseEntity entity, Vector3 startpos)
            {
                if (!canfireleft) return;
                var missle = GameManager.server.CreateEntity(rocketprefab, startpos + new Vector3(0f, 1.4f, 0f), Quaternion.identity, true);
                missle.GetComponent<ServerProjectile>().gravityModifier = 0f;
                missle.GetComponent<ServerProjectile>().speed = 30;
                missle.creatorEntity = botplayer;
                missle.SendMessage("InitializeVelocity", (Vector3)(entity.transform.up * 60f));
                missle.Spawn();
                canfireleft = false;
            }

            private void ImpactDamage(Vector3 position)
            {
                float hitradius = PunchDamageRadius;
                if (activatenpc) hitradius = PunchDamageRadius * 2f;
                List<BaseEntity> neararm = new List<BaseEntity>();
                Vis.Entities<BaseEntity>(position, hitradius, neararm);
                foreach (BaseEntity ply in neararm)
                {
                    var getparent = ply.GetParentEntity();
                    if (getparent == botentity) continue;
                    var ispoobot = ply.GetComponentInParent<PooBotEntity>();
                    if (!ispoobot)
                    {
                        if (activatenpc) return;
                        var getcombatent = ply.GetComponent<BaseCombatEntity>();
                        if (AllowResourceGather) CheckResourceType(ply, position);
                        if (getcombatent)
                        {
                            if (AllowNormalDamage) getcombatent.Hurt(DamageToNonRobots, Rust.DamageType.Blunt, null, false);
                            else if (AllowPlayerDamage) getcombatent.Hurt(DamageToNonRobots, Rust.DamageType.Blunt, botplayer, false);
                        }
                        if (EnabledNonRobotConditionLoss) health = health - ConditionLossByHittingNonRobots;
                        Effect.server.Run("assets/prefabs/deployable/chinooklockedcrate/effects/landing.prefab", position);
                        return;
                    }
                    else
                    {
                        if (ispoobot.botdefeated) return;
                        var getcombatent = ply.GetComponent<BaseCombatEntity>() ?? null;
                        if (getcombatent) getcombatent.Hurt(DamageToRobots, Rust.DamageType.Blunt, botplayer, false);
                        Effect.server.Run("assets/prefabs/deployable/chinooklockedcrate/effects/landing.prefab", position);
                        if (DoKnockbackEffect)
                        {
                            var newPos = ply.transform.position - (botentity.transform.position - ply.transform.position).normalized * (2F);
                            float groundy = TerrainMeta.HeightMap.GetHeight(newPos);
                            Vector3 newentitypos = new Vector3(newPos.x, groundy + 4.1f, newPos.z);
                            ply.transform.localPosition = newentitypos;
                            ply.transform.hasChanged = true;
                            ply.SendNetworkUpdateImmediate();
                            ply.UpdateNetworkGroup();
                        }
                        ChanceToKnockDown(ispoobot);
                        if (EnableRobotConditionLoss) health = health - ConditionLossByHittingRobots;
                        Effect.server.Run("assets/bundled/prefabs/fx/survey_explosion.prefab", ply.transform.position);
                        return;
                    }
                }
            }

            private void CheckResourceType(BaseEntity ent, Vector3 pos)
            {
                if (ent.GetComponentInParent<TreeEntity>())
                {
                    GiveResources(botplayer, -151838493, WoodGatherAmount);
                    if (gathercounter >= WoodGatherCycles) { ent.GetComponentInParent<TreeEntity>().DelayedKill(); gathercounter = 0; return; }
                }
                if (ent.GetComponentInParent<OreResourceEntity>())
                {
                    if (ent.name.Contains("stone-ore")) GiveResources(botplayer, -2099697608, OreGatherAmount);
                    if (ent.name.Contains("metal-ore")) GiveResources(botplayer, -4031221, OreGatherAmount);
                    if (ent.name.Contains("sulfur-ore")) GiveResources(botplayer, -1157596551, OreGatherAmount);
                    if (gathercounter >= OreGatherCycles) { ent.GetComponentInParent<OreResourceEntity>().OnKilled(null); gathercounter = 0; return; }
                }
                gathercounter = gathercounter + 1;
            }

            private void GiveResources(BasePlayer player, int itemid, int itemamount)
            {
                Item item = ItemManager.CreateByItemID(itemid, 1, 0);
                if (item == null) return;

                item.amount = itemamount;
                if (!player.inventory.GiveItem(item, null))
                {
                    item.Remove(0f);
                    return;
                }
                player.Command("note.inv", new object[] { item.info.itemid, item.amount });
            }

            private void ChanceToKnockDown(PooBotEntity pooBotEntity)
            {
                int moveroll = UnityEngine.Random.Range(0, 100);
                if (moveroll > KnockdownChance) return;
                pooBotEntity.botfalldown = true;
                Effect.server.Run("assets/prefabs/misc/orebonus/effects/ore_finish.prefab", pooBotEntity.transform.position);
                instance.timer.Once(2f, () => pooBotEntity.StandBotUp());
            }

            #endregion

            #region Robot Buttons

            public void RobotButtons(BasePlayer player)
            {
                instance.DestroyStaticCui(player);
                var swapweaponbutton = new CuiElementContainer();
                swapweaponbutton.Add(new CuiButton
                {
                    Button = { Command = $"poobotswapweapon", Color = "0.0 0.0 0.0 0.8" },
                    RectTransform = { AnchorMin = "0.38 0.89", AnchorMax = "0.42 0.94" },
                    Text = { Text = "SWAP WEAPON", FontSize = 12, Color = "1.0 1.0 1.0 1.0", Align = TextAnchor.MiddleCenter }
                }, "Overall", "SwapWeaponButton");
                CuiHelper.AddUi(player, swapweaponbutton);

                var repackbutton = new CuiElementContainer();
                repackbutton.Add(new CuiButton
                {
                    Button = { Command = $"poobotrepack", Color = "0.0 0.0 0.0 0.8" },
                    RectTransform = { AnchorMin = "0.43 0.89", AnchorMax = "0.47 0.94" },
                    Text = { Text = "REPACK ROBOT", FontSize = 12, Color = "1.0 1.0 1.0 1.0", Align = TextAnchor.MiddleCenter }
                }, "Overall", "RepackButton");
                CuiHelper.AddUi(player, repackbutton);

                string npcbuttoncolor = "0.0 0.0 0.0 0.8";
                if (activatenpc) npcbuttoncolor = "0.0 0.5 0.0 0.8";

                var activatenpcbutton = new CuiElementContainer();
                activatenpcbutton.Add(new CuiButton
                {
                    Button = { Command = $"poobotactivatenpc", Color = npcbuttoncolor },
                    RectTransform = { AnchorMin = "0.53 0.89", AnchorMax = "0.57 0.94" },
                    Text = { Text = "SPAR MODE", FontSize = 12, Color = "1.0 1.0 1.0 1.0", Align = TextAnchor.MiddleCenter }
                }, "Overall", "NPCButton");
                CuiHelper.AddUi(player, activatenpcbutton);

                string sentrybuttoncolor = "0.0 0.0 0.0 0.8";
                if (activatesentry) sentrybuttoncolor = "0.0 0.5 0.0 0.8";

                var activatesentrybutton = new CuiElementContainer();
                activatesentrybutton.Add(new CuiButton
                {
                    Button = { Command = $"poobotactivatesentry", Color = sentrybuttoncolor },
                    RectTransform = { AnchorMin = "0.58 0.89", AnchorMax = "0.62 0.94" },
                    Text = { Text = "SENTRY MODE", FontSize = 12, Color = "1.0 1.0 1.0 1.0", Align = TextAnchor.MiddleCenter }
                }, "Overall", "SentryButton");
                CuiHelper.AddUi(player, activatesentrybutton);
            }

            #endregion

            #region Health Indicator

            public void HealthIndicator(BasePlayer player)
            {
                if (player == null) { OnDestroy(); return; }
                instance.DestroyRefreshCui(player);

                /////////////////////////////////////////////////////

                var healthstr = health.ToString();

                var healthindicator = new CuiElementContainer();
                healthindicator.Add(new CuiButton
                {
                    Button = { Command = "", Color = "0.0 0.0 0.0 0.8" },
                    RectTransform = { AnchorMin = "0.45 0.85", AnchorMax = "0.55 0.88" },
                    Text = { Text = healthstr, FontSize = 18, Color = "0.0 0.0 1.0 1.0", Align = TextAnchor.MiddleCenter }
                }, "Overall", "HealthGui");
                CuiHelper.AddUi(player, healthindicator);

                /////////////////////////////////////////////////////

                var fuelstr = player.inventory.GetAmount(FuelItemID).ToString();

                var fuelindicator = new CuiElementContainer();
                fuelindicator.Add(new CuiButton
                {
                    Button = { Command = "", Color = "0.0 0.0 0.0 0.8" },
                    RectTransform = { AnchorMin = "0.48 0.89", AnchorMax = "0.52 0.92" },
                    Text = { Text = fuelstr, FontSize = 18, Color = "1.0 1.0 1.0 1.0", Align = TextAnchor.MiddleCenter }
                }, "Overall", "FuelGui");
                CuiHelper.AddUi(player, fuelindicator);

                var fueltext = new CuiElementContainer();
                fueltext.Add(new CuiButton
                {
                    Button = { Command = "", Color = "0.0 0.0 0.0 0.8" },
                    RectTransform = { AnchorMin = "0.48 0.92", AnchorMax = "0.52 0.94" },
                    Text = { Text = "FUEL", FontSize = 10, Color = "1.0 1.0 1.0 1.0", Align = TextAnchor.MiddleCenter }
                }, "Overall", "FuelStr");
                CuiHelper.AddUi(player, fueltext);

                /////////////////////////////////////////////////////

                var rockettxtleft = fireleftcooldown.ToString("F0");
                if (canfireleft) rockettxtleft = "Rocket Armed";
                if (!instance.HasRockets(player)) rockettxtleft = "No Rockets";

                var rocketstrleft = "1.0 0.0 0.0 1.0";
                if (canfireleft) rocketstrleft = "0.0 0.5 0.0 1.0";
                if (!instance.HasRockets(player)) rocketstrleft = "1.0 0.0 0.0 1.0";

                var leftrocket = new CuiElementContainer();
                leftrocket.Add(new CuiButton
                {
                    Button = { Command = "", Color = "0.0 0.0 0.0 0.8" },
                    RectTransform = { AnchorMin = "0.39 0.85", AnchorMax = "0.45 0.88" },
                    Text = { Text = rockettxtleft, FontSize = 12, Color = rocketstrleft, Align = TextAnchor.MiddleCenter }
                }, "Overall", "LeftRocketGui");
                CuiHelper.AddUi(player, leftrocket);

                /////////////////////////////////////////////////////


                var rockettextright = firerightcoolodwn.ToString("F0");
                if (canfireright) rockettextright = "Rocket Armed";
                if (!instance.HasRockets(player)) rockettextright = "No Rockets";

                var rocketstrright = "1.0 0.0 0.0 1.0";
                if (canfireright) rocketstrright = "0.0 0.5 0.0 1.0";
                if (!instance.HasRockets(player)) rocketstrright = "1.0 0.0 0.0 1.0";

                var rightrocket = new CuiElementContainer();
                rightrocket.Add(new CuiButton
                {
                    Button = { Command = "", Color = "0.0 0.0 0.0 0.8" },
                    RectTransform = { AnchorMin = "0.55 0.85", AnchorMax = "0.61 0.88" },
                    Text = { Text = rockettextright, FontSize = 12, Color = rocketstrright, Align = TextAnchor.MiddleCenter }
                }, "Overall", "RightRocketGui");
                CuiHelper.AddUi(player, rightrocket);

                /////////////////////////////////////////////////////
            }

            #endregion

            #region Fixed Update

            private void FixedUpdate()
            {
                var startpos = botentity.transform.position;
                botplayer = botentity.GetComponent<BaseMountable>()._mounted;
                if (chargecooldown)
                {
                    chargecooldowntimer = chargecooldowntimer + 1;
                    if (chargecooldowntimer >= 25) { chargecooldown = false; chargecooldowntimer = 0; }
                    return;
                }
                if (botdefeated && !botfalldown) return;
                if ((botfalldown && botdefeated))
                {
                    DoFallDownMove();
                    RefreshMovement();
                    return;
                }
                if (botstandup)
                {
                    StandBackUp();
                    RefreshMovement();
                    return;
                }
                if (health <= 0 && !botdefeated) { health = 0; PooBotDefeated(); return; }
                if (activatenpc)
                {
                    npccounter = npccounter + 1;
                    if (npccounter == 5) { moveforward = true; movebackward = false; moverightarm = false; moveleftarm = false; }
                    if (npccounter == 10) NPCSwingArm();
                    if (npccounter == 15) { moveforward = false; movebackward = true; moverightarm = false; moveleftarm = false; }
                    if (npccounter == 20) NPCSwingArm();
                    if (npccounter == 25) { moveforward = false; movebackward = false; moverightarm = false; moveleftarm = false; npccounter = 0; }

                }

                if (!activatenpc && !PlayerIsMounted()) { StopPooBot(); botplayer = null; return; }
                if (botplayer) HealthIndicator(botplayer);
                if (RequireFuel)
                {
                    if (fueltickcounter == 0f)
                    {
                        if (!instance.TakeItem1Mats(botplayer, FuelItemID, FuelUsagePerTick))
                        {
                            StopPooBot(); return;
                        }
                    }
                    fueltickcounter = fueltickcounter + 0.1f;
                    if (fueltickcounter >= FuelTickRate)
                    {
                        fueltickcounter = 0f;
                    }
                }

                DoArmMove();

                if (moveforward || movebackward || rotright || rotleft)
                {
                    counter = counter + 1;

                    if (counter == 5)
                    {
                        DoLegMove1();
                    }
                    if (counter == 10)
                    {
                        DoLegMove2();
                        counter = 0;
                    }
                    steps = 10f;
                    if (sprinting) steps = 15f;
                    //if (docharge) steps = 200f;

                    Vector3 entitypos = botentity.transform.position;
                    Vector3 startrot = botentity.transform.eulerAngles;

                    float groundy = TerrainMeta.HeightMap.GetHeight(entitypos);
                    Vector3 newentitypos = new Vector3(entitypos.x, groundy + 4.1f, entitypos.z);

                    if (moveforward) movedirection = newentitypos + (botentity.transform.forward * steps * Time.deltaTime);
                    else if (movebackward) movedirection = newentitypos + (botentity.transform.forward * -steps * Time.deltaTime);

                    if (hitSomething(newentitypos)) { movedirection = startpos; RefreshMovement(); StopPooBot(); return; }

                    botentity.transform.localPosition = movedirection;

                    if (rotright) { rotdirection = new Vector3(startrot.x, startrot.y + 3f, startrot.z); }
                    else if (rotleft) { rotdirection = new Vector3(startrot.x, startrot.y - 3f, startrot.z); }

                    botentity.transform.eulerAngles = rotdirection;
                }
                else if (!movebackward && !moveforward && !moverightarm && !moveleftarm)
                {
                    if (docrouch)
                    {
                        DoCrouch();
                    }
                    else
                    {
                        Vector3 entitypos = botentity.transform.position;
                        botentity.transform.localPosition = entitypos;
                        ResetLegs();
                    }
                }

                DoArmReset();

                if (botplayer != null && bodytop != null)
                {
                    bodytop.transform.rotation = Quaternion.Lerp(bodytop.transform.rotation, botplayer.eyes.rotation, 2f * Time.deltaTime);
                }
                if (!canfireleft)
                {
                    fireleftcooldown = fireleftcooldown - 0.1f;
                    if (fireleftcooldown <= 0f) { canfireleft = true; fireleftcooldown = RocketReuseTime; }
                }
                if (!canfireright)
                {
                    firerightcoolodwn = firerightcoolodwn - 0.1f;
                    if (firerightcoolodwn <= 0f) { canfireright = true; firerightcoolodwn = RocketReuseTime; }
                }

                RefreshMovement();
                if (docharge) { DoCharge(); chargecooldown = true; docharge = false; }
            }

            #endregion

            #region Movement Hooks

            private void DoArmMove()
            {
                if (moverightarm)
                {
                    if (armrightupper != null) armrightupper.transform.localEulerAngles = new Vector3(25, 80, 0);
                    if (armrighthand != null) armrighthand.transform.localEulerAngles += new Vector3(0, 30, 0);
                    if (armrighthand != null && righthandready) ImpactDamage(armrighthand.transform.position);
                    if (fireweapon && armrighthand != null) FireMisslesRight(armrightupper, armrighthand.transform.position + Vector3.down);
                }

                if (moveleftarm)
                {
                    if (armleftupper != null) armleftupper.transform.localEulerAngles = new Vector3(25, -80, 0);
                    if (armlefthand != null) armlefthand.transform.localEulerAngles += new Vector3(0, 30, 0);
                    if (armlefthand != null && lefthandready) ImpactDamage(armlefthand.transform.position);
                    if (fireweapon && armlefthand != null) FireMisslesLeft(armleftupper, armlefthand.transform.position + Vector3.down);
                }
            }

            private void DoArmReset()
            {

                if (!moverightarm && !docrouch)
                {
                    if (armrightupper != null) armrightupper.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                    if (armrightupper != null) armrightupper.transform.localEulerAngles = new Vector3(90, 30, 0);
                }
                if (!moveleftarm && !docrouch)
                {
                    if (armleftupper != null) armleftupper.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                    if (armleftupper != null) armleftupper.transform.localEulerAngles = new Vector3(90, -30, 0);
                }
            }

            private void DoCharge()
            {
            }

            private void DoCrouch()
            {
                Vector3 entitypos = botentity.transform.position;
                float groundy = TerrainMeta.HeightMap.GetHeight(entitypos);
                botentity.transform.position = new Vector3(entitypos.x, groundy + 3f, entitypos.z);

                if (armrightupper != null) armrightupper.transform.localEulerAngles = new Vector3(90, 30, -90);
                if (armleftupper != null) armleftupper.transform.localEulerAngles = new Vector3(90, -30, 90);

                if (legRight != null) legRight.transform.localPosition = new Vector3(1.25f, 3f, 0f);
                if (legRight != null) legRight.transform.localEulerAngles = new Vector3(0, 180, 180);

                if (legLeft != null) legLeft.transform.localPosition = new Vector3(-1.25f, 3f, 0f);
                if (legLeft != null) legLeft.transform.localEulerAngles = new Vector3(0, 0, 180);

                if (flamechunkerleft != null) flamechunkerleft.SetFlag(BaseEntity.Flags.Reserved4, true);
            }

            private void DoLegMove1()
            {
                if (legRight != null) legRight.transform.localPosition = new Vector3(1.25f, 4f, -1f); ;
                if (legRight != null) legRight.transform.localEulerAngles = new Vector3(30, 180, 180);

                if (legLeft != null) legLeft.transform.localPosition = new Vector3(-1.25f, 4f, 1f);
                if (legLeft != null) legLeft.transform.localEulerAngles = new Vector3(30, 0, 180);

                if (legLeft != null) Effect.server.Run("assets/bundled/prefabs/fx/beartrap/fire.prefab", legLeft.transform.position);
                ImpactDamage(legLeft.transform.position);
            }

            private void DoLegMove2()
            {
                if (legRight != null) legRight.transform.localPosition = new Vector3(1.25f, 4f, 1f);
                if (legRight != null) legRight.transform.localEulerAngles = new Vector3(330, 180, 180);

                if (legLeft != null) legLeft.transform.localPosition = new Vector3(-1.25f, 4f, -1f);
                if (legLeft != null) legLeft.transform.localEulerAngles = new Vector3(330, 0, 180);

                if (legRight != null) Effect.server.Run("assets/bundled/prefabs/fx/beartrap/fire.prefab", legRight.transform.position);
                ImpactDamage(legRight.transform.position);
            }

            private void ResetLegs()
            {
                Vector3 entitypos = botentity.transform.position;
                float groundy = TerrainMeta.HeightMap.GetHeight(entitypos);
                botentity.transform.position = new Vector3(entitypos.x, groundy + 4.1f, entitypos.z);

                if (armrightupper != null) armrightupper.transform.localEulerAngles = new Vector3(90, 30, 0);

                if (armleftupper != null) armleftupper.transform.localEulerAngles = new Vector3(90, -30, 0); ;

                if (legLeft != null) legLeft.transform.localPosition = new Vector3(-1.25f, 4f, 0f);
                if (legLeft != null) legLeft.transform.localEulerAngles = new Vector3(0, 0, 180);

                if (legRight != null) legRight.transform.localPosition = new Vector3(1.25f, 4f, 0f);
                if (legRight != null) legRight.transform.localEulerAngles = new Vector3(0, 180, 180);

                if (flamechunkerleft != null) flamechunkerleft.SetFlag(BaseEntity.Flags.Reserved4, false);
            }

            private void DoFallDownMove()
            {
                secsTaken = secsTaken + UnityEngine.Time.deltaTime;
                float single = Mathf.InverseLerp(0f, 1.5f, secsTaken);
                botentity.transform.localEulerAngles = Vector3.Lerp(startRot, -endRot, single);
                if (single >= 1)
                {
                    botentity.transform.localEulerAngles = endRot;
                    secsTaken = 0;
                    if (health <= 0) { health = 0; botfalldown = false; return; }
                    botstandup = true;
                    return;
                }
                Vector3 entitypos = botentity.transform.position;
                float groundy = TerrainMeta.HeightMap.GetHeight(entitypos);
                Vector3 newentitypos = new Vector3(entitypos.x, groundy, entitypos.z);

                botentity.transform.position = Vector3.MoveTowards(transform.position, newentitypos, (1.5f) * Time.deltaTime);
                botentity.transform.hasChanged = true;
                botentity.SendNetworkUpdateImmediate();
                botentity.UpdateNetworkGroup();
                RefreshMovement();
                return;
            }

            private void StandBackUp()
            {
                secsTaken = secsTaken + UnityEngine.Time.deltaTime;
                float single = Mathf.InverseLerp(0f, 1.5f, secsTaken);
                botentity.transform.localEulerAngles = Vector3.Lerp(-endRot, startRot, single);
                if (single >= 1)
                {
                    botentity.transform.localEulerAngles = startRot;
                    secsTaken = 0;
                    botstandup = false;
                    return;
                }
                Vector3 entitypos = botentity.transform.position;
                float groundy = TerrainMeta.HeightMap.GetHeight(entitypos);
                Vector3 newentitypos = new Vector3(entitypos.x, groundy + 4.1f, entitypos.z);

                botentity.transform.position = Vector3.MoveTowards(transform.position, newentitypos, (1.5f) * Time.deltaTime);
                botentity.transform.hasChanged = true;
                botentity.SendNetworkUpdateImmediate();
                botentity.RefreshEntityLinks();
                botentity.UpdateNetworkGroup();
                RefreshMovement();
                return;
            }

            private void RefreshMovement()
            {

                botentity.transform.hasChanged = true;
                botentity.SendNetworkUpdateImmediate();
                botentity.UpdateNetworkGroup();

                if (armrightupper != null) armrightupper.transform.hasChanged = true;
                if (armrightupper != null) armrightupper.SendNetworkUpdateImmediate(true);
                if (armrightupper != null) armrightupper.UpdateNetworkGroup();

                if (armrighthand != null) armrighthand.transform.hasChanged = true;
                if (armrighthand != null) armrighthand.SendNetworkUpdateImmediate(true);
                if (armrighthand != null) armrighthand.UpdateNetworkGroup();

                if (armleftupper != null) armleftupper.transform.hasChanged = true;
                if (armleftupper != null) armleftupper.SendNetworkUpdateImmediate(true);
                if (armleftupper != null) armleftupper.UpdateNetworkGroup();

                if (armlefthand != null) armlefthand.transform.hasChanged = true;
                if (armlefthand != null) armlefthand.SendNetworkUpdateImmediate(true);
                if (armlefthand != null) armlefthand.UpdateNetworkGroup();

                if (bodytop != null) bodytop.transform.hasChanged = true;
                if (bodytop != null) bodytop.SendNetworkUpdateImmediate(true);
                if (bodytop != null) bodytop.UpdateNetworkGroup();

                if (legRight != null) legRight.transform.hasChanged = true;
                if (legRight != null) legRight.SendNetworkUpdateImmediate(true);
                if (legRight != null) legRight.UpdateNetworkGroup();

                if (legLeft != null) legLeft.transform.hasChanged = true;
                if (legLeft != null) legLeft.SendNetworkUpdateImmediate(true);
                if (legLeft != null) legLeft.UpdateNetworkGroup();
            }

            #endregion
        }

        #endregion

    }
}