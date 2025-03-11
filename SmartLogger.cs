using System;
using System.Collections.Generic;
using System.Linq;

namespace Carbon.Plugins
{
    [Info("Smart Logger", "Assistant", "1.0.0")]
    [Description("Centralized logging and performance reporting")]
    internal sealed class SmartLogger : CarbonPlugin
    {
        private readonly Dictionary<string, int> _optimizationCounts = new();
        private DateTime _lastReportTime = DateTime.UtcNow;

        private void OnServerInitialized(bool initial)
        {
            _ = timer.Every(600f, LogAggregatedReport);
        }

        [HookMethod("OnOptimizationPerformed")]
        private void TrackOptimization(string pluginName, int count)
        {
            try
            {
                if (!string.IsNullOrEmpty(pluginName) && count > 0)
                {
                    _optimizationCounts[pluginName] = count;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Tracking error: {ex.Message}");
            }
        }

        private void LogAggregatedReport()
        {
            try
            {
                List<string> reportLines = new()
                {
                    "=== Performance Report ===",
                    $"Generated at: {DateTime.UtcNow:HH:mm:ss UTC}"
                };

                foreach (KeyValuePair<string, int> entry in _optimizationCounts.Where(e => e.Value > 0))
                {
                    reportLines.Add($"{entry.Key}: {entry.Value} optimizations");
                }

                reportLines.Add("========================");
                Logger.Log(string.Join("\n", reportLines));
                _optimizationCounts.Clear();

                _lastReportTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Logger.Log($"Report error: {ex.Message}");
            }
        }
    }
}