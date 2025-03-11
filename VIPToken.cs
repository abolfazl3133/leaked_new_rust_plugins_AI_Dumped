using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/* Changes 1.1.8
 * Fixed an issue where reset commands wouldn't run when there wasn't a BasePlayer.
 * Added stack handling config option
 * Updated to prevent Skill Tree's Ration perk from affecting tokens.
 * Added console command: givetoken <target> <tier> [optional: amount]
 * Updated dictionaries to be case insensitive for keys.
 * Added console command: removevip <target> <tier>
 * Patched for October update.
 */

namespace Oxide.Plugins
{
    [Info("VIPToken", "imthenewguy", "1.1.8")]
    [Description("Monetary VIP token")]
    class VIPToken : RustPlugin
    {
        #region Config

        public Configuration config;
        const string prefix = "<color=#00F7FF>[VIP Token]:</color>";

        List<ulong> tokenskins = new List<ulong>();

        public class Configuration
        {
            [JsonProperty("Maximum VIP days that a player can accumulate per tier")]
            public int max_vip_days = 90;

            [JsonProperty("Sound effect when purchasing or consuming a token")]
            public string purchase_effect = "assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab";

            [JsonProperty("Sound effect when consuming a token. Set it to nothing if you do not want an effect")]
            public string consume_effect = "assets/prefabs/misc/easter/painted eggs/effects/gold_open.prefab";

            [JsonProperty("Password for command verification (make one up)")]
            public string verification_password = "ChangeThisPassword124u5109123";

            [JsonProperty("Chat commands for opening the menu.")]
            public string[] chat_commands = { "tokenmenu", "tokenbalance", "storetoken", "redeemtoken" };

            [JsonProperty("Date time format (case sensitive): dd == day. MM == month. yyyy == year")]
            public string date_format = "dd-MM-yyyy";

            [JsonProperty("Prevent players from consuming a token if they are already in the group?")]
            public bool prevent_redemption = true;

            [JsonProperty("How often should the plugin check to see if a player has run out of VIP [seconds]?")]
            public float check_rate = 3600f;

            [JsonProperty("Should VIPTokens handle stacking of VIP token items? [set to false if using a stacks plugin]")]
            public bool handle_stacking = true;

            [JsonProperty("Add your vip tiers and commands here. Use {id} in place of a players userid and {name} in place of their name.")]
            public Dictionary<string, VIPInfo> vip_levels = new Dictionary<string, VIPInfo>(StringComparer.InvariantCultureIgnoreCase)
            {
                          
            };           

            public class VIPInfo
            {
                public string name;
                public string time_type = "day";
                public int time_to_add;
                public string vip_group;
                public string vip_description;
                public bool remove_tokens_on_wipe = false;
                public TokenInfo token_item;
                public CommandInfo _command;
                public List<CommandInfo> _commands;
                public List<CommandInfo> remove_commands;
            }

            public class TokenInfo
            {
                public string name;
                public ulong skin;
                public string item_shortname;
            }

            public class CommandInfo 
            {
                public string command;
                public string message;
                public string public_message;
                public bool hook = false;
            }


            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Loading a new config.");
            config = new Configuration();
            config.vip_levels.Add(
                "vip", new Configuration.VIPInfo()
                {
                    name = "vip tier 1",
                    time_type = "day",
                    time_to_add = 30,
                    vip_group = "vip",
                    vip_description = "This is an example of a group only VIP token",
                    token_item = new Configuration.TokenInfo()
                    {
                        name = "VIP Token - 30 days",
                        skin = 2529344523,
                        item_shortname = "radiationresisttea.pure"
                    }
                });
            config.vip_levels.Add(
                "command_1", new Configuration.VIPInfo()
                {
                    name = "Command Token 1",
                    vip_description = "This is an example of a command only vip token",
                    token_item = new Configuration.TokenInfo()
                    {
                        name = "Day token",
                        skin = 2546992444,
                        item_shortname = "radiationresisttea.pure"
                    },
                    _command = new Configuration.CommandInfo()
                    {
                        command = "env.time 11",
                        message = "You made it day.",
                        public_message = "{name} has redeemed a token, turning night to day.",
                        hook = true
                        
                    }
                });
            config.vip_levels.Add(
               "command_2", new Configuration.VIPInfo()
               {
                   name = "Give Item",
                   time_type = "hour",
                   time_to_add = 30,
                   vip_group = "vip2",
                   vip_description = "This is an example of a group and command token.\nYou can also add new lines like this.",
                   token_item = new Configuration.TokenInfo()
                   {
                       name = "Item Giver",
                       skin = 2546992685,
                       item_shortname = "radiationresisttea.pure"
                   },
                   _command = new Configuration.CommandInfo()
                   {
                       command = "inventory.giveto {id} scrap 1000",
                       message = "You received some scrap.",
                       public_message = "{name} has redeemed a token and give themselves some scrap. ID: {id}",
                       hook = true

                   },
                   _commands = new List<Configuration.CommandInfo>()
                   {
                       new Configuration.CommandInfo()
                       {
                            command = "inventory.giveto {id} scrap 1000",
                            message = "You received some scrap.",
                            public_message = "{name} has redeemed a token and give themselves some scrap. ID: {id}",
                            hook = true
                       },
                       new Configuration.CommandInfo()
                       {
                            command = "inventory.giveto {id} wood 1000",
                            message = "You received some wood.",
                            public_message = "{name} has redeemed a token and give themselves some wood. ID: {id}",
                            hook = true
                       }
                   },
                   remove_commands = new List<Configuration.CommandInfo>()
                   {
                       new Configuration.CommandInfo()
                       {
                            command = "inventory.giveto {id} scrap 1000",
                            message = "You received some scrap when your vip ended.",
                            public_message = "{name} has redeemed a token and give themselves some scrap. ID: {id}",
                            hook = true
                       }
                   }
               });
        }
            
            
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

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    Puts("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                Puts($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion

        #region Data

        PlayerInfo pcdData;
        private DynamicConfigFile PCDDATA;

        void Init()
        {
            permission.RegisterPermission("viptoken.admin", this);
            PCDDATA = Interface.Oxide.DataFileSystem.GetFile("VIPToken");
            cmd.AddConsoleCommand("addtoken", this, nameof(AddToken));

            if (!config.handle_stacking)
            {
                Unsubscribe(nameof(CanStackItem));
                Unsubscribe(nameof(CanCombineDroppedItem));
            }
        }

        void Unload()
        {
            SaveData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "VIP_Tokens");
                CuiHelper.DestroyUi(player, "VIP_Tokeninfo");
            }
            TokenSkins.Clear();
        }

