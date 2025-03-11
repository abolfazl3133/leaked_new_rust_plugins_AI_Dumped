using Oxide.Core;
using System;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ConVar;
using System.IO;
using System.Text;
using Network;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using System.Collections;
using Facepunch;
using Rust;

namespace Oxide.Plugins
{
    [Info("BombRocket", "sdapro", "1.0.0")]
    public class BombRocket : RustPlugin
    {
        #region Fields
        int repeatCount = 0;
        Timer repeatTimer;
        private const string SMOKE_ROCKET_PREFAB = "assets/prefabs/ammo/rocket/rocket_smoke.prefab";
        private const string SMOKE_PREFAB = "";
        public string textStart;
        private Timer GameTip;

        [JsonProperty(PropertyName = "Damage Bomb")] public static int DamageBOMB;
        [JsonProperty(PropertyName = "Damage Player")] public static int DamagePlayer;
        [JsonProperty(PropertyName = "Radius Damage Player")] public static int DamagePlayerRadius;
        [JsonProperty(PropertyName = "Explosion Height")] public static int explosionheight;


        void LoadDefaultConfig()

        {
            Puts("Creating a new configuration file for BombRocket.");
            var configData = new Dictionary<string, object>
            {
                ["textStart"] = "<color=#fff>Ядерный взрыв на острове!</color>",
                ["DamageBOMB"] = 2500,
                ["DamagePlayer"] = 100,
                ["DamagePlayerRadius"] = 80,
                ["explosionheight"] = 5,
            };

            Config.WriteObject(configData, true);
            SaveConfig();
        }

        void OnServerInitialized()
        {
            LoadConfigValues();
            Puts($"Text: {textStart}");
            Puts($"Damage Bomb: {DamageBOMB}");
            Puts($"Damage Player: {DamagePlayer}");
            Puts($"Radius Damage Player: {DamagePlayerRadius}");
            Puts($"Explosion Height: {explosionheight}");
        }

        void LoadConfigValues()
        {
            textStart = Config.Get<string>("textStart");
            DamageBOMB = Config.Get<int>("DamageBOMB");
            DamagePlayer = Config.Get<int>("DamagePlayer");
            DamagePlayerRadius = Config.Get<int>("DamagePlayerRadius");
            explosionheight = Config.Get<int>("explosionheight");
        }


        #endregion Fields

        #region Oxide Hooks

        private void OnEntityKill(BaseEntity entity, BasePlayer player)
        {

            if (!entity.IsValid())
                return;

            if (entity.PrefabName != SMOKE_ROCKET_PREFAB)
                return;


            BaseEntity smoke = GameManager.server.CreateEntity(SMOKE_PREFAB, entity.transform.position, Quaternion.identity);
            if (smoke)
                smoke.Spawn();
            

            repeatTimer = timer.Repeat(0.07f, 0, () =>
            {
            if (repeatCount < 30)
                foreach (var basePlayer in BasePlayer.activePlayerList)
                {
                    Effect.server.Run("assets/bundled/prefabs/fx/takedamage_generic.prefab", basePlayer.transform.position + Vector3.up * 1.0f);
                    repeatCount++;

                    if (repeatCount >= 30)
                    {
                        repeatTimer.Destroy();
                    }
                }
            });
            GrenadeExplosion explosion = new GrenadeExplosion(entity.transform.position);

            timer.Once(0.05f, () =>
            {
                foreach (var basePlayer in BasePlayer.activePlayerList)
                {
                    basePlayer.SendConsoleCommand("gametip.hidegametip");
                    basePlayer.SendConsoleCommand("gametip.showgametip", textStart);
                }

                GameTip?.Destroy();
                GameTip = timer.Once(10, () =>
                {
                    foreach (var basePlayer in BasePlayer.activePlayerList)
                    {
                        basePlayer.SendConsoleCommand("gametip.hidegametip");
                    }
                });

            });


        }

        public class GrenadeExplosion
        {
            private int count;
            private Vector3 position;

            private List<KeyValuePair<float, float>> blastRadius = new List<KeyValuePair<float, float>>
            {
                new KeyValuePair<float, float>(10f, 10f),
                new KeyValuePair<float, float>(5f, 5f),
                new KeyValuePair<float, float>(5f, 5f),
                new KeyValuePair<float, float>(5f, 5f),
                new KeyValuePair<float, float>(15f, 15f),
                new KeyValuePair<float, float>(18f, 18f),
            };

            public GrenadeExplosion() { }
            public GrenadeExplosion(Vector3 position)
            {
                this.position = position;
                BeginExplosion();
            }

            private void BeginExplosion()
            {
                BaseEntity entity = GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_basic.prefab", position);
                Next();
            }

            private void Next()
            {
                ServerMgr.Instance.StartCoroutine(CreateRing());
            }

            private IEnumerator CreateRing()
            {
                if (count == blastRadius.Count)
                    yield break;

                yield return new WaitForSeconds(0.25f);
                ExplosionRing(position + ((Vector3.up * explosionheight) * count), blastRadius[count].Key, blastRadius[count].Value);
                ++count;

                if (count == blastRadius.Count)
                {
                    KillPlayersInRadiusAfterExplosion();
                }
                else
                {
                    Next();
                }
            }

            private void ExplosionRing(Vector3 position, float radius, float amount)
            {
                float angle = 360f / amount;

                for (float i = 0f; i < amount; i++)
                {
                    float a = i * angle;
                    Vector3 pos = RandomCircle(position + (Vector3.up * 5), radius, a);
                    CreateExplosionAtPosition(pos);
                }

                for (float i = 0f; i < amount; i++)
                {
                    float a = i * angle;
                    Vector3 pos = RandomCircle(position + (Vector3.right * 5), radius, a);
                    CreateExplosionAtPosition(pos);
                }

                for (float i = 0f; i < amount; i++)
                {
                    float a = i * angle;
                    Vector3 pos = RandomCircle(position - (Vector3.right * 5), radius, a);
                    CreateExplosionAtPosition(pos);
                }
            }

            private void KillPlayersInRadiusAfterExplosion()
            {
                List<BasePlayer> playersInRange = new List<BasePlayer>();
                Vis.Entities(position, DamagePlayerRadius, playersInRange);


                foreach (var player in playersInRange)
                {
                    player.Hurt(DamagePlayer, Rust.DamageType.Explosion);
                }
            }

            private void CreateExplosionAtPosition(Vector3 pos)
            {
                BaseEntity entity = GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_basic.prefab", pos);
                if (entity != null)
                {
                    entity.Spawn();
                    TimedExplosive explosive = entity.GetComponent<TimedExplosive>();
                    if (explosive != null)
                    {
                        explosive.damageTypes = new List<DamageTypeEntry>
                {
                    new DamageTypeEntry { amount = DamageBOMB, type = DamageType.Explosion }
                };
                        explosive.Explode();
                    }
                }
            }

            private Vector3 RandomCircle(Vector3 center, float radius, float angle)
            {
                Vector3 pos;
                pos.x = center.x + radius * Mathf.Cos(angle * Mathf.Deg2Rad);
                pos.z = center.z + radius * Mathf.Sin(angle * Mathf.Deg2Rad);
                pos.y = center.y;
                return pos;
            }
        }

        #endregion Oxide Hooks
    }
}