using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using Newtonsoft.Json;
using static Oxide.Plugins.NRofls;

namespace Oxide.Plugins
{
    [Info("NRofls", "North", "1.0.1")]
    [Description("Рофлим над читерами.. ")] // надеюсь вам понравится, я старался, хахах =) 

    class NRofls : RustPlugin
    {
		#region [ VARIABLES ]
		private const string PERM_USE = "nrofls.use";

        [PluginReference] Plugin ImageLibrary;
		private ConfigData _config;
        #endregion

        #region [ OXIDE HOOK ]
        private void Loaded()
        {
            ReadConfig();

			if (!permission.PermissionExists(PERM_USE))
				permission.RegisterPermission(PERM_USE, this);

			foreach (var screamer in _config.Screamers) AddImage(screamer.Url, screamer.GetShortName());

			Puts(
				$"Загружено {_config.Screamers.Count} скримеров\n" +
				$"Скримеры: {_config.Screamers.Select(x => x.Name).ToList().ToSentence()}"
				);
		}
		#endregion

		#region [ CMD ]
		[ChatCommand("screamer")]
        void Screamer_ChatCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM_USE))
            {
                player.ChatMessage("У вас нет прав чтобы использовать эту команду!");
                return;
            }

            if (args.Length != 2)
            {
                player.ChatMessage("Недостаточно аргументов\n" +
                    "используйте /screamer <ник игрока> <название скримера>");
                return;
            }

            BasePlayer? TargetPlayer = FindPlayer(args[0]);
            if (TargetPlayer == null)
            {
                player.ChatMessage("Игрок не найден");
                return;
            }


            Screamer scream = _config.Screamers.Where(x => x.Name.ToLower() == args[1].ToLower()).FirstOrDefault();
            if (scream == null)
            {
                player.ChatMessage("Скример не найден!");
                return;
            }

            ShowScreamer(player, TargetPlayer, scream, _config.ScreamerTime);
		}
		#endregion

		private void ShowScreamer(BasePlayer adminPlayer, BasePlayer targetPlayer, Screamer screamer, float time = 5f)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image =
                {
                    Color = "0 0 0 0.5",
                    Material = "assets/content/ui/uibackgroundblur.mat",
                    Sprite = "assets/content/ui/ui.background.transparent.radial.psd"
                }

            }, "Overlay", "MainUI");
            container.Add(new CuiElement
            {
                Parent = "MainUI",
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage(screamer.GetShortName()) },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },

                }
            });

            adminPlayer.ChatMessage($"Запустили скример {screamer.Name} игроку: " + targetPlayer.displayName);


            CuiHelper.AddUi(targetPlayer, container);

            timer.Once(time, () =>
            {
                CuiHelper.DestroyUi(targetPlayer, "MainUI");
            });

        }

		#region [ EXT ]
		BasePlayer? FindPlayer(string filter) => BasePlayer.activePlayerList.Where(x => x.displayName.ToLower().Contains(filter.ToLower())).FirstOrDefault();
        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary.Call("GetImage", shortname, skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
		#endregion

		#region [ CONFIG ]
		class ConfigData
		{
            [JsonProperty("Скримеры [название/любой текст/ссылка на картинку]")]
		    public List<Screamer> Screamers = new List<Screamer>()
            {
                new Screamer() { Name = "1", Url = "https://gspics.org/images/2024/02/09/0Zlddi.jpg" },
                new Screamer() { Name = "2", Url = "https://gspics.org/images/2024/02/09/0ZlyE3.jpg" },
                new Screamer() { Name = "3", Url = "https://www.meme-arsenal.com/memes/2f95d4ee317ce9536249179577a4d7ad.jpg" },
                new Screamer() { Name = "4", Url = "https://gspics.org/images/2024/02/09/0ZeDry.jpg" },
            };

            [JsonProperty("Время показа скримера")]
            public float ScreamerTime = 5f;
	    }
		protected override void LoadDefaultConfig()
		{
			var config = new ConfigData();

			SaveConfig(config);
		}
		void SaveConfig(object config)
		{
			Config.WriteObject(config, true);
		}
		void ReadConfig()
		{
			base.Config.Settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
			_config = Config.ReadObject<ConfigData>();
			SaveConfig(_config);
		}
		#endregion
		public class Screamer
        {
            public string Name = "нейм";

            public string Url = "";
            public string GetShortName() => Name.Replace("https://", "").Replace(".jpg", "");
        }
	}
}