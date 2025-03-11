using Oxide.Core.Plugins;
using Oxide.Core;
using System;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SmoothNight", "Mrdecoder", "0.0.5")]
    [Description("make the day an night go Smooth and fast.")]
    public class SmoothNight : RustPlugin
    {
		bool Changed;
		bool Initialized;
		int componentSearchAttempts;
		TOD_Time timeComponent; 
		bool activatedDay;
		int dayTime;
		int nightTime;
		
		int Day;
		int Month;
		int Year;
		bool Preset;
		
		bool DEBUG;

		
		object GetConfig(string menu, string datavalue, object defaultValue)
		{
			var data = Config[menu] as Dictionary<string, object>;
			if (data == null)
			{
				data = new Dictionary<string, object>();
				Config[menu] = data;
				Changed = true;
			}
			object value;
			if (!data.TryGetValue(datavalue, out value))
			{
				value = defaultValue;
				data[datavalue] = value;
				Changed = true;
			}
			return value;
		}

		void LoadVariables()
		{
			
			
			DEBUG = System.Convert.ToBoolean(GetConfig("Settings", "DEBUG", false));
			
			dayTime =  System.Convert.ToInt32(GetConfig("Settings", "dayTime", 118));
			nightTime =  System.Convert.ToInt32(GetConfig("Settings", "nightTime", 2));
				
			Preset = System.Convert.ToBoolean(GetConfig("Settings", "Preset", false));
			Day =  System.Convert.ToInt32(GetConfig("Settings", "Preset_Day (if Preset is true)", 24));
			Month =  System.Convert.ToInt32(GetConfig("Settings", "Preset_Month (if Preset is true)", 7));
			Year =  System.Convert.ToInt32(GetConfig("Settings", "Preset_Year (if Preset is true)", 2021));
			
			if (!Changed) return;
			SaveConfig();
			Changed = false;
		}

		protected override void LoadDefaultConfig()
		{
			Config.Clear();
			LoadVariables();
		}

		void Loaded()
		{
			LoadVariables();
			Initialized = false;
		}

		void Unload()
		{
			if (timeComponent == null || !Initialized) return;
            timeComponent.OnSunset -= OnSunset;
			timeComponent.OnDay -= OnDay;
			timeComponent.OnHour -= OnHour;
		}

		void OnServerInitialized()
		{
			if (TOD_Sky.Instance == null)
            {
				componentSearchAttempts++;
                if (componentSearchAttempts < 5)
                    timer.Once(1, OnServerInitialized);
                else
                    PrintWarning("Could not find required component after 5 attempts. Plugin disabled");
                return;
            }
            timeComponent = TOD_Sky.Instance.Components.Time;
            if (timeComponent == null)
            {
                PrintWarning("Error no time. Plugin disabled");
                return;
            }
			
			if (Preset)
			{
				TOD_Sky.Instance.Cycle.Day = Day;
				TOD_Sky.Instance.Cycle.Month = Month;
				TOD_Sky.Instance.Cycle.Year = Year;
			}
			
			SetTimeComponent();
			
		}

        void SetTimeComponent()
        {
            timeComponent.ProgressTime = true;
            timeComponent.UseTimeCurve = false;
			timeComponent.OnSunset += OnSunset;
			timeComponent.OnDay += OnDay;
			timeComponent.OnHour += OnHour;
			Initialized = true;
            if (TOD_Sky.Instance.Cycle.Hour > 10.0f && TOD_Sky.Instance.Cycle.Hour < 18.0f) {
				
                OnSunrise();
				//Puts("Day already started");
			}
            else
                OnSunset();
        }
		
		
        void OnDay()
        {
			if (Initialized)
			{
				
			}
				//--TOD_Sky.Instance.Cycle.Day;
		}

        void OnHour()
        {
			if (!Initialized) return;
			if (TOD_Sky.Instance.Cycle.Hour > 10.0f && TOD_Sky.Instance.Cycle.Hour < 18.0f) 
			{
				
				OnSunrise();
				
				return;
			}
			if ((TOD_Sky.Instance.Cycle.Hour > 18.0f || TOD_Sky.Instance.Cycle.Hour < 10.0f) && activatedDay)
			{
				OnSunset();
				return;
			}
		}

        void OnSunrise()
        {
			if (!Initialized) return;
			
			timeComponent.DayLengthInMinutes = dayTime * (24.0f / (18.0f - 10.0f));
			
			Interface.CallHook("OnTimeSunrise");
			activatedDay = true;
			if (DEBUG)
			{
			Puts("day is active");
			}
        }

        void OnSunset()
        {
			if (!Initialized) return;
			
			timeComponent.DayLengthInMinutes = nightTime * (24.0f / (24.0f - (18.0f - 10.0f)));
			if (activatedDay)
				Interface.CallHook("OnTimeSunset");
			activatedDay = false;
        }
			

    }
}