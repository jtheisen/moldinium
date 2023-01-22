using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace CMinus;

public abstract class AbstractBakery
{
    Dictionary<Type, Type> bakedTypes = new Dictionary<Type, Type>();

    public T Create<T>()
    {
        var type = Resolve(typeof(T));

        var instance = Activator.CreateInstance(type);

        if (instance is T t)
        {
            return t;
        }
        else
        {
            throw new Exception("Unexpectedly got null or the wrong type from activator");
        }
    }

    public Type Resolve(Type interfaceOrBaseType)
    {
        if (!bakedTypes.TryGetValue(interfaceOrBaseType, out var bakedType))
        {
            bakedTypes[interfaceOrBaseType] = bakedType = GetCreatedType(interfaceOrBaseType);
        }

        return bakedType;
    }

    public abstract Type GetCreatedType(Type interfaceOrBaseType);
}

public abstract class AbstractlyBakery : AbstractBakery
{
    protected readonly string name;
    protected readonly bool makeAbstract;
    protected readonly AssemblyBuilder assemblyBuilder;
    protected readonly ModuleBuilder moduleBuilder;
    protected readonly TypeAttributes typeAttributes;

    HashSet<Assembly> accessedAssemblies = new HashSet<Assembly>();

    public AbstractlyBakery(String name, TypeAttributes typeAttributes = TypeAttributes.Public | TypeAttributes.Abstract)
    {
        this.name = name;
        this.typeAttributes = typeAttributes;
        assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
        moduleBuilder = assemblyBuilder.DefineDynamicModule(name);
    }

    protected virtual String GetTypeName(Type interfaceOrBaseType)
        => $"{GetTypePrefix()}{interfaceOrBaseType.Name}";

    String GetTypePrefix()
    {
        if (typeAttributes.HasFlag(TypeAttributes.Abstract))
        {
            return "A";
        }
        else if (typeAttributes.HasFlag(TypeAttributes.SequentialLayout))
        {
            return "S";
        }
        else
        {
            return "C";
        }
    }

    public override Type GetCreatedType(Type interfaceOrBaseType)
    {
        var name = GetTypeName(interfaceOrBaseType);

        return moduleBuilder.GetType(name) ?? CreateImpl(name, interfaceOrBaseType);
    }

    protected abstract Type CreateImpl(String name, Type interfaceOrBaseType);

    protected void EnsureAccessToAssembly(Assembly assembly)
    {
        if (accessedAssemblies.Add(assembly))
        {
            var ignoresAccessChecksTo = new CustomAttributeBuilder
            (
                typeof(IgnoresAccessChecksToAttribute).GetConstructor(new Type[] { typeof(String) })
                ?? throw new InternalErrorException("Can't get constructor for IgnoresAccessChecksToAttribute"),
                new object[] { assembly.GetName().Name ?? throw new InternalErrorException("Can't get name for assembly") }
            );

            assemblyBuilder.SetCustomAttribute(ignoresAccessChecksTo);
        }
    }
}

public class Bakery : AbstractlyBakery
{
    readonly BakeryConfiguration configuration;
    readonly IBakeryComponentGenerators generators;
    readonly IDefaultProvider defaultProvider;

    public String Name => name;

    public Bakery(String name)
        : this(name, BakeryConfiguration.PocGenerationConfiguration) { }

    public Bakery(String name, BakeryConfiguration configuration)
        : base(name, TypeAttributes.Public)
    {
        this.configuration = configuration;
        generators = this.configuration.Generators;
        defaultProvider = this.configuration.DefaultProvider;
    }

    (ImplementationMapping mapping, Type[] publicMixins) Analyze(Type interfaceOrBaseType)
    {
        var processor = new AnalyzingBakingProcessor(generators);

        processor.VisitFirst(interfaceOrBaseType);

        var interfaceTypes = processor.Interfaces;
        var mixinTypes = processor.PublicMixins;

        var mapping = new ImplementationMapping(interfaceTypes.Concat(mixinTypes).ToHashSet());



        return (mapping, mixinTypes.ToArray());
    }

    protected override Type CreateImpl(String name, Type interfaceOrBaseType)
    {
        var baseType = interfaceOrBaseType.IsClass ? interfaceOrBaseType : null;

        var (interfaceMapping, publicMixins) = Analyze(interfaceOrBaseType);

        var processor = new BuildingBakingProcessor(
            name, baseType, typeAttributes, interfaceMapping, defaultProvider, generators, EnsureAccessToAssembly, moduleBuilder);

        return processor.Create(interfaceMapping.Interfaces, publicMixins);
    }
}
