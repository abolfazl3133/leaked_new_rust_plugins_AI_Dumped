using Facepunch;
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using UnityEngine.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Game.Rust.Cui;
using System.Reflection;

namespace Oxide.Plugins
{
    /*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("BotReSpawn", "https://discord.gg/dNGbxafuJn", "1.2.5")]   
    [Description("Spawn tailored AI with kits at monuments, custom locations, or randomly.")]

    class BotReSpawn : RustPlugin
    {
        [PluginReference] Plugin Convoy, NoSash, Kits, CustomLoot, RustRewards, XPerience;
        int no_of_AI;
        bool loaded;
        static BotReSpawn bs;
        static System.Random random = new System.Random();
        public static string Get(ulong v) => RandomUsernames.Get((int)(v % 2147483647uL));

        public static bool IsNight()
        {
            if (bs.configData.Global.UseServerTime == true)
                return TOD_Sky.Instance.IsNight;

            if (bs.configData.Global.NightStartHour > bs.configData.Global.DayStartHour)
                return TOD_Sky.Instance.Cycle.Hour >= bs.configData.Global.NightStartHour || TOD_Sky.Instance.Cycle.Hour < bs.configData.Global.DayStartHour;
            else
                return TOD_Sky.Instance.Cycle.Hour >= bs.configData.Global.NightStartHour && TOD_Sky.Instance.Cycle.Hour < bs.configData.Global.DayStartHour;
        } 

        int GetRand(int l, int h) => random.Next(l, h);
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        const string permAllowed = "BotReSpawn.allowed";
        const string huff = "assets/prefabs/npc/murderer/sound/breathing.prefab";
        const string Parachute = "assets/prefabs/misc/parachute/parachute.prefab";
        const string RocketExplosion = "assets/prefabs/weapons/rocketlauncher/effects/rocket_explosion.prefab";
        const string LockedCrate = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";

        ItemDefinition fuel = ItemManager.FindItemDefinition("lowgradefuel");

        public List<ulong> HumanNPCs = new List<ulong>();
        public List<Vector3> protect = new List<Vector3>();
        public Dictionary<string, Profile> Profiles = new Dictionary<string, Profile>();
        Dictionary<string, LootContainer> Containers = new Dictionary<string, LootContainer>() { { "Default NPC", null } };
        LootContainer.LootSpawnSlot[] sc;
        public Dictionary<ulong, BotData> NPCPlayers = new Dictionary<ulong, BotData>();

        ItemDefinition scrap;
        #region Setup + TakeDown 
        void Init()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,  
            };
            no_of_AI = 0;
        }

        void Loaded()
        {
            ConVar.AI.npc_families_no_hurt = false;
            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission(permAllowed, this);
        }

        bool Unloading = false;
        void Unload()
        {
            Unloading = true;
            DestroySpawnGroups();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                DestroyMenu(player, true);
        }

        void OnServerInitialized()
        {
            PrintWarning("\n-----------------------------------------------------------------------------------------\n" +
           "     По всем вопросам: - DISCORD: SlivPlugin https://discord.gg/pFgKw6Dyyq \n" +
           "     Наш сайт: - https://slivplugin.ru \n" +
           "     Этот плагин исправлен Инкубом под заказ [Rust Plugin Sliv]\n" +
           "     Пожалуйста, оставьте отзыв\n" +
           "     Мне будет очень приятно!\n" +
           "-----------------------------------------------------------------------------------------");
            scrap = ItemManager.FindItemDefinition("scrap");
            sc = GameManager.server.FindPrefab("assets/prefabs/npc/scarecrow/scarecrow.prefab")?.GetComponent<ScarecrowNPC>()?.LootSpawnSlots;

            foreach (BasePlayer player in BaseNetworkable.serverEntities.OfType<BasePlayer>())
                ProcessHumanNPC(player);

            timer.Once(1f, () =>
            {
                if (NoSash)
                {
                    PrintWarning("NoSash plugin is installed");
                    PrintWarning("Target_Noobs option is now disabled.");
                    PrintWarning("BotReSpawn NPCs will target noob players.");
                }
                bs = this;
                GetBiomePoints();
                CheckMonuments(false);
                LoadConfigVariables();
                ImportFiles();
                loaded = true;
                SaveData();
                SetupProfiles();
                CheckKits();
                SetupLootSources();
                newsave = false;
                timer.Once(0.1f, () => CreateSpawnGroups());

                foreach (BradleyAPC apc in BaseNetworkable.serverEntities.OfType<BradleyAPC>())
                    SetupAPC(apc);

                if (!configData.Global.Turret_Safe)
                    Unsubscribe("CanBeTargeted");
            });

            // Remove in V1.1.0
            BaseEntity.saveList.RemoveWhere(p => !p);
            BaseEntity.saveList.RemoveWhere(p => p == null); 
        }

        void ProcessHumanNPC(BasePlayer player)
        {
            if (player == null || player.net?.connection != null || player.gameObject?.name == "BotReSpawn")
                return;
            foreach (var comp in player.GetComponents<Component>())
                if (comp?.GetType()?.Name == "HumanPlayer")
                {
                    HumanNPCs.Add(player.userID);
                    break;
                }
        }
        #endregion

        #region Setup Methods
        void GetBiomePoints()
        {
            var trees = BaseNetworkable.serverEntities.OfType<ResourceEntity>().Where(x => x is TreeEntity || x.ShortPrefabName.Contains("cactus"));
            string biomename = "";
            List<string> names = new List<string>();
            int biome = -1;
            Vector3 point = Vector3.zero;
            foreach (var tree in trees.ToList())
            {
                biome = TerrainMeta.BiomeMap.GetBiomeMaxType(tree.transform.position, -1);
                point = CalculateGroundPos(tree.transform.position + Vector3.forward / 2f, false, false);
                if (point != Vector3.zero)
                {
                    biomename = $"Biome{Enum.GetName(typeof(TerrainBiome.Enum), biome)}";
                    if (BiomeSpawns.ContainsKey(biomename))
                        BiomeSpawns[biomename].Add(point);
                    else
                        BiomeSpawns.Add(biomename, new List<Vector3> { point });
                }
            }
        }
        List<Vector3> OilRigs = new List<Vector3>();

        void CheckMonuments(bool add)
        {
            GameObject gobject;
            Vector3 pos;
            float rot;

            foreach (var monumentInfo in TerrainMeta.Path.Monuments.OrderBy(x => x.displayPhrase.english))
            {
                var displayPhrase = monumentInfo.displayPhrase.english.Replace("\n", String.Empty);
                if (displayPhrase.Contains("Water Well"))
                    continue;

                if (monumentInfo?.gameObject?.name != null)
                    displayPhrase = ProcessName(monumentInfo.gameObject.name, displayPhrase);

                gobject = monumentInfo.gameObject;
                pos = gobject.transform.position;
                rot = gobject.transform.eulerAngles.y;
                int counter = 0;

                if (displayPhrase != String.Empty)
                {
                    if (add)
                    {
                        if (displayPhrase.Contains("Oil Rig"))
                            OilRigs.Add(pos);

                        foreach (var entry in Profiles.Where(x => x.Key.Contains(displayPhrase) && (x.Key.Length == displayPhrase.Length + 2 || x.Key.Length == displayPhrase.Length + 3)))
                            counter++;
                        AddProfile(gobject, $"{displayPhrase} {counter}", null, pos);
                    }
                    else
                    {
                        foreach (var entry in GotMonuments.Where(x => x.Key.Contains(displayPhrase) && (x.Key.Length == displayPhrase.Length + 2 || x.Key.Length == displayPhrase.Length + 3)))
                            counter++;
                        GotMonuments.Add($"{displayPhrase} {counter}", new Profile(ProfileType.Monument));
                    }
                }
            }
        }

        List<string> numerical = new List<string>() { "mountain_", "power_sub_big_", "power_sub_small_", "ice_lake_" };
        string ProcessName(string name, string displayPhrase)
        {
            foreach (var n in numerical)
            {
                int num;
                if (name.Length > n.Length && int.TryParse(name.Substring(name.IndexOf(n) + n.Length, 1), out num))
                {
                    displayPhrase += name.Contains("sub_big_") ? "_Large" : name.Contains("sub_small_") ? "_Small" : "";
                    return displayPhrase + $"_{num}";
                }
            }
            if (displayPhrase == "Harbor")
                displayPhrase += name.Contains("_1") ? "_Small" : "_Large";
            else if (displayPhrase == "Fishing Village" || displayPhrase == "Wild Swamp")
                displayPhrase += name.Contains("_a") ? "_A" : name.Contains("_b") ? "_B" : "_C";

            return displayPhrase;
        }

        void ImportFiles()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>($"BotReSpawn/{configData.DataPrefix}-CustomProfiles");
            defaultData = Interface.Oxide.DataFileSystem.ReadObject<DefaultData>($"BotReSpawn/{configData.DataPrefix}-DefaultProfiles");
            if (!configData.Global.Allow_Oilrigs)
                defaultData.Monuments = defaultData.Monuments.Where(x => !x.Key.Contains("Oil Rig")).ToDictionary(pair => pair.Key, pair => pair.Value);
            spawnsData = Interface.Oxide.DataFileSystem.ReadObject<SpawnsData>($"BotReSpawn/{configData.DataPrefix}-SpawnsData");
            templateData = Interface.Oxide.DataFileSystem.ReadObject<TemplateData>("BotReSpawn/TemplateData");
        }

        Dictionary<string, List<Vector3>> BiomeSpawns = new Dictionary<string, List<Vector3>>()
        {
            { "BiomeArid", new List<Vector3>() },
            { "BiomeTemperate", new List<Vector3>() },
            { "BiomeTundra", new List<Vector3>() },
            { "BiomeArctic", new List<Vector3>() }
        };

        private void SetupProfiles()
        {
            CheckMonuments(true);

            foreach (var biome in defaultData.Biomes)
                AddProfile(new GameObject(), biome.Key, biome.Value, new Vector3());

            foreach (var e in defaultData.Events)
                AddProfile(new GameObject(), e.Key, e.Value, new Vector3());

            foreach (var profile in storedData.Profiles)
                AddData(profile.Key, profile.Value);

            SaveData();
            SetupSpawnsFile();
            foreach (var profile in Profiles.Where(x => x.Value.type == ProfileType.Custom || x.Value.type == ProfileType.Monument))
                if (profile.Value.Spawn.Kit.Count > 0 && Kits == null)
                    PrintWarning(lang.GetMessage("nokits", this), profile.Key);
        }

        void CheckKits()
        {
            ValidKits.Clear();
            Kits?.Call("GetKitNames", new object[] { ValidKits });

            var names = Kits?.Call("GetAllKits");
            if (names != null)
                ValidKits.AddRange((string[])names);

            ValidKits = ValidKits.Distinct().ToList();
            foreach (var kit in ValidKits.ToList())
            {
                object checkKit = Kits?.CallHook("GetKitInfo", kit, true);
                bool weaponInKit = false;
                JObject kitContents = checkKit as JObject;
                if (kitContents != null)
                {
                    JArray items = kitContents["items"] as JArray;
                    foreach (var weap in items)
                    {
                        JObject item = weap as JObject;
                        if (Isweapon(item["itemid"].ToString(), null))
                        {
                            weaponInKit = true;
                            break;
                        }
                    }
                }
                if (!weaponInKit)
                    ValidKits.Remove(kit);
            }

            if (Kits)
                foreach (var profile in Profiles)
                    profile.Value.Spawn.Kit = profile.Value.Spawn.Kit.Where(x => ValidKits.Contains(x)).ToList();

            ValidKits = ValidKits.OrderBy(x => x.ToString()).ToList();
        }

        void SetupLootSources()
        {
            List<string> ignore = Pool.GetList<string>();
            ignore.AddRange("hidden,test,shelves,stocking,mission".Split(','));
            Containers.Add("ScarecrowNPC", null);
            Containers.Add("Random", null);

            foreach (var entry in Resources.FindObjectsOfTypeAll<LootContainer>().Where(x => x != null && !x.isSpawned).ToList())
            {
                bool skip = false;
                foreach (var i in ignore)
                    if (entry.PrefabName.Contains(i))
                        skip = true;

                if (skip)
                    continue;

                string name = GetLootName(entry.PrefabName, entry.ShortPrefabName);
                if (Containers.ContainsKey(name))
                    continue;
                Containers.Add(name, entry);
            }
            Pool.FreeList<string>(ref ignore);
        }
        #endregion

        #region Helpers
        public string GetLootName(string name, string name2) => name.Contains("underwater_labs") ? "underwater_labs_" + name2 : name2;

        public void PopulateLoot(LootContainer source, ItemContainer container)
        {
            LootContainer.LootSpawnSlot[] lootSpawnSlots = source == null ? sc : source.LootSpawnSlots;
            if (lootSpawnSlots.Length != 0)
            {
                for (int i = 0; i < lootSpawnSlots.Length; i++)
                {
                    LootContainer.LootSpawnSlot lootSpawnSlot = lootSpawnSlots[i];
                    for (int j = 0; j < lootSpawnSlot.numberToSpawn; j++)
                        if (UnityEngine.Random.Range(0f, 1f) <= lootSpawnSlot.probability)
                            lootSpawnSlot.definition.SpawnIntoContainer(container);
                }
            }
            else if (source?.lootDefinition != null)
                for (int k = 0; k < source.maxDefinitionsToSpawn; k++)
                    source.lootDefinition.SpawnIntoContainer(container);

            if (source?.SpawnType == LootContainer.spawnType.ROADSIDE || source?.SpawnType == LootContainer.spawnType.TOWN)
                foreach (Item item in container.itemList)
                    if (item.hasCondition)
                        item.condition = UnityEngine.Random.Range(item.info.condition.foundCondition.fractionMin, item.info.condition.foundCondition.fractionMax) * item.info.condition.max;
            if (source != null && source.scrapAmount > 0)
            {
                var sc = ItemManager.Create(scrap, source.scrapAmount, 0);
                if (sc.MoveToContainer(container))
                    sc.Remove(0f);
            }
        }

        static BasePlayer FindPlayerByName(string name)
        {
            BasePlayer result = null;
            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                if (current.displayName.Equals(name, StringComparison.OrdinalIgnoreCase)
                    || current.UserIDString.Contains(name, CompareOptions.OrdinalIgnoreCase)
                    || current.displayName.Contains(name, CompareOptions.OrdinalIgnoreCase))
                    result = current;
            }
            return result;
        }

        public bool Isweapon(string i, Item item)
        {
            if (i != string.Empty)
                item = ItemManager.CreateByItemID(Convert.ToInt32(i), 1);
            var held = item?.GetHeldEntity();
            bool weapon = item?.info?.category == ItemCategory.Weapon || (held != null && (held as ThrownWeapon || held as BaseMelee || held as TorchWeapon));

            if (i != string.Empty && item != null)
                item.Remove();

            return weapon;
        }
        #endregion

        #region Hooks
        bool newsave = false;
        void OnNewSave(string filename) => newsave = true;

        object OnNpcKits(ulong userID) => NPCPlayers.ContainsKey(userID) ? true : (object)null;
        object OnNpcDuck(global::HumanNPC npc) => NPCPlayers.ContainsKey(npc.userID) ? true : (object)null;
        private object CanEntityBeHostile(ScientistNPC npc) => npc != null && NPCPlayers.ContainsKey(npc.userID) ? true : (object)null;

        private object CanBeTargeted(NPCPlayer npc, BaseEntity entity) => (npc != null && configData?.Global != null && configData.Global.Turret_Safe && NPCPlayers.ContainsKey(npc.userID)) ? false : (object)null;
        //private object OnTurretTarget(NPCPlayer npc) => (npc != null && configData?.Global != null && configData.Global.Turret_Safe && NPCPlayers.ContainsKey(npc.userID)) ? false : (object)null; // Probably lighter? Make sure it works.

        private object CanBradleyApcTarget(BradleyAPC bradley, NPCPlayer npc)
        {
            if (Convoy && (bool)Convoy?.Call("IsConvoyVehicle", bradley))
                return null;
            return npc == null ? null : NPCPlayers.ContainsKey(npc.userID) ? !configData.Global.APC_Safe : (object)null;
        }

        private void OnEntitySpawned(BradleyAPC apc) => SetupAPC(apc);
        void OnEntitySpawned(BasePlayer player) => NextTick(() => { ProcessHumanNPC(player); });

        void SetupAPC(BradleyAPC apc) => apc.InvokeRepeating(() => UpdateTargetList(apc), 0f, 2f);

        public void UpdateTargetList(BradleyAPC apc)
        {
            List<BasePlayer> list = Pool.GetList<BasePlayer>();
            Vis.Entities<BasePlayer>(apc.transform.position, apc.searchRange, list, 133120, QueryTriggerInteraction.Collide);
            foreach (var player in list)
            {
                if (!NPCPlayers.ContainsKey(player.userID) || !apc.VisibilityTest(player))
                    continue;

                bool flag = false;
                foreach (BradleyAPC.TargetInfo targetInfo in apc.targetList)
                {
                    if (targetInfo.entity == player)
                    {
                        targetInfo.lastSeenTime = Time.time;
                        flag = true;
                        break;
                    }
                }

                if (flag)
                    continue;

                BradleyAPC.TargetInfo targetInfo1 = Pool.Get<BradleyAPC.TargetInfo>();
                targetInfo1.Setup(player, Time.time);
                apc.targetList.Add(targetInfo1);
            }
            Pool.FreeList<BasePlayer>(ref list);
        }

        object OnEntityTakeDamage(BaseHelicopter heli, HitInfo info) => info?.InitiatorPlayer != null && NPCPlayers.ContainsKey(info.InitiatorPlayer.userID) ? true : (object)null;

        object OnEntityTakeDamage(ScientistNPC player, HitInfo info)
        {
            if (player == null || info == null)
                return null;

            BotData bData1;
            NPCPlayers.TryGetValue(player.userID, out bData1);

            if (bData1?.profile == null)
                return null;

            if (info?.Initiator == player || info?.Initiator is BaseAnimalNPC)
                return true;

            if (bData1.invincible || (bData1.profile.Other.Fire_Safe && info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Heat))
                return true;

            if (info.InitiatorPlayer != null)
            {
                if (bData1.profile.Behaviour.Friendly_Fire_Safe && NPCPlayers.ContainsKey(info.InitiatorPlayer.userID) && info.InitiatorPlayer != bData1.CurrentTarget)
                    return true;

                if (bData1.brain != null)
                {
                    if (bData1.inAir)
                    {
                        if (bData1.profile.Other.Invincible_Whilst_Chuting)
                            return true;
                        info.damageTypes.ScaleAll(10);
                    }
                    if (bData1.profile.Behaviour.Respect_Safe_Zones && player.InSafeZone())
                        return true;
                    if (info.InitiatorPlayer != bData1.CurrentTarget && Vector3.Distance(info.InitiatorPlayer.transform.position, player.transform.position) >= bData1.profile.Other.Immune_From_Damage_Beyond)
                        return true;

                    if (info.InitiatorPlayer == bData1.CurrentTarget || bData1.Targets.ContainsKey(info.InitiatorPlayer) && (!player.limitNetworking && !player._limitedNetworking))
                        SetTarget(bData1, bData1.brain, info.InitiatorPlayer, true);
                    else if (bData1.WantsAttack(info.InitiatorPlayer, true))
                    {
                        SetTarget(bData1, bData1.brain, info.InitiatorPlayer, true);
                        if (configData.Global.NPCs_Assist_NPCs && info.InitiatorPlayer.net?.connection != null)
                        {
                            var weapon = info?.Weapon as BaseProjectile;
                            List<BotData> bDatas = Pool.GetList<BotData>();
                            Vis.Components<BotData>(player.transform.position, (weapon != null && weapon.IsSilenced() ? 3 : Mathf.Max(50, bData1.profile.Behaviour.Assist_Sense_Range)), bDatas);
                            foreach (var bData in bDatas)
                            {
                                if (bData.brain == null || bData == bData1)
                                    continue;

                                if (info.InitiatorPlayer != bData.CurrentTarget && Vector3.Distance(info.InitiatorPlayer.transform.position, player.transform.position) >= bData.profile.Other.Immune_From_Damage_Beyond)
                                    continue;

                                if (info.InitiatorPlayer == bData.CurrentTarget || bData.profilename == bData1.profilename || bData.Targets.ContainsKey(info.InitiatorPlayer))//bData.WantsAttack(info.InitiatorPlayer, true))
                                {
                                    timer.Once(Vector3.Distance(bData.transform.position, bData1.transform.position) / 5f, () =>
                                    {
                                        if (bData?.profile != null && bData.brain?.Senses != null && info.InitiatorPlayer != null && !info.InitiatorPlayer.IsDead())
                                            SetTarget(bData, bData.brain, info.InitiatorPlayer, false);
                                    });
                                }
                                //else
                                //{
                                //    No LOS? SetDestination(...
                                //}
                            }
                            Pool.FreeList<BotData>(ref bDatas);
                        }
                    }
                }

                if (info != null && bData1.profile.Other.Die_Instantly_From_Headshot && info.isHeadshot)
                {
                    var weap = info?.Weapon?.GetItem()?.info?.shortname;
                    var weaps = bData1.profile.Other.Instant_Death_From_Headshot_Allowed_Weapons;

                    if (weaps.Count == 0 || (weap != null && weaps.Contains(weap)))
                    {
                        if (!bData1.profile.Other.Require_Two_Headshots || Headshots.Contains(player.userID))
                            info.damageTypes.Set(0, player.health);
                        else
                            Headshots.Add(player.userID);
                        return null;
                    }
                }
            }

            if (configData.Global.APC_Safe && info?.Initiator is BradleyAPC)
                return true;

            if (configData.Global.Pve_Safe)
            {
                var att = info.Initiator?.ToString();
                if (att == null && info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Bullet)
                    return true;
                if (att == null || att.Contains("cactus") || att.Contains("barricade"))
                    return true;
            }
            return null;
        }

        public List<ulong> Headshots = new List<ulong>();

        void SetTarget(BotData bData, BaseAIBrain brain, BasePlayer target, bool delay)
        {
            brain.Navigator.SetFacingDirectionEntity(target);

            if (delay)
            {
                timer.Once(random.Next(2, 15) / 10f, () =>
                {
                    bData.Addto(bData.Players, target);
                    bData.Addto(bData.Targets, target);
                    bData.CurrentTarget = target;
                });
            }
            else
            {
                bData.Addto(bData.Players, target);
                bData.Addto(bData.Targets, target);
                bData.CurrentTarget = target;
            }
        }

        object OnEntityKill(ScientistNPC npc) 
        {
            if (npc == null || npc.IsDestroyed)
                return null;

            return !Unloading && npc?.gameObject?.name == "BotReSpawn" ? true : (object)null;
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null)
                return;

            if (HumanNPCs.Contains(player.userID))
                HumanNPCs.Remove(player.userID);

            if (player?.net?.connection != null)
                DestroyMenu(player, true);

            ScientistNPC npc = player as ScientistNPC;
            if (npc != null)
                OnEntityKill(npc, info, info?.InitiatorPlayer != null);
        }

        List<ulong> BSCrates = new List<ulong>();

        void OnEntityKill(ScientistNPC npc, HitInfo info, bool killed)
        {
            if (npc?.userID != null && NPCPlayers.ContainsKey(npc.userID) && !botInventories.ContainsKey(npc.userID))
            {
                botInventories.Add(npc.userID, null);

                if (Headshots.Contains(npc.userID))
                    Headshots.Remove(npc.userID);

                BotData bData;
                NPCPlayers.TryGetValue(npc.userID, out bData);
                if (bData == null || !Profiles.ContainsKey(bData?.profilename))
                    return;

                if (!bData.temporary)
                    timer.Once(Mathf.Max(1, bData.profile.Death.Respawn_Timer * 60), () => bData.sg?.SpawnBot(1, bData.profile.type == ProfileType.Biome || bData.profile.Spawn.ChangeCustomSpawnOnDeath ? null : bData.sp));

                if (killed)
                {
                    if (!info.InitiatorPlayer.IsNpc)
                    {
                        if (bData.profile.Death.RustRewardsValue != 0)
                        {
                            var weapon = info?.Weapon?.GetItem()?.info?.shortname ?? info?.WeaponPrefab?.ShortPrefabName ?? "";
                            RustRewards?.Call("GiveRustReward", info.InitiatorPlayer, 0, bData.profile.Death.RustRewardsValue, npc, weapon, Vector3.Distance(info.InitiatorPlayer.transform.position, npc.transform.position), null);
                        }
                        if (bData.profile.Death.XPerienceValue > 0)
                            XPerience?.Call("GiveXP", info.InitiatorPlayer, bData.profile.Death.XPerienceValue);
                    }
                    if (bData.profile.Death.Spawn_Hackable_Death_Crate_Percent > 0 && bData.profile.Death.Spawn_Hackable_Death_Crate_Percent > GetRand(1, 101) && npc.WaterFactor() < 0.1f)
                    {
                        var pos = npc.transform.position;
                        timer.Once(2f, () =>
                        {
                            if (bData?.profile == null)
                                return;
                            var Crate = (HackableLockedCrate)GameManager.server.CreateEntity(LockedCrate, pos + new Vector3(1, 2, 0), Quaternion.Euler(0, 0, 0));
                            Crate.Spawn();
                            BSCrates.Add(Crate.net.ID.Value);

                            Crate.hackSeconds = HackableLockedCrate.requiredHackSeconds - bData.profile.Death.Death_Crate_LockDuration * 60;
                            timer.Once(1.4f, () =>
                            {
                                if (Crate == null || bData?.profile == null)
                                    return;
                                if (CustomLoot && bData.profile.Death.Death_Crate_CustomLoot_Profile != string.Empty)
                                {
                                    if (Crate != null) ////Check
                                    {
                                        Crate.inventory.capacity = 36;
                                        Crate.onlyAcceptCategory = ItemCategory.All;
                                        Crate.SendNetworkUpdateImmediate();
                                        Crate.inventory.Clear();

                                        List<Item> loot = (List<Item>)CustomLoot?.Call("MakeLoot", bData.profile.Death.Death_Crate_CustomLoot_Profile);
                                        if (loot != null)
                                            foreach (var item in loot)
                                                if (!item.MoveToContainer(Crate.inventory, -1, true))
                                                    item.Remove();
                                    }
                                }
                            });
                        });
                    }
                    Interface.CallHook("OnBotReSpawnNPCKilled", npc, bData.profilename, bData.group, info);
                }

                Item activeItem = npc.GetActiveItem();
                if (activeItem != null && bData.profile.Death.Weapon_Drop_Percent > 0 && bData.profile.Death.Weapon_Drop_Percent >= GetRand(1, 101))
                {
                    var numb = GetRand(Mathf.Min(bData.profile.Death.Min_Weapon_Drop_Condition_Percent, bData.profile.Death.Max_Weapon_Drop_Condition_Percent), bData.profile.Death.Max_Weapon_Drop_Condition_Percent);
                    numb = Convert.ToInt16((numb / 100f) * activeItem.maxCondition);
                    activeItem.condition = numb;
                    activeItem.Drop(npc.eyes.position, new Vector3(), new Quaternion());
                    npc.svActiveItemID = new ItemId();
                    npc.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                }

                ItemContainer[] source = { npc.inventory.containerMain, npc.inventory.containerWear, npc.inventory.containerBelt };
                Inv botInv = new Inv() { profile = bData.profile, name = npc.displayName, };
                botInventories[npc.userID] = botInv;
                for (int i = 0; i < source.Length; i++)
                    foreach (var item in source[i].itemList)
                    {
                        botInv.inventory[i].Add(new InvContents
                        {
                            ID = item.info.itemid,
                            amount = item.amount,
                            skinID = item.skin,
                        });
                    }

                DeadNPCPlayerIds.Add(npc.userID, bData.profile.Other.Backpack_Duration * 60);
                no_of_AI--;
            }
        }
        #endregion

        #region Events
        List<ulong> CHCrates = new List<ulong>();
        List<CH47Helicopter> helis = new List<CH47Helicopter>();
        bool NearCH(HackableLockedCrate crate)
        {
            helis.Clear();
            Vis.Entities<CH47Helicopter>(crate.transform.position, 10f, helis);
            return helis.Any();
        }

        List<ulong> ORCrates = new List<ulong>();

        bool NearOR(HackableLockedCrate crate)
        {
            foreach (var entry in OilRigs)
                if (Vector3.Distance(crate.transform.position, entry) < 100)
                    return true;
            return false;
        }

        bool ValidCrate(HackableLockedCrate crate)
        {
            if (crate.OwnerID != 0)
                return configData.Global.Allow_HackableCrates_With_OwnerID;
            if (CHCrates.Contains(crate.net.ID.Value))
                return configData.Global.Allow_HackableCrates_From_CH47;
            if (ORCrates.Contains(crate.net.ID.Value))
                return configData.Global.Allow_HackableCrates_At_Oilrig;
            if (crate.HasParent() || crate.transform?.parent != null)
                return configData.Global.Allow_Parented_HackedCrates;
            return configData.Global.Allow_All_Other_HackedCrates;
        }

        void OnEntitySpawned(HackableLockedCrate crate)
        {
            if (!loaded || crate?.net?.ID == null)
                return;

            NextTick(() =>
            {
                if (crate?.net?.ID == null || !loaded || BSCrates.Contains(crate.net.ID.Value))
                    return;

                if (Interface.CallHook("OnBotReSpawnCrateDropped", crate) != null)
                    return;

                if (NearCH(crate))
                    CHCrates.Add(crate.net.ID.Value);

                if (NearOR(crate))
                    ORCrates.Add(crate.net.ID.Value);

                var nearest = FindNearestDefault(crate.transform.position);
                if (nearest != null && nearest.Other.Block_Event_LockedCrate_Spawn)
                    return;

                if (ValidCrate(crate))
                    DoEvent("LockedCrate_Spawn", crate.transform.position);
            });
        }
        
        void OnCrateHack(HackableLockedCrate crate)
        {
            NextTick(() =>
            {
                if (crate?.net?.ID == null || !loaded)
                    return;

                if (Interface.CallHook("OnBotReSpawnCrateHackBegin", crate) != null)
                    return;

                var nearest = FindNearestDefault(crate.transform.position);
                if (nearest != null && nearest.Other.Block_Event_LockedCrate_HackStart)
                    return;

                if (ValidCrate(crate))
                    DoEvent("LockedCrate_HackStart", crate.transform.position);
            });
        }

        void OnEntityDeath(BaseEntity entity)
        {
            if (entity == null || entity.transform == null || entity is BasePlayer)
                return;

            string prof = string.Empty;
            if (entity is BradleyAPC)
            {
                if (Interface.CallHook("OnBotReSpawnAPCKill", entity) != null)
                    return;

                var nearest = FindNearestDefault(entity.transform.position);
                if (nearest != null && nearest.Other.Block_Event_APC_Kill)
                    return;

                prof = "APC_Kill";
            }
            else if (entity is PatrolHelicopter)
            {
                if (Interface.CallHook("OnBotReSpawnPatrolHeliKill", entity) != null)
                    return;

                var nearest = FindNearestDefault(entity.transform.position);
                if (nearest != null && nearest.Other.Block_Event_PatrolHeli_Kill)
                    return;

                prof = "PatrolHeli_Kill";
            }
            else if (entity is CH47Helicopter)
            {
                if (Interface.CallHook("OnBotReSpawnCH47Kill", entity) != null)
                    return;

                var nearest = FindNearestDefault(entity.transform.position);
                if (nearest != null && nearest.Other.Block_Event_CH47_Kill)
                    return;

                prof = "CH47_Kill";
            }

            if (prof != string.Empty)
                DoEvent(prof, entity.transform.position);
        }

        void DoEvent(string name, Vector3 pos)
        {
            Profile profile = Profiles[name];
            if (profile.Spawn.AutoSpawn == true && GetPop(profile) > 0)
            {
                int quantity = GetPop(profile);
                if (profile.Spawn.AutoSpawn == true && quantity > 0)
                {
                    profile.Other.Location = pos;
                    CreateTempSpawnGroup(pos, name, profile, string.Empty, quantity);
                }
            }
        }
        #endregion

        #region SpawningHooks  
        void OnEntitySpawned(DroppedItemContainer container)
        {
            NextTick(() =>
            {
                if (!loaded || container?.playerSteamID == null || container.IsDestroyed || container.playerSteamID == 0)
                    return;

                if (DeadNPCPlayerIds.ContainsKey(container.playerSteamID))
                {
                    if (configData.Global.Remove_BackPacks_Percent >= GetRand(1, 101))
                    {
                        if (container != null && !container.IsDestroyed)
                            container.Kill();
                    }
                    else
                    {
                        container.CancelInvoke(container.RemoveMe);
                        timer.Once(DeadNPCPlayerIds[container.playerSteamID], () =>
                        {
                            if (container != null && !container.IsDestroyed)
                                container?.Kill();
                        });
                    }
                    DeadNPCPlayerIds.Remove(container.playerSteamID);
                }
            });
        }

        void OnEntitySpawned(SupplySignal signal)
        {
            if (!loaded || configData.Global.Ignore_Skinned_Supply_Grenades && signal.skinID != 0)
                return;

            timer.Once(2.3f, () =>
            {
                if (!loaded || signal != null)
                    SmokeGrenades.Add(new Vector3(signal.transform.position.x, 0, signal.transform.position.z));
            });
        }

        void OnEntitySpawned(SupplyDrop drop)
        {
            if (!loaded || (!drop.name.Contains("supply_drop") && !drop.name.Contains("sleigh/presentdrop")))
                return;

            if (Interface.CallHook("OnBotReSpawnAirdrop", drop) != null)
                return;

            if (!configData.Global.Supply_Enabled)
            {
                foreach (var location in SmokeGrenades.Where(location => Vector3.Distance(location, new Vector3(drop.transform.position.x, 0, drop.transform.position.z)) < 35f))
                {
                    SmokeGrenades.Remove(location);
                    return;
                }
            }

            Profile profile = null;
            Profiles.TryGetValue("AirDrop", out profile);

            if (profile != null)
                DoEvent("AirDrop", drop.transform.position);
        }

        //object CanPopulateLoot(ScientistNPC entity, NPCPlayerCorpse corpse) => botInventories.ContainsKey(corpse.playerSteamID) ? true : (object)null;
        object CanPopulateLoot(ScientistNPC entity, NPCPlayerCorpse corpse) => !configData.Global.Allow_AlphaLoot && botInventories.ContainsKey(corpse.playerSteamID) ? true : (object)null;

        void OnEntitySpawned(NPCPlayerCorpse corpse)
        {
            if (!loaded || corpse == null || corpse.IsDestroyed)
                return;

            Inv botInv = new Inv();
            ulong id = corpse.playerSteamID;
            timer.Once(0.1f, () =>
            {
                if (corpse == null || corpse.IsDestroyed || !botInventories.ContainsKey(id))
                    return;

                botInv = botInventories[id];
                if (botInv == null)
                    return;
                Profile profile = botInv.profile;

                timer.Once(profile.Death.Corpse_Duration * 60, () => { if (corpse != null && !corpse.IsDestroyed) corpse?.Kill(); });

                if (!(profile.Death.Allow_Rust_Loot_Percent >= GetRand(1, 101)))
                    corpse.containers[0].Clear();
                else
                {
                    if (profile.Death.Rust_Loot_Source != "Default NPC")
                    {
                        LootContainer container = null;
                        if (profile.Death.Rust_Loot_Source == "Random")
                            container = Containers.ElementAt(random.Next(0, Containers.Count)).Value;
                        else
                            Containers.TryGetValue(profile.Death.Rust_Loot_Source, out container);
                        corpse.containers[0].Clear();

                        //// Needs AL private void PopulateLootAPI(ItemContainer container, string name) or private List<Item>GetLoot(string name)
                        //if (AlphaLoot && profile.Death.Use_AlphaLoot) 
                        //    AlphaLoot.Call("PopulateLoot", profile.Death.Rust_Loot_Source == "ScarecrowNPC" ? "scarecrow" : container.ShortPrefabName, corpse.containers[0]);
                        //else
                        PopulateLoot(container, corpse.containers[0]);
                    }
                    foreach (var item in corpse.containers[0].itemList.ToList())
                        if ((configData.Global.Remove_KeyCard && item.info.shortname.Contains("keycard")) || (configData.Global.Remove_Frankenstein_Parts && item.info.shortname.Contains("frankensteins.")))
                            item.Remove();
                }

                Item playerSkull = ItemManager.CreateByName("skull.human", 1);
                playerSkull.name = string.Concat($"Skull of {botInv.name}");
                ItemAmount SkullInfo = new ItemAmount() { itemDef = playerSkull.info, amount = 1, startAmount = 1 };
                var dispenser = corpse.resourceDispenser;
                if (dispenser != null)
                {
                    dispenser.containedItems.Add(SkullInfo);
                    dispenser.Initialize();
                }

                for (int i = 0; i < botInv.inventory.Length; i++)
                {
                    if (i == 1)
                        continue;
                    foreach (var item in botInv.inventory[i])
                    {
                        var giveItem = ItemManager.CreateByItemID(item.ID, item.amount, item.skinID);
                        if (!giveItem.MoveToContainer(corpse.containers[i], -1, true))
                            giveItem.Remove();
                    }
                }
                timer.Once(5f, () => botInventories?.Remove(id));
                timer.Once(1f, () =>
                {
                    if (profile != null) corpse?.ResetRemovalTime(profile.Death.Corpse_Duration * 60);
                    if (corpse != null)
                        foreach (var container in corpse.containers)
                            container.canAcceptItem = new Func<Item, int, bool>(CanAcceptItem);
                });

                if (profile.Death.Wipe_Belt_Percent >= GetRand(1, 101))
                    corpse.containers[2].Clear();
                if (profile.Death.Wipe_Clothing_Percent >= GetRand(1, 101))
                    corpse.containers[1].Clear();
                ItemManager.DoRemoves();

                corpse.containers = new ItemContainer[] { corpse.containers[0], new ItemContainer(), new ItemContainer(), corpse.containers[1], corpse.containers[2] };

                foreach (var item in corpse.containers[3].itemList.ToList())
                {
                    if (item.info.shortname.Contains("frankensteins."))
                    {
                        corpse.containers[0].capacity += 1;
                        item.MoveToContainer(corpse.containers[0], corpse.containers[0].capacity - 1);
                    }
                }

                corpse._playerName = botInv.name;
                corpse.lootPanelName = botInv.name;
            });
        }
        #endregion

        public bool CanAcceptItem(Item item, int i) => false;

        #region WeaponSwitching
        void SelectWeapon(ScientistNPC npc, BotData bData, BaseAIBrain brain)
        {
            if (npc == null)
            {
                TidyUp(npc, bData, $"Selectweapon - NPC is null - {bData.profilename}");
                return;
            }

            if (bData == null || bData.throwing || bData.healing || brain?.Senses == null || npc?.inventory?.containerBelt == null)
                return;

            if (bData.CurrentTarget == null || !bData.Targets.ContainsKey(bData.CurrentTarget) || !brain.Senses.Memory.IsLOS(bData.CurrentTarget))
                bData.GetNearest();

            if (bData.CurrentTarget == null && bData.gc != null)
            {
                UpdateActiveItem(npc, bData.gc, bData);
                return;
            }

            Range enemyrange = bData.CurrentTarget == null ? TargetRange(IsNight() ? 1 : 41) : TargetRange(Vector3.Distance(npc.transform.position, bData.CurrentTarget.transform.position));
            Range bestrange = BestRange(bData, enemyrange);
            bData.canFire = !(configData.Global.Limit_ShortRange_Weapon_Use && bestrange == Range.Close && enemyrange == Range.Long);
            if (bData.profile.Behaviour.Dont_Fire_Beyond_Distance > 0 && bData.CurrentTarget != null && Vector3.Distance(npc.transform.position, bData.CurrentTarget.transform.position) > bData.profile.Behaviour.Dont_Fire_Beyond_Distance)
                bData.canFire = false;

            if (bestrange == bData.currentRange)
            {
                var held = npc.GetHeldEntity();
                if (held != bData.gc)
                {
                    SetLights(bData, held);
                    return;
                }
            }
            bData.currentRange = bestrange;
            UpdateActiveItem(npc, bData.Weaps[bestrange].GetRandom(), bData);
        }

        void SetLights(BotData bData, HeldEntity held)
        {
            if (held.ShortPrefabName.Contains("flashlight"))
            {
                held.SetLightsOn(bData.profile.Behaviour.AlwaysUseLights ? true : IsNight());
            }
            else if (held is TorchWeapon)
            { 
                bData.torch = held as TorchWeapon;
                bData.torch.SetIsOn(bData.profile.Behaviour.AlwaysUseLights ? true : IsNight());
                bData.torch.CancelInvoke(new Action(bData.torch.UseFuel));
            }
            else
            {
                bData.gun = held as BaseProjectile;
                if (bData.gun != null)
                    bData.gun.SetLightsOn(bData.profile.Behaviour.AlwaysUseLights ? true : IsNight());
            }
            if (bData.hasHeadLamp)
                HeadLampToggle(bData.npc, bData.profile.Behaviour.AlwaysUseLights ? true : IsNight());
        }

        void UpdateActiveItem(ScientistNPC npc, HeldEntity held, BotData bData)
        {
            if (held?.GetItem() == null)
                return;

            if (npc?.inventory == null || npc.IsDead() || bData == null)
            {
                npc?.CancelInvoke("SelectWeapon");
                return;
            }
            var activeItem = npc.GetHeldEntity();
            if (activeItem == held)
                return;
            npc.svActiveItemID = new ItemId();
            if (activeItem != null)
                activeItem.SetHeld(false);

            npc.svActiveItemID = held.GetItem().uid;
            npc.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            SetRange(npc, held, bData);
            if (held != null)
                held.SetHeld(true);

            npc.inventory.UpdatedVisibleHolsteredItems();

            var pr = held as BaseProjectile;
            if (pr?.primaryMagazine != null)
            {
                pr.primaryMagazine.contents = pr.primaryMagazine.capacity;
                return;
            }
            var cs = held as Chainsaw;
            if (cs != null)
            {
                if (cs.HasFlag(BaseEntity.Flags.On))
                    return;

                cs.SetEngineStatus(true);
                cs.SendNetworkUpdateImmediate(false);
            }
            var jh = held as Jackhammer;
            if (jh != null)
            {
                if (jh.HasFlag(BaseEntity.Flags.On))
                    return;

                jh.SetEngineStatus(true);
                jh.SendNetworkUpdateImmediate(false);
            }
        }

        void SetRange(ScientistNPC npc, HeldEntity held, BotData bData)
        {
            var weapon = held as AttackEntity;
            if (bData != null && weapon != null)
                weapon.effectiveRange = bData.Weaps[Range.Melee].Contains(held) ? 2 : 410;
        }

        void HeadLampToggle(ScientistNPC npc, bool NewState)
        {
            foreach (var item in npc.inventory.containerWear.itemList.Where(item => item.info.shortname.Equals("hat.miner") || item.info.shortname.Equals("hat.candle")))
            {
                if ((NewState && !item.IsOn()) || (!NewState && item.IsOn()))
                {
                    item.SwitchOnOff(NewState);
                    npc.inventory.ServerUpdate(0f);
                    break;
                }
            }
        }
        bool debug = false;
        void TidyUp(ScientistNPC npc, BotData bData, string message)
        {
            if (npc != null && !npc.IsDestroyed)
            {
                npc.inventory?.containerBelt.Clear();
                ItemManager.DoRemoves();
                npc.Kill();
            }
            if (bData != null)
                UnityEngine.Object.DestroyImmediate(bData);
            if (debug)
            {
                PrintWarning("An npc had to be destroyed - Please notify Steenamaroo");
                PrintError(message);
            }
        }
        #endregion  

        #region SetUpLocations
        public Dictionary<string, Profile> GotMonuments = new Dictionary<string, Profile>();
        public Dictionary<string, List<SpawnerInfo>> Spawners = new Dictionary<string, List<SpawnerInfo>>();

        public class SpawnerInfo
        {
            public bool Destroy = false;
            public GameObject go;
            public Profile profile;
        }

        Profile FindNearestDefault(Vector3 location)
        {
            foreach (var entry in Profiles.Where(x => x.Value.type == ProfileType.Monument))
                if (Vector3.Distance(location, entry.Value.Other.Location) < entry.Value.Other.Block_Event_Here_Radius)
                    return entry.Value;
            return null;
        }

        void AddProfile(GameObject go, string name, Profile monument, Vector3 pos)
        {
            if (monument == null && defaultData.Monuments.ContainsKey(name))
                monument = defaultData.Monuments[name];
            else if (monument == null)
                monument = new Profile(ProfileType.Monument);

            Spawners[name] = new List<SpawnerInfo>() { new SpawnerInfo() { go = go, profile = monument } };
            Profiles[name] = monument;
            Profiles[name].Other.Location = pos;

            foreach (var custom in storedData.Profiles)
            {
                if (custom.Value.Other.Parent_Monument == name && storedData.MigrationDataDoNotEdit.ContainsKey(custom.Key))
                {
                    var path = storedData.MigrationDataDoNotEdit[custom.Key];
                    if (path.ParentMonument == new Vector3())
                    {
                        Puts($"Parent_Monument added for {custom.Key}. Removing any existing custom spawn points");
                        spawnsData.CustomSpawnLocations[custom.Key].Clear();
                        SaveSpawns();
                        path.ParentMonument = pos;
                        path.Offset = Spawners[name][0].go.transform.InverseTransformPoint(custom.Value.Other.Location);
                    }
                }
                else
                {
                    if (!storedData.MigrationDataDoNotEdit.ContainsKey(custom.Key))
                        storedData.MigrationDataDoNotEdit.Add(custom.Key, new ProfileRelocation());

                    if (custom.Value.Other.Parent_Monument == "" && storedData.MigrationDataDoNotEdit[custom.Key].ParentMonument != new Vector3())
                    {
                        Puts($"Parent_Monument removed for {custom.Key}. Removing any existing custom spawn points");
                        spawnsData.CustomSpawnLocations[custom.Key].Clear();
                        storedData.MigrationDataDoNotEdit[custom.Key] = new ProfileRelocation();
                        SaveSpawns();
                    }
                }
            }
        }

        void AddData(string name, Profile profile)
        {
            if (!storedData.MigrationDataDoNotEdit.ContainsKey(name))
                storedData.MigrationDataDoNotEdit.Add(name, new ProfileRelocation());

            var path = storedData.MigrationDataDoNotEdit[name];

            if (profile.Other.Parent_Monument != String.Empty)
            {
                if (Profiles.ContainsKey(profile.Other.Parent_Monument))
                {
                    if (path.ParentMonument != Profiles[profile.Other.Parent_Monument].Other.Location)
                    {
                        bool userChanged = false;
                        foreach (var monument in Profiles)
                            if (monument.Value.Other.Location == Profiles[profile.Other.Parent_Monument].Other.Location && monument.Key != profile.Other.Parent_Monument)
                            {
                                userChanged = true;
                                break;
                            }

                        profile.Other.Location = Spawners[profile.Other.Parent_Monument][0].go.transform.TransformPoint(path.Offset);

                        if (userChanged)
                        {
                            Puts($"Parent_Monument change detected for {name}. Removing any existing custom spawn points");
                            spawnsData.CustomSpawnLocations[name].Clear();
                            SaveSpawns();
                        }

                        path.ParentMonument = Profiles[profile.Other.Parent_Monument].Other.Location;
                        path.Offset = Spawners[profile.Other.Parent_Monument][0].go.transform.InverseTransformPoint(profile.Other.Location);
                    }
                }
                else if (profile.Spawn.AutoSpawn == true)
                    Puts($"Parent monument {profile.Other.Parent_Monument} does not exist for custom profile {name}");
            }
            else if (newsave && configData.Global.Disable_Non_Parented_Custom_Profiles_After_Wipe)
                profile.Spawn.AutoSpawn = false;

            Profiles[name] = profile;
            GameObject obj = new GameObject();
            obj.transform.position = profile.Other.Location;
            List<SpawnerInfo> parent;
            Spawners.TryGetValue(profile.Other.Parent_Monument, out parent);
            if (parent?[0]?.go)
                obj.transform.rotation = parent[0].go.transform.rotation;

            Spawners[name] = new List<SpawnerInfo>() { new SpawnerInfo() { go = obj, profile = profile, Destroy = true } };
        }

        void SetupSpawnsFile()
        {
            bool flag = false;
            foreach (var entry in Profiles.Where(entry => entry.Value.type != ProfileType.Biome && entry.Value.type != ProfileType.Event))
            {
                if (!spawnsData.CustomSpawnLocations.ContainsKey(entry.Key))
                {
                    spawnsData.CustomSpawnLocations.Add(entry.Key, new List<SpawnData>());
                    flag = true;
                }

                if (entry.Value.Spawn.AutoSpawn && entry.Value.Spawn.UseCustomSpawns && spawnsData.CustomSpawnLocations[entry.Key].Count == 0)
                    PrintWarning(lang.GetMessage("nospawns", this), entry.Key);
            }
            if (flag)
                SaveSpawns();
        }
        #endregion

        #region SpawnGroups
        void DestroySpawnGroups(GameObject gameObject = null)
        {
            foreach (var entry in Spawners.ToList())
            {
                if (entry.Value == null)
                    continue;

                foreach (var go in Spawners[entry.Key].ToList())
                {
                    if (gameObject == null || go?.go == gameObject)
                    {
                        if (go?.go != null)
                        {
                            var sg = go.go.GetComponent<CustomGroup>();

                            if (sg == null)
                                continue;

                            SpawnHandler.Instance.SpawnGroups.Remove(sg);
                            UnityEngine.Object.Destroy(sg);

                            if (go.Destroy)
                                UnityEngine.Object.Destroy(go.go);
                        }
                    }
                }
            }
        }

        void RemoveTemp(GameObject gameObject = null)
        {
            foreach (var entry in Spawners.ToList())
            {
                if (entry.Value == null)
                    continue;

                foreach (var go in entry.Value)
                {
                    if (gameObject == null || go?.go == gameObject)
                    {
                        if (go?.go != null)
                        {
                            var sg = go.go.GetComponent<CustomGroup>();

                            if (sg == null)
                                continue;

                            SpawnHandler.Instance.SpawnGroups.Remove(sg);
                        }
                    }
                }
            }
        }

        void CreateSpawnGroups(string single = "")
        {
            int delay = 0;
            foreach (var entry in Profiles)
            {
                if (entry.Value.Other.Parent_Monument != string.Empty && !Spawners.ContainsKey(entry.Value.Other.Parent_Monument))
                    continue;

                if (single == string.Empty || entry.Key == single)
                {
                    if (IsSpawner(entry.Key) && entry.Value.Spawn.AutoSpawn && Mathf.Max(configData.Global.DayStartHour, configData.Global.NightStartHour) > 0)
                    {
                        delay++;
                        timer.Once(delay, () =>
                        {
                            if (!bs.Profiles.ContainsKey(entry.Key))
                                return;

                            foreach (var go in Spawners[entry.Key])
                                SetUpSpawnGroup(go.go, go.profile, entry.Key, false, 0, String.Empty);
                        });
                    }
                }
            }
        }

        void CreateTempSpawnGroup(Vector3 pos, string name, Profile profile, string group, int quantity)
        {
            GameObject gameObject = new GameObject();
            gameObject.transform.position = pos;
            if (!Spawners.ContainsKey(name))
                Spawners.Add(name, new List<SpawnerInfo>());
            Spawners[name].Add(new SpawnerInfo() { go = gameObject, profile = profile, Destroy = true });
            SetUpSpawnGroup(gameObject, profile, name, true, quantity, group);
        }

        public class SpawnInfo
        {
            public SpawnInfo(SpawnData d)
            {
                if (d == null)
                    return;
                Stationary = d.Stationary;
                Kits = d.Kits.ToArray();
                Health = d.Health;
                RoamRange = d.RoamRange;
                UseOverrides = d.UseOverrides;
            }
            public Vector3 loc;
            public float rot = 0;
            public bool Stationary;
            public string[] Kits = null;
            public int Health = 150;
            public int RoamRange = 100;
            public bool UseOverrides = false;
        }

        public void SetUpSpawnGroup(GameObject gameObject, Profile profile, string name, bool t, int q, string g)
        {
            int skipped = 0;
            List<SpawnInfo> Points = new List<SpawnInfo>();
            var maxPopulation = t ? q : Mathf.Max(profile.Spawn.Day_Time_Spawn_Amount, profile.Spawn.Night_Time_Spawn_Amount);
            var comp = gameObject.AddComponent<CustomGroup>();
            if (profile.Other.Use_Map_Marker)
            {
                comp.marker = (MapMarkerGenericRadius)GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", gameObject.transform.position);
                comp.marker.enableSaving = false;
                comp.marker.color1 = Color.white;
                comp.marker.color2 = Color.black;
                comp.marker.alpha = 0.9f;
                comp.marker.radius = 0.2f;
                comp.marker.Spawn();
                comp.marker.SendUpdate();
            }

            comp.maxPopulation = 0;

            if (!t && profile.type == ProfileType.Biome)
            {
                if (BiomeSpawns[name].Count < maxPopulation)
                {
                    PrintWarning($"Found {BiomeSpawns[name].Count} out of {maxPopulation} spawnpoints for {name}.");
                    if (BiomeSpawns[name].Count > 0)
                        Puts("Adust desired population and reload profile.");
                }
                else
                {
                    while (Points.Count < maxPopulation)
                        Points.Add(new SpawnInfo(null) { loc = BiomeSpawns[name].GetRandom(), rot = 0 });

                    FinaliseSpawnGroup(comp, gameObject, profile, name, Points, t, q, g);
                }
            }
            else
            {
                if (!t && profile.Spawn.UseCustomSpawns)
                {
                    var SpawnsData = bs.spawnsData.CustomSpawnLocations[name];

                    foreach (var entry in SpawnsData)
                    {
                        var loc = entry.loc;
                        var l = bs.Spawners.ContainsKey(profile.Other.Parent_Monument) ? bs.Spawners[profile.Other.Parent_Monument]?[0]?.go.transform : bs.Spawners.ContainsKey(name) ? bs.Spawners[name]?[0]?.go.transform : null;
                        if (l != null)
                            loc = l.transform.TransformPoint(loc);

                        if (((entry.UseOverrides && !entry.Stationary) || (!entry.UseOverrides && !profile.Spawn.Stationary)) && !ValidPoint(loc))
                        {
                            skipped++;
                            continue;
                        }

                        Points.Add(new SpawnInfo(entry.Kits == null ? null : entry) { loc = loc, rot = entry.rot });
                    }
                    if (skipped > 0)
                        PrintWarning($"Skipped {skipped}/{SpawnsData.Count()} custom {(skipped == 1 ? "spawnpoint" : "spawnpoints")} for profile {name} as stationary is false and there's no navmesh.");
                }

                if (Points.Count < maxPopulation)
                {
                    int safety = 0;
                    while (Points.Count() < maxPopulation)
                    {
                        safety++;
                        if (safety > maxPopulation * 2)
                        {
                            if (profile.type != ProfileType.Event)
                                bs.Puts($"Failed to get enough spawnpoints for profile {name}.");
                            return;
                        }
                        Vector3 loc = TryGetSpawn(gameObject.transform.position, profile.Spawn.Radius, profile.type == ProfileType.Event, profile.Other.Off_Terrain);
                        if (loc != Vector3.zero)
                            Points.Add(new SpawnInfo(null) { loc = loc, rot = 0f });
                    }
                }
            }

            gameObject.transform.position = profile.Other.Location;
            if (profile.type != ProfileType.Biome && Points.Count >= maxPopulation)
                FinaliseSpawnGroup(comp, gameObject, profile, name, Points, t, q, g);
        }

        void FinaliseSpawnGroup(CustomGroup comp, GameObject gameObject, Profile profile, string name, List<SpawnInfo> Points, bool t, int q, string g)
        {
            bool canChute = profile.Other.Chute;
            int counter = 0;
            List<GameObject> sps = new List<GameObject>();
            List<BaseSpawnPoint> bsps = new List<BaseSpawnPoint>();

            foreach (var entry in Points)
            {
                var NewGO = new GameObject();                               //  Make this a property of NewSpawnPoint
                sps.Add(NewGO);                                             //  Get rid of this collection - Only used for destruction
                NewGO.transform.SetParent(gameObject.transform);
                NewSpawnPoint point = NewGO.AddComponent<NewSpawnPoint>();
                bsps.Add(point);

                var SO = NewGO.AddComponent<SpawnOverride>();               //  Make this a property of NewSpawnPoint
                SO.Point = entry;
                SO.Initialize();

                point.location = entry.loc;
                point.rotation = Quaternion.Euler(0, entry.rot, 0);
                point.dropToGround = false;

                if (canChute)
                    canChute = profile.Other.Chute && point.location.y + 1f >= TerrainMeta.HeightMap.GetHeight(point.location) && WaterLevel.GetWaterDepth(point.location, true, false) <= 0;
                counter++;
            }
            //  comp.spawnPoints = bsps.ToArray();
            //  Get rid of setup method and just put code here - Only called from this location.
            comp.Setup(bsps, profile, name, t, q, g, sps, canChute);
        }

        public class SpawnOverride : MonoBehaviour                          //  Lose mono inhertance
        {
            public SpawnInfo Point;

            public void Initialize()
            {
                Stationary = Point.Stationary;
                RoamRange = Point.RoamRange;
                Kits = Point.Kits;
                Health = Point.Health;
                UseOverrides = Point.UseOverrides;
            }
            public bool Stationary;
            public int RoamRange;
            public string[] Kits;
            public int Health;
            public bool UseOverrides;
        }

        static int GetPop(Profile p) => ScalePop(IsNight() ? p.Spawn.Night_Time_Spawn_Amount : p.Spawn.Day_Time_Spawn_Amount, p);

        static public int ScalePop(int amount, Profile p)
        {
            int ServerPop = BasePlayer.activePlayerList.Count;
            if (p.Spawn.Scale_NPC_Count_To_Player_Count)
            {
                foreach (var rule in bs.configData.Global.ScaleRules)
                    if (ServerPop >= rule.Value)
                        return amount * rule.Key / 100;
                return 0;
            }
            return amount;
        }

        public class CustomGroup : SpawnGroup
        {
            bool first = true;
            bool ableToChute;
            public MapMarkerGenericRadius marker;
            bool night = IsNight();
            int pop = 0;
            void CheckNight()
            {
                if (temporary)
                    return;

                if (marker != null && currentPopulation == 0 && !profile.Other.Always_Show_Map_Marker)
                {
                    marker.alpha = 0f;
                    marker.SendUpdate();
                }

                if (profile.Spawn.Day_Time_Spawn_Amount != profile.Spawn.Night_Time_Spawn_Amount && night != IsNight())
                {
                    maxPopulation = GetPop(profile);
                    night = IsNight();
                    if (night && profile.Spawn.Night_Time_Spawn_Amount > profile.Spawn.Day_Time_Spawn_Amount)
                        Spawn(profile.Spawn.Night_Time_Spawn_Amount - profile.Spawn.Day_Time_Spawn_Amount);
                    else if (!night && profile.Spawn.Day_Time_Spawn_Amount > profile.Spawn.Night_Time_Spawn_Amount)
                        Spawn(profile.Spawn.Day_Time_Spawn_Amount - profile.Spawn.Night_Time_Spawn_Amount);
                }
                else
                {
                    pop = GetPop(profile);

                    if (pop > maxPopulation)
                        Spawn(pop - maxPopulation);

                    maxPopulation = pop;
                }
            }

            List<GameObject> SpawnPoints = new List<GameObject>();
            public Profile profile;
            public string profilename = string.Empty, group = string.Empty;

            public bool ready = false;

            public void Setup(List<BaseSpawnPoint> bsps, Profile p, string n, bool t, int q, string g, List<GameObject> sps, bool canChute)
            {
                ableToChute = canChute;
                SpawnPoints = sps;
                spawnPoints = bsps.ToArray();
                name = temporary ? gameObject.GetInstanceID().ToString() : name;
                group = g;
                temporary = t;
                profile = p;
                profilename = n;
                maxPopulation = t ? q : GetPop(p);

                if (!t)
                    InvokeRepeating("CheckNight", random.Next(10, 20), random.Next(10, 20));

                numToSpawnPerTickMax = 0;
                numToSpawnPerTickMin = 0;
                respawnDelayMax = float.MaxValue;
                respawnDelayMin = float.MaxValue;
                wantsInitialSpawn = true;

                prefabs = new List<SpawnEntry>();
                prefabs.Add(new SpawnEntry() { prefab = new GameObjectRef { guid = "adb1626eb0a3ab747aa5345479befccf" } });

                enabled = true;
                gameObject.SetActive(true);
                Fill();
            }

            Vector3 ProcessPoint(Vector3 pos, bool chute, bool airdrop, bool stationary)
            {
                //check for new obstacles 
                if (!stationary || chute)
                {
                    NavMeshHit hit;
                    if (!NavMesh.SamplePosition(pos, out hit, 2, -1))
                        return Vector3.zero;
                }
                if (chute)
                {
                    pos.y = airdrop ? (pos.y = gameObject.transform.position.y - 40f) : Mathf.Min(1000, Mathf.Max(50, bs.configData.Global.Parachute_From_Height));
                    if (!airdrop)
                        pos += new Vector3(random.Next(-25, 25), 0, random.Next(-25, 25));
                }
                return pos;
            }

            public void SpawnBot(int num, NewSpawnPoint sp)
            {
                repoint = sp;
                Spawn(1);
                repoint = null;
            }

            bool update = false;
            NewSpawnPoint repoint = null;

            protected override void Spawn(int numToSpawn)
            {
                if (profile != null && !temporary)
                {
                    pop = GetPop(profile);
                    if (maxPopulation < pop)
                    {
                        update = true;
                        numToSpawn = pop - maxPopulation;
                    }
                    maxPopulation = GetPop(profile);
                }

                Vector3 vector3;
                Quaternion quaternion;
                if (!update)
                    numToSpawn = Mathf.Min(numToSpawn, maxPopulation - currentPopulation);

                update = false;

                for (int i = 0; i < numToSpawn; i++)
                {
                    GameObjectRef prefab = GetPrefab();
                    if (prefab != null && !string.IsNullOrEmpty(prefab.guid))
                    {
                        NewSpawnPoint spawnpoint = (NewSpawnPoint)GetSpawnPoint(prefab, out vector3, out quaternion);
                        if (spawnpoint)
                        {
                            var or = spawnpoint.gameObject.GetComponent<SpawnOverride>();
                            bool UseOR = or != null && or.UseOverrides;
                            spawnpoint.transform.position = vector3;
                            var point = ProcessPoint(vector3, ableToChute, profilename == "AirDrop", UseOR ? or.Stationary : profile.Spawn.Stationary);
                            if (point == Vector3.zero)
                                continue;

                            var npc = (global::HumanNPC)GameManager.server.CreateEntity(prefab.resourcePath, point, quaternion, false);
                            if (npc)
                            {
                                npc.gameObject.AwakeFromInstantiate();
                                string name = npc.gameObject.name;
                                npc.gameObject.name = "BotReSpawn";
                                bs.NextTick(() => npc.gameObject.name = name);

                                if (bs.Kits && profile?.Spawn?.Keep_Default_Loadout == false && (UseOR ? or.Kits.ToList() : profile.Spawn.Kit).Count > 0 && npc?.loadouts != null) ////  ADDED
                                    npc.loadouts = new PlayerInventoryProperties[] { };

                                npc.Spawn();

                                if (marker != null)
                                {
                                    marker.alpha = 0.9f;
                                    marker.SendUpdate();
                                }

                                PostSpawnProcess(npc, spawnpoint);
                                SpawnPointInstance ins = npc.gameObject.AddComponent<SpawnPointInstance>();
                                ins.parentSpawnPointUser = this;
                                ins.parentSpawnPoint = spawnpoint;
                                ins.Notify();

                                npc.eyes.rotation = Quaternion.Euler(0, bs.Spawners[profilename][0].go.transform.rotation.eulerAngles.y + spawnpoint.rotation.eulerAngles.y, 0);
                                npc.viewAngles = npc.eyes.rotation.eulerAngles;
                                npc.ServerRotation = npc.eyes.rotation;
                            }
                        }
                    }
                }
            }

            protected override BaseSpawnPoint GetSpawnPoint(GameObjectRef prefabRef, out Vector3 pos, out Quaternion rot)
            {
                spawnPoints = spawnPoints.Where(x => x.GetType() == typeof(NewSpawnPoint)).ToArray();
                BaseSpawnPoint baseSpawnPoint = null;
                pos = Vector3.zero;
                rot = Quaternion.identity;
                int num = UnityEngine.Random.Range(0, (int)spawnPoints.Length);
                int num1 = 0;
                if (repoint != null && repoint.IsAvailableTo(prefabRef.Get()))
                    baseSpawnPoint = repoint;
                else
                {
                    if (profile.type == ProfileType.Biome)
                    {
                        var available = spawnPoints.Where(x => x != null && x.IsAvailableTo(prefabRef.Get())).ToList();
                        baseSpawnPoint = available.GetRandom();
                    }
                    else
                    {
                        while (num1 < (int)spawnPoints.Length)
                        {
                            BaseSpawnPoint baseSpawnPoint1 = this.spawnPoints[(num + num1) % (int)spawnPoints.Length];
                            if (baseSpawnPoint1 == null || !baseSpawnPoint1.IsAvailableTo(prefabRef.Get()))
                                num1++;
                            else
                            {
                                baseSpawnPoint = baseSpawnPoint1;
                                break;
                            }
                        }
                    }
                }
                if (baseSpawnPoint)
                    baseSpawnPoint.GetLocation(out pos, out rot);

                return baseSpawnPoint;
            }

            protected override void PostSpawnProcess(BaseEntity entity, BaseSpawnPoint spawnPoint)
            {
                if (entity == null || spawnPoint == null)
                    return;

                base.PostSpawnProcess(entity, spawnPoint);

                var npc = entity as ScientistNPC;
                if (npc == null)
                    return;
                var nav = npc.GetComponent<BaseNavigator>();
                if (nav == null)
                    return;

                npc.NavAgent.enabled = false;
                nav.CanUseNavMesh = false;
                nav.DefaultArea = "Walkable";
                npc.NavAgent.areaMask = 1;
                npc.NavAgent.agentTypeID = -1372625422;
                npc.NavAgent.autoTraverseOffMeshLink = true;
                npc.NavAgent.autoRepath = true;
                nav.CanUseCustomNav = true;
                npc.NavAgent.baseOffset = -0.1f;

                var bData = npc.gameObject.AddComponent<BotData>();
                bData.invincible = true;
                bData.npc = npc;
                bData.abletoChute = ableToChute;
                bData.temporary = temporary;
                bData.profile = profile;
                bData.profilename = profilename;

                if (npc?.Brain?.Senses != null)
                    npc.Brain.Senses.nextKnownPlayersLOSUpdateTime = Time.time * Time.time;

                if (!bs.NPCPlayers.ContainsKey(npc.userID) && !bs.DeadNPCPlayerIds.ContainsKey(npc.userID))
                    bs.NPCPlayers.Add(npc.userID, bData);
                else
                {
                    bs.timer.Once(0.1f, () =>
                    {
                        npc?.inventory?.containerBelt?.Clear();
                        ItemManager.DoRemoves();
                        npc.userID = 1;
                        npc?.Kill();
                    });
                    return;
                }

                bs.timer.Once(1.0f, () =>
                {
                    if (npc == null || nav == null || bData?.profile == null)
                    {
                        string message = "";
                        if (bs.debug)
                        {
                            if (npc == null) message += "npc ";
                            if (nav == null) message += "nav ";
                            if (bData?.profile == null) message += "bData.profile";
                            message += $" - {bData?.profilename}";
                        }
                        bs.TidyUp(npc, bData, message);
                        return;
                    }

                    bs.no_of_AI++;
                    npc.EnablePlayerCollider();

                    if (npc.Brain == null)
                        npc.Brain = npc.GetComponent<ScientistBrain>();

                    var or = spawnPoint?.gameObject?.GetComponent<SpawnOverride>();

                    if (or == null || npc?.Brain?.Senses == null)
                    {
                        string message = "";
                        if (bs.debug)
                        {
                            if (or == null) message += "OR ";
                            if (npc == null) message += "npc ";
                            if (npc?.Brain == null) message += "npc?.Brain ";
                            if (npc?.Brain?.Senses == null) message += "np?c.Brain?.Senses ";
                            message += $"{bData?.profilename}";
                        }
                        bs.TidyUp(npc, bData, message);
                        return;
                    }

                    npc.Brain.UseAIDesign = false;
                    bool UseOR = or != null && or.UseOverrides;
                    bData.sp = (NewSpawnPoint)spawnPoint;
                    npc.Brain.Senses.nextUpdateTime = float.MaxValue;
                    npc.Brain.Senses.nextKnownPlayersLOSUpdateTime = float.MaxValue;
                    bData.profilename = profilename;
                    bData.stationary = UseOR ? or.Stationary : profile.Spawn.Stationary;
                    bData.group = group;
                    bData.sg = this;
                    npc.Brain.AllowedToSleep = false;
                    npc.Brain.sleeping = false;

                    if (npc.Brain.Events == null)
                    {
                        string message = "";
                        if (bs.debug)
                        { 
                            if (npc.Brain.Events == null) message += "npc.Brain.Events ";
                            message += $"{bData?.profilename}";
                        }
                        bs.TidyUp(npc, bData, message);
                        return;
                    }

                    npc.enableSaving = false;
                    if ((!temporary || first) && profile.Spawn.Announce_Spawn && profile.Spawn.Announcement_Text != String.Empty)
                    {
                        bs.PrintToChat(profile.Spawn.Announcement_Text);
                        first = false;
                    }

                    if (temporary && currentPopulation == maxPopulation)
                        bs.RemoveTemp(gameObject);

                    if (!bData.stationary && !ableToChute)
                    {
                        nav.CanUseNavMesh = true;
                        npc.NavAgent.enabled = true;
                    }

                    nav.Init(npc, npc.NavAgent);
                    npc.Brain.HostileTargetsOnly = bData.profile.Behaviour.Peace_Keeper;
                    npc.Brain.IgnoreSafeZonePlayers = true;
                    npc.Brain.SenseRange = 400;
                    npc.Brain.Senses.Init(npc, npc.Brain, bs.configData.Global.Deaggro_Memory_Duration, 400, 400, npc.Brain.VisionCone, npc.Brain.CheckVisionCone, npc.Brain.CheckLOS, npc.Brain.IgnoreNonVisionSneakers, npc.Brain.ListenRange, bData.profile.Behaviour.Peace_Keeper, npc.Brain.MaxGroupSize > 0, npc.Brain.IgnoreSafeZonePlayers, npc.Brain.SenseTypes, true);

                    npc._maxHealth = UseOR ? or.Health : bData.profile.Spawn.BotHealth;
                    npc.startHealth = npc._maxHealth;
                    npc.InitializeHealth(npc._maxHealth, npc._maxHealth);

                    if (ableToChute)
                        bs.AddChute(npc, bData, bData.sp.transform.position);

                    List<string> kits = UseOR ? or.Kits.ToList() : profile.Spawn.Kit;

                    if (HasFStein(profile))
                        npc.inventory.containerWear.Clear();

                    if (kits.Count > 0 && kits.Count() == profile.Spawn.BotNames.Count())
                    {
                        string kit = kits.GetRandom();
                        bs.GiveKit(npc, kit);
                        bData.kit = kit;
                        bs.SetName(profile, npc, profile.Spawn.Kit.IndexOf(kit));
                    }
                    else
                    {
                        bData.kit = kits.GetRandom();
                        bs.GiveKit(npc, bData.kit);
                        bs.SetName(profile, npc, -1);
                    }

                    bs.GiveFStein(npc, profile);
                    bs.SortWeapons(npc, bData);

                    SetupStates(npc.Brain, bData);

                    if (temporary)
                        bs.RunSuicide(npc, random.Next(profile.Other.Suicide_Timer * 60, (profile.Other.Suicide_Timer * 60) + 10));

                    if (bData.profile.Other.Disable_Radio == true)
                    {
                        npc.radioChatterType = ScientistNPC.RadioChatterType.NONE;
                        npc.DeathEffects = new GameObjectRef[0];
                        npc.RadioChatterEffects = new GameObjectRef[0];
                    }
                    bData.invincible = false;
                    Interface.CallHook("OnBotReSpawnNPCSpawned", npc, profilename, group);
                });
            }

            bool HasFStein(Profile p) => p.Spawn.FrankenStein_Head != FStein.None || p.Spawn.FrankenStein_Torso != FStein.None || p.Spawn.FrankenStein_Legs != FStein.None;

            private void OnDestroy()
            {
                CancelInvoke("CheckNight");

                for (int i = spawnInstances.Count - 1; i >= 0; i--)
                {
                    SpawnPointInstance item = spawnInstances[i];
                    if (item == null || item.gameObject == null)
                        continue;
                    BaseEntity baseEntity = item.gameObject.ToBaseEntity();
                    if (baseEntity?.transform?.position == null)// || setFreeIfMovedBeyond != null && !setFreeIfMovedBeyond.bounds.Contains(baseEntity.transform.position))
                        item?.Retire();
                    else if (baseEntity)
                        baseEntity.Kill(BaseNetworkable.DestroyMode.None);
                }
                spawnInstances.Clear();

                if (marker != null)
                    marker.Kill();
                foreach (var sp in SpawnPoints)
                    if (sp != null)
                        Destroy(sp);
            }
        }

        public class NewSpawnPoint : GenericSpawnPoint
        {
            public Vector3 location = new Vector3();
            public Quaternion rotation = new Quaternion();
            public override void ObjectSpawned(SpawnPointInstance instance)
            {
                OnObjectSpawnedEvent.Invoke();
                gameObject.SetActive(false);
            }

            public override void GetLocation(out Vector3 pos, out Quaternion rot)
            {
                pos = location;
                rot = rotation;
            }
        }
        #endregion

        #region BrainStates
        static void SetupStates(BaseAIBrain brain, BotData bData)
        {
            brain.Navigator.MaxRoamDistanceFromHome = bData.profile.Behaviour.Roam_Range;
            brain.Navigator.BestRoamPointMaxDistance = bData.profile.Behaviour.Roam_Range;
            brain.Navigator.BestMovementPointMaxDistance = bData.profile.Behaviour.Roam_Range;
            brain.Navigator.FastSpeedFraction = bData.profile.Behaviour.Running_Speed_Booster / 10f;
            brain.Events.Memory.Position.Set(bData.sp.transform.position, 4);
            brain.MemoryDuration = bs.configData.Global.Deaggro_Memory_Duration;

            if (brain != null && bData != null)
            {
                ClearBrain(brain);
                bs.timer.Once(5f, () => { if (brain != null) ClearBrain(brain); });
            }
        }

        static void ClearBrain(BaseAIBrain brain)
        {
            for (int i = 0; i < brain.Events.events.Count(); i++)
                if (brain.Events.events[i].EventType == AIEventType.AttackTick || brain.Events.events[i].EventType == AIEventType.BestTargetDetected)
                {
                    brain.Events.events.RemoveAt(i);
                    brain.Events.Memory.Entity.Set(null, 0);
                    i--;
                }
        }
        #endregion

        #region PosHelpers
        NavMeshHit navMeshHit;
        public bool ValidPoint(Vector3 pos) => NavMesh.SamplePosition(pos, out navMeshHit, 2, 1) && WaterLevel.GetWaterDepth(pos, true, false) <= 0;

        public static Vector3 CalculateGroundPos(Vector3 pos, bool e, bool Off_Terrain)
        {
            if (!Off_Terrain && (!e || TerrainMeta.HeightMap.GetHeight(pos) <= pos.y))
                pos.y = TerrainMeta.HeightMap.GetHeight(pos);

            NavMeshHit navMeshHit;
            if (!NavMesh.SamplePosition(pos, out navMeshHit, 2, 1) || WaterLevel.GetWaterDepth(pos, true, false) > 0 || ((!e || TerrainMeta.HeightMap.GetHeight(pos) - 100 <= pos.y) && Physics.RaycastAll(navMeshHit.position + new Vector3(0, 100, 0), Vector3.down, 99f, 1235288065).Any()))
                pos = Vector3.zero;
            else
                pos = navMeshHit.position;
            return pos;
        }

        Vector3 TryGetSpawn(Vector3 pos, int radius, bool e, bool Off_Terrain)
        {
            int attempts = 0;
            var spawnPoint = Vector3.zero;
            Vector2 rand;

            while (attempts < 50 && spawnPoint == Vector3.zero)
            {
                attempts++;
                rand = UnityEngine.Random.insideUnitCircle * radius;
                spawnPoint = CalculateGroundPos(pos + new Vector3(rand.x, 0, rand.y), e, Off_Terrain);
                if (spawnPoint != Vector3.zero)
                    return spawnPoint;
            }
            return spawnPoint;
        }
        #endregion

        #region BotSetup

        object OnEntityTakeDamage(Parachute chute) => chute?.gameObject?.name == "BotReSpawn" ? true : (object)null; 

        string aiheader = "CAEIAggDCAUIEggECAYIEwgUCBUIFggNEj0IABABGhkIABACGAAgACgAMACiBgoNAAAAABUAAIA"; 
        FieldInfo MountTime = typeof(Parachute).GetField("mountTime", (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

        object CanDismountEntity(ScientistNPC npc) => NPCPlayers.ContainsKey(npc.userID) ? true : (object)null;

        void AddChute(ScientistNPC npc, BotData bData, Vector3 loc)
        {
            float fall = (random.Next(60, Mathf.Min(100, configData.Global.Chute_Speed_Variation) + 60) / 20f) * configData.Global.Chute_Fall_Speed / 50f;

            Parachute chute = GameManager.server.CreateEntity(Parachute, npc.transform.position, new Quaternion(), true) as Parachute;
            if (chute != null)
            {
                chute.UprightLerpForce = 0f;
                chute.ConstantForwardForce = 0f;
                chute.TargetDrag = 0f;
                chute.TargetAngularDrag = 0f;
                chute.enableSaving = false;
                chute.Spawn();
                chute.SetHealth(chute.MaxHealth());

                foreach (var comp in chute.GetComponentsInChildren<Collider>().ToList())
                    UnityEngine.Object.DestroyImmediate(comp);

                UnityEngine.Object.DestroyImmediate(chute.GetComponent<EntityCollisionMessage>());
                chute.AttemptMount(npc, true); 

                foreach (var comp in chute.GetComponentsInChildren<Rigidbody>())
                {
                    comp.isKinematic = false; 
                    comp.useGravity = false;  
                    comp.drag = 0f;
                    comp.gameObject.layer = 0;
                }   
            }

            if (chute != null && npc?.Brain?.Navigator != null && bData != null) 
            {
                bData.chute = chute; 
                BaseMountable.AllMountables.Remove(chute);
                chute.rigidBody.rotation = Quaternion.LookRotation((new Vector3(loc.x, npc.transform.position.y, loc.z) - (npc.transform.position)).normalized);
                chute.rigidBody.velocity = (loc - npc.transform.position).normalized * fall;
                chute.gameObject.name = "BotReSpawn";

                if (bData.profile.Other.SamSite_Safe_Whilst_Chuting)
                    MountTime.SetValue(chute, (TimeSince)(-1000));  
            }

            var npcrb = npc.playerRigidbody;
            npcrb.isKinematic = false;
            npcrb.useGravity = false;
            npcrb.drag = 0f;
            npc.gameObject.layer = 0;
            npcrb.velocity = (loc - npc.transform.position).normalized * fall;
            npc.playerCollider.radius = 2.5f;
            npc.playerCollider.isTrigger = true;
        }

        void SetName(Profile p, global::HumanNPC npc, int num)
        {
            if (p.Spawn.BotNames.Count == 0)
            {
                npc.displayName = Get(npc.userID);
                npc.displayName = char.ToUpper(npc.displayName[0]) + npc.displayName.Substring(1);
            }
            else
            {
                if (num != -1 && p.Spawn.BotNames.Count > num)
                    npc.displayName = p.Spawn.BotNames[num];
                else
                    npc.displayName = p.Spawn.BotNames.GetRandom();
            }

            if (p.Spawn.BotNamePrefix != String.Empty)
                npc.displayName = p.Spawn.BotNamePrefix + " " + npc.displayName;
        }

        void GiveKit(ScientistNPC npc, string kit)
        {
            if (npc?.inventory?.containerBelt == null)
                return;

            BotData bData;
            NPCPlayers.TryGetValue(npc.userID, out bData);
            if (bData != null && !String.IsNullOrEmpty(kit))
            {
                if (bData.profile.Spawn.Keep_Default_Loadout == false) //// No longer needed
                    npc?.inventory?.Strip();

                Kits?.Call($"GiveKit", npc, kit, true);

                NextTick(() =>
                {
                    if (bData?.profile != null && npc?.inventory?.containerMain?.itemList != null)
                        if (bData.profile.Death.Wipe_Main_Percent >= GetRand(1, 101))
                        {
                            npc.inventory.containerMain.Clear();
                            ItemManager.DoRemoves();
                        }
                });
            }
        }

        void GiveFStein(ScientistNPC npc, Profile profile)
        {
            if (profile.Spawn.FrankenStein_Head != FStein.None)
            {
                npc.inventory.containerWear.capacity += 1;
                Item newItem = ItemManager.CreateByName($"frankensteins.monster.0{Convert.ToInt16(profile.Spawn.FrankenStein_Head)}.head", 1);
                if (newItem != null)
                {
                    SetOccupation(newItem, false, 0, 0);
                    if (!newItem.MoveToContainer(npc.inventory.containerWear))
                        newItem.Remove(0f);
                }
            }
            if (profile.Spawn.FrankenStein_Torso != FStein.None)
            {
                Item newItem = ItemManager.CreateByName($"frankensteins.monster.0{Convert.ToInt16(profile.Spawn.FrankenStein_Torso)}.torso", 1);
                if (newItem != null)
                {
                    SetOccupation(newItem, true, 504, 0);
                    if (!newItem.MoveToContainer(npc.inventory.containerWear))
                        newItem.Remove(0f);
                }
            }
            if (profile.Spawn.FrankenStein_Legs != FStein.None)
            {
                Item newItem = ItemManager.CreateByName($"frankensteins.monster.0{Convert.ToInt16(profile.Spawn.FrankenStein_Legs)}.legs", 1);
                if (newItem != null)
                {
                    SetOccupation(newItem, true, 129024, 0);
                    if (!newItem.MoveToContainer(npc.inventory.containerWear))
                        newItem.Remove(0f);
                }
            }
        }

        void SetOccupation(Item item, bool set, int under, int over)
        {
            var comp = item.info.GetComponent<ItemModWearable>();
            if (comp != null)
            {
                comp.protectionProperties = null;
                if (set)
                {
                    comp.targetWearable.occupationUnder = (Wearable.OccupationSlots)under;
                    comp.targetWearable.occupationOver = (Wearable.OccupationSlots)over;
                }
            }
        }

        void SortWeapons(ScientistNPC npc, BotData bData)
        {
            foreach (var attire in npc?.inventory?.containerWear?.itemList.Where(attire => attire?.info?.shortname != null && (attire.info.shortname.Equals("hat.miner") || attire.info.shortname.Equals("hat.candle"))))
            {
                if (attire?.contents == null)
                    continue;
                bData.hasHeadLamp = true;
                Item newItem = ItemManager.Create(fuel, 1);
                attire.contents.Clear();
                ItemManager.DoRemoves();
                if (!newItem.MoveToContainer(attire.contents))
                    newItem.Remove();
                else
                {
                    npc.SendNetworkUpdateImmediate();
                    npc.inventory.ServerUpdate(0f);
                }
            }

            for (int i = 0; i < 4; i++)
                bData.Weaps.Add((Range)i, new List<HeldEntity>());

            bool flag = false;
            if (npc?.inventory?.containerBelt?.itemList != null)
                foreach (Item item in npc.inventory.containerBelt.itemList)
                {
                    var h = item.GetHeldEntity();
                    if (h == null)
                        continue;
                    var held = h as HeldEntity;
                    if (!Isweapon(string.Empty, item))
                    {
                        MedicalTool med = held as MedicalTool;
                        if (med != null)
                            bData.meds.Add(med);
                        else if (held as GeigerCounter != null)
                            bData.gc = held as GeigerCounter;
                        else
                            item.Remove();
                        continue;
                    }

                    if (held is ThrownWeapon)
                    {
                        bData.throwables.Add(held);
                        continue;
                    }

                    //var lw = held as LiquidWeapon;
                    //if (lw != null)
                    //{
                    //    lw.AutoPump = true;
                    //    lw.RequiresPumping = false;
                    //}

                    var gun = held as BaseProjectile;
                    if (held as BaseMelee != null || held as TorchWeapon != null)
                        bData.Weaps[Range.Melee].Add(held);
                    else if (held as FlameThrower)
                    {
                        NextTick(() =>
                        {
                            if (bData?.Weaps == null || held == null)
                                return;
                            if (bData.Weaps[Range.Melee].Count == 0)
                                bData.Weaps[Range.Close].Add(held);

                            SetMelee(bData);
                        });
                    }
                    else if (gun != null)
                    {
                        if (gun.ShortPrefabName == "smg.entity" || gun.ShortPrefabName == "thompson.entity")
                        {
                            gun.attackLengthMin = 0.1f;
                            gun.attackLengthMax = 0.5f;
                        }
                        gun.primaryMagazine.contents = gun.primaryMagazine.capacity;
                        if (held.ShortPrefabName.Contains("pistol") || held.ShortPrefabName.Contains("shotgun") || held.ShortPrefabName.Contains("spas12") || held.ShortPrefabName.Contains("bow"))
                            bData.Weaps[Range.Close].Add(held);
                        else if (held.name.Contains("bolt") || held.name.Contains("l96"))
                            bData.Weaps[Range.Long].Add(held);
                        else bData.Weaps[Range.Mid].Add(held);
                    }
                    else
                        bData.Weaps[Range.Mid].Add(held);
                    flag = true;
                }

            SetMelee(bData);
            if (bData.melee && bData.profile.Other.MurdererSound)
            {
                Timer huffTimer = timer.Once(1f, () => { });
                huffTimer = timer.Repeat(8f, 0, () =>
                {
                    if (npc != null)
                    {
                        if (bData?.CurrentTarget != null)
                            Effect.server.Run(huff, npc, StringPool.Get("head"), Vector3.zero, Vector3.zero, null, false);
                    }
                    else
                        huffTimer?.Destroy();
                });
            }

            if (!flag)
            {
                PrintWarning(lang.GetMessage("noWeapon", this), bData.profilename, bData.kit);
                bData.noweapon = true;
                return;
            }
            npc.CancelInvoke(npc.EquipTest);
        }

        void SetMelee(BotData bData) => bData.melee = bData.Weaps[Range.Melee].Count > 0 && bData.Weaps[Range.Close].Count == 0 && bData.Weaps[Range.Mid].Count == 0 && bData.Weaps[Range.Long].Count == 0;

        void RunSuicide(ScientistNPC npc, int suicInt)
        {
            if (!NPCPlayers.ContainsKey(npc.userID))
                return;
            timer.Once(suicInt, () =>
            {
                if (npc == null)
                    return;
                HitInfo nullHit = new HitInfo();

                if (configData.Global.Suicide_Boom)
                {
                    Effect.server.Run(RocketExplosion, npc.transform.position);
                    nullHit.damageTypes.Add(Rust.DamageType.Explosion, 10000);
                }
                else
                    nullHit.damageTypes.Add(Rust.DamageType.Suicide, 10000);
                npc.Die(nullHit);
            });
        }
        #endregion

        #region Commands
        [ConsoleCommand("bot.count")]
        void CmdBotCount(ConsoleSystem.Arg arg)
        {
            string msg = (NPCPlayers.Count == 1) ? "numberOfBot" : "numberOfBots";
            PrintWarning(lang.GetMessage(msg, this), NPCPlayers.Count);
        }

        [ConsoleCommand("bots.count")]
        void CmdBotsCount(ConsoleSystem.Arg arg)
        {
            var records = BotReSpawnBots();
            if (records.Count == 0)
            {
                PrintWarning("There are no spawned npcs");
                return;
            }
            bool none = true;
            foreach (var entry in records)
                if (entry.Value.Count > 0)
                    none = false;
            if (none)
            {
                PrintWarning("There are no spawned npcs");
                return;
            }

            foreach (var entry in BotReSpawnBots().Where(x => Profiles[x.Key].Spawn.AutoSpawn == true || x.Value.Count > 0))
            {
                string temp = Profiles[entry.Key].Spawn.AutoSpawn == false ? "- Temp spawns" : entry.Value.Count > GetPop(Profiles[entry.Key]) ? "- Includes temp spawns" : "";
                PrintWarning(entry.Key + " - " + entry.Value.Count + "/" + GetPop(Profiles[entry.Key]) + temp);
            }
        }

        public BotData GetBData(BasePlayer player)
        {
            Vector3 start = player.eyes.position;
            Ray ray = new Ray(start, Quaternion.Euler(player.eyes.rotation.eulerAngles) * Vector3.forward);
            var hits = Physics.RaycastAll(ray);
            foreach (var hit in hits)
            {
                var npc = hit.collider?.GetComponentInParent<global::HumanNPC>();
                if (hit.distance < 2f)
                {
                    BotData bData;
                    NPCPlayers.TryGetValue(npc.userID, out bData);
                    if (bData != null)
                        return bData;
                }
            }
            return null;
        }

        string TitleText => "<color=orange>" + lang.GetMessage("Title", this) + "</color>";

        [ChatCommand("botrespawn")]
        void botrespawn(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permAllowed) && !player.IsAdmin)
                return;

            if (args == null || args.Length == 0)
            {
                CheckKits();
                BSBGUI(player);
                BSMainUI(player, "", "", "Spawn");
                return;
            }

            string pn = string.Empty;
            var sp = spawnsData.CustomSpawnLocations;

            if (args != null && args.Length == 1)
            {
                switch (args[0])
                {
                    case "info":
                        var bData = GetBData(player);
                        if (bData == null)
                            SendReply(player, TitleText + lang.GetMessage("nonpc", this));
                        else
                            SendReply(player, TitleText + "NPC from profile - " + bData.profilename + ", wearing " + (String.IsNullOrEmpty(bData.kit) ? "no kit" : "kit - " + bData.kit));
                        return;
                }
            }

            if (args != null && args.Length == 2)
            {
                switch (args[0])
                {
                    case "add":
                        args[1] = args[1].Replace(" ", "_").Replace("-", "_");

                        if (Profiles.ContainsKey(args[1]))
                        {
                            SendReply(player, TitleText + lang.GetMessage("alreadyexists", this), args[1]);
                            return;
                        }
                        var customSettings = new Profile(ProfileType.Custom);
                        customSettings.Other.Location = player.transform.position;

                        storedData.Profiles.Add(args[1], customSettings);
                        AddData(args[1], customSettings);
                        SetupSpawnsFile();
                        SaveData();
                        SendReply(player, TitleText + lang.GetMessage("customsaved", this), player.transform.position);
                        BSBGUI(player);
                        BSMainUI(player, "2", args[1], "Spawn");
                        break;

                    case "remove":
                        if (storedData.Profiles.ContainsKey(args[1]))
                        {
                            DestroySpawnGroups(Spawners[args[1]][0].go);
                            spawnsData.CustomSpawnLocations[args[1]].Clear();
                            SaveSpawns();
                            Profiles.Remove(args[1]);
                            storedData.Profiles.Remove(args[1]);
                            storedData.MigrationDataDoNotEdit.Remove(args[1]);
                            SaveData();
                            SendReply(player, TitleText + lang.GetMessage("customremoved", this), args[1]);
                        }
                        else
                            SendReply(player, TitleText + lang.GetMessage("noprofile", this));
                        break;
                }
            }
        }

        void ShowSpawn(BasePlayer player, Vector3 loc, int num, float duration) => player.SendConsoleCommand("ddraw.text", duration, ValidPoint(loc) ? Color.green : Color.red, loc, $"<size=80>{num}</size>");
        void ShowProfiles(BasePlayer player, Vector3 loc, string name, float duration) => player.SendConsoleCommand("ddraw.text", duration, Color.green, loc, $"<size=20>{name}</size>");

        #endregion

        public Dictionary<ulong, int> DeadNPCPlayerIds = new Dictionary<ulong, int>();
        public Dictionary<ulong, string> KitRemoveList = new Dictionary<ulong, string>();
        public List<Vector3> SmokeGrenades = new List<Vector3>();
        public Dictionary<ulong, Inv> botInventories = new Dictionary<ulong, Inv>();

        public class Inv
        {
            public Profile profile;
            public string name;
            public List<InvContents>[] inventory = { new List<InvContents>(), new List<InvContents>(), new List<InvContents>() };
        }

        public class InvContents
        {
            public int ID;
            public int amount;
            public ulong skinID;
        }

        public Dictionary<string, int> Biomes = new Dictionary<string, int> { { "BiomeArid", 1 }, { "BiomeTemperate", 2 }, { "BiomeTundra", 4 }, { "BiomeArctic", 8 } };

        bool IsSpawner(string name) => Biomes.ContainsKey(name) || defaultData.Monuments.ContainsKey(name) || storedData.Profiles.ContainsKey(name);

        public enum Range { Melee, Close, Mid, Long, None }
        public Range TargetRange(float distance) => distance > 40 ? Range.Long : distance > 12 ? Range.Mid : distance > 4 ? Range.Close : Range.Melee;

        public Range BestRange(BotData bdata, Range targetrange)
        {
            if (bdata.melee)
                return Range.Melee;

            switch (targetrange)
            {
                case Range.Melee: return bdata.Weaps[Range.Melee].Count > 0 ? Range.Melee : bdata.Weaps[Range.Close].Count > 0 ? Range.Close : bdata.Weaps[Range.Mid].Count > 0 ? Range.Mid : Range.Long;
                case Range.Close: return bdata.Weaps[Range.Close].Count > 0 ? Range.Close : bdata.Weaps[Range.Mid].Count > 0 ? Range.Mid : bdata.Weaps[Range.Long].Count > 0 ? Range.Long : Range.Melee;
                case Range.Mid: return bdata.Weaps[Range.Mid].Count > 0 ? Range.Mid : bdata.Weaps[Range.Long].Count > 0 ? Range.Long : bdata.Weaps[Range.Close].Count > 0 ? Range.Close : Range.Melee;
                case Range.Long: return bdata.Weaps[Range.Long].Count > 0 ? Range.Long : bdata.Weaps[Range.Mid].Count > 0 ? Range.Mid : bdata.Weaps[Range.Close].Count > 0 ? Range.Close : Range.Melee;

            }
            return Range.Mid;
        }

        #region BotMono
        public class BotData : MonoBehaviour
        {
            public string kit;
            public Parachute chute;
            public TorchWeapon torch;
            public GeigerCounter gc;
            public BaseProjectile gun;
            public Timer deaggro;
            public CustomGroup sg;
            public NewSpawnPoint sp;
            public List<SpawnInfo> SpawnPoints = new List<SpawnInfo>();
            public Dictionary<BasePlayer, float> Players = new Dictionary<BasePlayer, float>();
            public Dictionary<BasePlayer, float> Targets = new Dictionary<BasePlayer, float>();
            public BasePlayer CurrentTarget;
            public BaseEntity CurrentAnimal;
            public ScientistNPC npc;
            public BaseAIBrain brain;
            public Profile profile;
            public Range currentRange = Range.None;
            public Dictionary<Range, List<HeldEntity>> Weaps = new Dictionary<Range, List<HeldEntity>>();
            public List<HeldEntity> throwables = new List<HeldEntity>();
            public List<MedicalTool> meds = new List<MedicalTool>();
            public float nextThrowTime = Time.time + 10f;
            public float nextHealTime = Time.time + 10f;
            public string profilename, group;
            public bool canFire = true, throwing, healing, melee, temporary, noweapon, hasHeadLamp, stationary, inAir, abletoChute, invincible;
            CapsuleCollider capcol;

            void Start()
            {
                brain = npc.Brain;
                roampoint = npc.transform.position;

                if (npc.WaterFactor() > 0.9f)
                {
                    bs.timer.Once(2f, () =>
                    { 
                        if (npc != null && !npc.IsDestroyed)
                            npc.Kill();
                    });
                    return;
                }

                if (abletoChute)
                {
                    inAir = true;
                    capcol = npc.playerCollider;
                    //capcol.isTrigger = true; //moved to addchute
                    //capcol.radius = 2.5f;
                }

                if (!noweapon)
                    InvokeRepeating("SelectWeapon", UnityEngine.Random.Range(1.8f, 2.2f), UnityEngine.Random.Range(2.8f, 3.2f));

                InvokeRepeating("DoThink", UnityEngine.Random.Range(1.5f, 2.0f), UnityEngine.Random.Range(1.5f, 2.0f));
                InvokeRepeating("Attack", 0.5f, 0.5f);
                if (!temporary)
                    InvokeRepeating("KILL", random.Next(3, 15), random.Next(3, 15));
            }

            public Timer delay;
            public float distance = 0;
            public float distance1 = 0;
            public AIInformationZone zone;
            public Vector3 roampoint = new Vector3(); 
            public DateTime set;
            public bool relaxing;
            public DateTime lastmove = DateTime.Now;
            public bool SafeIgnore(BasePlayer player, ScientistNPC npc, bool safe) => safe && (player.InSafeZone() || npc.InSafeZone());
            public bool Hostile(BasePlayer player) => bs.NPCPlayers.ContainsKey(player.userID) ? true : player.State.unHostileTimestamp > Network.TimeEx.currentTimestamp;

            public void Think()
            {
                if (sp == null || inAir || npc.IsWounded())
                    return;

                //// TESTING
                if (profile.Behaviour.NPCs_Attack_Animals && CurrentAnimal != null)
                {
                    if (Vector3.Distance(CurrentAnimal.transform.position, npc.transform.position) > 30)
                        CurrentAnimal = null;
                    else
                    {
                        Fire(CurrentAnimal, npc.GetHeldEntity() as AttackEntity, true);
                        relaxing = false;
                        return;
                    }
                }

                if (CurrentTarget == null || !Targets.ContainsKey(CurrentTarget) || !brain.Senses.Memory.IsLOS(CurrentTarget))
                    GetNearest();

                if (CurrentTarget != null)
                {
                    if (!Hostile(CurrentTarget) && (profile.Behaviour.Peace_Keeper || SafeIgnore(CurrentTarget, npc, profile.Behaviour.Respect_Safe_Zones)))
                        CheckKnownTime(CurrentTarget);
                    else if (Targets.ContainsKey(CurrentTarget))
                    {
                        relaxing = false;
                        brain.Navigator.SetFacingDirectionEntity(CurrentTarget);
                    }
                }
                else
                {
                    if (!relaxing)
                    {
                        relaxing = true;
                        brain.Navigator.ClearFacingDirectionOverride();
                        if (profile.Spawn.Stationary)
                            brain.Navigator.SetFacingDirectionOverride(new Vector3(npc.eyes.HeadForward().x, 0, npc.eyes.HeadForward().z));
                        npc.modelState.aiming = false;
                        npc.SetPlayerFlag(BasePlayer.PlayerFlags.Aiming, false);
                    }
                    if (!stationary && GoHome())
                    {
                        if (sp == null)
                        {
                            if (npc != null && !npc.IsDestroyed)
                                npc.Kill();
                            return;
                        }
                        else
                            roampoint = sp.transform.position;
                    }
                }

                if (!npc.IsReloading())
                {
                    if (!throwing && nextHealTime < Time.time && npc.health < npc.MaxHealth() / 5 * 4 && meds.Count() > 0 && (CurrentTarget == null || !brain.Senses.Memory.IsLOS(CurrentTarget)))
                    {
                        TryHeal();
                        return;
                    }

                    if (!healing && CurrentTarget != null && nextThrowTime < Time.time && profile.NextProfileThrow < Time.time && throwables.Count > 0 && !brain.Senses.Memory.IsLOS(CurrentTarget) && !CurrentTarget.isInAir && ThrowDistance())
                    {
                        profile.NextProfileThrow = Time.time + 5;
                        TryThrow(CurrentTarget, Vector3.Distance(CurrentTarget.transform.position, npc.transform.position));
                        return;
                    }
                }

                if (!stationary)
                {
                    brain.Navigator.MaxRoamDistanceFromHome = CurrentTarget != null ? 400 : profile.Behaviour.Roam_Range;

                    distance1 = Vector3.Distance(roampoint, npc.transform.position);

                    if (distance1 < distance && distance1 > 2)//3?
                    {
                        lastmove = DateTime.Now;
                        distance = distance1;

                        GoTo(brain.Navigator, roampoint, CurrentTarget ? BaseNavigator.NavigationSpeed.Fast : BaseNavigator.NavigationSpeed.Slow);

                    }
                    else
                    {
                        if ((DateTime.Now - lastmove).TotalSeconds < profile.Behaviour.Roam_Pause_Length + 2.1)
                            return;

                        var loc = GetNearNavPoint(profile.Other.Short_Roam_Vision ? 10 : 30);

                        if (GoTo(brain.Navigator, loc, CurrentTarget ? BaseNavigator.NavigationSpeed.Fast : BaseNavigator.NavigationSpeed.Slow))
                        {
                            lastmove = DateTime.Now;
                            distance = Vector3.Distance(loc, npc.transform.position);
                            roampoint = loc;
                        }
                    }
                }
            }

            public bool ThrowDistance()
            {
                if (Vector3.Distance(CurrentTarget.transform.position, npc.transform.position) < 15 || Vector3.Distance(CurrentTarget.transform.position, npc.transform.position) > 50)
                    return false;
                if (CurrentTarget.transform.position.y - npc.transform.position.y > 20 || CurrentTarget.transform.position.y - npc.transform.position.y < -20)
                    return false;
                return true;
            }

            public bool GoTo(BaseNavigator nav, Vector3 pos, BaseNavigator.NavigationSpeed speed)
            {
                if (!nav.CanUseNavMesh)
                    return false;
                brain.Navigator.Destination = pos;
                return brain.Navigator.SetDestination(pos, CurrentTarget ? BaseNavigator.NavigationSpeed.Fast : BaseNavigator.NavigationSpeed.Slow, 0f, 0f);
            }

            public bool GoHome()
            {
                if (sp == null)
                    return false;

                brain.Navigator.MaxRoamDistanceFromHome = CurrentTarget == null ? profile.Behaviour.Roam_Range : 400;

                var dist = Vector3.Distance(sp.transform.position, brain.transform.position);
                if (dist > profile.Behaviour.Roam_Range && dist > 3)
                {
                    GoTo(brain.Navigator, sp.transform.position, BaseNavigator.NavigationSpeed.Normal);
                    return true;
                }
                return false;
            }

            void TryThrow(BasePlayer t, float distance)
            {
                if (t == null)
                    return;
                Vector3 loc = t.transform.position;
                var active = npc.GetHeldEntity();
                var throwable = throwables.GetRandom();
                if (throwable == null || active == null || throwing)
                    return;

                throwing = true;
                bs.UpdateActiveItem(npc, throwable, this);

                bs.timer.Once(1.5f, () =>
                {
                    if (npc != null && t != null)
                    {
                        if (npc.GetHeldEntity() != null)
                        {
                            npc.SetAimDirection((t == null ? loc : t.ServerPosition - npc.ServerPosition).normalized);
                            npc.SignalBroadcast(BaseEntity.Signal.Throw);
                            ServerThrow(npc, t);
                        }
                    }
                });

                bs.timer.Once(3f, () =>
                {
                    if (npc != null && active != null && this != null)
                    {
                        bs.UpdateActiveItem(npc, active, this);
                        throwing = false;
                        nextThrowTime = Time.time + 20f;
                    }
                });
            }

            public void ServerThrow(ScientistNPC npc, BasePlayer target)
            {
                ThrownWeapon weap = npc.GetHeldEntity() as ThrownWeapon;
                if (weap == null)
                    return;

                Vector3 vector3 = npc.eyes.position;
                var rand = UnityEngine.Random.insideUnitCircle * (10 - ((float)profile.Other.Grenade_Precision_Percent / 10f));
                Vector3 vector31 = ((target.transform.position + new Vector3(rand.x, 0, rand.y)) - npc.transform.position).normalized;
                weap.SignalBroadcast(BaseEntity.Signal.Throw, string.Empty, null);
                BaseEntity baseEntity = GameManager.server.CreateEntity(weap.prefabToThrow.resourcePath, vector3, Quaternion.LookRotation((weap.overrideAngle == Vector3.zero ? -vector31 : weap.overrideAngle)), true);
                if (baseEntity == null)
                    return;

                baseEntity.SetCreatorEntity(npc);
                Vector3 vector32 = vector31 + (Quaternion.AngleAxis(10f, Vector3.zero) * Vector3.up);
                float throwVelocity = GetThrowVelocity(vector3, target.transform.position, vector32);
                if (float.IsNaN(throwVelocity))
                {
                    vector32 = vector31 + (Quaternion.AngleAxis(20f, Vector3.zero) * Vector3.up);
                    throwVelocity = GetThrowVelocity(vector3, target.transform.position, vector32);
                    if (float.IsNaN(throwVelocity))
                        throwVelocity = 5f;
                }
                baseEntity.SetVelocity((vector32 * throwVelocity) * 1f);
                if (weap.tumbleVelocity > 0f)
                    baseEntity.SetAngularVelocity(Vector3.zero * weap.tumbleVelocity);

                baseEntity.Spawn();
                weap.StartAttackCooldown(weap.repeatDelay);
                TimedExplosive exp = baseEntity as TimedExplosive;
                if (exp != null && !exp.canStick)
                    exp.SetFuse(Mathf.Max(3f, throwVelocity / 4.5f));
            }

            float GetThrowVelocity(Vector3 throwPos, Vector3 targetPos, Vector3 aimDir)
            {
                Vector3 vector3 = targetPos - throwPos;
                Vector2 vector2 = new Vector2(vector3.x, vector3.z);
                float single = vector2.magnitude;
                float single1 = vector3.y;
                vector2 = new Vector2(aimDir.x, aimDir.z);
                float single2 = vector2.magnitude;
                float single3 = aimDir.y;
                float single4 = Physics.gravity.y;
                return Mathf.Sqrt(0.5f * single4 * single * single / (single2 * (single2 * single1 - single3 * single)));
            }

            public Vector3 GetNearNavPoint(int radius = 30)
            {
                var pos = bs.TryGetSpawn(CurrentTarget == null || currentRange == Range.Long ? npc.transform.position : CurrentTarget.transform.position, radius, profile.type == ProfileType.Event, profile.Other.Off_Terrain);
                return pos == Vector3.zero ? npc.transform.position : pos;
            }

            public void TryHeal()
            {
                HeldEntity active = npc.GetHeldEntity();
                if (active == null || healing)
                    return;

                healing = true;
                foreach (var item in meds)
                {
                    if (item.name.Contains("syringe_medical.entity")) //// Add bandage use
                    {
                        bs.UpdateActiveItem(npc, item, this);
                        bs.timer.Once(1.5f, () => npc?.SignalBroadcast(BasePlayer.Signal.Attack, "", null));
                        break;
                    }
                }
                bs.timer.Once(1f, () =>
                {
                    if (npc != null)
                    {
                        var newActive = npc.GetHeldEntity();
                        if (newActive?.name != null && newActive.name.Contains("syringe_medical.entity"))
                        {
                            ItemModConsumable component = newActive?.GetOwnerItemDefinition()?.GetComponent<ItemModConsumable>();
                            if (component == null)
                                return;
                            foreach (var effect in component.effects.Where(effect => effect.type == MetabolismAttribute.Type.Health || effect.type == MetabolismAttribute.Type.HealthOverTime))
                                npc.health = npc.health + (bs.configData.Global.Scale_Meds_To_Health ? effect.amount / 100 * npc.MaxHealth() : effect.amount);
                            npc.InitializeHealth(npc.health, npc.MaxHealth());
                        }
                    }
                });
                bs.timer.Once(4f, () =>
                {
                    if (npc != null)
                    {
                        bs.UpdateActiveItem(npc, active, this);
                        healing = false;
                        nextHealTime = Time.time + 15f;
                    }
                });
            }

            List<BaseAnimalNPC> Animals = new List<BaseAnimalNPC>();
            public void DoThink()
            {
                if (npc == null)
                {
                    bs.TidyUp(npc, this, $"NPC @ DoThink - {profilename}");
                    return;
                }
                Think();

                if (profile.Behaviour.NPCs_Attack_Animals && CurrentAnimal == null)
                {
                    Animals = new List<BaseAnimalNPC>();
                    Vis.Entities<BaseAnimalNPC>(npc.transform.position, 10f, Animals);
                    foreach (var animal in Animals)
                        if (npc.CanSeeTarget(animal))
                        {
                            CurrentAnimal = Animals[0];
                            break;
                        }
                }

                if (bs.configData.Global.Allow_Ai_Dormant && (profile.type != ProfileType.Biome || !bs.configData.Global.Prevent_Biome_Ai_Dormant))
                {
                    int playersnear = BaseEntity.Query.Server.GetPlayersInSphere(npc.Brain.Senses.owner.transform.position, 300, AIBrainSenses.playerQueryResults, new Func<BasePlayer, bool>(IsPlayer));

                    if (playersnear == 0)
                        npc.IsDormant = Rust.Ai.AiManager.ai_dormant && CurrentTarget == null;
                    else
                        npc.IsDormant = false;
                }

                int playersInSphere = BaseEntity.Query.Server.GetPlayersInSphere(npc.Brain.Senses.owner.transform.position, profile.Behaviour.Aggro_Range, AIBrainSenses.playerQueryResults, new Func<BasePlayer, bool>(WantsToAttack));
                for (int i = 0; i < playersInSphere; i++)
                {
                    BasePlayer player = AIBrainSenses.playerQueryResults[i];
                    Addto(Players, player);
                    Addto(Targets, player);

                    npc.Brain.Senses.Memory.SetKnown(player, npc, npc.Brain.Senses);
                    npc.Brain.Senses.Memory.Targets = Players.Keys.ToList<BaseEntity>();
                }

                if (playersInSphere == 0)
                    npc.Brain.Senses.Memory.Targets.Clear();

                UpdateKnownPlayersLOS();
                CheckKnownTime(null);
            }

            public void Addto(Dictionary<BasePlayer, float> list, BasePlayer player) => list[player] = Time.time;

            public void UpdateKnownPlayersLOS()
            {
                foreach (var player in Players)
                {
                    if (player.Key == null)
                        continue;

                    if (player.Key.health <= 0 || player.Key.IsDead() || player.Key.limitNetworking || player.Key._limitedNetworking)
                        CheckKnownTime(player.Key);

                    brain.Senses.Memory.SetLOS(player.Key, CanSeeTarget(player.Key));
                }
            }

            bool CanSeeTarget(BaseEntity entity)
            {
                BasePlayer player = entity as BasePlayer;
                if (player == null)
                    return true;
                return IsPlayerVisibleToUs(player, 1218519041);
            }

            public bool IsPlayerVisibleToUs(BasePlayer otherPlayer, int layerMask)
            {
                Vector3 vector3;
                if (otherPlayer == null)
                    return false;
                if (otherPlayer.limitNetworking || otherPlayer._limitedNetworking)
                    return false;
                if (npc.isMounted)
                    vector3 = npc.eyes.worldMountedPosition;
                else if (!npc.IsDucked())
                    vector3 = (!npc.IsCrawling() ? npc.eyes.worldStandingPosition : npc.eyes.worldCrawlingPosition);
                else
                    vector3 = npc.eyes.worldCrouchedPosition;

                if (!otherPlayer.IsVisibleSpecificLayers(vector3, otherPlayer.CenterPoint(), layerMask, Single.PositiveInfinity) && !otherPlayer.IsVisibleSpecificLayers(vector3, otherPlayer.transform.position, layerMask, Single.PositiveInfinity) && !otherPlayer.IsVisibleSpecificLayers(vector3, otherPlayer.eyes.position, layerMask, Single.PositiveInfinity))
                    return false;

                if (!IsVisibleSpecificLayers(otherPlayer.CenterPoint(), vector3, layerMask, Single.PositiveInfinity) && !IsVisibleSpecificLayers(otherPlayer.transform.position, vector3, layerMask, Single.PositiveInfinity) && !IsVisibleSpecificLayers(otherPlayer.eyes.position, vector3, layerMask, Single.PositiveInfinity))
                    return false;

                return true;
            }


            public bool IsVisibleSpecificLayers(Vector3 position, Vector3 target, int layerMask, float maxDistance = Single.PositiveInfinity)
            {
                Vector3 vector3 = target - position;
                float single = vector3.magnitude;
                if (single < Mathf.Epsilon)
                    return true;

                Vector3 vector31 = vector3 / single;
                Vector3 vector32 = vector31 * Mathf.Min(single, 0.01f);
                return IsVisible(new Ray(position + vector32, vector31), layerMask, maxDistance);
            }


            public bool IsVisible(Ray ray, int layerMask, float maxDistance)
            {
                RaycastHit raycastHit;
                RaycastHit raycastHit1;
                if (ray.origin.IsNaNOrInfinity() || ray.direction.IsNaNOrInfinity() || ray.direction == Vector3.zero)
                    return false;

                //if (npc.WorldSpaceBounds().Trace(ray, out raycastHit, maxDistance))
                //{
                //    return false;
                //}
                npc.WorldSpaceBounds().Trace(ray, out raycastHit, maxDistance);

                if (GamePhysics.Trace(ray, 0f, out raycastHit1, maxDistance, layerMask, QueryTriggerInteraction.UseGlobal, null))
                {
                    BaseEntity entity = raycastHit1.GetEntity();
                    if (entity == npc)
                        return true;
                    if (entity != null && npc.GetParentEntity() && npc.GetParentEntity().EqualNetID(entity) && raycastHit1.IsOnLayer(Rust.Layer.Vehicle_Detailed))
                        return true;

                    if (raycastHit1.distance <= raycastHit.distance)
                        return false;
                }
                return true;
            }

            float single = float.PositiveInfinity;
            float dist = 0;
            BasePlayer nearest = null;
            public void GetNearest()
            {
                single = float.PositiveInfinity;
                nearest = null;
                foreach (var player in Targets)
                {
                    if (player.Key == null || player.Key.Health() <= 0f)
                        continue;

                    dist = Vector3.Distance(player.Key.transform.position, npc.transform.position);
                    if (brain.Senses.Memory.IsLOS(player.Key) && dist < single)
                    {
                        single = dist;
                        nearest = player.Key;
                    }
                }
                CurrentTarget = nearest == null ? CurrentTarget : nearest;
            }

            public void CheckKnownTime(BasePlayer player = null)
            {
                if (player != null && CurrentTarget == player)
                    CurrentTarget = null;

                foreach (var p in Targets.ToDictionary(pair => pair.Key, pair => pair.Value))
                    if ((player != null && p.Key == player) || Time.time - p.Value > bs.configData.Global.Deaggro_Memory_Duration || p.Key.limitNetworking || p.Key._limitedNetworking)
                    {
                        if (CurrentTarget == p.Key)
                            CurrentTarget = null;
                        Targets.Remove(p.Key);
                    }
            }

            public bool IsPlayer(BasePlayer player) => player?.net?.connection != null && !player.IsNpc;
            public bool WantsToAttack(BasePlayer entity) => WantsAttack(entity, false);
            public bool WantsAttack(BasePlayer player, bool hurt)
            {
                if (npc?.Brain?.Senses == null || player == null || player.limitNetworking || player._limitedNetworking || !player.isServer || player.EqualNetID(npc.Brain.Senses.owner) || player.Health() <= 0f)
                    return false;

                if (bs.configData.Global.Enable_Targeting_Hook && Interface.CallHook("OnBotReSpawnNPCTarget", npc, player) != null)
                    return false;

                if (profile.Behaviour.Ignore_All_Players && IsPlayer(player))
                    return false;

                if (profile.Behaviour.Ignore_Sleepers && player.IsSleeping() || player.IsDead())
                    return false;

                if (!Hostile(player) && profile.Behaviour.Respect_Safe_Zones && (player.InSafeZone() || npc.InSafeZone()))
                    return false;

                if (!hurt && !bs.NoSash && !profile.Behaviour.Target_Noobs && player.IsNoob())
                    return false;

                if ((profile.Behaviour.Target_ZombieHorde == ShouldAttack.Ignore || (profile.Behaviour.Target_ZombieHorde == ShouldAttack.Defend && !hurt)) && player.Categorize() == "Zombie")
                    return false;

                if ((profile.Behaviour.Target_HumanNPC == ShouldAttack.Ignore || (profile.Behaviour.Target_HumanNPC == ShouldAttack.Defend && !hurt)) && bs.HumanNPCs.Contains(player.userID))
                    return false;

                BotData bData;
                bs.NPCPlayers.TryGetValue(player.userID, out bData);
                if (bData != null)
                {
                    if (bData.profilename == profilename)
                        return false;
                    if (!bs.configData.Global.Ignore_Factions && FactionAllies(profile.Behaviour.Faction, profile.Behaviour.SubFaction, bData.profile.Behaviour.Faction, bData.profile.Behaviour.SubFaction))
                        return false;
                }

                if (player.IsNpc && player.Categorize() != "Zombie" && bData == null)
                    if ((profile.Behaviour.Target_Other_Npcs == ShouldAttack.Ignore || (profile.Behaviour.Target_Other_Npcs == ShouldAttack.Defend && !hurt)))
                        return false;

                brain.Senses.Memory.SetLOS(player, CanSeeTarget(player));

                if (!hurt)
                {
                    if (profile.Behaviour.Peace_Keeper && (bs.configData.Global.Peacekeeper_Uses_Damage || !Hostile(player)))
                        return false;

                    if (!brain.Senses.Memory.IsLOS(player))
                        return false;

                    float dist = Vector3.Distance(npc.transform.position, player.transform.position);
                    if ((player == CurrentTarget && dist > profile.Behaviour.DeAggro_Range) || (player != CurrentTarget && dist > profile.Behaviour.Aggro_Range))
                        return false;
                }
                return true;
            }

            public bool FactionAllies(int aa, int ab, int ba, int bb) => (aa == 0 && ab == 0) || (ba == 0 && bb == 0) || (aa != 0 && aa == ba) || (aa != 0 && aa == bb) || (ab != 0 && ab == ba) || (ab != 0 && ab == bb);

            public void KILL()
            {

                if (npc == null || profile == null || sg?.currentPopulation == null)
                {
                    string message = "";
                    if (bs.debug)
                    {
                        if (npc == null) message += "npc ";
                        if (profile == null) message += "profile ";
                        if (sg == null) message += "sg ";
                        if (sg?.currentPopulation == null) message += "sg?.currentPopulation ";
                        message += $" - {profilename}";
                    }
                    bs.TidyUp(npc, this, message);
                    return;
                }
                if (sg.currentPopulation > GetPop(profile)) 
                {
                    DestroyImmediate(npc?.GetComponent<SpawnPointInstance>());
                    if (!npc.IsDestroyed)
                        npc.Kill();
                }
            }

            void SelectWeapon() => bs.SelectWeapon(npc, this, brain);

            public void OnDestroy()
            {
                CancelInvoke("KILL");
                CancelInvoke("SelectWeapon");
                CancelInvoke("DoThink");
                CancelInvoke("Attack");

                if (sg?.marker != null && sg.currentPopulation == 1 && !profile.Other.Always_Show_Map_Marker)
                {
                    sg.marker.alpha = 0f;
                    sg.marker.SendUpdate();
                }
                if (temporary && sg != null && sg.currentPopulation < 2)
                    Destroy(sg);

                if (npc?.Brain != null)
                    Destroy(npc.Brain);

                if (npc?.userID != null)
                    bs.NPCPlayers.Remove(npc.userID);

                if (chute != null)
                    chute.Kill();
            }

            private void OnTriggerEnter(Collider col)
            {
                if (!inAir)
                    return;

                NavMeshHit hit;
                if (!NavMesh.SamplePosition(npc.transform.position, out hit, 2, -1))
                    if (!NavMesh.SamplePosition(npc.transform.position, out hit, 10, -1))
                        return;

                if (npc.WaterFactor() > 0.9f)
                {
                    npc.Kill();
                    return;
                }

                if (chute != null)
                {
                    chute._health = 0.19f;
                    chute.Kill(BaseNetworkable.DestroyMode.Gib);
                    npc.DismountObject();
                }
                if (capcol != null)
                {
                    capcol.isTrigger = false;
                    capcol.radius -= 2f;
                }

                npc.playerRigidbody.isKinematic = true;
                npc.playerRigidbody.useGravity = false;
                npc.gameObject.layer = 17;
                npc.ServerPosition = hit.position;

                npc.Brain.Navigator.CanUseNavMesh = true;
                npc.NavAgent.enabled = true;
                npc.Brain.Navigator.PlaceOnNavMesh(hit.position.y); 

                if (!npc.NavAgent.isOnNavMesh)
                    npc.BecomeWounded();

                inAir = false;
                npc.Resume();
            }

            #region Attack
            public void Attack()
            {
                if (npc == null)
                {
                    bs.TidyUp(npc, this, $"NPC is null @Attack() - {profilename}");
                    return;
                }
                if (CurrentTarget == null || !Targets.ContainsKey(CurrentTarget) || !npc.Brain.Senses.Memory.IsLOS(CurrentTarget))
                    GetNearest();

                if (CurrentTarget != null && canFire)
                    AttackTarget(CurrentTarget, npc.Brain.Senses.Memory.IsLOS(CurrentTarget));
            }

            public void AttackTarget(BasePlayer target, bool targetIsLOS)
            {
                if (sp == null)
                    return;
                var heldEntity = npc.GetHeldEntity() as AttackEntity;
                if (heldEntity == null || target == null)
                    return;

                if (heldEntity == gc)
                    SelectWeapon();

                if (Targets.ContainsKey(target))
                    npc.Brain.Navigator.SetFacingDirectionEntity(target);

                var melee = heldEntity as BaseMelee;

                if (melee)
                {
                    npc.nextTriggerTime = Time.time + 1f;
                    melee.attackLengthMin = 10f;

                    if (target != null && bs.ValidPoint(target.transform.position))
                    {
                        MeleeAttack(target, melee);
                        if (!stationary)
                            GoTo(brain.Navigator, target.transform.position + (target.transform.position - npc.transform.position).normalized, BaseNavigator.NavigationSpeed.Fast);
                    }
                    else
                        if (!stationary)
                        GoTo(brain.Navigator, sp.transform.position, BaseNavigator.NavigationSpeed.Fast);
                    return;
                }

                if (!stationary)
                {
                    var ft = heldEntity as FlameThrower;
                    if (ft != null)
                    {
                        var loc = GetNearNavPoint(3);
                        if (bs.ValidPoint(loc))
                        {
                            FlameAttack(npc, target, ft);
                            GoTo(brain.Navigator, loc, BaseNavigator.NavigationSpeed.Fast);
                            return;
                        }
                    }
                }

                Fire(target, heldEntity, targetIsLOS);
            }

            public void Fire(BaseEntity target, AttackEntity heldEntity, bool targetIsLOS)
            {
                if (heldEntity == null)
                    return;

                //// TESTING
                var animal = target as BaseAnimalNPC;
                if (animal != null && profile.Behaviour.NPCs_Attack_Animals && CurrentTarget == null)
                {
                    brain.Navigator.SetFacingDirectionOverride(GetAim());

                    //npc.SetAimDirection(GetAim());
                    if (!profile.Spawn.Stationary)
                        npc.Brain.Navigator.SetDestination(npc.transform.position - npc.eyes.BodyForward() * 5);
                }

                float single = Vector3.Dot(npc.eyes.BodyForward(), (target.CenterPoint() - npc.eyes.position).normalized);
                if (!targetIsLOS)
                {
                    if (single < 0.5f)
                        npc.targetAimedDuration = 0f;
                    npc.CancelBurst(0.2f);
                }
                else if (single > 0.2f && !npc.IsReloading())
                    npc.targetAimedDuration += 0.5f;

                if (!(npc.targetAimedDuration >= 0.5f & targetIsLOS))
                    npc.CancelBurst(0.2f);
                else
                {
                    BaseProjectile baseProjectile = heldEntity as BaseProjectile;
                    if (baseProjectile)
                    {
                        if (baseProjectile.primaryMagazine.contents <= 0)
                        {
                            baseProjectile.ServerReload();
                            return;
                        }
                        if (baseProjectile.NextAttackTime > Time.time)
                            return;

                        if (currentRange == Range.Long || heldEntity is BowWeapon || heldEntity is BaseLauncher)
                        {
                            //// Work on better aim duration checks
                            npc.modelState.aiming = true;
                            npc.SetPlayerFlag(BasePlayer.PlayerFlags.Aiming, true);
                            if (npc.targetAimedDuration > 2f)
                                npc.targetAimedDuration = 0;
                            else
                                return;
                        }
                    }
                    if (Mathf.Approximately(heldEntity.attackLengthMin, -1f))
                    {
                        ServerUse(heldEntity, npc.damageScale);
                        npc.lastGunShotTime = Time.time;
                        if (animal != null)
                            animal.Hurt(animal.MaxHealth() / 4f);
                        return;
                    }
                    if (npc.IsInvoking(new Action(TriggerDown)))
                        return;

                    if (Time.time <= npc.nextTriggerTime || Time.time <= npc.triggerEndTime)
                        return;

                    npc.InvokeRepeating(new Action(TriggerDown), 0f, 0.01f);
                    npc.triggerEndTime = Time.time + UnityEngine.Random.Range(heldEntity.attackLengthMin / 2, Mathf.Min(4, heldEntity.attackLengthMax * 2));
                    TriggerDown();
                    if (animal != null)
                        animal.Hurt(animal.MaxHealth() / 4f);
                }
            }
            public void TriggerDown()
            {
                AttackEntity heldEntity = npc.GetHeldEntity() as AttackEntity;
                if (heldEntity != null)
                    ServerUse(heldEntity, npc.damageScale);

                npc.lastGunShotTime = Time.time;
                if (Time.time > npc.triggerEndTime)
                {
                    npc.CancelInvoke(new Action(TriggerDown));
                    npc.nextTriggerTime = Time.time + (heldEntity != null ? heldEntity.attackSpacing : 1f);
                }
            }

            public void LauncherUse(BaseLauncher launcher)
            {
                if (CurrentTarget == null || !Targets.ContainsKey(CurrentTarget) || !npc.Brain.Senses.Memory.IsLOS(CurrentTarget))
                    GetNearest();

                if (CurrentTarget == null)
                    return;
                var dist = Vector3.Distance(npc.transform.position, CurrentTarget.transform.position);
                if (launcher.primaryMagazine.ammoType.itemid != 1055319033 && (dist > 100 || dist < 7)) //// Multiple ammo types!?
                    return;

                ItemModProjectile component = launcher.primaryMagazine.ammoType.GetComponent<ItemModProjectile>();
                if (!component)
                    return;

                if (launcher.primaryMagazine.contents <= 0)
                {
                    launcher.SignalBroadcast(BaseEntity.Signal.DryFire, null);
                    launcher.StartAttackCooldown(1f);
                    return;
                }
                if (!component.projectileObject.Get().GetComponent<ServerProjectile>())
                {
                    launcher.ServerUse(1f, null);
                    return;
                }
                launcher.primaryMagazine.contents--;
                if (launcher.primaryMagazine.contents < 0)
                    launcher.primaryMagazine.contents = 0;

                float distance = Vector3.Distance(npc.transform.position, CurrentTarget.transform.position);
                Vector3 muzzlePoint = (CurrentTarget.transform.position + Offset(component.projectileObject.resourcePath, distance) - npc.eyes.position).normalized;

                var baseEntity = GameManager.server.CreateEntity(component.projectileObject.resourcePath, npc.eyes.position + (npc.eyes.BodyForward() / 2f), new Quaternion(), true);

                baseEntity.creatorEntity = npc;
                ServerProjectile serverProjectile = baseEntity.GetComponent<ServerProjectile>();
                if (serverProjectile)
                    serverProjectile.InitializeVelocity(muzzlePoint * serverProjectile.speed);

                baseEntity.SendMessage("SetDamageScale", profile.Behaviour.Rocket_DamageScale);
                baseEntity.Spawn();
                launcher.StartAttackCooldown(launcher.ScaleRepeatDelay(launcher.repeatDelay));
                launcher.SignalBroadcast(BaseEntity.Signal.Attack, string.Empty, null);
            }

            public Vector3 Offset(string resource, float distance)
            {
                if (resource.Contains("_he"))
                    return new Vector3(0, distance * (distance / 10) / 18f, 0);
                if (resource.Contains("_hv"))
                    return new Vector3(0, distance * (distance / 100) / 18f, 0); // 100 was 18. Changed?
                return new Vector3(0, distance * (distance / 14) / 18f, 0);
            }

            int AccuracyScaled(int accuracy, float distance) => distance > 300 ? (int)(accuracy / 2f) : distance > 200 ? (int)(accuracy / 5f * 3f) : distance > 100 ? (int)(accuracy / 4f * 3f) : accuracy;

            public Vector3 GetAim()
            {
                BaseEntity target = CurrentTarget != null ? CurrentTarget : CurrentAnimal != null ? CurrentAnimal : null;
                if (target == null || Vector3.Distance(npc.transform.position, target.transform.position) < 2f)
                    return npc.eyes.BodyForward();

                return (target.transform.position - npc.transform.position).normalized;
            }

            public void ServerUse(HeldEntity held, float damageModifier)
            {
                var launcher = held as BaseLauncher;
                if (launcher)
                {
                    LauncherUse(launcher);
                    return;
                }

                if (held is FlameThrower)
                    return;

                var att = held as BaseProjectile;
                if (att == null || att.HasAttackCooldown())
                    return;
                damageModifier *= (held.name.Contains("bolt") || held.name.Contains("l96")) ? (float)profile.Behaviour.RangeWeapon_DamageScale : 1;

                if (att.primaryMagazine.contents <= 0)
                {
                    held.SignalBroadcast(BaseEntity.Signal.DryFire, null);
                    att.StartAttackCooldownRaw(1f);
                    return;
                }

                att.primaryMagazine.contents--;
                if (att.primaryMagazine.contents < 0)
                    att.primaryMagazine.contents = 0;

                NPCPlayer npc1 = held.GetOwnerPlayer() as NPCPlayer;
                //npc1.SetAimDirection(npc1.GetAimDirection()); // GetAim()
                npc1.SetAimDirection(GetAim());
                att.StartAttackCooldownRaw(att.repeatDelay);
                att.SignalBroadcast(BaseEntity.Signal.Attack, string.Empty, null);

                ItemModProjectile component = att.primaryMagazine.ammoType.GetComponent<ItemModProjectile>();
                Projectile projectile = component?.projectileObject?.Get()?.GetComponent<Projectile>();
                var dir = npc1.eyes.BodyForward();
                BasePlayer target = null;

                int numberofhits = 0;

                for (int i = 0; i < component.numProjectiles; i++)
                {
                    var spread = i > 1 ? AimConeUtil.GetModifiedAimConeDirection(component.projectileSpread + att.GetAimCone() / 2f * 1f, npc1.eyes.BodyForward(), true) : npc.GetAimDirection();

                    List<RaycastHit> list = Pool.GetList<RaycastHit>();
                    GamePhysics.TraceAll(new Ray(npc1.eyes.position, spread), 0f, list, 400f, 1219701505, QueryTriggerInteraction.UseGlobal);

                    for (int j = 0; j < list.Count; j++)
                    {
                        RaycastHit item = list[j];
                        BaseEntity entity = item.GetEntity();

                        if (entity == null)
                            continue;
                        target = entity as BasePlayer;

                        if (target != null && !npc1.IsVisibleAndCanSee(target.eyes.position))
                            break;

                        if (entity != held && !entity.EqualNetID(held))
                        {
                            var distance = Vector3.Distance(npc.transform.position, entity.transform.position);
                            if (AccuracyScaled(profile.Behaviour.Bot_Accuracy_Percent, distance) < bs.GetRand(1, 101))
                                continue;

                            HitInfo hitInfo = new HitInfo();
                            if (bs.configData.Global.NPCs_Damage_Armour && i == 0)
                                hitInfo.HitBone = (uint)(bs.GetRand(1, 100) < 97 ? 1031402764 : 698017942); //// Make headshot chance a profile option

                            hitInfo.Initiator = att.GetOwnerPlayer();
                            if (hitInfo.Initiator == null)
                                hitInfo.Initiator = att.GetParentEntity();

                            hitInfo.Weapon = att;
                            hitInfo.WeaponPrefab = att.gameManager.FindPrefab(att.PrefabName).GetComponent<AttackEntity>();
                            hitInfo.IsPredicting = false;

                            if (projectile != null)
                                hitInfo.DoHitEffects = projectile.doDefaultHitEffects;

                            hitInfo.DidHit = true;
                            hitInfo.ProjectileVelocity = npc1.eyes.BodyForward() * 300f;

                            if (att.MuzzlePoint != null)
                                hitInfo.PointStart = att.MuzzlePoint.position;

                            hitInfo.PointEnd = item.point;
                            hitInfo.HitPositionWorld = item.point;
                            hitInfo.HitNormalWorld = item.normal;
                            hitInfo.HitEntity = entity;
                            hitInfo.UseProtection = true;

                            if (projectile != null)
                                projectile.CalculateDamage(hitInfo, att.GetProjectileModifier(), 1f);

                            hitInfo.damageTypes.ScaleAll((bs.configData.Global.Smooth_Damage_Scale ? 1 : att.GetDamageScale(false)) * damageModifier * 0.2f * ((float)profile.Behaviour.Bot_Damage_Percent) / 50f);

                            if (bs.configData.Global.Reduce_Damage_Over_Distance)
                                hitInfo.damageTypes.ScaleAll(Reduction(distance, (int)currentRange));

                            entity.OnAttacked(hitInfo);
                            BasePlayer player = entity as BasePlayer;
                            if (player != null)
                            {
                                numberofhits++;
                                player.metabolism?.bleeding.Add(-(hitInfo.damageTypes.Total() * 0.2f));
                                if (profile.Behaviour.Victim_Bleed_Amount_Per_Hit == 0)
                                    hitInfo.damageTypes.types[6] = 0f;

                                else
                                {
                                    if (profile.Behaviour.Victim_Bleed_Amount_Max == 100)
                                        player.metabolism?.bleeding.Add(profile.Behaviour.Bleed_Amount_Is_Percent_Of_Damage ? (hitInfo.damageTypes.Total() / 100 * profile.Behaviour.Victim_Bleed_Amount_Per_Hit) : profile.Behaviour.Victim_Bleed_Amount_Per_Hit);
                                    else
                                    {
                                        var max = profile.Behaviour.Victim_Bleed_Amount_Max - player.metabolism.bleeding.value;
                                        if (max > 0)
                                            player.metabolism?.bleeding.Add(Mathf.Min(max, profile.Behaviour.Victim_Bleed_Amount_Per_Hit));
                                    }
                                }
                            }

                            if (entity is BasePlayer || entity is BaseNpc)
                            {
                                hitInfo.HitPositionLocal = entity.transform.InverseTransformPoint(hitInfo.HitPositionWorld);
                                hitInfo.HitNormalLocal = entity.transform.InverseTransformDirection(hitInfo.HitNormalWorld);
                                hitInfo.HitMaterial = StringPool.Get("Flesh");
                                Effect.server.ImpactEffect(hitInfo);
                            }

                            if (entity.ShouldBlockProjectiles())
                                break;
                        }
                    }
                    Pool.FreeList<RaycastHit>(ref list);
                    var cone = AimConeUtil.GetModifiedAimConeDirection(component.projectileSpread + att.GetAimCone() + att.GetAIAimcone() * 1f, npc1.eyes.BodyForward(), true);
                    att.CreateProjectileEffectClientside(component.projectileObject.resourcePath, npc1.eyes.position + npc1.eyes.BodyForward() * 3f, cone * component.projectileVelocity, UnityEngine.Random.Range(1, 100), null, att.IsSilenced(), false);
                    att.CreateProjectileEffectClientside(component.projectileObject.resourcePath, npc1.eyes.position + npc1.eyes.BodyForward() * 3f, cone * component.projectileVelocity, UnityEngine.Random.Range(1, 100), target?.net?.connection, att.IsSilenced(), true);
                }
            }

            float Reduction(float distance, int range)
            {
                if (range == 1 && distance > 20)
                    return Mathf.Max(0.1f, Mathf.Min(1, 1 - (distance - 20) / 100));
                if (range == 2 && distance > 40)
                    return Mathf.Max(0.1f, Mathf.Min(1, 1 - (distance - 40) / 100));
                if (range == 3 && distance > 100)
                    return Mathf.Max(0.1f, Mathf.Min(1, 1 - (distance - 100) / 300));
                return 1;
            }

            void FlameAttack(global::HumanNPC npc, BasePlayer t, FlameThrower ft)
            {
                if (t == null || ft == null)
                    return;

                if (Vector3.Distance(npc.transform.position, t.transform.position) > 4 || ft.HasAttackCooldown())
                {
                    Flame(false, ft);
                    return;
                }
                if (ft.ammo < 1)
                {
                    Flame(false, ft);
                    ft.ServerReload();
                }
                else if (!npc.IsReloading())
                {
                    if (ft.IsOnFire())
                        return;
                    Flame(true, ft);
                }
            }

            void Flame(bool on, FlameThrower ft)
            {
                npc.modelState.aiming = on;
                npc.SetPlayerFlag(BasePlayer.PlayerFlags.Aiming, on);
                ft.SetFlameState(on);
            }

            void MeleeAttack(BasePlayer t, BaseMelee melee)
            {
                if (t == null || melee == null)
                    return;

                if (melee as Chainsaw)
                {
                    (melee as Chainsaw).SetAttackStatus(true);
                    melee.Invoke(() => (melee as Chainsaw).SetAttackStatus(false), melee.attackSpacing + 0.5f);
                }

                Vector3 serverPos = t.ServerPosition - npc.ServerPosition;
                if (serverPos.magnitude > 0.001f)
                    npc.ServerRotation = Quaternion.LookRotation(serverPos.normalized);

                if (melee.NextAttackTime > Time.time || Vector3.Distance(npc.transform.position, t.transform.position) > 1.5)
                    return;

                npc.SignalBroadcast(BaseEntity.Signal.Attack, string.Empty, null);
                if (melee.swingEffect.isValid)
                    Effect.server.Run(melee.swingEffect.resourcePath, melee.transform.position, Vector3.forward, npc.net.connection, false);

                delay = bs.timer.Once(0.2f, () =>
                {
                    if (npc == null || profile == null || melee == null)
                        return;

                    Vector3 position = npc.eyes.position;
                    Vector3 direction = npc.eyes.BodyForward();
                    for (int index1 = 0; index1 < 2; ++index1)
                    {
                        List<RaycastHit> list = Pool.GetList<RaycastHit>();
                        GamePhysics.TraceAll(new Ray(position - direction * (index1 == 0 ? 0.0f : 0.2f), direction), index1 == 0 ? 0.0f : melee.attackRadius, list, melee.effectiveRange + 0.2f, 1219701521, QueryTriggerInteraction.UseGlobal);
                        bool flag = false;
                        for (int index2 = 0; index2 < list.Count; ++index2)
                        {
                            RaycastHit hit = list[index2];
                            BaseEntity e = hit.GetEntity();

                            if (e != null && npc != null && !e.EqualNetID(npc) && !npc.isClient)
                            {
                                BasePlayer p = e as BasePlayer;
                                float num = 0.0f;
                                foreach (Rust.DamageTypeEntry damageType in melee.damageTypes)
                                    if (damageType?.amount != null)
                                        num += damageType.amount;

                                var attinfo = new HitInfo(npc, e, Rust.DamageType.Slash, num * 0.2f * 0.75f * (float)profile.Behaviour.Melee_DamageScale);

                                e.OnAttacked(attinfo);
                                HitInfo info = Pool.Get<HitInfo>();
                                info.HitEntity = e;
                                info.HitPositionWorld = hit.point;
                                info.HitNormalWorld = -direction;
                                info.HitMaterial = e is BaseNpc || p != null ? StringPool.Get("Flesh") : StringPool.Get(hit.GetCollider().sharedMaterial != null ? hit.GetCollider().sharedMaterial.GetName() : "generic");
                                info.damageTypes.ScaleAll(((float)profile.Behaviour.Bot_Damage_Percent) / 50f);
                                melee.ServerUse_OnHit(info);
                                Effect.server.ImpactEffect(info);

                                if (p != null)
                                {
                                    p.metabolism?.bleeding.Add(-(attinfo.damageTypes.Total() * 0.2f));
                                    if (profile.Behaviour.Victim_Bleed_Amount_Per_Hit == 0)
                                        attinfo.damageTypes.types[6] = 0f;
                                    else
                                    {
                                        if (profile.Behaviour.Victim_Bleed_Amount_Max == 100)
                                            p.metabolism?.bleeding.Add(profile.Behaviour.Bleed_Amount_Is_Percent_Of_Damage ? (attinfo.damageTypes.Total() / 100 * profile.Behaviour.Victim_Bleed_Amount_Per_Hit) : profile.Behaviour.Victim_Bleed_Amount_Per_Hit);
                                        else
                                        {
                                            var max = profile.Behaviour.Victim_Bleed_Amount_Max - p.metabolism.bleeding.value;
                                            if (max > 0)
                                                p.metabolism?.bleeding.Add(Mathf.Min(max, profile.Behaviour.Victim_Bleed_Amount_Per_Hit));
                                        }
                                    }
                                }

                                Pool.Free<HitInfo>(ref info);
                                flag = true;
                                if (!(e != null) || e.ShouldBlockProjectiles())
                                    break;
                            }
                        }
                        Pool.FreeList<RaycastHit>(ref list);
                        if (flag)
                            break;
                    }
                    melee.StartAttackCooldown(melee.repeatDelay * 2f);
                });
            }
            #endregion
        }
        #endregion 

        #region Config
        private ConfigData configData;

        public class UI
        {
            public string ButtonColour = "0.7 0.32 0.17 1";
            public string ButtonColour2 = "0.4 0.1 0.1 1";
        }

        public class Global
        {
            public bool Allow_Parented_HackedCrates = false, Allow_HackableCrates_With_OwnerID = false, Allow_HackableCrates_From_CH47 = true, Allow_HackableCrates_At_Oilrig = true, Allow_All_Other_HackedCrates = true, Smooth_Damage_Scale = false, Allow_Oilrigs = false, NPCs_Assist_NPCs = true, Reduce_Damage_Over_Distance = false, Ignore_Factions = false, Scale_Meds_To_Health = false, Allow_Ai_Dormant = false, Prevent_Biome_Ai_Dormant = false, Limit_ShortRange_Weapon_Use = false, NPCs_Damage_Armour = true, Peacekeeper_Uses_Damage = false, RustRewards_Whole_Numbers = true, XPerience_Whole_Numbers = true, Announce_Toplayer = false, UseServerTime = false, Disable_Non_Parented_Custom_Profiles_After_Wipe = true;
            public int Parachute_From_Height = 200, Deaggro_Memory_Duration = 20, DayStartHour = 8, NightStartHour = 20, Show_Profiles_Seconds = 10;
            public bool Enable_Targeting_Hook = false, APC_Safe = true, Turret_Safe = false, Supply_Enabled, Ignore_Skinned_Supply_Grenades, Staggered_Despawn = false, Allow_AlphaLoot = true;
            public bool Suicide_Boom = true, Remove_Frankenstein_Parts = true, Remove_KeyCard = true, Pve_Safe = true;
            public int Remove_BackPacks_Percent = 100, Chute_Speed_Variation = 100, Chute_Fall_Speed = 50, Show_Spawns_Duration = 20;
            public Dictionary<int, int> ScaleRules = new Dictionary<int, int>();
        }

        class ConfigData
        {
            public string DataPrefix = "default";
            public UI UI = new UI();
            public Global Global = new Global();
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
            SaveConfig(configData);

            if (configData.Global.ScaleRules.Count() == 0)
                configData.Global.ScaleRules = new Dictionary<int, int>() { { 100, 100 }, { 75, 75 }, { 50, 50 }, { 25, 25 } };
        }

        protected override void LoadDefaultConfig()
        {
            LoadConfigVariables();
            Puts("Creating new config file.");
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #region Data  
        class StoredData
        {
            public Dictionary<string, Profile> Profiles = new Dictionary<string, Profile>();
            public Dictionary<string, ProfileRelocation> MigrationDataDoNotEdit = new Dictionary<string, ProfileRelocation>();
        }

        public class ProfileRelocation
        {
            public Vector3 ParentMonument = new Vector3();
            public Vector3 Offset = new Vector3();
        }

        public class TemplateData
        {
            public Dictionary<string, Profile> Templates = new Dictionary<string, Profile>();
        }

        class DefaultData
        {
            public Dictionary<string, Profile> Events = new Dictionary<string, Profile>() { { "AirDrop", new Profile(ProfileType.Event) }, { "CH47_Kill", new Profile(ProfileType.Event) }, { "PatrolHeli_Kill", new Profile(ProfileType.Event) }, { "APC_Kill", new Profile(ProfileType.Event) }, { "LockedCrate_Spawn", new Profile(ProfileType.Event) }, { "LockedCrate_HackStart", new Profile(ProfileType.Event) } };
            public Dictionary<string, Profile> Monuments = bs.GotMonuments;
            public Dictionary<string, Profile> Biomes = new Dictionary<string, Profile>() { { "BiomeArid", new Profile(ProfileType.Biome) }, { "BiomeTemperate", new Profile(ProfileType.Biome) }, { "BiomeTundra", new Profile(ProfileType.Biome) }, { "BiomeArctic", new Profile(ProfileType.Biome) } };
        }

        class SpawnsData
        {
            public Dictionary<string, List<SpawnData>> CustomSpawnLocations = new Dictionary<string, List<SpawnData>>();
        }

        public class SpawnData
        {
            public SpawnData(Profile p)
            {
                if (p != null)
                {
                    Kits = p.Spawn.Kit.ToList();
                    Health = p.Spawn.BotHealth;
                    Stationary = p.Spawn.Stationary;
                    RoamRange = p.Behaviour.Roam_Range;
                }
            }
            public bool UseOverrides = false;
            public Vector3 loc;
            public float rot;
            public List<string> Kits;
            public int Health;
            public bool Stationary;
            public int RoamRange;
        }

        StoredData storedData = new StoredData();
        DefaultData defaultData;
        TemplateData templateData;
        SpawnsData spawnsData = new SpawnsData();

        void SaveSpawns() => Interface.Oxide.DataFileSystem.WriteObject($"BotReSpawn/{configData.DataPrefix}-SpawnsData", spawnsData);
        void SaveData()
        {
            if (!loaded)
                return;

            storedData.Profiles = storedData.Profiles.OrderBy(x => x.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
            defaultData.Monuments = defaultData.Monuments.OrderBy(x => x.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
            Interface.Oxide.DataFileSystem.WriteObject($"BotReSpawn/{configData.DataPrefix}-CustomProfiles", storedData);
            Interface.Oxide.DataFileSystem.WriteObject($"BotReSpawn/{configData.DataPrefix}-DefaultProfiles", defaultData);
            Interface.Oxide.DataFileSystem.WriteObject($"BotReSpawn/{configData.DataPrefix}-SpawnsData", spawnsData);
            Interface.Oxide.DataFileSystem.WriteObject($"BotReSpawn/TemplateData", templateData);
        }

        void ReloadData(string profile, bool UI, object AutoSpawn)
        {
            if (UI)
                SaveData();

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>($"BotReSpawn/{configData.DataPrefix}-CustomProfiles");
            defaultData = Interface.Oxide.DataFileSystem.ReadObject<DefaultData>($"BotReSpawn/{configData.DataPrefix}-DefaultProfiles");
            templateData = Interface.Oxide.DataFileSystem.ReadObject<TemplateData>("BotReSpawn/TemplateData");

            foreach (var spawner in Spawners[profile])
                DestroySpawnGroups(spawner.go);

            if (storedData.Profiles.ContainsKey(profile))
            {
                Profiles.Remove(profile);
                AddData(profile, storedData.Profiles[profile]);
                SaveData();
            }

            Profile prof = GetProfile(profile);

            if (prof != null)
            {
                if (AutoSpawn != null)
                    prof.Spawn.AutoSpawn = (bool)AutoSpawn;

                prof.Other.Location = Profiles[profile].Other.Location;
                Profiles[profile] = prof;
                GameObject go = Spawners[profile][0].go;
                AddProfile(go, profile, prof, go.transform.position);
            }

            timer.Once(1f, () => CreateSpawnGroups(profile));
        }

        public enum ShouldAttack { Ignore, Defend, Attack }
        public enum ProfileType { Monument, Custom, Biome, Event }
        public enum FStein { None, Light, Medium, Heavy }

        public class Profile
        {
            public Profile Clone(Profile source, bool templatesave)
            {
                var serialized = JsonConvert.SerializeObject(this);
                var deser = JsonConvert.DeserializeObject<Profile>(serialized);
                deser.Other.Parent_Monument = templatesave ? string.Empty : source.Other.Parent_Monument;
                deser.Other.Location = templatesave ? Vector3.zero : source.Other.Location;
                deser.type = source.type;
                return deser;
            }

            public Profile(ProfileType t)
            {
                type = t;
                Spawn = new _Spawn(t);
                Behaviour = new _Behaviour(t);
                Death = new _Death(t);
                Other = new _Other(t);
            }

            public ProfileType type;
            [JsonIgnore] public float NextProfileThrow = Time.time;
            public _Spawn Spawn;
            public _Behaviour Behaviour;
            public _Death Death;
            public _Other Other;

            public bool ShouldSerializetype()
            {
                Spawn.type = type;
                Behaviour.type = type;
                Death.type = type;
                Other.type = type;
                return true;
            }

            public class Base
            {
                [JsonIgnore] public ProfileType type;
            }

            public class _Spawn : Base
            {
                public _Spawn(ProfileType t) { type = t; }
                public bool AutoSpawn;
                public int Radius = 100;
                public List<string> BotNames = new List<string>();
                public string BotNamePrefix = String.Empty;
                public bool Keep_Default_Loadout;
                public List<string> Kit = new List<string>();
                public int Day_Time_Spawn_Amount = 5;
                public int Night_Time_Spawn_Amount = 0;
                public bool Announce_Spawn;
                public string Announcement_Text = String.Empty;
                public int BotHealth = 100;
                public bool Stationary;
                public bool UseCustomSpawns;
                public bool ChangeCustomSpawnOnDeath;
                public bool Scale_NPC_Count_To_Player_Count;
                public FStein FrankenStein_Head = FStein.None;
                public FStein FrankenStein_Torso = FStein.None;
                public FStein FrankenStein_Legs = FStein.None;

                public bool ShouldSerializeRadius() => type != ProfileType.Biome;
                public bool ShouldSerializeStationary() => type != ProfileType.Event;
                public bool ShouldSerializeUseCustomSpawns() => type != ProfileType.Event && type != ProfileType.Biome;
                public bool ShouldSerializeChangeCustomSpawnOnDeath() => type != ProfileType.Event && type != ProfileType.Biome;
            }

            public class _Behaviour : Base
            {
                public _Behaviour(ProfileType t) { type = t; }
                public int Roam_Range = 40;
                public int Aggro_Range = 30;
                public int DeAggro_Range = 40;
                public bool Peace_Keeper = true;
                public int Bot_Accuracy_Percent = 100;
                public int Bot_Damage_Percent = 50;
                public int Running_Speed_Booster = 10;
                public int Roam_Pause_Length = 0;
                public bool AlwaysUseLights;
                public bool Ignore_All_Players = false;
                public bool Ignore_Sleepers = true;
                public bool Target_Noobs = false;
                public bool NPCs_Attack_Animals = false;
                public bool Friendly_Fire_Safe = false;
                public double Melee_DamageScale = 1.0;
                public double RangeWeapon_DamageScale = 1.0;
                public double Rocket_DamageScale = 1.0;
                public int Assist_Sense_Range = 30;
                public int Victim_Bleed_Amount_Per_Hit = 1;
                public int Victim_Bleed_Amount_Max = 100;
                public bool Bleed_Amount_Is_Percent_Of_Damage = false;
                public ShouldAttack Target_ZombieHorde = 0;
                public ShouldAttack Target_HumanNPC = 0;
                public ShouldAttack Target_Other_Npcs = 0;
                public bool Respect_Safe_Zones = true;
                public int Faction = 0;
                public int SubFaction = 0;
                public int Dont_Fire_Beyond_Distance = 0;
            }

            public class _Death : Base
            {
                public _Death(ProfileType t) { type = t; }
                public int Spawn_Hackable_Death_Crate_Percent;
                public string Death_Crate_CustomLoot_Profile = "";
                public int Death_Crate_LockDuration = 10;
                public int Corpse_Duration = 1;
                public int Weapon_Drop_Percent = 0;
                public int Min_Weapon_Drop_Condition_Percent = 50;
                public int Max_Weapon_Drop_Condition_Percent = 100;
                public int Wipe_Main_Percent = 0;
                public int Wipe_Belt_Percent = 100;
                public int Wipe_Clothing_Percent = 100;
                public int Allow_Rust_Loot_Percent = 100;
                public string Rust_Loot_Source = "Default NPC";
                //public bool Use_AlphaLoot = false; 
                public int Respawn_Timer = 1;
                public double RustRewardsValue = 0.0;
                public double XPerienceValue = 0.0;
                public bool ShouldSerializeRespawn_Timer() => type != ProfileType.Event;
            }

            public class _Other : Base
            {
                public _Other(ProfileType t) { type = t; }
                public bool Chute;
                public bool Invincible_Whilst_Chuting = false;
                public bool SamSite_Safe_Whilst_Chuting = true;
                public int Backpack_Duration = 10;
                public int Suicide_Timer = 5;
                public bool Die_Instantly_From_Headshot = false;
                public bool Require_Two_Headshots = false;
                public bool Fire_Safe = true;
                public int Grenade_Precision_Percent = 50;
                public List<string> Instant_Death_From_Headshot_Allowed_Weapons = new List<string>();
                public bool Disable_Radio = true;
                public Vector3 Location;
                public string Parent_Monument = String.Empty;
                public bool Use_Map_Marker = false;
                public bool Always_Show_Map_Marker = false;
                public bool MurdererSound = false;
                public int Immune_From_Damage_Beyond = 400;
                public bool Short_Roam_Vision = false;
                public bool Off_Terrain = false;
                public bool Block_Event_LockedCrate_Spawn = false;
                public bool Block_Event_LockedCrate_HackStart = false;
                public bool Block_Event_APC_Kill = false;
                public bool Block_Event_PatrolHeli_Kill = false;
                public bool Block_Event_CH47_Kill = false;
                public int Block_Event_Here_Radius = 100;
                public bool ShouldSerializeBlock_Event_LockedCrate_Spawn() => type == ProfileType.Monument;
                public bool ShouldSerializeBlock_Event_LockedCrate_HackStart() => type == ProfileType.Monument;
                public bool ShouldSerializeBlock_Event_APC_Kill() => type == ProfileType.Monument;
                public bool ShouldSerializeBlock_Event_PatrolHeli_Kill() => type == ProfileType.Monument;
                public bool ShouldSerializeBlock_Event_CH47_Kill() => type == ProfileType.Monument;
                public bool ShouldSerializeBlock_Event_Here_Radius() => type == ProfileType.Monument;
                public bool ShouldSerializeLocation() => type == ProfileType.Custom;
                public bool ShouldSerializeParent_Monument() => type == ProfileType.Custom;
            }
        }
        #endregion

        #region Messages     
        readonly Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"Title", "BotReSpawn : " },
            {"customsaved", "Custom Location Saved @ {0}" },
            {"ProfileMoved", "Custom Location {0} has been moved to your current position." },
            {"ParentSelected", "Parent Monument {0} set for profile {1}." },
            {"nonpc", "No BotReSpawn npc found directly in front of you." },
            {"noNavHere", "No navmesh was found at this location.\nConsider removing this point or using Stationary : true." },
            {"nospawns", "No custom spawn points were found for profile - {0}." },
            {"removednum", "Removed point {0} from {1}." },
            {"movedspawn", "Moved point {0} in {1}." },
            {"notthatmany", "Number of spawn points in {0} is less than {1}." },
            {"alreadyexists", "Custom Location already exists with the name {0}." },
            {"customremoved", "Custom Location {0} Removed." },
            {"deployed", "'{0}' bots deployed to {1}." },
            {"noprofile", "There is no profile by that name in default or custom profiles jsons." },
            {"nokits", "Kits is not installed but you have declared custom kits at {0}." },
            {"noWeapon", "A bot at {0} has no weapon. Check your kit {1} for a valid bullet or melee weapon." },
            {"numberOfBot", "There is {0} spawned bot alive." },
            {"numberOfBots", "There are {0} spawned bots alive." },
            {"dupID", "Duplicate userID save attempted. Please notify author." },
            {"NoBiomeSpawn", "Failed to find spawnpoints at {0}. Consider reducing npc numbers, or using custom profiles." },
            {"ToPlayer", "{0} npcs  have been sent to {1}" }
        };
        #endregion

        #region CUI
        const string Font = "robotocondensed-regular.ttf";
        public List<string> ValidKits = new List<string>();
        void OnPlayerDisconnected(BasePlayer player) => DestroyMenu(player, true);

        void DestroyMenu(BasePlayer player, bool all)
        {
            if (all)
                CuiHelper.DestroyUi(player, "BSBGUI");
            CuiHelper.DestroyUi(player, "BSKitsUI");
            CuiHelper.DestroyUi(player, "BSSpawnsUI");
            CuiHelper.DestroyUi(player, "BSMainUI");
            CuiHelper.DestroyUi(player, "BSBSOverridesUI");
            CuiHelper.DestroyUi(player, "BSUIToPlayerSelect");
            CuiHelper.DestroyUi(player, "BSShowParentsUI");
            CuiHelper.DestroyUi(player, "BSShowLootUI");
            CuiHelper.DestroyUi(player, "BSTemplatesUI");
            CuiHelper.DestroyUi(player, "BSDocs");
        }

        void BSDocs(BasePlayer player, string docname, string doc)
        {
            if (player == null || configData == null)
                return;

            CuiHelper.DestroyUi(player, "BSDocs");

            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = "0.1 0.1 0.1 0.99" }, RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.9 0.95" }, CursorEnabled = true }, "Overlay", "BSDocs");
            elements.Add(new CuiButton { Button = { Color = "0 0 0 1" }, RectTransform = { AnchorMin = $"0 0.95", AnchorMax = $"0.999 1" }, Text = { Text = string.Empty } }, mainName);
            elements.Add(new CuiButton { Button = { Color = "0 0 0 1" }, RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.999 0.05" }, Text = { Text = string.Empty } }, mainName);
            elements.Add(new CuiLabel { Text = { Text = "BotReSpawn", FontSize = 20, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.2 0.95", AnchorMax = "0.8 1" } }, mainName);
            elements.Add(new CuiLabel { Text = { Text = $"{docname.Replace("_", " ")}", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.2 0.75", AnchorMax = "0.8 0.8" } }, mainName);
            elements.Add(new CuiLabel { Text = { Text = $"{doc}", FontSize = 14, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.2 0.05", AnchorMax = "0.8 0.95" } }, mainName);
            elements.Add(new CuiButton { Button = { Command = "CloseDocs", Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" }, Text = { Text = "", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            CuiHelper.AddUi(player, elements);
        }

        [ConsoleCommand("BSDocs")]
        private void BSDocs(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            if (Docs.ContainsKey(arg.Args[0]))
                BSDocs(arg.Player(), arg.Args[0], Docs[arg.Args[0]]);
        }

        [ConsoleCommand("CloseDocs")]
        private void CloseDocs(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            CuiHelper.DestroyUi(arg.Player(), "BSDocs");
        }

        void BSBGUI(BasePlayer player)
        {
            if (player == null || configData == null)
                return;
            DestroyMenu(player, true);

            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = "0.1 0.1 0.1 0.94" }, RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.9 0.95" }, CursorEnabled = true }, "Overlay", "BSBGUI");
            elements.Add(new CuiButton { Button = { Color = "0 0 0 1" }, RectTransform = { AnchorMin = $"0 0.95", AnchorMax = $"0.999 1" }, Text = { Text = string.Empty } }, mainName);
            elements.Add(new CuiButton { Button = { Color = "0 0 0 1" }, RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.999 0.05" }, Text = { Text = string.Empty } }, mainName);
            elements.Add(new CuiButton { Button = { Command = "CloseBS", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = "0.955 0.96", AnchorMax = "0.99 0.99" }, Text = { Text = "X", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            elements.Add(new CuiLabel { Text = { Text = "BotReSpawn", FontSize = 20, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.2 0.95", AnchorMax = "0.8 1" } }, mainName);
            CuiHelper.AddUi(player, elements);
        }

        void BSMainUI(BasePlayer player, string tab, string profile = "", string sub = "", int page = 1)
        {
            if (configData == null || defaultData == null)
            {
                MsgUI(player, "BotReSpawn is not initialised yet.", 3f);
                return;
            }
            DestroyMenu(player, false);

            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0.1 0", AnchorMax = "0.9 0.9" }, CursorEnabled = true }, "Overlay", "BSMainUI");
            elements.Add(new CuiElement { Parent = "BSMainUI", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });

            float top = 0.875f;
            float bottom = 0.9f;

            var data = tab == "1" ? defaultData.Events.Concat(defaultData.Biomes.Concat(defaultData.Monuments)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value) : storedData.Profiles;
            data = data.Where(x => Spawners.ContainsKey(x.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            bool odd = true;
            double left = 0;

            if (tab == string.Empty)
            {
                elements.Add(new CuiButton { Button = { Command = "", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0 0.95", AnchorMax = $"0.999 0.99" }, Text = { Text = $"BotReSpawn Settings", FontSize = 18, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                elements.Add(new CuiButton { Button = { Command = $"BSUIMain 0", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.45 0.775", AnchorMax = $"0.55 0.8" }, Text = { Text = $"Global settings", FontSize = 14, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSUIMain 1", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.45 0.725", AnchorMax = $"0.55 0.750" }, Text = { Text = $"Default profiles", FontSize = 14, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSUIMain 2", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.45 0.675", AnchorMax = $"0.55 0.700" }, Text = { Text = $"Custom profiles", FontSize = 14, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSUIShowAll", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.45 0.575", AnchorMax = $"0.55 0.600" }, Text = { Text = $"Show all profiles", FontSize = 14, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                if (Editing.ContainsKey(player.userID))
                {
                    string name = Editing[player.userID];
                    if (Profiles.ContainsKey(name))
                    {
                        elements.Add(new CuiLabel { Text = { Text = $"Addspawn command is enabled for profile {name}", FontSize = 14, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.35 0.425", AnchorMax = $"0.65 0.450" } }, mainName);
                        elements.Add(new CuiButton { Button = { Command = $"BSUIStopEdit", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.35 0.375", AnchorMax = $"0.65 0.400" }, Text = { Text = $"Stop editing {name}", FontSize = 14, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                        elements.Add(new CuiButton { Button = { Command = $"BSGotoProfile {(Profiles[name].type == ProfileType.Custom ? "2" : "1")} {RS(name)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.35 0.325", AnchorMax = $"0.65 0.350" }, Text = { Text = $"Go to settings for {name}", FontSize = 14, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                    }
                    else
                    {
                        Editing.Remove(player.userID);
                        TidyShowSpawns(player, false);
                    }
                }
            }
            else if (profile == string.Empty)
            {
                if (tab == "0")
                {
                    var conf = configData.Global;
                    elements.Add(new CuiButton { Button = { Command = "", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0 0.95", AnchorMax = $"0.999 0.99" }, Text = { Text = $"Global settings", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                    top += 0.05f;
                    bottom += 0.05f;
                    foreach (var setting in typeof(Global).GetFields().OrderBy(x => x.Name))
                    {
                        var cat = setting.GetValue(conf);

                        if (setting.FieldType == typeof(bool))
                        {
                            top -= 0.025f;
                            bottom -= 0.025f;
                            if (odd)
                                elements.Add(new CuiPanel { Image = { Color = $"0 0 0 0.8" }, RectTransform = { AnchorMin = $"0 {top}", AnchorMax = $"0.999 {bottom}" }, CursorEnabled = true }, mainName);

                            elements.Add(new CuiLabel { Text = { Text = $"{setting.Name.Replace("_", " ")}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.05 {top}", AnchorMax = $"0.3 {bottom}" } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSDocs {RS(setting.Name)}", Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"0.05 {top}", AnchorMax = $"0.3 {bottom}" }, Text = { Text = "", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSConfChangeBool {RS(setting.Name)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.35 {top + 0.003}", AnchorMax = $"0.45 {bottom - 0.003}" }, Text = { Text = $"{cat}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            odd = !odd;
                        }
                    }

                    top = 0.925f;
                    bottom = 0.95f;
                    foreach (var setting in typeof(Global).GetFields().OrderBy(x => x.Name).OrderByDescending(y => y.Name.Contains("Start")))
                    {
                        var cat = setting.GetValue(conf);

                        if (setting.FieldType == typeof(int))
                        {
                            top -= 0.025f;
                            bottom -= 0.025f;
                            elements.Add(new CuiLabel { Text = { Text = $"{setting.Name.Replace("_", " ")}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.5 {top}", AnchorMax = $"0.8 {bottom}" } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSDocs {RS(setting.Name)}", Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"0.5 {top}", AnchorMax = $"0.8 {bottom}" }, Text = { Text = "", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSConfChangeNum {RS(setting.Name)} false", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.85 {top + 0.003}", AnchorMax = $"0.87 {bottom - 0.003}" }, Text = { Text = "<", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            elements.Add(new CuiLabel { Text = { Text = $"{cat}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.875 {top + 0.003}", AnchorMax = $"0.925 {bottom - 0.003}" } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSConfChangeNum {RS(setting.Name)} true", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.93 {top + 0.003}", AnchorMax = $"0.95 {bottom - 0.003}" }, Text = { Text = ">", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                        }
                    }

                    top -= 0.05f;
                    bottom -= 0.05f;

                    elements.Add(new CuiLabel { Text = { Text = $"NPC Pop %", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.5 {top}", AnchorMax = $"0.8 {bottom}" } }, mainName);
                    elements.Add(new CuiButton { Button = { Command = $"BSDocs NPCPop%", Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"0.5 {top}", AnchorMax = $"0.8 {bottom}" }, Text = { Text = "", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                    elements.Add(new CuiLabel { Text = { Text = $"Server Pop", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.7 {top}", AnchorMax = $"1 {bottom}" } }, mainName);
                    foreach (var entry in configData.Global.ScaleRules)
                    {
                        top -= 0.025f;
                        bottom -= 0.025f;
                        elements.Add(new CuiLabel { Text = { Text = $"{entry.Key}%", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.5 {top + 0.003}", AnchorMax = $"0.525 {bottom - 0.003}" } }, mainName);
                        elements.Add(new CuiLabel { Text = { Text = $"of npcs will spawn if pop is at least", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.53 {top + 0.003}", AnchorMax = $"0.7 {bottom - 0.003}" } }, mainName);

                        elements.Add(new CuiButton { Button = { Command = $"BSChangePop false {entry.Key}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.7 {top + 0.003}", AnchorMax = $"0.72 {bottom - 0.003}" }, Text = { Text = "<", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                        elements.Add(new CuiLabel { Text = { Text = $"{entry.Value}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.725 {top + 0.003}", AnchorMax = $"0.775 {bottom - 0.003}" } }, mainName);
                        elements.Add(new CuiButton { Button = { Command = $"BSChangePop true {entry.Key}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.78 {top + 0.003}", AnchorMax = $"0.8 {bottom - 0.003}" }, Text = { Text = ">", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                    }
                }
                else
                {
                    elements.Add(new CuiButton { Button = { Command = "", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0 0.95", AnchorMax = $"0.999 0.99" }, Text = { Text = tab == "1" ? "Default Profiles" : "Custom Profiles", FontSize = 18, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                    int counter = -1;
                    foreach (var entry in data)
                    {
                        counter++;
                        if (counter >= page * 120 || counter < (page * 120) - 120)
                            continue;

                        if (counter > 0 && counter % 30 == 0)
                        {
                            top = 0.875f;
                            bottom = 0.9f;
                            left += 0.25;
                        }

                        if (odd && left == 0)
                            elements.Add(new CuiPanel { Image = { Color = $"0 0 0 0.8" }, RectTransform = { AnchorMin = $"0 {top}", AnchorMax = $"0.999 {bottom}" }, CursorEnabled = true }, mainName);

                        elements.Add(new CuiButton { Button = { Command = $"BSUI {tab} {RS(entry.Key)} 0 0", Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"{left + 0.05} {top}", AnchorMax = $"{left + 0.3} {bottom}" }, Text = { Text = $"{entry.Key}", Color = entry.Value.Spawn.AutoSpawn ? "0 1 0 1" : "1 1 1 1", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft } }, mainName);

                        top -= 0.025f;
                        bottom -= 0.025f;
                        odd = !odd;
                    }
                }
                elements.Add(new CuiButton { Button = { Command = "BSUIMain", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.065", AnchorMax = $"0.6 0.095" }, Text = { Text = $"Back", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                if (page > 1)
                    elements.Add(new CuiButton { Button = { Command = $"BSUIPage {tab} {page - 1}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.15 0.065", AnchorMax = $"0.3 0.095" }, Text = { Text = $"<-", FontSize = 18, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                if (data.Count > page * 120)
                    elements.Add(new CuiButton { Button = { Command = $"BSUIPage {tab} {page + 1}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.7 0.065", AnchorMax = $"0.85 0.095" }, Text = { Text = $"->", FontSize = 18, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            }
            else
            {
                var entry = data[RD(profile)];
                elements.Add(new CuiButton { Button = { Command = "", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0 0.95", AnchorMax = $"0.999 0.99" }, Text = { Text = $"{RD(profile)}", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                float l = 0.12f;
                float r = 0.27f;

                foreach (var category in typeof(Profile).GetFields())
                {
                    if (category.Name == "type" || category.Name == "NextProfileThrow")
                        continue;
                    if (sub == "0")
                        sub = category.Name;
                    elements.Add(new CuiButton { Button = { Command = $"BSUI {tab} {RS(profile)} {category.Name} 0", Color = configData.UI.ButtonColour2 }, RectTransform = { AnchorMin = $"{l} 0.91", AnchorMax = $"{r} 0.935" }, Text = { Text = $"{category.Name}", FontSize = 14, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                    l += 0.2f;
                    r += 0.2f;

                    if (category.Name != sub)
                        continue;

                    foreach (var setting in category.FieldType.GetFields())
                    {
                        var cat = category.GetValue(entry);
                        if (setting.Name == "type" || category.Name == "NextProfileThrow" || setting.FieldType == typeof(List<string>))
                            continue;

                        if ((entry.type == ProfileType.Biome || entry.type == ProfileType.Event) && setting.Name.Contains("CustomSpawn"))
                            continue;

                        if (entry.type == ProfileType.Biome && setting.Name == "Radius")
                            continue;

                        if (entry.type == ProfileType.Event && setting.Name == "Respawn_Timer")
                            continue;

                        if (entry.type != ProfileType.Monument && setting.Name.Contains("Block_Event"))
                            continue;

                        top -= 0.025f;
                        bottom -= 0.025f;

                        if (odd)
                            elements.Add(new CuiPanel { Image = { Color = $"0 0 0 0.8" }, RectTransform = { AnchorMin = $"0 {top}", AnchorMax = $"0.999 {bottom}" }, CursorEnabled = true }, mainName);

                        if (setting.FieldType == typeof(int))
                        {
                            elements.Add(new CuiLabel { Text = { Text = $"{setting.Name.Replace("_", " ")}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSDocs {RS(setting.Name)}", Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" }, Text = { Text = "", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSChangeNum {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)} false", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.25 {top + 0.003}", AnchorMax = $"0.27 {bottom - 0.003}" }, Text = { Text = "<", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            elements.Add(new CuiLabel { Text = { Text = $"{setting.GetValue(cat)}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.275 {top + 0.003}", AnchorMax = $"0.325 {bottom - 0.003}" } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSChangeNum {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)} true", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.33 {top + 0.003}", AnchorMax = $"0.35 {bottom - 0.003}" }, Text = { Text = ">", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                        }
                        else if (setting.FieldType == typeof(ShouldAttack) || setting.FieldType == typeof(FStein))
                        {
                            elements.Add(new CuiLabel { Text = { Text = $"{setting.Name.Replace("_", " ")}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSDocs {RS(setting.Name)}", Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" }, Text = { Text = "", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSChangeEnum {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)} false {Enum.GetNames(setting.FieldType).Length}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.25 {top + 0.003}", AnchorMax = $"0.27 {bottom - 0.003}" }, Text = { Text = "<", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            elements.Add(new CuiLabel { Text = { Text = $"{setting.GetValue(cat)}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.275 {top + 0.003}", AnchorMax = $"0.325 {bottom - 0.003}" } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSChangeEnum {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)} true {Enum.GetNames(setting.FieldType).Length}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.33 {top + 0.003}", AnchorMax = $"0.35 {bottom - 0.003}" }, Text = { Text = ">", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                        }
                        else if (setting.FieldType == typeof(double))
                        {
                            elements.Add(new CuiLabel { Text = { Text = $"{setting.Name.Replace("_", " ")}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSDocs {RS(setting.Name)}", Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" }, Text = { Text = "", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSChangeDouble {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)} false", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.25 {top + 0.003}", AnchorMax = $"0.27 {bottom - 0.003}" }, Text = { Text = "<", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            elements.Add(new CuiLabel { Text = { Text = $"{setting.GetValue(cat)}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.275 {top + 0.003}", AnchorMax = $"0.325 {bottom - 0.003}" } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSChangeDouble {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)} true", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.33 {top + 0.003}", AnchorMax = $"0.35 {bottom - 0.003}" }, Text = { Text = ">", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                        }
                        else if (setting.FieldType == typeof(bool))
                        {
                            elements.Add(new CuiLabel { Text = { Text = $"{setting.Name.Replace("_", " ")}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSDocs {RS(setting.Name)}", Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" }, Text = { Text = "", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSChangeBool {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.25 {top + 0.003}", AnchorMax = $"0.35 {bottom - 0.003}" }, Text = { Text = $"{setting.GetValue(cat)}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                        }
                        else if (setting.FieldType == typeof(string))
                        {
                            if (setting.Name == "Parent_Monument" && entry.type == ProfileType.Custom)
                            {
                                elements.Add(new CuiLabel { Text = { Text = $"{setting.Name.Replace("_", " ")}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" } }, mainName);
                                elements.Add(new CuiButton { Button = { Command = $"BSDocs {RS(setting.Name)}", Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" }, Text = { Text = "", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                                elements.Add(new CuiButton { Button = { Command = $"BSShowParents {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.25 {top + 0.003}", AnchorMax = $"0.35 {bottom - 0.003}" }, Text = { Text = $"{(setting.GetValue(cat).ToString() == string.Empty ? "Select" : setting.GetValue(cat))}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            }
                            else if (setting.Name == "Rust_Loot_Source")
                            {
                                elements.Add(new CuiLabel { Text = { Text = $"{setting.Name.Replace("_", " ")}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" } }, mainName);
                                elements.Add(new CuiButton { Button = { Command = $"BSDocs {RS(setting.Name)}", Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" }, Text = { Text = "", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                                elements.Add(new CuiButton { Button = { Command = $"BSShowLoot {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.25 {top + 0.003}", AnchorMax = $"0.35 {bottom - 0.003}" }, Text = { Text = $"{(setting.GetValue(cat).ToString() == string.Empty ? "Select" : setting.GetValue(cat))}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            }
                            else
                            {
                                elements.Add(new CuiLabel { Text = { Text = $"{setting.Name.Replace("_", " ")}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" } }, mainName);
                                elements.Add(new CuiButton { Button = { Command = $"BSDocs {RS(setting.Name)}", Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" }, Text = { Text = "", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                                elements.Add(new CuiLabel { Text = { Text = $"{(setting.Name == "Location" ? setting.GetValue(cat) : "Edit in json")}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.25 {top + 0.003}", AnchorMax = $"0.35 {bottom - 0.003}" } }, mainName);
                            }
                        }
                        else
                        {
                            elements.Add(new CuiLabel { Text = { Text = $"{setting.Name.Replace("_", "")}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSDocs {RS(setting.Name)}", Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" }, Text = { Text = "", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            elements.Add(new CuiLabel { Text = { Text = $"{(setting.Name == "Location" ? setting.GetValue(cat) : "Edit in json")}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.25 {top + 0.003}", AnchorMax = $"0.35 {bottom - 0.003}" } }, mainName);
                        }
                        odd = !odd;
                    }
                }

                if (data[RD(profile)].type == ProfileType.Custom || data[RD(profile)].type == ProfileType.Monument)
                {
                    elements.Add(new CuiButton { Button = { Command = $"BSUITPPlayer {RS(profile)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.08 0.14", AnchorMax = $"0.19 0.16" }, Text = { Text = "TP me there", FontSize = 12, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                    elements.Add(new CuiButton { Button = { Command = $"BSUIEditSpawns {RS(profile)} 0", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.21 0.14", AnchorMax = $"0.32 0.16" }, Text = { Text = "Edit Spawnpoints", FontSize = 12, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                    if (data[RD(profile)].type == ProfileType.Custom)
                        elements.Add(new CuiButton { Button = { Command = $"BSUIMoveProfile {RS(profile)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.21 0.11", AnchorMax = $"0.32 0.13" }, Text = { Text = "Move profile here", FontSize = 12, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                }

                elements.Add(new CuiButton { Button = { Command = $"BSUIReload {RS(profile)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.17", AnchorMax = $"0.6 0.19" }, Text = { Text = "Reload profile", FontSize = 12, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSUIEditKits {RS(profile)} -1 0", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.14", AnchorMax = $"0.6 0.16" }, Text = { Text = "Edit Kits", FontSize = 12, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSUIToPlayerSelect {RS(profile)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.11", AnchorMax = $"0.6 0.13" }, Text = { Text = "Send to player", FontSize = 12, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                elements.Add(new CuiButton { Button = { Command = $"BSCopy {tab} {RS(profile)} {sub}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.68 0.14", AnchorMax = $"0.79 0.16" }, Text = { Text = "Copy profile", FontSize = 12, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                if (Copy.ContainsKey(player.userID))
                    elements.Add(new CuiButton { Button = { Command = $"BSPaste {tab} {RS(profile)} {sub}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.68 0.11", AnchorMax = $"0.79 0.13" }, Text = { Text = $"Paste {Copy[player.userID]}", FontSize = 12, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                elements.Add(new CuiButton { Button = { Command = $"BSUISaveTemplate {RS(profile)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.81 0.14", AnchorMax = $"0.92 0.16" }, Text = { Text = "Save template", FontSize = 12, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                if (templateData.Templates.Count > 0)
                    elements.Add(new CuiButton { Button = { Command = $"BSUIViewTemplates {tab} {RS(profile)} {sub}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.81 0.11", AnchorMax = $"0.92 0.13" }, Text = { Text = "Load template", FontSize = 12, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                if (data[RD(profile)].type == ProfileType.Custom)
                    elements.Add(new CuiButton { Button = { Command = $"BSUIRemoveProfile {RS(profile)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.01 0.065", AnchorMax = $"0.16 0.095" }, Text = { Text = "Delete Profile", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                elements.Add(new CuiButton { Button = { Command = $"BSUIMain {tab}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.065", AnchorMax = $"0.6 0.095" }, Text = { Text = "Back", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

            }
            CuiHelper.AddUi(player, elements);
        }

        void SetProfile(string profile, Profile p)
        {
            switch (p.type)
            {
                case ProfileType.Monument:
                    defaultData.Monuments[profile] = p;
                    break;
                case ProfileType.Biome:
                    defaultData.Biomes[profile] = p;
                    break;
                case ProfileType.Event:
                    defaultData.Events[profile] = p;
                    break;
                case ProfileType.Custom:
                    storedData.Profiles[profile] = p;
                    break;
            }
        }

        void BSKitsUI(BasePlayer player, string profile = "", int spawnpoint = -1, int page = 0)
        {
            DestroyMenu(player, false);
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0.1 0", AnchorMax = "0.9 0.9" }, CursorEnabled = true }, "Overlay", "BSKitsUI");
            elements.Add(new CuiElement { Parent = "BSKitsUI", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            string text = $"Kits for {profile} - Use the numbers to balance probability when chosing multiple kits.";

            // Add label with instructions here.

            if (spawnpoint > -1)
                text += $" - spawnpoint {spawnpoint}";
            elements.Add(new CuiLabel { Text = { Text = text, FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0 0.9", AnchorMax = $"1 1" } }, mainName);

            float top = 0.925f;
            float bottom = 0.95f;
            double left = 0;

            List<string> k = spawnpoint == -1 ? GetProfile(profile).Spawn.Kit : spawnsData.CustomSpawnLocations[profile][spawnpoint].Kits;
            int num = 0;

            int from = page * 105;
            int to = ((page + 1) * 105) - 1;
            for (int i = from; i <= to; i++)
            {
                if (i >= ValidKits.Count)
                    break;

                if (i > from && (i - from) % 35 == 0)
                {
                    top = 0.925f;
                    bottom = 0.95f;
                    left += 0.33;
                }
                if (i - from > 104)
                    break;

                top -= 0.023f;
                bottom -= 0.023f;
                num = k.Where(x => x == ValidKits[i]).Count();
                if (spawnpoint > -1)
                {
                    elements.Add(new CuiLabel { Text = { Text = $"{ValidKits[i]}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"{left + 0.05} {top}", AnchorMax = $"{left + 0.25} {bottom}" } }, mainName);
                    elements.Add(new CuiButton { Button = { Command = $"BSChangeSPKit {RS(profile)} {i} {spawnpoint} {page} false", Color = num > 0 ? configData.UI.ButtonColour2 : configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.25} {top + 0.003}", AnchorMax = $"{left + 0.27} {bottom - 0.003}" }, Text = { Text = "<", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                    elements.Add(new CuiLabel { Text = { Text = $"{k.Where(x => x == ValidKits[i]).Count()}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"{left + 0.275} {top + 0.003}", AnchorMax = $"{left + 0.285} {bottom - 0.003}" } }, mainName);
                    elements.Add(new CuiButton { Button = { Command = $"BSChangeSPKit {RS(profile)} {i} {spawnpoint} {page} true", Color = num > 0 ? configData.UI.ButtonColour2 : configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.29} {top + 0.003}", AnchorMax = $"{left + 0.31} {bottom - 0.003}" }, Text = { Text = ">", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                }
                else
                {
                    elements.Add(new CuiLabel { Text = { Text = $"{ValidKits[i]}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"{left + 0.05} {top}", AnchorMax = $"{left + 0.25} {bottom}" } }, mainName);
                    elements.Add(new CuiButton { Button = { Command = $"BSChangeKit {RS(profile)} {i} -1 {page} false", Color = num > 0 ? configData.UI.ButtonColour2 : configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.25} {top + 0.003}", AnchorMax = $"{left + 0.27} {bottom - 0.003}" }, Text = { Text = "<", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                    elements.Add(new CuiLabel { Text = { Text = $"{num}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"{left + 0.275} {top + 0.003}", AnchorMax = $"{left + 0.285} {bottom - 0.003}" } }, mainName);
                    elements.Add(new CuiButton { Button = { Command = $"BSChangeKit {RS(profile)} {i} -1 {page} true", Color = num > 0 ? configData.UI.ButtonColour2 : configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.29} {top + 0.003}", AnchorMax = $"{left + 0.31} {bottom - 0.003}" }, Text = { Text = ">", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                }
            }

            if (page > 0)
                elements.Add(new CuiButton { Button = { Command = $"BSUIEditKits {RS(profile)} {spawnpoint} {page - 1}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.2 0.065", AnchorMax = $"0.3 0.095" }, Text = { Text = "<-", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            if (ValidKits.Count > to)
                elements.Add(new CuiButton { Button = { Command = $"BSUIEditKits {RS(profile)} {spawnpoint} {page + 1}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.7 0.065", AnchorMax = $"0.8 0.095" }, Text = { Text = "->", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);


            elements.Add(new CuiButton { Button = { Command = $"CloseExtra BSKitsUI {RS(profile)} {spawnpoint} {page}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.065", AnchorMax = $"0.6 0.095" }, Text = { Text = "Back", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            CuiHelper.AddUi(player, elements);
        }

        void BSSpawnsUI(BasePlayer player, string profile = "", int page = 0)
        {
            DestroyMenu(player, false);
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0.1 0", AnchorMax = "0.9 0.9" }, CursorEnabled = true }, "Overlay", "BSSpawnsUI");
            elements.Add(new CuiElement { Parent = "BSSpawnsUI", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            elements.Add(new CuiLabel { Text = { Text = $"Spawn points for {profile}", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0 0.9", AnchorMax = $"1 1" } }, mainName);

            float top = 0.875f;
            float bottom = 0.9f;
            double left = 0;

            var s = spawnsData.CustomSpawnLocations[profile];
            if (s.Count > 0)
            {
                s = s.GetRange(page * 54, Mathf.Min(s.Count() - page * 54, 54));

                if (s.Count() == 0)
                {
                    page--;
                    s = spawnsData.CustomSpawnLocations[profile];
                    s = s.GetRange(page * 54, Mathf.Min(s.Count() - page * 54, 54));
                }
            }

            if (s.Count < 27)
                left = 0.25;

            for (int i = 0; i < s.Count; i++)
            {
                if (i == 27)
                {
                    top = 0.875f;
                    bottom = 0.9f;
                    left = 0.5;
                }
                if (i > 53)
                    break;

                top -= 0.025f;
                bottom -= 0.025f;
                elements.Add(new CuiLabel { Text = { Text = $"{i + (page * 54)}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"{left + 0.08} {top}", AnchorMax = $"{left + 0.10} {bottom}" } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSUIMoveSpawn {RS(profile)} {i + (page * 54)} true {page}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.11} {top + 0.003}", AnchorMax = $"{left + 0.16} {bottom - 0.003}" }, Text = { Text = "Remove", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSUIMoveSpawn {RS(profile)} {i + (page * 54)} false {page}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.17} {top + 0.003}", AnchorMax = $"{left + 0.23} {bottom - 0.003}" }, Text = { Text = "Move here", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSEditOverRides {RS(profile)} {i + (page * 54)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.24} {top + 0.003}", AnchorMax = $"{left + 0.31} {bottom - 0.003}" }, Text = { Text = "Edit overrides", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            }

            elements.Add(new CuiButton { Button = { Command = $"BSUISetEditing {RS(profile)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.3 0.16", AnchorMax = $"0.49 0.19" }, Text = { Text = "Edit with console commands", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            elements.Add(new CuiButton { Button = { Command = $"BSUIAddSpawn {RS(profile)} {page}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.3 0.12", AnchorMax = $"0.49 0.15" }, Text = { Text = "Add spawnpoint here", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            elements.Add(new CuiButton { Button = { Command = $"BSUIShowSpawns {RS(profile)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.51 0.16", AnchorMax = $"0.7 0.19" }, Text = { Text = "Show all spawnpoints", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            elements.Add(new CuiButton { Button = { Command = $"BSUICheckNav", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.51 0.12", AnchorMax = $"0.7 0.15" }, Text = { Text = "Check for Navmesh", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

            if (page > 0)
                elements.Add(new CuiButton { Button = { Command = $"BSUIEditSpawns {RS(profile)} {page - 1}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.2 0.065", AnchorMax = $"0.3 0.095" }, Text = { Text = "<-", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            if (spawnsData.CustomSpawnLocations[profile].Count() > (page + 1) * 54)
                elements.Add(new CuiButton { Button = { Command = $"BSUIEditSpawns {RS(profile)} {page + 1}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.7 0.065", AnchorMax = $"0.8 0.095" }, Text = { Text = "->", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

            elements.Add(new CuiButton { Button = { Command = $"CloseExtra BSSpawnsUI {RS(profile)} -1", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.065", AnchorMax = $"0.6 0.095" }, Text = { Text = "Back", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

            CuiHelper.AddUi(player, elements);
        }

        void BSOverridesUI(BasePlayer player, string profile = "", int spawnpoint = 0)
        {
            CuiHelper.DestroyUi(player, "BSBSOverridesUI");
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = "0 0 0 0.96" }, RectTransform = { AnchorMin = "0.2 0.1", AnchorMax = "0.8 0.8" }, CursorEnabled = true }, "Overlay", "BSBSOverridesUI");
            elements.Add(new CuiElement { Parent = "BSBSOverridesUI", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            elements.Add(new CuiLabel { Text = { Text = $"Overrides for spawn point {spawnpoint}", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0 0.9", AnchorMax = $"1 1" } }, mainName);

            var s = spawnsData.CustomSpawnLocations[profile][spawnpoint];
            elements.Add(new CuiLabel { Text = { Text = $"Enable overrides", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.4 0.76", AnchorMax = $"0.5 0.8" } }, mainName);
            elements.Add(new CuiButton { Button = { Command = $"BSChangeOverRides {RS(profile)} {spawnpoint} 0 true", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.5 0.76", AnchorMax = $"0.6 0.8" }, Text = { Text = s.UseOverrides.ToString(), FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

            if (s.UseOverrides)
            {
                elements.Add(new CuiLabel { Text = { Text = $"Stationary", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.4 0.7", AnchorMax = $"0.5 0.74" } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSChangeOverRides {RS(profile)} {spawnpoint} 1 true", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.5 0.7", AnchorMax = $"0.6 0.74" }, Text = { Text = s.Stationary.ToString(), FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                elements.Add(new CuiLabel { Text = { Text = $"Health", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.4 0.64", AnchorMax = $"0.5 0.68" } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSChangeOverRides {RS(profile)} {spawnpoint} 2 false", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.5 0.64", AnchorMax = $"0.53 0.68" }, Text = { Text = "<", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiLabel { Text = { Text = s.Health.ToString(), FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.54 0.64", AnchorMax = $"0.56 0.68" } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSChangeOverRides {RS(profile)} {spawnpoint} 2 true", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.57 0.64", AnchorMax = $"0.6 0.68" }, Text = { Text = ">", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                elements.Add(new CuiLabel { Text = { Text = $"Roam range", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.4 0.58", AnchorMax = $"0.5 0.62" } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSChangeOverRides {RS(profile)} {spawnpoint} 3 false", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.5 0.58", AnchorMax = $"0.53 0.62" }, Text = { Text = "<", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiLabel { Text = { Text = s.RoamRange.ToString(), FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.54 0.58", AnchorMax = $"0.56 0.62" } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSChangeOverRides {RS(profile)} {spawnpoint} 3 true", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.57 0.58", AnchorMax = $"0.6 0.62" }, Text = { Text = ">", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                elements.Add(new CuiButton { Button = { Command = $"BSUIEditSPKits {RS(profile)} {spawnpoint} 0", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.52", AnchorMax = $"0.6 0.56" }, Text = { Text = "Kits", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            }

            elements.Add(new CuiButton { Button = { Command = $"BSCloseOverrides", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.015", AnchorMax = $"0.6 0.05" }, Text = { Text = "Save", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            CuiHelper.AddUi(player, elements);
        }


        void BSToPlayerUI(BasePlayer player, string profile)
        {
            CuiHelper.DestroyUi(player, "BSUIToPlayerSelect");
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = "0 0 0 0.98" }, RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.9 0.9" }, CursorEnabled = true }, "Overlay", "BSUIToPlayerSelect");
            elements.Add(new CuiElement { Parent = "BSUIToPlayerSelect", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            elements.Add(new CuiLabel { Text = { Text = $"Spawn {profile} npcs near a player.", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0 0.9", AnchorMax = $"1 1" } }, mainName);

            float top = 0.925f, bottom = 0.95f;
            double left = 0;
            bool odd = true;

            var players = BasePlayer.activePlayerList;
            if (players.Count < 34)
                left = 0.4;

            for (int i = 0; i < players.Count; i++)
            {
                if (i == 165)
                    break;

                if (i > 0 && i % 33 == 0 && left < 0.79)
                {
                    top = 0.925f;
                    bottom = 0.95f;
                    left += 0.2;
                }

                top -= 0.025f;
                bottom -= 0.025f;
                if (odd && left == 0)
                    elements.Add(new CuiPanel { Image = { Color = $"0 0 0 0.8" }, RectTransform = { AnchorMin = $"0 {top}", AnchorMax = $"0.999 {bottom - 0.005}" }, CursorEnabled = true }, mainName);

                elements.Add(new CuiButton { Button = { Command = $"BSUIToPlayer {RS(profile)} {players[i].userID} ", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.02} {top}", AnchorMax = $"{left + 0.18} {bottom - 0.005}" }, Text = { Text = players[i].displayName, FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            }
            elements.Add(new CuiButton { Button = { Command = $"BSUICloseToPlayer", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.015", AnchorMax = $"0.6 0.05" }, Text = { Text = "Close", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            CuiHelper.AddUi(player, elements);
        }

        void BSShowParentsUI(BasePlayer player, string tab, string profile, string category)
        {
            CuiHelper.DestroyUi(player, "BSShowParentsUI");
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = "0 0 0 0.98" }, RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.9 0.9" }, CursorEnabled = true }, "Overlay", "BSShowParentsUI");
            elements.Add(new CuiElement { Parent = "BSShowParentsUI", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            elements.Add(new CuiLabel { Text = { Text = $"Select parent monument for {profile}.", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0 0.9", AnchorMax = $"1 1" } }, mainName);

            float top = 0.925f;
            float bottom = 0.95f;
            double left = 0;
            bool odd = true;

            var mons = Spawners.Where(x => x.Value[0].profile.type == ProfileType.Monument).ToList();
            for (int i = 0; i < mons.Count(); i++)
            {
                if (i == 165)
                    break;

                if (i > 0 && i % 33 == 0 && left < 0.79)
                {
                    top = 0.925f;
                    bottom = 0.95f;
                    left += 0.2;
                }

                top -= 0.025f;
                bottom -= 0.025f;
                if (odd && left == 0)
                    elements.Add(new CuiPanel { Image = { Color = $"0 0 0 0.8" }, RectTransform = { AnchorMin = $"0 {top}", AnchorMax = $"0.999 {bottom - 0.005}" }, CursorEnabled = true }, mainName);

                elements.Add(new CuiButton { Button = { Command = $"BSSelectParent {tab} {RS(profile)} {RS(category)} {RS(mons[i].Key)} ", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.02} {top}", AnchorMax = $"{left + 0.18} {bottom - 0.005}" }, Text = { Text = mons[i].Key, FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            }
            elements.Add(new CuiButton { Button = { Command = $"BSUICloseParentMonument", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.015", AnchorMax = $"0.6 0.05" }, Text = { Text = "Close", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            CuiHelper.AddUi(player, elements);
        }

        void BSShowLootUI(BasePlayer player, string tab, string profile, string category)
        {
            CuiHelper.DestroyUi(player, "BSShowLootUI");
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = "0 0 0 0.98" }, RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.9 0.9" }, CursorEnabled = true }, "Overlay", "BSShowLootUI");
            elements.Add(new CuiElement { Parent = "BSShowLootUI", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            elements.Add(new CuiLabel { Text = { Text = $"Select vanilla loot table for {profile}.", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0 0.9", AnchorMax = $"1 1" } }, mainName);

            float top = 0.925f;
            float bottom = 0.95f;
            double left = 0;
            bool odd = true;

            var containers = Containers.Where(x => x.Key == "Default NPC" || x.Key == "ScarecrowNPC" || x.Key == "Random" || x.Value != null).ToList().OrderBy(x => x.Key).ToList();
            for (int i = 0; i < containers.Count(); i++)
            {
                if (i > 0 && i % 32 == 0)
                {
                    top = 0.925f;
                    bottom = 0.95f;
                    left += 0.2;
                }

                top -= 0.025f;
                bottom -= 0.025f;
                if (odd && left == 0)
                    elements.Add(new CuiPanel { Image = { Color = $"0 0 0 0.8" }, RectTransform = { AnchorMin = $"0 {top}", AnchorMax = $"0.999 {bottom - 0.005}" }, CursorEnabled = true }, mainName);

                elements.Add(new CuiButton { Button = { Command = $"BSSelectLoot {tab} {RS(profile)} {RS(category)} {RS(containers[i].Key)} ", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.02} {top}", AnchorMax = $"{left + 0.18} {bottom - 0.005}" }, Text = { Text = containers[i].Key, FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            }
            elements.Add(new CuiButton { Button = { Command = $"BSUICloseLoot", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.015", AnchorMax = $"0.6 0.05" }, Text = { Text = "Close", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            CuiHelper.AddUi(player, elements);
        }

        void BSTemplatesUI(BasePlayer player, string tab, string profile = "", string sub = "", int page = 1)
        {
            DestroyMenu(player, false);
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0.1 0", AnchorMax = "0.9 0.9" }, CursorEnabled = true }, "Overlay", "BSTemplatesUI");
            elements.Add(new CuiElement { Parent = "BSTemplatesUI", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            elements.Add(new CuiLabel { Text = { Text = "Profile templates", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0 0.9", AnchorMax = $"1 1" } }, mainName);

            float top = 0.925f;
            float bottom = 0.95f;
            double left = 0;
            int i = 0;

            foreach (var entry in templateData.Templates)
            {
                if (i > 0 && i % 35 == 0)
                {
                    top = 0.925f;
                    bottom = 0.95f;
                    left += 0.25;
                }
                if (i > 139)
                    break;

                top -= 0.023f;
                bottom -= 0.023f;
                elements.Add(new CuiButton { Button = { Command = $"BSUILoadTemplate {tab} {RS(profile)} {sub} {RS(entry.Key)} ", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.05} {top + 0.003}", AnchorMax = $"{left + 0.2} {bottom - 0.003}" }, Text = { Text = entry.Key, FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                i++;
            }

            elements.Add(new CuiButton { Button = { Command = $"BSUI {tab} {RS(profile)} {sub}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.065", AnchorMax = $"0.6 0.095" }, Text = { Text = "Back", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            CuiHelper.AddUi(player, elements);
        }
        #endregion

        #region UICommands
        void MsgUI(BasePlayer player, string message, float duration = 1.5f)
        {
            CuiHelper.DestroyUi(player, "msgui");
            timer.Once(duration, () =>
            {
                if (player != null)
                    CuiHelper.DestroyUi(player, "msgui");
            });
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { FadeIn = 0.3f, Color = $"0.1 0.1 0.1 0.9" }, RectTransform = { AnchorMin = "0.1 0.63", AnchorMax = "0.9 0.77" }, CursorEnabled = false, FadeOut = 0.3f }, "Overlay", "msgui");
            elements.Add(new CuiLabel { FadeOut = 0.5f, Text = { FadeIn = 0.5f, Text = message, Color = "1 1 1 1", FontSize = 26, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, mainName);

            CuiHelper.AddUi(player, elements);
        }

        [ConsoleCommand("BSCopy")]
        private void BSCopy(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            Copy[arg.Player().userID] = RD(arg.Args[1]);
            BSMainUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2], 0);
        }

        [ConsoleCommand("BSPaste")]
        private void BSPaste(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            SetProfile(RD(arg.Args[1]), GetProfile(Copy[arg.Player().userID]).Clone(GetProfile(RD(arg.Args[1])), false));
            BSMainUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2], 0);
        }

        [ConsoleCommand("BSUICloseToPlayer")]
        private void BSUICloseToPlayer(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                CuiHelper.DestroyUi(arg.Player(), "BSUIToPlayerSelect");
        }

        [ConsoleCommand("BSUIToPlayerSelect")]
        private void BSUIToPlayerSelect(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                BSToPlayerUI(arg.Player(), arg.Args[0]);
        }

        [ConsoleCommand("BSUIToPlayer")]
        private void BSUIToPlayer(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;

            BasePlayer target = GetPlayer(arg.Args[1]);
            if (target == null)
                return;

            if (!SpawnToPlayer(target, RD(arg.Args[0]), -1))
            {
                MsgUI(arg.Player(), lang.GetMessage("noprofile", this));
                SendReply(arg.Player(), TitleText + lang.GetMessage("noprofile", this));
            }
            CuiHelper.DestroyUi(arg.Player(), "BSUIToPlayerSelect");
        }

        [ConsoleCommand("botrespawn")]
        private void botrespawn(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !HasPermission(arg.Player().UserIDString, permAllowed) && !arg.Player().IsAdmin)
                return;
            if (arg?.Args == null)
                return;

            if (arg.Args.Length > 2 && arg.Args[0] == "toplayer")
            {
                int num = -1;
                if (arg.Args.Length == 4)
                    int.TryParse(arg.Args[3], out num);
                BasePlayer target = GetPlayer(arg.Args[1]);
                if (target == null)
                    return;

                if (!SpawnToPlayer(target, arg.Args[2], num))
                    Puts(lang.GetMessage("noprofile", this));
            }

            if (arg.Args.Length == 2)
            {
                if (!Profiles.ContainsKey(arg.Args[1]))
                    return;

                if (arg.Args[0] == "tempspawn")
                {
                    var prof = Profiles[arg.Args[1]];
                    AddGroupSpawn(prof.Other.Location, arg.Args[1], arg.Args[1], 0);
                    Puts($"Spawned temporary command receieved for profile {arg.Args[1]}.");
                }
                else if (arg.Args[0] == "disable" || arg.Args[0] == "enable")
                {
                    ReloadData(arg.Args[1], false, arg.Args[0] == "disable" ? false : true);
                }
            }
        }

        BasePlayer GetPlayer(string name)
        {
            BasePlayer target = FindPlayerByName(name);
            if (target == null)
                target = BasePlayer.Find(name);
            if (target == null)
                Puts($"No player found for {name}");
            return target;
        }

        bool SpawnToPlayer(BasePlayer target, string profile, int num)
        {
            foreach (var entry in Profiles.Where(entry => entry.Key == profile))
            {
                CreateTempSpawnGroup(target.transform.position, entry.Key, entry.Value, null, num == -1 ? IsNight() ? entry.Value.Spawn.Night_Time_Spawn_Amount : entry.Value.Spawn.Day_Time_Spawn_Amount : num);
                Puts(String.Format(lang.GetMessage("deployed", this), entry.Key, target.displayName));
                if (configData.Global.Announce_Toplayer)
                    bs.PrintToChat(string.Format(lang.GetMessage("ToPlayer", this), profile, target.displayName));
                return true;
            }
            return false;
        }

        [ConsoleCommand("BSUISaveTemplate")]
        private void BSUISaveTemplate(ConsoleSystem.Arg arg)
        {
            var prof = GetProfile(RD(arg.Args[0]));
            if (prof == null)
            {
                MsgUI(arg.Player(), "Profile not found. Notify author.", 3f);
            }
            else
            {
                if (templateData.Templates.ContainsKey(RD(arg.Args[0])))
                    MsgUI(arg.Player(), $"Template {arg.Args[0]} updated.", 3f);
                else
                    MsgUI(arg.Player(), $"Template {arg.Args[0]} saved.", 3f);
                templateData.Templates[RD(arg.Args[0])] = prof.Clone(prof, true);
            }
        }

        [ConsoleCommand("BSUIViewTemplates")]
        private void BSUIViewTemplates(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            BSTemplatesUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2]);
        }

        [ConsoleCommand("BSUILoadTemplate")]
        private void BSUILoadTemplate(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            SetProfile(RD(arg.Args[1]), templateData.Templates[RD(arg.Args[3])].Clone(GetProfile(RD(arg.Args[1])), false));
            BSMainUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2]);
            MsgUI(arg.Player(), $"Template {RD(arg.Args[3])} restored.", 3f);

        }

        [ConsoleCommand("BSUIEditSPKits")]
        private void BSUIEditSPKits(ConsoleSystem.Arg arg)
        {
            if (ValidKits.Count == 0)
            {
                MsgUI(arg.Player(), "There are no valid kits", 3f);
                return;
            }

            if (arg.Player() == null)
                return;
            BSKitsUI(arg.Player(), RD(arg.Args[0]), Convert.ToInt16(arg.Args[1]), Convert.ToInt16(arg.Args[2]));
        }

        [ConsoleCommand("BSChangeSPKit")]
        private void BSChangeSPKit(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            int num = Convert.ToInt16(arg.Args[1]);
            if (Convert.ToBoolean(arg.Args[4]) == true)
                spawnsData.CustomSpawnLocations[RD(arg.Args[0])][Convert.ToInt16(arg.Args[2])].Kits.Add(ValidKits[num]);
            else
                spawnsData.CustomSpawnLocations[RD(arg.Args[0])][Convert.ToInt16(arg.Args[2])].Kits.Remove(ValidKits[num]);
            BSKitsUI(arg.Player(), RD(arg.Args[0]), Convert.ToInt16(arg.Args[2]), Convert.ToInt16(arg.Args[3]));
        }

        [ConsoleCommand("BSChangeOverRides")]
        private void BSChangeOverRides(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            int option = Convert.ToInt16(arg.Args[2]);
            var sd = spawnsData.CustomSpawnLocations[RD(arg.Args[0])][Convert.ToInt16(arg.Args[1])];
            switch (option)
            {
                case 0:
                    sd.UseOverrides = !sd.UseOverrides;
                    if (sd.Kits == null)
                    {
                        var p = Profiles[RD(arg.Args[0])];
                        sd.Stationary = p.Spawn.Stationary;
                        sd.Kits = p.Spawn.Kit.ToList();
                        sd.Health = p.Spawn.BotHealth;
                        sd.RoamRange = p.Behaviour.Roam_Range;
                    }
                    break;
                case 1:
                    sd.Stationary = !sd.Stationary;
                    break;
                case 2:
                    sd.Health = Convert.ToBoolean(arg.Args[3]) == true ? sd.Health + 10 : sd.Health - 10;
                    sd.Health = Mathf.Max(sd.Health, 10);
                    break;
                case 3:
                    sd.RoamRange = Convert.ToBoolean(arg.Args[3]) == true ? sd.RoamRange + 10 : sd.RoamRange - 10;
                    sd.RoamRange = Mathf.Max(sd.RoamRange, 10);
                    break;
            }
            BSOverridesUI(arg.Player(), RD(arg.Args[0]), Convert.ToInt16(arg.Args[1]));
            SaveSpawns();
        }

        [ConsoleCommand("BSEditOverRides")]
        private void BSEditOverRides(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            BSOverridesUI(arg.Player(), RD(arg.Args[0]), Convert.ToInt16(arg.Args[1]));
            SaveSpawns();
        }

        [ConsoleCommand("BSCloseOverrides")]
        private void BSCloseOverrides(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            CuiHelper.DestroyUi(arg.Player(), "BSBSOverridesUI");
            SaveSpawns();
        }

        [ConsoleCommand("BSUIShowSpawns")]
        private void BSUIShowSpawns(ConsoleSystem.Arg arg)
        {
            var p = RD(arg.Args[0]);
            var s = spawnsData.CustomSpawnLocations[p];

            for (int i = 0; i < s.Count; i++)
            {
                var t = Spawners.ContainsKey(Profiles[p].Other.Parent_Monument) ? Spawners[Profiles[p].Other.Parent_Monument]?[0]?.go.transform : Spawners.ContainsKey(p) ? Spawners[p]?[0]?.go.transform : null;
                ShowSpawn(arg.Player(), t.TransformPoint(s[i].loc), i, configData.Global.Show_Spawns_Duration);
            }
            BSSpawnsUI(arg.Player(), RD(arg.Args[0]), 0);
        }

        [ConsoleCommand("BSUICheckNav")]
        private void BSUICheckNav(ConsoleSystem.Arg arg) => MsgUI(arg.Player(), ValidPoint(arg.Player().transform.position) ? "Navmesh found" : "No Navmesh");

        [ConsoleCommand("checknav")]
        private void checknav(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || (!HasPermission(arg.Player().UserIDString, permAllowed) && !arg.Player().IsAdmin))
                return;
            BSUICheckNav(arg);
        }

        Dictionary<ulong, Timer> ShowSpawnsTimers = new Dictionary<ulong, Timer>();

        bool TidyShowSpawns(BasePlayer player, bool message)
        {
            if (ShowSpawnsTimers.ContainsKey(player.userID))
            {
                ShowSpawnsTimers[player.userID].Destroy();
                ShowSpawnsTimers.Remove(player.userID);
                if (message)
                {
                    if (!Editing.ContainsKey(player.userID))
                        MsgUI(player, "You are not editing a profile", 2f);
                }
                return true;
            }
            return false;
        }
        [ConsoleCommand("showspawns")]
        private void ShowSpawns(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || (!HasPermission(arg.Player().UserIDString, permAllowed) && !arg.Player().IsAdmin))
                return;

            if (!TidyShowSpawns(arg.Player(), true))
            {
                if (Editing.ContainsKey(arg.Player().userID))
                {
                    var p = RS(Editing[arg.Player().userID]);
                    var s = spawnsData.CustomSpawnLocations[p];
                    ShowSpawnsTimers[arg.Player().userID] = timer.Repeat(1f, 0, () =>
                    {
                        for (int i = 0; i < s.Count; i++)
                        {
                            var t = Spawners.ContainsKey(Profiles[p].Other.Parent_Monument) ? Spawners[Profiles[p].Other.Parent_Monument]?[0]?.go.transform : Spawners.ContainsKey(p) ? Spawners[p]?[0]?.go.transform : null;
                            arg.Player().SendConsoleCommand("ddraw.text", 1f, ValidPoint(t.TransformPoint(s[i].loc)) ? Color.green : Color.red, t.TransformPoint(s[i].loc), $"<size=80>{i}</size>");
                        }
                    });
                }
                else
                    MsgUI(arg.Player(), "You are not editing a profile", 2f);
            }
        }

        [ConsoleCommand("BSUIMoveSpawn")]
        private void BSUIMoveSpawn(ConsoleSystem.Arg arg)
        {
            var p = RD(arg.Args[0]);
            var s = spawnsData.CustomSpawnLocations[p];
            var num = Convert.ToInt32(arg.Args[1]);
            var player = arg.Player();

            if (Convert.ToBoolean(arg.Args[2]) == true)
                s.RemoveAt(num);
            else
            {
                if (s.Count() >= num)
                {
                    var rot = player.viewAngles.y;
                    if (!ValidPoint(player.transform.position) && !Profiles[p].Spawn.Stationary && !s[num].UseOverrides && !s[num].Stationary)
                    {
                        s[num].UseOverrides = true;
                        s[num].Stationary = true;
                    }

                    var t = Spawners.ContainsKey(Profiles[p].Other.Parent_Monument) ? Spawners[Profiles[p].Other.Parent_Monument]?[0]?.go.transform : Spawners.ContainsKey(p) ? Spawners[p]?[0]?.go.transform : null;

                    if (t != null)
                    {
                        Vector3 loc = t.InverseTransformPoint(player.transform.position);
                        s[num].loc = loc;
                        s[num].rot = rot - t.transform.eulerAngles.y;
                        SaveSpawns();
                        ShowSpawn(player, player.transform.position, num, 10f);

                        MsgUI(player, String.Format(lang.GetMessage("movedspawn", this), num, p));
                        SendReply(player, TitleText + lang.GetMessage("movedspawn", this), num, p);
                        return;
                    }
                }
            }
            BSSpawnsUI(player, RD(arg.Args[0]), Convert.ToInt16(arg.Args[3]));
        }

        Dictionary<ulong, string> Editing = new Dictionary<ulong, string>();
        Dictionary<ulong, string> Copy = new Dictionary<ulong, string>();

        [ConsoleCommand("BSUISetEditing")]
        private void BSUISetEditing(ConsoleSystem.Arg arg)
        {
            Editing[arg.Player().userID] = RD(arg.Args[0]);
            TidyShowSpawns(arg.Player(), false);
            DestroyMenu(arg.Player(), true);
            MsgUI(arg.Player(), $"You can add spawnpoints to {RD(arg.Args[0])} by console command 'addspawn',\nToggle spawn visibility with 'showspawns',\nand check for navmesh with 'checknav'.", 5);
        }

        [ConsoleCommand("BSUIStopEdit")]
        private void BSUIStopEdit(ConsoleSystem.Arg arg)
        {
            Editing.Remove(arg.Player().userID);
            TidyShowSpawns(arg.Player(), false);
            BSMainUI(arg.Player(), "", "", "Spawn");
        }

        [ConsoleCommand("BSGotoProfile")]
        private void BSGotoProfile(ConsoleSystem.Arg arg)
        {
            BSMainUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), "Spawn");
        }

        [ConsoleCommand("BSUIAddSpawn")]
        private void BSUIAddSpawn(ConsoleSystem.Arg arg)
        {
            var p = RD(arg.Args[0]);
            var s = spawnsData.CustomSpawnLocations[p];
            var rot = arg.Player().viewAngles.y;
            var t = Spawners.ContainsKey(Profiles[p].Other.Parent_Monument) ? Spawners[Profiles[p].Other.Parent_Monument][0]?.go?.transform : Spawners.ContainsKey(p) ? Spawners[p]?[0]?.go.transform : null;

            if (t != null)
            {
                Vector3 loc = t.InverseTransformPoint(arg.Player().transform.position);
                s.Add(new SpawnData(null) { loc = loc, rot = rot - t.eulerAngles.y, Stationary = ValidPoint(arg.Player().transform.position) && Profiles[p].Spawn.Stationary });
                SaveSpawns();
                ShowSpawn(arg.Player(), arg.Player().transform.position, s.Count - 1, 10f);
            }

            BSSpawnsUI(arg.Player(), p, Convert.ToInt16(arg.Args[1]));
        }

        [ConsoleCommand("addspawn")]
        private void addspawn(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || (!HasPermission(arg.Player().UserIDString, permAllowed) && !arg.Player().IsAdmin))
                return;
            string p = string.Empty;
            Editing.TryGetValue(arg.Player().userID, out p);

            if (p != null && Profiles.ContainsKey(p))
            {
                var s = spawnsData.CustomSpawnLocations[Editing[arg.Player().userID]];
                var rot = arg.Player().viewAngles.y;
                var t = Spawners.ContainsKey(Profiles[p].Other.Parent_Monument) ? Spawners[Profiles[p].Other.Parent_Monument][0]?.go?.transform : Spawners.ContainsKey(p) ? Spawners[p]?[0]?.go.transform : null;

                if (t != null)
                {
                    Vector3 loc = t.InverseTransformPoint(arg.Player().transform.position);
                    s.Add(new SpawnData(Profiles[p]) { loc = loc, rot = rot - t.eulerAngles.y, Stationary = ValidPoint(arg.Player().transform.position) && Profiles[p].Spawn.Stationary });
                    SaveSpawns();
                    ShowSpawn(arg.Player(), arg.Player().transform.position, s.Count - 1, 10f);
                    MsgUI(arg.Player(), $"Added point {s.Count() - 1}", 1.5f);
                }
            }
            else
            {
                MsgUI(arg.Player(), "You are not presently editing a valid profile.", 3);
            }
        }

        [ConsoleCommand("BSUIEditKits")]
        private void BSUIEditKits(ConsoleSystem.Arg arg)
        {
            if (ValidKits.Count == 0)
                return;

            if (arg.Player() == null)
                return;
            BSKitsUI(arg.Player(), RD(arg.Args[0]), Convert.ToInt16(arg.Args[1]), Convert.ToInt16(arg.Args[2]));
        }

        [ConsoleCommand("BSChangeKit")]
        private void BSChangeKit(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            int num = Convert.ToInt16(arg.Args[1]);
            if (Convert.ToBoolean(arg.Args[4]) == true)
                GetProfile(RD(arg.Args[0])).Spawn.Kit.Add(ValidKits[num]);
            else
                GetProfile(RD(arg.Args[0])).Spawn.Kit.Remove(ValidKits[num]);

            BSKitsUI(arg.Player(), RD(arg.Args[0]), Convert.ToInt16(arg.Args[2]), Convert.ToInt16(arg.Args[3]));
        }


        [ConsoleCommand("BSUITPPlayer")]
        private void BSUITPPlayer(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            if (!arg.Player().IsFlying)
                MsgUI(arg.Player(), "Enable no-clip before teleporting", 2f);
            else
            {
                arg.Player().Teleport(Profiles[RD(arg.Args[0])].Other.Location);
                DestroyMenu(arg.Player(), true);
            }
        }

        [ConsoleCommand("BSUIEditSpawns")]
        private void BSUIEditSpawns(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            CuiHelper.DestroyUi(arg.Player(), "BSSpawnsUI");
            BSSpawnsUI(arg.Player(), RD(arg.Args[0]), Convert.ToInt16(arg.Args[1]));
        }

        [ConsoleCommand("CloseExtra")]
        private void CloseKitsBS(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            CuiHelper.DestroyUi(arg.Player(), arg.Args[0]);
            BSMainUI(arg.Player(), Profiles[RD(arg.Args[1])].type == ProfileType.Custom ? "2" : "1", RD(arg.Args[1]), "Spawn", 1);
            if (Convert.ToInt16(arg.Args[2]) > -1)
            {
                BSSpawnsUI(arg.Player(), RD(arg.Args[1]), 0);
                BSOverridesUI(arg.Player(), RD(arg.Args[1]), Convert.ToInt16(arg.Args[2]));
            }
            SaveSpawns();
        }

        Dictionary<string, DateTime> LastReloads = new Dictionary<string, DateTime>();
        [ConsoleCommand("BSUIReload")]
        private void BSUIReload(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            if (LastReloads.ContainsKey(arg.Args[0]))
            {
                var remaining = (DateTime.Now - LastReloads[arg.Args[0]]).TotalSeconds;
                if (remaining < 10)
                {
                    MsgUI(arg.Player(), $"Cooldown - {10 - Mathf.Ceil((float)remaining)}s remaining.", 2);
                    return;
                }
            }
            LastReloads[arg.Args[0]] = DateTime.Now;
            ReloadData(RD(arg.Args[0]), true, null);
        }

        [ConsoleCommand("BSUIShowAll")]
        private void BSUIShowAll(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            foreach (var profile in Profiles.Where(x => x.Value.type != ProfileType.Biome && x.Value.type != ProfileType.Event))
                ShowProfiles(arg.Player(), profile.Value.Other.Location, profile.Key, configData.Global.Show_Profiles_Seconds);
            DestroyMenu(arg.Player(), true);
        }

        [ConsoleCommand("BSUIMain")]
        private void BSUIMain(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            BSMainUI(arg.Player(), arg?.Args?.Length == 1 ? arg.Args[0] : "");
        }

        [ConsoleCommand("BSUI")]
        private void BSUI(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            BSMainUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2]);
        }

        [ConsoleCommand("BSUIPage")]
        private void BSUIPage(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            BSMainUI(arg.Player(), arg.Args[0], string.Empty, string.Empty, Convert.ToInt16(arg.Args[1]));
        }

        [ConsoleCommand("BSChangeBool")]
        private void BSChangeBool(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);

            var record = GetProfile(RD(arg.Args[1]));
            var sub = record.GetType().GetField(RD(arg.Args[2]));
            var subobj = sub.GetValue(record);
            var prop = subobj.GetType().GetField(RD(arg.Args[3]));
            var propobj = prop.GetValue(subobj);
            prop.SetValue(sub.GetValue(record), !(bool)propobj);
            sub.SetValue(record, subobj);

            BSMainUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2], 0);
        }

        [ConsoleCommand("BSUICloseParentMonument")]
        private void BSUICloseParentMonument(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            CuiHelper.DestroyUi(arg.Player(), "BSShowParentsUI");
        }

        [ConsoleCommand("BSUICloseLoot")]
        private void BSUICloseLoot(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            CuiHelper.DestroyUi(arg.Player(), "BSShowLootUI");
        }

        [ConsoleCommand("BSShowParents")]
        private void BSShowParents(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            BSShowParentsUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2]);
        }

        [ConsoleCommand("BSShowLoot")]
        private void BSShowLoot(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            BSShowLootUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2]);
        }

        [ConsoleCommand("BSSelectParent")]
        private void BSSelectParent(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            string cprofile = RD(arg.Args[1]);
            var record = GetProfile(cprofile);

            record.Other.Parent_Monument = RD(arg.Args[3]);

            var path = storedData.MigrationDataDoNotEdit[cprofile];
            if (path.ParentMonument == new Vector3())
                Puts($"Parent_Monument added for {cprofile}. Removing any existing custom spawn points");
            else
                Puts($"Parent_Monument changed for {cprofile}. Removing any existing custom spawn points");

            spawnsData.CustomSpawnLocations[cprofile].Clear();
            SaveSpawns();

            path.ParentMonument = Profiles[RD(arg.Args[3])].Other.Location;
            path.Offset = Spawners[RD(arg.Args[3])][0].go.transform.InverseTransformPoint(record.Other.Location);

            ReloadData(arg.Args[1], true, null);
            BSMainUI(arg.Player(), arg.Args[0], cprofile, arg.Args[2], 0);
            MsgUI(arg.Player(), String.Format(lang.GetMessage("ParentSelected", this), RD(arg.Args[3]), cprofile));
        }

        [ConsoleCommand("BSSelectLoot")]
        private void BSSelectLoot(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            string cprofile = RD(arg.Args[1]);
            var record = GetProfile(cprofile);

            record.Death.Rust_Loot_Source = RD(arg.Args[3]);
            ReloadData(cprofile, true, null);
            BSMainUI(arg.Player(), arg.Args[0], cprofile, arg.Args[2], 0);
        }

        [ConsoleCommand("BSUIMoveProfile")]
        private void BSUIMoveProfile(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            string cprofile = RD(arg.Args[0]);
            var record = GetProfile(cprofile);
            var path = storedData.MigrationDataDoNotEdit[cprofile];

            record.Other.Location = arg.Player().transform.position;

            if (Spawners.ContainsKey(record.Other.Parent_Monument))
            {
                path.ParentMonument = Profiles[cprofile].Other.Location;
                storedData.MigrationDataDoNotEdit[cprofile].Offset = Spawners[record.Other.Parent_Monument][0].go.transform.InverseTransformPoint(arg.Player().transform.position);
            }

            spawnsData.CustomSpawnLocations[arg.Args[0]].Clear();
            SaveSpawns();
            ReloadData(RD(arg.Args[0]), true, null);
            BSMainUI(arg.Player(), "2", cprofile, "Other");
            MsgUI(arg.Player(), String.Format(lang.GetMessage("ProfileMoved", this), cprofile));
        }

        string remove = string.Empty;
        [ConsoleCommand("BSUIRemoveProfile")]
        private void BSUIReoveProfile(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;

            string cprofile = RD(arg.Args[0]);
            if (remove == cprofile)
            {
                DestroySpawnGroups(Spawners[cprofile][0].go);
                spawnsData.CustomSpawnLocations[cprofile].Clear();
                SaveSpawns();
                Profiles.Remove(cprofile);
                storedData.Profiles.Remove(cprofile);
                storedData.MigrationDataDoNotEdit.Remove(cprofile);
                SaveData();
                BSMainUI(arg.Player(), "2", "", "Spawn");
            }
            else
            {
                MsgUI(arg.Player(), "Click again to confirm");
                remove = cprofile;
            }
        }

        Profile GetProfile(string name) => defaultData.Monuments.ContainsKey(name) ? defaultData.Monuments[name] : defaultData.Biomes.ContainsKey(name) ? defaultData.Biomes[name] : defaultData.Events.ContainsKey(name) ? defaultData.Events[name] : storedData.Profiles.ContainsKey(name) ? storedData.Profiles[name] : null;

        [ConsoleCommand("BSChangeEnum")]
        private void BSChangeEnum(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            bool up = Convert.ToBoolean(arg.Args[4]);
            var record = GetProfile(RD(arg.Args[1]));
            var sub = record.GetType().GetField(RD(arg.Args[2]));
            var subobj = sub.GetValue(record);
            var prop = sub.GetValue(record).GetType().GetField(RD(arg.Args[3]));
            var propobj = (int)prop.GetValue(subobj);
            propobj = up ? propobj + 1 : propobj - 1;
            propobj = Mathf.Max(Mathf.Min(propobj, Convert.ToInt16(arg.Args[5]) - 1), 0);
            prop.SetValue(sub.GetValue(record), propobj);
            sub.SetValue(record, subobj);
            BSMainUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2], 0);
        }

        [ConsoleCommand("BSChangeNum")]
        private void BSChangeNum(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            bool up = Convert.ToBoolean(arg.Args[4]);
            var record = GetProfile(RD(arg.Args[1]));
            var sub = record.GetType().GetField(RD(arg.Args[2]));
            var subobj = sub.GetValue(record);
            var prop = sub.GetValue(record).GetType().GetField(RD(arg.Args[3]));
            var propobj = (int)prop.GetValue(subobj);

            int increment = tens.Contains(arg.Args[3]) ? ScaleIncrement(propobj, up) : 1;
            if ((arg.Args[3].Contains("Percent") || arg.Args[3].Contains("Chute_Fall")) && !arg.Args[3].Contains("Damage"))
                increment = 5;
            propobj = limitpercent(RD(arg.Args[3]), Mathf.Max(tens.Contains(arg.Args[3]) ? 10 : 0, up ? propobj + increment : propobj - increment));
            prop.SetValue(sub.GetValue(record), propobj);
            sub.SetValue(record, subobj);
            BSMainUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2], 0);
        }

        int ScaleIncrement(int val, bool up)
        {
            if (up)
            {
                if (val >= 5000) return 1000;
                if (val >= 500) return 100;
                if (val >= 300) return 50;
                if (val >= 200) return 20;
                return 10;
            }
            else
            {
                if (val <= 200) return 10;
                if (val <= 300) return 20;
                if (val <= 500) return 50;
                if (val <= 5000) return 100;
                return 1000;
            }
        }

        [ConsoleCommand("BSChangeDouble")]
        private void BSChangeDouble(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            bool up = Convert.ToBoolean(arg.Args[4]);
            var record = GetProfile(RD(arg.Args[1]));
            var sub = record.GetType().GetField(RD(arg.Args[2]));
            var subobj = sub.GetValue(record);
            var prop = sub.GetValue(record).GetType().GetField(RD(arg.Args[3]));
            var propobj = Math.Round((double)prop.GetValue(subobj), 1);
            bool RR = (arg.Args[3].Contains("RustRewards") && configData.Global.RustRewards_Whole_Numbers) || (arg.Args[3].Contains("XPerience") && configData.Global.XPerience_Whole_Numbers);
            double num = RR ? ScaleRR((int)propobj, up) : 0.1;
            propobj = up ? propobj + num : propobj - num;
            if (RR)
                propobj = Math.Round(propobj, 0);
            else
                propobj = Math.Round(Mathf.Max(0, (float)propobj), 1);

            prop.SetValue(sub.GetValue(record), propobj);
            sub.SetValue(record, subobj);
            BSMainUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2], 0);
        }

        int ScaleRR(int val, bool up)
        {
            if (up)
            {
                if (val >= 5000) return 1000;
                if (val >= 500) return 100;
                if (val >= 300) return 50;
                if (val >= 200) return 20;
                if (val >= 100) return 10;
                if (val >= 25) return 5;
                return 1;
            }
            else
            {
                if (val <= 25) return 1;
                if (val <= 100) return 5;
                if (val <= 200) return 10;
                if (val <= 300) return 20;
                if (val <= 500) return 50;
                if (val <= 5000) return 100;
                return 1000;
            }
        }

        [ConsoleCommand("BSConfChangeNum")]
        private void BSConfChangeNum(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            bool up = Convert.ToBoolean(arg.Args[1]);
            var sub = configData.Global.GetType().GetField(RD(arg.Args[0]));
            var subobj = (int)sub.GetValue(configData.Global);

            int increment = tens.Contains(arg.Args[0]) ? 10 : 1;
            if (arg.Args[0].Contains("Percent") || arg.Args[0].Contains("Chute_Fall"))
                increment = 5;

            subobj = limitpercent(RD(arg.Args[0]), Mathf.Max(0, up ? subobj + increment : subobj - increment));
            sub.SetValue(configData.Global, subobj);
            BSMainUI(arg.Player(), "0", "", "", 1);
        }

        int limitpercent(string name, int number)
        {
            if (name.Contains("Damage"))
                return Mathf.Max(number, 0);
            if (name.Contains("Percent") || name.Contains("Chute_Fall"))
                return Mathf.Max(Mathf.Min(number, 100), 0);
            return number;
        }

        [ConsoleCommand("BSChangePop")]
        private void BSChangePop(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            bool up = Convert.ToBoolean(arg.Args[0]);
            int val = Convert.ToInt16(arg.Args[1]);
            if (up)
                configData.Global.ScaleRules[val] += 5;
            else
                configData.Global.ScaleRules[val] -= 5;

            if (configData.Global.ScaleRules[val] < 0)
                configData.Global.ScaleRules[val] = 0;

            if (configData.Global.ScaleRules[75] > configData.Global.ScaleRules[100])
                configData.Global.ScaleRules[75] = configData.Global.ScaleRules[100];

            if (configData.Global.ScaleRules[50] > configData.Global.ScaleRules[75])
                configData.Global.ScaleRules[50] = configData.Global.ScaleRules[75];

            if (configData.Global.ScaleRules[25] > configData.Global.ScaleRules[50])
                configData.Global.ScaleRules[25] = configData.Global.ScaleRules[50];

            BSMainUI(arg.Player(), "0", "", "", 1);
        }

        [ConsoleCommand("BSConfChangeBool")]
        private void BSConfChangeBool(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            var sub = configData.Global.GetType().GetField(RD(arg.Args[0]));
            var subobj = (bool)sub.GetValue(configData.Global);
            sub.SetValue(configData.Global, !subobj);

            if (arg.Args[0] == "Turret_Safe")
            {
                if (configData.Global.Turret_Safe) 
                    Subscribe("CanBeTargeted");
                else
                    Unsubscribe("CanBeTargeted");
            }
            BSMainUI(arg.Player(), "0", "", "", 1);
        }

        public List<string> tens = new List<string>() { "Block_Event_Here_Radius", "Parachute_From_Height", "Immune_From_Damage_Beyond", "Bot_Damage_Percent", "Radius", "BotHealth", "Roam_Range", "Aggro_Range", "DeAggro_Range" };

        [ConsoleCommand("CloseBS")]
        private void CloseBS(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), true);
            SaveData();
            SaveConfig(configData);
        }

        string RS(string input) => input.Replace(" ", "SPACE");
        string RD(string input) => input.Replace("SPACE", " ");
        #endregion

        #region API
        private Dictionary<string, List<ulong>> BotReSpawnBots()
        {
            var BotReSpawnBots = new Dictionary<string, List<ulong>>();
            foreach (var entry in Profiles)
                BotReSpawnBots.Add(entry.Key, new List<ulong>());

            foreach (var bot in NPCPlayers)
            {
                if (bot.Value == null)
                    continue;
                var bData = bot.Value;
                if (bData == null)
                    continue;
                if (BotReSpawnBots.ContainsKey(bData.profilename))
                    BotReSpawnBots[bData.profilename].Add(bot.Key);
                else
                    BotReSpawnBots.Add(bData.profilename, new List<ulong> { bot.Key });
            }
            return BotReSpawnBots;
        }

        private bool IsBotReSpawn(NPCPlayer npc) => NPCPlayers.ContainsKey(npc.userID);
        private bool IsBotReSpawn(ulong id) => NPCPlayers.ContainsKey(id);

        private string NPCProfile(NPCPlayer npc)
        {
            if (NPCPlayers.ContainsKey(npc.userID))
                return NPCPlayers[npc.userID].name;
            return "No Name";
        }

        private string[] AddGroupSpawn(Vector3 location, string profileName, string group, int quantity)
        {
            if (location == new Vector3() || profileName == null || group == null)
                return new string[] { "error", "null parameter" };

            foreach (var entry in bs.Profiles.Where(entry => entry.Key.ToLower() == profileName.ToLower() && IsSpawner(entry.Key)))
            {
                if (entry.Key.ToLower() == profileName.ToLower())
                {
                    if (quantity == 0)
                        quantity = GetPop(entry.Value);
                    CreateTempSpawnGroup(location, entry.Key, entry.Value, group.ToLower(), quantity);
                    return new string[] { "true", "Group successfully added" };
                }
            }
            return new string[] { "false", "Group add failed - Check profile name and try again" };
        }

        private string[] RemoveGroupSpawn(string group)
        {
            if (group == null)
                return new string[] { "error", "No group specified." };

            List<global::HumanNPC> toDestroy = Pool.GetList<global::HumanNPC>();
            bool flag = false;
            foreach (var bot in NPCPlayers.ToDictionary(pair => pair.Key, pair => pair.Value))
            {
                if (bot.Value == null)
                    continue;

                if (NPCPlayers[bot.Key]?.group == group.ToLower())
                {
                    flag = true;
                    NPCPlayers[bot.Key].npc?.Kill();
                }
            }
            Facepunch.Pool.FreeList(ref toDestroy);
            return flag ? new string[] { "true", $"Group {group} was destroyed." } : new string[] { "true", $"There are no bots belonging to {group}" };
        }
        #endregion

        #region Docs
        public Dictionary<string, string> Docs = new Dictionary<string, string>()
        {
            { "Allow_Ai_Dormant", "Setting this to true allows npcs to become inactive when no players are nearby.\nRust server setting 'ai_dormant' must also be true for this to work.\n\nSee 'Prevent_Biome_Ai_Dormant setting for excluding biome npcs from BotReSpawn dormancy settings." },
            { "Smooth_Damage_Scale", "This decreases the difference in damage between low, and high damage weapons used by npcs.\n\nWith the setting true the difference in damage dealt by AK and M249, for example, would be less extreme."},
            { "Allow_Oilrigs", "Setting this to true will enable an OilRig profile under 'Default Monuments'.\nThere is no navmesh at the OilRigs so you must set stationary to true for OilRig profiles for them to work.\n\nThis was added by request so that users with custom spawn points at OilRigs\ncan retain their spawnpoints and profile settings from wipe to wipe." },
            { "Announce_Toplayer", "Setting this to true will make a chat announcement when `/BotReSpawn toplayer` command or UI button is used. \n\nSee `ToPlayer` entry in /oxide/lang/**/BotReSpawn.json for announcement customisation." },
            { "APC_Safe", "Setting this to true will prevent APCs from targeting BotReSpawn npcs." },
            { "SamSite_Safe_Whilst_Chuting", "Setting this to true will prevent SamSites from from targeting parachuting BotReSpawn npcs." },
            { "Disable_Non_Parented_Custom_Profiles_After_Wipe", "When setting up a custom profile, BotReSpawn lets you choose a Parent_Monument.\nYou should do this if your custom profile is meant to be at, or near, a default monument.\n\nDoing so means BotReSpawn will auto-relocate your profile and its spawnpoints when your map changes\nif that monument type exists on the new map.\n\nCustom profiles which are not parented do not get relocated anywhere,\nmeaning they usually need to be manually relocated somewhere sensible after map change.\n\nFor that reason, BotReSpawn automatically disables non-parented profiles after a wipe/map change.\n\nIn cases where the same map is being re-used,\nyou may wish to set this to false to keep your custom profiles enabled across wipes." },
            { "Enable_Targeting_Hook", "BotReSpawn has a documented targeting hook that developers can use in their plugins.\nThis is disabled by default for performance.\n\nSetting this to true enables the use of the hook." },
            { "Ignore_Factions", "Setting this to true will cause all BotReSpawn npc profiles to fight with eachother,\n\ncompletely ignoring all faction/subfaction settings." },
            { "Allow_HackableCrates_With_OwnerID", "Some plugins use OwnerID to identify hackable crates that they've spawned.\n\nSetting this to true will run BotReSpawn's hackable crate profiles for such crates." },
            { "Allow_HackableCrates_From_CH47", "If this is set to true then BotReSpawn hackable crate event profiles\n\nwill run for codelocked hackable crates which are dropped at monuments by CH47." },
            { "Allow_HackableCrates_At_Oilrig", "If this is set to true then BotReSpawn hackable crate event profiles\n\nwill run for codelocked hackable crates on Oilrigs." },
            { "Allow_Parented_HackedCrates", "Some plugins which create hackable crates parent them to other entities, such as vehicles.\n\nSetting this to true will allow BotReSpawn's hackable crate profiles to run for such crates." },
            { "Allow_All_Other_HackedCrates", "Allows any hackable crates not governed by other settings to trigger BotReSpawn crate events." },
            { "Ignore_Skinned_Supply_Grenades", "If Supply_Enabled is true, allowing Airdrop profile to trigger for user-thrown supply grenades,\nsetting this option to true will make BotReSpawn ignore supply grenades with a non-default skin.\n\nThis can be useful if your server uses skinned supply grenades for other purposes." },
            { "Limit_ShortRange_Weapon_Use", "BotReSpawn npcs automatically choose the most suitable weapon based on target range.\n\n In cases where the npc only has a short range weapon (pistol/bow), and the target is far away,\nsetting this to true will prevent BotReSpawn npcs from firing.\n\nInstead the npc will try to move to a closer range and then fire." },
            { "NPCs_Assist_NPCs", "If this setting is true then BotReSpawn npcs will be aware when nearby BotReSpawn become aggravated.\nThey will then become aggravated themselves, and assist with the fight." },
            { "NPCs_Damage_Armour", "If this setting is true then when BotReSpawn npcs injure a player\nthere will also be damage to any armour the player is wearing." },
            { "Peacekeeper_Uses_Damage", "With Peacekeeper true, npcs will ignore players who are not marked, by the game, as hostile.\n\nSetting this to true means you must attack a peacekeeper:true npc before he will respond."},
            { "Prevent_Biome_Ai_Dormant", "If Ai_Dormant is true, setting this option to true will prevent it from applying to Biome profile BotReSpawn npcs." },
            { "Pve_Safe", "Setting this to true will prevent BotReSpawn npcs from being damaged by cacti and barricades." },
            { "Reduce_Damage_Over_Distance", "Setting true will reduce the damage caused by BotReSpawn npcs the farther away the npc from its hit target." },
            { "Remove_Frankenstein_Parts", "Setting true will prevent Frankenstein parts from spawning in npc corpse loot as part of vanilla Rust Loot." },
            { "Remove_KeyCard", "Setting true will prevent keycards from spawning as part of Rust vanilla corpse loot, if enabled.\n\nIn addition, default rust loot can be disabled entire per-profile." },
            { "RustRewards_Whole_Numbers", "RustRewards is a separate plugin available from Codefling.\n\nIf this setting is set to false, RustRewards value is adjusted in 0.1 increments in BotReSpawn UI.\nIf true, it's whole numbers." },
            { "Scale_Meds_To_Health", "BotReSpawn npcs can heal with syringes, if a syringe is placed in the kit belt.\nThe npcs will heal when line of sight is broken, or when they have no current target.\nOnly one syringe is needed in the belt. Quantity is ignored.\n\nIf this setting is true, the healing value of the syringe will increase/decrease relative to the npcs health settings.\n\nFor example, if you set your npc's spawn health to 1000, a syringe will heal a much greater amount than if npc spawn health was 100." },
            { "Staggered_Despawn", "Setting this to true will make BotReSpawn remove npcs one by one, over a priod of time,\nin cases where it needs to reduce the population. (eg. Day/Night changes)\n\nSetting false means BotReSpawn will remove as many as it needs to all in one go." },
            { "Suicide_Boom", "There is no upper limit on how many BotReSpawn npcs can exist for events, such as airdrop.\nThe more airdrops, the more npcs.\n\nTo prevent a runaway build up of npcs on high-airdrop/low-pop servers, these npcs will suicide after a preset duration.\n\nSetting this option to true makes them go out with a bang." },
            { "Supply_Enabled", "BotReSpawn has an 'Events' profile for airdrops, spawning npcs when airdops occur.\n\nIf this settings is true, that profile will trigger when players use supply grenades, as well as for server airdrops.\nIf false, the profile will only trigger for server airdrops." },
            { "Turret_Safe", "Setting this to true will prevent turrets from targeting BotReSpawn npcs." },
            { "UseServerTime", "BotReSpawn profiles have options for day, and night, spawn amounts.\nYou can customise the hours which consistute day, or night, in this settings page.\n\nSetting this option to true will use Rust's default day/night thresholds instead of your custom settings." },
            { "XPerience_Whole_Numbers", "Xperience is a separate plugin available from Codefling.\n\nIf this setting is set to false, Xperience value is adjusted in 0.1 increments in BotReSpawn UI.\nIf true, it's whole numbers." },

