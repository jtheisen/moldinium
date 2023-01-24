using System;
using System.Collections.Generic;
using System.Linq;

namespace Moldinium;

interface ITrackSubscription : IDisposable
{
    IEnumerable<ITrackable> Dependencies { get; }
}

interface ITrackable
{
    ITrackSubscription Subscribe(Object? agent, Action tracker);
}

interface ITrackableValue : ITrackable
{
    Object UntypedValue { get; }

    Type Type { get; }
}

interface ITrackable<out T> : ITrackableValue
{
    T Value { get; }
}

interface ITrackableVariable : ITrackable
{
    Object UntypedValue { get; set; }
}

interface ITrackableVariable<T> : ITrackable<T>
{
    new T Value { get; set; }
}

class ConcreteTrackable : ITrackable
{
    Action? trackers;
    Int32 noOfTrackers = 0;

    internal String Name { get; set; } = "<unnamed>";

    public ITrackSubscription Subscribe(Object? agent, Action tracker)
    {
        Repository.logger?.WriteLine($"{agent} subscribing to {this}");

        trackers += tracker;

        if (noOfTrackers++ == 0)
            Activate();

        return TrackSubscription.Create(this, () =>
        {
            Repository.logger?.WriteLine($"{agent} unsubscribing from {this}");

            trackers -= tracker;
            if (--noOfTrackers == 0)
                Relax();
        });
    }

    protected Boolean IsTracked => noOfTrackers > 0;

    protected virtual void Activate() { }

    protected virtual void Relax() { }

    public void Notify()
    {
        trackers?.Invoke();
    }

    public override string ToString()
    {
        return Name;
    }
}

abstract class TrackableValueBase : ConcreteTrackable, ITrackableValue
{
    public abstract Type Type { get; }

    Object ITrackableValue.UntypedValue
        => GetUntypedValue();

    Type ITrackableValue.Type => Type;

    protected abstract Object GetUntypedValue();
}

abstract class TrackableVariable : TrackableValueBase, ITrackableVariable
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

class TrackableVariable<T> : TrackableVariable, ITrackableVariable<T>
{
    T value;

    public TrackableVariable()
    {
        this.value = default(T)!;
    }

    public TrackableVariable(T def)
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

internal class CachedBeforeAndAfterComputedTrackable<T> : ITrackable
{
    Boolean dirty = true;

    T cache = default!;

    Exception? exception;

    SerialTrackSubscription? subscriptions = new SerialTrackSubscription();

    Boolean alert = false;

    IDisposable? loggingScope;

    Boolean isInSubscribingEvaluation;

    Int32 evaluationNestingLevel;

    Action? trackers;
    Int32 noOfTrackers = 0;

    internal String Name { get; set; } = "<unnamed>";

    public ITrackSubscription Subscribe(Object? agent, Action tracker)
    {
        Repository.logger?.WriteLine($"{agent} subscribing to {this}");

        trackers += tracker;

        if (noOfTrackers++ == 0)
            Activate();

        return TrackSubscription.Create(this, () =>
        {
            Repository.logger?.WriteLine($"{agent} unsubscribing from {this}");

            trackers -= tracker;
            if (--noOfTrackers == 0)
                Relax();
        });
    }

    protected Boolean IsTracked => noOfTrackers > 0;

