using CMinus.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace CMinus;

public interface ITrivialEventWrapper : IEventWrapperImplementation { }

public class TrivialEventWrapper : ITrivialEventWrapper, IEmptyImplementation { }

public interface IEventImplementation<[TypeKind(ImplementationTypeArgumentKind.Handler)] D> : IEventImplementation
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
    //public virtual void GenerateEventOld(IBuildingContext state, EventInfo evt)
    //{
    //    var typeBuilder = state.TypeBuilder;

    //    if (evt.EventHandlerType is null) throw new Exception($"Event {evt} has no handler type");

    //    var eventBuilder = typeBuilder.DefineEvent(evt.Name, evt.Attributes, evt.EventHandlerType);

    //    var addMethod = evt.GetAddMethod();
    //    var removeMethod = evt.GetRemoveMethod();

    //    if (addMethod is null) throw new Exception("An event must have an add method");
    //    if (removeMethod is null) throw new Exception("An event must have a remove method");

    //    var (fieldBuilder, backingAddMethodName, backingRemoveMethodName) = GetBackings(typeBuilder, evt);
    //    var backingAddMethod = fieldBuilder.FieldType.GetMethod(backingAddMethodName);
    //    var backingRemoveMethod = fieldBuilder.FieldType.GetMethod(backingRemoveMethodName);

    //    if (backingAddMethod is null) throw new Exception($"Event implementation type must have an '{backingAddMethodName}' method");
    //    if (backingRemoveMethod is null) throw new Exception($"Event implementation type must have a '{backingRemoveMethodName}' method");

    //    {
    //        var addMethodBuilder = Create(typeBuilder, addMethod, toRemove: MethodAttributes.Abstract);
    //        var generator = addMethodBuilder.GetILGenerator();
    //        GenerateWrapperCode(generator, fieldBuilder, backingAddMethod);
    //        eventBuilder.SetAddOnMethod(addMethodBuilder);
    //    }
    //    {
    //        var removeMethodBuilder = Create(typeBuilder, removeMethod, toRemove: MethodAttributes.Abstract);
    //        var generator = removeMethodBuilder.GetILGenerator();
    //        GenerateWrapperCode(generator, fieldBuilder, backingRemoveMethod);
    //        eventBuilder.SetRemoveOnMethod(removeMethodBuilder);
    //    }
    //}

    public virtual void GenerateEvent(IBuildingContext state, EventInfo evt)
    {
        var typeBuilder = state.TypeBuilder;

        var handlerType = evt.EventHandlerType;

        if (handlerType is null) throw new Exception($"Event {evt} has no handler type");

        var eventBuilder = typeBuilder.DefineEvent(evt.Name, evt.Attributes, handlerType);

        var mixinFieldBuilder = EnsureMixin(state);

        var addMethod = evt.GetAddMethod();
        var removeMethod = evt.GetRemoveMethod();

        var argumentKinds = GetArgumentKinds();

        argumentKinds[handlerType] = ImplementationTypeArgumentKind.Handler;

        var isImplementedByInterface = addMethod is not null ? state.GetOuterImplementationInfo(addMethod).IsImplememtedByInterface : false;

        var eventImplementation = GetEventImplementation(state, evt, isImplementedByInterface);

        if (eventImplementation is null) return;

        var (fieldBuilder, (addImplementation, removeImplementation)) = eventImplementation.Value;

        var codeCreator = new CodeCreation(typeBuilder, argumentKinds, fieldBuilder, mixinFieldBuilder);

        if (addMethod is not null)
        {
            var addMethodBuilder = codeCreator.CreateMethod(
                addMethod,
                addImplementation,
                handlerType,
                toRemove: MethodAttributes.Abstract
            );

            eventBuilder.SetAddOnMethod(addMethodBuilder);
        }

        if (removeMethod is not null)
        {
            var removeMethodBuilder = codeCreator.CreateMethod(
                removeMethod,
                removeImplementation,
                handlerType,
                toRemove: MethodAttributes.Abstract
            );

            eventBuilder.SetRemoveOnMethod(removeMethodBuilder);
        }
    }

    protected abstract (FieldBuilder?, EventImplementation)? GetEventImplementation(IBuildingContext state, EventInfo evt, Boolean wrap);
}

public abstract class AbstractImplementationTypeEventGenerator : AbstractEventGenerator
{
}

public class UnimplementedEventGenerator : AbstractEventGenerator
{
    public static readonly UnimplementedEventGenerator Instance = new UnimplementedEventGenerator();

