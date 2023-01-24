using System.Reflection.Emit;
using System.Reflection;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Moldinium;

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

        typeBuilder.DefineMethodOverride(methodBuilder, methodTemplate);

        CustomAttributeCopying.CopyCustomAttributes(methodBuilder.SetCustomAttribute, methodTemplate);

        return methodBuilder;
    }

    public MethodBuilder CreateMethod(
        MethodInfo methodTemplate,
        MethodImplementation implementation,
        Type valueOrReturnType,
        MethodAttributes toAdd = default,
        MethodAttributes toRemove = default
    )
    {
        var methodBuilder = Declare(typeBuilder, methodTemplate, toAdd, toRemove);
        var generator = methodBuilder.GetILGenerator();
        GenerateMethodImplementationCode(generator, methodBuilder, implementation, valueOrReturnType);
        return methodBuilder;
    }

    public void GenerateMethodImplementationCode(
        ILGenerator generator,
        MethodBuilder methodBuilder,
        MethodImplementation implementation,
        Type valueOrReturnType
    )
    {
        if (implementation is DirectMethodImplementation directMethodImplementation)
        {
            GenerateImplementationCode(
                generator,
                implementationFieldBuilder,
                directMethodImplementation.Method,
                ValueOrReturnAt.FirstArgumentPassedByValue,
                false,
                addRet: true
            );
        }
        else if (implementation is OuterMethodImplemention outerMethodImplementation)
        {
            GenerateNestedCallCode(generator, outerMethodImplementation.WrappedMethod);

            generator.Emit(OpCodes.Ret);
        }
        else if (implementation is WrappingMethodImplementation wrappingMethodImplementation)
        {
            var outerMethod = wrappingMethodImplementation.WrappedMethod;

            if (outerMethod is null || !outerMethod.Value.IsImplememted) throw new Exception($"Internal error: asked to wrap method {methodBuilder.Name} but have no method to wrap");

            GenerateWrappingImplementationCode(
                generator,
                methodBuilder,
                valueOrReturnType,
                wrappingMethodImplementation.BeforeMethod,
                wrappingMethodImplementation.AfterMethod,
                wrappingMethodImplementation.AfterOnErrorMethod,
                outerMethod.Value
            );
        }
        else
        {
            throw new Exception($"Unknown method implementation {implementation.GetType()}");
        }
    }

    void GenerateWrappingImplementationCode(
        ILGenerator il,
        MethodBuilder methodBuilder,
        Type valueOrReturnType,
        MethodInfo? backingBeforeMethod,
        MethodInfo? backingAfterMethod,
        MethodInfo? backingAfterErrorMethod,
        MethodImplementationInfo wrappedMethod
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

        if (backingBeforeMethod is not null)
        {
            GenerateImplementationCode(
                il,
                implementationFieldBuilder,
                backingBeforeMethod,
                ValueOrReturnAt.FirstLocalPassedByRef,
                false
            );

            il.Emit(OpCodes.Brfalse_S, exitLabel);
        }

        {
            var tryBlockLabel = il.BeginExceptionBlock();

            if (GenerateNestedCallCode(il, wrappedMethod))
            {
                il.Emit(OpCodes.Stloc_0);
            }

            il.Emit(OpCodes.Leave, tryBlockLabel);

            il.BeginCatchBlock(typeof(Exception));

            il.Emit(OpCodes.Stloc_1);

            var dontRethrowLabel = il.DefineLabel();

            if (backingAfterErrorMethod is not null)
            {
                GenerateImplementationCode(
                    il,
                    implementationFieldBuilder,
                    backingAfterErrorMethod,
                    ValueOrReturnAt.FirstLocalPassedByRef,
                    true
                );

                il.Emit(OpCodes.Brfalse_S, dontRethrowLabel);
            }

            il.Emit(OpCodes.Rethrow);

            il.MarkLabel(dontRethrowLabel);

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
                ValueOrReturnAt.FirstLocalPassedByRef,
                false
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
        MethodImplementationInfo nestedMethodInfo
        )
    {
        var nestedMethod = nestedMethodInfo.ImplementationMethod!;

        var isStatic = nestedMethod.IsStatic;

        if (!isStatic)
        {
            generator.Emit(OpCodes.Ldarg_0);

            if (nestedMethodInfo.MixinFieldBuilder is FieldBuilder mixinFieldBuilder)
            {
                generator.Emit(OpCodes.Ldflda, mixinFieldBuilder);
            }
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

    public enum ValueOrReturnAt
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
        ValueOrReturnAt valueOrReturnAt,
        Boolean haveExceptionAtLocal1,
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

        var concreteParameters = backingMethod.GetParameters();
        var genericParameters = genericTypeDefinitionBackingMethod.GetParameters();

        for (var i = 0; i< genericParameters.Length; i++)
        {
            var genericParameter = genericParameters[i];
            var concreteParameter = concreteParameters[i];

            var (parameterType, byRef) = GetParameterType(genericParameter);

            if (argumentKinds.TryGetValue(parameterType, out var kind))
            {
                switch (kind)
                {
                    case ImplementationTypeArgumentKind.Container:
                        generator.Emit(OpCodes.Ldarg_0);
                        break;
                    case ImplementationTypeArgumentKind.Exception:
                        if (!haveExceptionAtLocal1) throw new Exception($"Can't pass an exception in this context");
                        if (concreteParameter.ParameterType != typeof(Exception)) throw new Exception("The exception parameter must be a System.Exception");
                        generator.Emit(OpCodes.Ldloc_1);
                        break;
                    case ImplementationTypeArgumentKind.Value:
                    case ImplementationTypeArgumentKind.Handler:
                    case ImplementationTypeArgumentKind.Return:
                        switch (valueOrReturnAt)
                        {
                            case ValueOrReturnAt.FirstArgumentPassedByValue:
                                if (byRef) throw new Exception($"{kind} types need to be passed by value");
                                generator.Emit(OpCodes.Ldarg_1);
                                break;
                            case ValueOrReturnAt.FirstLocalPassedByRef:
                                if (!byRef) throw new Exception($"{kind} types to before and after methods need to be passed by ref");
                                generator.Emit(OpCodes.Ldloca_S, 0);
                                break;
                            case ValueOrReturnAt.GetFromDefaultImplementationPassedByValue:
                                if (kind == ImplementationTypeArgumentKind.Return) throw new Exception("Method wrappers don't have defaults");
                                if (byRef) throw new Exception($"{kind} types need to be passed by value");
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
            else
            {
                throw new Exception($"Dont know how to handle argument {genericParameter.Name} of type {genericParameter.ParameterType} of property implementation method {backingMethod.Name} on property implementation type {backingMethod.DeclaringType}");
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
