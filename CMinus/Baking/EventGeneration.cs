using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.PortableExecutable;

namespace CMinus;

[AttributeUsage(AttributeTargets.Interface)]
public class EventImplementationInterfaceAttribute : Attribute
{
    public Type EventGeneratorType { get; }

    public EventImplementationInterfaceAttribute(Type eventGeneratorType)
    {
        EventGeneratorType = eventGeneratorType;
    }
}

public interface IEventImplementation : IImplementation { }

[EventImplementationInterface(typeof(BasicEventGenerator))]
public interface IEventImplementation<D> : IEventImplementation
    where D : Delegate
{
    void Add(D d);

    void Remove(D d);
}

public struct GenericEventImplementation<D> : IEventImplementation<D>
    where D : Delegate
{
    D handler;

    public void Add(D value) => handler = (D)Delegate.Combine(handler, value);

    public void Remove(D value) => Delegate.Remove(handler, value);
}

public abstract class AbstractEventGenerator : AbstractGenerator
{
    public virtual void GenerateEvent(BakingState state, EventInfo evt)
    {
        var typeBuilder = state.TypeBuilder;

        if (evt.EventHandlerType is null) throw new Exception($"Event {evt} has no handler type");

        var eventBuilder = typeBuilder.DefineEvent(evt.Name, evt.Attributes, evt.EventHandlerType);

        var addMethod = evt.GetAddMethod();
        var removeMethod = evt.GetRemoveMethod();

        if (addMethod is null) throw new Exception("An event must have an add method");
        if (removeMethod is null) throw new Exception("An event must have a remove method");

        var (fieldBuilder, backingAddMethodName, backingRemoveMethodName) = GetBackings(typeBuilder, evt);
        var backingAddMethod = fieldBuilder.FieldType.GetMethod(backingAddMethodName);
        var backingRemoveMethod = fieldBuilder.FieldType.GetMethod(backingRemoveMethodName);

        if (backingAddMethod is null) throw new Exception($"Event implementation type must have an '{backingAddMethodName}' method");
        if (backingRemoveMethod is null) throw new Exception($"Event implementation type must have a '{backingRemoveMethodName}' method");

        {
            var addMethodBuilder = Create(typeBuilder, addMethod, toRemove: MethodAttributes.Abstract);
            var generator = addMethodBuilder.GetILGenerator();
            GenerateWrapperCode(generator, fieldBuilder, backingAddMethod);
            eventBuilder.SetAddOnMethod(addMethodBuilder);
        }
        {
            var removeMethodBuilder = Create(typeBuilder, removeMethod, toRemove: MethodAttributes.Abstract);
            var generator = removeMethodBuilder.GetILGenerator();
            GenerateWrapperCode(generator, fieldBuilder, backingRemoveMethod);
            eventBuilder.SetRemoveOnMethod(removeMethodBuilder);
        }
    }

    protected abstract (FieldBuilder fieldBuilder, String backingAddMethodName, String backingRemoveMethodName) GetBackings(TypeBuilder typeBuilder, EventInfo evt);

    protected virtual void GenerateWrapperCode(ILGenerator generator, FieldBuilder fieldBuilder, MethodInfo backingMethod)
    {
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldflda, fieldBuilder);
        generator.Emit(OpCodes.Ldarg_1);
        generator.Emit(OpCodes.Call, backingMethod);
        generator.Emit(OpCodes.Ret);
    }
}

public abstract class AbstractImplementationTypeEventGenerator : AbstractEventGenerator
{
    protected readonly Type eventImplementationType;

    public AbstractImplementationTypeEventGenerator(Type eventImplementationType)
    {
        this.eventImplementationType = eventImplementationType;
    }
}

public class UnimplementedEventGenerator : AbstractEventGenerator
{
    public static readonly UnimplementedEventGenerator Instance = new UnimplementedEventGenerator();

    public override void GenerateEvent(BakingState state, EventInfo evt) => throw new NotImplementedException();

    protected override (FieldBuilder fieldBuilder, String backingAddMethodName, String backingRemoveMethodName)
        GetBackings(TypeBuilder typeBuilder, EventInfo property)
        => throw new NotImplementedException();
}

public class BasicEventGenerator : AbstractImplementationTypeEventGenerator
{
    public BasicEventGenerator(Type eventImplementationType)
        : base(eventImplementationType)
    {
    }

    protected override (FieldBuilder fieldBuilder, String backingAddMethodName, String backingRemoveMethodName)
        GetBackings(TypeBuilder typeBuilder, EventInfo evt)
    {
        var backingEventImplementationType = eventImplementationType.MakeGenericType(evt.EventHandlerType!);
        var fieldBuilder = typeBuilder.DefineField($"backing_evt_{evt.Name}", backingEventImplementationType, FieldAttributes.Private);
        return (fieldBuilder, "Add", "Remove");
    }
}

public class DelegatingEventGenerator : AbstractEventGenerator
{
    private readonly FieldBuilder fieldBuilder;

    public DelegatingEventGenerator(FieldBuilder targetFieldBuilder)
    {
        this.fieldBuilder = targetFieldBuilder;
    }

    protected override (FieldBuilder fieldBuilder, String backingAddMethodName, String backingRemoveMethodName)
        GetBackings(TypeBuilder typeBuilder, EventInfo evt) => (fieldBuilder, evt.AddMethod!.Name, evt.RemoveMethod!.Name);
}

public static class EventGenerator
{
    public static AbstractEventGenerator Create(Type eventImplementationType)
    {
        var candidates =
            from i in eventImplementationType.GetInterfaces()
            let a = i.GetCustomAttribute<EventImplementationInterfaceAttribute>()
            where a is not null
            select a;

        var attribute = candidates.Single();

        var instance = Activator.CreateInstance(attribute.EventGeneratorType, eventImplementationType);

        return instance as AbstractEventGenerator ?? throw new Exception("Activator returned a null or incorrect type");
    }
}
