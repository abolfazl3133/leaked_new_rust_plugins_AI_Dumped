using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;


namespace Oxide.Plugins
{
    [Info("VoteMap", "David", "1.1.8")]

    public class VoteMap : RustPlugin
    {   

        #region ui images

        //do not change keys!
        private Dictionary<string, string> uiImgs = new Dictionary<string, string>{
        { "defmap", "https://rustplugins.net/products/defmap.png" },  
        { "bg_main", "https://rustplugins.net/products/votemap/bg_main.png" },                          
        { "title_dark", "https://rustplugins.net/products/votemap/title_dark.png" },  
        { "bg_map", "https://rustplugins.net/products/votemap/bg_map.png" },  
        { "map_wipe", "https://rustplugins.net/products/votemap/map_wipe.png" },  
        { "next_page", "https://rustplugins.net/products/votemap/next_page.png" },  
        { "prev_page", "https://rustplugins.net/products/votemap/prev_page.png" },  
        { "vote_icon", "https://rustplugins.net/products/votemap/vote_icon.png" },  
        { "view_icon", "https://rustplugins.net/products/votemap/view_icon.png" },  
        { "view_btn", "https://rustplugins.net/products/votemap/view_btn.png" },  
        { "vote_count", "https://rustplugins.net/products/votemap/vote_count.png" },  
        { "shadow_box", "https://rustplugins.net/products/votemap/shadow_box.png" },  
        { "green_hl", "https://rustplugins.net/products/votemap/green_hl.png" },  
        { "map_icon", "https://rustlabs.com/img/items180/map.png" },  
        };

        #endregion

        [PluginReference] Plugin ImageLibrary, WipeCountdown, WelcomePanel;
  
        Dictionary<int, int> mapVotes = new Dictionary<int, int>();

        string wp_layer = "wp_content";

        #region [Hooks]

        private void OnServerInitialized()
        {   
            //configurations
            LoadConfig();     
            LoadPlayerVotes();   
            LoadMaps();
            //image library
            DownloadImages();
            ImageQueCheck();
            //votes
            CountAllVotes();

            cmd.AddChatCommand(config.main.chatCmd, this, "OpenVoteUI_Chat");

            foreach (var _player in BasePlayer.activePlayerList)
                WritePlayerEntry(_player);

            BroadcastWinner();

            permission.RegisterPermission($"{Name}.vote", this);
        }

        private void Unload()
        {   
            foreach (var _player in BasePlayer.activePlayerList)
            {
                DestroyBase(_player);
                CuiHelper.DestroyUi(_player, "empty_background");
                CuiHelper.DestroyUi(_player, "btn_prev_1");
                CuiHelper.DestroyUi(_player, "btn_next_1");
                CuiHelper.DestroyUi(_player, "vp_mapwipe_text");
                CuiHelper.DestroyUi(_player, "vp_panel_votesLeft");
                DestroyImage(_player);
                DestroyView(_player);
                CuiHelper.DestroyUi(_player, "vp_emptyRef");
            }   
        }

        void OnNewSave()
        {   
            
            if (config.main.onMapWipe)
            {
                LoadPlayerVotes();
                LoadConfig();
                playerVotes.Clear(); 
                config.dsc.brd = false;
                SaveConfig();
                SavePlayerVotes();
                CountAllVotes();
            }
        }

        void OnPlayerConnected(BasePlayer player) => WritePlayerEntry(player);

        #endregion

        #region [Image Handling]

        //dictionary for load order
        private Dictionary<string, string> imgList = new Dictionary<string, string>();

        private void DownloadImages()
        {   
            if (ImageLibrary == null) 
            { Puts($"(! MISSING) ImageLibrary not found, image load speed will be significantly slower."); return; }
            
            //add to load order
            foreach (string imgName in uiImgs.Keys) 
            {   //saving names as url to compatible with my img function
                if (!imgList.ContainsKey(uiImgs[imgName]))
                    imgList.Add($"{uiImgs[imgName]}", $"{uiImgs[imgName]}");
            }

            foreach (int map in mapList.Keys)
            {
                if (!imgList.ContainsKey($"{mapList[map].mapImage}"))
                    imgList.Add($"{mapList[map].mapImage}", $"{mapList[map].mapImage}");   
            }

            foreach (int map in mapList.Keys)
            {
                foreach (string url in mapList[map].moreImages)
                {   
                    if (!imgList.ContainsKey($"{url}"))
                        imgList.Add($"{url}", $"{url}");     
                }
            }

            //call load order
            ImageLibrary.Call("ImportImageList", "VoteMap", imgList);
        }

        private void ImageQueCheck()
        {   
            int imgCount = imgList.Count();
            int downloaded = 0;
            foreach (string img in imgList.Keys)
            {
                if ((bool) ImageLibrary.Call("HasImage", img))
                    downloaded++; 
            }

            if (imgCount > downloaded)
                Puts($"(!) Stored Images ({downloaded}/{imgCount}). Reload ImageLibrary and then VoteMap plugin to start download order.");
            
            if (imgCount == downloaded)
                Puts($"Stored Images ({downloaded}). All images has been successfully stored in image library.");
        }

        private string Img(string url)
        {   //img url been used as image names
            if (ImageLibrary != null) 
            {   
                if (!(bool) ImageLibrary.Call("HasImage", url))
                    return url;
                else
                    return (string) ImageLibrary?.Call("GetImage", url);
            }
            else return url;
        }

        #endregion

        #region [Functions & Methods & Commands]

        private void WritePlayerEntry(BasePlayer player)
        {   
            if (!playerVotes.ContainsKey(player.userID))
            {
                playerVotes.Add(player.userID, new Votes());
            }
            SavePlayerVotes();
        }

        private int GetVotesLeft(BasePlayer player)
        {
            int maxVotes = config.main.maxVotes;
            int timesVoted = playerVotes[player.userID].votes.Count();
            return maxVotes - timesVoted;
        }

        private int GetMapVotes(int map)
        {   // count votes for each map
            int totalVotes = 0;
            foreach (ulong steamID in playerVotes.Keys)
            {
                foreach (int vote in playerVotes[steamID].votes)
                {
                    if (vote == map)
                    totalVotes ++; 
                }
            }
            return totalVotes; 
        }

