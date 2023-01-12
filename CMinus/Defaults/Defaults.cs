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

public static class Defaults
{
    public static Type CreateDefaultInterfaceType(Type type)
        => typeof(IDefault<>).MakeGenericType(type);
}
