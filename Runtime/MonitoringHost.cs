using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnityEssentials
{
    public class MonitoringHost : GlobalSingleton<MonitoringHost>
    {
        /// <summary>
        /// Cached targets + members built by the host. The ImGui layer only reads these.
        /// </summary>
        public static List<MonitorTarget> Targets = new();

        private void Update() =>
            MonitoringImGui.DrawImGui();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void FetchAndCacheTargets()
        {
            Targets.Clear();
            var monoBehaviours = RuntimeDiscovery.AllMonoBehavioursCached;
            for (var i = 0; i < monoBehaviours.Length; i++)
            {
                var mb = monoBehaviours[i];
                if (mb == null) continue;

                var members = FetchMembersForType(mb.GetType());
                if (members.Count == 0) continue;
                Targets.Add(new MonitorTarget(mb, members));
            }
        }

        private static List<MonitorMember> FetchMembersForType(Type type)
        {
            var list = new List<MonitorMember>(16);
            var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            var t = type;
            while (t != null)
            {
                var members = t.GetMembers(flags);
                for (var i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (RuntimeDiscovery.IsCompilerGenerated(member)) continue;

                    var attr = member.GetCustomAttribute<MonitorAttribute>(true);
                    if (attr == null) continue;

                    if (member is FieldInfo field)
                        list.Add(new MonitorMember(member, field, null, null, attr));
                    else if (member is PropertyInfo prop)
                    {
                        if (prop.GetIndexParameters().Length != 0) continue;
                        if (prop.GetGetMethod(true) == null) continue;
                        list.Add(new MonitorMember(member, null, prop, null, attr));
                    }
                    else if (member is MethodInfo method)
                    {
                        if (method.IsGenericMethod) continue;
                        if (method.GetParameters().Length != 0) continue;
                        if (method.ReturnType == typeof(void)) continue;
                        list.Add(new MonitorMember(member, null, null, method, attr));
                    }
                }

                t = t.BaseType;
            }

            list.Sort((a, b) =>
            {
                var groupCompare = string.CompareOrdinal(a.Group, b.Group);
                if (groupCompare != 0) return groupCompare;

                var orderCompare = a.Order.CompareTo(b.Order);
                if (orderCompare != 0) return orderCompare;

                return string.CompareOrdinal(a.Label, b.Label);
            });

            return list;
        }
    }
}