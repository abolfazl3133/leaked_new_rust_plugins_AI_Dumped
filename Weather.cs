using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Facepunch.Extend;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Weather", "tofurahie", "2.3.2")]
    internal class Weather : RustPlugin
    {
        #region Static
 
        private const string PERM = "weather.use"; 

        private Dictionary<string, string> ParamsDescription = new()
        {
            ["OceanScale"] = "Waves in the sea",
            ["ABrightness"] = "Atmosphere brightness",
            ["AContrast"] = "Atmosphere contrast",
            ["ADirectionality"] = "Sun glow size",
            ["AFogginess"] = "Fog density",
            ["AMieMultiplier"] = "Mie scattering intensity",
            ["ARayleighMultiplier"] = "Rayleigh scattering intensity",
            ["CAttenuation"] = "Skylight blocking",
            ["CBrightness"] = "Cloud brightness",
            ["CColoring"] = "Skylight blocking",
            ["CCoverage"] = "Cloud coverage",
            ["CScattering"] = "Cloud translucency glow",
            ["CSharpness"] = "Cloud transition sharpness",
            ["CSaturation"] = "Sunlight blocking",
            ["COpacity"] = "Cloud opacity",
            ["CSize"] = "Cloud size"
        };
 
        private IEnumerator PresetCoroutine, GeoCoroutine;
        private TOD_Time TimeComp;
        private Timer _timer;

        private Dictionary<string, float> DefaultChances = new();
        #endregion

        #region OxideHooks

        private void OnServerInitialized()
        {
            LoadData(); 

            permission.RegisterPermission(PERM, this);
   
            if (_config.UseTimeControle)
                EnableTimeModifier();
            
            FindDefaultPresets();
            
            DisableDefaultWeather();

            SetLastWeather();
        }

        private void FindDefaultPresets() 
        {
            foreach (var check in Climate.Instance.WeatherPresets)
            {
                if (check.name == _config.GetPresetByName(check.name)?.Name)
                    continue;

                var chance = 0;
                switch (check.name)
                {
                    case "Clear":
                        _config.Presets.Add(new WeatherSetup
                        {
                            Name = check.name,
                            Duration = 2700,
                            TransitionTime = 300,
                            Chance = 75,
                            Thunder = 0,
                            Rain = 0,
                            Rainbow = 0,
                            Wind = 0,
                            OceanScale = 3,
                            ABrightness = 1,
                            AContrast = 1.2f,
                            ADirectionality = 0.9f,
                            AFogginess = 0,
                            AMieMultiplier = 1,
                            ARayleighMultiplier = 1,
                            CAttenuation = 0.15f,
                            CBrightness = 1,
                            CColoring = 1,
                            CCoverage = 0,
                            CScattering = 1,
                            CSharpness = 1,
                            CSaturation = 1,
                            COpacity = 1,
                            CSize = 2,
                        });
                        
                        SaveConfig();
                        continue;
                    case "Dust":
                        chance = 15;
                        break;  
                    case "Fog":
                        chance = 15;
                        break; 
                    case "Overcast":
                        chance = 15;
                        break;   
                    case "RainMild":
                        chance = 15;
                        break; 
                    case "RainHeavy":
                        chance = 15;
                        break;
                    case "Storm":
                        chance = 15;
                        break;
                }
                
                _config.Presets.Add(new WeatherSetup
                {
                    Name = check.name,
                    Duration = 2700,
                    TransitionTime = 300,
                    Chance = chance,
                    Thunder = check.Thunder, 
                    Rain = check.Rain,
                    Rainbow = check.Rainbow,
                    Wind = check.Wind,
                    OceanScale = check.OceanScale,
                    ABrightness = check.Atmosphere.Brightness, 
                    AContrast = check.Atmosphere.Contrast,
                    ADirectionality = check.Atmosphere.Directionality,
                    AFogginess = check.Atmosphere.Fogginess,
                    AMieMultiplier = check.Atmosphere.MieMultiplier,
                    ARayleighMultiplier = check.Atmosphere.RayleighMultiplier,
                    CAttenuation = check.Clouds.Attenuation,
                    CBrightness = check.Clouds.Brightness,
                    CColoring = check.Clouds.Coloring,
                    CCoverage = check.Clouds.Coverage,
                    CScattering = check.Clouds.Scattering,
                    CSharpness = check.Clouds.Sharpness,
                    CSaturation = check.Clouds.Saturation,
                    COpacity = check.Clouds.Opacity,
                    CSize = check.Clouds.Size,
                });
            }

            SaveConfig();
        }

        private void Unload()
        {
            if (PresetCoroutine != null)
                ServerMgr.Instance.StopCoroutine(PresetCoroutine);      
            
            if (GeoCoroutine != null)
                ServerMgr.Instance.StopCoroutine(GeoCoroutine);
            
            SaveData();

            UI.DestroyToAll(".bg");

            if (_config.UseTimeControle)
                DisableTimeModifier();

            EnableDefaultWeather();
        }

        #endregion

        #region Functions

        private string API_GetCurrentWeatherPreset() => _data.PresetName;

        private void StartPreset(string name)
        {
            if (PresetCoroutine != null)
                ServerMgr.Instance.StopCoroutine(PresetCoroutine);
                
            PresetCoroutine = SetPreset(_config.GetPresetByName(name));
            ServerMgr.Instance.StartCoroutine(PresetCoroutine);
        }
        private void SetLastWeather()
        {
            if (_config.GeoWeather.UseIRL)
            {
                if (GeoCoroutine != null)
                    ServerMgr.Instance.StopCoroutine(GeoCoroutine);
                
                GeoCoroutine = CheckGeoWeather();
                ServerMgr.Instance.StartCoroutine(GeoCoroutine);

                return;
            }
            
            if (string.IsNullOrEmpty(_data.PresetName)) 
            {
                if (_config.UseCustomSchedule)
                    StartPreset(_config.CustomSchedule[GetNearest()]);
                else 
                    SetRandomPreset();
                
                return;
            } 
            
            StartPreset(_data.PresetName);
        }

        private void OnSunset() => TimeComp.DayLengthInMinutes = _config.UseSkipNight ? 0.001f : _config.NightLength * 2;

        private void OnSunrise() => TimeComp.DayLengthInMinutes = _config.DayLength * 2;

        private void OnHour()
        {
            if (_config.GeoWeather.UseIRL)
            {
                if (GeoCoroutine != null)
                    ServerMgr.Instance.StopCoroutine(GeoCoroutine);
                
                GeoCoroutine = CheckGeoWeather();
                ServerMgr.Instance.StartCoroutine(GeoCoroutine);
            }
            
            else if (_config.UseCustomSchedule)
            { 
                var preset = _config.CustomSchedule[(int)TOD_Sky.Instance.Cycle.Hour == 0 ? 24 : (int)TOD_Sky.Instance.Cycle.Hour];
                if (string.IsNullOrEmpty(preset)) 
                    return;
                
                StartPreset(preset);
            }
            
            if (TOD_Sky.Instance.SunriseTime <= Env.time && TOD_Sky.Instance.SunsetTime > Env.time)
                OnSunrise();
            else
                OnSunset();
        }

        private IEnumerator CheckGeoWeather()
        {
            using var request = UnityWebRequest.Get($"api.openweathermap.org/data/2.5/forecast?lat={_config.GeoWeather.Latitude}&lon={_config.GeoWeather.Longitude}&appid={_config.GeoWeather.APIKey}");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                PrintError("Connection error.");
                SetRandomPreset();
                yield break;
            }

            var json = JObject.Parse(request.downloadHandler.text);
            if (json["cod"].ToString() != "200")
            {
                PrintError(json["message"].ToString());
                SetRandomPreset();
                yield break;
            } 
            
            if (_config.GeoWeather.PresetNames[json["list"][0]["weather"][0]["main"].ToString()] == _data.PresetName)
                yield break;
                
            StartPreset(_config.GeoWeather.PresetNames[json["list"][0]["weather"][0]["main"].ToString()]);
        }
        
        private void EnableTimeModifier() 
        {
            TimeComp = TOD_Sky.Instance.Components.Time;

            TimeComp.ProgressTime = true;
            TimeComp.UseTimeCurve = false;

            TimeComp.OnSunrise += OnSunrise;
            TimeComp.OnSunset += OnSunset;
            TimeComp.OnHour += OnHour;

            TimeComp.DayLengthInMinutes = 2 * (TOD_Sky.Instance.IsDay ? _config.DayLength : _config.NightLength);
        }

        private void DisableTimeModifier()
        {
            TimeComp.OnSunrise -= OnSunrise;
            TimeComp.OnSunset -= OnSunset;
            TimeComp.OnHour -= OnHour;

            TimeComp.DayLengthInMinutes = 60;
        }

        private void SetRandomPreset()
        {
            WeatherSetup randomPreset = null; 
            var diceRoll = Random.Range(1, _config.Presets.Sum(i => i.Chance));
            var cumulative = 0;

            foreach (var item in _config.Presets)
            {
                cumulative += item.Chance;
                if (diceRoll >= cumulative)
                    continue;
                
                randomPreset = item;
                break;
            }

            if (randomPreset == null)
                return;

            if (randomPreset.Name == _data.PresetName)
            {  
                _data.ActivateTime = DateTime.Now;
                _timer = timer.Once(randomPreset.Duration, SetRandomPreset);
                return;
            } 
            
            StartPreset(randomPreset.Name);
        }

        private int GetNextHour()
        {
            var loop = false;
            for (int i = 1; i <= 24; i++)
            {
                var item = _config.CustomSchedule[i];
                if (i <= TOD_Sky.Instance.Cycle.Hour || string.IsNullOrEmpty(item))
                {
                    if (i == 24)
                    {
                        if (loop)
                            return 1;
                            
                        i = 1;
                        loop = true;
                    }
                    
                    continue;
                }

                return i;
            }

            return 1;
        }

        private int GetPreviousHour()
        {
            var loop = false;
            for (int i = 24; i > 0; i--)
            {
                var item = _config.CustomSchedule[i];
                if (i <= TOD_Sky.Instance.Cycle.Hour || string.IsNullOrEmpty(item))
                {
                    if (i == 1)
                    {
                        if (loop)
                            return 1;
                            
                        i = 24;
                        loop = true;
                    }
                    
                    continue;
                }

                return i;
            }

            return 1;
        }

        private int GetNearest() 
        {
            var nextHour = GetNextHour();
            var previousHour = GetPreviousHour();

            return nextHour - (int)TOD_Sky.Instance.Cycle.Hour > (int)TOD_Sky.Instance.Cycle.Hour - previousHour ? previousHour : nextHour;
        }


        private WeatherSetup GetPreset(string presetNames)
        {
            var presets = presetNames.Split(",");

            var weatherPresets = new List<WeatherSetup>();
            foreach (var check in presets)
            {
                var findPreset = _config.GetPresetByName(check.Trim());

                if (findPreset != null)  
                    weatherPresets.Add(findPreset);
            }  
            
            WeatherSetup randomPreset = null; 
            var diceRoll = Random.Range(1, weatherPresets.Sum(i => i.Chance));
            var cumulative = 0;
 
            foreach (var item in weatherPresets)
            {
                cumulative += item.Chance;
                if (diceRoll >= cumulative)
                    continue;
                
                randomPreset = item;
                break;
            }
            

            return randomPreset;
        }
 
        private IEnumerator SetPreset(WeatherSetup preset)
        {
            PrintWarning($"New Weather Preset - {preset.Name}");
            Interface.CallHook("OnNewPresetStart", preset.Name);
            
            var weatherState = Climate.Instance.WeatherOverrides;
            var oldWeatherState = new WeatherInfo(weatherState);

            _data.PresetName = preset.Name;
            _timer?.Destroy();
             
            _data.ActivateTime = DateTime.Now;  
            SaveData();  
            var passedTime = (int)DateTime.Now.Subtract(_data.ActivateTime).TotalSeconds;
 
            if (!_config.UseCustomSchedule)
                _timer = timer.Once(preset.Duration - passedTime, SetRandomPreset);

            var transitionTime = preset.TransitionTime * 10; 
            while (transitionTime > 0) 
            {
                if (!IsLoaded)
                    yield break; 

                weatherState.Rain = GetWeatherValueStep(oldWeatherState.Rain, weatherState.Rain, preset.Rain, preset.TransitionTime);
                weatherState.Wind = GetWeatherValueStep(oldWeatherState.Wind, weatherState.Wind, preset.Wind, preset.TransitionTime);
                weatherState.OceanScale = GetWeatherValueStep(oldWeatherState.OceanScale, weatherState.OceanScale, preset.OceanScale, preset.TransitionTime);
                weatherState.Thunder = GetWeatherValueStep(oldWeatherState.Thunder, weatherState.Thunder, preset.Thunder, preset.TransitionTime);
                weatherState.Rainbow = GetWeatherValueStep(oldWeatherState.Rainbow, weatherState.Rainbow, preset.Rainbow, preset.TransitionTime);

                weatherState.Atmosphere.RayleighMultiplier = GetWeatherValueStep(oldWeatherState.ARayleighMultiplier, weatherState.Atmosphere.RayleighMultiplier, preset.ARayleighMultiplier, preset.TransitionTime);
                weatherState.Atmosphere.MieMultiplier = GetWeatherValueStep(oldWeatherState.AMieMultiplier, weatherState.Atmosphere.MieMultiplier, preset.AMieMultiplier, preset.TransitionTime);
                weatherState.Atmosphere.Brightness = GetWeatherValueStep(oldWeatherState.ABrightness, weatherState.Atmosphere.Brightness, preset.ABrightness, preset.TransitionTime);
                weatherState.Atmosphere.Contrast = GetWeatherValueStep(oldWeatherState.AContrast, weatherState.Atmosphere.Contrast, preset.AContrast, preset.TransitionTime);
                weatherState.Atmosphere.Fogginess = GetWeatherValueStep(oldWeatherState.AFogginess, weatherState.Atmosphere.Fogginess, preset.AFogginess, preset.TransitionTime);
                weatherState.Atmosphere.Directionality = GetWeatherValueStep(oldWeatherState.ADirectionality, weatherState.Atmosphere.Directionality, preset.ADirectionality, preset.TransitionTime);

                weatherState.Clouds.Opacity = GetWeatherValueStep(oldWeatherState.COpacity, weatherState.Clouds.Opacity, preset.COpacity, preset.TransitionTime);
                weatherState.Clouds.Size = GetWeatherValueStep(oldWeatherState.CSize, weatherState.Clouds.Size, preset.CSize, preset.TransitionTime);
                weatherState.Clouds.Coverage = GetWeatherValueStep(oldWeatherState.CCoverage, weatherState.Clouds.Coverage, preset.CCoverage, preset.TransitionTime);
                weatherState.Clouds.Sharpness = GetWeatherValueStep(oldWeatherState.CSharpness, weatherState.Clouds.Sharpness, preset.CSharpness, preset.TransitionTime);
                weatherState.Clouds.Coloring = GetWeatherValueStep(oldWeatherState.CColoring, weatherState.Clouds.Coloring, preset.CColoring, preset.TransitionTime);
                weatherState.Clouds.Attenuation = GetWeatherValueStep(oldWeatherState.CAttenuation, weatherState.Clouds.Attenuation, preset.CAttenuation, preset.TransitionTime);
                weatherState.Clouds.Saturation = GetWeatherValueStep(oldWeatherState.CSaturation, weatherState.Clouds.Saturation, preset.CSaturation, preset.TransitionTime);
                weatherState.Clouds.Scattering = GetWeatherValueStep(oldWeatherState.CScattering, weatherState.Clouds.Scattering, preset.CScattering, preset.TransitionTime);
                weatherState.Clouds.Brightness = GetWeatherValueStep(oldWeatherState.CBrightness, weatherState.Clouds.Brightness, preset.CBrightness, preset.TransitionTime);
       
                ServerMgr.SendReplicatedVars("weather.");
                
                transitionTime -= 1; 

                yield return new WaitForSeconds(0.1f);
            }
        }

        private float GetWeatherValueStep(float old, float current, float target, float time) => Mathf.Clamp(current + (target - old) / (time * 10), 0, target > old ? target : old);

        private void DisableDefaultWeather()
        {
            var weather = Climate.Instance.Weather;
            
            DefaultChances.Add("Clear", weather.ClearChance);
            DefaultChances.Add("Dust", weather.DustChance);
            DefaultChances.Add("Fog", weather.FogChance);
            DefaultChances.Add("Storm", weather.StormChance);
            DefaultChances.Add("Overcast", weather.OvercastChance);
            DefaultChances.Add("Rain", weather.RainChance);
            
            weather.ClearChance = 0;
            weather.DustChance = 0;
            weather.FogChance = 0; 
            weather.StormChance = 0;
            weather.OvercastChance = 0;
            weather.RainChance = 0;

            ServerMgr.SendReplicatedVars("weather.");
        }

        private void EnableDefaultWeather()
        {
            var weather = Climate.Instance.Weather;
            
            weather.ClearChance = DefaultChances["Clear"];
            weather.DustChance = DefaultChances["Dust"];
            weather.FogChance = DefaultChances["Fog"]; 
            weather.StormChance = DefaultChances["Storm"];
            weather.OvercastChance = DefaultChances["Overcast"];
            weather.RainChance = DefaultChances["Rain"];

            Climate.Instance.WeatherState.Reset();
            
            ServerMgr.SendReplicatedVars("weather.");
        }

        #endregion

        #region Commands

        [ChatCommand("wsetup")]
        private void cmdChatwsetup(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM))
                return;

            ShowUIBG(player);
        }

        [ChatCommand("setday")]
        private void cmdChatsetday(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM))
                return;

            TOD_Sky.Instance.Cycle.Hour = TOD_Sky.Instance.SunriseTime + 2;
        }

        [ChatCommand("setnight")]
        private void cmdChatsetnight(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM))
                return;

            TOD_Sky.Instance.Cycle.Hour = TOD_Sky.Instance.SunsetTime + 2;
        }


        [ConsoleCommand("UI_W")]
        private void cmdConsoleUI_W(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs())
                return;
            var player = arg.Player();
            switch (arg.GetString(0))
            {
                case "CHANGEUSETIMECONTROLE":
                    _config.UseTimeControle = !_config.UseTimeControle;
                    if (_config.UseTimeControle)
                        EnableTimeModifier();
                    else
                        DisableTimeModifier();

                    ShowUISettings(player);
                    SaveConfig();
                    break; 
                case "CHANGESKIPNIGHT":
                    _config.UseSkipNight = !_config.UseSkipNight;
                    if (!TOD_Sky.Instance.IsDay)
                        OnSunset();

                    ShowUISettings(player);
                    SaveConfig();
                    break;
                case "CHANGECUSTOMSCHEDULE":
                    _config.UseCustomSchedule = !_config.UseCustomSchedule;

                    if (_config.UseCustomSchedule)
                        StartPreset(_config.CustomSchedule[GetNearest()]);
                        
                    UI.Destroy(player, ".custom.schedule.bg");
                    ShowUISettings(player);
                    SaveConfig();
                    break;     
                case "CHANGESCHEDULEPRESET":
                    _config.CustomSchedule[arg.GetInt(1)] = arg.GetString(2);                
    
                    SaveConfig();
                    break;
                case "CHANGEDAYLENGTH":
                    _config.DayLength = arg.GetUInt(1, 30);

                    DisableTimeModifier();
                    EnableTimeModifier();
                    SaveConfig();
                    break;
                case "CHANGENIGHTLENGTH":
                    _config.NightLength = arg.GetUInt(1, 30); 
                    
                    DisableTimeModifier();
                    EnableTimeModifier();
                    SaveConfig();
                    break;
                case "REMOVEPRESET":
                    _config.Presets.Remove(_config.GetPresetByName(arg.GetString(2)));
                    if (arg.GetString(2) == _data.PresetName)  
                        SetRandomPreset();

                    ShowUIPresets(player, arg.GetString(1));
                    SaveConfig();
                    break;
                case "SETPRESET":
                    StartPreset(arg.GetString(2));
                    ShowUIPresets(player, arg.GetString(1));
                    break;
                case "SETRANDOMPRESET":
                    SetRandomPreset();

                    ShowUIPresets(player, arg.GetString(1));
                    break;
                case "SELECTPRESET":
                    ShowUIPresets(player, arg.GetString(1));
                    ShowUIPreset(player, arg.GetString(1));
                    break;
                case "ADDNEWPRESET":
                    if (_config.GetPresetByName("NewPreset").Name == "NewPreset")
                        return;

                    _config.Presets.Reverse();
                    _config.Presets.Add(new WeatherSetup { Name = "NewPreset" });
                    _config.Presets.Reverse();

                    ShowUIPresets(player, arg.GetString(1));
                    ShowUIPreset(player, "NewPreset");
                    SaveConfig();
                    break; 
                case "PAGE":
                    ShowUIPresets(player, arg.GetString(1));
                    break;
                case "CHANGE":
                    var preset = _config.GetPresetByName(arg.GetString(1));
 
                    switch (arg.GetString(2))
                    {
                        case "Name":
                            preset.Name = arg.GetString(3);
                            break;
                        case "Duration":
                            preset.Duration = arg.GetInt(3);
                            break;    
                        case "TransitionTime":
                            preset.TransitionTime = arg.GetInt(3);
                            break;
                        case "Chance":
                            preset.Chance = arg.GetInt(3) > 100 ? 100 : arg.GetInt(3) < 0 ? 0 : arg.GetInt(3);
                            break;
                        case "Thunder":
                            preset.Thunder = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                        case "Rain":  
                            preset.Rain = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                        case "Rainbow":
                            preset.Rainbow = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                        case "Wind":
                            preset.Wind = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                        case "OceanScale":
                            preset.OceanScale = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break; 
                        case "ABrightness":
                            preset.ABrightness = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                        case "AContrast":
                            preset.AContrast = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                        case "ADirectionality":
                            preset.ADirectionality = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                        case "AFogginess":
                            preset.AFogginess = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                        case "AMieMultiplier":
                            preset.AMieMultiplier = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                        case "ARayleighMultiplier":
                            preset.ARayleighMultiplier = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                        case "CAttenuation":
                            preset.CAttenuation = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                        case "CBrightness":
                            preset.CBrightness = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                        case "CColoring":
                            preset.CColoring = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                        case "CCoverage":
                            preset.CCoverage = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                        case "CScattering":
                            preset.CScattering = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                        case "CSharpness":
                            preset.CSharpness = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                        case "CSaturation":
                            preset.CSaturation = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                        case "COpacity":
                            preset.COpacity = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break; 
                        case "CSize":
                            preset.CSize = arg.GetFloat(3) < 0 ? 0 : arg.GetFloat(3);
                            break;
                    }

                    if (_data.PresetName == arg.GetString(1))
                        StartPreset(preset.Name);

                    SaveConfig();
                    break;
            }
        }

        #endregion

        #region UI

        private void ShowUIPreset(BasePlayer player, string name)
        {
            var container = new CuiElementContainer();

            UI.Panel(ref container, ".bg", ".preset.bg", ".preset.bg", aMin: "0.5 0.5", aMax: "0.5 0.5", oMin: "-90 -280", oMax: "190 280", bgColor: "0.15 0.15 0.15 0.95", material: "assets/content/ui/binocular_overlay.mat");

            UI.Label(ref container, ".preset.bg", aMin: "0 1", aMax: "1 1", oMin: "5 -25", oMax: "-5 0", text: "Preset Settings", color: "yellow", font: "robotocondensed-bold.ttf", fontSize: 18);

            #region Params

            var Preset = _config.GetPresetByName(name);
            var listParam = new Dictionary<string, string>
            {
                ["Name"] = Preset.Name,
                ["Duration"] = Preset.Duration.ToString(),
                ["TransitionTime"] = Preset.TransitionTime.ToString(),
                ["Chance"] = Preset.Chance.ToString(),
                ["Thunder"] = Preset.Thunder.ToString(),
                ["Rain"] = Preset.Rain.ToString(),
                ["Rainbow"] = Preset.Rainbow.ToString(),
                ["Wind"] = Preset.Wind.ToString(),
                ["OceanScale"] = Preset.OceanScale.ToString(),
                ["ABrightness"] = Preset.ABrightness.ToString(),
                ["AContrast"] = Preset.AContrast.ToString(),
                ["ADirectionality"] = Preset.ADirectionality.ToString(),
                ["AFogginess"] = Preset.AFogginess.ToString(),
                ["AMieMultiplier"] = Preset.AMieMultiplier.ToString(),
                ["ARayleighMultiplier"] = Preset.ARayleighMultiplier.ToString(),
                ["CAttenuation"] = Preset.CAttenuation.ToString(),
                ["CBrightness"] = Preset.CBrightness.ToString(),
                ["CColoring"] = Preset.CColoring.ToString(),
                ["CCoverage"] = Preset.CCoverage.ToString(),
                ["CScattering"] = Preset.CScattering.ToString(),
                ["CSharpness"] = Preset.CSharpness.ToString(),
                ["CSaturation"] = Preset.CSaturation.ToString(),
                ["COpacity"] = Preset.COpacity.ToString(),
                ["CSize"] = Preset.CSize.ToString(),
            };

            var posY = -50;

            foreach (var check in listParam)
            {
                UI.Label(ref container, ".preset.bg", aMin: "0 1", aMax: "1 1", oMin: $"5 {posY}", oMax: $"-5 {posY + 20}", text: $"{(ParamsDescription.TryGetValue(check.Key, out var value) ? value : check.Key)}:", align: TextAnchor.MiddleLeft);

                UI.PanelInput(ref container, ".preset.bg", name: $".{check.Key}", aMin: "1 1", aMax: "1 1", oMin: $"-90 {posY}", oMax: $"-5 {posY + 20}", text: check.Value, fontSize: 12, command: $"UI_W CHANGE {Preset.Name} {check.Key}", bgColor: "0.23 0.23 0.23 0.85", material: "assets/content/ui/binocular_overlay.mat", limit: 16);
                
  
                posY -= 22;
            }

            #endregion

            UI.Create(player, container);
        }

        private void ShowUIPresets(BasePlayer player, string selectedName)
        {
            var container = new CuiElementContainer();

            UI.Panel(ref container, ".setup.bg", ".presets.bg", ".presets.bg", oMax: $"0 {(_config.UseTimeControle ? -130 : -80)}", bgColor: "0 0 0 0.6");

            UI.Label(ref container, ".presets.bg", aMin: "0 1", aMax: "1 1", oMin: "5 -25", oMax: "-5 0", text: "PRESETS", color: "yellow", font: "robotocondensed-bold.ttf", fontSize: 18);

            UI.Button(ref container, ".presets.bg", aMin: "1 1", aMax: "1 1", oMin: $"-22 -22", oMax: $"-3 -3", sprite: "assets/icons/slash.png", bgColor: "0.0829 0.2897 0.4338 1", command: $"UI_W SETRANDOMPRESET {selectedName}");

            UI.Button(ref container, ".presets.bg", aMin: "0.5 1", aMax: "0.5 1", oMin: $"-8 -41", oMax: $"8 -25", sprite: "assets/icons/authorize.png", bgColor: "1 1 1 1", command: $"UI_W ADDNEWPRESET {selectedName}");
            
            UI.Panel(ref container, ".presets.bg",".presets.list.bg", ".presets.list.bg", "0 0", "1 1", "0 0", "0 -45", "0 0 0 0");
            
            container.Add(new CuiElement
            {
                Parent = "UI_Weather.presets.list.bg",
                Name = "UI_Weather.scroll",
                Components =
                { 
                    new CuiScrollViewComponent
                    {
                        MovementType = ScrollRect.MovementType.Clamped,
                        Vertical = _config.Presets.Count > 13,
                        Inertia = false,
                        Horizontal = false,
                        Elasticity = 0.25f,
                        DecelerationRate = 0.3f,
                        ScrollSensitivity = 24f,
                        ContentTransform = new CuiRectTransform { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 {-_config.Presets.Count * 25}", OffsetMax = "0 0" },
                    }
                }
            });
            
            UI.Panel(ref container, ".scroll", ".scroll.bg", bgColor:"1 0 0 0");

            var posY = -25;
            foreach (var check in _config.Presets)
            {
                UI.Button(ref container, ".scroll.bg", aMin: "0 1", aMax: "1 1", oMin: $"10 {posY}", oMax: $"-10 {posY + 24}", text: check.Name, color: _data.PresetName == check.Name ? "0.451 0.5529 0.2706 1" : selectedName == check.Name ? "1 1 1 1" : "1 1 1 0.35", command: selectedName == check.Name ? "" : $"UI_W SELECTPRESET {check.Name}");

                UI.Button(ref container, ".scroll.bg", aMin: "0 1", aMax: "0 1", oMin: $"7 {posY + 7}", oMax: $"17 {posY + 17}", sprite: "assets/icons/close.png", bgColor: "red", command: $"UI_W REMOVEPRESET {selectedName} {check.Name}");

                if (_data.PresetName != check.Name)
                    UI.Button(ref container, ".scroll.bg", aMin: "1 1", aMax: "1 1", oMin: $"-23 {posY + 5}", oMax: $"-9 {posY + 19}", sprite: "assets/icons/check.png", bgColor: "green", command: $"UI_W SETPRESET {selectedName} {check.Name}");

                posY -= 24;
            }


            UI.Create(player, container); 
        }

        private void ShowUISettings(BasePlayer player)
        { 
            var container = new CuiElementContainer();

            UI.Panel(ref container, ".bg", ".setup.bg", ".setup.bg", aMin: "0.5 0.5", aMax: "0.5 0.5", oMin: "-300 -250", oMax: "-100 250", bgColor: "0.15 0.15 0.15 0.95", material: "assets/content/ui/binocular_overlay.mat");

            UI.Button(ref container, ".setup.bg", aMin: "0 1", aMax: "1 1", oMin: "5 -25", oMax: "-5 -5", text: $"Use custom schedule: {(_config.UseCustomSchedule ? "<color=green>ON</color>" : "<color=red>OFF</color>")}", command: "UI_W CHANGECUSTOMSCHEDULE");

            UI.Button(ref container, ".setup.bg", aMin: "0 1", aMax: "1 1", oMin: "5 -50", oMax: "-5 -30", text: $"Use time controle: {(_config.UseTimeControle ? "<color=green>ON</color>" : "<color=red>OFF</color>")}", command: "UI_W CHANGEUSETIMECONTROLE");
            
            UI.Button(ref container, ".setup.bg", aMin: "0 1", aMax: "1 1", oMin: "5 -75", oMax: "-5 -55", text: $"Use skip night: {(_config.UseSkipNight ? "<color=green>ON</color>" : "<color=red>OFF</color>")}", command: "UI_W CHANGESKIPNIGHT");

            var posY = -97;
            if (_config.UseTimeControle)
            {
                UI.Label(ref container, ".setup.bg", aMin: "0 1", aMax: "1 1", oMin: $"5 {posY}", oMax: $"-5 {posY + 20}", text: "Day length:", align: TextAnchor.MiddleLeft);

                UI.PanelInput(ref container, ".setup.bg", name: ".dayLength", aMin: "1 1", aMax: "1 1", oMin: $"-60 {posY}", oMax: $"-5 {posY + 20}", text: _config.DayLength.ToString(), command: "UI_W CHANGEDAYLENGTH", bgColor: "0.23 0.23 0.23 0.85", material: "assets/content/ui/binocular_overlay.mat", limit: 3);

                posY -= 25;
                UI.Label(ref container, ".setup.bg", aMin: "0 1", aMax: "1 1", oMin: $"5 {posY}", oMax: $"-5 {posY + 20}", text: "Night length:", align: TextAnchor.MiddleLeft);

                UI.PanelInput(ref container, ".setup.bg", name: ".nightLength", aMin: "1 1", aMax: "1 1", oMin: $"-60 {posY}", oMax: $"-5 {posY + 20}", text: _config.NightLength.ToString(), command: "UI_W CHANGENIGHTLENGTH", bgColor: "0.23 0.23 0.23 0.85", material: "assets/content/ui/binocular_overlay.mat", limit: 3);
            }

            UI.Create(player, container);

            ShowUIPresets(player, _data.PresetName);
            ShowUIPreset(player, _data.PresetName);
            if (_config.UseCustomSchedule)
                ShowUICustomSchedule(player);
        }

        private void ShowUIBG(BasePlayer player)
        {
            var container = new CuiElementContainer();

            UI.MainParent(ref container, aMin: "0 0", aMax: "1 1");

            UI.Panel(ref container, ".bg", bgColor: "0 0 0 0.85", material: "assets/content/ui/uibackgroundblur-ingamemenu.mat");

            UI.Button(ref container, ".bg", ".bg", aMin: "0 1", aMax: "0 1", oMin: "15 -45", oMax: "45 -15", sprite: "assets/icons/close.png", bgColor: "0.698 0.2039 0.0039 1");

            UI.Create(player, container);

            ShowUISettings(player);
        }

        private void ShowUICustomSchedule(BasePlayer player)
        {
            var container = new CuiElementContainer();

            UI.Panel(ref container, ".bg", ".custom.schedule.bg", ".custom.schedule.bg", aMin: "0.5 0.5", aMax: "0.5 0.5", oMin: "-460 -250", oMax: "-310 250", bgColor: "0.15 0.15 0.15 0.95", material: "assets/content/ui/binocular_overlay.mat");
            
            UI.Label(ref container, ".custom.schedule.bg", aMin: "0 1", aMax: "1 1", oMin: "5 -25", oMax: "-5 0", text: "SCHEDULE", color: "yellow", font: "robotocondensed-bold.ttf", fontSize: 16);

            var posY = -40;
            foreach (var check in _config.CustomSchedule)
            {
                UI.Label(ref container, ".custom.schedule.bg", aMin: "0 1", aMax: "1 1", oMin: $"5 {posY}", oMax: $"-5 {posY + 20}", text: $"{check.Key} h", color: (int) TOD_Sky.Instance.Cycle.Hour == check.Key ? "yellow" : "1 1 1 1", fontSize: 12, align:TextAnchor.MiddleLeft);
                
                UI.PanelInput(ref container, ".custom.schedule.bg", ".input.preset" + posY, command:$"UI_W CHANGESCHEDULEPRESET {check.Key}", aMin: "0 1", aMax: "1 1", oMin: $"35 {posY + 2}", oMax: $"-5 {posY + 18}", text: $"{check.Value}", bgColor:"0 0 0 0.8", color: "lightblue", fontSize: 12);

                posY -= 20;
            }

            UI.Create(player, container);
        }
        
        #endregion

        #region Classes

        private class Configuration
        {
            [JsonProperty("Use time control")]
            public bool UseTimeControle = true;
            
            [JsonProperty("Day length [minutes]")]
            public uint DayLength = 30u;

            [JsonProperty("Night length [minutes]")]
            public uint NightLength = 30u;

            [JsonProperty("Skip night")]
            public bool UseSkipNight = false;
            
            [JsonProperty("Use a custom schedule")]
            public bool UseCustomSchedule = false;

            [JsonProperty("IRL Weather presets")]
            public GeoWeather GeoWeather = new()
            {
                UseIRL = false, 
                APIKey = "",
                Latitude = 0f,
                Longitude = 0f,
                PresetNames = new()
                {
                    ["Clear"] = "Clear",
                    ["Clouds"] = "Clear",
                    ["Atmosphere"] = "Fog",
                    ["Drizzle"] = "RainMild",
                    ["Rain"] = "RainHeavy",
                    ["Snow"] = "RainMild",
                    ["Thunderstorm"] = "Storm",
                }
            };
            
            [JsonProperty("Custom schedule [1-24 - Preset Names | don't change the numbers]", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<int, string> CustomSchedule = new Dictionary<int, string>
            {
                [1] = "Clear",
                [2] = "",
                [3] = "",
                [4] = "",
                [5] = "",
                [6] = "",
                [7] = "",
                [8] = "",
                [9] = "Storm,Dust",
                [10] = "",
                [11] = "",
                [12] = "",
                [13] = "Clear",
                [14] = "",
                [15] = "",
                [16] = "",
                [17] = "",
                [18] = "RainMild,RainHeavy",
                [19] = "",
                [20] = "",
                [21] = "",
                [22] = "",
                [23] = "",
                [24] = "",
            };

            [JsonProperty("Presets", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<WeatherSetup> Presets = new();

            public WeatherSetup GetPresetByName(string name) 
            { 
                if (name == "NewPreset")
                    return Presets.FirstOrDefault(x => x.Name == "NewPreset") ?? new WeatherSetup();

                foreach (var check in Presets)
                    if (name == check.Name)
                        return check;

                return Presets.Count == 0 ? null : Presets[0];
            }
        }

        private class GeoWeather
        {
            [JsonProperty("Use IRL weather")]
            public bool UseIRL;

            [JsonProperty("API key [https://home.openweathermap.org/users/sign_up] (YOU DON'T NEED TO PAY FOR THIS)")]
            public string APIKey;

            [JsonProperty("Latitude [https://www.latlong.net]")]
            public float Latitude;

            [JsonProperty("Longitude [https://www.latlong.net]")]
            public float Longitude;

            [JsonProperty("Preset names [site's weather name which the plugin get from API and preset that should be applied")]
            public Dictionary<string, string> PresetNames;
        }

        private class WeatherSetup
        {
            [JsonProperty("Name")]
            public string Name;

            [JsonProperty("Duration [seconds]")]
            public int Duration = 14400;
            
            [JsonProperty("Transition time [seconds]")]
            public int TransitionTime = 60;
 
            [JsonProperty("Chance [0 - 100%]")]
            public int Chance;
 
            [JsonProperty("Wind")]
            public float Wind = -1;
 
            [JsonProperty("Rain")]
            public float Rain = -1;

            [JsonProperty("Thunder")]
            public float Thunder = -1;

            [JsonProperty("Rainbow")]
            public float Rainbow = -1;

            [JsonProperty("Ocean")]
            public float OceanScale = -1;

            [JsonProperty("Atmosphere rayleigh")]
            public float ARayleighMultiplier = -1;

            [JsonProperty("Atmosphere mie")]
            public float AMieMultiplier = -1;

            [JsonProperty("Atmosphere contrast")]
            public float AContrast = -1;

            [JsonProperty("Atmosphere directionality")]
            public float ADirectionality = -1;

            [JsonProperty("Atmosphere fogginess")]
            public float AFogginess = -1;

            [JsonProperty("Atmosphere brightness")]
            public float ABrightness = -1;

            [JsonProperty("Clouds size")]
            public float CSize = -1;

            [JsonProperty("Clouds opacity")]
            public float COpacity = -1;

            [JsonProperty("Clouds coverage")]
            public float CCoverage = -1;
 
            [JsonProperty("Clouds sharpness")]
            public float CSharpness = -1;

            [JsonProperty("Clouds coloring")]
            public float CColoring = -1;

            [JsonProperty("Clouds attenuation")]
            public float CAttenuation = -1;

            [JsonProperty("Clouds saturation")]
            public float CSaturation = -1;

            [JsonProperty("Clouds scattering")]
            public float CScattering = -1;

            [JsonProperty("Clouds brightness")]
            public float CBrightness = -1;
        }

        private class WeatherInfo
        {
            public float Wind;
            public float Rain;
            public float Thunder;
            public float Rainbow;
            public float OceanScale;
            public float ARayleighMultiplier;
            public float AMieMultiplier;
            public float AContrast;
            public float ADirectionality;
            public float AFogginess;
            public float ABrightness;
            public float CSize;
            public float COpacity;
            public float CCoverage;
            public float CSharpness;
            public float CColoring;
            public float CAttenuation;
            public float CSaturation;
            public float CScattering;
            public float CBrightness;

            public WeatherInfo(WeatherPreset weatherPreset)
            {
                Wind = weatherPreset.Wind;
                Rain = weatherPreset.Rain;
                Thunder = weatherPreset.Thunder;
                Rainbow = weatherPreset.Rainbow;
                OceanScale = weatherPreset.OceanScale;
                ARayleighMultiplier = weatherPreset.Atmosphere.RayleighMultiplier;
                AMieMultiplier = weatherPreset.Atmosphere.MieMultiplier;
                AContrast = weatherPreset.Atmosphere.Contrast;
                ADirectionality = weatherPreset.Atmosphere.Directionality;
                AFogginess = weatherPreset.Atmosphere.Fogginess;
                ABrightness = weatherPreset.Atmosphere.Brightness;
                CSize = weatherPreset.Clouds.Size;
                COpacity = weatherPreset.Clouds.Opacity;
                CCoverage = weatherPreset.Clouds.Coverage;
                CSharpness = weatherPreset.Clouds.Sharpness;
                CColoring = weatherPreset.Clouds.Coloring;
                CAttenuation = weatherPreset.Clouds.Attenuation;
                CSaturation = weatherPreset.Clouds.Saturation;
                CScattering = weatherPreset.Clouds.Scattering;
                CBrightness = weatherPreset.Clouds.Brightness;
            }
        }

        private class Data
        {
            [JsonProperty("Activate time")]
            public DateTime ActivateTime;

            [JsonProperty("Current last preset name")]
            public string PresetName;
        }

        #endregion

        #region Stuff

        #region Config

        private Configuration _config;

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                    throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion

        #region Data

        private Data _data;

        private void LoadData() => _data = Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/data") ? Interface.Oxide.DataFileSystem.ReadObject<Data>($"{Name}/data") : new Data();
        private void OnServerSave() => SaveData();

        private void SaveData()
        {
            if (_data != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/data", _data);
        }

        #endregion

        #region GUI

        private class UI
        {
            private const string Layer = "UI_Weather";

            #region MainElements

            public static void MainParent(ref CuiElementContainer container, string name = null, string aMin = "0.5 0.5", string aMax = "0.5 0.5", bool overAll = true, bool keyboardEnabled = true, bool cursorEnabled = true) =>
                container.Add(new CuiPanel
                {
                    KeyboardEnabled = keyboardEnabled,
                    CursorEnabled = cursorEnabled,
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }, 
                    Image = { Color = "0 0 0 0" }
                }, overAll ? "Overlay" : "Hud", Layer + ".bg" + name, Layer + ".bg" + name);

            public static void Panel(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", string bgColor = "0.33 0.33 0.33 1", string material = null) =>
                container.Add(new CuiElement
                {
                    Parent = Layer + parent,
                    Name = Layer + name,
                    DestroyUi = destroy == null ? null : Layer + destroy,
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax },
                        new CuiImageComponent { Color = HexToRustFormat(bgColor), Material = material },
                    },
                });

            public static void Icon(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", int itemID = 0, ulong skinID = 0, string sprite = null) =>
                container.Add(new CuiElement
                {
                    Parent = Layer + parent,
                    Name = Layer + name,
                    DestroyUi = destroy == null ? null : Layer + destroy,
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax },
                        new CuiImageComponent { ItemId = itemID, SkinId = skinID, Sprite = sprite },
                    },
                });

            public static void Image(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", string image = "", string color = "1 1 1 1") =>
                container.Add(new CuiElement
                {
                    Parent = Layer + parent,
                    Name = Layer + name,
                    DestroyUi = destroy == null ? null : Layer + destroy,
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax },
                        new CuiRawImageComponent { Png = !image.StartsWith("http") && !image.StartsWith("www") ? image : null, Url = image.StartsWith("http") || image.StartsWith("www") ? image : null, Color = HexToRustFormat(color), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    },
                });

            public static void Label(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", string text = null, int fontSize = 16, string color = "1 1 1 1", TextAnchor align = TextAnchor.MiddleCenter, string outlineDistance = null, string outlineColor = "0 0 0 1", VerticalWrapMode wrapMode = VerticalWrapMode.Truncate, string font = "robotocondensed-regular.ttf") =>
                container.Add(new CuiElement
                {
                    Parent = Layer + parent,
                    Name = Layer + name,
                    DestroyUi = destroy == null ? null : Layer + destroy,
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax },
                        new CuiTextComponent { Text = text, FontSize = fontSize, Color = HexToRustFormat(color), Align = align, Font = font, VerticalOverflow = wrapMode },
                        outlineDistance == null ? new CuiOutlineComponent { Distance = "0 0", Color = "0 0 0 0" } : new CuiOutlineComponent { Distance = outlineDistance, Color = HexToRustFormat(outlineColor) },
                    },
                });

            public static void Button(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", string text = null, int fontSize = 16, string color = "1 1 1 1", string command = null, string bgColor = "0 0 0 0", VerticalWrapMode wrapMode = VerticalWrapMode.Truncate, TextAnchor align = TextAnchor.MiddleCenter, string font = "robotocondensed-regular.ttf", string material = null, string sprite = null) =>
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax },
                    Text = { Text = text, FontSize = fontSize, Color = HexToRustFormat(color), Align = align, Font = font, VerticalOverflow = wrapMode },
                    Button = { Command = command, Close = command == null ? Layer + name : null, Color = HexToRustFormat(bgColor), Material = material, Sprite = sprite }
                }, Layer + parent, command == null ? null : Layer + name, destroy == null ? null : Layer + destroy);

            public static void Input(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", string text = null, int limit = 40, int fontSize = 16, string color = "1 1 1 1", string command = null, TextAnchor align = TextAnchor.MiddleCenter, bool autoFocus = false, bool hudMenuInput = false, bool readOnly = false, bool isPassword = false, bool needsKeyboard = false, bool singleLine = true, string font = "robotocondensed-regular.ttf") =>
                container.Add(new CuiElement
                {
                    Parent = Layer + parent,
                    Name = Layer + name,
                    DestroyUi = destroy == null ? null : Layer + destroy,
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax },
                        new CuiInputFieldComponent { Text = text, Command = command, CharsLimit = limit, FontSize = fontSize, Color = HexToRustFormat(color), Align = align, Font = font, Autofocus = autoFocus, IsPassword = isPassword, ReadOnly = readOnly, HudMenuInput = hudMenuInput, NeedsKeyboard = needsKeyboard, LineType = singleLine ? InputField.LineType.SingleLine : InputField.LineType.MultiLineNewline },
                    }
                });


            #endregion

            #region CombineElements

            public static void Outline(ref CuiElementContainer container, string parent, string size = "1 1 1 1", string color = "0 0 0 1", bool external = false)
            {
                var borders = size.Split(' ');

                if (borders[0] != "0")
                    Panel(ref container, parent, aMin: "0 1", aMax: "1 1", oMin: $"-{borders[0]} {(external ? "0" : "-" + borders[0])}", oMax: $"{borders[0]} {(external ? borders[0] : "0")}", bgColor: color);
                if (borders[1] != "0")
                    Panel(ref container, parent, aMin: "1 0", aMax: "1 1", oMin: $"{(external ? "0" : "-" + borders[1])} -{borders[1]}", oMax: $"{(external ? borders[1] : "0")} {borders[1]}", bgColor: color);
                if (borders[2] != "0")
                    Panel(ref container, parent, aMin: "0 0", aMax: "1 0", oMin: $"-{borders[2]} {(external ? "-" + borders[2] : "0")}", oMax: $"{borders[2]} {(external ? "0" : borders[2])}", bgColor: color);
                if (borders[3] != "0")
                    Panel(ref container, parent, aMin: "0 0", aMax: "0 1", oMin: $"{(external ? "-" + borders[3] : "0")} -{borders[3]}", oMax: $"{(external ? "0" : borders[3])} {borders[3]}", bgColor: color);
            }

            public static void ImageButton(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", string image = null, string color = "0 0 0 0", string command = null)
            {
                Image(ref container, parent, name + ".bg", destroy, aMin, aMax, oMin, oMax, image, color);

                Button(ref container,
                    name + ".bg",
                    name: name,
                    command: command);
            }

            public static void PanelInput(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", string text = null, int limit = 40, int fontSize = 16, string color = "1 1 1 1", string command = null, TextAnchor align = TextAnchor.MiddleCenter, bool autoFocus = false, bool hudMenuInput = false, bool readOnly = false, bool isPassword = false, bool needsKeyboard = false, bool singleLine = true, string font = "robotocondensed-regular.ttf", string sprite = null, string bgColor = "0 0 0 0", string paddings = "0 0 0 0", string material = null)
            {
                Panel(ref container, parent, name + ".bg", destroy, aMin, aMax, oMin, oMax, bgColor, material);

                var padding = paddings.Split(' ');
                Input(ref container,
                    name + ".bg",
                    name,
                    oMin: $"{padding[3].ToInt()} {padding[2].ToInt()}",
                    oMax: $"{-padding[1].ToInt()} {-padding[0].ToInt()}",
                    text: text,
                    limit: limit,
                    fontSize: fontSize,
                    color: color,
                    command: command,
                    align: align,
                    autoFocus: autoFocus,
                    hudMenuInput: hudMenuInput,
                    needsKeyboard: needsKeyboard,
                    isPassword: isPassword,
                    readOnly: readOnly,
                    singleLine: singleLine,
                    font: font);
            }

            public static void PanelIcon(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", int itemID = 0, ulong skinID = 0, string sprite = null, string bgColor = "0 0 0 0", string paddings = "0 0 0 0", string material = null)
            {
                Panel(ref container, parent, name + ".bg", destroy, aMin, aMax, oMin, oMax, bgColor, material);

                var padding = paddings.Split(' ');
                Icon(ref container,
                    name + ".bg",
                    name,
                    oMin: $"{padding[3].ToInt()} {padding[2].ToInt()}",
                    oMax: $"{-padding[1].ToInt()} {-padding[0].ToInt()}",
                    itemID: itemID,
                    skinID: skinID);
            } 

            public static void ItemIcon(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", int itemID = 0, ulong skinID = 0, int amount = 1, int fontSize = 12, string color = "1 1 1 1", string bgColor = "0 0 0 0", string paddings = "0 0 0 0", string material = null)
            {
                PanelIcon(ref container, parent, name, destroy, aMin, aMax, oMin, oMax, itemID, skinID, null, bgColor, paddings, material);

                Label(ref container, name + ".bg", oMin: "0 2", oMax: "-4 0", text: $"x{amount}", align: TextAnchor.LowerRight, color: color, fontSize: fontSize);
            }




            #endregion

            #region Functions
            
            public static string HexToRustFormat(string hex)
            {
                if (string.IsNullOrEmpty(hex))
                    return hex;

                Color color;

                if (hex.Contains(":"))
                    return ColorUtility.TryParseHtmlString(hex.Substring(0, hex.IndexOf(":")), out color) ? $"{color.r:F2} {color.g:F2} {color.b:F2} {hex.Substring(hex.IndexOf(":") + 1, hex.Length - hex.IndexOf(":") - 1)}" : hex;

                return ColorUtility.TryParseHtmlString(hex, out color) ? $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}" : hex;
            }

            public static void Create(BasePlayer player, CuiElementContainer container)
            {
                CuiHelper.AddUi(player, container);
            }

            public static void CreateToAll(CuiElementContainer container, string layer)
            {
                foreach (var player in BasePlayer.activePlayerList)
                    CuiHelper.AddUi(player, container);
            }

            public static void Destroy(BasePlayer player, string layer) => CuiHelper.DestroyUi(player, Layer + layer);

            public static void DestroyToAll(string layer)
            {
                foreach (var player in BasePlayer.activePlayerList)
                    Destroy(player, layer);
            }

            #endregion
        }

        #endregion

        #endregion
    }
}