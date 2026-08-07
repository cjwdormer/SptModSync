using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TCFModSync.Client.UI
{
    internal static class GameInputBlocker
    {
        private static object[]? _disabled;
        private static PropertyInfo? _enabledProperty;
        private static bool _searched;
        private static Type? _eventSystemType;

        public static void Block(Action<string> log)
        {
            if (_disabled != null) return;

            var type = ResolveEventSystemType();
            if (type == null)
            {
                log("[TCF-ModSync] Could not find Unity's EventSystem - the sync window will not block " +
                    "clicks on the game menu. Please finish the sync before using your profile.");
                return;
            }

            try
            {
                var found = UnityEngine.Object.FindObjectsOfType(type);
                if (found == null || found.Length == 0) return;

                _enabledProperty ??= typeof(Behaviour).GetProperty("enabled");
                if (_enabledProperty == null) return;

                foreach (var system in found)
                {
                    _enabledProperty.SetValue(system, false, null);
                }

                _disabled = found.Cast<object>().ToArray();
                log($"[TCF-ModSync] Game menu input suspended while the sync window is open ({found.Length} event system(s)).");
            }
            catch (Exception ex)
            {
                log($"[TCF-ModSync] Could not suspend game menu input: {ex.Message}");
            }
        }

        public static void Unblock(Action<string> log)
        {
            if (_disabled == null || _enabledProperty == null) return;

            foreach (var system in _disabled)
            {
                try
                {
                    if (system is UnityEngine.Object unityObject && unityObject == null) continue;
                    _enabledProperty.SetValue(system, true, null);
                }
                catch
                {
                }
            }

            _disabled = null;
            log("[TCF-ModSync] Game menu input restored.");
        }

        private static Type? ResolveEventSystemType()
        {
            if (_searched) return _eventSystemType;
            _searched = true;

            _eventSystemType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly =>
                {
                    try { return assembly.GetType("UnityEngine.EventSystems.EventSystem"); }
                    catch { return null; }
                })
                .FirstOrDefault(t => t != null);

            return _eventSystemType;
        }
    }
}
