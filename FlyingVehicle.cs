using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using Physics = UnityEngine.Physics;

namespace Oxide.Plugins
{
    [Info("Flying Vehicle", "misticos", "1.0.3")]
    [Description("Make vehicles fly on your server")]
    class FlyingVehicle : CovalencePlugin
    {
        #region Variables

        private const string PermissionUse = "flyingvehicle.use";

        private const string PrefabBoat = "assets/content/vehicles/boats/rowboat/rowboat.prefab";
        private const string PrefabRhib = "assets/content/vehicles/boats/rhib/rhib.prefab";
        private const string PrefabSedan = "assets/content/vehicles/sedan_a/sedantest.entity.prefab";
        private const string PrefabTomaha = "assets/content/vehicles/snowmobiles/tomahasnowmobile.prefab";
        private const string PrefabBike = "assets/content/vehicles/bikes/motorbike.prefab";
        private const string PrefabPedalBike = "assets/content/vehicles/bikes/pedalbike.prefab";
        private const string TugBoat = "assets/content/vehicles/boats/tugboat/tugboat.prefab";
        private const string SubmarineDuo = "assets/content/vehicles/submarine/submarineduo.entity.prefab";
        private const string PrefabKayak = "assets/content/vehicles/boats/kayak/kayak.prefab";

        private const string PrefabSeat = "assets/prefabs/vehicle/seats/copilotseat.prefab";

        private string _seatGuid = string.Empty;
        
        #endregion
        
        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
            
            AddCovalenceCommand("flyingvehicle.spawn", nameof(CommandSpawn));
            AddCovalenceCommand("flyingvehicle.claim", nameof(CommandClaim));
        }

        private void OnServerInitialized()
        {
            // Find the GUID of the seat we need
            // It is a foreach because pathToGuid is internal
            foreach (var x in GameManifest.guidToPath)
            {
                if (x.Value == PrefabSeat)
                    _seatGuid = x.Key;
            }
        }

        private void Unload()
        {
            foreach (var boat in UnityEngine.Object.FindObjectsOfType<VehicleController>())
                UnityEngine.Object.DestroyImmediate(boat.gameObject);
        }

