using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using VLB;
using Network;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Linq;
using Facepunch;

#if CARBON
using Carbon;
using Carbon.Plugins;
using Carbon.Components;
#else
using Oxide.Ext.CarbonAliases;
using System.Reflection;
#endif

namespace Oxide.Plugins
{
    [Info("MeteorEvent", "https://discord.gg/TrJ7jnS233", "2.0.9")]
#if CARBON
    public class MeteorEvent : CarbonPlugin
#else
    public class MeteorEvent : RustPlugin
#endif 
    {
        [PluginReference] private readonly Plugin Economics, ServerRewards, BankSystem, ShoppyStock;
        private static string webhookEmbed, webhookText;
        private static readonly HashSet<SphereEntity> meteors = new HashSet<SphereEntity>();
        private static readonly HashSet<SphereEntity> fallingMeteors = new HashSet<SphereEntity>();
        private static readonly Dictionary<ulong, string> cachedSelectedEvent = new Dictionary<ulong, string>();
        private static readonly Dictionary<ulong, Dictionary<string, int>> cooldowns = new Dictionary<ulong, Dictionary<string, int>>();
        private static string cachedJson = "";
        private static Timer cooldownTimer;
        private static bool checkExplosives = true;

#if !CARBON
        public static CUI.Handler CuiHandler { get; private set; }
#endif

        private void OnServerInitialized()
        {
            LoadConfig();
            LoadMessages();
            ValidateHookRequirement();
            RegisterCommandsAndPermissions();
            StartEventTimers();
            InitializeWebhooks();
            GenerateStaticJson();
        }

        private void Unload()
        {
            ClearMeteors();
            using CUI cui = new CUI(CuiHandler);
            foreach (var player in BasePlayer.activePlayerList)
            {
                cui.Destroy("MeteorEventUI_corePanel", player);
                cui.Destroy("MeteorEventUI_updateModule", player);
            }
        }

        private void RegisterCommandsAndPermissions()
        {
            cmd.AddConsoleCommand("msadmin", this, nameof(MeteorEventUserConsoleCommand));
#if CARBON
            foreach (var command in config.commands)
                cmd.AddCovalenceCommand(command, this, nameof(MeteorEventCommand));
            cmd.AddConsoleCommand(Community.Protect("UI_MeteorEvent"), this, nameof(MeteorEventConsoleCommand), @protected: true);
#else
            foreach (var command in config.commands)
                cmd.AddChatCommand(command, this, nameof(MeteorEventCommand));
            cmd.AddConsoleCommand("UI_MeteorEvent", this, nameof(MeteorEventConsoleCommand));
#endif
            if (config.commandPermission)
                permission.RegisterPermission($"meteorevent.use", this);
            permission.RegisterPermission($"meteorevent.admin", this);
            foreach (var profile in config.profiles)
                if (profile.Value.purchaseConfig.requirePerm)
                    permission.RegisterPermission($"meteorevent.purchase.{profile.Key}", this);
        }

        private void ValidateHookRequirement()
        {
            bool lockLargeMeteorHooks = true;
            bool lockExplosiveHooks = true;
            foreach (var profile in config.meteorPrefabs.Values)
            {
                if (profile.bigMeteors.bigMeteorChance > 0)
                {
                    lockLargeMeteorHooks = false;
                    foreach (var explosive in profile.bigMeteors.explosiveConfig.Keys)
                    {
                        if (explosive == "ammo.rifle.explosive")
                        {
                            lockExplosiveHooks = false;
                            break;
                        }
                    }
                    if (!lockExplosiveHooks)
                        break;
                }
            }
            if (lockLargeMeteorHooks)
            {
                Unsubscribe(nameof(OnExplosiveThrown));
                Unsubscribe(nameof(OnRocketLaunched));
            }
            if (lockExplosiveHooks)
                Unsubscribe(nameof(OnWeaponFired));
            checkExplosives = !lockLargeMeteorHooks;
        }

        private void StartEventTimers()
        {
            foreach (var configTimer in config.timedEvents.Keys)
                RunTimedEvent(configTimer);
        }

        private static void InitializeWebhooks()
        {
            webhookEmbed = JsonConvert.SerializeObject(new
            {
                embeds = new[] {
                    new {
                        title = "{title}", color = -5,
                        fields = new[] { new {name = "{message}", value = "", inline = true} }
                    }
                },
            }, Formatting.None);
            webhookText = JsonConvert.SerializeObject(new { content = "{message}" }, Formatting.None);
        }

        private void GenerateStaticJson()
        {
            using CUI cui = new CUI(CuiHandler);
            CuiElementContainer corePanel = cui.CreateContainer("MeteorEventUI_corePanel", BetterColors.Transparent, 0, 1, 0, 1, 0, 0, 0, 0, 0f, 0f, true, true, CUI.ClientPanels.HudMenu, null);
            //Background Blur
            cui.CreatePanel(corePanel, "MeteorEventUI_corePanel", BetterColors.BlackTransparent10, "assets/content/ui/uibackgroundblur.mat", 0, 1, 0, 1, 0, 0, 0, 0, true, 0f, 0f, false, false, null, null, false, "MeteorEventUI_UiBlur0", null, false);
            //Background Darker
            cui.CreatePanel(corePanel, "MeteorEventUI_corePanel", BetterColors.CraftingRadialBackground, "assets/content/ui/namefontmaterial.mat", 0, 1, 0, 1, 0, 0, 0, 0, false, 0f, 0f, false, false, null, null, false, "MeteorEventUI_UiPanel1", null, false);
            //Middle Anchor
            cui.CreatePanel(corePanel, "MeteorEventUI_corePanel", BetterColors.Transparent, "assets/content/ui/namefontmaterial.mat", 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 0, 0, false, 0f, 0f, false, false, null, null, false, "MeteorEventUI_middleAnchor", null, false);
            //Main Background
            cui.CreatePanel(corePanel, "MeteorEventUI_middleAnchor", BetterColors.RadioUiBackground, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, -227, 209, -108, 120, false, 0f, 0f, false, false, null, null, false, "MeteorEventUI_mainBackground", null, false);
            //Top Panel
            cui.CreatePanel(corePanel, "MeteorEventUI_mainBackground", BetterColors.LightGrayTransparent3_9, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 0, 436, 202, 228, false, 0f, 0f, false, false, null, null, false, "MeteorEventUI_topPanel", null, false);
            //Close Button
            cui.CreateProtectedButton(corePanel, "MeteorEventUI_topPanel", BetterColors.RedBackground, BetterColors.RedText, " ✖", 12, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 414, 432, 4, 22, "UI_MeteorEvent close", TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "MeteorEventUI_closeButton", null, false);
            //Right Panel
            cui.CreatePanel(corePanel, "MeteorEventUI_mainBackground", BetterColors.BlackTransparent20, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 132, 436, 0, 202, false, 0f, 0f, false, false, null, null, false, "MeteorEventUI_rightPanel", null, false);
            //Cooldown Panel
            cui.CreatePanel(corePanel, "MeteorEventUI_rightPanel", BetterColors.LightGrayTransparent3_9, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 265, 300, 182, 198, false, 0f, 0f, false, false, null, null, false, "MeteorEventUI_cooldownPanel", null, false);
            //Cooldown Icon
            cui.CreateSimpleImage(corePanel, "MeteorEventUI_cooldownPanel", "", "assets/icons/stopwatch.png", BetterColors.LightGrayTransparent73, null, 0f, 0f, 0f, 0f, 2, 14, 2, 14, 0f, 0f, false, false, null, null, false, "MeteorEventUI_UiImage4", null, false);
            //Meteor Amount Panel
            cui.CreatePanel(corePanel, "MeteorEventUI_rightPanel", BetterColors.LightGrayTransparent3_9, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 265, 300, 164, 180, false, 0f, 0f, false, false, null, null, false, "MeteorEventUI_meteorAmountPanel", null, false);
            //Meteor Amount Icon
            cui.CreateSimpleImage(corePanel, "MeteorEventUI_meteorAmountPanel", "", "assets/content/ui/map/icon-map_rock.png", BetterColors.LightGrayTransparent73, null, 0f, 0f, 0f, 0f, 0, 16, 0, 16, 0f, 0f, false, false, null, null, false, "MeteorEventUI_UiImage5", null, false);
            //Bottom Panel
            cui.CreatePanel(corePanel, "MeteorEventUI_rightPanel", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 0, 304, 0, 28, false, 0f, 0f, false, false, null, null, false, "MeteorEventUI_bottomPanel", null, false);
            //Info Icon
            cui.CreateSimpleImage(corePanel, "MeteorEventUI_bottomPanel", "", "assets/icons/info.png", BetterColors.LightGrayTransparent73, null, 0f, 0f, 0f, 0f, 6, 22, 6, 22, 0f, 0f, false, false, null, null, false, "MeteorEventUI_UiImage7", null, false);
            cachedJson = CuiHelper.ToJson(corePanel);
        }

        private void CheckCooldownTimer()
        {
            int cooldownCount = cooldowns.Count;
            if (cooldownCount > 0 && cooldownTimer == null)
            {
                cooldownTimer = timer.Every(1f, () => RunCooldownTimer());
            }
            else if (cooldownCount == 0 && cooldownTimer != null)
            {
                cooldownTimer.Destroy();
                cooldownTimer = null;
            }
        }

        private static void RunCooldownTimer()
        {
            foreach (var user in cooldowns.ToList())
            {
                foreach (var profile in user.Value.ToList())
                {
                    cooldowns[user.Key][profile.Key]--;
                    if (cooldowns[user.Key][profile.Key] <= 0)
                    {
                        cooldowns[user.Key].Remove(profile.Key);
                        if (cooldowns[user.Key].Count == 0)
                            cooldowns.Remove(user.Key);
                    }
                }
            }
        }

		private static void ClearMeteors()
		{

			var meteorsCopy = new List<BaseEntity>(meteors);
			foreach (var meteor in meteorsCopy)
			{
				if (meteor == null || meteor.IsDestroyed) continue;
				MeteorController controller = meteor.GetComponent<MeteorController>();
				if (controller == null) continue;
				controller.KillMeteor();
			}

			var fallingMeteorsCopy = new List<BaseEntity>(fallingMeteors);
			foreach (var meteor in fallingMeteorsCopy)
			{
				if (meteor == null || meteor.IsDestroyed) continue;
				FallingMeteorController controller = meteor.GetComponent<FallingMeteorController>();
				controller.DestroyFallingMeteor(false);
			}
			
			meteors.Clear();
			fallingMeteors.Clear();

			if (config.enableHooks)
				Interface.CallHook("OnMeteorsKilled");
		}

        private void MeteorEventCommand(BasePlayer player, string command, string[] args)
        {
            if (config.commandPermission && !permission.UserHasPermission(player.UserIDString, "meteorevent.use"))
            {
                SendReply(player, Lang("NoPermission", player.UserIDString));
                return;
            }
            if (args.Length == 0)
                OpenMeteorEventUI(player);
            else if (args.Length < 3)
            {
                if (!permission.UserHasPermission(player.UserIDString, "meteorevent.admin"))
                {
                    OpenMeteorEventUI(player);
                    return;
                }
                SendReply(player, Lang("CommandHelp", player.UserIDString, command));
            }
            else if (args[0].ToLower() == "direct")
            {
                if (!permission.UserHasPermission(player.UserIDString, "meteorevent.admin"))
                {
                    OpenMeteorEventUI(player);
                    return;
                }
                string profile = args[1];
                if (!config.profiles.ContainsKey(profile))
                {
                    SendReply(player, Lang("NoProfileFound", player.UserIDString, profile));
                    return;
                }
                BasePlayer directPlayer = BasePlayer.FindAwakeOrSleeping(args[2]);
                if (directPlayer == null)
                {
                    SendReply(player, Lang("NoPlayerFound", player.UserIDString, args[2]));
                    return;
                }
                int meteorAmount = 1;
                if (args.Length > 3)
                    int.TryParse(args[2], out meteorAmount);
                SendReply(player, Lang("CallingDirectOnPlayer", player.UserIDString, profile, directPlayer.displayName, meteorAmount));
                RunMeteorShower(profile, meteorAmount, true, directPlayer);
            }
        }

        private void MeteorEventConsoleCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (config.enableLogs)
                Puts($"Player {player.displayName} ({player.UserIDString}) ran MeteorEventUI sub-command: {arg.FullString}");
            using CUI cui = new CUI(CuiHandler);
            switch (arg.Args[0])
            {
                case "close":
                    cui.Destroy("MeteorEventUI_corePanel", player);
                    cui.Destroy("MeteorEventUI_updateModule", player);
                    break;
                case "switch":
                    string oldCategory = cachedSelectedEvent[player.userID];
                    cachedSelectedEvent[player.userID] = arg.Args[1];
                    UpdateDisplay(player, oldCategory);
                    break;
                case "forceStart":
                    int meteorCount;
                    if (int.TryParse(arg.Args[2], out meteorCount))
                    {
                        if (meteorCount <= 0) return;
                        cui.Destroy("MeteorEventUI_corePanel", player);
                        cui.Destroy("MeteorEventUI_updateModule", player);
                        RunMeteorShower(arg.Args[1], meteorCount);
                    }
                    break;
                case "start":
                    cui.Destroy("MeteorEventUI_corePanel", player);
                    cui.Destroy("MeteorEventUI_updateModule", player);
                    TryPurchaseEvent(player, arg.Args[1]);
                    break;
            }
        }

