using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/*2.2.2
 * Fixed team rewards.
 * Logic cleanup.
 */

/*2.2.1
 * Added player kill reward amounts.
 * Fixed team options.
 * Logic cleanup.
 */

/*2.1.9
 * Fixed team killing reward.
 */

/*2.1.8
 * Fixed NRE.
 */

/*2.1.7
 * Updated for Sept 2023 Force.
 */

/*2.1.6
 * Config grammar fixes.
 */

/*2.1.5
 * Removed animal ID list in config.
 * Added scientist/human NPC enable/disable in config.
 */

/* 2.1.4
 * Added animal enable/disable in config.
 */

/* 2.1.3
 * Added support for ChickenBow plugin.
 */

/* 2.1.1
 * Added New Missle Silo scientists.
 * Cleanup.
 */

/* 2.1.0
 * Fixed Team tag colors.
 * Removed waste logic.
 */

/* 2.0.2
 * Added Xmas npc ID's.
 */

/* 2.0.1
 * Wording/clarity/ect.
 * Added more npc ID's.
 */

/*2.0.0
 * Rewrote plugin to be more performant/universal for feature changes and code structure enhancements.
 */

/* list of prefab ID's (NPC Vehicles)
 * 3029415845 patrol heli
 * 1675349834 ch47
 * 1456850188 bradley
 */

/* NPC's
 * 732025282 tunnel dweller
 * 1605597847 underwater dweller
 * 1536035819 heavy scientist
 * 4272904018 scientist oil rig
 * 548379897 scientist roam
 * 4199494415 scientist patrol
 * 529928930 scientistnpc_roamtethered
 * 2390854225 scientistnpc_peacekeeper
 * 2066159302 scientistnpc_junkpile_pistol
 * 1410044857 scientistnpc_full_shotgun
 * 712785714 scientistnpc_full_pistol
 * 3595426380 scientistnpc_full_mp5
 * 3763080634 scientistnpc_full_lr300
 * 1539172658 scientistnpc_full_any
 * 4293908444 scientistnpc_excavator
 * 1017671955 scientistnpc_ch47_gunner
 * 881071619 scientistnpc_cargo_turret_lr300
 * 1639447304 scientistnpc_cargo_turret_any
 * 3623670799 scientistnpc_cargo
 * 1172642608 gingerbread_meleedungeon
 * 2992757580 gingerbread_dungeon
 */

/* animals
 * 502341109 boar
 * 1799741974 bear
 * 3880446623 horse
 * 2421623959 testridable horse
 * 2144238755 wolf
 * 152398164 chicken
 * 1378621008 stag
 * 749308997 polarbear
 * 947646353 shark
 */

namespace Oxide.Plugins
{
    [Info("DTags", "Gt403cyl2", "2.2.2")]
    [Description("Rewards players with Dog Tags for killing players, NPCs or animals.")]
    internal class DTags : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ChickenBow;

        private const string PermUse = "dtags.use";
        private const string PermRed = "dtags.red";
        private const string PermBlue = "dtags.blue";
        private const string PermSilver = "dtags.silver";

        private static int _prc = 0; //Players
        private static int _arc = 0; //Animals
        private static int _tdnrc = 0; //TunnelDweller
        private static int _hrc = 0; //HeavyScientist
        private static int _scrc = 0; //Scientists
        private static int _brrc = 0; //Bradley
        private static int _ahrc = 0; //AttackHeli

        private static readonly int Dtr = -602717596; //Red
        private static readonly int Dtb = 1036321299; //Blue
        private static readonly int Dts = 1223900335; //Silver

        #endregion Fields

        #region Config

        private ConfigData _config;

        private class ConfigData
        {
            public Options Options = new Options();
            public Rewards Rewards = new Rewards();
            public Colors Colors = new Colors();
            public Amounts Amounts = new Amounts();

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        private class Options
        {
            [JsonProperty("Player dog tags drop in body bag")]
            public bool ptbag = true;

            [JsonProperty("Player names on tags")]
            public bool pNametags = true;

            [JsonProperty("Use team tags")]
            public bool ptt = true;
        }

        private class Rewards
        {
            [JsonProperty("Bradley rewards")]
            public bool ebradley = true;

            [JsonProperty("Heli rewards")]
            public bool eheli = true;

            [JsonProperty("Animal rewards")]
            public bool eanimals = true;

            [JsonProperty("Scientist/Human NPC rewards")]
            public bool escientist = true;
        }

        private class Colors
        {
            [JsonProperty("Player dog tags. (Blue / Red / Silver)")]
            public string plTags = "Silver";

            [JsonProperty("Animal dog tags. (Blue / Red / Silver)")]
            public string aniTags = "Red";

