using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core;
namespace Oxide.Plugins
{
	[Info("TPEconomic", "pluginfuel.ru", "20.0.2")]
	class TPEconomic : RustPlugin
	{
		[PluginReference] Plugin TPMenuSystem;
		[JsonProperty("Система экономики")] public Dictionary<ulong, float> DataEconomics = new Dictionary<ulong, float>();
		[HookMethod("API_GET_BALANCE")]
		public float API_GET_BALANCE(object player)
		{
			ulong id = ToUlong(player);
			if (!DataEconomics.ContainsKey(id)) return 0f;
			return DataEconomics[id];
		}
		[HookMethod("API_PUT_BALANCE_PLUS")]
		public void API_PUT_BALANCE_PLUS(object player, object money)
		{
			ulong id = ToUlong(player);
			float amount = ToFloat(money);
			if (!DataEconomics.ContainsKey(id)) DataEconomics[id] = 0f;
			DataEconomics[id] += amount;
			WriteData();
			AnoncePlayer(id, $"Вам зачислено {amount} ₽");
		}
		
        [HookMethod("API_PUT_BALANCE_MINUS")]
        public void API_PUT_BALANCE_MINUS(object player, object money)
        {
            ulong id = ToUlong(player);
            float amount = ToFloat(money);
            if (!DataEconomics.ContainsKey(id)) DataEconomics[id] = 0f;
            DataEconomics[id] -= amount;
            WriteData();
            AnoncePlayer(id, $"C баланса списано {amount} ₽");
		}
		
        private ulong ToUlong(object val)
        {
            if (val is ulong u) return u;
            if (val is string s && ulong.TryParse(s, out var id)) return id;
            if (val is int i) return (ulong)i;
            if (val is long l) return (ulong)l;
            return 0;
		}
        private float ToFloat(object val)
        {
            if (val is float f) return f;
            if (val is double d) return (float)d;
            if (val is int i) return i;
            if (val is long l) return l;
            if (val is string s && float.TryParse(s, out var v)) return v;
            return 0f;
		}
		
        [ConsoleCommand("ec.give")]
        void cmdMoneyGive(ConsoleSystem.Arg args)
        {
            if (!args.IsAdmin) return;
            var player = args.Args[0];
            var much = args.Args[1];
			
            API_PUT_BALANCE_PLUS(player, much);
            Puts($"Баланс выдан {player} - {much} ₽.");
		}
		
        private BasePlayer FindPlayer(string nameOrId)
        {
            foreach (var check in BasePlayer.activePlayerList)
            {
                if (check.displayName.ToLower().Contains(nameOrId.ToLower()) || check.userID.ToString() == nameOrId)
				return check;
			}
            return null;
		}
		
        private void AnoncePlayer(ulong player, string txt)
        {
            BasePlayer playerBS = FindPlayer(player.ToString());
            if(playerBS == null) return;
            playerBS.ChatMessage(txt);
		}
		
        void OnPlayerConnected(BasePlayer player)
        {
            RegisteredDataUser(player.userID);
		}
		
        void RegisteredDataUser(ulong player)
        {
            if (!player.IsSteamId()) return;
            if (!DataEconomics.ContainsKey(player))
			DataEconomics.Add(player, 1);
		}
		
        void OnServerInitialized()
        {
            foreach(BasePlayer player in BasePlayer.activePlayerList)
			OnPlayerConnected(player);
            timer.Every(360f, WriteData);
		}
		
        void Unload() => WriteData();
        private void OnServerShutdown() => Unload();
        private void Init() => ReadData();
		
        void ReadData()
        {
            DataEconomics = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>("TP/DataEconomics");
		}
        void WriteData() {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("TP/DataEconomics", DataEconomics);
		}
	}
}