            { "DayStartHour", "The hour, in server time, when day begins." },
            { "NightStartHour", "The hour, in server time, when night begins." },
            { "Chute_Fall_Speed", "Adjust the base fall speed for parachuting npcs.\n50 is the default setting."},
            { "Deaggro_Memory_Duration", "The length of time it takes for an npc to forget you if you stay outside of aggro/deaggro range,\nor sustain broken line of sight." },
            { "Chute_Speed_Variation", "Increasing this number will increase the difference in speed between the fastest and slowest falling parachute npcs.\n\nIn cases where mutliple npcs spawn together, a greater number can look less mechanical / more natural." },
            { "Parachute_From_Height", "The height at which parachute-enabled profiles will spawn. 200 is default.\n\nThis does not apply to airdrop profile." },
            { "Remove_BackPacks_Percent", "The percentage chance that npc corpses will not turn into a backpack." },
            { "Show_Profiles_Seconds", "When showing profiles via UI button, this option determines how long they stay on-screen." },
            { "Show_Spawns_Duration", "When viewing spawnpoints for a profile via UI button, this option determines how long they stay on-screen." },
            { "NPCPop%", "Each profile has an option to scale npc population against server population.\n\nHere you can set how many players need to be online in order to have 100, 75, 50, or 25 percent of npcs spawning.\n\nThis is dynamically managed, so more npcs will spawn as more players join\nand npcs will be killed off as players leave." },

