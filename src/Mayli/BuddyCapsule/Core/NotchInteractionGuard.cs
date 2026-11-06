using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace WindowsBuddy.BuddyCapsule.Core
{
    /// <summary>
    /// Keeps the notch open while a context menu / dropdown popup or a modal dialog opened from inside the
    /// notch is visible. Those surfaces live in their own HWND, so the pointer "leaves" the capsule while
    /// the user is still interacting with the notch and the state machine would otherwise close it.
    /// </summary>
    public static class NotchInteractionGuard
    {
        private static readonly List<ContextMenu> _openMenus = new();
        private static int _modalDepth;
        private static bool _registered;

        /// <summary>Raised when the last guarding surface closes.</summary>
        public static event Action Released;

        public static bool IsHeld
        {
            get
            {
                for (int i = _openMenus.Count - 1; i >= 0; i--)
                {
                    if (!_openMenus[i].IsOpen) _openMenus.RemoveAt(i);
                }
                return _openMenus.Count > 0 || _modalDepth > 0;
            }
        }

        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent,
                new RoutedEventHandler((s, _) =>
                {
                    if (s is ContextMenu cm && !_openMenus.Contains(cm)) _openMenus.Add(cm);
                }));

            EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.ClosedEvent,
                new RoutedEventHandler((s, _) =>
                {
                    if (s is ContextMenu cm) _openMenus.Remove(cm);
                    NotifyIfReleased();
                }));
        }

        /// <summary>Wraps a modal dialog call so the notch stays open while it is displayed.</summary>
        public static T RunModal<T>(Func<T> showDialog)
        {
            _modalDepth++;
            try
            {
                return showDialog();
            }
            finally
            {
                _modalDepth = Math.Max(0, _modalDepth - 1);
                NotifyIfReleased();
            }
        }

        private static void NotifyIfReleased()
        {
            if (!IsHeld) Released?.Invoke();
        }
    }
}