    public Boolean BeforeGet(ref T value)
    {
        Repository.Instance.NoteEvaluation(this);

        var needEvaluation = dirty || !alert;

        if (needEvaluation)
        {
            if (evaluationNestingLevel > 0) throw new Exception("Attempting to enter a nested evaluation");

            if (IsTracked)
            {
                loggingScope = Repository.logger?.WriteLineWithScope($"{this} is tracked and dirty, so we're evaluating tracked.");

                Repository.Instance.BeginEvaluation(Name, evaluationNestingLevel++);

                isInSubscribingEvaluation = true;
            }
            else if (alert)
            {
                loggingScope = Repository.logger?.WriteLineWithScope($"{this} is dirty but untracked, so we're evaluating untracked.");
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

    Action MakeNotify(Action? notify)
    {
        if (notify is null) return InvalidateAndNotify;

        void Notify()
        {
            try
            {
                notify();
            }
            catch
            {
                // FIXME
            }

            InvalidateAndNotify();
        }

        return Notify;
    }

    public void AfterGet(ref T value, Action? notify = null)
    {
        if (isInSubscribingEvaluation)
        {
            var combinedNotify = MakeNotify(notify);

            Repository.Instance.EndEvaluationAndSubscribe(Name, --evaluationNestingLevel, value, ref subscriptions, combinedNotify);

            isInSubscribingEvaluation = false;
        }

        loggingScope?.Dispose();

        loggingScope = null;

        cache = value;

        exception = null;

        dirty = false;
    }

    public Boolean AfterErrorGet(Exception exception, Action? notify = null)
    {
        if (isInSubscribingEvaluation)
        {
            var combinedNotify = MakeNotify(notify);

            Repository.Instance.EndEvaluationWithExceptionAndSubscribe<T>(Name, --evaluationNestingLevel, exception, ref subscriptions, combinedNotify);

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
        trackers?.Invoke();
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

class CachedComputedTrackable<T> : TrackableValueBase, ITrackable<T>
{
    Func<T> evaluation;

    Action invalidateAndNotify;

    Boolean dirty = true;

    T value;

    Exception? exception;

    SerialTrackSubscription? subscriptions = new SerialTrackSubscription();

    Boolean alert = false;

    public CachedComputedTrackable(Func<T> evaluation)
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
                if (IsTracked)
                {
                    using (Repository.logger?.WriteLineWithScope($"{this} is tracked and dirty, so we're evaluating tracked."))
                        value = Repository.Instance.EvaluateAndSubscribe(
                            Name, ref subscriptions, evaluation, invalidateAndNotify);
                }
                else if (alert)
                {
                    using (Repository.logger?.WriteLineWithScope($"{this} is dirty but untracked, so we're evaluating untracked."))
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

public class TrackableList<T> : LiveList<T>
{
    ConcreteTrackable trackable = new ConcreteTrackable();

    public TrackableList() { }

    public TrackableList(Int32 capacity) : base(capacity) { }

    public TrackableList(IEnumerable<T> collection) : base(collection) { }

    protected override void OnEvaluated() => Repository.Instance.NoteEvaluation(trackable);

    protected override void OnModified() => trackable.Notify();

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

    SerialTrackSubscription? subscriptions;

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

interface ITrackablesLogger
{
    IDisposable WriteLineWithScope(String text);
    void WriteLine(String text);

    void BeginEvaluationFrame(Object evaluator);
    void CloseEvaluationFrameWithResult(Object result, IEnumerable<ITrackable> dependencies);
    void CloseEvaluationFrameWithException(Exception ex, IEnumerable<ITrackable> dependencies);
}

class TrackablesLogger : ITrackablesLogger
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

    public void CloseEvaluationFrameWithResult(object result, IEnumerable<ITrackable> dependencies)
    {
        var evaluator = evaluators.Pop();

        WriteLine($"Evaluating [{evaluator}] completed with ({result}), now listening to [{String.Join(", ", dependencies)}].");
    }

    public void CloseEvaluationFrameWithException(Exception ex, IEnumerable<ITrackable> dependencies)
    {
        var evaluator = evaluators.Pop();

        WriteLine($"Evaluating [{evaluator}] completed with {ex.GetType().Name}, now listening to [{String.Join(", ", dependencies)}].");
    }
}


class Repository
{
    public static Repository Instance { get { return instance.Value; } }

    static Lazy<Repository> instance = new Lazy<Repository>(() => new Repository());

    public static ITrackablesLogger? logger; // = new TrackablesLogger();

    Repository()
    {
        evaluationStack.Push(new EvaluationRecord());
    }

    class EvaluationRecord
    {
        internal Object? evaluator;
        internal Int32 id;
        internal List<ITrackable> evaluatedTrackables = new List<ITrackable>();
    }

    Stack<EvaluationRecord> evaluationStack = new Stack<EvaluationRecord>();

    TSource Evaluate<TSource>(Object evaluator, Func<TSource> evaluation, out IEnumerable<ITrackable> dependencies)
    {
        logger?.BeginEvaluationFrame(evaluator);

        evaluationStack.Push(new EvaluationRecord { evaluator = evaluator });

        try
        {
            var result = evaluation();

            dependencies = evaluationStack.Pop().evaluatedTrackables;

            logger?.CloseEvaluationFrameWithResult(result!, dependencies);

            return result;
        }
        catch (Exception ex)
        {
            dependencies = evaluationStack.Pop().evaluatedTrackables;

            logger?.CloseEvaluationFrameWithException(ex, dependencies);

            throw;
        }
    }

    TSource Evaluate<TSource, TContext>(Object evaluator, Func<TContext, TSource> evaluation, TContext context, out IEnumerable<ITrackable> dependencies)
    {
        logger?.BeginEvaluationFrame(evaluator);

        evaluationStack.Push(new EvaluationRecord());

        try
        {
            var result =  evaluation(context);

            dependencies = evaluationStack.Pop().evaluatedTrackables;

            logger?.CloseEvaluationFrameWithResult(result!, dependencies);

            return result;
        }
        catch (Exception ex)
        {
            dependencies = evaluationStack.Pop().evaluatedTrackables;

            logger?.CloseEvaluationFrameWithException(ex, dependencies);

            throw;
        }
    }

    internal void BeginEvaluation(Object evaluator, Int32 id)
    {
        logger?.BeginEvaluationFrame(evaluator);

        evaluationStack.Push(new EvaluationRecord { evaluator = evaluator, id = id });
    }

    void EndEvaluation<TSource>(Object evaluator, Int32 id, TSource result, out IEnumerable<ITrackable> dependencies)
    {
        var frame = evaluationStack.Pop();

        if (!Object.ReferenceEquals(evaluator, frame.evaluator) || id != frame.id)
        {
            throw new Exception($"EndEvaluation didn't match the current frame");
        }

        dependencies = frame.evaluatedTrackables;

        logger?.CloseEvaluationFrameWithResult(result!, frame.evaluatedTrackables);
    }

    void EndEvaluationWithException<TSource>(Object evaluator, Int32 id, Exception exception, out IEnumerable<ITrackable> dependencies)
    {
        var frame = evaluationStack.Pop();

        if (!Object.ReferenceEquals(evaluator, frame.evaluator) || id != frame.id)
        {
            throw new Exception($"EndEvaluation didn't match the current frame");
        }

        dependencies = frame.evaluatedTrackables;

        logger?.CloseEvaluationFrameWithException(exception, frame.evaluatedTrackables);
    }

    internal void EndEvaluationAndSubscribe<TSource>(Object evaluator, Int32 id, TSource result, ref SerialTrackSubscription? subscriptions, Action onChange)
    {
        EndEvaluation<TSource>(evaluator, id, result, out var dependecies);

        SubscribeAll(evaluator, ref subscriptions, dependecies, onChange);
    }

    internal void EndEvaluationWithExceptionAndSubscribe<TSource>(Object evaluator, Int32 id, Exception exception, ref SerialTrackSubscription? subscriptions, Action onChange)
    {
        EndEvaluationWithException<TSource>(evaluator, id, exception, out var dependecies);

        SubscribeAll(evaluator, ref subscriptions, dependecies, onChange);
    }

    internal TResult EvaluateAndSubscribe<TResult>(Object evaluator, ref SerialTrackSubscription? subscriptions, Func<TResult> evaluation, Action onChange)
    {
        if (onChange == null) throw new ArgumentException(nameof(onChange));

        IEnumerable<ITrackable>? dependencies = null;

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

    internal TResult EvaluateAndSubscribe<TResult, TContext>(Object evaluator, ref SerialTrackSubscription? subscriptions, Func<TContext, TResult> evaluation, TContext context, Action onChange)
    {
        if (onChange == null) throw new ArgumentException(nameof(onChange));

        IEnumerable<ITrackable>? dependencies = null;

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

    void SubscribeAll(Object evaluator, ref SerialTrackSubscription? subscriptions, IEnumerable<ITrackable>? dependencies, Action onChange)
    {
        if (dependencies != null)
        {
            if (subscriptions == null)
                subscriptions = new SerialTrackSubscription();

            subscriptions.Subscription = new CompositeTrackSubscription(
                from d in dependencies select d.Subscribe(evaluator, onChange));
        }
        else if (subscriptions != null)
        {
            subscriptions.Subscription = null;
        }
    }

    internal void NoteEvaluation(ITrackable trackable)
    {
        evaluationStack.Peek().evaluatedTrackables.Add(trackable);
    }
}

/// <summary>
/// Represents a trackable variable that can be written to and read from. It can participate in automatic dependency tracking.
/// </summary>
/// <typeparam name="T">The type of the variable.</typeparam>
public struct Var<T>
{
    ITrackableVariable<T> trackable;

    internal Var(ITrackableVariable<T> trackable)
    {
        this.trackable = trackable;
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Var{T}"/> to <see cref="Eval{T}"/>.
    /// </summary>
    /// <param name="var">The variable.</param>
    /// <returns>
    /// The result of the conversion.
    /// </returns>
    public static implicit operator Eval<T>(Var<T> var)
        => new Eval<T>(var.trackable);

    /// <summary>
    /// Gets or sets the trackable variable's value.
    /// </summary>
    /// <value>
    /// The value of the trackable variable. Neither the setter nor the getter will ever throw.
    /// </value>
    public T Value
    {
        get { return trackable.Value; }
        set { trackable.Value = value; }
    }
}

/// <summary>
/// Represents a trackable evaluation that can be read from. It can participate in automatic dependency tracking.
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
    internal readonly ITrackable<T> trackable;

    internal Eval(ITrackable<T> trackable)
    {
        this.trackable = trackable;
    }

    /// <summary>
    /// Gets the value of the trackable evaluation.
    /// </summary>
    /// <value>
    /// The value that the evaluation represents. This will throw if the evaluation throws.
    /// </value>
    public T Value => trackable.Value;

    public IDisposable Subscribe(Action? tracker = null, Object? name = null) => trackable.Subscribe(name, tracker ?? (() => { }));
}

/// <summary>
/// Provides a set of factory methods for trackables.
/// </summary>
public static class Trackable
{
    /// <summary>
    /// Creates a trackable variable.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="def">The initial value.</param>
    /// <returns>The new trackable variable.</returns>
    public static Var<T> Var<T>(T def = default!)
        => new Var<T>(new TrackableVariable<T>(def));

    /// <summary>
    /// Creates a trackable variable.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="def">The initial value.</param>
    /// <returns>The new trackable variable.</returns>
    public static Var<T> Var<T>(String name, T def = default!)
        => new Var<T>(new TrackableVariable<T>(def) { Name = name });

    internal static ITrackableVariable VarForType(Type type)
        => (TrackableVariable)Activator.CreateInstance(
            typeof(TrackableVariable<>).MakeGenericType(type))!;

    /// <summary>
    /// Creates a trackable evaluation.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="evaluation">The function to evaluate.</param>
    /// <returns>The new trackable evaluation.</returns>
    public static Eval<T> Eval<T>(Func<T> evaluation)
        => new Eval<T>(new CachedComputedTrackable<T>(evaluation));

    /// <summary>
    /// Creates a trackable evaluation.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="evaluation">The function to evaluate.</param>
    /// <returns>The new trackable evaluation.</returns>
    public static Eval<T> Eval<T>(String name, Func<T> evaluation)
        => new Eval<T>(new CachedComputedTrackable<T>(evaluation) { Name = name });

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
