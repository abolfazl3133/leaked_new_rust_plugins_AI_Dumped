using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using System.Collections;

namespace Oxide.Plugins
{
    [Info("Auto Signs", "YaMang -w-", "1.0.53")]
    [Description("https://discord.gg/DTQuEE7neZ")]
    public class AutoSigns : RustPlugin
    {
        #region Fields
        [PluginReference] private Plugin SignArtist;

        private List<Signage> signs = new List<Signage>();
        private string AdminPermission = "autosigns.admin";
        private float delay = 60;
        #endregion

        #region Command
        private void AutoSignsCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                Messages(player, Lang("NoPermission"));
                return;
            }
            if(args.Length == 0)
            {
                Messages(player, Lang("AutoSignsHelp"));
                return;
            }

            var sign = IsSign(player);
            if(sign == null)
            {
                Messages(player, $"Is Not Signable");
                return;
            }
            var pos = sign.ServerPosition;
            var qua = sign.ServerRotation;


            switch (args[0])
            {
                case "추가":
                case "add":
                    if(sign != null)
                    {
                        SignArtist?.Call("API_SkinSign", player, sign, args[1]);
                        var find = _config.Signs.FirstOrDefault(x => x.postion == pos);
                        if(find == null)
                        {
                            _config.Signs.Add(new Signs
                            {
                                postion = pos,
                                url = args[1]
                            });

                            Messages(player, $"Location: {pos} \nURL: {args[1]}\nhas been Added!");
                        }
                        else
                        {
                            
                            Messages(player, $"That sign already exists.\r\nPlease <b>/sign remove<b> and <b>/sign add url<b> try again.");
                        }

                        Messages(player, Lang("WarningIncrease"));

                        if (_config.generalSettings.AutoLock)
                        {
                            sign.SetFlag(BaseEntity.Flags.Locked, true, true);
                            sign.SendNetworkUpdateImmediate();
                        }
                        if (!signs.Contains(sign))
                            signs.Add(sign);
                    }
                    else
                    {
                        Messages(player, "This is not Sign or Not Found!");
                    }
                    break;

                case "지우기":
                case "remove":
                    if (sign != null)
                    {
                        var find = _config.Signs.FirstOrDefault(x => x.postion == pos);
                        if (find == null)
                        {
                            Messages(player, $"{pos} | There are no records registered for this sign.!");
                        }
                        else
                        {
                            _config.Signs.Remove(find);
                            Messages(player, $"{pos} | The sign has been removed.");
                        }

                        if (_config.generalSettings.AutoLock)
                        {
                            sign.SetFlag(BaseEntity.Flags.Locked, false, true);
                            sign.SendNetworkUpdateImmediate();
                        }
                        if (signs.Contains(sign))
                            signs.Remove(sign);
                    }
                    else
                    {
                        Messages(player, "This is not Sign or Not Found!");
                    }
                    break;

                case "초기화":
                case "wipe":
                    _config.Signs.Clear();
                    Messages(player, $"Sign Data Wiped!");
                    break;

                case "로드":
                case "load":

                    if(_spawnCoroutine != null)
                    {
                        if (_spawnCoroutine != null) ServerMgr.Instance.StopCoroutine(_spawnCoroutine);
                        _spawnCoroutine = null;
                    }

                    _spawnCoroutine = ServerMgr.Instance.StartCoroutine(LoadSigns());

                    Messages(player, $"Sign Load Start!!");
                    break;
            }
            SaveConfig();
        }

        #endregion

        #region Hook

        void OnServerInitialized()
        {
            if (SignArtist == null)
            {
                PrintWarning("SignArtist Not Loaded |-| Auto Signs not work");
                return;
            }

            permission.RegisterPermission(AdminPermission, this);

            for (int i = 0; i < _config.generalSettings.Commands.Count; i++)
            {
                cmd.AddChatCommand(_config.generalSettings.Commands[i], this, nameof(AutoSignsCMD));
            }

            if(!_config.generalSettings.BlockPickup)
                Unsubscribe(nameof(CanPickupEntity));
            if (!_config.generalSettings.NoDamage)
                Unsubscribe(nameof(OnEntityTakeDamage));

            if (_config.Signs.Count == 0) return;
            _spawnCoroutine = ServerMgr.Instance.StartCoroutine(LoadSigns());
        }
        private Coroutine _spawnCoroutine = null;
        private IEnumerator LoadSigns()
        {
            List<Signage> entities = new List<Signage>();
            var list = _config.Signs.ToList();
            if(_config.generalSettings.Debug)
                Puts($"{list.Count} Load Signs Progress... Waiting {delay} Seconds");

            yield return CoroutineEx.waitForSeconds(delay);

            int num = 1;
            foreach (var sign in list)
            {

                Vis.Entities(sign.postion, 10f, entities);

                bool fk_towns = false;

                foreach (var item in entities)
                {
                    if(item is Signage)
                    {
                        var s = item as Signage;

                        if (s.ServerPosition != sign.postion) continue;

                        SignArtist?.Call("API_SkinSign", null, s, sign.url);
                        if (_config.generalSettings.AutoLock)
                        {
                            s.SetFlag(BaseEntity.Flags.Locked, true, true);
                            s.SendNetworkUpdateImmediate();
                        }

                        if (!signs.Contains(s))
                            signs.Add(s);

                        if (_config.generalSettings.Debug)
                            Puts($"{num} Load Sign");

                        if (s.ShortPrefabName.Contains("sign.post.town"))
                            fk_towns = true;
                        else fk_towns = false;
                    }
                }

                if (fk_towns) continue;
                var entity = IsSign(sign.postion);
                if(entity == null)
                {
                    if (_config.generalSettings.Delete)
                    {
                        _config.Signs.Remove(sign);
                        PrintWarning($"{sign.postion} - {sign.url} is not Sign Delete Config");
                    }
                    else PrintWarning($"{sign.postion} - {sign.url} is not Sign");
                }

                signs.Clear();
                entities.Clear();
                num++;
                yield return CoroutineEx.waitForSeconds(1.5f);
            }

            if(_config.generalSettings.Delete)
                SaveConfig();
            if (_config.generalSettings.Debug)
                Puts($"Signs Load Completed");
            num = 0;
        }
        private bool CanPickupEntity(BasePlayer player, Signage sign)
        {
            if (signs.Contains(sign))
            {
                Messages(player, Lang("CantPickup"));
                return false;
            }
            return true;
        }

        private void OnEntityTakeDamage(Signage sign, HitInfo info)
        {
            if (signs.Contains(sign))
            {
                info.damageTypes.Clear();
                Messages(info.InitiatorPlayer, Lang("CantDamage"));
            }
        }

        void Unload()
        {
            if (_spawnCoroutine != null) ServerMgr.Instance.StopCoroutine(_spawnCoroutine);
        }

        #endregion

        #region Helper
        private void Messages(BasePlayer player, string text) => player.SendConsoleCommand("chat.add", 2, _config.generalSettings.SteamID, $"{_config.generalSettings.Prefix} {text}");
        private Signage IsSign(BasePlayer player)
        {
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 5f))
            {
                BaseEntity entity = hit.GetEntity();
                if (entity != null && IsSign(entity))
                {
                    return entity as Signage;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
        private bool IsSign(BaseEntity entity)
        {
            if (entity is Signage)
                return true;
            else if (entity is PhotoFrame)
                return true;
            
            return false;
        }
        private BaseEntity IsSign(Vector3 position)
        {
            Collider[] colliders = Physics.OverlapSphere(position, 0.1f);
            foreach (Collider collider in colliders)
            {
                BaseEntity entity = collider.GetComponentInParent<BaseEntity>();
                if (entity != null && entity is Signage)
                {
                    return entity;
                }
                else if (entity != null && entity is PhotoFrame)
                {
                    return entity;
                }
            }
            return null;
        }
        private Vector3 StringToVector3(string input)
        {
            // 문자열에서 괄호 및 공백 제거
            input = input.Replace("(", "").Replace(")", "").Replace(" ", "");

            string[] components = input.Split(',');

            if (components.Length != 3)
            {
                Debug.LogError("Invalid string format. Must be in the format '(x, y, z)'.");
                return Vector3.zero;
            }

            float x, y, z;
            if (!float.TryParse(components[0], out x) ||
                !float.TryParse(components[1], out y) ||
                !float.TryParse(components[2], out z))
            {
                Debug.LogError("Failed to parse components to floats.");
                return Vector3.zero;
            }

            return new Vector3(x, y, z);
        }
        #endregion

        #region Config        
        private ConfigData _config;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "General Settings")] public GeneralSettings generalSettings { get; set; }
            [JsonProperty(PropertyName = "Signs Settings")] public List<Signs> Signs { get; set; }
            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigData>();

            if (_config.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(_config, true);
        }

        protected override void LoadDefaultConfig() => _config = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                generalSettings = new GeneralSettings
                {
                    Prefix = "[Auto-Signs]",
                    Commands = new List<string>
                    {
                        "sign",
                        "as"
                    },
                    SteamID = "0",
                    AutoLock = true,
                    BlockPickup = true,
                    NoDamage = true,
                    Delete = false,
                    Debug = false
                },
                Signs = new List<Signs>(),
                Version = Version
            };
        }

        public class GeneralSettings
        {
            [JsonProperty(PropertyName = "Prefix", Order = 1)] public string Prefix { get; set; }
            [JsonProperty(PropertyName = "SteamID", Order = 2)] public string SteamID { get; set; }
            [JsonProperty(PropertyName = "Commands", Order = 3)] public List<string> Commands { get; set; }
            [JsonProperty(PropertyName = "Auto Signs Lock", Order = 4)] public bool AutoLock { get; set; }
            [JsonProperty(PropertyName = "Auto Block Pickup", Order = 5)] public bool BlockPickup { get; set; }
            [JsonProperty(PropertyName = "Sign Can't Damage", Order = 6)] public bool NoDamage { get; set; }
            [JsonProperty(PropertyName = "If the sign does not exist, delete it from the config", Order = 7)] public bool Delete { get; set; }
            [JsonProperty(PropertyName = "Debug", Order = 20)] public bool Debug { get; set; }
        }

        public class Signs
        {
            public Vector3 postion { get; set; }
            public string url { get; set; }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");
            if(_config.Version == new Core.VersionNumber(1, 0, 1))
            {
                _config.generalSettings.AutoLock = true;
                _config.generalSettings.BlockPickup = true;
                _config.generalSettings.NoDamage = true;
                _config.generalSettings.Delete = false;
            }
            _config.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {

                { "NoPermission", "<color=red>You do not  have permission!</color>" },
                { "WarningIncrease", "<color=red>If the image doesn't load, increase the image size limit!</color> <color=#00ff00>in SignArtist</color>" },
                { "CantPickup", "<color=red>This Signs Can't Pickup</color>" },
                { "CantDamage", "<color=red>This Signs Can't Damage</color>" },
                { "AutoSignsHelp", $"AutoSigns Help:\n" +
                                    $"/sign add url - Add the sign image data you are viewing.\n" +
                                    $"/sign remove - Delete the sign image data you are viewing." }

            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "NoPermission", "<color=red>당신은 권한이 없습니다.</color>" },
                { "AutoSignsHelp", $"AutoSigns 도움말:\n" +
                                    $"/sign add url - Add the sign image data you are viewing.\n" +
                                    $"/sign remove - Delete the sign image data you are viewing." }
            }, this, "ko");
        }

        private string Lang(string key, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this), args);
        }

        #endregion
    }
}
