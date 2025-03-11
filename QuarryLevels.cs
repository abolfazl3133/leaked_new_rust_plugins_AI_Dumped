using UnityEngine;
using System.Linq;
using Oxide.Core.Plugins;
using System.Globalization;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using Facepunch;

namespace Oxide.Plugins
{
    [Info ( "QuarryLevels", "WhitePlugins.Ru", "2.0.0" )]
    class QuarryLevels : RustPlugin
    {
        #region Declarations
        [PluginReference] Plugin Economics, ServerRewards;

        const string perm = "quarrylevels.use";
        System.Random random = new System.Random ();

        Dictionary<BasePlayer, MiningQuarry> ActiveGUI = new Dictionary<BasePlayer, MiningQuarry> ();
        #endregion

        public const int MiningQuarryItemId = 1052926200;
        public const int MiningPumpjackItemId = -1130709577;
        public const int StonesItemId = -2099697608;
        public const int SulfurOreItemId = -1157596551;
        public const int HQMetalOreItemId = -1982036270;
        public const int MetalOreItemId = -4031221;
        public const int CrudeOilItemId = -321733511;

        #region Hooks
        void Init ()
        {
            Unsubscribe ( "OnEntitySpawned" );
        }
        void OnServerInitialized ()
        {
            LoadConfig ();

            permission.RegisterPermission ( perm, this );

            foreach ( var quarry in BaseNetworkable.serverEntities.OfType<MiningQuarry> () )
            {
                if ( quarry.OwnerID != 0 )
                {
                    OnEntitySpawned ( quarry );
                }
            }

            Subscribe ( "OnEntitySpawned" );
        }

        void Unload ()
        {
            foreach ( var active in ActiveGUI )
            {
                CuiHelper.DestroyUi ( active.Key, "upgradebutton" );
                CuiHelper.DestroyUi ( active.Key, "upgradeconfirm" );
            }
        }

        object CanLootEntity ( BasePlayer player, ResourceExtractorFuelStorage quarry )
        {
            if ( !options.PlayerSettings.PreventUnauthorizedLooting )
            {
                return null;
            }

            var quarryOwner = quarry.GetParentEntity ().OwnerID;

            if ( quarryOwner != 0 && player.userID != quarryOwner && !player.IsBuildingAuthed () )
            {
                return true;
            }

            return null;
        }

        void OnLootEntity ( BasePlayer player, ResourceExtractorFuelStorage quarry )
        {
            if ( quarry.OwnerID != player.userID && !player.IsBuildingAuthed () || !quarry.HasParent () || !permission.UserHasPermission ( player.UserIDString, perm ) )
            {
                return;
            }

            if ( !ActiveGUI.ContainsKey ( player ) )
            {
                CreateGUI ( player );
                ActiveGUI.Add ( player, quarry.GetParentEntity ().GetComponent<MiningQuarry> () );
            }
        }

        void OnLootEntityEnd ( BasePlayer player, ResourceExtractorFuelStorage quarry )
        {
            if ( ActiveGUI.ContainsKey ( player ) )
            {
                DestroyCUI ( player );
            }
        }

        void OnEntityKill ( MiningQuarry quarry )
        {
            if ( quarry.OwnerID == 0 || quarry.ShortPrefabName.Equals ( "pumpjack-static" ) )
            {
                return;
            }

            var resources = Pool.GetList<ResourceDepositManager.ResourceDeposit.ResourceDepositEntry> ();
            resources.AddRange ( quarry._linkedDeposit._resources );

            foreach ( var res in resources )
            {
                if ( res.workNeeded == 4 || res.workNeeded == 50 )
                {
                    quarry._linkedDeposit._resources.Remove ( res );
                }
            }

            Pool.FreeList ( ref resources );
        }

