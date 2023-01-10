using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.PortableExecutable;

namespace CMinus;

public delegate FieldBuilder MixinEnsurer(BakingState state, Type type);

public record BakingState(TypeBuilder TypeBuilder, MixinEnsurer EnsureMixin)
{
    public readonly Dictionary<Type, FieldBuilder> Mixins = new Dictionary<Type, FieldBuilder>();
}

public class Bakery
{
    readonly string name;
    readonly BakeryConfiguration configuration;
    readonly IBakeryComponentGenerators generators;
    readonly TypeAttributes typeAttributes;
    readonly ModuleBuilder moduleBuilder;

    BakingState? state;

    public String Name => name;

    public Bakery(String name, BakeryConfiguration? configuration = null)
    {
        this.name = name;
        this.configuration = configuration ?? BakeryConfiguration.PocGenerationConfiguration;
        this.generators = this.configuration.Generators;

        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
        moduleBuilder = assemblyBuilder.DefineDynamicModule(name);
        typeAttributes = TypeAttributes.Public;

        if (this.configuration.MakeAbstract) typeAttributes |= TypeAttributes.Abstract;
    }

    public T Create<T>()
    {
        var type = Resolve(typeof(T));

        if (Activator.CreateInstance(type) is T t)
        {
            return t;
        }
        else
        {
            throw new Exception("Unexpectedly got null or the wrong type from activator");
        }
    }

    public Type Resolve(Type interfaceType)
    {
        var name = "C" + interfaceType.Name;
        return moduleBuilder.GetType(name) ?? Create(name, interfaceType);
    }

    Type Create(String name, Type interfaceType)
    {
        if (state is not null) throw new Exception("Already building a type");

        var typeBuilder = moduleBuilder.DefineType(name, typeAttributes);

        state = new BakingState(typeBuilder, EnsureMixin);

        try
        {
            ImplementInterface(state, interfaceType, generators);

            return typeBuilder.CreateType() ?? throw new Exception("no type?");
        }
        finally
        {
            state = null;
        }
    }

    void ImplementInterface(BakingState state, Type type, IBakeryComponentGenerators generators)
    {
        var typeBuilder = state.TypeBuilder;

        typeBuilder.AddInterfaceImplementation(type);

        foreach (var property in type.GetProperties())
        {
            generators.GetPropertyGenerator(property).GenerateProperty(state, property);
        }

        foreach (var evt in type.GetEvents())
        {
            generators.GetEventGenerator(evt).GenerateEvent(state, evt);
        }

        foreach (var method in type.GetMethods())
        {
            if (!method.IsAbstract || method.IsSpecialName) continue;

            typeBuilder.DefineMethod(method.Name, method.Attributes | MethodAttributes.Public, method.ReturnType, method.GetParameters().Select(p => p.ParameterType).ToArray());
        }
    }

    FieldBuilder EnsureMixin(BakingState state, Type type)
    {
        var fieldBuilder = state.Mixins.GetValueOrDefault(type);

        if (fieldBuilder is null)
        {
            CreateMixin(state, type, out fieldBuilder);
        }

        return fieldBuilder;
    }

    void CreateMixin(BakingState state, Type type, out FieldBuilder fieldBuilder)
    {
        var typeBuilder = state.TypeBuilder;

        fieldBuilder = typeBuilder.DefineField($"mixin_{(type.FullName ?? "x").Replace('.', '_')}_{Guid.NewGuid()}", type, FieldAttributes.Private);

        state.Mixins[type] = fieldBuilder;

        var propertyImplementationType = typeof(IPropertyImplementation);

        var interfaces = (
            from i in type.GetInterfaces()
            where !propertyImplementationType.IsAssignableFrom(i)
            select i
        ).ToArray();

        if (interfaces.Length == 0) return;

        var nestedGenerators = new ComponentGenerators(
            new DelegatingPropertyGenerator(fieldBuilder),
            new DelegatingEventGenerator(fieldBuilder)
        );

        foreach (var ifc in interfaces)
        {
            ImplementInterface(state, ifc, nestedGenerators);
        }
    }

}
