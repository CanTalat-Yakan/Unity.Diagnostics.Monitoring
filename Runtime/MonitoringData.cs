using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEssentials
{
    public readonly struct MonitorTarget
    {
        public readonly object TargetInstance;
        public readonly string TypeName;
        public readonly List<MonitorMember> Members;
        public readonly MonitorCorner Corner;
        public readonly int DockOrder;

        public MonitorTarget(object instance, List<MonitorMember> members,
            MonitorCorner corner = MonitorCorner.TopLeft, int dockOrder = 0)
        {
            TargetInstance = instance;
            TypeName = instance != null ? instance.GetType().Name : "<null>";
            Members = members;
            Corner = corner;
            DockOrder = dockOrder;
        }

        public bool IsAlive()
        {
            if (TargetInstance is UnityEngine.Object uo)
                return uo != null;
            return TargetInstance != null;
        }

        public bool IsVisibleInHierarchy()
        {
            if (TargetInstance is UnityEngine.MonoBehaviour mb)
                return mb != null && mb.isActiveAndEnabled;

            if (TargetInstance is UnityEngine.Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return IsAlive();
        }
    }

    public readonly struct MonitorMember
    {
        public readonly string Label;
        public readonly string Group;
        public readonly int Order;
        public readonly int GroupOrder;
        public readonly string Format;

        public readonly FieldInfo Field;
        public readonly PropertyInfo Property;
        public readonly MethodInfo Method;

        /// <summary>
        /// Non-null when this member is a graph (decorated with <see cref="MonitorGraphAttribute"/>).
        /// </summary>
        public readonly MonitorGraphAttribute GraphMeta;

        public bool IsGraph => GraphMeta != null;

        public MonitorMember(MemberInfo member, FieldInfo field, PropertyInfo property, MethodInfo method,
            MonitorAttribute attr)
        {
            Field = field;
            Property = property;
            Method = method;
            GraphMeta = null;

            Label = string.IsNullOrWhiteSpace(attr.Label) ? member.Name : attr.Label;
            Group = attr.Group;
            Order = member.MetadataToken;
            GroupOrder = property != null ? property.GetGetMethod(true).MetadataToken
                       : method != null ? method.MetadataToken
                       : int.MaxValue;
            Format = attr.Format;
        }

        public MonitorMember(MemberInfo member, FieldInfo field, PropertyInfo property,
            MonitorGraphAttribute graphAttr)
        {
            Field = field;
            Property = property;
            Method = null;
            GraphMeta = graphAttr;

            Label = graphAttr.Label;
            Group = graphAttr.Group;
            Order = member.MetadataToken;
            GroupOrder = property != null ? property.GetGetMethod(true).MetadataToken : int.MaxValue;
            Format = null;
        }

        public object GetValue(object instance)
        {
            if (Field != null)
                return Field.GetValue(Field.IsStatic ? null : instance);

            if (Property != null)
            {
                var getter = Property.GetGetMethod(true);
                return Property.GetValue(getter != null && getter.IsStatic ? null : instance);
            }

            if (Method != null)
                return Method.Invoke(Method.IsStatic ? null : instance, null);

            return null;
        }
    }

    /// <summary>
    /// A fixed-capacity ring buffer that accumulates float samples for graph display.
    /// Used with <see cref="MonitorGraphAttribute"/> to render live line plots in the monitoring overlay.
    /// </summary>
    public sealed class MonitorGraphData
    {
        private readonly float[] _buffer;
        private int _cursor;
        private int _count;

        public int Capacity => _buffer.Length;
        public int Count => _count;

        public MonitorGraphData(int capacity)
        {
            _buffer = new float[capacity];
        }

        public void Push(float value)
        {
            _buffer[_cursor] = value;
            _cursor = (_cursor + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
        }

        /// <summary>
        /// Copies samples in chronological order into <paramref name="destination"/>.
        /// Returns the number of samples copied.
        /// <paramref name="destination"/> must have length >= <see cref="Count"/>.
        /// </summary>
        public int CopyLinearized(float[] destination)
        {
            var n = _count;
            for (var i = 0; i < n; i++)
            {
                var idx = _cursor - n + i;
                if (idx < 0) idx += _buffer.Length;
                destination[i] = _buffer[idx];
            }
            return n;
        }

        /// <summary>
        /// Computes the average of all current samples.
        /// </summary>
        public float Average()
        {
            if (_count == 0) return 0f;
            double sum = 0;
            for (var i = 0; i < _count; i++)
                sum += _buffer[i];
            return (float)(sum / _count);
        }

        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _cursor = 0;
            _count = 0;
        }
    }
}