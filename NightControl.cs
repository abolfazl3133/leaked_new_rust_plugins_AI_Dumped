using System;
using System.Globalization;

namespace Carbon.Plugins
{
    [Info("Night Control", "Developer", "1.0.0")]
    [Description("Controls night environment settings using time events")]
    internal sealed class NightControl : CarbonPlugin
    {
        private Configuration _config = new();

        private void Init()
        {
            LoadConfig();
            // Apply settings immediately if it's night
            if (_config.IsNightTime)
            {
                ApplyNightSettings();
            }
        }

        private void OnRustNightStarted(bool isNight)
        {
            if (isNight)
            {
                ApplyNightSettings();
            }
        }

        private void ApplyNightSettings()
        {
            ConVar.Env.nightlight_brightness = _config.NightLightBrightness;
            ConVar.Env.nightlight_distance = _config.NightLightDistance;
            Logger.Log($"[NightControl] Night settings applied at {DateTime.Now:HH:mm}");
        }

        protected override void LoadConfig()
        {
            _config = Config.ReadObject<Configuration>() ?? new Configuration();
            ValidateConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        private void ValidateConfig()
        {
            _config.NightStart = Math.Clamp(_config.NightStart, 0, 23);
            _config.NightEnd = Math.Clamp(_config.NightEnd, 0, 23);
            _config.NightLightBrightness = Math.Clamp(_config.NightLightBrightness, 0f, 1f);
            _config.NightLightDistance = Math.Max(_config.NightLightDistance, 1f);
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        [ConsoleCommand("time")]
        private void TimeCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs())
            {
                arg.ReplyWith("Usage: time <day/night>");
                return;
            }

            switch (arg.Args[0].ToLower(CultureInfo.InvariantCulture))
            {
                case "day":
                    ConVar.Env.time = 12f; // Noon
                    arg.ReplyWith("Time set to day");
                    break;
                case "night":
                    ConVar.Env.time = 0f;  // Midnight
                    arg.ReplyWith("Time set to night");
                    break;
                default:
                    arg.ReplyWith("Invalid option. Use: time <day/night>");
                    break;
            }
        }

        private sealed class Configuration
        {
            public int NightStart = 19;
            public int NightEnd = 5;
            public float NightLightBrightness = 0.06f;
            public float NightLightDistance = 25f;

            public bool IsNightTime => DateTime.Now.Hour >= NightStart || DateTime.Now.Hour < NightEnd;
        }
    }
}