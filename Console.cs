using System;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
    using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
    using UnityEditor;
#endif

namespace Consolation
{
    /// <summary>
    /// A console to display Unity's debug logs in-game.
    ///
    /// Version: 1.3.1
    /// </summary>
    public class Console : MonoBehaviour
    {
        #region Inspector Settings

        [SerializeField, Tooltip("Hotkey to show and hide the console.")]
        #if ENABLE_INPUT_SYSTEM
            Key toggleKey = Key.Backquote;
        #else
            KeyCode toggleKey = KeyCode.BackQuote;
        #endif

        [SerializeField, Tooltip("Whether to open as soon as the game starts.")]
        bool openOnStart;

        [SerializeField, Tooltip("Whether to open the window by shaking the device (mobile-only).")]
        bool shakeToOpen = true;

        [SerializeField, Tooltip("Whether to require touches while shaking to avoid accidental shakes.")]
        bool shakeRequiresTouch;

        [SerializeField, Tooltip("Acceleration (squared) above which to open the console.")]
        float shakeAcceleration = 3f;

        [SerializeField, Tooltip("Number of seconds that need to pass between visibility toggles. This threshold prevents closing again while shaking to open.")]
        float toggleThresholdSeconds = .5f;

        [SerializeField, Tooltip("Whether to keep a limited number of logs. Useful if memory usage is a concern.")]
        bool restrictLogCount;

        [SerializeField, Tooltip("Number of logs to keep before removing old ones.")]
        int maxLogCount = 1000;

        [SerializeField, Tooltip("Whether log messages are collapsed by default or not.")]
        bool collapseLogOnStart;

        [SerializeField, Tooltip("Font size to display log entries with.")]
        int logFontSize = 12;

        [SerializeField, Tooltip("Amount to scale UI by.")]
        float scaleFactor = 1f;

        #endregion

        static readonly GUIContent clearLabel = new GUIContent("Clear", "Clear contents of console.");
        static readonly GUIContent onlyLastLogLabel = new GUIContent("Only Last Log", "Show only most recent log.");
        static readonly GUIContent collapseLabel = new GUIContent("Collapse", "Hide repeated messages.");
        const int margin = 20;
        const string windowTitle = "Console";

        static readonly Dictionary<LogType, Color> logTypeColors = new Dictionary<LogType, Color>
        {
            { LogType.Assert, Color.white },
            { LogType.Error, Color.red },
            { LogType.Exception, Color.red },
            { LogType.Log, Color.white },
            { LogType.Warning, Color.yellow },
        };

        bool isCollapsed;
        bool isVisible;
        float lastToggleTime;
        readonly List<Log> logs = new List<Log>();
        bool onlyLastLog;
        readonly ConcurrentQueue<Log> queuedLogs = new ConcurrentQueue<Log>();
        Vector2 scrollPosition;
        readonly Rect titleBarRect = new Rect(0, 0, 10000, 20);
        float windowX = margin;
        float windowY = margin;

        readonly Dictionary<LogType, bool> logTypeFilters = new Dictionary<LogType, bool>
        {
            { LogType.Assert, true },
            { LogType.Error, true },
            { LogType.Exception, true },
            { LogType.Log, true },
            { LogType.Warning, true },
        };

        #region MonoBehaviour Messages

        void OnDisable()
        {
            Application.logMessageReceivedThreaded -= HandleLogThreaded;
        }

        void OnEnable()
        {
            Application.logMessageReceivedThreaded += HandleLogThreaded;
        }

        void OnGUI()
        {
            if (!isVisible)
            {
                return;
            }

            GUI.matrix = Matrix4x4.Scale(Vector3.one * scaleFactor);

            var width = (Screen.width / scaleFactor) - (margin * 2);
            var height = (Screen.height / scaleFactor) - (margin * 2);
            var windowRect = new Rect(windowX, windowY, width, height);

            var newWindowRect = GUILayout.Window(123456, windowRect, DrawWindow, windowTitle);
            windowX = newWindowRect.x;
            windowY = newWindowRect.y;
        }

        void Start()
        {
            if (collapseLogOnStart)
            {
                isCollapsed = true;
            }

            if (openOnStart)
            {
                isVisible = true;
            }

            if (shakeRequiresTouch)
            {
                EnableMultiTouch();
            }
        }

        void Update()
        {
            UpdateQueuedLogs();

            if (WasToggleKeyPressed())
            {
                isVisible = !isVisible;
            }

            if (shakeToOpen &&
                Time.realtimeSinceStartup - lastToggleTime >= toggleThresholdSeconds &&
                WasShaken() &&
                (!shakeRequiresTouch || WasMultiTouchThresholdExceeded()))
            {
                isVisible = !isVisible;
                lastToggleTime = Time.realtimeSinceStartup;
            }
        }

        #endregion

        void DrawLog(Log log, GUIStyle logStyle)
        {
            GUI.contentColor = logTypeColors[log.Type];

            if (isCollapsed)
            {
                // Draw collapsed log with badge indicating count.
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(log.GetTruncatedMessage(), logStyle);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(log.Count.ToString(), GUI.skin.box);
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                // Draw expanded log.
                for (var i = 0; i < log.Count; i += 1)
                {
                    GUILayout.Label(log.GetTruncatedMessage(), logStyle);
                }
            }

            GUI.contentColor = Color.white;
        }

        void DrawLogList()
        {
            var logStyle = GUI.skin.label;
            logStyle.fontSize = logFontSize;

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            // Used to determine height of accumulated log labels.
            GUILayout.BeginVertical();
            {
                if (onlyLastLog)
                {
                    var lastVisibleLog = GetLastVisibleLog();

                    if (lastVisibleLog.HasValue)
                    {
                        DrawLog(lastVisibleLog.Value, logStyle);
                    }
                }
                else
                {
                    foreach (var log in logs)
                    {
                        if (!IsLogVisible(log))
                        {
                            continue;
                        }

                        DrawLog(log, logStyle);
                    }
                }
            }
            GUILayout.EndVertical();

            var innerScrollRect = GUILayoutUtility.GetLastRect();
            GUILayout.EndScrollView();
            var outerScrollRect = GUILayoutUtility.GetLastRect();

            // If we're scrolled to bottom now, guarantee that it continues to be in next cycle.
            if (Event.current.type == EventType.Repaint && IsScrolledToBottom(innerScrollRect, outerScrollRect))
            {
                ScrollToBottom();
            }
        }

        void DrawToolbar()
        {
            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button(clearLabel))
                {
                    logs.Clear();
                }

                foreach (LogType logType in Enum.GetValues(typeof(LogType)))
                {
                    var currentState = logTypeFilters[logType];
                    var label = logType.ToString();
                    logTypeFilters[logType] = GUILayout.Toggle(currentState, label, GUILayout.ExpandWidth(false));
                    GUILayout.Space(20);
                }

                isCollapsed = GUILayout.Toggle(isCollapsed, collapseLabel, GUILayout.ExpandWidth(false));
                onlyLastLog = GUILayout.Toggle(onlyLastLog, onlyLastLogLabel, GUILayout.ExpandWidth(false));
            }
            GUILayout.EndHorizontal();
        }

        void DrawWindow(int windowID)
        {
            DrawLogList();
            DrawToolbar();

            // Allow the window to be dragged by its title bar.
            GUI.DragWindow(titleBarRect);
        }

        void UpdateQueuedLogs()
        {
            while (queuedLogs.TryDequeue(out var log))
            {
                ProcessLogItem(log);
            }
        }

        Log? GetLastVisibleLog()
        {
            for (var i = logs.Count - 1; i >= 0; i--)
            {
                var log = logs[i];

                if (IsLogVisible(log))
                {
                    return log;
                }
            }

            return null;
        }

        void HandleLogThreaded(string message, string stackTrace, LogType type)
        {
            var log = new Log
            {
                Count = 1,
                Message = message,
                StackTrace = stackTrace,
                Type = type,
            };

            // Queue the log into a ConcurrentQueue to be processed later in the Unity main thread,
            // so that we don't get GUI-related errors for logs coming from other threads
            queuedLogs.Enqueue(log);
        }

        void ProcessLogItem(Log log)
        {
            var lastLog = logs.Count > 0 ? logs[logs.Count - 1] : (Log?)null;
            var isDuplicateOfLastLog = lastLog.HasValue && log.Equals(lastLog.Value);

            if (isDuplicateOfLastLog)
            {
                // Replace previous log with incremented count instead of adding a new one.
                log.Count = lastLog.Value.Count + 1;
                logs[logs.Count - 1] = log;
            }
            else
            {
                logs.Add(log);
                TrimExcessLogs();
            }
        }

        bool IsLogVisible(Log log)
        {
            return logTypeFilters[log.Type];
        }

        bool IsScrolledToBottom(Rect innerScrollRect, Rect outerScrollRect)
        {
            var innerScrollHeight = innerScrollRect.height;

            // Take into account extra padding added to the scroll container.
            var outerScrollHeight = outerScrollRect.height - GUI.skin.box.padding.vertical;

            // If contents of scroll view haven't exceeded outer container, treat it as scrolled to bottom.
            if (outerScrollHeight > innerScrollHeight)
            {
                return true;
            }

            // Scrolled to bottom (with error margin for float math)
            return Mathf.Approximately(innerScrollHeight, scrollPosition.y + outerScrollHeight);
        }

        void ScrollToBottom()
        {
            scrollPosition = new Vector2(0, Int32.MaxValue);
        }

        void TrimExcessLogs()
        {
            if (!restrictLogCount)
            {
                return;
            }

            var amountToRemove = logs.Count - maxLogCount;

            if (amountToRemove <= 0)
            {
                return;
            }

            logs.RemoveRange(0, amountToRemove);
        }

        bool WasMultiTouchThresholdExceeded()
        {
            #if ENABLE_INPUT_SYSTEM
                var touchCount = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count;
            #else
                var touchCount = Input.touchCount;
            #endif

            return touchCount > 2;
        }

        bool WasShaken()
        {
            #if ENABLE_INPUT_SYSTEM
                var acceleration = Accelerometer.current?.acceleration.ReadValue() ?? Vector3.zero;
            #else
                var acceleration = Input.acceleration;
            #endif

            return acceleration.sqrMagnitude > shakeAcceleration;
        }

        bool WasToggleKeyPressed()
        {
            #if ENABLE_INPUT_SYSTEM
                return Keyboard.current[toggleKey].wasPressedThisFrame;
            #else
                return Input.GetKeyDown(toggleKey);
            #endif
        }

        static void EnableMultiTouch()
        {
            #if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
            #else
                Input.multiTouchEnabled = true;
            #endif
        }
    }

    /// <summary>
    /// A basic container for log details.
    /// </summary>
    struct Log
    {
        public int Count;
        public string Message;
        public string StackTrace;
        public LogType Type;

        /// <summary>
        /// The max string length supported by UnityEngine.GUILayout.Label without triggering this error:
        /// "String too long for TextMeshGenerator. Cutting off characters."
        /// </summary>
        const int maxMessageLength = 16382;

        public bool Equals(Log log)
        {
            return Message == log.Message && StackTrace == log.StackTrace && Type == log.Type;
        }

        /// <summary>
        /// Return a truncated Message if it exceeds the max Message length.
        /// </summary>
        public string GetTruncatedMessage()
        {
            if (string.IsNullOrEmpty(Message))
            {
                return Message;
            }

            return Message.Length <= maxMessageLength ? Message : Message.Substring(0, maxMessageLength);
        }
    }

    /// <summary>
    /// Alternative to System.Collections.Concurrent.ConcurrentQueue
    /// (It's only available in .NET 4.0 and greater)
    /// </summary>
    /// <remarks>
    /// It's a bit slow (as it uses locks), and only provides a small subset of the interface
    /// Overall, the implementation is intended to be simple & robust
    /// </remarks>
    class ConcurrentQueue<T>
    {
        readonly Queue<T> queue = new Queue<T>();
        readonly object queueLock = new object();

        public void Enqueue(T item)
        {
            lock (queueLock)
            {
                queue.Enqueue(item);
            }
        }

        public bool TryDequeue(out T result)
        {
            lock (queueLock)
            {
                if (queue.Count == 0)
                {
                    result = default(T);
                    return false;
                }

                result = queue.Dequeue();
                return true;
            }
        }
    }

#if UNITY_EDITOR

    [CustomEditor(typeof(Console))]
    class ConsoleEditor : Editor
    {
        SerializedProperty toggleKey;
        SerializedProperty openOnStart;
        SerializedProperty shakeToOpen;
        SerializedProperty shakeRequiresTouch;
        SerializedProperty shakeAcceleration;
        SerializedProperty toggleThresholdSeconds;
        SerializedProperty restrictLogCount;
        SerializedProperty maxLogCount;
        SerializedProperty collapseLogOnStart;
        SerializedProperty logFontSize;
        SerializedProperty scaleFactor;

        void OnEnable()
        {
            toggleKey = serializedObject.FindProperty("toggleKey");
            openOnStart = serializedObject.FindProperty("openOnStart");
            shakeToOpen = serializedObject.FindProperty("shakeToOpen");
            shakeRequiresTouch = serializedObject.FindProperty("shakeRequiresTouch");
            shakeAcceleration = serializedObject.FindProperty("shakeAcceleration");
            toggleThresholdSeconds = serializedObject.FindProperty("toggleThresholdSeconds");
            restrictLogCount = serializedObject.FindProperty("restrictLogCount");
            maxLogCount = serializedObject.FindProperty("maxLogCount");
            collapseLogOnStart = serializedObject.FindProperty("collapseLogOnStart");
            logFontSize = serializedObject.FindProperty("logFontSize");
            scaleFactor = serializedObject.FindProperty("scaleFactor");
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.PropertyField(toggleKey);
            EditorGUILayout.PropertyField(openOnStart);
            EditorGUILayout.PropertyField(shakeToOpen);

            using (new EditorGUI.DisabledScope(!shakeToOpen.boolValue))
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(shakeRequiresTouch);
                EditorGUILayout.PropertyField(shakeAcceleration);
            }

            EditorGUILayout.PropertyField(toggleThresholdSeconds);
            EditorGUILayout.PropertyField(restrictLogCount);

            using (new EditorGUI.DisabledScope(!restrictLogCount.boolValue))
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(maxLogCount);
            }

            EditorGUILayout.PropertyField(collapseLogOnStart);
            EditorGUILayout.PropertyField(logFontSize);
            EditorGUILayout.PropertyField(scaleFactor);
            serializedObject.ApplyModifiedProperties();
        }
    }

#endif
}
