using UnityEngine;
using System.Text;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using Random = Oxide.Core.Random;

namespace Oxide.Plugins
{
    [Info("GoldCard", "shoprust.ru", "2.0.1")]
    [Description("Золотая карта доступа")]
    class GoldCard : RustPlugin
    {
        
        private void OnLootSpawn(LootContainer container)
        {
            if (container is null) return;

            Configuration.DropSettings.DropPrefabSettings dropPrefabSetting = _config.CardDropSettings.DropPrefabs
                .Find(prefab => prefab.ShortPrefabName.Contains(container.ShortPrefabName));

            if (dropPrefabSetting is null || !(Random.Range(0, 100) <= dropPrefabSetting.DropChance)) return;

            timer.Once(0.25f, () =>
            {
                if (container.inventory is null || container.inventory.capacity <= container.inventory.itemList.Count)
                    container.inventory.capacity = container.inventory.itemList.Count + 1;

                Item newItem = ItemManager.CreateByName(ShortName, 1, _config.SkinID);
                newItem.name = _config.DisplayName;
                newItem.MoveToContainer(container.inventory);
            });
        }

                
                private Configuration _config;
                private const string ShortName = "keycard_red";

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }
        public static StringBuilder Sb;
        
        [ConsoleCommand("goldcard.give")]
        private void CmdGiveCard(ConsoleSystem.Arg arg)
        {
            if (arg is null) return;

            BasePlayer player = arg.Player();
            if (player != null && !player.IsAdmin)
            {
                PrintToConsole(player, GetLang("GC_CMD_1", player.UserIDString));
                return;
            }
            
            if (!arg.HasArgs())
            {
                SendConsoleMessage(player, GetLang("GC_COMMAND_SYNTAX_ERROR", player?.UserIDString, "goldcard.give STEAMID"));
                return;
            }
            
            if(!ulong.TryParse(arg.GetString(0), out ulong playerid))
            {
                SendConsoleMessage(player, GetLang("GC_INVALID_PLAYER_ID_INPUT", player?.UserIDString));
                return;
            }

            BasePlayer playerToGive = BasePlayer.FindByID(playerid);
            if (!playerid.IsSteamId() || playerToGive == null)
            {
                SendConsoleMessage(player, GetLang("GC_NOT_A_STEAM_ID", player?.UserIDString));
                return;
            }
            
            CreateItem(playerToGive);
            PrintToChat(playerToGive, GetLang("GC_PLAYER_GIVE_CARD", playerToGive.UserIDString, _config.DisplayName));
            SendConsoleMessage(player, GetLang("GC_CMD_GIVE_CARD", player?.UserIDString, $"{playerToGive.displayName} ({playerid})"));
        }

        public string GetLang(string LangKey, string userID = null, params object[] args)
        {
            Sb?.Clear();
            if (args != null)
            {
                Sb?.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return Sb?.ToString();
            }

            return lang.GetMessage(LangKey, this, userID);
        }
        
        
                
        private void Unload() => Sb = null;

        private class Configuration
        {
            
