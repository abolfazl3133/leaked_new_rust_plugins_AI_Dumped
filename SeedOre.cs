using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using System.Runtime.InteropServices;

namespace Oxide.Plugins
{
    [Info("SeedOre", "LAGZYA", "1.0.9")]
    public class SeedOre : RustPlugin
    {

        [PluginReference] Plugin ImageLibrary;

        #region cfg
        private ConfigData cfg { get; set; }

        private class ConfigData
        {
            [JsonProperty("Скин айди семечки")] public ulong skinId = 1923097247;

            [JsonProperty("Время роста одной стадии")]
            public int cd = 30;
 
            [JsonProperty("Шанс выпадение руды при добыче")]
            public int random = 10;
 
            [JsonProperty("Рейты добычи?")]
            public float xd = 1.5f;
            [JsonProperty("Плавить ресурсы при добыче?")]
            public bool cook = true;
            [JsonProperty("Разрешить ставить ток в грядке??")]
            public bool planted = false;
            [JsonProperty("Список руд. Которые могут появится.")]
            public List<string> itemList;


            public static ConfigData GetNewConf()
            {
                var newConfig = new ConfigData();
                newConfig.itemList = new List<string>()
                {
                    "assets/bundled/prefabs/autospawn/resource/ores/sulfur-ore.prefab",
                    "assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab"
                };
                return newConfig;
            }
        }

        protected override void LoadDefaultConfig() => cfg = ConfigData.GetNewConf();

        protected override void SaveConfig() => Config.WriteObject(cfg);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                cfg = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        #endregion

        #region Data

        List<ulong> _oreList = new List<ulong>();

        #endregion

