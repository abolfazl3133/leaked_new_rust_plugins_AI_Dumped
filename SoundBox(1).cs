using System.Linq;
using System;
//using System.Reflection;
using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using System.Collections;
using Oxide.Game.Rust.Cui;
using System.Globalization;
using Oxide.Core.Plugins;
using VLB;
using Network;

namespace Oxide.Plugins
{
    [Info("SoundBox", "Razor", "1.2.1")]
    [Description("Play some streams")]
    public class SoundBox : RustPlugin
    {
        [PluginReference] Plugin ImageLibrary;
        private static SoundBox _;
        static System.Random random = new System.Random();
        private static Dictionary<ulong, bool> BoomBoxEntityPlayer = new Dictionary<ulong, bool>();
        private static string permAdmin = "soundbox.admin";
        private static string permUse = "soundbox.use";

        #region Init
        private void Init()
        {
            _ = this;
            RegisterPermissions();
        }

        private void OnServerInitialized()
        {
            GenerateGlobal();
            if (ImageLibrary == null)
            {
                Puts($"ImageLibrary not detected. Unloading {Name}");
                covalence.Server.Command($"o.unload {Name}");
                return;
            }
            LoadImages();

            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                OnActiveItemChange(player, null, 0);
            }
        }

        private void LoadImages()
        {
            ImageLibrary?.Call("AddImage", configData.settings.BackgroundImage, configData.settings.BackgroundImage);
            ImageLibrary?.Call("AddImage", configData.settings.ImageAdminPicks, configData.settings.ImageAdminPicks);
            ImageLibrary?.Call("AddImage", configData.settings.ImageHeader, configData.settings.ImageHeader);
            ImageLibrary?.Call("AddImage", configData.settings.ImageSongButton, configData.settings.ImageSongButton);
            ImageLibrary?.Call("AddImage", configData.settings.ImageRemoveButton, configData.settings.ImageRemoveButton);
        }

        private void Unload()
        {
            foreach (var Controler in UnityEngine.Object.FindObjectsOfType<BoomBoxPlayer>())
            {
                UnityEngine.Object.Destroy(Controler);
            }
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                CuiHelper.DestroyUi(player, "theUIConfig");
            }

