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
    public virtual void GenerateProperty(BakingState state, PropertyInfo property)
    {
        var typeBuilder = state.TypeBuilder;

        var propertyBuilder = typeBuilder.DefineProperty(property.Name, property.Attributes, property.PropertyType, null);

        var mixinFieldBuilder = EnsureMixin(state);

        var getMethod = property.GetGetMethod();
        var setMethod = property.GetSetMethod();

        if (setMethod is not null)
        {
            if (getMethod is null) throw new Exception("A writable property must also be readable");

            var (fieldBuilder, backingGetMethod, backingSetMethod) = GetBackings(typeBuilder, property);

            {
                var getMethodBuilder = Create(typeBuilder, getMethod, isAbstract: false);
                var generator = getMethodBuilder.GetILGenerator();
                GenerateGetterCode(generator, fieldBuilder, backingGetMethod, mixinFieldBuilder);
                propertyBuilder.SetGetMethod(getMethodBuilder);
            }
            {
                var setMethodBuilder = Create(typeBuilder, setMethod, isAbstract: false);
                var generator = setMethodBuilder.GetILGenerator();
                GenerateSetterCode(generator, fieldBuilder, backingSetMethod, mixinFieldBuilder);
                propertyBuilder.SetSetMethod(setMethodBuilder);
            }
        }
        else if (getMethod is not null)
        {
            propertyBuilder.SetGetMethod(Create(typeBuilder, getMethod));
        }
        else
        {
            throw new Exception("A property that is neither readable nor writable was encountered");
        }
    }

    protected virtual FieldBuilder? EnsureMixin(BakingState state) => null;

    protected abstract (FieldBuilder fieldBuilder, MethodInfo backingGetMethod, MethodInfo backingSetMethod) GetBackings(TypeBuilder typeBuilder, PropertyInfo property);

    protected MethodInfo GetMethod(FieldBuilder fieldBuilder, String name)
        => fieldBuilder.FieldType.GetMethod(name)
        ?? throw new Exception($"Property implementation type {fieldBuilder.FieldType} must have a '{name}' method");

    protected MethodInfo GetPropertyMethod(PropertyInfo property, Boolean setter)
        => (setter ? property.GetSetMethod() : property.GetGetMethod())
        ?? throw new Exception($"Property {property.Name} on implementation type {property.DeclaringType} must have a {(setter ? "setter" : "getter")} method");

    protected virtual void GenerateGetterCode(ILGenerator generator, FieldBuilder fieldBuilder, MethodInfo backingGetMethod, FieldBuilder? _)
    {
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldflda, fieldBuilder);
        generator.Emit(OpCodes.Call, backingGetMethod);
        generator.Emit(OpCodes.Ret);
    }

    protected virtual void GenerateSetterCode(ILGenerator generator, FieldBuilder fieldBuilder, MethodInfo backingSetMethod, FieldBuilder? _)
    {
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldflda, fieldBuilder);
        generator.Emit(OpCodes.Ldarg_1);
        generator.Emit(OpCodes.Call, backingSetMethod);
        generator.Emit(OpCodes.Ret);
    }
}

public abstract class AbstractImplementationTypePropertyGenerator : AbstractPropertyGenerator
{
    protected readonly Type propertyImplementationType;

    protected AbstractImplementationTypePropertyGenerator(Type propertyImplementationType)
    {
        this.propertyImplementationType = propertyImplementationType;
    }
}

public class UnimplementedPropertyGenerator : AbstractPropertyGenerator
{
    public static readonly UnimplementedPropertyGenerator Instance = new UnimplementedPropertyGenerator();

    public override void GenerateProperty(BakingState state, PropertyInfo property) => throw new NotImplementedException();

    protected override (FieldBuilder fieldBuilder, MethodInfo backingGetMethod, MethodInfo backingSetMethod)
        GetBackings(TypeBuilder typeBuilder, PropertyInfo property)
        => throw new NotImplementedException();
}

public class BasicPropertyGenerator : AbstractImplementationTypePropertyGenerator
{
    public BasicPropertyGenerator(Type propertyImplementationType) : base(propertyImplementationType) { }

    protected override (FieldBuilder fieldBuilder, MethodInfo backingGetMethod, MethodInfo backingSetMethod)
        GetBackings(TypeBuilder typeBuilder, PropertyInfo property)
    {
        var backingPropertyImplementationType = propertyImplementationType.MakeGenericType(property.PropertyType);
        var fieldBuilder = typeBuilder.DefineField($"backing_{property.Name}", backingPropertyImplementationType, FieldAttributes.Private);
        var backingProperty = backingPropertyImplementationType.GetProperty("Value");
        if (backingProperty is null) throw new Exception($"Property implementation type {backingPropertyImplementationType.Name} must have a 'Value' property");
        return (fieldBuilder, GetPropertyMethod(backingProperty, false), GetPropertyMethod(backingProperty, true));
    }
}

public class DelegatingPropertyGenerator : AbstractPropertyGenerator
{
    private readonly FieldBuilder fieldBuilder;

    public DelegatingPropertyGenerator(FieldBuilder targetFieldBuilder)
    {
        this.fieldBuilder = targetFieldBuilder;
    }

    protected override (FieldBuilder fieldBuilder, MethodInfo backingGetMethod, MethodInfo backingSetMethod)
        GetBackings(TypeBuilder typeBuilder, PropertyInfo property)
        => (fieldBuilder, GetPropertyMethod(property, false), GetPropertyMethod(property, true));
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

    protected override FieldBuilder? EnsureMixin(BakingState state) => state.EnsureMixin(state, mixinType);

    protected override (FieldBuilder fieldBuilder, MethodInfo backingGetMethod, MethodInfo backingSetMethod)
        GetBackings(TypeBuilder typeBuilder, PropertyInfo property)
    {
        var backingEventImplementationType = propertyImplementationType.MakeGenericType(property.PropertyType, property.DeclaringType!);
        var fieldBuilder = typeBuilder.DefineField($"backing_{property.Name}", backingEventImplementationType, FieldAttributes.Private);
        return (fieldBuilder, GetMethod(fieldBuilder, "Get"), GetMethod(fieldBuilder, "Set"));
    }

    protected override void GenerateGetterCode(ILGenerator generator, FieldBuilder fieldBuilder, MethodInfo backingGetMethod, FieldBuilder? mixInFieldBuilder)
    {
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldflda, fieldBuilder);

        generator.Emit(OpCodes.Ldarg_0);

        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldflda, mixInFieldBuilder!);

        generator.Emit(OpCodes.Call, backingGetMethod);

        generator.Emit(OpCodes.Ret);
    }

    protected override void GenerateSetterCode(ILGenerator generator, FieldBuilder fieldBuilder, MethodInfo backingSetMethod, FieldBuilder? mixInFieldBuilder)
    {
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldflda, fieldBuilder);

        generator.Emit(OpCodes.Ldarg_0);

        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldflda, mixInFieldBuilder!);

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

