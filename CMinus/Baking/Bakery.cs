using CMinus.Injection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace CMinus;

public abstract class AbstractBakery
{
    Dictionary<Type, Type> bakedTypes = new Dictionary<Type, Type>();

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

    public Type Resolve(Type interfaceOrBaseType)
    {
        if (!bakedTypes.TryGetValue(interfaceOrBaseType, out var bakedType))
        {
            bakedTypes[interfaceOrBaseType] = bakedType = Create(interfaceOrBaseType);
        }

        return bakedType;
    }

    public abstract Type Create(Type interfaceOrBaseType);
}

public class AbstractlyBakery : AbstractBakery
{
    protected readonly string name;
    protected readonly bool makeAbstract;
    protected readonly ModuleBuilder moduleBuilder;
    protected readonly TypeAttributes typeAttributes;

    public AbstractlyBakery(String name, TypeAttributes typeAttributes = TypeAttributes.Public | TypeAttributes.Abstract)
    {
        this.name = name;
        this.typeAttributes = typeAttributes;
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
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

    public override Type Create(Type interfaceOrBaseType)
    {
        var name = GetTypeName(interfaceOrBaseType);
        return moduleBuilder.GetType(name) ?? Create(name, interfaceOrBaseType);
    }

    protected virtual Type Create(String name, Type interfaceOrBaseType)
    {
        var typeBuilder = moduleBuilder.DefineType(name, typeAttributes);

        RedeclareInterface(typeBuilder, interfaceOrBaseType);

        return typeBuilder.CreateType() ?? throw new Exception("TypeBuilder gave no built type");
    }

    void RedeclareInterface(TypeBuilder typeBuilder, Type type)
    {
        typeBuilder.AddInterfaceImplementation(type);

        foreach (var property in type.GetProperties())
        {
            var propertyBuilder = typeBuilder.DefineProperty(property.Name, property.Attributes, property.PropertyType, null);

            var info = TypeProperties.Get(property.DeclaringType ?? throw new Exception("Unexpectedly not having a declaring type"));

            var requiresDefault = info.Properties.Single(p => p.info == property).requiresDefault;

            if (requiresDefault)
            {
                var attributeConstructor = typeof(RequiresDefaultAttribute).GetConstructor(new Type[] { })!;

                propertyBuilder.SetCustomAttribute(new CustomAttributeBuilder(attributeConstructor, new Object[] { }));
            }

            if (RedeclareMethodIfApplicable(typeBuilder, property.GetGetMethod(), out var getMethodBuilder))
                propertyBuilder.SetGetMethod(getMethodBuilder);
            if (RedeclareMethodIfApplicable(typeBuilder, property.GetSetMethod(), out var setMethodBuilder))
                propertyBuilder.SetSetMethod(setMethodBuilder);
        }

        foreach (var evt in type.GetEvents())
        {
            var eventBuilder = typeBuilder.DefineEvent(evt.Name, evt.Attributes, evt.EventHandlerType ?? throw new Exception($"No event handler type"));

            if (RedeclareMethodIfApplicable(typeBuilder, evt.GetAddMethod(), out var addMethodBuilder))
                eventBuilder.SetAddOnMethod(addMethodBuilder);
            if (RedeclareMethodIfApplicable(typeBuilder, evt.GetRemoveMethod(), out var removeMethodBuilder))
                eventBuilder.SetRemoveOnMethod(removeMethodBuilder);
        }

        foreach (var method in type.GetMethods())
        {
            if (method.IsSpecialName) continue;

            RedeclareMethodIfApplicable(typeBuilder, method, out _);
        }
    }

    // can be merged with the method declarer in the generation classes
    Boolean RedeclareMethodIfApplicable(TypeBuilder typeBuilder, MethodInfo? methodTemplate, [MaybeNullWhen(false)] out MethodBuilder methodBuilder)
    {
        methodBuilder = null;

        if (methodTemplate is null) return false;

        var attributes = methodTemplate.Attributes;

        //if (!methodTemplate.DeclaringType!.IsInterface) throw new Exception($"Expecting to derive an interface");

        //// Implemented interface methods don't show as abstract here
        //attributes |= MethodAttributes.Abstract;

        var parameters = methodTemplate.GetParameters();

        methodBuilder = typeBuilder.DefineMethod(
            methodTemplate.Name,
            attributes,
            methodTemplate.CallingConvention,
            methodTemplate.ReturnType,
            methodTemplate.ReturnParameter.GetRequiredCustomModifiers(),
            methodTemplate.ReturnParameter.GetOptionalCustomModifiers(),
            parameters.Select(p => p.ParameterType).ToArray(),
            parameters.Select(p => p.GetRequiredCustomModifiers()).ToArray(),
            parameters.Select(p => p.GetOptionalCustomModifiers()).ToArray()
        );

        return true;
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

    protected Type Analyze(Type interfaceOrBaseType)
    {
        var processor = new AnalyzingBakingProcessor(generators);

        processor.Visit(interfaceOrBaseType);

        throw new NotImplementedException();
    }

    protected override Type Create(String name, Type interfaceOrBaseType)
    {
        var baseType = interfaceOrBaseType.IsClass ? interfaceOrBaseType : null;

        var processor = new BuildingBakingProcessor(name, baseType, typeAttributes, defaultProvider, generators, moduleBuilder);

        return processor.Create(interfaceOrBaseType);
    }
}
