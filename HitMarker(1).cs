// Reference: System.Drawing
using Facepunch;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Color = UnityEngine.Color;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("HitMarker", "fix иной", "1.0.1")]
    class HitMarker : RustPlugin
    {
        #region CONFIGURATION

        private bool Changed;
        private bool enablesound;
        private string soundeffect;
        private string headshotsoundeffect;
        private float damageTimeout;
        private int historyCapacity;
        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }
        /// Лог


        protected override void LoadDefaultConfig()
        {
            enablesound = Convert.ToBoolean(GetConfig("Sound", "EnableSoundEffect", true));
            soundeffect =
                Convert.ToString(GetConfig("Sound", "Sound Effect", "assets/bundled/prefabs/fx/takedamage_hit.prefab"));
            headshotsoundeffect =
                Convert.ToString(GetConfig("Sound", "HeadshotSoundEffect", "assets/bundled/prefabs/fx/headshot.prefab"));
            GetVariable(Config, "Через сколько будет пропадать урон", out damageTimeout, 1f);
            GetVariable(Config, "Вместимость истории урона", out historyCapacity, 5);
            SaveConfig();
        }
        public static void GetVariable<T>(DynamicConfigFile config, string name, out T value, T defaultValue)
        {
            config[name] = value = config[name] == null ? defaultValue : (T)Convert.ChangeType(config[name], typeof(T));
        }
        #endregion


        #region FIELDS

        [PluginReference]
        private Plugin Clans;

        List<BasePlayer> hitmarkeron = new List<BasePlayer>();


        Dictionary<BasePlayer, List<KeyValuePair<float, int>>> damageHistory = new Dictionary<BasePlayer, List<KeyValuePair<float, int>>>();

        Dictionary<BasePlayer, Oxide.Plugins.Timer> destTimers = new Dictionary<BasePlayer, Oxide.Plugins.Timer>();
        #endregion

        #region COMMANDS

        [ChatCommand("hitmarker")]
        void cmdHitMarker(BasePlayer player, string cmd, string[] args)
        {
            if (!hitmarkeron.Contains(player))
            {
                hitmarkeron.Add(player);
                SendReply(player,
                    "HitMarker:" + " " + "<color=orange>Вы включили показ урона.</color>");
            }
            else
            {
                hitmarkeron.Remove(player);
                SendReply(player,
                    "HitMarker:" + " " + "<color=orange>Вы отключили показ урона.</color>");
            }
        }
        ///// data save

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            InitFileManager();
            LoadDefaultConfig();
            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                hitmarkeron.Add(current);
            }
            CommunityEntity.ServerInstance.StartCoroutine(LoadImages());
            timer.Every(0.1f, OnDamageTimer);
        }

        IEnumerator LoadImages()
        {
            foreach (var imgKey in Images.Keys.ToList())
            {
                yield return CommunityEntity.ServerInstance.StartCoroutine(
                    m_FileManager.LoadFile(imgKey, Images[imgKey]));
                Images[imgKey] = m_FileManager.GetPng(imgKey);
            }
        }

        void OnPlayerInit(BasePlayer player)
        {
            hitmarkeron.Add(player);
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            hitmarkeron.Remove(player);
            damageHistory.Remove(player);
        }
        void OnPlayerAttack(BasePlayer attacker, HitInfo hitinfo)
        {
            var victim = hitinfo.HitEntity as BasePlayer;
            if (victim && hitmarkeron.Contains(attacker))
            {
                bool isFriend = (Clans?.Call("HasFriend", attacker.userID, victim.userID) as bool?) ?? false;
                if (hitinfo.isHeadshot)
                {
                    if (enablesound == true)
                    {
                        Effect.server.Run(headshotsoundeffect, attacker.transform.position, Vector3.zero,
                            attacker.net.connection);
                    }
                    DestroyLastCui(attacker);
                    CuiHelper.AddUi(attacker,
                        HandleArgs(MenuGUI, Images["hitmarker.hit.head"]));
                    destTimers[attacker] = timer.Once(0.7f, () =>
                    {
                        CuiHelper.DestroyUi(attacker, "hitmarkergui");
                    });
                }
                else
                {
                    if (enablesound)
                    {
                        Effect.server.Run(soundeffect, attacker.transform.position, Vector3.zero,
                            attacker.net.connection);
                    }
                    DestroyLastCui(attacker);
                    CuiHelper.AddUi(attacker,
                        HandleArgs(MenuGUI, Images["hitmarker.hit." + (isFriend ? "friend" : "normal")]));
                    destTimers[attacker] = timer.Once(0.7f, () =>
                    {
                        CuiHelper.DestroyUi(attacker, "hitmarkergui");
                    });
                }
            }
        }

        string MenuGUI = "[{\"name\":\"hitmarkergui\",\"parent\":\"Hud\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"sprite\":\"assets/content/textures/generic/fulltransparent.tga\",\"png\":\"{0}\",\"fadeout\":\"0.5f\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.4934896 0.4884259\",\"anchormax\":\"0.5065104 0.511574\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]}]";
        string DamageGUI = "[{\"name\":\"hitmarkerDamage\",\"parent\":\"Hud\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{0}\",\"align\":\"MiddleCenter\"},{\"type\":\"UnityEngine.UI.Outline\",\"color\":\"0 0 0 0\",\"distance\":\"0 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.4834896 0.4487038\",\"anchormax\":\"0.5165104 0.4787038\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]}]";
        private string BigIconGUI = "[{\"name\":\"hitmarker.bigicon\",\"parent\":\"Hud\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"sprite\":\"assets/content/textures/generic/fulltransparent.tga\",\"png\":\"{0}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.4869792 0.4768519\",\"anchormax\":\"0.5130208 0.5231481\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]}]";
        string HandleArgs(string json, params object[] args)
        {
            for (int i = 0; i < args.Length; i++)
                json = json.Replace("{" + i + "}", args[i].ToString());
            return json;
        }
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            var victim = entity as BasePlayer;
            if (victim == null || hitInfo == null) return;
            DamageType type = hitInfo.damageTypes.GetMajorityDamageType();
            if (type == null) return;
            var attacker = hitInfo.InitiatorPlayer;
            if (attacker == null) return;
            NextTick(() =>
            {
                var damage =
                    System.Convert.ToInt32(Math.Round(hitInfo.damageTypes.Total(), 0, MidpointRounding.AwayFromZero));
                DamageNotifier(attacker, damage);
            });
        }


        void OnPlayerWound(BasePlayer player)
        {
            var attacker = player?.lastAttacker as BasePlayer;
            if (attacker == null) return;

            DestroyLastCui(attacker);

            CuiHelper.AddUi(attacker,
                HandleArgs(BigIconGUI, Images["hitmarker.hit.wound"]));
            destTimers[attacker] = timer.Once(0.7f, () =>
            {
                CuiHelper.DestroyUi(attacker, "hitmarker.bigicon");
            });
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var player = entity as BasePlayer;
            if (player == null) return;
            var attacker = info?.Initiator as BasePlayer;
            if (attacker == null) return;

            DestroyLastCui(attacker);

            CuiHelper.AddUi(attacker,
                HandleArgs(BigIconGUI, Images["hitmarker.kill"]));
            destTimers[attacker] = timer.Once(0.7f, () =>
            {
                CuiHelper.DestroyUi(attacker, "hitmarker.bigicon");
            });
        }
        #endregion

        #region Core

        void OnDamageTimer()
        {
            float time = Time.time;
            var toRemove = Pool.GetList<BasePlayer>();
            foreach (var dmgHistoryKVP in damageHistory)
            {
                dmgHistoryKVP.Value.RemoveAll(p => p.Key < time);

                DrawDamageNotifier(dmgHistoryKVP.Key);

                if (dmgHistoryKVP.Value.Count == 0)
                    toRemove.Add(dmgHistoryKVP.Key);
            }
            toRemove.ForEach(p => damageHistory.Remove(p));
            Pool.FreeList(ref toRemove);
        }

        void DamageNotifier(BasePlayer player, int damage)
        {
            List<KeyValuePair<float, int>> damages;
            if (!damageHistory.TryGetValue(player, out damages))
                damageHistory[player] = damages = new List<KeyValuePair<float, int>>();
            damages.Insert(0, new KeyValuePair<float, int>(Time.time + damageTimeout, damage));
            if (damages.Count > historyCapacity) damages.RemoveAt(damages.Count - 1);
            DrawDamageNotifier(player);
        }

        string GetDamageArg(BasePlayer player)
        {
            StringBuilder sb = new StringBuilder();
            List<KeyValuePair<float, int>> damages;
            if (!damageHistory.TryGetValue(player, out damages))
                return string.Empty;
            for (var i = 0; i < damages.Count; i++)
            {
                var item = damages[i];
                sb.Append($"<color=#{GetDamageColor(item.Value)}>{item.Value}</color>" + Environment.NewLine);
            }
            return sb.ToString();
        }

        void DestroyLastCui(BasePlayer player)
        {
            Oxide.Plugins.Timer tmr;
            if (destTimers.TryGetValue(player, out tmr))
            {
                tmr?.Callback?.Invoke();
                if (tmr != null && !tmr.Destroyed)
                    timer.Destroy(ref tmr);
            }
        }

        private Color minColor = ColorEx.Parse("1 1 1 1");
        private Color maxColor = ColorEx.Parse("1 0 0 1");
        string GetDamageColor(int damage)
        {
            return ColorToHex(Color.Lerp(minColor, maxColor, (float)damage / 100));
        }

        string ColorToHex(Color32 color)
        {
            string hex = color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2");
            return hex;
        }
        #endregion

        #region UI


        void DrawDamageNotifier(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "hitmarkerDamage");
            CuiHelper.AddUi(player,
                HandleArgs(DamageGUI, GetDamageArg(player)));
        }

        Dictionary<string, string> Images = new Dictionary<string, string>()
        {
            { "hitmarker.kill", "https://media.discordapp.net/attachments/938050321505218613/1117871476431933621/R0NeHWp.png" },
            { "hitmarker.hit.normal", "https://media.discordapp.net/attachments/938050321505218613/1117871476847165460/1.png?width=467&height=467" },
            { "hitmarker.hit.head", "https://media.discordapp.net/attachments/938050321505218613/1117871477220462653/RbXBvH2.png" },
            { "hitmarker.hit.friend", "https://media.discordapp.net/attachments/938050321505218613/1117871476847165460/1.png?width=467&height=467" },
            { "hitmarker.hit.wound", "https://media.discordapp.net/attachments/938050321505218613/1117871476142522458/ZjLZmzu.png?width=467&height=467" },
        };

        #endregion

        #region File Manager

        private GameObject FileManagerObject;
        private FileManager m_FileManager;

        /// <summary>
        /// Инициализация скрипта взаимодействующего с файлами сервера
        /// </summary>
        void InitFileManager()
        {
            FileManagerObject = new GameObject("MAP_FileManagerObject");
            m_FileManager = FileManagerObject.AddComponent<FileManager>();
        }

        class FileManager : MonoBehaviour
        {
            int loaded = 0;
            int needed = 0;

            public bool IsFinished => needed == loaded;
            const ulong MaxActiveLoads = 10;
            Dictionary<string, FileInfo> files = new Dictionary<string, FileInfo>();

            private class FileInfo
            {
                public string Url;
                public string Png;
            }


            public string GetPng(string name) => files[name].Png;


            public IEnumerator LoadFile(string name, string url, int size = -1)
            {
                if (files.ContainsKey(name) && files[name].Url == url && !string.IsNullOrEmpty(files[name].Png)) yield break;
                files[name] = new FileInfo() { Url = url };
                needed++;
                yield return StartCoroutine(LoadImageCoroutine(name, url, size));
            }

            IEnumerator LoadImageCoroutine(string name, string url, int size = -1)
            {
                using (WWW www = new WWW(url))
                {
                    yield return www;
                    if (string.IsNullOrEmpty(www.error))
                    {
                        var bytes = size == -1 ? www.bytes : Resize(www.bytes, size);


                        var entityId = CommunityEntity.ServerInstance.net.ID;
                        var crc32 = FileStorage.server.Store(bytes, FileStorage.Type.png, entityId).ToString();
                        files[name].Png = crc32;
                    }
                }
                loaded++;
            }

            static byte[] Resize(byte[] bytes, int size)
            {
                Image img = (Bitmap)(new ImageConverter().ConvertFrom(bytes));
                Bitmap cutPiece = new Bitmap(size, size);
                System.Drawing.Graphics graphic = System.Drawing.Graphics.FromImage(cutPiece);
                graphic.DrawImage(img, new Rectangle(0, 0, size, size), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel);
                graphic.Dispose();
                MemoryStream ms = new MemoryStream();
                cutPiece.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }
        }

        #endregion
    }
}



