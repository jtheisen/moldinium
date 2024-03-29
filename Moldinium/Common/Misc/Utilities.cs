﻿using Moldinium.Common.Misc;

namespace Moldinium.Utilities;

public class NoElementException : InvalidOperationException
{
    public NoElementException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

public class MultipleElementsException : InvalidOperationException
{
    public MultipleElementsException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

public class InternalErrorException : Exception
{
    public InternalErrorException()
        : base("An internal error occured")
    {
    }

    public InternalErrorException(string message)
        : base($"Internal error: {message}")
    {
    }

    public InternalErrorException(string message, Exception inner)
        : base($"Internal error: {message}", inner)
    {
    }
}

public static partial class Extensions
{
    public static T Return<T>(this object _, T value) => value;

    public static T BreakIf<T>(this T value, bool condition)
    {
        if (condition)
        {
            Debugger.Break();
        }

        return value;
    }

    [DebuggerHidden]
    public static T Single<T>(this IEnumerable<T> source, string error)
    {
        try
        {
            return source.Single();
        }
        catch (InvalidOperationException ex)
        {
            var isNonEmpty = source.Select(x => true).FirstOrDefault();
            if (isNonEmpty) throw new MultipleElementsException(error, ex); else throw new NoElementException(error, ex);
        }
    }

    [DebuggerHidden]
    public static T? SingleOrDefault<T>(this IEnumerable<T> source, string error)
    {
        try
        {
            return source.SingleOrDefault();
        }
        catch (InvalidOperationException ex)
        {
            throw new MultipleElementsException(error, ex);
        }
    }

    [DebuggerHidden]
    public static void Apply<S>(this S source, Action<S> func)
    => func(source);

    [DebuggerHidden]
    public static T Apply<S, T>(this S source, Func<S, T> func)
        => func(source);

    [DebuggerHidden]
    public static T Apply<T>(this T source, bool indeed, Func<T, T> func)
        => indeed ? func(source) : source;

    [DebuggerHidden]
    public static T Apply<S, T>(this S s, Func<S, T> fun, Action<S> onException)
    {
        var haveReachedEnd = false;
        try
        {
            var temp = fun(s);
            haveReachedEnd = true;
            return temp;
        }
        finally
        {
            if (!haveReachedEnd)
            {
                onException(s);
            }
        }
    }

    public static T? If<T>(this T source, bool predicate)
        where T : struct
        => predicate ? source : default;

    public static async void Ignore(this Task task)
    {
        await task;
    }

    public static void Ignore(this ValueTask _) { }

    [DebuggerHidden]
    public static T Assert<T>(this T value, Predicate<T> predicate, string? message = null)
    {
        if (!predicate(value))
        {
            if (message is not null)
            {
                throw new Exception(message);
            }
            else
            {
                throw new Exception();
            }
        }

        return value;
    }
}
