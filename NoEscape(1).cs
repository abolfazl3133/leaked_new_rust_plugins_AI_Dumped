// Reference: 0Harmony
using HarmonyLib;
using Network;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Rust;
using Layer = Rust.Layer;
using Time = UnityEngine.Time;
using Oxide.Plugins.NoEscapeEx;
using Oxide.Core.Plugins;
using Priority = Network.Priority;
// written by Khan#8615
/*
 * Update 1.0.1 Fixed Custom Building Damage Toggle not toggling off if set to false.
 * update 1.0.2 Fixed NRE when setting newcolor 0 0
 * ( Resolves the bug where the UI glitches )
 * Performance Improvement.
 *
 * update 1.0.3
 * Added remaining time for cmd block msg's
 * Added ability to over-ride default vanillia 30sec repair block from blocks being damaged.
 * Added option to not use distance check & instead remain raid-blocked until death. ( for attackers only! )
 * Fixed player.transform NRE's when players left server. ( forgot to use ServerPosition instead )
 * Another Performance Improvement.
 * Fixed UI not clearing due to NRE from above ^
 * Added Right side hud Top / Bottom preset options
 *
 * update 1.0.4
 * Fixed player.Connection being null ( always use player.net.connection ) thus UI not showing.
 *
 * update 1.0.5
 * Now formats time for the repair blocked msg like the rest of the msg responses.
 * Updated Until Death feature to include an expiration option ( uses current time to check if they are blocked instead of a bloat ware timer ).
 * Added Item ID edit support for UI Icons
 * Added sprite support option instead of game items
 * Added F1 Grenade support
 * MLRS do work just fine for the vanilla map ones, 3rd party plugins are not supported. ( too many variants )
 *
 * update 1.0.6
 * Fixed UI Cache not clearing properly on reload sometimes to re-apply the new settings.
 * Fixed UI failing to show during reload if triggering a raid / combat instantly during a reload process.
 * ( was due to subscribing to the oxide hook onserverinit prior to caching the UI thus no ui would show )
 * Added random color option so the raids can auto select random colors for you instead of being stuck with 1 color..
 *
 * update 1.0.7
 * Fixed new until death timer counting the wrong way + updated msg response.
 * Performance tweak + tiny amount of memory reduced if certain features are not used.
 *
 * update 1.0.8
 * fixed Duplicate Key error.
 * fix for players moving between 3+ over-lapping zones not being handled properly
 * performance improvement no longer use BasePlayer now only use link connections msg / effects / UI display etc..
 * performance improvement each zone is now tracked by a random assigned digit which is pooled.( to allow for UI updates better on the fly with multi-overlapping zones )
 * built custom int number pooling to identify zones since for some reason it cannot identify this zone between this zone when 2 zones over-lap the collider becomes confused and thinks it belongs to the other..
 *
 * update 1.0.9
 * Forgot exit code in previous update, all issues should be resolved now.
 *
 * Update 1.0.10
 * Combat-Block UI now re-appears when leaving a raid zone after 1-2 sec. ( if still combat blocked )
 * Fixed Raid-Zones Ending not removing block status from update changes in 1.0.9
 *
 * beta update 1.0.12
 * Changed Building Damage Features, now each building grade has dedicated options to allow more precise control between the twig/wood/metal/top-tier, grades.
 * Fixed Text Hex config option not working ( only the RGB option applied ) + Added config toggle to specify to use either hex code or rgb.
 * Added dedicated building & upgrade config options
 * Added entity damaged percentage trigger so it will ignore the damage until x-percentage then trigger a raid-zone (0 = disabled)
 *
 * Update 1.0.13
 * Updated harmony to v2
 * Carbon support at least compiles now.. ( requires more testing )
 *
 * Update 1.0.14
 * Fixed a big when trying to use custom ui setting 4.
 * Added combat blocking to players leaving raid zones + config toggle
 * Added new lerpspeed config setter to set how fast the bubble grows.
 * Added ability to disable repairs while raid-block by setting the repair wait time to zero 0.
 * Added ulong variants for API Hooks
 * bool IsRaidBlocked(ulong player)
 * bool IsCombatBlocked(ulong player)
 * bool IsEscapeBlocked(ulong player)
 *
 * Update 1.1.0
 * Fixed time msgs not supporting hours.
 * Fixed a few instances where the player.userID wasn't casted due to the facepunch changes.
 * Added HV Rocket support.
 * Added umod UI look-alike option.
 * Fixed version numbers.
 * Seperated configuration file options on the UI system for better customization support.
 * Added auto config updater
 *
 * Update 1.1.1
 * Swapped out PhoneController.PositionToGridCoord ( Deprecated ) to using MapHelper.PositionToString
 * Adjusted percentage trigger code to fix an issue with it not starting a raid properly
 * 
 * TODO:
 * Add new options for until death raid block mode -
 * Block TC members
 * Block TC owner
 * Block Attacker team members
 * Maybe add a new mono class for UI to work with until death mode enabled/timer.
 * RFTimedExplosive TimedExplosive
 * Possibly merge or update bubble logic / zones for blocks halfway-overlapping when 3+ layers are made.
 * Change Until Death feature again to just set as Until Death or Just use Timer instead.. ( possibly use the disabled raid timer & then use the newly added one if > 0 then time instead of death.. )
 * bug where when in a monument area you could become raid blocked if a raid happens and the bubble overlaps into the monument zone.
 * Change how bubbles / raid zones are made, they now auto gen based off the base size.
 *
 * Add russian lang support & oxides lang file system.
 */
namespace Oxide.Plugins
{
    [Info("No Escape", "Khan", "1.1.2")]
    [Description("Control players actions")]
    internal class NoEscape : RustPlugin
    {
        #region License Agreement (EULA) of No Escape

        /*
        End-User License Agreement (EULA) of No Escape

        This End-User License Agreement ("EULA") is a legal agreement between you and Kyle. This EULA agreement
        governs your acquisition and use of our No Escape plugin ("Software") directly from Kyle.

        Please read this EULA agreement carefully before downloading and using the No Escape plugin.
        It provides a license to use the No Escape and contains warranty information and liability disclaimers.

        If you are entering into this EULA agreement on behalf of a company or other legal entity, you represent that you have the authority
        to bind such entity and its affiliates to these terms and conditions. If you do not have such authority or if you do not agree with the
        terms and conditions of this EULA agreement, DO NOT purchase or download the Software.

        This EULA agreement shall apply only to the Software supplied by Kyle Farris regardless of whether other software is referred
        to or described herein. The terms also apply to any Kyle updates, supplements, Internet-based services, and support services for the Software,
        unless other terms accompany those items on delivery. If so, those terms apply.

        License Grant

        Kyle hereby grants you a personal, non-transferable, non-exclusive license to use the No Escape software on your devices in
        accordance with the terms of this EULA agreement. You are permitted to load the No Escape on your personal server owned by you.

        You are not permitted to:

        Edit, alter, modify, adapt, translate or otherwise change the whole or any part of the Software nor permit the whole or any part
        of the Software to be combined with or become incorporated in any other software, nor decompile, disassemble or reverse
        engineer the Software or attempt to do any such things.
        Reproduce, copy, distribute, resell or otherwise use the Software for any commercial purpose
        Allow any third party to use the Software on behalf of or for the benefit of any third party
        Use the Software in any way which breaches any applicable local, national or international law
        use the Software for any purpose that Kyle considers is a breach of this EULA agreement

        Intellectual Property and Ownership

        Kyle shall at all times retain ownership of the Software as originally downloaded by you and all subsequent downloads of the Software by you. 
        The Software (and the copyright, and other intellectual property rights of whatever nature in the Software, including any modifications made thereto) are and shall remain the property of Kyle.

        Termination

        This EULA agreement is effective from the date you first use the Software and shall continue until terminated. 
        You may terminate it at any time upon written notice to Kyle.
        It will also terminate immediately if you fail to comply with any term of this EULA agreement. 
        Upon such termination, the licenses granted by this EULA agreement will immediately terminate and you agree to stop all access and use of the Software. 
        The provisions that by their nature continue and survive will survive any termination of this EULA agreement.
        */

        #endregion

        #region Fields

        [PluginReference] Plugin RaidProtection;
        private static NoEscape _instance;
        private static Configuration _config;
        private Harmony _harmony;

        private const string Admin = "noescape.admin";

        private Zone _zone;
        private HashSet<Zone> _raidZones = new HashSet<Zone>();
        private Hash<int, List<Connection>> _display = new Hash<int, List<Connection>>();
        private Hash<ulong, Combat> _combat = new Hash<ulong, Combat>();
        private static int _combatTime;

        [Flags]
        private enum Type
        {
            All = Raid | Combat,
            Raid = 1,
            Combat = 2,
        }

        private List<ulong> _ignore = new List<ulong>();
        private Dictionary<string, Type> _commands = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        private const string Visualization = "assets/prefabs/visualization/sphere.prefab";
        private const string Path = "assets/bundled/prefabs/modding/events/twitch/br_sphere";