        private void OnPlayerInput(BasePlayer player, InputState state)
        {
            var vehicle = player.GetMountedVehicle()?.GetComponent<VehicleController>(); // TODO: Toggle collider
            if (vehicle == null || vehicle.Vehicle.GetDriver() != player)
                return;

            if (state.WasJustPressed(BUTTON.USE))
            {
                vehicle.Toggle();
            }

            /*
            if (state.WasJustPressed(BUTTON.DUCK))
            {
                foreach (var rigidbody in vehicle.Vehicle.GetComponentsInChildren<Rigidbody>())
                    rigidbody.detectCollisions = !rigidbody.detectCollisions;
            }
            */
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"No Permission", "You do not have enough permissions to do this (flyingvehicle.use)."},
                {"Command: Spawn: Syntax", "Syntax (flyingvehicle.spawn):\n" +
                                           "(rhib/car/boat)"},
                {"Command: Spawn: Spawned", "Your vehicle was spawned."},
                {"Command: Spawn: Unknown Prefab", "Entity was not spawned, contact the developer."},
                {"Command: Claim: Player Only", "You should be in the game to run this command."},
                {"Command: Claim: Unknown Entity", "We were unable to get this entity or it is already spawned with this plugin."},
                {"Command: Claim: Claimed", "We have successfully modified this entity."}
            }, this);
        }

        #endregion
        
        #region Commands

        private void CommandSpawn(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PermissionUse))
            {
                player.Reply(GetMsg("No Permission", player.Id));
                return;
            }

            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null)
                return;

            RaycastHit hit;
            if (!Physics.Raycast(basePlayer.eyes.HeadRay(), out hit))
                return;

            var position = hit.point;
            var type = args.Length > 0 ? args[0] : string.Empty;
            var prefab = string.Empty;
            
            switch (type)
            {
                case "rhib":
                {
                    prefab = PrefabRhib;
                    break;
                }

                case "car":
                {
                    prefab = PrefabSedan;
                    break;
                }
                
                case "boat":
                {
                    prefab = PrefabBoat;
                    break;
                }

                case "tomaha":
                {
                    prefab = PrefabTomaha;
                    break;
                }

                case "bike":
                {
                    prefab = PrefabBike;
                    break;
                }

                case "pedalbike":
                {
                    prefab = PrefabPedalBike;
                    break;
                }

                case "submarineduo":
                {
                    prefab = SubmarineDuo;
                    break;
                }
                case "kayak": // Новый тип
                {
                    prefab = PrefabKayak;
                    break;
                }
                case "tugboat":
                {
                    prefab = TugBoat;
                    break;
                }
            }

            if (string.IsNullOrEmpty(prefab))
            {
                player.Reply(GetMsg("Command: Spawn: Syntax", player.Id));
                return;
            }

            var entity = GameManager.server.CreateEntity(prefab, position + Vector3.up * 3f) as BaseVehicle;
            if (entity == null)
            {
                player.Reply(GetMsg("Command: Spawn: Unknown Prefab", player.Id));
                return;
            }

            Setup(entity);
        }

        private void CommandClaim(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PermissionUse))
            {
                player.Reply(GetMsg("No Permission", player.Id));
                return;
            }

            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null)
            {
                player.Reply(GetMsg("Command: Claim: Player Only", player.Id));
                return;
            }

            var entity = basePlayer.GetMountedVehicle();
            if (entity == null || entity.GetComponent<VehicleController>() != null)
            {
                player.Reply(GetMsg("Command: Claim: Unknown Entity", player.Id));
                return;
            }

            Setup(entity);
            
            player.Reply(GetMsg("Command: Claim: Claimed", player.Id));
        }

        private object OnVehiclePush(BaseVehicle boat, BasePlayer player)
        {
            if (boat.GetComponent<VehicleController>()?.IsEnabled ?? false)
                return false;
            
            return null;
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container.GetParentEntity()?.GetComponent<VehicleController>()?.IsEnabled ?? false)
                return false;

            return null;
        }
        
        #endregion
        
        #region Helpers

        private void Setup(BaseVehicle entity)
        {
            if (entity.mountPoints.Count > 0)
                entity.mountPoints[0].prefab.guid = _seatGuid; // Assign new driver seat so that you cannot look around

            if (!entity.isSpawned)
            {
                entity.Spawn();
            }
            else
            {
                entity.DismountAllPlayers();
                
                // Delete all old seats
                foreach (var mount in entity.mountPoints)
                    mount.mountable.Kill();
                
                // Respawn all new seats including the modified driver one
                entity.SpawnSubEntities();
            }

            var boat = entity as BaseBoat;
            if (boat != null)
            {
                boat.engineThrust = 0.0f;
                boat.steeringScale = 0.0f;
                boat.buoyancy.doEffects = false;
                boat.buoyancy.buoyancyScale = 0.0f;
                boat.buoyancy.flowMovementScale = 0.0f;

                foreach (var point in boat.buoyancy.points)
                {
                    point.doSplashEffects = false;
                    point.waveScale = 0.0f;
                    point.waveFrequency = 0.0f;
                }
            }

            var car = entity as BasicCar;
            if (car != null)
            {
                foreach (var wheel in car.wheels)
                {
                    var col = wheel.wheelCollider;
                    
                    col.suspensionDistance = 0.2f;
                    col.suspensionSpring = new JointSpring
                    {
                        damper = 200,
                        targetPosition = 0.3f,
                        spring = 2000
                    };
                }
            }

            foreach (var mount in entity.mountPoints)
            {
                // You should not be able to hold items!
                mount.mountable.canWieldItems = false;
            }
            
            entity.gameObject.AddComponent<VehicleController>();
        }
        
        #endregion
        
        #region Controller

        private class VehicleController : FacepunchBehaviour
        {
            public Rigidbody Rigidbody;
            public BaseVehicle Vehicle;
            public bool IsEnabled = false;

            private void Awake()
            {
                Rigidbody = GetComponent<Rigidbody>();
                Vehicle = GetComponent<BaseVehicle>();

                Rigidbody.velocity = Vector3.down;
                Rigidbody.mass = 50f;
                Rigidbody.angularVelocity = Vector3.zero;
                Rigidbody.centerOfMass = new Vector3(0.0f, -0.2f, 1.4f);
                Rigidbody.inertiaTensor = new Vector3(220.8f, 207.3f, 55.5f);
            }

            private void OnDestroy()
            {
                if (Vehicle != null && !Vehicle.IsDestroyed)
                    Vehicle.Kill();
            }

            private void FixedUpdate()
            {
                if ((Vehicle?.IsDestroyed ?? true) || Rigidbody == null)
                {
                    DestroyImmediate(this);
                    return;
                }

                // yes.
                Rigidbody.drag = 0.6f;
                Rigidbody.angularDrag = 5.0f;

                if (!IsEnabled)
                    return;

                var driver = Vehicle.GetDriver();
                if (driver == null)
                    return;
                
                var input = driver.serverInput;
                if (input.IsDown(BUTTON.RELOAD))
                {
                    // STOP!
                    Rigidbody.drag *= 5;
                    Rigidbody.angularDrag *= 2;
                }
                
                /*
                 * FORCE (MOVEMENT)
                 */

                var direction = Vector3.zero;

                if (input.IsDown(BUTTON.FORWARD))
                    direction += Vector3.forward;

                if (input.IsDown(BUTTON.BACKWARD))
                    direction += Vector3.back;

                if (direction != Vector3.zero)
                {
                    const float moveSpeed = 500f;
                    var speed = input.IsDown(BUTTON.SPRINT) ? moveSpeed * 3f : moveSpeed;

                    Rigidbody.AddRelativeForce(direction * speed, ForceMode.Force);
                }

                /*
                 * TORQUE (ROTATION)
                 */
                
                // PLEASE! Let me know if you have better ideas regarding rotation and making it smoother, better, et cetera.
                
                var torque = Vector3.zero;

                const float rotationSpeed = 700f;
                if (input.IsDown(BUTTON.LEFT))
                {
                    torque += new Vector3(0, -rotationSpeed, 0);
                }

                if (input.IsDown(BUTTON.RIGHT))
                {
                    torque += new Vector3(0, rotationSpeed, 0);
                }

                const float mouseSpeedY = 400f;
                const float mouseSpeedX = 100f;

                var mouse = input.current.mouseDelta;
                torque += new Vector3(mouse.y * mouseSpeedY, 0, mouse.x * -mouseSpeedX);
                
                Rigidbody.AddRelativeTorque(torque, ForceMode.Force);
            }

            public void Toggle()
            {
                if (IsEnabled)
                {
                    Rigidbody.useGravity = true;
                }
                else
                {
                    Rigidbody.useGravity = false;
                    Rigidbody.AddForce(Vector3.up * 50f, ForceMode.Impulse);
                }

                IsEnabled = !IsEnabled;
            }
        }
        
        #endregion
        
        #region Helpers

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}