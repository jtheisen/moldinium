using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CMinus.Injection;

[AttributeUsage(AttributeTargets.Class)]
public class RequiresDefaultAttribute : Attribute { }

public class TypeTraits
{
    public Boolean IsDefaultConstructible { get; }

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

        foreach (var i in type.GetInterfaces())
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
    }

    public Boolean DoesTypeImplement(Type i)
        => interfaces.Contains(i);

    static ConcurrentDictionary<Type, TypeInterfaces> cache = new ConcurrentDictionary<Type, TypeInterfaces>();
    public static TypeInterfaces Get(Type type) => cache.GetOrAdd(type, t => new TypeInterfaces(t));
}

public class TypeProperties
{
    public PropertyInfoStruct[] Properties { get; }

    public Boolean HasAnyInitSetters { get; }

    public Boolean HasAnyDefaultRequirements { get; }

    public struct PropertyInfoStruct
    {
        public PropertyInfo info;
        public Boolean requiresDefault;
        public Boolean hasInitSetter;
    }

	public TypeProperties(Type type)
	{
        var nullabilityContext = new NullabilityInfoContext();

        var props =
            from p in type.GetProperties()
            let set = p.SetMethod
            let rpcm = set?.ReturnParameter.GetRequiredCustomModifiers()
            let nullabilityInfo = nullabilityContext.Create(p)
            let hasInitSetter = rpcm?.Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)) ?? false
            let requiresDefaultPerNullabilityInfo = nullabilityInfo.ReadState == NullabilityState.NotNull && !hasInitSetter
            let requiresDefaultPerAttribute = p.GetCustomAttribute<RequiresDefaultAttribute>() is not null
            select new PropertyInfoStruct
            {
                info = p,
                requiresDefault = requiresDefaultPerAttribute || requiresDefaultPerNullabilityInfo,
                hasInitSetter = hasInitSetter
            };

        Properties = props.ToArray();

        HasAnyInitSetters = Properties.Any(p => p.hasInitSetter);
        HasAnyDefaultRequirements = Properties.Any(p => p.requiresDefault);
    }

    static ConcurrentDictionary<Type, TypeProperties> cache = new ConcurrentDictionary<Type, TypeProperties>();
    public static TypeProperties Get(Type type) => cache.GetOrAdd(type, t => new TypeProperties(t));
}
