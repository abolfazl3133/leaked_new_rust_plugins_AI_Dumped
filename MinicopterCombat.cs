/* Copyright (c) 2019 Karuza */

using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Rust;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("MinicopterCombat", "Karuza BxrFlip LeroyJenkins420", "1.1.2")]
    public class MinicopterCombat : RustPlugin
    {
        [PluginReference]
        Plugin BulletProjectile;

        #region Variables
        private static MinicopterCombat instance;

        private static string[] enabledMiniWeaponTypes;
        private static string[] enabledScrapWeaponTypes;
        private static string[] enabledch47;

        private static readonly Dictionary<string, int> weaponsToAmmoTypeItemIdMap = new Dictionary<string, int>();
        private static Permission permissionHelper;

        private static string clear = "0 0 0 0";
        private static string green = "0 1 0 0.4";
        private static string darkGreen = "0 1 0 0.7";
        private static string red = "1 0 0 0.4";
        private static string darkRed = "1 0 0 0.7";
        private static string solidRed = "1 0 0 1";
        private static string white = "1.0 1.0 1.0";
        private static string orange = "0.7 0.4 0 0.7";

        private static string gold = "1.000000000 0.819607843 0.137254902";

        private static string permAdmin = "MinicopterCombat.AdminPerm";
        private static string reticleOverlayName = "AimReticle";
        private static string primaryAmmoOverlayName = "PrimaryAmmoCounter";
        private static string secondaryAmmoOverlayName = "SecondaryAmmoCounter";
        private static string flareAmmoOverlayName = "FlareAmmoCounter";

        private static string primaryCooldownOverlayName = "PrimaryCooldownGui";
        private static string secondaryCooldownOverlayName = "SecondaryCooldownGui";
        private static string flareCooldownOverlayName = "FlareCooldownGui";
        private static string weaponTypeOverlayName = "WeaponSelector";
        private static string enemyLockWarningName = "EnemyLock";
        private static string targetLockGuiName = "TargetLockGui";
        private static string targetMessageGuiName = "TargetMessageGui";

        private static string flarePrefab = "assets/prefabs/tools/flareold/flare.deployed.prefab";

        private static LayerMask layerMask = -805568513;

        private static Configuration configuration;

        #endregion

        #region Methods

        private void RegisterPermissions()
        {
            permission.RegisterPermission(permAdmin, this);
            foreach (var weaponSetting in configuration.WeaponSettings)
            {
                permission.RegisterPermission(weaponSetting.Value.WeaponPermission, this);
            }

            foreach (var weaponSetting in configuration.ScrapcopterWeaponSettings)
            {
                permission.RegisterPermission(weaponSetting.Value.WeaponPermission, this);
            }

            foreach (var weaponSetting in configuration.WeaponSettings)
            {
                permission.RegisterPermission(weaponSetting.Value.WeaponPermission, this);
            }
            permissionHelper = permission;
        }
        void DestroyMinicopterCombatWrappers()
        {
            var miniCopterCombatWrappers = UnityEngine.Object.FindObjectsOfType<MinicopterWrapper>();
            foreach (var miniCopterCombatWrapper in miniCopterCombatWrappers)
                UnityEngine.Object.Destroy(miniCopterCombatWrapper);

            var scrapWrappers = UnityEngine.Object.FindObjectsOfType<ScrapcopterWrapper>();
            foreach (var scrapWrapper in scrapWrappers)
                UnityEngine.Object.Destroy(scrapWrapper);

            var ch47s = UnityEngine.Object.FindObjectsOfType<CH47Wrapper>();
            foreach (var ch47 in ch47s)
                UnityEngine.Object.Destroy(ch47);
        }

        static void GetEnabledWeapons()
        {
            enabledMiniWeaponTypes = configuration.WeaponSettings
                .Where(kv => kv.Value.Enabled)
                .Select(kv => kv.Key)
                .ToArray();

            enabledScrapWeaponTypes = configuration.ScrapcopterWeaponSettings
                .Where(kv => kv.Value.Enabled)
                .Select(kv => kv.Key)
                .ToArray();

            enabledch47 = configuration.WeaponSettings
                .Where(kv => kv.Value.Enabled)
                .Select(kv => kv.Key)
                .ToArray();
        }

        void UpdateExistingMinicopters()
        {
            DestroyMinicopterCombatWrappers();


            var miniCopters = UnityEngine.Object.FindObjectsOfType<Minicopter>();
            foreach (var miniCopter in miniCopters)
            {
                miniCopter.gameObject.AddComponent<MinicopterWrapper>();
            }

            var ch47s = UnityEngine.Object.FindObjectsOfType<CH47Helicopter>();
            foreach (var ch47 in ch47s)
            {
                ch47.gameObject.AddComponent<CH47Wrapper>();
            }

            if (configuration.ApplyToScrapCopter)
            {
                var scrapCopters = UnityEngine.Object.FindObjectsOfType<ScrapTransportHelicopter>();
                foreach (var scrapCopter in scrapCopters)
                {
                    scrapCopter.gameObject.AddComponent<ScrapcopterWrapper>();
                }
            }
        }

        void GetWeaponAmmoTypeItemIds()
        {
            foreach (var wt in configuration.WeaponSettings)
            {
                AddAmmoTypeToAmmoTypeMap(wt.Value.FlareShortName);
                AddAmmoTypeToAmmoTypeMap(wt.Value.PrimaryWeapon?.AmmoTypeShortName);
                AddAmmoTypeToAmmoTypeMap(wt.Value.SecondaryWeapon?.AmmoTypeShortName);
            }

            foreach (var wt in configuration.ScrapcopterWeaponSettings)
            {
                AddAmmoTypeToAmmoTypeMap(wt.Value.FlareShortName);
                AddAmmoTypeToAmmoTypeMap(wt.Value.PrimaryWeapon?.AmmoTypeShortName);
                AddAmmoTypeToAmmoTypeMap(wt.Value.SecondaryWeapon?.AmmoTypeShortName);
            }

            foreach (var wt in configuration.WeaponSettings)
            {
                AddAmmoTypeToAmmoTypeMap(wt.Value.FlareShortName);
                AddAmmoTypeToAmmoTypeMap(wt.Value.PrimaryWeapon?.AmmoTypeShortName);
                AddAmmoTypeToAmmoTypeMap(wt.Value.SecondaryWeapon?.AmmoTypeShortName);
            }
        }

        private void AddAmmoTypeToAmmoTypeMap(string ammoTypeShortName)
        {
            if (string.IsNullOrEmpty(ammoTypeShortName))
                return;

            if (!weaponsToAmmoTypeItemIdMap.ContainsKey(ammoTypeShortName))
                weaponsToAmmoTypeItemIdMap[ammoTypeShortName] = ItemManager.itemList.Find(x => x.shortname == ammoTypeShortName)?.itemid ?? 0;
        }

        private static float AngleOffAroundAxis(Vector3 v, Vector3 forward, Vector3 axis)
        {
            Vector3 right = Vector3.Cross(forward, axis);
            forward = Vector3.Cross(axis, right);
            return Mathf.Atan2(Vector3.Dot(v, right), Vector3.Dot(v, forward)) * Mathf.Rad2Deg;
        }

        #endregion

        #region Config

        public class Configuration
        {
            [JsonProperty]
            public float DebounceTimeSeconds { get; set; } = 4;

            [JsonProperty]
            public bool DisplayOutOfAmmoMessage { get; set; } = true;

            [JsonProperty]
            public bool DisplaySelectedWeaponMessage { get; set; } = true;

            [JsonProperty]
            public bool UnlimitedAmmo { get; set; } = false;

            [JsonProperty]
            public bool DisablePermissionCheck { get; set; } = false;

            [JsonProperty]
            public bool ApplyToScrapCopter { get; set; } = false;

            [JsonProperty]
            public string FlareFiredSfx { get; set; } = "assets/prefabs/deployable/research table/effects/research-table-deploy.prefab";

            [JsonProperty]
            public string SwitchWeaponSfx { get; set; } = "assets/prefabs/deployable/dropbox/effects/submit_items.prefab";

            [JsonProperty]
            public string AlarmSfx { get; set; } = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";

            [JsonProperty]
            public float CounterMeasureDespawnTime { get; set; } = 5f;

            [JsonProperty]
            public float WeaponSwitchDelay { get; set; } = 1f;

            [JsonProperty]
            public BUTTON FirePrimaryButton { get; set; } = BUTTON.FIRE_PRIMARY;

            [JsonProperty]
            public BUTTON FireSecondaryButton { get; set; } = BUTTON.FIRE_SECONDARY;

            [JsonProperty]
            public BUTTON SwitchWeaponButton { get; set; } = BUTTON.FIRE_THIRD;

            [JsonProperty]
            public BUTTON FireFlareButton { get; set; } = BUTTON.USE;

            [JsonProperty]
            public bool EnableScrapcopterGibs { get; set; }

            [JsonProperty]
            public float GibsDespawnTimerOverride { get; set; }

            [JsonProperty]
            public bool DisableFire { get; set; }

            [JsonProperty]
            public bool HideUnauthorizedWeapons { get; set; }

            [JsonProperty]
            public Dictionary<string, WeaponSettings> WeaponSettings = new Dictionary<string, WeaponSettings>()
            {
                {
                    "Disarm", new WeaponSettings()
                    {
                        DisplayShortName = "DISARM",
                        DisplayFullName = "DISARMED",
                        HudConfiguration = HUDConfiguration.None,
                        Enabled = true
                    }
                }
            };

            [JsonProperty]
            public Dictionary<string, WeaponSettings> ScrapcopterWeaponSettings = new Dictionary<string, WeaponSettings>()
            {
                {
                    "Disarm", new WeaponSettings()
                    {
                        DisplayShortName = "DISARM",
                        DisplayFullName = "DISARMED",
                        HudConfiguration = HUDConfiguration.None,
                        Enabled = true
                    }
                }
            };
            [JsonProperty]
            public Dictionary<string, WeaponSettings> ch47config = new Dictionary<string, WeaponSettings>()
            {
                {
                    "Disarm", new WeaponSettings()
                    {
                        DisplayShortName = "DISARM",
                        DisplayFullName = "DISARMED",
                        HudConfiguration = HUDConfiguration.None,
                        Enabled = true
                    }
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                configuration = Config.ReadObject<Configuration>();
                if (configuration == null)
                    throw new Exception();
            }
            catch
            {
                Config.WriteObject(configuration, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            configuration = new Configuration();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(configuration);
        }

        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            LoadConfig();
            GetEnabledWeapons();
            UpdateExistingMinicopters();
            GetWeaponAmmoTypeItemIds();

            RegisterPermissions();
            instance = this;
        }
        void Unload()
        {
            DestroyMinicopterCombatWrappers();
        }

        void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            var heliWrapper = entity.VehicleParent()?.GetComponent<HelicopterWrapper>();
            if (heliWrapper == null)
                return;
            heliWrapper.SetPlayer(player);
        }

        void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            if (player == null || entity == null)
                return;

            var heliWrapper = entity.VehicleParent()?.GetComponent<HelicopterWrapper>();
            if (heliWrapper == null)
                return;

            heliWrapper.RemovePlayer(player);
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            var ch47 = entity.GetComponent<CH47Helicopter>();
            var prefabname = entity.name;
            if (prefabname.Contains("servergibs_scraptransport"))
            {
                NextTick(() =>
                {
                    var debris = entity?.GetComponent<HelicopterDebris>() ?? null;
                    if (debris == null || entity.IsDestroyed)
                    {
                        return;
                    }

                    if (!configuration.EnableScrapcopterGibs)
                    {
                        entity.Kill();
                    }
                    else if (configuration.GibsDespawnTimerOverride > 0)
                    {
                        timer.Once(configuration.GibsDespawnTimerOverride, () =>
                        {
                            entity.Kill();
                            debris.SendNetworkUpdate();
                        });

                        return;
                    }
                });
            }
            if (entity is CH47Helicopter)
            {
                ch47.InitializeHealth(9999999999999999999, 9999999999999999999);
            }
            var ScrapCopter = entity.GetComponent<ScrapTransportHelicopter>();
            var miniCopter = entity.GetComponent<Minicopter>();
            if (miniCopter != null)
                miniCopter.gameObject.AddComponent<MinicopterWrapper>();

            if (ch47 != null)
            {
                Puts($"its a ch47");
                ch47.gameObject.AddComponent<CH47Wrapper>();
            }
            if (ScrapCopter != null)
            {
                Puts("its a scrappy");
                if (configuration.ApplyToScrapCopter)
                {
                    ScrapCopter.gameObject.AddComponent<ScrapcopterWrapper>();
                }
                return;
            }

            return;
        }


        #endregion

        #region MinicopterWrapper
        public class ScrapcopterWrapper : HelicopterWrapper
        {
            // todo update
            protected override float maxAngle { get { return 14f; } }
            protected override float minAngle { get { return -14f; } }
            protected override float centerX { get { return 0.491f; } }
            protected override float centerY { get { return 0.57675f; } }

            protected override string reticleGuiOverlayMin { get { return "0.4230 0.485"; } }
            protected override string reticleGuiOverlayMax { get { return "0.5730 0.635"; } }
            protected override string primaryAmmoMin { get { return "0.3445 0.586"; } }
            protected override string primaryAmmoMax { get { return "0.4215 0.609"; } }
            protected override string secondaryAmmoMin { get { return "0.5754 0.586"; } }
            protected override string secondaryAmmoMax { get { return "0.6524 0.609"; } }
            protected override string flareMin { get { return "0.473 0.4884"; } }
            protected override string flareMax { get { return "0.5236 0.5084"; } }
            protected override string lockGuiMin { get { return "0.46 0.461"; } }
            protected override string lockGuiMax { get { return "0.536 0.483"; } }

            protected override string GetNextWeaponType(string current)
            {
                int i = Array.IndexOf<string>(enabledScrapWeaponTypes, current) + 1;
                return (enabledScrapWeaponTypes.Length == i) ? enabledScrapWeaponTypes[0] : enabledScrapWeaponTypes[i];
            }

            protected override Dictionary<string, WeaponSettings> GetAvailableWeapons()
            {
                return configuration.ScrapcopterWeaponSettings;
            }
        }
        public class CH47Wrapper : HelicopterWrapper
        {
            protected override float maxAngle { get { return 14f; } }
            protected override float minAngle { get { return -14f; } }
            protected override float centerX { get { return 0.491f; } }
            protected override float centerY { get { return 0.57675f; } }

            protected override string reticleGuiOverlayMin { get { return "0.4230 0.485"; } }
            protected override string reticleGuiOverlayMax { get { return "0.5730 0.635"; } }
            protected override string primaryAmmoMin { get { return "0.3445 0.586"; } }
            protected override string primaryAmmoMax { get { return "0.4215 0.609"; } }
            protected override string secondaryAmmoMin { get { return "0.5754 0.586"; } }
            protected override string secondaryAmmoMax { get { return "0.6524 0.609"; } }
            protected override string flareMin { get { return "0.473 0.4884"; } }
            protected override string flareMax { get { return "0.5236 0.5084"; } }
            protected override string lockGuiMin { get { return "0.46 0.461"; } }
            protected override string lockGuiMax { get { return "0.536 0.483"; } }

            protected override string GetNextWeaponType(string current)
            {
                int i = Array.IndexOf<string>(enabledch47, current) + 1;
                return (enabledch47.Length == i) ? enabledch47[0] : enabledch47[i];
            }

            protected override Dictionary<string, WeaponSettings> GetAvailableWeapons()
            {
                return configuration.WeaponSettings;
            }
        }

        public class MinicopterWrapper : HelicopterWrapper
        {
            protected override float maxAngle { get { return 14f; } }
            protected override float minAngle { get { return -14f; } }
            protected override float centerX { get { return 0.491f; } }
            protected override float centerY { get { return 0.47675f; } }

            protected override string reticleGuiOverlayMin { get { return "0.4205 0.365"; } }
            protected override string reticleGuiOverlayMax { get { return "0.5705 0.515"; } }
            protected override string primaryAmmoMin { get { return "0.34295 0.466"; } }
            protected override string primaryAmmoMax { get { return "0.4195 0.489"; } }
            protected override string secondaryAmmoMin { get { return "0.573 0.466"; } }
            protected override string secondaryAmmoMax { get { return "0.65 0.489"; } }
            protected override string flareMin { get { return "0.473 0.368"; } }
            protected override string flareMax { get { return "0.5236 0.388"; } }
            protected override string lockGuiMin { get { return "0.46 0.341"; } }
            protected override string lockGuiMax { get { return "0.536 0.363"; } }

            protected override string GetNextWeaponType(string current)
            {
                int i = Array.IndexOf<string>(enabledMiniWeaponTypes, current) + 1;
                return (enabledMiniWeaponTypes.Length == i) ? enabledMiniWeaponTypes[0] : enabledMiniWeaponTypes[i];
            }

            protected override Dictionary<string, WeaponSettings> GetAvailableWeapons()
            {
                return configuration.WeaponSettings;
            }
        }

        public class HelicopterWrapper : MonoBehaviour, Lock
        {
            #region Variables
            public Guid Id { get; set; }
            public float ChanceToLoseLock { get { return weaponSettings.ChanceToLoseLock; } }
            public bool IsAlive { get; set; }

            private BasePlayer player { get; set; } = null;
            private string selectedWeaponType { get; set; } = "Disarm";
            private WeaponSettings weaponSettings { get; set; }

            private bool IsLocked { get { return locks.Count > 0; } }
            private bool primaryCooldownContainerActive = false;
            private bool primaryCooldownContainerContrastActive = false;

            private bool secondaryCooldownContainerActive = false;
            private bool secondaryCooldownContainerContrastActive = false;

            private bool flareCooldownContainerActive = false;
            private bool flareCooldownContainerContrastActive = false;


            // To prevent spamming the player, log the message time
            private float lastMsgTime = 0.0f;
            private float lastPrimaryWeaponFiredTime = 0.0f;
            private float lastSecondaryWeaponFiredTime = 0.0f;
            private float lastWeaponSwitchTime = 0.0f;

            private int primaryRoundsFired = 0;
            private int secondaryRoundsFired = 0;
            private int flareRoundsFired = 0;

            private float primaryCooldownTimer = 0f;
            private float secondaryCooldownTimer = 0f;
            private float flareCooldownTimer = 0f;

            private Dictionary<string, int[]> cooldownRoundsFired = new Dictionary<string, int[]>();
            private Dictionary<string, float> cooldownSwitchTime = new Dictionary<string, float>();

            private int primaryCurrentBarrel = 0;
            private int secondaryCurrentBarrel = 0;

            private BaseVehicle vehicle { get; set; } = null;
            private CH47Helicopter CH47 { get; set; } = new CH47Helicopter();

            #region GuiOverrides
            protected virtual string reticleGuiOverlayMin { get { return "0 0"; } }
            protected virtual string reticleGuiOverlayMax { get { return "0 0"; } }
            protected virtual string primaryAmmoMin { get { return "0 0"; } }
            protected virtual string primaryAmmoMax { get { return "0 0"; } }
            protected virtual string secondaryAmmoMin { get { return "0 0"; } }
            protected virtual string secondaryAmmoMax { get { return "0 0"; } }
            protected virtual string flareMin { get { return "0 0"; } }
            protected virtual string flareMax { get { return "0 0"; } }
            protected virtual string lockGuiMin { get { return "0 0"; } }
            protected virtual string lockGuiMax { get { return "0 0"; } }

            #endregion

            #region AARocketProperties
            private float aquiredAt = 0.0f;
            private float lockedAt = 0.0f;
            private bool isLockAlarmPlaying = false;
            private bool targetLocked = false;
            private bool acquiringLock = false;
            private BaseEntity target = null;

            protected virtual float maxAngle { get { return 0f; } }
            protected virtual float minAngle { get { return 0f; } }
            protected virtual float centerX { get { return 0f; } }
            protected virtual float centerY { get { return 0f; } }

            private float targetAngleX = 0.0f;
            private float targetAngleY = 0.0f;
            #endregion

            #region CounterMeasures
            private float timeBeforeFlaresExpire = 2f;
            private float lastFlaresFiredTime = 0.0f;
            private bool leftFlareFiredLast { get; set; } = false;
            private Dictionary<Guid, Lock> locks { get; set; } = new Dictionary<Guid, Lock>();
            #endregion

            #endregion

            #region WeaponSystemsMethods
            protected virtual Dictionary<string, WeaponSettings> GetAvailableWeapons()
            {
                return new Dictionary<string, WeaponSettings>();
            }
            private void UpdateWeapons()
            {
                UpdateTargetLock();
                Toggle_DrawTargetLock(true);
                Toggle_CooldownGui(true);
                if (player.serverInput.WasJustPressed(configuration.SwitchWeaponButton))
                    SwitchWeaponType();

                if (player.serverInput.WasJustPressed(configuration.FireFlareButton))
                    FireFlares();
                if (this.selectedWeaponType.Equals("Disarm", StringComparison.InvariantCultureIgnoreCase))
                    return;
                if (player.serverInput.IsDown(configuration.FirePrimaryButton))
                {
                    FireProjectile(this.weaponSettings.PrimaryWeapon);
                }
                if (this.weaponSettings.SecondaryWeapon != null)
                {
                    if (player.serverInput.IsDown(configuration.FireSecondaryButton))
                        FireProjectile(this.weaponSettings.SecondaryWeapon);
                    else if (this.weaponSettings.SecondaryWeapon.ProjectileType == ProjectileType.TargetLocker)
                        ClearTarget();
                }
            }

            private void FireFlares()
            {
                if (!weaponSettings.FlaresEnabled)
                    return;

                if (weaponSettings.FlaresBeforeCooldown > 0 && weaponSettings.FlaresBeforeCooldown <= flareRoundsFired)
                    return;

                float currentTime = UnityEngine.Time.realtimeSinceStartup;
                if (currentTime < (lastFlaresFiredTime + weaponSettings.FlareFireRate))
                    return;

                if (!weaponSettings.UnlimitedFlares && !DoesUserHaveAmmo(weaponSettings.FlareShortName, currentTime))
                    return;

                GameObject flareEnt = GameManager.server.CreatePrefab(flarePrefab, this.vehicle.transform.position, new Quaternion(), false);
                BaseEntity flareBaseEnt = flareEnt.GetComponent<BaseEntity>();

                flareBaseEnt.enableSaving = false;
                flareBaseEnt.Spawn();
                flareEnt.SetActive(true);
                flareBaseEnt.transform.position = Vector3.MoveTowards(this.vehicle.transform.position, this.vehicle.transform.localPosition + (this.vehicle.transform.right * (leftFlareFiredLast ? 25f : -25f)), 5f);
                flareEnt.AddComponent<CounterMeasure>();
                if (!string.IsNullOrEmpty(configuration.FlareFiredSfx))
                {
                    Effect.server.Run(configuration.FlareFiredSfx, flareBaseEnt, 0, Vector3.back, Vector3.forward, null);
                }

                leftFlareFiredLast = !leftFlareFiredLast;
                lastFlaresFiredTime = currentTime;
                flareRoundsFired++;

                foreach (var lck in locks.Values.ToList())
                {
                    if (lck.ChanceToLoseLock > 0 && lck.ChanceToLoseLock < 100 && Oxide.Core.Random.Range(0, 100) < lck.ChanceToLoseLock)
                        continue;
                    lck.SetLockTarget(flareBaseEnt);
                }

                if (!weaponSettings.UnlimitedFlares)
                    UseAmmo(weaponSettings.FlareShortName, null);
            }

            private void ResetRoundsFiredByConfig(WeaponConfiguration weaponConfiguration, float lastFiredTime, ref int roundsFired, ref float cooldownTimer)
            {
                // weaponConfiguration.ShotsBeforeCoolDown <= 0 || 
                if (weaponConfiguration == null)
                    return;

                ResetRoundsFired(weaponConfiguration.CoolDownDecay, lastFiredTime, ref roundsFired, ref cooldownTimer);
            }

            private void ResetRoundsFired(float coolDownDecay, float lastFiredTime, ref int roundsFired, ref float cooldownTimer)
            {
                if (roundsFired <= 0)
                    return;

                float currentTime = UnityEngine.Time.realtimeSinceStartup;
                float timeSinceLastFired = (currentTime - lastFiredTime);
                if (timeSinceLastFired <= 1)
                    return;

                float timeSinceLastCooldownCheck = (currentTime - cooldownTimer);
                if (timeSinceLastCooldownCheck > coolDownDecay)
                {
                    --roundsFired;
                    cooldownTimer = currentTime;
                }

                if (roundsFired < 0)
                    roundsFired = 0;
            }

            private Vector3 GetMuzzlePosition(WeaponConfiguration weaponConfiguration, int currentBarrel)
            {
                Vector3 muzzlePos = Vector3.zero;
                var vehicleTransform = this.vehicle.transform;
                switch (weaponConfiguration.BarrelConfiguration)
                {
                    case BarrelConfiguration.Bottom:
                        muzzlePos = this.player.transform.localPosition + Vector3.down;
                        break;
                    case BarrelConfiguration.Side:
                        muzzlePos = vehicleTransform.localPosition + (vehicleTransform.forward + (vehicleTransform.right * 1.5f * (currentBarrel % 2 == 0 ? 1 : -1)));
                        break;
                    case BarrelConfiguration.SideOuter:
                        muzzlePos = vehicleTransform.localPosition + (vehicleTransform.forward + (vehicleTransform.right * 2f * (currentBarrel % 2 == 0 ? 1 : -1)));
                        break;
                    case BarrelConfiguration.DualFront:
                        muzzlePos = vehicleTransform.localPosition + ((vehicleTransform.forward * 3.5f) + (vehicleTransform.right * 0.75f * (currentBarrel % 2 == 0 ? 1 : -1)));
                        break;
                    case BarrelConfiguration.CenterFront:
                        muzzlePos = vehicleTransform.localPosition + (vehicleTransform.forward * 3.5f);
                        break;
                    case BarrelConfiguration.Gattling:
                        muzzlePos = GetGattlingMuzzlePos(vehicleTransform, currentBarrel);
                        break;
                    case BarrelConfiguration.CenterBack:
                        muzzlePos = vehicleTransform.localPosition + (vehicleTransform.forward * -5.5f) + (vehicleTransform.up);
                        break;
                    default:
                        break;
                }

                return muzzlePos;
            }

            private Vector3 GetGattlingMuzzlePos(Transform vehicleTransform, int currentBarrel)
            {
                Vector3 modifier = Vector3.zero;
                switch (currentBarrel)
                {
                    case 0:
                        modifier = (vehicleTransform.up * 0.30f);
                        break;
                    case 1:
                        modifier = (vehicleTransform.right * 0.30f);
                        break;
                    case 2:
                        modifier = ((vehicleTransform.right + (vehicleTransform.up * -1)) * 0.30f);
                        break;
                    case 3:
                        modifier = ((vehicleTransform.right + vehicleTransform.up) * 0.30f) * -1;
                        break;
                    case 4:
                        modifier = (vehicleTransform.right * 0.30f * -1);
                        break;
                    default:
                        break;
                }

                return vehicleTransform.localPosition + ((vehicleTransform.forward * 3.5f) + modifier);
            }

            private void TriggerMuzzleEffect(WeaponConfiguration weaponConfiguration, int currentBarrel)
            {
                if (string.IsNullOrEmpty(weaponConfiguration.MuzzleEffect))
                    return;

                Vector3 posLocal;
                switch (weaponConfiguration.BarrelConfiguration)
                {
                    case BarrelConfiguration.Side:
                        posLocal = (currentBarrel % 2 == 0 ? Vector3.right : Vector3.left) * 2 + (Vector3.forward / 1.8f);
                        break;
                    case BarrelConfiguration.SideOuter:
                        posLocal = (currentBarrel % 2 == 0 ? Vector3.right : Vector3.left) * 3 + (Vector3.forward / 1.8f);
                        break;
                    case BarrelConfiguration.DualFront:
                        posLocal = (currentBarrel % 2 == 0 ? Vector3.right : Vector3.left) / 2 + (Vector3.forward * 2.1f);
                        break;
                    case BarrelConfiguration.CenterFront:
                        posLocal = (Vector3.forward);
                        break;
                    case BarrelConfiguration.CenterBack:
                        posLocal = (Vector3.forward * -5.5f);
                        break;
                    case BarrelConfiguration.Gattling:
                        posLocal = GetGattlingEffectPos(currentBarrel);
                        break;
                    case BarrelConfiguration.Bottom:
                    default:
                        return;
                }

                Effect.server.Run(weaponConfiguration.MuzzleEffect, this.vehicle, 0, posLocal, Vector3.forward, null);
            }


            private Vector3 GetGattlingEffectPos(int currentBarrel)
            {
                Vector3 modifier = Vector3.zero;
                switch (currentBarrel)
                {
                    case 0:
                        modifier = Vector3.up;
                        break;
                    case 1:
                        modifier = Vector3.right;
                        break;
                    case 2:
                        modifier = Vector3.right + (Vector3.up * -1);
                        break;
                    case 3:
                        modifier = Vector3.right + Vector3.up * -1;
                        break;
                    case 4:
                        modifier = Vector3.left;
                        break;
                    default:
                        break;
                }

                return modifier / 3f + (Vector3.forward * 2.1f);
            }

            private void FireProjectile(WeaponConfiguration weaponConfiguration)
            {
                int currentBarrel = 0;
                float lastFiredTime = 0f;
                int roundsFired = 0;

                float currentTime = UnityEngine.Time.realtimeSinceStartup;
                if (weaponConfiguration == weaponSettings.PrimaryWeapon)
                {
                    weaponConfiguration = this.weaponSettings.PrimaryWeapon;
                    lastFiredTime = lastPrimaryWeaponFiredTime;
                    currentBarrel = primaryCurrentBarrel;
                    roundsFired = primaryRoundsFired;
                }
                else
                {
                    weaponConfiguration = this.weaponSettings.SecondaryWeapon;
                    lastFiredTime = lastSecondaryWeaponFiredTime;
                    currentBarrel = secondaryCurrentBarrel;
                    roundsFired = secondaryRoundsFired;
                }

                if (weaponConfiguration.ShotsBeforeCoolDown > 0 && weaponConfiguration.ShotsBeforeCoolDown <= roundsFired)
                    return;

                if (currentTime < (lastFiredTime + weaponConfiguration.FireRate))
                    return;

                if (weaponConfiguration.RequiresAmmo && !DoesUserHaveAmmo(weaponConfiguration.AmmoTypeShortName, currentTime))
                    return;

                Vector3 muzzlePos = GetMuzzlePosition(weaponConfiguration, currentBarrel);
                if (muzzlePos == Vector3.zero)
                    return;

                switch (weaponConfiguration.ProjectileType)
                {
                    case ProjectileType.ServerProjectile:
                        TriggerMuzzleEffect(weaponConfiguration, currentBarrel);
                        FireWithServerProjectile(weaponConfiguration, muzzlePos);
                        break;
                    case ProjectileType.Bullet:
                        TriggerMuzzleEffect(weaponConfiguration, currentBarrel);
                        //FireWithProjectileSystem(weaponConfiguration, muzzlePos);
                        FireWithProjectileSystem(weaponConfiguration, muzzlePos);
                        break;
                    case ProjectileType.Air2Air:
                        if (!targetLocked || target == null)
                            return;
                        TriggerMuzzleEffect(weaponConfiguration, currentBarrel);
                        FireAARocket(weaponConfiguration, muzzlePos);
                        break;
                    case ProjectileType.TargetLocker:
                        TriggerMuzzleEffect(weaponConfiguration, currentBarrel);
                        LockTarget(muzzlePos, weaponConfiguration);
                        break;
                    default:
                        break;
                }

                if (weaponConfiguration == weaponSettings.PrimaryWeapon)
                {
                    lastPrimaryWeaponFiredTime = currentTime;
                    UpdateBarrel(weaponConfiguration.BarrelConfiguration, ref primaryCurrentBarrel);
                    primaryRoundsFired = ++roundsFired;
                    if (weaponConfiguration.RequiresAmmo)
                        UseAmmo(weaponConfiguration.AmmoTypeShortName, true);
                }
                else
                {
                    lastSecondaryWeaponFiredTime = currentTime;
                    UpdateBarrel(weaponConfiguration.BarrelConfiguration, ref secondaryCurrentBarrel);
                    secondaryRoundsFired = ++roundsFired;
                    if (weaponConfiguration.RequiresAmmo)
                        UseAmmo(weaponConfiguration.AmmoTypeShortName, false);
                }
            }

            private void UpdateBarrel(BarrelConfiguration barrelConfig, ref int nextBarrel)
            {
                switch (barrelConfig)
                {
                    case BarrelConfiguration.DualFront:
                    case BarrelConfiguration.Side:
                    case BarrelConfiguration.CenterFront:
                    case BarrelConfiguration.CenterBack:
                    case BarrelConfiguration.SideOuter:
                        if (++nextBarrel > 1)
                            nextBarrel = 0;
                        break;
                    case BarrelConfiguration.Bottom:
                        break;
                    case BarrelConfiguration.Gattling:
                        if (++nextBarrel > 4)
                            nextBarrel = 0;
                        break;
                    default:
                        break;
                }
            }

            private bool DoesUserHaveAmmo(string ammoTypeShortName, float currentTime)
            {
                if (configuration.UnlimitedAmmo)
                    return true;

                if (this.player.inventory.GetAmount(weaponsToAmmoTypeItemIdMap[ammoTypeShortName]) > 0)
                    return true;

                if (configuration.DisplayOutOfAmmoMessage && currentTime >= (lastMsgTime + configuration.DebounceTimeSeconds))
                {
                    player.ChatMessage("Out of Ammo");
                    lastMsgTime = currentTime;
                }
                return false;
            }

            private void FireWithProjectileSystem(WeaponConfiguration weaponConfiguration, Vector3 muzzlePos)
            {
                Vector3 modifiedAimDir = this.vehicle.transform.forward.normalized;
                if (weaponConfiguration.AimCone > 0)
                    modifiedAimDir = AimConeUtil.GetAimConeQuat(weaponConfiguration.AimCone) * modifiedAimDir;

                instance.BulletProjectile.CallHook("ShootProjectile", this.player, muzzlePos, modifiedAimDir * weaponConfiguration.WeaponSpeed, weaponConfiguration.DamageTypes, weaponConfiguration.AmmoPrefabPath);
            }

            private GameObject FireWithServerProjectile(WeaponConfiguration weaponConfiguration, Vector3 muzzlePos)
            {
                GameObject bulletEnt = GameManager.server.CreatePrefab(weaponConfiguration.AmmoPrefabPath, muzzlePos, new Quaternion(), false);
                ServerProjectile serverProjectile = bulletEnt.GetComponent<ServerProjectile>();
                TimedExplosive timedExplosive = bulletEnt.GetComponent<TimedExplosive>();

                var baseEnt = bulletEnt.GetComponent<BaseEntity>();
                baseEnt.creatorEntity = this.player;

                if (timedExplosive != null)
                {
                    timedExplosive.OwnerID = player.userID;

                    if (weaponConfiguration.DamageTypes != null && weaponConfiguration.DamageTypes.Any())
                    {
                        timedExplosive.damageTypes = weaponConfiguration.DamageTypes;
                    }

                    if (weaponConfiguration.MinBlastRadius > 0)
                    {
                        timedExplosive.minExplosionRadius = weaponConfiguration.MinBlastRadius;
                    }

                    if (weaponConfiguration.MaxBlastRadius > 0)
                    {
                        timedExplosive.explosionRadius = weaponConfiguration.MaxBlastRadius;
                    }

                    if (weaponConfiguration.DetonationTimerMin > 0)
                    {
                        timedExplosive.timerAmountMin = weaponConfiguration.DetonationTimerMin;
                    }

                    if (weaponConfiguration.DetonationTimerMax > 0)
                    {
                        timedExplosive.timerAmountMax = weaponConfiguration.DetonationTimerMax;
                    }
                }

                if (serverProjectile != null)
                {
                    serverProjectile.gravityModifier = 1f;
                    serverProjectile.speed = weaponConfiguration.WeaponSpeed;

                    if (weaponConfiguration.BarrelConfiguration != BarrelConfiguration.Bottom)
                    {
                        serverProjectile.InitializeVelocity(this.vehicle.transform.forward.normalized * weaponConfiguration.WeaponSpeed);
                    }
                }

                baseEnt.Spawn();
                bulletEnt.SetActive(true);

                return bulletEnt;
            }

            private void FireAARocket(WeaponConfiguration weaponConfiguration, Vector3 muzzlePos)
            {
                var projectile = FireWithServerProjectile(weaponConfiguration, muzzlePos);
                var homing = projectile.AddComponent<AARocket>();
                if (weaponConfiguration.AngleModifier > 0)
                    homing.AngleModifier = weaponConfiguration.AngleModifier;

                homing.Initialize(target, ChanceToLoseLock);

                if (weaponConfiguration.ClearAATargetOnFire)
                    ClearTarget();
            }

            private void UseAmmo(string ammoTypeShortName, bool? isPrimary)
            {
                var ammo = player.inventory.FindItemByItemID(weaponsToAmmoTypeItemIdMap[ammoTypeShortName]);
                ammo?.UseItem(1);
                if (isPrimary == null)
                    Toggle_FlareAmmoGui(true);
                else
                    Toggle_AmmoGui(true, isPrimary);
            }

            private void SwitchWeaponType()
            {
                float currentTime = UnityEngine.Time.realtimeSinceStartup;
                if (currentTime < (lastWeaponSwitchTime + configuration.WeaponSwitchDelay))
                    return;
                string oldSelection = selectedWeaponType;
                string newSelection = selectedWeaponType;
                do
                {
                    newSelection = GetNextWeaponType(newSelection);
                }
                while (!newSelection.Equals("Disarm", StringComparison.InvariantCultureIgnoreCase) && !configuration.DisablePermissionCheck && !permissionHelper.UserHasPermission(this.player.userID.ToString(), this.GetAvailableWeapons()[newSelection].WeaponPermission));
                this.selectedWeaponType = newSelection;
                this.weaponSettings = this.GetAvailableWeapons()[this.selectedWeaponType];
                this.SwitchWeaponsReset(currentTime, oldSelection);
                Effect.server.Run(configuration.SwitchWeaponSfx, this.player.transform.position + this.player.transform.forward);
                if (configuration.DisplaySelectedWeaponMessage)
                    player.ChatMessage($"Selected Weapon: {weaponSettings.DisplayFullName}");
                Toggle_ReticleGui(true);
                Toggle_AmmoGui(true, null);
                Toggle_FlareAmmoGui(true);
                Toggle_WeaponTypesGui(true);
                Toggle_CooldownGui(true);
                lastWeaponSwitchTime = currentTime;
            }

            protected virtual string GetNextWeaponType(string current)
            {
                throw new NotImplementedException();
            }

            public void TriggerLockAcquired(Lock lck)
            {
                locks.Add(lck.Id, lck);
                Toggle_ReticleGui(true);
                Toggle_EnemyLockGui(true);
            }

            public void TriggerLockLost(Lock lck)
            {
                locks.Remove(lck.Id);
                Toggle_ReticleGui(true);
                Toggle_EnemyLockGui(true);
            }

            private void LockTarget(Vector3 muzzlePos, WeaponConfiguration weaponConfiguration)
            {
                float currentTime = UnityEngine.Time.realtimeSinceStartup;
                if (targetLocked)
                {
                    bool exceedHud = (targetAngleX > maxAngle || targetAngleX < minAngle) || (targetAngleY > maxAngle || targetAngleY < minAngle);
                    if (exceedHud || target == null || currentTime > (lockedAt + weaponConfiguration.TimeBeforeLockExpires))
                    {
                        ClearTarget();
                    }
                }

                if (acquiringLock)
                {
                    bool exceedHud = (targetAngleX > maxAngle || targetAngleX < minAngle) || (targetAngleY > maxAngle || targetAngleY < minAngle);
                    if (exceedHud || target == null)
                    {
                        ClearTarget();
                    }
                    else if (currentTime > (aquiredAt + weaponConfiguration.TimeToLock))
                    {
                        SetLockTarget(target);
                        lockedAt = currentTime;
                        return;
                    }
                    else
                    {
                        return;
                    }
                }

                RaycastHit rayHit;
                if (!targetLocked && Physics.SphereCast(muzzlePos + Vector3.up, 1f, this.vehicle.transform.forward, out rayHit, 4000, layerMask, QueryTriggerInteraction.Collide))
                {
                    var newTrack = rayHit.GetEntity();
                    var newTarget = newTrack;

                    if (newTarget != target)
                    {
                        CancelInvoke();

                        isLockAlarmPlaying = false;
                        acquiringLock = false;
                        targetAngleX = 0.0f;
                        targetAngleY = 0.0f;
                        target = newTarget;
                    }

                    bool validTarget = false;
                    if (weaponConfiguration.AllowCH47HSR && newTarget is CH47Helicopter)
                    {
                        validTarget = true;
                    }
                    else if (weaponConfiguration.AllowPatrolHeliHSR && newTarget is BaseHelicopter)
                    {
                        validTarget = true;
                    }
                    else
                    {
                        var miniCopter = target as Minicopter;
                        var miniCopterWrapper = target?.GetComponent<HelicopterWrapper>();
                        if (miniCopterWrapper == null)
                        {
                            var targetPlayer = target as BasePlayer;
                            miniCopterWrapper = targetPlayer?.GetMountedVehicle()?.GetComponent<HelicopterWrapper>();
                        }

                        if (miniCopterWrapper != null)
                            validTarget = true;
                    }

                    if (validTarget)
                    {
                        aquiredAt = currentTime;
                        acquiringLock = true;
                        if (!isLockAlarmPlaying)
                        {
                            PlayLockAlarm();
                        }
                    }
                }
            }

            private void SwitchWeaponsReset(float time, string oldSelection)
            {
                cooldownSwitchTime[oldSelection] = time;
                cooldownRoundsFired[oldSelection] = new int[2] { primaryRoundsFired, secondaryRoundsFired };

                if (!cooldownRoundsFired.ContainsKey(selectedWeaponType) || !cooldownSwitchTime.ContainsKey(selectedWeaponType))
                {
                    this.primaryRoundsFired = 0;
                    this.secondaryRoundsFired = 0;
                    this.primaryCooldownTimer = 0.0f;
                    this.secondaryCooldownTimer = 0.0f;
                }
                else
                {
                    float primaryCoolRounds = (time - cooldownSwitchTime[selectedWeaponType]) / weaponSettings.PrimaryWeapon?.CoolDownDecay ?? 1;
                    float secondaryCoolRounds = (time - cooldownSwitchTime[selectedWeaponType]) / weaponSettings.SecondaryWeapon?.CoolDownDecay ?? 1;
                    int primaryCooldownRoundsFired = cooldownRoundsFired[selectedWeaponType][0] - (int)primaryCoolRounds;
                    int secondaryCooldownRoundsFired = cooldownRoundsFired[selectedWeaponType][1] - (int)secondaryCoolRounds;

                    this.primaryRoundsFired = primaryCooldownRoundsFired < 0 ? 0 : primaryCooldownRoundsFired;
                    this.secondaryRoundsFired = secondaryCooldownRoundsFired < 0 ? 0 : secondaryCooldownRoundsFired;
                    this.primaryCooldownTimer = (time - cooldownSwitchTime[selectedWeaponType]) % weaponSettings.PrimaryWeapon?.CoolDownDecay ?? 0;
                    this.secondaryCooldownTimer = (time - cooldownSwitchTime[selectedWeaponType]) % weaponSettings.SecondaryWeapon?.CoolDownDecay ?? 0;
                }

                this.lastPrimaryWeaponFiredTime = 0.0f;
                this.lastSecondaryWeaponFiredTime = 0.0f;

                this.primaryCurrentBarrel = 0;
                this.secondaryCurrentBarrel = 0;
            }

            private void ResetWeapons()
            {
                cooldownSwitchTime[selectedWeaponType] = UnityEngine.Time.realtimeSinceStartup;
                cooldownRoundsFired[selectedWeaponType] = new int[2] { primaryRoundsFired, secondaryRoundsFired };

                this.lastPrimaryWeaponFiredTime = 0.0f;
                this.lastSecondaryWeaponFiredTime = 0.0f;
                this.primaryCooldownTimer = 0.0f;
                this.secondaryCooldownTimer = 0.0f;
                this.primaryRoundsFired = 0;
                this.secondaryRoundsFired = 0;
                this.primaryCurrentBarrel = 0;
                this.secondaryCurrentBarrel = 0;
            }

            public static Vector2 WorldPosToImagePos(Vector3 worldPos)
            {
                Vector3 vector3_2 = new Vector3();
                vector3_2.x = worldPos.x;
                vector3_2.y = worldPos.y;

                return (Vector2)vector3_2;
            }

            public void ClearTarget()
            {
                CancelInvoke();
                isLockAlarmPlaying = false;
                if (target != null)
                {
                    var miniCopter = target as Minicopter;
                    var miniCopterWrapper = miniCopter?.GetComponent<HelicopterWrapper>();
                    if (miniCopterWrapper != null)
                        miniCopterWrapper.TriggerLockLost(this);
                }
                targetAngleX = 0.0f;
                targetAngleY = 0.0f;
                target = null;
                targetLocked = false;
                acquiringLock = false;
            }

            private void PlayLockAlarm()
            {
                isLockAlarmPlaying = true;
                Effect.server.Run(configuration.AlarmSfx, this.player.transform.position);
                // Increase frequency if a lock is acquired
                Invoke("PlayLockAlarm", targetLocked ? 0.2f : 1f);
            }

            public void SetLockTarget(BaseEntity target)
            {
                acquiringLock = false;
                targetLocked = true;

                var miniCopterWrapper = this.target?.GetComponent<HelicopterWrapper>();
                if (miniCopterWrapper != null)
                    miniCopterWrapper.TriggerLockLost(this);

                this.target = target;

                miniCopterWrapper = this.target.GetComponent<HelicopterWrapper>();
                if (miniCopterWrapper != null)
                    miniCopterWrapper.TriggerLockAcquired(this);
            }

            #endregion

            #region Methods
            public void SetPlayer(BasePlayer player)
            {
                if (player == null)
                    return;

                if (this.player == player && this.CH47.GetPlayerSeat(player) != 0)
                {
                    RemovePlayer(player);
                    return;
                }
                if (this.player == player && this.vehicle.GetPlayerSeat(player) != 0)
                {
                    RemovePlayer(player);
                    return;
                }
                else if (this.vehicle.GetPlayerSeat(player) != 0)
                    return;
                this.player = player;
                ClearTarget();
                Toggle_WeaponTypesGui(true);
            }

            public void RemovePlayer(BasePlayer player)
            {
                if (player == null || this.player != player)
                    return;

                this.ClearTarget();
                this.ResetWeapons();

                Toggle_ReticleGui(false);
                Toggle_AmmoGui(false, null);
                Toggle_FlareAmmoGui(false);
                Toggle_CooldownGui(false);
                Toggle_WeaponTypesGui(false);
                Toggle_DrawTargetLock(false);
                Toggle_EnemyLockGui(false);

                this.player = null;
                this.selectedWeaponType = "Disarm";
            }

            #endregion

            #region Hooks

            private void Awake()
            {
                this.weaponSettings = this.GetAvailableWeapons()["Disarm"];
                vehicle = GetComponent<BaseVehicle>();
                this.IsAlive = true;
                if (configuration.DisableFire)
                {
                    var baseHeli = this.GetComponent<BaseHelicopter>();
                    baseHeli.fireBall.guid = string.Empty;
                }
            }

            private void FixedUpdate()
            {
                if (!this.IsAlive || this.vehicle == null || this.player == null || this.player.IsDestroyed || !this.player.IsConnected)
                {
                    if (this.CH47 == null)
                    {
                        return;
                    }
                }
                ResetRoundsFiredByConfig(weaponSettings.PrimaryWeapon, lastPrimaryWeaponFiredTime, ref primaryRoundsFired, ref primaryCooldownTimer);
                ResetRoundsFiredByConfig(weaponSettings.SecondaryWeapon, lastSecondaryWeaponFiredTime, ref secondaryRoundsFired, ref secondaryCooldownTimer);
                ResetRoundsFired(weaponSettings.FlareCooldownDecay, lastFlaresFiredTime, ref flareRoundsFired, ref flareCooldownTimer);

                if (this.vehicle.HasMountPoints() && this.vehicle.GetPlayerSeat(this.player) == 0) UpdateWeapons(); 
            }

            private void OnDestroy()
            {
                this.IsAlive = false;
                this.RemovePlayer(this.player);
            }

            #endregion

            #region GUI
            private void Toggle_WeaponTypesGui(bool show)
            {
                CuiHelper.DestroyUi(this.player, weaponTypeOverlayName);

                if (!show)
                    return;

                var weaponSelectionContainer = UI.CreateElementContainer("Overlay", weaponTypeOverlayName, "0 0 0 0", "0.005 0.45", "0.049 0.6", false);
                Draw_WeaponSelectionBox(weaponSelectionContainer);

                CuiHelper.AddUi(this.player, weaponSelectionContainer);
            }

            private void Draw_WeaponSelectionBox(CuiElementContainer weaponSelectionContainer)
            {
                string disarmText = this.GetAvailableWeapons()["Disarm"].DisplayShortName;

                UI.CreatePanel(ref weaponSelectionContainer, weaponTypeOverlayName, selectedWeaponType.Equals("Disarm", StringComparison.InvariantCultureIgnoreCase) ? darkGreen : green, "0 .82", "1 1");
                UI.CreateLabel(ref weaponSelectionContainer, weaponTypeOverlayName, white, disarmText, 13, "0 .82", "1 1", TextAnchor.MiddleCenter);

                int column = 0;
                float heightMin = .62f;
                float heightMax = .80f;

                var enabledWeaponShortNames = this.GetAvailableWeapons().Where(wt => !wt.Key.Equals("Disarm", StringComparison.InvariantCultureIgnoreCase) && wt.Value.Enabled);
                foreach (var weaponType in enabledWeaponShortNames)
                {
                    float widthMin = 0f;
                    float widthMax = 1f;
                    string color = red;
                    if (configuration.DisablePermissionCheck || permissionHelper.UserHasPermission(this.player.userID.ToString(), this.GetAvailableWeapons()[weaponType.Key].WeaponPermission))
                    {
                        if (selectedWeaponType == weaponType.Key)
                            color = darkGreen;
                        else
                            color = green;
                    }
                    else if (configuration.HideUnauthorizedWeapons)
                    {
                        continue;
                    }

                    UI.CreatePanel(ref weaponSelectionContainer, weaponTypeOverlayName, color, $"{widthMin} {heightMin}", $"{widthMax} {heightMax}");
                    UI.CreateLabel(ref weaponSelectionContainer, weaponTypeOverlayName, white, weaponType.Value.DisplayShortName, 13, $"{widthMin} {heightMin}", $"{widthMax} {heightMax}", TextAnchor.MiddleCenter);
                    heightMin -= .20f;
                    heightMax -= .20f;
                }
            }

            private void Toggle_EnemyLockGui(bool show)
            {
                CuiHelper.DestroyUi(this.player, enemyLockWarningName);

                if (!show)
                    return;

                var enemyLockContainer = UI.CreateElementContainer("Overlay", enemyLockWarningName, "0 0 0 0", "0.36 0.08", "0.64 0.3", false);
                Draw_EnemyLockWarningBox(enemyLockContainer);

                CuiHelper.AddUi(this.player, enemyLockContainer);
            }

            private void Draw_EnemyLockWarningBox(CuiElementContainer enemyLockContainer)
            {
                string warningText = "Warning: Enemy Lock";
                string color = "0 0 0 0";
                if (IsLocked)
                    color = solidRed;
                string textColor = "0 0 0 0";
                if (IsLocked)
                    textColor = white;

                UI.CreatePanel(ref enemyLockContainer, enemyLockWarningName, color, "0 .82", "1 1");
                UI.CreateLabel(ref enemyLockContainer, enemyLockWarningName, textColor, warningText, 22, "0 .82", "1 1", TextAnchor.MiddleCenter);

            }

            private void Toggle_ReticleGui(bool show)
            {
                if (this.player == null)
                    return;

                CuiHelper.DestroyUi(this.player, reticleOverlayName);

                if (!show || this.weaponSettings.HudConfiguration == HUDConfiguration.None)
                    return;

                var reticleContainer = UI.CreateElementContainer("Overlay", reticleOverlayName, "0 0 0 0", this.reticleGuiOverlayMin, this.reticleGuiOverlayMax, false);
                Draw_ReticleBox(reticleContainer);
                Draw_ReticleByWeaponType(reticleContainer);

                CuiHelper.AddUi(this.player, reticleContainer);
            }

            private void UpdateTargetLock()
            {
                if (!acquiringLock && !targetLocked)
                    return;
                else if (target == null || target.IsDestroyed)
                {
                    ClearTarget();
                    return;
                }
                targetAngleY = AngleOffAroundAxis(this.vehicle.transform.forward, (target.transform.position - this.vehicle.transform.position), this.vehicle.transform.right);
                targetAngleX = AngleOffAroundAxis(this.vehicle.transform.forward, (target.transform.position - this.vehicle.transform.position), this.vehicle.transform.up);
            }

            private void Toggle_DrawTargetLock(bool show)
            {
                CuiHelper.DestroyUi(this.player, targetLockGuiName);
                CuiHelper.DestroyUi(this.player, targetMessageGuiName);

                if (!show || !acquiringLock && !targetLocked)
                    return;

                float modifiedTargetX = (targetAngleX) / 180;
                float modifiedTargetY = (targetAngleY) * -1 / 95;
                float targetBeginX = this.centerX + modifiedTargetX;
                float targetBeginY = this.centerY + modifiedTargetY;
                float targetEndX = targetBeginX + .015f;
                float targetEndY = targetBeginY + .025f;

                string color = green;
                string message = "Acquiring Target";
                if (targetLocked)
                {
                    color = red;
                    message = "Target Locked";
                }

                var messageContainer = UI.CreateElementContainer("Overlay", targetMessageGuiName, color, lockGuiMin, lockGuiMax, false);
                UI.CreateLabel(ref messageContainer, targetMessageGuiName, white, message, 13, "0.02 0", "1 1", TextAnchor.MiddleCenter);
                CuiHelper.AddUi(this.player, messageContainer);


                var reticleContainer = UI.CreateElementContainer("Overlay", targetLockGuiName, "0 0 0 0", "0 0", "1 1", false);
                UI.CreatePanel(ref reticleContainer, targetLockGuiName, color, $"{targetBeginX} {targetBeginY}", $"{targetEndX} {targetEndY}", false);
                CuiHelper.AddUi(this.player, reticleContainer);
            }

            private void Draw_ReticleBox(CuiElementContainer reticleContainer)
            {
                string color = green;
                if (IsLocked)
                    color = solidRed;
                // Left Line
                UI.CreatePanel(ref reticleContainer, reticleOverlayName, color, "0 0", "0.005 1.5", false);
                // Right Line
                UI.CreatePanel(ref reticleContainer, reticleOverlayName, color, "1 0", "1.005 1.5", false);
                // Top Line
                UI.CreatePanel(ref reticleContainer, reticleOverlayName, color, ".01 1.49", ".995 1.50", false);
                // Bottom Line
                UI.CreatePanel(ref reticleContainer, reticleOverlayName, color, ".01 0", ".995 0.01", false);
            }

            private void Draw_ReticleByWeaponType(CuiElementContainer reticleContainer)
            {
                string color = green;
                switch (weaponSettings.HudConfiguration)
                {
                    case HUDConfiguration.Bomb:
                        // Center Vertical Line
                        UI.CreatePanel(ref reticleContainer, reticleOverlayName, color, ".52 .4", "0.525 .74", false);
                        // Center Horizontal Line
                        UI.CreatePanel(ref reticleContainer, reticleOverlayName, color, ".38 .750", ".66 0.760", false);
                        break;
                    case HUDConfiguration.Shoot:
                    default:
                        // Center Vertical Line
                        UI.CreatePanel(ref reticleContainer, reticleOverlayName, color, ".52 .5", "0.525 1", false);
                        // Center Horizontal Line
                        UI.CreatePanel(ref reticleContainer, reticleOverlayName, color, ".38 .750", ".66 0.760", false);
                        break;
                }
            }

            private void Toggle_CooldownGui(bool show)
            {
                if (!show || this.selectedWeaponType.Equals("Disarm", StringComparison.InvariantCultureIgnoreCase))
                {
                    CuiHelper.DestroyUi(this.player, primaryCooldownOverlayName);
                    CuiHelper.DestroyUi(this.player, secondaryCooldownOverlayName);
                    CuiHelper.DestroyUi(this.player, flareCooldownOverlayName);

                    primaryCooldownContainerActive = false;
                    primaryCooldownContainerContrastActive = false;
                    secondaryCooldownContainerActive = false;
                    secondaryCooldownContainerContrastActive = false;
                    flareCooldownContainerActive = false;
                    flareCooldownContainerContrastActive = false;

                    return;
                }

                Draw_WeaponCooldownBox(this.weaponSettings.PrimaryWeapon, primaryRoundsFired, primaryCooldownOverlayName, ref primaryCooldownContainerActive, ref primaryCooldownContainerContrastActive);
                Draw_WeaponCooldownBox(this.weaponSettings.SecondaryWeapon, secondaryRoundsFired, secondaryCooldownOverlayName, ref secondaryCooldownContainerActive, ref secondaryCooldownContainerContrastActive);
                Draw_FlareCooldownBox();
            }

            private void Draw_FlareCooldownBox()
            {
                if (!weaponSettings.FlaresEnabled)
                    return;

                Draw_CooldownBox(flareMin, flareMax, this.weaponSettings.FlaresBeforeCooldown, flareRoundsFired, flareCooldownOverlayName, orange, ref flareCooldownContainerActive, ref flareCooldownContainerContrastActive);
            }

            private void Draw_WeaponCooldownBox(WeaponConfiguration weaponConfiguration, int roundsFired, string overlayName, ref bool cooldownContainerActive, ref bool cooldownContrastActive)
            {
                if (weaponConfiguration == null || weaponConfiguration.ProjectileType == ProjectileType.TargetLocker)
                {
                    CuiHelper.DestroyUi(this.player, overlayName);
                    cooldownContainerActive = false;
                    return;
                }

                string min = this.primaryAmmoMin;
                string max = this.primaryAmmoMax;
                if (weaponConfiguration != weaponSettings.PrimaryWeapon)
                {
                    min = this.secondaryAmmoMin;
                    max = this.secondaryAmmoMax;
                }

                Draw_CooldownBox(min, max, weaponConfiguration.ShotsBeforeCoolDown, roundsFired, overlayName, green, ref cooldownContainerActive, ref cooldownContrastActive);
            }

            private void Draw_CooldownBox(string min, string max, int shotsBeforeCooldown, float roundsFired, string overlayName, string color, ref bool cooldownContainerActive, ref bool cooldownContrastActive)
            {
                var cooldownContainer = UI.CreateElementContainer("Overlay", overlayName, clear, min, max, false);

                float panelMin = 0;
                if (shotsBeforeCooldown != 0 && roundsFired > 0)
                {
                    CuiHelper.DestroyUi(this.player, overlayName);
                    var currentTime = UnityEngine.Time.realtimeSinceStartup;
                    panelMin = (roundsFired / shotsBeforeCooldown);
                    UI.CreatePanel(ref cooldownContainer, overlayName, darkRed, $"0 0", $"{panelMin} 1");
                    cooldownContrastActive = true;
                }
                else if (cooldownContrastActive)
                {
                    CuiHelper.DestroyUi(this.player, overlayName);
                    cooldownContrastActive = false;
                }
                else if (cooldownContainerActive)
                {
                    return;
                }

                UI.CreatePanel(ref cooldownContainer, overlayName, color, $"{panelMin} 0", $"1 1");
                CuiHelper.AddUi(this.player, cooldownContainer);
                cooldownContainerActive = true;
            }

            private void Toggle_FlareAmmoGui(bool show)
            {
                CuiHelper.DestroyUi(this.player, flareAmmoOverlayName);

                if (!show || !weaponSettings.FlaresEnabled || this.selectedWeaponType.Equals("Disarm", StringComparison.InvariantCultureIgnoreCase))
                {
                    return;
                }

                Draw_AmmoText(weaponSettings.FlareShortName, flareAmmoOverlayName, flareMin, flareMax, "Flares");
            }

            private void Toggle_AmmoGui(bool show, bool? isPrimary)
            {
                if (!show || this.selectedWeaponType.Equals("Disarm", StringComparison.InvariantCultureIgnoreCase))
                {
                    CuiHelper.DestroyUi(this.player, primaryAmmoOverlayName);
                    CuiHelper.DestroyUi(this.player, secondaryAmmoOverlayName);
                    return;
                }

                bool drawPrimary = false;
                bool drawSecondary = false;
                if (isPrimary == null)
                {
                    drawPrimary = true;
                    drawSecondary = true;
                }
                else if (isPrimary.Value)
                {
                    drawPrimary = true;
                }
                else
                {
                    drawSecondary = true;
                }

                WeaponConfiguration primary = weaponSettings.PrimaryWeapon;
                if (drawPrimary && primary != null && primary.ProjectileType != ProjectileType.TargetLocker)
                {
                    CuiHelper.DestroyUi(this.player, primaryAmmoOverlayName);
                    Draw_AmmoText(primary.AmmoTypeShortName, primaryAmmoOverlayName, primaryAmmoMin, primaryAmmoMax);
                }

                WeaponConfiguration secondary = weaponSettings.SecondaryWeapon;
                if (secondary == null || secondary.ProjectileType == ProjectileType.TargetLocker)
                {
                    CuiHelper.DestroyUi(this.player, secondaryAmmoOverlayName);
                }
                else if (drawSecondary)
                {
                    CuiHelper.DestroyUi(this.player, secondaryAmmoOverlayName);
                    Draw_AmmoText(secondary.AmmoTypeShortName, secondaryAmmoOverlayName, secondaryAmmoMin, secondaryAmmoMax);
                }
            }

            private void Draw_AmmoText(string ammoShortName, string overlayName, string min, string max, string prefix = "Ammo")
            {
                var ammoContainer = UI.CreateElementContainer("Overlay", overlayName, clear, min, max, false);
                if (!configuration.UnlimitedAmmo)
                {
                    int ammoAmount = this.player.inventory.GetAmount(weaponsToAmmoTypeItemIdMap[ammoShortName]);
                    UI.CreateLabel(ref ammoContainer, overlayName, white, $"{prefix}: {ammoAmount}", 13, "0.02 0", "1 1", TextAnchor.MiddleLeft);
                }
                else
                {
                    UI.CreateLabel(ref ammoContainer, overlayName, white, $"{prefix}: ∞", 13, "0.02 0", "1 1", TextAnchor.MiddleCenter);
                }
                CuiHelper.AddUi(this.player, ammoContainer);
            }

            #endregion
        }
        #endregion

        #region AARocket
        private class AARocket : MonoBehaviour, Lock
        {
            public Guid Id { get; set; }
            public float ChanceToLoseLock { get; private set; }
            public float AngleModifier { get; set; } = 30f;

            private ServerProjectile projectile;
            private BaseEntity target;

            private void Awake()
            {
                projectile = GetComponent<ServerProjectile>();
            }

            private void FixedUpdate()
            {
                UpdateRocketPosition();
            }

            private void UpdateRocketPosition()
            {
                if (target == null || projectile == null || projectile.GetComponent<BaseEntity>().IsDestroyed)
                    return;

                Vector3 targetPos = target.transform.position + new Vector3(0, target.bounds.center.y / 2, 0);

                Vector3 targetDirection = (targetPos - projectile.transform.position).normalized;
                Vector3 currentDirection = (projectile.transform.localRotation * Vector3.forward);

                float maxAngleChange = (AngleModifier * UnityEngine.Time.fixedDeltaTime);
                float angleChange = Vector3.Angle(targetDirection, currentDirection);
                float maxChange = maxAngleChange < angleChange ? (maxAngleChange / angleChange) : 1;
                Vector3 change = Vector3.Slerp(currentDirection, targetDirection, maxChange);
                Vector3 modifiedDirection = change.normalized;

                projectile.InitializeVelocity(modifiedDirection * projectile.speed);
            }

            private void OnDestroy()
            {
                BaseEntity entity = projectile.GetComponent<BaseEntity>();

                if (entity != null && !entity.IsDestroyed)
                    entity.Kill();

                var miniCopterWrapper = this.target?.GetComponent<HelicopterWrapper>();
                if (miniCopterWrapper != null)
                    miniCopterWrapper.TriggerLockLost(this);

                CancelInvoke();
            }

            public void Initialize(BaseEntity target, float chanceToLoseLock)
            {
                this.Id = Guid.NewGuid();
                this.ChanceToLoseLock = chanceToLoseLock;

                SetLockTarget(target);
            }

            public void SetLockTarget(BaseEntity target)
            {
                var miniCopterWrapper = this.target?.GetComponent<HelicopterWrapper>();
                if (miniCopterWrapper != null)
                    miniCopterWrapper.TriggerLockLost(this);

                this.target = target;

                miniCopterWrapper = this.target.GetComponent<HelicopterWrapper>();
                if (miniCopterWrapper != null)
                    miniCopterWrapper.TriggerLockAcquired(this);
            }
        }
        #endregion

        #region Countermeasures
        private class CounterMeasure : MonoBehaviour
        {
            private BaseEntity baseEntity;
            private Vector3 lastPosition;
            private float timeAlive = 0.0f;
            private float sparkSpawnLimiter = 1f;
            protected static Effect reusableInstance = new Effect();

            private void Awake()
            {
                baseEntity = GetComponent<BaseEntity>();
                lastPosition = baseEntity.transform.position;
                timeAlive = UnityEngine.Time.realtimeSinceStartup;
            }

            private void FixedUpdate()
            {
                var currentTime = UnityEngine.Time.realtimeSinceStartup;
                if (currentTime > (timeAlive + configuration.CounterMeasureDespawnTime) || lastPosition == baseEntity.transform.position)
                    Destroy(this);

                if (currentTime < (timeAlive + sparkSpawnLimiter))
                {
                    reusableInstance.Clear();

                    Vector3 posModifier;
                    switch (UnityEngine.Random.Range(0, 5))
                    {
                        case 0:
                            posModifier = Vector3.forward;
                            break;
                        case 1:
                            posModifier = Vector3.left;
                            break;
                        case 2:
                            posModifier = Vector3.right;
                            break;
                        case 3:
                            posModifier = Vector3.up;
                            break;
                        case 4:
                            posModifier = Vector3.down;
                            break;
                        case 5:
                            posModifier = Vector3.back;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    reusableInstance.Init(Effect.Type.Generic, baseEntity, 0, posModifier * 2, new Vector3(), null);
                    reusableInstance.scale = 5f;
                    reusableInstance.pooledString = "assets/prefabs/misc/orebonus/effects/hotspot_death.prefab";
                    EffectNetwork.Send(reusableInstance);
                }

                lastPosition = baseEntity.transform.position;
            }

            private void OnDestroy()
            {
                if (baseEntity != null && !baseEntity.IsDestroyed)
                    baseEntity.Kill();
            }
        }
        #endregion

        #region UI

        public class UI
        {
            static public CuiElementContainer CreateElementContainer(string parent, string panelName, string color, string aMin, string aMax, bool useCursor)
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
                return NewElement;
            }

            static public void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }


            static public void CreatePanel(ref CuiElementContainer container, string panel, string color, RectTransform rectTransform, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = rectTransform.anchorMin.ToString(), AnchorMax = rectTransform.anchorMax.ToString() },
                    CursorEnabled = cursor
                },
                panel);
            }

            static public void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }
        }

        #endregion

        public class WeaponConfiguration
        {
            public string AmmoTypeShortName { get; set; }
            public string AmmoPrefabPath { get; set; }
            public string MuzzleEffect { get; set; } = "assets/prefabs/npc/patrol helicopter/effects/gun_fire.prefab";

            public ProjectileType ProjectileType { get; set; } = ProjectileType.ServerProjectile;

            public float WeaponSpeed { get; set; }
            public float FireRate { get; set; }
            public float MinBlastRadius { get; set; }
            public float MaxBlastRadius { get; set; }
            [JsonProperty(PropertyName = "CoolDownDecay: Speed at which the cooldown is decreased in seconds")]
            public float CoolDownDecay { get; set; } = 0.0f;

            [JsonProperty(PropertyName = "Min Detonation Time for Timed Explosives")]
            public float DetonationTimerMin { get; set; } = 0;
            [JsonProperty(PropertyName = "Max Detonation Time for Timed Explosives")]
            public float DetonationTimerMax { get; set; } = 0;
            [JsonProperty(PropertyName = "Air2Air: Angle Modifier for Rockets (Set to Override Default 30)")]
            public float AngleModifier { get; set; } = 0.0f;
            [JsonProperty(PropertyName = "TargetLocker: Time Before Losing the Lock")]
            public float TimeBeforeLockExpires { get; set; } = 0.0f;
            [JsonProperty(PropertyName = "TargetLocker: Time Required Before Locking Target")]
            public float TimeToLock { get; set; } = 0.0f;
            public float AimCone { get; set; } = 0.0f;

            public bool RequiresAmmo { get; set; } = true;
            public bool ClearAATargetOnFire { get; set; } = true;
            public bool AllowCH47HSR { get; set; } = false;
            public bool AllowPatrolHeliHSR { get; set; } = false;

            public int ShotsBeforeCoolDown { get; set; } = 0;

            public BarrelConfiguration BarrelConfiguration { get; set; } = BarrelConfiguration.DualFront;

            public List<DamageTypeEntry> DamageTypes { get; set; } = new List<DamageTypeEntry>();
        }

        public class WeaponSettings
        {
            public WeaponConfiguration PrimaryWeapon { get; set; } = null;
            public WeaponConfiguration SecondaryWeapon { get; set; } = null;

            public string DisplayShortName { get; set; }
            public string DisplayFullName { get; set; }
            public string WeaponPermission { get; set; }
            public string FlareShortName { get; set; } = "flare";

            public HUDConfiguration HudConfiguration { get; set; } = HUDConfiguration.Shoot;

            public float FlareFireRate { get; set; } = 1f;
            public float FlareCooldownDecay { get; set; } = 3f;
            public int FlaresBeforeCooldown { get; set; } = 6;

            [JsonProperty(PropertyName = "TargetLocker/Air2Air: % Chance to Lose Lock [0-100]")]
            public float ChanceToLoseLock { get; set; } = 10.0f;

            public bool UnlimitedFlares { get; set; } = false;
            public bool FlaresEnabled { get; set; } = true;

            public bool Enabled { get; set; }
        }

        public enum BarrelConfiguration
        {
            DualFront,
            Side,
            Bottom,
            CenterFront,
            SideOuter,
            Gattling,
            CenterBack
        }

        public enum HUDConfiguration
        {
            None,
            Shoot,
            Bomb
        }

        public enum ProjectileType
        {
            ServerProjectile,
            Bullet,
            Air2Air,
            TargetLocker
        }

        public interface Lock
        {
            Guid Id { get; set; }
            float ChanceToLoseLock { get; }
            void SetLockTarget(BaseEntity target);
        }
    }
}