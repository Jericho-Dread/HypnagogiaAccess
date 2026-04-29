using System;
using System.Runtime.InteropServices;

namespace HypnagogiaAccess
{
    public class ScreenReaderBridge
    {
        private bool _nvdaAvailable = false;
        private string _lastSpoken = "";

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode)]
        private static extern int nvdaController_speakText(string text);

        [DllImport("nvdaControllerClient64.dll")]
        private static extern int nvdaController_cancelSpeech();

        [DllImport("nvdaControllerClient64.dll")]
        private static extern int nvdaController_testIfRunning();

        public bool Initialize()
        {
            // Test NVDA
            try
            {
                int result = nvdaController_testIfRunning();
                _nvdaAvailable = (result == 0);
                Plugin.Log.LogInfo($"NVDA test result: {result}, available: {_nvdaAvailable}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"NVDA not available: {e.Message}");
                _nvdaAvailable = false;
            }

            return true; // Always return true - we'll try NVDA when speaking
        }

        public void Speak(string text, bool interrupt = true)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            text = CleanText(text);
            if (text == _lastSpoken && !interrupt) return;
            _lastSpoken = text;

            Plugin.Log.LogInfo($"[SPEECH] {text}");

            if (_nvdaAvailable)
            {
                try
                {
                    if (interrupt) nvdaController_cancelSpeech();
                    nvdaController_speakText(text);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"NVDA speak error: {e.Message}");
                }
            }
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
            return text;
        }
    }
}
