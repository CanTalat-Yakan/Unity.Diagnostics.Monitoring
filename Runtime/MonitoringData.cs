using System.Collections.Generic;
using System.Reflection;

namespace UnityEssentials
{
    public readonly struct MonitorTarget
    {
        public readonly object TargetInstance;
        public readonly string TypeName;
        public readonly List<MonitorMember> Members;

        public MonitorTarget(object instance, List<MonitorMember> members)
        {
            TargetInstance = instance;
            TypeName = instance != null ? instance.GetType().Name : "<null>";
            Members = members;
        }

        public bool IsAlive()
        {
            if (TargetInstance is UnityEngine.Object uo)
                return uo != null;
            return TargetInstance != null;
        }
    }

    public readonly struct MonitorMember
    {
        public readonly string Label;
        public readonly string Group;
        public readonly int Order;
        public readonly string Format;

        public readonly FieldInfo Field;
        public readonly PropertyInfo Property;
        public readonly MethodInfo Method;

        public MonitorMember(MemberInfo member, FieldInfo field, PropertyInfo property, MethodInfo method,
            MonitorAttribute attr)
        {
            Field = field;
            Property = property;
            Method = method;

            Label = string.IsNullOrWhiteSpace(attr.Label) ? member.Name : attr.Label;
            Group = attr.Group;
            Order = attr.Order;
            Format = attr.Format;
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
}