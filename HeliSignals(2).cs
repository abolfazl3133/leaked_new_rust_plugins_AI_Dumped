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

/* Changelog 1.0.25

 - Fixed: Hackable locked crate not despawning at time specified in config
 - Fixed: Custom loot not populating after 1.0.22 update (sorry)
 - Fixed: Occasional NullReferenceException error on CanStackItem
 - Added: Hackable locked crate loot table options

 */

namespace Oxide.Plugins
{
    /*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ /*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("Heli Signals", "https://discord.gg/dNGbxafuJn", "1.0.25")]
    [Description("Call Patrol Helicopters to your location with custom supply signals.")]
    public class HeliSignals: RustPlugin
    {
        #region Plugin References
        
        [PluginReference] Plugin Friends, Clans, FancyDrop, RewardPlugin, Vanish, NoEscape, BotReSpawn;
        
        #endregion Plugin References

        #region Constants

        public static HeliSignals Instance;
        
        public const string permAdmin = "helisignals.admin";
        public const string permBuy = "helisignals.buy";
        public const string permBypasscooldown = "helisignals.bypasscooldown";
        
        public const string heliPrefab = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";
        public const string heliExplosion = "assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab";
        public const string hackableCrate = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";

        public const string defaultWebhookUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        
        public const int supplySignalId = 1397052267;
        public const int scrapId = -932201673;

        // Default config item skins
        public const ulong easySkinID = 2920175997;
        public const ulong medSkinID = 2920176079;
        public const ulong hardSkinID = 2920176050;
        public const ulong eliteSkinID = 2920176024;

        // Default config item names
        public const string easyHeli = "Heli Signal (Easy)";
        public const string medHeli = "Heli Signal (Medium)";
        public const string hardHeli = "Heli Signal (Hard)";
        public const string eliteHeli = "Heli Signal (Elite)";
        
        #endregion Constants

        #region Language
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SyntaxPlayer"] = "<color=red>Invalid syntax</color>, use:\n\n<color=green>/hsgive <type> <SteamID/PlayerName> <amount></color>",
                ["Receive"] = "You received <color=orange>{0}</color> x <color=orange>{1}</color>!",
                ["PlayerReceive"] = "Player {0} ({1}) received {2} x {3}!",
                ["Permission"] = "You do not have permission to use <color=orange>{0}</color>!",
                ["RaidBlocked"] = "You cannot use <color=orange>{0}</color> while raid blocked!",
                ["CombatBlocked"] = "You cannot use <color=orange>{0}</color> while combat blocked!",
                ["CooldownTime"] = "Cooldown active! You can call another in {0}!",
                ["TeamCooldownTime"] = "Team cooldown active! You can call another in {0}!",
                ["GlobalLimit"] = "Global limit of {0} active helicopters is reached, please try again later",
                ["NotAdmin"] = "You do not have permission to use that command!",
                ["PlayerNotFound"] = "Can't find a player with the name or ID: {0}",
                ["InGameOnly"] = "Error: This command is only for use in game!",
                ["PlayerDead"] = "Player with name or ID {0} is dead, try again when they have respawned",
                ["InSafeZone"] = "<color=orange>{0}</color> was thrown in a <color=green>Safe Zone</color> and was refunded, check inventory.",
                ["SyntaxConsole"] = "Invalid syntax, use: hsgive <type> <SteamID/PlayerName> <amount>",
                ["InvalidDrop"] = "Signal type \"{0}\" not recognised, please check and try again!",
                ["CannotLoot"] = "You cannot loot this because it is not yours!",
                ["CannotHack"] = "You cannot hack this because it is not yours!",
                ["CannotHarvest"] = "You cannot harvest this because it is not yours!",
                ["BuySyntaxCmd"] = "Usage:\n\n/{0} {1}",
                ["NoBuy"] = "Buy Command for <color=orange>{0}</color> Is Not Enabled!",
                ["BuyPermission"] = "You do not have permission to buy Heli Signal \"<color=orange>{0}</color>\".",
                ["PriceList"] = "Heli Signal Prices:\n\n{0}",
                ["HeliKilledTime"] = "<color=orange>{0}</color> killed by <color=green>{1}</color> in grid <color=green>{2}</color> (Time Taken: {3})",
                ["HeliCalled"] = "<color=green>{0}</color> just called in a <color=orange>{1}</color> to their location in grid <color=green>{2}</color>",
                ["PointsGiven"] = "<color=green>{0} {1}</color> received for destroying <color=orange>{2}</color>!",
                ["ScrapGiven"] = "<color=green>{0}</color> Scrap received for destroying <color=orange>{1}</color>!",
                ["CannotDamage"] = "You <color=red>Cannot</color> damage this <color=orange>{0}</color>!",
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
                ["DamageReport"] = "<color=orange>Damage Report</color> / <color=orange>Rotor Accuracy</color>\n\n{0}",
                ["DamageReportItem"] = "<size=11><color=green>{0}</color> -> {1} HP / {2}%\n</size>",
                ["DiscordCall"] = "**{0}** just called a **{1}** to their location in grid **{2}**",
                ["DiscordKill"] = "**{0}** just took down a **{1}** in grid **{2}**",
                ["DiscordRetire"] = "**{0}** called by **{1}** just retired from grid **{2}**",
                ["RetiredHelis"] = "You have retired ALL your (your teams) called Patrol Helicopters"
            }, this, "en");
        }

        private string Lang(string messageKey, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this), args);
        }

        public void Message(IPlayer player, string messageKey, params object[] args)
        {
            var message = Lang(messageKey, args);
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

        public void Message(BasePlayer player, string messageKey, params object[] args)
        {
            if (player is NPCPlayer) return;
            var message = Lang(messageKey, args);
            if (config.options.usePrefix && config.options.chatPrefix != string.Empty)
            {
                player.ChatMessage($"{config.options.chatPrefix}: {message}");
            }
            else
            {
                player.ChatMessage(message);
            }
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

            if (config.options.noVanillaHeli)
                Puts($"INFO: Vanilla patrol Helicopter server event is disabled");
        }

        private void Init()
        {
            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permBuy, this);
            permission.RegisterPermission(permBypasscooldown, this);

            config.rewards.RegisterPermissions(permission, this);
            config.heli.RegisterPermissions(permission, this);

            foreach (var key in config.heli.heliConfig.Keys)
            {
                var permSuffix = config.heli.heliConfig[key].GiveItemCommand.ToLower();
                var perm = $"{Name.ToLower()}.{permSuffix}";
                permission.RegisterPermission(perm, this);
                HeliTypes.Add(config.heli.heliConfig[key].GiveItemCommand);
            }

            AddCovalenceCommand("hsreport", nameof(CmdReport));
            AddCovalenceCommand(config.purchasing.buyCommand, nameof(CmdBuySignal));
            AddCovalenceCommand(config.heli.retireCommand, nameof(CmdRetireHeli));
            AddCovalenceCommand("hsgive", nameof(CmdGiveSignal));
        }

        private void Unload()
        {
            foreach (var netId in HeliSignalData.Keys)
            {
                var heli = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as PatrolHelicopter;
                if (heli != null)
                {
                    if (IsHeliSignal(heli.skinID))
                    {
                        heli.Kill();
                    }
                }
            }

            if (config.options.noVanillaHeli)
                Puts($"INFO: Vanilla patrol Helicopter server event has been re-enabled");

            HeliSignalData.Clear();
            PlayerCooldowns.Clear();
            LockedCrates.Clear();
            HeliTypes.Clear();
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
            if (IsHeliSignal(item.skin)) NextTick(()=> FixSignalInfo(item));
        }

        private void OnExplosiveThrown(BasePlayer player, SupplySignal entity, ThrownWeapon item)
        {
            var signal = item.GetItem();
            if (signal == null) return;
            
            if (IsHeliSignal(signal.skin))
            {
                entity.EntityToCreate = null;
                entity.CancelInvoke(entity.Explode);
                entity.skinID = signal.skin;

                NextTick(()=> HeliSignalThrown(player, entity, signal));
            }
        }

        private object CanStackItem(Item item, Item targetItem)
        {
            if ((item.info || targetItem.info) == null)
                return null;
            
            if (item.info.itemid == targetItem.info.itemid && item.skin != targetItem.skin)
                return false;
            
            return null;
        }

        private object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item.item == null || targetItem.item == null)
                return null;
            
            return CanStackItem(item.item, targetItem.item);
        }

        private void OnExplosiveDropped(BasePlayer player, SupplySignal entity, ThrownWeapon item) => OnExplosiveThrown(player, entity, item);

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity.ShortPrefabName.Contains("rocket_heli"))
            {
                var heliList = Facepunch.Pool.GetList<PatrolHelicopter>();
                var rocketEnt = entity as BaseEntity;

                Vis.Entities(rocketEnt.transform.position, 10f, heliList);
                foreach (var ent in heliList)
                {
                    if (ent.IsDestroyed)
                        continue;
                    
                    PatrolHelicopter heli = ent as PatrolHelicopter;
                    if (!IsHeliSignal(heli.skinID))
                        continue;
                    
                    var heliProfile = heli._name;
                    if (heliProfile == null)
                        continue;
                    
                    rocketEnt.creatorEntity = heli;
                    rocketEnt.OwnerID = heli.OwnerID;
                    SetDamageScale((rocketEnt as TimedExplosive), config.heli.heliConfig[heliProfile].RocketDamageScale);
                    break;
                }
                Facepunch.Pool.FreeList(ref heliList);
            }
        }

        private object OnEntityTakeDamage(PatrolHelicopter heli, HitInfo info)
        {
            if (heli?._name == null)
                return null;
            
            if (IsHeliSignal(heli.skinID))
            {
                HeliComponent heliComp = heli.GetComponent<HeliComponent>();
                if (heliComp == null)
                    return null;
                
                if (heliComp.isRetiring)
                {
                    CancelHit(info);
                    return true;
                }

                var heliProfile = heli._name;
                if (!config.heli.heliConfig.ContainsKey(heliProfile))
                    return null;

                var attacker = info?.Initiator as BasePlayer;
                if (attacker != null)
                {
                    var heliAI = heli.GetComponent<PatrolHelicopterAI>();
                    if (heliAI == null) return null;

                    var heliDisplayName = config.heli.heliConfig[heliProfile].HeliName;
                    if (config.heli.heliConfig[heliProfile].OwnerDamage)
                    {
                        if (!IsOwnerOrFriend(attacker.userID, heli.OwnerID))
                        {
                            Message(attacker, "CannotDamage", heliDisplayName);
                            CancelHit(info);
                            return true;
                        }
                    }

                    var dist = Vector3.Distance(heli.transform.position, attacker.transform.position);
                    var maxDist = config.heli.maxHitDistance;
                    if (maxDist > 0 && dist > maxDist)
                    {
                        CancelHit(info);
                        Message(attacker, "TooFarAway", heliDisplayName, maxDist);
                        return true;
                    }

                    if (config.heli.RetireWarning && attacker.IsBuildingBlocked() && IsOwnerOrFriend(attacker.userID, heli.OwnerID))
                    {
                        HeliSignalData[heli.net.ID.Value].WarningLevel++;
                        if (!heliComp.isRetiring && HeliSignalData[heli.net.ID.Value].WarningLevel >= config.heli.WarningThreshold)
                        {
                            heliComp.isRetiring = true;
                            RetireHeli(heliAI, heli.net.ID.Value);
                            CancelHit(info);
                            Message(attacker, "RetireHeli", heliDisplayName, HeliSignalData[heli.net.ID.Value].WarningLevel, config.heli.WarningThreshold);
                            return true;
                        }

                        CancelHit(info);
                        Message(attacker, "RetireWarn", heliDisplayName, HeliSignalData[heli.net.ID.Value].WarningLevel, config.heli.WarningThreshold);
                        return true;
                    }

                    var heliId = heli.net.ID.Value;
                    if (heliAI._currentState != PatrolHelicopterAI.aiState.DEATH)
                    {
                        if (!HeliSignalData.ContainsKey(heliId))
                            HeliSignalData.Add(heliId, new HeliStats());

                        if (HeliSignalData[heliId].FirstHitTime == 0)
                            HeliSignalData[heliId].FirstHitTime = UnityEngine.Time.realtimeSinceStartup;

                        if (!HeliSignalData[heliId].Attackers.ContainsKey(attacker.userID))
                        {
                            HeliSignalData[heliId].Attackers.Add(attacker.userID, new AttackersStats());
                            HeliSignalData[heliId].Attackers[attacker.userID].Name = attacker.displayName;
                        }
                        
                        HeliSignalData[heliId].Attackers[attacker.userID].DamageDealt += info.damageTypes.Total();
                        HeliSignalData[heliId].Attackers[attacker.userID].TotalHits++;
                        if (info.HitMaterial == 2306822461)
                            HeliSignalData[heliId].Attackers[attacker.userID].RotorHits++;

                        if (heliAI._currentState != PatrolHelicopterAI.aiState.DEATH)
                            HeliSignalData[heliId].LastAttacker = attacker;
                    }
                }
            }
            return null;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null || entity == null)
                return null;

            if (info.Initiator == null)
                return null;

            if (info.Initiator is PatrolHelicopter)
            {
                PatrolHelicopter heli = info.Initiator as PatrolHelicopter;
                if (heli == null)
                    return null;

                PatrolHelicopterAI heliAI = heli.myAI;
                if (heliAI == null)
                    return null;
                
                if (IsHeliSignal(heli.skinID))
                {
                    string heliProfile = heli._name;
                    if (heliProfile == null || heliProfile == string.Empty)
                        return null;
                    
                    if (config.heli.heliConfig.ContainsKey(heliProfile))
                    {
                        if (entity is BasePlayer)
                        {
                            BasePlayer player = entity as BasePlayer;
                            if (player == null)
                                return null;
                            
                            if (config.heli.heliConfig[heliProfile].BlockPlayerDamage && !IsOwnerOrFriend(heli.OwnerID, player.userID))
                            {
                                CancelHit(info);
                                return true;
                            }
                        }
                        else if (entity.OwnerID.IsSteamId())
                        {
                            if (config.heli.heliConfig[heliProfile].BlockOtherDamage && !IsOwnerOrFriend(heli.OwnerID, entity.OwnerID))
                            {
                                CancelHit(info);
                                return true;
                            }
                            if (config.heli.heliConfig[heliProfile].BlockOwnerDamage && IsOwnerOrFriend(heli.OwnerID, entity.OwnerID))
                            {
                                CancelHit(info);
                                return true;
                            }
                        }
                    }
                }
            }
            else if (info.Initiator.ShortPrefabName.Equals("napalm") || info.Initiator.ShortPrefabName.Equals("oilfireballsmall")) // If Heli destroyed
            {
                string heliProfile = info.Initiator.creatorEntity?._name;
                if (heliProfile == null || heliProfile == string.Empty)
                    return null;

                if (IsHeliSignal(info.Initiator.creatorEntity.skinID))
                {
                    var heliOwner = info.Initiator.creatorEntity.OwnerID;
                    if (heliOwner == null)
                        return null;
                    
                    if (entity is BasePlayer)
                    {
                        BasePlayer player = entity as BasePlayer;
                        if (player == null)
                            return null;
                        
                        if (config.heli.heliConfig[heliProfile].BlockPlayerDamage && !IsOwnerOrFriend(heliOwner, player.userID))
                        {
                            CancelHit(info);
                            return true;
                        }
                    }
                    else if (entity.OwnerID.IsSteamId())
                    {
                        if (config.heli.heliConfig[heliProfile].BlockOtherDamage && !IsOwnerOrFriend(heliOwner, entity.OwnerID))
                        {
                            CancelHit(info);
                            return true;
                        }
                        else if (config.heli.heliConfig[heliProfile].BlockOwnerDamage && IsOwnerOrFriend(heliOwner, entity.OwnerID))
                        {
                            CancelHit(info);
                            return true;
                        }
                    }
                }
            }
            return null;
        }

        private object OnEntityDestroy(BaseEntity heli)
        {
            Puts($"DEBUG: OnEntityDestroy hooked! {heli}");
            return null;
        }



        private object OnEntityKill(PatrolHelicopter heli)
        {
            if (heli == null)
                return null;

            if (!HeliSignalData.ContainsKey(heli.net.ID.Value))
                return null;
            
            HeliComponent heliComp = heli.GetComponent<HeliComponent>();
            if (heliComp == null)
                return null;
            
            if (heliComp.isRetiring)
                return null;
            
            var skinId = heli.skinID;
            if (IsHeliSignal(skinId))
            {
                var position = heli.transform.position;
                var ownerId = heli.OwnerID;
                var heliProfile = heli._name;
                var heliId = heli.net.ID.Value;

                var gridPos = PositionToGrid(heli.transform.position);
                if (gridPos == null)
                    gridPos = "Unknown";
                
                BasePlayer lastAttacker = HeliSignalData[heliId].LastAttacker;
                if (lastAttacker == null)
                    return null;

                var totalReward = config.heli.heliConfig[heliProfile].RewardPoints;
                var totalScrap = config.heli.heliConfig[heliProfile].ScrapReward;
                float damageThreshold = config.heli.heliConfig[heliProfile].DamageThreshold;

                if (config.rewards.enableRewards && totalReward > 0)
                {
                    if (plugins.Find(config.rewards.rewardPlugin))
                    {
                        if (config.rewards.shareRewards)
                        {
                            foreach (var playerId in HeliSignalData[heliId].Attackers.Keys)
                            {
                                float damageDealt = HeliSignalData[heliId].Attackers[playerId].DamageDealt;
                                if (damageDealt >= damageThreshold)
                                {
                                    var amount = totalReward / HeliSignalData[heliId].Attackers.Count(key => key.Value.DamageDealt >= damageThreshold);
                                    GiveReward(playerId, amount, heliProfile);
                                }
                            }
                        }
                        else
                        {
                            GiveReward(ownerId, totalReward, heliProfile);
                        }
                    }
                }

                if (config.rewards.enableScrap && totalScrap > 0)
                {
                    if (config.rewards.shareScrap)
                    {
                        foreach (var playerId in HeliSignalData[heliId].Attackers.Keys)
                        {
                            float damageDealt = HeliSignalData[heliId].Attackers[playerId].DamageDealt;
                            if (damageDealt >= damageThreshold)
                            {
                                var amount = totalScrap / HeliSignalData[heliId].Attackers.Count(key => key.Value.DamageDealt >= damageThreshold);
                                if (playerId.IsSteamId()) GiveScrap(playerId, amount, heliProfile);
                            }
                        }
                    }
                    else
                    {
                        GiveScrap(ownerId, totalScrap, heliProfile);
                    }
                }

                if (config.announce.killGlobal && !heli.myAI.isRetiring)
                {
                    TimeSpan timeSpan = TimeSpan.FromSeconds(UnityEngine.Time.realtimeSinceStartup - HeliSignalData[heliId].FirstHitTime);
                    string time = timeSpan.ToString(@"hh\:mm\:ss");
                    var heliDisplayName = config.heli.heliConfig[heliProfile].HeliName;

                    var message = string.Format(lang.GetMessage("HeliKilledTime", this), heliDisplayName, lastAttacker.displayName, gridPos, time);
                    Server.Broadcast(message, config.options.usePrefix ? config.options.chatPrefix : null, 0);
                }

                if (config.announce.reportGlobal && !heli.myAI.isRetiring)
                {
                    string topReport = string.Empty;
                    int count = 0;
                    foreach (var key in HeliSignalData[heliId].Attackers.Keys.OrderByDescending(x => HeliSignalData[heliId].Attackers[x].DamageDealt))
                    {
                        if (count >= config.announce.maxReported)
                            break;
                        
                        string playerName = HeliSignalData[heliId].Attackers[key].Name;
                        float damageDealt = HeliSignalData[heliId].Attackers[key].DamageDealt;
                        int totalHits = HeliSignalData[heliId].Attackers[key].TotalHits;
                        int rotorHits = HeliSignalData[heliId].Attackers[key].RotorHits;
                        double rotorAccuracy = ((double)rotorHits / (double)totalHits) * 100;
                        topReport += string.Format(lang.GetMessage("DamageReportItem", this), playerName, Math.Round(damageDealt, 2), Math.Round(rotorAccuracy, 2));
                        count++;
                    }

                    var dmgReport = string.Format(lang.GetMessage("DamageReport", this), topReport);
                    Server.Broadcast(dmgReport, config.options.usePrefix ? config.options.chatPrefix : null, 0);
                }

                if (config.discord.sendHeliKill && !heli.myAI.isRetiring)
                {
                    string discordMsg = string.Format(lang.GetMessage("DiscordKill", this), lastAttacker.displayName, heliProfile, gridPos);
                    SendToDiscord(discordMsg);
                }

                if (config.heli.heliConfig[heliProfile].LockedCratesToSpawn > 0)
                {
                    SpawnLockedCrates(ownerId, skinId, heliProfile, position);
                }

                List<BaseEntity> ents = new List<BaseEntity>();
                Vis.Entities(position, 20f, ents);
                foreach (var ent in ents)
                {
                    if ((ent is HelicopterDebris) || (ent is LockedByEntCrate) || (ent is FireBall))
                    {
                        ent.OwnerID = ownerId;
                        ent.skinID = skinId;
                        ent._name = heliProfile;

                        ProcessHeliEnt(ent);
                    }
                }

                if (BotReSpawn && BotReSpawn.IsLoaded && !heli.myAI.isRetiring)
                {
                    var botReSpawnProfile = config.heli.heliConfig[heliProfile].BotReSpawnProfile;
                    if (botReSpawnProfile != string.Empty)
                    BotReSpawn?.Call("AddGroupSpawn", position, botReSpawnProfile, $"{botReSpawnProfile}Group", 0);
                }

                timer.Once(5f, () =>
                {
                    if (HeliSignalData.ContainsKey(heliId))
                        HeliSignalData.Remove(heliId);
                });
            }
            return null;
        }

        private object CanHelicopterStrafeTarget(PatrolHelicopterAI heliAI, BasePlayer player)
        {
            PatrolHelicopter heli = heliAI?.helicopterBase;
            if (!heli) return null;

            if (!IsHeliSignal(heli.skinID)) return null;

            string heliProfile = heli._name;
            if (heliProfile == null || heliProfile == string.Empty)
                return false;
            
            if (!config.heli.heliConfig[heliProfile].TargetOtherPlayers && !IsOwnerOrFriend(player.userID, heli.OwnerID))
            {
                heliAI.ExitCurrentState();

                for (int i = 0; i < heliAI._targetList.Count; i++)
                {
                    if (heliAI._targetList[i].ply == player)
                    {
                        heliAI._targetList.Remove(heliAI._targetList[i]);
                        continue;
                    }
                }

                return false;
            }

            return null;
        }

        private object CanHelicopterUseNapalm(PatrolHelicopterAI heliAI) => CanHelicopterStrafe(heliAI);

        private object CanHelicopterStrafe(PatrolHelicopterAI heliAI)
        {
            PatrolHelicopter heli = heliAI?.helicopterBase;
            if (heli == null) return null;

            HeliComponent heliComp = heli.GetComponent<HeliComponent>();
            if (heliComp != null)
            {
                if (heliComp.isRetiring)
                    return false;
            }
            return null;
        }

        private object OnHelicopterTarget(HelicopterTurret turret, BasePlayer player)
        {
            PatrolHelicopter heli = turret._heliAI?.helicopterBase;
            PatrolHelicopterAI heliAI = turret?._heliAI;
            if (!heli || !heliAI) return null;
            
            if (IsHeliSignal(heli.skinID))
                return CanCustomHeliTarget(heliAI, player);

            return null;
        }

        private object CanHelicopterTarget(PatrolHelicopterAI heliAI, BasePlayer player)
        {
            PatrolHelicopter heli = heliAI?.helicopterBase;
            if (!heli) return null;

            if (IsHeliSignal(heli.skinID))
                return CanCustomHeliTarget(heliAI, player);

            return null;
        }

        private object CanCustomHeliTarget(PatrolHelicopterAI heliAI, BasePlayer player)
        {
            if (Vanish && (bool)Vanish?.Call("IsInvisible", player)) // Don't target players in Vanish
                return null;

            PatrolHelicopter heli = heliAI?.helicopterBase;
            string heliProfile = heli._name;

            if (heliProfile == null || heliProfile == string.Empty)
                return false;
            
            if (config.heli.heliConfig.ContainsKey(heliProfile))
            {
                if (!config.heli.heliConfig[heliProfile].TargetOtherPlayers && !IsOwnerOrFriend(player.userID, heli.OwnerID))
                {
                    HeliComponent heliComp = heli.GetComponent<HeliComponent>();
                    if (heliComp != null)
                    {
                        heliAI.interestZoneOrigin = heliComp.calledPosition + new Vector3(0f, 30f, 0f);
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
            if (heli == null) return null;

            if (IsHeliSignal(heli.skinID) && heliAI.IsAlive())
            {
                heliAI.leftGun.maxTargetRange = 0f;
                heliAI.rightGun.maxTargetRange = 0f;

                if (config.discord.sendHeliRetire)
                {
                    var gridPos = PositionToGrid(heli.transform.position);
                    if (gridPos == null)
                        gridPos = "Unknown";

                    BasePlayer player = FindPlayer(heli.OwnerID.ToString())?.Object as BasePlayer;
                    if (player == null)
                        return null;
                    
                    string discordMsg = string.Format(lang.GetMessage("DiscordRetire", this), heli._name, player.displayName, gridPos);
                    SendToDiscord(discordMsg);
                }

                timer.Once(2f, ()=>
                {
                    if (HeliSignalData.ContainsKey(heli.net.ID.Value))
                        HeliSignalData.Remove(heli.net.ID.Value);
                });
            }
            return null;
        }

        private object CanLootEntity(BasePlayer player, LockedByEntCrate entity)
        {
            if (entity?._name == null) return null;

            if (IsHeliSignal(entity.skinID))
            {
                string heliProfile = entity._name;
                if (heliProfile != null)
                {
                    if (config.heli.heliConfig.ContainsKey(heliProfile))
                    {
                        if (config.heli.heliConfig[heliProfile].ProtectCrates && entity.OwnerID != 0)
                        {
                            if (!IsOwnerOrFriend(player.userID, entity.OwnerID))
                            {
                                Message(player, "CannotLoot");
                                return false;
                            }
                        }
                    }
                }
            }
            return null;
        }

        private object OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (attacker == null || info == null)
                return null;

            BaseEntity entity = info.HitEntity;
            if (entity != null)
            {
                if (entity is ServerGib && IsHeliSignal(entity.skinID))
                {
                    var heliProfile = entity._name;
                    if (heliProfile != null)
                    {
                        if (config.heli.heliConfig.ContainsKey(heliProfile))
                        {
                            if (config.heli.heliConfig[heliProfile].ProtectGibs && entity.OwnerID != 0)
                            {
                                if (!IsOwnerOrFriend(attacker.userID, entity.OwnerID))
                                {
                                    Message(attacker, "CannotHarvest");
                                    CancelHit(info);
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        private void OnLootSpawn(LootContainer lootContainer)
        {
            NextTick(() =>
            {
                var skinId = lootContainer.skinID;
                if (IsHeliSignal(skinId))
                {
                    var heliProfile = lootContainer._name;
                    if (heliProfile == null)
                        return;

                    bool useCustomLoot = config.heli.heliConfig[heliProfile].Loot.UseCustomLoot;
                    if (lootContainer.ShortPrefabName.Contains("heli_crate") && useCustomLoot)
                        SpawnHeliCrateLoot(lootContainer, heliProfile);

                    bool useExtraLoot = config.heli.heliConfig[heliProfile].ExtraLoot.UseExtraLoot;
                    if (lootContainer.ShortPrefabName.Contains("heli_crate") && useExtraLoot)
                        AddExtraHeliCrateLoot(lootContainer, heliProfile);

                    bool useLockedCrateLoot = config.heli.heliConfig[heliProfile].LockedCrateLoot.UseLockedCrateLoot;
                    if (lootContainer.ShortPrefabName.Contains("codelockedhackablecrate") && useLockedCrateLoot)
                        SpawnLockedCrateLoot(lootContainer, heliProfile);
                }
            });
        }

        private string PositionToGrid(Vector3 position) => PhoneController.PositionToGridCoord(position);

        #endregion Oxide Hooks

        #region Core

        private void HeliSignalThrown(BasePlayer player, SupplySignal entity, Item signal)
        {
            if (signal.name == null || !config.heli.heliConfig.ContainsKey(signal.name))
                FixSignalInfo(signal);

            var skinId = signal.skin;
            var heliProfile = signal.name;
            var permSuffix = config.heli.heliConfig[heliProfile].GiveItemCommand.ToLower();
            var perm = $"{Name.ToLower()}.{permSuffix}";

            if (!HasPermission(player.UserIDString, perm))
            {
                Message(player, "Permission", heliProfile);
                entity.Kill();
                GiveHeliDrop(player, skinId, heliProfile, 1, "refund");
                return;
            }

            if (NoEscape && config.heli.UseNoEscape)
            {
                if ((bool)NoEscape.CallHook("IsRaidBlocked", player))
                {
                    Message(player, "RaidBlocked", heliProfile);
                    entity.Kill();
                    GiveHeliDrop(player, skinId, heliProfile, 1, "refund");
                    return;
                }
                else if ((bool)NoEscape.CallHook("IsCombatBlocked", player))
                {
                    Message(player, "CombatBlocked", heliProfile);
                    entity.Kill();
                    GiveHeliDrop(player, skinId, heliProfile, 1, "refund");
                    return;
                }
            }

            if (config.heli.playerCooldown > 0f && !HasPermission(player.UserIDString, permBypasscooldown))
            {
                float cooldown;
                if (PlayerCooldowns.TryGetValue(player.userID, out cooldown))
                {
                    TimeSpan time = TimeSpan.FromSeconds(cooldown - UnityEngine.Time.time);
                    Message(player, "CooldownTime", time.ToString(@"hh\:mm\:ss"));
                    entity.Kill();
                    GiveHeliDrop(player, skinId, heliProfile, 1, "refund");
                    return;
                }
                else if (config.heli.teamCooldown)
                {
                    foreach (var playerId in PlayerCooldowns.Keys)
                    {
                        if (PlayerCooldowns.TryGetValue(playerId, out cooldown) && IsOwnerOrFriend(player.userID, playerId))
                        {
                            TimeSpan time = TimeSpan.FromSeconds(cooldown - UnityEngine.Time.time);
                            Message(player, "TeamCooldownTime", time.ToString(@"hh\:mm\:ss"));
                            entity.Kill();
                            GiveHeliDrop(player, skinId, heliProfile, 1, "refund");
                            return;
                        }
                    }
                }

                cooldown = config.heli.playerCooldown;
                foreach (KeyValuePair<string, float> keyPair in config.heli.vipCooldowns)
                {
                    if (HasPermission(player.UserIDString, keyPair.Key))
                    {
                        cooldown = keyPair.Value;
                    }
                }

                ulong userId = player.userID;
                PlayerCooldowns[userId] = UnityEngine.Time.time + cooldown;
                timer.Once(cooldown, () =>
                {
                    if (PlayerCooldowns.ContainsKey(userId))
                    {
                        PlayerCooldowns.Remove(userId);
                    }
                });
            }

            HeliSignalComponent signalComponent = entity.gameObject.AddComponent<HeliSignalComponent>();
            if (signalComponent != null)
            {
                signalComponent.signal = entity;
                signalComponent.player = player;
                signalComponent.skinId = skinId;
                signalComponent.heliProfile = heliProfile;
            }
        }

        private void ProcessHeliEnt (BaseEntity entity)
        {
            if (entity != null)
            {
                NextTick(() =>
                {
                    var heliProfile = entity._name;
                    if (heliProfile != null)
                    {
                        if (entity is HelicopterDebris)
                        {
                            var debris = entity?.GetComponent<HelicopterDebris>() ?? null;
                            if (debris != null)
                            {
                                debris.InitializeHealth(config.heli.heliConfig[heliProfile].GibsHealth, config.heli.heliConfig[heliProfile].GibsHealth);
                                debris.SendNetworkUpdate();

                                if (config.heli.heliConfig[heliProfile].KillGibs)
                                    entity.Kill();
                                
                                else if (config.heli.heliConfig[heliProfile].DisableFire)
                                    debris.tooHotUntil = UnityEngine.Time.realtimeSinceStartup;

                                else if (config.heli.heliConfig[heliProfile].GibsHotTime > 0)
                                    debris.tooHotUntil = UnityEngine.Time.realtimeSinceStartup + config.heli.heliConfig[heliProfile].GibsHotTime;

                                if (config.heli.heliConfig[heliProfile].ProtectGibs && config.heli.heliConfig[heliProfile].UnlockGibs > 0)
                                {
                                    float unlockTime = config.heli.heliConfig[heliProfile].DisableFire ? config.heli.heliConfig[heliProfile].UnlockGibs :
                                                      (config.heli.heliConfig[heliProfile].FireDuration + config.heli.heliConfig[heliProfile].UnlockGibs);
                                    RemoveHeliOwner(entity, unlockTime);
                                }
                                debris.SendNetworkUpdateImmediate();
                            }
                        }
                        else if (entity is FireBall)
                        {
                            var fireball = entity?.GetComponent<FireBall>() ?? null;
                            if (fireball != null)
                            {
                                if (config.heli.heliConfig[heliProfile].DisableFire)
                                {
                                    fireball.enableSaving = false;
                                    entity.Kill();
                                }
                                else
                                {
                                    timer.Once(config.heli.heliConfig[heliProfile].FireDuration, () =>
                                    {
                                        if (entity != null || !entity.IsDestroyed || fireball != null)
                                        {
                                            fireball.enableSaving = false;
                                            entity.Kill();
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
                                                {
                                                    lockingEnt.enableSaving = false;
                                                    lockingEnt.Kill();
                                                }
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
                                        {
                                            lockingEnt.enableSaving = false;
                                            lockingEnt.Kill();
                                        }
                                    }
                                    crate.CancelInvoke(crate.Think);
                                    crate.SetLocked(false);
                                    crate.lockingEnt = null;
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
                });
            }
        }

        #endregion Core

        #region Loot

        private void SpawnHeliCrateLoot(LootContainer lootContainer, string heliProfile)
        {
            if (lootContainer == null || heliProfile == null)
                return;

            timer.Once(2f, () =>
            {

                lootContainer.inventory.capacity = 12; // unlock all slots
                lootContainer.inventory.Clear();
                ItemManager.DoRemoves();

                List<LootItem> lootTable = new List<LootItem>(config.heli.heliConfig[heliProfile].Loot.LootTable);
                List<LootItem> items = lootTable;

                if (items == null || lootTable == null)
                    return;

                var minItems = config.heli.heliConfig[heliProfile].Loot.MinCrateItems;
                var maxItems = config.heli.heliConfig[heliProfile].Loot.MaxCrateItems;
                int count = UnityEngine.Random.Range(minItems, maxItems + 1);
                int given = 0;

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
                    if (!config.heli.heliConfig[heliProfile].Loot.AllowDupes && count <= items.Count)
                        items.Remove(lootItem);
                    
                    // Re-add loot if items run out (if loot table not adequate)
                    if (items.Count == 0)
                        items.AddRange(lootTable);

                    var amount = UnityEngine.Random.Range(lootItem.AmountMin, lootItem.AmountMax + 1);
                    Item item = ItemManager.CreateByName(lootItem.ShortName, amount, lootItem.SkinId);
                    if (item != null)
                    {
                        item.name = lootItem.DisplayName;
                        
                        if (item.MoveToContainer(lootContainer.inventory))
                        {
                            lootContainer.inventory.MarkDirty();
                            given++;

                            if (item.amount == 0)
                                item.Remove(0f);
                            
                            continue;
                        }
                        item.Remove(0f);
                    }
                }
            });
        }

        private void AddExtraHeliCrateLoot (LootContainer lootContainer, string heliProfile)
        {
            if (lootContainer == null || heliProfile == null)
                return;

            timer.Once(2.25f, () =>
            {
                List<LootItem> lootTable = new List<LootItem>(config.heli.heliConfig[heliProfile].ExtraLoot.LootTable);
                List<LootItem> items = lootTable;

                if (items == null || lootTable == null)
                    return;
                
                lootContainer.inventory.capacity = 12; // unlock all slots
                var minItems = config.heli.heliConfig[heliProfile].ExtraLoot.MinExtraItems;
                var maxItems = config.heli.heliConfig[heliProfile].ExtraLoot.MaxExtraItems;
                int count = UnityEngine.Random.Range(minItems, maxItems + 1);
                int given = 0;

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
                    if (!config.heli.heliConfig[heliProfile].ExtraLoot.AllowDupes && count <= items.Count)
                        items.Remove(lootItem);
                    
                    // Re-add loot if items run out (if loot table not adequate)
                    if (items.Count == 0)
                        items.AddRange(lootTable);

                    var amount = UnityEngine.Random.Range(lootItem.AmountMin, lootItem.AmountMax + 1);
                    Item item = ItemManager.CreateByName(lootItem.ShortName, amount, lootItem.SkinId);
                    if (item != null)
                    {
                        item.name = lootItem.DisplayName;
                        
                        if (item.MoveToContainer(lootContainer.inventory))
                        {
                            lootContainer.inventory.MarkDirty();
                            given++;

                            if (item.amount == 0)
                                item.Remove(0f);
                            
                            continue;
                        }
                        item.Remove(0f);
                    }
                }
            });
        }

        private void SpawnLockedCrateLoot(LootContainer lootContainer, string heliProfile)
        {
            if (lootContainer == null || heliProfile == null)
                return;

            timer.Once(2f, () =>
            {
                lootContainer.inventory.capacity = 36; // unlock all slots
                lootContainer.inventory.Clear();
                ItemManager.DoRemoves();

                List<LootItem> lootTable = new List<LootItem>(config.heli.heliConfig[heliProfile].LockedCrateLoot.LootTable);
                List<LootItem> items = lootTable;

                if (items == null || lootTable == null)
                    return;

                var minItems = config.heli.heliConfig[heliProfile].LockedCrateLoot.MinLockedCrateItems;
                var maxItems = config.heli.heliConfig[heliProfile].LockedCrateLoot.MaxLockedCrateItems;
                int count = UnityEngine.Random.Range(minItems, maxItems + 1);
                int given = 0;

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
                    if (!config.heli.heliConfig[heliProfile].LockedCrateLoot.AllowDupes && count <= items.Count)
                        items.Remove(lootItem);
                    
                    // Re-add loot if items run out (if loot table not adequate)
                    if (items.Count == 0)
                        items.AddRange(lootTable);

                    var amount = UnityEngine.Random.Range(lootItem.AmountMin, lootItem.AmountMax + 1);
                    Item item = ItemManager.CreateByName(lootItem.ShortName, amount, lootItem.SkinId);
                    if (item != null)
                    {
                        item.name = lootItem.DisplayName;
                        
                        if (item.MoveToContainer(lootContainer.inventory))
                        {
                            lootContainer.inventory.MarkDirty();
                            given++;

                            if (item.amount == 0)
                                item.Remove(0f);
                            
                            continue;
                        }
                        item.Remove(0f);
                    }
                }
            });
        }

        private void SpawnLockedCrates(ulong ownerId, ulong skinId, string heliProfile, Vector3 position)
        {
            for (int i = 0; i < config.heli.heliConfig[heliProfile].LockedCratesToSpawn; i++)
            {
                Vector3 newPos = position + UnityEngine.Random.onUnitSphere * 5f;
                newPos.y = TerrainMeta.HeightMap.GetHeight(newPos) + 7f;
                HackableLockedCrate crate = GameManager.server.CreateEntity(hackableCrate, newPos, new Quaternion()) as HackableLockedCrate;
                if (crate == null)
                    return;

                crate.OwnerID = ownerId;
                crate.skinID = skinId;
                crate._name = heliProfile;
                crate.Spawn();

                crate.Invoke(new Action(crate.DelayedDestroy), config.heli.heliConfig[heliProfile].LockedCrateDespawn);

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
            if (IsHeliSignal(crate.skinID))
            {
                NextTick(()=>
                {
                    float hackTime = 900f - config.heli.heliConfig[crate._name].HackSeconds;
                    if (crate.hackSeconds > hackTime)
                    {
                        return;
                    }
                    crate.hackSeconds = hackTime;
                });
            }
        }

        private object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (crate?._name == null)
                return null;

            if (IsHeliSignal(crate.skinID))
            {
                string heliProfile = crate._name;
                if (heliProfile != null)
                {
                    if (config.heli.heliConfig.ContainsKey(heliProfile))
                    {
                        if (config.heli.heliConfig[heliProfile].ProtectCrates && crate.OwnerID != 0)
                        {
                            ulong crateId = crate.net.ID.Value;
                            if (LockedCrates.ContainsKey(crateId) && !IsOwnerOrFriend(player.userID, LockedCrates[crateId]))
                            {
                                Message(player, "CannotHack");
                                return false;
                            }
                        }
                    }
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
            webrequest.Enqueue(config.discord.webHookUrl, discordMessage, SendToDiscordCallback, this, RequestMethod.POST, header);
        }

        private void SendToDiscordCallback(int callbackCode, string callbackMsg)
        {
            if (callbackCode != 204)
                Puts($"ERROR: {callbackMsg}");
        }

        #endregion Discord Announcements

        #region API

        // Other devs can call these hooks to help with compatability with their plugins if needed
        object IsHeliSignalObject(ulong skinId) => IsHeliSignal(skinId) ? true : (object)null;

        // Play nice with FancyDrop
        object ShouldFancyDrop(NetworkableId netId)
        {
            var signal = BaseNetworkable.serverEntities.Find(netId) as BaseEntity;
            if (signal != null)
            {
                if (IsHeliSignal(signal.skinID))
                    return true;
            }
            return null;
        }

        // DEBUG: BELOW: Anticipating scenarios after Rust update.
        // In case FancyDrop sends ulong info, convert to NetworkableId and check that way.
        // Will remove whichever isn't needed in next plugin update
        object ShouldFancyDrop(ulong netId)
        {
            var signal = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BaseEntity;
            if (signal != null)
            {
                if (IsHeliSignal(signal.skinID))
                    return true;
            }
            return null;
        }

        // Play nice with AlphaLoot
        object CanPopulateLoot(LootContainer lootContainer)
        {
            return IsHeliSignal(lootContainer.skinID) ? true : (object)null;
        }

        #endregion API

        #region Rewards

        private void CheckRewardPlugin()
        {
            if (config.rewards.enableRewards || CanPurchaseAnySignal())
            {
                RewardPlugin = plugins.Find(config.rewards.rewardPlugin);
                if (RewardPlugin == null)
                    Puts($"{config.rewards.rewardPlugin} not found, giving rewards is not possible until loaded.");
            }
        }

        private void GiveReward(ulong playerId, double amount, string heliProfile)
        {
            foreach (KeyValuePair<string, float> keyPair in config.rewards.rewardMultipliers)
            {
                if (HasPermission(playerId.ToString(), keyPair.Key))
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
                        Message(player, "PointsGiven", (int)amount, config.rewards.rewardUnit, heliProfile);
                        break;
                    case "economics":
                        RewardPlugin?.Call("Deposit", playerId, amount);
                        Message(player, "PointsGiven", config.rewards.rewardUnit, (int)amount, heliProfile);
                        break;
                    default:
                        break;
                }
            }
        }

        private void GiveScrap(ulong playerId, int amount, string heliProfile)
        {
            foreach (KeyValuePair<string, float> keyPair in config.rewards.rewardMultipliers)
            {
                if (HasPermission(playerId.ToString(), keyPair.Key))
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
            }
        }

        #endregion Rewards

        #region Helpers

        private bool CanPurchaseAnySignal()
        {
            foreach (var key in config.heli.heliConfig.Keys)
            {
                if (config.heli.heliConfig[key].UseBuyCommand && config.purchasing.defaultCurrency != "Custom")
                {
                    return true;
                }
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

        private object FixSignalInfo(Item signal)
        {
            if (signal.name == null || !config.heli.heliConfig.ContainsKey(signal.name))
            {
                foreach (var key in config.heli.heliConfig.Keys)
                {
                    if (signal.skin == config.heli.heliConfig[key].SignalSkinID)
                    {
                        signal.name = key;
                        signal.skin = config.heli.heliConfig[key].SignalSkinID;
                        return signal;
                    }
                }
            }
            return signal;
        }

        public bool IsHeliSignal(ulong skinId)
        {
            if (skinId != 0)
            {
                foreach (var key in config.heli.heliConfig.Keys)
                {
                    if (skinId == config.heli.heliConfig[key].SignalSkinID)
                        return true;
                }
            }
            return false;
        }

        private void CancelHit(HitInfo info)
        {
            if (info != null)
            {
                info.damageTypes = new DamageTypeList();
                info.DoHitEffects = false;
                info.DidHit = false;
            }
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
                    {
                        heliComp.isRetiring = true;
                        if (HeliSignalData.ContainsKey(heliId))
                            HeliSignalData.Remove(heliId);
                    }

                    heliAI.Retire();
                }
            });
        }

        private bool GiveHeliDrop(BasePlayer player, ulong skinId, string dropName, int dropAmount, string reason)
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

        private bool HasPermission(string userId, string perm) => Instance.permission.UserHasPermission(userId, perm);

        #endregion Helpers

        #region Commands

        private void CmdReport(IPlayer player, string command, string[] args)
        {
            string activeHelis = String.Empty;
            int count = 0;
            int total = HeliSignalData.Count;

            if (!HasPermission(player.Id, permAdmin))
            {
                Message(player, "Permission", "hsreport command");
                return;
            }
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
                    if (IsHeliSignal(heli.skinID))
                    {
                        count++;

                        Vector3 position = heli.transform.position;
                        var gridPos = PositionToGrid(position);
                        if (gridPos == null)
                            gridPos = "Unknown";

                        HeliComponent heliComp = heli.GetComponent<HeliComponent>();
                        if (heliComp == null)
                            return;

                        BasePlayer owner = heliComp.owner;
                        if (owner == null)
                            return;
                        
                        string heliProfile = heliComp.heliProfile;
                        if (heliProfile == null)
                            return;

                        var message = String.Empty;
                        if (player.IsServer)
                            message = Lang("HeliReportItemCon", count, total, config.heli.heliConfig[heliProfile].HeliName, owner.displayName, gridPos,
                                      Math.Round((decimal)heli.health, 0), Math.Round((decimal)heli.weakspots[0].health, 0), Math.Round((decimal)heli.weakspots[1].health, 0), heliComp.heliAI._currentState);
                        else
                            message = Lang("HeliReportItem", count, total, config.heli.heliConfig[heliProfile].HeliName, owner.displayName, gridPos,
                                      Math.Round((decimal)heli.health, 0), Math.Round((decimal)heli.weakspots[0].health, 0), Math.Round((decimal)heli.weakspots[1].health, 0), heliComp.heliAI._currentState);
                        
                        activeHelis += ($"{message}");
                        message = String.Empty;
                    }
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

            bool didRetire = false;

            foreach (var netId in HeliSignalData.Keys)
            {
                bool doRetire = false;

                var heli = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as PatrolHelicopter;
                if (heli == null) return;
                var ownerId = heli.OwnerID;
                
                BasePlayer basePlayer = FindPlayer(ownerId.ToString())?.Object as BasePlayer;
                if (basePlayer == null) return;
                var playerId = basePlayer.userID;

                var heliComp = heli.GetComponent<HeliComponent>();
                if (heliComp == null) return;

                if (ownerId == basePlayer.userID)
                    doRetire = true;
                else if (config.heli.canTeamRetire && IsOwnerOrFriend(heli.OwnerID, basePlayer.userID))
                    doRetire = true;

                if (doRetire)
                {
                    heliComp.isRetiring = true;
                    heli.myAI.Retire();
                    didRetire = true;
                }
            }

            if (didRetire)
                Message(player, "RetiredHelis");
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
                Message(player, "BuySyntaxCmd", config.purchasing.buyCommand, string.Join( "|", HeliTypes));
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
                    list += ($"{config.heli.heliConfig[key].HeliName} : {priceFormat}\n");
                }
                Message(player, "PriceList", list);
                return;
            }

            string type = args[0].ToLower();
            ulong skinId = 0;
            string heliProfile = string.Empty;

            foreach (var key in config.heli.heliConfig.Keys)
            {
                if (type == config.heli.heliConfig[key].GiveItemCommand.ToLower())
                {
                    skinId = config.heli.heliConfig[key].SignalSkinID;
                    heliProfile = key;
                    break;
                }
            }
            
            if (!HeliTypes.Contains(type))
            {
                Message(player, "InvalidDrop", type);
                return;
            }
            if (!HasPermission(player.Id, permBuy))
            {
                Message(player, "BuyPermission", type);
                return;
            }
            if (!config.heli.heliConfig[heliProfile].UseBuyCommand)
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
                        var cost = config.heli.heliConfig[heliProfile].CostToBuy;
                        var balance = RewardPlugin?.Call("CheckPoints", basePlayer.userID);
                        if (balance != null)
                        {
                            if (cost <= Convert.ToInt32(balance))
                            {
                                if (GiveHeliDrop(basePlayer, skinId, heliProfile, 1, "purchase"))
                                {
                                    RewardPlugin?.Call("TakePoints", basePlayer.userID, cost);
                                    Message(player, "Receive", 1, heliProfile);
                                    return;
                                }
                                Message(player, "FullInventory", heliProfile);
                                return;
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
                        var cost = Convert.ToDouble(config.heli.heliConfig[heliProfile].CostToBuy);
                        var balance = RewardPlugin?.Call("Balance", basePlayer.userID);
                        if (balance != null)
                        {
                            if (cost <= Convert.ToDouble(balance))
                            {
                                if (GiveHeliDrop(basePlayer, skinId, heliProfile, 1, "purchase"))
                                {
                                    RewardPlugin?.Call("Withdraw", basePlayer.userID, cost);
                                    Message(player, "Receive", 1, heliProfile);
                                    return;
                                }
                                Message(player, "FullInventory", heliProfile);
                                return;
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
                        var cost = config.heli.heliConfig[heliProfile].CostToBuy;
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
                                        if (GiveHeliDrop(basePlayer, skinId, heliProfile, 1, "purchase"))
                                        {
                                            basePlayer.inventory.Take(null, itemDef.itemid, cost);
                                            Message(player, "Receive", 1, heliProfile);
                                        }
                                        else
                                        {
                                            Message(player, "FullInventory", heliProfile);
                                            return;
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
            if (!HasPermission(player.Id, permAdmin))
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

            if (skinId == 0)
            {
                Message(player, "InvalidDrop", type);
                return;
            }

            if (GiveHeliDrop(target, skinId, heliProfile, dropAmount, "give"))
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
        
        #endregion Commands

        #region Monos
        
        private class HeliSignalComponent : MonoBehaviour
        {
            public SupplySignal signal;
            public BasePlayer player;
            public PatrolHelicopter heli;
            public PatrolHelicopterAI heliAI;
            public Vector3 position;
            public ulong skinId;
            public string heliProfile;

            void Start()
            {
                Invoke(nameof(CustomExplode), config.options.signalFuseLength);
            }

            void CustomExplode()
            {
                position = signal.transform.position;
                if (SignalAborted())
                {
                    signal.Kill();
                    if (player != null || !player.IsAlive())
                    {
                        Instance.NextTick (()=> Instance.GiveHeliDrop(player, skinId, heliProfile, 1, "refund"));

                        if (config.heli.playerCooldown > 0f && PlayerCooldowns.ContainsKey(player.userID))
                            PlayerCooldowns.Remove(player.userID);
                    }
                }
                else
                {
                    heli = GameManager.server.CreateEntity(heliPrefab, new Vector3(), new Quaternion(), true) as PatrolHelicopter;
                    heli.OwnerID = player.userID;
                    heli.skinID = skinId;
                    heli._name = heliProfile;

                    position.y += config.heli.arrivalHeight;

                    heliAI = heli.GetComponent<PatrolHelicopterAI>();
                    heliAI.SetInitialDestination(position, config.heli.mapScaleDistance);

                    StrafeHeliAI strafeHeliAI = heli.gameObject.AddComponent<StrafeHeliAI>();
                    if (strafeHeliAI == null) return;
                    strafeHeliAI.heliProfile = heliProfile;
                    heli.Spawn();

                    Instance.NextTick(() =>
                    {
                        // Calling on NextTick to stop issues with AlphaLoot and other plugins
                        // Which alter Heli settings on entity spawn

                        heliAI._targetList.Add(new PatrolHelicopterAI.targetinfo(player, player));
                        var heliComp = heli.gameObject.AddComponent<HeliComponent>();
                        heliComp.heli = heli;
                        heliComp.heliAI = heliAI;
                        heliComp.owner = player;
                        heliComp.heliProfile = heliProfile;
                        heliComp.calledPosition = position;

                        HeliSignalData.Add(heli.net.ID.Value, new HeliStats());

                        if (config.heli.heliConfig[heliProfile].DespawnTime > 0)
                            Instance.RetireHeli(heliAI, heli.net.ID.Value, config.heli.heliConfig[heliProfile].DespawnTime);

                        float finishUp = config.options.smokeDuration;
                        if (finishUp == null || finishUp < 0) finishUp = 210f;
                        signal.Invoke(new Action(signal.FinishUp), finishUp);
                        signal.SetFlag(BaseEntity.Flags.On, true);
                        signal.SendNetworkUpdateImmediate(false);

                        var gridPos = Instance.PositionToGrid(position);
                        if (gridPos == null)
                            gridPos = "Unknown";
                        
                        if (config.announce.callGlobal)
                        {
                            var heliDisplayName = config.heli.heliConfig[heliProfile].HeliName; 
                            var message = Instance.Lang("HeliCalled", player.displayName, heliDisplayName, gridPos);
                            Instance.Server.Broadcast(message, config.options.usePrefix ? config.options.chatPrefix : null, 0);
                        }

                        if (config.discord.sendHeliCall)
                        {
                            string discordMsg = string.Format(Instance.lang.GetMessage("DiscordCall", Instance), player.displayName, heliProfile, gridPos);
                            Instance.SendToDiscord(discordMsg);
                        }

                        SetupHeli();
                    });
                }

                DestroyImmediate(this);
            }

            void SetupHeli()
            {
                Instance.NextTick(() =>
                {
                    heli.startHealth = config.heli.heliConfig[heliProfile].Health;
                    heli.InitializeHealth(heli.startHealth, heli.startHealth);
                    heli.weakspots[0].health = config.heli.heliConfig[heliProfile].MainRotorHealth;
                    heli.weakspots[1].health = config.heli.heliConfig[heliProfile].TailRotorHealth;
                    heli.weakspots[0].maxHealth = config.heli.heliConfig[heliProfile].MainRotorHealth;
                    heli.weakspots[1].maxHealth = config.heli.heliConfig[heliProfile].TailRotorHealth;
                    heli.maxCratesToSpawn = config.heli.heliConfig[heliProfile].CratesToSpawn;
                    heliAI.maxSpeed = config.heli.heliConfig[heliProfile].InitialSpeed;
                    heliAI.maxRotationSpeed = config.heli.heliConfig[heliProfile].MaxRotationSpeed;
                    var dist = Vector3Ex.Distance2D(heliAI.transform.position, heliAI.destination);
                    heliAI.GetThrottleForDistance(dist);
                    heli.bulletDamage = config.heli.heliConfig[heliProfile].BulletDamage;
                    heli.bulletSpeed = config.heli.heliConfig[heliProfile].BulletSpeed;
                    heliAI.leftGun.fireRate = config.heli.heliConfig[heliProfile].GunFireRate;
                    heliAI.rightGun.fireRate = heliAI.leftGun.fireRate;
                    heliAI.leftGun.burstLength = config.heli.heliConfig[heliProfile].BurstLength;
                    heliAI.rightGun.burstLength = heliAI.leftGun.burstLength;
                    heliAI.leftGun.timeBetweenBursts = config.heli.heliConfig[heliProfile].TimeBetweenBursts;
                    heliAI.rightGun.timeBetweenBursts = heliAI.leftGun.timeBetweenBursts;
                    heliAI.leftGun.maxTargetRange = config.heli.heliConfig[heliProfile].MaxTargetRange;
                    heliAI.rightGun.maxTargetRange = heliAI.leftGun.maxTargetRange;
                    heliAI.timeBetweenRockets = config.heli.heliConfig[heliProfile].TimeBetweenRockets;
                    heliAI.interestZoneOrigin = position;
                    heliAI.hasInterestZone = true;
                    heli.SendNetworkUpdateImmediate(true);
                });
            }

            public bool SignalAborted()
            {
                if (player == null || !player.IsAlive())
                    return true;

                if (Instance.IsInSafeZone(signal.transform.position))
                {
                    Instance.Message(player, "InSafeZone", heliProfile);
                    return true;
                }
                
                if (config.heli.globalLimit > 0)
                {
                    int heliCount = 0;
                    foreach (var netId in HeliSignalData.Keys)
                    {
                        var heli = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as PatrolHelicopter;
                        if (heli != null)
                            heliCount++;
                    }

                    if (heliCount >= config.heli.globalLimit)
                    {
                        Instance.Message(player, "GlobalLimit", config.heli.globalLimit);
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
            public PatrolHelicopterAI heliAI;
            public PatrolHelicopter heli;
            public Vector3 calledPosition;
            public bool isDying = false;
            public bool isReturning = false;
            public bool hasArrived = false;
            public bool isRetiring = false;
            public Vector3 strafePosition = Vector3.zero;
            public Vector3 lastStrafePosition = Vector3.zero;
            public bool isStrafing = false;
            public float timeSinceSeen = 0f;
            public float lastStrafeTime = 0;
            public bool isTeamDead = false;

            public List<BasePlayer> callingTeam = new List<BasePlayer>();

            void Start()
            {
                heli.SendNetworkUpdateImmediate();
                heli.UpdateNetworkGroup();

                GetCallingTeam();

                InvokeRepeating(nameof(PositionCheck), 0.1f, 0.1f);
                InvokeRepeating(nameof(UpdateTargetList), 1f, 1f);
            }
            
            public void PositionCheck()
            {
                if (heli != null || heliAI != null)
                {
                    if (heliAI._currentState == PatrolHelicopterAI.aiState.DEATH)
                    {
                        CancelInvoke(nameof(PositionCheck));
                        return;
                    }

                    float distance = Vector3Ex.Distance2D(heliAI.transform.position, heliAI.destination);
                    if (distance < 50f)
                    {
                        isReturning = false;
                        hasArrived = true;
                        heliAI.maxSpeed = config.heli.heliConfig[heliProfile].MaxSpeed;
                        heliAI.ExitCurrentState();
                        heliAI.SetIdealRotation(heliAI.GetYawRotationTo(heliAI.destination), -1f);

                        if (config.heli.heliConfig[heliProfile].MaxOrbitDuration > 0)
                        {
                            FieldInfo maxOrbitDuration = typeof(PatrolHelicopterAI).GetField("maxOrbitDuration", (BindingFlags.Instance | BindingFlags.NonPublic));
                            maxOrbitDuration.SetValue(heliAI, config.heli.heliConfig[heliProfile].MaxOrbitDuration);
                        }

                        heliAI.State_Orbit_Enter(config.heli.heliConfig[heliProfile].OrbitRadius);
                        CancelInvoke(nameof(PositionCheck));

                        if (config.heli.returnToPlayer)
                            InvokeRepeating(nameof(ReturnHeliToPlayer), 30f, 5f);
                    }
                }
            }

            public void UpdateTargetList()
            {
                if (isReturning || isRetiring)
                    return;

                if (heliAI._currentState == PatrolHelicopterAI.aiState.DEATH)
                {
                    CancelInvoke(nameof(UpdateTargetList));
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
                        bool isTargeted = false;
                        timeSinceSeen = item.TimeSinceSeen();
                        if (timeSinceSeen.ToString() == "-Infinity") // Target never seen, so don't enter strafe
                            continue;

                        if (!heliAI.PlayerVisible(player))
                        {
                            if (timeSinceSeen >= 5f && player.IsAlive())
                            {
                                if (heliAI.CanStrafe() && heliAI.IsAlive() && !isStrafing && !player.IsDead())
                                {
                                    if (player == heliAI.leftGun._target || player == heliAI.rightGun._target)
                                        isTargeted = true;
                                }

                                if (isTargeted)
                                {
                                    isStrafing = true;
                                    strafePosition = player.transform.position;
                                }
                            }
                        }
                        else
                        {
                            item.lastSeenTime = UnityEngine.Time.realtimeSinceStartup;
                        }
                    }
                }

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

                if (isStrafing && (UnityEngine.Time.realtimeSinceStartup - lastStrafeTime > 30f))
                {
                    heliAI.ExitCurrentState();
                    heliAI.State_Strafe_Enter(strafePosition);
                    lastStrafeTime = UnityEngine.Time.realtimeSinceStartup;
                    isStrafing = false;
                }
            }

            private void ReturnHeliToPlayer()
            {
                if (heliAI._currentState == PatrolHelicopterAI.aiState.DEATH || isRetiring)
                {
                    CancelInvoke(nameof(ReturnHeliToPlayer));
                    return;
                }
                
                BasePlayer target = owner;
                Vector3 returnPosition = new Vector3();
                if (target == null || !target.IsConnected || !target.IsAlive() || target.IsSleeping())
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
                                CancelInvoke(nameof(ReturnHeliToPlayer));
                                return;
                            default:
                                break;
                        }
                    }
                    isReturning = true;
                    heliAI.ExitCurrentState();
                    heliAI.interestZoneOrigin = returnPosition;
                    heliAI.hasInterestZone = true;
                    heliAI.State_Move_Enter(returnPosition + new Vector3(0, config.heli.arrivalHeight, 0));
                    InvokeRepeating(nameof(PositionCheck), 0.1f, 0.1f);
                }
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

            void OnDestroy()
            {
                CancelInvoke(nameof(PositionCheck));
                CancelInvoke(nameof(UpdateTargetList));
            }
        }

        public class StrafeHeliAI : BaseMonoBehaviour
        {
            public string heliProfile;
            public PatrolHelicopterAI heliAI;
            public PatrolHelicopter heli;

            public void Awake()
            {
                heli = this.GetComponent<PatrolHelicopter>();
                heliAI = heli.GetComponent<PatrolHelicopterAI>();

                InvokeRepeating(nameof(StrafeThink), 0.25f, 0.25f);
            }

            public void StrafeThink()
            {
                if (heliAI == null) return;
                switch (heliAI._currentState)
                {
                    case PatrolHelicopterAI.aiState.STRAFE:
                    {
                        if (heliAI.lastRocketTime > 0f)
                            return;

                        UpdateSerializeableFields();
                        return;
                    }
                    case PatrolHelicopterAI.aiState.DEATH:
                    {
                        DestroyImmediate(this);
                        return;
                    }
                    default:
                    {
                        return;
                    }
                }
            }

            public void UpdateSerializeableFields()
            {
                FieldInfo numRocketsLeft = typeof(PatrolHelicopterAI).GetField("numRocketsLeft", (BindingFlags.Public | BindingFlags.Instance));
                object numRockets = config.heli.heliConfig[heliProfile].MaxHeliRockets;
                numRocketsLeft.SetValue(heliAI, numRockets);

                FieldInfo useNapalm = typeof(PatrolHelicopterAI).GetField("useNapalm", (BindingFlags.Instance | BindingFlags.NonPublic));
                object napalmChance = UseNapalm();
                useNapalm.SetValue(heliAI, napalmChance);
            }

            public bool UseNapalm()
            {
                return (Oxide.Core.Random.Range(0f, 1f) <= config.heli.heliConfig[heliProfile].NapalmChance);
            }

            void OnDestroy()
            {
                CancelInvoke(nameof(StrafeThink));
            }
        }

        #endregion Monos
        
        #region Temporary Data

        private static Dictionary<ulong, HeliStats> HeliSignalData = new Dictionary<ulong, HeliStats>();
        private static Dictionary<ulong, float> PlayerCooldowns = new Dictionary<ulong, float>();
        private static Dictionary<ulong, ulong> LockedCrates = new Dictionary<ulong, ulong>();
        private static List<string> HeliTypes = new List<string>();

        private class HeliStats
        {
            public float FirstHitTime = 0f;
            public BasePlayer LastAttacker;
            public Dictionary<ulong, AttackersStats> Attackers = new Dictionary<ulong, AttackersStats>();
            public int WarningLevel = 0;
        }

        private class AttackersStats
        {
            public string Name;
            public float DamageDealt = 0f;
            public int TotalHits = 0;
            public int RotorHits = 0;
        }

        #endregion Temporary Data

        #region Config
        
        private class HeliData
        {
            [JsonProperty(PropertyName = "Helicopter display name")]
            public string HeliName { get; set; }
            [JsonProperty(PropertyName = "Skin ID of the custom Supply Signal")]
            public ulong SignalSkinID { get; set; }
            [JsonProperty(PropertyName = "Profile shortname (for use in permission and give command)")]
            public string GiveItemCommand { get; set; }

            // DEBUG: Remove this later
            [JsonProperty("Enable purchasing using custom currency via the /hsbuy command")]
            public bool GetUseBuyCommand { get { return UseBuyCommand; } set { UseBuyCommand = value; } }
            public bool ShouldSerializeGetUseBuyCommand() => false;
            [JsonProperty("Cost to purchase (using hsbuy command)")]
            public int GetCostToBuy { get { return CostToBuy; } set { CostToBuy = value; } }
            public bool ShouldSerializeGetCostToBuy() => false;
            //

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
            [JsonProperty(PropertyName = "Scrap amount issued when destroyed (if enabled)")]
            public int ScrapReward { get; set; }
            [JsonProperty(PropertyName = "Damage Threshold (Min damage player needs to contribute to get rewards)")]
            public float DamageThreshold { get; set; }
            [JsonProperty(PropertyName = "BotReSpawn profile to spawn at crash site (leave blank for not using)")]
            public string BotReSpawnProfile { get; set; }

            [JsonProperty(PropertyName = "Loot Options")]
            public LootOptions Loot { get; set; }
            
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
                [JsonProperty(PropertyName = "Chat Prefix")]
                public string chatPrefix { get; set; }
                [JsonProperty(PropertyName = "Use Chat Prefix")]
                public bool usePrefix { get; set; }
                [JsonProperty(PropertyName = "Supply Signal Fuse Length (Rust Default = 3.5)")]
                public float signalFuseLength { get; set; }
                [JsonProperty(PropertyName = "Supply Signal Smoke Duration (Rust Default = 210)")]
                public float smokeDuration { get; set; }
                [JsonProperty(PropertyName = "Disable vanilla Patrol helicopter")]
                public bool noVanillaHeli { get; set; }
                [JsonProperty(PropertyName = "Use this plugin to control stacking/combing Heli Signal items")]
                public bool useStacking { get; set; }
            }

            public class Announce
            {
                [JsonProperty(PropertyName = "Announce When Player Calls a Patrol Helicopter to Global Chat")]
                public bool callGlobal { get; set; }
                [JsonProperty(PropertyName = "Announce Helicopter Kill to Global Chat")]
                public bool killGlobal { get; set; }
                [JsonProperty(PropertyName = "Announce Damage Report to Global Chat")]
                public bool reportGlobal { get; set; }
                [JsonProperty(PropertyName = "Max Number Players Displayed in Damage Report")]
                public int maxReported { get; set; }
            }

            public class Discord
            {
                [JsonProperty(PropertyName = "Discord WebHook URL")]
                public string webHookUrl { get; set; }
                [JsonProperty(PropertyName = "Announce to Discord when helicopter is called")]
                public bool sendHeliCall { get; set; }
                [JsonProperty(PropertyName = "Announce to Discord when helicopter is killed")]
                public bool sendHeliKill { get; set; }
                [JsonProperty(PropertyName = "Announce to Discord when helicopter retires")]
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
                [JsonProperty(PropertyName = "Enable Scrap Reward")]
                public bool enableScrap { get; set; }
                [JsonProperty(PropertyName = "Share Scrap Between Players Above Damage Threshold")]
                public bool shareScrap { get; set; }
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
                [JsonProperty(PropertyName = "Force Helicopter to Return to Player if it Moves Too far Away")]
                public bool returnToPlayer { get; set; }
                [JsonProperty(PropertyName = "Force Helicopter to Return Even if Attacking Other Players")]
                public bool returnIfAttacking { get; set; }
                [JsonProperty(PropertyName = "Max Distance of Helicopter From Player Before Force Return")]
                public float maxDistanceFromPlayer { get; set; }
                [JsonProperty(PropertyName = "Max Distance Helicopter Can Be Damaged By Any Player (0 = Disabled)")]
                public float maxHitDistance { get; set; }
                [JsonProperty(PropertyName = "Map Scale Distance Away to Spawn Helicopter (Default: 1.25 = 1.25 x Map Size Distance)")]
                public float mapScaleDistance { get; set; }
                [JsonProperty(PropertyName = "Height of heli when it arrives at called location")]
                public float arrivalHeight { get; set; }
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
                [JsonProperty(PropertyName = "Cooldown Applies to Clan/Team/Friends (Requires Use Friends/Use Clan/Use Teams)")]
                public bool teamCooldown { get; set; }
                [JsonProperty(PropertyName = "VIP/Custom Cooldowns")]
                public Hash<string, float> vipCooldowns { get; set; }

                [JsonIgnore]
                public Permission permission;
                public void RegisterPermissions(Permission permission, HeliSignals plugin)
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
                public Dictionary<string, HeliData> heliConfig { get; set; }
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
                    chatPrefix = "<color=orange>[Heli Signals]</color>",
                    usePrefix = true,
                    signalFuseLength = 3.5f,
                    smokeDuration = 210f,
                    noVanillaHeli = false,
                    useStacking = true
                },
                announce = new ConfigData.Announce
                {
                    killGlobal = true,
                    callGlobal = true,
                    reportGlobal = true,
                    maxReported = 5
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
                    enableScrap = false,
                    shareScrap = false,
                    rewardMultipliers = new Hash<string, float>
                    {
                        ["helisignals.examplevip1"] = 1.25f,
                        ["helisignals.examplevip2"] = 1.50f,
                        ["helisignals.examplevip3"] = 1.75f
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
                    globalLimit = 2,
                    returnToPlayer = false,
                    returnIfAttacking = false,
                    maxDistanceFromPlayer = 500f,
                    maxHitDistance = 0f,
                    mapScaleDistance = 1.25f,
                    arrivalHeight = 20f,
                    RetireWarning = false,
                    WarningThreshold = 25,
                    retireOnKilled = false,
                    UseNoEscape = false,
                    playerCooldown = 3600f,
                    teamCooldown = true,
                    vipCooldowns = new Hash<string, float>
                    {
                        ["helisignals.examplevip1"] = 3000f,
                        ["helisignals.examplevip2"] = 2400f,
                        ["helisignals.examplevip3"] = 1800f
                    },
                    heliConfig = new Dictionary<string, HeliData>
                    {
                        [easyHeli] = new HeliData
                        {
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
                            DespawnTime = 1200f,
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
                            BotReSpawnProfile = "",

                            Loot = new HeliData.LootOptions
                            {
                                UseCustomLoot = false,
                                MinCrateItems = 2,
                                MaxCrateItems = 6,
                                AllowDupes = false,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "explosive.timed", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "Timed Explosive Charge" },
                                    new LootItem { ShortName = "example.shortname", Chance = 50, AmountMin = 1, AmountMax = 10, SkinId = 1234567890, DisplayName = "Example Display name" }
                                }
                            },
                            ExtraLoot = new HeliData.ExtraLootOptions
                            {
                                UseExtraLoot = false,
                                MinExtraItems = 1,
                                MaxExtraItems = 3,
                                AllowDupes = false,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "ammo.rocket.basic", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "Rocket" },
                                    new LootItem { ShortName = "example.shortname", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display name" }
                                }
                            },
                            LockedCrateLoot = new HeliData.LockedCrateLootOptions
                            {
                                UseLockedCrateLoot = false,
                                MinLockedCrateItems = 1,
                                MaxLockedCrateItems = 3,
                                AllowDupes = false,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "supply.signal", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "Supply Signal" },
                                    new LootItem { ShortName = "example.shortname", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display name" }
                                }
                            }
                        },
                        [medHeli] = new HeliData
                        {
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
                            DespawnTime = 1800f,
                            OwnerDamage = false,
                            TargetOtherPlayers = true,
                            BlockOwnerDamage = false,
                            BlockOtherDamage = false,
                            BlockPlayerDamage = false,
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
                            ScrapReward = 2000,
                            DamageThreshold = 200f,
                            BotReSpawnProfile = "",

                            Loot = new HeliData.LootOptions
                            {
                                UseCustomLoot = false,
                                MinCrateItems = 4,
                                MaxCrateItems = 8,
                                AllowDupes = false,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "explosive.timed", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "Timed Explosive Charge" },
                                    new LootItem { ShortName = "example.shortname", Chance = 50, AmountMin = 1, AmountMax = 10, SkinId = 1234567890, DisplayName = "Example Display name" }
                                }
                            },
                            ExtraLoot = new HeliData.ExtraLootOptions
                            {
                                UseExtraLoot = false,
                                MinExtraItems = 1,
                                MaxExtraItems = 3,
                                AllowDupes = false,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "ammo.rocket.basic", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "Rocket" },
                                    new LootItem { ShortName = "example.shortname", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display name" }
                                }
                            },
                            LockedCrateLoot = new HeliData.LockedCrateLootOptions
                            {
                                UseLockedCrateLoot = false,
                                MinLockedCrateItems = 1,
                                MaxLockedCrateItems = 3,
                                AllowDupes = false,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "supply.signal", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "Supply Signal" },
                                    new LootItem { ShortName = "example.shortname", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display name" }
                                }
                            }
                        },
                        [hardHeli] = new HeliData
                        {
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
                            DespawnTime = 2400f,
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
                            DamageThreshold = 400f,
                            BotReSpawnProfile = "",

                            Loot = new HeliData.LootOptions
                            {
                                UseCustomLoot = false,
                                MinCrateItems = 6,
                                MaxCrateItems = 10,
                                AllowDupes = false,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "explosive.timed", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "Timed Explosive Charge" },
                                    new LootItem { ShortName = "example.shortname", Chance = 50, AmountMin = 1, AmountMax = 10, SkinId = 1234567890, DisplayName = "Example Display name" }
                                }
                            },
                            ExtraLoot = new HeliData.ExtraLootOptions
                            {
                                UseExtraLoot = false,
                                MinExtraItems = 1,
                                MaxExtraItems = 3,
                                AllowDupes = false,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "ammo.rocket.basic", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "Rocket" },
                                    new LootItem { ShortName = "example.shortname", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display name" }
                                }
                            },
                            LockedCrateLoot = new HeliData.LockedCrateLootOptions
                            {
                                UseLockedCrateLoot = false,
                                MinLockedCrateItems = 1,
                                MaxLockedCrateItems = 3,
                                AllowDupes = false,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "supply.signal", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "Supply Signal" },
                                    new LootItem { ShortName = "example.shortname", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display name" }
                                }
                            }
                        },
                        [eliteHeli] = new HeliData
                        {
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
                            DespawnTime = 3600f,
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
                            DamageThreshold = 600f,
                            BotReSpawnProfile = "",

                            Loot = new HeliData.LootOptions
                            {
                                UseCustomLoot = false,
                                MinCrateItems = 8,
                                MaxCrateItems = 12,
                                AllowDupes = false,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "explosive.timed", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "Timed Explosive Charge" },
                                    new LootItem { ShortName = "example.shortname", Chance = 50, AmountMin = 1, AmountMax = 10, SkinId = 1234567890, DisplayName = "Example Display name" }
                                }
                            },
                            ExtraLoot = new HeliData.ExtraLootOptions
                            {
                                UseExtraLoot = false,
                                MinExtraItems = 1,
                                MaxExtraItems = 3,
                                AllowDupes = false,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "ammo.rocket.basic", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "Rocket" },
                                    new LootItem { ShortName = "example.shortname", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display name" }
                                }
                            },
                            LockedCrateLoot = new HeliData.LockedCrateLootOptions
                            {
                                UseLockedCrateLoot = false,
                                MinLockedCrateItems = 1,
                                MaxLockedCrateItems = 3,
                                AllowDupes = false,
                                LootTable = new List<LootItem>
                                {
                                    new LootItem { ShortName = "supply.signal", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 0, DisplayName = "Supply Signal" },
                                    new LootItem { ShortName = "example.shortname", Chance = 50, AmountMin = 1, AmountMax = 2, SkinId = 1234567890, DisplayName = "Example Display name" }
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
                    Puts($"Exception: {ex}");
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
            if (config.Version < new VersionNumber(1, 0, 10))
            {
                config.heli.arrivalHeight = defaultConfig.heli.arrivalHeight;
                foreach (var key in config.heli.heliConfig.Keys)
                {
                    config.heli.heliConfig[key].InitialSpeed = 42f;
                    config.heli.heliConfig[key].OrbitRadius = 75f;
                }
            }
            if (config.Version < new VersionNumber(1, 0, 12))
            {
                config.heli.UseNoEscape = defaultConfig.heli.UseNoEscape;
                foreach (var key in config.heli.heliConfig.Keys)
                {
                    config.heli.heliConfig[key].BlockOtherDamage = false;
                    config.heli.heliConfig[key].BlockOwnerDamage = false;
                    config.heli.heliConfig[key].NewTargetRange = 150f;
                    config.heli.heliConfig[key].NapalmChance = 0.75f;
                    config.heli.heliConfig[key].BotReSpawnProfile = "";
                }
            }
            if (config.Version < new VersionNumber(1, 0, 14))
            {
                foreach (var key in config.heli.heliConfig.Keys)
                {
                    config.heli.heliConfig[key].MaxHeliRockets = 12;
                    config.heli.heliConfig[key].MaxOrbitDuration = 30f;
                    config.heli.heliConfig[key].RocketDamageScale = 1.0f;
                    config.heli.heliConfig[key].BlockPlayerDamage = false;
                    config.heli.heliConfig[key].TargetOtherPlayers = true;
                }
            }
            if (config.Version < new VersionNumber(1, 0, 18))
            {
                config.heli.teamCooldown = defaultConfig.heli.teamCooldown;
                config.options.noVanillaHeli = defaultConfig.options.noVanillaHeli;
                config.heli.RetireWarning = defaultConfig.heli.RetireWarning;
                config.heli.WarningThreshold = defaultConfig.heli.WarningThreshold;
            }
            if (config.Version < new VersionNumber(1, 0, 20))
            {
                config.heli.retireOnKilled = defaultConfig.heli.retireOnKilled;
                config.discord = new ConfigData.Discord();
                config.discord.webHookUrl = defaultConfig.discord.webHookUrl;
                config.discord.sendHeliCall = defaultConfig.discord.sendHeliCall;
                config.discord.sendHeliKill = defaultConfig.discord.sendHeliKill;
                config.discord.sendHeliRetire = defaultConfig.discord.sendHeliRetire;
            }
            if (config.Version < new VersionNumber(1, 0, 21))
            {
                config.announce.reportGlobal = defaultConfig.announce.reportGlobal;
                config.announce.maxReported = defaultConfig.announce.maxReported;
            }
            if (config.Version < new VersionNumber(1, 0, 22))
            {
                config.options.useStacking = defaultConfig.options.useStacking;
                config.purchasing.buyCommand = defaultConfig.purchasing.buyCommand;

                foreach (var key in config.heli.heliConfig.Keys)
                {
                    config.heli.heliConfig[key].LockedCratesToSpawn = 0;
                    config.heli.heliConfig[key].HackSeconds = 900f;
                    config.heli.heliConfig[key].LockedCrateDespawn = 7200f;
                }
            }
            if (config.Version < new VersionNumber(1, 0, 24))
            {
                config.heli.retireCommand = defaultConfig.heli.retireCommand;
                config.heli.canTeamRetire = defaultConfig.heli.canTeamRetire;

                foreach (var key in config.heli.heliConfig.Keys)
                {
                    config.heli.heliConfig[key].LockedCratesToSpawn = 0;
                    config.heli.heliConfig[key].HackSeconds = 900f;
                    config.heli.heliConfig[key].LockedCrateDespawn = 7200f;
                }
            }
            if (config.Version < new VersionNumber(1, 0, 25))
            {
                foreach (var key in config.heli.heliConfig.Keys)
                {
                    config.heli.heliConfig[key].LockedCrateLoot = new HeliData.LockedCrateLootOptions();
                    config.heli.heliConfig[key].LockedCrateLoot.UseLockedCrateLoot = false;
                    config.heli.heliConfig[key].LockedCrateLoot.MinLockedCrateItems = 6;
                    config.heli.heliConfig[key].LockedCrateLoot.MaxLockedCrateItems = 18;
                    config.heli.heliConfig[key].LockedCrateLoot.AllowDupes = false;
                    config.heli.heliConfig[key].LockedCrateLoot.LootTable = new List<LootItem>();
                }
            }
            config.Version = Version;
            SaveConfig();
            Puts("Config update complete!");
        }
        
        #endregion Config
    }
}