//#define Debug
using System;
using Oxide.Core.Plugins;
using Oxide.Core;
using Facepunch;
using System.Linq;
using System.Collections.Generic;
using Rust;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("ItemsLostDurabilityonNPCHit", "kaucsenta", "1.4.1")]
    [Description("Items Lost Durability on NPCHit")]

    public class ItemsLostDurabilityonNPCHit : RustPlugin
    {
        ConfigData configData;
        public List<string> Meleeweapon = new List<string>(new string[] { "grenade.beancan.entity.prefab", "bone_club.entity.prefab", "knife_bone.entity.prefab", "chainsaw.entity.prefab", "salvaged_cleaver.entity.prefab", "grenade.f1.entity.prefab", "flamethrower.entity.prefab", "flamethrower_fireball.prefab", "hacksaw.weapon.prefab", "butcherknife.entity.prefab", "pitchfork.entity.prefab", "sickle.entity.prefab", "hammer.entity.prefab", "hatchet.entity.prefab", "knife.combat.entity.prefab", "mace.entity.prefab", "machete.weapon.prefab", "militaryflamethrower.entity.prefab", "paddle.entity.prefab", "pickaxe.entity.prefab", "rock.entity.prefab", "axe_salvaged.entity.prefab", "hammer_salvaged.entity.prefab", "icepick_salvaged.entity.prefab", "explosive.satchel.entity.prefab", "stonehatchet.entity.prefab", "stone_pickaxe.entity.prefab", "spear_stone.entity.prefab", "longsword.entity.prefab", "salvaged_sword.entity.prefab", "torch.entity.prefab", "spear_wooden.entity.prefab" });
        public List<string> Closeweapon = new List<string>(new string[] { "double_shotgun.entity.prefab", "pistol_eoka.entity.prefab", "m92.entity.prefab", "nailgun.entity.prefab", "shotgun_waterpipe.entity.prefab", "python.entity.prefab", "pistol_revolver.entity.prefab", "shotgun_pump.entity.prefab", "pistol_semiauto.entity.prefab", "smg.entity.prefab", "spas12.entity.prefab"});
        public List<string> Longweapon = new List<string>(new string[] { "mgl.entity.prefab", "semi_auto_rifle.entity.prefab", "thompson.entity.prefab", "rocket_launcher.entity.prefab", "mp5.entity.prefab", "l96.entity.prefab", "lr300.entity.prefab", "m249.entity.prefab", "m39.entity.prefab", "compound_bow.entity.prefab", "crossbow.entity.prefab", "ak47u.entity.prefab", "bow_hunting.entity.prefab", "hmlmg.entity.prefab" });
        private const string ProtectPermission = "itemslostdurabilityonnpchit.protectplayer";
        private const string profile1 = "itemslostdurabilityonnpchit.profile1";
        private const string profile2 = "itemslostdurabilityonnpchit.profile2";
        private const string profile3 = "itemslostdurabilityonnpchit.profile3";
        public List<float> scales = new List<float>();
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {

            if (entity != null && !entity.IsNpc && entity is BasePlayer)
            {
#if Debug
                Puts(info.Initiator.name);

                Puts(info.Initiator.PrefabName);
                Puts(info.Initiator.GetType().ToString());

                Puts(info.WeaponPrefab.name);

                Puts(entity._name);
                Puts("afterfirstIF");
                Puts((info.Initiator is BaseHelicopter).ToString());
                Puts(info.DidHit.ToString());
                Puts(info.HitPart.ToString());
                Puts(info.Weapon.IsValid().ToString());

                Puts(entity._name);                
                Puts("afterfirstIF");
                Puts(info.Initiator.IsNpc.ToString());
                Puts(info.DidHit.ToString());
                Puts(info.HitPart.ToString());
                Puts(info.Weapon.IsValid().ToString());
                Puts(info.damageTypes.Has(DamageType.Bullet).ToString());
                Puts(info.damageTypes.Has(DamageType.Generic).ToString());
#endif
                BasePlayer target = entity as BasePlayer;
                if (!permission.UserHasPermission(target.UserIDString, ProtectPermission))
                {
                    try
                    {
                        if (info.damageTypes.Has(DamageType.Bullet) || info.damageTypes.Has(DamageType.Slash) || info.damageTypes.Has(DamageType.Blunt) || info.damageTypes.Has(DamageType.Bite) || info.damageTypes.Has(DamageType.Stab) || info.damageTypes.Has(DamageType.Arrow))
                        {
                            if (info.Initiator.IsNpc || info.Initiator is BaseHelicopter || info.Initiator is AutoTurret || info.Initiator is BradleyAPC || info.Initiator is GunTrap || info.Initiator is BearTrap)
                            {

                                ItemContainer wearitems = target.inventory.containerWear;
                                List<Item> inventoryitemswithdurability = new List<Item>();
                                float durabilitypercent = 0f;
                                foreach (Item i in wearitems.itemList)
                                {
                                    if (i.hasCondition && i.condition != 0)
                                    {
                                        inventoryitemswithdurability.Add(i);
                                    }
                                }
                                if (info.Weapon.IsValid())
                                {
                                    if (Meleeweapon.Any(s => info.WeaponPrefab.PrefabName.Contains(s)))
                                        durabilitypercent = configData.MeleeDurabilityPercent;
                                    else if (Closeweapon.Any(s => info.WeaponPrefab.PrefabName.Contains(s)))
                                        durabilitypercent = configData.CloseDurabilityPercent;
                                    else if (Longweapon.Any(s => info.WeaponPrefab.PrefabName.Contains(s)))
                                        durabilitypercent = configData.LongDurabilityPercent;
#if Debug
                                Puts(durabilitypercent + " " + info.Weapon.name.ToString());
#endif
                                }
                                else
                                {
                                    durabilitypercent = configData.MeleeDurabilityPercent;
#if Debug
                                //Puts("2 " + info.Weapon.name.ToString());
                                Puts(durabilitypercent + " undef-melee");
#endif
                                }
                                if (UserHasAnyPermission(target))
                                {
                                    float scale = 1.0f;
                                    int index = 0;
                                    int multipermission = 0;
                                    for (int i = 0; i < 3; i++)
                                    {
                                        if (permission.UserHasPermission(target.UserIDString, "itemslostdurabilityonnpchit.profile" + (i + 1).ToString()))
                                        {
                                            index = i;
                                            multipermission++;
                                        }
                                    }
                                    if (multipermission > 1 && configData.multipermissioncheck)
                                    {
                                        if (configData.multipermissioncheckwarning)
                                        {
                                            Puts("ItemLostDurabilityonNPCHit - Permission Error - " + target.UserIDString + " has more then 1 profile, default scale (1.0f) used");
                                        }
                                        scale = 1.0f;
                                    }
                                    else
                                    {
                                        scale = scales[index];
                                        if (scale < 0.0f) scale = 0.0f;
                                    }
                                    durabilitypercent *= scale;
                                }
                                if (inventoryitemswithdurability.Count != 0)
                                {
                                    Item randomitem = inventoryitemswithdurability.GetRandom<Item>();
                                    randomitem.LoseCondition(randomitem.maxCondition * (durabilitypercent / 100));
                                    if (randomitem.condition <= 0f)
                                    {
                                        ItemContainer temp = new ItemContainer();
                                        temp.isServer = true;
                                        temp.allowedContents = ItemContainer.ContentsType.Generic;
                                        temp.GiveUID();
                                        temp.capacity = 1;
                                        int tempitempos = randomitem.position;
                                        randomitem.MoveToContainer(temp);

                                        NextTick(() =>
                                        {
                                            if(configData.popoff)
                                            {
                                                if(configData.toinventory)
                                                {
                                                    if (!target.inventory.containerMain.IsFull())
                                                    {
                                                        randomitem.MoveToContainer(target.inventory.containerMain, tempitempos);
                                                    }
                                                    else
                                                    {
                                                        randomitem.Drop(target.transform.position, target.eyes.BodyForward() * 1.5f);
                                                    }
                                                }
                                                else
                                                {
                                                    randomitem.Drop(target.transform.position, target.eyes.BodyForward() * 1.5f);
                                                }
                                            }
                                            else
                                            {

                                                randomitem.MoveToContainer(target.inventory.containerWear, tempitempos);
                                            }
                                            temp.Kill();
                                        });
                                    }
                                }
                            }
#if Debug
                        BasePlayer player = (BasePlayer)entity;
                        Puts(player.userID + " " + player.displayName + " OnEntityTakeDamage works! " + info.damageProperties.name);
#endif
                        }
                    }
                    catch (NullReferenceException ex)
                    {
#if Debug
                        Puts(info.damageTypes.types.ToSentence<float>());
                        Puts("error: "  + ex.Message);
#endif

                    }
                }
            }
            return null;
        }
        
        private bool UserHasAnyPermission(BasePlayer owner)
        {
            for (int i = 1; i < 4; i++)
            {
                if (permission.UserHasPermission(owner.UserIDString, "itemslostdurabilityonnpchit.profile" + i.ToString()))
                {
                    return true;
                }
            }
            return false;
        }

#region Oxide Hooks

        void Loaded()
        {
            permission.RegisterPermission(ProtectPermission, this);
            permission.RegisterPermission(profile1, this);
            permission.RegisterPermission(profile2, this);
            permission.RegisterPermission(profile3, this);
        }

        void OnServerInitialized()
        {
            LoadVariables();
        }
#if Debug
        void Unload()
        {
            Puts("ItemsLostDurabilityonNPCHit UnLoaded");
        }
#endif
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
            scales.Add(configData.durabilitymodifierprofile1);
            scales.Add(configData.durabilitymodifierprofile2);
            scales.Add(configData.durabilitymodifierprofile3);
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            ConfigData config = new ConfigData
            {
                MeleeDurabilityPercent = 0.5f,
                CloseDurabilityPercent = 0.75f,
                LongDurabilityPercent = 1.0f,
                durabilitymodifierprofile1 = 1.0f,
                durabilitymodifierprofile2 = 1.0f,
                durabilitymodifierprofile3 = 1.0f,
                multipermissioncheck = false,
                multipermissioncheckwarning = false,
                cleanpermissiononwipe = true,
                popoff = false,
                toinventory = false,
            };
            SaveConfig(config);
        }
        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        void OnNewSave(string filename)
        {
            if(configData.cleanpermissiononwipe)
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

        class ConfigData
        {
            public float MeleeDurabilityPercent;
            public float CloseDurabilityPercent;
            public float LongDurabilityPercent;
            public float ExplosionDurabilityPercent;

            [JsonProperty(PropertyName = "itemslostdurabilityonnpchit.profile1 permission percentage scale range: 0-X, 0.1f mean 10% of the original durability loss")]
            public float durabilitymodifierprofile1 = 1.0f;
            [JsonProperty(PropertyName = "itemslostdurabilityonnpchit.profile2 permission percentage scale range: 0-X, 0.1f mean 10% of the original durability loss")]
            public float durabilitymodifierprofile2 = 1.0f;
            [JsonProperty(PropertyName = "itemslostdurabilityonnpchit.profile3 permission percentage scale range: 0-X, 0.1f mean 10% of the original durability loss")]
            public float durabilitymodifierprofile3 = 1.0f;
            [JsonProperty(PropertyName = "Permission check agains multiple permission group")]
            public bool multipermissioncheck = false;
            [JsonProperty(PropertyName = "Warning against multiple permission group (can flood the server console)")]
            public bool multipermissioncheckwarning = false;
            [JsonProperty(PropertyName = "CleanPermission on Wipe")]
            public bool cleanpermissiononwipe = true;
            [JsonProperty(PropertyName = "Pop off the armor from the player, if broken")]
            public bool popoff = false;
            [JsonProperty(PropertyName = "The popped of armor will be placed into the player inventory (if false, it will be dropped on the floor) [Only relevant, if \"Pop off the armor from the player, if broken\" configuration set to true]")]
            public bool toinventory = false;

        }
#endregion
    }


}