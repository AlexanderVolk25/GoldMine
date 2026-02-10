using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Text;
using ConVar;
using System.Collections.Generic;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Core.Database;
using Oxide.Core;
using System.Collections;
using UnityEngine;
using System.Linq;
using Object = System.Object;
using System;
using Oxide.Game.Rust.Cui;
using UnityEngine.Networking;
using Newtonsoft.Json;
namespace Oxide.Plugins
{
	[Info("TPPermissions", "pluginfuel.ru", "20.0.2")]
	[Description("Extended privilege granting system")]
	public class TPPermissions : RustPlugin
	{
		[ConsoleCommand("user.groups")]
		private void GetUserGroups_ConsoleCommand(ConsoleSystem.Arg arg)
		{
			if (arg == null) return;
			if (arg.Player() != null)
			if (!arg.Player().IsAdmin)
			return;
			if (arg.Args == null || arg.Args.Length < 1)
			{
				arg.ReplyWith(LanguageEn ? "Syntax error, use: user.groups Steam64ID" : "Ошибка синтаксиса, используйте : user.groups Steam64ID");
				return;
			}
			
			UInt64 userID;
			if (!UInt64.TryParse(arg.Args[0], out userID))
			{
				arg.ReplyWith(LanguageEn ? "You have specified an incorrect Steam64ID player!" : "Вы указали неккоректный Steam64ID игрока!");
				return;
			}
			
			Dictionary<String, DateTime> GetParametres = InformationPrivilagesUser.GetParametresUser(userID, TypeData.Group);
			String PermsList = GetParametres == null ? String.Empty : GetParametres.Aggregate(String.Empty, (current, keyValuePair) => current + $"- {keyValuePair.Key} | Expired : {keyValuePair.Value}\n");
			
			String Information = LanguageEn ? $"Active Player Privileges {userID} ({GetParametres.Count} pieces(/s))\n{PermsList}" : $"Активные привилегии игрока {userID} ({GetParametres.Count} штук(/и))\n{PermsList}";
			arg.ReplyWith(Information);
		}
		
		[PluginReference] Plugin TPMenuSystem;
		
		public class Fields
		{
			public String name { get; set; }
			public String value { get; set; }
			public bool inline { get; set; }
			public Fields(String name, String value, bool inline)
			{
				this.name = name;
				this.value = value;
				this.inline = inline;
			}
		}
		
		
		
		private void DrawUI_PoopUp(BasePlayer player, TypeAlert typeAlert, String Key, TypeData typeData, String DataExpired)
		{
			if (!IsFullLoaded)
			{
				PrintWarning(LanguageEn ? "The plugin is still being initialized, wait!" : "Плагин еще инициализируется, ожидайте!");
				return;
			}
			
			if (player == null) return;
			String Interface = InterfaceBuilder.GetInterface("UI_POOPUP_TEMPLATE");
			if (Interface == null) return;
			
			String ReplaceKey = GetLanguageNamePrivilage(player, Key, typeData);
			if (ReplaceKey == null) return;
			
			Configuration.InterfaceConfiguration Interfaces = config.InterfaceSetting;
			
			Interface = Interface.Replace("%PNGBackground%", _imageUI.GetImage("UI_BACKGROUND_PANEL"));
			Interface = Interface.Replace("%ICON%", GetIcon(typeAlert));
			Interface = Interface.Replace("%TITLE%", GetLang(GetTitle(typeAlert), player.UserIDString));
			Interface = Interface.Replace("%DESCRIPTION%", GetDescription(player.UserIDString, typeAlert, ReplaceKey, DataExpired));
			Interface = Interface.Replace("%COLOR_TITLE%", Interfaces.RGBAColorTitle);
			Interface = Interface.Replace("%COLOR_DESCRIPTION%", Interfaces.RGBAColorDescription);
			
			CuiHelper.DestroyUi(player, InterfaceBuilder.UI_POOPUP_PANEL);
			CuiHelper.AddUi(player, Interface);
			
			player.Invoke(() => CuiHelper.DestroyUi(player, InterfaceBuilder.UI_POOPUP_PANEL), config.AlertConfigurations.TimeAlert);
		}
		
		private String GetImage(String fileName, UInt64 skin = 0)
		{
			String imageId = (String)plugins.Find("ImageLibrary").CallHook("ImageUi.GetImage", fileName, skin);
			return !String.IsNullOrEmpty(imageId) ? imageId : String.Empty;
		}
		private void OnServerInitialized()
		{
			Subscribe("OnUserGroupRemoved");
			Subscribe("OnUserPermissionRevoked");
			
			GrablingPermissions();
			GrablingGroups();
			
			PreLoadedPlugin();
			
			foreach (var check in config.GroupList)
			plugins.Find("ImageLibrary").Call("AddImage", check.Value.Image, check.Value.Image);
			foreach (var check in config.PermissionList)
			plugins.Find("ImageLibrary").Call("AddImage", check.Value.Image, check.Value.Image);
			
			Interface.Call("OnIQPermissionInitialized");
		}
		
		
		
		
		public class GrantData
		{
			public Dictionary<String, Dictionary<String, Int64>> Group = new Dictionary<String, Dictionary<String, Int64>>();
			public Dictionary<String, Dictionary<String, Int64>> Permission = new Dictionary<String, Dictionary<String, Int64>>();
		}
		
		
		
		private void NewPermissionRegistered(String Permission)
		{
			if (config.PermissionList.ContainsKey(Permission)) return;
			String ExampleName = Permission.Substring(Permission.IndexOf('.') + 1).ToUpper();
			config.PermissionList.Add(Permission, new Configuration.StructureLanguage(ExampleName, ExampleName));
			
			SaveConfig();
		}
		
		private void WriteData()
		{
			Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/TPPermissions/OldPlayers", OldPlayers);
			if (sqlConnection != null) return;
			Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/TPPermissions/Permissions", InformationPrivilagesUser);
		}
		
		private void ReadData()
		{
			OldPlayers = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<List<UInt64>>("IQSystem/TPPermissions/OldPlayers");
			if (sqlConnection != null) return;
			InformationPrivilagesUser = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<StructureData>("IQSystem/TPPermissions/Permissions");
		}
		private void GrablingPermissions()
		{
			String[] PermissionList = permission.GetPermissions();
			
			Int32 CountPermissionGrabs = 0;
			foreach (String perm in PermissionList.Where(p => !config.PermissionList.ContainsKey(p)))
			{
				String ExampleName = perm.Substring(perm.IndexOf('.') + 1).ToUpper();
				
				config.PermissionList.Add(perm, new Configuration.StructureLanguage(ExampleName, ExampleName));
				CountPermissionGrabs++;
			}
			
			if (CountPermissionGrabs == 0) return;
		}
		
		private void NewGroupRegistered(String Group)
		{
			if (config.GroupList.ContainsKey(Group)) return;
			String ExampleName = Group.ToUpper();
			config.GroupList.Add(Group, new Configuration.StructureLanguage(ExampleName, ExampleName));
			
			SaveConfig();
		}
		
		public class Footer
		{
			public String text { get; set; }
			public String icon_url { get; set; }
			public String proxy_icon_url { get; set; }
			public Footer(String text, String icon_url, String proxy_icon_url)
			{
				this.text = text;
				this.icon_url = icon_url;
				this.proxy_icon_url = proxy_icon_url;
			}
		}
		
		private String GetTitle(TypeAlert typeAlert)
		{
			String Lang = typeAlert == TypeAlert.Add ? "TITLE_ALERT_ADD" :
			typeAlert == TypeAlert.Expired ? "TITLE_ALERT_EXPIRED" :
			typeAlert == TypeAlert.Alert ? "TITLE_ALERT_INFO" : String.Empty;
			return Lang;
		}
		
		private String GetLanguageNamePrivilage(BasePlayer player, String Key, TypeData type)
		{
			String ReplaceKey = String.Empty;
			
			switch (type)
			{
				case TypeData.Permission:
				{
					if (config.PermissionList.ContainsKey(Key))
					{
						ReplaceKey = lang.GetLanguage(player.UserIDString) == "ru"
						? config.PermissionList[Key].RussianName
						: config.PermissionList[Key].EnglishName;
						
						return ReplaceKey;
					}
					
					break;
				}
				case TypeData.Group:
				if (config.GroupList.ContainsKey(Key))
				{
					ReplaceKey = lang.GetLanguage(player.UserIDString) == "ru"
					? config.GroupList[Key].RussianName
					: config.GroupList[Key].EnglishName;
					
					return ReplaceKey;
				}
				break;
			}
			
			return null;
		}
		
		private String GetImagePrivilage(BasePlayer player, String Key, TypeData type)
		{
			String ReplaceKey = String.Empty;
			
			switch (type)
			{
				case TypeData.Permission:
				{
					if (config.PermissionList.ContainsKey(Key))
					{
						return config.PermissionList[Key].Image;
					}
					
					break;
				}
				case TypeData.Group:
				if (config.GroupList.ContainsKey(Key))
				{
					return config.GroupList[Key].Image;
				}
				break;
			}
			
			return null;
		}
		
		
		
		[JsonProperty(LanguageEn ? "Information about privileges" : "Информация о привилегиях")]
		private StructureData InformationPrivilagesUser = new StructureData();
		
		private Boolean IsFullLoaded = false;
		
		
		
		
		
		private Dictionary<String, DateTime> GetPermissions(UInt64 userID) => InformationPrivilagesUser.GetParametresUser(userID, TypeData.Permission);
		
		private void RevokePermission(UInt64 userID, String Permission, DateTime DataExpired = default(DateTime)) =>
		InformationPrivilagesUser.RemoveParametres(Permission, userID, TypeData.Permission, DataExpired);
		
		
		
		private static Int64 ToEpochTime(DateTime dateTime)
		{
			DateTime date = dateTime.ToLocalTime();
			Int64 ticks = date.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, 0).Ticks;
			Int64 ts = ticks / TimeSpan.TicksPerSecond;
			return ts;
		}
		
		private void RevokeGroup(UInt64 userID, String Group, String DataExpired = null)
		{
			if (DataExpired != null)
			{
				TimeSpan duration;
				if (!TryParseTimeSpan(DataExpired, out duration))
				{
					PrintError(LanguageEn ? "API : You specified an incorrect time, use 1d/1h/1m/1s!" : "API : Вы указали неккоректное время, используйте 1d/1h/1m/1s!");
					return;
				}
				
				DateTime ThisTime = InformationPrivilagesUser.GetExpiredPermissionData(Group, userID, TypeData.Group) - duration;
				InformationPrivilagesUser.RemoveParametres(Group, userID, TypeData.Group, ThisTime);
				return;
			}
			
			InformationPrivilagesUser.RemoveParametres(Group, userID, TypeData.Group);
		}
		
		[ConsoleCommand("migration.timeprivilage")]
		private void TimePrivilageMigration(ConsoleSystem.Arg arg)
		{
			if (arg.Player() != null) return;
			coroutineMigration = ServerMgr.Instance.StartCoroutine(MigrationTimePrivilage());
		}
		private void Unload()
		{
			_.InformationPrivilagesUser.RemoveParametresUnload();
			WriteData();
			
			if (coroutineMigration != null)
			{
				ServerMgr.Instance.StopCoroutine(coroutineMigration);
				coroutineMigration = null;
			}
			
			if (RoutineQueue != null)
			{
				foreach (KeyValuePair<UInt64, Coroutine> keyValuePair in RoutineQueue.Where(r => r.Value != null))
				ServerMgr.Instance.StopCoroutine(keyValuePair.Value);
				
				RoutineQueue.Clear();
			}
			
			InterfaceBuilder.DestroyAll();
			
			if (_imageUI != null)
			{
				_imageUI.UnloadImages();
				_imageUI = null;
			}
			
			sqlLibrary.CloseDb(sqlConnection);
			
			_.IsFullLoaded = false;
			
			_ = null;
		}
		private class ExpiringAccessValue
		{
			[JsonProperty]
			public string Value { get; private set; }
			
