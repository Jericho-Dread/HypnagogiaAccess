using HarmonyLib;
using TMPro;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HypnagogiaAccess
{
    public static class WorldTextPatchesSetup
    {
        private static Harmony _harmony;
        private static bool _textReaderPatched = false;
        private static bool _displayMessagePatched = false;
        private static readonly System.Collections.Generic.HashSet<string> _spokenWorldTexts =
            new System.Collections.Generic.HashSet<string>();
        private static readonly System.Collections.Generic.Dictionary<int, string> _lastDisplayMessageText =
            new System.Collections.Generic.Dictionary<int, string>();

        public static void Apply(Harmony harmony)
        {
            _harmony = harmony;
            TryPatchAll();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Plugin.Log.LogInfo($"[WORLD] Scene loaded: {scene.name}");
            _spokenWorldTexts.Clear();
            _lastDisplayMessageText.Clear();
            AnnounceSceneName(scene);
            if (!_textReaderPatched || !_displayMessagePatched)
                TryPatchAll();
        }

        private static void TryPatchAll()
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!_textReaderPatched)
                {
                    var t = asm.GetType("TextReader");
                    if (t != null && t.Namespace == null)
                    {
                        PatchMethod(t, "OnEnable", nameof(OnWorldTextShown));
                        PatchMethod(t, "DisplayMessage", nameof(OnWorldTextShown));
                        _textReaderPatched = true;
                        Plugin.Log.LogInfo("[WORLD] TextReader patched.");
                    }
                }

                if (!_displayMessagePatched)
                {
                    try
                    {
                        foreach (var type in asm.GetTypes())
                        {
                            if (type.Namespace != null || type.Name == "TextReader") continue;
                            var method = type.GetMethod("DisplayMessage",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (method != null)
                            {
                                PatchMethod(type, "DisplayMessage", nameof(OnWorldTextShown));
                                Plugin.Log.LogInfo($"[WORLD] {type.Name}.DisplayMessage patched.");
                            }
                        }
                        _displayMessagePatched = true;
                    }
                    catch { }
                }
            }
        }

        private static void PatchMethod(System.Type type, string methodName, string handlerName)
        {
            try
            {
                var method = type.GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null) return;
                var postfix = typeof(WorldTextPatchesSetup)
                    .GetMethod(handlerName, BindingFlags.Static | BindingFlags.Public);
                _harmony.Patch(method, postfix: new HarmonyMethod(postfix));
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[WORLD] Could not patch {type.Name}.{methodName}: {e.Message}");
            }
        }

        public static void OnWorldTextShown(object __instance)
        {
            try
            {
                var mono = __instance as MonoBehaviour;
                if (mono == null) return;
                if (!mono.isActiveAndEnabled) return;

                string combined = ExtractWorldText(mono);

                if (string.IsNullOrWhiteSpace(combined)) return;
                if (combined.Length < 3) return;
                if (LooksLikeNoise(combined)) return;

                string dedupKey = $"{mono.GetType().FullName}:{combined}";
                if (!_spokenWorldTexts.Add(dedupKey)) return;

                Plugin.Log.LogInfo($"[WORLD TEXT] {combined.Substring(0, System.Math.Min(80, combined.Length))}");
                Plugin.SR.Speak(combined, interrupt: false);
            }
            catch (System.Exception e) { Plugin.Log.LogError($"WorldText: {e.Message}"); }
        }

        private static string ExtractWorldText(MonoBehaviour mono)
        {
            string combined = "";

            // Read known string-backed fields first for popups that may not expose TMP text immediately.
            combined = AppendIfPresent(combined, ReadStringField(mono, "message"));
            combined = AppendIfPresent(combined, ReadStringField(mono, "info"));

            // Gather all TMP_Text children.
            var tmps = mono.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in tmps)
                combined = AppendIfPresent(combined, t?.text);

            // Some UI prefabs may still use legacy Text.
            var legacies = mono.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            foreach (var t in legacies)
                combined = AppendIfPresent(combined, t?.text);

            return combined;
        }

        public static void OnDisplayMessageUpdated(object __instance)
        {
            try
            {
                var mono = __instance as MonoBehaviour;
                if (mono == null) return;
                if (!mono.isActiveAndEnabled) return;

                string text = ExtractWorldText(mono);
                if (string.IsNullOrWhiteSpace(text)) return;
                if (text.Length < 3) return;
                if (LooksLikeNoise(text)) return;

                int id = mono.GetInstanceID();
                if (_lastDisplayMessageText.TryGetValue(id, out var lastText) &&
                    string.Equals(lastText, text, System.StringComparison.Ordinal))
                    return;

                _lastDisplayMessageText[id] = text;
                Plugin.Log.LogInfo($"[DISPLAY MESSAGE] {text.Substring(0, System.Math.Min(80, text.Length))}");
                Plugin.SR.Speak(text, interrupt: false);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"DisplayMessage: {e.Message}");
            }
        }

        private static string ReadStringField(MonoBehaviour mono, string fieldName)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = mono.GetType().GetField(fieldName, flags);
            return field?.GetValue(mono) as string ?? "";
        }

        private static string AppendIfPresent(string combined, string rawText)
        {
            string clean = System.Text.RegularExpressions.Regex
                .Replace(rawText ?? "", @"<[^>]+>", "").Trim();

            if (string.IsNullOrWhiteSpace(clean)) return combined;
            if (combined.Contains(clean)) return combined;
            return combined.Length > 0 ? $"{combined} {clean}" : clean;
        }

        private static bool LooksLikeNoise(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;

            int letters = 0;
            foreach (char c in text)
                if (char.IsLetter(c))
                    letters++;

            return letters < 2;
        }

        private static void AnnounceSceneName(Scene scene)
        {
            string clean = TextCleaner.Clean(scene.name);
            if (string.IsNullOrWhiteSpace(clean)) return;
            Plugin.SR.Speak($"Level: {clean}", interrupt: true);
        }
    }

    [HarmonyPatch]
    public class Patch_DisplayMessage_Update
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("DisplayMessage:Update");
        }

        static void Postfix(object __instance)
        {
            WorldTextPatchesSetup.OnDisplayMessageUpdated(__instance);
        }
    }

    [HarmonyPatch]
    public class Patch_DisplayMessage_Start
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("DisplayMessage:Start");
        }

        static void Postfix(object __instance)
        {
            WorldTextPatchesSetup.OnDisplayMessageUpdated(__instance);
        }
    }

    [HarmonyPatch]
    public class Patch_Billboard_Update
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Billboard:Update");
        }

        static void Postfix(object __instance)
        {
            WorldTextPatchesSetup.OnWorldTextShown(__instance);
        }
    }

    // Hook RPGTalk's museumText field - used for hub world info panels
    [HarmonyPatch(typeof(RPGTalk), "PutRightTextToShow")]
    public class Patch_RPGTalk_MuseumText
    {
        static void Postfix(RPGTalk __instance)
        {
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var museumField = typeof(RPGTalk).GetField("museumText", flags);
                if (museumField == null) return;

                var museumObj = museumField.GetValue(__instance);
                if (museumObj == null) return;

                // Check if museumText is active - if so, this is a hub info panel
                string text = "";
                if (museumObj is TMP_Text tmp && tmp.gameObject.activeInHierarchy)
                    text = tmp.text;
                else if (museumObj is GameObject go && go.activeInHierarchy)
                    text = go.GetComponentInChildren<TMP_Text>()?.text ?? "";

                if (string.IsNullOrWhiteSpace(text)) return;
                text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "").Trim();
                if (string.IsNullOrWhiteSpace(text)) return;

                Plugin.Log.LogInfo($"[MUSEUM TEXT] {text.Substring(0, System.Math.Min(80, text.Length))}");
                Plugin.SR.Speak(text, interrupt: false);
            }
            catch (System.Exception e) { Plugin.Log.LogError($"MuseumText: {e.Message}"); }
        }
    }
}
