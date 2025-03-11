using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using UnityEngine;
using VLB;
using Color = UnityEngine.Color;
using Debug = UnityEngine.Debug;

namespace Oxide.Plugins
{
    [Info("Sentry Turrets", "Tangerine", "3.0.0")]
    [Description("https://codefling.com/plugins/sentry-turrets")]
    public class SentryTurrets : RustPlugin
    { 
        #region Classes

        private class TurretRadialButton
        {
            public List<ButtonSettings> States;

            public TurretRadialButton(List<ButtonSettings> states)
            {
                States = states;
            }
            
            public TurretRadialButton(string spriteIcon, string command, string text)
            {
                States = new List<ButtonSettings>()
                {
                    new (spriteIcon, command, text)
                };

            }
            
            public ButtonSettings GetState(NPCAutoTurret turret, int state)
            {
                if (States.Count > 1)
                {
                    return States[turret.PeacekeeperMode() ? 1 : 0];
                }
                
                return States[state];
            }
            
            public struct ButtonSettings
            {
                public string SpriteIcon;
                public string Command;
                public string Text;

                public ButtonSettings(string spriteIcon, string command, string text)
                {
                    SpriteIcon = spriteIcon;
                    Command = command;
                    Text = text;
                }
            }
        }
        
        private class PlayerInteraction
        {
            public PlayerInteraction(BasePlayer player)
            {
                Player = player;
            }
                
            public NPCAutoTurret Turret;
            public BasePlayer Player;
            public RealTimeSince LastCheck;
            public bool WasHoldingDown;
            public float HoldDownTime;
            public bool HasInteraction;
            public bool HasRadial;

            public bool IsInteracting() => HasInteraction || HasRadial;

            public void Clear()
            {
                HasRadial = false;
                HasInteraction = false;
                WasHoldingDown = false;
                _plugin.DestroyUI_InteractBtn(Player);
                _plugin.DestroyUI_RadialBtn(Player);
                Turret = null;
            }

            public static PlayerInteraction Get(BasePlayer player)
            {
                var interactions = _plugin._playersInteractions;
                
                if (interactions.TryGetValue(player.userID, out var interaction) == false)
                {
                    interaction = new PlayerInteraction(player);
                    interactions.Add(player.userID, interaction);
                }

                return interaction;
            }
        }

        private class TurretProfile
        {
            [JsonProperty(PropertyName = "Key name, for command")]
            public string KeyName = "default";
            
            [JsonProperty(PropertyName = "Item display name")]
            public string DisplayName = "Sentry Turret";

            [JsonProperty(PropertyName = "Skin Id")]
            public ulong SkinId = 1587601905;
            
            [JsonProperty(PropertyName = "Is weapon required?")]
            public bool needsWeapon = false;
            
            [JsonProperty(PropertyName = "Can get damage")]
            public bool getDamage = true;

            [JsonProperty(PropertyName = "Required power")]
            public int requiredPower = 0; 

            [JsonProperty(PropertyName = "Authorize friends and team members")]
            public bool authorizeOthers = false;

            [JsonProperty(PropertyName = "Authorize tc members")]
            public bool authorizeByTC = false;

            [JsonProperty(PropertyName = "Amount of ammo for one spray (set to 0 for no-ammo mode)")]
            public int spray = 1;
            
            [JsonProperty(PropertyName = "Amount of ammo for one air spray (set to 0 for no-ammo mode)")]
            public int sprayAir = 1;
            
            [JsonProperty(PropertyName = "Range (normal turret - 30)")]
            public int range = 55;
            
            [JsonProperty(PropertyName = "Air Range (set to 0 to disable air mode")]
            public int airRange = 100;
            
            [JsonProperty(PropertyName = "Air Fire Rate Every N seconds (Default every 1 second)")]
            public float airFireRate = 1f;

            [JsonProperty(PropertyName = "Give back on ground missing")]
            public bool itemOnGroundMissing = true;

            [JsonProperty(PropertyName = "Health (normal turret - 1000)")]
            public float health = 1500;

            [JsonProperty(PropertyName = "Aim cone (normal turret - 4)")]
            public float aimCone = 2;
        }
        #endregion
        
        #region Vars

        private const string PrefabSentry = "assets/content/props/sentry_scientists/sentry.scientist.static.prefab";
        private const string PrefabSwitch = "assets/prefabs/deployable/playerioents/simpleswitch/switch.prefab";
        private const string PrefabRocketSam = "assets/prefabs/npc/sam_site_turret/rocket_sam.prefab";
        private const string ItemName = "autoturret";
        private const string SentryLootPanel = "autoturret";
        private const string Command = "sentryturrets.give";
        private const string ItemDisplayName = "Sentry Turret";
        private const string AmmoShortname = "ammo.rifle";
        private const string AirAmmoShortname = "ammo.rocket.sam";
        private static readonly Vector3 SwitchPosition = new Vector3(-0.65f, 0.25f, -0.4f);
        private static readonly Vector3 SwitchRotation = new Vector3(-50f, 220f, 0f);

        private readonly object _falseObject = false;
        private readonly object _trueObject = true;
        
        private Harmony _harmony;

        private static SentryTurrets _plugin;

        private readonly Dictionary<NPCAutoTurret, TurretComponent> _turrets = new ();
        private readonly Dictionary<ulong, PlayerInteraction> _playersInteractions = new ();

        [PluginReference] private Plugin TurretsExtended;
        
        private readonly List<TurretRadialButton> _turretRadialButtons = new ()
        {
            new TurretRadialButton("assets/icons/open.png", "UI_SentryTurrets open", "OPEN"),
            new TurretRadialButton(new List<TurretRadialButton.ButtonSettings>()
            {
                new ("assets/icons/peace.png", "UI_SentryTurrets mode", "PEACEKEEPER"),
                new ("assets/icons/target.png", "UI_SentryTurrets mode", "ATTACK ALL"),
            }),
            new TurretRadialButton("assets/icons/rotate.png", "UI_SentryTurrets rotate", "ROTATE"),
            new TurretRadialButton("assets/icons/friends_servers.png", "UI_SentryTurrets authorize", "AUTHORIZE\nFRIEND"),
            new TurretRadialButton("assets/icons/clear_list.png", "UI_SentryTurrets authorize_clear", "CLEAR\nAUTHORIZED"),
            new TurretRadialButton("assets/icons/deauthorize.png", "UI_SentryTurrets deauthorize_self", "DEAUTHORIZE"),
            // new TurretRadialButton("assets/icons/broadcast.png", "UI_SentryTurrets broadcast_id", "SET ID"),
        };
        #endregion

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            
            InterfaceManager.Init();
            
            cmd.AddConsoleCommand(Command, this, nameof(cmdGiveConsole));
            
            _harmony = new Harmony("sentryturrets.patch");
            _harmony.Patch(typeof(NPCAutoTurret)
                    .GetMethod("Ignore", BindingFlags.Public | BindingFlags.Instance),
                new HarmonyMethod(typeof(SentryTurrets).GetMethod(nameof(Patch_NPCAutoTurret_Ignore))));
            
            _harmony.Patch(typeof(NPCAutoTurret) 
                    .GetMethod("FireGun", BindingFlags.Public | BindingFlags.Instance),
                new HarmonyMethod(typeof(SentryTurrets).GetMethod(nameof(Patch_NPCAutoTurret_FireGun))));
            
            _harmony.Patch(typeof(AutoTurret) 
                    .GetMethod("TargetTick", BindingFlags.Public | BindingFlags.Instance),
                new HarmonyMethod(typeof(SentryTurrets).GetMethod(nameof(Patch_NPCAutoTurret_TargetTick))));
            
            //DrawUI_PickUpBar(BasePlayer.activePlayerList[0], 0.5f);
        }

        [HarmonyPrefix]
        public static bool Patch_NPCAutoTurret_TargetTick(AutoTurret __instance)
        {
            if (__instance is NPCAutoTurret npcAutoTurret == false)
                return true;

            var turretComponent = _plugin.GetTurretComponent(npcAutoTurret);
            if (turretComponent == null)
                return true;

            turretComponent.TargetTick();
            return false;
        }
        