    public override void GenerateEvent(IBuildingContext state, EventInfo evt) => throw new NotImplementedException();

    protected override (FieldBuilder, EventImplementation)? GetEventImplementation(IBuildingContext state, EventInfo evt, Boolean wrap)
        => throw new NotImplementedException();
}

public class GenericEventGenerator : AbstractImplementationTypeEventGenerator
{
    private readonly CheckedImplementation? implementation, wrapper;

    public GenericEventGenerator(CheckedImplementation? implementation, CheckedImplementation? wrapper)
    {
        this.implementation = implementation;
        this.wrapper = wrapper;

        implementation?.AssertWrapperOrNot(false);
        wrapper?.AssertWrapperOrNot(true);
    }

    protected override void AddArgumentKinds(Dictionary<Type, ImplementationTypeArgumentKind> argumentKinds)
    {
        implementation?.AddArgumentKinds(argumentKinds);
        wrapper?.AddArgumentKinds(argumentKinds);
    }

    //protected override (FieldBuilder fieldBuilder, String backingAddMethodName, String backingRemoveMethodName)
    //    GetBackings(TypeBuilder typeBuilder, EventInfo evt)
    //{
    //    var backingEventImplementationType = eventImplementationType.MakeGenericType(evt.EventHandlerType!);
    //    var fieldBuilder = typeBuilder.DefineField($"backing_evt_{evt.Name}", backingEventImplementationType, FieldAttributes.Private);
    //    return (fieldBuilder, "Add", "Remove");
    //}

    protected override (FieldBuilder?, EventImplementation)? GetEventImplementation(IBuildingContext state, EventInfo evt, Boolean wrap)
    {
        var outerAddImplementation = state.GetOuterImplementationInfo(evt.GetAddMethod());
        var outerRemoveImplementation = state.GetOuterImplementationInfo(evt.GetRemoveMethod());

        if (outerAddImplementation.Exists && outerRemoveImplementation.Exists)
        {
            if (outerAddImplementation.Kind != outerRemoveImplementation.Kind)
            {
                throw new Exception($"The event {evt.Name} has an add method of {outerAddImplementation.Kind} and a remove method of {outerRemoveImplementation.Kind}, but they should be the same");
            }

            if (outerAddImplementation.MixinFieldBuilder != outerRemoveImplementation.MixinFieldBuilder)
            {
                throw new Exception($"The event {evt.Name} has an should have their add and remove methods implemented by the same mixin");
            }
        }

        if (outerAddImplementation.Kind == MethodImplementationKind.ImplementedByMixin)
        {
            return (null, new EventImplementation(
                new OuterMethodImplemention(outerAddImplementation),
                new OuterMethodImplemention(outerRemoveImplementation)
            ));
        }

        var typeBuilder = state.TypeBuilder;

        var implementation = wrap ? wrapper : this.implementation;

        if (implementation is null)
        {
            // In this case, we can create the type without defining an implementation
            if (wrap && outerAddImplementation.IsMissingOrImplementedByInterface && outerRemoveImplementation.IsMissingOrImplementedByInterface) return null;

            throw new Exception($"Event {evt} needs to be {(wrap ? "wrapped" : "implemented")}, but there is no corresponding event implementation type");
        }

        var eventImplementationType = implementation.MakeImplementationType(propertyOrHandlerType: evt.EventHandlerType!);

        var fieldBuilder = typeBuilder.DefineField($"backing_{evt.Name}", eventImplementationType, FieldAttributes.Private);

        var eventImplementation = new EventImplementation(
            GetMethodImplementation(fieldBuilder, "Add", outerAddImplementation),
            GetMethodImplementation(fieldBuilder, "Remove", outerRemoveImplementation)
        );

        return (fieldBuilder, eventImplementation);
    }
}

public static class EventGenerator
{
    public static AbstractEventGenerator Create(Type? eventImplementationType, Type? eventWrapperType)
    {
        var ignoredInterfaces = new[] {
            typeof(IEventImplementation),
            typeof(IEventWrapperImplementation),
        };

        var checkedImplementation
            = eventImplementationType?.Apply(t => new CheckedImplementation(t, ignoredInterfaces));
        var checkedWrapper
            = eventWrapperType?.Apply(t => new CheckedImplementation(t, ignoredInterfaces));

        var instance = new GenericEventGenerator(checkedImplementation, checkedWrapper);

        return instance;
    }
}
