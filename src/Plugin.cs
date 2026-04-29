using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HypnagogiaAccess
{
    [BepInPlugin("com.accessibility.hypnagogia", "Hypnagogia Accessibility Mod", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ScreenReaderBridge SR;

        private void Awake()
        {
            try
            {
                Log = Logger;
                Log.LogInfo("Step 1: Logger initialized.");

                SR = new ScreenReaderBridge();
                Log.LogInfo("Step 2: ScreenReaderBridge created.");

                bool speechReady = SR.Initialize();
                Log.LogInfo($"Step 3: Speech initialized. Ready={speechReady}");

                var harmony = new Harmony("com.accessibility.hypnagogia");
                harmony.PatchAll();
                Log.LogInfo("Step 4: Harmony patches applied.");

                WorldTextPatchesSetup.Apply(harmony);
                Log.LogInfo("Step 5: WorldText patches applied.");

                CutscenesSetup.Apply(harmony);
                Log.LogInfo("Step 6: Cutscene patches applied.");

                var go = new GameObject("HypnagogiaAccessWatcher");
                go.AddComponent<UIWatcher>();
                GameObject.DontDestroyOnLoad(go);

                // Initialize positional audio cue system
                AudioCues.Initialize();

                Log.LogInfo("Step 7: UIWatcher and AudioCues started.");
                Log.LogInfo("Hypnagogia Accessibility Mod loaded successfully.");
            }
            catch (System.Exception e)
            {
                if (Log != null)
                    Log.LogError($"FATAL ERROR in Awake: {e}");
            }
        }
    }
}
