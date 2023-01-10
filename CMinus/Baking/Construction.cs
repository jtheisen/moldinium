using System;
using System.Reflection;

namespace CMinus.Construction;

//public interface IPropertyImplementation
//        <ValueInterface, ContainerInterface, Value, Container, MixIn>
//        where Value : ValueInterface
//        where Container : ContainerInterface
//        where MixIn : struct
//{
//    Value Get(
//        Container self,
//        ref MixIn mixIn
//        );

//    void Set(
//        Container self,
//        ref MixIn mixIn,
//        Value value
//        );
//}

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

// Indeed mysterious
public interface IMysteriousAccessor<Value, Container>
{
    String GetPropertyName();
    Int32 GetIndex();
    Boolean IsVariable();

    Value Get(Container container);
    void Set(Container container, Value value);
}

public struct EmptyMixIn { }

[PropertyImplementationInterfaceAttribute(typeof(BasicPropertyGenerator))]
public interface IPropertyImplementation<T> : IPropertyImplementation
{
    T Value { get; set; }
}

public struct GenericPropertyImplementation<T> : IPropertyImplementation<T>
{
    public T Value { get; set; }
}



public interface IReadonlyProperty<T>
{
    T Value { get; }
}

public interface IWrappedReadonlyProperty<T, B> : IReadonlyProperty<T>
    where B : struct
{
}

public interface ICachingReadonlyProperty<T, B> : IWrappedReadonlyProperty<T, B>
    where B : struct
{
    void Invalidate();
}

public struct CachedReadonlyProperty<T, B> : ICachingReadonlyProperty<T, B>
    where B : struct, IReadonlyProperty<T>
{
    T cached;
    Boolean isCached;
    B nested;

    public void Invalidate() => isCached = false;

    public T Value
    {
        get
        {
            if (!isCached)
            {
                cached = nested.Value;

                isCached = true;
            }

            return cached;
        }
    }
}
