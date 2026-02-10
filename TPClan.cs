using Rust;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.IO;


namespace Oxide.Plugins
{
    [Info("TPClan", "pluginfuel.ru", "20.0.2")]
    public class TPClan : RustPlugin
    {
        #region [Vars]
        [PluginReference] private Plugin ImageLibrary = null, TPTournament = null, TPCaptureZone = null, QuarryCapture = null, TPRTCapture = null;

        private const String Layer = "TPClan.Layer";
        private const Boolean LanguageEn = false;

        private readonly Regex Regex = new Regex(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>");
        private readonly String[] _gatherHooks = { "OnDispenserGather", "OnDispenserBonus", "OnCollectiblePickup" };
        private readonly String[] _gatherItem = { "wood", "stones", "metal.ore", "sulfur.ore", "hq.metal.ore", "cloth", "leather", "fat.animal", "loot-barrel", "metal.refined", "metal.fragments", "sulfur", "charcoal" };
        private readonly String[] _wearItem = { "hoodie", "pants", "shoes.boots", "metal.facemask", "metal.plate.torso", "roadsign.kilt" };
        private readonly Dictionary<UInt64, BasePlayer> _lastHeli = new Dictionary<UInt64, BasePlayer>();
        private readonly Dictionary<String, Int32> _itemIds = new Dictionary<String, Int32>();

        private List<BuildingPrivlidge> _cupboardList = new List<BuildingPrivlidge>();
        private List<CodeLock> _codeLockList = new List<CodeLock>();
        private List<SamSite> _sameSiteList = new List<SamSite>();
        private Dictionary<UInt64, ClanData> _playerToClan = new Dictionary<UInt64, ClanData>();
        private List<UInt64> _lootEntity = new List<UInt64>();
        private Dictionary<UInt64, DateTime> _cooldownPlayer = new Dictionary<UInt64, DateTime>();

        private static TPClan _;
        private static StringBuilder sb;
        #endregion

        #region [ImageLibrary]
        private Boolean HasImage(String imageName, UInt64 imageId = 0) => ImageLibrary?.Call<Boolean>("HasImage", imageName, imageId) ?? false;
        private Boolean AddImage(String url, String shortname, UInt64 skin = 0) => ImageLibrary?.Call<Boolean>("AddImage", url, shortname, skin) ?? false;
        private String GetImage(String shortname, UInt64 skin = 0) => ImageLibrary?.Call<String>("GetImage", shortname, skin);
        #endregion

        #region [Clan-Data]
        private List<ClanData> _clansList = new List<ClanData>();

        private void LoadClans()
        {
            try
            {
                _clansList = Interface.Oxide.DataFileSystem.ReadObject<List<ClanData>>($"{Name}/ClansList");

                if (_clansList != null)
                {
                    foreach (var clan in _clansList)
                    {
                        if (clan.Task == null)
                        {
                            clan.Task = String.Empty;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_clansList == null) _clansList = new List<ClanData>();
        }

        private void SaveClans()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/ClansList", _clansList);
        }

        private class ClanData
        {
            public String ClanTag;

            public String Avatar;

            public UInt64 LeaderID;

            public String LeaderName;

            [JsonProperty("Task")]
            public String Task;

            public UInt64 TeamID;

            public Dictionary<String, GatherSettings> GatherClan = new Dictionary<String, GatherSettings>()
            {
                ["wood"] = new GatherSettings(),
                ["stones"] = new GatherSettings(),
                ["metal.ore"] = new GatherSettings(),
                ["sulfur.ore"] = new GatherSettings(),
                ["hq.metal.ore"] = new GatherSettings(),
                ["cloth"] = new GatherSettings(),
                ["leather"] = new GatherSettings(),
                ["fat.animal"] = new GatherSettings(),
                ["loot-barrel"] = new GatherSettings()
            };

            public Dictionary<String, UInt64> SkinList = new Dictionary<String, UInt64>()
            {
                ["hoodie"] = 0,
                ["pants"] = 0,
                ["shoes.boots"] = 0,
                ["metal.facemask"] = 0,
                ["metal.plate.torso"] = 0,
                ["roadsign.kilt"] = 0,
            };

            public Dictionary<UInt64, PlayerData> Members = new Dictionary<UInt64, PlayerData>();

            public List<UInt64> Moderators = new List<UInt64>();

            [JsonIgnore]
            private RelationshipManager.PlayerTeam Team =>
                RelationshipManager.ServerInstance.FindTeam(TeamID) ?? FindOrCreateTeam();

            public class PlayerData
            {
                public Int32 Point = 0;

                public Int32 Kill = 0;

                public Int32 Death = 0;

                public Boolean FriendlyFire;

                public Int32 Online;

                public Dictionary<String, Int32> GatherMember = new Dictionary<String, Int32>()
                {
                    ["wood"] = 0,
                    ["stones"] = 0,
                    ["metal.ore"] = 0,
                    ["sulfur.ore"] = 0,
                    ["hq.metal.ore"] = 0,
                    ["cloth"] = 0,
                    ["leather"] = 0,
                    ["fat.animal"] = 0,
                    ["loot-barrel"] = 0
                };
            }

            public class GatherSettings
            {
                public Int32 Farm;

                public Int32 Need;
            }

            public Boolean IsOwner(String userId) => IsOwner(UInt64.Parse(userId));

            public Boolean IsOwner(UInt64 userId) => LeaderID == userId;

            public Boolean IsModerator(String userId) => IsModerator(UInt64.Parse(userId));

            public Boolean IsModerator(UInt64 userId) => Moderators.Contains(userId) || IsOwner(userId);

            public Boolean IsMember(String userId) => IsMember(UInt64.Parse(userId));

            public Boolean IsMember(UInt64 userId) => Members.ContainsKey(userId);

            public RelationshipManager.PlayerTeam FindOrCreateTeam()
            {
                RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindTeam(TeamID) ??
                            RelationshipManager.ServerInstance.FindPlayersTeam(LeaderID);
                if (team != null)
                {
                    if (team.teamLeader == LeaderID)
                    {
                        TeamID = team.teamID;
                        return team;
                    }

                    team.RemovePlayer(LeaderID);
                }

                return CreateTeam();
            }

            public RelationshipManager.PlayerTeam CreateTeam()
            {
                RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.CreateTeam();
                team.teamLeader = LeaderID;
                AddPlayer(LeaderID, team);

                TeamID = team.teamID;

                return team;
            }

            public RelationshipManager.PlayerTeam FindTeam()
            {
                RelationshipManager.PlayerTeam leaderTeam = RelationshipManager.ServerInstance.FindPlayersTeam(LeaderID);
                if (leaderTeam != null)
                {
                    TeamID = leaderTeam.teamID;
                    return leaderTeam;
                }

                return null;
            }

            public void SetTeam(UInt64 teamID) => TeamID = teamID;

            public void AddPlayer(UInt64 member, RelationshipManager.PlayerTeam team = null)
            {
                if (team == null) team = Team;

                if (!team.members.Contains(member)) team.members.Add(member);

                RelationshipManager.ServerInstance.playerToTeam[member] = team;

                BasePlayer player = RelationshipManager.FindByID(member);
                if (player != null)
                {
                    if (player.Team != null && player.Team.teamID != team.teamID)
                    {
                        player.Team.RemovePlayer(player.userID);
                        player.ClearTeam();
                    }

                    player.currentTeam = team.teamID;
                    team.MarkDirty();
                    player.SendNetworkUpdate();
                }
            }

            public static ClanData CreateNewClan(String clanTag, BasePlayer player)
            {
                ClanData clan = new ClanData
                {
                    ClanTag = clanTag,
                    Avatar = $"{_.Name}.Avatar{player.UserIDString}",
                    LeaderID = player.userID,
                    LeaderName = player.displayName,
                    Task = String.Empty
                };

                clan.Members.Add(player.userID, new PlayerData());

                _._clansList.Add(clan);

                if (_config.TagInName) player.displayName = $"[{clanTag}] {player.IPlayer.Name}";

                if (_config.AutoTeamCreation) clan.FindOrCreateTeam();

                _._playerToClan[player.userID] = clan;
                ClanCreate(clanTag);

                return clan;
            }

            public void Disband()
            {
                List<String> memberUserIDs = Facepunch.Pool.GetList<string>();
                memberUserIDs.AddRange(Members.Keys.Select(x => x.ToString()));

                ClanDisbanded(memberUserIDs);
                ClanDisbanded(ClanTag, memberUserIDs);

                Facepunch.Pool.FreeList(ref memberUserIDs);

                Members.Keys.ToList().ForEach(member => Kick(member, true));

                foreach (KeyValuePair<String, UInt64> skin in SkinList)
                {
                    if (skin.Value == 0) continue;

                    if (_._skinsUsedList[skin.Key].Contains(skin.Value))
                        _._skinsUsedList[skin.Key].Remove(skin.Value);
                }

                ClanDestroy(ClanTag);

                if (_config.AutoTeamCreation)
                {
                    Team?.members.ToList().ForEach(member =>
                    {
                        Team.RemovePlayer(member);

                        BasePlayer player = RelationshipManager.FindByID(member);
                        if (player != null) player.ClearTeam();
                    });
                }

                _._clansList.Remove(this);
            }

            public void Join(BasePlayer player)
            {
                Members.Add(player.userID, new PlayerData());

                if (_config.TagInName) player.displayName = $"[{ClanTag}] {player.displayName}";

                if (_config.AutoTeamCreation)
                {
                    if (player.Team != Team) player.Team?.RemovePlayer(player.userID);

                    Team?.AddPlayer(player);
                }

                foreach (UInt64 member in Members.Keys.ToList())
                {
                    AuthBuilding(member, player.userID);
                    AuthBuilding(player.userID, member);

                    AuthCodeLock(member, player.userID);
                    AuthCodeLock(player.userID, member);
                }

                if (_config.ChangeOwnerIDForLeader) ChangeOwnerIDForLeader(player.userID, LeaderID);

                _._playerToClan[player.userID] = this;
                ClanMemberJoined(player.UserIDString, ClanTag);
            }

            public void Kick(UInt64 target, Boolean disband = false)
            {
                String targetStringId = target.ToString();

                Members.Remove(target);
                Moderators.Remove(target);

                _._playerToClan.Remove(target);

                if (_config.TagInName)
                {
                    String name = _.covalence.Players.FindPlayerById(targetStringId)?.Name;
                    if (!String.IsNullOrWhiteSpace(name))
                    {
                        BasePlayer player = RelationshipManager.FindByID(target);
                        if (player != null) player.displayName = name;
                    }
                }

                if (!disband)
                {
                    if (_config.AutoTeamCreation && Team != null) Team.RemovePlayer(target);

                    if (Members.Count == 0)
                    {
                        Disband();
                    }
                    else
                    {
                        if (LeaderID == target) SetLeader((Moderators.Count > 0 ? Moderators : Members.Keys.ToList()).GetRandom());
                    }
                }

                foreach (UInt64 member in Members.Keys)
                {
                    DeAuthBuilding(member, target);
                    DeAuthBuilding(target, member);

                    DeAuthCodeLock(member, target);
                    DeAuthCodeLock(target, member);
                }

                ClanMemberGone(targetStringId, ClanTag);
            }

            public void SetLeader(UInt64 target)
            {
                if (!Members.ContainsKey(target)) return;

                if (Moderators.Contains(target)) Moderators.Remove(target);

                LeaderName = _.covalence.Players.FindPlayerById(target.ToString())?.Name;

                if (_config.ChangeOwnerIDForLeader) ChangeOwnerIDForLeader(LeaderID, target);

                LeaderID = target;

                Avatar = $"{_.Name}.Avatar{target}";

                if (_config.AutoTeamCreation) Team.SetTeamLeader(target);
            }

            public Int32 GetScore() => Members.Sum(member => member.Value.Point);

            public void UpdateLeaderName(String name) => LeaderName = name;

            public Int32 Online() => Members.Keys.Count(p => BasePlayer.Find(p.ToString()) != null);

            public String GetPercentClan()
            {
                Int32 need = GatherClan.Sum(x => x.Value.Need);
                Int32 current = GatherClan.Sum(x => x.Value.Farm);

                return $"{(Single)Math.Round(((Single)current * 100) / need, 2)}%";
            }

            public String GetPercentPlayer(UInt64 playerID)
            {
                Int32 need = GatherClan.Sum(x => x.Value.Need);
                Int32 current = Members[playerID].GatherMember.Sum(p => p.Value);

                return $"{(Single)Math.Round(((Single)current * 100) / need, 2)}%";
            }
        }
        #endregion

        #region [Skin-Data]
        private Dictionary<String, List<UInt64>> _skinsList = new Dictionary<String, List<UInt64>>();

        private void FillingSkinList()
        {
            Boolean needSave = false;

            foreach (String item in _wearItem)
            {
                if (_skinsList.ContainsKey(item) && _skinsList[item] != null && _skinsList[item].Count > 0) 
                    continue;

                List<UInt64> skins = ImageLibrary?.Call<List<UInt64>>("GetImageList", item) ?? new List<UInt64>();
                
                // Если скины не загружены из ImageLibrary, пытаемся загрузить из сохранённых данных
                if (skins.Count == 0 && _skinsList.ContainsKey(item))
                {
                    skins = _skinsList[item];
                }
                
                _skinsList[item] = skins;
                
                if (!_skinsUsedList.ContainsKey(item))
                {
                    _skinsUsedList[item] = new List<UInt64>();
                }

                needSave = true;
            }

            if (needSave)
            {
                SaveSkins();
                SaveUsedSkins();
            }
        }

        private void LoadSkins()
        {
            try
            {
                _skinsList = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<String, List<UInt64>>>($"{Name}/ClansSkin");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_skinsList == null) _skinsList = new Dictionary<String, List<UInt64>>();
        }

        private void SaveSkins() => Interface.Oxide.DataFileSystem.WriteObject($"{Name}/ClansSkin", _skinsList);
        #endregion

        #region [SkinUseed-Data]
        private Dictionary<String, List<UInt64>> _skinsUsedList = new Dictionary<String, List<UInt64>>();

        private void LoadUsedSkins()
        {
            try
            {
                _skinsUsedList = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<String, List<UInt64>>>($"{Name}/ClansSkinUsed");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_skinsUsedList == null) _skinsUsedList = new Dictionary<String, List<UInt64>>();
        }

        private void SaveUsedSkins() => Interface.Oxide.DataFileSystem.WriteObject($"{Name}/ClansSkinUsed", _skinsUsedList);
        #endregion

        #region [Oxide-Api]
        private void OnPluginLoaded(Plugin plugin)
        {
            // Если загружена ImageLibrary, перезагружаем скины
            if (plugin?.Name == "ImageLibrary")
            {
                ImageLibrary = plugin;
                FillingSkinList();
            }
            
            NextTick(() =>
            {
                foreach (String hook in _gatherHooks)
                {
                    Unsubscribe(hook);
                    Subscribe(hook);
                }
            });
        }

        private void OnServerSave() => SaveClans();

        private void Init()
        {
            _ = this;

            sb = new StringBuilder();

            LoadClans();

            LoadSkins();

            LoadUsedSkins();

            if (!_config.BlockCreateTeam) Unsubscribe(nameof(OnTeamCreate));

            AddCovalenceCommand(_config.ClanCommands.ToArray(), nameof(CmdClans));

            AddCovalenceCommand("aclan", nameof(AdminCmdClans));
        }

        private void OnServerInitialized()
        {
            FillingSkinList();

            _imageUI = new ImageUI();
            _imageUI.DownloadImage();

            FillingTeams();

            AddImage("https://i.postimg.cc/MpcXW1gf/cEBay4m.png", "loot-barrel");

            _cupboardList = BaseNetworkable.serverEntities.OfType<BuildingPrivlidge>().ToList();
            _codeLockList = BaseNetworkable.serverEntities.OfType<CodeLock>().ToList();
            if (_config.ChangeOwnerIDForLeader) _sameSiteList = BaseNetworkable.serverEntities.OfType<SamSite>().ToList();

            foreach (ClanData clan in _clansList)
            {
                foreach (UInt64 playerID in clan.Members.Keys)
                    _playerToClan[playerID] = clan;
            }

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);

            timer.Every(1f, TimeHandle);
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer + ".Main");

                if (_config.TagInName)
                {
                    String Name = player.IPlayer.Name;

                    player.displayName = Name;
                }
            }

            if (_imageUI != null)
            {
                _imageUI.UnloadImages();
                _imageUI = null;
            }

            SaveClans();

            SaveSkins();

            SaveUsedSkins();

            _ = null;
            _config = null;
            sb = null;
        }

        private void OnNewSave(String filename)
        {
            try
            {
                if (_clansList == null || _clansList.Count == 0)
                    LoadClans();

                if (_config.GameStoreSettings.Enable) RewardClans();

                _clansList.Clear();

                SaveClans();
            }
            catch (Exception e)
            {
                PrintError($"[OnNewSave] : {e.Message}");
            }
        }
        #endregion

        #region [Rust-Api]
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;

            GetAvatar(player.UserIDString, avatar => AddImage(avatar, $"{Name}.Avatar{player.UserIDString}"));

            ClanData clan = FindClanByUser(player.userID);
            if (clan == null) return;

            if (_config.TagInName) player.displayName = $"[{clan.ClanTag}] {player.IPlayer.Name}";

            if (_config.AutoTeamCreation) clan.AddPlayer(player.userID);

            if (clan.IsOwner(player.userID)) clan.UpdateLeaderName(player.IPlayer.Name);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;

            ClanData clan = FindClanByUser(player.userID);
            if (clan == null) return;

            if (_config.TagInName) player.displayName = $"{player.IPlayer.Name}";
        }

        private void OnEntitySpawned(BuildingPrivlidge buildingPrivlidge)
        {
            if (buildingPrivlidge == null || !buildingPrivlidge.OwnerID.IsSteamId()) return;

            _cupboardList.Add(buildingPrivlidge);

            ClanData clan = FindClanByUser(buildingPrivlidge.OwnerID);
            if (clan == null) return;

            if (_config.ChangeOwnerIDForLeader) buildingPrivlidge.OwnerID = clan.LeaderID;
            foreach (UInt64 member in clan.Members.Keys)
            {
                buildingPrivlidge.authorizedPlayers.RemoveWhere((ulong x) => x == member);
                buildingPrivlidge.authorizedPlayers.Add(member);
            }
            buildingPrivlidge.UpdateMaxAuthCapacity();
            buildingPrivlidge.SendNetworkUpdate();
        }

        private void OnEntitySpawned(CodeLock codeLock)
        {
            if (codeLock == null || !codeLock.OwnerID.IsSteamId()) return;

            _codeLockList.Add(codeLock);

            ClanData clan = FindClanByUser(codeLock.OwnerID);
            if (clan == null) return;

            if (_config.ChangeOwnerIDForLeader) codeLock.OwnerID = clan.LeaderID;
            codeLock.code = $"{UnityEngine.Random.Range(0, 1000)}";
            Effect.server.Run(codeLock.effectLocked.resourcePath, codeLock, 0, Vector3.zero, Vector3.forward, null, false);
            codeLock.SetFlag(BaseEntity.Flags.Locked, true);

            foreach (UInt64 member in clan.Members.Keys)
            {
                if (!codeLock.whitelistPlayers.Contains(member))
                    codeLock.whitelistPlayers.Add(member);
            }
            codeLock.SendNetworkUpdate();
        }

        private void OnEntitySpawned(SamSite samSite)
        {
            if (!_config.ChangeOwnerIDForLeader || samSite == null || !samSite.OwnerID.IsSteamId()) return;

            _sameSiteList.Add(samSite);

            ClanData clan = FindClanByUser(samSite.OwnerID);
            if (clan == null) return;

            samSite.OwnerID = clan.LeaderID;
            samSite.SendNetworkUpdate();
        }

        private void OnEntitySpawned(AutoTurret autoTurret)
        {
            if (!_config.ChangeOwnerIDForLeader || autoTurret == null || !autoTurret.OwnerID.IsSteamId()) return;

            ClanData clan = FindClanByUser(autoTurret.OwnerID);
            if (clan == null) return;

            autoTurret.OwnerID = clan.LeaderID;
            autoTurret.SendNetworkUpdate();
        }

        private object CanUseLockedEntity(BasePlayer player, KeyLock keyLock)
        {
            if (player == null || keyLock == null || !keyLock.IsLocked()) return null;

            BaseEntity parentEntity = keyLock.GetParentEntity();

            UInt64 ownerID = keyLock.OwnerID.IsSteamId() ? keyLock.OwnerID : parentEntity != null ? parentEntity.OwnerID : 0;
            if (!ownerID.IsSteamId() || ownerID == player.userID) return null;

            if (IsTeammates(ownerID, player.userID)) return true;

            return null;
        }

        private void OnEntityKill(BuildingPrivlidge buildingPrivlidge)
        {
            if (buildingPrivlidge == null || !buildingPrivlidge.OwnerID.IsSteamId()) return;
            if (_cupboardList.Contains(buildingPrivlidge)) _cupboardList.Remove(buildingPrivlidge);
        }

        private void OnEntityKill(CodeLock codeLock)
        {
            if (codeLock == null || !codeLock.OwnerID.IsSteamId()) return;
            if (_codeLockList.Contains(codeLock)) _codeLockList.Remove(codeLock);
        }

        private void OnEntityKill(SamSite samSite)
        {
            if (!_config.ChangeOwnerIDForLeader || samSite == null || !samSite.OwnerID.IsSteamId()) return;
            if (_sameSiteList.Contains(samSite)) _sameSiteList.Remove(samSite);
        }

        private object OnTurretTarget(AutoTurret turret, BasePlayer target)
        {
            if (target == null || turret == null || target.limitNetworking || (turret is NPCAutoTurret && !target.userID.IsSteamId()) || target.userID == turret.OwnerID) return null;

            ClanData clan = FindClanByUser(turret.OwnerID);
            if (clan == null) return null;

            if (clan.IsMember(target.userID))
                return false;

            return null;
        }

        private object OnSamSiteTarget(SamSite samSite, BaseVehicle vehicle)
        {
            if (samSite == null || !samSite.OwnerID.IsSteamId() || vehicle == null) return null;

            ClanData clan = FindClanByUser(samSite.OwnerID);
            if (clan == null) return null;

            if (vehicle.OwnerID == samSite.OwnerID || clan.IsMember(vehicle.OwnerID))
                return true;

            foreach (var mounted in vehicle.allMountPoints
                         .Where(allMountPoint => allMountPoint != null && allMountPoint.mountable != null)
                         .Select(allMountPoint => allMountPoint.mountable.GetMounted())
                         .Where(mounted => mounted != null))
            {
                if (mounted.userID == samSite.OwnerID || clan.IsMember(mounted.userID))
                    return true;
            }

            return null;
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null || item == null || !_gatherItem.Contains(item.info.shortname)) return;

            ClanData clan = FindClanByUser(player.userID);
            if (clan == null) return;

            String ShortName = item.info.shortname;

            if (ShortName.Contains("metal.fragments"))
                ShortName = "metal.ore";

            if (ShortName.Contains("sulfur"))
                ShortName = "sulfur.ore";

            if (ShortName.Contains("metal.refined"))
                ShortName = "hq.metal.ore";

            if (ShortName.Contains("charcoal"))
                ShortName = "wood";

            clan.GatherClan[ShortName].Farm += item.amount;
            clan.Members[player.userID].GatherMember[ShortName] += item.amount;
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null || item == null || !_gatherItem.Contains(item.info.shortname)) return;

            ClanData clan = FindClanByUser(player.userID);
            if (clan == null) return;

            String ShortName = item.info.shortname;

            if (ShortName.Contains("metal.fragments"))
                ShortName = "metal.ore";

            if (ShortName.Contains("sulfur"))
                ShortName = "sulfur.ore";

            if (ShortName.Contains("metal.refined"))
                ShortName = "hq.metal.ore";

            if (ShortName.Contains("charcoal"))
                ShortName = "wood";

            clan.GatherClan[ShortName].Farm += item.amount;
            clan.Members[player.userID].GatherMember[ShortName] += item.amount;
            clan.Members[player.userID].Point += GetPointConfig(ShortName);
        }

        private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (player == null || collectible == null || collectible.itemList == null) return;

            ClanData clan = FindClanByUser(player.userID);
            if (clan == null) return;

            foreach (var itemAmount in collectible.itemList)
            {
                if (itemAmount.itemDef != null)
                {
                    if (!_gatherItem.Contains(itemAmount.itemDef.shortname)) continue;

                    String ShortName = itemAmount.itemDef.shortname;

                    if (ShortName.Contains("metal.fragments"))
                        ShortName = "metal.ore";

                    if (ShortName.Contains("sulfur"))
                        ShortName = "sulfur.ore";

                    if (ShortName.Contains("metal.refined"))
                        ShortName = "hq.metal.ore";

                    if (ShortName.Contains("charcoal"))
                        ShortName = "wood";

                    clan.GatherClan[ShortName].Farm += (Int32)itemAmount.amount;
                    clan.Members[player.userID].GatherMember[ShortName] += (Int32)itemAmount.amount;
                }
            }
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container == null || item == null || !_wearItem.Contains(item.info.shortname)) return;

            BasePlayer player = container.GetOwnerPlayer();
            if (player == null || !player.userID.IsSteamId()) return;

            ClanData clan = FindClanByUser(player.userID);
            if (clan == null) return;

            if (clan.SkinList.ContainsKey(item.info.shortname))
            {
                UInt64 skin = clan.SkinList[item.info.shortname];
                if (skin != 0)
                {
                    if (item.info.category == ItemCategory.Attire)
                    {
                        if (container == player.inventory.containerWear) ApplySkinToItem(item, skin);
                    }
                    else
                    {
                        ApplySkinToItem(item, skin);
                    }
                }
            }
        }

        private void OnLootEntity(BasePlayer player, LootContainer entity)
        {
            if (player == null || entity == null || entity.OwnerID.IsSteamId() || entity.net == null || _lootEntity.Contains(entity.net.ID.Value)) return;

            ClanData clan = FindClanByUser(player.userID);
            if (clan == null) return;

            clan.GatherClan["loot-barrel"].Farm++;
            clan.Members[player.userID].GatherMember["loot-barrel"]++;
            clan.Members[player.userID].Point += GetPointConfig("loot-barrel");

            _lootEntity.Add(entity.net.ID.Value);
        }

        private void OnEntityTakeDamage(PatrolHelicopter helicopter, HitInfo info)
        {
            if (helicopter == null || helicopter.net == null || info == null || info.InitiatorPlayer == null) return;

            _lastHeli[helicopter.net.ID.Value] = info.InitiatorPlayer;
        }

        private void OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (player == null || !player.userID.IsSteamId() || info == null) return;

            BasePlayer initiatorPlayer = info.InitiatorPlayer;
            if (initiatorPlayer == null || !initiatorPlayer.userID.IsSteamId() || player.userID == initiatorPlayer.userID) return;

            ClanData clan = FindClanByUser(initiatorPlayer.userID);
            if (clan == null) return;

            Boolean friendlyFire = clan.Members[initiatorPlayer.userID].FriendlyFire;
            if (!friendlyFire && clan.IsMember(player.userID))
            {
                if (initiatorPlayer.SecondsSinceAttacked > 5)
                {
                    initiatorPlayer.ChatMessage(GetLang("DAMAGE_FF_OFF", player.UserIDString));
                    initiatorPlayer.lastAttackedTime = UnityEngine.Time.time;
                }
                info.damageTypes.ScaleAll(0);
                return;
            }
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null || (player.ShortPrefabName == "player" && !player.userID.IsSteamId())) return;

            if (info.damageTypes.Has(DamageType.Suicide))
            {
                ClanData clan = FindClanByUser(player.userID);
                if (clan == null) return;

                clan.Members[player.userID].Death++;
                clan.Members[player.userID].Point -= GetPointConfig("Suicide");
                return;
            }

            BasePlayer attacker = info.InitiatorPlayer;
            if (attacker == null || !attacker.userID.IsSteamId() || IsTeammates(player.userID, attacker.userID)) return;

            if (player.userID.IsSteamId())
            {
                ClanData clan = FindClanByUser(player.userID);
                if (clan != null)
                {
                    clan.Members[player.userID].Death++;
                    clan.Members[player.userID].Point -= GetPointConfig("Death");
                }

                ClanData clanAttacker = FindClanByUser(attacker.userID);
                if (clanAttacker != null)
                {
                    clanAttacker.Members[attacker.userID].Kill++;
                    clanAttacker.Members[attacker.userID].Point += GetPointConfig("Kill");
                }
            }
        }

        private void OnEntityDeath(PatrolHelicopter entity, HitInfo info)
        {
            if (entity == null || entity.net == null || info == null) return;

            BasePlayer player;

            if (_lastHeli.TryGetValue(entity.net.ID.Value, out player) && player != null)
            {
                ClanData clan = FindClanByUser(player.userID);
                if (clan == null) return;

                clan.Members[player.userID].Point += GetPointConfig("PatrolHelicopter");

                foreach (BasePlayer onlinePlayer in BasePlayer.activePlayerList) onlinePlayer.ChatMessage(GetLang("PATROLHELICOPTER", onlinePlayer.UserIDString, clan.ClanTag));
            }
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;

            BasePlayer player = info.InitiatorPlayer;
            if (player == null) return;

            ClanData clan = FindClanByUser(player.userID);
            if (clan == null) return;

            if (entity is BradleyAPC)
            {
                clan.Members[player.userID].Point += GetPointConfig("BradleyAPC");

                foreach (BasePlayer onlinePlayer in BasePlayer.activePlayerList) onlinePlayer.ChatMessage(GetLang("BRADLEYAPC", onlinePlayer.UserIDString, clan.ClanTag));
            }
            else if (entity.name.Contains("barrel"))
            {
                clan.Members[player.userID].Point += GetPointConfig("loot-barrel");
                clan.GatherClan["loot-barrel"].Farm++;
                clan.Members[player.userID].GatherMember["loot-barrel"]++;
            }
            else if (entity.name.Contains("wall") && !entity.name.Contains("wall.external.high"))
            {
                BuildingBlock buildingBlock = entity as BuildingBlock;
                if (buildingBlock == null) return;

                Int32 tier = (Int32)buildingBlock.grade;
                if (tier <= 1) return;

                if (tier == 2) clan.Members[player.userID].Point += GetPointConfig("WallTier2");

                if (tier == 3) clan.Members[player.userID].Point += GetPointConfig("WallTier3");

                if (tier == 4) clan.Members[player.userID].Point += GetPointConfig("WallTier4");
            }
        }

        private object OnTeamCreate(BasePlayer player)
        {
            if (player == null) return null;

            ClanData clan = FindClanByUser(player.userID);
            if (clan == null)
            {
                player.ChatMessage(GetLang("NO_CLAN", player.UserIDString));
                return false;
            }

            return null;
        }

        private object OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (team == null || player == null) return null;

            ClanData clan = FindClanByUser(player.userID);
            if (clan == null) return null;

            if (clan.IsOwner(player.userID))
            {
                player.ChatMessage(GetLang("IS_LEADER", player.UserIDString));
                return true;
            }

            clan.Kick(player.userID);
            return null;
        }

        private object OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer player, UInt64 target)
        {
            if (team == null || player == null) return null;

            ClanData clan = FindClanByUser(player.userID);
            if (clan == null) return null;

            if (!clan.IsMember(target)) return false;

            clan.Kick(target);
            return null;
        }

        private object OnTeamInvite(BasePlayer inviter, BasePlayer target)
        {
            if (inviter == null || target == null || !target.userID.IsSteamId()) return null;

            if (_cooldownPlayer.ContainsKey(inviter.userID) && _cooldownPlayer[inviter.userID].Subtract(DateTime.Now).TotalSeconds >= 0)
            {
                inviter.ChatMessage(GetLang("NO_FLOOD", inviter.UserIDString));
                return false;
            }

            _cooldownPlayer[inviter.userID] = DateTime.Now.AddSeconds(2f);

            SendInvite(inviter, target.userID);
            return null;
        }

        private void OnTeamPromote(RelationshipManager.PlayerTeam team, BasePlayer newLeader)
        {
            if (team == null || newLeader == null) return;

            ClanData clan = FindClanByUser(team.teamLeader);
            if (clan == null) return;

            clan.SetLeader(newLeader.userID);
        }

        private object OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (team == null || player == null) return null;

            AcceptInvite(player);
            return null;
        }

        private void OnTeamRejectInvite(BasePlayer player, RelationshipManager.PlayerTeam team)
        {
            if (team == null || player == null) return;

            Invite invite = _invites.Find(x => x.Recevier.userID == player.userID);
            if (invite == null)
            {
                player.ChatMessage(GetLang("NO_INVITE", player.UserIDString));
                return;
            }

            player.ChatMessage(GetLang("CANCEL_INVITE", player.UserIDString));
            invite.Inviter.ChatMessage(GetLang("INVITER_CANCEL_INVITE", invite.Inviter.UserIDString, player.IPlayer.Name));
            _invites.Remove(invite);
        }
        #endregion

        #region [Functional]
        private void TimeHandle()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                ClanData clan = FindClanByUser(player.userID);
                if (clan == null) continue;

                clan.Members[player.userID].Online++;
            }
        }

        private void RemoveInvite(String ClanTag, UInt64 playerID)
        {
            Invite invite = _invites.Find(p => p.ClanTag == ClanTag && p.Recevier.userID == playerID);
            if (invite == null) return;

            invite.Recevier.ChatMessage(GetLang("NO_TIME", invite.Recevier.UserIDString));
            invite.Recevier.ClearPendingInvite();
            invite.Inviter.ChatMessage(GetLang("INVITER_NO_TIME", invite.Inviter.UserIDString, invite.Recevier.IPlayer.Name));

            _invites.Remove(invite);
        }

        private void ApplySkinToItem(Item item, UInt64 Skin)
        {
            item.skin = Skin;
            item.MarkDirty();

            BaseEntity heldEntity = item.GetHeldEntity();
            if (heldEntity == null) return;

            heldEntity.skinID = Skin;
            heldEntity.SendNetworkUpdate();
        }

        private static void AuthCodeLock(UInt64 playerID, UInt64 friendID)
        {
            foreach (CodeLock codeLock in _._codeLockList)
            {
                if (playerID != codeLock.OwnerID || codeLock.whitelistPlayers.Contains(friendID)) continue;
                codeLock.whitelistPlayers.Add(friendID);
                codeLock.SendNetworkUpdate();
            }
        }

        private static void DeAuthCodeLock(UInt64 playerID, UInt64 friendID)
        {
            foreach (CodeLock codeLock in _._codeLockList)
            {
                if (playerID != codeLock.OwnerID || !codeLock.whitelistPlayers.Contains(friendID)) continue;
                codeLock.whitelistPlayers.Remove(friendID);
                codeLock.SendNetworkUpdate();
            }
        }

        private static void AuthBuilding(UInt64 playerID, UInt64 friendID)
        {
            foreach (BuildingPrivlidge buildingPrivlidge in _._cupboardList)
            {
                if (playerID != buildingPrivlidge.OwnerID) continue;
                buildingPrivlidge.authorizedPlayers.RemoveWhere((ulong x) => x == friendID);
                buildingPrivlidge.authorizedPlayers.Add(friendID);
                buildingPrivlidge.SendNetworkUpdate();
            }
        }

        private static void DeAuthBuilding(UInt64 playerID, UInt64 friendID)
        {
            foreach (BuildingPrivlidge buildingPrivlidge in _._cupboardList)
            {
                if (playerID != buildingPrivlidge.OwnerID) continue;
                buildingPrivlidge.authorizedPlayers.RemoveWhere((ulong x) => x == friendID);
                buildingPrivlidge.SendNetworkUpdate();
            }
        }

        private static void ChangeOwnerIDForLeader(UInt64 playerID, UInt64 leaderID)
        {
            foreach (BuildingPrivlidge buildingPrivlidge in _._cupboardList.Where(p => p.OwnerID == playerID))
            {
                buildingPrivlidge.OwnerID = leaderID;
                buildingPrivlidge.SendNetworkUpdate();
            }

            foreach (CodeLock codeLock in _._codeLockList.Where(p => p.OwnerID == playerID))
            {
                codeLock.OwnerID = leaderID;
                codeLock.SendNetworkUpdate();
            }

            foreach (SamSite samSite in _._sameSiteList.Where(p => p.OwnerID == playerID))
            {
                samSite.OwnerID = leaderID;
                samSite.SendNetworkUpdate();
            }
        }

        private Int32 GetPointConfig(String Name)
        {
            if (_config.ScoreTable.ContainsKey(Name))
                return _config.ScoreTable[Name];
            return 0;
        }

        private Boolean IsTeammates(UInt64 player, UInt64 friend)
        {
            return player == friend ||
                    RelationshipManager.ServerInstance.FindPlayersTeam(player)?.members?.Contains(friend) == true ||
                    FindClanByUser(player)?.IsMember(friend) == true;
        }

        Int32 GetClanIndex(String ClanTag)
        {
            Int32 Top = 1;
            IEnumerable<ClanData> clansList = _clansList.OrderByDescending(p => p.GetScore());

            foreach (ClanData clan in clansList)
            {
                if (clan.ClanTag == ClanTag)
                    break;
                Top++;
            }
            return Top;
        }

        public String FormatShortTime(TimeSpan time, String UserIDString)
        {
            String result = String.Empty;
            Boolean GetLanguage = lang.GetLanguage(UserIDString) == "ru";

            if (time == new TimeSpan())
                return "0 сек.";

            if (time.Days != 0)
                result += $"{time.Days} {(GetLanguage ? "д." : "d.")} ";

            if (time.Hours != 0)
                result += $"{time.Hours} {(GetLanguage ? "час." : "hour.")} ";

            if (time.Minutes != 0)
                result += $"{time.Minutes} {(GetLanguage ? "мин." : "min.")} ";

            if (time.Seconds != 0)
                result += $"{time.Seconds} {(GetLanguage ? "сек." : "sec.")} ";

            return result;
        }

        private Int32 FindItemID(String shortName)
        {
            Int32 val;
            if (_itemIds.TryGetValue(shortName, out val))
                return val;

            ItemDefinition definition = ItemManager.FindItemDefinition(shortName);
            if (definition == null) return 0;

            val = definition.itemid;
            _itemIds[shortName] = val;
            return val;
        }

        private void GetAvatar(String userId, Action<String> callback)
        {
            if (callback == null) return;

            try
            {
                webrequest.Enqueue($"http://steamcommunity.com/profiles/{userId}?xml=1", null, (code, response) =>
                {
                    if (code != 200 || response == null)
                        return;

                    String avatar = Regex.Match(response).Groups[1].ToString();
                    if (String.IsNullOrEmpty(avatar))
                        return;

                    callback.Invoke(avatar);
                }, this);
            }
            catch (Exception e)
            {
                PrintError($"{e.Message}");
            }
        }

        private void RewardClans()
        {
            if (String.IsNullOrEmpty(_config.GameStoreSettings.ShopID) || String.IsNullOrEmpty(_config.GameStoreSettings.SecretKey) || String.IsNullOrEmpty(_config.GameStoreSettings.ServerID)) return;

            StringBuilder text = new StringBuilder();
            IEnumerable<ClanData> _clansReward = _clansList.OrderByDescending(x => x.GetScore());
            Int32 i = 1;

            foreach (ClanData clan in _clansReward)
            {
                if (!_config.GameStoreSettings.RewardList.ContainsKey(i) || clan.GetScore() <= _config.GameStoreSettings.MinPoint) break;

                Int32 Bonus = _config.GameStoreSettings.RewardList[i];
                Double BonusPlayer = _config.GameStoreSettings.Prize ? Bonus / (Double)clan.Members.Count : Bonus;

                text.Append(_config.GameStoreSettings.Prize ? $"{i}. {clan.ClanTag} received a {BonusPlayer} usd each." : $"{i}. {clan.ClanTag} получили по {BonusPlayer} руб.");

                foreach (KeyValuePair<UInt64, ClanData.PlayerData> member in clan.Members)
                {
                    if (member.Value.Point <= _config.GameStoreSettings.MinPointPlayer) continue;

                    webrequest.Enqueue($"https://gamestores.app/api?shop_id={_config.GameStoreSettings.ShopID}&secret={_config.GameStoreSettings.SecretKey}&server={_config.GameStoreSettings.ServerID}&action=moneys&type=plus&steam_id={member.Key}&amount={BonusPlayer}&mess=Награда за клановый топ.", "", (code, response) =>
                    {
                        Puts(LanguageEn ? $"Player {member.Key} received: {BonusPlayer} units for the store" : $"Игроку {member.Key} получил: {BonusPlayer} единиц на магазин");
                    }, this, Core.Libraries.RequestMethod.GET);
                }

                i++;
            }

            if (_config.DiscordSetting.DiscordNotificationEnable)
            {
                DiscordMessage message = new DiscordMessage();

                Embed embed = new Embed();

                embed.AddTitle(LanguageEn ? "List of top clans by past wipe" : "Список топа кланов по прошедшему вайпу");

                embed.AddDescription(text.ToString());

                embed.AddColor("#de8732");

                message.AddEmbed(embed);

                SendDiscordMessage(_config.DiscordSetting.DiscordWebHook, message);
            }
        }

        private void FillingTeams()
        {
            if (_config.AutoTeamCreation)
            {
                RelationshipManager.maxTeamSize = _config.LimitSettings.MemberLimit;

                _clansList.ForEach(clan =>
                {
                    clan.FindOrCreateTeam();

                    clan.Members.Keys.ToList().ForEach(member => clan.AddPlayer(member));
                });
            }
        }
        #endregion

        #region [Interface]
        [ChatCommand("ctop")]
        void MainUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Name = Layer,
                Parent = ".Mains",
                Components =
                {
                    new CuiRawImageComponent { Png = _imageUI.GetImage("FON_CLAN_TOP") },
                    new CuiRectTransformComponent { AnchorMin = "-0.315 -0.27", AnchorMax = "1.3 1.275", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
                Button = { Close = Layer, Command = "clan.closeui", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.326 0.74", AnchorMax = "0.4 0.778" },
                Button = { Command = "UI_CLANS returnClanMain", Color = "0 0 0 0" },
                Text = { Text = "             Назад", Color = "1 1 1 0.6", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft }
            }, Layer);

            ClanTop(ref container, player);

            CuiHelper.AddUi(player, container);
        }

        private void ClanTop(ref CuiElementContainer container, BasePlayer player)
        {
            ClanData clan = FindClanByUser(player.userID);

            #region [Main-Gui]
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.332 0.68", AnchorMax = "0.645 0.72", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Main1");
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.332 0.4", AnchorMax = "0.67 0.675", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Main");

            #region [Text]
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.933", AnchorMax = "0.997 0.997" },
                Image = { Color = "0 0 0 0" }
            }, Layer + ".Main1", Layer + ".Text");

            container.Add(new CuiElement
            {
                Parent = Layer + ".Text",
                Components =
                {
                    new CuiTextComponent { Text = $"Название клана", Color = "1 1 1 0.7", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft},
                    new CuiRectTransformComponent { AnchorMin = $"0.02 -10", AnchorMax = $"1 1" },
                    new CuiOutlineComponent {Color = "1 1 1 0.1", Distance = "0.1 0.1"},
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".Text",
                Components =
                {
                    new CuiTextComponent { Text = $"Награда", Color = "1 1 1 0.7", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft},
                    new CuiRectTransformComponent { AnchorMin = $"0.28 -9.8", AnchorMax = $"1 1" },
                    new CuiOutlineComponent {Color = "1 1 1 0.1", Distance = "0.1 0.1"},
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".Text",
                Components =
                {
                    new CuiTextComponent { Text = $"Турнир", Color = "1 1 1 0.7", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft},
                    new CuiRectTransformComponent { AnchorMin = $"0.458 -10", AnchorMax = $"1 1" },
                    new CuiOutlineComponent {Color = "1 1 1 0.1", Distance = "0.1 0.1"},
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".Text",
                Components =
                {
                    new CuiTextComponent { Text = $"К/Д", Color = "1 1 1 0.7", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft},
                    new CuiRectTransformComponent { AnchorMin = $"0.618 -10", AnchorMax = $"1 1" },
                    new CuiOutlineComponent {Color = "1 1 1 0.1", Distance = "0.1 0.1"},
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".Text",
                Components =
                {
                    new CuiTextComponent { Text = $"Очки", Color = "1 1 1 0.7", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft},
                    new CuiRectTransformComponent { AnchorMin = $"0.74 -10", AnchorMax = $"1 1" },
                    new CuiOutlineComponent {Color = "1 1 1 0.1", Distance = "0.1 0.1"},
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".Text",
                Components =
                {
                    new CuiTextComponent { Text = $"Игроков", Color = "1 1 1 0.7", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft},
                    new CuiRectTransformComponent { AnchorMin = $"0.87 -10", AnchorMax = $"1 1" },
                    new CuiOutlineComponent {Color = "1 1 1 0.1", Distance = "0.1 0.1"},
                }
            });
            #endregion

            TopClanList(ref container, player);

            CuiHelper.DestroyUi(player, Layer + ".Ctop");
            CuiHelper.DestroyUi(player, Layer + ".Main");
            CuiHelper.DestroyUi(player, Layer + ".Main1");
            CuiHelper.DestroyUi(player, Layer + ".Description");
            CuiHelper.DestroyUi(player, Layer + ".Info");
            CuiHelper.DestroyUi(player, Layer + ".Info1");
            CuiHelper.DestroyUi(player, Layer + ".Info2");
        }

        private void TopClanList(ref CuiElementContainer container, BasePlayer player, Int32 page = 0)
        {
            IEnumerable<ClanData> clansList = _clansList.OrderByDescending(p => p.GetScore());
            Int32 i = 0;

            float width = 0.912f, height = 0.124f, startxBox = 0.005f, startyBox = 0.99f - height, xmin = startxBox, ymin = startyBox;
            for (Int32 y = 0; y < 5; y++)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" }
                }, Layer + ".Main", Layer + ".Main" + $".TopLine{y}");

                xmin += width;
                if (xmin + width >= 0)
                {
                    xmin = startxBox;
                    ymin -= height + 0.045f;
                }
            }

            foreach (ClanData clan in clansList.Skip(5 * page).Take(5))
            {
                container.Add(new CuiLabel
                {
                    Text = { Text = $"{i + 1 + (page * 5)}.", Color = "1 1 1 0.3", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.067 1" },
                }, Layer + ".Main" + $".TopLine{i}");

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{clan.ClanTag}", Font = "robotocondensed-regular.ttf", FontSize = 11, Color = "1 1 1 0.3", Align = TextAnchor.MiddleLeft },
                    RectTransform = { AnchorMin = $"0.05 0", AnchorMax = $"0.45 1" },
                }, Layer + ".Main" + $".TopLine{i}");

                if (_config.GameStoreSettings.RewardList.ContainsKey(i + 1 + (page * 5)))
                {
                    container.Add(new CuiLabel
                    {
                        Text = { Text = $"{_config.GameStoreSettings.RewardList[i + 1 + (page * 5)]}", Color = "1 1 1 0.3", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                        RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.655 1" },
                    }, Layer + ".Main" + $".TopLine{i}");
                }
                String tournamentText = "";
                if (!string.IsNullOrEmpty(clan.ClanTag))
                {
                    Plugin tournamentPlugin = TPTournament ?? plugins.Find("TPTournament");
                    if (tournamentPlugin != null && tournamentPlugin.IsLoaded)
                    {
                        try
                        {
                            string clanTagTrimmed = clan.ClanTag.Trim();

                            object status = null;
                            var method = tournamentPlugin.GetType().GetMethod("GetTournamentStatus", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (method != null)
                            {
                                status = method.Invoke(tournamentPlugin, new object[] { clanTagTrimmed });
                            }
                            else
                            {
                                status = tournamentPlugin.CallHook("GetTournamentStatus", clanTagTrimmed);
                            }

                            if (status != null)
                            {
                                string statusStr = status.ToString().Trim();
                                if (!string.IsNullOrEmpty(statusStr))
                                {
                                    statusStr = statusStr.ToLower();
                                    if (statusStr == "active")
                                    {
                                        tournamentText = GetLang("ACTIVE_TOURNAMENT", player.UserIDString);
                                    }
                                    else if (statusStr == "eliminated")
                                    {
                                        tournamentText = GetLang("ELIMINATED_TOURNAMENT", player.UserIDString);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            PrintError($"[TPClan] Error calling GetTournamentStatus for clan '{clan.ClanTag}': {ex.Message}");
                        }
                    }
                }
                container.Add(new CuiLabel
                {
                    Text = { Text = tournamentText, Font = "robotocondensed-regular.ttf", FontSize = 11, Color = "1 1 1 0.3", Align = TextAnchor.MiddleLeft },
                    RectTransform = { AnchorMin = $"0.448 0", AnchorMax = $"1 1" },
                }, Layer + ".Main" + $".TopLine{i}");
                container.Add(new CuiLabel
                {
                    Text = { Text = $"{clan.Members.Sum(p => p.Value.Kill)}/{clan.Members.Sum(p => p.Value.Death)}", Font = "robotocondensed-regular.ttf", FontSize = 11, Color = "1 1 1 0.3", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = $"0.495 0", AnchorMax = $"0.79 1" },
                }, Layer + ".Main" + $".TopLine{i}");


                container.Add(new CuiLabel
                {
                    Text = { Text = $"{clan.GetScore()}", Font = "robotocondensed-regular.ttf", FontSize = 11, Color = "1 1 1 0.3", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = $"0.66 0", AnchorMax = $"0.9 1" },
                }, Layer + ".Main" + $".TopLine{i}");

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{clan.Members.Count}", Font = "robotocondensed-regular.ttf", FontSize = 11, Color = "1 1 1 0.3", Align = TextAnchor.MiddleLeft },
                    RectTransform = { AnchorMin = $"0.923 0", AnchorMax = $"1 1" },
                }, Layer + ".Main" + $".TopLine{i}");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.25 0.25 0.23 0", Command = $"UI_CLANS info {clan.ClanTag}" },
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                }, Layer + ".Main" + $".TopLine{i}");

                i++;
            }

            #region [Button]

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = page > 0 ? $"UI_CLANS page {page - 1}" : "" },
                Text = { Text = "", FontSize = 22, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = $"0.94 0.6", AnchorMax = $"1 0.984" },
            }, Layer + ".Main", Layer + ".Main" + ".Page" + ".Next");

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = clansList.Skip(5 * (page + 1)).Count() > 0 ? $"UI_CLANS page {page + 1}" : "" },
                Text = { Text = "", FontSize = 22, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = $"0.94 0.19", AnchorMax = $"1 0.56" },
            }, Layer + ".Main", Layer + ".Main" + ".Page" + ".Previus");
            #endregion

            CuiHelper.DestroyUi(player, Layer + ".Main" + ".Page");
            CuiHelper.DestroyUi(player, Layer + ".Main" + ".Page" + ".Next");
            CuiHelper.DestroyUi(player, Layer + ".Main" + ".Page" + ".Previus");
            for (Int32 y = 0; y < 7; y++) CuiHelper.DestroyUi(player, Layer + ".Main" + $".TopLine{y}");
        }

        void ClanTopInfo(BasePlayer player, ClanData clan)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Name = Layer,
                Parent = ".Mains",
                Components =
                {
                    new CuiRawImageComponent { Png = _imageUI.GetImage("FON_CLAN_STAT") },
                    new CuiRectTransformComponent { AnchorMin = "-0.315 -0.27", AnchorMax = "1.3 1.275", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
                Button = { Close = Layer, Command = "clan.closeui", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer);

            #region [Main-Gui]
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.599 0.288", AnchorMax = "0.751 0.558", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Info");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.32 0.573", AnchorMax = "0.78 0.733", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Info1");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.244 0.573", AnchorMax = "0.3155 0.731", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Info2");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.25 0.3", AnchorMax = "0.589 0.525", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Info4");
            container.Add(new CuiLabel
            {
                Text = { Text = $"{clan.ClanTag}", Color = "1 1 1 0.5", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = $"0 0.1", AnchorMax = $"1 0.235", OffsetMax = "0 0" },
            }, Layer + ".Info2");
            #endregion

            #region [Avatar]
            container.Add(new CuiElement
            {
                Parent = Layer + ".Info2",
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage($"{clan.Avatar}") },
                    new CuiRectTransformComponent { AnchorMin = "0.23 0.4", AnchorMax = "0.77 0.82", OffsetMax = "0 0" }
                }
            });
            #endregion

            #region [Buttons]
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.244 0.74", AnchorMax = "0.315 0.778" },
                Button = { Command = $"UI_CLANS returnClanTop {Math.Ceiling(GetClanIndex(clan.ClanTag) / 10f)}", Color = "0 0 0 0" },
                Text = { Text = $"             {GetLang("RETURN", player.UserIDString)}", Color = "1 1 1 0.6", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft }
            }, Layer);
            #endregion

            #region [Resourse]
            float width = 0.271f, height = 0.28f, startxBox = 0.05f, startyBox = 0.961f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in clan.GatherClan.Select((y, t) => new { A = y, B = t }).Take(9))
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" },
                }, Layer + ".Info", Layer + ".Info" + $".Resourse{check.B}");
                xmin += width + 0.045f;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + 0.038f;
                }

                if (FindItemID(check.A.Key) != 0)
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Info" + $".Resourse{check.B}",
                        Components =
                        {
                            new CuiImageComponent { ItemId = FindItemID(check.A.Key), SkinId = 0 },
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "7 7", OffsetMax = "-7 -7"}
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Info" + $".Resourse{check.B}",
                        Components =
                        {
                            new CuiRawImageComponent { Png = GetImage($"{check.A.Key}") },
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "7 7", OffsetMax = "-7 -7"}
                        }
                    });
                }

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Info" + $".Resourse{check.B}",
                    Components =
                    {
                        new CuiTextComponent { Text = $"{check.A.Value.Farm}", Color = "1 1 1 0.3", Align = TextAnchor.MiddleRight, FontSize = 10, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0 0.7", AnchorMax = $"0.9 0.93" },
                    }
                });
            }
            #endregion

            #region [InfoClan]
            int capturedZones = 0;
            if (TPCaptureZone != null && TPCaptureZone.IsLoaded)
            {
                try
                {
                    var method = TPCaptureZone.GetType().GetMethod("GetCapturedZonesCount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (method != null)
                    {
                        var result = method.Invoke(TPCaptureZone, new object[] { clan.ClanTag });
                        if (result != null && int.TryParse(result.ToString(), out int count))
                        {
                            capturedZones = count;
                        }
                    }
                    else
                    {
                        var hookResult = TPCaptureZone.CallHook("GetCapturedZonesCount", clan.ClanTag);
                        if (hookResult != null && int.TryParse(hookResult.ToString(), out int hookCount))
                        {
                            capturedZones = hookCount;
                        }
                    }
                }
                catch (Exception ex)
                {
                    PrintError($"[TPClan] Error getting captured zones count for clan '{clan.ClanTag}': {ex.Message}");
                }
            }

            int capturedQuarries = 0;
            if (QuarryCapture != null && QuarryCapture.IsLoaded)
            {
                try
                {
                    var method = QuarryCapture.GetType().GetMethod("GetCapturedQuarriesCount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (method != null)
                    {
                        var result = method.Invoke(QuarryCapture, new object[] { clan.ClanTag });
                        if (result != null && int.TryParse(result.ToString(), out int count))
                        {
                            capturedQuarries = count;
                        }
                    }
                    else
                    {
                        var hookResult = QuarryCapture.CallHook("GetCapturedQuarriesCount", clan.ClanTag);
                        if (hookResult != null && int.TryParse(hookResult.ToString(), out int hookCount))
                        {
                            capturedQuarries = hookCount;
                        }
                    }
                }
                catch (Exception ex)
                {
                    PrintError($"[TPClan] Error getting captured quarries count for clan '{clan.ClanTag}': {ex.Message}");
                }
            }

            int capturedRT = 0;
            if (TPRTCapture != null && TPRTCapture.IsLoaded)
            {
                try
                {
                    var method = TPRTCapture.GetType().GetMethod("GetCapturedRTCount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (method != null)
                    {
                        var result = method.Invoke(TPRTCapture, new object[] { clan.ClanTag });
                        if (result != null && int.TryParse(result.ToString(), out int count))
                        {
                            capturedRT = count;
                        }
                    }
                    else
                    {
                        var hookResult = TPRTCapture.CallHook("GetCapturedRTCount", clan.ClanTag);
                        if (hookResult != null && int.TryParse(hookResult.ToString(), out int hookCount))
                        {
                            capturedRT = hookCount;
                        }
                    }
                }
                catch (Exception ex)
                {
                    PrintError($"[TPClan] Error getting captured RT count for clan '{clan.ClanTag}': {ex.Message}");
                }
            }

            Dictionary<String, String> InfoClan = new Dictionary<String, String>()
            {
                { $"Убийства", $"\n{clan.Members.Sum(p => p.Value.Kill)}" },
                { $"Смерти", $"\n{clan.Members.Sum(p => p.Value.Death)}" },
                { $"К/Д", $"\n{clan.Members.Sum(p => p.Value.Kill)}/{clan.Members.Sum(p => p.Value.Death)}" },
                { $"Фарм", $"\n0" },
                { $"Очков", $"\n{clan.GetScore()}" },
                { $"Игроков в игре", $"\n{clan.Members.Count}" },
                { $"Захвачено Зон", $"\n{capturedZones}" },
                { $"Захвачено карьеров", $"\n{capturedQuarries}" },
                { $"Захвачено РТ", $"\n{capturedRT}" },
                { $"Залутано MG", $"\n0" },
            };
            float width1 = 0.183f, height1 = 0.45f, startxBox1 = 0f, startyBox1 = 0.98f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
            foreach (var check in InfoClan.Select((u, t) => new { A = u, B = t }))
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = xmin1 + " " + ymin1, AnchorMax = (xmin1 + width1) + " " + (ymin1 + height1 * 1), OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" },
                }, Layer + ".Info1", Layer + ".Info" + $".Info{check.B}");
                xmin1 += width1 + 0.007f;
                if (xmin1 + width1 >= 1)
                {
                    xmin1 = startxBox1;
                    ymin1 -= height1 + 0.047f;
                }

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Info" + $".Info{check.B}",
                    Components =
                    {
                        new CuiTextComponent { Text = $"{check.A.Key}", Color = "1 1 1 0.4", FontSize = 12, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                        new CuiRectTransformComponent { AnchorMin = $"0.015 0.4", AnchorMax = $"1 1" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Info" + $".Info{check.B}",
                    Components =
                    {
                        new CuiTextComponent { Text = $"{check.A.Value}", Color = "1 1 1 0.4", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                        new CuiRectTransformComponent { AnchorMin = $"0.03 0", AnchorMax = $"0.985 0.9" },
                    }
                });
            }
            #endregion

            #region [Title]
            #endregion

            InfoClanList(ref container, clan, player);

            CuiHelper.DestroyUi(player, Layer + ".Ctop");
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.DestroyUi(player, Layer + ".Main1");
            CuiHelper.DestroyUi(player, Layer + ".Description");
            CuiHelper.AddUi(player, container);
        }

        private void InfoClanList(ref CuiElementContainer container, ClanData clan, BasePlayer player, Int32 page = 0)
        {
            var playersList = clan.Members.OrderByDescending(p => p.Value.Point);
            Int32 i = 0;

            float width = 0.91f, height = 0.167f, startxBox = 0.005f, startyBox = 0.99f - height, xmin = startxBox, ymin = startyBox;
            for (Int32 y = 0; y < 5; y++)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" }
                }, Layer + ".Info4", Layer + ".Info" + $".Line{y}");

                xmin += width;
                if (xmin + width >= 0)
                {
                    xmin = startxBox;
                    ymin -= height + 0.04f;
                }
            }

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.244 0.523", AnchorMax = $"0.562 0.557", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Info4");

            container.Add(new CuiElement
            {
                Parent = Layer + ".Info4",
                Components =
                {
                    new CuiTextComponent { Text = $"Имя игрока", Color = "1 1 1 0.7", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft},
                    new CuiRectTransformComponent { AnchorMin = $"0.025 0", AnchorMax = $"0.4 1" },
                    new CuiOutlineComponent {Color = "1 1 1 0.2", Distance = "0.1 0.1"},
                }
            });


            container.Add(new CuiElement
            {
                Parent = Layer + ".Info4",
                Components =
                {
                    new CuiTextComponent { Text = $"Очков", Color = "1 1 1 0.7", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft},
                    new CuiRectTransformComponent { AnchorMin = $"0.493 0", AnchorMax = $"0.7 1" },
                    new CuiOutlineComponent {Color = "1 1 1 0.2", Distance = "0.1 0.1"},
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".Info4",
                Components =
                {
                    new CuiTextComponent { Text = $"Убийств", Color = "1 1 1 0.7", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft},
                    new CuiRectTransformComponent { AnchorMin = $"0.61 0", AnchorMax = $"0.8 1" },
                    new CuiOutlineComponent {Color = "1 1 1 0.2", Distance = "0.1 0.1"},
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".Info4",
                Components =
                {
                    new CuiTextComponent { Text = $"Смертей", Color = "1 1 1 0.7", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft},
                    new CuiRectTransformComponent { AnchorMin = $"0.75 0", AnchorMax = $"0.9 1" },
                    new CuiOutlineComponent {Color = "1 1 1 0.2", Distance = "0.1 0.1"},
                }
            });


            container.Add(new CuiElement
            {
                Parent = Layer + ".Info4",
                Components =
                {
                    new CuiTextComponent { Text = $"К/Д", Color = "1 1 1 0.7", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft},
                    new CuiRectTransformComponent { AnchorMin = $"0.897 0", AnchorMax = $"1 1" },
                    new CuiOutlineComponent {Color = "1 1 1 0.2", Distance = "0.1 0.1"},
                }
            });

            foreach (KeyValuePair<UInt64, ClanData.PlayerData> member in playersList.Skip(5 * page).Take(5))
            {
                container.Add(new CuiLabel
                {
                    Text = { Text = $"{i + (1 + (page * 5))}.", Color = "1 1 1 0.3", FontSize = 12, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft },
                    RectTransform = { AnchorMin = $"0.025 0", AnchorMax = $"0.1 1" },
                }, Layer + ".Info" + $".Line{i}");

                IPlayer memberPlayer = covalence.Players.FindPlayerById(member.Key.ToString());
                String playerName = memberPlayer != null ? memberPlayer.Name : $"Игрок {member.Key}";

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{playerName}", Font = "robotocondensed-regular.ttf", FontSize = 11, Color = "1 1 1 0.3", Align = TextAnchor.MiddleLeft },
                    RectTransform = { AnchorMin = $"0.05 0", AnchorMax = $"0.3 1" },
                }, Layer + ".Info" + $".Line{i}");

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{member.Value.Point}", Color = "1 1 1 0.3", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = $"0.215 0", AnchorMax = $"0.85 1" },
                }, Layer + ".Info" + $".Line{i}");

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{member.Value.Kill}", Color = "1 1 1 0.3", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = $"0.32 0", AnchorMax = $"1 1" },
                }, Layer + ".Info" + $".Line{i}");

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{member.Value.Death}", Color = "1 1 1 0.3", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = $"0.605 0", AnchorMax = $"1 1" },
                }, Layer + ".Info" + $".Line{i}");
                container.Add(new CuiLabel
                {
                    Text = { Text = $"{member.Value.Kill}/{member.Value.Death}", Color = "1 1 1 0.3", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = $"0.84 0", AnchorMax = $"1 1" },
                }, Layer + ".Info" + $".Line{i}");

                i++;
            }

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = page > 0 ? $"UI_CLANS infopage {clan.ClanTag} {page - 1}" : "" },
                Text = { Text = "", Color = "1 1 1 1", FontSize = 22, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = $"0.567 0.417", AnchorMax = $"0.587 0.52" },
            }, Layer, Layer + ".Info" + ".Previus");

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = playersList.Skip(5 * (page + 1)).Count() > 0 ? $"UI_CLANS infopage {clan.ClanTag} {page + 1}" : "" },
                Text = { Text = "", Color = "1 1 1 1", FontSize = 22, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = $"0.567 0.302", AnchorMax = $"0.587 0.405" },
            }, Layer, Layer + ".Info" + ".Next");

            CuiHelper.DestroyUi(player, Layer + ".Info" + ".Text");
            CuiHelper.DestroyUi(player, Layer + ".Info" + ".Previus");
            CuiHelper.DestroyUi(player, Layer + ".Info" + ".Next");
            for (Int32 y = 0; y < 5; y++) CuiHelper.DestroyUi(player, Layer + ".Info" + $".Line{y}");
        }

        private void ClanMainUi(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Name = Layer,
                Parent = ".Mains",
                Components =
                {
                    new CuiRawImageComponent { Png = _imageUI.GetImage("MAIN_FON") },
                    new CuiRectTransformComponent { AnchorMin = "-0.315 -0.27", AnchorMax = "1.3 1.275", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
                Button = { Close = "Menu_UI", Command = "clan.closeui", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer);

            ClanMainInterface(ref container, player);

            CuiHelper.DestroyUi(player, Layer + ".Main");
            CuiHelper.AddUi(player, container);
        }

        private void ClanMainInterface(ref CuiElementContainer container, BasePlayer player)
        {
            ClanData clan = FindClanByUser(player.userID);
            if (clan == null) 
            {
                // Показываем интерфейс создания клана, если у игрока нет клана
                CreateClanInterface(ref container, player);
                return;
            }

            if (clan.Task == null)
            {
                clan.Task = String.Empty;
            }

            #region [Main-Gui]
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.251 0.617", AnchorMax = "0.308 0.7195", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Panel7");
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.3 0.643", AnchorMax = "0.405 0.72", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0", Material = "assets/icons/greyout.mat" }
            }, Layer, Layer + ".Panel1");
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.244 0.463", AnchorMax = "0.437 0.595", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Panel2");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.609 0.237", AnchorMax = "0.75 0.715", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Panel3");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.25 0.205", AnchorMax = "0.589 0.385", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Panel4");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.25 0.39", AnchorMax = "0.589 0.42", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Panel8");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.45 0.475", AnchorMax = "0.588 0.719", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Panel5");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-300 131", OffsetMax = "138 307" },
                Image = { Color = "0.3773585 0.3755785 0.3755785 0", Material = "assets/icons/greyout.mat" }
            }, Layer, Layer + ".Panel9");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-300 131", OffsetMax = "138 307" },
                Image = { Color = "0.3773585 0.3755785 0.3755785 0", Material = "assets/icons/greyout.mat" }
            }, Layer, Layer + ".Panel10");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-300 131", OffsetMax = "138 307" },
                Image = { Color = "0.3773585 0.3755785 0.3755785 0", Material = "assets/icons/greyout.mat" }
            }, Layer, Layer + ".Panel11");

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = $"chat.say /ctop" },
                Text = { Text = "Топ кланы", Color = "1 1 1 0.6", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.493 0.743", AnchorMax = "0.593 0.775" },
            }, Layer);

            #endregion

            container.Add(new CuiLabel
            {
                Text = { Text = $"{clan.ClanTag}", Color = "1 1 1 0.6", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft },
                RectTransform = { AnchorMin = $"1.13 0.64", AnchorMax = $"1.8 1" },
            }, Layer + ".Panel7");

            #region [Avatar]
            container.Add(new CuiElement
            {
                Parent = Layer + ".Panel7",
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage($"{clan.Avatar}") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "9 9", OffsetMax = "-9 -9" }
                }
            });
            #endregion

            #region [Info]
            Dictionary<String, String> InfoClan = new Dictionary<String, String>()
            {
                { $"{GetLang("LEADER_CLAN", player.UserIDString)}", $"{clan.LeaderName}" },
                { $"{GetLang("PLAYERS_INGAME", player.UserIDString)}", $"{clan.Online()}/{clan.Members.Count}" },
                { $"{GetLang("TOTAL_RATE", player.UserIDString)}", $"{clan.GetPercentClan()}" },
            };

            foreach (var check in InfoClan.Select((u, t) => new { A = u, B = t }))
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0.135 {0.285 - Math.Floor((float) check.B/ 1) * 0.292}",
                                        AnchorMax = $"1.22 {0.525 - Math.Floor((float) check.B / 1) * 0.292}", },
                    Image = { Color = "0 0 0 0", Material = "assets/icons/greyout.mat" }
                }, Layer + ".Panel1", Layer + ".Panel1" + $".Info{check.B}");

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Panel1" + $".Info{check.B}",
                    Components =
                    {
                        new CuiTextComponent { Text = $"{check.A.Key}", Color = "1 1 1 0.2", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft },
                        new CuiRectTransformComponent { AnchorMin = $"0.01 0", AnchorMax = $"0.8 1" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Panel1" + $".Info{check.B}",
                    Components =
                    {
                        new CuiTextComponent { Text = $"{check.A.Value}", Color = "1 1 1 0.8", FontSize = 9, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleRight },
                        new CuiRectTransformComponent { AnchorMin = $"0.5 0", AnchorMax = $"0.985 1" },
                    }
                });
            }
            #endregion

            #region [Task]
            container.Add(new CuiLabel
            {
                Text = { Text = $"{GetLang("CLAN_TASK", player.UserIDString)}", Align = TextAnchor.MiddleLeft, FontSize = 14, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.5" },
                RectTransform = { AnchorMin = $"0.07 0.6", AnchorMax = $"1 1", OffsetMax = "0 0" },
            }, Layer + ".Panel2");

            container.Add(new CuiElement
            {
                Name = Layer + ".Panel2" + ".Task",
                Parent = Layer + ".Panel2",
                Components =
                    {
                        new CuiImageComponent { Color = "0 0 0 0" },
                        new CuiRectTransformComponent {AnchorMin = "0.07 0.04", AnchorMax = "0.93 0.64", OffsetMax = "0 0"}
                    }
            });

            String taskText = String.IsNullOrWhiteSpace(clan.Task) ? GetLang("NO_TASK", player.UserIDString) : clan.Task;
            container.Add(new CuiLabel
            {
                Text = { Text = taskText, Align = TextAnchor.UpperLeft, FontSize = 10, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.2" },
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
            }, Layer + ".Panel2" + ".Task");
            #endregion

            #region [Wear]
            float width1 = 0, height1 = 0, z = 0;
            foreach (var check in clan.SkinList.Select((y, t) => new { A = y, B = t }).Take(6))
            {
                if (z == 0)
                {
                    width1 = 0.005f;
                    height1 = 0.626f;
                }
                else if (z == 1)
                {
                    width1 = 0.005f;
                    height1 = 0.312f;
                }
                else if (z == 2)
                {
                    width1 = 0.347f;
                    height1 = 0.018f;
                }
                else if (z == 3)
                {
                    width1 = 0.687f;
                    height1 = 0.853f;
                }
                else if (z == 4)
                {
                    width1 = 0.687f;
                    height1 = 0.625f;
                }
                else if (z == 5)
                {
                    width1 = 0.687f;
                    height1 = 0.313f;
                }
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{width1} {height1}", AnchorMax = $"{width1 + 0.306f} {height1 + 0.13f}", },
                    Image = { Color = "0 0 0 0" }
                }, Layer + ".Panel3", Layer + ".Panel3" + $".Wear{check.B}");

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Panel3" + $".Wear{check.B}",
                    Components =
                    {
                        new CuiImageComponent { ItemId = FindItemID(check.A.Key), SkinId = check.A.Value },
                        new CuiRectTransformComponent { AnchorMin = "0.15 0.05", AnchorMax = "0.825 0.95" }
                    }
                });

                if (clan.IsOwner(player.userID))
                {
                    container.Add(new CuiButton
                    {
                        Button = { Color = "0 0 0 0", Command = $"UI_CLANS OpenChoiseSkins" },
                        Text = { Text = "" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    }, Layer + ".Panel3" + $".Wear{check.B}");
                }
                z++;
            }
            #endregion

            #region [Text]
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.997 0.9985" },
            }, Layer + ".Panel8", Layer + ".Panel8" + ".PanelText");

            container.Add(new CuiElement
            {
                Parent = Layer + ".Panel8" + ".PanelText",
                Components =
                {
                    new CuiTextComponent { Text = $"Имя игрока", Color = "1 1 1 0.7", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft},
                    new CuiRectTransformComponent { AnchorMin = $"0.027 0", AnchorMax = $"1 1"  },
                    new CuiOutlineComponent {Color = "1 1 1 0.2", Distance = "0.1 0.1"},
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".Panel8" + ".PanelText",
                Components =
                {
                    new CuiTextComponent { Text = $"Очков", Color = "1 1 1 0.7", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft},
                    new CuiRectTransformComponent { AnchorMin = $"0.382 0", AnchorMax = $"1 1" },
                    new CuiOutlineComponent {Color = "1 1 1 0.2", Distance = "0.1 0.1"},
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".Panel8" + ".PanelText",
                Components =
                {
                    new CuiTextComponent { Text = $"Убийств", Color = "1 1 1 0.7", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft},
                    new CuiRectTransformComponent { AnchorMin = $"0.522 0", AnchorMax = $"1 1" },
                    new CuiOutlineComponent {Color = "1 1 1 0.2", Distance = "0.1 0.1"},
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".Panel8" + ".PanelText",
                Components =
                {
                    new CuiTextComponent { Text = $"Смертей", Color = "1 1 1 0.7", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft},
                    new CuiRectTransformComponent { AnchorMin = $"0.672 0", AnchorMax = $"1 1" },
                    new CuiOutlineComponent {Color = "1 1 1 0.2", Distance = "0.1 0.1"},
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".Panel8" + ".PanelText",
                Components =
                {
                    new CuiTextComponent { Text = $"К/Д", Color = "1 1 1 0.7", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft},
                    new CuiRectTransformComponent { AnchorMin = $"0.827 0", AnchorMax = $"1 1" },
                    new CuiOutlineComponent {Color = "1 1 1 0.2", Distance = "0.1 0.1"},
                }
            });

            #endregion

            #region [Resourse]

            float width = 0.298f, height = 0.3f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in clan.GatherClan.Select((y, t) => new { A = y, B = t }).Take(9))
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" },
                }, Layer + ".Panel5", Layer + ".Panel5" + $".Resourse{check.B}");
                xmin += width + 0.049f;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + 0.048f;
                }

                if (FindItemID(check.A.Key) != 0)
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Panel5" + $".Resourse{check.B}",
                        Components =
                        {
                            new CuiImageComponent { ItemId = FindItemID(check.A.Key), SkinId = 0 },
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "7 7", OffsetMax = "-7 -7"}
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Panel5" + $".Resourse{check.B}",
                        Components =
                        {
                            new CuiRawImageComponent { Png = GetImage($"{check.A.Key}") },
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "7 7", OffsetMax = "-7 -7"}
                        }
                    });
                }

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Panel5" + $".Resourse{check.B}",
                    Components =
                    {
                        new CuiTextComponent { Text = $"{check.A.Value.Farm}", Color = "1 1 1 0.3", Align = TextAnchor.MiddleRight, FontSize = 10, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0 0.7", AnchorMax = $"0.9 0.93" },
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = $"UI_CLANS OpenChoiseResourse" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                }, Layer + ".Panel5" + $".Resourse{check.B}");
            }
            #endregion

            MainClanList(ref container, player, clan);

            for (Int32 x = 0; x < 6; x++) CuiHelper.DestroyUi(player, Layer + $".Panel{x}");
        }

        private void MainClanList(ref CuiElementContainer container, BasePlayer player, ClanData clan, Int32 page = 0)
        {
            var playersList = clan.Members.OrderByDescending(p => p.Value.Point);
            Int32 i = 0;

            float width = 0.91f, height = 0.2f, startxBox = 0.005f, startyBox = 0.99f - height, xmin = startxBox, ymin = startyBox;
            for (Int32 y = 0; y < 4; y++)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" }
                }, Layer + ".Panel4", Layer + ".Panel4" + $".Line{y}");

                xmin += width;
                if (xmin + width >= 0)
                {
                    xmin = startxBox;
                    ymin -= height + 0.055f;
                }
            }

            foreach (KeyValuePair<UInt64, ClanData.PlayerData> member in playersList.Skip(4 * page).Take(4))
            {
                container.Add(new CuiLabel
                {
                    Text = { Text = $"{i + 1 + (page * 4)}.", Color = "1 1 1 0.3", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft },
                    RectTransform = { AnchorMin = $"0.025 0", AnchorMax = $"0.1 1" },
                }, Layer + ".Panel4" + $".Line{i}");

                IPlayer memberPlayer = covalence.Players.FindPlayerById(member.Key.ToString());
                String playerName = memberPlayer != null ? memberPlayer.Name : $"Игрок {member.Key}";

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{playerName}", Font = "robotocondensed-regular.ttf", FontSize = 11, Color = "1 1 1 0.3", Align = TextAnchor.MiddleLeft },
                    RectTransform = { AnchorMin = $"0.06 0", AnchorMax = $"1 1" },
                }, Layer + ".Panel4" + $".Line{i}");

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{member.Value.Point}", Color = "1 1 1 0.3", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = $"-0.095 0", AnchorMax = $"1 1" },
                }, Layer + ".Panel4" + $".Line{i}");

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{member.Value.Kill}", Color = "1 1 1 0.3", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = $"0.45 0", AnchorMax = $"0.78 1" },
                }, Layer + ".Panel4" + $".Line{i}");

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{member.Value.Death}", Color = "1 1 1 0.3", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = $"0.675 0", AnchorMax = $"0.9 1" },
                }, Layer + ".Panel4" + $".Line{i}");

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{member.Value.Kill}/{member.Value.Death}", Color = "1 1 1 0.3", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = $"0.85 0", AnchorMax = $"1 1" },
                }, Layer + ".Panel4" + $".Line{i}");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.25 0.25 0.23 0", Command = $"UI_CLANS clanstats {member.Key}" },
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                }, Layer + ".Panel4" + $".Line{i}");

                i++;
            }

            #region [Buttons]
            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = page > 0 ? $"UI_CLANS clanpage {page - 1}" : "" },
                Text = { Text = "", Color = "1 1 1 1", FontSize = 22, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = $"0.935 0.68", AnchorMax = $"0.995 1.25", OffsetMax = "0 0" },
            }, Layer + ".Panel4", Layer + ".Panel4" + ".Previus");

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = playersList.Skip(4 * (page + 1)).Count() > 0 ? $"UI_CLANS clanpage {page + 1}" : "" },
                Text = { Text = "", Color = "1 1 1 1", FontSize = 22, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = $"0.935 0.03", AnchorMax = $"0.995 0.6", OffsetMax = "0 0" },
            }, Layer + ".Panel4", Layer + ".Panel4" + ".Next");
            #endregion

            // Кнопка выхода из клана (только для участников, не для лидера)
            if (!clan.IsOwner(player.userID))
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.393 0.743", AnchorMax = "0.463 0.775"  },
                    Button = { Color = "0 0 0 0", Command = "chat.say /clan leave" },
                    Text = { Text = "Выйти из Кклана", Color = "1 1 1 0.5", FontSize = 11, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                }, Layer);
            }
            // Кнопка удаления клана (только для лидера)
            else
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.393 0.743", AnchorMax = "0.463 0.775" },
                    Button = { Color = "0 0 0 0", Command = "UI_CLANS disbandClan" },
                    Text = { Text = "Удалить клан", Color = "1 1 1 0.5", FontSize = 11, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                }, Layer);
            }

            CuiHelper.DestroyUi(player, Layer + ".Panel4" + ".Previus");
            CuiHelper.DestroyUi(player, Layer + ".Panel4" + ".Text");
            CuiHelper.DestroyUi(player, Layer + ".Panel4" + ".Next");
            for (Int32 x = 0; x < 5; x++) CuiHelper.DestroyUi(player, Layer + ".Panel4" + $".Line{x}");
        }

        private void CreateClanInterface(ref CuiElementContainer container, BasePlayer player)
        {
            // Основной фон интерфейса с изображением
            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".CreateBackground",
                Components =
                {
                    new CuiRawImageComponent { Png = _imageUI.GetImage("FON_CLAN") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            // Заголовок
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.3 0.7", AnchorMax = "0.7 0.75" },
                Text = { Text = "СОЗДАНИЕ КЛАНА", Color = "1 1 1 1", FontSize = 18, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
            }, Layer);

            // Описание
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.25 0.6", AnchorMax = "0.75 0.68" },
                Text = { Text = $"Введите название клана (от {_config.Tags.TagMin} до {_config.Tags.TagMax} символов)", Color = "1 1 1 0.8", FontSize = 12, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }
            }, Layer);

            // Поле ввода названия клана
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.3 0.5", AnchorMax = "0.7 0.55" },
                Image = { Color = "0.2 0.2 0.2 0.8" }
            }, Layer, Layer + ".InputPanel");

            container.Add(new CuiElement
            {
                Parent = Layer + ".InputPanel",
                Components =
                {
                    new CuiInputFieldComponent 
                    { 
                        Text = "", 
                        FontSize = 14, 
                        Font = "robotocondensed-regular.ttf", 
                        Align = TextAnchor.MiddleCenter, 
                        Color = "1 1 1 1",
                        CharsLimit = _config.Tags.TagMax,
                        Command = "UI_CLANS createClanWithName"
                    },
                    new CuiRectTransformComponent { AnchorMin = "0.05 0", AnchorMax = "0.95 1" }
                }
            });

            // Инструкция по использованию
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.25 0.43", AnchorMax = "0.75 0.48" },
                Text = { Text = "Введите название и нажмите ENTER", Color = "0.8 0.8 0.8 1", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }
            }, Layer);

            // Альтернативная кнопка для создания через чат
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.35 0.35", AnchorMax = "0.65 0.4" },
                Button = { Color = "0.4 0.4 0.4 0.8", Command = "clan.create.ui" },
                Text = { Text = "СОЗДАТЬ ЧЕРЕЗ ЧАТ", Color = "1 1 1 1", FontSize = 12, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }
            }, Layer);

            // Информация о правилах
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.25 0.2", AnchorMax = "0.75 0.32" },
                Text = { Text = "ПРАВИЛА СОЗДАНИЯ КЛАНА:\n• Название клана должно быть уникальным\n• Запрещены нецензурные слова\n• После создания название нельзя изменить", Color = "1 1 1 0.6", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft }
            }, Layer);
        }

        private void ClanPlayerStats(BasePlayer player, ClanData clan, UInt64 playerID)
        {
            if (clan == null || !clan.Members.ContainsKey(playerID)) return;

            CuiElementContainer container = new CuiElementContainer();

            ClanData.PlayerData Member = clan.Members[playerID];
            if (Member == null) return;

            #region [Parrent]
            container.Add(new CuiElement
            {
                Name = Layer,
                Parent = ".Mains",
                Components =
                {
                    new CuiRawImageComponent { Png = _imageUI.GetImage("FON_CLAN_PLAYER_STAT") },
                    new CuiRectTransformComponent { AnchorMin = "-0.315 -0.27", AnchorMax = "1.3 1.275", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
                Button = { Close = Layer, Command = "clan.closeui", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer);
            #endregion

            #region [Main-Gui]
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.333 0.63", AnchorMax = "0.52 0.73", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Panel1");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.525 0.46", AnchorMax = "0.677 0.73", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Panel2");

            #endregion

            #region [Text]
            #endregion

            #region [Resourse]
            float width = 0.273f, height = 0.27f, startxBox = 0.046f, startyBox = 0.956f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in Member.GatherMember.Select((y, t) => new { A = y, B = t }).Take(9))
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" },
                }, Layer + ".Panel2", Layer + ".Panel2" + $".Resourse{check.B}");
                xmin += width + 0.047f;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + 0.045f;
                }

                if (FindItemID(check.A.Key) != 0)
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Panel2" + $".Resourse{check.B}",
                        Components =
                        {
                            new CuiImageComponent { ItemId = FindItemID(check.A.Key), SkinId = 0 },
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "7 7", OffsetMax = "-7 -7"}
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Panel2" + $".Resourse{check.B}",
                        Components =
                        {
                            new CuiRawImageComponent { Png = GetImage($"{check.A.Key}") },
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "7 7", OffsetMax = "-7 -7"}
                        }
                    });
                }

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Panel2" + $".Resourse{check.B}",
                    Components =
                    {
                        new CuiTextComponent { Text = $"{clan.GatherClan[check.A.Key].Need}", Color = "1 1 1 0.3", Align = TextAnchor.MiddleRight, FontSize = 10, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0 0.7", AnchorMax = $"0.9 0.93" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Panel2" + $".Resourse{check.B}",
                    Components =
                    {
                        new CuiTextComponent { Text = $"{check.A.Value.ToString("N3", CultureInfo.GetCultureInfo("ru-RU")).Replace(",000", "")}", Color = "1 1 1 1", Align = TextAnchor.MiddleRight, FontSize = 10, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0 0.05", AnchorMax = $"0.9 0.3" },
                    }
                });
            }
            #endregion

            #region [Avatar]
            container.Add(new CuiElement
            {
                Parent = Layer + ".Panel1",
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage($"{Name}.Avatar{playerID}") },
                    new CuiRectTransformComponent{ AnchorMin = "0.04 0", AnchorMax =  "0.27 0.77" },
                }
            });
            #endregion

            #region [Info]
            IPlayer covPlayer = covalence.Players.FindPlayer(playerID.ToString());
            Dictionary<String, String> StatsInfo = new Dictionary<String, String>()
            {
                { $"{covPlayer.Name}", "" },
                { $"SteamID:", $"{covPlayer.Id}" },
                { $"Активность:", $"{FormatShortTime(TimeSpan.FromSeconds(Member.Online), player.UserIDString)}" },
                { $"Общая выполненная норма:", $"{clan.GetPercentPlayer(playerID)}" },
            };

            foreach (var check in StatsInfo.Select((u, t) => new { A = u, B = t }))
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{0.34 + check.B * 0 - Math.Floor((float) check.B / 1) * 1 * 0} {0.66 - Math.Floor((float) check.B/ 1) * 0.25}",
                                        AnchorMax = $"{0.95 + check.B * 0 - Math.Floor((float) check.B / 1) * 1 * 0} {0.84 - Math.Floor((float) check.B / 1) * 0.25}", },
                    Image = { Color = "0 0 0 0" }
                }, Layer + ".Panel1", Layer + ".Panel1" + $".Info{check.B}");

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Panel1" + $".Info{check.B}",
                    Components =
                    {
                        new CuiTextComponent { Text = $"{check.A.Key}", Color = "1 1 1 0.3", Align = TextAnchor.MiddleLeft, FontSize = 9, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0.01 0", AnchorMax = $"0.85 1" },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Panel1" + $".Info{check.B}",
                    Components =
                    {
                        new CuiTextComponent { Text = $"{check.A.Value}", Color = "1 1 1 0.7", Align = TextAnchor.MiddleRight, FontSize = 8, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0 0", AnchorMax = $"0.985 1" },
                    }
                });
            }
            #endregion

            #region [Button]
            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = !clan.IsModerator(player.userID) ? "" : $"UI_CLANS kick {playerID}" },
                Text = { Text = $"     Выгнать из клана", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10, Color = "1 1 1 0.3" },
                RectTransform = { AnchorMin = $"0.333 0.553", AnchorMax = $"0.513 0.583" },
            }, Layer);

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = clan.Moderators.Contains(playerID) ? $"UI_CLANS HandlerModerator DemoteModerator {playerID}" : $"UI_CLANS HandlerModerator PromoteModerator {playerID}" },
                Text = { Text = clan.Moderators.Contains(playerID) ? $"     Забрать модератора" : $"     Назначить модератором", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10, Color = "1 1 1 0.3" },
                RectTransform = { AnchorMin = $"0.333 0.513", AnchorMax = $"0.513 0.542" },
            }, Layer);

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = $"UI_CLANS changeLeader {playerID}" },
                Text = { Text = $"     Назначить лидером", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10, Color = "1 1 1 0.3" },
                RectTransform = { AnchorMin = $"0.333 0.473", AnchorMax = $"0.513 0.503" },
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.327 0.74", AnchorMax = "0.4 0.778" },
                Button = { Command = "UI_CLANS returnClanMain", Color = "0 0 0 0" },
                Text = { Text = $"             {GetLang("RETURN", player.UserIDString)}", Color = "1 1 1 0.6", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft }
            }, Layer);
            #endregion

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        private void CreateChoiseSkins(BasePlayer player, ClanData clan)
        {
            CuiElementContainer container = new CuiElementContainer();

            #region [Parrent]
            container.Add(new CuiElement
            {
                Name = Layer,
                Parent = ".Mains",
                Components =
                {
                    new CuiRawImageComponent { Png = _imageUI.GetImage("FON_SKIN_EDIT") },
                    new CuiRectTransformComponent { AnchorMin = "-0.315 -0.27", AnchorMax = "1.3 1.275", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
                Button = { Close = Layer, Command = "clan.closeui", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer);
            #endregion

            #region [Main-Gui]
            container.Add(new CuiElement
            {
                Name = Layer + "Panel1",
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent { Color = "0.3773585 0.3755785 0.3755785 0", Material = "assets/icons/greyout.mat" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-160 -149", OffsetMax = "551 151"}
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.26 0.44", AnchorMax = "0.412 0.71" },
                Button = { Color = "0 0 0 0" },
            }, Layer, Layer + ".Panel2");
            #endregion

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.26 0.74", AnchorMax = "0.335 0.778" },
                Button = { Command = "UI_CLANS returnClanMain", Color = "0 0 0 0" },
                Text = { Text = $"", Color = "1 1 1 0.6", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft }
            }, Layer);

            #region [Wear]
            float width1 = 0.279f, height1 = 0.279f, startxBox1 = 0.048f, startyBox1 = 0.942f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
            foreach (var check in clan.SkinList.Select((y, t) => new { A = y, B = t }).Take(6))
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" }
                }, Layer + ".Panel2", Layer + "Panel2" + $".Wear{check.B}");

                xmin1 += width1 + 0.033f;
                if (xmin1 + width1 >= 1)
                {
                    xmin1 = startxBox1;
                    ymin1 -= height1 + 0.033f;
                }

                container.Add(new CuiElement
                {
                    Parent = Layer + "Panel2" + $".Wear{check.B}",
                    Components =
                    {
                        new CuiImageComponent { ItemId = FindItemID(check.A.Key), SkinId = check.A.Value },
                        new CuiRectTransformComponent { AnchorMin = "0.025 0.025", AnchorMax = "0.975 0.975", OffsetMin = "5 5", OffsetMax = "-5 -5" }
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = $"UI_CLANS SkinsLayer {check.A.Key} 0" },
                    Text = { Text = "" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                }, Layer + "Panel2" + $".Wear{check.B}");
            }
            #endregion

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        private void CreateSkinsLayer(BasePlayer player, String shortName, Int32 page = 0)
        {
            CuiElementContainer container = new CuiElementContainer();
            
            // Проверяем, существует ли ключ в словаре скинов
            if (!_skinsList.ContainsKey(shortName) || _skinsList[shortName] == null || _skinsList[shortName].Count == 0)
            {
                player.ChatMessage(GetLang("NO_SKINS_AVAILABLE", player.UserIDString));
                return;
            }
            
            if (!_skinsUsedList.ContainsKey(shortName))
            {
                _skinsUsedList[shortName] = new List<UInt64>();
            }
            
            IEnumerable<UInt64> activeSkins = _skinsList[shortName].Where(p => !_skinsUsedList[shortName].Contains(p));

            #region [Parrent]
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.4167 0.353", AnchorMax = "0.71 0.71" },
                Button = { Color = "0 0 0 0" },
            }, Layer, Layer + "Panel2" + ".Skins");
            #endregion

            #region [Buttons]
            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = page != 0 ? $"UI_CLANS SkinsLayer {shortName} {page - 1}" : "" },
                Text = { Text = "", Color = "1 1 1 1", FontSize = 22, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = $"1.004 0.51", AnchorMax = $"1.073 0.965" },
            }, Layer + "Panel2" + ".Skins");

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = activeSkins.Count() > (page + 1) * 32 ? $"UI_CLANS SkinsLayer {shortName} {page + 1}" : "" },
                Text = { Text = "", Color = "1 1 1 1", FontSize = 22, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = $"1.004 0.035", AnchorMax = $"1.073 0.48" },
            }, Layer + "Panel2" + ".Skins");
            #endregion

            #region [Wear]
            float width = 0.14f, height = 0.205f, startxBox = 0.028f, startyBox = 0.951f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in activeSkins.Select((i, t) => new { A = i, B = t - page * 24 }).Skip(page * 24).Take(24))
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, Layer + "Panel2" + ".Skins", Layer + "Panel2" + ".Skins" + $".Wear{check.B}");

                xmin += width + 0.0227f;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + 0.031f;
                }

                container.Add(new CuiElement
                {
                    Parent = Layer + "Panel2" + ".Skins" + $".Wear{check.B}",
                    Components =
                    {
                        new CuiImageComponent { ItemId = FindItemID(shortName), SkinId = check.A },
                        new CuiRectTransformComponent { AnchorMin = "0.025 0.025", AnchorMax = "0.975 0.975", OffsetMin = "5 5", OffsetMax = "-5 -5" }
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = $"UI_CLANS SelectSkin {shortName} {check.A}" },
                    Text = { Text = "" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                }, Layer + "Panel2" + ".Skins" + $".Wear{check.B}");
            }
            #endregion

            CuiHelper.DestroyUi(player, Layer + "Panel2" + ".Text");
            CuiHelper.DestroyUi(player, Layer + "Panel2" + ".Skins");
            CuiHelper.AddUi(player, container);
        }

        private void CreateChoiseResourse(BasePlayer player, ClanData clan)
        {
            CuiElementContainer container = new CuiElementContainer();

            #region [Parrent]
            container.Add(new CuiElement
            {
                Name = Layer,
                Parent = ".Mains",
                Components =
                {
                    new CuiRawImageComponent { Png = _imageUI.GetImage("FON_RESOURCE_EDIT") },
                    new CuiRectTransformComponent { AnchorMin = "-0.315 -0.27", AnchorMax = "1.3 1.275", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
                Button = { Close = Layer, Command = "clan.closeui", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer);
            #endregion

            #region [Main-Gui]
            container.Add(new CuiElement
            {
                Name = Layer + ".Panel1",
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent { Color = "0.3773585 0.3755785 0.3755785 0", Material = "assets/icons/greyout.mat" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-160 -149", OffsetMax = "551 151"}
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.4167 0.353", AnchorMax = "0.71 0.71" },
                Button = { Color = "0 0 0 0" },
            }, Layer, Layer + ".Panel1");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.26 0.44", AnchorMax = "0.412 0.71" },
                Button = { Color = "0 0 0 0" },
            }, Layer, Layer + ".Panel2");
            #endregion

            #region [Text]
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.26 0.74", AnchorMax = "0.335 0.778" },
                Button = { Command = "UI_CLANS returnClanMain", Color = "0 0 0 0" },
                Text = { Text = $"", Color = "1 1 1 0.6", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft }
            }, Layer);
            #endregion

            float width = 0.14f, height = 0.205f, startxBox = 0.028f, startyBox = 0.951f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in clan.GatherClan.Select((i, t) => new { A = i, B = t }))
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, Layer + ".Panel1", Layer + ".Panel1" + $".BResourse{check.B}");

                xmin += width + 0.0227f;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + 0.031f;
                }

                if (FindItemID(check.A.Key) != 0)
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Panel1" + $".BResourse{check.B}",
                        Components =
                                    {
                                        new CuiImageComponent { ItemId = FindItemID(check.A.Key), SkinId = 0 },
                                        new CuiRectTransformComponent {AnchorMin = "0.025 0.025", AnchorMax = "0.975 0.975", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                                    }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Panel1" + $".BResourse{check.B}",
                        Components =
                                    {
                                        new CuiRawImageComponent { Png = GetImage(check.A.Key) },
                                        new CuiRectTransformComponent {AnchorMin = "0.025 0.025", AnchorMax = "0.975 0.975", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                                    }
                    });
                }

                if (clan.IsModerator(player.userID))
                {
                    container.Add(new CuiButton
                    {
                        Button = { Color = "0 0 0 0", Command = $"UI_CLANS SelectResourse {check.A.Key}" },
                        Text = { Text = "" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    }, Layer + ".Panel1" + $".BResourse{check.B}");
                }
            }

            #region [Resourse-Need]
            float width1 = 0.279f, height1 = 0.279f, startxBox1 = 0.048f, startyBox1 = 0.942f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
            foreach (var check in clan.GatherClan.Select((i, t) => new { A = i, B = t }))
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" }
                }, Layer + ".Panel2", Layer + ".Panel2" + $".Resourse{check.B}");

                xmin1 += width1 + 0.033f;
                if (xmin1 + width1 >= 1)
                {
                    xmin1 = startxBox1;
                    ymin1 -= height1 + 0.033f;
                }

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Panel2" + $".Resourse{check.B}",
                    Components =
                    {
                        new CuiTextComponent { Text = $"x{check.A.Value.Need}", Color = "1 1 1 0.3", Align = TextAnchor.MiddleCenter, FontSize = 11, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0 -0.3", AnchorMax = $"1 0.6" },
                    }
                });

                if (FindItemID(check.A.Key) != 0)
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Panel2" + $".Resourse{check.B}",
                        Components =
                        {
                            new CuiImageComponent { ItemId = FindItemID(check.A.Key), SkinId = 0 },
                            new CuiRectTransformComponent {AnchorMin = "0.025 0.025", AnchorMax = "0.975 0.975", OffsetMin = "7 7", OffsetMax = "-7 -7"}
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Panel2" + $".Resourse{check.B}",
                        Components =
                        {
                            new CuiRawImageComponent { Png = GetImage(check.A.Key) },
                            new CuiRectTransformComponent {AnchorMin = "0.025 0.025", AnchorMax = "0.975 0.975", OffsetMin = "7 7", OffsetMax = "-7 -7"}
                        }
                    });
                }
            }
            #endregion

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        private void CreateSelectResourse(BasePlayer player, String shortName)
        {
            CuiElementContainer container = new CuiElementContainer();

            #region [Parrent]
            container.Add(new CuiElement
            {
                Name = Layer + ".ChangeResourse",
                Parent = ".Mains",
                Components =
                {
                    new CuiRawImageComponent { Png = _imageUI.GetImage("FON_CLAN_NORMA") },
                    new CuiRectTransformComponent { AnchorMin = "0.28 0.35", AnchorMax = "0.72 0.65", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.932 0.804", AnchorMax = "1 1" },
                Button = { Close = Layer + ".ChangeResourse", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer + ".ChangeResourse");
            #endregion

            #region [Main-Gui]
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.8", OffsetMax = "" },
                Image = { Color = "0 0 0 0" }
            }, Layer + ".ChangeResourse", Layer + ".ChangeResourse" + ".Panel");
            #endregion

            #region [Text]
            container.Add(new CuiElement
            {
                Parent = Layer + ".ChangeResourse" + ".Panel",
                Components =
                {
                    new CuiTextComponent { Text = $"Введите количество", Color = "1 1 1 0.3", FontSize = 12, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter},
                    new CuiRectTransformComponent { AnchorMin = "0 0.75", AnchorMax = "1 1" }
                }
            });

            if (_config.LimitSettings.NeedLimit.ContainsKey(shortName))
            {
                container.Add(new CuiElement
                {
                    Parent = Layer + ".ChangeResourse" + ".Panel",
                    Components =
                    {
                        new CuiTextComponent { Text = $"{GetLang("ALLOW_MAXIMUM", player.UserIDString)} {_config.LimitSettings.NeedLimit[shortName].ToString("N3", CultureInfo.GetCultureInfo("ru-RU")).Replace(",000", "")}", Color = "1 1 1 0.3", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter},
                        new CuiRectTransformComponent { AnchorMin = "0.1 0.3", AnchorMax = "0.9 1" },

                    }
                });
            }
            #endregion

            #region [Input]
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.21 0.22", AnchorMax = "0.79 0.45" },
                Image = { Color = "0 0 0 0" }
            }, Layer + ".ChangeResourse" + ".Panel", Layer + ".ChangeResourse" + ".Panel" + ".inputPanel");

            container.Add(new CuiElement
            {
                Parent = Layer + ".ChangeResourse" + ".Panel" + ".inputPanel",
                Components =
                {
                    new CuiTextComponent { Text = $"Введите количество и нажмите ENTER", Color = "1 1 1 0.1", FontSize = 8, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                }
            });

            container.Add(new CuiElement()
            {
                Parent = Layer + ".ChangeResourse" + ".Panel" + ".inputPanel",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleCenter,
                        NeedsKeyboard = true,
                        CharsLimit = 12,
                        FontSize = 12,
                        Command = $"UI_CLANS ChangeResourseNeed {shortName} ",
                        Font = "robotocondensed-regular.ttf",
                        Text = "",
                        Color = "1 1 1 0.7"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1"
                    }
                }
            });
            #endregion

            CuiHelper.DestroyUi(player, Layer + ".ChangeResourse");
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region [Invites]
        private readonly List<Invite> _invites = new List<Invite>();

        private class Invite
        {
            public BasePlayer Inviter;

            public BasePlayer Recevier;

            public String ClanTag;
        }

        private void SendInvite(BasePlayer inviter, UInt64 target)
        {
            ClanData clan = FindClanByUser(inviter.userID);
            if (clan == null) return;

            BasePlayer recevier = BasePlayer.FindByID(target);
            if (recevier == null) return;

            if (!clan.IsModerator(inviter.userID))
            {
                inviter.ChatMessage(GetLang("NO_MODERATOR_AND_LEADER", inviter.UserIDString));
                return;
            }

            if (clan.Members.Count >= _config.LimitSettings.MemberLimit)
            {
                inviter.ChatMessage(GetLang("MAX_COUNT_CLAN", inviter.UserIDString));
                return;
            }

            ClanData targetClan = FindClanByUser(target);
            if (targetClan != null)
            {
                inviter.ChatMessage(GetLang("ALREADY_HAS_CLAN", inviter.UserIDString));
                return;
            }

            if (_invites.Exists(invite => invite.Recevier.userID == target && invite.ClanTag == clan.ClanTag))
            {
                inviter.ChatMessage(GetLang("ALREAD_YOU_CLAN", inviter.UserIDString));
                return;
            }

            if (_config.AutoTeamCreation)
            {
                RelationshipManager.PlayerTeam team = clan.FindTeam();
                if (!team.invites.Contains(recevier.userID)) team.SendInvite(recevier);
            }

            recevier.ChatMessage(GetLang("RECEVIER_INVITE", recevier.UserIDString, inviter.IPlayer.Name, clan.ClanTag));
            inviter.ChatMessage(GetLang("INVITER_INVITE", inviter.UserIDString, recevier.displayName));

            _invites.Add(new Invite
            {
                Inviter = inviter,
                Recevier = recevier,
                ClanTag = clan.ClanTag
            });
            timer.Once(15f, () => RemoveInvite(clan.ClanTag, recevier.userID));
        }

        private void AcceptInvite(BasePlayer player)
        {
            ClanData clan = FindClanByUser(player.userID);
            if (clan != null)
            {
                player.ChatMessage(GetLang("ALEADY_HAVE_CLAN", player.UserIDString));
                return;
            }

            Invite invite = _invites.Find(x => x.Recevier.userID == player.userID);
            if (invite == null)
            {
                player.ChatMessage(GetLang("NO_INVITE", player.UserIDString));
                return;
            }

            BasePlayer inviter = invite.Inviter;
            if (inviter == null) return;

            if (player.userID == inviter.userID)
            {
                player.ChatMessage(GetLang("NO_ADD_YOUR", player.UserIDString));
                return;
            }

            clan = FindClanByTag(invite.ClanTag);
            if (clan == null)
            {
                _invites.Remove(invite);
                return;
            }

            if (clan.Members.Count >= _config.LimitSettings.MemberLimit)
            {
                player.ChatMessage(GetLang("MAX_COUNT_CLAN", player.UserIDString));
                return;
            }

            clan.Join(player);
            inviter.ChatMessage(GetLang("INVITER_INVITE_ACCEPT", inviter.UserIDString, player.IPlayer.Name));
            player.ChatMessage(GetLang("RECEVIER_INVITE_ACCEPT", inviter.UserIDString, clan.ClanTag));
            player.ClearPendingInvite();
            _invites.Remove(invite);
        }
        #endregion

        #region [ConsoleCommand || ChatCommand]
        [ConsoleCommand("clan.closeui")]
        private void CmdCloseUI(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, Layer + ".Main");
            CuiHelper.DestroyUi(player, Layer + ".Main1");
            CuiHelper.DestroyUi(player, Layer + ".Ctop");
            CuiHelper.DestroyUi(player, Layer + ".Description");
            CuiHelper.DestroyUi(player, Layer + ".Info");
            CuiHelper.DestroyUi(player, Layer + ".Info1");
            CuiHelper.DestroyUi(player, Layer + ".Info2");
            CuiHelper.DestroyUi(player, Layer + ".Norma");
            CuiHelper.DestroyUi(player, Layer + ".ChangeResourse");
            for (Int32 x = 0; x < 6; x++) CuiHelper.DestroyUi(player, Layer + $".Panel{x}");

            player.Command("cursor.lock", "false");
            player.Command("cursor.visible", "false");
            player.Command("cursor.unlock");
            player.Command("player.look", "1");
            player.Command("player.move", "1");
            player.Command("player.attack", "1");
            player.Command("player.jump", "1");
            player.Command("player.sprint", "1");
            player.Command("player.duck", "1");
        }

        private void CmdClans(IPlayer cov, String command, String[] args)
        {
            BasePlayer player = cov?.Object as BasePlayer;
            if (player == null) return;

            if (_cooldownPlayer.ContainsKey(player.userID) && _cooldownPlayer[player.userID].Subtract(DateTime.Now).TotalSeconds >= 0)
            {
                player.ChatMessage(GetLang("NO_FLOOD", player.UserIDString));
                return;
            }

            _cooldownPlayer[player.userID] = DateTime.Now.AddSeconds(2f);

            if (args.Length == 0)
            {
                ClanData clan = FindClanByUser(player.userID);
                if (clan == null)
                {
                    player.ChatMessage(GetLang("CLAN_HELP", player.UserIDString));
                    return;
                }

                ClanMainUi(player);
                return;
            }

            switch (args[0])
            {
                case "create":
                    {
                        if (args.Length < 2)
                        {
                            player.ChatMessage(GetLang("CLAN_CREATE", player.UserIDString, _config.Tags.TagMax));
                            return;
                        }

                        if (FindClanByUser(player.userID) != null)
                        {
                            player.ChatMessage(GetLang("ALEADY_HAVE_CLAN", player.UserIDString));
                            return;
                        }

                        String clanTag = String.Join(" ", args.Skip(1));
                        if (String.IsNullOrEmpty(clanTag) || clanTag.Length < _config.Tags.TagMin || clanTag.Length > _config.Tags.TagMax)
                        {
                            player.ChatMessage(GetLang("CLAN_CREATE", player.UserIDString, _config.Tags.TagMax));
                            return;
                        }

                        clanTag = clanTag.Replace(" ", "");

                        if (_config.Tags.BlockedWords.Exists(word => clanTag.Contains(word, CompareOptions.OrdinalIgnoreCase)))
                        {
                            player.ChatMessage(GetLang("CLAN_CREATE_BLOCKED_NAME", player.UserIDString));
                            return;
                        }

                        ClanData clan = FindClanByTag(clanTag);
                        if (clan != null)
                        {
                            player.ChatMessage(GetLang("CLAN_CREATE_TAG", player.UserIDString));
                            return;
                        }

                        clan = ClanData.CreateNewClan(clanTag, player);
                        if (clan == null) return;

                        player.ChatMessage(GetLang("SUCCEFULL_CREATE", player.UserIDString, clanTag));
                        break;
                    }
                case "disband":
                    {
                        ClanData clan = FindClanByUser(player.userID);
                        if (clan == null)
                        {
                            player.ChatMessage(GetLang("NO_CLAN", player.UserIDString));
                            return;
                        }

                        if (!clan.IsOwner(player.userID))
                        {
                            player.ChatMessage(GetLang("NOT_LEADER", player.UserIDString));
                            return;
                        }

                        if (args.Length < 2)
                        {
                            player.ChatMessage(GetLang("NOT_DISBAND_YES", player.UserIDString));
                            return;
                        }

                        if (args[1] == "yes")
                        {
                            clan.Disband();
                            player.ChatMessage(GetLang("CLAN_DISBAND", player.UserIDString));
                            break;
                        }

                        player.ChatMessage(GetLang("NOT_DISBAND_YES", player.UserIDString));
                        break;
                    }
                case "leave":
                    {
                        ClanData clan = FindClanByUser(player.userID);
                        if (clan == null)
                        {
                            player.ChatMessage(GetLang("NO_CLAN", player.UserIDString));
                            return;
                        }

                        if (clan.IsOwner(player.userID))
                        {
                            player.ChatMessage(GetLang("IS_LEADER", player.UserIDString));
                            return;
                        }

                        clan.Kick(player.userID);
                        player.ChatMessage(GetLang("LEAVE_CLAN", player.UserIDString, clan.ClanTag));
                        break;
                    }
                case "cancel":
                    {
                        ClanData clan = FindClanByUser(player.userID);
                        if (clan != null)
                        {
                            player.ChatMessage(GetLang("ALEADY_HAVE_CLAN", player.UserIDString));
                            return;
                        }

                        Invite invite = _invites.Find(x => x.Recevier.userID == player.userID);
                        if (invite == null)
                        {
                            player.ChatMessage(GetLang("NO_INVITE", player.UserIDString));
                            return;
                        }

                        player.ChatMessage(GetLang("CANCEL_INVITE", player.UserIDString));
                        invite.Inviter.ChatMessage(GetLang("INVITER_CANCEL_INVITE", invite.Inviter.UserIDString, player.IPlayer.Name));
                        player.ClearPendingInvite();
                        _invites.Remove(invite);
                        break;
                    }
                case "invite":
                    {
                        ClanData clan = FindClanByUser(player.userID);
                        if (clan == null)
                        {
                            player.ChatMessage(GetLang("NO_CLAN", player.UserIDString));
                            return;
                        }

                        if (args.Length < 2)
                        {
                            player.ChatMessage(GetLang("INVITE_NO_ARGS", player.UserIDString));
                            return;
                        }

                        if (!clan.IsModerator(player.userID))
                        {
                            player.ChatMessage(GetLang("NO_MODERATOR_AND_LEADER", player.UserIDString));
                            return;
                        }

                        IPlayer target = covalence.Players.FindPlayer(args[1]);
                        if (target == null)
                        {
                            player.ChatMessage(GetLang("PLAYER_NOT_FOUND", player.UserIDString, args[1]));
                            return;
                        }

                        if (target.Id == player.UserIDString)
                        {
                            player.ChatMessage(GetLang("NO_ADD_YOUR", player.UserIDString));
                            return;
                        }

                        SendInvite(player, UInt64.Parse(target.Id));
                        break;
                    }
                case "kick":
                    {
                        ClanData clan = FindClanByUser(player.userID);
                        if (clan == null)
                        {
                            player.ChatMessage(GetLang("NO_CLAN", player.UserIDString));
                            return;
                        }

                        if (args.Length < 2)
                        {
                            player.ChatMessage(GetLang("KICK_NO_ARGS", player.UserIDString));
                            return;
                        }

                        IPlayer target = covalence.Players.FindPlayer(args[1]);
                        if (target == null)
                        {
                            player.ChatMessage(GetLang("PLAYER_NOT_FOUND", player.UserIDString, args[1]));
                            return;
                        }

                        if (!clan.IsModerator(player.userID))
                        {
                            player.ChatMessage(GetLang("NO_MODERATOR_AND_LEADER", player.UserIDString));
                            return;
                        }

                        if (clan.IsOwner(UInt64.Parse(target.Id)))
                        {
                            player.ChatMessage(GetLang("NO_KICK_LEADER", player.UserIDString));
                            return;
                        }

                        if (!clan.IsMember(target.Id))
                        {
                            player.ChatMessage(GetLang("MEMBERNOCONTAINSCLAN", player.UserIDString));
                            return;
                        }

                        clan.Kick(UInt64.Parse(target.Id));
                        player.ChatMessage(GetLang("KICK_PLAYER_SUCCEFULL", player.UserIDString, target.Name));

                        BasePlayer targetPlayer = target.Object as BasePlayer;
                        if (targetPlayer != null) targetPlayer.ChatMessage(GetLang("KICK_PLAYER", target.Id, clan.ClanTag));
                        break;
                    }
                case "ff":
                    {
                        ClanData clan = FindClanByUser(player.userID);
                        if (clan == null)
                        {
                            player.ChatMessage(GetLang("NO_CLAN", player.UserIDString));
                            return;
                        }

                        ClanData.PlayerData Member = clan.Members[player.userID];
                        if (Member == null) return;

                        if (Member.FriendlyFire)
                        {
                            player.ChatMessage(GetLang("FF_OFF", player.UserIDString));
                            Member.FriendlyFire = false;
                        }
                        else
                        {
                            player.ChatMessage(GetLang("FF_ON", player.UserIDString));
                            Member.FriendlyFire = true;
                        }
                        break;
                    }
                case "task":
                    {
                        ClanData clan = FindClanByUser(player.userID);
                        if (clan == null)
                        {
                            player.ChatMessage(GetLang("NO_CLAN", player.UserIDString));
                            return;
                        }

                        if (!clan.IsModerator(player.userID))
                        {
                            player.ChatMessage(GetLang("NO_MODERATOR_AND_LEADER", player.UserIDString));
                            return;
                        }

                        String Task = String.Join(" ", args.Skip(1));
                        if (String.IsNullOrWhiteSpace(Task))
                        {
                            player.ChatMessage(GetLang("INVITE_NO_ARGS", player.UserIDString));
                            return;
                        }

                        Task = Task.Trim();
                        if (Task.Length > 60)
                        {
                            player.ChatMessage(GetLang("CLAN_TASK_MORE", player.UserIDString));
                            return;
                        }

                        clan.Task = Task;

                        _playerToClan[player.userID] = clan;

                        SaveClans();

                        player.ChatMessage(GetLang("CLAN_TASK_SUCCEFULL", player.UserIDString));
                        ClanMainUi(player);
                        break;
                    }
                case "accept":
                    {
                        AcceptInvite(player);
                        break;
                    }
                default:
                    {
                        player.ChatMessage(GetLang("CLAN_HELP", player.UserIDString));
                        break;
                    }
            }
        }

        private void AdminCmdClans(IPlayer cov, String command, String[] args)
        {
            BasePlayer player = cov?.Object as BasePlayer;
            if (player != null && !player.IsAdmin) return;

            switch (args[0])
            {
                case "givepoint":
                    {
                        if (args.Length < 3) return;

                        Int32 Amount;
                        if (!Int32.TryParse(args[2], out Amount)) return;

                        IPlayer target = covalence.Players.FindPlayer(args[1]);
                        if (target == null) return;

                        ClanData clan = FindClanByUser(UInt64.Parse(target.Id));
                        if (clan == null) return;

                        clan.Members[UInt64.Parse(target.Id)].Point += Amount;
                        break;
                    }
                case "removepoint":
                    {
                        if (args.Length < 3) return;

                        Int32 Amount;
                        if (!Int32.TryParse(args[2], out Amount)) return;

                        IPlayer target = covalence.Players.FindPlayer(args[1]);
                        if (target == null) return;

                        ClanData clan = FindClanByUser(UInt64.Parse(target.Id));
                        if (clan == null) return;

                        clan.Members[UInt64.Parse(target.Id)].Point -= Amount;
                        break;
                    }
                case "leader":
                    {
                        if (args.Length < 2) return;

                        IPlayer target = covalence.Players.FindPlayer(args[1]);
                        if (target == null) return;

                        ClanData clan = FindClanByUser(UInt64.Parse(target.Id));
                        if (clan == null) return;

                        clan.SetLeader(UInt64.Parse(target.Id));
                        break;
                    }
            }
        }

        [ConsoleCommand("UI_CLANS")]
        private void ClanUIHandler(ConsoleSystem.Arg args)
        {
            BasePlayer player = args?.Player();
            if (player == null || !args.HasArgs()) return;

            switch (args.Args[0])
            {
                case "page":
                    {
                        Int32 page;
                        if (!Int32.TryParse(args.Args[1], out page)) return;
                        CuiElementContainer container = new CuiElementContainer();
                        TopClanList(ref container, player, page);
                        CuiHelper.AddUi(player, container);
                        break;
                    }
                case "info":
                    {
                        CuiHelper.DestroyUi(player, Layer + ".Main");
                        ClanData clan = FindClanByTag(args.Args[1]);
                        if (clan == null) return;
                        CuiElementContainer container = new CuiElementContainer();
                        ClanTopInfo(player, clan);
                        CuiHelper.AddUi(player, container);
                        break;
                    }
                case "returnClanTop":
                    {
                        Int32 page;
                        if (!Int32.TryParse(args.Args[1], out page)) return;
                        CuiElementContainer container = new CuiElementContainer();
                        MainUi(player);
                        CuiHelper.AddUi(player, container);
                        break;
                    }
                case "infopage":
                    {
                        ClanData clan = FindClanByTag(args.Args[1]);
                        if (clan == null) return;

                        Int32 page;
                        if (!Int32.TryParse(args.Args[2], out page)) return;

                        CuiElementContainer container = new CuiElementContainer();
                        InfoClanList(ref container, clan, player, page);
                        CuiHelper.AddUi(player, container);
                        break;
                    }
                case "clanpage":
                    {
                        ClanData clan = FindClanByUser(player.userID);
                        if (clan == null) return;

                        Int32 page;
                        if (!Int32.TryParse(args.Args[1], out page)) return;

                        CuiElementContainer container = new CuiElementContainer();
                        MainClanList(ref container, player, clan, page);
                        CuiHelper.AddUi(player, container);
                        break;
                    }
                case "clanstats":
                    {
                        ClanData clan = FindClanByUser(player.userID);
                        if (clan == null) return;

                        UInt64 playerID;
                        if (!UInt64.TryParse(args.Args[1], out playerID)) return;

                        ClanPlayerStats(player, clan, playerID);
                        break;
                    }
                case "returnClanMain":
                    {
                        ClanMainUi(player);
                        break;
                    }
                case "changeLeader":
                    {
                        UInt64 playerID;
                        if (!UInt64.TryParse(args.Args[1], out playerID) || player.userID == playerID) return;

                        ClanData clan = FindClanByUser(player.userID);
                        if (clan == null) return;

                        if (!clan.IsOwner(player.userID))
                        {
                            player.ChatMessage(GetLang("NOT_LEADER", player.UserIDString));
                            return;
                        }

                        clan.SetLeader(playerID);
                        player.ChatMessage(GetLang("CHANGE_LEADER_SUCCEFULL", player.UserIDString));
                        ClanMainUi(player);
                        break;
                    }
                case "HandlerModerator":
                    {
                        switch (args.Args[1])
                        {
                            case "PromoteModerator":
                                {
                                    UInt64 playerID;
                                    if (!UInt64.TryParse(args.Args[2], out playerID) || player.userID == playerID) return;

                                    ClanData clan = FindClanByUser(player.userID);
                                    if (clan == null || !clan.Members.ContainsKey(playerID)) return;

                                    if (!clan.IsOwner(player.userID))
                                    {
                                        player.ChatMessage(GetLang("NOT_LEADER", player.UserIDString));
                                        return;
                                    }

                                    if (clan.IsModerator(playerID))
                                    {
                                        player.ChatMessage(GetLang("ALREADY_MODERATOR", player.UserIDString));
                                        return;
                                    }

                                    if (clan.Moderators.Count >= _config.LimitSettings.ModeratorLimit)
                                    {
                                        player.ChatMessage(GetLang("MAX_COUNT_MODERATOR", player.UserIDString));
                                        return;
                                    }

                                    clan.Moderators.Add(playerID);
                                    player.ChatMessage(GetLang("PROMOTE_MODERATOR", player.UserIDString));
                                    ClanPlayerStats(player, clan, playerID);
                                    break;
                                }
                            case "DemoteModerator":
                                {
                                    UInt64 playerID;
                                    if (!UInt64.TryParse(args.Args[2], out playerID) || player.userID == playerID) return;

                                    ClanData clan = FindClanByUser(player.userID);
                                    if (clan == null || !clan.Members.ContainsKey(playerID)) return;

                                    if (!clan.IsOwner(player.userID))
                                    {
                                        player.ChatMessage(GetLang("NOT_LEADER", player.UserIDString));
                                        return;
                                    }

                                    if (!clan.IsModerator(playerID))
                                    {
                                        player.ChatMessage(GetLang("NOT_MODERATOR", player.UserIDString));
                                        return;
                                    }

                                    clan.Moderators.Remove(playerID);
                                    player.ChatMessage(GetLang("SUCCEFULL_DEMOTE_MODERATOR", player.UserIDString));
                                    ClanPlayerStats(player, clan, playerID);
                                    break;
                                }
                        }
                        break;
                    }
                case "kick":
                    {
                        UInt64 playerID;
                        if (!UInt64.TryParse(args.Args[1], out playerID) || player.userID == playerID) return;

                        ClanData clan = FindClanByUser(player.userID);
                        if (clan == null) return;

                        IPlayer target = covalence.Players.FindPlayer(playerID.ToString());
                        if (target == null)
                        {
                            player.ChatMessage(GetLang("PLAYER_NOT_FOUNDED", player.UserIDString));
                            return;
                        }

                        if (!clan.IsModerator(player.userID))
                        {
                            player.ChatMessage(GetLang("NO_MODERATOR_AND_LEADER", player.UserIDString));
                            return;
                        }

                        if (!clan.IsMember(target.Id))
                        {
                            player.ChatMessage(GetLang("MEMBERNOCONTAINSCLAN", player.UserIDString));
                            return;
                        }

                        if (clan.IsOwner(playerID))
                        {
                            player.ChatMessage(GetLang("NOT_KICK_LEADER", player.UserIDString));
                            return;
                        }

                        clan.Kick(UInt64.Parse(target.Id));
                        player.ChatMessage(GetLang("KICK_PLAYER_SUCCEFULL", player.UserIDString, target.Name));

                        BasePlayer targetPlayer = target.Object as BasePlayer;
                        if (targetPlayer != null) targetPlayer.ChatMessage(GetLang("KICK_PLAYER", target.Id, clan.ClanTag));
                        break;
                    }
                case "OpenChoiseSkins":
                    {
                        ClanData clan = FindClanByUser(player.userID);
                        if (clan == null) return;

                        CreateChoiseSkins(player, clan);
                        break;
                    }
                case "SkinsLayer":
                    {
                        CreateSkinsLayer(player, args.Args[1], Int32.Parse(args.Args[2]));
                        break;
                    }
                case "SelectSkin":
                    {
                        UInt64 SkinID;
                        if (!UInt64.TryParse(args.Args[2], out SkinID)) return;

                        ClanData clan = FindClanByUser(player.userID);
                        if (clan == null) return;

                        String ShortName = args.Args[1];
                        
                        // Проверяем, существует ли ключ в словаре скинов
                        if (!clan.SkinList.ContainsKey(ShortName))
                        {
                            player.ChatMessage(GetLang("INVALID_SKIN_TYPE", player.UserIDString));
                            return;
                        }
                        
                        if (!_skinsUsedList.ContainsKey(ShortName))
                        {
                            _skinsUsedList[ShortName] = new List<UInt64>();
                        }

                        if (clan.SkinList[ShortName] != 0) _skinsUsedList[ShortName].Remove(clan.SkinList[ShortName]);

                        clan.SkinList[ShortName] = SkinID;
                        if (SkinID != 0) _skinsUsedList[ShortName].Add(SkinID);
                        CreateChoiseSkins(player, clan);
                        break;
                    }
                case "OpenChoiseResourse":
                    {
                        ClanData clan = FindClanByUser(player.userID);
                        if (clan == null) return;

                        CreateChoiseResourse(player, clan);
                        break;
                    }
                case "SelectResourse":
                    {
                        CreateSelectResourse(player, args.Args[1]);
                        break;
                    }
                case "ChangeResourseNeed":
                    {
                        ClanData clan = FindClanByUser(player.userID);
                        if (clan == null) return;

                        String ShortName = args.Args[1];
                        if (args.GetString(2) == "") return;

                        Int32 amount;
                        if (!Int32.TryParse(args.Args[2], out amount)) return;

                        if (_config.LimitSettings.NeedLimit.ContainsKey(ShortName) && amount > _config.LimitSettings.NeedLimit[ShortName])
                            return;

                        clan.GatherClan[ShortName].Need = amount;
                        CuiHelper.DestroyUi(player, Layer + ".ChangeResourse");
                        CreateChoiseResourse(player, clan);
                        break;
                    }
                case "createClanInput":
                    {
                        // Сохраняем введенное название клана во временной переменной
                        // Это будет обработано при нажатии кнопки подтверждения
                        break;
                    }
                case "createClanConfirm":
                    {
                        // Получаем название клана из поля ввода
                        // Поскольку мы не можем напрямую получить значение из InputField,
                        // мы используем альтернативный подход через команду с параметром
                        break;
                    }
                case "createClanWithName":
                    {
                        if (args.Args.Length < 2) return;
                        
                        String clanTag = args.Args[1];
                        
                        // Проверяем, есть ли уже клан у игрока
                        if (FindClanByUser(player.userID) != null)
                        {
                            player.ChatMessage(GetLang("ALEADY_HAVE_CLAN", player.UserIDString));
                            return;
                        }

                        // Проверяем длину названия
                        if (String.IsNullOrEmpty(clanTag) || clanTag.Length < _config.Tags.TagMin || clanTag.Length > _config.Tags.TagMax)
                        {
                            player.ChatMessage(GetLang("CLAN_CREATE", player.UserIDString, _config.Tags.TagMax));
                            return;
                        }

                        // Проверяем запрещенные слова
                        if (_config.Tags.BlockedWords.Exists(word => clanTag.Contains(word, CompareOptions.OrdinalIgnoreCase)))
                        {
                            player.ChatMessage(GetLang("CLAN_CREATE_BLOCKED_NAME", player.UserIDString));
                            return;
                        }

                        // Проверяем уникальность названия
                        ClanData existingClan = FindClanByTag(clanTag);
                        if (existingClan != null)
                        {
                            player.ChatMessage(GetLang("CLAN_CREATE_TAG", player.UserIDString));
                            return;
                        }

                        // Создаем клан
                        ClanData newClan = ClanData.CreateNewClan(clanTag, player);
                        if (newClan == null) return;

                        player.ChatMessage(GetLang("SUCCEFULL_CREATE", player.UserIDString, clanTag));
                        
                        // Обновляем интерфейс, показывая новый клан
                        ClanMainUi(player);
                        break;
                    }
                case "disbandClan":
                    {
                        ClanData clan = FindClanByUser(player.userID);
                        if (clan == null)
                        {
                            player.ChatMessage(GetLang("NO_CLAN", player.UserIDString));
                            return;
                        }

                        if (!clan.IsOwner(player.userID))
                        {
                            player.ChatMessage(GetLang("NOT_LEADER", player.UserIDString));
                            return;
                        }

                        clan.Disband();
                        player.ChatMessage(GetLang("CLAN_DISBAND", player.UserIDString));
                        
                        // Обновляем интерфейс, показывая интерфейс создания клана
                        ClanMainUi(player);
                        break;
                    }
            }
        }

        [ConsoleCommand("clan.create.ui")]
        private void ClanCreateUIHandler(ConsoleSystem.Arg args)
        {
            BasePlayer player = args?.Player();
            if (player == null) return;

            // Открываем диалог для ввода названия клана
            player.SendConsoleCommand("chat.say \"/clan create \"");
        }
        #endregion

        #region [Lang]
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["NAME_CLAN"] = "CLAN NAME",
                ["REWARD"] = "REWARD",
                ["TOURNAMENT"] = "TOURNAMENT",
                ["SCORE"] = "SCORE",
                ["PLAYERS"] = "PLAYERS",
                ["CLANTOP_DESCRIPTION"] = "Points are given:\nKilling +{0}, mining ore +{1}, destroying a barrel +{2}, stone/iron/armored wall +{3}/+{4}/+{5}, shooting down a helicopter +{6}, destroying a tank +{7}\nPoints are taken away:\nDeath -{8}, suicide -{9}\nThe reward is given after a wipe on the server!",
                ["MY_CLAN"] = "MY CLAN",
                ["CLOSE"] = "CLOSE",
                ["ACTIVE_TOURNAMENT"] = "PARTICIPANT",
                ["ELIMINATED_TOURNAMENT"] = "ELIMINATED",
                ["STATS_CLAN"] = "Stats clan",
                ["RETURN"] = "RETURN",
                ["PLAYERNAME"] = "PLAYER NAME",
                ["SCORES"] = "SCORES",
                ["ACTIVITY"] = "ACTIVITY",
                ["INFO_KILL"] = "Kills",
                ["INFO_DEATH"] = "Deaths",
                ["INFO_LEADER_CLAN"] = "TEAM LEADER:",
                ["INFO_PLAYER_CLAN"] = "PLAYERS ON THE TEAM",
                ["INFO_SCORES"] = "POINTS SCORED:",
                ["INFO_ACTIVITY"] = "GENERAL ACTIVITY:",
                ["INFO_KILLS"] = "TOTAL KILLS:",
                ["INFO_DEATHS"] = "TOTAL DEATHS:",
                ["CLAN_NAME"] = "CLAN NAME:",
                ["LEADER_CLAN"] = "CHAPTER CLAN:",
                ["PLAYERS_INGAME"] = "PARTICIPANTS IN THE GAME:",
                ["TOTAL_RATE"] = "TOTAL NORMS MET:",
                ["CLAN_TASK"] = "CURRENT TASK",
                ["NO_TASK"] = "The clan leader didn't specify\nthe task at hand",
                ["CLAN_KIT"] = "CLAN KIT",
                ["NICK_PLAYER"] = "PLAYER'S NICKNAME",
                ["NEED_FARM"] = "TOTAL NEED FARM",
                ["ACTIONS"] = "ACTIONS",
                ["RESOURCE_EXTRACTION"] = "RESOURCE EXTRACTION",
                ["STATS_PLAYER"] = "Stats player:",
                ["INFO_PLAYERID"] = "PLAYER'S STEAM:",
                ["KICK_OUT"] = "KICK OUT",
                ["TAKE_MODERATOR"] = "TAKE A MODERATOR",
                ["GIVE_MODERATOR"] = "GET A MODERATOR",
                ["CHANGE_LEADER"] = "APPOINT A HEAD",
                ["SELECT_ITEM"] = "SELECT AN ITEM FOR SKIN SELECTION",
                ["SELECT_SKIN"] = "SELECTED SKINS",
                ["SELECT_RESOURSE"] = "SELECT RESOURCES",
                ["SELECTED_RESOURSE"] = "SELECTED RESOURCES",
                ["ENTRY_AMOUNT"] = "ENTER THE QUANTITY",
                ["ALLOW_MAXIMUM"] = "ALLOWABLE MAXIMUM:",
                ["ENTER_AMOUNT"] = "ENTER THE QUANTITY AND PRESS ENTER",
                ["DAMAGE_FF_OFF"] = "Damage on allies is off.",
                ["NO_CLAN"] = "You have <color=red>no</color> clan",
                ["IS_LEADER"] = "You <color=#9ACD32>are</color> the head of the clan. Hand over the leader from the beginning.",
                ["NO_INVITE"] = "You have no invitations to join the clan.",
                ["CANCEL_INVITE"] = "You have successfully declined an invitation to join the clan.",
                ["INVITER_CANCEL_INVITE"] = "Player {0}, declined the invitation to join the clan.",
                ["NO_TIME"] = "The time to respond has expired.",
                ["INVITER_NO_TIME"] = "{0} did not accept the invitation to join the clan!",
                ["NO_MODERATOR_AND_LEADER"] = "You are not a moderator or clan leader.",
                ["MAX_COUNT_CLAN"] = "Your clan has reached the maximum number of players in the clan.",
                ["ALREADY_HAS_CLAN"] = "You cannot invite a player who is already in the clan.",
                ["ALREAD_YOU_CLAN"] = "The player has already been invited to your clan.",
                ["RECEVIER_INVITE"] = "Player {0} invites to join <color=#9ACD32>{1}</color> /clan accept /clan cancel to join.",
                ["INVITER_INVITE"] = "You have successfully sent an invitation to player {0}.",
                ["ALEADY_HAVE_CLAN"] = "You already have a <color=#9ACD32>clan</color>.",
                ["NO_ADD_YOUR"] = "You can't add yourself to a clan.",
                ["INVITER_INVITE_ACCEPT"] = "Player {0} has accepted the invitation to join the clan.",
                ["RECEVIER_INVITE_ACCEPT"] = "You have successfully accepted an invitation to clan {0}",
                ["NO_FLOOD"] = "Not so fast.",
                ["CLAN_HELP"] = "<size=20>Available commands: </size>\n<color=#9ACD32>/clan</color> - open clan menu\n<color=#9ACD32>/clan create</color> - create clan\n<color=#9ACD32>/clan disband</color> - disband clan\n<color=#9ACD32>/clan leave</color> - leave clan\n<color=#9ACD32>/clan invite</color> - invite to clan\n<color=#9ACD32>/clan task</color> - set a task for clan\n<color=#9ACD32>/clan ff</color> - switch friendlies\n<color=#9ACD32>/clan help</color> - help\n<color=#9ACD32>/ctop</color> - top clans",
                ["CLAN_CREATE"] = "Enter <color=#9ACD32>/clan create</color> - clan name\nThe clan name cannot be more than <color=#9ACD32>{0}</color> characters.",
                ["CLAN_CREATE_BLOCKED_NAME"] = "The clan name cannot contain a forbidden word.",
                ["CLAN_CREATE_TAG"] = "This clan tag is already in use.",
                ["SUCCEFULL_CREATE"] = "You have successfully created a <color=#9ACD32>{0}</color> clan.",
                ["NOT_LEADER"] = "You're not the head of your clan.",
                ["NOT_DISBAND_YES"] = "To confirm clan disbanding enter <color=red>/clan disband yes</color>",
                ["LEAVE_CLAN"] = "You have left the clan <color=#9ACD32>{0}</color>",
                ["CLAN_DISBAND"] = "Your clan has been successfully <color=red>deleted</color>",
                ["INVITE_NO_ARGS"] = "Enter <color=#9ACD32>/clan invite</color> the player's name.",
                ["PLAYER_NOT_FOUND"] = "Player {0} has not been found.",
                ["KICK_NO_ARGS"] = "Type <color=#9ACD32>/clan kick</color> player name.",
                ["NO_KICK_LEADER"] = "You can't kick the head of the clan out of the clan!",
                ["KICK_PLAYER_SUCCEFULL"] = "You have successfully kicked player {0} out of the clan.",
                ["KICK_PLAYER"] = "You've been kicked out of clan {0}.",
                ["FF_OFF"] = "You have successfully turned off damage to allies.",
                ["FF_ON"] = "You have successfully enabled damage on allies.",
                ["CLAN_TASK_MORE"] = "You cannot specify a task longer than 60 characters.",
                ["CLAN_TASK_SUCCEFULL"] = "You have successfully set a new task for your clan.",
                ["CHANGE_LEADER_SUCCEFULL"] = "You have successfully transferred the clan leader.",
                ["ALREADY_MODERATOR"] = "The player is a moderator.",
                ["MAX_COUNT_MODERATOR"] = "Your clan has the maximum number of moderators.",
                ["PROMOTE_MODERATOR"] = "You have successfully appointed a moderator.",
                ["NOT_MODERATOR"] = "The player is not a moderator.",
                ["SUCCEFULL_DEMOTE_MODERATOR"] = "You have successfully removed the moderator.",
                ["PLAYER_NOT_FOUNDED"] = "Player not found.",
                ["NOT_KICK_LEADER"] = "You can't kick the head of the clan out of the clan!",
                ["BRADLEYAPC"] = "The tank was destroyed by clan <color=#9ACD32>{0}</color>",
                ["PATROLHELICOPTER"] = "The helicopter was shot down by clan <color=#9ACD32>{0}</color>",
                ["MEMBERNOCONTAINSCLAN"] = "The player is not a member of your clan."
            }, this);

            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["NAME_CLAN"] = "НАЗВАНИЕ КЛАНА",
                ["REWARD"] = "НАГРАДА",
                ["TOURNAMENT"] = "ТУРНИР",
                ["SCORE"] = "ОЧКИ",
                ["PLAYERS"] = "ИГРОКОВ",
                ["CLANTOP_DESCRIPTION"] = "Очки даются:\nУбийство +{0}, добыча руды +{1}, разрушение бочки +{2}, каменная/железная/бронированная стенка +{3}/+{4}/+{5}, сбитие верталета +{6}, уничтожение танка +{7}\nОчки отнимаются:\nСмерть -{8}, самоубийство -{9}\nНаграда выдается после вайпа на сервере!",
                ["MY_CLAN"] = "МОЙ КЛАН",
                ["CLOSE"] = "ЗАКРЫТЬ",
                ["ACTIVE_TOURNAMENT"] = "УЧАСТНИК",
                ["ELIMINATED_TOURNAMENT"] = "ВЫЛЕТЕЛ",
                ["STATS_CLAN"] = "Статистика клана",
                ["RETURN"] = "НАЗАД",
                ["PLAYERNAME"] = "ИМЯ ИГРОКА",
                ["SCORES"] = "ОЧКОВ",
                ["ACTIVITY"] = "АКТИВНОСТЬ",
                ["INFO_KILL"] = "Убийства",
                ["INFO_DEATH"] = "Смерти",
                ["INFO_LEADER_CLAN"] = "ГЛАВА КОМАНДЫ:",
                ["INFO_PLAYER_CLAN"] = "ИГРОКОВ В ИГРЕ",
                ["INFO_SCORES"] = "НАБРАНО ОЧКОВ:",
                ["INFO_ACTIVITY"] = "ОБЩАЯ АКТИВНОСТЬ:",
                ["INFO_KILLS"] = "ВСЕГО УБИЙСТВ:",
                ["INFO_DEATHS"] = "ВСЕГО СМЕРТЕЙ:",
                ["CLAN_NAME"] = "НАЗВАНИЕ КЛАНА:",
                ["LEADER_CLAN"] = "Глава клана:",
                ["PLAYERS_INGAME"] = "Участников в игре:",
                ["TOTAL_RATE"] = "Выполненная норма:",
                ["CLAN_TASK"] = "ЗАДАНИЕ КЛАНА",
                ["NO_TASK"] = "Глава клана не указал\nтекущую задачу",
                ["CLAN_KIT"] = "НАБОР КЛАНОВОЙ ОДЕЖДЫ",
                ["NICK_PLAYER"] = "НИК ИГРОКА",
                ["NEED_FARM"] = "НОРМА",
                ["ACTIONS"] = "ДЕЙСТВИЯ",
                ["RESOURCE_EXTRACTION"] = "ДОБЫЧА РЕСУРСОВ",
                ["STATS_PLAYER"] = "Статистика игрока:",
                ["INFO_PLAYERID"] = "СТИМ ИГРОКА:",
                ["TOTAL_RATE_ACHIEVED"] = "ОБЩАЯ ВЫПОЛНЕННАЯ НОРМА:",
                ["KICK_OUT"] = "ВЫГНАТЬ ИЗ КЛАНА",
                ["TAKE_MODERATOR"] = "ЗАБРАТЬ МОДЕРА",
                ["GIVE_MODERATOR"] = "ВЫДАТЬ МОДЕРА",
                ["CHANGE_LEADER"] = "НАЗНАЧИТЬ ГЛАВОЙ",
                ["SELECT_ITEM"] = "ВЫБЕРИТЕ ПРЕДМЕТ ДЛЯ ВЫБОРА СКИНА",
                ["SELECT_SKIN"] = "ВЫБРАННЫЕ СКИНЫ",
                ["SELECT_RESOURSE"] = "ВЫБЕРИТЕ РЕСУРСЫ",
                ["SELECTED_RESOURSE"] = "ВЫБРАННЫЕ РЕСУРСЫ",
                ["ENTRY_AMOUNT"] = "ВВЕДИТЕ КОЛИЧЕСТВО",
                ["ALLOW_MAXIMUM"] = "ДОПУСТИМЫЙ МАКСИМУМ:",
                ["ENTER_AMOUNT"] = "ВВЕДИТЕ КОЛИЧЕСТВО И НАЖМИТЕ ENTER",
                ["DAMAGE_FF_OFF"] = "Урон по союзникам выключен.",
                ["NO_CLAN"] = "У вас <color=red>нет</color> клана",
                ["IS_LEADER"] = "Вы <color=#9ACD32>являетесь</color> главой клана. С начала передайте лидера.",
                ["NO_INVITE"] = "У вас нет приглашений в клан.",
                ["CANCEL_INVITE"] = "Вы успешно отклонили приглашение в клан.",
                ["INVITER_CANCEL_INVITE"] = "Игрок {0}, отклонил приглашение в клан.",
                ["NO_TIME"] = "Время на ответ истекло.",
                ["INVITER_NO_TIME"] = "{0} не принял приглашение в клан!",
                ["NO_MODERATOR_AND_LEADER"] = "Вы не являетесь модератором или главой клана.",
                ["MAX_COUNT_CLAN"] = "Ваш клан достиг максимальное количество игроков в клане.",
                ["ALREADY_HAS_CLAN"] = "Вы не можете пригласить игрока который уже находится в клане.",
                ["ALREAD_YOU_CLAN"] = "Игрок уже приглашен в ваш клан.",
                ["RECEVIER_INVITE"] = "Игрок {0} приглашает вступить в клан <color=#9ACD32>{1}</color>\nЧтобы вступить введите /clan accept\nЧтобы отклонить введите /clan cancel.",
                ["INVITER_INVITE"] = "Вы успешно отправили приглашение игроку {0}.",
                ["ALEADY_HAVE_CLAN"] = "У вас уже есть <color=#9ACD32>клан</color>.",
                ["NO_ADD_YOUR"] = "Вы не можете добавить сами себя в клан.",
                ["INVITER_INVITE_ACCEPT"] = "Игрок {0} принял приглашение в клан.",
                ["RECEVIER_INVITE_ACCEPT"] = "Вы успешно приняли приглашение в клан {0}",
                ["NO_FLOOD"] = "Не так быстро.",
                ["CLAN_HELP"] = "<size=20>Доступные команды:</size>\n<color=#9ACD32>/clan</color> - открыть меню клана\n<color=#9ACD32>/clan create</color> - создать клан\n<color=#9ACD32>/clan disband</color> - распустить клан\n<color=#9ACD32>/clan leave</color> - покинуть клан\n<color=#9ACD32>/clan invite</color> - пригласить в клан\n<color=#9ACD32>/clan task</color> - установить задачу для клана\n<color=#9ACD32>/clan ff</color> - переключение френдли файера\n<color=#9ACD32>/clan help</color> - справка\n<color=#9ACD32>/ctop</color> - топ кланов",
                ["CLAN_CREATE"] = "Введите <color=#9ACD32>/clan create</color> - название клана\nИмя клана не может быть более <color=#9ACD32>{0}</color> символов.",
                ["CLAN_CREATE_BLOCKED_NAME"] = "Имя клана не может содержать запрещенное слово.",
                ["CLAN_CREATE_TAG"] = "Данный клан тэг уже используется.",
                ["SUCCEFULL_CREATE"] = "Вы успешно создали клан <color=#9ACD32>{0}</color>.",
                ["NOT_LEADER"] = "Вы не глава вашего клана.",
                ["NOT_DISBAND_YES"] = "Чтобы подтвердить удаление клана введите <color=red>/clan disband yes</color>",
                ["LEAVE_CLAN"] = "Вы покинули клан <color=#9ACD32>{0}</color>",
                ["CLAN_DISBAND"] = "Ваш клан успешно <color=red>удален</color>",
                ["INVITE_NO_ARGS"] = "Введите <color=#9ACD32>/clan invite</color> имя игрока.",
                ["PLAYER_NOT_FOUND"] = "Игрок {0} не найден.",
                ["KICK_NO_ARGS"] = "Введите <color=#9ACD32>/clan kick</color> имя игрока.",
                ["NO_KICK_LEADER"] = "Вы не можете выгнать главу из клана!",
                ["KICK_PLAYER_SUCCEFULL"] = "Вы успешно выгнали игрока {0} из клана.",
                ["KICK_PLAYER"] = "Вас выгнали из клана {0}.",
                ["FF_OFF"] = "Вы успешно выключили урон по союзникам.",
                ["FF_ON"] = "Вы успешно включили урон по союзникам.",
                ["CLAN_TASK_MORE"] = "Вы не можете указать задачу длинее 60 символов.",
                ["CLAN_TASK_SUCCEFULL"] = "Вы успешно установили новую задачу для вашего клана.",
                ["CHANGE_LEADER_SUCCEFULL"] = "Вы успешно передали лидера клана.",
                ["ALREADY_MODERATOR"] = "Игрок является модератором.",
                ["MAX_COUNT_MODERATOR"] = "Ваш клан имеет максимальное количество модераторов.",
                ["PROMOTE_MODERATOR"] = "Вы успешно назначили модератора.",
                ["NOT_MODERATOR"] = "Игрок не является модератором.",
                ["SUCCEFULL_DEMOTE_MODERATOR"] = "Вы успешно сняли модератора.",
                ["PLAYER_NOT_FOUNDED"] = "Игрок не найден.",
                ["NOT_KICK_LEADER"] = "Вы не можете выгнать главу из клана!",
                ["BRADLEYAPC"] = "Танк был уничтожен кланом <color=#9ACD32>{0}</color>",
                ["PATROLHELICOPTER"] = "Вертолет был сбит кланом <color=#9ACD32>{0}</color>",
                ["MEMBERNOCONTAINSCLAN"] = "Игрок не состоит в вашем клане."
            }, this, "ru");
        }

        private String GetLang(String LangKey, String userID = null, params object[] args)
        {
            sb.Clear();
            if (args != null)
            {
                sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return sb.ToString();
            }
            return lang.GetMessage(LangKey, this, userID);
        }
        #endregion

        #region [Embed Classes]
        private readonly Dictionary<String, String> _headers = new Dictionary<String, String>()
        {
            {"Content-Type", "application/json"}
        };

        private void SendDiscordMessage(String url, DiscordMessage message)
        {
            StringBuilder json = message.ToJson();

            webrequest.Enqueue(url, json.ToString(), SendDiscordMessageCallback, this, RequestMethod.POST, _headers);
        }

        private void SendDiscordMessageCallback(Int32 code, String message)
        {
            if (code != 204) PrintError(message);
        }

        private class DiscordMessage
        {
            [JsonProperty("username")]
            private String Username { get; set; }

            [JsonProperty("avatar_url")]
            private String AvatarUrl { get; set; }

            [JsonProperty("content")]
            private String Content { get; set; }

            [JsonProperty("embeds")]
            private List<Embed> Embeds { get; }

            public DiscordMessage(String username = null, String avatarUrl = null)
            {
                Username = username;
                AvatarUrl = avatarUrl;
                Embeds = new List<Embed>();
            }

            public DiscordMessage(String content, String username = null, String avatarUrl = null)
            {
                Content = content;
                Username = username;
                AvatarUrl = avatarUrl;
                Embeds = new List<Embed>();
            }

            public DiscordMessage(Embed embed, String username = null, String avatarUrl = null)
            {
                Embeds = new List<Embed> { embed };
                Username = username;
                AvatarUrl = avatarUrl;
            }

            public DiscordMessage AddEmbed(Embed embed)
            {
                if (Embeds.Count >= 10)
                {
                    throw new IndexOutOfRangeException("Only 10 embed are allowed per message");
                }

                Embeds.Add(embed);
                return this;
            }

            public DiscordMessage AddContent(String content)
            {
                Content = content;
                return this;
            }

            public DiscordMessage AddSender(String username, String avatarUrl)
            {
                Username = username;
                AvatarUrl = avatarUrl;
                return this;
            }

            public StringBuilder ToJson() => new StringBuilder(JsonConvert.SerializeObject(this, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
        }

        private class Embed
        {
            [JsonProperty("color")]
            private Int32 Color { get; set; }

            [JsonProperty("fields")]
            private List<Field> Fields { get; } = new List<Field>();

            [JsonProperty("title")]
            private String Title { get; set; }

            [JsonProperty("description")]
            private String Description { get; set; }

            [JsonProperty("url")]
            private String Url { get; set; }

            [JsonProperty("image")]
            private Image Image { get; set; }

            [JsonProperty("thumbnail")]
            private Image Thumbnail { get; set; }

            [JsonProperty("video")]
            private Video Video { get; set; }

            [JsonProperty("author")]
            private AuthorInfo Author { get; set; }

            [JsonProperty("footer")]
            private Footer Footer { get; set; }

            public Embed AddTitle(String title)
            {
                Title = title;
                return this;
            }

            public Embed AddDescription(String description)
            {
                Description = description;
                return this;
            }

            public Embed AddUrl(String url)
            {
                Url = url;
                return this;
            }

            public Embed AddAuthor(String name, String iconUrl = null, String url = null, String proxyIconUrl = null)
            {
                Author = new AuthorInfo(name, iconUrl, url, proxyIconUrl);
                return this;
            }

            public Embed AddFooter(String text, String iconUrl = null, String proxyIconUrl = null)
            {
                Footer = new Footer(text, iconUrl, proxyIconUrl);

                return this;
            }

            public Embed AddColor(Int32 color)
            {
                if (color < 0x0 || color > 0xFFFFFF)
                {
                    throw new Exception($"Color '{color}' is outside the valid color range");
                }

                Color = color;
                return this;
            }

            public Embed AddColor(String color)
            {
                Int32 parsedColor = Int32.Parse(color.TrimStart('#'), NumberStyles.AllowHexSpecifier);
                if (parsedColor < 0x0 || parsedColor > 0xFFFFFF)
                {
                    throw new Exception($"Color '{color}' is outside the valid color range");
                }

                Color = parsedColor;
                return this;
            }

            public Embed AddColor(Int32 red, Int32 green, Int32 blue)
            {
                if (red < 0 || red > 255 || green < 0 || green > 255 || green < 0 || green > 255)
                {
                    throw new Exception($"Color Red:{red} Green:{green} Blue:{blue} is outside the valid color range. Must be between 0 - 255");
                }

                Color = red * 65536 + green * 256 + blue; ;
                return this;
            }

            public Embed AddBlankField(bool inline)
            {
                Fields.Add(new Field("\u200b", "\u200b", inline));
                return this;
            }

            public Embed AddField(String name, String value, bool inline)
            {
                Fields.Add(new Field(name, value, inline));
                return this;
            }

            public Embed AddImage(String url, Int32? width = null, Int32? height = null, String proxyUrl = null)
            {
                Image = new Image(url, width, height, proxyUrl);
                return this;
            }

            public Embed AddThumbnail(String url, Int32? width = null, Int32? height = null, String proxyUrl = null)
            {
                Thumbnail = new Image(url, width, height, proxyUrl);
                return this;
            }

            public Embed AddVideo(String url, Int32? width = null, Int32? height = null)
            {
                Video = new Video(url, width, height);
                return this;
            }
        }

        private class Field
        {
            [JsonProperty("name")]
            private String Name { get; }

            [JsonProperty("value")]
            private String Value { get; }

            [JsonProperty("inline")]
            private bool Inline { get; }

            public Field(String name, String value, bool inline)
            {
                Name = name;
                Value = value;
                Inline = inline;
            }
        }

        private class Image
        {
            [JsonProperty("url")]
            private String Url { get; }

            [JsonProperty("width")]
            private Int32? Width { get; }

            [JsonProperty("height")]
            private Int32? Height { get; }

            [JsonProperty("proxyURL")]
            private String ProxyUrl { get; }

            public Image(String url, Int32? width, Int32? height, String proxyUrl)
            {
                Url = url;
                Width = width;
                Height = height;
                ProxyUrl = proxyUrl;
            }
        }

        private class Video
        {
            [JsonProperty("url")]
            private String Url { get; }

            [JsonProperty("width")]
            private Int32? Width { get; }

            [JsonProperty("height")]
            private Int32? Height { get; }

            public Video(String url, Int32? width, Int32? height)
            {
                Url = url;
                Width = width;
                Height = height;
            }
        }

        private class AuthorInfo
        {
            [JsonProperty("name")]
            private String Name { get; }

            [JsonProperty("url")]
            private String Url { get; }

            [JsonProperty("icon_url")]
            private String IconUrl { get; }

            [JsonProperty("proxy_icon_url")]
            private String ProxyIconUrl { get; }

            public AuthorInfo(String name, String iconUrl, String url, String proxyIconUrl)
            {
                Name = name;
                Url = url;
                IconUrl = iconUrl;
                ProxyIconUrl = proxyIconUrl;
            }
        }

        private class Footer
        {
            [JsonProperty("text")]
            private String Text { get; }

            [JsonProperty("icon_url")]
            private String IconUrl { get; }

            [JsonProperty("proxy_icon_url")]
            private String ProxyIconUrl { get; }

            public Footer(String text, String iconUrl, String proxyIconUrl)
            {
                Text = text;
                IconUrl = iconUrl;
                ProxyIconUrl = proxyIconUrl;
            }
        }
        #endregion

        #region [Config]
        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = LanguageEn ? "Main plugin command" : "Основная команда плагина", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<String> ClanCommands = new List<String>()
            {
                "clan",
                "clans"
            };

            [JsonProperty(PropertyName = LanguageEn ? "Use the clan tag in the player's name" : "Использовать тэг клана в имене игрока")]
            public Boolean TagInName = true;

            [JsonProperty(PropertyName = LanguageEn ? "Create greens automatically ?" : "Создавать зеленку автоматически ?")]
            public Boolean AutoTeamCreation = true;

            [JsonProperty(PropertyName = LanguageEn ? "Prohibit the creation of a green without a clan ?" : "Запретить создавать зеленку без клана ?")]
            public Boolean BlockCreateTeam = true;

            [JsonProperty(PropertyName = LanguageEn ? "Change the builder to clan leader ?" : "Изменять овнера постройки на лидера клана ?")]
            public Boolean ChangeOwnerIDForLeader = true;

            [JsonProperty(PropertyName = LanguageEn ? "Customizing prizes for wipe" : "Настройка призов за вайп")]
            public GameStoreSettings GameStoreSettings = new GameStoreSettings()
            {
                Enable = true,
                ShopID = String.Empty,
                ServerID = String.Empty,
                SecretKey = String.Empty,
                MinPoint = 1000,
                MinPointPlayer = 100,
                RewardList = new Dictionary<Int32, Int32>()
                {
                    [1] = 350,
                    [2] = 200,
                    [3] = 150
                }
            };

            [JsonProperty(PropertyName = LanguageEn ? "Limit settings" : "Настройки лимитов")]
            public LimitSettings LimitSettings = new LimitSettings()
            {
                MemberLimit = 5,
                ModeratorLimit = 2,
                NeedLimit = new Dictionary<String, Int32>()
                {
                    ["wood"] = 1000000,
                    ["stones"] = 1000000,
                    ["metal.ore"] = 1000000,
                    ["sulfur.ore"] = 1000000,
                    ["hq.metal.ore"] = 250000,
                    ["cloth"] = 100000,
                    ["leather"] = 100000,
                    ["fat.animal"] = 100000,
                    ["loot-barrel"] = 100000
                }
            };

            [JsonProperty(PropertyName = LanguageEn ? "Points Settings. [ShortName] - Number of points" : "Настройки очков. [ShortName] - Количество очков", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<String, Int32> ScoreTable = new Dictionary<String, Int32>()
            {
                ["wood"] = 2,
                ["stones"] = 5,
                ["metal.ore"] = 5,
                ["hq.metal.ore"] = 5,
                ["sulfur.ore"] = 5,
                ["loot-barrel"] = 2,
                ["PatrolHelicopter"] = 1500,
                ["BradleyAPC"] = 750,
                ["Death"] = 50,
                ["Suicide"] = 50,
                ["Kill"] = 50,
                ["WallTier2"] = 3,
                ["WallTier3"] = 5,
                ["WallTier4"] = 7
            };

            [JsonProperty(PropertyName = LanguageEn ? "Clan tag settings" : "Настройки кланового тега")]
            public TagSettings Tags = new TagSettings
            {
                TagMin = 2,
                TagMax = 6,
                BlockedWords = new List<String>()
                {
                    "Admin",
                    "Админы"
                }
            };

            public DiscordSettings DiscordSetting = new DiscordSettings()
            {
                DiscordNotificationEnable = false,
                DiscordWebHook = String.Empty
            };

            public VersionNumber Version = new VersionNumber();
        }

        private class LimitSettings
        {
            [JsonProperty(PropertyName = LanguageEn ? "Clan membership limit" : "Лимит участников в клане")]
            public Int32 MemberLimit;

            [JsonProperty(PropertyName = LanguageEn ? "Moderator limit" : "Лимит модераторов")]
            public Int32 ModeratorLimit;

            [JsonProperty(PropertyName = LanguageEn ? "Maximum quantity for billing rate ( Resource - maximum quantity )" : "Максимальное количество для выставления нормы ( Ресурс - максимальное количество )")]
            public Dictionary<String, Int32> NeedLimit = new Dictionary<String, Int32>();
        }

        private class TagSettings
        {
            [JsonProperty(PropertyName = LanguageEn ? "Banned clan tags" : "Запрещенные тэги кланов", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<String> BlockedWords = new List<String>();

            [JsonProperty(PropertyName = LanguageEn ? "Minimum number of characters of the clan tag" : "Минимальное количество символов кланового тега")]
            public Int32 TagMin;

            [JsonProperty(PropertyName = LanguageEn ? "Maximum number of characters of a clan tag" : "Максимальное количество символов кланового тега")]
            public Int32 TagMax;
        }

        public class GameStoreSettings
        {
            [JsonProperty(PropertyName = LanguageEn ? "Use the issuance of awards ?" : "Использовать выдачу наград ?")]
            public Boolean Enable;

            [JsonProperty(PropertyName = LanguageEn ? "Share the prize for the whole team or give the prize from the configuration ? (true - share prize | false - static prize)" : "Разделять приз на всю команду или давать приз из конфигурации ? (true - разделять приз | false - статический приз)")]
            public Boolean Prize;

            [JsonProperty(PropertyName = LanguageEn ? "Store ID in the service" : "ИД магазина в сервисе")]
            public String ShopID;

            [JsonProperty(PropertyName = LanguageEn ? "Server ID in the service" : "ИД сервера в сервисе")]
            public String ServerID;

            [JsonProperty(PropertyName = LanguageEn ? "Secret key (do not distribute it)" : "Секретный ключ (не распростраяйте его)")]
            public String SecretKey;

            [JsonProperty(PropertyName = LanguageEn ? "Minimum number of points to receive an award" : "Минимальное количество очков для получения награды")]
            public Int32 MinPoint;

            [JsonProperty(PropertyName = LanguageEn ? "Minimum number of points to receive an award (Player's)" : "Минимальное количество очков для получения награды (У игрока)")]
            public Int32 MinPointPlayer;

            [JsonProperty(PropertyName = LanguageEn ? "Place in the top and the balance given to the player" : "Место в топе и выдаваемый баланс игроку")]
            public Dictionary<Int32, Int32> RewardList = new Dictionary<Int32, Int32>();
        }

        public class DiscordSettings
        {
            [JsonProperty(PropertyName = LanguageEn ? "Use discord notification when awards are received ?" : "Использовать оповещение в дискорд о получении наград ?")]
            public Boolean DiscordNotificationEnable;

            [JsonProperty(PropertyName = LanguageEn ? "Discord WebHook" : "Дискорд ВебХук")]
            public String DiscordWebHook;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();

                if (_config.Version < Version)
                    UpdateConfigValues();

                SaveConfig();
            }
            catch (Exception ex)
            {
                PrintError(LanguageEn ? $"Your configuration file contains an error. Using the default configuration values.\n{ex}" : $"Ваш файл конфигурации содержит ошибку. Использование значений конфигурации по умолчанию.\n{ex}");

                LoadDefaultConfig();
            }
        }

        private void UpdateConfigValues()
        {
            PrintWarning(LanguageEn ? "Config update detected! Updating config values..." : "Обнаружено обновление конфигурации! Обновление значений конфигурации...");

            Configuration baseConfig = new Configuration();

            if (_config.Version != default(VersionNumber))
            {
                if (_config.Version < new VersionNumber(1, 0, 1))
                    _config.BlockCreateTeam = true;

                if (_config.Version < new VersionNumber(1, 2, 2))
                {
                    _config.DiscordSetting.DiscordNotificationEnable = false;
                    _config.DiscordSetting.DiscordWebHook = String.Empty;
                }
            }

            _config.Version = Version;
            PrintWarning(LanguageEn ? "Config update completed!" : "Обновление конфигурации завершено!");
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();
        #endregion

        #region [Api]
        private ClanData FindClanByTag(String ClanTag) => _clansList.Find(clan => clan.ClanTag.ToLower() == ClanTag.ToLower());

        private ClanData FindClanByUser(UInt64 playerID)
        {
            ClanData clan;
            if (_playerToClan.TryGetValue(playerID, out clan)) return clan;

            return null;
        }

        private Int32 GetClanPoint(String clanTag)
        {
            ClanData clan = FindClanByTag(clanTag);
            if (clan == null) return 0;

            return clan.GetScore();
        }

        private String GetClanTag(UInt64 playerID)
        {
            ClanData clan = FindClanByUser(playerID);
            if (clan == null) return null;

            return clan.ClanTag;
        }

        private List<String> GetMembersClan(String clanTag)
        {
            ClanData clan = FindClanByTag(clanTag);
            if (clan == null) return new List<String>();

            return clan.Members.Keys.Select(x => x.ToString()).ToList();
        }

        private Boolean IsClanMember(String userID, String targetID) => IsClanMember(UInt64.Parse(userID), UInt64.Parse(targetID));

        private Boolean IsClanMember(UInt64 userID, UInt64 targetID) => IsTeammates(userID, targetID);

        private Boolean IsModerator(UInt64 playerID)
        {
            ClanData clan = FindClanByUser(playerID);
            if (clan == null) return false;

            if (clan.IsModerator(playerID)) return true;

            return false;
        }

        private UInt64 GetClanLeader(String clanTag)
        {
            ClanData clan = FindClanByTag(clanTag);
            if (clan == null) return 0;

            return clan.LeaderID;
        }

        private void GetPointRaid(UInt64 userID, UInt64 targetID)
        {
            ClanData clan = FindClanByUser(userID);
            if (clan == null) return;

            Int32 totalPoint = clan.GetScore() / 2;
            if (totalPoint <= 0) return;

            ClanData clanKill = FindClanByUser(targetID);
            if (clanKill == null) return;

            clanKill.Members[targetID].Point += totalPoint;
        }

        private void GiveClanPoints(String clanTag, Int32 points)
        {
            ClanData clan = FindClanByTag(clanTag);
            if (clan == null) return;

            foreach (var member in clan.Members.Keys)
            {
                if (clan.Members.ContainsKey(member))
                {
                    clan.Members[member].Point += points;
                }
            }
        }

        private static void ClanCreate(String clanTag) => Interface.CallHook("OnClanCreate", clanTag);

        private static void ClanDisbanded(List<String> memberUserIDs) => Interface.CallHook("OnClanDisbanded", memberUserIDs);

        private static void ClanDisbanded(String clanTag, List<String> memberUserIDs) => Interface.CallHook("OnClanDisbanded", clanTag, memberUserIDs);

        private static void ClanDestroy(String clanTag) => Interface.CallHook("OnClanDestroy", clanTag);

        private static void ClanMemberGone(String userID, String clanTag) => Interface.CallHook("OnClanMemberGone", userID, clanTag);

        private static void ClanMemberJoined(String userID, String clanTag) => Interface.CallHook("OnClanMemberJoined", userID, clanTag);
        #endregion
        #endregion

        #region Images
        private static ImageUI _imageUI;
        private class ImageUI
        {
            private const String _path = "TPSystem/TPClan/";
            private const String _printPath = "data/" + _path;
            private readonly Dictionary<String, ImageData> _images = new()
            {
                { "FON_RESOURCE_EDIT", new ImageData() },
                { "FON_SKIN_EDIT", new ImageData() },
                { "FON_CLAN_NORMA", new ImageData() },
                { "FON_CLAN_PLAYER_STAT", new ImageData() },
                { "MAIN_FON", new ImageData() },
                { "FON_CLAN", new ImageData() },
                { "FON_CLAN_TOP", new ImageData() },
                { "FON_CLAN_STAT", new ImageData() },
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

                    if (failedImages.Count > 0)
                    {
                        String images = String.Join(", ", failedImages);
                        _.PrintError($"Не удалось загрузить следующие изображения: {images}. Возможно, вы не загрузили их в папку '{_printPath}'.");
                        Interface.Oxide.UnloadPlugin(_.Name);
                    }
                    else
                    {
                        _.Puts($"{_images.Count} изображений успешно загружено!");
                    }
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

            private IEnumerator ProcessDownloadImage(KeyValuePair<string, ImageData> image)
            {
                string url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + _path + image.Key + ".png";

                using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
                {
                    yield return www.SendWebRequest();

                    if (www.isNetworkError || www.isHttpError)
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
        #endregion
    }
}