        private void CountAllVotes()
        {   //count all and write into temp. dict.
            int countCheck = mapList.Count();
            if (countCheck <= 0) return; 

            foreach (int map in mapList.Keys)
            {   
                mapVotes.Add(map, GetMapVotes(map));
            }  
        }

        private bool IsWinning(int map)
        {   
            int countCheck = mapVotes.Count();
            if (countCheck <= 0) return false; 

            int highestVote = mapVotes.Values.Max();
            if (highestVote == 0) return false;

            if (mapVotes[map] == highestVote) 
                return true;
             else 
                return false;
        }

        private bool AlreadyVoted(BasePlayer player, int map)
        {
            if (playerVotes[player.userID].votes.Contains(map)) return true;

            return false;
        }

        bool IsDigitsOnly(string str)
        {
            foreach (char c in str)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            return true;
        }

        private void OpenVoteUI_Chat(BasePlayer player)
        {   
            if (ImageLibrary == null) 
            { 
                SendReply(player, "ImageLibrary not found, UI will not load."); 
                return; 
            }
            
            BaseCui(player);
            ContentCui(player, 1, 0); 
        }

        void OnWelcomePanelPageOpen(BasePlayer player, int tab, int page, string addon)
        {   
            if(addon == null) return;

            if (addon.ToLower().Contains("votemap"))
            {   
                // 4.3.4 version
                wp_layer = "wp_content";

                // 3.2 version
                if (WelcomePanel.Version.Major == 3)
                    wp_layer = "WelcomePanel_content";

                // 4.0.9 version
                if (WelcomePanel.Version.Major == 4 && WelcomePanel.Version.Minor < 3)
                    wp_layer = "content";

                ContentCui(player, 1, 0, true); 
            }
        }

        

        [ConsoleCommand("votemap_vote")]
        private void votemap_vote(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();

            #region basic checks
            var args = arg.Args;
            if (arg.Player() == null) return;
            if (args == null)   
            { Puts("!error - votemap_vote cmd resulted with null argument, contact developer."); return; }
            if (args.Length > 3) 
            { Puts("!error - votemap_vote cmd can't take more than 3 argument, contact developer."); return; }

            if (!permission.UserHasPermission(player.UserIDString, $"{Name}.vote")) 
            { SendReply(player, "You don't have permission to vote."); return; }
                
            #endregion

            if (GetVotesLeft(player) <= 0) {
                return;
            }
            else { 
                int mapVoted = Convert.ToInt32(args[0]);
                int onPage = Convert.ToInt32(args[1]);
                bool api = false;
                if (Convert.ToInt32(args[2]) == 1) api = true;
                if (playerVotes[player.userID].votes.Contains(mapVoted)) return;
                
                playerVotes[player.userID].votes.Add(mapVoted);
                mapVotes[mapVoted] ++;
                SavePlayerVotes();
                ContentCui(player, 1, onPage, api);
            }
        }

        [ConsoleCommand("votemap_ui")]
        private void votemap_ui(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            var args = arg.Args;
            if (arg.Player() == null) return;
            if (args.Length < 1) return;

            if (args[0] == "openpage")
            {   
                bool api = false;
                if (Convert.ToInt32(args[2]) == 1) api = true;
            
                ContentCui(player, 1, Convert.ToInt32(args[1]), api);
                return;
            }

            if (args[0] == "close")
            {
                DestroyBase(player);
                return;
            }
        }

        [ConsoleCommand("votemap_view")]
        private void votemap_view(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();

            #region basic checks
            var args = arg.Args;
            if (arg.Player() == null) return;
            if (args == null)   
            { Puts("!error - votemap_view cmd resulted with null argument, contact developer."); return; }
            if (args.Length > 3) 
            { Puts("!error - votemap_view cmd can't take more than 2 argument, contact developer."); return; }
            #endregion
          
            if (args[0] == "close")
            {
                DestroyView(player);
                DestroyImage(player);
                return;
            }
            if (args[0] == "viewimg")
            {   
                int mapId2 = Convert.ToInt32(args[1]);
                int img = Convert.ToInt32(args[2]);
                ImageGalCui(player, mapId2, img);
                return;
            }
            int mapId = Convert.ToInt32(args[0]);
            int onPage = Convert.ToInt32(args[1]);
            bool api = false;
            if (Convert.ToInt32(args[2]) == 1) api = true;

            ViewMapCui(player, mapId, onPage, api);
        }

