/*      INFO
          ____ _ _  _ ____ _  _ _    ____ ____ _ ___ _   _ 
          [__  | |\ | | __ |  | |    |__| |__/ |  |   \_/  
          ___] | | \| |__] |__| |___ |  | |  \ |  |    |   
        --------------------------------------------------
        For Quickest Support and Business Inquiries:
        Discord: singularity_gg
        
        For Updates or Support:
        [https://codefling.com/singularity12]
        --------------------------------------------------
        Paraglider introduces Control Adjustments with Lift, Descent, Boost, Launch, Trails/Effects, and Jet Drop
        with configurable Permissions, Cooldowns, Fuel Rates, and more.

        Customize these from within your config file located at (yourserver/oxide/config/Paraglider.json)
        --------------------------------------------------
*/
/*      PERMISSIONS INCLUDE: (Granting a higher tier permission overrides lower tier versions of that permission)
        
        "paraglider.use"--------------------Use improved parachute (as paraglider) or any of its controls and effects
        "paraglider.launchcmd"--------------Use the /launch chat command
        "paraglider.launcht1"---------------TIER 1: Launch and take off from standing
        "paraglider.launcht2"---------------TIER 2: Launch and take off from standing
        "paraglider.launcht3"---------------TIER 3: Launch and take off from standing
        "paraglider.boostt1"----------------TIER 1: Use boost to accelerate forward or backwards in Paraglider
        "paraglider.boostt2"----------------TIER 2: Use boost to accelerate forward or backwards in Paraglider
        "paraglider.boostt3"----------------TIER 3: Use boost to accelerate forward or backwards in Paraglider
        "paraglider.afterburnert1"----------TIER 1: Use afterburners for lift and altitude
        "paraglider.afterburnert2"----------TIER 2: Use afterburners for lift and altitude
        "paraglider.afterburnert3"----------TIER 3: Use afterburners for lift and altitude
        "paraglider.fuelt2"-----------------TIER 2: Fuel Capacity and recharge (TIER 1 is default with afterburner perms)
        "paraglider.fuelt3"-----------------TIER 3: Fuel Capacity and recharge
        "paraglider.lowert1"----------------TIER 1: Lower the Paraglider quickly
        "paraglider.lowert2"----------------TIER 2: Lower the Paraglider quickly
        "paraglider.lowert3"----------------TIER 3: Lower the Paraglider quickly
        "paraglider.jetdropt1"--------------TIER 1: Drop from a jet with /drop
        "paraglider.jetdropt2"--------------TIER 2: Drop from a jet with /drop
        "paraglider.jetdropt3"--------------TIER 3: Drop from a jet with /drop
        "paraglider.useflares"--------------Use flares to disable SAM targeting
        "paraglider.freeflares"-------------No need for flares in Inventory
        "paraglider.smoke"------------------Use smoke trail effect
        "paraglider.unlimitedfuel"----------Afterburners to no fuel
        "paraglider.spawnwithparachute"-----Respawn wearing a parachute
        "paraglider.nolaunchcd"-------------No cooldown for Launch takeoff
        "paraglider.nojetdropcd"------------No cooldown for Jet Drop
        "paraglider.jetrespawn"-------------Respawn automatically in a Jet Drop 
        "paraglider.yesescape"--------------Ignore No Escape Raid/Combat Block
*/


