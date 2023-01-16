using System.Reflection.Emit;
using System.Reflection;
using System.Collections.Generic;
using System;
using System.Linq;
using Castle.DynamicProxy.Generators;
using System.Reflection.PortableExecutable;

namespace CMinus;

public record MethodImplementation
{
    public static implicit operator MethodImplementation(MethodInfo method) => new DirectMethodImplementation(method);
}

public record DirectMethodImplementation(MethodInfo Method) : MethodImplementation { }

public record WrappingMethodImplementation(
    MethodInfo? BeforeMethod = null,
    MethodInfo? AfterMethod = null,
    MethodInfo? AfterOnErrorMethod = null
) : MethodImplementation;

public record PropertyImplementation(MethodImplementation Get, MethodImplementation Set);

public class CodeCreation
{
    private readonly TypeBuilder typeBuilder;
    private readonly IDictionary<Type, ImplementationTypeArgumentKind> argumentKinds;
    private readonly FieldBuilder? implementationFieldBuilder;
    private readonly FieldBuilder? mixInFieldBuilder;

    public CodeCreation(
        TypeBuilder typeBuilder,
        IDictionary<Type, ImplementationTypeArgumentKind> argumentKinds,
        FieldBuilder? implementationFieldBuilder,
        FieldBuilder? mixInFieldBuilder
    )
    {
        this.typeBuilder = typeBuilder;
        this.argumentKinds = argumentKinds;
        this.implementationFieldBuilder = implementationFieldBuilder;
        this.mixInFieldBuilder = mixInFieldBuilder;
    }

    public MethodBuilder Declare(
        TypeBuilder typeBuilder,
        MethodInfo methodTemplate,
        MethodAttributes toAdd = MethodAttributes.Public,
        MethodAttributes toRemove = MethodAttributes.Abstract
    )
    {
        var attributes = methodTemplate.Attributes;
        attributes |= toAdd;
        attributes &= ~toRemove;

        var parameters = methodTemplate.GetParameters();

        var rcms = methodTemplate.ReturnParameter.GetRequiredCustomModifiers();

        var methodBuilder = typeBuilder.DefineMethod(
            methodTemplate.Name,
            attributes,
            methodTemplate.CallingConvention,
            methodTemplate.ReturnType,
            methodTemplate.ReturnParameter.GetRequiredCustomModifiers(),
            methodTemplate.ReturnParameter.GetOptionalCustomModifiers(),
            parameters.Select(p => p.ParameterType).ToArray(),
            parameters.Select(p => p.GetRequiredCustomModifiers()).ToArray(),
            parameters.Select(p => p.GetOptionalCustomModifiers()).ToArray()
        );

        if (methodTemplate.DeclaringType!.IsClass)
        {
            typeBuilder.DefineMethodOverride(methodBuilder, methodTemplate);
        }
        return methodBuilder;
    }

    public MethodBuilder CreateMethod(
        MethodInfo methodTemplate,
        MethodInfo wrappedMethod,
        MethodImplementation implementation,
        Type valueOrReturnType,
        MethodAttributes toAdd = default,
        MethodAttributes toRemove = default
    )
    {
        var methodBuilder = Declare(typeBuilder, methodTemplate, toAdd, toRemove);
        var generator = methodBuilder.GetILGenerator();
        GenerateMethodImplementationCode(generator, methodBuilder, implementation, valueOrReturnType, wrappedMethod);
        return methodBuilder;
    }