        void OnQuarryToggled ( MiningQuarry quarry, BasePlayer player )
        {
            if ( quarry.OwnerID != 0 && options.PlayerSettings.PreventUnauthorizedToggling && player.userID != quarry.OwnerID && !player.IsBuildingAuthed () )
            {
                quarry.SetOn ( !quarry.IsOn () );
                return;
            }

            if ( !HasFuel ( quarry ) )
            {
                DirectMessage ( player, "This machine requires fuel to operate." );
                quarry.SetOn ( false );
            }
        }

        object OnQuarryConsumeFuel ( MiningQuarry quarry, Item item )
        {
            if ( quarry == null || quarry.OwnerID == 0 )
            {
                return item;
            }

            var fuel = quarry.fuelStoragePrefab.instance as StorageContainer;
            var expectedItem = fuel.inventory.FindItemByItemName ( quarry.ShortPrefabName.Contains ( "pumpjack" ) ? options.FuelSettings.PumpjackItem : options.FuelSettings.QuarryItem );

            if ( expectedItem != null )
            {
                return expectedItem;
            }

            return null;
        }

        void OnEntitySpawned ( MiningQuarry quarry )
        {
            quarry.fuelStoragePrefab.instance.OwnerID = quarry.OwnerID;
            quarry.hopperPrefab.instance.OwnerID = quarry.OwnerID;

            if ( quarry.skinID == 0 )
            {
                quarry.skinID = 1;
            }

            var level = ( int )quarry.skinID;
            var fuel = quarry.fuelStoragePrefab.instance as StorageContainer;
            var container = quarry.hopperPrefab.instance as StorageContainer;
            var pumpjack = quarry.ShortPrefabName.Contains ( "pumpjack" );

            fuel.inventory.canAcceptItem = ( item, amount ) => item.info.shortname == ( quarry.ShortPrefabName.Contains ( "pumpjack" ) ?
                options.FuelSettings.PumpjackItem : options.FuelSettings.QuarryItem );

            if ( level != 1 )
            {
                container.inventory.capacity = pumpjack ? container.inventory.capacity >= 46 ? 46 : ( options.QuarrySettings.PumpjackCapacityPerLevel * level ) : container.inventory.capacity >= 46 ? 46 : ( options.QuarrySettings.QuarryCapacityPerLevel * level );
                container.inventory.MarkDirty ();

                quarry.workToAdd = ( pumpjack ? 10 * level : 7.5f * level );
            }

            var resources = Pool.GetList<string> ();

            foreach ( var res in quarry._linkedDeposit._resources )
            {
                resources.Add ( res.type.shortname );
            }

            if ( pumpjack && !resources.Contains ( "crude.oil" ) )
            {
                quarry._linkedDeposit.Add ( ItemManager.FindItemDefinition ( "crude.oil" ), 1f, 1000, 10f, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, true );
            }

            if ( level >= options.DepositSettings.UnlockMetalAtLevel && !resources.Contains ( "metal.ore" ) )
            {
                quarry._linkedDeposit.Add ( ItemManager.FindItemDefinition ( "metal.ore" ), 1f, 1000, options.QuarryOptions.Metal_Production, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false );
            }

            if ( level >= options.DepositSettings.UnlockSulfurAtLevel && !resources.Contains ( "sulfur.ore" ) )
            {
                quarry._linkedDeposit.Add ( ItemManager.FindItemDefinition ( "sulfur.ore" ), 1f, 1000, options.QuarryOptions.Sulfur_Production, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false );
            }

            if ( level >= options.DepositSettings.UnlockHQMAtLevel && !resources.Contains ( "hq.metal.ore" ) )
            {
                quarry._linkedDeposit.Add ( ItemManager.FindItemDefinition ( "hq.metal.ore" ), 1f, 1000, options.QuarryOptions.HQM_Production, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false );
            }

            Pool.FreeList ( ref resources );
        }

        void OnEntitySpawned ( SurveyCrater crater )
        {
            if ( crater.ShortPrefabName == "survey_crater_oil" || random.Next ( 0, 100 ) > options.SurveySettings.OilCraterChance )
            {
                return;
            }

            GameManager.server.CreateEntity ( "assets/prefabs/tools/surveycharge/survey_crater_oil.prefab", crater.transform.position, crater.transform.rotation ).Spawn ();
            crater.Kill ();
        }
        #endregion

        #region Functions
        bool HasFuel ( MiningQuarry quarry )
        {
            var fuel = ( ( StorageContainer )quarry.fuelStoragePrefab.instance );
            var item = fuel.inventory.FindItemByItemName ( quarry.ShortPrefabName.Contains ( "pumpjack" ) ?
                options.FuelSettings.PumpjackItem : options.FuelSettings.QuarryItem );

            return item != null && item.amount >= ( int )quarry.skinID;
        }

        bool CanAfford ( BasePlayer player, bool pumpjack )
        {
            var item = player.inventory.containerMain.FindItemByItemName ( pumpjack ? "mining.pumpjack" : "mining.quarry" );

            if ( item == null )
            {
                item = player.inventory.containerBelt.FindItemByItemName ( pumpjack ? "mining.pumpjack" : "mining.quarry" );
            }

            if ( item == null || item.amount < 1 )
            {
                return false;
            }

            if ( item.skin != 0ul )
            {
                return false;
            }

            item.UseItem ( 1 );
            return true;
        }

        void DirectMessage ( BasePlayer player, string message )
        {
            player.SendConsoleCommand ( "chat.add", 0, "76561198070759528", message );
        }

        #region UI
        void CreateGUI ( BasePlayer player )
        {
            CuiHelper.DestroyUi ( player, "upgradebutton" );

            CuiElementContainer u = UI.Container ( "upgradebutton", UI.Color ( "000000", 0 ), options.Button.ButtonBounds );
            UI.Button ( ref u, "upgradebutton", UI.Color ( options.Button.ButtonColor, options.Button.ButtonOpacity ), $"<color={options.Button.ButtonFontColor}>Upgrade</color>", 10, new UI4 ( 0, 0, 1, 1f ), "ALSd01LASKDkaK2Qlasdka(1Kaklsdja2" );

            CuiHelper.AddUi ( player, u );
        }

        [ConsoleCommand ( "ALSd01LASKDkaK2Qlasdka(1Kaklsdja2" )]
        void CreateConfirmation ( ConsoleSystem.Arg arg )
        {
            CuiHelper.DestroyUi ( arg.Player (), "upgradebutton" );
            CuiHelper.DestroyUi ( arg.Player (), "upgradeconfirm" );

            var machine = ActiveGUI [ arg.Player () ];

            var pumpjack = machine.ShortPrefabName.Contains ( "pumpjack" );
            var capacity = machine.hopperPrefab.instance.GetComponent<StorageContainer> ().inventory.capacity;

            var level = ( int )machine.skinID;

            CuiElementContainer u = UI.Container ( "upgradeconfirm", UI.Color ( options.Panel.PanelColor, options.Panel.PanelOpacity ), options.Panel.PanelBounds );

            UI.Label ( ref u, "upgradeconfirm", $"<color={options.Panel.PanelFontColor}>{( pumpjack ? "Pumpjack Manager" : "Quarry Manager" )}</color>", 13, new UI4 ( 0, 0.8f, 1, 1f ), TextAnchor.MiddleCenter );
            UI.Panel ( ref u, "upgradeconfirm", UI.Color ( "000000", 0.6f ), new UI4 ( 0.43f, 0.76f, 0.57f, 0.84f ) );
            UI.Label_Lower ( ref u, "upgradeconfirm", $"<color={options.Panel.PanelFontColor}>Level {level}/{( pumpjack ? options.QuarrySettings.PumpjackMaxLevel : options.QuarrySettings.QuarryMaxLevel )}</color>", 8, new UI4 ( 0, 0.75f, 1, 0.85f ), TextAnchor.MiddleCenter );

            UI.Label ( ref u, "upgradeconfirm", $"<color={options.Panel.PanelFontColor}>Current Level</color>", 9, new UI4 ( 0.17f, 0.63f, 1, 0.73f ), TextAnchor.MiddleLeft );
            UI.Label_Lower ( ref u, "upgradeconfirm", $"<color={options.Panel.PanelFontColor}>Production:\nProcess Rate:\nCapacity:\nFuel Consumption:</color>", 8, new UI4 ( 0.1f, 0.34f, 1, 0.64f ), TextAnchor.MiddleLeft );
            UI.Label ( ref u, "upgradeconfirm", $"<color={options.Panel.PanelFontColor}>{machine.workToAdd}\n{machine.processRate}\n{capacity}\n{level}</color>", 8, new UI4 ( 0.1f, 0.34f, 0.4f, 0.64f ), TextAnchor.MiddleRight );

            UI.Panel ( ref u, "upgradeconfirm", UI.Color ( "#e8ddd4", 0.4f ), new UI4 ( 0.501f, 0.21f, 0.501f, 0.66f ) );
            UI.Image ( ref u, "upgradeconfirm", pumpjack ? MiningPumpjackItemId : MiningQuarryItemId, new UI4 ( 0.44f, 0.37f, 0.56f, 0.57f ) );

            UI.Button ( ref u, "upgradeconfirm", UI.Color ( "FF0000", 0.55f ), $"<color={options.Panel.PanelFontColor}>Cancel</color>", 9, new UI4 ( 0.502f, 0f, 0.997f, 0.13f ), "$19%(!*aslLKAK123(!@*AKJSK!(49128!(@#!@*#$%!" );
            UI.Button ( ref u, "upgradeconfirm", UI.Color ( $"{( level >= ( pumpjack ? options.QuarrySettings.PumpjackMaxLevel : options.QuarrySettings.QuarryMaxLevel ) ? "FF0000" : "008000" )}", 0.55f ), $"<color={options.Panel.PanelFontColor}>{( level >= ( pumpjack ? options.QuarrySettings.PumpjackMaxLevel : options.QuarrySettings.QuarryMaxLevel ) ? "Max Level" : "Upgrade" )}</color>", 9, new UI4 ( 0f, 0f, 0.499f, 0.13f ), $"{( level >= ( pumpjack ? options.QuarrySettings.PumpjackMaxLevel : options.QuarrySettings.QuarryMaxLevel ) ? string.Empty : "gLx$_+!)@laKS4391LAKS1291@$(!RKQSMDIO!@@" )}" );

            if ( level < ( pumpjack ? options.QuarrySettings.PumpjackMaxLevel : options.QuarrySettings.QuarryMaxLevel ) )
            {
                UI.Label ( ref u, "upgradeconfirm", $"<color=#e8ddd4>Next Level</color>", 9, new UI4 ( 0f, 0.64f, 0.81f, 0.74f ), TextAnchor.MiddleRight );
                UI.Label_Lower ( ref u, "upgradeconfirm", $"<color=#e8ddd4>Production:\nProcess Rate:\nCapacity:\nFuel Consumption:</color>", 8, new UI4 ( 0.6f, 0.34f, 1, 0.64f ), TextAnchor.MiddleLeft );
                UI.Label ( ref u, "upgradeconfirm", $"<color=#e8ddd4>{machine.workToAdd + ( pumpjack ? 10f : 7.5f )}\n{machine.processRate}\n{capacity + 2}\n{level + 1}</color>", 8, new UI4 ( 0.6f, 0.34f, 0.9f, 0.64f ), TextAnchor.MiddleRight );
            }

            var resources = Pool.GetList<string> ();

            var offsetL = 0.1f;
            var offsetR = 0.16f;

            var offsetRL = 0.6f;
            var offsetRR = 0.66f;

            if ( pumpjack )
            {
                UI.Image ( ref u, "upgradeconfirm", CrudeOilItemId, new UI4 ( offsetL, 0.21f, offsetR, 0.35f ) );
                UI.Panel ( ref u, "upgradeconfirm", UI.Color ( "000000", 0.6f ), new UI4 ( offsetL, 0.16f, offsetR, 0.22f ) );
                UI.Label ( ref u, "upgradeconfirm", $"<color=#e8ddd4>{level}</color>", 6, new UI4 ( offsetL, 0.14f, offsetR, 0.24f ), TextAnchor.MiddleCenter );

                if ( level != options.QuarrySettings.PumpjackMaxLevel )
                {
                    UI.Image ( ref u, "upgradeconfirm", CrudeOilItemId, new UI4 ( offsetRL, 0.21f, offsetRR, 0.35f ) );
                    UI.Panel ( ref u, "upgradeconfirm", UI.Color ( "000000", 0.6f ), new UI4 ( offsetRL, 0.16f, offsetRR, 0.22f ) );
                    UI.Label ( ref u, "upgradeconfirm", $"<color=#e8ddd4>{1 + level}</color>", 6, new UI4 ( offsetRL, 0.14f, offsetRR, 0.24f ), TextAnchor.MiddleCenter );
                }
            }
            else
            {
                foreach ( var res in machine._linkedDeposit._resources )
                {
                    UI.Image ( ref u, "upgradeconfirm", res.type.itemid, new UI4 ( offsetL, 0.21f, offsetR, 0.35f ) );
                    UI.Panel ( ref u, "upgradeconfirm", UI.Color ( "000000", 0.6f ), new UI4 ( offsetL, 0.16f, offsetR, 0.22f ) );
                    UI.Label ( ref u, "upgradeconfirm", $"<color=#e8ddd4>{( 6 * ( 7.5f / res.workNeeded ) * level ).ToString ( "0.0" )}</color>", 6, new UI4 ( offsetL, 0.14f, offsetR, 0.24f ), TextAnchor.MiddleCenter );

                    offsetL += 0.07f;
                    offsetR += 0.07f;

                    if ( level != options.QuarrySettings.QuarryMaxLevel )
                    {
                        UI.Image ( ref u, "upgradeconfirm", res.type.itemid, new UI4 ( offsetRL, 0.21f, offsetRR, 0.35f ) );
                        UI.Panel ( ref u, "upgradeconfirm", UI.Color ( "000000", 0.6f ), new UI4 ( offsetRL, 0.16f, offsetRR, 0.22f ) );
                        UI.Label ( ref u, "upgradeconfirm", $"<color=#e8ddd4>{( 6 * ( 7.5f / res.workNeeded ) * ( level + 1 ) ).ToString ( "0.0" )}</color>", 6, new UI4 ( offsetRL, 0.14f, offsetRR, 0.24f ), TextAnchor.MiddleCenter );

                        offsetRL += 0.07f;
                        offsetRR += 0.07f;
                    }

                    resources.Add ( res.type.shortname );
                }

                if ( level >= 2 && !resources.Contains ( "metal.ore" ) )
                {
                    UI.Image ( ref u, "upgradeconfirm", MetalOreItemId, new UI4 ( offsetRL, 0.21f, offsetRR, 0.35f ) );
                    UI.Panel ( ref u, "upgradeconfirm", UI.Color ( "000000", 0.6f ), new UI4 ( offsetRL, 0.16f, offsetRR, 0.22f ) );
                    UI.Label ( ref u, "upgradeconfirm", $"<color=#e8ddd4>??</color>", 6, new UI4 ( offsetRL, 0.14f, offsetRR, 0.24f ), TextAnchor.MiddleCenter );

                    offsetRL += 0.07f;
                    offsetRR += 0.07f;
                }

                if ( level >= 3 && !resources.Contains ( "sulfur.ore" ) )
                {
                    UI.Image ( ref u, "upgradeconfirm", SulfurOreItemId, new UI4 ( offsetRL, 0.21f, offsetRR, 0.35f ) );
                    UI.Panel ( ref u, "upgradeconfirm", UI.Color ( "000000", 0.6f ), new UI4 ( offsetRL, 0.16f, offsetRR, 0.22f ) );
                    UI.Label ( ref u, "upgradeconfirm", $"<color=#e8ddd4>??</color>", 6, new UI4 ( offsetRL, 0.14f, offsetRR, 0.24f ), TextAnchor.MiddleCenter );

                    offsetRL += 0.07f;
                    offsetRR += 0.07f;
                }

                if ( level >= 4 && !resources.Contains ( "hq.metal.ore" ) )
                {
                    UI.Image ( ref u, "upgradeconfirm", HQMetalOreItemId, new UI4 ( offsetRL, 0.21f, offsetRR, 0.35f ) );
                    UI.Panel ( ref u, "upgradeconfirm", UI.Color ( "000000", 0.6f ), new UI4 ( offsetRL, 0.16f, offsetRR, 0.22f ) );
                    UI.Label ( ref u, "upgradeconfirm", $"<color=#e8ddd4>??</color>", 6, new UI4 ( offsetRL, 0.14f, offsetRR, 0.24f ), TextAnchor.MiddleCenter );
                }
            }

            Pool.FreeList ( ref resources );
            CuiHelper.AddUi ( arg.Player (), u );
        }

        void DestroyCUI ( BasePlayer player )
        {
            ActiveGUI.Remove ( player );

            CuiHelper.DestroyUi ( player, "upgradebutton" );
            CuiHelper.DestroyUi ( player, "upgradeconfirm" );
        }
        #endregion

        #region Commands
        [ConsoleCommand ( "ql" )]
        void ConfigCommand ( ConsoleSystem.Arg arg )
        {
            if ( arg.Player () != null || arg.Args == null )
            {
                return;
            }

            // Went ahead and used a switch to easily add more commands in the future.
            switch ( arg.Args [ 0 ] )
            {
                case "reload":
                    LoadConfig ();
                    arg.ReplyWith ( "Config file has been reloaded." );
                    break;

                default:
                    arg.ReplyWith ( "Not a valid command." );
                    break;
            }
        }
        #endregion

        #region CallBacks
        [ConsoleCommand ( "$19%(!*aslLKAK123(!@*AKJSK!(49128!(@#!@*#$%!" )]
        void CloseConfirm ( ConsoleSystem.Arg arg )
        {
            CuiHelper.DestroyUi ( arg.Player (), "upgradeconfirm" );
            CreateGUI ( arg.Player () );
        }

        [ConsoleCommand ( "gLx$_+!)@laKS4391LAKS1291@$(!RKQSMDIO!@@" )]
        void Upgrade ( ConsoleSystem.Arg arg )
        {
            var player = arg.Player ();
            var machine = ActiveGUI [ player ];

            var resources = Pool.GetList<string> ();
            var pumpjack = machine.ShortPrefabName.Contains ( "pumpjack" );

            if ( Economics && options.QuarrySettings.EnableEconomics )
            {
                var call = Economics.Call ( "Withdraw", player.userID, options.QuarrySettings.EconomicsCost );

                if ( call == null || ( call is bool && !( bool )call ) )
                {
                    DirectMessage ( player, $"You cannot afford this upgrade. Requires <color=#add8e6ff>{options.QuarrySettings.EconomicsCost} {options.QuarrySettings.EconomicsCurrency}</color>!" );
                    return;
                }
            }
            else if ( ServerRewards && options.QuarrySettings.EnableServerRewards )
            {
                var call = ServerRewards.Call ( "TakePoints", player.userID, ( int )options.QuarrySettings.ServerRewardsCost );

                if ( call == null || ( call is bool && !( bool )call ) )
                {
                    DirectMessage ( player, $"You cannot afford this upgrade. Requires <color=#add8e6ff>{options.QuarrySettings.ServerRewardsCost} {options.QuarrySettings.ServerRewardsCurrency}</color>!" );
                    return;
                }
            }
            else
            {
                if ( !CanAfford ( player, pumpjack ) )
                {
                    DirectMessage ( player, $"You cannot afford this upgrade. Requires 1 <color=#add8e6ff>[{( pumpjack ? "Pumpjack" : "Mining Quarry" )}]</color>!" );
                    return;
                }
            }

            var output = machine.hopperPrefab.instance as StorageContainer;
            var level = ( int )++machine.skinID;

            output.inventory.capacity += 5;
            machine.workToAdd += pumpjack ? 10f : 7.5f;

            foreach ( var res in machine._linkedDeposit._resources )
            {
                resources.Add ( res.type.shortname );
            }

            if ( level >= 3 && !resources.Contains ( "metal.ore" ) )
            {
                machine._linkedDeposit.Add ( ItemManager.FindItemDefinition ( "metal.ore" ), 1f, 1000, options.QuarryOptions.Metal_Production, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false );
            }

            if ( level >= 4 && !resources.Contains ( "sulfur.ore" ) )
            {
                machine._linkedDeposit.Add ( ItemManager.FindItemDefinition ( "sulfur.ore" ), 1f, 1000, options.QuarryOptions.Sulfur_Production, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false );
            }

            if ( level >= 5 && !resources.Contains ( "hq.metal.ore" ) )
            {
                machine._linkedDeposit.Add ( ItemManager.FindItemDefinition ( "hq.metal.ore" ), 1f, 1000, options.QuarryOptions.HQM_Production, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false );
            }

            Effect.server.Run ( "assets/bundled/prefabs/fx/build/promote_metal.prefab", output.transform.position );
            Pool.FreeList ( ref resources );

            if ( output.IsOpen () )
            {
                player.EndLooting ();
                output.PlayerOpenLoot ( player, string.Empty, true );
                return;
            }

            CreateGUI ( player );
            CuiHelper.DestroyUi ( player, "upgradeconfirm" );
        }
        #endregion

        #region Config
        ConfigFile options;

        class ConfigFile
        {
            public FuelConfig FuelSettings = new FuelConfig ();
            public SurveyConfig SurveySettings = new SurveyConfig ();
            public DepositConfig DepositSettings = new DepositConfig ();
            public PlayerConfig PlayerSettings = new PlayerConfig ();
            public QuarryLevelOptions QuarrySettings = new QuarryLevelOptions ();
            public QuarryProduction QuarryOptions = new QuarryProduction ();
            public ButtonConfig Button = new ButtonConfig ();
            public PanelConfig Panel = new PanelConfig ();
        }

        class FuelConfig
        {
            public string PumpjackItem = "lowgradefuel";
            public string QuarryItem = "diesel_barrel";
        }

        class DepositConfig
        {
            public int UnlockMetalAtLevel = 3;
            public int UnlockSulfurAtLevel = 4;
            public int UnlockHQMAtLevel = 5;
        }

        class PlayerConfig
        {
            public bool PreventUnauthorizedToggling = false;
            public bool PreventUnauthorizedLooting = false;
        }

        class SurveyConfig
        {
            public bool EnableOilCraters = false;
            public int OilCraterChance = 10;
        }

        class QuarryLevelOptions
        {
            public int QuarryMaxLevel = 5;
            public int PumpjackMaxLevel = 5;

