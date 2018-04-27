﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Consolation
{
    /// <summary>
    /// A console to display Unity's debug logs in-game.
    /// </summary>
    class Console : MonoBehaviour
    {
        public static Version version = new Version(1, 0, 0);

        #region Inspector Settings

        /// <summary>
        /// The hotkey to show and hide the console window.
        /// </summary>
        public KeyCode toggleKey = KeyCode.BackQuote;

        /// <summary>
        /// Whether to open as soon as the game starts.
        /// </summary>
        public bool openOnStart = false;

        /// <summary>
        /// Whether to open the window by shaking the device (mobile-only).
        /// </summary>
        public bool shakeToOpen = true;

        /// <summary>
        /// The (squared) acceleration above which the window should open.
        /// </summary>
        public float shakeAcceleration = 3f;

        /// <summary>
        /// Whether to only keep a certain number of logs, useful if memory usage is a concern.
        /// </summary>
        public bool restrictLogCount = false;

        /// <summary>
        /// Number of logs to keep before removing old ones.
        /// </summary>
        public int maxLogCount = 1000;

        #endregion

        static readonly GUIContent clearLabel = new GUIContent("Clear", "Clear the contents of the console.");
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
        readonly List<Log> logs = new List<Log>();
        Vector2 scrollPosition;
        readonly Rect titleBarRect = new Rect(0, 0, 10000, 20);
        Rect windowRect = new Rect(margin, margin, Screen.width - (margin * 2), Screen.height - (margin * 2));

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
            Application.logMessageReceivedThreaded -= HandleLog;
        }

        void OnEnable()
        {
            Application.logMessageReceivedThreaded += HandleLog;
        }

        void OnGUI()
        {
            if (!isVisible)
            {
                return;
            }

            windowRect = GUILayout.Window(123456, windowRect, DrawWindow, windowTitle);
        }

        void Start()
        {
            if (openOnStart)
            {
                isVisible = true;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                isVisible = !isVisible;
            }

            if (shakeToOpen && Input.acceleration.sqrMagnitude > shakeAcceleration)
            {
                isVisible = true;
            }
        }

        #endregion

        void DrawCollapsedLog(Log log)
        {
            GUILayout.BeginHorizontal();

                GUILayout.Label(log.message);
                GUILayout.FlexibleSpace();
                GUILayout.Label(log.count.ToString(), GUI.skin.box);

            GUILayout.EndHorizontal();
        }

        void DrawExpandedLog(Log log)
        {
            for (var i = 0; i < log.count; i += 1)
            {
                GUILayout.Label(log.message);
            }
        }

        void DrawLog(Log log)
        {
            GUI.contentColor = logTypeColors[log.type];

            if (isCollapsed)
            {
                DrawCollapsedLog(log);
            }
            else
            {
                DrawExpandedLog(log);
            }
        }

        void DrawLogList()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            // Used to determine height of accumulated log labels.
            GUILayout.BeginVertical();

                var visibleLogs = logs.Where(IsLogVisible);

                foreach (Log log in visibleLogs)
                {
                    DrawLog(log);
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

            // Ensure GUI colour is reset before drawing other components.
            GUI.contentColor = Color.white;
        }

        void DrawToolbar()
        {
            GUILayout.BeginHorizontal();

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

            GUILayout.EndHorizontal();
        }

        void DrawWindow(int windowID)
        {
            DrawLogList();
            DrawToolbar();

            // Allow the window to be dragged by its title bar.
            GUI.DragWindow(titleBarRect);
        }

        Log? GetLastLog()
        {
            if (logs.Count == 0)
            {
                return null;
            }

            return logs.Last();
        }

        void HandleLog(string message, string stackTrace, LogType type)
        {
            var log = new Log {
                count = 1,
                message = message,
                stackTrace = stackTrace,
                type = type,
            };

            var lastLog = GetLastLog();
            var isDuplicateOfLastLog = lastLog.HasValue && log.Equals(lastLog.Value);

            if (isDuplicateOfLastLog)
            {
                // Replace previous log with incremented count instead of adding a new one.
                log.count = lastLog.Value.count + 1;
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
            return logTypeFilters[log.type];
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
    }

    /// <summary>
    /// A basic container for log details.
    /// </summary>
    struct Log
    {
        public int count;
        public string message;
        public string stackTrace;
        public LogType type;

        public bool Equals(Log log)
        {
            return message == log.message && stackTrace == log.stackTrace && type == log.type;
        }
    }
}
