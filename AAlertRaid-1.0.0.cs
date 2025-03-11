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
using Oxide.Ext.Discord.Attributes;
using Oxide.Ext.Discord.Entities.Messages;
using Oxide.Ext.Discord.Entities.Channels;
using Oxide.Ext.Discord.Entities.Guilds;
using Oxide.Ext.Discord.Constants;
using Oxide.Ext.Discord.Entities.Gatway;
using Oxide.Ext.Discord.Entities.Gatway.Events;
using Oxide.Ext.Discord.Entities.Messages.Embeds;
using Oxide.Ext.Discord.Entities.Permissions;
using Oxide.Ext.Discord.Entities.Activities;
using Oxide.Ext.Discord.Entities;
using System.Text.RegularExpressions;
using Oxide.Ext.Discord.Builders.MessageComponents;
using Oxide.Ext.Discord.Entities.Interactions.MessageComponents;
using Oxide.Ext.Discord.Entities.Interactions;
using Oxide.Ext.Discord.Entities.Users;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("AAlertRaid", "https://discord.gg/9vyTXsJyKR", "1.0.0")]
    public class AAlertRaid : RustPlugin
    {
        #region CONFIG
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
            [JsonProperty("Включить?")]
            public bool enable;

            [JsonProperty("API от группы")]
            public string api;

            [JsonProperty("Кд на отправку")]
            public float cooldown;
        }
        class RUSTPLUS
        {
            [JsonProperty("Включить?")]
            public bool enable;

            [JsonProperty("Кд на отправку")]
            public float cooldown;
        }
        class INGAME
        {
            [JsonProperty("Включить?")]
            public bool enable;

            [JsonProperty("Кд на отправку")]
            public float cooldown;

            [JsonProperty("Эффект при получении уведомления")]
            public string effect;

            [JsonProperty("Время, через которое пропадает UI [секунды]")]
            public float destroy;

            [JsonProperty("UI")]
            public string UI;
        }

        class DISCORD
        {
            [JsonProperty("Включить?")]
            public bool enable;

            [JsonProperty("Кд на отправку")]
            public float cooldown;

            [JsonProperty("Токен бота (https://discordapp.com/developers/applications)")]
            public string token;

            [JsonProperty("ID канала, гле игрок будет брать код, для подтверджения профиля")]
            public string channel;

            [JsonProperty("Дискорд канал с получением кода - текст")]
            public string channeltext;

            [JsonProperty("Дискорд канал с получением кода - цвет линии слева (https://gist.github.com/thomasbnt/b6f455e2c7d743b796917fa3c205f812#file-code_colors_discordjs-md)")]
            public uint channelcolor;

            [JsonProperty("Дискорд канал с получением кода - кнопка")]
            public string channelbutton;

            [JsonProperty("Дискорд канал с получением кода - ответ")]
            public string channelex;

            [JsonProperty("Дискорд канал с получением кода - ID сообщения (не трогаем! сам заполнится!)")]
            public string channelmessageid;
        }

        private class PluginConfig
        {
            [JsonProperty("Название сервера - для оповещений")]
            public string servername;

            [JsonProperty("Оповещание о рейде в ВК")]
            public VK vk;

            [JsonProperty("Оповещание о рейде в Rust+")]
            public RUSTPLUS rustplus;

            [JsonProperty("Оповещание о рейде в игре")]
            public INGAME ingame;

            [JsonProperty("Оповещание о рейде в дискорд")]
            public DISCORD discord;

            [JsonProperty("Дополнительный список предметов, которые учитывать")]
            public string[] spisok;
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
                        channelbutton = "Получить код",
                        channelex = "Ваш код: {code}",
                        channelmessageid = "",
                        channeltext = "Введите полученый код в меню интеграции дискорда с игровым профилем.\nЧат команда /raid\nВводить в самой игре, а не в дискорде!",
                        channelcolor = 14177041
                    },
                    spisok = _spisok
                };
            }
        }
        private static string[] _spisok = new string[] { "wall.external.high", "wall.external.high.stone", "gates.external.high.wood", "gates.external.high.stone", "wall.window.bars.metal", "wall.window.bars.toptier", "wall.window.glass.reinforced", "wall.window.bars.wood" };

        #endregion

        #region DISCORD
        private readonly DiscordSettings _discordSettings = new DiscordSettings();
        private DiscordGuild _guild;
        [DiscordClient] DiscordClient Client;
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
                    DiscordChannel channel = GetChannel(config.discord.channel);
                    if (channel == null)
                    {
                        Debug.Log("КАНАЛ НЕ СУЩЕСТВУЕТ!");
                        return;
                    }

                    var embeds = new List<DiscordEmbed> { new DiscordEmbed { Color = new DiscordColor(config.discord.channelcolor), Description = config.discord.channeltext } };
                    var components = CreateComponents(config.discord.channelbutton);
                    if (!string.IsNullOrEmpty(config.discord.channelmessageid))
                    {
                        channel.GetChannelMessage(Client, new Snowflake(config.discord.channelmessageid), message =>
                        {
                            message.Embeds = embeds;
                            message.Components.Clear();
                            message.Components = components;
                            message.EditMessage(Client);
                        },
                        error =>
                        {
                            if (error.HttpStatusCode == 404)
                            {
                                channel?.CreateMessage(Client, new MessageCreate { Embeds = embeds, Components = components }, message =>
                                {
                                    config.discord.channelmessageid = message.Id;
                                    SaveConfig();
                                });
                            }
                        });
                    }
                    else
                    {
                        channel?.CreateMessage(Client, new MessageCreate { Embeds = embeds, Components = components },
                         message =>
                         {
                             config.discord.channelmessageid = message.Id;
                             SaveConfig();
                         });
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

            if (!interaction.Data.ComponentType.HasValue || interaction.Data.ComponentType.Value != MessageComponentType.Button || interaction.Data.CustomId != "01")
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
            interaction.CreateInteractionResponse(Client, new InteractionResponse
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
            _guild = ready.Guilds.FirstOrDefault().Value;
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
            builder.AddActionButton(ButtonStyle.Success, button, "01", false);

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
        string connect = "22.01.20.20:0620";
        //{fon}
        string MAIN = "[{\"name\":\"SubContent_UI\",\"parent\":\"Main_UI\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"color\":\"0 0 0 0.4\",\"material\":\"assets/content/ui/goggle_overlay.mat\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0.6\",\"anchormax\":\"0.5 0.6\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]";
        string UI = "[{\"name\":\"IF\",\"parent\":\"SubContent_UI\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8901961 0.8901961 0.8901961 0.4156863\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0.5\",\"anchormax\":\"0.5 0.5\",\"offsetmin\":\"-120 -100\",\"offsetmax\":\"120 -70\"}]},{\"name\":\"D\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8784314 0.9843137 1 0.5686275\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 1\"}]},{\"name\":\"U\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8799988 0.984443 1 0.5695249\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 -1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"L\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8799988 0.984443 1 0.5695249\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 0\"}]},{\"name\":\"R\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8799988 0.984443 1 0.5695249\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"-1 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"I\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.InputField\",\"align\":\"MiddleLeft\",\"color\":\"1 1 1 0.8627451\",\"command\":\"raid.input\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 0\",\"offsetmax\":\"-5 0\"}]},{\"name\":\"L1\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8784314 0.9843137 1 0.5686275\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-40 17\",\"offsetmax\":\"-5 18\"}]},{\"name\":\"L4\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8784314 0.9843137 1 0.5686275\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-40 84\",\"offsetmax\":\"-5 85\"}]},{\"name\":\"P1\",\"parent\":\"L4\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8901961 0.8901961 0.8901961 0.4196078\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 -15\",\"offsetmax\":\"245 15\"}]},{\"name\":\"D\",\"parent\":\"P1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8784314 0.9843137 1 0.5686275\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 1\"}]},{\"name\":\"U\",\"parent\":\"P1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8799988 0.984443 1 0.5695249\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 -1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"L\",\"parent\":\"P1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8784314 0.9843137 1 0.5686275\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 0\"}]},{\"name\":\"R\",\"parent\":\"P1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8799988 0.984443 1 0.5695249\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"-1 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"T\",\"parent\":\"P1\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t2}\",\"align\":\"MiddleCenter\",\"color\":\"1 1 1 0.7843137\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 0\",\"offsetmax\":\"-5 0\"}]},{\"name\":\"L5\",\"parent\":\"L4\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8784314 0.9843137 1 0.5686275\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 35\"}]},{\"name\":\"L6\",\"parent\":\"L5\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8784314 0.9843137 1 0.5686275\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"35 1\"}]},{\"name\":\"T\",\"parent\":\"L6\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t1}\",\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"MiddleLeft\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 -10\",\"offsetmax\":\"300 10\"}]},{\"name\":\"L7\",\"parent\":\"L5\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8784314 0.9843137 1 0.5686275\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 35\"}]},{\"name\":\"L8\",\"parent\":\"L7\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8784314 0.9843137 1 0.5686275\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"35 1\"}]},{\"name\":\"T\",\"parent\":\"L8\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t0}\",\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"MiddleLeft\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 -10\",\"offsetmax\":\"300 10\"}]},{\"name\":\"H\",\"parent\":\"L7\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t6}\",\"fontSize\":24},{\"type\":\"RectTransform\",\"anchormin\":\"40 1\",\"anchormax\":\"500 1\",\"offsetmin\":\"0 20\",\"offsetmax\":\"0 60\"}]},{\"name\":\"L2\",\"parent\":\"L1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8784314 0.9843137 1 0.5686275\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 35\"}]},{\"name\":\"L3\",\"parent\":\"L2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8784314 0.9843137 1 0.5686275\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"35 1\"}]},{\"name\":\"T1\",\"parent\":\"L3\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t4}\",\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"MiddleLeft\",\"color\":\"1 1 1 0.8627451\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 -10\",\"offsetmax\":\"500 10\"}]},{\"name\":\"D\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t5}\",\"font\":\"RobotoCondensed-Regular.ttf\",\"color\":\"1 1 1 0.6699298\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"-160 -200\",\"offsetmax\":\"250 -100\"}]}]";
        string IF2 = "[{\"name\":\"IF2\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8901961 0.8901961 0.8901961 0.4156863\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"-120 -70\",\"offsetmax\":\"120 -40\"}]},{\"name\":\"D\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8784314 0.9843137 1 0.5686275\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 1\"}]},{\"name\":\"U\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8799988 0.984443 1 0.5695249\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 -1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"L\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8799988 0.984443 1 0.5695249\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 0\"}]},{\"name\":\"R\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8799988 0.984443 1 0.5695249\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"-1 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"I\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.InputField\",\"command\":\"raid.input\",\"align\":\"MiddleLeft\",\"color\":\"1 1 1 0.8627451\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 0\",\"offsetmax\":\"-5 0\"}]},{\"name\":\"BTN2\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"raid.accept\",\"color\":\"0.5450981 1 0.6941177 0.509804\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 -30\",\"offsetmax\":\"125 0\"}]},{\"name\":\"T\",\"parent\":\"BTN2\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text2}\",\"align\":\"MiddleCenter\",\"color\":\"1 1 1 0.9056942\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"L1\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8784314 0.9843137 1 0.5686275\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-40 17\",\"offsetmax\":\"-5 18\"}]},{\"name\":\"L2\",\"parent\":\"L1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8784314 0.9843137 1 0.5686275\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 35\"}]},{\"name\":\"L3\",\"parent\":\"L2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8784314 0.9843137 1 0.5686275\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"35 1\"}]},{\"name\":\"T1\",\"parent\":\"L3\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t3}\",\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"MiddleLeft\",\"color\":\"1 1 1 0.8627451\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 -10\",\"offsetmax\":\"500 10\"}]}]";
        string IF2A = "[{\"name\":\"BTN2\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"{coma}\",\"color\":\"{color}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 -30\",\"offsetmax\":\"125 0\"}]},{\"name\":\"T\",\"parent\":\"BTN2\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text2}\",\"align\":\"MiddleCenter\",\"color\":\"1 1 1 0.9056942\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]";
        string BTN = "[{\"name\":\"BTN\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"{coma}\",\"color\":\"{color}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 -30\",\"offsetmax\":\"125 0\"}]},{\"name\":\"T\",\"parent\":\"BTN\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text1}\",\"align\":\"MiddleCenter\",\"color\":\"1 1 1 0.9019608\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]";
        string ER = "[{\"name\":\"ER\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{e0}\",\"fontSize\":16,\"font\":\"RobotoCondensed-Regular.ttf\",\"color\":\"1 0.5429931 0.5429931 0.787812\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0.5\",\"anchormax\":\"0.5 0.5\",\"offsetmin\":\"-160 -95\",\"offsetmax\":\"245 -35\"}]}]";
        string IBLOCK = "[{\"name\":\"IBLOCK\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 0\",\"offsetmax\":\"-5 0\"}]}]";
        string MAINH = "[{\"name\":\"AG\",\"parent\":\"SubContent_UI\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{a0}\",\"fontSize\":24},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-120 60\",\"offsetmax\":\"500 115\"}]}]";
        string AG = "[{\"name\":\"AGG{num}\",\"parent\":\"SubContent_UI\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8901961 0.8901961 0.8901961 0.4156863\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-120 {min}\",\"offsetmax\":\"120 {max}\"}]},{\"name\":\"D\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8784314 0.9843137 1 0.5686275\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 1\"}]},{\"name\":\"R\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8799988 0.984443 1 0.5695249\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"-1 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"U\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8799988 0.984443 1 0.5695249\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 -1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"L\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.8799988 0.984443 1 0.5695249\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 0\"}]},{\"name\":\"AT\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{id}\",\"align\":\"MiddleLeft\",\"color\":\"1 1 1 0.7878121\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 0\",\"offsetmax\":\"-5 0\"}]},{\"name\":\"BTN{num}\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"{coma}\",\"color\":\"{color}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 0\",\"offsetmax\":\"125 30\"}]},{\"name\":\"T\",\"parent\":\"BTN{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text1}\",\"align\":\"MiddleCenter\",\"color\":\"1 1 1 0.9019608\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"AL\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{icocolor}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"-35 -30\",\"offsetmax\":\"-5 0\"}]},{\"name\":\"ALT\",\"parent\":\"AL\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{ico}\",\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]";
        string EXIT = "[{\"name\":\"E\",\"parent\":\"Main_UI\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"close\":\"Main_UI\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"-300 -100\",\"offsetmax\":\"-150 -50\"}]},{\"name\":\"ET\",\"parent\":\"E\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t7}\",\"fontSize\":30,\"align\":\"MiddleCenter\",\"color\":\"0.5938045 0.5789595 0.5789595 1\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"10 0\",\"offsetmax\":\"0 0\"}]}]";
        #region Data
        class Storage 
        {
            public string vk;
            public ulong discord;
            public bool rustplus;
            public bool ingame;
        }

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
            if (datas.TryGetValue(player.userID, out storage))
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

        #region API VK
        class ALERT
        {
            public DateTime gamecooldown;
            public DateTime rustpluscooldown;
            public DateTime vkcooldown;
            public DateTime discordcooldown;
            public DateTime vkcodecooldown;
        }

        private static Dictionary<ulong, ALERT> alerts = new Dictionary<ulong, ALERT>();
        class CODE
        {
            public string id;
            public ulong gameid;
        }

        private Dictionary<string, CODE> VKCODES = new Dictionary<string, CODE>();
        private Dictionary<ulong, string> DISCORDCODES = new Dictionary<ulong, string>();

        private void GetRequest(string reciverID, string msg, BasePlayer player = null, string num = null) => webrequest.Enqueue("https://api.vk.com/method/messages.send?domain=" + reciverID + "&message=" + msg.Replace("#", "%23") + "&v=5.81&access_token=" + config.vk.api, null, (code2, response2) => ServerMgr.Instance.StartCoroutine(GetCallback(code2, response2, reciverID, player, num)), this);

        private void SendError(BasePlayer player, string key)
        {
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "ER");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", ER.Replace("{e0}", GetMessage(key, player.UserIDString)));
        }
        private IEnumerator GetCallback(int code, string response, string id, BasePlayer player = null, string num = null)
        {
            if (player == null) yield break;
            if (response == null || code != 200)
            {
                ALERT alert;
                if (alerts.TryGetValue(player.userID, out alert)) alert.vkcooldown = DateTime.Now;
                Debug.Log("НЕ ПОЛУЧИЛОСЬ ОТПРАВИТЬ СООБЩЕНИЕ В ВК! => обнулили кд на отправку");
                yield break;
            }
            yield return new WaitForEndOfFrame();
            if (!response.Contains("error"))
            {
                ALERT aLERT;
                if (alerts.TryGetValue(player.userID, out aLERT))
                {
                    aLERT.vkcodecooldown = DateTime.Now.AddMinutes(1);
                }
                else
                {
                    alerts.Add(player.userID, new ALERT { vkcodecooldown = DateTime.Now.AddMinutes(1) });
                }
                if (VKCODES.ContainsKey(num)) VKCODES.Remove(num);
                VKCODES.Add(num, new CODE { gameid = player.userID, id = id });
                write[player.userID] = "";
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

        #region TIME
        private static string m0 = "МИНУТ";
        private static string m1 = "МИНУТЫ";
        private static string m2 = "МИНУТУ";

        private static string s0 = "СЕКУНД";
        private static string s1 = "СЕКУНДЫ";
        private static string s2 = "СЕКУНДУ";

        private static string FormatTime(TimeSpan time)
        => (time.Minutes == 0 ? string.Empty : FormatMinutes(time.Minutes)) + ((time.Seconds == 0) ? string.Empty : FormatSeconds(time.Seconds));

        private static string FormatMinutes(int minutes) => FormatUnits(minutes, m0, m1, m2);

        private static string FormatSeconds(int seconds) => FormatUnits(seconds, s0, s1, s2);

        private static string FormatUnits(int units, string form1, string form2, string form3)
        {
            var tmp = units % 10;

            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9 || tmp == 0)
                return $"{units} {form1} ";

            if (tmp >= 2 && tmp <= 4)
                return $"{units} {form2} ";

            return $"{units} {form3} ";
        }
        #endregion

        #region COMMANDS
        [PluginReference] Plugin BMenu;

        [ChatCommand("raid")]
        private void callcommandrn(BasePlayer player, string command, string[] arg)
        {
            OpenMenu(player);
        }

        private void OpenMenu(BasePlayer player, bool first = true)
        {
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "SubContent_UI");
            if (first)
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "SubMenu_UI");
                BMenu.Call("DestroyProfileLayers", player);
                BMenu.Call("SetPage", player.userID, "raid");
                BMenu.Call("SetActivePButton", player, "raid");
            }
            //0.5450981 1 0.6941177 0.509804
            //{\"name\":\"Main_UI\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.07843138 0.06666667 0.1098039 0.9490196\",\"material\":\"assets/content/ui/uibackgroundblur.mat\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", MAIN);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", MAINH.Replace("{a0}", GetMessage("{amain}", player.UserIDString)));
            int num = 0;
            Storage storage = GetStorage(player.userID);
            #region VK
            if (config.vk.enable && !string.IsNullOrEmpty(config.vk.api))
            {
                if (!string.IsNullOrEmpty(storage.vk)) AddElementUI(player, "Вконтакте", "0.8901961 0.8901961 0.8901961 0.4156863", "Отключить", "raid.vkdelete", "VK", "0.5803922 0.6627451 1 0.4156863", num);
                else AddElementUI(player, "Вконтакте", "0.5450981 1 0.6941177 0.509804", "Подключить", "raid.vkadd", "VK", "0.5803922 0.6627451 1 0.4156863", num);
                num++;
            }
            #endregion

            #region Rust+
            if (config.rustplus.enable)
            {
                if (!storage.rustplus) AddElementUI(player, "Приложение Rust+", "0.5450981 1 0.6941177 0.509804", "Включить", "raid.rustplus", "R+", "1 0.5803921 0.6013725 0.4156863", num);
                else AddElementUI(player, "Приложение Rust+", "0.8901961 0.8901961 0.8901961 0.4156863", "Отключить", "raid.rustplus", "R+", "1 0.5803921 0.6013725 0.4156863", num);
                num++;
            }
            #endregion

            #region InGame
            if (config.ingame.enable)
            {
                if (!storage.ingame) AddElementUI(player, "Графическое отображение в игре", "0.5450981 1 0.6941177 0.509804", "Включить", "raid.ingame", "UI", "1 0.7843137 0.5764706 0.4156863", num);
                else AddElementUI(player, "Графическое отображение в игре", "0.8901961 0.8901961 0.8901961 0.4156863", "Отключить", "raid.ingame", "UI", "1 0.7843137 0.5764706 0.4156863", num);
                num++;
            }
            #endregion

            #region Discord
            if (config.discord.enable && !string.IsNullOrEmpty(config.discord.token))
            {
                if (storage.discord == 0UL) AddElementUI(player, "Discord", "0.5450981 1 0.6941177 0.509804", "Включить", "raid.discordadd", "DS", "1 0.7843137 0.5764706 0.4156863", num);
                else
                {
                    AddElementUI(player, $"Discord", "0.8901961 0.8901961 0.8901961 0.4156863", "Отключить", "raid.discorddelete", "DS", "1 0.7843137 0.5764706 0.4156863", num);
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
            write[player.userID] = text;
        }

        private void SendError2(BasePlayer player, string key)
        {
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "BTN2");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", IF2A.Replace("{text2}", GetMessage(key, player.UserIDString)).Replace("{coma}", "").Replace("{color}", "1 0.5450981 0.5450981 0.509804"));
            timer.Once(1f, () =>
            {
                if (!player.IsConnected) return;
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "BTN2");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", IF2A.Replace("{text2}", GetMessage("{text2}", player.UserIDString)).Replace("{coma}", "raid.accept").Replace("{color}", "0.5450981 1 0.6941177 0.509804"));
            });
        }

        #region InGame Comand
        [ConsoleCommand("raid.ingame")]
        void raplsgame(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            Storage storage = GetStorage(player.userID);
            storage.ingame = !storage.ingame;
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
            Storage storage = GetStorage(player.userID);
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
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", MAIN);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", UI.Replace("{t7}", GetMessage("{d7}", player.UserIDString)).Replace("{t6}", GetMessage("{t6}", player.UserIDString)).Replace("{t5}", GetMessage("{d5}", player.UserIDString)).Replace("{t4}", GetMessage("{d3}", player.UserIDString)).Replace("{t2}", GetMessage("{d2}", player.UserIDString)).Replace("{t1}", GetMessage("{d1}", player.UserIDString)).Replace("{t0}", GetMessage("{d0}", player.UserIDString)));
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", BTN.Replace("{text1}", GetMessage("{text2}", player.UserIDString)).Replace("{coma}", "raid.acceptds").Replace("{color}", "0.5450981 1 0.6941177 0.509804"));
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
            if (!write.TryGetValue(player.userID, out text) || string.IsNullOrEmpty(text))
            {
                SendError(player, "rnnocode");
                return;
            }


            ulong user = DISCORDCODES.FirstOrDefault(x => x.Value == text).Key;
            if (user != 0UL)
            {
                Storage storage = GetStorage(player.userID);
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
            Storage storage = GetStorage(player.userID);
            storage.discord = 0;
            SaveStorage(player);
            OpenMenu(player, false);
        }
        #endregion

        #region Vk COmand
        [ConsoleCommand("raid.vkdelete")]
        void vkdelete(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            Storage storage = GetStorage(player.userID);
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
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", MAIN);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", UI.Replace("{t7}", GetMessage("{t7}", player.UserIDString)).Replace("{t6}", GetMessage("{t6}", player.UserIDString)).Replace("{t5}", GetMessage("{t5}", player.UserIDString)).Replace("{t4}", GetMessage("{t3}", player.UserIDString)).Replace("{t2}", GetMessage("{t2}", player.UserIDString)).Replace("{t1}", GetMessage("{t1}", player.UserIDString)).Replace("{t0}", GetMessage("{t0}", player.UserIDString)));
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", BTN.Replace("{text1}", GetMessage("{text1}", player.UserIDString)).Replace("{coma}", "raid.send").Replace("{color}", "0.5450981 1 0.6941177 0.509804"));
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
            if (!write.TryGetValue(player.userID, out text) || string.IsNullOrEmpty(text))
            {
                SendError2(player, "rnnocode"); 
                return;
            }

            CODE cODE;
            if (VKCODES.TryGetValue(text, out cODE) && cODE.gameid == player.userID)
            {
                Storage storage = GetStorage(player.userID);
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
            if (alerts.TryGetValue(player.userID, out aLERT) && aLERT.vkcodecooldown > DateTime.Now)
            {
                SendError(player, "rnaddcooldown");
                return;
            }

            string text;
            if(!write.TryGetValue(player.userID, out text) || string.IsNullOrEmpty(text))
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

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(messages, this);
        }

        Dictionary<string, string> messagesEN = new Dictionary<string, string>();

        private void OnServerInitialized()
        {
            if(!string.IsNullOrEmpty(config.discord.token)) CreateClient();
            else
            {
                Debug.LogError("Не указан токен для Discord бота!");
            }
            messagesEN = lang.GetMessages("en", this);
            connect = ConVar.Server.ip + ":" + ConVar.Server.port;
            CreateSpawnGrid();
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info, Item item)
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
            else if (entity is AnimatedBuildingBlock || entity is SamSite || entity is AutoTurret || entity is DecayEntity && config.spisok.Contains(entity.ShortPrefabName))
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

            BuildingPrivlidge buildingPrivlidge = entity.GetBuildingPrivilege(entity.WorldSpaceBounds());
            yield return new WaitForSeconds(1f);
            if (buildingPrivlidge == null) yield break;
            if (!buildingPrivlidge.AnyAuthed()) yield break;
            string name = player.displayName;
            string quad = GetNameGrid(position);
            string connect = ConVar.Server.ip + ":" + ConVar.Server.port;

            string destroy = GetMessage("+" + dname, player.UserIDString);
            foreach (var z in buildingPrivlidge.authorizedPlayers)
            {
                ALERTPLAYER(z.userid, name, quad, connect, destroy);
                yield return new WaitForEndOfFrame();
            }
        }

        private void ALERTPLAYER(ulong ID, string name, string quad, string connect, string destroy)
        {
            string IDstring = ID.ToString();
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
                    GetRequest(storage.vk, GetMessage("alertvk", IDstring).Replace("{ip}", connect).Replace("{name}", name).Replace("{destroy}", destroy).Replace("{quad}", quad).Replace("{servername}", config.servername));
                    alert.vkcooldown = DateTime.Now.AddSeconds(config.vk.cooldown);
                }
            }
            #endregion

            #region ОПОВЕЩЕНИЕ В RUST+
            if (storage.rustplus && config.rustplus.enable && alert.rustpluscooldown < DateTime.Now)
            {
                NotificationList.SendNotificationTo(ID, NotificationChannel.SmartAlarm, GetMessage("alertrustplus", IDstring).Replace("{ip}", connect).Replace("{name}", name).Replace("{destroy}", destroy).Replace("{quad}", quad).Replace("{servername}", config.servername), config.servername, Util.GetServerPairingData());
                alert.rustpluscooldown = DateTime.Now.AddSeconds(config.rustplus.cooldown);
            }
            #endregion

            #region ОПОВЕЩЕНИЕ В DISCORD
            if (config.discord.enable && !string.IsNullOrEmpty(config.discord.token) && alert.discordcooldown < DateTime.Now)
            {
                if (storage.discord != 0UL)
                {
                    Snowflake ss = new Snowflake(storage.discord);
                    if (!_guild.Members.Any(x => x.Value.User.Id == ss)) return;
                    _guild.Members.First(x => x.Value.User.Id == ss).Value.User.SendDirectMessage(Client, new MessageCreate { Content = GetMessage("alertdiscord", IDstring).Replace("{ip}", connect).Replace("{name}", name).Replace("{destroy}", destroy).Replace("{quad}", quad).Replace("{servername}", config.servername) });
                    alert.discordcooldown = DateTime.Now.AddSeconds(config.discord.cooldown);
                }
            }
            #endregion

            #region ОПОВЕЩЕНИЕ В ИГРЕ
            if (storage.ingame && config.ingame.enable && alert.gamecooldown < DateTime.Now)
            {
                BasePlayer player = BasePlayer.FindByID(ID);
                if (player != null && player.IsConnected)
                {
                    Timer ss;
                    if (timal.TryGetValue(player.userID, out ss))
                    {
                        if (!ss.Destroyed) ss.Destroy();
                    }
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "UIA");
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", config.ingame.UI.Replace("{text}", GetMessage("alertingame", IDstring).Replace("{ip}", connect).Replace("{name}", name).Replace("{destroy}", destroy).Replace("{quad}", quad).Replace("{servername}", config.servername)));
                    if(!string.IsNullOrEmpty(config.ingame.effect)) EffectNetwork.Send(new Effect(config.ingame.effect, player, 0, Vector3.up, Vector3.zero) { scale = 1f }, player.net.connection);
                    timal[player.userID] = timer.Once(config.ingame.destroy, () => CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "UIA"));
                    alert.gamecooldown = DateTime.Now.AddSeconds(config.ingame.cooldown);
                }
            }
            #endregion
        }

        private Dictionary<ulong, Timer> timal = new Dictionary<ulong, Timer>();
        #endregion

        #region Lang
        Dictionary<string, string> messages = new Dictionary<string, string>
        {
            { "{t0}", "Вступить в группу" },
            { "{t1}", "Написать любое сообщение в группу" },
            { "{t2}", "VK.COM/HAXLITE" },
            { "{t3}", "Ссылка на ваш профиль" },
            { "{t4}", "Проверьте вашу почту в vk.com и введите полученый код" },
            { "{t5}", "Вводите текст через Ctrl+V, что бы во время ввода не выполнялись команды забинженые на клавиши, которые вы нажимаете  " },
            { "{t6}", "Подключение оповещения о рейдах в Вконтакте" },
            { "{d0}", "Присоединиться к дискорд серверу" },
            { "{d1}", "Получить код в канале 'Интеграция с игровым профилем'" },
            { "{d2}", "DISCORD.GG/CyFmAqGBED" },
            { "{d3}", "Введите полученный код" },
            { "{d5}", "Вводите текст через Ctrl+V, что бы во время ввода не выполнялись команды забинженые на клавиши, которые вы нажимаете  " },
            { "{d6}", "Подключение оповещения о рейдах в Discord" },
            { "{t7}", "ВЫХОД" },
            { "{amain}", "Панель управления оповещений о рейде" },
            { "{text1}", "Получить код" },
            { "code", "Код для подтверджения аккаунта: {code}." },
            { "{text2}", "Подтвердить" },
            { "notallow" , "Купите возможность подключить оповещения о рейде в магазине!" },
            { "rncancel" , "Неверный код!" },
            { "alertvk" , "Внимание! Игрок {name} разрушил {destroy} в квадрате {quad}\nconnect {ip}" },
            { "alertdiscord" , "Внимание! Игрок {name} разрушил {destroy} в квадрате {quad}\nconnect {ip}" },
            { "alertrustplus" , "Внимание! Игрок {name} разрушил {destroy} в квадрате {quad}" },
            { "alertingame" , "Игрок {name} разрушил {destroy} в квадрате {quad}" },
            { "null" , "Введите ссылку на ваш профиль!" },
            { "rnnocode" , "Не указали код!" },
            { "rnprivate" , "Ваши настройки приватности не позволяют отправить вам сообщение." },
            { "rnerror" , "Невозможно отправить сообщение.\nПроверьте правильность ссылки или повторите попытку позже." },
            { "rnblack" , "Невозможно отправить сообщение.\nВы добавили группу в черный список или не подписаны на нее, если это не так, то просто напишите в группу сервера любое сообщение и попробуйте еще раз." },
            { "rnerror2" , "Вы указали неверную ссылку на ваш Вк, если это не так, то просто напишите в группу сервера любое сообщение и попробуйте еще раз." },
            { "rnaddcooldown", "Вы недавно создавали код для пордтверджения, попробуйте еще раз через минуту." },
            { "+wall Stone", "вашу каменную стену"},
            { "+wall.low Stone", "вашу каменную низкую стену"},
            { "+wall.frame Stone", "ваш каменный настенный каркас"},
            { "+foundation Stone", "ваш каменный фундамент"},
            { "+roof Stone", "вашу каменную крышу"},
            { "+wall.doorway Stone", "ваш каменный дверной проём"},
            { "+foundation.steps Stone", "ваши каменные ступеньки"},
            { "+block.stair.lshape Stone", "вашу каменную L-лестницу"},
            { "+block.stair.ushape Stone", "вашу каменную U-лестницу"},
            { "+foundation.triangle Stone", "ваш каменный треугольный фундамент"},
            { "+wall.window Stone", "ваш каменное окно"},
            { "+wall.half Stone", "вашу каменную полустену"},
            { "+wall Metal", "вашу металлическую стену"},
            { "+wall.low Metal", "вашу металлическую низкую стену"},
            { "+wall.frame Metal", "ваш металлический настенный каркас"},
            { "+foundation Metal", "ваш металлический фундамент"},
            { "+roof Metal", "вашу металлическую крышу"},
            { "+wall.doorway Metal", "ваш металлический дверной проём"},
            { "+foundation.steps Metal", "ваши металлические ступеньки"},
            { "+block.stair.lshape Metal", "вашу металлическую L-лестницу"},
            { "+block.stair.ushape Metal", "вашу металлическую U-лестницу"},
            { "+foundation.triangle Metal", "ваш металлический треугольный фундамент"},
            { "+wall.window Metal", "ваше металлическое окно"},
            { "+wall.half Metal", "вашу металлическую полустену"},
            { "+wall TopTier", "вашу бронированную стену"},
            { "+wall.low TopTier", "вашу бронированную низкую стену"},
            { "+wall.frame TopTier", "ваш бронированный настенный каркас"},
            { "+foundation TopTier", "ваш бронированный фундамент"},
            { "+roof TopTier", "вашу бронированную крышу"},
            { "+wall.doorway TopTier", "ваш бронированный дверной проём"},
            { "+foundation.steps TopTier", "ваши бронированные ступеньки"},
            { "+block.stair.lshape TopTier", "вашу бронированную L-лестницу"},
            { "+block.stair.ushape TopTier", "вашу бронированную U-лестницу"},
            { "+foundation.triangle TopTier", "ваш бронированный треугольный фундамент"},
            { "+wall.window TopTier", "ваше бронированное окно"},
            { "+wall.half TopTier", "вашу бронированную полустену"},
            { "+wall Wood", "вашу деревянную стену"},
            { "+wall.low Wood", "вашу деревянную низкую стену"},
            { "+wall.frame Wood", "ваш деревянный настенный каркас"},
            { "+foundation Wood", "ваш деревянный фундамент"},
            { "+roof Wood", "вашу деревянную крышу"},
            { "+wall.doorway Wood", "ваш деревянный дверной проём"},
            { "+foundation.steps Wood", "ваши деревянные ступеньки"},
            { "+block.stair.lshape Wood", "вашу деревянную L-лестницу"},
            { "+block.stair.ushape Wood", "вашу деревянную U-лестницу"},
            { "+foundation.triangle Wood", "ваш деревянный треугольный фундамент"},
            { "+wall.window Wood", "ваше деревянное окно"},
            { "+door.hinged.metal", "вашу металлическую дверь"},
            { "+floor Wood", "ваш деревянный пол"},
            { "+floor Metal", "ваш металлический пол"},
            { "+door.hinged.wood", "вашу деревянную дверь"},
            { "+floor Stone", "ваш каменный пол"},
            { "+door.double.hinged.wood", "вашу двойную деревянную дверь"},
            { "+door.double.hinged.metal", "вашу двойную металлическую дверь"},
            { "+shutter.wood.a", "ваши деревянные ставни"},
            { "+wall.frame.garagedoor", "вашу гаражную дверь"},
            { "+wall.window.bars.wood", "вашу деревянную решетку"},
            { "+floor.triangle Stone", "ваш каменный треугольный потолок"},
            { "+wall.external.high.wood", "ваши высокие деревянные ворота"},
            { "+door.double.hinged.toptier", "вашу двойную бронированную дверь"},
            { "+floor.triangle Metal", "ваш металлический треугольный потолок"},
            { "+wall.frame.netting", "вашу сетчатую стену"},
            { "+door.hinged.toptier", "вашу бронированную дверь"},
            { "+shutter.metal.embrasure.a", "ваши металлические ставни"},
            { "+wall.external.high.stone", "вашу высокую каменную стену"},
            { "+gates.external.high.stone", "ваши высокие каменные ворота"},
            { "+floor.ladder.hatch", "ваш люк с лестницей"},
            { "+floor.grill", "ваш решетчатый настил"},
            { "+floor.triangle Wood", "ваш деревянный треугольный потолок"},
            { "+floor.triangle TopTier", "ваш бронированный треугольный потолок"},
            { "+gates.external.high.wood", "ваши высокие деревянные ворота"},
            { "+wall.half Wood", "вашу деревянную полустену"},
            { "+floor TopTier", "ваш треугольный бронированный потолок"},
            { "+wall.frame.cell", "вашу тюремную стену"},
            { "+wall.window.bars.metal", "вашу металлическую решетку"},
            { "+wall.frame.fence", "ваш сетчатый забор"},
            { "+shutter.metal.embrasure.b", "вашу металлическую бойницу"},
            { "+wall.window.glass.reinforced", "ваше окно из укрепленного стекла"},
            { "+wall.frame.fence.gate", "вашу сетчатую дверь"},
            { "+floor.frame Stone", "ваш каменный потолочный каркас"},
            { "+wall.frame.cell.gate", "вашу тюремную решетку"},
            { "+floor.frame Metal", "ваш металический потолочный каркас"},
            { "+floor.frame Wood", "ваш деревянный потолочный каркас" },
            { "+wall.frame.shopfront", "вашу витрину" },
            { "+wall.window.bars.toptier", "вашы оконные решетки" },
            { "+autoturret_deployed", "вашу турель" },
            { "+sam_site_turret_deployed", "вашу зенитную турель" },
            { "+ramp TopTier", "вашу бронированную рампу" },
            { "+floor.triangle.ladder.hatch", "ваш треугольный люк с лестницей" },
            { "+block.stair.spiral Wood", "вашу деревянную спиральную лестницу" },
            { "+ramp Metal", "вашу металлическую рампу" },
            { "+block.stair.spiral.triangle Wood", "вашу деревянную треугольную спиральную лестницу" },
            { "+block.stair.spiral.triangle Metal", "вашу металическую треугольную спиральную лестницу" },
            { "+block.stair.spiral Stone", "вашу каменную спиральную лестницу" },
            { "+block.stair.spiral Metal", "вашу металическую спиральную лестницу" },
            { "+floor.triangle.frame Stone", "ваш каменный треугольный потолочный каркас" },
            { "+roof.triangle Metal", "вашу металическую крышу" },
            { "+floor.triangle.frame Metal", "ваш металический треугольный потолочный каркас" },
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
            { "+floor.frame TopTier", "ваш бронированный потолочный каркас"}

        };

        private string GetMessage(string key, string userId)
        {
            string text;
            if (!messagesEN.TryGetValue(key, out text)) text = key;
            return text;
            //lang.GetMessage(key, this, userId);
        }
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