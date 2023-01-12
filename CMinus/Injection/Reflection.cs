using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace CMinus.Injection;

public class Reflection
{
    public Type Type { get; }

    public PropertyInfoStruct[] Properties { get; }

    public Boolean HasAnyInitSetters { get; }

    public Boolean HasAnyDefaultRequirements { get; }

    public Boolean IsDefaultConstructible { get; }

    public struct PropertyInfoStruct
    {
        public PropertyInfo info;
        public Boolean requiresDefault;
        public Boolean hasInitSetter;
    }

	public Reflection(Type type)
	{
        Type = type;

        IsDefaultConstructible = CanCreateInstanceUsingDefaultConstructor(type);

        var nullabilityContext = new NullabilityInfoContext();

        var props =
            from p in type.GetProperties()
            let set = p.SetMethod
            let rpcm = set?.ReturnParameter.GetRequiredCustomModifiers()
            let nullabilityInfo = nullabilityContext.Create(p)
            let hasInitSetter = rpcm?.Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)) ?? false
            select new PropertyInfoStruct
            {
                info = p,
                requiresDefault = nullabilityInfo.ReadState == NullabilityState.NotNull && !hasInitSetter,
                hasInitSetter = hasInitSetter
            };

        Properties = props.ToArray();

        HasAnyInitSetters = Properties.Any(p => p.hasInitSetter);
        HasAnyDefaultRequirements = Properties.Any(p => p.requiresDefault);
    }

    static bool CanCreateInstanceUsingDefaultConstructor(Type t) =>
        t.IsValueType || !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null;

    static ConcurrentDictionary<Type, Reflection> cache = new ConcurrentDictionary<Type, Reflection>();

    public static Reflection Get(Type type) => cache.GetOrAdd(type, t => new Reflection(t));
}
