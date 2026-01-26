using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnityEssentials
{
    public static class RuntimeMonitorImGui
    {
        private const float DefaultRefreshSeconds = 0.25f;

        private static bool _installed;
        private static readonly List<Target> Targets = new(64);
        private static readonly Dictionary<Type, TypePlan> TypePlans = new();
        private static float _nextRefreshTime;

        public static bool Enabled { get; set; } = true;
        public static float RefreshSeconds { get; set; } = DefaultRefreshSeconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed)
                return;

            _installed = true;
            ImGuiHost.Register(DrawOverlay);

            // Zero-setup behavior: always auto-register scene targets that contain [Monitor] members.
            SceneAutoRegisterHook.EnsureInstalled();
        }

        public static void Register(object target)
        {
            if (target == null)
                return;

            var plan = GetOrCreatePlan(target.GetType());
            if (plan.Entries.Count == 0)
                return;

            for (var i = 0; i < Targets.Count; i++)
            {
                if (ReferenceEquals(Targets[i].Instance, target))
                    return;
            }

            Targets.Add(new Target(target, plan));
        }

        public static void Unregister(object target)
        {
            if (target == null)
                return;

            for (var i = Targets.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(Targets[i].Instance, target))
                    Targets.RemoveAt(i);
            }
        }

        private static void DrawOverlay()
        {
            if (!Enabled)
                return;

            var now = Time.unscaledTime;
            if (now >= _nextRefreshTime)
            {
                _nextRefreshTime = now + Mathf.Max(0.01f, RefreshSeconds);

                for (var i = Targets.Count - 1; i >= 0; i--)
                {
                    if (!Targets[i].IsAlive())
                    {
                        Targets.RemoveAt(i);
                        continue;
                    }

                    Targets[i].Refresh();
                }
            }

            if (Targets.Count == 0)
                return;

            // One simple window per group (no header/decoration), translucent bg.
            // This keeps drawing cheap and mirrors the user’s requested style.
            var grouped = GetGroupedSnapshot();
            for (var i = 0; i < grouped.Count; i++)
                DrawGroupWindow(grouped[i]);
        }

        private static TypePlan GetOrCreatePlan(Type type)
        {
            if (TypePlans.TryGetValue(type, out var existing))
                return existing;

            var plan = TypePlan.Build(type);
            TypePlans[type] = plan;
            return plan;
        }

        private readonly struct OverlayGroup
        {
            public readonly string GroupName;
            public readonly List<(string Label, string Value, bool HadError)> Lines;

            public OverlayGroup(string groupName, List<(string Label, string Value, bool HadError)> lines)
            {
                GroupName = groupName;
                Lines = lines;
            }
        }

        private static readonly List<OverlayGroup> _overlayGroups = new(16);
        private static readonly Dictionary<string, List<(string Label, string Value, bool HadError)>> _overlayGroupMap = new(StringComparer.Ordinal);

        private static List<OverlayGroup> GetGroupedSnapshot()
        {
            _overlayGroups.Clear();
            _overlayGroupMap.Clear();

            for (var t = 0; t < Targets.Count; t++)
            {
                var target = Targets[t];

                // Include type name in group in case user didn’t set Group.
                // Each entry’s Group can be null/empty: treat that as type header.
                for (var s = 0; s < target.StateCount; s++)
                {
                    target.GetStateLine(s, out var group, out var label, out var value, out var hadError);

                    if (string.IsNullOrWhiteSpace(group))
                        group = target.DisplayName;

                    if (!_overlayGroupMap.TryGetValue(group, out var list))
                    {
                        list = new List<(string Label, string Value, bool HadError)>(32);
                        _overlayGroupMap[group] = list;
                    }

                    list.Add((label, value, hadError));
                }
            }

            foreach (var kvp in _overlayGroupMap)
                _overlayGroups.Add(new OverlayGroup(kvp.Key, kvp.Value));

            // Stable ordering by group name.
            _overlayGroups.Sort((a, b) => string.CompareOrdinal(a.GroupName, b.GroupName));
            return _overlayGroups;
        }

        private static void DrawGroupWindow(OverlayGroup group)
        {
            ImGuiNET.ImGui.SetNextWindowBgAlpha(0.35f);

            const ImGuiNET.ImGuiWindowFlags flags =
                ImGuiNET.ImGuiWindowFlags.NoDecoration |
                ImGuiNET.ImGuiWindowFlags.AlwaysAutoResize |
                ImGuiNET.ImGuiWindowFlags.NoSavedSettings |
                ImGuiNET.ImGuiWindowFlags.NoFocusOnAppearing |
                ImGuiNET.ImGuiWindowFlags.NoNav;

            // Use group name as window id so ImGui handles layout/persistence.
            if (!ImGuiNET.ImGui.Begin(group.GroupName, flags))
            {
                ImGuiNET.ImGui.End();
                return;
            }

            var lines = group.Lines;
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.HadError)
                    ImGuiNET.ImGui.TextColored(new System.Numerics.Vector4(1f, 0.3f, 0.3f, 1f), $"{line.Label}: {line.Value}");
                else
                    ImGuiNET.ImGui.TextUnformatted($"{line.Label}: {line.Value}");
            }

            ImGuiNET.ImGui.End();
        }

        private sealed class Target
        {
            public readonly object Instance;
            private readonly TypePlan _plan;
            private readonly List<EntryState> _states;

            public int StateCount => _states.Count;

            public string DisplayName => _plan.DisplayName;

            public Target(object instance, TypePlan plan)
            {
                Instance = instance;
                _plan = plan;
                _states = new List<EntryState>(plan.Entries.Count);
                for (var i = 0; i < plan.Entries.Count; i++)
                    _states.Add(new EntryState(plan.Entries[i]));

                Refresh();
            }

            public bool IsAlive()
            {
                if (Instance is UnityEngine.Object unityObj)
                    return unityObj != null;
                return true;
            }

            public void Refresh()
            {
                for (var i = 0; i < _states.Count; i++)
                {
                    _states[i].Refresh(Instance);
                }
            }

            public void GetStateLine(int index, out string group, out string label, out string value, out bool hadError)
            {
                var state = _states[index];
                group = state.Entry.Group;
                label = state.Entry.Label;
                value = state.LastValue;
                hadError = state.HadError;
            }
        }

        private sealed class EntryState
        {
            public readonly Entry Entry;
            public string LastValue => _lastValue;
            public bool HadError => _hadError;

            private string _lastValue = "-";
            private bool _hadError;

            public EntryState(Entry entry) => Entry = entry;

            public void Refresh(object instance)
            {
                try
                {
                    _hadError = false;
                    var value = Entry.Getter(instance);
                    _lastValue = FormatValue(value, Entry.Format);
                }
                catch (Exception)
                {
                    _hadError = true;
                    _lastValue = "<error>";
                }
            }

            public void DrawUi()
            {
                if (_hadError)
                    ImGuiNET.ImGui.TextColored(new System.Numerics.Vector4(1f, 0.3f, 0.3f, 1f), $"{Entry.Label}: {_lastValue}");
                else
                    ImGuiNET.ImGui.TextUnformatted($"{Entry.Label}: {_lastValue}");
            }
        }

        private static string FormatValue(object value, string format)
        {
            if (value == null)
                return "null";

            if (value is UnityEngine.Object uo)
                return uo == null ? "null" : uo.name;

            if (value is string s)
                return s;

            if (!string.IsNullOrEmpty(format) && value is IFormattable formattable)
                return formattable.ToString(format, System.Globalization.CultureInfo.InvariantCulture);

            return value.ToString() ?? "<no ToString()>";
        }

        private sealed class TypePlan
        {
            public readonly string DisplayName;
            public readonly List<Entry> Entries;

            private TypePlan(string displayName, List<Entry> entries)
            {
                DisplayName = displayName;
                Entries = entries;
            }

            public static TypePlan Build(Type type)
            {
                var entries = new List<Entry>(16);

                var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                var t = type;
                while (t != null)
                {
                    var members = t.GetMembers(flags);
                    for (var i = 0; i < members.Length; i++)
                    {
                        var attr = members[i].GetCustomAttribute<MonitorAttribute>(true);
                        if (attr == null)
                            continue;

                        if (members[i] is FieldInfo field)
                        {
                            if (field.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), true))
                                continue;

                            entries.Add(Entry.ForField(field, attr));
                        }
                        else if (members[i] is PropertyInfo prop)
                        {
                            if (prop.GetIndexParameters().Length != 0)
                                continue;

                            var getter = prop.GetGetMethod(true);
                            if (getter == null)
                                continue;
                            if (getter.IsGenericMethod)
                                continue;

                            entries.Add(Entry.ForProperty(prop, attr));
                        }
                        else if (members[i] is MethodInfo method)
                        {
                            if (method.IsGenericMethod)
                                continue;
                            if (method.GetParameters().Length != 0)
                                continue;
                            if (method.ReturnType == typeof(void))
                                continue;

                            entries.Add(Entry.ForMethod(method, attr));
                        }
                    }

                    t = t.BaseType;
                }

                entries.Sort(EntryComparer.Instance);

                return new TypePlan(type.Name, entries);
            }
        }

        private sealed class EntryComparer : IComparer<Entry>
        {
            public static readonly EntryComparer Instance = new();

            public int Compare(Entry x, Entry y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                var groupCompare = string.CompareOrdinal(x.Group, y.Group);
                if (groupCompare != 0) return groupCompare;

                var orderCompare = x.Order.CompareTo(y.Order);
                if (orderCompare != 0) return orderCompare;

                return string.CompareOrdinal(x.Label, y.Label);
            }
        }

        private sealed class Entry
        {
            public readonly string Label;
            public readonly string Group;
            public readonly int Order;
            public readonly string Format;
            public readonly Func<object, object> Getter;

            private Entry(string label, string group, int order, string format, Func<object, object> getter)
            {
                Label = label;
                Group = group;
                Order = order;
                Format = format;
                Getter = getter;
            }

            public static Entry ForField(FieldInfo field, MonitorAttribute attr)
            {
                var label = string.IsNullOrWhiteSpace(attr.Label) ? field.Name : attr.Label;
                var group = attr.Group;
                var order = attr.Order;
                var format = attr.Format;

                return new Entry(label, group, order, format, instance => field.GetValue(field.IsStatic ? null : instance));
            }

            public static Entry ForProperty(PropertyInfo prop, MonitorAttribute attr)
            {
                var label = string.IsNullOrWhiteSpace(attr.Label) ? prop.Name : attr.Label;
                var group = attr.Group;
                var order = attr.Order;
                var format = attr.Format;

                return new Entry(label, group, order, format, instance => prop.GetValue(instance));
            }

            public static Entry ForMethod(MethodInfo method, MonitorAttribute attr)
            {
                var label = string.IsNullOrWhiteSpace(attr.Label) ? method.Name : attr.Label;
                var group = attr.Group;
                var order = attr.Order;
                var format = attr.Format;

                return new Entry(label, group, order, format, instance => method.Invoke(method.IsStatic ? null : instance, null));
            }
        }

        private static class SceneAutoRegisterHook
        {
            private static bool _hooked;

            public static void EnsureInstalled()
            {
                if (_hooked)
                    return;

                _hooked = true;
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            }

            private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
            {
                try
                {
                    // Include inactive objects too; this matches DI-style scans and avoids confusion.
#if UNITY_2023_1_OR_NEWER
                    var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
#else
                    var behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(includeInactive: true);
#endif
                    for (var i = 0; i < behaviours.Length; i++)
                    {
                        var b = behaviours[i];
                        if (b == null)
                            continue;

                        var type = b.GetType();


                        var plan = GetOrCreatePlan(type);
                        if (plan.Entries.Count > 0)
                            Register(b);
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }
    }
}