        private void MeteorEventUserConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "Meteor Event Admin Commands:\nmsadmin run <profile> <amount> - Starts meteor event of desired profile with certain amount of meteors.\nmsadmin direct <profile> [amount] [userId] - Starts direct meteor event of desired profile with certain amount of meteors at certain player.\n\nmsadmin kill - Kills all meteors around the map");
                return;
            }
            switch (arg.Args[0])
            {
                case "run":
                    string profile = arg.Args[1];
                    if (!config.profiles.ContainsKey(profile))
                    {
                        SendReply(arg, "[ERROR] Couldn't find meteor event profile!");
                        return;
                    }
                    int amount;
                    if (int.TryParse(arg.Args[2], out amount))
                        RunMeteorShower(profile, amount);
                    else
                        SendReply(arg, "[ERROR] Specify correct amount of meteors!");
                    break;
                case "direct":
                    profile = arg.Args[1];
                    if (!config.profiles.ContainsKey(profile))
                    {
                        SendReply(arg, "[ERROR] Couldn't find meteor event profile!");
                        return;
                    }
                    amount = 1;
                    if (arg.Args.Length > 1)
                        int.TryParse(arg.Args[2], out amount);
                    BasePlayer directPlayer = null;
                    ulong directId = 0;
                    if (arg.Args.Length > 2 && ulong.TryParse(arg.Args[3], out directId))
                        directPlayer = BasePlayer.FindByID(directId);
                    if (directId != 0 && directPlayer == null)
                    {
                        SendReply(arg, "[ERROR] Player not found!");
                        return;
                    }
                    RunMeteorShower(profile, amount, true, directPlayer);
                    break;
                case "kill":
                    ClearMeteors();
                    break;
                default:
                    SendReply(arg, "[ERROR] Invalid format!\nMeteor Event Admin Commands:\nmsadmin run <profile> <amount> - Starts meteor event of desired profile with certain amount of meteors.\nmsadmin direct <profile> [amount] [userId] - Starts direct meteor event of desired profile with certain amount of meteors at certain player.\n\nmsadmin kill - Kills all meteors around the map");
                    break;
            }
        }

        private void TryPurchaseEvent(BasePlayer player, string profile)
        {
            MeteorConfig configValue = config.profiles[profile];
            if (configValue.purchaseConfig.showerCooldown > 0 && cooldowns.ContainsKey(player.userID) && cooldowns[player.userID].ContainsKey(profile)) return;
            bool hasRequiredItem;
            if (configValue.purchaseConfig.requiredShortname == "")
                hasRequiredItem = HaveCurrency(player, configValue.purchaseConfig.economyPlugin, configValue.purchaseConfig.economyCurrency) >= configValue.purchaseConfig.showerItemAmount;
            else
			{
				int itemAmount = 0;
				List<Item> playerItems = new List<Item>();
				playerItems.AddRange(player.inventory.containerMain.itemList);
				playerItems.AddRange(player.inventory.containerBelt.itemList);
				playerItems.AddRange(player.inventory.containerWear.itemList);
				foreach (var item in playerItems)
				{
					if (item.info.shortname == configValue.purchaseConfig.requiredShortname && item.skin == configValue.purchaseConfig.requiredSkin)
					{
						itemAmount += item.amount;
					}
				}
				hasRequiredItem = itemAmount >= configValue.purchaseConfig.showerItemAmount;
			}

            if (!hasRequiredItem) return;
            if (configValue.purchaseConfig.requiredShortname == "")
                TakeCurrency(player, configValue.purchaseConfig.showerItemAmount, configValue.purchaseConfig.economyPlugin, configValue.purchaseConfig.economyCurrency);
			else
			{
				int remainingAmount = configValue.purchaseConfig.showerItemAmount;

				// Collect items from all inventory containers (main, belt, and wear)
				List<Item> playerItems = new List<Item>();
				playerItems.AddRange(player.inventory.containerMain.itemList);
				playerItems.AddRange(player.inventory.containerBelt.itemList);
				playerItems.AddRange(player.inventory.containerWear.itemList);

				// Loop through all items
				foreach (var item in playerItems)
				{
					if (item.info.shortname == configValue.purchaseConfig.requiredShortname && item.skin == configValue.purchaseConfig.requiredSkin)
					{
						if (item.amount < remainingAmount)
						{
							remainingAmount -= item.amount;
							item.GetHeldEntity()?.Kill(); // Kill the held entity (if applicable)
							item.RemoveFromContainer();   // Remove the item from its container
							item.Remove();                // Remove the item entirely
						}
						else
						{
							item.amount -= remainingAmount; // Reduce item amount by the remaining required amount
							item.MarkDirty();               // Mark the item as dirty to update the inventory
							break;                           // Break after deducting the required amount
						}
					}
				}
			}

            if (configValue.purchaseConfig.showerCooldown > 0)
            {
                cooldowns.TryAdd(player.userID, new Dictionary<string, int>());
                cooldowns[player.userID].TryAdd(profile, 0);
                cooldowns[player.userID][profile] = configValue.purchaseConfig.showerCooldown;
                CheckCooldownTimer();
            }
            if (config.enableLogs)
                Puts($"Player {player.displayName} ({player.UserIDString}) purchased meteor event of profile '{profile}'.");
            SendReply(player, Lang("SuccessfullyPurchasedEvent", player.UserIDString));
            RunMeteorShower(profile, configValue.purchaseConfig.showerMeteorAmount, configValue.purchaseConfig.isDirect, player, configValue.purchaseConfig.allowLarge);
        }

        private object OnMeleeAttack(BasePlayer player, HitInfo info)
        {
            if (info.HitEntity == null) return null;
            SphereEntity sphere = info.HitEntity.GetParentEntity() as SphereEntity;
            if (sphere == null) return null;
            MeteorController controller = sphere.GetComponent<MeteorController>();
            if (controller == null) return null;
            MeteorPrefabConfig configValue = config.meteorPrefabs[controller.profile];
            string shortname = player.GetActiveItem()?.info?.shortname ?? "";
            if (controller.largeMeteor)
            {
                if (shortname == "" || !configValue.bigMeteors.explosiveConfig.ContainsKey(shortname))
                {
                    SendReply(player, Lang($"WrongToolToGather_{controller.profile}", player.UserIDString));
                    return false;
                }
            }
            if (configValue.toolBlacklist.Contains(shortname))
            {
                SendReply(player, Lang("ToolInBlacklist", player.UserIDString));
                return false;
            }
            if (configValue.toolWhitelist.Count > 0 && !configValue.toolWhitelist.Contains(shortname))
            {
                SendReply(player, Lang("ToolNotInWhitelist", player.UserIDString));
                return false;
            }
            OreResourceEntity ore = info.HitEntity as OreResourceEntity;
            if (ore != null)
                InitializeCustomGatherMethod(info, ore);
            else
                InitializeFullyCustomGatherMethod(player, info, controller, configValue);
            return false;
        }

        private void OnPlayerConnected() => FixMarkerVisibility();

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity) => entity.OwnerID = player.userID;

        private void OnRocketLaunched(BasePlayer player, BaseEntity entity) => entity.OwnerID = player.userID;

        private void OnEntityKill(TimedExplosive explosive)
        {
            if (!checkExplosives || explosive.OwnerID == 0) return;
            OreResourceEntity node = explosive.GetParentEntity() as OreResourceEntity;
            if (node == null)
            {
                if (explosive.GetComponent<ServerProjectile>() == null) return;
                List<OreResourceEntity> nearbyOres = Pool.GetList<OreResourceEntity>();
                Vis.Entities(explosive.transform.position, 1, nearbyOres);
                foreach (var nearbyNode in nearbyOres)
                {
                    SphereEntity nearbySphere = nearbyNode.GetParentEntity() as SphereEntity;
                    if (nearbySphere == null) continue;
                    MeteorController nearbyController = nearbySphere.GetComponent<MeteorController>();
                    if (nearbyController == null || !nearbyController.largeMeteor) continue;
                    BasePlayer player1 = BasePlayer.FindByID(explosive.OwnerID);
                    if (player1 == null) continue;
                    DamageNode(nearbyNode, nearbyController, player1, explosive.ShortPrefabName);
                }
                Pool.FreeList(ref nearbyOres);
                return;
            }
            SphereEntity sphere = node.GetParentEntity() as SphereEntity;
            if (sphere == null) return;
            MeteorController controller = sphere.GetComponent<MeteorController>();
            if (controller == null || !controller.largeMeteor) return;
            BasePlayer player2 = BasePlayer.FindByID(explosive.OwnerID);
            if (player2 == null) return;
            DamageNode(node, controller, player2, explosive.ShortPrefabName);
        }

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player)
        {
            if (projectile.primaryMagazine.ammoType.shortname != "ammo.rifle.explosive") return;
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, config.allMeteors.maxDistanceAmmo))
            {
                OreResourceEntity hitRock = hit.GetCollider().ToBaseEntity() as OreResourceEntity;
                if (hitRock == null) return;
                SphereEntity sphere = hitRock.GetParentEntity() as SphereEntity;
                if (sphere == null) return;
                MeteorController controller = sphere.GetComponent<MeteorController>();
                if (controller == null || !controller.largeMeteor) return;
                DamageNode(hitRock, controller, player, "ammo.rifle.explosive");
            }
        }

        private void OnEntityKill(OreResourceEntity rock) => CheckForMeteorKill(rock);

        private void CheckForMeteorKill(OreResourceEntity rock)
        {
            if (rock.transform.position.y < 0) return;
            SphereEntity sphere = rock.GetParentEntity() as SphereEntity;
            if (sphere == null)
            {
                if (!config.randomDirectEventsEnabled) return;
                RunRandomMeteorEvent(rock);
                return;
            }
            MeteorController controller = sphere.GetComponent<MeteorController>();
            if (controller == null) return;
            controller.KillMeteor();
        }

        private void RunRandomMeteorEvent(OreResourceEntity rock)
        {
            foreach (var randomEvent in config.randomDirectEvents)
            {
                if (Core.Random.Range(0f, 100f) > randomEvent.chance) continue;
                int meteorCount = randomEvent.minMeteors == randomEvent.maxMeteors ? randomEvent.minMeteors : Core.Random.Range(randomEvent.minMeteors, randomEvent.maxMeteors + 1);
                List<BasePlayer> nearbyPlayers = Pool.GetList<BasePlayer>();
                Vis.Entities(rock.transform.position, 2.5f, nearbyPlayers);
                if (nearbyPlayers.Count == 0)
                {
                    Pool.FreeList(ref nearbyPlayers);
                    return;
                }
                RunMeteorShower(randomEvent.profileName, meteorCount, true, nearbyPlayers[0], randomEvent.allowLarge);
                Pool.FreeList(ref nearbyPlayers);
            }
        }

        private static void FixMarkerVisibility()
        {
            foreach (var meteor in meteors)
                foreach (var child in meteor.children)
                    if (child is MapMarkerGenericRadius marker)
                        marker.SendUpdate();
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            OreResourceEntity node = dispenser._baseEntity?.GetComponent<OreResourceEntity>();
            if (node == null) return;
            TryGiveBonusItems(node, player, item, true);
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            OreResourceEntity node = dispenser._baseEntity?.GetComponent<OreResourceEntity>();
            if (node == null) return;
            TryGiveBonusItems(node, player, item);
        }

        private static void TryGiveBonusItems(OreResourceEntity node, BasePlayer player, Item item, bool finalHit = false)
        {
            SphereEntity sphere = node.GetParentEntity() as SphereEntity;
            if (sphere == null) return;
            MeteorController controller = sphere.GetComponent<MeteorController>();
            if (controller == null) return;
            MeteorPrefabConfig configValue = config.meteorPrefabs[controller.profile];
            item.amount = (int)Mathf.Round(item.amount * configValue.meteorYieldMultiplier);
            if (configValue.scaleMeteorYield)
                item.amount = (int)Mathf.Round(item.amount * sphere.lerpRadius);
            foreach (var bonusItem in configValue.additionalOutput)
            {
                if (bonusItem.lastHitOnly && !finalHit) continue;
                if (bonusItem.requiredAmount > 0 && item.amount < bonusItem.requiredAmount) continue;
                if (Core.Random.Range(0f, 100f) <= bonusItem.chance)
                {
                    Item bonus = ItemManager.CreateByName(bonusItem.shortname, bonusItem.amount, bonusItem.skinId);
                    if (bonusItem.name != "")
                        bonus.name = bonusItem.name;
                    player.GiveItem(bonus);
                }
            }
        }

        private static void TryGiveCustomBonusItems(BaseEntity entity, BasePlayer player, int damageDealt, bool finalHit = false)
        {
            SphereEntity sphere = entity.GetParentEntity() as SphereEntity;
            if (sphere == null) return;
            MeteorController controller = sphere.GetComponent<MeteorController>();
            if (controller == null) return;
            MeteorPrefabConfig configValue = config.meteorPrefabs[controller.profile];
            foreach (var bonusItem in configValue.additionalOutput)
            {
                if (bonusItem.lastHitOnly && !finalHit) continue;
                if (bonusItem.requiredAmount > 0 && damageDealt < bonusItem.requiredAmount) continue;
                if (Core.Random.Range(0f, 100f) <= bonusItem.chance)
                {
                    Item bonus = ItemManager.CreateByName(bonusItem.shortname, bonusItem.amount, bonusItem.skinId);
                    if (bonusItem.name != "")
                        bonus.name = bonusItem.name;
                    player.GiveItem(bonus);
                }
            }
        }

        private void InitializeFullyCustomGatherMethod(BasePlayer player, HitInfo info, MeteorController controller, MeteorPrefabConfig configValue)
        {
            BaseCombatEntity entity = info.HitEntity as BaseCombatEntity;
            if (entity == null)
            {
                Puts($"You've set {info.HitEntity.ShortPrefabName} as meteor, but it isn't an BaseCombatEntity and can't be used as meteor!");
                return;
            }
#if CARBON
            Item tool = info.Weapon?.cachedItem;
#else
            Item tool = info.Weapon.GetCachedItem();
#endif
            if (tool == null) return;
            string itemShortname = configValue.customGather.customGatherTools.ContainsKey(tool.info.shortname) ? tool.info.shortname : "*";
            if (!configValue.customGather.customGatherTools.ContainsKey(itemShortname))
            {
                SendReply(player, Lang($"WrongToolToGather_{controller.profile}", player.UserIDString));
                return;
            }
            CustomToolConfig customToolConfig = configValue.customGather.customGatherTools[itemShortname];
            if (customToolConfig.requireSkin && tool.skin != customToolConfig.skin)
            {
                SendReply(player, Lang($"WrongToolToGather_{controller.profile}", player.UserIDString));
                return;
            }
            float dealtDmg = entity.MaxHealth() / 100f * customToolConfig.damageDealtPercentage;
            entity.health -= dealtDmg;
            entity.SendNetworkUpdate();
            bool lastHit = entity.health <= 0;
            foreach (var resource in configValue.customGather.meteorItems)
            {
                if (resource.lastHitOnly && !lastHit) continue;
                float percentage = lastHit && resource.lastHitOnly ? 100f : customToolConfig.damageDealtPercentage;
                int resourceAmount = (int)Mathf.Round(resource.amount / 100f * percentage);
                if (resourceAmount == 0) continue;
                Item outputItem = ItemManager.CreateByName(resource.shortname, resourceAmount, resource.skinId);
                if (resource.name != "")
                    outputItem.name = resource.name;
                player.GiveItem(outputItem);
            }
            TryGiveCustomBonusItems(entity, player, Mathf.CeilToInt(dealtDmg), lastHit);
            if (lastHit)
            {
                entity.Kill(BaseNetworkable.DestroyMode.Gib);
                controller.KillMeteor();
            }
        }

        #region Edited RUST Gather Code

