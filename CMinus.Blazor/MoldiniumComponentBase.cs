using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace CMinus.Blazor;

public abstract class MoldiniumComponentUpperBase : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder) => BuildRenderTreeFromRazor(builder);

    protected virtual void BuildRenderTreeFromRazor(RenderTreeBuilder builder) { }
}

public abstract class MoldiniumComponentBase : MoldiniumComponentUpperBase
{
    protected override void BuildRenderTreeFromRazor(RenderTreeBuilder builder) => BuildRenderTree(builder);

    protected new virtual void BuildRenderTree(RenderTreeBuilder builder) { }
}