            { "AutoSpawn", "Setting this to true will make your profile spawn automatically, and maintain the correct population.\n\n Profiles can still be used for /toplayer commands or calls from other plugins if AutoSpawn is set to false." },
            { "Radius", "When UseCustomSpawns is set to false BotReSpawn will attempt to find random spawnpoints for your npcs,\nnear the profile's location.\n\nRadius is the size of the area around your profile in which BotReSpawn will look for suitable spawnpoints.\n\n If UseCustomSpawns is true and there are enough custom spawn points added\nthis setting is ignored." },
            { "BotNamePrefix", "BotReSpawn npcs can be given random names (automatically), or custom names.\n BotNamePrefix will appear before the npc's name, and will be the same for all npcs from this profile.\n\nSimilar to player clantags...\n\nBotNames and BotNamePrefix need to be added manually in the correct /data/BotReSpawn json file.\n\nSee documentation at codefling.com/plugins/botrespawn for correct formatting." },
            { "Keep_Default_Loadout", "Setting this to false will strip the npc of default clothing and weaponry before applying any kits you've assigned.\nThis is necessary if your kit contains clothing, as the default hazmat needs to be removed first.\n\n Keeping it true allows you to have npcs with vanilla clothing and weapons.\nYou can still use kits to give additional items or weapons in this case, if you wish." },
            { "Day_Time_Spawn_Amount", "The number of npcs that this profile should spawn, and maintain, during day time.\n\nPopulation is dynamically adjusted as day/night changes.\n\nDay/Night time thresholds can be customised in Global Settings page." },
            { "Night_Time_Spawn_Amount", "The number of npcs that this profile should spawn, and maintain, during night time.\n\nPopulation is dynamically adjusted as day/night changes.\n\nDay/Night time thresholds can be customised in Global Settings page." },
            { "Announce_Spawn", "Setting this to true will push a chat announcement when npcs from this profile spawn." },
            { "Announcement_Text", "This is the text that will be announced if Announce_Spawn is set to true.\n\nThis text can only be edited manually in the correct /data/BotReSpawn json file." },
            { "BotHealth", "The maximum, and starting, health that npcs from this profile will have." },
            { "Stationary", "Is this is set to true then npcs from this profile will stand still where they spawn.\n\nThis can be useful for custom-placed snipers, for example." },
            { "UseCustomSpawns", "If this is true then BotReSpawn will place npcs at your custom spawn points (Added via UI buttons below).\n\nIf false BotReSpawn will find random spawnpoints in the area, within the profile Radius.\n\nIf there aren't sufficient custom spawn points, BotReSpawn will make up the nunbers with random spawnpoints." },
            { "ChangeCustomSpawnOnDeath", "If this is set to true then an npc who has been killed will not respawn at its previous spawnpoint.\n\nInstead a new spawnpoint will be chosen when it respawns." },
            { "Scale_NPC_Count_To_Player_Count", "If this is set to true then the population of npcs for this profile will be restricted\naccording to how many players are online.\n\nThe thresholds for this restriction are adjustable in Global Settings." },
            { "FrankenStein_Head", "Choose which FrankenStein head the npc has." },
            { "FrankenStein_Torso", "Choose which FrankenStein torso the npc has." },
            { "FrankenStein_Legs", "Choose which FrankenStein legs the npc has." },