        [HarmonyPrefix]
        public static bool Patch_NPCAutoTurret_FireGun(NPCAutoTurret __instance)
        {
            if (__instance.OwnerID == 0)
                return true;
            
            var turretComponent = _plugin.GetTurretComponent(__instance);
            if (turretComponent == null)
                return true;
            
            if (__instance.GetAttachedWeapon() != null)
                return true;

            if (turretComponent.ConfigDef.needsWeapon)
                return false;

            if (turretComponent.IsAirTarget(turretComponent.TurretOwner.target))
            {
                var speedMultiplier = turretComponent.TurretOwner.target is SamSite.ISamSiteTarget samSiteTarget ? samSiteTarget.SAMTargetType.speedMultiplier : 3f;
                if (turretComponent.ConfigDef.sprayAir > 0 && turretComponent.TakeItem("ammo.rocket.sam", turretComponent.ConfigDef.sprayAir) == 0)
                    return false;

                var targetTransform = turretComponent.TurretOwner.target.transform;
                var targetPosition = targetTransform.position + turretComponent.TurretOwner.target.GetWorldVelocity() * 0.1f;
                var turretPosition = turretComponent.TurretOwner.muzzleLeft.position;
                var targetDirection = targetPosition - turretPosition;
                turretComponent.FireProjectile(targetDirection, speedMultiplier);
                return false;
            }
            
            if(turretComponent.ConfigDef.spray == 0)
                return true;

            return turretComponent.TakeItem(AmmoShortname, turretComponent.ConfigDef.spray) != 0;
        }
        
        [HarmonyPrefix]
        public static bool Patch_NPCAutoTurret_Ignore(NPCAutoTurret __instance, ref bool __result)
        {
            if (__instance.OwnerID == 0)
                return true;
            
            __result = false;
            return false;
        }

        private void OnServerInitialized()
        {
            CheckExistingTurrets();
            
            if(TurretsExtended != null)
                Unsubscribe(nameof(OnSwitchToggle)); 
            
            StartPluginLoad(); 
        }

        private void Unload()
        {
            _plugin = null;
            _harmony.UnpatchAll(_harmony.Id);
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            CheckPlacement(plan, go);
        }

        private object CanPickupEntity(BasePlayer player, NPCAutoTurret entity)
        {
            CheckPickup(player, entity);
            return _falseObject;
        }
        
        private object CanPickupEntity(BasePlayer player, ElectricSwitch entity)
        {
            var npcTurret = entity.GetParentEntity() as NPCAutoTurret;
            if (npcTurret == null)
                return null;
            
            CheckPickup(player, npcTurret);
            return _falseObject;
        }

        private object OnEntityGroundMissing(NPCAutoTurret turret)
        {
            if (turret.OwnerID == 0)
                return null;
            
            GroundMissing(turret);
            return _trueObject;
        }

        // private object OnBookmarkControl(ComputerStation station, BasePlayer player, string id, NPCAutoTurret turret)
        // {
        //     return _trueObject;
        // }

        private void OnSwitchToggle(ElectricSwitch entity, BasePlayer player)
        {
            var turret = entity.GetComponentInParent<NPCAutoTurret>();
            if (turret == null)
            {
                return;
            }

            var turretComponent = GetTurretComponent(turret);
            if (turretComponent == null)
                return;
            
            if (turret.authorizedPlayers.Any(x => x.userid == player.userID) == false)
            {
                player.ChatMessage("No permission");
                entity.SetSwitch(!entity.IsOn());
                return;
            }

            if (entity.GetCurrentEnergy() < turretComponent.ConfigDef.requiredPower)
            {
                player.ChatMessage("No power");
                entity.SetSwitch(!entity.IsOn());
                return;
            }

            var enabled = turret.IsOn();
            turret.RCEyes = turret.muzzlePos;
            turret.SetIsOnline(!enabled);
            turret.SetFlag(BaseEntity.Flags.Reserved8, !enabled);
            turret.SendNetworkUpdate();
        }
        
        #endregion
        
        #region Commands

        private void cmdGiveConsole(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin == false)
            {
                SendReply(arg, "You don't have access to that command!");
                return;
            }

            var args = arg.Args;
            if (args == null || args?.Length == 0)
            {
                SendReply(arg, "Usage: sentryturrets.give {steamID / Name} {amount} {profile key}");
                return;
            }
            
            var player = FindPlayer(args[0]);
            if (player == null)
                return;

            var amount = arg.GetInt(1, 1);
            
            var profileName = arg.GetString(2, "default");
            if (string.IsNullOrEmpty(profileName))
                return;

            var profile = _config.Profiles.Find(x => x.KeyName == profileName);
            if (profile == null)
                return;
            
