//#define DEBUG

using Newtonsoft.Json;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("PlanterBoxEvents", "Tangerine", "1.0.0")]
    [Description("PlanterBoxEvents")]
    public class PlanterBoxEvents : RustPlugin
    {
        #region Variables

        private readonly HashSet<PlanterBox> _planters = new HashSet<PlanterBox>();
        private readonly HashSet<PlanterBox> _plantersWithEvent = new HashSet<PlanterBox>();

        private const string Permission = "planterboxevents.use";
        private const string ChickenPrefab = "assets/rust.ai/agents/chicken/chicken.prefab";
        private const string FireBallPrefab = "assets/bundled/prefabs/fireball_small.prefab";

        private static PlanterBoxEvents _plugin;

        private readonly List<BaseEventDefinition> _availableEvents = new ();
        
        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            _plugin = this;
            
            permission.RegisterPermission(Permission, this);

            foreach (var networkable in BaseNetworkable.serverEntities)
            {
                if (networkable.IsValid() == false || networkable.IsDestroyed)
                    continue;

                if (networkable is PlanterBox planter == false)
                    continue;

                if (planter.OwnerID == 0 || HasPermission(planter.OwnerID.ToString(), Permission) == false)
                    continue;

                _planters.Add(planter);
            }

            _availableEvents.Add(_config.СhickenEvent);
            _availableEvents.Add(_config.FireEvent);
            
            timer.Every(_config.EventsTimerSeconds, TryInitializeEvents);
        }

        void Unload()
        {
            _plugin = null;
            _planters.Clear();
        }

        private void OnEntityBuilt(Planner plan, GameObject gameObject)
        {
            var planter = gameObject.GetComponent<PlanterBox>();

            if (planter == null)
                return;

            if (planter.OwnerID == 0 || HasPermission(planter.OwnerID.ToString(), Permission) == false)
                return;

            _planters.Add(planter);
        }

        #endregion

        #region Commands

        [ConsoleCommand("planterevent.start")]
        private void Console_PlanterEventStart(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin == false) 
                return;

            var player = arg.Player();
            if (player == null)
            {
                SendReply(arg, "Only works from player console!");
                return;
            }

            var entites = Facepunch.Pool.GetList<PlanterBox>();
            try
            {
                Vis.Entities(player.transform.position, 5f, entites);
                if (entites.Count == 0)
                {
                    SendReply(arg, "No planter boxes found!");
                    return;
                }
                
                CreateEventFor(entites[0]);
                SendReply(arg, "Event created!");
            }
            finally
            {
                Facepunch.Pool.FreeList(ref entites);
            }
        }

        #endregion
        
        #region Core

        private void TryInitializeEvents()
        {
            foreach (var planter in _planters)
            {
                if (planter.children.Count == 0)
                    continue;

                if(_plantersWithEvent.Contains(planter))
                    continue;

                var random01 = Core.Random.Range(0, 1f);
                
#if DEBUG
                PrintToChat($"Chance: {random01}");
#endif
                
                var validEvents = _availableEvents.FindAll(x => x.Chance <= random01);
                if (validEvents.Count == 0)
                    continue;

                BaseEventDefinition eventDefinition = null;
                if (validEvents.Count > 1)
                {
                    eventDefinition = validEvents.GetRandom();
                }
                else
                {
                    eventDefinition = validEvents[0];
                }
                
                StartEvent(planter, eventDefinition);
            }

#if DEBUG
            PrintToChat($"TryInitiateEvents tick!");
#endif
        }

        private void CreateEventFor(PlanterBox planter)
        {
            StartEvent(planter, _availableEvents.GetRandom());
        }

        private void StartEvent(PlanterBox planter, BaseEventDefinition eventDefinition)
        {
            var growables = planter.children.FindAll(x => x is GrowableEntity);
            if (growables.Count == 0)
                return;
            
            GrowableEntity growable = null;
            if (growables.Count > 1)
            {
                growable = (GrowableEntity)growables.GetRandom();
            }
            else
            {
                growable = (GrowableEntity)growables[0];
            }
            
            var entity = eventDefinition.SpawnEntity(growable.transform.position + new Vector3(0, 0.2f, 0));
            if (entity == null)
            {
#if DEBUG
                PrintToChat($"Couldn't create event entity '{eventDefinition.GetType()}'");
#endif
                return;
            }

            ApplyEventDamage(planter, growable, entity, eventDefinition);

            var playerOwner = BasePlayer.FindByID(planter.OwnerID);
            if (playerOwner != null && playerOwner.IsConnected)
            {
                switch (eventDefinition)
                {
                    case ChickenEventDefinition:
                    {
                        PlayerMessage(playerOwner, LangKey.Chat_ChickensOnFarm);
                        break;
                    }
                    case FireEventDefinition:
                    {
                        PlayerMessage(playerOwner, LangKey.Chat_FarmOnFire);
                        break;
                    }
                }
            }
#if DEBUG
            PrintToChat($"Event '{eventDefinition.GetType()}' started at position: {planter.transform.position}");
#endif

        }

        private void ApplyEventDamage(PlanterBox planter, GrowableEntity growable, BaseEntity entity, BaseEventDefinition eventDefinition)
        {
            _plantersWithEvent.Add(planter);
            
            entity.SetParent(growable, true);
            var component = entity.GetOrAddComponent<PlanterEventHandler>();
            component.DamagePerTick = eventDefinition.DamagePerSecond;
        }
        
        #endregion

        #region Scripts

        private class PlanterEventHandler : MonoBehaviour
        {
            private GrowableEntity _growable;
            private BaseEntity _ownerEntity;
            private PlanterBox _planter;

            public float DamagePerTick = 1f;

            private void Start()
            {
                _ownerEntity = GetComponent<BaseEntity>();
                _growable = GetComponentInParent<GrowableEntity>();
                if (_growable == null)
                {
                    Destroy(this);
                    return;
                }

                _planter = _growable.planter;
                if (_planter == null)
                {
                    Destroy(this);
                    return;
                }
                
                InvokeRepeating(nameof(DamageTick), 1f, 1f);
            }

            private void DamageTick() 
            {
                if (_plugin == null || _plugin.IsLoaded == false)
                {
                    _ownerEntity.Kill();
                    Destroy(this);
                    return;
                }

                if (_ownerEntity == null || _ownerEntity.IsDestroyed)
                {
                    Destroy(this);
                    return;
                }
                
                _growable.SetHealth(_growable.health - DamagePerTick);
                _growable.SendNetworkUpdate();

                if (_growable.health <= 0)
                {
                    _plugin._plantersWithEvent.Remove(_growable.planter);
                    _growable.Kill();
                }
            }

            private void OnDestroy()
            {
                if (_plugin == null)
                    return;
#if DEBUG
                _plugin.PrintToChat($"Planter event handler was destroyed!");
#endif
                if(_planter != null)
                    _plugin._plantersWithEvent.Remove(_planter);
            }

        }

        #endregion
        
        #region Config

        private static PluginConfig _config;

        private class PluginConfig
        {
            [JsonProperty("Chicken event properties", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public ChickenEventDefinition СhickenEvent = new()
            {
                Chance = 0.1f,
                DamagePerSecond = 1.5f
            };
            
            [JsonProperty("Fire event properties", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public FireEventDefinition FireEvent = new()
            {
                Chance = 0.1f,
                DamagePerSecond = 2.5f
            };

            [JsonProperty("Time interval in seconds between each attempt to initialize events")]
            public float EventsTimerSeconds = 60f;
        }

        private class BaseEventDefinition
        {
            [JsonProperty("Damage per second")]
            public float DamagePerSecond = 0.5f;

            [JsonProperty("Chance to start event (range: 0 - 1, -1 to off event)")]
            public float Chance = 0.05f;

            public virtual BaseEntity SpawnEntity(Vector3 position)
            {
                throw new NotImplementedException();
            }
        }
        
        private class ChickenEventDefinition : BaseEventDefinition
        {
            public override BaseEntity SpawnEntity(Vector3 position)
            {
                var chicken = GameManager.server.CreateEntity(ChickenPrefab,  position) as Chicken;
                if (chicken == null)
                    return null;

                chicken.Spawn();
                if (chicken.brain != null)
                {
                    _plugin.timer.Once(1f, () =>
                    {
                        chicken.brain.CancelInvoke(chicken.brain.TickMovement);
                        BaseEntity.Query.Server.RemoveBrain(chicken);
                        chicken.HasBrain = false;
                    });
                }
                chicken.SendNetworkUpdate();
                return chicken;
            }
        }
        
        private class FireEventDefinition : BaseEventDefinition
        {
            public override BaseEntity SpawnEntity(Vector3 position)
            {
                var fireBall = GameManager.server.CreateEntity(FireBallPrefab, position) as FireBall;
                if (fireBall == null)
                    return null;
                
                fireBall.Spawn();
                fireBall.damagePerSecond = 0f;
                fireBall.CancelInvoke(fireBall.TryToSpread);
                fireBall.SendNetworkUpdate();
                return fireBall;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
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
                _config = GetDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {

            };
        }

        #endregion
        
        #region Localisation v0.0.1
        private enum LangKey
        {
            Chat_FarmOnFire,
            Chat_ChickensOnFarm
        }
        
        protected override void LoadDefaultMessages()
        {
            var dict = new Dictionary<LangKey, string>
            {
                [LangKey.Chat_FarmOnFire] = "Your farm caught on fire!",
                [LangKey.Chat_ChickensOnFarm] = "Chickens are destroying your farm!"
            };
            
            lang.RegisterMessages(dict.ToDictionary(x => x.Key.ToString(), x => x.Value), this);
        }
        
        private void PlayerMessage(BasePlayer player, LangKey message, params object[] args)
        {
            Player.Message(player, string.Format(GetLocal(player.UserIDString, message.ToString()), args), 0);// _config.ChatIconId);
        }
        
        private void PlayerMessage(BasePlayer player, string message, params object[] args)
        {
            Player.Message(player, string.Format(message, args), 0);// _config.ChatIconId);
        }
        
        private string GetLocal(string playerId, string key) => lang.GetMessage(key, this, playerId);
        
        private string GetLocal(string playerId, LangKey key) => lang.GetMessage(key.ToString(), this, playerId);
        
        #endregion
        
        #region Utils

        private bool HasPermission(string playerId, string name) => permission.UserHasPermission(playerId, name);

        #endregion
    }
}
