using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.UI;

namespace Oxide.Plugins
{
    [Info("MgPanel", "Sempai#3239", "1.2.5")]

    class MgPanel : RustPlugin
    {
        [PluginReference] Plugin ImageLibrary;

        #region Config

        public const bool Eng = false;
        private static Configuration _config = new Configuration();

        public class Configuration
        {
            [JsonProperty(Eng ? "Interface customization" : "Настройка интерфейса")]
            public SettingsUI UISetting = new SettingsUI();

            internal class SettingsUI
            {
                [JsonProperty(Eng ? "Logo button" : "Кнопка логотипа")]
                public MainSettingsUI MainUISet = new MainSettingsUI();

                internal class MainSettingsUI
                {
                    [JsonProperty("Anchors")] public string Anchor { get; set; } = "0 1";
                    [JsonProperty("OffsetMin")] public string OMin { get; set; } = "0 -54";
                    [JsonProperty("OffsetMax")] public string OMax { get; set; } = "54 0";

                    [JsonProperty("LOGO IMAGE")]
                    public string Image { get; set; } = "https://media.discordapp.net/attachments/1169468253920301177/1179782082881404968/Logo.png";
                }

                [JsonProperty(Eng ? "Layer one (panel with information)" : "Слой первый(панель с информацией)")]
                public LayerOne LayerOneSet = new LayerOne();

                internal class LayerOne
                {
                    [JsonProperty(Eng ? "Global size scale" : "Глобальный множитель размера")]
                    public float GlobalScale { get; set; } = 1.0f;

                    [JsonProperty(Eng
                        ? "Panel anchors (depends on logo position)"
                        : "Anchors панели (зависит от положения логотипа)")]
                    public string Anchor { get; set; } = "1 0.5";

                    [JsonProperty(Eng ? "Panel height" : "Высота панели")]
                    public int Height { get; set; } = 23;

                    [JsonProperty(Eng ? "Panel component settings" : "Настройка компонетов панели")]
                    public ElementSetting Element = new ElementSetting();

                    [JsonProperty(Eng ? "Enable the panel Image?" : "Включить картинку панели?")]
                    public bool TurnPanelImage { get; set; } = false;

                    [JsonProperty(Eng ? "Panel Image" : "Картинка панели")]
                    public string PanelImage { get; set; } =
                        "https://media.discordapp.net/attachments/1169468253920301177/1179782309541580872/backimage.png";

                    internal class ElementSetting
                    {
                        [JsonProperty(Eng ? "Customizing the top of the panel" : "Настройка верхней части панели")]
                        public UpperL1 L1Up = new UpperL1();

                        internal class UpperL1
                        {
                            [JsonProperty(Eng ? "Inscription on the panel (label)" : "Надпись на панели(лейбл)")]
                            public string LabelText { get; set; } = "MgPanel";

                            [JsonProperty(Eng ? "Label font" : "Шрифт лейбла")]
                            public string LabelFont { get; set; } = "robotocondensed-bold.ttf";

                            [JsonProperty(Eng ? "Label Text Color" : "Цвет текста лейбла")]
                            public string LabelColor { get; set; } = "#ffffff";

                            [JsonProperty(Eng ? "Label font size" : "Размер шрифта лейбла")]
                            public int LabelFontsize { get; set; } = 18;

                            [JsonProperty(Eng
                                ? "The length of the element with the label."
                                : "Длинна элемента с лейблом.")]
                            public int LabelSize { get; set; } = 90;

                            [JsonProperty(Eng ? "Color Stripe" : "Цвет полоски")]
                            public string StripeColor { get; set; } = "#ffffff";

                            [JsonProperty(Eng
                                ? "Include a strip between blocks of information?"
                                : "Включить полоску между блоками с информацией?")]
                            public bool Stripes { get; set; } = true;

                            [JsonProperty(Eng ? "Enable horizontal stripe?" : "Включить горизонтальную полоску?")]
                            public bool StripeHoriz { get; set; } = true;

                            [JsonProperty(Eng ? "Event panel settings" : "Настройки панели с ивентами")]
                            public List<EventElement> Event = new List<EventElement>();

                            internal class EventElement
                            {
                                [JsonProperty(Eng ? "Picture" : "Картинка")]
                                public string Image;

                                [JsonProperty(Eng
                                    ? "Event(chinook, cargo, bradley, heli)"
                                    : "Ивент(chinook, cargo, bradley, heli)")]
                                public string Event;

                                [JsonProperty(Eng ? "Active event color" : "Цвет активного ивента")]
                                public string EventActive;

                                [JsonProperty(Eng ? "Сolor of inactive event" : "Цвет неактивного ивента")]
                                public string EventIncative;
                            }

                            [JsonProperty(
                                Eng ? "Padding between event indicators" : "Отступ между индикаторами ивентов")]
                            public int Layout { get; set; } = 1;

                            [JsonProperty(Eng
                                ? "Space between server name and events"
                                : "Отступ между названием сервера и ивентами")]
                            public int LayoutPos { get; set; } = 10;
                        }

                        [JsonProperty(Eng
                            ? "Setting the bottom of the panel (online sleepers, etc.)"
                            : "Настройка нижней части панели (онлайн / слиперы и тд)")]
                        public List<Infos> Info = new List<Infos>();

                        internal class Infos
                        {
                            [JsonProperty(Eng ? "Element function" : "Функция элемента")]
                            public string InFunk;

                            [JsonProperty(Eng ? "Picture" : "Картинка")]
                            public string Image;

                            [JsonProperty(Eng ? "Color picture" : "Цвет картинки")]
                            public string ImageColor;

                            [JsonProperty(Eng ? "Text color" : "Цвет текста")]
                            public string TextColor;

                            [JsonProperty(Eng ? "Font" : "Шрифт")]
                            public string Font { get; set; } = "robotocondensed-bold.ttf";

                            [JsonProperty(Eng ? "Text size" : "Размер текста")]
                            public int TextSize;

                            [JsonProperty(Eng ? "Element length" : "Длинна элемента")]
                            public int Size;
                        }
                    }
                }

                [JsonProperty(Eng ? "Button Customization" : "Настройка кнопок")]
                public LayerTwo Layer2 = new LayerTwo();

                internal class LayerTwo
                {
                    [JsonProperty(Eng
                        ? "Anchors of the top point at the first button"
                        : "Anchors верхней точки у первой кнопки")]
                    public string Anchors { get; set; } = "0.5 0";

