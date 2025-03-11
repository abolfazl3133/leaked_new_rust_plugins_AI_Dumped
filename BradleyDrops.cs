using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Time = UnityEngine.Time;
using Random = UnityEngine.Random;

/* Changelog

1.1.16
 - Fixed: For Rust Update
 
1.1.15
 - Fixed: OnEntitySpawned (Object reference not set to an instance of an object) error on bradley cannon fire
 - Fixed: Loot items not stacking correctly with other loot items of the same type
 
1.1.14
 - Fixed: Entity is NULL but is still in saveList - not destroyed properly? error.

1.1.13
 - Fixed: "ArgumentNullException: Value cannot be null. Parameter name: key" error.
 - Added: Added balloon delivery method for now, Chinook delivery will be on the proper full update

1.1.12
 - Fixed: Reward points not being correctly issued since Rust update

1.1.11
 - Fixed: Occasional CanHackCrate error
 - Added: Config option to prevent players calling in monuments

1.1.10
 - Fixed: Hackable crates not giving correct loot in some cases when enabled in the config

1.1.9
 - Fixed: Bradley Signals are now blocked in monuments

1.1.8
 - Fixed: Hook conflict on CanCombineDroppedItem returning false instead of true
 - Fixed: Added missing config option to allow compatibility with Dynamic PVP plugin
 - Updated: Changed some code to be more performant

1.1.7
 - Fixed: OnEntityDestroy NRE when one or more bradley kill team members are offline
 - Fixed: Bug where rewards were not received in cases where team members offline
 - Fixed: Bug where no rewards earned if players take bradley with auto turret
 - Fixed: AlphaLoot not being able to populate hackable crates
 - Added: Custom chat icon config option

1.1.6
 - Fixed: Language defaulting always to English (Fixed this time)

1.1.5
 - Fixed: Player limit not resetting after player using despawn command
 - Fixed: Occasional OnEntityTakeDamage NRE spam in console (hopefully)
 - Added: Config option to destroy/remove entities placed in the landing zone after signal thrown

1.1.4
 - Fixed: Language defaulting always to English, users who use their own lang files should now be able to
 - Fixed: Better compatibility  with NextGenPVE/TruePVE (Any plugins using CanEntityTakeDamage hook)
 - Fixed: Compatibility  with Dynamic PVP now works properly
 - Fixed: BotReSpawn vanilla Bradley profile triggering as well as bot profile set in BradleyDrops config
 - Added: BP chance & max BPs to loot table config
 - Added: Config option to announce to player/team only or global chat
 - Added: Check to allow players with admin perm to override crate and gibs protection

1.1.3
 - Fixed: report command not using custom command if edited in the config
 - Added: Clear cooldown admin command (bdclearcd) requires admin perm
 - Added: Seperate cooldowns for each Bradley tier/profile
 - Added: Config option to enable per tier cooldown or not
 - Added: Player Bradley limit config option

1.1.2
 - Fixed: Bug where players were prevented from looting crates from other Bradley or Heli in certain cases
 
1.1.1
 - Fixed: Bullet accuracy option not working
 - Fixed: NRE when bradley receives damage from fireball
 - Fixed: NRE when players shoot bradley with remote Auto Turrets
 - Added: Config option to allow/disallow damage to bradley by remote Auto Turret
 - Added: Report command is now usable by players for their owned bradleys (admin perm allows all bradleys)
 - Added: Config option to specify custom report command (default = bdreport)
 - Updated: Re-write of some methods for improvements in damage handling and code efficiency
 - Info: Code cleanup

1.0.32
 - Added example items to loot tables
 - 
1.0.31
 - Fixed: Hackable locked crate not despawning at time specified in config
 - Fixed: Occasional NullReferenceException error on CanStackItem
 - Added: Hackable locked crate loot table options

 */
namespace Oxide.Plugins
{
    [Info("Bradley Drops", "ZEODE", "1.1.16")]
    [Description("Call a Bradley APC to your location with custom supply signals.")]
    public class BradleyDrops: RustPlugin
    {
        #region Plugin References
        
        [PluginReference] Plugin Friends, Clans, FancyDrop, RewardPlugin, Vanish, NoEscape, BotReSpawn, DynamicPVP;
        
        #endregion Plugin References

        #region Constants

        private static BradleyDrops Instance;
        private static System.Random random = new System.Random();

        private const string permAdmin = "bradleydrops.admin";
        private const string permBuy = "bradleydrops.buy";
        private const string permBypasscooldown = "bradleydrops.bypasscooldown";

        private const string bradleyPrefab = "assets/prefabs/npc/m2bradley/bradleyapc.prefab";
        private const string balloonPrefab = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab";
        private const string planePrefab = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";
        private const string bradleyExplosion = "assets/prefabs/npc/m2bradley/effects/bradley_explosion.prefab";
        private const string hackableCrate = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";
        private const string shockSound = "assets/prefabs/locks/keypad/effects/lock.code.shock.prefab";
        private const string deniedSound = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";
        private const string balloonExplosion = "assets/content/vehicles/minicopter/effects/mincopter_explosion.prefab";

        private const string defaultWebhookUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

        private const int supplySignalId = 1397052267;
        private const int scrapId = -932201673;
        
        // Default config item skins
        private const ulong easySkinID = 2905355269;
        private const ulong medSkinID = 2905355312;
        private const ulong hardSkinID = 2905355296;
        private const ulong eliteSkinID = 2911864795;

        // Default config item names
        private const string easyDrop = "Bradley Drop (Easy)";
        private const string medDrop = "Bradley Drop (Medium)";
        private const string hardDrop = "Bradley Drop (Hard)";
        private const string eliteDrop = "Bradley Drop (Elite)";
        
        #endregion Constants

