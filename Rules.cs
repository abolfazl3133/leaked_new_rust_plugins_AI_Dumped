/*
*  < ----- End-User License Agreement ----->
*  
*  You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of this software without the developer’s consent.
*
*  THIS SOFTWARE IS PROVIDED BY IIIaKa AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, 
*  THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS 
*  BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
*  GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT 
*  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*
*  Developer: IIIaKa
*      https://t.me/iiiaka
*      Discord: @iiiaka
*      https://github.com/IIIaKa
*      https://umod.org/user/IIIaKa
*      https://codefling.com/iiiaka
*      https://lone.design/vendor/iiiaka/
*      https://www.patreon.com/iiiaka
*      https://boosty.to/iiiaka
*  Codefling plugin page: https://codefling.com/plugins/rules
*  Codefling license: https://codefling.com/plugins/rules?tab=downloads_field_4
*  
*  Lone.Design plugin page: https://lone.design/product/rules/
*  
*  Copyright © 2023-2024 IIIaKa
*/

using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries.Covalence;
using System.Globalization;
using UnityEngine.UI;
using Newtonsoft.Json.Converters;

namespace Oxide.Plugins
{
    [Info("Rules", "IIIaKa", "0.1.4")]
    [Description("Useful Rules Agreement Plugin. Allowing you to prevent players who do not agree with your rules from playing on the server.")]
    class Rules : RustPlugin
    {
        #region ~Variables~
        private const string PERMISSION_ADMIN = "rules.admin", PERMISSION_IGNORE = "rules.ignore";
        private const string UiName = "RulesUi", PanelName = $"{UiName}_Panel",
            PanelTitleName = $"{PanelName}_Title", PanelTitleTextName = $"{PanelTitleName}_Text",
            PanelPaginationNextParentName = $"{PanelTitleName}_Pagination_Next", PanelPaginationNextName = $"{PanelPaginationNextParentName}_Button",
            PanelPaginationPreviousParentName = $"{PanelTitleName}_Pagination_Previous", PanelPaginationPreviousName = $"{PanelPaginationPreviousParentName}_Button",
            PanelContentName = $"{PanelName}_Content", PanelContentScrollName = $"{PanelContentName}_Scroll", PanelContentProgressName = $"{PanelContentName}_Progress", ProgressFillName = $"{PanelContentProgressName}_Fill",
            PanelFooterName = $"{PanelName}_Footer", PanelButtonDeclineName = $"{PanelFooterName}_Decline", PanelButtonAcceptName = $"{PanelFooterName}_Accept", PanelButtonAcceptInactiveName = $"{PanelButtonAcceptName}_Inactive";
        public const string TransparentColor = "0 0 0 0", Color_1_1_1_1 = "1 1 1 1", Anchor_0_0 = "0 0", Anchor_1_1 = "1 1", Offset_0_0 = "0 0",
            Placeholders_TitleText = "*titleText*", Placeholders_TitleSubText = "*titleSubText*", Placeholders_DeclineBtnText = "*declineBtnText*", Placeholders_AcceptBtnText = "*acceptBtnText*";
        private bool _rulesEffect = false, _acceptEffect = false;
        private Hash<string, Timer> _awaitingPlayers = new Hash<string, Timer>();
        private DateTime _lastUpdate = DateTime.Now;
        #endregion

