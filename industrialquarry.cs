// Reference: Rust.Harmony
/*▄▄▄    ███▄ ▄███▓  ▄████  ▄▄▄██▀▀▀▓█████▄▄▄█████▓
▓█████▄ ▓██▒▀█▀ ██▒ ██▒ ▀█▒   ▒██   ▓█   ▀▓  ██▒ ▓▒
▒██▒ ▄██▓██    ▓██░▒██░▄▄▄░   ░██   ▒███  ▒ ▓██░ ▒░
▒██░█▀  ▒██    ▒██ ░▓█  ██▓▓██▄██▓  ▒▓█  ▄░ ▓██▓ ░ 
░▓█  ▀█▓▒██▒   ░██▒░▒▓███▀▒ ▓███▒   ░▒████▒ ▒██▒ ░ 
░▒▓███▀▒░ ▒░   ░  ░ ░▒   ▒  ▒▓▒▒░   ░░ ▒░ ░ ▒ ░░   
▒░▒   ░ ░  ░      ░  ░   ░  ▒ ░▒░    ░ ░  ░   ░    
 ░    ░ ░      ░   ░ ░   ░  ░ ░ ░      ░    ░      
 ░             ░         ░  ░   ░      ░  ░*/
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("industrialquarry", "bmgjet", "1.3.3")]
    [Description("Connect Industrial pipes up to the quarry.")]
    class industrialquarry : RustPlugin
    {
        private StoredData storedData;
        private class StoredData { public List<IQuarry2> LoadedIQuarrys = new List<IQuarry2>(); }
        private IHarmonyModHooks Harmony2_3;
        private bool oxide = true;
        private string FrameWork;
        private bool secondaryport = false;
        private readonly string[] Web = new string[] { "http://plugin.bmgjet.com/?plugin=", "http://plugin2.bmgjet.com:81/?plugin=" };

        //Permission
        private string permallow = "industrialquarry.allow"; //Permission required to use plugin

        //Strings
        private const string StorageAdapter = "assets/prefabs/deployable/playerioents/industrialadaptors/storageadaptor.deployed.prefab";
        private const string Conveyor = "assets/prefabs/deployable/playerioents/industrialconveyor/industrialconveyor.deployed.prefab";
        private const string SwitchPrefab = "assets/prefabs/deployable/playerioents/simpleswitch/switch.prefab";
        private const string QuarryRock = "assets/bundled/prefabs/modding/admin/admin_rock_quarry_small_a.prefab";
        private const string Crude = "crudeoutput";

        public class IQuarry2
        {
            public ulong OwnerID;
            public ulong ParentQuarry;
            public Vector3 ParentPosition;
            public ulong StoneNetID;
            public ulong FuelAdapterNetID0;
            public ulong FuelAdapterNetID1;
            public ulong ResourceAdapterNetID0;
            public ulong ResourceAdapterNetID1;
            public ulong SwitchNetID;
            public Vector3[] Pipes0;
            public Vector3[] Pipes1;
        }

        #region Configuration
        private PluginConfig _config;
        private class PluginConfig
        {
            [JsonProperty("OrderID: ")]
            public string OrderID = "Fuck bmgjet";
            [JsonProperty("Force Carbon Fallback")]
            public bool ForceCarbon = false;
            [JsonProperty("Output Debug Messages In Console")]
            public bool DebugMessages = false;
            [JsonProperty("Monument Quarry Building Block Size")]
            public float MbuildingBlock = 65;
            [JsonProperty("Underground Quarry Height Offset (Custom Maps)")]
            public float underGroundQuarryHeightOffset = 2;
            [JsonProperty("Underground Quarry Forward Offset (Custom Maps)")]
            public float underGroundQuarryForwardOffset = 1;
            [JsonProperty("Add a conveyor to the quarry output")]
            public bool AddOutput = true;
            [JsonProperty("Add a conveyor to the fuel tank")]
            public bool AddFuelInput = true;
            [JsonProperty("Add a IO On/Off Switch")]
            public bool AddIOSwitch = true;
            [JsonProperty("Attach to monument/map placed quarries")]
            public bool AllowOnStaticQuarrys = true;
            [JsonProperty("Adjust amount to move each tick, If set -1 will set default (128 default, quarries faster than that so raise to 256).")]
            public int MaxStackSizePerMoveAdustment = 256;
            [JsonProperty("Block players from switching off the conveyors on the quarry stone.")]
            public bool BlockSwitchingOffConveyors = true;
            [JsonProperty("Check for missing quarry parts every (sec)")]
            public int MissingPartsDelay = 60;
            [JsonProperty("Run CleanIQ on server restarts.")]
            public bool CleanonStart = false;

            public string ToJson() => JsonConvert.SerializeObject(this);
            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() { _config = new PluginConfig(); }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null) { throw new JsonException(); }

                if (!_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }
        protected override void SaveConfig()
        {
            PrintWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }
        #endregion Configuration

        #region Harmony Loader

        private string CheckFramework()
        {
            string prefix = (!_config.ForceCarbon) ? "Oxide(" : "Carbon(";
            var sb = new StringBuilder();
            oxide = (!_config.ForceCarbon ? true : false);
#if CARBON
            prefix = "Carbon(";
            oxide = false;
#endif
            var bytes = Encoding.ASCII.GetBytes(ConVar.Server.port + ":" + ConVar.Server.queryport);
            sb.Append(prefix);
            foreach (var t in bytes) { sb.Append(t.ToString("X2").ToLower()); }
            sb.Append(")");
            return sb.ToString();
        }

        public bool IsBase64String(string s) { s = s.Trim(); return (s.Length % 4 == 0) && System.Text.RegularExpressions.Regex.IsMatch(s, @"^[a-zA-Z0-9\+/]*={0,3}$", System.Text.RegularExpressions.RegexOptions.None); }

#endregion

        private void Init()
        {
            Unsubscribe("OnEntityKill");
            Unsubscribe("OnEntitySpawned");
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("industrialquarry");
            //Add permissions
            permission.RegisterPermission(permallow, this);
        }

        [ChatCommand("killiq")]
        private void ChatKillIndustrialQuarry(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin) //limit to admin
            {
                RaycastHit hit;
                var raycast = UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out hit, 10, -1);
                BaseEntity entity = raycast ? hit.GetEntity() : null;
                if (entity != null)
                {
                    entity.skinID = 0;
                    entity.Kill();
                }
            }
        }

        [ChatCommand("cleaniq")]
        private void ChatIndustrialQuarry(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin) //limit to admin
            {
                DoCleanUp();
                player.ChatMessage("Cleaned IQ Ents. Respawning in 5secs");
                ServerMgr.Instance.StartCoroutine(Startup(10));//restart plugin so it respawns and reconnects
            }
        }

        [ChatCommand("addiq")]
        private void ChatAddIndustrialQuarry(BasePlayer player, string command, string[] args)
        {
            //Check permission
            if (!permission.UserHasPermission(player.UserIDString, permallow))
            {
                player.ChatMessage("You Don't Have The Required Permission!");
                return;
            }

            //Find quarry player looking at
            MiningQuarry entity = FindQuarry(player.eyes.HeadRay());
            if (entity == null)
            {
                player.ChatMessage("No Quarry Found!");
                return;
            }

            //Check Owner
            if (entity.OwnerID != player.userID && !player.IsAdmin)
            {
                player.ChatMessage("You Don't Own This Quarry!");
                return;
            }

            //Check if it already has parts
            foreach (var q in storedData.LoadedIQuarrys)
            {
                if (q.ParentQuarry == entity.net.ID.Value)
                {
                    player.ChatMessage("Already Set As Industrial Quarry!");
                    return;
                }
            }

            //Add parts
            player.ChatMessage("Adding Industrial Parts.");
            AttachIndustrialParts(entity);
        }

        [ChatCommand("removeiq")]
        private void ChatRemoveIndustrialQuarry(BasePlayer player, string command, string[] args)
        {
            //Check permission
            if (!permission.UserHasPermission(player.UserIDString, permallow))
            {
                player.ChatMessage("You Don't Have The Required Permission!");
                return;
            }

            //Find quarry player looking at
            MiningQuarry entity = FindQuarry(player.eyes.HeadRay());
            if (entity == null)
            {
                player.ChatMessage("No Quarry Found!");
                return;
            }

            //Check Owner
            if (entity.OwnerID != player.userID && !player.IsAdmin)
            {
                player.ChatMessage("You Don't Own This Quarry!");
                return;
            }

            //Check if it already has parts
            foreach (var q in storedData.LoadedIQuarrys)
            {
                if (q.ParentQuarry == entity.net.ID.Value)
                {
                    player.ChatMessage("Removing Industrial Quarry!");
                    RemoveQuarry(entity, true);
                    return;
                }
            }
        }

        private bool IsIQQuarry(IOEntity conveyor)
        {
            foreach (var q in storedData.LoadedIQuarrys)
            {
                if (q == null) { continue; }
                if (conveyor.net.ID.Value == q.SwitchNetID || conveyor.net.ID.Value == q.FuelAdapterNetID0 || conveyor.net.ID.Value == q.FuelAdapterNetID1 || conveyor.net.ID.Value == q.ResourceAdapterNetID0 || conveyor.net.ID.Value == q.ResourceAdapterNetID1)
                {
                    return true;
                }
            }
            return false;
        }

        private object OnSwitchToggle(IOEntity conveyor, BasePlayer player)
        {
            //Block players switching off quarry conveyors
            if (IsIQQuarry(conveyor) && _config.BlockSwitchingOffConveyors) { return true; }
            return null;
        }

        private void OnInputUpdate(IOEntity io, int inputAmount, int inputSlot)
        {
            if (io.skinID != 1234567890) { return; } //Fast exit from function

            //Check if quarry switch has been toggled electronically with 1 power or more.
            if (IsIQQuarry(io))
            {
                if (inputSlot == 1 && inputAmount >= 1)
                {
                    foreach (var q in storedData.LoadedIQuarrys)
                    {
                        if (q.SwitchNetID == io.net.ID.Value)
                        {
                            MiningQuarry quarry = (MiningQuarry)BaseNetworkable.serverEntities.Find(new NetworkableId(q.ParentQuarry));
                            if (quarry != null)
                            {
                                quarry.EngineSwitch(true);
                                return;
                            }
                        }
                    }
                }
                else if (inputSlot == 2 && inputAmount >= 1)
                {
                    foreach (var q in storedData.LoadedIQuarrys)
                    {
                        if (q.SwitchNetID == io.net.ID.Value)
                        {
                            MiningQuarry quarry = (MiningQuarry)BaseNetworkable.serverEntities.Find(new NetworkableId(q.ParentQuarry));
                            if (quarry != null)
                            {
                                quarry.EngineSwitch(false);
                                return;
                            }
                        }
                    }
                }
            }
        }

        private object OnWireClear(BasePlayer player, IOEntity dest, int ssocket, IOEntity source)
        {
            //Null checks
            if (source == null || dest == null) { return null; } //Run normal code

            //Check if its a switch used for Quarry Toggling.
            bool isswitch = (dest is ElectricSwitch || source is ElectricSwitch);
            //check if industrial part of interest to allow for earlier exit if not.
            if (dest is IndustrialEntity || source is IndustrialEntity || isswitch)
            {
                //Check if its a part of interest.
                if (IsIQQuarry(source) || IsIQQuarry(dest))
                {
                    //Block disconnecting at quarry side
                    if (IsIQQuarry(dest))
                    {
                        if (!isswitch)
                        {
                            CreateTip("You Can't Disconnect Pipe From Quarry Side.", player);
                            return true;
                        }

                        CreateTip("You Can't Disconnect Wire From Quarry Side.", player);
                        return true;
                    }
                }
            }

            //Run normal code
            return null;
        }

        private object OnWireConnect(BasePlayer player, IOEntity dest, int ssocket, IOEntity source, int dsocket)
        {
            //Null checks
            if (source == null || dest == null) { return null; } //Run normal code

            //Check if its a switch used for Quarry Toggling.
            bool isswitch = (dest is ElectricSwitch || source is ElectricSwitch);
            if (dest is IndustrialEntity || source is IndustrialEntity || isswitch)
            {
                //Check if its a part of intrest.
                if (IsIQQuarry(source) || IsIQQuarry(dest))
                {
                    //Block hooking into the Wire IO
                    if (player.GetHeldEntity().ShortPrefabName == "wiretool.entity" && !isswitch)
                    {
                        CreateTip("You Can't Connect Into Quarry Conveyor IO!", player);
                        return true;
                    }

                    //Block pipe looping
                    if (IsIQQuarry(source) && IsIQQuarry(dest))
                    {
                        CreateTip("You Can't Loop Quarry Pipes!", player);
                        return true;
                    }
                }
            }

            return null;
        }

        //Attach parts after 1 frame to allow it to spawn in
        private void OnEntitySpawned(MiningQuarry quarry) { NextFrame(() => { try { AttachIndustrialParts(quarry); } catch { } }); }

        private void OnServerInitialized(bool initial)
        {
            //Wait 30 secs if server reboot to allow things to spawn in.
            Subscribe("OnEntityKill");
            Subscribe("OnEntitySpawned");
            if (_config.CleanonStart && initial)
            {
                timer.Once(30, () =>
                {
                    DoCleanUp();
                    Puts("Cleaned IQ Ents.");
                    ServerMgr.Instance.StartCoroutine(Startup(10)); //restart plugin so it respawns and reconnects
                });
            }
            else
            {
                ServerMgr.Instance.StartCoroutine(Startup(initial ? 30 : 1));
            }
        }

        private object CanPickupEntity(BasePlayer player, BaseCombatEntity iOEntity)
        {
            //Block pickup of spawned in parts
            if (iOEntity.skinID == 1234567890 && iOEntity is IOEntity)
            {
                if (player != null && player.IsConnected)
                {
                    CreateTip("You Can't PickUp Quarry Parts!", player);
                }
                return false;
            }

            //run normally
            return null;
        }

        private object OnEntityTakeDamage(BaseCombatEntity iOEntity, HitInfo info)
        {
            //Block damage to spawned in parts
            if (iOEntity.skinID == 1234567890 && iOEntity is IOEntity)
            {
                BasePlayer player = info?.Initiator?.ToPlayer();
                if (player != null && player.IsConnected)
                {
                    CreateTip("You Can't Damage Quarry Parts!", player);
                }
                return true;
            }

            //run normally
            return null;
        }

        //Admin Kill
        private void OnEntityKill(MiningQuarry bn)
        {
            RemoveQuarry(bn);
        }

        //Admin Kill
        private object OnEntityKill(IOEntity bn)
        {
            if (bn.skinID == 1234567890)
            {
                (bn).Heal(200); //Set back to max health
                return true; //Block parts being destroyed
            }
            return null;
        }

        //Killed normally
        private void OnEntityDeath(BaseCombatEntity baseEntity)
        {
            if (baseEntity is MiningQuarry)
            {
                RemoveQuarry((MiningQuarry)baseEntity);
            }
        }

        //Remove Tool Killed
        private void OnNormalRemovedEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity is MiningQuarry)
            {
                RemoveQuarry((MiningQuarry)entity);
            }
        }

        //Remove Tool
        private object canRemove(BasePlayer player, BaseEntity entity)
        {
            if (entity.skinID == 1234567890) { return false; }
            return null;
        }


        void OnNewSave(string filename)
        {
            Puts("Clearing Save Data");
            storedData.LoadedIQuarrys.Clear();
        }

        private void Unload() { Interface.Oxide.DataFileSystem.WriteObject("industrialquarry", storedData); if (Harmony2_3 != null) { Harmony2_3.OnUnloaded(null); } }

        public IEnumerator Startup(int delay)
        {
            yield return new WaitForSeconds(delay);
            int to = 0;
            Vector3 zero = new Vector3(0, 0, 0);
            //Find all quarries and attach parts.
            foreach (BaseNetworkable bn in BaseNetworkable.serverEntities.entityList.Get().Values)
            {
                if (bn == null) { continue; }
                if (bn is MiningQuarry)
                {
                    MiningQuarry quarry = (MiningQuarry)bn;
                    if (quarry != null)
                    {
                        try { AttachIndustrialParts(quarry); }
                        catch { Puts("Bad quarry @ " + quarry); }
                    }
                }
                if (bn is IndustrialStorageAdaptor && !bn.IsDestroyed && Vector3.Distance(zero, bn.transform.position) < 10)
                {
                    try
                    {
                        (bn as BaseEntity).skinID = 0;
                        bn.Kill();
                    }
                    catch { }
                }
            }
            timer.Every(_config.MissingPartsDelay, () => { CheckParts(); }); //Check Quarrys every min to make sure server queue manger not destroyed pipes
        }

            private void RemoveQuarry(MiningQuarry baseEntity, bool PartsOnly = false)
        {
            if (baseEntity == null || storedData.LoadedIQuarrys.Count == 0) { return; }
            //Destroy parts and remove from datafile
            for (int i = storedData.LoadedIQuarrys.Count - 1; i >= 0; i--)
            {
                IQuarry2 quarry = storedData.LoadedIQuarrys[i];
                if (quarry.ParentPosition == baseEntity.transform.position)
                {
                    BaseEntity part0 = (BaseEntity)BaseNetworkable.serverEntities.Find(new NetworkableId(quarry.SwitchNetID));
                    BaseEntity part1 = (BaseEntity)BaseNetworkable.serverEntities.Find(new NetworkableId(quarry.ParentQuarry));
                    BaseEntity part2 = (BaseEntity)BaseNetworkable.serverEntities.Find(new NetworkableId(quarry.StoneNetID));
                    BaseEntity part3 = (BaseEntity)BaseNetworkable.serverEntities.Find(new NetworkableId(quarry.FuelAdapterNetID0));
                    BaseEntity part4 = (BaseEntity)BaseNetworkable.serverEntities.Find(new NetworkableId(quarry.FuelAdapterNetID1));
                    BaseEntity part5 = (BaseEntity)BaseNetworkable.serverEntities.Find(new NetworkableId(quarry.ResourceAdapterNetID0));
                    BaseEntity part6 = (BaseEntity)BaseNetworkable.serverEntities.Find(new NetworkableId(quarry.ResourceAdapterNetID1));
                    storedData.LoadedIQuarrys.Remove(quarry);
                    if (part0 != null)
                    {
                        part0.skinID = 0;
                        part0.Kill();
                    }
                    if (part1 != null && !PartsOnly)
                    {
                        timer.Once(1, () =>
                        {
                            if (part1 != null && !part1.IsDestroyed)
                            {
                                part1.Kill();
                            }
                        });
                    }
                    if (part2 != null)
                    {
                        part2.skinID = 0;
                        part2.Kill();
                    }
                    if (part3 != null)
                    {
                        part3.skinID = 0;
                        part3.Kill();
                    }
                    if (part4 != null)
                    {
                        part4.skinID = 0;
                        part4.Kill();
                    }
                    if (part5 != null)
                    {
                        part5.skinID = 0;
                        part5.Kill();
                    }
                    if (part6 != null)
                    {
                        part6.skinID = 0;
                        part6.Kill();
                    }
                }
            }
        }

        private MiningQuarry FindQuarry(Ray ray)
        {
            //Ray cast to find quarry
            RaycastHit hit;
            var raycast = UnityEngine.Physics.Raycast(ray, out hit, 10, -1);
            BaseEntity entity = raycast ? hit.GetEntity() : null;
            if (entity != null && entity is MiningQuarry)
            {
                return entity as MiningQuarry;
            }

            if (entity != null && entity?.GetParentEntity() is MiningQuarry)
            {
                return entity.GetParentEntity() as MiningQuarry;
            }

            return null;
        }

        //Destroy game objects stuff
        private void DestroyMeshCollider(BaseEntity ent)
        {
            foreach (var mesh in ent.GetComponentsInChildren<MeshCollider>())
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private void DestroyGroundComp(BaseEntity ent)
        {
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<GroundWatch>());
        }

        //Creates a tool tip on players screen
        private void CreateTip(string msg, BasePlayer player, int time = 10)
        {
            if (player == null)
            {
                return;
            }

            player.SendConsoleCommand("gametip.hidegametip");
            player.SendConsoleCommand("gametip.showgametip", msg);
            timer.Once(time, () => player?.SendConsoleCommand("gametip.hidegametip"));
        }

        private BaseEntity SpawnPart(string prefab, MiningQuarry quarry, Vector3 pos, Quaternion rot)
        {
            //Spawns in parts of the quarry
            BaseEntity baseent = GameManager.server.CreateEntity(prefab, pos, rot);
            baseent.OwnerID = quarry.OwnerID;
            baseent.Spawn();
            baseent.skinID = 1234567890;
            baseent.EnableSaving(true);
            // _SpawnedList.Add(baseent, quarry);
            //Destroy collider and it breaking from not being grounded
            DestroyMeshCollider(baseent);
            DestroyGroundComp(baseent);
            //Power it on
            baseent.SetFlag(BaseEntity.Flags.Reserved8, true);
            baseent.SetFlag(BaseEntity.Flags.On, true);
            if (baseent is BaseCombatEntity)
            {
                (baseent as BaseCombatEntity)?.InitializeHealth(999999999, 999999999); //Give massive health
            }
            if (baseent is DecayEntity)
            {
                (baseent as DecayEntity).decay = null;
            }
            return baseent;
        }

        public void DoCleanUp()
        {
            foreach (BaseNetworkable bn in BaseNetworkable.serverEntities.entityList.Get().Values) //Scan all server ents
            {
                if (bn.PrefabName == QuarryRock) //Found the rock things mounted to
                {
                    List<BaseEntity> list = new List<BaseEntity>();
                    Vis.Entities<BaseEntity>(bn.transform.position, 6f, list); //Scan everything within 6f
                    foreach (BaseEntity be in list) { if (be is IOEntity && !be.IsDestroyed) { (bn as BaseEntity).skinID = 0; be.Kill(); } } //Kill io ents
                    if (!bn.IsDestroyed) { bn.Kill(); } //kill rock
                    continue;
                }
                try
                {
                    if ((bn as BaseEntity).skinID == 1234567890)
                    {
                        (bn as BaseEntity).skinID = 0;
                        bn.Kill();
                    }
                }
                catch { }
            }
        }

        private void RunIO(IOEntity source, IOEntity dest)
        {
            //Try Clear Any Already Used Slots
            try { if (source.outputs[0].connectedTo.Get() != null) { WireTool.AttemptClearSlot(source.outputs[0].connectedTo.Get(), null, 0, true); } } catch { }
            try { if (dest.inputs[0].connectedTo.Get() != null) { WireTool.AttemptClearSlot(dest.inputs[0].connectedTo.Get().outputs[0].connectedTo.Get(), null, 0, true); } } catch { }
            //Connect
            source.ConnectTo(dest, 0, 0, new List<Vector3>() { source.transform.InverseTransformPoint(dest.transform.position), new Vector3(0, 0, 0) }, new List<float>(), new IOEntity.LineAnchor[0], 0);
            source.NotifyIndustrialNetworkChanged();
            source.NotifyIndustrialNetworkChanged();
        }

        public void AttachIndustrialParts(MiningQuarry quarry)
        {
            //Plugins disabled basically with both turned off
            if (!_config.AddOutput && !_config.AddFuelInput && !_config.AddIOSwitch) { return; }

            //Don't run if quarry doesn't exist
            if (quarry == null || quarry.transform == null) { return; }

            //Check if player has perm to use or if its blocked
            if ((quarry.OwnerID != 0 && !permission.UserHasPermission(quarry.OwnerID.ToString(), permallow))) { return; }
            if (quarry.OwnerID == 0 && !_config.AllowOnStaticQuarrys) { return; }

            //Setup
            StorageContainer Outputcontainer = null;
            StorageContainer Inputcontainer = null;
            bool pumpjack = false;
            Vector3 ConvayPoint = Vector3.zero;
            Quaternion ConvayerRot = Quaternion.identity;
            Quaternion FuelRot = Quaternion.identity;
            float BBDistance = 10f;
            //Check if quarry underground (likely a custom map one)
            bool Underground = (quarry.transform.position.y < TerrainMeta.HeightMap.GetHeight(quarry.transform.position) - 5);
            //Adjust position for monument quarries
            if (quarry.isStatic && !Underground) { BBDistance = _config.MbuildingBlock; }

            //Find Quarry Parts
            foreach (BaseEntity be in quarry.children)
            {
                try
                {
                    //Output has 18 slots
                    if (be is StorageContainer && (be as StorageContainer).inventorySlots == 18)
                    {
                        Outputcontainer = be as StorageContainer;
                        //Check if pumpjack
                        if (be.ShortPrefabName == Crude)
                        {
                            if (quarry.isStatic && TerrainMeta.TopologyMap.GetTopology(quarry.transform.position, TerrainTopology.MONUMENT)) { return; } //Building block too big around pump jacks at monuments
                            pumpjack = true;
                            FuelRot = Quaternion.Euler(0, 270, 0);
                            ConvayerRot = Outputcontainer.transform.rotation * Quaternion.Euler(0, 0, 180);
                            ConvayPoint = Outputcontainer.transform.position + (Outputcontainer.transform.right * BBDistance) - (Outputcontainer.transform.forward * 1.5f);
                        }
                        //Normal Quarry
                        else
                        {
                            FuelRot = Quaternion.Euler(0, 180, 0);
                            ConvayerRot = Outputcontainer.transform.rotation;
                            ConvayPoint = Outputcontainer.transform.position - (Outputcontainer.transform.right * BBDistance);
                        }
                    }

                    //Fuel tanks have 6
                    if (be is StorageContainer && (be as StorageContainer).inventorySlots == 6) { Inputcontainer = be as StorageContainer; }
                }
                catch
                {
                    Puts("Couldnt find any output container on quarry");
                    return;
                }
            }

            if (Outputcontainer == null) { return; }

            //Check ground height
            float TerrainHeight = 0;
            if (!Underground)
            {
                TerrainHeight = TerrainMeta.HeightMap.GetHeight(ConvayPoint) + 0.7f;
                ConvayPoint.y = TerrainHeight;
            }
            else
            {
                ConvayPoint.y += _config.underGroundQuarryHeightOffset;
                ConvayPoint += (Outputcontainer.transform.up * _config.underGroundQuarryHeightOffset);
                ConvayPoint += (Outputcontainer.transform.right * _config.underGroundQuarryForwardOffset);
            }

            List<BaseEntity> list = new List<BaseEntity>();
            Vis.Entities<BaseEntity>(ConvayPoint, BBDistance + 5, list);
            BaseEntity rock = null;
            //Add rock to mount things on
            foreach (var scan in list)
            {
                try
                {
                    if (scan.transform.position == ConvayPoint)
                    {
                        rock = scan;
                        break;
                    }
                }
                catch { Puts("Failed to find rock"); }
            }

            if (rock == null)
            {
                rock = GameManager.server.CreateEntity(QuarryRock, ConvayPoint, Outputcontainer.transform.rotation * Quaternion.Euler(270, 0, 0));
                rock.Spawn();
                rock.OwnerID = quarry.OwnerID;
                rock.skinID = 1234567890;
                rock.EnableSaving(true);
                rock.SendNetworkUpdateImmediate();
            }

            ElectricSwitch quarryswitch = null;
            Vector3 qs = rock.transform.position - (rock.transform.up * 0.95f);
            if (pumpjack) { qs += (rock.transform.forward * 0.25f); }
            else { qs += (rock.transform.forward * 0.25f); }

            IndustrialStorageAdaptor adapterfuel = null;
            Vector3 af = Inputcontainer.transform.position + (Inputcontainer.transform.up * 1.1f) - (Inputcontainer.transform.right * 0.55f);
            IndustrialConveyor conveyorfuel = null;
            Vector3 cf = rock.transform.position + (rock.transform.forward * 0.6f) - (rock.transform.up * 0.85f);
            IndustrialStorageAdaptor adapter = null;
            Vector3 a = Outputcontainer.transform.position + (Outputcontainer.transform.up * 0.95f);
            IndustrialConveyor conveyor = null;
            Vector3 c = rock.transform.position + (rock.transform.forward * 1.1f) - (rock.transform.up * 0.85f);
            foreach (var scan in list)
            {
                try
                {
                    if (scan.transform.position == qs && (ElectricSwitch)scan)
                    {
                        quarryswitch = scan as ElectricSwitch;
                        continue;
                    }

                    if (scan.transform.position == af && (IndustrialStorageAdaptor)scan)
                    {
                        adapterfuel = scan as IndustrialStorageAdaptor;
                        continue;
                    }

                    if (scan.transform.position == cf && (IndustrialConveyor)scan)
                    {
                        conveyorfuel = scan as IndustrialConveyor;
                        continue;
                    }

                    if (scan.transform.position == a && (IndustrialStorageAdaptor)scan)
                    {
                        adapter = scan as IndustrialStorageAdaptor;
                        continue;
                    }

                    if (scan.transform.position == c && (IndustrialConveyor)scan)
                    {
                        conveyor = scan as IndustrialConveyor;
                    }
                }
                catch { Puts("Failed to run scan"); }
            }

            //Spawn IO Switch
            if (_config.AddIOSwitch && quarryswitch == null)
            {
                try { quarryswitch = SpawnPart(SwitchPrefab, quarry, qs, ConvayerRot) as ElectricSwitch; } catch { Puts("Failed to spawn Switch"); }
            }

            //Add Fuel input stuff
            if (_config.AddFuelInput)
            {
                try
                {
                    if (adapterfuel == null) { adapterfuel = SpawnPart(StorageAdapter, quarry, af, Inputcontainer.transform.rotation * FuelRot) as IndustrialStorageAdaptor; }
                    if (conveyorfuel == null) { conveyorfuel = SpawnPart(Conveyor, quarry, cf, ConvayerRot * Quaternion.Euler(0, 0, 180)) as IndustrialConveyor; }
                    if (_config.MaxStackSizePerMoveAdustment != -1) { conveyorfuel.MaxStackSizePerMove = _config.MaxStackSizePerMoveAdustment; }
                    RunIO(conveyorfuel, adapterfuel);
                    adapterfuel.SetParent(Inputcontainer, true, true);
                }
                catch { Puts("Failed to spawn Fuel Adapter"); }
            }

            if (_config.AddOutput)
            {
                try
                {
                    //Add Quarry output stuff
                    if (adapter == null) { adapter = SpawnPart(StorageAdapter, quarry, a, Outputcontainer.transform.rotation) as IndustrialStorageAdaptor; }
                    if (conveyor == null) { conveyor = SpawnPart(Conveyor, quarry, c, ConvayerRot) as IndustrialConveyor; }
                    if (_config.MaxStackSizePerMoveAdustment != -1) { conveyor.MaxStackSizePerMove = _config.MaxStackSizePerMoveAdustment; }
                    RunIO(adapter, conveyor);
                    adapter.SetParent(Outputcontainer, true, true);
                }
                catch { Puts("Failed to spawn Output adapter"); }
            }

            //Check not already in data file
            if (storedData.LoadedIQuarrys.Count > 0)
            {
                foreach (var q in storedData.LoadedIQuarrys)
                {
                    try
                    {
                        if (q.ParentPosition == quarry.transform.position)
                        {
                            //Update Datafile
                            try { q.ParentPosition = quarry.transform.position; } catch { }
                            try { q.ParentQuarry = quarry.net.ID.Value; } catch { }
                            try { q.OwnerID = quarry.OwnerID; } catch { }
                            try { q.StoneNetID = rock.net.ID.Value; } catch { }
                            try { q.FuelAdapterNetID0 = adapterfuel.net.ID.Value; } catch { }
                            try { q.FuelAdapterNetID1 = conveyorfuel.net.ID.Value; } catch { }
                            try { q.ResourceAdapterNetID0 = adapter.net.ID.Value; } catch { }
                            try { q.ResourceAdapterNetID1 = conveyor.net.ID.Value; } catch { }
                            try { q.SwitchNetID = quarryswitch.net.ID.Value; } catch { }
                            try { q.Pipes0 = adapterfuel?.inputs[0]?.connectedTo?.Get()?.outputs[0]?.linePoints; } catch { }
                            try { q.Pipes1 = adapter?.outputs[0]?.linePoints; } catch { }
                            return;
                        }
                    }
                    catch { Puts("Failed To Update Datafile"); }
                }
            }
            //Create new datafile
            try
            {
                IQuarry2 quarry1 = new IQuarry2();
                try { quarry1.ParentPosition = quarry.transform.position; } catch { }
                try { quarry1.ParentQuarry = quarry.net.ID.Value; } catch { }
                try { quarry1.OwnerID = quarry.OwnerID; } catch { }
                try { quarry1.StoneNetID = rock.net.ID.Value; } catch { }
                try { quarry1.FuelAdapterNetID0 = adapterfuel.net.ID.Value; } catch { }
                try { quarry1.FuelAdapterNetID1 = conveyorfuel.net.ID.Value; } catch { }
                try { quarry1.ResourceAdapterNetID0 = adapter.net.ID.Value; } catch { }
                try { quarry1.ResourceAdapterNetID1 = conveyor.net.ID.Value; } catch { }
                try { quarry1.SwitchNetID = quarryswitch.net.ID.Value; } catch { }
                try { quarry1.Pipes0 = adapterfuel?.inputs[0]?.connectedTo?.Get()?.outputs[0]?.linePoints; } catch { }
                try { quarry1.Pipes1 = adapter?.outputs[0]?.linePoints; } catch { }
                storedData.LoadedIQuarrys.Add(quarry1);
            }
            catch { Puts("Failed To Create New Datafile"); }
            try { Interface.Oxide.DataFileSystem.WriteObject("industrialquarry", storedData); } catch { Puts("Failed to write to disk"); }
        }

        void CheckParts()
        {
            if (storedData.LoadedIQuarrys.Count == 0) { return; }
            for (int i = storedData.LoadedIQuarrys.Count - 1; i >= 0; i--)
            {
                IQuarry2 quarry = storedData.LoadedIQuarrys[i];
                if (quarry == null) { continue; }
                BaseEntity part0 = (BaseEntity)BaseNetworkable.serverEntities.Find(new NetworkableId(quarry.SwitchNetID));
                BaseEntity mainquarry = (BaseEntity)BaseNetworkable.serverEntities.Find(new NetworkableId(quarry.ParentQuarry));
                BaseEntity part2 = (BaseEntity)BaseNetworkable.serverEntities.Find(new NetworkableId(quarry.StoneNetID));
                BaseEntity part3 = (BaseEntity)BaseNetworkable.serverEntities.Find(new NetworkableId(quarry.FuelAdapterNetID0));
                BaseEntity part4 = (BaseEntity)BaseNetworkable.serverEntities.Find(new NetworkableId(quarry.FuelAdapterNetID1));
                BaseEntity part5 = (BaseEntity)BaseNetworkable.serverEntities.Find(new NetworkableId(quarry.ResourceAdapterNetID0));
                BaseEntity part6 = (BaseEntity)BaseNetworkable.serverEntities.Find(new NetworkableId(quarry.ResourceAdapterNetID1));
                if (mainquarry == null)
                {
                    if (_config.DebugMessages) Puts("Missing Quarry, Removing Parts");
                    storedData.LoadedIQuarrys.Remove(quarry);
                    if (part0 != null)
                    {
                        part0.skinID = 0;
                        part0.Kill();
                    }
                    if (part2 != null)
                    {
                        part2.skinID = 0;
                        part2.Kill();
                    }
                    if (part3 != null)
                    {
                        part3.skinID = 0;
                        part3.Kill();
                    }
                    if (part4 != null)
                    {
                        part4.skinID = 0;
                        part4.Kill();
                    }
                    if (part5 != null)
                    {
                        part5.skinID = 0;
                        part5.Kill();
                    }
                    if (part6 != null)
                    {
                        part6.skinID = 0;
                        part6.Kill();
                    }
                    continue;
                }
                if (quarry.OwnerID != 0)
                {
                    if (!permission.UserHasPermission(quarry.OwnerID.ToString(), permallow))
                    {
                        if (_config.DebugMessages) Puts("Owner Lost Permission");
                        RemoveQuarry(mainquarry as MiningQuarry, true);
                        continue;
                    }
                }
                if (part0 == null)
                {
                    if (_config.DebugMessages) Puts("Missing SwitchNetID");
                    storedData.LoadedIQuarrys.Remove(quarry);
                    AttachIndustrialParts(mainquarry as MiningQuarry);
                    continue;
                }
                if (part2 == null)
                {
                    if (_config.DebugMessages) Puts("Missing StoneNetID");
                    storedData.LoadedIQuarrys.Remove(quarry);
                    AttachIndustrialParts(mainquarry as MiningQuarry);
                    continue;
                }
                if (part3 == null)
                {
                    if (_config.DebugMessages) Puts("Missing FuelAdapterNetID0");
                    storedData.LoadedIQuarrys.Remove(quarry);
                    AttachIndustrialParts(mainquarry as MiningQuarry);
                    continue;
                }
                if (part4 == null)
                {
                    if (_config.DebugMessages) Puts("Missing FuelAdapterNetID1");
                    storedData.LoadedIQuarrys.Remove(quarry);
                    AttachIndustrialParts(mainquarry as MiningQuarry);
                    continue;
                }
                if (part5 == null)
                {
                    if (_config.DebugMessages) Puts("Missing ResourceAdapterNetID0");
                    storedData.LoadedIQuarrys.Remove(quarry);
                    AttachIndustrialParts(mainquarry as MiningQuarry);
                    continue;
                }
                if (part6 == null)
                {
                    if (_config.DebugMessages) Puts("Missing ResourceAdapterNetID1");
                    storedData.LoadedIQuarrys.Remove(quarry);
                    AttachIndustrialParts(mainquarry as MiningQuarry);
                    continue;
                }
            }
        }
    }
}