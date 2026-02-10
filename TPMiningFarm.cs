using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections;
using System;
using UnityEngine.Networking;
using System.IO;
using Rust;
namespace Oxide.Plugins
{
	[Info("TPMiningFarm", "pluginfuel.ru", "20.0.2")]
	class TPMiningFarm : RustPlugin
	{
		[PluginReference]
		private Plugin ImageLibrary, IQEconomic, TPMenuSystem;
		public static TPMiningFarm _;
		public class ElectricBatteryHelper : FacepunchBehaviour
		{
			public ElectricBattery battery;
			public Mailbox mailbox;
			public IOEntity split;
			private float checkInterval = 1f;
			private float timer = 0f;
			public void Awake()
			{
				battery = GetComponent<ElectricBattery>();
			}
			
			public void AddChildMailBox(Mailbox box)
			{
				mailbox = box;
			}
			
			public void AddChildSplit(IOEntity a)
			{
				split = a;
			}
			
			public void Destroy()
			{
				if(mailbox != null)
				{
					StorageContainer[] containers = mailbox.GetComponentsInChildren<StorageContainer>();
					foreach (StorageContainer container in containers) {
						container.DropItems();
					}
					battery.RemoveChild(mailbox);
					mailbox.Kill();
					battery.SendNetworkUpdate();
					_.SaveData();
				}
			}
			
			public void Update()
			{
				try
				{
					if (split == null || battery == null) return;
					timer += Time.deltaTime;
					if (timer >= checkInterval)
					{
						timer = 0f;
						var components = _.GetComponents(split);
						if (components != null && mailbox != null)
						CheckBatteryEnergy();
					}
				}
				catch (Exception ex)
				{
					_.PrintError($"[ElectricBatteryHelper.Update] {ex.Message}\n{ex.StackTrace}");
				}
			}
			
            public void CheckBatteryEnergy()
            {
                if (battery != null)
                {
                    float energy = battery.rustWattSeconds;
					
                    float required = Mathf.Max(1f, configData.Fermsettings.watt) * 60f;
                    if (energy >= required)
                    {
                        int payouts = Mathf.FloorToInt(energy / required);
                        for (int i = 0; i < payouts; i++)
                        {
                            DropMoney();
						}
                        battery.rustWattSeconds = energy - payouts * required;
					}
				}
			}
			
            private void DropMoney()
            {
                var config = configData.Fermsettings;
                var itemContainer = mailbox.inventory;
				
                int amm=0;
                int much=config.amount;
				
                foreach(var a in itemContainer.itemList)
				if(a != null) amm = a.amount;
                
                if(amm >= config.maxCoins)
				return;
				
                if(amm+config.amount > config.maxCoins)
				much = config.maxCoins-amm;
				
                Item item = ItemManager.CreateByItemID(config.id, much, config.SkinID);
                item.name = $"{config.Name}";
				
                Vector3 mailboxPosition = mailbox.transform.position;
                foreach (var player in BasePlayer.activePlayerList)
                {
                    float distance = Vector3.Distance(player.transform.position, mailboxPosition);
                    if (distance <= 5f)
                    {
                        player.SendConsoleCommand("ddraw.text", 0, 1.5f, Color.yellow, mailboxPosition + Vector3.up * 1.6f, $"+{much}");
					}
				}
				
                mailbox.inventory.GiveItem(item);
                mailbox.SendNetworkUpdate();
			}
            
		}
		
        public class TeslaTrapHelper : FacepunchBehaviour
        {
            private IOEntity farmEntity;
            private ulong ownerID;
            private bool isActive = false;
            private float lastDischargeTime = 0f;
            private TPMiningFarm mainPlugin;
			
            public void Initialize(IOEntity entity, ulong owner, TPMiningFarm plugin)
            {
                farmEntity = entity;
                ownerID = owner;
                mainPlugin = plugin;
                isActive = true;
			}
			
            public void Update()
            {
                if (!isActive || farmEntity == null || farmEntity.IsDestroyed) return;
                if (!configData.TeslaTrap.Enabled) return;
				
                if (Time.time - lastDischargeTime >= configData.TeslaTrap.ConstantDischargeInterval)
                {
                    lastDischargeTime = Time.time;
                    DischargeElectricity();
				}
			}
			
            private void DischargeElectricity()
            {
                if (farmEntity == null) return;
				
                Vector3 farmPosition = farmEntity.transform.position;
                float radius = configData.TeslaTrap.Radius;
				
                List<BasePlayer> nearbyPlayers = new List<BasePlayer>();
                Vis.Entities(farmPosition, radius, nearbyPlayers);
				
                bool foundTargets = false;
				
                foreach (BasePlayer player in nearbyPlayers)
                {
                    if (player == null || player.IsDestroyed || !player.IsConnected) continue;
					
                    if (ShouldIgnorePlayer(player)) continue;
					
                    player.Hurt(configData.TeslaTrap.Damage, DamageType.ElectricShock, null, false);
                    foundTargets = true;
					
                    if (!string.IsNullOrEmpty(configData.TeslaTrap.ElectricEffect))
                    {
                        Effect.server.Run(configData.TeslaTrap.ElectricEffect, player.transform.position);
					}
					
                    if (configData.TeslaTrap.AdditionalEffects != null)
                    {
                        foreach (string effect in configData.TeslaTrap.AdditionalEffects)
                        {
                            if (!string.IsNullOrEmpty(effect))
                            {
                                Effect.server.Run(effect, player.transform.position);
							}
						}
					}
				}
				
                if (configData.TeslaTrap.DischargeOnlyIfEnemyNearby && !foundTargets)
				return;
				
                if (!string.IsNullOrEmpty(configData.TeslaTrap.ElectricEffect))
                {
                    Effect.server.Run(configData.TeslaTrap.ElectricEffect, farmPosition);
				}
                
                if (configData.TeslaTrap.AdditionalEffects != null)
                {
                    foreach (string effect in configData.TeslaTrap.AdditionalEffects)
                    {
                        if (!string.IsNullOrEmpty(effect))
                        {
                            Effect.server.Run(effect, farmPosition);
						}
					}
				}
			}
			
            private bool ShouldIgnorePlayer(BasePlayer player)
            {
                if (player == null) return true;
				
                if (configData.TeslaTrap.IgnoreOwner && player.userID == ownerID)
				return true;
				
                if (configData.TeslaTrap.IgnoreFriends)
                {
                    var ownerPlayer = BasePlayer.FindByID(ownerID);
                    if (ownerPlayer != null)
                    {
                        if (ownerPlayer.currentTeam != 0)
                        {
                            if (player.currentTeam != 0 && player.currentTeam == ownerPlayer.currentTeam)
                            {
                                return true;
							}
						}
						
                        try
                        {
                            var clanPlugin = mainPlugin.plugins.Find("Clans");
                            if (clanPlugin != null)
                            {
                                var ownerClan = clanPlugin.Call("GetClanOf", ownerID);
                                var playerClan = clanPlugin.Call("GetClanOf", player.userID);
                                
                                if (ownerClan != null && playerClan != null && ownerClan == playerClan)
                                {
                                    return true;
								}
							}
						}
                        catch (Exception ex)
                        {
						}
					}
				}
				
                return false;
			}
			
            public void Deactivate()
            {
                isActive = false;
			}
		}
		
        void OnItemUpgrade(Item item, Item upgraded, BasePlayer player)
        {
            if(item.info.itemid == configData.Fermsettings.id && item.skin == configData.Fermsettings.SkinID)
            {
                rust.RunServerCommand($"ec.give {player.userID} {(float)(10 * configData.Mon)}");
                IQEconomic?.Call("API_ADD_BALANCE", player.userID, 10 * configData.Mon);
				
                upgraded.Remove();
			}
		}
		
        void Unload()
        {
            foreach(var a in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(a, "Main.");
			}
			
            ElectricBattery[] batteries = GameObject.FindObjectsOfType<ElectricBattery>();
			
            data.batt.Clear();
            foreach (var batt in batteries){
                if(batt != null && CheckMailBox(batt))
                {
                    ElectricBatteryHelper batteryHelpers = batt.GetComponent<ElectricBatteryHelper>();
                    if(batteryHelpers != null)
                    {
                        if(!data.batt.ContainsKey(batt.OwnerID))
						data.batt.Add(batt.OwnerID, new List<int>());
                        data.batt[batt.OwnerID].Add(batt.GetInstanceID());
						
                        UnityEngine.Object.DestroyImmediate(batteryHelpers);
					}
                    
                    TeslaTrapHelper teslaTrapHelper = batt.GetComponent<TeslaTrapHelper>();
                    if (teslaTrapHelper != null)
                    {
                        teslaTrapHelper.Deactivate();
					}
				}
			}
            SaveData();
            if (_imageUI != null)
            {
                _imageUI.UnloadImages();
                _imageUI = null;
			}
            _ = null;
		}
		
        bool CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            ElectricBattery batt = entity?.GetComponent<ElectricBattery>();
            if(batt != null && CheckMailBox(batt))
            {
                ElectricBatteryHelper batteryHelpers = batt.GetComponent<ElectricBatteryHelper>();
                if(batteryHelpers != null && data?.batt?.ContainsKey(batt.OwnerID) == true)
                {
                    data.batt[batt.OwnerID].Remove(batt.GetInstanceID());
                    SaveData();
				}
			}
            var io = entity as IOEntity;
            if (io != null)
            {
                RemoveFarmByComponentInstance(io.GetInstanceID());
			}
            return true;
		}
		
        void FlyingText()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is Mailbox mailbox)
                {
                    if (mailbox.skinID == 2436472234)
                    {
                        Vector3 mailboxPosition = mailbox.transform.position;
                        int much = mailbox.inventory.GetAmount(-126305173, false);
                        foreach (var player in BasePlayer.activePlayerList)
                        {
                            float distance = Vector3.Distance(player.transform.position, mailboxPosition);
                            if (distance <= 5f)
                            {
                                player.SendConsoleCommand("ddraw.text", 3f, Color.green, mailboxPosition + Vector3.up * 1.5f, $"ФЕРМА\nМонет: {much}");
							}
						}
					}
				}
			}
		}
		
        void FlagSet(BasePlayer player, BasePlayer.PlayerFlags flag, bool set)
        {
            if (player.Connection.authLevel == 0)
            {
                player.SetPlayerFlag(flag, set); 
                player.SendNetworkUpdate();
			}
		}
		
        void Hui()
        {
            timer.Every(3f, () => {
                FlyingText();
			});
		}
		
        void OnServerInitialized()
        {   
            Hui();
            ItemDefinition item = ItemManager.FindItemDefinition(configData.Fermsettings.id);
            item.stackable = 1000;
			
            _ = this;
			
            _imageUI = new ImageUI();
            _imageUI.DownloadImage();
			
            ScheduleFarmRestoration();
		}
		
        private HashSet<int> splitterCreationLock = new HashSet<int>();
        private Timer farmsRestoreTimer;
        private int farmsRestoreAttempts = 0;
		
        void Loaded()
        {
            try
            {
                LoadFarmsData();
                ScheduleFarmRestoration();
			}
            catch (Exception ex)
            {
                _.PrintError($"[Loaded] {ex.Message}\n{ex.StackTrace}");
			}
		}
		
        private void ScheduleFarmRestoration()
        {
            farmsRestoreAttempts = 0;
            farmsRestoreTimer?.Destroy();
            farmsRestoreTimer = timer.Every(5f, () =>
				{
					farmsRestoreAttempts++;
					bool allRestored = TryRestoreFarmsOnce();
					if (allRestored || farmsRestoreAttempts >= 6)
					{
						farmsRestoreTimer?.Destroy();
						farmsRestoreTimer = null;
						float now = Time.realtimeSinceStartup;
						foreach (var kvp in playerFarms)
                        lastFarmPlacedTime[kvp.Key] = now;
						foreach (var owner in playerFarms.Keys.ToList())
                        EnforceFarmLimit(owner);
					}
				});
		}
		
        private bool TryRestoreFarmsOnce()
        {
            return RebuildFarmsFromWorld();
		}
		
        private bool RebuildFarmsFromWorld()
        {
            try
            {
                var rebuilt = new Dictionary<ulong, List<FarmData>>();
                int found = 0;
				
                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    var mailbox = entity as Mailbox;
                    if (mailbox == null) continue;
                    if (mailbox.IsDestroyed) continue;
                    if (mailbox.skinID != 2436472234) continue;
					
                    var parentEntity = mailbox.GetParentEntity();
                    var battery = parentEntity as ElectricBattery;
                    if (battery == null) continue;
					
                    var ownerID = battery.OwnerID;
                    if (ownerID == 0) continue;
					
                    IOEntity split = null;
                    if (battery.inputs != null && battery.inputs.Length > 0 && battery.inputs[0]?.connectedTo != null)
					split = battery.inputs[0].connectedTo.Get(true) as IOEntity;
                    if (split == null) continue;
                    if (split.ShortPrefabName != "splitter") continue;
					
                    var components = GetComponents(split);
                    if (components == null) continue;
					
                    var helper = battery.GetComponent<ElectricBatteryHelper>();
                    if (helper == null) helper = battery.gameObject.AddComponent<ElectricBatteryHelper>();
                    helper.AddChildSplit(split);
                    helper.AddChildMailBox(mailbox);
					
                    if (configData.TeslaTrap.Enabled)
                    {
                        var teslaTrapHelper = battery.GetComponent<TeslaTrapHelper>();
                        if (teslaTrapHelper == null)
						teslaTrapHelper = battery.gameObject.AddComponent<TeslaTrapHelper>();
                        teslaTrapHelper.Initialize(battery, ownerID, this);
					}
					
					var farmData = new FarmData
					{
						SplitterID = split.GetInstanceID(),
						ComponentIDs = components.Values.Select(c => c.GetInstanceID()).ToList()
					};
					
					if (!rebuilt.ContainsKey(ownerID))
					rebuilt[ownerID] = new List<FarmData>();
					if (!rebuilt[ownerID].Any(f => f.SplitterID == farmData.SplitterID))
					{
						rebuilt[ownerID].Add(farmData);
						found++;
					}
				}
				
                foreach (var entity in BaseNetworkable.serverEntities)
                {
					var split = entity as IOEntity;
					if (split == null) continue;
					if (split.ShortPrefabName != "splitter") continue;
					
					var components = GetComponents(split);
					if (components == null) continue;
					if (!components.ContainsKey("smallrechargablebattery.deployed") || !components.ContainsKey("rfbroadcaster")) continue;
					var rb = components["rfbroadcaster"] as RFBroadcaster;
					if (rb == null || rb.frequency != configData.Fermsettings.hzn) continue;
					var battery = components["smallrechargablebattery.deployed"] as ElectricBattery;
					if (battery == null) continue;
					var ownerID = battery.OwnerID;
					if (ownerID == 0) continue;
					
					if (!rebuilt.ContainsKey(ownerID))
					rebuilt[ownerID] = new List<FarmData>();
					if (rebuilt[ownerID].Any(f => f.SplitterID == split.GetInstanceID()))
					continue;
					
					var helper2 = battery.GetComponent<ElectricBatteryHelper>();
					if (helper2 == null) helper2 = battery.gameObject.AddComponent<ElectricBatteryHelper>();
					helper2.AddChildSplit(split);
					
					if (!CheckMailBox(battery))
					{
						SpawnMailBox(battery, split);
						var children = battery.children;
						if (children != null)
						{
							foreach (var child in children)
							{
								if (child != null && child.ShortPrefabName == "mailbox.deployed")
								{
									var mb = child as Mailbox;
									if (mb != null)
									{
										helper2.AddChildMailBox(mb);
										break;
									}
								}
							}
						}
					}
					else
					{
						var children = battery.children;
						if (children != null)
						{
							foreach (var child in children)
							{
								if (child != null && child.ShortPrefabName == "mailbox.deployed")
								{
									var mb = child as Mailbox;
									if (mb != null)
									{
										helper2.AddChildMailBox(mb);
										break;
									}
								}
							}
						}
					}
					
					if (configData.TeslaTrap.Enabled)
					{
						var teslaTrapHelper = battery.GetComponent<TeslaTrapHelper>();
						if (teslaTrapHelper == null)
						teslaTrapHelper = battery.gameObject.AddComponent<TeslaTrapHelper>();
						teslaTrapHelper.Initialize(battery, ownerID, this);
					}
					
					rebuilt[ownerID].Add(new FarmData
						{
							SplitterID = split.GetInstanceID(),
							ComponentIDs = components.Values.Select(c => c.GetInstanceID()).ToList()
						});
						found++;
				}
				
                playerFarms = rebuilt;
                SaveFarmsData();
				
                float now = Time.realtimeSinceStartup;
                foreach (var kvp in playerFarms)
				lastFarmPlacedTime[kvp.Key] = now;
                foreach (var owner in playerFarms.Keys.ToList())
				EnforceFarmLimit(owner);
				
                return true; 
			}
			catch
			{
                return false;
			}
		}
		
		static bool AreListsEqual<T>(List<T> list1, List<T> list2)
		{
			if (list1.Count != list2.Count)
			return false;
			
			list1.Sort();
			list2.Sort();
			
			return list1.SequenceEqual(list2);
		}
		
		List<string> shortnameComp = new List<string>{"electric.flasherlight.deployed", "smallrechargablebattery.deployed", "rfbroadcaster"};
		public Dictionary<string, IOEntity> GetComponents(IOEntity entity)
		{
			if (entity == null) return null;
			
			Dictionary<string, IOEntity> connectedComponents = new Dictionary<string, IOEntity>();
			List<string> connectedComponentsShortPrefabName = new List<string>();
			
			if (entity.outputs == null) return null;
			
			foreach (var slot in entity.outputs)
			{
                if (slot?.connectedTo != null)
                {
					var connectedObject = slot.connectedTo.Get(true);
					
					if (connectedObject != null)
					{
						connectedComponents[connectedObject.ShortPrefabName] = connectedObject;
						connectedComponentsShortPrefabName.Add(connectedObject.ShortPrefabName);
					}
				}
			}
			
			if (shortnameComp == null || !AreListsEqual(shortnameComp, connectedComponentsShortPrefabName))
			return null;
			
			return connectedComponents;
		}
		
		void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
		{
			if(entity?.ShortPrefabName == "smallrechargablebattery.deployed")
			{
                if(data?.batt?.ContainsKey(entity.OwnerID) == true)
                {
					var ent = entity.GetComponent<BaseEntity>();
					if(CheckMailBox(entity.GetComponent<IOEntity>()))
					{
						if(data.batt[entity.OwnerID].Contains(entity.GetInstanceID()))
						{
							data.batt[entity.OwnerID].Remove(entity.GetInstanceID());
							SaveData();
						}
					}
				}
			}
		}
		
		void DestroyMeshCollider(BaseEntity ent) {
			foreach (var mesh in ent.GetComponentsInChildren<MeshCollider>()) {
                UnityEngine.Object.DestroyImmediate(mesh);
			}
		}
		
		void DestroyGroundComp(BaseEntity ent) {
			UnityEngine.Object.DestroyImmediate(ent.GetComponent<DestroyOnGroundMissing>());
			UnityEngine.Object.DestroyImmediate(ent.GetComponent<GroundWatch>());
		}
		
		List<ulong> playerNotifed = new List<ulong>();
		private void SpawnMailBox(IOEntity entity, IOEntity split)
		{
			try
			{
                if (entity == null || split == null) return;
                if (CheckMailBox(entity)) return;
                var ownerID = entity.GetComponent<BaseEntity>()?.OwnerID ?? 0;
                if (ownerID != 0)
                {
					var compsCheck = GetComponents(split);
					if (compsCheck == null) return;
					var existsOnThis = playerFarms.ContainsKey(ownerID) && playerFarms[ownerID].Any(f => f.SplitterID == split.GetInstanceID());
					if (!existsOnThis && !CanPlayerCreateFarm(ownerID))
					return;
					foreach (var c in compsCheck.Values)
					{
						if (c == null) return;
						if (IsComponentUsedAnywhere(c.GetInstanceID()) && !existsOnThis)
						return;
					}
					int batteries = 0; bool anyMailbox = false;
					if (split.outputs != null)
					{
						foreach (var slot in split.outputs)
						{
							var conn = slot?.connectedTo?.Get(true);
							if (conn == null) continue;
							if (conn.ShortPrefabName == "smallrechargablebattery.deployed")
							{
								batteries++;
								var eb = conn as ElectricBattery;
								if (eb != null && CheckMailBox(eb)) anyMailbox = true;
							}
						}
					}
					if (anyMailbox)
					{
						return;
					}
					if (batteries > 1)
					{
						
						if (existsOnThis) return;
					}
				}
                Vector3 worldHandlePosition = entity.transform.TransformPoint(entity.outputs[0].handlePosition);
                Mailbox mailbox = GameManager.server.CreateEntity("assets/prefabs/deployable/mailbox/mailbox.deployed.prefab", entity.transform.position, entity.transform.rotation * Quaternion.Euler(0f, -90f, 0f)) as Mailbox;
                if (mailbox == null) return;
                BasePlayer player = BasePlayer.FindByID(ownerID);
                DestroyMeshCollider(mailbox);
                DestroyGroundComp(mailbox);
                mailbox.allowedItems = new ItemDefinition[0];
                mailbox.allowedItems.Append(ItemManager.FindItemDefinition(configData.Fermsettings.id));
                entity.AddChild(mailbox);
                mailbox.skinID = 2436472234;
                mailbox.Spawn();
                mailbox.SendNetworkUpdate();
                var electricBatteryHelper = entity.gameObject.GetComponent<ElectricBatteryHelper>();
                if (electricBatteryHelper == null)
				electricBatteryHelper = entity.gameObject.AddComponent<ElectricBatteryHelper>();
                electricBatteryHelper.AddChildSplit(split);
                electricBatteryHelper.AddChildMailBox(mailbox);
                
                if (configData.TeslaTrap.Enabled)
                {
					var teslaTrapHelper = entity.gameObject.GetComponent<TeslaTrapHelper>();
					if (teslaTrapHelper == null)
					teslaTrapHelper = entity.gameObject.AddComponent<TeslaTrapHelper>();
					teslaTrapHelper.Initialize(entity, ownerID, this);
				}
                
                if (!data.batt.ContainsKey(ownerID))
                {
					data.batt[ownerID] = new List<int>();
				}
                if (!data.batt[ownerID].Contains(entity.GetInstanceID()))
				data.batt[ownerID].Add(entity.GetInstanceID());
                SaveData();
                entity.SendNetworkUpdate();
			}
			catch (Exception ex)
			{
                _.PrintError($"[SpawnMailBox] {ex.Message}\n{ex.StackTrace}");
			}
		}
		
		private bool CheckMailBox(IOEntity entity)
		{
			if (entity == null) return false;
			var child = entity.children;
			if (child == null) return false;
			foreach (var a in child)
			{
                if (a != null && a.ShortPrefabName == "mailbox.deployed")
				return true;
			}
			return false;
		}
		
		private bool IsFarmValid(IOEntity splitter)
		{
			if (splitter == null || splitter.IsDestroyed || splitter.Health() <= 0) return false;
			
			var components = GetComponents(splitter);
			if (components == null) return false;
			
			if (!components.ContainsKey("electric.flasherlight.deployed") ||
                !components.ContainsKey("smallrechargablebattery.deployed") ||
			!components.ContainsKey("rfbroadcaster"))
			{
                return false;
			}
			
			foreach (var component in components.Values)
			{
                if (component == null || component.IsDestroyed || component.Health() <= 0)
                {
					return false;
				}
			}
			
			var rfbroadcaster = components["rfbroadcaster"] as RFBroadcaster;
			if (rfbroadcaster == null || rfbroadcaster.frequency != configData.Fermsettings.hzn)
			{
                return false;
			}
			
			if (rfbroadcaster.inputs == null || rfbroadcaster.inputs.Length == 0 || 
			rfbroadcaster.inputs[0]?.connectedTo == null)
			{
                return false;
			}
			
			var connectedInput = rfbroadcaster.inputs[0].connectedTo.Get(true);
			if (connectedInput == null || connectedInput.GetInstanceID() != splitter.GetInstanceID())
			{
                return false;
			}
			
			return true;
		}
		
		private bool IsComponentUsedAnywhere(int instanceId)
		{
			foreach (var farms in playerFarms.Values)
			{
                foreach (var farm in farms)
                {
					if (farm.SplitterID == instanceId) return true;
					if (farm.ComponentIDs != null && farm.ComponentIDs.Contains(instanceId)) return true;
				}
			}
			return false;
		}
		
		private bool RemoveFarmByComponentInstance(int instanceId)
		{
			foreach (var kvp in playerFarms.ToList())
			{
                var ownerId = kvp.Key;
                var farms = kvp.Value;
                var target = farms.FirstOrDefault(f => f.SplitterID == instanceId || (f.ComponentIDs != null && f.ComponentIDs.Contains(instanceId)));
                if (target != null)
                {
					RemoveFarm(ownerId, target.SplitterID);
					return true;
				}
			}
			return false;
		}
		
		private void TrackFarmComponents(IOEntity entity, ulong ownerID)
		{
			if (entity == null || ownerID == 0) return;
			
			var components = GetComponents(entity);
			if (components == null) return;
			
			foreach (var component in components.Values)
			{
                if (component != null)
                {
					if (!data.batt.ContainsKey(ownerID))
					{
						data.batt[ownerID] = new List<int>();
					}
					if (!data.batt[ownerID].Contains(component.GetInstanceID()))
					{
						data.batt[ownerID].Add(component.GetInstanceID());
					}
				}
			}
			SaveData();
		}
		
		void OnEntityKill(BaseNetworkable entity)
		{
			if (entity == null) return;
			
			var ioEntity = entity as IOEntity;
			if (ioEntity == null) return;
			
			foreach (var playerFarmList in playerFarms.ToList())
			{
                var farmsToRemove = new List<FarmData>();
                
                foreach (var farm in playerFarmList.Value.ToList())
                {
					bool shouldRemove = false;
					
					if (farm.SplitterID == ioEntity.GetInstanceID())
					{
						shouldRemove = true;
					}
					else if (farm.ComponentIDs != null && farm.ComponentIDs.Contains(ioEntity.GetInstanceID()))
					{
						shouldRemove = true;
					}
					
					if (shouldRemove)
					{
						farmsToRemove.Add(farm);
						
						var player = BasePlayer.FindByID(playerFarmList.Key);
						if (player != null)
						{
							player.ChatMessage("Ферма была удалена из списка из-за уничтожения компонента");
						}
					}
				}
                
                foreach (var farm in farmsToRemove)
                {
					RemoveFarm(playerFarmList.Key, farm.SplitterID);
				}
			}
		}
		
		
		
		private Dictionary<ulong, float> lastFarmMessageTime = new Dictionary<ulong, float>();
		private const float farmMessageCooldown = 5f; 
		private Dictionary<ulong, float> lastFarmPlacedTime = new Dictionary<ulong, float>(); 
		object OnOutputUpdate(IOEntity entity)
		{
			try
			{
                if (entity == null) return null;
                var baseEntity = entity.GetComponent<BaseEntity>();
                if (baseEntity == null) return null;
                var ownerID = baseEntity.OwnerID;
                BasePlayer player = BasePlayer.FindByID(ownerID);
                var RFBroad = entity.GetComponent<RFBroadcaster>();
                if (RFBroad == null || ownerID == 0 || entity.ShortPrefabName != "rfbroadcaster") return null;
                if (configData?.Fermsettings == null) return null;
                if (RFBroad.frequency != configData.Fermsettings.hzn || entity.GetConnectedInputCount() < 1) return null;
                var input = entity.inputs[0]?.connectedTo?.Get(true);
                if (input == null || input.ShortPrefabName != "splitter") return null;
                var upstream = input.inputs != null && input.inputs.Length > 0 ? input.inputs[0]?.connectedTo?.Get(true) : null;
                if (upstream != null && upstream.ShortPrefabName == "splitter")
                {
					if (player != null) player.ChatMessage("Ферма должна питаться от источника (генератор/солнечная/ветер), а не от другого разветвителя");
					entity.Kill();
					return null;
				}
                
                
                int currentFarms = GetPlayerFarmCount(ownerID);
                int maxFarms = GetPlayerFarmLimit(ownerID);
                float now = Time.realtimeSinceStartup;
                float lastPlaced = 0f;
                lastFarmPlacedTime.TryGetValue(ownerID, out lastPlaced);
                
                var split = RFBroad.inputs[0]?.connectedTo?.Get(true);
                bool farmExistsOnThisSplitter = false;
                if (playerFarms.ContainsKey(ownerID))
                {
					var existingFarm = playerFarms[ownerID].FirstOrDefault(f => f.SplitterID == split.GetInstanceID());
					farmExistsOnThisSplitter = existingFarm != null;
				}
                
                if (currentFarms >= maxFarms)
                {
					var exFarm = playerFarms.ContainsKey(ownerID) ? playerFarms[ownerID].FirstOrDefault(f => f.SplitterID == split.GetInstanceID()) : null;
					if (exFarm != null && exFarm.ComponentIDs != null && exFarm.ComponentIDs.Contains(entity.GetInstanceID()))
					{
						return null; 
					}
					
					if (player != null && (now - lastPlaced > 3f))
					{
						if (!lastFarmMessageTime.ContainsKey(ownerID) || now - lastFarmMessageTime[ownerID] > farmMessageCooldown)
						{
							player.ChatMessage($"У вас уже установлено {currentFarms} ферм из {maxFarms} возможных");
							player.ChatMessage(configData.LimitMessage);
							lastFarmMessageTime[ownerID] = now;
						}
					}
					
					entity.Kill();
					return null;
				}
                
                if (!IsFarmValid(input))
                {
					if (player != null)
					{
						player.ChatMessage("Проверьте соединение компонентов фермы");
					}
					return null;
				}
                
                var split2 = RFBroad.inputs[0]?.connectedTo?.Get(true);
                var splitComponents2 = GetComponents(split2);
                if (splitComponents2 == null) return null;
                if (!splitComponents2.ContainsKey("smallrechargablebattery.deployed")) return null;
				
				foreach (var comp in splitComponents2.Values)
                {
					if (comp == null) { return null; }
					var id = comp.GetInstanceID();
					if (IsComponentUsedAnywhere(id) && !farmExistsOnThisSplitter)
					{
						if (player != null)
						{
							player.ChatMessage("Один из компонентов уже используется другой фермой");
						}
						if (comp.ShortPrefabName == "rfbroadcaster")
						{
							comp.Kill();
						}
						return null;
					}
				}
                
                if (playerFarms.ContainsKey(ownerID))
                {
					var existingFarm = playerFarms[ownerID].FirstOrDefault(f => f.SplitterID == split2.GetInstanceID());
					if (existingFarm != null)
					{
						if (existingFarm.ComponentIDs == null || !existingFarm.ComponentIDs.Contains(entity.GetInstanceID()))
						{
							entity.Kill();
						}
						return null;
					}
				}
                
				if (IsSplitterAlreadyUsed(split2)) {
					if (player != null) player.ChatMessage("На этом оборудовании уже установлена ферма другим игроком.");
					entity.Kill();
					return null;
				}
                var splitId = split2.GetInstanceID();
                if (splitterCreationLock.Contains(splitId))
                {
					return null;
				}
                splitterCreationLock.Add(splitId);
                try
                {
					SpawnMailBox(splitComponents2["smallrechargablebattery.deployed"], split2);
					AddFarm(ownerID, split2);
				}
                finally
                {
					splitterCreationLock.Remove(splitId);
				}
                if (player != null)
                {
					currentFarms = GetPlayerFarmCount(ownerID);
					player.ChatMessage($"Ферма успешно установлена ({currentFarms} из {maxFarms})");
					lastFarmMessageTime[ownerID] = now;
				}
                
                lastFarmPlacedTime[ownerID] = now;
                return null;
			}
			catch (Exception ex)
			{
                _.PrintError($"[OnOutputUpdate] {ex.Message}\n{ex.StackTrace}");
                return null;
			}
		}
		
		void Init()
		{
			_ = this;
			LoadVariables();
			LoadData();
			LoadFarmsData();
			
			if (permission != null)
			{
                permission.RegisterPermission("tpminingfarm.default", this);
                permission.RegisterPermission("tpminingfarm.vip", this);
                permission.RegisterPermission("tpminingfarm.premium", this);
                permission.RegisterPermission("tpminingfarm.elite", this);
                permission.RegisterPermission("tpminingfarm.drop", this);
                permission.RegisterPermission("tpminingfarm.showfarms", this);
			}
		}
		
		public string MenuContent = "TPMiningFarm.Content";
		
		private void chatMiningFarm(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, MenuContent);
			var container = new CuiElementContainer();  
			
			container.Add(new CuiPanel
				{
					CursorEnabled = true,
					RectTransform = { AnchorMin = "-0.315 -0.2656", AnchorMax = "1.298 1.272" },
					Image = { Color = "0 0 0 0", Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
				}, ".Mains", MenuContent);
				
				container.Add(new CuiElement
					{
						Name = MenuContent+"lay",
						Parent = MenuContent,
						Components ={
							new CuiRawImageComponent{Png = _imageUI.GetImage("MAIN_FON")},
						new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1"}}
					});
					
					container.Add(new CuiButton
						{
							Button ={
								Close = "Menu_UI",
								Color = "1 1 1 0",
							},
							RectTransform ={
								AnchorMin = "0.801 0.805",
								AnchorMax = "0.817 0.832",
							}
						}, MenuContent+"lay");
						
						container.Add(new CuiButton{
							Button ={
								Command = $"playerVideoTPMiningFarm {configData.urlviedo}",
								Color = "1 1 1 0"
							},
							Text ={
								Text = lang.GetMessage("INFO_VIDEOMANUAL", this, player.UserIDString),
								FontSize = 12,
								Align = TextAnchor.MiddleCenter,
								Color = "1 1 1 0.9",
								Font = "robotocondensed-regular.ttf"
							},
							RectTransform ={
								AnchorMin = "0.292 0.563",
								AnchorMax = "0.384 0.597",
							}
						}, MenuContent+"lay");
						
						container.Add(new CuiElement{
							Parent = MenuContent+"lay",
							Components ={
								new CuiTextComponent{
									Text = lang.GetMessage("INFO_RIGHT_UP", this, player.UserIDString),
									FontSize = 11,
									Align = TextAnchor.UpperCenter,
									Color = "1 1 1 0.9",
									Font = "robotocondensed-regular.ttf"
								},
								new CuiRectTransformComponent{
									AnchorMin = "0.48 0.67",
									AnchorMax = "0.8 0.77",
								},
							}
						});
						
						container.Add(new CuiElement{
							Parent = MenuContent+"lay",
							Components ={
								new CuiTextComponent{
									Text =  lang.GetMessage("INFO_RIGHT_DOWN", this, player.UserIDString),
									FontSize = 11,
									Align = TextAnchor.UpperCenter,
									Color = "1 1 1 0.9",
									Font = "robotocondensed-regular.ttf"
								},
								new CuiRectTransformComponent{
									AnchorMin = "0.48 0.19",
									AnchorMax = "0.8 0.35",
								},
							}
						});
						
						CuiHelper.AddUi(player, container);
		}
		
		[ConsoleCommand("playerVideoTPMiningFarm")]
		void paltdsag(ConsoleSystem.Arg arg)
		{
			BasePlayer baseplayer = arg.Player();
			if(baseplayer == null) return;
			string @string = arg.GetString(0, "");
			baseplayer.Command("client.playvideo", new object[]
				{
					@string
				});
		}
		
		[ChatCommand("showfarms")]
		private void CmdShowFarms(BasePlayer player, string command, string[] args)
		{
			if (permission == null || !permission.UserHasPermission(player.UserIDString, "tpminingfarm.showfarms"))
			{
                player.ChatMessage("У вас нет прав для использования этой команды.");
                return;
			}
			int count = 0;
			foreach (var kvp in playerFarms)
			{
                foreach (var farm in kvp.Value)
                {
					var splitter = BaseNetworkable.serverEntities.FirstOrDefault(e => e.GetInstanceID() == farm.SplitterID) as IOEntity;
					Vector3? pos = null;
					if (splitter != null)
					{
						pos = splitter.transform.position;
					}
					else if (farm.ComponentIDs != null && farm.ComponentIDs.Count > 0)
					{
						var comp = BaseNetworkable.serverEntities.FirstOrDefault(e => farm.ComponentIDs.Contains(e.GetInstanceID())) as BaseEntity;
						if (comp != null)
						pos = comp.transform.position;
					}
					if (pos != null)
					{
						player.ConsoleMessage($"Ферма: {pos.Value.x:F2} {pos.Value.y:F2} {pos.Value.z:F2} (владелец: {kvp.Key})");
						count++;
					}
				}
			}
			player.ChatMessage($"Найдено {count} ферм. Координаты выведены в консоль (F1)");
		}
		
		[ChatCommand("farminfo")]
		private void CmdFarmInfo(BasePlayer player, string command, string[] args)
		{
			int currentFarms = GetPlayerFarmCount(player.userID);
			int maxFarms = GetPlayerFarmLimit(player.userID);
			
			player.ChatMessage($"Ваши фермы: {currentFarms}/{maxFarms}");
			
			var activePerms = new List<string>();
			if (permission != null)
			{
                foreach (var perm in configData.FarmLimits.Keys)
                {
					if (permission.UserHasPermission(player.UserIDString, perm))
					{
						activePerms.Add($"{perm} ({configData.FarmLimits[perm]} ферм)");
					}
				}
			}
			
			if (activePerms.Count > 0)
			{
                player.ChatMessage($"Активные пермишены: {string.Join(", ", activePerms)}");
			}
			else
			{
                player.ChatMessage("У вас нет специальных пермишенов для ферм");
			}
			
			if (playerFarms.ContainsKey(player.userID) && playerFarms[player.userID].Count > 0)
			{
                player.ChatMessage("Ваши фермы:");
                foreach (var farm in playerFarms[player.userID])
                {
					var splitter = BaseNetworkable.serverEntities.FirstOrDefault(e => e.GetInstanceID() == farm.SplitterID) as IOEntity;
					if (splitter != null)
					{
						bool isValid = IsFarmValid(splitter);
						player.ChatMessage($"- Ферма на {splitter.transform.position}: {(isValid ? "работает" : "не работает")}");
					}
					else
					{
						player.ChatMessage("- Ферма: компоненты не найдены");
					}
				}
			}
		}
		
		[ChatCommand("farmdebug")]
		private void CmdFarmDebug(BasePlayer player, string command, string[] args)
		{
			if (permission == null || !permission.UserHasPermission(player.UserIDString, "tpminingfarm.showfarms"))
			{
                player.ChatMessage("У вас нет прав для использования этой команды.");
                return;
			}
			
			player.ChatMessage("=== ОТЛАДКА ФЕРМ ===");
			
			CleanupInvalidFarms(player.userID);
			
			int currentFarms = GetPlayerFarmCount(player.userID);
			int maxFarms = GetPlayerFarmLimit(player.userID);
			
			player.ChatMessage($"Лимит: {currentFarms}/{maxFarms}");
			
			if (playerFarms.ContainsKey(player.userID))
			{
                player.ChatMessage($"Всего ферм в списке: {playerFarms[player.userID].Count}");
                
                for (int i = 0; i < playerFarms[player.userID].Count; i++)
                {
					var farm = playerFarms[player.userID][i];
					var splitter = BaseNetworkable.serverEntities.FirstOrDefault(e => e.GetInstanceID() == farm.SplitterID) as IOEntity;
					
					if (splitter != null)
					{
						bool isValid = IsFarmValid(splitter);
						player.ChatMessage($"Ферма {i+1}: SplitterID={farm.SplitterID}, Valid={isValid}, Pos={splitter.transform.position}");
					}
					else
					{
						player.ChatMessage($"Ферма {i+1}: SplitterID={farm.SplitterID}, Valid=false (не найден)");
					}
				}
			}
			else
			{
                player.ChatMessage("Ферм в списке нет");
			}
		}
		
		[ChatCommand("tesla")]
		private void CmdTesla(BasePlayer player, string command, string[] args)
		{
			if (args.Length == 0)
			{
                player.ChatMessage("=== Gold TRAP УПРАВЛЕНИЕ ===");
                player.ChatMessage($"Статус: {(configData.TeslaTrap.Enabled ? "ВКЛЮЧЕН" : "ВЫКЛЮЧЕН")}");
                player.ChatMessage($"Радиус: {configData.TeslaTrap.Radius}м");
                player.ChatMessage($"Урон: {configData.TeslaTrap.Damage}");
                player.ChatMessage($"Интервал разрядов: {configData.TeslaTrap.ConstantDischargeInterval}с");
                player.ChatMessage("Режим: ПОСТОЯННЫЕ РАЗРЯДЫ");
                player.ChatMessage($"Игнорировать владельца: {(configData.TeslaTrap.IgnoreOwner ? "ДА" : "НЕТ")}");
                player.ChatMessage($"Игнорировать друзей: {(configData.TeslaTrap.IgnoreFriends ? "ДА" : "НЕТ")}");
                return;
			}
			
			if (permission == null || !permission.UserHasPermission(player.UserIDString, "tpminingfarm.showfarms"))
			{
                player.ChatMessage("У вас нет прав для использования этой команды.");
                return;
			}
			
			string action = args[0].ToLower();
			
			switch (action)
			{
                case "on":
				configData.TeslaTrap.Enabled = true;
				player.ChatMessage("Gold Trap эффект ВКЛЮЧЕН");
				break;
                case "off":
				configData.TeslaTrap.Enabled = false;
				player.ChatMessage("Gold Trap эффект ВЫКЛЮЧЕН");
				break;
                case "radius":
				if (args.Length > 1 && float.TryParse(args[1], out float radius))
				{
					configData.TeslaTrap.Radius = radius;
					player.ChatMessage($"Радиус Gold Trap изменен на {radius}м");
				}
				else
				{
					player.ChatMessage("Использование: /tesla radius <число>");
				}
				break;
                case "damage":
				if (args.Length > 1 && float.TryParse(args[1], out float damage))
				{
					configData.TeslaTrap.Damage = damage;
					player.ChatMessage($"Урон Gold Trap изменен на {damage}");
				}
				else
				{
					player.ChatMessage("Использование: /tesla damage <число>");
				}
				break;
                case "interval":
				if (args.Length > 1 && float.TryParse(args[1], out float interval))
				{
					configData.TeslaTrap.ConstantDischargeInterval = interval;
					player.ChatMessage($"Интервал разрядов Gold Trap изменен на {interval}с");
				}
				else
				{
					player.ChatMessage("Использование: /tesla interval <число>");
				}
				break;
                case "test":
				TestTeslaProtection(player);
				break;
                default:
				player.ChatMessage("Команды: on, off, radius, damage, interval, test");
				break;
			}
			
			SaveConfig(configData);
		}
		
		private void TestTeslaProtection(BasePlayer player)
		{
			player.ChatMessage("=== ТЕСТ ЗАЩИТЫ Gold TRAP ===");
			
			if (player.currentTeam != 0)
			{
                var team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                if (team != null)
                {
					player.ChatMessage($"Ваша команда: {team.teamID}");
					player.ChatMessage($"Участники команды: {team.members.Count}");
				}
                else
                {
					player.ChatMessage("Команда не найдена");
				}
			}
			else
			{
                player.ChatMessage("У вас нет команды");
			}
			
			try
			{
                var clanPlugin = this.plugins.Find("Clans");
                if (clanPlugin != null)
                {
					var playerClan = clanPlugin.Call("GetClanOf", player.userID);
					if (playerClan != null)
					{
						player.ChatMessage($"Ваш клан: {playerClan}");
					}
					else
					{
						player.ChatMessage("У вас нет клана");
					}
				}
                else
                {
					player.ChatMessage("Плагин кланов не найден");
				}
			}
			catch (Exception ex)
			{
                player.ChatMessage("Ошибка проверки клана");
			}
			
			player.ChatMessage($"Игнорировать владельца: {configData.TeslaTrap.IgnoreOwner}");
			player.ChatMessage($"Игнорировать друзей: {configData.TeslaTrap.IgnoreFriends}");
		}
		
		#region Configuration
			
			private static ConfigData configData;
			
			private class ConfigData
			{
				[JsonProperty("Настройка Фермы")]
				public FermSet Fermsettings;
				
				[JsonProperty("Ссылка на видеоинструкцию")]
				public string urlviedo;
				
				[JsonProperty("Сколько стоит одна монетка в IQEconomic")]
				public decimal Mon;
				
				[JsonProperty("Сообщение при достижении лимита ферм")]
				public string LimitMessage = "Чтобы ставить больше ферм, вы можете купить дополнительные фермы в нашем магазине dropshop.gamestores.app";
				
				[JsonProperty("Настройки Gold Trap эффекта")]
				public TeslaTrapSettings TeslaTrap = new TeslaTrapSettings();
				
				[JsonProperty("Лимиты ферм по привилегиям")]
				public Dictionary<string, int> FarmLimits = new Dictionary<string, int>
				{
					{"tpminingfarm.default", 1},
					{"tpminingfarm.vip", 2}, 
					{"tpminingfarm.premium", 3}, 
					{"tpminingfarm.elite", 5} 
				};
				
				public class FermSet
				{
					[JsonProperty("id предмета который будет использоваться для монетки")]
					public int id;
					[JsonProperty("Частота которую игроки должны вписать")]
					public int hzn;
					[JsonProperty("Название монетки")]
					public string Name;
					[JsonProperty("SkinID монетки")]
					public ulong SkinID;
					[JsonProperty("Падающее кол-во")]
					public int amount;
					[JsonProperty("Значение заряда аккумулятора для выдачи монетки")]
					public int watt;
					[JsonProperty("Максимальное кол-во монеток, которое может вместиться в одну ферму")]
					public int maxCoins;
				}
				
				public class TeslaTrapSettings
				{
					[JsonProperty("Включить Gold Trap эффект")]
					public bool Enabled = true;
					
					[JsonProperty("Радиус действия Gold Trap")]
					public float Radius = 3f;
					
					[JsonProperty("Урон от электрического разряда")]
					public float Damage = 10f;
					
					[JsonProperty("Интервал постоянных разрядов (в секундах)")]
					public float ConstantDischargeInterval = 0.3f;
					
					[JsonProperty("Эффект электрического разряда")]
					public string ElectricEffect = "assets/prefabs/locks/keypad/effects/lock.code.shock.prefab";
					
					[JsonProperty("Дополнительные эффекты (через запятую)")]
					public string[] AdditionalEffects = new string[]
					{
						"assets/bundled/prefabs/fx/build/repair_failed.prefab",
						"assets/bundled/prefabs/fx/impacts/additive/fire.prefab"
					};
					
					[JsonProperty("Игнорировать владельца фермы")]
					public bool IgnoreOwner = true;
					
					[JsonProperty("Игнорировать друзей владельца")]
					public bool IgnoreFriends = true;
					
					[JsonProperty("Разряжать только если найден хотя бы один чужой игрок")]
					public bool DischargeOnlyIfEnemyNearby = true;
				}
			}
			
			private void LoadVariables() => configData = Config.ReadObject<ConfigData>();
			
			protected override void LoadDefaultConfig()
			{
				configData = new ConfigData
				{
					Fermsettings = new ConfigData.FermSet()
					{
						id = -126305173,
						hzn = 202,
						Name = "123",
						SkinID = 642482233,
						amount = 1,
						watt = 100,
						maxCoins = 1000
					},
					urlviedo = "\x68\x74\x74\x70\x73\x3A\x2F\x2F\x74\x6F\x70\x70\x6C\x75\x67\x69\x6E\x2E\x72\x75\x2F\xD0\xA8\xD0\xBB\xD0\xB0\xD0\xBA\x2F\x4D\x69\x6E\x69\x6E\x67\x72\x75\x61\x72\x2E\x6D\x70\x34",
					LimitMessage = "Чтобы ставить больше ферм, вы можете купить дополнительные фермы в нашем магазине GoldMine.gamestores.app",
					TeslaTrap = new ConfigData.TeslaTrapSettings()
					{
						Enabled = true,
						Radius = 3f,
						Damage = 10f,
						ConstantDischargeInterval = 0.3f,
						ElectricEffect = "assets/prefabs/locks/keypad/effects/lock.code.shock.prefab",
						AdditionalEffects = new string[]
						{
							"assets/bundled/prefabs/fx/build/repair_failed.prefab",
							"assets/bundled/prefabs/fx/impacts/additive/fire.prefab"
						},
						IgnoreOwner = true,
						IgnoreFriends = true
					},
					FarmLimits = new Dictionary<string, int>
					{
						{"tpminingfarm.default", 1},
						{"tpminingfarm.vip", 2},
						{"tpminingfarm.premium", 3},
						{"tpminingfarm.elite", 5},
						{"tpminingfarm.drop", 4}
					}
				};
				SaveConfig(configData);
			}
			
			private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
			
		#endregion
		
		#region Data
			
			private static MiningData data = new MiningData();        
			private class MiningData
			{
				public Dictionary<ulong, List<int>> batt = new Dictionary<ulong, List<int>>();     
			}
			
			
			private class DATA
			{
				[JsonProperty("баланс")]
				public int balance;
			}
			
			private void LoadData() 
			{
				try 
				{
					data = Interface.GetMod().DataFileSystem.ReadObject<MiningData>("TPSystem/TPMining/MiningData");
					
					if (data == null)
					data = new MiningData();
					
					if (data.batt == null)
					data.batt = new Dictionary<ulong, List<int>>();
				}
				catch (Exception ex)
				{
					_.PrintError($"Error loading data: {ex.Message}");
					data = new MiningData
					{
						batt = new Dictionary<ulong, List<int>>()
					};
				}
			}
			
			private void SaveData()
			{ 
				if (data != null)
                Interface.GetMod().DataFileSystem.WriteObject("TPSystem/TPMining/MiningData", data);   
			}    
			void OnServerSave() => SaveFarmsData();
		#endregion      
		
		private new void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
				{
					["FARM_CREATED"] = "Поздравляем! Ферма успешно собрана.\nОжидайте монетки в почтовом ящике\nАктивных ферм {0}/{1}",
					["FARM_ACTIVED"] = "Фермы успешно загружены.\nОжидайте монетки в почтовом ящике\nАктивных ферм {0}/{1}",
					["FARM_LIMIT"] = "Вы достигли лимита ферм - {0} шт.",
					
					["MENU_HEADER"] = "Майнинг ферма",
					["INFO_VIDEOMANUAL"] = "Видеоинструкция",
					["INFO_RIGHT_UP"] = "Для сборки Фермы необходимо:\n(Малый генератор, Солнечная панель или Ветрогенератор)\nРазветвитель, Мигалка, Аккумулятор, Радиопередатчик.\nКогда всё подключено, задайте на Радиопередатчике частоту 1001 и нажмите Применить.",
					["INFO_RIGHT_DOWN"] = "\n\nЕсли всё правильно, сразу после подачи питания возле него появится Почтовый ящик, в который и будут майниться ваши МОНЕТЫ.\nБлок МОНЕТ с инвентаря можно перевести на Электронный кошелёк, накопив 10 МОНЕТ и нажав на них кнопку Улучшить.",
					
				}, this, "ru");
				
				lang.RegisterMessages(new Dictionary<string, string>
					{
						["FARM_CREATED"] = "Congratulations! The farm has been successfully built.\nWait for coins in the mailbox\nActive farms {0}/{1}",
						["FARM_ACTIVED"] = "Farms successfully loaded.\nWait for coins in the mailbox\nActive farms {0}/{1}",
						["FARM_LIMIT"] = "You have reached the farm limit - {0} pcs.",
						
						["MENU_HEADER"] = "Майнинг ферма",
						["INFO_VIDEOMANUAL"] = "Video instruction",
						["INFO_RIGHT_UP"] = "Для сборки Фермы необходимо:\n(Малый генератор, Солнечная панель или Ветрогенератор)\nРазветвитель, Мигалка, Аккумулятор, Радиопередатчик.\nКогда всё подключено, задайте на Радиопередатчике частоту 1001 и нажмите Применить.",
						["INFO_RIGHT_DOWN"] = "\n\nЕсли всё правильно, сразу после подачи питания возле него появится Почтовый ящик, в который и будут майниться ваши МОНЕТЫ.\nБлок МОНЕТ с инвентаря можно перевести на Электронный кошелёк, накопив 10 МОНЕТ и нажав на них кнопку Улучшить.",
						
					}, this, "en");
					PrintWarning("Языковой файл загружен успешно");
		}
		
		#region Images
			private static ImageUI _imageUI;
			private class ImageUI
			{
				private const String _path = "TPSystem/TPMining/Image/";
				private const String _printPath = "data/" + _path;
				private readonly Dictionary<String, ImageData> _images = new()
				{
					{ "MAIN_FON", new ImageData() }
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
					if(item.Value.Status == ImageStatus.Loaded)
					if (item.Value?.Id != null)
					FileStorage.server.Remove(uint.Parse(item.Value.Id), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
					
					_images?.Clear();
				}
				
				private IEnumerator ProcessDownloadImage(KeyValuePair<string, ImageData> image)
				{
					string url = "\x66\x69\x6C\x65\x3A\x2F\x2F" + Interface.Oxide.DataDirectory + (char)0x2F + _path + image.Key + "\x2E\x70\x6E\x67";
					
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
		
		private int GetPlayerFarmLimit(ulong userID)
		{
			if (configData?.FarmLimits == null || !configData.FarmLimits.ContainsKey("tpminingfarm.default"))
			return 1;
			
			int limit = configData.FarmLimits["tpminingfarm.default"];
			
			if (permission == null) return limit;
			
			string[] permissionOrder = { "tpminingfarm.elite", "tpminingfarm.premium", "tpminingfarm.vip", "tpminingfarm.drop" };
			
			foreach (string perm in permissionOrder)
			{
                if (configData.FarmLimits.ContainsKey(perm) && permission.UserHasPermission(userID.ToString(), perm))
                {
					limit = configData.FarmLimits[perm];
					break; 
				}
			}
			
			return limit;
		}
		
		private bool CanPlayerCreateFarm(ulong userID)
		{
			if (playerFarms == null) return true;
			if (!playerFarms.ContainsKey(userID)) return GetPlayerFarmLimit(userID) > 0;
			int registered = playerFarms[userID].Count;
			int limit = GetPlayerFarmLimit(userID);
			return registered < limit;
		}
		
		private bool IsSplitterAlreadyUsed(IOEntity splitter)
		{
			foreach (var farms in playerFarms.Values)
			{
                if (farms.Any(f => f.SplitterID == splitter.GetInstanceID()))
				return true;
			}
			return false;
		}
		
		private class FarmData
		{
			public int SplitterID { get; set; }
			public List<int> ComponentIDs { get; set; } = new List<int>();
		}
		
		private Dictionary<ulong, List<FarmData>> playerFarms = new Dictionary<ulong, List<FarmData>>();
		
		private void SaveFarmsData()
		{
			Interface.GetMod().DataFileSystem.WriteObject("TPSystem/TPMining/FarmData", playerFarms);
		}
		
		private void LoadFarmsData()
		{
			try
			{
                playerFarms = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, List<FarmData>>>("TPSystem/TPMining/FarmData");
                if (playerFarms == null)
				playerFarms = new Dictionary<ulong, List<FarmData>>();
			}
			catch
			{
                playerFarms = new Dictionary<ulong, List<FarmData>>();
			}
		}
		
		private void AddFarm(ulong ownerID, IOEntity splitter)
		{
			if (!playerFarms.ContainsKey(ownerID))
			playerFarms[ownerID] = new List<FarmData>();
			
			if (playerFarms[ownerID].Any(f => f.SplitterID == splitter.GetInstanceID()))
			return;
			
			var components = GetComponents(splitter);
			if (components == null) return;
			
			if (!CanPlayerCreateFarm(ownerID))
			{
                if (playerFarms[ownerID].Any(f => f.SplitterID == splitter.GetInstanceID()))
				return;
                return;
			}
			
			foreach (var comp in components.Values)
			{
                if (comp == null) return;
                var id = comp.GetInstanceID();
                if (IsComponentUsedAnywhere(id))
				return;
			}
			
			var farmData = new FarmData
			{
                SplitterID = splitter.GetInstanceID(),
                ComponentIDs = components.Values.Select(c => c.GetInstanceID()).ToList()
			};
			
			playerFarms[ownerID].Add(farmData);
			SaveFarmsData();
			EnforceFarmLimit(ownerID);
			
		}
		
		private void RemoveFarm(ulong ownerID, int splitterID)
		{
			if (playerFarms.ContainsKey(ownerID))
			{
                var farm = playerFarms[ownerID].FirstOrDefault(f => f.SplitterID == splitterID);
                if (farm != null)
                {
					if (data?.batt != null && data.batt.ContainsKey(ownerID) && farm.ComponentIDs != null)
					{
						foreach (var cid in farm.ComponentIDs)
						{
							var be = BaseNetworkable.serverEntities.FirstOrDefault(e => e.GetInstanceID() == cid) as BaseEntity;
							if (be != null && be.ShortPrefabName == "smallrechargablebattery.deployed")
							{
								data.batt[ownerID].Remove(cid);
								break;
							}
						}
						SaveData();
					}
					
					playerFarms[ownerID].Remove(farm);
					SaveFarmsData();
				}
			}
		}
		private int GetPlayerFarmCount(ulong ownerID)
		{
			if (!playerFarms.ContainsKey(ownerID))
			return 0;
			return playerFarms[ownerID].Count;
		}
		private void CleanupInvalidFarms(ulong ownerID)
		{
			if (!playerFarms.ContainsKey(ownerID)) return;
			var validFarms = new List<FarmData>();
			foreach (var farm in playerFarms[ownerID])
			{
				var splitter = BaseNetworkable.serverEntities.FirstOrDefault(e => e.GetInstanceID() == farm.SplitterID) as IOEntity;
				if (splitter != null && IsFarmValid(splitter))
				{
					validFarms.Add(farm);
				}
			}
			if (validFarms.Count != playerFarms[ownerID].Count)
			{
				playerFarms[ownerID] = validFarms;
				SaveFarmsData();
			}
		}
		private void EnforceFarmLimit(ulong ownerID)
		{
			int limit = GetPlayerFarmLimit(ownerID);
			if (!playerFarms.ContainsKey(ownerID)) return;
			var list = playerFarms[ownerID];
			if (list.Count <= limit) return;
			var toRemove = list.Skip(limit).ToList();
			foreach (var farm in toRemove)
			{
				RemoveFarm(ownerID, farm.SplitterID);
			}
		}
	}
}				