            public int QuarryCapacityPerLevel = 5;
            public int PumpjackCapacityPerLevel = 5;

            public bool EnableEconomics = false;
            public double EconomicsCost = 5000;
            public string EconomicsCurrency = "credits";

            public bool EnableServerRewards = false;
            public double ServerRewardsCost = 5000;
            public string ServerRewardsCurrency = "rp";
        }

        class QuarryProduction
        {
            public float Metal_Production = 4f;
            public float Sulfur_Production = 4f;
            public float HQM_Production = 50f;
        }

        class ButtonConfig
        {
            public UI4 ButtonBounds = new UI4 ( 0.648f, 0.115f, 0.72f, 0.143f );
            public string ButtonColor = "FFFFF3";
            public float ButtonOpacity = 0.160f;
            public string ButtonFontColor = "#f7ebe1";
        }

        class PanelConfig
        {
            public UI4 PanelBounds = new UI4 ( 0.39f, 0.55f, 0.61f, 0.75f );
            public string PanelColor = "FFFFF3";
            public float PanelOpacity = 0.160f;
            public string PanelFontColor = "#e8ddd4";
        }

        void LoadDefaultConfig ()
        {
            SaveConfig ( new ConfigFile () );
        }

        void LoadConfig ()
        {
            options = Config.ReadObject<ConfigFile> ();
            SaveConfig ( options );
        }

