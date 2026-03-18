using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace UnityEssentials
{
    /// <summary>
    /// Marks a field/property/method to be shown in the ImGui Runtime Monitor.
    /// Supported targets:
    /// - Fields
    /// - Properties (non-indexer)
    /// - Methods (parameterless, non-generic, non-void return)
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class MonitorAttribute : Attribute
    {
        /// <summary>
        /// Optional label shown in the UI. If omitted, the member name is used.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Optional group name. If omitted, the declaring type name is used.
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Optional numeric format string used for IFormattable values (for example "0.00" or "F2").
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// Source line number of the attribute application, filled in automatically by the compiler.
        /// Used to preserve declaration order across different metadata tables (fields vs properties).
        /// </summary>
        public int SourceOrder { get; }

        public MonitorAttribute(string label = null, [CallerLineNumber] int sourceOrder = 0)
        {
            Label = label;
            SourceOrder = sourceOrder;
        }
    }

    /// <summary>
    /// Screen corner used by <see cref="MonitorDockAttribute"/>.
    /// </summary>
    public enum MonitorCorner
    {
        TopLeft = 0,
        TopCenter,
        TopRight,
        CenterLeft,
        Center,
        CenterRight,
        BottomLeft,
        BottomCenter,
        BottomRight,
    }

    /// <summary>
    /// Place on a MonoBehaviour class to dock its monitored members into a screen corner.
    /// If omitted, the default is <see cref="MonitorCorner.TopLeft"/> with Order 0.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class MonitorDockAttribute : Attribute
    {
        /// <summary>
        /// The screen corner this script's groups are docked to.
        /// </summary>
        public MonitorCorner Corner { get; }

        /// <summary>
        /// Sort priority among scripts in the same corner. Lower numbers are closer to the corner's anchor edge.
        /// </summary>
        public int Order { get; set; }

        public MonitorDockAttribute(MonitorCorner corner = MonitorCorner.TopLeft)
        {
            Corner = corner;
        }
    }

    /// <summary>
    /// Marks a field or property of type <see cref="MonitorGraphData"/> to be rendered as a live
    /// line-plot graph in the monitoring overlay.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class MonitorGraphAttribute : Attribute
    {
        /// <summary>
        /// Optional label shown above the graph. If omitted, the member name is used.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Optional group name. If omitted, the declaring type name is used.
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Y-axis minimum value.
        /// </summary>
        public float Min { get; set; }

        /// <summary>
        /// Y-axis maximum value.
        /// </summary>
        public float Max { get; set; } = 50f;

        /// <summary>
        /// Graph height in pixels.
        /// </summary>
        public float Height { get; set; } = 25f;

        /// <summary>
        /// Source line number of the attribute application, filled in automatically by the compiler.
        /// Used to preserve declaration order across different metadata tables (fields vs properties).
        /// </summary>
        public int SourceOrder { get; }

        public MonitorGraphAttribute(string label = null, [CallerLineNumber] int sourceOrder = 0)
        {
            Label = label;
            SourceOrder = sourceOrder;
        }
    }
}
