using CMinus.Baking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;

namespace CMinus;

public interface IBuildingContext
{
    TypeBuilder TypeBuilder { get; }
    ILGenerator ConstructorGenerator { get; }
    IDefaultProvider DefaultProvider { get; }

    FieldBuilder EnsureMixin(Type type, Boolean isPrivate);
}

public abstract class BakingProcessorWithComponentGenerators
{
    IBakeryComponentGenerators generators;

    public BakingProcessorWithComponentGenerators(IBakeryComponentGenerators generators)
    {
        this.generators = generators;
    }

    public void Visit(Type type)
    {
        if (type.IsInterface)
        {
            VisitInterface(type);
        }

        foreach (var ifc in type.GetInterfaces())
        {
            VisitInterface(ifc);
        }

        VisitMembers(type);
    }

    protected void VisitMembers(Type type)
    {
        foreach (var property in type.GetProperties())
        {
            VisitProperty(property, generators.GetPropertyGenerator(property));
        }

        foreach (var evt in type.GetEvents())
        {
            VisitEvent(evt, generators.GetEventGenerator(evt));
        }

        foreach (var method in type.GetMethods())
        {
            if (method.IsSpecialName) continue;

            VisitMethod(method, generators.GetMethodGenerator(method));
        }
    }

    protected virtual void VisitInterface(Type type) { }

    protected abstract void VisitProperty(PropertyInfo property, AbstractPropertyGenerator? generator);
    protected abstract void VisitEvent(EventInfo evt, AbstractEventGenerator? generator);
    protected abstract void VisitMethod(MethodInfo method, AbstractMethodGenerator? generator);
}

public class BuildingBakingProcessor : BakingProcessorWithComponentGenerators, IBuildingContext
{
    private readonly IDefaultProvider defaultProvider;
    private readonly IBakeryComponentGenerators generators;
    private readonly TypeBuilder typeBuilder;
    private readonly ILGenerator constructorGenerator;

    private readonly HashSet<Type> interfacesAndBases = new HashSet<Type>();
    private readonly Dictionary<Type, FieldBuilder> mixins = new Dictionary<Type, FieldBuilder>();

    public IDefaultProvider DefaultProvider => defaultProvider;
    public TypeBuilder TypeBuilder => typeBuilder;
    public ILGenerator ConstructorGenerator => constructorGenerator;

    public BuildingBakingProcessor(
        String name, Type? baseType, TypeAttributes typeAttributes,
        IDefaultProvider defaultProvider, IBakeryComponentGenerators generators, ModuleBuilder moduleBuilder
    )
        : base(generators)
    {
        this.defaultProvider = defaultProvider;
        this.generators = generators;

        typeBuilder = moduleBuilder.DefineType(name, typeAttributes, baseType);

        var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { });

        constructorGenerator = constructorBuilder.GetILGenerator();
    }

    public Type Create(Type interfaceOrBaseOrMixinType)
    {
        Visit(interfaceOrBaseOrMixinType);

        constructorGenerator.Emit(OpCodes.Ret);

        return typeBuilder.CreateType() ?? throw new Exception("Internal error: got no type from type builder");
    }

    void ImplementBaseOrInterface(Type type, IBakeryComponentGenerators generators)
    {
        ImplementBaseOrInterfaceCore(type, generators);

        ImplementInterfaces(type, generators);
    }

    void ImplementInterfaces(Type type, IBakeryComponentGenerators generators)
    {
        var interfaces = type.GetInterfaces();

        foreach (var ifc in interfaces)
        {
            ImplementBaseOrInterfaceCore(ifc, generators);
        }
    }

    void ImplementBaseOrInterfaceCore(Type type, IBakeryComponentGenerators generators)
    {
        if (interfacesAndBases.Contains(type)) return;

        interfacesAndBases.Add(type);

        if (type.IsInterface)
        {
            typeBuilder.AddInterfaceImplementation(type);
        }

        foreach (var property in type.GetProperties())
        {
            generators.GetPropertyGenerator(property)?.GenerateProperty(this, property);
        }

        foreach (var evt in type.GetEvents())
        {
            generators.GetEventGenerator(evt).GenerateEvent(this, evt);
        }

        foreach (var method in type.GetMethods())
        {
            if (method.IsSpecialName) continue;

            generators.GetMethodGenerator(method)?.GenerateMethod(this, method);
        }
    }

    FieldBuilder EnsureDelegatingMixin(Type type, Boolean isPrivate)
        => EnsureMixin(type, true, isPrivate);

    FieldBuilder EnsureMixin(Type type, Boolean onlyDelegate, Boolean isPrivate)
    {
        var fieldBuilder = mixins.GetValueOrDefault(type);

        if (fieldBuilder is null)
        {
            CreateMixin(type, onlyDelegate, isPrivate, out fieldBuilder);
        }

        return fieldBuilder;
    }

    void CreateMixin(Type type, Boolean onlyDelegate, Boolean isPrivate, out FieldBuilder fieldBuilder)
    {
        fieldBuilder = typeBuilder.DefineField($"mixin_{(type.FullName ?? "x").Replace('.', '_')}_{Guid.NewGuid()}", type, FieldAttributes.Private);

        mixins[type] = fieldBuilder;

        var propertyImplementationType = typeof(IPropertyImplementation);

        var interfaces = (
            from i in type.GetInterfaces()
            where !propertyImplementationType.IsAssignableFrom(i)
            select i
        ).ToArray();

        if (interfaces.Length == 0) return;

        if (!isPrivate)
        {
            var nestedGenerators = onlyDelegate
                ? new ComponentGenerators(
                    new DelegatingMethodGenerator(fieldBuilder),
                    new DelegatingPropertyGenerator(fieldBuilder),
                    new DelegatingPropertyGenerator(fieldBuilder),
                    new DelegatingEventGenerator(fieldBuilder)
                )
                : generators;

            foreach (var ifc in interfaces)
            {
                ImplementBaseOrInterface(ifc, nestedGenerators);
            }
        }
    }

    protected override void VisitInterface(Type type) => ImplementBaseOrInterface(type, generators);

    protected override void VisitEvent(EventInfo evt, AbstractEventGenerator? generator)
        => generator?.GenerateEvent(this, evt);

    protected override void VisitMethod(MethodInfo method, AbstractMethodGenerator? generator)
        => generator?.GenerateMethod(this, method);

    protected override void VisitProperty(PropertyInfo property, AbstractPropertyGenerator? generator)
        => generator?.GenerateProperty(this, property);

    FieldBuilder IBuildingContext.EnsureMixin(Type type, bool isPrivate) => EnsureDelegatingMixin(type, isPrivate);
}

public class AnalyzingBakingProcessor : BakingProcessorWithComponentGenerators
{
    public AnalyzingBakingProcessor(IBakeryComponentGenerators generators)
        : base(generators)
    {
    }

    public readonly HashSet<Type> Interfaces = new HashSet<Type>();
    public readonly HashSet<Type> Mixins = new HashSet<Type>();

    protected override void VisitEvent(EventInfo evt, AbstractEventGenerator? generator)
        => generator?.GetMixinType();

    protected override void VisitMethod(MethodInfo method, AbstractMethodGenerator? generator)
        => generator?.GetMixinType();

    protected override void VisitProperty(PropertyInfo property, AbstractPropertyGenerator? generator)
        => generator?.GetMixinType();

    protected override void VisitInterface(Type type)
    {
        Interfaces.Add(type);
    }

    void AddMixin(Type? mixin)
    {
        if (mixin is not null)
        {
            Mixins.Add(mixin);

            Visit(mixin);
        }
    }
}