        private enum SphereColor
        {
            None,
            Blue,
            Cyan,
            Green,
            Pink,
            Purple,
            Red,
            White,
            Yellow,
            Turquoise,
            Brown,
        }

        private static Dictionary<SphereColor, List<string>> _sphereColors = new Dictionary<SphereColor, List<string>>
        {
            { SphereColor.None, new List<string>() },
            { SphereColor.Blue, new List<string> { Path+".prefab" }},
            { SphereColor.Cyan, new List<string> { Path+".prefab", Path+"_green.prefab" }},
            { SphereColor.Green, new List<string> { Path+"_green.prefab" }},
            { SphereColor.Pink, new List<string> { Path+"_purple.prefab", Path+"_red.prefab" }},
            { SphereColor.Purple, new List<string> { Path+"_purple.prefab" }},
            { SphereColor.Red, new List<string> { Path+"_red.prefab" }},
            { SphereColor.White, new List<string> { Path+"_red.prefab", Path+"_green.prefab", Path+".prefab" }},
            { SphereColor.Yellow, new List<string> { Path+"_red.prefab", Path+"_green.prefab" }},
            { SphereColor.Turquoise, new List<string> { Path+"_green.prefab", Path+"_purple.prefab" }},
            { SphereColor.Brown, new List<string> { Path+"_red.prefab", Path+"_green.prefab", Path+"_purple.prefab" }},
        };

        // LT | LB | RT | RB 
        private List<GuiPos> _presets = new List<GuiPos>
        {
            new GuiPos
            {
                HMinMax = new KeyValuePair<string, string>("0.345 0.11", "0.465 0.14"),
                IMinMax = new KeyValuePair<string, string>("0 0", "0.13 1"),
                TMinMax = new KeyValuePair<string, string>("0.15 0", "1 1")
            },
            new GuiPos
            {
                HMinMax = new KeyValuePair<string, string>("0.345 0", "0.465 0.03"),
                IMinMax = new KeyValuePair<string, string>("0 0", "0.13 0.8"),
                TMinMax = new KeyValuePair<string, string>("0.15 0", "1 0.8")
            },
            new GuiPos // RT
            {
                HMinMax = new KeyValuePair<string, string>("0.55 0.11", "0.67 0.14"),
                IMinMax = new KeyValuePair<string, string>("0 0", "0.13 1"),
                TMinMax = new KeyValuePair<string, string>("0.15 0", "1 1")
            },
            new GuiPos // RB
            {
                HMinMax = new KeyValuePair<string, string>("0.55 0", "0.67 0.03"),
                IMinMax = new KeyValuePair<string, string>("0 0", "0.13 0.8"),
                TMinMax = new KeyValuePair<string, string>("0.15 0", "1 0.8")
            },
        };

        private static bool _death = false;

        // Add F1 Grenades & MLRS
        //private static List<int> _check = null;
        private static GUI _gui;

        #endregion

        #region Number Pooling

        private static NumberPool _pool;
        public class NumberPool
        {
            private HashSet<int> _pooled;
            private int _min;
            private int _max;

            public NumberPool(int min = 0, int max = 300)
            {
                if (min >= max)
                {
                    throw new ArgumentException("Min value must be less than max value.");
                }

                this._min = min;
                this._max = max;
                _pooled = new HashSet<int>();

                FillPool();
            }

            private void FillPool()
            {
                for (int i = _min; i <= _max; i++)
                {
                    _pooled.Add(i);
                }
            }

            public int Min
            {
                get
                {
                    return _pooled.First();
                }
            }

            public int Max
            {
                get
                {
                    return _pooled.Count();
                }
            }

            public int GetNumber()
            {
                if (_pooled.Count == 0)
                    throw new InvalidOperationException("Number pool is empty.");
  
                int number = Min;
                _pooled.Remove(number);
                return number;
            }

            public void ReturnNumber(int number) => _pooled.Add(number);

            public void Clear() => _pooled.Clear();
        }


        #endregion

        #region MonoBehaviours

        private Hash<ulong, float> _raiders = new Hash<ulong, float>();

        private class Zone : MonoBehaviour
        {
            public int id;
            private int _init;
            public List<SphereEntity> spheres;
            public List<string> colors;
            public float timeLeft = 0;
            public int sphereCount;
            public string zoneCoord;
            public float zoneRadius;
            public bool gui;
            private float _lerpSpeed;

            private void Awake()
            {
                _lerpSpeed = _config.RaidBlock.LerpSpeed;
                _init = 0;
                id = _pool.GetNumber();
                _instance._display[id] = new List<Connection>();
                spheres = new List<SphereEntity>();
                gui = _gui.RaidGUI.Raid;
                zoneRadius = _config.RaidBlock.Radius;
                sphereCount = _config.RaidBlock.Spheres;
                colors = _sphereColors[_config.RaidBlock.Color];
                if (_config.RaidBlock.RandomColor)
                {
                    int roll = UnityEngine.Random.Range(1, _sphereColors.Count);
                    colors = _sphereColors[(SphereColor)roll];
                }

                zoneCoord = MapHelper.PositionToString(transform.position);
                Rigidbody rb = gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.detectCollisions = true;

                if (colors.Count > 0)
                    foreach (string c in colors)
                        CreateSphere(c);

                if (sphereCount > 0)
                    CreateSpheres(sphereCount, Visualization);

                if (gui)
                    InvokeRepeating(nameof(TickTimer), 2f, 2f);
            }

            private void Update()
            {
                timeLeft -= Time.deltaTime;
                if (timeLeft <= 2)
                    CancelInvoke(nameof(TickTimer));

                if (timeLeft <= 0)
                    DeleteZone();
            }

            private void TickTimer()
            {
                string payload = _instance._jsonPayloadRaid.Replace("_Count", FormatTime((long)timeLeft));
                var update = _instance._display[id];
                Cui.UpdateGUI(update, payload);
            }

            private void OnTriggerEnter(Collider other)
            {
                BasePlayer player = other?.ToBaseEntity() as BasePlayer;
                Connection user = player?.net.connection;
                if (player == null || user == null || _instance._display[id].Contains(user))
                    return;

                //if (gui)
                    _instance._display[id].Add(user);

                ulong userID = (ulong)player.userID;
                if (_instance._raiders.ContainsKey(userID))
                    return;

                _instance._raiders.Add(userID, 0);
                _instance.Response(player, _config.Msg.RaidBlocked, FormatTime((long)timeLeft));
                _instance.FindCombatBlock(userID)?.UpdateGUI();
                user.RunEffect(_config.Effect.RaidStart);
                Interface.CallHook("OnRaidBlock", player);
            }

            private void OnTriggerExit(Collider other)
            {
                BasePlayer player = other?.ToBaseEntity() as BasePlayer;
                Connection user = player?.net.connection;
                if (player == null || user == null)
                    return;

                //if (gui)
                    _instance._display[id].Remove(user);

                bool keep = false;
                foreach (var zone in _instance._raidZones)
                {
                    if (this != zone && _instance._display[zone.id].Contains(user))
                        keep = true;
                }

                if (keep) return;
                if (gui)
                    Cui.UpdateGUI(user, null, false);

                ulong userID = (ulong)player.userID;
                _instance._raiders.Remove(userID);
                _instance.Response(player, _config.Msg.UnRaidBlocked);
                user.RunEffect(_config.Effect.RaidEnd);
                _instance.FindCombatBlock(userID)?.UpdateGUI();
                if (_config.RaidBlock.Combat && !_instance._ignore.Contains(userID))
                    _instance.HandleCombatBlock(player);
            }

            private void OnDestroy()
            {
                if (Rust.Application.isQuitting)
                    return;

                if (_instance._raidZones.Contains(this))
                    _instance._raidZones.Remove(this);
            }

            public void DeleteZone()
            {
                var update = _instance._display[id];
                NetWrite netWrite = Network.Net.sv.StartWrite();
                netWrite.PacketID(Message.Type.ConsoleCommand);
                netWrite.String(ConsoleSystem.BuildCommand("chat.add", 2, _config.Msg.ChatIcon, _config.Msg.UnRaidBlocked));
                netWrite.Send(new SendInfo(update));
                update.RunEffects(_config.Effect.RaidEnd);

                if (gui)
                    Cui.ClearGUI(update);

                if (_gui.CombatGUI.Combat)
                {
                    foreach (var user in update)
                    {
                        ulong p = (ulong)user.userid;
                        _instance._raiders.Remove(p);
                        _instance.FindCombatBlock(p)?.UpdateGUI();
                    }
                }
                else
                {
                    foreach (var user in update)
                        _instance._raiders.Remove((ulong)user.userid);
                }

                _instance._display[id].Clear();
                _instance._display.Remove(id);
                _pool.ReturnNumber(id);

                int c = spheres.Count;
                for (int i = 0; i < c; i++)
                {
                    SphereEntity sphere = spheres[i];
                    if (sphere != null && !sphere.IsDestroyed)
                        sphere.Kill();
                }

                spheres.Clear();

                if (gameObject != null)
                    Destroy(gameObject);
            }

