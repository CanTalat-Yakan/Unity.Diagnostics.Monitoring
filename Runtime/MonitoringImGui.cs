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

        private const float Padding = 10f;
        private const float Gap = 4f;

        private static float _nextRefreshTime;

        private static readonly List<OverlayGroup> _overlayGroups = new(16);

        // Composite key: (corner, groupName) → lines.
        private static readonly Dictionary<(MonitorCorner, string), OverlayGroupBuilder> _groupBuilders = new();

        /// <summary>
        /// A single entry in an overlay group — either a text line or a graph.
        /// </summary>
        public readonly struct OverlayEntry
        {
            // Text mode
            public readonly string Label;
            public readonly string TextValue;
            public readonly bool HadError;

            // Graph mode
            public readonly MonitorGraphData GraphData;
            public readonly MonitorGraphAttribute GraphMeta;

            public bool IsGraph => GraphData != null;

            public OverlayEntry(string label, string textValue, bool hadError)
            {
                Label = label;
                TextValue = textValue;
                HadError = hadError;
                GraphData = null;
                GraphMeta = null;
            }

            public OverlayEntry(string label, MonitorGraphData graphData, MonitorGraphAttribute graphMeta)
            {
                Label = label;
                TextValue = null;
                HadError = false;
                GraphData = graphData;
                GraphMeta = graphMeta;
            }
        }

        /// <summary>
        /// A pre-grouped snapshot to draw.
        /// </summary>
        public readonly struct OverlayGroup
        {
            public readonly string Key;
            public readonly string GroupName;
            public readonly MonitorCorner Corner;
            public readonly int DockOrder;
            public readonly int MinGroupOrder;
            public readonly List<OverlayEntry> Entries;

            public OverlayGroup(string key, string groupName, MonitorCorner corner, int dockOrder,
                int minGroupOrder, List<OverlayEntry> entries)
            {
                Key = key;
                GroupName = groupName;
                Corner = corner;
                DockOrder = dockOrder;
                MinGroupOrder = minGroupOrder;
                Entries = entries;
            }
        }

        private struct OverlayGroupBuilder
        {
            public string DisplayName;
            public MonitorCorner Corner;
            public int DockOrder;
            public int MinGroupOrder;
            public List<OverlayEntry> Entries;
        }

        private static float[] _graphScratch = new float[512];

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

            // Track cumulative Y offset per corner.
            float offsetTL = 0f, offsetTR = 0f, offsetBL = 0f, offsetBR = 0f;

            for (var i = 0; i < _overlayGroups.Count; i++)
            {
                var g = _overlayGroups[i];
                ref float offset = ref offsetTL;
                switch (g.Corner)
                {
                    case MonitorCorner.TopRight: offset = ref offsetTR; break;
                    case MonitorCorner.BottomLeft: offset = ref offsetBL; break;
                    case MonitorCorner.BottomRight: offset = ref offsetBR; break;
                }

                var height = DrawGroupWindow(g, offset);
                offset += height + Gap;
            }
        }

        private static void RebuildOverlayGroupsFromHostCache()
        {
            _overlayGroups.Clear();
            _groupBuilders.Clear();

            var targets = MonitoringHost.Targets;
            for (var t = 0; t < targets.Count; t++)
            {
                var target = targets[t];
                if (!target.IsVisibleInHierarchy())
                    continue;

                var members = target.Members;
                for (var m = 0; m < members.Count; m++)
                {
                    var member = members[m];

                    var hasExplicitGroup = !string.IsNullOrWhiteSpace(member.Group);
                    var displayName = hasExplicitGroup ? member.Group : member.Label;

                    // Only merge entries into the same window when they share an explicit Group name.
                    // Without an explicit Group, each member gets its own standalone window.
                    var keyGroup = hasExplicitGroup ? member.Group : $"{target.TypeName}::{member.Label}::{m}";
                    var key = (target.Corner, keyGroup);
                    if (!_groupBuilders.TryGetValue(key, out var builder))
                    {
                        builder = new OverlayGroupBuilder
                        {
                            DisplayName = displayName,
                            Corner = target.Corner,
                            DockOrder = target.DockOrder,
                            MinGroupOrder = member.GroupOrder,
                            Entries = new List<OverlayEntry>(32),
                        };
                        _groupBuilders[key] = builder;
                    }
                    else
                    {
                        if (target.DockOrder < builder.DockOrder)
                            builder.DockOrder = target.DockOrder;
                        if (member.GroupOrder < builder.MinGroupOrder)
                            builder.MinGroupOrder = member.GroupOrder;
                        _groupBuilders[key] = builder;
                    }

                    if (member.IsGraph)
                    {
                        try
                        {
                            var graphData = member.GetValue(target.TargetInstance) as MonitorGraphData;
                            if (graphData != null)
                                builder.Entries.Add(new OverlayEntry(member.Label, graphData, member.GraphMeta));
                        }
                        catch
                        {
                            builder.Entries.Add(new OverlayEntry(member.Label, "<error>", true));
                        }
                    }
                    else
                    {
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

                        builder.Entries.Add(new OverlayEntry(member.Label, valueStr, hadError));
                    }
                }
            }

            foreach (var kvp in _groupBuilders)
            {
                var b = kvp.Value;
                _overlayGroups.Add(new OverlayGroup(kvp.Key.Item2, b.DisplayName, b.Corner, b.DockOrder, b.MinGroupOrder, b.Entries));
            }

            // Sort: Corner → DockOrder → declaration order (first member in group).
            _overlayGroups.Sort((a, b) =>
            {
                var cc = ((int)a.Corner).CompareTo((int)b.Corner);
                if (cc != 0) return cc;

                // For bottom corners, reverse DockOrder so lowest order is drawn last (closest to bottom edge).
                var isBottom = a.Corner == MonitorCorner.BottomLeft || a.Corner == MonitorCorner.BottomRight;
                var oc = isBottom
                    ? b.DockOrder.CompareTo(a.DockOrder)
                    : a.DockOrder.CompareTo(b.DockOrder);
                if (oc != 0) return oc;

                return a.MinGroupOrder.CompareTo(b.MinGroupOrder);
            });
        }

        private static float DrawGroupWindow(OverlayGroup group, float cornerOffset)
        {
            var vp = ImGui.GetMainViewport();
            var workPos = vp.WorkPos;
            var workSize = vp.WorkSize;

            System.Numerics.Vector2 pos, pivot;
            switch (group.Corner)
            {
                default: // TopLeft
                    pos = new System.Numerics.Vector2(workPos.X + Padding, workPos.Y + Padding + cornerOffset);
                    pivot = new System.Numerics.Vector2(0f, 0f);
                    break;
                case MonitorCorner.TopRight:
                    pos = new System.Numerics.Vector2(workPos.X + workSize.X - Padding, workPos.Y + Padding + cornerOffset);
                    pivot = new System.Numerics.Vector2(1f, 0f);
                    break;
                case MonitorCorner.BottomLeft:
                    pos = new System.Numerics.Vector2(workPos.X + Padding, workPos.Y + workSize.Y - Padding - cornerOffset);
                    pivot = new System.Numerics.Vector2(0f, 1f);
                    break;
                case MonitorCorner.BottomRight:
                    pos = new System.Numerics.Vector2(workPos.X + workSize.X - Padding, workPos.Y + workSize.Y - Padding - cornerOffset);
                    pivot = new System.Numerics.Vector2(1f, 1f);
                    break;
            }

            ImGui.SetNextWindowPos(pos, ImGuiCond.Always, pivot);
            ImGui.SetNextWindowBgAlpha(0.35f);

            const ImGuiWindowFlags flags =
                ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.AlwaysAutoResize |
                ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoFocusOnAppearing |
                ImGuiWindowFlags.NoNav |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoInputs;

            // Use a unique window ID combining corner + key to avoid collisions.
            var windowId = $"{group.Corner}##{group.Key}";
            if (!ImGui.Begin(windowId, flags))
            {
                ImGui.End();
                return 0f;
            }

            // // Draw group header.
            // ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1f), group.GroupName);
            // ImGui.Separator();

            var entries = group.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.IsGraph)
                {
                    var gd = entry.GraphData;
                    var gm = entry.GraphMeta;
                    if (gd.Count > 0)
                    {
                        if (_graphScratch.Length < gd.Capacity)
                            _graphScratch = new float[gd.Capacity];
                        var n = gd.CopyLinearized(_graphScratch);

                        // Remove the default frame background so only the plot line is visible.
                        ImGui.PushStyleColor(ImGuiCol.FrameBg, new System.Numerics.Vector4(0, 0, 0, 0));
                        var plotLabel = string.IsNullOrEmpty(entry.Label) ? " " : entry.Label;
                        ImGui.PlotLines(plotLabel, ref _graphScratch[0], n, 0, null,
                            gm.Min, gm.Max, new System.Numerics.Vector2(0, gm.Height));
                        ImGui.PopStyleColor();
                    }
                }
                else if (entry.HadError)
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1f, 0.3f, 0.3f, 1f),
                        $"{entry.Label}: {entry.TextValue}");
                }
                else
                {
                    ImGui.TextUnformatted($"{entry.Label}: {entry.TextValue}");
                }
            }

            var windowHeight = ImGui.GetWindowSize().Y;
            ImGui.End();
            return windowHeight;
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
