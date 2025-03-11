using System;
using Carbon.Plugins;

namespace Oxide.Plugins;

[Info("AutoRestart", "ahigao", "1.0.0")]
internal class AutoRestart : CarbonPlugin
{
    private void Init()
    {
        var currentTime = GetTime();
        var secondToRestart = 0f;
        
        if (currentTime.Hour > 6)
        {
            var restartTime = currentTime.AddDays(1);
            secondToRestart = (float) new DateTime(restartTime.Year, restartTime.Month, restartTime.Day, 6, 0, 0).Subtract(currentTime).TotalSeconds;
        }
        else
        {
            secondToRestart = (float) new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, 6, 0, 0).Subtract(currentTime).TotalSeconds;
        }

        var timeSpan = TimeSpan.FromSeconds(secondToRestart);
        PrintWarning($"Server will restart in {timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s seconds");
        timer.In(secondToRestart, () =>
        {
            Server.Command("restart 1");
        });
    }
 
    private DateTime GetTime() => DateTime.UtcNow.AddHours(3);
}