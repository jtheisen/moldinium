using System.Reflection.Emit;
using System.Reflection;
using System.Collections.Generic;
using System;
using System.Linq;
using Castle.DynamicProxy.Generators;
using System.Reflection.PortableExecutable;

namespace CMinus;

public record MethodImplementation(MethodInfo? Method = null, MethodInfo? BeforeMethod = null, MethodInfo? AfterMethod = null)
{
    public static implicit operator MethodImplementation(MethodInfo method) => new MethodImplementation(method);
}

public record PropertyImplementation(FieldBuilder FieldBuilder, MethodImplementation Get, MethodImplementation Set);

public class MethodCreation
{
    private readonly TypeBuilder typeBuilder;
    private readonly IDictionary<Type, ImplementationTypeArgumentKind> argumentKinds;
    private readonly FieldBuilder? implementationFieldBuilder;
    private readonly FieldBuilder? mixInFieldBuilder;

    public MethodCreation(
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

    public MethodBuilder CreatePropertyMethod(
        CodeGenerationContextType contextType,
        MethodInfo methodTemplate,
        MethodInfo wrappedMethod,
        MethodImplementation implementation,
        Type valueType,
        MethodAttributes toAdd = default,
        MethodAttributes toRemove = default
    )
    {
        var methodBuilder = Declare(typeBuilder, methodTemplate, toAdd, toRemove);
        var generator = methodBuilder.GetILGenerator();
        GeneratePropertyMethodImplementationCode(generator, implementation, contextType, valueType, wrappedMethod);
        return methodBuilder;
    }

    public void GeneratePropertyMethodImplementationCode(
        ILGenerator generator,
        MethodImplementation methodImplementation,
        CodeGenerationContextType contextType,
        Type valueType,
        MethodInfo? wrappedMethod = null
    )
    {
        var haveBefore = methodImplementation.BeforeMethod is not null;
        var haveAfter = methodImplementation.AfterMethod is not null;

        if (methodImplementation.Method is MethodInfo backingMethod)
        {
            if (haveBefore || haveAfter) throw new Exception("Internal error: didn't expect to have a before or after method");

            GenerateImplementationCode(generator, contextType, implementationFieldBuilder, backingMethod);
        }
        else if (haveBefore || haveAfter)
        {
            GenerateWrappingPropertyImplementationCode(
                generator,
                valueType,
                methodImplementation.BeforeMethod,
                methodImplementation.AfterMethod,
                wrappedMethod
            );
        }
        else
        {
            if (haveBefore || haveAfter) throw new Exception("Internal error: no method implementation given");
        }
    }

    void GenerateWrappingPropertyImplementationCode(
        ILGenerator generator,
        Type propertyType,
        MethodInfo? backingTryMethod,
        MethodInfo? backingPostMethod,
        MethodInfo? wrappedMethod
        )
    {
        if (propertyType.IsValueType)
        {
            generator.DeclareLocal(propertyType);
        }
        else
        {
            generator.DeclareLocal(propertyType, pinned: true);
        }

        generator.Emit(OpCodes.Ldloca_S, 0);
        generator.Emit(OpCodes.Initobj);

        var label = generator.DefineLabel();

        if (backingTryMethod is not null)
        {
            GenerateImplementationCode(
                generator,
                CodeGenerationContextType.Wrapper,
                implementationFieldBuilder,
                backingTryMethod);

            //generator.Emit(OpCodes.Stloc_1);
            //generator.Emit(OpCodes.Ldloc_1);
            generator.Emit(OpCodes.Brfalse_S, label);
            //generator.Emit(OpCodes.Nop);
        }

        if (wrappedMethod is not null)
        {
            GenerateImplementationCode(
                generator,
                CodeGenerationContextType.Nested,
                fieldBuilder: null,
                wrappedMethod);
        }

        if (backingPostMethod is not null)
        {
            GenerateImplementationCode(
                generator,
                CodeGenerationContextType.Wrapper,
                implementationFieldBuilder,
                backingPostMethod);
        }

        generator.MarkLabel(label);
    }

    public void GenerateImplementationCode(
        ILGenerator generator,
        CodeGenerationContextType methodType,
        FieldBuilder? fieldBuilder,
        MethodInfo backingMethod,
        FieldBuilder? defaultImplementationFieldBuilder = null,
        MethodInfo? defaultImplementationGetMethod = null
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

        var parameters = backingMethod.GetParameters();

        foreach (var p in parameters)
        {
            if (p.ParameterType == typeof(Object))
            {
                generator.Emit(OpCodes.Ldarg_0);
            }
            else
            {
                var (parameterType, byRef) = GetParameterType(p);

                if (!argumentKinds.TryGetValue(parameterType, out var kind))
                {
                    throw new Exception($"Dont know how to handle argument {p.Name} of type {p.ParameterType} of property implementation method {backingMethod.Name} on property implementation type {backingMethod.DeclaringType}");
                }

                switch (kind)
                {
                    case ImplementationTypeArgumentKind.Value:
                        switch (methodType)
                        {
                            case CodeGenerationContextType.Wrapper:
                                if (!byRef) throw new Exception($"Value types to before and after methods need to be passed by ref");
                                break;
                            default:
                                if (byRef) throw new Exception($"Value types need to be passed by value");
                                break;
                        }

                        switch (methodType)
                        {
                            case CodeGenerationContextType.Constructor:
                                if (defaultImplementationFieldBuilder is null || defaultImplementationGetMethod is null)
                                {
                                    throw new Exception("Expected to have a fieldBuilder with a get method");
                                }
                                generator.Emit(OpCodes.Ldarg_0);
                                generator.Emit(OpCodes.Ldflda, defaultImplementationFieldBuilder);
                                generator.Emit(OpCodes.Call, defaultImplementationGetMethod);
                                break;
                            case CodeGenerationContextType.Set:
                                generator.Emit(OpCodes.Ldarg_1);
                                break;
                            case CodeGenerationContextType.Wrapper:
                                generator.Emit(OpCodes.Ldloca_S, 0);
                                break;
                            default:
                                break;
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
        }

        generator.Emit(OpCodes.Call, backingMethod);

        if (methodType != CodeGenerationContextType.Constructor)
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
