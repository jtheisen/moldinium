using CMinus.Injection;
using System;
using System.Collections.Generic;

namespace CMinus;

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

public struct DefaultDefaultConstructible<T, I> : IDefault<T>
    where I : T, new()
{
    public T Default => new I();
}

public interface IDefaultProvider
{
    Type? GetDefaultType(Type type);
}

public class DefaultDefaultProvider : IDefaultProvider
{
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

        if (interfaces.DoesTypeImplement(typeof(ICollection<>)) && traits.IsDefaultConstructible)
        {
            return typeof(DefaultDefaultConstructible<>);
        }

        return null;
   }
}

public static class Defaults
{
    public static Type CreateConcreteDefaultImplementationType(Type type, Type valueType)
        => type.IsGenericTypeDefinition ? type.MakeGenericType(valueType) : type;

    public static IDefaultProvider GetDefaultDefaultProvider()
        => new DefaultDefaultProvider();
}
