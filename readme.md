# Moldinium

## Introduction

Moldinium allows you to write code in interfaces such as these:

```c#
interface Job
{
    CancellationToken Ct { get; init; }

    Boolean IsRunning { get; set; }

    Int32 Progress { get; set; }

    async Task Run() { /* ... */ }
}
```

and then creates class implementations for them at runtime
through *dynamic type creation*.

For xaml consumption, those can have `INotifyPropertyChanged` automatically
implemented, eg. to fire when `Progress` is set to a new value.

For Blazor consumption, those can have a different notification mechanism
implemented that allows Blazor components be written like this:

```c#
@inherits LocalComponentBase

<div class="progress" style="height: 4px;">
    <div class="progress-bar" role="progressbar" style="width: @Job.Progress%;"></div>
</div>

@code {
    [Parameter]
    public Job Job { get; set; } = null!;
}
```

And again, every time `Progress` changes, this Blazor component rerenders.
Take out the reference to `Progress` and it will no longer rerender.

The same magic that makes this possible is also improving on usage in the xaml
world: A job collection may be dependent on all the `IsRunning` fields of its
children:

```c#
interface JobCollection
{
    IList<Job> Jobs { get; set; }

    Boolean HaveRunningChildren => Jobs.Any(j => j.IsRunning);
}
```

In a xaml environment, the created class that implements `INotifyPropertyChanged`
will fire it for `HaveRunningChildren` the moment it needs to be reevaluated
due to changes of `IsRunning` in one of its children. In fact, the value of
`HaveRunningChildren` will be cached and multiple reads will retrieve the
cached value until a dependent value change invalidates this cache.

This is called *value dependency tracking* and is a form of state management
that has been implemented in the JavaScript world a number of times, but
there was none in .NET until now as far as I know.

Since you can't create interfaces with `new` and don't have access to the
implementations that will be created at runtime, Moldinium also provides
a simple *dependency injection* system that allows you to create these types:

```c#
interface JobCollection
{
    // Moldinium injects this delegate and calling it will get you a new
    // Job with the passed token set to its Ct property
    Func<CancellationToken, Job> NewJob { get; init; }

    // ...
}
```

Since interfaces don't have constructors, we use `init` setters to
mark dependencies.

## Rationale

