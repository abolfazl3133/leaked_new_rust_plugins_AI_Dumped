
using Oxide.Core;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using System;
using System.Linq;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;
using Rust;
using Rust.Ai;
using Network;
using Facepunch;
using System.Runtime.InteropServices;

namespace Oxide.Plugins
{
    /*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("Payback", "https://discord.gg/dNGbxafuJn", "2.0.14")]
    [Description("Special Admin Commands To Mess With Cheaters")]
    class Payback : RustPlugin
    {
        [PluginReference] Plugin Payback2;//| reference to the original payback2
        [PluginReference] private Plugin ImageLibrary;//| Optional reference to ImageLibrary

        //| TUTORIAL:
        //| admins require the permission payback.admin to use this plugin
        //| use /payback in game to see the tutorial


        //| =============================================
        //| CHANGELOG
        //| =============================================


        //| 2.0.13
        //| - fixed rocketman for May 2 2024 update


        //| 2.0.13
        //| - fixed rocketman sfx
        //| - reduced damage on chicken
        //| - fixed gestures



        //| 2.0.12
        //| - Fixed HigherGround

        //| 2.0.11
        //| - Fixed OnEntityTakeDamage NRE


        //| 2.0.10
        //| - Fixed OnBanned NRE
        //| - Landmines can no longer be picked up

        //| 2.0.9
        //| - Fixed missing image links

        //| 2.0.8
        //| - Fixed console and chat binds when targeting a player with raycast

        //| 2.0.7
        //| - Updated missing image urls

        //| 2.0.6
        //| - Sit command now uses a toilet chair by default! Includes options for using other chairs
        //| - Added config to disable showing UI to admins while commands are active
        //| - Updated License : you are allowed to use this plugin on multiple servers as long as you are the owner of the servers without purchasing additional copies.  Please consider tipping!

        //| 2.0.5
        //| - Restored rocketman to its former glory with the return of the invisible chair!

        //| 2.0.4
        //| - Added a temporary fix for rocketman and other broken commands, next month Facepunch will update the game and it will work just like before!
        //| - Added ability to for commands to persist between server restarts

        //| 2.0.3
        //| - Jan 2023 patch basemounteable

        //| 2.0.2
        //| - Prevent [MASOCHIST] targets from killing themselves with fall damage

        //| 2.0.1
        //| - General improvements

        //| 2.0.0
        //| - Added ability to log admins using payback to discord
        //| - Added UI - Shows which commands are currently active
        //| - Improved support for conflicting commands

        //| 1.9.8
        //| - Updated for Jan 28 new year content
        //| - Added auto-ban functionality for temp-bans.  




        //| =========

        //| ===================

        //| ==============================================================================
        //| TOMMYGUN'S EULA - BY USING THIS PLUGIN YOU AGREE TO THE FOLLOWING!
        //| ==============================================================================
        //| 
        //| Code contained in this file is not licensed to be copied, shared, resold, or modified in any way.
        //| You may copy the plugin freely to each server instance that your organization owns.  I ask that you include an optional tip if you are a large server organization, the suggested amount is one copy for every 10 servers you are running. 
        //| Do not share this plugin with other server organizations, they must purchase their own licenses.
        //|
        //| =======================================

        //| ===================

        //| =========

        //| TOMMYGUN
        //MMMMMMMMMMMMMMMMMMMMMMMMWNO::loll::kKKXXXXXXXXXXKXXKx;o000000KKKKNWWWWWWWWWWWWWWWWWWMMMMMMWWXxkNNNNN
        //MMMMMMMMMMMMMMMMMMMMMMMMKc,.......................... ...........'',,,,,,,,,,,,,,,,,;;;;;;;;:..,'''.
        //MMMMMMMMMMMMMMMMMMMMMMMMXc',,;;;;;;;;;;;;;;;;;;;,.         ',''',;:::::::::::::clldxxxxxxxxxxooocccc
        //MMMMMMMMMMMMMMNXK00KXKOdc;'',;:;,,;lxddl::,;::;'.',..''   .kWNNNK:...',,........'oXMMMMMMMMMMMMMMMMM
        //MMMMWNX0Oxol:;'.......       :Oc  .xMMMKc....','.;ool,..  .OMMMMNxcdO0KKo.      lNMMMMMMMMMMMMMMMMMM
        //Odlc;'..                     .:,';xNMMWk.     ;xoxXXx'..   cXMMMMMMMMMWO:     .;OMMMMMMMMMMMMMMMMMMM
        //.                     ';:clodxO0XWMMMXo.    .:x000KK0Ol.   .xMMMMMMMW0:.    .:0WWMMMMMMMMMMMMMMMMMMM
        //.                 .,lONMMMMMMMMMMMMMX:     ,0WMMMMMMMMx.cXMMMMMMMK,     'kNWMMMMMMMMMMMMMMMMMMMMMMMM
        //'              .;o0NMMMMMMMMMMMMMMMMk.    .oWMMMMMMMMMx.  .OMMMMMMMMNl    ,OWMMMMMMMMMMMMMMMMMMMMMMM
        //'           .:xKWMMMMMMMMMMMMMMMMMMMNd.  .oNMMMMMMMMMMx.  .OMMMMMMMMMNkl::kWMMMMMMMMMMMMMMMMMMMMMMMM
        //.       .,lkXMMMMMMMMMMMMMMMMMMMMMMMMMXOdxXMMMMMMMMMMMx.  .OMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM
        //.   .,lkKWMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMXOkkONMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM
        //: .l0NMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM



        //| ==============================================================
        //| Definitions
        //| ==============================================================
        public enum Card
        {
            Pacifism = 0,//zeros all outgoing damage from target player
            Butterfingers = 1,//on dealing damage to any player, chance to drop current held weapon.
            InstantKarma = 4,//reflects damage back to the player
            Dud = 5,//prevents damage to non-player entities
            DogDoo = 6,//landmine under target player when the access stashes
            BSOD = 7,//give target player fake BSOD
            Sit = 8,//force player to sit
            Naked = 9,//force player to drop everything they have in their inventory
            Camomo = 10,//apply a combination of abilities Camomo has selected
            HigherGround = 11,//target player is teleported 100m into the air
            Thirsty = 12,//target player becomes thirsty very quickly
            DrNo = 13, // target player no longer receives health from healing items
            Dana = 14, // steal target player's inventory and place it in your own
            Pinyata = 15, // target player's inventory explodes out of them when they die
            Rocketman, // strap target player to a rocket and launch them!
            NoRest, // No Rest For The Wicked! Force the player to respawn!
            ChickenChaser, // Spawns a horde of chickens with super speed that only attack target player!
            ViewLoot, // View target entities loot
            Burn, // Gives a player a flamethrower that will make his foes scream and burn
            Hammer, // hammer - gives target player a hammer that will destroy all the entities owned by the hammer's target
            Bag, // Bag - Print all players that have bagged target player in, and print all players that have been bagged.  Include "discord" after the command to log the results to discord
            Shocker, // Shocker - Shock target player to death.  Affects nearby players so be careful.  Make sure to disable it after use.
                     //Cowboy, // Cowboy - Ride target player like a wild hog -> couldn't get this to work
            Masochist, // Masochist - Stop player from F1 killing themselves
            Emote, // Emote - options: 
            Shark, // Jaws - shark comes to eat the cheater
        }

        Dictionary<Card, string> descriptions = new Dictionary<Card, string>() {
            {Card.Butterfingers, "% chance for target player to drop their weapon when damaging an enemy" },
            {Card.Dud, "target player deals no damage to NON-PLAYER entities.  Also prevents farming / tool use" },
            {Card.InstantKarma, "target player deals no damage to enemies and 35% of the damage is reflected back to them" },
            {Card.Pacifism, "target player deals no player damage to non-teammates; add 'silent' to not send a message about it to other players." },
            {Card.DogDoo, "landmine under target player when the access stashes" },
            {Card.BSOD, "target player receives a fake blue-screen-of-death" },
            {Card.Sit, "spawns a chair in front of you and forces the cheater to sit.  Doesn't let them get up and will place them back in if they die. add 'beach' or 'gamer' to the end of the command to change the chair prefab!" },
            {Card.Naked, "force player to drop everything they have in their inventory" },
            {Card.Camomo, "apply a combination of abilities Camomo has selected [pf,bf,in,dog,dud,dr]" },
            {Card.HigherGround, "target player is teleported 100m into the air" },
            {Card.Thirsty, "target player becomes thirsty very quickly" },
            {Card.DrNo, "target player can no longer heal" },
            {Card.Dana, "steal target player's inventory and place it in your own" },
            {Card.Pinyata, "target player's inventory explodes out of them when they die" },
            {Card.Rocketman, "strap target player to a rocket and launch them!" },
            {Card.NoRest, "No Rest For The Wicked! Force the player to respawn when they die!" },
            {Card.ChickenChaser, "Spawns a horde of chickens with super speed that only attack target player! add 'wolf' 'stag' 'bear' or 'boar' after the command to change the animal" },
            {Card.ViewLoot, "View target player's loot" },
            {Card.Burn, "Gives a player a flamethrower that will make his foes scream and burn" },
            {Card.Hammer, "Gives admin a hammer that will destroy all the entities owned by the hammer's target.  Add -noloot to also delete the loot" },
            {Card.Bag, "Print all players that have bagged target player in, and print all players that have been bagged.  Include \"discord\" after the command to log the results to discord" },
            {Card.Shocker, "Shock target player to death.  Affects nearby players so be careful.  Make sure to disable it after use." },
			//{Card.Cowboy, "Cowboy - Ride target player like a wild hog" },
			{Card.Masochist, "Masochist - Stop player from F1 killing themselves" },
            {Card.Emote, "Emote - force player to do an emote. "},
            {Card.Shark, "shark comes to eat the cheater"},
        };

        Dictionary<string, Card> cardAliases = new Dictionary<string, Card>() {
            { "bf", Card.Butterfingers },
            { "dud", Card.Dud },
            { "in", Card.InstantKarma},
            { "pf", Card.Pacifism},
            { "dog", Card.DogDoo},
            { "bs", Card.BSOD},
            { "nk", Card.Naked},
            { "cumnum", Card.Camomo},
            { "hg", Card.HigherGround},
            { "th", Card.Thirsty},
            { "dr", Card.DrNo},
            { "dana", Card.Dana},
            { "steal", Card.Dana},
            { "pin", Card.Pinyata},
            { "rm", Card.Rocketman},
            { "nr", Card.NoRest},
            { "res", Card.NoRest},
            { "ch", Card.ChickenChaser},
            { "loot", Card.ViewLoot},
            { "bu", Card.Burn},
            { "ham", Card.Hammer},
            { "bg", Card.Bag},
            { "sh", Card.Shocker},
			//{ "cow", Card.Cowboy},
			{ "ms", Card.Masochist},
            { "em", Card.Emote},
            { "jaws", Card.Shark},
        };



        HashSet<Card> cardsWhichCauseConflicts = new HashSet<Card>() {
            Card.Sit,
            Card.Rocketman,
            Card.Shark,
        };

        HashSet<Card> cardsWhichCanPersist = new HashSet<Card>() {
            Card.Butterfingers,
            Card.Dud,
            Card.InstantKarma,
            Card.Pacifism,
            Card.DogDoo,
            Card.BSOD,
            Card.Thirsty,
            Card.DrNo,
            Card.NoRest,
            Card.ChickenChaser,
            Card.Masochist,            
        };

        //| ==============================================================
        //| Giving
        //| ==============================================================
        public void GiveCard(ulong userID, Card card, string[] args = null, BasePlayer admin = null)
        {
            BasePlayer player = BasePlayer.FindByID(userID);

            if (cardsWhichCauseConflicts.Contains(card)) {
                ResolveConflictingCommands(player, admin, (int)card);
            }

            HashSet<Card> cards;
            if (!cardMap.TryGetValue(userID, out cards))
            {
                cards = new HashSet<Card>();
                cardMap[userID] = cards;
            }
            cards.Add(card);
            
            //Puts($"Payback card {card} given to {userID}");

            if (player != null)
            {


                if (card == Card.BSOD)
                {
                    bool playPublic = args.Contains("public");

                    DoBSOD(player, playPublic);
                }
                else if (card == Card.Sit)
                {
                    DoSitCommand(player, admin, args);
                }
                else if (card == Card.Naked)
                {
                    DoNakedCommand(player);
                }
                else if (card == Card.Camomo)
                {
                    DoCamomoCommand(player);
                }
                else if (card == Card.HigherGround)
                {
                    DoHigherGround(player);
                }
                else if (card == Card.Thirsty)
                {
                    DoThirsty(player);
                }
                else if (card == Card.Dana)
                {
                    DoDana(player, admin);
                }
                else if (card == Card.Pacifism)
                {
                    silentPacifism = false;
                    if (args != null)
                    {
                        if (args.Contains("silent"))
                        {
                            silentPacifism = true;
                        }
                    }
                }
                else if (card == Card.Rocketman)
                {
                    RocketManTarget(player);
                }
                else if (card == Card.NoRest)
                {
                    if (player.IsDead())
                    {
                        player.Respawn();
                    }
                }
                else if (card == Card.ChickenChaser)
                {
                    AdminSpawnChickens(admin, player, args);
                    TakeCard(player, Card.ChickenChaser);
                }
                else if (card == Card.ViewLoot)
                {
                    ViewTargetPlayerInventory(player, admin);
                    TakeCard(player, Card.ViewLoot);
                }
                else if (card == Card.Burn)
                {
                    GivePlayerFlamethrower(player);
                }
                else if (card == Card.Hammer)
                {
                    GiveAdminHammer(player);
                    if (args.Contains("noloot"))
                    {
                        flag_kill_no_loot = true;
                        PrintToPlayer(admin, $"Hammer set to remove loot!");
                    }
                    else
                    {
                        flag_kill_no_loot = false;
                    }
                }
                else if (card == Card.Shocker)
                {
                    DoShocker(player, args, admin);
                }
                else if (card == Card.Emote)
                {
                    DoEmote(player, args, admin);
                }
                else if (card == Card.Shark)
                {
                    DoShark(player, args, admin);
                }

                if (config.logPaybackCommands)
                {
                    SendToDiscordWebhook(new Dictionary<string, string>() {
                        { "Admin", $"{admin?.displayName} | {admin?.userID}" },
                        { $"{card.ToString()}", $"{player.displayName} | {player?.userID}" },
                    }, $"{ConVar.Server.hostname}");
                }

                //| if we are saving commands and this command can be saved, do the saving
                if (config.persistentCommands && cardsWhichCanPersist.Contains(card))
                {
                    
                    HashSet<Card> persistentCards = null;
                    if (!paybackData.persistentCommandMap.TryGetValue(player.userID, out persistentCards))
                    {
                        persistentCards = new HashSet<Card>();
                        paybackData.persistentCommandMap[player.userID] = persistentCards;
                    }
                    persistentCards.Add(card);
                    
                }
            }


        }

        bool silentPacifism = false;



        #region VISUALIZATION

        string url_payback_logo = "http://na.fragmod.com/fragimages/dec2023/payback_logo_broad.png";
        string url_payback_logo_raised = "http://na.fragmod.com/fragimages/dec2023/payback_logo_broad_down.png";


        List<BasePlayer> admins = new List<BasePlayer>();
        float ts_admin = 0;

        //| ===================================================
        HashSet<ulong> adminsNoUI = new HashSet<ulong>();
        [ConsoleCommand("paybacknoui")]
        void Console_Payback_NoUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player != null)
            {
                if (!IsAdmin(player)) return;
                if (!adminsNoUI.Contains(player.userID))
                {
                    adminsNoUI.Add(player.userID);
                    PrintToPlayer(player, $"paybackui disabled : {adminsNoUI.Count}");
                    UpdateAdminUI(player);
                }
                else
                {
                    adminsNoUI.Remove(player.userID);
                    PrintToPlayer(player, $"paybackui enabled : {adminsNoUI.Count}");
                    UpdateAdminUI(player);
                }
            }
        }
        //| ===================================================
        
        void CacheAdmins()
        {
            if (Time.realtimeSinceStartup - ts_admin > 3)
            {
                ts_admin = Time.realtimeSinceStartup;
                admins.Clear();

                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (IsAdmin(player))
                    {
                        admins.Add(player);
                    }
                }
            }
        }

        int currentActiveCardCount = 0;


        IEnumerator AdminVisualizationCo()
        {
            while (config.showUI)
            {
                var allCards = GetActiveCards();

                currentActiveCardCount = allCards.Count;
                if (Payback2 != null)
                {
                    var otherCards = Payback2.Call("GetActiveCardsStrings") as List<string>;
                    currentActiveCardCount += otherCards.Count;
                }

                if (cachedCards != currentActiveCardCount)
                {
                    animState = 1;
                    animationFlag = true;
                } else
                {
                    if (animState != 0)
                    {
                        animationFlag = true;
                    }
                    animState = 0;
                }
                foreach (var admin in admins)
                {
                    if (admin == null) continue;
                    UpdateAdminUI(admin);
                }
                animationFlag = false;
                cachedCards = currentActiveCardCount;

                CacheAdmins();

                yield return new WaitForSeconds(0.3f);
            }
        }

        HashSet<Card> GetActiveCards()
        {
            HashSet<Card> allCards = new HashSet<Card>();

            foreach (var userid in cardMap.Keys.ToArray())
            {
                var target = BasePlayer.FindByID(userid);
                if (target == null) continue;
                var cards = cardMap[userid];
                if (cards == null || cards.Count == 0) continue;
                allCards.UnionWith(cards);
            }
            return allCards;
        }

        //| Bridge to other Payback
        List<string> GetActiveCardsStrings()
        {
            List<string> cards = new List<string>();
            foreach (var card in GetActiveCards())
            {
                cards.Add(card.ToString());
            }
            return cards;
        }


        int cachedCards = 0;
        int animState = 0;
        bool animationFlag = false;

        HashSet<ulong> activePaybackUIS = new HashSet<ulong>();
        void UpdateAdminUI(BasePlayer player)
        {

            //| disable showing the UI
            if (!config.showUI)
            {
                return;
            }

            
            //| ===================================
            //| BASE UI SETUP
            //| ===================================
            if (player.net.connection == null) return;

            string guid = "guid_admin";
            UI2.guids.Add(guid);

            var elements = new CuiElementContainer();


            if (currentActiveCardCount == 0 || (adminsNoUI.Contains(player.userID)))
            {
                CuiHelper.DestroyUi(player, guid);
                activePaybackUIS.Remove(player.userID);
                return;
            }

            if (animState == 1)
            {
                CuiHelper.DestroyUi(player, "payback_logo");
            }
            if (animState == 0)
            {
                CuiHelper.DestroyUi(player, "payback_logo_raised");
            }

            //| refresh the commands
            CuiHelper.DestroyUi(player, "commands");


            //| ===================================
            //| Bounds definitions
            //| ===================================
            float width = 0.4f;
            //float height = 0.2f;
            float logoWidth = 0.125f;

            Vector4 mainBounds = new Vector4(0.5f - width / 2f, 0.5f, 0.5f + width / 2f, 0.9f);
            Vector4 logoBounds = new Vector4(0.5f - logoWidth / 2f, 0.90f, 0.5f + logoWidth / 2f, 1f);

            string output = "";
            var allCards = GetActiveCards();
            foreach (var card in allCards)
            {
                output += $"{UI2.ColorText("•", "#B00101")} {card.ToString()}" + "\n";
            }

            if (Payback2 != null)
            {
                var otherCards = Payback2.Call("GetActiveCardsStrings") as List<string>;
                foreach (var card in otherCards)
                {
                    output += $"{UI2.ColorText("•", "#B00101")} {card.ToString()}" + "\n";
                }
            }


            bool updateLogo = false;
            if (!activePaybackUIS.Contains(player.userID))
            {
                UI2.CreatePanel(elements, "Under", guid, "1 1 1 0", UI2.vectorFullscreen, null, false);
                activePaybackUIS.Add(player.userID);
                updateLogo = true;
            }
            if (animationFlag)
            {
                updateLogo = true;
            }
            if (updateLogo)
            {
                if (ImageLibrary != null)
                {
                    if (animState == 0)
                    {
                        UI2.CreatePanel(elements, guid, "payback_logo", "1 1 1 1", logoBounds, GetImage(url_payback_logo), false, 0.0f, 0.0f, true);
                    }
                    else if (animState == 1)
                    {
                        UI2.CreatePanel(elements, guid, "payback_logo_raised", "1 1 1 1", logoBounds, GetImage(url_payback_logo_raised), false, 0.0f, 0.0f, true);
                    }
                }
                else
                {
                    if (animState == 0)
                    {
                        UI2.CreatePanel(elements, guid, "payback_logo", "1 1 1 1", logoBounds, GetImage(url_payback_logo), false, 0.0f, 0.0f, false);
                    }
                    else if (animState == 1)
                    {
                        UI2.CreatePanel(elements, guid, "payback_logo_raised", "1 1 1 1", logoBounds, GetImage(url_payback_logo_raised), false, 0.0f, 0.0f, false);
                    }
                }
            }
            

            UI2.CreateOutlineLabel(elements, guid, "commands", output, "1 1 1 1", 28, mainBounds, TextAnchor.UpperCenter);

            //| ===================================
            //| CONDITIONAL UI UPDATES
            //| ===================================

            //send the ui updates
            if (elements.Count > 0)
            {
                CuiHelper.AddUi(player, elements);
            }
        }

        #endregion


        //| ==============================================================
        //| COMMAND Implementation
        //| ==============================================================

        void DoShark(BasePlayer player, string[] args, BasePlayer admin = null)
        {
            Worker.StaticStartCoroutine(SharkCo2(player, args, admin));
        }
        IEnumerator SharkCo2(BasePlayer player, string[] args, BasePlayer admin = null)
        {

            TakeCard(player, Card.Shark);

            ////| KILL ALL EXISTING SHARKS FOR DEBUGGING
            //SimpleShark[] sharks = GameObject.FindObjectsOfType<SimpleShark>();
            //foreach (var s in sharks)
            //{
            //    s.Kill();
            //}

            //| SPAWN THE SHARK
            SimpleShark shark;
            string sharkPrefab = "assets/rust.ai/agents/fish/simpleshark.prefab";

            BaseEntity entity = GameManager.server.CreateEntity(sharkPrefab, player.transform.position + new Vector3(-100, -100, -100));

            shark = entity as SimpleShark;
            entity.Spawn();

            shark.enabled = false;

            Vector3 playerForward = player.eyes.HeadForward();
            playerForward.y = 0;
            playerForward.Normalize();

            shark.transform.LookAt(player.transform.position + Vector3.up * 100 + playerForward);

            yield return Worker.StaticStartCoroutine(MoveSharkCo(shark, player));



        }
        string sfx_bloodHit = "assets/bundled/prefabs/fx/impacts/stab/flesh/fleshbloodimpact.prefab";
        string sfx_watersplash = "assets/bundled/prefabs/fx/explosions/water_bomb.prefab";
        IEnumerator MoveSharkCo(SimpleShark shark, BasePlayer player)
        {

            float duration = 4f;
            //float duration = 20f;
            float ts = Time.realtimeSinceStartup;
            Vector3 sharkStartPos = player.transform.position + Vector3.down * 30f;
            Vector3 playerStartPosition = player.transform.position;

            Vector3 playerForward = player.eyes.HeadForward();
            playerForward.y = 0;
            playerForward.Normalize();

            bool didSit = false;
            BaseEntity chair = null;

            //bool didEmote = false;

            float p;
            float y;
            while (Time.realtimeSinceStartup - ts < duration && player != null && shark != null)
            {
                p = (Time.realtimeSinceStartup - ts) / duration;
                y = Mathf.Sin(p * Mathf.PI);

                //if (p < 0.34)
                if (p < 0.24)
                {
                    //playerStartPosition = player.transform.position + Vector3.up * 4f;//|V2
                    playerStartPosition = player.transform.position + Vector3.up * 10f;
                    shark.transform.LookAt(player.transform.position + Vector3.up * 100 + playerForward);

                }
                else
                {
                    if (!didSit)
                    {
                        //| PLAY SFX
                        //PlaySound(sfx_bloodHit, player, false, player.transform.position + Vector3.up);
                        //PlaySound(sfx_watersplash, player, false, player.transform.position);
                        global::Effect.server.Run(shark.bloodCloud.resourcePath, player.transform.position + Vector3.up, Vector3.forward, null, false);
                        global::Effect.server.Run(sfx_watersplash, player.transform.position + Vector3.up, Vector3.forward, null, false);

                        timer.Once(0.2f, () =>
                        {
                            if (player == null) return;
                            PlayGesture(player, "friendly");

                        });

                        //| Dismount the player if mounted
                        if (HasCard(player.userID, Card.Sit))
                        {
                            TakeCard(player.userID, Card.Sit);
                        }

                        Vector3 playerFacing = player.eyes.HeadForward();
                        playerFacing.y = 0;
                        playerFacing.Normalize();

                        //| Sit the player
                        chair = InvisibleSit(player);

                        chair.SetParent(shark, true, true);

                        //| V2

                        chair.transform.LookAt(chair.transform.position + playerFacing);

                        didSit = true;
                    }


                    if (chair != null)
                    {
                        //chair.transform.position = shark.transform.position + shark.transform.forward * 0.88f + shark.transform.up * 0.15f;
                        chair.transform.position = shark.transform.position + shark.transform.forward * 0.88f + shark.transform.up * 0.19f;

                        //chair.transform.LookAt(shark.transform.up * -1 + shark.transform.position + Vector3.up * 0.25f);
                        chair.transform.LookAt(shark.transform.position + shark.transform.up * -1 + Vector3.up * 0.15f);
                    }


                }

                shark.transform.position = Vector3.Lerp(sharkStartPos, playerStartPosition, y);
                shark.transform.LookAt(player.transform.position + Vector3.up * 100 + playerForward);

                shark.transform.Rotate(shark.transform.right, -15f);

                yield return new WaitForFixedUpdate();
            }

            if (shark != null)
            {
                shark.transform.position += Vector3.down * 100000;
                shark.SendNetworkUpdate();
            }


            yield return null;

            player?.GetMounted()?.DismountPlayer(player, true);
            chair?.Kill();
            player?.Die();
            shark?.Kill();
        }

        void DoEmote(BasePlayer player, string[] args, BasePlayer admin = null)
        {
            if (player == null) return;

            string gesture = "wave";

            if (args.Length > 1)
            {
                gesture = args[1];

                var g = GestureCollection.Instance.StringToGesture(gesture);
                if (g == null)
                {

                    string output = "\n";
                    foreach (var gg in GestureCollection.Instance.AllGestures)
                    {
                        output += gg.convarName + "\n";
                    }

                    PrintToPlayer(admin, $"Gesture not found: {gesture}\nAvailable Gestures:{output}");
                }

            }
            else
            {
                PrintToPlayer(admin, $"to see all emotes use: /emote <target> list");
            }

            PlayGesture(player, gesture);
            TakeCard(player, Card.Emote);
        }

        bool? CanUseGesture(BasePlayer player, GestureConfig gesture)
        {
            if (HasAnyCard(player.userID)) return true;
            return null;
        }


        void ResolveConflictingCommands(BasePlayer player, BasePlayer admin = null, int ignoreCard = (int)Card.NoRest, bool fromExternalPlugin = false)
        {
            //Puts($"Payback | ResolveConflictingCommands | HasAnyCard : {HasAnyCard(player.userID)} | Payback2 : {Payback2 != null} | IgnoreCard: {(int)ignoreCard}");

            if (HasCard(player.userID, Card.Sit) && ignoreCard != (int)Card.Sit)
            {
                TakeCard(player, Card.Sit);
            }
            if (HasCard(player.userID, Card.Rocketman) && ignoreCard != (int)Card.Rocketman)
            {
                TakeCard(player, Card.Rocketman);
            }
            if (HasCard(player.userID, Card.Shark) && ignoreCard != (int)Card.Shark)
            {
                TakeCard(player, Card.Shark);
            }

            if (Payback2 != null && !fromExternalPlugin)
            {
                Payback2.CallHook("ResolveConflictingCommands", player, admin, ignoreCard, true);
            }
            
            if (player.isMounted)
            {
                var car = player.GetMountedVehicle();
                if (car != null)
                {
                    car.Kill(BaseNetworkable.DestroyMode.Gib);
                }
                var mount = player.GetMounted();
                if (mount != null)
                {
                    mount.DismountPlayer(player, true);
                }
            }

        }

        //object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        //{
        //    if (cowboynetworkables.ContainsKey(target.userID) && cowboynetworkables[target.userID].Contains(entity))
        //    {
        //        PrintToChat($"CanNetworkTo: {entity.ShortPrefabName} {target.displayName}");
        //        return true;
        //    }
        //    return null;
        //}

        //Dictionary<ulong, List<BaseNetworkable>> cowboynetworkables = new Dictionary<ulong, List<BaseNetworkable>>();
        //void DoCowboy(BasePlayer player, string[] args, BasePlayer admin = null) {

        //    //disabled for testing
        //    if (player == null) return;

        //    if (HasCard(admin.userID, Card.Cowboy))
        //    {
        //        List<BaseNetworkable> existing;
        //        if (cowboynetworkables.TryGetValue(admin.userID, out existing))
        //        {
        //            if (existing != null)
        //            {
        //                existing.ForEach(x => x.Kill());
        //                existing.Clear();
        //            }
        //            cowboynetworkables.Remove(admin.userID);
        //        }
        //    }

        //    //down target player
        //    //disabled for testing
        //    player.BecomeWounded();
        //    player.ProlongWounding(100000000000);

        //    ResolveConflictingCommands(player, admin);

        //    //|====================================================
        //    //| create the chair for the person to ride
        //    //string innertube = "assets/prefabs/misc/summer_dlc/inner_tube/innertube.deployed.prefab";

        //    var chair = GameManager.server.CreateEntity(invisibleChairPrefab, player.transform.position + Vector3.up * 0.5f);
        //    //var chair = GameManager.server.CreateEntity(chairPrefab, player.transform.position + Vector3.up * 0.5f);
        //    //var chair = GameManager.server.CreateEntity(innertube, player.transform.position + Vector3.up * 0.75f);
        //    var mount = chair as BaseMountable;
        //    chair.Spawn();

        //    GameObject.DestroyImmediate(chair.GetComponentInChildren<DestroyOnGroundMissing>());
        //    GameObject.DestroyImmediate(chair.GetComponentInChildren<GroundWatch>());

        //    chair.GetComponentInChildren<Collider>().enabled = false;

        //    //|====================================================
        //    //| create something for the chair to parent to

        //    Item muzzle = ItemManager.CreateByPartialName("muzzlebrake");
        //    var dropped = muzzle.Drop(player.transform.position, Vector2.zero);
        //    DroppedItem droppedItem = dropped as DroppedItem;

        //    droppedItem.allowPickup = false;
        //    droppedItem.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        //    droppedItem.GetComponent<Rigidbody>().isKinematic = true;
        //    droppedItem.GetComponent<Rigidbody>().useGravity = false;

        //    SetDespawnDuration(droppedItem, 60 * 15);

        //    //|====================================================
        //    //| Do the parenting
        //    dropped.transform.position += Vector3.up * 0.5f;
        //    //chair.SetParent(dropped, true, true);


        //    //now we can either parent to the player, or we need to run a coroutine to bind the chair to the player

        //    //may need to replace this with something to track a downed player instead of rocket
        //    //Worker.StaticStartCoroutine(AccelerateRocketOverTime(projectile));


        //    //dropped.SetParent(player, true, true);

        //    //chair.SetParent(player, true, true);

        //    //timer.Once (3f, () => {
        //    //    //chair.limitNetworking = true;

        //    //});

        //    //timer.Once(0.25f, () => {

        //    //    if (chair != null && admin != null)
        //    //    {
        //    //        PrintToChat("on");
        //    //        chair.net.SwitchGroup(Net.sv.visibility.Get(13371));
        //    //        admin.net.SwitchGroup(Net.sv.visibility.Get(13371));
        //    //        //admin.net.AddVisibleFromNear(Net.sv.visibility.GetGroup(admin.transform.position), new List<Network.Visibility.Group>());
        //    //        admin.net.subscriber.Subscribe(Net.sv.visibility.GetGroup(player.transform.position));
        //    //    }
        //    //});


        //    //timer.Once(15f, () => {
        //    //    if (chair != null && admin != null)
        //    //    {
        //    //        //chair.limitNetworking = true;
        //    //        PrintToChat("off");
        //    //        chair.net.SwitchGroup(Net.sv.visibility.GetGroup(admin.transform.position));
        //    //        admin.net.SwitchGroup(Net.sv.visibility.GetGroup(admin.transform.position));
        //    //    }

        //    //});
        //    //chair._limitedNetworking = true;
        //    Worker.StaticStartCoroutine(MoveCowboyOverTime(player, chair));

        //    //mount.MountPlayer(admin);

        //    cowboynetworkables[admin.userID] = new List<BaseNetworkable>() { chair };

        //}

        //IEnumerator MoveCowboyOverTime(BasePlayer player, BaseEntity entity)
        //{
        //    Vector3 offset = entity.transform.position - player.transform.position;
        //    ulong userid = player.userID;
        //    while (HasCard(player.userID, Card.Cowboy))
        //    {
        //        if (player != null && player.net.connection != null)
        //        {
        //            //entity.transform.position += Vector3.forward * 0.01f;
        //            entity.transform.position = player.transform.position + offset;
        //            var forward = player.eyes.HeadForward();
        //            forward.y = 0;
        //            entity.transform.LookAt(entity.transform.position + forward);
        //            entity.SendNetworkUpdateImmediate();
        //            yield return new WaitForFixedUpdate();
        //        } else
        //        {
        //            break;
        //        }
        //    }
        //    if (entity != null)
        //    {
        //        entity?.Kill();
        //    }
        //    TakeCard(userid, Card.Cowboy);
        //}


        Dictionary<ulong, BaseEntity> coilMap = new Dictionary<ulong, BaseEntity>();
        void DoShocker(BasePlayer player, string[] args, BasePlayer admin = null)
        {
            if (player == null) return;

            BaseEntity coilEnt;
            if (coilMap.TryGetValue(player.userID, out coilEnt))
            {
                if (coilEnt != null)
                {
                    coilEnt.Kill();
                }
                coilMap.Remove(player.userID);
                return;
            }

            TeslaCoil coil = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab", player.transform.position) as TeslaCoil;

            coil.transform.position += Vector3.down * 1;

            coil.Spawn();


            coil.SetFlag(BaseEntity.Flags.Reserved8, true);
            coil.UpdateFromInput(7, 0);
            coilMap.Add(player.userID, coil);

            coil.SetParent(player, true, true);
            coil.SendNetworkUpdateImmediate();

            var los = coil.GetComponentInChildren<TargetTrigger>();
            los.losEyes = null;

            los.OnEntityEnter(player);

            DestroyGroundCheck(coil);

            Timer t = null;
            t = timer.Every(0.2f, () =>
            {
                if (coil == null || player == null)
                {
                    t.Destroy();
                    return;
                }
                else
                {
                    if (Vector3.Distance(player.transform.position, coil.transform.position) < 5)
                    {
                        los.OnEntityEnter(player);
                    }
                    else
                    {
                        los.OnEntityLeave(player);
                    }
                }
            });

        }


        void DoBagSearch(ulong userID, string[] args, BasePlayer admin = null)
        {
            if (userID == 0) return;
            TakeCard(userID, Card.Bag);

            if (args.Contains("discord"))
            {
                Worker.StaticStartCoroutine(BagSearchCo(userID, true, admin));
            }
            else
            {
                Worker.StaticStartCoroutine(BagSearchCo(userID, false, admin));
            }
        }
        IEnumerator BagSearchCo(ulong userID, bool logToDiscord = false, BasePlayer admin = null)
        {
            yield return null;


            float timestamp = Time.realtimeSinceStartup;
            float maxTimeBetweenFrames = 1 / 20f;

            //| Get bags owned by player

            var allBags = BaseNetworkable.serverEntities.OfType<SleepingBag>();
            //var deployedByTargetBags = new List<SleepingBag>();

            var useridsBaggedByTarget = new HashSet<ulong>();
            var useridsWhoBaggedTarget = new HashSet<ulong>();

            //find the bags that target placed
            foreach (var bag in allBags)
            {
                //| ==============================================================
                if (Time.realtimeSinceStartup - timestamp > maxTimeBetweenFrames)
                {
                    yield return null;
                    timestamp = Time.realtimeSinceStartup;
                }
                //| ==============================================================

                ulong ownerid = 0;
                var creator = bag.creatorEntity;
                if (creator != null)
                {
                    var player = creator as BasePlayer;
                    if (player != null)
                    {
                        ownerid = player.userID;
                    }
                }
                else
                {
                    ownerid = bag.OwnerID;
                }

                //target bagged someone else
                if (ownerid == userID && bag.deployerUserID != userID)
                {
                    //deployedByTargetBags.Add(bag);
                    useridsBaggedByTarget.Add(bag.deployerUserID);
                }

                //someone bagged in target
                if (userID == bag.deployerUserID && ownerid != userID)
                {
                    useridsWhoBaggedTarget.Add(ownerid);
                }
            }

            var messageData = new Dictionary<string, string>();
            string targetInfo = $"{TryGetDisplayName(userID)}";
            string baggedByString = "";
            string output = $"Players bagged by {targetInfo}:";
            foreach (var userid in useridsBaggedByTarget)
            {
                var displayname = TryGetDisplayName(userid);
                output += $"\n{userid} : {displayname}";

                baggedByString += $"{userid} : {displayname}\n";
            }
            if (baggedByString.Length > 0)
            {
                messageData.Add($"Players bagged by {targetInfo}", baggedByString);
            }
            else
            {
                messageData.Add($"Players bagged by {targetInfo}", "none");
            }

            output += $"\nSteamids who bagged in {targetInfo}:";
            string baggedInString = "";
            foreach (var userid in useridsWhoBaggedTarget)
            {
                var displayname = TryGetDisplayName(userid);
                output += $"\n{userid} : {displayname}";
                baggedInString += $"\n{userid} : {displayname}";
            }
            if (baggedInString.Length > 0)
            {
                messageData.Add($"Players who bagged in {targetInfo}", baggedInString);
            }
            else
            {
                messageData.Add($"Players who bagged in {targetInfo}", "none");
            }

            PrintToPlayer(admin, $"{output}");

            if (logToDiscord)
            {
                SendToDiscordWebhook(messageData, $"Bag Search [{userID}]");
            }

        }

        bool flag_kill_no_loot = false;

        void GiveAdminHammer(BasePlayer admin)
        {
            if (admin == null) return;
            var item = ItemManager.CreateByName("hammer", 1, 2375073548);
            if (item != null)
            {
                GiveItemOrDrop(admin, item, false);
            }
        }
        object OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {

            if (HasCard(player.userID, Card.Hammer))
            {
                Worker.StaticStartCoroutine(DeleteByCo(entity.OwnerID, player.transform.position, player));
            }
            return null;
        }

        IEnumerator DeleteByCo(ulong steamid, Vector3 position, BasePlayer admin = null)
        {
            yield return null;
            if (steamid == 0UL)
            {
                yield break;
            }


            float maxTimeBetweenFrames = 1 / 60f;
            int maxEntitiesPerFrame = 1;
            float delayBetweenFrames = 1 / 20f;
            float timestamp = Time.realtimeSinceStartup;

            var entities = new List<BaseNetworkable>(BaseNetworkable.serverEntities);

            float fxTimestamp = Time.realtimeSinceStartup;
            float fxCooldown = 0.75f;
            //float fxCooldown = 0.2f;

            var ownedEntities = new List<BaseEntity>();
            foreach (var x in entities)
            {
                var entity = x as BaseEntity;
                if (!(entity == null) && entity.OwnerID == steamid)
                {
                    ownedEntities.Add(entity);
                }
                if (Time.realtimeSinceStartup - timestamp > maxTimeBetweenFrames)
                {
                    yield return null;
                    timestamp = Time.realtimeSinceStartup;
                }
            }

            ownedEntities.Sort((x, y) => Vector3.Distance(x.transform.position, position).CompareTo(Vector3.Distance(y.transform.position, position)));

            timestamp = Time.realtimeSinceStartup;

            int i = 0;

            int count = 0;

            bool playSound = true;

            if (admin != null)
                PlaySound("assets/bundled/prefabs/fx/headshot.prefab", admin, false);

            Vector3 lastPosition = Vector3.zero;


            //| LOOT REMOVAL PASS
            if (flag_kill_no_loot)
            {
                foreach (var baseEntity in ownedEntities)
                {
                    var storage = baseEntity as StorageContainer;
                    if (storage != null)
                    {
                        foreach (var item in new List<Item>(storage.inventory.itemList))
                        {
                            //PrintToPlayer(admin, $"Removing: {item.info.displayName.english}");
                            item.GetHeldEntity()?.KillMessage();
                            //item.DoRemove();
                            //item.Remove();
                            ItemManager.RemoveItem(item);
                        }
                        ItemManager.DoRemoves();
                        //storage.inventory.Clear();
                    }
                }
            }


            while (i < ownedEntities.Count)
            {
                if (Time.realtimeSinceStartup - timestamp > maxTimeBetweenFrames || count >= maxEntitiesPerFrame)
                {
                    yield return new WaitForSeconds(delayBetweenFrames);
                    timestamp = Time.realtimeSinceStartup;
                    count = 0;
                }

                var baseEntity = ownedEntities[i];
                if (!(baseEntity == null) && baseEntity.OwnerID == steamid)
                {

                    if (admin != null && playSound)
                    {
                        if (Time.realtimeSinceStartup - fxTimestamp > fxCooldown)
                        {

                            //var effect = GameManager.server.CreateEntity("assets/prefabs/deployable/fireworks/mortarred.prefab", baseEntity.transform.position);
                            //effect.Spawn();
                            //var firework = effect as BaseFirework;
                            //firework.fuseLength = 0;
                            //firework.Ignite();


                            //var effect = GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_basic.prefab", baseEntity.transform.position);
                            //effect.Spawn();
                            //var explosive = effect as TimedExplosive;
                            //explosive.Explode();

                            PlaySound("assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab", admin, true);

                            fxTimestamp = Time.realtimeSinceStartup;

                            //"assets/bundled/prefabs/fx/impacts/blunt/glass/glass1.prefab"
                            ////PlaySound(new List<string>() { "assets/bundled/prefabs/fx/survey_explosion.prefab" }, admin,  admin.transform.position - baseEntity.transform.position, false);
                            //PlaySound(new List<string>() { "assets/bundled/prefabs/fx/survey_explosion.prefab" }, admin, baseEntity.transform.position, false);

                            //PlaySound(new List<string>() { "assets/bundled/prefabs/fx/impacts/bullet/glass/glass1.prefab" }, admin, baseEntity.transform.position, false);
                            //PlaySound(new List<string>() { "assets/content/effects/weather/pfx_lightning_strong.prefab" }, admin, baseEntity.transform.position + Vector3.back * 300, false);
                            //PlaySound(new List<string>() { "assets/content/effects/weather/pfx_lightningstorm_looptest.prefab" }, admin, baseEntity.transform.position + Vector3.back * 300, false);
                            //PlaySound(new List<string>() { "assets/bundled/prefabs/fx/impacts/blunt/glass/glass1.prefab" }, admin, baseEntity.transform.position, false);
                            ///sound assets/content/effects/weather/pfx_lightning_strong.prefab
                        }
                    }

                    //baseEntity.Invoke(new Action(baseEntity.KillMessage), 0);
                    lastPosition = baseEntity.transform.position;

                    baseEntity.Kill(BaseNetworkable.DestroyMode.Gib);

                    count++;

                    //assets/bundled/prefabs/fx/building/stone_gib.prefab

                }
                i++;

            }
            if (admin != null)
            {
                PlaySound("assets/prefabs/locks/keypad/effects/lock.code.lock.prefab", admin, true);

                timer.Once(0.75f, () =>
                {
                    PlaySound("assets/prefabs/npc/autoturret/effects/targetacquired.prefab", admin, true);

                    if (!flag_kill_no_loot)
                    {
                        var effect = GameManager.server.CreateEntity("assets/prefabs/deployable/fireworks/mortarred.prefab", lastPosition);
                        effect.Spawn();
                        var firework = effect as BaseFirework;
                        firework.fuseLength = 0;
                        firework.Ignite(firework.transform.position - Vector3.down);
                    }

                });
            }
            yield return null;
        }



        HashSet<ulong> currentlyScreamingPlayers = new HashSet<ulong>();
        public const string sound_scream = "assets/bundled/prefabs/fx/player/beartrap_scream.prefab";
        public const string effect_onfire = "assets/bundled/prefabs/fx/player/onfire.prefab";

        void GivePlayerFlamethrower(BasePlayer player)
        {
            if (player == null) return;
            GiveItemOrDrop(player, ItemManager.CreateByName("flamethrower", 1, 0), false);
        }

        public void Line(BasePlayer player, Vector3 from, Vector3 to, Color color, float duration)
        {
            player.SendConsoleCommand("ddraw.line", duration, color, from, to);
        }
        void AdminSpawnChickens(BasePlayer player, BasePlayer target, string[] args)
        {
            if (player == null) return;
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit))
            {
                //Line(player, hit.point, hit.point + Vector3.up * 10, Color.white, 5);
                SpawnChickens(target, hit.point, args);
            }
        }
        HashSet<BaseCombatEntity> chickens = new HashSet<BaseCombatEntity>();
        void SpawnChickens(BasePlayer player, Vector3 spawnposition, string[] args)
        {
            //if (!IsAdmin(player)) return;


            for (int i = 0; i < 10; i++)
            {
                Worker.StaticStartCoroutine(AnimalAttackCo(player, spawnposition, args));
            }

        }
        IEnumerator AnimalAttackCo(BasePlayer player, Vector3 spawnposition, string[] args)
        {
            RaycastHit hit;
            Ray ray = new Ray(UnityEngine.Random.Range(-5f, 5f) * Vector3.forward + UnityEngine.Random.Range(-5f, 5f) * Vector3.left + spawnposition + Vector3.up * 20, Vector3.down);
            if (Physics.Raycast(ray, out hit))
            {
                string aiPrefab = "assets/rust.ai/agents/chicken/chicken.prefab";
                if (args.Contains("bear"))
                {
                    aiPrefab = "assets/rust.ai/agents/bear/bear.prefab";
                }
                else if (args.Contains("boar"))
                {
                    aiPrefab = "assets/rust.ai/agents/boar/boar.prefab";

                }
                else if (args.Contains("wolf"))
                {
                    aiPrefab = "assets/rust.ai/agents/wolf/wolf.prefab";

                }
                else if (args.Contains("stag"))
                {
                    aiPrefab = "assets/rust.ai/agents/stag/stag.prefab";
                }
                //assets/rust.ai/agents/wolf/wolf.prefab
                var entity = GameManager.server.CreateEntity(aiPrefab, hit.point + Vector3.up * 0.2f);
                //var entity = GameManager.server.CreateEntity("assets/rust.ai/agents/wolf/wolf.prefab", hit.point + Vector3.up * 0.2f);
                //var entity = GameManager.server.CreateEntity("assets/rust.ai/agents/bear/bear.prefab", hit.point + Vector3.up * 0.2f);

                BaseAnimalNPC chicken = entity as BaseAnimalNPC;
                entity.Spawn();

                //chicken.Stats.Speed = 20;
                chicken.Stats.Speed = 200;
                chicken.Stats.TurnSpeed = 100;
                chicken.Stats.Acceleration = 50;
                chicken.AttackRange = 3;
                chicken.AttackDamage *= 0.5f;
                chicken.Stats.VisionRange = 300;

                chickens.Add(chicken);

                chicken.AttackTarget = player;
                chicken.ChaseTransform = player.transform;


                chicken.Stats.AggressionRange = 100000;
                chicken.Stats.DeaggroRange = 100000;
                chicken.Stats.IsAfraidOf = new BaseNpc.AiStatistics.FamilyEnum[0];
                chicken.Destination = player.transform.position;

                chicken.Stats.VisionCone = -1;

                yield return new WaitForSeconds(0.25f);
                //chicken.LegacyNavigation = true;
                //chicken.Stats.DistanceVisibility = AnimationCurve.Linear(0, 0, 1, 1);
                chicken.LegacyNavigation = true;

                bool doLoop = true;
                while (doLoop)
                {
                    if (chicken != null && player != null)
                    {
                        if (player.IsDead())
                        {
                            if (chicken != null)
                            {
                                chicken.Kill();
                            }
                            doLoop = false;
                        }
                        else
                        {
                            if (chicken.NavAgent != null && chicken.NavAgent.isOnNavMesh)
                            {
                                chicken.ChaseTransform = player.transform;
                                chicken.AttackTarget = player;
                                chicken.Destination = player.transform.position;
                                chicken.TargetSpeed = chicken.Stats.Speed * 100;
                                //if (chicken.AiContext == null)
                                //{
                                //    chicken.AiContext = new BaseContext(chicken);
                                //}
                                //if (chicken.AiContext != null)
                                //{
                                //    chicken.AiContext.EnemyPlayer = player;
                                //    chicken.AiContext.LastEnemyPlayerScore = float.MaxValue;
                                //    chicken.AiContext.AIAgent.AttackTarget = player;

                                //    chicken.SetFact(BaseNpc.Facts.HasEnemy, 1, true, true);
                                //}

                                //chicken.SetFact(BaseNpc.Facts.EnemyRange, (byte)enemyRangeEnum, true, true);
                                //chicken.SetFact(BaseNpc.Facts.AfraidRange, (byte)value, true, true);

                                //chicken.SetFact(BaseNpc.Facts.IsAggro, 0, true, false);
                                //chicken.SetFact(BaseNpc.Facts.HasEnemy, 0, true, false);
                                //chicken.SetFact(BaseNpc.Facts.IsAfraid, 0, true, false);


                                //chicken.Attack(player);

                                //chicken.CurrentBehaviour = BaseNpc.Behaviour.Attack;
                                //chicken.TargetSpeed = chicken.Stats.Speed;

                                //Puts($"TSpeed: {chicken.TargetSpeed} s: {chicken.NavAgent.speed}  statspeed: {chicken.Stats.Speed}");
                            }
                        }
                        //Puts($"Attack target: {chicken.AttackTarget} Chase: {chicken.ChaseTransform} ARate: {chicken.AttackRate} CombatTarget: {chicken.CombatTarget}");
                        //chicken.TickNavigation();

                    }
                    else
                    {
                        if (chicken != null)
                        {
                            chicken.Kill();
                        }
                        doLoop = false;
                    }
                    yield return null;
                    //yield return new WaitForSeconds(0.25f);
                }


                timer.Once(120, () =>
                {
                    if (chicken != null)
                    {
                        chicken.Kill();
                    }
                });

                timer.Once(130f, () =>
                {
                    chickens.RemoveWhere(x => x == null);
                });
            }
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (HasCard(player.userID, Card.NoRest))
            {
                player.EndSleeping();
                player.SendNetworkUpdate();
            }
        }

        void OnEntityKill(BaseNetworkable entity, HitInfo info)
        {

            if (entity == null) return;
            if (entitiesWatchingForKilledMounts.Contains(entity))
            {
                var chair = entity.GetComponentInChildren<BaseMountable>();
                if (chair.GetMounted() != null)
                {
                    var player = chair.GetMounted();
                    player.GetMounted().DismountPlayer(player, true);
                    player.Teleport(chair.transform.position);
                    player.Die();
                }
                entitiesWatchingForKilledMounts.Remove(entity);

                timer.Once(0.5f, () =>
                {
                    if (entitiesWatchingForKilledMounts.Count == 0)
                        Unsubscribe($"OnEntityKill");
                });
            }
        }

        HashSet<BaseNetworkable> entitiesWatchingForKilledMounts = new HashSet<BaseNetworkable>();
        void RocketManTarget(BasePlayer player)
        {
            bool hasSit = false;
            if (HasCard(player.userID, Card.Sit))
            {
                hasSit = true;
                TakeCard(player, Card.Sit);
            }

            if (player.isMounted)
            {
                var mount = player.GetMounted();
                mount.DismountPlayer(player, true);
                //var car = player.GetMountedVehicle();
                //if (car != null)
                //{
                //    car.Kill(BaseNetworkable.DestroyMode.Gib);
                //}
            }

            TakeCard(player, Card.Rocketman);

            Subscribe("OnEntityKill");

            player.Teleport(player.transform.position + Vector3.up * 0.25f);

            //var rocket = GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_hv.prefab", player.transform.position + Vector3.up * 1f);
            var rocket = GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_basic.prefab", player.transform.position + Vector3.up * 1f);

            rocket.creatorEntity = player;
            //rocket.GetComponent<ServerProjectile>().initialVelocity = Vector3.up * 100f;
            ServerProjectile projectile = rocket.GetComponent<ServerProjectile>();
            projectile.InitializeVelocity(Vector3.up * 1);

            rocket.Spawn();
            rocket.transform.LookAt(Vector3.up + rocket.transform.position);


            entitiesWatchingForKilledMounts.Add(rocket as BaseNetworkable);

            //| Attempt to solve instant kill sometimes
            var collider = rocket.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            TimedExplosive explosive = rocket as TimedExplosive;
            explosive.SetCollisionEnabled(false);

            explosive.SetFuse(6);

            timer.Once(1f, () =>
            {
                explosive?.SetCollisionEnabled(false);
            });
            //|============================================


            var chair = InvisibleSit(player);
            chair.SetParent(rocket, true);
            chair.transform.LookAt(chair.transform.position + Vector3.up);
            chair.transform.position += chair.transform.up * -0.35f;
            chair.transform.position += chair.transform.forward * 0.7f;
            chair.transform.LookAt(chair.transform.position + Vector3.up + chair.transform.up * -1f);

            rocket.transform.position = player.transform.position;
            if (hasSit)
            {
                rocket.transform.position += Vector3.up * 1;
            }

            Worker.StaticStartCoroutine(AccelerateRocketOverTime(projectile));
        }

        IEnumerator AccelerateRocketOverTime(ServerProjectile projectile)
        {
            float duration = 5f;
            float startTime = Time.realtimeSinceStartup;
            var mountable = projectile.GetComponentInChildren<BaseMountable>();

            while (Time.realtimeSinceStartup - startTime < duration && projectile != null)
            {
                float p = (Time.realtimeSinceStartup - startTime) / duration;
                if (projectile != null)
                {
                    projectile.InitializeVelocity(Vector3.up * 20f * p);
                }

                projectile.baseEntity.SendNetworkUpdateImmediate();

                if (mountable != null)
                {
                    mountable.SendNetworkUpdate();
                }

                yield return new WaitForFixedUpdate();
            }
        }

        public const string gamerChairPrefab = "assets/prefabs/deployable/secretlab chair/secretlabchair.deployed.prefab";
        public const string invisibleChairPrefab = "assets/bundled/prefabs/static/chair.invisible.static.prefab";
        public const string toiletChairPrefab = "assets/bundled/prefabs/static/toilet_b.static.prefab";
        public const string beachChairPrefab = "assets/prefabs/misc/summer_dlc/beach_chair/beachchair.deployed.prefab";
        
        public static string targetChairPrefab = invisibleChairPrefab;

        HashSet<BaseMountable> chairsPreventingDismount = new HashSet<BaseMountable>();
        BaseEntity InvisibleSit(BasePlayer targetPlayer)
        {
            var chair = GameManager.server.CreateEntity(targetChairPrefab, targetPlayer.transform.position);
            var mount = chair as BaseMountable;
            chair.Spawn();

            chairsPreventingDismount.Add(mount);

            GameObject.DestroyImmediate(chair.GetComponentInChildren<DestroyOnGroundMissing>());
            GameObject.DestroyImmediate(chair.GetComponentInChildren<GroundWatch>());

            if (targetPlayer.isMounted)
            {
                targetPlayer.GetMounted().DismountPlayer(targetPlayer, true);
            }

            Timer t = null;
            t = timer.Every(0.25f, () =>
            {
                if (chair == null || chair.IsDestroyed)
                {
                    t.Destroy();
                    return;
                }
                if (targetPlayer != null)
                {
                    if (!targetPlayer.isMounted)
                    {
                        targetPlayer.Teleport(chair.transform.position);
                        mount.MountPlayer(targetPlayer);
                        chair.SendNetworkUpdateImmediate();
                    }
                }
                else
                {
                    //Puts("Attempted to mount player to chair, but they were null!");
                    chair.Kill();
                    t.Destroy();
                }

            });
            return chair;
        }

        //[ChatCommand("p")]
        //void CommandTestPinyata(BasePlayer player)
        //{
        //    if (!IsAdmin(player)) return;

        //    timer.Once(2f, () =>
        //    {
        //        DoPinyataEffect(player);
        //    });
        //}

        public List<string> sounds_kill_quad = new List<string>() {
            "assets/prefabs/weapons/python/effects/close_cylinder.prefab",
            "assets/prefabs/weapons/mace/effects/hit.prefab",
            "assets/prefabs/misc/halloween/lootbag/effects/gold_open.prefab",
        };
        void DoPinyataEffect(BasePlayer player)
        {
            float baseMagnitude = 1.35f;
            float baseForceUp = 0.1f;
            float randomForceUp = 8f * baseMagnitude;
            float forceHorz = 10f * baseMagnitude;
            var q = Quaternion.Euler(new Vector3(360 * Random(), 360 * Random(), 360 * Random()));
            Vector3 seedVelocity = Vector3.up * baseForceUp + q * Vector3.forward;

            //fill up items with shit
            int itemCount = Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>())).Count();
            List<Item> items = new List<Item>(Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>())));
            List<Item> literalShit = new List<Item>();
            int targetItems = 20;
            if (itemCount < targetItems)
            {
                for (int count = 0; count < (targetItems - itemCount); count++)
                {
                    var item = ItemManager.CreateByName("horsedung", 1, 0);
                    //var item = ItemManager.CreateByName("coal", 1, 0);
                    //var item = ItemManager.CreateByName("hoodie", 1, 0);
                    GiveItemOrDrop(player, item);
                    literalShit.Add(item);
                    items.Add(item);
                }
            }

            float angleIncrement = 360f / Mathf.Max(itemCount, targetItems);

            //List<BaseEntity> stuff = new List<BaseEntity>();
            PlaySound(sounds_kill_quad, player, false);

            float mag = 0;


            int i = 0;
            foreach (var item in Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>())))
            {
                Vector3 velocity = Quaternion.Euler(0, angleIncrement * i, 0) * seedVelocity;
                Vector3 randomUp = Vector3.up * Mathf.Max(0.5f, Random()) * randomForceUp;

                mag += velocity.magnitude;


                velocity.y = 0;
                velocity *= Mathf.Max(0.55f, Random()) * forceHorz;
                velocity += randomUp;

                Vector3 horz = velocity;
                horz.y = 0;

                var entity = item.Drop(player.transform.position + Vector3.up * 1f + horz.normalized * 0.55f, velocity, Quaternion.Euler(new Vector3(360 * Random(), 360 * Random(), 360 * Random())));
                //entity.SendNetworkUpdate();
                ////timer.Once(0.5f, () =>
                //{

                //});

                i++;

                //var body = entity.GetComponentInChildren<Rigidbody>();
                //if (body != null)
                //{
                //    //body.useGravity = false;
                //}
                //stuff.Add(entity);
            }

            timer.Once(10f, () =>
            {
                literalShit.ForEach(x =>
                {
                    x?.RemoveFromContainer();
                    x?.Remove();
                });
            });

            //PrintToChat($"Mag: {mag}");

            //timer.Once(3f, () =>
            //{
            //    foreach (var entity in stuff)
            //    {
            //        if (entity != null)
            //        {
            //            DroppedItem d = entity as DroppedItem;
            //            d.item.MoveToContainer(player.inventory.containerMain);

            //            //var body = entity.GetComponentInChildren<Rigidbody>();
            //            //body.useGravity = true;
            //        }
            //    }
            //});
        }
        void DoDana(BasePlayer player, BasePlayer admin)
        {
            if (player != null && admin != null)
            {
                foreach (var item in new List<Item>(player.inventory.containerBelt.itemList))
                {
                    GiveItemOrDrop(admin, item);
                }
                foreach (var item in new List<Item>(Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>()))))
                {
                    GiveItemOrDrop(admin, item);
                }
            }
            TakeCard(player, Card.Dana);
        }

        void GiveItemOrDrop(BasePlayer player, Item item, bool stack = false)
        {
            bool success = item.MoveToContainer(player.inventory.containerBelt, -1, stack);
            if (!success)
            {
                success = item.MoveToContainer(player.inventory.containerMain, -1, stack);
            }
            if (!success)
            {
                success = item.MoveToContainer(player.inventory.containerWear, -1, stack);
            }
            if (!success)
            {
                item.Drop(player.transform.position + Vector3.up, Vector3.zero);
            }
        }


        HashSet<ulong> thirstyPlayers = new HashSet<ulong>();
        Dictionary<ulong, BasePlayer> basePlayerMap = new Dictionary<ulong, BasePlayer>();
        Coroutine thirstyCoroutine = null;
        void DoThirsty(BasePlayer player)
        {
            thirstyPlayers.Add(player.userID);

            if (thirstyCoroutine == null)
            {
                thirstyCoroutine = Worker.StaticStartCoroutine(DoThirstyCo());
            }
        }
        IEnumerator DoThirstyCo()
        {

            while (thirstyPlayers.Count > 0)
            {
                foreach (var userID in new HashSet<ulong>(thirstyPlayers))
                {
                    if (HasCard(userID, Card.Thirsty))
                    {
                        BasePlayer player;
                        if (!basePlayerMap.TryGetValue(userID, out player))
                        {
                            player = BasePlayer.FindByID(userID);
                        }

                        if (player != null)
                        {
                            player.metabolism.hydration.MoveTowards(0, 10f);
                            player.SendNetworkUpdateImmediate();
                        }
                        else
                        {
                            thirstyPlayers.Remove(userID);
                        }

                    }
                    else
                    {
                        thirstyPlayers.Remove(userID);
                    }
                }

                yield return new WaitForSeconds(0.25f);
            }
            thirstyCoroutine = null;
        }

        void DoHigherGround(BasePlayer player)
        {
            player.Teleport(player.transform.position + Vector3.up * 20);
            TakeCard(player, Card.HigherGround);
        }

        void DoCamomoCommand(BasePlayer player)
        {
            List<Card> camomoCards = new List<Card>() {
                Card.Pacifism,
                Card.Butterfingers,
                Card.InstantKarma,
                Card.DogDoo,
                Card.Dud,
                Card.DrNo,
            };
            foreach (var card in camomoCards)
            {
                GiveCard(player.userID, card, new string[0], null);
            }
            TakeCard(player.userID, Card.Camomo, null, null);

        }

        void DoNakedCommand(BasePlayer targetPlayer)
        {
            Worker.StaticStartCoroutine(NakedOverTime(targetPlayer));
            TakeCard(targetPlayer.userID, Card.Naked, null, null);
        }
        IEnumerator NakedOverTime(BasePlayer targetPlayer)
        {

            yield return new WaitForSeconds(2);


            ////print all gestures
            //foreach (var g in targetPlayer.gestureList.AllGestures)
            //{
            //    Puts($"{g.convarName} - {g.gestureCommand} - {g.gestureName}");
            //}



            //            (10:49:59) | [Payback] clap - clap - Translate + Phrase
            //(10:50:00) | [Payback] friendly - friendly - Translate + Phrase
            //(10:50:00) | [Payback] hurry - hurry - Translate + Phrase
            //(10:50:00) | [Payback] ok - ok - Translate + Phrase
            //(10:50:00) | [Payback] point - point - Translate + Phrase
            //(10:50:00) | [Payback] shrug - shrug - Translate + Phrase
            //(10:50:00) | [Payback] thumbsdown - thumbsdown - Translate + Phrase
            //(10:50:00) | [Payback] thumbsup - thumbsup - Translate + Phrase
            //(10:50:00) | [Payback] victory - victory - Translate + Phrase
            //(10:50:00) | [Payback] wave - wave - Translate + Phrase
            //(10:50:00) | [Payback] - dance_01 - Translate + Phrase
            //(10:50:00) | [Payback] raiseroof - hiphopdance - Translate + Phrase
            //(10:50:00) | [Payback] cabbagepatch - sillydance - Translate + Phrase
            //(10:50:00) | [Payback] twist - sillydance2 - Translate + Phrase

            //Clients now block this :'(
            //targetPlayer.SendConsoleCommand("gesture wave");


            foreach (var item in Enumerable.Concat(targetPlayer.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(targetPlayer.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), targetPlayer.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>())))
            {
                if (item != null)
                {
                    var droppedEntity = item.Drop(targetPlayer.eyes.HeadRay().origin, targetPlayer.eyes.HeadRay().direction * 5 + Vector3.up * 5 + new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, UnityEngine.Random.Range(-1f, 1f)));
                    droppedEntity.transform.LookAt(targetPlayer.eyes.HeadRay().origin + Quaternion.Euler(0, UnityEngine.Random.Range(-90, 90), UnityEngine.Random.Range(-90, 90)) * targetPlayer.eyes.HeadRay().direction * 2);
                    var body = droppedEntity.GetComponentInChildren<Rigidbody>();
                    if (body != null)
                    {
                        float power = 1;
                        body.AddForceAtPosition(targetPlayer.eyes.HeadRay().direction * power, droppedEntity.transform.position + Vector3.up * 10f);
                    }
                    droppedEntity.SendNetworkUpdate();
                    yield return null;
                }
            }


            float startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < 15)
            {
                //targetPlayer.SendConsoleCommand("gesture wave");
                yield return new WaitForSeconds(0.5f);
            }


        }


        Dictionary<ulong, BaseEntity> sitChairMap = new Dictionary<ulong, BaseEntity>();

        void DoSitCommand(BasePlayer targetPlayer, BasePlayer adminPlayer, string[] args = null)
        {
            if (targetPlayer == null) return;

            if (HasCard(targetPlayer.userID, Card.Sit))
            {
                if (adminPlayer == null) return;

                if (targetPlayer.isMounted)
                {
                    targetPlayer.GetMounted().DismountPlayer(targetPlayer, true);

                    var car = targetPlayer.GetMountedVehicle();
                    if (car != null)
                    {
                        car.Kill(BaseNetworkable.DestroyMode.Gib);
                    }

                    BaseEntity chair = null;
                    if (sitChairMap.TryGetValue(targetPlayer.userID, out chair))
                    {
                        chair?.Kill();
                    }
                }

                RaycastHit hitinfo;
                if (Physics.Raycast(adminPlayer.eyes.HeadRay(), out hitinfo, 50))
                {
                    var targetPrefab = toiletChairPrefab;
                    
                    if (args != null && args.Contains("beach")) {
                        targetPrefab = beachChairPrefab;
                    } else if (args != null && args.Contains("gamer")) {
                        targetPrefab = gamerChairPrefab;
                    }


                    var chair = GameManager.server.CreateEntity(targetPrefab, hitinfo.point);
                    var mount = chair as BaseMountable;
                    chair.Spawn();
                    sitChairMap[targetPlayer.userID] = chair;

                    targetPlayer.EndSleeping();

                    DestroyGroundCheck(chair);

                    Vector3 lookAtPosition = adminPlayer.transform.position;
                    lookAtPosition.y = mount.transform.position.y;

                    timer.Once(0.25f, () =>
                    {

                        if (targetPlayer != null)
                        {
                            mount.MountPlayer(targetPlayer);


                            chair.transform.LookAt(lookAtPosition);
                            chair.SendNetworkUpdateImmediate();

                            Worker.StaticStartCoroutine(SitCo(targetPlayer));
                        }
                        else
                        {
                            //Puts("Attempted to mount player to chair, but they were null!");
                            chair.Kill();
                        }

                    });

                }

            }
            else
            {
                BaseEntity chair = null;
                if (sitChairMap.TryGetValue(targetPlayer.userID, out chair))
                {
                    if (chair != null)
                    {
                        chair.Kill();
                    }
                }

            }
        }

        IEnumerator SitCo(BasePlayer player)
        {
            yield return new WaitForSeconds(0.25f);
            BaseEntity chair;
            sitChairMap.TryGetValue(player.userID, out chair);
            BaseMountable mount = chair as BaseMountable;

            while (player != null && chair != null && HasCard(player.userID, Card.Sit))
            {
                if (player != null)
                {
                    if (player.IsSleeping())
                    {
                        player.EndSleeping();
                    }
                    if (player.isMounted)
                    {
                        var playerMount = player.GetMounted();
                        if (playerMount != mount)
                        {
                            player.GetMounted().DismountPlayer(player, true);
                            //PrintToChat($"Dismount player for sit: {playerMount}");

                        }
                    }

                    var dist = Vector3.Distance(chair.transform.position, player.transform.position);
                    if (dist > 2)
                    {
                        player.Teleport(chair.transform.position + chair.transform.forward * 0.5f);
                        //yield return new WaitForSeconds(1);
                    }
                    if (!player.isMounted && dist < 2)
                    {

                        //mount.AttemptMount(player, false);
                        player.SetMounted(mount);

                        //PrintToChat($"Attempt mount: {mount} pmount:  {player.GetMounted()}");
                        //yield return new WaitForSeconds(0.25f);
                    }

                }
                else
                {
                    chair.Kill();
                }
                yield return new WaitForSeconds(0.25f);
            }
            if (chair != null)
            {
                chair.Kill();
            }

        }

        object CanDismountEntity(BasePlayer player, BaseMountable entity)
        {
            if (cardMap.Count == 0 && chairsPreventingDismount.Count == 0) return null;//early out for maximum perf

            if (HasCard(player.userID, Card.Sit))
            {
                return false;
            }

            //cleanup dead chairs
            foreach (var chair in new HashSet<BaseMountable>(chairsPreventingDismount))
            {
                if (chair == null || chair.IsDestroyed)
                {
                    chairsPreventingDismount.Remove(chair);
                }
            }

            if (chairsPreventingDismount.Contains(entity))
            {
                return false;
            }

            return null;
        }


        string guid_BSOD = "guid_BSOD";
        string url_bsod = "http://na.fragmod.com/fragimages/dec2023/Bsodwindows10.png";
        void DoBSOD(BasePlayer player, bool playPublic)
        {
            if (player.net.connection == null) return;

            UI2.guids.Add(guid_BSOD);

            var elements = new CuiElementContainer();

            UI2.CreatePanel(elements, "Overlay", guid_BSOD, "1 1 1 1", UI2.vectorFullscreen, url_bsod, true, 0, 0, false);
            UI2.CreatePanel(elements, guid_BSOD, "blackpreloader", "0 0 0 1", UI2.vectorFullscreen, null, true, 0, 0, false);

            //add a way out for admins
            if (IsAdmin(player))
            {
                UI2.CreateButton(elements, guid_BSOD, "exitbutton", "0 0 0 0", "", 12, UI2.vectorFullscreen, "uipaybackcommand bsod");
            }

            //clear out any old UI that is getting updated
            foreach (var id in elements)
            {
                CuiHelper.DestroyUi(player, id.Name);
            }

            //send the ui updates
            if (elements.Count > 0)
            {
                CuiHelper.AddUi(player, elements);
            }

            timer.Once(0.2f, () =>
            {
                if (player != null)
                {
                    CuiHelper.DestroyUi(player, "blackpreloader");
                }
            });

            Worker.StaticStartCoroutine(PlayBSODSounds(player, playPublic));
        }


        //sounds like fapping
        string sound_bsod = "assets/bundled/prefabs/fx/impacts/physics/phys-impact-meat-hard.prefab";
        string sound_fall = "assets/bundled/prefabs/fx/player/fall-damage.prefab";
        IEnumerator PlayBSODSounds(BasePlayer player, bool playPublic)
        {
            float timeStart = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - timeStart < 30)
            {
                PlaySound(sound_fall, player, !playPublic);
                yield return new WaitForSeconds(0.2f);
            }
            yield return null;
        }

        [ConsoleCommand("uipaybackcommand")]
        void CommandUICommand(ConsoleSystem.Arg arg)
        {

            var player = arg.Connection?.player as BasePlayer;
            if (player != null)
            {
                if (arg.Args.Length >= 1)
                {
                    string command = arg.Args[0];
                    if (command == "bsod")
                    {
                        TakeCard(player.userID, Card.BSOD, arg.Args, null);
                    }
                }
            }
        }


        [ChatCommand("setdroppercent")]
        void Command_SetDropPercent(BasePlayer player, string cmd, string[] args)
        {
            if (!IsAdmin(player)) return;
            SetDropPercent(player, args);
        }

        [ConsoleCommand("setdroppercent")]
        void Console_CommandSetDropPercent(ConsoleSystem.Arg arg)
        {

            //Puts($"Console_CommandSetDropPercent: {arg} {arg.Connection} {arg.Connection?.player}");

            var player = arg.Connection?.player as BasePlayer;
            if (player != null)
            {
                if (!IsAdmin(player)) return;
            }

            SetDropPercent(player, arg.Args);
        }

        void SetDropPercent(BasePlayer player, string[] args)
        {
            if (args.Length == 1)
            {
                float p;
                if (float.TryParse(args[0], out p))
                {
                    if (p > 1)
                    {
                        p = p / 100f;//assume the user is trying to put 42 for 42% instead of 0.42
                    }
                    p = Mathf.Clamp(p, 0f, 1f);

                    paybackData.percent_butterfingers_dropchance = p;

                    if (player != null) PrintToPlayer(player, $"Set percent to drop weapon with butterfingers to : %{p * 100}");
                    //Puts($"Set percent to drop weapon with butterfingers to : %{p * 100}");

                }
                else
                {
                    if (player != null) PrintToPlayer(player, "Must enter a valid % value <0 - 100>");
                }
            }
            else
            {
                if (player != null) PrintToPlayer(player, "usage: setdroppercent <0-100>");
            }

        }


        Dictionary<ulong, float> playerMessageTimestamps = new Dictionary<ulong, float>();
        void SendPlayerLimitedMessage(ulong userID, string message, float rate = 5)
        {
            float ts = float.NegativeInfinity;
            if (playerMessageTimestamps.TryGetValue(userID, out ts))
            {
                if (Time.realtimeSinceStartup - ts > rate)
                {
                    ts = Time.realtimeSinceStartup;
                    playerMessageTimestamps[userID] = ts;
                    SendReply(BasePlayer.FindByID(userID), message);
                }
            }
            else
            {
                playerMessageTimestamps[userID] = ts;
                SendReply(BasePlayer.FindByID(userID), message);
            }
        }




        void AdminCommandToggleCard(BasePlayer admin, Card card, string[] args)
        {

            //| Special Commands
            if (card == Card.Bag)
            {
                ulong userID;
                if (!ulong.TryParse(args[0], out userID))
                {
                    PrintToPlayer(admin, "usage: /bag <steamid>");
                    return;
                }
                DoBagSearch(userID, args, admin);
            }

            //| Requires target commands
            if ( (args.Length == 0 && admin != null) || (args.Length == 1 && args[0].ToLower() == "true"))
            {

                var entity = RaycastFirstEntity(admin.eyes.HeadRay(), 100);
                if (entity is BasePlayer)
                {
                    var targetPlayer = entity as BasePlayer;
                    AdminToggleCard(admin, targetPlayer, card, args);
                }
                else
                {
                    //raycast target in front of you
                    //SendReply(admin, "did not find player from head raycast, either look at your target or do /<cardname> <playername>");
                    PrintToPlayer(admin, "did not find player from head raycast, either look at your target or do /<cardname> <playername>");
                }

                return;
            }

            if (args.Length >= 1)
            {
                var targetPlayer = GetPlayerWithName(args[0]);
                if (targetPlayer != null)
                {

                    if (args.Length == 2 && args[1] == "team")
                    {

                        var members = GetPlayerTeam(targetPlayer.userID);

                        string teamMatesPrintout = "";
                        foreach (var member in members)
                        {
                            BasePlayer p = BasePlayer.FindByID(member);
                            if (p != null && p.IsConnected)
                            {
                                teamMatesPrintout += p.displayName + " ";
                            }
                        }
                        PrintToPlayer(admin, $"Giving {card} to team {targetPlayer.displayName}  - {members.Count} team mates: {teamMatesPrintout}");

                        foreach (var member in members)
                        {
                            BasePlayer p = BasePlayer.FindByID(member);
                            if (p != null && p.IsConnected)
                            {
                                AdminToggleCard(admin, p, card, args);
                            }

                        }

                    }
                    else
                    {
                        AdminToggleCard(admin, targetPlayer, card, args);
                    }
                }
                else
                {

                    ulong userID;
                    if (ulong.TryParse(args[0], out userID))
                    {
                        targetPlayer = BasePlayer.FindByID(userID);
                        if (targetPlayer != null)
                        {

                            if (args.Length == 2 && args[1] == "team")
                            {

                                var members = GetPlayerTeam(targetPlayer.userID);
                                PrintToPlayer(admin, $"Giving {card} to team {targetPlayer.displayName} has {members.Count} team mates");
                                foreach (var member in members)
                                {
                                    BasePlayer p = BasePlayer.FindByID(member);
                                    if (p != null && p.IsConnected)
                                    {
                                        AdminToggleCard(admin, p, card, args);
                                    }

                                }

                            }
                            else
                            {
                                AdminToggleCard(admin, targetPlayer, card, args);
                            }


                            return;
                        }
                        else
                        {

                        }

                    }
                    else
                    {

                    }

                    PrintToPlayer(admin, $"could not find player : {args[0]}");
                }
            }
        }
        void AdminToggleCard(BasePlayer admin, BasePlayer targetPlayer, Card card, string[] args)
        {
            if (HasCard(targetPlayer.userID, card))
            {
                TakeCard(targetPlayer.userID, card, args, admin);
                PrintToPlayer(admin, $"Removed {card} from {targetPlayer.displayName}");
            }
            else
            {
                GiveCard(targetPlayer.userID, card, args, admin);
                PrintToPlayer(admin, $"Gave {card} to {targetPlayer.displayName}");
            }
        }



        [ConsoleCommand("payback")]
        void Console_Payback(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player != null)
            {
                if (!IsAdmin(player)) return;
            }
            CommandPayback(player, "", arg.Args);
        }

        [ChatCommand("payback")]
        void ChatCommandPayback(BasePlayer player, string cmd, string[] args)
        {
            if (!IsAdmin(player)) return;
            SendReply(player, "Check Payback output in F1 console!");
            CommandPayback(player, cmd, args);
        }
        void CommandPayback(BasePlayer player, string cmd, string[] args)
        {
            if (player != null && !IsAdmin(player)) return;
            // list all cards

            if (args == null || args.Length == 0)
            {
                DoPaybackPrintout(player, args);
                return;
            }

            List<string> argsList = new List<string>(args);
            if (argsList.FirstOrDefault(x => x == "show") != null)
            {
                string output = "Active Cards:\n";
                // show all active cards and players
                foreach (var userid in cardMap.Keys)
                {
                    var targetPlayer = BasePlayer.FindByID(userid);
                    string playername = "";
                    if (targetPlayer != null)
                    {
                        playername = targetPlayer.displayName;
                    }
                    HashSet<Card> cards = cardMap[userid];
                    output += $"{userid} : {playername}\n";
                    foreach (var card in cards)
                    {
                        output += $"\n{card.ToString()} : {UI2.ColorText(descriptions[card], "white")}";
                    }
                    output += "\n\n";
                }
                PrintToPlayer(player, output);

            }

            if (argsList.FirstOrDefault(x => x == "clear") != null)
            {

                foreach (var userid in new List<ulong>(cardMap.Keys))
                {
                    var targetPlayer = BasePlayer.FindByID(userid);
                    string playername = "";
                    if (targetPlayer != null)
                    {
                        playername = targetPlayer.displayName;
                    }

                    if (player != null)
                    {
                        HashSet<Card> cards = cardMap[userid];
                        foreach (var card in new HashSet<Card>(cards))
                        {
                            TakeCard(player, card);
                        }
                    }

                }

                cardMap.Clear();
                PrintToPlayer(player, "removed all cards from all players");
            }

        }
        void DoPaybackPrintout(BasePlayer player, string[] args)
        {


            Dictionary<Card, List<string>> cardToAliases = new Dictionary<Card, List<string>>();
            foreach (var alias in cardAliases.Keys)
            {
                Card c = cardAliases[alias];
                List<string> aliases;
                if (!cardToAliases.TryGetValue(c, out aliases))
                {
                    aliases = new List<string>();
                    cardToAliases[c] = aliases;
                }
                aliases.Add(alias);
            }

            var cards = Enum.GetValues(typeof(Card));
            string output = "";

            output += "\n" + "Add \"team\" after a command to apply the effect to target player's team as well as them.  Example: /butterfingers <steamid> team";
            output += "\n" + "/setdroppercent <1-100>% to change the chance butterfingers would drop";
            output += "\n" + $"admins require the permisison {permission_admin} to use these commands!";
            output += "\n" + $"use '/payback show' to see which players have which cards";
            output += "\n" + $"use '/payback clear' to remove all cards from all players.";
            output += "\n" + $"It is NOT necessary to remove effects from players when finished.";
            output += "\n" + $"Whitelist temp banned players with: bancheckexception <id>";

            output += "\n\nPayback 1 Commands:";

            foreach (Card card in cards)
            {
                string desc;
                descriptions.TryGetValue(card, out desc);

                List<string> aliases = cardToAliases[card];
                string aliasesTogether = "";
                aliases.ForEach(x => aliasesTogether += $"[ {UI2.ColorText(x, "yellow")} ] ");


                output += "\n\n" + $"{aliasesTogether}: {UI2.ColorText(desc, "white")}";
            }
            output += "\n\n" + UI2.ColorText("you can also use /listen to cycle between players who have recently used the microphone (great to bind!)", "white");

            if (Payback2 == null)
            {
                output += "\n\n " + UI2.ColorText("Payback2 Is Available Now! :", "red") + UI2.ColorText(" There's even more Payback available at https://payback.fragmod.com !", "white");
            }

            output += "\n\n " + UI2.ColorText("CHADIUS.IO :", "purple") + UI2.ColorText(" Are you protected by Rust's #1 AI Admin? Eliminate cheaters on autopilot at chadius.io", "white");


            PrintToPlayer(player, output);
        }

        //| ==============================================================
        //| PAYBACK OPTIONS
        //| ==============================================================

        Dictionary<ulong, HashSet<Card>> cardMap = new Dictionary<ulong, HashSet<Card>>();
        public bool HasAnyCard(ulong userID)
        {
            HashSet<Card> cards = null;
            if (cardMap.TryGetValue(userID, out cards))
            {
                if (cards.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }
        public bool HasCard(ulong userID, Card card)
        {
            HashSet<Card> cards;
            if (cardMap.TryGetValue(userID, out cards))
            {
                if (cards.Contains(card))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public void TakeCard(BasePlayer player, Card card, string[] args = null, BasePlayer admin = null)
        {
            TakeCard(player.userID, card, args, admin);
        }
        public void TakeCard(ulong userID, Card card, string[] args = null, BasePlayer admin = null)
        {
            HashSet<Card> cards;
            if (!cardMap.TryGetValue(userID, out cards))
            {
                cards = new HashSet<Card>();
                cardMap[userID] = cards;
            }
            cards.Remove(card);

            var player = BasePlayer.FindByID(userID);

            if (cards.Count == 0)
            {
                cardMap.Remove(userID);
            }
            if (card == Card.BSOD)
            {
                if (player != null)
                {
                    CuiHelper.DestroyUi(player, guid_BSOD);
                }
            }
            else if (card == Card.Sit)
            {
                if (player != null)
                {
                    DoSitCommand(player, admin, args);
                }
            }
            else if (card == Card.Shocker)
            {
                DoShocker(player, null, admin);
            }


            //| remove the card from any persistence
            if (cardsWhichCanPersist.Contains(card))
            {

                HashSet<Card> persistentCards = null;
                if (paybackData.persistentCommandMap.TryGetValue(player.userID, out persistentCards))
                {
                    persistentCards.Remove(card);
                }

            }

            
            //Puts($"Payback card {card} taken from {userID}");
        }



        HashSet<ulong> recentPlayerVoices = new HashSet<ulong>();
        Dictionary<ulong, float> recentPlayerVoiceTimestamps = new Dictionary<ulong, float>();

        Dictionary<ulong, HashSet<ulong>> listenedPlayersMap = new Dictionary<ulong, HashSet<ulong>>();

        Timer listenTimer = null;
        bool isListening = false;



        [ConsoleCommand("listen")]
        void ConsoleCommandListenNext(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player != null)
            {
                if (!IsAdmin(player)) return;
                CommandListenNext(player);
            }
        }

        [ChatCommand("listen")]
        void CommandListenNext(BasePlayer player)
        {
            if (!IsAdmin(player)) return;

            if (!isListening)
            {
                isListening = true;
                PrintToPlayer(player, $"Payback was not listening, its starting to listen now!");
                Subscribe("OnPlayerVoice");
                return;
            }
            else
            {
                if (listenTimer != null)
                {
                    listenTimer.Destroy();
                }
                listenTimer = timer.Once(60 * 15, () =>
                {
                    isListening = false;
                    this.Unsubscribe("OnPlayerVoice");
                    if (player != null)
                    {
                        PrintToPlayer(player, "Payback stopped listening for player voices.");
                    }
                });
            }

            HashSet<ulong> alreadyListenedToPlayers = null;
            if (!listenedPlayersMap.TryGetValue(player.userID, out alreadyListenedToPlayers))
            {
                alreadyListenedToPlayers = new HashSet<ulong>();
                listenedPlayersMap[player.userID] = alreadyListenedToPlayers;
            }

            recentPlayerVoices.RemoveWhere(x => Time.realtimeSinceStartup - recentPlayerVoiceTimestamps[x] > 60);
            List<ulong> voices = new List<ulong>(recentPlayerVoices);
            voices.RemoveAll(x => alreadyListenedToPlayers.Contains(x));
            voices.Sort((x, y) => recentPlayerVoiceTimestamps[y].CompareTo(recentPlayerVoiceTimestamps[x]));

            if (voices.Count == 0 && alreadyListenedToPlayers.Count > 0)
            {
                listenedPlayersMap[player.userID] = new HashSet<ulong>();
                if (recentPlayerVoices.Count > 0)
                {
                    CommandListenNext(player);
                    return;
                }
                else
                {
                    PrintToPlayer(player, "No one else has said anything in the last 60 seconds!");
                }
            }
            else
            {
                if (voices.Count > 0)
                {
                    ulong playerID = voices[0];

                    BasePlayer targetPlayer = BasePlayer.FindByID(playerID);

                    if (targetPlayer != null)
                    {
                        alreadyListenedToPlayers.Add(playerID);

                        player.SendConsoleCommand($"spectate {playerID}");
                        PrintToPlayer(player, $"Listening in on ... {targetPlayer.displayName} [{(int)(Time.realtimeSinceStartup - recentPlayerVoiceTimestamps[playerID])}s] \n{(alreadyListenedToPlayers.Count)} / {(alreadyListenedToPlayers.Count + voices.Count - 1)}");
                    }
                    else
                    {
                        recentPlayerVoices.Remove(playerID);
                        PrintToPlayer(player, $"Player went offline, try again...");
                    }

                }
                else
                {
                    PrintToPlayer(player, "No one has said anything in the last 60 seconds!");
                }
            }

        }




        //| ==============================================================
        //| OXIDE HOOKS
        //| ==============================================================

        private object OnPlayerViolation(BasePlayer player, AntiHackType type)
        {
            if (type == AntiHackType.InsideTerrain && HasAnyCard(player.userID)) return false;
            return null;
        }

        object OnPlayerDeath(BasePlayer player, HitInfo hitinfo)
        {
            if (HasAnyCard(player.userID))
            {
                if (HasCard(player.userID, Card.Pinyata))
                {
                    DoPinyataEffect(player);
                }
                if (player.isMounted)
                {
                    player.GetMounted().DismountPlayer(player, true);
                }

                //if (HasCard(player.userID, Card.Cowboy))
                //{
                //    DoCowboy(player, null);
                //}

                if (HasCard(player.userID, Card.NoRest))
                {
                    timer.Once(3f, () =>
                    {
                        if (player != null)
                        {
                            if (player.IsDead())
                            {
                                player.Respawn();
                            }
                        }
                    });
                }

                if (HasCard(player.userID, Card.Shocker))
                {
                    BaseEntity coil;
                    if (coilMap.TryGetValue(player.userID, out coil))
                    {
                        if (coil != null)
                        {
                            coil.SetParent(null, true, true);
                            var trigger = coil.GetComponentInChildren<TriggerBase>();
                            trigger.OnEntityLeave(player);
                        }
                    }
                }
            }

            return null;
        }



        float Random()
        {
            return UnityEngine.Random.Range(0f, 1f);
        }

        object OnPlayerVoice(BasePlayer player, Byte[] data)
        {
            recentPlayerVoices.Add(player.userID);
            recentPlayerVoiceTimestamps[player.userID] = Time.realtimeSinceStartup;
            return null;
        }


        object OnHealingItemUse(MedicalTool tool, BasePlayer player)
        {
            if (HasAnyCard(player.userID))
            {
                if (HasCard(player.userID, Card.DrNo))
                {
                    return false;
                }
            }
            return null;
        }
        object OnPlayerHealthChange(BasePlayer player, float oldValue, float newValue)
        {
            if (HasAnyCard(player.userID))
            {

                if (HasCard(player.userID, Card.DrNo))
                {
                    if (oldValue < newValue)
                    {

                        NextTick(() =>
                        {
                            if (player != null)
                            {
                                if (player.health > oldValue)
                                {
                                    player.health = oldValue;
                                    player.metabolism.pending_health.SetValue(0);
                                    player.SendNetworkUpdateImmediate();

                                }
                            }

                        });
                        return false;

                    }
                }

            }
            return null;
        }


        [ChatCommand("TestBanned")]
        void CommandTestBanned(BasePlayer player, string cmd, string[] args)
        {
            if (!IsAdmin(player)) return;
            CheckPublisherBan(player.name, player.userID, player.net.connection.ipaddress, "PublisherBanned");
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            //if (player != null)
            //{
            //    CheckPublisherBan(player.displayName, player.userID, player.net?.connection?.ipaddress, reason);
            //}
            if (player != null && HasCard(player.userID, Card.Shocker))
            {
                TakeCard(player.userID, Card.Shocker);
            }
            if (player != null && HasCard(player.userID, Card.Sit))
            {
                TakeCard(player, Card.Sit);
            }


            activePaybackUIS.Remove(player.userID);
        }

        //Interface.CallHook("OnPlayerBanned", connection, status.ToString());        
        void OnPlayerBanned(Network.Connection connection, string reason)
        {
            if (connection != null)
            {
                var player = connection.player as BasePlayer;
                if (player != null)
                {
                    OnPlayerBanned(player.displayName, player.userID, connection.ipaddress, reason, 0);
                }
            }
        }

        void OnPlayerBanned(string name, ulong id, string address, string reason, long param5)
        {
            //force the banned player dead and out of any chairs, else the model seems to stay behind
            var player = BasePlayer.FindByID(id);
            if (player != null)
            {
                if (sitChairMap.ContainsKey(id))
                {
                    if (player.GetMounted() != null)
                    {
                        player.GetMounted().DismountPlayer(player, true);
                        player.Die();
                    }
                }
            }
            CheckPublisherBan(name, id, address, reason);
        }
        void OnPlayerKicked(BasePlayer player, string reason, bool param3)
        {
            //force the banned player dead and out of any chairs, else the model seems to stay behind
            if (sitChairMap.ContainsKey(player.userID))
            {
                if (player.GetMounted() != null)
                {
                    player.GetMounted().DismountPlayer(player, true);
                    player.Die();
                }
            }

            CheckPublisherBan(player?.name, player.userID, player?.net?.connection?.ipaddress, reason);
        }

        List<string> banReasons = new List<string>() {
            "kickbanned",
            "cheat detected",
            "PublisherBanned",
        };

        void CheckPublisherBan(string name, ulong id, string address, string reason)
        {
            bool reasonIsPublisherBan = banReasons.Any(x => reason.ToLower().Contains(x.ToLower()));
            //Puts($"[Payback Detected Player Ban] {player.name} : {id} : {address} - {reason} | notifygb: {config.notify_game_ban} onlyteams: {config.notify_only_if_has_team}");
            if (reasonIsPublisherBan)
            {

                if (!config.notify_game_ban) return;

                string serverHostName = ConsoleSystem.Run(ConsoleSystem.Option.Server, $"hostname", new object[0]);

                var team = RelationshipManager.ServerInstance.FindPlayersTeam(id);

                var payload = new Dictionary<string, string>() {
                    { "Server", $"{serverHostName}" },
					//{ "Banned Player", $"{name} : {id}" },
					//{ "BM", $"https://www.battlemetrics.com/rcon/players?filter[search]={id}" },
					{ "Banned Player", $"[Info](https://steamid.uk/profile/{id}) - {id} : [{TryGetDisplayName(id)}](https://www.battlemetrics.com/rcon/players?filter[search]={id})" },

                };

                int number = 1;
                string playerOutput = "";

                if (team != null)
                {
                    foreach (var userID in new HashSet<ulong>(team.members))
                    {
                        if (userID != id)
                        {
                            //playerOutput += $"\n{number} -> {userID} : {TryGetDisplayName(userID)}";

                            if (config.notify_ban_include_bm)
                            {
                                //playerOutput += $"\nhttps://www.battlemetrics.com/rcon/players?filter[search]={userID}";
                                playerOutput += $"\n[{userID}](https://steamid.uk/profile/{userID}) : [{TryGetDisplayName(userID)}](https://www.battlemetrics.com/rcon/players?filter[search]={userID})";

                            }

                            number++;
                        }
                    }
                }
                if (number == 1)
                {
                    if (config.notify_only_if_has_team)
                    {
                        return;
                    }
                }
                else
                {
                    payload.Add($"teaminfo", playerOutput);
                }

                //Puts($"[Payback Detected Player Ban] sending to webhook");

                SendToDiscordWebhook(payload, "GAME BAN");
            }
        }


        string TryGetDisplayName(ulong userID)
        {
            return covalence.Players.FindPlayerById(userID.ToString())?.Name;
        }


        [ConsoleCommand("bancheckexception")]
        void CommandBanCheckException(ConsoleSystem.Arg arg)
        {

            var player = arg.Connection?.player as BasePlayer;
            if (player != null)
            {
                if (!IsAdmin(player)) return;
            }
            if (arg.Args.Length >= 1)
            {
                ulong id;
                if (ulong.TryParse(arg.Args[0], out id))
                {
                    paybackData.bancheck_exceptions.Add(id);
                    PrintToPlayer(player, $"Added bancheck exception: {id} total: {paybackData.bancheck_exceptions.Count}");
                }
                else
                {
                    PrintToPlayer(player, $"could not parse id");
                }
            }
            else
            {
                PrintToPlayer(player, $"not enough args");
            }
        }


        bool test_connect = false;
        [ChatCommand("testconnect")]
        void CommandTestConnect(BasePlayer player)
        {
            if (!IsAdmin(player)) return;

            test_connect = true;
            OnPlayerConnected(player);
            test_connect = false;
        }

        void OnPlayerConnected(BasePlayer player)
        {
            
            //| re-apply saved cards if we're using persistent data
            if (config.persistentCommands)
            {

                HashSet<Card> persistentCards = null;
                if (paybackData.persistentCommandMap.TryGetValue(player.userID, out persistentCards))
                {
                    foreach (var card in persistentCards)
                    {
                        timer.Once(2f, () => {
                            if (player == null) return;
                            GiveCard(player.userID, card, new string[0], null);
                        });
                    }
                }
                
            }

            if (config.enabled_nexus_gamebancheck)
            {
                if (paybackData.bancheck_exceptions.Contains(player.userID)) return;

                string url = $"https://www.nexusonline.co.uk/bans/profile/?id={player.userID}";
                if (test_connect)
                {
                    url = $"https://www.nexusonline.co.uk/bans/profile/?id=76561199128818380";
                }
                //string url = $"https://www.nexusonline.co.uk/bans/profile/?id=76561199128818380";

                webrequest.Enqueue(url, "", (code, response) =>
                {
                    if (code == 200)
                    {
                        if (response == null) return;

                        //Puts(response);
                        if (response.Contains("IS CURRENTLY GAME BANNED".ToLower()))
                        {

                            Regex regex = new Regex($"<a.+?<a.+?\">(.+?)<\\/a><\\/blockquote>");
                            var match = regex.Match(response);
                            DateTime date = DateTime.Now;
                            foreach (var g in match.Groups)
                            {
                                if (g == null) continue;
                                //Puts($"G: {g}");
                                if (DateTime.TryParse(g.ToString(), out date))
                                {
                                    //Puts($"Parsed date: {date}");
                                    break;
                                }
                            }

                            if (date != null && date.AddDays(config.nexus_ban_days) > DateTime.Now)
                            {
                                string serverHostName = ConsoleSystem.Run(ConsoleSystem.Option.Server, $"hostname", new object[0]);
                                if (player == null) return;
                                //Puts($"Detected game-banned user: {player.userID} {player.displayName}");


                                if (config.autoban_tempbans)
                                {
                                    player.IPlayer.Ban($"Auto-Banned By Payback | CODE 69420 | {config.autoban_banmessage}");
                                    SendToDiscordWebhook(new Dictionary<string, string>() {
                                        { "Server", $"{serverHostName}" },
										//{ "Url", $"{url}" },
										//{ "Player", $"{player.displayName} : {player.userID}" },
										//{ "BM", $"https://www.battlemetrics.com/rcon/players?filter[search]={player.userID}" },
										{ "Player", $"[Info](https://steamid.uk/profile/{player.userID}) - {player.userID} : [{TryGetDisplayName(player.userID)}](https://www.battlemetrics.com/rcon/players?filter[search]={player.userID})" },
                                    }, $"TEMP BAN DETECTED - AUTO-BANNED");
                                }
                                else
                                {
                                    SendToDiscordWebhook(new Dictionary<string, string>() {
                                        { "Server", $"{serverHostName}" },
										//{ "Url", $"{url}" },
										//{ "Player", $"{player.displayName} : {player.userID}" },
										//{ "BM", $"https://www.battlemetrics.com/rcon/players?filter[search]={player.userID}" },
										{ "Player", $"[Info](https://steamid.uk/profile/{player.userID}) - {player.userID} : [{TryGetDisplayName(player.userID)}](https://www.battlemetrics.com/rcon/players?filter[search]={player.userID})" },

                                    });
                                }
                            }

                        }

                    }
                    else
                    {
                        //PrintError($"nexusonline HTTP CODE: {code}");
                    }
                }, this);
            }

        }

        void SendToDiscordWebhook(Dictionary<string, string> messageData, string title = "TEMP GAME BAN DETECTED")
        {
            if (config.webhooks == null || config.webhooks.Count == 0)
            {
                Puts($"Could not send Discord Webhook: webhook not configured");
                return;
            }

            string discordEmbedTitle = title;


            List<object> fields = new List<object>();

            foreach (var key in messageData.Keys)
            {
                string data = messageData[key];
                fields.Add(new { name = $"{key}", value = $"{data}", inline = false });
            }

            object f = fields.ToArray();


            foreach (var webhook in config.webhooks)
            {
                SendWebhook(webhook, (string)discordEmbedTitle, f);
            }
        }

        private void SendWebhook(string WebhookUrl, string title, object fields)
        {
            if (string.IsNullOrEmpty(WebhookUrl))
            {
                Puts("Error: Someone tried to use a command but the WebhookUrl is not set!");
                return;
            }

            //test
            string json = new SendEmbedMessage(13964554, title, fields).ToJson();

            webrequest.Enqueue(WebhookUrl, json, (code, response) =>
            {
                if (code == 429)
                {
                    Puts("Sending too many requests, please wait");
                    return;
                }

                if (code != 204)
                {
                    Puts(code.ToString());
                }
                if (code == 400)
                {
                    Puts(response + "\n\n" + json);
                }
            }, this, Oxide.Core.Libraries.RequestMethod.POST, new Dictionary<string, string> { ["Content-Type"] = "application/json" });
        }

        private class SendEmbedMessage
        {
            public SendEmbedMessage(int EmbedColour, string discordMessage, object _fields)
            {
                object embed = new[]
                {
                    new
                    {
                        title = discordMessage,
                        fields = _fields,
                        color = EmbedColour,
                        thumbnail = new Dictionary<object, object>() { { "url", "http://na.fragmod.com/fragimages/dec2023/payback_hammer_icon_1.png" } },
                    }
                };
                Embeds = embed;
            }

            [JsonProperty("embeds")] public object Embeds { get; set; }

            public string ToJson() => JsonConvert.SerializeObject(this);
        }


        private void OnEntityTakeDamage(BaseEntity entity, HitInfo hitinfo)
        {
            if (entity == null || hitinfo == null) return;

            if (chickens.Count > 0)
            {
                if (chickens.Contains(entity))
                {
                    hitinfo.damageTypes.Clear();
                    hitinfo.DoHitEffects = false;
                    return;
                }
            }

            if (cardMap.Count == 0) return;//early out for maximum perf


            if (hitinfo != null)
            {

                if (protectedStashes.Contains(entity))
                {
                    hitinfo.damageTypes.Clear();
                    hitinfo.DoHitEffects = false;
                }
                if (sitChairMap.Values.Contains(entity))
                {
                    hitinfo.damageTypes.Clear();
                    hitinfo.DoHitEffects = false;
                }


                var player = entity as BasePlayer;
                var attacker = hitinfo.InitiatorPlayer;

                var damageType = hitinfo.damageTypes.GetMajorityDamageType();
                if (player != null && HasCard(player.userID, Card.Masochist) && hitinfo.damageTypes != null && (damageType == DamageType.Suicide || damageType == DamageType.Fall))
                {
                    hitinfo.damageTypes.Clear();
                    hitinfo.DoHitEffects = false;
                }

                if (player != null && HasCard(player.userID, Card.Shocker) && hitinfo.damageTypes != null && hitinfo.damageTypes.GetMajorityDamageType() == DamageType.ElectricShock)
                {
                    DoScreaming(player, null, false, true);
                }

                //flamethrower
                if (hitinfo.WeaponPrefab != null && hitinfo.WeaponPrefab.prefabID == 3717106868 && entity is BasePlayer && attacker != null && player != null)
                {
                    if (hitinfo.InitiatorPlayer != null)
                    {
                        if (HasCard(hitinfo.InitiatorPlayer.userID, Card.Burn))
                        {

                            if (hitinfo.InitiatorPlayer == entity)
                            {
                                hitinfo.damageTypes.Scale(DamageType.Heat, 0f);//prevent heat damage to self
                            }
                            else
                            {

                                DoScreaming(player, attacker, true);

                                FlameThrower t = attacker.GetHeldEntity() as FlameThrower;
                                if (t == null) return;

                                t.ammo = t.maxAmmo;

                                var ammoItem = t.GetAmmo();
                                if (ammoItem != null)
                                {
                                    ammoItem.amount = 100;
                                    ammoItem.MarkDirty();
                                }
                                t.SendNetworkUpdate();

                            }
                        }
                    }

                }



                //PrintToChat($"OnEntityTakeDamage {player} attacker {hitinfo?.InitiatorPlayer}");

                if (attacker != null && HasAnyCard(attacker.userID))
                {
                    var members = GetPlayerTeam(attacker.userID);
                    members.Remove(attacker.userID);

                    bool friendlyFire = false;
                    if (player != null)
                    {
                        friendlyFire = members.Contains(player.userID);
                    }

                    bool isSuicide = hitinfo.damageTypes.GetMajorityDamageType() == Rust.DamageType.Suicide;

                    //no suicide!
                    //if (isSuicide && HasCard(player.userID, Card.Sit))
                    //{
                    //    hitinfo.damageTypes.Clear();
                    //    hitinfo.DoHitEffects = false;
                    //}

                    if (player != null && attacker != null && attacker != player)
                    {


                        if (HasCard(attacker.userID, Card.InstantKarma))
                        {

                            if (!friendlyFire)
                            {

                                float newHealth = attacker.health - hitinfo.damageTypes.Total() * 0.35f;
                                if (newHealth < 5)
                                {
                                    attacker.Die();
                                }
                                else
                                {
                                    attacker.SetHealth(newHealth);
                                    attacker.metabolism.SendChangesToClient();
                                    attacker.SendNetworkUpdateImmediate();
                                    //PlaySound("assets/bundled/prefabs/fx/headshot.prefab", attacker, false);
                                    PlaySound("assets/bundled/prefabs/fx/headshot_2d.prefab", attacker, true);
                                }

                                hitinfo.damageTypes.Clear();
                                hitinfo.DoHitEffects = false;

                            }

                        }

                        if (HasCard(attacker.userID, Card.Butterfingers) && !friendlyFire)
                        {
                            var roll = UnityEngine.Random.Range(0f, 1f);


                            //manipulate target based on clip size

                            BaseProjectile weapon = hitinfo.Weapon as BaseProjectile;
                            float magazineMultiplier = 1;
                            if (weapon != null)
                            {
                                magazineMultiplier = Mathf.Clamp(20f / weapon.primaryMagazine.capacity, 1, 10);
                            }


                            if (roll < paybackData.percent_butterfingers_dropchance * magazineMultiplier)
                            {
                                //chance to drop weapon.

                                var heldEntity = attacker.GetHeldEntity();


                                if (heldEntity != null)
                                {
                                    var item = heldEntity.GetItem();
                                    if (item != null)
                                    {
                                        var droppedEntity = item.Drop(attacker.eyes.HeadRay().origin, attacker.eyes.HeadRay().direction * 5 + Vector3.up * 5);
                                        droppedEntity.transform.LookAt(attacker.eyes.HeadRay().origin + Quaternion.Euler(0, UnityEngine.Random.Range(-90, 90), UnityEngine.Random.Range(-90, 90)) * attacker.eyes.HeadRay().direction * 2);
                                        var body = droppedEntity.GetComponentInChildren<Rigidbody>();
                                        if (body != null)
                                        {
                                            float power = 1;
                                            body.AddForceAtPosition(attacker.eyes.HeadRay().direction * power, droppedEntity.transform.position + Vector3.up * 10f);
                                        }
                                        droppedEntity.SendNetworkUpdate();

                                    }
                                }



                            }
                        }

                    }

                    if (HasCard(attacker.userID, Card.Pacifism) && attacker != player && player != null)
                    {

                        if (!friendlyFire)
                        {
                            hitinfo.damageTypes.Clear();
                            hitinfo.DoHitEffects = false;

                            if (config.notifyCheaterAttacking && !silentPacifism)
                            {
                                SendPlayerLimitedMessage(player.userID, $"You are being attacked by [{UI2.ColorText(attacker.displayName, "yellow")}] a known cheater!\n{UI2.ColorText("Tommygun's Payback Plugin", "#7A2E30")} has prevented all damage to you.");
                            }
                            //Puts($"{player.displayName} attacked by [{attacker.displayName}] a known cheater! Tommygun's Payback has prevented all damage from the cheater");

                        }

                    }


                    //prevent damage to non-player entities
                    if (HasCard(attacker.userID, Card.Dud) && player == null)
                    {
                        hitinfo.damageTypes.Clear();
                        //hitinfo.DoHitEffects = false;
                        hitinfo.gatherScale = 0;
                    }


                }

                //make landmines from dog doo kill the player
                if (player != null && HasAnyCard(player.userID))
                {
                    if (hitinfo.Initiator is Landmine && player != null && HasCard(player.userID, Card.DogDoo))
                    {
                        hitinfo.damageTypes.ScaleAll(10);
                    }
                }


            }
        }

        void DoScreaming(BasePlayer player, BasePlayer attacker, bool fire = false, bool screamSourceIsTarget = false)
        {
            if (!currentlyScreamingPlayers.Contains(player.userID))
            {

                PlayGesture(player, "friendly");
                timer.Once(2f, () =>
                {
                    if (player != null)
                        PlayGesture(player, "friendly");
                });
                timer.Once(4f, () =>
                {
                    if (player != null)
                        PlayGesture(player, "friendly");
                });

                if (screamSourceIsTarget)
                {
                    PlaySound(sound_scream, player, false);
                }
                else
                {
                    PlaySound(sound_scream, attacker, false);
                }

                if (fire)
                {
                    PlaySound(effect_onfire, player, false);
                }

                currentlyScreamingPlayers.Add(player.userID);

                timer.Once(5f, () =>
                {
                    if (player != null)
                        currentlyScreamingPlayers.Remove(player.userID);
                });
            }
        }

        HashSet<BaseEntity> protectedStashes = new HashSet<BaseEntity>();
        HashSet<Landmine> landmines = new HashSet<Landmine>();
        object CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity is Landmine)
            {
                if (landmines.Contains(entity)) {
                    return false;
                }
            }
            return null;
        }
        void OnStashExposed(StashContainer stash, BasePlayer player)
        {
            if (HasCard(player.userID, Card.DogDoo))
            {
                landmines.RemoveWhere(x => x == null || x.IsDead() || x.IsDestroyed);
                if (protectedStashes.Contains(stash)) return;//once only.
                protectedStashes.Add(stash);
                timer.Once(2f, () =>
                {

                    if (player != null)
                    {
                        var entity = GameManager.server.CreateEntity("assets/prefabs/deployable/landmine/landmine.prefab", player.transform.position);
                        Landmine landmine = entity as Landmine;
                        entity.Spawn();
                        landmine.Arm();
                        landmine.SendNetworkUpdateImmediate();
                        landmines.Add(landmine);
                    }

                });

                timer.Once(120f, () =>
                {

                    if (stash != null && !stash.IsDead())
                    {
                        protectedStashes.Remove(stash);
                    }

                });

            }
        }

        //| ==============================================================
        //| UTILITY
        //| ==============================================================

        public BasePlayer GetPlayerWithName(string displayName)
        {
            foreach (var p in BasePlayer.allPlayerList)
            {
                if (p.displayName.ToLower().Contains(displayName.ToLower()))
                {
                    return p;
                }
            }
            return null;
        }
        BaseEntity RaycastFirstEntity(Ray ray, float distance)
        {
            RaycastHit hit;
            if (Physics.Raycast(ray.origin, ray.direction, out hit, distance))
            {
                return hit.GetEntity();
            }
            return null;
        }


        //| ==============================================================
        //| INIT
        //| ==============================================================
        void Initialize()
        {
            //Puts("Tommygun's Payback Initialized");

            Unsubscribe("OnPlayerVoice");
            Unsubscribe($"OnEntityKill");

            LoadData();

            permission.RegisterPermission(permission_admin, this);

            var cards = Enum.GetValues(typeof(Card));

            foreach (Card card in cards)
            {
                cardAliases[card.ToString().ToLower()] = card;
            }
            foreach (var alias in cardAliases.Keys)
            {
                cmd.AddChatCommand(alias, this, nameof(GenericChatCommand));
                cmd.AddConsoleCommand(alias, this, nameof(GenericConsoleCommand));
            }

            timer.Once(0.5f, () =>
            {
                if (Payback2 == null)
                {
                    Puts($"\nCheck out the sequel to this plugin Payback 2! there's a lot more Payback to be had at https://payback.fragmod.com !\n");
                }

                Worker.StaticStartCoroutine(AdminVisualizationCo());

                ImageLibrary?.CallHook("AddImage", url_payback_logo, url_payback_logo, (ulong)0);
                ImageLibrary?.CallHook("AddImage", url_payback_logo_raised, url_payback_logo_raised, (ulong)0);

            });
            //Puts("Tommygun's Payback Finished Initialization");

        }
        void GenericChatCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!IsAdmin(player)) return;
            string argsTogether = "";
            foreach (var arg in args)
            {
                argsTogether += arg + " ";
            }
            //SendReply(player, $"cmd: {cmd} args {argsTogether}");
            Card card;
            if (cardAliases.TryGetValue(cmd.ToLower(), out card))
            {
                AdminCommandToggleCard(player, card, args);
            }
        }
        void GenericConsoleCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;

            if (player != null)
            {
                if (!IsAdmin(player)) return;
            }
            if (arg == null) return;
            if (arg.cmd == null) return;

            string argsTogether = "";

            if (arg.Args != null)
            {
                foreach (var param in arg.Args)
                {
                    argsTogether += param + " ";
                }
            }

            string cmd = string.Empty;
            if (arg.cmd.Name != null)
            {
                cmd = arg.cmd.Name;
            }

            Card card;
            if (cardAliases.TryGetValue(cmd.ToLower(), out card))
            {
                if (arg.Args == null)
                {
                    arg.Args = new string[0];
                }
                AdminCommandToggleCard(player, card, arg.Args);
            }
        }
        void OnServerInitialized(bool serverIsNOTinitialized)
        {
            bool serverHasInitialized = !serverIsNOTinitialized;
            Initialize();


            //| preload images
            timer.Once(10, () => {

                if (ImageLibrary == null)
                {
                    Puts($"[Payback] (Optional) Please install the ImageLibrary plugin for optimal performance in Payback [https://umod.org/plugins/image-library]");
                }
            });
        }






        //| ==============================================================
        //| ViewInventory - Copied from Whispers88 and modified here
        //| ==============================================================
        private static List<string> _viewInventoryHooks = new List<string> { "OnLootEntityEnd", "CanMoveItem", "OnEntityDeath" };

        void ViewTargetPlayerInventory(BasePlayer target, BasePlayer admin)
        {
            if (admin == null) return;
            if (admin.IsSpectating())
            {
                PrintToPlayer(admin, $"{UI2.ColorText($"[PAYBACK WARNING] ", "yellow")} : {UI2.ColorText($"cannot open target's inventory while spectating! you must respawn", "white")}");
                return;
            }
            PrintToPlayer(admin, $"{UI2.ColorText($"[PAYBACK WARNING] ", "yellow")} : {UI2.ColorText($"you must exit the F1 console immediately after using the command to view inventory", "white")}");

            ViewInvCmd(admin.IPlayer, "ViewInvCmd", new string[] { $"{target.userID}" });
        }


        #region ViewInventoryCommands
        private void ViewInvCmd(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;

            //if (!HasPerm(player.UserIDString, permission_admin))
            //{
            //    ChatMessage(iplayer, GetLang("NoPerms"));
            //    return;
            //}


            if (args.Length == 0 || string.IsNullOrEmpty(args[0]))
            {
                RaycastHit hitinfo;
                if (!Physics.Raycast(player.eyes.HeadRay(), out hitinfo, 3f, (int)Layers.Server.Players))
                {
                    ChatMessage(iplayer, "NoPlayersFoundRayCast");
                    return;
                }
                BasePlayer targetplayerhit = hitinfo.GetEntity().ToPlayer();
                if (targetplayerhit == null)
                {
                    ChatMessage(iplayer, "NoPlayersFoundRayCast");
                    return;
                }
                //ChatMessage(iplayer, "ViewingPLayer", targetplayerhit.displayName);
                ViewInventory(player, targetplayerhit);
                return;
            }
            IPlayer target = FindPlayer(args[0]);
            if (target == null)
            {
                //ChatMessage(iplayer, "NoPlayersFound", args[0]);
                return;
            }
            BasePlayer targetplayer = target.Object as BasePlayer;
            if (targetplayer == null)
            {
                //ChatMessage(iplayer, "NoPlayersFound", args[0]);
                return;
            }
            //ChatMessage(iplayer, "ViewingPLayer", targetplayer.displayName);
            ViewInventory(player, targetplayer);
        }

        #endregion Commands

        #region Methods
        private List<LootableCorpse> _viewingcorpse = new List<LootableCorpse>();
        private void ViewInventory(BasePlayer player, BasePlayer targetplayer)
        {
            if (_viewingcorpse.Count == 0)
                SubscribeToHooks();

            player.EndLooting();

            var corpse = GetLootableCorpse(targetplayer.displayName);
            corpse.SendAsSnapshot(player.Connection);

            timer.Once(1f, () =>
            {
                StartLooting(player, targetplayer, corpse);
            });
        }

        LootableCorpse GetLootableCorpse(string title = "")
        {
            LootableCorpse corpse = GameManager.server.CreateEntity(StringPool.Get(2604534927), Vector3.zero) as LootableCorpse;
            corpse.CancelInvoke("RemoveCorpse");
            corpse.syncPosition = false;
            corpse.limitNetworking = true;
            //corpse.playerName = targetplayer.displayName;
            corpse.playerName = title;
            corpse.playerSteamID = 0;
            corpse.enableSaving = false;
            corpse.Spawn();
            corpse.SetFlag(BaseEntity.Flags.Locked, true);
            Buoyancy bouyancy;
            if (corpse.TryGetComponent<Buoyancy>(out bouyancy))
            {
                UnityEngine.Object.Destroy(bouyancy);
            }
            Rigidbody ridgidbody;
            if (corpse.TryGetComponent<Rigidbody>(out ridgidbody))
            {
                UnityEngine.Object.Destroy(ridgidbody);
            }
            return corpse;
        }

        private void StartLooting(BasePlayer player, BasePlayer targetplayer, LootableCorpse corpse)
        {
            player.inventory.loot.AddContainer(targetplayer.inventory.containerMain);
            player.inventory.loot.AddContainer(targetplayer.inventory.containerWear);
            player.inventory.loot.AddContainer(targetplayer.inventory.containerBelt);
            player.inventory.loot.entitySource = corpse;
            player.inventory.loot.PositionChecks = false;
            player.inventory.loot.MarkDirty();
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer<string>(null, player, "RPC_OpenLootPanel", "player_corpse");
            _viewingcorpse.Add(corpse);
        }
        private void StartLootingContainer(BasePlayer player, ItemContainer container, LootableCorpse corpse)
        {
            player.inventory.loot.AddContainer(container);
            player.inventory.loot.entitySource = corpse;
            player.inventory.loot.PositionChecks = false;
            player.inventory.loot.MarkDirty();
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer<string>(null, player, "RPC_OpenLootPanel", "player_corpse");
            _viewingcorpse.Add(corpse);
        }

        #endregion Methods

        #region Hooks
        private void OnLootEntityEnd(BasePlayer player, LootableCorpse corpse)
        {
            if (!_viewingcorpse.Contains(corpse)) return;

            _viewingcorpse.Remove(corpse);
            if (corpse != null)
                corpse.Kill();

            if (_viewingcorpse.Count == 0)
                UnSubscribeFromHooks();

        }


        void OnEntityDeath(LootableCorpse corpse, HitInfo info)
        {
            if (!_viewingcorpse.Contains(corpse)) return;
            _viewingcorpse.Remove(corpse);
            if (corpse != null)
                corpse.Kill();
            if (_viewingcorpse.Count == 0)
                UnSubscribeFromHooks();
        }
        #endregion Hooks

        #region Helpers

        private IPlayer FindPlayer(string nameOrId)
        {
            return BasePlayer.activePlayerList.FirstOrDefault(x => x.UserIDString == nameOrId || x.displayName.Contains(nameOrId)).IPlayer;
        }

        private bool HasPerm(string id, string perm) => permission.UserHasPermission(id, perm);

        private string GetLang(string langKey, string playerId = null, params object[] args) => string.Format(lang.GetMessage(langKey, this, playerId), args);
        private void ChatMessage(IPlayer player, string langKey, params object[] args)
        {
            if (player.IsConnected) player.Message(GetLang(langKey, player.Id, args));
        }

        private void UnSubscribeFromHooks()
        {
            foreach (var hook in _viewInventoryHooks)
                Unsubscribe(hook);
        }

        private void SubscribeToHooks()
        {
            foreach (var hook in _viewInventoryHooks)
                Subscribe(hook);
        }
        #endregion

        //| ==============================================================
        //| DATA
        //| ==============================================================


        string filename_data
        {
            get
            {
                return $"Payback/Payback.dat";
            }
        }


        DynamicConfigFile file_payback_data;

        public PaybackData paybackData = new PaybackData();

        public class PaybackData
        {
            public float percent_butterfingers_dropchance = 0.3f;
            public HashSet<ulong> bancheck_exceptions = new HashSet<ulong>();

            public Dictionary<ulong, HashSet<Card>> persistentCommandMap = new Dictionary<ulong, HashSet<Card>>();
        }

        void Unload()
        {
            //Puts("Unload Tommygun's Payback");

            SaveData();

            Worker.GetSingleton()?.StopAllCoroutines();
            GameObject.Destroy(Worker.GetSingleton());

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                UI2.ClearUI(player);
            }

        }



        private void SaveData()
        {
            //| WRITE SERVER FILE
            file_payback_data.WriteObject(paybackData);
        }
        private void LoadData()
        {
            //Puts("Load Data");

            ReadDataIntoDynamicConfigFiles();
            LoadFromDynamicConfigFiles();
        }
        void ReadDataIntoDynamicConfigFiles()
        {
            file_payback_data = Interface.Oxide.DataFileSystem.GetFile(filename_data);
        }
        void LoadFromDynamicConfigFiles()
        {
            try
            {
                paybackData = file_payback_data.ReadObject<PaybackData>();

                if (paybackData.persistentCommandMap == null)
                {
                    paybackData.persistentCommandMap = new Dictionary<ulong, HashSet<Card>>();
                }
                
            }
            catch (Exception e)
            {
                paybackData = new PaybackData();
                //Puts($"Creating new data {e}");
            }
            Worker.StaticStartCoroutine(ApplyPersistentCommandsFromReloadCo());

        }

        IEnumerator ApplyPersistentCommandsFromReloadCo()
        {
            if (!config.persistentCommands)
            {
                yield break;
            }
            
            yield return null;

            foreach (var player in BasePlayer.activePlayerList.ToArray())
            {
                if (player != null)
                {

                    HashSet<Card> persistentCards = null;
                    if (paybackData.persistentCommandMap.TryGetValue(player.userID, out persistentCards))
                    {
                        foreach (var card in persistentCards)
                        {
                            GiveCard(player.userID, card, new string[0], null);
                        }
                    }
                    yield return null;
                }
            }

        }

        public const string permission_admin = "payback.admin";

        public bool IsAdmin(BasePlayer player)
        {
            if (permission.UserHasPermission(player.Connection.userid.ToString(), permission_admin))
            {
                return true;
            }
            return false;
        }


        //| ==============================================================
        //| UTILITIES
        //| ==============================================================
        private string GetImage(string url)
        {
            if (ImageLibrary == null) return url;
            var obj = ImageLibrary?.Call("GetImage", url);
            if (obj == null || obj.ToString() == null) return url;
            return obj?.ToString();
        }

        void SetDespawnDuration(DroppedItem dropped, float seconds)
        {
            dropped.Invoke(new Action(dropped.IdleDestroy), seconds);//prevent dropped item from despawn
        }
        void DestroyGroundCheck(BaseEntity entity)
        {
            GameObject.DestroyImmediate(entity.GetComponentInChildren<DestroyOnGroundMissing>());
            GameObject.DestroyImmediate(entity.GetComponentInChildren<GroundWatch>());
        }

        [ChatCommand("sound")]
        void SoundCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!IsAdmin(player)) return;

            if (args.Length == 0)
            {
                SendReply(player, "/sound <asset>");
                return;
            }
            for (int i = 0; i < args.Length; i++)
            {
                string sound = args[i];
                PlaySound(sound, player, false);
            }
        }

        void PrintToPlayer(BasePlayer player, string text)
        {
            if (player == null)
            {
                Puts($"{text}");
                return;
            }
            //SendReply(player, text);
            player.SendConsoleCommand($"echo {text}");
        }
        public HashSet<ulong> GetPlayerTeam(ulong userID)
        {
            BasePlayer player = BasePlayer.FindByID(userID);
            if (player == null)
            {
                return new HashSet<ulong>();
            }

            RelationshipManager.PlayerTeam existingTeam = RelationshipManager.ServerInstance.FindPlayersTeam(player.userID);
            if (existingTeam != null)
            {
                return new HashSet<ulong>(existingTeam.members);
            }
            return new HashSet<ulong>() { userID };
        }

        public void PlaySound(List<string> effects, BasePlayer player, Vector3 worldPosition, bool playlocal = true)
        {
            if (player == null) return;//ai
            foreach (var effect in effects)
            {
                //var sound = new Effect(effect, player, 0, localPosition, localPosition.normalized);
                var sound = new Effect(effect, worldPosition, Vector3.up);
                if (playlocal)
                {
                    EffectNetwork.Send(sound, player.net.connection);
                }
                else
                {
                    EffectNetwork.Send(sound);
                }
            }
        }

        public void PlaySound(List<string> effects, BasePlayer player, bool playlocal = true)
        {
            if (player == null) return;//ai
            foreach (var effect in effects)
            {
                var sound = new Effect(effect, player, 0, Vector3.zero + Vector3.up * 0.5f, Vector3.forward);
                if (playlocal)
                {
                    EffectNetwork.Send(sound, player.net.connection);
                }
                else
                {
                    EffectNetwork.Send(sound);
                }
            }
        }
        public void PlaySound(string effect, ListHashSet<BasePlayer> players, bool playlocal = true)
        {
            //all players
            foreach (var player in players)
            {
                PlaySound(effect, player, playlocal);
            }
        }

        bool test = false;

        public void PlaySound(string effect, BasePlayer player, bool playlocal = true, Vector3 posLocal = default(Vector3))
        {
            if (player == null) return;//ai

            var sound = new Effect(effect, player, 0, Vector3.zero, Vector3.forward);

            if (posLocal != Vector3.zero)
            {
                sound = new Effect(effect, player.transform.position + posLocal, Vector3.forward);
            }


            if (playlocal)
            {
                EffectNetwork.Send(sound, player.net.connection);
            }
            else
            {
                EffectNetwork.Send(sound);
            }
        }

        //[ChatCommand("gesture")]
        //void CommandGesture(BasePlayer player)
        //{
        //    if (!IsAdmin(player)) return;
        //    //foreach (var g in player.gestureList.AllGestures)
        //    //{
        //    //    PrintToChat($"{g.convarName} - {g.gestureCommand} - {g.gestureName} : {g.gestureId}");
        //    //}
        //    PlayGesture(player, "friendly");
        //}

        public void PlayGesture(BasePlayer target, string gestureName, bool canCancel = false)
        {
            if (target == null) return;
            if (GestureCollection.Instance == null) return;
            var gesture = GestureCollection.Instance.StringToGesture(gestureName);
            if (gesture == null)
            {
                return;
            }
            bool saveCanCancel = gesture.canCancel;
            gesture.canCancel = canCancel;
            //target.SendMessage("Server_StartGesture", gesture);
            target.Server_StartGesture(gesture, BasePlayer.GestureStartSource.ServerAction);
            gesture.canCancel = saveCanCancel;
        }
        public class Worker : MonoBehaviour
        {
            public static Worker GetSingleton()
            {
                if (_singleton == null)
                {
                    GameObject worker = new GameObject();
                    worker.name = "Worker Singleton";
                    _singleton = worker.AddComponent<Worker>();
                }
                return _singleton;
            }
            static Worker _singleton;
            public static Coroutine StaticStartCoroutine(IEnumerator c)
            {
                return Worker.GetSingleton().StartCoroutine(c);
            }

        }




        #region Config

        private void Init()
        {
            LoadConfig();
        }

        private PluginConfig config;

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {

            };
            SaveConfig();
        }

        private class PluginConfig
        {
            [JsonProperty("Check temporary game bans and notify via Discord Webhook")]
            public bool enabled_nexus_gamebancheck = false;

            [JsonProperty("Only report temp bans younger than days")]
            public int nexus_ban_days = 60;

            [JsonProperty("Automatically ban players with a temp ban")]
            public bool autoban_tempbans = false;

            [JsonProperty("Ban appeal message")]
            public string autoban_banmessage = "";

            [JsonProperty("Notify Game Ban + Team")]
            public bool notify_game_ban = true;

            [JsonProperty("Only notify ban if has team")]
            public bool notify_only_if_has_team = false;

            [JsonProperty("Include bm links in ban notification")]
            public bool notify_ban_include_bm = false;

            [JsonProperty("These discord webhooks will get notified")]
            public List<string> webhooks = new List<string>();

            [JsonProperty("notify player being attacked by cheater")]
            public bool notifyCheaterAttacking = true;

            [JsonProperty("report payback command usage to discord")]
            public bool logPaybackCommands = false;

            [JsonProperty("commands persist : save commands and re-apply them players between server restarts and plugin reloads")]
            public bool persistentCommands = false;

            [JsonProperty("Show Payback UI to admins while commands are active")]
            public bool showUI = false;
        }

        #endregion Config

        //| ===================

        //| =======================================
        //| TOMMYGUN'S PROPRIETARY UI CLASSES
        //| =======================================
        //| 
        //| Code contained below this line is not licensed to be used, copied, or modified.
        //| 
        //| 
        //| =======================================

        //| ===================



        public class UI2
        {
            public static Vector4 vectorFullscreen = new Vector4(0, 0, 1, 1);

            public static string ColorText(string input, string color)
            {
                return "<color=" + color + ">" + input + "</color>";
            }

            public static void ClearUI(BasePlayer player)
            {
                foreach (var guid in UI2.guids)
                {
                    CuiHelper.DestroyUi(player, guid);
                }
            }

            //| =============================
            //| DIRT 
            //| =============================
            public static Dictionary<ulong, HashSet<string>> dirtyMap = new Dictionary<ulong, HashSet<string>>();
            public static HashSet<string> GetDirtyBitsForPlayer(BasePlayer player)
            {
                if (player == null) return new HashSet<string>();
                if (!dirtyMap.ContainsKey(player.userID))
                {
                    dirtyMap[player.userID] = new HashSet<string>();
                }
                return dirtyMap[player.userID];
            }

            //| =============================
            //| LAYOUT 
            //| =============================

            public class Layout
            {

                public Vector2 startPosition;

                public Vector4 cellBounds;
                public Vector2 padding;
                public Vector4 cursor;
                public int maxRows;

                public int row = 0;
                public int col = 0;

                public void Init(Vector2 _startPosition, Vector4 _cellBounds, int _maxRows, Vector2 _padding = default(Vector2))
                {
                    startPosition = _startPosition;
                    cellBounds = _cellBounds;
                    maxRows = _maxRows;
                    padding = _padding;
                    row = 0;
                    col = 0;
                }

                public void NextCell(System.Action<Vector4, int, int> populateAction)
                {
                    float cellX = startPosition.x + (col * (cellBounds.z + padding.x)) + padding.x / 2f;
                    float cellY = startPosition.y - (row * (cellBounds.w + padding.y)) - cellBounds.w - padding.y;

                    cursor = new Vector4(cellX, cellY, cellX, cellY);

                    populateAction(cursor, row, col);

                    //move to next element
                    row++;
                    if (row == maxRows)
                    {
                        row = 0;
                        col++;
                    }

                }

                public void Reset()
                {
                    row = 0;
                    col = 0;
                }
            }



            //| =============================
            //| COLOR FUNCTIONS
            //| =============================

            public static string ColorToHex(Color color)
            {
                return ColorUtility.ToHtmlStringRGB(color);
            }
            public static string HexToRGBAString(string hex)
            {
                Color color = Color.white;
                ColorUtility.TryParseHtmlString("#" + hex, out color);
                string c = $"{String.Format("{0:0.000}", color.r)} {String.Format("{0:0.000}", color.g)} {String.Format("{0:0.000}", color.b)} {String.Format("{0:0.000}", color.a)}";
                return c;
            }

            //| =============================
            //| RECT FUNCTIONS
            //| =============================
            public static Vector4 GetOffsetVector4(Vector2 offset)
            {
                return new Vector4(offset.x, offset.y, offset.x, offset.y);
            }
            public static Vector4 GetOffsetVector4(float x, float y)
            {
                return new Vector4(x, y, x, y);
            }

            public static Vector4 SubtractPadding(Vector4 input, float padding)
            {
                float verticalPadding = GetSquareFromWidth(padding);
                return new Vector4(input.x + padding / 2f, verticalPadding / 2f, input.z - padding / 2f, input.w - verticalPadding / 2f);
            }

            public static float GetSquareFromWidth(float width, float aspect = 16f / 9f)
            {
                //return width * 1f / aspect;
                return width * aspect;
            }
            public static float GetSquareFromHeight(float height, float aspect = 16f / 9f)
            {
                //return height * aspect;
                return height * 1f / aspect;
            }

            //specify the screen-space x1, x2, y1 and it will populate y2
            public static Vector4 MakeSquareFromWidth(Vector4 bounds, float aspect = 16f / 9f)
            {
                return new Vector4(bounds.x, bounds.y, bounds.z, bounds.y + GetSquareFromWidth(bounds.z - bounds.x));
            }
            //specify the screen-space x1, y1, and y2 and it will populate the x2
            public static Vector4 MakeSquareFromHeight(Vector4 bounds, float aspect = 16f / 9f)
            {
                return new Vector4(bounds.x, bounds.y, bounds.x + GetSquareFromHeight(bounds.z - bounds.y), bounds.w);
            }
            //make any sized rect from x1, x2, and y1
            public static Vector4 MakeRectFromWidth(Vector4 bounds, float ratio, float aspect = 16f / 9f)
            {
                Vector4 square = MakeSquareFromWidth(bounds, aspect);
                return new Vector4(square.x, square.y, square.z, square.y + (square.w - square.y) * ratio);
            }
            //make any sized rect from y1, y2 and x1
            public static Vector4 MakeRectFromHeight(Vector4 bounds, float ratio, float aspect = 16f / 9f)
            {
                Vector4 square = MakeSquareFromHeight(bounds, aspect);
                return new Vector4(square.x, square.y, square.x + (square.z - square.x) * ratio, square.w);
            }


            //| =============================
            //| UI PANELS
            //| =============================
            public static HashSet<string> guids = new HashSet<string>();

            public static string GetMinUI(Vector4 panelPosition)
            {
                return panelPosition.x.ToString("0.####") + " " + panelPosition.y.ToString("0.####");
            }
            public static string GetMaxUI(Vector4 panelPosition)
            {
                return panelPosition.z.ToString("0.####") + " " + panelPosition.w.ToString("0.####");
            }
            public static string GetColorString(Vector4 color)
            {
                return color.x.ToString("0.####") + " " + color.y.ToString("0.####") + " " + color.z.ToString("0.####") + " " + color.w.ToString("0.####");
            }
            public static CuiElement CreateInputField(CuiElementContainer container, string parent, string panelName, string message, int textSize, string color, Vector4 bounds, string command)
            {

                CuiElement element = new CuiElement
                {
                    Name = panelName,
                    Parent = parent,
                    Components = {
                        new CuiInputFieldComponent {
                            Align = TextAnchor.MiddleLeft,
                            Color = color,
                            Command = command,
							//Text = message,
							FontSize = textSize,
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = GetMinUI(bounds),
                            AnchorMax = GetMaxUI(bounds),
                        }
                    }
                };
                container.Add(element
                );

                return element;
            }

            public static void CreateOutlineLabel(CuiElementContainer container, string parent, string panelName, string message, string color, int size, Vector4 bounds, TextAnchor textAlignment = TextAnchor.MiddleCenter, float fadeOut = 0, float fadeIn = 0, string outlineColor = "0 0 0 0.8", string outlineDistance = "0.7 -0.7")
            {

                container.Add(new CuiElement
                {
                    Name = panelName,
                    Parent = parent,
                    FadeOut = fadeOut,
                    Components = {

                        new CuiTextComponent {
                            Align = textAlignment,
                            Color = color,
                            FadeIn = fadeIn,
                            FontSize = size,
                            Text = message
                        },
                        new CuiOutlineComponent {
                            Color = outlineColor,
                            Distance = outlineDistance,
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = GetMinUI(bounds),
                            AnchorMax = GetMaxUI(bounds),
                        }
                    }
                });
            }

            public static void CreateLabel(CuiElementContainer container, string parent, string panelName, string message, string color, int size, string aMin, string aMax, TextAnchor textAlignment = TextAnchor.MiddleCenter, float fadeIn = 0, float fadeOut = 0)
            {


                CuiLabel label = new CuiLabel();
                label.Text.Text = message;
                label.RectTransform.AnchorMin = aMin;
                label.RectTransform.AnchorMax = aMax;
                label.Text.Align = textAlignment;
                label.Text.Color = color;
                label.Text.FontSize = size;
                label.Text.FadeIn = fadeIn;
                label.FadeOut = fadeOut;

                container.Add(label, parent, panelName);

            }
            public static CuiButton CreateButton(CuiElementContainer container, string parent, string panelName, string color, string text, int size, Vector4 bounds, string command, TextAnchor align = TextAnchor.MiddleCenter, string textColor = "1 1 1 1")
            {

                container.Add(new CuiElement
                {
                    Name = panelName,
                    Parent = parent,
                    Components = {


                            new CuiButtonComponent {
                                Color = color,
                                Command = command,
                            },

                            new CuiRectTransformComponent
                            {
                                AnchorMin = GetMinUI(bounds),
                                AnchorMax = GetMaxUI(bounds),
                            }
                        }
                });

                CreateOutlineLabel(container, panelName, "text", text, textColor, size, new Vector4(0, 0, 1, 1), align);

                return null;

            }


            public static CuiPanel CreatePanel(CuiElementContainer container, string parent, string panelName, string color, Vector4 bounds, string imageUrl = "", bool cursor = false, float fadeOut = 0, float fadeIn = 0, bool png = false, bool blur = false, bool outline = true)
            {

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    //hack to get images working
                    if (png)
                    {
                        if (outline)
                        {
                            container.Add(new CuiElement
                            {
                                Name = panelName,
                                Parent = parent,
                                FadeOut = fadeOut,
                                Components = {
																
								//new CuiRawImageComponent { Color = "0 0 0 0.5", Sprite = "assets/content/materials/highlight.png", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },

								new CuiRawImageComponent
                                {
                                    Color = color,
                                    Png = imageUrl,
                                    FadeIn = fadeIn
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = GetMinUI(bounds),
                                    AnchorMax = GetMaxUI(bounds),
                                },
                                new CuiOutlineComponent {
                                    Color = "0 0 0 0.9",
                                    Distance = "0.7 -0.7",
                                },
                            }
                            });
                        }
                        else
                        {
                            container.Add(new CuiElement
                            {
                                Name = panelName,
                                Parent = parent,
                                FadeOut = fadeOut,
                                Components = {
																
								//new CuiRawImageComponent { Color = "0 0 0 0.5", Sprite = "assets/content/materials/highlight.png", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },

								new CuiRawImageComponent
                                {

                                    Color = color,
                                    Png = imageUrl,
                                    FadeIn = fadeIn
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = GetMinUI(bounds),
                                    AnchorMax = GetMaxUI(bounds),
                                }
                            }
                            });
                        }


                    }
                    else
                    {
                        container.Add(new CuiElement
                        {
                            Name = panelName,
                            Parent = parent,
                            FadeOut = fadeOut,
                            Components = {


                                new CuiRawImageComponent
                                {
                                                                        
                                    Sprite = "assets/content/textures/generic/fulltransparent.tga",

                                    Color = color,
                                    Url = imageUrl,
                                    FadeIn = fadeIn
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = GetMinUI(bounds),
                                    AnchorMax = GetMaxUI(bounds),
                                }
                            }
                        });
                    }


                    return null;

                }
                else
                {

                    if (blur)
                    {

                        //BLURS
                        //assets/content/ui/uibackgroundblur-ingamemenu.mat
                        //assets/content/ui/uibackgroundblur-notice.mat
                        //assets/content/ui/uibackgroundblur.mat
                        // dirty bg blur, can't stretch large
                        string mat = "assets/content/ui/uibackgroundblur-ingamemenu.mat";// MEDIUM BLURRY 
                                                                                         //string mat = "assets/content/ui/uibackgroundblur.mat";//VERY BLURRY

                        //string sprite = "assets/content/ui/ui.white.tga";//kind of boxy outline
                        //string sprite = "assets/content/ui/ui.white.tga";//


                        container.Add(new CuiElement
                        {
                            Name = panelName,
                            Parent = parent,
                            FadeOut = fadeOut,
                            Components = {
                                    new CuiImageComponent {
                                        Color = color,
                                        Material = mat,
                                        FadeIn = fadeIn
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = GetMinUI(bounds),
                                        AnchorMax = GetMaxUI(bounds),
                                    }
                                }
                        });

                    }
                    else
                    {

                        CuiPanel element = new CuiPanel();
                        element.RectTransform.AnchorMin = GetMinUI(bounds);
                        element.RectTransform.AnchorMax = GetMaxUI(bounds);
                        //element.FadeOut = 1f;
                        element.Image.Color = color;
                        element.CursorEnabled = cursor;
                        element.Image.FadeIn = fadeIn;
                        element.FadeOut = fadeOut;

                        container.Add(element, parent, panelName);
                        return element;

                    }

                    return null;

                }

            }

        }


    }
}

/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */