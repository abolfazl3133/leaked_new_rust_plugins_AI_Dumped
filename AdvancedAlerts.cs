using System;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AdvancedAlerts", "ProCelle", "1.3.3")]
    public class AdvancedAlerts : RustPlugin
    {
        [PluginReference]
        private Plugin ImageLibrary, RaidableBases, Convoy, Sputnik, ArmoredTrain;
        private string AlertImage;
        private ConfigData configData;
        private const string AlertsPanel = "MainAlertsPanel";
        private const string AlertsText = "MainAlertsText";
        private const string AlertsPanel2 = "MainAlertsPanel2";
        private const string AlertsText2 = "MainAlertsText2";
        string message;
        string mainName;
        string mainName2;
        bool usesPerms;
        string anchorMin;
        string anchorMax;
        float timeAlert;
        float alertFadeIn;
        bool enableCopter;
        string copterText;
        bool enableCopterDestroyed;
        string copterDestroyedText;
        bool copterDestroyedEnableDistance;
        double copterDestroyedMaxDistance;
        bool enableBradley;
        string bradleyText;
        bool enableBradleyDestroyed;
        string bradleyDestroyedText;
        bool bradleyDestroyedEnableDistance;
        double bradleyDestroyedMaxDistance;
        bool enableCH47;
        string cH47Text;
        bool enableCargoShip;
        string cargoShipText;
        bool enableAirdrop;
        string airdropText;
        bool enableCrate;
        string crateText;
        bool enableHalloween;
        string halloweenText;
        bool enableXmas;
        string xmasText;
        bool enableMLRS;
        string mLRSText;

        bool enableRaidBase;
        bool enableRaidBaseEnded;
        bool enableRaidBaseEnterPlayer;
        bool enableRaidBaseExitPlayer;
        string raidBaseText;
        string raidBaseTextEnterPlayer;
        string raidBaseTextEnded;
        string raidBaseTextExitPlayer;

        bool enableConvoy;
        string convoyText;
        bool enableConvoyEnded;
        string convoyTextEnded;


         #region Localization
        // Soon
        #endregion


        private void OnServerInitialized() 
        {
            if (ImageLibrary == null) { Puts("Image Library is required"); covalence.Server.Command("o.unload AdvancedAlerts"); }
            else
            { if (!ImageLibrary.Call<bool>("HasImage", AlertImage)) ImageLibrary.Call("AddImage", AlertImage, AlertImage, 0UL); }
        
            permission.RegisterPermission(USE, this);
            permission.RegisterPermission(COPTER, this);
            permission.RegisterPermission(BRADLEY, this);
            permission.RegisterPermission(BRADLEYDESTROYED, this);
            permission.RegisterPermission(AIRDROP, this);
            permission.RegisterPermission(CARGO, this);
            permission.RegisterPermission(CH47, this);
            permission.RegisterPermission(CRATE, this);
            permission.RegisterPermission(COPTERDESTROYED, this);
            permission.RegisterPermission(HALLOWEEN, this);
            permission.RegisterPermission(XMAS, this);
            permission.RegisterPermission(MLRS, this);
            permission.RegisterPermission(RAIDABLEBASE, this);
            permission.RegisterPermission(CONVOY, this);
            permission.RegisterPermission(SPUTNIK, this);
            permission.RegisterPermission(ARMTRAIN, this);
            permission.RegisterPermission(ARTIC, this);

        }
        void Init()
        {
           if (!LoadConfigVariables()) {
            Puts("Config file issue detected. Please delete file, or check syntax and fix.");
            return;
            }

            AlertImage = configData.PluginSettings.AlertImg;
            usesPerms = configData.PluginSettings.UsesPerms;
            anchorMin = configData.PluginSettings.AnchorMin;
            anchorMax = configData.PluginSettings.AnchorMax;
            timeAlert = configData.PluginSettings.TimeAlert;
            alertFadeIn = configData.PluginSettings.AlertFadeIn;
            enableCopter = configData.CopterSettings.EnableCopter;
            copterText = configData.CopterSettings.CopterText;
            enableCopterDestroyed = configData.CopterSettings.EnableCopterDestroyed;
            copterDestroyedText = configData.CopterSettings.CopterDestroyedText;
            copterDestroyedEnableDistance = configData.CopterSettings.CopterDestroyedEnableDistance;
            copterDestroyedMaxDistance = configData.CopterSettings.CopterDestroyedMaxDistance;
            enableBradley = configData.BradleySettings.EnableBradley;
            bradleyText = configData.BradleySettings.BradleyText;
            enableBradleyDestroyed = configData.BradleySettings.EnableBradleyDestroyed;
            bradleyDestroyedText = configData.BradleySettings.BradleyDestroyedText;
            bradleyDestroyedEnableDistance = configData.BradleySettings.BradleyDestroyedEnableDistance;
            bradleyDestroyedMaxDistance = configData.BradleySettings.BradleyDestroyedMaxDistance;
            enableCH47 = configData.CH47Settings.EnableCH47;
            cH47Text = configData.CH47Settings.CH47Text;
            enableCargoShip = configData.CargoShipSettings.EnableCargoShip;
            cargoShipText = configData.CargoShipSettings.CargoShipText;
            enableAirdrop = configData.AirdropSettings.EnableAirdrop;
            airdropText = configData.AirdropSettings.AirdropText;
            enableCrate = configData.CrateSettings.EnableCrate;
            crateText = configData.CrateSettings.CrateText;
            enableHalloween = configData.HalloweenSettings.EnableHalloween;
            halloweenText = configData.HalloweenSettings.HalloweenText;
            enableXmas = configData.XmasSettings.EnableXmas;
            xmasText = configData.XmasSettings.XmasText;
            enableMLRS = configData.MLRSSettings.EnableMLRS;
            mLRSText = configData.MLRSSettings.MLRSText;
            enableRaidBase = configData.RaideaBasesAlert.EnableRaidBase;
            raidBaseText = configData.RaideaBasesAlert.RaidBaseText;
            enableRaidBaseEnded = configData.RaideaBasesAlert.EnableRaidBaseEnded;
            raidBaseTextEnterPlayer = configData.RaideaBasesAlert.RaidBaseTextEnterPlayer;
            enableRaidBaseEnterPlayer = configData.RaideaBasesAlert.EnableRaidBaseEnterPlayer;
            raidBaseTextEnded = configData.RaideaBasesAlert.RaidBaseTextEnded;
            enableRaidBaseExitPlayer = configData.RaideaBasesAlert.EnableRaidBaseExitPlayer;
            raidBaseTextExitPlayer = configData.RaideaBasesAlert.RaidBaseTextExitPlayer;
            enableConvoy = configData.ConvoyAlerts.EnableConvoy;
            convoyText = configData.ConvoyAlerts.ConvoyText;
            enableConvoyEnded = configData.ConvoyAlerts.EnableConvoyEnded;
            convoyTextEnded = configData.ConvoyAlerts.ConvoyTextEnded;


        }
        private void Unload() 
        {
            foreach(BasePlayer current in BasePlayer.activePlayerList) 
            {
                CuiHelper.DestroyUi(current, AlertsPanel);
                CuiHelper.DestroyUi(current, AlertsText);
                CuiHelper.DestroyUi(current, AlertsPanel2);
                CuiHelper.DestroyUi(current, AlertsText2);
            }
        }
        private const string USE = "advancedalerts.use";
        private const string COPTER = "advancedalerts.copter";
        private const string COPTERDESTROYED = "advancedalerts.copterdestroyed";
        private const string BRADLEY = "advancedalerts.bradley";
        private const string BRADLEYDESTROYED = "advancedalerts.bradleydestroyed";
        private const string AIRDROP = "advancedalerts.airdrop";
        private const string CARGO = "advancedalerts.cargo";
        private const string CH47 = "advancedalerts.ch47";
        private const string CRATE = "advancedalerts.crate";
        private const string HALLOWEEN = "advancedalerts.halloween";
        private const string XMAS = "advancedalerts.xmas";
        private const string MLRS = "advancedalerts.mlrs";
        private const string RAIDABLEBASE = "advancedalerts.raidablebases";
        private const string CONVOY = "advancedalerts.convoy";
        private const string SPUTNIK = "advancedalerts.sputnik";
        private const string ARMTRAIN = "advancedalerts.armoredtrain";
        private const string ARTIC = "advancedalerts.articbase";

        
        public void SpawnAlert(string Perm, string AlertText, string MinAnchor, string MaxAnchor, float Timer)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                if (usesPerms == false || Perm == "hook" || permission.UserHasPermission(player.UserIDString, USE) || permission.UserHasPermission(player.UserIDString, Perm))
                {
                    
                    CuiHelper.DestroyUi(player, AlertsPanel);
                    CuiHelper.DestroyUi(player, AlertsText);
                    CuiHelper.DestroyUi(player, AlertsPanel2);
                    CuiHelper.DestroyUi(player, AlertsText2);
                    var elements = new CuiElementContainer();
                    var mainName = elements.Add(new CuiPanel { Image = { Color = "0.8 0.8 0.8 0.05" }, RectTransform = { AnchorMin = MinAnchor, AnchorMax = MaxAnchor }, CursorEnabled = false });
                    var image = new CuiElement { Parent = mainName, Name = AlertsPanel }; image.Components.Add(new CuiRawImageComponent { Png = ImageLibrary.Call<string>("GetImage", AlertImage), FadeIn = alertFadeIn }); image.Components.Add(new CuiRectTransformComponent { AnchorMax = "1 1", AnchorMin = "0 0" });
                    elements.Add(image);
                    elements.Add(new CuiLabel { Text = { Text = AlertText, FontSize = 20, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.05 0.1", AnchorMax = "0.95 0.8" } }, mainName, AlertsText);
                    CuiHelper.AddUi(player, elements);
                    timer.Once(Timer, () => { CuiHelper.DestroyUi(player, mainName); });
                }
        }

        [HookMethod("SpawnAlert")]
        public void SpawnAlert2(BasePlayer player, string Perm, string AlertText, string MinAnchor, string MaxAnchor, float Timer)
        {

            if (usesPerms == false || permission.UserHasPermission(player.UserIDString, USE) || permission.UserHasPermission(player.UserIDString, Perm))
            {
                CuiHelper.DestroyUi(player, AlertsPanel2);
                CuiHelper.DestroyUi(player, AlertsText2);
                CuiHelper.DestroyUi(player, AlertsPanel);
                CuiHelper.DestroyUi(player, AlertsText);
                var elements = new CuiElementContainer();
                var mainName2 = elements.Add(new CuiPanel { Image = { Color = "0.8 0.8 0.8 0.05" }, RectTransform = { AnchorMin = MinAnchor, AnchorMax = MaxAnchor }, CursorEnabled = false });
                var image = new CuiElement { Parent = mainName2, Name = AlertsPanel2 }; image.Components.Add(new CuiRawImageComponent { Png = ImageLibrary.Call<string>("GetImage", AlertImage), FadeIn = alertFadeIn}); image.Components.Add(new CuiRectTransformComponent { AnchorMax = "1 1", AnchorMin = "0 0" });
                elements.Add(image);
                elements.Add(new CuiLabel { Text = { Text = AlertText, FontSize = 20, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.05 0.1", AnchorMax = "0.95 0.8" } }, mainName2, AlertsText2);
                CuiHelper.AddUi(player, elements);
                timer.Once(Timer, () => { CuiHelper.DestroyUi(player, mainName2); });
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            
            if(entity is PatrolHelicopter && enableCopter){
                
                timer.Once(1f, () =>
                {
                    var isconvoy = Convoy?.Call("IsConvoyHeli", entity);
                    bool myBool = Convert.ToBoolean(isconvoy);
                    
                    if(myBool) return;
                });
                SpawnAlert(COPTER, copterText, anchorMin, anchorMax, timeAlert);
            }
            
            else if(entity is BradleyAPC && enableBradley){
                timer.Once(1f, () =>
                {
                    var isconvoy = Convoy?.Call("IsConvoyVehicle", entity);
                    var istrain = ArmoredTrain?.Call("IsTrainBradley", entity);
                    bool myBool = Convert.ToBoolean(isconvoy);
                    if(myBool) return;
                });
                SpawnAlert(BRADLEY, bradleyText, anchorMin, anchorMax, timeAlert);

            }

            else if(entity is CargoPlane && enableAirdrop) {
                SpawnAlert(AIRDROP, airdropText, anchorMin, anchorMax, timeAlert);

            }

            else if(entity is CargoShip && enableCargoShip){
                SpawnAlert(CARGO, cargoShipText, anchorMin, anchorMax, timeAlert);

            }
                   
            else if (entity is CH47Helicopter && enableCH47) {
                SpawnAlert(CH47, cH47Text, anchorMin, anchorMax, timeAlert);
                
            }

            else if(entity is HalloweenHunt && enableHalloween) {
                SpawnAlert(HALLOWEEN, halloweenText, anchorMin, anchorMax, timeAlert);
                
            }
            
            else if(entity is XMasRefill && enableXmas) {
                SpawnAlert(XMAS, xmasText, anchorMin, anchorMax, timeAlert);
                
            }
            

        }
        void OnMlrsFired(MLRS mlrs, BasePlayer player)
        {
            SpawnAlert(MLRS, mLRSText, anchorMin, anchorMax, timeAlert);
        }
        void OnCrateDropped(HackableLockedCrate crate)
        {
            if(enableCrate){
                timer.Once(1f, () =>
                    {
                        var isconvoy = Convoy?.Call("IsConvoyCrate", crate);
                        bool myBool = Convert.ToBoolean(isconvoy);
                        if(!myBool)
                        SpawnAlert(CRATE, crateText, anchorMin, anchorMax, timeAlert);

                    });
            }
        } 
        object OnEntityDestroy(BaseCombatEntity entity)
        {
            Vector3 pos = entity.transform.position;
            if (entity is BradleyAPC && enableBradleyDestroyed)
            {
                var isconvoy = Convoy?.Call("IsConvoyVehicle", entity);
                bool myBool = Convert.ToBoolean(isconvoy);
                    
                if(!myBool)
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    double distance2 = Math.Round(Vector3.Distance(pos, player.transform.position), 2);
                    if (!bradleyDestroyedEnableDistance || distance2 <= bradleyDestroyedMaxDistance)
                        SpawnAlert2(player, BRADLEYDESTROYED, bradleyDestroyedText, anchorMin, anchorMax, timeAlert);
                }
            }
            return null;
        }
        void OnEntityKill(BaseNetworkable entity)
        {

            Vector3 pos = entity.transform.position;
            if(entity is PatrolHelicopter && enableCopterDestroyed)
            {
                    var isconvoy = Convoy?.Call("IsConvoyHeli", entity);
                    bool myBool = Convert.ToBoolean(isconvoy);
                    if(myBool) return;

                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    double distance2 = Math.Round(Vector3.Distance(pos, player.transform.position), 2);
                        if(!copterDestroyedEnableDistance || distance2 <= copterDestroyedMaxDistance)
                            SpawnAlert2(player, COPTERDESTROYED, copterDestroyedText, anchorMin, anchorMax, timeAlert);
                }
            }
        }

        private void OnRaidableBaseStarted(Vector3 raidPos, int difficulty) {
            if(enableRaidBase)
            SpawnAlert(RAIDABLEBASE, raidBaseText, anchorMin, anchorMax, timeAlert);
        }
        private void OnRaidableBaseEnded(Vector3 raidPos, int difficulty){
            if(enableRaidBaseEnded)
            SpawnAlert(RAIDABLEBASE, raidBaseTextEnded, anchorMin, anchorMax, timeAlert);
        }

        private void OnPlayerEnteredRaidableBase(BasePlayer player, Vector3 raidPos, bool allowPVP) {
            if(enableRaidBaseEnterPlayer)
            SpawnAlert2(player, RAIDABLEBASE, raidBaseTextEnterPlayer, anchorMin, anchorMax, timeAlert);

        }
        private void OnPlayerExitedRaidableBase(BasePlayer player, Vector3 raidPos, bool allowPVP) {
            if(enableRaidBaseExitPlayer)
            SpawnAlert2(player, RAIDABLEBASE, raidBaseTextExitPlayer, anchorMin, anchorMax, timeAlert);
            
        }

        private void OnConvoyStart() {
            if(enableConvoy)
            SpawnAlert(CONVOY, convoyText, anchorMin, anchorMax, timeAlert);
        }

        private void OnConvoyStop() {
            if(enableConvoy)
            SpawnAlert(CONVOY, convoyTextEnded, anchorMin, anchorMax, timeAlert);
        }

        private void OnSputnikEventStart () {

            if(configData.SputnikAlerts.EnableSputnik)
            SpawnAlert(SPUTNIK, configData.SputnikAlerts.SputnikText, anchorMin, anchorMax, timeAlert);
        }

        private void OnSputnikEventStop () {
            if(configData.SputnikAlerts.EnableSputnikEnded)
            SpawnAlert(SPUTNIK, configData.SputnikAlerts.SputnikTextEnded, anchorMin, anchorMax, timeAlert);
        }

        private void OnArmoredTrainEventStart () {
            if(configData.ArmTrainAlerts.EnableArmTrain)
            SpawnAlert(ARMTRAIN, configData.ArmTrainAlerts.ArmTrainText, anchorMin, anchorMax, timeAlert);
        }
        private void OnArmoredTrainEventStop () {
            if(configData.ArmTrainAlerts.EnableArmTrainEnded)
            SpawnAlert(ARMTRAIN, configData.ArmTrainAlerts.ArmTrainTextEnded, anchorMin, anchorMax, timeAlert);
        }

        private void OnArcticBaseEventStart () {
            if(configData.ArticBaseAlerts.EnableArticBase)
            SpawnAlert(ARTIC, configData.ArticBaseAlerts.ArticBaseText, anchorMin, anchorMax, timeAlert);
        }
        private void OnArcticBaseEventEnd () {
            if(configData.ArticBaseAlerts.EnableArticBaseEnded)
            SpawnAlert(ARTIC, configData.ArticBaseAlerts.ArticBaseTextEnded, anchorMin, anchorMax, timeAlert);
        }


        #region PluginConfig

        class ConfigData {
            [JsonProperty(PropertyName = "General Settings")]
            public GeneralSettings PluginSettings = new GeneralSettings();

            [JsonProperty(PropertyName = "Copter Alert Settings")]
            public CopterSettings CopterSettings = new CopterSettings();

            [JsonProperty(PropertyName = "Bradley Alert Settings")]
            public BradleySettings BradleySettings = new BradleySettings();

            [JsonProperty(PropertyName = "CH47 Alert Settings")]
            public CH47Settings CH47Settings = new CH47Settings();

            [JsonProperty(PropertyName = "Cargo Ship Alert Settings")]
            public CargoShipSettings CargoShipSettings = new CargoShipSettings();

            [JsonProperty(PropertyName = "Airdrop Alert Settings")]
            public AirdropSettings AirdropSettings = new AirdropSettings();

            [JsonProperty(PropertyName = "Crate Alert Settings")]
            public CrateSettings CrateSettings = new CrateSettings();

            
            [JsonProperty(PropertyName = "Halloween Alert Settings")]
            public HalloweenSettings HalloweenSettings = new HalloweenSettings();

            [JsonProperty(PropertyName = "Xmas Alert Settings")]
            public XmasSettings XmasSettings = new XmasSettings();

            [JsonProperty(PropertyName = "MLRS Alert Settings")]
            public MLRSSettings MLRSSettings = new MLRSSettings();

            [JsonProperty(PropertyName = "Raideable Base Settings")]
            public RaideaBasesAlert RaideaBasesAlert = new RaideaBasesAlert(); 

            [JsonProperty(PropertyName = "Convoy Alerts Settings")]
            public ConvoyAlerts ConvoyAlerts = new ConvoyAlerts();

            [JsonProperty(PropertyName = "Sputnik Alerts Settings")] 
            public SputnikAlerts SputnikAlerts = new SputnikAlerts();

            [JsonProperty(PropertyName = "Armoured Train Alerts Settings")]  
            public ArmTrainAlerts ArmTrainAlerts = new ArmTrainAlerts();

            [JsonProperty(PropertyName = "Artic Base Alerts Settings")]  
            public ArticBaseAlerts ArticBaseAlerts = new ArticBaseAlerts();

        }

        class GeneralSettings {

            [JsonProperty(PropertyName = "Alert Image")]
            public string AlertImg = "https://i.imgur.com/UbWTfq5.png";

            [JsonProperty(PropertyName = "Only users with perms can see the alert")]
            public bool UsesPerms = false;

            [JsonProperty(PropertyName = "UI Anchor Min")]
            public string AnchorMin = "0.35 0.85";

            [JsonProperty(PropertyName = "UI Anchor Max")]
            public string AnchorMax = "0.65 0.95";

            [JsonProperty(PropertyName = "Time alert will be displayed (Seconds)")]
            public float TimeAlert = 5f;

            [JsonProperty(PropertyName = "Alert Fade In(Seconds)")]
            public float AlertFadeIn = 0.2f;
        }
        class CopterSettings {

            [JsonProperty(PropertyName = "Enable alert")]
            public bool EnableCopter = true;

            [JsonProperty(PropertyName = "Alert text when spawning")]
            public string CopterText = "Attack Helicopter has just spawned!";

            [JsonProperty(PropertyName = "Enable alert when destroyed")]
            public bool EnableCopterDestroyed = true;

            [JsonProperty(PropertyName = "Alert text when destroyed")]
            public string CopterDestroyedText = "Attack Helicopter has been taken down!";

            [JsonProperty(PropertyName = "Enable max distance alert when destroyed")]
            public bool CopterDestroyedEnableDistance = false;

            [JsonProperty(PropertyName = "Max distance alert when destroyed")]
            public double CopterDestroyedMaxDistance = 1000.0;
        }
        class BradleySettings {

            [JsonProperty(PropertyName = "Enable alert when spawning")]
            public bool EnableBradley = false;

            [JsonProperty(PropertyName = "Alert text when spawning")]
            public string BradleyText = "BradleyAPC has just spawned!";

            [JsonProperty(PropertyName = "Enable alert when destroyed")]
            public bool EnableBradleyDestroyed = true;

            [JsonProperty(PropertyName = "Alert text when destroyed")]
            public string BradleyDestroyedText = "BradleyAPC has been taken down!";

            [JsonProperty(PropertyName = "Enable max distance alert when destroyed")]
            public bool BradleyDestroyedEnableDistance = false;

            [JsonProperty(PropertyName = "Max distance alert when destroyed")]
            public double BradleyDestroyedMaxDistance = 1000.0;
        }
        class CH47Settings {

            [JsonProperty(PropertyName = "Enable alert when spawning")]
            public bool EnableCH47 = false;

            [JsonProperty(PropertyName = "Alert text when spawning")]
            public string CH47Text = "Chinook has just spawned!";

        }
        class CargoShipSettings {

            [JsonProperty(PropertyName = "Enable alert when spawning")]
            public bool EnableCargoShip = true;

            [JsonProperty(PropertyName = "Alert text when spawning")]
            public string CargoShipText = "Cargo Ship will spawn shortly!";

        }
        class AirdropSettings {

            [JsonProperty(PropertyName = "Enable alert when spawning")]
            public bool EnableAirdrop = true;

            [JsonProperty(PropertyName = "Alert text when spawning")]
            public string AirdropText = "Airdrop on its way!";

        }
        class CrateSettings {

            [JsonProperty(PropertyName = "Enable alert when spawning")]
            public bool EnableCrate = true;

            [JsonProperty(PropertyName = "Alert text when spawning")]
            public string CrateText = "A locked crate has appeared!";

        }
        class HalloweenSettings {

            [JsonProperty(PropertyName = "Enable alert when spawning")]
            public bool EnableHalloween = true;

            [JsonProperty(PropertyName = "Alert text when spawning")]
            public string HalloweenText = "A new Halloween event has started!";

        }
        class XmasSettings {

            [JsonProperty(PropertyName = "Enable alert when spawning")]
            public bool EnableXmas = true;

            [JsonProperty(PropertyName = "Alert text when spawning")]
            public string XmasText = "A new Xmas event has started!";

        }
        class MLRSSettings {

            [JsonProperty(PropertyName = "Enable alert when spawning")]
            public bool EnableMLRS = true;

            [JsonProperty(PropertyName = "Alert text when spawning")]
            public string MLRSText = "MLRS Strike incoming!";

        }
        class RaideaBasesAlert {

            [JsonProperty(PropertyName = "Enable alert on event started (Depends on Raidable Bases plugin)")]
            public bool EnableRaidBase = false;

            [JsonProperty(PropertyName = "Alert text on event started")]
            public string RaidBaseText = "A new Raideable Base has spawned!";

            [JsonProperty(PropertyName = "Enable alert on event ended (Depends on Raidable Bases plugin)")]
            public bool EnableRaidBaseEnded = false;

            [JsonProperty(PropertyName = "Alert text on event ended")]
            public string RaidBaseTextEnded = "A Raidable Base event has ended!";
            
            [JsonProperty(PropertyName = "Alert player when entering the Raidable Base")]
            public bool EnableRaidBaseEnterPlayer = false;

            [JsonProperty(PropertyName = "Alert text on player entry")]
            public string RaidBaseTextEnterPlayer = "You have entered a Raidable Base event!";
                        
            [JsonProperty(PropertyName = "Alert player upon leaving the Raidable Base")]
            public bool EnableRaidBaseExitPlayer = false;

            [JsonProperty(PropertyName = "Alert text on player exit")]
            public string RaidBaseTextExitPlayer = "You have left a Raidable Base event!";
        }
        class ConvoyAlerts {

            [JsonProperty(PropertyName = "Enable alert on event started (Depends on Convoy plugin)")]
            public bool EnableConvoy = false;

            [JsonProperty(PropertyName = "Alert text on event started")]
            public string ConvoyText = "A new Convoy is on its way!";

            [JsonProperty(PropertyName = "Enable alert on event ended (Depends on Convoy plugin)")]
            public bool EnableConvoyEnded = false;

            [JsonProperty(PropertyName = "Alert text on event ended")]
            public string ConvoyTextEnded = "The Convoy event has ended!";
        }
        class SputnikAlerts {

            [JsonProperty(PropertyName = "Enable alert on event started (Depends on Sputnik plugin)")]
            public bool EnableSputnik = false;

            [JsonProperty(PropertyName = "Alert text on event started")]
            public string SputnikText = "A meteorite is falling from the sky!";

            [JsonProperty(PropertyName = "Enable alert on event ended (Depends on Sputnik plugin)")]
            public bool EnableSputnikEnded = false;

            [JsonProperty(PropertyName = "Alert text on event ended")]
            public string SputnikTextEnded = "The Sputnik event has ended!";
        }
        class ArmTrainAlerts {

            [JsonProperty(PropertyName = "Enable alert on event started (Depends on Armored Train plugin)")]
            public bool EnableArmTrain = false;

            [JsonProperty(PropertyName = "Alert text on event started")]
            public string ArmTrainText = "An Armoured Train has appeared!";

            [JsonProperty(PropertyName = "Enable alert on event ended (Depends on Armored Train plugin)")]
            public bool EnableArmTrainEnded = false;

            [JsonProperty(PropertyName = "Alert text on event ended")]
            public string ArmTrainTextEnded = "The Armoured Train has disappeared!";
        }
        class ArticBaseAlerts {

            [JsonProperty(PropertyName = "Enable alert on event started (Depends on Artic Base plugin)")]
            public bool EnableArticBase = false;

            [JsonProperty(PropertyName = "Alert text on event started")]
            public string ArticBaseText = "The Artic Base Event has started!";

            [JsonProperty(PropertyName = "Enable alert on event ended (Depends on Artic Base plugin)")]
            public bool EnableArticBaseEnded = false;

            [JsonProperty(PropertyName = "Alert text on event ended")]
            public string ArticBaseTextEnded = "The Artic Base Event has ended!";
        }

		private bool LoadConfigVariables() {
            try
            {
            configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
            return false;
            }
            SaveConf();
            return true;
        }

        protected override void LoadDefaultConfig() {
            Puts("Fresh install detected, creating a new config file.");
            configData = new ConfigData();
            SaveConf();
        }

        void SaveConf() => Config.WriteObject(configData, true);
        #endregion
    }
}