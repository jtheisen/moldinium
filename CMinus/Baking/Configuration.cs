﻿using System;
using System.Reflection;

namespace CMinus;

public struct Dummy { }

public interface IBakeryComponentGenerators
{
    MixInGenerator[] GetMixInGenerators(Type type);

    AbstractPropertyGenerator GetPropertyGenerator(PropertyInfo property);

    AbstractEventGenerator GetEventGenerator(EventInfo evt);
}

public class ComponentGenerator : IBakeryComponentGenerators
{
    private readonly AbstractPropertyGenerator propertyGenerator;
    private readonly AbstractEventGenerator eventGenerator;

    public ComponentGenerator(AbstractPropertyGenerator propertyGenerator, AbstractEventGenerator eventGenerator)
    {
        this.propertyGenerator = propertyGenerator;
        this.eventGenerator = eventGenerator;
    }

    public MixInGenerator[] GetMixInGenerators(Type type) => new MixInGenerator[] { };

    public AbstractPropertyGenerator GetPropertyGenerator(PropertyInfo property) => propertyGenerator;

    public AbstractEventGenerator GetEventGenerator(EventInfo evt) => eventGenerator;

    public static ComponentGenerator Create(Type propertyImplementationType, Type eventImplementationType)
        => new ComponentGenerator(
            PropertyGenerator.Create(propertyImplementationType),
            EventGenerator.Create(eventImplementationType)
        );
}

public record BakeryConfiguration(IBakeryComponentGenerators Generators, Boolean MakeAbstract = false)
{
    public static BakeryConfiguration Create(Type? propertyImplementationType = null, Type? eventImplementationType = null)
        => new BakeryConfiguration(ComponentGenerator.Create(
            propertyImplementationType ?? typeof(GenericPropertyImplementation<>),
            eventImplementationType ?? typeof(GenericEventImplementation<>)));

    public static BakeryConfiguration PocGenerationConfiguration
        = new BakeryConfiguration(ComponentGenerator.Create(typeof(GenericPropertyImplementation<>), typeof(GenericEventImplementation<>)));

    public Bakery CreateBakery(String name) => new Bakery(name, this);
}

public class MixInGenerator
{
}
