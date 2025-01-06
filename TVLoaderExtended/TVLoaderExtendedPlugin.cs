using BepInEx;
using BepInEx.Logging;

using HarmonyLib;

using TVLoaderExtended.Utils;

namespace TVLoaderExtended
{
	[BepInPlugin(MyGUID, PluginName, VersionString)]
	public class TVLoaderExtendedPlugin : BaseUnityPlugin
	{
		private const string MyGUID = "Filigrani.TVLoaderExtended";
		private const string PluginName = "TVLoaderExtended";
		private const string VersionString = "1.1.2";

		private static readonly Harmony Harmony = new Harmony(MyGUID);
		public static ManualLogSource Log = new ManualLogSource(PluginName);

		private void Awake()
		{
			Log = Logger;

			Harmony.PatchAll();
			VideoManager.Load();
			Logger.LogInfo($"PluginName: {PluginName}, VersionString: {VersionString} is loaded. Video Count: {VideoManager.Videos.Count}");
		}

	}
}
