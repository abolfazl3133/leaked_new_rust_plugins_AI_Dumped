using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("TicketSystem", "https://discord.gg/9vyTXsJyKR", "1.0.0")]
    [Description("Плагин для связи с игроком-администратором.")]

    public class TicketSystem : RustPlugin
    {
        StoredData storedData;
        string DataFile = "ticket_data";

        private bool OnlineAdmins = true;
        private string AdminPerm = "ticketsystem.moder";
        private string SuperAdminPerm = "ticketsystem.admin";
        string HelpTitle = "<size=24>Ticket System - </size><size=18>by topplugin.ru</size>";
        string HelpTitleOtp = "[TicketSystem - topplugin.ru]";
        private string TextLogo = "#66ff66";
        private string TextLogoCommands = "#66ff66";
        private string TextLogoCommandsTitle = "#66ff66";
        private string AdminsOnlineColor = "#66ff66";
        private string TicketCreateColor = "#66ff66";
        private string TextLogoCommandsInfo = "#66ff66";
        private string HelpTitleOtpColor = "#5beaea";
        private string SendMessageCol = "Нельзя отправлять так часто тикеты! Подождите {0:0} сек. и повторите еще раз!";
        private double Cooldown = 120f;


        private Dictionary<BasePlayer, DateTime> Cooldowns = new Dictionary<BasePlayer, DateTime>();
        Dictionary<string, string> Commands;
        Dictionary<string, string> AdminCommands;
        Dictionary<string, string> SuperAdminCommands;

        #region Config
        private void LoadDefaultConfig()
        {

            GetConfig("Основные настройки", "Привилегия для модераторов", ref AdminPerm);
            GetConfig("Основные настройки", "Привилегия для администраторов", ref SuperAdminPerm);
            GetConfig("Основные настройки", "Влючить команду /admins?", ref OnlineAdmins);
            GetConfig("Основные настройки", "Подпись к страницы помощи по командам", ref HelpTitle);
            GetConfig("Основные настройки", "Подпись при отправке/получение тикета", ref HelpTitleOtp);
            GetConfig("Основные настройки", "Задержка перед повторной отправкой тикета (сек)", ref Cooldown);
            GetConfig("Основные настройки", "Сообщение о задержке перед отправлением", ref SendMessageCol);


            GetConfig("Настройка цветов", "Цвет 'Титла'", ref TextLogo);
            GetConfig("Настройка цветов", "Цвет 'Доступные команды'", ref TextLogoCommands);
            GetConfig("Настройка цветов", "Цвет 'Доступные команды (Самих команд)'", ref TextLogoCommands);
            GetConfig("Настройка цветов", "Цвет 'Админы онлайн'", ref AdminsOnlineColor);
            GetConfig("Настройка цветов", "Цвет 'Доступные команды (Описание к командам)'", ref TextLogoCommandsInfo);
            GetConfig("Настройка цветов", "Цвет 'Цвет Подписи при отправке/получение тикета'", ref HelpTitleOtpColor);
            SaveConfig();
        }


        private void GetConfig<T>(string MainMenu, string Key, ref T var)
        {
            if (Config[MainMenu, Key] != null)
            {
                var = (T)Convert.ChangeType(Config[MainMenu, Key], typeof(T));
            }
            Config[MainMenu, Key] = var;
        }
        #endregion

        void OnServerInitialized()
        {
            PrintWarning("\n-----------------------------\n" +
            "     Author - https://topplugin.ru/\n" +
            "     VK - https://vk.com/rustnastroika\n" +
            "     Discord - https://discord.com/invite/5DPTsRmd3G\n" +
            "-----------------------------");
            LoadConfig();
            LoadDefaultConfig();
            PermissionService.RegisterPermissions(this, new List<string>() { AdminPerm, SuperAdminPerm });
        }

        void Loaded()
        {
            openDataFile();
            Commands = new Dictionary<string, string>
            {
                { "/ticket <сообщение>", "Создание тикета." },
                { "/calladmin <причина>", "Посылает запрос администратору, для телепортации к Вам!" },
                { "/admins", "Показывает список онлайн администраторов." },
            };

            AdminCommands = new Dictionary<string, string>
            {
                { "/ticket tp <id>", "Телепортироваться на запрос игрока по номеру ID." },
                { "/ticket list <page>", "Список всех открытых тикетов/выбор страницы" },
                { "/ticket view <id>", "Просмотр сведений о конкретном тикете." }

            };
            SuperAdminCommands = new Dictionary<string, string>
            {
                { "/ticket del <id>", "Удалить тикет с определенным ID " },
                { "/ticket clear", "Удалить все тикеты." }
            };
        }

        #region PlayerCommands


        [ChatCommand("calladmin")]
        void onCommandCallAdmin(BasePlayer sender, string command, string[] args)
        {
            string msg = "";
            foreach (string arg in args)
                msg += $"{arg} ";

            sendToAdmins($"{col("#66ff66", sender.displayName)} обратился за помощью.\nПричина: {col("#66ff66", msg)}");

            if (adminOnline.Count > 0)
                sendMessage(sender, "Администратор был уведомлен о вашем запросе на телепорт.");
            else
                sendMessage(sender, "В настоящее время администраторов нет. Пожалуйста, оставьте тикет /ticket <message>");
        }

        [ChatCommand("admins")]
        void onCommandAdmins(BasePlayer player, string command, string[] args)
        {
            if (!OnlineAdmins) return;

            if (adminOnline.Count <= 0)
            { sendMessage(player, "На данный момент нет администраторов в сети."); return; }

            string msg = $"{col(AdminsOnlineColor, "Admins Online:")}\n";
            msg += string.Join("\n", adminOnline.Select(p => $"> {p.displayName}").ToArray());
            //if (player.IsAdmin || PermissionService.HasPermission(player.userID, AdminPerm))
            //    msg += $"> {player.displayName}\n";
            sendMessage(player, msg, false);
        }
        #endregion

        #region AdminCommands



        [ChatCommand("ticket")]
        private void onCommandsTicket(BasePlayer player, string cmd, string[] Args)
        {

            if (Args == null || Args.Length == 0)
            {
                showHelp(player);
                return;
            }

            switch (Args[0])
            {
                case "list":

                    if (!player.IsAdmin && !PermissionService.HasPermission(player.userID, AdminPerm))
                    { sendMessage(player, col("#cc3f3f", "Insufficient permissions.")); return; }
                    int page = 0;
                    if (Args == null || Args.Length == 1)
                    {
                        showTicketList(player, page);
                        return;
                    }
                    page = Int32.Parse(Args[1]);
                    showTicketList(player, page);
                    return;
                case "clear":
                    if (!player.IsAdmin && !PermissionService.HasPermission(player.userID, SuperAdminPerm))
                    { sendMessage(player, col("#cc3f3f", "Insufficient permissions.")); return; }

                    storedData.AllTickets.Clear();
                    sendMessage(player, "You have cleared the tickets list.");
                    writeFile();
                    return;
                case "view":
                    if (!player.IsAdmin && !PermissionService.HasPermission(player.userID, AdminPerm))
                    { sendMessage(player, col("#cc3f3f", "Insufficient permissions.")); return; }
                    if (Args == null || Args.Length == 1)
                    {
                        SendReply(player, "Вы не правильно используете команду. \nПример: /ticket view (Номер тикета)");
                        return;
                    }
                    var ticketNum = Int32.Parse(Args[1]);
                    Ticket ticket = getTicket(ticketNum);
                    sendMessage(player, getTicketData(ticket), false);
                    return;
                case "del":

                    if (!player.IsAdmin && !PermissionService.HasPermission(player.userID, SuperAdminPerm))
                    { sendMessage(player, col("#cc3f3f", "Insufficient permissions.")); return; }
                    if (Args == null || Args.Length == 1)
                    {
                        SendReply(player, "Вы не правильно используете команду. \nПример: /ticket del (Номер тикета)");
                        return;
                    }
                    int ticketNum1 = Int32.Parse(Args[1]);
                    Ticket ticket1 = getTicket(ticketNum1);
                    storedData.AllTickets.Remove(ticket1);
                    writeFile();
                    sendMessage(player, $"Тикет: [{ticket1.ticketID}] {player.displayName} был закрыт.", true);
                    return;
                case "tp":
                    if (!player.IsAdmin && !PermissionService.HasPermission(player.userID, AdminPerm))
                    { sendMessage(player, col("#cc3f3f", "Insufficient permissions.")); return; }
                    if (Args == null || Args.Length == 1)
                    {
                        SendReply(player, "Вы не правильно используете команду. \nПример: /ticket tp (Номер тикета)");
                        return;
                    }
                    int ticketNum2 = Int32.Parse(Args[1]);
                    Ticket ticket2 = getTicket(ticketNum2);
                    Teleport(player, new Vector3(ticket2.x, ticket2.y, ticket2.z));
                    sendMessage(player, $"Вы были телепортированы в место, где был открыт запрос.");
                    return;

                    
                default:
                    string msg = "";
                    foreach (string arg in Args)
                        msg += $"{arg} ";
                    if (Cooldowns.ContainsKey(player))
                    {
                        double seconds = Cooldowns[player].Subtract(DateTime.Now).TotalSeconds;
                        if (seconds >= 0)
                        {
                            player.ChatMessage(string.Format($"{SendMessageCol}", seconds));
                            return;
                        }
                    }
                    Cooldowns[player] = DateTime.Now.AddSeconds(Cooldown);
                    player.ChatMessage($"");
                    newTicket(player, $"{msg}");

                    VKBot?.Call("VKAPIChatMsg", $"Создан новый тикет от {player.displayName} [{player.userID}]\n" +
                                               $"Текст сообщений: {msg}");
                    
                    if (adminOnline.Count >= 1)
                        sendMessage(player, $"Ваш тикет был отправлен и будет рассмотрен администратором в ближайшее время.");
                    else
                        sendMessage(player, $"Ваш тикет был отправлен и будет рассмотрен администратором когда он будет в сети.");
                    return;
            }
        }

        [PluginReference] private Plugin VKBot;



        #endregion

        #region Tickets
        Ticket getTicket(int id)
        {
            foreach (Ticket t in storedData.AllTickets)
                if (t.ticketID == id)
                    return t;
            return null;
        }
        void newTicket(BasePlayer player, string message)
        {
            Ticket ticket = new Ticket(player.displayName, player.userID, message, player.transform.position, DateTime.Now.ToLongDateString(), DateTime.Now.ToLongTimeString());
            ticket.ticketID = generateUniqueID();
            storedData.AllTickets.Add(ticket);
            writeFile();
            sendToAdmins(getTicketData(ticket));
        }
        void showTicketList(BasePlayer player, int page)
        {
            string msg = $"Страница тикетов {page}:\n";
            int perPage = 7;
            for (int i = (perPage * page); i < storedData.AllTickets.Count; i++)
            {
                Ticket t = storedData.AllTickets[i];
                if (i < perPage * (page + 1))
                    msg += $"\n[{t.ticketID.ToString()}] -> [{col("#66ff66", t.myDateS)}] {t.senderName} : {col("#66ff66", t.message)}";
                else
                    break;
            }
            sendMessage(player, msg, false);
        } // Displays the page
        string getTicketData(Ticket t)
        {
            return $"\n{t.senderName} создал новый тикет:\n\nИмя: {col("#66ff66", t.senderName)}\nСообщение: {col("#518eef", t.message)}\nКоординаты: {col("#66ff66", t.x.ToString())}, {col("#66ff66", t.y.ToString())}, {col("#66ff66", t.z.ToString())}\nДата: {col("#66ff66", t.myDate)}\nВремя: {col("#66ff66", t.myTime)}\nТикет ID: {col("#66ff66", t.ticketID.ToString())}";
        } // Sends the ticket info to player
        #endregion

        #region PlayerInformation
        private List<BasePlayer> adminOnline => BasePlayer.activePlayerList.Where(p => PermissionService.HasPermission(p.userID, AdminPerm)).ToList();

        void sendToAll(string msg, bool prefix = true)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                sendMessage(player, msg, prefix);
        } // Sends a message to all active users
        void sendToAdmins(string msg)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                if (player.IsAdmin || PermissionService.HasPermission(player.userID, AdminPerm))
                    sendMessage(player, msg);
        } // Sends a message to all admins
        void sendMessage(BasePlayer player, string message, bool prefix = true)
        {
            if (prefix == false)
                PrintToChat(player, $"{message}");
            else
                PrintToChat(player, $"{getPrefix()} {message}");
        } // Sends a formatted message
        void showHelp(BasePlayer player)
        {
            string msg = $"{HelpTitle}\n" + $"{col(TextLogo, "<size=16>Доступные команды:</size>")}\n";
            foreach (string s in Commands.Keys)
                msg += $"- {col(TextLogoCommands, s)}: {Commands[s]}\n";

            if (player.IsAdmin || PermissionService.HasPermission(player.userID, AdminPerm))
                foreach (string s in AdminCommands.Keys)
                    msg += $"- {col(TextLogoCommands, s)}: {AdminCommands[s]}\n";
            if (player.IsAdmin || PermissionService.HasPermission(player.userID, SuperAdminPerm))
                foreach (string s in SuperAdminCommands.Keys)
                    msg += $"- {col(TextLogoCommands, s)}: {SuperAdminCommands[s]}\n";
            sendMessage(player, msg, false);
        } // Shows commands list
        #endregion

        #region Data
        void writeFile() { Interface.Oxide.DataFileSystem.WriteObject(DataFile, storedData); } // Do this when changes are made
        void openDataFile() { storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(DataFile); } // Only do this once
        #endregion

        #region StringFormatting
        string col(string colour, string text)
        {
            return $"<color={colour}>{text}</color>";
        } // Adds colour to the string
        string getPrefix()
        {
            return $"<color={HelpTitleOtpColor}>{HelpTitleOtp}</color>";
        } // Returns formatted prefix
        #endregion

        #region Misc
        void Teleport(BasePlayer player, Vector3 position)
        {
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "StartLoading");
            StartSleeping(player);
            player.MovePosition(position);
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "ForcePositionTo", position);
            if (player.net?.connection != null)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate(false);
            if (player.net?.connection == null) return;
            try { player.ClearEntityQueue(null); } catch { }
            player.SendFullSnapshot();
        } // teleport to position as sleeper
        void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping())
                return;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);
            player.CancelInvoke("InventoryUpdate");
        }
        int generateUniqueID()
        {
            int num = 1; int ran;
            Start:
            ran = UnityEngine.Random.Range(100, 999);
            num = ran;

            foreach (Ticket t in storedData.AllTickets)
                if (t.ticketID == num)
                    goto Start;

            return num;
        } // Generates a unique id for the ticket
        #endregion

        class Ticket
        {
            public string senderName;
            public ulong senderID;
            public string message;
            public string myDate;
            public string myDateS;
            public string myTime;
            public float x, y, z;
            public int ticketID;

            public Ticket(string name, ulong id, string msg, Vector3 loc, string date, string time)
            {
                senderName = name;
                senderID = id;
                message = msg;
                myDate = DateTime.Now.ToShortDateString();
                myTime = time;
                x = loc.x;
                y = loc.y;
                z = loc.z;
                myDateS = DateTime.Now.ToShortDateString();
            }
        }

        class StoredData
        {
            public List<Ticket> AllTickets = new List<Ticket>();
        }

    }

    #region Permission Service

    public static class PermissionService
    {
        public static Permission permission = Interface.GetMod().GetLibrary<Permission>();

        public static bool HasPermission(ulong uid, string permissionName)
        {
            return !string.IsNullOrEmpty(permissionName) && permission.UserHasPermission(uid.ToString(), permissionName);
        }

        public static void RegisterPermissions(Plugin owner, List<string> permissions)
        {
            if (owner == null) throw new ArgumentNullException("owner");
            if (permissions == null) throw new ArgumentNullException("commands");

            foreach (var permissionName in permissions.Where(permissionName => !permission.PermissionExists(permissionName)))
            {
                permission.RegisterPermission(permissionName, owner);
            }
        }
    }

    #endregion
}
