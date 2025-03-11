using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MyEcoRecycler", "EcoSmile", "1.0.10")]
    class MyEcoRecycler : RustPlugin
    {
        [PluginReference] Plugin NoEscape;
        static MyEcoRecycler ins;
        PluginConfig config;

        public class PluginConfig
        {
            [JsonProperty("Привилегии")]
            public Dictionary<string, Recyclspeed> permis;
            [JsonProperty("Команда")]
            public string command;
            [JsonProperty("Блокировать использование в зоне действия чужого шкафа?")]
            public bool noBuild;
            [JsonProperty("Блокировать при рейде?")]
            public bool RadBlock;
            [JsonProperty("Список предметов запрещенных к переработке")]
            public List<string> itemBlackList;
        }

        public class Recyclspeed
        {
            [JsonProperty("Скорость переработки")]
            public float speed;
            [JsonProperty("Кулдаун использования команды")]
            public float cooldown;
        }

        Dictionary<BasePlayer, float> cooldow = new Dictionary<BasePlayer, float>();

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {
                permis = new Dictionary<string, Recyclspeed>()
                {
                    ["myecorecycler.defaul"] = new Recyclspeed
                    {
                        speed = 6,
                        cooldown = 10f
                    },
                    ["myecorecycler.vip"] = new Recyclspeed
                    {
                        speed = 3,
                        cooldown = 5f
                    },
                    ["myecorecycler.prem"] = new Recyclspeed
                    {
                        speed = 1,
                        cooldown = 3f
                    },
                },
                itemBlackList = new List<string>()
                {
                    "",""
                },
                command = "/rec",
                noBuild = true,
                RadBlock = true
            }
            ;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        private void OnServerInitialized()
        {
            ins = this;
            LoadConfig();
            LoadMessages();

            foreach (var perm in config.permis.Keys.ToList())
                permission.RegisterPermission(perm, this);

            cmd.AddChatCommand(config.command.Replace("/", ""), this, "cmd_recycler");

            ColdownInit();
        }

        void Unload()
        {
            var keys = recOpen.Keys.ToList();

            for (int i = recOpen.Count - 1; i >= 0; i--)
                RecClose(keys[i]);
        }

        void cmd_recycler(BasePlayer player)
        {
            if (!CanPlayerRecycle(player)) return;
            if (cooldow.ContainsKey(player))
            {
                SendReply(player, string.Format(GetMsg("Cooldown", player), cooldow[player]));
                return;
            }
            if (config.RadBlock)
            {
                var isRaid = NoEscape?.Call("IsRaidBlock", player.userID);
                if (isRaid == null)
                {
                    var umodNE = NoEscape?.Call("IsRaidBlocked", player);
                    if (umodNE != null && (bool)umodNE)
                    {
                        SendReply(player, GetMsg("RaidBlock", player));
                        return;
                    }
                }
                else if (isRaid != null && (bool)isRaid)
                {
                    SendReply(player, GetMsg("RaidBlock", player));
                    return;
                }
            }
            if (RecClose(player)) return;
            if (player.inventory.loot?.entitySource != null) return;
            timer.Once(0.2f, () =>
            {
                if (!player.IsOnGround()) return;
                MyRecycler rec = MyRecycler.Spawn(player);
                recOpen.Add(player, rec);
                rec.StartLoot();
            }
            );
        }
        void ColdownInit()
        {
            timer.Every(1f, () =>
            {
                foreach (var key in cooldow.Keys.ToList())
                {
                    cooldow[key]--;
                    if (cooldow[key] <= 0) cooldow.Remove(key);
                }
            }
            );
        }
        public Dictionary<BasePlayer, MyRecycler> recOpen = new Dictionary<BasePlayer, MyRecycler>();
        bool RecClose(BasePlayer player)
        {
            var data = recData.FirstOrDefault(x => x.Value == player);
            if (data.Key != null)
                recData.Remove(data.Key);

            MyRecycler rec;
            if (!recOpen.TryGetValue(player, out rec)) return false;
            recOpen.Remove(player);
            if (rec == null) return false;
            foreach (var it in rec.GetItems) player.GiveItem(it, BaseEntity.GiveItemReason.PickedUp);
            rec.Close();
            var cooldown = GetCooldown(player.UserIDString);
            cooldow.Add(player, cooldown);
            return true;
        }

        object OnRecycleItem(Item item, Recycler recycler) => OnItemRecycle(item, recycler);

        object OnItemRecycle(Item item, Recycler recycler)
        {
            if (!recData.ContainsKey(recycler)) return null;

            if (config.itemBlackList.Contains(item.info.shortname))
            {
                recycler.StopRecycling();
                return false;
            }

            return null;
        }

        void OnItemDropped(Item item, BaseEntity entity)
        {
            if (entity == null || item == null) return;
            var poolRec = Pool.GetList<Recycler>();
            Vis.Entities(entity.transform.position, 1f, poolRec);
            if (poolRec.Count > 0)
            {
                var myrec = poolRec.FirstOrDefault(x => x.GetComponent<MyRecycler>());
                if (myrec != null && myrec.GetComponent<MyRecycler>().owner != null)
                    entity.transform.position = myrec.GetComponent<MyRecycler>().owner.transform.position + Vector3.up * 2;
            }
            Pool.FreeList(ref poolRec);
        }

        object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainer, int targetSlot, int amount)
        {
            var player = playerLoot.GetComponent<BasePlayer>();
            if (player == null) return null;
            var recyclerIC = playerLoot.FindContainer(targetContainer);
            if (targetContainer.Value == 0)
            {

                NextTick(() =>
                {
                    recyclerIC = item.GetRootContainer();
                    if (recyclerIC == null) return;
                    Recycler recycler = recyclerIC.entityOwner as Recycler;
                    if (recycler == null) return;
                    targetSlot = item.position;
                    if (targetSlot > 5) return;
                    if (recycler.GetComponent<MyRecycler>() != null)
                    {
                        if (recycler.GetComponent<Recycler>() == null)
                        {
                            var speed = GetSpeed(player);
                            recycler.gameObject.AddComponent<Recycler>().StartRecycling();
                            recycler.CancelInvoke(new Action(recycler.GetComponent<Recycler>().RecycleThink));
                            recycler.InvokeRepeating(new Action(recycler.GetComponent<Recycler>().RecycleThink), speed, speed);
                        }
                        else
                        {
                            var speed = GetSpeed(player);
                            recycler.GetComponent<Recycler>().StartRecycling();
                            recycler.CancelInvoke(new Action(recycler.GetComponent<Recycler>().RecycleThink));
                            recycler.InvokeRepeating(new Action(recycler.GetComponent<Recycler>().RecycleThink), speed, speed);
                        }
                    }

                });

                return null;
            }
            else
            {
                if (recyclerIC == null) return null;
                var reply = 0;
                if (reply == 0) { }
                Recycler recycler = recyclerIC.entityOwner as Recycler;
                if (recycler == null) return null;
                if (targetSlot > 5) return null;
                //if (config.itemBlackList.Contains(item.info.shortname)) return false;
                //if (recycler.inventory.itemList.Any(x => config.itemBlackList.Contains(x.info.shortname))) return null;
                if (recycler.GetComponent<MyRecycler>() != null)
                {
                    if (recycler.GetComponent<Recycler>() == null)
                    {
                        var speed = GetSpeed(player);
                        recycler.gameObject.AddComponent<Recycler>().StartRecycling();
                        recycler.CancelInvoke(new Action(recycler.GetComponent<Recycler>().RecycleThink));
                        recycler.InvokeRepeating(new Action(recycler.GetComponent<Recycler>().RecycleThink), speed, speed);
                    }
                    else
                    {
                        var speed = GetSpeed(player);
                        recycler.GetComponent<Recycler>().StartRecycling();
                        recycler.CancelInvoke(new Action(recycler.GetComponent<Recycler>().RecycleThink));
                        recycler.InvokeRepeating(new Action(recycler.GetComponent<Recycler>().RecycleThink), speed, speed);
                    }
                }
            }
            return null;
        }

        bool CanPlayerRecycle(BasePlayer player)
        {
            if (!HasPermis(player))
            {
                SendReply(player, GetMsg("noperm", player));
                return false;
            }
            if (config.noBuild && !player.CanBuild())
            {
                SendReply(player, GetMsg("nopriv", player));
                return false;
            }
            if (player.IsSwimming())
            {
                SendReply(player, GetMsg("Swimming", player));
                return false;
            }
            if (!player.IsOnGround())
            {
                SendReply(player, GetMsg("Falling", player));
                return false;
            }
            if (player.IsFlying)
            {
                SendReply(player, GetMsg("Falling", player));
                return false;
            }
            if (player.IsWounded())
            {
                SendReply(player, GetMsg("Wounded", player));
                return false;
            }
            return true;
        }
        Dictionary<Recycler, BasePlayer> recData = new Dictionary<Recycler, BasePlayer>();
        public class MyRecycler : MonoBehaviour
        {
            public Recycler recycler;
            public BasePlayer owner;
            public void Init(Recycler rec, BasePlayer owner)
            {
                this.recycler = rec;
                this.owner = owner;
            }
            public static MyRecycler Spawn(BasePlayer player)
            {
                player.EndLooting();
                var storage = SpawnRec(player);
                var rec = storage.gameObject.AddComponent<MyRecycler>();
                rec.Init(storage, player);
                return rec;
            }
            public static Recycler SpawnRec(BasePlayer player)
            {
                var pos = player.transform.position;
                pos -= new Vector3(0, 300, 0);
                return SpawnRec(player, pos);
            }
            private static Recycler SpawnRec(BasePlayer player, Vector3 position, ulong playerid = 0)
            {
                Recycler rec = GameManager.server.CreateEntity("assets/bundled/prefabs/static/recycler_static.prefab") as Recycler;
                if (rec == null) return null;
                rec.transform.position = position;
                rec.SendMessage("SetDeployedBy", player, (SendMessageOptions)1);
                rec.globalBroadcast = true;
                rec.Spawn();
                ins.recData[rec] = player;
                return rec;
            }
            private void PlayerStoppedLooting(BasePlayer player) => ins.RecClose(player);
            public void Close()
            {
                recycler.StopRecycling();
                ClearItems();
                recycler.Kill();
            }
            public void StartLoot()
            {
                recycler.SetFlag(BaseEntity.Flags.Open, true, false);
                owner.inventory.loot.StartLootingEntity(recycler, false);
                owner.inventory.loot.AddContainer(recycler.inventory);
                owner.inventory.loot.SendImmediate();
                owner.ClientRPC(RpcTarget.Player("RPC_OpenLootPanel", owner), recycler.panelName);
                recycler.SendNetworkUpdate();
            }
            public void ClearItems() => recycler.inventory.itemList.Clear();

            internal BasePlayer GetPlayer()
            {
                return owner;
            }

            public List<Item> GetItems => recycler.inventory.itemList.Where(i => i != null).ToList();
        }
        private float GetSpeed(BasePlayer player)
        {
            float privilage = 5;
            foreach (var pri in config.permis)
            {
                if (permission.UserHasPermission(player.UserIDString, pri.Key))
                {
                    privilage = pri.Value.speed;
                }
            }
            return privilage;
        }
        private float GetCooldown(string player)
        {
            float privilage = 10;
            foreach (var pri in config.permis)
            {
                if (permission.UserHasPermission(player, pri.Key))
                {
                    privilage = pri.Value.cooldown;
                }
            }
            return privilage;
        }
        bool HasPermis(BasePlayer player)
        {
            bool enable = false;
            foreach (var pri in config.permis.Keys)
            {
                if (permission.UserHasPermission(player.UserIDString, pri))
                {
                    enable = true;
                }
            }
            return enable;
        }
        string GetMsg(string key, BasePlayer player = null)
        {
            return lang.GetMessage(key, this, player == null ? null : player.UserIDString);
        }
        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> {
                    {
                    "noperm", "You don't have permission to use this command."
                }
                , {
                    "nopriv", "Cannot be used in buildings blocked."
                }
                , {
                    "Swimming", "Can not be used when swimming."
                }
                , {
                    "Falling", "Can't be used in a falling."
                }
                , {
                    "Wounded", "Cannot be used when you are wounded."
                }
                , {
                    "Cooldown", "Recycler cooldown! \nYou need to wait {0} sec."
                }
                , {
                    "RaidBlock", "Can't be used in raid!"
                }
            }
            , this);
            lang.RegisterMessages(new Dictionary<string, string> {
                    {
                    "noperm", "Вы не можете использовать данную команду."
                }
                , {
                    "nopriv", "Нельзя использовать в зоне чужого дома."
                }
                , {
                    "Swimming", "Нельзя использовать в воде."
                }
                , {
                    "Falling", "Нельзя использовать в падении."
                }
                , {
                    "Wounded", "Нельзя использовать когда вы ранены."
                }
                , {
                    "Cooldown", "Переработчик перезагружается! \nОсталось подождать {0} сек."
                }
                , {
                    "RaidBlock", "Запрещено использовать переработчик в рейде!"
                }
            }
            , this, "ru");
        }
    }
}