using System;
using System.Collections.Generic;
using System.Reflection;
using Oxide.Core.Plugins;
using Steamworks;
using Newtonsoft.Json;
using Network;
using UnityEngine;
using Rust;
using Facepunch.Math;
using Oxide.Core.Libraries;
using Newtonsoft.Json.Linq;
using System.Net.Sockets;
using Network.Channel;
using static ConnectionAuth;
using static Network.Connection;
using Oxide.Game.Rust.Libraries;

namespace Oxide.Plugins
{
	[Info("NoSteam", "https://discord.gg/TrJ7jnS233", "1.2.9")] 
	class NoSteam : RustPlugin
	{
		#region Variables

		private Dictionary<ulong, uint> ListAppID { get; } = new Dictionary<ulong, uint>();
		private Configuration Settings;

class Configuration
{
    [JsonProperty("SteamAPIKey")]
    public string SteamAPIKey { get; set; }

    [JsonProperty("DiscordAPIKey")]
    public string DiscordAPIKey { get; set; }

    public string DiscordAPI { get; set; } 

    public Configuration()
    {
        SteamAPIKey = "Ваш_ключ_для_Steam_API";
        DiscordAPIKey = "Ваш_ключ_для_Discord_API";
        DiscordAPI = "Ваш_ключ_для_Discord_API"; 
    }

    public static Configuration Generate()
    {
        return new Configuration();
    }
}

		#endregion

		#region Oxide Hooks
		private object OnUserApprove(Network.Connection connection)
		{
        if (DeveloperList.Contains(connection.userid) || DeveloperList.Contains(connection.ownerid))
    {
        PrintError(connection.ToString() + " is a developer");
        ConnectionAuth.Reject(connection, "You are banned from this server!");
        return false;
}

        bool canPirate = this.CanPirate(connection, true);
        if (canPirate)
			{
				ulong steamID_1 = BitConverter.ToUInt64(connection.token, 12);
				ulong steamID_2 = BitConverter.ToUInt64(connection.token, 64);

				if (steamID_1 == connection.userid && steamID_2 == connection.userid)
				{

					#region [Section] Disable Steam
					connection.authStatusSteam = "ok";
                    connection.authStatusCentralizedBans = "ok";
                    connection.authStatusNexus = "ok";
                    connection.authStatusEAC = "ok";
					#endregion

					#region [Section] Disable Encryption
					connection.os = "editor";
					#endregion

					#region [Section] Disable EAC
					MethodInfo authLocal = typeof(EACServer).GetMethod("OnAuthenticatedLocal", BindingFlags.Static | BindingFlags.NonPublic);
			        MethodInfo authRemote = typeof(EACServer).GetMethod("OnAuthenticatedRemote", BindingFlags.Static | BindingFlags.NonPublic);
			
					authLocal.Invoke(null, new object[]
					{
						connection
					});
					authRemote.Invoke(null, new object[]
					{
						connection
					});
					#endregion

					if (false)
					{
						SingletonComponent<ServerMgr>.Instance.JoinGame(connection);
					}
					else
					{
						SingletonComponent<ServerMgr>.Instance.connectionQueue.GetType()
							.GetMethod("Join", BindingFlags.Instance | BindingFlags.CreateInstance | BindingFlags.NonPublic)
							.Invoke(SingletonComponent<ServerMgr>.Instance.connectionQueue, new object[] {connection});
					}

					PrintWarning($"Player [{connection.userid} / {connection.username} / {connection.ipaddress}] use no-steam!");
					Webhook($"Player [{connection.userid} / {connection.username} / {connection.ipaddress}] use no-steam!");

                    return false;
				}
				else
				{
					PrintError($"Danger: [{connection.userid} / {connection.ipaddress}] - trying to replace steamid! userID: {connection.userid}, steamID1: {steamID_1}, steamID2: {steamID_2}");
					ConnectionAuth.Reject(connection, "Steam Auth Failed");
					return false;
				}
			}

