using UnityEngine;

namespace FrameRateLimiter
{
    internal sealed class TooltipOverlay : MonoBehaviour
    {
        private const float MinimumWidth = 240f;
        private const float MaximumWidth = 360f;
        private const float TooltipGap = 4f;

        private static TooltipOverlay _instance;

        private GUIStyle _style;
        private Texture2D _background;

        internal static void Create()
        {
            if (_instance != null)
            {
                return;
            }

            var gameObject = new GameObject("FrameRateLimiterTooltipOverlay");
            DontDestroyOnLoad(gameObject);
            _instance = gameObject.AddComponent<TooltipOverlay>();
        }

        internal static void Stop()
        {
            if (_instance == null)
            {
                return;
            }

            if (_instance._background != null)
            {
                Object.Destroy(_instance._background);
            }

            Object.Destroy(_instance.gameObject);
            _instance = null;
        }

        internal static void DrawInGui(string tooltip, Rect anchor)
        {
            if (_instance == null || string.IsNullOrEmpty(tooltip) ||
                Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            var content = new GUIContent(tooltip);
            var style = _instance.GetStyle();
            var width = Mathf.Min(
                MaximumWidth,
                Mathf.Max(MinimumWidth, style.CalcSize(content).x));
            var height = style.CalcHeight(content, width);
            var rect = new Rect(
                anchor.x,
                anchor.y - height - TooltipGap,
                width,
                height);

            if (rect.y < 6f)
            {
                rect.y = anchor.yMax + TooltipGap;
            }

            if (rect.xMax > Screen.width - 6f)
            {
                rect.x = Screen.width - width - 6f;
            }

            var previousColor = GUI.color;
            GUI.color = Color.white;
            GUI.Box(rect, content, style);
            GUI.color = previousColor;
        }

        private GUIStyle GetStyle()
        {
            if (_style != null)
            {
                return _style;
            }

            _background = new Texture2D(1, 1);
            _background.SetPixel(0, 0, new Color(0.06f, 0.06f, 0.06f, 1f));
            _background.Apply();
            _background.hideFlags = HideFlags.HideAndDontSave;

            _style = new GUIStyle(GUI.skin.box);
            _style.normal.background = _background;
            _style.hover.background = _background;
            _style.active.background = _background;
            _style.focused.background = _background;
            _style.fontSize = 12;
            _style.wordWrap = true;
            _style.alignment = TextAnchor.UpperLeft;
            _style.padding = new RectOffset(8, 8, 6, 6);
            _style.normal.textColor = Color.white;
            _style.hover.textColor = Color.white;
            _style.active.textColor = Color.white;
            _style.focused.textColor = Color.white;
            return _style;
        }
    }
}
