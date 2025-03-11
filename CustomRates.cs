using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("CustomRates", "sdapro", "1.0.0")]
    internal class CustomRates : RustPlugin
    {
        #region OxideHooks

        #region GatherRates

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            OnDispenserGather(dispenser, player, item);
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || player == null || item == null)
                return;

            var gatherRateSettings = _config.GatherRates.DefaultSettings;
            var multiplier = gatherRateSettings.GatherRate;

            if (_config.GatherRates.EnableSpecializedGatherRates)
                gatherRateSettings.SGatherRate.TryGetValue(item.info.shortname, out multiplier);

            item.amount = (int) (item.amount * multiplier);
        }
        
        private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (collectible == null || player == null)
                return;

            var gatherRateSettings = _config.GatherRates.DefaultSettings;
            var multiplier = gatherRateSettings.CollectingRate;
            
            foreach (var check in collectible.itemList)
            {
                if (_config.GatherRates.EnableSpecializedGatherRates)
                    gatherRateSettings.SCollectingRate.TryGetValue(check.itemDef.shortname, out multiplier);

                check.amount = (int) (check.amount * multiplier);
                multiplier = gatherRateSettings.CollectingRate;
            }
        }
        
        private void OnGrowableGathered(GrowableEntity growable, Item item, BasePlayer player)
        {
            if (growable == null || item == null || player == null)
                return;
            
            var gatherRateSettings = _config.GatherRates.DefaultSettings;
            var multiplier = gatherRateSettings.PlanterBoxHarvesting;

            if (_config.GatherRates.EnableSpecializedGatherRates)
                gatherRateSettings.SPlanterBoxHarvesting.TryGetValue(item.info.shortname, out multiplier);

            item.amount = (int) (item.amount * multiplier);
        }
        
        private void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            if (quarry == null || item == null)
                return;

            var gatherRateSettings = _config.GatherRates.DefaultSettings;
            var multiplier = gatherRateSettings.QuarryRate;

            if (_config.GatherRates.EnableSpecializedGatherRates)
                gatherRateSettings.SQuarryRate.TryGetValue(item.info.shortname, out multiplier);

            item.amount = (int) (item.amount * multiplier);
        }

        private void OnExcavatorGather(ExcavatorArm excavator, Item item)
        {
            if (excavator == null || item == null)
                return;

            var gatherRateSettings = _config.GatherRates.DefaultSettings;
            var multiplier = gatherRateSettings.LargeExcavatorRate;

            if (_config.GatherRates.EnableSpecializedGatherRates)
                gatherRateSettings.SLargeExcavatorRate.TryGetValue(item.info.shortname, out multiplier);

            item.amount = (int) (item.amount * multiplier);
        }

        #endregion

        #endregion

        #region Config
        
        private class Configuration
        {
            [JsonProperty("Gather Rates")]
            public GatherRates GatherRates = new GatherRates
            {
                Enable = true,
                EnableSpecializedGatherRates = false,
                DefaultSettings = new GatherRateSettings
                {
                    GatherRate = 3,
                    CollectingRate = 3,
                    PlanterBoxHarvesting = 3,
                    QuarryRate = 3,
                    LargeExcavatorRate = 3,
                    SGatherRate = new Dictionary<string, float>(),
                    SCollectingRate = new Dictionary<string, float>(),
                    SPlanterBoxHarvesting = new Dictionary<string, float>(),
                    SQuarryRate = new Dictionary<string, float>(),
                    SLargeExcavatorRate = new Dictionary<string, float>()
                }
            };
        }
        

        private class LootTableItem
        {
            [JsonProperty("Shortname")]
            public string Shortname;

            [JsonProperty("SkinID")]
            public ulong Skin;

            [JsonProperty("Minimum amount")]
            public int Minimum;

            [JsonProperty("Maximum amount")]
            public int Maximum;

            [JsonProperty("Weigh")]
            public int Weigh;
        }
        
        private class GatherRates
        {
            [JsonProperty("Enable Gather Rates")]
            public bool Enable;

            [JsonProperty("Enable Specialized Gather Rates[Changes the rate for specific items. Example: all rates are x5, but the rate for sulfur is x10]")]
            public bool EnableSpecializedGatherRates;

            [JsonProperty("Default Gather Rates settings")]
            public GatherRateSettings DefaultSettings;
        }
        
        private class GatherRateSettings
        {
            [JsonProperty("Global Gathering Rate(When player farm rocks, trees, cactus)")]
            public float GatherRate;

            [JsonProperty("Global Collecting Rate(When player pick up stone, sulfur, wild plants, Diesel fuel, etc)")]
            public float CollectingRate;

            [JsonProperty("Global Harvesting from Planter Box")]
            public float PlanterBoxHarvesting;

            [JsonProperty("Global Quarry Rate")]
            public float QuarryRate;

            [JsonProperty("Global Large Excavator Rate")]
            public float LargeExcavatorRate;
            
            [JsonProperty("Specialized Gathering Rate[Item Shortname - Rate]", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> SGatherRate;

            [JsonProperty("Specialized Collecting Rate[Item Shortname - Rate]", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> SCollectingRate;
            
            [JsonProperty("Specialized Harvesting from Planter Box", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> SPlanterBoxHarvesting;

            [JsonProperty("Specialized Quarry Rate[Item Shortname - Rate]", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> SQuarryRate;

            [JsonProperty("Specialized Large Excavator Rate[Item Shortname - Rate]", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> SLargeExcavatorRate;
        }

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
    }
}