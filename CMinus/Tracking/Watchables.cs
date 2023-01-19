using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Xml.Linq;

namespace CMinus;

interface IWatchSubscription : IDisposable
{
    IEnumerable<IWatchable> Dependencies { get; }
}

interface IWatchable
{
    IWatchSubscription Subscribe(Object? agent, Action watcher);
}

interface IWatchableValue : IWatchable
{
    Object UntypedValue { get; }

    Type Type { get; }
}

interface IWatchable<out T> : IWatchableValue
{
    T Value { get; }
}

interface IWatchableVariable : IWatchable
{
    Object UntypedValue { get; set; }
}

interface IWatchableVariable<T> : IWatchable<T>
{
    new T Value { get; set; }
}

class ConcreteWatchable : IWatchable
{
    Action? watchers;
    Int32 noOfWatchers = 0;

    internal String Name { get; set; } = "<unnamed>";

    public IWatchSubscription Subscribe(Object? agent, Action watcher)
    {
        Repository.logger?.WriteLine($"{agent} subscribing to {this}");

        watchers += watcher;

        if (noOfWatchers++ == 0)
            Activate();

        return WatchSubscription.Create(this, () =>
        {
            Repository.logger?.WriteLine($"{agent} unsubscribing from {this}");

            watchers -= watcher;
            if (--noOfWatchers == 0)
                Relax();
        });
    }

    protected Boolean IsWatched => noOfWatchers > 0;

    protected virtual void Activate() { }

    protected virtual void Relax() { }

    public void Notify()
    {
        watchers?.Invoke();
    }

    public override string ToString()
    {
        return Name;
    }
}

abstract class WatchableValueBase : ConcreteWatchable, IWatchableValue
{
    public abstract Type Type { get; }

    Object IWatchableValue.UntypedValue
        => GetUntypedValue();

    Type IWatchableValue.Type => Type;

    protected abstract Object GetUntypedValue();
}

abstract class WatchableVariable : WatchableValueBase, IWatchableVariable
{
    public Object UntypedValue
    {
        get
        {
            return GetUntypedValue();
        }

        set
        {
            SetUntypedValue(value);
        }
    }

    protected abstract void SetUntypedValue(Object value);
}

class WatchableVariable<T> : WatchableVariable, IWatchableVariable<T>
{
    T value;

    public WatchableVariable()
    {
        this.value = default(T)!;
    }

    public WatchableVariable(T def)
    {
        this.value = def;
    }

    public T Value
    {
        get
        {
            Repository.logger?.WriteLine($"{this} returning its value, notifying its evaluation.");

            Repository.Instance.NoteEvaluation(this);

            return value;
        }
        set
        {
            Repository.logger?.WriteLine($"{this} setting its value, notifying its change.");

            this.value = value;

            Notify();
        }
    }

    protected override Object GetUntypedValue() => Value!;
    protected override void SetUntypedValue(Object value) => Value = (T)value;

    public override Type Type => typeof(T);
}

/// <summary>
/// Encapsulates an exception encountered on a previous evaluation that is now rethrown on a repeated get operation.
/// </summary>
/// <seealso cref="System.Exception" />
public class RethrowException : Exception
{
    internal RethrowException(Exception innerException)
        : base("The value evaluation threw the inner exception last time it was attempted, and the dependencies didn't change since.", innerException)
    {
    }
}

internal class CachedBeforeAndAfterComputedWatchable<T> : IWatchable
{
    Boolean dirty = true;

    T cache = default!;

    Exception? exception;

    SerialWatchSubscription? subscriptions = new SerialWatchSubscription();

    Boolean alert = false;

    IDisposable? loggingScope;

    Boolean isInSubscribingEvaluation;

    Int32 evaluationNestingLevel;

    Action? watchers;
    Int32 noOfWatchers = 0;

    internal String Name { get; set; } = "<unnamed>";