        [ConsoleCommand("votemap_admin")]
        private void votemap_admin(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (arg.Player() != null)
            { if (!player.IsAdmin) return; }


            var args = arg.Args;
            if (args == null)
            {
                if (arg.Player() != null)
                    player.ConsoleMessage("• Admin Console Commands \n    votemap_admin result - Prints winning map into console.\n    votemap_admin reset - Reset all votes.\n    votemap_admin reset <steamID>- Reset votes for certain player.\n    votemap_admin add - Create new map into data/Maps.json. \n    votemap_admin remove - Remove last map in data/Maps.json. ");
                else 
                    Puts("\n \n • Admin Console Commands \n \n    votemap_admin result - Prints winning map into console.\n    votemap_admin reset - Reset all votes.\n    votemap_admin reset <steamID>- Reset votes for certain player.\n    votemap_admin add - Create new map into data/Maps.json. \n    votemap_admin remove - Remove last map in data/Maps.json. \n \n ");
                
                return;
            }

            if (args[0] == "result")
            {   
                int maxValue = mapVotes.Values.Max();
                foreach (int map in mapVotes.Keys)
                {
                    if (mapVotes[map] == maxValue)
                        Puts($"Map #{map} ({mapList[map].mapName}) with {mapVotes[map]} votes.");
                }
                return;
            }

            if (args[0] == "reset")
            {   
                if (args.Length == 2)
                {
                    if (!IsDigitsOnly(args[1])) 
                    {   
                        if (arg.Player() != null)
                            player.ConsoleMessage("Please enter valid steamID.");
                        else
                            Puts($"Please enter valid steamID.");
                        
                        return;
                    }
                    ulong steamID = Convert.ToUInt64(args[1]);

                    if (!playerVotes.ContainsKey(steamID))
                    {
                        if (arg.Player() != null)
                            player.ConsoleMessage($"Player was not found in stored votes.");
                        else
                            Puts($"Player was not found in stored votes.");
                        
                        return;
                    } 
                    else 
                    { 
                        playerVotes.Remove(steamID);
                        SavePlayerVotes();

                        if (arg.Player() != null)
                            player.ConsoleMessage($"Player votes has been removed. ({steamID})");
                        else
                            Puts($"Player votes has been removed. ({steamID})");
                        
                        return;
                    }
                    return;
                }

                mapVotes.Clear();
                playerVotes.Clear();
                SavePlayerVotes();
                if (arg.Player() != null)
                    player.ConsoleMessage("[VoteMap] All votes has been wiped.");
                
                Puts($"All votes has been wiped.");
                CountAllVotes();
                return;
            }

            if (args[0] == "add")
            {
                int index = mapList.Keys.Last() + 1;
                mapList.Add(index, new Maps());
                mapList[index].mapName = "New Map";
                mapList[index].mapDescription = "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s,";
                mapList[index].mapImage = $"https://rustplugins.net/products/votemap/imgs/6_thumbnail.JPG";
                mapList[index].moreImages.Add($"https://rustplugins.net/products/votemap/imgs/6_1.JPG");
                mapList[index].moreImages.Add($"https://rustplugins.net/products/votemap/imgs/6_2.JPG");
                mapList[index].moreImages.Add($"https://rustplugins.net/products/votemap/imgs/6_3.JPG");
                mapList[index].moreImages.Add($"https://rustplugins.net/products/votemap/imgs/6_4.JPG");
                mapList[index].moreImages.Add($"https://rustplugins.net/products/votemap/imgs/6_5.JPG");
                SaveMaps();

                if (arg.Player() != null)
                    player.ConsoleMessage($"[VoteMap] Map #{index} was added.");
                
                Puts($"Map #{index} was added.");

                return;
            }

            if (args[0] == "remove")
            {
                int index = mapList.Keys.Last();
                mapList.Remove(index);
                SaveMaps();

                if (arg.Player() != null)
                    player.ConsoleMessage($"[VoteMap] Map #{index} was removed.");
                
                Puts($"Map #{index} was removed.");

                return;
            }
        }

        #endregion 

        #region CUI

        #region Base

        private bool CS()
        {
            if (config.main.colors == 0)
                return true;
            else
                return false;
        }

