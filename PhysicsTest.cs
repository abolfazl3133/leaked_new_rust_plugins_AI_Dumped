namespace Carbon.Plugins
{
    [Info("Physics Test", "Assistant", "1.0.0")]
    [Description("Simple plugin to test physics commands")]
    internal sealed class PhysicsTest : CarbonPlugin
    {
        private void OnServerInitialized(bool initial)
        {
            Logger.Log("Physics Test plugin loaded!");
            ApplyPhysicsSettings();
        }

        [ChatCommand("testphysics")]
        private void TestPhysicsCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("Only admins can use this command.");
                return;
            }

            ApplyPhysicsSettings();
            player.ChatMessage("Physics settings applied!");
        }

        private void ApplyPhysicsSettings()
        {
            // Apply physics optimizations using valid commands
            _ = ConsoleSystem.Run(ConsoleSystem.Option.Server, "physics.sleepthreshold", "0.5");
            _ = ConsoleSystem.Run(ConsoleSystem.Option.Server, "physics.substeps", "3");
            _ = ConsoleSystem.Run(ConsoleSystem.Option.Server, "physics.tickrate", "30");

            Logger.Log("Applied physics optimizations");
        }
    }
}