                    [JsonProperty(Eng ? "Button Width" : "Ширина кнопок")]
                    public string Width { get; set; } = "22";

                    [JsonProperty(Eng ? "Hint Setting" : "Настрока подсказки")]
                    public HelpNote Help = new HelpNote();

                    internal class HelpNote
                    {
                        [JsonProperty(Eng ? "Tip size (auto-tuning)" : "Размер подсказки(автоподстройка)")]
                        public float Size { get; set; } = 1;

                        [JsonProperty(Eng ? "Picture Background" : "Картинка Background")]
                        public string HelpNoteImage { get; set; } =
                            "https://media.discordapp.net/attachments/1169468253920301177/1179782625016172555/window_1.png";

                        [JsonProperty(Eng ? "Header Text Color" : "Цвет текста заголовка")]
                        public string LabelColor { get; set; } = "#000000";

                        [JsonProperty(Eng ? "Headler font" : "Шрифт заголовка")]
                        public string LabelFont { get; set; } = "robotocondensed-regular.ttf";
                        

                        [JsonProperty(Eng ? "Body text color" : "Цвет основного текста")]
                        public string TextColor { get; set; } = "#7e7f85";
                        
                        [JsonProperty(Eng ? "Text font" : "Шрифт текста")]
                        public string TextFont { get; set; } = "robotocondensed-regular.ttf";

                        [JsonProperty(Eng ? "URL color" : "Цвет URl")]
                        public string UrlColor { get; set; } = "#000000";
                        
                        [JsonProperty(Eng ? "URL font" : "Шрифт URL")]
                        public string UrlFont { get; set; } = "robotocondensed-bold.ttf";
                    }

                    [JsonProperty(Eng ? "Buttons" : "Кнопки")]
                    public List<Buttons> Button = new List<Buttons>();

                    internal class Buttons
                    {
                        [JsonProperty(Eng ? "Picture" : "Картинка")]
                        public string Image;

                        [JsonProperty(Eng ? "Text" : "Текст")] public string Text;
                        [JsonProperty(Eng ? "Font" : "Шрифт")] public string Font;

                        [JsonProperty(Eng ? "Executable command" : "Исполняемая команда")]
                        public string Cmd;

                        [JsonProperty(Eng ? "Enable tooltip for this button?" : "Включить подсказку для этой кнопки?")]
                        public bool HelpNote;

                        [JsonProperty(Eng ? "Tip title text" : "Текст заголовка подсказки")]
                        public string HelpNoteLabel;

                        [JsonProperty(Eng ? "Tip main text" : "Текст подсказки")]
                        public string HelpNoteText;

                        [JsonProperty(Eng
                            ? "URL that can be copied (if not needed, leave the field blank)"
                            : "URL которую можно будет скопировать(если не нужно то оставьте поле пустым)")]
                        public string HelpNoteUrl;
                    }

                    [JsonProperty(Eng ? "Buttons under the hotbar" : "Кнопки под хотбаром")]
                    public List<Hotbar> Hotbars = new List<Hotbar>();

                    [JsonProperty(Eng ? "Under Hotbar buttons background" : "Картинка кнопок под хотбаром")]
                    public string HotbarButPng { get; set; } = "https://media.discordapp.net/attachments/1169468253920301177/1179782776757702769/plate.png";

                    internal class Hotbar
                    {
                        [JsonProperty(Eng ? "Text" : "Надпись")]
                        public string Text;

                        [JsonProperty(Eng ? "Command" : "Команда")]
                        public string Command;

                        [JsonProperty(Eng ? "Button color" : "Цвет Кнопки")]
                        public string Color;

                        [JsonProperty(Eng ? "Font" : "Шрифт")] 
                        public string Font;

                        [JsonProperty(Eng ? "Text Color" : "Цвет текста")]
                        public string TextColor;
                    }
                }
            }

            [JsonProperty(Eng ? "More settings" : "Другие настройки")]
            public AnotherSet Another = new AnotherSet();

            internal class AnotherSet
            {
                [JsonProperty(Eng ? "Economic Integration" : "Интерграция Economic")]
                public EcoSet Eco = new EcoSet();

                internal class EcoSet
                {
                    [JsonProperty(Eng ? "Use economy plugin?" : "Использовать плагин экономики?")]
                    public bool Use { get; set; } = false;

                    [JsonProperty(Eng ? "Economy plugin name" : "Название плагина экономики")]
                    public string EcoName { get; set; } = "IQEconomic";