#if !CARBON
        private static readonly Dictionary<OreResourceEntity, int> bonusesKilled = new Dictionary<OreResourceEntity, int>();
#endif

        private static void InitializeCustomGatherMethod(HitInfo info, OreResourceEntity ore)
        {
            if (!info.DidGather && info.gatherScale > 0f)
            {
                Jackhammer jackhammer = info.Weapon as Jackhammer;
                if (ore._hotSpot || jackhammer)
                {
                    if (ore._hotSpot == null)
                        ore._hotSpot = ore.SpawnBonusSpot(ore.lastNodeDir);
#if CARBON
                    if ((ore._hotSpot != null && Vector3.Distance(info.HitPositionWorld, ore._hotSpot.transform.position) <= ore._hotSpot.GetComponent<SphereCollider>().radius * 1.5f) || jackhammer != null)
                    {
                        float num = (jackhammer == null) ? 1f : jackhammer.HotspotBonusScale;
                        ore.bonusesKilled++;
                        info.gatherScale = 1f + Mathf.Clamp((float)ore.bonusesKilled * 0.5f, 0f, 2f * num);
                        if (ore._hotSpot != null)
                        {
                            ore._hotSpot.FireFinishEffect();
                            ore.ClientRPC<int, Vector3>(null, "PlayBonusLevelSound", ore.bonusesKilled, ore._hotSpot.transform.position);
                        }
                    }
                    else if (ore.bonusesKilled > 0)
                    {
                        ore.bonusesKilled = 0;
                        Effect.server.Run(ore.bonusFailEffect.resourcePath, ore.transform.position, ore.transform.up, null, false);
                    }
                    if (ore.bonusesKilled > 0)
                        ore.CleanupBonus();
#else
                    bonusesKilled.TryAdd(ore, 0);
                    if ((ore._hotSpot != null && Vector3.Distance(info.HitPositionWorld, ore._hotSpot.transform.position) <= ore._hotSpot.GetComponent<SphereCollider>().radius * 1.5f) || jackhammer != null)
                    {
                        float num = (jackhammer == null) ? 1f : jackhammer.HotspotBonusScale;
                        bonusesKilled[ore]++;
                        info.gatherScale = 1f + Mathf.Clamp((float)bonusesKilled[ore] * 0.5f, 0f, 2f * num);
                        if (ore._hotSpot != null)
                        {
                            ore._hotSpot.FireFinishEffect();
                            ore.ClientRPC<int, Vector3>(null, "PlayBonusLevelSound", bonusesKilled[ore], ore._hotSpot.transform.position);
                        }
                    }
                    else if (bonusesKilled[ore] > 0)
                    {
                        bonusesKilled[ore] = 0;
                        Effect.server.Run(ore.bonusFailEffect.resourcePath, ore.transform.position, ore.transform.up, null, false);
                    }
                    if (bonusesKilled[ore] > 0)
                        ore.CleanupBonus();
#endif
                }
            }
            if (ore._hotSpot == null)
                ore.DelayedBonusSpawn();
            if (ore.isServer && !ore.IsDestroyed)
            {
                if (ore.resourceDispenser != null)
                {
                    if (!ore.resourceDispenser.baseEntity.isServer)
                        return;
                    if (info.DidGather)
                        return;
                    if (ore.resourceDispenser.gatherType == ResourceDispenser.GatherType.UNSET)
                    {
                        Debug.LogWarning("Object :" + ore.resourceDispenser.gameObject.name + ": has unset gathertype!");
                        return;
                    }
                    BaseMelee baseMelee = (info.Weapon == null) ? null : (info.Weapon as BaseMelee);
                    float num;
                    float num2;
                    if (baseMelee != null)
                    {
                        ResourceDispenser.GatherPropertyEntry gatherInfoFromIndex = baseMelee.GetGatherInfoFromIndex(ore.resourceDispenser.gatherType);
                        num = gatherInfoFromIndex.gatherDamage * info.gatherScale;
                        num2 = gatherInfoFromIndex.destroyFraction;
                        if (num == 0f)
                            return;
                        baseMelee.SendPunch(new Vector3(UnityEngine.Random.Range(0.5f, 1f), UnityEngine.Random.Range(-0.25f, -0.5f), 0f) * -30f * (gatherInfoFromIndex.conditionLost / 6f), 0.05f);
                        baseMelee.LoseCondition(gatherInfoFromIndex.conditionLost);
                        if (!baseMelee.IsValid() || baseMelee.IsBroken())
                            return;
                        info.DidGather = true;
                    }
                    else
                    {
                        num = info.damageTypes.Total();
                        num2 = 0.5f;
                    }
                    float num3 = ore.resourceDispenser.fractionRemaining;
#if CARBON
                    ore.resourceDispenser.GiveResources(info.InitiatorPlayer, num, num2, info.Weapon);
                    ore.resourceDispenser.UpdateFraction();
#else
                    MethodInfo giveResourcesMethod = ore.resourceDispenser.GetType().GetMethod("GiveResources", BindingFlags.NonPublic | BindingFlags.Instance);
                    giveResourcesMethod.Invoke(ore.resourceDispenser, new object[] { info.InitiatorPlayer, num, num2, info.Weapon });
                    MethodInfo updateFractionMethod = ore.resourceDispenser.GetType().GetMethod("UpdateFraction", BindingFlags.NonPublic | BindingFlags.Instance);
                    updateFractionMethod.Invoke(ore.resourceDispenser, new object[] { });
#endif
                    float damageAmount;
                    if (ore.resourceDispenser.fractionRemaining <= 0f)
                    {
                        damageAmount = ore.resourceDispenser.baseEntity.MaxHealth();
                        if (info.DidGather && num2 < ore.resourceDispenser.maxDestroyFractionForFinishBonus)
                            ore.resourceDispenser.AssignFinishBonus(info.InitiatorPlayer, 1f - num2, info.Weapon);
                    }
                    else
                        damageAmount = (num3 - ore.resourceDispenser.fractionRemaining) * ore.resourceDispenser.baseEntity.MaxHealth();
                    HitInfo hitInfo = new HitInfo(info.Initiator, ore.resourceDispenser.baseEntity, Rust.DamageType.Generic, damageAmount, ore.resourceDispenser.transform.position)
                    {
                        gatherScale = 0f,
                        PointStart = info.PointStart,
                        PointEnd = info.PointEnd,
                        WeaponPrefab = info.WeaponPrefab,
                        Weapon = info.Weapon
                    };
                    ore.resourceDispenser.baseEntity.OnAttacked(hitInfo);
                }
                if (!info.DidGather)
                {
                    if (ore.baseProtection)
                        ore.baseProtection.Scale(info.damageTypes, 1f);
                    float num = info.damageTypes.Total();
                    ore.health -= num;
                    if (ore.health <= 0f)
                        ore.OnKilled(info);
#if CARBON
                    ore.OnHealthChanged();
#else
                    MethodInfo healthChangeMethod = ore.GetType().GetMethod("OnHealthChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                    healthChangeMethod.Invoke(ore, new object[] { });
#endif
                }
            }
        }
        #endregion

        private static void DamageNode(OreResourceEntity node, MeteorController controller, BasePlayer player, string prefabName)
        {
            if (node.IsDestroyed) return;
            MeteorPrefabConfig configValue = config.meteorPrefabs[controller.profile];
            if (!configValue.bigMeteors.explosiveConfig.ContainsKey(prefabName)) return;
            float multiplier = configValue.bigMeteors.explosiveConfig[prefabName].resourceMultiplier;
            node.health -= configValue.bigMeteors.explosiveConfig[prefabName].damageDealt;
            node.UpdateNetworkStage();
            foreach (var item in configValue.bigMeteors.output)
            {
                if (item.lastHitOnly && node.health > 0) continue;
                if (item.chance < 100 && Core.Random.Range(0f, 100f) > item.chance) continue;
                Item createdItem = ItemManager.CreateByName(item.shortname, (int)Mathf.Round(item.amount * multiplier), item.skinId);
                if (item.name != "")
                    createdItem.name = item.name;
                if (configValue.bigMeteors.callHook)
                    Interface.CallHook("OnDispenserGather", node.resourceDispenser, player, createdItem);
                player.GiveItem(createdItem);
            }
            if (node.health <= 0)
            {
                Effect.server.Run("assets/prefabs/misc/orebonus/effects/ore_finish.prefab", node.transform.position);
                controller.KillMeteor();
            }
        }

        private void RunTimedEvent(string keyName)
        {
            TimedEventConfig configValue = config.timedEvents[keyName];
            float time = configValue.timerMin == configValue.timerMax ? configValue.timerMin : Core.Random.Range(configValue.timerMin, configValue.timerMax);
            timer.Once(time, () => ExecuteTimedEvent(keyName, configValue));
        }

        private void ExecuteTimedEvent(string keyName, TimedEventConfig configValue)
        {
            if (configValue.eventChance < 100 && Core.Random.Range(0f, 100f) > configValue.eventChance)
            {
                if (config.enableLogs)
                    Puts($"Timed event {keyName} has been skipped due to chance not rolled.");
                RunTimedEvent(keyName);
                return;
            }
            if (meteors.Count > config.allMeteors.maxMeteors)
            {
                if (config.enableLogs)
                    Puts($"Timed event {keyName} has been skipped due to too many meteors on map.");
                RunTimedEvent(keyName);
                return;
            }
            float meteorCount = configValue.meteorCount;
            if (configValue.basedOnOnline)
                meteorCount *= BasePlayer.activePlayerList.Count;
            if (meteorCount > configValue.maxMeteorCount)
                meteorCount = configValue.maxMeteorCount;
            if (meteorCount == 0)
            {
                if (config.enableLogs)
                    Puts("There is not enough players to start meteor event. Skipping event...");
                RunTimedEvent(keyName);
                return;
            }
            int sumWeight = 0;
            foreach (var profile in configValue.spawnProfiles)
                sumWeight += profile.Value;
            int rolledProfile = Core.Random.Range(0, sumWeight + 1);
            sumWeight = 0;
            foreach (var profile in configValue.spawnProfiles)
            {
                sumWeight += profile.Value;
                if (sumWeight >= rolledProfile)
                {
                    int meteorCountInt = (int)Mathf.Round(meteorCount);
                    foreach (var command in configValue.commandsToRun)
                        Server.Command(command.Replace("{meteorCount}", meteorCountInt.ToString()));
                    if (configValue.webhook.enabled)
                        SendWebhook(configValue, meteorCountInt);
                    RunMeteorShower(profile.Key, meteorCountInt, configValue.directPlayer);
                    break;
                }
            }
            RunTimedEvent(keyName);
        }

        private void SendWebhook(TimedEventConfig configValue, int meteorCount)
        {
            string message = configValue.webhook.embed ? webhookEmbed.Replace("-5", configValue.webhook.color.ToString()).Replace(true.ToString(), configValue.webhook.inline.ToString().ToLower()).Replace("{title}", configValue.webhook.title).Replace("{message}", string.Format(configValue.webhook.message, meteorCount)) : webhookText.Replace("{message}", string.Format(configValue.webhook.message, meteorCount));
            webrequest.Enqueue(configValue.webhook.webhook, message, (i, s) =>
            {
                if (i != 204)
                    PrintWarning($"Cannot send Discord Webhook. Error {i}:\n{s}");
            }, this, RequestMethod.POST, new Dictionary<string, string> { { "Content-Type", "application/json" } });
        }

        private void RunMeteorShower(string profile, int meteorCount, bool direct = false, BasePlayer directPlayer = null, bool allowLarge = true)
        {
            MeteorConfig configValue = config.profiles[profile];
            bool soundPrefab = configValue.soundPrefab != "";
            if (!direct && (soundPrefab || configValue.message))
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (soundPrefab)
                        Effect.server.Run(configValue.soundPrefab, player.eyes.position);
                    if (configValue.message)
                        SendReply(player, Lang($"MeteorShowerIncoming_{profile}", player.UserIDString, meteorCount));
                }
            float timeBetween = configValue.maxMeteorShowerLength / meteorCount;
            Vector3 angle = configValue.directFalling;
            Vector3 offset = Vector3.zero;
            if (!direct)
            {
                int sumWeight = 0;
                foreach (var angleValue in configValue.angleConfig)
                    sumWeight += angleValue.weight;
                int rolledAngle = Core.Random.Range(0, sumWeight + 1);
                sumWeight = 0;
                foreach (var angleValue in configValue.angleConfig)
                {
                    sumWeight += angleValue.weight;
                    if (sumWeight >= rolledAngle)
                    {
                        angle = angleValue.angle;
                        offset = angleValue.offset;
                        break;
                    }
                }
            }
            int summedMeteorProfileWeight = 0;
            foreach (var meteorProfile in configValue.meteorProfiles)
                summedMeteorProfileWeight += meteorProfile.Value;
            int counter = 0;
            float worldSize = World.Size / config.allMeteors.meteorSpread;
            if (config.enableLogs)
                Puts($"Started meteor shower of profile '{profile}' with {meteorCount} meteors. (direct: {direct}, directPlayer: {directPlayer != null})");
            if (config.enableHooks)
                Interface.CallHook("OnMeteorShowerStart", meteorCount, profile, direct, directPlayer);
            timer.Repeat(configValue.maxMeteorShowerLength / meteorCount, meteorCount, () => {
                Vector3 targetPos;
                if (direct)
                {
                    if (directPlayer != null)
                    {
                        Vector3 playerPos = directPlayer.transform.position;
                        targetPos = new Vector3(playerPos.x + Core.Random.Range(-configValue.impactRadius, configValue.impactRadius), configValue.spawnHeight, playerPos.z + Core.Random.Range(-configValue.impactRadius, configValue.impactRadius));
                    }
                    else
                    {
                        int playerIndex = counter % BasePlayer.activePlayerList.Count;
                        Vector3 targetPlayerPos = BasePlayer.activePlayerList[playerIndex].transform.position;
                        targetPos = new Vector3(targetPlayerPos.x + Core.Random.Range(-configValue.impactRadius, configValue.impactRadius), configValue.spawnHeight, targetPlayerPos.z + Core.Random.Range(-configValue.impactRadius, configValue.impactRadius));
                    }
                }
                else
                {
                    float randX = Core.Random.Range(-worldSize, worldSize);
                    float randZ = Core.Random.Range(-worldSize, worldSize);
                    targetPos = new Vector3(randX + offset.x, configValue.spawnHeight + offset.y, randZ + offset.z);
                }
                int rolledProfile = Core.Random.Range(0, summedMeteorProfileWeight + 1);
                int profileCounter = 0;
                foreach (var meteorProfile in configValue.meteorProfiles)
                {
                    profileCounter += meteorProfile.Value;
                    if (profileCounter >= rolledProfile)
                    {
                        SpawnMeteor(meteorProfile.Key, targetPos, angle, allowLarge);
                        break;
                    }
                }
                counter++;
            });
        }

        private void SpawnMeteor(string meteorKey, Vector3 position, Vector3 angle, bool allowLarge = true)
        {
            MeteorPrefabConfig configValue = config.meteorPrefabs[meteorKey];
            BaseEntity meteorEntity = GameManager.server.CreateEntity(configValue.meteorPrefab, position);
            if (meteorEntity == null)
            {
                Puts($"Meteor entity {configValue.meteorPrefab} is not valid entity path and it won't be spawned!");
                return;
            }
            meteorEntity.transform.rotation = Quaternion.Euler(configValue.randomizeXrotation ? Core.Random.Range(0, 360) : 0, configValue.randomizeYrotation ? Core.Random.Range(0, 360) : 0, configValue.randomizeZrotation ? Core.Random.Range(0, 360) : 0);
            SphereEntity sphere = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", position) as SphereEntity;
            Rigidbody rb = sphere.GetOrAddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 0.01f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.AddRelativeForce(angle, ForceMode.Force);
            sphere.Spawn();
            fallingMeteors.Add(sphere);
            if (configValue.atmosphereEnterSound)
                Effect.server.Run("assets/content/vehicles/mlrs/effects/pfx_airburst.prefab", sphere.transform.position);
            sphere.gameObject.layer = 0;
            FallingMeteorController controller = sphere.GetOrAddComponent<FallingMeteorController>();
            controller.SetupProfile(meteorKey);
            controller.attachedEntity = configValue.meteorPrefab;
            float meteorScale = Core.Random.Range(configValue.meteorScaleMin, configValue.meteorScaleMax);
            if (allowLarge && configValue.bigMeteors.bigMeteorChance > 0)
            {
                if (Core.Random.Range(0f, 100f) < configValue.bigMeteors.bigMeteorChance)
                {
                    meteorScale = Core.Random.Range(configValue.bigMeteors.meteorScaleMin, configValue.bigMeteors.meteorScaleMax);
                    controller.largeMeteor = true;
                }
            }
            sphere.LerpRadiusTo(meteorScale, 1000);
            SphereCollider col = sphere.GetOrAddComponent<SphereCollider>();
            col.radius = 1f;
            col.isTrigger = true;
            if (config.allMeteors.checkForMeshCollider)
            {
                OreResourceEntity oreEnt = meteorEntity as OreResourceEntity;
                if (oreEnt != null)
                {
                    foreach (var stage in oreEnt.stages)
                    {
                        MeshCollider col2 = stage.instance.GetComponent<MeshCollider>();
                        col2?.SetActive(false);
                    }
                }
            }
            meteorEntity.globalBroadcast = true;
            meteorEntity.Spawn();
            meteorEntity.SetParent(sphere, true);
            meteorEntity.syncPosition = false;
            if (configValue.meteorSmoke)
            {
                BaseEntity smoke = GameManager.server.CreateEntity("assets/prefabs/tools/smoke grenade/grenade.smoke.deployed.prefab", sphere.transform.position);
                UnityEngine.Object.Destroy(smoke.GetComponent<Rigidbody>());
                smoke.Spawn();
                smoke.SetParent(sphere, true);
                smoke.syncPosition = false;
            }
            if (configValue.meteorFlame)
            {
                BaseEntity flame = GameManager.server.CreateEntity("assets/prefabs/npc/m2bradley/oilfireball2.prefab", sphere.transform.position);
                UnityEngine.Object.Destroy(flame.GetComponent<Rigidbody>());
                flame.Spawn();
                flame.SetParent(sphere, true);
                flame.syncPosition = false;
            }
            if (config.enableHooks)
                Interface.CallHook("OnFallingMeteorSpawned", meteorKey, sphere, meteorEntity);
        }

        private class FallingMeteorController : FacepunchBehaviour
        {
            private SphereEntity sphere;
            private MeteorPrefabConfig configValue;
            private string profile = "";

            public string attachedEntity = "";
            public bool largeMeteor = false;

            private void Awake()
            {
                sphere = GetComponent<SphereEntity>();
            }

            public void SetupProfile(string _profile)
            {
                profile = _profile;
                configValue = config.meteorPrefabs[profile];
                if (configValue.maxAirTime > 0)
                    Invoke(() => DestroyFallingMeteor(false), configValue.maxAirTime);

            }

            private static readonly HashSet<int> lockedLayers = new HashSet<int> { -1, 18, 30 };

            private void OnTriggerEnter(Collider other)
            {
                int layer = other.gameObject?.layer ?? -1;
                if (!lockedLayers.Contains(layer))
                    DestroyFallingMeteor(true);
            }

            public void DestroyFallingMeteor(bool spawnLanded)
            {
                if (sphere == null || sphere.IsDestroyed) return;
                Effect.server.Run("assets/content/vehicles/mlrs/effects/pfx_mlrs_rocket_explosion_air.prefab", sphere.transform.position);
                foreach (var child in sphere.children)
                    if (child.prefabID == 1464001967 && !child.IsDestroyed) //Killing smoke
                    {
                        child.Kill();
                        break;
                    }
                fallingMeteors.Remove(sphere);
                if (spawnLanded)
                    SpawnLandedMeteor(sphere.lerpRadius);
                if (!sphere.IsDestroyed)
                    sphere.Kill();
                if (config.enableHooks && fallingMeteors.Count == 0)
                    Interface.CallHook("OnFallingMeteorsKilled");
            }

            private void SpawnLandedMeteor(float radius)
            {
                Vector3 spherePos = sphere.transform.position;
				bool isInWater;
				if (config.allMeteors.destroyUnderwater && sphere.IsInWaterVolume(spherePos, out isInWater) && isInWater) 
				{
					return;
				}
                int topology = TerrainMeta.TopologyMap.GetTopology(spherePos);
                if (config.allMeteors.roadKill && ((topology & 2048) != 0 || (topology & 4096) != 0)) return;
                if (config.allMeteors.railKill && ((topology & 524288) != 0 || (topology & 1048576) != 0)) return;
                if (config.allMeteors.monumentKill && (topology & 1024) != 0) return;
                RaycastHit hit;
                if (Physics.Raycast(spherePos + new Vector3(0, 10, 0), Vector3.down, out hit, config.allMeteors.groundHeightCheck, LayerMask.GetMask("Terrain", "World")))
                {
                    if (config.allMeteors.destroyTrees > 0)
                        DestroyTrees();
                    SphereEntity sphereEntity = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", hit.point) as SphereEntity;
                    sphereEntity.Spawn();
                    MeteorController controller = sphereEntity.GetOrAddComponent<MeteorController>();
                    controller.SetupMeteor(profile, largeMeteor);
                    meteors.Add(sphereEntity);
                    sphereEntity.LerpRadiusTo(radius, 1000);
                    Quaternion rotation = Quaternion.Euler(configValue.randomizeXrotationLanded ? Core.Random.Range(0, 360) : 0, configValue.randomizeYrotationLanded ? Core.Random.Range(0, 360) : 0, configValue.randomizeZrotationLanded ? Core.Random.Range(0, 360) : 0);
                    Vector3 spawnPosOffset = hit.point + configValue.landedOffset;
                    BaseEntity meteorEntity = GameManager.server.CreateEntity(attachedEntity, spawnPosOffset, rotation);
                    meteorEntity.globalBroadcast = true;
                    meteorEntity.Spawn();
                    meteorEntity.SetParent(sphereEntity, true);
                    meteorEntity.syncPosition = false;
                    if (configValue.radiationChance > 0 && Core.Random.Range(0f, 100f) < configValue.radiationChance)
                    {
                        SphereCollider col = sphereEntity.GetOrAddComponent<SphereCollider>();
                        col.radius = radius;
                        col.isTrigger = true;
                        TriggerRadiation radiation = sphereEntity.GetOrAddComponent<TriggerRadiation>();
                        radiation.interestLayers.value = 131072;
                        float radiationAmount = configValue.radiationAmount;
                        if (configValue.scaleRadiation)
                            radiationAmount *= radius;
                        radiation.RadiationAmountOverride = radiationAmount;
                    }
                    if (largeMeteor && configValue.bigMeteors.crateChance > 0 && Core.Random.Range(0f, 100f) < configValue.bigMeteors.crateChance)
                        AddCrate(spawnPosOffset, rotation);
                    AddMeteorMarker(sphereEntity, spawnPosOffset);
                    if (config.enableHooks)
                        Interface.CallHook("OnMeteorLanded", meteorEntity, profile);
                }
            }

            private void AddCrate(Vector3 spawnPosOffset, Quaternion rotation)
            {
                int crateWeight = 0;
                foreach (var crate in configValue.bigMeteors.crates)
                    crateWeight += crate.Value;
                int rolledCrate = Core.Random.Range(0, crateWeight + 1);
                crateWeight = 0;
                foreach (var crate in configValue.bigMeteors.crates)
                {
                    crateWeight += crate.Value;
                    if (crateWeight >= rolledCrate)
                    {
                        BaseEntity crateEntity = GameManager.server.CreateEntity(crate.Key, spawnPosOffset, rotation);
                        if (crateEntity == null)
                        {
                            Debug.Log($"[MeteorEvent ERROR] Meteor tried to spawn {crate.Key} inside meteor but entity is not valid path!");
                            break;
                        }
                        crateEntity.Spawn();
                        Rigidbody rig = crateEntity.GetComponent<Rigidbody>();
                        if (rig != null)
                            rig.freezeRotation = true;
                        break;
                    }
                }
            }

            private void AddMeteorMarker(SphereEntity sphereEntity, Vector3 spawnPosOffset)
            {
                string markerType = configValue.markerConfig.markerType.ToLower();
                if (markerType != "none")
                {
                    if (markerType == "explosion")
                    {
                        MapMarkerExplosion marker = GameManager.server.CreateEntity("assets/prefabs/tools/map/explosionmarker.prefab", spawnPosOffset) as MapMarkerExplosion;
                        marker.SetParent(sphereEntity, true);
                        marker.Spawn();
                    }
                    else if (markerType == "normal")
                    {
                        if (configValue.markerConfig.markerText != "")
                        {
                            VendingMachineMapMarker vendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", spawnPosOffset) as VendingMachineMapMarker;
                            vendingMarker.markerShopName = configValue.markerConfig.markerText;
                            vendingMarker.SetParent(sphereEntity, true);
                            vendingMarker.Spawn();
                        }
                        MapMarkerGenericRadius marker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", spawnPosOffset) as MapMarkerGenericRadius;
                        marker.alpha = configValue.markerConfig.markerAlpha;
                        Color color1, color2;
                        if (ColorUtility.TryParseHtmlString(configValue.markerConfig.markerColor1, out color1))
                            marker.color1 = color1;
                        if (ColorUtility.TryParseHtmlString(configValue.markerConfig.markerColor2, out color2))
                            marker.color2 = color2;
                        marker.radius = configValue.markerConfig.markerRadius;
                        marker.SetParent(sphereEntity, true);
                        marker.Spawn();
                        marker.SendUpdate();
                    }
                }
            }

            private void DestroyTrees()
            {
                Vector3 spherePos = sphere.transform.position;
                List<TreeEntity> trees = Pool.GetList<TreeEntity>();
                Vis.Entities(spherePos, config.allMeteors.destroyTrees, trees);
                foreach (var tree in trees)
                    if (tree.OwnerID == 0)
                        tree.OnKilled(new HitInfo() { PointStart = spherePos, PointEnd = tree.transform.position });
                Pool.FreeList(ref trees);
            }
        }
        private class MeteorController : FacepunchBehaviour
        {
            private SphereEntity sphere;
            private MeteorPrefabConfig configValue;

            public string profile = "";
            public bool largeMeteor = false;
            public BaseEntity entityInside = null;

            private void Awake()
            {
                sphere = GetComponent<SphereEntity>();
            }

            public void SetupMeteor(string _profile, bool _largeMeteor)
            {
                profile = _profile;
                largeMeteor = _largeMeteor;
                configValue = config.meteorPrefabs[profile];
                if (configValue.meteorLifetime > 0)
                    Invoke(() => KillMeteor(), configValue.meteorLifetime);
            }

            public void KillMeteor()
            {
                if (entityInside != null && !entityInside.IsDestroyed)
                {
                    LootContainer loot = entityInside as LootContainer;
                    if (loot != null)
                    {
                        foreach (var item in loot.inventory.itemList.ToArray())
                        {
                            item.GetHeldEntity()?.Kill();
                            item.RemoveFromContainer();
                            item.Remove();
                        }
                    }
                    entityInside.Kill();
                }
                if (!sphere.IsDestroyed)
                    sphere.Kill();
            }

            private void OnDestroy()
            {
                meteors.Remove(sphere);
            }
        }

        private static string FormatTime(int seconds)
        {
            if (seconds < 60) return $"{seconds}s";
            int minutes = (seconds - seconds % 60) / 60;
            if (minutes < 60) return $"{minutes}m";
            int hours = (minutes - minutes % 60) / 60;
            if (hours < 25) return $"{hours}h";
            return $"{(hours - hours % 24) / 24}d";
        }

        private int HaveCurrency(BasePlayer player, int currencyPlugin, string currency)
        {
            return currencyPlugin switch
            {
                1 => (int)Math.Floor(Economics.Call<double>("Balance", player.userID)),
                2 => ServerRewards.Call<int>("CheckPoints", player.userID),
                3 => BankSystem.Call<int>("Balance", player.userID),
                4 => ShoppyStock.Call<int>("GetCurrencyAmount", currency, player.userID),
                _ => 0,
            };
        }

        private void TakeCurrency(BasePlayer player, int amount, int currencyPlugin, string currency)
        {
            switch (currencyPlugin)
            {
                case 1: Economics.Call("Withdraw", player.userID, Convert.ToDouble(amount)); break;
                case 2: ServerRewards.Call("TakePoints", player.userID, amount); break;
                case 3: BankSystem.Call("Withdraw", player.userID, amount); break;
                case 4: ShoppyStock.Call("TakeCurrency", currency, player.userID, amount); break;
            };
        }

        private static void SendJson(BasePlayer player, string json) => CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(player.net.connection), null, "AddUI", json);

        private void OpenMeteorEventUI(BasePlayer player)
        {
            SendJson(player, cachedJson);
            using CUI cui = new CUI(CuiHandler);
            CuiElementContainer corePanel = cui.CreateContainer("MeteorEventUI_updateModule", BetterColors.Transparent, 0, 0, 0, 0, 0, 0, 0, 0, 0f, 0f, false, false, CUI.ClientPanels.Under, null);
            LoadDynamicTopLeft(cui, corePanel, player);
            LoadDynamicBottomRight(cui, corePanel, player);
            cui.Send(corePanel, player);
        }

        private void LoadDynamicTopLeft(CUI cui, CuiElementContainer corePanel, BasePlayer player)
        {
            //Title
            cui.CreateText(corePanel, "MeteorEventUI_topPanel", BetterColors.LightGray, Lang("MeteorEventTitle", player.UserIDString), 18, 0f, 0f, 0f, 0f, 8, 276, 0, 26, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "MeteorEventUI_UiText2", null, false);
            //Select Event Title
            cui.CreateText(corePanel, "MeteorEventUI_mainBackground", BetterColors.LightGrayTransparent73, Lang("SelectEventTitle", player.UserIDString), 12, 0f, 0f, 0f, 0f, 0, 132, 180, 202, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "MeteorEventUI_selectEventTitle", null, false);
            cachedSelectedEvent.TryAdd(player.userID, "");
            bool isAdmin = permission.UserHasPermission(player.UserIDString, "meteorevent.admin");
            if (cachedSelectedEvent[player.userID] == "")
            {
                foreach (var profile in config.profiles)
                {
                    if (isAdmin)
                    {
                        cachedSelectedEvent[player.userID] = profile.Key;
                        break;
                    }
                    if (!profile.Value.purchaseConfig.showerAllowPurchasing) continue;
                    if (!profile.Value.purchaseConfig.requirePerm || (profile.Value.purchaseConfig.requirePerm && (permission.UserHasPermission(player.UserIDString, $"meteorevent.purchase.{profile.Key}") || isAdmin)))
                    {
                        cachedSelectedEvent[player.userID] = profile.Key;
                        break;
                    }
                }
            }
            string currEvent = cachedSelectedEvent[player.userID];
            if (currEvent == "") return;
            int startY = 0;
            foreach (var profile in config.profiles)
            {
                string key = profile.Key;
                if (isAdmin || (profile.Value.purchaseConfig.showerAllowPurchasing && (!profile.Value.purchaseConfig.requirePerm || (profile.Value.purchaseConfig.requirePerm && (permission.UserHasPermission(player.UserIDString, $"meteorevent.purchase.{key}") || isAdmin)))))
                {
                    if (currEvent == key)
                    {
                        //Event Button - Selected
                        cui.CreateProtectedButton(corePanel, "MeteorEventUI_selectEventTitle", BetterColors.GreenBackgroundTransparent, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 0, 132, startY - 20, startY, "", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, $"MeteorEventUI_eventButton_{key}", null, false);
                        //Event Button Text - Selected
                        cui.CreateText(corePanel, $"MeteorEventUI_eventButton_{key}", BetterColors.GreenText, Lang($"EventName_{key}", player.UserIDString), 11, 0f, 0f, 0f, 0f, 8, 132, 0, 20, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, $"MeteorEventUI_eventButton_{key}_text", null, false);
                    }
                    else
                    {
                        //Event Button
                        cui.CreateProtectedButton(corePanel, "MeteorEventUI_selectEventTitle", BetterColors.Transparent, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 0, 132, startY - 20, startY, $"UI_MeteorEvent switch {key}", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, $"MeteorEventUI_eventButton_{key}", null, false);
                        //Event Button Text
                        cui.CreateText(corePanel, $"MeteorEventUI_eventButton_{key}", BetterColors.LightGray, Lang($"EventName_{key}", player.UserIDString), 11, 0f, 0f, 0f, 0f, 8, 132, 0, 20, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, $"MeteorEventUI_eventButton_{key}_text", null, false);
                    }
                    startY -= 20;
                }
            }
        }

        private void LoadDynamicBottomRight(CUI cui, CuiElementContainer corePanel, BasePlayer player)
        {
            string currEvent = cachedSelectedEvent[player.userID];
            if (currEvent == "") return;
            MeteorConfig configValue = config.profiles[currEvent];
            //Event Name
            cui.CreateText(corePanel, "MeteorEventUI_rightPanel", BetterColors.LightGray, Lang($"EventName_{currEvent}", player.UserIDString), 16, 0f, 0f, 0f, 0f, 8, 239, 176, 198, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "MeteorEventUI_eventName", null, false);
            //Event Description
            cui.CreateText(corePanel, "MeteorEventUI_rightPanel", BetterColors.LightGrayTransparent73, Lang($"EventDescription_{currEvent}", player.UserIDString), 8, 0f, 0f, 0f, 0f, 8, 239, 100, 176, TextAnchor.UpperLeft, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "MeteorEventUI_eventDescription", null, false);
            //Cooldown Amount
            cui.CreateText(corePanel, "MeteorEventUI_cooldownPanel", BetterColors.LightGrayTransparent73, FormatTime(configValue.purchaseConfig.showerCooldown), 10, 0f, 0f, 0f, 0f, 16, 35, 0, 16, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "MeteorEventUI_cooldownAmount", null, false);
            //Meteor Amount Text
            cui.CreateText(corePanel, "MeteorEventUI_meteorAmountPanel", BetterColors.LightGrayTransparent73, configValue.purchaseConfig.showerMeteorAmount.ToString(), 10, 0f, 0f, 0f, 0f, 16, 35, 0, 16, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "MeteorEventUI_meteorAmount", null, false);
            //Requirements Title
            cui.CreateText(corePanel, "MeteorEventUI_rightPanel", BetterColors.LightGray, Lang("Requirements", player.UserIDString), 14, 0f, 0f, 0f, 0f, 8, 188, 66, 84, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "MeteorEventUI_requirementsTitle", null, false);
            bool hasRequiredItem;
            if (configValue.purchaseConfig.requiredShortname == "")
                hasRequiredItem = HaveCurrency(player, configValue.purchaseConfig.economyPlugin, configValue.purchaseConfig.economyCurrency) >= configValue.purchaseConfig.showerItemAmount;
            else
			{
				int itemAmount = 0;

				// Collect items from all inventory containers (main, belt, and wear)
				List<Item> playerItems = new List<Item>();
				playerItems.AddRange(player.inventory.containerMain.itemList);
				playerItems.AddRange(player.inventory.containerBelt.itemList);
				playerItems.AddRange(player.inventory.containerWear.itemList);

				// Loop through all items
				foreach (var item in playerItems)
				{
					if (item.info.shortname == configValue.purchaseConfig.requiredShortname && item.skin == configValue.purchaseConfig.requiredSkin)
					{
						itemAmount += item.amount;
					}
				}

				// Check if player has the required amount of the item
				hasRequiredItem = itemAmount >= configValue.purchaseConfig.showerItemAmount;
			}

            string textColor = hasRequiredItem ? BetterColors.LightGrayTransparent73 : BetterColors.RedText;
            //Requirement Text
            cui.CreateText(corePanel, "MeteorEventUI_requirementsTitle", textColor, Lang($"ItemRequirement_{currEvent}", player.UserIDString, configValue.purchaseConfig.showerItemAmount), 12, 0f, 0f, 0f, 0f, 0, 180, -17, 0, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "MeteorEventUI_requirementsText", null, false);
            if (permission.UserHasPermission(player.UserIDString, "meteorevent.admin"))
            {
                //Admin Set Panel
                cui.CreatePanel(corePanel, "MeteorEventUI_rightPanel", BetterColors.RedBackgroundTransparent, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 223, 300, 32, 72, false, 0f, 0f, false, false, null, null, false, "MeteorEventUI_adminSetPanel", null, false);
                //Admin Panel Title
                cui.CreateText(corePanel, "MeteorEventUI_adminSetPanel", BetterColors.FishingRed, Lang("AdminInputTitle", player.UserIDString), 8, 0f, 0f, 0f, 0f, 0, 77, 20, 40, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "MeteorEventUI_UiText6", null, false);
                //Admin Panel Input
                cui.CreateProtectedInputField(corePanel, "MeteorEventUI_adminSetPanel", BetterColors.RedText, "0", 15, 3, false, 0f, 0f, 0f, 0f, 0, 77, 0, 20, $"UI_MeteorEvent forceStart {currEvent}", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, false, false, UnityEngine.UI.InputField.LineType.SingleLine, 0f, 0f, false, false, "MeteorEventUI_adminSetInput", null, false);
            }
            bool onCooldown = cooldowns.ContainsKey(player.userID) && cooldowns[player.userID].ContainsKey(currEvent);
            string bottomText;
            if (!hasRequiredItem)
                bottomText = Lang("NoRequiredItems", player.UserIDString);
            else if (onCooldown)
                bottomText = Lang("EventOnCooldown", player.UserIDString, FormatTime(cooldowns[player.userID][currEvent]));
            else
                bottomText = Lang("CanStartEventInfo", player.UserIDString);
            bool locked = !hasRequiredItem || onCooldown;
            string buttonColor = locked ? BetterColors.RedBackgroundTransparent : BetterColors.GreenBackgroundTransparent;
            string buttonTextColor = locked ? BetterColors.RedText : BetterColors.GreenText;
            string buttonTextText = locked ? Lang("EventLocked", player.UserIDString) : Lang("RunEvent", player.UserIDString);
            string command = locked ? "" : $"UI_MeteorEvent start {currEvent}";
            //Info Text
            cui.CreateText(corePanel, "MeteorEventUI_bottomPanel", BetterColors.LightGrayTransparent73, bottomText, 7, 0f, 0f, 0f, 0f, 28, 219, 0, 28, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "MeteorEventUI_bottomInfoText", null, false);
            //Run Button
            cui.CreateProtectedButton(corePanel, "MeteorEventUI_bottomPanel", buttonColor, BetterColors.Transparent, "", 15, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 223, 300, 4, 24, command, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "MeteorEventUI_runButton", null, false);
            cui.CreateText(corePanel, "MeteorEventUI_runButton", buttonTextColor, buttonTextText, 15, font: CUI.Handler.FontTypes.RobotoCondensedBold, id: "MeteorEventUI_runButtonText");
        }

        private void UpdateDisplay(BasePlayer player, string oldKey)
        {
            using CUI cui = new CUI(CuiHandler);
            CUI.Handler.UpdatePool elements = cui.UpdatePool();
            string currEvent = cachedSelectedEvent[player.userID];
            //Event Button - New Selected
            elements.Add(cui.UpdateProtectedButton($"MeteorEventUI_eventButton_{currEvent}", BetterColors.GreenBackgroundTransparent, BetterColors.Transparent, "", 1, command: ""));
            //Event Button Text - New Selected
            elements.Add(cui.UpdateText($"MeteorEventUI_eventButton_{currEvent}_text", BetterColors.GreenText, Lang($"EventName_{currEvent}", player.UserIDString), 11, align: TextAnchor.MiddleLeft));
            //Event Button - Old Selected
            elements.Add(cui.UpdateProtectedButton($"MeteorEventUI_eventButton_{oldKey}", BetterColors.Transparent, BetterColors.Transparent, "", 1, command: $"UI_MeteorEvent switch {oldKey}"));
            //Event Button Text - Old Selected
            elements.Add(cui.UpdateText($"MeteorEventUI_eventButton_{oldKey}_text", BetterColors.LightGray, Lang($"EventName_{oldKey}", player.UserIDString), 11, align: TextAnchor.MiddleLeft));
            //Event Name
            elements.Add(cui.UpdateText("MeteorEventUI_eventName", BetterColors.LightGray, Lang($"EventName_{currEvent}", player.UserIDString), 16, align: TextAnchor.MiddleLeft, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            //Event Description
            elements.Add(cui.UpdateText("MeteorEventUI_eventDescription", BetterColors.LightGrayTransparent73, Lang($"EventDescription_{currEvent}", player.UserIDString), 8, align: TextAnchor.UpperLeft));
            MeteorConfig configValue = config.profiles[currEvent];
            //Cooldown Amount
            elements.Add(cui.UpdateText("MeteorEventUI_cooldownAmount", BetterColors.LightGrayTransparent73, FormatTime(configValue.purchaseConfig.showerCooldown), 10));
            //Meteor Amount Text
            elements.Add(cui.UpdateText("MeteorEventUI_meteorAmount", BetterColors.LightGrayTransparent73, configValue.purchaseConfig.showerMeteorAmount.ToString(), 10));
            bool hasRequiredItem;
            if (configValue.purchaseConfig.requiredShortname == "")
                hasRequiredItem = HaveCurrency(player, configValue.purchaseConfig.economyPlugin, configValue.purchaseConfig.economyCurrency) >= configValue.purchaseConfig.showerItemAmount;
            else
			{
				int itemAmount = 0;

				// Collect items from all inventory containers (main, belt, and wear)
				List<Item> playerItems = new List<Item>();
				playerItems.AddRange(player.inventory.containerMain.itemList);
				playerItems.AddRange(player.inventory.containerBelt.itemList);
				playerItems.AddRange(player.inventory.containerWear.itemList);

				// Loop through all items
				foreach (var item in playerItems)
				{
					if (item.info.shortname == configValue.purchaseConfig.requiredShortname && item.skin == configValue.purchaseConfig.requiredSkin)
					{
						itemAmount += item.amount;
					}
				}

				// Check if player has the required amount of the item
				hasRequiredItem = itemAmount >= configValue.purchaseConfig.showerItemAmount;
			}

            string textColor = hasRequiredItem ? BetterColors.LightGrayTransparent73 : BetterColors.RedText;
            //Requirement Text
            elements.Add(cui.UpdateText("MeteorEventUI_requirementsText", textColor, Lang($"ItemRequirement_{currEvent}", player.UserIDString, configValue.purchaseConfig.showerItemAmount), 12, align: TextAnchor.MiddleLeft));
            if (permission.UserHasPermission(player.UserIDString, "meteorevent.admin"))
            {
                //Admin Spawn Input
                elements.Add(cui.UpdateProtectedInputField("MeteorEventUI_adminSetInput", BetterColors.RedText, "0", 15, 3, false, align: TextAnchor.MiddleCenter, font: CUI.Handler.FontTypes.RobotoCondensedBold, command: $"UI_MeteorEvent forceStart {currEvent}", lineType: UnityEngine.UI.InputField.LineType.SingleLine));
            }
            bool onCooldown = cooldowns.ContainsKey(player.userID) && cooldowns[player.userID].ContainsKey(currEvent);
            string bottomText;
            if (!hasRequiredItem)
                bottomText = Lang("NoRequiredItems", player.UserIDString);
            else if (onCooldown)
                bottomText = Lang("EventOnCooldown", player.UserIDString, FormatTime(cooldowns[player.userID][currEvent]));
            else
                bottomText = Lang("CanStartEventInfo", player.UserIDString);
            bool locked = !hasRequiredItem || onCooldown;
            string buttonColor = locked ? BetterColors.RedBackgroundTransparent : BetterColors.GreenBackgroundTransparent;
            string buttonTextColor = locked ? BetterColors.RedText : BetterColors.GreenText;
            string buttonTextText = locked ? Lang("EventLocked", player.UserIDString) : Lang("RunEvent", player.UserIDString);
            string command = locked ? "" : $"UI_MeteorEvent start {currEvent}";
            //Info Text
            elements.Add(cui.UpdateText("MeteorEventUI_bottomInfoText", BetterColors.LightGrayTransparent73, bottomText, 7, align: TextAnchor.MiddleLeft, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            //Run Button
            elements.Add(cui.UpdateProtectedButton("MeteorEventUI_runButton", buttonColor, BetterColors.Transparent, "", 15, command: command));
            elements.Add(cui.UpdateText("MeteorEventUI_runButtonText", buttonTextColor, buttonTextText, 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            elements.Send(player);
        }

        private void LoadMessages()
        {
            Dictionary<string, string> langMessages = new Dictionary<string, string>()
            {
                ["CommandHelp"] = "\nAdmin command usage:\n<color=#5c81ed>/{0} direct <profile> <userNameOrId> <amount></color> - Spawns direct meteor on player.\n<color=#5c81ed>/{0} run <profile> <amount></color> - Runs meteor event.",
                ["NoProfileFound"] = "Profile of name '<color=#5c81ed>{0}</color>' not found!",
                ["NoPlayerFound"] = "Player with name or ID '<color=#5c81ed>{0}</color>' not found!",
                ["CallingDirectOnPlayer"] = "Calling <color=#5c81ed>{2}</color> direct meteors of profile <color=#5c81ed>{0}</color> on player <color=#5c81ed>{1}</color>.",
                ["ToolInBlacklist"] = "You can't mine this meteor with this tool!",
                ["ToolNotInWhitelist"] = "You can't mine this meteor with this tool!",
                ["MeteorEventTitle"] = "METEOR EVENTS",
                ["SelectEventTitle"] = "SELECT EVENT TYPE",
                ["Requirements"] = "REQUIREMENTS",
                ["AdminInputTitle"] = "ADMIN METEOR AMOUNT START",
                ["RunEvent"] = "RUN",
                ["EventLocked"] = "LOCKED",
                ["NoRequiredItems"] = "You don't have all required items in your inventory to start this type of event!",
                ["EventOnCooldown"] = "You've purchased this event lately and currently this event is on cooldown, come back in {0}.",
                ["CanStartEventInfo"] = "You met all conditions to start this type of event. Click RUN button to start an event!",
                ["NoPermission"] = "You don't have permission to run this command!",
                ["SuccessfullyPurchasedEvent"] = "You've successfully purchased an Meteor Event!",
            };
            foreach (var prefab in config.meteorPrefabs)
                if (prefab.Value.bigMeteors.bigMeteorChance > 0 || prefab.Value.customGather.customGatherTools.Count > 0)
                    langMessages.TryAdd($"WrongToolToGather_{prefab.Key}", "You cannot gather anything from this big meteor with an use of regular tools. Use explosives!");
            foreach (var profile in config.profiles)
            {
                langMessages.TryAdd($"MeteorShowerIncoming_{profile.Key}", "There is an incoming meteor shower that contains at least <color=#5c81ed>{0}</color> meteors!");
                langMessages.TryAdd($"EventName_{profile.Key}", profile.Key.ToUpper());
                langMessages.TryAdd($"EventDescription_{profile.Key}", "Prepare to witness the night sky come alive with an awe-inspiring celestial spectacle as the Radiant Meteor Shower graces the heavens. This annual meteor shower promises a dazzling display of shooting stars that will leave stargazers of all ages in sheer wonder. This description can be edited in /lang/en/MeteorEvent.json!");
                langMessages.TryAdd($"ItemRequirement_{profile.Key}", "x{0} of Something (check lang file)");
            }
            lang.RegisterMessages(langMessages, this);
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private static PluginConfig config = new PluginConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(config);
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(config = new PluginConfig()
            {
                commands = new List<string>()
                {
                    "ms",
                    "meteor"
                },
                meteorPrefabs = new Dictionary<string, MeteorPrefabConfig>()
                {
                    { "stone_ore", new MeteorPrefabConfig() {
                        meteorPrefab = "assets/bundled/prefabs/autospawn/resource/ores/stone-ore.prefab",
                        toolBlacklist = new HashSet<string>()
                        {
                            "rock",
                            "stone.pickaxe"
                        },
                        additionalOutput = new List<OutputConfig>()
                        {
                            new OutputConfig()
                            {
                                shortname = "stones",
                                amount = 50,
                                requiredAmount = 20,
                                chance = 35
                            },
                            new OutputConfig()
                            {
                                shortname = "metal.refined",
                                amount = 2,
                                requiredAmount = 20,
                                chance = 2
                            }
                        },
                        bigMeteors = new BigMeteorConfig()
                        {
                            bigMeteorChance = 5f,
                            output = new List<OutputConfigBig>()
                            {
                                new OutputConfigBig()
                                {
                                    amount = 1500,
                                    shortname = "stones"
                                }
                            },
                            explosiveConfig = new Dictionary<string, ExplosiveConfig>()
                            {
                                { "explosive.timed.deployed", new ExplosiveConfig() {
                                    damageDealt = 50,
                                    resourceMultiplier = 1
                                } },
                                { "rocket_basic", new ExplosiveConfig() {
                                    damageDealt = 35,
                                    resourceMultiplier = 0.7f
                                } },
                                { "ammo.rifle.explosive", new ExplosiveConfig() {
                                    damageDealt = 5,
                                    resourceMultiplier = 0.1f
                                } },
                            },
                            crateChance = 5,
                            crates = new Dictionary<string, int>()
                            {
                                { "assets/bundled/prefabs/radtown/crate_normal_2.prefab", 5 },
                                { "assets/bundled/prefabs/radtown/crate_normal.prefab", 3 },
                                { "assets/prefabs/misc/supply drop/supply_drop.prefab", 1 }
                            }
                        },
                        markerConfig = new MarkerConfig()
                        {
                            markerType = "Normal",
                            markerColor1 = "#E01300",
                            markerColor2 = "#7D0B00",
                            markerAlpha = 0.75f,
                            markerRadius = 0.15f
                        }
                    }},
                    { "metal_ore", new MeteorPrefabConfig() {
                        meteorPrefab = "assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab",
                        toolBlacklist = new HashSet<string>()
                        {
                            "rock",
                            "stone.pickaxe"
                        },
                        additionalOutput = new List<OutputConfig>()
                        {
                            new OutputConfig()
                            {
                                shortname = "metal.ore",
                                amount = 35,
                                requiredAmount = 20,
                                chance = 35
                            },
                            new OutputConfig()
                            {
                                shortname = "metal.refined",
                                amount = 2,
                                requiredAmount = 20,
                                chance = 2
                            }
                        },
                        bigMeteors = new BigMeteorConfig()
                        {
                            bigMeteorChance = 5f,
                            output = new List<OutputConfigBig>()
                            {
                                new OutputConfigBig()
                                {
                                    amount = 900,
                                    shortname = "metal.ore"
                                },
                                new OutputConfigBig()
                                {
                                    amount = 20,
                                    shortname = "hq.metal.ore",
                                    lastHitOnly = true
                                }
                            },
                            explosiveConfig = new Dictionary<string, ExplosiveConfig>()
                            {
                                { "explosive.timed.deployed", new ExplosiveConfig() {
                                    damageDealt = 50,
                                    resourceMultiplier = 1
                                } },
                                { "rocket_basic", new ExplosiveConfig() {
                                    damageDealt = 35,
                                    resourceMultiplier = 0.7f
                                } },
                                { "ammo.rifle.explosive", new ExplosiveConfig() {
                                    damageDealt = 5,
                                    resourceMultiplier = 0.1f
                                } },
                            },
                            crateChance = 5,
                            crates = new Dictionary<string, int>()
                            {
                                { "assets/bundled/prefabs/radtown/crate_normal_2.prefab", 5 },
                                { "assets/bundled/prefabs/radtown/crate_normal.prefab", 3 },
                                { "assets/prefabs/misc/supply drop/supply_drop.prefab", 1 }
                            }
                        },
                        markerConfig = new MarkerConfig()
                        {
                            markerType = "Normal",
                            markerColor1 = "#E01300",
                            markerColor2 = "#7D0B00",
                            markerAlpha = 0.75f,
                            markerRadius = 0.15f
                        }
                    }},
                    { "sulfur_ore", new MeteorPrefabConfig() {
                        meteorPrefab = "assets/bundled/prefabs/autospawn/resource/ores/sulfur-ore.prefab",
                        toolBlacklist = new HashSet<string>()
                        {
                            "rock",
                            "stone.pickaxe"
                        },
                        additionalOutput = new List<OutputConfig>()
                        {
                            new OutputConfig()
                            {
                                shortname = "sulfur.ore",
                                amount = 20,
                                requiredAmount = 20,
                                chance = 35
                            },
                            new OutputConfig()
                            {
                                shortname = "metal.refined",
                                amount = 2,
                                requiredAmount = 20,
                                chance = 2
                            }
                        },
                        bigMeteors = new BigMeteorConfig()
                        {
                            bigMeteorChance = 5f,
                            output = new List<OutputConfigBig>()
                            {
                                new OutputConfigBig()
                                {
                                    amount = 450,
                                    shortname = "sulfur.ore"
                                }
                            },
                            explosiveConfig = new Dictionary<string, ExplosiveConfig>()
                            {
                                { "explosive.timed.deployed", new ExplosiveConfig() {
                                    damageDealt = 50,
                                    resourceMultiplier = 1
                                } },
                                { "rocket_basic", new ExplosiveConfig() {
                                    damageDealt = 35,
                                    resourceMultiplier = 0.7f
                                } },
                                { "ammo.rifle.explosive", new ExplosiveConfig() {
                                    damageDealt = 5,
                                    resourceMultiplier = 0.1f
                                } },
                            },
                            crateChance = 5,
                            crates = new Dictionary<string, int>()
                            {
                                { "assets/bundled/prefabs/radtown/crate_normal_2.prefab", 5 },
                                { "assets/bundled/prefabs/radtown/crate_normal.prefab", 3 },
                                { "assets/prefabs/misc/supply drop/supply_drop.prefab", 1 }
                            }
                        },
                        markerConfig = new MarkerConfig()
                        {
                            markerType = "Normal",
                            markerColor1 = "#E01300",
                            markerColor2 = "#7D0B00",
                            markerAlpha = 0.75f,
                            markerRadius = 0.15f
                        }
                    }},
                    { "falling_star", new MeteorPrefabConfig() {
                        meteorPrefab = "assets/bundled/prefabs/modding/cinematic/cinelights/cinelight_point_cool.prefab",
                        maxAirTime = 1.5f,
                        meteorLifetime = 30,
                        meteorScaleMin = 2,
                        meteorScaleMax = 5,
                        atmosphereEnterSound = false,
                        meteorFlame = false,
                        meteorSmoke = false,
                        bigMeteors = new BigMeteorConfig()
                        {
                            bigMeteorChance = 0
                        }
                    }},
                    { "toilet", new MeteorPrefabConfig() {
                        meteorPrefab = "assets/bundled/prefabs/static/toilet_b.static.prefab",
                        meteorScaleMin = 1.2f,
                        meteorScaleMax = 3,
                        customGather = new CustomGatherConfig()
                        {
                            meteorItems = new List<MeteorItemsConfig>()
                            {
                                new MeteorItemsConfig()
                                {
                                    shortname = "metal.refined",
                                    amount = 300
                                },
                                new MeteorItemsConfig()
                                {
                                    shortname = "metal.fragments",
                                    amount = 5000
                                }
                            },
                            customGatherTools = new Dictionary<string, CustomToolConfig>()
                            {
                                { "hammer.salvaged", new CustomToolConfig() {
                                    damageDealtPercentage = 3
                                }},
                                { "paddle", new CustomToolConfig() {
                                    damageDealtPercentage = 2.5f
                                }}
                            }
                        },
                        radiationChance = 100,
                        radiationAmount = 12.5f,
                        scaleRadiation = true,
                        bigMeteors = new BigMeteorConfig()
                        {
                            bigMeteorChance = 5f,
                            output = new List<OutputConfigBig>()
                            {
                                new OutputConfigBig()
                                {
                                    amount = 50,
                                    shortname = "metal.refined"
                                }
                            },
                            explosiveConfig = new Dictionary<string, ExplosiveConfig>()
                            {
                                { "explosive.timed.deployed", new ExplosiveConfig() {
                                    damageDealt = 50,
                                    resourceMultiplier = 1
                                } },
                                { "rocket_basic", new ExplosiveConfig() {
                                    damageDealt = 35,
                                    resourceMultiplier = 0.7f
                                } },
                                { "ammo.rifle.explosive", new ExplosiveConfig() {
                                    damageDealt = 5,
                                    resourceMultiplier = 0.1f
                                } },
                            }
                        },
                        markerConfig = new MarkerConfig()
                        {
                            markerType = "Normal",
                            markerColor1 = "#E0D5C30",
                            markerColor2 = "#6E665A",
                            markerAlpha = 0.75f,
                            markerRadius = 0.15f,
                            markerText = "Space Toilet"
                        }
                    }},
                    { "barrel", new MeteorPrefabConfig() {
                        meteorPrefab = "assets/bundled/prefabs/radtown/loot_barrel_1.prefab",
                        meteorScaleMin = 12f,
                        meteorScaleMax = 12f,
                        radiationChance = 100,
                        radiationAmount = 12.5f,
                        randomizeXrotation = false,
                        randomizeZrotation = false,
                        customGather = new CustomGatherConfig()
                        {
                            meteorItems = new List<MeteorItemsConfig>()
                            {
                                new MeteorItemsConfig()
                                {
                                    shortname = "scrap",
                                    amount = 2000
                                },
                                new MeteorItemsConfig()
                                {
                                    shortname = "metal.fragments",
                                    amount = 10000
                                }
                            },
                            customGatherTools = new Dictionary<string, CustomToolConfig>()
                            {
                                { "hammer.salvaged", new CustomToolConfig() {
                                    damageDealtPercentage = 3
                                }}
                            }
                        },
                        bigMeteors = new BigMeteorConfig()
                        {
                            bigMeteorChance = 0f
                        },
                        markerConfig = new MarkerConfig()
                        {
                            markerType = "Normal",
                            markerColor1 = "#E0D5C30",
                            markerColor2 = "#6E665A",
                            markerAlpha = 0.75f,
                            markerRadius = 0.15f,
                            markerText = "Space Barrel"
                        }
                    }}
                },
                profiles = new Dictionary<string, MeteorConfig>()
                {
                    { "default", new MeteorConfig() {
                        directFalling = new Vector3(0, -9, 0),
                        maxMeteorShowerLength = 30,
                        angleConfig = new HashSet<AngleConfig>()
                        {
                            new AngleConfig()
                            {
                                angle = new Vector3(3.5f, -4.6f, 0f),
                                weight = 1,
                                offset = new Vector3(-300f, 0f, 0f)
                            },
                            new AngleConfig()
                            {
                                angle = new Vector3(-3.5f, -4.6f, 1.2f),
                                weight = 1,
                                offset = new Vector3(300f, 0f, -100f)
                            }
                        },
                        meteorProfiles = new Dictionary<string, int>()
                        {
                            { "stone_ore", 1 },
                            { "metal_ore", 1 },
                            { "sulfur_ore", 1 }
                        },
                        purchaseConfig = new PurchaseConfig()
                        {
                            showerAllowPurchasing = true,
                            isDirect = true,
                            showerMeteorAmount = 3
                        }
                    }},
                    { "falling_stars", new MeteorConfig() {
                        directFalling = new Vector3(0, -9, 0),
                        maxMeteorShowerLength = 60,
                        soundPrefab = "",
                        message = false,
                        angleConfig = new HashSet<AngleConfig>()
                        {
                            new AngleConfig()
                            {
                                angle = new Vector3(45.5f, -4.2f, 12.7f),
                                weight = 1
                            },
                            new AngleConfig()
                            {
                                angle = new Vector3(-56.5f, -3.2f, -14.2f),
                                weight = 1
                            }
                        },
                        meteorProfiles = new Dictionary<string, int>()
                        {
                            { "falling_star", 1 }
                        }
                    }},
                    { "toilets", new MeteorConfig() {
                        directFalling = new Vector3(0, -9, 0),
                        maxMeteorShowerLength = 60,
                        angleConfig = new HashSet<AngleConfig>()
                        {
                            new AngleConfig()
                            {
                                angle = new Vector3(3.5f, -4.6f, 0f),
                                weight = 1,
                                offset = new Vector3(-300f, 0f, 0f)
                            },
                            new AngleConfig()
                            {
                                angle = new Vector3(-3.5f, -4.6f, 1.2f),
                                weight = 1,
                                offset = new Vector3(300f, 0f, -100f)
                            }
                        },
                        meteorProfiles = new Dictionary<string, int>()
                        {
                            { "toilet", 1 }
                        }
                    }},
                    { "barrel", new MeteorConfig() {
                        directFalling = new Vector3(0, -2, 0),
                        maxMeteorShowerLength = 10,
                        angleConfig = new HashSet<AngleConfig>()
                        {
                            new AngleConfig()
                            {
                                angle = new Vector3(0, -0.6f, 0f),
                                weight = 1,
                                offset = new Vector3(0f, 0f, 0f)
                            }
                        },
                        meteorProfiles = new Dictionary<string, int>()
                        {
                            { "barrel", 1 }
                        }
                    }}

                },
                timedEvents = new Dictionary<string, TimedEventConfig>()
                {
                    { "default", new TimedEventConfig() {
                        spawnProfiles = new Dictionary<string, int>()
                        {
                            { "default", 10 },
                            { "falling_stars", 1 }
                        }
                    } },
                    { "toilets", new TimedEventConfig() {
                        timerMin = 28800,
                        timerMax = 50400,
                        spawnProfiles = new Dictionary<string, int>()
                        {
                            { "toilets", 1 }
                        },
                        basedOnOnline = false,
                        directPlayer = true,
                        commandsToRun = new List<string>()
                        {
                            "say There is {meteorCount} falling toilets in the sky!"
                        },
                        eventChance = 5,
                        maxMeteorCount = 10,
                        meteorCount = 10
                    } }
                },
                randomDirectEvents = new List<RandomDirectEventConfig>()
                {
                    new RandomDirectEventConfig()
                    {
                       profileName = "default"
                    },
                    new RandomDirectEventConfig()
                    {
                       profileName = "toilets",
                       chance = 0
                    }
                }

            }, true);
        }

        private class PluginConfig
        {
            [JsonProperty("Commands")]
            public List<string> commands = new List<string>();

            [JsonProperty("Command Require Permission")]
            public bool commandPermission = false;

            [JsonProperty("Enable Logs")]
            public bool enableLogs = true;

            [JsonProperty("Enable Plugin Hooks")]
            public bool enableHooks = false;

            [JsonProperty("Global Meteor Config")]
            public GlobalMeteorConfig allMeteors = new GlobalMeteorConfig();

            [JsonProperty("Meteor Prefabs")]
            public Dictionary<string, MeteorPrefabConfig> meteorPrefabs = new Dictionary<string, MeteorPrefabConfig>();

            [JsonProperty("Meteor Profiles")]
            public Dictionary<string, MeteorConfig> profiles = new Dictionary<string, MeteorConfig>();

            [JsonProperty("Timed Events")]
            public Dictionary<string, TimedEventConfig> timedEvents = new Dictionary<string, TimedEventConfig>();

            [JsonProperty("Enable Random Direct Falling Meteors On Mining Regular Ones")]
            public bool randomDirectEventsEnabled = true;

            [JsonProperty("Random Direct Falling Meteor Options")]
            public List<RandomDirectEventConfig> randomDirectEvents = new List<RandomDirectEventConfig>();
        }

        private class GlobalMeteorConfig
        {
            [JsonProperty("Destroy Meteors Underwater")]
            public bool destroyUnderwater = false;

            [JsonProperty("Destroy Trees On Impact Radius (0 to disable)")]
            public float destroyTrees = 25f;

            [JsonProperty("Max Meteors On Map At One Time")]
            public int maxMeteors = 80;

            [JsonProperty("Falling Meteor Ground Check Radius (increase might be required if meteors are falling slowly)")]
            public float groundHeightCheck = 25f;

            /*[JsonProperty("Damage Entities")]
            public bool damageEntities = true;

            [JsonProperty("Damage Only Unowned Entities")]
            public bool damageUnownedEntities = false;

            [JsonProperty("Damage Players")]
            public bool damagePlayers = true;

            [JsonProperty("Damage Bots")]
            public bool damageBots = true;*/

            [JsonProperty("Kill Meteors On Roads")]
            public bool roadKill = true;

            [JsonProperty("Kill Meteors On Rail Tracks")]
            public bool railKill = true;

            [JsonProperty("Kill Meteors On Monuments")]
            public bool monumentKill = false;

            [JsonProperty("Fix For Meteor MeshCollider Message (console spam fix only, might have performance impact if enabled)")]
            public bool checkForMeshCollider = false;

            //[JsonProperty("Check For ZoneManager For Explosives")]
            //public bool useZoneManager = false;

            [JsonProperty("Meteor Spread On Map (higher = smaller impact radius)")]
            public float meteorSpread = 2.5f;

            [JsonProperty("Explosive Ammo Max Shoot Distance")]
            public float maxDistanceAmmo = 50f;
        }

        private class MeteorConfig
        {
            [JsonProperty("Randomized Impact Radius (if direct meteor)")]
            public float impactRadius = 10f;

            [JsonProperty("Direct Meteor Falling Angle")]
            public Vector3 directFalling = Vector3.zero;

            [JsonProperty("Meteor Event Length (in seconds)")]
            public float maxMeteorShowerLength = 5;

            [JsonProperty("Incoming Shower Sound Effect Prefab (disabled if empty)")]
            public string soundPrefab = "assets/prefabs/tools/pager/effects/beep.prefab";

            [JsonProperty("Send Message About Meteor Event On Chat")]
            public bool message = true;

            [JsonProperty("Spawn Height")]
            public float spawnHeight = 500f;

            [JsonProperty("Falling Angle Config")]
            public HashSet<AngleConfig> angleConfig = new HashSet<AngleConfig>();

            [JsonProperty("Meteor Profiles And Spawn Weights (profile: spawn weight)")]
            public Dictionary<string, int> meteorProfiles = new Dictionary<string, int>();

            [JsonProperty("Purchase Config")]
            public PurchaseConfig purchaseConfig = new PurchaseConfig();
        }

        private class AngleConfig
        {
            [JsonProperty("Appear Weight")]
            public int weight = 1;

            [JsonProperty("Angle")]
            public Vector3 angle = Vector3.zero;

            [JsonProperty("Falling Radius Offset")]
            public Vector3 offset = Vector3.zero;
        }

        private class MeteorPrefabConfig
        {
            [JsonProperty("Meteor Prefab Path")]
            public string meteorPrefab = "";

            [JsonProperty("Max Meteor Flight Time (0, to not destroy)")]
            public float maxAirTime = 0;

            [JsonProperty("Meteor Lifetime (0, to not destroy)")]
            public float meteorLifetime = 1800;

            [JsonProperty("Minimum Scale")]
            public float meteorScaleMin = 0.5f;

            [JsonProperty("Maximum Scale")]
            public float meteorScaleMax = 2;

            [JsonProperty("Yield Multiplier")]
            public float meteorYieldMultiplier = 2;

            [JsonProperty("Scale Yield By Size")]
            public bool scaleMeteorYield = true;

            [JsonProperty("Meteor Radiation Chance (0-100)")]
            public float radiationChance = 0;

            [JsonProperty("Meteor Radiation Amount")]
            public float radiationAmount = 0;

            [JsonProperty("Scale Radiation With Size")]
            public bool scaleRadiation = true;

            [JsonProperty("Run Atmosphere Enter Sound")]
            public bool atmosphereEnterSound = true;

            [JsonProperty("Enable Meteor Flame")]
            public bool meteorFlame = true;

            [JsonProperty("Enable Meteor Smoke")]
            public bool meteorSmoke = true;

            [JsonProperty("Falling - Randomize Meteor X Rotation")]
            public bool randomizeXrotation = true;

            [JsonProperty("Falling - Randomize Meteor Y Rotation")]
            public bool randomizeYrotation = true;

            [JsonProperty("Falling - Randomize Meteor Z Rotation")]
            public bool randomizeZrotation = true;

            [JsonProperty("Landed - Randomize Meteor X Rotation")]
            public bool randomizeXrotationLanded = false;

            [JsonProperty("Landed - Randomize Meteor Y Rotation")]
            public bool randomizeYrotationLanded = true;

            [JsonProperty("Landed - Randomize Meteor Z Rotation")]
            public bool randomizeZrotationLanded = false;

            [JsonProperty("Landed Entity Offset")]
            public Vector3 landedOffset = Vector3.zero;

            [JsonProperty("Required Tool To Destroy - Whitelist (shortnames)")]
            public HashSet<string> toolWhitelist = new HashSet<string>();

            [JsonProperty("Required Tool To Destroy - Blacklist (shortnames)")]
            public HashSet<string> toolBlacklist = new HashSet<string>();

            [JsonProperty("Fully Custom Gather System Tools")]
            public CustomGatherConfig customGather = new CustomGatherConfig();

            [JsonProperty("Additional Output Per Hit")]
            public List<OutputConfig> additionalOutput = new List<OutputConfig>();

            [JsonProperty("Big Meteor Config")]
            public BigMeteorConfig bigMeteors = new BigMeteorConfig();

            [JsonProperty("Marker Config")]
            public MarkerConfig markerConfig = new MarkerConfig();
        }

        private class CustomGatherConfig
        {
            [JsonProperty("Items Per Meteor")]
            public List<MeteorItemsConfig> meteorItems = new List<MeteorItemsConfig>();

            [JsonProperty("Fully Custom Gather System Tools (key value is tool shortname)")]
            public Dictionary<string, CustomToolConfig> customGatherTools = new Dictionary<string, CustomToolConfig>();
        }

        private class MeteorItemsConfig
        {
            [JsonProperty("Shortname")]
            public string shortname = "";

            [JsonProperty("Skin ID")]
            public ulong skinId = 0;

            [JsonProperty("Amount")]
            public int amount = 1;

            [JsonProperty("Display Name")]
            public string name = "";

            [JsonProperty("Only On Last Hit")]
            public bool lastHitOnly = false;
        }

        private class CustomToolConfig
        {
            [JsonProperty("Require Exact Skin ID")]
            public bool requireSkin = false;

            [JsonProperty("Tool Skin ID")]
            public ulong skin = 0;

            [JsonProperty("Tool Damage Dealt Percentage")]
            public float damageDealtPercentage = 5;

        }

        private class BigMeteorConfig
        {
            [JsonProperty("Chance (0-100)")]
            public float bigMeteorChance = 10f;

            [JsonProperty("Minimum Scale")]
            public float meteorScaleMin = 5f;

            [JsonProperty("Maximum Scale")]
            public float meteorScaleMax = 7f;

            [JsonProperty("Output Per Hit")]
            public List<OutputConfigBig> output = new List<OutputConfigBig>();

            [JsonProperty("Call OnDispenserGather hook")]
            public bool callHook = true;

            [JsonProperty("Explosive Config (key value is explosive prefab shortname, see website for more info)")]
            public Dictionary<string, ExplosiveConfig> explosiveConfig = new Dictionary<string, ExplosiveConfig>();

            [JsonProperty("Crate Inside Chance (0-100)")]
            public float crateChance = 0;

            [JsonProperty("Crates Spawned Inside (prefab names: spawn weight)")]
            public Dictionary<string, int> crates = new Dictionary<string, int>();
        }

        private class OutputConfig
        {
            [JsonProperty("Required Gather Per Hit (0 to disable)")]
            public int requiredAmount = 0;

            [JsonProperty("Shortname")]
            public string shortname = "";

            [JsonProperty("Skin ID")]
            public ulong skinId = 0;

            [JsonProperty("Amount")]
            public int amount = 1;

            [JsonProperty("Display Name")]
            public string name = "";

            [JsonProperty("Chance (0-100)")]
            public float chance = 100;

            [JsonProperty("Only On Last Hit")]
            public bool lastHitOnly = false;
        }

        private class OutputConfigBig
        {
            [JsonProperty("Shortname")]
            public string shortname = "";

            [JsonProperty("Skin ID")]
            public ulong skinId = 0;

            [JsonProperty("Amount")]
            public int amount = 1;

            [JsonProperty("Display Name")]
            public string name = "";

            [JsonProperty("Chance (0-100)")]
            public float chance = 100;

            [JsonProperty("Only On Last Hit")]
            public bool lastHitOnly = false;
        }

        private class ExplosiveConfig
        {
            [JsonProperty("Damage Dealt")]
            public float damageDealt = 50;

            [JsonProperty("Resource Multiplier")]
            public float resourceMultiplier = 1;
        }

        private class PurchaseConfig
        {
            [JsonProperty("Allow Purchasing")]
            public bool showerAllowPurchasing = false;

            [JsonProperty("Used Economy Plugin (see website for reference)")]
            public int economyPlugin = 0;

            [JsonProperty("Used Economy Currency (if ShoppyStock used)")]
            public string economyCurrency = "rp";

            [JsonProperty("Required Permission")]
            public bool requirePerm = true;

            [JsonProperty("Direct Meteor Shower (on player)")]
            public bool isDirect = false;

            [JsonProperty("Allow To Spawn Large Meteors")]
            public bool allowLarge = false;

            [JsonProperty("Meteors Amount")]
            public int showerMeteorAmount = 10;

            [JsonProperty("Item Shortname Required To Purchase Meteor Shower (empty if currency)")]
            public string requiredShortname = "scrap";

            [JsonProperty("Item Skin ID Required To Purchase Meteor Shower (empty if currency)")]
            public ulong requiredSkin = 0;

            [JsonProperty("Item/Currency Amount Required To Purchase Meteor Shower")]
            public int showerItemAmount = 1000;

            [JsonProperty("Cooldown After Purchasing Meteor Shower (in seconds)")]
            public int showerCooldown = 3600;
        }

        private class MarkerConfig
        {
            [JsonProperty("Marker Type (None/Normal/Explosion)")]
            public string markerType = "Explosion";

            [JsonProperty("Marker Alpha (normal only)")]
            public float markerAlpha = 0.75f;

            [JsonProperty("Marker Color #1 (normal only)")]
            public string markerColor1 = "#E01300";

            [JsonProperty("Marker Color #2 (normal only)")]
            public string markerColor2 = "#7D0B00";

            [JsonProperty("Marker Radius (normal only)")]
            public float markerRadius = 0.4f;

            [JsonProperty("Map Marker Text (normal only, disabled if empty)")]
            public string markerText = "";
        }

        private class TimedEventConfig
        {
            [JsonProperty("Event Every X Seconds (minimal time, in seconds)")]
            public int timerMin = 1200;

            [JsonProperty("Event Every X Seconds (maximal time, in seconds)")]
            public int timerMax = 2400;

            [JsonProperty("Used Meteor Config Keys (profile: spawn weight)")]
            public Dictionary<string, int> spawnProfiles = new Dictionary<string, int>();

            [JsonProperty("Direct Meteors To Players")]
            public bool directPlayer = false;

            [JsonProperty("Meteor Amount Based On Player Count")]
            public bool basedOnOnline = true;

            [JsonProperty("Meteor Count (if based on player count - per player)")]
            public float meteorCount = 4;

            [JsonProperty("Max Meteor Count")]
            public int maxMeteorCount = 40;

            [JsonProperty("Chance To Start Event (0-100)")]
            public float eventChance = 100;

            [JsonProperty("Commands Ran On Start")]
            public List<string> commandsToRun = new List<string>();

            [JsonProperty("Webhook Message")]
            public MeteorDiscordWebhook webhook = new MeteorDiscordWebhook();
        }

        private class MeteorDiscordWebhook
        {
            [JsonProperty("Enabled")]
            public bool enabled = false;

            [JsonProperty("Embed")]
            public bool embed = true;

            [JsonProperty("Webhook")]
            public string webhook = "";

            [JsonProperty("Inline")]
            public bool inline = true;

            [JsonProperty("Color")]
            public int color = 0;

            [JsonProperty("Title")]
            public string title = "Meteor Shower";

            [JsonProperty("Message")]
            public string message = "Meteor Shower with {0} meteors is incoming!";
        }

        private class RandomDirectEventConfig
        {
            [JsonProperty("Spawn Chance (0-100)")]
            public float chance = 1;

            [JsonProperty("Meteor Profile Name (not prefab key)")]
            public string profileName = "";

            [JsonProperty("Meteor Min Amount")]
            public int minMeteors = 1;

            [JsonProperty("Meteor Max Amount")]
            public int maxMeteors = 3;

            [JsonProperty("Allow To Spawn Large Meteors")]
            public bool allowLarge = false;
        }

        private class BetterColors
        {
            public static readonly string GreenBackgroundTransparent = "0.4509804 0.5529412 0.2705882 0.5450981";
            public static readonly string GreenText = "0.6078432 0.7058824 0.4313726 1";
            public static readonly string RedBackgroundTransparent = "0.6980392 0.2039216 0.003921569 0.5450981";
            public static readonly string RedBackground = "0.6980392 0.2039216 0.003921569 1";
            public static readonly string RedText = "0.9411765 0.4862745 0.3058824 1";
            public static readonly string FishingRed = "0.6666667 0.2784314 0.2039216 1";
            public static readonly string Transparent = "1 1 1 0";
            public static readonly string BlackTransparent10 = "0 0 0 0.1";
            public static readonly string BlackTransparent20 = "0 0 0 0.2";
            public static readonly string LightGray = "0.9686275 0.9215686 0.8823529 1";
            public static readonly string LightGrayTransparent3_9 = "0.9686275 0.9215686 0.8823529 0.0397451";
            public static readonly string LightGrayTransparent8 = "0.9686275 0.9215686 0.8823529 0.02843138";
            public static readonly string LightGrayTransparent73 = "0.9686275 0.9215686 0.8823529 0.7294118";
            public static readonly string RadioUiBackground = "0.1529412 0.1411765 0.1137255 1";
            public static readonly string CraftingRadialBackground = "0.1686275 0.1607843 0.1411765 0.7529412";
        }
    }
}