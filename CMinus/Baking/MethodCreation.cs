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

public record WrappingMethodImplementation(MethodInfo? BeforeMethod = null, MethodInfo? AfterMethod = null) : MethodImplementation;

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
        GeneratePropertyMethodImplementationCode(generator, methodBuilder, implementation, contextType, valueType, wrappedMethod);
        return methodBuilder;
    }

    public void GeneratePropertyMethodImplementationCode(
        ILGenerator generator,
        MethodBuilder methodBuilder,
        MethodImplementation methodImplementation,
        CodeGenerationContextType contextType,
        Type valueType,
        MethodInfo? wrappedMethod = null
    )
    {
        if (methodImplementation is DirectMethodImplementation directMethodImplementation)
        {
            GenerateImplementationCode(generator, contextType, implementationFieldBuilder, directMethodImplementation.Method);
        }
        else if (methodImplementation is WrappingMethodImplementation wrappingMethodImplementation)
        {
            GenerateWrappingPropertyImplementationCode(
                generator,
                methodBuilder,
                valueType,
                wrappingMethodImplementation.BeforeMethod,
                wrappingMethodImplementation.AfterMethod,
                wrappedMethod
            );
        }
        else
        {
            throw new Exception($"Unknown method implementation {methodImplementation.GetType()}");
        }
    }

    void GenerateWrappingPropertyImplementationCode(
        ILGenerator generator,
        MethodBuilder methodBuilder,
        Type propertyType,
        MethodInfo? backingTryMethod,
        MethodInfo? backingPostMethod,
        MethodInfo? wrappedMethod
        )
    {
        //if (propertyType.IsValueType)
        //{
        //    generator.DeclareLocal(propertyType);
        //}
        //else
        //{
        //    generator.DeclareLocal(propertyType, pinned: true);
        //}

        //generator.Emit(OpCodes.Ldloca_S);
        //generator.Emit(OpCodes.Initobj);

        ////var label = generator.DefineLabel();

        ////if (backingTryMethod is not null)
        ////{
        ////    GenerateImplementationCode(
        ////        generator,
        ////        CodeGenerationContextType.Wrapper,
        ////        implementationFieldBuilder,
        ////        backingTryMethod);

        ////    //generator.Emit(OpCodes.Stloc_1);
        ////    //generator.Emit(OpCodes.Ldloc_1);
        ////    generator.Emit(OpCodes.Brfalse_S, label);
        ////    //generator.Emit(OpCodes.Nop);
        ////}

        ////if (wrappedMethod is not null)
        ////{
        ////    GenerateImplementationCode(
        ////        generator,
        ////        CodeGenerationContextType.Nested,
        ////        fieldBuilder: null,
        ////        wrappedMethod);

        ////    // FIXME: if getter, put the return into the variable
        ////}

        ////if (backingPostMethod is not null)
        ////{
        ////    GenerateImplementationCode(
        ////        generator,
        ////        CodeGenerationContextType.Wrapper,
        ////        implementationFieldBuilder,
        ////        backingPostMethod);
        ////}

        ////generator.MarkLabel(label);

        //if (methodBuilder.ReturnType != typeof(void))
        //{
        //    generator.Emit(OpCodes.Ldloc_0, 0);
        //}

        if (methodBuilder.ReturnType != typeof(void))
        {
            generator.DeclareLocal(propertyType);
            generator.Emit(OpCodes.Ldloca_S, 0);
            generator.Emit(OpCodes.Initobj);
            generator.Emit(OpCodes.Ldloc_0);
        }

        generator.Emit(OpCodes.Ret);
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
                                throw new Exception($"Passing the value to {backingMethod} is not valid");
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
