using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using HarmonyLib;
using System.Reflection;

namespace HypnagogiaAccess
{
    public class UIWatcher : MonoBehaviour
    {
        private class InteractionCandidate
        {
            public MonoBehaviour Source;
            public string Label = "";
        }

        public static UIWatcher Instance { get; private set; }

        // Dialogue suppression - automatically expires after a timeout
        // so menus always recover even if end-of-dialogue hooks don't fire
        private static float _dialogueSuppressUntil = 0f;
        private const float DIALOGUE_SUPPRESS_DURATION = 4f;
        private static float _protectDialogueSpeechUntil = 0f;
        private const float MIN_DIALOGUE_PROTECT_DURATION = 2f;
        private const float MAX_DIALOGUE_PROTECT_DURATION = 12f;
        private const float CHARS_PER_SECOND = 14f;

        private string _lastMenuButton = "";
        private float _menuScanTimer = 0f;
        private const float MENU_SCAN_INTERVAL = 0.05f;
        private string _lastInteractionAnnouncement = "";
        private float _interactionScanTimer = 0f;
        private const float INTERACTION_SCAN_INTERVAL = 0.45f;
        private const float INTERACTION_REPEAT_RESET = 0.75f;
        private const float INFO_NODE_DISTANCE = 4.5f;
        private const float PICKUP_DISTANCE = 3.5f;
        private int _interactionScanPhase = 0;
        private int _interactionMissCount = 0;
        private int _lastFallbackInteractionSourceId = 0;
        private static int _activeInteractionSourceId = 0;
        private static string _activeInteractionLabel = "";
        private static string _lastChoiceFocus = "";

        private static readonly string[] _ignoredNames = new[]
        {
            "SliderSensitivity", "Scrollbar", "VolumeSlider", "fovSlider",
            "lookSensitivitySlider", "sensitivitySlider", "ScrollbarVertical",
            "ScrollbarHorizontal", "Handle", "Fill", "Background", "Viewport",
            "fullscreenToggle"
        };

        private void Awake() { Instance = this; }

        private void Update()
        {
            _menuScanTimer += Time.unscaledDeltaTime;
            if (_menuScanTimer >= MENU_SCAN_INTERVAL)
            {
                _menuScanTimer = 0f;
                // Only suppress if within the timeout window
                if (Time.unscaledTime < _dialogueSuppressUntil && !IsDialogueChoiceUIActive()) return;
                ScanMenuButtons();
            }

            _interactionScanTimer += Time.unscaledDeltaTime;
            if (_interactionScanTimer >= INTERACTION_SCAN_INTERVAL)
            {
                _interactionScanTimer = 0f;
                ScanInteractions();
            }
        }

        // Called when dialogue starts - suppresses menu for a fixed duration
        public static void SetDialogueActive(bool active)
        {
            if (active)
                _dialogueSuppressUntil = Time.unscaledTime + DIALOGUE_SUPPRESS_DURATION;
            else
                _dialogueSuppressUntil = 0f;
        }

        public static void OnDialogueEnded()
        {
            _dialogueSuppressUntil = 0f;
        }

        private void ScanMenuButtons()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null) return;
            if (!selected.activeInHierarchy) return;

            var canvas = selected.GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.isActiveAndEnabled) return;

            var tmp = selected.GetComponentInChildren<TMP_Text>();
            var legacy = selected.GetComponentInChildren<Text>();
            string label = tmp?.text ?? legacy?.text ?? "";

            if (string.IsNullOrWhiteSpace(label))
                label = selected.name;

            foreach (var ignored in _ignoredNames)
                if (label.Equals(ignored, System.StringComparison.OrdinalIgnoreCase)) return;

            if (label.Length < 2) return;

            label = System.Text.RegularExpressions.Regex.Replace(label, @"<[^>]+>", "").Trim();
            if (string.IsNullOrWhiteSpace(label)) return;
            if (label == _lastMenuButton) return;

            _lastMenuButton = label;
            Plugin.Log.LogInfo($"[MENU] {label}");
            bool choiceActive = IsDialogueChoiceUIActive();
            bool shouldInterrupt = choiceActive || Time.unscaledTime >= _protectDialogueSpeechUntil;
            Plugin.SR.Speak(label, interrupt: shouldInterrupt);
        }

        public static void OnNewDialogueLine(string speaker, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            // Each new dialogue line resets the suppression timer
            SetDialogueActive(true);
            string full = string.IsNullOrWhiteSpace(speaker) ? text : $"{speaker}: {text}";
            ProtectDialogueSpeech(full);
            Plugin.SR.Speak(full, interrupt: true);
        }

        public static void OnDialogueChunk(string text, bool interrupt)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            SetDialogueActive(true);
            ProtectDialogueSpeech(text);
            Plugin.SR.Speak(text, interrupt: interrupt);
        }

        public static void OnChoicesAppeared(string announcement)
        {
            if (string.IsNullOrWhiteSpace(announcement)) return;
            // Choices mean player input needed - clear suppression immediately
            _dialogueSuppressUntil = 0f;
            _protectDialogueSpeechUntil = 0f;
            _lastChoiceFocus = "";
            if (Instance != null)
                Instance._lastMenuButton = "";
        }

        public static void OnChoiceFocused(string label)
        {
            label = TextCleaner.Clean(label);
            if (string.IsNullOrWhiteSpace(label)) return;
            if (label == _lastChoiceFocus) return;

            _dialogueSuppressUntil = 0f;
            _protectDialogueSpeechUntil = 0f;
            _lastChoiceFocus = label;

            if (Instance != null)
                Instance._lastMenuButton = label;

            Plugin.Log.LogInfo($"[CHOICE FOCUS] {label}");
            Plugin.SR.Speak(label, interrupt: true);
        }

        public static void ResetChoiceFocus()
        {
            _lastChoiceFocus = "";
            if (Instance != null)
                Instance._lastMenuButton = "";
        }

        public static void OnInteractionPromptShown(MonoBehaviour mono, string label)
        {
            if (mono == null) return;

            label = TextCleaner.Clean(label);
            if (string.IsNullOrWhiteSpace(label)) return;

            int id = mono.GetInstanceID();
            if (_activeInteractionSourceId == id && _activeInteractionLabel == label)
                return;

            _activeInteractionSourceId = id;
            _activeInteractionLabel = label;

            if (Instance != null)
            {
                Instance._lastInteractionAnnouncement = label;
                Instance._interactionMissCount = 0;
                Instance._lastFallbackInteractionSourceId = 0;
            }

            Plugin.Log.LogInfo($"[INTERACT EVENT] {label}");
            bool shouldInterrupt = Time.unscaledTime >= _protectDialogueSpeechUntil;
            Plugin.SR.Speak(label, interrupt: shouldInterrupt);
        }

        public static void OnInteractionPromptHidden(MonoBehaviour mono)
        {
            if (mono == null) return;
            if (_activeInteractionSourceId != mono.GetInstanceID()) return;

            _activeInteractionSourceId = 0;
            _activeInteractionLabel = "";
            if (Instance != null)
            {
                Instance._lastInteractionAnnouncement = "";
                Instance._interactionMissCount = 0;
                Instance._lastFallbackInteractionSourceId = 0;
            }
        }

        private static void ProtectDialogueSpeech(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            float estimatedSeconds = Mathf.Clamp(
                text.Length / CHARS_PER_SECOND,
                MIN_DIALOGUE_PROTECT_DURATION,
                MAX_DIALOGUE_PROTECT_DURATION);

            _protectDialogueSpeechUntil = Time.unscaledTime + estimatedSeconds;
        }

        private bool IsDialogueChoiceUIActive()
        {
            var rpgTalks = Object.FindObjectsOfType<RPGTalk>();
            foreach (var talk in rpgTalks)
            {
                if (talk == null || !talk.isActiveAndEnabled) continue;

                var choicesParent = AccessTools.Field(typeof(RPGTalk), "choicesParent")?.GetValue(talk) as Transform;
                if (choicesParent == null || !choicesParent.gameObject.activeInHierarchy) continue;

                var buttons = choicesParent.GetComponentsInChildren<Button>(true);
                foreach (var button in buttons)
                {
                    if (button != null && button.gameObject.activeInHierarchy)
                        return true;
                }
            }

            return false;
        }

        private void ScanInteractions()
        {
            try
            {
                if (_activeInteractionSourceId != 0) return;

                var candidate = FindBestInteractionCandidate();
                if (candidate == null)
                {
                    _interactionMissCount++;
                    if (_interactionMissCount >= 4)
                    {
                        _lastInteractionAnnouncement = "";
                        _lastFallbackInteractionSourceId = 0;
                    }
                    return;
                }

                _interactionMissCount = 0;
                int sourceId = candidate.Source != null ? candidate.Source.GetInstanceID() : 0;
                if (sourceId == _lastFallbackInteractionSourceId &&
                    candidate.Label == _lastInteractionAnnouncement)
                    return;

                _lastFallbackInteractionSourceId = sourceId;
                _lastInteractionAnnouncement = candidate.Label;
                bool shouldInterrupt = Time.unscaledTime >= _protectDialogueSpeechUntil + INTERACTION_REPEAT_RESET;
                Plugin.Log.LogInfo($"[INTERACT] {candidate.Label}");
                Plugin.SR.Speak(candidate.Label, interrupt: shouldInterrupt);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Interaction scan failed: {e.Message}");
            }
        }

        private InteractionCandidate FindBestInteractionCandidate()
        {
            Vector3 origin = GetInteractionOrigin();
            float bestDistance = float.MaxValue;
            InteractionCandidate best = null;

            switch (_interactionScanPhase)
            {
                case 0:
                    EvaluateType(origin, AccessTools.TypeByName("DemoInfo"), IsInfoNodeAvailable, DescribeInfoNode,
                        ref bestDistance, ref best);
                    break;

                case 1:
                    EvaluateType(origin, AccessTools.TypeByName("objectPickup"), IsObjectPickupAvailable, DescribeObjectPickup,
                        ref bestDistance, ref best);
                    EvaluateType(origin, AccessTools.TypeByName("Pickup"), IsBasicPickupAvailable, DescribeBasicPickup,
                        ref bestDistance, ref best);
                    EvaluateType(origin, AccessTools.TypeByName("HealthPickup"), IsWrappedPickupAvailable, DescribeHealthPickup,
                        ref bestDistance, ref best);
                    EvaluateType(origin, AccessTools.TypeByName("JetpackPickup"), IsWrappedPickupAvailable, DescribeJetpackPickup,
                        ref bestDistance, ref best);
                    EvaluateType(origin, AccessTools.TypeByName("WeaponPickup"), IsWrappedPickupAvailable, DescribeWeaponPickup,
                        ref bestDistance, ref best);
                    EvaluateType(origin, AccessTools.TypeByName("ObjectivePickupItem"), IsWrappedPickupAvailable, DescribeObjectivePickup,
                        ref bestDistance, ref best);
                    break;

                default:
                    EvaluateType(origin, AccessTools.TypeByName("PhysicsPickup"), IsPhysicsPickupAvailable, DescribePhysicsPickup,
                        ref bestDistance, ref best);
                    break;
            }

            _interactionScanPhase = (_interactionScanPhase + 1) % 3;

            return best;
        }

        private void EvaluateType(
            Vector3 origin,
            System.Type type,
            System.Func<MonoBehaviour, bool> isAvailable,
            System.Func<MonoBehaviour, string> describe,
            ref float bestDistance,
            ref InteractionCandidate best)
        {
            if (type == null) return;

            var objects = Resources.FindObjectsOfTypeAll(type);
            foreach (var obj in objects)
            {
                if (!(obj is MonoBehaviour mono)) continue;
                if (!mono.gameObject.scene.IsValid()) continue;
                if (!mono.gameObject.activeInHierarchy) continue;
                if (!mono.enabled) continue;
                if (!isAvailable(mono)) continue;

                float distance = Vector3.Distance(origin, mono.transform.position);
                string label = describe(mono);
                if (string.IsNullOrWhiteSpace(label)) continue;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = new InteractionCandidate
                    {
                        Source = mono,
                        Label = label
                    };
                }
            }
        }

        private Vector3 GetInteractionOrigin()
        {
            var playerType = AccessTools.TypeByName("PlayerCharacterController");
            if (playerType != null)
            {
                var player = Object.FindObjectOfType(playerType) as MonoBehaviour;
                if (player != null) return player.transform.position;
            }

            if (Camera.main != null) return Camera.main.transform.position;
            return transform.position;
        }

        private bool IsObjectPickupAvailable(MonoBehaviour mono)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            return (bool?)mono.GetType().GetField("canGrab", flags)?.GetValue(mono) ?? false;
        }

        private string DescribeObjectPickup(MonoBehaviour mono)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var wp = mono.GetType().GetField("wp", flags)?.GetValue(mono) as GameObject;
            var current = mono.GetType().GetField("currentWeapon", flags)?.GetValue(mono) as GameObject;
            string name = wp != null ? wp.name : current != null ? current.name : mono.gameObject.name;
            return $"Pickup: {CleanName(name)}";
        }

        private bool IsBasicPickupAvailable(MonoBehaviour mono)
        {
            Vector3 origin = GetInteractionOrigin();
            return Vector3.Distance(origin, mono.transform.position) <= PICKUP_DISTANCE;
        }

        private string DescribeBasicPickup(MonoBehaviour mono)
        {
            return $"Pickup: {CleanName(mono.gameObject.name)}";
        }

        private bool IsWrappedPickupAvailable(MonoBehaviour mono)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var pickup = mono.GetType().GetField("m_Pickup", flags)?.GetValue(mono) as MonoBehaviour;
            return pickup != null && IsBasicPickupAvailable(pickup);
        }

        private string DescribeHealthPickup(MonoBehaviour mono)
        {
            return "Pickup: health";
        }

        private string DescribeJetpackPickup(MonoBehaviour mono)
        {
            return "Pickup: jetpack";
        }

        private string DescribeWeaponPickup(MonoBehaviour mono)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var weaponPrefab = mono.GetType().GetField("weaponPrefab", flags)?.GetValue(mono) as MonoBehaviour;
            if (weaponPrefab != null)
                return $"Pickup: {CleanName(weaponPrefab.gameObject.name)}";
            return "Pickup: weapon";
        }

        private string DescribeObjectivePickup(MonoBehaviour mono)
        {
            return $"Pickup: {CleanName(mono.gameObject.name)}";
        }

        private bool IsPhysicsPickupAvailable(MonoBehaviour mono)
        {
            Vector3 origin = GetInteractionOrigin();
            if (Vector3.Distance(origin, mono.transform.position) > PICKUP_DISTANCE) return false;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var icon1 = mono.GetType().GetField("PickupIcon", flags)?.GetValue(mono) as GameObject;
            var icon2 = mono.GetType().GetField("PickupIcon2", flags)?.GetValue(mono) as GameObject;

            return (icon1 != null && icon1.activeInHierarchy) || (icon2 != null && icon2.activeInHierarchy);
        }

        private string DescribePhysicsPickup(MonoBehaviour mono)
        {
            return $"Interact: {CleanName(mono.gameObject.name)}";
        }

        private bool IsInfoNodeAvailable(MonoBehaviour mono)
        {
            Vector3 origin = GetInteractionOrigin();
            if (Vector3.Distance(origin, mono.transform.position) > INFO_NODE_DISTANCE) return false;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            string info = mono.GetType().GetField("info", flags)?.GetValue(mono) as string ?? "";
            return !string.IsNullOrWhiteSpace(info);
        }

        private string DescribeInfoNode(MonoBehaviour mono)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            string info = mono.GetType().GetField("info", flags)?.GetValue(mono) as string ?? "";
            info = TextCleaner.Clean(info);
            if (string.IsNullOrWhiteSpace(info))
                return $"Interact: {CleanName(mono.gameObject.name)}";
            return $"Info node: {info}";
        }

        private static string CleanName(string raw)
        {
            string clean = TextCleaner.Clean(raw ?? "");
            clean = System.Text.RegularExpressions.Regex.Replace(clean, @"[_\-]+", " ").Trim();
            return string.IsNullOrWhiteSpace(clean) ? "object" : clean;
        }
    }

    [HarmonyPatch(typeof(RPGTalkArea), "ShowInteractionInstruction")]
    public class Patch_RPGTalkArea_ShowInteractionInstruction
    {
        static void Postfix(RPGTalkArea __instance)
        {
            UIWatcher.OnInteractionPromptShown(__instance, "Interact");
        }
    }

    [HarmonyPatch(typeof(RPGTalkArea), "HideInteractionInstruction")]
    public class Patch_RPGTalkArea_HideInteractionInstruction
    {
        static void Postfix(RPGTalkArea __instance)
        {
            UIWatcher.OnInteractionPromptHidden(__instance);
        }
    }

    [HarmonyPatch(typeof(KeypadButton), "OnMouseOver")]
    public class Patch_KeypadButton_OnMouseOver
    {
        static void Postfix(KeypadButton __instance)
        {
            int number = (int)AccessTools.Field(typeof(KeypadButton), "keypadNumber").GetValue(__instance);
            string label = number >= 0 ? $"Interact: keypad button {number}" : "Interact";
            UIWatcher.OnInteractionPromptShown(__instance, label);
        }
    }

    [HarmonyPatch(typeof(KeypadButton), "OnMouseExit")]
    public class Patch_KeypadButton_OnMouseExit
    {
        static void Postfix(KeypadButton __instance)
        {
            UIWatcher.OnInteractionPromptHidden(__instance);
        }
    }

    [HarmonyPatch]
    public class Patch_PadlockRotate_OnMouseOver
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method("PadlockRotate:OnMouseOver");
        }

        static void Postfix(MonoBehaviour __instance)
        {
            int number = (int?)AccessTools.Field(__instance.GetType(), "numberShown")?.GetValue(__instance) ?? -1;
            string label = number >= 0 ? $"Interact: padlock dial {number}" : "Interact";
            UIWatcher.OnInteractionPromptShown(__instance, label);
        }
    }

    [HarmonyPatch]
    public class Patch_PadlockRotate_OnMouseExit
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method("PadlockRotate:OnMouseExit");
        }

        static void Postfix(MonoBehaviour __instance)
        {
            UIWatcher.OnInteractionPromptHidden(__instance);
        }
    }
}