            private void CreateSphere(string prefab)
            {
                SphereEntity sphere = GameManager.server.CreateEntity(prefab, transform.position) as SphereEntity;
                if (sphere == null) return;
                sphere.currentRadius = 1f;
                /*if (_init != 0)
                    sphere.enabled = false;*/
                sphere.Spawn();
                if (_init != 0)
                    sphere.triggers.Clear();
                sphere.LerpRadiusTo(zoneRadius * 2, _lerpSpeed);
                spheres.Add(sphere);
                if (_init == 0)
                    _init++;
            }

            private void CreateSpheres(int loop, string prefab)
            {
                for (int i = 0; i < loop; i++)
                {
                    CreateSphere(prefab);
                }
            }
        }

        private class Regen : MonoBehaviour
        {
            private BaseCombatEntity _entity;
            public bool isDestroying;
            public ulong iD;
            private bool MaxHealth => Math.Abs(_entity.MaxHealth() - _entity.Health()) < 0.1f;
            public float regenRate;
            public float regenAmount;
            public float attackedRegenTime;

            private void Awake()
            {
                _entity = GetComponent<BaseCombatEntity>();
                iD = _entity.net.ID.Value;
            }

            private void OnDestroy()
            {
                if (Rust.Application.isQuitting)
                    return;

                Destroy(this);
            }

            public void Dispose()
            {
                isDestroying = true;
                CancelInvoke(nameof(Heal));
                Destroy(this);
            }

            public void OnModifyState(float health, float rate, float amount, float pause)
            {
                if (isDestroying) return;
                regenRate = rate;
                regenAmount = amount;
                attackedRegenTime = pause;
                _entity.health = health;
                InvokeRepeating(nameof(Heal), regenRate, regenRate);
            }

            public void Restart(float health, float rate, float amount, float pause)
            {
                CancelInvoke(nameof(Heal));
                regenRate = rate;
                regenAmount = amount;
                attackedRegenTime = pause;
                _entity.health = health;
                _entity.SendNetworkUpdate();
                InvokeRepeating(nameof(Heal), regenRate, regenRate);
            }

            private void Heal()
            {
                if (!_entity.IsValid() || MaxHealth)
                {
                    Dispose();
                    return;
                }

                int check = (int)_entity.SecondsSinceAttacked;
                if (check > 0 && check < attackedRegenTime)
                    return;

                _entity.health = _entity._health + regenAmount;
                _entity.SendNetworkUpdate();
            }
        }

        private class Combat : MonoBehaviour, IDisposable
        {
            public bool isDestroying;
            public BasePlayer player;
            public Connection link;
            public ulong userID;
            public float time;
            public bool gui;
            public bool toggle;
            public bool IsBlocked => time > 0 && time - Time.realtimeSinceStartup > 0;
            public long TimeRemaining => (long)(time - Time.realtimeSinceStartup);

            private void Awake()
            {
                gui = _gui.CombatGUI.Combat;
                player = GetComponent<BasePlayer>();
                link = player.net.connection;
                userID = (ulong)player.userID;
                _instance._combat[userID] = this;
                toggle = gui;
                InvokeRepeating(gui ? nameof(TickTimerGUI): nameof(TickTimer), 2f, 2f);
            }

            private void OnDestroy()
            {
                if (Rust.Application.isQuitting)
                    return;

                _instance._combat.Remove(userID);
            }

            public void Dispose()
            {
                isDestroying = true;
                CancelInvoke(toggle ? nameof(TickTimerGUI) : nameof(TickTimer));
                _instance.Response(player, _config.Msg.UnCombatBlocked);
                player.net.connection.RunEffect(_config.Effect.CombatEnd);
                Cui.UpdateGUI(link, null, false);
                Destroy(this);
            }

            public void UpdateGUI()
            {
                if (!gui) return;
                if (toggle)
                {
                    CancelInvoke(nameof(TickTimerGUI));
                    Cui.UpdateGUI(link, null, false);
                    InvokeRepeating(nameof(TickTimer), 2f, 2f);
                    toggle = false;
                }
                else
                {
                    CancelInvoke(nameof(TickTimer));
                    InvokeRepeating(nameof(TickTimerGUI), 2f, 2f);
                    toggle = true;
                }
            }

            private void TickTimer()
            {
                if (IsBlocked || isDestroying) return;
                Dispose();
            }

            private void TickTimerGUI()
            {
                if (isDestroying) return;
                if (IsBlocked)
                {
                    //if (_instance._raiders.ContainsKey(player.userID)) return;
                    string payload = _instance._jsonPayloadCombat.Replace("_Count", FormatTime(TimeRemaining));
                    Cui.UpdateGUI(link, payload);
                }
                else
                    Dispose();
            }

            public void ResetBlock(int seconds) => time = Time.realtimeSinceStartup + seconds;

            public void BeginBlock(int seconds)
            {
                time = Time.realtimeSinceStartup + seconds;
                if (gui)
                {
                    string payload = _instance._jsonPayloadCombat.Replace("_Count", FormatTime(TimeRemaining));
                    Cui.UpdateGUI(link, payload);   
                }
                Interface.Oxide.CallHook("OnCombatBlock", player);
                _instance.Response(player, _config.Msg.CombatBlocked, FormatTime(_combatTime));
                player.net.connection.RunEffect(_config.Effect.CombatSart);
            }
        }

        #endregion

        #region Config

        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("Specify commands to block ( 3 = Block Both | 1 = Block Raid | 2 = Block Combat )")]
            public Hash<string, Type> Commands;

            [JsonProperty("Combat Block")]
            public CombatBlock CombatBlock = new CombatBlock
            {
                Enable = true,
                Time = 1,
                Exclude = new List<ulong>()
            };

            [JsonProperty("Raid Block")]
            public Raid RaidBlock = new Raid
            {
                Enable = true,
                Death = new Death
                {
                    Die = false,
                    Time = 0,
                    /*Owner = true,
                    Team = true,
                    TC = true,*/
                },
                Time = 300,
                Radius = 100,
                TriggerPercentage = 0,
                Spheres = 3,
                Color = SphereColor.Red,
                LerpSpeed = 10f,
                RandomColor = false,
                Upgrade = true,
                Repair = 30,
                Building = true,
                Combat = true,
            };

            [JsonProperty("User Interface")]
            public GUI GUI = new GUI
            {
                Umod = false,
                RaidGUI = new RaidGUI
                {
                    Raid = true,
                    Icon = 1248356124,
                    SkinId = 0,
                    SpriteToggle = false,
                    Sprite = "assets/icons/explosion.png",
                    SpriteColor = "0.95 0 0.02 0.67",
                    Preset = 0,
                    Transparency = new Colors("#", 0.1f),
                    Text = new Colors("#09ff00"),
                    Size = 13,
                    ColorToggle = true,
                    GuiPos = new GuiPos
                    {
                        HMinMax = new KeyValuePair<string, string>("0.345 0.11", "0.465 0.14"),
                        IMinMax = new KeyValuePair<string, string>( "0 0", "0.13 1"),
                        TMinMax = new KeyValuePair<string, string>("0.15 0", "1 1")
                    },
                },
                CombatGUI = new CombatGUI
                {
                    Combat = true,
                    Icon = 1545779598,
                    SkinId = 0,
                    SpriteToggle = false,
                    Sprite = "assets/icons/bullet.png",
                    SpriteColor = "0.95 0 0.02 0.67",
                    Preset = 0,
                    Transparency = new Colors("#", 0.1f),
                    Text = new Colors("#09ff00"),
                    Size = 13,
                    ColorToggle = true,
                    GuiPos = new GuiPos
                    {
                        HMinMax = new KeyValuePair<string, string>("0.345 0.11", "0.465 0.14"),
                        IMinMax = new KeyValuePair<string, string>( "0 0", "0.13 1"),
                        TMinMax = new KeyValuePair<string, string>("0.15 0", "1 1")
                    },
                },
            };

            [JsonProperty("Building (None = Doors, VendingMachine, ShopFront)")]
            public Dictionary<BuildingGrade.Enum, Building> Building = new Dictionary<BuildingGrade.Enum, Building>
            {
                {BuildingGrade.Enum.None, new Building
                {
                    StartHealth = 35,
                    RegenRate = 1,
                    RegenAmount = 20,
                    AttackedRegenTime = 30
                }},
                {BuildingGrade.Enum.Twigs, new Building
                {
                    StartHealth = 10,
                    RegenRate = 1,
                    RegenAmount = 1,
                    AttackedRegenTime = 30
                }},
                {BuildingGrade.Enum.Wood, new Building
                {
                    StartHealth = 20,
                    RegenRate = 1,
                    RegenAmount = 20,
                    AttackedRegenTime = 30
                }},
                {BuildingGrade.Enum.Stone, new Building
                {
                    StartHealth = 30,
                    RegenRate = 1,
                    RegenAmount = 25,
                    AttackedRegenTime = 30
                }},
                {BuildingGrade.Enum.Metal, new Building
                {
                    StartHealth = 40,
                    RegenRate = 1,
                    RegenAmount = 30,
                    AttackedRegenTime = 30
                }},
                {BuildingGrade.Enum.TopTier, new Building
                {
                    StartHealth = 50,
                    RegenRate = 1,
                    RegenAmount = 40,
                    AttackedRegenTime = 30
                }},
            };