            [JsonProperty("Scientists dog tags. (Blue / Red / Silver)")]
            public string sciTags = "Red";

            [JsonProperty("Heavy Scientist dog tags. (Blue / Red / Silver)")]
            public string hsTags = "Red";

            [JsonProperty("Tunnel Dweller dog tags. (Blue / Red / Silver)")]
            public string tdTags = "Red";

            [JsonProperty("Attack Heli dog tags. (Blue / Red / Silver)")]
            public string ahTags = "Red";

            [JsonProperty("Bradley dog tags. (Blue / Red / Silver)")]
            public string brTags = "Red";
        }

        private class Amounts
        {
            [JsonProperty("From Players. (Default is 1)")]
            public int planTags = 1;

            [JsonProperty("From Animals. (Default is 1)")]
            public int aninTags = 1;

            [JsonProperty("From Scientists. (Default is 1)")]
            public int scinTags = 1;

            [JsonProperty("From Heavy Scientists. (Default is 1)")]
            public int hsnTags = 1;

            [JsonProperty("From Tunnel Dwellers. (Default is 1)")]
            public int tdnTags = 1;

            [JsonProperty("From Attack Heli. (Default is 1)")]
            public int ahnTags = 1;

            [JsonProperty("From Bradley. (Default is 1)")]
            public int brnTags = 1;
        }

        protected override void LoadDefaultConfig() => _config = new ConfigData();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<ConfigData>();

