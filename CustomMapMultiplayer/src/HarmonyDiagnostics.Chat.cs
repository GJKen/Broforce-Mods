using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private const float ChatInputRepeatDelaySeconds = 0.4f;
        private const float ChatInputRepeatIntervalSeconds = 0.06f;
        private const int ChatMessageMaxLength = 500;
        private const float ChatHistoryMessageSpacing = 10f;
        private const float ChatHistoryFallbackVisibleHeight = 500f;
        private const int ChatCharacterCountFontSize = 18;
        private const float ChatCharacterCountWidth = 80f;
        private const float ChatCharacterCountHeight = 28f;
        private const float ChatCharacterCountRightOffset = 6f;
        private const float ChatCharacterCountBottomOffset = -6f;
        private static FieldInfo _keyboardSkipNextFrameField;
        private static bool _chatInputRepeatActive;
        private static KeyCode _chatInputRepeatKey;
        private static float _chatInputRepeatAt;
        private static FieldInfo _chatCaretTextField;
        private static FieldInfo _chatCaretCaretTextField;
        private static FieldInfo _chatCaretMessageField;
        private static FieldInfo _chatCaretShowCursorField;
        private static FieldInfo _chatCaretHideField;
        private static FieldInfo _chatCaretForceShowField;
        private static PropertyInfo _chatCaretTextProperty;
        private static PropertyInfo _chatTextRectTransformProperty;
        private static PropertyInfo _chatTextPixelsPerUnitProperty;
        private static PropertyInfo _chatTextFontSizeProperty;
        private static PropertyInfo _chatTextHorizontalOverflowProperty;
        private static PropertyInfo _chatTextVerticalOverflowProperty;
        private static MethodInfo _chatTextGetGenerationSettingsMethod;
        private static PropertyInfo _chatTextCachedTextGeneratorProperty;
        private static TextGenerator _chatMessageTextGenerator;
        private static TextGenerator _chatVisibleTextGenerator;
        private static string _chatViewportLayoutMessage;
        private static float _chatViewportLayoutWidth;
        private static float _chatViewportLayoutHeight;
        private static int _chatViewportLayoutFontSize;
        private static float _chatViewportLayoutPixelsPerUnit;
        private static int[] _chatViewportLineStarts;
        private static int[] _chatViewportLineEnds;
        private static float _chatViewportLineHeight;
        private static int _chatViewportVisibleLineCount;
        private static int _chatViewportFirstLine;
        private static bool _chatViewportStateInitialized;
        private static bool _chatViewportWasOpen;
        private static Caret _chatViewportActiveCaret;
        private static bool _chatVerticalNavigationActive;
        private static float _chatVerticalPreferredX;
        private static string _chatVerticalNavigationMessage;
        private static int _chatVerticalNavigationCaret;
        private static int _chatVerticalNavigationLine;
        private static bool _chatVerticalKeyConsumedThisFrame;
        private static bool _chatVerticalTraceActive;
        private static int _chatVerticalTraceFrame = -1;
        private static KeyCode _chatVerticalTraceKey;
        private static object _chatViewportConfiguredText;
        private static object _chatViewportConfiguredCaretText;
        private static object _chatViewportTextOriginalHorizontalOverflow;
        private static object _chatViewportTextOriginalVerticalOverflow;
        private static object _chatViewportCaretOriginalHorizontalOverflow;
        private static object _chatViewportCaretOriginalVerticalOverflow;
        private static Caret _chatCharacterCountOwner;
        private static Text _chatCharacterCountText;
        private static RectTransform _chatCharacterCountSourceRect;
        private static bool _chatImeModeEnabled;
        private static bool _chatImeCompositionActiveThisFrame;
        private static bool _chatImeCompositionWasActive;
        private static bool _chatImeLastCompositionActive;
        private static bool _chatImeCompositionStateKnown;
        private static bool _chatImeSuppressNativeLetters;
        private static bool _chatImeAllowInsert;

        private static void PatchChatPauseBoundary()
        {
            var keyboardUpdate = typeof(KeyboardInput).GetMethod(
                "Update", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            var pauseToggle = typeof(PauseController).GetMethod(
                "TogglePause", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            var submitMessage = typeof(MessageController).GetMethod(
                "SubmitMessage", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(PID), typeof(int) }, null);
            var insertLetter = typeof(MessageController).GetMethod(
                "InsertLetter", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(string) }, null);
            var playerGetInput = typeof(Player).GetMethod(
                "GetInput", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(bool).MakeByRefType(),
                    typeof(bool).MakeByRefType(),
                    typeof(bool).MakeByRefType(),
                    typeof(bool).MakeByRefType(),
                    typeof(bool).MakeByRefType(),
                    typeof(bool).MakeByRefType(),
                    typeof(bool).MakeByRefType(),
                    typeof(bool).MakeByRefType(),
                    typeof(bool).MakeByRefType(),
                    typeof(bool).MakeByRefType()
                },
                null);
            var keyboardPrefix = typeof(HarmonyDiagnostics).GetMethod(
                "KeyboardInputChatBoundaryPrefix", BindingFlags.NonPublic | BindingFlags.Static);
            var keyboardPostfix = typeof(HarmonyDiagnostics).GetMethod(
                "KeyboardInputChatRepeatPostfix", BindingFlags.NonPublic | BindingFlags.Static);
            var playerInputPrefix = typeof(HarmonyDiagnostics).GetMethod(
                "PlayerChatInputBlockPrefix", BindingFlags.NonPublic | BindingFlags.Static);
            var insertLetterPrefix = typeof(HarmonyDiagnostics).GetMethod(
                "MessageControllerChatImeInsertLetterPrefix", BindingFlags.NonPublic | BindingFlags.Static);
            var pausePostfix = typeof(HarmonyDiagnostics).GetMethod(
                "PauseControllerChatBoundaryPostfix", BindingFlags.NonPublic | BindingFlags.Static);
            var submitPostfix = typeof(HarmonyDiagnostics).GetMethod(
                "MessageControllerChatInputRepeatCleanupPostfix", BindingFlags.NonPublic | BindingFlags.Static);
            if (keyboardUpdate == null || pauseToggle == null || submitMessage == null || insertLetter == null ||
                playerGetInput == null || keyboardPrefix == null || keyboardPostfix == null ||
                playerInputPrefix == null || insertLetterPrefix == null || pausePostfix == null ||
                submitPostfix == null)
            {
                DiagnosticLog.Warning("Chat boundary patch could not resolve its target methods.");
                return;
            }

            try
            {
                ClearChatInputRepeatState();
                DisableChatIme();
                _keyboardSkipNextFrameField = typeof(KeyboardInput).GetField(
                    "skipNextFrame", BindingFlags.NonPublic | BindingFlags.Static);
                _harmony.Patch(
                    keyboardUpdate,
                    new HarmonyMethod(keyboardPrefix),
                    new HarmonyMethod(keyboardPostfix),
                    null,
                    null);
                _harmony.Patch(
                    insertLetter,
                    new HarmonyMethod(insertLetterPrefix),
                    null,
                    null,
                    null);
                _harmony.Patch(
                    playerGetInput,
                    new HarmonyMethod(playerInputPrefix),
                    null,
                    null,
                    null);
                _harmony.Patch(pauseToggle, null, new HarmonyMethod(pausePostfix), null, null);
                _harmony.Patch(submitMessage, null, new HarmonyMethod(submitPostfix), null, null);
                DiagnosticLog.Info(
                    "Chat boundary patch enabled for KeyboardInput.Update, MessageController.SubmitMessage, " +
                    "Player.GetInput, and PauseController.TogglePause.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Chat Escape boundary patch failed: " + exception);
            }
        }

        private static void PatchChatMessageLengthLimit()
        {
            var updateText = typeof(MessageController).GetMethod(
                "UpdateText",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            var transpiler = typeof(HarmonyDiagnostics).GetMethod(
                "MessageControllerUpdateTextTranspiler",
                BindingFlags.NonPublic | BindingFlags.Static);
            var viewportPostfix = typeof(HarmonyDiagnostics).GetMethod(
                "MessageControllerUpdateTextViewportPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var flowPrefix = typeof(HarmonyDiagnostics).GetMethod(
                "MessageControllerUpdateTextChatVerticalTracePrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (updateText == null || transpiler == null || viewportPostfix == null ||
                flowPrefix == null)
            {
                DiagnosticLog.Warning(
                    "Chat message length patch could not resolve MessageController.UpdateText.");
                return;
            }

            try
            {
                _harmony.Patch(updateText, null, null, new HarmonyMethod(transpiler), null);
                _harmony.Patch(updateText, null, new HarmonyMethod(viewportPostfix), null, null);
                _harmony.Patch(
                    updateText,
                    new HarmonyMethod(flowPrefix),
                    null,
                    null,
                    null);
                DiagnosticLog.Info(
                    "Chat message length patch enabled; message limit is " +
                    ChatMessageMaxLength + " characters.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Chat message length patch failed: " + exception);
            }
        }

        private static IEnumerable<CodeInstruction> MessageControllerUpdateTextTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var result = new List<CodeInstruction>(instructions);
            var nativeMin = typeof(Mathf).GetMethod(
                "Min",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(int) },
                null);
            var keepFullLength = typeof(HarmonyDiagnostics).GetMethod(
                "LimitChatMessageLength",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(int) },
                null);
            if (nativeMin == null || keepFullLength == null)
            {
                DiagnosticLog.Warning(
                    "Chat message length transpiler could not resolve integer Mathf.Min methods.");
                return result;
            }

            var minCallCount = 0;
            for (var index = 0; index < result.Count; index++)
            {
                if (!nativeMin.Equals(result[index].operand as MethodInfo))
                {
                    continue;
                }

                minCallCount++;
            }

            if (minCallCount != 2)
            {
                DiagnosticLog.Warning(
                    "Chat message length transpiler expected two integer Mathf.Min calls; found " +
                    minCallCount + ". Native length limit was not changed.");
                return result;
            }

            for (var index = 0; index < result.Count; index++)
            {
                if (!nativeMin.Equals(result[index].operand as MethodInfo))
                {
                    continue;
                }

                result[index].opcode = OpCodes.Call;
                result[index].operand = keepFullLength;
                break;
            }

            DiagnosticLog.Info(
                "Chat message length transpiler replaced the first integer Mathf.Min call; " +
                "caret boundary call remains native.");
            return result;
        }

        private static int LimitChatMessageLength(int ignoredNativeLimit, int messageLength)
        {
            return messageLength > ChatMessageMaxLength ? ChatMessageMaxLength : messageLength;
        }

        private static void PatchChatMessageViewport()
        {
            var update = typeof(ChatTextBox).GetMethod(
                "Update",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            var submitMessage = typeof(ChatTextBox).GetMethod(
                "SubmitMessage",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(PID), typeof(int), typeof(int) },
                null);
            var postfix = typeof(HarmonyDiagnostics).GetMethod(
                "ChatTextBoxUpdatePostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var submitPostfix = typeof(HarmonyDiagnostics).GetMethod(
                "ChatTextBoxSubmitMessagePostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var messageUpdate = typeof(MessageController).GetMethod(
                "Update",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            var messageUpdatePrefix = typeof(HarmonyDiagnostics).GetMethod(
                "MessageControllerChatVerticalTracePrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var caretLateUpdate = typeof(Caret).GetMethod(
                "LateUpdate",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            var caretLateUpdatePostfix = typeof(HarmonyDiagnostics).GetMethod(
                "CaretChatVerticalTracePostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var populateMeshPrefix = typeof(HarmonyDiagnostics).GetMethod(
                "ChatTextOnPopulateMeshPrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (update == null || submitMessage == null || postfix == null ||
                submitPostfix == null || messageUpdate == null ||
                messageUpdatePrefix == null || caretLateUpdate == null ||
                caretLateUpdatePostfix == null || populateMeshPrefix == null)
            {
                DiagnosticLog.Warning(
                    "Chat message viewport patch could not resolve ChatTextBox.Update or its mesh prefix.");
                return;
            }

            try
            {
                _chatCaretTextField = typeof(Caret).GetField(
                    "text", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _chatCaretCaretTextField = typeof(Caret).GetField(
                    "caretText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _chatCaretMessageField = typeof(Caret).GetField(
                    "message", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _chatCaretShowCursorField = typeof(Caret).GetField(
                    "showCursor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _chatCaretHideField = typeof(Caret).GetField(
                    "hide", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _chatCaretForceShowField = typeof(Caret).GetField(
                    "forceShow", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _chatCaretTextProperty = null;
                _chatTextRectTransformProperty = null;
                _chatTextPixelsPerUnitProperty = null;
                _chatTextFontSizeProperty = null;
                _chatTextHorizontalOverflowProperty = null;
                _chatTextVerticalOverflowProperty = null;
                _chatTextGetGenerationSettingsMethod = null;
                _chatTextCachedTextGeneratorProperty = null;
                if (_chatCaretTextField != null)
                {
                    var textType = _chatCaretTextField.FieldType;
                    var onPopulateMesh = FindChatTextMeshMethod(textType);
                    _chatCaretTextProperty = textType.GetProperty(
                        "text", BindingFlags.Public | BindingFlags.Instance);
                    _chatTextRectTransformProperty = textType.GetProperty(
                        "rectTransform", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    _chatTextPixelsPerUnitProperty = textType.GetProperty(
                        "pixelsPerUnit", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    _chatTextFontSizeProperty = textType.GetProperty(
                        "fontSize", BindingFlags.Public | BindingFlags.Instance);
                    _chatTextHorizontalOverflowProperty = textType.GetProperty(
                        "horizontalOverflow", BindingFlags.Public | BindingFlags.Instance);
                    _chatTextVerticalOverflowProperty = textType.GetProperty(
                        "verticalOverflow", BindingFlags.Public | BindingFlags.Instance);
                    _chatTextGetGenerationSettingsMethod = textType.GetMethod(
                        "GetGenerationSettings",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        new[] { typeof(Vector2) },
                        null);
                    _chatTextCachedTextGeneratorProperty = textType.GetProperty(
                        "cachedTextGenerator",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (onPopulateMesh == null)
                    {
                        DiagnosticLog.Warning(
                            "Chat message viewport patch could not resolve Text.OnPopulateMesh.");
                        return;
                    }

                    _harmony.Patch(
                        onPopulateMesh,
                        new HarmonyMethod(populateMeshPrefix),
                        null,
                        null,
                        null);
                }

                if (_chatCaretTextField == null || _chatCaretCaretTextField == null ||
                    _chatCaretTextProperty == null ||
                    _chatTextRectTransformProperty == null ||
                    _chatTextHorizontalOverflowProperty == null ||
                    _chatTextVerticalOverflowProperty == null ||
                    _chatTextGetGenerationSettingsMethod == null)
                {
                    DiagnosticLog.Warning(
                        "Chat message viewport patch could not resolve Text geometry or generation settings.");
                    return;
                }

                _chatMessageTextGenerator = new TextGenerator();
                _chatVisibleTextGenerator = new TextGenerator();
                _chatViewportLayoutMessage = null;
                _chatViewportLineStarts = null;
                _chatViewportLineEnds = null;
                _chatViewportStateInitialized = false;
                _chatViewportWasOpen = false;
                _chatViewportActiveCaret = null;
                ResetChatVerticalNavigation();
                _chatVerticalTraceActive = false;
                _chatVerticalTraceFrame = -1;
                _chatVerticalTraceKey = KeyCode.None;
                _chatViewportConfiguredText = null;
                _chatViewportConfiguredCaretText = null;
                _harmony.Patch(update, null, new HarmonyMethod(postfix), null, null);
                _harmony.Patch(
                    submitMessage,
                    null,
                    new HarmonyMethod(submitPostfix),
                    null,
                    null);
                _harmony.Patch(
                    messageUpdate,
                    new HarmonyMethod(messageUpdatePrefix),
                    null,
                    null,
                    null);
                _harmony.Patch(
                    caretLateUpdate,
                    null,
                    new HarmonyMethod(caretLateUpdatePostfix),
                    null,
                    null);
                DiagnosticLog.Info(
                    "Chat message viewport patch enabled; display uses TextGenerator pixel measurements and " +
                    "Text.OnPopulateMesh synchronization.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Chat message viewport patch failed: " + exception);
            }
        }

        private static MethodInfo FindChatTextMeshMethod(Type textType)
        {
            if (textType == null)
            {
                return null;
            }

            var methods = textType.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (method.Name == "OnPopulateMesh" && method.GetParameters().Length == 1)
                {
                    return method;
                }
            }

            return null;
        }

        private static void ChatTextBoxUpdatePostfix(
            ChatTextBox __instance,
            List<Text> ___chatMessages,
            Vector3 ___anchorStart)
        {
            if (__instance == null)
            {
                return;
            }

            ApplyChatHistoryVisibility(__instance, ___chatMessages, ___anchorStart);
            if (__instance.chatText != null)
            {
                ApplyChatViewport(__instance.chatText);
            }

            UpdateChatCharacterCount(__instance.chatText);
            RecordChatVerticalTraceStage("ChatTextBox.Update");
        }

        private static void ChatTextBoxSubmitMessagePostfix(
            ChatTextBox __instance,
            List<Text> ___chatMessages,
            float ___offset)
        {
            if (__instance == null || ___chatMessages == null || ___chatMessages.Count == 0)
            {
                return;
            }

            var latestMessage = ___chatMessages[___chatMessages.Count - 1];
            if (latestMessage == null)
            {
                return;
            }

            var expectedPosition = Vector3.down * ___offset;
            latestMessage.transform.localPosition = expectedPosition;
            __instance.StartCoroutine(
                CorrectChatMessagePositionAtEndOfFrame(latestMessage, expectedPosition));
        }

        private static IEnumerator CorrectChatMessagePositionAtEndOfFrame(
            Text message,
            Vector3 expectedPosition)
        {
            yield return new WaitForEndOfFrame();
            if (message != null)
            {
                message.transform.localPosition = expectedPosition;
            }
        }

        private static void ApplyChatHistoryVisibility(
            ChatTextBox chatTextBox,
            List<Text> chatMessages,
            Vector3 anchorStart)
        {
            if (chatMessages == null || chatMessages.Count == 0)
            {
                return;
            }

            var visibleHeight = GetChatHistoryVisibleHeight(chatTextBox, anchorStart);
            var usedHeight = 0f;
            for (var index = chatMessages.Count - 1; index >= 0; index--)
            {
                var message = chatMessages[index];
                if (message == null || message.rectTransform == null)
                {
                    continue;
                }

                usedHeight += message.rectTransform.sizeDelta.y + ChatHistoryMessageSpacing;
                var isLatestMessage = index == chatMessages.Count - 1;
                message.gameObject.SetActive(isLatestMessage || usedHeight <= visibleHeight);
            }
        }

        private static float GetChatHistoryVisibleHeight(
            ChatTextBox chatTextBox,
            Vector3 anchorStart)
        {
            var messageParent = chatTextBox.messageAnchor == null
                ? null
                : chatTextBox.messageAnchor.parent;
            var visualRoot = chatTextBox.chatText == null
                ? null
                : chatTextBox.chatText.transform.parent;
            if (messageParent == null || visualRoot == null)
            {
                return ChatHistoryFallbackVisibleHeight;
            }

            RectTransform historyBoundary = null;
            var components = visualRoot.GetComponentsInChildren<Component>(true);
            for (var index = 0; index < components.Length; index++)
            {
                var component = components[index];
                var imageRect = component == null ||
                                component.GetType().FullName != "UnityEngine.UI.Image"
                    ? null
                    : component.GetComponent<RectTransform>();
                if (imageRect != null &&
                    (historyBoundary == null ||
                     imageRect.rect.height > historyBoundary.rect.height))
                {
                    historyBoundary = imageRect;
                }
            }

            if (historyBoundary == null)
            {
                return ChatHistoryFallbackVisibleHeight;
            }

            var corners = new Vector3[4];
            historyBoundary.GetWorldCorners(corners);
            var topY = messageParent.InverseTransformPoint(corners[0]).y;
            for (var index = 1; index < corners.Length; index++)
            {
                topY = Mathf.Max(topY, messageParent.InverseTransformPoint(corners[index]).y);
            }

            // The newest message ends at anchorStart; its first 10-unit gap is not message content.
            return Mathf.Max(0f, topY - anchorStart.y + ChatHistoryMessageSpacing);
        }

        private static void UpdateChatCharacterCount(Caret chatCaret)
        {
            if (!KeyboardInput.open || chatCaret == null || _chatCaretTextField == null)
            {
                HideChatCharacterCount();
                return;
            }

            var inputText = _chatCaretTextField.GetValue(chatCaret) as Text;
            if (inputText == null || inputText.rectTransform == null)
            {
                HideChatCharacterCount();
                return;
            }

            if (_chatCharacterCountText == null ||
                !object.ReferenceEquals(_chatCharacterCountOwner, chatCaret) ||
                !object.ReferenceEquals(_chatCharacterCountSourceRect, inputText.rectTransform))
            {
                CreateChatCharacterCount(chatCaret, inputText);
            }

            if (_chatCharacterCountText == null)
            {
                return;
            }

            var messageLength = MessageController.message == null
                ? 0
                : MessageController.message.Length;
            _chatCharacterCountText.text = messageLength + "/" + ChatMessageMaxLength;
            _chatCharacterCountText.gameObject.SetActive(true);
        }

        private static void CreateChatCharacterCount(Caret chatCaret, Text sourceText)
        {
            DestroyChatCharacterCount();

            var countObject = new GameObject(
                "ChatCharacterCount",
                typeof(RectTransform),
                typeof(Text));
            var countText = countObject.GetComponent<Text>();
            var countRect = countObject.GetComponent<RectTransform>();
            countText.font = sourceText.font;
            countText.fontStyle = sourceText.fontStyle;
            countText.fontSize = ChatCharacterCountFontSize;
            countText.alignment = TextAnchor.LowerRight;
            countText.supportRichText = false;
            countText.horizontalOverflow = HorizontalWrapMode.Overflow;
            countText.verticalOverflow = VerticalWrapMode.Truncate;
            countText.lineSpacing = sourceText.lineSpacing;
            countText.color = sourceText.color;
            countText.material = sourceText.material;
            countText.raycastTarget = false;

            var sourceShadow = sourceText.GetComponent<Shadow>();
            if (sourceShadow != null)
            {
                var countShadow = countObject.AddComponent<Shadow>();
                countShadow.effectColor = sourceShadow.effectColor;
                countShadow.effectDistance = sourceShadow.effectDistance;
                countShadow.useGraphicAlpha = sourceShadow.useGraphicAlpha;
            }

            countRect.SetParent(sourceText.rectTransform, false);
            countRect.anchorMin = new Vector2(1f, 0f);
            countRect.anchorMax = new Vector2(1f, 0f);
            countRect.pivot = new Vector2(1f, 0f);
            countText.text = ChatMessageMaxLength + "/" + ChatMessageMaxLength;
            countRect.sizeDelta = new Vector2(
                ChatCharacterCountWidth,
                ChatCharacterCountHeight);
            countRect.anchoredPosition = new Vector2(
                -ChatCharacterCountRightOffset,
                ChatCharacterCountBottomOffset);
            countRect.SetAsLastSibling();

            _chatCharacterCountOwner = chatCaret;
            _chatCharacterCountText = countText;
            _chatCharacterCountSourceRect = sourceText.rectTransform;
        }

        private static void HideChatCharacterCount()
        {
            if (_chatCharacterCountText != null)
            {
                _chatCharacterCountText.gameObject.SetActive(false);
            }
        }

        private static void DestroyChatCharacterCount()
        {
            if (_chatCharacterCountText != null)
            {
                _chatCharacterCountText.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(_chatCharacterCountText.gameObject);
            }

            _chatCharacterCountOwner = null;
            _chatCharacterCountText = null;
            _chatCharacterCountSourceRect = null;
        }

        private static void MessageControllerChatVerticalTracePrefix(
            MessageController __instance)
        {
            if (__instance == null ||
                !object.ReferenceEquals(
                    __instance,
                    SingletonMono<MessageController>.Instance))
            {
                return;
            }

            RecordChatVerticalTraceStage("MessageController.Update");
        }

        private static void CaretChatVerticalTracePostfix(Caret __instance)
        {
            if (__instance == null ||
                !object.ReferenceEquals(__instance, _chatViewportActiveCaret))
            {
                return;
            }

            RecordChatVerticalTraceStage("Caret.LateUpdate");
        }

        private static void MessageControllerUpdateTextViewportPostfix()
        {
            if (!KeyboardInput.open)
            {
                return;
            }

            var chatTextBoxes = Resources.FindObjectsOfTypeAll<ChatTextBox>();
            if (chatTextBoxes == null)
            {
                return;
            }

            for (var index = 0; index < chatTextBoxes.Length; index++)
            {
                var chatTextBox = chatTextBoxes[index];
                if (chatTextBox != null && chatTextBox.isActiveAndEnabled &&
                    chatTextBox.chatText != null)
                {
                    ApplyChatViewport(chatTextBox.chatText);
                    UpdateChatCharacterCount(chatTextBox.chatText);
                }
            }
        }

        private static void ChatTextOnPopulateMeshPrefix(object __instance)
        {
            if (__instance == null || _chatViewportActiveCaret == null)
            {
                return;
            }

            var activeChatText = _chatCaretTextField == null
                ? null
                : _chatCaretTextField.GetValue(_chatViewportActiveCaret);
            var activeCaretText = _chatCaretCaretTextField == null
                ? null
                : _chatCaretCaretTextField.GetValue(_chatViewportActiveCaret);
            if (!object.ReferenceEquals(__instance, activeChatText) &&
                !object.ReferenceEquals(__instance, activeCaretText))
            {
                return;
            }

            ApplyChatViewport(_chatViewportActiveCaret);
            RecordChatVerticalTraceStage("Text.OnPopulateMesh");
            RecordChatVerticalTraceSnapshot();
        }

        private static void BeginChatVerticalTrace(KeyCode key)
        {
            _chatVerticalTraceActive = true;
            _chatVerticalTraceFrame = Time.frameCount;
            _chatVerticalTraceKey = key;
            RecordChatVerticalTraceStage("KeyboardInput.Update");
        }

        private static void MessageControllerUpdateTextChatVerticalTracePrefix(
            MessageController __instance)
        {
            if (__instance == null ||
                !object.ReferenceEquals(
                    __instance,
                    SingletonMono<MessageController>.Instance))
            {
                return;
            }

            RecordChatVerticalTraceStage("MessageController.UpdateText");
        }

        private static void RecordChatVerticalTraceStage(string stage)
        {
            if (!_chatVerticalTraceActive || _chatVerticalTraceFrame != Time.frameCount)
            {
                return;
            }

            DiagnosticLog.InfoFileOnly(
                "CHAT_VERTICAL frame=" + Time.frameCount +
                "; key=" + _chatVerticalTraceKey +
                "; stage=" + stage + ".");
        }

        private static void RecordChatVerticalTraceSnapshot()
        {
            if (!_chatVerticalTraceActive || _chatVerticalTraceFrame != Time.frameCount ||
                _chatViewportActiveCaret == null)
            {
                return;
            }

            var chatText = _chatCaretTextField.GetValue(_chatViewportActiveCaret);
            var caretText = _chatCaretCaretTextField.GetValue(_chatViewportActiveCaret);
            var message = MessageController.message ?? string.Empty;
            var viewportStart = 0;
            var viewportEnd = 0;
            if (_chatViewportLineStarts != null && _chatViewportLineStarts.Length > 0)
            {
                var firstLine = Mathf.Clamp(
                    _chatViewportFirstLine,
                    0,
                    _chatViewportLineStarts.Length - 1);
                var lastLine = Mathf.Min(
                    _chatViewportLineStarts.Length - 1,
                    firstLine + _chatViewportVisibleLineCount - 1);
                viewportStart = _chatViewportLineStarts[firstLine];
                viewportEnd = _chatViewportLineEnds[lastLine];
                while (viewportEnd > viewportStart &&
                       IsChatLineBreak(message[viewportEnd - 1]))
                {
                    viewportEnd--;
                }
            }

            var builder = new StringBuilder();
            builder.Append("CHAT_VERTICAL_STATE frame=");
            builder.Append(Time.frameCount);
            builder.Append("; key=");
            builder.Append(_chatVerticalTraceKey);
            builder.Append("; message=");
            builder.Append(FormatChatTraceText(message));
            builder.Append("; messageCaret=");
            builder.Append(MessageController.caretPos);
            builder.Append("; caretMessage=");
            builder.Append(FormatChatTraceText(
                (string)_chatCaretMessageField.GetValue(_chatViewportActiveCaret)));
            builder.Append("; caretPosition=");
            builder.Append(_chatViewportActiveCaret.position);
            builder.Append("; caretText=");
            builder.Append(FormatChatTraceText(
                (string)_chatCaretTextProperty.GetValue(chatText, null)));
            builder.Append("; caretCaretText=");
            builder.Append(FormatChatTraceText(
                caretText == null
                    ? string.Empty
                    : (string)_chatCaretTextProperty.GetValue(caretText, null)));
            builder.Append("; viewportFirstLine=");
            builder.Append(_chatViewportFirstLine);
            builder.Append("; viewportSource=");
            builder.Append(viewportStart);
            builder.Append("-");
            builder.Append(viewportEnd);
            builder.Append("; ");
            builder.Append(FormatChatGenerator(
                "ownTextGenerator",
                _chatMessageTextGenerator));
            builder.Append("; ");
            builder.Append(FormatChatGenerator(
                "mainCachedTextGenerator",
                GetChatCachedTextGenerator(chatText)));
            builder.Append("; ");
            builder.Append(FormatChatGenerator(
                "caretCachedTextGenerator",
                GetChatCachedTextGenerator(caretText)));
            builder.Append(".");
            DiagnosticLog.InfoFileOnly(builder.ToString());
            _chatVerticalTraceActive = false;
        }

        private static TextGenerator GetChatCachedTextGenerator(object chatText)
        {
            if (chatText == null || _chatTextCachedTextGeneratorProperty == null)
            {
                return null;
            }

            return _chatTextCachedTextGeneratorProperty.GetValue(chatText, null) as TextGenerator;
        }

        private static string FormatChatGenerator(string name, TextGenerator generator)
        {
            if (generator == null)
            {
                return name + "=unavailable";
            }

            var builder = new StringBuilder();
            builder.Append(name);
            builder.Append("{lineCount=");
            builder.Append(generator.lineCount);
            builder.Append(";characterCountVisible=");
            builder.Append(generator.characterCountVisible);
            builder.Append(";rectExtents=");
            builder.Append(FormatChatRect(generator.rectExtents));
            builder.Append(";lines=");
            var lines = generator.lines;
            for (var index = 0; lines != null && index < lines.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(",");
                }

                var line = lines[index];
                builder.Append("[");
                builder.Append("startCharIdx=");
                builder.Append(line.startCharIdx);
                builder.Append(";height=");
                builder.Append(line.height);
                builder.Append(";top=");
                builder.Append(line.topY);
                builder.Append(";characterCountVisible=");
                builder.Append(generator.characterCountVisible);
                builder.Append(";rectExtents=");
                builder.Append(FormatChatRect(generator.rectExtents));
                builder.Append("]");
            }

            builder.Append("}");
            return builder.ToString();
        }

        private static string FormatChatRect(Rect rect)
        {
            return rect.x + "," + rect.y + "," + rect.width + "," + rect.height;
        }

        private static string FormatChatTraceText(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace(";", "\\;");
        }

        private static void ApplyChatViewport(Caret chatCaret)
        {
            if (chatCaret == null || _chatCaretTextField == null || _chatCaretTextProperty == null)
            {
                return;
            }

            _chatViewportActiveCaret = chatCaret;

            if (!KeyboardInput.open)
            {
                var closedChatText = _chatCaretTextField.GetValue(chatCaret);
                var closedCaretText = _chatCaretCaretTextField == null
                    ? null
                    : _chatCaretCaretTextField.GetValue(chatCaret);
                RestoreChatViewportText(closedChatText, false);
                RestoreChatViewportText(closedCaretText, true);
                _chatViewportStateInitialized = false;
                _chatViewportWasOpen = false;
                _chatViewportActiveCaret = null;
                return;
            }

            if (!_chatViewportWasOpen)
            {
                _chatViewportStateInitialized = false;
                _chatViewportFirstLine = 0;
                _chatViewportWasOpen = true;
            }

            var chatText = _chatCaretTextField.GetValue(chatCaret);
            if (chatText == null)
            {
                return;
            }

            var message = MessageController.message ?? string.Empty;
            if (message.Length == 0)
            {
                _chatCaretTextProperty.SetValue(chatText, string.Empty, null);
                SetChatCaretMessage(chatCaret, string.Empty);
                chatCaret.position = 0;
                var emptyCaretText = _chatCaretCaretTextField.GetValue(chatCaret);
                if (emptyCaretText != null)
                {
                    SetChatCaretDisplayText(chatCaret, emptyCaretText, string.Empty, 0);
                }
                _chatViewportStateInitialized = false;
                return;
            }

            var viewportWidth = GetChatTextViewportWidth(chatText);
            var viewportHeight = GetChatTextViewportHeight(chatText);
            if (viewportWidth <= 0f || viewportHeight <= 0f ||
                _chatMessageTextGenerator == null)
            {
                return;
            }

            var pixelsPerUnit = GetChatTextPixelsPerUnit(chatText);
            var fontSize = GetChatTextFontSize(chatText);
            SetChatTextWrapMode(chatText, false);
            var caretText = _chatCaretCaretTextField.GetValue(chatCaret);
            if (caretText != null)
            {
                SetChatTextWrapMode(caretText, true);
            }

            EnsureChatViewportLayout(
                chatText,
                message,
                viewportWidth,
                viewportHeight,
                pixelsPerUnit,
                fontSize);

            if (_chatViewportLineStarts == null || _chatViewportLineEnds == null ||
                _chatViewportLineStarts.Length == 0 ||
                _chatViewportLineEnds.Length != _chatViewportLineStarts.Length)
            {
                _chatViewportStateInitialized = false;
                return;
            }

            int displayCaretPosition;
            var displayText = GetChatMessageDisplay(
                message,
                MessageController.caretPos,
                out displayCaretPosition);
            SetChatCaretMessage(chatCaret, displayText);
            _chatCaretTextProperty.SetValue(chatText, displayText, null);
            chatCaret.position = displayCaretPosition;
            var currentCaretText = _chatCaretCaretTextField.GetValue(chatCaret);
            if (currentCaretText != null)
            {
                SetChatCaretDisplayText(
                    chatCaret,
                    currentCaretText,
                    displayText,
                    displayCaretPosition);
            }
        }

        private static void ApplyActiveChatViewport()
        {
            if (_chatViewportActiveCaret != null)
            {
                ApplyChatViewport(_chatViewportActiveCaret);
                return;
            }

            var chatTextBoxes = Resources.FindObjectsOfTypeAll<ChatTextBox>();
            if (chatTextBoxes == null)
            {
                return;
            }

            for (var index = 0; index < chatTextBoxes.Length; index++)
            {
                var chatTextBox = chatTextBoxes[index];
                if (chatTextBox != null && chatTextBox.isActiveAndEnabled &&
                    chatTextBox.chatText != null)
                {
                    ApplyChatViewport(chatTextBox.chatText);
                    return;
                }
            }
        }

        private static bool MoveChatCaretVertical(int direction)
        {
            ApplyActiveChatViewport();

            var message = MessageController.message ?? string.Empty;
            if (message.Length == 0)
            {
                ResetChatVerticalNavigation();
                return false;
            }

            if (_chatViewportLineStarts == null || _chatViewportLineEnds == null ||
                _chatViewportLineStarts.Length == 0)
            {
                return false;
            }

            if (_chatViewportLineStarts.Length == 1)
            {
                ResetChatVerticalNavigation();
                ApplyActiveChatViewport();
                return false;
            }

            var caret = Mathf.Clamp(MessageController.caretPos, 0, message.Length);
            var sameNavigation = _chatVerticalNavigationActive &&
                                  string.Equals(
                                      _chatVerticalNavigationMessage,
                                      message,
                                      StringComparison.Ordinal) &&
                                  _chatVerticalNavigationCaret == caret;
            var currentLine = sameNavigation
                ? _chatVerticalNavigationLine
                : FindChatViewportLine(caret);
            if (!sameNavigation)
            {
                _chatVerticalPreferredX = GetChatCaretX(message, currentLine, caret);
                _chatVerticalNavigationMessage = message;
                _chatVerticalNavigationCaret = caret;
                _chatVerticalNavigationLine = currentLine;
                _chatVerticalNavigationActive = true;
            }

            var targetLine = currentLine + direction;
            if (targetLine >= 0 && targetLine < _chatViewportLineStarts.Length)
            {
                var targetCaret = FindChatCaretClosestToX(
                    message,
                    targetLine,
                    _chatVerticalPreferredX);
                MessageController.caretPos = targetCaret;
                _chatVerticalNavigationCaret = targetCaret;
                _chatVerticalNavigationLine = targetLine;
            }

            ApplyActiveChatViewport();
            return true;
        }

        private static float GetChatCaretX(string message, int line, int caret)
        {
            var lineStart = _chatViewportLineStarts[line];
            var lineEnd = GetChatLineCaretEnd(message, line);
            var clampedCaret = Mathf.Clamp(caret, lineStart, lineEnd);
            var characters = _chatMessageTextGenerator.characters;

            for (var sourceIndex = clampedCaret - 1; sourceIndex >= lineStart; sourceIndex--)
            {
                if (IsChatLineBreak(message[sourceIndex]) || sourceIndex >= characters.Count)
                {
                    continue;
                }

                var character = characters[sourceIndex];
                return character.cursorPos.x + character.charWidth;
            }

            for (var sourceIndex = clampedCaret; sourceIndex < lineEnd; sourceIndex++)
            {
                if (IsChatLineBreak(message[sourceIndex]) || sourceIndex >= characters.Count)
                {
                    continue;
                }

                return characters[sourceIndex].cursorPos.x;
            }

            return 0f;
        }

        private static int FindChatCaretClosestToX(string message, int line, float preferredX)
        {
            var lineStart = _chatViewportLineStarts[line];
            var lineEnd = GetChatLineCaretEnd(message, line);
            var bestCaret = lineStart;
            var bestDistance = float.MaxValue;
            for (var candidate = lineStart; candidate <= lineEnd; candidate++)
            {
                var distance = Mathf.Abs(GetChatCaretX(message, line, candidate) - preferredX);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCaret = candidate;
                }
            }

            return bestCaret;
        }

        private static int GetChatLineCaretEnd(string message, int line)
        {
            var lineStart = _chatViewportLineStarts[line];
            var lineEnd = _chatViewportLineEnds[line];
            while (lineEnd > lineStart && IsChatLineBreak(message[lineEnd - 1]))
            {
                lineEnd--;
            }

            return lineEnd;
        }

        private static bool IsChatLineBreak(char character)
        {
            return character == '\r' || character == '\n';
        }

        private static void ResetChatVerticalNavigation()
        {
            _chatVerticalNavigationActive = false;
            _chatVerticalPreferredX = 0f;
            _chatVerticalNavigationMessage = null;
            _chatVerticalNavigationCaret = 0;
            _chatVerticalNavigationLine = 0;
        }

        private static void SetChatCaretMessage(Caret chatCaret, string message)
        {
            if (chatCaret == null || _chatCaretMessageField == null)
            {
                return;
            }

            _chatCaretMessageField.SetValue(chatCaret, message);
        }

        private static bool GetChatCaretBool(FieldInfo field, Caret chatCaret)
        {
            if (field == null || chatCaret == null)
            {
                return false;
            }

            var value = field.GetValue(chatCaret);
            return value != null && (bool)value;
        }

        private static void SetChatCaretDisplayText(
            Caret chatCaret,
            object caretText,
            string displayText,
            int displayCaretPosition)
        {
            var caret = Mathf.Clamp(displayCaretPosition, 0, displayText.Length);
            var caretPrefix = displayText.Substring(0, caret);
            var showCursor = GetChatCaretBool(_chatCaretForceShowField, chatCaret) ||
                             (GetChatCaretBool(_chatCaretShowCursorField, chatCaret) &&
                              !GetChatCaretBool(_chatCaretHideField, chatCaret));
            var value = "<color=#00ffff00>" + caretPrefix + "</color>" +
                        (showCursor ? "|" : string.Empty);
            _chatCaretTextProperty.SetValue(caretText, value, null);
        }

        private static void SetChatTextWrapMode(object chatText, bool isCaretText)
        {
            if (chatText == null)
            {
                return;
            }

            if (isCaretText)
            {
                if (!object.ReferenceEquals(_chatViewportConfiguredCaretText, chatText))
                {
                    _chatViewportConfiguredCaretText = chatText;
                    _chatViewportCaretOriginalHorizontalOverflow =
                        _chatTextHorizontalOverflowProperty.GetValue(chatText, null);
                    _chatViewportCaretOriginalVerticalOverflow =
                        _chatTextVerticalOverflowProperty.GetValue(chatText, null);
                }
            }
            else if (!object.ReferenceEquals(_chatViewportConfiguredText, chatText))
            {
                _chatViewportConfiguredText = chatText;
                _chatViewportTextOriginalHorizontalOverflow =
                    _chatTextHorizontalOverflowProperty.GetValue(chatText, null);
                _chatViewportTextOriginalVerticalOverflow =
                    _chatTextVerticalOverflowProperty.GetValue(chatText, null);
            }

            _chatTextHorizontalOverflowProperty.SetValue(
                chatText,
                HorizontalWrapMode.Wrap,
                null);
            _chatTextVerticalOverflowProperty.SetValue(
                chatText,
                VerticalWrapMode.Truncate,
                null);
        }

        private static void RestoreChatViewportText(object chatText, bool isCaretText)
        {
            if (chatText == null)
            {
                return;
            }

            if (isCaretText && object.ReferenceEquals(_chatViewportConfiguredCaretText, chatText))
            {
                _chatTextHorizontalOverflowProperty.SetValue(
                    chatText,
                    _chatViewportCaretOriginalHorizontalOverflow,
                    null);
                _chatTextVerticalOverflowProperty.SetValue(
                    chatText,
                    _chatViewportCaretOriginalVerticalOverflow,
                    null);
            }
            else if (!isCaretText && object.ReferenceEquals(_chatViewportConfiguredText, chatText))
            {
                _chatTextHorizontalOverflowProperty.SetValue(
                    chatText,
                    _chatViewportTextOriginalHorizontalOverflow,
                    null);
                _chatTextVerticalOverflowProperty.SetValue(
                    chatText,
                    _chatViewportTextOriginalVerticalOverflow,
                    null);
            }
        }

        private static float GetChatTextViewportWidth(object chatText)
        {
            var rectTransform = _chatTextRectTransformProperty.GetValue(chatText, null) as RectTransform;
            return rectTransform == null ? 0f : rectTransform.rect.width;
        }

        private static float GetChatTextViewportHeight(object chatText)
        {
            var rectTransform = _chatTextRectTransformProperty.GetValue(chatText, null) as RectTransform;
            return rectTransform == null ? 0f : rectTransform.rect.height;
        }

        private static float GetChatTextPixelsPerUnit(object chatText)
        {
            if (_chatTextPixelsPerUnitProperty == null)
            {
                return 1f;
            }

            var value = _chatTextPixelsPerUnitProperty.GetValue(chatText, null);
            var pixelsPerUnit = value == null ? 1f : Convert.ToSingle(value);
            return pixelsPerUnit > 0f ? pixelsPerUnit : 1f;
        }

        private static int GetChatTextFontSize(object chatText)
        {
            if (_chatTextFontSizeProperty == null)
            {
                return 0;
            }

            var value = _chatTextFontSizeProperty.GetValue(chatText, null);
            return value == null ? 0 : Convert.ToInt32(value);
        }

        private static void EnsureChatViewportLayout(
            object chatText,
            string message,
            float viewportWidth,
            float viewportHeight,
            float pixelsPerUnit,
            int fontSize)
        {
            if (string.Equals(_chatViewportLayoutMessage, message, StringComparison.Ordinal) &&
                Mathf.Approximately(_chatViewportLayoutWidth, viewportWidth) &&
                Mathf.Approximately(_chatViewportLayoutHeight, viewportHeight) &&
                _chatViewportLayoutFontSize == fontSize &&
                Mathf.Approximately(_chatViewportLayoutPixelsPerUnit, pixelsPerUnit))
            {
                return;
            }

            var settings = (TextGenerationSettings)_chatTextGetGenerationSettingsMethod.Invoke(
                chatText,
                new object[] { new Vector2(viewportWidth, viewportHeight) });
            settings.horizontalOverflow = HorizontalWrapMode.Wrap;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.generationExtents = new Vector2(
                Mathf.Max(1f, viewportWidth),
                100000f);
            settings.generateOutOfBounds = true;

            _chatMessageTextGenerator.Invalidate();
            _chatMessageTextGenerator.Populate(message, settings);

            var visibleSettings = settings;
            visibleSettings.verticalOverflow = VerticalWrapMode.Truncate;
            visibleSettings.generationExtents = new Vector2(
                Mathf.Max(1f, viewportWidth),
                Mathf.Max(1f, viewportHeight));
            visibleSettings.generateOutOfBounds = false;
            _chatVisibleTextGenerator.Invalidate();
            _chatVisibleTextGenerator.Populate(message, visibleSettings);

            var lines = _chatMessageTextGenerator.lines;
            var lineCount = lines == null ? 0 : lines.Count;
            var generatedLineHeight = lineCount > 0 ? lines[0].height : 0;
            if (lineCount == 0)
            {
                lineCount = 1;
                _chatViewportLineStarts = new[] { 0 };
                _chatViewportLineEnds = new[] { message.Length };
            }
            else
            {
                _chatViewportLineStarts = new int[lineCount];
                _chatViewportLineEnds = new int[lineCount];
                for (var index = 0; index < lineCount; index++)
                {
                    _chatViewportLineStarts[index] = Mathf.Clamp(
                        lines[index].startCharIdx,
                        0,
                        message.Length);
                    if (index > 0 &&
                        _chatViewportLineStarts[index] < _chatViewportLineStarts[index - 1])
                    {
                        _chatViewportLineStarts[index] = _chatViewportLineStarts[index - 1];
                    }
                }

                for (var index = 0; index < lineCount; index++)
                {
                    _chatViewportLineEnds[index] = index + 1 < lineCount
                        ? _chatViewportLineStarts[index + 1]
                        : message.Length;
                }
            }

            _chatViewportLineHeight = generatedLineHeight > 0
                ? generatedLineHeight / pixelsPerUnit
                : Mathf.Max(1f, fontSize);
            var visibleLineCount = _chatVisibleTextGenerator.lines == null
                ? 0
                : _chatVisibleTextGenerator.lines.Count;
            _chatViewportVisibleLineCount = Mathf.Clamp(
                visibleLineCount,
                1,
                lineCount);
            _chatViewportLayoutMessage = message;
            _chatViewportLayoutWidth = viewportWidth;
            _chatViewportLayoutHeight = viewportHeight;
            _chatViewportLayoutFontSize = fontSize;
            _chatViewportLayoutPixelsPerUnit = pixelsPerUnit;
        }

        private static string GetChatMessageDisplay(
            string message,
            int caretPosition,
            out int displayCaretPosition)
        {
            var caret = Mathf.Clamp(caretPosition, 0, message.Length);
            var caretLine = FindChatViewportLine(caret);
            if (_chatVerticalNavigationActive &&
                string.Equals(_chatVerticalNavigationMessage, message, StringComparison.Ordinal) &&
                _chatVerticalNavigationCaret == caret)
            {
                caretLine = Mathf.Clamp(
                    _chatVerticalNavigationLine,
                    0,
                    _chatViewportLineStarts.Length - 1);
            }
            if (!_chatViewportStateInitialized)
            {
                _chatViewportFirstLine = Mathf.Max(
                    0,
                    caretLine - _chatViewportVisibleLineCount + 1);
                _chatViewportStateInitialized = true;
            }

            var firstLine = Mathf.Clamp(
                _chatViewportFirstLine,
                0,
                _chatViewportLineStarts.Length - 1);
            if (caret == message.Length)
            {
                firstLine = Mathf.Max(
                    0,
                    caretLine - _chatViewportVisibleLineCount + 1);
            }
            else if (caretLine < firstLine)
            {
                firstLine = caretLine;
            }
            else if (caretLine >= firstLine + _chatViewportVisibleLineCount)
            {
                firstLine = caretLine - _chatViewportVisibleLineCount + 1;
            }

            _chatViewportFirstLine = firstLine;
            var lastLine = Mathf.Min(
                _chatViewportLineStarts.Length - 1,
                firstLine + _chatViewportVisibleLineCount - 1);
            var start = _chatViewportLineStarts[firstLine];
            var end = _chatViewportLineEnds[lastLine];
            while (end > start &&
                   (message[end - 1] == '\r' || message[end - 1] == '\n'))
            {
                end--;
            }
            displayCaretPosition = caret - start;
            if (displayCaretPosition > end - start)
            {
                displayCaretPosition = end - start;
            }
            return message.Substring(start, end - start);
        }

        private static int FindChatViewportLine(int caret)
        {
            for (var index = _chatViewportLineStarts.Length - 1; index >= 0; index--)
            {
                if (caret >= _chatViewportLineStarts[index])
                {
                    return index;
                }
            }

            return 0;
        }

        private static bool KeyboardInputChatBoundaryPrefix()
        {
            _chatImeSuppressNativeLetters = false;
            _chatImeCompositionActiveThisFrame = false;
            _chatVerticalKeyConsumedThisFrame = false;
            if (KeyboardInput.open && !Connect.IsOffline)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    BeginChatVerticalTrace(KeyCode.UpArrow);
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    BeginChatVerticalTrace(KeyCode.DownArrow);
                }
            }
            if (!KeyboardInput.open ||
                Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete) ||
                Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                ResetChatVerticalNavigation();
            }
            if (!KeyboardInput.open)
            {
                DisableChatIme();
            }
            else
            {
                EnableChatIme();
            }

            var escapeDown = Input.GetKeyDown(KeyCode.Escape);
            if (KeyboardInput.open)
            {
                var composition = Input.compositionString;
                var inputString = Input.inputString;
                var compositionActive = !string.IsNullOrEmpty(composition);
                var committedInput = string.Empty;
                var digitKeyInput = string.Empty;
                var digitKeyDownMask = 0;
                ObserveChatImeComposition(compositionActive, composition, inputString);
                if (!escapeDown)
                {
                    committedInput = GetChatImeCommittedInput(inputString, compositionActive);
                    digitKeyInput = GetChatImeDigitKeyInput(out digitKeyDownMask);
                    ObserveChatImeInput(
                        composition,
                        inputString,
                        compositionActive,
                        committedInput,
                        digitKeyInput,
                        digitKeyDownMask);
                    if (committedInput != string.Empty)
                    {
                        InsertChatImeInput(committedInput, inputString);
                    }

                    if (digitKeyInput != string.Empty &&
                        !ContainsChatImeCharacter(committedInput, digitKeyInput[0]))
                    {
                        InsertChatImeInput(digitKeyInput, digitKeyInput);
                    }
                }

                _chatImeCompositionActiveThisFrame = compositionActive;
                if (compositionActive && !escapeDown)
                {
                    return false;
                }

                if (digitKeyInput != string.Empty)
                {
                    return false;
                }

                if (committedInput != string.Empty &&
                    (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
                {
                    return false;
                }

                if (!Connect.IsOffline && Input.GetKeyDown(KeyCode.UpArrow))
                {
                    if (MoveChatCaretVertical(-1))
                    {
                        _chatVerticalKeyConsumedThisFrame = true;
                        return false;
                    }
                }

                if (!Connect.IsOffline && Input.GetKeyDown(KeyCode.DownArrow))
                {
                    if (MoveChatCaretVertical(1))
                    {
                        _chatVerticalKeyConsumedThisFrame = true;
                        return false;
                    }
                }
            }

            if (!Input.GetKeyDown(KeyCode.Return) || Connect.IsOffline ||
                _keyboardSkipNextFrameField == null ||
                (bool)_keyboardSkipNextFrameField.GetValue(null))
            {
                return true;
            }

            if (PauseController.pauseStatus != PauseStatus.UnPaused)
            {
                return true;
            }

            var campaignMenu = SingletonMono<NewCustomCampaignMenu>.Instance;
            if (campaignMenu != null &&
                campaignMenu.highlightState != NewCustomCampaignMenu.HighlightState.Chat)
            {
                return true;
            }

            KeyboardInput.Toggle();
            return false;
        }

        private static bool PlayerChatInputBlockPrefix(
            Player __instance,
            ref bool up,
            ref bool down,
            ref bool left,
            ref bool right,
            ref bool fire,
            ref bool buttonJump,
            ref bool special,
            ref bool highFive,
            ref bool buttonGesture,
            ref bool sprint)
        {
            if (!KeyboardInput.open || __instance == null || !__instance.IsMine ||
                __instance.playerNum != NewChatController.GetFirstKeyboardPlayer())
            {
                return true;
            }

            up = false;
            down = false;
            left = false;
            right = false;
            fire = false;
            buttonJump = false;
            special = false;
            highFive = false;
            buttonGesture = false;
            sprint = false;
            return false;
        }

        private static void KeyboardInputChatRepeatPostfix()
        {
            var compositionActive = _chatImeCompositionActiveThisFrame;
            _chatImeCompositionActiveThisFrame = false;
            if (Connect.IsOffline || !KeyboardInput.open ||
                PauseController.pauseStatus != PauseStatus.UnPaused)
            {
                _chatImeCompositionWasActive = false;
                DisableChatIme();
                _chatImeSuppressNativeLetters = false;
                ClearChatInputRepeatState();
                return;
            }

            EnableChatIme();
            _chatImeCompositionWasActive = compositionActive;
            if (compositionActive)
            {
                _chatImeSuppressNativeLetters = false;
                ClearChatInputRepeatState();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                StartChatInputRepeat(KeyCode.Backspace);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Delete))
            {
                StartChatInputRepeat(KeyCode.Delete);
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                StartChatInputRepeat(KeyCode.LeftArrow);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                StartChatInputRepeat(KeyCode.RightArrow);
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (_chatVerticalKeyConsumedThisFrame)
                {
                    StartChatInputRepeat(KeyCode.UpArrow);
                }
                _chatVerticalKeyConsumedThisFrame = false;
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (_chatVerticalKeyConsumedThisFrame)
                {
                    StartChatInputRepeat(KeyCode.DownArrow);
                }
                _chatVerticalKeyConsumedThisFrame = false;
                return;
            }

            if (!_chatInputRepeatActive)
            {
                return;
            }

            if (!Input.GetKey(_chatInputRepeatKey))
            {
                ClearChatInputRepeatState();
                return;
            }

            if (Time.unscaledTime < _chatInputRepeatAt)
            {
                return;
            }

            if (_chatInputRepeatKey == KeyCode.Backspace)
            {
                ResetChatVerticalNavigation();
                SingletonMono<MessageController>.Instance.BackSpace();
            }
            else if (_chatInputRepeatKey == KeyCode.Delete)
            {
                ResetChatVerticalNavigation();
                SingletonMono<MessageController>.Instance.Delete();
            }
            else if (_chatInputRepeatKey == KeyCode.LeftArrow)
            {
                ResetChatVerticalNavigation();
                SingletonMono<MessageController>.Instance.MoveCursorLeft();
            }
            else if (_chatInputRepeatKey == KeyCode.RightArrow)
            {
                ResetChatVerticalNavigation();
                SingletonMono<MessageController>.Instance.MoveCursorRight();
            }
            else if (_chatInputRepeatKey == KeyCode.UpArrow)
            {
                MoveChatCaretVertical(-1);
            }
            else
            {
                MoveChatCaretVertical(1);
            }
            _chatInputRepeatAt = Time.unscaledTime + ChatInputRepeatIntervalSeconds;
        }

        private static bool MessageControllerChatImeInsertLetterPrefix()
        {
            RecordChatVerticalTraceStage("MessageController.InsertLetter");
            ResetChatVerticalNavigation();
            return !_chatImeSuppressNativeLetters || _chatImeAllowInsert;
        }

        private static void EnableChatIme()
        {
            if (_chatImeModeEnabled)
            {
                return;
            }

            Input.imeCompositionMode = IMECompositionMode.On;
            _chatImeModeEnabled = true;
            DiagnosticLog.Info("CHAT_IME mode=On; chatOpen=True.");
        }

        private static void DisableChatIme()
        {
            if (_chatImeModeEnabled)
            {
                Input.imeCompositionMode = IMECompositionMode.Off;
                DiagnosticLog.Info("CHAT_IME mode=Off; chatOpen=False.");
            }

            _chatImeModeEnabled = false;
            _chatImeCompositionActiveThisFrame = false;
            _chatImeCompositionWasActive = false;
            _chatImeLastCompositionActive = false;
            _chatImeCompositionStateKnown = false;
            _chatImeSuppressNativeLetters = false;
            _chatImeAllowInsert = false;
            _chatVerticalKeyConsumedThisFrame = false;
            ResetChatVerticalNavigation();
        }

        private static void ObserveChatImeComposition(
            bool compositionActive,
            string composition,
            string inputString)
        {
            if (_chatImeCompositionStateKnown &&
                _chatImeLastCompositionActive == compositionActive)
            {
                return;
            }

            _chatImeCompositionStateKnown = true;
            _chatImeLastCompositionActive = compositionActive;
            _chatImeCompositionActiveThisFrame = compositionActive;
            DiagnosticLog.Info(
                "CHAT_IME composition=" + (compositionActive ? "active" : "inactive") +
                "; compositionChars=" + (composition == null ? 0 : composition.Length) +
                "; inputChars=" + (inputString == null ? 0 : inputString.Length) +
                "; nonAsciiInput=" + (ContainsNonAscii(inputString) ? "True" : "False") + ".");
        }

        private static string GetChatImeCommittedInput(string inputString, bool compositionActive)
        {
            if (string.IsNullOrEmpty(inputString) ||
                (compositionActive && !ContainsNonAscii(inputString) &&
                 !ContainsChatImeSymbol(inputString) &&
                 !ContainsChatImeDigit(inputString)) ||
                 (!compositionActive && !_chatImeCompositionWasActive &&
                 !ContainsNonAscii(inputString) && !ContainsChatImeSymbol(inputString) &&
                 !ContainsChatImeDigit(inputString)))
            {
                return string.Empty;
            }

            var committed = string.Empty;
            for (var index = 0; index < inputString.Length; index++)
            {
                var character = inputString[index];
                if (char.IsControl(character) ||
                    (compositionActive && character <= 127 &&
                     !IsChatImeSymbol(character) && !IsChatImeDigit(character)))
                {
                    continue;
                }

                committed += character;
            }

            return committed;
        }

        private static void ObserveChatImeInput(
            string composition,
            string inputString,
            bool compositionActive,
            string committedInput,
            string digitKeyInput,
            int digitKeyDownMask)
        {
            if (string.IsNullOrEmpty(inputString) && digitKeyDownMask == 0)
            {
                return;
            }

            var inputStringDigitCount = CountChatImeDigits(inputString);
            var source = string.Empty;
            if (inputStringDigitCount > 0)
            {
                source = "Input.inputString";
            }

            if (digitKeyInput != string.Empty)
            {
                source += (source == string.Empty ? string.Empty : "+") +
                          "Input.GetKeyDown";
            }

            var skipNextFrame = "unknown";
            if (_keyboardSkipNextFrameField != null)
            {
                skipNextFrame = ((bool)_keyboardSkipNextFrameField.GetValue(null)).ToString();
            }

            DiagnosticLog.Info(
                "CHAT_IME input source=" + (source == string.Empty ? "filtered-or-other" : source) +
                "; inputStringChars=" + (inputString == null ? 0 : inputString.Length) +
                "; inputStringDigits=" + inputStringDigitCount +
                "; inputStringNonAscii=" + (ContainsNonAscii(inputString) ? "True" : "False") +
                "; compositionChars=" + (composition == null ? 0 : composition.Length) +
                "; composition=" + (compositionActive ? "active" : "inactive") +
                "; chatOpen=" + KeyboardInput.open +
                "; imeMode=" + Input.imeCompositionMode +
                "; skipNextFrame=" + skipNextFrame +
                "; committedChars=" + (committedInput == null ? 0 : committedInput.Length) +
                "; digitKeyInput=" + (digitKeyInput == string.Empty ? "none" : digitKeyInput) +
                "; digitKeyDown=" + FormatChatImeDigitKeyDownState(digitKeyDownMask) + ".");
        }

        private static bool ContainsChatImeSymbol(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                if (IsChatImeSymbol(value[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsChatImeDigit(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                if (IsChatImeDigit(value[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsChatImeCharacter(string value, char character)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(character) >= 0;
        }

        private static int CountChatImeDigits(string value)
        {
            var count = 0;
            if (string.IsNullOrEmpty(value))
            {
                return count;
            }

            for (var index = 0; index < value.Length; index++)
            {
                if (IsChatImeDigit(value[index]))
                {
                    count++;
                }
            }

            return count;
        }

        private static string GetChatImeDigitKeyInput(out int digitKeyDownMask)
        {
            digitKeyDownMask = 0;
            if (Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift))
            {
                return string.Empty;
            }

            var digitKeyInput = string.Empty;
            for (var digit = 0; digit <= 9; digit++)
            {
                var key = (KeyCode)((int)KeyCode.Alpha0 + digit);
                if (Input.GetKeyDown(key))
                {
                    digitKeyDownMask |= 1 << digit;
                    if (digitKeyInput == string.Empty)
                    {
                        digitKeyInput = ((char)('0' + digit)).ToString();
                    }
                }
            }

            for (var digit = 0; digit <= 9; digit++)
            {
                var key = (KeyCode)((int)KeyCode.Keypad0 + digit);
                if (Input.GetKeyDown(key))
                {
                    digitKeyDownMask |= 1 << (digit + 10);
                    if (digitKeyInput == string.Empty)
                    {
                        digitKeyInput = ((char)('0' + digit)).ToString();
                    }
                }
            }

            return digitKeyInput;
        }

        private static string FormatChatImeDigitKeyDownState(int digitKeyDownMask)
        {
            var state = string.Empty;
            for (var digit = 0; digit <= 9; digit++)
            {
                state += (state == string.Empty ? string.Empty : ",") +
                         "Alpha" + digit + "=" +
                         (((digitKeyDownMask & (1 << digit)) != 0) ? "1" : "0");
            }

            for (var digit = 0; digit <= 9; digit++)
            {
                state += ",Keypad" + digit + "=" +
                         (((digitKeyDownMask & (1 << (digit + 10))) != 0) ? "1" : "0");
            }

            return state;
        }

        private static bool IsChatImeDigit(char character)
        {
            return character >= '0' && character <= '9';
        }

        private static bool IsChatImeSymbol(char character)
        {
            return char.IsPunctuation(character) || char.IsSymbol(character);
        }

        private static bool ContainsNonAscii(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                if (value[index] > 127)
                {
                    return true;
                }
            }

            return false;
        }

        private static void InsertChatImeInput(string input, string rawInput)
        {
            _chatImeSuppressNativeLetters = true;
            _chatImeAllowInsert = true;
            try
            {
                SingletonMono<MessageController>.Instance.InsertLetter(input);
            }
            finally
            {
                _chatImeAllowInsert = false;
            }

            DiagnosticLog.Info(
                "CHAT_IME commit chars=" + input.Length +
                "; nonAsciiChars=" + CountNonAscii(input) +
                "; rawChars=" + (rawInput == null ? 0 : rawInput.Length) +
                "; messageChars=" + MessageController.message.Length +
                "; caretPos=" + MessageController.caretPos + ".");
        }

        private static int CountNonAscii(string value)
        {
            var count = 0;
            if (string.IsNullOrEmpty(value))
            {
                return count;
            }

            for (var index = 0; index < value.Length; index++)
            {
                if (value[index] > 127)
                {
                    count++;
                }
            }

            return count;
        }

        private static void StartChatInputRepeat(KeyCode key)
        {
            _chatInputRepeatActive = true;
            _chatInputRepeatKey = key;
            _chatInputRepeatAt = Time.unscaledTime + ChatInputRepeatDelaySeconds;
        }

        private static void MessageControllerChatInputRepeatCleanupPostfix()
        {
            ClearChatInputRepeatState();
            ResetChatVerticalNavigation();
        }

        private static void ClearChatInputRepeatState()
        {
            _chatInputRepeatActive = false;
            _chatInputRepeatKey = KeyCode.None;
            _chatInputRepeatAt = 0f;
        }

        private static void PauseControllerChatBoundaryPostfix()
        {
            ClearChatInputRepeatState();
            if (PauseController.pauseStatus != PauseStatus.UnPaused)
            {
                return;
            }

            var pauseMenu = PauseMenu.instance;
            if (pauseMenu != null && pauseMenu.MenuActive)
            {
                pauseMenu.MenuActive = false;
                DiagnosticLog.Trace(
                    "Pause menu active flag synchronized after Escape returned to UnPaused.");
            }
        }
    }
}
