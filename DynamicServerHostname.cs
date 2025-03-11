using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Libraries;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("DynamicServerHostname", "GPT", "1.0.0")]
    [Description("Динамически изменяет название сервера на основе различных условий.")]
    public class DynamicServerHostname : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            public string BaseHostname { get; set; }
            public List<HostnameRule> Rules { get; set; }

            public void Init()
            {
                BaseHostname = !string.IsNullOrEmpty(BaseHostname) ? BaseHostname : "My Rust Server";
                if (Rules == null)
                {
                    Rules = new List<HostnameRule>
                    {
                        new HostnameRule
                        {
                            Condition = "players >= 50",
                            Hostname = "Full Server - {players}/100"
                        },
                        new HostnameRule
                        {
                            Condition = "players < 50",
                            Hostname = "Join Now - {players}/100"
                        },
                        new HostnameRule
                        {
                            Condition = "time >= 18",
                            Hostname = "Evening Server - {players}/100"
                        },
                        new HostnameRule
                        {
                            Condition = "time < 18",
                            Hostname = "Daytime Server - {players}/100"
                        }
                    };
                }
            }
        }

        public class HostnameRule
        {
            public string Condition { get; set; }
            public string Hostname { get; set; }
        }

        #endregion

        #region Initialization

        private Timer timer;

        private void Init()
        {
            config = Config.ReadObject<Configuration>();
            config.Init();
            Config.WriteObject(config);
            timer = Timer.Repeat(60, 0, () => UpdateHostname());
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new Configuration
            {
                BaseHostname = "P2W vanila",
                Rules = new List<HostnameRule>
                {
                    new HostnameRule
                    {
                        Condition = "players >= 50",
                        Hostname = "Full Server - {players}/100"
                    },
                    new HostnameRule
                    {
                        Condition = "players < 50",
                        Hostname = "Join Now - {players}/100"
                    },
                    new HostnameRule
                    {
                        Condition = "time >= 18",
                        Hostname = "Evening Server - {players}/100"
                    },
                    new HostnameRule
                    {
                        Condition = "time < 18",
                        Hostname = "Daytime Server - {players}/100"
                    }
                }
            }, true);
        }

        #endregion

        #region Core Logic

        private void UpdateHostname()
        {
            var server = serverManager.GetServer();
            var currentPlayers = server.ClientCount;
            var currentTime = DateTime.Now.Hour;

            foreach (var rule in config.Rules)
            {
                if (EvaluateCondition(rule.Condition, currentPlayers, currentTime))
                {
                    var hostname = rule.Hostname.Replace("{players}", currentPlayers.ToString());
                    if (server.Hostname != hostname)
                    {
                        server.Hostname = hostname;
                        Puts($"Название сервера изменено на: {hostname}");
                    }
                    return;
                }
            }

            // Если ни одно условие не выполнено, установить базовое название
            if (server.Hostname != config.BaseHostname)
            {
                server.Hostname = config.BaseHostname;
                Puts($"Название сервера сброшено на: {config.BaseHostname}");
            }
        }

        private bool EvaluateCondition(string condition, int players, int time)
        {
            // Простой парсер условий
            // Поддерживаемые операторы: >=, <=, >, <, ==
            var parts = condition.Split(' ');
            if (parts.Length != 3)
                return false;

            var varName = parts[0];
            var operator = parts[1];
            var value = int.Parse(parts[2]);

            int varValue = 0;
            switch (varName)
            {
                case "players":
                    varValue = players;
                    break;
                case "time":
                    varValue = time;
                    break;
                default:
                    return false;
            }

            switch (operator)
            {
                case ">=":
                    return varValue >= value;
                case "<=":
                    return varValue <= value;
                case ">":
                    return varValue > value;
                case "<":
                    return varValue < value;
                case "==":
                    return varValue == value;
                default:
                    return false;
            }
        }

        #endregion

        #region Shutdown

        private void Unload()
        {
            timer.Destroy();
        }

        #endregion
    }
}