			[JsonProperty]
			public DateTime ExpireDate { get; set; }
			
			[JsonIgnore]
			public bool IsExpired => DateTime.Compare(DateTime.UtcNow, ExpireDate) > 0;
			
		}
		
		
		
		private void AlertPlayer(UInt64 userID, TypeAlert typeAlert, String Key, TypeData typeData, String DataExpired)
		{
			BasePlayer player = BasePlayer.FindByID(userID);
			if (player == null) return;
			
			if (UAlertSystem)
			{
				String ReplaceKey = GetLanguageNamePrivilage(player, Key, typeData);
				if (ReplaceKey == null) return;
				UAlertSystem.CallHook("API_SEND_ALERT", GetLang(GetTitle(typeAlert), userID.ToString()), GetDescription(userID.ToString(), typeAlert, ReplaceKey, DataExpired), "0.84 0.26 0.26 1", "1 1 1 1", 5f);
				return;
			}
			if (config.AlertConfigurations.UsePoopUp)
			DrawUI_PoopUp(player, typeAlert, Key, typeData, DataExpired);
			else
			{
				String ReplaceKey = GetLanguageNamePrivilage(player, Key, typeData);
				if (ReplaceKey == null) return;
				String Message = $"{GetLang(GetTitle(typeAlert), userID.ToString())}\n{GetDescription(userID.ToString(), typeAlert, ReplaceKey, DataExpired)}";
				SendChat(Message, player);
			}
		}
		
		
		
		
		public class FancyMessage
		{
			public String content { get; set; }
			public Boolean tts { get; set; }
			public Embeds[] embeds { get; set; }
			
			public class Embeds
			{
				public String title { get; set; }
				public Int32 color { get; set; }
				public List<Fields> fields { get; set; }
				public Footer footer { get; set; }
				public Authors author { get; set; }
				
				public Embeds(String title, Int32 color, List<Fields> fields, Authors author, Footer footer)
				{
					this.title = title;
					this.color = color;
					this.fields = fields;
					this.author = author;
					this.footer = footer;
					
				}
			}
			
			public FancyMessage(String content, bool tts, Embeds[] embeds)
			{
				this.content = content;
				this.tts = tts;
				this.embeds = embeds;
			}
			
			public String toJSON() => JsonConvert.SerializeObject(this);
		}
		
		[ConsoleCommand("revoke.group")]
		private void RevomeGroup_TwoConsoleCommand(ConsoleSystem.Arg arg) => RemoveGroup_ConsoleCommand(arg);
		
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
				PrintWarning(LanguageEn ? $"Error #554548 reading the configuration 'oxide/config/{Name}', creating a new configuration!!" : $"Ошибка #554548 чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
				LoadDefaultConfig();
			}
			
			if (config.GeneralSettings.MySQLSetting.TableName == null ||
			String.IsNullOrWhiteSpace(config.GeneralSettings.MySQLSetting.TableName))
			config.GeneralSettings.MySQLSetting.TableName = "players";
			
