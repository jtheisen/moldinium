using System;
using System.Collections.Generic;
using System.Linq;

namespace Moldinium;

class ActionDisposable : IDisposable
{
    Action action;

    public ActionDisposable(Action action)
    {
        this.action = action;
    }

    public void Dispose()
    {
        action();
    }
}

class ActionWatchSubscription : IWatchSubscription
{
    IWatchable watchable;

    Action action;

    public ActionWatchSubscription(IWatchable watchable, Action action)
    {
        this.watchable = watchable;
        this.action = action;
    }

    public IEnumerable<IWatchable> Dependencies
        => new[] { watchable };

    public void Dispose()
    {
        action();
    }
}

class SerialWatchSubscription : IWatchSubscription
{
    IWatchSubscription? subscription;

    public IEnumerable<IWatchable> Dependencies
        => subscription?.Dependencies ?? Enumerable.Empty<IWatchable>();

    public IWatchSubscription? Subscription
    {
        get
        {
            return subscription;
        }
        set
        {
            if (null != subscription)
                subscription.Dispose();
            subscription = value;
        }
    }

    public void Dispose()
    {
        Subscription = null;
    }
}

class CompositeWatchSubscription : IWatchSubscription
{
    IWatchSubscription[] subscriptions;

    public CompositeWatchSubscription(params IWatchSubscription[] subscriptions)
    {
        this.subscriptions = subscriptions;
    }

    public CompositeWatchSubscription(IEnumerable<IWatchSubscription> subscriptions)
    {
        this.subscriptions = subscriptions.ToArray();
    }

    public IEnumerable<IWatchable> Dependencies
        => from s in subscriptions from d in s.Dependencies select d;

    public void Dispose()
    {
        foreach (var subscription in subscriptions)
            subscription.Dispose();
    }
}

static class WatchSubscription
{
    public static IWatchSubscription Create(IWatchable watchable, Action action)
        => new ActionWatchSubscription(watchable, action);
}
