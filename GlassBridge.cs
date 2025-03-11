/*
*  <----- End-User License Agreement ----->
*  Copyright © 2024 Iftebinjan
*  Devoloper: Iftebinjan (Contact: https://discord.gg/HFaGs8YwsH)
*  
*  You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of This Software without the Developer’s consent
*  
*  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO,
*  THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS
*  BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE
*  GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
*  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using VLB;
using System.Collections;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using UnityEngine.UI;
using Network;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("Glass Bridge", "https://discord.gg/TrJ7jnS233", "1.7.1")]
    [Description("The Glass Bridge game")]

    class GlassBridge : RustPlugin
    {
        #region Variables

        [PluginReference]
        Plugin ImageLibrary;

        static GlassBridge plugin;

        private const string glassPrefab = "assets/prefabs/building/wall.window.reinforcedglass/wall.window.glass.reinforced.prefab";
        private const string foundationPrefab = "assets/prefabs/building core/foundation/foundation.prefab";
        private const string entitiesName = "[GlassBridge] Entity";

        private GameObject currentEvent = null;

        private enum EventStatus
        {
            Spawning,
            Waiting,
            Started,
            Finishing,
        };

        #endregion Variables

        #region Hooks

        private void Init()
        {
            plugin = this;

            permission.RegisterPermission(config.permissionName, this);

            foreach (var command in config.commands)
            {
                cmd.AddChatCommand(command, this, nameof(GlassBridgeCommand));
                cmd.AddConsoleCommand(command, this, nameof(GlassBridgeConsoleCommand));
            }
        }

        private void Unload()
        {
            if (currentEvent != null)
            {
                UnityEngine.Object.Destroy(currentEvent);
            }
        }

        private void OnServerInitialized()
        {
            if (config.eventSettings.autoStartCooldown > 0)
            {
                timer.Every(config.eventSettings.autoStartCooldown, () =>
                {
                    if (GetCurrentEvent() != null)
                    {
                        var timespan = TimeSpan.FromSeconds(config.eventSettings.autoStartCooldown);
                        Interface.Oxide.LogDebug($"Unable to start an event automatically because there is already an event currently active. Trying again in {(int)timespan.TotalMinutes}:{timespan.Seconds:00}");
                        return;
                    }

                    StartGlassBridgeEvent();
                });
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var @event = GetCurrentEvent();
            if (@event != null && @event.HasPlayer(player))
            {
                @event.RemovePlayer(player);
            }
        }

        private object OnEntityMarkHostile(BaseCombatEntity entity, float duration)
        {
            if (!(entity is BasePlayer)) return null;
            if (IsPlaying((entity as BasePlayer).userID)) return false;
            return null;
        }

        /* TruePVE hook */
        private object CanEntityTakeDamage(BasePlayer victim, HitInfo info)
        {
            if (victim != null && victim.userID.IsSteamId() && IsPlaying(victim.userID))
            {
                return true;
            }

            return null;
        }

        private object OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            var @event = GetCurrentEvent();
            if (@event != null)
            {
                if (entity is BasePlayer)
                {
                    var player = entity as BasePlayer;
                    if (!@event.HasPlayer(player))
                    {
                        return null;
                    }

                    if (!config.eventSettings.pvpEnabled)
                    {
                        var initiator = info.InitiatorPlayer;
                        if (initiator != null && initiator.userID.IsSteamId())
                        {
                            info.damageTypes.ScaleAll(0f);
                            return true;
                        }
                    }

                    var damageType = info.damageTypes.GetMajorityDamageType();
                    if (damageType == Rust.DamageType.Fall)
                    {
                        info.damageTypes.ScaleAll(0f);
                        return false;
                    }

                    if (@event.Status() != EventStatus.Started)
                    {
                        info.damageTypes.ScaleAll(0f);
                        return false;
                    }
                }

                if (@event.HasGlass(entity))
                {
                    info.damageTypes.ScaleAll(0f);
                    return true;
                }
            }

            return null;
        }

        private void OnRunPlayerMetabolism(PlayerMetabolism metabolism, BasePlayer player)
        {
            GlassBridgeEvent @event = GetCurrentEvent();
            if (@event == null || !@event.HasPlayer(player))
                return;

            if (metabolism.bleeding.value < 1)
                return;

            metabolism.bleeding.value = 0;
        }

        private object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            var @event = GetCurrentEvent();
            if (@event == null || !@event.HasPlayer(player))
            {
                return null;
            }

            if (config.eventSettings.rollbackWhenDie)
            {
                @event.RollbackPlayer(player);
            }
            else
            {
                info.damageTypes.ScaleAll(0f);
                info.DidHit = false;
                info.PointStart = Vector3.zero;

                @event.RemovePlayer(player);
            }

            return true;
        }

        private object OnPlayerWound(BasePlayer player, HitInfo info)
            => OnPlayerDeath(player, info);

        private object OnUserCommand(IPlayer _player, string command, string[] args)
        {
            if (config.commands.Contains(command))
            {
                return null;
            }

            var player = BasePlayer.Find(_player.Id);
            if (player == null || !IsPlaying(player.userID))
            {
                return null;
            }

            if (config.blockedChatCommands.Contains("*") || config.blockedChatCommands.Contains(command))
            {
                SendMessage(player, "CommandBlocked");
                return true;
            }

            return null;
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !IsPlaying(player.userID))
            {
                return null;
            }

            if (config.allowedConsoleCommands.Contains("*"))
            {
                return null;
            }

            var command = arg.cmd.Name;

            var allowedCommands = config.allowedConsoleCommands.Append("glassbridge_ui");
            if (!allowedCommands.Contains(command))
            {
                arg.ReplyWith(Lang("CommandBlocked", player.UserIDString));
                return true;
            }

            return null;
        }

        #endregion Hooks

        #region Commands

        private void GlassBridgeCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                SendMessage(player, "SyntaxError", command);
                return;
            }

            switch (args[0])
            {
                case "start":
                    {
                        if (!HasPermission(player))
                        {
                            SendMessage(player, "NotAllowed");
                            return;
                        }

                        if (currentEvent != null)
                        {
                            SendMessage(player, "AlreadyActive");
                            return;
                        }

                        StartGlassBridgeEvent();
                        SendMessage(player, "YouStartedAnEvent");
                        return;
                    }

                case "stop":
                    {
                        if (!HasPermission(player))
                        {
                            SendMessage(player, "NotAllowed");
                            return;
                        }

                        var @event = GetCurrentEvent();
                        if (@event == null)
                        {
                            SendMessage(player, "NoActiveEvent");
                            return;
                        }

                        if (@event.Status() == GlassBridge.EventStatus.Finishing)
                        {
                            SendMessage(player, "AlreadyBeingFinalized");
                            return;
                        }

                        @event.Finalize(GlassBridgeEvent.FinalizationType.Stop);
                        SendMessage(player, "YouStoppedTheEvent");
                        return;
                    }

                case "join":
                    {
                        var @event = GetCurrentEvent();
                        if (@event == null)
                        {
                            SendMessage(player, "NoActiveEvent");
                            return;
                        }

                        if (@event.HasPlayer(player))
                        {
                            SendMessage(player, "IsAlreadyAtTheEvent");
                            return;
                        }

                        if (@event.Status() == EventStatus.Started)
                        {
                            SendMessage(player, "AlreadyStarted");
                            return;
                        }

                        if (@event.Status() != EventStatus.Waiting)
                        {
                            SendMessage(player, "NoActiveEvent");
                            return;
                        }

                        if (player.IsDead() || player.IsWounded())
                        {
                            SendMessage(player, "DeadOrWounded");
                            return;
                        }

                        if (!player.IsOnGround())
                        {
                            SendMessage(player, "NotOnGround");
                            return;
                        }

                        player.inventory.crafting.CancelAll();
                        @event.AddPlayer(player);
                        SendMessage(player, "Joined");
                        return;
                    }

                case "leave":
                    {
                        var @event = GetCurrentEvent();
                        if (@event == null || (@event.Status() != EventStatus.Started && @event.Status() != EventStatus.Waiting))
                        {
                            SendMessage(player, "NoActiveEvent");
                            return;
                        }

                        if (!@event.HasPlayer(player))
                        {
                            SendMessage(player, "NotParticipating");
                            return;
                        }

                        @event.RemovePlayer(player);
                        SendMessage(player, "Leave");
                        return;
                    }

                default:
                    SendMessage(player, "SyntaxError", command);
                    break;
            }
        }

        private void GlassBridgeConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg == null)
                return;

            BasePlayer player = arg.Player();
            if (!arg.HasArgs())
            {
                arg.ReplyWith(HumanizedLang("ConsoleSyntaxError", player, arg.cmd.Name));
                return;
            }

            switch (arg.Args[0])
            {
                case "start":
                    {
                        if (player != null && !HasPermission(player))
                        {
                            arg.ReplyWith(HumanizedLang("NotAllowed", player));
                            return;
                        }

                        if (currentEvent != null)
                        {
                            arg.ReplyWith(HumanizedLang("AlreadyActive", player));
                            return;
                        }

                        StartGlassBridgeEvent();
                        arg.ReplyWith(HumanizedLang("YouStartedAnEvent", player));
                        return;
                    }

                case "stop":
                    {
                        if (player != null && !HasPermission(player))
                        {
                            arg.ReplyWith(HumanizedLang("NotAllowed", player));
                            return;
                        }

                        var @event = GetCurrentEvent();
                        if (@event == null)
                        {
                            arg.ReplyWith(HumanizedLang("NoActiveEvent", player));
                            return;
                        }

                        if (@event.Status() == GlassBridge.EventStatus.Finishing)
                        {
                            arg.ReplyWith(HumanizedLang("AlreadyBeingFinalized", player));
                            return;
                        }

                        @event.Finalize(GlassBridgeEvent.FinalizationType.Stop);
                        arg.ReplyWith(HumanizedLang("YouStoppedTheEvent", player));
                        return;
                    }

                default:
                    arg.ReplyWith(HumanizedLang("ConsoleSyntaxError", player, arg.cmd.Name));
                    break;
            }
        }

        [ConsoleCommand("glassbridge_ui")]
        private void consoleCommandGlassBridgeJoin(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !arg.HasArgs(1))
            {
                return;
            }

            var subcommand = arg.Args[0];
            player.SendConsoleCommand($"chat.say", $"/{config.commands.FirstOrDefault()} {subcommand}");
        }

        #endregion Commands

        #region Methods

        public void StartGlassBridgeEvent()
        {
            if (GetCurrentEvent() != null)
            {
                return;
            }

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = config.eventSettings.position;

            var collider = sphere.GetComponent<SphereCollider>();
            collider.radius = config.glassesSettings.rows * config.glassesSettings.columns + 50;

            var trigger = sphere.GetOrAddComponent<TriggerBase>();
            trigger.interestLayers = LayerMask.GetMask("Player (Server)");
            trigger.enabled = true;

            var @event = sphere.AddComponent<GlassBridgeEvent>();
            currentEvent = sphere;
        }

        private GlassBridgeEvent GetCurrentEvent()
        {
            return currentEvent?.GetComponent<GlassBridgeEvent>();
        }

        public Item GiveItem(ItemContainer container, string name, int amount, int loadedAmmo = 0, ulong skinId = 0L, string customName = "")
        {
            var item = ItemManager.CreateByName(name, amount, skinId);
            if (item != null)
            {
                if (loadedAmmo > 0)
                {
                    var projectile = item.GetHeldEntity()?.GetComponent<BaseProjectile>();
                    if (projectile != null)
                    {
                        projectile.primaryMagazine.contents = Math.Min((int)loadedAmmo, projectile.primaryMagazine.capacity);
                    }
                }

                if (!string.IsNullOrEmpty(customName))
                {
                    item.name = customName;
                }

                item.MoveToContainer(container, allowStack: false, ignoreStackLimit: true);
            }

            return item;
        }

        private void BroadcastMessage(string message, params object[] args)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                SendMessage(player, message, args);
            }
        }

        private void SendMessage(BasePlayer player, string message, params object[] args)
            => rust.SendChatMessage(
                player,
                Lang("ChatName", player.UserIDString),
                string.Format(Lang(message, player.UserIDString), args)
            );

        #endregion Methods

        #region API

        private bool IsActive()
            => GetCurrentEvent() != null;

        private bool IsPlaying(string playerId)
        {
            var @event = GetCurrentEvent();
            if (@event != null)
            {
                return @event.HasPlayer(playerId);
            }

            return false;
        }

        private bool IsPlaying(ulong playerId)
            => IsPlaying(playerId.ToString());

        private void RemovePlayer(string playerId)
        {
            var @event = GetCurrentEvent();
            if (@event != null)
            {
                @event.RemovePlayer(playerId);
            }
        }

        private void RemovePlayer(ulong playerId)
            => RemovePlayer(playerId.ToString());

        private List<BasePlayer> PlayingList()
        {
            var playing = new List<BasePlayer>();

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (IsPlaying(player.userID))
                {
                    playing.Add(player);
                }
            }

            return playing;
        }

        #endregion API

        #region ImageLibrary

        private bool AddImage(string url, string name)
            => (bool)ImageLibrary?.Call("AddImage", url, name);

        private string GetImage(string name)
            => ImageLibrary?.Call<string>("GetImage", name);

        private bool HasImage(string name)
            => (bool)ImageLibrary?.Call("HasImage", name);

        #endregion ImageLibrary

        #region Helpers

        private bool HasPermission(BasePlayer player)
            => player.IsAdmin || permission.UserHasPermission(player.UserIDString, config.permissionName);

        private int[] GetRandomNumbersBetweenRange(int start, int end, int amount)
        {
            if (end - start < amount)
            {
                return new int[] { };
            }

            var result = new int[amount];
            for (int i = 0; i < amount; i++)
            {
                int num = UnityEngine.Random.Range(start, end + 1);
                if (!result.Contains(num))
                {
                    result[i] = num;
                    continue;
                }
                i--;
            }

            return result;
        }

        private long DateNow()
        {
            return (long)(DateTime.Now.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds;
        }

        #endregion Helpers

        #region UI

        private void CreateJoinLeaveUI(BasePlayer player)
        {
            var container = new CuiElementContainer();
            var @event = GetCurrentEvent();
            if (@event == null || @event.Status() != EventStatus.Waiting)
            {
                CuiHelper.DestroyUi(player, "JoinLeavePanel");
                return;
            }

            var settings = config.UI.joinLeave;
            var isPlaying = IsPlaying(player.UserIDString);

            UIBuilder.CreatePanel(ref container, "Overlay", settings.color, settings.anchor, settings.offset, name: "JoinLeavePanel", destroyUi: "JoinLeavePanel");
            UIBuilder.CreatePanel(ref container, "JoinLeavePanel", "0 1 1 1", "0 1 1 1", "0 -2 0 0");

            // Title
            UIBuilder.CreateLabel(ref container, "JoinLeavePanel", Lang("GlassBridgeTitle", player.UserIDString), "0 1 1 1", "0 -56 0 0", align: TextAnchor.MiddleCenter);

            // Button
            UIBuilder.CreatePanel(ref container, "JoinLeavePanel", isPlaying ? settings.button.leaveColor : settings.button.joinColor, "0 0 1 0", "4 4 -4 36", name: "JoinLeaveButton");
            UIBuilder.CreateLabel(ref container, "JoinLeaveButton", Lang(isPlaying ? "LeaveButton" : "JoinButton", player.UserIDString), "0 0 1 1", "", align: TextAnchor.MiddleCenter);
            container.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Color = "0 0 0 0", Command = $"glassbridge_ui {(isPlaying ? "leave" : "join")}" }
                },
                "JoinLeaveButton"
            );

            UIBuilder.CreatePanel(ref container, "JoinLeavePanel", "1 1 1 .1", "0 0 1 0", "4 68 -4 92", name: "JoinLeaveInfoPlayers");
            UIBuilder.CreatePanel(ref container, "JoinLeaveInfoPlayers", null, "0 .5 0 .5", "4 -7 18 7", image: "https://i.ibb.co/0msdX54/image.png");
            UIBuilder.CreateLabel(ref container, "JoinLeaveInfoPlayers", $"<size=12><b>{@event?.GetRemainingPlayersAmount() ?? 0}</b></size>", "0 0 1 1", "0 0 0 0", align: TextAnchor.MiddleCenter);

            TimeSpan timespan = TimeSpan.FromSeconds(@event.waitStartedAt != null ? Math.Max(config.eventSettings.waitDuration - (DateNow() - (int)@event.waitStartedAt), 0) : 0);
            UIBuilder.CreatePanel(ref container, "JoinLeavePanel", "1 1 1 .1", "0 0 1 0", "4 40 -4 64", name: "JoinLeaveInfoTime");
            UIBuilder.CreatePanel(ref container, "JoinLeaveInfoTime", null, "0 .5 0 .5", "4 -7 18 7", image: "https://i.ibb.co/Fb5WsqH/image.png");
            UIBuilder.CreateLabel(ref container, "JoinLeaveInfoTime", $"<size=12><b>{(int)timespan.TotalMinutes}:{timespan.Seconds:00}</b></size>", "0 0 1 1", "0 0 0 0", align: TextAnchor.MiddleCenter);

            CuiHelper.AddUi(player, container);
        }

        private void UpdateEventStatusUI(BasePlayer player, int remainingPlayers, long remainingTime = 0)
        {
            var container = new CuiElementContainer();

            var timespan = TimeSpan.FromSeconds(remainingTime);
            var minutes = $"{(config.eventSettings.autoStopAfter > 0 ? ((int)timespan.TotalMinutes).ToString() : "--")}";
            var seconds = $"{(config.eventSettings.autoStopAfter > 0 ? timespan.Seconds.ToString() : "--")}";

            UIBuilder.CreateLabel(ref container, "EventStatusRemainingPlayersBox", string.Format(Lang("EventStatusRemainingPlayersText", player.UserIDString), remainingPlayers), "0 0 1 1", "4 0 -4 0", align: TextAnchor.MiddleCenter, name: "EventStatusRemainingPlayersText", destroyUi: "EventStatusRemainingPlayersText");
            UIBuilder.CreateLabel(ref container, "EventStatusRemainingTimeBox", string.Format(Lang("EventStatusRemainingTimeText", player.UserIDString), minutes, seconds.PadLeft(2, '0')), "0 0 1 1", "4 0 -4 0", align: TextAnchor.MiddleCenter, name: "EventStatusRemainingTimeText", destroyUi: "EventStatusRemainingTimeText");

            CuiHelper.AddUi(player, container);
        }

        private void CreateEventStatusUI(BasePlayer player, int remainingPlayers, long remainingTime = 0)
        {
            var container = new CuiElementContainer();

            var UISettings = config.UI.eventStatus;
            UIBuilder.CreatePanel(ref container, "Hud", "0 0 0 0", UISettings.anchor, UISettings.offset, name: "EventStatus", destroyUi: "EventStatus");
            UIBuilder.CreatePanel(ref container, "EventStatus", UISettings.remainingPlayers.color, UISettings.remainingPlayers.anchor, UISettings.remainingPlayers.offset, name: "EventStatusRemainingPlayersBox");
            UIBuilder.CreatePanel(ref container, "EventStatus", UISettings.remainingTime.color, UISettings.remainingTime.anchor, UISettings.remainingTime.offset, name: "EventStatusRemainingTimeBox");

            UIBuilder.CreatePanel(ref container, "EventStatusRemainingPlayersBox", "1 1 1 1", UISettings.remainingPlayers.iconAnchor, UISettings.remainingPlayers.iconOffset, image: UISettings.remainingPlayers.iconUrl);

            CuiHelper.AddUi(player, container);

            UpdateEventStatusUI(player, remainingPlayers, remainingTime);
        }

        private void CreateStartCountdownUI(BasePlayer player, string text, bool isLast = false)
        {
            var container = new CuiElementContainer();

            UIBuilder.CreateLabel(ref container, "Hud", text, "0 0 1 1", "", .1f, .3f, TextAnchor.MiddleCenter, "StartCountdown", "StartCountdown");
            Effect.server.Run(isLast ? "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab" : "assets/prefabs/locks/keypad/effects/lock.code.lock.prefab", player, 0, new Vector3(), player.transform.position, null, false);

            CuiHelper.AddUi(player, container);
        }

        private void CreateGiveTimeWarnUI(BasePlayer player, string text, string color)
        {
            var container = new CuiElementContainer();

            UIBuilder.CreatePanel(ref container, "Hud", "0 0 0 .65", "0.5 0 0.5 0", "-128 100 128 136", .3f, .3f, name: "GiveTimeWarn", destroyUi: "GiveTimeWarn", material: "assets/content/ui/uibackgroundblur-ingamemenu.mat");
            UIBuilder.CreatePanel(ref container, "GiveTimeWarn", color, "0 0 0 1", "0 0 3 0");
            UIBuilder.CreatePanel(ref container, "GiveTimeWarn", color, "1 0 1 1", "-3 0 0 0");

            UIBuilder.CreateLabel(ref container, "GiveTimeWarn", text, "0 0 1 1", $"12 0 -12 0", align: TextAnchor.MiddleCenter, fontSize: 12);

            CuiHelper.AddUi(player, container);
        }

        private class UIBuilder
        {
            public static void CreatePanel(ref CuiElementContainer container, string parent, string color, string anchor, string offset, float fadeIn = 0f, float fadeOut = 0f, string image = null, int itemId = 0, string name = null, string destroyUi = null, string material = null)
            {
                var _anchor = ParseMinMax(anchor);
                var _offset = ParseMinMax(offset);

                if (string.IsNullOrEmpty(image))
                {
                    container.Add(
                        new CuiPanel
                        {
                            FadeOut = fadeOut,
                            RectTransform = {
                                AnchorMin = _anchor[0],
                                AnchorMax = _anchor[1],
                                OffsetMin = _offset[0],
                                OffsetMax = _offset[1],
                            },
                            Image = {
                                Color = color,
                                ItemId = itemId,
                                FadeIn = fadeIn,
                                Material = material,
                            }
                        },
                        parent,
                        name,
                        destroyUi
                    );
                }
                else
                {
                    if (!plugin.HasImage(image))
                    {
                        plugin.AddImage(image, image);
                    }

                    container.Add(
                        new CuiElement
                        {
                            Name = name,
                            Parent = parent,
                            DestroyUi = destroyUi,
                            FadeOut = fadeOut,
                            Components = {
                                new CuiRectTransformComponent {
                                    AnchorMin = _anchor[0],
                                    AnchorMax = _anchor[1],
                                    OffsetMin = _offset[0],
                                    OffsetMax = _offset[1],
                                },
                                new CuiRawImageComponent {
                                    Url = plugin.HasImage(image) ? null : image,
                                    Png = plugin.HasImage(image) ? plugin.GetImage(image) : null,
                                    FadeIn = fadeIn,
                                }
                            }
                        }
                    );
                }
            }

            public static void CreateLabel(ref CuiElementContainer container, string parent, string text, string anchor, string offset, float fadeIn = 0f, float fadeOut = 0f, TextAnchor align = TextAnchor.MiddleLeft, string name = null, string destroyUi = null, int fontSize = 14)
            {
                var _anchor = ParseMinMax(anchor);
                var _offset = ParseMinMax(offset);

                container.Add(
                    new CuiLabel
                    {
                        FadeOut = fadeOut,
                        RectTransform = {
                            AnchorMin = _anchor[0],
                            AnchorMax = _anchor[1],
                            OffsetMin = _offset[0],
                            OffsetMax = _offset[1],
                        },
                        Text = {
                            Color = "1 1 1 1",
                            Text = text,
                            FadeIn = fadeIn,
                            Align = align,
                            FontSize = fontSize,
                            Font = "robotocondensed-regular.ttf"
                        }
                    },
                    parent,
                    name,
                    destroyUi
                );
            }

            private static string[] ParseMinMax(string value)
            {
                var array = value.Split(' ');
                return new string[] {
                    string.Join(" ", array.Take(2)),
                    string.Join(" ", array.Skip(2)),
                };
            }
        }

        #endregion UI

        #region Structures

        private class Glass : MonoBehaviour
        {
            public bool isBroken = false;


            private BaseEntity entity;
            private BoxCollider collider;

            private void Awake()
            {
                entity = GetComponent<BaseEntity>();
                if (entity == null)
                {
                    Destroy(this);
                    return;
                }

                entity.name = "[GlassBridge] Glass Entity";

                var stability = GetComponent<StabilityEntity>();
                stability.grounded = true;

                collider = this.GetOrAddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.center = entity.bounds.center;
                collider.size = entity.bounds.size + new Vector3(3f, 0f, 0f);

                gameObject.layer = (int)Rust.Layer.Reserved1;
                gameObject.name = "[GlassBridge] Glass"; ;
            }

            private void OnTriggerEnter(Collider col)
            {
                var player = col.GetComponent<BasePlayer>();
                if (player == null || !player.userID.IsSteamId())
                {
                    return;
                }

                if (isBroken)
                {
                    var @event = plugin.GetCurrentEvent();
                    if (!@event)
                    {
                        Destroy(this);
                        return;
                    }

                    if (@event.Status() == EventStatus.Started && @event.HasPlayer(player))
                    {
                        @event.DestroyGlass(this);
                    }
                }
            }

            private void OnDestroy()
            {
                if (entity != null && !entity.IsDestroyed)
                {
                    entity.AdminKill();
                }
            }
        }

        private class EventPlayer : MonoBehaviour
        {
            public BasePlayer player { get; private set; }
            private PlayerBackup backup;

            private int startCountdown = 3;
            private bool isFreeze = false;
            private Vector3? freezePosition = null;
            private bool isWinner = false;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
            }

            private void Update()
            {
                var @event = plugin.GetCurrentEvent();
                if (!@event)
                {
                    Destroy(this);
                    return;
                }

                if (@event.Status() == EventStatus.Started)
                {
                    if (isFreeze && freezePosition != null)
                    {
                        player.Teleport(freezePosition.Value);
                    }

                    if (@event.GetPlayerEventPosition(player) == GlassBridgeEvent.PlayerEventPosition.Arrival)
                    {
                        @event.Finalize(GlassBridgeEvent.FinalizationType.Winner, player);
                        return;
                    }
                }

                if (transform.position.y < plugin.config.eventSettings.position.y - 20f)
                {
                    if (plugin.config.eventSettings.loseWhenFall && @event.Status() == EventStatus.Started && @event.startedAt != null)
                    {
                        @event.RemovePlayer(player);
                        return;
                    }

                    player.Teleport(@event.GetRandomSpawnPoint());
                }
            }

            private void OnDestroy()
            {
                CancelInvoke(nameof(ShowStartCountdown));
                CancelInvoke(nameof(DestroyCountdown));
                CancelInvoke(nameof(UpdateEventStatusUI));

                DestroyAllUI();
                Restore();
            }


            private void Restore()
            {
                player.metabolism.Reset();
                player.SetHealth(backup.metabolism.health);
                player.metabolism.bleeding.value = backup.metabolism.bleeding;
                player.metabolism.calories.value = backup.metabolism.calories;
                player.metabolism.hydration.value = backup.metabolism.hydration;
                player.metabolism.oxygen.value = backup.metabolism.oxygen;
                player.metabolism.poison.value = backup.metabolism.poison;
                player.metabolism.radiation_level.value = backup.metabolism.radiation_level;
                player.metabolism.radiation_poison.value = backup.metabolism.radiation_poison;
                player.metabolism.wetness.value = backup.metabolism.wetness;
                player.metabolism.temperature.value = backup.metabolism.temperature;

                player.inventory.Strip();
                backup.RestorePlayer();

                Teleport(backup.position);

                backup = null;

                if (isWinner)
                {
                    /* Give winner items */
                    foreach (var item in plugin.config.prizeSettings.items)
                    {
                        var container = player.inventory.containerMain.itemList.Count() < 24
                            ? player.inventory.containerMain
                            : player.inventory.containerBelt;

                        plugin.GiveItem(container, item.shortname, item.amount, 0, item.skinId, item.customName);
                    }

                    /* Execute server commnads */
                    foreach (var command in plugin.config.prizeSettings.commands)
                    {
                        var args = command
                            .Replace("{winner_id}", player.UserIDString)
                            .Replace("{winner_name}", player.displayName)
                            .Split(' ');

                        plugin.Server.Command(args[0], args.Skip(1));
                    }
                }
            }

            public void SetWinner()
            {
                isWinner = true;
            }

            public void Teleport(Vector3 position)
            {
                if (!player.IsValid()) return;
                try
                {
                    player.UpdateActiveItem(new ItemId(0u));
                    player.EnsureDismounted();

                    if (player.HasParent())
                        player.SetParent(null, true, true);

                    if (player.IsConnected)
                    {
                        player.EndLooting();
                        if (!player.IsSleeping())
                        {
                            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
                            player.sleepStartTime = Time.time;
                            BasePlayer.sleepingPlayerList.Add(player);
                            player.CancelInvoke("InventoryUpdate");
                            player.CancelInvoke("TeamUpdate");
                        }
                    }

                    player.RemoveFromTriggers();
                    player.Teleport(position);

                    if (player.IsConnected && !Net.sv.visibility.IsInside(player.net.group, position))
                    {
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
                        player.ClientRPCPlayer(null, player, "StartLoading");
                        player.SendEntityUpdate();
                        player.UpdateNetworkGroup();
                        player.SendNetworkUpdateImmediate(false);
                    }
                }
                finally
                {
                    player.ForceUpdateTriggers();
                }
            }

            public void SetFreeze(bool freeze)
            {
                if (freeze)
                {
                    isFreeze = true;
                    freezePosition = player.transform.position;
                }
                else
                {
                    isFreeze = false;
                    freezePosition = null;
                }
            }

            public void Heal()
            {
                player.SetHealth(player._maxHealth);
                player.metabolism.bleeding.value = 0;
                player.metabolism.calories.min = 0;
                player.metabolism.calories.value = 500;
                player.metabolism.hydration.min = 0;
                player.metabolism.hydration.value = 250;
                player.metabolism.oxygen.value = 1;
                player.metabolism.poison.value = 0;
                player.metabolism.radiation_level.value = 0;
                player.metabolism.radiation_poison.value = 0;
                player.metabolism.wetness.value = 0;
                player.metabolism.temperature.min = 32;
                player.metabolism.temperature.max = 32;
                player.metabolism.temperature.value = 32;
                player.metabolism.SendChangesToClient();
            }

            public void CreateBackup()
            {
                backup = new PlayerBackup(player);
            }


            public void ShowGiveTimeWarn(string text, string color)
            {
                if (IsInvoking(nameof(DestroyGiveTimeWarn)))
                {
                    CancelInvoke(nameof(DestroyGiveTimeWarn));
                }

                plugin.CreateGiveTimeWarnUI(player, text, color);
                Invoke(nameof(DestroyGiveTimeWarn), 6f);
            }

            public void ShowStartCountdown()
            {
                plugin.CreateStartCountdownUI(player, plugin.Lang($"StartCountdown_{startCountdown}", player.UserIDString), !(startCountdown > 0));
                Invoke(nameof(DestroyCountdown), .7f);

                if (startCountdown > 0)
                {
                    startCountdown--;
                    Invoke(nameof(ShowStartCountdown), 1f);
                }
            }

            public void ShowEventStatus(int remainingPlayers, long remainingTime = 0)
            {
                plugin.CreateEventStatusUI(player, remainingPlayers, remainingTime);
                InvokeRepeating(nameof(UpdateEventStatusUI), 1f, 1f);
            }

            public void UpdateEventStatusUI()
            {
                var @event = plugin.GetCurrentEvent();
                if (!@event)
                {
                    Destroy(this);
                    return;
                }

                long remainingTime = plugin.config.eventSettings.autoStopAfter;
                if (@event.startedAt != null && plugin.config.eventSettings.autoStopAfter > 0)
                {
                    var elapsed = plugin.DateNow() - @event.startedAt;
                    remainingTime = Math.Max(plugin.config.eventSettings.autoStopAfter - (int)elapsed, 0);
                }

                plugin.UpdateEventStatusUI(
                    player,
                    @event.GetRemainingPlayersAmount(),
                    remainingTime
                );
            }

            private void DestroyGiveTimeWarn()
            {
                CuiHelper.DestroyUi(player, "GiveTimeWarn");
            }

            private void DestroyCountdown()
            {
                CuiHelper.DestroyUi(player, "StartCountdown");
            }

            private void DestroyEventStatus()
            {
            }

            private void DestroyAllUI()
            {
                DestroyGiveTimeWarn();
                DestroyCountdown();
                CuiHelper.DestroyUi(player, "EventStatus");
            }
        }

        private class GlassBridgeEvent : MonoBehaviour
        {
            private EventStatus status = EventStatus.Spawning;
            private List<EventPlayer> players = new List<EventPlayer>();
            private List<Glass> glasses = new List<Glass>();
            private List<BaseEntity> foundations = new List<BaseEntity>();
            private List<BaseEntity> arrivalFoundations = new List<BaseEntity>();

            public long? startedAt { get; private set; }
            public long? waitStartedAt { get; private set; }

            public enum PlayerEventPosition
            {
                Lobby,
                Bridge,
                Arrival,
            };

            public enum FinalizationType
            {
                Winner,
                Stop,
                AutoStop,
                NoPlayers,
                MinPlayers,
            };

            private void Awake()
            {
                gameObject.layer = (int)Rust.Layer.Reserved1;
                gameObject.name = "[GlassBridge] Event";

                var sphere = GetComponent<SphereCollider>();
                sphere.isTrigger = true;
            }

            private void Start()
            {
                StartCoroutine(CreateLobby());
            }

            private void OnTriggerEnter(Collider col)
            {
                var player = col?.GetComponent<BasePlayer>();
                if (player == null || !player.userID.IsSteamId())
                {
                    return;
                }

                if (!IsSafePlayer(player))
                {
                    player.Invoke(() => player.Hurt(1000f, Rust.DamageType.Explosion, null, false), 0.25f);
                }
            }

            private void OnTriggerExit(Collider col)
            {
                var player = col?.GetComponent<BasePlayer>();
                if (player == null || !HasPlayer(player))
                {
                    return;
                }

                var eventPlayer = player.GetComponent<EventPlayer>();
                players.Remove(eventPlayer);
                Destroy(eventPlayer);

                plugin.SendMessage(player, "LeftFromEventArea");

                if (players.Count < 1 && status == EventStatus.Started)
                {
                    Finalize(FinalizationType.NoPlayers);
                }
            }

            private void OnDestroy()
            {
                CancelInvoke(nameof(ShowPlayersJoinLeaveUI));
                DestroyPlayersJoinLeaveUI();

                foreach (var eventPlayer in players)
                {
                    Destroy(eventPlayer);
                }

                foreach (var glass in glasses)
                {
                    Destroy(glass);
                }

                foreach (var foundation in foundations)
                {
                    foundation?.AdminKill();
                }

                foreach (var foundation in arrivalFoundations)
                {
                    foundation?.AdminKill();
                }

                plugin.currentEvent = null;
            }


            private IEnumerator StartEvent()
            {
                status = EventStatus.Waiting;
                waitStartedAt = plugin.DateNow();

                Interface.CallHook("OnGlassBridgeEventStarted");
                plugin.BroadcastMessage("EventStarted", plugin.config.commands[0]);

                InvokeRepeating(nameof(ShowPlayersJoinLeaveUI), 0, 1f);

                yield return new WaitForSeconds(plugin.config.eventSettings.waitDuration);

                CancelInvoke(nameof(ShowPlayersJoinLeaveUI));
                DestroyPlayersJoinLeaveUI();

                status = EventStatus.Started;
                waitStartedAt = null;

                if (players.Count < Math.Max(plugin.config.eventSettings.minPlayers, 1))
                {
                    Finalize(FinalizationType.MinPlayers);
                    yield break;
                }

                foreach (var eventPlayer in players)
                {
                    var player = eventPlayer.player;
                    player.Teleport(GetRandomSpawnPoint());
                    eventPlayer.SetFreeze(true);
                    eventPlayer.ShowStartCountdown();
                    eventPlayer.ShowEventStatus(players.Count, plugin.config.eventSettings.autoStopAfter);
                }

                yield return new WaitForSeconds(4f);

                startedAt = plugin.DateNow();

                foreach (var eventPlayer in players)
                {
                    eventPlayer.SetFreeze(false);
                }

                if (plugin.config.eventSettings.autoStopAfter > 0)
                {
                    Invoke(nameof(AutoStop), plugin.config.eventSettings.autoStopAfter);
                }

                InvokeRepeating(nameof(TryGiveTime), 0f, 1f);
            }

            public void Finalize(FinalizationType type, BasePlayer winner = null)
            {
                status = EventStatus.Finishing;

                CancelInvoke(nameof(AutoStop));
                CancelInvoke(nameof(TryGiveTime));

                Interface.CallHook("OnGlassBridgeEventEnded", winner);

                switch (type)
                {
                    case FinalizationType.Winner:
                        {
                            Destroy(this, 5f);
                            if (winner != null && HasPlayer(winner))
                            {
                                winner.GetComponent<EventPlayer>().SetWinner();
                                plugin.BroadcastMessage("WinnerBroadcast", winner.displayName);

                                foreach (var eventPlayer in players)
                                {
                                    var player = eventPlayer.player;
                                    plugin.SendMessage(player, "WaitForTeleport");
                                }
                            }
                            break;
                        }

                    case FinalizationType.Stop:
                        {
                            Destroy(this);
                            plugin.BroadcastMessage("Stopped");
                            break;
                        }

                    case FinalizationType.AutoStop:
                        {
                            Destroy(this);
                            plugin.BroadcastMessage("AutoStopped");
                            break;
                        }

                    case FinalizationType.NoPlayers:
                        {
                            Destroy(this);
                            plugin.BroadcastMessage("NoPlayers");
                            break;
                        }

                    case FinalizationType.MinPlayers:
                        {
                            Destroy(this);
                            plugin.BroadcastMessage("MinPlayers", Math.Max(plugin.config.eventSettings.minPlayers, 1));
                            break;
                        }
                }
            }

            public void AutoStop()
                => Finalize(FinalizationType.AutoStop);

            private void TryGiveTime()
            {
                var elapsed = plugin.DateNow() - startedAt;
                if (elapsed == null)
                {
                    return;
                }

                var gives = plugin.config.eventSettings.giveTimes
                    .Where((x) => x.time == elapsed);

                foreach (var give in gives)
                {
                    foreach (var eventPlayer in players)
                    {
                        var player = eventPlayer.player;
                        var container = give.container == "belt"
                            ? player.inventory.containerBelt
                            : give.container == "wear"
                                ? player.inventory.containerWear
                                : player.inventory.containerMain;

                        Item item = plugin.GiveItem(container, give.shortname, give.amount, give.loadedAmmo, give.skinId);
                        if (give.showWarn && item != null)
                        {
                            eventPlayer.ShowGiveTimeWarn(string.Format(plugin.Lang(give.langKey, player.UserIDString), give.amount, item.info.displayName.english), give.warnBgColor);
                        }
                    }
                }
            }

            public EventStatus Status()
                => status;

            private IEnumerator CreateLobby()
            {
                var columns = plugin.config.glassesSettings.columns;

                for (int i = 0; i < (3.5 * columns / 3); i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        var entity = GameManager.server.CreateEntity(foundationPrefab, plugin.config.eventSettings.position + new Vector3(i * 3, 0, j * 3));
                        entity.name = entitiesName;
                        entity.Spawn();

                        var build = entity.GetComponent<BuildingBlock>();
                        if (build != null)
                        {
                            build.SetGrade(BuildingGrade.Enum.Stone);
                            build.SetHealthToMax();
                        }

                        foundations.Add(entity);
                    }
                    yield return new WaitForFixedUpdate();
                }

                yield return StartCoroutine(CreateGlasses());
            }

            private IEnumerator CreateArrival()
            {
                var columns = plugin.config.glassesSettings.columns;
                var rows = plugin.config.glassesSettings.rows;

                for (int i = 0; i < (3.5 * columns / 3); i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        var pos = new Vector3(i * 3f, 0f, j * 3f + rows * 3f + 12f + 1.7f);
                        var entity = GameManager.server.CreateEntity(
                            foundationPrefab,
                            plugin.config.eventSettings.position + pos
                        );
                        entity.name = entitiesName;
                        entity.Spawn();

                        var build = entity.GetComponent<BuildingBlock>();
                        if (build != null)
                        {
                            build.SetGrade(BuildingGrade.Enum.Metal);
                            build.SetHealthToMax();
                        }

                        arrivalFoundations.Add(entity);
                    }
                    yield return new WaitForFixedUpdate();
                }

                yield return StartCoroutine(StartEvent());
            }

            private IEnumerator CreateGlasses()
            {
                var rows = plugin.config.glassesSettings.rows;
                var columns = plugin.config.glassesSettings.columns;
                var brokenByRow = plugin.config.glassesSettings.brokenByRow;

                int[] lastSafeGlasses = null;
                for (int i = 0; i < rows; i++)
                {
                    var brokenGlasses = GenerateBrokenGlassesNumbers(columns, Math.Min(brokenByRow, columns - 1), lastSafeGlasses);

                    lastSafeGlasses = Enumerable
                        .Range(1, rows)
                        .Where((x) => !brokenGlasses.Contains(x))
                        .ToArray();

                    float startX = (columns * 3.5f - 1.3f) - (3.5f * columns / 3f) * 3f;
                    for (int j = 0; j < columns; j++)
                    {
                        var pos = plugin.config.eventSettings.position;
                        pos.x -= startX;

                        CreateGlass(pos + new Vector3(j * 3.5f, 0f, 12f + i * 3f), brokenGlasses.Contains(j + 1));
                        yield return new WaitForFixedUpdate();
                    }
                }

                yield return StartCoroutine(CreateArrival());
            }

            private void CreateGlass(Vector3 position, bool isBroken = false)
            {
                var entity = GameManager.server.CreateEntity(glassPrefab, position, new Quaternion(1f, 1f, 1f, 1));
                entity.name = entitiesName;
                entity.Spawn();

                var glass = entity.GetOrAddComponent<Glass>();
                glass.isBroken = isBroken;
                glasses.Add(glass);
            }

            public void DestroyGlass(Glass glass)
            {
                if (glasses.Contains(glass))
                {
                    glasses.Remove(glass);
                }

                Destroy(glass);
            }

            public bool HasGlass(BaseEntity entity)
            {
                var glass = entity.GetComponent<Glass>();
                return glass != null && glasses.Contains(glass);
            }

            public PlayerEventPosition GetPlayerEventPosition(BasePlayer player)
            {
                if (foundations.Where((x) => !x.IsDestroyed).Any((x) => x.Distance(player) <= 1f))
                {
                    return PlayerEventPosition.Lobby;
                }

                if (arrivalFoundations.Where((x) => !x.IsDestroyed).Any((x) => x.Distance(player) <= 1f))
                {
                    return PlayerEventPosition.Arrival;
                }

                return PlayerEventPosition.Bridge;
            }

            public void AddPlayer(BasePlayer player)
            {
                if (HasPlayer(player))
                {
                    return;
                }

                var eventPlayer = player.GetOrAddComponent<EventPlayer>();
                players.Add(eventPlayer);

                eventPlayer.CreateBackup();
                eventPlayer.player.inventory.Strip();
                eventPlayer.Heal();
                eventPlayer.Teleport(GetRandomSpawnPoint());
            }

            public void RemovePlayer(string playerId)
            {
                if (!HasPlayer(playerId))
                {
                    return;
                }

                var eventPlayer = players.Find((x) => x.player.UserIDString == playerId);
                players.Remove(eventPlayer);
                Destroy(eventPlayer);

                if (players.Count < 1 && status == EventStatus.Started)
                {
                    Finalize(FinalizationType.NoPlayers);
                }
            }

            public void RemovePlayer(BasePlayer player)
                => RemovePlayer(player.UserIDString);

            public bool HasPlayer(string playerId)
            {
                var eventPlayer = players.Find((x) => x.player.UserIDString == playerId);
                return eventPlayer != null;
            }

            public bool HasPlayer(BasePlayer player)
                => HasPlayer(player.UserIDString);

            public void RollbackPlayer(BasePlayer player)
            {
                EventPlayer eventPlayer = player.GetComponent<EventPlayer>();
                if (eventPlayer == null)
                    return;

                Vector3 spawnPoint = GetRandomSpawnPoint();
                eventPlayer.Heal();
                player.Teleport(spawnPoint);
            }

            public void ShowPlayersJoinLeaveUI()
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    plugin.CreateJoinLeaveUI(player);
                }
            }

            public void DestroyPlayersJoinLeaveUI()
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, "JoinLeavePanel");
                }
            }

            private bool IsSafePlayer(BasePlayer player)
                => player.IsAdmin || HasPlayer(player);

            public int GetRemainingPlayersAmount()
                => players.Count();

            public Vector3 GetRandomSpawnPoint()
            {
                var foundation = foundations
                    .Where((x) => !x.IsDestroyed)
                    .ToList()
                    .GetRandom();

                return foundation.transform.position;
            }

            private int[] GenerateBrokenGlassesNumbers(int columns, int amount, int[] lastSafeGlasses)
            {
                var result = plugin.GetRandomNumbersBetweenRange(0, columns, amount);

                if (lastSafeGlasses != null)
                {
                    var safeGlasses = Enumerable
                        .Range(1, columns)
                        .Where((x) => !result.Contains(x))
                        .ToArray();

                    bool itsClose = false;
                    foreach (var num in lastSafeGlasses)
                    {
                        if (safeGlasses.Any((x) => Math.Abs(x - num) <= 1))
                        {
                            itsClose = true;
                            break;
                        }
                    }

                    if (!itsClose)
                    {
                        return GenerateBrokenGlassesNumbers(columns, amount, lastSafeGlasses);
                    }
                }

                return result;
            }
        }

        private class PlayerBackup
        {
            public BasePlayer player;
            public MetabolismBackup metabolism;
            public Vector3 position;

            public class MetabolismBackup
            {
                public float health;
                public float bleeding;
                public float calories;
                public float hydration;
                public float oxygen;
                public float poison;
                public float radiation_level;
                public float radiation_poison;
                public float wetness;
                public float temperature;
            }

            public ItemData[] containerMain;
            public ItemData[] containerWear;
            public ItemData[] containerBelt;

            public PlayerBackup(BasePlayer player)
            {
                this.player = player;
                position = player.transform.position;

                containerBelt = GetItems(player.inventory.containerBelt);
                containerMain = GetItems(player.inventory.containerMain);
                containerWear = GetItems(player.inventory.containerWear);

                metabolism = new PlayerBackup.MetabolismBackup
                {
                    health = player.health,
                    bleeding = player.metabolism.bleeding.value,
                    calories = player.metabolism.calories.value,
                    hydration = player.metabolism.hydration.value,
                    oxygen = player.metabolism.oxygen.value,
                    poison = player.metabolism.poison.value,
                    radiation_level = player.metabolism.radiation_level.value,
                    radiation_poison = player.metabolism.radiation_poison.value,
                    temperature = player.metabolism.temperature.value,
                    wetness = player.metabolism.wetness.value
                };
            }

            public void RestorePlayer()
            {
                if (!player.IsConnected)
                    return;

                RestoreItems(player, containerBelt, "belt");
                RestoreItems(player, containerWear, "wear");
                RestoreItems(player, containerMain, "main");
            }

            private bool RestoreItems(BasePlayer player, ItemData[] itemData, string containerType)
            {
                ItemContainer container = containerType == "belt"
                    ? player.inventory.containerBelt
                    : containerType == "wear"
                        ? player.inventory.containerWear
                        : player.inventory.containerMain;

                for (int i = 0; i < itemData.Length; i++)
                {
                    Item item = CreateItem(itemData[i], player);
                    if (item == null)
                        continue;

                    if (!item.MoveToContainer(container, itemData[i].position)
                        && !item.MoveToContainer(container)
                        && !player.inventory.GiveItem(item))
                    {
                        item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
                    }
                }
                return true;
            }

            private Item CreateItem(ItemData itemData, BasePlayer player)
            {
                Item item = ItemManager.CreateByItemID(itemData.itemid, Mathf.Max(1, itemData.amount), itemData.skin);
                if (item == null)
                    return null;

                item.condition = itemData.condition;
                item.maxCondition = itemData.maxCondition;

                if (itemData.displayName != null)
                {
                    item.name = itemData.displayName;
                }

                if (itemData.text != null)
                {
                    item.text = itemData.text;
                }

                item.flags |= itemData.flags;

                if (itemData.frequency > 0)
                {
                    ItemModRFListener rfListener = item.info.GetComponentInChildren<ItemModRFListener>();
                    if (rfListener != null)
                    {
                        PagerEntity pagerEntity = BaseNetworkable.serverEntities.Find(item.instanceData.subEntity) as PagerEntity;
                        if (pagerEntity != null)
                        {
                            pagerEntity.ChangeFrequency(itemData.frequency);
                            item.MarkDirty();
                        }
                    }
                }

                if (itemData.instanceData?.IsValid() ?? false)
                    itemData.instanceData.Restore(item);

                FlameThrower flameThrower = item.GetHeldEntity() as FlameThrower;
                if (flameThrower != null)
                    flameThrower.ammo = itemData.ammo;

                if (itemData.contents != null && item.contents != null)
                {
                    foreach (ItemData contentData in itemData.contents)
                    {
                        Item childItem = CreateItem(contentData, player);
                        if (childItem == null)
                            continue;

                        if (!childItem.MoveToContainer(item.contents, contentData.position)
                            && !childItem.MoveToContainer(item.contents)
                            && !player.inventory.GiveItem(item))
                        {
                            item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
                        }
                    }
                }

                // Process weapon attachments/capacity after child items have been added.
                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    weapon.DelayedModsChanged();

                    if (!string.IsNullOrEmpty(itemData.ammotype))
                        weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(itemData.ammotype);
                    weapon.primaryMagazine.contents = itemData.ammo;
                }

                return item;
            }

            private ItemData GetItem(Item item)
            {
                return new ItemData
                {
                    itemid = item.info.itemid,
                    amount = item.amount,
                    displayName = item.name,
                    ammo = item.GetHeldEntity() is BaseProjectile
                        ? (item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents
                        : item.GetHeldEntity() is FlameThrower
                            ? (item.GetHeldEntity() as FlameThrower).ammo
                            : 0,
                    ammotype = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname ?? null,
                    position = item.position,
                    skin = item.skin,
                    condition = item.condition,
                    maxCondition = item.maxCondition,
                    frequency = ItemModAssociatedEntity<PagerEntity>.GetAssociatedEntity(item)?.GetFrequency() ?? -1,
                    instanceData = new ItemData.InstanceData(item),
                    text = item.text,
                    flags = item.flags,
                    contents = item.contents?.itemList.Select(childItem => GetItem(childItem)).ToArray(),
                };
            }

            private ItemData[] GetItems(ItemContainer container)
            {
                if (container.itemList.Count == 0)
                    return new ItemData[0];

                ItemData[] itemData = new ItemData[container.itemList.Count];
                for (int i = 0; i < container.itemList.Count; i++)
                {
                    Item item = container.itemList[i];
                    itemData[i] = GetItem(item);
                }
                return itemData;
            }
        }

        public class ItemData
        {
            public int itemid;
            public ulong skin;
            public string displayName;
            public int amount;
            public float condition;
            public float maxCondition;
            public int ammo;
            public string ammotype;
            public int position;
            public int frequency;
            public InstanceData instanceData;
            public string text;
            public Item.Flag flags;
            public ItemData[] contents;

            public class InstanceData
            {
                public int dataInt;
                public int blueprintTarget;
                public int blueprintAmount;

                public InstanceData(Item item)
                {
                    if (item.instanceData == null)
                        return;

                    dataInt = item.instanceData.dataInt;
                    blueprintAmount = item.instanceData.blueprintAmount;
                    blueprintTarget = item.instanceData.blueprintTarget;
                }

                public void Restore(Item item)
                {
                    if (item.instanceData == null)
                        item.instanceData = new ProtoBuf.Item.InstanceData();

                    item.instanceData.ShouldPool = false;

                    item.instanceData.blueprintAmount = blueprintAmount;
                    item.instanceData.blueprintTarget = blueprintTarget;
                    item.instanceData.dataInt = dataInt;

                    item.MarkDirty();
                }

                public bool IsValid()
                {
                    return dataInt != 0 || blueprintAmount != 0 || blueprintTarget != 0;
                }
            }
        }

        #endregion Structures

        #region Lang

        private string Lang(string key, string playerId)
            => lang.GetMessage(key, this, playerId);

        private string HumanizedLang(string key, BasePlayer player, params object[] args)
        {
            string msg = string.Format(Lang(key, player?.UserIDString), args);
            msg = msg.Replace("<b>", "")
                .Replace("</b>", "")
                .Replace("</size>", "")
                .Replace("</color>", "")
                .Replace("<i>", "")
                .Replace("</i>", "");

            msg = Regex.Replace(msg, @"<color=.*?>", "");
            msg = Regex.Replace(msg, @"<size=\d+>", "");
            return msg;
        }

        protected override void LoadDefaultMessages()
        {
            var keys = config.eventSettings.giveTimes
                .Where((x) => x.showWarn && !string.IsNullOrEmpty(x.langKey))
                .Select((x) => x.langKey)
                .Distinct();

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ChatName"] = "<color=orange>GlassBridge:</color>",
                ["SyntaxError"] = "Available commands:\n/{0} join - To join the event\n/{0} leave - To leave the event\n/{0} start - To start a new event\n/{0} stop - To stop the current event",
                ["ConsoleSyntaxError"] = "Available commands:\n{0} start - To start a new event\n{0} stop - To stop the current event",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["AlreadyActive"] = "An event is already active",
                ["YouStartedAnEvent"] = "You have started an event, an announcement will be given on when it is ready for players to join",
                ["EventStarted"] = "An event has started! Join using /{0} join",
                ["NoActiveEvent"] = "There are no active events",
                ["AlreadyBeingFinalized"] = "The event is already being finalized",
                ["YouStoppedTheEvent"] = "You stopped the event!",
                ["AlreadyStarted"] = "The event already is started",
                ["IsAlreadyAtTheEvent"] = "You are already at the event",
                ["NotParticipating"] = "You are not participating in the event",
                ["DeadOrWounded"] = "You cannot participate in the event while you are dead or wounded",
                ["NotOnGround"] = "You cannot participate in the event while flying",
                ["Joined"] = "You joined the event!",
                ["Leave"] = "You left the event!",
                ["WaitForTeleport"] = "You will be teleported back in 5 seconds...",
                ["WinnerBroadcast"] = "The event has ended. {0} was the winner of the event!",
                ["Stopped"] = "The event was stopped by an administrator",
                ["AutoStopped"] = "The event ended because the time limit expired and no player was the winner :worried:",
                ["NoPlayers"] = "The event has ended because it has no players",
                ["MinPlayers"] = "The event was ended because it did not contain the minimum number of players ({0})",
                ["CommandBlocked"] = "This command is blocked while you are participating in the event.",
                ["LeftFromEventArea"] = "You were removed from the event because you left the event area.",
                ["GlassBridgeTitle"] = "<b><color=orange><size=12>★<size=14>★</size>★\nGLASS BRIDGE</size></color></b>",
                ["JoinButton"] = "<b><color=#6fff52><size=16>JOIN</size></color></b>",
                ["LeaveButton"] = "<b><color=#ee8181><size=16>LEAVE</size></color></b>",
                ["EventStatusRemainingPlayersText"] = "<size=14><b>{0}</b></size>",
                ["EventStatusRemainingTimeText"] = "<size=24><b>{0}:{1}</b></size>",
                ["StartCountdown_3"] = "<size=128><b>3</b></size>",
                ["StartCountdown_2"] = "<size=128><b>2</b></size>",
                ["StartCountdown_1"] = "<size=128><b>1</b></size>",
                ["StartCountdown_0"] = "<size=128><b>GO!</b></size>"
            }
                .Concat(keys.ToDictionary((x) => x, (x) => "You received <color=orange><b>{0}x {1}</b></color>!"))
                .ToDictionary((x) => x.Key, (x) => x.Value),
            this);
        }

        #endregion Lang

        #region Configuration

        private Configuration config;

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();

            if (config.eventSettings.waitDuration < 1)
            {
                Interface.Oxide.LogError("Failed to load the configuration. TThe duration of the wait cooldown time cannot be less than 1 second.");
                Interface.Oxide.UnloadPlugin(nameof(plugin));
                return;
            }

            // Update config values
            if (config.Version < new VersionNumber(1, 5, 0))
            {
                Interface.Oxide.LogWarning("Configuration appears to be outdated; updating and saving");
                for (int i = 0; i < config.eventSettings.giveTimes.Count; i++)
                {
                    var giveTime = config.eventSettings.giveTimes[i];
                    if (giveTime.showWarn) config.eventSettings.giveTimes[i].langKey = "ReceivedNewItem";
                }
            }

            if (config.Version < new VersionNumber(1, 6, 0))
            {
                Interface.Oxide.LogWarning("Configuration appears to be outdated; updating and saving");
                config.UI.joinLeave.offset = "-146 -12 -12 138";
            }

            config.Version = Version;
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration
            {
                commands = new List<string> {
                    "glassbridge",
                    "gb"
                },
                eventSettings = {
                    giveTimes = new List<Configuration.EventSettings.GiveTime> {
                        new Configuration.EventSettings.GiveTime {
                            time = 30f,
                            shortname = "arrow.wooden",
                            amount = 3,
                            container = "main",
                            showWarn = false,
                        },
                        new Configuration.EventSettings.GiveTime {
                            time = 30f,
                            shortname = "bow.hunting",
                            container = "belt",
                            loadedAmmo = 1,
                            warnBgColor = "0.12 0.46 0.21 1",
                            langKey = "ReceivedNewItem",
                        },
                        new Configuration.EventSettings.GiveTime {
                            time = 120f,
                            shortname = "pistol.revolver",
                            container = "belt",
                            loadedAmmo = 4,
                            langKey = "ReceivedNewItem"
                        },
                        new Configuration.EventSettings.GiveTime {
                            time = 180f,
                            shortname = "rifle.ak",
                            container = "belt",
                            loadedAmmo = 12,
                            warnBgColor = "0.58 0.25 0.25 1",
                            langKey = "ReceivedNewItem"
                        }
                    }
                },
                prizeSettings = {
                    commands = new List<string> {
                        "oxide.usergroup add {winner_id} glassbridge_event_winner"
                    },
                    items = new List<Configuration.PrizeSettings.GiveItem> {
                        new Configuration.PrizeSettings.GiveItem {
                            shortname = "rifle.ak",
                            skinId = 1826520371UL,
                            customName = "Apocalyptic Knight AK"
                        },
                        new Configuration.PrizeSettings.GiveItem {
                            shortname = "ammo.rifle",
                            amount = 128,
                            customName = "Ammo for your AK"
                        }
                    }
                }
            };
        }

        private class Configuration
        {
            [JsonProperty(PropertyName = "Commands")]
            public List<string> commands;

            [JsonProperty(PropertyName = "Permission name")]
            public string permissionName = "glassbridge.admin";

            [JsonProperty(PropertyName = "Allowed console commands ('*' to allow all commands)")]
            public List<string> allowedConsoleCommands = new List<string>();

            [JsonProperty(PropertyName = "Blocked chat commands ('*' to block all commands)")]
            public List<string> blockedChatCommands = new List<string>();

            [JsonProperty(PropertyName = "Event settings")]
            public EventSettings eventSettings = new EventSettings();

            [JsonProperty(PropertyName = "Glasses settings")]
            public GlassesSettings glassesSettings = new GlassesSettings();

            [JsonProperty(PropertyName = "Prize settings")]
            public PrizeSettings prizeSettings = new PrizeSettings();

            [JsonProperty(PropertyName = "UI settings")]
            public UISettings UI = new UISettings();

            public class EventSettings
            {
                [JsonProperty(PropertyName = "Auto start cooldown in seconds (0 = disabled)")]
                public int autoStartCooldown = 3600;

                [JsonProperty(PropertyName = "Waiting duration (seconds)")]
                public int waitDuration = 60 * 2;

                [JsonProperty(PropertyName = "Auto stop after seconds (0 = disabled)")]
                public int autoStopAfter = 60 * 5;

                [JsonProperty(PropertyName = "Minimum players")]
                public int minPlayers = 1;

                [JsonProperty(PropertyName = "Lose when fall")]
                public bool loseWhenFall = false;

                [JsonProperty(PropertyName = "PVP Enabled")]
                public bool pvpEnabled = true;

                [JsonProperty(PropertyName = "Rollback players to start point when die")]
                public bool rollbackWhenDie = true;

                [JsonProperty(PropertyName = "Spawn position")]
                public Vector3 position = new Vector3(300f, 800f, 300f);

                [JsonProperty(PropertyName = "Give times")]
                public List<GiveTime> giveTimes;

                public class GiveTime
                {
                    [JsonProperty(PropertyName = "Tiem after the event started")]
                    public float time;

                    [JsonProperty(PropertyName = "Item shortname")]
                    public string shortname;

                    [JsonProperty(PropertyName = "Item amount")]
                    public int amount = 1;

                    [JsonProperty(PropertyName = "Item skin ID")]
                    public ulong skinId = 0L;

                    [JsonProperty(PropertyName = "Loaded ammo (0 = disabled)")]
                    public int loadedAmmo = 0;

                    [JsonProperty(PropertyName = "Container to give (wear/belt/main)")]
                    public string container = "main";

                    [JsonProperty(PropertyName = "Show warn for this item?")]
                    public bool showWarn = true;

                    [JsonProperty(PropertyName = "Warn background color")]
                    public string warnBgColor = "0 0.52 1 1";

                    [JsonProperty(PropertyName = "Lang message key")]
                    public string langKey = "";
                }
            }

            public class GlassesSettings
            {
                public int columns = 18;
                public int rows = 24;

                [JsonProperty(PropertyName = "Broken glasses by row (need to be less than columns)")]
                public int brokenByRow = 9;
            }

            public class PrizeSettings
            {
                [JsonProperty(PropertyName = "Commands to execute on server")]
                public List<string> commands;

                [JsonProperty(PropertyName = "Items to give")]
                public List<GiveItem> items;

                public class GiveItem
                {
                    [JsonProperty(PropertyName = "Item shortname")]
                    public string shortname;

                    [JsonProperty(PropertyName = "Item amount")]
                    public int amount = 1;

                    [JsonProperty(PropertyName = "Item skin ID")]
                    public ulong skinId = 0L;

                    [JsonProperty(PropertyName = "Custom name (empty = disabled)")]
                    public string customName = "";
                }
            }

            public class UISettings
            {
                [JsonProperty(PropertyName = "Join Leave UI")]
                public JoinLeaveUISettings joinLeave = new JoinLeaveUISettings();

                [JsonProperty(PropertyName = "Event status UI")]
                public EventStatusUISettings eventStatus = new EventStatusUISettings();

                public class JoinLeaveUISettings
                {
                    [JsonProperty(PropertyName = "Anchor")]
                    public string anchor = "1 0.5 1 0.5";

                    [JsonProperty(PropertyName = "Offset")]
                    public string offset = "-146 -12 -12 138";

                    [JsonProperty(PropertyName = "Background color")]
                    public string color = "0.18 0.17 0.18 1";

                    [JsonProperty(PropertyName = "Join leave button")]
                    public JoinLeaveUIButton button = new JoinLeaveUIButton();

                    public class JoinLeaveUIButton
                    {
                        [JsonProperty(PropertyName = "Join background color")]
                        public string joinColor = "0.18 0.41 0.12 1";

                        [JsonProperty(PropertyName = "Leave background color")]
                        public string leaveColor = "0.41 0.12 0.12 1";
                    }
                }

                public class EventStatusUISettings
                {
                    [JsonProperty(PropertyName = "Anchor")]
                    public string anchor = "0.5 1 0.5 1";

                    [JsonProperty(PropertyName = "Offset")]
                    public string offset = "-48 -100 48 -36";

                    [JsonProperty(PropertyName = "Remaining players")]
                    public EventStatusRemainingPlayersUISettings remainingPlayers = new EventStatusRemainingPlayersUISettings();

                    [JsonProperty(PropertyName = "Remaining time")]
                    public EventStatusRemainingTimeUISettings remainingTime = new EventStatusRemainingTimeUISettings();

                    public class EventStatusRemainingPlayersUISettings
                    {
                        [JsonProperty(PropertyName = "Background color")]
                        public string color = "1 1 1 0.5";

                        [JsonProperty(PropertyName = "Anchor")]
                        public string anchor = "0 0 1 0";

                        [JsonProperty(PropertyName = "Offset")]
                        public string offset = "0 0 0 24";

                        [JsonProperty(PropertyName = "Icon URL")]
                        public string iconUrl = "https://i.postimg.cc/HsfZffQb/HY9DNLr.png";

                        [JsonProperty(PropertyName = "Icon anchor")]
                        public string iconAnchor = "0 0 0 1";

                        [JsonProperty(PropertyName = "Icon offset")]
                        public string iconOffset = "4 4 20 -4";
                    }

                    public class EventStatusRemainingTimeUISettings
                    {
                        [JsonProperty(PropertyName = "Background color")]
                        public string color = "1 1 1 0.5";

                        [JsonProperty(PropertyName = "Anchor")]
                        public string anchor = "0 0 1 1";

                        [JsonProperty(PropertyName = "Offset")]
                        public string offset = "0 28 0 0";
                    }
                }
            }

            public VersionNumber Version = new VersionNumber(1, 0, 0);
        }

        #endregion Configuration

    }
}