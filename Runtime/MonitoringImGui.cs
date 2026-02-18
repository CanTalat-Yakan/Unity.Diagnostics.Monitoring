using System;
using System.Collections.Generic;
using ImGuiNET;
using UnityEngine;

namespace UnityEssentials
{
    public static class MonitoringImGui
    {
        public static bool Enabled { get; set; } = true;
        public static float RefreshSeconds { get; set; } = 0.25f;

        private static float _nextRefreshTime;

        private static readonly List<OverlayGroup> _overlayGroups = new(16);
        private static readonly Dictionary<string, List<(string Label, string Value, bool HadError)>> _overlayGroupMap =
            new(StringComparer.Ordinal);

        /// <summary>
        /// A pre-grouped snapshot to draw.
        /// </summary>
        public readonly struct OverlayGroup
        {
            public readonly string GroupName;
            public readonly List<(string Label, string Value, bool HadError)> Lines;

            public OverlayGroup(string groupName, List<(string Label, string Value, bool HadError)> lines)
            {
                GroupName = groupName;
                Lines = lines;
            }
        }

        /// <summary>
        /// Called every frame by <see cref="MonitoringHost"/>.
        /// Rendering only + reading cached host data. No discovery, no hooks.
        /// </summary>
        public static void DrawImGui()
        {
            if (!Enabled)
                return;

            using var scope = ImGuiScope.TryEnter();
            if (!scope.Active)
                return;

            var now = Time.unscaledTime;
            if (now >= _nextRefreshTime)
            {
                _nextRefreshTime = now + Mathf.Max(0.01f, RefreshSeconds);
                RebuildOverlayGroupsFromHostCache();
            }

            if (_overlayGroups.Count == 0)
                return;

            for (var i = 0; i < _overlayGroups.Count; i++)
                DrawGroupWindow(_overlayGroups[i]);
        }

        private static void RebuildOverlayGroupsFromHostCache()
        {
            _overlayGroups.Clear();
            _overlayGroupMap.Clear();

            var targets = MonitoringHost.Targets;
            for (var t = 0; t < targets.Count; t++)
            {
                var target = targets[t];
                if (!target.IsAlive())
                    continue;

                var members = target.Members;
                for (var m = 0; m < members.Count; m++)
                {
                    var member = members[m];

                    var group = string.IsNullOrWhiteSpace(member.Group) ? target.TypeName : member.Group;
                    var label = member.Label;

                    string valueStr;
                    var hadError = false;
                    try
                    {
                        var value = member.GetValue(target.TargetInstance);
                        valueStr = FormatValue(value, member.Format);
                    }
                    catch
                    {
                        hadError = true;
                        valueStr = "<error>";
                    }

                    if (!_overlayGroupMap.TryGetValue(group, out var list))
                    {
                        list = new List<(string Label, string Value, bool HadError)>(32);
                        _overlayGroupMap[group] = list;
                    }

                    list.Add((label, valueStr, hadError));
                }
            }

            foreach (var kvp in _overlayGroupMap)
                _overlayGroups.Add(new OverlayGroup(kvp.Key, kvp.Value));

            _overlayGroups.Sort((a, b) => string.CompareOrdinal(a.GroupName, b.GroupName));
        }

        private static void DrawGroupWindow(OverlayGroup group)
        {
            ImGui.SetNextWindowBgAlpha(0.35f);

            const ImGuiWindowFlags flags =
                ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.AlwaysAutoResize |
                ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoFocusOnAppearing |
                ImGuiWindowFlags.NoNav;

            if (!ImGui.Begin(group.GroupName, flags))
            {
                ImGui.End();
                return;
            }

            var lines = group.Lines;
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.HadError)
                    ImGui.TextColored(new System.Numerics.Vector4(1f, 0.3f, 0.3f, 1f),
                        $"{line.Label}: {line.Value}");
                else
                    ImGui.TextUnformatted($"{line.Label}: {line.Value}");
            }

            ImGui.End();
        }

        public static string FormatValue(object value, string format)
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
    }
}
