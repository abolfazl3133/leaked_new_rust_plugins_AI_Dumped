using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Reflection;
using Time = UnityEngine.Time;
using Random = UnityEngine.Random;

/* Changelog

1.2.23
 - Added: Check during plugin load for duplicate skin ID & profile permission/shortname
 - Fixed: "Failed to run a 11.00 timer" error after heli dies in certain cases
 - Fixed: KeyNotFoundException error during "ProcessRewards"

1.2.22
 - Fixed: Hook conflict with PreventLooting.

1.2.21
 - Fixed: "ArgumentNullException: Value cannot be null. Parameter name: key" error.
 - Fixed: Wave heli signals still sending further helis after retire command used
 
1.2.20 
 - Fixed: Entity is NULL but is still in saveList spam
 - Fixed: Failed to execute OnFrame callback error when throwing Heli Signals
 - Fixed: Players getting rewards if heli retires
 - Fixed: NullReferenceException error when signal thrown while using custom map with custom monuments
 - Added: Support for Better NPC to enable/disable crate guards

1.2.19
 - Fixed: Buy command not working using ServerRewards or Economics
 - Fixed: Heli going into never ending Strafe loop
 - Added: Config option to specify minimum time bweteen strafe attacks
 - Added: Config option to specify Terrain Pushback force (increase if helis glitch under map)
 - Added: Config option to specify Obstacle Pushback force (increase it helis glitch through structures)

1.2.18
 - Fixed: Custom item name being incorrectly assigned causing stacking conflicts
 - Fixed: Active helicopter number reported for some players incorrectly
 - Fixed: Rewards not being processed if heli is destroyed mid air without death spiral
 - Added: Config option to penalize players rewards who do majority damage with controlled auto turrets
 - Added: Config option for Heli to target player controlled auto turrets with optional cooldown option
 - Added: Player controlled turret damage added to damage report
 - Updated: Code efficiency improvements
 
1.2.17
 - Fixed: OnHelicopterStrafeEnter error console spam
 - Fixed: Helis exploding mid air when both rotors are destroyed under certain conditions
 - Added: Option to disable monument crash for heli (NOTE: Sets a server convar).

1.2.16
 - Fixed: For May Rust update
 
1.2.15
 - Fixed: Heli spawning under terrain under certain conditions (causing 'Infinity or NaN' error spam)

1.2.14

 - Fixed: OrbitStrafe chance setting not working correctly
 - Fixed: OrbitStrafe rockets not hitting the correct position
 - Fixed: MapScaleDistance config option now spawns helis the distance from the player, not map center
 - Fixed: Rewards not being issued in some cases unless the heli was hit while crashing
 - Fixed: Retire command not working in some cases
 - Changed: Retire command is now less spammy in chat (better if retiring multiple helis)

1.2.13
 - Fixed: OrbitStrafe rockets fired too high above target intermitently

1.2.12
 - Fixed: Heli never entering strafe mode if constantly attacked
 - Fixed: OnEntityTakeDamage NRE message

1.2.11
 - Fixed: Heli not firing rockets/strafing when Allow Flee option was set to false
 - Added: Wave signals can now call multi heli profiles (new wave not called until all previous helis destroyed)

1.2.10
 - Fixed: Heli fleeing when AllowFlee was set to false. New config options (NOTE: Sets global server ConVar)
 - Added: Config option to change the damage percent received before helicopter flees a heavy attack
 - Added: Config option to specify how many rockets fired during orbit strafe
 - Added: Minimum & maximum variance config options for number of rockets fired during orbit strafe

1.2.9
 - Fixed: For Rust force wipe changes
 - Fixed: Certain chat messages only showing to the calling player instead of the team/chat
 - Added: Config option to specify chance of Heli rocket strafe being an Orbit Strafe type
 - Added: Config option to prevent Heli Fleeing when being attacked

1.2.8
 - Fixed: Plugin failing to load on server start/restart

1.2.7
 - Fixed: Wave Helis are now working as they should
 - Fixed: Allow in monuments config option now works properly
 - Fixed: Occasional NRE OnEntityKill & OnEntityTakeDamage

1.2.6
 - Fixed: Error where the calling player cannot damage the heli unless in a clan
 
1.2.5
 - Fixed: Memory leak leading to poor performance/stuttering in certain cases (Thanks Death for the help)
 - Updated: Code cleanup and edits for better performance
 - Added: Option to award XP via SkillTree instead of XPerience
 - Added: Option to award XP including players XP boosts

1.2.4
 - Fixed: Hook conflict with NpcSpawn
 - Fixed: OnEntityTakeDamage NullReferenceException error
 - Fixed: Occasional bug where killed heli regains HP and is then invincible

1.2.3
 - Fixed: Compatibility with Carbon (Helis now fire rockets on Carbon servers)
 - Fixed: Heli not firing rockets in some circumstances
 - Fixed: NRE console error message
 - Fixed: Heli flying away and not returning when it should
 - Fixed: Heli signals not enabled for buying showing up in the buy list
 
1.2.2
 - Fixed: Wave signals not being purchasable via buy command
 - Added: Separate skin IDs in the config for default wave signals (prevents item name stacking bug)

1.2.1
 - Added: Config option to always block damage to a list of specified entities
 - Added: Config option to give XP as a reward (requires XPerience plugin)
 - Added: Config option to specify number of helis spawned with a signal - default 1 (new skins available on plugin page)
 - Added: Config option to reward with a custom item (shortname, skin, name)
 - Added: Config option to also give damage report if helicopter retires
 - Added: Config option for new Heli Wave Signal (Calls helis in waves, one after the other)
 - Added: Config option to display damage report for vanilla patrol helicopter
 - Added: Config option for display and owner for vanilla patrol helicopter for damage report
*/

namespace Oxide.Plugins
{
    [Info("Heli Signals", "ZEODE", "1.2.24")]
    [Description("Call Patrol Helicopters to your location with custom supply signals.")]
    public class HeliSignals: RustPlugin
    {
        #region Plugin References
        
        [PluginReference] Plugin Friends, Clans, FancyDrop, RewardPlugin, ServerRewards, Economics, Vanish, NoEscape, BotReSpawn, BetterNPC, DynamicPVP, XPerience, SkillTree, NpcSpawn;
        
        #endregion Plugin References

        #region Constants

        private static HeliSignals Instance;
        private static System.Random random = new System.Random();
        
        private const string permAdmin = "helisignals.admin";
        private const string permBuy = "helisignals.buy";
        private const string permBypasscooldown = "helisignals.bypasscooldown";
        
        private const string heliPrefab = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";
        private const string hackableCrate = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";
        private const string shockSound = "assets/prefabs/locks/keypad/effects/lock.code.shock.prefab";
        private const string deniedSound = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";

        private const string defaultWebhookUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        
        private const int supplySignalId = 1397052267;
        private const int scrapId = -932201673;

        // Default config item skins
        // Single
        private const ulong easySkinID = 2920175997;
        private const ulong medSkinID = 2920176079;
        private const ulong hardSkinID = 2920176050;
        private const ulong eliteSkinID = 2920176024;
        private const ulong expertSkinID = 3099117081;
        private const ulong nightmareSkinID = 3099117372;
        // Multi
        private const ulong easyMultiSkinID = 3083234542;
        private const ulong medMultiSkinID = 3083234833;
        private const ulong hardMultiSkinID = 3083234755;
        private const ulong eliteMultiSkinID = 3083234647;
        private const ulong elxpertMultiSkinID = 3099124338;
        private const ulong nightmareMultiSkinID = 3099124426;
        // Wave
        private const ulong normalWaveSkinID = 3104667036;
        private const ulong hardWaveSkinID = 3104666951;

        // Default config item names
        // Single
        private const string easyHeli = "Heli Signal (Easy)";
        private const string medHeli = "Heli Signal (Medium)";
        private const string hardHeli = "Heli Signal (Hard)";
        private const string eliteHeli = "Heli Signal (Elite)";
        // Multi
        private const string easyMulti = "Multi Heli (Easy)";
        private const string medMulti = "Multi Heli (Medium)";
        private const string hardMulti = "Multi Heli (Hard)";
        private const string eliteMulti = "Multi Heli (Elite)";
        // Wave
        private const string normalWave = "Heli Wave Signal (Normal)";
        private const string hardWave = "Heli Wave Signal (Hard)";
        
        #endregion Constants

        #region Language
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SyntaxPlayer"] = "<color=red>Invalid syntax</color>, use:\n\n<color=green>/hsgive <type> <SteamID/PlayerName> <amount></color>",
                ["SyntaxConsole"] = "Invalid syntax, use: hsgive <type> <SteamID/PlayerName> <amount>",
                ["ClearSyntaxPlayer"] = "<color=red>Invalid syntax</color>, use:\n\n<color=green>/hsclearcd</color> (clears all cooldowns)\n\nOr\n\n<color=green>/hsclearcd <SteamID/PlayerName></color>",
                ["ClearSyntaxConsole"] = "Invalid syntax, use: \"hsclearcd\" (clears all cooldowns) or \"hsclearcd <SteamID/PlayerName>\"",
                ["Receive"] = "You received <color=orange>{0}</color> x <color=orange>{1}</color>!",
                ["PlayerReceive"] = "Player {0} ({1}) received {2} x {3}!",
                ["Permission"] = "You do not have permission to use <color=orange>{0}</color>!",
                ["RaidBlocked"] = "You cannot use <color=orange>{0}</color> while raid blocked!",
                ["CombatBlocked"] = "You cannot use <color=orange>{0}</color> while combat blocked!",
                ["CooldownTime"] = "Cooldown active! You can call another in {0}!",
                ["TeamCooldownTime"] = "Team cooldown active! You can call another in {0}!",
                ["GlobalLimit"] = "Global limit of {0} active helicopters is reached, please try again later",
                ["PlayerLimit"] = "Player limit of {0} active helicopters is reached, please try again later",
                ["NotAdmin"] = "You do not have permission to use that command!",
                ["PlayerNotFound"] = "Can't find a player with the name or ID: {0}",
                ["InGameOnly"] = "Error: This command is only for use in game!",
                ["PlayerDead"] = "Player with name or ID {0} is dead, try again when they have respawned",
                ["InNamedMonument"] = "You cannot call <color=orange>{0}</color> in or near <color=red>{1}</color>, signal refunded, check inventory.",
                ["InSafeZone"] = "<color=orange>{0}</color> was thrown in a <color=green>Safe Zone</color> and was refunded, check inventory.",
                ["InvalidDrop"] = "Signal type \"{0}\" not recognised, please check and try again!",
                ["CannotLoot"] = "You cannot loot this because it is not yours!",
                ["CannotHack"] = "You cannot hack this because it is not yours!",
                ["CannotHarvest"] = "You cannot harvest this because it is not yours!",
                ["BuyCmdSyntax"] = "Buy Command Usage (prefix / for chat):\n\n{0}{1}",
                ["NoBuy"] = "Buy Command for <color=orange>{0}</color> Is Not Enabled!",
                ["BuyPermission"] = "You do not have permission to buy Heli Signal \"<color=orange>{0}</color>\".",
                ["PriceList"] = "Heli Signal Prices:\n\n{0}",
                ["HeliKilledTime"] = "<color=orange>{0}</color> killed by <color=green>{1}</color> in grid <color=green>{2}</color> (Time Taken: {3})",
                ["HeliCalled"] = "<color=green>{0}</color> just called in <color=orange>{1}</color> to their location in grid <color=green>{2}</color>",
                ["PointsGiven"] = "<color=green>{0} {1}</color> received for destroying <color=orange>{2}</color>!",
                ["XPGiven"] = "<color=green>{0} XP</color> received for destroying <color=orange>{1}</color>!",
                ["ScrapGiven"] = "<color=green>{0} Scrap</color> received for destroying <color=orange>{1}</color>!",
                ["CustomGiven"] = "<color=green>{0} {1}</color> received for destroying <color=orange>{2}</color>!",
                ["CannotDamage"] = "You <color=red>Cannot</color> damage this <color=orange>{0}</color>!",
                ["NoTurret"] = "You <color=red>Cannot</color> damage this <color=orange>{0}</color> with remote turrets!",
                ["TooFarAway"] = "You are <color=red>Too Far</color> away to engage this <color=orange>{0}</color> ({1} m)",
                ["CantAfford"] = "You <color=red>Cannot</color> afford this! Cost: <color=orange>{0}</color> Required: <color=orange>{1}</color>",
                ["FullInventory"] = "<color=green>{0}</color> <color=red>NOT</color> purchased - no inventory space. You have not been charged.",
                ["PlayerFull"] = "<color=green>{0}</color> <color=red>NOT</color> given to {1} - No inventory space.",
                ["HeliReportTitle"] = "There are currently <color=orange>{0}</color> active Helicopters",
                ["HeliReportItem"] = "<size=9><color=orange>{0}/{1}</color> - <color=green>{2}</color> (Owner: <color=orange>{3}</color> Grid: <color=orange>{4}</color> Health: <color=orange>{5}</color> Rotors: <color=orange>{6}/{7}</color> State: <color=orange>{8}</color>)\n</size>",
                ["HeliReportTitleCon"] = "There are currently {0} active Helicopters",
                ["HeliReportItemCon"] = "{0}/{1} - {2} (Owner: {3} Grid: {4} Health: {5} Rotors: {6}/{7} State: {8})\n",
                ["HeliReportList"] = "{0}",
                ["RetireHeli"] = "<color=orange>{0}</color> is retiring, you were warned! <color=red>{1}</color>/<color=red>{2}</color>",
                ["RetireWarn"] = "<color=red>Damage Blocked!</color> You may only attack from a base with TC auth. If you continue, the <color=orange>{0}</color> will retire. Warning Level: <color=red>{1}</color>/<color=red>{2}</color>",
                ["DmgReport2"] = "<color=orange>Damage</color> / <color=orange>Rotors</color> / <color=orange>Turret Damage</color>\n\n{0}\n{1}",
                ["DmgReportOwner2"] = "<size=11>Type: <color=orange>{0}</color> Owner: <color=green>{1}</color> Status: <color=red>{2}</color>\n</size>",
                ["DmgReportIndex2"] = "<size=11>{0}. <color=green>{1}</color> -> {2} HP / <color=green>{3}%</color> / <color=red>{4}%</color>\n</size>",
                ["DiscordCall"] = "**{0}** just called a **{1}** to their location in grid **{2}**",
                ["DiscordKill"] = "**{0}** just took down a **{1}** in grid **{2}**",
                ["DiscordRetire"] = "**{0}** called by **{1}** just retired from grid **{2}**",
                ["RetiredHelis"] = "You have retired ALL your (your teams) called Patrol Helicopters",
                ["NoRetiredHelis"] = "You have no active Patrol Helicopters to retire.",
                ["CooldownsCleared"] = "All player cooldowns have been cleared!",
                ["PlayerCooldownCleared"] = "Cooldown cleared for player {0} ({1})",
                ["PlayerNoCooldown"] = "No active cooldown for player {0} ({1})",
                ["NoWaveProfiles"] = "There are no wave heli profiles set up for <color=orange>{0}</color>, please report to an Admin.",
                ["WaveProfileError"] = "There is an error in the plugin config <color=orange>{0}</color>, please report to an Admin.",
                ["NextHeliCalled"] = "<color=green>{0}</color> destroyed! Stand by, a <color=red>{1}</color> is now on route to your location!",
                ["WaveFinished"] = "<color=green>{0}</color> destroyed! <color=red>Hostile forces have no more airbourne assets to send to your location.</color> Well Done!",
                ["HeliRetired"] = "<color=green>{0}</color> is low on fuel and is breaking off engagement to return to base.",
                ["WaveRetired"] = "<color=green>{0}</color> finished operations in the area and is retiring. Hostile airbourne forces are returning to base."
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

        private void Init()
        {
            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permBuy, this);
            permission.RegisterPermission(permBypasscooldown, this);
            config.rewards.RegisterPermissions(permission, this);
            config.heli.RegisterPermissions(permission, this);

            AddCovalenceCommand(config.options.reportCommand, nameof(CmdReport));
            AddCovalenceCommand(config.purchasing.buyCommand, nameof(CmdBuySignal));
            AddCovalenceCommand(config.heli.retireCommand, nameof(CmdRetireHeli));
            AddCovalenceCommand("hsgive", nameof(CmdGiveSignal));
            AddCovalenceCommand("hsclearcd", nameof(CmdClearCooldown));
        }

        private void OnServerInitialized()
        {
            Instance = this;
            CheckRewardPlugins();
            NextTick(()=>
            {
                LoadProfileCache();

                if (!config.options.useStacking)
                {
                    Unsubscribe(nameof(CanStackItem));
                    Unsubscribe(nameof(CanCombineDroppedItem));
                }
                if (config.options.noVanillaHeli)
                    PrintWarning($"INFO: Vanilla patrol Helicopter server event is disabled");

                foreach (var monument in TerrainMeta.Path.Monuments)
                {
                    if (config.heli.blockedMonuments.Contains(monument.name))
                        Monuments.Add(monument);
                }
                if (Monuments[0] == null)
                {
                    PrintWarning($"WARNING: No monument info found. Config options relating to 'Allow Players to Call Helis at Monuments' will not function.");
                }
                if (config.heli.allowFlee)
                {
                    RunSilentCommand("patrolhelicopterai.use_danger_zones", "True");
                    RunSilentCommand("patrolhelicopterai.flee_damage_percentage", $"{config.heli.fleePercent}");
                }
                else if (!config.heli.allowFlee)
                {
                    RunSilentCommand("patrolhelicopterai.use_danger_zones", "False");
                    RunSilentCommand("patrolhelicopterai.flee_damage_percentage", "0.35");
                }
                if (config.heli.canMonumentCrash)
                    RunSilentCommand("patrolhelicopterai.monument_crash", "True");
                else if (!config.heli.canMonumentCrash)
                    RunSilentCommand("patrolhelicopterai.monument_crash", "False");
            });
        }

        private void Unload()
        {
            foreach (var netId in HeliSignalData.Keys)
            {
                var heli = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as PatrolHelicopter;
                if (heli != null) heli.Kill();
            }
            if (config.options.noVanillaHeli)
                PrintWarning($"INFO: Vanilla patrol Helicopter server event has been re-enabled");

            HeliSignalData.Clear();
            PlayerCooldowns.Clear();
            TierCooldowns.Clear();
            LockedCrates.Clear();
            HeliProfiles.Clear();
            WaveProfiles.Clear();
            Monuments.Clear();
            HeliProfileCache.Clear();
            RewardPlugin = null;
            Instance = null;
        }

        private object OnEventTrigger(TriggeredEventPrefab eventPrefab)
        {
            if (eventPrefab.name.Contains("event_helicopter") && config.options.noVanillaHeli)
                return true;

            return null;
        }

        // Re-Adds supply signal names to items added via kits/loadouts by plugins
        // which don't specify a custom item display name.
        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (item == null)
                return;
            