        object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || player == null || item == null) return null;
            if (dispenser.GetComponent<ResourceEntity>()?.skinID != 21382131) return null;
            if(cfg.cook)
            {
                if (item.info.gameObject.GetComponent<ItemModCookable>() == null || item.info.gameObject.GetComponent<ItemModCookable>().becomeOnCooked == null)
                {
                    item.amount = (int) (item.amount * cfg.xd);
                    return null;
                }
                var itemGive = ItemManager.Create(item.info.gameObject.GetComponent<ItemModCookable>().becomeOnCooked,
                    1);
                itemGive.amount = (int) (item.amount * cfg.xd);
                player.GiveItem(itemGive, BaseEntity.GiveItemReason.ResourceHarvested);
                return true;
            }
            item.amount = (int) (item.amount * cfg.xd);
            return null;
        }

        private CuiElementContainer container;

        object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || player == null || item == null) return null;
            if (dispenser.GetComponent<ResourceEntity>()?.skinID == 21382131)
            {
                if (cfg.cook)
                {
                    if (item.info.gameObject.GetComponent<ItemModCookable>() == null || item.info.gameObject.GetComponent<ItemModCookable>().becomeOnCooked == null)
                    {
                        item.amount = (int) (item.amount * cfg.xd);
                        return null;
                    }
                    var itemGive = ItemManager.Create(item.info.gameObject.GetComponent<ItemModCookable>().becomeOnCooked, 1);
                    itemGive.amount = (int) (item.amount * cfg.xd);  
                    return itemGive;
                } 
                item.amount = (int) (item.amount * cfg.xd);
                return item;
            }
            if (item.info.shortname == "stones" || item.info.shortname == "metal.ore" ||
                item.info.shortname == "sulfur.ore")
            {
                var random = Core.Random.Range(0f, 100f);
                if (random > cfg.random) return null;
                EffectNetwork.Send(new Effect("assets/prefabs/misc/xmas/presents/effects/unwrap.prefab", player, 0, Vector3.up, Vector3.zero)
                {
                    scale = UnityEngine.Random.Range(0f, 1f)
                });
                var giveItem = ItemManager.CreateByName("seed.corn", 1, cfg.skinId);

                Server.Command($"gametipsapi.showforplayer {player.userID} 0 Вот это везение! Вам выпало семечко руды!");

                giveItem.name = "Семечко руды";
                if (!player.inventory.GiveItem(giveItem))
                    giveItem.Drop(player.inventory.containerMain.dropPosition,
                        player.inventory.containerMain.dropVelocity);
            }

            return null;
        }

        void Unload()
        {
            if(start !=null) Global.Runner.StopCoroutine(start);
            foreach (var resourceSeed in UnityEngine.Object.FindObjectsOfType<ResourceSeed>()) resourceSeed.OnDestroy();
            Interface.Oxide.DataFileSystem.WriteObject("SeedOre", _oreList);
        }

        [PluginReference] private Plugin StacksExtended, CustomSkinsStacksFix, StackModifier;
        private Item OnItemSplit(Item item, int amount)
        {
            if (StacksExtended || CustomSkinsStacksFix || StackModifier) return null;
            if (amount <= 0) return null;
            if (item.skin != cfg.skinId) return null;
            item.amount -= amount;
            var newItem = ItemManager.Create(item.info, amount, item.skin);
            newItem.name = item.name;
            newItem.skin = item.skin;
            newItem.amount = amount;
            item.GetOwnerPlayer()?.SendNetworkUpdate();
            return newItem;
        }

        private object CanCombineDroppedItem(WorldItem first, WorldItem second)
        {
            return CanStackItem(first.item, second.item);
        }

        object CanStackItem(Item item, Item targetItem)
        {
            if (item.skin == cfg.skinId && targetItem.skin == cfg.skinId) return true;
            return null;
        }

        List<ulong> oreList = new List<ulong>();

        private Coroutine start;
        private void OnServerInitialized()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("SeedOre")) _oreList = Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>("SeedOre");
            if(cfg.planted) Subscribe("CanBuild");
            else
            {
                Unsubscribe("CanBuild");
            }
        }

        void Init()
        {
            ins = this;
        }

        public static SeedOre ins;

        class ResourceSeed : MonoBehaviour
        {
            private ResourceEntity ore;
            private ulong netId;
            private float health;
            private BaseEntity parent;

            private void Awake()
            {
                ore = GetComponent<ResourceEntity>();
                netId = ore.net.ID.Value;
                health = ore.health;
                InvokeRepeating("UpdateStage", ins.cfg.cd, ins.cfg.cd);
            }

            public void OnDestroy()
            {
                Destroy(this);
            }

            void UpdateStage()
            {
                if (ore == null)
                {
                    OnDestroy(); 
                    ins._oreList.Remove(netId);
                    return;
                }

                if (health != ore.health)
                {
                    OnDestroy();
                    ins._oreList.Remove(netId);
                    return;
                }

                if (ore.Health() + 150 > 500)
                {
                    ore.OnAttacked(new HitInfo(new BasePlayer(), ore, DamageType.Generic, -150, ore.transform.position));
                    health = ore.health;
                    ore.health = 500;
                    ore.GetComponent<OreResourceEntity>().RespawnBonus();
                    OnDestroy();
                    ins._oreList.Remove(netId);
                    return;
                }

                ore.OnAttacked(new HitInfo(new BasePlayer(), ore, DamageType.Generic, -150, ore.transform.position));
                health = ore.health;
            }
        }

        object CanBuild(Planner planner, Construction prefan, Construction.Target target)
        {
            if (planner.skinID != cfg.skinId || !prefan.fullName.Contains("corn.entity")) return null;
            if (target.entity == null) 
            {
                ReplySend(planner.GetOwnerPlayer(), "Посадите семечко в грядку!");
                return false;
            }
            if (target.entity.ShortPrefabName != "planter.large.deployed" && target.entity.ShortPrefabName != "planter.small.deployed")
            {
                ReplySend(planner.GetOwnerPlayer(), "Посадите семечко в грядку!");
                return false;
            }
            return null;
        } 
        
        void OnEntitySpawned(GrowableEntity entity)
        {
            if (entity.skinID != cfg.skinId || entity.ShortPrefabName != "corn.entity") return;
                var player = BasePlayer.FindByID(entity.OwnerID);
                if (player == null) return;
                var ore = GameManager.server.CreateEntity(cfg.itemList.GetRandom(), entity.transform.position) as OreResourceEntity;
                ore.health = ore.stages[2].health;
                
                ore.stage = 2;
                ore.OnParentSpawning();
                var info = new HitInfo(new BasePlayer(), ore, DamageType.Generic, -1f, ore.transform.position);
                ore.OnAttacked(info); 
                NextTick(() =>
                {
                    if(entity.GetPlanter() != null) entity.GetPlanter().AddChild(ore);
                    entity.SetParent(ore, true, true);
                });
                ore.gameObject.AddComponent<ResourceSeed>();
                ore.skinID = 21382131;
                _oreList.Add(ore.net.ID.Value);
        }

        private void ReplySend(BasePlayer player, string message) => player.SendConsoleCommand("chat.add 0",
            new object[2]
                {76561199015371818, $"<size=12><color=#87CEFF>Руда</color></size>\n{message}"});

        [ChatCommand("giveseedore")] 
        void Update(BasePlayer player)
        {
            if(!player.IsAdmin) return;
            var item = ItemManager.CreateByName("seed.corn", 10, cfg.skinId);
            item.name = "Семечко руды";
            item.MoveToContainer(player.inventory.containerBelt);
        }
    } 
}
