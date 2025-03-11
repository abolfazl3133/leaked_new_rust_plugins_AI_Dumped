using Oxide.Ext.Discord.Builders;
using Oxide.Ext.Discord.Builders;
using Oxide.Ext.Discord.Clients;
 using Oxide.Ext.Discord.Connections;
 using Oxide.Ext.Discord.Entities;
 using Oxide.Ext.Discord.Libraries; 
 using Oxide.Ext.Discord.Logging;
using Oxide.Ext.Discord.Clients;
 using Oxide.Ext.Discord.Connections;
 using Oxide.Ext.Discord.Entities;
 using Oxide.Ext.Discord.Libraries; 
 using Oxide.Ext.Discord.Logging;



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 



 
 
  
 

 
 
  
 
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using CompanionServer;
using Oxide.Ext.Discord;









using System.Text.RegularExpressions;




using Oxide.Core.Libraries.Covalence;
using ru = Oxide.Game.Rust;
using ConVar;

namespace Oxide.Plugins
{
    /*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("AAlertRaid", "https://discord.gg/dNGbxafuJn", "1.0.0")]
    public class AAlertRaid : RustPlugin
    {
        #region CONFIG
        const bool fermensEN = false;

        private static PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }


        class VK
        {
            [JsonProperty(fermensEN ? "Enable?" : "Включить?")]
            public bool enable;

            [JsonProperty(fermensEN ? "API" : "API от группы")]
            public string api;

            [JsonProperty(fermensEN ? "Link to the group" : "Ссылка на группу")]
            public string link;

            [JsonProperty(fermensEN ? "Cooldown for sending" : "Кд на отправку")]
            public float cooldown;
        }
        class RUSTPLUS
        {
            [JsonProperty(fermensEN ? "Enable?" : "Включить?")]
            public bool enable;

            [JsonProperty(fermensEN ? "Cooldown for sending" : "Кд на отправку")]
            public float cooldown;
        }
        class INGAME
        {
            [JsonProperty(fermensEN ? "Enable?" : "Включить?")]
            public bool enable;

            [JsonProperty(fermensEN ? "Cooldown for sending" : "Кд на отправку")]
            public float cooldown;

            [JsonProperty(fermensEN ? "Send game effect when notification are received" : "Эффект при получении уведомления")]
            public string effect;

            [JsonProperty(fermensEN ? "Time after the UI is destroyed" : "Время, через которое пропадает UI [секунды]")]
            public float destroy;

            [JsonProperty("UI")]
            public string UI;
        }

        class DISCORD
        {
            [JsonProperty(fermensEN ? "Enable?" : "Включить?")]
            public bool enable;

            [JsonProperty(fermensEN ? "Cooldown for sending" : "Кд на отправку")]
            public float cooldown;

            [JsonProperty(fermensEN ? "Invitation link" : "Приглашение в группу")]
            public string link;

            [JsonProperty(fermensEN ? "Token (https://discordapp.com/developers/applications)" : "Токен бота (https://discordapp.com/developers/applications)")]
            public string token;

            [JsonProperty(fermensEN ? "Channel ID, where the player will take the code to confirm the profile" : "ID канала, гле игрок будет брать код, для подтверджения профиля")]
            public string channel;

            [JsonProperty(fermensEN ? "Info text" : "Дискорд канал с получением кода - текст")]
            public string channeltext;

            [JsonProperty(fermensEN ? "Info text - line color on the left" : "Дискорд канал с получением кода - цвет линии слева (https://gist.github.com/thomasbnt/b6f455e2c7d743b796917fa3c205f812#file-code_colors_discordjs-md)")]
            public uint channelcolor;

            [JsonProperty(fermensEN ? "Text on button" : "Дискорд канал с получением кода - кнопка")]
            public string channelbutton;

            [JsonProperty(fermensEN ? "Reply after button click" : "Дискорд канал с получением кода - ответ")]
            public string channelex;

            [JsonProperty(fermensEN ? "Don't touch this field" : "Дискорд канал с получением кода - ID сообщения (не трогаем! сам заполнится!)")]
            public string channelmessageid;
        }

        class TELEGRAM
        {
            [JsonProperty(fermensEN ? "Enable?" : "Включить?")]
            public bool enable;

            [JsonProperty(fermensEN ? "Cooldown for sending" : "Кд на отправку")]
            public float cooldown;

            [JsonProperty(fermensEN ? "Bot tag" : "Тэг бота")]
            public string bottag;

            [JsonProperty(fermensEN ? "Token" : "Токен")]
            public string token;
        }

        class UIMenu
        {
            [JsonProperty(fermensEN ? "Background color" : "Цвет фона")]
            public string background;

            [JsonProperty(fermensEN ? "Strip color" : "Цвет полоски")]
            public string stripcolor;

            [JsonProperty(fermensEN ? "Rectangular container background color" : "Цвет фона прямоугольного контейнера")]
            public string rectangularcolor;

            [JsonProperty(fermensEN ? "Button text color" : "Цвет текста в кнопке")]
            public string buttoncolortext;

            [JsonProperty(fermensEN ? "Text color" : "Цвет текста")]
            public string textcolor;

            [JsonProperty(fermensEN ? "Green button color" : "Цвет зелёной кнопки")]
            public string greenbuttoncolor;

            [JsonProperty(fermensEN ? "Red button color" : "Цвет красной кнопки")]
            public string redbuttoncolor;

            [JsonProperty(fermensEN ? "Gray button color" : "Цвет серой кнопки")]
            public string graybuttoncolor;

            [JsonProperty(fermensEN ? "Header text color" : "Цвет текста заголовка")]
            public string headertextcolor;

            [JsonProperty(fermensEN ? "Error text color" : "Цвет текста ошибки")]
            public string errortextcolor;

            [JsonProperty(fermensEN ? "Text color of <exit> and <back> buttons" : "Цвет текста кнопок <выход> и <назад>")]
            public string colortextexit;

            [JsonProperty(fermensEN ? "Rectangular container text color" : "Цвет текст прямоугольного контейнера")]
            public string rectangulartextcolor;

            [JsonProperty(fermensEN ? "The color of the text with hints at the bottom of the screen" : "Цвет текста с подсказками внизу экрана")]
            public string hintstextcolor;

            [JsonProperty(fermensEN ? "Abbreviations and their colors" : "Аббревиатуры и их цвета")]
            public UIMainMenu uIMainMenu;
        }

        class UIMainMenu
        {
            [JsonProperty(fermensEN ? "Abbreviation for telegram" : "Аббревиатура для телеграма")]
            public string abr_telegram;

            [JsonProperty(fermensEN ? "Telegram icon color" : "Цвет иконки телеграма")]
            public string color_telegram;

            [JsonProperty(fermensEN ? "Abbreviation for vk.com" : "Аббревиатура для вконтакте")]
            public string abr_vk;

            [JsonProperty(fermensEN ? "Vk.com icon color" : "Цвет иконки вконтакте")]
            public string color_vk;

            [JsonProperty(fermensEN ? "Abbreviation for rust+" : "Аббревиатура для rust+")]
            public string abr_rustplus;

            [JsonProperty(fermensEN ? "Rust+ icon color" : "Цвет иконки rust+")]
            public string color_rustplus;

            [JsonProperty(fermensEN ? "Abbreviation for discord" : "Аббревиатура для дискорда")]
            public string abr_discord;

            [JsonProperty(fermensEN ? "Discord icon color" : "Цвет иконки дискорда")]
            public string color_discord;

            [JsonProperty(fermensEN ? "Abbreviation for in game" : "Аббревиатура для графическое отображение в игре")]
            public string abr_ui;

            [JsonProperty(fermensEN ? "In game icon color" : "Цвет иконки графическое отображение в игре")]
            public string color_ui;
        }

        private class PluginConfig
        {
            [JsonProperty(fermensEN ? "Server name, will using for alerts" : "Название сервера - для оповещений")]
            public string servername;

            [JsonProperty(fermensEN ? "Raid alert works only for those who have permission" : "Оповещение о рейде работает только для тех, у кого есть разрешение")]
            public bool needpermission;

            [JsonProperty(fermensEN ? "VK.com" : "Оповещание о рейде в ВК")]
            public VK vk;

            [JsonProperty(fermensEN ? "Rust+" : "Оповещание о рейде в Rust+")]
            public RUSTPLUS rustplus;

            [JsonProperty(fermensEN ? "In game" : "Оповещание о рейде в игре")]
            public INGAME ingame;

            [JsonProperty(fermensEN ? "Discord" : "Оповещание о рейде в дискорд")]
            public DISCORD discord;

            [JsonProperty(fermensEN ? "Telegram" : "Оповещание о рейде в телеграм")]
            public TELEGRAM telegram { get; set; } = new TELEGRAM
            {
                token = "",
                cooldown = 1200f,
                enable = true,
                bottag = "@haxlite_bot"
            };

            [JsonProperty(fermensEN ? "Menu UI" : "Настройка UI")]
            public UIMenu ui { get; set; } = new UIMenu
            {
                background = "0.07843138 0.06666667 0.1098039 0.9490196",
                stripcolor = "0.8784314 0.9843137 1 0.5686275",
                rectangularcolor = "0.8901961 0.8901961 0.8901961 0.4156863",
                graybuttoncolor = "0.8901961 0.8901961 0.8901961 0.4156863",
                buttoncolortext = "1 1 1 0.9056942",
                rectangulartextcolor = "1 1 1 0.7843137",
                textcolor = "1 1 1 1",
                headertextcolor = "1 1 1 1",
                hintstextcolor = "1 1 1 0.6699298",
                greenbuttoncolor = "0.5450981 1 0.6941177 0.509804",
                errortextcolor = "1 0.5429931 0.5429931 0.787812",
                colortextexit = "0.5938045 0.5789595 0.5789595 1",
                redbuttoncolor = "1 0.5450981 0.5450981 0.509804",
                uIMainMenu = new UIMainMenu
                {
                    abr_discord = "DS",
                    abr_rustplus = "R+",
                    abr_telegram = "TG",
                    abr_ui = "UI",
                    abr_vk = "VK",
                    color_discord = "0.6313726 0.5764706 1 0.4156863",
                    color_rustplus = "1 0.5803921 0.6013725 0.4156863",
                    color_vk = "0.5803922 0.6627451 1 0.4156863",
                    color_ui = "1 0.7843137 0.5764706 0.4156863",
                    color_telegram = "0.5479987 0.9459876 1 0.4156863"
                }
            };


            [JsonProperty(fermensEN ? "Additional list" : "Дополнительный список предметов, которые учитывать")]
            public string[] spisok;

            [JsonProperty(fermensEN ? "Notification when usual items are destroyed" : "Оповещение при уничтожении обычных предметов")]
            public bool extralist;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    servername = "HaxLite X10",
                    vk = new VK
                    {
                        api = "",
                        cooldown = 1200f,
                        enable = true,
                    },
                    rustplus = new RUSTPLUS
                    {
                        cooldown = 600f,
                        enable = true
                    },
                    ingame = new INGAME
                    {
                        cooldown = 60f,
                        enable = true,
                        effect = "assets/prefabs/weapons/toolgun/effects/repairerror.prefab",
                        destroy = 4f,
                        UI = "[{\"name\":\"UIA\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"material\":\"assets/content/ui/uibackgroundblur.mat\", \"sprite\":\"assets/content/ui/ui.background.transparent.linearltr.tga\",\"color\":\"0 0 0 0.6279221\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0.5\",\"anchormax\":\"1 0.5\",\"offsetmin\":\"-250 -30\",\"offsetmax\":\"0 30\"}]},{\"name\":\"D\",\"parent\":\"UIA\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"1 0 0 0.392904\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 5\"}]},{\"name\":\"T\",\"parent\":\"UIA\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text}\",\"fontSize\":12,\"align\":\"MiddleLeft\",\"color\":\"1 1 1 0.8644356\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 0\",\"offsetmax\":\"-5 0\"}]},{\"name\":\"U\",\"parent\":\"UIA\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"1 0 0 0.3921569\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 -5\",\"offsetmax\":\"0 0\"}]}]"
                    },
                    discord = new DISCORD
                    {
                        cooldown = 600f,
                        enable = true,
                        token = "",
                        channel = "",
                        channelbutton = fermensEN ? "Get code" : "Получить код",
                        channelex = fermensEN ? "Your code: {code}" : "Ваш код: {code}",
                        channelmessageid = "",
                        channeltext = fermensEN ? "Enter the received code in the integration menu for raid alerts.\nChat command /raid\nEnter it in the game itself, not in the discord!" : "Введите полученый код в меню интеграции дискорда с игровым профилем.\nЧат команда /raid\nВводить в самой игре, а не в дискорде!",
                        channelcolor = 14177041
                    },
                    spisok = _spisok
                };
            }
        }
        private static string[] _spisok = new string[] { "wall.external.high", "wall.external.high.stone", "gates.external.high.wood", "gates.external.high.stone", "wall.window.bars.metal", "wall.window.bars.toptier", "wall.window.glass.reinforced", "wall.window.bars.wood" };

        #endregion

        #region DISCORD
        private readonly BotConnection _discordSettings = new BotConnection();
        private DiscordGuild _guild;
         DiscordClient Client;
        private void CreateClient()
        {
            _discordSettings.ApiToken = config.discord.token;
            _discordSettings.Intents = GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.Guilds | GatewayIntents.GuildMembers;
            _discordSettings.LogLevel = Ext.Discord.Logging.DiscordLogLevel.Error;
            Client.Connect(_discordSettings);

            timer.Once(5f, () =>
            {
                if (Client == null)
                {
                    CreateClient();
                    Debug.Log("Discord reconnecting in 5 sec...");
                }
                else
                {
                    DiscordChannel channel;
                    if (!_guild.Channels.TryGetValue(new Snowflake(config.discord.channel), out channel))
                    {
                        Debug.Log(fermensEN ? $"CHANNEL NOT FOUND! ({_guild.Channels.Count})" : $"КАНАЛ НЕ СУЩЕСТВУЕТ! ({_guild.Channels.Count})");
                        return;
                    }

                    var embeds = new List<DiscordEmbed> { new DiscordEmbed { Color = new DiscordColor(config.discord.channelcolor), Description = config.discord.channeltext } };
                    var components = CreateComponents(config.discord.channelbutton);

                    if (!string.IsNullOrEmpty(config.discord.channelmessageid))
                    {
                        // Получаем список сообщений
                        channel.GetMessages(Client, new ChannelMessagesRequest { Limit = 50 }).Then(messages =>
                        {
                            // Ищем сообщение с нужным ID
                            var existingMessage = messages.FirstOrDefault(msg => msg.Id == new Snowflake(config.discord.channelmessageid));

                            if (existingMessage != null)
                            {
                                // Если сообщение найдено, редактируем его
                                var update = new MessageUpdate
                                {
                                    Embeds = embeds,
                                    Components = components
                                };

                                existingMessage.Edit(Client, update).Then(_ =>
                                {
                                    Puts("Сообщение успешно отредактировано.");
                                })
                                .Catch(error =>
                                {
                                    Puts($"Ошибка при редактировании сообщения: {error.Message}");
                                });
                            }
                            else
                            {
                                // Если сообщение не найдено, создаём новое
                                Puts("Сообщение не найдено, создаю новое.");
                                channel.CreateMessage(Client, new MessageCreate { Embeds = embeds, Components = components }).Then(newMessage =>
                                {
                                    config.discord.channelmessageid = newMessage.Id;
                                    SaveConfig();
                                    Puts("Новое сообщение успешно создано.");
                                })
                                .Catch(error =>
                                {
                                    Puts($"Ошибка при создании сообщения: {error.Message}");
                                });
                            }
                        })
                        .Catch(error =>
                        {
                            Puts($"Ошибка при получении сообщений: {error.Message}");
                        });
                    }
                    else
                    {
                        try
                        {
                            if (channel != null)
                            {
                                // Создаем сообщение
                                var createdMessage = channel.CreateMessage(Client, new MessageCreate
                                {
                                    Embeds = embeds,
                                    Components = components
                                });

                                // Если CreateMessage возвращает результат, обрабатываем его
                                if (createdMessage != null)
                                {
                                    config.discord.channelmessageid = createdMessage.Id;
                                    SaveConfig();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Puts($"Ошибка при создании сообщения: {ex.Message}");
                        }
                    }
                }
            });
        }

        private void OnDiscordInteractionCreated(DiscordInteraction interaction)
        {
            if (interaction.Type != InteractionType.MessageComponent)
            {
                return;
            }

            if (!interaction.Data.ComponentType.HasValue || interaction.Data.ComponentType.Value != MessageComponentType.Button || interaction.Data.CustomId != $"{Name}_{ConVar.Server.ip}_{ConVar.Server.port}")
            {
                return;
            }

            DiscordUser user = interaction.User ?? interaction.Member?.User;
            HandleAcceptLinkButton(interaction, user);

        }
        private void HandleAcceptLinkButton(DiscordInteraction interaction, DiscordUser user)
        {
            string num;
            if (!DISCORDCODES.TryGetValue(user.Id.Id, out num))
            {
                num = DISCORDCODES[user.Id.Id] = RANDOMNUM();
            }
            string linkMessage = Formatter.ToPlaintext(config.discord.channelex.Replace("{code}", num));
            interaction.CreateResponse(Client, new InteractionResponse
            {
                Type = InteractionResponseType.ChannelMessageWithSource,
                Data = new InteractionCallbackData
                {
                    Content = linkMessage,
                    Flags = MessageFlags.Ephemeral
                }
            });
        }

        private void OnDiscordGatewayReady(GatewayReadyEvent ready)
        {
            NextTick(()=> {
            _guild = ready.Guilds.FirstOrDefault().Value;
            Debug.Log(fermensEN ? $"DISCORD BOT CONNECTED TO ID{_guild.Id}." : $"DISCORD БОТ АВТОРИЗОВАН НА СЕРВЕРЕ ID{_guild.Id}.");
            });
        }

        private void CloseClient()
        {
            if (Client != null) Client.Disconnect();
        }

        private void CREATECHANNEL(string dsid, string text)
        {
            Snowflake ss = new Snowflake(dsid);
            if (!_guild.Members.Any(x => x.Value.User.Id == ss)) return;
            _guild.Members.First(x => x.Value.User.Id == ss).Value.User.SendDirectMessage(Client, new MessageCreate { Content = text });
        }

        private void SENDMESSAGE(string dsid, string text)
        {
            DiscordChannel channel = _guild.GetChannel(dsid);

            if (channel != null)
            {
                channel?.CreateMessage(Client, text);
            }
            else
            {
                CREATECHANNEL(dsid, text);
            }
        }

        public List<ActionRowComponent> CreateComponents(string button)
        {
            MessageComponentBuilder builder = new MessageComponentBuilder();
            builder.AddActionButton(ButtonStyle.Success, button, $"{Name}_{ConVar.Server.ip}_{ConVar.Server.port}", false);

            return builder.Build();
        }

        private readonly List<Regex> _regexTags = new List<Regex>
        {
            new Regex("<color=.+?>", RegexOptions.Compiled),
            new Regex("<size=.+?>", RegexOptions.Compiled)
        };

        private readonly List<string> _tags = new List<string>
        {
            "</color>",
            "`",
            "</size>",
            "<i>",
            "</i>",
            "<b>",
            "</b>"
        };

        private string STRIP(string original)
        {
            if (string.IsNullOrEmpty(original))
            {
                return string.Empty;
            }

            foreach (string tag in _tags)
            {
                original = original.Replace(tag, "");
            }

            foreach (Regex regexTag in _regexTags)
            {
                original = regexTag.Replace(original, "");
            }

            return original;
        }

        private DiscordChannel GetChannel(string id)
        {
            return _guild.Channels.FirstOrDefault(x => x.Key.ToString() == id).Value;
        }
        #endregion

        #region STORAGE
        string connect = "14.02.22:1406";

        //{fon}

        string FON = "";
        string MAIN = "";
        string UI = "";
        string IF2 = "";
        string IF2A = "";
        string BTN = "";
        string ER = "";
        string IBLOCK = "";
        string MAINH = "";
        string AG = "";
        string EXIT = "";
        string BACK = "";

        #region Data
        class Storage
        {
            public string vk;
            public string telegram;
            public ulong discord;
            public bool rustplus;
            public bool ingamerust { get; set; } = true;
        }

        #region fermens#8767
        #endregion

        private Storage GetStorage(ulong userid)
        {
            Storage storage;
            if (datas.TryGetValue(userid, out storage)) return storage;

            string useridstring = userid.ToString();
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile($"AAlertRaid/{useridstring}"))
            {
                storage = new Storage();
                datas.Add(userid, storage);
                return storage;
            }

            storage = Interface.Oxide.DataFileSystem.ReadObject<Storage>($"AAlertRaid/{useridstring}");
            datas.Add(userid, storage);
            return storage;
        }

        private void SaveStorage(BasePlayer player)
        {
            Storage storage;
            if (datas.TryGetValue(player.userID.Get(), out storage))
            {
                ServerMgr.Instance.StartCoroutine(Saving(player.UserIDString, storage));
            }
        }

        private IEnumerator Saving(string userid, Storage storage)
        {
            yield return new WaitForSeconds(1f);
            Interface.Oxide.DataFileSystem.WriteObject($"AAlertRaid/{userid}", storage);
        }

        Dictionary<ulong, Storage> datas = new Dictionary<ulong, Storage>();
        #endregion
        #endregion

        #region API TELEGRAM 
        private void GetRequestTelegram(string reciverID, string msg, BasePlayer player = null, bool accept = false) => webrequest.Enqueue($"https://api.telegram.org/bot" + config.telegram.token + "/sendMessage?chat_id=" + reciverID + "&text=" + Uri.EscapeDataString(msg), null, (code2, response2) => ServerMgr.Instance.StartCoroutine(GetCallbackTelegram(code2, response2, reciverID, player, accept)), this);

        private IEnumerator GetCallbackTelegram(int code, string response, string id, BasePlayer player = null, bool accept = false)
        {
            if (player == null || response == null) yield break;

            if (code == 401)
            {
                Debug.LogError("[AlertRaid] Telegram token not valid!");
            }
            else if (code == 200)
            {
                if (!response.Contains("error_code"))
                {
                    ALERT aLERT;
                    if (alerts.TryGetValue(player.userID.Get(), out aLERT))
                    {
                        aLERT.vkcodecooldown = DateTime.Now.AddMinutes(1);
                    }
                    else
                    {
                        alerts.Add(player.userID.Get(), new ALERT { telegramcodecooldown = DateTime.Now.AddMinutes(1) });
                    }

                    Storage storage = GetStorage(player.userID.Get());
                    storage.telegram = id;
                    SaveStorage(player);

                    write[player.userID.Get()] = "";
                    OpenMenu(player, false);
                }
            }
            else
            {
                SendError(player, "telegramuseridnotfound");
            }
            yield break;
        }
        #endregion

        #region API VK
        const string connects = "001.002.2022:1508";
        class ALERT
        {
            public DateTime gamecooldown;
            public DateTime rustpluscooldown;
            public DateTime vkcooldown;
            public DateTime discordcooldown;
            public DateTime vkcodecooldown;

            public DateTime telegramcooldown;
            public DateTime telegramcodecooldown;
        }

        private static Dictionary<ulong, ALERT> alerts = new Dictionary<ulong, ALERT>();
        class CODE
        {
            public string id;
            public ulong gameid;
        }

        private Dictionary<string, CODE> VKCODES = new Dictionary<string, CODE>();
        private Dictionary<ulong, string> DISCORDCODES = new Dictionary<ulong, string>();

        private void GetRequest(string reciverID, string msg, BasePlayer player = null, string num = null) => webrequest.Enqueue("https://api.vk.com/method/messages.send?domain=" + reciverID + "&message=" + Uri.EscapeDataString(msg) + "&v=5.81&access_token=" + config.vk.api, null, (code2, response2) => ServerMgr.Instance.StartCoroutine(GetCallbackVK(code2, response2, reciverID, player, num)), this);

        private void SendError(BasePlayer player, string key)
        {
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "ER");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", ER.Replace("{e0}", GetMessage(key, player.UserIDString)));
        }
        private IEnumerator GetCallbackVK(int code, string response, string id, BasePlayer player = null, string num = null)
        {
            if (player == null) yield break;
            if (response == null || code != 200)
            {
                ALERT alert;
                if (alerts.TryGetValue(player.userID.Get(), out alert)) alert.vkcooldown = DateTime.Now;
                Debug.Log("НЕ ПОЛУЧИЛОСЬ ОТПРАВИТЬ СООБЩЕНИЕ В ВК! => обнулили кд на отправку");
                yield break;
            }
            yield return new WaitForEndOfFrame();
            if (!response.Contains("error"))
            {
                ALERT aLERT;
                if (alerts.TryGetValue(player.userID.Get(), out aLERT))
                {
                    aLERT.vkcodecooldown = DateTime.Now.AddMinutes(1);
                }
                else
                {
                    alerts.Add(player.userID.Get(), new ALERT { vkcodecooldown = DateTime.Now.AddMinutes(1) });
                }
                if (VKCODES.ContainsKey(num)) VKCODES.Remove(num);
                VKCODES.Add(num, new CODE { gameid = player.userID.Get(), id = id });
                write[player.userID.Get()] = "";
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "ER");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "BTN");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", IBLOCK);
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", BTN.Replace("{text1}", GetMessage("{text1}", player.UserIDString)).Replace("{color}", "1 1 1 0.509804"));
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", IF2.Replace("{t3}", GetMessage("{t4}", player.UserIDString)).Replace("{coma}", "").Replace("{text2}", GetMessage("{text2}", player.UserIDString)));
            }
            else if (response.Contains("PrivateMessage"))
            {
                SendError(player, "rnprivate");
            }
            else if (response.Contains("ErrorSend"))
            {
                SendError(player, "rnerror");
            }
            else if (response.Contains("BlackList"))
            {
                SendError(player, "rnblack");
            }
            else
            {
                SendError(player, "rnerror2");
            }
            yield break;
        }
        #endregion

        #region COMMANDS
        private string perm = "discord fermens#8767";
        [PluginReference] Plugin BMenu;

        private void callcommandrn(BasePlayer player, string command, string[] arg)
        {
            OpenMenu(player);
        }

        private bool HasAcces(string id)
        {
            if (!config.needpermission) return true;
            return permission.UserHasPermission(id, perm);
        }

        private void OpenMenu(BasePlayer player, bool first = true)
        {
            if (!HasAcces(player.UserIDString))
            {
                player.ChatMessage(GetMessage("permission", player.UserIDString));
                return;
            }

            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "SubContent_UI");
            if (first)
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "Main_UI");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", FON);
                if (BMenu != null)
                {
                    BMenu.Call("DestroyProfileLayers", player);
                    BMenu.Call("SetPage", player.userID.Get(), "raid");
                    BMenu.Call("SetActivePButton", player, "raid");
                }
            }
            //0.5450981 1 0.6941177 0.509804
            //{\"name\":\"Main_UI\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.07843138 0.06666667 0.1098039 0.9490196\",\"material\":\"assets/content/ui/uibackgroundblur.mat\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", MAIN);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "E");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", EXIT.Replace("{t7}", GetMessage("{t7}", player.UserIDString)));
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", MAINH.Replace("{a0}", GetMessage("{amain}", player.UserIDString)));
            int num = 0;
            Storage storage = GetStorage(player.userID.Get());
            #region VK
            if (config.vk.enable && !string.IsNullOrEmpty(config.vk.api))
            {
                if (!string.IsNullOrEmpty(storage.vk)) AddElementUI(player, GetMessage("{element_vk}", player.UserIDString), config.ui.graybuttoncolor, GetMessage("{element_disable}", player.UserIDString), "raid.vkdelete", config.ui.uIMainMenu.abr_vk, config.ui.uIMainMenu.color_vk, num);
                else AddElementUI(player, GetMessage("{element_vk}", player.UserIDString), config.ui.greenbuttoncolor, GetMessage("{element_setup}", player.UserIDString), "raid.vkadd", config.ui.uIMainMenu.abr_vk, config.ui.uIMainMenu.color_vk, num);
                num++;
            }
            #endregion

            #region Telegram
            if (config.telegram.enable && !string.IsNullOrEmpty(config.telegram.token))
            {
                if (!string.IsNullOrEmpty(storage.telegram)) AddElementUI(player, GetMessage("{element_telegram}", player.UserIDString), config.ui.graybuttoncolor, GetMessage("{element_disable}", player.UserIDString), "raid.tgdelete", config.ui.uIMainMenu.abr_telegram, config.ui.uIMainMenu.color_telegram, num);
                else AddElementUI(player, GetMessage("{element_telegram}", player.UserIDString), config.ui.greenbuttoncolor, GetMessage("{element_setup}", player.UserIDString), "raid.tgadd", config.ui.uIMainMenu.abr_telegram, config.ui.uIMainMenu.color_telegram, num);
                num++;
            }
            #endregion

            #region Rust+
            if (config.rustplus.enable && !string.IsNullOrEmpty(App.serverid) && App.port > 0 && App.notifications)
            {
                if (!storage.rustplus) AddElementUI(player, GetMessage("{element_rustplus}", player.UserIDString), config.ui.greenbuttoncolor, GetMessage("{element_enable}", player.UserIDString), "raid.rustplus", config.ui.uIMainMenu.abr_rustplus, config.ui.uIMainMenu.color_rustplus, num);
                else AddElementUI(player, GetMessage("{element_rustplus}", player.UserIDString), config.ui.graybuttoncolor, GetMessage("{element_disable}", player.UserIDString), "raid.rustplus", config.ui.uIMainMenu.abr_rustplus, config.ui.uIMainMenu.color_rustplus, num);
                num++;
            }
            #endregion

            #region InGame
            if (config.ingame.enable)
            {
                if (!storage.ingamerust) AddElementUI(player, GetMessage("{element_ingame}", player.UserIDString), config.ui.greenbuttoncolor, GetMessage("{element_enable}", player.UserIDString), "raid.ingame", config.ui.uIMainMenu.abr_ui, config.ui.uIMainMenu.color_ui, num);
                else AddElementUI(player, GetMessage("{element_ingame}", player.UserIDString), config.ui.graybuttoncolor, GetMessage("{element_disable}", player.UserIDString), "raid.ingame", config.ui.uIMainMenu.abr_ui, config.ui.uIMainMenu.color_ui, num);
                num++;
            }
            #endregion

            #region Discord
            if (config.discord.enable && !string.IsNullOrEmpty(config.discord.token))
            {
                if (storage.discord == 0UL) AddElementUI(player, GetMessage("{element_discord}", player.UserIDString), config.ui.greenbuttoncolor, GetMessage("{element_setup}", player.UserIDString), "raid.discordadd", config.ui.uIMainMenu.abr_discord, config.ui.uIMainMenu.color_discord, num);
                else
                {
                    AddElementUI(player, GetMessage("{element_discord}", player.UserIDString), config.ui.graybuttoncolor, GetMessage("{element_disable}", player.UserIDString), "raid.discorddelete", config.ui.uIMainMenu.abr_discord, config.ui.uIMainMenu.color_discord, num);
                }
                num++;
            }
            #endregion
        }

        class C
        {
            public string min;
            public string max;
        }

        Dictionary<int, C> _caddele = new Dictionary<int, C>();

        private void AddElementUI(BasePlayer player, string name, string color, string button, string command, string ico, string icocolor, int num)
        {
            C ce;
            if (!_caddele.TryGetValue(num, out ce))
            {
                ce = new C();
                float start = 60f;
                float e = 30f;
                float p = 35f;
                float max = start - (num * p);
                ce.min = (max - e).ToString();
                ce.max = max.ToString();
                _caddele.Add(num, ce);
            }

            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", AG.Replace("{num}", num.ToString()).Replace("{id}", name).Replace("{coma}", command).Replace("{ico}", ico).Replace("{icocolor}", icocolor).Replace("{color}", color).Replace("{text1}", button).Replace("{min}", ce.min).Replace("{max}", ce.max));
        }

        Dictionary<ulong, string> write = new Dictionary<ulong, string>();

        [ConsoleCommand("raid.input")]
        void ccmdopeinput(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            string text = arg.HasArgs() ? string.Join(" ", arg.Args) : null;
            write[player.userID.Get()] = text;
        }

        private void SendError2(BasePlayer player, string key)
        {
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "BTN2");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", IF2A.Replace("{text2}", GetMessage(key, player.UserIDString)).Replace("{coma}", "").Replace("{color}", config.ui.redbuttoncolor));
            timer.Once(1f, () =>
            {
                if (!player.IsConnected) return;
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "BTN2");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", IF2A.Replace("{text2}", GetMessage("{text2}", player.UserIDString)).Replace("{coma}", "raid.accept").Replace("{color}", config.ui.greenbuttoncolor));
            });
        }

        #region InGame Comand
        [ConsoleCommand("raid.ingame")]
        void raplsgame(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            Storage storage = GetStorage(player.userID.Get());
            storage.ingamerust = !storage.ingamerust;
            SaveStorage(player);
            OpenMenu(player, false);
        }
        #endregion


        #region Rust+ Comand
        [ConsoleCommand("raid.rustplus")]
        void rapls(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            Storage storage = GetStorage(player.userID.Get());
            storage.rustplus = !storage.rustplus;
            SaveStorage(player);
            OpenMenu(player, false);
        }
        #endregion

        #region Discord command
        [ConsoleCommand("raid.discordadd")]
        void ccmdadiscoradd(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "SubContent_UI");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "E");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", MAIN);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", BACK.Replace("{t7}", GetMessage("{back}", player.UserIDString)));
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", UI.Replace("{t7}", GetMessage("{d7}", player.UserIDString)).Replace("{t6}", GetMessage("{d6}", player.UserIDString)).Replace("{t5}", GetMessage("{d5}", player.UserIDString)).Replace("{t4}", GetMessage("{d3}", player.UserIDString)).Replace("{t2}", config.discord.link).Replace("{t1}", GetMessage("{d1}", player.UserIDString)).Replace("{t0}", GetMessage("{d0}", player.UserIDString))); ;
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", BTN.Replace("{text1}", GetMessage("{text2}", player.UserIDString)).Replace("{coma}", "raid.acceptds").Replace("{color}", config.ui.greenbuttoncolor));
        }

        [ConsoleCommand("raid.acceptds")]
        void raidacceptds(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            //0.8901961 0.8901961 0.8901961 0.4156863
            //1 0.5450981 0.5450981 0.509804
            // raid.accept
            string text;
            if (!write.TryGetValue(player.userID.Get(), out text) || string.IsNullOrEmpty(text))
            {
                SendError(player, "rnnocode");
                return;
            }


            ulong user = DISCORDCODES.FirstOrDefault(x => x.Value == text).Key;
            if (user != 0UL)
            {
                Storage storage = GetStorage(player.userID.Get());
                storage.discord = user;
                SaveStorage(player);
                DISCORDCODES.Remove(user);
                OpenMenu(player, false);
            }
            else
            {
                SendError(player, "rncancel");
            }
        }

        [ConsoleCommand("raid.discorddelete")]
        void vdiscorddelete(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            Storage storage = GetStorage(player.userID.Get());
            storage.discord = 0;
            SaveStorage(player);
            OpenMenu(player, false);
        }
        #endregion

        #region Telegram COmand
        [ConsoleCommand("raid.tgdelete")]
        void rgdelete(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            Storage storage = GetStorage(player.userID.Get());
            storage.telegram = null;
            SaveStorage(player);
            OpenMenu(player, false);
        }

        [ConsoleCommand("raid.tgadd")]
        void ccmdtgadd(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "SubContent_UI");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "E");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", MAIN);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", BACK.Replace("{t7}", GetMessage("{back}", player.UserIDString)));
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", UI.Replace("{t7}", GetMessage("{teleg7}", player.UserIDString)).Replace("{t6}", GetMessage("{teleg6}", player.UserIDString)).Replace("{t5}", GetMessage("{teleg5}", player.UserIDString)).Replace("{t4}", GetMessage("{teleg3}", player.UserIDString)).Replace("{t2}", GetMessage("{teleg2}", player.UserIDString).Replace("{tag}", config.telegram.bottag)).Replace("{t1}", GetMessage("{teleg1}", player.UserIDString).Replace("{tag}", config.telegram.bottag)).Replace("{t0}", GetMessage("{teleg0}", player.UserIDString)));
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", BTN.Replace("{text1}", GetMessage("{text2}", player.UserIDString)).Replace("{coma}", "raid.accepttg").Replace("{color}", config.ui.greenbuttoncolor));
        }

        [ConsoleCommand("raid.accepttg")]
        void ccmdaccepttg(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            ALERT aLERT;
            if (alerts.TryGetValue(player.userID.Get(), out aLERT) && aLERT.telegramcodecooldown > DateTime.Now)
            {
                SendError(player, "rnaddcooldown");
                return;
            }

            string text;
            if (!write.TryGetValue(player.userID.Get(), out text) || string.IsNullOrEmpty(text))
            {
                SendError(player, "telegid");
                return;
            }

            GetRequestTelegram(text, GetMessage("telegramadd", player.UserIDString), player, true);
        }
        #endregion

        #region Vk COmand
        [ConsoleCommand("raid.vkdelete")]
        void vkdelete(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            Storage storage = GetStorage(player.userID.Get());
            storage.vk = null;
            SaveStorage(player);
            OpenMenu(player, false);
        }

        [ConsoleCommand("raid.vkadd")]
        void ccmdavkadd(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "SubContent_UI");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "E");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", MAIN);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", BACK.Replace("{t7}", GetMessage("{back}", player.UserIDString)));
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", UI.Replace("{t7}", GetMessage("{t7}", player.UserIDString)).Replace("{t6}", GetMessage("{t6}", player.UserIDString)).Replace("{t5}", GetMessage("{t5}", player.UserIDString)).Replace("{t4}", GetMessage("{t3}", player.UserIDString)).Replace("{t2}", config.vk.link).Replace("{t1}", GetMessage("{t1}", player.UserIDString)).Replace("{t0}", GetMessage("{t0}", player.UserIDString)));
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", BTN.Replace("{text1}", GetMessage("{text1}", player.UserIDString)).Replace("{coma}", "raid.send").Replace("{color}", config.ui.greenbuttoncolor));
        }

        [ConsoleCommand("raid.accept")]
        void ccmdaccept(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            //0.8901961 0.8901961 0.8901961 0.4156863
            //1 0.5450981 0.5450981 0.509804
            // raid.accept
            string text;
            if (!write.TryGetValue(player.userID.Get(), out text) || string.IsNullOrEmpty(text))
            {
                SendError2(player, "rnnocode");
                return;
            }

            CODE cODE;
            if (VKCODES.TryGetValue(text, out cODE) && cODE.gameid == player.userID.Get())
            {
                Storage storage = GetStorage(player.userID.Get());
                storage.vk = cODE.id;
                SaveStorage(player);
                VKCODES.Remove(text);
                OpenMenu(player, false);
            }
            else
            {
                SendError2(player, "rncancel");
            }
        }

        [ConsoleCommand("raid.send")]
        void ccmdopesendt(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            ALERT aLERT;
            if (alerts.TryGetValue(player.userID.Get(), out aLERT) && aLERT.vkcodecooldown > DateTime.Now)
            {
                SendError(player, "rnaddcooldown");
                return;
            }

            string text;
            if (!write.TryGetValue(player.userID.Get(), out text) || string.IsNullOrEmpty(text))
            {
                SendError(player, "null");
                return;
            }

            string vkid = text.ToLower().Replace("vk.com/", "").Replace("https://", "").Replace("http://", "");
            string num = RANDOMNUM();
            GetRequest(vkid, GetMessage("code", player.UserIDString).Replace("{code}", num), player, num);
        }
        #endregion

        private string RANDOMNUM() => UnityEngine.Random.Range(1000, 99999).ToString();
        #endregion

        #region OXIDE HOOKS
        private void Unload()
        {
            CloseClient();
            //CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = Network.Net.sv.connections }, null, "DestroyUI", "Main_UI");
        }

        private void OnServerInitialized()
        {
            string token = "040620222246fermens";
            string namer = "AlertRaid";
            ServerMgr.Instance.StartCoroutine(GetCallback());
        }

        #region WEBCONFIG

        public Dictionary<string, string> messagesEN = new Dictionary<string, string>
        {
            { "{t0}", "Join a group" },
            { "{element_vk}", "Vkontakte" },
            { "{element_telegram}", "Telegram" },
            { "{element_discord}", "Discord" },
            { "{element_rustplus}", "Rust+ app" },
            { "{element_ingame}", "Show in game" },
            { "{element_setup}", "Set up" },
            { "{element_disable}", "Disable" },
            { "{element_enable}", "Enable" },
            { "permission", "You do not have permission to use this command!" },
            { "{t1}", "Write any message to the group" },
            { "{t2}", "VK.COM/HAXLITE" },
            { "{t3}", "Link to your profile" },
            { "{t4}", "Check your mail in vk.com and enter the received code" },
            { "{t5}", "Enter text via Ctrl + V, so that commands binded on the keys that you press are not executed during input" },
            { "{t6}", "Set up alert" },
            { "{d0}", "Join the discord server" },
            { "{d1}", "Get the code in the 'Game Profile Integration' channel" },
            { "{d2}", "DISCORD.GG/CyFmAqGBED" },
            { "{d3}", "Enter the received code" },
            { "{d5}", "Enter text via Ctrl + V, so that commands binded on the keys that you press are not executed during input" },
            { "{d6}", "Set up alert" },
            { "{t7}", "EXIT" },
            { "{teleg0}", "Add the @userinfobot bot, press /start and copy the resulting Id" },
            { "{teleg1}", "Add bot {tag} and press /start" },
            { "{teleg2}", "{tag}" },
            { "{teleg3}", "Enter the copied Id" },
            { "{teleg5}", "Enter text via Ctrl + V, so that commands binded on the keys that you press are not executed during input" },
            { "{teleg6}", "Set up alert" },
            { "{back}", "BACK" },
            { "telegramuseridnotfound", "User id not found!" },
            { "telegramadd", "Now you will receive alert raid here" },
            { "telegid", "Enter the copied Id!" },
            { "{amain}", "Raid Alerts Control Panel" },
            { "{text1}", "Get code" },
            { "code", "Account verification code: {code}." },
            { "{text2}", "Confirm" },
            { "notallow", "Buy privilige to enable raid alerts in the store!" },
            { "rncancel", "Incorrect code!" },
            { "alertvk", "Attention! Player {name} destroyed {destroy} in square {quad}\nconnect {ip}" },
            { "alerttelegram", "Attention! Player {name} destroyed {destroy} in square {quad}\nconnect {ip}" },
            { "alertdiscord", "Attention! Player {name} destroyed {destroy} in square {quad}\nconnect {ip}" },
            { "alertrustplus", "Attention! Player {name} destroyed {destroy} in {quad}" },
            { "alertingame", "Player {name} destroyed {destroy} in {quad}" },
            { "null", "Enter the link to your profile!" },
            { "rnnocode", "No code provided!" },
            { "rnprivate", "Your privacy settings do not allow you to send a message." },
            { "rnerror", "The message could not be sent.\nPlease check the link is correct or try again later." },
            { "rnblack", "Unable to send message.\nYou have added a group to the black list or are not subscribed to it, if this is not the case, then simply write any message to the server group and try again." },
            { "rnerror2", "You have specified an incorrect link to your VK, if this is not the case, then simply write any message to the server group and try again." },
            { "rnaddcooldown", "You recently generated a verification code, please try again in a minute." },
            { "+wall Stone", "your stone wall" },
            { "+wall.low Stone", "your low stone wall" },
            { "+wall.frame Stone", "your stone wall frame" },
            { "+foundation Stone", "your stone foundation" },
            { "+roof Stone", "your stone roof" },
            { "+wall.doorway Stone", "your stone doorway" },
            { "+foundation.steps Stone", "your stone steps" },
            { "+block.stair.lshape Stone", "your stone L-stairs" },
            { "+block.stair.ushape Stone", "your stone U-ladder" },
            { "+foundation.triangle Stone", "your stone triangular foundation" },
            { "+wall.window Stone", "your stone window" },
            { "+wall.half Stone", "your stone half-wall" },
            { "+wall Metal", "your metal wall" },
            { "+wall.low Metal", "your metal low wall" },
            { "+wall.frame Metal", "your metal wall frame" },
            { "+foundation Metal", "your metal foundation" },
            { "+roof Metal", "your metal roof" },
            { "+wall.doorway Metal", "your metal doorway" },
            { "+foundation.steps Metal", "your metal stairs" },
            { "+block.stair.lshape Metal", "your metal L-ladder" },
            { "+block.stair.ushape Metal", "your metal U-ladder" },
            { "+foundation.triangle Metal", "your metal triangular foundation" },
            { "+wall.window Metal", "your metal window" },
            { "+wall.half Metal", "your metal half wall" },
            { "+wall TopTier", "your armored wall" },
            { "+wall.low TopTier", "your armored low wall" },
            { "+wall.frame TopTier", "your armored wall frame" },
            { "+foundation TopTier", "your armored foundation" },
            { "+roof TopTier", "your armored roof" },
            { "+wall.doorway TopTier", "your armored doorway" },
            { "+foundation.steps TopTier", "your armored steps" },
            { "+block.stair.lshape TopTier", "your armored L-ladder" },
            { "+block.stair.ushape TopTier", "your armored U-ladder" },
            { "+foundation.triangle TopTier", "your armored triangular foundation" },
            { "+wall.window TopTier", "your armored window" },
            { "+wall.half TopTier", "your armored half-wall" },
            { "+wall Wood", "your wooden wall" },
            { "+wall.low Wood", "your wooden low wall" },
            { "+wall.frame Wood", "your wooden wall frame" },
            { "+foundation Wood", "your wooden foundation" },
            { "+roof Wood", "your wooden roof" },
            { "+wall.doorway Wood", "your wooden doorway" },
            { "+foundation.steps Wood", "your wooden steps" },
            { "+block.stair.lshape Wood", "your wooden L-ladder" },
            { "+block.stair.ushape Wood", "your wooden U-ladder" },
            { "+foundation.triangle Wood", "your wooden triangular foundation" },
            { "+wall.window Wood", "your wooden window" },
            { "+door.hinged.metal", "" },
            { "+floor Wood", "your metal door" },
            { "+floor Metal", "your metal floor" },
            { "+door.hinged.wood", "your wooden door" },
            { "+floor Stone", "your stone floor" },
            { "+door.double.hinged.wood", "your double wooden door" },
            { "+door.double.hinged.metal", "your double metal door" },
            { "+shutter.wood.a", "your wooden shutters" },
            { "+wall.frame.garagedoor", "your garage door" },
            { "+wall.window.bars.wood", "your wood grate" },
            { "+floor.triangle Stone", "your stone triangular ceiling" },
            { "+wall.external.high.wood", "your high wooden gate" },
            { "+door.double.hinged.toptier", "your double armored door" },
            { "+floor.triangle Metal", "your metal triangular ceiling" },
            { "+wall.frame.netting", "your frame netting wall" },
            { "+door.hinged.toptier", "your armored door" },
            { "+shutter.metal.embrasure.a", "your metal shutters" },
            { "+wall.external.high.stone", "your external high stone wall" },
            { "+gates.external.high.stone", "your external high stone gates" },
            { "+floor.ladder.hatch", "your hatch with a ladder" },
            { "+floor.grill", "your floor grill" },
            { "+floor.triangle Wood", "your wooden triangular ceiling" },
            { "+floor.triangle TopTier", "your armored triangular ceiling" },
            { "+gates.external.high.wood", "your high wooden gate" },
            { "+wall.half Wood", "your wooden half wall" },
            { "+floor TopTier", "your triangular armored ceiling" },
            { "+wall.frame.cell", "your frame wall" },
            { "+wall.window.bars.metal", "your metal grill" },
            { "+wall.frame.fence", "your frame fence" },
            { "+shutter.metal.embrasure.b", "your metal shutter embrasure" },
            { "+wall.window.glass.reinforced", "your reinforced glass window" },
            { "+wall.frame.fence.gate", "your frame fence gate" },
            { "+floor.frame Stone", "your stone floor frame" },
            { "+wall.frame.cell.gate", "your wall frame cell gate" },
            { "+floor.frame Metal", "your metal floor frame" },
            { "+floor.frame Wood", "your wooden floor frame" },
            { "+wall.frame.shopfront", "your shopfront" },
            { "+wall.window.bars.toptier", "your window bars" },
            { "+autoturret_deployed", "your autoturret" },
            { "+sam_site_turret_deployed", "your sam site turret" },
            { "+ramp TopTier", "your armored ramp" },
            { "+floor.triangle.ladder.hatch", "your triangular hatch with stairs" },
            { "+block.stair.spiral Wood", "your wooden spiral staircase" },
            { "+ramp Metal", "your metal ramp" },
            { "+block.stair.spiral.triangle Wood", "your wooden triangular spiral staircase" },
            { "+block.stair.spiral.triangle Metal", "your metal triangular spiral staircase" },
            { "+block.stair.spiral Stone", "your stone spiral staircase" },
            { "+block.stair.spiral Metal", "your metal spiral staircase" },
            { "+floor.triangle.frame Stone", "your stone triangular ceiling frame" },
            { "+roof.triangle Metal", "your metal roof" },
            { "+floor.triangle.frame Metal", "your metal triangular ceiling frame" },
            { "+block.stair.spiral.triangle Stone", "your stone triangular spiral staircase" },
            { "+block.stair.spiral.triangle TopTier", "your armored triangular spiral staircase" },
            { "+ramp Wood", "your wooden ramp" },
            { "+roof.triangle Stone", "your stone roof" },
            { "+floor.triangle.frame TopTier", "your armored triangular ceiling frame" },
            { "+door.hinged.industrial.a", "your industrial door" },
            { "+roof.triangle TopTier", "your armored roof" },
            { "+ramp Stone", "your stone ramp" },
            { "+block.stair.spiral TopTier", "your armored spiral staircase" },
            { "+roof.triangle Wood", "your wooden roof" },
            { "+floor.frame TopTier", "your armored ceiling frame" }
        };

        public Dictionary<string, string> messagesRU = new Dictionary<string, string>
        {
            { "{t0}", "Вступить в группу" },
            { "{element_vk}", "Вконтакте" },
            { "{element_telegram}", "Телеграм" },
            { "{element_discord}", "Discord" },
            { "{element_rustplus}", "Приложение Rust+" },
            { "{element_ingame}", "Графическое отображение в игре" },
            { "{element_setup}", "Подключить" },
            { "{element_disable}", "Отключить" },
            { "{element_enable}", "Включить" },
            { "permission", "У вас нет разрешения для использования этой команды!" },
            { "{t1}", "Написать любое сообщение в группу" },
            { "{t2}", "VK.COM/HAXLITE" },
            { "{t3}", "Ссылка на ваш профиль" },
            { "{t4}", "Проверьте вашу почту в vk.com и введите полученый код" },
            { "{t5}", "Вводите текст через Ctrl+V, что бы во время ввода не выполнялись команды забинженые на клавиши, которые вы нажимаете" },
            { "{t6}", "Подключение оповещения о рейдах" },
            { "{d0}", "Присоединитесь к дискорд серверу" },
            { "{d1}", "Получить код в канале 'Интеграция с игровым профилем'" },
            { "{d2}", "DISCORD.GG/CyFmAqGBED" },
            { "{d3}", "Введите полученный код" },
            { "{d5}", "Вводите текст через Ctrl+V, что бы во время ввода не выполнялись команды забинженые на клавиши, которые вы нажимаете" },
            { "{d6}", "Подключение оповещения о рейдах" },
            { "{t7}", "ВЫХОД" },
            { "{teleg0}", "Добавьте бота @userinfobot, нажмите /start и скопируйте полученный Id" },
            { "{teleg1}", "Добавьте бота {tag} и нажать /start" },
            { "{teleg2}", "{tag}" },
            { "{teleg3}", "Введите скопированный Id" },
            { "{teleg5}", "Вводите текст через Ctrl+V, что бы во время ввода не выполнялись команды забинженые на клавиши, которые вы нажимаете" },
            { "{teleg6}", "Подключение оповещения о рейдах" },
            { "{back}", "НАЗАД" },
            { "telegramuseridnotfound", "User id не найден" },
            { "telegramadd", "Теперь вы будете получать рейд-оповещение здесь" },
            { "telegid", "Введите скопированный Id!" },
            { "{amain}", "Панель управления оповещений о рейде" },
            { "{text1}", "Получить код" },
            { "code", "Код для подтверджения аккаунта: {code}." },
            { "{text2}", "Подтвердить" },
            { "notallow", "Купите возможность подключить оповещения о рейде в магазине!" },
            { "rncancel", "Неверный код!" },
            { "alertvk", "Внимание! Игрок {name} разрушил {destroy} в квадрате {quad}\nconnect {ip}" },
            { "alerttelegram", "Внимание! Игрок {name} разрушил {destroy} в квадрате {quad}\nconnect {ip}" },
            { "alertdiscord", "Внимание! Игрок {name} разрушил {destroy} в квадрате {quad}\nconnect {ip}" },
            { "alertrustplus", "Внимание! Игрок {name} разрушил {destroy} в квадрате {quad}" },
            { "alertingame", "Игрок {name} разрушил {destroy} в квадрате {quad}" },
            { "null", "Введите ссылку на ваш профиль!" },
            { "rnnocode", "Не указали код!" },
            { "rnprivate", "Ваши настройки приватности не позволяют отправить вам сообщение." },
            { "rnerror", "Невозможно отправить сообщение.\nПроверьте правильность ссылки или повторите попытку позже." },
            { "rnblack", "Невозможно отправить сообщение.\nВы добавили группу в черный список или не подписаны на нее, если это не так, то просто напишите в группу сервера любое сообщение и попробуйте еще раз." },
            { "rnerror2", "Вы указали неверную ссылку на ваш Вк, если это не так, то просто напишите в группу сервера любое сообщение и попробуйте еще раз." },
            { "rnaddcooldown", "Вы недавно создавали код для подтверждения, попробуйте еще раз через минуту." },
            { "+wall Stone", "вашу каменную стену" },
            { "+wall.low Stone", "вашу каменную низкую стену" },
            { "+wall.frame Stone", "ваш каменный настенный каркас" },
            { "+foundation Stone", "ваш каменный фундамент" },
            { "+roof Stone", "вашу каменную крышу" },
            { "+wall.doorway Stone", "ваш каменный дверной проём" },
            { "+foundation.steps Stone", "ваши каменные ступеньки" },
            { "+block.stair.lshape Stone", "вашу каменную L-лестницу" },
            { "+block.stair.ushape Stone", "вашу каменную U-лестницу" },
            { "+foundation.triangle Stone", "ваш каменный треугольный фундамент" },
            { "+wall.window Stone", "ваше каменное окно" },
            { "+wall.half Stone", "вашу каменную полустену" },
            { "+wall Metal", "вашу металлическую стену" },
            { "+wall.low Metal", "вашу металлическую низкую стену" },
            { "+wall.frame Metal", "ваш металлический настенный каркас" },
            { "+foundation Metal", "ваш металлический фундамент" },
            { "+roof Metal", "вашу металлическую крышу" },
            { "+wall.doorway Metal", "ваш металлический дверной проём" },
            { "+foundation.steps Metal", "ваши металлические ступеньки" },
            { "+block.stair.lshape Metal", "вашу металлическую L-лестницу" },
            { "+block.stair.ushape Metal", "вашу металлическую U-лестницу" },
            { "+foundation.triangle Metal", "ваш металлический треугольный фундамент" },
            { "+wall.window Metal", "ваше металлическое окно" },
            { "+wall.half Metal", "вашу металлическую полустену" },
            { "+wall TopTier", "вашу бронированную стену" },
            { "+wall.low TopTier", "вашу бронированную низкую стену" },
            { "+wall.frame TopTier", "ваш бронированный настенный каркас" },
            { "+foundation TopTier", "ваш бронированный фундамент" },
            { "+roof TopTier", "вашу бронированную крышу" },
            { "+wall.doorway TopTier", "ваш бронированный дверной проём" },
            { "+foundation.steps TopTier", "ваши бронированные ступеньки" },
            { "+block.stair.lshape TopTier", "вашу бронированную L-лестницу" },
            { "+block.stair.ushape TopTier", "вашу бронированную U-лестницу" },
            { "+foundation.triangle TopTier", "ваш бронированный треугольный фундамент" },
            { "+wall.window TopTier", "ваше бронированное окно" },
            { "+wall.half TopTier", "вашу бронированную полустену" },
            { "+wall Wood", "вашу деревянную стену" },
            { "+wall.low Wood", "вашу деревянную низкую стену" },
            { "+wall.frame Wood", "ваш деревянный настенный каркас" },
            { "+foundation Wood", "ваш деревянный фундамент" },
            { "+roof Wood", "вашу деревянную крышу" },
            { "+wall.doorway Wood", "ваш деревянный дверной проём" },
            { "+foundation.steps Wood", "ваши деревянные ступеньки" },
            { "+block.stair.lshape Wood", "вашу деревянную L-лестницу" },
            { "+block.stair.ushape Wood", "вашу деревянную U-лестницу" },
            { "+foundation.triangle Wood", "ваш деревянный треугольный фундамент" },
            { "+wall.window Wood", "ваше деревянное окно" },
            { "+door.hinged.metal", "вашу металлическую дверь" },
            { "+floor Wood", "ваш деревянный пол" },
            { "+floor Metal", "ваш металлический пол" },
            { "+door.hinged.wood", "вашу деревянную дверь" },
            { "+floor Stone", "ваш каменный пол" },
            { "+door.double.hinged.wood", "вашу двойную деревянную дверь" },
            { "+door.double.hinged.metal", "вашу двойную металлическую дверь" },
            { "+shutter.wood.a", "ваши деревянные ставни" },
            { "+wall.frame.garagedoor", "вашу гаражную дверь" },
            { "+wall.window.bars.wood", "вашу деревянную решетку" },
            { "+floor.triangle Stone", "ваш каменный треугольный потолок" },
            { "+wall.external.high.wood", "ваши высокие деревянные ворота" },
            { "+door.double.hinged.toptier", "вашу двойную бронированную дверь" },
            { "+floor.triangle Metal", "ваш металлический треугольный потолок" },
            { "+wall.frame.netting", "вашу сетчатую стену" },
            { "+door.hinged.toptier", "вашу бронированную дверь" },
            { "+shutter.metal.embrasure.a", "ваши металлические ставни" },
            { "+wall.external.high.stone", "вашу высокую каменную стену" },
            { "+gates.external.high.stone", "ваши высокие каменные ворота" },
            { "+floor.ladder.hatch", "ваш люк с лестницей" },
            { "+floor.grill", "ваш решетчатый настил" },
            { "+floor.triangle Wood", "ваш деревянный треугольный потолок" },
            { "+floor.triangle TopTier", "ваш бронированный треугольный потолок" },
            { "+gates.external.high.wood", "ваши высокие деревянные ворота" },
            { "+wall.half Wood", "вашу деревянную полустену" },
            { "+floor TopTier", "ваш треугольный бронированный потолок" },
            { "+wall.frame.cell", "вашу тюремную стену" },
            { "+wall.window.bars.metal", "вашу металлическую решетку" },
            { "+wall.frame.fence", "ваш сетчатый забор" },
            { "+shutter.metal.embrasure.b", "вашу металлическую бойницу" },
            { "+wall.window.glass.reinforced", "ваше окно из укрепленного стекла" },
            { "+wall.frame.fence.gate", "вашу сетчатую дверь" },
            { "+floor.frame Stone", "ваш каменный потолочный каркас" },
            { "+wall.frame.cell.gate", "вашу тюремную решетку" },
            { "+floor.frame Metal", "ваш металический потолочный каркас" },
            { "+floor.frame Wood", "ваш деревянный потолочный каркас" },
            { "+wall.frame.shopfront", "вашу витрину" },
            { "+wall.window.bars.toptier", "ваши оконные решетки" },
            { "+autoturret_deployed", "вашу турель" },
            { "+sam_site_turret_deployed", "вашу зенитную турель" },
            { "+ramp TopTier", "вашу бронированную рампу" },
            { "+floor.triangle.ladder.hatch", "ваш треугольный люк с лестницей" },
            { "+block.stair.spiral Wood", "вашу деревянную спиральную лестницу" },
            { "+ramp Metal", "вашу металлическую рампу" },
            { "+block.stair.spiral.triangle Wood", "вашу деревянную треугольную спиральную лестницу" },
            { "+block.stair.spiral.triangle Metal", "вашу металлическую треугольную спиральную лестницу" },
            { "+block.stair.spiral Stone", "вашу каменную спиральную лестницу" },
            { "+block.stair.spiral Metal", "вашу металлическую спиральную лестницу" },
            { "+floor.triangle.frame Stone", "ваш каменный треугольный потолочный каркас" },
            { "+roof.triangle Metal", "вашу металлическую крышу" },
            { "+floor.triangle.frame Metal", "ваш металлический треугольный потолочный каркас" },
            { "+block.stair.spiral.triangle Stone", "вашу каменную треугольную спиральную лестницу" },
            { "+block.stair.spiral.triangle TopTier", "вашу бронированную треугольную спиральную лестницу" },
            { "+ramp Wood", "вашу деревянную рампу" },
            { "+roof.triangle Stone", "вашу каменную крышу" },
            { "+floor.triangle.frame TopTier", "ваш бронированный треугольный потолочный каркас" },
            { "+door.hinged.industrial.a", "вашу промышленную дверь" },
            { "+roof.triangle TopTier", "вашу бронированную крышу" },
            { "+ramp Stone", "вашу каменную  рампу" },
            { "+block.stair.spiral TopTier", "вашу бронированную спиральную лестницу" },
            { "+roof.triangle Wood", "вашу деревянную крышу" },
            { "+floor.frame TopTier", "ваш бронированный потолочный каркас" }
        };

        IEnumerator GetCallback()
        {
            if (config.discord.link == null)
            {
                config.discord.link = GetMessage("{t2}", "0");
                if (config.vk.link == "{t2}") config.vk.link = "VK.COM/YOURLINK";
            }

            if (config.vk.link == null)
            {
                config.vk.link = GetMessage("{d2}", "0");
                if (config.vk.link == "{d2}") config.vk.link = "DISCORD.GG/YOURLINK";
            }
            SaveConfig();

            lang.RegisterMessages(messagesEN, this, "en");
            lang.RegisterMessages(messagesRU, this, "ru");
            perm = Name + ".use";
            permission.RegisterPermission(perm, this);

            FON = "[{\"name\":\"Main_UI\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"NeedsCursor\"},{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{color}\",\"material\":\"assets/content/ui/uibackgroundblur.mat\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]".Replace("{color}", config.ui.background);
            MAIN = "[{\"name\":\"SubContent_UI\",\"parent\":\"Main_UI\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"color\":\"0 0 0 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0.6\",\"anchormax\":\"0.5 0.6\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]";
            UI = "[{\"name\":\"IF\",\"parent\":\"SubContent_UI\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{rectangularcolor}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0.5\",\"anchormax\":\"0.5 0.5\",\"offsetmin\":\"-120 -100\",\"offsetmax\":\"120 -70\"}]},{\"name\":\"D\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 1\"}]},{\"name\":\"U\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 -1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"L\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 0\"}]},{\"name\":\"R\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"-1 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"I\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.InputField\",\"align\":\"MiddleLeft\",\"color\":\"{colorcontainertext}\",\"command\":\"raid.input\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 0\",\"offsetmax\":\"-5 0\"}]},{\"name\":\"L1\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-40 17\",\"offsetmax\":\"-5 18\"}]},{\"name\":\"L4\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-40 84\",\"offsetmax\":\"-5 85\"}]},{\"name\":\"P1\",\"parent\":\"L4\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{rectangularcolor}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 -15\",\"offsetmax\":\"245 15\"}]},{\"name\":\"D\",\"parent\":\"P1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 1\"}]},{\"name\":\"U\",\"parent\":\"P1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 -1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"L\",\"parent\":\"P1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 0\"}]},{\"name\":\"R\",\"parent\":\"P1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"-1 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"T\",\"parent\":\"P1\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t2}\",\"align\":\"MiddleCenter\",\"color\":\"{colorcontainertext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 0\",\"offsetmax\":\"-5 0\"}]},{\"name\":\"L5\",\"parent\":\"L4\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 35\"}]},{\"name\":\"L6\",\"parent\":\"L5\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"35 1\"}]},{\"name\":\"T\",\"parent\":\"L6\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t1}\", \"color\":\"{colortext}\",\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"MiddleLeft\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 -10\",\"offsetmax\":\"720 10\"}]},{\"name\":\"L7\",\"parent\":\"L5\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 35\"}]},{\"name\":\"L8\",\"parent\":\"L7\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"35 1\"}]},{\"name\":\"T\",\"parent\":\"L8\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t0}\", \"color\":\"{colortext}\",\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"MiddleLeft\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 -10\",\"offsetmax\":\"720 10\"}]},{\"name\":\"H\",\"parent\":\"L7\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t6}\", \"color\":\"{colorheader}\",\"fontSize\":24},{\"type\":\"RectTransform\",\"anchormin\":\"40 1\",\"anchormax\":\"720 1\",\"offsetmin\":\"0 20\",\"offsetmax\":\"0 60\"}]},{\"name\":\"L2\",\"parent\":\"L1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 35\"}]},{\"name\":\"L3\",\"parent\":\"L2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"35 1\"}]},{\"name\":\"T1\",\"parent\":\"L3\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t4}\",\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"MiddleLeft\",\"color\":\"{colortext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 -10\",\"offsetmax\":\"720 10\"}]},{\"name\":\"DESC\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t5}\",\"font\":\"RobotoCondensed-Regular.ttf\",\"color\":\"{colordesctext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"-160 -200\",\"offsetmax\":\"250 -100\"}]}]".Replace("{colorline}", config.ui.stripcolor).Replace("{rectangularcolor}", config.ui.rectangularcolor).Replace("{colordesctext}", config.ui.hintstextcolor).Replace("{colortext}", config.ui.textcolor).Replace("{colorcontainertext}", config.ui.rectangulartextcolor).Replace("{colorheader}", config.ui.headertextcolor).Replace("{colordesctext}", config.ui.hintstextcolor);
            IF2 = "[{\"name\":\"IF2\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{rectangularcolor}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"-120 -70\",\"offsetmax\":\"120 -40\"}]},{\"name\":\"D\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 1\"}]},{\"name\":\"U\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 -1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"L\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 0\"}]},{\"name\":\"R\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"-1 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"I\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.InputField\",\"command\":\"raid.input\",\"align\":\"MiddleLeft\",\"color\":\"{colorcontainertext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 0\",\"offsetmax\":\"-5 0\"}]},{\"name\":\"BTN2\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"raid.accept\",\"color\":\"{greenbuttoncolor}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 -30\",\"offsetmax\":\"125 0\"}]},{\"name\":\"T\",\"parent\":\"BTN2\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text2}\",\"align\":\"MiddleCenter\",\"color\":\"{buttoncolortext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"L1\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-40 17\",\"offsetmax\":\"-5 18\"}]},{\"name\":\"L2\",\"parent\":\"L1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 35\"}]},{\"name\":\"L3\",\"parent\":\"L2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"35 1\"}]},{\"name\":\"T1\",\"parent\":\"L3\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t3}\",\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"MiddleLeft\",\"color\":\"{colortext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 -10\",\"offsetmax\":\"500 10\"}]}]".Replace("{rectangularcolor}", config.ui.rectangularcolor).Replace("{colorline}", config.ui.stripcolor).Replace("{colorcontainertext}", config.ui.rectangulartextcolor).Replace("{colortext}", config.ui.textcolor).Replace("{greenbuttoncolor}", config.ui.greenbuttoncolor).Replace("{buttoncolortext}", config.ui.buttoncolortext);
            IF2A = "[{\"name\":\"BTN2\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"{coma}\",\"color\":\"{color}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 -30\",\"offsetmax\":\"125 0\"}]},{\"name\":\"T\",\"parent\":\"BTN2\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text2}\",\"align\":\"MiddleCenter\",\"color\":\"{buttoncolortext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]".Replace("{buttoncolortext}", config.ui.buttoncolortext);
            BTN = "[{\"name\":\"BTN\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"{coma}\",\"color\":\"{color}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 -30\",\"offsetmax\":\"125 0\"}]},{\"name\":\"T\",\"parent\":\"BTN\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text1}\",\"align\":\"MiddleCenter\",\"color\":\"{buttoncolortext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]".Replace("{buttoncolortext}", config.ui.buttoncolortext);
            ER = "[{\"name\":\"ER\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{e0}\",\"fontSize\":16,\"font\":\"RobotoCondensed-Regular.ttf\",\"color\":\"{errortextcolor}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0.5\",\"anchormax\":\"0.5 0.5\",\"offsetmin\":\"-160 -95\",\"offsetmax\":\"245 -35\"}]}]".Replace("{errortextcolor}", config.ui.errortextcolor);
            MAINH = "[{\"name\":\"AG\",\"parent\":\"SubContent_UI\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{a0}\",\"fontSize\":24},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-155 60\",\"offsetmax\":\"500 115\"}]}]";
            IBLOCK = "[{\"name\":\"E\",\"parent\":\"Main_UI\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"close\":\"Main_UI\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"-300 -100\",\"offsetmax\":\"-150 -50\"}]},{\"name\":\"ET\",\"parent\":\"E\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t7}\",\"fontSize\":30,\"align\":\"MiddleCenter\",\"color\":\"{colortextexit}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"10 0\",\"offsetmax\":\"0 0\"}]}]";
            BACK = "[{\"name\":\"E\",\"parent\":\"Main_UI\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"chat.say /raid\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"-300 -100\",\"offsetmax\":\"-150 -50\"}]},{\"name\":\"ET\",\"parent\":\"E\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t7}\",\"fontSize\":30,\"align\":\"MiddleCenter\",\"color\":\"{colortextexit}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"10 0\",\"offsetmax\":\"0 0\"}]}]".Replace("{colortextexit}", config.ui.colortextexit);
            EXIT = "[{\"name\":\"E\",\"parent\":\"Main_UI\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"close\":\"Main_UI\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"-300 -100\",\"offsetmax\":\"-150 -50\"}]},{\"name\":\"ET\",\"parent\":\"E\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t7}\",\"fontSize\":30,\"align\":\"MiddleCenter\",\"color\":\"{colortextexit}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"10 0\",\"offsetmax\":\"0 0\"}]}]".Replace("{colortextexit}", config.ui.colortextexit);
            AG = "[{\"name\":\"AGG{num}\",\"parent\":\"SubContent_UI\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{rectangularcolor}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-120 {min}\",\"offsetmax\":\"120 {max}\"}]},{\"name\":\"D\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 1\"}]},{\"name\":\"R\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"-1 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"U\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 -1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"L\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 0\"}]},{\"name\":\"AT\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{id}\",\"align\":\"MiddleLeft\",\"color\":\"{colorcontainertext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 0\",\"offsetmax\":\"-5 0\"}]},{\"name\":\"BTN{num}\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"{coma}\",\"color\":\"{color}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 0\",\"offsetmax\":\"125 30\"}]},{\"name\":\"T\",\"parent\":\"BTN{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text1}\",\"align\":\"MiddleCenter\",\"color\":\"{buttoncolortext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"AL\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{icocolor}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"-35 -30\",\"offsetmax\":\"-5 0\"}]},{\"name\":\"ALT\",\"parent\":\"AL\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{ico}\",\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]".Replace("{colorline}", config.ui.stripcolor).Replace("{rectangularcolor}", config.ui.rectangularcolor).Replace("{colorcontainertext}", config.ui.rectangulartextcolor).Replace("{buttoncolortext}", config.ui.buttoncolortext);

            if (!string.IsNullOrEmpty(config.discord.token)) CreateClient();
            else Debug.LogError(fermensEN ? "AALERTRAID - TOKEN FOR DISCORD BOT IS NULL!" : "AALERTRAID - Не указан токен для Discord бота!");

            connect = ConVar.Server.ip + ":" + ConVar.Server.port;
            CreateSpawnGrid();

            Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddChatCommand("raid", this, "callcommandrn");

            Debug.Log(">>AlertRaid<< OK!");

            yield break;
        }
        #endregion

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null || entity == null) return;
            BasePlayer player = info.InitiatorPlayer;
            if (player == null) return;
            if (entity is BuildingBlock)
            {
                int tt = (int)(entity as BuildingBlock).grade;
                if (tt <= 0) return;
                ServerMgr.Instance.StartCoroutine(Alerting(entity, player, tt));
            }
            else if (config.extralist && (entity is DecayEntity || entity is IOEntity) || entity is AnimatedBuildingBlock || entity is SamSite || entity is AutoTurret || config.spisok.Contains(entity.ShortPrefabName))
            {
                ServerMgr.Instance.StartCoroutine(Alerting(entity, player));
            }
        }
        #endregion

        #region FUNCTIONS

        private IEnumerator Alerting(BaseCombatEntity entity, BasePlayer player, int tt = 0)
        {
            Vector3 position = entity.transform.position;
            string dname = entity.ShortPrefabName;

            if (tt == 1) dname += " Wood";
            else if (tt == 2) dname += " Stone";
            else if (tt == 3) dname += " Metal";
            else if (tt == 4) dname += " TopTier";

            BuildingPrivlidge buildingPrivlidge = entity is BuildingPrivlidge ? entity as BuildingPrivlidge : entity.GetBuildingPrivilege(entity.WorldSpaceBounds());
            if (buildingPrivlidge == null) yield break;
            if (!buildingPrivlidge.AnyAuthed()) yield break;

            var list = buildingPrivlidge.authorizedPlayers.ToList();

            yield return CoroutineEx.waitForSeconds(0.5f);

            string name = player.displayName;
            string attackerid = player.UserIDString;
            string quad = GetNameGrid(position);
            string connect = ConVar.Server.ip + ":" + ConVar.Server.port;

            string key = "+" + dname;

            foreach (var z in list)
            {
                string destroy = GetMessage(key, z.userid.ToString());
                if (destroy == key) destroy = entity.ShortPrefabName.Replace(".deployed", "");

                ALERTPLAYER(z.userid, name, quad, connect, destroy, attackerid);

                yield return CoroutineEx.waitForEndOfFrame;
            }
        }

        List<ulong> block = new List<ulong>();
        private void ALERTPLAYER(ulong ID, string name, string quad, string connect, string destroy, string attackerid)
        {
            string IDstring = ID.ToString();

            if (!HasAcces(IDstring)) return;

            ALERT alert;
            if (!alerts.TryGetValue(ID, out alert))
            {
                alerts.Add(ID, new ALERT());
                alert = alerts[ID];
            }
            Storage storage = GetStorage(ID);

            #region ОПОВЕЩЕНИЕ В ВК
            if (config.vk.enable && !string.IsNullOrEmpty(config.vk.api) && alert.vkcooldown < DateTime.Now)
            {
                if (!string.IsNullOrEmpty(storage.vk))
                {
                    GetRequest(storage.vk, GetMessage("alertvk", IDstring).Replace("{ip}", connect).Replace("{steamid}", attackerid).Replace("{name}", name).Replace("{destroy}", destroy).Replace("{quad}", quad).Replace("{servername}", config.servername));
                    alert.vkcooldown = DateTime.Now.AddSeconds(config.vk.cooldown);
                }
            }
            #endregion

            #region ОПОВЕЩЕНИЕ В ТЕЛЕГРАМ
            if (config.telegram.enable && !string.IsNullOrEmpty(config.telegram.token) && alert.telegramcooldown < DateTime.Now)
            {
                if (!string.IsNullOrEmpty(storage.telegram))
                {
                    GetRequestTelegram(storage.telegram, GetMessage("alerttelegram", IDstring).Replace("{ip}", connect).Replace("{steamid}", attackerid).Replace("{name}", name).Replace("{destroy}", destroy).Replace("{quad}", quad).Replace("{servername}", config.servername));
                    alert.telegramcooldown = DateTime.Now.AddSeconds(config.telegram.cooldown);
                }
            }
            #endregion

            #region ОПОВЕЩЕНИЕ В RUST+
            if (!string.IsNullOrEmpty(App.serverid) && App.port > 0 && App.notifications && storage.rustplus && config.rustplus.enable && alert.rustpluscooldown < DateTime.Now)
            {
                NotificationList.SendNotificationTo(ID, NotificationChannel.SmartAlarm, GetMessage("alertrustplus", IDstring).Replace("{steamid}", attackerid).Replace("{ip}", connect).Replace("{name}", name).Replace("{destroy}", destroy).Replace("{quad}", quad).Replace("{servername}", config.servername), config.servername, Util.GetServerPairingData());
                alert.rustpluscooldown = DateTime.Now.AddSeconds(config.rustplus.cooldown);
            }
            #endregion

            #region ОПОВЕЩЕНИЕ В DISCORD
            if (config.discord.enable && !block.Contains(ID) && !string.IsNullOrEmpty(config.discord.token) && alert.discordcooldown < DateTime.Now)
            {
                if (storage.discord != 0UL)
                {
                    Snowflake ss = new Snowflake(storage.discord);
                    if (!_guild.Members.Any(x => x.Value.User.Id == ss))
                        return;

                    var user = _guild.Members.First(x => x.Value.User.Id == ss).Value.User;
                    try
                    {
                        // Формируем сообщение
                        string messageContent = GetMessage("alertdiscord", IDstring)
                            .Replace("{steamid}", attackerid)
                            .Replace("{ip}", connect)
                            .Replace("{name}", name)
                            .Replace("{destroy}", destroy)
                            .Replace("{quad}", quad)
                            .Replace("{servername}", config.servername);

                        // Отправляем сообщение
                        user.SendDirectMessage(Client, new MessageCreate
                        {
                            Content = messageContent
                        });

                        // Устанавливаем cooldown
                        alert.discordcooldown = DateTime.Now.AddSeconds(config.discord.cooldown);
                    }
                    catch (Exception ex)
                    {
                        // Обрабатываем известную ошибку 50007 (не удается отправить сообщение пользователю)
                        if (ex.Message.Contains("50007"))
                        {
                            block.Add(ID);
                        }
                        else
                        {
                            // Логируем любую другую ошибку для отладки
                            Puts($"Ошибка при отправке сообщения: {ex.Message}");
                        }
                    }
                }
            }
            #endregion

            #region ОПОВЕЩЕНИЕ В ИГРЕ
            if (storage.ingamerust && config.ingame.enable && alert.gamecooldown < DateTime.Now)
            {
                BasePlayer player = BasePlayer.FindByID(ID);
                if (player != null && player.IsConnected)
                {
                    Timer ss;
                    if (timal.TryGetValue(player.userID.Get(), out ss))
                    {
                        if (!ss.Destroyed) ss.Destroy();
                    }
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "UIA");
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", config.ingame.UI.Replace("{text}", GetMessage("alertingame", IDstring).Replace("{steamid}", attackerid).Replace("{ip}", connect).Replace("{name}", name).Replace("{destroy}", destroy).Replace("{quad}", quad).Replace("{servername}", config.servername)));
                    if (!string.IsNullOrEmpty(config.ingame.effect)) EffectNetwork.Send(new Effect(config.ingame.effect, player, 0, Vector3.up, Vector3.zero) { scale = 1f }, player.net.connection);
                    timal[player.userID.Get()] = timer.Once(config.ingame.destroy, () => CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "UIA"));
                    alert.gamecooldown = DateTime.Now.AddSeconds(config.ingame.cooldown);
                }
            }
            #endregion
        }

        private Dictionary<ulong, Timer> timal = new Dictionary<ulong, Timer>();
        #endregion

        #region Lang
        private string GetMessage(string key, string userId) => lang.GetMessage(key, this, userId);
        #endregion

        #region GRID
        private static Dictionary<string, Vector3> Grids = new Dictionary<string, Vector3>();
        private void CreateSpawnGrid()
        {
            Grids.Clear();
            var worldSize = (ConVar.Server.worldsize);
            float offset = worldSize / 2;
            var gridWidth = (0.0066666666666667f * worldSize);
            float step = worldSize / gridWidth;

            string start = "";

            char letter = 'A';
            int number = 0;

            for (float zz = offset; zz > -offset; zz -= step)
            {
                for (float xx = -offset; xx < offset; xx += step)
                {
                    Grids.Add($"{start}{letter}{number}", new Vector3(xx - 55f, 0, zz + 20f));
                    if (letter.ToString().ToUpper() == "Z")
                    {
                        start = "A";
                        letter = 'A';
                    }
                    else
                    {
                        letter = (char)(((int)letter) + 1);
                    }


                }
                number++;
                start = "";
                letter = 'A';
            }
        }

        private string GetNameGrid(Vector3 pos) => Grids.Where(x => x.Value.x < pos.x && x.Value.x + 150f > pos.x && x.Value.z > pos.z && x.Value.z - 150f < pos.z).FirstOrDefault().Key;
        #endregion
    }
}
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */