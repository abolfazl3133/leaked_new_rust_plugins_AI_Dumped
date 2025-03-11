using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections;
using System.Text;
using Facepunch.Extend;

/*
 * Has WearContainer Anti Stacking Duplication features/bug fixes
 * Fixes Custom skin splitting issues + Custom Item Names. Making oranges skinsfix plugin not required/needed. 
 * Has vending machine no ammo patch toggle (so it won't affect default map vending machine not giving out stock ammo.
 * Doesn't have ammo duplication with repair bench by skin manipulation issue.
 * Doesn't have condition reset issues when re-stacking used weapons.
 * Has not being able to stack a used gun onto a new gun, only.
 * Doesn't have the weapon attachments issues
 * Doesn't have losing stack of candy cane lick issues
 * 
 *
 * Fixed Visual bug on item splits ( where the players inventory UI wasn't updating properly )
 * Slight performance tweak
 * Added Updater methods.
 *
 * Fixed new NRE issues 6/8/2021
 *
 * Update 1.0.5 6/12/2021
 * Changed check config value to >= because fuck it
 *
 * updated 1.0.6 7/15/2021
 * Added feature to stop player abusing higher stack sizes when moving items from other storage containers to player inventory set from other plugins
 * Fixed Clone stack issues
 * Updated OnItemAddedToContainer code logic to fix StackItemStorage Bug Credits to Clearshot.
 *
 * Update 1.0.8
 * Fixed High hook time warnings significantly reduced
 * Fixed Condition loss comparison between float values
 * Added Ignore Admin check for F1 Spawns
 * Adjusted/fixed item moving issues from other plugins
 *
 * Update 1.0.9
 * Patched Skins auto merging into 1 stack bug
 *
 * Update 1.1.0
 * Added Liquid stacking support
 * Fixed On ItemAdded issue with stacks when using StackItemStorage
 *
 * Update 1.1.1
 * Added support for stacking Miner Hats with fuel + Candle Hats
 *
 * Update 1.1.2
 * Fixed Stacking issues with float values not matching due to unity float comparison bug
 *
 * Update 1.1.3
 * Fixed Vendor bug..
 *
 * Update 1.1.4
 * Added OnCardSwipe to fix stack loss when it hits broken stage.
 *
 * Update 1.1.5
 * Fixed High hook time hangs sometimes resulted in server crashes..
 *
 * Update 1.1.7
 * Fixes all vendor problems when booting/rebooting servers
 * Added Chat command to manually reset the vendors that you talk to only /resetvendors
 *
 * Update 1.1.8
 * Pulled due to false reports.
 * Reverted back to original patch of 1.1.7
 * 
 * Update 1.1.9
 * Fixes custom items that have different custom names applied with the same skinids from stacking.
 * Fixes resetting stacks to default if ( true ) in config.
 *
 * Update 1.2.0
 * Added Global Category Group Stack Setting options
 *
 * Update 1.3.0
 * Added Editor UI
 * Swapped back to Rust Plugin
 * Added Image Library Support
 * Added New Config Options
 * Added Search Bar + fade out.
 * Added additional checks/options made some performance improvements
 *
 * Update 1.3.2
 * Blocks player movements while using editor
 * Updated console output responses
 * Updated UI Systems and fixed a bug relating to first time opening
 * ( it was double opening )
 *
 * Update 1.3.21
 * Updated Input checks
 * Fixed spectating players while using the UI Editor..
 *
 * Update 1.3.22
 * Expanded the UI Editor Search parameters a bit
 *
 * Update 1.3.3
 * Fixed Missing Defaults
 * Fixed UI Not showing correctly between Multipliers and Modified items
 * Added
 * aiming.module.mlrs, MLRS Aiming Module
 * mlrs, MLRS
 * ammo.rocket.mlrs, MLRS Rocket
 * lumberjack hoodie, Lumberjack Hoodie
 * frankensteintable, Frankenstein Table
 * carvable.pumpkin, Carvable Pumpkin
 * frankensteins.monster.01.head, Light Frankenstein Head
 * frankensteins.monster.01.legs, Light Frankenstein Legs
 * frankensteins.monster.01.torso, Light Frankenstein Torso
 * frankensteins.monster.02.head, Medium Frankenstein Head
 * frankensteins.monster.02.legs, Medium Frankenstein Legs
 * frankensteins.monster.02.torso, Medium Frankenstein Torso
 * frankensteins.monster.03.head, Heavy Frankenstein Head
 * frankensteins.monster.03.legs, Heavy Frankenstein Legs
 * frankensteins.monster.03.torso, Heavy Frankenstein Torso
 * sunglasses02black, Sunglasses
 * sunglasses02camo, Sunglasses
 * sunglasses02red, Sunglasses
 * sunglasses03black, Sunglasses
 * sunglasses03chrome, Sunglasses
 * sunglasses03gold, Sunglasses
 * captainslog, Captain's Log
 * fishing.tackle, Fishing Tackle
 * bottle.vodka, Vodka Bottle
 * vehicle.2mod.camper, Camper Vehicle Module
 * skull, Skull
 * rifle.lr300, LR-300 Assault Rifle
 *
 * Update 1.3.4
 * Added new reset Command for the search bar
 * Added new set Command for the search bar
 * Updated Category Descriptions
 * ( Type reset in any category search bar and it will reset that whole category for you! )
 * ( Type set 8 in any category search bar and it will set that whole category for you to 8! )
 *
 * Update 1.3.5
 * Re-fixed vendor problem..
 *
 * Notice
 * I will not be providing any more updates for this, this month.
 *
 * Update 1.3.7
 * Fixes ( Stack problems with different stack sizes for different storages )
 * WARNING!
 * Potentially heavy update.. idk if this will crash 1000000mil x Servers or not!
 *
 * Update 1.3.71
 * Code Cleanup + improvements / back to permanent
 * Fixed Reloading problems
 * Added some missed checks for ImageLibrary to resolve
 * Removed Admin Toggle Config Option. ( it's hardcoded to ignore admins )
 *
 * Update 1.4.0
 * Partial UI Re-write/Optimizations!
 * More config options!
 * New command sluts! /stackmodifiercolor
 * Fixed Search bar commands! ( reset & set ) now work excellently!
 * Search parameters work 200% better than before!
 * Added Patch for Nivex's RaidableBases Plugins
 * This update was brought to you in conjunction with baz!
 *
 * Update 1.4.1
 * Fixed UI Constantly Re-Updating the Multiplier Descriptions ( when not needed ).
 * Another UI Performance update/Tweak
 * Fixed Sunglasses Display Names
 * Added Lang API Support from codefling https://codefling.com/plugins/lang-api?tab=details
 *
 * update 1.4.2
 * Re-fixed ignore admin check
 *
 * update 1.4.4
 * Optional Update
 * Re-designed UI System
 * Added All Category
 *
 * update 1.4.7
 * Updated for rust update
 * Added the following new items
 * rhib
 * rowboat
 * snowmobile
 * snowmobiletomaha
 * hazmatsuit.arcticsuit
 * hazmatsuit_scientist_arctic
 * spraycan
 * rifle.ak.ice
 *
 * update 1.4.8
 * Updated for rust update
 * Added the following new items
 * bluedogtags
 * dogtagneutral
 * reddogtags
 * attire.egg.suit
 * sign.egg.suit
 *
 * update 1.4.9
 * Added 1 new feature
 * Disable Images toggle: Disables ImageLibrary Requirement / Images for UI Editor
 *
 * update 1.5.0
 * Fixed loot panel UI not updating value counts on items when splitting a skinned item stack inside of a loot panel container into a players inventory
 *
 * update 1.5.1
 * Removed All ImageLibrary support / code All plugins moving forward will strictly use built in FacePunch Native logic.
 * Re-designed CanStack Hook re-coded fixing all rock skinned stacking bugs etc
 * Big performance gains
 * Improved search functions by 10 fold.
 *
 * update 1.5.2
 * patched oncardswipe error
 *
 * update 1.5.3
 * patched scientist suit having same name as peacekeeper scientist suit
 * Fixed Stacking Problems with scientist suit & peacekeeper scientist suit
 * Native FacePunch Bug / If you look in F1 menu for scientist suit you'll see what I mean.
 *
 * update 1.5.4
 * added 3 new items
 *
 * update 1.6.0
 * WARNING!! OPTIONAL UPDATE! HAS MANY CHANGES THAT MAY CHANGE THE DEFAULT BEHAVIOR YOUR USED TO.
 * Replaced blocked list with a new bool toggle system
 * Fixed a couple ui issues displaying the wrong value to you that it's actually using.
 * Fixed the stack multiplier fields so they actually multiple the default values now.
 * StackModifier will now check the item contents capacity & if it does not equal the same it will deny the stack.
 * Search Input Field has 5 command options now, disableon | disableoff | set 100 | multiply 5 | reset
 * Thanks to Jakes commit I can now remove the vending machine repair code!
 * https://commits.facepunch.com/412359
 * Fixed the following item display names being messed up by facepunch.
 * skullspikes.candles
 * skullspikes.pumpkin
 * skull.trophy.jar
 * skull.trophy.jar2
 * skull.trophy.table
 * innertube.horse
 * innertube.unicorn
 * sled.xmas
 * discofloor.largetiles
 *
 * Update 1.6.1
 * Added multiple safe guards to prevent setting item stack sizes to Zero..
 * Added locomotive
 * Added trophy
 * Added grenade.flashbang
 * Added grenade.molotov
 * Added Hook > OnIgnoreStackSize
 *
 * Update 1.6.2
 * Adjusted OnItemAddedToContainer
 *
 * Update 1.6.3
 * Added new game items & updated chocolate to the new name
 *
 * Update 1.6.4
 * Added another 3 new game items
 *
 * Update 1.6.5
 * Moved config correction code to beginning of logic.
 * Delayed server update by 3 & 5 on load.
 *
 * Update 1.6.6
 * Fixes new config generation bug caused by old debug code.
 * Created Correction logic for owners attempting to set negative values in the config.
 * Created Corrections log file for owners to see what all got corrected.
 * TODO: Re-write the check config script into a coroutine and clean that shit up..
 *
 * Update 1.6.7
 * added 2 new rust game items from today's update.
 *
 * Update 1.6.8
 * Added 4 new game items, Removed Car Key since FacePunch removed the item from the game..
 * Fixed Server Setting Stack Size bugs
 *
 * Update 1.6.9
 * Added hat.rabbitmask 1
 *
 * Update 1.7.0
 * Added New Game Items
    "electric.furnace" 1
    "storageadaptor" 5
    "industrial.combiner" 5
    "industrial.conveyor" 5
    "industrial.crafter" 5
    "industrial.splitter" 5
    "pipetool" 1
 *
 * Update 1.7.1
 * Fixed
 * multiply & set search commands in the UI
 *
 * Update 1.7.2
 * Added new cam
 *
 * Update 2.0.0
 *  UI Tweak + is now paid
 *
 * Update 2.0.1
 * Added 3 new game items
 * Single & Double Horse saddle
 * New easter Egg
 *
 * 
 * Update 2.0.2
Patched for latest "pending" rust-update.
Added The following new items.

    hazmatsuit_scientist_nvgm, 1
    blueidtag, 5000
    grayidtag, 5000
    greenidtag, 5000
    orangeidtag, 5000
    pinkidtag, 5000
    purpleidtag, 5000
    redidtag, 5000
    whiteidtag, 5000
    yellowidtag, 5000
    supertea, 1

 * Update 2.0.3
 * Optional Weapon Attachment stack fix. ( defaults on )
 *
 * Update 2.0.4
 * Removed left over test code from previous patch work in OnItemAddedToContainer
 *
 * Update 2.0.5
 * Now prevents inputs of -1 as a stack value.. or any negative digit, ever..
 *
 * Update 2.0.6
 * Added toggle option for disabling weapon attachment fix.
 * ( Added in 2.0.3 )
 *
 * Update 2.0.7
 * FacePunch removed sign.egg.suit
 * Fixed unload error.
 * Fixed server shutdown error ( if reset toggle is true & server is shutting down it will now do nothing. )
 * Fixed a bug if you set all multipliers to 1..  ( 10 x 1 is still 10 people. ) that would cause a handful of specific items to reset to default..
 * Now prevents setting multipliers of 1.
 * 
 * Update 2.0.8
 * Added 5 new game items
 * Fixed UI Bug.
 * 
 * Update 2.0.9
 * Added new item clantable.
 * Added item.text support for 3rd party plugin checks.
 * Added harmony patch to over-ride ItemModWearable.CanExistWith ( removed in current release )
 * ( This fixes custom clothing items being bugged specifically )
 * Officially compatible with imthenewguy's work.
 *
 * Update 2.1.0
 * Code-Cleanup
 *
 * Update 2.1.1
 * Updated for rust update.
 *
 * Update 2.2.0
 * Fixed stacking Candy Cane Club without losing the stack while lick. ( includes config toggle option to disable fix )
 * Fixed the All Category UI Bugs when using the search setter command functions.
 * Added 10 missing game items to defaults.
 * Updated OnCardSwipe code to correctly handle stack sizes for the cards instead of just spreading them all by 1..
 * Added 2 new config toggle to enable / disable other fixes.
 *
 * Update 2.2.1
 * Removed reset toggle on unload option.
 * Added Reset Console cmd & chat command + config setter.
 * Fixed UI Bug where the page number was not resetting when switching between categories
 * ( making it appear theirs nothing but if you click the page back button it would have showed you everything still as normal.. )
 *
 * Update 2.2.4
 * Added 3 new halloween items.
 *
 * Update 2.3.0
 * Added new game content / defaults
 * Performance update switched from string comparison to int
 * Added support for modified presents for unwrapping
 * Added Missing instance data checks
 *
 * Update 2.3.1
 * Added 11 new game items
 *
 * Update 2.3.2 / 2.3.3
 * Added OnStackSizeUpdated API Hook ( useful for plugins that cache max stackable sizes )
 * This api addition will allow plugins to update their max stack caches by being notified after the UI Editor is closed.
 * I noticed this problem when I was coding out a new plugin that I made where I cached max stacks for faster lookups..
 * Added new game items ( hazmatyoutooz & heavyscientistyoutooz )
 *
 * Update 2.3.4
 * Added 2 new game items. ( that or their id's changed for some reason )
 * Missing chicken.costume, -152332823, 1
 * Missing walkietalkie, -1416322465, 1
 *
 * Update 2.3.5
 * Fixed Duplication bug for those using category multipliers & reloading the plugin after server is already booted.
 * Updated code checks to improve debug checks for rare catch-cases.
 *
 * Update 2.3.6
 * API hook Interface.CallHook("OnStackSizeUpdated"); is now also called on plugin loaded right after the plugin updates the stack sizes on server reboots & when reloading the plugin.
 * It's also still called whenever an update is done from the UI editor.
 * Performance tweaks & null check adjustments.
 * Adjusted CanCombineDroppedItem code.
 *
 * update 2.3.7
 * prepared for today's coming update.
 *
 * Update 2.3.8
 * Fixed F1 spawns ( example fuel missing from lantern crafts )
 * Fixed crafted items ( example fuel missing from lantern crafts )
 * Credits to whitethunder for pointing out a flaw in the logic.
 *
 * Update 2.3.9
 * fix for console error.
 *
 * Update 2.4.0
 * Rewrote Discrepancies Checks ( Its a whole region now.. shesh what a nightmare )
 * Rewrote how i generate default config files to better ensure valid default settings.
 * Fixed missing null check in code edit.
 *
 * 2.4.2
 * Updated config checks for missing items / invalid values
 *
 * 2.4.3
 * added new game item SKS_-348232115_rifle.sks_1
 *
 * 2.5.0
 * Patches Industrial Conveyor stack issues. ( fixes plant genetics, bag of holding capacities, items with loot like candle hats with fuel, custom items, skill tree items & cooking items, etc )
 * New Not supported plugins.
 * ItemPerks by imthenewguy is not supported & will cause stack bugs / problems just by having this plugin on your server alone due to harmony patching done inside it, also the repair logic is bugged.
 * IQAlcoholFarm by BadMandarin/Mercury is not supported & will cause stack bugs / problems ( could be supported with an author update, but current version is not supported )
 * Custom Skins Stacks Fix ( not needed )
 * IndustrialCustomSkinsFix ( not needed + doesn't check everything right so its broken by its self.. )
 *
 * 2.5.1
 * Greatly Improved & reduced CanStack Hook logic & performance
 * Fixed on-item split for salt water stacking
 * Fixed F1 spawn issues with liquid contents ( like water guns losing spawned contents when stacked )
 * Forgot about the condition value checks for industrial conveyor ( is now patched ) & paintings..
 * More items can now stack in general with this update.
 * Improved performance for Industrial Conveyor
 *
 * 2.5.2
 * Completely rewrote OnItemSplit logic drastically improving performance and reduced logic checking.
 * Fixed presents in Conveyors..
 * Fixed bota / liquid containers in Conveyors..
 * Fixed liquid containers not retaining some specific data when split ( like custom item based bota bags )
 * Fixed an extremely specific stack bug where container was null to eject items to.
 * Added config toggle to turn off Industrial patch for those that don't want it. ( until its 100% sorted )
 * Further reduced performance overhead for item creation of stacked items.
 *
 * 2.5.3
 * Fixed bugged vanilla item creation on specific rust items like metal ore, etc.
 * ( Condition values are being set on items that have condition disabled & not have the condition.max setting > 0 ) thus prevents ( metal ore from stacking with old ore ) & other bugged items.
 * Updated Condition Handling to deal with this nonsense from facepunch.
 *
 * 2.5.5
 * Corrected condition split miss-hap after allowing conditions based items to stack with same condition variables from previous update.
 *
 * 2.5.6
 * Patched paintings stacking when content was painted on them.
 * Should still allow empty paintings to stack.
 *
 * Finally patched Head Bags from various animals/players from stacking
 * Should now deny the stack to prevent loss of data.
 *
 * 2.5.7
 * Added missing default. Seismic Sensor
 * The following vanilla stack sizes have changed.
    1296788329_ammo.rocket.seeker_4_2
    -1507239837_electric.hbhfsensor_5_1
    443432036_fluid.switch_5_1
    -365097295_powered.water.purifier_3_1
    -1284169891_waterpump_3_1
    140006625_ptz.cctv.camera_5_64
    -143132326_sign.wooden.huge_1_5
    1153652756_sign.wooden.large_1_5
    -1819233322_sign.wooden.medium_1_5
    -1138208076_sign.wooden.small_1_5
    42535890_sign.neon.125x215.animated_5_1
    866332017_sign.neon.xl_5_1
    -1982036270_hq.metal.ore_100_1000
    
    2.5.8
    Added new game items.
    
 * Update 2.5.91
 * Fixed OnItemAddedToContainer Memory leak, resulting in increased memory usage over time.
 * Added new game item Tin Can Alarm to defaults.
 * Added new game item 1174957864
 
 * Update 2.6.0
 * More performance improvements.
 * Added 2 new game items.
 * Fixed OnCardSwipe so it will only touch the stack if the card reader access level matches the swipe.
 * Updated for pooling changes.
 *
 * Update 2.6.2
 * Added config checks to fix item category changes.. ( like what happened with the weapon.mod.gascompressionovedrive )
 * ( From component to weapon category )
 *
 * 2.6.4
 * New game items.
 * Added server console multiply cmd, will set the servers multiplier for item stacks.
 * Bug Fixes
*/

namespace Oxide.Plugins
{
    [Info("Stack Modifier", "Khan", "2.6.6")]
    [Description("Modify item stack sizes, includes UI Editor")]
    public class StackModifier : RustPlugin
    {
        #region License Agreement (EULA) of Stack Modifier

        /*
        End-User License Agreement (EULA) of Stack Modifier

        This End-User License Agreement ("EULA") is a legal agreement between you and Kyle. This EULA agreement
        governs your acquisition and use of our Stack Modifier plugin ("Software") directly from Kyle.

        Please read this EULA agreement carefully before downloading and using the Stack Modifier plugin.
        It provides a license to use the Stack Modifier and contains warranty information and liability disclaimers.

        If you are entering into this EULA agreement on behalf of a company or other legal entity, you represent that you have the authority
        to bind such entity and its affiliates to these terms and conditions. If you do not have such authority or if you do not agree with the
        terms and conditions of this EULA agreement, DO NOT purchase or download the Software.

        This EULA agreement shall apply only to the Software supplied by Kyle Farris regardless of whether other software is referred
        to or described herein. The terms also apply to any Kyle updates, supplements, Internet-based services, and support services for the Software,
        unless other terms accompany those items on delivery. If so, those terms apply.

        License Grant

        Kyle hereby grants you a personal, non-transferable, non-exclusive license to use the Stack Modifier software on your devices in
        accordance with the terms of this EULA agreement. You are permitted to load the Stack Modifier on your personal server owned by you.

        You are not permitted to:

        Edit, alter, modify, adapt, translate or otherwise change the whole or any part of the Software nor permit the whole or any part
        of the Software to be combined with or become incorporated in any other software, nor decompile, disassemble or reverse
        engineer the Software or attempt to do any such things.
        Reproduce, copy, distribute, resell or otherwise use the Software for any commercial purpose
        Allow any third party to use the Software on behalf of or for the benefit of any third party
        Use the Software in any way which breaches any applicable local, national or international law
        use the Software for any purpose that Kyle considers is a breach of this EULA agreement

        Intellectual Property and Ownership

        Kyle shall at all times retain ownership of the Software as originally downloaded by you and all subsequent downloads of the Software by you. 
        The Software (and the copyright, and other intellectual property rights of whatever nature in the Software, including any modifications made thereto) are and shall remain the property of Kyle.

        Termination

        This EULA agreement is effective from the date you first use the Software and shall continue until terminated. 
        You may terminate it at any time upon written notice to Kyle.
        It will also terminate immediately if you fail to comply with any term of this EULA agreement. 
        Upon such termination, the licenses granted by this EULA agreement will immediately terminate and you agree to stop all access and use of the Software. 
        The provisions that by their nature continue and survive will survive any termination of this EULA agreement.
        */

        #endregion

        #region Fields

        [PluginReference] Plugin LangAPI;
        private Harmony _harmony;

        private Hash<ulong, int> _editorPageSM = new Hash<ulong, int>();

        //TODO: Code in feature request for stack overrides
        private const string Admin = "stackmodifier.admin";
        private const string AllC = "All";

        private const int Candy = 1789825282;

        private static Dictionary<int, int> _defaults = null;

        private static Dictionary<int, int> _FP = new Dictionary<int, int>
        {
            {-92315244, 1},
            {-1647389398, 1},
            {809689733, 1},
            {-105415879, 1},
            {-874908751, 1},
            {-880412831, 1},
            {1937380239, 1},
            {-1056824343, 1},
            {-1767794021, 1},
            {1174957864, 1},
            {962186730, 1},
            {-348232115, 1},
            {-1315992997, 1},
            {-1536855921, 1},
            {695450239, 1},
            {-907422733, 1},
            {2068884361, 1},
            {1168856825, 1},
            {-1478212975, 1},
            {-1973785141, 1},
            {2104517339, 1},
            {1113514903, 1},
            {190184021, 1},
            {1394042569, 1},
            {1878053256, 1},
            {-561148628, 1},
            {1426574435, 1},
            {343045591, 1},
            {-1449152644, 1},
            {-1843426638, 6},
            {-1884328185, 1},
            {-1364246987, 1},
            {1768112091, 1},
            {1015352446, 1},
            {-187031121, 1},
            {-2027988285, 1},
            {996757362, 1},
            {-810326667, 1},
            {1055319033, 24},
            {349762871, 12},
            {915408809, 12},
            {-1023065463, 64},
            {-1234735557, 64},
            {215754713, 64},
            {14241751, 64},
            {588596902, 64},
            {-2097376851, 64},
            {785728077, 128},
            {51984655, 128},
            {-1691396643, 128},
            {-1211166256, 128},
            {-1321651331, 128},
            {605467368, 128},
            {1712070256, 128},
            {-742865266, 3},
            {1638322904, 3},
            {-1841918730, 3},
            {1296788329, 4},
            {-17123659, 3},
            {-1685290200, 64},
            {-1036635990, 64},
            {-727717969, 32},
            {-1800345240, 64},
            {-1671551935, 100},
            {1390353317, 1},
            {1221063409, 1},
            {-1336109173, 1},
            {-2067472972, 1},
            {1353298668, 1},
            {1729120840, 1},
            {936496778, 10},
            {1948067030, 1},
            {1983621560, 10},
            {2041899972, 1},
            {-691113464, 1},
            {-335089230, 1},
            {-316250604, 5},
            {-967648160, 10},
            {99588025, 10},
            {-956706906, 1},
            {-1429456799, 10},
            {1451568081, 1},
            {-1117626326, 10},
            {-148794216, 1},
            {1516985844, 5},
            {-796583652, 1},
            {-148229307, 1},
            {-819720157, 10},
            {671706427, 10},
            {-1183726687, 10},
            {-1199897169, 20},
            {-1199897172, 20},
            {-1614955425, 10},
            {-1023374709, 20},
            {-463122489, 5},
            {296519935, 1},
            {-113413047, 1},
            {-2022172587, 1},
            {-1101924344, 1},
            {-1000573653, 1},
            {-1215166612, 1},
            {1189981699, 1},
            {1659114910, 1},
            {21402876, 1},
            {1366282552, 1},
            {-699558439, 1},
            {-1108136649, 1},
            {-258574361, 1},
            {1865253052, 1},
            {-1043618880, 1},
            {277730763, 1},
            {273951840, 1},
            {809942731, 1},
            {-1334569149, 1},
            {3222790, 1},
            {1675639563, 1},
            {-23994173, 1},
            {850280505, 1},
            {1877339384, 1},
            {1714496074, 1},
            {-1022661119, 1},
            {968019378, 1},
            {-803263829, 1},
            {-1903165497, 1},
            {1181207482, 1},
            {-1539025626, 1},
            {-575744869, 1},
            {671063303, 1},
            {-2094954543, 1},
            {1751045826, 1},
            {1746956556, 1},
            {-1102429027, 1},
            {-48090175, 1},
            {-1163532624, 1},
            {418081930, 1},
            {-763071910, 1},
            {-2012470695, 1},
            {-702051347, 1},
            {-194953424, 1},
            {110116923, 1},
            {-1518883088, 1},
            {-1506417026, 1},
            {1992974553, 1},
            {-1778159885, 1},
            {1722154847, 1},
            {1850456855, 1},
            {-1695367501, 1},
            {832133926, 1},
            {237239288, 1},
            {980333378, 1},
            {602741290, 1},
            {-2025184684, 1},
            {196700171, 1},
            {1608640313, 1},
            {-1549739227, 1},
            {-761829530, 1},
            {794356786, 1},
            {-1773144852, 1},
            {-1622110948, 1},
            {-470439097, 1},
            {-797592358, 1},
            {1266491000, 1},
            {861513346, 1},
            {491263800, 1},
            {-253079493, 1},
            {1107575710, 1},
            {86840834, 1},
            {-1958316066, 1},
            {-560304835, 1},
            {-1772746857, 1},
            {1601468620, 1},
            {-97459906, 1},
            {935692442, 1},
            {223891266, 1},
            {1110385766, 1},
            {-1478855279, 1},
            {-2002277461, 1},
            {1553078977, 20},
            {1401987718, 20},
            {656371028, 5},
            {656371027, 5},
            {656371026, 5},
            {1158340334, 5},
            {1158340331, 5},
            {1158340332, 5},
            {1883981798, 10},
            {1883981801, 10},
            {1883981800, 10},
            {-89874794, 20},
            {-493159321, 20},
            {1072924620, 20},
            {1330084809, 15},
            {926800282, 15},
            {-1802083073, 15},
            {-629028935, 10},
            {479143914, 20},
            {-1899491405, 10},
            {1882709339, 20},
            {95950017, 20},
            {-1673693549, 5},
            {1199391518, 20},
            {1414245522, 50},
            {1234880403, 20},
            {-1994909036, 20},
            {-1021495308, 20},
            {642482233, 100},
            {2019042823, 20},
            {73681876, 50},
            {176787552, 10},
            {573926264, 10},
            {1230323789, 10},
            {-1950721390, 10},
            {1373240771, 10},
            {1655650836, 10},
            {-559599960, 10},
            {15388698, 10},
            {866889860, 10},
            {1382263453, 10},
            {1099314009, 1},
            {-582782051, 3},
            {-1273339005, 1},
            {1946219319, 1},
            {1142993169, 10},
            {1534542921, 5},
            {-1944704288, 5},
            {486661382, 1},
            {-1488398114, 1},
            {-1588628467, 1},
            {1588492232, 1},
            {-1519126340, 5},
            {1177596584, 5},
            {-1535621066, 1},
            {1744298439, 20},
            {1324203999, 20},
            {-656349006, 20},
            {-7270019, 20},
            {-379734527, 20},
            {-1553999294, 20},
            {-280223496, 20},
            {-515830359, 20},
            {-1306288356, 20},
            {-1486461488, 20},
            {-99886070, 20},
            {261913429, 20},
            {-454370658, 20},
            {-1538109120, 20},
            {-92759291, 10},
            {1575635062, 1},
            {1413014235, 1},
            {-1992717673, 1},
            {-1999722522, 1},
            {-1163943815, 1},
            {1277159544, 1},
            {1160881421, 1},
            {-1989600732, 1},
            {-1759188988, 1},
            {1242482355, 1},
            {-1824943010, 1},
            {-1663759755, 5},
            {1658229558, 1},
            {833533164, 1},
            {-1863559151, 1},
            {-110921842, 1},
            {-586784898, 1},
            {1259919256, 1},
            {1696050067, 1},
            {-1130709577, 1},
            {-1293296287, 1},
            {1581210395, 10},
            {1903654061, 10},
            {2100007442, 5},
            {-695978112, 5},
            {988652725, 5},
            {1149964039, 1},
            {553270375, 1},
            {2023888403, 1},
            {-692338819, 1},
            {-1778897469, 5},
            {-216999575, 5},
            {-1507239837, 5},
            {-798293154, 5},
            {-2049214035, 1},
            {-502177121, 5},
            {-1196547867, 1},
            {-784870360, 5},
            {-265292885, 5},
            {-1166712463, 5},
            {443432036, 5},
            {1171735914, 5},
            {-690968985, 5},
            {-1448252298, 5},
            {-458565393, 5},
            {-746647361, 5},
            {-1286302544, 5},
            {492357192, 5},
            {-1044468317, 1},
            {888415708, 1},
            {1293102274, 5},
            {1849887541, 1},
            {-295829489, 1},
            {2090395347, 3},
            {-44876289, 3},
            {-1049172752, 5},
            {1538126328, 5},
            {610102428, 5},
            {1430085198, 5},
            {742745918, 5},
            {-939424778, 5},
            {-282113991, 1},
            {762289806, 5},
            {-365097295, 3},
            {1951603367, 5},
            {-563624462, 5},
            {-781014061, 10},
            {1371909803, 3},
            {665332906, 5},
            {1835946060, 1},
            {-1284169891, 3},
            {140006625, 5},
            {1052926200, 1},
            {-1736356576, 1},
            {803222026, 1},
            {-1861522751, 1},
            {-1104881824, 1},
            {-1985799200, 1},
            {2087678962, 1},
            {567871954, 5},
            {1950721418, 10},
            {23352662, 5},
            {1205607945, 5},
            {-1647846966, 5},
            {-845557339, 5},
            {-1370759135, 5},
            {121049755, 5},
            {-996185386, 5},
            {98508942, 5},
            {2070189026, 5},
            {1521286012, 5},
            {1542290441, 5},
            {-1832422579, 5},
            {826309791, 5},
            {-143132326, 1},
            {1153652756, 1},
            {-1819233322, 1},
            {-1138208076, 1},
            {352499047, 1},
            {-1754948969, 1},
            {-369760990, 5},
            {-555122905, 2},
            {782422285, 2},
            {-1100422738, 1},
            {559147458, 5},
            {593465182, 1},
            {1524187186, 1},
            {-41896755, 1},
            {-1607980696, 1},
            {-97956382, 1},
            {-1478445584, 1},
            {198438816, 1},
            {-1100168350, 1},
            {-132247350, 1},
            {2114754781, 1},
            {-246672609, 10},
            {1973949960, 10},
            {-849373693, 10},
            {-52398594, 10},
            {1132603396, 10},
            {240752557, 10},
            {-96256997, 10},
            {-1819763926, 1},
            {-180129657, 1},
            {1548091822, 10},
            {352130972, 1},
            {1931713481, 1},
            {-586342290, 20},
            {613961768, 1},
            {-568419968, 20},
            {1770475779, 20},
            {1783512007, 10},
            {-700591459, 10},
            {-1941646328, 10},
            {-965336208, 10},
            {342438846, 10},
            {-587989372, 5},
            {1668129151, 20},
            {989925924, 20},
            {-1698937385, 10},
            {-542577259, 10},
            {-1904821376, 5},
            {-851988960, 10},
            {-1654233406, 10},
            {-1768880890, 5},
            {-1878764039, 10},
            {680234026, 10},
            {-746030907, 10},
            {1973684065, 20},
            {-1848736516, 20},
            {-1440987069, 20},
            {-751151717, 20},
            {-78533081, 20},
            {-1509851560, 20},
            {1422530437, 20},
            {1917703890, 20},
            {-1162759543, 20},
            {-1130350864, 20},
            {-682687162, 20},
            {1536610005, 20},
            {-1709878924, 20},
            {1272768630, 20},
            {-989755543, 20},
            {1873897110, 20},
            {-1520560807, 20},
            {1827479659, 20},
            {813023040, 20},
            {-395377963, 20},
            {-1167031859, 20},
            {1391703481, 20},
            {-242084766, 20},
            {621915341, 20},
            {-1962971928, 10},
            {286193827, 10},
            {-1039528932, 1},
            {-119235651, 1},
            {-2107018088, 1},
            {-1049881973, 1},
            {-1330640246, 1},
            {-2040817543, 1},
            {-2124352573, 1},
            {-979951147, 1},
            {1272430949, 1},
            {-1379036069, 1},
            {273172220, 1},
            {1784406797, 1},
            {-211235948, 1},
            {-1112793865, 1},
            {-850982208, 10},
            {1159991980, 10},
            {-996920608, 1000},
            {-1916473915, 1},
            {-854270928, 1},
            {-22883916, 1},
            {-961457160, 1},
            {1315082560, 1},
            {-986782031, 1},
            {271048478, 1},
            {1819863051, 20},
            {-1770889433, 20},
            {-1824770114, 20},
            {831955134, 20},
            {-1433390281, 20},
            {-1961560162, 5},
            {709206314, 1},
            {359723196, 1},
            {-1151332840, 1},
            {-1274093662, 10},
            {209218760, 1},
            {-1913996738, 1},
            {960673498, 1},
            {-869598982, 1},
            {1361520181, 10},
            {615112838, 10},
            {-1863063690, 5},
            {1758333838, 5},
            {192249897, 5},
            {-2073432256, 1},
            {-258457936, 1},
            {1307626005, 1},
            {-1421257350, 1},
            {446206234, 1},
            {301063058, 1},
            {-1265020883, 1},
            {1463862472, 1},
            {-1344017968, 1},
            {1036321299, 5000},
            {1223900335, 5000},
            {-602717596, 5000},
            {1409529282, 1},
            {-1004426654, 1},
            {-1266045928, 1},
            {23391694, 1},
            {-979302481, 1},
            {1856217390, 1},
            {-747743875, 1},
            {-173268129, 1},
            {-173268132, 1},
            {-173268131, 1},
            {-173268126, 1},
            {-173268125, 1},
            {-173268128, 1},
            {1081315464, 1},
            {844440409, 10},
            {-1002156085, 10},
            {-126305173, 1000},
            {1757265204, 10},
            {-888153050, 1000},
            {-489848205, 1},
            {-2058362263, 1},
            {1524980732, 1},
            {573676040, 1},
            {1242522330, 1},
            {809199956, 1},
            {699075597, 1},
            {-134959124, 1},
            {106959911, 1},
            {-1624770297, 1},
            {-1732475823, 1},
            {835042040, 1},
            {1491753484, 1},
            {-297099594, 1},
            {-2024549027, 1},
            {1614528785, 1},
            {-1679267738, 10},
            {479292118, 10},
            {1899610628, 10},
            {1319617282, 10},
            {1346158228, 1},
            {177226991, 5},
            {-25740268, 1},
            {-1078639462, 1},
            {-1073015016, 1},
            {-216116642, 1},
            {553887414, 1},
            {882559853, 10},
            {1885488976, 1},
            {-1785231475, 1},
            {971362526, 1},
            {-924959988, 1},
            {-156748077, 1},
            {-769647921, 1},
            {1364514421, 5000},
            {-455286320, 5000},
            {1762167092, 5000},
            {-282193997, 5000},
            {180752235, 5000},
            {-1386082991, 5000},
            {70102328, 5000},
            {22947882, 5000},
            {81423963, 5000},
            {242933621, 1},
            {2055695285, 1},
            {340210699, 1},
            {1787198294, 1},
            {-996235148, 1},
            {-1901993050, 1},
            {-1836526520, 1},
            {-1528767189, 1},
            {1365234594, 1},
            {-1804515496, 1},
            {-1444650226, 1},
            {2120241887, 1},
            {242421166, 1},
            {-1294739579, 1},
            {1691223771, 1},
            {1950013766, 1},
            {450531685, 1},
            {1028889957, 1},
            {-389796733, 1},
            {1916016738, 1},
            {-1094453063, 1},
            {-1060567807, 1},
            {-498301781, 1},
            {-1774190142, 1},
            {-82758111, 1},
            {839738457, 1},
            {-1050697733, 1},
            {-1380144986, 1},
            {-1683726934, 1},
            {-635951327, 1},
            {-1541706279, 1},
            {-1476278729, 1},
            {1769475390, 1},
            {1312679249, 1},
            {756125481, 1},
            {-1497205569, 1},
            {723407026, 1},
            {3380160, 1},
            {-2047081330, 1},
            {1414245162, 1},
            {1784005657, 1},
            {602628465, 1},
            {2054391128, 1},
            {1268178466, 10},
            {1623701499, 10},
            {-1160621614, 10},
            {996293980, 1},
            {1840570710, 1},
            {-321431890, 1},
            {-1621539785, 1},
            {657352755, 1},
            {-8312704, 1},
            {-1478094705, 1},
            {-697981032, 1},
            {185586769, 1},
            {2052270186, 1},
            {-2001260025, 1},
            {-733625651, 1},
            {62577426, 1},
            {1697996440, 1},
            {1205084994, 1},
            {1729712564, 1},
            {1258768145, 1},
            {-2103694546, 1},
            {1557173737, 1},
            {-176608084, 1},
            {-1997698639, 1},
            {-1408336705, 1},
            {352321488, 1},
            {722955039, 1},
            {-1815301988, 1},
            {975983052, 1},
            {20489901, 1},
            {-1569700847, 1},
            {-1442559428, 1},
            {818733919, 1},
            {-2027793839, 1},
            {1789825282, 1},
            {1058261682, 20},
            {674734128, 10},
            {-1230433643, 10},
            {1121925526, 1},
            {-695124222, 1},
            {1327005675, 10},
            {-985781766, 10},
            {282103175, 1},
            {1305578813, 5},
            {42535890, 5},
            {-1423304443, 5},
            {1643667218, 5},
            {866332017, 5},
            {-1651220691, 1},
            {-151387974, 150},
            {204391461, 1},
            {-1622660759, 1},
            {756517185, 5},
            {-722241321, 10},
            {-135252633, 1},
            {-333406828, 1},
            {1358643074, 1},
            {-363689972, 1},
            {1103488722, 1},
            {1629293099, 5},
            {-465682601, 1},
            {1668858301, 5},
            {-558880549, 1},
            {-324675402, 1},
            {2126889441, 1},
            {-575483084, 1},
            {-842267147, 1},
            {-1379835144, 10},
            {204970153, 1},
            {1094293920, 1},
            {2009734114, 1},
            {-1667224349, 1},
            {-209869746, 1},
            {1686524871, 1},
            {1723747470, 1},
            {-129230242, 1},
            {-1331212963, 1},
            {2106561762, 1},
            {794443127, 1},
            {1230691307, 1},
            {-1707425764, 1},
            {755224797, 1},
            {-2139580305, 1},
            {528668503, 1},
            {-690276911, 1},
            {-384243979, 1000},
            {-1009359066, 1},
            {1771755747, 20},
            {122783240, 50},
            {1911552868, 50},
            {1112162468, 20},
            {838831151, 50},
            {803954639, 50},
            {858486327, 20},
            {-1305326964, 50},
            {-1776128552, 50},
            {1272194103, 20},
            {2133269020, 50},
            {830839496, 50},
            {854447607, 20},
            {1533551194, 50},
            {-992286106, 50},
            {1660145984, 20},
            {390728933, 50},
            {-520133715, 50},
            {1367190888, 20},
            {-778875547, 50},
            {998894949, 50},
            {-886280491, 50},
            {-237809779, 50},
            {-2086926071, 20},
            {1512054436, 50},
            {-2084071424, 50},
            {-567909622, 20},
            {1898094925, 50},
            {-1511285251, 50},
            {-1018587433, 1000},
            {609049394, 1},
            {1776460938, 1000},
            {1719978075, 1000},
            {634478325, 64},
            {-1938052175, 1000},
            {-858312878, 1000},
            {-321733511, 500},
            {1568388703, 20},
            {1655979682, 10},
            {-1557377697, 10},
            {-592016202, 100},
            {-930193596, 1000},
            {-265876753, 1000},
            {-1579932985, 100},
            {-1982036270, 100},
            {317398316, 100},
            {1381010055, 1000},
            {-946369541, 500},
            {69511070, 1000},
            {-4031221, 1000},
            {-1779183908, 1000},
            {-804769727, 1000},
            {-544317637, 1000},
            {-932201673, 1000},
            {-2099697608, 1000},
            {-1157596551, 1000},
            {-1581843485, 1000},
            {1523195708, 64},
            {2048317869, 1},
            {-151838493, 1000},
            {-2123125470, 10},
            {-929092070, 10},
            {-1677315902, 10},
            {603811464, 10},
            {-1184406448, 10},
            {1712261904, 10},
            {2063916636, 10},
            {1480022580, 10},
            {1729374708, 10},
            {2021351233, 10},
            {-496584751, 10},
            {1905387657, 10},
            {-1729415579, 10},
            {-487356515, 10},
            {-33009419, 10},
            {524678627, 10},
            {263834859, 10},
            {2024467711, 10},
            {-1003665711, 1},
            {-541206665, 10},
            {-649128577, 10},
            {-557539629, 10},
            {-1432674913, 10},
            {-1262185308, 1},
            {1248356124, 10},
            {-1316706473, 1},
            {596469572, 1},
            {1569882109, 1},
            {304481038, 5},
            {-196667575, 1},
            {999690781, 1},
            {363163265, 1},
            {1488979457, 1},
            {-484206264, 1},
            {37122747, 1},
            {-1880870149, 1},
            {254522515, 1},
            {1176355476, 1},
            {-1360171080, 1},
            {-399173933, 1},
            {236677901, 1},
            {696029452, 1},
            {1079279582, 2},
            {-566907190, 1},
            {-144513264, 1},
            {1525520776, 1},
            {1263920163, 3},
            {-596876839, 1},
            {-1366326648, 10},
            {1397052267, 1},
            {1975934948, 10},
            {-144417939, 1},
            {-44066600, 1},
            {-44066823, 1},
            {-44066790, 1},
            {1770744540, 1},
            {-1501451746, 1},
            {1874610722, 1},
            {170758448, 1},
            {1559779253, 1},
            {-1880231361, 1},
            {-1615281216, 1},
            {1376065505, 1},
            {268565518, 1},
            {-626174997, 1},
            {-1040518150, 1},
            {-1693832478, 1},
            {1186655046, 1},
            {895374329, 1},
            {878301596, 1},
            {-1113501606, 1},
            {576509618, 1},
            {476066818, 1},
            {-912398867, 1},
            {1523403414, 1},
            {-1530414568, 1},
            {1895235349, 5},
            {286648290, 5},
            {1735402444, 5},
            {968421290, 5},
            {853471967, 5},
            {-583379016, 1},
            {39600618, 5},
            {-20045316, 1},
            {-343857907, 5},
            {1234878710, 1},
            {174866732, 1},
            {838308300, 1},
            {2005491391, 1},
            {952603248, 1},
            {442289265, 1},
            {-132516482, 1},
            {-1405508498, 1},
            {1478091698, 1},
            {-855748505, 1},
            {-1850571427, 1},
            {567235583, 1},
            {1545779598, 1},
            {-139037392, 1},
            {-1335497659, 1},
            {-2072273936, 3},
            {1840822026, 5},
            {1588298435, 1},
            {1711033574, 1},
            {1814288539, 1},
            {1443579727, 1},
            {1973165031, 1},
            {1104520648, 1},
            {-1978999529, 1},
            {884424049, 1},
            {1965232394, 1},
            {1046904719, 1},
            {1561022037, 1},
            {1846605708, 1},
            {-765183617, 1},
            {-75944661, 1},
            {143803535, 5},
            {-1215753368, 1},
            {-936921910, 5},
            {1914691295, 1},
            {-1123473824, 1},
            {-2026042603, 1},
            {-194509282, 1},
            {1090916276, 1},
            {-1368584029, 1},
            {-1175656359, 1},
            {1312843609, 1},
            {-885833256, 1},
            {200773292, 1},
            {-1252059217, 1},
            {-1214542497, 1},
            {-218009552, 1},
            {2040726127, 1},
            {-778367295, 1},
            {-1812555177, 1},
            {-2069578888, 1},
            {28201841, 1},
            {-852563019, 1},
            {-1966748496, 1},
            {-1137865085, 1},
            {1556365900, 5},
            {1318558775, 1},
            {1953903201, 1},
            {1491189398, 1},
            {-1302129395, 1},
            {-1367281941, 1},
            {1373971859, 1},
            {649912614, 1},
            {963906841, 1},
            {442886268, 1},
            {-262590403, 1},
            {-1506397857, 1},
            {-1780802565, 1},
            {-1878475007, 10},
            {795371088, 1},
            {818877484, 1},
            {-904863145, 1},
            {1796682209, 1},
            {-41440462, 1},
            {-1517740219, 1},
            {-1583967946, 1},
            {171931394, 1},
            {1602646136, 1},
            {-1469578201, 1},
            {1326180354, 1},
            {-1758372725, 1},
            {1803831286, 1},
            {795236088, 1},
            {1424075905, 1},
            {1540934679, 1},
            {60528587, 1},
            {1659447559, 1},
            {-1323101799, 1},
            {-1997543660, 1},
            {1559915778, 1},
            {1400460850, 1},
            {1989785143, 1},
            {-1211268013, 1},
            {607400343, 1},
            {468313189, 1},
            {-1310391395, 1},
            {1603174987, 1},
            {1223729384, 5000},
            {1572152877, 5000},
            {-1442496789, 5},
            {-901370585, 1},
            {-243540612, 5},
            {-73195037, 1},
            {678698219, 1},
            {-1696379844, 1},
            {-722629980, 1},
            {-770304148, 1},
            {-152332823, 1},
            {-1416322465, 1},
            {355877490, 1},
            {703057617, 1},
            {935606207, 1},
            {1488606552, 1},
            {-948291630, 10},
            {-1417478274, 1},
            {1869224826, 1},
            {821588319, 1},
            {1991794121, 1},
            {-892718768, 1},
            {-839576748, 1},
            {-1501434104, 10},
            {1619039771, 10},
        };

        private readonly List<string> _exclude = new List<string>
        {
            "water",
            "water.salt",
            "water.radioactive",
            "cardtable",
            "ammo.snowballgun",
            "sign.egg.suit",
            /*"rowboat",
            "rhib"*/
        };

        private readonly Dictionary<string, string> _corrections = new Dictionary<string, string>
        {
            {"sunglasses02black", "Sunglasses Style 2"},
            {"sunglasses02camo", "Sunglasses Camo"},
            {"sunglasses02red", "Sunglasses Red"},
            {"sunglasses03black", "Sunglasses Style 3"},
            {"sunglasses03chrome", "Sunglasses Chrome"},
            {"sunglasses03gold", "Sunglasses Gold"},
            {"twitchsunglasses", "Sunglasses Purple"},
            {"hazmatsuit_scientist_peacekeeper", "Peacekeeper Scientist Suit"},
            {"skullspikes.candles", "Skull Spikes Candles"},
            {"skullspikes.pumpkin", "Skull Spikes Pumpkin"},
            {"skull.trophy.jar", "Skull Trophy Jar"},
            {"skull.trophy.jar2", "Skull Trophy Jar 2"},
            {"skull.trophy.table", "Skull Trophy Table"},
            {"innertube.horse", "Inner Tube Horse"},
            {"innertube.unicorn", "Inner Tube Unicorn"},
            {"sled.xmas", "Xmas Sled"},
            {"discofloor.largetiles", "Disco Floor Large"},
        };

        #endregion

        #region Config

        private PluginConfig _config;

        private Dictionary<string, string> _itemMap = new Dictionary<string, string>();

        internal class PluginConfig : SerializableConfiguration
        {
            [JsonProperty("Disable Industrial Conveyor Patch (true = disabled)")]
            public bool IndustrialPatch = false;

            [JsonProperty("Disable Weapon Attachment stack fix (Unsubscribes from both OnWeaponModChange & CanMoveItem)")]
            public bool WeaponModFix = false;

            [JsonProperty("Disable Wearable Clothes fix (Unsubscribes from OnItemAddedToContainer)")]
            public bool DisableWearableFix = false;

            [JsonProperty("Disable Ammo/Fuel duplication fix (Recommended false)")]
            public bool DisableFix = false;

            [JsonProperty("Disable Candy Cane Club Lick fix & unwrap fix (Unsubscribes from OnItemAction)")]
            public bool LickFix = false;

            [JsonProperty("Disable OnCardSwipe fix (Unsubscribes from OnCardSwipe)")]
            public bool SwipeFix = false;

            [JsonProperty("VendingMachine Ammo Fix (Recommended true)")]
            public bool VendingMachineAmmoFix = true;

            [JsonProperty("Enable UI Editor")]
            public bool EnableEditor = true;

            [JsonProperty("Disable Images / Toggles off Images for UI Editor")]
            public bool DisableImages = false;

            [JsonProperty("Sets editor command")]
            public string modifycommand = "stackmodifier";

            [JsonProperty("Sets reset command for both console & chat")]
            public string resetcommand = "stackmodifier.reset";

            [JsonProperty("Sets editor color command")]
            public string colorcommand = "stackmodifiercolor";

            [JsonProperty("Sets Default Category to open")]
            public string DefaultCat = AllC;

            public Colors Colors = new Colors();

            [JsonProperty("Category Stack Multipliers", Order = 4)]
            public Dictionary<string, int> StackCategoryMultipliers = new Dictionary<string, int>();

            [JsonProperty("Stack Categories", Order = 5)]
            public Dictionary<string, Dictionary<string, _Items>> StackCategories = new Dictionary<string, Dictionary<string, _Items>>();


            public void ResetCategory(string cat)
            {
                if (cat == AllC)
                {
                    foreach (var cats in StackCategories.Values)
                    {
                        foreach (var i in cats)
                            i.Value.Modified = _defaults[i.Value.ItemId];
                    }

                    foreach (var value in StackCategories.Keys)
                        StackCategoryMultipliers[value] = 0;
                }
                else
                {
                    if (!StackCategoryMultipliers.ContainsKey(cat)) return;
                    StackCategoryMultipliers[cat] = 0;

                    foreach (var item in StackCategories[cat].Values)
                        item.Modified = _defaults[item.ItemId];
                }
            }

            public void SetCategory(string cat, int digit)
            {
                if (digit == 1)
                    digit = 0;
                if (cat == AllC)
                {
                    foreach (var value in StackCategories.Keys)
                        StackCategoryMultipliers[value] = digit;
                }
                else
                {
                    if (!StackCategoryMultipliers.ContainsKey(cat)) return;
                    StackCategoryMultipliers[cat] = digit;
                }
            }

            public void SetItems(string cat, int digit)
            {
                if (digit == 0)
                    digit = 1;

                if (cat == AllC)
                {
                    foreach (var cats in StackCategories.Values)
                    {
                        foreach (var i in cats)
                                i.Value.Modified = digit;
                    }
                }
                else
                {
                    if (!StackCategoryMultipliers.ContainsKey(cat)) return;
                    foreach (var item in StackCategories[cat].Values)
                        item.Modified = digit;
                }
            }

            public void ToggleCats(string cat, bool toggle)
            {
                if (cat == AllC)
                {
                    foreach (var cats in StackCategories.Values)
                    {
                        foreach (var i in cats)
                            i.Value.Disable = toggle;
                    }
                }
                else
                {
                    if (!StackCategoryMultipliers.ContainsKey(cat)) return;
                    foreach (var item in StackCategories[cat].Values)
                        item.Disable = toggle;
                }
            }
        }

        public class _Items
        {
            public string ShortName;
            public int ItemId;
            public string DisplayName;
            public int Modified;
            public bool Disable;
        }

        public class Colors
        {
            public Color InputPanel = new Color("#0E0E10", 0.98f);//"#0E0E10", 0.98f); "#FFFFFF", 0.2f);
            //public Color InputText = new Color("#FFE24B", 0.5f);
            public Color TextColor = new Color( "#FFFFFF");
            public Color Transparency = new Color("#", 0.95f);
            //public Color SearchLable = new Color("#FFFFFF", 0.05f);
            public Color DescriptionText = new Color("#FFFFFF", 0.5f);
            //public Color ButtonGreen = new Color("#556c31", 0.65f);
            public Color NewInputColor = new Color("#ffa805", 1f);
            public Color ButtonGreenText = new Color("#9ab36d", 0.431f);
            public Color ButtonGrey = new Color("#bfbfbf", 0.3f);
            public Color ButtonGreyText = new Color("#bfbfbf", 1f);

            public Color Enable = new Color( "#738D45");
            public Color Disable = new Color( "#CD412B");
        }

        public Color ButtonGreen = new Color("#a0ff70", 0.8f);

        #region Updater

        internal class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                            .ToDictionary(prop => prop.Name, prop => ToObject(prop.Value));
                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue) token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        #endregion

        #endregion

        #region Discrepancies Check

        // removed search common favorite All
        public List<string> Cats = new List<string>
        {
            "Weapon",
            "Construction",
            "Items",
            "Resources",
            "Attire",
            "Tool",
            "Medical",
            "Food",
            "Ammunition",
            "Traps",
            "Misc",
            "Component",
            "Electrical",
            "Fun",
        };

        private void GenerateConfig()
        {
            foreach (string c in Cats)
            {
                _config.StackCategoryMultipliers.TryAdd(c, 0);
                _config.StackCategories.TryAdd(c, new Dictionary<string, _Items>());
            }

            StringBuilder stacksChanged = new StringBuilder();
            foreach (ItemDefinition item in ItemManager.itemList)
            {
                string categoryName = item.category.ToString();
                string shortName = item.shortname;
                if (_exclude.Contains(shortName))
                    continue;

                int id = item.itemid;
                int stack = item.stackable;
                int stored = _defaults[id];
                if (stack != stored)
                    stacksChanged.AppendLine($"{item.itemid}_{item.shortname}_{item.stackable}_{stored}");

                Dictionary<string, _Items> stackCategory = _config.StackCategories[categoryName];
                if (stack <= 0)
                    item.stackable = stored;

                stackCategory.TryAdd(shortName, new _Items
                {
                    ShortName = shortName,
                    ItemId = id,
                    DisplayName = _corrections.ContainsKey(shortName) ? _corrections[shortName] : item.displayName.english,
                    Modified = item.stackable,
                });
            }

            if (stacksChanged.Length > 0)
                PrintError($"Vanilla stacks have changed!?\n{stacksChanged}");

            stacksChanged.Clear();
            SaveConfig();
        }

        // Built because sometimes FacePunch removes or adds game items or changes them thus breaking the config file/ui editor.
        // Also because some people edit the config and accidents happen.. so i have to auto-sort out their config errors.
        private IEnumerator CheckForDiscrepanciesWithGameItems()
        {
            Puts("Checking For Discrepancies With Game Items");
            StringBuilder multiply = new StringBuilder();
            foreach (string c in Cats)
                if (_config.StackCategoryMultipliers[c] < 0)
                {
                    int digit = _config.StackCategoryMultipliers[c];
                    _config.StackCategoryMultipliers[c] = 0;
                    multiply.AppendLine($"Category '{c}' Multiplier was invalid '{digit}' and has been reset.");
                }

            if (multiply.Length > 0)
                PrintWarning($"Categories have been repaired:\n{multiply}");

            Puts("16%");
            yield return CoroutineEx.waitForSeconds(0.30f);
            StringBuilder invalid = new StringBuilder();
            StringBuilder changed = new StringBuilder();
            StringBuilder categorychanged = new StringBuilder();
            StringBuilder removed = new StringBuilder();
            foreach (var cat in _config.StackCategories)
            foreach (var item in cat.Value.ToArray())
            {
                string shortName = item.Key;
                if (_exclude.Contains(shortName))
                {
                    PrintWarning($"Removed a config item that doesn't belong: {shortName}");
                    _config.StackCategories[cat.Key].Remove(shortName);
                    continue;
                }

                int id = item.Value.ItemId;
                string name = item.Value.DisplayName;
                if (item.Value.Modified <= 0)
                {
                    invalid.AppendLine($"{name}_{id}_{shortName} was set as {item.Value.Modified}!?");
                    _config.StackCategories[cat.Key][item.Key].Modified = _defaults[id];
                }

                if (ItemManager.FindItemDefinition(shortName) == null || !ItemManager.itemDictionary.ContainsKey(id) || shortName != ItemManager.itemDictionary[id].shortname)
                {
                    removed.AppendLine($"{name}_{id}_{shortName}");
                    _config.StackCategories[cat.Key].Remove(shortName);
                    continue;
                }
                ItemDefinition update = ItemManager.itemDictionary[id];
                string check = update.displayName.english;
                if (name != check && !_corrections.ContainsKey(shortName))
                {
                    _config.StackCategories[cat.Key][item.Key].DisplayName = check;
                    changed.AppendLine($"{name}_{id}_{shortName} to {check}_{id}_{update.shortname}");
                }

                string category = update.category.ToString();
                if (category != cat.Key)
                {
                    categorychanged.AppendLine($"{cat.Key}_{name}_{id}_{shortName} to {category}_{check}_{id}_{update.shortname}");
                    _config.StackCategories[cat.Key].Remove(shortName);
                }
            }

            if (invalid.Length > 0)
                PrintWarning($"Item's have been fixed!?:\n{invalid}");
            if (changed.Length > 0)
                PrintWarning($"Item's have been changed:\n{changed}");
            if (removed.Length > 0)
                PrintWarning($"Item's have been removed:\n{removed}");
            if (categorychanged.Length > 0)
                PrintWarning($"Item's category have been changed:\n{categorychanged}");

            Puts("66%");
            yield return CoroutineEx.waitForSeconds(0.30f);
            StringBuilder missing = new StringBuilder();
            StringBuilder defaults = new StringBuilder();
            foreach (var item in ItemManager.itemDictionary)
            {
                string category = item.Value.category.ToString();
                string shortname = item.Value.shortname;
                if (_exclude.Contains(shortname))
                    continue;

                if (!_config.StackCategories[category].ContainsKey(shortname))
                {
                    _config.StackCategories[category].TryAdd(shortname, new _Items
                    {
                        ShortName = shortname,
                        ItemId = item.Value.itemid,
                        DisplayName = _corrections.ContainsKey(shortname) ? _corrections[shortname] : item.Value.displayName.english,
                        Modified = item.Value.stackable,
                    });
                    missing.AppendLine($"{item.Value.displayName.english}_{item.Value.itemid}_{shortname}_{item.Value.stackable}");
                }

                int id = item.Key;
                if (!_defaults.ContainsKey(id))
                    defaults.AppendLine($"{item.Value.displayName.english}_{item.Value.itemid}_{shortname}_{item.Value.stackable}");
            }

            if (missing.Length > 0)
                PrintError($"Config Item's are missing!?:\n{missing}");
            if (defaults.Length > 0)
                PrintError($"Default Item's are missing! Notify DEV! Create Support Ticket!:\n{defaults}");
            if (multiply.Length > 0 || invalid.Length > 0 || changed.Length > 0 || removed.Length > 0 || missing.Length > 0 || defaults.Length > 0 || categorychanged.Length > 0)
            {
                SaveConfig();
                multiply.Clear();
                invalid.Clear();
                changed.Clear();
                categorychanged.Clear();
                removed.Clear();
                missing.Clear();
                defaults.Clear();
            }
            yield return CoroutineEx.waitForSeconds(0.30f);
            Puts("100%");
            Updating = null;
            Updating = ServerMgr.Instance.StartCoroutine(ModifyStacks());
        }

        private IEnumerator ModifyStacks()
        {
            Puts("Modifying Stacks");
            yield return CoroutineEx.waitForSeconds(0.30f);
            foreach (ItemDefinition item in ItemManager.itemList)
            {
                string shortName = item.shortname;
                if (_exclude.Contains(shortName))
                    continue;

                string categoryName = item.category.ToString();
                if (_config.EnableEditor)
                    _itemMap.TryAdd(shortName, categoryName);

                int stock = _defaults[item.itemid];
                if (item.stackable <= 0)
                {
                    PrintError($"{item.displayName.english}_{item.itemid}_{item.shortname} has an invalid stack-size '{item.stackable}' and has been restored to default value.");
                    item.stackable = stock;
                }

                int multiplier = _config.StackCategoryMultipliers[categoryName];
                _Items update = _config.StackCategories[categoryName][shortName];
                if (update.Disable)
                {
                    item.stackable = 1;
                    item.maxDraggable = 1;
                }
                else if (multiplier > 0 && update.Modified == stock)
                {
                    int math = stock *= multiplier;
                    item.stackable = math;
                    item.maxDraggable = math;
                }
                else if (update.Modified > 0)
                {
                    item.stackable = update.Modified;
                    item.maxDraggable = update.Modified;
                }
            }

            Puts("All Stack Modifications have completed!");
            Interface.CallHook("OnStackSizeUpdated");
            Updating = null;
            yield return null;
        }

        #endregion

        #region Oxide

