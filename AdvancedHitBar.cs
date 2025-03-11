using System;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using UnityEngine;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("AdvancedHitBar", "ProCelle", "1.2.0" )]
    [Description("Advanced enemy health bar")]

    class AdvancedHitBar: RustPlugin
    {
        [PluginReference]
        private Plugin ImageLibrary;
        string ImageBarFillingStart;
        string ImageBarFillingEnd;
        string message;


        string HitBarImage;
        string HitBarImageFillingBlue;
        string HitBarImageFillingBlueEnd;
        string HitBarImageFillingRed;
        string HitBarImageFillingRedEnd;
        string HitBarImageFillingPink;
        string HitBarImageFillingPinkEnd;
        string HitBarImageFillingLime;
        string HitBarImageFillingLimeEnd;
        string HitBarImageFillingPurple;
        string HitBarImageFillingPurpleEnd;
        string HitBarImageFillingGreen;
        string HitBarImageFillingGreenEnd;
        string HitBarImageFillingWhite;
        string HitBarImageFillingWhiteEnd;


        string BlueExampleImage;
        string LimeExampleImage;
        string GreenExampleImage;
        string RedExampleImage;
        string PinkExampleImage;
        string PurpleExampleImage;
        string WhiteeExampleImage;
        string SettingsBackground;

        string Bar = "ExternarBar";
        string BarFilling = "InternalBar";
        string BarFillingEnd = "InternalBarEnd";
        string MainConfigPannel = "MainConfigPannel";
        string ConfigTextBox = "ConfigTextBox";



        private ConfigData configData;


        bool usesPerms;
        string anchorMin;
        string anchorMax;
        float timers;
        string textBox;
        string textColor;
        bool enableCopter;
        bool enableBradley;
        bool enablePlayers;
        bool enableNPC;
        string acceptButtonText;
        string closeButtonText;
        string chatCommand;
        bool enableMini;
        bool enableScrapCopter;
        bool enableDeployables;
        bool enableBuildingBlocks;
        bool enableAllEntities;
        int customHealth;

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {

                ["Hitbar Help"] = "Available Commands:\n- Change the HitBar color: <color=#ffd479>/hitbar</color>\n- <color=#00a000>Enable</color> the HitBar: <color=#ffd479>/hitbar on</color>\n- <color=#ff0000>Disable</color> the HitBar: <color=#ffd479>/hitbar off</color>",
                ["Hitbar Turned OFF"] = "You have <color=#ff0000>Disabled</color> the HitBar.",
                ["Hitbar Turned ON"] = "You have <color=#00a000>Enabled</color> the HitBar.",
                ["Hitbar Color Change"] = "You have modified the HitBar color!",
                ["No permission"] = "You dont have permission to run that command.",
                ["Sintax Error"] = "Sintax Error! To see the avaible commands please type <color=#ffd479>/hitbar help</color>"
            }, this);
        }
        #endregion
        

        private void OnServerInitialized() 
        {
            if (ImageLibrary == null) { Puts("Image Library is required"); covalence.Server.Command("o.unload AdvancedAlerts"); }
            else{

            
                if (!ImageLibrary.Call<bool>("HasImage", HitBarImage))
                    ImageLibrary.Call("AddImage", HitBarImage, HitBarImage, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", HitBarImageFillingBlue))
                    ImageLibrary.Call("AddImage", HitBarImageFillingBlue, HitBarImageFillingBlue, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", HitBarImageFillingBlueEnd))
                    ImageLibrary.Call("AddImage", HitBarImageFillingBlueEnd, HitBarImageFillingBlueEnd, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", HitBarImageFillingRed))
                    ImageLibrary.Call("AddImage", HitBarImageFillingRed, HitBarImageFillingRed, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", HitBarImageFillingRedEnd))
                    ImageLibrary.Call("AddImage", HitBarImageFillingRedEnd, HitBarImageFillingRedEnd, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", HitBarImageFillingPink))
                    ImageLibrary.Call("AddImage", HitBarImageFillingPink, HitBarImageFillingPink, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", HitBarImageFillingPinkEnd))
                    ImageLibrary.Call("AddImage", HitBarImageFillingPinkEnd, HitBarImageFillingPinkEnd, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", HitBarImageFillingLime))
                    ImageLibrary.Call("AddImage", HitBarImageFillingLime, HitBarImageFillingLime, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", HitBarImageFillingLimeEnd))
                    ImageLibrary.Call("AddImage", HitBarImageFillingLimeEnd, HitBarImageFillingLimeEnd, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", HitBarImageFillingGreen))
                    ImageLibrary.Call("AddImage", HitBarImageFillingGreen, HitBarImageFillingGreen, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", HitBarImageFillingGreenEnd))
                    ImageLibrary.Call("AddImage", HitBarImageFillingGreenEnd, HitBarImageFillingGreenEnd, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", HitBarImageFillingPurple))
                    ImageLibrary.Call("AddImage", HitBarImageFillingPurple, HitBarImageFillingPurple, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", HitBarImageFillingPurpleEnd))
                    ImageLibrary.Call("AddImage", HitBarImageFillingPurpleEnd, HitBarImageFillingPurpleEnd, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", HitBarImageFillingWhite))
                    ImageLibrary.Call("AddImage", HitBarImageFillingWhite, HitBarImageFillingWhite, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", HitBarImageFillingWhiteEnd))
                    ImageLibrary.Call("AddImage", HitBarImageFillingWhiteEnd, HitBarImageFillingWhiteEnd, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", BlueExampleImage))
                    ImageLibrary.Call("AddImage", BlueExampleImage, BlueExampleImage, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", LimeExampleImage))
                    ImageLibrary.Call("AddImage", LimeExampleImage, LimeExampleImage, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", GreenExampleImage))
                    ImageLibrary.Call("AddImage", GreenExampleImage, GreenExampleImage, 0UL);

                if (!ImageLibrary.Call<bool>( "HasImage", RedExampleImage))
                    ImageLibrary.Call("AddImage", RedExampleImage, RedExampleImage, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", PinkExampleImage))
                    ImageLibrary.Call("AddImage", PinkExampleImage, PinkExampleImage, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", PurpleExampleImage))
                    ImageLibrary.Call("AddImage", PurpleExampleImage, PurpleExampleImage, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", WhiteeExampleImage))
                    ImageLibrary.Call("AddImage", WhiteeExampleImage, WhiteeExampleImage, 0UL);

                if (!ImageLibrary.Call<bool>("HasImage", SettingsBackground))
                    ImageLibrary.Call("AddImage", SettingsBackground, SettingsBackground, 0UL);

            }


            permission.RegisterPermission(USE, this);
            permission.RegisterPermission(EDIT, this);
            cmd.AddChatCommand(chatCommand, this, "hitbar");

            foreach (var c in configData.PluginSettings.HlthList)
            {
                if (!permission.PermissionExists(c.Key, this))
                    permission.RegisterPermission(c.Key, this);
            }

        }

        private const string USE = "advancedhitbar.use";
        private const string EDIT = "advancedhitbar.edit";


        void Init()
        {
            if (!LoadConfigVariables()) {
            Puts("Config file issue detected. Please delete file, or check syntax and fix.");
            return;
            }


            HitBarImage = configData.ImageSettings.HitBarImag;
            HitBarImageFillingBlue = configData.ImageSettings.HitBarImageFillingBlu;
            HitBarImageFillingBlueEnd = configData.ImageSettings.HitBarImageFillingBlueEn;
            HitBarImageFillingRed = configData.ImageSettings.HitBarImageFillingRe;
            HitBarImageFillingRedEnd = configData.ImageSettings.HitBarImageFillingRedEn;
            HitBarImageFillingPink = configData.ImageSettings.HitBarImageFillingPin;
            HitBarImageFillingPinkEnd = configData.ImageSettings.HitBarImageFillingPinkEn;
            HitBarImageFillingLime = configData.ImageSettings.HitBarImageFillingLim;
            HitBarImageFillingLimeEnd = configData.ImageSettings.HitBarImageFillingLimeEn;
            HitBarImageFillingPurple = configData.ImageSettings.HitBarImageFillingPurpl;
            HitBarImageFillingPurpleEnd = configData.ImageSettings.HitBarImageFillingPurpleEn;
            HitBarImageFillingGreen = configData.ImageSettings.HitBarImageFillingGree;
            HitBarImageFillingGreenEnd = configData.ImageSettings.HitBarImageFillingGreenEn;
            HitBarImageFillingWhite = configData.ImageSettings.HitBarImageFillingWhit;
            HitBarImageFillingWhiteEnd = configData.ImageSettings.HitBarImageFillingWhiteEn;


            BlueExampleImage = configData.ImageSettings.BlueExampleImag;
            LimeExampleImage = configData.ImageSettings.LimeExampleImag;
            GreenExampleImage = configData.ImageSettings.GreenExampleImag;
            RedExampleImage = configData.ImageSettings.RedExampleImag;
            PinkExampleImage = configData.ImageSettings.PinkExampleImag;
            PurpleExampleImage = configData.ImageSettings.PurpleExampleImag;
            WhiteeExampleImage = configData.ImageSettings.WhiteeExampleImag;
            SettingsBackground = configData.ImageSettings.SettingsBackgroun;


            usesPerms = configData.PluginSettings.UsesPerms;
            anchorMin = configData.PluginSettings.AnchorMin;
            anchorMax = configData.PluginSettings.AnchorMax;
            timers = configData.PluginSettings.Timer;
            textBox = configData.PluginSettings.TextBox;
            textColor = configData.PluginSettings.TextColor;
            enableCopter = configData.PluginSettings.EnableCopter;
            enableBradley = configData.PluginSettings.EnableBradley;
            enablePlayers = configData.PluginSettings.EnablePlayers;
            enableNPC = configData.PluginSettings.EnableNPC;
            acceptButtonText = configData.PluginSettings.AcceptButtonText;
            closeButtonText = configData.PluginSettings.CloseButtonText;
            chatCommand = configData.PluginSettings.ChatCommand;
            enableMini = configData.PluginSettings.EnableMini;
            enableScrapCopter = configData.PluginSettings.EnableScrapCopter;
            enableDeployables = configData.PluginSettings.EnableDeployables; 
            enableBuildingBlocks = configData.PluginSettings.EnableBuildingBlocks;
            enableAllEntities = configData.PluginSettings.EnableAllEntities;
            customHealth = configData.PluginSettings.CustomHealth;
            Puts("Plugin loaded");
        }
        private void Unload() 
        {
            foreach(BasePlayer current in BasePlayer.activePlayerList) {
                CuiHelper.DestroyUi(current, Bar);
                CuiHelper.DestroyUi(current, BarFilling);
                CuiHelper.DestroyUi(current, BarFillingEnd);
                CuiHelper.DestroyUi(current, MainConfigPannel);
            }
        }   

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            
            if(info.Initiator is BasePlayer && entity) {
                var type = info.damageTypes.GetMajorityDamageType();
                if(type.ToString() == "Heat" && !configData.PluginSettings.EnableFire) return null;
                if(entity.ToString().Contains("corpse")) return null;
                if(((entity is BasePlayer && enablePlayers && !isNPC(entity)) || (entity is BaseNpc && enableNPC || entity is BasePlayer && isNPC(entity)) || (entity is BuildingBlock && enableBuildingBlocks) || ((entity.name.Contains("deploy") || entity.name.Contains("door") || entity.name.Contains("external")) && enableDeployables) || (entity is PatrolHelicopter && enableCopter) || (entity is BradleyAPC && enableBradley) || (entity is Minicopter && enableMini) || (entity is ScrapTransportHelicopter && enableScrapCopter) || (enableAllEntities))) {
                var ActMaxHealt = entity._maxHealth;
                var NetId = entity.net.ID.Value;
                
                NextTick(() =>
                        {
                            
                            var buildgrades = 5;
                            if(entity is BuildingBlock) {
                                var buildingrade = entity as BuildingBlock;
                                buildgrades = (int)buildingrade.grade;
                                
                            }
                        
                            var attacker = info?.InitiatorPlayer;
                            if (attacker == null)
                                return;
                            
                            if(permission.UserHasPermission(attacker.UserIDString, USE) || usesPerms == false) {
                                
                                if (storedData.PlayerStats.ContainsKey(attacker.UserIDString)) {
                                    
                                    if(storedData.PlayerStats[attacker.UserIDString].Status == "off") return;
                                    
                                    if(storedData.PlayerStats[attacker.UserIDString].Type == "blue") {
                                        ImageBarFillingStart = HitBarImageFillingBlue;
                                        ImageBarFillingEnd = HitBarImageFillingBlueEnd;
                                    }
                                    else if(storedData.PlayerStats[attacker.UserIDString].Type == "red") {
                                        ImageBarFillingStart = HitBarImageFillingRed;
                                        ImageBarFillingEnd = HitBarImageFillingRedEnd;
                                    }
                                    else if(storedData.PlayerStats[attacker.UserIDString].Type == "pink") {
                                        ImageBarFillingStart = HitBarImageFillingPink;
                                        ImageBarFillingEnd = HitBarImageFillingPinkEnd;
                                    }
                                    else if(storedData.PlayerStats[attacker.UserIDString].Type == "lime") {
                                        ImageBarFillingStart = HitBarImageFillingLime;
                                        ImageBarFillingEnd = HitBarImageFillingLimeEnd;
                                    }
                                    else if(storedData.PlayerStats[attacker.UserIDString].Type == "green") {
                                        ImageBarFillingStart = HitBarImageFillingGreen;
                                        ImageBarFillingEnd = HitBarImageFillingGreenEnd;
                                    }
                                    else if(storedData.PlayerStats[attacker.UserIDString].Type == "purple") {
                                        ImageBarFillingStart = HitBarImageFillingPurple;
                                        ImageBarFillingEnd = HitBarImageFillingPurpleEnd;
                                    }
                                    else if(storedData.PlayerStats[attacker.UserIDString].Type == "white") {
                                        ImageBarFillingStart = HitBarImageFillingWhite;
                                        ImageBarFillingEnd = HitBarImageFillingWhiteEnd;
                                    }
                                }
                                else {
                                    ImageBarFillingStart = HitBarImageFillingBlue;
                                    ImageBarFillingEnd = HitBarImageFillingBlueEnd;
                                }

                                
                                SpawnBar(attacker, entity, ImageBarFillingStart, ImageBarFillingEnd, buildgrades, ActMaxHealt, NetId);
                            }
                                
                        });   
                }
            }
            return null;

        }
        
        
        void hitbar(BasePlayer player, string command, string[] args)
        {
            if (args.Length <= 0) {

                if(permission.UserHasPermission(player.UserIDString, EDIT) || usesPerms == false)
                {
                    if (!storedData.PlayerStats.ContainsKey(player.UserIDString)) {
                    Hits value = new Hits();
                    storedData.PlayerStats.Add(player.UserIDString, value);
                    }
                    BarConfig(player); 
                }
                else {
                    message = lang.GetMessage("No permission", this, player.UserIDString);
                    SendReply(player, message);
                }
            }
            else if(args[0].ToLower() == "on") {
                    storedData.PlayerStats[player.UserIDString].Status = "on";
                    SaveData();
                    message = lang.GetMessage("Hitbar Turned ON", this, player.UserIDString);
                    SendReply(player, message);
            }
            else if (args[0].ToLower() == "off") {

                    storedData.PlayerStats[player.UserIDString].Status = "off";
                    SaveData();message = lang.GetMessage("Hitbar Turned OFF", this, player.UserIDString);
                    SendReply(player, message);
            }
            else if (args[0].ToLower() == "help") {

                    message = lang.GetMessage("Hitbar Help", this, player.UserIDString);
                    SendReply(player, message);                    
            }
            else {

                message = lang.GetMessage("Sintax Error", this, player.UserIDString);
                SendReply(player, message); 
            }
        }

        [ConsoleCommand("advancedhitbar.settings")]
        void contest(ConsoleSystem.Arg arg)
        {
            

            BasePlayer player = arg.Player();
            string[] args = arg.Args;
            string argument = args[0];

            if(!permission.UserHasPermission(player.UserIDString, EDIT) && usesPerms == true) {

                message = lang.GetMessage("No permission", this, player.UserIDString);
                SendReply(player, message);
            return;
            }
            if (!storedData.PlayerStats.ContainsKey(player.UserIDString)) {
                    Hits value = new Hits();
                    storedData.PlayerStats.Add(player.UserIDString, value);
            }

            switch (args[0])
            {
                case "1":      
                        storedData.PlayerStats[player.UserIDString].Type = "blue";
                        SaveData();
                        message = lang.GetMessage("Hitbar Color Change", this, player.UserIDString);
                        SendReply(player, message);
                    break;
                case "2":
                        storedData.PlayerStats[player.UserIDString].Type = "lime";
                        SaveData();
                        message = lang.GetMessage("Hitbar Color Change", this, player.UserIDString);
                        SendReply(player, message);
                    break;
                case "3":
                        storedData.PlayerStats[player.UserIDString].Type = "green";
                        SaveData();
                        message = lang.GetMessage("Hitbar Color Change", this, player.UserIDString);
                        SendReply(player, message);
                    break;
                case "4":
                        storedData.PlayerStats[player.UserIDString].Type = "red";
                        SaveData();
                        message = lang.GetMessage("Hitbar Color Change", this, player.UserIDString);
                        SendReply(player, message);
                    break;
                case "5":
                        storedData.PlayerStats[player.UserIDString].Type = "pink";
                        SaveData();
                        message = lang.GetMessage("Hitbar Color Change", this, player.UserIDString);
                        SendReply(player, message);
                    break;
                case "6":
                        storedData.PlayerStats[player.UserIDString].Type = "purple";
                        SaveData();
                        message = lang.GetMessage("Hitbar Color Change", this, player.UserIDString);
                        SendReply(player, message);
                    break;
                case "7":
                        storedData.PlayerStats[player.UserIDString].Type = "white";
                        SaveData();
                        message = lang.GetMessage("Hitbar Color Change", this, player.UserIDString);
                        SendReply(player, message);
                    break;
                case "8":
                        storedData.PlayerStats[player.UserIDString].Status = "on";
                        SaveData();
                        message = lang.GetMessage("Hitbar Turned ON", this, player.UserIDString);
                        SendReply(player, message);
                    break;
                case "9":
                        storedData.PlayerStats[player.UserIDString].Status = "off";
                        SaveData();
                        message = lang.GetMessage("Hitbar Turned OFF", this, player.UserIDString);
                        SendReply(player, message);
                    break;
                default:
                
                    break;
                
            }
            
                SaveData();
                
        }

        

        public void BarConfig(BasePlayer player)
        {
                CuiHelper.DestroyUi(player, MainConfigPannel);
                var elements = new CuiElementContainer();
                var mainconfigpanel = elements.Add(new CuiPanel { Image = { Color = "0.12 0.12 0.12 0" }, RectTransform = { AnchorMin = "0.224 0.119", AnchorMax = "0.732 0.953" }, CursorEnabled = true });    
                var BackgroundImage = new CuiElement { Parent = mainconfigpanel, Name = MainConfigPannel }; BackgroundImage.Components.Add(new CuiRawImageComponent { Png = ImageLibrary.Call<string>("GetImage", SettingsBackground) }); BackgroundImage.Components.Add(new CuiRectTransformComponent { AnchorMax = "1 1", AnchorMin = "0 0" });
                elements.Add(BackgroundImage);
                var BlueBarImage = new CuiElement { Parent = mainconfigpanel, Name = MainConfigPannel }; BlueBarImage.Components.Add(new CuiRawImageComponent { Png = ImageLibrary.Call<string>("GetImage", BlueExampleImage) }); BlueBarImage.Components.Add(new CuiRectTransformComponent { AnchorMax = "0.75 0.80", AnchorMin = "0.15 0.76" });
                elements.Add(BlueBarImage);
                var LimeBarImage = new CuiElement { Parent = mainconfigpanel, Name = MainConfigPannel }; LimeBarImage.Components.Add(new CuiRawImageComponent { Png = ImageLibrary.Call<string>("GetImage", LimeExampleImage) }); LimeBarImage.Components.Add(new CuiRectTransformComponent { AnchorMax = "0.75 0.7", AnchorMin = "0.15 0.66" });
                elements.Add(LimeBarImage);
                var GreenBarImage = new CuiElement { Parent = mainconfigpanel, Name = MainConfigPannel }; GreenBarImage.Components.Add(new CuiRawImageComponent { Png = ImageLibrary.Call<string>("GetImage", GreenExampleImage) }); GreenBarImage.Components.Add(new CuiRectTransformComponent { AnchorMax = "0.75 0.60", AnchorMin = "0.15 0.56" });
                elements.Add(GreenBarImage);
                var RedBarImage = new CuiElement { Parent = mainconfigpanel, Name = MainConfigPannel }; RedBarImage.Components.Add(new CuiRawImageComponent { Png = ImageLibrary.Call<string>("GetImage", RedExampleImage) }); RedBarImage.Components.Add(new CuiRectTransformComponent { AnchorMax = "0.75 0.50", AnchorMin = "0.15 0.46" });
                elements.Add(RedBarImage);
                var PinkBarImage = new CuiElement { Parent = mainconfigpanel, Name = MainConfigPannel }; PinkBarImage.Components.Add(new CuiRawImageComponent { Png = ImageLibrary.Call<string>("GetImage", PinkExampleImage) }); PinkBarImage.Components.Add(new CuiRectTransformComponent { AnchorMax = "0.75 0.40", AnchorMin = "0.15 0.36" });
                elements.Add(PinkBarImage);
                var PurpleBarImage = new CuiElement { Parent = mainconfigpanel, Name = MainConfigPannel }; PurpleBarImage.Components.Add(new CuiRawImageComponent { Png = ImageLibrary.Call<string>("GetImage", PurpleExampleImage) }); PurpleBarImage.Components.Add(new CuiRectTransformComponent { AnchorMax = "0.75 0.30", AnchorMin = "0.15 0.26" });
                elements.Add(PurpleBarImage); 
                var WhiteBarImage = new CuiElement { Parent = mainconfigpanel, Name = MainConfigPannel }; WhiteBarImage.Components.Add(new CuiRawImageComponent { Png = ImageLibrary.Call<string>("GetImage", WhiteeExampleImage) }); WhiteBarImage.Components.Add(new CuiRectTransformComponent { AnchorMax = "0.75 0.20", AnchorMin = "0.15 0.16" });
                elements.Add(WhiteBarImage);
                var closeButton = new CuiButton { Button =  { Close = mainconfigpanel, Color = "255 0 0 1" },  RectTransform = { AnchorMin = "0.785 0.825", AnchorMax = "0.9 0.875" }, Text = { Text = closeButtonText, FontSize = 15, Align = TextAnchor.MiddleCenter } };
                
                elements.Add(closeButton, mainconfigpanel);
                elements.Add(new CuiButton { Button = { Command = "advancedhitbar.settings 1" , Color = "0.22 0.84 0.22 0.8" }, RectTransform = { AnchorMax = "0.90 0.8", AnchorMin = "0.785 0.76" }, Text = { Text = "ACCEPT", Color = "1 1 1 1", FontSize = 15, Align=TextAnchor.MiddleCenter } }, mainconfigpanel);
                elements.Add(new CuiButton { Button = { Command = "advancedhitbar.settings 2" , Color = "0.22 0.84 0.22 0.8" }, RectTransform = { AnchorMax = "0.90 0.7", AnchorMin = "0.785 0.66" }, Text = { Text = "ACCEPT", Color = "1 1 1 1", FontSize = 15, Align=TextAnchor.MiddleCenter } }, mainconfigpanel);
                elements.Add(new CuiButton { Button = { Command = "advancedhitbar.settings 3" , Color = "0.22 0.84 0.22 0.8" }, RectTransform = { AnchorMax = "0.90 0.6", AnchorMin = "0.785 0.56" }, Text = { Text = "ACCEPT", Color = "1 1 1 1", FontSize = 15, Align=TextAnchor.MiddleCenter } }, mainconfigpanel);
                elements.Add(new CuiButton { Button = { Command = "advancedhitbar.settings 4" , Color = "0.22 0.84 0.22 0.8" }, RectTransform = { AnchorMax = "0.90 0.5", AnchorMin = "0.785 0.46" }, Text = { Text = "ACCEPT", Color = "1 1 1 1", FontSize = 15, Align=TextAnchor.MiddleCenter } }, mainconfigpanel);
                elements.Add(new CuiButton { Button = { Command = "advancedhitbar.settings 5" , Color = "0.22 0.84 0.22 0.8" }, RectTransform = { AnchorMax = "0.90 0.4", AnchorMin = "0.785 0.36" }, Text = { Text = "ACCEPT", Color = "1 1 1 1", FontSize = 15, Align=TextAnchor.MiddleCenter } }, mainconfigpanel);
                elements.Add(new CuiButton { Button = { Command = "advancedhitbar.settings 6" , Color = "0.22 0.84 0.22 0.8" }, RectTransform = { AnchorMax = "0.90 0.3", AnchorMin = "0.785 0.26" }, Text = { Text = "ACCEPT", Color = "1 1 1 1", FontSize = 15, Align=TextAnchor.MiddleCenter } }, mainconfigpanel);
                elements.Add(new CuiButton { Button = { Command = "advancedhitbar.settings 7" , Color = "0.22 0.84 0.22 0.8" }, RectTransform = { AnchorMax = "0.90 0.2", AnchorMin = "0.785 0.16" }, Text = { Text = "ACCEPT", Color = "1 1 1 1", FontSize = 15, Align=TextAnchor.MiddleCenter } }, mainconfigpanel);
                
                elements.Add(new CuiButton { Button = { Command = "advancedhitbar.settings 8" , Color = "0.22 0.84 0.22 0.8" }, RectTransform = { AnchorMax = "0.443 0.15", AnchorMin = "0.38 0.108" }, Text = { Text = "ON", Color = "1 1 1 1", FontSize = 15, Align=TextAnchor.MiddleCenter } }, mainconfigpanel);
                elements.Add(new CuiButton { Button = { Command = "advancedhitbar.settings 9" , Color = "255 0 0 0.8" }, RectTransform = { AnchorMax = "0.521 0.15", AnchorMin = "0.459 0.108" }, Text = { Text = "OFF", Color = "1 1 1 1", FontSize = 15, Align=TextAnchor.MiddleCenter } }, mainconfigpanel);
                
                elements.Add(new CuiLabel { Text = { Text = $"<color={textColor}>{textBox}</color>", FontSize = 40, Font = "RobotoCondensed-Bold.ttf", Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.25 0.80", AnchorMax = "0.74 0.90" } }, mainconfigpanel, ConfigTextBox);
                
                CuiHelper.AddUi(player, elements);  

        }

        bool isNPC(BaseCombatEntity entity)
        {
            var player = entity as BasePlayer; 
            if(player.userID >= 76560000000000000L || player.userID <= 0L)
            return false;
            else {return true;}
        }

        public void SpawnBar(BasePlayer player, BaseCombatEntity entity, string FillingStart, string FillingEnd, int grade, float PreviousHealth, float entNetID)
        {
                
                var ActualHealth = entity.health;
                var TotalHealth = entity._maxHealth;
                var EmptyBar = 0.02;
                if(entity is BasePlayer && !isNPC(entity)) {

                    var victim = entity as BasePlayer;
                    foreach (var check in configData.PluginSettings.HlthList)
                    {
                        
                        if (permission.UserHasPermission(victim.UserIDString, check.Key)) {

                            TotalHealth = check.Value;
                        }
                    }
                }
              
                if(grade != 5)
                {
                    if(grade == 0)
                    TotalHealth = 10;

                    else if(grade == 1)
                    TotalHealth = 250;

                    else if(grade == 2)
                    TotalHealth = 500;

                    else if(grade == 3)
                    TotalHealth = 1000;

                    else if(grade == 4)
                    TotalHealth = 2000;
                }
                
                if(entity.IsDead())
                    ActualHealth = 0;
                if(ActualHealth == 0)
                {
                    EmptyBar = 0;
                    storedData.EntityHealth.Remove(entNetID.ToString());
                    SaveData(); 
                }
                    

                var BarFilled = ActualHealth / TotalHealth; 
                
                if(BarFilled > 1) {

                 if (!storedData.EntityHealth.ContainsKey(entNetID.ToString())) {

                    EntHealth value = new EntHealth();
                    storedData.EntityHealth.Add(entNetID.ToString(), value);
                    if(ActualHealth > PreviousHealth) storedData.EntityHealth[entNetID.ToString()].MaxHealth = ActualHealth;
                    else storedData.EntityHealth[entNetID.ToString()].MaxHealth = ActualHealth;
                    SaveData();                    
                    }

                    BarFilled = ActualHealth / storedData.EntityHealth[entNetID.ToString()].MaxHealth; 

                }

                CuiHelper.DestroyUi(player, Bar);
                CuiHelper.DestroyUi(player, BarFilling);
                CuiHelper.DestroyUi(player, BarFillingEnd);
                var elements = new CuiElementContainer();
                var BarBorders = elements.Add(new CuiPanel { Image = { Color = "0.1 0.1 0.1 0" }, RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax }, CursorEnabled = false });    
                var image = new CuiElement { Parent = BarBorders, Name = Bar }; image.Components.Add(new CuiRawImageComponent { Png = ImageLibrary.Call<string>("GetImage", HitBarImage) }); image.Components.Add(new CuiRectTransformComponent { AnchorMax = "1 1", AnchorMin = "0 0" });
                var image2 = new CuiElement { Parent = BarBorders, Name = BarFilling }; image2.Components.Add(new CuiRawImageComponent { Png = ImageLibrary.Call<string>("GetImage", FillingStart) }); image2.Components.Add(new CuiRectTransformComponent { AnchorMax = $"{EmptyBar} 0.75", AnchorMin = "0.007 0.15" });
                var image3 = new CuiElement { Parent = BarBorders, Name = BarFillingEnd }; image3.Components.Add(new CuiRawImageComponent { Png = ImageLibrary.Call<string>("GetImage", FillingEnd) }); image3.Components.Add(new CuiRectTransformComponent { AnchorMax = $"{BarFilled} 0.75", AnchorMin = "0.02 0.15" });
                elements.Add(image);
                elements.Add(image2);
                elements.Add(image3);
                CuiHelper.AddUi(player, elements);  
                timer.Once(timers, () => { CuiHelper.DestroyUi(player, BarBorders); });
            
        }

        #region PluginConfig

        public class ConfigData {

            [JsonProperty(PropertyName = "Plugin Settings")]
            public GeneralSettings PluginSettings = new GeneralSettings();
            
            [JsonProperty(PropertyName = "Image Settings")]
            public IMGSetting ImageSettings = new IMGSetting();

        }

        public class GeneralSettings {

            [JsonProperty(PropertyName = "- Only users with perms can see the alert")]
            public bool UsesPerms = false;

            [JsonProperty(PropertyName = "- Time alert will be displayed (Seconds)")]
            public float Timer = 3f;

            [JsonProperty(PropertyName = "- UI Anchor Min")]
            public string AnchorMin = "0.356 0.117";

            [JsonProperty(PropertyName = "- UI Anchor Max")]
            public string AnchorMax = "0.63 0.158";
            
            [JsonProperty(PropertyName = "- Config Text")]
            public string TextBox = "HITBAR SETTINGS";

            [JsonProperty(PropertyName = "- Config Color")]
            public string TextColor = "#ffffff";

            [JsonProperty(PropertyName = "Copter - Enable health bar")]
            public bool EnableCopter = true;
            
            [JsonProperty(PropertyName = "Bradley - Enable health bar")]
            public bool EnableBradley = true;
            
            [JsonProperty(PropertyName = "Players - Enable health bar")]
            public bool EnablePlayers = true;
            
            [JsonProperty(PropertyName = "NPC - Enable health bar (Animals, Scientists, Scarecrows, etc.)")]
            public bool EnableNPC = true;
            
            [JsonProperty(PropertyName = "Minicopter - Enable health bar")]
            public bool EnableMini = true;
            
            [JsonProperty(PropertyName = "ScrapCopter - Enable health bar")]
            public bool EnableScrapCopter = true;

            [JsonProperty(PropertyName = "Deployables - Enable health bar")]
            public bool EnableDeployables = true;

            [JsonProperty(PropertyName = "Building Blocks - Enable health bar")]
            public bool EnableBuildingBlocks = true;

            [JsonProperty(PropertyName = "All Entities - Enable health bar")]
            public bool EnableAllEntities = false;

            [JsonProperty(PropertyName = "Show hitbar when damage type is fire (Not Recommended)")]
            public bool EnableFire = false;
            
            [JsonProperty(PropertyName = "- Accept button text")]
            public string AcceptButtonText = "ACCEPT";

            [JsonProperty(PropertyName = "- Close button text")]
            public string CloseButtonText = "CLOSE";
            
            [JsonProperty(PropertyName = "- Config Chat command")]
            public string ChatCommand = "hitbar";

            [JsonProperty(PropertyName = "- Config Custom Players Health (ONLY CHANGE THIS IF YOU CHANGED THE DEFAULT PLAYERS HEALTH)")]
            public int CustomHealth = 100;

            [JsonProperty("- Config Custom Players Health (Assign the perm to the the players that have different health)")]
            public Dictionary<string, int> HlthList = new Dictionary<string, int>()
            {
                ["advancedhitbar.vip"] = 150,
                ["advancedhitbar.vip1"] = 200,
            };

            

        }

        public class IMGSetting {

            [JsonProperty(PropertyName = "Hitbar borders")]
            public string HitBarImag = "https://media.discordapp.net/attachments/1155876595215638548/1155876753370259607/Healthbar-min.png?width=1440&height=98";

            [JsonProperty(PropertyName = "Hitbar fill blue")]
            public string HitBarImageFillingBlu = "https://media.discordapp.net/attachments/1155876595215638548/1155879791883464734/azulinicio.png";
            
            [JsonProperty(PropertyName = "Hitbar fill blue end")]
            public string HitBarImageFillingBlueEn = "https://media.discordapp.net/attachments/1155876595215638548/1155879523464777728/azuldentro22.png";
            
            [JsonProperty(PropertyName = "Hitbar fill red")]
            public string HitBarImageFillingRe = "https://media.discordapp.net/attachments/1155876595215638548/1155878332152434769/rojoinicio-min.png";
            
            [JsonProperty(PropertyName = "Hitbar fill red end")]
            public string HitBarImageFillingRedEn = "https://media.discordapp.net/attachments/1155876595215638548/1155877835320340582/rojocortado-min.png";
            
            [JsonProperty(PropertyName = "Hitbar fill pink")]
            public string HitBarImageFillingPin = "https://media.discordapp.net/attachments/1155876595215638548/1155878333687549982/rosainicio-min.png";
            
            [JsonProperty(PropertyName = "Hitbar fill pink end")]
            public string HitBarImageFillingPinkEn = "https://media.discordapp.net/attachments/1155876595215638548/1155878332416663614/rosacortado-min.png";
            
            [JsonProperty(PropertyName = "Hitbar fill lime")]
            public string HitBarImageFillingLim = "https://media.discordapp.net/attachments/1155876595215638548/1155877833944600637/limainicio-min.png";
            
            [JsonProperty(PropertyName = "Hitbar fill lime end")]
            public string HitBarImageFillingLimeEn = "https://media.discordapp.net/attachments/1155876595215638548/1155876753647091854/limacortado-min.png";
            
            [JsonProperty(PropertyName = "Hitbar fill purple")]
            public string HitBarImageFillingPurpl = "https://media.discordapp.net/attachments/1155876595215638548/1155877834812821609/moradoinicio-min.png";
            
            [JsonProperty(PropertyName = "Hitbar fill purple end")]
            public string HitBarImageFillingPurpleEn = "https://media.discordapp.net/attachments/1155876595215638548/1155877834200457266/moradocortado-min.png";
            
            [JsonProperty(PropertyName = "Hitbar fill white")]
            public string HitBarImageFillingWhit = "https://media.discordapp.net/attachments/1155876595215638548/1155876752552370237/blancoinicio-min.png";
            
            [JsonProperty(PropertyName = "Hitbar fill white end")]
            public string HitBarImageFillingWhiteEn = "https://media.discordapp.net/attachments/1155876595215638548/1155879523972284547/blancocortado.png";
            
            [JsonProperty(PropertyName = "Hitbar fill green")]
            public string HitBarImageFillingGree = "https://media.discordapp.net/attachments/1155876595215638548/1155878334689988680/verdeinicio-min.png";
            
            [JsonProperty(PropertyName = "Hitbar fill green end")]
            public string HitBarImageFillingGreenEn = "https://media.discordapp.net/attachments/1155876595215638548/1155878333981138974/verdecortado-min.png";
            
            [JsonProperty(PropertyName = "Blue example")]
            public string BlueExampleImag = "https://media.discordapp.net/attachments/1155876595215638548/1155876752019701921/azulejemplo-min.png";
            
            [JsonProperty(PropertyName = "Lime example")]
            public string LimeExampleImag = "https://media.discordapp.net/attachments/1155876595215638548/1155876752795648000/cyanejemplo-min.png";
            
            [JsonProperty(PropertyName = "Green example")]
            public string GreenExampleImag = "https://media.discordapp.net/attachments/1155876595215638548/1155878334287319060/verdeejemplo-min.png";
            
            [JsonProperty(PropertyName = "Red example")]
            public string RedExampleImag = "https://media.discordapp.net/attachments/1155876595215638548/1155877835551019028/rojoejemplo-min.png";
            
            [JsonProperty(PropertyName = "Pink example")]
            public string PinkExampleImag = "https://media.discordapp.net/attachments/1155876595215638548/1155878332706066565/rosaejemplo-min.png";
            
            [JsonProperty(PropertyName = "Purple example")]
            public string PurpleExampleImag = "https://media.discordapp.net/attachments/1155876595215638548/1155877834481479840/moradoejemplo-min.png";
            
            [JsonProperty(PropertyName = "White example")]
            public string WhiteeExampleImag = "https://media.discordapp.net/attachments/1155876595215638548/1155876752250392740/blancoejemplo-min.png";
            
            [JsonProperty(PropertyName = "Settings Background")]
            public string SettingsBackgroun = "https://media.discordapp.net/attachments/1155876595215638548/1155876753059893339/fondo2-min.png?width=957&height=676";
            
            
        }



		private bool LoadConfigVariables() {
            try
            {
            configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
            return false;
            }
            SaveConf();
            return true;
        }

        protected override void LoadDefaultConfig() {
            Puts("Fresh install detected, creating a new config file.");
            configData = new ConfigData();
            SaveConf();
        }

        void SaveConf() => Config.WriteObject(configData, true);
        #endregion

        #region Data
        
        StoredData storedData;
        class StoredData
        {
            public Dictionary<string, Hits> PlayerStats = new Dictionary<string, Hits>();
            public Dictionary<string, EntHealth> EntityHealth = new Dictionary<string, EntHealth>();
        }
        class Hits
        {
            public string Type;
            public string Status;
        }
        class EntHealth
        {
            public float MaxHealth;
        }
        void Loaded()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("AdvancedHitBar");
            Interface.Oxide.DataFileSystem.WriteObject("AdvancedHitBar", storedData);
        }
        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("AdvancedHitBar", storedData);
        }
        #endregion

    }
}