            [JsonProperty("Upgrading only works for BuildingBlocks")]
            public Dictionary<BuildingGrade.Enum, Upgrading> Upgrading = new Dictionary<BuildingGrade.Enum, Upgrading>
            {
                {BuildingGrade.Enum.Twigs, new Upgrading
                {
                    StartHealth = 10,
                    RegenRate = 1,
                    RegenAmount = 1,
                    AttackedRegenTime = 30
                }},
                {BuildingGrade.Enum.Wood, new Upgrading
                {
                    StartHealth = 20,
                    RegenRate = 1,
                    RegenAmount = 20,
                    AttackedRegenTime = 30
                }},
                {BuildingGrade.Enum.Stone, new Upgrading
                {
                    StartHealth = 30,
                    RegenRate = 1,
                    RegenAmount = 25,
                    AttackedRegenTime = 30
                }},
                {BuildingGrade.Enum.Metal, new Upgrading
                {
                    StartHealth = 40,
                    RegenRate = 1,
                    RegenAmount = 30,
                    AttackedRegenTime = 30
                }},
                {BuildingGrade.Enum.TopTier, new Upgrading
                {
                    StartHealth = 50,
                    RegenRate = 1,
                    RegenAmount = 40,
                    AttackedRegenTime = 30
                }},
            };

            [JsonProperty("Sound Effects")]
            public Effect Effect = new Effect();