It all started with *dependency tracking*, which I loved since
[KockoutJS](https://knockoutjs.com/). In JavaScript, it's also the concept
behind [MobX](https://mobx.js.org/) and [VueJS](https://vuejs.org/). It helps
significantly with UI development by shifting the burden of tracking
what needs updating in the presence of changing sources to a generic solution.

However, it requires that all access to trackable variables and computations
are wrapped by the dependency tracking framework. Without some form
of type creation or proxying this results in ugly application code. MobX,
for example, can use JavaScript proxies and needs React components be wrapped
by one of its framework facilities. KnockoutJS and VueJS hide this behind
their templating engines.

This is why I created the *dynamic type creation* part of Moldinium which
can bake this logic into the properties for the user and allow clean and
simple-looking application code. It looks even cleaner than MobX does in React.

Once dynamic type creation was in the picture, one needs dependency injection
to resolve the implementation types that are only present at runtime:
`new` no longer works.

Moldinium's dependency injection can perhaps be replaced by an existing one,
but I had some strong opinion on dependency injection anyway - see the
relevant notes on implementation below for more on that.

Then, since all properties need to be virtual to allow redefining, I wondered if
one wouldn't be better off with using only interfaces to write application code
right away.

## Sample application

There's a
[all-in-one-file](https://github.com/jtheisen/moldinium/blob/master/SampleApp/App.cs)
sample application using only interfaces and records. It comes in a
class library that doesn't depend on anything besides .NET, not even on
Moldinium: Dependency injection purists will rejoice.

This class library is used by three different projects that each
provide a UI for it: One for WPF, one for Blazor WebAssembly and a
stateless one using ASP.NET MVC.

All depend on Moldinium and ask it to instantiate the application.
The WPF one asks for tracking with notifying (through `INotifyPropertyChanged`),
while the Blazor one asks only for tracking. Instead, the Blazor application
has all its components derive indirectly from `MoldiniumComponentBase`
which makes the magic work (see the implementation notes about dependency tracking
below for details).

The ASP.NET one is content without any notification mechanism: The entities
will behave without any magic applied to it. This version of the sample app,
however, does something interesting with it's default collection to support
its multi-threaded environment, see the section below.

## Collections and default values

The Moldinium type creator also allows to ensure defaults to non-nullable
properties of certain types. Besides the common case of `String`s
(which are then defaulted to the empty string), this is mostly a
concern with collections.

Besides `INotifyPropertChanged`, there's also `INotifyCollectionChanged`
for collections, which xaml-based UIs want to see lest they either
don't update their list views or recreate all item components when
the whole collection instance changes.

Blazor is more forgiving in the latter case as it reconciles anyway,
but the former case still necessitates a collection type that can
be tracked.

.NET itself provides `ObservableCollection<>` which is only sufficient
if no dependency tracking is desired, but, unfortunately, it doesn't
implement `IList<>`.

Moldinium provides `LiveList`, which also implements
`INotifyCollectionChanged` but also implements `IList<>` and has a
derived implementation that can be tracked at well.

Since the ASP.NET sample app has to expect multiple threads accessing
the sample app concurrently, it provides its own implementation:
`ConcurrentList`, which is a very simple implementation that should
be thread-safe. (The sample app itself isn't really thread-safe, but it's
good enough for demonstration purposes.)

## Default configurations

The entry to your Moldinium-created types are extension methods such as
`ServiceCollection.AddSingletonMoldiniumRootModel<YourRootType>` which
take a configuration builder. On resolving the root type instance, it will
be instantiated with all the dependencies of the then-available
`IServiceProvider` also provided.

The most important option of the builder is to select a mode from which
the implementation of the created types as well as the defaults for properties
dervies.

There are four modes:

|                       | Basic     | Notifying only         | Tracking only     | Notif. + Track.   |
| --------------------- | --------- | ---------------------- | ----------------- | ----------------- |
| IList<> default       | List<>    | LiveList<>**           | LiveList<>        | LiveList<>        |
| ICollection<> default | List<>    | LiveList<>**           | LiveList<>        | LiveList<>        |
| Computations cached   | no        | no                     | yes               | yes               |
| Thread safe           | yes*      | yes*                   | no                | no                |
| Uerful for            | stateless work | very little       | Blazor          | XAML              |

*as long as your app is thread safe and you use thread safe implementations for `IList<>` and `ICollection<>`

**only because `ObservableCollection<>` doesn't implement `IList<>`

## Moldinium standard semantics for interface implenmentations

Here's an overview of Moldiniums standard semantics:

```c#
interface MyInterface
{
    // optional dependency
    ADependency1? OptionalDependency { get; init; }

    // required dependency
    ADependency2 RequiredDependency { get; init; }

    // A potentially trackable variable, defaults to "" because not nullable
    String Name { get; set; }

    // A potentially cached computation because property and implemented
    String UpperCaseName => Name.ToUpper();

    // Also a potentially cached computation but it would also invalidate on writing
    Int32 WritableComputation { get { /* ... */ } set { /* ... */ } }

    // Methods are never cached
    String GetUpperCaseName() => Name.ToUpper();
}
```

## Notes on implementation

These sections are not at all necessary to understand the usage
and behavior of Moldinium, but they are useful for understanding how
separate the three components are from each other.

### Dependency Tracking

Dependency tracking requires a common *tracking repository* and having
all *computations* (eg. the implementation of `HaveRunningChildren`
in the introduction) executed within an *evaluation scope* in that repository.

The repository executes the computation and when, during, the computation accesses a
*trackable* (eg. `IsRunning`), this trackable tells the repository about
it being read. After the computation is completed, the repository now
does not only know the result, but also which trackables have been
read - those are the ones the computation depends on: If one of those
change, the computation changes, ie. `HaveRunningChildren` invalidates.

In the current Moldinium implementation, the repository is a static
singleton. This is how it's done in JavaScript and while it's not an
issue there, in .NET it would be an issue for Blazor Server where
multiple circuits need to be separated. A future version should do this properly.

In any case, only one thread must be evaluating anything at a time
for each repository and that's a conceptual limitation. A future version
should guard against misuse here.

There are some further features that MobX does and Moldinium doesn't yet
do, as they are not strictly necessary for a proof-of-concept. Those
are [transactions](https://mobx.js.org/actions.html) and early recomputation
(computed - ie. implemented - properties re-evaluate their cache
even before anything tries to read them).

The former is mostly an optimization and the latter improves debugging.

### Dynamic Type Creation

Moldinium's type creator is called *the bakery*.

The bakery creates types using `System.Reflection.Emit` and does some
non-trivial CIL weaving.

It takes an interface type and creates a concrete type by implementing
and wrapping properties, events and methods with the help of
*implementation structs* that are baked into the new type. A simple
example is that for an unimplemented property using tracking without
notifying:

```c#
public struct TrackedPropertyImplementation<Value> : ITrackedPropertyImplementation<Value, TrackedPropertyMixin>
{
    TrackableVariable<Value> variable;

    public void Init(Value def) => variable = new TrackableVariable<Value>(def);

    public Value Get() => variable.Value;

    public void Set(Value value) => variable.Value = value;
}
```

The newly created type get's a field of this struct for each such property
and one field of type `TrackedPropertyMixin` (also a struct) shared by those
former fields.

As you see, the bakery itself therefore does not proxy anything and
the constructed type does not necessarily require additional heap
allocations except the one for itself.

As you also see, tracking does currently require additional allocations,
but that can be improved upon in later versions.

While the bakery is designed to easily allow custom implementations and
additional wrappers by providing such structs, it's currently not easy
to do so in combination with the ones needed for tracking and notifying.
This would obviously be very useful and may, again, come in a later version.

### Dependecy Injection