                if (!_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintWarning($"{Name} Configuration appears to be outdated; Updating and saving.");
                    SaveConfig();
                }
            }
            catch (FileNotFoundException)
            {
                PrintWarning($"No {Name} configuration file found, creating default.");
                LoadDefaultConfig();
                SaveConfig();
            }
            catch (JsonReaderException)
            {
                PrintError($"{Name} Configuration file contains invalid JSON, creating default.");
            }
        }

        protected override void SaveConfig()
        {
            PrintWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        private void Init()
        {
            permission.RegisterPermission(PermUse, this);
            permission.RegisterPermission(PermRed, this);
            permission.RegisterPermission(PermBlue, this);
            permission.RegisterPermission(PermSilver, this);
            Unsubscribe(nameof(OnEntityTakeDamage));
        }

        private void OnServerInitialized()
        {
            switch (_config.Colors.plTags)
            {
                case "Red":
                    _prc = Dtr;
                    break;

                case "Blue":
                    _prc = Dtb;
                    break;

                case "Silver":
                    _prc = Dts;
                    break;
            }

            switch (_config.Colors.aniTags)
            {
                case "Red":
                    _arc = Dtr;
                    break;

                case "Blue":
                    _arc = Dtb;
                    break;

                case "Silver":
                    _arc = Dts;
                    break;
            }

            switch (_config.Colors.tdTags)
            {
                case "Red":
                    _tdnrc = Dtr;
                    break;

                case "Blue":
                    _tdnrc = Dtb;
                    break;

                case "Silver":
                    _tdnrc = Dts;
                    break;
            }

            switch (_config.Colors.hsTags)
            {
                case "Red":
                    _hrc = Dtr;
                    break;

                case "Blue":
                    _hrc = Dtb;
                    break;

                case "Silver":
                    _hrc = Dts;
                    break;
            }

            switch (_config.Colors.sciTags)
            {
                case "Red":
                    _scrc = Dtr;
                    break;

                case "Blue":
                    _scrc = Dtb;
                    break;

                case "Silver":
                    _scrc = Dts;
                    break;
            }

            switch (_config.Colors.brTags)
            {
                case "Red":
                    _brrc = Dtr;
                    break;

                case "Blue":
                    _brrc = Dtb;
                    break;

                case "Silver":
                    _brrc = Dts;
                    break;
            }

            switch (_config.Colors.ahTags)
            {
                case "Red":
                    _ahrc = Dtr;
                    break;

                case "Blue":
                    _ahrc = Dtb;
                    break;

                case "Silver":
                    _ahrc = Dts;
                    break;
            }

            //if (_config.Rewards.eheli)
            //    Subscribe(nameof(OnHelicopterAttacked));
            if (_config.Rewards.eheli)
                Subscribe(nameof(OnEntityTakeDamage));
        }

        #endregion Config

        #region Hooks

        private object OnPlayerDeath(BasePlayer victim, HitInfo info)
        {
            if (victim == null || info?.InitiatorPlayer == null || !victim.userID.IsSteamId() || !info.InitiatorPlayer.userID.IsSteamId()) return null;
            if (victim == info.InitiatorPlayer) return null;
            if (!permission.UserHasPermission(info.InitiatorPlayer.UserIDString, PermUse)) return null;

            if (info.InitiatorPlayer.currentTeam == 0 || info.InitiatorPlayer.currentTeam != 0 && info.InitiatorPlayer.currentTeam != victim.currentTeam)
            {
                Item dtn = ItemManager.CreateByItemID(_prc, _config.Amounts.planTags);
                if (_config.Options.pNametags) dtn.name = $"{victim.displayName}'s DogTags";

                if (_config.Options.ptt && info.InitiatorPlayer.currentTeam != 0 && victim.currentTeam != 0)
                {
                    if (permission.UserHasPermission(info.InitiatorPlayer.UserIDString, PermRed)) _prc = Dtr;
                    if (permission.UserHasPermission(info.InitiatorPlayer.UserIDString, PermBlue)) _prc = Dtb;
                    if (permission.UserHasPermission(info.InitiatorPlayer.UserIDString, PermSilver)) _prc = Dts;
                }

                if (_config.Options.ptbag) victim.GiveItem(dtn);
                else info.InitiatorPlayer.GiveItem(dtn);
            }

            return null;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;

            if (_config.Rewards.eheli && entity.prefabID == 3029415845)
            {
                Give(_heliLastAttacker, _ahrc, _config.Amounts.ahnTags);
                _heliLastAttacker = null;
                return;
            }

            if (info == null || info.Initiator == null) return;
            if (info.InitiatorPlayer == null || !(info.Initiator is BasePlayer)) return;
            if (entity == info.Initiator) return;

            if (ChickenBow != null && ChickenBow.IsLoaded)
            {
                if (entity is Chicken && Convert.ToBoolean(ChickenBow.Call("IsSpawnedChicken", entity.net.ID.Value))) return;
            }

            var attacker = info.Initiator.ToPlayer();
            if (!permission.UserHasPermission(attacker.UserIDString, PermUse)) return;

            //Puts($"{entity.prefabID} {entity.ShortPrefabName} was killed");

            switch (entity.prefabID)
            {
                case 732025282: // tunnel
                case 1605597847: // underwater dwellers
                    {
                        if (!_config.Rewards.escientist) return;
                        Give(attacker, _tdnrc, _config.Amounts.tdnTags);
                        return;
                    }

                case 1536035819: // scientist heavy
                case 2390854225: // scientistnpc_peacekeeper
                    {
                        if (!_config.Rewards.escientist) return;
                        Give(attacker, _hrc, _config.Amounts.hsnTags);
                        return;
                    }

                case 4272904018: // scientist npc oil rig
                case 548379897: // roam
                case 4199494415: // patrol
                case 529928930: // scientistnpc_roamtethered
                case 2066159302: // scientistnpc_junkpile_pistol
                case 1410044857: // scientistnpc_full_shotgun
                case 712785714: // scientistnpc_full_pistol
                case 3595426380: // scientistnpc_full_mp5
                case 3763080634: // scientistnpc_full_lr300
                case 1539172658: // scientistnpc_full_any
                case 4293908444: // scientistnpc_excavator
                case 1017671955: // scientistnpc_ch47_gunner
                case 881071619: // scientistnpc_cargo_turret_lr300
                case 1639447304: // scientistnpc_cargo_turret_any
                case 3623670799: // scientistnpc_cargo
                case 1172642608: // gingerbread_meleedungeon
                case 2992757580: // gingerbread_dungeon
                case 4134517186: //scientistnpc_roam_nvg_variant
                    {
                        if (!_config.Rewards.escientist) return;
                        Give(attacker, _scrc, _config.Amounts.scinTags);
                        return;
                    }

                case 1456850188: // Bradley
                    {
                        if (!_config.Rewards.ebradley) return;
                        Give(attacker, _brrc, _config.Amounts.brnTags);
                        break;
                    }

                case 502341109: // boar
                case 1799741974: // bear
                case 3880446623: // horse
                case 2421623959: // testridable horse
                case 2144238755: // wolf
                case 152398164: // chicken
                case 1378621008: // stag
                case 749308997: // polarbear
                case 947646353: // shark
                    {
                        if (!_config.Rewards.eanimals) return;
                        Give(attacker, _arc, _config.Amounts.aninTags);
                        return;
                    }
            }
        }

        #endregion Hooks

        #region Patrolheli

        private BasePlayer _heliLastAttacker;

        private void OnEntityTakeDamage(PatrolHelicopter heli, HitInfo info)
        {
            if (heli == null || !(heli is PatrolHelicopter) || info == null || info.InitiatorPlayer == null) return;
            _heliLastAttacker = info.InitiatorPlayer;
        }

        #endregion Patrolheli

        #region Helpers

        private void Give(BasePlayer player, int itemID, int amount = 1)
        {
            player?.GiveItem(ItemManager.CreateByItemID(itemID, amount));
        }

        #endregion Helpers
    }
}