        void Loaded()
        {
            LoadConfig();
            LoadData();
            foreach (KeyValuePair<string, Configuration.VIPInfo> kvp in config.vip_levels)
            {
                if (!permission.GroupExists(kvp.Value.vip_group)) permission.CreateGroup(kvp.Value.vip_group, "", 1);
                VIPTiers.Add(kvp.Key);
            }
        }

        void SaveData()
        {
            PCDDATA.WriteObject(pcdData);
        }

        void LoadData()
        {
            try
            {
                pcdData = Interface.GetMod().DataFileSystem.ReadObject<PlayerInfo>("VIPToken");
            }
            catch
            {
                Puts("Couldn't load player data, creating new Playerfile");
                pcdData = new PlayerInfo();
            }
        }
        class PlayerInfo
        {
            public Dictionary<ulong, PCDInfo> pentiy = new Dictionary<ulong, PCDInfo>();
            public PlayerInfo() { }
        }

        class PCDInfo
        {
            public Dictionary<string, int> Redemptions = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            public Dictionary<string, VIPData> vip_levels = new Dictionary<string, VIPData>(StringComparer.InvariantCultureIgnoreCase);
        }

        class VIPData
        {
            public DateTime start_time;
            public DateTime end_date;
            public string token_key;
        }

        #endregion

        #region Hooks