        private void BaseCui(BasePlayer player)
        {   
            WritePlayerEntry(player);
            //background
            var _baseCui = CUIClass.CreateOverlay("main_background", config.cs.bg, "0 0", "1 1", true, 0f, "assets/content/ui/uibackgroundblur.mat");
            CUIClass.CreatePanel(ref _baseCui, "vp_offsetContainer", "main_background", "0 0 0 0", "0.5 0.5", "0.5 0.5", false, 0f, "assets/icons/iconmaterial.mat", "-680 -360", "680 360");
            //main
            CUIClass.CreatePanel(ref _baseCui, "vp_panel_main", "vp_offsetContainer", config.cs.main, "0.3 0.23", "0.70 0.75", false, 0f, "assets/icons/iconmaterial.mat");
            if (CS()) CUIClass.CreateImage(ref _baseCui, "vp_panel_main", Img(uiImgs["bg_main"]), "0.0 0.0", "1 1", 0f);
            //title
            CUIClass.CreatePanel(ref _baseCui, "vp_title_main", "vp_panel_main", config.cs.title, "0.0 0.93", "1 1", false, 0f, "assets/icons/iconmaterial.mat");
            if (CS()) CUIClass.CreateImage(ref _baseCui, "vp_title_main", Img(uiImgs["title_dark"]), "0.0 0.0", "1 1", 0f);
                CUIClass.CreateText(ref _baseCui, "vp_title_main_text", "vp_title_main", "1 1 1 0.7", gl("title"), 12, "0.05 0", "1 1", TextAnchor.MiddleLeft, $"robotocondensed-regular.ttf", "0 0 0 0", $"0 0"); 
                CUIClass.CreateImage(ref _baseCui, "vp_title_main", Img(uiImgs["map_icon"]), "0.01 0.06", "0.045 0.91", 0f);    
                CUIClass.CreateButton(ref _baseCui, $"vp_btn_close", "vp_title_main", config.cs.close, gl("closeBtn"), 9, "0.91 0.17", $"0.993 0.83", $"votemap_ui close", "", "1 1 1 0.9", 0.3f, TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf");
                
            DestroyBase(player);
            CuiHelper.AddUi(player, _baseCui);     
        }

        private void DestroyBase(BasePlayer player) 
        {   
            CuiHelper.DestroyUi(player, "main_background");
            CuiHelper.DestroyUi(player, "vp_offsetContainer"); 
            CuiHelper.DestroyUi(player, "vp_panel_main"); 
        }

        #endregion

        #region Maps

        private void ContentCui(BasePlayer player, int layout, int page, bool api = false)
        {   
            int countCheck = mapList.Count();
            if (countCheck <= 0) { Puts("Map list is empty! (data/VoteMap/Maps.json)"); return; }
            
            var mapNames = new List<string>();
            var mapDesc = new List<string>();
            var mapImages = new List<string>();
            var mapIDs = new List<int>();
            mapNames.Add("indexing"); mapDesc.Add("indexing"); mapImages.Add("indexing"); mapIDs.Add(0);

            foreach (int map in mapList.Keys)
            {
                mapNames.Add(mapList[map].mapName);
                mapDesc.Add(mapList[map].mapDescription);
                mapImages.Add(mapList[map].mapImage);
                mapIDs.Add(map);
            }
            
            string mainPanel = "vp_panel_main";
            int apiCmd = 0;
            if (api) { 
                mainPanel = wp_layer;  

                apiCmd = 1;
            }
            
            var _contentCui = CUIClass.CreateOverlay("empty_background", "0 0 0 0.0", "0 0", "0 0", false, 0f, "assets/icons/iconmaterial.mat");
            if (layout == 1)
            {   
                for (int i = 1; i < 4; i++)
                { CuiHelper.DestroyUi(player, $"vp_panel_map{i}");  CuiHelper.DestroyUi(player, $"vp_panel_bg{i}");}
                   
                int pageIndex = 3 * page;
                int mapsTotal = mapNames.Count() - 1;

                if (1 + pageIndex <= mapsTotal) CreateMapPanel(player, 1, page, mapImages[1 + pageIndex], mapNames[1 + pageIndex], mapDesc[1 + pageIndex], mapIDs[1 + pageIndex], api);
                if (2 + pageIndex <= mapsTotal) CreateMapPanel(player, 2, page, mapImages[2 + pageIndex], mapNames[2 + pageIndex], mapDesc[2 + pageIndex], mapIDs[2 + pageIndex], api);
                if (3 + pageIndex <= mapsTotal) CreateMapPanel(player, 3, page, mapImages[3 + pageIndex], mapNames[3 + pageIndex], mapDesc[3 + pageIndex], mapIDs[3 + pageIndex], api);
                //votes left                                                                                  
                CUIClass.CreateText(ref _contentCui, "vp_panel_votesLeft", mainPanel, "1 1 1 0.4", gl("voteLeft").Replace("{votesLeft}", $"{GetVotesLeft(player)}"), 9, "0.0 0.86", "1 0.9", TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "0 0 0 0", $"0 0");  
                //footer wipe text
                string wipeCountdown = "PLEASE INSTALL WIPECOUNTDOWN PLUGIN.";
                if (WipeCountdown != null) {    
                    if (CS()) wipeCountdown = $"     {gl("mapWipe")}" + "{countdown}";
                    else wipeCountdown = $"{gl("mapWipe")}" + "{countdown}";
                    string _getcdAPI = Convert.ToString(WipeCountdown.CallHook("GetCountdownFormated_API"));
                    string _replacedText = wipeCountdown.Replace("{countdown}", _getcdAPI);
                    wipeCountdown = _replacedText; } 
                if (CS()) CUIClass.CreateImage(ref _contentCui, mainPanel, Img(uiImgs["map_wipe"]), "0.3 0.04", "0.7 0.11", 0f);
                CUIClass.CreateText(ref _contentCui, "vp_mapwipe_text", mainPanel, "1 1 1 0.4", wipeCountdown, 10, "0.3 0.04", "0.7 0.11", TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "0 0 0 0", $"0 0");  
                //page buttons
                if (3 + pageIndex < mapsTotal) {
                    CUIClass.CreateButton(ref _contentCui, "btn_next_1", mainPanel, "0 0 0 0.0", $"{gl("next")}    <size=13>›</size>", 10, "0.75 0.04", "0.9 0.105", $"votemap_ui openpage {page + 1} {apiCmd}", "", "1 1 1 0.6", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                    if (CS())    CUIClass.CreateImage(ref _contentCui, "btn_next_1", Img(uiImgs["next_page"]), "0 0", "1 1", 0f);
                    if (CS())    CUIClass.CreateText(ref _contentCui, "btn_next_1_text", "btn_next_1", "1 1 1 0.4", $"{gl("next")}      ", 10, "0 0", "1 1", TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "0 0 0 0", $"0 0");  
                }
                if (page != 0) {
                    CUIClass.CreateButton(ref _contentCui, "btn_prev_1", mainPanel, "0 0 0 0.0", $"<size=13>‹</size>    {gl("back")}", 10, "0.1 0.04", "0.25 0.105", $"votemap_ui openpage {page - 1} {apiCmd}", "", "1 1 1 0.6", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                    if (CS())    CUIClass.CreateImage(ref _contentCui, "btn_prev_1", Img(uiImgs["prev_page"]), "0 0", "1 1", 0f);
                    if (CS())    CUIClass.CreateText(ref _contentCui, "btn_prev_1_text", "btn_prev_1", "1 1 1 0.4", $"    {gl("back")}", 10, "0 0", "1 1", TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "0 0 0 0", $"0 0");  
                }
            }
         
            CuiHelper.DestroyUi(player, "empty_background");
            CuiHelper.DestroyUi(player, "btn_prev_1");
            CuiHelper.DestroyUi(player, "btn_next_1");
            CuiHelper.DestroyUi(player, "vp_mapwipe_text");
            CuiHelper.DestroyUi(player, "vp_panel_votesLeft");

            CuiHelper.AddUi(player, _contentCui);     
        }

        
        

        private void CreateMapPanel(BasePlayer player, int panelNumber, int onPage, string mapImg, string mapTitle, string mapDesc, int mapID, bool api = false)
        {   
            string mainPanel = "vp_panel_main";
            int apiCmd = 0;
            string[] height = {"0.185", "0.83"};
            if (api) {  
                mainPanel =  wp_layer;

                apiCmd = 1; 
                height[0] = "0.2"; 
                height[1] = "0.85";
            }

            string anchorMin = $"0.06 {height[0]}";
            string anchorMax = $"0.31 {height[1]}";
            if (panelNumber == 2) {anchorMin = $"0.375 {height[0]}";  anchorMax = $"0.625 {height[1]}";} 
            if (panelNumber == 3) {anchorMin = $"0.69 {height[0]}";  anchorMax = $"0.94 {height[1]}";} 

            var _mapPanelCui = CUIClass.CreateOverlay("vp_emptyRef", "0 0 0 0.0", "0 0", "0 0", false, 0f, "assets/icons/iconmaterial.mat");
            //Main
          
            CUIClass.CreatePanel(ref _mapPanelCui, $"vp_panel_bg{panelNumber}", mainPanel, "1 1 1 0.0", anchorMin, anchorMax, false, 0.3f, "assets/icons/iconmaterial.mat");
                if (config.cs.shadow) CUIClass.CreateImage(ref _mapPanelCui, $"vp_panel_bg{panelNumber}", Img(uiImgs["shadow_box"]), "0 -0.1", "1.2 1", 0.5f);
                if (IsWinning(mapID))  CUIClass.CreateImage(ref _mapPanelCui, $"vp_panel_bg{panelNumber}", Img(uiImgs["green_hl"]), "-0.007 -0.005", "1.007 1.005", 0.5f);
             
            CUIClass.CreatePanel(ref _mapPanelCui, $"vp_panel_map{panelNumber}", mainPanel, config.cs.map, anchorMin, anchorMax, false, 0.3f, "assets/content/ui/uibackgroundblur.mat");
            
            if (CS()) CUIClass.CreateImage(ref _mapPanelCui, $"vp_panel_map{panelNumber}", Img(uiImgs["bg_map"]), "0.0 0.0", "1 1", 0.3f);
                //Map Img
                CUIClass.CreateImage(ref _mapPanelCui, $"vp_panel_map{panelNumber}", Img(mapImg), "0.06 0.45", "0.94 0.96", 0.3f);
                //Map Votes
            
                CUIClass.CreatePanel(ref _mapPanelCui, $"vp_panel_votes{panelNumber}", $"vp_panel_map{panelNumber}", "1 1 1 0.5", "0.33 0.435", "0.67 0.5", false, 0.3f, "assets/icons/iconmaterial.mat");
                CUIClass.CreateImage(ref _mapPanelCui, $"vp_panel_votes{panelNumber}", Img(uiImgs["vote_count"]), "0.0 0.0", "1 1", 0.3f);
                    CUIClass.CreateText(ref _mapPanelCui, $"vp_panel_map{panelNumber}_count", $"vp_panel_votes{panelNumber}", "1 1 1 0.4", $"{gl("voteCount")}{mapVotes[mapID]}", 8, "0 0", "1 1", TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "0 0 0 0", $"0 0");  
                //Title + Description
                CUIClass.CreateText(ref _mapPanelCui, $"vp_panel_map{panelNumber}_name", $"vp_panel_map{panelNumber}", "1 1 1 1", $"{mapTitle}", 15, "0.06 0.3", "0.94 0.42", TextAnchor.UpperCenter, $"robotocondensed-bold.ttf", "0 0 0 0", $"0 0");  
                CUIClass.CreateText(ref _mapPanelCui, $"vp_panel_map{panelNumber}_desc", $"vp_panel_map{panelNumber}", "1 1 1 1", $"{mapDesc}", 8, "0.07 0.16", "0.93 0.34", TextAnchor.UpperCenter, $"robotocondensed-regular.ttf", "0 0 0 0", $"0 0");  
                //Buttons
            
                if (GetVotesLeft(player) <= 0 || AlreadyVoted(player, mapID)) {
                    CUIClass.CreateButton(ref _mapPanelCui, $"btn_vote_{panelNumber}", $"vp_panel_map{panelNumber}", "0.18 0.18 0.18 1.0", "ALREADY\nVOTED", 8, "0.06 0.03", $"0.48 0.14", $"", "", "1 1 1 0.9", 0.3f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                } else {
                    CUIClass.CreateButton(ref _mapPanelCui, $"btn_vote_{panelNumber}", $"vp_panel_map{panelNumber}", config.cs.vote, gl("voteBtn"), 10, "0.06 0.03", $"0.48 0.14", $"votemap_vote {mapID} {onPage} {apiCmd}", "", "1 1 1 0.9", 0.3f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        CUIClass.CreateImage(ref _mapPanelCui, $"btn_vote_{panelNumber}", Img(uiImgs["vote_icon"]), "0.05 0.07", "0.85 0.93", 0.3f);
                }
                CUIClass.CreateButton(ref _mapPanelCui, $"btn_view_{panelNumber}", $"vp_panel_map{panelNumber}",config.cs.view, gl("viewBtn"), 10, "0.52 0.03", $"0.94 0.14", $"votemap_view {mapID} {onPage} {apiCmd}", "", "1 1 1 0.6", 0.3f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                    CUIClass.CreateImage(ref _mapPanelCui, $"btn_view_{panelNumber}", Img(uiImgs["view_btn"]), "0.0 0.0", "1 1", 0.3f);
            
            CuiHelper.DestroyUi(player, "vp_emptyRef");
            CuiHelper.AddUi(player, _mapPanelCui);  
        }

        #endregion

        #region ViewMap

        private void ViewMapCui(BasePlayer player, int mapID, int onPage, bool api = false)
        {   
            string mainPanel = "vp_panel_main";
            if (api) {
                mainPanel =  wp_layer;
            }

            var _viewMap = CUIClass.CreateOverlay("vp_view_empty", "0 0 0 0.0", "0 0", "0 0", false, 0f, "assets/content/ui/uibackgroundblur.mat");
            //main
            CUIClass.CreatePanel(ref _viewMap, "vp_panel_mainView", mainPanel, "0 0 0 0.9", "0.0 0.0", "1 0.93", false, 0.3f, "assets/content/ui/uibackgroundblur.mat");
            if (CS()) CUIClass.CreateImage(ref _viewMap, "vp_panel_mainView", Img(uiImgs["bg_main"]), "0.0 0.0", "1 1", 0.3f);
            //Text
            CUIClass.CreateText(ref _viewMap, "vp_view_map_title", "vp_panel_mainView", "1 1 1 0.7", $"{mapList[mapID].mapName}", 22, "0.67 0.80", "1 0.89", TextAnchor.UpperLeft, $"robotocondensed-bold.ttf", "0 0 0 0", $"0 0"); 
            CUIClass.CreateText(ref _viewMap, "vp_view_map_text", "vp_panel_mainView", "1 1 1 0.7", $"{mapList[mapID].mapDescription}", 12, "0.67 0", "0.98 0.81", TextAnchor.UpperLeft, $"robotocondensed-regular.ttf", "0 0 0 0", $"0 0"); 
                //CUIClass.CreateImage(ref _viewMap, "vp_title_main", (string) ImageLibrary?.Call("GetImage", "map_icon"), "0.01 0.06", "0.045 0.91", 0f);    
            CUIClass.CreateButton(ref _viewMap, $"vp_view_close", "vp_panel_mainView", "0.80 0.25 0.16 0.5", "BACK", 9, "0.45 0.12", $"0.55 0.2", "votemap_view close", "", "1 1 1 0.9", 0.3f, TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf");
                
            DestroyView(player);
            CuiHelper.AddUi(player, _viewMap);     
            ImageGalCui(player, mapID, 0);
        }

        
        private void ImageGalCui(BasePlayer player, int mapID, int imgNumber)
        {   
            string img = mapList[mapID].moreImages[imgNumber];
            int count = mapList[mapID].moreImages.Count() - 1;
            var _igCui = CUIClass.CreateOverlay("vp_ig_empty", "0 0 0 0.0", "0 0", "0 0", false, 0f, "assets/content/ui/uibackgroundblur.mat");
            //main
            CUIClass.CreatePanel(ref _igCui, "vp_ig_main", "vp_panel_mainView", "0 0 0 0.9", config.cs.aMin, config.cs.aMax, false, 0.3f, "assets/icons/iconmaterial.mat");
            if (img.StartsWith("http") || img.StartsWith("www"))
                CUIClass.CreateImage(ref _igCui, "vp_ig_main", Img(img), "0.0 0.0", "1 1", 0.3f);
            
            if (imgNumber < count) {
                CUIClass.CreateButton(ref _igCui, $"vp_ig_next", "vp_ig_main", "0 0 0 0.0", "›", 43, "0.90 0.17", $"0.99 0.83", $"votemap_view viewimg {mapID} {imgNumber + 1}", "", "0 0 0 1", 0.3f, TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "assets/icons/iconmaterial.mat");
                CUIClass.CreateButton(ref _igCui, $"vp_ig_next", "vp_ig_main", "0 0 0 0.0", "›", 40, "0.91 0.17", $"0.99 0.83", $"votemap_view viewimg {mapID} {imgNumber + 1}", "", "1 1 1 1", 0.3f, TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "assets/icons/iconmaterial.mat");
            }
            if (imgNumber != 0) {
                CUIClass.CreateButton(ref _igCui, $"vp_ig_back", "vp_ig_main", "0 0 0 0.0", "‹", 43, "0.01 0.17", $"0.1 0.83", $"votemap_view viewimg {mapID} {imgNumber - 1}", "", "0 0 0 1", 0.3f, TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "assets/icons/iconmaterial.mat");
                CUIClass.CreateButton(ref _igCui, $"vp_ig_back", "vp_ig_main", "0 0 0 0.0", "‹", 40, "0.01 0.17", $"0.09 0.83", $"votemap_view viewimg {mapID} {imgNumber - 1}", "", "1 1 1 1", 0.3f, TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "assets/icons/iconmaterial.mat");
            }
            DestroyImage(player);
            CuiHelper.AddUi(player, _igCui);     
        }
        
        private void DestroyImage(BasePlayer player) 
        {   
            CuiHelper.DestroyUi(player, "vp_ig_empty");
            CuiHelper.DestroyUi(player, "vp_ig_main");
        }

        private void DestroyView(BasePlayer player) 
        {   
            CuiHelper.DestroyUi(player, "vp_view_empty");
            CuiHelper.DestroyUi(player, "vp_panel_mainView"); 
        }


        #endregion

        #endregion

        #region [CUI Classes]

        public class CUIClass
        {
            public static CuiElementContainer CreateOverlay(string _name, string _color, string _anchorMin, string _anchorMax, bool _cursorOn = false, float _fade = 0f, string _mat ="assets/content/ui/uibackgroundblur.mat")
            {   

            
                var _element = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = _color, Material = _mat, FadeIn = _fade},
                            RectTransform = { AnchorMin = _anchorMin, AnchorMax = _anchorMax },
                            CursorEnabled = _cursorOn
                        },
                        new CuiElement().Parent = "Overlay",
                        _name
                    }
                };
                return _element;
            }

            public static void CreatePanel(ref CuiElementContainer _container, string _name, string _parent, string _color, string _anchorMin, string _anchorMax, bool _cursorOn = false, float _fade = 0f, string _mat2 ="assets/content/ui/uibackgroundblur.mat", string _OffsetMin = "", string _OffsetMax = "" )
            {
                _container.Add(new CuiPanel
                {
                    Image = { Color = _color, Material = _mat2, FadeIn = _fade },
                    RectTransform = { AnchorMin = _anchorMin, AnchorMax = _anchorMax, OffsetMin = _OffsetMin, OffsetMax = _OffsetMax },
                    CursorEnabled = _cursorOn
                },
                _parent,
                _name);
            }

            public static void CreateImage(ref CuiElementContainer _container, string _parent, string _image, string _anchorMin, string _anchorMax, float _fade = 0f)
            {
                if (_image.StartsWith("http") || _image.StartsWith("www"))
                {
                    _container.Add(new CuiElement
                    {
                        Parent = _parent,
                        Components =
                        {
                            new CuiRawImageComponent { Url = _image, Sprite = "assets/content/textures/generic/fulltransparent.tga", FadeIn = _fade},
                            new CuiRectTransformComponent { AnchorMin = _anchorMin, AnchorMax = _anchorMax }
                        }
                    });
                }
                else
                {
                    _container.Add(new CuiElement
                    {
                        Parent = _parent,
                        Components =
                        {
                            new CuiRawImageComponent { Png = _image, Sprite = "assets/content/textures/generic/fulltransparent.tga", FadeIn = _fade},
                            new CuiRectTransformComponent { AnchorMin = _anchorMin, AnchorMax = _anchorMax }
                        }
                    });
                }
            }

            public static void CreateInput(ref CuiElementContainer _container, string _name, string _parent, string _color, int _size, string _anchorMin, string _anchorMax, string _font = "permanentmarker.ttf", string _command = "command.processinput", TextAnchor _align = TextAnchor.MiddleCenter)
            {
                _container.Add(new CuiElement
                {
                    Parent = _parent,
                    Name = _name,

                    Components =
                    {
                        new CuiInputFieldComponent
                        {

                            Text = "0",
                            CharsLimit = 250,
                            Color = _color,
                            IsPassword = false,
                            Command = _command,
                            Font = _font,
                            FontSize = _size,
                            Align = _align
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = _anchorMin,
                            AnchorMax = _anchorMax

                        }

                    },
                });
            }

            public static void CreateText(ref CuiElementContainer _container, string _name, string _parent, string _color, string _text, int _size, string _anchorMin, string _anchorMax, TextAnchor _align = TextAnchor.MiddleCenter, string _font = "robotocondensed-bold.ttf", string _outlineColor = "", string _outlineScale ="")
            {   
               

                _container.Add(new CuiElement
                {
                    Parent = _parent,
                    Name = _name,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = _text,
                            FontSize = _size,
                            Font = _font,
                            Align = _align,
                            Color = _color,
                            FadeIn = 0f,
                        },

                        new CuiOutlineComponent
                        {
                            
                            Color = _outlineColor,
                            Distance = _outlineScale
                            
                        },

                        new CuiRectTransformComponent
                        {
                             AnchorMin = _anchorMin,
                             AnchorMax = _anchorMax
                        }
                    },
                });
            }

            public static void CreateButton(ref CuiElementContainer _container, string _name, string _parent, string _color, string _text, int _size, string _anchorMin, string _anchorMax, string _command = "", string _close = "", string _textColor = "", float _fade = 1f, TextAnchor _align = TextAnchor.MiddleCenter, string _font = "", string _mat = "assets/content/ui/uibackgroundblur.mat")
            {       
               
                _container.Add(new CuiButton
                {
                    Button = { Close = _close, Command = _command, Color = _color, Material = _mat, FadeIn = _fade},
                    RectTransform = { AnchorMin = _anchorMin, AnchorMax = _anchorMax },
                    Text = { Text = _text, FontSize = _size, Align = _align, Color = _textColor, Font = _font, FadeIn = _fade}
                },
                _parent,
                _name);
            }

        }
        #endregion

        #region [Vote Data]

        private void SavePlayerVotes()
        {
            if (playerVotes != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/PlayerVotes", playerVotes);
        }

        private Dictionary<ulong, Votes> playerVotes;

        private class Votes
        {
            public List<int> votes = new List<int>{};  
        }

        private void LoadPlayerVotes()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/PlayerVotes"))
            {
                playerVotes = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Votes>>($"{Name}/PlayerVotes");
            }
            else
            {
                playerVotes = new Dictionary<ulong, Votes>();
                SavePlayerVotes();
            }
        }

        #endregion

        #region [Map Data]

        private void SaveMaps()
        {
            if (mapList != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Maps", mapList);
        }

        private Dictionary<int, Maps> mapList;

        private class Maps
        {   
            public string mapName;
            public string mapDescription;
            public string mapImage;
            public List<string> moreImages = new List<string>{};  
        }

        private void LoadMaps()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/Maps"))
            {
                mapList = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<int, Maps>>($"{Name}/Maps");
            }
            else
            {
                mapList = new Dictionary<int, Maps>();

                CreateExamples();
                SaveMaps();
            }
        }

        private void CreateExamples()
        {   
            string[] mapNames = {"empty", "KeanLand", "Tahuata", "World War II", "Custom Map #4", "Custom Map #5", "Custom Map #6"};
            string[] mapDescs = {"empty", 
                "Discover an unique custom map! Tons of details, beautiful scenery, miles of endless roads and deep forests. This map has everything that Rust offers and more in terms of transportation \n\nPurchase at\n<b>codefling.com/skirow</b> \n\nCredits\n<b>SKIROW</b>", 
                "Tahuata is a tropical map based on Hawaii islands. The map is composed of one main island and 8 smaller islands. This map has an huge train system above ground with 751 track segments. \n\nPurchase at\n<b>codefling.com/skirow</b> \n\nCredits\n<b>SKIROW</b>", 
                "KBEdits WWII themed custom map. All the monuments have been built into a custom crafted 2k map that is suitable for both small and medium pop servers. The loot has been taken from the official Facepunch monuments. \n\nPurchase at\n<b>codefling.com/knockcree</b> \n\nCredits\n<b>Bran</b>, <b>Cobalt</b> and <b>Knockcree</b>.", 
                "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s,", 
                "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s,", 
                "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s,"
            };

            for (int i = 1; i < 7; i++)
            {
                mapList.Add(i, new Maps());
                mapList[i].mapName = mapNames[i];
                mapList[i].mapDescription = mapDescs[i];
                mapList[i].mapImage = $"https://rustplugins.net/products/votemap/imgs/{i}_thumbnail.JPG";
                mapList[i].moreImages.Add($"https://rustplugins.net/products/votemap/imgs/{i}_1.JPG");
                mapList[i].moreImages.Add($"https://rustplugins.net/products/votemap/imgs/{i}_2.JPG");
                mapList[i].moreImages.Add($"https://rustplugins.net/products/votemap/imgs/{i}_3.JPG");
                mapList[i].moreImages.Add($"https://rustplugins.net/products/votemap/imgs/{i}_4.JPG");
                mapList[i].moreImages.Add($"https://rustplugins.net/products/votemap/imgs/{i}_5.JPG");
            }
        }
        #endregion

        #region [Config] 

        private Configuration config;
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.CreateConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        class Configuration
        {   
            [JsonProperty(PropertyName = "Vote Settings")]
            public  MainSet main { get; set; }

            public class MainSet
            {
                [JsonProperty("Reset votes on NewSave (when map is changed).")]
                public bool onMapWipe { get; set; }

                [JsonProperty("Number of votes allowed per one player.")]
                public int maxVotes { get; set; }

                [JsonProperty("Chat command to open VoteMap ui.")]
                public string chatCmd { get; set; }

                [JsonProperty("Color Scheme (0 = default | 1 = customizable).")]
                public int colors { get; set; }
            } 

            [JsonProperty(PropertyName = "Discord Message")]
            public Dsc dsc { get; set; }

            public class Dsc
            {
                [JsonProperty("Enable discord message.")]
                public bool enabled { get; set; }

                [JsonProperty("Discord WebHook URL")]
                public string webhook { get; set; }

                [JsonProperty("Send message X seconds before wipe.")]
                public long seconds { get; set; }

                [JsonProperty("Embed Message Color code.")]
                public long embColor { get; set; }

                [JsonProperty("Message (Do not delete from list, just change left side values!).")]
                public Dictionary<string, string> msg { get; set; }

                [JsonProperty("Already broadcasted?")]
                public bool brd { get; set; }
            } 

            [JsonProperty(PropertyName = "Color Scheme Customization")]
            public Cs cs { get; set; }

            public class Cs
            {
                [JsonProperty("background")]
                public string bg { get; set; }

                [JsonProperty("main")]
                public string main { get; set; }

                [JsonProperty("title")]
                public string title { get; set; }

                [JsonProperty("map")]
                public string map { get; set; }

                [JsonProperty("vote")]
                public string vote { get; set; }

                [JsonProperty("view")]
                public string view { get; set; }

                [JsonProperty("close")]
                public string close { get; set; }

                [JsonProperty("Img gallery AnchorMin")]
                public string aMin { get; set; }

                [JsonProperty("Img gallery AnchorMax")]
                public string aMax { get; set; }

                [JsonProperty("Shadow")]
                public bool shadow { get; set; }
            }

            public static Configuration CreateConfig()
            {
                return new Configuration
                {   
                    main = new VoteMap.Configuration.MainSet
                    {   
                        onMapWipe = false,
                        maxVotes = 3,
                        chatCmd = "votemap",
                        colors = 0,
                    },
                    dsc = new VoteMap.Configuration.Dsc
                    {   
                        enabled = false,
                        webhook = "Your discord webhook url",
                        seconds = 86400,
                        embColor = 15105570l,
                        msg = new Dictionary<string, string>
                        {
                            { "ServerTitle", "Use |{hostname}| or type your name" },
                            { "UnderServerTitle", "Vote Map results for tomorrow wipe." },
                            { "MapTitle", "**{mapname}  #{mapID} 🏆**" },
                            { "UnderMapTitle", "Voted by {votes} players!" },
                            { "mention", "@everyone" },
                        },
                        brd = false,
                    },
                    cs = new VoteMap.Configuration.Cs
                    {   
                        bg = "0 0 0 0.81",
                        main = "0 0 0 0.7",
                        title = "0 0 0 0.5",
                        map = "0.11 0.11 0.11 0.7",
                        vote = "0.80 0.25 0.16 1.0",
                        view = "0.18 0.18 0.18 1.0",
                        close = "0.80 0.25 0.16 0.5",
                        aMin = "0.05 0.30",
                        aMax = "0.65 0.89",
                        shadow = true,
                        
                    },
                };
            }

        }
        #endregion

        #region Discord Webhook

        private void BroadcastWinner()
        {   
            if (!config.dsc.webhook.StartsWith("http"))
                return;
            
            if (!config.dsc.enabled)
                return;

            if (config.dsc.brd)
                return;
            
            timer.Every(60f, () =>
            {   
                if (config.dsc.brd)
                    return;

                long getSecLeft = (long) WipeCountdown.CallHook("GetCountdownSeconds_API");
                if (getSecLeft < config.dsc.seconds)
                {
                    int maxValue = mapVotes.Values.Max();
                    foreach (int map in mapVotes.Keys)
                    {
                        if (mapVotes[map] == maxValue)
                        {   
                            if (mapList[map].mapImage.Contains("rustmaps.com") || mapList[map].mapImage.Contains("http://playrust.io/"))
                                SendDiscordMessage(map, maxValue, true);
                            else
                                SendDiscordMessage(map, maxValue, false);

                            break;    
                        }
                    }
                    config.dsc.brd = true;
                    SaveConfig();
                    return;
                }
            });
        }

        [ConsoleCommand("votemap_msg")]
        private void storemenuvalue(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (arg.Player() != null)
            { 
                if (!player.IsAdmin)
                    return;
            }

            int maxValue = mapVotes.Values.Max();
                    foreach (int map in mapVotes.Keys)
                    {
                        if (mapVotes[map] == maxValue)
                        {   
                            if (mapList[map].mapImage.Contains("rustmaps.com") || mapList[map].mapImage.Contains("http://playrust.io/"))
                                SendDiscordMessage(map, maxValue, true);
                            else
                                SendDiscordMessage(map, maxValue, false);

                            break;    
                        }
                    }
        }
        
        private readonly Dictionary<string, string> headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json"
        };

