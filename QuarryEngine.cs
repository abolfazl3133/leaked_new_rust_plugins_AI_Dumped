using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using Newtonsoft.Json.Converters;
using Facepunch;
using VLB;

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rust.Modular;
using Network;

namespace Oxide.Plugins
{ 
    [Info("QuarryEngine", "EcoSmile", "2.0.5")]
    class QuarryEngine : RustPlugin
    {
        static QuarryEngine ins;
        PluginConfig config;

        [JsonConverter(typeof(StringEnumConverter))]
        public enum Quality { Low, Medium, Height }
        [JsonConverter(typeof(StringEnumConverter))]
        public enum EngineType { Solo, Duo }

        public class PluginConfig
        {
            [JsonProperty("Запретить открывать двигатель без авторизации в шкафу?")]
            public bool ReqBp = true;
            [JsonProperty("Стандартное количество Карьеров для установки")]
            public int StandartQuarry = 1;
            [JsonProperty("Стандартное количество Нефтекачек для установки")]
            public int StandartPump = 1;
            [JsonProperty("Кастомные привилегии - Рейты добычи")]
            public Dictionary<string, float> PrivelageRates = new Dictionary<string, float>()
            {
                ["quarryengine.vip"] = 2f,
                ["quarryengine.premium"] = 3f,
                ["quarryengine.gold"] = 4f,
            };
            [JsonProperty("Кастомные привилегии - Лимит на постройку Карьеров")]
            public Dictionary<string, int> BuildLimitQuarry = new Dictionary<string, int>()
            {
                ["quarryengine.vip"] = 2,
                ["quarryengine.premium"] = 3,
                ["quarryengine.gold"] = 4,
            };
            [JsonProperty("Кастомные привилегии - Лимит на постройку Нефтекачек")]
            public Dictionary<string, int> BuildLimitPump = new Dictionary<string, int>()
            {
                ["quarryengine.vip"] = 2,
                ["quarryengine.premium"] = 3,
                ["quarryengine.gold"] = 4,
            };
            [JsonProperty("Прифаб - шанс спавна карьера")]
            public Dictionary<string, int> QuarrySpawnChance = new Dictionary<string, int>()
            {
                ["crate_elite"] = 15,
                ["bradley_crate"] = 30,
                ["heli_crate"] = 40,
            };
            [JsonProperty("Прифаб - шанс спавна геозаряда")]
            public Dictionary<string, SurvAmount> SurvaySpawnChance = new Dictionary<string, SurvAmount>()
            {
                ["crate_elite"] = new SurvAmount { },
                ["bradley_crate"] = new SurvAmount { },
                ["heli_crate"] = new SurvAmount { },
            };
            [JsonProperty("Прифаб - шанс спавна Нефтекачки")]
            public Dictionary<string, int> OilPumpSpawnChance = new Dictionary<string, int>()
            {
                ["crate_elite"] = 15,
                ["bradley_crate"] = 30,
                ["heli_crate"] = 40,
            };
            [JsonProperty("Настройка карьера")]
            public QuarrySetting quarrySetting;
            [JsonProperty("Настройка нефтяных лунок")]
            public PumpSetting pumpSetting;
            [JsonProperty("Настройка работы карьера (Качество предмета - настройки)")]
            public Dictionary<Quality, ConditionSetting> ItemProperties;
        }

        public class SurvAmount
        {
            public float Chance = 30;
            public int MinAmount = 1;
            public int MaxAmount = 2;
        }


        public class QuarrySetting
        {
            [JsonProperty("Тип двигателя карьера (Solo - 5 Слотов двигателя, Duo - 8 Слотов двигателя)")]
            public EngineType engineType;
            [JsonProperty("Шанс спавна ресурсной лунки")]
            public float spawnChance;
            [JsonProperty("Максимальное количество уникальных ресурсов в лунке")]
            public int MaxResourceAmount;
            [JsonProperty("Минимальное количество уникальных ресурсов в лунке")]
            public int MinResourceAmount;
            [JsonProperty("Настройка ресурсов в лунке")]
            public List<DepostiResource> DepositInfo;
        }

        public class PumpSetting
        {
            [JsonProperty("Тип двигателя карьера (Solo - 5 Слотов двигателя, Duo - 8 Слотов двигателя)")]
            public EngineType engineType;
            [JsonProperty("Шанс спавна нефтяной лунки")]
            public float spawnChance;
            [JsonProperty("Настройка нефти в лунке")]
            public DepostiResource depostiResource;
        }

        public class ConditionSetting
        {
            [JsonProperty("Потеря прочности единиц в секунду")]
            public float LoosCondition;
            [JsonProperty("Бонус к эффективности добычи от ОДНОГО компонента в % (Значения меньше 100% снижают скорость)")]
            public float SpeedBoost;
        }

        public class DepostiResource
        {
            [JsonProperty("Шортнейм предмета")]
            public string ShortName;
            [JsonProperty("Максимальное содержания ресурса в лунке")]
            public int MaxitemAmount;
            [JsonProperty("Минимальное содержания ресурса в лунке")]
            public int MinitemAmount;
            [JsonProperty("Предмет является жидкостью?")]
            public bool IsLiquid;
            [JsonProperty("Сколько тиков требуется для добычи")]
            public float TickAmount;
        }

        public class CraterDeposit
        {
            public string ShortName;
            public int ItemAmount;
            public bool IsLiquid;
            public float ToNeedWork;
            public float TickAmount;
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {
                ReqBp = true,
                quarrySetting = new QuarrySetting()
                {
                    engineType = EngineType.Duo,
                    spawnChance = 15f,
                    MinResourceAmount = 1,
                    MaxResourceAmount = 5,
                    DepositInfo = new List<DepostiResource>()
                    {
                        new DepostiResource
                        {
                            ShortName = "stones",
                            MaxitemAmount = 5,
                            MinitemAmount = 1,
                            TickAmount = 1f,
                            IsLiquid = false
                        },
                        new DepostiResource
                        {
                            ShortName = "metal.ore",
                            MaxitemAmount = 5,
                            MinitemAmount = 1,
                            TickAmount = 1f,
                            IsLiquid = false
                        },
                        new DepostiResource
                        {
                            ShortName = "metal.fragments",
                            MaxitemAmount = 5,
                            MinitemAmount = 1,
                            TickAmount = 1f,
                            IsLiquid = false
                        },
                        new DepostiResource
                        {
                            ShortName = "sulfur.ore",
                            MaxitemAmount = 3,
                            MinitemAmount = 1,
                            TickAmount = 1f,
                            IsLiquid = false
                        },
                        new DepostiResource
                        {
                            ShortName = "hq.metal.ore",
                            MaxitemAmount = 1,
                            MinitemAmount = 1,
                            TickAmount = 4f,
                            IsLiquid = false
                        },
                    }
                },
                pumpSetting = new PumpSetting
                {
                    engineType = EngineType.Solo,
                    spawnChance = 15f,
                    depostiResource = new DepostiResource()
                    {
                        ShortName = "crude.oil",
                        MaxitemAmount = 3,
                        MinitemAmount = 1,
                        TickAmount = 2f,
                        IsLiquid = true
                    }
                },
                ItemProperties = new Dictionary<Quality, ConditionSetting>()
                {
                    [Quality.Low] = new ConditionSetting
                    {
                        LoosCondition = 1f,
                        SpeedBoost = 95f,
                    },
                    [Quality.Medium] = new ConditionSetting
                    {
                        LoosCondition = 0.6f,
                        SpeedBoost = 110f,
                    },
                    [Quality.Height] = new ConditionSetting
                    {
                        LoosCondition = 0.5f,
                        SpeedBoost = 150f,
                    }
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            LoadData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        public class UserData
        {
            public int Quarry = 0;
            public int Pumjack = 0;
        }
        void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name + "/QuarryData"))
                quarryData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, QuarryData>>(Name + "/QuarryData");
            else
                Interface.Oxide.DataFileSystem.WriteObject(Name + "/QuarryData", quarryData = new Dictionary<ulong, QuarryData>());

            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name + "/UsersData"))
                buildingData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, UserData>>(Name + "/UsersData");
            else
                Interface.Oxide.DataFileSystem.WriteObject(Name + "/UsersData", buildingData = new Dictionary<ulong, UserData>());
        }

        Dictionary<ulong, QuarryData> quarryData = new Dictionary<ulong, QuarryData>();
        Dictionary<ulong, UserData> buildingData = new Dictionary<ulong, UserData>();

        public class QuarryData
        {
            public List<EngineItem> engineItems;
            public List<CraterDeposit> depostiResources;
        }

        public class EngineItem
        {
            public string Shortname;
            public float Condition;
            public float Maxcodition;
            public Quality Quality;
        }

        private void OnServerInitialized()
        {
            ins = this;

            foreach (var perm in config.BuildLimitPump)
                if (!permission.PermissionExists(perm.Key, this))
                    permission.RegisterPermission(perm.Key, this);

            foreach (var perm in config.BuildLimitQuarry)
                if (!permission.PermissionExists(perm.Key, this))
                    permission.RegisterPermission(perm.Key, this);


            foreach (var perm in config.PrivelageRates)
                if (!permission.PermissionExists(perm.Key, this))
                    permission.RegisterPermission(perm.Key, this);

            foreach (var entity in BaseNetworkable.serverEntities.OfType<MiningQuarry>())
                if (quarryData.ContainsKey(entity.net.ID.Value))
                {
                    if (!entity.GetComponent<QuarryEngineComponent>())
                        entity.gameObject.AddComponent<QuarryEngineComponent>();
                }
        }

        void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/QuarryData", quarryData);
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/UsersData", buildingData);

            var objects = UnityEngine.Object.FindObjectsOfType<QuarryEngineComponent>();
            foreach (var obj in objects)
                UnityEngine.Object.Destroy(obj);

            var craters = BaseNetworkable.serverEntities.OfType<SurveyCrater>();
            foreach (var item in craters)
                item.Kill();

            foreach (BasePlayer pl in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(pl, "Engine.BTN");
            }
        }

        void OnServerSave()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/QuarryData", quarryData);
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/UsersData", buildingData);

        }

        void OnNewSave()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/UsersData", buildingData = new Dictionary<ulong, UserData>());
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/QuarryData", quarryData = new Dictionary<ulong, QuarryData>());
        }
        [PluginReference]
        Plugin Remove;
        object OnHammerHit(BasePlayer player, HitInfo info)
        {
            var entity = info.HitEntity;
            if (entity == null) return null;
            if (Remove?.Call<bool>("OnRemoveActivate", player.userID) == true) return null;
            var engine = entity.GetComponent<QuarryEngineComponent>();
            if (engine != null)
            {
                if (entity.HasFlag(BaseEntity.Flags.On))
                {
                    SendReply(player, $"Нельзя открывать двигатель при работающем карьере");
                    return false;
                }
                if (config.ReqBp && player.IsBuildingBlocked()) return null;
                engine.StartLootEngine(player);
            }


            return null;
        }

        void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            if (!quarryData.ContainsKey(quarry.net.ID.Value)) return;
            var rates = GetUserRate(quarry.OwnerID.ToString());
            item.amount = (int)Math.Ceiling(item.amount * rates);
        }

        private float GetUserRate(string ownerID)
        {
            float rate = 1f;
            foreach (var perm in config.PrivelageRates)
            {
                if (permission.UserHasPermission(ownerID, perm.Key) && rate < perm.Value)
                    rate = perm.Value;
            }
            return rate;
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            var player = plan.GetOwnerPlayer();
            var entity = go.ToBaseEntity();
            if (!(entity is MiningQuarry)) return;
            if (entity.ShortPrefabName == "mining_quarry")
            {
                if (!buildingData.ContainsKey(player.userID))
                    buildingData[player.userID] = new UserData();

                buildingData[player.userID].Quarry++;
                var limit = GetQuarryLimit(player);
                SendReply(player, $"Осталось установок Карьеров: {limit - buildingData[player.userID].Quarry} шт.");
            }
            if (entity.ShortPrefabName == "mining.pumpjack")
            {
                if (!buildingData.ContainsKey(player.userID))
                    buildingData[player.userID] = new UserData();

                buildingData[player.userID].Pumjack++;
                var limit = GetPumjackLimit(player);

                SendReply(player, $"Осталось установок Нефтекачек: {limit - buildingData[player.userID].Pumjack} шт.");
            }
            if (entity is MiningQuarry)
            {
                entity.OwnerID = player.userID;
            }
        }

        object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (planner == null || prefab == null || planner.GetOwnerPlayer()?.GetActiveItem() == null) return null;
            var player = planner.GetOwnerPlayer();

            if (prefab.fullName.Contains("mining_quarry"))
            {
                if (!buildingData.ContainsKey(player.userID))
                    buildingData[player.userID] = new UserData();

                var data = buildingData[player.userID];
                var limit = GetQuarryLimit(player);
                if (data.Quarry >= limit)
                {
                    SendReply(player, $"Вы не можете установить карьер т.к. превышен лимит. Ваш лимит {limit} карьер(ов).");
                    return false;
                }
            }

            if (prefab.fullName.Contains("mining.pumpjack"))
            {
                if (!buildingData.ContainsKey(player.userID))
                    buildingData[player.userID] = new UserData();

                var limit = GetQuarryLimit(player);
                if (buildingData[player.userID].Pumjack >= limit)
                {
                    SendReply(player, $"Вы не можете установить нефтекачку т.к. превышен лимит. Ваш лимит {limit} нефтекачка(ек).");
                    return false;
                }
            }

            return null;
        }

        private int GetQuarryLimit(BasePlayer player)
        {
            int limit = config.StandartQuarry;
            foreach (var perm in config.BuildLimitQuarry)
            {
                if (permission.UserHasPermission(player.UserIDString, perm.Key) && limit < perm.Value)
                    limit = perm.Value;
            }
            return limit;
        }

        private int GetPumjackLimit(BasePlayer player)
        {
            int limit = config.StandartPump;

            foreach (var perm in config.BuildLimitPump)
            {
                if (permission.UserHasPermission(player.UserIDString, perm.Key) && limit < perm.Value)
                    limit = perm.Value;
            }
            return limit;
        }

        void OnQuarryToggled(MiningQuarry quarry, BasePlayer player)
        {
            var engine = quarry.GetComponent<QuarryEngineComponent>();

            if (quarry.HasFlag(BaseEntity.Flags.On) && engine)
            {
                if (!engine.CanStartEngine())
                {
                    NextTick(() => quarry.SetOn(false));
                    SendReply(player, $"Карьер не может быть запущен - нехватает запчастей в двигателе\nУдарьте киянкой по карьру чтобы открыть двигатель");
                    return;
                }
            }
        }
        List<LootContainer> handledContainers = new List<LootContainer>();

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (!(entity is LootContainer))
                return;
            if (!config.OilPumpSpawnChance.ContainsKey(entity.ShortPrefabName) && !config.QuarrySpawnChance.ContainsKey(entity.ShortPrefabName) && !config.SurvaySpawnChance.ContainsKey(entity.ShortPrefabName)) return;

            var container = (LootContainer)entity;
            if (handledContainers.Contains(container) || container.ShortPrefabName == "stocking_large_deployed" || container.ShortPrefabName == "stocking_small_deployed") return;
            handledContainers.Add(container);
            var chance = UnityEngine.Random.value * 100f;
            if (config.QuarrySpawnChance[entity.ShortPrefabName] >= chance)
            {
                var it = ItemManager.CreateByName("mining.quarry");
                if (container.inventory.capacity == container.inventory.itemList.Count)
                    container.inventory.capacity++;
                container.SendNetworkUpdate();
                it.MoveToContainer(container.inventory);
            }
            chance = UnityEngine.Random.value * 100f;
            if (config.OilPumpSpawnChance[entity.ShortPrefabName] >= chance)
            {
                var it = ItemManager.CreateByName("mining.pumpjack");
                if (container.inventory.capacity == container.inventory.itemList.Count)
                    container.inventory.capacity++;
                container.SendNetworkUpdate();
                it.MoveToContainer(container.inventory);
            }
            chance = UnityEngine.Random.value * 100f;
            if (config.SurvaySpawnChance[entity.ShortPrefabName].Chance >= chance)
            {
                var amount = UnityEngine.Random.Range(config.SurvaySpawnChance[entity.ShortPrefabName].MinAmount, config.SurvaySpawnChance[entity.ShortPrefabName].MaxAmount + 1);
                var it = ItemManager.CreateByName("surveycharge", amount);
                if (container.inventory.capacity == container.inventory.itemList.Count)
                    container.inventory.capacity++;
                container.SendNetworkUpdate();
                it.MoveToContainer(container.inventory);
            }
        }
        void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            var recyclerEngine = entity.GetComponentInParent<QuarryEngineComponent>();
            var reply = 0;
            if (entity.ShortPrefabName.Contains("engine") && recyclerEngine)
            {
                if (recyclerEngine.engineLooters.Contains(player))
                    recyclerEngine.engineLooters.Remove(player);
                CuiHelper.DestroyUi(player, "Engine.BTN");
                recyclerEngine.SaveItems();
            }
            if (reply == 0) { }
        }

        object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainer, int targetSlot, int amount)
        {
            var player = playerLoot.GetComponent<BasePlayer>();
            var container = playerLoot.FindContainer(targetContainer);
            if (container != null)
            {
                var entity = container.entityOwner;
                if (entity != null && entity.ShortPrefabName.Contains("engine"))
                {
                    var engine = entity.GetComponentInParent<QuarryEngineComponent>();
                    if (!engine) return null;
                    NextTick(() => engine.DrawEngineBtn(player, true));
                }
            }
            var itContainer = item.GetRootContainer();
            if (itContainer != null)
            {
                var entity = player.inventory.loot.entitySource;
                if (entity != null && entity.ShortPrefabName.Contains("engine"))
                {
                    var engine = entity.GetComponentInParent<QuarryEngineComponent>();
                    if (!engine) return null;
                    NextTick(() => engine.DrawEngineBtn(player, true));
                }
            }
            return null;
        }

        private void OnExplosiveDropped(BasePlayer player, BaseEntity entity) => OnExplosiveThrown(player, entity);

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (!(entity is SurveyCharge)) return;
            entity.gameObject.AddComponent<SurveyComp>();
        }

        private void OnAnalysisComplete(SurveyCrater crater, BasePlayer player)
        {
            if (player == null || crater == null) return;
            if (crater.GetComponent<CraterInfo>() == null) return;
            var deposit = crater.GetComponent<CraterInfo>().depostis;
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("- Mineral Analysis -");
            stringBuilder.AppendLine();
            foreach (var resource in deposit)
            {
                var ptick = resource.ItemAmount / resource.TickAmount;
                var perMin = ptick * 7;
                var displayName = ItemManager.FindItemDefinition(resource.ShortName).displayName.english;
                stringBuilder.AppendLine($"{displayName} : ~{perMin:0.00} pM");
            }
            var noteItem = ItemManager.CreateByName("note");
            noteItem.text = stringBuilder.ToString();
            player.GiveItem(noteItem, BaseEntity.GiveItemReason.PickedUp);
        }


        public class CraterInfo : FacepunchBehaviour
        {
            SurveyCrater crater;
            void Awake()
            {
                crater = GetComponent<SurveyCrater>();
            }
            public List<CraterDeposit> depostis = new List<CraterDeposit>();
            public void SaveDepositInfo(List<CraterDeposit> resources) => depostis = resources;

            public void SetDepositInfo(MiningQuarry quarry)
            {
                ins.quarryData.Add(quarry.net.ID.Value, new QuarryData { engineItems = new List<EngineItem>(), depostiResources = new List<CraterDeposit>() });
                foreach (var item in depostis)
                {
                    var workNeed = 1f / item.ItemAmount;
                    workNeed *= item.TickAmount;
                    ins.quarryData[quarry.net.ID.Value].depostiResources.Add(new CraterDeposit { ShortName = item.ShortName, ItemAmount = item.ItemAmount, IsLiquid = item.IsLiquid, ToNeedWork = workNeed, TickAmount = item.TickAmount });
                }
            }

            void OnDestroy()
            {
                List<MiningQuarry> quarries = new List<MiningQuarry>();
                Vis.Entities(crater.transform.position, 1f, quarries);
                if (quarries.Count <= 0) return;
                var quarry = quarries[0];
                if (ins.quarryData.ContainsKey(quarry.net.ID.Value)) return;
                SetDepositInfo(quarry);
                quarry.gameObject.AddComponent<QuarryEngineComponent>();
            }
        }

        List<string> craterType = new List<string>()
        {
            "oil", "resource"
        };

        public class SurveyComp : FacepunchBehaviour
        {
            SurveyCharge charge;
            void Awake()
            {
                charge = GetComponent<SurveyCharge>();
            }

            void OnDestroy()
            {
                List<BaseEntity> baseEntities = new List<BaseEntity>();
                Vis.Entities(charge.transform.position, 1f, baseEntities);
                foreach (var crater in baseEntities)
                {
                    if (crater.ShortPrefabName == "survey_crater")
                        crater.Kill();
                }
                baseEntities.Clear();
                if (baseEntities.Where(x => x != null && !x.IsDestroyed).Count() > 0) return;
                var resType = ins.craterType.GetRandom();
                var spawnChance = UnityEngine.Random.Range(0, 100);
                if (resType == "resource" && spawnChance <= ins.config.quarrySetting.spawnChance)
                {
                    var depositList = new List<CraterDeposit>();
                    int maxTry = 20;
                    var ResourceCount = UnityEngine.Random.Range(ins.config.quarrySetting.MinResourceAmount, ins.config.quarrySetting.MaxResourceAmount + 1);
                    for (int i = 0; i < ResourceCount; i++)
                    {
                        if (maxTry <= 0)
                            break;

                        var deposit = ins.config.quarrySetting.DepositInfo.GetRandom();
                        if (depositList.Any(x => x.ShortName == deposit.ShortName))
                        {
                            maxTry--;
                            i--;
                            continue;
                        }
                        CraterDeposit craterDeposit = new CraterDeposit();
                        craterDeposit.ShortName = deposit.ShortName;
                        craterDeposit.ItemAmount = UnityEngine.Random.Range(deposit.MinitemAmount, deposit.MaxitemAmount + 1);
                        craterDeposit.IsLiquid = deposit.IsLiquid;
                        craterDeposit.TickAmount = deposit.TickAmount;
                        craterDeposit.ToNeedWork = 0;
                        depositList.Add(craterDeposit);
                    }

                    SurveyCrater crate = GameManager.server.CreateEntity("assets/prefabs/tools/surveycharge/survey_crater.prefab", charge.transform.position) as SurveyCrater;
                    crate.Spawn();
                    Vector3 modifiedAimConeDirection = global::AimConeUtil.GetModifiedAimConeDirection(20f, Vector3.up, true);
                    foreach (var item in depositList)
                    {
                        ItemManager.CreateByName(item.ShortName, item.ItemAmount).Drop(crate.transform.position + Vector3.up * 1f, crate.GetInheritedDropVelocity() + modifiedAimConeDirection * UnityEngine.Random.Range(5f, 10f), UnityEngine.Random.rotation).SetAngularVelocity(UnityEngine.Random.rotation.eulerAngles * 5f);
                    }
                    var crateInfo = crate.gameObject.AddComponent<CraterInfo>();
                    crateInfo.SaveDepositInfo(depositList);
                }

                if (resType == "oil" && spawnChance <= ins.config.pumpSetting.spawnChance)
                {
                    SurveyCrater crate = GameManager.server.CreateEntity("assets/prefabs/tools/surveycharge/survey_crater_oil.prefab", charge.transform.position) as SurveyCrater;
                    crate.Spawn();
                    var depositList = new List<CraterDeposit>();
                    CraterDeposit craterDeposit = new CraterDeposit();
                    craterDeposit.ShortName = ins.config.pumpSetting.depostiResource.ShortName;
                    craterDeposit.ItemAmount = UnityEngine.Random.Range(ins.config.pumpSetting.depostiResource.MinitemAmount, ins.config.pumpSetting.depostiResource.MaxitemAmount + 1);
                    craterDeposit.IsLiquid = ins.config.pumpSetting.depostiResource.IsLiquid;
                    craterDeposit.TickAmount = ins.config.pumpSetting.depostiResource.TickAmount;
                    craterDeposit.ToNeedWork = 0;
                    depositList.Add(craterDeposit);
                    Vector3 modifiedAimConeDirection = global::AimConeUtil.GetModifiedAimConeDirection(20f, Vector3.up, true);
                    foreach (var item in depositList)
                    {
                        ItemManager.CreateByName(item.ShortName, item.ItemAmount).Drop(crate.transform.position + Vector3.up * 1f, crate.GetInheritedDropVelocity() + modifiedAimConeDirection * UnityEngine.Random.Range(5f, 10f), UnityEngine.Random.rotation).SetAngularVelocity(UnityEngine.Random.rotation.eulerAngles * 5f);
                    }
                    var crateInfo = crate.gameObject.AddComponent<CraterInfo>();
                    crateInfo.SaveDepositInfo(depositList);
                }
            }
        }

        public class QuarryEngineComponent : FacepunchBehaviour
        {
            private MiningQuarry quarry;
            private ModularCar car;
            private BaseVehicleModule firstEngine;
            private BaseVehicleModule secondEngine;

            private EngineStorage firstEngineStorage;
            private EngineStorage secondEngineStorage;
            private EngineType engineType;
            public List<BasePlayer> engineLooters = new List<BasePlayer>();
            ulong ownerID = 0;
            void Awake()
            {
                quarry = GetComponent<MiningQuarry>();
                ownerID = quarry.OwnerID;
                NetID = quarry.net.ID.Value;
                quarry._linkedDeposit._resources.Clear();
                foreach (var item in ins.quarryData[quarry.net.ID.Value].depostiResources)
                {
                    var workNeed = 1f / item.ItemAmount;
                    workNeed *= item.TickAmount;
                    quarry._linkedDeposit.Add(ItemManager.FindItemDefinition(item.ShortName), 1f, item.ItemAmount, item.ToNeedWork, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, item.IsLiquid);
                }
                engineType = quarry.ShortPrefabName == "mining.pumpjack" ? ins.config.pumpSetting.engineType : ins.config.quarrySetting.engineType;

                quarry.workToAdd = 1 * GetRecyclerSpeed();
                car.AdminKill();

                quarry.CancelInvoke(quarry.ProcessResources);
                quarry.processRate = quarry.workToAdd;
                quarry.InvokeRepeating(quarry.ProcessResources, quarry.processRate, quarry.processRate);

                if (quarry.IsOn())
                {
                    quarry.SetOn(false);

                    if (CanStartEngine())
                    {
                        Invoke(() => quarry.SetOn(true), 1f);
                    }
                }
            }

            void CrateCar(ulong playerid = 0)
            {
                if (car != null) return;
                car = GameManager.server.CreateEntity("assets/content/vehicles/modularcar/2module_car_spawned.entity.prefab") as ModularCar;
                car.spawnSettings.useSpawnSettings = false;
                car.GetComponent<Rigidbody>().isKinematic = true;
                car.GetComponent<Rigidbody>().useGravity = false;
                car.SetParent(quarry);
                car.transform.position = quarry.transform.position + Vector3.down * 100f;
                car.enableSaving = false;
                car.immuneToDecay = true;
                car.Spawn();

                var Module1 = ItemManager.CreateByName("vehicle.1mod.cockpit.with.engine");
                car.TryAddModule(Module1, 0);
                var Module2 = ItemManager.CreateByName("vehicle.1mod.engine");
                car.TryAddModule(Module2, 1);

                car.ModuleEntityAdded(car.GetComponentsInChildren<BaseVehicleModule>()[0]);
                car.ModuleEntityAdded(car.GetComponentsInChildren<BaseVehicleModule>()[1]);

                foreach (var ent in car.GetComponentsInChildren<BaseVehicleModule>())
                {
                    if (ent.ShortPrefabName == "1module_cockpit_with_engine")
                    {
                        ent.enableSaving = false;
                        firstEngine = ent;
                    }
                    else
                    {
                        ent.enableSaving = false;
                        secondEngine = ent;
                    }
                }

                firstEngineStorage = firstEngine.GetComponentInChildren<EngineStorage>();
                secondEngineStorage = secondEngine.GetComponentInChildren<EngineStorage>();

                return;
            }

            public void StartLootEngine(BasePlayer player)
            {
                player.EndLooting();

                CrateCar();

                if (!player.net.subscriber.IsSubscribed(car.net.group))
                    player.net.subscriber.Subscribe(car.net.group);

                if (engineType == EngineType.Solo)
                {
                    SendEntity(player, firstEngine);
                    SendEntity(player, firstEngineStorage);

                    FillEngine(firstEngineStorage);
                    firstEngineStorage.PlayerOpenLoot(player, firstEngineStorage.panelName, false);
                }
                else
                {
                    SendEntity(player, secondEngine);
                    SendEntity(player, secondEngineStorage);

                    FillEngine(secondEngineStorage);
                    secondEngineStorage.PlayerOpenLoot(player, secondEngineStorage.panelName, false);
                }
                DrawEngineBtn(player, false);
            }

            public void DrawEngineBtn(BasePlayer player, bool AsMove)
            {
                CuiHelper.DestroyUi(player, "Engine.BTN");
                if (!engineLooters.Contains(player))
                    engineLooters.Add(player);

                var container = new CuiElementContainer();
                container.Add(new CuiElement()
                {
                    Parent = "Hud.Menu",
                    Name = "Engine.BTN",
                    Components =
                    {
                        new CuiImageComponent{Color = "0 0 0 0"},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "193 16", OffsetMax = "421 97"}
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = "Engine.BTN",
                    Name = "Fuel.Btn",
                    Components =
                    {
                        new CuiButtonComponent{Color = "0.815 0.776 0.741 0.05", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat", Command = $""},
                        new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-114 1", OffsetMax = "114 41"}
                    }
                });

                container.Add(new CuiElement()
                {
                    Parent = "Fuel.Btn",
                    Name = "Fuel.Btn.Text﻿​﻿​",
                    Components =
                    {
                        new CuiTextComponent{Color = "0.815 0.776 0.741 0.55", Text = $"Current Quarry performance: x{ (AsMove ? Math.Round(GetSpeedAsMove(), 1) : Math.Round(GetRecyclerSpeed(), 1)) * 100f}%", Align = TextAnchor.MiddleCenter, FontSize = 14},
                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
                });
                CuiHelper.AddUi(player, container);
            }

            public void SendEntity(BasePlayer a, BaseEntity b)
            {
                if (!Net.sv.IsConnected())
                    return;

                NetWrite netWrite = Net.sv.StartWrite();

                a.net.connection.validate.entityUpdates++;
                BaseNetworkable.SaveInfo c = new BaseNetworkable.SaveInfo
                {
                    forConnection = a.net.connection,
                    forDisk = false
                };

                netWrite.PacketID(Message.Type.Entities);
                netWrite.UInt32(a.net.connection.validate.entityUpdates);
                b.ToStreamForNetwork(netWrite, c);
                netWrite.Send(new SendInfo(a.net.connection));

            }

            void FillEngine(EngineStorage storage)
            {
                storage.inventory.itemList.Clear();
                var itemList = ins.quarryData[quarry.net.ID.Value].engineItems;
                if (itemList.Count == 0) return;
                foreach (var item in itemList)
                {
                    Item it = ItemManager.CreateByName(item.Shortname);
                    it.condition = item.Condition;
                    if (item.Maxcodition == null || item.Maxcodition == 0)
                        item.Maxcodition = it.maxCondition;
                    it.maxCondition = item.Maxcodition;
                    var slot = GetValidSlot(it, storage);
                    if (slot == -1)
                    {
                        Debug.LogError($"Error to Adding item {it.info.shortname} slot = -1");
                        continue;
                    }
                    it.MoveToContainer(storage.inventory, slot);
                }
            }

            public float GetRecyclerSpeed()
            {
                CrateCar();
                float currentSpedBonus = 1f;
                foreach (var it in ins.quarryData[quarry.net.ID.Value].engineItems)
                {
                    var bonus = ins.config.ItemProperties[it.Quality].SpeedBoost / 100f;
                    currentSpedBonus += bonus;
                }

                float averageBonus = currentSpedBonus / (engineType == EngineType.Solo ? firstEngineStorage.inventory.capacity : secondEngineStorage.inventory.capacity);

                return averageBonus;
            }

            public float GetSpeedAsMove()
            {
                EngineStorage storage = engineType == EngineType.Solo ? firstEngineStorage : secondEngineStorage;
                if (storage == null) return 0;
                float currentSpedBonus = 0f;
                foreach (var it in storage.inventory.itemList)
                {
                    var quality = it.info.shortname.Contains("1") ? Quality.Low : it.info.shortname.Contains("2") ? Quality.Medium : Quality.Height;
                    var bonus = ins.config.ItemProperties[quality].SpeedBoost / 100f;
                    currentSpedBonus += bonus;
                }
                float averageBonus = currentSpedBonus / storage.inventory.capacity;
                return averageBonus;
            }

            public void SaveItems()
            {
                var itemList = ins.quarryData[quarry.net.ID.Value].engineItems;
                itemList.Clear();
                var InventoryList = engineType == EngineType.Solo ? firstEngineStorage.inventory.itemList : secondEngineStorage.inventory.itemList;
                foreach (var item in InventoryList)
                {
                    EngineItem engineItem = new EngineItem();
                    engineItem.Condition = item.condition;
                    engineItem.Shortname = item.info.shortname;
                    engineItem.Maxcodition = item.maxCondition;
                    engineItem.Quality = item.info.shortname.Contains("1") ? Quality.Low : item.info.shortname.Contains("2") ? Quality.Medium : Quality.Height;
                    itemList.Add(engineItem);
                }
                quarry.workToAdd = 1 * GetRecyclerSpeed();
                quarry.CancelInvoke(new Action(quarry.ProcessResources));
                quarry.processRate = quarry.workToAdd;
                quarry.InvokeRepeating(new Action(quarry.ProcessResources), quarry.processRate, quarry.processRate);
                car.AdminKill();
            }

            private int GetValidSlot(Item item, EngineStorage engineStorage)
            {
                ItemModEngineItem component = item.info.GetComponent<ItemModEngineItem>();
                if (component == null)
                {
                    return -1;
                }
                EngineStorage.EngineItemTypes engineItemType = component.engineItemType;
                for (int i = 0; i < engineStorage.inventorySlots; i++)
                {
                    if (component.engineItemType == engineStorage.slotTypes[i] && !engineStorage.inventory.SlotTaken(item, i))
                    {
                        return i;
                    }
                }
                return -1;
            }

            public bool CanStartEngine()
            {
                return engineType == EngineType.Solo && ins.quarryData[quarry.net.ID.Value].engineItems.Count == 5 || engineType == EngineType.Duo && ins.quarryData[quarry.net.ID.Value].engineItems.Count == 8;
            }

            void FixedUpdate()
            {
                if (quarry == null) return;
                if (!quarry.HasFlag(BaseEntity.Flags.On)) return;
                if (!CanStartEngine())
                {
                    quarry.pendingWork = 0;
                    quarry.SetOn(false);
                    return;
                }
                if (engineLooters.Count > 0)
                {
                    foreach (var player in engineLooters.ToList())
                    {
                        player.EndLooting();
                        engineLooters.Remove(player);
                    }
                }
                var engineItem = ins.quarryData[quarry.net.ID.Value].engineItems.GetRandom();
                var looseValue = ins.config.ItemProperties[engineItem.Quality].LoosCondition * Time.fixedDeltaTime;
                engineItem.Condition -= looseValue;
                if (engineItem.Condition <= 0)
                {
                    ins.quarryData[quarry.net.ID.Value].engineItems.Remove(engineItem);
                    quarry.SetOn(false);
                }
            }

            ulong NetID;
            void OnDestroy()
            {
                if (ins.buildingData.ContainsKey(ownerID) && (ins.buildingData[ownerID].Quarry > 0 || ins.buildingData[ownerID].Pumjack > 0))
                {
                    if (quarry.PrefabName.Contains("mining.pumpjack") && ins.buildingData[ownerID].Pumjack > 0)
                    {
                        ins.buildingData[ownerID].Pumjack--;
                    }
                    else if (quarry.PrefabName.Contains("mining_quarry") && ins.buildingData[ownerID].Quarry > 0)
                    {
                        ins.buildingData[ownerID].Quarry--;
                    }
                }
                if (quarry != null && quarry.IsDead())
                {
                    foreach (var item in ins.quarryData[NetID].engineItems)
                    {
                        Item it = ItemManager.CreateByName(item.Shortname);
                        it.Drop(quarry.transform.position, new Vector3());
                    }
                    ins.quarryData.Remove(NetID);
                }

                if (firstEngineStorage != null && !firstEngineStorage.IsDestroyed)
                {
                    firstEngineStorage?.inventory?.itemList.Clear();
                }

                if (secondEngineStorage != null && !secondEngineStorage.IsDestroyed)
                    secondEngineStorage?.inventory?.itemList.Clear();

                if (car != null && !car.IsDestroyed)
                    car.AdminKill();
            }
        }
    }
}
