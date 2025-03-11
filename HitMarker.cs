// Reference: System.Drawing
using Facepunch;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("HitMarker", "Adoxid", "1.0.3")]
    class HitMarker : RustPlugin
    {
        #region CONFIGURATION

        private bool Changed;
        private bool enablesound;
        private string soundeffect;
        private string headshotsoundeffect;
        private float damageTimeout;
		
        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }        

        protected override void LoadDefaultConfig()
        {
            enablesound = Convert.ToBoolean( GetConfig( "Sound", "EnableSoundEffect", true ) );
            soundeffect =
                Convert.ToString( GetConfig( "Sound", "Sound Effect", "assets/bundled/prefabs/fx/takedamage_hit.prefab" ) );
            headshotsoundeffect =
                Convert.ToString( GetConfig( "Sound", "HeadshotSoundEffect", "assets/bundled/prefabs/fx/headshot.prefab" ) );
            GetVariable(Config, "Через сколько будет пропадать урон", out damageTimeout, 0.5f );
            SaveConfig();
        }
        public static void GetVariable<T>( DynamicConfigFile config, string name, out T value, T defaultValue )
        {
            config[ name ] = value = config[ name ] == null ? defaultValue : (T) Convert.ChangeType( config[ name ], typeof( T ) );
        }
        #endregion
        
        #region FIELDS

        [PluginReference] private Plugin Clans;
        
		Random rnd = new Random();
		
        List<BasePlayer> hitmarkeron = new List<BasePlayer>();

        Dictionary<BasePlayer, List<KeyValuePair<float, HitNfo>>> damageHistory = new Dictionary<BasePlayer, List<KeyValuePair<float, HitNfo>>>();

		class HitNfo
		{
			public int damage;
			public bool isHead;
			public bool isFriend;
			public double xs;
			public double ys;
			public double xe;
			public double ye;
			public int num;
		}
		
        Dictionary<BasePlayer, Oxide.Plugins.Timer> destTimers = new Dictionary<BasePlayer, Oxide.Plugins.Timer>();
        #endregion

        #region COMMANDS

        [ChatCommand("hitmarker")]
        void cmdHitMarker(BasePlayer player, string cmd, string[] args)
        {
            if (!hitmarkeron.Contains(player))
            {
                hitmarkeron.Add(player);
                SendReply(player,
                    "<color=cyan>HitMarker</color>:" + " " + "<color=orange>Вы включили показ урона.</color>");
            }
            else
            {
                hitmarkeron.Remove(player);
                SendReply(player,
                    "<color=cyan>HitMarker</color>:" + " " + "<color=orange>Вы отключили показ урона.</color>");
            }
        }

        #endregion

        #region OXIDE HOOKS

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                hitmarkeron.Remove(player);
                damageHistory.Remove(player);
            }
        }

        void OnServerInitialized()
        {            
            LoadDefaultConfig();
            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                hitmarkeron.Add(current);
            }            
            timer.Every(0.1f, OnDamageTimer);
        }        

        void OnPlayerInit(BasePlayer player)
        {
            hitmarkeron.Add(player);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            hitmarkeron.Remove(player);
            damageHistory.Remove(player);
        }
        void OnPlayerAttack(BasePlayer attacker, HitInfo hitinfo)
        {
            var victim = hitinfo.HitEntity as BasePlayer;
            if (victim && hitmarkeron.Contains(attacker))
            {                
                if (hitinfo.isHeadshot)
                {
                    if (enablesound == true)
                    {
                        Effect.server.Run(headshotsoundeffect, attacker.transform.position, Vector3.zero,
                            attacker.net.connection);
                    }
                }
                else
                {
                    if (enablesound)
                    {
                        Effect.server.Run(soundeffect, attacker.transform.position, Vector3.zero,
                            attacker.net.connection);
                    }
                }
            }
        }

        string DamageGUI = "[{\"name\":\"hitmarkerDamage{0}\",\"parent\":\"Hud\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{1}\"},{\"type\":\"UnityEngine.UI.Outline\",\"color\":\"0 0 0 1\",\"distance\":\"0.3 -0.3\"},{\"type\":\"RectTransform\",\"anchormin\":\"{2} {3}\",\"anchormax\":\"{4} {5}\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]}]" ;
		string ActionGUI = "[{\"name\":\"hitmarkerAction{0}\",\"parent\":\"Hud\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{1}\"},{\"type\":\"UnityEngine.UI.Outline\",\"color\":\"0 0 0 1\",\"distance\":\"0.3 -0.3\"},{\"type\":\"RectTransform\",\"anchormin\":\"{2} {3}\",\"anchormax\":\"{4} {5}\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]}]" ;        
		
        string HandleArgs( string json, params object[] args )
        {
            for (int i = 0; i < args.Length; i++)
                json = json.Replace( "{" + i + "}", args[ i ].ToString() );
            return json;
        }
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            var victim = entity as BasePlayer;
            if (victim == null || hitInfo == null) return;
            DamageType type = hitInfo.damageTypes.GetMajorityDamageType();
            var attacker = hitInfo.InitiatorPlayer;
            if (attacker == null) return;
			
			var isHead = hitInfo.isHeadshot;
			bool isFriend = (Clans?.Call("HasFriend", attacker.userID, (victim as BasePlayer).userID) as bool?) ?? false;
            NextTick(() =>
            {
                var damage =
                    System.Convert.ToInt32(Math.Round(hitInfo.damageTypes.Total(), 0, MidpointRounding.AwayFromZero));
                DamageNotifier(attacker, damage, isHead, isFriend);
            });
        }

        void OnPlayerWound( BasePlayer player )
        {
            var attacker = player?.lastAttacker as BasePlayer;
            if (attacker == null) return;

            DestroyLastCui( attacker );

			var deltaX = GetRandomX();
			var deltaY = GetRandomY();
			
			var rn = rnd.Next(0, 10000);
			
            CuiHelper.AddUi( attacker,
                HandleArgs( ActionGUI, rn, GetDamageText("wound"), 0.4919792+deltaX, 0.4531481+deltaY, 0.675+deltaX, 0.5587038+deltaY ) );
            destTimers[ attacker ] = timer.Once(damageTimeout, () =>
            {
                CuiHelper.DestroyUi( attacker, "hitmarkerAction" +rn.ToString() );
            } );
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var player = entity as BasePlayer;
            if (player == null) return;
            var attacker = info?.Initiator as BasePlayer;
            if (attacker == null) return;

            DestroyLastCui(attacker);

			var deltaX = GetRandomX();
			var deltaY = GetRandomY();
			
			var rn = rnd.Next(0, 10000);
			
            CuiHelper.AddUi( attacker,
                HandleArgs( ActionGUI, rn, GetDamageText("kill"), 0.4919792+deltaX, 0.4531481+deltaY, 0.675+deltaX, 0.5587038+deltaY ) );
            destTimers[ attacker ] = timer.Once(damageTimeout, () =>
            {
                CuiHelper.DestroyUi( attacker, "hitmarkerAction" +rn.ToString() );
            } );
        }
        #endregion

        #region Core

        void OnDamageTimer()
        {            
            var toRemove = Pool.GetList<BasePlayer>(); 
            foreach (var dmgHistoryKVP in damageHistory)
            {				
                DrawDamageNotifier( dmgHistoryKVP.Key );
                if (dmgHistoryKVP.Value.Count == 0)
                    toRemove.Add(dmgHistoryKVP.Key);
            }
            toRemove.ForEach(p=>damageHistory.Remove(p));
            Pool.FreeList(ref toRemove);
        }

        void DamageNotifier(BasePlayer player, int damage, bool isHead, bool isFriend)
        {
            List<KeyValuePair<float, HitNfo>> damages;
            if (!damageHistory.TryGetValue(player, out damages))
                damageHistory[player] = damages = new List<KeyValuePair<float, HitNfo>>();
			
			var deltaX = GetRandomX();
			var deltaY = GetRandomY();
			
            damages.Insert(0,new KeyValuePair<float, HitNfo>(Time.time+ damageTimeout, new HitNfo() { damage = damage, isHead = isHead, isFriend = isFriend, xs=0.4919792+deltaX, ys=0.4531481+deltaY, xe=0.675+deltaX, ye=0.5587038+deltaY, num=rnd.Next(0,10000) }) );
           
            DrawDamageNotifier(player);
        }        
		
		string GetDamageText(string action)
		{
			switch (action)
			{
				case "wound": return "<color=#FF7979>УПАЛ!</color>";							  
				case "kill":  return "<color=red>УБИТ!</color>";							  
			}
			
			return "<color=white>ПОПАЛ!</color>";
		}

        void DestroyLastCui(BasePlayer player)
        {
            Oxide.Plugins.Timer tmr;
            if (destTimers.TryGetValue(player, out tmr))
            {
                tmr?.Callback?.Invoke();
                if (tmr != null && !tmr.Destroyed)
                    timer.Destroy(ref tmr);
            }
        }
        
        #endregion

        #region UI
		
		float GetRandomX()
		{
			return (rnd.Next(0,101)-50)/2000f;
		}
		
		float GetRandomY()
		{
			return -0.1f-rnd.Next(0,101)/2000f;
		}

        void DrawDamageNotifier(BasePlayer player)
        {						
			List<KeyValuePair<float, HitNfo>> damages;
            if (!damageHistory.TryGetValue(player, out damages)) return;
			
			float time = Time.time;			
            for (var i = damages.Count-1; i >= 0; i--)
            {
				var item = damages[i];	
                CuiHelper.DestroyUi(player, "hitmarkerDamage"+item.Value.num.ToString());										
				if (item.Key < time)
					damages.RemoveAt(i);
				else
					CuiHelper.AddUi( player, HandleArgs( DamageGUI, item.Value.num, $"<color={item.Value.isFriend ? "green" : (item.Value.isHead ? "red" : "white")}>-{item.Value.damage}</color>", item.Value.xs, item.Value.ys, item.Value.xe, item.Value.ye ) );
                destTimers[player] = timer.Once(damageTimeout, () =>
                {
                    CuiHelper.DestroyUi(player, "hitmarkerDamage" + item.Value.num.ToString());
                });
            }			            
        }        

        #endregion
        
    }
}



