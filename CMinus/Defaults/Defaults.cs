using System;

namespace CMinus;

public interface IDefault<T>
{
    T Default { get; }
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
    public static Type CreateDefaultStructType(Type type)
        => type.IsGenericTypeDefinition ? typeof(IDefault<>).MakeGenericType(type) : type;

    public static IDefaultProvider GetDefaultDefaultProvider()
        => new DefaultDefaultProvider();
}
