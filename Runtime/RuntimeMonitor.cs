using System;
using System.Reflection;
using UnityEngine;

namespace UnityEssentials
{
    /// <summary>
    /// Zero-setup auto registrar for the ImGui Runtime Monitor.
    /// 
    /// This is intentionally NOT dependency injection.
    /// It only discovers MonoBehaviours that contain [Monitor] members and registers them for display.
    /// </summary>
    internal static class RuntimeMonitor
    {
        private static readonly BindingFlags SBindingFlags =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterSceneLoad()
        {
            try
            {
                var monoBehaviours = FindMonoBehaviours();
                for (var i = 0; i < monoBehaviours.Length; i++)
                {
                    var mb = monoBehaviours[i];
                    if (mb == null)
                        continue;

                    if (!HasAnyMonitorMembers(mb))
                        continue;

                    RuntimeMonitorImGui.Register(mb);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[RuntimeMonitor] Auto registration failed: {e}");
            }
        }

        private static MonoBehaviour[] FindMonoBehaviours()
        {
#if UNITY_2023_1_OR_NEWER
            // Include inactive as well. DI and monitoring both benefit from predictable discovery.
            return UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.InstanceID);
#else
            return UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(includeInactive: true);
#endif
        }

        private static bool HasAnyMonitorMembers(MonoBehaviour obj)
        {
            var type = obj.GetType();

            // Walk inheritance chain so private members in base types are found too.
            for (var t = type; t != null; t = t.BaseType)
            {
                // Fields
                var fields = t.GetFields(SBindingFlags);
                for (var i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    if (field.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), true))
                        continue;

                    if (Attribute.IsDefined(field, typeof(MonitorAttribute), inherit: true))
                        return true;
                }

                // Properties
                var props = t.GetProperties(SBindingFlags);
                for (var i = 0; i < props.Length; i++)
                {
                    var prop = props[i];
                    if (prop.GetIndexParameters().Length != 0)
                        continue;

                    if (Attribute.IsDefined(prop, typeof(MonitorAttribute), inherit: true))
                        return true;
                }

                // Methods
                var methods = t.GetMethods(SBindingFlags);
                for (var i = 0; i < methods.Length; i++)
                {
                    var method = methods[i];
                    if (method.IsGenericMethod)
                        continue;

                    if (method.GetParameters().Length != 0)
                        continue;

                    if (method.ReturnType == typeof(void))
                        continue;

                    if (Attribute.IsDefined(method, typeof(MonitorAttribute), inherit: true))
                        return true;
                }
            }

            return false;
        }
    }
}
