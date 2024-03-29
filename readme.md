*WARNING: this is a proof-of-concept and not ready for production*

# Moldinium

![Moldinium2](https://user-images.githubusercontent.com/1516294/215204618-c7f1870c-a810-45e9-ad9f-5b8d5bda229e.gif)

*--- the Moldinium Sample App bound against different UI technologies*

The Blazor WebAssembly version of the sample app is [hosted here](https://red-hill-0c5d5c510.2.azurestaticapps.net/).

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
implemented that allows Blazor components to be written like this:

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

And again, every time `Progress` changes, this Blazor component re-renders.
Take out the reference to `Progress` and it will no longer re-render.

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
will fire it for `HaveRunningChildren` the moment it needs to be re-evaluated
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
for example, can use JavaScript proxies and needs React components to be wrapped
by one of its framework facilities. KnockoutJS and VueJS hide this behind
their templating engines.

This is why I created the *dynamic type creation* part of Moldinium which
can bake this logic into the properties for the user and allow clean and
simple-looking application code. It looks even cleaner than MobX does in React.

Once dynamic type creation was in the picture, one needs dependency injection
to resolve the implementation types that are only present at runtime:
`new` no longer works.

Moldinium's dependency injection can perhaps be replaced by an existing one,
but I had some strong opinions on dependency injection anyway - see the
relevant notes on implementation below for more on that.

Then, since all properties need to be virtual to allow redefining, I wondered if
one wouldn't be better off using only interfaces to write application code
right away.

## Sample application

There's a
[all-in-one-file](https://github.com/jtheisen/moldinium/blob/master/SampleApp/App.cs)
sample application using only interfaces and records. It comes in a
class library that doesn't depend on anything besides .NET, not even on
Moldinium: Dependency injection purists will rejoice.

This class library is used by three different projects which each
provide a UI for it: One for WPF, one for Blazor WebAssembly and a
stateless one using ASP.NET MVC.

All depend on Moldinium and ask it to instantiate the application.
The WPF one asks for tracking with notifying (through `INotifyPropertyChanged`),
while the Blazor one asks only for tracking. Instead, the Blazor application
has all its components derive indirectly from `MoldiniumComponentBase`
which makes the magic work (see the implementation notes about dependency tracking
below for details).

The ASP.NET one is content without any notification mechanism: The entities
will behave without any magic applied to them. This version of the sample app,
however, does something interesting with its default collection to support
its multi-threaded environment, see the section below.

## Collections and default values

The Moldinium type creator also allows ensuring defaults to non-nullable
properties of certain types. After the common case of `String`s
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

Moldinium provides `LiveList`, which implements
`INotifyCollectionChanged`, but also implements `IList<>` and has a
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
the implementations of the created types as well as the defaults for
collection types derive.

There are four modes:

|                       | Basic     | Notifying only         | Tracking only     | Notif. + Track.   |
| --------------------- | --------- | ---------------------- | ----------------- | ----------------- |
| IList<> default       | List<>    | LiveList<>**           | LiveList<>        | LiveList<>        |
| ICollection<> default | List<>    | ObservableCollction<>  | LiveList<>        | LiveList<>        |
| Computations cached   | no        | no                     | yes               | yes               |
| Thread-safe           | yes*      | yes*                   | no                | no                |
| Uerful for            | stateless work | very little       | Blazor          | XAML              |

*as long as your app is thread-safe and you use thread-safe implementations for `IList<>` and `ICollection<>`

**only because `ObservableCollection<>` doesn't implement `IList<>`

Moldinium gives created types the same name as their corresponding interface, but
they live in a runtime assembly with a name that shows them to be such created
types. For example, types from the ASP.NET Sample end up in an assembly named
`MoldiniumTypes.Basic.ConcurrentList`, showing what to expect from
the types therein.

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

    String Unimplemented { get; } // error: no implementation

    // Methods are never cached
    String GetUpperCaseName() => Name.ToUpper();

    // Optional dependency
    ADependency1? OptionalDependency { get; init; }

    // Essential dependency, doesn't need a default either because it
    // is expected to be initialized
    ADependency2 RequiredDependency { get; init; }
}
```

Note that we assume that this is the interface to create a type from,
ie. the *moldinium type*. Of course, a base interface can declare the
`Unimplemented` property and a derived interface can still have a class
type created if it implements this property.

## A new language?

One could interpret this new style of coding as being a "new language"
that has been "discovered within C#", somewhat like JSON was
discovered within JavaScript. The analogy isn't perfect as JSON
throws away the vast majority of JavaScript whereas this new language
throws away only a very small part (the classes and their constructors).

I couldn't think of a great name yet. Contenders are "c minus"
(which may already be taken) and "calm c".

The "new language" perspective gives some justification for breaking the
C# style guide of naming interfaces only with a prefixed "I". Instead,
we're prefixing interfaces with an "I" if they still play the role
of interfaces. If they play the role of concrete types, they lose the "I".

## Outlook

### Maturity and State of the Library

This library is a proof-of-concept and not ready for production. There's
a substantial amount of details that are not yet implemented properly
and what's there is likely quite buggy.

One example would be that while the dependency injection component
tells apart optional dependencies from essential (required) ones, it
does so only for init setters. A factory declaration like
`Func<Dep1, Dep2?, Foo>` will not allow you to pass null for the
second dependency at runtime. For this to work, some work
on nullability would have to be done, as the information about
nullability is stored in a complicated fashion and lives in unexpected
places
([see here](https://github.com/dotnet/roslyn/blob/main/docs/features/nullable-metadata.md)). For example, the one for the factory above would live on the property
where it's injected. A facility that should help with this,
`NullabilityInfoContext`, is not available in Blazor WebAssembly
(strangely it is when I run it locally - I noticed the problem only
after I deployed the sample to Azure).

Then there is "an icky part of the bakery" I talk about below
in the notes on implementation.

### What to expect

It's unlikely I can afford to bring this library to a really mature state,
let alone maintain it after that.

I document the in my opinion quite elegant design below in part
so that someone with more free time has something to build on in the
following sections.

Also note that after those, there are some notes on further features
I would regard as essential for practical use.

## Notes on implementation

### Separation of Concerns

The three components *baking* (type creation), *tracking* and *injecting*
do not depend on each other and could as well each have their own
library. The source layout is this:

- Common *-utilities usable by all three components*
- Components
  - Baking
  - Tracking
  - Injection
- Combined *-putting the three components together*

### Dependency Tracking

Dependency tracking requires a common *tracking repository* and having
all *computations* (eg. the implementation of `HaveRunningChildren`
in the introduction) executed within an *evaluation scope* in that repository.

The repository executes the computation and when, during, the computation accesses a
*trackable* (eg. `IsRunning`), this trackable tells the repository about
it being read. After the computation is completed, the repository now
does not only know the result, but also which trackables have been
read - those are the ones the computation depends on: If one of those
changes, the computation changes, ie. `HaveRunningChildren` invalidates.

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
CIL weaving.

Instead of monolithically doing just what is needed for this proof-of-concept,
the bakery is very configurable for a variety of purposes. In particular, it
does not know about the *mode* (basic, notifying, etc.) discussed above and
instead is configured by getting a default provider as well as a number
of *implementation and wrapper structs*.

It creates a type by looking at a number of interfaces to implement and
separately considers properties, events and methods. Each of these can
in principle be requiring an implementation (if they are
not yet implemented by an interface) or a wrapper (if they are).

The implementation and wrapper structs are then baked into the resulting
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
the constructed type does not necessarily require heap
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
its methods are supposed to mean. It then creates code for the getters and setters
of the wrapped property on the created type with CIL weaving. This code
calls them with the given parameters according to the interface definition.
The methods that implement the wrapping code, such as `AfterSet`, must have one
of some pre-defined names that imply their purpose, but their parameters can be
any, as long they are declared this way.

The moment when looking at this interface is also when the bakery realizes
that `NotifyingPropertyMixin` actually is a mixin (again because of the
type attribute) and that its interface (`INotifyPropertyChanged`)
should become an interface of the created type and all the properties,
events and methods of that interface also need implementing.

This design allows defining type creation with clear separation of
concerns and an easy way to provide additional custom wrappers and
implementations for properties.

Unfortunately, multiple wrappers are not yet implemented, so it's
currently not easy to do so in combination with the ones needed for
tracking and notifying (unless you replicate their code in your
implementation and wrapper). This would obviously be very useful and
may, again, come in a later version.

#### Details about the implementation and wrapper structs

The structs are expected to have a certain set of expected names. For
properties, an implementation is expected to have a single one for
each property method:

```c#
struct MyPropertyImplementation<..., T, ...> : ...
{
    public void Init(...);
    public T Get(...);
    public void Set(...);
}
```

The init method is used to set default values and is called from the
created type's constructor. Wrappers are more complex:

```c#
struct MyPropertyWrapper<...> : ...
    public Boolean BeforeGet(...);
    public void AfterGet(...);
    public Boolean AfterErrorGet(...);

    // same for the setter
```

The return value for `Before` methods indicates whether the wrapped method
should be called. A cache, for instance, may choose not to.

The return value for `AfterError` methods indicates whether the exception
should be rethrown.

For events, the picture is analogous, with `Add` and `Remove` instead of
`Get` and `Set`. For non-special methods there are only wrappers and the
methods are only called `Before`, `After` and `AfterErrror`.

Again, the method parameters themselves can be configured freely with
the interface definition the implementation or wrapper derives from. The
`ImplementationTypeArgumentKind`s that are supported are:

* `Value`: Property type (only property structs)
* `Handler`: Event handler type (only event structs)
* `Return`: Method return type (only method structs)
* `Exception`: Exception type for the `AfterError*` methods, should be constrained to `Sytem.Exception`
* `Container`: The baked type
* `Mixin`: A mixin

The first three are conceptually similar and must be taken by value
in implementations and by ref in wrappers.

Although this design would also allow multiple mixins to be used, this
isn't currently allowed.

While you can't debug the generated IL code, you can debug those implementation
and wrapper structs when they live in your own assemblies or you
disable "just my code".

#### An icky part of the bakery

Due to a limitation in .NET's reflection APIs I had to work around, there's
a messy part that is likely buggy and definitely unable to handle all
desired cases.

When a class method implements a base method, the reflection API allows
to extract this information with the `MethodInfo.BaseMethod` property.
Interfaces can now also implement other interface methods:

```c#
public interface IBaseInterface
{
    public String Name { get; }
}

public interface IImplementingInterface : IBaseInterface
{
    String IBaseInterface.Name => "foo";
}
```

As in the case of classes, the information appears in the disassembly
as an `override` declaration on the method body:

```
.method private hidebysig specialname virtual final 
        instance string  IBaseInterface.get_Name() cil managed
{
  .override IBaseInterface::get_Name
  // Code size       6 (0x6)
  .maxstack  8
  IL_0000:  ldstr      "foo"
  IL_0005:  ret
} // end of method IImplementingInterface::IBaseInterface.get_Name

```

However, there's no way to get at this information using reflection.

Moldinium works around this by collecting all the methods together that
have the same name and signature and then see if there's a unique
implementation. Since `SignatureHelper` is also not available on Blazor
WebAssembly there's a lot of code that is reproducing .NET runtime logic,
which is probably quite buggy.

The current implementation has also more limitations than it needs to, but
I'd rather see the .NET folk fix this little oversight in the reflection
APIs than work on this further.

#### You can't implement all baked classes in C#

Interestingly, the baked types can be of a form that you can't implement
purely in C# (without again using reflection helpers anyway).

In the example of the previous section, the `Name` getter may have to
be wrapped, ie. the baked type also has a `Name` getter that must call
down to `IImplementingInterface.Name`'s getter. At the same time, the
`Name` of the baked type must again implement `IBaseInterface.Name`.

This requires that the baked type's function must call
`IImplementingInterface.Name`'s getter *non-virtually*. (A virtual
call would be an infinite recursion.)

For classes, you can call a base's methods non-virtually in C# by using
`base.Name`, but for interfaces, there is no such thing (and there's
no unique base, so the syntax would have to be something like
`base(IImplementingInterface).Name` if it was to exist in C#).

On the CLR level, however, you can just call the method non-virtually
like any other.

(If it wasn't private, that is. In fact, the dynamic assembly has to
be also annotated with an undocumented `IgnoresAccessChecksToAttribute`
to allow calling those methods; another thing that had cost me days
to figure out.)

### Dependency Injection

#### Nested-scopes IoC Containers

Most IoC containers have three different modes of registering types in three
different modes: *singleton*, *scoped* and *transient*.

They also do lifetime management.

I think this design can be improved by thinking of these three modes as
*nested scopes* living in a tree, the root scope containing the *singleton*s and the *transient* object living in their own leave scopes.

Classical IoC containers offer those three modes mostly so that the dependencies
for each of those modes can be checked at container *validation time*,
which is usually when the program starts or when tests are run.
Another reason is so that code that creates new scopes don't
need a dependency on the IoC container. It follows that lifetime management
must be done by the container: if it creates the instances
on each level, it needs to dispose of them too.

If those two initial issues (dependency on the container and early validation)
are deemed irrelevant, one could just create a new container
at the time a nested scope is needed, inject new "singletons", and resolve again there.

Moldinium instead allows resolving factories that can be used to create subscopes
and provide new dependencies for them in a manner that still allows early validation.

Take the following example:

```c#
interface App
{
    Func<CancellationToken, Job> NewJob { get; init; }

    async Task Run(CancellationToken ct)
    {
        await using var job = NewJob(ct);

        await job.Run();
    }
}

interface Job : IAsyncDisposable
{
    Func<RequestConfig, Request> NewRequest { get; init; }

    async Task Run()
    {
        var request = NewRequest(new RequestConfig(new Uri("...")));

        await request.FetchContent();
    }

    // Our job needs this for mysterious reasons
    async ValueTask IAsyncDisposable.DisposeAsync() { /* ... */ }
}

record RequestConfig(Uri Url);

interface Request
{
    RequestConfig Config { get; init; }

    CancellationToken Ct { get; init; }

    HttpClient HttpClient { get; init; }

    async Task FetchContent() { /* ... */ }
}
```

The top scope `App` is resolved by the application setup code, likely in `Program.cs`.

First Moldinium validates the dependencies:

- `App` needs a factory that creates `Job`s but for those
  it provides `CancellationToken`s
- `Job` needs a factory that creates `Request`s, but for those it provides `RequestConfig`s
- `Request` gets a `CancellationToken` from two scopes up and now has all except `HttpClient`, which we assume was provided by the root setup

By making the factories be typed on the dependencies that will be provided,
Moldinium can check all that at validation time before any instances are created.

I also see no reason for lifetime management to be part of the container with
this design: All object creation is done only when calling factory functions, so
it's now the *user* that creates. The user should then dispose as well.

This simplifies the matter considerably: Lifetime management is far from trivial, especially when you consider `IAsyncDisposable` and exception handling during cleanup. That can now be done in the usual C# fashion using `usings` and `await usings` and debugged accordingly.

#### The Dependency Report

Moldinium can give a dependency report. For the example above that is:

```
    - Bakery resolved fei`App
      - InitSetter resolved feb`App
        - Activator resolved veb`App
          - AcceptRootType resolved teb`App
        - Factory resolved foc`Func<CancellationTokenJob>
    
    - subscope for foc`Func<CancellationTokenJob>
      - Bakery resolved fei`Job
        - InitSetter resolved feb`Job
          - Activator resolved veb`Job
            - AcceptRootType resolved teb`Job
          - Factory resolved foc`Func<RequestConfigRequest>
    
      - subscope for foc`Func<RequestConfigRequest>
        - Bakery resolved fei`Request
          - InitSetter resolved feb`Request
            - Activator resolved veb`Request
              - AcceptRootType resolved teb`Request
            - ServiceProvider resolved foc`HttpClient
            - FactoryArguments resolved foc`RequestConfig
            - FactoryArguments resolved fes`CancellationToken
```

You can nicely see how the subscopes nest here, and how arguments
are used for resolving dependencies.

The prefixes (`fei`, `veb`, etc.) have the following meaning:

- first character is the *runtime maturity*:
  - `t`: type without instance
  - `v`: uninitialized instance (virgin)
  - `f`: finished / externally initialized (init-setters set)
- the second character can be `e` for essential (required) and `o` for optional
- the third character is a property of the type itself:
  - `b`: baked class (taken from an attribute the bakery puts on the type)
  - `c`: class or record
  - `i`: interface
  - `s`: struct

The first two of those are an additional disambiguator of the dependency.
Something can depend on a type or a finished instance - those are not
the same thing.

The last of those is a property of the type itself and is just there for the
benefit for this report.

#### Dependency providers and the resolution process

The injection system resolves by going from a root dependency asking
a number of *dependency providers* if they can resolve the dependency.

The names in the report are the class names of such providers, minus
the `DependencyProvider` suffix. So, `Bakery` is the
`BakeryDependencyProvider` and it's configured to provide a `f*i*` for
any `f*b*` that counts as a *molidinum type* (you configure that
with `IdentifyMoldiniumTypes` on the configuration builder).

The `InitSetter` then says it's able to provide a finished instance
if it has a freshly created instance and some further dependencies coming
from the properties that need to be set.

The `Activator` can provide such a virgin instance provided the
type itself is known, which we assume for the root types of all scopes
and is provided by `AcceptRootType`.

The root types are those you explicitly hooked up with one of the
`Add*MoldiniumRoot` and also those returned by factories (they are
the roots of subscopes).

The `Factory` is responsible for resolving factory delegates. It's
special in that it marks the returned resolution as requiring a
subscope, which the dependency injection system handles after the
current scope is completely resolved.

The `Factory` also provides an additional dependency provider for the new
subscope, the `Arguments` that will provide the dependencies injected
through the arguments of the delegate.

After the dependency injection system has resolved everything for
the current scope, it goes to the recorded subscopes and recursively
starts again.

This whole logic lives in the `Scope` class which represents a scope
at validation time and also forms a tree with its subscopes.

There's also `RuntimeScope`, which is the representation of an
instantiated scope. There will be as many instances of `RuntimeScope`
for each `Scope` as there were calls to the factory that
represents the scope (and usually only one for the root scope).

## Further essential features not yet implemented

### Internal Dependency Resolution, Entity Framework and Deserializers

Besides maturity, one of the main limitations of the current implementation
is that it can't be used with deserializers: Those do reflection themselves.
Upon trying to create an object for a property they will complain that they
don't know what type to use when that property's type is just an interface.

This could be solved by making the baked type implement the interfaces privately
and instead expose the properties with different types: The baked types.

Then, the only remaining issue is that these types need to be
default-constructible. They technically already are, but a deserializer will
not have their init setters initialized by using the correct dependency injection.

While the model types typically used in these scenarios may not really need
proper dependency injection, there is one dependency that is needed:
That of the tracking repository (once it's no longer static).

To make this work, I'm eyeing *ambient storage*: .NET has the wonderful ability
to allow methods to be called with a *call context* that can store stuff. It's like
thread-local storage, but also works across asynchronous chains of execution.

Before calling the deserializer, the dependency injection system would be installed
in ambient storage and then retrieved by the constructor of a baked type
to do *internal dependency resolution*. This would allow dependency injection
to work in these scenarios and also make the baked type's instance have
the correct tracking repository.

Another issue is type hierarchies: Deserializers require a single-base
hierarchy for polymorphic deserialization and, currently, baked types don't
derive from other baked types at all. This could be done though, but of
course requires the user to somehow sometimes distinguish a unique base.

On the plus side, such libraries are generally willing to use an existing
collection instance in properties typed as `IList<>` or `ICollection<>`. So,
at least there is no additional trouble on that front.

#### Entity Framework

Entity Framework is even more important than deserializers as entities are
more often bound against in UIs. It's similar in that it also wants to create
its entity types. It has one major additional problem: Fluent configuration.

The best solution that can be implemented with reasonable effort would
offer the user to configure like this:

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

Moldinium could then provide a baked implementation for `IMyInterfaceDbContext`
simply by providing the correct arguments to the type parameters. The
result should be good enough for EF, but I haven't tried.

This does require some repetition of type names, obviously, but at least
it's only one place per context that is ugly.

Another option would be to write a wrapper around EF's `ModelBuilder`.
This would be very specific to EF and may require maintenance when EF's
`ModelBuilder` changes.

### Access control

Interfaces can't have private members (except private method implementations),
but they can have protected members.

Moldinium should allow that. I'm thinking about using protected init
setters to mark internal dependency resolution (see the section above).

### Dependency Tracking

I already mentioned supporting Blazor Server by making the repository non-static
and have it injected.

The tracking system is also quite wasteful as it needs each trackable to have
its own object. This can lead to an explosion of heap allocations in a complex
application. It would be better if all trackables on a single moldinium model
share a single heap instance to communicate with the repository.

The tracking system could also do with a lot more tools guarding against misuse
and diagnostic facilities, such as a dependency report similar to that the
dependency injection system has.

### Dependency Injection

Already mentioned is the weakness regarding nullable types for factories.

There's another issue that users likely desire: To globally define
additional dependencies provided for the created type of a specific factory
without having to specify those as argument types or passing them at
the factory call.

This is the way scoped dependencies are set up in most IoC containers, and
it should be an option here as well.

The user can declare a factory type explicitly as a delegate:

```c#
public delegate MySubscopRootType MySubscope( /* ... */ );

public interface SomeType
{
    MySubscope CreateMySubscopeRootType { get; init; }

    // ...
}
```

Then, it should be allowed to register additional dependencies on the
application root identifying the subscope with this delegate type in the
same way other IoC containers allow it.
