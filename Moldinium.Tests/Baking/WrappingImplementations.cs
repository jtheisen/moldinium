namespace Testing.Baking;

public interface IWrappingMethodImplementation<
    [TypeKind(ImplementationTypeArgumentKind.Return)] Return,
    [TypeKind(ImplementationTypeArgumentKind.Exception)] Exception,
    [TypeKind(ImplementationTypeArgumentKind.Mixin)] Mixin
> : IMethodWrapperImplementation
    where Exception : System.Exception
{
    Boolean Before(ref Return value, ref Mixin mixin);
    void After(ref Return value, ref Mixin mixin);
    Boolean AfterError(Exception exception, ref Mixin mixin);
}

public interface IWrappingPropertyImplementation<
    [TypeKind(ImplementationTypeArgumentKind.Value)] Value,
    [TypeKind(ImplementationTypeArgumentKind.Exception)] Exception,
    [TypeKind(ImplementationTypeArgumentKind.Mixin)] Mixin
> : IPropertyWrapperImplementation
    where Exception : System.Exception
{
    Boolean BeforeGet([MaybeNullWhen(true)] ref Value value, ref Mixin mixin);
    Boolean BeforeSet(ref Value value, ref Mixin mixin);

    void AfterGet(ref Value value, ref Mixin mixin);
    void AfterSet(ref Value value, ref Mixin mixin);

    Boolean AfterErrorGet(Exception exception, ref Value value, ref Mixin mixin);
    Boolean AfterErrorSet(Exception exception, ref Value value, ref Mixin mixin);
}

/* Delegating */

public interface IDelegatingWrappingPropertyMixin
{
    Object MethodWrappingMethods { get; set; }
    Object GetWrappingMethods { get; set; }
    Object SetWrappingMethods { get; set; }
}

public struct DelegatingWrappingMixin : IDelegatingWrappingPropertyMixin
{
    public Object MethodWrappingMethods { get; set; }
    public Object GetWrappingMethods { get; set; }
    public Object SetWrappingMethods { get; set; }
}

public record WrappingMethods<T>(OnBefore<T>? Before = null, OnAfter<T>? After = null, OnAfterError<T>? AfterError = null);

public delegate Boolean OnBefore<T>(ref T value);
public delegate void OnAfter<T>(ref T value);
public delegate Boolean OnAfterError<T>(Exception exception);

public struct DelegatingWrappingMethodImplementation<Return, Exception>
    : IWrappingMethodImplementation<Return, Exception, DelegatingWrappingMixin>
    where Exception : System.Exception
{
    public bool Before(ref Return value, ref DelegatingWrappingMixin mixin)
    {
        return (mixin.MethodWrappingMethods as WrappingMethods<Return>)?.Before?.Invoke(ref value) ?? true;
    }

    public void After(ref Return value, ref DelegatingWrappingMixin mixin)
    {
        (mixin.MethodWrappingMethods as WrappingMethods<Return>)?.After?.Invoke(ref value);
    }

    public bool AfterError(Exception exception, ref DelegatingWrappingMixin mixin)
    {
        return (mixin.MethodWrappingMethods as WrappingMethods<Return>)?.AfterError?.Invoke(exception) ?? true;
    }
}

public struct DelegatingWrappingPropertyImplementation<Value, Exception>
    : IWrappingPropertyImplementation<Value, Exception, DelegatingWrappingMixin>
    where Exception : System.Exception
{
    public Boolean BeforeGet(ref Value value, ref DelegatingWrappingMixin mixin)
    {
        return (mixin.GetWrappingMethods as WrappingMethods<Value>)?.Before?.Invoke(ref value) ?? true;
    }

    public void AfterGet(ref Value value, ref DelegatingWrappingMixin mixin)
    {
        (mixin.GetWrappingMethods as WrappingMethods<Value>)?.After?.Invoke(ref value);
    }

    public Boolean BeforeSet(ref Value value, ref DelegatingWrappingMixin mixin)
    {
        return (mixin.SetWrappingMethods as WrappingMethods<Value>)?.Before?.Invoke(ref value) ?? true;
    }

    public void AfterSet(ref Value value, ref DelegatingWrappingMixin mixin)
    {
        (mixin.SetWrappingMethods as WrappingMethods<Value>)?.After?.Invoke(ref value);
    }

    public Boolean AfterErrorGet(Exception exception, ref Value value, ref DelegatingWrappingMixin mixin)
    {
        return (mixin.GetWrappingMethods as WrappingMethods<Value>)?.AfterError?.Invoke(exception) ?? true;
    }

    public Boolean AfterErrorSet(Exception exception, ref Value value, ref DelegatingWrappingMixin mixin)
    {
        return (mixin.SetWrappingMethods as WrappingMethods<Value>)?.AfterError?.Invoke(exception) ?? true;
    }
}

/* Event recording */

public enum WrappingPropertyNotificationEventType
{
    BeforeGet,
    AfterGet,
    AfterErrorGet,
    BeforeSet,
    AfterSet,
    AfterErrorSet
}

public interface IWrappingPropertyNotificationMixin
{
    event Action<WrappingPropertyNotificationEventType, Object?> OnEvent;
}

public struct WrappingPropertyNotificationMixin : IWrappingPropertyNotificationMixin
{
    public event Action<WrappingPropertyNotificationEventType, Object?> OnEvent;

    public void Notify(WrappingPropertyNotificationEventType type, Object? value)
    {
        OnEvent?.Invoke(type, value);
    }
}

public struct EventWrappingPropertyImplementation<Value, Exception>
    : IWrappingPropertyImplementation<Value, Exception, WrappingPropertyNotificationMixin>
    where Exception : System.Exception
{
    public Boolean BeforeGet(ref Value value, ref WrappingPropertyNotificationMixin mixin)
    {
        mixin.Notify(WrappingPropertyNotificationEventType.BeforeGet, value);

        return true;
    }

    public void AfterGet(ref Value value, ref WrappingPropertyNotificationMixin mixin)
    {
        mixin.Notify(WrappingPropertyNotificationEventType.AfterGet, value);
    }

    public Boolean BeforeSet(ref Value value, ref WrappingPropertyNotificationMixin mixin)
    {
        mixin.Notify(WrappingPropertyNotificationEventType.BeforeSet, value);

        return true;
    }

    public void AfterSet(ref Value value, ref WrappingPropertyNotificationMixin mixin)
    {
        mixin.Notify(WrappingPropertyNotificationEventType.AfterSet, value);
    }

    public Boolean AfterErrorGet(Exception exception, ref Value value, ref WrappingPropertyNotificationMixin mixin)
    {
        mixin.Notify(WrappingPropertyNotificationEventType.AfterErrorGet, exception);

        return true;
    }

    public Boolean AfterErrorSet(Exception exception, ref Value value, ref WrappingPropertyNotificationMixin mixin)
    {
        mixin.Notify(WrappingPropertyNotificationEventType.AfterErrorSet, exception);

        return true;
    }
}
