*WARNING: this is a proof-of-concept and not ready for production*

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

## Moldinium standard semantics for interface implementations

Here's an overview of Moldiniums standard semantics:

```c#
interface MyInterface
{
    // A potentially trackable variable, defaults to "" because not nullable
    String Name { get; set; }

    // A potentially cached computation because property and implemented
    String UpperCaseName => Name.ToUpper();

    // Also a potentially cached computation but it would also invalidate on writing
    Int32 WritableComputation { get { /* ... */ } set { /* ... */ } }

    // Methods are never cached
    String GetUpperCaseName() => Name.ToUpper();

    // optional dependency
    ADependency1? OptionalDependency { get; init; }

    // required dependency, doesn't need a default either because it is
    // expected to be initialized
    ADependency2 RequiredDependency { get; init; }
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

Instead of monolithically doing just what is needed for this proof-of-concept,
the bakery is very configurable for a variety of purposes.

It creates a type by looking at a number of interfaces to implement and
separately considers properties, events and methods.

Each of these can in principle be requiring an implementation (if they are
not yet implemented by an interface) or a wrapper (if they are).

The implementations and wrappers then come in the form of
*implementation and wrapper structs* that are baked into the resulting
type as fields. Those structs are provided for each of the modes
talked about above. The following is a wrapper struct for wrapping
an implemented property to implement `INotifyPropertyChanged`: 

```c#
public struct NotifyingComputedPropertyImplementation<Value, Container>
    : INotifyingComputedPropertyImplementation<Value, Container, NotifyingPropertyMixin>
    where Container : class
{
    // Just to make the bakery happy
    public Boolean BeforeGet() => true;

    public void AfterSet(Container container, ref NotifyingPropertyMixin mixin)
    {
        mixin.NotifyPropertyChanged(container);
    }

    // Presumably the value hasn't changed if the setter threw;
    // the return value indicates that the exception should be rethrown
    public Boolean AfterErrorSet() => true;
}
```

([taken from here](https://github.com/jtheisen/moldinium/blob/master/Moldinium/MoldiniumImplementations/Notifying.cs))

The newly created type gets a field of this struct for each such property
and one field of type `NotifyingPropertyMixin` (also a struct) shared by those
former fields.

As you see, the bakery itself therefore does not proxy anything and
the constructed type does not necessarily require additional heap
allocations except the one for itself.

The interface the struct above derives from is defined like this:

```c#
public interface INotifyingComputedPropertyImplementation<
    [TypeKind(ImplementationTypeArgumentKind.Value)] Value,
    [TypeKind(ImplementationTypeArgumentKind.Container)] Container,
    [TypeKind(ImplementationTypeArgumentKind.Mixin)] Mixin
> : IPropertyWrapperImplementation
    where Container : class
{
    Boolean BeforeGet();

    void AfterSet(Container container, ref Mixin mixin);
    Boolean AfterErrorSet();
}
```

When the bakery receives the struct implementation or wrapper type for a property,
it analyzes this interface first to understand what the types on
the method are supposed to mean. In then creates code for the setter
of the wrapped property on the created type with CIL weaving. This code
calls `AfterSet` with the given parameters according to the interface definition.
The methods that implement the wrapping code, such as `AfterSet` must have some
pre-defined names, but their parameters can be any, as long they are
declared this way.

The moment when looking at this interface is also when the bakery realizes
that `NotifyingPropertyMixin` actually is a mixin (again because of the
type attribute) and that its interface (`INotifyPropertyChanged`)
should become an interface of the created type and all the properties,
event and methods of that interface also need implementing.

This design allows to define type creation with clear separation of
concerns and an easy way to provide additional custom wrappers and
implementations for properties.

Unfortunately multiple wrappers are not yet implemented, so it's
currently not easy to do so in combination with the ones needed for
tracking and notifying (unless you replicate their code in your
implementation and wrapper). This would obviously be very useful and
may, again, come in a later version.

### Dependecy Injection

[see also](https://github.com/jtheisen/moldinium/blob/master/nested-scopes-ioc-container.md)

## Outlook

### Internal Dependency Resolution, Entity Framework and Deserializers

Besides maturity, one of the main limitations of the current implementation
is that it can't be used with deserializers: Those do reflection themselves.
Upon trying to create an object for a property they will complain that they
don't know what type to use when that property is just an interface.

This could be solved by making the baked type implement the interfaces privately
and instead expose the properties with different types: The baked types.

Then, the only remaining issue is that these types need to be
default-constructible. They technically already are, but a deserializer will
not have their init setters initialized by using the correct dependency injection.

While model types used in these scenarios may not really need proper dependency
injection, there is one dependency that is needed: That of the tracking repository
(once it's no longer static).

To make this work, I'm eyeing *ambient storage*: .NET has the wonderful ability
to allow methods be called with an *call context* that can store stuff. It's like
thread-local storage, but works also accross asynchronous chains of execution.

Before calling the serializer, the dependency injection system would be installed
in ambient storage and the retrieved by the default constructor of a baked type
to do *internal dependency resolution*. This would allow dependency injection
to work in these scenarios and also make the baked type's instance have
the correct tracking repository.

Another issue is type hierarchies: Deserializers require a single-base
hierarchy for polymorphic deserliazation and, currently, baked types don't
derive from other baked types at all. This could be done though, but of
course requires the user to sometimes specify a unique base.

### Entity Framework

Entity Framework is similar in that it also wants to create it's entity types.
It has two additional problems:

* it wants the types to be 
The most annoying trouble arises with fluent configuration. I still think
it can be done by doing this:

```c#
using Outer = NamespaceWhereTheModelsLive;

namespace NamespaceWhereTheModelsLive;

// ...

public interface IMyInterfaceDbContext<
    EntityType1,
    EntityType1
>
    where EntityType1 : Outer.EntityType1
    where EntityType2 : Outer.EntityType2
{
    DbSet<EntityType1> Entities1 { get; set; }
    DbSet<EntityType2> Entities2 { get; set; }

    void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<EntityType1>(b => b.HasKey(e => e.Id));
    }
}
```

Moldinium could then provide an implementation for `IMyInterfaceDbContext`
simply by providing the correct arguments to the type parameters. The
result should be good enough for EF, but I haven't tried.


