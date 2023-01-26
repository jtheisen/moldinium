//#define CHECK_AGAINST_NULLABILITY_INFO_CONTEXT

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Moldinium.Common.Misc;

[AttributeUsage(AttributeTargets.Class)]
public class RequiresDefaultAttribute : Attribute { }

public class TypeTraits
{
    public bool IsDefaultConstructible { get; }

    private TypeTraits(Type type)
    {
        IsDefaultConstructible = CanCreateInstanceUsingDefaultConstructor(type);
    }

    static bool CanCreateInstanceUsingDefaultConstructor(Type t) =>
        t.IsValueType || !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null;

    static ConcurrentDictionary<Type, TypeTraits> cache = new ConcurrentDictionary<Type, TypeTraits>();
    public static TypeTraits Get(Type type) => cache.GetOrAdd(type, t => new TypeTraits(t));
}

public class TypeInterfaces
{
    HashSet<Type> interfaces;

    TypeInterfaces(Type type)
    {
        interfaces = new HashSet<Type>();

        if (type.IsInterface)
        {
            AddInterface(type);
        }

        foreach (var i in type.GetInterfaces())
        {
            AddInterface(i);
        }
    }

    void AddInterface(Type i)
    {
        if (i.IsGenericType)
        {
            interfaces.Add(i.GetGenericTypeDefinition());
        }
        else
        {
            interfaces.Add(i);
        }
    }

    public bool DoesTypeImplement(Type i)
        => interfaces.Contains(i);

    static ConcurrentDictionary<Type, TypeInterfaces> cache = new ConcurrentDictionary<Type, TypeInterfaces>();
    public static TypeInterfaces Get(Type type) => cache.GetOrAdd(type, t => new TypeInterfaces(t));
}

public class TypeProperties
{
    public PropertyInfoStruct[] Properties { get; }

    public bool HasAnyInitSetters { get; }

    public bool HasAnyDefaultRequirements { get; }

    public struct PropertyInfoStruct
    {
        public PropertyInfo info;
        public NullableFlag? nullableFlag;
        public bool requiresDefault;
        public bool hasInitSetter;
        public bool isNotNullable;

#if CHECK_AGAINST_NULLABILITY_INFO_CONTEXT
        public bool isNotNullableAccordingToNic;
#endif

        public override string ToString()
        {
            var writer = new StringWriter();

            if (isNotNullable) writer.Write(" not-nullable");
            if (requiresDefault) writer.Write(" default-requiring");
            if (hasInitSetter) writer.Write(" with-init-setter");

            writer.Write(' ');
            writer.Write(info.Name);

            return writer.ToString().TrimStart();
        }
    }

    public TypeProperties(Type type)
    {
#if CHECK_AGAINST_NULLABILITY_INFO_CONTEXT
        var nullabilityContext = new NullabilityInfoContext();
#endif

        var nullabilityTypeFlag = NullabilityHelper.GetNullableContextFlag(type);

        var props =
            from p in type.GetProperties()
            let set = p.SetMethod
            let rpcm = set?.ReturnParameter.GetRequiredCustomModifiers()
            let nullableFlag = NullabilityHelper.GetOwnFlag(p, nullabilityTypeFlag)
            let isNotNullable = !nullableFlag?.IsNullable() ?? false
            let hasInitSetter = rpcm?.Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)) ?? false
            let requiresDefaultPerNullabilityInfo = isNotNullable && !hasInitSetter
            let requiresDefaultPerAttribute = p.GetCustomAttribute<RequiresDefaultAttribute>() is not null
            select new PropertyInfoStruct
            {
                info = p,
                isNotNullable = isNotNullable,
                requiresDefault = requiresDefaultPerAttribute || requiresDefaultPerNullabilityInfo,
                nullableFlag = nullableFlag,
                hasInitSetter = hasInitSetter,
#if CHECK_AGAINST_NULLABILITY_INFO_CONTEXT
                isNotNullableAccordingToNic = nullabilityContext.Create(p).ReadState == NullabilityState.NotNull
#endif
            };

        Properties = props.ToArray();

#if CHECK_AGAINST_NULLABILITY_INFO_CONTEXT

        foreach (var p in Properties)
        {
            if (p.isNotNullable != p.isNotNullableAccordingToNic) throw new Exception($"Have a discrepancy between NIC and our own nullability detection logic");
        }
#endif

        HasAnyInitSetters = Properties.Any(p => p.hasInitSetter);
        HasAnyDefaultRequirements = Properties.Any(p => p.requiresDefault);
    }

    static ConcurrentDictionary<Type, TypeProperties> cache = new ConcurrentDictionary<Type, TypeProperties>();
    public static TypeProperties Get(Type type) => cache.GetOrAdd(type, t => new TypeProperties(t));
}

public struct ComparableArray<T> : IEquatable<ComparableArray<T>>
    where T : class
{
    private readonly T[] values;

    public ComparableArray(T[] values)
    {
        this.values = values;
    }

    public override int GetHashCode()
    {
        var hash = 0;

        foreach (T v in values)
        {
            hash ^= v.GetHashCode();
        }

        return hash;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is null) return false;

        if (obj is ComparableArray<T> other)
        {
            return Equals(other);
        }
        else
        {
            return false;
        }
    }

    public bool Equals(ComparableArray<T> other)
    {
        if (values.Length != other.values.Length) return false;

        for (var i = 0; i < values.Length; i++)
        {
            if (!values[i].Equals(other.values[i])) return false;
        }

        return true;
    }
}
