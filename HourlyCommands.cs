using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using Facepunch; // Добавляем пространство имён для ConsoleSystem

namespace Oxide.Plugins
{
    [Info("HourlyCommands", "HS", "1.0.2")]
    [Description("Executes configurable commands at a configurable interval or immediately on server start. By DeepSeek")]
    class HourlyCommands : RustPlugin
    {
        private Timer _timer;
        private PluginConfig config;

        private class PluginConfig
        {
            public int IntervalSeconds { get; set; }
            public List<string> Commands { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new PluginConfig
            {
                IntervalSeconds = 3600,
                Commands = new List<string>
                {
                    "sentry.interferenceradius 40",
                    "sentry.maxinterference 12",
                    "hackablelockedcrate.requiredhackseconds 500"
                }
            }, true);
        }

        private void Init()
        {
            config = Config.ReadObject<PluginConfig>();

            if (config.IntervalSeconds > 0)
            {
                _timer = timer.Every(config.IntervalSeconds, ExecuteCommands);
                Puts($"Timer started with interval: {config.IntervalSeconds} seconds");
            }
            else
            {
                ExecuteCommands();
                Puts("Commands executed immediately");
            }
        }

        private void ExecuteCommands()
        {
            foreach (var command in config.Commands)
            {
                // Исправленная строка: используем ConsoleSystem.Run
                ConsoleSystem.Run(ConsoleSystem.Option.Server, command);
                Puts($"Executed: {command}");
            }
        }

        private void Unload()
        {
            _timer?.Destroy();
        }
    }
}