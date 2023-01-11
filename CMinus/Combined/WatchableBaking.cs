namespace CMinus;

public struct WatchablePropertyImplementation<T> : IPropertyImplementation<T>
{
    Var<T> variable;

    public T Value { get => variable.Value; set => variable.Value = value; }
}
