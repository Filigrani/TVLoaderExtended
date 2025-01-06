using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using BepInEx.Logging;


namespace TVLoaderExtended.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    internal class NewSave
    {
        [HarmonyPatch("firstDayAnimation")]
        [HarmonyPostfix]
        internal static void StartPatching()
        {
            StartOfRound Instance = StartOfRound.Instance;

            // We not need to do this if we are client.
            if (!Instance.NetworkManager.IsHost)
            {
                return;
            }

            List<UnlockableItem> List = Instance.unlockablesList.unlockables;



            foreach (UnlockableItem item in List)
            {
                if(item.unlockableName == "Television")
                {
                    UnlockShipItem(Instance, List.IndexOf(item), item.unlockableName);
                    return;
                }
            }
        }

        private static void UnlockShipItem(StartOfRound instance, int unlockableID, string name)
        {
            try
            {
                var unlockShipMethod = instance.GetType().GetMethod("UnlockShipObject",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                unlockShipMethod.Invoke(instance, new object[] { unlockableID });
            }
            catch (NullReferenceException ex)
            {

            }
        }
    }
}
