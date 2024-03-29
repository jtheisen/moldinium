﻿using System.Reflection.Emit;
using System.Reflection;
using Moldinium.Common.Defaulting;
using Moldinium.Common.Misc;

namespace Moldinium.Baking;

public interface IBuildingContext
{
    TypeBuilder TypeBuilder { get; }
    ILGenerator ConstructorGenerator { get; }
    IDefaultProvider DefaultProvider { get; }

    NullableFlag GetNullableFlagForInterface(Type interfaceType);

    MethodImplementationInfo GetOuterImplementationInfo(MethodInfo? method);

    FieldBuilder EnsureMixin(Type type, Boolean isPrivate);
}

public abstract class BakingProcessorWithComponentGenerators
{
    IBakeryComponentGenerators generators;

    public BakingProcessorWithComponentGenerators(IBakeryComponentGenerators generators)
    {
        this.generators = generators;
    }

    public abstract void Visit(Type type);

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

    protected abstract void VisitProperty(PropertyInfo property, AbstractPropertyGenerator? generator);
    protected abstract void VisitEvent(EventInfo evt, AbstractEventGenerator? generator);
    protected abstract void VisitMethod(MethodInfo method, AbstractMethodGenerator? generator);
}

public delegate void AccessEnsurer(Assembly assembly);

public class BuildingBakingProcessor : BakingProcessorWithComponentGenerators, IBuildingContext
{
    private readonly ImplementationMapping implementationMapping;
    private readonly IDefaultProvider defaultProvider;
    private readonly IBakeryComponentGenerators generators;
    private readonly AccessEnsurer ensureAccess;
    private readonly TypeBuilder typeBuilder;
    private readonly ILGenerator constructorGenerator;

    private readonly Dictionary<Type, NullableFlag> nullableFlags = new Dictionary<Type, NullableFlag>();
    private readonly HashSet<Type> interfacesAndBases = new HashSet<Type>();
    private readonly Dictionary<Type, FieldBuilder> mixins = new Dictionary<Type, FieldBuilder>();

    private readonly NullableFlag defaultNullability = NullableFlag.NotNullable;

    private static readonly CustomAttributeBuilder typeClassCharacterAttributeBuilder = TypeClassCharacterAttribute.MakeBuilder('b');

    public IDefaultProvider DefaultProvider => defaultProvider;
    public TypeBuilder TypeBuilder => typeBuilder;
    public ILGenerator ConstructorGenerator => constructorGenerator;
    public NullableFlag GetNullableFlagForInterface(Type type) => nullableFlags[type];

    Type? createdType;

    public BuildingBakingProcessor(
        String name, Type? baseType, TypeAttributes typeAttributes, ImplementationMapping implementationMapping,
        IDefaultProvider defaultProvider, IBakeryComponentGenerators generators, AccessEnsurer ensureAccess, ModuleBuilder moduleBuilder
    )
        : base(generators)
    {
        this.implementationMapping = implementationMapping;
        this.defaultProvider = defaultProvider;
        this.generators = generators;
        this.ensureAccess = ensureAccess;
        typeBuilder = moduleBuilder.DefineType(name, typeAttributes, baseType);

        typeBuilder.SetCustomAttribute(typeClassCharacterAttributeBuilder);

        typeBuilder.SetCustomAttribute(NullabilityHelper.GetNullableContextAttributeBuilder(defaultNullability));

        var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { });

        constructorGenerator = constructorBuilder.GetILGenerator();
    }

    public Type Create(Type[] interfaces, Type[] publicMixins)
    {
        if (createdType is not null) throw new Exception("Internal error: Type already created");

        foreach (var mixin in publicMixins)
        {
            CreateMixin(mixin, out _);
        }

        foreach (var ifc in interfaces)
        {
            ensureAccess(ifc.Assembly);

            nullableFlags[ifc] = NullabilityHelper.GetNullableContextFlag(ifc) ?? NullableFlag.Oblivious;

            Visit(ifc);
        }

        constructorGenerator.Emit(OpCodes.Ret);

        return createdType = typeBuilder.CreateType() ?? throw new Exception("Internal error: got no type from type builder");
    }

    public override void Visit(Type type)
    {
        if (type.IsInterface)
        {
            VisitInterface(type);

            VisitMembers(type);
        }
    }

    FieldBuilder EnsureMixin(Type type)
    {
        var fieldBuilder = mixins.GetValueOrDefault(type);

        if (fieldBuilder is null)
        {
            CreateMixin(type, out fieldBuilder);
        }

        return fieldBuilder;
    }

    void CreateMixin(Type type, out FieldBuilder fieldBuilder)
    {
        fieldBuilder = typeBuilder.DefineField($"mixin_{(type.FullName ?? "x").Replace('.', '_')}_{Guid.NewGuid()}", type, FieldAttributes.Private);

        mixins[type] = fieldBuilder;
    }

    void VisitInterface(Type type)
    {
        if (interfacesAndBases.Contains(type)) throw new Exception($"Already processed interface {type}");

        typeBuilder.AddInterfaceImplementation(type);

        interfacesAndBases.Add(type);
    }

    protected override void VisitEvent(EventInfo evt, AbstractEventGenerator? generator)
        => generator?.GenerateEvent(this, evt);

    protected override void VisitMethod(MethodInfo method, AbstractMethodGenerator? generator)
        => generator?.GenerateMethod(this, method);

    protected override void VisitProperty(PropertyInfo property, AbstractPropertyGenerator? generator)
        => generator?.GenerateProperty(this, property);

    FieldBuilder IBuildingContext.EnsureMixin(Type type, bool isPrivate)
    {
        if (isPrivate)
        {
            return EnsureMixin(type);
        }
        else
        {
            return mixins[type];
        }
    }

    MethodImplementationInfo IBuildingContext.GetOuterImplementationInfo(MethodInfo? method)
        => new MethodImplementationInfo(implementationMapping, mixins, method);
}

public class AnalyzingBakingProcessor : BakingProcessorWithComponentGenerators
{
    public AnalyzingBakingProcessor(IBakeryComponentGenerators generators)
        : base(generators)
    {
    }

    public readonly HashSet<Type> Interfaces = new HashSet<Type>();
    public readonly HashSet<Type> PublicMixins = new HashSet<Type>();

    public override void Visit(Type type)
    {
        if (type.IsInterface)
        {
            VisitInterface(type);

            VisitMembers(type);
        }
        else
        {
            VisitMembers(type);

            foreach (var ifc in type.GetInterfaces())
            {
                Visit(ifc);
            }
        }
    }

    public void VisitFirst(Type type)
    {
        Visit(type);

        if (type.IsInterface)
        {
            foreach (var ifc in type.GetInterfaces())
            {
                Visit(ifc);
            }
        }
    }

    protected override void VisitEvent(EventInfo evt, AbstractEventGenerator? generator)
        => AddMixin(generator?.GetMixinType());

    protected override void VisitMethod(MethodInfo method, AbstractMethodGenerator? generator)
        => AddMixin(generator?.GetMixinType());

    protected override void VisitProperty(PropertyInfo property, AbstractPropertyGenerator? generator)
        => AddMixin(generator?.GetMixinType());

    void VisitInterface(Type type) => Interfaces.Add(type);

    void AddMixin(Type? mixin)
    {
        if (mixin is not null)
        {
            if (PublicMixins.Add(mixin))
            {
                Visit(mixin);
            }
        }
    }
}
