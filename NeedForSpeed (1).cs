using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Need For Speed", "SwenenzY", "1.0.5")]
    [Description("Changes Speed / Mechanical of minicopter, scrap copter ")]
    public class NeedForSpeed : RustPlugin
    {
        
        #region Config

        private static ConfigData config;
        
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Permission -> Settings")]
            public Dictionary<string, PluginSettings> permissions;
        }

        private class PluginSettings
        {
            // MiniCopter
            [JsonProperty(PropertyName = "MiniCopter: Torque Scale Pitch")]
            public float mTSP;
            [JsonProperty(PropertyName = "MiniCopter: Torque Scale Yaw")]
            public float mTSY;
            [JsonProperty(PropertyName = "MiniCopter: Torque Scale Roll")] 
            public float mTSR;
            [JsonProperty(PropertyName = "MiniCopter: Maximum Rotor Speed")]
            public float mMRS;
            [JsonProperty(PropertyName = "MiniCopter: Time Until Maximum Rotor Speed")]
            public float mTUMRS;
            [JsonProperty(PropertyName = "MiniCopter: Rotor Blur Threshold")] 
            public float mRBT;
            [JsonProperty(PropertyName = "MiniCopter: Motor Force Constant")]
            public float mMFC;
            [JsonProperty(PropertyName = "MiniCopter: Brake Force Constant")]
            public float mBFC;
            [JsonProperty(PropertyName = "MiniCopter: Fuel Per Second")] 
            public float mFPS;
            [JsonProperty(PropertyName = "MiniCopter: Thwop Gain Min")]
            public float mTGMi;
            [JsonProperty(PropertyName = "MiniCopter: Thwop Gain Max")]
            public float mTGMa;
            [JsonProperty(PropertyName = "MiniCopter: Lift Fraction")]
            public float mLF;
            // ScrapCopter
            [JsonProperty(PropertyName = "ScrapCopter: Torque Scale Pitch")]
            public float cTSP;
            [JsonProperty(PropertyName = "ScrapCopter: Torque Scale Yaw")]
            public float cTSY;
            [JsonProperty(PropertyName = "ScrapCopter: Torque Scale Roll")] 
            public float cTSR;
            [JsonProperty(PropertyName = "ScrapCopter: Maximum Rotor Speed")]
            public float cMRS;
            [JsonProperty(PropertyName = "ScrapCopter: Time Until Maximum Rotor Speed")]
            public float cTUMRS;
            [JsonProperty(PropertyName = "ScrapCopter: Rotor Blur Threshold")] 
            public float cRBT;
            [JsonProperty(PropertyName = "ScrapCopter: Motor Force Constant")]
            public float cMFC;
            [JsonProperty(PropertyName = "ScrapCopter: Brake Force Constant")]
            public float cBFC;
            [JsonProperty(PropertyName = "ScrapCopter: Fuel Per Second")] 
            public float cFPS;
            [JsonProperty(PropertyName = "ScrapCopter: Thwop Gain Min")]
            public float cTGMi;
            [JsonProperty(PropertyName = "ScrapCopter: Thwop Gain Max")]
            public float cTGMa;
            [JsonProperty(PropertyName = "ScrapCopter: Lift Fraction")]
            public float cLF;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData 
            {
                permissions = new Dictionary<string, PluginSettings>
                {
                    ["needforspeed.3"] = new PluginSettings
                    {

                        mTSP = 400f,
                        mTSY = 400f,
                        mTSR = 400f,
                        mMRS = 10f,
                        mTUMRS = 7f,
                        mRBT = 8f,
                        mMFC = 150f,
                        mBFC = 500f,
                        mFPS = 0.50f,
                        mTGMi = 0.50f,
                        mTGMa = 1f,
                        mLF = 0.25f,

                        cTSP = 1600f,
                        cTSY = 1600f,
                        cTSR = 1600f,
                        cMRS = 10f,
                        cTUMRS = 7f,
                        cRBT = 8f,
                        cMFC = 150f,
                        cBFC = 500f,
                        cFPS = 0.50f,
                        cTGMi = 0.50f,
                        cTGMa = 1f,
                        cLF = 0.25f
                    },
                    ["needforspeed.2"] = new PluginSettings
                    {

                        mTSP = 400f,
                        mTSY = 400f,
                        mTSR = 400f,
                        mMRS = 10f,
                        mTUMRS = 7f,
                        mRBT = 8f,
                        mMFC = 150f,
                        mBFC = 500f,
                        mFPS = 0.50f,
                        mTGMi = 0.50f,
                        mTGMa = 1f,
                        mLF = 0.25f,

                        cTSP = 1600f,
                        cTSY = 1600f,
                        cTSR = 1600f,
                        cMRS = 10f,
                        cTUMRS = 7f,
                        cRBT = 8f,
                        cMFC = 150f,
                        cBFC = 500f,
                        cFPS = 0.50f,
                        cTGMi = 0.50f,
                        cTGMa = 1f,
                        cLF = 0.25f
                    },
                    ["needforspeed.1"] = new PluginSettings
                    {

                        mTSP = 400f,
                        mTSY = 400f,
                        mTSR = 400f,
                        mMRS = 10f,
                        mTUMRS = 7f,
                        mRBT = 8f,
                        mMFC = 150f,
                        mBFC = 500f,
                        mFPS = 0.50f,
                        mTGMi = 0.50f,
                        mTGMa = 1f,
                        mLF = 0.25f,

                        cTSP = 1600f,
                        cTSY = 1600f,
                        cTSR = 1600f,
                        cMRS = 10f,
                        cTUMRS = 7f,
                        cRBT = 8f,
                        cMFC = 150f,
                        cBFC = 500f,
                        cFPS = 0.50f,
                        cTGMi = 0.50f,
                        cTGMa = 1f,
                        cLF = 0.25f
                    },
                }
            };
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
   
            try
            {
                config = Config.ReadObject<ConfigData>();
        
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }
        
        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
            

        #endregion
        private void Init()
        {
            foreach (var value in config.permissions.Keys)
            {
                permission.RegisterPermission(value, this);
            }
        }

        private void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            if (entity != null)
            {
                foreach (var pair in config.permissions)
                {
                    if (permission.UserHasPermission(player.UserIDString, pair.Key))
                    {
                        PluginSettings data = pair.Value;
                        var vehicle = player.GetMountedVehicle();
                        BasePlayer driver = vehicle.GetDriver();
                        if (driver == null) return;
                        if (vehicle == null) return;
                        if (driver.userID != player.userID) return;
                        if (vehicle as ScrapTransportHelicopter)
                        {
                            var scrap = vehicle as ScrapTransportHelicopter;
                            if (scrap == null) return;
                            var torqueScalePitch = data.cTSP;
                            var torqueScaleYaw = data.cTSY;
                            var torqueScaleRoll = data.cTSR;
                            scrap.torqueScale = new Vector3(torqueScalePitch, torqueScaleYaw, torqueScaleRoll);
                            scrap.maxRotorSpeed = data.cMRS;
                            scrap.timeUntilMaxRotorSpeed = data.cTUMRS;
                            scrap.rotorBlurThreshold = data.cRBT;
                            scrap.motorForceConstant = data.cMFC;
                            scrap.brakeForceConstant = data.cBFC;
                            scrap.fuelPerSec = data.cFPS;
                            scrap.thwopGainMin = data.cTGMi;
                            scrap.thwopGainMax = data.cTGMa;
                            scrap.liftFraction = data.cLF;
                            scrap.SendNetworkUpdate();
                        }
                        else if (vehicle as MiniCopter)
                        {
                            var copter = vehicle as MiniCopter;
                            if (copter == null) return;
                            var torqueScalePitch = data.mTSP;
                            var torqueScaleYaw = data.mTSY;
                            var torqueScaleRoll = data.mTSR;
                            copter.torqueScale = new Vector3(torqueScalePitch, torqueScaleYaw, torqueScaleRoll);
                            copter.maxRotorSpeed = data.mMRS;
                            copter.timeUntilMaxRotorSpeed = data.mTUMRS;
                            copter.rotorBlurThreshold = data.mRBT;
                            copter.motorForceConstant = data.mMFC;
                            copter.brakeForceConstant = data.mBFC;
                            copter.fuelPerSec = data.mFPS;
                            copter.thwopGainMin = data.mTGMi;
                            copter.thwopGainMax = data.mTGMa;
                            copter.liftFraction = data.mLF;
                            copter.SendNetworkUpdate();
                        }
                        break;
                    }
                }
            }
        }
      
    }
}
