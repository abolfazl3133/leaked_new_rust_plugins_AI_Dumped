//#define Debug
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ItemDurability", "kaucsenta", "1.2.0")]
    [Description("Plugin to manipulate the weapon/item durability loss with percentage per permission")]
    internal class ItemDurability : RustPlugin
    {
        public PluginConfig config;
        public List<float> scales = new List<float>();
        private const string profile1 = "itemdurability.profile1";
        private const string profile2 = "itemdurability.profile2";
        private const string profile3 = "itemdurability.profile3";

        void OnLoseCondition(Item item, ref float amount)
        {
            BasePlayer owner = item?.GetOwnerPlayer();

#if Debug
            PrintToChat("1 - " + item.info.category.ToString() + " " + item.info.shortname);
#endif
            if (item != null && (owner != null || (item?.parentItem != null && item?.parentItem.info.category == ItemCategory.Weapon)) && (item.info.category == ItemCategory.Weapon || item.info.category == ItemCategory.Tool))
            {

                if(owner == null)
                {
                    owner = item?.parentItem?.GetOwnerPlayer();
                }
                if (owner == null)
                {
#if Debug
                    PrintToChat("No attachement owner - " + item.info.category.ToString() + " " + item.info.shortname);
#endif
                    return; /*No attachment owner*/
                }
                    
                if (item.info.category == ItemCategory.Weapon && config.excludeweapons)
                {
#if Debug
                    PrintToChat("1 - " + item.info.category.ToString() + " " + item.info.shortname);
#endif
                    return;
                }
                
                if (item.info.category == ItemCategory.Tool && config.excludeNOTweapons)
                {
#if Debug
                    PrintToChat("1 - " + item.info.category.ToString() + " " + item.info.shortname);
#endif
                    return;
                }

                if(item.info.shortname.Contains("keykard", System.Globalization.CompareOptions.IgnoreCase))
                {
#if Debug
                    /*exclude cards*/
                    PrintToChat("1 - " + item.info.category.ToString() + " " + item.info.shortname);
#endif
                    return;
                }
                    
                if (UserHasAnyPermission(owner))
                {
                    float scale = 1.0f;
                    int index = 0;
                    int multipermission = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        if (permission.UserHasPermission(owner.UserIDString, "itemdurability.profile" + (i+1).ToString()))
                        {
                            index = i;
                            multipermission++;
                        }
                    }
                    if (multipermission > 1 && config.multipermissioncheck )
                    {
                        if (config.multipermissioncheckwarning)
                        {
                            Puts("ItemDurability - Permission Error - " + owner.UserIDString + " has more then 1 profile, default scale (1.0f) used");
                        }
                        scale = 1.0f;
                        return;
                    }
                    scale = scales[index];
                    if (scale < 0.0f) scale = 0.0f;
                    amount *= scale;
#if Debug
                    PrintToChat("1 - " + amount.ToString() + " " + item.condition.ToString());
#endif
                }
            }
        }

        private bool UserHasAnyPermission(BasePlayer owner)
        {
            for(int i = 1; i<4; i++)
            {
                if (permission.UserHasPermission(owner.UserIDString, "itemdurability.profile" + i.ToString()))
                {
                    return true;
                }
            }
            return false;
        }

        private void Init()
        {
            LoadVariables();
            permission.RegisterPermission(profile1, this);
            permission.RegisterPermission(profile2, this);
            permission.RegisterPermission(profile3, this);
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig(config);
        }
        private void LoadConfigVariables()
        {
            config = Config.ReadObject<PluginConfig>();
            scales.Add(config.durabilitymodifierprofile1);
            scales.Add(config.durabilitymodifierprofile2);
            scales.Add(config.durabilitymodifierprofile3);
        }
        public class PluginConfig
        {
            [JsonProperty(PropertyName = "itemdurability.profile1 permission percentage scale range: 0-X, 0.1f mean 10% of the original durability loss")]
            public float durabilitymodifierprofile1 = 1.0f;
            [JsonProperty(PropertyName = "itemdurability.profile2 permission percentage scale range: 0-X, 0.1f mean 10% of the original durability loss")]
            public float durabilitymodifierprofile2 = 1.0f;
            [JsonProperty(PropertyName = "itemdurability.profile3 permission percentage scale range: 0-X, 0.1f mean 10% of the original durability loss")]
            public float durabilitymodifierprofile3 = 1.0f;
            [JsonProperty(PropertyName = "Permission check agains multiple permission group")]
            public bool multipermissioncheck = false;
            [JsonProperty(PropertyName = "Warning against multiple permission group (can flood the server console)")]
            public bool multipermissioncheckwarning = false;
            [JsonProperty(PropertyName = "Exclude weapons")]
            public bool excludeweapons = false;
            [JsonProperty(PropertyName = "Exclude items, what are not weapons")]
            public bool excludeNOTweapons = true;
            [JsonProperty(PropertyName = "CleanPermission on Wipe")]
            public bool cleanpermissiononwipe = true;
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            config = new PluginConfig
            {
                durabilitymodifierprofile1 = 1.0f,
                durabilitymodifierprofile2 = 1.0f,
                durabilitymodifierprofile3 = 1.0f,
                multipermissioncheck = false,
                multipermissioncheckwarning = false,
                excludeweapons = false,
                excludeNOTweapons = false,
                cleanpermissiononwipe = true,
            };
            SaveConfig(config);
        }
        void SaveConfig(PluginConfig config, string filename = null) => Config.WriteObject(config, true, filename);
        void OnNewSave(string filename)
        {
            if (config.cleanpermissiononwipe)
            {
                var players = covalence.Players.All.ToList();

                foreach (IPlayer user in players)
                {
                    permission.RevokeUserPermission(user.Id, profile1);
                    permission.RevokeUserPermission(user.Id, profile2);
                    permission.RevokeUserPermission(user.Id, profile3);
                }
                Puts("Itemslostdurabilityonnpchit permissions cleared");
            }
        }
    }
}