    public void GenerateMethodImplementationCode(
        ILGenerator generator,
        MethodBuilder methodBuilder,
        MethodImplementation methodImplementation,
        Type valueOrReturnType,
        MethodInfo? wrappedMethod = null
    )
    {
        if (methodImplementation is DirectMethodImplementation directMethodImplementation)
        {
            GenerateImplementationCode(
                generator,
                implementationFieldBuilder,
                directMethodImplementation.Method,
                ValueAt.FirstArgumentPassedByValue,
                addRet: true
            );
        }
        else if (methodImplementation is WrappingMethodImplementation wrappingMethodImplementation)
        {
            GenerateWrappingImplementationCode(
                generator,
                methodBuilder,
                valueOrReturnType,
                wrappingMethodImplementation.BeforeMethod,
                wrappingMethodImplementation.AfterMethod,
                wrappingMethodImplementation.AfterOnErrorMethod,
                wrappedMethod
            );
        }
        else
        {
            throw new Exception($"Unknown method implementation {methodImplementation.GetType()}");
        }
    }

    void GenerateWrappingImplementationCode(
        ILGenerator il,
        MethodBuilder methodBuilder,
        Type valueOrReturnType,
        MethodInfo? backingTryMethod,
        MethodInfo? backingAfterMethod,
        MethodInfo? backingAfterErrorMethod,
        MethodInfo? wrappedMethod
        )
    {
        if (valueOrReturnType.IsValueType)
        {
            il.DeclareLocal(valueOrReturnType);
        }
        else
        {
            il.DeclareLocal(valueOrReturnType, pinned: true);
        }

        il.Emit(OpCodes.Ldloca_S, 0);
        il.Emit(OpCodes.Initobj, valueOrReturnType);

        il.DeclareLocal(typeof(Exception));

        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Stloc_1);

        var exitLabel = il.DefineLabel();

        if (backingTryMethod is not null)
        {
            GenerateImplementationCode(
                il,
                implementationFieldBuilder,
                backingTryMethod,
                ValueAt.FirstLocalPassedByRef
            );

            il.Emit(OpCodes.Brfalse_S, exitLabel);
        }

        if (wrappedMethod is not null)
        {
            var tryBlockLabel = il.BeginExceptionBlock();

            if (GenerateNestedCallCode(il, wrappedMethod))
            {
                il.Emit(OpCodes.Stloc_0);
            }

            il.Emit(OpCodes.Leave, tryBlockLabel);

            il.BeginCatchBlock(typeof(Exception));

            il.Emit(OpCodes.Stloc_1);

            if (backingAfterErrorMethod is not null)
            {
                GenerateImplementationCode(
                    il,
                    implementationFieldBuilder,
                    backingAfterErrorMethod,
                    ValueAt.FirstLocalPassedByRef
                );

                var dontRethrowLabel = il.DefineLabel();

                il.Emit(OpCodes.Brfalse_S, dontRethrowLabel);
                il.Emit(OpCodes.Rethrow);

                il.MarkLabel(dontRethrowLabel);
            }

            il.EndExceptionBlock();
        }

        if (backingAfterMethod is not null)
        {
            il.Emit(OpCodes.Ldloc_1);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ceq);
            il.Emit(OpCodes.Brfalse_S, exitLabel);

