using System;

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
        if (type == typeof(String))
        {
            return typeof(DefaultString);
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
