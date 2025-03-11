using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core;
using System.Reflection;
using UnityEngine.SceneManagement;
using Facepunch;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("CrystalFarm", "https://discord.gg/TrJ7jnS233", "1.0.2")]
    [Description("CrystalFarm")]
    public class CrystalFarm : RustPlugin
    {
        public static CrystalFarm instance;
        public ConfigData configData;

        public class ConfigData
        {
            public int spawnChance = 100;
            public string name = "Кристалл";
            public string message = "Тебе улыбнулась удача и ты смог найти редкую породу руды!";
            public int timeBetweenFazesInSeconds = 600;
            public float harvestMultiplier = 0.5f;
        }

        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData();
            SaveConfig(configData);
        }

        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        PluginData pluginData;

        class PluginData
        {
            public Dictionary<NetworkableId, MagicCrystalItemConfig> trees = new Dictionary<NetworkableId, MagicCrystalItemConfig>();
        }

        /*void SaveData() 
        {
            Interface.Oxide.DataFileSystem.WriteObject(this.Title, pluginData);
        }*/

        public class MagicCrystalItemConfig
        {
            public int faze = 0;
            public string ShortPrefabName;
            public Dictionary<string, float> location = new Dictionary<string, float>();
            [JsonIgnore] public MagicCrystalItem tree;
        }

        public class MagicCrystalItem : MonoBehaviour
        {
            public MagicCrystalItemConfig config = new MagicCrystalItemConfig();
            public BaseEntity entity;
            public StagedResourceEntity stagedResourceEntity;
            public ResourceDispenser dispenser;

            void Awake()
            {
                if (this.config.location.Count < 1)
                {
                    config.location.Add("x", this.transform.position.x);
                    config.location.Add("y", this.transform.position.y);
                    config.location.Add("z", this.transform.position.z);
                }

                this.config.tree = this;
                this.entity = this.gameObject.GetComponent<BaseEntity>();
                this.entity.enableSaving = false;
                this.config.ShortPrefabName = this.entity.ShortPrefabName; /*if (this.gameObject.GetComponent<Spawnable>()) UnityEngine.Object.Destroy(this.gameObject.GetComponent<Spawnable>());*/
                stagedResourceEntity = this.gameObject.GetComponent<StagedResourceEntity>();
                dispenser = this.gameObject.GetComponent<ResourceDispenser>();
            }

            public void InitFaze(int faze)
            {
                this.config.faze = faze;
                GetComponent<StagedResourceEntity>().health = 500 * (1f - 0.25f * faze);
                stagedResourceEntity.stage = faze;
                stagedResourceEntity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                for (int index = 0; index < stagedResourceEntity.stages.Count; ++index) stagedResourceEntity.stages[index].instance.SetActive(index == stagedResourceEntity.stage);
                GroundWatch.PhysicsChanged(stagedResourceEntity.gameObject);
                if (faze == 0) Effect.server.Run("assets/bundled/prefabs/fx/ore_break.prefab", entity.transform.position);
                foreach (ItemAmount containedItem in dispenser.containedItems)
                {
                    if ((double)containedItem.amount > 0.0) containedItem.amount = containedItem.startAmount * (1f - 0.25f * faze);
                }

                float num1 = dispenser.containedItems.Sum<ItemAmount>(x => x.startAmount);
                float num2 = dispenser.containedItems.Sum<ItemAmount>(x => x.amount);
                if (num1 == 0f) dispenser.fractionRemaining = 0f;
                else dispenser.fractionRemaining = num2 / num1;
                dispenser.UpdateRemainingCategories();
                if (faze > 0) dispenser.maxDestroyFractionForFinishBonus = -0.2f;
                else dispenser.maxDestroyFractionForFinishBonus = 0.2f;
                if (faze > 0)
                {
                    Invoke("SwitchFaze", instance.configData.timeBetweenFazesInSeconds);
                }
            }

            public void SwitchFaze()
            {
                this.config.faze--;
                this.InitFaze(this.config.faze);
                instance.pluginData.trees[entity.net.ID] = this.config;
                
                // Отправляем уведомление только когда кристалл полностью созрел (faze = 0)
                if (this.config.faze == 0)
                {
                    var players = BasePlayer.activePlayerList.Where(p => Vector3.Distance(p.transform.position, this.transform.position) <= 30f);
                    foreach (var player in players)
                    {
                        player.Command("gametip.showtoast", 1, "<size=14>Ваш кристалл созрел и готов к добыче!</size>", 0);
                    }
                }
            }

            public void OnDestroy()
            {
            }
        }

        void Init()
        {
            instance = this;
            configData = Config.ReadObject<ConfigData>();
            SaveConfig(configData);

            pluginData = new PluginData();

            foreach (var p in treePrefabs)
            {
                treePrefabsList.Add(p.Value);
            }
        }

        void Unload()
        {
            //SaveData();
            foreach (var x in pluginData.trees)
            {
                UnityEngine.Object.Destroy(x.Value.tree);
            }
        }

        static readonly Dictionary<string, string> treePrefabs = new Dictionary<string, string>() { { "ore_sulfur", "assets/bundled/prefabs/radtown/ore_sulfur.prefab" }, { "ore_metal", "assets/bundled/prefabs/radtown/ore_metal.prefab" }, };
        static readonly List<string> treePrefabsList = new List<string>();

        void OnServerInitialized()
        {
            PrintWarning("\n-----------------------------\n" +
            "     Author - https://devplugins.ru/\n" +
            "     VK - https://vk.com/dev.plugin\n" +
            "     Discord - https://discord.gg/eHXBY8hyUJ\n" +
            "-----------------------------");
            cmd.AddConsoleCommand("givecrystal", this, "cmdGive");
            /*var trees = pluginData.trees.Values.ToArray();
            pluginData.trees.Clear();
            foreach (var x in trees)
            {
                BaseEntity tree  = InstantiatePrefab(treePrefabs[x.ShortPrefabName], new Vector3(x.location["x"], x.location["y"], x.location["z"]), new Quaternion());
                var        mtree = tree.gameObject.AddComponent<MagicCrystalItem>();
                mtree.InitFaze(x.faze);
                tree.Spawn(); var planter = GetPlanterBox(tree.transform.position); tree.transform.parent = planter.transform;
                pluginData.trees.Add(tree.net.ID, mtree.config);
            }*/
        }

        /*void OnNewSave()
        {
            pluginData.trees.Clear();
            SaveData();
        }

        void OnServerSave()
        {
            SaveData();
        }*/

        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BaseEntity ore = dispenser.GetComponent<BaseEntity>();
            if (ore != null && pluginData.trees.ContainsKey(ore.net.ID) || ore.OwnerID == 9985442254)
            {
                var cookable = item.info.GetComponent<ItemModCookable>();
                item.info = cookable.becomeOnCooked;
                item.amount = (int)(item.amount * configData.harvestMultiplier);
                if (pluginData.trees.ContainsKey(ore.net.ID) && pluginData.trees[ore.net.ID].faze > 0)
                {
                    ore.OwnerID = 9985442254;
                    pluginData.trees[ore.net.ID].tree.CancelInvoke("SwitchFaze");
                    GameObject.Destroy(pluginData.trees[ore.net.ID].tree);
                    pluginData.trees.Remove(ore.net.ID);
                }
            }
        }

        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (item.info.shortname == "wood") return;

            BaseEntity entity = dispenser.GetComponent<BaseEntity>();
            if (entity != null && pluginData.trees.ContainsKey(entity.net.ID) || entity.OwnerID == 9985442254)
            {
                entity.OwnerID = 9985442254;
                var cookable = item.info.GetComponent<ItemModCookable>();
                item.info = cookable.becomeOnCooked;
                item.amount = (int)(item.amount * configData.harvestMultiplier);
                return;
            }

            int rnd = UnityEngine.Random.Range(0, 100);
            if (rnd <= configData.spawnChance && entity.OwnerID != 2222444)
            {
                entity.OwnerID = 2222444;
                GiveTree(player);
                player.Command("gametip.showtoast", 2, $"<size=14>{configData.message}</size>", 0);
            }
        }

        void OnEntityBuilt(Planner plan, GameObject obj)
        {
            if (plan == null || obj == null) return;

            var entity = obj.GetComponent<BaseEntity>();
            if (entity != null && entity.ShortPrefabName == "corn.entity" && entity.skinID == 2226183438)
            {
                var list = new List<BaseEntity>();
                Vis.Entities(entity.transform.position, 0.2f, list);

                if (list.Any(p => p.PrefabName.Contains("ore")))
                {
                    var player = plan.GetOwnerPlayer();
                    NextTick(() =>
                    {
                        entity?.Kill();
                    });
                    GiveTree(player);
                    return;
                }

                plugins.Find("Quest")?.Call("AddEXP", plan.GetOwnerPlayer(), 0.01f, "За посадку кристала");
                BaseEntity crystal = InstantiatePrefab(treePrefabsList.GetRandom(), entity.transform.position, new Quaternion());
                NextTick(() =>
                {
                    entity?.Kill();
                });

                var mtree = crystal.gameObject.AddComponent<MagicCrystalItem>();
                mtree.InitFaze(3);
                crystal.Spawn();
                pluginData.trees.Add(crystal.net.ID, mtree.config);
            }
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null || entity.net == null) return;
            if (pluginData == null || pluginData.trees == null) return;

            if (entity is PlanterBox)
            {
                var list = new List<BaseEntity>();
                Vis.Entities(entity.transform.position, 1.5f, list);

                foreach (var check in list.Where(p => p.PrefabName.Contains("ore_")))
                    check.Kill();
            }

            if (pluginData.trees.ContainsKey(entity.net.ID)) pluginData.trees.Remove(entity.net.ID);
        }

        void GiveTree(BasePlayer player)
        {
            Item item = ItemManager.CreateByName("seed.corn", 1, 2226183438);
            item.name = configData.name;

            if (!player.inventory.GiveItem(item))
            {
                item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity, new Quaternion());
            }
        }

        void cmdGive(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player() ?? null;
            if (player?.net.connection.authLevel < 2) return;
            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "bad syntax");
                return;
            }

            BasePlayer targetPlayer = BasePlayer.Find(arg.Args[0]);
            if (targetPlayer == null)
            {
                SendReply(arg, "error player not found for give");
                return;
            }

            GiveTree(targetPlayer);
        }

        public BaseEntity GetPlanterBox(Vector3 position)
        {
            RaycastHit hitInfo;
            Physics.Raycast(position + new Vector3(0f, 3f, 0f), Vector3.down, out hitInfo, 3f, constructions, QueryTriggerInteraction.Ignore);
            if (hitInfo.collider == null || !(hitInfo.GetEntity() is PlanterBox)) return null;
            return hitInfo.GetEntity();
        }

        BaseEntity InstantiatePrefab(string prefabname, Vector3 position, Quaternion rotation)
        {
            var prefab = GameManager.server.FindPrefab(prefabname);
            GameObject gameObject = Instantiate.GameObject(prefab, position, rotation);
            gameObject.name = prefabname;
            SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);
            if (gameObject.GetComponent<Spawnable>()) UnityEngine.Object.Destroy(gameObject.GetComponent<Spawnable>());
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            BaseEntity component = gameObject.GetComponent<BaseEntity>();
            return component;
        }

        public bool IsOutside(BaseEntity entity)
        {
            OBB obb = entity.WorldSpaceBounds();
            return IsOutside(obb.position + obb.up * obb.extents.y);
        }

        private static int constructions = LayerMask.GetMask("Construction", "Deployable", "Prevent Building", "Deployed");

        public bool IsOutside(Vector3 position)
        {
            return !UnityEngine.Physics.Raycast(position, Vector3.up, 100f, 1101070337) && !UnityEngine.Physics.Raycast(position, Vector3.down, 3f, constructions);
        }

        private string msg(string key, BasePlayer player = null) => lang.GetMessage(key, this, player?.UserIDString);

        private void SendMsg(BasePlayer player, string langkey, bool title = true, params string[] args)
        {
            string message = String.Format(msg(langkey, player), args);
            SendReply(player, message);
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>() { { "denyPlant", "You can plant your crystal only in plantation!" } }, this);
            lang.RegisterMessages(new Dictionary<string, string>() { { "denyPlant", "Ты можешь посадить свой кристалл только на плантации!" } }, this, "ru");
        }
    }
}