        private class TextField
        {
            public string name;
            public string value;
            public bool inline;
        };

        private void SendDiscordMessage(int mapId, int mapVotes, bool procedural = false)
        {   
            string webhook = config.dsc.webhook;
            string hostname = ConVar.Server.hostname;
            string serverName = config.dsc.msg["ServerTitle"].Replace("{hostname}", hostname).Replace("{mapname}", mapList[mapId].mapName).Replace("{mapID}", $"{mapId}").Replace("{votes}", $"{mapVotes}");
            string underSrvName = config.dsc.msg["UnderServerTitle"].Replace("{hostname}", hostname).Replace("{mapname}", mapList[mapId].mapName).Replace("{mapID}", $"{mapId}").Replace("{votes}", $"{mapVotes}");
            string mapName = config.dsc.msg["MapTitle"].Replace("{hostname}", hostname).Replace("{mapname}", mapList[mapId].mapName).Replace("{mapID}", $"{mapId}").Replace("{votes}", $"{mapVotes}");
            string underMapName = config.dsc.msg["UnderMapTitle"].Replace("{hostname}", hostname).Replace("{mapname}", mapList[mapId].mapName).Replace("{mapID}", $"{mapId}").Replace("{votes}", $"{mapVotes}");
            string mapImage = mapList[mapId].mapImage;
            string mention = config.dsc.msg["mention"];
            string footerText = "Default";

            if (procedural)
                footerText = "Procedural Map";
            else
                footerText =  "Custom Map";

        #region JsonObject

            TextField[] fields = new[]  { new TextField { name="▪️", value="▪️", inline=false }, new TextField { name = mapName, value = underMapName, inline=false } };

            Dictionary<string, string> footer = new Dictionary<string, string>()
            {
                {"text", footerText},
                {"icon_url", "https://i.ibb.co/939hNM0/rustlogo.jpg"},
            };  

            Dictionary<string, string> image = new Dictionary<string, string>()
            {
                {"url", mapImage},
            }; 
           
            string json = JsonConvert.SerializeObject(new
            {
                content = mention,
                embeds = new[]
                {
                    new
                    {
                        image,
                        title = $"**{serverName}**",
                        description = underSrvName,
                        color = config.dsc.embColor,
                        footer,
                        fields
                    }
                }

            });
        #endregion
        
            webrequest.Enqueue(webhook, json, (code, response) =>          
            {
                if (code == 204 || response == null)
                    Puts($"Discord Message Sent.");
                
            }, this, RequestMethod.POST, headers );
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["title"] = "<b>VOTE</b><color=#C2C2C2>MAP</color>",
                ["closeBtn"] = "✘ CLOSE",
                ["voteBtn"] = "    VOTE",
                ["viewBtn"] = "    VIEW",
                ["mapWipe"] = "MAP WIPE IN ",
                ["voteLeft"] = "You have {votesLeft} votes left.",
                ["voteCount"] = "VOTES: ",
                ["back"] = "BACK",
                ["next"] = "NEXT",

            }, this);
        }

        private string gl(string _message) => lang.GetMessage(_message, this);

        #endregion
    }
}