    public IWatchSubscription Subscribe(Object? agent, Action watcher)
    {
        Repository.logger?.WriteLine($"{agent} subscribing to {this}");

        watchers += watcher;

        if (noOfWatchers++ == 0)
            Activate();

        return WatchSubscription.Create(this, () =>
        {
            Repository.logger?.WriteLine($"{agent} unsubscribing from {this}");

            watchers -= watcher;
            if (--noOfWatchers == 0)
                Relax();
        });
    }

    protected Boolean IsWatched => noOfWatchers > 0;

    public Boolean BeforeGet(ref T value)
    {
        Repository.Instance.NoteEvaluation(this);

        var needEvaluation = dirty || !alert;

        if (needEvaluation)
        {
            if (evaluationNestingLevel > 0) throw new Exception("Attempting to enter a nested evaluation");

            if (IsWatched)
            {
                loggingScope = Repository.logger?.WriteLineWithScope($"{this} is watched and dirty, so we're evaluating watched.");

                Repository.Instance.BeginEvaluation(Name, evaluationNestingLevel++);

                isInSubscribingEvaluation = true;
            }
            else if (alert)
            {
                loggingScope = Repository.logger?.WriteLineWithScope($"{this} is dirty but unwatched, so we're evaluating unwatched.");
            }
            else
            {
                loggingScope = Repository.logger?.WriteLineWithScope($"{this} is not alert, so we're evaluating.");
            }
        }
        else if (exception is not null)
        {
            throw new RethrowException(exception);
        }
        else
        {
            value = cache;
        }

        return needEvaluation;
    }

    public void AfterGet(ref T value)
    {
        if (isInSubscribingEvaluation)
        {
            Repository.Instance.EndEvaluationAndSubscribe(Name, --evaluationNestingLevel, value, ref subscriptions, InvalidateAndNotify);

            isInSubscribingEvaluation = false;
        }

        loggingScope?.Dispose();

        loggingScope = null;

        cache = value;

        exception = null;

        dirty = false;
    }

    public Boolean AfterErrorGet(Exception exception)
    {
        if (isInSubscribingEvaluation)
        {
            Repository.Instance.EndEvaluationWithExceptionAndSubscribe<T>(Name, --evaluationNestingLevel, exception, ref subscriptions, InvalidateAndNotify);

            isInSubscribingEvaluation = false;
        }

        loggingScope?.Dispose();

        loggingScope = null;

        cache = default!;

        this.exception = exception;

        dirty = false;

        return true;
    }

    public void AfterSet() => InvalidateAndNotify();

    public void AfterErrorSet() => InvalidateAndNotify();

    protected void Activate()
    {
        Repository.logger?.WriteLine($"{this} is activated, let's look what we missed.");

        dirty = true;
        alert = true;
    }

    protected void Relax()
    {
        alert = false;

        if (subscriptions != null)
        {
            Repository.logger?.WriteLine($"{this} is relaxing.");

            subscriptions.Subscription = null;
        }
    }

    void Notify()
    {
        watchers?.Invoke();
    }

    public void InvalidateAndNotify()
    {
        dirty = true;
        if (alert)
        {
            Notify();
        }
    }
}

class CachedComputedWatchable<T> : WatchableValueBase, IWatchable<T>
{
    Func<T> evaluation;

    Action invalidateAndNotify;

    Boolean dirty = true;

    T value;

    Exception? exception;

    SerialWatchSubscription? subscriptions = new SerialWatchSubscription();

    Boolean alert = false;

    public CachedComputedWatchable(Func<T> evaluation)
    {
        value = default!;
        this.evaluation = evaluation;
        this.invalidateAndNotify = InvalidateAndNotify;
    }

    public T Value
    {
        get
        {
            EnsureUpdated(rethrow: true);

            if (null != exception)
                throw new RethrowException(exception);
            else
                return value;
        }
    }

    void EnsureUpdated(Boolean rethrow)
    {
        Repository.Instance.NoteEvaluation(this);

        if (dirty || !alert)
        {
            try
            {
                if (IsWatched)
                {
                    using (Repository.logger?.WriteLineWithScope($"{this} is watched and dirty, so we're evaluating watched."))
                        value = Repository.Instance.EvaluateAndSubscribe(
                            Name, ref subscriptions, evaluation, invalidateAndNotify);
                }
                else if (alert)
                {
                    using (Repository.logger?.WriteLineWithScope($"{this} is dirty but unwatched, so we're evaluating unwatched."))
                        value = evaluation();
                }
                else
                {
                    using (Repository.logger?.WriteLineWithScope($"{this} is not alert, so we're evaluating."))
                        value = evaluation();
                }

                exception = null;

                dirty = false;
            }
            catch (Exception ex)
            {
                value = default!;

                exception = ex;

                dirty = false;

                if (rethrow) throw;
            }
        }
    }

    protected override void Activate()
    {
        Repository.logger?.WriteLine($"{this} is activated, let's look what we missed.");

        dirty = true;
        alert = true;

        //EnsureUpdated(rethrow: false);
    }

    protected override void Relax()
    {
        alert = false;

        if (subscriptions != null)
        {
            Repository.logger?.WriteLine($"{this} is relaxing.");

            subscriptions.Subscription = null;
        }
    }

    public override Type Type => typeof(T);

    protected override Object GetUntypedValue() => Value!;

    public void InvalidateAndNotify()
    {
        dirty = true;
        if (alert)
        {
            //using (Repository.logger?.WriteLineWithScope($"{this} is invalidated, so we're re-evaluating."))
            //    EnsureUpdated(rethrow: false);
            Notify();
        }
    }
}

public interface IReaction<T> : IDisposable
{
    T Value { get; }
}

class Reaction<T> : IReaction<T>
{
    readonly Action action;
    private readonly Func<T> evaluation;
    readonly String name;

    Action invalidateAndNotify;

    Boolean dirty = true;

    Exception? exception;

    SerialWatchSubscription? subscriptions;

    public T Value { get; private set; } = default!;

    public Reaction(Func<T> action, String? name = null)
        : this(null, action, name)
    {

    }

    public Reaction(Action? action, Func<T> evaluation, String? name = null)
    {
        this.action = action ?? EnsureRun;
        this.evaluation = evaluation;
        this.name = name ?? "<unnamed>";
        this.invalidateAndNotify = InvalidateAndNotify;

        EnsureRun();
    }

    void EnsureRun()
    {
        try
        {
            if (!dirty) return;

            Value = Repository.Instance.EvaluateAndSubscribe(
                this, ref subscriptions, evaluation, invalidateAndNotify);

            exception = null;
        }
        catch (Exception ex)
        {
            exception = ex;
        }
    }

    void InvalidateAndNotify()
    {
        action();
        dirty = true;
    }

    public void Dispose()
    {
        if (subscriptions != null)
            subscriptions.Subscription = null;
    }
}

class Reaction : Reaction<Object?>
{
    public Reaction(Action action, Action evaluation, String? name = null)
        : base(action, () => { evaluation(); return null; }, name)
    { }

    public Reaction(Action action, String? name = null)
        : base(() => { action(); return null; }, name)
    { }
}

interface IWatchablesLogger
{
    IDisposable WriteLineWithScope(String text);
    void WriteLine(String text);

    void BeginEvaluationFrame(Object evaluator);
    void CloseEvaluationFrameWithResult(Object result, IEnumerable<IWatchable> dependencies);
    void CloseEvaluationFrameWithException(Exception ex, IEnumerable<IWatchable> dependencies);
}

class WatchablesLogger : IWatchablesLogger
{
    Int32 nesting = 0;

    Stack<Object> evaluators = new Stack<Object>();

    public IDisposable WriteLineWithScope(String text)
    {
        WriteLine(text);
        ++nesting;
        return new ActionDisposable(() => --nesting);
    }

