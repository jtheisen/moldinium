﻿using System.Reflection.Emit;
using System.Reflection;

namespace Moldinium.Baking;

public interface ITrivialMethodWrapper : IMethodWrapperImplementation { }

public struct TrivialMethodWrapper : ITrivialMethodWrapper, IEmptyImplementation { }

public interface IStandardMethodWrapper<
    [TypeKind(ImplementationTypeArgumentKind.Value)] TResult,
    [TypeKind(ImplementationTypeArgumentKind.Exception)] TException,
    [TypeKind(ImplementationTypeArgumentKind.Mixin)] TMixin
> : IMethodWrapperImplementation
    where TException : Exception
{
    Boolean Before(ref TResult result, ref TMixin mixin);

    void After(ref TResult result, ref TMixin mixin);
    Boolean AfterError(TException exception, ref TMixin mixin);
}

public abstract class AbstractMethodGenerator : AbstractGenerator
{
    public virtual void GenerateMethod(IBuildingContext state, MethodInfo method)
    {
        var typeBuilder = state.TypeBuilder;

        var returnType = method.ReturnType;

        if (returnType == typeof(void))
        {
            returnType = typeof(VoidDummy);
        }

        var mixinFieldBuilder = EnsureMixin(state);

        var argumentKinds = GetArgumentKinds();

        var methodImplementation = GetMethodImplementation(state, method);

        if (methodImplementation is null) return;

        var (fieldBuilder, backingMethod) = methodImplementation.Value;

        var codeCreator = new CodeCreation(typeBuilder, argumentKinds, fieldBuilder, mixinFieldBuilder);

        codeCreator.CreateMethod(
            method,
            backingMethod,
            returnType,
            toRemove: MethodAttributes.Abstract
        );
    }

    protected abstract (FieldBuilder, MethodImplementation)? GetMethodImplementation(IBuildingContext state, MethodInfo method);
}

public class GenericMethodGenerator : AbstractMethodGenerator
{
    private readonly CheckedImplementation implementation;

    public GenericMethodGenerator(CheckedImplementation implementation)
    {
        this.implementation = implementation;
    }

    protected override void AddArgumentKinds(Dictionary<Type, ImplementationTypeArgumentKind> argumentKinds)
    {
        implementation.AddArgumentKinds(argumentKinds);
    }

    public override IEnumerable<Type?> GetMixinTypes()
    {
        yield return implementation.MixinType;
    }

    protected override (FieldBuilder, MethodImplementation)? GetMethodImplementation(IBuildingContext state, MethodInfo method)
    {
        var typeBuilder = state.TypeBuilder;

        var outerMethodImplementation = state.GetOuterImplementationInfo(method);


        var methodImplementationType = implementation.MakeImplementationType(returnType: method.ReturnType);

        var fieldBuilder = typeBuilder.DefineField($"backing_{method.Name}", methodImplementationType, FieldAttributes.Private);

        var methodImplementation = GetMethodImplementation(fieldBuilder, "", outerMethodImplementation);

        return (fieldBuilder, methodImplementation);
    }
}

public static class MethodGenerator
{
    public static AbstractMethodGenerator Create(Type methodImplementationType)
    {
        var instance = new GenericMethodGenerator(new CheckedImplementation(methodImplementationType, typeof(IMethodWrapperImplementation)));

        return instance;
    }
}

