using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using ProtoBuf;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("TeamMarker", "LAGZYA", "1.0.4")]
    public class TeamMarker : RustPlugin
    {
        #region Config

        private const bool En = true;
        private ConfigData cfg { get; set; } 

        private class ConfigData
        {
            
            [JsonProperty(En ? "Setting pings" : "Настройка пингов")]
            public PingSettings _ping = new PingSettings();
            [JsonProperty(En ? "Setting sound" : "Настройка звуков")]
            public SoundSettings _sound = new SoundSettings();
            internal class PingSettings
            {
                
                [JsonProperty(En ? "Color index(0-yellow,1-blue,2-green,3-red,4-purple,5-cyan)" : "Цвет(0-желтый,1-синий,2-зеленый,3-красный,4-фиолетовый,5-голубой)")]
                public int colorIndex = 3;
                [JsonProperty(En ? "Icon type(Hostile = 0,GoTo = 1,Dollar = 2,Loot = 3,Node = 4,Gun = 5)" : "Картинка(Hostile = 0,GoTo = 1,Dollar = 2,Loot = 3,Node = 4,Gun = 5)")]
                public PingType type = PingType.Hostile;
                [JsonProperty(En ? "Max distance ping" : "Максимальная дистанция пинга")]
                public float maxDistance = 250f;
                [JsonProperty(En ? "Duration" : "Время жизни")]
                public float duration = 5f;
            }

            internal class SoundSettings
            {
                [JsonProperty(En ? "Enabled?" : "Включить")]
                public bool enabled = true;
                [JsonProperty(En ? "Effect/sound prefab" : "Effect/sound префаб")]
                public string effect = "assets/bundled/prefabs/fx/invite_notice.prefab";
                [JsonProperty(En ? "Send sound teammates?" : "Отправлять звук команде?")]
                public bool teamSound = true;
                [JsonProperty(En ? "Only nearby teammates?" : "Отправлять звук только напарникам по близости?")]
                public bool nearbyteams = true;
                [JsonProperty(En ? "Max distance" : "Максимальная дистанция для отправки звука")]
                public float nearbyDistance = 150f;
            }

            public static ConfigData GetNewConf()
            {
                var newConfig = new ConfigData();
                return newConfig;
            }
        }

        protected override void LoadDefaultConfig()
        {
            cfg = ConfigData.GetNewConf();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(cfg);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                cfg = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        #endregion
        private void OnPlayerInput(BasePlayer player, InputState state)
        {
            if(player == null || state == null || player.modelState.aiming == false) return;
            if (!state.WasJustPressed(BUTTON.USE)) return;
            if( player.eyes == null) return;
            var ray = player.eyes.HeadRay();
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, cfg._ping.maxDistance, LayerMask.GetMask("Default", "Terrain", "Construction", "Water", "Deployed", "Tree", "Debris", "World", "Player (Server)", "AI", "Ragdoll")))
            {
                CreatePing(player, hit.point);
            }

        }

        private void CreatePing(BasePlayer player, Vector3 pos)
        {
var activeItem = player.GetActiveItem();
if (activeItem != null)
{
    // Dürbün ve jackhammer için engelleme
    string[] blockedItems = { "tool.binoculars", "jackhammer" };
    if (Array.Exists(blockedItems, item => item == activeItem.info.shortname))
    {
        return;
    }
}
            MapNote n = new MapNote()
            {
                isPing = true,
                timeRemaining = cfg._ping.duration,
                totalDuration = 0,
                colourIndex = cfg._ping.colorIndex,
                icon = 1,
                noteType = 1,
                worldPosition = pos,
                ShouldPool = true
            };
            
            if (player.State.pings == null)
            {
                player.State.pings = new List<MapNote>();
            }

            if (player.State.pings.Count >= ConVar.Server.maximumPings)
                player.State.pings.RemoveAt(0);
            
            ValidateMapNote(n);
            ApplyPingStyle(n, cfg._ping.type);
            player.State.pings.Add(n);
            player.DirtyPlayerState();
            if (cfg._sound.enabled)
            {
                Effect reusableSoundEffectInstance = new Effect();
                if (player.Team != null && player.Team.GetOnlineMemberConnections().Count > 1 && cfg._sound.teamSound )
                {
                    foreach (var onlineMemberConnection in player.Team.GetOnlineMemberConnections())
                    {
                        if (onlineMemberConnection.userid == player.userID) continue;
                        var target = onlineMemberConnection.player as BasePlayer;
                        if (target == null) continue;
                        if(cfg._sound.nearbyteams)
                        {
                            
                            reusableSoundEffectInstance.Init(Effect.Type.Generic, target,0, Vector3.zero, Vector3.forward);
                            reusableSoundEffectInstance.pooledString = cfg._sound.effect;
                            if( Vector3.Distance(player.transform.position, target.transform.position) <= cfg._sound.nearbyDistance) 
                                EffectNetwork.Send(reusableSoundEffectInstance, target.Connection);
                        }
                        else
                        {
                            reusableSoundEffectInstance.Init(Effect.Type.Generic, target,0, Vector3.zero, Vector3.forward);
                            reusableSoundEffectInstance.pooledString = cfg._sound.effect;
                            EffectNetwork.Send(reusableSoundEffectInstance, target.Connection);
                        }
                    }
                }
                
                reusableSoundEffectInstance.Init(Effect.Type.Generic, player, 0, Vector3.zero, Vector3.forward);
                reusableSoundEffectInstance.pooledString = cfg._sound.effect;
                EffectNetwork.Send(reusableSoundEffectInstance, player.Connection);
            }
            SendPingsToClient(player);
            player.TeamUpdate(true);
            
        }
        private static void SendPingsToClient(BasePlayer player)
        {
            using (MapNoteList mapNoteList = Facepunch.Pool.Get<MapNoteList>())
            {
                mapNoteList.notes = Facepunch.Pool.GetList<MapNote>();
                mapNoteList.notes.AddRange((IEnumerable<MapNote>)player.State.pings);
                player.ClientRPCPlayer<MapNoteList>((Network.Connection)null, player, "Client_ReceivePings", mapNoteList);
                mapNoteList.notes.Clear();
            }
        }

        private struct PingStyle
        {
            public int IconIndex;
            public int ColourIndex;
            public Translate.Phrase PingTitle;
            public Translate.Phrase PingDescription;
            public BasePlayer.PingType Type;

            public PingStyle(
                int icon,
                int colour,
                Translate.Phrase title,
                Translate.Phrase desc,
                BasePlayer.PingType pType)
            {
                this.IconIndex = icon;
                this.ColourIndex = colour;
                this.PingTitle = title;
                this.PingDescription = desc;
                this.Type = pType;
            }
        }

        public enum PingType
        {
            Hostile = 0,
            GoTo = 1,
            Dollar = 2,
            Loot = 3,
            Node = 4,
            Gun = 5
        }

        private void ApplyPingStyle(MapNote note, PingType type)
        {
            PingStyle pingStyle = new PingStyle();
            switch (type)
            {
                case PingType.Hostile:
                    pingStyle = HostileMarker;
                    break;
                case PingType.GoTo:
                    pingStyle = GoToMarker;
                    break;
                case PingType.Dollar:
                    pingStyle = DollarMarker;
                    break;
                case PingType.Loot:
                    pingStyle = LootMarker;
                    break;
                case PingType.Node:
                    pingStyle = NodeMarker;
                    break;
                case PingType.Gun:
                    pingStyle = GunMarker;
                    break;
            }
            note.icon = pingStyle.IconIndex;
        }
        private void ValidateMapNote(MapNote n)
        {
            if (n.label == null)
                return;
            n.label = n.label.Truncate(10).ToUpperInvariant();
        }
         private static readonly Translate.Phrase HostileTitle = new Translate.Phrase("ping_hostile", "Hostile");
        private static readonly Translate.Phrase HostileDesc = new Translate.Phrase("ping_hostile_desc", "Danger in area");
        private static readonly PingStyle HostileMarker = new PingStyle(4, 3, HostileTitle, HostileDesc, BasePlayer.PingType.Hostile);
        private static readonly Translate.Phrase GoToTitle = new Translate.Phrase("ping_goto", "Go To");
        private static readonly Translate.Phrase GoToDesc = new Translate.Phrase("ping_goto_desc", "Look at this");
        private static readonly PingStyle GoToMarker = new PingStyle(0, 2, GoToTitle, GoToDesc, BasePlayer.PingType.GoTo);
        private static readonly Translate.Phrase DollarTitle = new Translate.Phrase("ping_dollar", "Value");
        private static readonly Translate.Phrase DollarDesc = new Translate.Phrase("ping_dollar_desc", "Something valuable is here");
        private static readonly PingStyle DollarMarker = new PingStyle(1, 1, DollarTitle, DollarDesc, BasePlayer.PingType.Dollar);
        private static readonly Translate.Phrase LootTitle = new Translate.Phrase("ping_loot", "Loot");
        private static readonly Translate.Phrase LootDesc = new Translate.Phrase("ping_loot_desc", "Loot is here");
        private static readonly PingStyle LootMarker = new PingStyle(11, 0, LootTitle, LootDesc, BasePlayer.PingType.Loot);
        private static readonly Translate.Phrase NodeTitle = new Translate.Phrase("ping_node", "Node");
        private static readonly Translate.Phrase NodeDesc = new Translate.Phrase("ping_node_desc", "An ore node is here");
        private static readonly PingStyle NodeMarker = new PingStyle(10, 4, NodeTitle, NodeDesc, BasePlayer.PingType.Node);
        private static readonly Translate.Phrase GunTitle = new Translate.Phrase("ping_gun", "Weapon");
        private static readonly Translate.Phrase GunDesc = new Translate.Phrase("ping_weapon_desc", "A dropped weapon is here");
        private static readonly PingStyle GunMarker = new PingStyle(9, 5, GunTitle, GunDesc, BasePlayer.PingType.Gun);
    }
}