    public void WriteLine(String text)
    {
        System.Diagnostics.Debug.WriteLine(new string(' ', evaluators.Count * 2) + text);
    }

    public void BeginEvaluationFrame(object evaluator)
    {
        WriteLine($"Evaluating [{evaluator}]");

        evaluators.Push(evaluator);
    }

    public void CloseEvaluationFrameWithResult(object result, IEnumerable<IWatchable> dependencies)
    {
        var evaluator = evaluators.Pop();

        WriteLine($"Evaluating [{evaluator}] completed with ({result}), now listening to [{String.Join(", ", dependencies)}].");
    }

    public void CloseEvaluationFrameWithException(Exception ex, IEnumerable<IWatchable> dependencies)
    {
        var evaluator = evaluators.Pop();

        WriteLine($"Evaluating [{evaluator}] completed with {ex.GetType().Name}, now listening to [{String.Join(", ", dependencies)}].");
    }
}


class Repository
{
    public static Repository Instance { get { return instance.Value; } }

    static Lazy<Repository> instance = new Lazy<Repository>(() => new Repository());

    public static IWatchablesLogger logger = new WatchablesLogger();

    Repository()
    {
        evaluationStack.Push(new EvaluationRecord());
    }

    class EvaluationRecord
    {
        internal Object? evaluator;
        internal Int32 id;
        internal List<IWatchable> evaluatedWatchables = new List<IWatchable>();
    }

    Stack<EvaluationRecord> evaluationStack = new Stack<EvaluationRecord>();

    TSource Evaluate<TSource>(Object evaluator, Func<TSource> evaluation, out IEnumerable<IWatchable> dependencies)
    {
        logger?.BeginEvaluationFrame(evaluator);

        evaluationStack.Push(new EvaluationRecord { evaluator = evaluator });

        try
        {
            var result = evaluation();

            dependencies = evaluationStack.Pop().evaluatedWatchables;

            logger?.CloseEvaluationFrameWithResult(result!, dependencies);

            return result;
        }
        catch (Exception ex)
        {
            dependencies = evaluationStack.Pop().evaluatedWatchables;

            logger?.CloseEvaluationFrameWithException(ex, dependencies);

            throw;
        }
    }

    TSource Evaluate<TSource, TContext>(Object evaluator, Func<TContext, TSource> evaluation, TContext context, out IEnumerable<IWatchable> dependencies)
    {
        logger?.BeginEvaluationFrame(evaluator);

        evaluationStack.Push(new EvaluationRecord());

        try
        {
            var result =  evaluation(context);

            dependencies = evaluationStack.Pop().evaluatedWatchables;

            logger?.CloseEvaluationFrameWithResult(result!, dependencies);

            return result;
        }
        catch (Exception ex)
        {
            dependencies = evaluationStack.Pop().evaluatedWatchables;

            logger?.CloseEvaluationFrameWithException(ex, dependencies);

            throw;
        }
    }

    internal void BeginEvaluation(Object evaluator, Int32 id)
    {
        logger?.BeginEvaluationFrame(evaluator);

        evaluationStack.Push(new EvaluationRecord { evaluator = evaluator, id = id });
    }

    void EndEvaluation<TSource>(Object evaluator, Int32 id, TSource result, out IEnumerable<IWatchable> dependencies)
    {
        var frame = evaluationStack.Pop();

        if (!Object.ReferenceEquals(evaluator, frame.evaluator) || id != frame.id)
        {
            throw new Exception($"EndEvaluation didn't match the current frame");
        }

        dependencies = frame.evaluatedWatchables;

        logger?.CloseEvaluationFrameWithResult(result!, frame.evaluatedWatchables);
    }

