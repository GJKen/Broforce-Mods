using UnityEngine;

namespace TestMod
{
    internal sealed class FrameRateDisplay : MonoBehaviour
    {
        private float _sampleStartedAt;
        private int _sampleFrames;
        private string _text = "FPS: --";
        private GUIStyle _style;

        private void OnEnable()
        {
            _sampleStartedAt = Time.realtimeSinceStartup;
            _sampleFrames = 0;
        }

        private void Update()
        {
            var key = Plugin.Settings.FrameRateToggleKey;
            if (!Plugin.CapturingHotkey && Application.isFocused &&
                key != KeyCode.None && Input.GetKeyDown(key))
                Plugin.SetFrameRateVisible(!Plugin.Settings.ShowFrameRate);

            var now = Time.realtimeSinceStartup;
            if (!Plugin.Settings.ShowFrameRate)
            {
                _sampleStartedAt = now;
                _sampleFrames = 0;
                return;
            }

            _sampleFrames++;
            var elapsed = now - _sampleStartedAt;
            if (elapsed < 0.5f) return;
            _text = "FPS: " + Mathf.RoundToInt(_sampleFrames / elapsed);
            _sampleStartedAt = now;
            _sampleFrames = 0;
        }

        private void OnGUI()
        {
            if (!Plugin.Settings.ShowFrameRate || Event.current.type != EventType.Repaint) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label);
                _style.alignment = TextAnchor.UpperCenter;
                _style.fontSize = 22;
                _style.fontStyle = FontStyle.Bold;
                _style.normal.textColor = Color.white;
            }

            var color = GUI.color;
            var depth = GUI.depth;
            GUI.depth = -1000;
            var rect = new Rect((Screen.width - 180f) * 0.5f, 8f, 180f, 32f);
            GUI.color = Color.black;
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), _text, _style);
            GUI.color = Color.white;
            GUI.Label(rect, _text, _style);
            GUI.color = color;
            GUI.depth = depth;
        }
    }
}
