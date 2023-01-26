namespace Moldinium.Tracking;

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

class ActionTrackSubscription : ITrackSubscription
{
    ITrackable trackable;

    Action action;

    public ActionTrackSubscription(ITrackable trackable, Action action)
    {
        this.trackable = trackable;
        this.action = action;
    }

    public IEnumerable<ITrackable> Dependencies
        => new[] { trackable };

    public void Dispose()
    {
        action();
    }
}

class SerialTrackSubscription : ITrackSubscription
{
    ITrackSubscription? subscription;

    public IEnumerable<ITrackable> Dependencies
        => subscription?.Dependencies ?? Enumerable.Empty<ITrackable>();

    public ITrackSubscription? Subscription
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

class CompositeTrackSubscription : ITrackSubscription
{
    ITrackSubscription[] subscriptions;

    public CompositeTrackSubscription(params ITrackSubscription[] subscriptions)
    {
        this.subscriptions = subscriptions;
    }

    public CompositeTrackSubscription(IEnumerable<ITrackSubscription> subscriptions)
    {
        this.subscriptions = subscriptions.ToArray();
    }

    public IEnumerable<ITrackable> Dependencies
        => from s in subscriptions from d in s.Dependencies select d;

    public void Dispose()
    {
        foreach (var subscription in subscriptions)
            subscription.Dispose();
    }
}

static class TrackSubscription
{
    public static ITrackSubscription Create(ITrackable trackable, Action action)
        => new ActionTrackSubscription(trackable, action);
}
