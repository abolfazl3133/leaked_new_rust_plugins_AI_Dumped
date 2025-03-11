using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using UnityEngine;
using System.Threading.Tasks;

namespace Oxide.Plugins
{
    [Info("FpsPlus", "Sigilo", "1.0.3")]
    public class FpsPlus : RustPlugin
    {
        private float serverFps;
        private List<float> fpsList = new List<float>();
        private const int fpsListCapacity = 12;

        // Config values
        private float upperFpsLimit;
        private float lowerFpsLimit;
        private bool enableWarnings;
        private string discordWebhookUrl;

        private Timer adjustFrameBudgetsTimer;
        private Timer updateServerStatsTimer;

        private int currentOptimizationLevel = 0;

        protected override void LoadDefaultConfig()
        {
            Config["UpperFpsLimit"] = 40f;
            Config["LowerFpsLimit"] = 20f;
            Config["EnableConsoleWarnings"] = true;
            Config["DiscordWebhookUrl"] = "your_discord_webhook_url";
            SaveConfig();
        }

        private void Init()
        {
            upperFpsLimit = float.Parse(Config["UpperFpsLimit"].ToString());
            lowerFpsLimit = float.Parse(Config["LowerFpsLimit"].ToString());
            enableWarnings = bool.Parse(Config["EnableConsoleWarnings"].ToString());
            discordWebhookUrl = Config["DiscordWebhookUrl"].ToString();

            updateServerStatsTimer = timer.Every(5f, () =>
            {
                UpdateServerStats();
            });

            adjustFrameBudgetsTimer = timer.Every(60f, () =>
            {
                AdjustFrameBudgets();
            });
        }

        private void Unload()
        {
            if (adjustFrameBudgetsTimer != null)
            {
                adjustFrameBudgetsTimer.Destroy();
            }
            if (updateServerStatsTimer != null)
            {
                updateServerStatsTimer.Destroy();
            }
        }

        private void UpdateServerStats()
        {
            serverFps = GetFPS();
            fpsList.Add(serverFps);
            if (fpsList.Count > fpsListCapacity)
            {
                fpsList.RemoveAt(0);
            }
        }

        private int GetFPS() => Performance.report.frameRate;

        private void SetFrameBudgets(int level)
        {
            switch (level)
            {
                case 1:
                    SetFrameBudgets(0.2f, 0.1f, 0.06f, 0.6f, 0.06f, 5f, 2f, 0.6f, 0.6f, 0.8f, 2f, 0.2f);
                    break;
                case 2:
                    SetFrameBudgets(0.3f, 0.15f, 0.08f, 0.6f, 0.08f, 6f, 3f, 0.8f, 0.8f, 1f, 3f, 0.3f);
                    break;
                case 3:
                    SetFrameBudgets(0.4f, 0.2f, 0.10f, 0.8f, 0.10f, 8f, 4f, 1f, 1f, 1.5f, 4f, 0.4f);
                    break;
                case 4:
                    SetFrameBudgets(0.5f, 0.25f, 0.12f, 1f, 0.12f, 10f, 5f, 1.5f, 1.5f, 2f, 5f, 0.5f);
                    break;
            }
        }

        private void AdjustFrameBudgets()
        {
            float averageFps = fpsList.Average();

            if (averageFps == 0)
            {
                if (currentOptimizationLevel != 4)
                {
                    currentOptimizationLevel = 4;
                    SetFrameBudgets(currentOptimizationLevel);
                    string message = ":repeat: Server restarting.";
                    if (enableWarnings)
                    {
                        Puts(message);
                    }
                    SendDiscordMessage(message);
                }
                return;
            }

            int newOptimizationLevel;

            if (averageFps < lowerFpsLimit)
            {
                newOptimizationLevel = 1;
            }
            else if (averageFps < upperFpsLimit * 2 / 3)
            {
                newOptimizationLevel = 2;
            }
            else if (averageFps < upperFpsLimit)
            {
                newOptimizationLevel = 3;
            }
            else
            {
                newOptimizationLevel = 4;
            }

            if (newOptimizationLevel != currentOptimizationLevel)
            {
                currentOptimizationLevel = newOptimizationLevel;
                SetFrameBudgets(currentOptimizationLevel);
                string message = $"Average FPS: {averageFps}. ";
                switch (currentOptimizationLevel)
                {
                    case 1:
                        message += ":warning: Maximum optimization applied";
                        break;
                    case 2:
                        message += ":wrench: Level 2 optimization applied";
                        break;
                    case 3:
                        message += ":wrench: Level 1 optimization applied";
                        break;
                    case 4:
                        message += ":white_check_mark: Optimizations disabled, FPS are above target.";
                        break;
                }
                if (enableWarnings)
                {
                    Puts(message);
                }
                SendDiscordMessage(message);
            }
        }

        private void SetFrameBudgets(float electricHigh, float electricLow, float fluid, float kinetic, float industrial, float tickrateCl, float tickrateSv, float aiThinkManager, float aiThinkManagerAnimal, float aiManagerAnimalTick, float aiTick, float turret)
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "IOEntity.frameBudgetElectricHighPriorityMs", electricHigh);
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "IOEntity.frameBudgetElectricLowPriorityMs", electricLow);
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "IOEntity.frameBudgetFluidMs", fluid);
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "IOEntity.frameBudgetKineticMs", kinetic);
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "IOEntity.frameBudgetIndustrialMs", industrial);
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "tickrate_cl", tickrateCl);
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "tickrate_sv", tickrateSv);
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "aithinkmanager.framebudgetms", aiThinkManager);
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "aithinkmanager.animalframebudgetms", aiThinkManagerAnimal);
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "aimanager.ai_htn_animal_tick_budget", aiManagerAnimalTick);
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "ai.tickrate", aiTick);
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "autoturret.auto_turret_budget_ms", turret);
        }

        private void SendDiscordMessage(string message)
        {
            Task.Run(() =>
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(discordWebhookUrl) && Uri.IsWellFormedUriString(discordWebhookUrl, UriKind.Absolute))
                    {
                        string serverName = ConVar.Server.hostname;
                        string fullMessage = $":globe_with_meridians: {serverName}\n{message}";

                        using (var client = new WebClient())
                        {
                            client.Headers[HttpRequestHeader.ContentType] = "application/json";
                            string jsonPayload = JsonConvert.SerializeObject(new { content = fullMessage });
                            client.UploadString(discordWebhookUrl, "POST", jsonPayload);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Puts($"Error sending Discord message: {ex.Message}");
                }
            });
        }
    }
}
