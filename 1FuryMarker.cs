using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime;
using ConVar;
using Facepunch.Utility;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using Rust.Ai;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FuryMarker", "Frizen", "1.0.0")]
    [Description("HitMarker like FuryRust.")]
    public class FuryMarker : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary;

        List<BasePlayer> hitmarkeron = new List<BasePlayer>();

        public string Layer = "Hitmarker";
        public string Layertext = "HitmarkerText";
        public float FadeIn = 0.10f;
        public float FadeOut = 0.10f;

        private Dictionary<string, string> Images = new Dictionary<string, string>
        {
            ["hitmarker.kill"] = "http://i.imgur.com/R0NeHWp.png",
            ["hitmarker.hit.normal"] = "https://cdn.discordapp.com/attachments/1187033764698787961/1187090005588459590/2323232e1637722_1_1.png?ex=65959eb8&is=658329b8&hm=077b365387a86863909f20738c20a152ab0f9a2e32dc9423b5cabde5c89f2c60&",
            ["hitmarker.hit.friend"] = "https://i.imgur.com/kkHy29M.png",
            ["hitmarker.hit.wound"] = "https://i.imgur.com/ZjLZmzu.png"

        };

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

        void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }

            foreach (var check in Images)
                ImageLibrary.Call("AddImage", check.Value, check.Key);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!hitmarkeron.Contains(player))
            {
                hitmarkeron.Add(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            hitmarkeron.Remove(player);
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
            };
        }

        public void ShowText(BaseEntity target, HitInfo info)
        {
            CuiElementContainer container = new CuiElementContainer();
            var obj = target.GetComponent<BaseCombatEntity>();
            if (obj == null) return;
            var attacker = info?.InitiatorPlayer;
            if (attacker == null) return;

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                FadeOut = FadeOut,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 -50", OffsetMax = "50 50" },
            }, "Hud", Layertext);

            if (target is BasePlayer)
            {
                float curHealth = obj.health;
                float maxHealth = obj.MaxHealth();
                string textDamage = info.damageTypes.Total().ToString("F0");

                if (Mathf.FloorToInt(info.damageTypes.Total()) == 0)
                    return;

                container.Add(new CuiElement()
                {
                    Name = ".hitText",
                    Parent = Layertext,
                    FadeOut = FadeOut,
                    Components =
                    {
                        new CuiTextComponent { Text = $"<b>{textDamage}</b>", FadeIn = FadeIn, Color = "1 1 1 1", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, },
                        new CuiOutlineComponent() {Color = "0 0 0 1", Distance = "0.155004182 0.15505041812"},
                        new CuiRectTransformComponent() { AnchorMin = $"0 0", AnchorMax = $"0 0", OffsetMin = "34 15", OffsetMax = "69 35" }
                    }
                });


                CuiHelper.DestroyUi(attacker, ".hitText");
                CuiHelper.DestroyUi(attacker, Layertext);
                CuiHelper.AddUi(attacker, container);
                timer.Once(1.0f, () =>
                {
                    CuiHelper.DestroyUi(attacker, ".hitText");
                    CuiHelper.DestroyUi(attacker, Layertext);
                });
            }
        }

        public void ShowHit(BaseEntity target, HitInfo info, string image = "hitmarker.hit.normal", string color = "1 1 1 1")
        {

            CuiElementContainer container = new CuiElementContainer();
            var obj = target.GetComponent<BaseCombatEntity>();
            if (obj == null) return;
            var attacker = info?.InitiatorPlayer;
            if (attacker == null) return;

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                FadeOut = FadeOut,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 -50", OffsetMax = "50 50" },
            }, "Hud", Layer);


            if (target is BasePlayer)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    FadeOut = FadeOut,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "32 35", OffsetMax = "70 65" },
                    Image = { FadeIn = FadeIn, Color = color, Png = (string)ImageLibrary.Call("GetImage", image) }
                }, Layer, ".hitIcon");



                CuiHelper.DestroyUi(attacker, ".hitIcon");
                CuiHelper.DestroyUi(attacker, ".hitText");
                CuiHelper.DestroyUi(attacker, Layer);
                CuiHelper.AddUi(attacker, container);
                timer.Once(1.0f, () =>
                {
                    CuiHelper.DestroyUi(attacker, ".hitIcon");
                    CuiHelper.DestroyUi(attacker, ".hitText");
                    CuiHelper.DestroyUi(attacker, Layer);
                });
            }
        }

        private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            var target = info.HitEntity as BasePlayer;

            if (attacker.IsNpc || target == null) return;

            if (!hitmarkeron.Contains(attacker)) return;

            NextTick(() =>
            {
                if (target != null && !target.IsDestroyed && !target.IsWounded() && !target.IsDead())
                {
                    ShowHit(attacker, info);
                    ShowText(attacker, info);
                }

                if (info.isHeadshot && target.IsAlive() && !target.IsWounded())
                {
                    ShowHit(attacker, info, color: "255 0 0 1");
                    ShowText(attacker, info);
                }
                if (target.IsWounded())
                {
                    ShowHit(attacker, info, image: "hitmarker.hit.wound");
                }

            });
        }



        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var attacker = info?.InitiatorPlayer;
            if (attacker == null || !(bool)(attacker is BasePlayer) || attacker.IsNpc || attacker.IsDead()) return;
            if (entity is BaseCorpse) return;
            if (entity == null || info == null) return;
            if (entity is BasePlayer)
            {
                if (!info.isHeadshot) ShowHit(attacker, info, image: "hitmarker.kill", color: "0.99 0.99 0.99 1");
                else { ShowHit(attacker, info, image: "hitmarker.kill", color: "255 0 0 1"); }

            }

        }


    }
}