            if (item.info?.itemid == supplySignalId)
                NextTick(()=> CheckAndFixSignal(item));
        }

        private object OnExplosiveThrown(BasePlayer player, SupplySignal entity, ThrownWeapon item)
        {
            var signal = item.GetItem();
            if (signal == null)
                return null;
            
            if (HeliProfileCache.ContainsKey(signal.skin))
            {
                entity.EntityToCreate = null;
                entity.CancelInvoke(entity.Explode);
                entity.skinID = signal.skin;
                HeliSignalThrown(player, entity, signal);
            }
            return null;
        }
        private void OnExplosiveDropped(BasePlayer player, SupplySignal entity, ThrownWeapon item) => OnExplosiveThrown(player, entity, item);

        private object CanStackItem(Item item, Item targetItem)
        {
            if (item == null || targetItem == null)
                return null;
            
            if (HeliProfileCache.ContainsKey(item.skin))
            {
                // Only act on Heli Signal supply drops
                if (item.info.itemid == targetItem.info.itemid && item.skin != targetItem.skin)
                    return false;
            }
            return null;
        }

        private object CanCombineDroppedItem(DroppedItem droppedItem, DroppedItem targetItem)
        {
            if (droppedItem == null || targetItem == null)
                return null;

            if (HeliProfileCache.ContainsKey(droppedItem.item.skin))
            {
                // Only act on Heli Signal supply drops
                if (droppedItem.item.info.itemid == targetItem.item.info.itemid && droppedItem.item.skin != targetItem.item.skin)
                    return true;
            }
            return null;
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity.ShortPrefabName.Contains("rocket_heli"))
            {
                NextTick(()=>
                {
                    if (HeliSignalData.Count == 0 || entity == null)
                        return;
                    
                    var rocketEnt = entity as BaseEntity;
                    if (rocketEnt == null)
                        return;

                    var heliList = Pool.Get<List<PatrolHelicopter>>();
                    Vis.Entities(rocketEnt.transform.position, 20f, heliList);

                    var ent = heliList[0];
                    if (ent == null)
                    {
                        Pool.FreeUnmanaged(ref heliList);
                        return;
                    }
                    else if (ent.IsDestroyed)
                    {
                        Pool.FreeUnmanaged(ref heliList);
                        return;
                    }
                    
                    PatrolHelicopter heli = ent as PatrolHelicopter;
                    var heliProfile = heli?._name;
                    if (string.IsNullOrEmpty(heliProfile))
                    {
                        Pool.FreeUnmanaged(ref heliList);
                        return;
                    }

                    if (!config.heli.heliConfig.ContainsKey(heliProfile))
                    {
                        Pool.FreeUnmanaged(ref heliList);
                        return;
                    }
                    
                    rocketEnt.creatorEntity = heli;
                    rocketEnt.OwnerID = heli.OwnerID;
                    SetDamageScale((rocketEnt as TimedExplosive), config.heli.heliConfig[heliProfile].RocketDamageScale);
                    Pool.FreeUnmanaged(ref heliList);
                });
            }
            else if (entity.ShortPrefabName.Equals("napalm"))
            {
                // Set the owner ID & name of fire/napalm from heli rockets to stop NRE which occurs in
                // OnEntityTakeDamage from leftover napalm fire after heli is destroyed.
                NextTick(()=>
                {
                    BaseEntity baseEnt = entity as BaseEntity;
                    if (baseEnt.creatorEntity != null)
                    {
                        baseEnt.OwnerID = baseEnt.creatorEntity.OwnerID;
                        baseEnt._name = baseEnt.creatorEntity._name;
                    }
                });
            }
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var Initiator = info?.Initiator;
            if (Initiator == null || entity == null)
                return null;

            var ownerId = entity.OwnerID;
            if (ownerId == null)
                return null;
            
            if (Initiator is PatrolHelicopter)
            {
                PatrolHelicopter heli = Initiator as PatrolHelicopter;
                if (heli == null)
                    return null;
                else if (!HeliSignalData.ContainsKey(heli.net.ID.Value))
                    return null;
                
                string heliProfile = heli?._name;
                if (heliProfile == null)
                    return null;
                
                if (heliProfile.Contains("patrolhelicopter"))
                    return null;
                
                var heliOwnerId = heli.OwnerID;
                if (heliOwnerId == null)
                    return null;
                
                if (config.heli.heliConfig.TryGetValue(heliProfile, out HeliData? heliData) && heliData != null)
                {
                    if (entity is BasePlayer)
                    {
                        BasePlayer player = entity as BasePlayer;
                        if (player == null)
                            return null;
                        
                        if (heliData.BlockPlayerDamage && !IsOwnerOrFriend(heliOwnerId, player.userID))
                        {
                            info?.damageTypes.ScaleAll(0);
                            return true;
                        }
                        else if (info.damageTypes.GetMajorityDamageType() == DamageType.Bullet)
                        {
                            float hitProb = (float)random.Next(1, 100);
                            if (heliData.BulletAccuracy < hitProb)
                            {
                                info?.damageTypes.ScaleAll(0);
                                return true;
                            }
                        }
                    }
                    else if (ownerId.IsSteamId())
                    {
                        if (heliData.BlockProtectedList && config.heli.protectedPrefabs.Contains(entity.name))
                        {
                            info?.damageTypes.ScaleAll(0);
                            return true;
                        }
                        else if (heliData.BlockOtherDamage && !IsOwnerOrFriend(heliOwnerId, ownerId))
                        {
                            info?.damageTypes.ScaleAll(0);
                            return true;
                        }
                        else if (heliData.BlockOwnerDamage && IsOwnerOrFriend(heliOwnerId, ownerId))
                        {
                            info?.damageTypes.ScaleAll(0);
                            return true;
                        }
                    }
                }
            }
            else if (Initiator.ShortPrefabName.Equals("napalm"))
            {
                string heliProfile = Initiator?._name;
                if (heliProfile == null)
                    return null;
                
                if (!config.heli.heliConfig.TryGetValue(heliProfile, out HeliData? heliData) || heliData == null)
                    return null;

                var heliOwner = Initiator.OwnerID;
                if (heliOwner == null)
                    return null;
                
                if (entity is BasePlayer)
                {
                    BasePlayer player = entity as BasePlayer;
                    if (player == null)
                        return null;
                    
                    if (heliData.BlockPlayerDamage && !IsOwnerOrFriend(heliOwner, player.userID))
                    {
                        info?.damageTypes.ScaleAll(0);
                        return true;
                    }
                }
                else if (ownerId.IsSteamId())
                {
                    if (heliData.BlockProtectedList && config.heli.protectedPrefabs.Contains(entity.name))
                    {
                        info?.damageTypes.ScaleAll(0);
                        return true;
                    }
                    else if (heliData.BlockOtherDamage && !IsOwnerOrFriend(heliOwner, ownerId))
                    {
                        info?.damageTypes.ScaleAll(0);
                        return true;
                    }
                    else if (heliData.BlockOwnerDamage && IsOwnerOrFriend(heliOwner, ownerId))
                    {
                        info?.damageTypes.ScaleAll(0);
                        return true;
                    }
                }
            }
            return null;
        }

        private object OnPatrolHelicopterTakeDamage (PatrolHelicopter heli, HitInfo info)
        {
            var Initiator = info?.Initiator;
            if (heli == null || Initiator == null || Initiator == heli)
                return null;

            var heliProfile = heli._name;
            bool isVanilla = false;
            
            if (heliProfile == null)
            {
                if (heli.skinID == 0)
                    isVanilla = true;
                else
                    return null;
            }
            else if (heliProfile != null)
            {
                if (heliProfile.Contains("patrolhelicopter") && heli.skinID == 0)
                    isVanilla = true;
                else if (!config.heli.heliConfig.ContainsKey(heliProfile))
                    return null;
            }

            var heliAI = heli.myAI;
            var heliId = heli.net.ID.Value;
            var ownerId = heli.OwnerID;
            ulong attackerId = 0;

            BasePlayer attacker = Initiator as BasePlayer;
            AutoTurret turret = Initiator as AutoTurret;
            if (Initiator is BasePlayer)
            {
                attacker = Initiator as BasePlayer;
                if (attacker == null)
                    return null;
                
                attackerId = attacker.userID;
            }
            else if (Initiator is AutoTurret)
            {
                turret = Initiator as AutoTurret;
                if (turret == null)
                    return null;
                
                ulong turretPlayerId = turret.ControllingViewerId.GetValueOrDefault().SteamId;
                if (turretPlayerId == null)
                    return null;
                
                attacker = BasePlayer.FindByID(turretPlayerId);
                if (attacker == null)
                    return null;
                
                attackerId = attacker.userID;
            }
            else
            {
                return null;
            }

            if (attacker == null || attackerId == null)
                return null;
            
            if (isVanilla && (heliAI.isRetiring || heliAI.isDead))
                return null;

            HeliComponent heliComp = heli?.GetComponent<HeliComponent>();
            if (!isVanilla && heliComp == null)
                return null;

            if (!HeliSignalData.ContainsKey(heliId))
            {
                HeliSignalData.Add(heliId, new HeliStats());
                HeliSignalData[heliId].OwnerID = ownerId;
                HeliSignalData[heliId].OwnerName = isVanilla ? config.announce.vanillaOwner : heliComp.owner.displayName;
                HeliSignalData[heliId].Attackers.Add(attackerId, new AttackersStats());
                HeliSignalData[heliId].Attackers[attackerId].Name = attacker.displayName;
            }
            else if (!HeliSignalData[heliId].Attackers.ContainsKey(attackerId))
            {
                HeliSignalData[heliId].Attackers.Add(attackerId, new AttackersStats());
                HeliSignalData[heliId].Attackers[attackerId].Name = attacker.displayName;
            }

            if (!isVanilla)
            {
                if (heliComp.isDying)
                {
                    info?.damageTypes.ScaleAll(0);
                    return true;
                }

                var heliDisplayName = config.heli.heliConfig[heliProfile].HeliName;
                if (config.heli.heliConfig.ContainsKey(heliProfile))
                {
                    if (Initiator is AutoTurret)
                    {
                        if (!config.heli.allowTurretDamage)
                        {
                            info?.damageTypes.ScaleAll(0);
                            ComputerStation station = attacker?.GetMounted() as ComputerStation;
                            if (station != null)
                            {
                                timer.Once(0.25f, ()=> Effect.server.Run(shockSound, station.transform.position));
                                timer.Once(0.5f, ()=> Effect.server.Run(deniedSound, station.transform.position));
                                attacker.EnsureDismounted();
                                Message(attacker, "NoTurret", heliDisplayName);
                            }
                            return true;
                        }

                        HeliSignalData[heliId].LastTurretAttacker = turret;
                        HeliSignalData[heliId].TurretDamage += info.damageTypes.Total();
                        HeliSignalData[heliId].Attackers[attackerId].TurretDamage += info.damageTypes.Total();

                        if (config.heli.heliTargetTurret && (Time.realtimeSinceStartup - heliComp.turretCooldown > config.heli.turretCooldown))
                        {
                            if (!heliComp.isStrafingTurret && (HeliSignalData[heliId].TurretDamage > HeliSignalData[heliId].PlayerDamage))
                            {
                                heliComp.turretCooldown = Time.realtimeSinceStartup;
                                heliComp.isStrafing = true;
                                heliComp.isStrafingTurret = true;
                                heliAI.ExitCurrentState();
                                heliAI.State_Strafe_Enter(attacker, false);
                            }
                        }
                    }
                    else
                    {
                        HeliSignalData[heliId].PlayerDamage += info.damageTypes.Total();
                    }
                }

                if (config.heli.heliConfig[heliProfile].OwnerDamage && !IsOwnerOrFriend(attackerId, ownerId))
                {
                    info?.damageTypes.ScaleAll(0);
                    Message(attacker, "CannotDamage", heliDisplayName);
                    return true;
                }

                var dist = Vector3.Distance(heli.transform.position, attacker.transform.position);
                var maxDist = config.heli.maxHitDistance;
                if (maxDist > 0 && dist > maxDist)
                {
                    info?.damageTypes.ScaleAll(0);
                    Message(attacker, "TooFarAway", heliDisplayName, maxDist);
                    return true;
                }

                if (config.heli.RetireWarning && attacker.IsBuildingBlocked() && IsOwnerOrFriend(attackerId, ownerId))
                {
                    HeliSignalData[heliId].WarningLevel++;
                    if (!heliComp.isRetiring && HeliSignalData[heliId].WarningLevel >= config.heli.WarningThreshold)
                    {
                        heliComp.isRetiring = true;
                        info?.damageTypes.ScaleAll(0);
                        RetireHeli(heliAI, heliId);
                        Message(attacker, "RetireHeli", heliDisplayName, HeliSignalData[heliId].WarningLevel, config.heli.WarningThreshold);
                        return true;
                    }

                    info?.damageTypes.ScaleAll(0);
                    if (!heliComp.isRetiring) Message(attacker, "RetireWarn", heliDisplayName, HeliSignalData[heliId].WarningLevel, config.heli.WarningThreshold);
                    return true;
                }
            }

            if (HeliSignalData[heliId].FirstHitTime == 0)
                HeliSignalData[heliId].FirstHitTime = Time.realtimeSinceStartup;
            
            HeliSignalData[heliId].LastAttacker = attacker;
            HeliSignalData[heliId].Attackers[attackerId].DamageDealt += (info.damageTypes.Total() > heli._health) ? heli._health : info.damageTypes.Total();
            HeliSignalData[heliId].Attackers[attackerId].TotalHits++;
            
            if (info?.HitMaterial == 2306822461)
                HeliSignalData[heliId].Attackers[attackerId].RotorHits++;

            return null;
        }

        private object OnPatrolHelicopterKill(PatrolHelicopter heli, HitInfo info)
        {
            HeliComponent heliComp = heli?.GetComponent<HeliComponent>();
            if (heliComp == null)
                return null;

            var heliProfile = heliComp.heliProfile;
            if (config.heli.heliConfig.ContainsKey(heliProfile))
            {
                if (info.damageTypes.Total() >= heli.health && !heli.myAI.isDead)
                {
                    // Prevent heli blowing up in mid air before crash spiral under certain conditions
                    heli.health = config.heli.heliConfig[heliProfile].Health;
                    heli.myAI.CriticalDamage();
                    return true;
                }
            }

            return null;
        }

        private object OnEntityKill(PatrolHelicopter heli)
        {
            if (!HeliSignalData.ContainsKey(heli.net.ID.Value) || heli.myAI.isRetiring)
                return null;
            
            var skinId = heli.skinID;
            var heliProfile = heli._name;
            bool isVanilla = false;
            if ((heliProfile == null && skinId == 0) || (heliProfile.Contains("patrolhelicopter") && skinId == 0))
                isVanilla = true;
            else if (heliProfile == null && skinId != 0)
                return null;

            var position = heli.transform.position;
            var ownerId = heli.OwnerID;
            var heliId = heli.net.ID.Value;
            var heliDisplayName = isVanilla ? config.announce.vanillaName : config.heli.heliConfig[heliProfile].HeliName;
            var gridPos = PositionToGrid(position);
            if (gridPos == null)
                gridPos = "Unknown";
            
            BasePlayer lastAttacker = HeliSignalData[heliId].LastAttacker;
            if (lastAttacker == null)
                return null;

            if (config.announce.killChat)
            {
                if (!isVanilla || (isVanilla && config.announce.killVanilla))
                {
                    TimeSpan timeSpan = TimeSpan.FromSeconds(Time.realtimeSinceStartup - HeliSignalData[heliId].FirstHitTime);
                    string time = timeSpan.ToString(@"hh\:mm\:ss");
                    var message = string.Format(lang.GetMessage("HeliKilledTime", this, lastAttacker.UserIDString), heliDisplayName, lastAttacker.displayName, gridPos, time);
                    AnnounceToChat(lastAttacker, message);
                }
            }

            if (config.announce.reportChat)
            {
                if (!isVanilla || (isVanilla && config.announce.reportVanilla))
                {
                    var heliOwnerName = config.announce.vanillaOwner;
                    var heliComp = heli.GetComponent<HeliComponent>();
                    if (heliComp != null)
                        heliOwnerName = heliComp.owner.displayName;

                    string topReport = string.Empty;
                    string ownerReport = string.Format(lang.GetMessage("DmgReportOwner2", this, lastAttacker.UserIDString), heliDisplayName, heliOwnerName, "Killed");
                    int count = 1;
                    foreach (var key in HeliSignalData[heliId].Attackers.Keys.OrderByDescending(x => HeliSignalData[heliId].Attackers[x].DamageDealt))
                    {
                        if (count >= config.announce.maxReported)
                            break;
                        
                        string playerName = HeliSignalData[heliId].Attackers[key].Name;
                        float damageDealt = HeliSignalData[heliId].Attackers[key].DamageDealt;
                        int totalHits = HeliSignalData[heliId].Attackers[key].TotalHits;
                        int rotorHits = HeliSignalData[heliId].Attackers[key].RotorHits;
                        float turretDamage = HeliSignalData[heliId].Attackers[key].TurretDamage;
                        double rotorAccuracy = ((double)rotorHits / (double)totalHits) * 100;
                        double damageRatio = ((double)HeliSignalData[heliId].Attackers[key].TurretDamage / (double)HeliSignalData[heliId].Attackers[key].DamageDealt) * 100;
                        topReport += string.Format(lang.GetMessage("DmgReportIndex2", this, lastAttacker.UserIDString), count, playerName, Math.Round(damageDealt, 2), Math.Round(rotorAccuracy, 2), Math.Round(damageRatio, 2));
                        count++;
                    }
                    var dmgReport = string.Format(lang.GetMessage("DmgReport2", this, lastAttacker.UserIDString), ownerReport, topReport);
                    AnnounceToChat(lastAttacker, dmgReport);
                }
            }

            if (!isVanilla)
            {
                if (config.discord.sendHeliKill)
                    SendToDiscord(string.Format(lang.GetMessage("DiscordKill", this, lastAttacker.UserIDString), lastAttacker.displayName, heliProfile, gridPos));

                if (config.heli.heliConfig[heliProfile].LockedCratesToSpawn > 0)
                    SpawnLockedCrates(ownerId, skinId, heliProfile, position);

                NextTick(() =>
                {
                    var ents = Pool.Get<List<BaseEntity>>();
                    Vis.Entities(position, 15f, ents);
                    foreach (var ent in ents)
                    {
                        if ((ent is ServerGib) || (ent is LockedByEntCrate) || (ent is FireBall))
                        {
                            ent.OwnerID = ownerId;
                            ent.skinID = skinId;
                            ent._name = heliProfile;

                            if ((ent is ServerGib) || (ent is LockedByEntCrate))
                            {
                                // COMMENT: Adding box collider & changing rb values to help stop
                                // gibs & crates falling through map on occasion
                                BoxCollider box = ent.gameObject.AddComponent<BoxCollider>();
                                if (box != null)
                                    box.size = new Vector3(0.6f, 0.6f, 0.6f);

                                Rigidbody rigidbody = ent.gameObject.GetComponent<Rigidbody>();
                                if (rigidbody)
                                {
                                    rigidbody.drag = 0.5f;
                                    rigidbody.angularDrag = 0.5f;
                                    rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                                }
                            }
                            ProcessHeliEnt(ent);
                        }
                    }
                    Pool.FreeUnmanaged(ref ents);
                });

                if (BotReSpawn && BotReSpawn.IsLoaded)
                {
                    var botReSpawnProfile = config.heli.heliConfig[heliProfile].BotReSpawnProfile;
                    if (botReSpawnProfile != string.Empty)
                        BotReSpawn?.Call("AddGroupSpawn", position, botReSpawnProfile, $"{botReSpawnProfile}Group", 0);
                }
            }

            timer.Once(15f, () =>
            {
                if (HeliSignalData.ContainsKey(heliId))
                    HeliSignalData.Remove(heliId);
            });
            return null;
        }

        private object CanHelicopterStrafeTarget(PatrolHelicopterAI heliAI, BasePlayer player)
        {
            PatrolHelicopter heli = heliAI?.helicopterBase;
            string heliProfile = heli._name;
            if (heliProfile == null)
                return null;

            if (!config.heli.heliConfig.ContainsKey(heliProfile))
                return null;
            
            if (!config.heli.heliConfig[heliProfile].TargetOtherPlayers && !IsOwnerOrFriend(player.userID, heli.OwnerID))
            {
                heliAI.ExitCurrentState();
                for (int i = 0; i < heliAI._targetList.Count; i++)
                {
                    if (heliAI._targetList[i].ply == player)
                    {
                        heliAI.ClearAimTarget();
                        heliAI._targetList.Remove(heliAI._targetList[i]);
                        continue;
                    }
                }
                return false;
            }
            return null;
        }

        private object CanHelicopterStrafe(PatrolHelicopterAI heliAI)
        {
            HeliComponent heliComp = heliAI?.helicopterBase.GetComponent<HeliComponent>();
            if (heliComp != null)
            {
                if (heliComp.isRetiring || heliComp.isDying)
                    return false;
                else if (heliComp.isStrafing)
                    return false;
                else if (Time.realtimeSinceStartup - heliAI.lastStrafeTime < config.heli.heliConfig[heliComp.heliProfile].StrafeCooldown)
                    return false;
                else
                    return true;
            }
            return null;
        }

        private object OnHelicopterStrafeEnter(PatrolHelicopterAI heliAI, Vector3 strafePosition, BasePlayer strafeTarget)
        {
            HeliComponent heliComp = heliAI?.helicopterBase.GetComponent<HeliComponent>();
            if (heliComp != null)
            {
                if (heliComp.isStrafingTurret)
                {
                    var heliId = heliComp.heliId;
                    if (heliId == null)
                        return null;
                    
                    if (HeliSignalData[heliId].LastTurretAttacker == null)
                        return null;

                    Vector3 turretPos = HeliSignalData[heliId].LastTurretAttacker.transform.position;
                    heliAI.strafe_target_position = turretPos;
                    heliAI.lastStrafeTime = Time.realtimeSinceStartup;
                    heliAI._currentState = PatrolHelicopterAI.aiState.STRAFE;
                    heliAI.numRocketsLeft = config.heli.heliConfig[heliComp.heliProfile].MaxHeliRockets + 1;
                    heliAI.lastRocketTime = 0f;
                    Vector3 randomOffset = heliAI.GetRandomOffset(turretPos, 175f, 192.5f, turretPos.y, turretPos.y + 60f);
                    heliAI.SetTargetDestination(randomOffset, 10f, 60f);
                    heliAI.SetIdealRotation(heliAI.GetYawRotationTo(randomOffset), -1f);
                    heliAI.puttingDistance = true;
                    heliComp.isStrafing = true;

                    HeliSignalData[heliId].TurretDamage = 0f;
                    HeliSignalData[heliId].PlayerDamage = 0f;
                    HeliSignalData[heliId].LastTurretAttacker = null;
                    return true;
                }
                NextTick(()=>
                {
                    heliAI.numRocketsLeft = config.heli.heliConfig[heliComp.heliProfile].MaxHeliRockets + 1;
                    heliComp.UpdateSerializeableFields(heliAI.numRocketsLeft);
                });
            }
            return null;
        }

        private object CanHelicopterUseNapalm(PatrolHelicopterAI heliAI)
        {
            HeliComponent heliComp = heliAI?.helicopterBase.GetComponent<HeliComponent>();
            if (heliComp != null)
            {
                var heliProfile = heliComp.heliProfile;
                if (heliProfile == null)
                    return null;

                if (heliComp.isRetiring || heliComp.isDying)
                    return false;

                return true;
            }
            return null;
        }

        private object OnHelicopterTarget(HelicopterTurret turret, BasePlayer player)
        {
            PatrolHelicopter heli = turret._heliAI?.helicopterBase;
            PatrolHelicopterAI heliAI = turret?._heliAI;
            if (heli == null || heliAI == null)
                return null;
            
            string heliProfile = heli._name;
            if (heliProfile == null)
                return null;

            if (config.heli.heliConfig.ContainsKey(heliProfile))
                return CanCustomHeliTarget(heliAI, player, heliProfile);

            return null;
        }

        private object CanHelicopterTarget(PatrolHelicopterAI heliAI, BasePlayer player)
        {
            string heliProfile = heliAI?.helicopterBase._name;
            if (heliProfile == null)
                return null;

            if (config.heli.heliConfig.ContainsKey(heliProfile))
                return CanCustomHeliTarget(heliAI, player, heliProfile);

            return null;
        }

        private object CanCustomHeliTarget(PatrolHelicopterAI heliAI, BasePlayer player, string heliProfile)
        {
            if (heliAI == null || player == null || heliProfile == null)
                return null;
            
            if (Vanish && (bool)Vanish?.Call("IsInvisible", player))
                return null;
            
            PatrolHelicopter heli = heliAI?.helicopterBase;
            if (heli == null)
                return null;
            
            if (config.heli.heliConfig.ContainsKey(heliProfile))
            {
                if (!config.heli.heliConfig[heliProfile].TargetOtherPlayers && !IsOwnerOrFriend(player.userID, heli.OwnerID))
                {
                    HeliComponent heliComp = heli.GetComponent<HeliComponent>();
                    if (heliComp != null)
                    {
                        heliAI.interestZoneOrigin = heliComp.calledPosition;
                        heliAI.hasInterestZone = true;
                    }
                    return false;
                }
            }
            if (config.heli.maxHitDistance > 0 && (Vector3.Distance(heli.transform.position, player.transform.position) > config.heli.maxHitDistance))
                return false;

            return null;
        }

        private object OnHelicopterRetire(PatrolHelicopterAI heliAI)
        {
            PatrolHelicopter heli = heliAI?.helicopterBase;
            string heliProfile = heli?._name;
            if (heliProfile == null)
                return null;
            
            if (config.heli.heliConfig.ContainsKey(heliProfile) && heliAI.IsAlive())
            {
                HeliComponent heliComp = heli.GetComponent<HeliComponent>();
                if (heliComp == null)
                    return null;

                heliComp.isRetiring = true;
                BasePlayer owner = heliComp.owner;
                heliAI.leftGun.maxTargetRange = 0f;
                heliAI.rightGun.maxTargetRange = 0f;

                if (!heliComp.retireCmdUsed)
                {
                    if (heliComp.isWaveHeli)
                    {
                        var message = Lang("WaveRetired", owner.UserIDString, heliProfile);
                        AnnounceToChat(owner, message);
                    }
                    else
                    {
                        var message = Lang("HeliRetired", owner.UserIDString, heliProfile);
                        AnnounceToChat(owner, message);
                    }
                }

                if (config.announce.reportChat && config.announce.reportRetire)
                {
                    var heliId = heli.net.ID.Value;
                    BasePlayer lastAttacker = HeliSignalData[heliId].LastAttacker;
                    if (lastAttacker != null)
                    {
                        string topReport = string.Empty;
                        string ownerReport = string.Format(lang.GetMessage("DmgReportOwner2", this, lastAttacker.UserIDString), config.heli.heliConfig[heliProfile].HeliName, owner.displayName, "Retired");
                        int count = 1;
                        foreach (var key in HeliSignalData[heliId].Attackers.Keys.OrderByDescending(x => HeliSignalData[heliId].Attackers[x].DamageDealt))
                        {
                            if (count >= config.announce.maxReported)
                                break;
                            
                            string playerName = HeliSignalData[heliId].Attackers[key].Name;
                            float damageDealt = HeliSignalData[heliId].Attackers[key].DamageDealt;
                            int totalHits = HeliSignalData[heliId].Attackers[key].TotalHits;
                            int rotorHits = HeliSignalData[heliId].Attackers[key].RotorHits;
                            float turretDamage = HeliSignalData[heliId].Attackers[key].TurretDamage;
                            double rotorAccuracy = ((double)rotorHits / (double)totalHits) * 100;
                            double damageRatio = ((double)HeliSignalData[heliId].Attackers[key].TurretDamage / (double)HeliSignalData[heliId].Attackers[key].DamageDealt) * 100;
                            topReport += string.Format(lang.GetMessage("DmgReportIndex2", this, lastAttacker.UserIDString), count, playerName, Math.Round(damageDealt, 2), Math.Round(rotorAccuracy, 2), Math.Round(damageRatio, 2));

                            count++;
                        }
                        var dmgReport = string.Format(lang.GetMessage("DmgReport2", this, lastAttacker.UserIDString), ownerReport, topReport);
                        AnnounceToChat(lastAttacker, dmgReport);
                    }
                }

                if (config.discord.sendHeliRetire)
                {
                    var gridPos = PositionToGrid(heli.transform.position);
                    if (gridPos == null)
                        gridPos = "Unknown";

                    BasePlayer player = FindPlayer(heli.OwnerID.ToString())?.Object as BasePlayer;
                    if (player == null)
                        return null;
                    
                    string discordMsg = string.Format(lang.GetMessage("DiscordRetire", this, player.UserIDString), heli._name, player.displayName, gridPos);
                    SendToDiscord(discordMsg);
                }
            }
            return null;
        }

        private object CanLootEntity(BasePlayer player, LockedByEntCrate entity)
        {
            if (entity.OwnerID == 0)
                return null;
            
            string heliProfile = entity?._name;
            if (heliProfile == null)
                return null;
            
            if (config.heli.heliConfig.ContainsKey(heliProfile))
            {
                if (config.heli.heliConfig[heliProfile].ProtectCrates)
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
                var heliProfile = entity._name;
                if (heliProfile == null)
                    return null;

                if (config.heli.heliConfig.ContainsKey(heliProfile))
                {
                    if (config.heli.heliConfig[heliProfile].ProtectGibs)
                    {
                        if (permission.UserHasPermission(attacker.UserIDString, permAdmin))
                            return null;

                        if (!IsOwnerOrFriend(attacker.userID, entity.OwnerID))
                        {
                            info.damageTypes.ScaleAll(0);
                            Message(attacker, "CannotHarvest");
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
                var heliProfile = lootContainer?._name;
                if (heliProfile == null)
                    return;

                if (config.heli.heliConfig.ContainsKey(heliProfile))
                {
                    if (lootContainer.ShortPrefabName.Contains("heli_crate") && config.heli.heliConfig[heliProfile].Loot.UseCustomLoot)
                        SpawnHeliCrateLoot(lootContainer, heliProfile);

                    else if (lootContainer.ShortPrefabName.Contains("heli_crate") && config.heli.heliConfig[heliProfile].ExtraLoot.UseExtraLoot)
                        AddExtraHeliCrateLoot(lootContainer, heliProfile);

                    else if (lootContainer.ShortPrefabName.Contains("codelockedhackablecrate") && config.heli.heliConfig[heliProfile].LockedCrateLoot.UseLockedCrateLoot)
                        SpawnLockedCrateLoot(lootContainer, heliProfile);
                }
            });
        }

        private string PositionToGrid(Vector3 position) => MapHelper.PositionToString(position);

        #endregion Oxide Hooks

        #region Core

        private void HeliSignalThrown(BasePlayer player, SupplySignal entity, Item signal)
        {
            if (player == null || entity == null || signal == null)
                return;
            
            string heliProfile = signal.name;
            ulong skinId = signal.skin;
            int heliAmount = 1;
            string permSuffix = string.Empty;
            bool isWaveHeli = false;
            List<string> waveProfileCache = new List<string>();
            string initialWaveProfile = string.Empty;

            if (string.IsNullOrEmpty(heliProfile))
            {
                HeliProfileCache.TryGetValue(skinId, out heliProfile);

                if (string.IsNullOrEmpty(heliProfile))
                {
                    if (entity != null) entity.Kill();
                    return;
                }
            }

            if (config.heli.heliConfig.ContainsKey(heliProfile))
            {
                heliAmount = config.heli.heliConfig[heliProfile].HeliAmount;
                permSuffix = config.heli.heliConfig[heliProfile].GiveItemCommand.ToLower();
            }
            else if (config.heli.waveConfig.ContainsKey(heliProfile))
            {
                var waveProfile = config.heli.waveConfig[heliProfile].WaveProfiles[0];
                heliAmount = config.heli.heliConfig[waveProfile].HeliAmount;
                permSuffix = config.heli.waveConfig[heliProfile].GiveItemCommand.ToLower();
                isWaveHeli = true;
                if (config.heli.waveConfig[heliProfile].WaveProfiles.Count > 0)
                {
                    foreach (var profile in config.heli.waveConfig[heliProfile].WaveProfiles)
                    {
                        if (!config.heli.heliConfig.ContainsKey(profile))
                        {
                            PrintError($"ERROR: WaveHeli config contains a profile with an incorrect name ({profile}) please correct!");
                            Message(player, "WaveProfileError", heliProfile);
                            if (entity != null) entity.Kill();
                            GiveHeliSignal(player, skinId, heliProfile, 1, "refund");
                            return;
                        }
                        waveProfileCache.Add(profile);
                    }
                    initialWaveProfile = waveProfileCache[0];
                }
                else
                {
                    Message(player, "NoWaveProfiles", heliProfile);
                    if (entity != null) entity.Kill();
                    GiveHeliSignal(player, skinId, heliProfile, 1, "refund");
                    return;
                }
            }

            var perm = $"{Name.ToLower()}.{permSuffix}";
            if (!permission.UserHasPermission(player.UserIDString, perm))
            {
                Message(player, "Permission", heliProfile);
                NextTick(()=>
                {
                    if (entity != null) entity.Kill();
                    GiveHeliSignal(player, skinId, heliProfile, 1, "refund");
                });
                return;
            }

            if (config.heli.UseNoEscape && NoEscape)
            {
                if ((bool)NoEscape.CallHook("IsRaidBlocked", player))
                {
                    Message(player, "RaidBlocked", heliProfile);
                    NextTick(()=>
                    {
                        if (entity != null) entity.Kill();
                        GiveHeliSignal(player, skinId, heliProfile, 1, "refund");
                    });
                    return;
                }
                else if ((bool)NoEscape.CallHook("IsCombatBlocked", player))
                {
                    Message(player, "CombatBlocked", heliProfile);
                    NextTick(()=>
                    {
                        if (entity != null) entity.Kill();
                        GiveHeliSignal(player, skinId, heliProfile, 1, "refund");
                    });
                    return;
                }
            }

            if (config.heli.playerCooldown > 0f && !permission.UserHasPermission(player.UserIDString, permBypasscooldown))
            {
                float cooldown;
                ulong userId = player.userID;
                if (!config.heli.tierCooldowns)
                {
                    if (PlayerCooldowns.TryGetValue(userId, out cooldown))
                    {
                        TimeSpan time = TimeSpan.FromSeconds(cooldown - Time.time);
                        Message(player, "CooldownTime", time.ToString(@"hh\:mm\:ss"));
                        NextTick(()=>
                        {
                            if (entity != null) entity.Kill();
                            GiveHeliSignal(player, skinId, heliProfile, 1, "refund");
                        });
                        return;
                    }
                    else if (config.heli.teamCooldown)
                    {
                        foreach (var playerId in PlayerCooldowns.Keys)
                        {
                            if (PlayerCooldowns.TryGetValue(playerId, out cooldown) && IsOwnerOrFriend(userId, playerId))
                            {
                                TimeSpan time = TimeSpan.FromSeconds(cooldown - Time.time);
                                Message(player, "TeamCooldownTime", time.ToString(@"hh\:mm\:ss"));
                                NextTick(()=>
                                {
                                    if (entity != null) entity.Kill();
                                    GiveHeliSignal(player, skinId, heliProfile, 1, "refund");
                                });
                                return;
                            }
                        }
                    }
                }
                else
                {
                    if (TierCooldowns.ContainsKey(userId))
                    {
                        if (TierCooldowns[userId].TryGetValue(heliProfile, out cooldown))
                        {
                            TimeSpan time = TimeSpan.FromSeconds(cooldown - Time.time);
                            Message(player, "CooldownTime", time.ToString(@"hh\:mm\:ss"));
                            NextTick(()=>
                            {
                                if (entity != null) entity.Kill();
                                GiveHeliSignal(player, skinId, heliProfile, 1, "refund");
                            });
                            return;
                        }
                        else if (config.heli.teamCooldown)
                        {
                            foreach (var playerId in TierCooldowns.Keys)
                            {
                                if (TierCooldowns[userId].TryGetValue(heliProfile, out cooldown) && IsOwnerOrFriend(userId, playerId))
                                {
                                    TimeSpan time = TimeSpan.FromSeconds(cooldown - Time.time);
                                    Message(player, "TeamCooldownTime", time.ToString(@"hh\:mm\:ss"));
                                    NextTick(()=>
                                    {
                                        if (entity != null) entity.Kill();
                                        GiveHeliSignal(player, skinId, heliProfile, 1, "refund");
                                    });
                                    return;
                                }
                            }
                        }
                    }
                }

                cooldown = config.heli.playerCooldown;
                foreach (KeyValuePair<string, float> keyPair in config.heli.vipCooldowns)
                {
                    if (permission.UserHasPermission(player.UserIDString, keyPair.Key))
                    {
                        if (keyPair.Value < cooldown)
                        {
                            cooldown = keyPair.Value;
                            continue;
                        }
                    }
                }

                if (!config.heli.tierCooldowns)
                {
                    if (!PlayerCooldowns.ContainsKey(userId))
                        PlayerCooldowns.Add(userId, Time.time + cooldown);
                    else
                        PlayerCooldowns[userId] = Time.time + cooldown;
                    
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

                    if (!TierCooldowns[userId].ContainsKey(heliProfile))
                        TierCooldowns[userId].Add(heliProfile, Time.time + cooldown);
                    else
                        TierCooldowns[userId][heliProfile] = Time.time + cooldown;

                    timer.Once(cooldown, () =>
                    {
                        if (!TierCooldowns.ContainsKey(userId))
                            return;

                        if (TierCooldowns[userId].ContainsKey(heliProfile))
                            TierCooldowns[userId].Remove(heliProfile);
                    });
                }
            }

            HeliSignalComponent signalComponent = entity?.gameObject.AddComponent<HeliSignalComponent>();
            if (signalComponent != null)
            {
                signalComponent.signal = entity;
                signalComponent.player = player;
                signalComponent.skinId = skinId;
                signalComponent.heliProfile = isWaveHeli ? initialWaveProfile : heliProfile;
                signalComponent.heliAmount = heliAmount;
                signalComponent.isWaveHeli = isWaveHeli;
                signalComponent.waveProfile = heliProfile;
                signalComponent.waveProfileCache = waveProfileCache;
            }
        }

        private void ProcessHeliEnt (BaseEntity entity)
        {
            if (entity != null)
            {
                var heliProfile = entity._name;
                if (heliProfile != null)
                {
                    if (entity is HelicopterDebris)
                    {
                        var debris = entity as HelicopterDebris;
                        if (debris != null)
                        {
                            debris.InitializeHealth(config.heli.heliConfig[heliProfile].GibsHealth, config.heli.heliConfig[heliProfile].GibsHealth);
                            debris.SendNetworkUpdate();

                            if (config.heli.heliConfig[heliProfile].KillGibs)
                                NextTick(()=> debris.Kill());
                            
                            else if (config.heli.heliConfig[heliProfile].DisableFire)
                                debris.tooHotUntil = Time.realtimeSinceStartup;

                            else if (config.heli.heliConfig[heliProfile].GibsHotTime > 0)
                                debris.tooHotUntil = Time.realtimeSinceStartup + config.heli.heliConfig[heliProfile].GibsHotTime;

                            if (config.heli.heliConfig[heliProfile].ProtectGibs && config.heli.heliConfig[heliProfile].UnlockGibs > 0)
                            {
                                float unlockTime = config.heli.heliConfig[heliProfile].DisableFire ? config.heli.heliConfig[heliProfile].UnlockGibs :
                                                    (config.heli.heliConfig[heliProfile].FireDuration + config.heli.heliConfig[heliProfile].UnlockGibs);
                                RemoveHeliOwner(debris, unlockTime);
                            }
                            debris.SendNetworkUpdateImmediate();
                        }
                    }
                    else if (entity is FireBall)
                    {
                        var fireball = entity as FireBall;
                        if (fireball != null)
                        {
                            if (config.heli.heliConfig[heliProfile].DisableFire)
                            {
                                NextTick(()=> fireball.Kill());
                            }
                            else
                            {
                                timer.Once(config.heli.heliConfig[heliProfile].FireDuration, () =>
                                {
                                    if (fireball != null)
                                        NextTick(()=> fireball.Kill());
                                });
                            }
                            fireball.SendNetworkUpdateImmediate();
                        }
                    }
                    else if (entity is LockedByEntCrate)
                    {
                        var crate = entity as LockedByEntCrate;
                        if (crate != null)
                        {
                            if (config.heli.heliConfig[heliProfile].FireDuration > 0 && !config.heli.heliConfig[heliProfile].DisableFire)
                            {
                                timer.Once(config.heli.heliConfig[heliProfile].FireDuration, () =>
                                {
                                    if (crate != null)
                                    {
                                        if (crate.lockingEnt != null)
                                        {
                                            var lockingEnt = crate.lockingEnt.GetComponent<FireBall>();
                                            if (lockingEnt != null)
                                                NextTick(()=> lockingEnt.Kill());
                                        }
                                        crate.SetLockingEnt(null);
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
                                crate.SetLockingEnt(null);
                            }
                            
                            if (config.heli.heliConfig[heliProfile].ProtectCrates && config.heli.heliConfig[heliProfile].UnlockCrates > 0)
                            {
                                float unlockTime = config.heli.heliConfig[heliProfile].DisableFire ? config.heli.heliConfig[heliProfile].UnlockCrates :
                                                    (config.heli.heliConfig[heliProfile].FireDuration + config.heli.heliConfig[heliProfile].UnlockCrates);

                                RemoveHeliOwner(entity, unlockTime);
                            }
                        }
                    }
                }
            }
        }

        #endregion Core

        #region Loot

        private void SpawnHeliCrateLoot(LootContainer lootContainer, string heliProfile)
        {
            if (lootContainer == null || heliProfile == null)
                return;

            lootContainer.inventory.capacity = 12; // unlock all slots
            lootContainer.inventory.Clear();
            ItemManager.DoRemoves();

            List<LootItem> lootTable = new List<LootItem>(config.heli.heliConfig[heliProfile].Loot.LootTable);
            List<LootItem> items = lootTable;

            if (items == null || lootTable == null)
                return;

            var minItems = config.heli.heliConfig[heliProfile].Loot.MinCrateItems;
            var maxItems = config.heli.heliConfig[heliProfile].Loot.MaxCrateItems;
            int count = Random.Range(minItems, maxItems + 1);
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

                if (lootItem.Chance < Random.Range(0f, 100f))
                {
                    if (given < minItems)
                    {
                        i--;
                        continue;
                    }
                    break;
                }

                // Only remove if enough in loot table (if loot table not adequate)
                if (!config.heli.heliConfig[heliProfile].Loot.AllowDupes && count <= items.Count)
                    items.Remove(lootItem);
                
                // Re-add loot if items run out (if loot table not adequate)
                if (items.Count == 0)
                    items.AddRange(lootTable);

                ItemDefinition itemDef = ItemManager.FindItemDefinition(lootItem.ShortName);
                Item item = null;

                if (Random.Range(0f, 101f) <= lootItem.BlueprintChance && itemDef.Blueprint != null && IsBP(itemDef) && bps < config.heli.heliConfig[heliProfile].Loot.MaxBP)
                {
                    ItemDefinition bpDef = ItemManager.FindItemDefinition("blueprintbase");
                    item = ItemManager.Create(bpDef, 1, 0uL);
                    item.blueprintTarget = itemDef.itemid;
                    bps++;
                }
                else
                {
                    var amount = Random.Range(lootItem.AmountMin, lootItem.AmountMax + 1);
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

        private void AddExtraHeliCrateLoot (LootContainer lootContainer, string heliProfile)
        {
            if (lootContainer == null || heliProfile == null)
                return;

            List<LootItem> lootTable = new List<LootItem>(config.heli.heliConfig[heliProfile].ExtraLoot.LootTable);
            List<LootItem> items = lootTable;

            if (items == null || lootTable == null)
                return;
            
            lootContainer.inventory.capacity = 12; // unlock all slots
            var minItems = config.heli.heliConfig[heliProfile].ExtraLoot.MinExtraItems;
            var maxItems = config.heli.heliConfig[heliProfile].ExtraLoot.MaxExtraItems;
            int count = Random.Range(minItems, maxItems + 1);
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

                if (lootItem.Chance < Random.Range(0f, 101f))
                {
                    if (given < minItems)
                    {
                        i--;
                        continue;
                    }
                    break;
                }

                // Only remove if enough in loot table (if loot table not adequate)
                if (!config.heli.heliConfig[heliProfile].ExtraLoot.AllowDupes && count <= items.Count)
                    items.Remove(lootItem);
                
                // Re-add loot if items run out (if loot table not adequate)
                if (items.Count == 0)
                    items.AddRange(lootTable);

                ItemDefinition itemDef = ItemManager.FindItemDefinition(lootItem.ShortName);
                Item item = null;

                if (Random.Range(0f, 101f) <= lootItem.BlueprintChance && IsBP(itemDef) && bps < config.heli.heliConfig[heliProfile].ExtraLoot.MaxBP)
                {
                    ItemDefinition bpDef = ItemManager.FindItemDefinition("blueprintbase");
                    item = ItemManager.Create(bpDef, 1, 0uL);
                    item.blueprintTarget = itemDef.itemid;
                    bps++;
                }
                else
                {
                    var amount = Random.Range(lootItem.AmountMin, lootItem.AmountMax + 1);
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

        private void SpawnLockedCrateLoot(LootContainer lootContainer, string heliProfile)
        {
            if (lootContainer == null || heliProfile == null)
                return;

            lootContainer.inventory.capacity = 36; // unlock all slots
            lootContainer.inventory.Clear();
            ItemManager.DoRemoves();

            List<LootItem> lootTable = new List<LootItem>(config.heli.heliConfig[heliProfile].LockedCrateLoot.LootTable);
            List<LootItem> items = lootTable;

            if (items == null || lootTable == null)
                return;

            var minItems = config.heli.heliConfig[heliProfile].LockedCrateLoot.MinLockedCrateItems;
            var maxItems = config.heli.heliConfig[heliProfile].LockedCrateLoot.MaxLockedCrateItems;
            int count = Random.Range(minItems, maxItems + 1);
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

                if (lootItem.Chance < Random.Range(0f, 101f))
                {
                    if (given < minItems)
                    {
                        i--;
                        continue;
                    }
                    break;
                }

                // Only remove if enough in loot table (if loot table not adequate)
                if (!config.heli.heliConfig[heliProfile].LockedCrateLoot.AllowDupes && count <= items.Count)
                    items.Remove(lootItem);
                
                // Re-add loot if items run out (if loot table not adequate)
                if (items.Count == 0)
                    items.AddRange(lootTable);

                ItemDefinition itemDef = ItemManager.FindItemDefinition(lootItem.ShortName);
                Item item = null;

                if (Random.Range(0f, 101f) <= lootItem.BlueprintChance && IsBP(itemDef) && bps <= config.heli.heliConfig[heliProfile].LockedCrateLoot.MaxBP)
                {
                    ItemDefinition bpDef = ItemManager.FindItemDefinition("blueprintbase");
                    item = ItemManager.Create(bpDef, 1, 0uL);
                    item.blueprintTarget = itemDef.itemid;
                    bps++;
                }
                else
                {
                    var amount = Random.Range(lootItem.AmountMin, lootItem.AmountMax + 1);
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

        private void SpawnLockedCrates(ulong ownerId, ulong skinId, string heliProfile, Vector3 position)
        {
            for (int i = 0; i < config.heli.heliConfig[heliProfile].LockedCratesToSpawn; i++)
            {
                Vector2 rand;
                rand = Random.insideUnitCircle * 5f;
                position = position + new Vector3(rand.x, 2f, (rand.y));
                HackableLockedCrate crate = GameManager.server.CreateEntity(hackableCrate, position, new Quaternion()) as HackableLockedCrate;
                if (crate == null)
                    return;

                crate._name = heliProfile;
                crate.Spawn();
                crate.Invoke(new Action(crate.DelayedDestroy), config.heli.heliConfig[heliProfile].LockedCrateDespawn);

                NextTick(()=>
                {
                    crate.OwnerID = ownerId;
                    crate.skinID = skinId;
                });

                Rigidbody rigidbody = crate.gameObject.GetComponent<Rigidbody>();
                if (rigidbody)
                {
                    rigidbody.drag = 1.0f;
                    rigidbody.angularDrag = 1.0f;
                    rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }

                LockedCrates.Add(crate.net.ID.Value, ownerId);

                float unlockTime = config.heli.heliConfig[heliProfile].UnlockCrates;
                if (config.heli.heliConfig[heliProfile].ProtectCrates && unlockTime > 0)
                {
                    timer.Once(unlockTime, ()=>
                    {
                        // Unlock the locked crates to anyone after time, if set in config
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
            string heliProfile = crate?._name;
            if (heliProfile == null)
                return;

            if (config.heli.heliConfig.ContainsKey(heliProfile))
            {
                NextTick(()=>
                {
                    float hackTime = 900f - config.heli.heliConfig[heliProfile].HackSeconds;
                    if (crate.hackSeconds > hackTime)
                        return;
                    
                    crate.hackSeconds = hackTime;
                });
            }
        }

        private object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            string heliProfile = crate?._name;
            if (heliProfile == null)
                return null;

            if (config.heli.heliConfig.ContainsKey(heliProfile))
            {
                ulong crateId = crate.net.ID.Value;
                ulong crateOwnerId;
                
                if (!LockedCrates.TryGetValue(crateId, out crateOwnerId))
                    return null;
                
                if (config.heli.heliConfig[heliProfile].ProtectCrates && !IsOwnerOrFriend(player.userID, crateOwnerId))
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
                PrintWarning($"Discord message not sent, please check WebHook URL within the config.");
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
                PrintError($"ERROR: {callbackMsg}");
        }

        #endregion Discord Announcements

        #region API

        // Other devs can call these hooks to help with compatability with their plugins if needed
        object IsHeliSignalObject(ulong skinId) => HeliProfileCache.ContainsKey(skinId) ? true : (object)null;
        
        // Play nice with BetterNPC
        object CanHelicopterSpawnNpc(PatrolHelicopter helicopter)
        {
            if (helicopter != null)
            {
                if (!config.options.useBetterNPC && HeliProfileCache.ContainsKey(helicopter.skinID) && HeliSignalData.ContainsKey(helicopter.net.ID.Value))
                {
                    return true;
                }
            }
            return null;
        }

        // Play nice with FancyDrop
        object ShouldFancyDrop(NetworkableId netId)
        {
            var signal = BaseNetworkable.serverEntities.Find(netId) as BaseEntity;
            if (signal != null)
            {
                if (HeliProfileCache.ContainsKey(signal.skinID))
                    return true;
            }
            return null;
        }

        // Play nice with AlphaLoot
        object CanPopulateLoot(LootContainer lootContainer)
        {
            return HeliProfileCache.ContainsKey(lootContainer.skinID) ? true : (object)null;
        }

        object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (NpcSpawn && entity.skinID == 11162132011012)
                return null;

            if (hitInfo?.Initiator == null)
                return null;

            string heliProfile = hitInfo.Initiator._name;
            if (heliProfile == null)
                return null;

            if (config.heli.heliConfig.ContainsKey(heliProfile))
                return true;

            return null;
        }

        // Play nice with BotReSpawn
        object OnBotReSpawnPatrolHeliKill(PatrolHelicopterAI heliAi)
        {
            PatrolHelicopter heli = heliAi.helicopterBase;
            if (HeliSignalData.ContainsKey(heli.net.ID.Value) && HeliProfileCache.ContainsKey(heli.skinID))
                return true;

            return null;
        }

        // Play nice with Dynamic PvP
        private object OnCreateDynamicPVP(string eventName, BaseEntity entity)
        {
            if (!config.options.useDynamicPVP)
            {
                if (HeliSignalData.ContainsKey(entity.net.ID.Value) && HeliProfileCache.ContainsKey(entity.skinID))
                    return true;
            }
            return null;
        }

        #endregion API

        #region Rewards

        private void CheckRewardPlugins()
        {
            if (config.rewards.enableRewards || CanPurchaseAnySignal())
            {
                RewardPlugin = plugins.Find(config.rewards.rewardPlugin);
                if (!RewardPlugin)
                {
                    config.rewards.enableRewards = false;
                    PrintWarning($"{config.rewards.rewardPlugin} not found, giving rewards is not possible until loaded.");
                }
            }
            
            if (config.rewards.enableXP)
            {
                if (config.rewards.pluginXP.ToLower() == "xperience" && !XPerience)
                {
                    config.rewards.enableXP = false;
                    PrintWarning($"XPerience plugin not found, giving XP is not possible until loaded.");
                }
                else if (config.rewards.pluginXP.ToLower() == "skilltree" && !SkillTree)
                {
                    config.rewards.enableXP = false;
                    PrintWarning($"SkillTree plugin not found, giving XP is not possible until loaded.");
                }
            }
        }

        private void GiveReward(ulong playerId, double amount, string heliProfile)
        {
            foreach (KeyValuePair<string, float> keyPair in config.rewards.rewardMultipliers)
            {
                if (permission.UserHasPermission(playerId.ToString(), keyPair.Key))
                {
                    if (keyPair.Value > 1.0f)
                        amount = (int)amount * keyPair.Value;
                }
            }

            BasePlayer player = FindPlayer(playerId.ToString())?.Object as BasePlayer;
            if (!player)
            {
                PrintError($"ERROR: Failed to give reward to: {playerId}, no player found.");
                return;
            }
            else if (!plugins.Find(config.rewards.rewardPlugin))
            {
                PrintError($"ERROR: Failed to give reward to: {playerId}, {config.rewards.rewardPlugin} not loaded.");
                return;
            }
            switch (config.rewards.rewardPlugin.ToLower())
            {
                case "serverrewards":
                    RewardPlugin?.Call("AddPoints", playerId, (int)amount);
                    Message(player, "PointsGiven", (int)amount, config.rewards.rewardUnit, heliProfile);
                    break;
                case "economics":
                    RewardPlugin?.Call("Deposit", playerId, amount);
                    Message(player, "PointsGiven", config.rewards.rewardUnit, (int)amount, heliProfile);
                    break;
                default:
                    break;
            }
            return;
        }

        private void GiveXP(ulong playerId, double amount, string heliProfile)
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
            if (!player)
            {
                PrintError($"ERROR: Failed to give XP to: {playerId}, no player found.");
                return;
            }
            
            if (config.rewards.pluginXP.ToLower() == "xperience")
            {
                if (!XPerience)
                {
                    PrintError($"ERROR: Failed to give XP to: {playerId}, XPerience is not loaded");
                    return;
                }
                if (config.rewards.boostXP)
                    XPerience?.Call("GiveXP", player, amount);
                else
                    XPerience?.Call("GiveXPBasic", player, amount);
            }
            else if (config.rewards.pluginXP.ToLower() == "skilltree")
            {
                if (!SkillTree)
                {
                    PrintError($"ERROR: Failed to give XP to: {playerId}, SkillTree is not loaded");
                    return;
                }
                if (config.rewards.boostXP)
                    SkillTree?.Call("AwardXP", player, amount, "HeliSignals", false);
                else
                    SkillTree?.Call("AwardXP", player, amount, "HeliSignals", true);
            }
            Message(player, "XPGiven", amount, heliProfile);
        }

        private void GiveScrap(ulong playerId, int amount, string heliProfile)
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
                Message(player, "ScrapGiven", amount, heliProfile);
                return;
            }
            PrintError($"ERROR: Failed to give scrap to: {playerId}, no player found.");
        }
        
        private void GiveCustomReward(ulong playerId, int amount, string heliProfile)
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
                var itemShortname = config.rewards.customRewardItem.ShortName;
                var skinId = config.rewards.customRewardItem.SkinId;
                Item custom = ItemManager.CreateByName(itemShortname, amount, skinId);
                if (!string.IsNullOrEmpty(config.rewards.customRewardItem.DisplayName))
                {
                    custom.name = config.rewards.customRewardItem.DisplayName;
                    Message(player, "CustomGiven", amount, custom.name, heliProfile);
                }
                else
                {
                    Message(player, "CustomGiven", amount, custom.info.displayName.translated, heliProfile);
                }
                
                player.inventory.GiveItem(custom);
                return;
            }
            PrintError($"ERROR: Failed to give custom reward to: {playerId}, no player found.");
        }
        
        private void ProcessRewards(ulong heliId, ulong ownerId, string heliProfile)
        {
        	if (heliId == null || ownerId == null || heliProfile == null)
                return;
            
            var totalReward = config.heli.heliConfig[heliProfile].RewardPoints;
            var totalXP = config.heli.heliConfig[heliProfile].XPReward;
            var totalScrap = config.heli.heliConfig[heliProfile].ScrapReward;
            var totalCustom = config.heli.heliConfig[heliProfile].CustomReward;
            float damageThreshold = config.heli.heliConfig[heliProfile].DamageThreshold;
            var eligibleAttackers = HeliSignalData[heliId].Attackers.Count(key => key.Value.DamageDealt >= damageThreshold);
            double turretPenalty = (100 - config.heli.turretPenalty) / 100;

            if (!HeliSignalData.ContainsKey(heliId))
                return;;

            if ((config.rewards.enableRewards && totalReward > 0))
            {
                if (config.rewards.shareRewards)
                {
                    foreach (var playerId in HeliSignalData[heliId].Attackers.Keys)
                    {
                        if (!HeliSignalData[heliId].Attackers.ContainsKey(playerId))
                            continue;
                        
                        float damageDealt = HeliSignalData[heliId].Attackers[playerId].DamageDealt;
                        if (damageDealt >= damageThreshold)
                        {
                            var amount = totalReward / eligibleAttackers;
                            float damageRatio = (HeliSignalData[heliId].Attackers[playerId].TurretDamage / HeliSignalData[heliId].Attackers[playerId].DamageDealt);
                            if (config.heli.allowTurretDamage && config.heli.turretPenalty > 0f && damageRatio > 0.5f)
                                amount = amount * turretPenalty;
                            
                            GiveReward(playerId, amount, heliProfile);
                        }
                    }
                }
                else
                {
                    if (!HeliSignalData[heliId].Attackers.ContainsKey(ownerId))
                        return;
                    
                    float damageRatio = (HeliSignalData[heliId].Attackers[ownerId].TurretDamage / HeliSignalData[heliId].Attackers[ownerId].DamageDealt);
                    if (config.heli.allowTurretDamage && config.heli.turretPenalty > 0f && damageRatio > 0.5f)
                        totalReward = totalReward * turretPenalty;
                    
                    GiveReward(ownerId, totalReward, heliProfile);
                }
            }

            if (config.rewards.enableXP && totalXP > 0)
            {
                if (config.rewards.shareXP)
                {
                    foreach (var playerId in HeliSignalData[heliId].Attackers.Keys)
                    {
                        if (!HeliSignalData[heliId].Attackers.ContainsKey(playerId))
                            continue;
                        
                        float damageDealt = HeliSignalData[heliId].Attackers[playerId].DamageDealt;
                        if (damageDealt >= damageThreshold)
                        {
                            var amount = totalXP / eligibleAttackers;
                            float damageRatio = (HeliSignalData[heliId].Attackers[playerId].TurretDamage / HeliSignalData[heliId].Attackers[playerId].DamageDealt);
                            if (config.heli.allowTurretDamage && config.heli.turretPenalty > 0f && damageRatio > 0.5f)
                                amount = amount * turretPenalty;
                            
                            GiveXP(playerId, amount, heliProfile);
                        }
                    }
                }
                else
                {
                    if (!HeliSignalData[heliId].Attackers.ContainsKey(ownerId))
                        return;
                    
                    float damageRatio = (HeliSignalData[heliId].Attackers[ownerId].TurretDamage / HeliSignalData[heliId].Attackers[ownerId].DamageDealt);
                    if (config.heli.allowTurretDamage && config.heli.turretPenalty > 0f && damageRatio > 0.5f)
                        totalXP = totalXP * turretPenalty;
                    
                    GiveXP(ownerId, totalXP, heliProfile);
                }
            }

            if (config.rewards.enableScrap && totalScrap > 0)
            {
                if (config.rewards.shareScrap)
                {
                    foreach (var playerId in HeliSignalData[heliId].Attackers.Keys)
                    {
                        if (!HeliSignalData[heliId].Attackers.ContainsKey(playerId))
                            continue;
                        
                        float damageDealt = HeliSignalData[heliId].Attackers[playerId].DamageDealt;
                        if (damageDealt >= damageThreshold)
                        {
                            var amount = totalScrap / eligibleAttackers;
                            float damageRatio = (HeliSignalData[heliId].Attackers[playerId].TurretDamage / HeliSignalData[heliId].Attackers[playerId].DamageDealt);
                            if (config.heli.allowTurretDamage && config.heli.turretPenalty > 0f && damageRatio > 0.5f)
                                amount = (int)(amount * turretPenalty);
                            
                            GiveScrap(playerId, amount, heliProfile);
                        }
                    }
                }
                else
                {
                    if (!HeliSignalData[heliId].Attackers.ContainsKey(ownerId))
                        return;
                    
                    float damageRatio = (HeliSignalData[heliId].Attackers[ownerId].TurretDamage / HeliSignalData[heliId].Attackers[ownerId].DamageDealt);
                    if (config.heli.allowTurretDamage && config.heli.turretPenalty > 0f && damageRatio > 0.5f)
                        totalScrap = (int)(totalScrap * turretPenalty);
                    
                    GiveScrap(ownerId, totalScrap, heliProfile);
                }
            }

            if (config.rewards.enableCustomReward && totalCustom > 0)
            {
                if (config.rewards.shareCustomReward)
                {
                    foreach (var playerId in HeliSignalData[heliId].Attackers.Keys)
                    {
                        if (!HeliSignalData[heliId].Attackers.ContainsKey(playerId))
                            continue;

                        float damageDealt = HeliSignalData[heliId].Attackers[playerId].DamageDealt;
                        if (damageDealt >= damageThreshold)
                        {
                            var amount = totalCustom / eligibleAttackers;
                            float damageRatio = (HeliSignalData[heliId].Attackers[playerId].TurretDamage / HeliSignalData[heliId].Attackers[playerId].DamageDealt);
                            if (config.heli.allowTurretDamage && config.heli.turretPenalty > 0f && damageRatio > 0.5f)
                                amount = (int)(amount * turretPenalty);
                            
                            GiveCustomReward(playerId, amount, heliProfile);
                        }
                    }
                }
                else
                {
                    if (!HeliSignalData[heliId].Attackers.ContainsKey(ownerId))
                        return;

                    float damageRatio = (HeliSignalData[heliId].Attackers[ownerId].TurretDamage / HeliSignalData[heliId].Attackers[ownerId].DamageDealt);
                    if (config.heli.allowTurretDamage && config.heli.turretPenalty > 0f && damageRatio > 0.5f)
                        totalCustom = (int)(totalCustom * turretPenalty);
                    
                    GiveCustomReward(ownerId, totalCustom, heliProfile);
                }
            }
        }
        
        #endregion Rewards

        #region Helpers
        
        private void LoadProfileCache()
        {
            foreach (var key in config.heli.heliConfig.Keys)
            {
                var permSuffix = config.heli.heliConfig[key].GiveItemCommand.ToLower();
                var perm = $"{Name.ToLower()}.{permSuffix}";
                permission.RegisterPermission(perm, this);

                if (HeliProfiles.Contains(config.heli.heliConfig[key].GiveItemCommand))
                {
                    PrintError($"ERROR: One or more Heli Profiles contains a duplicate 'Profile Shortname', these must be unique for each profile. Correct your config & reload.");
                    continue;
                }
                else
                {
                    HeliProfiles.Add(config.heli.heliConfig[key].GiveItemCommand);
                }

                if (HeliProfileCache.ContainsKey(config.heli.heliConfig[key].SignalSkinID))
                {
                    PrintError($"ERROR: One or more Heli Profiles contains a duplicate 'Skin ID', these must be unique for each profile. Correct your config & reload.");
                    continue;
                }
                else
                {
                    HeliProfileCache.Add(config.heli.heliConfig[key].SignalSkinID, key);
                }
            }
            
            foreach (var key in config.heli.waveConfig.Keys)
            {
                var permSuffix = config.heli.waveConfig[key].GiveItemCommand.ToLower();
                var perm = $"{Name.ToLower()}.{permSuffix}";
                permission.RegisterPermission(perm, this);
                if (WaveProfiles.Contains(config.heli.waveConfig[key].GiveItemCommand))
                {
                    PrintError($"ERROR: One or more of your Wave Profiles contains a duplicate 'Profile Shortname', these must be unique for each profile. Correct your config & reload.");
                    continue;
                }
                else
                {
                    WaveProfiles.Add(config.heli.waveConfig[key].GiveItemCommand);
                }

                if (HeliProfileCache.ContainsKey(config.heli.waveConfig[key].SkinId))
                {
                    PrintError($"ERROR: One or more of your Wave Profiles contains a duplicate 'Skin ID', these must be unique for each profile. Correct your config & reload.");
                    continue;
                }
                else
                {
                    HeliProfileCache.Add(config.heli.waveConfig[key].SkinId, key);
                }

                if (config.heli.waveConfig[key].WaveProfiles.Count == 0)
                {
                    // If loading fresh, populate default wave profiles
                    foreach (var profile in config.heli.heliConfig.Keys)
                        config.heli.waveConfig[key].WaveProfiles.Add(profile);  
                    
                    SaveConfig();
                }
            }
        }

        private static ConsoleSystem.Arg RunSilentCommand(string strCommand, params object[] args)
        {
            var command = ConsoleSystem.BuildCommand(strCommand, args);
            var arg = new ConsoleSystem.Arg(ConsoleSystem.Option.Unrestricted, command);
            if (arg.Invalid || !arg.cmd.Variable) return null;
            arg.cmd.Call(arg);
            return arg;
        }

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

                if (Clans && config.options.useClans)
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
                        Player.Message(member, message, config.options.usePrefix ? config.options.chatPrefix : null, config.options.chatIcon);
                    }
                }
            }
        }

        private bool IsBP(ItemDefinition itemDef) => itemDef?.Blueprint != null && itemDef.Blueprint.isResearchable && !itemDef.Blueprint.defaultBlueprint;

        private bool CanPurchaseAnySignal()
        {
            foreach (var key in config.heli.heliConfig.Keys)
            {
                if (config.heli.heliConfig[key].UseBuyCommand && config.purchasing.defaultCurrency != "Custom")
                    return true;
            }
            return false;
        }

        private void SetDamageScale(TimedExplosive rocket, float scale)
        {
            foreach (DamageTypeEntry damageType in rocket.damageTypes)
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

        private object CheckAndFixSignal(Item signal)
        {
            if (signal == null)
                return null;
            
            string heliProfile;
            ulong skinId = signal.skin;
            if (HeliProfileCache.TryGetValue(skinId, out heliProfile))
            {
                signal.name = heliProfile;
                signal.skin = skinId;
                signal.MarkDirty();
            }
            return signal;
        }

        private void RemoveHeliOwner(BaseNetworkable entity, float time)
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

        private void RetireHeli(PatrolHelicopterAI heliAI, ulong heliId, float retireTime = 1f)
        {
            timer.Once(retireTime, () =>
            {
                if (heliAI != null)
                {
                    HeliComponent heliComp = heliAI.helicopterBase.GetComponent<HeliComponent>();
                    if (heliComp != null)
                        heliComp.isRetiring = true;

                    heliAI.Retire();
                }
            });
        }

        private bool GiveHeliSignal(BasePlayer player, ulong skinId, string dropName, int dropAmount, string reason)
        {
            if (player != null && player.IsAlive())
            {
                Item heliDrop = ItemManager.CreateByItemID(supplySignalId, dropAmount, skinId);
                heliDrop.name = dropName;
                if (player.inventory.GiveItem(heliDrop))
                    return true;

                heliDrop.Remove(0);
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

        #endregion Helpers

        #region Commands

        private void CmdReport(IPlayer player, string command, string[] args)
        {
            string activeHelis = String.Empty;
            int count = 0;
            int total = HeliSignalData.Count;

            if (total == 0)
            {
                Message(player, "HeliReportTitleCon", "NO");
                return;
            }

            if (player.IsServer)
                Message(player, "HeliReportTitleCon", total);
            else
                Message(player, "HeliReportTitle", total);

            foreach (var netId in HeliSignalData.Keys)
            {
                var heli = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as PatrolHelicopter;
                if (heli != null)
                {
                    if (!IsOwnerOrFriend(heli.OwnerID, UInt64.Parse(player.Id)) && !permission.UserHasPermission(player.Id, permAdmin))
                        continue;

                    count++;

                    Vector3 position = heli.transform.position;
                    var gridPos = PositionToGrid(position);
                    if (gridPos == null)
                        gridPos = "Unknown";

                    HeliComponent heliComp = heli.GetComponent<HeliComponent>();
                    if (heliComp == null)
                        continue;

                    BasePlayer owner = heliComp.owner;
                    if (owner == null)
                        continue;
                    
                    string heliProfile = heliComp.heliProfile;
                    if (heliProfile == null)
                        continue;

                    var message = String.Empty;
                    if (player.IsServer)
                        message = Lang("HeliReportItemCon", player.Id, count, total, config.heli.heliConfig[heliProfile].HeliName, owner.displayName, gridPos,
                                    Math.Round((decimal)heli.health, 0), Math.Round((decimal)heli.weakspots[0].health, 0), Math.Round((decimal)heli.weakspots[1].health, 0), heliComp.heliAI._currentState);
                    else
                        message = Lang("HeliReportItem", player.Id, count, total, config.heli.heliConfig[heliProfile].HeliName, owner.displayName, gridPos,
                                    Math.Round((decimal)heli.health, 0), Math.Round((decimal)heli.weakspots[0].health, 0), Math.Round((decimal)heli.weakspots[1].health, 0), heliComp.heliAI._currentState);
                    
                    activeHelis += ($"{message}");
                    message = String.Empty;
                }
            }

            if (config.options.usePrefix)
            {
                config.options.usePrefix = false;
                Message(player, "HeliReportList", activeHelis);
                config.options.usePrefix = true;
            }
            else
            {
                Message(player, "HeliReportList", activeHelis);
            }
            activeHelis = String.Empty;
        }

        private void CmdRetireHeli(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
            {
                Message(player, "InGameOnly");
                return;
            }

            BasePlayer basePlayer = FindPlayer(player.Id)?.Object as BasePlayer;
            if (basePlayer == null) return;

            bool didRetireAny = false;
            foreach (var netId in HeliSignalData.Keys)
            {
                var heli = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as PatrolHelicopter;
                if (heli == null) continue;

                var heliComp = heli.GetComponent<HeliComponent>();
                if (heliComp == null) continue;

                if (heli.OwnerID == basePlayer.userID || (config.heli.canTeamRetire && IsOwnerOrFriend(heli.OwnerID, basePlayer.userID)))
                {
                    didRetireAny = true;
                    heliComp.isRetiring = true;
                    heliComp.retireCmdUsed = true;
                    heli.myAI.Retire();
                }
            }

            if (didRetireAny)
                Message(player, "RetiredHelis");
            else
                Message(player, "NoRetiredHelis");
        }
    
        private void CmdBuySignal(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
            {
                Message(player, "InGameOnly");
                return;
            }
            else if (args?.Length < 1 || args?.Length > 1)
            {
                string buyHelis = String.Empty;
                foreach (var key in config.heli.heliConfig.Keys)
                {
                    if (config.heli.heliConfig[key].UseBuyCommand)
                        buyHelis += $"{config.purchasing.buyCommand} {config.heli.heliConfig[key].GiveItemCommand}\n";
                }
                buyHelis += ($"<color=green>---------------------------------------------</color>\n");

                string buyWaves = String.Empty;
                foreach (var key in config.heli.waveConfig.Keys)
                {
                    if (config.heli.waveConfig[key].UseBuyCommand)
                        buyWaves += $"{config.purchasing.buyCommand} {config.heli.waveConfig[key].GiveItemCommand}\n";
                }

                Message(player, "BuyCmdSyntax", buyHelis, buyWaves);
                return;
            }

            string currencyItem = config.purchasing.defaultCurrency;
            string priceFormat;
            string priceUnit;

            if (args?[0].ToLower() == "list")
            {
                string list = String.Empty;
                foreach (var key in config.heli.heliConfig.Keys)
                {
                    switch (currencyItem)
                    {
                        case "ServerRewards":
                            {
                                priceFormat = $"{config.heli.heliConfig[key].CostToBuy} {config.purchasing.purchaseUnit}";
                            }
                            break;
                        case "Economics":
                            {
                                priceFormat = $"{config.purchasing.purchaseUnit}{config.heli.heliConfig[key].CostToBuy}";
                            }
                            break;
                        default:
                            {
                                priceFormat = $"{config.heli.heliConfig[key].CostToBuy} {config.purchasing.customCurrency[0].DisplayName}";
                            }
                            break;
                    }
                    if (config.heli.heliConfig[key].UseBuyCommand) list += ($"{config.heli.heliConfig[key].HeliName} : {priceFormat}\n");
                }

                list += ($"<color=green>---------------------------------------------</color>\n");

                foreach (var key in config.heli.waveConfig.Keys)
                {
                    switch (currencyItem)
                    {
                        case "ServerRewards":
                            {
                                priceFormat = $"{config.heli.waveConfig[key].CostToBuy} {config.purchasing.purchaseUnit}";
                            }
                            break;
                        case "Economics":
                            {
                                priceFormat = $"{config.purchasing.purchaseUnit}{config.heli.waveConfig[key].CostToBuy}";
                            }
                            break;
                        default:
                            {
                                priceFormat = $"{config.heli.waveConfig[key].CostToBuy} {config.purchasing.customCurrency[0].DisplayName}";
                            }
                            break;
                    }
                    if (config.heli.waveConfig[key].UseBuyCommand) list += ($"{key} : {priceFormat}\n");
                }

                Message(player, "PriceList", list);
                return;
            }

            string type = args[0].ToLower();
            ulong skinId = 0;
            string heliProfile = string.Empty;
            bool isWaveHeli = false;

            if (!HeliProfiles.Contains(type) && !WaveProfiles.Contains(type))
            {
                Message(player, "InvalidDrop", type);
                return;
            }

            if (!Instance.permission.UserHasPermission(player.Id, permBuy))
            {
                Message(player, "BuyPermission", type);
                return;
            }

            foreach (var key in config.heli.heliConfig.Keys)
            {
                if (type == config.heli.heliConfig[key].GiveItemCommand.ToLower())
                {
                    skinId = config.heli.heliConfig[key].SignalSkinID;
                    heliProfile = key;
                    break;
                }
            }
            foreach (var key in config.heli.waveConfig.Keys)
            {
                if (type == config.heli.waveConfig[key].GiveItemCommand.ToLower())
                {
                    skinId = config.heli.waveConfig[key].SkinId;
                    heliProfile = key;
                    isWaveHeli = true;
                    break;
                }
            }

            if (isWaveHeli && !config.heli.waveConfig[heliProfile].UseBuyCommand)
            {
                Message(player, "NoBuy", type);
                return;
            }
            else if (!isWaveHeli && !config.heli.heliConfig[heliProfile].UseBuyCommand)
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
                        var cost = isWaveHeli ? config.heli.waveConfig[heliProfile].CostToBuy : config.heli.heliConfig[heliProfile].CostToBuy;
                        var balance = Interface.CallHook("CheckPoints", (ulong)basePlayer.userID);
                        if (balance != null)
                        {
                            if (cost <= Convert.ToInt32(balance))
                            {
                                if (GiveHeliSignal(basePlayer, skinId, heliProfile, 1, "purchase"))
                                {
                                    Interface.CallHook("TakePoints", (ulong)basePlayer.userID, cost);
                                    Message(player, "Receive", 1, heliProfile);
                                    return;
                                }
                                else
                                {
                                    Message(player, "FullInventory", heliProfile);
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
                        var cost = Convert.ToDouble(isWaveHeli ? config.heli.waveConfig[heliProfile].CostToBuy : config.heli.heliConfig[heliProfile].CostToBuy);
                        var balance = Interface.CallHook("Balance", (ulong)basePlayer.userID);
                        if (balance != null)
                        {
                            if (cost <= Convert.ToDouble(balance))
                            {
                                if (GiveHeliSignal(basePlayer, skinId, heliProfile, 1, "purchase"))
                                {
                                    Interface.CallHook("Withdraw", (ulong)basePlayer.userID, cost);
                                    Message(player, "Receive", 1, heliProfile);
                                    return;
                                }
                                else
                                {
                                    Message(player, "FullInventory", heliProfile);
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
                        var cost = isWaveHeli ? config.heli.waveConfig[heliProfile].CostToBuy : config.heli.heliConfig[heliProfile].CostToBuy;
                        int balance = 0;

                        ItemDefinition itemDef = ItemManager.FindItemDefinition(shortName);
                        ItemContainer[] inventories = { basePlayer.inventory.containerMain, basePlayer.inventory.containerBelt };
                        for (int i = 0; i < inventories.Length; i++)
                        {
                            foreach (var item in inventories[i].itemList)
                            {
                                if (item.info.shortname == shortName && item.skin == currencySkin)
                                {
                                    balance += item.amount;
                                    if (cost <= balance)
                                    {
                                        if (GiveHeliSignal(basePlayer, skinId, heliProfile, 1, "purchase"))
                                        {
                                            basePlayer.inventory.Take(null, itemDef.itemid, cost);
                                            Message(player, "Receive", 1, heliProfile);
                                        }
                                        else
                                        {
                                            Message(player, "FullInventory", heliProfile);
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

        private void CmdGiveSignal(IPlayer player, string command, string[] args)
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
            string heliProfile = string.Empty;
            foreach (var item in config.heli.heliConfig.Keys)
            {
                if (type == config.heli.heliConfig[item].GiveItemCommand.ToLower())
                {
                    skinId = config.heli.heliConfig[item].SignalSkinID;
                    heliProfile = item;
                    break;
                }
            }
            foreach (var item in config.heli.waveConfig.Keys)
            {
                if (type == config.heli.waveConfig[item].GiveItemCommand.ToLower())
                {
                    skinId = config.heli.waveConfig[item].SkinId;
                    heliProfile = item;
                    break;
                }
            }

            if (skinId == 0)
            {
                Message(player, "InvalidDrop", type);
                return;
            }

            if (GiveHeliSignal(target, skinId, heliProfile, dropAmount, "give"))
            {
                Message(target, "Receive", dropAmount, heliProfile);
                Message(player, "PlayerReceive", target.displayName, target.userID, dropAmount, heliProfile);
            }
            else
            {
                Message(player, "PlayerFull", heliProfile, target);
                return;
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
        
        private class HeliSignalComponent : MonoBehaviour
        {
            public SupplySignal signal;
            public BasePlayer player;
            public Vector3 position;
            public ulong skinId;
            public string heliProfile;
            public int heliAmount;
            public bool isWaveHeli;
            public string waveProfile;
            public List<string> waveProfileCache;

            void Start()
            {
                Invoke(nameof(CustomExplode), config.options.signalFuseLength);
            }

            void CustomExplode()
            {
                position = signal.transform.position;
                var playerId = player.userID;
                if (SignalAborted())
                {
                    signal.Kill();
                    if (player != null || !player.IsAlive())
                    {
                        Instance.NextTick (()=> Instance.GiveHeliSignal(player, skinId, heliProfile, 1, "refund"));

                        if (config.heli.playerCooldown > 0f)
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
                    float finishUp = config.options.smokeDuration;
                    if (finishUp == null || finishUp < 0) finishUp = 210f; // Rust default smoke duration
                    signal.Invoke(new Action(signal.FinishUp), finishUp);
                    signal.SetFlag(BaseEntity.Flags.On, true);
                    signal.SendNetworkUpdateImmediate(false);

                    var gridPos = Instance.PositionToGrid(position);
                    if (gridPos == null)
                        gridPos = "Unknown";

                    var heliDisplayName = isWaveHeli ? waveProfile : config.heli.heliConfig[heliProfile].HeliName;

                    if (config.announce.callChat)
                    {
                        var message = Instance.Lang("HeliCalled", player.UserIDString, player.displayName, heliDisplayName, gridPos);
                        Instance.AnnounceToChat(player, message);
                    }

                    if (config.discord.sendHeliCall)
                    {
                        string discordMsg = string.Format(Instance.lang.GetMessage("DiscordCall", Instance, player.UserIDString), player.displayName, heliDisplayName, gridPos);
                        Instance.SendToDiscord(discordMsg);
                    }

                    var WaveTime = Time.realtimeSinceStartup;
                    if (isWaveHeli) WavesCalled.Add(WaveTime, new List<ulong>());
                    var arrivalPosition = position + new Vector3(0, config.heli.arrivalHeight, 0);
                    for (int i = 0; i < heliAmount; i++)
                    {
                        PatrolHelicopter heli = GameManager.server.CreateEntity(heliPrefab, new Vector3(), new Quaternion(), true) as PatrolHelicopter;
                        heli.OwnerID = playerId;
                        heli.skinID = skinId;
                        heli._name = heliProfile;
                        heli.Spawn();

                        // Spawn then move the heli at a random location from the called position
                        // depending on the mapScaleDistance in config.
                        float size = TerrainMeta.Size.x * config.heli.mapScaleDistance;
                        Vector2 rand = Random.insideUnitCircle.normalized;
                        Vector3 pos = new Vector3(rand.x, 0, rand.y);
                        pos *= size;
                        pos += arrivalPosition + new Vector3(0f, config.heli.spawnHeight, 0f);
                        heli.transform.position = pos;
                        
                        var heliId = heli.net.ID.Value;
                        if (isWaveHeli) WavesCalled[WaveTime].Add(heliId);

                        Instance.NextTick(() =>
                        {
                            // Calling on NextTick to stop issues with AlphaLoot and other plugins
                            // which alter Heli settings on entity spawn
                            PatrolHelicopterAI heliAI = heli.myAI;
                            heliAI._targetList.Add(new PatrolHelicopterAI.targetinfo(player, player));

                            var heliComp = heli.gameObject.AddComponent<HeliComponent>();
                            heliComp.heli = heli;
                            heliComp.heliAI = heliAI;
                            heliComp.owner = player;
                            heliComp.skinId = skinId;
                            heliComp.heliProfile = heliProfile;
                            heliComp.calledPosition = position;
                            heliComp.isWaveHeli = isWaveHeli;
                            heliComp.waveProfileCache = waveProfileCache;

                            heliAI.hasInterestZone = true;
                            heliAI.interestZoneOrigin = arrivalPosition;
                            heliAI.ExitCurrentState();
                            heliAI.State_Move_Enter(arrivalPosition);
                            
                            HeliSignalData.Add(heliId, new HeliStats());
                            HeliSignalData[heliId].OwnerID = playerId;
                            HeliSignalData[heliId].OwnerName = player.displayName;

                            var despawnTime = config.heli.heliConfig[heliProfile].DespawnTime;
                            if (despawnTime == null) return;
                            if (despawnTime > 0)
                                Instance.RetireHeli(heliAI, heliId, despawnTime);
                        });
                    }
                }
                DestroyImmediate(this);
            }

            public bool SignalAborted()
            {
                if (player == null || !player.IsAlive())
                    return true;

                if (config.heli.globalLimit > 0 && !Instance.permission.UserHasPermission(player.UserIDString, permAdmin))
                {
                    int heliCount = 0;
                    foreach (var netId in HeliSignalData.Keys)
                    {
                        var heli = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as PatrolHelicopter;
                        if (heli != null)
                            heliCount++;
                    }
                    if ((heliCount + heliAmount) >= config.heli.globalLimit)
                    {
                        Instance.Message(player, "GlobalLimit", config.heli.globalLimit);
                        return true;
                    }
                }

                if (config.heli.playerLimit > 0 && !Instance.permission.UserHasPermission(player.UserIDString, permAdmin))
                {
                    int heliCount = 0;
                    foreach (var key in HeliSignalData.Keys)
                    {
                        if (HeliSignalData[key].OwnerID == player.userID) 
                            heliCount++;
                    }
                    if ((heliCount + heliAmount) > config.heli.playerLimit)
                    {
                        Instance.Message(player, "PlayerLimit", config.heli.playerLimit);
                        return true;
                    }
                }

                if (Instance.IsInSafeZone(position))
                {
                    Instance.Message(player, "InSafeZone", heliProfile);
                    return true;
                }
                
                if (!config.heli.allowMonuments)
                {
                    var monument = TerrainMeta.Path.FindClosest<MonumentInfo>(Monuments, position);
                    if (monument == null)
                        return false;
                    
                    float dist = Vector3.Distance(monument.ClosestPointOnBounds(position), position);
                    if (config.heli.blockedMonuments.Contains(monument.name) && dist < config.heli.distFromMonuments)
                    {
                        Instance.Message(player, "InNamedMonument", heliProfile, monument.displayPhrase.translated);
                        return true;
                    }
                }
                return false;
            }
        }

        private class HeliComponent : MonoBehaviour
        {
            public string heliProfile;
            public BasePlayer owner;
            public ulong skinId;
            public PatrolHelicopterAI heliAI;
            public PatrolHelicopter heli;
            public Vector3 calledPosition;
            public Vector3 arrivalPosition;
            public bool isDying = false;
            public bool isReturning = false;
            public bool isRetiring = false;
            public Vector3 strafePosition = Vector3.zero;
            public BasePlayer strafeTarget;
            public bool isStrafing = false;
            public bool isOrbitStrafing = false;
            public bool isStrafingTurret = false;
            public float timeSinceSeen = 0f;
            public bool isTeamDead = false;
            public ulong heliId;
            public bool isWaveHeli;
            public List<string> waveProfileCache;
            public List<BasePlayer> callingTeam = new List<BasePlayer>();
            public bool retireCmdUsed = false;
            public float turretCooldown = 0f;
            public FieldInfo useNapalm;
            private float lastUpdateTargets;
            private float lastPositionCheck;
            private float lastReturnCheck;
            private float lastStrafeThink;
            private float lastUpdateHeliInfo;


            void Start()
            {
                useNapalm = typeof(PatrolHelicopterAI).GetField("useNapalm", (BindingFlags.Instance | BindingFlags.NonPublic));
                turretCooldown = Time.realtimeSinceStartup;
                var startTime = Time.realtimeSinceStartup;
                lastUpdateTargets = startTime;
                lastPositionCheck = startTime;
                lastReturnCheck = startTime;
                lastStrafeThink = startTime;
                lastUpdateHeliInfo = startTime;
                heliId = heli.net.ID.Value;
                arrivalPosition = calledPosition + new Vector3(0, config.heli.arrivalHeight, 0);
                isReturning = true;
                GetCallingTeam();
                SetupHeli();
            }

            void Update()
            {
                if (heli == null)
                    return;
                
                if (heliAI._currentState == PatrolHelicopterAI.aiState.DEATH || heliAI.isDead)
                    isDying = true;

                if (heliAI.isRetiring)
                    isRetiring = true;

                StrafeThink();
                UpdateTargetList();
                PositionCheck();
                ReturnHeliToPlayer();
                UpdateHeliInfo();
            }
            
            void UpdateHeliInfo()
            {
                if (Time.realtimeSinceStartup - lastUpdateHeliInfo > 1f)
                {
                    lastUpdateHeliInfo = Time.realtimeSinceStartup;
                    heli.OwnerID = owner.userID;
                    heli._name = heliProfile;
                    heli.skinID = skinId;
                }
            }
            
            void SetupHeli()
            {
                Instance.NextTick(() =>
                {
                    heli._maxHealth = config.heli.heliConfig[heliProfile].Health;
                    heli.startHealth = heli._maxHealth;
                    heli.InitializeHealth(heli.startHealth, heli.startHealth);
                    heli.weakspots[0].maxHealth = config.heli.heliConfig[heliProfile].MainRotorHealth;
                    heli.weakspots[1].maxHealth = config.heli.heliConfig[heliProfile].TailRotorHealth;
                    heli.weakspots[0].health = config.heli.heliConfig[heliProfile].MainRotorHealth;
                    heli.weakspots[1].health = config.heli.heliConfig[heliProfile].TailRotorHealth;
                    heli.maxCratesToSpawn = config.heli.heliConfig[heliProfile].CratesToSpawn;
                    heli.bulletDamage = config.heli.heliConfig[heliProfile].BulletDamage;
                    heli.bulletSpeed = config.heli.heliConfig[heliProfile].BulletSpeed;

                    heliAI.maxSpeed = config.heli.heliConfig[heliProfile].InitialSpeed;
                    heliAI.maxRotationSpeed = config.heli.heliConfig[heliProfile].MaxRotationSpeed;
                    var dist = Vector3Ex.Distance2D(heliAI.transform.position, heliAI.destination);
                    heliAI.GetThrottleForDistance(dist);
                    heliAI.leftGun.fireRate = config.heli.heliConfig[heliProfile].GunFireRate;
                    heliAI.rightGun.fireRate = heliAI.leftGun.fireRate;
                    heliAI.leftGun.burstLength = config.heli.heliConfig[heliProfile].BurstLength;
                    heliAI.rightGun.burstLength = heliAI.leftGun.burstLength;
                    heliAI.leftGun.timeBetweenBursts = config.heli.heliConfig[heliProfile].TimeBetweenBursts;
                    heliAI.rightGun.timeBetweenBursts = heliAI.leftGun.timeBetweenBursts;
                    heliAI.leftGun.maxTargetRange = config.heli.heliConfig[heliProfile].MaxTargetRange;
                    heliAI.rightGun.maxTargetRange = heliAI.leftGun.maxTargetRange;
                    heliAI.leftGun.loseTargetAfter = 8f;
                    heliAI.rightGun.loseTargetAfter = 8f;
                    heliAI.timeBetweenRockets = config.heli.heliConfig[heliProfile].TimeBetweenRockets;
                    heliAI.interestZoneOrigin = arrivalPosition;
                    heliAI.hasInterestZone = true;
                    heliAI.lastStrafeTime = Time.realtimeSinceStartup;
                    heliAI.terrainPushForce = config.heli.heliConfig[heliProfile].TerrainPushForce; // default = 50f
                    heliAI.obstaclePushForce = config.heli.heliConfig[heliProfile].ObstaclePushForce; // default = 20f

                    heli.SendNetworkUpdateImmediate(true);
                    heli.UpdateNetworkGroup();
                });
            }
            
            public void PositionCheck()
            {
                if (!isReturning)
                    return;

                if (Time.realtimeSinceStartup - lastPositionCheck > 0.2f)
                {
                    lastPositionCheck = Time.realtimeSinceStartup;
                    if (heli != null || heliAI != null)
                    {
                        if (heliAI._currentState == PatrolHelicopterAI.aiState.DEATH || heliAI.isRetiring)
                            return;

                        float distance = Vector3Ex.Distance2D(heli.transform.position, heliAI.destination);
                        if (distance < (config.heli.heliConfig[heliProfile].OrbitRadius/2))
                        {
                            isReturning = false;
                            heliAI.maxSpeed = config.heli.heliConfig[heliProfile].MaxSpeed;
                            heliAI.ExitCurrentState();
                            heliAI.SetIdealRotation(heliAI.GetYawRotationTo(heliAI.destination), -1f);
                            if (config.heli.heliConfig[heliProfile].MaxOrbitDuration > 0)
                                heliAI.maxOrbitDuration = config.heli.heliConfig[heliProfile].MaxOrbitDuration;
                            
                            heliAI.State_Orbit_Enter(config.heli.heliConfig[heliProfile].OrbitRadius);
                        }
                    }
                }
            }
            
            public void UpdateTargetList()
            {
                if (isReturning || isRetiring || isDying)
                    return;

                if (Time.realtimeSinceStartup - lastUpdateTargets > 1.0f)
                {
                    lastUpdateTargets = Time.realtimeSinceStartup;
                    if (heliAI.isRetiring)
                    {
                        isRetiring = true;
                        return;
                    }
                    else if (heliAI.isDead)
                    {
                        isDying = true;
                        return;
                    }

                    if (config.heli.retireOnKilled)
                    {
                        foreach (var member in callingTeam)
                        {
                            if (member.IsAlive())
                            {
                                isTeamDead = false;
                                break;
                            }
                            isTeamDead = true;
                        }

                        if (isTeamDead)
                        {
                            isRetiring = true;
                            heliAI._targetList.Clear();
                            heliAI.Retire();
                        }
                    }

                    for (int i = 0; i < heliAI._targetList.Count; i++)
                    {
                        PatrolHelicopterAI.targetinfo item = heliAI._targetList[i];
                        BasePlayer player = item?.ply;
                        if (item == null || item.ent == null || player == null || !player.IsAlive())
                        {
                            heliAI._targetList.Remove(item);
                            continue;
                        }

                        if (config.heli.heliConfig[heliProfile].OwnerDamage && !callingTeam.Contains(player))
                        {
                            heliAI._targetList.Remove(item);
                            continue;
                        }
                        else
                        {
                            timeSinceSeen = item.TimeSinceSeen();
                            if (timeSinceSeen.ToString() == "-Infinity") // Target never seen, so don't enter strafe
                                continue;

                            if (!heliAI.PlayerVisible(player))
                            {
                                if (timeSinceSeen >= 6f && player.IsAlive())
                                {
                                    if (heliAI.CanStrafe() && heliAI.IsAlive() && !isStrafing && !player.IsDead())
                                    {
                                        if (player == heliAI.leftGun._target || player == heliAI.rightGun._target)
                                        {
                                            isStrafing = true;
                                            strafePosition = player.transform.position;
                                            strafeTarget = player;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                item.lastSeenTime = Time.realtimeSinceStartup;
                            }
                        }
                    }

                    this.AddNewTargetsToList();
                    if (isStrafing)
                    {
                        if (heliAI._currentState == PatrolHelicopterAI.aiState.STRAFE || heliAI._currentState == PatrolHelicopterAI.aiState.ORBITSTRAFE)
                            return;
                        else if (strafeTarget == null)
                            return;

                        heliAI.ExitCurrentState();
                        heliAI.State_Strafe_Enter(strafeTarget, UseNapalm());
                    }
                }
            }

            private void AddNewTargetsToList()
            {
                foreach (BasePlayer basePlayer in BasePlayer.activePlayerList)
                {
                    if (basePlayer.InSafeZone() || Vector3Ex.Distance2D(heli.transform.position, basePlayer.transform.position) > config.heli.heliConfig[heliProfile].NewTargetRange)
                        continue;
                    else if (config.heli.heliConfig[heliProfile].OwnerDamage && !callingTeam.Contains(basePlayer))
                        continue;

                    var noTarget = false;
                    foreach (PatrolHelicopterAI.targetinfo _targetinfo in heliAI._targetList)
                    {
                        if (_targetinfo.ply != basePlayer)
                            continue;
                        
                        noTarget = true;
                        break;
                    }

                    if (noTarget || basePlayer.GetThreatLevel() <= 0.5f || !heliAI.PlayerVisible(basePlayer))
                        continue;

                    heliAI._targetList.Add(new PatrolHelicopterAI.targetinfo(basePlayer, basePlayer));
                }
            }
            
            public void StrafeThink()
            {
                if (isDying || isRetiring)
                    return;

                if (Time.realtimeSinceStartup - lastStrafeThink > (config.heli.heliConfig[heliProfile].TimeBetweenRockets / 2))
                {
                    lastStrafeThink = Time.realtimeSinceStartup;
                    switch (heliAI._currentState)
                    {
                        case PatrolHelicopterAI.aiState.STRAFE:
                        {
                            isStrafing = true;
                            if (strafeTarget != null)
                            {
                                heliAI.strafe_target = strafeTarget;
                                heliAI.strafe_target_position = strafeTarget.transform.position;
                                heliAI.lastStrafeTime = Time.realtimeSinceStartup;
                            }

                            if (heliAI.ClipRocketsLeft() <= 1)
                            {
                                if (Random.Range(0f, 1f) <= config.heli.heliConfig[heliProfile].OrbitStrafeChance)
                                {
                                    isOrbitStrafing = true;
                                    heliAI.ExitCurrentState();
                                    heliAI.State_OrbitStrafe_Enter();
                                    var rocketVariance = Random.Range(config.heli.heliConfig[heliProfile].MinOrbitMultiplier,
                                                                                config.heli.heliConfig[heliProfile].MaxOrbitMultiplier);
                                    
                                    heliAI.numRocketsLeft = config.heli.heliConfig[heliProfile].MaxOrbitRockets + rocketVariance;
                                    UpdateSerializeableFields(heliAI.numRocketsLeft);
                                    return;
                                }
                                heliAI.ExitCurrentState();
                                heliAI.State_Move_Enter(heliAI.GetAppropriatePosition(heliAI.strafe_target_position + (heli.transform.forward * 120f), 20f, 30f));
                            }
                            return;
                        }
                        case PatrolHelicopterAI.aiState.ORBITSTRAFE:
                        {
                            isStrafing = true;
                            if (strafeTarget != null)
                            {
                                heliAI.strafe_target = strafeTarget;
                                heliAI.strafe_target_position = strafeTarget.transform.position;
                                heliAI.lastStrafeTime = Time.realtimeSinceStartup;
                            }

                            if (!isOrbitStrafing)
                            {
                                // Prevent or allow Orbit strafe initiated by vanilla game code according to config chance
                                if (Oxide.Core.Random.Range(0f, 1f) <= config.heli.heliConfig[heliProfile].OrbitStrafeChance)
                                {
                                    isOrbitStrafing = true;
                                    var rocketVariance = Random.Range(config.heli.heliConfig[heliProfile].MinOrbitMultiplier,
                                                                                config.heli.heliConfig[heliProfile].MaxOrbitMultiplier);
                                    
                                    heliAI.numRocketsLeft = config.heli.heliConfig[heliProfile].MaxOrbitRockets + rocketVariance;
                                    UpdateSerializeableFields(heliAI.numRocketsLeft);
                                    return;
                                }
                                heliAI.ExitCurrentState();
                                heliAI.State_Move_Enter(heliAI.GetAppropriatePosition(heliAI.strafe_target_position + (heli.transform.forward * 120f), 20f, 30f));
                            }
                            return;
                        }
                        default:
                        {
                            isStrafing = false;
                            isOrbitStrafing = false;
                            if (isStrafingTurret) turretCooldown = Time.realtimeSinceStartup;
                            isStrafingTurret = false;
                            return;
                        }
                    }
                }
            }

            public void CallNextWaveHeli(string waveHeliProfile)
            {
                if (!HeliProfiles.Contains(config.heli.heliConfig[waveHeliProfile].GiveItemCommand))
                {
                    Instance.PrintError($"ERROR: No such profile: {waveHeliProfile}, check config.");
                    return;
                }

                float WaveTime = Time.realtimeSinceStartup;
                WavesCalled.Add(WaveTime, new List<ulong>());
                for (int i = 0; i < config.heli.heliConfig[waveHeliProfile].HeliAmount; i++)
                {
                    PatrolHelicopter newHeli = GameManager.server.CreateEntity(heliPrefab, arrivalPosition, new Quaternion(), true) as PatrolHelicopter;
                    newHeli.OwnerID = owner.userID;
                    newHeli.skinID = heli.skinID;
                    newHeli._name = waveHeliProfile;
                    newHeli.Spawn();

                    // Calling on NextTick to stop issues with AlphaLoot and other plugins
                    // which alter Heli settings on entity spawn
                    float size = TerrainMeta.Size.x * config.heli.mapScaleDistance;
                    Vector2 rand = Random.insideUnitCircle.normalized;
                    Vector3 pos = new Vector3(rand.x, 0, rand.y);
                    pos *= size;
                    pos += arrivalPosition + new Vector3(0f, config.heli.spawnHeight, 0f);
                    newHeli.transform.position = pos;

                    Instance.NextTick(() =>
                    {
                        // Calling on NextTick to stop issues with AlphaLoot and other plugins
                        // which alter Heli settings on entity spawn
                        PatrolHelicopterAI newHeliAI = newHeli.myAI;
                        newHeliAI._targetList.Add(new PatrolHelicopterAI.targetinfo(owner, owner));
                        
                        var heliComp = newHeli.gameObject.AddComponent<HeliComponent>();
                        heliComp.heli = newHeli;
                        heliComp.heliAI = newHeliAI;
                        heliComp.owner = owner;
                        heliComp.heliProfile = waveHeliProfile;
                        heliComp.calledPosition = calledPosition;
                        heliComp.isWaveHeli = this.isWaveHeli;
                        heliComp.waveProfileCache = this.waveProfileCache;

                        newHeliAI.hasInterestZone = true;
                        newHeliAI.interestZoneOrigin = arrivalPosition;
                        newHeliAI.ExitCurrentState();
                        newHeliAI.State_Move_Enter(arrivalPosition);

                        var heliId = newHeli.net.ID.Value;
                        WavesCalled[WaveTime].Add(heliId);
                        HeliSignalData.Add(heliId, new HeliStats());
                        HeliSignalData[heliId].OwnerID = owner.userID;
                        HeliSignalData[heliId].OwnerName = owner.displayName;
                        
                        if (config.heli.heliConfig[waveHeliProfile].DespawnTime > 0)
                            Instance.RetireHeli(newHeliAI, heliId, config.heli.heliConfig[waveHeliProfile].DespawnTime);
                    });
                };
                var message = Instance.Lang("NextHeliCalled", owner.UserIDString, heliProfile, waveProfileCache[0]);
                Instance.AnnounceToChat(owner, message);
            }

            public void ReturnHeliToPlayer()
            {
                if (!config.heli.returnToPlayer)
                    return;
                
                if (isRetiring || isDying)
                    return;

                if (Time.realtimeSinceStartup - lastReturnCheck > 2.0f)
                {
                    lastReturnCheck = Time.realtimeSinceStartup;
                    BasePlayer target = owner;
                    Vector3 returnPosition = new Vector3();
                    if (target == null)
                    {
                        returnPosition = calledPosition;
                    }
                    else if (!target.IsConnected || target.IsDead() || target.IsSleeping() || config.heli.returnToPosition)
                    {
                        returnPosition = calledPosition;
                    }
                    else
                    {
                        heliAI._targetList.Add(new PatrolHelicopterAI.targetinfo(target, target));
                        returnPosition = target.transform.position;
                    }

                    if (Vector3Ex.Distance2D(heliAI.transform.position, returnPosition) > config.heli.maxDistanceFromPlayer)
                    {
                        if (!config.heli.returnIfAttacking)
                        {
                            switch (heliAI._currentState)
                            {
                                case PatrolHelicopterAI.aiState.ORBIT:
                                    return;
                                case PatrolHelicopterAI.aiState.STRAFE:
                                    return;
                                case PatrolHelicopterAI.aiState.DEATH:
                                    isDying = true;
                                    return;
                                default:
                                    break;
                            }
                        }
                        isReturning = true;
                        heliAI.ExitCurrentState();
                        heliAI.State_Move_Enter(returnPosition + new Vector3(0, config.heli.arrivalHeight, 0));
                    }
                }
            }

            public void UpdateSerializeableFields(int rockets)
            {
                heliAI.numRocketsLeft = rockets;
                bool doNapalm = UseNapalm();
#if CARBON
                heliAI.useNapalm = doNapalm;
#else
                if (useNapalm != null)
                    useNapalm.SetValue(heliAI, (object)doNapalm);
#endif
            }

            public bool UseNapalm()
            {
                return (Oxide.Core.Random.Range(0f, 1f) <= config.heli.heliConfig[heliProfile].NapalmChance);
            }

            public object GetCallingTeam()
            {
                if (!callingTeam.Contains(owner))
                    callingTeam.Add(owner);

                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (Instance.IsOwnerOrFriend(player.userID, owner.userID) && !callingTeam.Contains(player))
                        callingTeam.Add(player);
                }
                return callingTeam;
            }

            private void RemoveHeliData()
            {
                Instance.timer.Once(10f, () =>
                {
                    if (HeliSignalData.ContainsKey(heliId))
                        HeliSignalData.Remove(heliId);
                });
            }

            private void OnDestroy()
            {
                if (this.isWaveHeli && this.waveProfileCache.Count > 0)
                {
                    float waveTime = 0;
                    foreach (var key in WavesCalled.Keys)
                    {
                        if (WavesCalled[key].Contains(heliId))
                        {
                            WavesCalled[key].Remove(heliId);
                            waveTime = key;
                        }
                    }

                    Instance.NextTick(()=>
                    {
                        if (WavesCalled[waveTime].Count() != 0)
                        {
                            this.RemoveHeliData();
                            return;
                        }
                        
                        this.waveProfileCache.RemoveAt(0);
                        if (this.waveProfileCache.Count < 1)
                        {
                            var message = Instance.Lang("WaveFinished", owner.UserIDString, heliProfile);
                            Instance.AnnounceToChat(owner, message);
                            this.RemoveHeliData();
                            return;
                        }

                        if (!isRetiring && !heliAI.isRetiring)
                            CallNextWaveHeli(this.waveProfileCache[0]);
                    });
                }

                if (!isRetiring && !heliAI.isRetiring)
                {
                    if (HeliSignalData.ContainsKey(heliId))
                        Instance.ProcessRewards(heliId, owner.userID, heliProfile);
                }
                
                if (!PatrolHelicopterAI.monument_crash)
                {
                    Instance.timer.Once(11f, ()=>
                    {
                        if (heli != null)
                            heli.Hurt(heli.health * 2f, DamageType.Generic, null, false);
                    });
                }
                this.RemoveHeliData();
            }
        }

        #endregion Monos
        
        #region Temporary Data

        private static Dictionary<ulong, HeliStats> HeliSignalData = new Dictionary<ulong, HeliStats>();
        private static Dictionary<ulong, float> PlayerCooldowns = new Dictionary<ulong, float>();
        private static Dictionary<ulong, Dictionary<string, float>> TierCooldowns = new Dictionary<ulong, Dictionary<string, float>>();
        private static Dictionary<ulong, ulong> LockedCrates = new Dictionary<ulong, ulong>();
        private static List<string> HeliProfiles = new List<string>();
        private static List<string> WaveProfiles = new List<string>();
        private static Dictionary<float, List<ulong>> WavesCalled = new Dictionary<float, List<ulong>>();
        private static List<MonumentInfo> Monuments = new List<MonumentInfo>();
        private static Dictionary<ulong, string> HeliProfileCache = new Dictionary<ulong, string>();

        private class HeliStats
        {
            public ulong OwnerID;
            public string OwnerName;
            public float FirstHitTime = 0f;
            public BasePlayer LastAttacker;
            public Dictionary<ulong, AttackersStats> Attackers = new Dictionary<ulong, AttackersStats>();
            public int WarningLevel = 0;
            public float PlayerDamage = 0f;
            public float TurretDamage = 0f;
            public AutoTurret LastTurretAttacker;
        }

        private class AttackersStats
        {
            public string Name;
            public float DamageDealt = 0f;
            public float TurretDamage = 0f;
            public int TotalHits = 0;
            public int RotorHits = 0;
        }

        #endregion Temporary Data

        #region Config
        
        private class HeliData
        {
            [JsonProperty(PropertyName = "Number of helicopters called to the player")]
            public int HeliAmount { get; set; }
            [JsonProperty(PropertyName = "Helicopter display name")]
            public string HeliName { get; set; }
            [JsonProperty(PropertyName = "Skin ID of the custom Supply Signal")]
            public ulong SignalSkinID { get; set; }
            [JsonProperty(PropertyName = "Profile shortname (for use in permission and give command)")]
            public string GiveItemCommand { get; set; }
            [JsonProperty(PropertyName = "Enable purchasing using custom currency via the buy command")]
            public bool UseBuyCommand { get; set; }
            [JsonProperty(PropertyName = "Cost to purchase (using buy command)")]
            public int CostToBuy { get; set; }
            [JsonProperty(PropertyName = "Starting health")]
            public float Health { get; set; }
            [JsonProperty(PropertyName = "Main rotor health")]
            public float MainRotorHealth { get; set; }
            [JsonProperty(PropertyName = "Tail rotor health")]
            public float TailRotorHealth { get; set; }
            [JsonProperty(PropertyName = "Initial Helicopter speed until it arrives at location")]
            public float InitialSpeed { get; set; }
            [JsonProperty(PropertyName = "Helicopter max speed (Default = 42)")]
            public float MaxSpeed { get; set; }
            [JsonProperty(PropertyName = "Distance from target when orbiting (Default = 75)")]
            public float OrbitRadius { get; set; }
            [JsonProperty(PropertyName = "Max orbit duration when Helicopter arrives at location (Default = 30)")]
            public float MaxOrbitDuration { get; set; }
            [JsonProperty(PropertyName = "Helicopter max rotation speed SCALE (Default = 1.0)")]
            public float MaxRotationSpeed { get; set; }
            [JsonProperty(PropertyName = "Terrain pushback force (Help stop faster helis glitch under map)")]
            public float TerrainPushForce { get; set; }
            [JsonProperty(PropertyName = "Obstacle pushback force (Help stop faster helis glitch through structures)")]
            public float ObstaclePushForce { get; set; }
            [JsonProperty(PropertyName = "Number of crates to spawn")]
            public int CratesToSpawn { get; set; }
            [JsonProperty(PropertyName = "Number of locked hackable crates to spawn")]
            public int LockedCratesToSpawn { get; set; }
            [JsonProperty(PropertyName = "Hack time for locked crate (seconds)")]
            public float HackSeconds { get; set; }
            [JsonProperty(PropertyName = "Locked crate despawn time (seconds)")]
            public float LockedCrateDespawn { get; set; }
            [JsonProperty(PropertyName = "Bullet damage (Default = 20)")]
            public float BulletDamage { get; set; }
            [JsonProperty(PropertyName = "Bullet speed (Default = 250)")]
            public int BulletSpeed { get; set; }
            [JsonProperty(PropertyName = "Gun fire rate (Default = 0.125)")]
            public float GunFireRate { get; set; }
            [JsonProperty(PropertyName = "Gun burst length (Default = 3)")]
            public float BurstLength { get; set; }
            [JsonProperty(PropertyName = "Time between bursts (Default = 3)")]
            public float TimeBetweenBursts { get; set; }
            [JsonProperty(PropertyName = "New target detection range (Default = 150)")]
            public float NewTargetRange { get; set; }
            [JsonProperty(PropertyName = "Max targeting range (Default = 300)")]
            public float MaxTargetRange { get; set; }
            [JsonProperty(PropertyName = "Weapon accuracy % (1 to 100)")]
            public float BulletAccuracy { get; set; }
            [JsonProperty(PropertyName = "Max number of rockets to fire (Default = 12)")]
            public int MaxHeliRockets { get; set; }
            [JsonProperty(PropertyName = "Time between rockets (Default = 0.2)")]
            public float TimeBetweenRockets { get; set; }
            [JsonProperty(PropertyName = "Rocket damage scale (Default = 1.0)")]
            public float RocketDamageScale { get; set; }
            [JsonProperty(PropertyName = "Napalm chance (Default = 0.75)")]
            public float NapalmChance { get; set; }
            [JsonProperty(PropertyName = "Orbit Strafe chance (Default = 0.4)")]
            public float OrbitStrafeChance { get; set; }
            [JsonProperty(PropertyName = "Number of rockets to fire during orbit strafe (Default = 12)")]
            public int MaxOrbitRockets { get; set; }
            [JsonProperty(PropertyName = "Minimum variance to number of rockets fired during orbit strafe (Default = -3)")]
            public int MinOrbitMultiplier { get; set; }
            [JsonProperty(PropertyName = "Maximum variance to number of rockets fired during orbit strafe (Default = 24)")]
            public int MaxOrbitMultiplier { get; set; }
            [JsonProperty(PropertyName = "Minimum time between strafe attacks")]
            public float StrafeCooldown { get; set; }
            [JsonProperty(PropertyName = "Despawn timer")]
            public float DespawnTime { get; set; }
            [JsonProperty(PropertyName = "Only owner can damage (and team if enabled)")]
            public bool OwnerDamage { get; set; }
            [JsonProperty(PropertyName = "Allow Helicopter to target other players")]
            public bool TargetOtherPlayers { get; set; }
            [JsonProperty(PropertyName = "Block damage to calling players bases")]
            public bool BlockOwnerDamage { get; set; }
            [JsonProperty(PropertyName = "Block damage to other players bases")]
            public bool BlockOtherDamage { get; set; }
            [JsonProperty(PropertyName = "Block damage to other players")]
            public bool BlockPlayerDamage { get; set; }
            [JsonProperty(PropertyName = "Block damage ALWAYS to entities in the protected prefab list")]
            public bool BlockProtectedList { get; set; }
            [JsonProperty(PropertyName = "Disable Heli gibs")]
            public bool KillGibs { get; set; }
            [JsonProperty(PropertyName = "Gibs too hot to mine time (Seconds)")]
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
            [JsonProperty(PropertyName = "XP issued when destroyed (if enabled)")]
            public double XPReward { get; set; }
            [JsonProperty(PropertyName = "Scrap amount issued when destroyed (if enabled)")]
            public int ScrapReward { get; set; }
            [JsonProperty(PropertyName = "Custom reward amount issued when destroyed (if enabled)")]
            public int CustomReward { get; set; }
            [JsonProperty(PropertyName = "Damage Threshold (Min damage player needs to contribute to get rewards)")]
            public float DamageThreshold { get; set; }
            [JsonProperty(PropertyName = "BotReSpawn profile to spawn at crash site (leave blank for not using)")]
            public string BotReSpawnProfile { get; set; }

            [JsonProperty(PropertyName = "Loot Options")]
            public LootOptions Loot { get; set; }
            [JsonProperty(PropertyName = "Extra Loot Options")]
            public ExtraLootOptions ExtraLoot { get; set; }
            [JsonProperty(PropertyName = "Locked Crate Loot Options")]
            public LockedCrateLootOptions LockedCrateLoot { get; set; }

            public class LootOptions
            {
                [JsonProperty(PropertyName = "Use custom loot table to override crate loot")]
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
        
        public class CustomRewardItem
        {
            [JsonProperty(PropertyName = "ShortName")]
            public string ShortName { get; set; }
            [JsonProperty(PropertyName = "SkinID")]
            public ulong SkinId { get; set; }

            // DEBUG: Remove this later
            [JsonProperty("Display Name")]
            public string GetDisplayName { get { return DisplayName; } set { DisplayName = value; } }
            public bool ShouldSerializeGetDisplayName() => false;
            // #######################

            [JsonProperty(PropertyName = "Custom Display Name (leave blank unless creating custom items)")]
            public string DisplayName { get; set; }
        }

        public class LootItem
        {
            [JsonProperty(PropertyName = "ShortName")]
            public string ShortName { get; set; }
            [JsonProperty(PropertyName = "Chance (0 - 100)")]
            public float Chance { get; set; }
            [JsonProperty(PropertyName = "Min Amount")]
            public int AmountMin { get; set; }
            [JsonProperty(PropertyName = "Max Amount")]
            public int AmountMax { get; set; }
            [JsonProperty(PropertyName = "SkinID")]
            public ulong SkinId { get; set; }

            // DEBUG: Remove this later
            [JsonProperty("Display Name")]
            public string GetDisplayName { get { return DisplayName; } set { DisplayName = value; } }
            public bool ShouldSerializeGetDisplayName() => false;
            // #######################

            [JsonProperty(PropertyName = "Custom Display Name (leave blank unless creating custom items)")]
            public string DisplayName { get; set; }
            [JsonProperty(PropertyName = "Blueprint Chance Instead of Item, 0 = disabled. (0 - 100)")]
            public float BlueprintChance { get; set; }
        }

        public class WaveData
        {
            [JsonProperty(PropertyName = "SkinID")]
            public ulong SkinId { get; set; }
            [JsonProperty(PropertyName = "Profile shortname (for use in permission and give command)")]
            public string GiveItemCommand { get; set; }
            [JsonProperty(PropertyName = "Enable purchasing using custom currency via the buy command")]
            public bool UseBuyCommand { get; set; }
            [JsonProperty(PropertyName = "Cost to purchase (using buy command)")]
            public int CostToBuy { get; set; }
            [JsonProperty(PropertyName = "Heli Wave Profile List (Helis Called in Order From Top to Bottom)")]
            public List<string> WaveProfiles { get; set; }
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
            [JsonProperty(PropertyName = "Patrol Helicopter Options")]
            public Heli heli { get; set; }
            
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
                [JsonProperty(PropertyName = "Allow Better NPC Bots to Guard Heli Crates (If Loaded)")]
                public bool useBetterNPC { get; set; }
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
                [JsonProperty(PropertyName = "Disable vanilla Patrol Helicopter")]
                public bool noVanillaHeli { get; set; }
                [JsonProperty(PropertyName = "Use This Plugin to Control Stacking/Combining Heli Signal Items")]
                public bool useStacking { get; set; }
                [JsonProperty(PropertyName = "Command to Show Details of Players Own Active Helis (Admin Perm Allows to See ALL Active Helis)")]
                public string reportCommand { get; set; }
            }

            public class Announce
            {
                [JsonProperty(PropertyName = "Announce When Player Calls a Patrol Helicopter in Chat")]
                public bool callChat { get; set; }
                [JsonProperty(PropertyName = "Announce Helicopter Kill in Chat")]
                public bool killChat { get; set; }
                [JsonProperty(PropertyName = "Announce When a Helicopter Retires in Chat")]
                public bool retireChat { get; set; }
                [JsonProperty(PropertyName = "Announce Damage Report in Chat")]
                public bool reportChat { get; set; }
                [JsonProperty(PropertyName = "Also Give Damage Report When Helicopter Retires")]
                public bool reportRetire { get; set; }
                [JsonProperty(PropertyName = "Announce Server/Vanilla Patrol Helicopter Kill in Chat")]
                public bool killVanilla { get; set; }
                [JsonProperty(PropertyName = "Announce Server/Vanilla Patrol Helicopter Damage Report in Chat")]
                public bool reportVanilla { get; set; }
                [JsonProperty(PropertyName = "Announce Server/Vanilla Patrol Helicopter Display Name")]
                public string vanillaName { get; set; }
                [JsonProperty(PropertyName = "Announce Server/Vanilla Patrol Helicopter Owner Name")]
                public string vanillaOwner { get; set; }
                [JsonProperty(PropertyName = "Max Number Players Displayed in Damage Report")]
                public int maxReported { get; set; }
                [JsonProperty(PropertyName = "Announcements Also go to Global Chat (false = Player/Team Only)")]
                public bool announceGlobal { get; set; }
            }

            public class Discord
            {
                [JsonProperty(PropertyName = "Discord WebHook URL")]
                public string webHookUrl { get; set; }
                [JsonProperty(PropertyName = "Announce to Discord When Helicopter is Called")]
                public bool sendHeliCall { get; set; }
                [JsonProperty(PropertyName = "Announce to Discord When Helicopter is Killed")]
                public bool sendHeliKill { get; set; }
                [JsonProperty(PropertyName = "Announce to Discord When Helicopter Retires")]
                public bool sendHeliRetire { get; set; }
            }

            public class Rewards
            {
                [JsonProperty(PropertyName = "Rewards Plugin (ServerRewards | Economics)")]
                public string rewardPlugin { get; set; }
                [JsonProperty(PropertyName = "Currency Unit Displayed e.g: RP | $")]
                public string rewardUnit { get; set; }
                [JsonProperty(PropertyName = "Enable Rewards")]
                public bool enableRewards { get; set; }
                [JsonProperty(PropertyName = "Share Rewards Between Players Above Damage Threshold")]
                public bool shareRewards { get; set; }
                [JsonProperty(PropertyName = "Plugin to Use For Awarding XP (SkillTree | XPerience)")]
                public string pluginXP { get; set; }
                [JsonProperty(PropertyName = "Enable XP Reward")]
                public bool enableXP { get; set; }
                [JsonProperty(PropertyName = "Share XP Between Players Above Damage Threshold")]
                public bool shareXP { get; set; }
                [JsonProperty(PropertyName = "Award XP Including Players Existing Boosts")]
                public bool boostXP { get; set; }
                [JsonProperty(PropertyName = "Enable Scrap Reward")]
                public bool enableScrap { get; set; }
                [JsonProperty(PropertyName = "Share Scrap Between Players Above Damage Threshold")]
                public bool shareScrap { get; set; }
                [JsonProperty(PropertyName = "Enable Custom Reward Currency")]
                public bool enableCustomReward { get; set; }
                [JsonProperty(PropertyName = "Share Custom Reward Between Players Above Damage Threshold")]
                public bool shareCustomReward { get; set; }
                [JsonProperty(PropertyName = "Custom Reward Currency Item")]
                public CustomRewardItem customRewardItem { get; set; }
                [JsonProperty(PropertyName = "Rewards multipliers by permission")]
                public Hash<string, float> rewardMultipliers { get; set; }

                [JsonIgnore]
                public Permission permission;
                public void RegisterPermissions(Permission permission, HeliSignals plugin)
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

            public class Heli
            {
                [JsonProperty(PropertyName = "Player Give Up and Retire Command (Retires All of That Players Helis, NO Refund Given)")]
                public string retireCommand { get; set; }
                [JsonProperty(PropertyName = "Team Can Retire Helis Using the Command (Requires Use Friends/Clans/Teams option)")]
                public bool canTeamRetire { get; set; }
                [JsonProperty(PropertyName = "Global Helicopter Limit (0 = No Limit)")]
                public int globalLimit { get; set; }
                [JsonProperty(PropertyName = "Player Helicopter Limit (0 = No Limit)")]
                public int playerLimit { get; set; }
                [JsonProperty(PropertyName = "Allow Helicopter to crash at nearest monument (Sets server ConVar: 'patrolhelicopterai.monument_crash')")]
                public bool canMonumentCrash { get; set; }
                [JsonProperty(PropertyName = "Allow Helicopter to flee attack (Sets server ConVar: 'patrolhelicopterai.use_danger_zones')")]
                public bool allowFlee { get; set; }
                [JsonProperty(PropertyName = "Percent damage to trigger helicopter Fleeing (Sets server ConVar: 'patrolhelicopterai.flee_damage_percentage')")]
                public float fleePercent { get; set; }
                [JsonProperty(PropertyName = "Force Helicopter to Return to Player if it Moves Too far Away")]
                public bool returnToPlayer { get; set; }
                [JsonProperty(PropertyName = "Force Helicopter to Return Even if Attacking Other Players")]
                public bool returnIfAttacking { get; set; }
                [JsonProperty(PropertyName = "Force Helicopter to Return To Original Called Position Instead Of Player")]
                public bool returnToPosition { get; set; }
                [JsonProperty(PropertyName = "Max Distance of Helicopter From Player Before Force Return")]
                public float maxDistanceFromPlayer { get; set; }
                [JsonProperty(PropertyName = "Max Distance Helicopter Can Be Damaged By Any Player (0 = Disabled)")]
                public float maxHitDistance { get; set; }
                [JsonProperty(PropertyName = "Map Scale Distance Away to Spawn Helicopter (Default: 1.25 = 1.25 x Map Size Distance)")]
                public float mapScaleDistance { get; set; }
                [JsonProperty(PropertyName = "Height of heli when it arrives at called location")]
                public float arrivalHeight { get; set; }
                [JsonProperty(PropertyName = "Height of heli when it spawns (increase if it spawns under/in terrain)")]
                public float spawnHeight { get; set; }
                [JsonProperty(PropertyName = "Retire if Attacking Player is Building Blocked, While 'Block Damage to Other Players Bases' is True")]
                public bool RetireWarning { get; set; }
                [JsonProperty(PropertyName = "Retire Warning Threshold (Number of Warnings Allowed Before Retiring)")]
                public int WarningThreshold { get; set; }
                [JsonProperty(PropertyName = "Retire Heli on Calling Player/Team Killed")]
                public bool retireOnKilled { get; set; }
                [JsonProperty(PropertyName = "Use NoEscape")]
                public bool UseNoEscape { get; set; }
                [JsonProperty(PropertyName = "Player Cooldown (seconds) Between Calls (0 = no cooldown)")]
                public float playerCooldown { get; set; }
                [JsonProperty(PropertyName = "Player Cooldowns Apply to Each Tier Seperately")]
                public bool tierCooldowns { get; set; }
                [JsonProperty(PropertyName = "Cooldown Applies to Clan/Team/Friends (Requires Use Friends/Use Clan/Use Teams)")]
                public bool teamCooldown { get; set; }

                [JsonProperty(PropertyName = "Allow Players to Damage Helis With Remote Auto Turrets")]
                public bool allowTurretDamage { get; set; }
                [JsonProperty(PropertyName = "Heli Rockets Player Controlled Auto Turrets if Majority Damage Comes From Them")]
                public bool heliTargetTurret { get; set; }
                [JsonProperty(PropertyName = "Cooldown Before Heli Can Strafe Player Controlled Turrets Again (seconds)")]
                public float turretCooldown { get; set; }
                [JsonProperty(PropertyName = "Penalize Players With Majority Damage From Auto Turrets by This Percentage (0 = No Penalty)")]
                public double turretPenalty { get; set; }


                [JsonProperty(PropertyName = "Allow Players to Call Helis at Monuments")]
                public bool allowMonuments { get; set; }
                [JsonProperty(PropertyName = "Minimum Distance From Monuments When Allow at Monuments is False")]
                public float distFromMonuments { get; set; }
                [JsonProperty(PropertyName = "List of Monuments (Prefabs) to Block When Allow at Monuments is False")]
                public List<string> blockedMonuments { get; set; }
                [JsonProperty(PropertyName = "VIP/Custom Cooldowns")]
                public Hash<string, float> vipCooldowns { get; set; }
                [JsonProperty(PropertyName = "Protected Prefab List (Prefabs Listed Here Will Never Take Damage)")]
                public List<string> protectedPrefabs { get; set; }
                [JsonProperty(PropertyName = "Heli Wave Options")]
                public Dictionary<string, WaveData> waveConfig { get; set; }
                [JsonProperty(PropertyName = "Profiles")]
                public Dictionary<string, HeliData> heliConfig { get; set; }

                [JsonIgnore]
                public Oxide.Core.Libraries.Permission permission;
                public void RegisterPermissions(Oxide.Core.Libraries.Permission permission, HeliSignals plugin)
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
                    chatPrefix = "<color=orange>[Heli Signals]</color>",
                    usePrefix = true,
                    chatIcon = 0,
                    signalFuseLength = 3.5f,
                    smokeDuration = 210f,
                    noVanillaHeli = false,
                    useStacking = true,
                    reportCommand = "hsreport"
                },
                announce = new ConfigData.Announce
                {
                    killChat = true,
                    callChat = true,
                    retireChat = true,
                    reportChat = true,
                    reportRetire = true,
                    killVanilla = false,
                    reportVanilla = false,
                    vanillaName = "Patrol Helicopter",
                    vanillaOwner = "USAF (SERVER)",
                    maxReported = 5,
                    announceGlobal = true
                },
                discord = new ConfigData.Discord
                {
                    webHookUrl = defaultWebhookUrl,
                    sendHeliCall = false,
                    sendHeliKill = false,
                    sendHeliRetire = false
                },
                rewards = new ConfigData.Rewards
                {
                    rewardPlugin = "ServerRewards",
                    rewardUnit = "RP",
                    enableRewards = false,
                    shareRewards = false,
                    pluginXP = "XPerience",
                    enableXP = false,
                    shareXP = false,
                    boostXP = false,
                    enableScrap = false,
                    shareScrap = false,
                    enableCustomReward = false,
                    shareCustomReward = false,
                    customRewardItem = new CustomRewardItem
                    {
                        ShortName = "item.shortname",
                        SkinId = 0,
                        DisplayName = ""
                    },
                    rewardMultipliers = new Hash<string, float>
                    {
                        ["helisignals.vip1"] = 1.25f,
                        ["helisignals.vip2"] = 1.50f,
                        ["helisignals.vip3"] = 1.75f
                    }
                },
                purchasing = new ConfigData.Purchasing
                {
                    buyCommand = "hsbuy",
                    defaultCurrency = "ServerRewards",
                    purchaseUnit = "RP",
                    customCurrency = new List<CurrencyItem>
                    {
                        new CurrencyItem { ShortName = "scrap", SkinId = 0, DisplayName = "Scrap" }
                    }
                },
                heli = new ConfigData.Heli
                {
                    retireCommand = "hsretire",
                    canTeamRetire = false,
                    globalLimit = 10,
                    playerLimit = 3,
                    canMonumentCrash = true,
                    allowFlee = true,
                    fleePercent = 0.35f,
                    returnToPlayer = false,
                    returnIfAttacking = false,
                    returnToPosition = false,
                    maxDistanceFromPlayer = 500f,
                    maxHitDistance = 0f,
                    mapScaleDistance = 1.25f,
                    arrivalHeight = 20f,
                    spawnHeight = 100f,
                    RetireWarning = false,
                    WarningThreshold = 25,
                    retireOnKilled = false,
                    UseNoEscape = false,
                    playerCooldown = 3600f,
                    tierCooldowns = true,
                    teamCooldown = true,
                    allowTurretDamage = true,
                    heliTargetTurret = true,
                    turretCooldown = 30f,
                    turretPenalty = 0,
                    allowMonuments = false,
                    distFromMonuments = 50f,
                    blockedMonuments = new List<string>
                    {
                        "assets/bundled/prefabs/autospawn/monument/arctic_bases/arctic_research_base_a.prefab",
                        "assets/bundled/prefabs/autospawn/monument/harbor/ferry_terminal_1.prefab",
                        "assets/bundled/prefabs/autospawn/monument/harbor/harbor_1.prefab",
                        "assets/bundled/prefabs/autospawn/monument/harbor/harbor_2.prefab",
                        "assets/bundled/prefabs/autospawn/monument/large/airfield_1.prefab",
                        "assets/bundled/prefabs/autospawn/monument/large/excavator_1.prefab",
                        "assets/bundled/prefabs/autospawn/monument/large/military_tunnel_1.prefab",
                        "assets/bundled/prefabs/autospawn/monument/large/powerplant_1.prefab",
                        "assets/bundled/prefabs/autospawn/monument/large/trainyard_1.prefab",
                        "assets/bundled/prefabs/autospawn/monument/large/water_treatment_plant_1.prefab",
                        "assets/bundled/prefabs/remapped/monument/large/trainyard_1_scene.prefab",
                        "assets/bundled/prefabs/autospawn/monument/lighthouse/lighthouse.prefab",
                        "assets/bundled/prefabs/autospawn/monument/medium/junkyard_1.prefab",
                        "assets/bundled/prefabs/autospawn/monument/medium/nuclear_missile_silo.prefab",
                        "assets/bundled/prefabs/autospawn/monument/medium/radtown_small_3.prefab",
                        "assets/bundled/prefabs/autospawn/monument/military_bases/desert_military_base_a.prefab",
                        "assets/bundled/prefabs/autospawn/monument/military_bases/desert_military_base_b.prefab",
                        "assets/bundled/prefabs/autospawn/monument/military_bases/desert_military_base_c.prefab",
                        "assets/bundled/prefabs/autospawn/monument/military_bases/desert_military_base_d.prefab",
                        "assets/bundled/prefabs/autospawn/monument/offshore/oilrig_1.prefab",
                        "assets/bundled/prefabs/autospawn/monument/offshore/oilrig_2.prefab",
                        "assets/bundled/prefabs/autospawn/monument/roadside/gas_station_1.prefab",
                        "assets/bundled/prefabs/autospawn/monument/roadside/supermarket_1.prefab",
                        "assets/bundled/prefabs/autospawn/monument/roadside/warehouse.prefab",
                        "assets/bundled/prefabs/autospawn/monument/small/satellite_dish.prefab",
                        "assets/bundled/prefabs/autospawn/monument/small/sphere_tank.prefab",
                        "assets/bundled/prefabs/autospawn/monument/swamp/swamp_a.prefab",
                        "assets/bundled/prefabs/autospawn/monument/swamp/swamp_b.prefab",
                        "assets/bundled/prefabs/autospawn/monument/swamp/swamp_c.prefab",
                        "assets/bundled/prefabs/autospawn/monument/tiny/water_well_a.prefab",
                        "assets/bundled/prefabs/autospawn/monument/tiny/water_well_b.prefab",
                        "assets/bundled/prefabs/autospawn/monument/tiny/water_well_c.prefab",
                        "assets/bundled/prefabs/autospawn/monument/tiny/water_well_d.prefab",
                        "assets/bundled/prefabs/autospawn/monument/tiny/water_well_e.prefab",
                        "assets/bundled/prefabs/autospawn/monument/xlarge/launch_site_1.prefab"
                    },
                    vipCooldowns = new Hash<string, float>
                    {
                        ["helisignals.examplevip1"] = 3000f,
                        ["helisignals.examplevip2"] = 2400f,
                        ["helisignals.examplevip3"] = 1800f
                    },
                    protectedPrefabs = new List<string>
                    {
                        "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab",
                        "assets/prefabs/deployable/planters/planter.large.deployed.prefab"
                    },
                    waveConfig = new Dictionary<string, WaveData>
                    {
                        [normalWave] = new WaveData
                        {
                            SkinId = normalWaveSkinID,
                            GiveItemCommand = "wave_normal",
                            UseBuyCommand = false,
                            CostToBuy = 10000,
                            WaveProfiles = new List<string>()
                        },
                        [hardWave] = new WaveData
                        {
                            SkinId = hardWaveSkinID,
                            GiveItemCommand = "wave_hard",
                            UseBuyCommand = false,
                            CostToBuy = 20000,
                            WaveProfiles = new List<string>()
                        }
                    },
                    heliConfig = new Dictionary<string, HeliData>
                    {
                        [easyHeli] = new HeliData
                        {
                            HeliAmount = 1,
                            HeliName = easyHeli,
                            SignalSkinID = easySkinID,
                            GiveItemCommand = "easy",
                            UseBuyCommand = true,
                            CostToBuy = 500,
                            Health = 10000f,
                            MainRotorHealth = 900f,
                            TailRotorHealth = 500f,
                            InitialSpeed = 42f,
                            MaxSpeed = 42f,
                            OrbitRadius = 75f,
                            MaxOrbitDuration = 30f,
                            MaxRotationSpeed = 1.0f,
                            TerrainPushForce = 150f,
                            ObstaclePushForce = 150f,
                            CratesToSpawn = 4,
                            LockedCratesToSpawn = 0,
                            HackSeconds = 900f,
                            LockedCrateDespawn = 7200f,
                            BulletDamage = 20f,
                            BulletSpeed = 250,
                            GunFireRate = 0.125f,
                            BurstLength = 3f,
                            TimeBetweenBursts = 3f,
                            NewTargetRange = 150f,
                            MaxTargetRange = 300f,
                            BulletAccuracy = 40f,
                            MaxHeliRockets = 12,
                            TimeBetweenRockets = 0.2f,
                            RocketDamageScale = 1.0f,
                            NapalmChance = 0.75f,
                            OrbitStrafeChance = 0.4f,
                            MaxOrbitRockets = 12,
                            MinOrbitMultiplier = -3,
                            MaxOrbitMultiplier = 24,
                            StrafeCooldown = 20f,
                            DespawnTime = 1200f,
                            OwnerDamage = false,
                            TargetOtherPlayers = true,
                            BlockOwnerDamage = false,
                            BlockOtherDamage = false,
                            BlockPlayerDamage = false,
                            BlockProtectedList = false,
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
                            XPReward = 1000,
                            ScrapReward = 1000,
                            CustomReward = 1000,
                            DamageThreshold = 100f,
                            BotReSpawnProfile = "",
                            Loot = new HeliData.LootOptions
                            {
                                UseCustomLoot = false,
                                MinCrateItems = 2,
                                MaxCrateItems = 6,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            },
                            ExtraLoot = new HeliData.ExtraLootOptions
                            {
                                UseExtraLoot = false,
                                MinExtraItems = 1,
                                MaxExtraItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            },
                            LockedCrateLoot = new HeliData.LockedCrateLootOptions
                            {
                                UseLockedCrateLoot = false,
                                MinLockedCrateItems = 1,
                                MaxLockedCrateItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            }
                        },
                        [medHeli] = new HeliData
                        {
                            HeliAmount = 1,
                            HeliName = medHeli,
                            SignalSkinID = medSkinID,
                            GiveItemCommand = "medium",
                            UseBuyCommand = true,
                            CostToBuy = 1000,
                            Health = 20000f,
                            MainRotorHealth = 1800f,
                            TailRotorHealth = 1000f,
                            InitialSpeed = 42f,
                            MaxSpeed = 42f,
                            OrbitRadius = 75f,
                            MaxOrbitDuration = 30f,
                            MaxRotationSpeed = 1.0f,
                            TerrainPushForce = 150f,
                            ObstaclePushForce = 150f,
                            CratesToSpawn = 6,
                            LockedCratesToSpawn = 0,
                            HackSeconds = 900f,
                            LockedCrateDespawn = 7200f,
                            BulletDamage = 30f,
                            BulletSpeed = 300,
                            GunFireRate = 0.125f,
                            BurstLength = 3f,
                            TimeBetweenBursts = 3f,
                            NewTargetRange = 150f,
                            MaxTargetRange = 320f,
                            BulletAccuracy = 60f,
                            MaxHeliRockets = 12,
                            TimeBetweenRockets = 0.2f,
                            RocketDamageScale = 1.0f,
                            NapalmChance = 0.75f,
                            OrbitStrafeChance = 0.4f,
                            MaxOrbitRockets = 12,
                            MinOrbitMultiplier = -3,
                            MaxOrbitMultiplier = 24,
                            StrafeCooldown = 20f,
                            DespawnTime = 1800f,
                            OwnerDamage = false,
                            TargetOtherPlayers = true,
                            BlockOwnerDamage = false,
                            BlockOtherDamage = false,
                            BlockPlayerDamage = false,
                            BlockProtectedList = false,
                            KillGibs = false,
                            GibsHotTime = 600f,
                            GibsHealth = 1000f,
                            ProtectGibs = false,
                            UnlockGibs = 300f,
                            DisableFire = false,
                            FireDuration = 300f,
                            ProtectCrates = false,
                            UnlockCrates = 300f,
                            RewardPoints = 2000,
                            XPReward = 2000,
                            ScrapReward = 2000,
                            CustomReward = 2000,
                            DamageThreshold = 200f,
                            BotReSpawnProfile = "",
                            Loot = new HeliData.LootOptions
                            {
                                UseCustomLoot = false,
                                MinCrateItems = 4,
                                MaxCrateItems = 8,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            },
                            ExtraLoot = new HeliData.ExtraLootOptions
                            {
                                UseExtraLoot = false,
                                MinExtraItems = 1,
                                MaxExtraItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            },
                            LockedCrateLoot = new HeliData.LockedCrateLootOptions
                            {
                                UseLockedCrateLoot = false,
                                MinLockedCrateItems = 1,
                                MaxLockedCrateItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            }
                        },
                        [hardHeli] = new HeliData
                        {
                            HeliAmount = 1,
                            HeliName = hardHeli,
                            SignalSkinID = hardSkinID,
                            GiveItemCommand = "hard",
                            UseBuyCommand = true,
                            CostToBuy = 2000,
                            Health = 30000f,
                            MainRotorHealth = 2700f,
                            TailRotorHealth = 1500f,
                            InitialSpeed = 42f,
                            MaxSpeed = 42f,
                            OrbitRadius = 75f,
                            MaxOrbitDuration = 30f,
                            MaxRotationSpeed = 1.0f,
                            TerrainPushForce = 150f,
                            ObstaclePushForce = 150f,
                            CratesToSpawn = 8,
                            LockedCratesToSpawn = 0,
                            HackSeconds = 900f,
                            LockedCrateDespawn = 7200f,
                            BulletDamage = 40f,
                            BulletSpeed = 350,
                            GunFireRate = 0.125f,
                            BurstLength = 3f,
                            TimeBetweenBursts = 3f,
                            NewTargetRange = 150f,
                            MaxTargetRange = 340f,
                            BulletAccuracy = 80f,
                            MaxHeliRockets = 12,
                            TimeBetweenRockets = 0.2f,
                            RocketDamageScale = 1.0f,
                            NapalmChance = 0.75f,
                            OrbitStrafeChance = 0.4f,
                            MaxOrbitRockets = 12,
                            MinOrbitMultiplier = -3,
                            MaxOrbitMultiplier = 24,
                            StrafeCooldown = 20f,
                            DespawnTime = 2400f,
                            OwnerDamage = false,
                            TargetOtherPlayers = true,
                            BlockOwnerDamage = false,
                            BlockOtherDamage = false,
                            BlockPlayerDamage = false,
                            BlockProtectedList = false,
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
                            XPReward = 4000,
                            ScrapReward = 4000,
                            CustomReward = 4000,
                            DamageThreshold = 400f,
                            BotReSpawnProfile = "",
                            Loot = new HeliData.LootOptions
                            {
                                UseCustomLoot = false,
                                MinCrateItems = 6,
                                MaxCrateItems = 10,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            },
                            ExtraLoot = new HeliData.ExtraLootOptions
                            {
                                UseExtraLoot = false,
                                MinExtraItems = 1,
                                MaxExtraItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            },
                            LockedCrateLoot = new HeliData.LockedCrateLootOptions
                            {
                                UseLockedCrateLoot = false,
                                MinLockedCrateItems = 1,
                                MaxLockedCrateItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            }
                        },
                        [eliteHeli] = new HeliData
                        {
                            HeliAmount = 1,
                            HeliName = eliteHeli,
                            SignalSkinID = eliteSkinID,
                            GiveItemCommand = "elite",
                            UseBuyCommand = true,
                            CostToBuy = 4000,
                            Health = 40000f,
                            MainRotorHealth = 3600f,
                            TailRotorHealth = 2000f,
                            InitialSpeed = 42f,
                            MaxSpeed = 42f,
                            OrbitRadius = 75f,
                            MaxOrbitDuration = 30f,
                            MaxRotationSpeed = 1.0f,
                            TerrainPushForce = 150f,
                            ObstaclePushForce = 150f,
                            CratesToSpawn = 10,
                            LockedCratesToSpawn = 0,
                            HackSeconds = 900f,
                            LockedCrateDespawn = 7200f,
                            BulletDamage = 50f,
                            BulletSpeed = 400,
                            GunFireRate = 0.125f,
                            BurstLength = 3f,
                            TimeBetweenBursts = 3f,
                            NewTargetRange = 150f,
                            MaxTargetRange = 360f,
                            BulletAccuracy = 40f,
                            MaxHeliRockets = 12,
                            TimeBetweenRockets = 0.2f,
                            RocketDamageScale = 1.0f,
                            NapalmChance = 0.75f,
                            OrbitStrafeChance = 0.4f,
                            MaxOrbitRockets = 12,
                            MinOrbitMultiplier = -3,
                            MaxOrbitMultiplier = 24,
                            StrafeCooldown = 20f,
                            DespawnTime = 3600f,
                            OwnerDamage = false,
                            TargetOtherPlayers = true,
                            BlockOwnerDamage = false,
                            BlockOtherDamage = false,
                            BlockPlayerDamage = false,
                            BlockProtectedList = false,
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
                            XPReward = 8000,
                            ScrapReward = 8000,
                            CustomReward = 8000,
                            DamageThreshold = 600f,
                            BotReSpawnProfile = "",
                            Loot = new HeliData.LootOptions
                            {
                                UseCustomLoot = false,
                                MinCrateItems = 8,
                                MaxCrateItems = 12,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            },
                            ExtraLoot = new HeliData.ExtraLootOptions
                            {
                                UseExtraLoot = false,
                                MinExtraItems = 1,
                                MaxExtraItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            },
                            LockedCrateLoot = new HeliData.LockedCrateLootOptions
                            {
                                UseLockedCrateLoot = false,
                                MinLockedCrateItems = 1,
                                MaxLockedCrateItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            }
                        },
                        [easyMulti] = new HeliData
                        {
                            HeliAmount = 2,
                            HeliName = easyMulti,
                            SignalSkinID = easyMultiSkinID,
                            GiveItemCommand = "easy_multi",
                            UseBuyCommand = true,
                            CostToBuy = 750,
                            Health = 10000f,
                            MainRotorHealth = 900f,
                            TailRotorHealth = 500f,
                            InitialSpeed = 42f,
                            MaxSpeed = 42f,
                            OrbitRadius = 75f,
                            MaxOrbitDuration = 30f,
                            MaxRotationSpeed = 1.0f,
                            TerrainPushForce = 150f,
                            ObstaclePushForce = 150f,
                            CratesToSpawn = 4,
                            LockedCratesToSpawn = 0,
                            HackSeconds = 900f,
                            LockedCrateDespawn = 7200f,
                            BulletDamage = 20f,
                            BulletSpeed = 250,
                            GunFireRate = 0.125f,
                            BurstLength = 3f,
                            TimeBetweenBursts = 3f,
                            NewTargetRange = 150f,
                            MaxTargetRange = 300f,
                            BulletAccuracy = 40f,
                            MaxHeliRockets = 12,
                            TimeBetweenRockets = 0.2f,
                            RocketDamageScale = 1.0f,
                            NapalmChance = 0.75f,
                            OrbitStrafeChance = 0.4f,
                            MaxOrbitRockets = 12,
                            MinOrbitMultiplier = -3,
                            MaxOrbitMultiplier = 24,
                            StrafeCooldown = 20f,
                            DespawnTime = 1200f,
                            OwnerDamage = false,
                            TargetOtherPlayers = true,
                            BlockOwnerDamage = false,
                            BlockOtherDamage = false,
                            BlockPlayerDamage = false,
                            BlockProtectedList = false,
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
                            XPReward = 1000,
                            ScrapReward = 1000,
                            CustomReward = 1000,
                            DamageThreshold = 100f,
                            BotReSpawnProfile = "",
                            Loot = new HeliData.LootOptions
                            {
                                UseCustomLoot = false,
                                MinCrateItems = 2,
                                MaxCrateItems = 6,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            },
                            ExtraLoot = new HeliData.ExtraLootOptions
                            {
                                UseExtraLoot = false,
                                MinExtraItems = 1,
                                MaxExtraItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            },
                            LockedCrateLoot = new HeliData.LockedCrateLootOptions
                            {
                                UseLockedCrateLoot = false,
                                MinLockedCrateItems = 1,
                                MaxLockedCrateItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            }
                        },
                        [medMulti] = new HeliData
                        {
                            HeliAmount = 2,
                            HeliName = medMulti,
                            SignalSkinID = medMultiSkinID,
                            GiveItemCommand = "medium_multi",
                            UseBuyCommand = true,
                            CostToBuy = 1500,
                            Health = 20000f,
                            MainRotorHealth = 1800f,
                            TailRotorHealth = 1000f,
                            InitialSpeed = 42f,
                            MaxSpeed = 42f,
                            OrbitRadius = 75f,
                            MaxOrbitDuration = 30f,
                            MaxRotationSpeed = 1.0f,
                            TerrainPushForce = 150f,
                            ObstaclePushForce = 150f,
                            CratesToSpawn = 6,
                            LockedCratesToSpawn = 0,
                            HackSeconds = 900f,
                            LockedCrateDespawn = 7200f,
                            BulletDamage = 30f,
                            BulletSpeed = 300,
                            GunFireRate = 0.125f,
                            BurstLength = 3f,
                            TimeBetweenBursts = 3f,
                            NewTargetRange = 150f,
                            MaxTargetRange = 320f,
                            BulletAccuracy = 60f,
                            MaxHeliRockets = 12,
                            TimeBetweenRockets = 0.2f,
                            RocketDamageScale = 1.0f,
                            NapalmChance = 0.75f,
                            OrbitStrafeChance = 0.4f,
                            MaxOrbitRockets = 12,
                            MinOrbitMultiplier = -3,
                            MaxOrbitMultiplier = 24,
                            StrafeCooldown = 20f,
                            DespawnTime = 1800f,
                            OwnerDamage = false,
                            TargetOtherPlayers = true,
                            BlockOwnerDamage = false,
                            BlockOtherDamage = false,
                            BlockPlayerDamage = false,
                            BlockProtectedList = false,
                            KillGibs = false,
                            GibsHotTime = 600f,
                            GibsHealth = 1000f,
                            ProtectGibs = false,
                            UnlockGibs = 300f,
                            DisableFire = false,
                            FireDuration = 300f,
                            ProtectCrates = false,
                            UnlockCrates = 300f,
                            RewardPoints = 2000,
                            XPReward = 2000,
                            ScrapReward = 2000,
                            CustomReward = 2000,
                            DamageThreshold = 200f,
                            BotReSpawnProfile = "",
                            Loot = new HeliData.LootOptions
                            {
                                UseCustomLoot = false,
                                MinCrateItems = 4,
                                MaxCrateItems = 8,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            },
                            ExtraLoot = new HeliData.ExtraLootOptions
                            {
                                UseExtraLoot = false,
                                MinExtraItems = 1,
                                MaxExtraItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            },
                            LockedCrateLoot = new HeliData.LockedCrateLootOptions
                            {
                                UseLockedCrateLoot = false,
                                MinLockedCrateItems = 1,
                                MaxLockedCrateItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            }
                        },
                        [hardMulti] = new HeliData
                        {
                            HeliAmount = 2,
                            HeliName = hardMulti,
                            SignalSkinID = hardMultiSkinID,
                            GiveItemCommand = "hard_multi",
                            UseBuyCommand = true,
                            CostToBuy = 3000,
                            Health = 30000f,
                            MainRotorHealth = 2700f,
                            TailRotorHealth = 1500f,
                            InitialSpeed = 42f,
                            MaxSpeed = 42f,
                            OrbitRadius = 75f,
                            MaxOrbitDuration = 30f,
                            MaxRotationSpeed = 1.0f,
                            TerrainPushForce = 150f,
                            ObstaclePushForce = 150f,
                            CratesToSpawn = 8,
                            LockedCratesToSpawn = 0,
                            HackSeconds = 900f,
                            LockedCrateDespawn = 7200f,
                            BulletDamage = 40f,
                            BulletSpeed = 350,
                            GunFireRate = 0.125f,
                            BurstLength = 3f,
                            TimeBetweenBursts = 3f,
                            NewTargetRange = 150f,
                            MaxTargetRange = 340f,
                            BulletAccuracy = 80f,
                            MaxHeliRockets = 12,
                            TimeBetweenRockets = 0.2f,
                            RocketDamageScale = 1.0f,
                            NapalmChance = 0.75f,
                            OrbitStrafeChance = 0.4f,
                            MaxOrbitRockets = 12,
                            MinOrbitMultiplier = -3,
                            MaxOrbitMultiplier = 24,
                            StrafeCooldown = 20f,
                            DespawnTime = 2400f,
                            OwnerDamage = false,
                            TargetOtherPlayers = true,
                            BlockOwnerDamage = false,
                            BlockOtherDamage = false,
                            BlockPlayerDamage = false,
                            BlockProtectedList = false,
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
                            XPReward = 4000,
                            ScrapReward = 4000,
                            CustomReward = 4000,
                            DamageThreshold = 400f,
                            BotReSpawnProfile = "",
                            Loot = new HeliData.LootOptions
                            {
                                UseCustomLoot = false,
                                MinCrateItems = 6,
                                MaxCrateItems = 10,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            },
                            ExtraLoot = new HeliData.ExtraLootOptions
                            {
                                UseExtraLoot = false,
                                MinExtraItems = 1,
                                MaxExtraItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            },
                            LockedCrateLoot = new HeliData.LockedCrateLootOptions
                            {
                                UseLockedCrateLoot = false,
                                MinLockedCrateItems = 1,
                                MaxLockedCrateItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            }
                        },
                        [eliteMulti] = new HeliData
                        {
                            HeliAmount = 2,
                            HeliName = eliteMulti,
                            SignalSkinID = eliteMultiSkinID,
                            GiveItemCommand = "elite_multi",
                            UseBuyCommand = true,
                            CostToBuy = 6000,
                            Health = 40000f,
                            MainRotorHealth = 3600f,
                            TailRotorHealth = 2000f,
                            InitialSpeed = 42f,
                            MaxSpeed = 42f,
                            OrbitRadius = 75f,
                            MaxOrbitDuration = 30f,
                            MaxRotationSpeed = 1.0f,
                            TerrainPushForce = 150f,
                            ObstaclePushForce = 150f,
                            CratesToSpawn = 10,
                            LockedCratesToSpawn = 0,
                            HackSeconds = 900f,
                            LockedCrateDespawn = 7200f,
                            BulletDamage = 50f,
                            BulletSpeed = 400,
                            GunFireRate = 0.125f,
                            BurstLength = 3f,
                            TimeBetweenBursts = 3f,
                            NewTargetRange = 150f,
                            MaxTargetRange = 360f,
                            BulletAccuracy = 40f,
                            MaxHeliRockets = 12,
                            TimeBetweenRockets = 0.2f,
                            RocketDamageScale = 1.0f,
                            NapalmChance = 0.75f,
                            OrbitStrafeChance = 0.4f,
                            MaxOrbitRockets = 12,
                            MinOrbitMultiplier = -3,
                            MaxOrbitMultiplier = 24,
                            StrafeCooldown = 20f,
                            DespawnTime = 3600f,
                            OwnerDamage = false,
                            TargetOtherPlayers = true,
                            BlockOwnerDamage = false,
                            BlockOtherDamage = false,
                            BlockPlayerDamage = false,
                            BlockProtectedList = false,
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
                            XPReward = 8000,
                            ScrapReward = 8000,
                            CustomReward = 8000,
                            DamageThreshold = 600f,
                            BotReSpawnProfile = "",
                            Loot = new HeliData.LootOptions
                            {
                                UseCustomLoot = false,
                                MinCrateItems = 8,
                                MaxCrateItems = 12,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            },
                            ExtraLoot = new HeliData.ExtraLootOptions
                            {
                                UseExtraLoot = false,
                                MinExtraItems = 1,
                                MaxExtraItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
                                }
                            },
                            LockedCrateLoot = new HeliData.LockedCrateLootOptions
                            {
                                UseLockedCrateLoot = false,
                                MinLockedCrateItems = 1,
                                MaxLockedCrateItems = 3,
                                AllowDupes = false,
                                MaxBP = 2,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "example.shortname1", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f },
                                    new LootItem { ShortName = "example.shortname2", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "", BlueprintChance = 0f }
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
                    PrintError($"Exception Type: {ex.GetType()}");
                    PrintError($"Exception: {ex}");
                    return;
                }
                throw;
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Configuration file missing or corrupt, creating default config file.");
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

            PrintWarning("Config update detected! Updating config file...");
            if (config.Version < new VersionNumber(1, 0, 26))
            {
                // Adding example loot items if there are none, since I stupidly forgot to add them previously
                foreach (var key in config.heli.heliConfig.Keys)
                {
                    if (config.heli.heliConfig[key].Loot.LootTable.Count == 0)
                    {
                        config.heli.heliConfig[key].Loot.LootTable.Add(new LootItem
                        {
                            ShortName = "example.shortname1",
                            Chance = 50,
                            AmountMin = 1,
                            AmountMax = 2,
                            SkinId = 0,
                            DisplayName = ""
                        });
                        config.heli.heliConfig[key].Loot.LootTable.Add(new LootItem
                        {
                            ShortName = "example.shortname2",
                            Chance = 50,
                            AmountMin = 1,
                            AmountMax = 2,
                            SkinId = 0,
                            DisplayName = ""
                        });
                    }
                    if (config.heli.heliConfig[key].ExtraLoot.LootTable.Count == 0)
                    {
                        config.heli.heliConfig[key].ExtraLoot.LootTable.Add(new LootItem
                        {
                            ShortName = "example.shortname1",
                            Chance = 50,
                            AmountMin = 1,
                            AmountMax = 2,
                            SkinId = 0,
                            DisplayName = ""
                        });
                        config.heli.heliConfig[key].ExtraLoot.LootTable.Add(new LootItem
                        {
                            ShortName = "example.shortname2",
                            Chance = 50,
                            AmountMin = 1,
                            AmountMax = 2,
                            SkinId = 0,
                            DisplayName = ""
                        });
                    }
                    if (config.heli.heliConfig[key].LockedCrateLoot.LootTable.Count == 0)
                    {
                        config.heli.heliConfig[key].LockedCrateLoot.LootTable.Add(new LootItem
                        {
                            ShortName = "example.shortname1",
                            Chance = 50,
                            AmountMin = 1,
                            AmountMax = 2,
                            SkinId = 0,
                            DisplayName = ""
                        });
                        config.heli.heliConfig[key].LockedCrateLoot.LootTable.Add(new LootItem
                        {
                            ShortName = "example.shortname2",
                            Chance = 50,
                            AmountMin = 1,
                            AmountMax = 2,
                            SkinId = 0,
                            DisplayName = ""
                        });
                    }
                }
            }
            if (config.Version < new VersionNumber(1, 1, 1))
            {
                config.options.reportCommand = defaultConfig.options.reportCommand;
                config.heli.allowTurretDamage = defaultConfig.heli.allowTurretDamage;
            }
            if (config.Version < new VersionNumber(1, 1, 3))
            {
                config.heli.tierCooldowns = defaultConfig.heli.tierCooldowns;
                config.heli.playerLimit = defaultConfig.heli.playerLimit;
            }
            if (config.Version < new VersionNumber(1, 1, 7))
            {
                config.announce.announceGlobal = defaultConfig.announce.announceGlobal;
                config.heli.returnToPosition = defaultConfig.heli.returnToPosition;

                foreach (var key in config.heli.heliConfig.Keys)
                {
                    config.heli.heliConfig[key].Loot.MaxBP = 2;
                    config.heli.heliConfig[key].ExtraLoot.MaxBP = 2;
                    config.heli.heliConfig[key].LockedCrateLoot.MaxBP = 2;

                    foreach (var lootItem in config.heli.heliConfig[key].Loot.LootTable)
                    {
                        lootItem.BlueprintChance = 0f;
                    }

                    foreach (var lootItem in config.heli.heliConfig[key].ExtraLoot.LootTable)
                    {
                        lootItem.BlueprintChance = 0f;
                    }

                    foreach (var lootItem in config.heli.heliConfig[key].LockedCrateLoot.LootTable)
                    {
                        lootItem.BlueprintChance = 0f;
                    }
                    
                }
            }
            if (config.Version < new VersionNumber(1, 1, 9))
            {
                config.options.chatIcon = defaultConfig.options.chatIcon;
            }
            if (config.Version < new VersionNumber(1, 1, 10))
            {
                config.options.useDynamicPVP = defaultConfig.options.useDynamicPVP;
            }
            if (config.Version < new VersionNumber(1, 1, 12))
            {
                config.heli.allowMonuments = defaultConfig.heli.allowMonuments;
            }
            if (config.Version < new VersionNumber(1, 2, 1))
            {
                config.announce.retireChat = defaultConfig.announce.retireChat;
                config.announce.reportRetire = defaultConfig.announce.reportRetire;
                config.announce.killVanilla = defaultConfig.announce.killVanilla;
                config.announce.reportVanilla = defaultConfig.announce.reportVanilla;
                config.announce.vanillaName = defaultConfig.announce.vanillaName;
                config.announce.vanillaOwner = defaultConfig.announce.vanillaOwner;

                config.rewards.enableCustomReward = defaultConfig.rewards.enableCustomReward;
                config.rewards.shareCustomReward = defaultConfig.rewards.shareCustomReward;
                config.rewards.customRewardItem = defaultConfig.rewards.customRewardItem;

                config.heli.protectedPrefabs = defaultConfig.heli.protectedPrefabs;
                config.heli.waveConfig = defaultConfig.heli.waveConfig;

                foreach (var key in config.heli.heliConfig.Keys)
                {
                    config.heli.waveConfig[normalWave].WaveProfiles.Add(key);
                    config.heli.waveConfig[hardWave].WaveProfiles.Add(key);

                    config.heli.heliConfig[key].HeliAmount = 1;
                    config.heli.heliConfig[key].CustomReward = 1000;
                    config.heli.heliConfig[key].XPReward = 1000;
                    config.heli.heliConfig[key].BlockProtectedList = false;
                }
            }
            if (config.Version < new VersionNumber(1, 2, 2))
            {
                foreach (var key in config.heli.waveConfig.Keys)
                {
                    if (key == normalWave)
                        config.heli.waveConfig[key].SkinId = normalWaveSkinID;
                    else if (key == hardWave)
                        config.heli.waveConfig[key].SkinId = hardWaveSkinID;
                }
            }
            if (config.Version < new VersionNumber(1, 2, 5))
            {
                config.rewards.pluginXP = defaultConfig.rewards.pluginXP;
                config.rewards.boostXP = defaultConfig.rewards.boostXP;
            }
            if (config.Version < new VersionNumber(1, 2, 7))
            {
                config.heli.blockedMonuments = defaultConfig.heli.blockedMonuments;
                config.heli.distFromMonuments = defaultConfig.heli.distFromMonuments;
            }
            if (config.Version < new VersionNumber(1, 2, 10))
            {
                config.heli.allowFlee = defaultConfig.heli.allowFlee;
                config.heli.fleePercent = defaultConfig.heli.fleePercent;

                foreach (var key in config.heli.heliConfig.Keys)
                {
                    config.heli.heliConfig[key].OrbitStrafeChance = 0.4f;
                    config.heli.heliConfig[key].MaxOrbitRockets = 12;
                    config.heli.heliConfig[key].MinOrbitMultiplier = -3;
                    config.heli.heliConfig[key].MaxOrbitMultiplier = 24;
                }
            }
            if (config.Version < new VersionNumber(1, 2, 15))
            {
                config.heli.spawnHeight = defaultConfig.heli.spawnHeight;
            }
            if (config.Version < new VersionNumber(1, 2, 17))
            {
                config.heli.canMonumentCrash = defaultConfig.heli.canMonumentCrash;
            }
            if (config.Version < new VersionNumber(1, 2, 18))
            {
                config.heli.heliTargetTurret = defaultConfig.heli.heliTargetTurret;
                config.heli.turretPenalty = defaultConfig.heli.turretPenalty;
                config.heli.turretCooldown = defaultConfig.heli.turretCooldown;
            }
            if (config.Version < new VersionNumber(1, 2, 19))
            {
                foreach (var key in config.heli.heliConfig.Keys)
                {
                    config.heli.heliConfig[key].StrafeCooldown = 20f;
                    config.heli.heliConfig[key].TerrainPushForce = 150f; 
                    config.heli.heliConfig[key].ObstaclePushForce = 150f;
                }
            }
            if (config.Version < new VersionNumber(1, 2, 20))
            {
                config.options.useBetterNPC = defaultConfig.options.useBetterNPC;
            }

            config.Version = Version;
            SaveConfig();
            defaultConfig = null;
            PrintWarning("Config update complete!");
        }
        
        #endregion Config
    }
}