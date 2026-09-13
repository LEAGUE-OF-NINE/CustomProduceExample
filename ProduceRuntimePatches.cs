using HarmonyLib;
using System;
using UnityEngine.Playables;

// USE THE FOLLOWING FILE IF YOU HAVE PROBLEMS WITH PLAYBACK NOT COMPLETING (change the GO name prefix and other stuff to accomodate for your specific case.)

namespace ProduceExample
{
    /// <summary>
    /// Runtime Harmony patches required for synthetic produce playback.
    /// These are NOT diagnostics; they are what makes the native produce
    /// Update loop cooperate with our custom ProduceBase.
    /// </summary>
    public static class ProduceRuntimePatches
    {
        // Toggle from Main.Load via config if you ever need to disable.
        public static bool Enabled = true;

        private const string ProduceGONamePrefix = "CustomBattleProduce_";
        private const double PinTime = 0.8673333336896278;
        private const double PinTolerance = 0.001;

        private static bool IsOurProduce(PlayableDirector d)
        {
            return d != null
                && d.gameObject != null
                && d.gameObject.name != null
                && d.gameObject.name.StartsWith(ProduceGONamePrefix, StringComparison.Ordinal);
        }

        // --- Suppress the native per-frame Pause() clamp -------------------
        [HarmonyPatch(typeof(PlayableDirector), nameof(PlayableDirector.Pause), new Type[0])]
        [HarmonyPrefix]
        public static bool Director_Pause_Pre(PlayableDirector __instance)
        {
            if (!Enabled) return true;
            if (!IsOurProduce(__instance)) return true;

            // Native loop calls Pause() every frame near end-of-produce.
            // We rely on director.stopped firing instead, so swallow this.
            return false;
        }

        // --- Suppress the native per-frame set_time() pin ------------------
        [HarmonyPatch(typeof(PlayableDirector), "set_time", new Type[] { typeof(double) })]
        [HarmonyPrefix]
        public static bool Director_SetTime_Pre(PlayableDirector __instance, double value)
        {
            if (!Enabled) return true;
            if (!IsOurProduce(__instance)) return true;

            double current = __instance.time;

            bool isObservedPin = Math.Abs(value - PinTime) < PinTolerance;
            bool isBackwardPin = value < current && (current - value) < 0.05;

            if (isObservedPin || isBackwardPin)
                return false; // skip

            return true;
        }

        // --- Skip SetLookRotateObject (prefab has no look-rotate list) -----
        // Optional. The base method is a no-op when the mode field is 0,
        // but if anything ever populates it with a null list it will throw.
        [HarmonyPatch(typeof(ProduceBase), nameof(ProduceBase.SetLookRotateObject))]
        [HarmonyPrefix]
        public static bool ProduceBase_SetLookRotateObject_Skip(ProduceBase __instance)
        {
            return false;
        }
    }
}