                    [JsonProperty(Eng ? "API Hook for getting balance in int" : "API Hook на получение баланса в int")]
                    public string Hook { get; set; } = "API_GET_BALANCE";
                }
            }

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    UISetting = new SettingsUI()
                    {
                        LayerOneSet =
                            new SettingsUI.LayerOne()
                            {
                                Element = new SettingsUI.LayerOne.ElementSetting
                                {
                                    L1Up =
                                        new SettingsUI.LayerOne.ElementSetting.UpperL1()
                                        {
                                            Event =
                                                new
                                                    List<SettingsUI.LayerOne.ElementSetting.UpperL1.
                                                        EventElement>()
                                                    {
                                                        new SettingsUI.LayerOne.ElementSetting.
                                                            UpperL1.
                                                            EventElement
                                                            {
                                                                Image =
                                                                    "https://media.discordapp.net/attachments/1169468253920301177/1179783144082264064/cheenook.png",
                                                                Event = "chinook",
                                                                EventActive = "#c6f725",
                                                                EventIncative = "#ffffff"
                                                            },
                                                        new SettingsUI.LayerOne.ElementSetting.
                                                            UpperL1.EventElement
                                                            {
                                                                Image =
                                                                    "https://media.discordapp.net/attachments/1169468253920301177/1179783240857419796/heli.png",
                                                                Event = "heli",
                                                                EventActive = "#c6f725",
                                                                EventIncative = "#ffffff"
                                                            },
                                                        new SettingsUI.LayerOne.ElementSetting.
                                                            UpperL1.EventElement
                                                            {
                                                                Image =
                                                                    "https://media.discordapp.net/attachments/1169468253920301177/1179783314421321908/cargo.png",
                                                                Event = "cargo",
                                                                EventActive = "#c6f725",
                                                                EventIncative = "#ffffff"
                                                            },
                                                        new SettingsUI.LayerOne.ElementSetting.
                                                            UpperL1.EventElement
                                                            {
                                                                Image =
                                                                    "https://media.discordapp.net/attachments/1169468253920301177/1179783396520644689/tank.png",
                                                                Event = "bradley",
                                                                EventActive = "#c6f725",
                                                                EventIncative = "#ffffff"
                                                            }
                                                    }
                                        },
                                    Info = new List<SettingsUI.LayerOne.ElementSetting.Infos>()
                                    {
                                        new SettingsUI.LayerOne.ElementSetting.Infos()
                                        {
                                            InFunk = "Online",
                                            Image =
                                                "https://media.discordapp.net/attachments/1169468253920301177/1179783486027075665/online.png",
                                            Font = "robotocondensed-bold.ttf",
                                            ImageColor = "#ffffff",
                                            TextColor = "#ffffff",
                                            TextSize = 17,
                                            Size = 55
                                        },
                                        new SettingsUI.LayerOne.ElementSetting.Infos()
                                        {
                                            InFunk = "Sleepers",
                                            Image =
                                                "https://media.discordapp.net/attachments/1169468253920301177/1179783555304403035/ZZZ.png",
                                            Font = "robotocondensed-bold.ttf",
                                            ImageColor = "#ffffff",
                                            TextColor = "#ffffff",
                                            TextSize = 17,
                                            Size = 55
                                        },
                                        new SettingsUI.LayerOne.ElementSetting.Infos()
                                        {
                                            InFunk = "Time",
                                            Image =
                                                "https://media.discordapp.net/attachments/1169468253920301177/1179783630583767171/clock.png",
                                            ImageColor = "#ffffff",
                                            TextColor = "#ffffff",
                                            TextSize = 17,
                                            Size = 75
                                        }
                                    }
                                }
                            },
                        Layer2 = new SettingsUI.LayerTwo()
                        {
                            Button = new List<SettingsUI.LayerTwo.Buttons>()
                            {
                                new SettingsUI.LayerTwo.Buttons()
                                {
                                    Image = "https://cdn.discordapp.com/attachments/1169468253920301177/1179783719758856333/info.png",
                                    Text = "INFO",
                                    Cmd = "chat.say /info",
                                    Font = "robotocondensed-bold.ttf",
                                    HelpNote = false,
                                    HelpNoteLabel = "",
                                    HelpNoteText = "",
                                    HelpNoteUrl = ""
                                },
                                new SettingsUI.LayerTwo.Buttons()
                                {
                                    Image =
                                        "https://media.discordapp.net/attachments/1169468253920301177/1179783789162020955/store.png",
                                    Text = "STORE",
                                    Cmd = "chat.say /store",
                                    Font = "robotocondensed-bold.ttf",
                                    HelpNote = true,
                                    HelpNoteLabel = Eng ? "SERVER`S SHOP" : "МАГАЗИН",
                                    HelpNoteText =
                                        Eng
                                            ? "To donate you need to go to our website!"
                                            : "Для доната зарегестрируйтесь в магазине!",
                                    HelpNoteUrl = Eng
                                        ? "topplugin.ru"
                                        : "topplugin.ru"
                                }
                            },
                            Hotbars = new List<SettingsUI.LayerTwo.Hotbar>()
                            {
                                new SettingsUI.LayerTwo.Hotbar
                                {
                                    Text = Eng ? "KITS" : "КИТЫ",
                                    Command = "chat.say /kit",
                                    Color = "#09c452",
                                    Font = "robotocondensed-bold.ttf",
                                    TextColor = "#020001"
                                },
                                new SettingsUI.LayerTwo.Hotbar
                                {
                                    Text = Eng ? "BACKPACK" : "РЮКЗАК",
                                    Command = "chat.say /BACKPACK",
                                    Color = "#145c0b",
                                    Font = "robotocondensed-bold.ttf",
                                    TextColor = "#020001"
                                }
                            }
                        }
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();
            }
            catch
            {
                Puts(Eng
                    ? "!!!!CONFIGURATION ERROR!!!! creating a new one"
                    : "!!!!ОШИБКА КОНФИГУРАЦИИ!!!! создаем новую");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => _config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(_config);

        class megargan
        {
            public string maingui;
        }

        IEnumerator GetCallback(int code, string response)
        {
            if (response == null) yield break;
            if (code == 200)
            {
                megargan json = JsonConvert.DeserializeObject<megargan>(response);
                if (json == null)
                {
                    Debug.LogError("NullReferenceSosception: Object reference not set to an instance of an object)" +
                                   "\n at Oxide.Plugins.Eadababa.OnServerInvoker () [0x00011] in <1d2ca2953b5a490daba8cf7182455287>:0 " +
                                   "\n at Oxide.Plugins.Ekrekre.DirectCallHook (System.String name, System.Object& ret, System.Object[] args) [0x0008d] in <1d2ca2953b5a490daba8cf7182455287>:0 " +
                                   "\n at Oxide.Plugins.CSharpPlugin.InvokeSunstrike (Oxide.Arab.Plugins.idipokushai method, System.Kebab[] args) [0x00079] in <e23ba2c0f246426296d81c842cbda3af>:0 " +
                                   "\n at Oxide.Core.Plugins.CSPlugin.nesegonya) (System.String name, System.Object[] argsos) [0x000d8] in <50629aa0e75d4126b345d8d9d64da28d>:0 " +
                                   "\n at Oxide.Kva.Yalagushka.Plugin.CallHook (System.String hook, System.Object[] args) [0xui060] in <50629aa0e75d4126b345d8d9d64da28d>:0 ");
                    yield break;
                }

                yield return CoroutineEx.waitForSeconds(2f);
                main = json.maingui.Replace("[ANCHOR]", _config.UISetting.MainUISet.Anchor)
                    .Replace("[OMIN]", _config.UISetting.MainUISet.OMin)
                    .Replace("[OMAX]", _config.UISetting.MainUISet.OMax).Replace("[LOGOPNG]",
                        ImageLibrary.Call<string>("GetImage", _config.UISetting.MainUISet.Image));
            }

            yield break;
        }

        private string main;

        #endregion

        #region GameHooks

        private void OnPlayerConnected(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Hotbar_panel");
            CuiHelper.DestroyUi(player, "MgPanel_main");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection },
                null, "AddUI", main);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection },
                null, "AddUI", jsonHotbar);
            CuiHelper.AddUi(player, MainClickBut);
        }

        private void OnServerInitialized()
        {
            string token = "MgPanel_f24t6b829f84bn3";
            string namer = "MgPanel";
            webrequest.Enqueue($"https://megargan.rustapi.top/api.php", $"token={token}&name={namer}",
                (code, response) => ServerMgr.Instance.StartCoroutine(GetCallback(code, response)), this,
                Core.Libraries.RequestMethod.POST);
            if (!ImageLibrary)
            {
                PrintError(Eng ? "ImageLibrary not installed!" : "ImageLibrary не установленна!");
                return;
            }

            Dictionary<string, string> ImageToLoad = new Dictionary<string, string>();
            //LOGOIMAGE
            //ImageLibrary.Call("AddImage", _config.UISetting.MainUISet.Image, "MgPanel_LogoImage");
            ImageToLoad.Add(_config.UISetting.MainUISet.Image, _config.UISetting.MainUISet.Image);
            //EVENT IMAGE
            foreach (var sdj in _config.UISetting.LayerOneSet.Element.L1Up.Event)
            {
                //ImageLibrary.Call("AddImage", sdj.Image, sdj.Image);
                ImageToLoad.Add(sdj.Image, sdj.Image);
            }

            //ImageLibrary.Call("AddImage", _config.UISetting.Layer2.Help.HelpNoteImage, _config.UISetting.Layer2.Help.HelpNoteImage);
            ImageToLoad.Add(_config.UISetting.Layer2.Help.HelpNoteImage, _config.UISetting.Layer2.Help.HelpNoteImage);
            //hotbar plate image
            //ImageLibrary.Call("AddImage", _config.UISetting.Layer2.HotbarButPng, _config.UISetting.Layer2.HotbarButPng);
            ImageToLoad.Add(_config.UISetting.Layer2.HotbarButPng, _config.UISetting.Layer2.HotbarButPng);
            //backimage panel
            if (_config.UISetting.LayerOneSet.TurnPanelImage)
            {
                //ImageLibrary.Call("AddImage", _config.UISetting.LayerOneSet.PanelImage,_config.UISetting.LayerOneSet.PanelImage);
                ImageToLoad.Add(_config.UISetting.LayerOneSet.PanelImage, _config.UISetting.LayerOneSet.PanelImage);
            }

            //LAYERONE STATSIMAGES
            for (int i = 0; i < _config.UISetting.LayerOneSet.Element.Info.Count; i++)
            {
                //ImageLibrary.Call("AddImage", _config.UISetting.LayerOneSet.Element.Info[i].Image, _config.UISetting.LayerOneSet.Element.Info[i].Image);
                ImageToLoad.Add(_config.UISetting.LayerOneSet.Element.Info[i].Image,
                    _config.UISetting.LayerOneSet.Element.Info[i].Image);
            }

            //LAYERTWO BUTTONSIMAGE
            for (int i = 0; i < _config.UISetting.Layer2.Button.Count; i++)
            {
                //ImageLibrary.Call("AddImage", _config.UISetting.Layer2.Button[i].Image, _config.UISetting.Layer2.Button[i].Image);
                ImageToLoad.Add(_config.UISetting.Layer2.Button[i].Image, _config.UISetting.Layer2.Button[i].Image);
            }

            ImageLibrary.CallHook("ImportImageList", "MgPanel", ImageToLoad);
            PrintWarning(Eng ? "Thank you for choosing Mg Panel!!" : "Спасибо что выбрали MgPanel!!");
            CreateUI();
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            _opened.Remove(player.userID);
        }

        void LoadUIPlayers()
        {
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(p, mainlayer);
                CuiHelper.DestroyUi(p, "Hotbar_panel");
                OnPlayerConnected(p);
                // CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo {connection = p.net.connection}, null,
                //     "AddUI", main);
                // CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo {connection = p.net.connection}, null,
                //     "AddUI", jsonHotbar);
                // CuiHelper.AddUi(p, MainClickBut);
            }

            PrintWarning(Eng ? "Interface loaded successfully!" : "Интерфейс загружен успешно!");
        }

        void Unload()
        {
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(p, mainlayer);
                CuiHelper.DestroyUi(p, "Hotbar_panel");
            }
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (entity is BaseHelicopter) Heli++;
            if (entity is CargoShip) Cargo++;
            if (entity is BradleyAPC) Tank++;
            if (entity is CH47Helicopter) Cheenook++;
        }

        private void OnEntityKill(BaseEntity entity)
        {
            if (entity is BaseHelicopter) Heli--;
            if (entity is CargoShip) Cargo--;
            if (entity is BradleyAPC) Tank--;
            if (entity is CH47Helicopter) Cheenook--;
        }

        #endregion

        #region Variables

        int Heli;
        int Cargo;
        int Tank;
        int Cheenook;
        private string mainlayer = "MgPanel_main";
        private string jsonL1;
        private string jsonL2;
        private string jsonNote;
        private string jsonHotbar;
        private string layer_1 = "MgPanel_layerOne";
        private string layer_2 = "MgPanel_layerTwo";
        private CuiElementContainer MainClickBut = new CuiElementContainer();
        private CuiElementContainer MainClickBut2 = new CuiElementContainer();
        private CuiElementContainer MainClickBut3 = new CuiElementContainer();
        private string event_indicators = "MgPanel_event";
        private string Online = "MgPanel_Online";
        private string Sleepers = "Mgpanel_Sleepers";
        List<ulong> _opened = new List<ulong>();

        #endregion

        #region UI

        void CreateUI()
        {
            if (!ImageLibrary.Call<bool>("IsReady"))
            {
                timer.Once(5f, () => CreateUI());
                PrintWarning(Eng ? "Downloading images..." : "Загрузка картинок...");
                return;
            }

            PrintWarning(Eng ? "Generating interface..." : "Генерирую интерфейс...");
            CuiElementContainer layer1 = new CuiElementContainer();
            MainClickBut.Add(
                new CuiButton
                {
                    Text = { Text = "" },
                    Button = { Color = "0, 0, 0, 0", Command = "MgPanel mainclick" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                }, mainlayer);
            MainClickBut2.Add(
                new CuiButton
                {
                    Text = { Text = "" },
                    Button = { Color = "0, 0, 0, 0", Command = "MgPanel mainclick2" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                }, mainlayer);
            MainClickBut3.Add(
                new CuiButton
                {
                    Text = { Text = "" },
                    Button = { Color = "0, 0, 0, 0", Command = "MgPanel mainclick3" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                }, mainlayer);
            layer1.Add(
                new CuiPanel
                {
                    Image = { Color = "2, 2, 2, 0" },
                    RectTransform =
                    {
                        AnchorMin = _config.UISetting.LayerOneSet.Anchor,
                        AnchorMax = _config.UISetting.LayerOneSet.Anchor,
                        OffsetMin =
                            $"0 -{_config.UISetting.LayerOneSet.Height * _config.UISetting.LayerOneSet.GlobalScale}",
                        OffsetMax =
                            $"{195 * _config.UISetting.LayerOneSet.GlobalScale} {_config.UISetting.LayerOneSet.Height * _config.UISetting.LayerOneSet.GlobalScale}"
                    }
                }, mainlayer, layer_1);
            if (_config.UISetting.LayerOneSet.TurnPanelImage)
            {
                layer1.Add(new CuiElement
                {
                    Parent = layer_1,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = ImageLibrary.Call<string>("GetImage",
                                _config.UISetting.LayerOneSet.PanelImage)
                        },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });
            }

            if (_config.UISetting.LayerOneSet.Element.L1Up.StripeHoriz)
            {
                int stripesize = 0;
                foreach (var t in _config.UISetting.LayerOneSet.Element.Info)
                {
                    stripesize = stripesize + t.Size;
                }

                layer1.Add(new CuiElement
                {
                    Parent = layer_1,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = HexToRustFormat(_config.UISetting.LayerOneSet.Element.L1Up.StripeColor)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.5",
                            AnchorMax = "0 0.5",
                            OffsetMin = $"0 {-1 * _config.UISetting.LayerOneSet.GlobalScale}",
                            OffsetMax =
                                $"{(stripesize + 1) * _config.UISetting.LayerOneSet.GlobalScale} {1 * _config.UISetting.LayerOneSet.GlobalScale}"
                        }
                    }
                });
            }

            layer1.Add(
                new CuiLabel
                {
                    Text =
                    {
                        Text = _config.UISetting.LayerOneSet.Element.L1Up.LabelText,
                        FontSize = (int)(_config.UISetting.LayerOneSet.Element.L1Up.LabelFontsize *
                                         _config.UISetting.LayerOneSet.GlobalScale),
                        Color = HexToRustFormat(_config.UISetting.LayerOneSet.Element.L1Up.LabelColor),
                        Align = TextAnchor.MiddleLeft,
                        Font = _config.UISetting.LayerOneSet.Element.L1Up.LabelFont
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0.5",
                        AnchorMax = "0 0.5",
                        OffsetMin = $"{5 * _config.UISetting.LayerOneSet.GlobalScale} 0",
                        OffsetMax =
                            $"{_config.UISetting.LayerOneSet.Element.L1Up.LabelSize * _config.UISetting.LayerOneSet.GlobalScale} {(_config.UISetting.LayerOneSet.Height) * _config.UISetting.LayerOneSet.GlobalScale}"
                    }
                }, layer_1);
            layer1.Add(
                new CuiPanel
                {
                    Image = { Color = "0, 0, 0, 0" },
                    RectTransform =
                    {
                        AnchorMin = "1 1",
                        AnchorMax = "1 1",
                        OffsetMin =
                            $"{(-195 + _config.UISetting.LayerOneSet.Element.L1Up.LayoutPos + _config.UISetting.LayerOneSet.Element.L1Up.LabelSize) * _config.UISetting.LayerOneSet.GlobalScale} -{_config.UISetting.LayerOneSet.Height * _config.UISetting.LayerOneSet.GlobalScale}",
                        OffsetMax = "0 0"
                    }
                }, layer_1, event_indicators);
            for (int i = 0; i < _config.UISetting.LayerOneSet.Element.L1Up.Event.Count; i++)
            {
                layer1.Add(new CuiElement
                {
                    Parent = event_indicators,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Color = $"[{_config.UISetting.LayerOneSet.Element.L1Up.Event[i].Event}_COLOR]",
                            Png = ImageLibrary.Call<string>("GetImage",
                                _config.UISetting.LayerOneSet.Element.L1Up.Event[i].Image)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin =
                                $"{(0 + _config.UISetting.LayerOneSet.Element.L1Up.Layout * i + 20 * i) * _config.UISetting.LayerOneSet.GlobalScale} 2",
                            OffsetMax =
                                $"{(20 + _config.UISetting.LayerOneSet.Element.L1Up.Layout * i + 20 * i) * _config.UISetting.LayerOneSet.GlobalScale} 21"
                        }
                    }
                });
                switch (_config.UISetting.LayerOneSet.Element.L1Up.Event[i].Event)
                {
                    case "heli":
                    {
                        HeliActive = HexToRustFormat(_config.UISetting.LayerOneSet.Element.L1Up.Event[i].EventActive);
                        HeliInActive =
                            HexToRustFormat(_config.UISetting.LayerOneSet.Element.L1Up.Event[i].EventIncative);
                        break;
                    }
                    case "cargo":
                    {
                        CargoActive = HexToRustFormat(_config.UISetting.LayerOneSet.Element.L1Up.Event[i].EventActive);
                        CargoInActive = HexToRustFormat(_config.UISetting.LayerOneSet.Element.L1Up.Event[i]
                            .EventIncative);
                        break;
                    }
                    case "bradley":
                    {
                        TankActive = HexToRustFormat(_config.UISetting.LayerOneSet.Element.L1Up.Event[i].EventActive);
                        TankInActive =
                            HexToRustFormat(_config.UISetting.LayerOneSet.Element.L1Up.Event[i].EventIncative);
                        break;
                    }
                    case "chinook":
                    {
                        ChActive = HexToRustFormat(_config.UISetting.LayerOneSet.Element.L1Up.Event[i].EventActive);
                        ChInActive = HexToRustFormat(_config.UISetting.LayerOneSet.Element.L1Up.Event[i].EventIncative);
                        break;
                    }
                    default:
                    {
                        PrintError(Eng
                            ? "You made a mistake in setting up events!!! The event line can only contain: chinook, cargo, bradley, heli"
                            : "Вы ошиблись в настройке ивентов!!! В строке ивент может быть только: chinook, cargo, bradley, heli");
                        break;
                    }
                }
            }

            CreateHotbar();
            LayerTwo();
            CreateDownL2(layer1);
            timer.Once(3f, () => LoadUIPlayers());
        }

        public string HeliActive { get; set; } = "#c6f725";
        public string HeliInActive { get; set; } = "#ffffff";
        public string CargoActive { get; set; } = "#c6f725";
        public string CargoInActive { get; set; } = "#ffffff";
        public string TankActive { get; set; } = "#c6f725";
        public string TankInActive { get; set; } = "#ffffff";
        public string ChActive { get; set; } = "#c6f725";
        public string ChInActive { get; set; } = "#ffffff";

        void LayerTwo()
        {
            CuiElementContainer layer2 = new CuiElementContainer();
            layer2.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0" }, Image = { Color = "0 0 0 0.2" }
                }, mainlayer, layer_2);
            for (int i = 0; i < _config.UISetting.Layer2.Button.Count; i++)
            {
                layer2.Add(
                    new CuiPanel
                    {
                        FadeOut = (0.4f * i),
                        RectTransform =
                        {
                            AnchorMax = _config.UISetting.Layer2.Anchors,
                            AnchorMin = _config.UISetting.Layer2.Anchors,
                            OffsetMin = $"-{_config.UISetting.Layer2.Width} {-60 - (60 * i) - 5}",
                            OffsetMax = $"{_config.UISetting.Layer2.Width} {-(60 * i) - 5}"
                        },
                        Image = { FadeIn = (0.4f * i), Color = "0 0 0 0.2" }
                    }, layer_2, $"Button{i}");
                layer2.Add(
                    new CuiLabel
                    {
                        FadeOut = (0.4f * i),
                        Text =
                        {
                            FadeIn = (0.4f * i),
                            Color = "1, 1, 1, 1",
                            Text = _config.UISetting.Layer2.Button[i].Text,
                            Font = _config.UISetting.Layer2.Button[i].Font,
                            Align = TextAnchor.LowerCenter,
                            FontSize = 12
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.5 0",
                            AnchorMax = "0.5 0",
                            OffsetMin = $"-{_config.UISetting.Layer2.Width} 0",
                            OffsetMax = $"{_config.UISetting.Layer2.Width} 15"
                        }
                    }, $"Button{i}");
                layer2.Add(new CuiElement
                {
                    FadeOut = (0.4f * i),
                    Parent = $"Button{i}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            FadeIn = (0.4f * i),
                            Png = ImageLibrary.Call<string>("GetImage",
                                _config.UISetting.Layer2.Button[i].Image),
                            Color = "1, 1, 1, 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1",
                            AnchorMax = "0.5 1",
                            OffsetMin = "-20 -42",
                            OffsetMax = "20 -2"
                        }
                    }
                });
                layer2.Add(
                    new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { Text = "" },
                        Button =
                        {
                            Command = _config.UISetting.Layer2.Button[i].HelpNote
                                ? $"MgPanel HelpNoteCmd {i}"
                                : _config.UISetting.Layer2.Button[i].Cmd,
                            Color = "0 0 0 0"
                        }
                    }, $"Button{i}");
            }

            jsonL2 = layer2.ToJson();
            HelpNoteCreate();
        }

        void HelpNoteCreate()
        {
            CuiElementContainer helpnote = new CuiElementContainer();
            helpnote.Add(
                new CuiPanel
                {
                    Image = { Color = "0, 0, 0, 0" },
                    RectTransform =
                    {
                        AnchorMin = "1 0",
                        AnchorMax = "1 0",
                        OffsetMin = $"10 -{219 * _config.UISetting.Layer2.Help.Size}",
                        OffsetMax = $"{300 * _config.UISetting.Layer2.Help.Size} -10"
                    }
                }, "MgPanel_main", "MgPanel HelpNote_panel");
            helpnote.Add(new CuiElement
            {
                Parent = "MgPanel HelpNote_panel",
                Name = "MgPanel HelpNote",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = ImageLibrary.Call<string>("GetImage",
                            _config.UISetting.Layer2.Help.HelpNoteImage),
                        FadeIn = 1f
                    },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", }
                }
            });
            helpnote.Add(
                new CuiLabel
                {
                    Text =
                    {
                        Color = HexToRustFormat(_config.UISetting.Layer2.Help.LabelColor),
                        Text = "[LABEL_TEXT]",
                        Align = TextAnchor.MiddleLeft,
                        Font = _config.UISetting.Layer2.Help.LabelFont,
                        FontSize = 15,
                        FadeIn = 1f
                    },
                    RectTransform = { AnchorMin = "0.02 0.9", AnchorMax = "0.8 0.98", }
                }, "MgPanel HelpNote");
            helpnote.Add(
                new CuiLabel
                {
                    Text =
                    {
                        Color = HexToRustFormat(_config.UISetting.Layer2.Help.TextColor),
                        Text = "[MAIN_TEXT]",
                        Align = TextAnchor.UpperLeft,
                        Font = _config.UISetting.Layer2.Help.TextFont,
                        FontSize = 12,
                        FadeIn = 1f
                    },
                    RectTransform = { AnchorMin = "0.02 0.2", AnchorMax = "0.98 0.88", }
                }, "MgPanel HelpNote");
            helpnote.Add(
                new CuiButton
                {
                    Text =
                    {
                        Text = "OK",
                        Color = _config.UISetting.Layer2.Help.LabelColor,
                        Align = TextAnchor.MiddleCenter,
                        FadeIn = 1f
                    },
                    RectTransform = { AnchorMin = "0.67 0.07", AnchorMax = "0.93 0.18", },
                    Button = { Color = "0 0 0 0", Command = "[COMMAND]" }
                }, "MgPanel HelpNote");
            helpnote.Add(
                new CuiButton
                {
                    Text = { Text = "" },
                    RectTransform = { AnchorMin = "0.9 0.9", AnchorMax = "1 1", },
                    Button = { Color = "0 0 0 0", Command = "MgPanel HelpClose" }
                }, "MgPanel HelpNote");
            helpnote.Add(new CuiElement
            {
                Parent = "MgPanel HelpNote",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Color = HexToRustFormat(_config.UISetting.Layer2.Help.UrlColor),
                        Align = TextAnchor.MiddleCenter,
                        Text = "[TEXT_URL]",
                        ReadOnly = true,
                        LineType = InputField.LineType.SingleLine,
                        Command = ""
                    },
                    new CuiRectTransformComponent { AnchorMin = "0.05 0.05", AnchorMax = "0.68 0.17", }
                }
            });
            jsonNote = helpnote.ToJson();
        }

        private int nextsize = 0;

        void CreateDownL2(CuiElementContainer layer1)
        {
            for (int i = 0; i < _config.UISetting.LayerOneSet.Element.Info.Count; i++)
            {
                layer1.Add(
                    new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = $"{nextsize * _config.UISetting.LayerOneSet.GlobalScale} 0",
                            OffsetMax =
                                $"{(_config.UISetting.LayerOneSet.Element.Info[i].Size + nextsize) * _config.UISetting.LayerOneSet.GlobalScale} {21 * _config.UISetting.LayerOneSet.GlobalScale}"
                        },
                        Image = { Color = "0, 0, 0, 0" }
                    }, layer_1, $"L2_{_config.UISetting.LayerOneSet.Element.Info[i].InFunk}");
                nextsize = nextsize + _config.UISetting.LayerOneSet.Element.Info[i].Size;
                layer1.Add(new CuiElement
                {
                    Parent = $"L2_{_config.UISetting.LayerOneSet.Element.Info[i].InFunk}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Color =
                                HexToRustFormat(_config.UISetting.LayerOneSet.Element.Info[i].ImageColor),
                            Png = ImageLibrary.Call<string>("GetImage",
                                _config.UISetting.LayerOneSet.Element.Info[i].Image)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = $"{3 * _config.UISetting.LayerOneSet.GlobalScale} 0",
                            OffsetMax =
                                $"{24 * _config.UISetting.LayerOneSet.GlobalScale} {21 * _config.UISetting.LayerOneSet.GlobalScale}"
                        }
                    }
                });
                if (_config.UISetting.LayerOneSet.Element.L1Up.Stripes)
                {
                    layer1.Add(new CuiElement
                    {
                        Parent = $"L2_{_config.UISetting.LayerOneSet.Element.Info[i].InFunk}",
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = HexToRustFormat(_config.UISetting.LayerOneSet.Element.L1Up
                                    .StripeColor)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "1 1",
                                AnchorMax = "1 1",
                                OffsetMin =
                                    $"{-1 * _config.UISetting.LayerOneSet.GlobalScale} -{(_config.UISetting.LayerOneSet.Height - 1) * _config.UISetting.LayerOneSet.GlobalScale}",
                                OffsetMax =
                                    $"{1 * _config.UISetting.LayerOneSet.GlobalScale} {1 * _config.UISetting.LayerOneSet.GlobalScale}"
                            }
                        }
                    });
                }

                string text = "";
                switch (_config.UISetting.LayerOneSet.Element.Info[i].InFunk)
                {
                    case "Online":
                    {
                        text = "[ONLINE_PLAYERS]";
                        break;
                    }
                    case "Sleepers":
                    {
                        text = "[SPEEPERS]";
                        break;
                    }
                    case "Time":
                    {
                        text = "[TIME]";
                        break;
                    }
                    case "Queue":
                    {
                        text = "[CONNECTING]";
                        break;
                    }
                    case "Balance":
                    {
                        text = "[BALANCE]";
                        break;
                    }
                }

                layer1.Add(
                    new CuiLabel
                    {
                        Text =
                        {
                            Text = text,
                            FontSize =
                                (int)(_config.UISetting.LayerOneSet.Element.Info[i].TextSize *
                                      _config.UISetting.LayerOneSet.GlobalScale),
                            Font = _config.UISetting.LayerOneSet.Element.Info[i].Font,
                            Align = TextAnchor.MiddleCenter,
                            Color = HexToRustFormat(_config.UISetting.LayerOneSet.Element.Info[i].TextColor)
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 0.5",
                            AnchorMax = "0 0.5",
                            OffsetMin =
                                $"{24 * _config.UISetting.LayerOneSet.GlobalScale} {-10 * _config.UISetting.LayerOneSet.GlobalScale}",
                            OffsetMax =
                                $"{_config.UISetting.LayerOneSet.Element.Info[i].Size * _config.UISetting.LayerOneSet.GlobalScale} {10 * _config.UISetting.LayerOneSet.GlobalScale}"
                        }
                    }, $"L2_{_config.UISetting.LayerOneSet.Element.Info[i].InFunk}");
            }

            jsonL1 = layer1.ToJson();
            UpdateInfo();
        }

        void CreateHotbar()
        {
            if (_config.UISetting.Layer2.Hotbars.Count <= 0) return;
            CuiElementContainer hotbar = new CuiElementContainer();
            hotbar.Add(
                new CuiPanel
                {
                    CursorEnabled = false,
                    RectTransform =
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-200 0", OffsetMax = "200 13"
                    },
                    Image = { Color = "0 0 0 0" }
                }, "Overlay", "Hotbar_panel");
            int size = 378 / _config.UISetting.Layer2.Hotbars.Count;
            for (int i = 0; i < _config.UISetting.Layer2.Hotbars.Count; i++)
            {
                hotbar.Add(new CuiElement
                {
                    Parent = "Hotbar_panel",
                    Name = $"ButtonHotbar_{i}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = ImageLibrary.Call<string>("GetImage",
                                _config.UISetting.Layer2.HotbarButPng),
                            Color = HexToRustFormat(_config.UISetting.Layer2.Hotbars[i].Color)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.5",
                            AnchorMax = "0 0.5",
                            OffsetMin = $"{size * i} -8",
                            OffsetMax = $"{size * (i + 1)} 8"
                        }
                    }
                });
                hotbar.Add(
                    new CuiButton 
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1.8" },
                        Button = { Command = _config.UISetting.Layer2.Hotbars[i].Command, Color = "0 0 0 0" },
                        Text =
                        {
                            Text = _config.UISetting.Layer2.Hotbars[i].Text,
                            Color = HexToRustFormat(_config.UISetting.Layer2.Hotbars[i].TextColor),
                            Font = _config.UISetting.Layer2.Hotbars[i].Font,
                            Align = TextAnchor.LowerCenter
                        }
                    }, $"ButtonHotbar_{i}");
            }

            jsonHotbar = hotbar.ToJson();
        }

        private string jsonSending;

        private void UpdateInfo()
        {
            jsonSending = jsonL1.Replace("[ONLINE_PLAYERS]", BasePlayer.activePlayerList.Count.ToString())
                .Replace("[SPEEPERS]", BasePlayer.sleepingPlayerList.Count.ToString())
                .Replace("[TIME]", covalence.Server.Time.ToString("HH:mm"))
                .Replace("[CONNECTING]", ServerMgr.Instance.connectionQueue.Joining.ToString())
                .Replace("[heli_COLOR]", Heli > 0 ? HeliActive : HeliInActive)
                .Replace("[cargo_COLOR]", Cargo > 0 ? CargoActive : CargoInActive)
                .Replace("[bradley_COLOR]", Tank > 0 ? TankActive : TankInActive)
                .Replace("[chinook_COLOR]", Cheenook > 0 ? ChActive : ChInActive);
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (_opened.Contains(player.userID))
                {
                    CommunityEntity.ServerInstance.ClientRPCEx(
                        new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", layer_1);
                    if (_config.Another.Eco.Use)
                    {
                        CommunityEntity.ServerInstance.ClientRPCEx(
                            new Network.SendInfo { connection = player.net.connection }, null, "AddUI",
                            jsonSending.Replace("[BALANCE]", Balance(player).ToString()));
                    }
                    else
                    {
                        CommunityEntity.ServerInstance.ClientRPCEx(
                            new Network.SendInfo { connection = player.net.connection }, null, "AddUI", jsonSending);
                    }
                }
            }

            timer.Once(30f, UpdateInfo);
        }

        #endregion

        #region ConsoleCommands

        private List<ulong> SwitchPanel = new List<ulong>();

        [ConsoleCommand("panelswitch")]
        void SwitchShowPanel(ConsoleSystem.Arg ag)
        {
            BasePlayer p = ag.Player();
            if (SwitchPanel.Contains(p.userID))
            {
                SwitchPanel.Remove(p.userID);
                OnPlayerConnected(p);
                SendReply(p, "Панель восстановлена!");
            }
            else
            {
                SwitchPanel.Add(p.userID);
                _opened.Remove(p.userID);
                CuiHelper.DestroyUi(p, "Hotbar_panel");
                CuiHelper.DestroyUi(p, "MgPanel_main");
                SendReply(p, "Панель скрыта!");
            }
        }

        [ConsoleCommand("MgPanel")]
        void InterfaceButtons(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (arg.Args.Length <= 0) return;
            switch (arg.Args[0])
            {
                case "mainclick":
                {
                    CommunityEntity.ServerInstance.ClientRPCEx(
                        new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", layer_1);
                    //CuiHelper.DestroyUi(player, layer_1);
                    CuiHelper.DestroyUi(player, "MainBut");
                    if (_config.Another.Eco.Use)
                    {
                        CommunityEntity.ServerInstance.ClientRPCEx(
                            new Network.SendInfo { connection = player.net.connection }, null, "AddUI",
                            jsonSending.Replace("[BALANCE]", Balance(player).ToString()));
                    }
                    else
                    {
                        CommunityEntity.ServerInstance.ClientRPCEx(
                            new Network.SendInfo { connection = player.net.connection }, null, "AddUI", jsonSending);
                    }

                    CuiHelper.AddUi(player, MainClickBut2);
                    _opened.Add(player.userID);
                    break;
                }
                case "mainclick2":
                {
                    CommunityEntity.ServerInstance.ClientRPCEx(
                        new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", layer_2);
                    CuiHelper.DestroyUi(player, "MainBut");
                    CuiHelper.AddUi(player, MainClickBut3);
                    CommunityEntity.ServerInstance.ClientRPCEx(
                        new Network.SendInfo { connection = player.net.connection }, null, "AddUI", jsonL2);
                    break;
                }
                case "mainclick3":
                {
                    CommunityEntity.ServerInstance.ClientRPCEx(
                        new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", layer_1);
                    CommunityEntity.ServerInstance.ClientRPCEx(
                        new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", layer_2);
                    CuiHelper.DestroyUi(player, "MainBut");
                    CuiHelper.AddUi(player, MainClickBut);
                    _opened.Remove(player.userID);
                    RemoveOpened(player.userID);
                    break;
                }
                case "HelpNoteCmd":
                {
                    int button = int.Parse(arg.Args[1]);
                    CommunityEntity.ServerInstance.ClientRPCEx(
                        new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI",
                        "MgPanel HelpNote");
                    CommunityEntity.ServerInstance.ClientRPCEx(
                        new Network.SendInfo { connection = player.net.connection }, null, "AddUI",
                        jsonNote.Replace("[LABEL_TEXT]", _config.UISetting.Layer2.Button[button].HelpNoteLabel)
                            .Replace("[MAIN_TEXT]", _config.UISetting.Layer2.Button[button].HelpNoteText)
                            .Replace("[COMMAND]",
                                $"MgPanel HelpNotePullCmd {_config.UISetting.Layer2.Button[button].Cmd}")
                            .Replace("[TEXT_URL]", _config.UISetting.Layer2.Button[button].HelpNoteUrl));
                    break;
                }
                case "HelpNotePullCmd":
                {
                    CommunityEntity.ServerInstance.ClientRPCEx(
                        new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI",
                        "MgPanel HelpNote_panel");
                    rust.RunClientCommand(player, string.Join(" ", arg.Args).Replace("HelpNotePullCmd ", ""));
                    break;
                }
                case "HelpClose":
                {
                    CommunityEntity.ServerInstance.ClientRPCEx(
                        new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI",
                        "MgPanel HelpNote_panel");
                    break;
                }
            }
        }

        #endregion

        #region OtherMethods

        void RemoveOpened(ulong ID)
        {
            _opened.Remove(ID);
            if (_opened.Contains(ID))
            {
                RemoveOpened(ID);
            }
        }

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');
            if (str.Length == 6) str += "FF";
            if (str.Length != 8)
            {
                throw new InvalidOperationException("Cannot convert a wrong format.");
                throw new Exception(hex);
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
            Color color = new Color32(r, g, b, a);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        public int Balance(BasePlayer player)
        {
            return plugins?.Find(_config.Another.Eco.EcoName)?.Call<int>(_config.Another.Eco.Hook, player.userID) ?? 0;
        }

        #endregion

        #region API

        [HookMethod("SendNoteMessage")]
        void SendNoteMessage(BasePlayer player, string title = "", string text = "", string executableCommand = "",
            string url = "")
        {
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection },
                null, "DestroyUI", "MgPanel HelpNote");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection },
                null, "AddUI",
                jsonNote.Replace("[LABEL_TEXT]", title).Replace("[MAIN_TEXT]", text)
                    .Replace("[COMMAND]", $"MgPanel HelpNotePullCmd {executableCommand}").Replace("[TEXT_URL]", url));
        }

        [HookMethod("SendNoteMessageEveryone")]
        void SendNoteMessageEveryone(string title = "", string text = "", string executableCommand = "",
            string url = "")
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection },
                    null, "DestroyUI", "MgPanel HelpNote");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection },
                    null, "AddUI",
                    jsonNote.Replace("[LABEL_TEXT]", title).Replace("[MAIN_TEXT]", text)
                        .Replace("[COMMAND]", $"MgPanel HelpNotePullCmd {executableCommand}")
                        .Replace("[TEXT_URL]", url));
            }
        }

        #endregion
    }
}