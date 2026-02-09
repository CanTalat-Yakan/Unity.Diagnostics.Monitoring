using System;
using System.Reflection;
using UnityEngine;

namespace UnityEssentials
{
    internal static class RuntimeMonitor
    {
        private static readonly BindingFlags s_bindingFlags = RuntimeDiscovery.AllMembers;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterSceneLoad()
        {
            try
            {
                var monoBehaviours = RuntimeDiscovery.AllMonoBehavioursCached;
                for (var i = 0; i < monoBehaviours.Length; i++)
                {
                    var mb = monoBehaviours[i];
                    if (mb == null) continue;
                    if (!HasAnyMonitorMembers(mb)) continue;
                    RuntimeMonitorImGui.Register(mb);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[RuntimeMonitor] Auto registration failed: {e}");
            }
        }

        private static bool HasAnyMonitorMembers(MonoBehaviour obj)
        {
            var type = obj.GetType();

            return RuntimeDiscovery.AnyMemberInHierarchy(type, member =>
            {
                if (RuntimeDiscovery.IsCompilerGenerated(member))
                    return false;

                // Fields
                if (member is FieldInfo field)
                    return RuntimeDiscovery.HasAttribute(field, typeof(MonitorAttribute), inherit: true);

                // Properties
                if (member is PropertyInfo prop)
                {
                    if (prop.GetIndexParameters().Length != 0) return false;
                    return RuntimeDiscovery.HasAttribute(prop, typeof(MonitorAttribute), inherit: true);
                }

                // Methods
                if (member is MethodInfo method)
                {
                    if (method.IsGenericMethod) return false;
                    if (method.GetParameters().Length != 0) return false;
                    if (method.ReturnType == typeof(void)) return false;
                    return RuntimeDiscovery.HasAttribute(method, typeof(MonitorAttribute), inherit: true);
                }

                return false;

            }, s_bindingFlags);
        }
    }
}