using System.Collections.Generic;
using UnityEngine;
using UnityModManagerNet;

namespace TestMod
{
    public static partial class Plugin
    {
        private static SettingsUiStyles _uiStyles;
        private static readonly float[] SpeedPresets = { 0.1f, 0.25f, 0.5f, 1f, 2f, 4f };

        private static void DrawSettingsUI(UnityModManager.ModEntry modEntry)
        {
            if (_uiStyles == null) _uiStyles = new SettingsUiStyles();
            var chinese = IsChinese();
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical(_uiStyles.NavigationPanel, GUILayout.Width(180f), GUILayout.MinHeight(500f));
            GUILayout.Label("Test Mod", _uiStyles.Title);
            GUILayout.Label(chinese ? "游戏测试工具" : "Game testing tools", _uiStyles.Help);
            GUILayout.Space(14f);
            DrawNavigation(modEntry, "speed", chinese ? "速度与动作帧" : "Speed & Frames");
            DrawNavigation(modEntry, "enemies", chinese ? "敌人清理" : "Enemy Cleanup");
            DrawNavigation(modEntry, "language", chinese ? "语言设置" : "Language");
            GUILayout.FlexibleSpace();
            DrawPlaybackStatus(chinese);
            GUILayout.EndVertical();
            GUILayout.Space(8f);
            GUILayout.BeginVertical(_uiStyles.ContentPanel, GUILayout.ExpandWidth(true), GUILayout.MinHeight(500f));
            switch (Settings.SettingsPanel)
            {
                case "enemies": DrawEnemySettings(chinese); break;
                case "language": DrawLanguageSettings(modEntry, chinese); break;
                default: DrawGameSpeed(modEntry, chinese); break;
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private static void DrawNavigation(UnityModManager.ModEntry modEntry, string panel, string label)
        {
            if (GUILayout.Button(label, Settings.SettingsPanel == panel ? _uiStyles.SelectedNavigation :
                _uiStyles.Navigation, GUILayout.ExpandWidth(true)))
            {
                Settings.SettingsPanel = panel;
                _bindingTarget = 0;
                SaveSettings(modEntry);
            }
            GUILayout.Space(3f);
        }

        private static void DrawPlaybackStatus(bool chinese)
        {
            GUILayout.BeginVertical(_uiStyles.SectionPanel);
            GUILayout.Label(chinese ? "当前状态" : "Current state", _uiStyles.SectionTitle);
            var status = _speedController == null ? (chinese ? "Mod 未启用" : "Mod inactive") :
                IsGamePaused ? (chinese ? "已暂停" : "Paused") :
                Settings.EnableGameSpeed ? (chinese ? "正在播放" : "Playing") :
                (chinese ? "原生游戏速度" : "Native game speed");
            GUILayout.Label(status, _uiStyles.Label);
            if (IsGameSpeedEnabled)
                GUILayout.Label((chinese ? "播放倍率：" : "Playback: ") + Settings.GameSpeed.ToString("0.00") + "x",
                    _uiStyles.Help);
            if (IsGamePaused)
            {
                GUILayout.Label((chinese ? "已推进帧数：" : "Stepped frames: ") + SteppedFrames, _uiStyles.Help);
                if (GUILayout.Button(chinese ? "恢复播放" : "Resume", _uiStyles.SelectedButton)) SetGamePaused(false);
            }
            GUILayout.EndVertical();
        }

        private static void DrawPageHeading(string title, string intro)
        {
            GUILayout.Label(title, _uiStyles.Title);
            GUILayout.Label(intro, _uiStyles.Help);
            GUILayout.Space(10f);
        }

        private static void BeginSettingsSection(string title)
        {
            GUILayout.BeginVertical(_uiStyles.SectionPanel, GUILayout.ExpandWidth(true));
            GUILayout.Label(title, _uiStyles.SectionTitle);
            GUILayout.Space(5f);
        }

        private static void DrawGameSpeed(UnityModManager.ModEntry modEntry, bool chinese)
        {
            DrawPageHeading(chinese ? "速度与动作帧" : "Speed & Frames",
                chinese ? "调整播放速度，暂停画面，逐帧查看角色动作。" :
                "Adjust playback speed, freeze the picture and inspect character motion frame by frame.");

            BeginSettingsSection(chinese ? "播放速度" : "Playback speed");
            var enabled = GUILayout.Toggle(Settings.EnableGameSpeed,
                chinese ? "启用游戏速度控制" : "Enable game speed control", _uiStyles.Toggle);
            if (enabled != Settings.EnableGameSpeed) SetGameSpeedEnabled(enabled);
            GUILayout.BeginHorizontal();
            GUILayout.Label(chinese ? "播放倍率" : "Speed multiplier", _uiStyles.Label);
            GUILayout.FlexibleSpace();
            GUILayout.Label(Settings.GameSpeed.ToString("0.00") + "x", _uiStyles.SectionTitle);
            GUILayout.EndHorizontal();
            var speed = GUILayout.HorizontalSlider(Settings.GameSpeed, 0.01f, 4f);
            if (Mathf.Abs(speed - Settings.GameSpeed) > 0.001f)
            {
                Settings.GameSpeed = speed;
                if (_speedController != null) _speedController.ApplySettings();
            }
            GUILayout.Space(5f);
            GUILayout.BeginHorizontal();
            foreach (var preset in SpeedPresets)
            {
                var selected = Settings.EnableGameSpeed && Mathf.Abs(Settings.GameSpeed - preset) < 0.001f;
                if (GUILayout.Button(preset.ToString("0.##") + "x", selected ? _uiStyles.SelectedButton :
                    _uiStyles.Button, GUILayout.ExpandWidth(true))) SetGameSpeed(preset);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(chinese ? "范围 0.01x～4x。选择常用倍率会直接恢复播放。" :
                "Range: 0.01x–4x. Choosing a preset starts playback at that speed.", _uiStyles.Help);
            GUILayout.EndVertical();
            GUILayout.Space(10f);

            BeginSettingsSection(chinese ? "暂停与逐帧查看" : "Pause & frame stepping");
            var guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && _speedController != null;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(IsGamePaused ? (chinese ? "恢复播放" : "Resume") :
                (chinese ? "暂停当前画面" : "Pause"), _uiStyles.SelectedButton, GUILayout.ExpandWidth(true)))
                SetGamePaused(!IsGamePaused);
            GUI.enabled = guiEnabled && _speedController != null && PauseController.pauseStatus == PauseStatus.UnPaused;
            if (GUILayout.Button(chinese ? "下一帧" : "Next frame", _uiStyles.Button, GUILayout.ExpandWidth(true))) StepOneFrame();
            GUI.enabled = guiEnabled && _speedController != null;
            if (GUILayout.Button(chinese ? "恢复正常速度" : "Normal speed", _uiStyles.Button, GUILayout.ExpandWidth(true)))
                SetGameSpeed(1f);
            GUILayout.EndHorizontal();
            GUI.enabled = guiEnabled;
            GUILayout.Label(chinese ? "每次推进 1/60 秒，之后保持暂停。动画贴图可能需要推进多次才切换。" :
                "Advance 1/60 second, then stay paused. A sprite animation frame may span several steps.", _uiStyles.Help);
            if (PauseController.pauseStatus != PauseStatus.UnPaused)
                GUILayout.Label(chinese ? "请先关闭游戏原生暂停菜单，再执行下一帧。" :
                    "Close the game's pause menu before stepping.", _uiStyles.Help);
            if (_speedController == null)
                GUILayout.Label(chinese ? "启用 Test Mod 后即可操作暂停与下一帧。" :
                    "Enable Test Mod to use pause and frame stepping.", _uiStyles.Help);
            GUILayout.EndVertical();
            GUILayout.Space(10f);

            BeginSettingsSection(chinese ? "快捷键" : "Hotkeys");
            DrawHotkey(modEntry, chinese, 1, chinese ? "暂停 / 恢复" : "Pause / resume", Settings.PauseKey);
            DrawHotkey(modEntry, chinese, 2, chinese ? "下一帧" : "Next frame", Settings.FrameStepKey);
            GUILayout.Label(chinese ? "点击按钮后按目标键绑定，Esc 取消。单按推进一帧。" :
                "Click a binding, then press a key. Esc cancels. A tap advances one frame.", _uiStyles.Help);
            GUILayout.Space(6f);
            var holdToStep = GUILayout.Toggle(Settings.HoldToStep,
                chinese ? "长按下一帧快捷键时连续推进" : "Hold the next-frame key to repeat", _uiStyles.Toggle);
            if (holdToStep != Settings.HoldToStep)
            {
                Settings.HoldToStep = holdToStep;
                SaveSettings(modEntry);
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(chinese ? "连续推进速度" : "Repeat rate", _uiStyles.Label);
            GUILayout.FlexibleSpace();
            GUILayout.Label(Settings.FrameStepRepeatRate + (chinese ? " 帧 / 秒" : " frames / second"),
                _uiStyles.SectionTitle);
            GUILayout.EndHorizontal();
            var repeatRate = Mathf.RoundToInt(GUILayout.HorizontalSlider(Settings.FrameStepRepeatRate, 1f, 60f));
            if (repeatRate != Settings.FrameStepRepeatRate)
                Settings.FrameStepRepeatRate = repeatRate;
            GUILayout.Label(chinese ? "长按 0.35 秒后开始连发，松开后保持暂停。速度按真实时间计时，范围 1～60 帧/秒。" :
                "Repeat starts after holding for 0.35 seconds. Release to stay paused. Rate: 1–60 frames per real second.",
                _uiStyles.Help);
            GUILayout.Label(chinese ? "连续推进速度不超过实际渲染帧率；每次仍推进 1/60 秒游戏时间。" :
                "Repeat is limited by the render frame rate. Each step still advances 1/60 second of game time.",
                _uiStyles.Help);
            GUILayout.EndVertical();
            GUILayout.Space(8f);
            GUILayout.Label(chinese ? "速度控制仅作用于当前游戏端。关闭控制会恢复接管前的时间设置。" :
                "Speed control applies to this instance. Turning it off restores the previous timing settings.", _uiStyles.Help);
        }

        private static void DrawEnemySettings(bool chinese)
        {
            DrawPageHeading(chinese ? "敌人清理" : "Enemy Cleanup",
                chinese ? "快速清理当前屏幕的敌人，方便准备角色动作测试环境。" :
                "Clear visible enemies to prepare a character animation test.");
            BeginSettingsSection(chinese ? "自动清理" : "Automatic cleanup");
            var enabled = GUILayout.Toggle(Settings.EnableAutoKill,
                chinese ? "启用自动击杀" : "Enable automatic killing", _uiStyles.Toggle);
            if (enabled != Settings.EnableAutoKill) SetAutoKillEnabled(enabled);
            GUILayout.Label(chinese ? "进入关卡后，发现屏幕内符合条件的敌人时自动清理一次。" :
                "After entering a level, automatically clear eligible visible enemies once.", _uiStyles.Help);
            GUILayout.EndVertical();
            GUILayout.Space(10f);

            BeginSettingsSection(chinese ? "手动清理" : "Manual cleanup");
            var guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && Settings.EnableAutoKill && _behaviour != null;
            if (GUILayout.Button(chinese ? "立即清除当前屏幕敌人" : "Kill visible enemies now", _uiStyles.Button))
                KillVisibleEnemiesNow();
            GUI.enabled = guiEnabled;
            GUILayout.Label((chinese ? "上次清除数量：" : "Last kill count: ") + LastKillCount, _uiStyles.Label);
            if (!Settings.EnableAutoKill)
                GUILayout.Label(chinese ? "自动击杀开关关闭时，手动清理也会禁用。" :
                    "Turning automatic killing off also disables manual cleanup.", _uiStyles.Help);
            GUILayout.EndVertical();
            GUILayout.Space(10f);

            BeginSettingsSection(chinese ? "清理范围" : "Cleanup scope");
            GUILayout.Label(chinese ? "仅处理当前屏幕可见、属于本机、仍然存活的敌方 Mook。英雄不会被清理。" :
                "Only living enemy Mooks that are visible and owned by this instance are cleared. Heroes are excluded.",
                _uiStyles.Help);
            GUILayout.EndVertical();
        }

        private static void DrawLanguageSettings(UnityModManager.ModEntry modEntry, bool chinese)
        {
            DrawPageHeading(chinese ? "语言设置" : "Language",
                chinese ? "选择 Test Mod 设置面板的显示语言。" : "Choose the language for the Test Mod settings panel.");
            BeginSettingsSection(chinese ? "界面语言 / Language" : "Interface language / 语言");
            var languageIndex = GetLanguageIndex(Settings.Language);
            var choices = new[] { chinese ? "跟随系统" : "Follow System", "English", "中文" };
            GUILayout.BeginHorizontal();
            for (var index = 0; index < choices.Length; index++)
            {
                if (GUILayout.Button(choices[index], index == languageIndex ? _uiStyles.SelectedButton :
                    _uiStyles.Button, GUILayout.ExpandWidth(true)))
                {
                    Settings.Language = index == 1 ? "en" : index == 2 ? "zh" : "system";
                    SaveSettings(modEntry);
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(chinese ? "选择后立即生效，并在下次启动时保留。跟随系统会根据系统语言选择中文或英文。" :
                "Changes apply immediately and are saved. Follow System chooses Chinese or English from the system language.",
                _uiStyles.Help);
            GUILayout.EndVertical();
        }

        private static void DrawHotkey(UnityModManager.ModEntry modEntry, bool chinese,
            int target, string label, KeyCode key)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _uiStyles.Label, GUILayout.Width(135f));
            var listening = _bindingTarget == target;
            var caption = listening ? (chinese ? "请按键（Esc 取消）" : "Press a key (Esc cancels)") :
                key == KeyCode.None ? (chinese ? "未绑定：点击绑定" : "Unbound: click to bind") : key.ToString();
            if (GUILayout.Button(caption, listening ? _uiStyles.SelectedButton : _uiStyles.Button,
                GUILayout.ExpandWidth(true))) _bindingTarget = listening ? 0 : target;
            if (GUILayout.Button(chinese ? "清除" : "Clear", _uiStyles.Button, GUILayout.Width(60f)))
            {
                if (target == 1) Settings.PauseKey = KeyCode.None;
                else Settings.FrameStepKey = KeyCode.None;
                _bindingTarget = 0;
                SaveSettings(modEntry);
            }
            GUILayout.EndHorizontal();

            var currentEvent = Event.current;
            if (_bindingTarget != target || currentEvent.type != EventType.KeyDown ||
                currentEvent.keyCode == KeyCode.None) return;
            if (currentEvent.keyCode != KeyCode.Escape)
            {
                if (target == 1)
                {
                    Settings.PauseKey = currentEvent.keyCode;
                    if (Settings.FrameStepKey == Settings.PauseKey) Settings.FrameStepKey = KeyCode.None;
                }
                else
                {
                    Settings.FrameStepKey = currentEvent.keyCode;
                    if (Settings.PauseKey == Settings.FrameStepKey) Settings.PauseKey = KeyCode.None;
                }
                SaveSettings(modEntry);
            }
            _bindingTarget = 0;
            _ignoreHotkeysThroughFrame = Time.frameCount + 1;
            currentEvent.Use();
        }

        private static void ReleaseSettingsUI()
        {
            if (_uiStyles == null) return;
            _uiStyles.Release();
            _uiStyles = null;
        }

        // Match CustomMapMultiplayer's dark panels, orange actions and warm text.
        private sealed class SettingsUiStyles
        {
            private readonly List<Texture2D> _textures = new List<Texture2D>();
            internal readonly GUIStyle NavigationPanel, ContentPanel, SectionPanel;
            internal readonly GUIStyle Title, SectionTitle, Label, Help, Toggle;
            internal readonly GUIStyle Button, SelectedButton, Navigation, SelectedNavigation;

            internal SettingsUiStyles()
            {
                var primary = new Color(1f, 0.97f, 0.92f, 1f);
                var secondary = new Color(0.96f, 0.78f, 0.57f, 1f);
                var accent = new Color(1f, 0.68f, 0.24f, 1f);
                NavigationPanel = Panel(new Color(0.05f, 0.07f, 0.12f, 1f), 10);
                ContentPanel = Panel(new Color(0.08f, 0.11f, 0.17f, 1f), 14);
                SectionPanel = Panel(new Color(0.12f, 0.16f, 0.23f, 1f), 12);
                Title = Text(18, primary, true);
                SectionTitle = Text(14, accent, true);
                Label = Text(14, primary, false);
                Help = Text(13, secondary, false);
                Toggle = new GUIStyle(GUI.skin.toggle);
                Toggle.fontSize = 14;
                Toggle.fixedHeight = 30f;
                Toggle.normal.textColor = primary;
                Toggle.onNormal.textColor = new Color(0.72f, 1f, 1f, 1f);
                Button = ActionButton(new Color(0.32f, 0.16f, 0.07f, 1f),
                    new Color(0.50f, 0.25f, 0.08f, 1f), new Color(0.70f, 0.36f, 0.09f, 1f), primary);
                SelectedButton = ActionButton(new Color(0.72f, 0.27f, 0.06f, 1f),
                    new Color(0.90f, 0.42f, 0.08f, 1f), new Color(1f, 0.58f, 0.14f, 1f), Color.white);
                Navigation = new GUIStyle(Button);
                Navigation.fixedHeight = 42f;
                Navigation.wordWrap = true;
                SelectedNavigation = new GUIStyle(SelectedButton);
                SelectedNavigation.fixedHeight = 42f;
                SelectedNavigation.wordWrap = true;
                SelectedNavigation.fontStyle = FontStyle.Bold;
                SelectedNavigation.normal.background = Background(
                    new Color(0.62f, 0.25f, 0.06f, 1f), new Color(0.82f, 0.40f, 0.09f, 1f), 18);
                SelectedNavigation.hover.background = Background(
                    new Color(0.78f, 0.34f, 0.07f, 1f), new Color(0.96f, 0.54f, 0.13f, 1f), 18);
                SelectedNavigation.active.background = Background(
                    new Color(0.88f, 0.44f, 0.10f, 1f), new Color(1f, 0.66f, 0.20f, 1f), 18);
                SelectedNavigation.focused.background = SelectedNavigation.hover.background;
            }

            private GUIStyle Panel(Color color, int padding)
            {
                var style = new GUIStyle(GUI.skin.box);
                style.normal.background = Background(color);
                style.border = new RectOffset(7, 7, 7, 7);
                style.padding = new RectOffset(padding, padding, 10, 12);
                style.margin = new RectOffset(0, 0, 0, 0);
                return style;
            }

            private static GUIStyle Text(int size, Color color, bool bold)
            {
                var style = new GUIStyle(GUI.skin.label);
                style.fontSize = size;
                style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
                style.wordWrap = true;
                style.normal.textColor = color;
                return style;
            }

            private GUIStyle ActionButton(Color normal, Color hover, Color active, Color text)
            {
                var style = new GUIStyle(GUI.skin.button);
                style.fontSize = 14;
                style.fixedHeight = 32f;
                style.alignment = TextAnchor.MiddleCenter;
                style.padding = new RectOffset(6, 6, 0, 0);
                style.border = new RectOffset(7, 7, 7, 7);
                style.normal.background = Background(normal);
                style.hover.background = Background(hover);
                style.active.background = Background(active);
                style.focused.background = style.hover.background;
                style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = text;
                return style;
            }

            private Texture2D Background(Color color)
            {
                return Background(color, color, 24);
            }

            // Use the same radius and nine-slice borders as CustomMapMultiplayer.
            // The transparent corners remain round when a panel or button stretches.
            private Texture2D Background(Color color, Color highlight, int textureSize)
            {
                const float cornerRadius = 7f;
                var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
                texture.hideFlags = HideFlags.HideAndDontSave;
                for (var y = 0; y < textureSize; y++)
                {
                    for (var x = 0; x < textureSize; x++)
                    {
                        var centerX = x + 0.5f;
                        var centerY = y + 0.5f;
                        var distanceX = centerX < cornerRadius ? cornerRadius - centerX :
                            centerX > textureSize - cornerRadius ? centerX - (textureSize - cornerRadius) : 0f;
                        var distanceY = centerY < cornerRadius ? cornerRadius - centerY :
                            centerY > textureSize - cornerRadius ? centerY - (textureSize - cornerRadius) : 0f;
                        var outsideCorner = distanceX > 0f && distanceY > 0f &&
                            distanceX * distanceX + distanceY * distanceY > cornerRadius * cornerRadius;
                        var highlightAmount = y >= textureSize - 4 ? (y - (textureSize - 4) + 1) / 4f : 0f;
                        texture.SetPixel(x, y, outsideCorner ? Color.clear : Color.Lerp(color, highlight, highlightAmount));
                    }
                }
                texture.Apply();
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                _textures.Add(texture);
                return texture;
            }

            internal void Release()
            {
                foreach (var texture in _textures) Object.Destroy(texture);
                _textures.Clear();
            }
        }
    }
}
