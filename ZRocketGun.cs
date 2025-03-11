using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using UnityEngine;
using System;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("ZRocketGun", "Zwe1st", "0.0.1")]
    [Description("Добавляет уникальное оружие, стреляющее ракетами")]
    public class ZRocketGun : RustPlugin
    {
        private Configuration config;

        private class Configuration
        {
            [JsonProperty("GunShortname")]
            public string GunShortname = "pistol.revolver";

            [JsonProperty("GunSkinID")]
            public ulong GunSkinID = 0;

            [JsonProperty("PermissionName")]
            public string PermissionName = "rocketgun.use";

            [JsonProperty("RocketGunDisplayName")]
            public string RocketGunDisplayName = "Ракетное Оружие";

            [JsonProperty("CustomRocketPrefab")]
            public string CustomRocketPrefab = "assets/prefabs/ammo/rocket/rocket_basic.prefab";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        private void Init()
        {
            permission.RegisterPermission(config.PermissionName, this);
            cmd.AddChatCommand("rg", this, "CmdRocketGun");
        }

        [ChatCommand("rg")]
        private void CmdRocketGun(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, config.PermissionName))
            {
                player.ChatMessage("У вас нет разрешения на использование этой команды!");
                return;
            }

            Item item = ItemManager.CreateByName(config.GunShortname);
            if (item == null)
            {
                PrintError($"Неверный shortname предмета: {config.GunShortname}");
                return;
            }

            item.name = config.RocketGunDisplayName;
            if (config.GunSkinID > 0)
                item.skin = config.GunSkinID;

            player.GiveItem(item);
            player.ChatMessage($"Вы получили {config.RocketGunDisplayName}!");
        }

        private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (attacker == null || info == null || info.Weapon == null) return;

            Item weapon = info.Weapon.GetItem();
            if (weapon == null || weapon.name != config.RocketGunDisplayName) return;

            Vector3 position = info.HitPositionWorld;
            if (position == default(Vector3)) return;

            BaseEntity rocket = GameManager.server.CreateEntity(config.CustomRocketPrefab, position, new Quaternion());
            if (rocket == null) return;

            rocket.creatorEntity = attacker;
            rocket.Spawn();
        }
    }
} 