            { "Roam_Range", "The maximum distance that a non-aggravated npc will roam from his spawnpoint, before returning." },
            { "Aggro_Range", "The max distance at which npcs can become aware of potential targets." },
            { "DeAggro_Range", "The distance beyond which npcs will be able to lose, or forget, targets.\n\nPlayers must stay beyond this distance for as long as Global Setting 'Deaggro_Memory_Duration'\nfor the npc to forget them." },
            { "Peace_Keeper", "If this setting is true then npcs from this profile will ignore players who are not marked hostile.\n\nIf it's false then the npcs will attack players regardless of the players' hostility status." },
            { "Bot_Accuracy_Percent", "Increasing this value will increase npc accuracy with weapons.\nDecreasing it will make the npcs less accurate." },
            { "Bot_Damage_Percent", "Increasing this value will make npcs do more damage to their target per hit.\n\nDecreasing it makes them do less damage per hit." },
            { "Running_Speed_Booster", "Increasing this value makes the npcs from this profile faster whilst running.\n\nDecreasing it makes them run slower." },
            { "Roam_Pause_Length", "Non-aggravated NPCs choose a destination to walk to, walk there, then choose another, and repeat.\n\nThis setting is the length of time an npc will pause at each destination, before choosing another one.\n\nThis only applies when npcs are passively roaming - Not when in combat." },
            { "AlwaysUseLights", "If this setting is true then npcs will enable any weapon lights, hand held lights, or head worn lights,\nregardless of time of day." },
            { "Ignore_All_Players", "If this is set to true then npcs from this profile will never attack, or respond to, any players." },
            { "Ignore_Sleepers", "If this is set to true then npcs from this profile will not kill or attack sleeping players." },
            { "Target_Noobs", "If this is set to false then npcs from this profile will not attack 'noob' players.\n\nThis setting uses Rust's built-in sash system and is automatically disabled\nif NoSash plugin is installed." },
            { "NPCs_Attack_Animals", "If this is set to true then npcs from this profile will attack animals which are nearby." },
            { "Friendly_Fire_Safe", "If this is set to true then npcs who are allies will not take damage from accidentally shooting eachother.\n\nSee 'Faction' and 'Subfaction' for more information on enemy/ally profiles." },
            { "Melee_DamageScale", "Use this to fine-tune the damage dealt by npcs that are using melee weapons.\n\nHigher means more damage." },
            { "RangeWeapon_DamageScale", "Use this to fine-tune the damage dealt by npcs that are using long-range weapons.\n\nHigher means more damage." },
            { "Rocket_DamageScale", "Use this to fine-tune the damage dealt by npcs that are using rocket/launched grenade weapons.\n\nHigher means more damage." },
            { "Assist_Sense_Range", "If Global Setting 'NPCs_Assist_NPCs' is set to true then npcs can assist nearby npcs who have a target.\n\nUse this setting to adjust how far away an npc can sense other aggravated npcs." },
            { "Victim_Bleed_Amount_Per_Hit", "The amount of bleeding damage that npcs from this profile can cause a player per hit." },
            { "Victim_Bleed_Amount_Max", "An upper limit to the maximum amount of bleeding damage an npc can cause a player,\nregardless of how many times the npc hits the player." },
            { "Bleed_Amount_Is_Percent_Of_Damage", "If this is set to false then bleeding amount per hit is a fixed amount as per above setting.\nIf it's true then bleeding amount per hit is a fraction of the damage dealt." },
            { "Target_ZombieHorde", "ZombieHorde is another npc plugin.\n\nIf this is set to true then npcs from this profile will target and attack ZombieHorde npcs." },
            { "Target_HumanNPC", "HumanNPC is another npc plugin.\n\nIf set to true then npcs from this profile will target and attack HumanNPC npcs." },
            { "Target_Other_Npcs", "If this is set to true then npcs from this profile will target any npcs which are not covered by the other settings." },
            { "Respect_Safe_Zones", "If this is set to true then npcs from this profile will not engage in combat\n\nif they are, or their target is, in a Safe Zone." },
            { "Faction", "BotReSpawn profiles with the same faction, or subfaction (above 0) will be allies.\n\nProfiles with different factions and subfactions (above 0) will be enemies.\n\nIf faction and subfaction are 0 then this profile will not attack,\nor be attacked by, any other profile." },
            { "SubFaction", "BotReSpawn profiles with the same faction, or subfaction (above 0) will be allies.\n\nProfiles with different factions and subfactions (above 0) will be enemies.\n\nIf faction and subfaction are 0 then this profile will not attack,\nor be attacked by, any other profile." },
            { "Dont_Fire_Beyond_Distance", "Beyond this distance npcs will refuse to fire, instead favouring moving closer to their target." },

