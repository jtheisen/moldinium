using Moldinium.Misc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace Moldinium;

public interface IDefault<T>
{
    T Default { get; }
}

public struct DummyDefault<T> : IDefault<T>
{
    public T Default => default!;
}

public struct DefaultString : IDefault<String>
{
    public string Default => String.Empty;
}

public struct DefaultDefaultConstructible<T> : IDefault<T>
    where T : new()
{
    public T Default => new T();
}

public struct DefaultDefaultConstructible<Type, Implementation> : IDefault<Type>
    where Implementation : Type, new()
{
    public Type Default => new Implementation();
}

public struct DefaultTrackableList<T> : IDefault<ICollection<T>>
{
    public ICollection<T> Default => new TrackableList<T>();
}

public interface IDefaultProvider
{
    Type? GetDefaultType(Type type);
}

public class DefaultDefaultProvider : IDefaultProvider
{
    private readonly Type? genericCollectionType;

    public DefaultDefaultProvider(Type? genericCollectionType)
    {
        this.genericCollectionType = genericCollectionType;
    }

    public Type? GetDefaultType(Type type)
    {
        if (type.IsValueType)
        {
            return typeof(DefaultDefaultConstructible<>);
        }
        else if (type == typeof(String))
        {
            return typeof(DefaultString);
        }

        var interfaces = TypeInterfaces.Get(type);

        if (interfaces.DoesTypeImplement(typeof(IDisposable))) return null;
        if (interfaces.DoesTypeImplement(typeof(IAsyncDisposable))) return null;

        var traits = TypeTraits.Get(type);

        if (interfaces.DoesTypeImplement(typeof(ICollection<>)))
        {
            if (genericCollectionType is not null)
            {
                var defaultImplementationType = GetDefaultImplementationTypeForCollectionType(genericCollectionType, type);

                return defaultImplementationType;
            }
            else if (traits.IsDefaultConstructible)
            {
                return typeof(DefaultDefaultConstructible<>);
            }
        }

        return null;
    }

    Type GetDefaultImplementationTypeForCollectionType(Type genericCollectionType, Type valueType)
    {
        var arguments = valueType.GetGenericArguments();

        if (arguments.Length <= 1)
        {
            var typeArgument = arguments.Length == 1 ? arguments[0] : typeof(Object);

            var implementationType = genericCollectionType.MakeGenericType(typeArgument);

            var defaultImplementationType = typeof(DefaultDefaultConstructible<,>).MakeGenericType(valueType, implementationType);

            return defaultImplementationType;
        }
        else
        {
            throw new Exception($"Expected generic collection type {valueType} to have at most one type paramter");
        }
    }
}

public static class Defaults
{
    public static Type CreateConcreteDefaultImplementationType(Type type, Type valueType)
        => type.IsGenericTypeDefinition ? type.MakeGenericType(valueType) : type;

    public static IDefaultProvider GetDefaultDefaultProvider(Type? genericCollectionType = null)
        => new DefaultDefaultProvider(genericCollectionType);
}
