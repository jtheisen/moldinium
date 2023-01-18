using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMinus;

public class NoElementException : InvalidOperationException
{
    public NoElementException(String message, Exception inner)
        : base(message, inner)
    {
    }
}

public class MultipleElementsException : InvalidOperationException
{
    public MultipleElementsException(String message, Exception inner)
        : base(message, inner)
    {
    }
}

public static class Extensions
{
    public static T Return<T>(this Object _, T value) => value;

    [DebuggerHidden]
    public static T Single<T>(this IEnumerable<T> source, String error)
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
    public static T? SingleOrDefault<T>(this IEnumerable<T> source, String error)
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
    public static Task<T> Ensure<T>(this Task<T> task)
        => task ?? Task.FromResult<T>(default);

    [DebuggerHidden]
    public static T Apply<T>(this T source, Boolean indeed, Func<T, T> func)
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

    public static T? If<T>(this T source, Boolean predicate)
        where T : struct
        => predicate ? source : default;

    public static async void Ignore(this Task task)
    {
        await task;
    }

    public static void Ignore(this ValueTask _) { }

    [DebuggerHidden]
    public static T Assert<T>(this T value, Predicate<T> predicate, String? message = null)
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