            { "Spawn_Hackable_Death_Crate_Percent", "The percentage chance of a hackable crate spawning when an npc from this profile is killed.\n\nThe crate will spawn at the location where the npc died." },
            { "Death_Crate_CustomLoot_Profile", "CustomLoot is a separate plugin, available at Codefling.\n\nIf you want the Death-Crate loot to come from a CustomLoot table that you made, enter the name here.\n\nChanges to this setting must be made manually in the correct /data/BotReSpawn json file." },
            { "Death_Crate_LockDuration", "The length of time that it takes to hack the Death-Crate." },
            { "Corpse_Duration", "The length of time, in minutes, that the corpse of a dead npc from this profile will remain." },
            { "Weapon_Drop_Percent", "The percentage chance that a killed npc from this profile will drop his weapon on the ground." },
            { "Min_Weapon_Drop_Condition_Percent", "If a killed npc drops his weapon, this is the minimum condition % that the weapon can have." },
            { "Max_Weapon_Drop_Condition_Percent", "If a killed npc drops his weapon, this is the maximum condition % that the weapon can have." },
            { "Wipe_Main_Percent", "The percentage chance that the main inventory container of an npc is erased when the npc is killed." },
            { "Wipe_Belt_Percent", "The percentage chance that the belt inventory container of an npc is erased when the npc is killed." },
            { "Wipe_Clothing_Percent", "The percentage chance that the clothing inventory container of an npc is erased when the npc is killed." },
            { "Allow_Rust_Loot_Percent", "The percentage chance of vanilla Rust loot spawning in a dead npc's corpse/backpack." },
            { "Rust_Loot_Source", "Use this setting if you'd like to populate npc corpses with alternative vanilla loot tables.\n\nYou can select any vanilla container you want to give the corpse loot that you'd normally see in that container type." },
            //{ "Use_AlphaLoot", "Setting this to true will ask AlphaLoot to produce the loot for your chosen container type (Rust_Loot_Source).\nSetting this to false will produce vanilla loot for your chosen container type." },
            { "Respawn_Timer", "The length of time, in minutes, that it takes for a killed npc to repspawn." },
            { "RustRewardsValue", "Rust Rewards is a separate plugin, available from Codefling.\n\nThis setting is the quantity of RustRewards that is given to a player\nfor killing an npc from this profile.\n\nYou can toggle between 1 and 0.1 increments in the Global settings page." },
            { "XPerienceValue", "XPerience is a separate plugin, available from Codefling.\n\nThis setting is the quantity of XPerience that is given to a player\nfor killing an npc from this profile.\n\nYou can toggle between 1 and 0.1 increments in the Global settings page." },