    void EndEvaluationWithException<TSource>(Object evaluator, Int32 id, Exception exception, out IEnumerable<IWatchable> dependencies)
    {
        var frame = evaluationStack.Pop();

        if (!Object.ReferenceEquals(evaluator, frame.evaluator) || id != frame.id)
        {
            throw new Exception($"EndEvaluation didn't match the current frame");
        }

        dependencies = frame.evaluatedWatchables;

        logger?.CloseEvaluationFrameWithException(exception, frame.evaluatedWatchables);
    }

    internal void EndEvaluationAndSubscribe<TSource>(Object evaluator, Int32 id, TSource result, ref SerialWatchSubscription? subscriptions, Action onChange)
    {
        EndEvaluation<TSource>(evaluator, id, result, out var dependecies);

        SubscribeAll(evaluator, ref subscriptions, dependecies, onChange);
    }

    internal void EndEvaluationWithExceptionAndSubscribe<TSource>(Object evaluator, Int32 id, Exception exception, ref SerialWatchSubscription? subscriptions, Action onChange)
    {
        EndEvaluationWithException<TSource>(evaluator, id, exception, out var dependecies);

        SubscribeAll(evaluator, ref subscriptions, dependecies, onChange);
    }

    internal TResult EvaluateAndSubscribe<TResult>(Object evaluator, ref SerialWatchSubscription? subscriptions, Func<TResult> evaluation, Action onChange)
    {
        if (onChange == null) throw new ArgumentException(nameof(onChange));

        IEnumerable<IWatchable>? dependencies = null;

        try
        {
            var result = Evaluate(evaluator, evaluation, out dependencies);

            SubscribeAll(evaluator, ref subscriptions, dependencies, onChange);

            return result;
        }
        catch (Exception)
        {
            SubscribeAll(evaluator, ref subscriptions, dependencies, onChange);

            throw;
        }
    }

    internal TResult EvaluateAndSubscribe<TResult, TContext>(Object evaluator, ref SerialWatchSubscription? subscriptions, Func<TContext, TResult> evaluation, TContext context, Action onChange)
    {
        if (onChange == null) throw new ArgumentException(nameof(onChange));

        IEnumerable<IWatchable>? dependencies = null;

        try
        {
            var result = Evaluate(evaluator, evaluation, context, out dependencies);

            SubscribeAll(evaluator, ref subscriptions, dependencies, onChange);

            return result;
        }
        catch (Exception)
        {
            SubscribeAll(evaluator, ref subscriptions, dependencies, onChange);

            throw;
        }
    }

    void SubscribeAll(Object evaluator, ref SerialWatchSubscription? subscriptions, IEnumerable<IWatchable>? dependencies, Action onChange)
    {
        if (dependencies != null)
        {
            if (subscriptions == null)
                subscriptions = new SerialWatchSubscription();

            subscriptions.Subscription = new CompositeWatchSubscription(
                from d in dependencies select d.Subscribe(evaluator, onChange));
        }
        else if (subscriptions != null)
        {
            subscriptions.Subscription = null;
        }
    }

    internal void NoteEvaluation(IWatchable watchable)
    {
        evaluationStack.Peek().evaluatedWatchables.Add(watchable);
    }
}

/// <summary>
/// Represents a watchable variable that can be written to and read from. It can participate in automatic dependency tracking.
/// </summary>
/// <typeparam name="T">The type of the variable.</typeparam>
public struct Var<T>
{
    IWatchableVariable<T> watchable;