        protected override void LoadDefaultConfig() => _config = new PluginConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null)
                {
                    PrintWarning($"No configuration file found, creating config.");
                    LoadDefaultConfig();
                }

                if (MaybeUpdateConfig(_config))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                PrintWarning("Failed to load config file (is the config file corrupt?) (" + ex.Message + ")");
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private Coroutine Updating = null;

        private void Unload()
        {
            if (Rust.Application.isQuitting)
                return;

            if (Updating != null)
                ServerMgr.Instance.StopCoroutine(Updating);

            if (!_config.EnableEditor) return;
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (!permission.UserHasPermission(player.UserIDString, Admin)) continue;
                DestroyUi(player, true);
            }

            if (!_config.IndustrialPatch)
            {
                _harmony.UnpatchAll(_harmony.Id);
                Puts($"Removed All Patches relating to {Name}");
            }
            _itemMap.Clear();
            _editorPageSM.Clear();
            _defaults = null;
        }

        private void OnServerShutdown()
        {
            SaveConfig();

            if (!_config.EnableEditor) return;
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (!player.userID.IsSteamId() || !permission.UserHasPermission(player.UserIDString, Admin)) continue;
                DestroyUi(player, true);
            }
            _defaults = null;
        }

        private void Init()
        {
            Unsubscribe(nameof(OnWeaponModChange));
            Unsubscribe(nameof(CanMoveItem));
            Unsubscribe(nameof(OnItemAddedToContainer));
            Unsubscribe(nameof(OnItemAction));
            Unsubscribe(nameof(OnCardSwipe));
            _defaults = _FP;
            if (_config.StackCategories.IsNullOrEmpty())
                GenerateConfig();
        }

        private void OnServerInitialized()
        {
            ItemManager.Initialize();
            Updating = ServerMgr.Instance.StartCoroutine(CheckForDiscrepanciesWithGameItems());
            permission.RegisterPermission(Admin, this);
            cmd.AddChatCommand(_config.modifycommand, this, CmdModify);
            cmd.AddChatCommand(_config.resetcommand, this, nameof(CmdReset));
            cmd.AddConsoleCommand(_config.resetcommand, this, nameof(ConsoleSmReset));
            cmd.AddChatCommand(_config.colorcommand, this, CmdColor);
            if (!_config.IndustrialPatch)
            {
                _harmony = new Harmony("StackModifier" + "Patch");
                _harmony.PatchAll();
            }

            if (!_config.DisableWearableFix)
                Subscribe(nameof(OnItemAddedToContainer));

            if (!_config.LickFix)
                Subscribe(nameof(OnItemAction));

            if (!_config.SwipeFix)
                Subscribe(nameof(OnCardSwipe));

            if (_config.WeaponModFix)
                return;
            Subscribe(nameof(OnWeaponModChange));
            Subscribe(nameof(CanMoveItem));
        }

        private object CanStackItem(Item item, Item targetItem)
        {
            BasePlayer p = item.GetOwnerPlayer() ?? targetItem.GetOwnerPlayer();
            ItemContainer fix = p?.inventory.containerMain ?? (item.parent ?? targetItem.parent);
            if (fix == null)
                return null;

            ItemDefinition id = item.info;
            ItemDefinition td = targetItem.info;
            if (!item.IsValid() || item.amount >= id.stackable || targetItem.amount >= td.stackable || id.itemid != td.itemid || id.HasComponent<ItemModHead>())
                return false;

            if (targetItem.skin != item.skin ||
                targetItem.name != item.name ||
                (item.hasCondition && ((int)item._maxCondition != (int)targetItem?._maxCondition || (int)item._condition != (int)targetItem?._condition)) ||
                targetItem.text != item.text ||
                item.IsBlueprint() && item.blueprintTarget != targetItem.blueprintTarget ||
                td.shortname != id.shortname ||
                targetItem.instanceData?.dataInt != item.instanceData?.dataInt ||
                (td.HasComponent<ItemModSign>() && targetItem.instanceData?.subEntity.Value != null))
                return false;

            ItemContainer tc = targetItem.contents;
            ItemContainer ic = item.contents;
            if ((ic?.capacity ?? 0) != (tc?.capacity ?? 0))
                return false;

            bool liquid = item.contents?.allowedContents == ItemContainer.ContentsType.Liquid;
            if (liquid)
            {
                if (!CanWaterItemsStack(item, targetItem))
                    return false;
            }
            else
            {
                int tcc = tc?.itemList.Count ?? 0;
                if (tcc > 0)
                {
                    if (!HasVanillaContainer(td))
                        return false;

                    for (int i = tcc - 1; i >= 0; i--)
                    {
                        Item childItem = tc.itemList[i];
                        fix.GiveItem(childItem);
                    }
                }
                int icc = ic?.itemList.Count ?? 0;
                if (icc > 0)
                {
                    if (!HasVanillaContainer(td))
                        return false;

                    for (int i = icc - 1; i >= 0; i--)
                    {
                        Item childItem = ic.itemList[i];
                        fix.GiveItem(childItem);
                    }
                }
            }

            BaseEntity held = targetItem.GetHeldEntity();
            if (held is BaseProjectile projectile)
            {
                if (projectile.primaryMagazine.contents <= 0) return true;
                fix.GiveItem(ItemManager.Create(projectile.primaryMagazine.ammoType, projectile.primaryMagazine.contents));
                projectile.primaryMagazine.contents = 0;
            }
            else if (held is FlameThrower flameThrower)
            {
                if (flameThrower.ammo <= 0) return true;
                fix.GiveItem(ItemManager.Create(flameThrower.fuelType, flameThrower.ammo));
                flameThrower.ammo = 0;
            }
            else if (held is Chainsaw chainsaw)
            {
                if (chainsaw.ammo <= 0) return true;
                fix.GiveItem(ItemManager.Create(chainsaw.fuelType, chainsaw.ammo));
                chainsaw.ammo = 0;
            }

            return true;
        }

        private static bool HasVanillaContainer(ItemDefinition itemDefinition)
        {
            foreach (ItemMod itemMod in itemDefinition.itemMods)
                if (itemMod is ItemModContainer)
                    return true;

            return false;
        }

        private object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            Item i = item.item;
            Item t = targetItem.item;
            if (i.info.itemid != t.info.itemid ||
                i.skin != t.skin ||
                (i.hasCondition && ((int)i?._condition != (int)t.maxCondition || (int)t?._condition != (int)i.maxCondition)) ||
                i.name != t.name ||
                i.text != t.text ||
                i.IsBlueprint() && i.blueprintTarget != t.blueprintTarget ||
                i.info.HasComponent<ItemModHead>() ||
                (i.info.HasComponent<ItemModSign>() && i.instanceData?.subEntity.Value != null))
                return false;

            ItemContainer contentA = i.contents;
            ItemContainer contentB = t.contents;
            if (contentA == null && contentB == null)
                return null;

            if (contentA?.itemList.Count > 0 ||
                contentB?.itemList.Count > 0 ||
                (contentA?.capacity ?? 0) != (contentB?.capacity ?? 0)) 
                return false;

            return null;
        }

        private Item OnItemSplit(Item item, int amount)
        {
            Item i = ItemManager.Create(item.info, amount, item.skin);
            ItemContainer container = item?.contents;
            if (container != null)
            {
                if (i.contents == null)
                {
                    i.contents = new ItemContainer();
                    i.contents.ServerInitialize(i, container.capacity);
                    i.contents.GiveUID();
                }
                else
                    i.contents.capacity = container.capacity;

                if (container.allowedContents == ItemContainer.ContentsType.Liquid && container.itemList.Count > 0)
                {
                    Item liquid = container.itemList[0];
                    i.contents.AddItem(liquid.info, liquid.amount, liquid.skin);
                }
            }

            item.amount -= amount;
            i.name = item.name;
            i.text = item.text;
            i.skin = item.skin;
            if (item.instanceData != null)
                i.instanceData = item.instanceData;
            if (item.IsBlueprint())
                i.blueprintTarget = item.blueprintTarget;
            if (item.hasCondition)
            {
                i._condition = item._condition;
                i._maxCondition = item._maxCondition;
            }

            BaseEntity held = i.GetHeldEntity();
            if (held != null)
                held.skinID = item.skin;

            i.MarkDirty();
            item.MarkDirty();

            if (!_config.VendingMachineAmmoFix && item.GetRootContainer()?.entityOwner is VendingMachine)
                return i;

            if (_config.DisableFix)
                return i;

            if (held is BaseProjectile projectile)
                projectile.primaryMagazine.contents = 0;
            else if (held is FlameThrower thrower)
                thrower.ammo = 0;
            else if (held is Chainsaw chainsaw)
                chainsaw.ammo = 0;

            return i;
        }

        private void OnWeaponModChange(BaseProjectile weapon, BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            Item pew = weapon.GetCachedItem();
            int count = pew.contents?.itemList?.Count ?? 0;
            if (count == 0) return;

            foreach (Item i in pew.contents.itemList)
            {
                if (i == null || i.isBroken || i.amount <= 1) continue;
                int division = i.amount / 1;

                for (int d = 0; d < division; d++)
                {
                    Item x = i.SplitItem(1);
                    player.inventory.GiveItem(x);
                }
            }

        }

        private object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainer, int targetSlot, int amount)
        {
            if (item == null) return null;
            BasePlayer player = item.GetOwnerPlayer();
            if (player == null || !player.userID.IsSteamId()) return null;
            BaseEntity held = item.GetHeldEntity();
            if (held == null || !(held is ProjectileWeaponMod)) return null;

            Item thingy = playerLoot.FindContainer(targetContainer)?.parent;
            BaseEntity heldEntity = thingy?.GetHeldEntity();
            if (heldEntity == null || !(heldEntity is BaseProjectile)) return null;

            int id = item.info.itemid;
            foreach (Item i in thingy.contents.itemList)
                if (i.info.itemid == id)
                    return 0;

            return null;
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            BasePlayer player = container.GetOwnerPlayer();
            if (player == null || !player.userID.IsSteamId() || player.IsAdmin) return;
            if (player.inventory.containerWear.uid != container.uid) return;
            if (item.amount <= 1) return;
            if (Interface.CallHook("OnIgnoreStackSize", player, item) != null) return;

            int amount2 = item.amount -= 1;
            item.amount = 1;
            Interface.Oxide.NextTick(() =>
            {
                Item x = ItemManager.Create(item.info, amount2, item.skin);
                x.name = item.name;
                x.text = item.text;
                BaseEntity held = x.GetHeldEntity();
                if (held != null)
                    held.skinID = item.skin;

                x._condition = item._condition;
                x._maxCondition = item._maxCondition;
                if (item.instanceData != null)
                    x.instanceData = item.instanceData;

                x.MarkDirty();
                if (!x.MoveToContainer(player.inventory.containerMain))
                    x.DropAndTossUpwards(player.transform.position);
            });
        }

        private object OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            Item item = card.GetItem();
            if (item == null || item.isBroken || item.amount <= 1 || cardReader.accessLevel != card.accessLevel) return null;
            Sort(item, player);
            return null;
        }

        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (item == null || item.isBroken || item.amount <= 1) return null;
            if (item.info.itemid == Candy || action == "unwrap" && item.skin != 0)
                Sort(item, player);
            return null;
        }

        #endregion

        /*[ChatCommand("checkme")]
        private void CheckMeCommand(BasePlayer player, string command, string[] args)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var i in player.inventory.AllItems())
            {
                sb.AppendLine($"{i.info.displayName.english}");
                sb.AppendLine($"valid item {i.IsValid()}");
                sb.AppendLine($"amount stack {i.amount < i.info.stackable}");
                sb.AppendLine($"custom name ? {i.name != null} ? {i?.name}");
                sb.AppendLine($"text {i.text}");
                sb.AppendLine($"skinid {i.skin}");
                sb.AppendLine($"is bp? {i.IsBlueprint()}");
                sb.AppendLine($"instance null ? {i.instanceData == null}");
                sb.AppendLine($"dataint ? {i.instanceData?.dataInt}");
                sb.AppendLine($"contents empty? {i.contents?.itemList.Count == 0}");
                sb.AppendLine();
            }

            Puts(sb.ToString());
        }

        [ChatCommand("repair.stacks")]
        private void ConsoleStackRepair(BasePlayer player, string command, string[] args) => Updating = ServerMgr.Instance.StartCoroutine(RepairItems());

        private IEnumerator RepairItems()
        {
            Puts("Searching for broken items");
            int count = 0;
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                BasePlayer player = entity as BasePlayer;
                if (player != null && player.userID.IsSteamId())
                {
                    var items = player.inventory.AllItems();
                    foreach (var i in items)
                    {
                        var check = i.info;
                        if (check.shortname.Contains("cctv") || check.itemid == 1523195708 || check.itemid == -1779183908)
                            continue;
                        if (check.category == ItemCategory.Resources && check.amountType != ItemDefinition.AmountType.Genetics)
                        {
                            if (i.instanceData != null && i.name == null && i.contents == null && i.skin == 0)
                            {
                                i.instanceData = null;
                                count++;
                            }
                        }
                    }
                    //yield return CoroutineEx.waitForEndOfFrame;
                }
                if (entity is DroppedItem)
                {
                    DroppedItem thing = entity as DroppedItem;
                    Item i = thing.GetItem();
                    var check = i.info;
                    if (check.shortname.Contains("cctv") || check.itemid == 1523195708 || check.itemid == -1779183908)
                        continue;
                    if (check.category == ItemCategory.Resources && check.amountType != ItemDefinition.AmountType.Genetics)
                    {
                        if (i.instanceData != null && i.name == null && i.contents == null && i.skin == 0)
                        {
                            i.instanceData = null;
                            count++;
                        }
                    }
                }
                if (entity is StorageContainer)
                {
                    var box = entity as StorageContainer;
                    var items = box.inventory.itemList;
                    foreach (var i in items)
                    {
                        var check = i.info;
                        if (check.shortname.Contains("cctv") || check.itemid == 1523195708 || check.itemid == -1779183908)
                            continue;
                        if (check.category == ItemCategory.Resources && check.amountType != ItemDefinition.AmountType.Genetics)
                        {
                            if (i.instanceData != null && i.name == null && i.contents == null && i.skin == 0)
                            {
                                i.instanceData = null;
                                count++;
                            }
                        }
                    }
                }

            }

            if (count == 0)
            {
                PrintWarning($"No broken items were found for resources. :)");
            }
            else
            {
                PrintWarning($"Repaired {count} broken resource specific items.");
            }
            Updating = null;
            yield return null;
        }*/

        #region Industrial Patch

        [HarmonyPatch(typeof(ItemContainer), nameof(ItemContainer.QuickIndustrialPreCheck))]
        private static class ItemContainer_QuickIndustrialPreCheck
        {
            [HarmonyPrefix]
            static bool Prefix(ItemContainer __instance, Item toTransfer, Vector2i range, out int foundSlot, ref bool __result)
            {
                int containerSize = range.y - range.x + 1;
                int count = __instance.itemList.Count;
                int itemsInRange = 0;
                foundSlot = -1;

                bool liquid = toTransfer.contents?.allowedContents == ItemContainer.ContentsType.Liquid;
                if (toTransfer.contents?.itemList.Count > 0 && !liquid)
                {
                    ItemContainer tc = toTransfer.contents;
                    ItemContainer deport = toTransfer.parent;
                    if (tc?.itemList.Count > 0)
                        for (int i = tc.itemList.Count - 1; i >= 0; i--)
                        {
                            Item childItem = tc.itemList[i];
                            deport.GiveItem(childItem);
                        }
                }

                for (int i = 0; i < count; ++i)
                {
                    Item item = __instance.itemList[i];
                    int position = item.position;
                    if (position < range.x || range.y < position) 
                        continue;

                    ++itemsInRange;
                    if (item.amount >= item.info.stackable || liquid && toTransfer.contents?.itemList.Count > 0)
                        continue;

                    if (item.info.itemid != toTransfer.info.itemid || toTransfer.IsBlueprint() && item.blueprintTarget != toTransfer.blueprintTarget)
                        continue;

                    if (item.skin != toTransfer.skin ||
                        (item.hasCondition && ((int)item?._condition != (int)toTransfer?._condition || (int)item?._maxCondition != (int)toTransfer._maxCondition)) ||
                        item.name != toTransfer.name ||
                        item.text != toTransfer.text ||
                        item.contents?.itemList.Count > 0 ||
                        item.instanceData?.dataInt != toTransfer.instanceData?.dataInt ||
                        item.info.HasComponent<ItemModHead>() ||
                        (item.info.HasComponent<ItemModSign>() && item.instanceData?.subEntity.Value != null) ||
                        (item.contents?.capacity ?? 0) != (toTransfer.contents?.capacity ?? 0))
                        continue;

                    foundSlot = position;
                    __result = true;
                    return false;
                }

                __result =  itemsInRange < containerSize;
                return false;
            }
        }

        #endregion

        #region Helpers


        private static void Sort(Item item, BasePlayer player)
        {
            int amount = item.amount;
            amount -= 1;
            int stackable = item.info.stackable;
            var results = Enumerable.Repeat(stackable, amount / stackable).Concat(Enumerable.Repeat(amount % stackable, 1)).Where(x => x > 0);

            var container = player.inventory.containerMain;
            foreach (int value in results)
            {
                Item x = item.SplitItem(value);
                if (x != null && !x.MoveToContainer(container, -1, false) && (item.parent == null || !x.MoveToContainer(item.parent)))
                    x.Drop(container.dropPosition, container.dropVelocity);
            }
        }

        private bool CanWaterItemsStack(Item item, Item targetItem)
        {
            if (item.GetHeldEntity()?.GetComponentInChildren<BaseLiquidVessel>() == null)
                return true;

            ItemContainer targetContents = targetItem.contents;
            ItemContainer itemContents = item.contents;
            if (targetContents.IsEmpty() || itemContents.IsEmpty())
                return (!targetContents.IsEmpty() || !itemContents.IsFull()) && (!itemContents.IsEmpty() || !targetContents.IsFull());

            Item first = itemContents.itemList.First();
            Item second = targetContents.itemList.First();
            if (first.info.itemid != second.info.itemid || first.amount != second.amount) return false;

            return (!targetContents.IsEmpty() || !itemContents.IsFull()) && (!itemContents.IsEmpty() || !targetContents.IsFull());
        }

        private void ConsoleSmReset(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg?.Player() ?? null;
            CmdReset(player, null, null);
        }

        private void CmdReset(BasePlayer player, string command, string[] args)
        {
            if (player != null && !permission.UserHasPermission(player.UserIDString, Admin))
                return;

            _config.ResetCategory(AllC);
            SaveConfig();

            foreach (ItemDefinition id in ItemManager.itemList)
            {
                int name = id.itemid;
                int value = 1;
                if (_defaults.TryGetValue(name, out value))
                    id.stackable = value;
            }

            string reply = "Server Stack Sizes & Configurations have been reset!";
            if (player == null)
                Puts(reply);
            else
                player.ChatMessage(reply);
        }


        #endregion

        #region Cmds

        [ConsoleCommand("multiply")]
        private void ConsoleMultiply(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player != null) return;
            string input = arg.GetString(0);
            int multiplier = input.ToInt();
            if (string.IsNullOrEmpty(input) || multiplier <= 0)
            {
                Puts("You must input a number to set the multiplier to.\n Ex: multiply 10, Default is 1 for vanilla.");
                return;
            }
            _config.SetCategory(AllC, multiplier);

            foreach (ItemDefinition id in ItemManager.itemList)
            {
                string category = id.category.ToString();
                string name = id.shortname;
                if (_exclude.Contains(name))
                    continue;

                int vanilla = _defaults[id.itemid];
                if (!_config.StackCategories[category].ContainsKey(name)) continue;
                if (_config.StackCategories[category][name].Disable)
                    id.stackable = 1;
                else if (_config.StackCategoryMultipliers[category] > 0 && _config.StackCategories[category][name].Modified == vanilla)
                    id.stackable *= _config.StackCategoryMultipliers[category];
                else if (_config.StackCategories[category][name].Modified != vanilla)
                    id.stackable = _config.StackCategories[category][name].Modified;
                else
                    id.stackable = vanilla;
            }

            SaveConfig();
            Puts($"You have set the stack multiplier to: {multiplier}x");
        }

        #endregion

        #region UI

        private const string StackModifierEditorOverlayName = "StackModifierEditorOverlay";
        private const string StackModifierEditorContentName = "StackModifierEditorContent";
        private const string StackModifierEditorDescOverlay = "StackModifierEditorDescOverlay";

        private CuiElementContainer CreateEditorOverlay()
        {
            return new CuiElementContainer
            {
                new CuiElement
                {
                    DestroyUi = StackModifierEditorOverlayName,
                },
                {
                    new CuiPanel //background transparency
                    {
                        Image =
                        {
                            Color = _config.Colors.Transparency.Rgb
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        },
                        CursorEnabled = true
                    },
                    "Overlay", StackModifierEditorOverlayName
                },
                    new CuiElement //Background image
                    {
                        Parent = StackModifierEditorOverlayName,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Material = "assets/content/ui/uibackgroundblur.mat",
                                Color = "0 0 0 0.3",
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1"
                            }
                        }
                    },
                {
                    new CuiLabel //Welcome Msg
                    {
                        Text =
                        {
                            Text = "Stack Modifier Editor ◝(⁰▿⁰)◜",//GetText(_config.EditorMsg, "label"),
                            FontSize = 30,
                            //Font = "permanentmarker.ttf", // TODO:
                            Color = _config.Colors.TextColor.Rgb,
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.25 0.85",
                            AnchorMax = "0.7 0.95"
                        }
                    },
                    StackModifierEditorOverlayName
                },
                {
                    new CuiLabel // Set Label
                    {
                        Text =
                        {
                            Text = "Set Stacks",//GetText(_config.SetLabel, "label"),
                            FontSize = 20,
                            //Font = "permanentmarker.ttf", // TODO:
                            Color = _config.Colors.TextColor.Rgb,
                            Align = TextAnchor.MiddleLeft
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.57 0.6",
                            AnchorMax = "0.7 0.645"
                        }
                    },
                    StackModifierEditorOverlayName
                },
                {
                    new CuiLabel // Stack Label,
                    {
                        Text =
                        {
                            Text = "Default Stacks",//GetText(_config.StackLabel, "label"),
                            FontSize = 20,
                            //Font = "permanentmarker.ttf", // TODO:
                            Color = _config.Colors.TextColor.Rgb,
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.44 0.6",
                            AnchorMax = "0.55 0.645"
                        }
                    },
                    StackModifierEditorOverlayName
                },
                {
                    new CuiLabel // Search Label,
                    {
                        Text =
                        {
                            Text = "Search",//GetText(_config.SearchLable, "label"),
                            FontSize = 20,
                            //Font = "permanentmarker.ttf", // TODO:
                            Color = _config.Colors.TextColor.Rgb,
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.355 0.6",
                            AnchorMax = "0.4 0.645"
                        }
                    },
                    StackModifierEditorOverlayName
                },
                {
                    new CuiLabel // Arrow Search Label,
                    {
                        Text =
                        {
                            Text = GetText("▶", "label"),
                            FontSize = 20,
                            Color = _config.Colors.TextColor.Rgb,
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform = {AnchorMin = "0.32 0.6", AnchorMax = "0.35 0.65"}
                    },
                    StackModifierEditorOverlayName
                },
                {
                    new CuiPanel
                    {
                        Image = {Color = _config.Colors.InputPanel.Rgb},
                        RectTransform =
                        {
                            AnchorMin = "0.325 0.60",
                            AnchorMax = "0.445 0.645"
                        },
                        CursorEnabled = true
                    },
                    StackModifierEditorOverlayName, "InputNameSearch"
                },
                {
                    new CuiButton //close button
                    {
                        Button =
                        {
                            Command = $"editorsm.close",
                            Color = _config.Colors.ButtonGrey.Rgb
                        },
                        RectTransform = {AnchorMin = "0.444 0.11", AnchorMax = "0.54 0.16"},
                        Text =
                        {
                            Text = "✖", //GetText(_config.CloseButtonlabel, "label"),
                            FontSize = 20,
                            Color = _config.Colors.ButtonGreyText.Rgb,
                            Align = TextAnchor.MiddleCenter
                        }
                    },
                    StackModifierEditorOverlayName, "close"
                }
            };
        }

        private readonly CuiLabel editorDescription = new CuiLabel
        {
            Text =
            {
                Text = "{editorDescription}",
                FontSize = 15,
                Align = TextAnchor.MiddleCenter
            },
            RectTransform =
            {
                AnchorMin = "0.16 0.66", //"0.16 0.66",
                AnchorMax = "0.8 0.71" //"0.8 0.71"
            }
        };

        private CuiElementContainer CreateEditorItemEntry(_Items dataItem, float ymax, float ymin, string catName, string text)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = $"{_defaults[dataItem.ItemId]}",
                        FontSize = 15,
                        Color = _config.Colors.TextColor.Rgb,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = $"{0.4} {ymin}",
                        AnchorMax = $"{0.5} {ymax}"
                    }
                },
                StackModifierEditorContentName);

            container.Add(new CuiButton
                {
                    Text =
                    {
                        Text = dataItem.Disable ? "D" : "E",
                        Color = dataItem.Disable ? _config.Colors.Disable.Rgb : /*_config.Colors.Enable.Rgb*/ ButtonGreen.Rgb,
                        Font = "RobotoCondensed-Bold.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    },
                    Button =
                    {
                        Color = _config.Colors.InputPanel.Rgb,
                        Command = $"{"editorsm.toggle"} {catName} {dataItem.DisplayName.Replace(" ", "_")} {dataItem.Disable}",
                    },
                    RectTransform =
                    {
                        AnchorMin = $"{(0.47) + 1 * 0.03 + 0.001} {ymin}",
                        AnchorMax = $"{(0.498) + 1 * 0.03 - 0.001} {ymax - 0.01}"
                    }
                },
                StackModifierEditorContentName, "ButtonToggle");

            int multiplier = _defaults[dataItem.ItemId] * _config.StackCategoryMultipliers[_itemMap[dataItem.ShortName]];

            int newmsg = dataItem.Disable ? 1 : _config.StackCategoryMultipliers[_itemMap[dataItem.ShortName]] > 0 && dataItem.Modified == _defaults[dataItem.ItemId] ? multiplier : dataItem.Modified != _defaults[dataItem.ItemId] ? dataItem.Modified : _defaults[dataItem.ItemId];

            container.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = dataItem.Disable ? "Disabled = 1" : $"{newmsg}",
                        FontSize = 15,
                        Color = _config.Colors.TextColor.Rgb, //$"0.5 0.5 0.5 0.5",
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = $"{(0.499) + 1 * 0.03 + 0.001} {ymin}",
                        AnchorMax = $"{(0.579) + 1 * 0.03 - 0.001} {ymax - 0.01}"
                    }
                },
                StackModifierEditorContentName);

            container.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = _config.Colors.InputPanel.Rgb
                    },
                    RectTransform =
                    {
                        AnchorMin = $"{(0.499) + 1 * 0.03 + 0.001} {ymin}",
                        AnchorMax = $"{(0.579) + 1 * 0.03 - 0.001} {ymax - 0.01}"
                    },
                    CursorEnabled = true
                },
                StackModifierEditorContentName, "InputName");

            if (!dataItem.Disable)
                container.Add(new CuiElement
                {
                    Parent = "InputName",
                    FadeOut = 1f,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = String.Empty, //$"{dataItem.Modified}",
                            FontSize = 18,
                            Align = TextAnchor.MiddleCenter,
                            Color = _config.Colors.NewInputColor.Rgb,
                            CharsLimit = 40,
                            IsPassword = false,
                            NeedsKeyboard = true,
                            Command = $"{"editorsm.edit"} {catName} {dataItem.DisplayName.Replace(" ", "_")} {text}",
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        },
                    }
                });

            return container;
        }

        private void CreateEditorItemIcon(ref CuiElementContainer container, int itemId, string shortname, string displayName, string userId, float ymax, float ymin)
        {
            float position = _config.DisableImages ? 0.27f : 0.3f;

            var label = new CuiLabel
            {
                Text =
                {
                    Text = LangAPI?.Call<string>("GetItemDisplayName", shortname, displayName, userId) ?? displayName,
                    FontSize = 15,
                    Color = _config.Colors.TextColor.Rgb,
                    Align = TextAnchor.MiddleLeft
                },
                RectTransform =
                {
                    AnchorMin = $"{position} {ymin}",
                    AnchorMax = $"0.4 {ymax}"
                }
            };

            container.Add(label, StackModifierEditorContentName);

            if (_config.DisableImages) return;

            var icons = new CuiImageComponent
            {
                ItemId = itemId,
            };

            container.Add(new CuiElement
            {
                Parent = StackModifierEditorContentName,
                Components =
                {
                    icons,
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0.26 {ymin}",
                        AnchorMax = $"0.29 {ymax}"
                    }
                }
            });
        }

        private void CreateEditorChangePage(ref CuiElementContainer container, string currentcat, int editorpageminus, int editorpageplus)
        {
            container.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"editorsm.show {currentcat} {editorpageminus}",
                        Color = _config.Colors.ButtonGrey.Rgb //"0 0 0 0.40"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.344 0.11",
                        AnchorMax = "0.44 0.16"
                    },
                    Text =
                    {
                        Text = "◀",//GetText(_config.BackButtonText, "label"),
                        Color = _config.Colors.ButtonGreenText.Rgb,
                        FontSize = 30,
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf"
                    }
                },
                StackModifierEditorOverlayName,
                "ButtonBack");

            container.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"editorsm.show {currentcat} {editorpageplus}",
                        Color = _config.Colors.ButtonGrey.Rgb //"0 0 0 0.40"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.544 0.11",
                        AnchorMax = "0.64 0.16"
                    },
                    Text =
                    {
                        Text = "▶",//GetText(_config.ForwardButtonText, "label"),
                        Color = _config.Colors.ButtonGreenText.Rgb,
                        FontSize = 30,
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf"
                    }
                },
                StackModifierEditorOverlayName,
                "ButtonForward");
        }

        private void CreateTab(ref CuiElementContainer container, string currentcat, string cat, int editorpageminus, int rowPos)
        {
            int numberPerRow = 5;

            float padding = 0.01f;
            float margin = (0.314f + padding);

            float width = ((0.334f - (padding * (numberPerRow + 1))) / numberPerRow);
            float height = (width * 0.65f);

            int row = (int) Math.Floor((float) rowPos / numberPerRow);
            int col = (rowPos - (row * numberPerRow));

            container.Add(new CuiButton
            {
                Button =
                {
                    Command = $"editorsm.show {cat} {editorpageminus}",
                    Color = "0.5 0.5 0.5 0.5"
                },
                RectTransform = // 0.050 <width  padding> 0.056
                {
                    AnchorMin = $"{margin + (width * col) + (padding * col)} {(0.85f - padding) - ((row + 1) * height) - (padding * row)}", // 0.11  0.334  // 0.78
                    AnchorMax = $"{margin + (width * (col + 1)) + (padding * col)} {(0.85f - padding) - (row * height) - (padding * row)}" // 0.16 0.384  // 0.82
                },
                Text =
                {
                    Text = $"{cat}", //StackModifierLang(cat, player.UserIDString),
                    Align = TextAnchor.MiddleCenter,
                    Color = currentcat == cat ? ButtonGreen.Rgb /*_config.Colors.Enable.Rgb*/ : _config.Colors.TextColor.Rgb,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12
                }
            }, StackModifierEditorOverlayName, cat);
        }

        /*
        private void CreateAdditionalCmdButtons(ref CuiElementContainer container, string currentcat, string text)
        {
            container.Add(new CuiButton
            {
                Button =
                {
                    Command = $"editorsm.show {currentcat}",
                    Color = "0.5 0.5 0.5 0.5"
                },
                RectTransform =
                {
                    AnchorMin = "",
                    AnchorMax = "",
                },
                Text =
                {
                    Text = text,
                    Align = TextAnchor.MiddleCenter,
                    Color = _config.StackCategoryMultipliers[currentcat] != 0 ? ButtonGreen.Rgb : _config.Colors.TextColor.Rgb,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12
                }
            }, StackModifierEditorOverlayName, cat);
        }
        */

        private void DestroyUi(BasePlayer player, bool full = false)
        {
            CuiHelper.DestroyUi(player, StackModifierEditorContentName);
            CuiHelper.DestroyUi(player, "ButtonForward");
            CuiHelper.DestroyUi(player, "ButtonBack");
            if (!full) return;
            CuiHelper.DestroyUi(player, StackModifierEditorDescOverlay);
            CuiHelper.DestroyUi(player, StackModifierEditorOverlayName);
        }

        private void ShowEditor(BasePlayer player, string catid, int from = 0, bool fullPaint = true, bool refreshMultipler = false, bool filter = false, string input = "")
        {
            _editorPageSM[player.userID] = from;
            Dictionary<string, _Items> item = null;
            if (catid == AllC)
            {
                item = new Dictionary<string, _Items>();
                foreach (var cat in _config.StackCategories.Values)
                {
                    foreach (var i in cat)
                        item.Add(i.Key, i.Value);
                }
            }
            else
                item = _config.StackCategories[catid];

            editorDescription.Text.Color = _config.Colors.DescriptionText.Rgb;
            if (catid == AllC)
                editorDescription.Text.Text = $"Global Multiplier is disabled in All category";
            else if (_config.StackCategoryMultipliers[catid] != 0)
                editorDescription.Text.Text = $"{catid} Multiplier {_config.StackCategoryMultipliers[catid]}x & will re-apply on ( restart or reload )\nUnless the Mod23129ified value does not equal default";
            else
                editorDescription.Text.Text = $"{catid} Global Multiplier is disabled since it has not been modified";

            CuiElementContainer container;

            if (fullPaint)
            {
                container = CreateEditorOverlay();
                CreateTab(ref container, catid, AllC, 0, 0);

                int rowPos = 1;
                foreach (var cat in _config.StackCategories)
                {
                    CreateTab(ref container, catid, cat.Key, 0, rowPos);
                    rowPos++;
                }

                if (!refreshMultipler)
                    container.Add(editorDescription, StackModifierEditorOverlayName, StackModifierEditorDescOverlay);
            }
            else
                container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                DestroyUi = StackModifierEditorContentName,
            });
            container.Add(new CuiElement
            {
                DestroyUi = "Field",
            });
            container.Add(new CuiElement
            {
                DestroyUi = "ButtonForward",
            });
            container.Add(new CuiElement
            {
                DestroyUi = "ButtonBack",
            });
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0.0"},
                RectTransform = {AnchorMin = "0.08 0.2", AnchorMax = "1 0.6"}
            }, StackModifierEditorOverlayName, StackModifierEditorContentName);

            if (refreshMultipler)
            {
                container.Add(new CuiElement
                {
                    DestroyUi = StackModifierEditorDescOverlay,
                });
                //CuiHelper.DestroyUi(player, StackModifierEditorDescOverlay);
                container.Add(editorDescription, StackModifierEditorOverlayName, StackModifierEditorDescOverlay);
            }

            int current = 0;
            List<_Items> items = Facepunch.Pool.Get<List<_Items>>(); // Facepunch.Pool.GetList<_Items>();

            if (filter && !string.IsNullOrEmpty(input))
            {
                input = input.Replace("_", " ");
                items.AddRange(item.Where(s => s.Key.Contains(input, CompareOptions.OrdinalIgnoreCase)).Select(x => x.Value));
                items.Sort((a, b) => a.DisplayName.Length.CompareTo(b.DisplayName.Length));

                /*if (catid == AllC)
                {
                    foreach (var cItem in item.Values)
                    {
                        if (cItem.DisplayName.Contains(input.Replace("_", " "), CompareOptions.OrdinalIgnoreCase))
                            items.Add(cItem);
                    }
                }
                else
                    foreach (var shortname in item.Keys)
                    {
                        if (!_config.StackCategories[catid].ContainsKey(shortname)) continue;

                        _Items cItem = _config.StackCategories[catid][shortname];
                        if (cItem.DisplayName.Contains(input.Replace("_", " "), CompareOptions.OrdinalIgnoreCase))
                            items.Add(cItem);
                    }*/
            }
            else
            {
                if (catid == AllC)
                {
                    foreach (var cItem in item.Values)
                        items.Add(cItem);
                }
                else
                    foreach (string shortname in item.Keys)
                    {
                        if (!_config.StackCategories[catid].ContainsKey(shortname))
                            continue;

                        items.Add(_config.StackCategories[catid][shortname]);
                    }
            }

            input = string.Empty;

            container.Add(new CuiElement
            {
                Parent = "InputNameSearch",
                Name = "Field",
                FadeOut = 1f,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = String.Empty,
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter,
                        Color = _config.Colors.NewInputColor.Rgb, //$"{new Color("#FFE24B", 0.5f).Rgb}", //$"{new Color( "#FFFFFF", 0.05f).Rgb}",
                        CharsLimit = 40,
                        IsPassword = false,
                        NeedsKeyboard = true,
                        Command = $"editorsm.{("search")} {catid} {input.Replace(" ", "_")}",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                }
            });

            foreach (_Items data in items)
            {
                try
                {
                    if (current >= from && current < from + 8)
                    {
                        float pos = 0.85f - 0.125f * (current - from);

                        CreateEditorItemIcon(ref container, data.ItemId, data.ShortName, data.DisplayName, player.UserIDString, pos + 0.125f, pos);

                        container.AddRange(CreateEditorItemEntry(data, pos + 0.125f, pos, catid, ""));
                    }
                }
                catch (System.Exception ex)
                {
                    PrintError($"foreach threw Exception: {data?.DisplayName}, {data?.ItemId}, {data?.ShortName} {data?.Modified}, {data?.Disable}");
                    throw;
                }

                current++;
            }

            Facepunch.Pool.FreeUnmanaged(ref items);

            int minfrom = from <= 8 ? 0 : from - 8;
            int maxfrom = from + 8 >= current ? from : from + 8;

            CreateEditorChangePage(ref container, catid, minfrom, maxfrom);
            CuiHelper.AddUi(player, container);
        }

        private void CmdModify(BasePlayer player, string command, string[] args)
        {
            if (!_config.EnableEditor || !permission.UserHasPermission(player.UserIDString, Admin))
                return;

            if (LangAPI != null && LangAPI.Call<bool>("IsReady") == false)
            {
                player.ChatMessage($"Waiting On {LangAPI.Title} to finish the load order");
                return;
            }

            ShowEditor(player, _config.DefaultCat);
        }

        private void CmdColor(BasePlayer player, string command, string[] args)
        {
            if (!_config.EnableEditor || !permission.UserHasPermission(player.UserIDString, Admin))
                return;

            if (args.Length < 2)
            {
                player.ChatMessage($"Invalid Syntax, please type /{_config.colorcommand} <inputpanel|inputpanelnew|inputtext|text|transparent|description> <color> <alpha|ex, 0.98>");
                return;
            }

            UnityEngine.Color color;
            if (!ColorUtility.TryParseHtmlString(args[1], out color))
            {
                SendReply(player, "Not a valid hex color");
                return;
            }

            float alpha = 1f;
            if (args.Length == 3 && !float.TryParse(args[2], out alpha))
                alpha = 1f;

            switch (args[0].ToLower())
            {
                case "inputpanel":
                    _config.Colors.InputPanel = new Color(args[1], alpha);
                    SendReply(player, $"UI {args[0]} color was updated");
                    break;
                case "inputpanelnew":
                    _config.Colors.NewInputColor = new Color(args[1], alpha);
                    SendReply(player, $"UI {args[0]} color was updated");
                    break;
                case "inputtext":
                    _config.Colors.NewInputColor = new Color(args[1], alpha);
                    SendReply(player, $"UI {args[0]} color was updated");
                    break;
                case "text":
                    _config.Colors.TextColor = new Color(args[1], alpha);
                    SendReply(player, $"UI {args[0]} color was updated");
                    break;
                case "transparent":
                    _config.Colors.Transparency = new Color(args[1], alpha);
                    SendReply(player, $"UI {args[0]} color was updated");
                    break;
                case "description":
                    _config.Colors.DescriptionText = new Color(args[1], alpha);
                    SendReply(player, $"UI {args[0]} color was updated");
                    break;
                default:
                    player.ChatMessage($"Invalid Syntax, please type /{_config.colorcommand} <inputpanel|inputpanelnew|inputtext|text|transparent|description> <color> <alpha|ex, 0.98>");
                    break;
            }

            SaveConfig();
        }

        [ConsoleCommand("editorsm.show")]
        private void ConsoleEditorShow(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(2)) return;
            BasePlayer player = arg.Player();
            string catid = arg.GetString(0);

            if (catid.Equals("close"))
            {
                BasePlayer targetPlayer = arg.GetPlayer(1);
                DestroyUi(targetPlayer, true);
                _editorPageSM.Remove(targetPlayer.userID);
                Interface.CallHook("OnStackSizeUpdated");
                return;
            }

            if (player == null || !permission.UserHasPermission(player.UserIDString, Admin))
                return;

            int value = arg.GetInt(1);
            ShowEditor(player, catid, value, true, true);
        }

        [ConsoleCommand("editorsm.edit")]
        private void ConsoleEditSet(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(3)) return;
            BasePlayer player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, Admin)) return;
            string catName = arg.GetString(0).Replace("_", " ");
            string item = arg.GetString(1).Replace("_", " ");
            int amount = arg.GetInt(2);
            if (amount <= 0 || string.IsNullOrEmpty(amount.ToString())) return;

            if (catName == AllC)
            {
                foreach (var cats in _config.StackCategories.Values)
                {
                    foreach (var i in cats)
                    {
                        if (i.Value.DisplayName != item) continue;
                            i.Value.Modified = amount;
                            break;
                    }
                }
                SaveConfig();
            }
            else
            {
                foreach (var shortname in _config.StackCategories[catName].Keys)
                {
                    if (!_config.StackCategories[catName].ContainsKey(shortname)) continue;
                    _Items stackItem = _config.StackCategories[catName][shortname];
                    if (stackItem.DisplayName != item) continue;
                    stackItem.Modified = amount;
                    break;
                }
                SaveConfig();
            }

            if (catName == AllC)
            {
                foreach (ItemDefinition id in ItemManager.itemList)
                {
                    string categoryName = id.category.ToString();
                    string name = id.shortname;
                    if (!_config.StackCategories[categoryName].ContainsKey(name)) continue;
                    _Items stackItem = _config.StackCategories[categoryName][name];
                    if (stackItem.DisplayName == item)
                    {
                        id.stackable = _config.StackCategories[categoryName][name].Modified;
                        break;
                    }
                }
                SaveConfig();
            }
            else
            {
                foreach (ItemDefinition id in ItemManager.itemList)
                {
                    string categoryName = id.category.ToString();
                    string name = id.shortname;
                    if (!_config.StackCategories[catName].ContainsKey(name)) continue;
                    _Items stackItem = _config.StackCategories[catName][name];
                    if (stackItem.DisplayName == item)
                    {
                        id.stackable = _config.StackCategories[categoryName][name].Modified;
                        break;
                    }
                }
                SaveConfig();
            }

            ShowEditor(player, catName, _editorPageSM[player.userID], false);
        }

        [ConsoleCommand("editorsm.toggle")]
        private void ConsoleToggleSet(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(3)) return;
            BasePlayer player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, Admin)) return;
            string catName = arg.GetString(0).Replace("_", " ");
            string item = arg.GetString(1).Replace("_", " ");
            bool value = arg.GetBool(2);

            if (value == false)
                value = true;
            else
                value = false;

            if (catName == AllC)
            {
                foreach (var cats in _config.StackCategories.Values)
                {
                    foreach (var i in cats)
                    {
                        if (i.Value.DisplayName != item) continue;
                            i.Value.Disable = value;
                            break;
                    }
                }
            }
            else
            {
                foreach (var shortname in _config.StackCategories[catName].Keys)
                {
                    if (!_config.StackCategories[catName].ContainsKey(shortname)) continue;
                    _Items stackItem = _config.StackCategories[catName][shortname];
                    if (stackItem.DisplayName != item) continue;
                    stackItem.Disable = value;
                    break;
                }
            }
            SaveConfig();

            if (catName == AllC)
            {
                foreach (ItemDefinition id in ItemManager.itemList)
                {
                    string categoryName = id.category.ToString();
                    if (!_config.StackCategories[categoryName].ContainsKey(id.shortname)) continue;
                    _Items stackItem = _config.StackCategories[categoryName][id.shortname];
                    if (stackItem.DisplayName == item)
                    {
                        if (_config.StackCategories[categoryName][id.shortname].Disable)
                            id.stackable = 1;
                        else if (_config.StackCategoryMultipliers[categoryName] > 0 && _config.StackCategories[categoryName][id.shortname].Modified == _defaults[id.itemid])
                            id.stackable *= _config.StackCategoryMultipliers[categoryName];
                        else if (_config.StackCategories[categoryName][id.shortname].Modified != _defaults[id.itemid])
                            id.stackable = _config.StackCategories[categoryName][id.shortname].Modified;
                        break;
                    }
                }
            }
            else
            {
                foreach (ItemDefinition id in ItemManager.itemList)
                {
                    string categoryName = id.category.ToString();
                    if (!_config.StackCategories[catName].ContainsKey(id.shortname)) continue;
                    _Items stackItem = _config.StackCategories[catName][id.shortname];
                    if (stackItem.DisplayName == item)
                    {
                        if (_config.StackCategories[categoryName][id.shortname].Disable)
                            id.stackable = 1;
                        else if (_config.StackCategoryMultipliers[categoryName] > 0 && _config.StackCategories[categoryName][id.shortname].Modified == _defaults[id.itemid])
                            id.stackable *= _config.StackCategoryMultipliers[categoryName];
                        else if (_config.StackCategories[categoryName][id.shortname].Modified != _defaults[id.itemid])
                            id.stackable = _config.StackCategories[categoryName][id.shortname].Modified;
                        break;
                    }
                }
            }
            ShowEditor(player, catName, _editorPageSM[player.userID], false);
        }

        [ConsoleCommand("editorsm.search")]
        private void ConsoleEditSearch(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, Admin)) return;
            string catid = arg.GetString(0);
            string input = arg.GetString(1) + arg.GetString(2) + arg.GetString(3) + arg.GetString(4) + arg.GetString(5);
            if (string.IsNullOrEmpty(input)) return;
            bool resetting = false;
            bool filter = true;
            bool refresh = false;

            bool all = catid == AllC;
            int amount = arg.GetInt(2);
            int onoff = arg.GetString(1).Equals("disableon", StringComparison.OrdinalIgnoreCase) ? 2 : arg.GetString(1).Equals("disableoff", StringComparison.OrdinalIgnoreCase) ? 4 : 3;
            int old_new = arg.GetString(1).Equals("set", StringComparison.OrdinalIgnoreCase) ? 0 : arg.GetString(1).Equals("multiply", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
            int commandis = arg.GetString(1).Equals("reset", StringComparison.OrdinalIgnoreCase) ? 0 : old_new != 2 ? 1 : onoff != 3 ? onoff : 3;

            //player.ChatMessage($"command is ? > {commandis}, on/off is ? > {onoff}, oldnew is ? > {old_new}");

            if (commandis != 3)
            {
                resetting = true;
                filter = false;
                refresh = true;
            }

            if (commandis == 0)
            {
                _config.ResetCategory(catid);
                SaveConfig();

                if (all)
                {
                    foreach (ItemDefinition id in ItemManager.itemList)
                        if (_defaults.ContainsKey(id.itemid))
                            id.stackable = _defaults[id.itemid];
                }
                else
                    foreach (ItemDefinition id in ItemManager.itemList)
                    {
                        if (!_config.StackCategories[catid].ContainsKey(id.shortname)) continue;
                        id.stackable = _defaults[id.itemid];
                    }
            }

            if (commandis == 1)
            {
                if (amount < 0) // was <= 0 now prevents setting multiply to 1 & always sets 1 to 0 to avoid reboot bug of fucked stack sizes.
                    return;

                if (old_new == 1)
                    _config.SetCategory(catid, amount);
                else
                    _config.SetItems(catid, amount);

                SaveConfig();

                if (all)
                    foreach (ItemDefinition id in ItemManager.itemList)
                    {
                        string category = id.category.ToString();
                        if (!_config.StackCategories[category].ContainsKey(id.shortname)) continue;
                        if (_config.StackCategories[category][id.shortname].Disable)
                            id.stackable = 1;
                        else if (_config.StackCategoryMultipliers[category] > 0 && _config.StackCategories[category][id.shortname].Modified == _defaults[id.itemid])
                            id.stackable *= _config.StackCategoryMultipliers[category];
                        else if (_config.StackCategories[category][id.shortname].Modified != _defaults[id.itemid])
                            id.stackable = _config.StackCategories[category][id.shortname].Modified;
                        else
                            id.stackable = _defaults[id.itemid];
                    }
                else
                    foreach (ItemDefinition id in ItemManager.itemList)
                    {
                        if (!_config.StackCategories[catid].ContainsKey(id.shortname)) continue;
                        if (_config.StackCategories[catid][id.shortname].Disable)
                            id.stackable = 1;
                        else if (_config.StackCategoryMultipliers[catid] > 0 && _config.StackCategories[catid][id.shortname].Modified == _defaults[id.itemid])
                            id.stackable *= _config.StackCategoryMultipliers[catid];
                        else if (_config.StackCategories[catid][id.shortname].Modified != _defaults[id.itemid])
                            id.stackable = _config.StackCategories[catid][id.shortname].Modified;
                        else
                            id.stackable = _defaults[id.itemid];
                    }
            }

            if (onoff != 3)
            {
                _config.ToggleCats(catid, onoff == 2);
                SaveConfig();

                if (all)
                {
                    foreach (ItemDefinition id in ItemManager.itemList)
                    {
                        string categoryName = id.category.ToString();
                        if (!_defaults.ContainsKey(id.itemid)) continue;
                        if (_config.StackCategories[categoryName][id.shortname].Disable)
                                id.stackable = 1;
                        else if (_config.StackCategoryMultipliers[categoryName] > 0 && _config.StackCategories[categoryName][id.shortname].Modified == _defaults[id.itemid])
                            id.stackable *= _config.StackCategoryMultipliers[categoryName];
                        else if (_config.StackCategories[categoryName][id.shortname].Modified != _defaults[id.itemid])
                            id.stackable = _config.StackCategories[categoryName][id.shortname].Modified;
                    }
                }
                else
                {
                    foreach (ItemDefinition id in ItemManager.itemList)
                    {
                        string categoryName = id.category.ToString();
                        if (categoryName != catid) continue;
                        if (!_config.StackCategories[catid].ContainsKey(id.shortname)) continue;
                        if (_config.StackCategories[categoryName][id.shortname].Disable)
                                id.stackable = 1;
                        else if (_config.StackCategoryMultipliers[categoryName] > 0 && _config.StackCategories[categoryName][id.shortname].Modified == _defaults[id.itemid])
                            id.stackable *= _config.StackCategoryMultipliers[categoryName];
                        else if (_config.StackCategories[categoryName][id.shortname].Modified != _defaults[id.itemid])
                            id.stackable = _config.StackCategories[categoryName][id.shortname].Modified;
                        break;
                    }
                }
            }

            ShowEditor(player, catid, arg.GetInt(1), resetting, refresh, filter, input);
        }

        [ConsoleCommand("editorsm.close")]
        private void ConsoleEditClose(ConsoleSystem.Arg arg)
        {
            SaveConfig();
            BasePlayer player = arg.Player();
            DestroyUi(player, true);
            _editorPageSM.Remove(player.userID);
            Interface.CallHook("OnStackSizeUpdated");
        }

        #endregion

        #region UI Colors

        private string GetText(string text, string type)
        {
            switch (type)
            {
                case "label":
                    return text;
                case "image":
                    return "https://i.imgur.com/fL7N8Zf.png";
            }

            return "";
        }

        public class Color
        {
            [JsonIgnore]
            public int R;
            [JsonIgnore]
            public int G;
            [JsonIgnore]
            public int B;
            [JsonIgnore]
            public float A;
            public string Hex;
            public string Rgb;

            public Color(string hex, float alpha = 1f)
            {
                if (hex.StartsWith("#"))
                    hex = hex.Substring(1);

                if (hex.Length == 6)
                {
                    R = int.Parse(hex.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                    G = int.Parse(hex.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                    B = int.Parse(hex.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                }

                A = alpha;
                Hex = "#" + hex;
                Rgb = $"{(double)R / 255} {(double)G / 255} {(double)B / 255} {A}";
            }
        }

        #endregion
    }
}