            { "Chute", "Setting this to true will make npcs from this profile spawn high up in the air,\nand parachute down to their spawnpoint on the ground.\n\nFor event profiles which can take place underground (crate spawn/crate hack)\nchute:true will be ignored where appropriate, allowing npcs to spawn near the event." },
            { "Invincible_Whilst_Chuting", "If Chute setting is enabled, this setting lets you make the npcs invincible until they land on the ground." },
            { "Backpack_Duration", "This is the length of time, in minutes, that npc backpacks will remain on the ground\nafter their corpse has despawned." },
            { "Suicide_Timer", "Event profile populations are kept under control by causing the npcs to suicide after X minutes.\n(Airdrop, Crate hack, Crate spawn, Heli kill, etc)\n\nThis setting allows you to adjust the number of minutes before the npcs will suicide.\n\nFor default/custom/biome profiles, this setting only applies when /toplayer command or button was used,\nas those are additional npcs outside of the regular maintained population." },
            { "Die_Instantly_From_Headshot", "If this is set to true, one headshot will kill npcs from this profile regardless of their health." },
            { "Require_Two_Headshots", "If Die_Instantly_From_Headshot is enabled, setting this option to true will mean two headshots are required\ninstead of just one.\n\nIt doesn't matter if the headshots came from a single player, or two different players." },
            { "Fire_Safe", "Setting this to true will prevent npcs from this profile from being hurt, or dying, from flames." },
            { "Grenade_Precision_Percent", "100% makes grenade throws extremely accurate. 0% makes npcs much more likely to miss their target." },
            { "Disable_Radio", "Vanilla npcs have radio-chatter sound effects.\n\nSetting this option to true will disable those effects." },
            { "Location", "This is for internal use only. Do not edit this value.\n\nIf you wish to relocate a profile, use the 'Move profile here' button on this settings page." },
            { "Parent_Monument", "If you've created a custom profile at, or near, a default monument,\nand want that profile to stay near that default monument even when the map has changed,\nselect that default monument here.\n\nDoing so will remove all custom spawn points, so be sure to select parent\nbefore adding custom spawn points." },
            { "Use_Map_Marker", "If this is set to true a basic map marker will show on the in game map, to indicate a BotReSpawn profile location.\n\nIt does not show at the location of specific npcs - It shows at the location of the profile." },
            { "Always_Show_Map_Marker", "By default the map marker does not show if all npcs from that profile are dead.\n\nSet this to true to show the marker regardless." },
            { "MurdererSound", "Setting this to true will cause the npcs to make the 'Murderer' grunt noises.\n\nThis only applies if the npcs only have melee weapons." },
            { "Immune_From_Damage_Beyond", "Attacks from beyond this distance will not hurt npcs from this profile.\nThe npcs will completely ignore these attack." },
            { "Short_Roam_Vision", "When passively roaming, npcs choose a spot on the ground to walk to, walk to it, then choose another...and repeat.\n\nIf this setting is true npcs will choose spots much closer to their current position than normal.\n\nThis can be useful where npcs are in areas with a narrow river, for example,\nto prevent them trying to roam to the other side of it." },
            { "Off_Terrain", "If your custom profile is out at sea, on a custom OilRig or shipwreck, or your npcs are placed on top of a structure, for example,\nthis setting should be set to true." }
        };
        #endregion
    }
}

