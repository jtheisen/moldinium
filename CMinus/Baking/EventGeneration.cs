using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

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

public interface IEventImplementation { }

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
    protected readonly Type eventImplementationType;

    protected AbstractEventGenerator(Type eventImplementationType)
    {
        this.eventImplementationType = eventImplementationType;
    }

    protected abstract Type GetBackingType(EventInfo evt);

    public abstract void GenerateEvent(BakingState state, EventInfo evt);
}

public class BasicEventGenerator : AbstractEventGenerator
{
    public BasicEventGenerator(Type eventImplementationType)
        : base(eventImplementationType)
    {
    }

    public override void GenerateEvent(BakingState state, EventInfo evt)
    {
        var typeBuilder = state.TypeBuilder;

        if (evt.EventHandlerType is null) throw new Exception($"Event {evt} has no handler type");

        var eventBuilder = typeBuilder.DefineEvent(evt.Name, evt.Attributes, evt.EventHandlerType);

        var addMethod = evt.GetAddMethod();
        var removeMethod = evt.GetRemoveMethod();

        if (addMethod is null) throw new Exception("An event must have an add method");
        if (removeMethod is null) throw new Exception("An event must have a remove method");

        var backingEventImplementationType = GetBackingType(evt);
        var fieldBuilder = typeBuilder.DefineField($"backing_evt_{evt.Name}", backingEventImplementationType, FieldAttributes.Private);
        var backingAddMethod = fieldBuilder.FieldType.GetMethod("Add");
        var backingRemoveMethod = fieldBuilder.FieldType.GetMethod("Remove");

        if (backingAddMethod is null) throw new Exception("Event implementation type must have an 'Add' method");
        if (backingRemoveMethod is null) throw new Exception("Event implementation type must have a 'Remove' method");

        {
            var addMethodBuilder = Create(typeBuilder, addMethod, isAbstract: false);
            var generator = addMethodBuilder.GetILGenerator();
            GenerateWrapperCode(generator, fieldBuilder, backingAddMethod);
            eventBuilder.SetAddOnMethod(addMethodBuilder);
        }
        {
            var removeMethodBuilder = Create(typeBuilder, removeMethod, isAbstract: false);
            var generator = removeMethodBuilder.GetILGenerator();
            GenerateWrapperCode(generator, fieldBuilder, backingRemoveMethod);
            eventBuilder.SetRemoveOnMethod(removeMethodBuilder);
        }
    }

    protected override Type GetBackingType(EventInfo eventInfo)
        => eventImplementationType.MakeGenericType(eventInfo.EventHandlerType!);

    void GenerateWrapperCode(ILGenerator generator, FieldBuilder fieldBuilder, MethodInfo backingMethod)
    {
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldflda, fieldBuilder);
        generator.Emit(OpCodes.Ldarg_1);
        generator.Emit(OpCodes.Call, backingMethod);
        generator.Emit(OpCodes.Ret);
    }
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
