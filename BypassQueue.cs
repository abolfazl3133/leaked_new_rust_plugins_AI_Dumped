using Network;

namespace Oxide.Plugins
{
    [Info("Bypass Queue", "Orange", "1.0.4")]
    public class BypassQueue : RustPlugin
    {
        private const string permUse = "bypassqueue.allow";

        private void Init()
        {
            permission.RegisterPermission(permUse, this);
        }

        private object CanBypassQueue(Connection connection)
        {
            if (ConVar.Server.maxplayers == 0)
            {
                return null;
            }
        
            if (permission.UserHasPermission(connection.userid.ToString(), permUse) == true)
            {
                return true;
            }
            
            return null;
        }
    }
}