//  Fixed in V1.0.3
//  Melee attack/hitinfo Error.
//  Properly fixed disable radio chatter.
//  Added RustRewardsValue per profile.
//  Automatically remove non-weapon items from npc belt.
//  Made day/night respawn times quicker - Not related to regular respawn timer.
//  Fixed the resetting of kits info where Kits plugin was not loaded.
//  Running speed booster fixed - 10 is default - bigger is faster.
//  Removed accidental 'edit spawnpoints' button in Biome profile view. 
//  Found and fixed conflict issue which resulted in fail to compile during server restarts. Thanks @Krungh Crow
//  New kits are found without the need to reload.
//  Kits with any melee weapon should now be accepted (hatchets etc).
//  Addspawn console command has been added, for keybinding.It is enabled in UI (spawnpoints page).
//  Kits page allows for four columns, allowing for approx 120 kits total.
//  Added option to use server time, ignoring day/night start hours.


//  Fixed in V1.0.4
//  Number adjustment increments are dynamic - Bigger increment for larger numbers  
//  ToPlayer showing 'does not exist' for some profile names. 
//  Profile page not having enough room on larger servers.
//  Issues relating to quantity of AirDrop/ToPlayer spawned npcs.
//  Removed non-applicable options from Airdop profile page.
//  Always use lights setting, and general use-lights behaviour
//  Respawn time/order - now more accurate/predictable.
//  Added Wipe_Main_Percent option - This is for people who want to add loot items to main via kit, but only want them available a % of the time.
//  Spaces and hyphens in profile names causing issues. They are now auto removed.
//  Day/night spawning times/delays.
//  APC_Safe should apply to npcs being 'run over' now - untested.
//  Reported death type for suicide is now 'Suicide', where explosion option is disabled.
//  Roam behaviour with melee-only npcs.