            public class DropSettings
            {
                [JsonProperty("Конфигурация выпадения карт из контейнеров (бочек, ящиков)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<DropPrefabSettings> DropPrefabs = new()
                {
                    new DropPrefabSettings
                    {
                        ShortPrefabName = "crate_normal",
                        DropChance = 15,
                    },
                    new DropPrefabSettings
                    {
                        ShortPrefabName = "crate_elite",
                        DropChance = 25,
                    },
                    new DropPrefabSettings
                    {
                        ShortPrefabName = "codelockedhackablecrate_oilrig",
                        DropChance = 30,
                    },
                    new DropPrefabSettings
                    {
                        ShortPrefabName = "loot_barrel_1",
                        DropChance = 3,
                    },
                };

                public class DropPrefabSettings
                {
                    [JsonProperty("Короткое имя контейнера")]
                    public string ShortPrefabName;

                    [JsonProperty("Вероятность выпадения (0-100%)")]
                    public int DropChance;
                    
                }
            }
            
            [JsonProperty("Настройки потери прочности для карты (стандартная прочность: 2.0, стандартная потеря: 1.0)")]
            public CastleDurabilityLossSettings DurabilityLossSettings = new();
		   		 		  						  	   		  	 	 		  	   		  	  			  	 	 
            public class CastleDurabilityLossSettings
            {
                [JsonProperty("Потеря прочности при свайпе по синему замку")]
                public float BlueCastleDurabilityLoss = 0.6f;

                public float GetDurabilityLoss(int level)
                {
                    return level switch
                    {
                        1 => GreenCastleDurabilityLoss,
                        2 => BlueCastleDurabilityLoss,
                        3 => RedCastleDurabilityLoss,
                        _ => throw new ArgumentOutOfRangeException(nameof(level), "Уровень должен быть в диапазоне от 1 до 3.")
                    };
                }
                [JsonProperty("Потеря прочности при свайпе по зеленому замку")]
                public float GreenCastleDurabilityLoss = 0.3f;
                [JsonProperty("Потеря прочности при свайпе по красному замку")]
                public float RedCastleDurabilityLoss = 1.1f;
            }
            [JsonProperty("SkinID для предмета")]
            public ulong SkinID = 1977450795;
            [JsonProperty("Настройка выпадения карты из бочек и ящиков")]
            public DropSettings CardDropSettings = new();
            [JsonProperty("DisplayName для предмета")]
            public string DisplayName="Карта общего доступа";
        }
        private void Init() => Sb = new StringBuilder();

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }
		   		 		  						  	   		  	 	 		  	   		  	  			  	 	 
        private object OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            Item goldCard = card.GetItem();
            if (goldCard is not null && goldCard.skin == _config.SkinID && goldCard.conditionNormalized > 0.0)
            {
                cardReader.Invoke(cardReader.GrantCard, 0.5f);

                float durabilityLoss = _config.DurabilityLossSettings?.GetDurabilityLoss(cardReader.accessLevel) ?? 0.5f;
		   		 		  						  	   		  	 	 		  	   		  	  			  	 	 
                goldCard.LoseCondition(durabilityLoss);
                return cardReader.inputs[0]?.connectedTo?.Get() == null;
            }
            return null;
        }
                
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GC_CMD_1"] = "No Permission!!",
                ["GC_COMMAND_SYNTAX_ERROR"] = "Incorrect syntax! Use: {0}",
                ["GC_INVALID_PLAYER_ID_INPUT"] = "Invalid input! Please enter a valid player ID.",
                ["GC_NOT_A_STEAM_ID"] = "The entered ID is not a SteamID or the player is not online. Please check and try again.",
                ["GC_CMD_GIVE_CARD"] = "The golden card was successfully issued to the player {0}",
                ["GC_PLAYER_GIVE_CARD"] = "You have successfully received {0}",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GC_CMD_1"] = "Недостаточно прав!",
                ["GC_COMMAND_SYNTAX_ERROR"] = "Неверный синтаксис! Используйте: {0}",
                ["GC_INVALID_PLAYER_ID_INPUT"] = "Неверный ввод! Пожалуйста, введите корректный ID игрока.",
                ["GC_NOT_A_STEAM_ID"] = "Введенный ID не является SteamID или игрок не в сети. Пожалуйста, проверьте и попробуйте снова.",
                ["GC_CMD_GIVE_CARD"] = "Золотая карта успешно выдана игроку {0}",
                ["GC_PLAYER_GIVE_CARD"] = "Вы успешно получили {0}",
            }, this, "ru");
        }

                
                private void CreateItem(BasePlayer player)
        {
            Item item = ItemManager.CreateByName(ShortName, 1, _config.SkinID);
            item.name = _config.DisplayName;
            player.GiveItem(item);
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                    LoadDefaultConfig();
                SaveConfig();
            }
            catch (JsonException ex)
            {
                Debug.LogException(ex);
                LoadDefaultConfig();
            }
        }

        
                private void SendConsoleMessage(BasePlayer player, string message)
        {
            if(player != null)
                player.ConsoleMessage(message);
            else
                PrintWarning(message);
        }
            }
}
