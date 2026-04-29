using HarmonyLib;
using TMPro;
using UnityEngine.UI;
using System.Reflection;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HypnagogiaAccess
{
    public static class TextCleaner
    {
        public static string Clean(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            input = Regex.Replace(input, @"<[^>]+>", "");
            input = Regex.Replace(input, @"\[[^\]]+\]", "");
            // Strip Unicode combining characters (glitch text fix)
            input = new string(System.Array.FindAll(input.ToCharArray(),
                c => c < 0x0300 || (c > 0x036F && c < 0x1DC0) || c > 0x1DFF));
            input = input.Trim('"');
            input = Regex.Replace(input, @"\s+", " ").Trim();
            return input;
        }
    }

    [HarmonyPatch(typeof(RPGTalk), "PutRightTextToShow")]
    public class Patch_RPGTalk_DialogueLine
    {
        private static string _lastSpokenLine = "";
        private static Coroutine _activeCoroutine = null;

        public static void ResetLastLine() { _lastSpokenLine = ""; }

        static void Postfix(RPGTalk __instance)
        {
            if (_activeCoroutine != null)
                UIWatcher.Instance?.StopCoroutine(_activeCoroutine);
            _activeCoroutine = UIWatcher.Instance?.StartCoroutine(
                WaitForFullLine(__instance));
        }

        static IEnumerator WaitForFullLine(RPGTalk instance)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var textUIField = typeof(RPGTalk).GetField("textUIObj", flags);
            var speakerField = typeof(RPGTalk).GetField("dialogerObj", flags);

            yield return null;

            string lastText = "";
            int stableFrames = 0;
            float timeout = 30f;
            string startScene = SceneManager.GetActiveScene().name;

            while (timeout > 0f)
            {
                yield return null;
                timeout -= Time.unscaledDeltaTime;

                if (SceneManager.GetActiveScene().name != startScene) yield break;

                try
                {
                    if (instance == null) yield break;
                    var textGO = textUIField?.GetValue(instance) as GameObject;
                    if (textGO == null) continue;

                    string cleaned = TextCleaner.Clean(
                        textGO.GetComponentInChildren<TMP_Text>()?.text ?? "");

                    if (!string.IsNullOrWhiteSpace(cleaned) && cleaned == lastText)
                    {
                        stableFrames++;
                        if (stableFrames >= 2)
                        {
                            if (cleaned == _lastSpokenLine) yield break;
                            _lastSpokenLine = cleaned;

                            var speakerGO = speakerField?.GetValue(instance) as GameObject;
                            string speaker = TextCleaner.Clean(
                                speakerGO?.GetComponentInChildren<TMP_Text>()?.text ?? "");

                            string toSpeak = string.IsNullOrWhiteSpace(speaker)
                                ? cleaned : $"{speaker}: {cleaned}";

                            Plugin.Log.LogInfo($"[DIALOGUE] {toSpeak}");
                            UIWatcher.OnNewDialogueLine(speaker, cleaned);
                            yield break;
                        }
                    }
                    else
                    {
                        stableFrames = 0;
                        lastText = cleaned;
                    }
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogError($"WaitForFullLine: {e.Message}");
                    yield break;
                }
            }
        }
    }

    [HarmonyPatch(typeof(RPGTalk), "LookForNewTalk")]
    public class Patch_RPGTalk_LookForNewTalk
    {
        static void Prefix()
        {
            Patch_RPGTalk_DialogueLine.ResetLastLine();
            UIWatcher.SetDialogueActive(true);
        }
    }

    [HarmonyPatch(typeof(RPGTalk), "LookForChoices")]
    public class Patch_RPGTalk_Choices
    {
        private static string _lastAnnouncement = "";
        private static float _lastTime = -99f;
        private const float DEDUP_WINDOW = 2f;

        static void Postfix(RPGTalk __instance)
        {
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var parent = typeof(RPGTalk).GetField("choicesParent", flags)
                    ?.GetValue(__instance) as Transform;
                if (parent == null) return;

                var buttons = parent.GetComponentsInChildren<Button>();
                if (buttons == null || buttons.Length == 0) return;

                string announcement = $"{buttons.Length} choices: ";
                for (int i = 0; i < buttons.Length; i++)
                {
                    string label = TextCleaner.Clean(
                        buttons[i].GetComponentInChildren<TMP_Text>()?.text ?? buttons[i].name);
                    announcement += $"{i + 1}: {label}";
                    if (i < buttons.Length - 1) announcement += ", ";
                }

                float now = Time.unscaledTime;
                if (announcement == _lastAnnouncement && now - _lastTime < DEDUP_WINDOW)
                    return;

                _lastAnnouncement = announcement;
                _lastTime = now;

                Plugin.Log.LogInfo($"[CHOICES] {announcement}");
                UIWatcher.OnChoicesAppeared(announcement);
            }
            catch (System.Exception e) { Plugin.Log.LogError($"ChoicesPatch: {e}"); }
        }
    }

    [HarmonyPatch(typeof(RPGTalk), "LookForQuestions")]
    public class Patch_RPGTalk_Questions
    {
        static void Postfix(string __result)
        {
            if (string.IsNullOrWhiteSpace(__result)) return;
            UIWatcher.OnChoicesAppeared(__result);
        }
    }

    [HarmonyPatch(typeof(RPGTalk), "SelectButton")]
    public class Patch_RPGTalk_SelectButton
    {
        static void Prefix(Button __0)
        {
            if (__0 == null) return;

            string label = TextCleaner.Clean(
                __0.GetComponentInChildren<TMP_Text>()?.text ??
                __0.GetComponentInChildren<Text>()?.text ??
                __0.name);

            UIWatcher.OnChoiceFocused(label);
        }
    }

    [HarmonyPatch(typeof(RPGTalk), "MadeAChoice")]
    public class Patch_RPGTalk_MadeChoice
    {
        static void Prefix(string questionID, int choiceNumber, string text)
        {
            try
            {
                UIWatcher.ResetChoiceFocus();
                string clean = TextCleaner.Clean(text);
                if (string.IsNullOrWhiteSpace(clean)) return;
                Plugin.Log.LogInfo($"[CHOICE MADE] {choiceNumber}: {clean}");
            }
            catch (System.Exception e) { Plugin.Log.LogError($"MadeChoicePatch: {e}"); }
        }
    }
}