//  Fixed in V1.0.5
//  Rare issue where day night switching from zero to many npcs could throw errors. 
//  Altered initial save timing. @Sasquire
//  Made room for more kits, until pagination is added - Might stutter maxed out?
//  Botnames should sync with kit names, where possible.
//  Improvements to deaggro range / line of sight disengage.
//  Added kit name to /botrespawn info command
//  Added AddGroupSpawn API - BotReSpawn?.Call("AddGroupSpawn", location, "profilename", "MadeUpName", quantity); @bazz3l
//  Added RemoveGroupSpawn API.
//  Added hook OnBotReSpawnNPCSpawned(ScientistNPC npc, string profilename, string group, passing group name for API users.
//  BotReSpawn npcs should ignore Safezone players.
//  Hooked up ChangeCustomSpawnOnDeath option - TEST THIS
//  Prevent custom spawn point related options showing for Biome/Event


//  Changes in V1.0.6
//  Fixed UI issue changing overrides for default monument spawnpoints. 
//  Fixed issue where override kits for newly created spawnpoints would synchronise. 
//  Fixed issue where kit changes may not hold on first attempt.
//  Fixed issue where full list of bot names wasn't used.
//  Added "Go to settings for {profile}" button on main UI page, when 'addspawn' command is enabled for a profile.
//  Fixed incorrect title on 'Select Parent_Monument page.
//  Added Copy/Paste buttons for profiles - Does not copy location, Parent_Monument, or spawn points.
//  Added 'Show All Profiles' duration option (seconds) in global config.
//  Fixed accuracy and damage multipliers.
//  Added "Delete Profile" button in UI - Two clicks required.
//  Bot_Damage_Percent can now exceed 100.
//  Added global option "Ignore_Skinned_Supply_Grenades".
//  Default kits are only copied to custom spawn point overrides (as defaults) if/when UseOverrides is set true.
//  Added toplayer console command. "botrespawn toplayer NameOrID ProfileName amount(optional)".
//  Fixed late timing of wipe_main_percent, which resulted in wiping loot placed by other plugins.
//  Removed unused CH47 event profile.
//  Replaced Harbor profiles with Harbor_Small/Harbor_Large.
//  Replaced small Fishing Village profiles with Fishing Village_A/_B/_C. Large remains as before.
//  Added "Disable_Non_Parented_Custom_Profiles_After_Wipe" option. Set true if reusing the same map.
//  Removed 'Failed to get enough spawnpoints for...' message for event profiles, as this doesn't indicate a problem.

//  Added Events
//  LockedCrate_Spawn
//  LockedCrate_HackStart
//  APC_Kill  
//  PatrolHeli_Kill
//  CH47_Kill

//  Added API
//  object OnBotReSpawnCrateDropped(HackableLockedCrate crate)  
//  object OnBotReSpawnCrateHackBegin(HackableLockedCrate crate)
//  object OnBotReSpawnAPCKill(BradleyAPC apc)
//  object OnBotReSpawnPatrolHeliKill(PatrolHelicopterAI heli)
//  object OnBotReSpawnCH47Kill(CH47HelicopterAIController ch)
//  object OnBotReSpawnAirdrop(SupplyDrop drop)

//  Notes
//  If you use Parent_Monument for a custom profile, but without custom spawnpoints, please use "Move Profile Here" after installing and before your next wipe.
//  If you used the Harbor profiles, you'll need to set them up again. They're now called Harbor_Large/_Small
//  If you used the small fishing village profiles, you'll need to set them up again. They're now called Fishing Village_A/_B/_C.


//  Changes in V1.0.7
//  Fixed toplayer console command.
//  Fixed inconsequential null reference error during server boot.
//  Fixed log file spam regarding navagent with stationary npcs. 
//  Fixed GiveRustReward error.
//  Added Ignore Sleepers.
//  Added Ignore Noob players. (based on Rust sash)
//  Added Ignore/Defend/Attack ZombieHorde.
//  Added Ignore/Defend/Attack HumanNPC. 
//  Added Ignore/Defend/Attack Other NPCs.
//  Added number for profile 'Faction'.
//  Added number for profile 'SubFaction'.
//  Added attack/ignore eachother, using 'Faction/SubFaction' as identifier.
//  Added CheckNav console command and UI button.
//  Added global RustRewards whole number true/false.
//  Added pagination for spawn points page.
//  Added Optional map-markers per profile.
//  Added Murderer breathing sound (true/false) per profile

//  Made APCs target + hurt BotReSpawn npcs, where APC_Safe is false.
//  Added toplayer announcement global config option, and lang message.
//  Separated out variations of Harbor Fishing Villages Ice lakes, mountains, substations, and swamps - Old profiles are auto-removed.
//      Take a backup for reference, if needed. 
//  Show Spawnpoints shows as red for points with no navmesh.

//  NPCs no longer pursue players after player death.
//  NPCs can now heal with syringes when line of sight is broken. 
//  NPCs can now throw grenades when line of sight is broken.
//  NPCs can now use - Flamethrowers, bows, rocket launchers, MGL, chainsaws, jackhammers, nailguns.
//  NPCs can now damage the armour you are wearing. (global config option)
//  NPCs will now headshot you, from time to time.
//  Increased randomisation of length of automatic weapon fire bursts.
//  Evened out npc damage across the board, so nothing should be OP now.
//  Added Melee_DamageScale and RangeWeapon_DamageScale per profile incase you liked them OP.
//  Added Global option to prevent using shorter range weapons over long range
//  Server Ai_Dormant is now respected. Sniping an npc will override. Global config option enables/disables.
//  Removed unneeded 'Radius' option from biome profiles.
//  Fixed biome npc respawn position not being randomised. 
//  Added selectable vanilla loot source.
//  Added Scale_Meds_To_Health global option
//  Made RustRewards value up/down increments bigger/smaller, depending on current number. (whole numbers only)


//  Changes in V1.0.8
//  bug fix


//  Changes in V1.0.9
//  Readded missing corpse dispenser skulls.
//  Auto-Disabled Target_Noobs false if NoSash plugin is installed.
//  Fixed faction/subfaction issue.
//  Fixed parachute destroy savelist issue.
//  Added global option Ignore_Factions - All profiles fight all profiles.
//  Fixed NPCs failing to return fire when constantly being hit.
//  Fixed stuttery melee npc chasing.
//  Fixed missing projectile traces/bullet holes.
//  Added ScarecrowNPC as a vanilla loot source.
//  Added OnBotReSpawnNPCKilled API.
//  Fixed "Reload Profile" not working for biomes.
//  Fixed NPC pursuit logic.
//  Fixed unintentionally long fire bursts with m249.
//  Added global reduce damage over distance option.
//  Added aggro memory duration global option.


//  Changes in V1.1.0 
//  Fixed issue with loot source selection and profiles with spaces in names.
//  Fixed missing ScarecrowNPC loot source option.
//  Added global Ignore_HackableCrates_With_OwnerID
//  More biome spawnpoints are found now, and much faster.
//  Made AutoSpawn:true profiles show as green in UI.
//  Fixed ocassional fail to return home - CONFIRM...
//  Fixed Hapis monument name checking error.


//  Changes in V1.1.1 
//  Fixed toplayer button in UI sending to user, not target.
//  Fixed startup performance issue.


//  Changes in V1.1.2
//  Performance/responsiveness improvements
//  Fixed collision for parachute npcs (arrows) @LizardMods
//  Fixed for Rust update changes to BaseAIBrain @LizardMods
//  Made npcs always safe from fire they created. @MooDDang
//  NPCs will get involved in nearby fights. 
//  Now throws c4 or satchels near players (like grenades)
//  Fixed head worn lamps issue. @406_Gromit
//  Added global Remove_Frankenstein_Parts option @MM617 
//  Added DM crates to Rust_Loot_Source @damnpixel
//  Added Global NPCs_Assist_NPCs - default true.
//  Changed < > button colour for kits in use - Easier to see with many kits.
//
//  Added per profile:
//  XPerienceValue @beepssy & @Somescrub
//  Immune_From_Damage_Beyond @Playerwtfa
//  Fire_Safe (for fire the npc didn't create) @MooDDang
//  Victim_Bleed_Amount_Per_Hit - default 1 @Covfefe
//  Victim_Bleed_Amount_Max - default 100 @Covfefe
//  Backpack_Duration - default 10 (minutes) @MooDDang


//  Changes in V1.1.3

//  Fixed.
//  Backpack destroy console warning.
//  Possible NRE in ProcessHumanNPC.
//  Issue with 0/0 faction npcs targeting others.
//  Attire and belt containers not showing in corpses, as of Rust update Sept 20th.

//  Added (Console Commands).
//  botrespawn tempspawn "Profile Name".
//  botrespawn enable/disable "Profile Name".

//  Added (Per Profile).
//  Assist_Sense_Range.
//  Roam_Pause_Length.
//  Off_Terrain default false. Allows roaming on custom monuments at sea.
//  Added Bleed_Amount_Is_Percent_Of_Damage - default false.
//  Vanilla scrap auto-added to various loot sources.
//  Random option for corpse loot source.
//  Rocket_DamageScale per profile for rockets + launched grenades.
//  'Save template' and 'Load template' buttons in UI.
//  Frankenstein clothing choices.

//  Added (Config).
//  XPerience_Whole_Numbers config option.

//  Altered
//  Automatic override of Chute true for events which occur undeground.
//  Made npc sense reaction range much shorter if silencer is used.
//  Skip usage of custom spawnpoints with no nearby nav when stationary is false, and notify in console of quantity skipped.
//  bots.count command now indicates temporary, or part temporary, spawn amounts.
//  Prevented npcs randomly ducking.
//  NPCS now ignite torches, under the same rules as head worn lamps and flashlights.

//  API
//  Added Enable_Targeting_Hook global option- default false.
//  Added OnBotReSpawnNPCTarget(ScientistNPC npc, BasePlayer player) hook.


//  Fixed in V1.1.5
//  Keep_Default_Loadout check only happens if kit is being given.
//  Vanilla attire container now gets wiped if frankenstein clothing is being applied. 
//  Respect_Safe_Zone npcs can't be hurt if they're in a safe zone.
//  Respect_Safe_Zone npcs should ignore players if the player, or the npc, is in a safe zone.
//  Removed unecessary/duplicate SaveData() calls.
//  Added Use_AlphaLoot false - Global config option
//  Patched for Rust update 01/12
//  Added teleport-to-profile-location button in UI.


//  Fixed in V1.1.6
//  Fix for event spawning issue.


//  Fixed in V1.1.7
//  Fixed AlphaLoot allow option, and changed default to setting to true


//  Changes in 1.1.8 
//  Alphabetised global settings.
//  Split global settings into two lists - true/false + numbers.

//  Made npcs use flashlights again, and favour melee at night when not in aggro.
//  Made npcs use geiger counters if available, and when not in aggro.

//  Added 'showspawns' and 'checknav' console commands for use in command-editing mode
//  Added Allow_Oilrigs option - Oilrig profile must use custom spawnpoints with stationary : true
//  Added Kits pagination, and more space for kit names
//  Added colour for kits in use, as per regular kits page, in the Spawnpoint overrides kits pages
//  Added NPCs_Attack_Animals per profile
//  Added Require_Two_Headshots as an additional option for Die_Instantly_From_Headshot : true
//  Added Parachute_From_Height to global config.
//  Added Smooth_Damage_Scale to global config
//  Added Friendly_Fire_Safe per profile.
//  Added ignore for Convoy.cs spawned APCs
//  Renamed Max_Chute_Fall_Speed to Chute_Speed_Variation
//  Added Chute_Fall_Speed 
//  Added documentation per setting - Just click the setting name/label.
//  Added npc population scaling against server population.Enable/Disable per-profile, and scaling thresholds in global settings.
//  Changed hackable crate ignore options to allow
//  Added Allow options for hack crates from Oilrig, and from CH47 drops.
//  Npcs now respond to rock/torch attacks from noobs when Target_Noobs = false
//  Added Peacekeeper_Uses_Damage - true means npcs will ignore hostile players unless they've hurt the npc.

//  Fixed issue with custom spawn point overrides not being taken into account during intial stationary/navmesh checks.
//  Fixes for underground-spawning events
//  Fixed NRE in TryThrow
//  Fixed NRE in OnEntityDeath
//  Fixed possibility of no room for Frankenstein head option.
//  Fixed Thompson rate of fire
//  Fixed possible NRE in NextTick in OnEntitySpawned
//  Fixed Respect_Safe_Zones setting ignoring all players, even when hostile.

//  Prevented npc friendly fire to helicopters
//  Prevented frankenstein attire from being lootable.
//  Prevented spawn Announcement_Text from happening where no npc can spawn

//  Reformatted UI profile buttons to prevent overlap due to new options.
//  Improved thrown explosive aim and timing.
//  Ability to parachute is now on per-npc basis behind the scenes.

//  Added API IsBotReSpawn(NPCPlayer npc)
//  Added API IsBotReSpawn(ulong id)
//  Added API NPCProfile(NPCPlayer npc)


//  Changes in 1.1.9 
//  Prevented players being able to add items to npc corpse inventories.
//  Added additional option Allow_All_Other_HackedCrates. Allows those not covered by other options to trigger event profiles. 
//  Improvements for victim bleed amounts and limits.
//  Randomised grenade throwing accuracy/timing slightly and added Precise_Grenade_Throws (false) to restore previous accuracy.
//  Fixes for animal target aim and damage.
//  Fixed lack of spread for buckshot. Players will be hit for less damage over greater distance.
//  Headshot % chance with buckshot is now fixed - It's in-line with other ammo types now.
//  Added random short delay for npcs responding to being hit from distance.
//  Fixed and improved accuracy drop-off over distance.
//  Changed OverWater to OffTerrain - Set true for any profile which is not directly on open terrain.


//  Changes in 1.2.0
//  Reduced maximum throwable distance.
//  Limited use of throwables based on npc/target height difference.
//  Added profile-wide grenade-throw cooldown, to prevent multiple npcs all throwing at the same time.
//  Removed Precise_Grenade_Throws in favour of Grenade_Precision_Percent per profile - Default = 50.
//  Fixed event profiles not checking for overhead obstacles (rocks/buildings).
//  Removed hardcoded limit on monuments of the same kind.
//  Fixed spas shotgun not being considered a short-range weapon.


//  Changes in 1.2.1
//  Small fix for Dont_Fire_Beyond_Distance.
//  Keep_Default_Loadout now prevents npcs getting defaults, rather than clearing afterwards.
//  Potential rare NRE fix - NullReferenceException: Object reference not set to an instance of an object at Oxide.Plugins.BotReSpawn.SelectWeapon ....
//  Fixed day/night inconsistency.


//  Changes in 1.2.2
//  Fix to allow for negative RustRewardsValue
//  Fix for setting Turret_Safe true requiring plugin reload. 
//  Fix for PatrolHeli event.


//  Changes in 1.2.3
//  New parachute models.
//  Performance improvement.
//  Added options to block events, by name, per default profile.
//  Added radius for blocking of events near default profiles.


//  Changes in 1.2.4
//  Minor bug fix.


/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */