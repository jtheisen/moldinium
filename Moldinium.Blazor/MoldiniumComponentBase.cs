﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Moldinium.Tracking;

namespace Moldinium.Blazor;

public abstract class MoldiniumComponentUpperBase : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder) => BuildRenderTreeFromRazor(builder);

    protected virtual void BuildRenderTreeFromRazor(RenderTreeBuilder builder) { }
}

public abstract class MoldiniumComponentBase : MoldiniumComponentUpperBase
{
    IDisposable? trackingSubscription;

    protected override void BuildRenderTreeFromRazor(RenderTreeBuilder builder)
    {
        trackingSubscription = Trackable.React(() =>
        {
            BuildRenderTree(builder);
        }, () =>
        {
            ClearSubscription();
            StateHasChanged();
        });
    }

    void ClearSubscription()
    {
        trackingSubscription?.Dispose();
        trackingSubscription = null;
    }

    protected new virtual void BuildRenderTree(RenderTreeBuilder builder) { }

    protected virtual void Dispose()
    {
        ClearSubscription();
    }
}
