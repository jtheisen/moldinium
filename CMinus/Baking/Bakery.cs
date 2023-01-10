using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace CMinus;

public record BakingState(TypeBuilder TypeBuilder)
{
    public readonly Dictionary<Type, FieldBuilder> MixIns = new Dictionary<Type, FieldBuilder>();
}

public class Bakery
{
    readonly string name;
    readonly BakeryConfiguration configuration;
    readonly IBakeryComponentGenerators generators;
    readonly TypeAttributes typeAttributes;
    readonly ModuleBuilder moduleBuilder;

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
        var typeBuilder = moduleBuilder.DefineType(name, typeAttributes);
        typeBuilder.AddInterfaceImplementation(interfaceType);

        var state = new BakingState(typeBuilder);

        foreach (var property in interfaceType.GetProperties())
        {
            generators.GetPropertyGenerator(property).GenerateProperty(state, property);
        }

        foreach (var method in interfaceType.GetMethods())
        {
            if (!method.IsAbstract || method.IsSpecialName) continue;

            typeBuilder.DefineMethod(method.Name, method.Attributes | MethodAttributes.Public, method.ReturnType, method.GetParameters().Select(p => p.ParameterType).ToArray());
        }

        return typeBuilder.CreateType() ?? throw new Exception("no type?");
    }
}
