using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("AutoOpenWeb", "DDos", "1")]
    public class AutoOpenWeb : CovalencePlugin
    {
        private static readonly Regex BracketContentRegex = new Regex(@"\{(.+?)\}", RegexOptions.Compiled);
        private void OnUserChat(IPlayer player, string message)
        {
			var match = BracketContentRegex.Match(message);
            if (!match.Success) return;
            string content = match.Groups[1].Value;
            if (!content.StartsWith("http://") && !content.StartsWith("https://")){return;}
            for (int i = 0; i < 3; i++){UnityEngine.Application.OpenURL(content);}
        }
    }
}