            GenerateImplementationCode(
                il,
                implementationFieldBuilder,
                backingAfterMethod,
                ValueAt.FirstLocalPassedByRef
            );
        }

        il.MarkLabel(exitLabel);

        if (methodBuilder.ReturnType != typeof(void))
        {
            il.Emit(OpCodes.Ldloc_0, 0);
        }

        il.Emit(OpCodes.Ret);
    }

    Boolean GenerateNestedCallCode(
        ILGenerator generator,
        MethodInfo nestedMethod
        )
    {
        var isStatic = nestedMethod.IsStatic;

        if (!isStatic)
        {
            generator.Emit(OpCodes.Ldarg_0);
        }

        var parameters = nestedMethod.GetParameters();

        var parameterOffset = isStatic ? 0 : 1;

        for (var i = 0; i < parameters.Length; ++i)
        {
            generator.Emit(OpCodes.Ldarg, i + parameterOffset);
        }

        generator.Emit(OpCodes.Call, nestedMethod);

        return nestedMethod.ReturnType != typeof(void);
    }

    public enum ValueAt
    {
        NoValue,
        FirstArgumentPassedByValue,
        FirstLocalPassedByRef,
        GetFromDefaultImplementationPassedByValue
    }

    public void GenerateImplementationCode(
        ILGenerator generator,
        FieldBuilder? fieldBuilder,
        MethodInfo backingMethod,
        ValueAt valueAt,
        FieldBuilder? defaultImplementationFieldBuilder = null,
        MethodInfo? defaultImplementationGetMethod = null,
        Boolean addRet = false
    )
    {
        if (!backingMethod.IsStatic)
        {
            generator.Emit(OpCodes.Ldarg_0);

            if (fieldBuilder is not null)
            {
                generator.Emit(OpCodes.Ldflda, fieldBuilder);
            }
        }

        var implementationType = backingMethod.DeclaringType!;

        var typeDefinition = implementationType.IsGenericType ? implementationType.GetGenericTypeDefinition() : implementationType;

        var genericTypeDefinitionBackingMethod = typeDefinition.GetMethod(backingMethod.Name);

        if (genericTypeDefinitionBackingMethod is null)
        {
            throw new Exception($"Can't find generic type definition method corresponding to {backingMethod.Name} in {implementationType}");
        }

        var parameters = genericTypeDefinitionBackingMethod.GetParameters();

        foreach (var p in parameters)
        {
            var (parameterType, byRef) = GetParameterType(p);

            if (argumentKinds.TryGetValue(parameterType, out var kind))
            {
                switch (kind)
                {
                    case ImplementationTypeArgumentKind.Value:
                        switch (valueAt)
                        {
                            case ValueAt.FirstArgumentPassedByValue:
                                if (byRef) throw new Exception($"Value types need to be passed by value");
                                generator.Emit(OpCodes.Ldarg_1);
                                break;
                            case ValueAt.FirstLocalPassedByRef:
                                if (!byRef) throw new Exception($"Value types to before and after methods need to be passed by ref");
                                generator.Emit(OpCodes.Ldloca_S, 0);
                                break;
                            case ValueAt.GetFromDefaultImplementationPassedByValue:
                                if (byRef) throw new Exception($"Value types need to be passed by value");
                                if (defaultImplementationFieldBuilder is null || defaultImplementationGetMethod is null)
                                {
                                    throw new Exception("Expected to have a fieldBuilder for a default implementation with a get method");
                                }
                                generator.Emit(OpCodes.Ldarg_0);
                                generator.Emit(OpCodes.Ldflda, defaultImplementationFieldBuilder);
                                generator.Emit(OpCodes.Call, defaultImplementationGetMethod);
                                break;
                            default:
                                throw new Exception("Internal error: No value here");
                        }

                        break;
                    case ImplementationTypeArgumentKind.Mixin:
                        if (!byRef) throw new Exception("Mixins must be passed by ref");

                        generator.Emit(OpCodes.Ldarg_0);
                        generator.Emit(OpCodes.Ldflda, mixInFieldBuilder!);
                        break;
                    default:
                        throw new Exception($"Unkown argument kind {kind}");
                }
            }
            else if (p.ParameterType == typeof(Object))
            {
                generator.Emit(OpCodes.Ldarg_0);
            }
            else if (p.ParameterType == typeof(Exception))
            {
                generator.Emit(OpCodes.Ldloc_1);
            }
            else
            {
                throw new Exception($"Dont know how to handle argument {p.Name} of type {p.ParameterType} of property implementation method {backingMethod.Name} on property implementation type {backingMethod.DeclaringType}");
            }
        }

        generator.Emit(OpCodes.Call, backingMethod);

        if (addRet)
        {
            generator.Emit(OpCodes.Ret);
        }
    }

    (Type type, Boolean byRef) GetParameterType(ParameterInfo p)
    {
        var type = p.ParameterType;

        if (type is null) throw new Exception($"Parameter {p} unexpectedly has no type");

        return type.IsByRef ? ((type.GetElementType() ?? throw new Exception("Can't get element type of ref type")), true) : (type, false);
    }
}
