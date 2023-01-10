using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Threading;

namespace CMinus;

[AttributeUsage(AttributeTargets.Interface)]
public class PropertyImplementationInterfaceAttribute : Attribute
{
    public Type PropertyGeneratorType { get; }

    public PropertyImplementationInterfaceAttribute(Type propertyGeneratorType)
    {
        PropertyGeneratorType = propertyGeneratorType;
    }
}

public interface IPropertyImplementation { }

[PropertyImplementationInterface(typeof(BasicPropertyGenerator))]
public interface IPropertyImplementation<T> : IPropertyImplementation
{
    T Value { get; set; }
}

[PropertyImplementationInterface(typeof(ComplexPropertyGenerator))]
public interface IPropertyImplementation<Value, Container, MixIn> : IPropertyImplementation
        where MixIn : struct
{
    Value Get(
        Container self,
        ref MixIn mixIn
        );

    void Set(
        Container self,
        ref MixIn mixIn,
        Value value
        );
}

public struct EmptyMixIn { }

public struct GenericPropertyImplementation<T> : IPropertyImplementation<T>
{
    public T Value { get; set; }
}

public abstract class AbstractGenerator
{
    protected MethodBuilder Create(TypeBuilder typeBuilder, MethodInfo methodTemplate, Boolean isAbstract = true)
    {
        var attributes = methodTemplate.Attributes | MethodAttributes.Public;

        if (!isAbstract) attributes &= ~MethodAttributes.Abstract;

        var parameters = methodTemplate.GetParameters();

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

        return methodBuilder;
    }
}

public abstract class AbstractPropertyGenerator : AbstractGenerator
{
    public abstract void GenerateProperty(BakingState state, PropertyInfo property);
}

public abstract class AbstractImplementationTypePropertyGenerator : AbstractPropertyGenerator
{
    protected readonly Type propertyImplementationType;

    protected AbstractImplementationTypePropertyGenerator(Type propertyImplementationType)
    {
        this.propertyImplementationType = propertyImplementationType;
    }

    protected abstract Type GetBackingType(PropertyInfo property);
}

public class UnimplementedPropertyGenerator : AbstractPropertyGenerator
{
    public static readonly UnimplementedPropertyGenerator Instance = new UnimplementedPropertyGenerator();

    public override void GenerateProperty(BakingState state, PropertyInfo property) => throw new NotImplementedException();
}

public class BasicPropertyGenerator : AbstractImplementationTypePropertyGenerator
{
    public BasicPropertyGenerator(Type propertyImplementationType) : base(propertyImplementationType) { }

    public override void GenerateProperty(BakingState state, PropertyInfo property)
    {
        var typeBuilder = state.TypeBuilder;

        var propertyBuilder = typeBuilder.DefineProperty(property.Name, property.Attributes, property.PropertyType, null);

        var setMethod = property.GetSetMethod();
        var getMethod = property.GetGetMethod();

        if (setMethod != null)
        {
            if (getMethod == null) throw new Exception("A writable property must also be readable");

            var backingPropertyImplementationType = GetBackingType(property);
            var fieldBuilder = typeBuilder.DefineField($"backing_{property.Name}", backingPropertyImplementationType, FieldAttributes.Private);
            var backingProperty = backingPropertyImplementationType.GetProperty("Value");
            var backingGetMethod = backingProperty?.GetGetMethod();
            var backingSetMethod = backingProperty?.GetSetMethod();

            if (backingGetMethod == null) throw new Exception("Type must have a readable 'Value' property");
            if (backingSetMethod == null) throw new Exception("Type must have a writable 'Value' property");

            {
                var getMethodBuilder = Create(typeBuilder, getMethod, isAbstract: false);
                var generator = getMethodBuilder.GetILGenerator();
                GenerateGetterCode(generator, fieldBuilder, backingGetMethod);
                propertyBuilder.SetGetMethod(getMethodBuilder);
            }
            {
                var setMethodBuilder = Create(typeBuilder, setMethod, isAbstract: false);
                var generator = setMethodBuilder.GetILGenerator();
                GenerateSetterCode(generator, fieldBuilder, backingSetMethod);
                propertyBuilder.SetSetMethod(setMethodBuilder);
            }
        }
        else if (getMethod != null)
        {
            propertyBuilder.SetGetMethod(Create(typeBuilder, getMethod));
        }
        else
        {
            throw new Exception("A property that is neither readable nor writable was encountered");
        }
    }

    protected override Type GetBackingType(PropertyInfo property)
        => propertyImplementationType.MakeGenericType(property.PropertyType);

    void GenerateGetterCode(ILGenerator generator, FieldBuilder fieldBuilder, MethodInfo backingGetMethod)
    {
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldflda, fieldBuilder);
        generator.Emit(OpCodes.Call, backingGetMethod);
        generator.Emit(OpCodes.Ret);
    }

    void GenerateSetterCode(ILGenerator generator, FieldBuilder fieldBuilder, MethodInfo backingSetMethod)
    {
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldflda, fieldBuilder);
        generator.Emit(OpCodes.Ldarg_1);
        generator.Emit(OpCodes.Call, backingSetMethod);
        generator.Emit(OpCodes.Ret);
    }
}