            GiveItem(player, amount, profile.SkinId);
        }

        [ConsoleCommand("UI_SentryTurrets")]
        private void Console_UISentryTurrets(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            var interaction = PlayerInteraction.Get(player);
            var turret = interaction.Turret;
            
            DestroyUI_RadialBtn(player, interaction);
            
            switch (arg.GetString(0))
            {
                case "radial_close":
                {
                    break;
                }
                
                case "open":
                {
                    turret.PlayerOpenLoot(player);
                    break;
                }
                
                case "mode":
                {
                    turret.SetPeacekeepermode(!turret.PeacekeeperMode());
                    break;
                }
                
                case "rotate":
                {
                    var turretTransform = turret.transform;
                    turretTransform.rotation = Quaternion.LookRotation(-turretTransform.forward, turretTransform.up);
                    turret.SendNetworkUpdate();
                    break;
                }
                
                case "authorize":
                {
                    if (arg.Args.Length < 2)
                    {
                        DrawUI_AuthFriend(player);
                        break;
                    }

                    var input = string.Join(" ", arg.Args.Skip(1));
                    DrawUI_AuthPlayersList(player, input);
                    
                    break;
                }
                
                case "authorizeid":
                {
                    var userId = arg.GetUInt64(1);
                    if (userId.IsSteamId() == false)
                        break;

                    turret.AddSelfAuthorize(BasePlayer.FindAwakeOrSleepingByID(userId));  
                    break;
                }
                
                case "authorize_clear":
                {
                    turret.authorizedPlayers.Clear();
                    turret.authDirty = true;
                    turret.UpdateMaxAuthCapacity();
                    turret.SendNetworkUpdate();
                    break;
                }
                
                case "deauthorize_self":
                {
                    turret.authorizedPlayers.RemoveWhere(x => x.userid == player.userID);
                    turret.authDirty = true;
                    turret.UpdateMaxAuthCapacity();
                    turret.SendNetworkUpdate();
                    break;
                }
                
                // case "broadcast_id":
                // {
                //     CuiHelper.DestroyUi(player, InterfaceManager.UI_LayerWindow);
                //     
                //     if (arg.Args.Length < 2)
                //     {
                //         DrawUI_SetRemoteId(player, turret.rcIdentifier);
                //         break;
                //     }
                //
                //     var id = arg.GetString(1);
                //     if (string.IsNullOrEmpty(id))
                //         break;
                //
                //     Puts($"ID: {id} {RemoteControlEntity.allControllables.Contains(turret)}");
                //     turret.UpdateIdentifier(id);
                //     break;
                // }
            }
        }
        #endregion

        #region Core
        
        private void GroundMissing(NPCAutoTurret turret)
        {
            var turretComp = GetTurretComponent(turret);
            if (turretComp == null)
                return;
            
            var position = turret.transform.position;
            
            turret.Kill();
            
            Effect.server.Run("assets/bundled/prefabs/fx/item_break.prefab", position);
            
            if (turretComp.ConfigDef.itemOnGroundMissing)
            {
                _plugin.DropItem(turretComp.ConfigDef.SkinId, position, turret.healthFraction);
            }
        }
        
        private void CheckExistingTurrets()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is NPCAutoTurret turret == false)
                    continue;

                if (turret.OwnerID == 0)
                    continue;
                
                SetupTurret(turret);
            }
        }

        private void CheckPickup(BasePlayer player, NPCAutoTurret entity)
        {
            var component = GetTurretComponent(entity);
            if (component == null)
                return;
            
            var items = entity.inventory?.itemList.ToArray() ?? new Item[]{};
            
            foreach (var item in items)
            {
                player.GiveItem(item);
            }

            var healthFrac = entity.healthFraction;
            entity.Kill();
            GiveItem(player, 1, component.ConfigDef.SkinId, healthFrac);
        }

        private void CheckPlacement(Planner plan, GameObject go)
        {
            var entity = go.ToBaseEntity() as AutoTurret;
            if (entity == null)
                return;
            
            if (entity.skinID == 0)
                return;

            var turretProfile = _config.Profiles.Find(x => x.SkinId == entity.skinID);
            if (turretProfile == null)
                return;

            var player = plan.GetOwnerPlayer();
            if (player == null)
                return;

            var item = plan.GetItem();

            var transform = entity.transform;
            var position = transform.position;
            var rotation = transform.rotation;
            var owner = entity.OwnerID;
            NextTick(()=> { entity.Kill();});
            var turret = GameManager.server.CreateEntity(PrefabSentry, position, rotation)?.GetComponent<NPCAutoTurret>();
            if (turret == null)
            {
                GiveItem(player, 1, entity.skinID);
                return;
            }
            
            turret.rcIdentifier = turretProfile.KeyName; 
            turret.OwnerID = owner;
            turret.Spawn();
            turret.SetIsOnline(false);
            turret.SetPeacekeepermode(false);
            turret.AttachToBuilding(entity.buildingID);
            turret.AddSelfAuthorize(player);
            SetupTurret(turret);
            turret.SetHealth(turret.MaxHealth() * item.conditionNormalized);
        }

        private void SetupTurret(NPCAutoTurret turret)
        {
            var turretCfg = _config.Profiles.Find(x => x.KeyName == turret.rcIdentifier);
            if (turretCfg == null)
            {
                turret.Kill();
                return;
            }
            
            turret.sightRange = turretCfg.range;
            turret.targetTrigger.GetComponent<SphereCollider>().radius = turret.sightRange;
            turret.aimCone = turretCfg.aimCone;
            AuthorizeOthers(turret.OwnerID, turret);
            
            if (turret.socketTransform == null)
                turret.socketTransform = turret.muzzlePos;
            
            timer.Once(0.5f, () =>
            {
                CheckSwitch(turret); 
                var component = new GameObject().AddComponent<TurretComponent>();
                component.gameObject.transform.SetParent(turret.transform, false);
                component.ConfigDef = turretCfg;
                _turrets.Add(turret, component);
            });
            
            turret.SendNetworkUpdate();
        }

        private void CheckSwitch(BaseEntity turret)
        {
            var entity = turret.GetComponentInChildren<ElectricSwitch>();
            if (entity == null)
            {
                entity = GameManager.server.CreateEntity(PrefabSwitch, turret.transform.position, Quaternion.identity) as ElectricSwitch;
                if (entity == null)
                {
                    PrintError("Failed to spawn turret switch");   
                    return;
                }
                
                entity.Spawn();
                entity.SetParent(turret, true);
                entity.transform.localPosition = SwitchPosition;
                entity.transform.localRotation = Quaternion.Euler(SwitchRotation);
                entity.SendNetworkUpdate();
            }
            
            entity.InitializeHealth(100 * 1000, 100 * 1000);
            entity.pickup.enabled = false;
        }

        private TurretComponent GetTurretComponent(NPCAutoTurret turret)
        {
            return _turrets.GetValueOrDefault(turret);
        }

        private Item CreateItem(ulong skinId, int amount = 1)
        {
            var item = ItemManager.CreateByName(ItemName, Math.Max(amount, 1), skinId);
            if (item == null)
            {
                return null;
            }
            
            item.name = ItemDisplayName;
            return item;
        }
        
        private void DropItem(ulong skinId, Vector3 position, float healthFraction)
        {
            var item = CreateItem(skinId);
            if (item == null)
            {
                return;
            }

            item.condition = item.maxCondition * healthFraction;
            
            item.Drop(position, Vector3.down);  
        } 

        private void GiveItem(BasePlayer player, int amount, ulong skinId, float healthFraction = 1f)
        {
            var item = CreateItem(skinId, amount);
            if (item == null)
            {
                return;
            }
            
            item.condition = item.maxCondition * healthFraction;
            
            player.GiveItem(item);
            Puts($"Turret was gave successfully to {player.displayName}");
        }

        private BasePlayer FindPlayer(string nameOrID)
        {
            var targets = BasePlayer.activePlayerList.Where(x => x.UserIDString == nameOrID || x.displayName.ToLower().Contains(nameOrID.ToLower())).ToList();
            
            if (targets.Count == 0)
            {
                Puts("There are no players with that Name or steamID!");
                return null;
            }

            if (targets.Count > 1)
            {
                Puts($"There are many players with that Name:\n{targets.Select(x => x.displayName).ToSentence()}");
                return null;
            }

            return targets[0];
        }
        
        private void AuthorizeOthers(ulong userID, NPCAutoTurret entity)
        {
            var turretComponent = GetTurretComponent(entity);
            if (turretComponent == null)
                return;
            
            if (turretComponent.ConfigDef.authorizeOthers == true)
            {
                var team = RelationshipManager.ServerInstance.teams.FirstOrDefault(x => x.Value.members.Contains(userID)).Value;
                if (team?.members != null)
                {
                    foreach (var member in team.members)
                    {
                        entity.authorizedPlayers.Add(new PlayerNameID
                        {
                            userid = member,
                            username = "Player"
                        });
                    }
                }
            
                var friends = GetFriends(userID.ToString());
                if (friends != null)
                {
                    foreach (var friend in friends)
                    {
                        var friendID = (ulong) 0;
                        if (ulong.TryParse(friend, out friendID) == true)
                        {
                            entity.authorizedPlayers.Add(new PlayerNameID
                            {
                                userid = friendID,
                                username = "Player"
                            });
                        }
                    }
                }
            }

            if (turretComponent.ConfigDef.authorizeByTC == true)
            {
                var tc = entity.GetBuildingPrivilege();
                if (tc != null)
                {
                    foreach (var value in tc.authorizedPlayers)
                    {
                        entity.authorizedPlayers.Add(value);
                    }
                }
            }

            entity.authorizedPlayers = entity.authorizedPlayers.Distinct().ToHashSet();
        }
        
        private static string GetColor(string hex, float alpha = 1f)
        {
            if (hex.Length != 7) hex = "#FFFFFF";
            if (alpha < 0 || alpha > 1f) alpha = 1f;

            var color = ColorTranslator.FromHtml(hex);
            var r = Convert.ToInt16(color.R) / 255f;
            var g = Convert.ToInt16(color.G) / 255f;
            var b = Convert.ToInt16(color.B) / 255f;

            return $"{r} {g} {b} {alpha}";
        }
        #endregion
        
        #region Configuration 2.0.0

        private static ConfigData _config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty("Turrets profiles", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TurretProfile> Profiles = new List<TurretProfile>()
            {
                new TurretProfile() 
            };
        }

        protected override void LoadConfig() 
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<ConfigData>();   
                if (_config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                for (var i = 0; i < 3; i++)
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                }
                
                LoadDefaultConfig(); 
                return;
            }

            ValidateConfig();
            SaveConfig(); 
        }

        private void ValidateConfig()
        {
            if (Interface.Oxide.CallHook("OnConfigValidate") != null)
            {
                PrintWarning("Using default configuration...");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = new ConfigData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion
        
        #region Ddraw

        internal static class DDraw
        {
            public static void Line(BasePlayer player, float duration = 0.5f, Color? color = null,
                Vector3? from = null, Vector3? to = null)
            {
                player.SendConsoleCommand("ddraw.line", duration, Format(color), Format(from), Format(to));
            }

            public static void Arrow(BasePlayer player, float duration = 0.5f, Color? color = null,
                Vector3? from = null, Vector3? to = null, float headSize = 0f)
            {
                player.SendConsoleCommand("ddraw.arrow", duration, Format(color), Format(from), Format(to), headSize);
            }

            public static void Sphere(BasePlayer player, float duration = 0.5f, Color? color = null,
                Vector3? from = null, string text = "")
            {
                player.SendConsoleCommand("ddraw.sphere", duration, Format(color), Format(from), text);
            }

            public static void Text(BasePlayer player, float duration = 0.5f, Color? color = null,
                Vector3? from = null, string text = "")
            {
                player.SendConsoleCommand("ddraw.text", duration, Format(color), Format(from), text);
            }

            public static void Box(BasePlayer player, float duration = 0.5f, Color? color = null,
                Vector3? from = null, float size = 0.1f)
            {
                player.SendConsoleCommand("ddraw.box", duration, Format(color), Format(from), size);
            }

            private static string Format(Color? color) => ReferenceEquals(color, null)
                ? string.Empty
                : $"{color.Value.r},{color.Value.g},{color.Value.b},{color.Value.a}";

            private static string Format(Vector3? pos) => ReferenceEquals(pos, null)
                ? string.Empty
                : $"{pos.Value.x} {pos.Value.y} {pos.Value.z}";
        }

        #endregion
        
        #region Scripts
        
        private class TurretInput : MonoBehaviour
        {
            private NPCAutoTurret _turret;
            private readonly HashSet<PlayerInteraction> _players = new ();

            #region Events

            private void Start()
            {
                _turret = GetComponentInParent<NPCAutoTurret>();
                SetupBoxCollider();
            }

            private void OnTriggerStay(Collider other)
            {
                try
                {
                    var player = other.ToBaseEntity() as BasePlayer;
                    if (player == null)
                        return;

                    TriggerTick(player);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }
            
            private void OnTriggerExit(Collider other)
            {
                var player = other.ToBaseEntity() as BasePlayer;
                if (player == null)
                    return;

                _plugin.DestroyUI_InteractBtn(player);
                var interaction = PlayerInteraction.Get(player);
                if(interaction.Turret == _turret)
                    interaction.Clear();
                
                _players.Remove(interaction);
            }

            #endregion
            
            #region Utils

            private void SetupBoxCollider()
            {
                var collider = this.gameObject.AddComponent<BoxCollider>();
                var pos = collider.transform.position;
                collider.transform.position = pos.WithY(pos.y + 1f); 
                collider.size = new Vector3(4f, 4f, 4f);
                collider.gameObject.layer = (int)Rust.Layer.Reserved1;
                collider.isTrigger = true;
            } 
            
            private void TriggerTick(BasePlayer player)
            {
                var interaction = PlayerInteraction.Get(player);

                _players.Add(interaction);

                if (interaction.Turret != _turret)
                {
                    interaction.Clear();
                    interaction.Turret = _turret;
                }
                
                InputCheck(interaction);
                
                if (interaction.LastCheck < 0.5f)
                    return;

                interaction.LastCheck = 0f;

                if (CanSeeInteraction(player) == false)
                {
                    if (interaction.HasInteraction == false) 
                        return;
                    
                    _plugin.DestroyUI_InteractBtn(player);                
                    _plugin.DestroyUI_RadialBtn(player);
                    return;
                }
                
                if (interaction.IsInteracting()) 
                    return;
                
                _plugin.DrawUI_InteractionBtn(player, _turret.IsAuthed(player));
            }

            private void InputCheck(PlayerInteraction interaction)
            {
                var player = interaction.Player;
                
                if (interaction.HasInteraction == false)
                    return;
                
                var playerInput = player.serverInput;
                
                if(interaction.WasHoldingDown == false && playerInput.WasJustReleased(BUTTON.USE))
                {
                    if (AuthorizeAttempt(player))
                        return;
                    
                    if (interaction.HasRadial)
                    {
                        interaction.HasInteraction = false;
                        _plugin.DestroyUI_RadialBtn(player);
                        return;
                    }
                    
                    _turret.PlayerOpenLoot(player);
                }

                if (interaction.WasHoldingDown)
                {
                    CuiHelper.DestroyUi(player, InterfaceManager.UI_LayerBtn);
                }
                
                interaction.WasHoldingDown = false;

                
                if (playerInput.WasDown(BUTTON.USE) && playerInput.IsDown(BUTTON.USE))
                {
                    if (AuthorizeAttempt(player))
                        return;
                    
                    interaction.HoldDownTime += Time.deltaTime;
                    if (interaction.HoldDownTime < 0.2f) 
                        return;
                    
                    interaction.WasHoldingDown = true;
                    OnHoldDown(player, interaction);

                    return;
                }
                
                interaction.HoldDownTime = 0;
            }

            private bool AuthorizeAttempt(BasePlayer player)
            {
                if (_turret.IsAuthed(player) != false) 
                    return false;
                
                _turret.AddSelfAuthorize(player);
                _plugin.DrawUI_InteractionBtn(player, _turret.IsAuthed(player));
                return true;
            }

            private void OnHoldDown(BasePlayer player, PlayerInteraction interaction)
            {
                if (player.GetActiveItem()?.info.shortname == "hammer")
                {
                    _plugin.DrawUI_PickUpBar(player, interaction.HoldDownTime / 1f);
                    if (interaction.HoldDownTime > 1)
                    {
                        CuiHelper.DestroyUi(player, InterfaceManager.UI_LayerBtn);
                        _plugin.GiveItem(player, 1, _plugin.GetTurretComponent(_turret).ConfigDef.SkinId);
                        _turret.Kill();
                    }
                    
                    return;
                }
                
                if (interaction.HasRadial)
                    return;
            
                _plugin.DrawUI_RadialBtn(player);
                _plugin.DestroyUI_InteractBtn(player);
            }

            private bool CanSeeInteraction(BasePlayer player)
            {
                if (_turret.IsOn())
                    return false;
                
                if (Physics.Raycast(player.eyes.HeadRay(), out var hit, 4f) == false)
                    return false;

                if (hit.GetEntity() != _turret)
                    return false;

                return true;
            }

            #endregion
        }
        
        private class TurretComponent : MonoBehaviour
        {
            public NPCAutoTurret TurretOwner;
            private ElectricSwitch eSwitch;
            private TurretInput _turretInput;
            public TurretProfile ConfigDef;
            public TargetTrigger AirTrigger;
            
            private readonly int TurretVisibilityMask = LayerMask.GetMask("Construction", "World", "Terrain");

            #region Events

            private void Start()
            {
                TurretOwner = GetComponentInParent<NPCAutoTurret>();
                if (TurretOwner == null)
                {
                    Destroy(gameObject);
                    return;
                }
                
                eSwitch = TurretOwner.GetComponentInChildren<ElectricSwitch>(); 
                
                TurretOwner.GetOrAddComponent<GroundWatch>();
                TurretOwner.GetOrAddComponent<DestroyOnGroundMissing>();
                
                InvokeRepeating(nameof(DoChecks), 2f, 2f);

                _turretInput = new GameObject().AddComponent<TurretInput>();
                _turretInput.transform.SetParent(transform, false);
                
                TurretOwner.SetFlag(BaseEntity.Flags.Busy, true);

                SetupProtection();
                SetupContainer();
                SetupAirTriggerCollider(); 
            }

            #endregion


            #region Ticks

            private void DoChecks()
            {
                try
                {
                    if (_plugin == null || _plugin.IsLoaded == false)
                    {
                        Kill();
                        return;
                    }
                
                    CheckPower();
                    CheckTarget();
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }
            
            #endregion

            #region Utils

            private void CheckTarget()
            {
                if (ConfigDef.airRange == 0)
                    return;

                if (TurretOwner.IsOn() == false)
                    return;

                if (TurretOwner.HasTarget() && IsAirTarget(TurretOwner.target) && IsValidAirTarget(TurretOwner.target) == false)
                {
                    TurretOwner.SetTarget(null);
                }
                
                if ((AirTrigger.entityContents?.Count ?? 0) == 0) 
                    return;  
                
                foreach (var airTriggerEntityContent in AirTrigger.entityContents)
                {
                    OnEntityEnterTrigger(airTriggerEntityContent);
                }
            }
            
            private void CheckPower()
            {
                if (HasPower())
                {
                    return;
                }
                
                TurretOwner.SetIsOnline(false);
            }

            private bool HasPower()
            {
                return eSwitch != null && eSwitch.GetCurrentEnergy() >= ConfigDef.requiredPower;
            }
            
            private void SetupAirTriggerCollider()
            {
                if (ConfigDef.airRange == 0)
                    return;
                
                var collider = gameObject.AddComponent<SphereCollider>();
                var turretCollider = TurretOwner.targetTrigger.GetComponent<SphereCollider>();

                collider.excludeLayers = turretCollider.excludeLayers;
                collider.isTrigger = true;
                collider.radius = ConfigDef.airRange;
                collider.gameObject.layer = TurretOwner.targetTrigger.gameObject.layer;
                
                AirTrigger = collider.gameObject.AddComponent<TargetTrigger>();
                AirTrigger.interestLayers = LayerMask.GetMask("Vehicle World");
                AirTrigger.OnEntityEnterTrigger += OnEntityEnterTrigger;
            }

            private void OnEntityEnterTrigger(BaseNetworkable obj)
            {
                if (TurretOwner.HasTarget() || TurretOwner.IsOn() == false)
                    return;

                if (obj is BaseCombatEntity combatEntity == false)
                    return;

                if (IsValidAirTarget(combatEntity) == false)
                    return;
                
                TurretOwner.SetTarget(combatEntity); 
            }

            public bool IsAirTarget(BaseCombatEntity target)
            {
                switch (target)
                {
                    case PatrolHelicopter:
                        return true;
                    
                    case SamSite.ISamSiteTarget samSiteTarget:
                        return true;
                }

                return false;
            }
            
            private bool IsValidAirTarget(BaseCombatEntity target)
            {
                switch (target)
                {
                    case PatrolHelicopter:
                        return true;
                    
                    case SamSite.ISamSiteTarget samSiteTarget:
                        if (samSiteTarget.IsValidSAMTarget(false) == false)
                            break;

                        if (IsVisible(target, TurretOwner.muzzleLeft.position, target.transform.position, ConfigDef.airRange)) 
                            break;

                        return true;
                }

                return false;
            }
            
            private bool IsVisible(BaseEntity entity, Vector3 position, Vector3 target, float maxDistance = float.PositiveInfinity)
            {
                var targetDirection = target - position;
                var magnitude = targetDirection.magnitude;
                if (magnitude < Mathf.Epsilon)
                    return true;
                
                var direction = targetDirection / magnitude;
                var directionMagnitude = direction * Mathf.Min(magnitude, 0.01f);
                maxDistance = Mathf.Min(maxDistance, magnitude + 0.2f);
                return entity.IsVisible(new Ray(position + directionMagnitude, direction), TurretVisibilityMask, maxDistance);
            }
            
            public void Kill()
            {
                Destroy(gameObject);
            }

            private void SetupContainer()
            {
                TurretOwner.inventory.entityOwner = TurretOwner;
                TurretOwner.inventory.capacity = 7;
                TurretOwner.inventory.canAcceptItem = CanAcceptItem;
                TurretOwner.inventory.onItemAddedRemoved = TurretOwner.OnItemAddedOrRemoved;
                TurretOwner.dropsLoot = false;
                TurretOwner.lootPanelName = SentryLootPanel;
            }

            private bool CanAcceptItem(Item item, int targetSlot)
            {
                if (ConfigDef.airRange > 0 && targetSlot > 0 && item.info.shortname == "ammo.rocket.sam")
                {
                    return true;
                }
                
                if(ConfigDef.needsWeapon)
                    return TurretOwner.CanAcceptItem(item, targetSlot);
                
                if (targetSlot > 0 && TurretOwner.GetAttachedWeapon() == null)
                {
                    if (item.info.shortname == "ammo.rifle")
                        return true;

                    return false;
                }
                
                return TurretOwner.CanAcceptItem(item, targetSlot);
            }
            
            
            private void SetupProtection()
            {
                var health = ConfigDef.health;
                TurretOwner._maxHealth = health;
                TurretOwner.health = health;
            
                if (ConfigDef.getDamage == true)
                {
                    TurretOwner.baseProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
                    TurretOwner.baseProtection.amounts = new float[]
                    {
                        1,1,1,1,1,0.8f,1,1,1,0.9f,0.5f,
                        0.5f,1,1,0,0.5f,0,1,1,0,1,0.9f
                    };
                }
            }

            private RealTimeSince _lastAirFire;
            
            public void FireProjectile(Vector3 direction, float speedMultiplier)
            {
                if (_lastAirFire < ConfigDef.airFireRate)
                    return;

                _lastAirFire = 0;
                
                var entity = GameManager.server.CreateEntity(PrefabRocketSam, TurretOwner.muzzleLeft.position + TurretOwner.aimDir * 2.5f, Quaternion.LookRotation(direction, Vector3.up));
                if (entity == null)
                    return;
                
                entity.creatorEntity = TurretOwner;
                var component = entity.GetComponent<ServerProjectile>();
                if (component)
                    component.InitializeVelocity(TurretOwner.GetInheritedProjectileVelocity(direction) + direction * component.speed * speedMultiplier);
                
                entity.Spawn();
            }

            public void TargetTick()
            {
                if (Time.realtimeSinceStartup >= TurretOwner.nextVisCheck)
                {
                    TurretOwner.nextVisCheck = Time.realtimeSinceStartup + UnityEngine.Random.Range(0.2f, 0.3f);
                    TurretOwner.targetVisible = TurretOwner.ObjectVisible(TurretOwner.target);
                    if (TurretOwner.targetVisible)
                        TurretOwner.lastTargetSeenTime = UnityEngine.Time.realtimeSinceStartup;
                }

                TurretOwner.EnsureReloaded();
                var attachedWeapon = TurretOwner.GetAttachedWeapon();

                if (Time.time >= TurretOwner.nextShotTime &&
                    TurretOwner.targetVisible &&
                    Mathf.Abs(TurretOwner.AngleToTarget(TurretOwner.target, TurretOwner.currentAmmoGravity != 0.0)) < TurretOwner.GetMaxAngleForEngagement())
                {
                    if (attachedWeapon)
                    {
                        if (attachedWeapon.primaryMagazine.contents > 0)
                        {
                            TurretOwner.FireAttachedGun(TurretOwner.AimOffset(TurretOwner.target), TurretOwner.aimCone, target: TurretOwner.PeacekeeperMode() ? TurretOwner.target : (BaseCombatEntity)null);
                            float delay = attachedWeapon.isSemiAuto ? attachedWeapon.repeatDelay * 1.5f : attachedWeapon.repeatDelay;
                            TurretOwner.nextShotTime = UnityEngine.Time.time + attachedWeapon.ScaleRepeatDelay(delay);
                        }
                        else
                            TurretOwner.nextShotTime = UnityEngine.Time.time + 5f;
                    }
                    else if (TurretOwner.HasFallbackWeapon())
                    {
                        TurretOwner.FireGun(TurretOwner.AimOffset(TurretOwner.target), TurretOwner.aimCone, target: TurretOwner.target);
                        TurretOwner.nextShotTime = UnityEngine.Time.time + 0.115f;
                    }
                    else if (TurretOwner.HasGenericFireable())
                    {
                        TurretOwner.AttachedWeapon.ServerUse();
                        TurretOwner.nextShotTime = UnityEngine.Time.time + 0.115f;
                    }
                    else
                        TurretOwner.nextShotTime = UnityEngine.Time.time + 1f;
                }

                if (TurretOwner.target != null && 
                    TurretOwner.target.IsAlive() && 
                    Time.realtimeSinceStartup - TurretOwner.lastTargetSeenTime <= 3.0 &&
                    Vector3.Distance(this.transform.position, TurretOwner.target.transform.position) <= SightRangeForTarget(TurretOwner.target) && 
                    (TurretOwner.PeacekeeperMode() == false || TurretOwner.IsEntityHostile(TurretOwner.target)))
                    return;

                TurretOwner.SetTarget(null);
            }

            private float SightRangeForTarget(BaseCombatEntity target)
            {
                if (target is SamSite.ISamSiteTarget || target is PatrolHelicopter)
                    return ConfigDef.airRange;

                return ConfigDef.range;
            }
            
            public int TakeItem(string shortName, int amount)
            {
                if (TurretOwner.inventory.itemList.Count == 0)
                    return 0;

                for (var i = TurretOwner.inventory.itemList.Count - 1; i >= 0; i--)
                {
                    var item = TurretOwner.inventory.itemList[i];
                    if (item.info.shortname != shortName)
                        continue;
                        
                    item.amount -= amount;
                    if (item.amount <= 0)
                    {
                        item.amount = 0;
                        item.Remove();
                    }
                    else
                        item.MarkDirty();
                }
                    
                return amount;
            }
            
            #endregion
        }

        #endregion
        
        #region Friends Support

        [PluginReference] private Plugin Friends, RustIOFriendListAPI;
        
        private string[] GetFriends(string playerID)
        {
            var flag1 = Friends?.Call<string[]>("GetFriends", playerID) ?? new string[]{};
            var flag2 = RustIOFriendListAPI?.Call<string[]>("GetFriends", playerID) ?? new string[]{};
            return flag1.Length > 0 ? flag1 : flag2;
        }

        #endregion
        
        #region Interface v0.0.1

        private readonly string _whiteColor = "1 1 1 1";
        private readonly string _blueColor = GetColor("#439fe0");
        private readonly string _redColor = GetColor("#b13726");
        private readonly string _greenColor = GetColor("#8fa46b");
        
        private void DrawUI_PickUpBar(BasePlayer player, float progressFraction)
        {
            var gui = InterfaceManager.GetInterface("PickUpBlock");
            
            CuiHelper.DestroyUi(player, InterfaceManager.UI_LayerBtn);
            CuiHelper.AddUi(player, gui.Replace("%p%", Math.Clamp(progressFraction, 0, 1).ToString()));
        }
        
        private void DrawUI_SetRemoteId(BasePlayer player, string id = "")
        {
            var gui = InterfaceManager.GetInterface("RemoteIdBlock");

            gui = gui.Replace("%id%", id);
            
            CuiHelper.DestroyUi(player, InterfaceManager.UI_LayerWindow);
            CuiHelper.AddUi(player, gui);
        }
        
        private void DrawUI_AuthFriend(BasePlayer player)
        {
            var gui = InterfaceManager.GetInterface("AuthFriendBlock");
            
            CuiHelper.DestroyUi(player, InterfaceManager.UI_LayerWindow);
            CuiHelper.AddUi(player, gui);

            DrawUI_AuthPlayersList(player);
        }

        private void DrawUI_AuthPlayersList(BasePlayer player, string input = null)
        {
            var interaction = PlayerInteraction.Get(player);
            if (interaction == null || interaction.Turret == null)
                return;
            
            RelationshipManager.ServerInstance.relationships.TryGetValue(player.userID, out var relationships);
            List<BasePlayer> playersList = null;
            if (input != null)
            {
                playersList = BasePlayer.allPlayerList.Where(x => x.displayName.Contains(input, CompareOptions.IgnoreCase) && interaction.Turret.IsAuthed(player) == false).ToList();
            }
            else if(relationships != null)
            {
                playersList = relationships.relations.Values.Select(x => BasePlayer.FindAwakeOrSleepingByID(x.player))
                    .Where(x => x != null && interaction.Turret.IsAuthed(player) == false).ToList();
            }

            if (playersList == null)
                return;
            
            var gui = InterfaceManager.GetInterface("AuthFriendItemParent");
            var itemJson = InterfaceManager.GetInterface("AuthFriendItem");
            
            CuiHelper.DestroyUi(player, InterfaceManager.UI_LayerItemsParent);
            CuiHelper.AddUi(player, gui);

            float width = 232, height = 65, marginX = width + 10, marginY = height + 10;
            
            for (int i = 0, r = 0, d = 0; i < 30 && i < playersList.Count; i++)
            {
                var targetPlayer = playersList[i];
                
                gui = itemJson;
                
                gui = gui.Replace("%on%", $"{0 + marginX * r} {-height - marginY * d}");
                gui = gui.Replace("%ox%", $"{width + marginX * r} {0 - marginY * d}");
                
                gui = gui.Replace("%id%", targetPlayer.UserIDString);
                gui = gui.Replace("%img%", GetImage(targetPlayer.UserIDString));
                gui = gui.Replace("%t%", targetPlayer.displayName);

                gui = gui.Replace("%tc%", GetRelationsColor(relationships, targetPlayer));
                    
                CuiHelper.AddUi(player, gui);

                if (++r >= 5)
                {
                    r = 0;
                    d++;
                }
            }
        }

        private string GetRelationsColor(RelationshipManager.PlayerRelationships relationships, BasePlayer target)
        {
            if (relationships == null)
                return _whiteColor;

            var relations = relationships.GetRelations(target.userID);
            if(relations == null)
                return _whiteColor;
            
            switch (relations.type)
            {
                case RelationshipManager.RelationshipType.Acquaintance:
                {
                    return _blueColor;
                }
                case RelationshipManager.RelationshipType.Enemy:
                {
                    return _redColor;
                }
                case RelationshipManager.RelationshipType.Friend:
                {
                    return _greenColor;
                }
            }
            
            return _whiteColor;
        }
        
        private void DrawUI_InteractionBtn(BasePlayer player, bool authed, PlayerInteraction interaction = null)
        {
            interaction ??= PlayerInteraction.Get(player);
            var gui = InterfaceManager.GetInterface(authed ? "InteractBtn" : "AuthBtn");
            
            CuiHelper.DestroyUi(player, InterfaceManager.UI_LayerBtn);
            CuiHelper.AddUi(player, gui);

            interaction.HasInteraction = true;
            
            if (authed == false)
                return;

            timer.Once(1f, () =>
            {
                CuiHelper.DestroyUi(player, $"{InterfaceManager.UI_LayerBtn}.Extra");
                CuiHelper.AddUi(player, InterfaceManager.GetInterface("InteractBtnExtra"));
            });
        }

        private void DestroyUI_InteractBtn(BasePlayer player, PlayerInteraction interaction = null)
        {
            interaction ??= PlayerInteraction.Get(player);

            interaction.HasInteraction = false;
            CuiHelper.DestroyUi(player, InterfaceManager.UI_LayerBtn);
        }
        
        private void DrawUI_RadialBtn(BasePlayer player, PlayerInteraction interaction = null)
        {
            interaction ??= PlayerInteraction.Get(player);
            var gui = InterfaceManager.GetInterface("RadialBtns");
            var btnJson = InterfaceManager.GetInterface("RadialItem");
            
            DestroyUI_RadialBtn(player);
            CuiHelper.AddUi(player, gui);
            
            interaction.HasRadial = true;

            float radius = 120;
            float rotationAngle = 90f;  // Rotation angle in degrees
            float rotationRadians = rotationAngle * Mathf.Deg2Rad;
            
            for (int i = 0; i < _turretRadialButtons.Count; i++)
            {
                var btn = _turretRadialButtons[i];
                var btnInfo = btn.GetState(interaction.Turret, 0);
                gui = btnJson;
                float angle = 6.28f / -_turretRadialButtons.Count * i + rotationRadians;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                
                var str = $"{x} {y}";
                gui = gui.Replace("%an%", str);
                gui = gui.Replace("%ax%", str);
                gui = gui.Replace("%i%", btnInfo.SpriteIcon);
                gui = gui.Replace("%t%", btnInfo.Text);
                gui = gui.Replace("%c%", btnInfo.Command);
                 
                CuiHelper.AddUi(player, gui);
            }
        }
        
        private void DestroyUI_RadialBtn(BasePlayer player, PlayerInteraction interaction = null)
        {
            interaction ??= PlayerInteraction.Get(player);

            interaction.HasRadial = false;
            interaction.HasInteraction = false;
            CuiHelper.DestroyUi(player, InterfaceManager.UI_LayerRadial);
        }
        
        private class InterfaceManager
        {
            #region Vars

            public static void Init() => Instance = new InterfaceManager();
            
            public static InterfaceManager Instance;

            public const string UI_Layer = "UI_TurretInteraction";
            public const string UI_LayerBtn = "UI_TurretInteraction.Btn";
            public const string UI_LayerRadial = "UI_TurretInteraction.Radial";
            public const string UI_LayerWindow = "UI_TurretInteraction.Window";
            public const string UI_LayerItemsParent = $"{UI_LayerWindow}.ItemsParent";
            public const string UI_LayerProgress = "UI_TurretInteraction.Progress";
            public Dictionary<string, string> Interfaces;

            #endregion

            #region Main

            public InterfaceManager()
            {
                Instance = this;
                Interfaces = new Dictionary<string, string>();
                BuildInterface();
            }

            public static void AddInterface(string name, string json)
            {
                if (Instance.Interfaces.ContainsKey(name))
                {
                    _plugin.PrintError($"Error! Tried to add existing cui elements! -> {name}");
                    return;
                }
                
                Instance.Interfaces.Add(name, json);
            }

            public static string GetInterface(string name)
            {
                string json = string.Empty;
                if (Instance.Interfaces.TryGetValue(name, out json) == false)
                {
                    _plugin.PrintWarning($"Warning! UI elements not found by name! -> {name}");
                }

                return json;
            }

            public static void DestroyAll()
            {
                for (var i = 0; i < BasePlayer.activePlayerList.Count; i++)
                {
                    var player = BasePlayer.activePlayerList[i];
                    CuiHelper.DestroyUi(player, UI_Layer);
                }
            }

            #endregion
            
            private void BuildInterface()
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiElement
                        {
                            Parent = "Hud",
                            Name = UI_LayerBtn,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = "0 0 0 0"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.5 0.5", 
                                    AnchorMax = "0.5 0.5",
                                    OffsetMin = "-150 -50",
                                    OffsetMax = "150 60",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerBtn,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Sprite = "assets/icons/open.png", Color = "1 1 1 1", FadeIn = 0.1f
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"0.5 1",
                                    AnchorMax = $"0.5 1",
                                    OffsetMin = "-13 -26",
                                    OffsetMax = "13 0",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerBtn,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Sprite = "assets/icons/circle_closed.png", Color = "1 1 1 1", FadeIn = 0.1f
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"0.5 0.5",
                                    AnchorMax = $"0.5 0.5",
                                    OffsetMin = "-4.5 -9",
                                    OffsetMax = "4.5 0",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerBtn,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = $"OPEN", Align = TextAnchor.LowerCenter, Color = "1 1 1 1", FontSize = 13, 
                                    FadeIn = 0.1f, Font = "robotocondensed-bold.ttf"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"0 1",
                                    AnchorMax = $"1 1",
                                    OffsetMin = "0 -40.7",
                                    OffsetMax = "0 -20",
                                }
                            }
                        }
                    },
                };
                AddInterface("InteractBtn", container.ToJson());
                
                container = new CuiElementContainer()
                {
                    {
                        new CuiElement
                        {
                            Parent = "Hud",
                            Name = UI_LayerBtn,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = "0 0 0 0"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.5 0.5", 
                                    AnchorMax = "0.5 0.5",
                                    OffsetMin = "-150 -50",
                                    OffsetMax = "150 60",
                                },
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerBtn,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Sprite = "assets/icons/authorize.png", Color = "1 1 1 1", FadeIn = 0.1f
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"0.5 1",
                                    AnchorMax = $"0.5 1",
                                    OffsetMin = "-13 -26",
                                    OffsetMax = "13 0",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerBtn,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Sprite = "assets/icons/circle_closed.png", Color = "1 1 1 1", FadeIn = 0.1f
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"0.5 0.5",
                                    AnchorMax = $"0.5 0.5",
                                    OffsetMin = "-4.5 -9",
                                    OffsetMax = "4.5 0",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerBtn,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = $"AUTHORIZE", Align = TextAnchor.LowerCenter, Color = "1 1 1 1", FontSize = 13, 
                                    FadeIn = 0.1f, Font = "robotocondensed-bold.ttf"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"0 1",
                                    AnchorMax = $"1 1",
                                    OffsetMin = "0 -40.7",
                                    OffsetMax = "0 -20",
                                }
                            }
                        }
                    },
                };
                AddInterface("AuthBtn", container.ToJson());
                
                container = new CuiElementContainer()
                {
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerBtn,
                            Name = $"{UI_LayerBtn}.Extra",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = $"MORE OPTIONS ARE AVAILABLE\nHOLD DOWN [USE] TO OPEN MENU", Align = TextAnchor.LowerCenter, Color = "1 1 1 1", FontSize = 12, 
                                    FadeIn = 0.7f, Font = "robotocondensed-regular.ttf"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"0 0",
                                    AnchorMax = $"1 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = "0 0",
                                }
                            }
                        }
                    },
                };
                AddInterface("InteractBtnExtra", container.ToJson());
                
                container = new CuiElementContainer()
                {
                    {
                        new CuiElement
                        {
                            Parent = "Hud",
                            Name = UI_LayerRadial,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = "0 0 0 0"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.5 0.5", 
                                    AnchorMax = "0.5 0.5",
                                },
                                new CuiNeedsCursorComponent(),
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerRadial,
                            Name = $"{UI_LayerRadial}.Button",
                            Components =
                            {
                                new CuiButtonComponent
                                {
                                    Color = "0 0 0 0", Command = "UI_SentryTurrets radial_close"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", 
                                    AnchorMax = "1 1",
                                    OffsetMin = "-45 -45",
                                    OffsetMax = "45 45",
                                },
                                new CuiNeedsCursorComponent(),
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = $"{UI_LayerRadial}.Button",
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Png = _plugin.GetImage(CloseImageIcon)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", 
                                    AnchorMax = "1 1"
                                },
                                new CuiNeedsCursorComponent(),
                            }
                        }
                    },
                };
                AddInterface("RadialBtns", container.ToJson());
                
                container = new CuiElementContainer()
                {
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerRadial,
                            Name = $"{UI_LayerBtn}.Btn",
                            Components =
                            {
                                new CuiButtonComponent
                                {
                                    Color = "1 0 0 0", Command = "%c%"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "%an%", 
                                    AnchorMax = "%ax%",
                                    OffsetMin = "-50 -50",
                                    OffsetMax = "50 50",
                                },
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = $"{UI_LayerBtn}.Btn",
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Png = _plugin.GetImage(MenuImageIcon), Color = "1 1 1 1",
                                    FadeIn = 0.2f, 
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", 
                                    AnchorMax = "1 1",
                                },
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = $"{UI_LayerBtn}.Btn",
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Sprite = "%i%",
                                    FadeIn = 0.2f, 
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.5 0.5", 
                                    AnchorMax = "0.5 0.5",
                                    OffsetMin = "-15 -5",
                                    OffsetMax = "15 25"
                                },
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = $"{UI_LayerBtn}.Btn",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = $"%t%", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 11, 
                                    Font = "robotocondensed-regular.ttf",
                                    FadeIn = 0.2f, 
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"0 0",
                                    AnchorMax = $"1 1",
                                    OffsetMin = "0 15",
                                    OffsetMax = "0 -50",
                                }
                            }
                        }
                    },
                };
                AddInterface("RadialItem", container.ToJson());
                
                container = new CuiElementContainer()
                {
                    {
                        new CuiElement
                        {
                            Parent = "Overlay",
                            Name = UI_LayerWindow,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = "0.4 0.4 0.4 0.7"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", 
                                    AnchorMax = "1 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = "0 0",
                                },
                                new CuiNeedsCursorComponent(),
                                new CuiNeedsKeyboardComponent()
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerWindow,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = "0 0 0 0.3", Material = "assets/content/ui/uibackgroundblur.mat"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", 
                                    AnchorMax = "1 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = "0 0",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerWindow,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "AUTHORIZE A FRIEND", Align = TextAnchor.UpperLeft, Color = "1 1 1 0.8", FontSize = 30
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"0 0",
                                    AnchorMax = $"1 1",
                                    OffsetMin = "50 0",
                                    OffsetMax = "0 -40",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerWindow,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "CLOSE", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.9", FontSize = 28
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"1 1",
                                    AnchorMax = $"1 1",
                                    OffsetMin = "-200 -75",
                                    OffsetMax = "-50 -35",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerWindow, 
                            Components =
                            {
                                new CuiButtonComponent
                                {
                                    Color = "0 0 0 0", Close = UI_LayerWindow
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"0 0",
                                    AnchorMax = $"1 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = "0 0",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerWindow,
                            Name = $"{UI_LayerWindow}.Search",
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = "0.8 0.8 0.8 0.7", Material = "assets/content/ui/uibackgroundblur.mat"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.5 1", 
                                    AnchorMax = "0.5 1",
                                    OffsetMin = "-200 -150",
                                    OffsetMax = "100 -110",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = $"{UI_LayerWindow}.Search",
                            Components =
                            {
                                new CuiInputFieldComponent()
                                {
                                    Text = "ENTER PLAYER NAME...", Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.8", FontSize = 22,
                                    Command = "UI_SentryTurrets authorize"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", 
                                    AnchorMax = "1 1",
                                    OffsetMin = "15 0",
                                    OffsetMax = "0 0",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = $"{UI_LayerWindow}.Search",
                            Name = $"{UI_LayerWindow}.SearchBtn",
                            Components =
                            {
                                new CuiButtonComponent()
                                {
                                    Color = GetColor("#8fa46b"), Material = "assets/content/ui/uibackgroundblur.mat"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "1 0", 
                                    AnchorMax = "1 1",
                                    OffsetMin = "10 0",
                                    OffsetMax = "150 0",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = $"{UI_LayerWindow}.SearchBtn",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "SEARCH", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.9", FontSize = 18
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", 
                                    AnchorMax = "1 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = "0 0",
                                }
                            }
                        }
                    },
                };
                AddInterface("AuthFriendBlock", container.ToJson());
                
                container = new CuiElementContainer()
                {
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerWindow,
                            Name = UI_LayerItemsParent,
                            Components =
                            {
                                new CuiImageComponent 
                                {
                                    Color = "1 0 0 0"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", 
                                    AnchorMax = "1 1",
                                    OffsetMin = "40 100",
                                    OffsetMax = "-40 -170",
                                }
                            }
                        }
                    },
                };
                AddInterface("AuthFriendItemParent", container.ToJson());
                
                container = new CuiElementContainer()
                {
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerItemsParent,
                            Name = $"{UI_LayerWindow}.Item",
                            Components =
                            {
                                new CuiButtonComponent
                                {
                                    Color = "0.8 0.8 0.8 0.4",
                                    Command = "UI_SentryTurrets authorizeid %id%"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1", 
                                    AnchorMax = "0 1",
                                    OffsetMin = "%on%",
                                    OffsetMax = "%ox%",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent =  $"{UI_LayerWindow}.Item",
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Png = "%img%"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", 
                                    AnchorMax = "0 1",
                                    OffsetMin = "5 5",
                                    OffsetMax = "60 -5",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent =  $"{UI_LayerWindow}.Item",
                            Components =
                            {
                                // new CuiImageComponent
                                // {
                                //     Color = "1 0 0 0.6"
                                // },
                                new CuiTextComponent
                                {
                                    Text = "%t%", Align = TextAnchor.MiddleLeft, Color = "%tc%", FontSize = 18
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1", 
                                    AnchorMax = "1 1",
                                    OffsetMin = "65 -35",
                                    OffsetMax = "-5 -5",
                                }
                            }
                        }
                    },
                };
                AddInterface("AuthFriendItem", container.ToJson());
                
                container = new CuiElementContainer()
                {
                    {
                        new CuiElement
                        {
                            Parent = "Overlay",
                            Name = UI_LayerWindow,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = "0.4 0.4 0.4 0.7"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", 
                                    AnchorMax = "1 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = "0 0",
                                },
                                new CuiNeedsCursorComponent(),
                                new CuiNeedsKeyboardComponent()
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerWindow,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = "0 0 0 0.3", Material = "assets/content/ui/uibackgroundblur.mat"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", 
                                    AnchorMax = "1 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = "0 0",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerWindow,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "SET IDENTIFIER", Align = TextAnchor.MiddleCenter, Color = GetColor("#f7ebe1"), FontSize = 32 
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"0 0",
                                    AnchorMax = $"1 1",
                                    OffsetMin = "0 130",
                                    OffsetMax = "0 0",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerWindow,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "Anyone can access any device if they have the identifier, use a unique name!", 
                                    Align = TextAnchor.MiddleCenter, Color = GetColor("#f7ebe1"), FontSize = 13 
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"0 0",
                                    AnchorMax = $"1 1",
                                    OffsetMin = "0 60",
                                    OffsetMax = "0 0",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerWindow, 
                            Components =
                            {
                                new CuiButtonComponent
                                {
                                    Color = "0 0 0 0", Close = UI_LayerWindow
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"0 0",
                                    AnchorMax = $"1 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = "0 0",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerWindow,
                            Name = $"{UI_LayerWindow}.Search",
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = GetColor("#f7ebe1", 0.2f), Material = "assets/content/ui/uibackgroundblur.mat"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.5 0.5", 
                                    AnchorMax = "0.5 0.5",
                                    OffsetMin = "-103 -12",
                                    OffsetMax = "103 12",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = $"{UI_LayerWindow}.Search",
                            Components =
                            {
                                new CuiInputFieldComponent()
                                {
                                    Text = "%id%", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8", FontSize = 14,
                                    Command = "UI_SentryTurrets broadcast_id"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", 
                                    AnchorMax = "1 1",
                                    OffsetMin = "5 0",
                                    OffsetMax = "-5 0",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = $"{UI_LayerWindow}.Search",
                            Name = $"{UI_LayerWindow}.SearchBtn",
                            Components =
                            {
                                new CuiButtonComponent()
                                {
                                    Color = GetColor("#8fa46b"), 
                                    Material = "assets/content/ui/uibackgroundblur.mat"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.5 0", 
                                    AnchorMax = "1 0",
                                    OffsetMin = "2 -26",
                                    OffsetMax = "0 -3",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = $"{UI_LayerWindow}.SearchBtn",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "SET", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7", FontSize = 12
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", 
                                    AnchorMax = "1 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = "0 0",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = $"{UI_LayerWindow}.Search",
                            Name = $"{UI_LayerWindow}.CancelBtn",
                            Components =
                            {
                                new CuiButtonComponent()
                                {
                                    Color = GetColor("#b47265"), 
                                    Material = "assets/content/ui/uibackgroundblur.mat", Close = UI_LayerWindow
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", 
                                    AnchorMax = "0.5 0",
                                    OffsetMin = "0 -26",
                                    OffsetMax = "-2 -3",
                                }
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = $"{UI_LayerWindow}.CancelBtn",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "CANCEL", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7", FontSize = 12
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", 
                                    AnchorMax = "1 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = "0 0",
                                }
                            }
                        }
                    },
                };
                AddInterface("RemoteIdBlock", container.ToJson());
                
                container = new CuiElementContainer()
                {
                    {
                        new CuiElement
                        {
                            Parent = "Overlay",
                            Name = UI_LayerBtn, 
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = "1 1 1 0.6"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.5 0.5", 
                                    AnchorMax = "0.5 0.5",
                                    OffsetMin = "-100 22",
                                    OffsetMax = "100 30",
                                },
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerBtn, 
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = "1 1 1 1"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", 
                                    AnchorMax = "%p% 1",
                                },
                            }
                        }
                    },
                    {
                        new CuiElement
                        {
                            Parent = UI_LayerBtn,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "Pickup", Align = TextAnchor.LowerLeft, Color = "1 1 1 1", FontSize = 14 
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"0 1",
                                    AnchorMax = $"1 1",
                                    OffsetMin = "10 0",
                                    OffsetMax = "0 20",
                                }
                            }
                        }
                    },
                };
                AddInterface("PickUpBlock", container.ToJson());
            }
        }
        #endregion
        
        #region PluginLoading v0.0.4

        private List<string> _imagesLoading = new List<string>();

        private const string MenuImageIcon = "https://i.imgur.com/0txI7gN.png";
        private const string CloseImageIcon = "https://i.imgur.com/uakIT0e.png";
        
        private void StartPluginLoad()
        {
            if (ImageLibrary != null)
            {
                //Load your images here
                AddImage(MenuImageIcon); 
                AddImage(CloseImageIcon); 
                CheckStatus();
            }
            else
            {
                PrintError($"ImageLibrary not found! Please, check your plugins list.");
            }
        }

        private void CheckStatus()
        {
            int loadedImages = 0;
            foreach (var value in _imagesLoading)
            {
                if (HasImage(value) == false)
                    continue;

                loadedImages++;
            }
            
            if (loadedImages < _imagesLoading.Count - 1 && (bool)ImageLibrary.Call("IsReady") == false)
            {
                PrintWarning($"Plugin is not ready! Loaded: {loadedImages}/{_imagesLoading.Count} images.");
                timer.Once(10f, CheckStatus);
            }
            else
            {
                FullLoad();
                Puts("Plugin images loaded!");
            }
        }

        private void FullLoad()
        {
            InterfaceManager.Init(); 
        }
        
        #region ImageLibrary
        
        [PluginReference] private Plugin ImageLibrary;
        
        private string GetImage(string name)
        {
            string ID = (string)ImageLibrary?.Call("GetImage", name);
            if (ID == "")
                ID = (string)ImageLibrary?.Call("GetImage", name) ?? ID;
        
            return ID;
        }
        
        private void AddImage(string name)
        {
            if (HasImage(name))
                return;
            
            ImageLibrary?.Call("AddImage", name, name);
            _imagesLoading.Add(name);
        }

        private bool HasImage(string name) => (bool)(ImageLibrary?.Call("HasImage", name) ?? false);

        #endregion

        
        #endregion
    }
}