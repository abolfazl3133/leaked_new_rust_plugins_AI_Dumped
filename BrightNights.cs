
using Network;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Facepunch;
using ProtoBuf;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("BrightNights", "Whispers88", "1.0.5")]
    [Description("Makes Nights Brighter")]
    public class BrightNights : CovalencePlugin
    {
        private Dictionary<float, byte[]> _brightnessBytes = new Dictionary<float, byte[]>();
        PointEntity? _envSync;
        string permadmin = "brightnights.admin";

        #region Configuration
        private Configuration config;

        public class Configuration
        {
            [JsonProperty("Max Night Brightness")]
            public float brightness = 5f;

            [JsonProperty("Night Contrast (Lower = brighter, Default = 1.5f)")]
            public float contrast = 1f;

            [JsonProperty("Update Transition Rate Seconds (Time taken between transition updates)")]
            public float updateRate = 0.2f;

            [JsonProperty("Update Amount (Amount to update by to reach target brightness)")]
            public float updateAmt = 0.05f;

            [JsonProperty("Night Delay (Delay till transition after sunset in seconds)")]
            public float transitiondelay = 1f;

            [JsonProperty("Inverted Nights Mode (Makes nights very bright)")]
            public bool invertedNight = false;

            [JsonProperty("Inverted Nights Sky Darkness (lower is darker)")]
            public float invertedNightDarkness = 0.01f;

            [JsonProperty("Inverted Nights Environment Darkness (1 Dark, 0 Bright)")]
            public float invertedNightEnvDark = 0.9f;

            [JsonProperty("Inverted Nights Start Position 0-100 (increases the starting pos of the bright moon)")]
            public int invertedNightstartPos = 0;

            [JsonProperty("Use Night Light (Adds night vision to immediate proximity)")]
            public bool nightLight = false;

            [JsonProperty("Nightlight Brightness (Adds a night light around the player)")]
            public float nightLightBrightness = 0.1f;

            [JsonProperty("Nightlight Distance (Modifies the throw of the night light)")]
            public float nightLightDist = 7f;

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            LogWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }
        #endregion Configuration

        #region Init
        WaitForSeconds waitForSeconds;
        private float _contrastupdateamt;
        private int _invertedNightStartTimeHrs;
        private int _invertedNightStartTimeMins;

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permadmin, this);
            AddCovalenceCommand("settime", "SetTimeCMD");

            if (TOD_Sky.Instance == null)
            {
                Puts("Sky Not Init retrying in 15");
                ServerMgr.Instance.Invoke(OnServerInitialized, 15f);
                return;
            }

            _envSync = BaseNetworkable.serverEntities.OfType<EnvSync>().FirstOrDefault();
            if (_envSync == null)
            {
                Puts("EnvSync not found, retrying in 15");
                ServerMgr.Instance.Invoke(OnServerInitialized, 15f);
                return;
            }

            updateNightCycle = UpdateNightCycle;
            invertNightCycle = UpdateInvertedNightCycle;

            waitForSeconds = CoroutineEx.waitForSeconds(config.updateRate);


            TOD_Sky.Instance.Components.Time.OnSunrise += SunRiseHandle;
            TOD_Sky.Instance.Components.Time.OnSunset += SunSetHandle;

            TOD_Sky.Instance.Moon.Position = TOD_MoonPositionType.OppositeToSun;


            _contrastupdateamt = (config.contrast - 1.5f) / (config.brightness / config.updateAmt);

            for (int i = 0; i < config.brightness / config.updateAmt; i++)
            {
                float brightness = (float)Math.Round(config.brightness - config.updateAmt * i, 2);
                float contrast = config.contrast - _contrastupdateamt * i;
                _brightnessBytes.Add(brightness, CreateWrite(brightness, contrast));
            }

            if (config.invertedNight)
            {
                float starttime = 7.75f + 4.25f * config.invertedNightstartPos / 100;
                _invertedNightStartTimeHrs = (int)starttime;
                _invertedNightStartTimeMins = (int)Math.Round((starttime - _invertedNightStartTimeHrs) * 60);
                _invertedNightBytes = CreateInvertedNightWrite(0f, 1f, 0.99f, 0f, 0f, config.invertedNightEnvDark);
                _revertNightBytes = CreateInvertedNightWrite(-1f, -1f, -1f, -1f, -1f, -1f);
                TOD_Sky.Instance.Components.Time.OnHour += HourlyInvertedTimeUpdate;
            }

            if (config.nightLight)
            {
                _NightLightEnabledBytes = CreateNightLightWrite(true, config.nightLightBrightness, config.nightLightDist);
                _NightLightDisabledBytes = CreateNightLightWrite(false, 0.0175f, 7f);
                if (config.nightLight)
                    SendNightLightUpdateAll(_NightLightEnabledBytes);
            }

            if (TOD_Sky.Instance.IsNight)
            {
                if (config.invertedNight)
                {
                    _envSync._limitedNetworking = true;
                    UpdateBrightnessAll(config.invertedNightDarkness, config.contrast);
                    SendInvertedNightUpdateAll(_invertedNightBytes);
                    DateTime currentitme = TOD_Sky.Instance.Cycle.DateTime;
                    int adjustedHour = (currentitme.Hour + 24 - 12) % 24;
                    if (adjustedHour > _invertedNightStartTimeHrs)
                        SendNightNetworkUpdateAll(6, 21, adjustedHour, currentitme.Minute);
                    else
                        SendNightNetworkUpdateAll(6, 21, _invertedNightStartTimeHrs, _invertedNightStartTimeMins);
                    return;
                }
                UpdateBrightnessAll(config.brightness, config.contrast);
                StartNightCycle();
            }
        }

        #endregion Init

        #region Methods
        IEnumerator setTargetCoroutine = null;
        private void SunRiseHandle()
        {
            Climate climate = SingletonComponent<Climate>.Instance;
            if (climate.WeatherStateTarget == null)
                return;

            StopNightCycle();

            if (setTargetCoroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(setTargetCoroutine);
            }
            setTargetCoroutine = UpdateBrightness(climate, 1f);
            ServerMgr.Instance.StartCoroutine(setTargetCoroutine);
        }

        private void SunSetHandle()
        {
            Climate climate = SingletonComponent<Climate>.Instance;
            if (climate.WeatherOverrides == null)
                return;

            climate.WeatherOverrides.Atmosphere.Brightness = 1;
            if (config.invertedNight)
            {
                StartInvertedNightCycle();
                setTargetCoroutine = UpdateBrightness(climate, config.invertedNightDarkness);
                ServerMgr.Instance.StartCoroutine(setTargetCoroutine);
                return;
            }

            TOD_Sky.Instance.Moon.Position = TOD_MoonPositionType.OppositeToSun;

            StartNightCycle();

            if (setTargetCoroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(setTargetCoroutine);
            }

            setTargetCoroutine = UpdateBrightness(climate, config.brightness);
            ServerMgr.Instance.StartCoroutine(setTargetCoroutine);
        }

        private IEnumerator UpdateBrightness(Climate climate, float target = 1f)
        {
            if (target > climate.WeatherOverrides.Atmosphere.Brightness) //going to night
            {
                if (climate.WeatherOverrides.Atmosphere.Contrast < 1)
                {
                    climate.WeatherOverrides.Atmosphere.Contrast = 1.5f;
                }
                else if (climate.WeatherOverrides.Atmosphere.Contrast > 1.5f)
                {
                    climate.WeatherOverrides.Atmosphere.Contrast = 1.5f;
                }
                while (climate.WeatherOverrides.Atmosphere.Brightness < target)
                {
                    climate.WeatherOverrides.Atmosphere.Brightness += config.updateAmt;
                    climate.WeatherOverrides.Atmosphere.Contrast += _contrastupdateamt;
                    UpdateBrightnessAll((float)Math.Round(climate.WeatherOverrides.Atmosphere.Brightness, 2), climate.WeatherOverrides.Atmosphere.Contrast);
                    yield return new WaitForSeconds(1f);
                }
            }
            else // going to day
            {
                while (climate.WeatherOverrides.Atmosphere.Brightness > target)
                {
                    climate.WeatherOverrides.Atmosphere.Brightness -= config.updateAmt;
                    climate.WeatherOverrides.Atmosphere.Contrast -= _contrastupdateamt;
                    UpdateBrightnessAll((float)Math.Round(climate.WeatherOverrides.Atmosphere.Brightness, 2), climate.WeatherOverrides.Atmosphere.Contrast);
                    yield return new WaitForSeconds(1f);
                }
            }
            setTargetCoroutine = null;
        }

        private void UpdateBrightnessAll(float brightness, float contrast)
        {
            if (!_brightnessBytes.TryGetValue(brightness, out byte[] bytes))
            {
                bytes = CreateWrite(brightness, contrast);
                _brightnessBytes.Add(brightness, bytes);
            }
            if (!Net.sv.IsConnected()) return;
            NetWrite netWrite = Net.sv.StartWrite();
            netWrite.Write(bytes, 0, bytes.Length);
            netWrite.Send(new SendInfo(_envSync.net.group.subscribers));
        }

        private void UpdateBrightnessPlayer(Connection connection, float brightness, float contrast)
        {
            if (!_brightnessBytes.TryGetValue(brightness, out byte[] bytes))
            {
                bytes = CreateWrite(brightness, contrast);
                _brightnessBytes.Add(brightness, bytes);
            }
            if (!Net.sv.IsConnected() || connection == null) return;
            NetWrite netWrite = Net.sv.StartWrite();
            netWrite.Write(bytes, 0, bytes.Length);
            netWrite.Send(new SendInfo(connection));
        }

        private byte[] CreateWrite(float brightness, float contrast)
        {
            NetWrite netWrite = Net.sv.StartWrite();
            netWrite.PacketID(Message.Type.ConsoleReplicatedVars);
            netWrite.UInt32(2);
            netWrite.String("weather.atmosphere_brightness");
            netWrite.String(brightness.ToString());
            netWrite.String("weather.atmosphere_contrast");
            netWrite.String(contrast.ToString());
            byte[] bytes = new byte[netWrite.Data.Length];
            Buffer.BlockCopy(netWrite.Data, 0, bytes, 0, (int)netWrite.Length);
            Pool.Free(ref netWrite);
            return bytes;
        }

        #region InvertedNights
        byte[] _invertedNightBytes;
        byte[] _revertNightBytes;
        private byte[] CreateInvertedNightWrite(float size, float coverage, float opacity, float scattering, float rainchance, float fog)
        {
            NetWrite netWrite = Net.sv.StartWrite();
            netWrite.PacketID(Message.Type.ConsoleReplicatedVars);
            netWrite.UInt32(6);
            netWrite.String("weather.cloud_size");
            netWrite.String($"{size}");
            netWrite.String("weather.cloud_coverage");
            netWrite.String($"{coverage}");
            netWrite.String("weather.cloud_opacity");
            netWrite.String($"{opacity}");
            netWrite.String("weather.cloud_scattering");
            netWrite.String($"{scattering}");
            netWrite.String("weather.rain_chance");
            netWrite.String($"{rainchance}");
            netWrite.String("weather.fog");
            netWrite.String($"{fog}");
            byte[] bytes = new byte[netWrite.Data.Length];
            Buffer.BlockCopy(netWrite.Data, 0, bytes, 0, (int)netWrite.Length);
            Pool.Free(ref netWrite);
            return bytes;
        }

        private void SendInvertedNightUpdate(Connection connection, byte[] bytes)
        {
            if (!Net.sv.IsConnected() || connection == null) return;
            NetWrite netWrite = Net.sv.StartWrite();
            netWrite.Write(bytes, 0, bytes.Length);
            netWrite.Send(new SendInfo(connection));
        }

        private void SendInvertedNightUpdateAll(byte[] bytes)
        {
            if (!Net.sv.IsConnected()) return;
            NetWrite netWrite = Net.sv.StartWrite();
            netWrite.Write(bytes, 0, bytes.Length);
            netWrite.Send(new SendInfo(_envSync.net.group.subscribers));
        }

        #endregion InvertedNights

        #region NightLight
        byte[] _NightLightEnabledBytes;
        byte[] _NightLightDisabledBytes;
        private byte[] CreateNightLightWrite(bool enabled, float brightness, float distance)
        {
            NetWrite netWrite = Net.sv.StartWrite();
            netWrite.PacketID(Message.Type.ConsoleReplicatedVars);
            netWrite.UInt32(3);
            netWrite.String("env.nightlight_enabled");
            netWrite.String(enabled.ToString());
            netWrite.String("env.nightlight_brightness");
            netWrite.String(brightness.ToString());
            netWrite.String("env.nightlight_distance");
            netWrite.String(distance.ToString());
            byte[] bytes = new byte[netWrite.Data.Length];
            Buffer.BlockCopy(netWrite.Data, 0, bytes, 0, (int)netWrite.Length);
            Pool.Free(ref netWrite);
            return bytes;
        }

        private void SendNightLightUpdate(Connection connection, byte[] bytes)
        {
            if (!Net.sv.IsConnected() || connection == null) return;
            NetWrite netWrite = Net.sv.StartWrite();
            netWrite.Write(bytes, 0, bytes.Length);
            netWrite.Send(new SendInfo(connection));
        }

        private void SendNightLightUpdateAll(byte[] bytes)
        {
            if (!Net.sv.IsConnected()) return;
            NetWrite netWrite = Net.sv.StartWrite();
            netWrite.Write(bytes, 0, bytes.Length);
            netWrite.Send(new SendInfo(_envSync.net.group.subscribers));
        }
        #endregion NightLight

        #endregion Methods

        #region Hooks
        private void Unload()
        {
            if (_envSync != null)
                _envSync._limitedNetworking = false;

            if (setTargetCoroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(setTargetCoroutine);
            }
            if (ServerMgr.Instance.IsInvoking(updateNightCycle))
            {
                ServerMgr.Instance.CancelInvoke(updateNightCycle);
            }
            if (ServerMgr.Instance.IsInvoking(invertNightCycle))
            {
                ServerMgr.Instance.CancelInvoke(invertNightCycle);
            }
            if (ServerMgr.Instance.IsInvoking(_setNightAction))
            {
                ServerMgr.Instance.CancelInvoke(_setNightAction);
            }

            UpdateBrightnessAll(1f, 1.5f);

            if (config.invertedNight)
            {
                TOD_Sky.Instance.Components.Time.OnHour -= HourlyInvertedTimeUpdate;
                SendInvertedNightUpdateAll(_revertNightBytes);
            }

            if (config.nightLight)
            {
                SendInvertedNightUpdateAll(_NightLightDisabledBytes);
            }

            TOD_Sky.Instance.Components.Time.OnSunrise -= SunRiseHandle;
            TOD_Sky.Instance.Components.Time.OnSunset -= SunSetHandle;
            TOD_Sky.Instance.Moon.Position = TOD_MoonPositionType.Realistic;
        }
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            SetUpConnectingPlayer(player);
        }

        private void SetUpConnectingPlayer(BasePlayer player)
        {
            Climate climate = SingletonComponent<Climate>.Instance;
            if (climate == null) return;

            if (TOD_Sky.Instance?.IsNight ?? false)
            {
                if (_envSync == null) return;
                UpdateBrightnessPlayer(player.net.connection, climate.WeatherOverrides.Atmosphere.Brightness, climate.WeatherOverrides.Atmosphere.Contrast);

                if (config.invertedNight)
                {
                    DateTime currentitme = TOD_Sky.Instance.Cycle.DateTime;
                    int adjustedHour = (currentitme.Hour + 24 - 12) % 24;
                    if (adjustedHour > _invertedNightStartTimeHrs)
                    {
                        long date = GetDate(TOD_Sky.Instance.Cycle.DateTime, 6, 21, adjustedHour, currentitme.Minute);
                        UpdatePlayerDateTime(player.Connection, date);
                    }
                    else
                    {
                        long date = GetDate(TOD_Sky.Instance.Cycle.DateTime, 6, 21, _invertedNightStartTimeHrs, _invertedNightStartTimeMins);
                        UpdatePlayerDateTime(player.Connection, date);
                    }
                    SendInvertedNightUpdate(player.net.connection, _invertedNightBytes);
                    return;
                }
                UpdatePlayerDateTime(player.net.connection, GetDate(TOD_Sky.Instance.Cycle.DateTime, 6, 21));
            }

            if (config.nightLight)
                SendNightLightUpdate(player.net.connection, _NightLightEnabledBytes);
        }
        #endregion Hooks

        #region NightTime Handling

        private long GetDate(DateTime dateTime, int month, int day, int hour = -1, int minutes = -1)
        {
            return new DateTime(dateTime.Year, month, day, hour == -1 ? dateTime.Hour : hour, minutes == -1 ? dateTime.Minute : minutes, dateTime.Second, DateTimeKind.Utc).ToBinary();
        }

        private void UpdatePlayerDateTime(Connection connection, long date)
        {
            NetWrite netWrite = Net.sv.StartWrite();
            connection.validate.entityUpdates++;
            BaseNetworkable.SaveInfo saveInfo = new BaseNetworkable.SaveInfo
            {
                forConnection = connection,
                forDisk = false
            };
            netWrite.PacketID(Message.Type.Entities);
            netWrite.UInt32(connection.validate.entityUpdates);
            using (saveInfo.msg = Pool.Get<Entity>())
            {
                _envSync.Save(saveInfo);
                saveInfo.msg.environment.dateTime = date;
                saveInfo.msg.ToProto(netWrite);
                _envSync.PostSave(saveInfo);
                netWrite.Send(new SendInfo(connection));
            }
        }

        private void StartNightCycle()
        {
            if (_envSync != null)
                _envSync._limitedNetworking = true;
            ServerMgr.Instance.Invoke(updateNightCycle, config.transitiondelay);
        }

        private void StartInvertedNightCycle()
        {
            if (_envSync == null) return;

            _envSync._limitedNetworking = true;
            ServerMgr.Instance.Invoke(invertNightCycle, config.transitiondelay);
        }

        Action? invertNightCycle;
        private void UpdateInvertedNightCycle()
        {
            if (_envSync == null) return;
            DateTime currentitme = TOD_Sky.Instance.Cycle.DateTime;
            int adjustedHour = (currentitme.Hour + 24 - 12) % 24;
            if (adjustedHour > _invertedNightStartTimeHrs)
            {
                SendNightNetworkUpdateAll(6, 21, adjustedHour, currentitme.Minute);
            }
            else
            {
                SendNightNetworkUpdateAll(6, 21, _invertedNightStartTimeHrs, _invertedNightStartTimeMins);
            }
            SendInvertedNightUpdateAll(_invertedNightBytes);
        }

        private void HourlyInvertedTimeUpdate()
        {
            if (_envSync == null || !_envSync._limitedNetworking) return;
            DateTime currentitme = TOD_Sky.Instance.Cycle.DateTime;
            int adjustedHour = (currentitme.Hour + 24 - 12) % 24;
            if (adjustedHour <= _invertedNightStartTimeHrs)
                return;
            SendNightNetworkUpdateAll(6, 21, adjustedHour, currentitme.Minute);
        }

        private void StopNightCycle()
        {
            if (_envSync != null)
                _envSync._limitedNetworking = false;

            if (config.invertedNight)
                SendInvertedNightUpdateAll(_revertNightBytes);
        }

        Action? updateNightCycle;
        private void UpdateNightCycle()
        {
            if (_envSync == null) return;
            SendNightNetworkUpdateAll(6, 21);
        }

        private void SendNightNetworkUpdateAll(int month, int day, int hour = -1, int minutes = -1)
        {
            long date = GetDate(TOD_Sky.Instance.Cycle.DateTime, month, day, hour, minutes);
            foreach (var connection in _envSync.net.group.subscribers)
            {
                UpdatePlayerDateTime(connection, date);
            }
        }

        #endregion NightTime Handling

        #region Commands
        private void SetTimeCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)iplayer.Object;
            if (player == null) return;

            if (!HasPerm(player.UserIDString, permadmin))
            {
                player.ChatMessage("You do not have permission to use this command");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Usage: /settime <hour> or /settime sunset/sunrise");
                return;
            }

            if (args.Length == 1)
            {
                if (args[0].ToLower() == "sunset")
                {
                    iplayer.Message("Setting Time to Sunset");
                    RefreshTime(19.1f);
                    return;
                }
                if (args[0].ToLower() == "sunrise")
                {
                    SunRiseHandle();
                    RefreshTime(7.25f);
                    return;
                }

                if (!float.TryParse(args[0], out float hour))
                {
                    player.ChatMessage("Invalid Hour");
                    return;
                }

                RefreshTime(hour);
                iplayer.Message($"Setting Time to {hour} hrs");
                return;

            }
            player.ChatMessage("Usage: /settime <hour> or /settime sunset/sunrise");
            return;
        }

        private void RefreshTime(float hour)
        {
            //Reset
            bool wasnight = TOD_Sky.Instance.IsNight;
            Climate climate = SingletonComponent<Climate>.Instance;
            climate.WeatherOverrides.Atmosphere.Contrast = config.contrast;
            climate.WeatherOverrides.Atmosphere.Brightness = config.brightness;

            UpdateBrightnessAll(climate.WeatherOverrides.Atmosphere.Brightness, climate.WeatherOverrides.Atmosphere.Contrast);

            if (config.invertedNight)
            {
                TOD_Sky.Instance.Components.Time.OnHour -= HourlyInvertedTimeUpdate;
                SendInvertedNightUpdateAll(_revertNightBytes);
            }

            if (config.nightLight)
                SendNightLightUpdateAll(_NightLightDisabledBytes);

            ConsoleSystem.Run(ConsoleSystem.Option.Server, $"env.time {hour}");

            DateTime currentitme = TOD_Sky.Instance.Cycle.DateTime;

            foreach (var connection in _envSync.net.group.subscribers)
            {
                UpdatePlayerDateTime(connection, GetDate(currentitme, currentitme.Month, currentitme.Day));
            }

            if (_setNightAction == null)
                _setNightAction = SetNightAction;

            ServerMgr.Instance.Invoke(_setNightAction, 3f); // have to wait here for client to catch up
        }

        Action _setNightAction;
        private void SetNightAction()
        {
            DateTime currentitme = TOD_Sky.Instance.Cycle.DateTime;

            if (TOD_Sky.Instance.IsNight)
            {

                if (config.nightLight)
                    SendNightLightUpdateAll(_NightLightEnabledBytes);

                if (config.invertedNight)
                {
                    _envSync._limitedNetworking = true;
                    TOD_Sky.Instance.Components.Time.OnHour += HourlyInvertedTimeUpdate;
                    UpdateBrightnessAll(config.invertedNightDarkness, config.contrast);
                    SendInvertedNightUpdateAll(_invertedNightBytes);
                    int adjustedHour = (currentitme.Hour + 12) % 24;
                    SendNightNetworkUpdateAll(6, 21, adjustedHour, currentitme.Minute);
                    return;
                }
                UpdateBrightnessAll(config.brightness, config.contrast);
                StartNightCycle();
            }
        }
        private bool HasPerm(string id, string perm) => permission.UserHasPermission(id, perm);
        #endregion Commands
    }
}