    internal Var(IWatchableVariable<T> watchable)
    {
        this.watchable = watchable;
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Var{T}"/> to <see cref="Eval{T}"/>.
    /// </summary>
    /// <param name="var">The variable.</param>
    /// <returns>
    /// The result of the conversion.
    /// </returns>
    public static implicit operator Eval<T>(Var<T> var)
        => new Eval<T>(var.watchable);

    /// <summary>
    /// Gets or sets the watchable variable's value.
    /// </summary>
    /// <value>
    /// The value of the watchable variable. Neither the setter nor the getter will ever throw.
    /// </value>
    public T Value
    {
        get { return watchable.Value; }
        set { watchable.Value = value; }
    }
}

/// <summary>
/// Represents a watchable evaluation that can be read from. It can participate in automatic dependency tracking.
/// </summary>
/// <typeparam name="T">The type.</typeparam>
/// <remarks>
/// The evaluation happens only once initially and each time after a dependency changes,
/// each time on first read. On other accesses to <see cref="Value"/>, a cached value is returned.
/// If the evaluation throws the exception is not caught and will fall through to the read of <see cref="Value"/>.
/// Such an exception is also cached and subsequent reads before dependencies change will receive a
/// <see cref="RethrowException"/> with the old exception as the <see cref="Exception.InnerException"/>.
/// From the point of dependency tracking, exceptions are just another "return value".
/// </remarks>
public struct Eval<T>
{
    internal readonly IWatchable<T> watchable;

    internal Eval(IWatchable<T> watchable)
    {
        this.watchable = watchable;
    }

    /// <summary>
    /// Gets the value of the watchable evaluation.
    /// </summary>
    /// <value>
    /// The value that the evaluation represents. This will throw if the evaluation throws.
    /// </value>
    public T Value => watchable.Value;

    public IDisposable Subscribe(Action? watcher = null, Object? name = null) => watchable.Subscribe(name, watcher ?? (() => { }));
}

/// <summary>
/// Provides a set of factory methods for watchables.
/// </summary>
public static class Watchable
{
    /// <summary>
    /// Creates a watchable variable.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="def">The initial value.</param>
    /// <returns>The new watchable variable.</returns>
    public static Var<T> Var<T>(T def = default!)
        => new Var<T>(new WatchableVariable<T>(def));

    /// <summary>
    /// Creates a watchable variable.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="def">The initial value.</param>
    /// <returns>The new watchable variable.</returns>
    public static Var<T> Var<T>(String name, T def = default!)
        => new Var<T>(new WatchableVariable<T>(def) { Name = name });

    internal static IWatchableVariable VarForType(Type type)
        => (WatchableVariable)Activator.CreateInstance(
            typeof(WatchableVariable<>).MakeGenericType(type))!;

    /// <summary>
    /// Creates a watchable evaluation.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="evaluation">The function to evaluate.</param>
    /// <returns>The new watchable evaluation.</returns>
    public static Eval<T> Eval<T>(Func<T> evaluation)
        => new Eval<T>(new CachedComputedWatchable<T>(evaluation));

    /// <summary>
    /// Creates a watchable evaluation.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="evaluation">The function to evaluate.</param>
    /// <returns>The new watchable evaluation.</returns>
    public static Eval<T> Eval<T>(String name, Func<T> evaluation)
        => new Eval<T>(new CachedComputedWatchable<T>(evaluation) { Name = name });

    /// <summary>
    /// Creates a reaction.
    /// </summary>
    /// <param name="action">The action that will run on changes to dependencies.</param>
    /// <returns>A handle to the new reaction.</returns>
    public static IDisposable React(Action action)
        => new Reaction(action);

    /// <summary>
    /// Creates a reaction.
    /// </summary>
    /// <param name="action">The action that will run on changes to dependencies.</param>
    /// <returns>A handle to the new reaction.</returns>
    public static IReaction<T> React<T>(Func<T> action)
        => new Reaction<T>(action);

    /// <summary>
    /// Creates a reaction.
    /// </summary>
    /// <param name="action">The action that will run on changes to dependencies.</param>
    /// <returns>A handle to the new reaction.</returns>
    public static IDisposable React(Action evaluation, Action reaction)
        => new Reaction(reaction, evaluation);

    /// <summary>
    /// Creates a reaction.
    /// </summary>
    /// <param name="action">The action that will run on changes to dependencies.</param>
    /// <returns>A handle to the new reaction.</returns>
    public static IReaction<T> React<T>(Func<T> evaluation, Action reaction)
        => new Reaction<T>(reaction, evaluation);

}
