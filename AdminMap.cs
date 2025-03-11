using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Admin Map", "Mr. Blue", "0.0.5")]
    [Description("Allows admins to show all players on the map")]
    class AdminMap : HurtworldPlugin
    {
        #region Variables
        private List<PlayerSession> adminsToggled = new List<PlayerSession>();
        private const string perm = "adminmap.use";
        #endregion

        #region Loading
        void Init()
        {
            permission.RegisterPermission(perm, this);
        }
        void Unload()
        {
            foreach (PlayerSession playerSession in adminsToggled)
                DisableMarkers(playerSession);
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> {
                { "AdminMapEnabled", "<color=orange>[Admin Map]</color> Enabled" },
                { "AdminMapDisabled", "<color=orange>[Admin Map]</color> Disabled" },
                { "AdminMapReloading", "<color=orange>[Admin Map]</color> Reloading" },
                { "AdminMapReloaded", "<color=orange>[Admin Map]</color> Reloaded" },
                { "NoPermissions", "<color=orange>[Admin Map]</color> You don't have the permission to use this command!" }
            }, this);
        }

        string Msg(string msg, string SteamId = null) => lang.GetMessage(msg, this, SteamId);
        #endregion

        #region Reloading
        void OnPlayerDisconnected(PlayerSession session)
        {
            if (adminsToggled.Contains(session))
                adminsToggled.Remove(session);
        }
        void OnPlayerSpawned() => ReloadAdminMaps();
        void OnPlayerRespawn() => ReloadAdminMaps();
        void OnClanMemberAdded() => ReloadAdminMaps();
        void OnClanMemberRemoved() => ReloadAdminMaps();
        void OnClanCreate() => ReloadAdminMaps();
        #endregion

        #region MapLogic
        private void ReloadAdminMaps()
        {
            foreach (PlayerSession playerSession in adminsToggled)
            {
                string steamId = playerSession.SteamId.ToString();
                AlertManager.Instance.GenericTextNotificationServer(Msg("AdminMapReloading", steamId), playerSession.Player);
                EnableMarkers(playerSession);
                AlertManager.Instance.GenericTextNotificationServer(Msg("AdminMapReloaded", steamId), playerSession.Player);
            }
        }

        private void EnableMarkers(PlayerSession session)
        {
            foreach (PlayerSession playerSession in GameManager.Instance.GetSessions().Values)
            {
                if (playerSession?.WorldPlayerEntity?.Transform?.gameObject?.GetComponent<PlayerStatManager>() == null) continue;
                PlayerStatManager playerStatManager = playerSession.WorldPlayerEntity.Transform.gameObject.GetComponent<PlayerStatManager>();
                if (playerStatManager?.PlayerMapMarker == null) continue;
                MapMarkerData mapMarkerData = playerStatManager.PlayerMapMarker;
                mapMarkerData.AuthorisedPlayers.Add(session.Identity);
                mapMarkerData.Label = playerSession.Identity.Name;
                mapMarkerData.NetworkObject.InvalidatePlayerAuth();
            }
        }

        private void DisableMarkers(PlayerSession session)
        {
            foreach (PlayerSession playerSession in GameManager.Instance.GetSessions().Values)
            {
                if (playerSession?.WorldPlayerEntity?.Transform?.gameObject?.GetComponent<PlayerStatManager>() == null) continue;
                PlayerStatManager playerStatManager = playerSession.WorldPlayerEntity.Transform.gameObject.GetComponent<PlayerStatManager>();
                playerStatManager.UpdateClanTracking();
            }
        }
        #endregion

        #region ChatCommands
        [ChatCommand("adminmap")]
        private void TogglePlayersCommand(PlayerSession session, string command, string[] args)
        {
            string steamId = session.SteamId.ToString();

            if (!permission.UserHasPermission(steamId, perm))
            {
                Player.Message(session, Msg("NoPermissions", steamId));
                return;
            }

            if (adminsToggled.Contains(session))
            {
                DisableMarkers(session);
                adminsToggled.Remove(session);
                AlertManager.Instance.GenericTextNotificationServer(Msg("AdminMapDisabled", steamId), session.Player);
            }
            else
            {
                adminsToggled.Add(session);
                EnableMarkers(session);
                AlertManager.Instance.GenericTextNotificationServer(Msg("AdminMapEnabled", steamId), session.Player);
            }
        }
        #endregion
    }
}