        void SaveConfig ( ConfigFile config )
        {
            Config.WriteObject ( config, true );
        }
        #endregion

        #region UI - Frame Work
        public static class UI
        {
            static public CuiElementContainer Container ( string panel, string color, UI4 dimensions, bool useCursor = false, string parent = "Overlay" )
            {
                CuiElementContainer container = new CuiElementContainer ()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax()},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panel
                    }
                };
                return container;
            }

            static public void Panel ( ref CuiElementContainer container, string panel, string color, UI4 dimensions, bool cursor = false )
            {
                container.Add ( new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = dimensions.GetMin (), AnchorMax = dimensions.GetMax () },
                    CursorEnabled = cursor
                },
                panel );
            }

            static public void Label ( ref CuiElementContainer container, string panel, string text, int size, UI4 dimensions, TextAnchor align = TextAnchor.MiddleCenter )
            {
                container.Add ( new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = dimensions.GetMin (), AnchorMax = dimensions.GetMax () }
                },
                panel );

            }

            static public void Label_Lower ( ref CuiElementContainer container, string panel, string text, int size, UI4 dimensions, TextAnchor align = TextAnchor.MiddleCenter )
            {
                container.Add ( new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text, Font = "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = dimensions.GetMin (), AnchorMax = dimensions.GetMax () }
                },
                panel );

            }

            static public void Button ( ref CuiElementContainer container, string panel, string color, string text, int size, UI4 dimensions, string command, TextAnchor align = TextAnchor.MiddleCenter )
            {
                container.Add ( new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 0f },
                    RectTransform = { AnchorMin = dimensions.GetMin (), AnchorMax = dimensions.GetMax () },
                    Text = { Text = text, FontSize = size, Align = align, Font = "robotocondensed-regular.ttf" }
                },
                panel );
            }

            static public void Image ( ref CuiElementContainer container, string panel, string png, UI4 dimensions )
            {
                container.Add ( new CuiElement
                {
                    Name = CuiHelper.GetGuid (),
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {Png = png },
                        new CuiRectTransformComponent {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                    }
                } );
            }

            static public void Image ( ref CuiElementContainer container, string panel, int itemid, UI4 dimensions )
            {
                container.Add ( new CuiElement
                {
                    Name = CuiHelper.GetGuid (),
                    Parent = panel,
                    Components =
                    {
                        new CuiImageComponent { ItemId = itemid },
                        new CuiRectTransformComponent {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                    }
                } );
            }

            public static string Color ( string hexColor, float alpha )
            {
                if ( hexColor.StartsWith ( "#" ) )
                    hexColor = hexColor.Substring ( 1 );
                int red = int.Parse ( hexColor.Substring ( 0, 2 ), NumberStyles.AllowHexSpecifier );
                int green = int.Parse ( hexColor.Substring ( 2, 2 ), NumberStyles.AllowHexSpecifier );
                int blue = int.Parse ( hexColor.Substring ( 4, 2 ), NumberStyles.AllowHexSpecifier );
                return $"{( double )red / 255} {( double )green / 255} {( double )blue / 255} {alpha}";
            }
        }
        public class UI4
        {
            public float xMin, yMin, xMax, yMax;
            public UI4 ( float xMin, float yMin, float xMax, float yMax )
            {
                this.xMin = xMin;
                this.yMin = yMin;
                this.xMax = xMax;
                this.yMax = yMax;
            }
            public string GetMin () => $"{xMin} {yMin}";
            public string GetMax () => $"{xMax} {yMax}";
        }
        #endregion

        #endregion
    }
}
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */