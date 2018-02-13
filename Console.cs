using System;
using System.Collections.Generic;
using UnityEngine;

namespace Consolation
{
    /// <summary>
    /// A console to display Unity's debug logs in-game.
    /// </summary>
    class Console : MonoBehaviour
    {
        struct Log
        {
            public string message;
            public string stackTrace;
            public LogType type;
            public int duplicateCount;
        }

        #region Inspector Settings

        /// <summary>
        /// The hotkey to show and hide the console window.
        /// </summary>
        public KeyCode toggleKey = KeyCode.BackQuote;

        /// <summary>
        /// Whether to open the window by shaking the device (mobile-only).
        /// </summary>
        public bool shakeToOpen = true;

        /// <summary>
        /// The (squared) acceleration above which the window should open.
        /// </summary>
        public float shakeAcceleration = 3f;

        /// <summary>
        /// Whether to only keep a certain number of logs.
        ///
        /// Setting this can be helpful if memory usage is a concern.
        /// </summary>
        public bool restrictLogCount = false;

        /// <summary>
        /// Number of logs to keep before removing old ones.
        /// </summary>
        public int maxLogs = 1000;

        #endregion

        readonly List<Log> logs = new List<Log>();
        Vector2 scrollPosition;
        bool visible;
        bool collapse;

        Dictionary<LogType, bool> logTypeFilters = new Dictionary<LogType, bool>
        {
            { LogType.Assert, true },
            { LogType.Error, true },
            { LogType.Exception, true },
            { LogType.Log, true },
            { LogType.Warning, true },
        };

        // Visual elements:

        static readonly Dictionary<LogType, Color> logTypeColors = new Dictionary<LogType, Color>
        {
            { LogType.Assert, Color.white },
            { LogType.Error, Color.red },
            { LogType.Exception, Color.red },
            { LogType.Log, Color.white },
            { LogType.Warning, Color.yellow },
        };

        const string windowTitle = "Console";
        const int margin = 20;
        static readonly GUIContent clearLabel = new GUIContent("Clear", "Clear the contents of the console.");
        static readonly GUIContent collapseLabel = new GUIContent("Collapse", "Hide repeated messages.");

        readonly Rect titleBarRect = new Rect(0, 0, 10000, 20);
        Rect windowRect = new Rect(margin, margin, Screen.width - (margin * 2), Screen.height - (margin * 2));

        void OnEnable ()
        {
            Application.logMessageReceived += HandleLog;
        }

        void OnDisable ()
        {
            Application.logMessageReceived -= HandleLog;
        }

        void Update ()
        {
            if (Input.GetKeyDown(toggleKey)) {
                visible = !visible;
            }

            if (shakeToOpen && Input.acceleration.sqrMagnitude > shakeAcceleration) {
                visible = true;
            }
        }

        void OnGUI ()
        {
            if (visible) {
                windowRect = GUILayout.Window(123456, windowRect, DrawConsoleWindow, windowTitle);
            }
        }

        /// <summary>
        /// Displays a window that lists the recorded logs.
        /// </summary>
        /// <param name="windowID">Window ID.</param>
        void DrawConsoleWindow (int windowID)
        {
            DrawLogsList();
            DrawToolbar();

            // Allow the window to be dragged by its title bar.
            GUI.DragWindow(titleBarRect);
        }

        /// <summary>
        /// Displays a scrollable list of logs.
        /// </summary>
        void DrawLogsList ()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            // Used to determine height of accumulated log labels.
            GUILayout.BeginVertical();

                // Iterate through the recorded logs.
                for (var i = 0; i < logs.Count; i++) {
                    Log log = logs[i];

                    // Skip logs that are filtered out.
                    if (!logTypeFilters[log.type]) {
                        continue;
                    }

                    GUI.contentColor = logTypeColors[log.type];

                    // Collapse duplicates into a single log entry with a leading counter
                    if (collapse) {
                        GUILayout.Label(string.Format("({0}) {1}", log.duplicateCount, log.message));

                    // Duplicates get individual lines
                    } else {
                        for (int j = 0; j <= log.duplicateCount; j++) {
                            GUILayout.Label(log.message);
                        }
                    }
                }

            GUILayout.EndVertical();
            var innerScrollRect = GUILayoutUtility.GetLastRect();
            GUILayout.EndScrollView();
            var outerScrollRect = GUILayoutUtility.GetLastRect();

            // If we're scrolled to bottom now, guarantee that it continues to be in next cycle.
            if (Event.current.type == EventType.Repaint && IsScrolledToBottom(innerScrollRect, outerScrollRect)) {
                ScrollToBottom();
            }

            // Ensure GUI colour is reset before drawing other components.
            GUI.contentColor = Color.white;
        }

        /// <summary>
        /// Displays options for filtering and changing the logs list.
        /// </summary>
        void DrawToolbar ()
        {
            GUILayout.BeginHorizontal();

                if (GUILayout.Button(clearLabel)) {
                    logs.Clear();
                }

                foreach (LogType logType in Enum.GetValues(typeof(LogType))) {
                    var currentState = logTypeFilters[logType];
                    var label = logType.ToString();
                    logTypeFilters[logType] = GUILayout.Toggle(currentState, label, GUILayout.ExpandWidth(false));
                    GUILayout.Space(20);
                }

                collapse = GUILayout.Toggle(collapse, collapseLabel, GUILayout.ExpandWidth(false));

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Records a log from the log callback.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="stackTrace">Trace of where the message came from.</param>
        /// <param name="type">Type of message (error, exception, warning, assert).</param>
        void HandleLog (string message, string stackTrace, LogType type)
        {
            int lastIndex = logs.Count - 1;
            int duplicateCount = 0;

            // Log list not empty
            if (lastIndex > -1) {
                Log lastLog = logs[lastIndex];

                // Increment duplicateCount if log matches previous
                if (message == lastLog.message && type == lastLog.type && stackTrace == lastLog.stackTrace) {
                    duplicateCount = lastLog.duplicateCount + 1;
                }
            }

            Log newLog = new Log {
                message = message,
                stackTrace = stackTrace,
                type = type,
                duplicateCount = duplicateCount
            };

            // Log is a duplicate; update previous log
            if (duplicateCount > 0) {
                logs[lastIndex] = newLog;

            } else {
                logs.Add(newLog);
                
                // Remove oldest log if restriction is enabled and limit is met
                if (restrictLogCount && lastIndex >= maxLogs) {
                    logs.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Determines whether the scroll view is scrolled to the bottom.
        /// </summary>
        /// <param name="innerScrollRect">Rect surrounding scroll view content.</param>
        /// <param name="outerScrollRect">Scroll view container.</param>
        /// <returns>Whether scroll view is scrolled to bottom.</returns>
        bool IsScrolledToBottom (Rect innerScrollRect, Rect outerScrollRect)
        {
            var innerScrollHeight = innerScrollRect.height;

            // Take into account extra padding added to the scroll container.
            var outerScrollHeight = outerScrollRect.height - GUI.skin.box.padding.vertical;

            // If contents of scroll view haven't exceeded outer container, treat it as scrolled to bottom.
            if (outerScrollHeight > innerScrollHeight) {
                return true;
            }
            
            // Scrolled to bottom (with error margin for float math)
            return Mathf.Approximately(innerScrollHeight, scrollPosition.y + outerScrollHeight);
        }

        /// <summary>
        /// Moves the scroll view down so that the last log is visible.
        /// </summary>
        void ScrollToBottom ()
        {
            scrollPosition = new Vector2(0, Int32.MaxValue);
        }
    }
}
