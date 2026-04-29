using HarmonyLib;
using TMPro;
using System.Reflection;

namespace HypnagogiaAccess
{
    // -------------------------------------------------------
    // CUTSCENE PATCHES
    // Uses runtime reflection to find RPGTalkSkipCutscene
    // and RPGTalkCinematicBehaviour to avoid compile-time
    // type resolution issues
    // -------------------------------------------------------

    public static class CutscenesSetup
    {
        private static string _lastCinematicText = "";

        public static void Apply(Harmony harmony)
        {
            var postfix = typeof(CutscenesSetup)
                .GetMethod(nameof(OnCutsceneEnabled), BindingFlags.Static | BindingFlags.Public);
            var cinPostfix = typeof(CutscenesSetup)
                .GetMethod(nameof(OnCinematicFrame), BindingFlags.Static | BindingFlags.Public);

            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                // Patch RPGTalkSkipCutscene.OnEnable
                var skipType = asm.GetType("RPGTalkSkipCutscene");
                if (skipType != null)
                {
                    var onEnable = skipType.GetMethod("OnEnable",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (onEnable != null)
                        harmony.Patch(onEnable, postfix: new HarmonyMethod(postfix));
                    Plugin.Log.LogInfo("RPGTalkSkipCutscene patched.");
                }

                // Patch RPGTalkCinematicBehaviour.ProcessFrame
                var cinType = asm.GetType("RPGTalkCinematicBehaviour");
                if (cinType != null)
                {
                    var processFrame = cinType.GetMethod("ProcessFrame",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (processFrame != null)
                        harmony.Patch(processFrame, postfix: new HarmonyMethod(cinPostfix));
                    Plugin.Log.LogInfo("RPGTalkCinematicBehaviour patched.");
                }
            }
        }

        public static void OnCutsceneEnabled(object __instance)
        {
            var mono = __instance as UnityEngine.MonoBehaviour;
            if (mono == null) return;

            var tmp = mono.GetComponentInChildren<TMP_Text>();
            if (tmp == null) return;

            string text = tmp.text;
            if (string.IsNullOrWhiteSpace(text)) return;

            Plugin.SR.Speak(text, interrupt: true);
        }

        public static void OnCinematicFrame(object __instance)
        {
            var mono = __instance as UnityEngine.MonoBehaviour;
            if (mono == null) return;

            var tmp = mono.GetComponentInChildren<TMP_Text>();
            if (tmp == null) return;

            string text = tmp.text;
            if (string.IsNullOrWhiteSpace(text) || text == _lastCinematicText) return;

            _lastCinematicText = text;
            Plugin.SR.Speak(text, interrupt: true);
        }
    }
}
