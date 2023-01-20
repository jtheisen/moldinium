using CMinus.Baking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;

namespace CMinus;

public struct MethodImplementationInfo
{
    public Boolean Exists { get; }
    public MethodInfo? ImplementationMethod { get; }
    public FieldBuilder? MixinFieldBuilder { get; }

    public Boolean IsImplememted => ImplementationMethod is not null;
    public Boolean IsMissingOrImplemented => !Exists || ImplementationMethod is not null;

    public MethodImplementationInfo(ImplementationMapping mapping, Dictionary<Type, FieldBuilder> mixins, MethodInfo? method)
    {
        if (method is not null)
        {
            var implementationMethod = mapping.GetImplementationMethod(method);

            Exists = true;
            ImplementationMethod = implementationMethod;

            if (implementationMethod?.DeclaringType is Type type && mixins.TryGetValue(type, out var mixinFieldBuilder))
            {
                MixinFieldBuilder = mixinFieldBuilder;
            }
            else
            {
                MixinFieldBuilder = null;
            }
        }
        else
        {
            Exists = false;
            ImplementationMethod = null;
            MixinFieldBuilder = null;
        }
    }
}

public interface IBuildingContext
{
    TypeBuilder TypeBuilder { get; }
    ILGenerator ConstructorGenerator { get; }
    IDefaultProvider DefaultProvider { get; }

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

    public void Visit(Type type)
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
    private readonly ImplementationMapping interfaceMapping;
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
        String name, Type? baseType, TypeAttributes typeAttributes, ImplementationMapping interfaceMapping,
        IDefaultProvider defaultProvider, IBakeryComponentGenerators generators, ModuleBuilder moduleBuilder
    )
        : base(generators)
    {
        this.interfaceMapping = interfaceMapping;
        this.defaultProvider = defaultProvider;
        this.generators = generators;

        typeBuilder = moduleBuilder.DefineType(name, typeAttributes, baseType);

        var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { });

        constructorGenerator = constructorBuilder.GetILGenerator();
    }

    public Type Create(Type[] interfaces, Type[] publicMixins)
    {
        foreach (var mixin in publicMixins)
        {
            CreateMixin(mixin, out _);
        }

        foreach (var ifc in interfaces)
        {
            Visit(ifc);
        }

        constructorGenerator.Emit(OpCodes.Ret);

        return typeBuilder.CreateType() ?? throw new Exception("Internal error: got no type from type builder");
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

    protected override void VisitInterface(Type type)
    {
        if (interfacesAndBases.Contains(type)) throw new Exception($"Already processed interface {type}");

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
        => new MethodImplementationInfo(interfaceMapping, mixins, method);
}

public class AnalyzingBakingProcessor : BakingProcessorWithComponentGenerators
{
    public AnalyzingBakingProcessor(IBakeryComponentGenerators generators)
        : base(generators)
    {
    }

    public readonly HashSet<Type> Interfaces = new HashSet<Type>();
    public readonly HashSet<Type> PublicMixins = new HashSet<Type>();

    protected override void VisitEvent(EventInfo evt, AbstractEventGenerator? generator)
        => AddMixin(generator?.GetMixinType());

    protected override void VisitMethod(MethodInfo method, AbstractMethodGenerator? generator)
        => AddMixin(generator?.GetMixinType());

    protected override void VisitProperty(PropertyInfo property, AbstractPropertyGenerator? generator)
        => AddMixin(generator?.GetMixinType());

    protected override void VisitInterface(Type type) => Interfaces.Add(type);

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
