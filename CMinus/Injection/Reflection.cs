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

    public Boolean IsDefaultConstructible { get; }

    public struct PropertyInfoStruct
    {
        public PropertyInfo info;
        public Boolean hasInitSetter;
    }

	public Reflection(Type type)
	{
        Type = type;

        IsDefaultConstructible = CanCreateInstanceUsingDefaultConstructor(type);

        var props =
            from p in type.GetProperties()
            let set = p.SetMethod
            let rpcm = set?.ReturnParameter.GetRequiredCustomModifiers()
            select new PropertyInfoStruct
            {
                info = p,
                hasInitSetter = rpcm?.Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)) ?? false
            };

        Properties = props.ToArray();

        HasAnyInitSetters = Properties.Any(p => p.hasInitSetter);
    }

    static bool CanCreateInstanceUsingDefaultConstructor(Type t) =>
        t.IsValueType || !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null;

    static ConcurrentDictionary<Type, Reflection> cache = new ConcurrentDictionary<Type, Reflection>();

    public static Reflection Get(Type type) => cache.GetOrAdd(type, t => new Reflection(t));
}
