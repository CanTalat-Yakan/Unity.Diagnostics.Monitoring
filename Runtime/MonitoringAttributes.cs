using System;

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
        /// Sort order within a group. Lower numbers show first.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Optional numeric format string used for IFormattable values (for example "0.00" or "F2").
        /// </summary>
        public string Format { get; set; }

        public MonitorAttribute(string label = null)
        {
            Label = label;
        }
    }
}