public class ComplexPropertyGenerator : AbstractImplementationTypePropertyGenerator
{
    Type semiConcreteInterface;
    Type mixinType;

    public ComplexPropertyGenerator(Type propertyImplementationType) : base(propertyImplementationType)
    {
        var complexInterfaceBaseType = typeof(IPropertyImplementation<,,>);

        semiConcreteInterface = propertyImplementationType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == complexInterfaceBaseType)
            ?? throw new Exception($"Property implementation type {propertyImplementationType} does not implement {complexInterfaceBaseType}");

        var implementationTypeArguments = propertyImplementationType.GetGenericArguments();

        if (implementationTypeArguments.Length != 2) throw new Exception($"Expected complex property type implementation to have two type parameters and found {implementationTypeArguments.Length}");

        var valueType = implementationTypeArguments[0];
        var containerType = implementationTypeArguments[1];

        if (!valueType.IsGenericTypeParameter)
        {
            throw new Exception($"Expected type paramter Value not to be concrete in property implementation type ${propertyImplementationType}");
        }

        if (!containerType.IsGenericTypeParameter)
        {
            throw new Exception($"Expected type paramter Container not to be concrete in property implementation type ${propertyImplementationType}");
        }

        var semiConcreteInterfaceArguments = semiConcreteInterface.GetGenericArguments();

        if (semiConcreteInterfaceArguments.Length != 3) throw new Exception($"Unexpected length of type arguments");

        mixinType = semiConcreteInterfaceArguments[2];

        if (mixinType.IsGenericTypeParameter)
        {
            throw new Exception($"Expected type paramter Mixin to be concrete in property implementation type ${propertyImplementationType}");
        }
    }

    public override void GenerateProperty(BakingState state, PropertyInfo property)
    {
        var typeBuilder = state.TypeBuilder;

        var propertyBuilder = typeBuilder.DefineProperty(property.Name, property.Attributes, property.PropertyType, null);

        var mixin = state.EnsureMixin(state, mixinType);

        var setMethod = property.GetSetMethod();
        var getMethod = property.GetGetMethod();

        if (setMethod != null)
        {
            if (getMethod == null) throw new Exception("A writable property must also be readable");

            var backingPropertyImplementationType = GetBackingType(property);
            var fieldBuilder = typeBuilder.DefineField($"backing_{property.Name}", backingPropertyImplementationType, FieldAttributes.Private);

            var backingGetMethod = fieldBuilder.FieldType.GetMethod("Get");
            var backingSetMethod = fieldBuilder.FieldType.GetMethod("Set");

            if (backingGetMethod is null) throw new Exception("Type must have a 'Get' method");
            if (backingSetMethod is null) throw new Exception("Type must have a 'Set' method");

            {
                var getMethodBuilder = Create(typeBuilder, getMethod, isAbstract: false);
                var generator = getMethodBuilder.GetILGenerator();
                GenerateGetterCode(generator, fieldBuilder, backingGetMethod, mixin);
                propertyBuilder.SetGetMethod(getMethodBuilder);
            }
            {
                var setMethodBuilder = Create(typeBuilder, setMethod, isAbstract: false);
                var generator = setMethodBuilder.GetILGenerator();
                GenerateSetterCode(generator, fieldBuilder, backingSetMethod, mixin);
                propertyBuilder.SetSetMethod(setMethodBuilder);
            }
        }
        else if (getMethod != null)
        {
            propertyBuilder.SetGetMethod(Create(typeBuilder, getMethod));
        }
        else
        {
            throw new Exception("A property that is neither readable nor writable was encountered");
        }
    }

    protected override Type GetBackingType(PropertyInfo property)
        => propertyImplementationType.MakeGenericType(property.PropertyType, property.DeclaringType!);

    void GenerateGetterCode(ILGenerator generator, FieldBuilder fieldBuilder, MethodInfo backingGetMethod, FieldBuilder mixInFieldBuilder)
    {
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldflda, fieldBuilder);

        generator.Emit(OpCodes.Ldarg_0);

        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldflda, mixInFieldBuilder);

        generator.Emit(OpCodes.Call, backingGetMethod);

        generator.Emit(OpCodes.Ret);
    }

    void GenerateSetterCode(ILGenerator generator, FieldBuilder fieldBuilder, MethodInfo backingSetMethod, FieldBuilder mixInFieldBuilder)
    {
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldflda, fieldBuilder);

        generator.Emit(OpCodes.Ldarg_0);

        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldflda, mixInFieldBuilder);

        generator.Emit(OpCodes.Ldarg_1);

        generator.Emit(OpCodes.Call, backingSetMethod);
        generator.Emit(OpCodes.Ret);
    }
}

public static class PropertyGenerator
{
    public static AbstractPropertyGenerator Create(Type propertyImplementationType)
    {
        var candidates =
            from i in propertyImplementationType.GetInterfaces()
            let a = i.GetCustomAttribute<PropertyImplementationInterfaceAttribute>()
            where a is not null
            select a;

        var attribute = candidates.Single();

        var instance = Activator.CreateInstance(attribute.PropertyGeneratorType, propertyImplementationType);

        return instance as AbstractPropertyGenerator ?? throw new Exception("Activator returned a null or incorrect type");
    }
}