        void OnNewSave(string filename)
        {
            foreach (KeyValuePair<ulong, PCDInfo> kvp in pcdData.pentiy)
            {
                foreach (KeyValuePair<string, Configuration.VIPInfo> ckvp in config.vip_levels)
                {
                    if (kvp.Value.Redemptions.ContainsKey(ckvp.Key) && ckvp.Value.remove_tokens_on_wipe)
                    {
                        kvp.Value.Redemptions[ckvp.Key] = 0;
                    }
                }
            }
        }

        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player != null && player.net?.connection?.authLevel >= 0)
            {
                CuiHelper.DestroyUi(player, "VIP_Tokens");
                CuiHelper.DestroyUi(player, "VIP_Tokeninfo");
            }
            return null;
        }

        object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item == null) return null;
            if (tokenskins.Contains(item.item.skin) || tokenskins.Contains(targetItem.item.skin))
            {
                return false;
            }
            return null;
        }

        object CanStackItem(Item item, Item targetItem)
        {
            if (item == null) return null;
            if (tokenskins.Contains(item.skin))
            {
                return false;
            }
            return null;
        }

        DateTime CalculateEndDate(DateTime date, double add, string type)
        {
            Puts($"date: {date} - add {add} - type: {type}");
            if (type.ToLower().Contains("second")) return date.AddSeconds(add);
            if (type.ToLower().Contains("minute")) return date.AddMinutes(add);
            if (type.ToLower().Contains("hour")) return date.AddHours(add);
            if (type.ToLower().Contains("day")) return date.AddDays(add);

            Puts($"Returning date: {date.ToString()}");
            return date;
        }

        object OnPlayerAddModifiers(BasePlayer player, Item item, ItemModConsumable consumable)
        {
            if (player == null || item == null || !TokenSkins.Contains(item.skin)) return null;
            foreach (KeyValuePair<string, Configuration.VIPInfo> kvp in config.vip_levels)
            {
                if (kvp.Value.token_item.skin == item.skin)
                {
                    if (kvp.Value.vip_group != null)
                    {                       
                        if (!pcdData.pentiy.ContainsKey(player.userID)) pcdData.pentiy.Add(player.userID, new PCDInfo());
                        var playerData = pcdData.pentiy[player.userID];
                        if (config.prevent_redemption && permission.UserHasGroup(player.UserIDString, kvp.Value.vip_group) && (!playerData.vip_levels.ContainsKey(kvp.Key) || playerData.vip_levels[kvp.Key].end_date < DateTime.Now))
                        {
                            PrintToChat(player, $"{prefix} You are already in this group. Wait until {kvp.Value.name.TitleCase()} expires before consuming this token. Type /{config.chat_commands.First()} to open the menu.");
                            LogToFile("VIPWallet", $"[{DateTime.Now}] {player.displayName} tried to consume token {kvp.Key} but they are already part of the group.", this, true);
                            CreateToken(player, kvp.Key.ToLower());
                            return false;
                        }
                        if (!permission.UserHasGroup(player.UserIDString, kvp.Value.vip_group)) permission.AddUserGroup(player.UserIDString, kvp.Value.vip_group);

                        if (!playerData.vip_levels.ContainsKey(kvp.Value.vip_group.ToLower())) playerData.vip_levels.Add(kvp.Value.vip_group.ToLower(), new VIPData());
                        var VIPData = playerData.vip_levels[kvp.Value.vip_group.ToLower()];
                        if (VIPData.start_time.Year < 2000) VIPData.start_time = DateTime.Now;
                        if (VIPData.end_date.Year < 2000)
                        {
                            VIPData.end_date = CalculateEndDate(DateTime.Now, kvp.Value.time_to_add, kvp.Value.time_type);
                            VIPData.token_key = kvp.Key;
                            SaveData();
                            PrintToChat(player, $"{prefix} You have redeemed {kvp.Value.time_to_add} {kvp.Value.time_type}s of {kvp.Value.vip_group.ToUpper()} for {kvp.Value.name}. New end date: {VIPData.end_date.ToString($"{config.date_format} HH-mm")}");
                            EffectNetwork.Send(new Effect(config.consume_effect, player.transform.position, player.transform.position), player.net.connection);
                            LogToFile("VIPTokens", $"[{DateTime.Now}] Added {player.displayName}[{player.UserIDString}] to VIPKey: {kvp.Key}. Expires: {VIPData.end_date.ToString($"{config.date_format} HH-mm")}.", this, true);
                            if (kvp.Value._command == null && kvp.Value._commands == null) return false;
                        }
                        else
                        {
                            var endDate = CalculateEndDate(VIPData.end_date, kvp.Value.time_to_add, kvp.Value.time_type);
                            if (endDate > DateTime.Now.AddDays(config.max_vip_days))
                            {
                                PrintToChat(player, $"{prefix} You cannot have more than {config.max_vip_days} days of {kvp.Value.name} using tokens.");
                                LogToFile("VIPWallet", $"[{DateTime.Now}] {player.displayName} tried to consume token {kvp.Key} but failed the max vip days check. Spawning them a new one.", this, true);
                                CreateToken(player, kvp.Key.ToLower());
                                return false;
                            }
                            else
                            {
                                var calculateFrom = DateTime.Now;
                                if (VIPData.end_date > calculateFrom) calculateFrom = VIPData.end_date;
                                VIPData.end_date = CalculateEndDate(calculateFrom, kvp.Value.time_to_add, kvp.Value.time_type);
                                VIPData.token_key = kvp.Key;
                                SaveData();
                                PrintToChat(player, $"{prefix} You have redeemed {kvp.Value.time_to_add} {kvp.Value.time_type}s of VIP for {kvp.Value.name}. New end date: {VIPData.end_date.ToString($"{config.date_format} HH-mm")}");
                                EffectNetwork.Send(new Effect(config.consume_effect, player.transform.position, player.transform.position), player.net.connection);
                                LogToFile("VIPTokens", $"[{DateTime.Now}] Extended {player.displayName}[{player.UserIDString}]'s VIPKey: {kvp.Key}. Expires: {VIPData.end_date.ToString($"{config.date_format} HH-mm")}.", this, true);
                                if (kvp.Value._command == null && kvp.Value._commands == null) return false;
                            }
                        }
                    }
                    if (kvp.Value._command != null)
                    {
                        if (kvp.Value._command.command != null)
                        {
                            rust.RunServerCommand(CommandBuilder(player.UserIDString, player.displayName, kvp.Value._command.command));
                            if (kvp.Value._command.message != null) PrintToChat(player, CommandBuilder(player.UserIDString, player.displayName, kvp.Value._command.message));
                            if (kvp.Value._command.public_message != null) PrintToChat(CommandBuilder(player.UserIDString, player.displayName, kvp.Value._command.public_message));
                            if (config.consume_effect != null) EffectNetwork.Send(new Effect(config.consume_effect, player.transform.position, player.transform.position), player.net.connection);
                        }
                        if (kvp.Value._command.hook) Interface.CallHook("OnTokenConsumed", player, kvp.Key);
                        if (kvp.Value._commands == null) return false;
                    }
                    if (kvp.Value._commands != null)
                    {
                        foreach (var command in kvp.Value._commands)
                        {
                            if (command.command != null)
                            {
                                rust.RunServerCommand(CommandBuilder(player.UserIDString, player.displayName, command.command));
                                if (command.message != null) PrintToChat(player, CommandBuilder(player.UserIDString, player.displayName, command.message));
                                if (command.public_message != null) PrintToChat(CommandBuilder(player.UserIDString, player.displayName, command.public_message));
                                if (config.consume_effect != null) EffectNetwork.Send(new Effect(config.consume_effect, player.transform.position, player.transform.position), player.net.connection);
                            }
                            if (command.hook) Interface.CallHook("OnTokenConsumed", player, kvp.Key);
                        }
                        return false;
                    }
                }
            }
            return null;
        }

        List<ulong> TokenSkins = new List<ulong>();
        void OnServerInitialized(bool initial)
        {
            timer.Every(config.check_rate, () =>
            {
                CheckSubscriptions();
            });
            foreach (KeyValuePair<string, Configuration.VIPInfo> kvp in config.vip_levels)
            {
                tokenskins.Add(kvp.Value.token_item.skin);
            }
            Puts($"Loaded {tokenskins.Count} skins.");
            foreach (var command in config.chat_commands)
            {
                cmd.AddChatCommand(command, this, "SendTokenMenu");
            }
            Puts($"Added {config.chat_commands.Length} commands.");

            foreach (var kvp in config.vip_levels)
                if (!TokenSkins.Contains(kvp.Value.token_item.skin)) TokenSkins.Add(kvp.Value.token_item.skin);
        }

        #endregion

        #region Check subscriptions
        
        [ChatCommand("forcecheck")]
        void ForceCheck(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, "viptoken.admin")) return;
            CheckSubscriptions();
        }

        void CheckSubscriptions()
        {
            Dictionary<ulong, List<string>> DeleteData = new Dictionary<ulong, List<string>>();
            if (pcdData.pentiy.Count == 0) return;
            foreach (KeyValuePair<ulong, PCDInfo> kvp in pcdData.pentiy)
            {                
                List<string> del_tiers = new List<string>();

                if (kvp.Value.vip_levels == null) continue;
                Puts($"Checking {kvp.Value.vip_levels.Count} entries for {kvp.Key}");
                foreach (KeyValuePair<string, VIPData> vkvp in kvp.Value.vip_levels)
                {
                    if (vkvp.Value.end_date > DateTime.Now) continue;
                    var perm = vkvp.Key;
                    Puts($"Attempting to remove {kvp.Key} from permission group: {perm}");
                    if (permission.UserHasGroup(kvp.Key.ToString(), perm)) permission.RemoveUserGroup(kvp.Key.ToString(), perm);
                    LogToFile("VIPTokens", $"[{DateTime.Now}] Removed {kvp.Key} from VIP: {vkvp.Key} as it has expired ({vkvp.Value.end_date.ToString()}).", this, true);

                    Configuration.VIPInfo vipInfo;
                    if (config.vip_levels.TryGetValue(vkvp.Value.token_key, out vipInfo) && vipInfo.remove_commands != null && vipInfo.remove_commands.Count > 0)
                    {
                        var useridString = kvp.Key.ToString();
                        var player = BasePlayer.Find(useridString);

                        foreach (var command in vipInfo.remove_commands)
                        {
                            if (command.command != null)
                            {
                                rust.RunServerCommand(CommandBuilder(useridString, player?.displayName ?? useridString, command.command));
                                if (command.message != null && player != null) PrintToChat(player, CommandBuilder(useridString, player?.displayName ?? useridString, command.message));
                                if (command.public_message != null) PrintToChat(CommandBuilder(useridString, player?.displayName ?? useridString, command.public_message));
                                if (config.consume_effect != null && player != null) EffectNetwork.Send(new Effect(config.consume_effect, player.transform.position, player.transform.position), player.net.connection);
                            }
                            if (command.hook) Interface.CallHook("OnVIPEndedCommandFired", player, vkvp.Value.token_key);
                        }
                    }

                    del_tiers.Add(vkvp.Key);
                }
                if (del_tiers.Count > 0)
                {
                    DeleteData.Add(kvp.Key, del_tiers);
                }
            }
            if (DeleteData.Count > 0)
            {
                foreach (KeyValuePair<ulong, List<string>> kvp in DeleteData)
                {
                    var playerData = pcdData.pentiy[kvp.Key];                    
                    foreach (var str in kvp.Value)
                    {
                        playerData.vip_levels.Remove(str);
                    }
                    if (playerData.vip_levels.Count == 0 && playerData.Redemptions.Count == 0) pcdData.pentiy.Remove(kvp.Key);
                }
                SaveData();
            }
        }

        #endregion

        #region Token

        private void AddRedemption(ulong id, string tier, int amount = 1)
        {
            if (!pcdData.pentiy.ContainsKey(id)) pcdData.pentiy.Add(id, new PCDInfo());
            var playerData = pcdData.pentiy[id];
            if (!playerData.Redemptions.ContainsKey(tier)) playerData.Redemptions.Add(tier, 0);
            playerData.Redemptions[tier] += amount;
            SaveData();
            
            var player = FindOnlinePlayerByID(id);
            if (player != null)
            {
                EffectNetwork.Send(new Effect(config.purchase_effect, player.transform.position, player.transform.position), player.net.connection);                
                PrintToChat(player, $"{prefix} You have successfully purchased {amount}x {config.vip_levels[tier].name} token. Type <color=#DFF008>/{config.chat_commands.First()}</color> to see more information.");
                LogToFile("VIPPurchases", $"[{DateTime.Now}] Player: {player.displayName}[{id}] purchased {amount}x Tier:{tier} token from the website.", this, true);
            }
            else LogToFile("VIPPurchases", $"[{DateTime.Now}] Player {id} purchased {amount}x Tier:{tier} token from the website.", this, true);
        }

        void GiveToken(BasePlayer player, Item item)
        {
            var dropped = false;
            if (!player.inventory.containerBelt.IsFull()) item.MoveToContainer(player.inventory.containerBelt);
            else if (!player.inventory.containerMain.IsFull()) item.MoveToContainer(player.inventory.containerMain);
            else
            {
                item.DropAndTossUpwards(player.transform.position, 3);
                dropped = true;
            }                
            LogToFile("VIPWallet", $"[{DateTime.Now}] created token for {player.displayName} - {item.name}. Dropped to floor: {dropped}", this, true);
        }

        void CreateToken(BasePlayer player, string token_type, int quantity = 1)
        {
            if (!config.vip_levels.ContainsKey(token_type))
            {
                Puts($"Token type {token_type} does not exist in the config.");
                LogToFile("VIPWallet", $"[{DateTime.Now}] {player.displayName} tried to redeem Token type: {token_type}, but it is not valid.", this, true);
                return;
            }
            var TokenData = config.vip_levels[token_type].token_item;
            var item = ItemManager.CreateByName(TokenData.item_shortname, quantity, TokenData.skin);
            item.name = TokenData.name;
            LogToFile("VIPWallet", $"[{DateTime.Now}] Redemption: confirmed check for {player.displayName} - tier {token_type}.", this, true);
            //test
            GiveToken(player, item);
        }

        #endregion

        #region Chat command

        [ChatCommand("givetoken")]
        void GiveToken(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "viptoken.admin")) return;
            if (config.vip_levels.Count == 0) return;
            var quantity = 1;
            if (args.Length == 0 || args.Length > 2 || !VIPTiers.Contains(args[0].ToLower()))
            {
                PrintToChat(player, $"{prefix} Usage: /givetoken <{string.Join(", ", VIPTiers)}>[Optional: <quantity>]");
                return;
            }
            if (args.Length == 2 && args[1].IsNumeric()) quantity = Convert.ToInt32(args[1]);
            CreateToken(player, args[0].ToLower(), quantity);
        }

        [ConsoleCommand("givetoken")]
        void GiveTolenConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, "viptoken.admin")) return;
            if (config.vip_levels.Count == 0) return;
            var quantity = 1;
            if (arg.Args.Length == 0 || arg.Args.Length > 3 || !VIPTiers.Contains(arg.Args[1].ToLower()))
            {
                arg.ReplyWith($"{prefix} Usage: /givetoken <target> <{string.Join(", ", VIPTiers)}>[Optional: <quantity>]");
                return;
            }
            var target = FindPlayer(arg.Args[0]);
            if (target == null)
            {
                arg.ReplyWith($"Could not find a player that matched: {arg.Args[0]}");
                return;
            }
            var tier = arg.Args[1].ToLower();
            if (arg.Args.Length > 2) int.TryParse(arg.Args[2], out quantity);
            if (quantity < 1) quantity = 1;

            CreateToken(target, tier, quantity);
        }

        BasePlayer FindPlayer(string nameOrID)
        {
            var target = BasePlayer.Find(nameOrID);
            if (target == null) target = BasePlayer.activePlayerList.FirstOrDefault(x => x.displayName.Contains(nameOrID));
            return target;
        }

        [ChatCommand("removevip")]
        void RemoveVIP(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "viptoken.admin")) return;
            if (args.Length != 2 || !VIPTiers.Contains(args[1].ToLower()))
            {
                PrintToChat(player, $"Usage: /removevip <player name/ID> <{string.Join(", ", VIPTiers)}>");
                return;
            }
            var tier = args[1].ToLower();
            var id = 0ul;
            if (args[0].IsSteamId())
            {
                id = Convert.ToUInt64(args[0]);
            }
            else
            {
                var target = FindPlayerByName(args[0], player);
                if (target == null) return;
                id = target.userID;
            }
            if (!pcdData.pentiy.ContainsKey(id))
            {
                PrintToChat(player, "Player not found in the token data base.");
                return;
            }
            var playerData = pcdData.pentiy[id];
            if (playerData.vip_levels.ContainsKey(tier))
            {
                playerData.vip_levels.Remove(tier);
                SaveData();
                PrintToChat(player, $"Removed {tier} from {id}");
                permission.RemoveUserGroup(id.ToString(), tier);
                return;
            }
            else PrintToChat(player, $"ID: {id} does not have vip tier: {tier}");
        }

        [ConsoleCommand("removevip")]
        void RemoveVIPConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, "viptoken.admin")) return;
            if (arg.Args.Length != 2 || !VIPTiers.Contains(arg.Args[1].ToLower()))
            {
                arg.ReplyWith($"Usage: /removevip <player name/ID> <{string.Join(", ", VIPTiers)}>");
                return;
            }
            var tier = arg.Args[1].ToLower();
            if (!ulong.TryParse(arg.Args[0], out var id))
            {
                var target = FindPlayer(arg.Args[0]);
                if (target == null)
                {
                    arg.ReplyWith($"Found no player matching: {arg.Args[0]}");
                    return;
                }
                id = target.userID.Get();
            }

            if (!pcdData.pentiy.ContainsKey(id))
            {
                arg.ReplyWith("Player not found in the token data base.");
                return;
            }
            var playerData = pcdData.pentiy[id];
            if (playerData.vip_levels.ContainsKey(tier))
            {
                playerData.vip_levels.Remove(tier);
                SaveData();
                arg.ReplyWith($"Removed {tier} from {id}");
                permission.RemoveUserGroup(id.ToString(), tier);
                return;
            }
            else arg.ReplyWith($"ID: {id} does not have vip tier: {tier}");
        }

        #endregion

        #region Console
        private void AddToken(ConsoleSystem.Arg arg)
        {
            if (arg == null)
            {
                Puts("arg was null");
                return;
            }
            if (arg.Args == null)
            {
                Puts("arg.Args was null");
                return;
            }
            if (arg.Args.Length < 3)
            {
                Puts($"arg.Args length was < 3 [length = {arg.Args.Length}]");
                return;
            }
            if (arg.Args[0] != config.verification_password)
            {
                Puts($"Password mismatch: Given: {arg.Args[0]}. Real PW: {config.verification_password}");
                return;
            }
            if (!arg.Args[1].IsSteamId())
            {
                Puts($"{arg.Args[1]} is not a valid steam ID.");
                return;
            }
            var id = Convert.ToUInt64(arg.Args[1]);
            var tier = arg.Args[2];
            if (!config.vip_levels.ContainsKey(tier))
            {
                Puts($"Token type {tier} does not exist in the config.");
                return;
            }
            var amount = 1;
            if (arg.Args.Length == 4)
            {
                amount = Math.Max(1, Convert.ToInt32(arg.Args[3]));
            }
            LogToFile("VIPTokens", $"[{DateTime.Now}] {id} purchased VIP Tier: {tier}.", this, true);
            AddRedemption(id, tier, amount);
        }

        private void AddToken(ulong id, string tier)
        {
            if (!id.IsSteamId())
            {
                Puts($"{id} is not a valid steam ID");
                return;
            }
            if (!config.vip_levels.ContainsKey(tier))
            {
                Puts($"{tier} is not a valid token tier.");
                return;
            }
            AddRedemption(id, tier);
        }

        #endregion

        #region Helper

        public List<string> VIPTiers = new List<string>();

        private BasePlayer FindPlayerByName(string Playername, BasePlayer SearchingPlayer = null)
        {
            var targetList = BasePlayer.allPlayerList.Where(x => x.displayName.ToLower().Contains(Playername.ToLower())).OrderBy(x => x.displayName.Length);
            if (targetList.Count() == 1) return targetList.First();
            if (targetList.Count() > 1)
            {
                if (targetList.First().displayName.ToLower() == Playername.ToLower()) return targetList.First();
                if (SearchingPlayer != null) PrintToChat(SearchingPlayer, $"More than one player found: {String.Join(",", targetList.Select(x => x.displayName))}");
                return null;
            }
            if (targetList.Count() == 0)
            {
                if (SearchingPlayer != null) PrintToChat(SearchingPlayer, $"No player was found that matched: {Playername}");
                return null;
            }
            return null;
        }

        BasePlayer FindOnlinePlayerByID(ulong id)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.userID == id) return player;
            }
            return null;
        }

        string CommandBuilder(string userID, string name, string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            return line.Replace("{id}", userID).Replace("{name}", name);
        }

        #endregion

        #region API

        object STOnRationTrigger(BasePlayer player, Item item)
        {
            if (!TokenSkins.Contains(item.skin)) return null;
            return true;
        }

        #endregion

        #region Menu

        const string back_panel_offset_min_1 = "-160 100";
        const string back_panel_offset_min_2 = "-160 30";
        const string back_panel_offset_min_3 = "-160 -40";
        const string back_panel_offset_min_4 = "-160 -110";
        const string back_panel_offset_min_5 = "-160 -180";

        class MenuCooldown
        {
            public Timer Cooldown;
        }

        Dictionary<ulong, MenuCooldown> menu_cooldown = new Dictionary<ulong, MenuCooldown>();

        
        void SendTokenMenu(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "VIP_Tokeninfo");
            if (config.vip_levels.Count == 0) return;
            var sendNext = false;
            List<string> vip_tiers = new List<string>();
            foreach (KeyValuePair<string, Configuration.VIPInfo> kvp in config.vip_levels)
            {
                if (vip_tiers.Count == 5)
                {
                    sendNext = true;
                    break;
                }
                vip_tiers.Add(kvp.Key);
            }
            VIP_Tokens(player, vip_tiers, sendNext);
        }

        string GetTokenBalance(BasePlayer player, string tier)
        {
            if (!pcdData.pentiy.ContainsKey(player.userID)) return "0";
            var playerData = pcdData.pentiy[player.userID];
            if (!playerData.Redemptions.ContainsKey(tier)) return "0";
            return playerData.Redemptions[tier].ToString();
        }

        string GetEndDate(BasePlayer player, string tier)
        {
            if (!pcdData.pentiy.ContainsKey(player.userID)) return "NA";
            var playerData = pcdData.pentiy[player.userID];
            if (!playerData.vip_levels.ContainsKey(tier)) return "NA";
            if (playerData.vip_levels[tier].end_date.Date == DateTime.Now.Date)
            {
                var enddate = playerData.vip_levels[tier].end_date;
                return $"{enddate.Hour}:{enddate.Minute}:{enddate.Second}";
            }
                
            return playerData.vip_levels[tier].end_date.ToString($"{config.date_format}");
        }
        bool WithDrawToken(BasePlayer player, string tier)
        {
            if (!pcdData.pentiy.ContainsKey(player.userID))
            {
                PrintToChat(player, "You do not have any tokens.");
                return false;
            }
            if (menu_cooldown.ContainsKey(player.userID) && menu_cooldown[player.userID].Cooldown != null)
            {
                PrintToChat(player, "Please wait a few seconds before withdrawing more tokens.");
                return false;
            }
            var playerData = pcdData.pentiy[player.userID];
            if (!playerData.Redemptions.ContainsKey(tier) || playerData.Redemptions[tier] == 0)
            {
                PrintToChat(player, $"You do not have any {config.vip_levels[tier].name} tokens.");
                return false;
            }
            if (player.inventory.containerBelt.IsFull() && player.inventory.containerMain.IsFull())
            {
                PrintToChat(player, $"{prefix} You need 1 slot available in your inventory in order to redeem a token.");
                return false;
            }
            if (!menu_cooldown.ContainsKey(player.userID)) menu_cooldown.Add(player.userID, new MenuCooldown());
            var MenuData = menu_cooldown[player.userID];
            MenuData.Cooldown = timer.Once(2f, () =>
            {
                if (!MenuData.Cooldown.Destroyed) MenuData.Cooldown.Destroy();
                menu_cooldown.Remove(player.userID);
            });
            playerData.Redemptions[tier]--;
            SaveData();
            CreateToken(player, tier);
            return true;
        }

        List<Item> AllItems(BasePlayer player)
        {
            List<Item> result = Pool.Get<List<Item>>();

            if (player.inventory.containerMain?.itemList != null)
                result.AddRange(player.inventory.containerMain.itemList);

            if (player.inventory.containerBelt?.itemList != null)
                result.AddRange(player.inventory.containerBelt.itemList);

            if (player.inventory.containerWear?.itemList != null)
                result.AddRange(player.inventory.containerWear.itemList);

            return result;
        }

        bool DepositToken(BasePlayer player, string tier)
        {
            if (menu_cooldown.ContainsKey(player.userID) && menu_cooldown[player.userID].Cooldown != null)
            {
                PrintToChat(player, "Please wait a few seconds before depositing more tokens.");
                return false;
            }
            var items = AllItems(player);
            try
            {
                if (items.Count == 0)
                {
                    PrintToChat(player, "You do not have any tokens in your inventory.");
                    return false;
                }
            }
            finally { Pool.FreeUnmanaged(ref items); }

            var skin = config.vip_levels[tier].token_item.skin;

            foreach (var item in items)
            {
                if (skin == item.skin)
                {
                    if (!menu_cooldown.ContainsKey(player.userID)) menu_cooldown.Add(player.userID, new MenuCooldown());
                    var MenuData = menu_cooldown[player.userID];
                    MenuData.Cooldown = timer.Once(2f, () =>
                    {
                        if (!MenuData.Cooldown.Destroyed) MenuData.Cooldown.Destroy();
                        menu_cooldown.Remove(player.userID);
                    });
                    if (!pcdData.pentiy.ContainsKey(player.userID)) pcdData.pentiy.Add(player.userID, new PCDInfo());
                    var playerData = pcdData.pentiy[player.userID];
                    if (!playerData.Redemptions.ContainsKey(tier)) playerData.Redemptions.Add(tier, item.amount);
                    else playerData.Redemptions[tier] += item.amount;
                    SaveData();
                    PrintToChat(player, $"Stored {item.amount}x token{(item.amount > 1 ? "s" : "")} into your wallet.");
                    item.Remove();
                    return true;
                }
            }
            PrintToChat(player, "Token not found.");
            return false;
        }

        [ConsoleCommand("withdrawviptoken")]
        private void withdrawviptoken(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (arg.Args.Length == 0) return;
            var tier = arg.Args[0];

            if (WithDrawToken(player, tier)) SendTokenMenu(player);
        }

        [ConsoleCommand("depositviptoken")]
        private void depositviptoken(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (arg.Args.Length == 0) return;
            var tier = arg.Args[0];

            if (DepositToken(player, tier)) SendTokenMenu(player);
        }

        [ConsoleCommand("vipclosemenu")]
        private void vipclosemenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, "VIP_Tokens");
        }

        [ConsoleCommand("vipnextmenu")]
        private void vipnextmenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (arg.Args.Length != 1) return;
            var lastTier = arg.Args[0];
            CuiHelper.DestroyUi(player, "VIP_Tokeninfo");
            var sendNext = false;
            List<string> vip_tiers = new List<string>();
            var startAdding = false;
            foreach (KeyValuePair<string, Configuration.VIPInfo> kvp in config.vip_levels)
            {
                if (vip_tiers.Count == 5)
                {
                    sendNext = true;
                    break;
                }
                if (!startAdding)
                {
                    if (!lastTier.Equals(kvp.Key)) continue;
                    startAdding = true;
                    continue;
                }
                vip_tiers.Add(kvp.Key);
            }
            if (vip_tiers.Count == 0) return;
            VIP_Tokens(player, vip_tiers, sendNext, true);
        }

        [ConsoleCommand("vipbackmenu")]
        private void vipbackmenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (arg.Args.Length != 1) return;
            CuiHelper.DestroyUi(player, "VIP_Tokeninfo");
            var firstTier = arg.Args[0];
            var sendNext = false;
            var sendBack = false;
            List<string> vip_tiers = new List<string>();
            var firstKey = "";
            foreach (KeyValuePair<string, Configuration.VIPInfo> kvp in config.vip_levels)
            {
                if (string.IsNullOrEmpty(firstKey)) firstKey = kvp.Key;
                if (vip_tiers.Count == 5)
                {
                    sendNext = true;                    
                    break;
                }
                if (kvp.Key.Equals(firstTier)) break;
                vip_tiers.Add(kvp.Key);
            }
            if (vip_tiers.Count == 0) return;
            if (!vip_tiers.Contains(firstKey)) sendBack = true;
            VIP_Tokens(player, vip_tiers, sendNext, sendBack);
        }

        void VIP_Tokens(BasePlayer player, List<string> vip_tiers, bool sendnext = false, bool sendback = false)
        {
            var BackPanelOffset = "";
            if (vip_tiers.Count == 1) BackPanelOffset = back_panel_offset_min_1;
            if (vip_tiers.Count == 2) BackPanelOffset = back_panel_offset_min_2;
            if (vip_tiers.Count == 3) BackPanelOffset = back_panel_offset_min_3;
            if (vip_tiers.Count == 4) BackPanelOffset = back_panel_offset_min_4;
            if (vip_tiers.Count == 5) BackPanelOffset = back_panel_offset_min_5;

            if (string.IsNullOrEmpty(BackPanelOffset)) return;

            var configInstance = config.vip_levels;

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0.8823529" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-0.351 -0.328", OffsetMax = "0.349 0.332" }
            }, "Overlay", "VIP_Tokens");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.245283 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = BackPanelOffset, OffsetMax = "160 180" }
            }, "VIP_Tokens", "vip_back_panel");            

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.3962264 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 110", OffsetMax = "150 170" }
            }, "VIP_Tokens", "vip_info_panel_1");

            container.Add(new CuiElement
            {
                Name = "vip_info_header_1",
                Parent = "vip_info_panel_1",
                Components = {
                    new CuiTextComponent { Text = configInstance[vip_tiers[0]].name.ToUpper(), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 0", OffsetMax = "150 30" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "vip_balance_1",
                Parent = "vip_info_panel_1",
                Components = {
                    new CuiTextComponent { Text = $"Token Balance: {GetTokenBalance(player, vip_tiers[0])}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-140 -30", OffsetMax = "0 0" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "vip_end_date_1",
                Parent = "vip_info_panel_1",
                Components = {
                    new CuiTextComponent { Text = $"Ends: {GetEndDate(player, vip_tiers[0])}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -30", OffsetMax = "150 0" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2470588 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "180 -30", OffsetMax = "262 30" }
            }, "vip_info_panel_1", "vip_button_panel_1_1");

            container.Add(new CuiButton
            {
                Button = { Color = "0.3960784 0 0 1", Command = $"withdrawviptoken {vip_tiers[0]}" },
                Text = { Text = "Withdraw", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-36 5", OffsetMax = "36 25" }
            }, "vip_button_panel_1_1", "vip_withdraw_button_1_1");

            container.Add(new CuiButton
            {
                Button = { Color = "0.3960784 0 0 1", Command = $"depositviptoken {vip_tiers[0]}" },
                Text = { Text = "Deposit", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-36 -25", OffsetMax = "36 -5" }
            }, "vip_button_panel_1_1", "vip_withdraw_button_2_1");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2470588 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-196 -14", OffsetMax = "-168 14" }
            }, "vip_info_panel_1", "vip_info_button_panel_1");

            container.Add(new CuiButton
            {
                Button = { Color = "0.3960784 0 0 1", Command = $"vipinfo {vip_tiers[0]}" },
                Text = { Text = "i", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-12 -12", OffsetMax = "12 12" }
            }, "vip_info_button_panel_1", "vip_info_button_button_1");

            if (vip_tiers.Count > 1)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.4 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 40", OffsetMax = "150 100" }
                }, "VIP_Tokens", "vip_info_panel_2");

                container.Add(new CuiElement
                {
                    Name = "vip_info_header_2",
                    Parent = "vip_info_panel_2",
                    Components = {
                    new CuiTextComponent { Text = configInstance[vip_tiers[1]].name.ToUpper(), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 0", OffsetMax = "150 30" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "vip_balance_2",
                    Parent = "vip_info_panel_2",
                    Components = {
                    new CuiTextComponent { Text = $"Token Balance: {GetTokenBalance(player, vip_tiers[1])}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-140 -30", OffsetMax = "0 0" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "vip_end_date_2",
                    Parent = "vip_info_panel_2",
                    Components = {
                    new CuiTextComponent { Text = $"Ends: {GetEndDate(player, vip_tiers[1])}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -30", OffsetMax = "150 0" }
                }
                });

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.2470588 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "180 -30", OffsetMax = "262 30" }
                }, "vip_info_panel_2", "vip_button_panel_1_2");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.3960784 0 0 1", Command = $"withdrawviptoken {vip_tiers[1]}" },
                    Text = { Text = "Withdraw", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-36 5", OffsetMax = "36 25" }
                }, "vip_button_panel_1_2", "vip_withdraw_button_1_2");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.3960784 0 0 1", Command = $"depositviptoken {vip_tiers[1]}" },
                    Text = { Text = "Deposit", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-36 -25", OffsetMax = "36 -5" }
                }, "vip_button_panel_1_2", "vip_withdraw_button_2_2");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.2470588 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-196 -14", OffsetMax = "-168 14" }
                }, "vip_info_panel_2", "vip_info_button_panel_2");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.3960784 0 0 1", Command = $"vipinfo {vip_tiers[1]}" },
                    Text = { Text = "i", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-12 -12", OffsetMax = "12 12" }
                }, "vip_info_button_panel_2", "vip_info_button_button_2");
            }
            if (vip_tiers.Count > 2)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.4 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -30", OffsetMax = "150 30" }
                }, "VIP_Tokens", "vip_info_panel_3");

                container.Add(new CuiElement
                {
                    Name = "vip_info_header_3",
                    Parent = "vip_info_panel_3",
                    Components = {
                    new CuiTextComponent { Text = configInstance[vip_tiers[2]].name.ToUpper(), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 0", OffsetMax = "150 30" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "vip_balance_3",
                    Parent = "vip_info_panel_3",
                    Components = {
                    new CuiTextComponent { Text = $"Token Balance: {GetTokenBalance(player, vip_tiers[2])}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-140 -30", OffsetMax = "0 0" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "vip_end_date_3",
                    Parent = "vip_info_panel_3",
                    Components = {
                    new CuiTextComponent { Text = $"Ends: {GetEndDate(player, vip_tiers[2])}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -30", OffsetMax = "150 0" }
                }
                });

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.2470588 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "180 -30", OffsetMax = "262 30" }
                }, "vip_info_panel_3", "vip_button_panel_1_3");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.3960784 0 0 1", Command = $"withdrawviptoken {vip_tiers[2]}" },
                    Text = { Text = "Withdraw", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-36 5", OffsetMax = "36 25" }
                }, "vip_button_panel_1_3", "vip_withdraw_button_1_3");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.3960784 0 0 1", Command = $"depositviptoken {vip_tiers[2]}" },
                    Text = { Text = "Deposit", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-36 -25", OffsetMax = "36 -5" }
                }, "vip_button_panel_1_3", "vip_withdraw_button_2_3");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.2470588 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-196 -14", OffsetMax = "-168 14" }
                }, "vip_info_panel_3", "vip_info_button_panel_3");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.3960784 0 0 1", Command = $"vipinfo {vip_tiers[2]}" },
                    Text = { Text = "i", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-12 -12", OffsetMax = "12 12" }
                }, "vip_info_button_panel_3", "vip_info_button_button_3");
            }

            if (vip_tiers.Count > 3)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.4 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -100", OffsetMax = "150 -40" }
                }, "VIP_Tokens", "vip_info_panel_4");

                container.Add(new CuiElement
                {
                    Name = "vip_info_header_4",
                    Parent = "vip_info_panel_4",
                    Components = {
                    new CuiTextComponent { Text = configInstance[vip_tiers[3]].name.ToUpper(), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 0", OffsetMax = "150 30" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "vip_balance_4",
                    Parent = "vip_info_panel_4",
                    Components = {
                    new CuiTextComponent { Text = $"Token Balance: {GetTokenBalance(player, vip_tiers[3])}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-140 -30", OffsetMax = "0 0" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "vip_end_date_4",
                    Parent = "vip_info_panel_4",
                    Components = {
                    new CuiTextComponent { Text = $"Ends: {GetEndDate(player, vip_tiers[3])}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -30", OffsetMax = "150 0" }
                }
                });

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.2470588 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "180 -30", OffsetMax = "262 30" }
                }, "vip_info_panel_4", "vip_button_panel_1_4");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.3960784 0 0 1", Command = $"withdrawviptoken {vip_tiers[3]}" },
                    Text = { Text = "Withdraw", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-36 5", OffsetMax = "36 25" }
                }, "vip_button_panel_1_4", "vip_withdraw_button_1_4");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.3960784 0 0 1", Command = $"depositviptoken {vip_tiers[3]}" },
                    Text = { Text = "Deposit", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-36 -25", OffsetMax = "36 -5" }
                }, "vip_button_panel_1_4", "vip_withdraw_button_2_4");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.2470588 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-196 -14", OffsetMax = "-168 14" }
                }, "vip_info_panel_4", "vip_info_button_panel_4");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.3960784 0 0 1", Command = $"vipinfo {vip_tiers[3]}" },
                    Text = { Text = "i", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-12 -12", OffsetMax = "12 12" }
                }, "vip_info_button_panel_4", "vip_info_button_button_4");
            }

            if (vip_tiers.Count > 4)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.4 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -170", OffsetMax = "150 -110" }
                }, "VIP_Tokens", "vip_info_panel_5");

                container.Add(new CuiElement
                {
                    Name = "vip_info_header_5",
                    Parent = "vip_info_panel_5",
                    Components = {
                    new CuiTextComponent { Text = configInstance[vip_tiers[4]].name.ToUpper(), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 0", OffsetMax = "150 30" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "vip_balance_5",
                    Parent = "vip_info_panel_5",
                    Components = {
                    new CuiTextComponent { Text = $"Token Balance: {GetTokenBalance(player, vip_tiers[4])}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-140 -30", OffsetMax = "0 0" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "vip_end_date_5",
                    Parent = "vip_info_panel_5",
                    Components = {
                    new CuiTextComponent { Text = $"Ends: {GetEndDate(player, vip_tiers[4])}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -30", OffsetMax = "150 0" }
                }
                });

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.2470588 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "180 -30", OffsetMax = "262 30" }
                }, "vip_info_panel_5", "vip_button_panel_1_5");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.3960784 0 0 1", Command = $"withdrawviptoken {vip_tiers[4]}" },
                    Text = { Text = "Withdraw", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-36 5", OffsetMax = "36 25" }
                }, "vip_button_panel_1_5", "vip_withdraw_button_1_5");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.3960784 0 0 1", Command = $"depositviptoken {vip_tiers[4]}" },
                    Text = { Text = "Deposit", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-36 -25", OffsetMax = "36 -5" }
                }, "vip_button_panel_1_5", "vip_withdraw_button_2_5");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.2470588 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-196 -14", OffsetMax = "-168 14" }
                }, "vip_info_panel_5", "vip_info_button_panel_5");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.3960784 0 0 1", Command = $"vipinfo {vip_tiers[4]}" },
                    Text = { Text = "i", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-12 -12", OffsetMax = "12 12" }
                }, "vip_info_button_panel_5", "vip_info_button_button_5");
            }                

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2470588 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-35 -221.6", OffsetMax = "35 -193.6" }
            }, "VIP_Tokens", "vip_close_panel");

            container.Add(new CuiButton
            {
                Button = { Color = "0.4 0 0 1", Command = "vipclosemenu" },
                Text = { Text = "CLOSE", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -11", OffsetMax = "32 11" }
            }, "vip_close_panel", "vip_close_button");

            if (sendback)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.2470588 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-153 -221.6", OffsetMax = "-83 -193.6" }
                }, "VIP_Tokens", "vip_back_panel");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.4 0 0 1", Command = $"vipbackmenu {vip_tiers.First()}" },
                    Text = { Text = "BACK", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -11", OffsetMax = "32 11" }
                }, "vip_back_panel", "vip_back_button");
            }            

            if (sendnext)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.2470588 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "83 -221.6", OffsetMax = "153 -193.6" }
                }, "VIP_Tokens", "vip_next_panel");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.4 0 0 1", Command = $"vipnextmenu {vip_tiers.Last()}" },
                    Text = { Text = "NEXT", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -11", OffsetMax = "32 11" }
                }, "vip_next_panel", "vip_next_button");
            }
            

            CuiHelper.DestroyUi(player, "VIP_Tokens");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("vipinfo")]
        void vipinfo(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, "VIP_Tokens");
            if (arg.Args.Length != 1) return;
            VIP_TokenInfo(player, arg.Args[0]);
        }

        [ConsoleCommand("vipinfotokenclose")]
        void vipinfotokenclose(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, "VIP_Tokeninfo");
        }

        [ConsoleCommand("vipinfotokenback")]
        void vipinfotokenback(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, "VIP_Tokeninfo");
            SendTokenMenu(player);
        }

        void VIP_TokenInfo(BasePlayer player, string tier)
        {
            if (!config.vip_levels.ContainsKey(tier)) return;
            var tokenData = config.vip_levels[tier];
            
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0.8823529" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-0.351 -0.328", OffsetMax = "0.349 0.332" }
            }, "Overlay", "VIP_Tokeninfo");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.245283 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-160 -179.36", OffsetMax = "160 180.64" }
            }, "VIP_Tokeninfo", "VIP_Tokeninfo_panel");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.3960784 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -170", OffsetMax = "150 170" }
            }, "VIP_Tokeninfo", "VIP_Tokeninfo_front_panel");

            container.Add(new CuiElement
            {
                Name = "VIP_Tokeninfo_title",
                Parent = "VIP_Tokeninfo_front_panel",
                Components = {
                    new CuiTextComponent { Text = tokenData.name.ToUpper(), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-131.092 116.831", OffsetMax = "131.092 162.769" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "VIP_Tokeninfo_days_title",
                Parent = "VIP_Tokeninfo_front_panel",
                Components = {
                    new CuiTextComponent { Text = $"Token {tokenData.time_type}s:", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-131.092 72.191", OffsetMax = "-0.002 95.8" }
                }
            });
            var days = "NA";
            if (tokenData.time_to_add != 0) days = tokenData.time_to_add.ToString();

            container.Add(new CuiElement
            {
                Name = "VIP_Tokeninfo_days_value",
                Parent = "VIP_Tokeninfo_days_title",
                Components = {
                    new CuiTextComponent { Text = days, Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperCenter, Color = "0.4609563 1 0 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "65.545 -11.804", OffsetMax = "196.635 11.804" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "VIP_Tokeninfo_description_title",
                Parent = "VIP_Tokeninfo_front_panel",
                Components = {
                    new CuiTextComponent { Text = "Description:", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-131.09 31.68", OffsetMax = "0 59.402" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "VIP_Tokeninfo_description_value",
                Parent = "VIP_Tokeninfo_description_title",
                Components = {
                    new CuiTextComponent { Text = tokenData.vip_description, Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-65.544 -179.28", OffsetMax = "196.636 -13.86" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2470588 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "80 -221.6", OffsetMax = "150 -193.6" }
            }, "VIP_Tokeninfo", "VIP_Tokeninfo_close_panel");

            container.Add(new CuiButton
            {
                Button = { Color = "0.4 0 0 1", Command = "vipinfotokenclose" },
                Text = { Text = "CLOSE", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -11", OffsetMax = "32 11" }
            }, "VIP_Tokeninfo_close_panel", "VIP_Tokeninfo_close_button");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2470588 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-153 -221.6", OffsetMax = "-83 -193.6" }
            }, "VIP_Tokeninfo", "VIP_Tokeninfo_back_panel");

            container.Add(new CuiButton
            {
                Button = { Color = "0.4 0 0 1", Command = "vipinfotokenback" },
                Text = { Text = "BACK", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -11", OffsetMax = "32 11" }
            }, "VIP_Tokeninfo_back_panel", "VIP_Tokeninfo_back_button");

            CuiHelper.DestroyUi(player, "VIP_Tokeninfo");
            CuiHelper.AddUi(player, container);
        }

        #endregion       

    }
}