        #region Language
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SyntaxPlayer"] = "<color=red>Invalid syntax</color>, use:\n\n<color=green>/bdgive <type> <SteamID/PlayerName> <amount></color>",
                ["SyntaxConsole"] = "Invalid syntax, use: bdgive <type> <SteamID/PlayerName> <amount>",
                ["ClearSyntaxPlayer"] = "<color=red>Invalid syntax</color>, use:\n\n<color=green>/bdclearcd</color> (clears all cooldowns)\n\nOr\n\n<color=green>/bdclearcd <SteamID/PlayerName></color>",
                ["ClearSyntaxConsole"] = "Invalid syntax, use: \"bdclearcd\" (clears all cooldowns) or \"bdclearcd <SteamID/PlayerName>\"",
                ["Receive"] = "You received <color=orange>{0}</color> x <color=orange>{1}</color>!",
                ["PlayerReceive"] = "Player {0} ({1}) received {2} x {3}!",
                ["Permission"] = "You do not have permission to use <color=orange>{0}</color>!",
                ["CooldownTime"] = "Cooldown active! You can call another in {0}!",
                ["TeamCooldownTime"] = "Team cooldown active! You can call another in {0}!",
                ["GlobalLimit"] = "Global limit of {0} active Bradley APCs is reached, please try again later",
                ["PlayerLimit"] = "Player limit of {0} active Bradley APCs is reached, please try again later",
                ["NotAdmin"] = "You do not have permission to use that command!",
                ["PlayerNotFound"] = "Can't find a player with the name or ID: {0}",
                ["InGameOnly"] = "Error: This command is only for use in game!",
                ["PlayerDead"] = "Player with name or ID {0} is dead, try again when they have respawned",
                ["NotOnGround"] = "<color=orange>{0}</color> must be thrown onto the floor, signal refunded, check inventory.",
                ["InMonument"] = "You cannot call <color=orange>{0}</color> in a <color=red>monument</color>, signal refunded, check inventory.",
                ["UnderWater"] = "<color=orange>{0}</color> was thrown too near <color=blue>water</color> and was refunded, check inventory.",
                ["IntoWater"] = "<color=orange>{0}</color> went into deep <color=blue>water</color> and was destroyed.",
                ["InSafeZone"] = "<color=orange>{0}</color> was thrown in a <color=green>Safe Zone</color> and was refunded, check inventory.",
                ["IntoSafeZone"] = "<color=orange>{0}</color> moved into a <color=green>Safe Zone</color> and was destroyed.",
                ["BuildingPriv"] = "<color=orange>{0}</color> was thrown too close to a players base and was refunded, check inventory.",
                ["NearCollider"] = "<color=orange>{0}</color> was thrown too close to a object, please throw in more open ground.",
                ["LZCleared"] = "Entities placed near the landing zone of <color=orange>{0}</color> have been destroyed.",
                ["Inside"] = "<color=orange>{0}</color> was thrown inside and was refunded, check inventory.",
                ["InvalidDrop"] = "Drop type \"{0}\" not recognised, please check and try again!",
                ["CannotLoot"] = "You cannot loot this because it is not yours!",
                ["CannotHack"] = "You cannot hack this because it is not yours!",
                ["CannotMine"] = "You cannot mine this because it is not yours!",
                ["BuyCmdSyntax"] = "Useage:\n\n/{0} {1}",
                ["NoBuy"] = "Buy Command for <color=orange>{0}</color> Is Not Enabled!",
                ["BuyPermission"] = "You do not have permission to buy Bradley Drop \"<color=orange>{0}</color>\".",
                ["PriceList"] = "Bradley Drop Prices:\n\n{0}",
                ["BradleyKilledTime"] = "<color=orange>{0}</color> killed by <color=green>{1}</color> in grid <color=green>{2}</color> (Time Taken: {3})",
                ["BradleyCalled"] = "<color=green>{0}</color> just called in a <color=orange>{1}</color> to their location in grid <color=green>{2}</color>",
                ["RewardGiven"] = "<color=green>{0} {1}</color> points received for destroying <color=orange>{2}</color>!",
                ["ScrapGiven"] = "<color=green>{0}</color> Scrap received for destroying <color=orange>{1}</color>!",
                ["CannotDamage"] = "You <color=red>Cannot</color> damage this <color=orange>{0}</color>!",
                ["NoTurret"] = "You <color=red>Cannot</color> damage this <color=orange>{0}</color> with remote turrets!",
                ["TooFarAway"] = "You are <color=red>Too Far</color> away to engage this <color=orange>{0}</color> ({1} m)",
                ["CantAfford"] = "You <color=red>Cannot</color> afford this! Cost: <color=orange>{0}</color> Required: <color=orange>{1}</color>",
                ["FullInventory"] = "<color=green>{0}</color> <color=red>NOT</color> given! No inventory space!",
                ["NotLanded"] = "You <color=red>Cannot</color> damage <color=orange>{0}</color> until it's landed!",
                ["ApcReportTitle"] = "There are currently <color=orange>{0}</color> active dropped Bradley APCs",
                ["ApcReportItem"] = "<size=9><color=orange>{0}/{1}</color> - <color=green>{2}</color> (Owner: <color=orange>{3}</color> Grid: <color=orange>{4}</color> Health: <color=orange>{5}</color>)\n</size>",
                ["ApcReportTitleCon"] = "There are currently {0} active dropped Bradley APCs",
                ["ApcReportItemCon"] = "{0}/{1} - {2} (Owner: {3} Grid: {4} Health: {5})\n",
                ["ApcReportList"] = "{0}",
                ["DespawnApc"] = "<color=orange>{0}</color> is despawning, you were warned! <color=red>{1}</color>/<color=red>{2}</color>",
                ["DespawnWarn"] = "<color=red>Damage Blocked!</color> You may only attack from a base with TC auth. If you continue, the <color=orange>{0}</color> will despawn. Warning Level: <color=red>{1}</color>/<color=red>{2}</color>",
                ["DamageReport"] = "<color=orange>Damage Report</color>\n\n{0}",
                ["DamageReportIndex"] = "<size=11>{0}. <color=green>{1}</color> -> {2} HP\n</size>",
                ["DiscordCall"] = "**{0}** just called a **{1}** to their location in grid **{2}**",
                ["DiscordKill"] = "**{0}** just destroyed a **{1}** in grid **{2}**",
                ["DiscordDespawn"] = "**{0}** called by **{1}** just despawned at grid **{2}**",
                ["DespawnedBradleys"] = "You have retired ALL your (your teams) called Bradley APCs",
                ["CooldownsCleared"] = "All player cooldowns have been cleared!",
                ["PlayerCooldownCleared"] = "Cooldown cleared for player {0} ({1})",
                ["PlayerNoCooldown"] = "No active cooldown for player {0} ({1})"
            }, this);
        }

        private string Lang(string messageKey, string playerId, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerId), args);
        }

        private void Message(IPlayer player, string messageKey, params object[] args)
        {
            if (player == null || !player.IsConnected)
                return;
            
            var message = Lang(messageKey, player.Id, args);
            if (config.options.usePrefix && config.options.chatPrefix != string.Empty)
            {
                if (player.IsServer)
                {
                    Regex regex = new Regex(@"(\[.*?\])");
                    Match match = regex.Match(config.options.chatPrefix);
                    player.Reply($"{match}: {message}");
                }
                else
                {
                    player.Reply($"{config.options.chatPrefix}: {message}");
                }
            }
            else
            {
                player.Reply(message);
            }
        }

        private void Message(BasePlayer player, string messageKey, params object[] args)
        {
            if (player == null || player is NPCPlayer || !player.IPlayer.IsConnected)
                return;
            
            var message = Lang(messageKey, player.UserIDString, args);
            Player.Message(player, message, config.options.usePrefix ? config.options.chatPrefix : null, config.options.chatIcon);

        }

        #endregion Language

        #region Oxide Hooks

        private void OnServerInitialized()
        {
            Instance = this;
            CheckRewardPlugin();

            if (!config.options.useStacking)
            {
                Unsubscribe(nameof(CanStackItem));
                Unsubscribe(nameof(CanCombineDroppedItem));
            }

            if (config.options.noVanillaApc)
            {
                ConVar.Bradley.enabled = false;
                Puts($"INFO: Vanilla Bradley APC server event at Launch Site is disabled");
            }
        }

        private void Init()
        {
            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permBuy, this);
            permission.RegisterPermission(permBypasscooldown, this);

            config.rewards.RegisterPermissions(permission, this);
            config.bradley.RegisterPermissions(permission, this);

            foreach (var key in config.bradley.apcConfig.Keys)
            {
                var permSuffix = config.bradley.apcConfig[key].GiveItemCommand.ToLower();
                var perm = $"{Name.ToLower()}.{permSuffix}";
                permission.RegisterPermission(perm, this);
                DropTypes.Add(config.bradley.apcConfig[key].GiveItemCommand);
                BradleyProfileCache.Add(config.bradley.apcConfig[key].SignalSkinID, key);
            }

            AddCovalenceCommand(config.options.reportCommand, nameof(CmdReport));
            AddCovalenceCommand(config.purchasing.buyCommand, nameof(CmdBuyDrop));
            AddCovalenceCommand(config.bradley.despawnCommand, nameof(CmdDespawnApc));
            AddCovalenceCommand("bdgive", nameof(CmdGiveDrop));
            AddCovalenceCommand("bdclearcd", nameof(CmdClearCooldown));
        }

        private void Unload()
        {
            foreach (var netId in BradleyDropData.Keys)
            {
                var apc = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BradleyAPC;
                if (apc != null)
                {
                    var bradleyComp = apc.gameObject.GetComponent<BradleyDrop>();
                    if (bradleyComp != null)
                        UnityEngine.Object.DestroyImmediate(bradleyComp);
                    
                    apc.Kill();
                }
            }

            foreach (var ent in CargoPlaneList)
            {
                if (ent != null)
                {
                    var planeComp = ent.gameObject.GetComponent<BradleyDropPlane>();
                    if (planeComp != null)
                        UnityEngine.Object.DestroyImmediate(planeComp);
                    
                    ent.Kill();
                }
            }

            if (config.options.noVanillaApc)
            {
                ConVar.Bradley.enabled = true;
                Puts($"Vanilla Bradley APC event at Launch Site has been re-enabled");
            }

            BradleyDropData.Clear();
            CargoPlaneList.Clear();
            PlayerCooldowns.Clear();
            TierCooldowns.Clear();
            DropTypes.Clear();
            BradleyProfileCache.Clear();
            RewardPlugin = null;
            Instance = null;
        }

        // Re-Adds supply signal names to items added via kits/loadouts by plugins
        // which don't specify a custom item display name.
        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (item.info.itemid == supplySignalId)
                NextTick(()=> FixSignalInfo(item));
        }

        private void OnExplosiveThrown(BasePlayer player, SupplySignal entity, ThrownWeapon item)
        {
            var signal = item.GetItem();
            if (signal == null) return;
            
            if (BradleyProfileCache.ContainsKey(signal.skin))
            {
                entity.EntityToCreate = null;
                entity.CancelInvoke(entity.Explode);
                entity.skinID = signal.skin;
                BradleySignalThrown(player, entity, signal);
            }
        }
        private void OnExplosiveDropped(BasePlayer player, SupplySignal entity, ThrownWeapon item) => OnExplosiveThrown(player, entity, item);

        private object CanStackItem(Item item, Item targetItem)
        {
            if (item == null || targetItem == null)
                return null;
            
            if (item.info.itemid == targetItem.info.itemid && item.skin != targetItem.skin)
                return false;
            
            return null;
        }

        private object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item == null || targetItem == null)
                return null;
            
            if (item.item.info.itemid == targetItem.item.info.itemid && item.item.skin != targetItem.item.skin)
                return true;
            
            return null;
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
			NextTick(()=>
            {
                if (entity == null)
                    return;
                
            	if (entity.ShortPrefabName.Contains("maincannonshell"))
            	{
                    if (BradleyDropData.Count == 0)
                        return;
                    
                    TimedExplosive shellEnt = entity as TimedExplosive;
                    BradleyAPC bradley = shellEnt.creatorEntity as BradleyAPC;
                    string apcProfile = bradley?._name;
                    if (shellEnt == null || apcProfile == null)
                    	return;
                        
                    if (!config.bradley.apcConfig.ContainsKey(apcProfile))
                        return;
                        
                    shellEnt.OwnerID = bradley.OwnerID;
                    SetDamageScale(shellEnt, config.bradley.apcConfig[apcProfile].ShellDamageScale);
            	}
            	else if (entity.ShortPrefabName.Equals("oilfireballsmall"))
            	{
                    // Set the owner ID & name of fire on Bradley crates to stop NRE from
                    // OnEntityTakeDamage since LockedEnt crate fires arent assigned this.
                    BaseEntity parentEnt = entity.GetParentEntity();
                    if (parentEnt != null)
                    {
                        BaseEntity baseEnt = entity as BaseEntity;
                        baseEnt.OwnerID = parentEnt.OwnerID;
                        baseEnt._name = parentEnt._name;
                    }
            	}
            });
        }

        private object OnEntityTakeDamage(BradleyAPC bradley, HitInfo info)
        {
            var Initiator = info?.Initiator;
            if (Initiator == null || Initiator == bradley)
                return null;

            var apcProfile = bradley._name;
            if (apcProfile == null)
                return null;

            if (!config.bradley.apcConfig.ContainsKey(apcProfile))
                return null;
            
            // If bradley touches fireball, allow normal damage and stop NRE
            if (Initiator.ShortPrefabName.Equals("fireball_small"))
                return null;
            
            BradleyDrop bradComp = bradley.GetComponent<BradleyDrop>();
            if (bradComp == null)
                return null;

            if (bradComp.isDespawning)
            {
                info.damageTypes.ScaleAll(0);
                return true;
            }

            var apcName = config.bradley.apcConfig[apcProfile].APCName;
            BasePlayer attacker = Initiator as BasePlayer;
            if (Initiator is BasePlayer)
            {
                attacker = Initiator as BasePlayer;
            }
            else if (Initiator is AutoTurret)
            {
                AutoTurret turret = Initiator as AutoTurret;
                ulong playerId = turret.ControllingViewerId.GetValueOrDefault().SteamId;
                attacker = BasePlayer.FindByID(playerId);
                if (attacker == null)
                    return null;
                
                if (!config.bradley.allowTurretDamage)
                {
                    info.damageTypes.ScaleAll(0);
                    ComputerStation station = attacker.GetMounted() as ComputerStation;
                    if (station != null)
                    {
                        timer.Once(0.25f, ()=> Effect.server.Run(shockSound, station.transform.position));
                        timer.Once(0.5f, ()=> Effect.server.Run(deniedSound, station.transform.position));
                    }
                    attacker.EnsureDismounted();
                    Message(attacker, "NoTurret", apcName);
                    return true;
                }
            }

            if (!bradComp.hasLanded && config.bradley.apcConfig[apcProfile].ChuteProtected)
            {
                info.damageTypes.ScaleAll(0);
                Message(attacker, "NotLanded", apcName);
                return true;
            }

            if (config.bradley.apcConfig[apcProfile].OwnerDamage && !IsOwnerOrFriend(attacker.userID, bradley.OwnerID))
            {
                info.damageTypes.ScaleAll(0);
                Message(attacker, "CannotDamage", apcName);
                return true;
            }

            var dist = Vector3.Distance(bradley.transform.position, attacker.transform.position);
            var maxDist = config.bradley.maxHitDistance;
            if (maxDist > 0 && dist > maxDist)
            {
                info.damageTypes.ScaleAll(0);
                Message(attacker, "TooFarAway", apcName, maxDist);
                return true;
            }

            var bradleyId = bradley.net.ID.Value;
            if (config.bradley.DespawnWarning && attacker.IsBuildingBlocked() && IsOwnerOrFriend(attacker.userID, bradley.OwnerID))
            {
                BradleyDropData[bradleyId].WarningLevel++;
                if (!bradComp.isDespawning && BradleyDropData[bradleyId].WarningLevel >= config.bradley.WarningThreshold)
                {
                    bradComp.isDespawning = true;
                    info.damageTypes.ScaleAll(0);
                    DespawnAPC(bradley);
                    Message(attacker, "DespawnApc", apcName, BradleyDropData[bradleyId].WarningLevel, config.bradley.WarningThreshold);
                    return true;
                }

                info.damageTypes.ScaleAll(0);
                Message(attacker, "DespawnWarn", apcName, BradleyDropData[bradleyId].WarningLevel, config.bradley.WarningThreshold);
                return true;
            }

            if (!BradleyDropData.ContainsKey(bradleyId))
                BradleyDropData.Add(bradleyId, new ApcStats());

            if (!BradleyDropData[bradleyId].Attackers.ContainsKey(attacker.userID))
            {
                BradleyDropData[bradleyId].Attackers.Add(attacker.userID, new AttackersStats());
                BradleyDropData[bradleyId].Attackers[attacker.userID].Name = attacker.displayName;
            }

            BradleyDropData[bradleyId].Attackers[attacker.userID].DamageDealt += info.damageTypes.Total();
            BradleyDropData[bradleyId].LastAttacker = attacker;

            return null;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var Initiator = info?.Initiator;
            if (Initiator == null)
                return null;

            if (Initiator is BradleyAPC)
            {
                BradleyAPC bradley = Initiator as BradleyAPC;
                if (bradley == null)
                    return null;
                
                if (!BradleyDropData.ContainsKey(bradley.net.ID.Value))
                    return null;

                string apcProfile = bradley._name;
                if (apcProfile == null)
                    return null;

                if (config.bradley.apcConfig.ContainsKey(apcProfile))
                {
                    if (entity is BasePlayer)
                    {
                        BasePlayer player = entity as BasePlayer;
                        if (player == null)
                            return null;
                        
                        if (config.bradley.apcConfig[apcProfile].BlockPlayerDamage && !IsOwnerOrFriend(bradley.OwnerID, player.userID))
                        {
                            info.damageTypes.ScaleAll(0);
                            return true;
                        }
                        else if (info.damageTypes.GetMajorityDamageType() == DamageType.Bullet)
                        {
                            info.damageTypes.ScaleAll(config.bradley.apcConfig[apcProfile].GunDamage);
                            float hitProb = (float)random.Next(1, 100);
                            if (config.bradley.apcConfig[apcProfile].GunAccuracy < hitProb)
                            {
                                info.damageTypes.ScaleAll(0);
                                return true;
                            }
                        }
                    }
                    else if (entity.OwnerID.IsSteamId())
                    {
                        if (config.bradley.apcConfig[apcProfile].BlockOtherDamage && !IsOwnerOrFriend(bradley.OwnerID, entity.OwnerID))
                        {
                            info.damageTypes.ScaleAll(0);
                            return true;
                        }
                        if (config.bradley.apcConfig[apcProfile].BlockOwnerDamage && IsOwnerOrFriend(bradley.OwnerID, entity.OwnerID))
                        {
                            info.damageTypes.ScaleAll(0);
                            return true;
                        }
                    }
                }
            }
            else if (Initiator.ShortPrefabName.Equals("oilfireballsmall"))
            {
                string apcProfile = Initiator?._name;
                if (apcProfile == null)
                    return null;

                if (!config.bradley.apcConfig.ContainsKey(apcProfile))
                    return null;

                var apcOwner = Initiator.OwnerID;
                if (apcOwner == null)
                    return null;
                
                if (entity is BasePlayer)
                {
                    BasePlayer player = entity as BasePlayer;
                    if (player == null)
                        return null;
                    
                    if (config.bradley.apcConfig[apcProfile].BlockPlayerDamage && !IsOwnerOrFriend(apcOwner, player.userID))
                    {
                        info.damageTypes.ScaleAll(0);
                        return true;
                    }
                }
                else if (entity.OwnerID.IsSteamId())
                {
                    if (config.bradley.apcConfig[apcProfile].BlockOtherDamage && !IsOwnerOrFriend(apcOwner, entity.OwnerID))
                    {
                        info.damageTypes.ScaleAll(0);
                        return true;
                    }
                    else if (config.bradley.apcConfig[apcProfile].BlockOwnerDamage && IsOwnerOrFriend(apcOwner, entity.OwnerID))
                    {
                        info.damageTypes.ScaleAll(0);
                        return true;
                    }
                }
            }
            return null;
        }

        private object OnEntityDestroy(BradleyAPC bradley)
        {
            if (bradley == null || !BradleyDropData.ContainsKey(bradley.net.ID.Value))
                return null;

            var skinId = bradley.skinID;
            var position = bradley.transform.position;
            var ownerId = bradley.OwnerID;
            var apcProfile = bradley._name;
            var bradleyId = bradley.net.ID.Value;

            var gridPos = PositionToGrid(position);
            if (gridPos == null)
                gridPos = "Unknown";

            BasePlayer lastAttacker = BradleyDropData[bradleyId].LastAttacker;
            if (lastAttacker == null)
                return null;

            var totalReward = config.bradley.apcConfig[apcProfile].RewardPoints;
            var totalScrap = config.bradley.apcConfig[apcProfile].ScrapReward;
            float damageThreshold = config.bradley.apcConfig[apcProfile].DamageThreshold;

            if (config.rewards.enableRewards && totalReward > 0)
            {
                if (plugins.Find(config.rewards.rewardPlugin))
                {
                    if (config.rewards.shareRewards)
                    {
                        foreach (var playerId in BradleyDropData[bradleyId].Attackers.Keys)
                        {
                            float damageDealt = BradleyDropData[bradleyId].Attackers[playerId].DamageDealt;
                            if (damageDealt >= damageThreshold)
                            {
                                var amount = totalReward / BradleyDropData[bradleyId].Attackers.Count(key => key.Value.DamageDealt >= damageThreshold);
                                NextTick(()=> GiveReward(playerId, amount, apcProfile));
                            }
                        }
                    }
                    else
                    {
                        NextTick(()=> GiveReward(ownerId, totalReward, apcProfile));
                    }
                }
            }

            if (config.rewards.enableScrap && totalScrap > 0)
            {
                if (config.rewards.shareScrap)
                {
                    foreach (var playerId in BradleyDropData[bradleyId].Attackers.Keys)
                    {
                        float damageDealt = BradleyDropData[bradleyId].Attackers[playerId].DamageDealt;
                        if (damageDealt >= damageThreshold)
                        {
                            var amount = totalScrap / BradleyDropData[bradleyId].Attackers.Count(key => key.Value.DamageDealt >= damageThreshold);
                            NextTick(()=> GiveScrap(playerId, amount, apcProfile));
                        }
                    }
                }
                else
                {
                    NextTick(()=> GiveScrap(ownerId, totalScrap, apcProfile));
                }
            }

            if (config.announce.killChat)
            {
                TimeSpan timeSpan = TimeSpan.FromSeconds(UnityEngine.Time.realtimeSinceStartup - BradleyDropData[bradleyId].StartTime);
                string time = timeSpan.ToString(@"hh\:mm\:ss");
                var apcDisplayName = config.bradley.apcConfig[apcProfile].APCName;

                var message = string.Format(lang.GetMessage("BradleyKilledTime", this), apcDisplayName, lastAttacker.displayName, gridPos, time);
                AnnounceToChat(lastAttacker, message);
            }

            if (config.announce.reportChat)
            {
                string topReport = string.Empty;
                int count = 1;
                foreach (var key in BradleyDropData[bradleyId].Attackers.Keys.OrderByDescending(x => BradleyDropData[bradleyId].Attackers[x].DamageDealt))
                {
                    if (count >= config.announce.maxReported)
                        break;
                    
                    string playerName = BradleyDropData[bradleyId].Attackers[key].Name;
                    float damageDealt = BradleyDropData[bradleyId].Attackers[key].DamageDealt;
                    topReport += string.Format(lang.GetMessage("DamageReportIndex", this, lastAttacker.UserIDString), count, playerName, Math.Round(damageDealt, 2));
                    count++;
                }

                var dmgReport = string.Format(lang.GetMessage("DamageReport", this, lastAttacker.UserIDString), topReport);
                AnnounceToChat(lastAttacker, dmgReport);
            }

            if (config.discord.sendApcKill)
            {
                string discordMsg = string.Format(lang.GetMessage("DiscordKill", this, lastAttacker.UserIDString), lastAttacker.displayName, apcProfile, gridPos);
                SendToDiscord(discordMsg);
            }

            if (config.bradley.apcConfig[apcProfile].LockedCratesToSpawn > 0)
                SpawnLockedCrates(ownerId, skinId, apcProfile, position);

            NextTick(() =>
            {
                List<BaseEntity> ents = new List<BaseEntity>();
                Vis.Entities(position, 15f, ents);
                foreach (var ent in ents)
                {
                    if ((ent is HelicopterDebris) || (ent is LockedByEntCrate) || (ent is FireBall))
                    {
                        ent.OwnerID = ownerId;
                        ent.skinID = skinId;
                        ent._name = apcProfile;
                        
                        ProcessBradleyEnt(ent);
                    }
                }
            });

            if (BotReSpawn && BotReSpawn.IsLoaded)
            {
                var botReSpawnProfile = config.bradley.apcConfig[apcProfile].BotReSpawnProfile;
                if (botReSpawnProfile != string.Empty)
                    BotReSpawn?.Call("AddGroupSpawn", position, botReSpawnProfile, $"{botReSpawnProfile}Group", 1);
            }

            timer.Once(5f, () =>
            {
                if (BradleyDropData.ContainsKey(bradleyId))
                    BradleyDropData.Remove(bradleyId);
            });

            return null;
        }
    
        private object CanLootEntity(BasePlayer player, LockedByEntCrate entity)
        {
            if (entity.OwnerID == 0)
                return null;

            string apcProfile = entity?._name;
            if (apcProfile == null)
                return null;

            if (config.bradley.apcConfig.ContainsKey(apcProfile))
            {
                if (config.bradley.apcConfig[apcProfile].ProtectCrates)
                {
                    if (permission.UserHasPermission(player.UserIDString, permAdmin))
                        return null;
                    
                    if (!IsOwnerOrFriend(player.userID, entity.OwnerID))
                    {
                        Message(player, "CannotLoot");
                        return true;
                    }
                }
            }
            return null;
        }

        private object OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            BaseEntity entity = info?.HitEntity;
            if (entity == null)
                return null;

            if (entity.OwnerID == 0)
                return null;

            if (entity is ServerGib)
            {
                var apcProfile = entity._name;
                if (apcProfile == null)
                    return null;
                
                if (config.bradley.apcConfig.ContainsKey(apcProfile))
                {
                    if (config.bradley.apcConfig[apcProfile].ProtectGibs)
                    {
                        if (permission.UserHasPermission(attacker.UserIDString, permAdmin))
                            return null;
                        
                        if (!IsOwnerOrFriend(attacker.userID, entity.OwnerID))
                        {
                            info.damageTypes.ScaleAll(0);
                            Message(attacker, "CannotMine");
                            return true;
                        }
                    }
                }
            }
            return null;
        }

        private void OnLootSpawn(LootContainer lootContainer)
        {
            timer.Once(2f, () =>
            {
                var apcProfile = lootContainer?._name;
                if (apcProfile == null)
                    return;

                if (config.bradley.apcConfig.ContainsKey(apcProfile))
                {
                    if (lootContainer.ShortPrefabName.Contains("bradley_crate") && config.bradley.apcConfig[apcProfile].Loot.UseCustomLoot)
                        SpawnBradleyCrateLoot(lootContainer, apcProfile);

                    else if (lootContainer.ShortPrefabName.Contains("bradley_crate") && config.bradley.apcConfig[apcProfile].ExtraLoot.UseExtraLoot)
                        AddExtraBradleyCrateLoot(lootContainer, apcProfile);

                    else if (lootContainer.ShortPrefabName.Contains("codelockedhackablecrate") && config.bradley.apcConfig[apcProfile].LockedCrateLoot.UseLockedCrateLoot)
                        SpawnLockedCrateLoot(lootContainer, apcProfile);
                }
            });
        }

        // private string PositionToGrid(Vector3 position) => PhoneController.PositionToGridCoord(position);
        
        private string PositionToGrid(Vector3 pos)
        {
			char letter = 'A';
			var x = Mathf.Floor((pos.x+(ConVar.Server.worldsize/2)) / 146.3f)%26;
			var z = (Mathf.Floor(ConVar.Server.worldsize/146.3f))-Mathf.Floor((pos.z+(ConVar.Server.worldsize/2)) / 146.3f);
			letter = (char)(((int)letter)+x);
			return $"{letter}{z}";
        }

        private void OnSupplyDropDropped(SupplyDrop supplyDrop, CargoPlane plane)
        {
            if (plane != null)
            {
                var planeComp = plane.gameObject.GetComponent<BradleyDropPlane>();
                if (planeComp != null)
                {
                    var position = supplyDrop.transform.position;
                    supplyDrop.Kill();
                    SpawnBradleyDrop(position, plane.transform.eulerAngles, planeComp.player, planeComp.skinId, planeComp.apcProfile);
                    GameManager.DestroyImmediate(planeComp);

                    if (CargoPlaneList.Contains(plane))
                        CargoPlaneList.Remove(plane);
                }
            }
        }

        private object CanBradleyApcTarget(BradleyAPC bradley, BaseEntity target)
        {
            var apcProfile = bradley._name;
            if (apcProfile == null)
                return null;
            
            if (!config.bradley.apcConfig.ContainsKey(apcProfile))
                return null;

            if (target.IsNpc)
                return false;

            BasePlayer player = target as BasePlayer;
            if (player != null)
            {
                if (Vanish && (bool)Vanish?.Call("IsInvisible", player))
                    return false;

                if (!config.bradley.apcConfig[apcProfile].TargetSleepers && player.IsSleeping())
                    return false;
                
                if (!config.bradley.apcConfig[apcProfile].AttackOwner && IsOwnerOrFriend(player.userID, bradley.OwnerID))
                    return false;

                if (!config.bradley.apcConfig[apcProfile].TargetOtherPlayers && !IsOwnerOrFriend(player.userID, bradley.OwnerID))
                    return false;

                var mainTurretPosition = bradley.mainTurret.transform.position;
                if (!(player.IsVisible(mainTurretPosition, bradley.CenterPoint()) || player.IsVisible(mainTurretPosition, player.eyes.position) || player.IsVisible(mainTurretPosition, player.transform.position)))
                    return false;
            }
            return null;
        }

        private object OnBradleyApcInitialize(BradleyAPC bradley)
        {
            var apcProfile = bradley._name;
            if (apcProfile == null)
                return null;

            if (config.bradley.apcConfig.ContainsKey(apcProfile))
            {
                bradley._maxHealth = config.bradley.apcConfig[apcProfile].Health;
                bradley.health = bradley._maxHealth;
                bradley.viewDistance = 0f;  // Set to 0 so APC doesn't target while parachuting
                bradley.searchRange = 0f;   // Set to 0 so APC doesn't target while parachuting
            }
            return null;
        }

        #endregion Oxide Hooks

        #region Core

        private void BradleySignalThrown(BasePlayer player, SupplySignal entity, Item signal)
        {
            if (player == null || entity == null || signal == null)
                return;

            var skinId = signal.skin;
            var apcProfile = signal.name;
            var permSuffix = config.bradley.apcConfig[apcProfile].GiveItemCommand.ToLower();
            var perm = $"{Name.ToLower()}.{permSuffix}";

            if (string.IsNullOrEmpty(apcProfile))
            {
                BradleyProfileCache.TryGetValue(skinId, out apcProfile);
                if (string.IsNullOrEmpty(apcProfile))
                {
                    if (entity != null) entity.Kill();
                    return;
                }
            }

            if (!permission.UserHasPermission(player.UserIDString, perm))
            {
                Message(player, "Permission", apcProfile);
                NextTick(() =>
                {
                    if (entity != null) entity.Kill();
                });
                GiveBradleyDrop(player, skinId, apcProfile, 1, "refund");
                return;
            }

            if (config.bradley.useNoEscape && NoEscape)
            {
                if ((bool)NoEscape.CallHook("IsRaidBlocked", player))
                {
                    Message(player, "RaidBlocked", apcProfile);
                    NextTick(() =>
                    {
                        if (entity != null) entity.Kill();
                    });
                    GiveBradleyDrop(player, skinId, apcProfile, 1, "refund");
                    return;
                }
                else if ((bool)NoEscape.CallHook("IsCombatBlocked", player))
                {
                    Message(player, "CombatBlocked", apcProfile);
                    NextTick(() =>
                    {
                        if (entity != null) entity.Kill();
                    });
                    GiveBradleyDrop(player, skinId, apcProfile, 1, "refund");
                    return;
                }
            }

            if (config.bradley.playerCooldown > 0f && !permission.UserHasPermission(player.UserIDString, permBypasscooldown))
            {
                float cooldown;
                ulong userId = player.userID;
                if (!config.bradley.tierCooldowns)
                {
                    if (PlayerCooldowns.TryGetValue(player.userID, out cooldown))
                    {
                        TimeSpan time = TimeSpan.FromSeconds(cooldown - UnityEngine.Time.time);
                        Message(player, "CooldownTime", time.ToString(@"hh\:mm\:ss"));
                        NextTick(() =>
                        {
                            if (entity != null) entity.Kill();
                        });
                        GiveBradleyDrop(player, skinId, apcProfile, 1, "refund");
                        return;
                    }
                    else if (config.bradley.teamCooldown)
                    {
                        foreach (var playerId in PlayerCooldowns.Keys)
                        {
                            if (PlayerCooldowns.TryGetValue(playerId, out cooldown) && IsOwnerOrFriend(player.userID, playerId))
                            {
                                TimeSpan time = TimeSpan.FromSeconds(cooldown - UnityEngine.Time.time);
                                Message(player, "TeamCooldownTime", time.ToString(@"hh\:mm\:ss"));
                                NextTick(() =>
                                {
                                    if (entity != null) entity.Kill();
                                });
                                GiveBradleyDrop(player, skinId, apcProfile, 1, "refund");
                                return;
                            }
                        }
                    }
                }
                else
                {
                    if (TierCooldowns.ContainsKey(userId))
                    {
                        if (TierCooldowns[userId].TryGetValue(apcProfile, out cooldown))
                        {
                            TimeSpan time = TimeSpan.FromSeconds(cooldown - UnityEngine.Time.time);
                            Message(player, "CooldownTime", time.ToString(@"hh\:mm\:ss"));
                            NextTick(() =>
                            {
                                if (entity != null) entity.Kill();
                            });
                            GiveBradleyDrop(player, skinId, apcProfile, 1, "refund");
                            return;
                        }
                        else if (config.bradley.teamCooldown)
                        {
                            foreach (var playerId in TierCooldowns.Keys)
                            {
                                if (TierCooldowns[userId].TryGetValue(apcProfile, out cooldown) && IsOwnerOrFriend(userId, playerId))
                                {
                                    TimeSpan time = TimeSpan.FromSeconds(cooldown - UnityEngine.Time.time);
                                    Message(player, "TeamCooldownTime", time.ToString(@"hh\:mm\:ss"));
                                    NextTick(() =>
                                    {
                                        if (entity != null) entity.Kill();
                                    });
                                    GiveBradleyDrop(player, skinId, apcProfile, 1, "refund");
                                    return;
                                }
                            }
                        }
                    }
                }

                cooldown = config.bradley.playerCooldown;
                foreach (KeyValuePair<string, float> keyPair in config.bradley.vipCooldowns)
                {
                    if (permission.UserHasPermission(player.UserIDString, keyPair.Key))
                    {
                        cooldown = keyPair.Value;
                        break;
                    }
                }

                if (!config.bradley.tierCooldowns)
                {
                    if (!PlayerCooldowns.ContainsKey(userId))
                        PlayerCooldowns.Add(userId, UnityEngine.Time.time + cooldown);
                    else
                        PlayerCooldowns[userId] = UnityEngine.Time.time + cooldown;
                    
                    timer.Once(cooldown, () =>
                    {
                        if (PlayerCooldowns.ContainsKey(userId))
                            PlayerCooldowns.Remove(userId);
                    });
                }
                else
                {
                    if (!TierCooldowns.ContainsKey(userId))
                        TierCooldowns.Add(userId, new Dictionary<string, float>());

                    if (!TierCooldowns[userId].ContainsKey(apcProfile))
                        TierCooldowns[userId].Add(apcProfile, UnityEngine.Time.time + cooldown);
                    else
                        TierCooldowns[userId][apcProfile] = UnityEngine.Time.time + cooldown;

                    timer.Once(cooldown, () =>
                    {
                        if (!TierCooldowns.ContainsKey(userId))
                            return;
                        
                        if (TierCooldowns[userId].ContainsKey(apcProfile))
                            TierCooldowns[userId].Remove(apcProfile);
                    });
                }
            }

            BradleyDropSignal signalComponent = entity.gameObject.AddComponent<BradleyDropSignal>();
            if (signalComponent != null)
            {
                signalComponent.signal = entity;
                signalComponent.player = player;
                signalComponent.skinId = skinId;
                signalComponent.apcProfile = apcProfile;
            }
        }

        private BaseEntity SpawnBradleyDrop(Vector3 position, Vector3 eulerAngles, BasePlayer player, ulong skinId, string apcProfile)
        {
            BradleyAPC bradley = GameManager.server.CreateEntity(bradleyPrefab, new Vector3(), new Quaternion()) as BradleyAPC;
            if (bradley == null)
                return null;

            bradley.syncPosition = true;
            bradley.EnableGlobalBroadcast(true);
            bradley.OwnerID = player.userID;
            bradley.skinID = skinId;
            bradley._name = apcProfile;
            bradley.Spawn();

            bradley.transform.position = position;
            bradley.transform.eulerAngles = eulerAngles;
            bradley.transform.hasChanged = true;
            bradley.SendNetworkUpdateImmediate(true);

            bradley.gameObject.AddComponent<BradleyDrop>();
            var bradleyComp = bradley.gameObject.GetComponent<BradleyDrop>();
            if (bradleyComp == null)
                return null;
            
            bradleyComp.owner = player;
            bradleyComp.bradley = bradley;
            bradleyComp.apcProfile = apcProfile;

            BradleyDropData.Add(bradley.net.ID.Value, new ApcStats());
            BradleyDropData[bradley.net.ID.Value].OwnerID = player.userID;

            if (config.bradley.apcConfig[apcProfile].DespawnTime > 0)
                DespawnAPC(bradley, config.bradley.apcConfig[apcProfile].DespawnTime);

            return bradley;
        }

        private void ProcessBradleyEnt(BaseEntity entity)
        {
            if (entity != null)
            {
                var apcProfile = entity._name;
                if (apcProfile != null)
                {
                    if (entity is HelicopterDebris)
                    {
                        var debris = entity?.GetComponent<HelicopterDebris>() ?? null;
                        if (debris != null)
                        {
                            debris.InitializeHealth(config.bradley.apcConfig[apcProfile].GibsHealth, config.bradley.apcConfig[apcProfile].GibsHealth);
                            debris.SendNetworkUpdate();

                            if (config.bradley.apcConfig[apcProfile].KillGibs)
                                entity.Kill();
                            
                            else if (config.bradley.apcConfig[apcProfile].DisableFire)
                                debris.tooHotUntil = UnityEngine.Time.realtimeSinceStartup;
                            
                            else if (config.bradley.apcConfig[apcProfile].GibsHotTime > 0)
                                debris.tooHotUntil = UnityEngine.Time.realtimeSinceStartup + config.bradley.apcConfig[apcProfile].GibsHotTime;

                            if (config.bradley.apcConfig[apcProfile].ProtectGibs && config.bradley.apcConfig[apcProfile].UnlockGibs > 0)
                            {
                                float unlockTime = config.bradley.apcConfig[apcProfile].DisableFire ? config.bradley.apcConfig[apcProfile].UnlockGibs :
                                                    (config.bradley.apcConfig[apcProfile].FireDuration + config.bradley.apcConfig[apcProfile].UnlockGibs);
                                RemoveBradleyOwner(entity, unlockTime);
                            }
                            debris.SendNetworkUpdateImmediate();
                        }
                    }
                    else if (entity is FireBall)
                    {
                        var fireball = entity?.GetComponent<FireBall>() ?? null;
                        if (fireball != null)
                        {
                            if (config.bradley.apcConfig[apcProfile].DisableFire)
                            {
                                fireball.Kill();
                            }
                            else
                            {
                                timer.Once(config.bradley.apcConfig[apcProfile].FireDuration, () =>
                                {
                                    if (fireball != null)
                                    {
                                        fireball.Kill();
                                    }
                                });
                            }
                            fireball.SendNetworkUpdateImmediate();
                        }
                    }
                    else if (entity is LockedByEntCrate)
                    {
                        var crate = entity?.GetComponent<LockedByEntCrate>();
                        if (crate != null)
                        {
                            if (config.bradley.apcConfig[apcProfile].FireDuration > 0 && !config.bradley.apcConfig[apcProfile].DisableFire)
                            {
                                timer.Once(config.bradley.apcConfig[apcProfile].FireDuration, () =>
                                {
                                    if (crate != null)
                                    {
                                        if (crate.lockingEnt != null)
                                        {
                                            var lockingEnt = crate.lockingEnt.GetComponent<FireBall>();
                                            if (lockingEnt != null)
                                                NextTick(()=> lockingEnt.Kill());
                                        }
                                        crate.CancelInvoke(crate.Think);
                                        crate.SetLocked(false);
                                        crate.lockingEnt = null;
                                    }
                                });
                            }
                            else
                            {
                                if (crate.lockingEnt != null)
                                {
                                    var lockingEnt = crate?.lockingEnt.GetComponent<FireBall>();
                                    if (lockingEnt != null)
                                        NextTick(()=> lockingEnt.Kill());
                                }
                                crate.CancelInvoke(crate.Think);
                                crate.SetLocked(false);
                                crate.lockingEnt = null;
                            }
                            
                            if (config.bradley.apcConfig[apcProfile].ProtectCrates && config.bradley.apcConfig[apcProfile].UnlockCrates > 0)
                            {
                                float unlockTime = config.bradley.apcConfig[apcProfile].DisableFire ? config.bradley.apcConfig[apcProfile].UnlockCrates :
                                                    (config.bradley.apcConfig[apcProfile].FireDuration + config.bradley.apcConfig[apcProfile].UnlockCrates);
                                RemoveBradleyOwner(entity, unlockTime);
                            }
                        }
                    }
                }
            }
        }

        #endregion Core

        #region Loot

        private void SpawnBradleyCrateLoot(LootContainer lootContainer, string apcProfile)
        {
            if (lootContainer == null || apcProfile == null)
                return;

            lootContainer.inventory.capacity = 12; // unlock all slots
            lootContainer.inventory.Clear();
            ItemManager.DoRemoves();

            List<LootItem> lootTable = new List<LootItem>(config.bradley.apcConfig[apcProfile].Loot.LootTable);
            List<LootItem> items = lootTable;

            if (items == null || lootTable == null)
                return;

            var minItems = config.bradley.apcConfig[apcProfile].Loot.MinCrateItems;
            var maxItems = config.bradley.apcConfig[apcProfile].Loot.MaxCrateItems;
            int count = UnityEngine.Random.Range(minItems, maxItems + 1);
            int given = 0;
            int bps = 0;

            for (int i = 0; i < count; i++)
            {
                LootItem lootItem = items.GetRandom();

                if (lootItem == null)
                {
                    i--;
                    continue;
                }

                if (lootItem.Chance < UnityEngine.Random.Range(0f, 100f))
                {
                    if (given < minItems)
                    {
                        i--;
                        continue;
                    }
                    break;
                }

                // Only remove if enough in loot table (if loot table not adequate)
                if (!config.bradley.apcConfig[apcProfile].Loot.AllowDupes && count <= items.Count)
                    items.Remove(lootItem);
                
                // Re-add loot if items run out (if loot table not adequate)
                if (items.Count == 0)
                    items.AddRange(lootTable);

                ItemDefinition itemDef = ItemManager.FindItemDefinition(lootItem.ShortName);
                Item item = null;

                if (UnityEngine.Random.Range(0f, 100f) <= lootItem.BlueprintChance && itemDef.Blueprint != null && IsBP(itemDef) && bps < config.bradley.apcConfig[apcProfile].Loot.MaxBP)
                {
                    ItemDefinition bpDef = ItemManager.FindItemDefinition("blueprintbase");
                    item = ItemManager.Create(bpDef, 1, 0uL);
                    item.blueprintTarget = itemDef.itemid;
                    bps++;
                }
                else
                {
                    var amount = UnityEngine.Random.Range(lootItem.AmountMin, lootItem.AmountMax + 1);
                    item = ItemManager.Create(itemDef, amount, lootItem.SkinId);
                    if (!string.IsNullOrEmpty(lootItem.DisplayName))
                        item.name = lootItem.DisplayName;
                }

                if (item != null)
                {
                    if (item.MoveToContainer(lootContainer.inventory))
                    {
                        item.MarkDirty();
                        given++;

                        if (item.amount == 0)
                            item.Remove(0f);
                        
                        continue;
                    }
                    item.Remove(0f);
                }
            }
        }

        private void AddExtraBradleyCrateLoot (LootContainer lootContainer, string apcProfile)
        {
            if (lootContainer == null || apcProfile == null)
                return;
            
            List<LootItem> lootTable = new List<LootItem>(config.bradley.apcConfig[apcProfile].ExtraLoot.LootTable);
            List<LootItem> items = lootTable;

            if (items == null || lootTable == null)
                return;

            lootContainer.inventory.capacity = 12; // unlock all slots
            var minItems = config.bradley.apcConfig[apcProfile].ExtraLoot.MinExtraItems;
            var maxItems = config.bradley.apcConfig[apcProfile].ExtraLoot.MaxExtraItems;
            int count = UnityEngine.Random.Range(minItems, maxItems + 1);
            int given = 0;
            int bps = 0;

            for (int i = 0; i < count; i++)
            {
                LootItem lootItem = items.GetRandom();

                if (lootItem == null)
                {
                    i--;
                    continue;
                }

                if (lootItem.Chance < UnityEngine.Random.Range(0f, 100f))
                {
                    if (given < minItems)
                    {
                        i--;
                        continue;
                    }
                    break;
                }

                // Only remove if enough in loot table (if loot table not adequate)
                if (!config.bradley.apcConfig[apcProfile].ExtraLoot.AllowDupes && count <= items.Count)
                    items.Remove(lootItem);
                
                // Re-add loot if items run out (if loot table not adequate)
                if (items.Count == 0)
                    items.AddRange(lootTable);

                ItemDefinition itemDef = ItemManager.FindItemDefinition(lootItem.ShortName);
                Item item = null;

                if (UnityEngine.Random.Range(0f, 100f) <= lootItem.BlueprintChance && itemDef.Blueprint != null && IsBP(itemDef) && bps < config.bradley.apcConfig[apcProfile].ExtraLoot.MaxBP)
                {
                    ItemDefinition bpDef = ItemManager.FindItemDefinition("blueprintbase");
                    item = ItemManager.Create(bpDef, 1, 0uL);
                    item.blueprintTarget = itemDef.itemid;
                    bps++;
                }
                else
                {
                    var amount = UnityEngine.Random.Range(lootItem.AmountMin, lootItem.AmountMax + 1);
                    item = ItemManager.Create(itemDef, amount, lootItem.SkinId);
                    if (!string.IsNullOrEmpty(lootItem.DisplayName))
                        item.name = lootItem.DisplayName;
                }

                if (item != null)
                {
                    if (item.MoveToContainer(lootContainer.inventory))
                    {
                        item.MarkDirty();
                        given++;

                        if (item.amount == 0)
                            item.Remove(0f);
                        
                        continue;
                    }
                    item.Remove(0f);
                }
            }
        }

        private void SpawnLockedCrateLoot(LootContainer lootContainer, string apcProfile)
        {
            if (lootContainer == null || apcProfile == null)
                return;

            lootContainer.inventory.capacity = 36; // unlock all slots
            lootContainer.inventory.Clear();
            ItemManager.DoRemoves();

            List<LootItem> lootTable = new List<LootItem>(config.bradley.apcConfig[apcProfile].LockedCrateLoot.LootTable);
            List<LootItem> items = lootTable;

            if (items == null || lootTable == null)
                return;

            var minItems = config.bradley.apcConfig[apcProfile].LockedCrateLoot.MinLockedCrateItems;
            var maxItems = config.bradley.apcConfig[apcProfile].LockedCrateLoot.MaxLockedCrateItems;
            int count = UnityEngine.Random.Range(minItems, maxItems + 1);
            int given = 0;
            int bps = 0;

            for (int i = 0; i < count; i++)
            {
                LootItem lootItem = items.GetRandom();

                if (lootItem == null)
                {
                    i--;
                    continue;
                }

                if (lootItem.Chance < UnityEngine.Random.Range(0f, 100f))
                {
                    if (given < minItems)
                    {
                        i--;
                        continue;
                    }
                    break;
                }

                // Only remove if enough in loot table (if loot table not adequate)
                if (!config.bradley.apcConfig[apcProfile].LockedCrateLoot.AllowDupes && count <= items.Count)
                    items.Remove(lootItem);
                
                // Re-add loot if items run out (if loot table not adequate)
                if (items.Count == 0)
                    items.AddRange(lootTable);

                ItemDefinition itemDef = ItemManager.FindItemDefinition(lootItem.ShortName);
                Item item = null;

                if (UnityEngine.Random.Range(0f, 100f) <= lootItem.BlueprintChance && itemDef.Blueprint != null && IsBP(itemDef) && bps < config.bradley.apcConfig[apcProfile].LockedCrateLoot.MaxBP)
                {
                    ItemDefinition bpDef = ItemManager.FindItemDefinition("blueprintbase");
                    item = ItemManager.Create(bpDef, 1, 0uL);
                    item.blueprintTarget = itemDef.itemid;
                    bps++;
                }
                else
                {
                    var amount = UnityEngine.Random.Range(lootItem.AmountMin, lootItem.AmountMax + 1);
                    item = ItemManager.Create(itemDef, amount, lootItem.SkinId);
                    if (!string.IsNullOrEmpty(lootItem.DisplayName))
                        item.name = lootItem.DisplayName;
                }

                if (item != null)
                {
                    if (item.MoveToContainer(lootContainer.inventory))
                    {
                        item.MarkDirty();
                        given++;

                        if (item.amount == 0)
                            item.Remove(0f);
                        
                        continue;
                    }
                    item.Remove(0f);
                }
            }
        }

        private void SpawnLockedCrates(ulong ownerId, ulong skinId, string apcProfile, Vector3 position)
        {
            for (int i = 0; i < config.bradley.apcConfig[apcProfile].LockedCratesToSpawn; i++)
            {
                Vector3 newPos = position + UnityEngine.Random.onUnitSphere * 5f;
                newPos.y = TerrainMeta.HeightMap.GetHeight(newPos) + 7f;
                HackableLockedCrate crate = GameManager.server.CreateEntity(hackableCrate, newPos, new Quaternion()) as HackableLockedCrate;
                if (crate == null)
                    return;

                crate._name = apcProfile;
                crate.Spawn();
                crate.Invoke(new Action(crate.DelayedDestroy), config.bradley.apcConfig[apcProfile].LockedCrateDespawn);

                NextTick(()=>
                {
                    crate.OwnerID = ownerId;
                    crate.skinID = skinId;
                });

                LockedCrates.Add(crate.net.ID.Value, ownerId);

                float unlockTime = config.bradley.apcConfig[apcProfile].UnlockCrates;
                if (config.bradley.apcConfig[apcProfile].ProtectCrates && unlockTime > 0)
                {
                    timer.Once(unlockTime, ()=>
                    {
                        // Unlock the crates to anyone after time, if set in config
                        if (crate != null)
                        {
                            crate.OwnerID = 0;
                            if (LockedCrates.ContainsKey(crate.net.ID.Value))
                                LockedCrates.Remove(crate.net.ID.Value);
                        }
                    });
                }
            }
        }

        private void OnCrateHack(HackableLockedCrate crate)
        {
            string apcProfile = crate?._name;
            if (apcProfile == null)
                return;

            if (config.bradley.apcConfig.ContainsKey(apcProfile))
            {
                NextTick(()=>
                {
                    float hackTime = 900f - config.bradley.apcConfig[apcProfile].HackSeconds;
                    if (crate.hackSeconds > hackTime)
                        return;
                    
                    crate.hackSeconds = hackTime;
                });
            }
        }

        private object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            string apcProfile = crate?._name;
            if (apcProfile == null)
                return null;

            if (config.bradley.apcConfig.ContainsKey(apcProfile))
            {
                ulong crateId = crate.net.ID.Value;
                ulong crateOwnerId;

                if (!LockedCrates.TryGetValue(crateId, out crateOwnerId))
                    return null;

                if (config.bradley.apcConfig[apcProfile].ProtectCrates && !IsOwnerOrFriend(player.userID, crateOwnerId))
                {
                    Message(player, "CannotHack");
                    return false;
                }
            }
            return null;
        }

        #endregion Loot

        #region Discord Announcements

        private void SendToDiscord(string content)
        {
            if (string.IsNullOrEmpty(config.discord.webHookUrl) || config.discord.webHookUrl == defaultWebhookUrl)
            {
                Puts($"Discord message not sent, please check WebHook URL within the config.");
                return;
            }

            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Content-Type", "application/json");

            string discordMessage = "{\"content\": \"" + content + "\"}";
            NextTick(()=> webrequest.Enqueue(config.discord.webHookUrl, discordMessage, SendToDiscordCallback, this, RequestMethod.POST, header));
        }

        private void SendToDiscordCallback(int callbackCode, string callbackMsg)
        {
            if (callbackCode != 204)
                Puts($"ERROR: {callbackMsg}");
        }

        #endregion Discord Announcements

        #region API

        // Other devs can call this hook to help with compatability with their plugins if needed
        object IsBradleyDrop(ulong skinId) => BradleyProfileCache.ContainsKey(skinId) ? true : (object)null;

        // Play nice with FancyDrop
        object ShouldFancyDrop(NetworkableId netId)
        {
            var signal = BaseNetworkable.serverEntities.Find(netId) as BaseEntity;
            if (signal != null)
            {
                if (BradleyProfileCache.ContainsKey(signal.skinID))
                    return false;
            }
            return null;
        }

        // Play nice with AlphaLoot
        object CanPopulateLoot(LootContainer lootContainer)
        {
            return BradleyProfileCache.ContainsKey(lootContainer.skinID) ? true : (object)null;
        }

        // Play nice with TruePVE etc
        object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo?.Initiator == null)
                return null;

            string apcProfile = hitInfo.Initiator._name;
            if (apcProfile == null)
                return null;

            if (config.bradley.apcConfig.ContainsKey(apcProfile))
            {
                return true;
            }

            return null;
        }

        // Play nice with BotReSpawn
        object OnBotReSpawnAPCKill(BradleyAPC bradley)
        {
            if (BradleyDropData.ContainsKey(bradley.net.ID.Value))
                return true;

            return null;
        }

        // Play nice with Dynamic PvP
        private object OnCreateDynamicPVP(string eventName, BaseEntity entity)
        {
            if (!config.options.useDynamicPVP && BradleyDropData.ContainsKey(entity.net.ID.Value))
                return true;

            return null;
        }

        #endregion API

        #region Rewards

        private void CheckRewardPlugin()
        {
            if (config.rewards.enableRewards || CanPurchaseAnySignal())
            {
                RewardPlugin = plugins.Find(config.rewards.rewardPlugin);
                if (RewardPlugin == null)
                    Puts($"{config.rewards.rewardPlugin} not found, giving rewards is not possible until this is loaded.");
            }
        }

        private void GiveReward(ulong playerId, double amount, string apcProfile)
        {
            foreach (KeyValuePair<string, float> keyPair in config.rewards.rewardMultipliers)
            {
                if (permission.UserHasPermission(playerId.ToString(), keyPair.Key))
                {
                    if (keyPair.Value > 1.0f)
                        amount *= keyPair.Value;
                }
            }

            BasePlayer player = FindPlayer(playerId.ToString())?.Object as BasePlayer;
            if (player)
            {
                switch (config.rewards.rewardPlugin.ToLower())
                {
                    case "serverrewards":
                        RewardPlugin?.Call("AddPoints", playerId, (int)amount);
                        Message(player, "RewardGiven", (int)amount, config.rewards.rewardUnit, apcProfile);
                        break;
                    case "economics":
                        RewardPlugin?.Call("Deposit", playerId, amount);
                        Message(player, "RewardGiven", config.rewards.rewardUnit, (int)amount, apcProfile);
                        break;
                    default:
                        break;
                }
            }
        }

        private void GiveScrap(ulong playerId, int amount, string apcProfile)
        {
            foreach (KeyValuePair<string, float> keyPair in config.rewards.rewardMultipliers)
            {
                if (permission.UserHasPermission(playerId.ToString(), keyPair.Key))
                {
                    if (keyPair.Value > 1.0f)
                        amount = (int)(amount * keyPair.Value);
                }
            }

            BasePlayer player = FindPlayer(playerId.ToString())?.Object as BasePlayer;
            if (player)
            {
                Item scrap = ItemManager.CreateByItemID(scrapId, amount, 0);
                player.inventory.GiveItem(scrap);
                Message(player, "ScrapGiven", amount, apcProfile);
            }
        }

        #endregion Rewards

        #region Helpers

        private void AnnounceToChat(BasePlayer player, string message)
        {
            if (player == null || message == null)
                return;
            
            if (config.announce.announceGlobal)
            {
                Server.Broadcast(message, config.options.usePrefix ? config.options.chatPrefix : null, config.options.chatIcon);
            }
            else
            {
                List<BasePlayer> players = new List<BasePlayer>();
                bool hasClan = false;
                bool hasTeam = false;
                bool hasFriends = false;

                if (config.options.useClans && Clans)
                {
                    List<string> clan = (List<string>)Clans?.Call("GetClanMembers", player.UserIDString);
                    if (clan != null)
                    {
                        foreach (var memberId in clan)
                        {
                            var member = FindPlayer(memberId);
                            if (member == null)
                                continue;
                            
                            if (member.IsConnected)
                            {
                                BasePlayer p = member.Object as BasePlayer;
                                if (!players.Contains(p))
                                    players.Add(p);
                                
                                hasClan = true;
                            }
                        }
                    }
                }

                if (config.options.useTeams)
                {
                    RelationshipManager.PlayerTeam team;
                    RelationshipManager.ServerInstance.playerToTeam.TryGetValue(player.userID, out team);
                    if (team != null)
                    {
                        foreach (var memberId in team.members)
                        {
                            var member = FindPlayer(memberId.ToString());
                            if (member == null)
                                continue;
                            
                            if (member.IsConnected)
                            {
                                BasePlayer p = member.Object as BasePlayer;
                                if (!players.Contains(p))
                                    players.Add(p);
                                
                                hasTeam = true;
                            }
                        }
                    }
                }

                if (config.options.useFriends && Friends)
                {
                    string[] friends = (string[])Friends?.Call("GetFriendList", player.UserIDString);
                    if (friends != null)
                    {
                        foreach (var friendId in friends)
                        {
                            var friend = FindPlayer(friendId.ToString());
                            if (friend == null)
                                continue;
                            
                            if (friend.IsConnected)
                            {
                                BasePlayer p = friend.Object as BasePlayer;
                                if (!players.Contains(p))
                                    players.Add(p);
                                
                                hasFriends = true;
                            }
                        }
                    }
                }

                if (hasTeam == false && hasClan == false && hasFriends == false)
                {
                    Player.Message(player, message, config.options.usePrefix ? config.options.chatPrefix : null, config.options.chatIcon);
                }
                else
                {
                    foreach (var member in players)
                    {
                        Player.Message(player, message, config.options.usePrefix ? config.options.chatPrefix : null, config.options.chatIcon);
                    }
                }
            }
        }

        private bool IsBP(ItemDefinition itemDef) => itemDef?.Blueprint != null && itemDef.Blueprint.isResearchable && !itemDef.Blueprint.defaultBlueprint;

        private bool CanPurchaseAnySignal()
        {
            foreach (var key in config.bradley.apcConfig.Keys)
            {
                if (config.bradley.apcConfig[key].UseBuyCommand && config.purchasing.defaultCurrency != "Custom")
                {
                    return true;
                }
            }
            return false;
        }

        private void SetDamageScale(TimedExplosive shell, float scale)
        {
            foreach (DamageTypeEntry damageType in shell.damageTypes)
                damageType.amount *= scale;
        }

        private bool IsOwnerOrFriend(ulong playerId, ulong targetId)
        {
            if (config.options.useFriends && Friends)
            {
                if ((bool)Friends?.Call("AreFriends", playerId, targetId))
                    return true;
            }
            if (config.options.useClans && Clans)
            {
                if ((bool)Clans?.Call("IsMemberOrAlly", playerId, targetId))
                    return true;
            }
            if (config.options.useTeams)
            {
                RelationshipManager.PlayerTeam team;
                RelationshipManager.ServerInstance.playerToTeam.TryGetValue(playerId, out team);
                if (team != null)
                {
                    if (team.members.Contains(targetId))
                        return true;
                }
            }
            if (playerId == targetId)
                return true;
            
            return false;
        }

        private object FixSignalInfo(Item signal)
        {
            if (signal == null)
                return null;
            
            string apcProfile;
            ulong skinId = signal.skin;
            if (BradleyProfileCache.TryGetValue(skinId, out apcProfile))
            {
                signal.name = apcProfile;
                signal.skin = skinId;
                signal.MarkDirty();
            }
            return signal;
        }

        private void RemoveBradleyOwner(BaseNetworkable entity, float time)
        {
            timer.Once(time, () =>
            {
                if (entity != null || !entity.IsDestroyed)
                {
                    (entity as BaseEntity).OwnerID = 0;
                    entity.SendNetworkUpdateImmediate();
                }
            });
        }

        private void DespawnAPC(BradleyAPC bradley, float despawnTime = 1f)
        {
            timer.Once(despawnTime, () =>
            {
                if (bradley != null)
                {
                    if (config.discord.sendApcDespawn)
                    {
                        var gridPos = PositionToGrid(bradley.transform.position);
                        if (gridPos == null)
                            gridPos = "Unknown";

                        BasePlayer player = FindPlayer(bradley.OwnerID.ToString())?.Object as BasePlayer;
                        if (player == null)
                            return;
                        
                        string discordMsg = string.Format(lang.GetMessage("DiscordDespawn", this), bradley._name, player.displayName, gridPos);
                        SendToDiscord(discordMsg);
                    }

                    if(config.bradley.despawnExplosion)
                        Effect.server.Run(bradleyExplosion, bradley.transform.position);

                    var bradleyId = bradley.net.ID.Value;
                    bradley.Kill();

                    if (BradleyDropData.ContainsKey(bradleyId))
                        BradleyDropData.Remove(bradleyId);
                }
            });
        }

        private bool GiveBradleyDrop(BasePlayer player, ulong skinId, string dropName, int dropAmount, string reason)
        {
            if (player != null && player.IsAlive())
            {
                Item apcDrop = ItemManager.CreateByItemID(supplySignalId, dropAmount, skinId);
                apcDrop.name = dropName;
                if (player.inventory.GiveItem(apcDrop))
                    return true;
                
                apcDrop.Remove(0f);
            }
            return false;
        }

        private bool IsInSafeZone(Vector3 position)
        {
            int loop = Physics.OverlapSphereNonAlloc(position, 1f, Vis.colBuffer, 1 << 18, QueryTriggerInteraction.Collide);
            for (int i = 0; i < loop; i++)
            {
                Collider collider = Vis.colBuffer[i];
                if (collider.GetComponent<TriggerSafeZone>())
                    return true;
            }
            return false;
        }

        private IPlayer FindPlayer(string nameOrIdOrIp)
        {
            foreach (var activePlayer in covalence.Players.Connected)
            {
                if (activePlayer.Id == nameOrIdOrIp)
                    return activePlayer;
                if (activePlayer.Name.Contains(nameOrIdOrIp))
                    return activePlayer;
                if (activePlayer.Name.ToLower().Contains(nameOrIdOrIp.ToLower()))
                    return activePlayer;
                if (activePlayer.Address == nameOrIdOrIp)
                    return activePlayer;
            }
            return null;
        }

        private bool IsNearCollider(BaseEntity entity, bool destroy = false)
        {
            bool didHit = false;
            LayerMask layerMask = (1 << 20) | LayerMask.GetMask("Default", "Construction", "Tree", "Deployed");
            Collider[] hitColliders = Physics.OverlapSphere(entity.transform.position, config.options.proximityRadius, layerMask, QueryTriggerInteraction.UseGlobal);
            if (hitColliders.Length != 0)
            {
                for (int i = 0; i < hitColliders.Length; i++)
                {
                    var collider = hitColliders[i].name.ToLower();
                    if (collider.Contains("clutter") || collider.Contains("collectable") || collider.Contains("driftwood") || collider.Contains("forwardhurttrigger") || collider.Contains("lowercrushtrigger"))
                    {
                        continue;
                    }
                    else
                    {
                        var hitEntity = hitColliders[i].GetComponentInParent<BaseEntity>();
                        if (hitEntity == null || hitEntity == entity || hitEntity is BradleyAPC || hitEntity is LockedByEntCrate)
                            continue;

                        didHit = true;
                        if (destroy)
                        {
                            if (hitEntity != null || !hitEntity.IsDestroyed)
                                hitEntity.Kill();
                            
                            continue;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
                if (didHit) return true;
            }
            return false;
        }

        // Stop people throwing drops too close to their bases and risk having the APC land on structure and get stuck or inside compound etc
        private bool IsInBuildingPriv(Vector3 position, string apcProfile)
        {
            var distance = config.options.buildPrivRadius;
            // Prevent possibility of players bases being destroyed
            // if config option to kill ents in LZ enabled
            if (distance < config.options.proximityRadius)
                distance = config.options.proximityRadius;
            
            var layerMask = LayerMask.GetMask("Construction");
            int loop = Physics.OverlapSphereNonAlloc(position, distance, Vis.colBuffer, layerMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < loop; i++)
            {
                Collider collider = Vis.colBuffer[i];
                if (collider.GetComponentInParent<BaseEntity>().GetBuildingPrivilege())
                    return true;
            }
            return false;
        }

        #endregion Helpers

        #region Commands

        private void CmdReport(IPlayer player, string command, string[] args)
        {
            string activeApcs = String.Empty;
            int count = 0;
            int total = BradleyDropData.Count;

            if (total == 0)
            {
                Message(player, "ApcReportTitleCon", "NO");
                return;
            }

            if (player.IsServer)
                Message(player, "ApcReportTitleCon", total);
            else
                Message(player, "ApcReportTitle", total);

            foreach (var netId in BradleyDropData.Keys)
            {
                var bradley = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BradleyAPC;
                if (bradley != null)
                {
                    if (!IsOwnerOrFriend(bradley.OwnerID, UInt64.Parse(player.Id)) && !permission.UserHasPermission(player.Id, permAdmin))
                        continue;
                    
                    count++;

                    Vector3 position = bradley.transform.position;
                    var gridPos = PositionToGrid(position);
                    if (gridPos == null)
                        gridPos = "Unknown";

                    BradleyDrop bradleyComp = bradley.GetComponent<BradleyDrop>();
                    if (bradleyComp == null)
                        continue;

                    BasePlayer owner = bradleyComp.owner;
                    if (owner == null)
                        continue;
                    
                    string apcProfile = bradleyComp.apcProfile;
                    if (apcProfile == null)
                        continue;

                    var message = String.Empty;
                    if (player.IsServer)
                        message = Lang("ApcReportItemCon", player.Id, count, total, config.bradley.apcConfig[apcProfile].APCName, owner.displayName, gridPos, Math.Round((decimal)bradley.health, 0));
                    else
                        message = Lang("ApcReportItem", player.Id, count, total, config.bradley.apcConfig[apcProfile].APCName, owner.displayName, gridPos, Math.Round((decimal)bradley.health, 0));
                    
                    activeApcs += ($"{message}");
                    message = String.Empty;
                }
            }

            if (config.options.usePrefix)
            {
                config.options.usePrefix = false;
                Message(player, "ApcReportList", activeApcs);
                config.options.usePrefix = true;
            }
            else
            {
                Message(player, "ApcReportList", activeApcs);
            }
            activeApcs = String.Empty;
        }

        private void CmdDespawnApc(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
            {
                Message(player, "InGameOnly");
                return;
            }

            bool didDespawn = false;

            foreach (var netId in BradleyDropData.Keys)
            {
                var bradley = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BradleyAPC;
                if (bradley == null) return;
                
                BasePlayer basePlayer = FindPlayer(player.Id)?.Object as BasePlayer;
                if (basePlayer == null) return;

                if (bradley.OwnerID == basePlayer.userID || (config.bradley.canTeamDespawn && IsOwnerOrFriend(bradley.OwnerID, basePlayer.userID)))
                {
                    DespawnAPC(bradley);
                    Message(player, "DespawnedBradleys");
                }
            }
        }

        private void CmdBuyDrop(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
            {
                Message(player, "InGameOnly");
                return;
            }
            else if (args?.Length < 1 || args?.Length > 1)
            {
                Message(player, "BuyCmdSyntax", config.purchasing.buyCommand, string.Join( "|", DropTypes));
                return;
            }
            
            string currencyItem = config.purchasing.defaultCurrency;
            string priceFormat;
            string priceUnit;

            if (args?[0].ToLower() == "list")
            {
                string list = String.Empty;
                foreach (var key in config.bradley.apcConfig.Keys)
                {
                    switch (currencyItem)
                    {
                        case "ServerRewards":
                            {
                                priceFormat = $"{config.bradley.apcConfig[key].CostToBuy} {config.purchasing.purchaseUnit}";
                            }
                            break;
                        case "Economics":
                            {
                                priceFormat = $"{config.purchasing.purchaseUnit}{config.bradley.apcConfig[key].CostToBuy}";
                            }
                            break;
                        default:
                            {
                                priceFormat = $"{config.bradley.apcConfig[key].CostToBuy} {config.purchasing.customCurrency[0].DisplayName}";
                            }
                            break;
                    }
                    list += ($"{config.bradley.apcConfig[key].APCName} : {priceFormat}\n");
                }
                Message(player, "PriceList", list);
                return;
            }

            string type = args[0].ToLower();
            ulong skinId = 0;
            string apcProfile = string.Empty;

            if (!DropTypes.Contains(type))
            {
                Message(player, "InvalidDrop", type);
                return;
            }

            if (!permission.UserHasPermission(player.Id, permBuy))
            {
                Message(player, "BuyPermission", type);
                return;
            }
            
            foreach (var key in config.bradley.apcConfig.Keys)
            {
                if (type == config.bradley.apcConfig[key].GiveItemCommand.ToLower())
                {
                    skinId = config.bradley.apcConfig[key].SignalSkinID;
                    apcProfile = key;
                    break;
                }
            }

            if (!config.bradley.apcConfig[apcProfile].UseBuyCommand)
            {
                Message(player, "NoBuy", type);
                return;
            }

            BasePlayer basePlayer = player.Object as BasePlayer;
            if (basePlayer == null) return;

            switch (currencyItem)
            {
                case "ServerRewards":
                    {
                        var cost = config.bradley.apcConfig[apcProfile].CostToBuy;
                        var balance = RewardPlugin?.Call("CheckPoints", basePlayer.userID);
                        if (balance != null)
                        {
                            if (cost <= Convert.ToInt32(balance))
                            {
                                if (GiveBradleyDrop(basePlayer, skinId, apcProfile, 1, "give"))
                                {
                                    RewardPlugin?.Call("TakePoints", basePlayer.userID, cost);
                                    Message(player, "Receive", 1, apcProfile);
                                    return;
                                }
                                else
                                {
                                    Message(player, "FullInventory", apcProfile);
                                    return;
                                }
                            }
                            else
                            {
                                Message(player, "CantAfford", $"{cost} {config.purchasing.purchaseUnit}", $"{cost - Convert.ToDouble(balance)} {config.purchasing.purchaseUnit}");
                                return;
                            }
                        }
                    }
                    break;
                case "Economics":
                    {
                        var cost = Convert.ToDouble(config.bradley.apcConfig[apcProfile].CostToBuy);
                        var balance = RewardPlugin?.Call("Balance", basePlayer.userID);
                        if (balance != null)
                        {
                            if (cost <= Convert.ToDouble(balance))
                            {
                                if (GiveBradleyDrop(basePlayer, skinId, apcProfile, 1, "give"))
                                {
                                    RewardPlugin?.Call("Withdraw", basePlayer.userID, cost);
                                    Message(player, "Receive", 1, apcProfile);
                                    return;
                                }
                                else
                                {
                                    Message(player, "FullInventory", apcProfile);
                                    return;
                                }
                            }
                            else
                            {
                                Message(player, "CantAfford", $"{config.purchasing.purchaseUnit}{cost}", $"{config.purchasing.purchaseUnit}{cost - Convert.ToDouble(balance)}");
                                return;
                            }
                        }
                    }
                    break;
                default:
                    {
                        var shortName = config.purchasing.customCurrency[0].ShortName;
                        var displayName = config.purchasing.customCurrency[0].DisplayName;
                        var currencySkin = config.purchasing.customCurrency[0].SkinId;
                        var cost = config.bradley.apcConfig[apcProfile].CostToBuy;
                        int balance = 0;

                        ItemDefinition itemDef = ItemManager.FindItemDefinition(shortName);
                        ItemContainer[] inventories = { basePlayer.inventory.containerMain, basePlayer.inventory.containerWear, basePlayer.inventory.containerBelt };
                        for (int i = 0; i < inventories.Length; i++)
                        {
                            foreach (var item in inventories[i].itemList)
                            {
                                if (item.info.shortname == shortName && item.skin == currencySkin)
                                {
                                    balance += item.amount;
                                    if (cost <= balance)
                                    {
                                        if (GiveBradleyDrop(basePlayer, skinId, apcProfile, 1, "give"))
                                        {
                                            basePlayer.inventory.Take(null, itemDef.itemid, cost);
                                            Message(player, "Receive", 1, apcProfile);
                                        }
                                        else
                                        {
                                            Message(player, "FullInventory", apcProfile);
                                        }
                                        if (item.amount < 1)
                                            item.Remove(0f);
                                        
                                        return;
                                    }
                                }
                            }
                        }
                        Message(player, "CantAfford", $"{cost} {displayName}", $"{cost - balance} {displayName}");
                        return;
                    }
                    break;
            }
        }

        private void CmdGiveDrop(IPlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.Id, permAdmin))
            {
                Message(player, "NotAdmin");
                return;
            }
            else if (args?.Length < 2 || args?.Length > 3)
            {
                if (player.IsServer)
                {
                    Message(player, "SyntaxConsole");
                    return;
                }
                else
                {
                    Message(player, "SyntaxPlayer");
                    return;
                }
            }

            int dropAmount = 1;
            if (args?.Length == 3)
            {
                int amt;
                if (Int32.TryParse(args[2], out amt))
                {
                    dropAmount = amt;
                }
                else
                {
                    if (player.IsServer)
                    {
                        Message(player, "SyntaxConsole");
                        return;
                    }
                    else
                    {
                        Message(player, "SyntaxPlayer");
                        return;
                    }
                }
            }

            var target = FindPlayer(args[1])?.Object as BasePlayer;
            if (target == null)
            {
                Message(player, "PlayerNotFound", args[1]);
                return;
            }
            else if (!target.IsAlive())
            {
                Message(player, "PlayerDead", args[1]);
                return;
            }

            string type = args[0].ToLower();
            ulong skinId = 0;
            string apcProfile = string.Empty;
            foreach (var item in config.bradley.apcConfig.Keys)
            {
                if (type == config.bradley.apcConfig[item].GiveItemCommand.ToLower())
                {
                    skinId = config.bradley.apcConfig[item].SignalSkinID;
                    apcProfile = item;
                    break;
                }
            }

            if (skinId == 0)
            {
                Message(player, "InvalidDrop", type);
                return;
            }

            if (GiveBradleyDrop(target, skinId, apcProfile, dropAmount, "give"))
            {
                Message(target, "Receive", dropAmount, apcProfile);
                Message(player, "PlayerReceive", target.displayName, target.userID, dropAmount, apcProfile);
            }
            else
            {
                Message(player, "FullInventory", apcProfile);
            }
        }

        private void CmdClearCooldown(IPlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.Id, permAdmin))
            {
                Message(player, "NotAdmin");
                return;
            }
            if (args?.Length > 1)
            {
                if (player.IsServer)
                    Message(player, "ClearSyntaxConsole");
                else
                    Message(player, "ClearSyntaxPlayer");
                
                return;
            }
            else if (args?.Length == 0)
            {
                PlayerCooldowns.Clear();
                TierCooldowns.Clear();
                Message(player, "CooldownsCleared");
                return;
            }
            var target = FindPlayer(args[0])?.Object as BasePlayer;
            if (target == null)
            {
                Message(player, "PlayerNotFound", args[0]);
                return;
            }

            var playerId = target.userID;
            if (PlayerCooldowns.ContainsKey(playerId))
            {
                PlayerCooldowns.Remove(playerId);
                Message(player, "PlayerCooldownCleared", target.displayName, playerId);
            }
            else if (TierCooldowns.ContainsKey(playerId))
            {
                TierCooldowns.Remove(playerId);
                Message(player, "PlayerCooldownCleared", target.displayName, playerId);
            }
            else
            {
                Message(player, "PlayerNoCooldown", target.displayName, playerId);
            }
        }

        #endregion Commands

        #region Monos
        
        private class BradleyDropSignal : MonoBehaviour
        {
            public SupplySignal signal;
            public BasePlayer player;
            public CargoPlane plane;
            public Vector3 position;
            public string apcProfile;
            public ulong skinId;

            void Start()
            {
                Invoke(nameof(CustomExplode), config.options.signalFuseLength);
            }

            public void CustomExplode()
            {
                position = signal.transform.position;
                var playerId = player.userID;
                if (DropAborted())
                {
                    signal.Kill();
                    if (player != null || !player.IsAlive())
                    {
                        Instance.NextTick (()=> Instance.GiveBradleyDrop(player, signal.skinID, apcProfile, 1, "refund"));

                        if (config.bradley.playerCooldown > 0f)
                        {
                            if (PlayerCooldowns.ContainsKey(playerId))
                                PlayerCooldowns.Remove(playerId);

                            if (TierCooldowns.ContainsKey(playerId))
                                TierCooldowns.Remove(playerId);
                        }
                    }
                }
                else
                {
                    plane = GameManager.server.CreateEntity(planePrefab, new Vector3(), new Quaternion(), true) as CargoPlane;
                    plane.OwnerID = player.userID;
                    plane.skinID = signal.skinID;
                    plane._name = apcProfile;
                    plane.SendMessage("InitDropPosition", position, SendMessageOptions.DontRequireReceiver);
                    plane.Spawn();

                    CargoPlaneList.Add(plane);

                    plane.gameObject.AddComponent<BradleyDropPlane>();
                    var planeComponent = plane.gameObject.GetComponent<BradleyDropPlane>();
                    planeComponent.plane = plane;
                    planeComponent.player = player;
                    planeComponent.skinId = skinId;
                    planeComponent.apcProfile = apcProfile;

                    float finishUp = config.options.smokeDuration;
                    if (finishUp == null || finishUp < 0) finishUp = 210f;
                    signal.Invoke(new Action(signal.FinishUp), finishUp);
                    signal.SetFlag(BaseEntity.Flags.On, true);
                    signal.SendNetworkUpdateImmediate(false);

                    var gridPos = Instance.PositionToGrid(position);
                    if (gridPos == null)
                        gridPos = "Unknown";
                    
                    if (config.announce.callChat)
                    {
                        var apcName = config.bradley.apcConfig[apcProfile].APCName; 
                        var message = Instance.Lang("BradleyCalled", player.UserIDString, player.displayName, apcName, gridPos);
                        Instance.AnnounceToChat(player, message);
                    }

                    if (config.discord.sendApcCall)
                    {
                        string discordMsg = string.Format(Instance.lang.GetMessage("DiscordCall", Instance), player.displayName, apcProfile, gridPos);
                        Instance.SendToDiscord(discordMsg);
                    }
                }

                DestroyImmediate(this);
            }

            public bool DropAborted()
            {
                if (player == null || !player.IsAlive())
                    return true;

                if (config.bradley.globalLimit > 0)
                {
                    int apcCount = 0;
                    foreach (var netId in BradleyDropData.Keys)
                    {
                        var bradley = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BradleyAPC;
                        if (bradley != null)
                            apcCount++;
                    }

                    foreach (var ent in CargoPlaneList)
                    {
                        if (ent != null || !ent.IsDestroyed)
                            apcCount++;
                    }

                    if (apcCount >= config.bradley.globalLimit)
                    {
                        Instance.Message(player, "GlobalLimit", config.bradley.globalLimit);
                        return true;
                    }
                }
                if (config.bradley.playerLimit > 0)
                {
                    int apcCount = 0;
                    foreach (var key in BradleyDropData.Keys)
                    {
                        var bradley = BaseNetworkable.serverEntities.Find(new NetworkableId(key)) as BradleyAPC;
                        if (bradley != null && BradleyDropData[key].OwnerID == player.userID)
                            apcCount++;
                    }

                    foreach (var ent in CargoPlaneList)
                    {
                        if (ent != null && ent.OwnerID == player.userID)
                            apcCount++;
                    }

                    if (apcCount >= config.bradley.playerLimit)
                    {
                        Instance.Message(player, "PlayerLimit", config.bradley.playerLimit);
                        return true;
                    }
                }

                if ((signal.transform.position.y - TerrainMeta.HeightMap.GetHeight(signal.transform.position)) > 2f)
                {
                    Instance.Message(player, "NotOnGround", apcProfile);
                    return true;
                }

                var anyMonument = TerrainMeta.Path.FindMonumentWithBoundsOverlap(signal.transform.position);
                if (anyMonument != null && anyMonument.ToString().Contains("/monument/"))
                {
                    Instance.Message(player, "InMonument", apcProfile);
                    return true;
                }
                else if (Instance.IsInSafeZone(signal.transform.position))
                {
                    Instance.Message(player, "InSafeZone", apcProfile);
                    return true;
                }
                else if (signal.WaterFactor() > 0.7f)
                {
                    Instance.Message(player, "UnderWater", apcProfile);
                    return true;
                }
                else if (!signal.IsOutside())
                {
                    Instance.Message(player, "Inside", apcProfile);
                    return true;
                }
                else if (Instance.IsInBuildingPriv(signal.transform.position, apcProfile))
                {
                    Instance.Message(player, "BuildingPriv", apcProfile);
                    return true;
                }
                else if (config.options.strictProximity && Instance.IsNearCollider(signal, false))
                {
                    Instance.Message(player, "NearCollider", apcProfile);
                    return true;
                }
                return false;
            }
        }

        private class BradleyDropPlane : MonoBehaviour
        {
            public ulong skinId;
            public BasePlayer player;
            public CargoPlane plane;
            public string apcProfile;

            void Start()
            {
                BradleyDropPosition();
            }

            public void BradleyDropPosition()
            {
                Vector3 newDropPosition = plane.dropPosition;
                newDropPosition = newDropPosition.XZ3D();

                plane.startPos = Vector3Ex.Range(-1f, 1f);
                plane.startPos.y = 0f;
                plane.startPos.Normalize();
                plane.startPos *= (TerrainMeta.Size.x * 2f);
                plane.startPos.y = 150f + config.options.planeHeight;
                plane.endPos = plane.startPos * -1f;
                plane.endPos.y = plane.startPos.y;
                plane.startPos += newDropPosition;
                plane.endPos += newDropPosition;
                plane.secondsToTake = Vector3.Distance(plane.startPos, plane.endPos) / config.options.planeSpeed;

                plane.transform.position = plane.startPos;
                plane.transform.rotation = Quaternion.LookRotation(plane.endPos - plane.startPos);
                plane.dropPosition = newDropPosition;
                plane.SendNetworkUpdateImmediate();
            }
        }

        private class BradleyDrop : MonoBehaviour
        {
            public string apcProfile;
            public BasePlayer owner;
            public BradleyAPC bradley;
            public HotAirBalloon balloon;
            public bool destroyBalloon = false;
            public bool hasLanded = false;
            public Vector3 checkPos;
            public BasePlayer lastAttacker;
            public bool isDespawning = false;
            public ulong bradleyId;

            private int liftBalloon = 0;

            void Start()
            {
                bradleyId = bradley.net.ID.Value;
                bradley.SendNetworkUpdateImmediate(true);
                bradley.UpdateNetworkGroup();
                if (bradley.myRigidBody != null)
                {
                    bradley.myRigidBody.drag = 2f; //config.options.chuteDrag;
                    bradley.myRigidBody.detectCollisions = false;
                }
                Instance.timer.Once(1f, ()=>  AddBalloon());
                InvokeRepeating(nameof(LandingCheck), 1f, 0.25f);
            }

            void FixedUpdate()
            {
                if (hasLanded) return;

                if (balloon != null)
                    balloon.myRigidbody.isKinematic = true;
            }

            void AddBalloon()
            {
                balloon = GameManager.server.CreateEntity(balloonPrefab, bradley.transform.position, Quaternion.Euler(new Vector3(0f, 0f, 0f))) as HotAirBalloon;
                balloon.gameObject.Identity();
                balloon.myRigidbody.isKinematic = true;
                balloon.myRigidbody.detectCollisions = false;
                balloon.inflationLevel = 1f;
                balloon.transform.localPosition = new Vector3(0f, 2f, 0f);
                balloon.SetParent(bradley);
                balloon.Spawn();
            }

            void LandingCheck()
            {
                if (!hasLanded)
                {
                    if (!destroyBalloon)
                    {
                        LayerMask layerMask = LayerMask.GetMask("Water", "Tree", "Clutter", "Player_Server", "Construction", "Terrain", "World", "Deployed");
                        var colliders = Physics.OverlapSphere(transform.position, 2.5f, layerMask);
                        if (colliders.Any())
                        {
                            destroyBalloon = true;
                            if (bradley != null)
                            {
                                if (bradley.myRigidBody != null)
                                {
                                    bradley.myRigidBody.velocity = Vector3.zero;
                                }
                            }
                        }
                    }
                    else
                    {
                        hasLanded = true;
                        destroyBalloon = true;

                        if (config.options.clearLandingZone && config.options.strictProximity)
                            Instance.IsNearCollider(bradley, true);

                        if (BradleyDropData.ContainsKey(bradley.net.ID.Value))
                            BradleyDropData[bradley.net.ID.Value].StartTime = UnityEngine.Time.realtimeSinceStartup;
                    }
                }
                else
                {
                    bradley.myRigidBody.detectCollisions = true;
                    bradley.myRigidBody.isKinematic = false;

                    bradley._maxHealth = config.bradley.apcConfig[apcProfile].Health;
                    bradley.health = bradley._maxHealth;
                    // Now set the search and turret range which was set to 0 while parachuting
                    bradley.searchRange = config.bradley.apcConfig[apcProfile].SearchRange;
                    bradley.viewDistance = config.bradley.apcConfig[apcProfile].MainGunRange;
                    bradley.maxCratesToSpawn = config.bradley.apcConfig[apcProfile].CratesToSpawn;
                    bradley.throttle = config.bradley.apcConfig[apcProfile].ThrottleResponse;
                    bradley.leftThrottle = bradley.throttle;
                    bradley.rightThrottle = bradley.throttle;
                    bradley.ClearPath();
                    bradley.currentPath.Clear();
                    bradley.currentPathIndex = 0;
                    bradley.DoAI = true;
                    bradley.DoSimpleAI();

                    // Now set patrol path
                    var position = bradley.transform.position;
                    if (config.bradley.apcConfig[apcProfile].PatrolPathNodes > 1)
                    {
                        for (int i = 0; i < config.bradley.apcConfig[apcProfile].PatrolPathNodes; i++)
                        {
                            position = position + UnityEngine.Random.onUnitSphere * config.bradley.apcConfig[apcProfile].PatrolRange;
                            position.y = TerrainMeta.HeightMap.GetHeight(position);
                            bradley.currentPath.Add(position);
                        }
                    }

                    CancelInvoke(nameof(LandingCheck));
                    InvokeRepeating(nameof(PositionCheck), 0.1f, 5.0f);

                    if (balloon != null)
                    {
                        balloon.fuelSystem.FillFuel();
                        balloon.SetFlag(BaseEntity.Flags.Reserved6, true, false, true);
                        balloon.myRigidbody.isKinematic = false;
                        //balloon.myRigidbody.detectCollisions = true;
                        balloon.inflationLevel = 1f;
                        balloon.SetParent(null);
                        balloon.sinceLastBlast = Time.realtimeSinceStartup;
                        balloon.transform.position = bradley.transform.position + new Vector3(0, 1f, 0);

                        balloon.SetFlag(BaseEntity.Flags.On, true, false, true);

                        InvokeRepeating(nameof(LiftBalloon), 0f, 0.5f);
                    }
                }
            }

            void LiftBalloon()
            {
                if (liftBalloon >= 7)
                {
                    if (balloon != null)
                    {
                        Effect.server.Run(balloonExplosion, balloon.transform.position);
                        balloon.Kill(BaseNetworkable.DestroyMode.Gib);
                    }
                    CancelInvoke(nameof(LiftBalloon));
                    liftBalloon = 0;
                    return;
                }
                liftBalloon++;
                if (balloon != null)
                {
                    balloon.currentBuoyancy = 10f;
                    balloon.liftAmount = 1000f;
                    balloon.inflationLevel = 1f;
                    balloon.myRigidbody.AddForceAtPosition(((Vector3.up * balloon.liftAmount) * balloon.currentBuoyancy) * 10f, balloon.buoyancyPoint.position, ForceMode.Force);
                }
            }

            void PositionCheck()
            {
                if (bradley != null || !bradley.IsDestroyed)
                {
                    if (config.bradley.apcConfig[apcProfile].KillInSafeZone && Instance.IsInSafeZone(bradley.transform.position))
                    {
                        if (config.bradley.despawnExplosion)
                            Effect.server.Run(bradleyExplosion, bradley.transform.position);
                        
                        bradley.Kill();
                        Instance.Message(owner, "IntoSafeZone", apcProfile);
                        DestroyImmediate(this);
                    }
                    else if (bradley.WaterFactor() > 0.6f)
                    {
                        if (config.bradley.despawnExplosion)
                            Effect.server.Run(bradleyExplosion, bradley.transform.position);
                        
                        bradley.Kill();
                        Instance.Message(owner, "IntoWater", apcProfile);
                        DestroyImmediate(this);
                    }
                }
            }
            
            void OnDestroy()
            {
                CancelInvoke(nameof(LiftBalloon));
                CancelInvoke(nameof(LandingCheck));
                CancelInvoke(nameof(PositionCheck));
                if (balloon != null)
                {
                    Effect.server.Run(balloonExplosion, balloon.transform.position);
                    balloon.Kill(BaseNetworkable.DestroyMode.Gib);
                }
            }
        }
        
        #endregion Monos
        
        #region Temporary Data

        private static Dictionary<ulong, ApcStats> BradleyDropData = new Dictionary<ulong, ApcStats>();
        private static List<CargoPlane> CargoPlaneList = new List<CargoPlane>();
        private static Dictionary<ulong, float> PlayerCooldowns = new Dictionary<ulong, float>();
        private static Dictionary<ulong, Dictionary<string, float>> TierCooldowns = new Dictionary<ulong, Dictionary<string, float>>();
        private static Dictionary<ulong, ulong> LockedCrates = new Dictionary<ulong, ulong>();
        private static List<string> DropTypes = new List<string>();
        private static Dictionary<ulong, string> BradleyProfileCache = new Dictionary<ulong, string>();

        private class ApcStats
        {
            public ulong OwnerID;
            public float StartTime = 0f;
            public BasePlayer LastAttacker;
            public Dictionary<ulong, AttackersStats> Attackers = new Dictionary<ulong, AttackersStats>();
            public int WarningLevel = 0;
        }

        private class AttackersStats
        {
            public string Name;
            public float DamageDealt = 0f;
        }

        #endregion Temporary Data

        #region Config
        
        private class APCData
        {
            [JsonProperty(PropertyName = "Bradley display name")]
            public string APCName { get; set; }
            [JsonProperty(PropertyName = "Skin ID of the custom Supply Signal")]
            public ulong SignalSkinID { get; set; }
            [JsonProperty(PropertyName = "Profile shortname (for use in permission and give command)")]
            public string GiveItemCommand { get; set; }
            [JsonProperty(PropertyName = "Enable purchasing via the buy command")]
            public bool UseBuyCommand { get; set; }
            [JsonProperty(PropertyName = "Cost to purchase (using buy command)")]
            public int CostToBuy { get; set; }
            [JsonProperty(PropertyName = "Starting health")]
            public float Health { get; set; }
            [JsonProperty(PropertyName = "Prevent damage while parachuting")]
            public bool ChuteProtected { get; set; }
            [JsonProperty(PropertyName = "Range of main gun")]
            public float MainGunRange { get; set; }
            [JsonProperty(PropertyName = "Gun Damage scale (1.0 = default, 2.0 = 2x, etc)")]
            public float GunDamage { get; set; }
            [JsonProperty(PropertyName = "Gun accuracy % (1 to 100)")]
            public float GunAccuracy { get; set; }
            [JsonProperty(PropertyName = "Main cannon damage scale (1.0 = default, 2.0 = 2x, etc)")]
            public float ShellDamageScale { get; set; }
            [JsonProperty(PropertyName = "Search range")]
            public float SearchRange { get; set; }
            [JsonProperty(PropertyName = "Patrol radius")]
            public float PatrolRange { get; set; }
            [JsonProperty(PropertyName = "Number of patrol points")]
            public int PatrolPathNodes { get; set; }
            [JsonProperty(PropertyName = "Throttle response (1.0 = default)")]
            public float ThrottleResponse { get; set; }
            [JsonProperty(PropertyName = "Number of crates to spawn")]
            public int CratesToSpawn { get; set; }
            [JsonProperty(PropertyName = "Number of locked hackable crates to spawn")]
            public int LockedCratesToSpawn { get; set; }
            [JsonProperty(PropertyName = "Hack time for locked crate (seconds)")]
            public float HackSeconds { get; set; }
            [JsonProperty(PropertyName = "Locked crate despawn time (seconds)")]
            public float LockedCrateDespawn { get; set; }
            [JsonProperty(PropertyName = "Kill if APC goes in SafeZone")]
            public bool KillInSafeZone { get; set; }
            [JsonProperty(PropertyName = "Despawn timer")]
            public float DespawnTime { get; set; }
            [JsonProperty(PropertyName = "Attack owner")]
            public bool AttackOwner { get; set; }
            [JsonProperty(PropertyName = "Target sleeping players")]
            public bool TargetSleepers { get; set; }
            [JsonProperty(PropertyName = "Only owner can damage (and team if enabled) ")]
            public bool OwnerDamage { get; set; }
            [JsonProperty(PropertyName = "Allow Bradley to target other players")]
            public bool TargetOtherPlayers { get; set; }
            [JsonProperty(PropertyName = "Block damage to calling players bases")]
            public bool BlockOwnerDamage { get; set; }
            [JsonProperty(PropertyName = "Block damage to other players bases")]
            public bool BlockOtherDamage { get; set; }
            [JsonProperty(PropertyName = "Block damage to other players")]
            public bool BlockPlayerDamage { get; set; }
            [JsonProperty(PropertyName = "Disable Bradley gibs")]
            public bool KillGibs { get; set; }
            [JsonProperty(PropertyName = "Gibs too hot to harvest time (Seconds)")]
            public float GibsHotTime { get; set; }
            [JsonProperty(PropertyName = "Health of gibs (more health = more resources)")]
            public float GibsHealth { get; set; }
            [JsonProperty(PropertyName = "Lock mining gibs to owner")]
            public bool ProtectGibs { get; set; }
            [JsonProperty(PropertyName = "Unlock mining gibs to others after time in seconds (0 = Never)")]
            public float UnlockGibs { get; set; }
            [JsonProperty(PropertyName = "Disable fire on crates")]
            public bool DisableFire { get; set; }
            [JsonProperty(PropertyName = "Crate fire duration (seconds)")]
            public float FireDuration { get; set; }
            [JsonProperty(PropertyName = "Lock looting crates to owner")]
            public bool ProtectCrates { get; set; }
            [JsonProperty(PropertyName = "Unlock looting crates to others after time in seconds (0 = Never)")]
            public float UnlockCrates { get; set; }
            [JsonProperty(PropertyName = "Reward points issued when destroyed (if enabled)")]
            public double RewardPoints { get; set; }
            [JsonProperty(PropertyName = "Scrap amount issued when destroyed (if enabled)")]
            public int ScrapReward { get; set; }
            [JsonProperty(PropertyName = "Damage Threshold (Min damage player needs to contribute to get rewards)")]
            public float DamageThreshold { get; set; }
            [JsonProperty(PropertyName = "BotReSpawn profile to spawn at Bradley kill site (leave blank for not using)")]
            public string BotReSpawnProfile { get; set; }

            [JsonProperty(PropertyName = "Loot Options")]
            public LootOptions Loot { get; set; }

            public class LootOptions
            {
                [JsonProperty(PropertyName = "Use custom loot table")]
                public bool UseCustomLoot { get; set; }
                [JsonProperty(PropertyName = "Minimum number loot items in crate (0 - 12)")]
                public int MinCrateItems { get; set; }
                [JsonProperty(PropertyName = "Maximum number loot items in crate (0 - 12)")]
                public int MaxCrateItems { get; set; }
                [JsonProperty(PropertyName = "Allow duplication of loot items")]
                public bool AllowDupes { get; set; }
                [JsonProperty(PropertyName = "Maximum number of BPs in each crate")]
                public int MaxBP { get; set; }
                [JsonProperty(PropertyName = "Custom loot table")]
                public List<LootItem> LootTable { get; set; }
            }

            [JsonProperty(PropertyName = "Extra Loot Options")]
            public ExtraLootOptions ExtraLoot { get; set; }

            public class ExtraLootOptions
            {
                [JsonProperty(PropertyName = "Use extra loot table (NOTE: Total of crate loot + extra items cannot exceed 12)")]
                public bool UseExtraLoot { get; set; }
                [JsonProperty(PropertyName = "Minimum number extra items to add to crate")]
                public int MinExtraItems { get; set; }
                [JsonProperty(PropertyName = "Maximum number extra items to add to crate")]
                public int MaxExtraItems { get; set; }
                [JsonProperty(PropertyName = "Allow duplication of extra items")]
                public bool AllowDupes { get; set; }
                [JsonProperty(PropertyName = "Maximum number of BPs in each crate")]
                public int MaxBP { get; set; }
                [JsonProperty(PropertyName = "Extra loot table")]
                public List<LootItem> LootTable { get; set; }
            }

            [JsonProperty(PropertyName = "Locked Crate Loot Options")]
            public LockedCrateLootOptions LockedCrateLoot { get; set; }

            public class LockedCrateLootOptions
            {
                [JsonProperty(PropertyName = "Use locked crate loot table (NOTE: Total items cannot exceed 36)")]
                public bool UseLockedCrateLoot { get; set; }
                [JsonProperty(PropertyName = "Minimum number items to add to locked crate")]
                public int MinLockedCrateItems { get; set; }
                [JsonProperty(PropertyName = "Maximum number items to add to locked crate")]
                public int MaxLockedCrateItems { get; set; }
                [JsonProperty(PropertyName = "Allow duplication of locked crate items")]
                public bool AllowDupes { get; set; }
                [JsonProperty(PropertyName = "Maximum number of BPs in crate")]
                public int MaxBP { get; set; }
                [JsonProperty(PropertyName = "Locked crate loot table")]
                public List<LootItem> LootTable { get; set; }
            }
        }

        public class CurrencyItem
        {
            [JsonProperty(PropertyName = "ShortName")]
            public string ShortName { get; set; }
            [JsonProperty(PropertyName = "SkinID")]
            public ulong SkinId { get; set; }
            [JsonProperty(PropertyName = "Display Name")]
            public string DisplayName { get; set; }
        }

        public class LootItem
        {
            [JsonProperty(PropertyName = "ShortName")]
            public string ShortName { get; set; }
            [JsonProperty(PropertyName = "Chance (0 - 100)")]
            public float Chance { get; set; }
            [JsonProperty(PropertyName = "Min amount")]
            public int AmountMin { get; set; }
            [JsonProperty(PropertyName = "Max Amount")]
            public int AmountMax { get; set; }
            [JsonProperty(PropertyName = "SkinID")]
            public ulong SkinId { get; set; }
            [JsonProperty(PropertyName = "Display Name")]
            public string DisplayName { get; set; }
            [JsonProperty(PropertyName = "Blueprint Chance Instead of Item, 0 = disabled. (0 - 100)")]
            public float BlueprintChance { get; set; }
        }

        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "General Options")]
            public Options options { get; set; }
            [JsonProperty(PropertyName = "Announce Options")]
            public Announce announce { get; set; }
            [JsonProperty(PropertyName = "Discord Options")]
            public Discord discord { get; set; }
            [JsonProperty(PropertyName = "Reward Options")]
            public Rewards rewards { get; set; }
            [JsonProperty(PropertyName = "Purchasing Options")]
            public Purchasing purchasing { get; set; }
            [JsonProperty(PropertyName = "Bradley APC Options")]
            public Bradley bradley { get; set; }
            
            public class Options
            {
                [JsonProperty(PropertyName = "Use Friends")]
                public bool useFriends { get; set; }
                [JsonProperty(PropertyName = "Use Clans")]
                public bool useClans { get; set; }
                [JsonProperty(PropertyName = "Use Teams")]
                public bool useTeams { get; set; }
                [JsonProperty(PropertyName = "Allow Dynamic PVP to Create PVP Zones")]
                public bool useDynamicPVP { get; set; }
                [JsonProperty(PropertyName = "Chat Prefix")]
                public string chatPrefix { get; set; }
                [JsonProperty(PropertyName = "Use Chat Prefix")]
                public bool usePrefix { get; set; }
                [JsonProperty(PropertyName = "Custom Chat Icon (Default = 0)")]
                public ulong chatIcon { get; set; }
                [JsonProperty(PropertyName = "Supply Signal Fuse Length (Rust Default = 3.5)")]
                public float signalFuseLength { get; set; }
                [JsonProperty(PropertyName = "Supply Signal Smoke Duration (Rust Default = 210)")]
                public float smokeDuration { get; set; }
                [JsonProperty(PropertyName = "Cargo Plane Speed (Rust Default = 35)")]
                public float planeSpeed { get; set; }
                [JsonProperty(PropertyName = "Cargo Plane Height Above The Heighest Point On The Map")]
                public float planeHeight { get; set; }
                [JsonProperty(PropertyName = "Parachute Drag (Lower = Faster. eg: 0.6)")]
                public float chuteDrag { get; set; }
                [JsonProperty(PropertyName = "Min Distance From Building Privilege To Use Signals (Important: Greater or Equal To Proximity Check Radius)")]
                public float buildPrivRadius { get; set; }
                [JsonProperty(PropertyName = "Strict Proximity Check (Checks for objects close to signal, prevents APC landing on objects)")]
                public bool strictProximity { get; set; }
                [JsonProperty(PropertyName = "Strict Proximity Check Radius")]
                public float proximityRadius { get; set; }
                [JsonProperty(PropertyName = "Remove Entities In Landing Zone Radius (Requires Strict Proximity Check Enabled)")]
                public bool clearLandingZone { get; set; }
                [JsonProperty(PropertyName = "Disable vanilla Bradley APC at Launch Site")]
                public bool noVanillaApc { get; set; }
                [JsonProperty(PropertyName = "Use this plugin to control stacking/combining Bradley Drop signal items")]
                public bool useStacking { get; set; }
                [JsonProperty(PropertyName = "Command to Show Details of Players Own Active Bradleys (Admin Perm Allows to See ALL Active Bradleys)")]
                public string reportCommand { get; set; }
            }

            public class Announce
            {
                [JsonProperty(PropertyName = "Announce When Player Calls a Bradley Drop")]
                public bool callChat { get; set; }
                [JsonProperty(PropertyName = "Announce Bradley Kill In Chat")]
                public bool killChat { get; set; }
                [JsonProperty(PropertyName = "Announce Damage Report In Chat")]
                public bool reportChat { get; set; }
                [JsonProperty(PropertyName = "Max Number Players Displayed in Damage Report")]
                public int maxReported { get; set; }
                [JsonProperty(PropertyName = "Announcements Also Go To Global Chat (false = Player/Team Only)")]
                public bool announceGlobal { get; set; }
            }

            public class Discord
            {
                [JsonProperty(PropertyName = "Discord WebHook URL")]
                public string webHookUrl { get; set; }
                [JsonProperty(PropertyName = "Announce to Discord when Bradley is called")]
                public bool sendApcCall { get; set; }
                [JsonProperty(PropertyName = "Announce to Discord when Bradley is killed")]
                public bool sendApcKill { get; set; }
                [JsonProperty(PropertyName = "Announce to Discord when Bradley de-spawns")]
                public bool sendApcDespawn { get; set; }
            }

            public class Rewards
            {
                [JsonProperty(PropertyName = "Rewards Plugin (ServerRewards | Economics)")]
                public string rewardPlugin { get; set; }
                [JsonProperty(PropertyName = "Currency Unit Displayed e.g: RP | $")]
                public string rewardUnit { get; set; }
                [JsonProperty(PropertyName = "Enable Rewards")]
                public bool enableRewards { get; set; }
                [JsonProperty(PropertyName = "Share Reward Between Players Above Damage Threshold")]
                public bool shareRewards { get; set; }
                [JsonProperty(PropertyName = "Enable Scrap Reward")]
                public bool enableScrap { get; set; }
                [JsonProperty(PropertyName = "Share Scrap Between Players Above Damage Threshold")]
                public bool shareScrap { get; set; }
                [JsonProperty(PropertyName = "Rewards multipliers by permission")]
                public Hash<string, float> rewardMultipliers { get; set; }

                [JsonIgnore]
                public Permission permission;
                public void RegisterPermissions(Permission permission, BradleyDrops plugin)
                {
                    this.permission = permission;
                    foreach (string key in rewardMultipliers.Keys)
                    {
                        if (!permission.PermissionExists(key, plugin))
                        {
                            permission.RegisterPermission(key, plugin);
                        }
                    }
                }
            }

            public class Purchasing
            {
                [JsonProperty(PropertyName = "Player Buy Command (Chat or F1 Console)")]
                public string buyCommand { get; set; }
                [JsonProperty(PropertyName = "Purchasing Currency (ServerRewards|Economics|Custom)")]
                public string defaultCurrency { get; set; }
                [JsonProperty(PropertyName = "Currency Unit Displayed e.g: RP | $ (Not Used for Custom Currency)")]
                public string purchaseUnit { get; set; }
                [JsonProperty(PropertyName = "Custom Currency")]
                public List<CurrencyItem> customCurrency { get; set; }
            }

            public class Bradley
            {
                [JsonProperty(PropertyName = "Player Give Up and Despawn Command (Despawns All of That Players Bradleys, NO Refund Given)")]
                public string despawnCommand { get; set; }
                [JsonProperty(PropertyName = "Team Can Deswpan Bradleys Using the Command (Requires Use Friends/Clans/Teams option)")]
                public bool canTeamDespawn { get; set; }
                [JsonProperty(PropertyName = "Global Bradley Limit (0 = No Limit)")]
                public int globalLimit { get; set; }
                [JsonProperty(PropertyName = "Player Bradley Limit (0 = No Limit)")]
                public int playerLimit { get; set; }
                [JsonProperty(PropertyName = "Max Distance Bradley Can Be Damaged By Any Player (0 = Disabled)")]
                public float maxHitDistance { get; set; }
                [JsonProperty(PropertyName = "Use Explosion Effect When Bradley Despawns")]
                public bool despawnExplosion { get; set; }
                [JsonProperty(PropertyName = "Despawn if attacking player is building blocked, while 'Block damage to other players bases' is true")]
                public bool DespawnWarning { get; set; }
                [JsonProperty(PropertyName = "Despawn warning threshold (Number of warnings allowed before despawning)")]
                public int WarningThreshold { get; set; }
                [JsonProperty(PropertyName = "Use NoEscape")]
                public bool useNoEscape { get; set; }
                [JsonProperty(PropertyName = "Player Cooldown (seconds) Between Bradley Drop Calls (0 = no cooldown)")]
                public float playerCooldown { get; set; }
                [JsonProperty(PropertyName = "Player Cooldowns Apply to Each Tier Seperately")]
                public bool tierCooldowns { get; set; }
                [JsonProperty(PropertyName = "Cooldown Applies to Clan/Team/Friends (Requires Use Friends/Use Clan/Use Teams)")]
                public bool teamCooldown { get; set; }
                [JsonProperty(PropertyName = "Allow Players to Damage Bradleys With Remote Auto Turrets")]
                public bool allowTurretDamage { get; set; }
                [JsonProperty(PropertyName = "VIP/Custom Cooldowns")]
                public Hash<string, float> vipCooldowns { get; set; }

                [JsonIgnore]
                public Permission permission;
                public void RegisterPermissions(Permission permission, BradleyDrops plugin)
                {
                    this.permission = permission;
                    foreach (string key in vipCooldowns.Keys)
                    {
                        if (!permission.PermissionExists(key, plugin))
                        {
                            permission.RegisterPermission(key, plugin);
                        }
                    }
                }

                [JsonProperty(PropertyName = "Profiles")]
                public Dictionary<string, APCData> apcConfig { get; set; }
            }
            public VersionNumber Version { get; set; }
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                options = new ConfigData.Options
                {
                    useFriends = false,
                    useClans = false,
                    useTeams = false,
                    useDynamicPVP = false,
                    chatPrefix = "<color=orange>[Bradley Drops]</color>",
                    usePrefix = true,
                    chatIcon = 0,
                    signalFuseLength = 3.5f,
                    smokeDuration = 210f,
                    planeSpeed = 50.0f,
                    planeHeight = 50.0f,
                    chuteDrag = 0.5f,
                    buildPrivRadius = 20f,
                    strictProximity = true,
                    proximityRadius = 20f,
                    clearLandingZone = false,
                    noVanillaApc = false,
                    useStacking = true,
                    reportCommand = "bdreport"
                },
                announce = new ConfigData.Announce
                {
                    killChat = false,
                    callChat = false,
                    reportChat = true,
                    maxReported = 5,
                    announceGlobal = true
                },
                discord = new ConfigData.Discord
                {
                    webHookUrl = defaultWebhookUrl,
                    sendApcCall = false,
                    sendApcKill = false,
                    sendApcDespawn = false
                },
                rewards = new ConfigData.Rewards
                {
                    rewardPlugin = "ServerRewards",
                    rewardUnit = "RP",
                    enableRewards = false,
                    shareRewards = false,
                    enableScrap = false,
                    shareScrap = false,
                    rewardMultipliers = new Hash<string, float>
                    {
                        ["bradleydrops.examplevip1"] = 1.25f,
                        ["bradleydrops.examplevip2"] = 1.50f,
                        ["bradleydrops.examplevip3"] = 1.75f
                    }
                },
                purchasing = new ConfigData.Purchasing
                {
                    buyCommand = "bdbuy",
                    defaultCurrency = "ServerRewards",
                    purchaseUnit = "RP",
                    customCurrency = new List<CurrencyItem>
                    {
                        new CurrencyItem { ShortName = "scrap", SkinId = 0, DisplayName = "Scrap" }
                    },
                },
                bradley = new ConfigData.Bradley
                {
                    despawnCommand = "bddespawn",
                    canTeamDespawn = false,
                    globalLimit = 2,
                    playerLimit = 1,
                    maxHitDistance = 1000f,
                    despawnExplosion = true,
                    DespawnWarning = false,
                    WarningThreshold = 25,
                    useNoEscape = false,
                    playerCooldown = 3600f,
                    tierCooldowns = true,
                    teamCooldown = true,
                    allowTurretDamage = true,
                    vipCooldowns = new Hash<string, float>
                    {
                        ["bradleydrops.examplevip1"] = 3000f,
                        ["bradleydrops.examplevip2"] = 2400f,
                        ["bradleydrops.examplevip3"] = 1800f
                    },
                    apcConfig = new Dictionary<string, APCData>
                    {
                        [easyDrop] = new APCData
                        {
                            APCName = easyDrop,
                            SignalSkinID = easySkinID,
                            GiveItemCommand = "easy",
                            UseBuyCommand = true,
                            CostToBuy = 500,
                            Health = 1000f,
                            ChuteProtected = true,
                            MainGunRange = 60f,
                            GunDamage = 1f,
                            GunAccuracy = 40f,
                            ShellDamageScale = 1f,
                            SearchRange = 60f,
                            PatrolRange = 20f,
                            PatrolPathNodes = 4,
                            ThrottleResponse = 1f,
                            CratesToSpawn = 3,
                            LockedCratesToSpawn = 0,
                            HackSeconds = 900f,
                            LockedCrateDespawn = 7200f,
                            KillInSafeZone = true,
                            DespawnTime = 1800f,
                            AttackOwner = true,
                            TargetSleepers = false,
                            OwnerDamage = false,
                            TargetOtherPlayers = true,
                            BlockOwnerDamage = false,
                            BlockOtherDamage = false,
                            BlockPlayerDamage = false,
                            KillGibs = false,
                            GibsHotTime = 600f,
                            GibsHealth = 500f,
                            ProtectGibs = false,
                            UnlockGibs = 300f,
                            DisableFire = false,
                            FireDuration = 300f,
                            ProtectCrates = false,
                            UnlockCrates = 300f,
                            RewardPoints = 1000,
                            ScrapReward = 1000,
                            DamageThreshold = 100f,
                            BotReSpawnProfile = string.Empty,

                            Loot = new APCData.LootOptions
                            {
                                UseCustomLoot = false,
                                MinCrateItems = 2,
                                MaxCrateItems = 6,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 1", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 2", BlueprintChance = 0f }
                                }
                            },
                            ExtraLoot = new APCData.ExtraLootOptions
                            {
                                UseExtraLoot = false,
                                MinExtraItems = 1,
                                MaxExtraItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 1", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 2", BlueprintChance = 0f }
                                }
                            },
                            LockedCrateLoot = new APCData.LockedCrateLootOptions
                            {
                                UseLockedCrateLoot = false,
                                MinLockedCrateItems = 1,
                                MaxLockedCrateItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 1", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 2", BlueprintChance = 0f }
                                }
                            }
                        },
                        [medDrop] = new APCData
                        {
                            APCName = medDrop,
                            SignalSkinID = medSkinID,
                            GiveItemCommand = "medium",
                            UseBuyCommand = true,
                            CostToBuy = 1000,
                            Health = 2000f,
                            ChuteProtected = true,
                            MainGunRange = 80f,
                            GunDamage = 1f,
                            GunAccuracy = 60f,
                            ShellDamageScale = 1f,
                            SearchRange = 80f,
                            PatrolRange = 20f,
                            PatrolPathNodes = 3,
                            ThrottleResponse = 1f,
                            CratesToSpawn = 6,
                            LockedCratesToSpawn = 0,
                            HackSeconds = 900f,
                            LockedCrateDespawn = 7200f,
                            KillInSafeZone = true,
                            DespawnTime = 1800f,
                            AttackOwner = true,
                            TargetSleepers = false,
                            OwnerDamage = false,
                            TargetOtherPlayers = true,
                            BlockOwnerDamage = false,
                            BlockOtherDamage = false,
                            BlockPlayerDamage = false,
                            KillGibs = false,
                            GibsHotTime = 600f,
                            GibsHealth = 500f,
                            ProtectGibs = false,
                            UnlockGibs = 300f,
                            DisableFire = false,
                            FireDuration = 300f,
                            ProtectCrates = false,
                            UnlockCrates = 300f,
                            RewardPoints = 2000,
                            ScrapReward = 2000,
                            DamageThreshold = 150f,
                            BotReSpawnProfile = string.Empty,

                            Loot = new APCData.LootOptions
                            {
                                UseCustomLoot = false,
                                MinCrateItems = 4,
                                MaxCrateItems = 8,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 1", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 2", BlueprintChance = 0f }
                                }
                            },
                            ExtraLoot = new APCData.ExtraLootOptions
                            {
                                UseExtraLoot = false,
                                MinExtraItems = 1,
                                MaxExtraItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 1", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 2", BlueprintChance = 0f }
                                }
                            },
                            LockedCrateLoot = new APCData.LockedCrateLootOptions
                            {
                                UseLockedCrateLoot = false,
                                MinLockedCrateItems = 1,
                                MaxLockedCrateItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 1", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 2", BlueprintChance = 0f }
                                }
                            }
                        },
                        [hardDrop] = new APCData
                        {
                            APCName = hardDrop,
                            SignalSkinID = hardSkinID,
                            GiveItemCommand = "hard",
                            UseBuyCommand = true,
                            CostToBuy = 2000,
                            Health = 4000f,
                            ChuteProtected = true,
                            MainGunRange = 100f,
                            GunDamage = 1f,
                            GunAccuracy = 80f,
                            ShellDamageScale = 1f,
                            SearchRange = 100f,
                            PatrolRange = 25f,
                            PatrolPathNodes = 4,
                            ThrottleResponse = 1f,
                            CratesToSpawn = 9,
                            LockedCratesToSpawn = 0,
                            HackSeconds = 900f,
                            LockedCrateDespawn = 7200f,
                            KillInSafeZone = true,
                            DespawnTime = 1800f,
                            AttackOwner = true,
                            TargetSleepers = false,
                            OwnerDamage = false,
                            TargetOtherPlayers = true,
                            BlockOwnerDamage = false,
                            BlockOtherDamage = false,
                            BlockPlayerDamage = false,
                            KillGibs = false,
                            GibsHotTime = 600f,
                            GibsHealth = 500f,
                            ProtectGibs = false,
                            UnlockGibs = 300f,
                            DisableFire = false,
                            FireDuration = 300f,
                            ProtectCrates = false,
                            UnlockCrates = 300f,
                            RewardPoints = 4000,
                            ScrapReward = 4000,
                            DamageThreshold = 200f,
                            BotReSpawnProfile = string.Empty,

                            Loot = new APCData.LootOptions
                            {
                                UseCustomLoot = false,
                                MinCrateItems = 6,
                                MaxCrateItems = 10,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 1", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 2", BlueprintChance = 0f }
                                }
                            },
                            ExtraLoot = new APCData.ExtraLootOptions
                            {
                                UseExtraLoot = false,
                                MinExtraItems = 1,
                                MaxExtraItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 1", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 2", BlueprintChance = 0f }
                                }
                            },
                            LockedCrateLoot = new APCData.LockedCrateLootOptions
                            {
                                UseLockedCrateLoot = false,
                                MinLockedCrateItems = 1,
                                MaxLockedCrateItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 1", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 2", BlueprintChance = 0f }
                                }
                            }
                        },
                        [eliteDrop] = new APCData
                        {
                            APCName = eliteDrop,
                            SignalSkinID = eliteSkinID,
                            GiveItemCommand = "elite",
                            UseBuyCommand = true,
                            CostToBuy = 4000,
                            Health = 8000f,
                            ChuteProtected = true,
                            MainGunRange = 150f,
                            GunDamage = 2.0f,
                            GunAccuracy = 100f,
                            ShellDamageScale = 1f,
                            SearchRange = 150f,
                            PatrolRange = 25f,
                            PatrolPathNodes = 3,
                            ThrottleResponse = 1f,
                            CratesToSpawn = 18,
                            LockedCratesToSpawn = 0,
                            HackSeconds = 900f,
                            LockedCrateDespawn = 7200f,
                            KillInSafeZone = true,
                            DespawnTime = 1800f,
                            AttackOwner = true,
                            TargetSleepers = false,
                            OwnerDamage = false,
                            TargetOtherPlayers = true,
                            BlockOwnerDamage = false,
                            BlockOtherDamage = false,
                            BlockPlayerDamage = false,
                            KillGibs = false,
                            GibsHotTime = 600f,
                            GibsHealth = 500f,
                            ProtectGibs = false,
                            UnlockGibs = 300f,
                            DisableFire = false,
                            FireDuration = 300f,
                            ProtectCrates = false,
                            UnlockCrates = 300f,
                            RewardPoints = 8000,
                            ScrapReward = 8000,
                            DamageThreshold = 250f,
                            BotReSpawnProfile = string.Empty,

                            Loot = new APCData.LootOptions
                            {
                                UseCustomLoot = false,
                                MinCrateItems = 8,
                                MaxCrateItems = 12,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 1", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 2", BlueprintChance = 0f }
                                }
                            },
                            ExtraLoot = new APCData.ExtraLootOptions
                            {
                                UseExtraLoot = false,
                                MinExtraItems = 1,
                                MaxExtraItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 1", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 2", BlueprintChance = 0f }
                                }
                            },
                            LockedCrateLoot = new APCData.LockedCrateLootOptions
                            {
                                UseLockedCrateLoot = false,
                                MinLockedCrateItems = 1,
                                MaxLockedCrateItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 1", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display Name 2", BlueprintChance = 0f }
                                }
                            }
                        }
                    }
                },
                Version = Version
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
                else if (config.Version < Version)
                {
                    UpdateConfigValues();
                }
            }
            catch (Exception ex)
            {
                if (ex is JsonSerializationException || ex is NullReferenceException || ex is JsonReaderException || ex is KeyNotFoundException)
                {
                    Puts($"Exception Type: {ex.GetType()}");
                    Puts($"INFO: {ex}");
                    return;
                }
                throw;
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Configuration file missing or corrupt, creating default config file.");
            config = GetDefaultConfig();
            SaveConfig();
        }
        
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private void UpdateConfigValues()
        {
            ConfigData defaultConfig = GetDefaultConfig();

            Puts("Config update detected! Updating config file...");

            if (config.Version < new VersionNumber(1, 0, 19))
            {
                foreach (var key in config.bradley.apcConfig.Keys)
                {
                    config.bradley.apcConfig[key].DamageThreshold = 100;
                }
            }
            if (config.Version < new VersionNumber(1, 0, 20))
            {
                config.purchasing = new ConfigData.Purchasing();
                config.purchasing.defaultCurrency = defaultConfig.purchasing.defaultCurrency;
                config.purchasing.customCurrency = defaultConfig.purchasing.customCurrency;
                config.purchasing.purchaseUnit = defaultConfig.purchasing.purchaseUnit;
                config.rewards.rewardUnit = defaultConfig.rewards.rewardUnit;
                config.bradley.globalLimit = defaultConfig.bradley.globalLimit;

                foreach (var key in config.bradley.apcConfig.Keys)
                {
                    config.bradley.apcConfig[key].CostToBuy = 1000;
                    config.bradley.apcConfig[key].ChuteProtected = true;
                    config.bradley.apcConfig[key].Loot = new APCData.LootOptions();
                    config.bradley.apcConfig[key].Loot.UseCustomLoot = false;
                    config.bradley.apcConfig[key].Loot.MinCrateItems = 2;
                    config.bradley.apcConfig[key].Loot.MaxCrateItems = 6;
                    config.bradley.apcConfig[key].Loot.AllowDupes = false;
                    config.bradley.apcConfig[key].Loot.LootTable = new List<LootItem>();
                }
            }
            if (config.Version < new VersionNumber(1, 0, 22))
            {
                config.bradley.maxHitDistance = defaultConfig.bradley.maxHitDistance;
                foreach (var key in config.bradley.apcConfig.Keys)
                {
                    config.bradley.apcConfig[key].ExtraLoot = new APCData.ExtraLootOptions();
                    config.bradley.apcConfig[key].ExtraLoot.UseExtraLoot = false;
                    config.bradley.apcConfig[key].ExtraLoot.MinExtraItems = 1;
                    config.bradley.apcConfig[key].ExtraLoot.MaxExtraItems = 3;
                    config.bradley.apcConfig[key].ExtraLoot.AllowDupes = false;
                    config.bradley.apcConfig[key].ExtraLoot.LootTable = new List<LootItem>();
                }
            }
            if (config.Version < new VersionNumber(1, 0, 25))
            {
                config.bradley.useNoEscape = defaultConfig.bradley.useNoEscape;
                foreach (var key in config.bradley.apcConfig.Keys)
                {
                    config.bradley.apcConfig[key].ShellDamageScale = 1f;
                    config.bradley.apcConfig[key].BotReSpawnProfile = string.Empty;
                    config.bradley.apcConfig[key].TargetOtherPlayers = true;
                    config.bradley.apcConfig[key].BlockOwnerDamage = false;
                    config.bradley.apcConfig[key].BlockOtherDamage = false;
                    config.bradley.apcConfig[key].BlockPlayerDamage = false;
                }
            }
            if (config.Version < new VersionNumber(1, 0, 26))
            {
                config.bradley.teamCooldown = defaultConfig.bradley.teamCooldown;
                config.bradley.DespawnWarning = defaultConfig.bradley.DespawnWarning;
                config.bradley.WarningThreshold = defaultConfig.bradley.WarningThreshold;
            }
            if (config.Version < new VersionNumber(1, 0, 27))
            {
                config.discord = new ConfigData.Discord();
                config.discord.webHookUrl = defaultConfig.discord.webHookUrl;
                config.discord.sendApcCall = defaultConfig.discord.sendApcCall;
                config.discord.sendApcKill = defaultConfig.discord.sendApcKill;
                config.discord.sendApcDespawn = defaultConfig.discord.sendApcDespawn;
                config.purchasing.buyCommand = defaultConfig.purchasing.buyCommand;
                config.announce.reportChat = defaultConfig.announce.reportChat;
                config.announce.maxReported = defaultConfig.announce.maxReported;
                config.options.noVanillaApc = defaultConfig.options.noVanillaApc;
                config.options.useStacking = defaultConfig.options.useStacking;
            }
            if (config.Version < new VersionNumber(1, 0, 30))
            {
                config.bradley.despawnCommand = defaultConfig.bradley.despawnCommand;
                config.bradley.canTeamDespawn = defaultConfig.bradley.canTeamDespawn;
                foreach (var key in config.bradley.apcConfig.Keys)
                {
                    config.bradley.apcConfig[key].LockedCratesToSpawn = 0;
                    config.bradley.apcConfig[key].HackSeconds = 900f;
                    config.bradley.apcConfig[key].LockedCrateDespawn = 7200f;
                }
            }
            if (config.Version < new VersionNumber(1, 0, 31))
            {
                foreach (var key in config.bradley.apcConfig.Keys)
                {
                    config.bradley.apcConfig[key].LockedCrateLoot = new APCData.LockedCrateLootOptions();
                    config.bradley.apcConfig[key].LockedCrateLoot.UseLockedCrateLoot = false;
                    config.bradley.apcConfig[key].LockedCrateLoot.MinLockedCrateItems = 6;
                    config.bradley.apcConfig[key].LockedCrateLoot.MaxLockedCrateItems = 18;
                    config.bradley.apcConfig[key].LockedCrateLoot.AllowDupes = false;
                    config.bradley.apcConfig[key].LockedCrateLoot.LootTable = new List<LootItem>();
                }
            }
            if (config.Version < new VersionNumber(1, 0, 32))
            {
                // Adding example loot items if there are none, since I stupidly forgot to add them previously (yes, I'm an idiot).
                foreach (var key in config.bradley.apcConfig.Keys)
                {
                    if (config.bradley.apcConfig[key].Loot.LootTable.Count == 0)
                    {
                        config.bradley.apcConfig[key].Loot.LootTable.Add(new LootItem
                        {
                            ShortName = "example.shortname1",
                            Chance = 50, AmountMin = 1,
                            AmountMax = 2,
                            SkinId = 1234567890,
                            DisplayName = "Example Display Name 1"
                        });
                        config.bradley.apcConfig[key].Loot.LootTable.Add(new LootItem
                        {
                            ShortName = "example.shortname2",
                            Chance = 50, AmountMin = 1,
                            AmountMax = 2,
                            SkinId = 1234567890,
                            DisplayName = "Example Display Name 2"
                        });
                    }
                    if (config.bradley.apcConfig[key].ExtraLoot.LootTable.Count == 0)
                    {
                        config.bradley.apcConfig[key].ExtraLoot.LootTable.Add(new LootItem
                        {
                            ShortName = "example.shortname1",
                            Chance = 50, AmountMin = 1,
                            AmountMax = 2,
                            SkinId = 1234567890,
                            DisplayName = "Example Display Name 1"
                        });
                        config.bradley.apcConfig[key].ExtraLoot.LootTable.Add(new LootItem
                        {
                            ShortName = "example.shortname2",
                            Chance = 50, AmountMin = 1,
                            AmountMax = 2,
                            SkinId = 1234567890,
                            DisplayName = "Example Display Name 2"
                        });
                    }
                    if (config.bradley.apcConfig[key].LockedCrateLoot.LootTable.Count == 0)
                    {
                        config.bradley.apcConfig[key].LockedCrateLoot.LootTable.Add(new LootItem
                        {
                            ShortName = "example.shortname1",
                            Chance = 50, AmountMin = 1,
                            AmountMax = 2,
                            SkinId = 1234567890,
                            DisplayName = "Example Display Name 1"
                        });
                        config.bradley.apcConfig[key].LockedCrateLoot.LootTable.Add(new LootItem
                        {
                            ShortName = "example.shortname2",
                            Chance = 50, AmountMin = 1,
                            AmountMax = 2,
                            SkinId = 1234567890,
                            DisplayName = "Example Display Name 2"
                        });
                    }
                }
            }
            if (config.Version < new VersionNumber(1, 1, 1))
            {
                config.options.reportCommand = defaultConfig.options.reportCommand;
                config.bradley.allowTurretDamage = defaultConfig.bradley.allowTurretDamage;
                foreach (var key in config.bradley.apcConfig.Keys)
                {
                    config.bradley.apcConfig[key].GunDamage = defaultConfig.bradley.apcConfig[key].GunDamage;
                    config.bradley.apcConfig[key].GunAccuracy = defaultConfig.bradley.apcConfig[key].GunAccuracy;
                }
            }
            if (config.Version < new VersionNumber(1, 1, 3))
            {
                config.bradley.tierCooldowns = defaultConfig.bradley.tierCooldowns;
                config.bradley.playerLimit = defaultConfig.bradley.playerLimit;
            }
            if (config.Version < new VersionNumber(1, 1, 4))
            {
                config.announce.announceGlobal = defaultConfig.announce.announceGlobal;
                foreach (var key in config.bradley.apcConfig.Keys)
                {
                    config.bradley.apcConfig[key].Loot.MaxBP = 2;
                    config.bradley.apcConfig[key].ExtraLoot.MaxBP = 2;
                    config.bradley.apcConfig[key].LockedCrateLoot.MaxBP = 2;
                    foreach (var lootItem in config.bradley.apcConfig[key].Loot.LootTable)
                    {
                        lootItem.BlueprintChance = 0f;
                    }
                    foreach (var lootItem in config.bradley.apcConfig[key].ExtraLoot.LootTable)
                    {
                        lootItem.BlueprintChance = 0f;
                    }
                    foreach (var lootItem in config.bradley.apcConfig[key].LockedCrateLoot.LootTable)
                    {
                        lootItem.BlueprintChance = 0f;
                    }
                }
            }
            if (config.Version < new VersionNumber(1, 1, 5))
            {
                config.options.buildPrivRadius = defaultConfig.options.buildPrivRadius;
                config.options.clearLandingZone = defaultConfig.options.clearLandingZone;
            }
            if (config.Version < new VersionNumber(1, 1, 7))
            {
                config.options.chatIcon = defaultConfig.options.chatIcon;
            }
            if (config.Version < new VersionNumber(1, 1, 8))
            {
                config.options.useDynamicPVP = defaultConfig.options.useDynamicPVP;
            }

            config.Version = Version;
            SaveConfig();
            Puts("Config update complete!");
        }
        
        #endregion Config
    }
}