            _ = null;
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permUse, this);
        }
        #endregion Init

        #region Config
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Image Links")]
            public Settings settings { get; set; }

            [JsonProperty(PropertyName = "General Settings")]
            public Settings2 settings2 { get; set; }

            public class Settings
            {
                public string BackgroundImage { get; set; }
                public string ImageAdminPicks { get; set; }
                public string ImageHeader { get; set; }
                public string ImageSongButton { get; set; }
                public string ImageRemoveButton { get; set; }
            }

            public class Settings2
            {
                public List<string> MustContain { get; set; }
                public List<string> BlockedUrls { get; set; }
                public int TotalAllowedPlayerUrls { get; set; }
            }

                public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                settings = new ConfigData.Settings
                {
                    BackgroundImage = "https://i.ibb.co/JKs9163/background.png",
                    ImageHeader = "https://i.ibb.co/tY8HjKJ/header1.png",
                    ImageAdminPicks = "https://i.ibb.co/tY8HjKJ/header1.png",
                    ImageSongButton = "https://i.ibb.co/7JYzKMw/Button-Songs.png",
                    ImageRemoveButton = "https://i.ibb.co/yqrLDv2/delete.png",
                },

                settings2 = new ConfigData.Settings2
                {
                    MustContain = new List<string>() { "http" },
                    BlockedUrls = new List<string>() { ".php", ".htm", "?" },
                    TotalAllowedPlayerUrls = 20
                },

                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();           

            if (configData.Version < new VersionNumber(1, 0, 4))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion Config

        #region heldBoomBox Conroler

        public class BoomBoxPlayer : MonoBehaviour
        {
            private BasePlayer player { get; set; }
            private float nextPressTime { get; set; }
            private HeldBoomBox heldBoomBox { get; set; }

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                float delay = random.Next(60, 120);
                InvokeRepeating("ItemCheck", delay, 601);
            }

            private void ItemCheck()
            {
                if (player?.GetActiveItem()?.GetHeldEntity() as HeldBoomBox == null)
                {
                    UnityEngine.GameObject.Destroy(this);
                    return;
                }
            }

            private void Update()
            {
                if (player.serverInput.WasJustPressed(BUTTON.RELOAD))
                {
                    float time = Time.realtimeSinceStartup;
                    if (nextPressTime < time)
                    {
                        heldBoomBox = player?.GetActiveItem()?.GetHeldEntity() as HeldBoomBox;
                        if (heldBoomBox == null || heldBoomBox.BoxController == null)
                        {
                            UnityEngine.GameObject.Destroy(this);
                            return;
                        }
                        nextPressTime = time + 0.1f;
                        _.UrlSpeakerUsageHeld(player, "stream", null);
                    }
                }
            }
        }

        #endregion heldBoomBox Conroler

        #region HooksAnCommands

        private void OnActiveItemChange(BasePlayer player, Item oldItem, uint newItemId)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, permUse) && !permission.UserHasPermission(player.UserIDString, permAdmin))
                return;

            NextTick(() =>
            {
                HeldBoomBox boomBox = player?.GetActiveItem()?.GetHeldEntity() as HeldBoomBox;
                if (boomBox != null)
                    player.GetOrAddComponent<BoomBoxPlayer>();
            });
        }

        private object CanLootEntity(BasePlayer player, DeployableBoomBox deployableBoomBox)
        {
            if (permission.UserHasPermission(player.UserIDString, permAdmin))
            {
                if (BoomBoxEntityPlayer.ContainsKey(player.userID))
                    return null;
            }
            else if (!permission.UserHasPermission(player.UserIDString, permUse) || BoomBoxEntityPlayer.ContainsKey(player.userID))
                     return null;

            if (deployableBoomBox != null)
            {
                TheData SaveFilePlayer = getPlayerData(player);
                TheData SaveFilePlayerG = getPlayerData(player, true);

                if (SaveFilePlayer == null)
                {
                    PrintWarning("could not load save player data");
                    return null;
                }

                if (SaveFilePlayerG == null)
                {
                    PrintWarning("could not load save Global data");
                    return null;
                }

                theUIConfigMenu(player, deployableBoomBox.net.ID.Value, SaveFilePlayer, SaveFilePlayerG);
                return true;
            }
            return null;
        }

        [ChatCommand("stream")]
        private void UrlSpeakerUsageHeld(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permUse) && !permission.UserHasPermission(player.UserIDString, permAdmin))
                return;

            HeldBoomBox boomBox = player?.GetActiveItem()?.GetHeldEntity() as HeldBoomBox;
            if (boomBox != null)
            {
                TheData SaveFilePlayer = getPlayerData(player);
                TheData SaveFilePlayerG = getPlayerData(player, true);

                if (SaveFilePlayer == null)
                {
                    PrintWarning("could not load save player data");
                    return;
                }

                if (SaveFilePlayerG == null)
                {
                    PrintWarning("could not load save Global data");
                    return;
                }

                theUIConfigMenu(player, boomBox.net.ID.Value, SaveFilePlayer, SaveFilePlayerG);
                return;
            }
            SendReply(player, lang.GetMessage("NoBoomBox", this, player.UserIDString));
        }

        [ChatCommand("boombox")]
        private void UrlSpeakerUsage(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, permUse) || permission.UserHasPermission(player.UserIDString, permAdmin))
            {

                if (args.Length == 0)
                {
                    if (BoomBoxEntityPlayer.ContainsKey(player.userID))
                    {
                        BoomBoxEntityPlayer.Remove(player.userID);
                        SendReply(player, lang.GetMessage("active", this, player.UserIDString));
                    }
                    else
                    {
                        BoomBoxEntityPlayer.Add(player.userID, true);
                        SendReply(player, lang.GetMessage("deactive", this, player.UserIDString));
                    }
                    return;
                }
            }
            if (args.Length == 3 && permission.UserHasPermission(player.UserIDString, permAdmin))
            {
                switch (args[0].ToLower())
                {
                    case "picks":
                        addAdminPicks(player, CombineWords(args[1]), args[2]);
                        return;

                    default:
                        break;
                }
            }
            else SendReply(player, lang.GetMessage("IncorectUsage", this, player.UserIDString));
        }

        private void addAdminPicks(BasePlayer player, string name, string url)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
            {
                SendReply(player, lang.GetMessage("PICKINVALIDFORMAT", this, player.UserIDString));
                return;
            }

            if (url.ToLower().Contains("dropbox") && !url.ToLower().Contains("dropboxusercontent"))
            {
                url = url.Replace("dropbox", "dl.dropboxusercontent").Replace("www.", "").Replace("?dl=0", "").Replace("?dl=1", "");
            }

            foreach (string key in configData.settings2.MustContain)
            if (!url.ToLower().Contains(key))
            {
                SendReply(player, lang.GetMessage("BadUrlKey", this, player.UserIDString), key);
                return;
            }
 
            foreach (string key in configData.settings2.BlockedUrls)
            if (url.ToLower().Contains(key) && (!url.ToLower().Contains("drive.google.com") && !url.ToLower().Contains("dropboxusercontent")))
            {
                SendReply(player, lang.GetMessage("BlockedUrl", this, player.UserIDString), key);
                return;
            }

            TheData SaveFilePlayerG = null;
            if (!HasSaveFile("Global"))
            {
                SaveFilePlayerG = new TheData();
                SavePlayData(SaveFilePlayerG, Name + $"/Global/" + "Global");
            }

            LoadData(out SaveFilePlayerG, Name + $"/Global/" + "Global");

            if (SaveFilePlayerG == null)
            {
                SendReply(player, lang.GetMessage("BadSaveFile", this, player.UserIDString));
                return;
            }

            saveUrl(player, name, url, true);
        }

        public void ChangeUrl(BoomBox boxController, string url, BasePlayer player, bool autoReplay = true)
        {
            bool wasPlaying = false;
            if (url.ToLower().Contains("dropbox") && !url.ToLower().Contains("dropboxusercontent"))
            {
                url = url.Replace("dropbox", "dl.dropboxusercontent").Replace("www.", "").Replace("?dl=0", "").Replace("?dl=1", "");
            }

            if (boxController != null)
            {
                if (boxController.IsOn())
                {
                    wasPlaying = true;
                    boxController.ServerTogglePlay(false);
                }

                if (player != null)
                    boxController.AssignedRadioBy = player.userID;

                boxController.CurrentRadioIp = url;
                boxController.baseEntity.ClientRPC<string>((Connection)null, "OnRadioIPChanged", url);

                NextTick(() =>
                {
                    if (boxController != null)
                    {
                        if (autoReplay && wasPlaying && !boxController.IsOn())
                            boxController.ServerTogglePlay(true);
                        boxController.baseEntity.SendNetworkUpdateImmediate(true);
                    }
                });
            }
        }

        private string CombineWords(string oldstring)
        {
            string new_text = oldstring.Replace(" ", "_");
            return new_text;
        }

        private void connectSpeakers(IOEntity entity)
        {
            if (entity is IOEntity)
            {
                IOEntity.IOSlot output = entity.outputs[0];
                if (output != null)
                {
                    IOEntity ioSource = output.connectedTo.Get(true);
                    if (ioSource != null)
                    {
                        ioSource.ensureOutputsUpdated = true;
                        ioSource.OnCircuitChanged(true);
                        ioSource.SendIONetworkUpdate();
                        connectSpeakers(ioSource);
                       
                    }
                }
                
            }
        }
        #endregion HooksAndCommands

        #region Data
        class TheData
        {
            public Dictionary<string, string> SavedURLS = new Dictionary<string, string>();
        }

        public bool HasSaveFile(string id) =>      
             Interface.Oxide.DataFileSystem.ExistsDatafile(Name + $"/{id.ToString()}/" + id);
        

        private static void SavePlayData<T>(T data, string filename = null) =>
            Interface.Oxide.DataFileSystem.WriteObject(filename ?? _.Name, data);

        private static void LoadData<T>(out T data, string filename = null) =>
            data = Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? _.Name);

        private void GenerateGlobal()
        {
            if (!HasSaveFile("Global"))
            {
                TheData SaveFilePlayer = new TheData();
                SaveFilePlayer.SavedURLS.Add("Hot_Hits", "http://stream.dotpoint.nl:8000/hothitz");
                SavePlayData(SaveFilePlayer, Name + $"/Global/" + "Global");
            }
        }

        private TheData getPlayerData(BasePlayer player, bool global = false)
        {
            TheData SaveFilePlayer = null;
            if (global)
            {
                if (!HasSaveFile("Global"))
                {
                    SaveFilePlayer = new TheData();
                    SavePlayData(SaveFilePlayer, Name + $"/Global/" + "Global");
                }

                LoadData(out SaveFilePlayer, Name + $"/Global/" + "Global");

                if (SaveFilePlayer == null)
                {
                    PrintWarning("could not load save global data");
                    return null;
                }
            }
            else
            {
                if (!HasSaveFile(player.userID.ToString()))
                {
                    SaveFilePlayer = new TheData();
                    SavePlayData(SaveFilePlayer, Name + $"/{player.userID.ToString()}/" + player.userID.ToString());
                }

                LoadData(out SaveFilePlayer, Name + $"/{player.userID.ToString()}/" + player.userID.ToString());

                if (SaveFilePlayer == null)
                {
                    PrintWarning("could not load save player data");
                    return null;
                }
            }
            return SaveFilePlayer;
        }

        private void saveUrl(BasePlayer player, string name, string url, bool global = false)
        {
            TheData SaveFilePlayer = null;

            if (url.ToLower().Contains("dropbox") && !url.ToLower().Contains("dropboxusercontent"))
            {
                url = url.Replace("dropbox", "dl.dropboxusercontent").Replace("www.", "").Replace("?dl=0", "").Replace("?dl=1", "");
            }

            foreach (string key in configData.settings2.MustContain)
            if (!url.ToLower().Contains(key))
            {
                SendReply(player, lang.GetMessage("BadUrlKey", this, player.UserIDString), key);
                return;
            }

            foreach (string key in configData.settings2.BlockedUrls)
            if (url.ToLower().Contains(key) && (!url.ToLower().Contains("drive.google.com") && !url.ToLower().Contains("dropboxusercontent")))
            {
                SendReply(player, lang.GetMessage("BlockedUrl", this, player.UserIDString), key);
                return;
            }

            if (global)
            {
                SaveFilePlayer = getPlayerData(player, true);
                if (SaveFilePlayer == null)
                {
                    PrintWarning("could not load save player data");
                    return;
                }

                if (!SaveFilePlayer.SavedURLS.ContainsKey(name))
                {
                    SaveFilePlayer.SavedURLS.Add(name, url);
                    SavePlayData(SaveFilePlayer, Name + $"/Global/" + "Global");
                    SendReply(player, lang.GetMessage("urlAdded", this, player.UserIDString));
                }
                else
                {
                    SendReply(player, lang.GetMessage("exists", this, player.UserIDString));
                }
            }
            else
            {
                SaveFilePlayer = getPlayerData(player);
                if (SaveFilePlayer == null)
                {
                    PrintWarning("could not load save player data");
                    return;
                }

                if (SaveFilePlayer.SavedURLS.Count >= configData.settings2.TotalAllowedPlayerUrls)
                {
                    SendReply(player, lang.GetMessage("maxUsage", this, player.UserIDString), configData.settings2.TotalAllowedPlayerUrls);
                }
                else if (!SaveFilePlayer.SavedURLS.ContainsKey(name))
                {
                    SaveFilePlayer.SavedURLS.Add(name, url);
                    SavePlayData(SaveFilePlayer, Name + $"/{player.userID.ToString()}/" + player.userID.ToString());
                }
                else
                {
                    SendReply(player, lang.GetMessage("exists", this, player.UserIDString));
                }
            }
        }

        private void removeUrl(BasePlayer player, string name, bool global = false)
        {
            TheData SaveFilePlayer = null;
            if (global)
            {
                SaveFilePlayer = getPlayerData(player, true);
                if (SaveFilePlayer == null)
                {
                    PrintWarning("could not load save player data");
                    return;
                }

                if (SaveFilePlayer.SavedURLS.ContainsKey(name))
                {
                    SaveFilePlayer.SavedURLS.Remove(name);
                    SavePlayData(SaveFilePlayer, Name + $"/Global/" + "Global");
                }
                else
                {
                    PrintWarning("save url removed already");
                }
            }
            else
            {
                SaveFilePlayer = getPlayerData(player);
                if (SaveFilePlayer == null)
                {
                    PrintWarning("could not load save player data");
                    return;
                }

                if (SaveFilePlayer.SavedURLS.ContainsKey(name))
                {
                    SaveFilePlayer.SavedURLS.Remove(name);
                    SavePlayData(SaveFilePlayer, Name + $"/{player.userID.ToString()}/" + player.userID.ToString());
                }
                else
                {
                    PrintWarning("save url removed already");
                }
            }
        }
        #endregion Data
        
        #region UI
        private void theUIConfigMenu(BasePlayer player, ulong theNet, TheData SaveFilePlayer, TheData GlobalFile)
        {
            string bImage = "";
            string ImageAdminPicks = "";
            string ImageHeader = "";
            string ImageSongButton = "";
            string ImageRemoveButton = "";

            if (!string.IsNullOrEmpty(configData.settings.BackgroundImage))
                bImage = ImageLibrary?.Call<string>("GetImage", configData.settings.BackgroundImage);

            if (!string.IsNullOrEmpty(configData.settings.ImageAdminPicks))
                ImageAdminPicks = ImageLibrary?.Call<string>("GetImage", configData.settings.ImageAdminPicks);

            if (!string.IsNullOrEmpty(configData.settings.ImageHeader))
                ImageHeader = ImageLibrary?.Call<string>("GetImage", configData.settings.ImageHeader);

            if (!string.IsNullOrEmpty(configData.settings.ImageSongButton))
                ImageSongButton = ImageLibrary?.Call<string>("GetImage", configData.settings.ImageSongButton);

            if (!string.IsNullOrEmpty(configData.settings.ImageRemoveButton))
                ImageRemoveButton = ImageLibrary?.Call<string>("GetImage", configData.settings.ImageRemoveButton);

            CuiHelper.DestroyUi(player, "theUIConfig");
            double movementUP = 0.850;
            double movementDown = 0.895;
            double movementUP2 = 0.350;
            double movementDown2 = 0.395;

            double buttonUP = 0.850;
            double buttonDown = 0.895;
            double buttonUP2 = 0.350;
            double buttonDown2 = 0.395;

            int line = 0;
            Color backgroundColor;
            ColorExtensions.TryParseHexString("#404040", out backgroundColor);
            var elements = new CuiElementContainer();
            {
                var ConfigMenu2 = elements.Add(new CuiPanel
                {
                    Image = { Color = HexToColor("#000000") },
                    RectTransform = { AnchorMin = "0.20 0.2", AnchorMax = "0.80 0.90" },
                    CursorEnabled = true
                }, "Overlay", "theUIConfig");


             
                var ConfigMenu = elements.Add(new CuiPanel
                {
                    Image = { Color = ColorExtensions.ToRustFormatString(backgroundColor) },
                    RectTransform = { AnchorMin = "0.0005 0.0005", AnchorMax = "0.998 0.997" },
                    CursorEnabled = true
                }, "theUIConfig", "parentGray");

                elements.Add(new CuiElement
                {
                        Parent = ConfigMenu,
                        Components = { new CuiRawImageComponent  { Png = bImage }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } }
                });

                elements.Add(new CuiElement
                {
                    Parent = ConfigMenu,
                    Components = { new CuiRawImageComponent { Png = ImageHeader },
                    new CuiRectTransformComponent { AnchorMin = "0 0.93", AnchorMax = "1 1" }
                }});

                var closeThis = elements.Add(new CuiButton
                {
                    Button = { Close = ConfigMenu2, Color = "255 255 255 0" },
                    RectTransform = { AnchorMin = "0.85 0.93", AnchorMax = ".99 1" }, 
                    Text = { Text = $"<B>{lang.GetMessage("Exit", this)}</B>", FontSize = 15, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                }, ConfigMenu);

                

                if (configData.settings2.TotalAllowedPlayerUrls > 0)
                {
                    elements.Add(new CuiElement
                    {
                        Parent = ConfigMenu,
                        Components = { new CuiRawImageComponent { Png = ImageAdminPicks },
                        new CuiRectTransformComponent { AnchorMin = "0 0.43", AnchorMax = "1 0.48" } }
                    });

                    elements.Add(new CuiLabel
                    {
                        Text = { Text = $"<B>{lang.GetMessage("AdminPicks", this)}</B>", FontSize = 15, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                        RectTransform = { AnchorMin = "0 0.43", AnchorMax = "0.2 0.48" }
                    }, ConfigMenu);

                    var newURL = elements.Add(new CuiButton
                    {
                        Button = { Command = $"AddPlayerSoundUrls {theNet} not", Color = "255 255 255 0" },
                        RectTransform = { AnchorMin = "0.65 0.93", AnchorMax = ".84 1" },
                        Text = { Text = $"<B>{lang.GetMessage("AddUrlNew", this)}</B>", FontSize = 15, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                    }, ConfigMenu);

                    elements.Add(new CuiLabel
                    {
                        Text = { Text = $"<B>{lang.GetMessage("PersonalSongs", this)}</B>", FontSize = 15, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                        RectTransform = { AnchorMin = "0 0.93", AnchorMax = "0.2 1" }
                    }, ConfigMenu);

                    if (SaveFilePlayer != null)
                        foreach (var config in SaveFilePlayer.SavedURLS.ToList())
                        {
                            line++;
                            List<int> MoveRight = new List<int>() { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21 };
                            string name = config.Key;
                            string up = "0.05 " + movementUP.ToString();
                            string down = "0.43 " + movementDown.ToString();
                            string buttonup = "0.43 " + buttonUP.ToString();
                            string buttondown = "0.46 " + buttonDown.ToString();
                            if (!MoveRight.Contains(line))
                            {
                                up = "0.55 " + movementUP.ToString();
                                down = "0.91 " + movementDown.ToString();
                                movementUP = movementUP - 0.050;
                                movementDown = movementDown - 0.050;

                                buttonup = "0.91 " + buttonUP.ToString();
                                buttondown = "0.94 " + buttonDown.ToString();
                                buttonUP = buttonUP - 0.050;
                                buttonDown = buttonDown - 0.050;
                            }

                            elements.Add(new CuiElement
                            {
                                Parent = ConfigMenu,
                                Components = { new CuiRawImageComponent { Png = ImageSongButton },
                                new CuiRectTransformComponent { AnchorMin = up, AnchorMax = down } }
                            });

                            var buttonsong = elements.Add(new CuiButton
                            {
                                Button = { Command = $"hardestSetSound {name} {theNet} notsomthing", Close = ConfigMenu2, Color = "255 255 255 0" },
                                RectTransform = { AnchorMin = up, AnchorMax = down },
                                Text = { Text = "<b>" + name + "</b>", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                            }, ConfigMenu);

                            elements.Add(new CuiElement
                            {
                                Parent = ConfigMenu,
                                Components = { new CuiRawImageComponent { Png = ImageRemoveButton },
                                new CuiRectTransformComponent { AnchorMin = buttonup, AnchorMax = buttondown } }
                            });

                            var ConfigOption = elements.Add(new CuiButton
                            {
                                Button = { Command = $"hardestRemoveSound {name} isUsers {theNet}", Close = ConfigMenu2, Color = "255 255 255 0" },
                                RectTransform = { AnchorMin = buttonup, AnchorMax = buttondown },
                                Text = { Text = "", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                            }, ConfigMenu);



                        }
                    line = 0;
                    if (GlobalFile != null)
                        foreach (var config in GlobalFile.SavedURLS.ToList())
                        {
                            line++;
                            List<int> MoveRight = new List<int>() { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21 };
                            string name = config.Key;
                            string up = "0.05 " + movementUP2.ToString();
                            string down = "0.43 " + movementDown2.ToString();
                            string buttonup2 = "0.43 " + buttonUP2.ToString();
                            string buttondown2 = "0.46 " + buttonDown2.ToString();


                            if (!MoveRight.Contains(line))
                            {
                                up = "0.50 " + movementUP2.ToString();
                                down = "0.91 " + movementDown2.ToString();
                                movementUP2 = movementUP2 - 0.050;
                                movementDown2 = movementDown2 - 0.050;

                                buttonup2 = "0.91 " + buttonUP2.ToString();
                                buttondown2 = "0.94 " + buttonDown2.ToString();
                                buttonUP2 = buttonUP2 - 0.050;
                                buttonDown2 = buttonDown2 - 0.050;
                            }

                            elements.Add(new CuiElement
                            {
                                Parent = ConfigMenu,
                                Components = { new CuiRawImageComponent { Png = ImageSongButton },
                                new CuiRectTransformComponent { AnchorMin = up, AnchorMax = down } }
                            });

                            var ConfigOptionGlobal = elements.Add(new CuiButton
                            {
                                Button = { Command = $"hardestSetSound {name} {theNet} global", Close = ConfigMenu2, Color = "255 255 255 0" },
                                RectTransform = { AnchorMin = up, AnchorMax = down, },
                                Text = { Text = "<b>" + name + "</b>", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                            }, ConfigMenu);

                            if (permission.UserHasPermission(player.UserIDString, permAdmin))
                            {
                                elements.Add(new CuiElement
                                {
                                    Parent = ConfigMenu,
                                    Components = { new CuiRawImageComponent { Png = ImageRemoveButton },
                                    new CuiRectTransformComponent { AnchorMin = buttonup2, AnchorMax = buttondown2 } }
                                });

                                var ConfigOption = elements.Add(new CuiButton
                                {
                                    Button = { Command = $"hardestRemoveSound {name} global {theNet}", Close = ConfigMenu2, Color = "255 255 255 0" },
                                    RectTransform = { AnchorMin = buttonup2, AnchorMax = buttondown2 },
                                    Text = { Text = "", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                                }, ConfigMenu);
                            }
                        }
                }
                else
                {
                    elements.Add(new CuiLabel
                    {
                        Text = { Text = $"<B>{lang.GetMessage("AdminPicks", this)}</B>", FontSize = 15, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                        RectTransform = { AnchorMin = "0 0.93", AnchorMax = "0.2 1" }
                    }, ConfigMenu);

                    if (permission.UserHasPermission(player.UserIDString, permAdmin))
                    {
                        var newURL = elements.Add(new CuiButton
                        {
                            Button = { Command = $"AddPlayerSoundUrls {theNet} global", Color = "255 255 255 0" },
                            RectTransform = { AnchorMin = "0.65 0.93", AnchorMax = ".84 1" },
                            Text = { Text = $"<B>{lang.GetMessage("AddUrlNew", this)}</B>", FontSize = 15, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                        }, ConfigMenu);
                    }

                    if (GlobalFile != null)
                        foreach (var config in GlobalFile.SavedURLS.ToList())
                        {
                            line++;
                            List<int> MoveRight = new List<int>() { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29, 31, 33, 35, 37, 39, 41 };
                            string name = config.Key;
                            string up = "0.05 " + movementUP.ToString();
                            string down = "0.43 " + movementDown.ToString();
                            string buttonup = "0.43 " + buttonUP.ToString();
                            string buttondown = "0.46 " + buttonDown.ToString();

                            if (!MoveRight.Contains(line))
                            {
                                up = "0.55 " + movementUP.ToString();
                                down = "0.91 " + movementDown.ToString();
                                movementUP = movementUP - 0.050;
                                movementDown = movementDown - 0.050;

                                buttonup = "0.91 " + buttonUP.ToString();
                                buttondown = "0.94 " + buttonDown.ToString();
                                buttonUP = buttonUP - 0.050;
                                buttonDown = buttonDown - 0.050;
                            }

                            elements.Add(new CuiElement
                            {
                                Parent = ConfigMenu,
                                Components = { new CuiRawImageComponent { Png = ImageSongButton },
                                new CuiRectTransformComponent { AnchorMin = up, AnchorMax = down } }
                            });

                            var ConfigOptionGlobal = elements.Add(new CuiButton
                            {
                                Button = { Command = $"hardestSetSound {name} {theNet} global", Close = ConfigMenu2, Color = "255 255 255 0" },
                                RectTransform = { AnchorMin = up, AnchorMax = down, },
                                Text = { Text = "<b>" + name + "</b>", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                            }, ConfigMenu);

                            if (permission.UserHasPermission(player.UserIDString, permAdmin))
                            {
                                elements.Add(new CuiElement
                                {
                                    Parent = ConfigMenu,
                                    Components = { new CuiRawImageComponent { Png = ImageRemoveButton },
                                    new CuiRectTransformComponent { AnchorMin = buttonup, AnchorMax = buttondown } }
                                });

                                var ConfigOption = elements.Add(new CuiButton
                                {
                                    Button = { Command = $"hardestRemoveSound {name} global {theNet}", Close = ConfigMenu2, Color = "255 255 255 0" },
                                    RectTransform = { AnchorMin = buttonup, AnchorMax = buttondown },
                                    Text = { Text = "", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                                }, ConfigMenu);
                            }
                        }
                }
                CuiHelper.AddUi(player, elements);
            }
        }

        private void theUIAddUrl(BasePlayer player, ulong theNet, TheData SaveFilePlayer, TheData GlobalFile, bool isGlobal = false)
        {
            CuiHelper.DestroyUi(player, "theUIConfig");
            string informationsTxt = lang.GetMessage("AddInfo", this);
            Color backgroundColor;
            Vector3 buttonPosition = new Vector3(-107.3504f, 12.1489f, -107.7641f);
            ColorExtensions.TryParseHexString("#404040", out backgroundColor);

            string bImage = "";
            string ImageAdminPicks = "";
            string ImageHeader = "";
            string ImageSongButton = "";
            string ImageRemoveButton = "";

            if (!string.IsNullOrEmpty(configData.settings.BackgroundImage))
                bImage = ImageLibrary?.Call<string>("GetImage", configData.settings.BackgroundImage);

            if (!string.IsNullOrEmpty(configData.settings.ImageAdminPicks))
                ImageAdminPicks = ImageLibrary?.Call<string>("GetImage", configData.settings.ImageAdminPicks);

            if (!string.IsNullOrEmpty(configData.settings.ImageHeader))
                ImageHeader = ImageLibrary?.Call<string>("GetImage", configData.settings.ImageHeader);

            if (!string.IsNullOrEmpty(configData.settings.ImageSongButton))
                ImageSongButton = ImageLibrary?.Call<string>("GetImage", configData.settings.ImageSongButton);

            if (!string.IsNullOrEmpty(configData.settings.ImageRemoveButton))
                ImageRemoveButton = ImageLibrary?.Call<string>("GetImage", configData.settings.ImageRemoveButton);
            if (buttonPosition != null) { }
            var elements = new CuiElementContainer();
            {
                var ConfigMenu2 = elements.Add(new CuiPanel
                {
                    Image = { Color = HexToColor("#000000")  },
                    RectTransform = { AnchorMin = "0.20 0.2", AnchorMax = "0.80 0.90" },
                    CursorEnabled = true
                }, "Overlay", "theUIConfig");

                var ConfigMenu = elements.Add(new CuiPanel
                {
                    Image = { Color = ColorExtensions.ToRustFormatString(backgroundColor) },
                    RectTransform = { AnchorMin = "0.0005 0.0005", AnchorMax = "0.998 0.997" },
                    CursorEnabled = true
                }, "theUIConfig", "parentGray");

                elements.Add(new CuiElement
                {
                    Parent = ConfigMenu,
                    Components = { new CuiRawImageComponent { Png = bImage }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } }
                });

                elements.Add(new CuiElement
                {
                    Parent = ConfigMenu,
                    Components = { new CuiRawImageComponent { Png = ImageHeader },
                    new CuiRectTransformComponent { AnchorMin = "0 0.93", AnchorMax = "1 1" }
                }
                });

                elements.Add(new CuiButton
                {
                    Button = { Close = ConfigMenu2,Color = "255 255 255 0" },
                    RectTransform = { AnchorMin = "0.85 0.93", AnchorMax = ".99 1" },
                    Text = { Text = $"<B>{lang.GetMessage("Exit", this)}</B>", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                }, ConfigMenu);

                elements.Add(new CuiButton
                {
                    Button = { Color = HexToColor("#383838") },
                    RectTransform = { AnchorMin = "0.18 0.205", AnchorMax = "0.30 0.245" },
                    Text = { Text = $"{lang.GetMessage("SongHttp", this)}", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                }, ConfigMenu);

                elements.Add(new CuiLabel
                {
                    Text = { Text = informationsTxt, FontSize = 20, Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 0.9" }
                }, ConfigMenu);

                elements.Add(new CuiButton
                {
                    Button = { Color = HexToColor("#383838") },
                    RectTransform = { AnchorMin = "0.18 0.345", AnchorMax = "0.30 0.385" },
                    Text = { Text = $"{lang.GetMessage("SongName", this)}", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                }, ConfigMenu);

                if (!isGlobal)
                {
                    elements.Add(new CuiButton
                    {
                        Button = { Close = ConfigMenu2, Color = HexToColor("#383838"), Command = $"AddUrlSoundBox {isGlobal} {theNet}" },
                        RectTransform = { AnchorMin = "0.85 0.03", AnchorMax = ".99 0.07" },
                        Text = { Text = $"{lang.GetMessage("AddUrl", this)}", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                    }, ConfigMenu);

                    if (permission.UserHasPermission(player.UserIDString, permAdmin))
                    {
                        elements.Add(new CuiButton
                        {
                            Button = { Close = ConfigMenu2, Color = HexToColor("#383838"), Command = $"AddUrlSoundBox True {theNet}" },
                            RectTransform = { AnchorMin = "0.68 0.03", AnchorMax = ".83 0.07" },
                            Text = { Text = $"{lang.GetMessage("AddUrlGlobal", this)}", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                        }, ConfigMenu);
                    }
                }
                else
                {
                    elements.Add(new CuiButton
                    {
                        Button = { Close = ConfigMenu2, Color = HexToColor("#383838"), Command = $"AddUrlSoundBox {isGlobal} {theNet}" },
                        RectTransform = { AnchorMin = "0.85 0.03", AnchorMax = ".99 0.07" },
                        Text = { Text = $"{lang.GetMessage("AddUrlGlobal", this)}", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                    }, ConfigMenu);
                }

                var mainName2 = elements.Add(new CuiPanel
                {
                    Image = { Color = HexToColor("#FFFFFF") },
                    RectTransform = { AnchorMin = "0.18 0.16", AnchorMax = "0.82 0.20" },
                    CursorEnabled = true
                }, "parentGray", "input");

                elements.Add(new CuiElement
                {
                    Name = "TestNameInput",
                    Parent = "input",
                    Components =
                    {
                    new CuiInputFieldComponent { NeedsKeyboard = true, Text = "0", CharsLimit = 90, Color = "0 0 0 1", IsPassword = false, Command = "uiinput.inputtext", Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleLeft },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });

                elements.Add(new CuiPanel
                {
                    Image = { Color = HexToColor("#FFFFFF") },
                    RectTransform = { AnchorMin = "0.18 0.30", AnchorMax = "0.42 0.34" },
                    CursorEnabled = true
                }, "parentGray", "input2");

                elements.Add(new CuiElement
                {
                    Name = "TestNameInput2",
                    Parent = "input2",
                    Components =
                    {
                        new CuiInputFieldComponent { NeedsKeyboard = true, Text = "0", CharsLimit = 100, Color = "0 0 0 1", IsPassword = false, Command = "uiinput.inputtext2", Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleLeft },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });

                CuiHelper.AddUi(player, elements);
            }
        }

        #region UiCommands
        private Dictionary<string, string> inputtext = new Dictionary<string, string>();
        private Dictionary<string, string> inputtext2 = new Dictionary<string, string>();
        [ConsoleCommand("uiinput.inputtext")]
        private void InputTextCallback(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (arg.Args.Length <= 0)
            {
                if (inputtext.ContainsKey(player.UserIDString))
                    inputtext.Remove(player.UserIDString);
                return;
            }
            if (inputtext.ContainsKey(player.UserIDString))
            {
                inputtext[player.UserIDString] = arg.Args[0];
            }
            else
            {
                inputtext.Add(player.UserIDString, arg.Args[0]);
            }
        }

        [ConsoleCommand("uiinput.inputtext2")]
        private void InputTextCallback2(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (arg.Args.Length <= 0)
            {
                if (inputtext2.ContainsKey(player.UserIDString))
                    inputtext2.Remove(player.UserIDString);
                return;
            }
            if (inputtext2.ContainsKey(player.UserIDString))
            {
                inputtext2[player.UserIDString] = arg.Args[0];
            }
            else
            {
                inputtext2.Add(player.UserIDString, arg.Args[0]);
            }
        }

        [ConsoleCommand("AddPlayerSoundUrls")]
        private void UiOpenConfigAdd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            ulong theID = default(ulong);
            BaseNetworkable speaker = null;
            string global = arg.Args[1];
            bool isGlobal = global == "global";
            
            if (!ulong.TryParse(arg.Args[0], out theID))
            {
                SendReply(player, lang.GetMessage("SomthingWentWrong", this));
                return;
            }
            ulong theUID = default(ulong);

            speaker = BaseNetworkable.serverEntities.Find(new NetworkableId(theID));

            if (speaker == null)
            {
                PrintWarning($"Could not find speaker with net id {theID.ToString()}");
                return;
            }

            TheData SaveFilePlayer = getPlayerData(player);
            TheData SaveFilePlayerG = getPlayerData(player, true);

            if (SaveFilePlayer == null)
            {
                Puts("could not load save player data uiMenu");
                return;
            }
            if (!isGlobal && SaveFilePlayer.SavedURLS.Count >= configData.settings2.TotalAllowedPlayerUrls)
            {
                SendReply(player, lang.GetMessage("maxUsage", this, player.UserIDString), configData.settings2.TotalAllowedPlayerUrls);
                return;
            }
            CuiHelper.DestroyUi(player, "theUIConfig");
            theUIAddUrl(player, theID, SaveFilePlayer, SaveFilePlayerG, isGlobal);
        }

        [ConsoleCommand("AddUrlSoundBox")]
        private void UiAddConfigSpeaker(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            string global = arg.Args[0];
            ulong theID = default(ulong);
            BaseNetworkable speaker = null;
            bool isGlobal = global == "True";
            if (!ulong.TryParse(arg.Args[1], out theID))
            {
                SendReply(player, lang.GetMessage("SomthingWentWrong", this));
                return;
            }

            speaker = BaseNetworkable.serverEntities.Find(new NetworkableId(theID));

            if (speaker == null)
            {
                PrintWarning($"Could not find speaker with net id {theID.ToString()}");
                return;
            }

            if (!inputtext.ContainsKey(player.UserIDString) || !inputtext2.ContainsKey(player.UserIDString))
            {
                PrintWarning("could not find input text");
                return;
            }

            saveUrl(player, CombineWords(inputtext2[player.UserIDString]), inputtext[player.UserIDString], isGlobal);

            TheData SaveFilePlayer = getPlayerData(player);
            TheData SaveFilePlayerG = getPlayerData(player, true);

            if (SaveFilePlayer == null)
            {
                PrintWarning("could not load save player data uiMenu");
                return;
            }

            CuiHelper.DestroyUi(player, "theUIConfig");
            theUIConfigMenu(player, theID, SaveFilePlayer, SaveFilePlayerG);
        }

        [ConsoleCommand("hardestSetSound")]
        private void UiSetupConfigSpeaker(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            string TheConfig = arg.Args[0];
            string global = arg.Args[2];
            ulong theID = default(ulong);
            BaseNetworkable speaker = null;
            TheData SaveFilePlayer = null;

            if (!ulong.TryParse(arg.Args[1], out theID))
            {
                SendReply(player, lang.GetMessage("SomthingWentWrong", this));
                return;
            }

            speaker = BaseNetworkable.serverEntities.Find(new NetworkableId(theID));

            if (speaker == null)
            {
                PrintWarning($"Could not find speaker with net id {theID.ToString()}");
                return;
            }

            if (global == "global")
                SaveFilePlayer = getPlayerData(player, true);
            else SaveFilePlayer = getPlayerData(player);
 
            if (SaveFilePlayer == null)
            {
                PrintWarning("could not load save data UiSetupConfigSpeaker");
                return;
            }

            DeployableBoomBox deployableBoomBox = speaker?.GetComponent<DeployableBoomBox>();
            HeldBoomBox boomBox = speaker?.GetComponent<HeldBoomBox>();
            if (deployableBoomBox != null)
            {
                ChangeUrl(deployableBoomBox.BoxController, SaveFilePlayer.SavedURLS[TheConfig], player);
            }
            else if (boomBox != null)
            {
                ChangeUrl(boomBox.BoxController, SaveFilePlayer.SavedURLS[TheConfig], player);
            }
            else
            {
                PrintWarning("deployableBoomBox is null");
            }

            CuiHelper.DestroyUi(player, "theUIConfig");
        }

        [ConsoleCommand("hardestRemoveSound")]
        private void UiRemoveConfigSpeaker(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            string TheConfig = arg.Args[0];
            string global = arg.Args[1];
            ulong theID = default(ulong);

            if (!ulong.TryParse(arg.Args[2], out theID))
            {
                SendReply(player, lang.GetMessage("SomthingWentWrong", this));
                return;
            }

            if (global != "global")
            {
                removeUrl(player, TheConfig);              
            }
            else
            {
                removeUrl(player, TheConfig, true);                 
            }
            TheData SaveFilePlayer = getPlayerData(player);
            TheData SaveFilePlayerG = getPlayerData(player, true);
            if (SaveFilePlayer == null || SaveFilePlayerG == null)
            {
                PrintWarning("could not load save player data remove");
                CuiHelper.DestroyUi(player, "theUIConfig");
                return;
            }

            CuiHelper.DestroyUi(player, "theUIConfig");
            theUIConfigMenu(player, theID, SaveFilePlayer, SaveFilePlayerG);
        }

        #endregion UiCommands

        #region UiColorHelpers
        private static string HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');

            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        public static bool TryParseHexString(string hexString, out Color color)
        {
            try
            {
                color = FromHexString(hexString);
                return true;
            }
            catch
            {
                color = Color.white;
                return false;
            }
        }
        private static Color FromHexString(string hexString)
        {
            if (string.IsNullOrEmpty(hexString))
            {
                throw new InvalidOperationException("Cannot convert an empty/null string.");
            }
            var trimChars = new[] { '#' };
            var str = hexString.Trim(trimChars);
            switch (str.Length)
            {
                case 3:
                    {
                        var chArray2 = new[] { str[0], str[0], str[1], str[1], str[2], str[2], 'F', 'F' };
                        str = new string(chArray2);
                        break;
                    }
                case 4:
                    {
                        var chArray3 = new[] { str[0], str[0], str[1], str[1], str[2], str[2], str[3], str[3] };
                        str = new string(chArray3);
                        break;
                    }
                default:
                    if (str.Length < 6)
                    {
                        str = str.PadRight(6, '0');
                    }
                    if (str.Length < 8)
                    {
                        str = str.PadRight(8, 'F');
                    }
                    break;
            }
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            return new Color32(r, g, b, a);
        }
        public static class ColorExtensions
        {
            public static string ToRustFormatString(Color color)
            {
                return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
            }

            public static string ToHexStringRGB(Color col)
            {
                Color32 color = col;
                return string.Format("{0}{1}{2}", color.r, color.g, color.b);
            }

            public static string ToHexStringRGBA(Color col)
            {
                Color32 color = col;
                return string.Format("{0}{1}{2}{3}", color.r, color.g, color.b, color.a);
            }

            public static bool TryParseHexString(string hexString, out Color color)
            {
                try
                {
                    color = FromHexString(hexString);
                    return true;
                }
                catch
                {
                    color = Color.white;
                    return false;
                }
            }
        }
        #endregion UiColorHelpers

        #endregion UI

        #region DefaultMessages

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["active"] = "BoomBox controler active!",
                ["deactive"] = "BoomBox controler deactivated!",
                ["BadUrlKey"] = "The Url must contain {0}",
                ["BlockedUrl"] = "This Url can not contain {0}",
                ["exists"] = "This save name already exists.",
                ["NoBoomBox"] = "You are not holding a boombox.",
                ["maxUsage"] = "You can not save over {0} urls.",
                ["PersonalSongs"] = "<color=#FFFF00>PERSONAL SONGS</color>",
                ["AdminPicks"] = "<color=#FFFF00>ADMIN PICKS</color>",
                ["AddUrlNew"] = "ADD NEW URL",
                ["AddUrl"] = "ADD URL",
                ["AddUrlGlobal"] = "ADD GLOBAL",
                ["Exit"] = "EXIT MENU",
                ["SongHttp"] = "Song Http URL",
                ["SongName"] = "Song Name",
                ["AddInfo"] = "<color=#FFFF00>Please enter a name to save this url as in the box below. \n In the url box all url's must start with http:// or https://</color>",
            }, this);
        }

        #endregion DefaultMessages
    }
}