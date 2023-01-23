# Nested-scopes IoC Containers

Most IoC containers have three different modes of registering types in three
different modes: *singleton*, *scoped* and *transient*.

They also do lifetime management.

I think this can be improved by thinking of these three modes as
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

Moldinium instead allows to resolve factories that can be used to create subscopes
and provide new dependencies for them in a manner that still allows early validation.

Take the following example:

```
interface App
{
    Func<CancellationToken, Circuit> NewJob { get; init; }

    async Task Run(CancellationToken ct)
    {
        await using var job = NewJob(ct);

        await job.Run();
    }
}

interface Job : IAsyncDisposable
{
    Func<Request> NewRequest { get; init; }

    async Task Run()
    {
        var request = NewRequest(new RequestConfig());

        await request.FetchContent();
    }

    // Our job needs this for mysterious reasons
    async Task IAsyncDisposable.Dispose() { /* ... */ }
}

record RequestConfig(Uri Url);

interface Request
{
    RequestConfig Config { get; init; }

    CancellationToken { get; init; }

    HttpClient { get; init; }

    async Task FetchContent() { /* ... */ }
}
```

The top scope `App` is resolved by the application setup code, eg in `Program.cs`.

First Moldinium validates the dependencies:

- `App` needs a factory that creates `Job`s but for those
  it provides `CancellationToken`s
- `Job` needs a factory that creates `Request`s, but for those it provides `RequestConfig`s
- `Request` get a `CancellationToken` from two scopes up and now has all except `HttpClient`, which we assume was provided by the root setup

By making the factories be typed on the dependencies that will be provided,
Moldinium can check all that at validation time before any instances are created.

I also see no reason for lifetime management being part of the container with
this design: All object creation is done only when calling factory functions, so
it's now the *user* that creates. The user should then dispose as well.

This simplifies the matter considerably: Lifetime management is far from trivial, especially when you consider `IAsyncDisposable` and exception handling during cleanup. That can now be done in the usual C# fashion using `usings` and `await usings` and debugged accordingly.