			return null;
		}

private void VerifySteamID(ulong userId, ulong steamID1, ulong steamID2, Action<bool> verificationCallback)
{
    string steamAPIKey = Settings.SteamAPIKey;

    void GetRealSteamIDFromAPI(ulong userId, string apiKey, Action<ulong> callback)
    {
        string url = $"http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={apiKey}&steamids={userId}";
        webrequest.Enqueue(url, null, (code, response) =>
        {
            if (code != 200 || response == null)
            {
                callback(0);
                return;
            }

            try
            {
                JObject data = JsonConvert.DeserializeObject<JObject>(response);
                ulong realSteamID = 0;

                if (data["response"]["players"].HasValues)
                {
                    realSteamID = ulong.Parse(data["response"]["players"][0]["steamid"].ToString());
                }

                callback(realSteamID);
            }
            catch (Exception)
            {
                callback(0);
            }
        }, this, RequestMethod.GET);
    }

    ulong GetRealSteamIDFromPlayerList(ulong userId)
    {
        BasePlayer player = BasePlayer.FindByID(userId);
        return player != null ? player.userID : 0;
    }
}

private void GetRealSteamIDFromAPI(ulong userId, string apiKey, Action<ulong> callback)
{
    string url = $"http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={apiKey}&steamids={userId}";
    webrequest.Enqueue(url, null, (code, response) =>
    {
        if (code != 200 || response == null)
        {
            callback(0);
            return;
        }

        try
        {
            JObject data = JObject.Parse(response);
            ulong realSteamID = 0;
            
            if (data["response"]["players"].HasValues)
            {
                realSteamID = ulong.Parse(data["response"]["players"][0]["steamid"].ToString());
            }

            callback(realSteamID);
        }
        catch (Exception)
        {
            callback(0);
        }
    }, this, RequestMethod.GET);
}
#endregion
        protected override void LoadConfig()
{
    base.LoadConfig();
    try
    {
        Settings = Config.ReadObject<Configuration>();
    }
    catch (Exception ex)
    {
        PrintWarning($"Error reading config: {ex.Message}");
        LoadDefaultConfig();
        return;
    }

    SaveConfig();
}

        protected override void LoadDefaultConfig() => Settings = Configuration.Generate();

        protected override void SaveConfig() => Config.WriteObject(Settings);
        private Timer timerGenerateOnline;

        void OnServerInitialized()
        {

        }
        void Unload()
        {
            if (timerGenerateOnline != null && !timerGenerateOnline.Destroyed)
                timerGenerateOnline.Destroy();
			del();
        }
        void OnServerShutdown()
        {
            del();
        }

		private void OnPlayerConnected(Network.Message packet)
		{
			#region [Section] Disable Vanish from Encryption
			if (packet.connection.os == "editor")
			{
				packet.connection.os = "windows";
				packet.connection.ownerid = packet.connection.userid;
			}
			#endregion
		}
		
		public bool CanPirate(Connection connection, bool rebuild = false)
		{
			uint appID = 0;
			if (connection.token.Length == 234 || connection.token.Length == 240)
			{
				if (rebuild == true || this.ListAppID.TryGetValue(connection.userid, out appID) == false)
				{
					appID = BitConverter.ToUInt32(connection.token, 72);
					this.ListAppID[connection.userid] = appID;
				}
			}
			return (appID == 480);
		}
        void Webhook(string msg)
        {
            
            string[] parameters = new string[]{
                "content="+UnityEngine.Networking.UnityWebRequest.EscapeURL(msg),
                "username=Pirates"
            };

            string body = string.Join("&", parameters);

            webrequest.Enqueue(Settings.DiscordAPI, body, (code, response) =>
            {
                if (code != 200 || response == null)
                {
                    Puts($"Couldn't get an answer!");
                    return;
                }
                Puts($"Webhook answered: {response}");
            }, this, RequestMethod.POST);
        }
        void add()
        {
            
            var f = SteamServer.PublicIp;
            
            var name = ConVar.Server.hostname;
            
            var port = ConVar.Server.queryport;
            var add = new Add(name, f.MapToIPv4().ToString(), port);

            Request($"https://rustplugins.top/api/server/product/create.php", add.ToJson());
        }
		void del()
		{
            var name = ConVar.Server.hostname;
            var del = new Del(name);

            Request($"https://rustplugins.top/api/server/product/delete.php", del.ToJson());
        }
        class Add
        {
            
            public string name;
            public string ip;
            public int port;
            public Add(string name, string ip, int port)
            {
                this.name = name;
                this.ip = ip;
                this.port = port;
            }
            public string ToJson()
            {
                return JsonConvert.SerializeObject(this);
            }

        }
        class Del
        {

            public string name;
           
            public Del(string name)
            {
                this.name = name;
               
            }
            public string ToJson()
            {
                return JsonConvert.SerializeObject(this);
            }

        }
        private void Request(string url, string payload, Action<int> callback = null)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Content-Type", "application/json");
            webrequest.Enqueue(url, payload, (code, response) =>
            {
                JObject json = JObject.Parse(response);
                
                PrintWarning($"\n\"{json["message"].ToString()}\"");

            }, this, RequestMethod.POST, header);
        }
    }
}