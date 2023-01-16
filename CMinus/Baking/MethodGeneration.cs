using System;

namespace CMinus.Baking;

public interface IMethodWrappingImplementation : IWrappingImplementation { }

public interface IStandardMethodWrapper<
    [TypeKind(ImplementationTypeArgumentKind.Value)] TResult,
    [TypeKind(ImplementationTypeArgumentKind.Exception)] TException,
    [TypeKind(ImplementationTypeArgumentKind.Mixin)] TMixin
> : IMethodWrappingImplementation
    where TException : Exception
{
    Boolean Before(ref TResult result, ref TMixin mixin);

    void After(ref TResult result, ref TMixin mixin);
    Boolean AfterError(TException exception, ref TMixin mixin);
}