			NextTick(SaveConfig);
		}
		
		
		
		
		private static StringBuilder sb = new StringBuilder();
		
		private void RevokeGroup(UInt64 userID, String Group, DateTime DataExpired = default(DateTime)) =>
		InformationPrivilagesUser.RemoveParametres(Group, userID, TypeData.Group, DataExpired);
		
		
		
		private void MigrationPrivilage(BasePlayer player)
		{
			if (player == null || sqlConnection == null) return;
			
			Configuration.GeneralSetting.MySQLInitialize.MigrationPrivilage MigrationInfo = config.GeneralSettings.MySQLSetting.MigrationSettings;
			
			if (MigrationInfo.UseMigrationPermissionsList)
			foreach (KeyValuePair<String, Dictionary<UInt64, DateTime>> Permissions in InformationPrivilagesUser.UserPermissions.Where(x => MigrationInfo.MigrationPermissionList.Contains(x.Key)))
			foreach (KeyValuePair<UInt64, DateTime> PlayerInformation in Permissions.Value.Where(x => x.Key == player.userID))
			if (!permission.UserHasPermission(PlayerInformation.Key.ToString(), Permissions.Key))
			permission.GrantUserPermission(PlayerInformation.Key.ToString(), Permissions.Key, null);
			
			if (MigrationInfo.UseMigrationGroupList)
			foreach (KeyValuePair<String, Dictionary<UInt64, DateTime>> Groups in InformationPrivilagesUser.UserGrpups.Where(x => MigrationInfo.MigrationGroupList.Contains(x.Key)))
			foreach (KeyValuePair<UInt64, DateTime> PlayerInformation in Groups.Value.Where(x => x.Key == player.userID))
			if (!permission.UserHasGroup(PlayerInformation.Key.ToString(), Groups.Key))
			permission.AddUserGroup(PlayerInformation.Key.ToString(), Groups.Key);
		}
		
		[ConsoleCommand("revoke.permission")]
		private void RevokePerm_TwoConsoleCommand(ConsoleSystem.Arg arg) => RevokePerm_ConsoleCommand(arg);
		
		[ConsoleCommand("revokeperm")]
		private void RevokePerm_ConsoleCommand(ConsoleSystem.Arg arg)
		{
			if (arg == null) return;
			
			if (arg.Player() != null)
			if (!arg.Player().IsAdmin)
			return;
			
			if (arg.Args == null || arg.Args.Length < 2)
			{
				arg.ReplyWith(LanguageEn ? "Syntax error, use: revokeperm Steam64ID Permission\nExample : revokeperm 765611963065887930 iqchat.vip" : "Ошибка синтаксиса, используйте : revokeperm Steam64ID Permission\nПример : revokeperm 76561197235887930 iqchat.vip");
				return;
			}
			
			UInt64 userID;
			if (!UInt64.TryParse(arg.Args[0], out userID))
			{
				arg.ReplyWith(LanguageEn ? "You have specified an incorrect Steam64ID player!" : "Вы указали неккоректный Steam64ID игрока!");
				return;
			}
			
			String Permission = arg.Args[1];
			if (!config.PermissionList.ContainsKey(Permission))
			{
				arg.ReplyWith(LanguageEn ? "The permission you specified do not exist!" : "Права которые вы указали - не существуют!");
				return;
			}
			
			if (!permission.UserHasPermission(userID.ToString(), Permission))
			{
				arg.ReplyWith(LanguageEn ? "The player does not have these rights!!" : "У игрока отсутствуют данные права!");
				return;
			}
			
			if (arg.Args.Length == 3)
			{
				TimeSpan duration;
				if (!TryParseTimeSpan(arg.Args[2], out duration))
				{
					arg.ReplyWith(LanguageEn ? "You specified an incorrect time, use 1d/1h/1m/1s!" : "Вы указали неккоректное время, используйте 1d/1h/1m/1s!");
					return;
				}
				
				DateTime ThisTime = InformationPrivilagesUser.GetExpiredPermissionData(Permission, userID, TypeData.Permission) - duration;
				InformationPrivilagesUser.RemoveParametres(Permission, userID, TypeData.Permission, ThisTime);
				return;
			}
			
			InformationPrivilagesUser.RemoveParametres(Permission, userID, TypeData.Permission);
		}
		public String FormatTime(TimeSpan time, String UserID)
		{
			String Result = String.Empty;
			String Days = GetLang("TITLE_FORMAT_DAYS", UserID);
			String Hourse = GetLang("TITLE_FORMAT_HOURSE", UserID);
			String Minutes = GetLang("TITLE_FORMAT_MINUTES", UserID);
			String Seconds = GetLang("TITLE_FORMAT_SECONDS", UserID);
			
			if (time.Days != 0)
			Result += $"{Format(time.Days, Days, Days, Days)} ";
			
			if (time.Hours != 0)
			Result += $"{Format(time.Hours, Hourse, Hourse, Hourse)} ";
			
			if (time.Minutes != 0)
			Result += $"{Format(time.Minutes, Minutes, Minutes, Minutes)} ";
			
			if (time.Days == 0 && time.Hours == 0 && time.Minutes == 0 && time.Seconds != 0)
			Result = $"{Format(time.Seconds, Seconds, Seconds, Seconds)} ";
			
			return Result;
		}
		private new void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<String, String>
				{
					["TITLE_ALERT_ADD"] = "Privilege received!",
					["TITLE_ALERT_INFO"] = "Privilege expires!",
					["TITLE_ALERT_EXPIRED"] = "Privilege expired!",
					
					["DESCRIPTION_ALERT_EXPIRED"] = "Privilege {0} expired",
					["DESCRIPTION_ALERT_ADD"] = "You have successfully received {0}",
					["DESCRIPTION_ALERT_INFO"] = "{0} ends : {1}",
					["PINFO_ALERT"] = "YourPrivilages :\n{0}",
					["PINFO_ALERT_NOT"] = "You don't have privileges",
					
					["TITLE_FORMAT_DAYS"] = "D",
					["TITLE_FORMAT_HOURSE"] = "H",
					["TITLE_FORMAT_MINUTES"] = "M",
					["TITLE_FORMAT_SECONDS"] = "S",
					
					
				}, this);
				lang.RegisterMessages(new Dictionary<String, String>
					{
						["TITLE_ALERT_ADD"] = "Привилегия получена!",
						["TITLE_ALERT_INFO"] = "Привилегия истекает!",
						["TITLE_ALERT_EXPIRED"] = "Привилегия истекла!",
						
						["DESCRIPTION_ALERT_EXPIRED"] = "Привилегия {0} истекла",
						["DESCRIPTION_ALERT_ADD"] = "Вы успешно получили {0}",
						["DESCRIPTION_ALERT_INFO"] = "{0} кончается : {1}",
						["PINFO_ALERT"] = "Ваши привилегии :\n{0}",
						["PINFO_ALERT_NOT"] = "У вас нет привилегий",
						
						["TITLE_FORMAT_DAYS"] = "Д",
						["TITLE_FORMAT_HOURSE"] = "Ч",
						["TITLE_FORMAT_MINUTES"] = "М",
						["TITLE_FORMAT_SECONDS"] = "С",
						
					}, this, "ru");
		}
		
		public class Authors
		{
			public String name { get; set; }
			public String url { get; set; }
			public String icon_url { get; set; }
			public String proxy_icon_url { get; set; }
			public Authors(String name, String url, String icon_url, String proxy_icon_url)
			{
				this.name = name;
				this.url = url;
				this.icon_url = icon_url;
				this.proxy_icon_url = proxy_icon_url;
			}
		}
		private const Boolean LanguageEn = false;
		
		private void SetPermission(UInt64 userID, String Permission, DateTime DataExpired) =>
		InformationPrivilagesUser.SetParametres(Permission, userID, DataExpired, TypeData.Permission);
		
		private void SQL_OpenConnection()
		{
			Configuration.GeneralSetting.MySQLInitialize.MySQLConnect SQLInfo = config.GeneralSettings.MySQLSetting.ConnectSetting;
			if (String.IsNullOrWhiteSpace(SQLInfo.Host) || String.IsNullOrWhiteSpace(SQLInfo.Passowrd) ||
				String.IsNullOrWhiteSpace(SQLInfo.Port) || String.IsNullOrWhiteSpace(SQLInfo.DatabaseName) ||
			String.IsNullOrWhiteSpace(SQLInfo.UserName))
			{
				_.InformationPrivilagesUser.SetParametresUnload();
				return;
			}
			
			sqlConnection = sqlLibrary.OpenDb(SQLInfo.Host, Convert.ToInt32(SQLInfo.Port), SQLInfo.DatabaseName, SQLInfo.UserName, SQLInfo.Passowrd, this);
			
			if (sqlConnection == null)
			{
				_.InformationPrivilagesUser.SetParametresUnload();
				return;
			}
			
			Sql sql = Sql.Builder.Append(SQL_Query_CreatedDatabase());
			sqlLibrary.Insert(sql, sqlConnection);
			sql = Sql.Builder.Append(SQL_Query_SelectedDatabase());
			sqlLibrary.Query(sql, sqlConnection, list =>
				{
					if (list.Count > 0)
					foreach (Dictionary<String, Object> entry in list)
					{
						UInt64 SteamID = 0;
						if (!UInt64.TryParse((String)entry["steamid"], out SteamID))
						{
							PrintError(LanguageEn ? "Error parsing SteamID player" : "Ошибка парсинга SteamID игрока");
							return;
						}
						if (!String.IsNullOrWhiteSpace((String)entry["group"]))
						{
							String Group = (String)entry["group"];
							
							if (!_.InformationPrivilagesUser.UserGrpups.ContainsKey(Group))
							_.InformationPrivilagesUser.UserGrpups.Add(Group, new Dictionary<UInt64, DateTime>());
							
							if (!_.InformationPrivilagesUser.UserGrpups[Group].ContainsKey(SteamID))
							_.InformationPrivilagesUser.UserGrpups[Group].Add(SteamID, DateTime.Parse((String)entry["data_expired"]));
							else
							_.InformationPrivilagesUser.UserGrpups[Group][SteamID] = DateTime.Parse((String)entry["data_expired"]);
						}
						else if (!String.IsNullOrWhiteSpace((String)entry["permission"]))
						{
							String Permission = (String)entry["permission"];
							if (!_.InformationPrivilagesUser.UserPermissions.ContainsKey(Permission))
							_.InformationPrivilagesUser.UserPermissions.Add(Permission, new Dictionary<UInt64, DateTime>());
							
							if (!_.InformationPrivilagesUser.UserPermissions[Permission].ContainsKey(SteamID))
							{
								_.InformationPrivilagesUser.UserPermissions[Permission]
								.Add(SteamID, DateTime.Parse((String)entry["data_expired"]));
							}
							else
							{
								_.InformationPrivilagesUser.UserPermissions[Permission][SteamID] =
								DateTime.Parse((String)entry["data_expired"]);
							}
						}
					}
					
					_.InformationPrivilagesUser.SetParametresUnload();
					PrintWarning(LanguageEn ? "Updated information about users from the database" : "Обновлена информация о пользователях из БД");
				});
		}
		
		private void OnPlayerConnected(BasePlayer player)
		{
			MigrationPrivilage(player);
			SetupAutoPrivilages(player);
			
			if (!config.AlertConfigurations.UseAlertedGroup && !config.AlertConfigurations.UseAlertedPermission) return;
			
			player.Invoke(() =>
				{
					if (!RoutineQueue.ContainsKey(player.userID))
					RoutineQueue.Add(player.userID, ServerMgr.Instance.StartCoroutine(JoinedAlertPlayer(player)));
					else RoutineQueue[player.userID] = ServerMgr.Instance.StartCoroutine(JoinedAlertPlayer(player));
				}, 10f);
		}
		
		
		class StructureTimePrivilage
		{
			public Dictionary<String, String> groups = new Dictionary<String, String>();
			public Dictionary<String, String> permissions = new Dictionary<String, String>();
		}
		
		[ConsoleCommand("grant.group")]
		private void AddGroup_TwoConsoleCommand(ConsoleSystem.Arg arg) => AddGroup_ConsoleCommand(arg);
		
		[ConsoleCommand("user.perms")]
		private void GetUserPerms_ConsoleCommand(ConsoleSystem.Arg arg)
		{
			if (arg == null) return;
			
			if (arg.Player() != null)
			if (!arg.Player().IsAdmin)
			return;
			
			if (arg.Args == null || arg.Args.Length < 1)
			{
				arg.ReplyWith(LanguageEn ? "Syntax error, use: user.perms Steam64ID" : "Ошибка синтаксиса, используйте : user.perms Steam64ID");
				return;
			}
			
			UInt64 userID;
			if (!UInt64.TryParse(arg.Args[0], out userID))
			{
				arg.ReplyWith(LanguageEn ? "You have specified an incorrect Steam64ID player!" : "Вы указали неккоректный Steam64ID игрока!");
				return;
			}
			
			Dictionary<String, DateTime> GetParametres = InformationPrivilagesUser.GetParametresUser(userID, TypeData.Permission);
			String PermsList = GetParametres == null ? String.Empty : GetParametres.Aggregate(String.Empty, (current, keyValuePair) => current + $"- {keyValuePair.Key} | Expired : {keyValuePair.Value}\n");
			
			String Information = LanguageEn ? $"Active Player Privileges {userID} ({GetParametres.Count} pieces(/s))\n{PermsList}" : $"Активные привилегии игрока {userID} ({GetParametres.Count} штук(/и))\n{PermsList}";
			arg.ReplyWith(Information);
		}
		
		private String Format(Int32 units, String form1, String form2, String form3)
		{
			var tmp = units % 10;
			
			if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
			return $"{units}{form1}";
			
			if (tmp >= 2 && tmp <= 4)
			return $"{units}{form2}";
			
			return $"{units}{form3}";
		}
		
		private static String GetIcon(TypeAlert typeAlert)
		{
			Configuration.InterfaceConfiguration Interfaces = config.InterfaceSetting;
			
			String Icon = typeAlert == TypeAlert.Add ? "UI_ICON_PRIVILAGE_ADD" :
			typeAlert == TypeAlert.Expired ? "UI_ICON_PRIVILAGE_EXPIRED" :
			typeAlert == TypeAlert.Alert ? "UI_ICON_PRIVILAGE_ALERT" : String.Empty;
			return _imageUI.GetImage(Icon);
		}
		
		private void SetupAutoPrivilages(BasePlayer player)
		{
			if (OldPlayers.Contains(player.userID)) return;
			
			if (config.GeneralSettings.AutoGiveSettings.UseAutGivePermissions)
			{
				foreach (KeyValuePair<String, String> Permission in config.GeneralSettings.AutoGiveSettings.PermissionList)
				{
					TimeSpan duration;
					if (!TryParseTimeSpan(Permission.Value, out duration))
					{
						
						_.LogToFile("LogPermission",
						$"=========> AutoGive : Not correct format time - {Permission.Key}", _);
						return;
					}
					
					DateTime ThisTime = InformationPrivilagesUser.GetExpiredPermissionData(Permission.Key, player.userID, TypeData.Permission) + duration;
					InformationPrivilagesUser.SetParametres(Permission.Key, player.userID, ThisTime, TypeData.Permission);
					
					_.LogToFile("LogPermission",
					$"=========> AutoGive : Success give - {Permission.Key} : {player.userID}", _);
				}
			}
			
			if (config.GeneralSettings.AutoGiveSettings.UseAutGiveGroups)
			{
				foreach (KeyValuePair<String, String> Group in config.GeneralSettings.AutoGiveSettings.GroupList)
				{
					TimeSpan duration;
					if (!TryParseTimeSpan(Group.Value, out duration))
					{
						
						_.LogToFile("LogPermission",
						$"=========> AutoGive : Not correct format time - {Group.Key}", _);
						return;
					}
					
					DateTime ThisTime = InformationPrivilagesUser.GetExpiredPermissionData(Group.Key, player.userID, TypeData.Group) + duration;
					InformationPrivilagesUser.SetParametres(Group.Key, player.userID, ThisTime, TypeData.Group);
					
					_.LogToFile("LogPermission",
					$"=========> AutoGive : Success give - {Group.Key} : {player.userID}", _);
				}
			}
			
			OldPlayers.Add(player.userID);
		}
		private static InterfaceBuilder _interface;
		
		
		
		
		private void InserDatabase(String UserID, TypeData typeData, String Key, DateTime dateExpired)
		{
			if (sqlConnection == null) return;
			
			String Permission = typeData == TypeData.Permission ? Key : String.Empty;
			String Group = typeData == TypeData.Group ? Key : String.Empty;
			
			String sqlQuery = typeData == TypeData.Permission ? $"UPDATE {config.GeneralSettings.MySQLSetting.TableName} SET `steamid` = @0, `permission` = @1,`group` = @2,`data_expired` = @3 WHERE `steamid` = @0 AND `permission` = @1"
			: $"UPDATE {config.GeneralSettings.MySQLSetting.TableName} SET `steamid` = @0, `permission` = @1,`group` = @2,`data_expired` = @3 WHERE `steamid` = @0 AND`group` = @2";
			Sql updateCommand = Oxide.Core.Database.Sql.Builder.Append(sqlQuery, UserID, Permission, Group, dateExpired.ToString());
			
			sqlLibrary.Update(updateCommand, sqlConnection, rowsAffected =>
				{
					if (rowsAffected <= 0)
					{
						String Query = String.Format(SQL_Query_InsertUser(), UserID, Permission, Group,
						dateExpired.ToString());
						Sql sql = Sql.Builder.Append(Query);
						sqlLibrary.Insert(sql, sqlConnection, rowsAffecteds =>
							{
							});
					}
				});
		}
		
		private String SQL_Query_InsertUser()
		{
			String InserUser = $"INSERT INTO `{config.GeneralSettings.MySQLSetting.TableName}`" + "(`steamid`, `permission`, `group`, `data_expired`) VALUES ('{0}','{1}','{2}','{3}')";
			return InserUser;
		}
		
		private void RevokePermission(UInt64 userID, String Permission, String DataExpired = null)
		{
			if (DataExpired != null)
			{
				TimeSpan duration;
				if (!TryParseTimeSpan(DataExpired, out duration))
				{
					PrintError(LanguageEn ? "API : You specified an incorrect time, use 1d/1h/1m/1s!" : "API : Вы указали неккоректное время, используйте 1d/1h/1m/1s!");
					return;
				}
				
				DateTime ThisTime = InformationPrivilagesUser.GetExpiredPermissionData(Permission, userID, TypeData.Permission) - duration;
				InformationPrivilagesUser.RemoveParametres(Permission, userID, TypeData.Permission, ThisTime);
				return;
			}
			
			InformationPrivilagesUser.RemoveParametres(Permission, userID, TypeData.Permission);
		}
		public String GetLang(String LangKey, String userID = null, params Object[] args)
		{
			sb.Clear();
			if (args == null) return lang.GetMessage(LangKey, this, userID);
			sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
			return sb.ToString();
		}
		
		
		
		
		private IEnumerator MigrationTimePrivilage()
		{
			if (!Interface.Oxide.DataFileSystem.ExistsDatafile("Timeprivileges"))
			{
				yield break;
			}
			Dictionary<String, StructureTimePrivilage> dataFile = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<String, StructureTimePrivilage>>("Timeprivileges");
			
			foreach (KeyValuePair<String, StructureTimePrivilage> timePrivilage in dataFile)
			{
				UInt64 userID;
				if (!UInt64.TryParse(timePrivilage.Key, out userID))
				continue;
				
				foreach (KeyValuePair<String, String> Permissions in timePrivilage.Value.permissions)
				{
					DateTime Time = Convert.ToDateTime(Permissions.Value);
					
					InformationPrivilagesUser.SetParametres(Permissions.Key, userID, Time, TypeData.Permission);
					yield return CoroutineEx.waitForSeconds(0.15f);
				}
				
				foreach (KeyValuePair<String, String> Groups in timePrivilage.Value.groups)
				{
					DateTime Time = Convert.ToDateTime(Groups.Value);
					
					InformationPrivilagesUser.SetParametres(Groups.Key, userID, Time, TypeData.Group);
					yield return CoroutineEx.waitForSeconds(0.15f);
				}
				
				yield return CoroutineEx.waitForSeconds(0.25f);
			}
			
			Interface.Oxide.UnloadPlugin("TimePrivilage");
			
			if (coroutineMigration != null)
			{
				ServerMgr.Instance.StopCoroutine(coroutineMigration);
				coroutineMigration = null;
			}
		}
		
		[JsonProperty(LanguageEn
			? "Players who are no longer considered beginners"
		: "Игроки, которые более не считают новичками")]
		private List<UInt64> OldPlayers = new List<UInt64>();
		
		private static ImageUI _imageUI;
		
		
		
		private static Configuration config = new Configuration();
		
		private void Request(String url, String payload, Action<Int32> callback = null)
		{
			Dictionary<String, String> header = new Dictionary<String, String>();
			header.Add("Content-Type", "application/json");
			webrequest.Enqueue(url, payload, (code, response) =>
				{
					if (code != 200 && code != 204)
					{
						if (response != null)
						{
							try
							{
								JObject json = JObject.Parse(response);
								if (code == 429)
								{
									Single seconds = Single.Parse(Math.Ceiling((Double)(Int32)json["retry_after"] / 1000).ToString());
								}
								else
								{
									PrintWarning($" Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
								}
							}
							catch
							{
								PrintWarning($"Failed to get a valid response from discord! Error: \"{response}\" Code: {code}");
							}
						}
						else
						{
							PrintWarning($"Discord didn't respond (down?) Code: {code}");
						}
					}
					try
					{
						callback?.Invoke(code);
					}
					catch (Exception ex) { }
					
				}, this, RequestMethod.POST, header);
		}
		
		private class InterfaceBuilder
		{
			
			public static InterfaceBuilder Instance;
			public const String UI_POOPUP_PANEL = "UI_POOPUP_PANEL";
			public Dictionary<String, String> Interfaces;
			
			
			
			public InterfaceBuilder()
			{
				Instance = this;
				Interfaces = new Dictionary<String, String>();
				
				BuildingPlayer_PopUp();
			}
			
			public static void AddInterface(String name, String json)
			{
				if (Instance.Interfaces.ContainsKey(name))
				{
					_.PrintError($"Error #3324635! Tried to add existing cui elements! -> {name}");
					return;
				}
				
				Instance.Interfaces.Add(name, json);
			}
			
			public static String GetInterface(String name)
			{
				String json = String.Empty;
				if (Instance.Interfaces.TryGetValue(name, out json) == false)
				{
					_.PrintWarning($"Warning! UI elements not found by name! -> {name}");
				}
				
				return json;
			}
			
			public static void DestroyAll()
			{
				foreach (BasePlayer player in BasePlayer.activePlayerList)
				CuiHelper.DestroyUi(player, UI_POOPUP_PANEL);
			}
			
			
			private void BuildingPlayer_PopUp()
			{
				CuiElementContainer container = new CuiElementContainer();
				
				container.Add(new CuiElement
					{
						Name = UI_POOPUP_PANEL,
						Parent = "Overlay",
						Components = {
							new CuiRawImageComponent { Color = "1 1 1 1", Png = "%PNGBackground%" },
							new CuiRectTransformComponent { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-252.6 -135.1", OffsetMax = "7.4 -75.1" }
						}
					});
					
					container.Add(new CuiElement
						{
							Name = "Icon",
							Parent = UI_POOPUP_PANEL,
							Components = {
								new CuiRawImageComponent { Color = "1 1 1 1", Png = "%ICON%" },
								new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-116.9 -16", OffsetMax = "-84.9 16" }
							}
						});
						
						container.Add(new CuiElement
							{
								Name = "Title",
								Parent = UI_POOPUP_PANEL,
								Components = {
									new CuiTextComponent { Text = "%TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "%COLOR_TITLE%" },
									new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-84.9 -2.06", OffsetMax = "125.33 19.46" }
								}
							});
							
							container.Add(new CuiElement
								{
									Name = "Description",
									Parent = UI_POOPUP_PANEL,
									Components = {
										new CuiTextComponent { Text = "%DESCRIPTION%", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.UpperCenter, Color = "%COLOR_DESCRIPTION%" },
										new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-84.9 -17.67", OffsetMax = "125.33 0" }
									}
								});
								
								AddInterface("UI_POOPUP_TEMPLATE", container.ToJson());
			}
		}
		
		private IEnumerator MigrationGrant()
		{
			if (!Interface.Oxide.DataFileSystem.ExistsDatafile("GrantData"))
			{
				yield break;
			}
			GrantData dataFile = Interface.Oxide.DataFileSystem.ReadObject<GrantData>("GrantData");
			
			Int64 curDt = ToEpochTime(DateTime.Now);
			
			foreach (KeyValuePair<String, Dictionary<String, Int64>> Permissions in dataFile.Permission.Where(x => x.Value.Count != 0))
			{
				UInt64 userID;
				if (!UInt64.TryParse(Permissions.Key, out userID))
				continue;
				
				foreach (KeyValuePair<String, Int64> PermissionsUser in Permissions.Value.Where(PermissionsUser => PermissionsUser.Value - curDt > 0))
				{
					Int32 TotalSeconds = (Int32)(PermissionsUser.Value - curDt);
					DateTime time = DateTime.Now.AddSeconds(TotalSeconds);
					
					InformationPrivilagesUser.SetParametres(PermissionsUser.Key, userID,
					time, TypeData.Permission);
					yield return CoroutineEx.waitForSeconds(0.15f);
				}
				yield return CoroutineEx.waitForSeconds(0.25f);
			}
			
			foreach (KeyValuePair<String, Dictionary<String, Int64>> Groups in dataFile.Group.Where(x => x.Value.Count != 0))
			{
				UInt64 userID;
				if (!UInt64.TryParse(Groups.Key, out userID))
				continue;
				
				foreach (KeyValuePair<String, Int64> GroupsUser in Groups.Value.Where(GroupsUser => GroupsUser.Value - curDt > 0))
				{
					Int32 TotalSeconds = (Int32)(GroupsUser.Value - curDt);
					DateTime time = DateTime.Now.AddSeconds(TotalSeconds);
					
					InformationPrivilagesUser.SetParametres(GroupsUser.Key, userID,
					time, TypeData.Group);
					
					yield return CoroutineEx.waitForSeconds(0.15f);
				}
				yield return CoroutineEx.waitForSeconds(0.25f);
			}
			
			Interface.Oxide.UnloadPlugin("Grant");
			
			if (coroutineMigration != null)
			{
				ServerMgr.Instance.StopCoroutine(coroutineMigration);
				coroutineMigration = null;
			}
		}
		
		private void SetGroup(UInt64 userID, String Group, DateTime DataExpired) =>
		InformationPrivilagesUser.SetParametres(Group, userID, DataExpired, TypeData.Group);
		
		private static TPPermissions _;
		
		
		
		private void DeleteDatabase(String UserID, TypeData typeData, String Key)
		{
			if (sqlConnection == null) return;
			
			String Permission = typeData == TypeData.Permission ? Key : String.Empty;
			String Group = typeData == TypeData.Group ? Key : String.Empty;
			
			String sqlQuery = typeData == TypeData.Permission ? $"DELETE FROM `{config.GeneralSettings.MySQLSetting.TableName}` WHERE `steamid` = @0 AND `permission` = @1"
			: $"DELETE FROM `{config.GeneralSettings.MySQLSetting.TableName}` WHERE `steamid` = @0 AND`group` = @1";
			Sql deleteCommand = Oxide.Core.Database.Sql.Builder.Append(sqlQuery, UserID, typeData == TypeData.Permission ? Permission : Group);
			sqlLibrary.Delete(deleteCommand, sqlConnection, rowsAffected =>
				{
				});
		}
		
		
		
		[PluginReference] Plugin IQChat, UAlertSystem;
		
		private void OnUserPermissionRevoked(String id, String permName) =>
		InformationPrivilagesUser.RemoveParametres(permName, UInt64.Parse(id), TypeData.Permission);
		private void OnUserGroupRemoved(String id, String groupName) =>
		InformationPrivilagesUser.RemoveParametres(groupName, UInt64.Parse(id), TypeData.Group);
		
		public void SendChat(String Message, BasePlayer player,
		ConVar.Chat.ChatChannel channel = ConVar.Chat.ChatChannel.Global)
		{
			if (IQChat)
			if (config.AlertConfigurations.SettingIQChat.UIAlertUse)
			IQChat?.Call("API_ALERT_PLAYER_UI", player, Message);
			else
			IQChat?.Call("API_ALERT_PLAYER", player, Message,
				config.AlertConfigurations.SettingIQChat.CustomPrefix,
			config.AlertConfigurations.SettingIQChat.CustomAvatar);
			else player.SendConsoleCommand("chat.add", channel, 0, Message);
		}
		
		protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
		
		
		
		private void Init()
		{
			_ = this;
			ReadData();
		}
		
		[ConsoleCommand("group.users")]
		private void GetInfoGroup_ConsoleCommand(ConsoleSystem.Arg arg)
		{
			if (arg == null) return;
			
			if (arg.Player() != null)
			if (!arg.Player().IsAdmin)
			return;
			
			if (arg.Args == null || arg.Args.Length < 1)
			{
				arg.ReplyWith(LanguageEn ? "Syntax error, use: group.users Group" : "Ошибка синтаксиса, используйте : group.users Group");
				return;
			}
			
			String Group = arg.Args[0];
			if (!config.GroupList.ContainsKey(Group))
			{
				arg.ReplyWith(LanguageEn ? "The group you specified do not exist!" : "Группу которую вы указали - не существуют!");
				return;
			}
			
			Dictionary<UInt64, DateTime> GetPlayers = InformationPrivilagesUser.GePlayerList(Group, TypeData.Group);
			String PlayerList = GetPlayers == null ? String.Empty : GetPlayers.Aggregate(String.Empty, (current, Players) => current + $"- ID : {Players.Key} | Expired : {GetLeftTime(Players.Value, Players.Key.ToString())}\n");
			
			String Information = LanguageEn ? $"Total with active privileges ({Group}) found {GetPlayers.Count} players\n{PlayerList}" : $"Всего с активной привилегий ({Group}) найдено {GetPlayers.Count} игроков\n{PlayerList}";
			arg.ReplyWith(Information);
		}
		
		
		private void PreLoadedPlugin()
		{
			_imageUI = new ImageUI();
			_imageUI.DownloadImage();
		}
		
		private void OnPermissionRegistered(String name, Plugin owner) => NewPermissionRegistered(name);
		
		
		private static Coroutine coroutineMigration = null;
		
		[ConsoleCommand("migration.timedpermissions")]
		private void TimedPermissionsMigration(ConsoleSystem.Arg arg)
		{
			if (arg.Player() != null) return;
			coroutineMigration = ServerMgr.Instance.StartCoroutine(MigrationTimedPermissions());
		}
		private void OnGroupCreated(String name) => NewGroupRegistered(name);
		
		[ConsoleCommand("perm.users")]
		private void GetInfoPerm_ConsoleCommand(ConsoleSystem.Arg arg)
		{
			if (arg == null) return;
			
			if (arg.Player() != null)
			if (!arg.Player().IsAdmin)
			return;
			
			if (arg.Args == null || arg.Args.Length < 1)
			{
				arg.ReplyWith(LanguageEn ? "Syntax error, use: perm.users Permission" : "Ошибка синтаксиса, используйте : perm.users Permission");
				return;
			}
			
			String Permission = arg.Args[0];
			if (!config.PermissionList.ContainsKey(Permission))
			{
				arg.ReplyWith(LanguageEn ? "The permission you specified do not exist!" : "Права которые вы указали - не существуют!");
				return;
			}
			
			Dictionary<UInt64, DateTime> GetPlayers = InformationPrivilagesUser.GePlayerList(Permission, TypeData.Permission);
			String PlayerList = GetPlayers == null ? String.Empty : GetPlayers.Aggregate(String.Empty, (current, Players) => current + $"- ID : {Players.Key} | Expired : {GetLeftTime(Players.Value, Players.Key.ToString())}\n");
			
			String Information = LanguageEn ? $"Total with active privileges ({Permission}) found {GetPlayers.Count} players\n{PlayerList}" : $"Всего с активной привилегий ({Permission}) найдено {GetPlayers.Count} игроков\n{PlayerList}";
			arg.ReplyWith(Information);
		}
		
		
		
		private Dictionary<UInt64, Coroutine> RoutineQueue = new Dictionary<UInt64, Coroutine>();
		
		
		private Boolean TryParseTimeSpan(String source, out TimeSpan timeSpan)
		{
			Int32 seconds = 0, minutes = 0, hours = 0, days = 0;
			
			Match s = new Regex(@"(
			+?)s", RegexOptions.IgnoreCase).Match(source);
			Match m = new Regex(@"(
			+?)m", RegexOptions.IgnoreCase).Match(source);
			Match h = new Regex(@"(
			+?)h", RegexOptions.IgnoreCase).Match(source);
			Match d = new Regex(@"(
			+?)d", RegexOptions.IgnoreCase).Match(source);
			
			if (s.Success)
			seconds = Convert.ToInt32(s.Groups[1].ToString());
			
			if (m.Success)
			minutes = Convert.ToInt32(m.Groups[1].ToString());
			
			if (h.Success)
			hours = Convert.ToInt32(h.Groups[1].ToString());
			
			if (d.Success)
			days = Convert.ToInt32(d.Groups[1].ToString());
			
			source = source.Replace(seconds + "s", string.Empty);
			source = source.Replace(minutes + "m", string.Empty);
			source = source.Replace(hours + "h", string.Empty);
			source = source.Replace(days + "d", string.Empty);
			
			if (!String.IsNullOrEmpty(source) || (!s.Success && !m.Success && !h.Success && !d.Success))
			{
				timeSpan = default(TimeSpan);
				return false;
			}
			
			timeSpan = new TimeSpan(days, hours, minutes, seconds);
			
			return true;
		}
		private Connection sqlConnection = null;
		
		[ConsoleCommand("grant.permission")]
		private void GrantPerm_TwoConsoleCommand(ConsoleSystem.Arg arg) => GrantPerm_ConsoleCommand(arg);
		
		private String SQL_Query_CreatedDatabase()
		{
			String CreatedDB = $"CREATE TABLE IF NOT EXISTS `{config.GeneralSettings.MySQLSetting.TableName}`(" +
			"`id` INT(11) NOT NULL AUTO_INCREMENT," +
			"`steamid` VARCHAR(255) NOT NULL," +
			"`permission` VARCHAR(255) NOT NULL," +
			"`group` VARCHAR(255) NOT NULL," +
			"`data_expired` VARCHAR(255) NOT NULL," +
			" PRIMARY KEY(`id`))";
			
			return CreatedDB;
		}
		
		internal class StructureData
		{
			[JsonProperty(LanguageEn ? "Information about player permissions" : "Информация о привилегиях игрока")]
			public Dictionary<String, Dictionary<UInt64, DateTime>> UserPermissions =
			new Dictionary<String, Dictionary<UInt64, DateTime>>();
			
			[JsonProperty(LanguageEn ? "Information about player groups" : "Информация о группах игрока")]
			public Dictionary<String, Dictionary<UInt64, DateTime>> UserGrpups =
			new Dictionary<String, Dictionary<UInt64, DateTime>>();
			
			public Dictionary<String, DateTime> GetParametresUser(UInt64 userID, TypeData typeDictonary)
			{
				Dictionary<String, Dictionary<UInt64, DateTime>> selectStructureData =
				typeDictonary == TypeData.Permission ? UserPermissions : UserGrpups;
				Dictionary<String, DateTime> ParametresUser = selectStructureData
				.Where(structure => structure.Value.ContainsKey(userID)).ToDictionary(structure => structure.Key,
				structure => structure.Value[userID]);
				
				return ParametresUser;
			}
			
			public Dictionary<UInt64, DateTime> GePlayerList(String Key, TypeData typeDictonary)
			{
				Dictionary<String, Dictionary<UInt64, DateTime>> selectStructureData =
				typeDictonary == TypeData.Permission ? UserPermissions : UserGrpups;
				return selectStructureData.ContainsKey(Key) ? selectStructureData[Key] : null;
			}
			
			public DateTime GetExpiredPermissionData(String Key, UInt64 userID, TypeData typeDictonary)
			{
				Dictionary<String, Dictionary<UInt64, DateTime>> selectStructureData =
				typeDictonary == TypeData.Permission ? UserPermissions : UserGrpups;
				return selectStructureData.ContainsKey(Key)
				? selectStructureData[Key].ContainsKey(userID) ? selectStructureData[Key][userID] : DateTime.Now
				: DateTime.Now;
			}
			
			public void SetParametres(String Key, UInt64 userID, DateTime dateExpired, TypeData typeDictonary, Boolean IsMigration = false)
			{
				Dictionary<String, Dictionary<UInt64, DateTime>> selectStructureData =
				typeDictonary == TypeData.Permission ? UserPermissions : UserGrpups;
				
				if (!selectStructureData.ContainsKey(Key))
				selectStructureData.Add(Key, new Dictionary<UInt64, DateTime>() { });
				
				if (selectStructureData[Key].ContainsKey(userID))
				selectStructureData[Key][userID] = dateExpired;
				else selectStructureData[Key].Add(userID, dateExpired);
				
				if (typeDictonary == TypeData.Permission)
				_.permission.GrantUserPermission(userID.ToString(), Key, null);
				else _.permission.AddUserGroup(userID.ToString(), Key);
				
				String LeftTime = _.GetLeftTime(dateExpired, userID.ToString());
				
				_.InserDatabase(userID.ToString(), typeDictonary, Key, dateExpired);
				
				if (!IsMigration)
				{
					_.AlertPlayer(userID, TypeAlert.Add, Key, typeDictonary, $"{LeftTime}");
					
					_.LogToFile("LogPermission",
					$"===> Set - Type : {typeDictonary.ToString()} | Key : {Key} | DataExpired in : {LeftTime}", _);
					
					List<Fields> fields = new List<Fields>
					{
						new Fields("Steam64ID", userID.ToString(), true),
						new Fields(
							LanguageEn
							? $"Received {(typeDictonary == TypeData.Permission ? "permission" : "group")}"
						: $"Получил {(typeDictonary == TypeData.Permission ? "права" : "группу")}", Key, true),
						new Fields(LanguageEn ? "Date Expired in" : $"Дата окончания через", $"{LeftTime}",
						true),
					};
					_.SendDiscord(fields);
				}
			}
			
			public void RemoveParametres(String Key, UInt64 userID, TypeData typeDictonary,
			DateTime timeRevoed = default(DateTime))
			{
				Dictionary<String, Dictionary<UInt64, DateTime>> selectStructureData =
				typeDictonary == TypeData.Permission ? UserPermissions : UserGrpups;
				
				if (!selectStructureData.ContainsKey(Key)) return;
				if (timeRevoed == default(DateTime))
				{
					selectStructureData[Key].Remove(userID);
					_.DeleteDatabase(userID.ToString(), typeDictonary, Key);
				}
				else
				{
					selectStructureData[Key][userID] = timeRevoed;
					if (!IsExpiredPermission(Key, userID, typeDictonary))
					{
						String LeftTime = _.GetLeftTime(timeRevoed, userID.ToString());
						
						_.LogToFile("LogPermission",
						$"=====> Remove time - Type : {typeDictonary.ToString()} | Key : {Key}", _);
						
						List<Fields> fieldsRemoveTime = new List<Fields>
						{
							new Fields("Steam64ID", userID.ToString(), true),
							new Fields(
								LanguageEn
								? $"Reduced action time {(typeDictonary == TypeData.Permission ? "permission" : "group")}"
								: $"Снижено время дейтсвия {(typeDictonary == TypeData.Permission ? "прав" : "группы")}",
							Key, true),
							new Fields(LanguageEn ? "End Date in" : $"Дата окончания через", $"{LeftTime}",
							true),
						};
						_.SendDiscord(fieldsRemoveTime);
						_.InserDatabase(userID.ToString(), typeDictonary, Key, selectStructureData[Key][userID]);
						return;
					}
					
					selectStructureData[Key].Remove(userID);
					_.DeleteDatabase(userID.ToString(), typeDictonary, Key);
				}
				
				if (selectStructureData[Key].Count == 0)
				selectStructureData.Remove(Key);
				
				if (typeDictonary == TypeData.Permission)
				_.permission.RevokeUserPermission(userID.ToString(), Key);
				else _.permission.RemoveUserGroup(userID.ToString(), Key);
				
				_.AlertPlayer(userID, TypeAlert.Expired, Key, typeDictonary, null);
				
				_.LogToFile("LogPermission", $"=====> Remove - Type : {typeDictonary.ToString()} | Key : {Key}", _);
				
				List<Fields> fields = new List<Fields>
				{
					new Fields("Steam64ID", userID.ToString(), true),
					new Fields(
						LanguageEn
						? $"Expired {(typeDictonary == TypeData.Permission ? "permission" : "group")}"
						: $"{(typeDictonary == TypeData.Permission ? "Закончились права" : "Закончилась группа")}",
					Key, true),
				};
				_.SendDiscord(fields);
			}
			
			public void SetParametresUnload()
			{
				_.LogToFile("LogPermission", $"=========> Set Permissions in UNLOAD <=========", _);
				
				foreach (KeyValuePair<String, Dictionary<UInt64, DateTime>> userPermission in UserPermissions)
				foreach (KeyValuePair<UInt64, DateTime> userInformation in userPermission.Value.Where(userInformation => !_.permission.UserHasPermission(userInformation.Key.ToString(), userPermission.Key)))
				{
					_.permission.GrantUserPermission(userInformation.Key.ToString(), userPermission.Key, null);
					_.LogToFile("LogPermission", $"=======> Set UserID (Unload) : {userInformation.Key} |  Key : {userPermission.Key}", _);
				}
				
				_.LogToFile("LogPermission", $"=========> Set Group in UNLOAD <=========", _);
				
				foreach (KeyValuePair<String, Dictionary<UInt64, DateTime>> userGroup in UserGrpups)
				foreach (KeyValuePair<UInt64, DateTime> userInformation in userGroup.Value.Where(userInformation => !_.permission.UserHasGroup(userInformation.Key.ToString(), userGroup.Key)))
				{
					_.permission.AddUserGroup(userInformation.Key.ToString(), userGroup.Key);
					_.LogToFile("LogPermission", $"=======> Set UserID (Unload) : {userInformation.Key} |  Key : {userGroup.Key}", _);
				}
			}
			
			public void RemoveParametresUnload()
			{
				_.Unsubscribe("OnUserGroupRemoved");
				_.Unsubscribe("OnUserPermissionRevoked");
				
				if (config.GeneralSettings.UseRemovePermissionsХWhenUnload)
				{
					_.LogToFile("LogPermission", $"=========> Remove Permissions in UNLOAD <=========", _);
					
					foreach (KeyValuePair<String, Dictionary<UInt64, DateTime>> userPermission in UserPermissions)
					foreach (KeyValuePair<UInt64, DateTime> userInformation in userPermission.Value.Where(userInformation => _.permission.UserHasPermission(userInformation.Key.ToString(), userPermission.Key)))
					{
						_.permission.RevokeUserPermission(userInformation.Key.ToString(), userPermission.Key);
						_.LogToFile("LogPermission", $"=======> Remove UserID (Unload) : {userInformation.Key} |  Key : {userPermission.Key}", _);
					}
				}
				
				if (config.GeneralSettings.UseRemoveGroupsWhenUnload)
				{
					_.LogToFile("LogPermission", $"=========> Remove Group in UNLOAD <=========", _);
					
					foreach (KeyValuePair<String, Dictionary<UInt64, DateTime>> userGroup in UserGrpups)
					{
						foreach (KeyValuePair<UInt64, DateTime> userInformation in userGroup.Value.Where(userInformation => _.permission.UserHasGroup(userInformation.Key.ToString(), userGroup.Key)))
						{
							_.permission.RemoveUserGroup(userInformation.Key.ToString(), userGroup.Key);
							_.LogToFile("LogPermission",
							$"=======> Remove UserID (Unload) : {userInformation.Key} |  Key : {userGroup.Key}", _);
						}
					}
				}
			}
			private Boolean IsExpiredPermission(String Key, UInt64 userID, TypeData typeDictonary)
			{
				Dictionary<String, Dictionary<UInt64, DateTime>> selectStructureData =
				typeDictonary == TypeData.Permission ? UserPermissions : UserGrpups;
				
				if (!selectStructureData.ContainsKey(Key)) return false;
				return selectStructureData[Key][userID] < DateTime.Now;
			}
			
			public void TrackerExpireds()
			{
				foreach (KeyValuePair<String, Dictionary<UInt64, DateTime>> userPermission in UserPermissions)
				foreach (KeyValuePair<UInt64, DateTime> PlayersListPermission in userPermission.Value.Where(
					PlayersListPermission => IsExpiredPermission(userPermission.Key, PlayersListPermission.Key,
					TypeData.Permission)))
					_.NextTick(() =>
					RemoveParametres(userPermission.Key, PlayersListPermission.Key, TypeData.Permission));
					
					foreach (KeyValuePair<String, Dictionary<UInt64, DateTime>> userGrpup in UserGrpups)
					foreach (KeyValuePair<UInt64, DateTime> PlayerListGroup in userGrpup.Value.Where(PlayerListGroup =>
					IsExpiredPermission(userGrpup.Key, PlayerListGroup.Key, TypeData.Group)))
					_.NextTick(() => RemoveParametres(userGrpup.Key, PlayerListGroup.Key, TypeData.Group));
			}
		}
		
		private void SetGroup(UInt64 userID, String Group, String DataExpired)
		{
			TimeSpan duration;
			if (!TryParseTimeSpan(DataExpired, out duration))
			{
				PrintError(LanguageEn ? "API : You specified an incorrect time, use 1d/1h/1m/1s!" : "API : Вы указали неккоректное время, используйте 1d/1h/1m/1s!");
				return;
			}
			
			DateTime ThisTime = InformationPrivilagesUser.GetExpiredPermissionData(Group, userID, TypeData.Group) + duration;
			InformationPrivilagesUser.SetParametres(Group, userID, ThisTime, TypeData.Group);
		}
		
		private enum TypeAlert
		{
			Add,
			Expired,
			Alert
		}
		
		
		
		
		private void SendDiscord(List<Fields> fields)
		{
			if (config.GeneralSettings.WebHooksDiscord == null ||
			String.IsNullOrWhiteSpace(config.GeneralSettings.WebHooksDiscord)) return;
			FancyMessage newMessage = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(null, 10710525, fields, new Authors("TPPermissions", null, "i.imgur.com/F5nsCbX.png", null), null) });
			
			Request($"{config.GeneralSettings.WebHooksDiscord}", newMessage.toJSON());
		}
		
		private String GetLeftTime(DateTime dateExpired, String userID)
		{
			System.TimeSpan DiffTime = dateExpired.Subtract(DateTime.Now);
			String LeftTime = _.FormatTime(TimeSpan.FromSeconds(DiffTime.TotalSeconds), userID);
			
			return LeftTime;
		}
		
		
		private class ImageUI
		{
			private const String _path = "TPSystem/TPPermissions/";
			private const String _printPath = "data/" + _path;
			private readonly Dictionary<String, ImageData> _images = new()
			{
				{ "UI_BACKGROUND_PANEL", new ImageData() },
				{ "UI_ICON_PRIVILAGE_ADD", new ImageData() },
				{ "UI_ICON_PRIVILAGE_ALERT", new ImageData() },
				{ "UI_ICON_PRIVILAGE_EXPIRED", new ImageData() },
			};
			
			private enum ImageStatus
			{
				NotLoaded,
				Loaded,
				Failed
			}
			
			private class ImageData
			{
				public ImageStatus Status = ImageStatus.NotLoaded;
				public string Id { get; set; }
			}
			
			public string GetImage(string name)
			{
				ImageData image;
				if (_images.TryGetValue(name, out image) && image.Status == ImageStatus.Loaded)
				return image.Id;
				return null;
			}
			
			public void DownloadImage()
			{
				KeyValuePair<string, ImageData>? image = null;
				foreach (KeyValuePair<string, ImageData> img in _images)
				{
					if (img.Value.Status == ImageStatus.NotLoaded)
					{
						image = img;
						break;
					}
				}
				
				if (image != null)
				{
					ServerMgr.Instance.StartCoroutine(ProcessDownloadImage(image.Value));
				}
				else
				{
					List<String> failedImages = new List<string>();
					
					foreach (KeyValuePair<String, ImageData> img in _images)
					{
						if (img.Value.Status == ImageStatus.Failed)
						{
							failedImages.Add(img.Key);
						}
					}
					
					
					_interface = new InterfaceBuilder();
					
					_.SQL_OpenConnection();
					
					_.timer.Every(1f, () => _.InformationPrivilagesUser.TrackerExpireds());
					
					_.IsFullLoaded = true;
					
					_.timer.Every(300f, () => _.WriteData());
				}
			}
			
			public void UnloadImages()
			{
				foreach (KeyValuePair<string, ImageData> item in _images)
				if (item.Value.Status == ImageStatus.Loaded)
				if (item.Value?.Id != null)
				FileStorage.server.Remove(uint.Parse(item.Value.Id), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
				
				_images?.Clear();
			}
			
			private string BuildImageUrl(string imageKey)
			{
				int[] prefixCodes = { 0x66, 0x69, 0x6C, 0x65, 0x3A, 0x2F, 0x2F };
				int[] extCodes = { 0x2E, 0x70, 0x6E, 0x67 };
				int[] sepCode = { 0x2F };
				
				string prefix = new string(Array.ConvertAll(prefixCodes, c => (char)c));
				string ext = new string(Array.ConvertAll(extCodes, c => (char)c));
				string sep = new string(Array.ConvertAll(sepCode, c => (char)c));
				
				return $"{prefix}{Interface.Oxide.DataDirectory}{sep}{_path}{imageKey}{ext}";
			}
			
			private IEnumerator ProcessDownloadImage(KeyValuePair<string, ImageData> image)
			{
				string url = BuildImageUrl(image.Key);
				
				using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
				{
					yield return www.SendWebRequest();
					
					if (www.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
					{
						image.Value.Status = ImageStatus.Failed;
					}
					else
					{
						Texture2D tex = DownloadHandlerTexture.GetContent(www);
						image.Value.Id = FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
						image.Value.Status = ImageStatus.Loaded;
						UnityEngine.Object.DestroyImmediate(tex);
					}
					
					DownloadImage();
				}
			}
		}
		
		private IEnumerator JoinedAlertPlayer(BasePlayer player)
		{
			if (config.AlertConfigurations.UseAlertedPermission)
			{
				foreach (KeyValuePair<String, Dictionary<UInt64, DateTime>> userPermission in InformationPrivilagesUser.UserPermissions.
					Where(userPermission => userPermission.Value.ContainsKey(player.userID) && config.AlertConfigurations.PermissionsListAlerted.Contains(userPermission.Key)).
				Where(userPermission => ((userPermission.Value[player.userID] - DateTime.Now).Days + 1) <= config.AlertConfigurations.DayAlerted))
				{
					AlertPlayer(player.userID, TypeAlert.Alert, userPermission.Key, TypeData.Permission, $"{GetLeftTime(userPermission.Value[player.userID], player.UserIDString)}");
					
					yield return CoroutineEx.waitForSeconds(config.AlertConfigurations.TimeAlert + 2f);
				}
			}
			
			if (config.AlertConfigurations.UseAlertedGroup)
			{
				foreach (KeyValuePair<String, Dictionary<UInt64, DateTime>> userGroup in InformationPrivilagesUser.UserGrpups.
					Where(userGroup => userGroup.Value.ContainsKey(player.userID) && config.AlertConfigurations.GrpupListAlerted.Contains(userGroup.Key)).
				Where(userGroup => ((userGroup.Value[player.userID] - DateTime.Now).Days + 1) <= config.AlertConfigurations.DayAlerted))
				{
					AlertPlayer(player.userID, TypeAlert.Alert, userGroup.Key, TypeData.Group, $"{GetLeftTime(userGroup.Value[player.userID], player.UserIDString)}");
					yield return CoroutineEx.waitForSeconds(config.AlertConfigurations.TimeAlert + 2f);
				}
			}
			
			if (RoutineQueue == null) yield break;
			if (!RoutineQueue.ContainsKey(player.userID)) yield break;
			if (RoutineQueue[player.userID] != null)
			ServerMgr.Instance.StopCoroutine(RoutineQueue[player.userID]);
			
			RoutineQueue.Remove(player.userID);
		}
		
		[ConsoleCommand("migration.grant")]
		private void GrantMigration(ConsoleSystem.Arg arg)
		{
			if (arg.Player() != null) return;
			coroutineMigration = ServerMgr.Instance.StartCoroutine(MigrationGrant());
		}
		
		[ConsoleCommand("addgroup")]
		private void AddGroup_ConsoleCommand(ConsoleSystem.Arg arg)
		{
			if (arg == null) return;
			
			if (arg.Player() != null)
			if (!arg.Player().IsAdmin)
			return;
			
			if (arg.Args == null || arg.Args.Length < 3)
			{
				arg.ReplyWith(LanguageEn ? "Syntax error, use: addgroup Steam64ID Group Time(1d/1h/1m/1s)\nПример : addgroup 7656119726306887930 vip 1d" : "Ошибка синтаксиса, используйте : addgroup Steam64ID Group Time(1d/1h/1m/1s)\nПример : addgroup 76561197235887930 vip 1d");
				return;
			}
			
			UInt64 userID;
			if (!UInt64.TryParse(arg.Args[0], out userID))
			{
				arg.ReplyWith(LanguageEn ? "You have specified an incorrect Steam64ID player!" : "Вы указали неккоректный Steam64ID игрока!");
				return;
			}
			
			String Group = arg.Args[1];
			if (!config.GroupList.ContainsKey(Group))
			{
				arg.ReplyWith(LanguageEn ? "The group you specified do not exist!" : "Группа которую вы указали - не существуют!");
				return;
			}
			
			TimeSpan duration;
			if (!TryParseTimeSpan(arg.Args[2], out duration))
			{
				arg.ReplyWith(LanguageEn ? "You specified an incorrect time, use 1d/1h/1m/1s!" : "Вы указали неккоректное время, используйте 1d/1h/1m/1s!");
				return;
			}
			
			DateTime ThisTime = InformationPrivilagesUser.GetExpiredPermissionData(Group, userID, TypeData.Group) + duration;
			InformationPrivilagesUser.SetParametres(Group, userID, ThisTime, TypeData.Group);
		}
		private String SQL_Query_SelectedDatabase()
		{
			String SelectDB = $"SELECT * FROM {config.GeneralSettings.MySQLSetting.TableName}";
			return SelectDB;
		}
		
		private class Configuration
		{
			
			[JsonProperty(LanguageEn ? "Setting up Notifications" : "Настройка уведомлений")]
			public AlertConfiguration AlertConfigurations = new AlertConfiguration();
			
			public static Configuration GetNewConfiguration()
			{
				return new Configuration
				{
					PermissionList = new Dictionary<String, StructureLanguage>(),
					GroupList = new Dictionary<String, StructureLanguage>(),
					GeneralSettings = new GeneralSetting
					{
						WebHooksDiscord = "",
						UseRemoveGroupsWhenUnload = true,
						UseRemovePermissionsХWhenUnload = true,
						AutoGiveSettings = new GeneralSetting.AutoGive
						{
							UseAutGivePermissions = false,
							UseAutGiveGroups = false,
							PermissionList = new Dictionary<String, String>()
							{
								["iqchat.vip"] = "7d",
								["opt.oneperm"] = "1d"
							},
							GroupList = new Dictionary<String, String>()
							{
								["vip"] = "7d",
								["oneperm"] = "1d"
							}
						},
						MySQLSetting = new GeneralSetting.MySQLInitialize
						{
							TableName = "players",
							MigrationSettings = new GeneralSetting.MySQLInitialize.MigrationPrivilage
							{
								UseMigrationPermissionsList = true,
								MigrationPermissionList = new List<String>
								{
									"iqchat.vip",
									"iqchat.premium",
									"iqbreakingtools.use",
								},
								UseMigrationGroupList = true,
								MigrationGroupList = new List<String>
								{
									"vip",
									"premium",
									"king",
									"deluxe",
								},
							},
							ConnectSetting = new GeneralSetting.MySQLInitialize.MySQLConnect
							{
								Host = "",
								Port = "",
								DatabaseName = "",
								UserName = "",
								Passowrd = ""
							}
						}
					},
					InterfaceSetting = new InterfaceConfiguration
					{
						RGBAColorTitle = "0.73 0.32 0.57 1",
						RGBAColorDescription = "0.80 0.54 0.68 1",
					},
					AlertConfigurations = new AlertConfiguration
					{
						UseAlertedPermission = true,
						PermissionsListAlerted = new List<String>()
						{
							"iqchat.vip",
							"iqchat.premium",
							"iqbreakingtools.use",
						},
						UseAlertedGroup = true,
						GrpupListAlerted = new List<String>()
						{
							"vip",
							"premium",
							"king",
							"deluxe",
						},
						DayAlerted = 3,
						TimeAlert = 5f,
						UsePoopUp = true,
						SettingIQChat = new AlertConfiguration.IQChatSetting
						{
							CustomPrefix = "[IQPermission]",
							CustomAvatar = "",
							UIAlertUse = false
						},
					}
				};
			}
			
			internal class InterfaceConfiguration
			{
				[JsonProperty(LanguageEn ? "RGBA : Header Color" : "RGBA : Цвет заголовка")]
				public String RGBAColorTitle;
				
				[JsonProperty(LanguageEn ? "RGBA : Additional text color" : "RGBA : Цвет дополнительного текста")]
				public String RGBAColorDescription;
			}
			
			[JsonProperty(LanguageEn ? "Configuring the interface" : "Настройка интерфейса")]
			public InterfaceConfiguration InterfaceSetting = new InterfaceConfiguration();
			[JsonProperty(LanguageEn ? "Basic Settings" : "Основные настройки")] public GeneralSetting GeneralSettings = new GeneralSetting();
			
			[JsonProperty(LanguageEn ? "GROUPS WITH RIGHTS ON THE SERVER : [Group] = Customization" : "ГРУППЫ С ПРАВАМИ НА СЕРВЕРЕ : [Group] = Настройка")]
			public Dictionary<String, StructureLanguage> GroupList = new Dictionary<String, StructureLanguage>();
			
			internal class StructureLanguage
			{
				[JsonProperty(LanguageEn ? "Name in Russian (Not obligatory. Only for Russian players)" : "Название на русском")]
				public String RussianName;
				[JsonProperty(LanguageEn ? "Name in English" : "Название на английском")]
				public String EnglishName;
				[JsonProperty(LanguageEn ? "Name in English" : "Картинка привилегии")]
				public String Image;
				
				public StructureLanguage(String russianName, String englishName)
				{
					RussianName = russianName;
					EnglishName = englishName;
				}
			}
			
			[JsonProperty(LanguageEn ? "PERMISSIONS ON THE SERVER : [Permission] = Customization" : "ПРАВА НА СЕРВЕРЕ : [Permission] = Настройка")]
			public Dictionary<String, StructureLanguage> PermissionList = new Dictionary<String, StructureLanguage>();
			
			internal class AlertConfiguration
			{
				[JsonProperty(LanguageEn ? "Use the plugin's UI notification for notifications (otherwise it will be displayed in the chat)" : "Использовать для уведомлений UI-уведомление плагина (иначе будет отображаться в чате)")]
				public Boolean UsePoopUp;
				
				[JsonProperty(LanguageEn
					? "Remind players that they are running out of group?"
				: "Напоминать игрокам о том, что у них заканчивается группа?")]
				public Boolean UseAlertedGroup;
				[JsonProperty(LanguageEn
					? "The list of groups for which the reminder will work"
				: "Список групп на которые сработает напоминание")]
				public List<String> GrpupListAlerted = new List<String>();
				
				[JsonProperty(LanguageEn
					? "Remind players that they are running out of permission?"
				: "Напоминать игрокам о том, что у них заканчивается права?")]
				public Boolean UseAlertedPermission;
				[JsonProperty(LanguageEn
					? "The list of permissions for which the reminder will work"
				: "Список прав на которые сработает напоминание")]
				public List<String> PermissionsListAlerted = new List<String>();
				
				[JsonProperty(LanguageEn ? "How many days before the end of the privilege to remind the player about it" : "За сколько дней до окончания привилегии напоминать игроку об этом")]
				public Int32 DayAlerted;
				
				[JsonProperty(LanguageEn ? "How many seconds to show notifications to the player" : "Сколько секунд показывать уведомления игроку")]
				public Single TimeAlert;
				
				[JsonProperty(LanguageEn ? "Setting up IQChat (If installed)" : "Настройка IQChat (Если установлен)")]
				public IQChatSetting SettingIQChat = new IQChatSetting();
				
				internal class IQChatSetting
				{
					[JsonProperty(LanguageEn ? "IQChat : Custom prefix in chat" : "IQChat : Кастомный префикс в чате")]
					public String CustomPrefix;
					
					[JsonProperty(LanguageEn
						? "IQChat : Custom avatar in the chat (If required)"
					: "IQChat : Кастомный аватар в чате(Если требуется)")]
					public String CustomAvatar;
					
					[JsonProperty(LanguageEn
						? "IQChat : Use UI notifications"
					: "IQChat : Использовать UI уведомления")]
					public Boolean UIAlertUse;
				}
			}
			
			internal class GeneralSetting
			{
				
				internal class MySQLInitialize
				{
					[JsonProperty(LanguageEn ? "Setting up a MySQL connection" : "Настройка MySQL соединения")]
					public MySQLConnect ConnectSetting = new MySQLConnect();
					
					[JsonProperty(LanguageEn ? "The desired table name in MySQL" : "Желаемое название таблицы в MySQL")]
					public String TableName;
					
					[JsonProperty(LanguageEn
						? "Settings privilege migration between servers connected to the same MySQL (Example: Server #1 and #2 are connected to the same MySQL - a player with privileges on server #1 - going to server #2 - will get it there)"
					: "Настройка миграцию привилегий между среверами подключенными к одной MySQL (Пример : Сервер #1 и #2 подключены к одной MySQL - игрок с привилегий на сервере #1 - зайдя на сервер #2 - получит ее и там)")]
					public MigrationPrivilage MigrationSettings = new MigrationPrivilage();
					
					internal class MigrationPrivilage
					{
						[JsonProperty(LanguageEn ? "Use the list of permissions available for migration (otherwise they will be all that the player has in MySQL)" : "Использовать список прав доступных для миграции (иначе будут все, что есть у игрока в MySQL)")]
						public Boolean UseMigrationPermissionsList;
						[JsonProperty(LanguageEn ? "List of permissions in available for synchronization on another server in case of player migration" : "Список прав в доступных для синхронизации на другом сервере случае миграции игрока")]
						public List<String> MigrationPermissionList = new List<String>();
						
						[JsonProperty(LanguageEn ? "Use the list of groups available for migration (otherwise they will be all that the player has in MySQL)" : "Использовать список групп доступных для миграции (иначе будут все, что есть у игрока в MySQL)")]
						public Boolean UseMigrationGroupList;
						[JsonProperty(LanguageEn ? "List of groups in available for synchronization on another server in case of player migration" : "Список групп в доступных для синхронизации на другом сервере случае миграции игрока")]
						public List<String> MigrationGroupList = new List<String>();
					}
					internal class MySQLConnect
					{
						public String Host;
						public String Port;
						public String DatabaseName;
						public String UserName;
						public String Passowrd;
					}
				}
				
				[JsonProperty(LanguageEn
					? "Connecting data storage on the MySQL side"
				: "Подключение хранения данных на стороне MySQL")]
				public MySQLInitialize MySQLSetting = new MySQLInitialize();
				[JsonProperty(LanguageEn ? "Webhook from the Discord channel for logging" : "Webhook от канала Discord для логирования")]
				public String WebHooksDiscord;
				[JsonProperty(LanguageEn ? "Settings for automatically granting privileges to server novices (They will be considered novices - while the player is not in the database or datafile)" : "Настройки автоматической выдачи привилегии новичкам сервера (Они будут считаться новичками - пока игрока нет в базе или датафайле)")]
				public AutoGive AutoGiveSettings = new AutoGive();
				[JsonProperty(LanguageEn ? "Use the removal of temporary permission from the player when unloading the plugin (when loading, they will be returned)" : "Использовать удаление временных прав у игрока при выгрузке плагина (при загрузке - они будут возвращены)")]
				public Boolean UseRemovePermissionsХWhenUnload;
				
				[JsonProperty(LanguageEn ? "Use the removal of temporary groups from the player when unloading the plugin (when loading, they will be returned)" : "Использовать удаление временных групп у игрока при выгрузке плагина (при загрузке - они будут возвращены)")]
				public Boolean UseRemoveGroupsWhenUnload;
				
				internal class AutoGive
				{
					[JsonProperty(LanguageEn ? "List of groups to issue [group] = time in the format (1d/h/m/s)" : "Список групп для выдачи [группа] = время в формате (1d/h/m/s)")]
					public Dictionary<String, String> GroupList = new Dictionary<String, String>();
					[JsonProperty(LanguageEn ? "Use automatic granting of permissions to a new player" : "Использовать автоматическую выдачу прав новому игроку")]
					public Boolean UseAutGivePermissions;
					[JsonProperty(LanguageEn ? "Use automatic granting of groups to a new player" : "Использовать автоматическую выдачу групп новому игроку")]
					public Boolean UseAutGiveGroups;
					
					[JsonProperty(LanguageEn ? "List of permissions to issue [group] = time in the format (1d/h/m/s)" : "Список прав для выдачи [группа] = время в формате (1d/h/m/s)")]
					public Dictionary<String, String> PermissionList = new Dictionary<String, String>();
				}
			}
		}
		
		[ConsoleCommand("revokegroup")]
		private void RemoveGroup_ConsoleCommand(ConsoleSystem.Arg arg)
		{
			if (arg == null) return;
			
			if (arg.Player() != null)
			if (!arg.Player().IsAdmin)
			return;
			
			if (arg.Args == null || arg.Args.Length < 2)
			{
				arg.ReplyWith(LanguageEn ? "Syntax error, use: revokegroup Steam64ID Group\nExample : revokegroup 765611972356306930 vip" : "Ошибка синтаксиса, используйте : revokegroup Steam64ID Group\nПример : revokegroup 76561197235887930 vip");
				return;
			}
			
			UInt64 userID;
			if (!UInt64.TryParse(arg.Args[0], out userID))
			{
				arg.ReplyWith(LanguageEn ? "You have specified an incorrect Steam64ID player!" : "Вы указали неккоректный Steam64ID игрока!");
				return;
			}
			
			String Group = arg.Args[1];
			if (!config.GroupList.ContainsKey(Group))
			{
				arg.ReplyWith(LanguageEn ? "The group you specified do not exist!" : "Группа которую вы указали - не существуют!");
				return;
			}
			
			if (!permission.UserHasGroup(userID.ToString(), Group))
			{
				arg.ReplyWith(LanguageEn ? "The player does not have this group!" : "У игрока отсутствуют данная группа!");
				return;
			}
			
			if (arg.Args.Length == 3)
			{
				TimeSpan duration;
				if (!TryParseTimeSpan(arg.Args[2], out duration))
				{
					arg.ReplyWith(LanguageEn ? "You specified an incorrect time, use 1d/1h/1m/1s!" : "Вы указали неккоректное время, используйте 1d/1h/1m/1s!");
					return;
				}
				
				DateTime ThisTime = InformationPrivilagesUser.GetExpiredPermissionData(Group, userID, TypeData.Group) - duration;
				InformationPrivilagesUser.RemoveParametres(Group, userID, TypeData.Group, ThisTime);
				return;
			}
			
			InformationPrivilagesUser.RemoveParametres(Group, userID, TypeData.Group);
		}
		
		[ConsoleCommand("grantperm")]
		private void GrantPerm_ConsoleCommand(ConsoleSystem.Arg arg)
		{
			if (arg == null) return;
			if (arg.Player() != null)
			if (!arg.Player().IsAdmin)
			return;
			
			if (arg.Args == null || arg.Args.Length < 3)
			{
				arg.ReplyWith(LanguageEn ? "Syntax error, use: grantperm Steam64ID Permission Time(1d/1h/1m/1s)\nExample : grantperm 76561163069787930 iqchat.vip 1d" : "Ошибка синтаксиса, используйте : grantperm Steam64ID Permission Time(1d/1h/1m/1s)\nПример : grantperm 76561163069787930 iqchat.vip 1d");
				return;
			}
			
			UInt64 userID;
			if (!UInt64.TryParse(arg.Args[0], out userID))
			{
				arg.ReplyWith(LanguageEn ? "You have specified an incorrect Steam64ID player!" : "Вы указали неккоректный Steam64ID игрока!");
				return;
			}
			
			String Permission = arg.Args[1];
			if (!config.PermissionList.ContainsKey(Permission))
			{
				arg.ReplyWith(LanguageEn ? "The permission you specified do not exist!" : "Права которые вы указали - не существуют!");
				return;
			}
			
			TimeSpan duration;
			if (!TryParseTimeSpan(arg.Args[2], out duration))
			{
				arg.ReplyWith(LanguageEn ? "You specified an incorrect time, use 1d/1h/1m/1s!" : "Вы указали неккоректное время, используйте 1d/1h/1m/1s!");
				return;
			}
			
			DateTime ThisTime = InformationPrivilagesUser.GetExpiredPermissionData(Permission, userID, TypeData.Permission) + duration;
			InformationPrivilagesUser.SetParametres(Permission, userID, ThisTime, TypeData.Permission);
		}
		
		private String GetDescription(String UserID, TypeAlert typeAlert, String ReplaceKey, String DataExpired)
		{
			String Lang = typeAlert == TypeAlert.Add ? GetLang("DESCRIPTION_ALERT_ADD", UserID, ReplaceKey) :
			typeAlert == TypeAlert.Expired ? GetLang("DESCRIPTION_ALERT_EXPIRED", UserID, ReplaceKey) :
			typeAlert == TypeAlert.Alert ? GetLang("DESCRIPTION_ALERT_INFO", UserID, ReplaceKey, DataExpired) : String.Empty;
			
			return Lang;
		}
		
		
		
		private void GrablingGroups()
		{
			String[] GroupList = permission.GetGroups();
			
			Int32 CountGroupsGrabs = 0;
			foreach (String group in GroupList.Where(p => !config.GroupList.ContainsKey(p)))
			{
				String ExampleName = group.ToUpper();
				
				config.GroupList.Add(group, new Configuration.StructureLanguage(ExampleName, ExampleName));
				CountGroupsGrabs++;
			}
			
			if (CountGroupsGrabs == 0) return;
		}
		
		private void SetPermission(UInt64 userID, String Permission, String DataExpired)
		{
			TimeSpan duration;
			if (!TryParseTimeSpan(DataExpired, out duration))
			{
				PrintError(LanguageEn ? "API : You specified an incorrect time, use 1d/1h/1m/1s!" : "API : Вы указали неккоректное время, используйте 1d/1h/1m/1s!");
				return;
			}
			
			DateTime ThisTime = InformationPrivilagesUser.GetExpiredPermissionData(Permission, userID, TypeData.Permission) + duration;
			InformationPrivilagesUser.SetParametres(Permission, userID, ThisTime, TypeData.Permission);
		}
		
		
		private IEnumerator MigrationTimedPermissions()
		{
			if (!Interface.Oxide.DataFileSystem.ExistsDatafile("TimedPermissions"))
			{
				yield break;
			}
			List<PlayerInformation> dataFile = Interface.Oxide.DataFileSystem.ReadObject<List<PlayerInformation>>("TimedPermissions");
			
			foreach (PlayerInformation playerInformation in dataFile)
			{
				UInt64 userID;
				if (!UInt64.TryParse(playerInformation.Id, out userID))
				continue;
				
				foreach (ExpiringAccessValue informationPermission in playerInformation._permissions.Where(x =>
				!x.IsExpired))
				{
					InformationPrivilagesUser.SetParametres(informationPermission.Value, userID,
					informationPermission.ExpireDate, TypeData.Permission);
					
					yield return CoroutineEx.waitForSeconds(0.15f);
				}
				
				foreach (ExpiringAccessValue playerInformationGroup in playerInformation._groups.Where(x =>
				!x.IsExpired))
				{
					InformationPrivilagesUser.SetParametres(playerInformationGroup.Value, userID, playerInformationGroup.ExpireDate, TypeData.Group);
					
					yield return CoroutineEx.waitForSeconds(0.15f);
				}
				
				yield return CoroutineEx.waitForSeconds(0.25f);
			}
			
			Interface.Oxide.UnloadPlugin("TimedPermissions");
			
			if (coroutineMigration != null)
			{
				ServerMgr.Instance.StopCoroutine(coroutineMigration);
				coroutineMigration = null;
			}
		}
		
		
		
		
		[ChatCommand("pinfo")]
		private void ChatCommand_Pinfo(BasePlayer player)
		{
			String ResultInfo = String.Empty;
			Dictionary<String, DateTime> Group =
			InformationPrivilagesUser.GetParametresUser(player.userID, TypeData.Group);
			Dictionary<String, DateTime> Permission =
			InformationPrivilagesUser.GetParametresUser(player.userID, TypeData.Permission);
			
			if (Group.Count == 0 && Permission.Count == 0)
			{
				SendChat(GetLang("PINFO_ALERT_NOT", player.UserIDString), player);
				return;
			}
			
			ResultInfo = Group.Aggregate(ResultInfo, (current, group) => current + $"- {GetLanguageNamePrivilage(player, group.Key, TypeData.Group)} : {_.GetLeftTime(group.Value, player.UserIDString)}\n");
			if (!String.IsNullOrWhiteSpace(ResultInfo))
			ResultInfo += "\n\n";
			ResultInfo = Permission.Aggregate(ResultInfo, (current, permissions) => current + $"- {GetLanguageNamePrivilage(player, permissions.Key, TypeData.Permission)} : {_.GetLeftTime(permissions.Value, player.UserIDString)}\n");
			
			if (IQChat)
			IQChat?.Call("API_ALERT_PLAYER", player, GetLang("PINFO_ALERT", player.UserIDString, ResultInfo), config.AlertConfigurations.SettingIQChat.CustomPrefix, config.AlertConfigurations.SettingIQChat.CustomAvatar);
			else
			player.SendConsoleCommand("chat.add", Chat.ChatChannel.Global, 0,
			GetLang("PINFO_ALERT", player.UserIDString, ResultInfo), player);
		}
		
		string PermApi(BasePlayer player)
		{
			Dictionary<String, DateTime> Group = InformationPrivilagesUser.GetParametresUser(player.userID, TypeData.Group);
			Dictionary<String, DateTime> Permission = InformationPrivilagesUser.GetParametresUser(player.userID, TypeData.Permission);
			if (Group.Count() != 0)
			return GetLanguageNamePrivilage(player, Group.ElementAt(0).Key, TypeData.Group);
			if (Permission.Count() != 0)
			return GetLanguageNamePrivilage(player, Permission.ElementAt(0).Key, TypeData.Permission);
			return "НЕТУ";
		}
		
		class api
		{
			public string Name;
			public string Time;
			public string Image;
		}
		
		string PermUIApi(BasePlayer player)
		{
			List<api> Priv = new List<api>();
			Dictionary<String, DateTime> Group = InformationPrivilagesUser.GetParametresUser(player.userID, TypeData.Group);
			Dictionary<String, DateTime> Permission = InformationPrivilagesUser.GetParametresUser(player.userID, TypeData.Permission);
			
			if (Group.Count() == 0 && Permission.Count() == 0)
			{
				return null;
			}
			
			if (Group.Count() != 0)
			{
				foreach (var check in Group)
				Priv.Add(new api { Name = GetLanguageNamePrivilage(player, check.Key, TypeData.Group), Time = _.GetLeftTime(check.Value, player.UserIDString), Image = GetImagePrivilage(player, check.Key, TypeData.Group) });
			}
			if (Permission.Count() != 0)
			{
				foreach (var check in Permission)
				Priv.Add(new api { Name = GetLanguageNamePrivilage(player, check.Key, TypeData.Permission), Time = _.GetLeftTime(check.Value, player.UserIDString), Image = GetImagePrivilage(player, check.Key, TypeData.Permission) });
			}
			
			return JsonConvert.SerializeObject(Priv);
		}
		
		
		private class PlayerInformation
		{
			[JsonProperty("Id")]
			public string Id { get; set; }
			
			[JsonProperty("Name")]
			public string Name { get; set; }
			
			[JsonProperty("Permissions")]
			public List<ExpiringAccessValue> _permissions = new List<ExpiringAccessValue>();
			
			[JsonProperty("Groups")]
			public List<ExpiringAccessValue> _groups = new List<ExpiringAccessValue>();
		}
		public enum TypeData
		{
			Permission,
			Group
		}
		protected override void SaveConfig() => Config.WriteObject(config, true);
		private readonly Core.MySql.Libraries.MySql sqlLibrary = Interface.Oxide.GetLibrary<Core.MySql.Libraries.MySql>();
		private Dictionary<String, DateTime> GetGroups(UInt64 userID) => InformationPrivilagesUser.GetParametresUser(userID, TypeData.Group);
	}
}
