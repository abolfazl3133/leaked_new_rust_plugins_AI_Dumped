using Newtonsoft.Json;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CargoShipSink", "By RIPJAWBONES", "1.0.1")]
    public class CargoShipSink : RustPlugin
    {
        private Configuration config;
        private CargoShip cargoShip;
        private Timer sinkTimer;
        private Timer removeTimer;
        private bool isSinking = false;
        private bool secondEffectInProgress = false;
        private bool finalEffectInProgress = false;

        private class Configuration
        {
            [JsonProperty(PropertyName = "SinkTime")]
            public float SinkTime { get; set; } = 1800;
            [JsonProperty(PropertyName = "SinkSpeed")]
            public float SinkSpeed { get; set; } = 0.01f;
            [JsonProperty(PropertyName = "TiltSpeed")]
            public float TiltSpeed { get; set; } = 0.2f;
            [JsonProperty(PropertyName = "RotationSpeed")]
            public float RotationSpeed { get; set; } = 0.6f;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            Config.WriteObject(config, true);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch (Exception e)
            {
                PrintError($"Failed to load config: {e.Message}");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        void OnEntitySpawned(CargoShip cargo)
        {
            if (cargo == null) return;

            cargoShip = cargo;

            if (sinkTimer != null)
            {
                sinkTimer.Destroy();
                removeTimer.Destroy();
            }


            if (cargoShip != null && !isSinking)
            {
                timer.Once(config.SinkTime, () => SpawnRockets());
                sinkTimer = timer.Once(config.SinkTime, StartSinking);
            }
        }

        void StartSinking()
        {
            if (cargoShip != null && !isSinking)
            {
                isSinking = true;
                sinkTimer.Destroy();
                secondEffectInProgress = false;
                finalEffectInProgress = false;
                removeTimer = timer.Once(1800, RemoveCargoShip);
                SendChatMessage("The Cargo Ship has been hit with Torpedo Rockets!");

                timer.Every(5.0f, () =>
                {
                    if (isSinking && cargoShip != null && cargoShip.transform.position.y > -5f)
                    {
                        cargoShip.transform.position -= new Vector3(0, config.SinkSpeed, 0);
                        cargoShip.transform.Rotate(0, 0, config.RotationSpeed);
                        cargoShip.transform.Rotate(config.TiltSpeed, 0, 0);

                        if (cargoShip.transform.position.y < -1f)
                        {
                           SendChatMessage("The Cargo Ship is sinking!");
                           PerformSecondEffect();
                        }
                    }
                    else if (isSinking && cargoShip != null)
                    {
                        isSinking = false;
                    }
                });
            }
        }

        void PerformSecondEffect()
        {
            secondEffectInProgress = true;
            isSinking = false;
            timer.Every(2.0f, () =>
            {
                if (secondEffectInProgress && cargoShip != null && cargoShip.transform.position.y > -10f)
                {
                    cargoShip.transform.position -= new Vector3(0, config.SinkSpeed, 0);
                    cargoShip.transform.Rotate(0, 0, config.RotationSpeed);
                    cargoShip.transform.Rotate(config.TiltSpeed, 0, 0);

                    if (cargoShip.transform.position.y < -6f)
                    {
                        SendChatMessage("The Cargo Ship is not holding up much longer!");
                        PerformFinalEffect();
                    }
                }
                else if (secondEffectInProgress && cargoShip != null)
                {
                    isSinking = false;
                    secondEffectInProgress = false; 
                }
            });
        }

        void PerformFinalEffect()
        {
            finalEffectInProgress = true;
            secondEffectInProgress = false;
            timer.Every(0.1f, () =>
            {
                if (finalEffectInProgress && cargoShip != null && cargoShip.transform.position.y > -10f)
                {
                    cargoShip.transform.position -= new Vector3(0, config.SinkSpeed, 0);
                    cargoShip.transform.Rotate(0, 0, config.RotationSpeed);
                    cargoShip.transform.Rotate(config.TiltSpeed, 0, 0);

                    if (cargoShip.transform.position.y < -8f)
                    {
                    }
                }
                else if (finalEffectInProgress && cargoShip != null)
                {
                    SendChatMessage("The Cargo Ship has sunk!");
                    cargoShip.Kill();
                    isSinking = false;
                    secondEffectInProgress = false;
                    finalEffectInProgress = false;
                }
            });
        }

        void RemoveCargoShip()
        {
            if (cargoShip != null)
            {
                SendChatMessage("The cargo ship is gone!");
                cargoShip.Kill();
            }
        }

        void SendChatMessage(string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                SendReply(player, message);
            }
        }

        void SpawnRockets()
        {

            for (int i = 0; i < 5; i++) 
           {
              Vector3 rocketPosition = cargoShip.transform.position + new Vector3(UnityEngine.Random.Range(-5f, 5f), 10f, UnityEngine.Random.Range(-5f, 5f));
              Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab", rocketPosition, Vector3.zero);
           }
        }
    }
}
