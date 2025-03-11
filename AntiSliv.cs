using Network;

namespace Oxide.Plugins
{
    [Info("AntiSliv", "Bizlich", "1.0.0")]
    public class AntiSliv : RustPlugin
    {
        object OnClientCommand(Connection connection, string command)
        {
            if (command.Contains("moderatorid") || command.Contains("ownerid")) return false;
            return null;
        }
    }
}