        #region ~Configuration~
        private static Configuration _config;
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "Chat command")]
            public string Command = "rules";
            
            [JsonProperty(PropertyName = "Is it worth enabling GameTips for messages?")]
            public bool GameTips_Enabled = true;
            
            [JsonProperty(PropertyName = "Is it worth enabling a requirement for agreement with the rules? If disabled, the rules will not be displayed")]
            public bool Rules_Enabled = true;
            
            [JsonProperty(PropertyName = "Date of the last rules update. Format: yyyy-MM-dd HH:mm")]
            public string LastUpdate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            
            [JsonProperty(PropertyName = "Is it worth asking for agreement with the rules every time? If disabled, consent will only be requested again if the rules have been updated")]
            public bool EveryTime = false;
            
            [JsonProperty(PropertyName = "Is it worth preventing a player from using chat until they agree to the rules?")]
            public bool RestrictChat = true;
            
            [JsonProperty(PropertyName = "Is it worth preventing a player from using commands until they agree to the rules?")]
            public bool RestrictCommands = true;
            
            [JsonProperty(PropertyName = "Is it worth preventing a player from using voice chat until they agree to the rules?")]
            public bool RestrictVoice = true;
            
            [JsonProperty(PropertyName = "Is it worth requesting agreement from all active players with the rules after the plugin is (re)loaded or enabled?")]
            public bool ReaskOnLoad = true;
            
            [JsonProperty(PropertyName = "Time in seconds(0-600) given to the player to respond, after which they will be kicked/banned from the server. A value of 0 disables the time limit.")]
            public float ResponseTime = 0f;
            
            [JsonProperty(PropertyName = "Number of rejections(in a row) of the rules after which the player will be banned. A value of 0 disables the ban")]
            public int RejectionsToBan = 5;
            
            [JsonProperty(PropertyName = "Number of rules pages. Will automatically add additional language keys if they are missing")]
            public int TotalPages = 4;
            
            [JsonProperty(PropertyName = "Additional languages for key generation(except en and ru)")]
            public List<string> AdditionalLanguages = new List<string>();
            
            [JsonProperty(PropertyName = "Prefab name for the effect when requesting agreement with the rules. Leave empty to disable")]
            public string ShowRules_Sound_Prefab = "assets/bundled/prefabs/fx/invite_notice.prefab";
            
            [JsonProperty(PropertyName = "Prefab name for the effect upon agreement with the rules. Leave empty to disable")]
            public string Accepted_Sound_Prefab = "assets/prefabs/misc/xmas/advent_calendar/effects/open_advent.prefab";
            
            [JsonProperty(PropertyName = "Width of the container for the rules text, needed for scroll view calculation")]
            public float TextContainer_Width = 600f;
            
            [JsonProperty(PropertyName = "Height of the container for the rules text, needed for scroll view calculation")]
            public float TextContainer_Height = 375f;
            
            public Oxide.Core.VersionNumber Version;
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try { _config = Config.ReadObject<Configuration>(); }
            catch (Exception ex) { PrintError($"{ex.Message}\n\n[{Title}] Your configuration file contains an error."); }
            if (_config == null || _config.Version == new VersionNumber())
            {
                PrintWarning("The configuration file is not found or contains errors. Creating a new one...");
                LoadDefaultConfig();
            }
            else if (_config.Version < Version)
            {
                PrintWarning($"Your configuration file version({_config.Version}) is outdated. Updating it to {Version}.");
                _config.Version = Version;
                PrintWarning($"The configuration file has been successfully updated to version {_config.Version}!");
            }
            
            if (!DateTime.TryParseExact(_config.LastUpdate, "yyyy-MM-dd HH:mm", null, DateTimeStyles.None, out _lastUpdate))
            {
                PrintWarning($"Failed to convert {_config.LastUpdate} to a date. Using date {_lastUpdate}.");
                _config.LastUpdate = _lastUpdate.ToString("yyyy-MM-dd HH:mm");
            }
            _config.ResponseTime = Mathf.Clamp(_config.ResponseTime, 0f, 600f);
            _config.TotalPages = Math.Max(_config.TotalPages, 1);
            if (_config.AdditionalLanguages == null)
                _config.AdditionalLanguages = new List<string>();
            if (_config.AdditionalLanguages.Any())
            {
                foreach (var newKey in _config.AdditionalLanguages.ToList())
                {
                    if (string.IsNullOrWhiteSpace(newKey) || newKey == "ru" || newKey.Length != 2 || !newKey.All(c => char.IsLetter(c)))
                        _config.AdditionalLanguages.Remove(newKey);
                    else
                        _langs.Add(newKey.ToLower());
                }
            }
            else
                _config.AdditionalLanguages.Add(string.Empty);
            
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);
        protected override void LoadDefaultConfig() => _config = new Configuration() { Version = Version };
        #endregion
        
        #region ~DataFile~
        private static StoredData _storedData;

        private class StoredData
        {
            [JsonProperty(PropertyName = "Player's Data")]
            public Dictionary<string, PlayerData> PlayersData = new Dictionary<string, PlayerData>();
        }
        
        public class PlayerData
        {
            public bool HasAgreed { get; set; }
            public int Declines { get; set; }
            public DateTime LastAgree { get; set; }
            public bool HasReadAll { get; set; }
        }
        
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);
        #endregion

        #region ~Language~
        private HashSet<string> _langs = new HashSet<string>() { "en" };

        private Dictionary<string, string> _enLang = new Dictionary<string, string>
        {
            ["RulesNotFound"] = "Argument {0} not found!",
            ["RulesAccepted"] = "Thank you for accepting our terms! Enjoy the game!",
            ["RulesAcceptNotRead"] = "You haven't read all the pages!",
            ["RulesEnabled"] = "The requirement to agree with the rules is enabled!",
            ["RulesDisabled"] = "The requirement to agree with the rules is disabled!",
            ["RulesAlreadyEnabled"] = "The requirement to agree with the rules is already enabled!",
            ["RulesAlreadyDisabled"] = "The requirement to agree with the rules is already disabled!",
            ["RulesOnce"] = "A one-time request for agreement with the rules is enabled!",
            ["RulesEveryTime"] = "A one-time request for agreement with the rules is disabled!",
            ["RulesReAsk"] = "All players have been sent a request for agreement with the rules, depending on the settings.",
            ["KickReason"] = "You have been kicked because you declined our rules!",
            ["BanReason"] = "You have been banned because you declined our rules {0} times in a row!",
            ["PanelTitle"] = "Terms Of Service",
            ["PanelLastUpdate"] = "Last updated on <color=brown>{0} {1}, {2}</color>",
            ["BtnAccept"] = "Accept",
            ["BtnDecline"] = "Decline",
            ["BtnNextPage"] = ">",
            ["BtnPreviousPage"] = "<",
            ["January"] = "January",
            ["February"] = "February",
            ["March"] = "March",
            ["April"] = "April",
            ["May"] = "May",
            ["June"] = "June",
            ["July"] = "July",
            ["August"] = "August",
            ["September"] = "September",
            ["October"] = "October",
            ["November"] = "November",
            ["December"] = "December",
            ["Rules_1"] = "<b>1. Information</b>\n\n<b>1.1</b> - Lack of knowledge of the rules does not exempt you from responsibility.\n<b>1.2</b> - By playing on the server, you automatically agree to all the rules listed below.\n<b>1.3</b> - If you have already been caught using cheats/macros or exploiting on another server/project, and there is proof against you, we reserve the right to ban you without further investigation.\n<b>1.4</b> - The administration determines the punishment for a player based on the severity of the violation and the circumstances. Violators may receive a warning or a permanent ban (there is no temporary bans). Bans apply to all servers within the project.\n<b>1.5</b> - The administration does not compensate for in-game items lost due to your errors, technical server/hosting issues, game bugs, or contact with rule violators.\n<b>1.6</b> - The administration does not interfere in player-to-player relationships. You are solely responsible for the people you choose to play with, so if a player deceives you, there will be no punishments for them from us.\n<b>1.7</b> - You are responsible for all your accounts. If one account is banned, the ban applies to all your accounts. The same applies if one of your accounts has a game ban (EAC).\n<b>1.8</b> - Impersonating a member of the server's administration is prohibited.",
            ["Rules_2"] = "<b>2. Gameplay</b>\n\n<b>2.1</b> - It is prohibited to use/store/purchase/distribute third-party software or any other means that provide an advantage over other players.\n<b>2.2</b> - Using cheat services is forbidden.\n<b>2.3</b> - Exploiting game bugs is not allowed.\n<b>2.4</b> - Exceeding the player limit in a team is prohibited:\n<b>+</b> Alliances or truces with other players are not allowed if the total number of players involved exceeds the server's limitations;\n<b>+</b> Frequent changes of allies will be considered a rule violation, as will playing with another player while your teammate is AFK or not nearby;\n<b>+</b> Changing teammates temporarily is not allowed if the replaced teammate intends to continue playing with you;\n<b>+</b> Changing a partner is allowed if your previous partner will not be in contact with you in the future.",
            ["Rules_3"] = "<b>3. In-Game Chat/Voice Chat</b>\n\n<b>3.1</b> - Discussion of politics, religion, immoral, and other inappropriate topics is prohibited.\n<b>3.2</b> - Inciting national, racial, or religious hatred or insulting other players and individuals is forbidden.\n<b>3.3</b> - Posting links to third-party services and websites in the chat is not allowed.\n<b>3.4</b> - Spamming (repeatedly posting meaningless phrases or characters) or sending identical messages in a short period of time is prohibited.\n<b>3.5</b> - Selling or pretending to sell cheats/macros is not allowed.\n<b>3.6</b> - Proposing actions that lead to an unwanted server exit is prohibited. Such actions include, for example, pressing <b>alt+f4</b>, typing <b>disconnect</b> in the console, and similar actions whose meanings other players may not be aware of.\n<b>3.7</b> - The administration reserves the right to mute or block a player in the chat if they behave inappropriately or disrespectfully towards other players.\n<b>3.8</b> - Selling/buying in-game items for real currency, crypto or skins is prohibited.",
            ["Rules_4"] = "<b>4. In-Game Check</b>\n\n<b>4.1</b> - In-Game checks are conducted exclusively through the <b>Discord</b> program. Every player on our project must have the ability to access it for the purpose of undergoing in-game checks.\n<b>4.2</b> - Calls for in-game checks are made only through in-game notifications and never through voice or text chat.\n<b>4.3</b> - If a player leaves the server, ignores a in-game check, or refuses to participate in it, they will immediately receive a ban.\n<b>4.4</b> - Clearing your PC before a in-game check is prohibited.\n<b>4.5</b> - Refusal to provide the necessary information for the in-game check or inappropriate behavior will result in a ban.\n<b>4.6</b> - If a player is banned as a result of a in-game check (including bans for refusal, ignoring, leaving the server, and providing incorrect contact information), their entire team will also be banned.\n\n<b>+</b> You have the full right to refuse to undergo a in-game check, but in this case, you and your allies will be banned.\n<b>+</b> Leaving the server, providing incorrect contact information, and ignoring the in-game check will also be considered a refusal.\n<b>+</b> If you agree to undergo the in-game check, you automatically allow the administration to install third-party programs necessary for checking your PC(e.g., AnyDesk, RCC, etc.).\n\n<b>Appeal Process</b>\nOne month after the ban, you have the ability to submit an appeal, but only if the ban was not for cheats or macros.\nAppeals can be submitted on our website."
        };
        
        private Dictionary<string, string> _ruLang = new Dictionary<string, string>
        {
            ["RulesNotFound"] = "Аргумент {0} не найден!",
            ["RulesAccepted"] = "Спасибо, что приняли наши условия! Приятной вам игры!",
            ["RulesAcceptNotRead"] = "Вы не прочли все страницы!",
            ["RulesEnabled"] = "Требование согласия с правилами включено!",
            ["RulesDisabled"] = "Требование согласия с правилами выключено!",
            ["RulesAlreadyEnabled"] = "Требование согласия с правилами уже включено!",
            ["RulesAlreadyDisabled"] = "Требование согласия с правилами уже выключено!",
            ["RulesOnce"] = "Однократный запрос на согласие с правилами включен!",
            ["RulesEveryTime"] = "Однократный запрос на согласие с правилами выключен!",
            ["RulesReAsk"] = "Всем игрокам в зависимости от настроек был отправлен запрос на соглашение с правилами.",
            ["KickReason"] = "Вы были исключены, так как вы отклонили наши правила!",
            ["BanReason"] = "Вы были заблокированы, так как вы в {0} раз подряд отклонили наши правила!",
            ["PanelTitle"] = "Условия использования",
            ["PanelLastUpdate"] = "Последнее обновление <color=brown>{1} {0} {2} года</color>",
            ["BtnAccept"] = "Принимаю",
            ["BtnDecline"] = "Отказываюсь",
            ["BtnNextPage"] = ">",
            ["BtnPreviousPage"] = "<",
            ["January"] = "Января",
            ["February"] = "Февраля",
            ["March"] = "Марта",
            ["April"] = "Апреля",
            ["May"] = "Мая",
            ["June"] = "Июня",
            ["July"] = "Июля",
            ["August"] = "Августа",
            ["September"] = "Сентября",
            ["October"] = "Октября",
            ["November"] = "Ноября",
            ["December"] = "Декабря",
            ["Rules_1"] = "<b>1. Информация</b>\n\n<b>1.1</b> - Не знание правил не освобождает Вас от ответственности.\n<b>1.2</b> - Играя на сервере Вы автоматически соглашаетесь со всеми нижеперечисленными пунктами правил.\n<b>1.3</b> - Если Вы уже были замечены с читами/макросами или использованием просвета на другом сервере/проекте и на вас есть пруфы - мы имеем право забанить Вас без проверки.\n<b>1.4</b> - Администрация сама выбирает наказание для игрока в зависимости от степени нарушения и обстоятельств. Нарушитель может получить как предупреждение, так и перманентный бан(временных блокировок нет). Блокировка выдаётся на всех серверах проекта.\n<b>1.5</b> - Администрация не компенсирует игровые ценности, утраченные по причине вашей ошибки, технических проблем на сервере/хостинге, багов игры или контакта с нарушителями.\n<b>1.6</b> - Администрация не вмешивается во взаимоотношения игроков, за тех с кем вы играете ответственны только Вы, поэтому в случае если игрок вас обманет — ему ничего за это не будет.\n<b>1.7</b> - Вы несете ответственность за все свои аккаунты. Получив бан на одном аккаунте - Вы получите его и на остальных аккаунтах. То же самое будет если на одном из ваших аккаунтах имеется игровая блокировка(EAC).\n<b>1.8</b> - Запрещено выдавать себя за члена Администрации сервера.",
            ["Rules_2"] = "<b>2. Геймплей</b>\n\n<b>2.1</b> - Запрещено использовать/хранить/приобретать/распространять стороннее ПО или любые другие средства, позволяющие получить преимущество над другими игроками.\n<b>2.2</b> - Запрещено использование услуг читеров.\n<b>2.3</b> - Запрещено использование багов.\n<b>2.4</b> - Запрещено превышать лимит игроков в команде:\n<b>+</b> Нельзя устраивать альянсы или перемирия с другими игроками если в сумме вас больше, чем указано в ограничениях сервера;\n<b>+</b> Частая смена союзников будет считаться за нарушение правил, тоже самое касается и игру с другим игроком пока тиммейт стоит афк или не находится рядом;\n<b>+</b> Запрещена смена союзников на время, если заменяемый союзник продолжит с вами играть;\n<b>+</b> Разрешено сменить напарника, если ваш предыдущий напарник в дальнейшем не будет с вами контактировать.",
            ["Rules_3"] = "<b>3. Игровой Чат/Голосовой чат</b>\n\n<b>3.1</b> - Запрещено обсуждение политики, религии, аморальных и прочих неуместных тем.\n<b>3.2</b> - Запрещено разжигание национальной, расовой или религиозной ненависти или оскорбления других игроков и других людей.\n<b>3.3</b> - Запрещены ссылки в чате на сторонние сервисы и сайты.\n<b>3.4</b> - Запрещен флуд(многократное повторение бессмысленных фраз, символов) или многократное отправление одинаковых фраз за короткий промежуток времени.\n<b>3.5</b> - Запрещено продавать или делать вид что вы продаёте читы/макросы.\n<b>3.6</b> - Запрещено предлагать сделать действия, приводящие к нежеланному выходу с сервера. К таким действиям относится например нажатие <b>alt+f4</b>, прописывание <b>disconnect</b> в консоль и прочие подобные действия, о значении которых другие игроки могут не знать.\n<b>3.7</b> - Администрация оставляет за собой право выдать мут или заблокировать игрока в чате если тот ведёт себя неадекватно или некорректно по отношению к другим игрокам.\n<b>3.8</b> - Запрещена продажа/покупка игровых ценностей за реальную валюту, крипту или скины.",
            ["Rules_4"] = "<b>4. Игровая проверка</b>\n\n<b>4.1</b> - Проверки проходят только через программу <b>Discord</b>. Каждый игрок на нашем проекте, в обязательном порядке должен иметь возможность зайти в нее для прохождения проверки.\n<b>4.2</b> - Вызов на проверку осуществляется только через игровое оповещение и ни в коем случае не через голосовой или текстовый чат.\n<b>4.3</b> - Если игрок покинул сервер, проигнорировал проверку или отказался от неё, то он сразу получает блокировку.\n<b>4.4</b> - Запрещено чистить ПК перед проверкой.\n<b>4.5</b> - За отказ показывать нужную для проверки информацию или неадекватное поведение — вы будете заблокированы.\n<b>4.6</b> - Если по итогу(итогом считается и блокировка за отказ / игнор / выход из сервера и предоставление некорректных данных для связи) проверки игрок блокируется, то и вся его команда блокируется вместе с ним.\n\n<b>+</b> Вы имеете полное право отказаться проходить проверку, но в этом случае Вы и ваши союзники будут заблокированы.\n<b>+</b> Так же отказом от проверки будет считаться выход с сервера, предоставление некорректных контактных данных и игнорирование проверки.\n<b>+</b> Если Вы согласны пройти проверку - то автоматически разрешаете устанавливать сторонние программы нужные администрации для проверки вашего PC(AnyDesk, RCC и т.д).\n\n<b>Возможность разблокировки</b>\nЧерез месяц после блокировки можно подать апелляцию, но, только в случае если бан был получен не за читы или макросы.\nАпелляцию можно подать на нашем сайте."
        };
        
        protected override void LoadDefaultMessages()
        {
            for (int i = 1; i <= _config.TotalPages; i++)
            {
                string key = $"Rules_{i}";
                if (!_enLang.ContainsKey(key))
                    _enLang[key] = key;
                if (!_ruLang.ContainsKey(key))
                    _ruLang[key] = key;
            }
            
            foreach (var key in _langs)
                lang.RegisterMessages(_enLang, this, key);
            lang.RegisterMessages(_ruLang, this, "ru");
        }
        #endregion
        
        #region ~Methods~
        private void AskRules()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.userID.IsSteamId() && !permission.UserHasPermission(player.UserIDString, PERMISSION_IGNORE) && !permission.UserHasPermission(player.UserIDString, PERMISSION_ADMIN))
                    ShowRules(player);
            }
        }
        
        private void ToggleRules(bool isUnload = false)
        {
            if (isUnload || !_config.Rules_Enabled)
            {
                Interface.CallHook("RulesDisabled");
                DestroyAllPanels();
                Unsubscribe(nameof(OnPlayerConnected));
                Unsubscribe(nameof(OnUserChat));
                Unsubscribe(nameof(OnUserCommand));
                Unsubscribe(nameof(OnPlayerVoice));
                Unsubscribe(nameof(OnUserUnbanned));
                Unsubscribe(nameof(OnUserPermissionRevoked));
                Unsubscribe(nameof(OnGroupPermissionRevoked));
            }
            else
            {
                _awaitingPlayers.Clear();
                Interface.CallHook("RulesEnabled");
                if (_config.ReaskOnLoad)
                    AskRules();
                Subscribe(nameof(OnPlayerConnected));
                Subscribe(nameof(OnUserUnbanned));
                Subscribe(nameof(OnUserPermissionRevoked));
                Subscribe(nameof(OnGroupPermissionRevoked));
            }
        }
        
        private static void SendEffect(Vector3 position, Network.Connection connection, string prefabName)
        {
            var effect = new Effect();
            effect.Init(Effect.Type.Generic, position, Vector3.zero);
            effect.pooledString = prefabName;
            EffectNetwork.Send(effect, connection);
        }
        
        private void DestroyPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UiName);
            if (_awaitingPlayers.TryGetValue(player.UserIDString, out var timer))
            {
                if (timer != null)
                    timer.Destroy();
                _awaitingPlayers.Remove(player.UserIDString);
            }
        }
        
        private void DestroyAllPanels()
        {
            foreach (var player in BasePlayer.activePlayerList)
                DestroyPanel(player);
        }
        #endregion

        #region ~Oxide Hooks~
        void OnPlayerConnected(BasePlayer player)
        {
            if (!_storedData.PlayersData.ContainsKey(player.UserIDString))
                _storedData.PlayersData[player.UserIDString] = new PlayerData();
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_IGNORE) && !permission.UserHasPermission(player.UserIDString, PERMISSION_ADMIN))
                ShowRules(player);
        }
        
        object OnUserChat(IPlayer player, string message) => _awaitingPlayers.ContainsKey(player.Id) ? false : null;
        
        object OnUserCommand(IPlayer player, string command, string[] args) => command != _config.Command && _awaitingPlayers.ContainsKey(player.Id) ? false : null;
        
        object OnPlayerVoice(BasePlayer player, Byte[] data) => _awaitingPlayers.ContainsKey(player.UserIDString) ? false : null;
        
        void OnUserUnbanned(string name, string id, string ipAddress)
        {
            if (_storedData.PlayersData.TryGetValue(id, out var playerData))
                playerData.Declines = 0;
        }
        
        void OnUserPermissionRevoked(string id, string permName)
        {
            if ((permName != PERMISSION_IGNORE && permName != PERMISSION_ADMIN) ||
                permission.UserHasPermission(id, PERMISSION_IGNORE) || permission.UserHasPermission(id, PERMISSION_ADMIN)) return;
            if (ulong.TryParse(id, out var userID) && BasePlayer.TryFindByID(userID, out var player))
                ShowRules(player);
        }
        
        void OnGroupPermissionRevoked(string groupName, string permName)
        {
            if (permName != PERMISSION_IGNORE && permName != PERMISSION_ADMIN) return;
            foreach (var userStr in permission.GetUsersInGroup(groupName))
            {
                var userIDString = userStr.Substring(0, userStr.IndexOf('(')).Trim();
                if (ulong.TryParse(userIDString, out var userID) && BasePlayer.TryFindByID(userID, out var player))
                {
                    if (!permission.UserHasPermission(player.UserIDString, PERMISSION_IGNORE) && !permission.UserHasPermission(player.UserIDString, PERMISSION_ADMIN))
                        ShowRules(player);
                }
            }
        }
        
        void Init()
        {
            Unsubscribe(nameof(OnPlayerConnected));
            Unsubscribe(nameof(OnUserChat));
            Unsubscribe(nameof(OnUserCommand));
            Unsubscribe(nameof(OnPlayerVoice));
            Unsubscribe(nameof(OnUserUnbanned));
            Unsubscribe(nameof(OnUserPermissionRevoked));
            Unsubscribe(nameof(OnGroupPermissionRevoked));
            Unsubscribe(nameof(OnServerSave));
            permission.RegisterPermission(PERMISSION_IGNORE, this);
            permission.RegisterPermission(PERMISSION_ADMIN, this);
            AddCovalenceCommand(_config.Command, nameof(Rules_Command));
            _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }
        
        void OnServerInitialized()
        {
            if (!string.IsNullOrWhiteSpace(_config.ShowRules_Sound_Prefab))
            {
                _rulesEffect = true;
                if (!StringPool.toNumber.ContainsKey(_config.ShowRules_Sound_Prefab))
                {
                    PrintError($"Effect {_config.ShowRules_Sound_Prefab} not found. Default is being used.");
                    _config.ShowRules_Sound_Prefab = "assets/bundled/prefabs/fx/invite_notice.prefab";
                    SaveConfig();
                }
            }
            if (!string.IsNullOrWhiteSpace(_config.Accepted_Sound_Prefab))
            {
                _acceptEffect = true;
                if (!StringPool.toNumber.ContainsKey(_config.Accepted_Sound_Prefab))
                {
                    PrintError($"Effect {_config.Accepted_Sound_Prefab} not found. Default is being used.");
                    _config.Accepted_Sound_Prefab = "assets/prefabs/misc/xmas/advent_calendar/effects/open_advent.prefab";
                    SaveConfig();
                }
            }
            InitUi();
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.userID.IsSteamId() && !_storedData.PlayersData.ContainsKey(player.UserIDString))
                    _storedData.PlayersData[player.UserIDString] = new PlayerData();
            }
            ToggleRules();
            Subscribe(nameof(OnServerSave));
        }

        void OnServerSave() => SaveData();
        #endregion

        #region ~Commands~
        private void Rules_Command(IPlayer player, string command, string[] args)
        {
            if (args == null || args.Length < 1) return;
            
            string replyKey = string.Empty;
            string[] replyArgs = new string[5];
            bool isWarning = false, commandHandled = false;
            if (player.IsServer || permission.UserHasPermission(player.Id, PERMISSION_ADMIN))
            {
                commandHandled = true;
                switch (args[0])
                {
                    case "enable":
                        if (_config.Rules_Enabled)
                        {
                            replyKey = "RulesAlreadyEnabled";
                            isWarning = true;
                        }
                        else
                        {
                            _config.Rules_Enabled = true;
                            ToggleRules();
                            replyKey = "RulesEnabled";
                            SaveConfig();
                        }
                        break;
                    case "disable":
                        if (!_config.Rules_Enabled)
                        {
                            replyKey = "RulesAlreadyDisabled";
                            isWarning = true;
                        }
                        else
                        {
                            _config.Rules_Enabled = false;
                            ToggleRules();
                            replyKey = "RulesDisabled";
                            SaveConfig();
                        }
                        break;
                    case "once":
                        _config.EveryTime = !_config.EveryTime;
                        SaveConfig();
                        replyKey = _config.EveryTime ? "RulesEveryTime" : "RulesOnce";
                        break;
                    case "reask":
                        if (_config.Rules_Enabled)
                        {
                            AskRules();
                            replyKey = "RulesReAsk";
                        }
                        else
                        {
                            replyKey = "RulesDisabled";
                            isWarning = true;
                        }
                        break;
                    default:
                        commandHandled = false;
                        replyKey = "RulesNotFound";
                        replyArgs[0] = args[0];
                        isWarning = true;
                        break;
                }
            }
            
            if (commandHandled)
                goto exit;
            
            if (permission.UserHasPermission(player.Id, PERMISSION_IGNORE))
                return;
            else if (player.Object is BasePlayer bPlayer)
            {
                replyKey = string.Empty;
                isWarning = false;
                
                if (args[0] == "show")
                    ShowRules(bPlayer);
                else if (_awaitingPlayers.ContainsKey(player.Id))
                {
                    var playerData = _storedData.PlayersData[player.Id];
                    switch (args[0])
                    {
                        case "accept":
                        case "decline":
                            if (args[0] == "accept" && !playerData.HasReadAll)
                            {
                                replyKey = "RulesAcceptNotRead";
                                isWarning = true;
                                break;
                            }

                            DestroyPanel(bPlayer);
                            if (!_awaitingPlayers.Any())
                            {
                                Unsubscribe(nameof(OnUserChat));
                                Unsubscribe(nameof(OnUserCommand));
                                Unsubscribe(nameof(OnPlayerVoice));
                            }

                            if (args[0] == "accept")
                            {
                                playerData.HasAgreed = true;
                                if (Interface.CallHook("RulesAccepted", player) != null) break;
                                playerData.Declines = 0;
                                playerData.LastAgree = DateTime.Now;
                                if (_acceptEffect)
                                    SendEffect(bPlayer.transform.position, bPlayer.Connection, _config.Accepted_Sound_Prefab);
                                replyKey = "RulesAccepted";
                            }
                            else if (args[0] == "decline")
                            {
                                playerData.HasAgreed = false;
                                playerData.Declines++;
                                if (Interface.CallHook("RulesDeclined", player, playerData.Declines, _config.RejectionsToBan) != null) break;
                                if (_config.RejectionsToBan > 0 && playerData.Declines >= _config.RejectionsToBan)
                                    player.Ban(string.Format(lang.GetMessage("BanReason", this, player.Id), playerData.Declines));
                                else
                                    player.Kick(lang.GetMessage("KickReason", this, player.Id));
                            }
                            break;
                        default:
                            if (int.TryParse(args[0], out int page))
                                DrawContent(bPlayer, playerData, page);
                            break;
                    }
                }
            }
        
        exit:
            if (!string.IsNullOrWhiteSpace(replyKey))
            {
                if (!player.IsServer && _config.GameTips_Enabled)
                    player.Command("gametip.showtoast", (int)(isWarning ? GameTip.Styles.Error : GameTip.Styles.Blue_Normal), string.Format(lang.GetMessage(replyKey, this, player.Id), replyArgs), string.Empty);
                else
                    player.Reply(string.Format(lang.GetMessage(replyKey, this, player.Id), replyArgs));
            }
        }
        #endregion

        #region ~Preparing UI~
        private void InitUi()
        {
            RulesText savedText;
            RulesButton savedBtn;
            CuiImageComponent savedImage;
            
            /* ~Main Panel~ */
            if (!TryGetSavedUi(Path_MainPanel, out List<CuiElement> savedElements) || !savedElements.Any())
            {
                savedElements = GetDefaultMainPanel();
                SaveDefaultUi(Path_MainPanel, savedElements);
            }
            _cached_MainPanel = CuiHelper.ToJson(savedElements);
            
            var container = new CuiElementContainer();
            /* ~Text Title~ */
            if (!TryGetSavedUi(Path_TextTitle, out savedText))
            {
                savedText = GetDefaultTextTitle();
                SaveDefaultUi(Path_TextTitle, savedText);
            }
            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = Placeholders_TitleText,
                    Font = savedText.Font,
                    FontSize = savedText.FontSize,
                    Color = savedText.Color,
                    Align = savedText.Align,
                    FadeIn = savedText.FadeIn,
                    VerticalOverflow = savedText.VerticalOverflow
                },
                RectTransform = { AnchorMin = Anchor_0_0, AnchorMax = Anchor_1_1, OffsetMin = Offset_0_0, OffsetMax = Offset_0_0 }
            }, PanelTitleTextName);

            /* ~Text TitleSub~ */
            if (!TryGetSavedUi(Path_TextTitleSub, out savedText))
            {
                savedText = GetDefaultTextTitleSub();
                SaveDefaultUi(Path_TextTitleSub, savedText);
            }
            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = Placeholders_TitleSubText,
                    Font = savedText.Font,
                    FontSize = savedText.FontSize,
                    Color = savedText.Color,
                    Align = savedText.Align,
                    FadeIn = savedText.FadeIn,
                    VerticalOverflow = savedText.VerticalOverflow
                },
                RectTransform = { AnchorMin = Anchor_0_0, AnchorMax = Anchor_1_1, OffsetMin = Offset_0_0, OffsetMax = Offset_0_0 }
            }, PanelTitleTextName);

            /* ~Button Decline~ */
            if (!TryGetSavedUi(Path_ButtonDecline, out savedBtn))
            {
                savedBtn = GetDefaultButtonDecline();
                SaveDefaultUi(Path_ButtonDecline, savedBtn);
            }
            container.Add(new CuiButton
            {
                Button =
                {
                    Command = $"{_config.Command} decline",
                    Sprite = savedBtn.Button.Sprite,
                    Material = savedBtn.Button.Material,
                    Color = savedBtn.Button.Color,
                    ImageType = savedBtn.Button.ImageType,
                    FadeIn = savedBtn.Button.FadeIn
                },
                Text =
                {
                    Text = Placeholders_DeclineBtnText,
                    Font = savedBtn.Text.Font,
                    FontSize = savedBtn.Text.FontSize,
                    Color = savedBtn.Text.Color,
                    Align = savedBtn.Text.Align,
                    FadeIn = savedBtn.Text.FadeIn,
                    VerticalOverflow = savedBtn.Text.VerticalOverflow
                },
                RectTransform = { AnchorMin = Anchor_0_0, AnchorMax = Anchor_1_1, OffsetMin = Offset_0_0, OffsetMax = Offset_0_0 }
            }, PanelButtonDeclineName);

            /* ~Button Accept~ */
            if (!TryGetSavedUi(Path_ButtonAccept, out savedBtn))
            {
                savedBtn = GetDefaultButtonAccept();
                SaveDefaultUi(Path_ButtonAccept, savedBtn);
            }
            container.Add(new CuiButton
            {
                Button =
                {
                    Command = $"{_config.Command} accept",
                    Sprite = savedBtn.Button.Sprite,
                    Material = savedBtn.Button.Material,
                    Color = savedBtn.Button.Color,
                    ImageType = savedBtn.Button.ImageType,
                    FadeIn = savedBtn.Button.FadeIn
                },
                Text =
                {
                    Text = Placeholders_AcceptBtnText,
                    Font = savedBtn.Text.Font,
                    FontSize = savedBtn.Text.FontSize,
                    Color = savedBtn.Text.Color,
                    Align = savedBtn.Text.Align,
                    FadeIn = savedBtn.Text.FadeIn,
                    VerticalOverflow = savedBtn.Text.VerticalOverflow
                },
                RectTransform = { AnchorMin = Anchor_0_0, AnchorMax = Anchor_1_1, OffsetMin = Offset_0_0, OffsetMax = Offset_0_0 }
            }, PanelButtonAcceptName);
            _cached_PanelSubElements = CuiHelper.ToJson(container);
            
            /* ~Accept Inactive~ */
            if (!TryGetSavedUi(Path_AcceptInactive, out savedImage))
            {
                savedImage = GetDefaultAcceptInactive();
                SaveDefaultUi(Path_AcceptInactive, savedImage);
            }
            _acceptInactive = savedImage;

            /* ~Button Next~ */
            if (!TryGetSavedUi(Path_ButtonNext, out savedBtn))
            {
                savedBtn = GetDefaultButtonNext();
                SaveDefaultUi(Path_ButtonNext, savedBtn);
            }
            _nextBtn = savedBtn;

            /* ~Button Previous~ */
            if (!TryGetSavedUi(Path_ButtonPrevious, out savedBtn))
            {
                savedBtn = GetDefaultButtonPrevious();
                SaveDefaultUi(Path_ButtonPrevious, savedBtn);
            }
            _prevBtn = savedBtn;
            
            /* ~Progress Fill~ */
            if (!TryGetSavedUi(Path_ProgressFill, out savedImage))
            {
                savedImage = GetDefaultProgressFill();
                SaveDefaultUi(Path_ProgressFill, savedImage);
            }
            _progressFill = savedImage;
            
            /* ~Text Rules~ */
            if (!TryGetSavedUi(Path_TextRules, out savedText))
            {
                savedText = GetDefaultTextRules();
                SaveDefaultUi(Path_TextRules, savedText);
            }
            _rulesText = savedText;
        }
        
        private List<CuiElement> GetDefaultMainPanel()
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = Anchor_0_0, AnchorMax = Anchor_1_1, OffsetMin = Offset_0_0, OffsetMax = Offset_0_0 },
                CursorEnabled = true,
                KeyboardEnabled = true,
                Image = { Color = "0 0 0 0.9", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, "Overlay", UiName);

            /* ~Panel~ */
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-320 -270", OffsetMax = "320 270" },
                Image = { Color = TransparentColor }
            }, UiName, PanelName);
            
            /* ~Title~ */
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = Anchor_1_1, OffsetMin = "0 -60", OffsetMax = Offset_0_0 },
                Image = { Color = "0.1 0.65 0.95 0.9" }
            }, PanelName, PanelTitleName);
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "11.5 -20", OffsetMax = "51.5 20" },
                Image = { Color = Color_1_1_1_1, Sprite = "assets/icons/warning.png" }
            }, PanelTitleName, $"{PanelTitleName}_Sprite");
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = Anchor_0_0, AnchorMax = Anchor_1_1, OffsetMin = "62 10", OffsetMax = "-20 -10" },
                Image = { Color = TransparentColor }
            }, PanelTitleName, PanelTitleTextName);

            /* ~Pagination~ */
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-70 -15", OffsetMax = "-20 15" },
                Image = { Color = TransparentColor }
            }, PanelTitleName, PanelPaginationNextParentName);
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-140 -15", OffsetMax = "-90 15" },
                Image = { Color = TransparentColor }
            }, PanelTitleName, PanelPaginationPreviousParentName);
            
            /* ~Content~ */
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = Anchor_1_1, OffsetMin = "0 -475", OffsetMax = "0 -60" },
                Image = { Color = "0.9 0.9 0.9 0.7" }
            }, PanelName, PanelContentName);
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = Anchor_0_0, AnchorMax = "1 0", OffsetMin = Offset_0_0, OffsetMax = "0 2" },
                Image = { Color = TransparentColor }
            }, PanelContentName, PanelContentProgressName);
            
            /* ~Footer~ */
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = Anchor_0_0, AnchorMax = Anchor_1_1, OffsetMin = Offset_0_0, OffsetMax = "0 -475" },
                Image = { Color = "0.47 0.8 1 0.8" }
            }, PanelName, PanelFooterName);
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "50 -15", OffsetMax = "200 15" },
                Image = { Color = TransparentColor }
            }, PanelFooterName, PanelButtonDeclineName);
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-200 -15", OffsetMax = "-50 15" },
                Image = { Color = TransparentColor }
            }, PanelFooterName, PanelButtonAcceptName);
            
            return container;
        }
        
        private RulesText GetDefaultTextTitle()
        {
            return new RulesText
            {
                Font = "RobotoCondensed-Bold.ttf",
                FontSize = 16,
                Color = Color_1_1_1_1,
                Align = TextAnchor.UpperLeft
            };
        }
        
        private RulesText GetDefaultTextTitleSub()
        {
            return new RulesText
            {
                Font = "RobotoCondensed-Regular.ttf",
                FontSize = 11,
                Color = Color_1_1_1_1,
                Align = TextAnchor.LowerLeft
            };
        }
        
        private RulesButton GetDefaultButtonDecline()
        {
            return new RulesButton
            {
                Button = { Color = "0.9 0.9 0.9 0.7" },
                Text =
                {
                    Font = "RobotoCondensed-Regular.ttf",
                    FontSize = 14,
                    Color = Color_1_1_1_1,
                    Align = TextAnchor.MiddleCenter
                }
            };
        }
        
        private RulesButton GetDefaultButtonAccept()
        {
            return new RulesButton
            {
                Button = { Color = "0.2 0.56 1 1" },
                Text =
                {
                    Font = "RobotoCondensed-Regular.ttf",
                    FontSize = 14,
                    Color = Color_1_1_1_1,
                    Align = TextAnchor.MiddleCenter
                }
            };
        }
        
        private CuiImageComponent GetDefaultAcceptInactive() => new CuiImageComponent { Color = "0.9 0.9 0.9 0.7" };
        
        private RulesButton GetDefaultButtonNext()
        {
            return new RulesButton
            {
                Button = { Color = "0.9 0.9 0.9 0.7" },
                Text =
                {
                    Font = "RobotoCondensed-Regular.ttf",
                    FontSize = 14,
                    Color = Color_1_1_1_1,
                    Align = TextAnchor.MiddleCenter
                }
            };
        }
        
        private RulesButton GetDefaultButtonPrevious()
        {
            return new RulesButton
            {
                Button = { Color = "0.9 0.9 0.9 0.7" },
                Text =
                {
                    Font = "RobotoCondensed-Regular.ttf",
                    FontSize = 14,
                    Color = Color_1_1_1_1,
                    Align = TextAnchor.MiddleCenter
                }
            };
        }
        
        private CuiImageComponent GetDefaultProgressFill() => new CuiImageComponent { Color = "0.2 0.56 1 1" };
        
        private RulesText GetDefaultTextRules()
        {
            return new RulesText
            {
                Font = "RobotoCondensed-Regular.ttf",
                FontSize = 14,
                Color = Color_1_1_1_1,
                Align = TextAnchor.UpperLeft,
                FadeIn = 1f
            };
        }
        
        public class RulesButton
        {
            public RulesButtonComponent Button { get; set; } = new RulesButtonComponent();
            public RulesText Text { get; set; } = new RulesText();
        }
        
        public class RulesButtonComponent
        {
            public string Sprite { get; set; }
            public string Material { get; set; }
            
            [JsonProperty(PropertyName = "Text Color(RGBA %)")]
            public string Color { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public Image.Type ImageType { get; set; }
            
            public float FadeIn { get; set; }
        }
        
        public class RulesText
        {
            [JsonProperty(PropertyName = "Text Size")]
            public int FontSize { get; set; }
            
            [JsonProperty(PropertyName = "Text Font(https://umod.org/guides/rust/basic-concepts-of-gui#fonts)")]
            public string Font { get; set; }
            
            [JsonConverter(typeof(StringEnumConverter))]
            public TextAnchor Align { get; set; }
            
            [JsonProperty(PropertyName = "Text Color(RGBA %)")]
            public string Color { get; set; }
            
            [JsonConverter(typeof(StringEnumConverter))]
            public VerticalWrapMode VerticalOverflow { get; set; }
            
            public float FadeIn { get; set; }
        }
        
        private static string _cached_MainPanel, _cached_PanelSubElements;
        private RulesButton _prevBtn, _nextBtn;
        private RulesText _rulesText;
        private CuiImageComponent _progressFill, _acceptInactive;
        private const string Path_MainPanel = $"{UiName}\\MainPanel",
            Path_TextTitle = $"{UiName}\\TextStyle_Title", Path_TextTitleSub = $"{UiName}\\TextStyle_TitleSub", Path_ButtonDecline = $"{UiName}\\ButtonStyle_Decline", Path_ButtonAccept = $"{UiName}\\ButtonStyle_Accept", Path_AcceptInactive = $"{UiName}\\AcceptInactive",
            Path_ButtonNext = $"{UiName}\\ButtonStyle_Next", Path_ButtonPrevious = $"{UiName}\\ButtonStyle_Previous", Path_ProgressFill = $"{UiName}\\ProgressFill", Path_TextRules = $"{UiName}\\TextStyle_Rules";
        
        private bool TryGetSavedUi<T>(string path, out T result) where T : class
        {
            result = null;
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(path))
            {
                try { result = Interface.Oxide.DataFileSystem.ReadObject<T>(path); }
                catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
            }
            return result != null;
        }
        
        private void SaveDefaultUi<T>(string path, T uiElement) => Interface.Oxide.DataFileSystem.WriteObject(path, uiElement);
        #endregion

        #region ~UI~
        private void ShowRules(BasePlayer player)
        {
            if (!_storedData.PlayersData.TryGetValue(player.UserIDString, out var playerData) || (!_config.EveryTime && playerData.LastAgree > _lastUpdate)) return;
            
            DestroyPanel(player);
            CuiHelper.AddUi(player, _cached_MainPanel);
            CuiHelper.AddUi(player, _cached_PanelSubElements
                .Replace(Placeholders_TitleText, lang.GetMessage("PanelTitle", this, player.UserIDString))
                .Replace(Placeholders_TitleSubText, string.Format(lang.GetMessage("PanelLastUpdate", this, player.UserIDString), lang.GetMessage(_lastUpdate.ToString("MMMM"), this, player.UserIDString), _lastUpdate.Day, _lastUpdate.Year))
                .Replace(Placeholders_DeclineBtnText, $"{lang.GetMessage("BtnDecline", this, player.UserIDString)}{(_config.RejectionsToBan > 0 ? string.Format($"({_config.RejectionsToBan - playerData.Declines})") : string.Empty)}")
                .Replace(Placeholders_AcceptBtnText, lang.GetMessage("BtnAccept", this, player.UserIDString)));
            
            _awaitingPlayers.Add(player.UserIDString, null);
            if (_awaitingPlayers.Count == 1)
            {
                if (_config.RestrictChat)
                    Subscribe(nameof(OnUserChat));
                if (_config.RestrictCommands)
                    Subscribe(nameof(OnUserCommand));
                if (_config.RestrictVoice)
                    Subscribe(nameof(OnPlayerVoice));
            }
            if (_config.ResponseTime > 0f)
                _awaitingPlayers[player.UserIDString] = timer.Once(_config.ResponseTime, () => { player.IPlayer.Command(_config.Command, "decline"); });
            
            DrawContent(player, playerData);
            if (_rulesEffect)
                SendEffect(player.transform.position, player.Connection, _config.ShowRules_Sound_Prefab);
        }

        private void DrawContent(BasePlayer player, PlayerData playerData, int page = 1)
        {
            int totalPages = _config.TotalPages;
            page = Math.Clamp(page, 1, totalPages);
            double progress = (double)page / totalPages;
            var container = new CuiElementContainer();

            /* ~Pagination~ */
            if (totalPages > 1)
            {
                if (page < totalPages)
                {
                    container.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"{_config.Command} {page + 1}",
                            Sprite = _nextBtn.Button.Sprite,
                            Material = _nextBtn.Button.Material,
                            Color = _nextBtn.Button.Color,
                            ImageType = _nextBtn.Button.ImageType,
                            FadeIn = _nextBtn.Button.FadeIn
                        },
                        Text =
                        {
                            Text = lang.GetMessage("BtnNextPage", this, player.UserIDString),
                            Font = _nextBtn.Text.Font,
                            FontSize = _nextBtn.Text.FontSize,
                            Color = _nextBtn.Text.Color,
                            Align = _nextBtn.Text.Align,
                            FadeIn = _nextBtn.Text.FadeIn,
                            VerticalOverflow = _nextBtn.Text.VerticalOverflow
                        },
                        RectTransform = { AnchorMin = Anchor_0_0, AnchorMax = Anchor_1_1, OffsetMin = Offset_0_0, OffsetMax = Offset_0_0 }
                    }, PanelPaginationNextParentName, PanelPaginationNextName);
                }
                if (page > 1)
                {
                    container.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"{_config.Command} {page - 1}",
                            Sprite = _prevBtn.Button.Sprite,
                            Material = _prevBtn.Button.Material,
                            Color = _prevBtn.Button.Color,
                            ImageType = _prevBtn.Button.ImageType,
                            FadeIn = _prevBtn.Button.FadeIn
                        },
                        Text =
                        {
                            Text = lang.GetMessage("BtnPreviousPage", this, player.UserIDString),
                            Font = _prevBtn.Text.Font,
                            FontSize = _prevBtn.Text.FontSize,
                            Color = _prevBtn.Text.Color,
                            Align = _prevBtn.Text.Align,
                            FadeIn = _prevBtn.Text.FadeIn,
                            VerticalOverflow = _prevBtn.Text.VerticalOverflow
                        },
                        RectTransform = { AnchorMin = Anchor_0_0, AnchorMax = Anchor_1_1, OffsetMin = Offset_0_0, OffsetMax = Offset_0_0 }
                    }, PanelPaginationPreviousParentName, PanelPaginationPreviousName);
                }
            }

            /* ~Content~ */
            string text = lang.GetMessage($"Rules_{page}", this, player.UserIDString);
            string[] lines = text.Split('\n');
            int rows = lines.Length, rulesTextSize = _rulesText.FontSize;
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = System.Text.RegularExpressions.Regex.Replace(lines[i], "<.*?>", string.Empty);
                float width = (lines[i].Length * rulesTextSize * 0.39f) - _config.TextContainer_Width;
                if (width > 0f)
                    rows += (int)Math.Ceiling(width / _config.TextContainer_Width);
            }
            container.Add(new CuiElement
            {
                Name = PanelContentScrollName,
                Parent = PanelContentName,
                Components =
                {
                    new CuiImageComponent { Color = TransparentColor },
                    new CuiRectTransformComponent { AnchorMin = Anchor_0_0, AnchorMax = Anchor_1_1, OffsetMin = "20 20", OffsetMax = "-20 -20" },
                    new CuiScrollViewComponent
                    {
                        ContentTransform = new CuiRectTransform { AnchorMin = Anchor_0_0, AnchorMax = Anchor_1_1, OffsetMin = $"0 -{(rulesTextSize * rows * 1.2) - _config.TextContainer_Height}", OffsetMax = Offset_0_0 },
                        Vertical = true,
                        MovementType = ScrollRect.MovementType.Elastic,
                        ScrollSensitivity = 20f,
                        HorizontalScrollbar = null,
                        VerticalScrollbar = null
                    }
                }
            });
            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = text,
                    Font = _rulesText.Font,
                    FontSize = rulesTextSize,
                    Color = _rulesText.Color,
                    Align = _rulesText.Align,
                    FadeIn = _rulesText.FadeIn
                },
                RectTransform = { AnchorMin = Anchor_0_0, AnchorMax = Anchor_1_1, OffsetMin = Offset_0_0, OffsetMax = Offset_0_0 }
            }, PanelContentScrollName);
            
            if (totalPages > 1)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = Anchor_0_0, AnchorMax = $"{progress} 1", OffsetMin = Offset_0_0, OffsetMax = Offset_0_0 },
                    Image = _progressFill
                }, PanelContentProgressName, ProgressFillName);
            }
            
            /* ~Accept Inactive Button~ */
            if (progress < 1d)
            {
                playerData.HasReadAll = false;
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = Anchor_0_0, AnchorMax = Anchor_1_1, OffsetMin = Offset_0_0, OffsetMax = Offset_0_0 },
                    Image = _acceptInactive
                }, PanelButtonAcceptName, PanelButtonAcceptInactiveName);
            }
            else
                playerData.HasReadAll = true;
            
            CuiHelper.DestroyUi(player, PanelPaginationNextName);
            CuiHelper.DestroyUi(player, PanelPaginationPreviousName);
            CuiHelper.DestroyUi(player, PanelContentScrollName);
            CuiHelper.DestroyUi(player, ProgressFillName);
            CuiHelper.DestroyUi(player, PanelButtonAcceptInactiveName);
            
            CuiHelper.AddUi(player, container);
        }
        #endregion
        
        #region ~Unload~
        void Unload()
        {
            Unsubscribe(nameof(OnServerSave));
            OnServerSave();
            ToggleRules(true);
            _storedData = null;
            _config = null;
        }
        #endregion
    }
}