using System;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using UnityEngine;
using Facepunch;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Paraglider", "Singularity", "2.4.2")]
    [Description("Introduces new, configurable mechanics to Parachutes")]

    class Paraglider : RustPlugin
    {
        #region Fields

        [PluginReference]
        Plugin NoEscape;

        #region Permissions

        private const string PermissionParaglide = "paraglider.use";
        private const string PermissionLaunchCommand = "paraglider.launchcmd";
        private const string PermissionLaunchT1 = "paraglider.launcht1";
        private const string PermissionLaunchT2 = "paraglider.launcht2";
        private const string PermissionLaunchT3 = "paraglider.launcht3";
        private const string PermissionBoostT1 = "paraglider.boostt1";
        private const string PermissionBoostT2 = "paraglider.boostt2";
        private const string PermissionBoostT3 = "paraglider.boostt3";
        private const string PermissionAfterburnerT1 = "paraglider.afterburnert1";
        private const string PermissionAfterburnerT2 = "paraglider.afterburnert2";
        private const string PermissionAfterburnerT3 = "paraglider.afterburnert3";
        private const string PermissionFuelT2 = "paraglider.fuelt2";
        private const string PermissionFuelT3 = "paraglider.fuelt3";
        private const string PermissionNoFuelRequired = "paraglider.unlimitedfuel";
        private const string PermissionLowerT1 = "paraglider.lowert1";
        private const string PermissionLowerT2 = "paraglider.lowert2";
        private const string PermissionLowerT3 = "paraglider.lowert3";
        private const string PermissionUseFlares = "paraglider.useflares";
        private const string PermissionFreeFlares = "paraglider.freeflares";
        private const string PermissionSmoke = "paraglider.smoke";
        private const string PermissionJetDropT1 = "paraglider.jetdropt1";
        private const string PermissionJetDropT2 = "paraglider.jetdropt2";
        private const string PermissionJetDropT3 = "paraglider.jetdropt3";
        private const string PermissionSpawnWithChute = "paraglider.spawnwithparachute";
        private const string PermissionNoLaunchCooldown = "paraglider.nolaunchcd";
        private const string PermissionNoJetDropCooldown = "paraglider.nojetdropcd";
        private const string PermissionSpawnfromJet = "paraglider.jetrespawn";
        private const string PermissionIgnoreNoEscape = "paraglider.yesescape";

        #endregion

        #region Prefabs

        public string FlareEffect = "assets/content/vehicles/attackhelicopter/effects/pfx_flares_attackhelicopter.prefab";
        public string AfterburnerEffect = "assets/prefabs/clothes/diving.tank/effects/tank_refill.prefab";
        public string BoosterEffectA = "assets/prefabs/tools/spraycan/reskineffect.prefab"; //Currently commented out
        public string BoosterEffectB = "assets/prefabs/weapons/toolgun/effects/ringeffect.prefab";
        private const string SupplySignalPath = "assets/prefabs/tools/supply signal/grenade.supplysignal.deployed.prefab";
        public string ParachuteVehiclePrefab = "assets/prefabs/vehicle/seats/parachuteseat.prefab";
        public string PreLaunchEffect = "assets/content/effects/crossbreed/pfx crossbreed plain.prefab";

        #endregion

        #region Entities

        private BaseEntity chairEntity;
        private BaseEntity spawnedJet;
        private BaseEntity spawnedRocket;
        private Item flareToConsume;

        #endregion

        #region Dictionaries and Data

        private ParagliderConfiguration _config;
        private const string ConfigFileVersion = "2.4.0";

        private Dictionary<BasePlayer, BaseEntity> playerParachuteMap = new Dictionary<BasePlayer, BaseEntity>();
        private Dictionary<string, float> playerLastAfterburnTime = new Dictionary<string, float>();
        private Dictionary<string, float> playerLastBoostTime = new Dictionary<string, float>();
        private Dictionary<string, float> playerLastFlaresTime = new Dictionary<string, float>();
        private Dictionary<string, float> playerFuel = new Dictionary<string, float>();
        private Dictionary<string, float> lastRefillTime = new Dictionary<string, float>();
        private Dictionary<string, float> lastFuelEmptyMessageTime = new Dictionary<string, float>();
        private Dictionary<string, float> lastFuelFullMessageTime = new Dictionary<string, float>();
        private Dictionary<string, float> lastEquipMessageTime = new Dictionary<string, float>();
        private Dictionary<string, float> lastLaunchMessageTime = new Dictionary<string, float>();
        private Dictionary<string, float> lastJetMessageTime = new Dictionary<string, float>();
        private Dictionary<string, float> lastFlareMessageTime = new Dictionary<string, float>();
        private Dictionary<string, float> lastLaunchTime = new Dictionary<string, float>();
        private Dictionary<string, float> lastCancelLaunchTime = new Dictionary<string, float>();
        private Dictionary<string, float> lastJetDropTime = new Dictionary<string, float>();

        Dictionary<string, object> launchingPlayers = new Dictionary<string, object>();

        public List<BaseEntity> smoketraillist = new List<BaseEntity>();

        private DynamicConfigFile playerData;
        private Dictionary<ulong, PlayerSettings> playerSettings = new Dictionary<ulong, PlayerSettings>();

        private Dictionary<ulong, Dictionary<string, bool>> playerPermissions = new Dictionary<ulong, Dictionary<string, bool>>();
        private List<string> permissionsToCache = new List<string>
        {
        "paraglider.use",
        "paraglider.launcht1",
        "paraglider.launcht2",
        "paraglider.launcht3",
        "paraglider.boostt1",
        "paraglider.boostt2",
        "paraglider.boostt3",
        "paraglider.afterburnert1",
        "paraglider.afterburnert2",
        "paraglider.afterburnert3",
        "paraglider.fuelt2",
        "paraglider.fuelt3",
        "paraglider.unlimitedfuel",
        "paraglider.lowert1",
        "paraglider.lowert2",
        "paraglider.lowert3",
        "paraglider.useflares",
        "paraglider.freeflares",
        "paraglider.smoke",
        };

        ///
        class PlayerSettings
        {
            public bool ToggleParaglider { get; set; }
        }
        ///

        #endregion

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermissionParaglide, this);
            permission.RegisterPermission(PermissionLaunchT1, this);
            permission.RegisterPermission(PermissionLaunchT2, this);
            permission.RegisterPermission(PermissionLaunchT3, this);
            permission.RegisterPermission(PermissionBoostT1, this);
            permission.RegisterPermission(PermissionBoostT2, this);
            permission.RegisterPermission(PermissionBoostT3, this);
            permission.RegisterPermission(PermissionAfterburnerT1, this);
            permission.RegisterPermission(PermissionAfterburnerT2, this);
            permission.RegisterPermission(PermissionAfterburnerT3, this);
            permission.RegisterPermission(PermissionFuelT2, this);
            permission.RegisterPermission(PermissionFuelT3, this);
            permission.RegisterPermission(PermissionNoFuelRequired, this);
            permission.RegisterPermission(PermissionLowerT1, this);
            permission.RegisterPermission(PermissionLowerT2, this);
            permission.RegisterPermission(PermissionLowerT3, this);
            permission.RegisterPermission(PermissionUseFlares, this);
            permission.RegisterPermission(PermissionFreeFlares, this);
            permission.RegisterPermission(PermissionSmoke, this);
            permission.RegisterPermission(PermissionJetDropT1, this);
            permission.RegisterPermission(PermissionJetDropT2, this);
            permission.RegisterPermission(PermissionJetDropT3, this);
            permission.RegisterPermission(PermissionSpawnWithChute, this);
            permission.RegisterPermission(PermissionNoLaunchCooldown, this);
            permission.RegisterPermission(PermissionNoJetDropCooldown, this);
            permission.RegisterPermission(PermissionSpawnfromJet, this);
            permission.RegisterPermission(PermissionIgnoreNoEscape, this);


            LoadConfig();
            LoadPlayerData();
            UpdatePermissions();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            ulong playerId = player.userID;
            if (!playerSettings.ContainsKey(playerId))
            {
                if (_config.ToggleParagliderDefault)
                {
                    playerSettings[playerId] = new PlayerSettings
                    {
                        ToggleParaglider = true
                    };
                }
                else
                {
                    {
                        playerSettings[playerId] = new PlayerSettings
                        {
                            ToggleParaglider = false
                        };
                    }
                }

            }

            UpdatePermissions();
        }

        private void LoadPlayerData()
        {
            playerData = Interface.Oxide.DataFileSystem.GetFile("Paraglider_playerdata");

            try
            {
                playerSettings = playerData.ReadObject<Dictionary<ulong, PlayerSettings>>();
            }
            catch
            {
                PrintWarning("Failed to load player data, creating new data file");
                playerSettings = new Dictionary<ulong, PlayerSettings>();
            }
        }

        private void SavePlayerData()
        {
            playerData.WriteObject(playerSettings);
        }

        private object CanMountEntity(BasePlayer player, BaseEntity entity)
        {

            if (entity is ParachuteSeat && HasPermission(player.userID, PermissionParaglide))
            {
                Parachute parachute = entity.GetParentEntity() as Parachute;

                if (playerParachuteMap.ContainsKey(player))
                {
                    BaseEntity smoketrail = playerParachuteMap[player];
                    if (smoketrail != null && !smoketrail.IsDestroyed)
                    {
                        smoketrail.Kill();
                    }
                    playerParachuteMap.Remove(player);
                }

                if (parachute != null)
                {
                    player.ChatMessage("<size=12><color=#517508>PARAGLIDER EQUIPPED</color></size>");

                    if (HasPermission(player.userID, PermissionBoostT1) || HasPermission(player.userID, PermissionBoostT2) || HasPermission(player.userID, PermissionBoostT3))
                    {
                        player.ChatMessage("<size=10><color=#a4bd6f><size=12><color=#517508>SHIFT</color></size> to use your <size=12><color=#517508>BOOSTERS!</size></color></color></size>");
                    }
                    if (HasPermission(player.userID, PermissionAfterburnerT1) || HasPermission(player.userID, PermissionAfterburnerT2) || HasPermission(player.userID, PermissionAfterburnerT3))
                    {
                        player.ChatMessage("<size=10><color=#a4bd6f><size=12><color=#517508>LEFT CLICK</color></size> to use your <size=12><color=#517508>AFTERBURNERS!</color></size></color></size>");
                    }
                    if (HasPermission(player.userID, PermissionLowerT1) || HasPermission(player.userID, PermissionLowerT2) || HasPermission(player.userID, PermissionLowerT3))
                    {
                        player.ChatMessage("<size=10><color=#a4bd6f><size=12><color=#517508>RIGHT CLICK</color></size> to quickly <size=12><color=#517508>DESCEND!</color></size></color></size>");
                    }
                    if (HasPermission(player.userID, PermissionUseFlares))
                    {
                        player.ChatMessage("<size=10><color=#a4bd6f><size=12><color=#517508>E</color></size> to evade SAMs with <size=12><color=#517508>FLARES!</color></size></color></size>");
                    }
                    if (HasPermission(player.userID, PermissionSmoke))
                    {
                        player.ChatMessage("<size=10><color=#a4bd6f><size=12><color=#517508>R</color></size> to toggle <size=12><color=#517508>SMOKE TRAIL!</color></size></color></size>");
                    }
                    parachute.ForwardTiltAcceleration = _config.UnboostedForwardTiltAcceleration;
                    parachute.TurnForce = _config.UnboostedTurnForce;
                }
            }
            return null;
        }

        private object OnSamSiteTarget(SamSite sam, BaseMountable mountable)
        {
            if (sam == null) return null;
            if (mountable == null) return null;
            BasePlayer player = GetMountedPlayer(mountable);

            if (player.IsValid() && IsDroppingFlares(player))
            {
                return true;
            }
            return null;
        }

        object OnPlayerRespawn(BasePlayer player, BasePlayer.SpawnPoint spawnPoint)
        {
            if (permission.UserHasPermission(player.UserIDString, PermissionSpawnfromJet))
            {
                if (!IsParachuteInWearContainer(player))
                {
                    GiveParachute(player);
                }

                JetDropPlayer(player);
            }
            return null;
        }

        object OnPlayerSleepEnded(BasePlayer player)
        {
            if (playerParachuteMap.ContainsKey(player))
            {
                BaseEntity smoketrail = playerParachuteMap[player];

                if (smoketrail != null && !smoketrail.IsDestroyed)
                {
                    smoketrail.Kill();
                }
                playerParachuteMap.Remove(player);
            }

            if (permission.UserHasPermission(player.UserIDString, PermissionSpawnWithChute) && !IsParachuteInWearContainer(player))
            {
                GiveParachute(player);
                NotifyPlayerControls(player);
            }

            return null;
        }

        object CanWearItem(PlayerInventory inventory, Item item, int targetSlot)
        {
            int targetPos = 7;
            BasePlayer player = inventory.containerWear?.playerOwner;

            if (player != null && targetPos == targetSlot && item.info.itemid == 602628465 && HasPermission(player.userID, PermissionParaglide))
            {
                NotifyPlayerControls(player);
            }

            return null;
        }




        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null) return;
            if (input == null) return;

            ulong playerId = player.userID;
            if (playerSettings.ContainsKey(playerId))
            {
                if (!playerSettings[playerId].ToggleParaglider) return;
            }

            float currentTime = Time.realtimeSinceStartup;

            if (HasPermission(player.userID, PermissionParaglide))
            {
                BaseMountable mount = player.GetMounted();
                if (mount != null && mount.name.Equals(ParachuteVehiclePrefab) == true)
                {
                    Parachute parachute = mount.gameObject.GetComponentInParent<Parachute>();

                    if (player.GetHeldEntity() != null) return;

                    if (input.IsDown(BUTTON.FORWARD)) //BOOSTER
                    {
                        if (HasPermission(player.userID, PermissionBoostT3) && parachute != null)
                        {
                            if (input.IsDown(BUTTON.SPRINT))
                            {
                                parachute.ForwardTiltAcceleration = _config.BoostedForwardTiltAccelerationT3;
                                parachute.TurnForce = _config.BoostedTurnForceT3;
                            }
                            else
                            {
                                parachute.ForwardTiltAcceleration = _config.UnboostedForwardTiltAcceleration;
                            }
                        }
                        else if (HasPermission(player.userID, PermissionBoostT2) && parachute != null)
                        {
                            if (input.IsDown(BUTTON.SPRINT))
                            {
                                parachute.ForwardTiltAcceleration = _config.BoostedForwardTiltAccelerationT2;
                                parachute.TurnForce = _config.BoostedTurnForceT2;
                            }
                            else
                            {
                                parachute.ForwardTiltAcceleration = _config.UnboostedForwardTiltAcceleration;
                            }
                        }
                        else if (HasPermission(player.userID, PermissionBoostT1) && parachute != null)
                        {
                            if (input.IsDown(BUTTON.SPRINT))
                            {
                                parachute.ForwardTiltAcceleration = _config.BoostedForwardTiltAccelerationT1;
                                parachute.TurnForce = _config.BoostedTurnForceT1;
                            }
                            else
                            {
                                parachute.ForwardTiltAcceleration = _config.UnboostedForwardTiltAcceleration;
                            }
                        }
                    }

                    if (input.IsDown(BUTTON.BACKWARD)) //BACKWARD BOOSTER
                    {
                        if (HasPermission(player.userID, PermissionBoostT3))
                        {
                            if (input.IsDown(BUTTON.SPRINT) && parachute != null)
                            {
                                parachute.BackInputForceMultiplier = _config.BoostedBackInputForceMultiplierT3;
                                parachute.TurnForce = _config.BoostedTurnForceT3;
                            }
                        }
                        else if (HasPermission(player.userID, PermissionBoostT2))
                        {
                            if (input.IsDown(BUTTON.SPRINT) && parachute != null)
                            {
                                parachute.BackInputForceMultiplier = _config.BoostedBackInputForceMultiplierT2;
                                parachute.TurnForce = _config.BoostedTurnForceT2;
                            }
                        }
                        else if (HasPermission(player.userID, PermissionBoostT1))
                        {
                            if (input.IsDown(BUTTON.SPRINT) && parachute != null)
                            {
                                parachute.BackInputForceMultiplier = _config.BoostedBackInputForceMultiplierT1;
                                parachute.TurnForce = _config.BoostedTurnForceT1;
                            }
                        }
                        else if (parachute != null)
                        {
                            parachute.BackInputForceMultiplier = _config.UnboostedBackInputForceMultiplier;
                        }
                    }

                    if (input.IsDown(BUTTON.FIRE_PRIMARY) && parachute != null) // AFTERBURNER
                    {
                        if (HasPermission(player.userID, PermissionAfterburnerT3)) //TIER 3
                        {
                            if (HasPermission(player.userID, PermissionNoFuelRequired))
                            {
                                PlayAfterburnEffect(player);

                                parachute.rigidBody.AddForce(Vector3.up * _config.upwardsForceT3, ForceMode.Impulse);
                                parachute.rigidBody.AddForce(parachute.transform.forward * _config.forwardsForceT3, ForceMode.Impulse);
                            }
                            else if (HasEnoughFuel(player.UserIDString))
                            {
                                ConsumeFuel(player.UserIDString, _config.FuelConsumption);
                                RefillFuelOverTime(player.UserIDString);
                                PlayAfterburnEffect(player);

                                parachute.rigidBody.AddForce(Vector3.up * _config.upwardsForceT3, ForceMode.Impulse);
                                parachute.rigidBody.AddForce(parachute.transform.forward * _config.forwardsForceT3, ForceMode.Impulse);

                            }
                            else if (!lastFuelEmptyMessageTime.ContainsKey(player.UserIDString) || (Time.realtimeSinceStartup - lastFuelEmptyMessageTime[player.UserIDString] >= 6f))
                            {
                                player.ChatMessage("<size=10><color=#a4bd6f>FUEL EMPTY!\nRefilling!</color></size>");
                                RefillFuelOverTime(player.UserIDString);
                                lastFuelEmptyMessageTime[player.UserIDString] = Time.realtimeSinceStartup;
                            }
                        }
                        else if (HasPermission(player.userID, PermissionAfterburnerT2)) //TIER 2
                        {
                            if (HasPermission(player.userID, PermissionNoFuelRequired))
                            {
                                PlayAfterburnEffect(player);

                                parachute.rigidBody.AddForce(Vector3.up * _config.upwardsForceT2, ForceMode.Impulse);
                                parachute.rigidBody.AddForce(parachute.transform.forward * _config.forwardsForceT2, ForceMode.Impulse);
                            }
                            else if (HasEnoughFuel(player.UserIDString))
                            {
                                ConsumeFuel(player.UserIDString, _config.FuelConsumption);
                                RefillFuelOverTime(player.UserIDString);
                                PlayAfterburnEffect(player);

                                parachute.rigidBody.AddForce(Vector3.up * _config.upwardsForceT2, ForceMode.Impulse);
                                parachute.rigidBody.AddForce(parachute.transform.forward * _config.forwardsForceT2, ForceMode.Impulse);
                            }
                            else if (!lastFuelEmptyMessageTime.ContainsKey(player.UserIDString) || (Time.realtimeSinceStartup - lastFuelEmptyMessageTime[player.UserIDString] >= 6f))
                            {
                                player.ChatMessage("<size=10><color=#a4bd6f>FUEL EMPTY!\nRefilling!</color></size>");
                                RefillFuelOverTime(player.UserIDString);
                                lastFuelEmptyMessageTime[player.UserIDString] = Time.realtimeSinceStartup;
                            }
                        }
                        else if (HasPermission(player.userID, PermissionAfterburnerT1)) //TIER 1
                        {
                            if (HasPermission(player.userID, PermissionNoFuelRequired))
                            {
                                PlayAfterburnEffect(player);

                                parachute.rigidBody.AddForce(Vector3.up * _config.upwardsForceT1, ForceMode.Impulse);
                                parachute.rigidBody.AddForce(parachute.transform.forward * _config.forwardsForceT1, ForceMode.Impulse);
                            }
                            else if (HasEnoughFuel(player.UserIDString))
                            {
                                ConsumeFuel(player.UserIDString, _config.FuelConsumption);
                                RefillFuelOverTime(player.UserIDString);
                                PlayAfterburnEffect(player);

                                parachute.rigidBody.AddForce(Vector3.up * _config.upwardsForceT1, ForceMode.Impulse);
                                parachute.rigidBody.AddForce(parachute.transform.forward * _config.forwardsForceT1, ForceMode.Impulse);
                            }
                            else if (!lastFuelEmptyMessageTime.ContainsKey(player.UserIDString) || (Time.realtimeSinceStartup - lastFuelEmptyMessageTime[player.UserIDString] >= 6f))
                            {
                                player.ChatMessage("<size=10><color=#a4bd6f>FUEL EMPTY!\nRefilling!</color></size>");
                                RefillFuelOverTime(player.UserIDString);
                                lastFuelEmptyMessageTime[player.UserIDString] = Time.realtimeSinceStartup;
                            }
                        }
                    }

                    if (input.WasJustPressed(BUTTON.USE)) //FLARES
                    {
                        if (HasPermission(player.userID, PermissionUseFlares))
                        {
                            if (!HasPermission(player.userID, PermissionFreeFlares))
                            {
                                var inventory = player.inventory;
                                bool hasFlare = false;

                                List<Item> items = Pool.GetList<Item>();
                                inventory.GetAllItems(items);

                                foreach (var flareitem in items)
                                {
                                    if (flareitem.info.shortname == "flare")
                                    {
                                        hasFlare = true;
                                        flareToConsume = flareitem;
                                        break;
                                    }
                                }

                                Pool.FreeList(ref items);

                                if (!hasFlare)
                                {
                                    player.ChatMessage("<color=#bcb6b3>You don't have any flares.</color>");
                                    return;
                                }

                                if (flareToConsume != null)
                                {
                                    flareToConsume.UseItem(1);
                                }
                            }

                            playerLastFlaresTime[player.UserIDString] = Time.realtimeSinceStartup;

                            Vector3 effectPosition = player.transform.position - (player.transform.forward * 5f) - (player.transform.up * 3f);
                            Quaternion effectRotation = player.transform.rotation;
                            Vector3 effectDirection = effectRotation * Vector3.forward;

                            Effect.server.Run(FlareEffect, effectPosition, effectDirection);


                            if (!lastFlareMessageTime.ContainsKey(player.UserIDString) || (Time.realtimeSinceStartup - lastFlareMessageTime[player.UserIDString] >= _config.flaresCooldownDuration))
                            {
                                player.ChatMessage("<size=10><color=#a4bd6f>FLARES DEPLOYED</color>\n<color=#bcb6b3>SAM TARGETING DISABLED\n" +
                                    "for " + _config.flaresCooldownDuration.ToString() + " seconds</size></color>");
                                lastFlareMessageTime[player.UserIDString] = Time.realtimeSinceStartup;
                            }
                        }
                    }

                    if (input.IsDown(BUTTON.FIRE_SECONDARY) && parachute != null) //LOWER
                    {
                        if (HasPermission(player.userID, PermissionLowerT3))
                        {
                            if (input.IsDown(BUTTON.SPRINT) && parachute != null && HasPermission(player.userID, PermissionBoostT3))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.boostDownwardsForceT3, ForceMode.Impulse);
                            }
                            else if (input.IsDown(BUTTON.SPRINT) && parachute != null && HasPermission(player.userID, PermissionBoostT2))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.boostDownwardsForceT2, ForceMode.Impulse);
                            }
                            else if (input.IsDown(BUTTON.SPRINT) && parachute != null && HasPermission(player.userID, PermissionBoostT1))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.boostDownwardsForceT1, ForceMode.Impulse);
                            }

                            if (input.IsDown(BUTTON.FIRE_PRIMARY) && parachute != null && HasPermission(player.userID, PermissionAfterburnerT3))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.afterburnDownwardsForceT3, ForceMode.Impulse);
                            }
                            else if (input.IsDown(BUTTON.FIRE_PRIMARY) && parachute != null && HasPermission(player.userID, PermissionAfterburnerT2))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.afterburnDownwardsForceT2, ForceMode.Impulse);
                            }
                            else if (input.IsDown(BUTTON.FIRE_PRIMARY) && parachute != null && HasPermission(player.userID, PermissionAfterburnerT1))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.afterburnDownwardsForceT1, ForceMode.Impulse);
                            }

                            else if (parachute != null)
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.downwardsForceT3, ForceMode.Impulse);
                            }
                        }
                        else if (HasPermission(player.userID, PermissionLowerT2))
                        {
                            if (input.IsDown(BUTTON.SPRINT) && parachute != null && HasPermission(player.userID, PermissionBoostT3))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.boostDownwardsForceT3, ForceMode.Impulse);
                            }
                            else if (input.IsDown(BUTTON.SPRINT) && parachute != null && HasPermission(player.userID, PermissionBoostT2))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.boostDownwardsForceT2, ForceMode.Impulse);
                            }
                            else if (input.IsDown(BUTTON.SPRINT) && parachute != null && HasPermission(player.userID, PermissionBoostT1))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.boostDownwardsForceT1, ForceMode.Impulse);
                            }

                            if (input.IsDown(BUTTON.FIRE_PRIMARY) && parachute != null && HasPermission(player.userID, PermissionAfterburnerT3))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.afterburnDownwardsForceT3, ForceMode.Impulse);
                            }
                            else if (input.IsDown(BUTTON.FIRE_PRIMARY) && parachute != null && HasPermission(player.userID, PermissionAfterburnerT2))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.afterburnDownwardsForceT2, ForceMode.Impulse);
                            }
                            else if (input.IsDown(BUTTON.FIRE_PRIMARY) && parachute != null && HasPermission(player.userID, PermissionAfterburnerT1))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.afterburnDownwardsForceT1, ForceMode.Impulse);
                            }

                            else if (parachute != null)
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.downwardsForceT2, ForceMode.Impulse);
                            }
                        }
                        else if (HasPermission(player.userID, PermissionLowerT1))
                        {
                            if (input.IsDown(BUTTON.SPRINT) && parachute != null && HasPermission(player.userID, PermissionBoostT3))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.boostDownwardsForceT3, ForceMode.Impulse);
                            }
                            else if (input.IsDown(BUTTON.SPRINT) && parachute != null && HasPermission(player.userID, PermissionBoostT2))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.boostDownwardsForceT2, ForceMode.Impulse);
                            }
                            else if (input.IsDown(BUTTON.SPRINT) && parachute != null && HasPermission(player.userID, PermissionBoostT1))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.boostDownwardsForceT1, ForceMode.Impulse);
                            }

                            if (input.IsDown(BUTTON.FIRE_PRIMARY) && parachute != null && HasPermission(player.userID, PermissionAfterburnerT3))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.afterburnDownwardsForceT3, ForceMode.Impulse);
                            }
                            else if (input.IsDown(BUTTON.FIRE_PRIMARY) && parachute != null && HasPermission(player.userID, PermissionAfterburnerT2))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.afterburnDownwardsForceT2, ForceMode.Impulse);
                            }
                            else if (input.IsDown(BUTTON.FIRE_PRIMARY) && parachute != null && HasPermission(player.userID, PermissionAfterburnerT1))
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.afterburnDownwardsForceT1, ForceMode.Impulse);
                            }

                            else if (parachute != null)
                            {
                                parachute.rigidBody.AddForce(Vector3.down * _config.downwardsForceT1, ForceMode.Impulse);
                            }
                        }
                    }

                    if (input.WasJustPressed(BUTTON.RELOAD) && parachute != null) //TOGGLE SMOKE
                    {
                        if (HasPermission(player.userID, PermissionSmoke))
                        {
                            if (playerParachuteMap.ContainsKey(player))
                            {
                                BaseEntity smoketrail = playerParachuteMap[player];

                                if (smoketrail != null && !smoketrail.IsDestroyed)
                                {
                                    smoketrail.Kill();
                                }
                                playerParachuteMap.Remove(player);
                                player.ChatMessage("<size=10><color=#cf4bde>SMOKE TRAIL DISABLED</color></size>");
                            }
                            else if (!playerParachuteMap.ContainsKey(player))
                            {
                                SpawnSupplySignal(player, parachute, SupplySignalPath);
                            }
                        }
                    }
                }

                if (input.WasJustPressed(BUTTON.FIRE_THIRD))
                {
                    if (!IsParachuteInWearContainer(player))
                    {
                        return;
                    }

                    if (HasPermission(player.userID, PermissionLaunchT1) ||
                        HasPermission(player.userID, PermissionLaunchT2) ||
                        HasPermission(player.userID, PermissionLaunchT3))
                    {
                        if (IsRaidBlocked(player) || IsCombatBlocked(player)) return;

                        if (!permission.UserHasPermission(player.UserIDString, PermissionIgnoreNoEscape) && !player.CanBuild() && _config.preventLaunchWhenBuildBlocked)
                        {
                            player.ChatMessage($"<size=10><color=#a4bd6f>Building Blocked!</color></size>");
                            return;
                        }

                        if (!lastCancelLaunchTime.ContainsKey(player.UserIDString) || (Time.realtimeSinceStartup - lastCancelLaunchTime[player.UserIDString] >= 0.75f))
                        {
                            if (permission.UserHasPermission(player.UserIDString, PermissionLaunchT3)) //TIER 3
                            {
                                if (!lastLaunchTime.ContainsKey(player.UserIDString) || (Time.realtimeSinceStartup - lastLaunchTime[player.UserIDString] >= _config.launchCooldownT3) || permission.UserHasPermission(player.UserIDString, PermissionNoLaunchCooldown))
                                {
                                    player.ChatMessage("<color=#517508>LAUNCHING PARAGLIDER</color>\n<size=10><color=#a4bd6f>[RELEASE TO CANCEL]\nMake sure you have space!</color></size>");
                                    launchingPlayers[player.UserIDString] = null;

                                    var count = (int)_config.CountdownTimerT3;
                                    CountdownToLaunch(player, count, input);
                                }
                                else
                                {
                                    float timeElapsed = Time.realtimeSinceStartup - lastLaunchTime[player.UserIDString];
                                    float remainingTime = _config.launchCooldownT3 - timeElapsed;

                                    if (remainingTime < 0)
                                    {
                                        remainingTime = 0;
                                    }

                                    player.ChatMessage($"<size=10><color=#a4bd6f>Launch Cooldown remaining: {remainingTime:F1} seconds</color></size>");
                                }
                            }
                            else if (permission.UserHasPermission(player.UserIDString, PermissionLaunchT2)) //TIER 2
                            {
                                if (!lastLaunchTime.ContainsKey(player.UserIDString) ||
                                    (Time.realtimeSinceStartup - lastLaunchTime[player.UserIDString] >= _config.launchCooldownT2) ||
                                    permission.UserHasPermission(player.UserIDString, PermissionNoLaunchCooldown))
                                {

                                    player.ChatMessage("<color=#517508>LAUNCHING PARAGLIDER</color>\n<size=10><color=#a4bd6f>[RELEASE TO CANCEL]\nMake sure you have space!</color></size>");
                                    launchingPlayers[player.UserIDString] = null;

                                    var count = (int)_config.CountdownTimerT2;
                                    CountdownToLaunch(player, count, input);
                                }
                                else
                                {
                                    float timeElapsed = Time.realtimeSinceStartup - lastLaunchTime[player.UserIDString];
                                    float remainingTime = _config.launchCooldownT2 - timeElapsed;

                                    if (remainingTime < 0)
                                    {
                                        remainingTime = 0;
                                    }

                                    player.ChatMessage($"<size=10><color=#a4bd6f>Launch Cooldown remaining: {remainingTime:F1} seconds</color></size>");
                                }
                            }
                            else if (permission.UserHasPermission(player.UserIDString, PermissionLaunchT1)) //TIER 1
                            {
                                if (!lastLaunchTime.ContainsKey(player.UserIDString) ||
                                    (Time.realtimeSinceStartup - lastLaunchTime[player.UserIDString] >= _config.launchCooldownT1) ||
                                    permission.UserHasPermission(player.UserIDString, PermissionNoLaunchCooldown))
                                {

                                    player.ChatMessage("<color=#517508>LAUNCHING PARAGLIDER</color>\n<size=10><color=#a4bd6f>[RELEASE TO CANCEL]\nMake sure you have space!</color></size>");
                                    launchingPlayers[player.UserIDString] = null;

                                    var count = (int)_config.CountdownTimerT1;
                                    CountdownToLaunch(player, count, input);
                                }
                                else
                                {
                                    float timeElapsed = Time.realtimeSinceStartup - lastLaunchTime[player.UserIDString];
                                    float remainingTime = _config.launchCooldownT1 - timeElapsed;

                                    if (remainingTime < 0)
                                    {
                                        remainingTime = 0;
                                    }

                                    player.ChatMessage($"<size=10><color=#a4bd6f>Launch Cooldown remaining: {remainingTime:F1} seconds</color></size>");
                                }
                            }
                        }
                    }
                }

                if (input.WasJustReleased(BUTTON.FIRE_THIRD))
                {
                    if (launchingPlayers.ContainsKey(player.UserIDString))
                    {
                        launchingPlayers.Remove(player.UserIDString);
                        lastCancelLaunchTime[player.UserIDString] = Time.realtimeSinceStartup;
                    }
                }
            }
        }




        #endregion

        #region Methods

        private void GiveParachute(BasePlayer player)
        {
            Item parachute = ItemManager.CreateByItemID(ItemManager.FindItemDefinition("parachute").itemid, 1, 0);
            parachute.SetParent(player.inventory.containerWear);
            parachute.position = 7;
        }

        private void NotifyPlayerControls(BasePlayer player)
        {
            float currentTime = Time.realtimeSinceStartup;

            if (permission.UserHasPermission(player.UserIDString, PermissionParaglide))
            {
                if (!lastEquipMessageTime.ContainsKey(player.UserIDString) || (Time.realtimeSinceStartup - lastEquipMessageTime[player.UserIDString] >= 6f))
                {
                    player.ChatMessage("<size=12><color=#517508>PARAGLIDER EQUIPPED</color></size>");
                    lastEquipMessageTime[player.UserIDString] = Time.realtimeSinceStartup;

                    if (HasPermission(player.userID, PermissionLaunchT1) ||
                        HasPermission(player.userID, PermissionLaunchT2) ||
                        HasPermission(player.userID, PermissionLaunchT3))
                    {
                        if (!lastLaunchMessageTime.ContainsKey(player.UserIDString) || (Time.realtimeSinceStartup - lastLaunchMessageTime[player.UserIDString] >= 6f))
                        {
                            player.ChatMessage("<size=10><color=#a4bd6f>Hold <size=12><color=#517508>MIDDLE MOUSE</color></size> to <size=12><color=#517508>LAUNCH!</size></color></color>\n" +
                           "<size=10><color=#a4bd6f>Make sure you have space!</color></size>");
                            lastLaunchMessageTime[player.UserIDString] = Time.realtimeSinceStartup;
                        }
                    }

                    if (permission.UserHasPermission(player.UserIDString, PermissionJetDropT1) ||
                        permission.UserHasPermission(player.UserIDString, PermissionJetDropT2) ||
                        permission.UserHasPermission(player.UserIDString, PermissionJetDropT3))
                    {
                        if (!lastJetMessageTime.ContainsKey(player.UserIDString) || (Time.realtimeSinceStartup - lastJetMessageTime[player.UserIDString] >= 6f))
                        {
                            player.ChatMessage("<size=10><color=#a4bd6f>Type <size=12><color=#517508>/drop</color></size> to get dropped by a <size=12><color=#517508>JET!</size></color></color></size>");
                            lastJetMessageTime[player.UserIDString] = Time.realtimeSinceStartup;
                        }
                    }
                }
            }
        }

        private void CountdownToLaunch(BasePlayer player, int count, InputState input)
        {
            if (!launchingPlayers.ContainsKey(player.UserIDString))
            {
                lastCancelLaunchTime[player.UserIDString] = Time.realtimeSinceStartup;
                return;
            }

            if (!input.IsDown(BUTTON.FIRE_THIRD))
            {
                launchingPlayers.Remove(player.UserIDString);
                lastCancelLaunchTime[player.UserIDString] = Time.realtimeSinceStartup;
                return;
            }

            if (count > 0)
            {
                if (_config.launchEffect)
                {
                    Vector3 preLaunchEffectPosition = player.transform.position;
                    Quaternion preLaunchEffectRotation = player.transform.rotation;
                    Vector3 preLaunchEffectDirection = preLaunchEffectRotation * Vector3.forward;

                    Effect.server.Run(PreLaunchEffect, preLaunchEffectPosition, preLaunchEffectDirection);
                    timer.Once(0.3f, () => Effect.server.Run(PreLaunchEffect, preLaunchEffectPosition, preLaunchEffectDirection));
                    timer.Once(0.6f, () => Effect.server.Run(PreLaunchEffect, preLaunchEffectPosition, preLaunchEffectDirection));
                    timer.Once(0.9f, () => Effect.server.Run(PreLaunchEffect, preLaunchEffectPosition, preLaunchEffectDirection));
                }

                player.ChatMessage($"<size=10><color=#ff0000>{count}</color></size>");
                timer.Once(1.0f, () => CountdownToLaunch(player, count - 1, input));
            }

            else if (count <= 0)
            {
                LaunchPlayer(player);
                launchingPlayers.Remove(player.UserIDString);
            }
        }

        private void LaunchByCommand(BasePlayer player, int count)
        {
            if (!launchingPlayers.ContainsKey(player.UserIDString))
            {
                lastCancelLaunchTime[player.UserIDString] = Time.realtimeSinceStartup;
                return;
            }

            if (count > 0)
            {
                if (_config.launchEffect)
                {
                    Vector3 preLaunchEffectPosition = player.transform.position;
                    Quaternion preLaunchEffectRotation = player.transform.rotation;
                    Vector3 preLaunchEffectDirection = preLaunchEffectRotation * Vector3.forward;

                    Effect.server.Run(PreLaunchEffect, preLaunchEffectPosition, preLaunchEffectDirection);
                    timer.Once(0.3f, () => Effect.server.Run(PreLaunchEffect, preLaunchEffectPosition, preLaunchEffectDirection));
                    timer.Once(0.6f, () => Effect.server.Run(PreLaunchEffect, preLaunchEffectPosition, preLaunchEffectDirection));
                    timer.Once(0.9f, () => Effect.server.Run(PreLaunchEffect, preLaunchEffectPosition, preLaunchEffectDirection));
                }

                player.ChatMessage($"<size=10><color=#ff0000>{count}</color></size>");
                timer.Once(1.0f, () => LaunchByCommand(player, count - 1));
            }

            else if (count <= 0)
            {
                LaunchPlayer(player);
                launchingPlayers.Remove(player.UserIDString);
            }
        }

        private void CountdownToDrop(BasePlayer player, BaseEntity chairEntity, int count)
        {
            BaseMountable mountable = player.GetMounted();

            if (mountable == null || mountable != chairEntity)
                return;

            if (count <= 0)
            {
                mountable.DismountPlayer(player);
            }
            else
            {
                player.ChatMessage($"<size=10><color=#ff0000>{count}</color></size>");
                timer.Once(1.0f, () => CountdownToDrop(player, chairEntity, count - 1));
            }
        }

        [ChatCommand("Launch")]
        private void ChatCmdLaunch(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionLaunchCommand))
            {
                player.ChatMessage("<size=10><color=#a4bd6f>No Permission.</color></size>");
            }

            if (!IsParachuteInWearContainer(player))
            {
                player.ChatMessage("<size=10><color=#a4bd6f>No Paraglider equipped.</color></size>");
                return;
            }

            if (HasPermission(player.userID, PermissionLaunchT1) ||
                HasPermission(player.userID, PermissionLaunchT2) ||
                HasPermission(player.userID, PermissionLaunchT3))
            {
                if (IsRaidBlocked(player) || IsCombatBlocked(player)) return;

                if (!permission.UserHasPermission(player.UserIDString, PermissionIgnoreNoEscape) && !player.CanBuild() && _config.preventLaunchWhenBuildBlocked)
                {
                    player.ChatMessage($"<size=10><color=#a4bd6f>Building Blocked!</color></size>");
                    return;
                }

                if (!lastCancelLaunchTime.ContainsKey(player.UserIDString) || (Time.realtimeSinceStartup - lastCancelLaunchTime[player.UserIDString] >= 0.75f))
                {
                    if (permission.UserHasPermission(player.UserIDString, PermissionLaunchT3)) //TIER 3
                    {
                        if (!lastLaunchTime.ContainsKey(player.UserIDString) || (Time.realtimeSinceStartup - lastLaunchTime[player.UserIDString] >= _config.launchCooldownT3) || permission.UserHasPermission(player.UserIDString, PermissionNoLaunchCooldown))
                        {
                            player.ChatMessage("<color=#517508>LAUNCHING PARAGLIDER</color>\nMake sure you have space!</color></size>");
                            launchingPlayers[player.UserIDString] = null;

                            var count = (int)_config.CountdownTimerT3;
                            LaunchByCommand(player, count);
                        }
                        else
                        {
                            float timeElapsed = Time.realtimeSinceStartup - lastLaunchTime[player.UserIDString];
                            float remainingTime = _config.launchCooldownT3 - timeElapsed;

                            if (remainingTime < 0)
                            {
                                remainingTime = 0;
                            }

                            player.ChatMessage($"<size=10><color=#a4bd6f>Launch Cooldown remaining: {remainingTime:F1} seconds</color></size>");
                        }
                    }
                    else if (permission.UserHasPermission(player.UserIDString, PermissionLaunchT2)) //TIER 2
                    {
                        if (!lastLaunchTime.ContainsKey(player.UserIDString) ||
                            (Time.realtimeSinceStartup - lastLaunchTime[player.UserIDString] >= _config.launchCooldownT2) ||
                            permission.UserHasPermission(player.UserIDString, PermissionNoLaunchCooldown))
                        {

                            player.ChatMessage("<color=#517508>LAUNCHING PARAGLIDER</color>\n<size=10><color=#a4bd6f>[RELEASE TO CANCEL]\nMake sure you have space!</color></size>");
                            launchingPlayers[player.UserIDString] = null;

                            var count = (int)_config.CountdownTimerT2;
                            LaunchByCommand(player, count);
                        }
                        else
                        {
                            float timeElapsed = Time.realtimeSinceStartup - lastLaunchTime[player.UserIDString];
                            float remainingTime = _config.launchCooldownT2 - timeElapsed;

                            if (remainingTime < 0)
                            {
                                remainingTime = 0;
                            }

                            player.ChatMessage($"<size=10><color=#a4bd6f>Launch Cooldown remaining: {remainingTime:F1} seconds</color></size>");
                        }
                    }
                    else if (permission.UserHasPermission(player.UserIDString, PermissionLaunchT1)) //TIER 1
                    {
                        if (!lastLaunchTime.ContainsKey(player.UserIDString) ||
                            (Time.realtimeSinceStartup - lastLaunchTime[player.UserIDString] >= _config.launchCooldownT1) ||
                            permission.UserHasPermission(player.UserIDString, PermissionNoLaunchCooldown))
                        {

                            player.ChatMessage("<color=#517508>LAUNCHING PARAGLIDER</color>\n<size=10><color=#a4bd6f>[RELEASE TO CANCEL]\nMake sure you have space!</color></size>");
                            launchingPlayers[player.UserIDString] = null;

                            var count = (int)_config.CountdownTimerT1;
                            LaunchByCommand(player, count);
                        }
                        else
                        {
                            float timeElapsed = Time.realtimeSinceStartup - lastLaunchTime[player.UserIDString];
                            float remainingTime = _config.launchCooldownT1 - timeElapsed;

                            if (remainingTime < 0)
                            {
                                remainingTime = 0;
                            }

                            player.ChatMessage($"<size=10><color=#a4bd6f>Launch Cooldown remaining: {remainingTime:F1} seconds</color></size>");
                        }
                    }
                }
            }
        }

        [ChatCommand("drop")]
        private void ChatCmdDrop(BasePlayer player)
        {
            float currentTime = Time.realtimeSinceStartup;

            if (permission.UserHasPermission(player.UserIDString, PermissionJetDropT3) || 
                permission.UserHasPermission(player.UserIDString, PermissionJetDropT2) || 
                permission.UserHasPermission(player.UserIDString, PermissionJetDropT1))
            {
                if (!IsParachuteInWearContainer(player))
                {
                    GiveParachute(player);
                }

                if (!permission.UserHasPermission(player.UserIDString, PermissionIgnoreNoEscape) && !player.CanBuild() && _config.preventDropWhenBuildBlocked)
                {
                    player.ChatMessage($"<size=10><color=#a4bd6f>Building Blocked!</color></size>");
                    return;
                }

                if (IsRaidBlocked(player) || IsCombatBlocked(player)) return;

                if (permission.UserHasPermission(player.UserIDString, PermissionJetDropT3))
                {
                    if (!lastJetDropTime.ContainsKey(player.UserIDString) || (Time.realtimeSinceStartup - lastJetDropTime[player.UserIDString] >= _config.jetDropCooldownT3) || permission.UserHasPermission(player.UserIDString, PermissionNoJetDropCooldown))
                    {
                        JetDropPlayer(player);
                    }
                    else
                    {
                        float timeElapsed = Time.realtimeSinceStartup - lastJetDropTime[player.UserIDString];
                        float remainingTime = _config.jetDropCooldownT3 - timeElapsed;

                        if (remainingTime < 0)
                        {
                            remainingTime = 0;
                        }

                        player.ChatMessage($"<size=10><color=#a4bd6f>Jet Drop on cooldown. Cooldown remaining: {remainingTime:F1} seconds</color></size>");
                    }
                }
                else if (permission.UserHasPermission(player.UserIDString, PermissionJetDropT2))
                {
                    if (!lastJetDropTime.ContainsKey(player.UserIDString) || (Time.realtimeSinceStartup - lastJetDropTime[player.UserIDString] >= _config.jetDropCooldownT2) || permission.UserHasPermission(player.UserIDString, PermissionNoJetDropCooldown))
                    {
                        JetDropPlayer(player);
                    }
                    else
                    {
                        float timeElapsed = Time.realtimeSinceStartup - lastJetDropTime[player.UserIDString];
                        float remainingTime = _config.jetDropCooldownT2 - timeElapsed;

                        if (remainingTime < 0)
                        {
                            remainingTime = 0;
                        }

                        player.ChatMessage($"<size=10><color=#a4bd6f>Jet Drop on cooldown. Cooldown remaining: {remainingTime:F1} seconds</color></size>");
                    }
                }
                else if (permission.UserHasPermission(player.UserIDString, PermissionJetDropT1))
                {
                    if (!lastJetDropTime.ContainsKey(player.UserIDString) || (Time.realtimeSinceStartup - lastJetDropTime[player.UserIDString] >= _config.jetDropCooldownT1) || permission.UserHasPermission(player.UserIDString, PermissionNoJetDropCooldown))
                    {
                        JetDropPlayer(player);
                    }
                    else
                    {
                        float timeElapsed = Time.realtimeSinceStartup - lastJetDropTime[player.UserIDString];
                        float remainingTime = _config.jetDropCooldownT1 - timeElapsed;

                        if (remainingTime < 0)
                        {
                            remainingTime = 0;
                        }

                        player.ChatMessage($"<size=10><color=#a4bd6f>Jet Drop on cooldown. Cooldown remaining: {remainingTime:F1} seconds</color></size>");
                    }
                }
            }
        }

        [ChatCommand("toggleglider")]
        private void ChatCmdToggleGlider(BasePlayer player)
        {
            ulong playerId = player.userID;
            if (!playerSettings.ContainsKey(playerId))
            {
                if (_config.ToggleParagliderDefault)
                {
                    playerSettings[playerId] = new PlayerSettings
                    {
                        ToggleParaglider = true
                    };
                    SavePlayerData();
                    player.ChatMessage("<size=10><color=#517508>Paraglider Enabled</color></size>");
                }
                else
                {
                    playerSettings[playerId] = new PlayerSettings
                    {
                        ToggleParaglider = false
                    };
                    SavePlayerData();
                    player.ChatMessage("<size=10><color=#517508>Paraglider Disabled</color></size>");
                }

                return;
            } 
            
            if (!playerSettings[playerId].ToggleParaglider)
            {
                playerSettings[playerId].ToggleParaglider = true;
                SavePlayerData();
                player.ChatMessage("<size=10><color=#517508>Paraglider Enabled</color></size>");
                return;
            }

            if (playerSettings[playerId].ToggleParaglider)
            {
                playerSettings[playerId].ToggleParaglider = false;
                SavePlayerData();
                player.ChatMessage("<size=10><color=#517508>Paraglider Disabled</color></size>");
                return;
            }

        }

        private void JetDropPlayer(BasePlayer player)
        {
            lastJetDropTime[player.UserIDString] = Time.realtimeSinceStartup;

            var jetPrefab = "assets/scripts/entity/misc/f15/f15e.prefab";
            var chairPrefab = "assets/bundled/prefabs/static/chair.invisible.static.prefab";

            Vector3 targetPosition = player.transform.position;
            float additionalHeight = 300f;
            Vector3 spawnPosition = CalculateSpawnPosition(targetPosition, additionalHeight);

            var jet = (F15)GameManager.server.CreateEntity(jetPrefab, spawnPosition);
            if (jet == null) return;

            jet.Spawn();
            spawnedJet = jet;
            jet.movePosition = targetPosition;

            chairEntity = GameManager.server.CreateEntity(chairPrefab, Vector3.zero, Quaternion.identity);
            chairEntity.Spawn();
            chairEntity.SetParent(jet);

            var chairLocalPosition = new Vector3(0, 1.0f, 5f);
            chairEntity.transform.SetLocalPositionAndRotation(chairLocalPosition, jet.transform.localRotation);

            timer.Once(1f, () =>
            {
                player.EndSleeping();

                chairEntity.GetComponent<BaseMountable>().MountPlayer(player);

                player.ChatMessage($"<color=#517508>PARAGLIDER READY</color>\n<size=10><color=#a4bd6f>[JUMP TO DEPLOY]</color>\n\n<color=#ff0000>AUTO-DEPLOYING IN:</color></size>");

                CountdownToDrop(player, chairEntity, 10);
            });

            timer.Once(15f, () =>
            {
                if (jet != null && !jet.IsDestroyed)
                {
                    jet.Kill();
                    jet = null;
                }

                if (chairEntity != null && !chairEntity.IsDestroyed)
                {
                    chairEntity.Kill();
                    chairEntity = null;
                }
            });
        }

        private void LaunchPlayer(BasePlayer player)
        {
            lastLaunchTime[player.UserIDString] = Time.realtimeSinceStartup;

            var rocketPosition = player.transform.localPosition + new Vector3(0f, .75f, -.5f);
            var rocketPrefab = "assets/prefabs/ammo/rocket/rocket_smoke.prefab";
            spawnedRocket = GameManager.server.CreateEntity(rocketPrefab, rocketPosition, Quaternion.identity);

            Vector3 playerPosition = player.transform.position;
            Vector3 playerViewDirection = player.eyes.HeadForward();
            Vector3 velocityDirection = playerViewDirection.normalized;

            if (spawnedRocket == null) return;

            ServerProjectile projectile;
            if (spawnedRocket.TryGetComponent(out projectile))
                if (projectile == null) return;

            if (permission.UserHasPermission(player.UserIDString, PermissionLaunchT3))
            {
                projectile.InitializeVelocity(velocityDirection * _config.launchVelocityT3);
            } //TIER 3 Velocity
            else if (permission.UserHasPermission(player.UserIDString, PermissionLaunchT2))
            {
                projectile.InitializeVelocity(velocityDirection * _config.launchVelocityT2);
            } //TIER 2 Velocity
            else if (permission.UserHasPermission(player.UserIDString, PermissionLaunchT1))
            {
                projectile.InitializeVelocity(velocityDirection * _config.launchVelocityT1);
            } //TIER 1 Velocity

            spawnedRocket.Spawn();

            var chairPrefab = "assets/bundled/prefabs/static/chair.invisible.static.prefab";
            chairEntity = GameManager.server.CreateEntity(chairPrefab, Vector3.zero, Quaternion.identity);
            chairEntity.Spawn();

            chairEntity.SetParent(spawnedRocket);

            var chairLocalPosition = chairEntity.transform.localPosition + new Vector3(0f, 0.5f, 0.5f);
            var chairLocalRotation = spawnedRocket.transform.rotation;
            chairEntity.transform.localPosition = chairLocalPosition;

            chairEntity.GetComponent<BaseMountable>().MountPlayer(player);

            string LaunchEffect = "assets/prefabs/weapons/homingmissilelauncher/effects/attack.prefab";
            Vector3 launchEffectPosition = player.transform.position - (player.transform.forward * .5f) - (player.transform.up * .5f);
            Quaternion launchEffectRotation = player.transform.rotation;
            Vector3 launchEffectDirection = launchEffectRotation * Vector3.forward;

            Effect.server.Run(LaunchEffect, launchEffectPosition, launchEffectDirection);

            BaseMountable mountable = player.GetMounted();

            if (permission.UserHasPermission(player.UserIDString, PermissionLaunchT3)) //TIER 3 Duration
            {
                timer.Once(_config.launchDurationT3, () =>
                {
                    if (mountable == null || mountable != chairEntity)
                        return;

                    mountable.DismountPlayer(player);

                    Item slot = player.inventory.containerWear.GetSlot(7);
                    if (slot == null || !(slot.conditionNormalized > 0f) || slot.isBroken || !slot.info.TryGetComponent<ItemModParachute>(out var component))
                    {
                        return;
                    }

                    //Auto-mount player to parachute after launch
                    if (_config.AutoDeployGlider)
                    {
                        timer.Once(.5f, () =>
                        {
                            Parachute parachute = GameManager.server.CreateEntity(component.ParachuteVehiclePrefab.resourcePath, player.transform.position, player.eyes.rotation) as Parachute;
                            if (parachute != null)
                            {
                                parachute.skinID = slot.skin;
                                parachute.Spawn();
                                parachute.SetHealth(parachute.MaxHealth() * slot.conditionNormalized);
                                parachute.AttemptMount(player);
                                if (player.isMounted)
                                {
                                    slot.Remove();
                                    ItemManager.DoRemoves();
                                }
                                else
                                {
                                    parachute.Kill();
                                }
                            }
                        });
                    }

                    if (spawnedRocket != null && !spawnedRocket.IsDestroyed)
                    {
                        spawnedRocket.Kill();
                    }

                    if (chairEntity != null && !chairEntity.IsDestroyed)
                    {
                        chairEntity.Kill();
                    }
                });
            }
            else if (permission.UserHasPermission(player.UserIDString, PermissionLaunchT2)) //TIER 2 Duration
            {
                timer.Once(_config.launchDurationT2, () =>
                {
                    if (mountable == null || mountable != chairEntity)
                        return;

                    mountable.DismountPlayer(player);

                    Item slot = player.inventory.containerWear.GetSlot(7);
                    if (slot == null || !(slot.conditionNormalized > 0f) || slot.isBroken || !slot.info.TryGetComponent<ItemModParachute>(out var component))
                    {
                        return;
                    }

                    //Auto-mount player to parachute after launch
                    if (_config.AutoDeployGlider)
                    {
                        Parachute parachute = GameManager.server.CreateEntity(component.ParachuteVehiclePrefab.resourcePath, player.transform.position, player.eyes.rotation) as Parachute;
                        if (parachute != null)
                        {
                            parachute.skinID = slot.skin;
                            parachute.Spawn();
                            parachute.SetHealth(parachute.MaxHealth() * slot.conditionNormalized);
                            parachute.AttemptMount(player);
                            if (player.isMounted)
                            {
                                slot.Remove();
                                ItemManager.DoRemoves();
                            }
                            else
                            {
                                parachute.Kill();
                            }
                        }
                    }

                    if (spawnedRocket != null && !spawnedRocket.IsDestroyed)
                    {
                        spawnedRocket.Kill();
                    }

                    if (chairEntity != null && !chairEntity.IsDestroyed)
                    {
                        chairEntity.Kill();
                    }
                });
            }
            else if (permission.UserHasPermission(player.UserIDString, PermissionLaunchT1)) //TIER 1 Duration
            {
                timer.Once(_config.launchDurationT1, () =>
                {
                    if (mountable == null || mountable != chairEntity)
                        return;

                    mountable.DismountPlayer(player);

                    Item slot = player.inventory.containerWear.GetSlot(7);
                    if (slot == null || !(slot.conditionNormalized > 0f) || slot.isBroken || !slot.info.TryGetComponent<ItemModParachute>(out var component))
                    {
                        return;
                    }

                    //Auto-mount player to parachute after launch
                    if (_config.AutoDeployGlider)
                    {
                        Parachute parachute = GameManager.server.CreateEntity(component.ParachuteVehiclePrefab.resourcePath, player.transform.position, player.eyes.rotation) as Parachute;
                        if (parachute != null)
                        {
                            parachute.skinID = slot.skin;
                            parachute.Spawn();
                            parachute.SetHealth(parachute.MaxHealth() * slot.conditionNormalized);
                            parachute.AttemptMount(player);
                            if (player.isMounted)
                            {
                                slot.Remove();
                                ItemManager.DoRemoves();
                            }
                            else
                            {
                                parachute.Kill();
                            }
                        }
                    }

                    if (spawnedRocket != null && !spawnedRocket.IsDestroyed)
                    {
                        spawnedRocket.Kill();
                    }

                    if (chairEntity != null && !chairEntity.IsDestroyed)
                    {
                        chairEntity.Kill();
                    }
                });
            }
        }

        private void SpawnSupplySignal(BasePlayer player, Parachute parachute, string SupplySignalPath)
        {
            if (parachute != null)
            {
                BaseEntity smoketrail = GameManager.server.CreateEntity(SupplySignalPath, player.transform.position, player.transform.rotation);

                if (smoketrail != null)
                {
                    smoketrail.SetParent(player);
                    smoketrail.GetComponent<Rigidbody>().useGravity = false;
                    smoketrail.GetComponent<Rigidbody>().isKinematic = true;
                    smoketrail.Spawn();
                    {
                        SupplySignal ss = smoketrail as SupplySignal;

                        ss.CancelInvoke(ss.Explode);
                        ss.Invoke(() =>
                        {
                            ss.SetFlag(BaseEntity.Flags.On, true);
                            ss.SendNetworkUpdateImmediate();
                        }, 0);
                    }

                    Vector3 smoketrailPosition = player.transform.position;
                    Quaternion smoketrailRotation = player.transform.rotation * Quaternion.Euler(-90, 0, 0);

                    smoketrail.transform.SetPositionAndRotation(smoketrailPosition, smoketrailRotation);

                    player.ChatMessage("<color=#cf4bde><size=10>SMOKE TRAIL ENABLED</color></size>");
                    playerParachuteMap[player] = smoketrail;
                }
            }
        }

        private Vector3 CalculateSpawnPosition(Vector3 targetPosition, float additionalHeight)
        {
            Vector3 mapCenter = new Vector3(0f, 0f, 0f);
            Vector3 directionToEdge = (targetPosition - mapCenter).normalized;
            float distanceToEdge = 750f;
            Vector3 spawnPosition = targetPosition + directionToEdge * distanceToEdge;

            spawnPosition.y += additionalHeight;

            return spawnPosition;
        }

        private void PlayAfterburnEffect(BasePlayer player)
        {
            if (AfterburnerEffectOffCooldown(player.UserIDString))
            {
                playerLastAfterburnTime[player.UserIDString] = Time.realtimeSinceStartup;

                Vector3 afterburnEffectPosition = player.transform.position - (player.transform.forward * 5f) - (player.transform.up * 3f);
                Quaternion afterburnEffectRotation = player.transform.rotation;
                Vector3 afterburnEffectDirection = afterburnEffectRotation * Vector3.forward;

                Effect.server.Run(AfterburnerEffect, afterburnEffectPosition, afterburnEffectDirection);

                if (!_config.afterburnerEffect) return;

                Vector3 boosterEffectBPosition = player.transform.position - (player.transform.forward * 1f) - (player.transform.up * 1f);
                Quaternion boosterEffectBRotation = player.transform.rotation;
                Vector3 boosterEffectBDirection = boosterEffectBRotation * Vector3.forward;

                Effect.server.Run(BoosterEffectB, boosterEffectBPosition, boosterEffectBDirection);

            }
        }

        private bool IsRaidBlocked(BasePlayer player)
        {
            if (NoEscape == null) return false;
            if (!_config.preventWhenRaidBlocked) return false;
            if (permission.UserHasPermission(player.UserIDString, PermissionIgnoreNoEscape)) return false;
            bool playerIsRaidBlocked = Convert.ToBoolean(NoEscape?.Call("IsRaidBlocked", player));
            if (playerIsRaidBlocked) player.ChatMessage("<size=10><color=#a4bd6f>Raid Blocked!</color></size>");
            return playerIsRaidBlocked;
        }

        private bool IsCombatBlocked(BasePlayer player)
        {
            if (NoEscape == null) return false;
            if (!_config.preventWhenCombatBlocked) return false;
            if (permission.UserHasPermission(player.UserIDString, PermissionIgnoreNoEscape)) return false;
            bool playerIsCombatBlocked = Convert.ToBoolean(NoEscape?.Call("IsCombatBlocked", player));
            if (playerIsCombatBlocked) player.ChatMessage("<size=10><color=#a4bd6f>Combat Blocked!</color></size>");
            return playerIsCombatBlocked;
        }

        #endregion

        #region Helpers

        private void UpdatePermissions()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                Dictionary<string, bool> permissionCache = new Dictionary<string, bool>();
                ulong playerId = player.userID;

                foreach (string permissions in permissionsToCache)
                {
                    bool hasPermission = permission.UserHasPermission(player.UserIDString, permissions);
                    permissionCache[permissions] = hasPermission;
                }

                playerPermissions[playerId] = permissionCache;
            }
        }

        private bool HasPermission(ulong playerId, string permissionToCheck)
        {
            return playerPermissions.TryGetValue(playerId, out Dictionary<string, bool> cachedPermissions) &&
                   cachedPermissions.TryGetValue(permissionToCheck, out bool hasPermission) &&
                   hasPermission;
        }

        void OnUserPermissionGranted(string id, string permName)
        {
            UpdatePermissions();
        }

        void OnGroupPermissionGranted(string name, string perm)
        {
            UpdatePermissions();
        }

        void OnUserPermissionRevoked(string id, string permName)
        {
            UpdatePermissions();
        }

        void OnGroupPermissionRevoked(string name, string perm)
        {
            UpdatePermissions();
        }

        private void RefillFuelOverTime(string userID)
        {
            BasePlayer player = BasePlayer.FindByID(ulong.Parse(userID));

            if (playerFuel.ContainsKey(userID) && player != null)
            {
                float currentTime = Time.realtimeSinceStartup;

                if (!lastRefillTime.ContainsKey(userID) || (currentTime - lastRefillTime[userID] >= 1f))
                {
                    if (HasPermission(player.userID, PermissionFuelT3))
                    {
                        if (playerFuel[userID] < _config.InitialFuelAmountT3)
                        {
                            playerFuel[userID] += _config.FuelRefillRateT3;

                            lastRefillTime[userID] = currentTime;

                            timer.Once(1f, () => RefillFuelOverTime(userID));
                        }
                        else if (!lastFuelFullMessageTime.ContainsKey(player.UserIDString) || (Time.realtimeSinceStartup - lastFuelFullMessageTime[player.UserIDString] >= 6f))
                        {
                            player.ChatMessage("<size=10><color=#a4bd6f>FUEL REFILLED!</color></size>");
                            lastFuelFullMessageTime[player.UserIDString] = Time.realtimeSinceStartup;
                        }
                    }
                    else if (HasPermission(player.userID, PermissionFuelT2))
                    {
                        if (playerFuel[userID] < _config.InitialFuelAmountT2)
                        {
                            playerFuel[userID] += _config.FuelRefillRateT2;

                            lastRefillTime[userID] = currentTime;

                            timer.Once(1f, () => RefillFuelOverTime(userID));
                        }
                        else if (!lastFuelFullMessageTime.ContainsKey(player.UserIDString) || (Time.realtimeSinceStartup - lastFuelFullMessageTime[player.UserIDString] >= 6f))
                        {
                            player.ChatMessage("<size=10><color=#a4bd6f>FUEL REFILLED!</color></size>");
                            lastFuelFullMessageTime[player.UserIDString] = Time.realtimeSinceStartup;
                        }
                    }
                    else
                    {
                        if (playerFuel[userID] < _config.InitialFuelAmountT1)
                        {
                            playerFuel[userID] += _config.FuelRefillRateT1;

                            lastRefillTime[userID] = currentTime;

                            timer.Once(1f, () => RefillFuelOverTime(userID));
                        }
                        else if (!lastFuelFullMessageTime.ContainsKey(player.UserIDString) || (Time.realtimeSinceStartup - lastFuelFullMessageTime[player.UserIDString] >= 6f))
                        {
                            player.ChatMessage("<size=10><color=#a4bd6f>FUEL REFILLED!</color></size>");
                            lastFuelFullMessageTime[player.UserIDString] = Time.realtimeSinceStartup;
                        }
                    }
                }
            }
        }

        private void ConsumeFuel(string userID, float amount)
        {
            BasePlayer player = BasePlayer.FindByID(ulong.Parse(userID));

            if (playerFuel.ContainsKey(userID))
            {
                playerFuel[userID] -= amount;

                if (playerFuel[userID] < 0)
                {
                    playerFuel[userID] = 0;
                }
            }
            else //set inital fuel amount
            {
                if (HasPermission(player.userID, PermissionFuelT3))
                {
                    playerFuel[userID] = _config.InitialFuelAmountT3;
                }
                else if (HasPermission(player.userID, PermissionFuelT2))
                {
                    playerFuel[userID] = _config.InitialFuelAmountT2;
                }
                else
                {
                    playerFuel[userID] = _config.InitialFuelAmountT1;
                }
            }
        }

        private bool HasEnoughFuel(string userID)
        {
            BasePlayer player = BasePlayer.FindByID(ulong.Parse(userID));

            if (playerFuel.ContainsKey(userID))
            {
                return playerFuel[userID] >= _config.FuelConsumption;
            }
            else //set inital fuel amount
            {
                if (HasPermission(player.userID, PermissionFuelT3))
                {
                    playerFuel[userID] = _config.InitialFuelAmountT3;
                }
                else if (HasPermission(player.userID, PermissionFuelT2))
                {
                    playerFuel[userID] = _config.InitialFuelAmountT2;
                }
                else
                {
                    playerFuel[userID] = _config.InitialFuelAmountT1;
                }
                return true;
            }
        }

        private bool AfterburnerEffectOffCooldown(string playerId)
        {
            if (!playerLastAfterburnTime.ContainsKey(playerId))
            {
                return true;
            }

            float lastAfterburnTime = playerLastAfterburnTime[playerId];
            float currentTime = Time.realtimeSinceStartup;

            if (currentTime - lastAfterburnTime >= _config.afterburnCooldown)
            {
                return true;
            }

            return false;
        }

        private bool BoostEffectOffCooldown(string playerId)
        {
            if (!playerLastBoostTime.ContainsKey(playerId))
            {
                return true;
            }

            float lastBoostTime = playerLastBoostTime[playerId];
            float currentTime = Time.realtimeSinceStartup;

            if (currentTime - lastBoostTime >= _config.boostCooldown)
            {
                return true;
            }

            return false;
        }

        private bool IsDroppingFlares(BasePlayer player)
        {
            float lastFlaresTime;
            if (playerLastFlaresTime.TryGetValue(player.UserIDString, out lastFlaresTime))
            {
                float currentTime = Time.realtimeSinceStartup;
                float cooldownDuration = _config.flaresCooldownDuration;

                if (currentTime - lastFlaresTime < cooldownDuration)
                {
                    return true;
                }
            }
            return false;
        }

        private BasePlayer GetMountedPlayer(BaseMountable mount)
        {
            if (mount.GetMounted())
            {
                return mount.GetMounted();
            }

            if (mount as BaseVehicle)
            {
                BaseVehicle vehicle = mount as BaseVehicle;

                foreach (BaseVehicle.MountPointInfo point in vehicle.mountPoints)
                {
                    if (point.mountable.IsValid() && point.mountable.GetMounted())
                    {
                        return point.mountable.GetMounted();
                    }
                }
            }

            return null;
        }

        private BasePlayer FindMountedPlayerOnParachute(Parachute parachute)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                BaseEntity mountedEntity = player.GetMounted();

                if (mountedEntity is Parachute && mountedEntity == parachute)
                {
                    return player;
                }
            }
            return null;
        }

        private bool IsParachuteInWearContainer(BasePlayer player)
        {
            var parachuteItemID = 602628465;
            var wearContainer = player.inventory.containerWear;

            foreach (var wearItem in wearContainer.itemList)
            {
                if (wearItem.info.itemid == parachuteItemID)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Configuration

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ParagliderConfiguration>();

            if (_config == null)
            {
                LoadDefaultConfig();
            }
            else
            {
                MigrateConfigIfNeeded();
            }
        }

        private void MigrateConfigIfNeeded()
        {
            string CurrentVersion = _config.ConfigVersion;
            string NewVersion = ConfigFileVersion;

            Version current = new Version(CurrentVersion);
            Version newVersion = new Version(NewVersion);

            if (_config.ConfigVersion == null || current < newVersion)
            {
                _config.afterburnerEffect = true;

                _config.launchEffect = true;

                _config.ToggleParagliderDefault = true;

                _config.WipePlayerDataOnWipe = true;

                _config.WipePlayerDataOnDisconnect = false;

                _config.ConfigVersion = ConfigFileVersion;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = new ParagliderConfiguration();
            SaveConfig();
            Puts("Created new default config.");
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        private class ParagliderConfiguration
        {
            [JsonProperty("Unboosted Force increase while leaning forwards (Default from Facepunch is 2f)")]
            public float UnboostedForwardTiltAcceleration { get; set; } = 75f;

            [JsonProperty("Unboosted Force increase while leaning backwards (Default from Facepunch is 0.2f)")]
            public float UnboostedBackInputForceMultiplier { get; set; } = 1f;

            [JsonProperty("Unboosted Turn Force (Default from Facepunch is 2f)")]
            public float UnboostedTurnForce { get; set; } = 8f;

            [JsonProperty("TIER 1: Forwards Force increase while Boosting forwards")]
            public float BoostedForwardTiltAccelerationT1 { get; set; } = 150f;

            [JsonProperty("TIER 2: Forwards Force increase while Boosting forwards")]
            public float BoostedForwardTiltAccelerationT2 { get; set; } = 200f;

            [JsonProperty("TIER 3: Forwards Force increase while Boosting forwards")]
            public float BoostedForwardTiltAccelerationT3 { get; set; } = 250f;

            [JsonProperty("TIER 1: Backwards Force increase while Boosting backwards")]
            public float BoostedBackInputForceMultiplierT1 { get; set; } = 2f;

            [JsonProperty("TIER 2: Backwards Force increase while Boosting backwards")]
            public float BoostedBackInputForceMultiplierT2 { get; set; } = 4f;

            [JsonProperty("TIER 3: Backwards Force increase while Boosting backwards")]
            public float BoostedBackInputForceMultiplierT3 { get; set; } = 6f;

            [JsonProperty("TIER 1: Turn Force increase while Boosting")]
            public float BoostedTurnForceT1 { get; set; } = 16f;

            [JsonProperty("TIER 2: Turn Force increase while Boosting")]
            public float BoostedTurnForceT2 { get; set; } = 20f;

            [JsonProperty("TIER 3: Turn Force increase while Boosting")]
            public float BoostedTurnForceT3 { get; set; } = 24f;

            [JsonProperty("TIER 1: How much lift you want from AFTERBURNERS")]
            public float upwardsForceT1 { get; set; } = 2.5f;

            [JsonProperty("TIER 2: How much lift you want from AFTERBURNERS")]
            public float upwardsForceT2 { get; set; } = 4f;

            [JsonProperty("TIER 3: How much lift you want from AFTERBURNERS")]
            public float upwardsForceT3 { get; set; } = 5.5f;

            [JsonProperty("TIER 1: How much forward force from AFTERBURNERS - balances upwards force to produce glide")]
            public float forwardsForceT1 { get; set; } = 3.5f;

            [JsonProperty("TIER 2: How much forward force from AFTERBURNERS - balances upwards force to produce glide")]
            public float forwardsForceT2 { get; set; } = 5.5f;

            [JsonProperty("TIER 3: How much forward force from AFTERBURNERS - balances upwards force to produce glide")]
            public float forwardsForceT3 { get; set; } = 7.5f;

            [JsonProperty("TIER 1: How much downwards force you want the Parachutes to have when pressing descend")]
            public float downwardsForceT1 { get; set; } = 1.5f;

            [JsonProperty("TIER 2: How much downwards force you want the Parachutes to have when pressing descend")]
            public float downwardsForceT2 { get; set; } = 2.5f;

            [JsonProperty("TIER 3: How much downwards force you want the Parachutes to have when pressing descend")]
            public float downwardsForceT3 { get; set; } = 3.5f;

            [JsonProperty("TIER 1: How much downwards force while descending and boosting")]
            public float boostDownwardsForceT1 { get; set; } = 2.0f;

            [JsonProperty("TIER 2: How much downwards force while descending and boosting")]
            public float boostDownwardsForceT2 { get; set; } = 4.0f;

            [JsonProperty("TIER 3: How much downwards force while descending and boosting")]
            public float boostDownwardsForceT3 { get; set; } = 6.0f;

            [JsonProperty("TIER 1: How much downwards force while descending with afterburners")]
            public float afterburnDownwardsForceT1 { get; set; } = 4.0f;

            [JsonProperty("TIER 2: How much downwards force while descending with afterburners")]
            public float afterburnDownwardsForceT2 { get; set; } = 6.0f;

            [JsonProperty("TIER 3: How much downwards force while descending with afterburners")]
            public float afterburnDownwardsForceT3 { get; set; } = 8.0f;

            [JsonProperty("TIER 1: Initial Afterburner Fuel")]
            public float InitialFuelAmountT1 { get; set; } = 150f;

            [JsonProperty("TIER 2: Initial Afterburner Fuel")]
            public float InitialFuelAmountT2 { get; set; } = 200f;

            [JsonProperty("TIER 3: Initial Afterburner Fuel")]
            public float InitialFuelAmountT3 { get; set; } = 250f;

            [JsonProperty("TIER 1: Amount of fuel to refill (per second)")]
            public float FuelRefillRateT1 { get; set; } = 6f;

            [JsonProperty("TIER 2: Amount of fuel to refill (per second)")]
            public float FuelRefillRateT2 { get; set; } = 8f;

            [JsonProperty("TIER 3: Amount of fuel to refill (per second)")]
            public float FuelRefillRateT3 { get; set; } = 10f;

            [JsonProperty("Afterburner Fuel Consumption Rate (per player input tick)(consistent for all tiers)")]
            public float FuelConsumption { get; set; } = 1f;

            [JsonProperty("TIER 1: Hold to Launch Countdown Timer")]
            public float CountdownTimerT1 { get; set; } = 10f;

            [JsonProperty("TIER 2: Hold to Launch Countdown Timer")]
            public float CountdownTimerT2 { get; set; } = 5f;

            [JsonProperty("TIER 3: Hold to Launch Countdown Timer")]
            public float CountdownTimerT3 { get; set; } = 3f;

            [JsonProperty("TIER 1: Launch Cooldown Timer (between launches)")]
            public float launchCooldownT1 { get; set; } = 30f;

            [JsonProperty("TIER 2: Launch Cooldown Timer (between launches)")]
            public float launchCooldownT2 { get; set; } = 15f;

            [JsonProperty("TIER 3: Launch Cooldown Timer (between launches)")]
            public float launchCooldownT3 { get; set; } = 10f;

            [JsonProperty("TIER 1: Launch Duration (in seconds)")]
            public float launchDurationT1 { get; set; } = 2f;

            [JsonProperty("TIER 2: Launch Duration (in seconds)")]
            public float launchDurationT2 { get; set; } = 4f;

            [JsonProperty("TIER 3: Launch Duration (in seconds)")]
            public float launchDurationT3 { get; set; } = 6f;

            [JsonProperty("TIER 1: Launch Velocity")]
            public float launchVelocityT1 { get; set; } = 15f;

            [JsonProperty("TIER 2: Launch Velocity")]
            public float launchVelocityT2 { get; set; } = 25f;

            [JsonProperty("TIER 3: Launch Velocity")]
            public float launchVelocityT3 { get; set; } = 35f;

            [JsonProperty("TIER 1: Jet Drop Cooldown Timer (between drops)")]
            public float jetDropCooldownT1 { get; set; } = 600f;

            [JsonProperty("TIER 2: Jet Drop Cooldown Timer (between drops)")]
            public float jetDropCooldownT2 { get; set; } = 300f;

            [JsonProperty("TIER 3: Jet Drop Cooldown Timer (between drops)")]
            public float jetDropCooldownT3 { get; set; } = 120f;

            [JsonProperty("How many seconds you're not targeted by SAMs after deploying flares")]
            public float flaresCooldownDuration { get; set; } = 5.0f;

            [JsonProperty("Prevent Launch and Drop when NoEscape CCOMBAT BLOCKED (grant paraglider.yesescape to bypass)")]
            public bool preventWhenCombatBlocked { get; set; } = true;

            [JsonProperty("Prevent Launch and Drop when NoEscape RAID BLOCKED (grant paraglider.yesescape to bypass)")]
            public bool preventWhenRaidBlocked { get; set; } = true;

            [JsonProperty("Prevent Drop when BUILDING BLOCKED (grant paraglider.yesescape to bypass)")]
            public bool preventDropWhenBuildBlocked { get; set; } = true;

            [JsonProperty("Prevent Launch when BUILDING BLOCKED (prevents exploits paraglider.yesescape to bypass)")]
            public bool preventLaunchWhenBuildBlocked { get; set; } = true;

            [JsonProperty("Automatically deploy Paraglider after Launch")]
            public bool AutoDeployGlider { get; set; } = true;

            [JsonProperty("Time between Afterburner trail effects")]
            public float afterburnCooldown { get; set; } = 0.8f;

            [JsonProperty("Time between Boost trail effects")]
            public float boostCooldown { get; set; } = 0.5f;

            [JsonProperty("Enable Afterburner trail effects")]
            public bool afterburnerEffect { get; set; } = true;

            [JsonProperty("Enable Launch effects")]
            public bool launchEffect { get; set; } = true;

            [JsonProperty("Should paragliders be enabled by default for players with permission?")]
            public bool ToggleParagliderDefault { get; set; } = true;

            [JsonProperty("Wipe player data settings with server wipe?")]
            public bool WipePlayerDataOnWipe { get; set; } = true;

            [JsonProperty("Wipe player data settings when a player disconnects?")]
            public bool WipePlayerDataOnDisconnect { get; set; } = false;

            [JsonProperty("CONFIG FILE VERSION -- DO NOT EDIT")]
            public string ConfigVersion { get; set; } = ConfigFileVersion;
        }

        #endregion

        #region Cleanup

        private void CanDismountEntity(BasePlayer player, BaseMountable mount)
        {
            if (mount.name.Equals("assets/prefabs/vehicle/seats/parachuteseat.prefab") == true)
            {
                if (permission.UserHasPermission(player.UserIDString, PermissionParaglide))
                {
                    player.ChatMessage("<size=10><color=#a4bd6f>EJECTED FROM PARAGLIDER</color></size>");

                    if (playerParachuteMap.ContainsKey(player))
                    {
                        BaseEntity smoketrail = playerParachuteMap[player];

                        if (smoketrail != null && !smoketrail.IsDestroyed)
                        {
                            smoketrail.Kill();
                        }
                        playerParachuteMap.Remove(player);
                    }

                    Vector3 playerPosition = player.transform.position;
                    float radius = 1.5f;

                    Collider[] colliders = Physics.OverlapSphere(playerPosition, radius, LayerMask.GetMask("Ragdoll"));

                    foreach (var collider in colliders)
                    {
                        BaseEntity entity = collider.GetComponentInParent<BaseEntity>();

                        if (entity is SupplySignal)
                        {
                            entity.Kill(); //catch any bugged supply signals 
                        }
                    }
                }
            }
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity is Parachute)
            {
                Parachute parachute = entity as Parachute;

                BasePlayer mountedPlayer = FindMountedPlayerOnParachute(parachute);

                if (mountedPlayer != null)
                {
                    if (playerParachuteMap.ContainsKey(mountedPlayer))
                    {
                        BaseEntity smoketrail = playerParachuteMap[mountedPlayer];
                        if (smoketrail != null && !smoketrail.IsDestroyed)
                        {
                            smoketrail.Kill();
                            playerParachuteMap.Remove(mountedPlayer);
                        }
                    }
                }
            }
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (playerParachuteMap.ContainsKey(player))
            {
                BaseEntity smoketrail = playerParachuteMap[player];
                if (smoketrail != null && !smoketrail.IsDestroyed)
                {
                    smoketrail.Kill();
                    playerParachuteMap.Remove(player);
                }
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;

            if (playerParachuteMap.ContainsKey(player))
            {
                BaseEntity smoketrail = playerParachuteMap[player];
                if (smoketrail != null && !smoketrail.IsDestroyed)
                {
                    smoketrail.Kill();
                    playerParachuteMap.Remove(player);
                }
            }

            ulong playerId = player.userID;
            if (playerPermissions.ContainsKey(playerId))
            {
                playerPermissions.Remove(playerId);
            }

            if (playerLastAfterburnTime.ContainsKey(player.UserIDString))
            {
                playerLastAfterburnTime.Remove(player.UserIDString);
            }

            if (playerLastBoostTime.ContainsKey(player.UserIDString))
            {
                playerLastBoostTime.Remove(player.UserIDString);
            }

            if (playerLastFlaresTime.ContainsKey(player.UserIDString))
            {
                playerLastFlaresTime.Remove(player.UserIDString);
            }

            if (playerFuel.ContainsKey(player.UserIDString))
            {
                playerFuel.Remove(player.UserIDString);
            }

            if (lastRefillTime.ContainsKey(player.UserIDString))
            {
                lastRefillTime.Remove(player.UserIDString);
            }

            if (lastFuelEmptyMessageTime.ContainsKey(player.UserIDString))
            {
                lastFuelEmptyMessageTime.Remove(player.UserIDString);
            }

            if (lastFuelFullMessageTime.ContainsKey(player.UserIDString))
            {
                lastFuelFullMessageTime.Remove(player.UserIDString);
            }

            if (lastEquipMessageTime.ContainsKey(player.UserIDString))
            {
                lastEquipMessageTime.Remove(player.UserIDString);
            }

            if (lastLaunchMessageTime.ContainsKey(player.UserIDString))
            {
                lastLaunchMessageTime.Remove(player.UserIDString);
            }

            if (lastJetMessageTime.ContainsKey(player.UserIDString))
            {
                lastJetMessageTime.Remove(player.UserIDString);
            }

            if (lastFlareMessageTime.ContainsKey(player.UserIDString))
            {
                lastFlareMessageTime.Remove(player.UserIDString);
            }

            if (lastLaunchTime.ContainsKey(player.UserIDString))
            {
                lastLaunchTime.Remove(player.UserIDString);
            }

            if (lastCancelLaunchTime.ContainsKey(player.UserIDString))
            {
                lastCancelLaunchTime.Remove(player.UserIDString);
            }

            if (lastJetDropTime.ContainsKey(player.UserIDString))
            {
                lastJetDropTime.Remove(player.UserIDString);
            }

            if (lastCancelLaunchTime.ContainsKey(player.UserIDString))
            {
                lastCancelLaunchTime.Remove(player.UserIDString);
            }

            //
            if (_config.WipePlayerDataOnDisconnect)
            {
                if (playerSettings.ContainsKey(playerId))
                {
                    playerSettings.Remove(playerId);
                    SavePlayerData();
                }
            }
            //
        }

        public void ClearTrails()
        {
            foreach (BaseEntity smoke in smoketraillist)
            {
                if (smoke != null)
                {
                    smoke.Kill();
                }
            }
            smoketraillist.Clear();
        }

        private void Unload()
        {
            ClearTrails();
            playerParachuteMap.Clear();
            permissionsToCache.Clear();
            smoketraillist.Clear();

            //
            if (_config.WipePlayerDataOnDisconnect)
            {
                playerSettings.Clear();
                SavePlayerData();
            }
            //
        }

        void OnNewSave(string filename)
        {
            if (_config.WipePlayerDataOnWipe)
            {
            Puts("Wipe detected! Clearing player data...");
            playerSettings.Clear();
            SavePlayerData();
            }
        }

        private void OnServerSave() => SavePlayerData();
        private void OnServerShutdown() => SavePlayerData();

        #endregion
    }
}