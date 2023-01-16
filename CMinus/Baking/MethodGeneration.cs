using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;

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

public abstract class AbstractMethodGenerator : AbstractGenerator
{
    public virtual void GenerateMethod(BakingState state, MethodInfo method)
    {
        var typeBuilder = state.TypeBuilder;

        var returnType = method.ReturnType;

        var mixinFieldBuilder = EnsureMixin(state);

        var argumentKinds = GetArgumentKinds();

        argumentKinds[returnType] = ImplementationTypeArgumentKind.Return;

        var (fieldBuilder, backingMethod) = GetMethodImplementation(state, method);

        var codeCreator = new CodeCreation(typeBuilder, argumentKinds, fieldBuilder, mixinFieldBuilder);

        codeCreator.CreateMethod(
            method, method.GetBaseDefinition(),
            backingMethod,
            returnType,
            toRemove: MethodAttributes.Abstract
        );
    }

    protected virtual FieldBuilder? EnsureMixin(BakingState state) => null;

    protected abstract (FieldBuilder, MethodImplementation) GetMethodImplementation(BakingState state, MethodInfo method);
}

public class GenericMethodGenerator : AbstractMethodGenerator
{
    private readonly CheckedImplementation implementation;

    public GenericMethodGenerator(CheckedImplementation implementation)
    {
        this.implementation = implementation;
    }

    protected override IDictionary<Type, ImplementationTypeArgumentKind> GetArgumentKinds()
        => implementation.GetArgumentKinds();

    protected override FieldBuilder? EnsureMixin(BakingState state) => implementation.MixinType is not null ? state.EnsureMixin(state, implementation.MixinType, false) : null;

    protected override (FieldBuilder, MethodImplementation) GetMethodImplementation(BakingState state, MethodInfo method)
    {
        var typeBuilder = state.TypeBuilder;

        var methodImplementationType = implementation.MakeImplementationType(returnType: method.ReturnType);

        var fieldBuilder = typeBuilder.DefineField($"backing_{method.Name}", methodImplementationType, FieldAttributes.Private);

        var methodImplementation = GetMethodImplementation(fieldBuilder, "");

        return (fieldBuilder, methodImplementation);
    }
}

public class DelegatingMethodGenerator : AbstractMethodGenerator
{
    private readonly FieldBuilder fieldBuilder;

    public DelegatingMethodGenerator(FieldBuilder targetFieldBuilder)
    {
        this.fieldBuilder = targetFieldBuilder;
    }

    protected override (FieldBuilder, MethodImplementation)
        GetMethodImplementation(BakingState state, MethodInfo method)
    {
        var implementationMethod = GetMethod(fieldBuilder, method.Name);

        return (fieldBuilder, implementationMethod);
    }
}

public static class MethodGenerator
{
    public static AbstractMethodGenerator Create(Type methodImplementationType)
    {
        var instance = new GenericMethodGenerator(new CheckedImplementation(methodImplementationType, typeof(IImplementation), typeof(IMethodWrappingImplementation)));

        return instance;
    }
}

