using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;

namespace CMinus.Baking;

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

        var (fieldBuilder, backingMethod) = GetMethodImplementation(state, method);

        var codeCreator = new CodeCreation(typeBuilder, argumentKinds, fieldBuilder, mixinFieldBuilder);

        codeCreator.CreateMethod(
            method,
            backingMethod,
            returnType,
            toRemove: MethodAttributes.Abstract
        );
    }

    protected abstract (FieldBuilder, MethodImplementation) GetMethodImplementation(IBuildingContext state, MethodInfo method);
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

    protected override (FieldBuilder, MethodImplementation) GetMethodImplementation(IBuildingContext state, MethodInfo method)
    {
        var typeBuilder = state.TypeBuilder;

        var methodImplementationType = implementation.MakeImplementationType(returnType: method.ReturnType);

        var fieldBuilder = typeBuilder.DefineField($"backing_{method.Name}", methodImplementationType, FieldAttributes.Private);

        var methodImplementation = GetMethodImplementation(fieldBuilder, "", state.GetOuterImplementationInfo(method).ImplementationMethod);

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
        GetMethodImplementation(IBuildingContext state, MethodInfo method)
    {
        var implementationMethod = GetMethod(fieldBuilder, method.Name);

        return (fieldBuilder, implementationMethod);
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