            [JsonProperty("Message Responses")]
            public Lang Msg = new Lang();
        }

        public class GUI
        {
            [JsonProperty("Enable uMods UI: Not Configurable.")]
            public bool Umod;
            public RaidGUI RaidGUI = new RaidGUI();
            public CombatGUI CombatGUI = new CombatGUI();
        }

        public class CombatGUI
        {
            [JsonProperty("Enable Combat UI")]
            public bool Combat;
            [JsonProperty("Item ID Default: 1545779598, 0 = None")]
            public int Icon;
            [JsonProperty("Skin ID: Default 0")]
            public ulong SkinId;
            [JsonProperty("Switch to sprite instead of Icon")]
            public bool SpriteToggle;
            [JsonProperty("Sprite Default: assets/icons/bullet.png")]
            public string Sprite;
            [JsonProperty("Sprite Color Default: 0.95 0 0.02 0.67")]
            public string SpriteColor;
            [JsonProperty("Hud Preset Positions: ( 0 Left Top | 1 Left Bottom | 2 Right Top | 3 Right Bottom | 4 Custom )")]
            public int Preset;
            [JsonProperty("Hud Transparency Default: #, 0.1f")]
            public Colors Transparency;
            [JsonProperty("Text Color Default: #09ff00")]
            public Colors Text;
            [JsonProperty("Text Font Size Default: 13")]
            public int Size;
            [JsonProperty("Hex or RGB toggle: Default is Hex")]
            public bool ColorToggle;
            [JsonProperty("Custom UI POS: Key is anchorMin | Value is anchorMax")]
            public GuiPos GuiPos = new GuiPos();
        }

        public class RaidGUI
        {
            [JsonProperty("Enable Raid UI")]
            public bool Raid;
            [JsonProperty("Item ID Default: 1248356124, 0 = None")]
            public int Icon;
            [JsonProperty("Skin ID: Default 0")]
            public ulong SkinId;
            [JsonProperty("Switch to sprite instead of Icon")]
            public bool SpriteToggle;
            [JsonProperty("Sprite Default: assets/icons/explosion.png")]
            public string Sprite;
            [JsonProperty("Sprite Color Default: 0.95 0 0.02 0.67")]
            public string SpriteColor;
            [JsonProperty("Hud Preset Positions: ( 0 Left Top | 1 Left Bottom | 2 Right Top | 3 Right Bottom | 4 Custom )")]
            public int Preset;
            [JsonProperty("Hud Transparency Default: #, 0.1f")]
            public Colors Transparency;
            [JsonProperty("Text Color Default: #09ff00")]
            public Colors Text;
            [JsonProperty("Text Font Size Default: 13")]
            public int Size;
            [JsonProperty("Hex or RGB toggle: Default is Hex")]
            public bool ColorToggle;
            [JsonProperty("Custom UI POS: Key is anchorMin | Value is anchorMax")]
            public GuiPos GuiPos = new GuiPos();
        }

        public class GuiPos
        {
            [JsonProperty("Hud")]
            public KeyValuePair<string, string> HMinMax;
            [JsonProperty("Icon")]
            public KeyValuePair<string, string> IMinMax;
            [JsonProperty("Text")]
            public KeyValuePair<string, string> TMinMax;
        }

        private class CombatBlock
        {
            [JsonProperty("Enable Combat Block?")]
            public bool Enable;
            [JsonProperty("Block Time (Min)")]
            public int Time;
            [JsonProperty("Exclude Steam 64IDs")]
            public List<ulong> Exclude;
        }

        private class Raid
        {
            [JsonProperty("Enable Raid Block?")]
            public bool Enable;
            [JsonProperty("Raid Block player until death instead of distance checks or zones. + 'Optional' timer setting in seconds Default: 0.0 = disabled.")]
            public Death Death;
            [JsonProperty("Block Time (Sec)")]
            public float Time;
            [JsonProperty("Block Radius")]
            public float Radius;
            [JsonProperty("Damaged Health Percentage on an entity to trigger a raid (0 = disabled)")]
            public int TriggerPercentage;
            [JsonProperty("Sphere Visibility (Recommend 3 or 5, 0 = disabled)")]
            public int Spheres;
            [JsonProperty("Sphere Color (0 = none, 1 = Blue, 2 = Cyan, 3 = Green, 4 = Pink, 5 = Purple, 6 = Red, 7 = White, 8 = Yellow, 9 = Turquoise, 10 = Brown)")]
            public SphereColor Color;
            [JsonProperty("Sphere generation speed, how fast the bubble grows Default: 10f, max 100f")]
            public float LerpSpeed;
            [JsonProperty("Enable Random Sphere Colors? (Randomly selects a new color each time a raid block is triggered)")]
            public bool RandomColor;
            [JsonProperty("Allow Upgrade or Block?")]
            public bool Upgrade;
            [JsonProperty("Override facepunches default repair wait time after being attacked? Default: 30sec")]
            public int Repair;
            [JsonProperty("Enable Base Building Block Features")]
            public bool Building;
            [JsonProperty("Apply Combat Blocking when leaving a raid zone")]
            public bool Combat;
        }

        private class Death
        {
            public bool Die;
            public float Time;
            [JsonProperty("Raid Block TC Owner (Person who placed TC)")]
            public bool Owner;
            [JsonProperty("Raid Block TC Owner's Team")]
            public bool Team;
            [JsonProperty("Raid Block Everyone On TC")]
            public bool TC;
        }

        private class Building
        {
            [JsonProperty("Raid Blocked Building Spawned Health Percentage")]
            public int StartHealth;
            [JsonProperty("Health Regen Rate (Sets how fast it gens the health every x(Sec)")]
            public float RegenRate;
            [JsonProperty("Regen Amount (0 = Disabled Sets how much to regen every x(Sec)")]
            public float RegenAmount;
            [JsonProperty("After Being Attacked Regen Time (Sec)")]
            public float AttackedRegenTime;
        }

        private class Upgrading
        {
            [JsonProperty("Raid Blocked Upgrading Spawned Health Percentage")]
            public int StartHealth;
            [JsonProperty("Health Regen Rate (Sets how fast it gens the health every x(Sec)")]
            public float RegenRate;
            [JsonProperty("Regen Amount (0 = Disabled Sets how much to regen every x(Sec)")]
            public float RegenAmount;
            [JsonProperty("After Being Attacked Regen Time (Sec)")]
            public float AttackedRegenTime;
        }

        private class Lang
        {
            public ulong ChatIcon = 0;
            public string RaidBlocked = "You are now <color=#00FF00>raid blocked</color>! For <color=#00FF00>{0}</color>!";
            public string UnRaidBlocked = "You are <color=#00FF00>no longer</color> raid blocked.";
            public string CombatBlocked = "You are <color=#00FF00>combat blocked</color> For <color=#00FF00>{0}</color>.";
            public string UnCombatBlocked = "You are <color=#00FF00>no longer</color> combat blocked.";
            public string CommandBlocked = "Access Denied: Cannot use <color=#FFA500>'{0}'</color> command during <color=#FFA500>{1}</color>: <color=#FFA500>{2}</color>";
            public string ActionBlocked = "Denied: Cannot <color=#FFA500>{0}</color> while <color=#FFA500>raid blocked</color>";
            public string RepairBlocked = "Unable to repair: Recently damaged. Repairable in: ";
        }

        private class Effect
        {
            public string RaidStart = "assets/bundled/prefabs/fx/takedamage_hit.prefab";
            public string CombatSart = "assets/bundled/prefabs/fx/kill_notify.prefab";
            public string RaidEnd = "assets/prefabs/building/door.hinged/effects/vault-metal-close-end.prefab";
            public string CombatEnd = "assets/prefabs/building/door.hinged/effects/vault-metal-close-end.prefab";
            public string Denied = "assets/prefabs/weapons/toolgun/effects/repairerror.prefab";
        }

        #region Updater

        internal class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                            .ToDictionary(prop => prop.Name, prop => ToObject(prop.Value));
                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue) token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        #endregion

        #endregion

        #region Harmony

        [HarmonyPatch(typeof(BuildingBlock), nameof(BuildingBlock.ChangeGrade))]
        private static class BuildingBlock_ChangeGrade
        {
            [HarmonyPostfix]
            static void Postfix(BuildingBlock __instance)
            {
                Interface.CallHook("OnBuildingBlockChangedGrade", __instance);
            }
        }

        #endregion

        #region Hooks

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
                if (MaybeUpdateConfig(_config))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (FileNotFoundException)
            {
                PrintWarning($"No {Name} configuration file found, creating new config.");
                LoadDefaultConfig();
                SaveConfig();
            }
            catch (Exception ex)
            {
                PrintWarning("Failed to load config file (is the config file corrupt?) (" + ex.Message + ")");
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void Init()
        {
            _instance = this;
            Unsubscribe(nameof (OnPlayerAttack));
            Unsubscribe(nameof (OnEntityTakeDamage));
            Unsubscribe(nameof (OnEntityDeath));
            Unsubscribe(nameof (OnEntityBuilt));
            Unsubscribe(nameof (OnStructureUpgrade));
            Unsubscribe(nameof(OnStructureRepair));
            Unsubscribe(nameof (OnPlayerCommand));
            permission.RegisterPermission(Admin, this);
        }

        private void Loaded()
        {
            if (_config.Commands.IsNullOrEmpty())
            {
                _config.Commands = new Hash<string, Type>
                {
                    { "shop", Type.All },
                };
                SaveConfig();
            }

            if (_config.RaidBlock.Death.Die)
                _death = true;

            _pool = new NumberPool();

            string name = _config.Commands.First().Key;
            if (string.IsNullOrEmpty(name))
                return;

            foreach (KeyValuePair<string, Type> c in _config.Commands)
            {
                string check = c.Key;
                if (c.Key.Contains("/"))
                    check = c.Key.Replace("/", "");

                _commands.Add(check, c.Value);
            }

            Subscribe(nameof (OnPlayerCommand));
        }

        private void OnServerInitialized()
        {
            if (_config.RaidBlock.Upgrade)
            {
                _harmony = new Harmony(Name + "Patch");
                _instance._harmony.PatchAll(typeof(BuildingBlock_ChangeGrade).Assembly);
                Subscribe(nameof(OnBuildingBlockChangedGrade));
            }

            // Added F1 Grenades & MLRS
            /*_check = new List<int>
            {
                143803535,
                -1843426638
            };*/

            _gui = _config.GUI;
            string response = string.Empty;
            GuiPos pos = null;
            if (_config.RaidBlock.Enable)
            {
                if (_gui.RaidGUI.Raid)
                {
                    if (!_gui.Umod)
                    {
                        pos = _gui.RaidGUI.Preset == 4 ? _gui.RaidGUI.GuiPos : _presets[_gui.RaidGUI.Preset > 4 ? _config.GUI.RaidGUI.Preset = 0 : _gui.RaidGUI.Preset];

                        if (_gui.RaidGUI.Icon > 0)
                        {
                            ItemDefinition check = ItemManager.FindItemDefinition(_gui.RaidGUI.Icon);
                            if (check == null)
                                response = $"Invalid Item ID '{_gui.RaidGUI.Icon}' for Raid Icon";
                            ulong skin = _gui.RaidGUI.SkinId;
                            if (skin != 0)
                            {
                                TrySkinChangeItem(ref check, ref skin);
                                if (skin == 0)
                                    response += $"Invalid Skin ID '{_gui.RaidGUI.SkinId}' for Raid Icon";
                            }
                        }

                        _presets.Add(_gui.RaidGUI.GuiPos);
                        RaidUI(pos);
                    }
                    else
                        RaidUI();
                }

                Subscribe(nameof (OnEntityTakeDamage));
                Subscribe(nameof (OnEntityDeath));
                Subscribe(nameof(OnStructureRepair));
                if (_config.RaidBlock.Building)
                    Subscribe(nameof (OnEntityBuilt));
                if (!_config.RaidBlock.Upgrade)
                    Subscribe(nameof (OnStructureUpgrade));
            }

            if (_config.CombatBlock.Enable)
            {
                _ignore = _config.CombatBlock.Exclude;
                _combatTime = _config.CombatBlock.Time * 60;

                if (_gui.CombatGUI.Combat)
                {
                    if (!_gui.Umod)
                    {
                        pos = _gui.CombatGUI.Preset == 4 ? _gui.CombatGUI.GuiPos : _presets[_gui.CombatGUI.Preset > 4 ? _config.GUI.CombatGUI.Preset = 0 : _gui.CombatGUI.Preset];

                        if (_gui.CombatGUI.Icon > 0)
                        {
                            ItemDefinition check = ItemManager.FindItemDefinition(_gui.CombatGUI.Icon);
                            if (check == null)
                                response += $" & Invalid Item ID '{_gui.CombatGUI.Icon}' for Combat Icon";
                            ulong skin = _gui.CombatGUI.SkinId;
                            if (skin != 0)
                            {
                                TrySkinChangeItem(ref check, ref skin);
                                if (skin == 0)
                                    response += $"Invalid Skin ID '{_gui.CombatGUI.SkinId}' for Combat Icon";
                            }
                        }

                        _presets.Add(_gui.CombatGUI.GuiPos);
                        CombatUI(pos);
                    }
                    else
                        CombatUI();
                }

                Subscribe(nameof(OnPlayerAttack));
            }

            if (!string.IsNullOrEmpty(response))
                PrintError(response);
        }

        private void Unload()
        {
            _ignoreToggle.Clear();
            _presets.Clear();
            _ignore.Clear();
            _commands.Clear();

            if (_raidZones.Count > 0)
                foreach (Zone zone in _raidZones.ToArray())
                    zone.DeleteZone();

            if (_combat.Count > 0)
                foreach (KeyValuePair<ulong, Combat> combatBlock in _combat.ToArray())
                    combatBlock.Value.Dispose();

            _combat.Clear();
            _raiders.Clear();
            Cui.ClearGUI();
            _combatTime = 0;
            _instance._jsonPayloadRaid = string.Empty;
            _instance._jsonPayloadCombat = string.Empty;
            _death = false;
            _gui = null;
            if (_config.RaidBlock.Upgrade && !Rust.Application.isQuitting)
            {
                Unsubscribe(nameof(OnBuildingBlockChangedGrade));
                _harmony.UnpatchAll(_harmony.Id);
                Puts($"Removed All Patches relating to {Name}");
                _harmony = null;
            }
            _pool.Clear();

           // _check = null;
            _config = null;
            IEnumerable<Regen> regenComponents = BaseNetworkable.serverEntities.OfType<Regen>();
            if (regenComponents.Count() > 0)
                foreach (Regen healComponent in regenComponents)
                    healComponent.Dispose();

            _pool = null;
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            ulong userID = (ulong)player.userID;
            if (_death)
                _raiders.Remove(userID);
            else if (TryGetZone(player.ServerPosition, out _zone))
            {
                _display[_zone.id].Remove(player.net.connection);
                _raiders.Remove(userID);
                Cui.UpdateGUI(player.net.connection, null, false);
            }
            FindCombatBlock(userID)?.Dispose();
        }

        // mlrs prefabid 223554808 | shortprefabid mlrs.entity | type MLRS
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            BasePlayer attacker = info?.InitiatorPlayer;
            if (attacker == null || !attacker.userID.Get().IsSteamId())
                return null;

            int value = _config.RaidBlock.TriggerPercentage;
            bool force = false;
            if (value > 0)
            {
                float amount = entity.MaxHealth() / 100 * value;
                if (entity.health > amount)
                    return null;

                force = true;
            }

            uint id = info?.WeaponPrefab?.prefabID ?? 0;
            bool nade = id == 1128089209 || id == 1217937936;
            if (!force && info.damageTypes.GetMajorityDamageType() != DamageType.Explosion && info.damageTypes.GetMajorityDamageType() != DamageType.Heat && !nade)
                return null;

            if (!IsTriggerEntity(entity))
                return null;

            if (!force && (info.damageTypes.Has(DamageType.Bullet) && !nade || (double) Vector3.Distance(entity.ServerPosition, attacker.ServerPosition) > _config.RaidBlock.Radius))
                return null;

            ulong userID = (ulong)attacker.userID;
            if (!_ignoreToggle.Contains(userID) && SameTeam(entity, attacker))
                return null;

            if ((RaidProtection?.Call<float>("GetProtectionPercent", entity) ?? 0) > 0)
                return null;

            if (_death)
            {
                //List<ulong> skip = new List<ulong>();
                float moment = Time.realtimeSinceStartup + _config.RaidBlock.Death.Time;
                float time;
                if (!_raiders.TryGetValue(userID, out time))
                {
                    _raiders[userID] = moment;
                    Interface.CallHook("OnRaidBlock", attacker);
                    attacker.net.connection.RunEffect(_config.Effect.RaidStart);
                    FindCombatBlock(userID)?.UpdateGUI();
                    Response(attacker, _config.Msg.RaidBlocked, _config.RaidBlock.Death.Time > 0 ? $"{FormatTime((long)(moment - Time.realtimeSinceStartup))} or Until Death" : "Until Death");
                }
                else
                    _raiders[userID] = moment;

                /*skip.Add(userID);
                BuildingPrivlidge tc = entity?.GetBuildingPrivilege();
                if (tc != null)
                {
                    if (_config.RaidBlock.Death.TC && tc.authorizedPlayers.Count > 0)
                    {
                        foreach (var p in tc.authorizedPlayers)
                        {
                            ulong user = p.userid;
                            if (skip.Contains(user)) continue;
                            BasePlayer found = BasePlayer.FindByID(user);
                            if (found == null || found.Connection.connected == false) continue;
                            float check;
                            if (!_raiders.TryGetValue(user, out check))
                            {
                                _raiders[user] = moment;
                                Interface.CallHook("OnRaidBlock", found);
                                found.net.connection.RunEffect(_config.Effect.RaidStart);
                                FindCombatBlock(user)?.UpdateGUI();
                                Response(found, _config.Msg.RaidBlocked, _config.RaidBlock.Death.Time > 0 ? $"{FormatTime((long)(moment - Time.realtimeSinceStartup))} or Until Death" : "Until Death");
                            }
                            else
                                _raiders[user] = moment;

                            skip.Add(user);
                        }
                    }

                    if (_config.RaidBlock.Death.Owner && tc?.OwnerID != 0 && !skip.Contains(tc.OwnerID))
                    {
                        ulong user = tc.OwnerID;
                        BasePlayer found = BasePlayer.FindByID(tc.OwnerID);
                        if (found != null && found.Connection.connected)
                        {
                            float check;
                            if (!_raiders.TryGetValue(user, out check))
                            {
                                _raiders[user] = moment;
                                Interface.CallHook("OnRaidBlock", found);
                                found.net.connection.RunEffect(_config.Effect.RaidStart);
                                FindCombatBlock(user)?.UpdateGUI();
                                Response(found, _config.Msg.RaidBlocked, _config.RaidBlock.Death.Time > 0 ? $"{FormatTime((long)(moment - Time.realtimeSinceStartup))} or Until Death" : "Until Death");
                            }
                            else
                                _raiders[user] = moment;

                            skip.Add(user);
                        }
                    }
                }
                if (_config.RaidBlock.Death.Team && attacker.Team != null && attacker.Team.members.Count > 1)
                {
                    foreach (ulong user in attacker.Team.members)
                    {
                        if (skip.Contains(user)) continue;
                        BasePlayer found = BasePlayer.FindByID(user);
                        float check;
                        if (!_raiders.TryGetValue(user, out check))
                        {
                            _raiders[user] = moment;
                            Interface.CallHook("OnRaidBlock", found);
                            found.net.connection.RunEffect(_config.Effect.RaidStart);
                            FindCombatBlock(user)?.UpdateGUI();
                            Response(found, _config.Msg.RaidBlocked, _config.RaidBlock.Death.Time > 0 ? $"{FormatTime((long)(moment - Time.realtimeSinceStartup))} or Until Death" : "Until Death");
                        }
                        else
                            _raiders[user] = moment;

                        skip.Add(user);
                    }
                }

                skip.Clear();*/
            }
            else if (TryGetZone(entity.transform.position, out _zone))
                _zone.timeLeft = _config.RaidBlock.Time;
            else
                CreateZone(entity);

            return null;
        }

        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            NextFrame(() =>
            {
                if (player == null || !player.userID.IsSteamId()) return;
                ulong userID = (ulong)player.userID;
                if (_death)
                {
                    float time = _raiders[userID] - Time.realtimeSinceStartup;
                    if (time > 0)
                        Response(player, _config.Msg.UnRaidBlocked);
                    _raiders.Remove(userID);
                }
                else if (TryGetZone(player.ServerPosition, out _zone))
                {
                    _display[_zone.id].Remove(player.net.connection);
                    _raiders.Remove(userID);
                    Response(player, _config.Msg.UnRaidBlocked);
                    Cui.UpdateGUI(player.net.connection, null, false);
                }

                FindCombatBlock(userID)?.Dispose();
            });
        }

        private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (info == null || !attacker.userID.IsSteamId()) return;
            BasePlayer victim = info.HitEntity as BasePlayer;
            if (victim == null) return;
            if (!_ignoreToggle.Contains((ulong)attacker.userID))
            {
                if (!victim.userID.IsSteamId()) return;
                if (attacker.currentTeam != 0 && attacker.currentTeam == victim.currentTeam) return;
                if (!IsRaidBlocked(victim.UserIDString) && !_ignore.Contains((ulong)victim.userID))
                    HandleCombatBlock(victim);
            }

            if (!IsRaidBlocked(attacker.UserIDString) && !_ignore.Contains((ulong)attacker.userID))
                HandleCombatBlock(attacker);
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            BasePlayer player = plan.GetOwnerPlayer();
            if (player == null) return;

            BaseEntity entity = go?.ToBaseEntity();
            if (IsRaidBlocked(player.UserIDString))
                CheckEntity(entity);
        }

        private void OnBuildingBlockChangedGrade(BuildingBlock block)
        {
            //if (!block.gameObject.HasComponent<Regen>()) return;
            Regen regenComponent = block.gameObject.GetComponent<Regen>();
            if (regenComponent?.iD != block.net.ID.Value) return;
            Upgrading set = _config.Upgrading[block.grade];
            float amount = block.MaxHealth() / 100 * set.StartHealth;
            regenComponent.Restart(amount, set.RegenRate, set.RegenAmount, set.AttackedRegenTime);
        }

        private object OnStructureUpgrade(StabilityEntity stabilityEntity, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (!IsRaidBlocked(player.UserIDString))
                return null;

            Response(player, _config.Msg.ActionBlocked, "UpGrade");
            return false;
        }

        private object OnPlayerCommand(BasePlayer player, string command, string[] args) => player == null ? null : CanDo(command, player);

        // BaseCombatEntity
        private object OnStructureRepair(StabilityEntity entity, BasePlayer player)
        {
            if (!IsRaidBlocked(player.UserIDString))
                return null;

            int repair = _config.RaidBlock.Repair;
            if (repair == 0)
            {
                Response(player, _config.Msg.ActionBlocked, "Repair Building");
                return null;
            }

            int time = (int)entity.SecondsSinceAttacked;
            if (time > 0 && time <= repair)
            {
                Response(player, _config.Msg.RepairBlocked + FormatTime(repair - time));
                return false;
            }

            if (!entity.gameObject.HasComponent<Regen>()) return null;
            Response(player, _config.Msg.ActionBlocked, "Repair a Regen Block");
            return false;
        }

        #endregion

        #region Helpers

        private bool IsTriggerEntity(BaseEntity entity)
        {
            if ((entity as BuildingBlock)?.grade == BuildingGrade.Enum.Twigs) 
                return false;

            return entity is StabilityEntity || 
                   entity is AutoTurret || 
                   entity is SamSite ||
                   entity is ShopFront || 
                   entity is VendingMachine;
        }

        private bool TryGetZone(Vector3 pos, out Zone data)
        {
            data = null;
            foreach (Zone zone in _raidZones)
                if (Vector3Ex.Distance2D(pos, zone.transform.position) < _config.RaidBlock.Radius)
                {
                    data = zone;
                    return true;
                }

            return false;
        }

        private void CreateZone(BaseEntity entity)
        {
            Zone zoneTime = new GameObject
            {
                layer = (int)Layer.Reserved1,
                name = "Raid Zone",
                transform =
                {
                    position = entity.transform.position,
                    rotation = Quaternion.Euler(0f, 1f, 0f)
                }
            }.AddComponent<Zone>();

            zoneTime.timeLeft = _config.RaidBlock.Time;
            SphereCollider col = zoneTime.gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = _config.RaidBlock.Radius;
            _raidZones.Add(zoneTime);
        }

        private Combat FindCombatBlock(ulong userID)
        {
            Combat combatBlock;
            return _combat.TryGetValue(userID, out combatBlock) ? combatBlock : null;
        }

        private void HandleCombatBlock(BasePlayer player)
        {
            Combat combatBlock = FindCombatBlock((ulong)player.userID);
            if (combatBlock != null)
            {
                combatBlock.ResetBlock(_combatTime);
                return;
            }

            player.gameObject.AddComponent<Combat>().BeginBlock(_combatTime);
        }

        // may have to wrap this in a NextTick TBD
        private void CheckEntity(BaseEntity entity)
        {
            if (!entity.IsValid() || entity.ShortPrefabName.Contains("external.high"))
                return;

            if (entity is SimpleBuildingBlock || entity is BuildingBlock || entity is Door || entity is ShopFront || entity is VendingMachine)
            {
                BuildingGrade.Enum grade = (entity as BuildingBlock)?.grade ?? BuildingGrade.Enum.None;
                Building set = _config.Building[grade];
                float amount = entity.MaxHealth() / 100 * set.StartHealth;
                entity.gameObject.AddComponent<Regen>().OnModifyState(amount, set.RegenRate, set.RegenAmount, set.AttackedRegenTime);
            }
        }

        private static string FormatTime(long seconds)
        {
            TimeSpan timespan = TimeSpan.FromSeconds(seconds);
            if (timespan.TotalHours >= 1)
                return string.Format("{0:0}h {1:0}m {2:0}s", timespan.Hours, timespan.Minutes, timespan.Seconds);
            if (timespan.TotalMinutes >= 1)
                return string.Format("{0:0}m {1:0}s", timespan.Minutes, timespan.Seconds);

            return string.Format("{0:0}s", timespan.Seconds);
        }

        private void Response(BasePlayer player, string message, params object[] args)
        {
            if (player == null)
                return;

            ConsoleNetwork.SendClientCommand(player.net.connection, "chat.add", 2, _config.Msg.ChatIcon, string.Format(message, args));
        }

        private object CanDo(string command, BasePlayer player)
        {
            if (!_commands.ContainsKey(command))
                return null;

            Type commandType = _commands[command];
            ulong user = (ulong)player.userID;
            if (commandType.HasFlag(Type.Raid) && _raiders.ContainsKey(user))
            {
                if (_death)
                {
                    float time = _raiders[user] - Time.realtimeSinceStartup;
                    if (time <= 0)
                    {
                        _raiders.Remove(user);
                        return null;
                    }
                    ProcessCommandBlock(player, command, Type.Raid, _config.RaidBlock.Death.Time > 0 ? $"For {FormatTime((long)time)} or Until Death" : "Until Death");
                    return true;
                }

                if (!TryGetZone(player.ServerPosition, out _zone)) return null;
                ProcessCommandBlock(player, command, Type.Raid, FormatTime((long)_zone.timeLeft));
                return true;
            }

            if (!commandType.HasFlag(Type.Combat) || !_combat.ContainsKey(user)) return null;
            ProcessCommandBlock(player, command, Type.Combat, FormatTime(FindCombatBlock(user).TimeRemaining));
            return true;
        }

        private void ProcessCommandBlock(BasePlayer player, string cmd, Type blockType, string time)
        {
            Response(player, _config.Msg.CommandBlocked, cmd, blockType, time);
            player.net.connection.RunEffect(_config.Effect.Denied);
        }

        private bool SameTeam(BaseEntity entity, BasePlayer attacker) => (entity.OwnerID == 0 || entity.OwnerID == (ulong)attacker.userID || attacker.currentTeam != 0 && attacker.Team.members.Contains(entity.OwnerID));

        #endregion

        #region API

        bool IsRaidBlocked(BasePlayer player) => _raiders.ContainsKey((ulong)player.userID);
        bool IsCombatBlocked(BasePlayer player) => IsCombatBlocked(player.UserIDString);
        bool IsBlocked(BasePlayer player) => IsCombatBlocked(player) || IsRaidBlocked(player);
        bool IsEscapeBlocked(BasePlayer player) => IsEscapeBlocked(player.UserIDString);

        bool IsRaidBlocked(string player) => _raiders.ContainsKey(Convert.ToUInt64(player));
        bool IsCombatBlocked(string player) => FindCombatBlock(Convert.ToUInt64(player)) != null;
        bool IsEscapeBlocked(string player) => FindCombatBlock(Convert.ToUInt64(player)) != null || IsRaidBlocked(player);

        bool IsRaidBlocked(ulong player) => _raiders.ContainsKey(player);
        bool IsCombatBlocked(ulong player) => FindCombatBlock(player) != null;
        bool IsEscapeBlocked(ulong player) => FindCombatBlock(player) != null || IsRaidBlocked(player);

        #endregion

        #region UI Helpers

        private void Panel(ref CuiElementContainer container, string name, string anchorMin, string anchorMax, string color)
        {
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image =
                {
                    Color = color,
                },
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                }
            }, "Hud", name);
        }

        private void Label(ref CuiElementContainer container, string parent, string anchorMin, string anchorMax, string text, int fontSize, string color, TextAnchor textAnchor = TextAnchor.MiddleLeft, bool fontbold = false)
        {
            container.Add(new CuiLabel
            {
                Text = {
                    Text = text,
                    FontSize = fontSize,
                    Color =  color,
                    Align = textAnchor,
                    Font = fontbold ? "RobotoCondensed-Bold.ttf" : "RobotoCondensed-Regular.ttf"
                },
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                }
            }, parent);
        }

        private void Icon(ref CuiElementContainer container, string parent, string anchorMin, string anchorMax, int icon = 0, ulong skin = 0)
        {
            container.Add(new CuiElement
            {
                Parent = parent,
                Name = Image,
                Components =
                {
                    new CuiImageComponent
                    {
                        ItemId = icon,
                        SkinId = skin
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax
                    }
                }
            });
        }

        private void Sprite(ref CuiElementContainer container, string parent, string anchorMin, string anchorMax, string sprite, string color)
        {
            container.Add(new CuiElement
            {
                Parent = parent,
                Name = Image,
                Components =
                {
                    new CuiImageComponent
                    {
                        Sprite = sprite,
                        Color = color
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax
                    }
                }
            });
        }

        private static class Cui
        {
            private const string DUI = "DestroyUI";
            private const string AUI = "AddUI";

            public static void UpdateGUI(List<Connection> players, string json)
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(players), null, DUI, ContentPanel);
                CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(players), null, AUI, json);
            }

            public static void ClearGUI(List<Connection> players) => CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(players), null, DUI, ContentPanel);

            public static void UpdateGUI(Connection player, string json, bool update = true)
            {
                if (player == null)
                    return;

                CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(player), null, DUI, ContentPanel);

                if (update)
                    CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(player), null, AUI, json);
            }

            public static void ClearGUI()
            {
                foreach (BasePlayer player in BasePlayer.allPlayerList)
                    UpdateGUI(player.net.connection, null, false);
            }
        }

        public class Colors
        {
            [JsonIgnore]
            public int R;
            [JsonIgnore]
            public int G;
            [JsonIgnore]
            public int B;
            [JsonIgnore]
            public float A;
            public string Hex;
            public string Rgb;

            public Colors(string hex, float alpha = 1f)
            {
                if (hex.StartsWith("#"))
                    hex = hex.Substring(1);

                if (hex.Length == 6)
                {
                    R = int.Parse(hex.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                    G = int.Parse(hex.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                    B = int.Parse(hex.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                }

                A = alpha;
                Hex = "#" + hex;
                Rgb = $"{(double)R / 255} {(double)G / 255} {(double)B / 255} {A}";
            }

            public string Convert()
            {
                string update = Hex.Replace("#", "");
                R = int.Parse(update.Substring(0, 2), NumberStyles.HexNumber);
                G = int.Parse(update.Substring(2, 2), NumberStyles.HexNumber);
                B = int.Parse(update.Substring(4, 2), NumberStyles.HexNumber);
                return $"{(double)R / 255} {(double)G / 255} {(double)B / 255} 1";
            }
        }

        private static void TrySkinChangeItem(ref ItemDefinition template, ref ulong skinId)
        {
            if (skinId == 0UL) return;
            ItemSkinDirectory.Skin inventoryDefinitionId = ItemSkinDirectory.FindByInventoryDefinitionId((int) skinId);
            if (inventoryDefinitionId.id == 0) return;
            ItemSkin invItem = inventoryDefinitionId.invItem as ItemSkin;
            if (invItem == null || invItem.Redirect == null) return;
            template = invItem.Redirect;
            skinId = 0UL;
        }

        #endregion

        #region User Interface

        private const string ContentPanel = "Content_Panel";
        private const string Image = "Icon_Pic";
        private string _jsonPayloadRaid;
        private string _jsonPayloadCombat;

        // umod's ui look-alike.
        private void RaidUI()
        {
            CuiElementContainer container = new CuiElementContainer();
            Panel(ref container, ContentPanel, "0.838 0.293", "0.986 0.324", "0.95 0 0.02 0.67");
            Sprite(ref container, ContentPanel, "0 0", "0.15 1", "assets/icons/explosion.png", new Colors("#570E0E", 0.9f).Convert());

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.2 0",
                    AnchorMax = "0.8 1"
                },
                Text =
                {
                    Color = new Colors("#FFFFFF", 0.9f).Convert(),
                    Text = "RAID BLOCK",
                    FontSize = 15,
                    Align = TextAnchor.MiddleLeft,
                }
            }, ContentPanel);
            container.Add(new CuiElement
            {
                Name = "TimerPanel",
                Parent = ContentPanel,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = new Colors("#570E0E", 0.9f).Convert(),
                        ImageType = UnityEngine.UI.Image.Type.Filled
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.73 0",
                        AnchorMax = "1 0.95"
                    }
                }
            });
            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                Text =
                {
                    Color = new Colors("#FFFFFF", 0.9f).Convert(),
                    Text = "_Count",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter,
                }
            }, "TimerPanel");

            _jsonPayloadRaid = container.ToJson();
            container.Clear();
        }

        private void CombatUI()
        {
            CuiElementContainer container = new CuiElementContainer();
            Panel(ref container, ContentPanel, "0.838 0.293", "0.986 0.324", "0.95 0 0.02 0.67");
            Sprite(ref container, ContentPanel, "0.02 0.1", "0.13 0.9", "assets/icons/bullet.png", new Colors("#570E0E", 0.9f).Convert());

            container.Add (new CuiLabel {
                RectTransform =
                {
                    AnchorMin = "0.2 0",
                    AnchorMax = "0.8 1"
                },
                Text =
                {
                    Color = new Colors("#FFFFFF", 0.9f).Convert(),
                    Text = "COMBAT BLOCK",
                    FontSize = 14,
                    Align = TextAnchor.MiddleLeft,
                }
            }, ContentPanel);
            container.Add (new CuiElement {
                Name = "TimerPanel",
                Parent = ContentPanel,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = new Colors("#570E0E", 0.9f).Convert(),
                        ImageType = UnityEngine.UI.Image.Type.Filled
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.73 0",
                        AnchorMax = "1 0.95"
                    }
                }
            });
            container.Add (new CuiLabel {
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                Text =
                {
                    Color = new Colors("#FFFFFF", 0.9f).Convert(),
                    Text = "_Count",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter,
                }
            }, "TimerPanel");
            
            _jsonPayloadCombat = container.ToJson();
            container.Clear();
        }

        private void RaidUI(GuiPos pos)
        {
            CuiElementContainer container = new CuiElementContainer();
            Panel(ref container, ContentPanel, pos.HMinMax.Key, pos.HMinMax.Value, _gui.RaidGUI.Transparency.Rgb);
            Label(ref container, ContentPanel, pos.TMinMax.Key, pos.TMinMax.Value, "Raid Block _Count", _gui.RaidGUI.Size,  _gui.RaidGUI.ColorToggle ? _gui.RaidGUI.Text.Convert() : _gui.RaidGUI.Text.Rgb);
            if (_gui.RaidGUI.Icon > 0)
            {
                if (_gui.RaidGUI.SpriteToggle)
                    Sprite(ref container, ContentPanel, pos.IMinMax.Key, pos.IMinMax.Value, _gui.RaidGUI.Sprite, _gui.RaidGUI.SpriteColor);
                else
                    Icon(ref container, ContentPanel, pos.IMinMax.Key, pos.IMinMax.Value, _gui.RaidGUI.Icon, _gui.RaidGUI.SkinId);
            }
            _jsonPayloadRaid = container.ToJson();
            container.Clear();
        }

        private void CombatUI(GuiPos pos)
        {
            CuiElementContainer container = new CuiElementContainer();
            Panel(ref container, ContentPanel, pos.HMinMax.Key, pos.HMinMax.Value, _gui.CombatGUI.Transparency.Rgb);
            Label(ref container, ContentPanel, pos.TMinMax.Key, pos.TMinMax.Value, "Combat Block _Count", _gui.CombatGUI.Size, _gui.CombatGUI.ColorToggle ? _gui.CombatGUI.Text.Convert() : _gui.CombatGUI.Text.Rgb);
            if (_gui.CombatGUI.Icon > 0)
            {
                if (_gui.CombatGUI.SpriteToggle)
                    Sprite(ref container, ContentPanel, pos.IMinMax.Key, pos.IMinMax.Value, _gui.CombatGUI.Sprite, _gui.CombatGUI.SpriteColor);
                else
                    Icon(ref container, ContentPanel, pos.IMinMax.Key, pos.IMinMax.Value, _gui.CombatGUI.Icon, _gui.CombatGUI.SkinId);
            }
            _jsonPayloadCombat = container.ToJson();
            container.Clear();
        }

        #endregion

        #region Cmd

        [ConsoleCommand("newcolor")]
        private void CmdNewColor(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            bool user = player == null;
            if (!user)
            {
                if (!permission.UserHasPermission(player.UserIDString, Admin))
                    return;
            }
            int check = arg?.Args?.Length ?? 0;
            string msg = string.Empty;
            SphereColor c;
            switch (check)
            {
                case 1:
                    c = (SphereColor)arg.GetInt(0);
                    _config.RaidBlock.Color = c;
                    msg = $"updated to {c} Sphere Visibility level {_config.RaidBlock.Spheres}";
                    break;

                case 2:
                    c = (SphereColor)arg.GetInt(0);
                    _config.RaidBlock.Color = c;
                    int s = arg.GetInt(1);
                    _config.RaidBlock.Spheres = s;
                    msg = $"updated to {c} Sphere Visibility level {s}";
                    break;

                default:
                    if (!user)
                        msg = "color 10 5\n0  | none\n1  | Blue\n2  | Cyan\n3  | Green\n4  | Pink\n5  | Purple\n6  | Red\n7  | White\n8  | Yellow\n9  | Turquoise\n10 | Brownish";
                    else
                        msg = "color 10 5\n0   | none\n1   | Blue\n2   | Cyan\n3   | Green\n4   | Pink\n5   | Purple\n6   | Red\n7   | White\n8   | Yellow\n9   | Turquoise\n10  | Brownish";
                    break;
            }

            if (!user)
                player.ConsoleMessage(msg);
            else 
                Puts(msg);
            SaveConfig();
        }

        // Need to trigger raids on your self or combat block for NPCs? For testing! Use the new command!
        // Example: F1 menu Type "noescape" in game to toggle for your self.
        // Example: F1 menu or server-console Type "noescape steamID" to toggle for someone else.
        // Requires the noescape.admin perm to use.
        private List<ulong> _ignoreToggle = new List<ulong>();
        [ConsoleCommand("noescape")]
        private void CmdTestPlugin(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            bool user = player == null;
            if (!user)
            {
                if (!permission.UserHasPermission(player.UserIDString, Admin))
                    return;
            }
            int check = arg?.Args?.Length ?? 0;
            string msg = string.Empty;
            ulong set = 0;
            switch (check)
            {
                case 0:
                    if (!user)
                        set = (ulong)player.userID;
                    if (!set.IsSteamId())
                        break;

                    if (_ignoreToggle.Contains(set))
                        _ignoreToggle.Remove(set);
                    else
                        _ignoreToggle.Add(set);

                    msg = $"SteamID {set} was {(_ignoreToggle.Contains(set) ? "\nAdded to" : "\nRemoved from")} the raid-self/combat-block-npcs list.";
                    break;

                case 1:
                    set = arg.GetUInt64(0);
                    if (!set.IsSteamId())
                        break;

                    if (_ignoreToggle.Contains(set))
                        _ignoreToggle.Remove(set);
                    else
                        _ignoreToggle.Add(set);

                    msg = $"SteamID {set} was {(_ignoreToggle.Contains(set) ? "\nAdded to" : "\nRemoved from")} the raid-self/combat-block-npcs list.";
                    break;

                default:
                    if (!user)
                        msg = $"Invalid Steam ID {set}\nExample: noescape steamID";
                    else
                        msg = $"Invalid Steam ID {set}, Example: noescape steamID";
                    break;
            }

            if (!user)
                player.ConsoleMessage(msg);
            else 
                Puts(msg);
            //SaveConfig();
        }

        #endregion
    }

    namespace NoEscapeEx
    {
        public static class PlayerEx
        {
            public static readonly Effect EffectInstance = new Effect();

            public static void RunEffect(this Network.Connection player, string prefabPath)
            {
                if (player == null || string.IsNullOrEmpty(prefabPath))
                    return;

                EffectInstance.Clear();
                EffectInstance.Init(Effect.Type.Generic, player.player.transform.position, Vector3.zero);
                EffectInstance.pooledString = prefabPath;
                EffectNetwork.Send(EffectInstance, player);
            }

            public static void RunEffects(this List<Network.Connection> players, string prefabPath)
            {
                if (players == null || string.IsNullOrEmpty(prefabPath))
                    return;

                EffectInstance.Clear();
                EffectInstance.broadcast = true;
                EffectInstance.pooledString = prefabPath;
                NetWrite netWrite = Net.sv.StartWrite();
                netWrite.PacketID(Message.Type.Effect);
                EffectInstance.WriteToStream((Stream)netWrite);
                netWrite.Send(new SendInfo(players));
            }
        }
    }
}