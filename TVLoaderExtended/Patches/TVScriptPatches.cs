using HarmonyLib;

using System.Reflection;
using TVLoaderExtended.Utils;

using Unity.Netcode;

using UnityEngine;
using UnityEngine.Video;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace TVLoaderExtended.Patches
{

	[HarmonyPatch(typeof(TVScript))]
	internal class TVScriptPatches
	{
		private static FieldInfo currentClipProperty = typeof(TVScript).GetField("currentClip", BindingFlags.NonPublic | BindingFlags.Instance);
		private static FieldInfo currentTimeProperty = typeof(TVScript).GetField("currentClipTime", BindingFlags.NonPublic | BindingFlags.Instance);
		private static FieldInfo wasTvOnLastFrameProp = typeof(TVScript).GetField("wasTvOnLastFrame", BindingFlags.NonPublic | BindingFlags.Instance);
		private static FieldInfo timeSinceTurningOffTVProp = typeof(TVScript).GetField("timeSinceTurningOffTV", BindingFlags.NonPublic | BindingFlags.Instance);
		private static MethodInfo setMatMethod = typeof(TVScript).GetMethod("SetTVScreenMaterial", BindingFlags.NonPublic | BindingFlags.Instance);
		private static MethodInfo onEnableMethod = typeof(TVScript).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance);

        private static bool s_EverWasOn = false;

        private static RenderTexture renderTexture;

        private static List<string> m_Videos = new List<string>();
		private static int s_LastSeed = 0;

		private static int s_LastSeenIndex = -1;

		private static string GetVideoPath(int Index)
		{
			return GetFullPath(m_Videos[Index]);
        }

        private static int GetSeed(TVScript Instance)
		{
			return Instance.NetworkObjectId.ToString().GetHashCode();
		}

        private static void Shuffle<T>(List<T> List, int Seed)
        {
            System.Random RNG = new System.Random(Seed);
            int n = List.Count;
            for (int i = 0; i < (n - 1); i++)
            {
                int r = i + RNG.Next(n - i);
                T t = List[r];
                List[r] = List[i];
                List[i] = t;
            }
        }

		public static List<string> GetClearList(List<string> Pathes)
        {
            List<string> EternalList = new List<string>();
			for (int i = 0; (i < Pathes.Count); i++)
            {
                string Path = Pathes[i];
				string[] Sepa = { "plugins" };
				string[] Subs = Path.Split(Sepa, StringSplitOptions.None);
                string FilePath = Subs[1];
                EternalList.Add(FilePath);
            }
            EternalList.Sort();
            return EternalList;
        }

		public static string GetFullPath(string ShortPath)
		{
			foreach (string vid in VideoManager.Videos)
			{
				if (vid.EndsWith(ShortPath))
				{
					return vid;
				}
			}
			return "";
		}

        public static void ShuffleVideos(TVScript Instance)
        {
            m_Videos = GetClearList(VideoManager.Videos);
            TVLoaderExtendedPlugin.Log.LogInfo("[TVScriptExtended] ShuffleVideos " + GetSeed(Instance));
            Shuffle(m_Videos, GetSeed(Instance));

            for (int i = 0; (i < m_Videos.Count); i++)
            {
                TVLoaderExtendedPlugin.Log.LogInfo("[TVScriptExtended] m_Videos[" + i + "] " + m_Videos[i]);
            }
        }

		public static void ClientUpdate(TVScript Instance)
		{
            int currentClip = (int)currentClipProperty.GetValue(Instance);
            if (s_LastSeenIndex != currentClip)
			{
				s_LastSeenIndex = currentClip;
                Debug.Log("[ClientUpdate] currentClip " + currentClip + " Instance.tvOn " + Instance.tvOn);
                if (Instance.tvOn)
                {
                    PlayVideo(Instance);
                }
            }
        }
		
		public static bool PlayerIsHost(TVScript __instance)
		{
			if (__instance)
			{
				return __instance.NetworkManager.IsHost;
            }
			return false;
		}

        [HarmonyPrefix]
		[HarmonyPatch("Update")]
		public static bool Update(TVScript __instance)
		{
			if (renderTexture == null)
			{
				renderTexture = __instance.GetComponent<VideoPlayer>().targetTexture;
			}

			if (!PlayerIsHost(__instance))
			{
				ClientUpdate(__instance);
            }

            if (s_LastSeed != GetSeed(__instance))
            {
                s_LastSeed = GetSeed(__instance);
                ShuffleVideos(__instance);

                if (PlayerIsHost(__instance))
                {
                    currentClipProperty.SetValue(__instance, 0);
                    s_EverWasOn = false;
                }
            }

            return false;
		}

		[HarmonyPrefix]
		[HarmonyPatch("TurnTVOnOff")]
		public static bool TurnTVOnOff(TVScript __instance, bool on)
		{
            //TVLoaderExtendedPlugin.Log.LogInfo($"VideoPlayer Resolution: {currentVideoPlayer.targetTexture.width}x{currentVideoPlayer.targetTexture.height}");
			TVLoaderExtendedPlugin.Log.LogInfo($"TVOnOff: {on}");


            if (VideoManager.Videos.Count == 0) return false;

            int currentClip = (int)currentClipProperty.GetValue(__instance);
            __instance.tvOn = on;
            if (PlayerIsHost(__instance))
			{
                // Skip to the next video if this is not our first time turning on the TV
                if (on)
                {
                    if (!s_EverWasOn)
                    {
                        s_EverWasOn = true;
                    }
                    else
                    {
                        currentClip = (currentClip + 1) % VideoManager.Videos.Count;
                        currentClipProperty.SetValue(__instance, currentClip);
                    }
                    PlayVideo(__instance);
                }
            }
            else
            {
                if (on)
                {
                    currentTimeProperty.SetValue(__instance, 0f);
                }
            }
            if (on)
            {
				__instance.tvSFX.PlayOneShot(__instance.switchTVOn);
                WalkieTalkie.TransmitOneShotAudio(__instance.tvSFX, __instance.switchTVOn);
            }
            else
            {
                __instance.video.Stop();
                __instance.tvSFX.PlayOneShot(__instance.switchTVOff);
                WalkieTalkie.TransmitOneShotAudio(__instance.tvSFX, __instance.switchTVOff);
            }

            setMatMethod.Invoke(__instance, new object[] { on });
			return false;
		}

		[HarmonyPrefix]
		[HarmonyPatch("TVFinishedClip")]
		public static bool TVFinishedClip(TVScript __instance, VideoPlayer source)
		{
			if (__instance.tvOn && PlayerIsHost(__instance))
			{
                // Skip to the next video
                TVLoaderExtendedPlugin.Log.LogInfo("TVFinishedClip");
                int currentClip = (int)currentClipProperty.GetValue(__instance);
                if (VideoManager.Videos.Count > 0)
                    currentClip = (currentClip + 1) % VideoManager.Videos.Count;

                currentTimeProperty.SetValue(__instance, 0f);
                currentClipProperty.SetValue(__instance, currentClip);

                // Play it
                PlayVideo(__instance);
            }
			return false;
		}

		private static void PrepareVideo(TVScript instance, int index)
		{
			string VideoPath = GetVideoPath(index);

			GameObject.Destroy(instance.video);

			// Also prepare the next video
			VideoPlayer nextVideoPlayer = instance.gameObject.AddComponent<VideoPlayer>();
			nextVideoPlayer.playOnAwake = false;
			nextVideoPlayer.isLooping = false;
			nextVideoPlayer.source = VideoSource.Url;
			nextVideoPlayer.controlledAudioTrackCount = 1;
			nextVideoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
			nextVideoPlayer.SetTargetAudioSource(0, instance.tvSFX);
            TVLoaderExtendedPlugin.Log.LogInfo("Going to prepare: " + VideoPath);
            nextVideoPlayer.url = "file://"+ VideoPath;
			nextVideoPlayer.skipOnDrop = true;
			nextVideoPlayer.Prepare();

            nextVideoPlayer.prepareCompleted += (VideoPlayer source) => { TVLoaderExtendedPlugin.Log.LogInfo("Prepared video!"); };
            instance.video = nextVideoPlayer;
        }

		private static void PlayVideo(TVScript instance)
		{
			if (VideoManager.Videos.Count == 0) return;

            currentTimeProperty.SetValue(instance, 0f);

            PrepareVideo(instance, (int)currentClipProperty.GetValue(instance));

            onEnableMethod.Invoke(instance, new object[] { });


            TVLoaderExtendedPlugin.Log.LogInfo("TV Current Video Index "+ (int)currentClipProperty.GetValue(instance));

            instance.video.targetTexture = renderTexture;
			instance.video.Play();

            if (PlayerIsHost(instance))
            {
                instance.SyncTVServerRpc();
            }

            //PrepareVideo(instance);
        }
	}
}