using CMinus.Construction;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace CMinus
{
    public class PropertyGenerator
    {
        public Type GetBackingType(PropertyInfo property)
        {
            return GetPropertyImplementationType(property.PropertyType);
        }

        public void GenerateGetterCode(ILGenerator generator, FieldBuilder fieldBuilder, MethodInfo backingGetMethod)
        {
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldflda, fieldBuilder);
            generator.Emit(OpCodes.Call, backingGetMethod);
            generator.Emit(OpCodes.Ret);
        }

        public void GenerateSetterCode(ILGenerator generator, FieldBuilder fieldBuilder, MethodInfo backingSetMethod)
        {
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldflda, fieldBuilder);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Call, backingSetMethod);
            generator.Emit(OpCodes.Ret);
        }

        Type GetPropertyImplementationType(Type propertyType)
        {
            return typeof(Construction.GenericPropertyImplementation<>).MakeGenericType(propertyType);
        }
    }

    public class Bakery
    {
        readonly string name;
        readonly IBakeryConfiguration configuration;
        readonly TypeAttributes typeAttributes;
        readonly ModuleBuilder moduleBuilder;

        public Bakery(String name, IBakeryConfiguration configuration)
        {
            this.name = name;
            this.configuration = configuration;

            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
            moduleBuilder = assemblyBuilder.DefineDynamicModule(name);
            typeAttributes = TypeAttributes.Public;
            if (configuration.MakeAbstract) typeAttributes |= TypeAttributes.Abstract;
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

            foreach (var property in interfaceType.GetProperties())
            {
                var propertyBuilder = typeBuilder.DefineProperty(property.Name, property.Attributes, property.PropertyType, null);

                var setMethod = property.GetSetMethod();
                var getMethod = property.GetGetMethod();

                if (setMethod != null)
                {
                    if (getMethod == null) throw new Exception("A writable property must also be readable");

                    var propertyGenerator = configuration.GetGenerator(property);

                    var backingPropertyImplementationType = propertyGenerator.GetBackingType(property);
                    var fieldBuilder = typeBuilder.DefineField($"backing_{property.Name}", backingPropertyImplementationType, FieldAttributes.Private);
                    var backingProperty = backingPropertyImplementationType.GetProperty("Value");
                    var backingGetMethod = backingProperty?.GetGetMethod();
                    var backingSetMethod = backingProperty?.GetSetMethod();

                    if (backingGetMethod == null) throw new Exception("Type must have a readable 'Value' property");
                    if (backingSetMethod == null) throw new Exception("Type must have a writable 'Value' property");

                    {
                        var getMethodBuilder = Create(typeBuilder, getMethod, isAbstract: false);
                        var generator = getMethodBuilder.GetILGenerator();
                        propertyGenerator.GenerateGetterCode(generator, fieldBuilder, backingGetMethod);
                        propertyBuilder.SetGetMethod(getMethodBuilder);
                    }
                    {
                        var setMethodBuilder = Create(typeBuilder, setMethod, isAbstract: false);
                        var generator = setMethodBuilder.GetILGenerator();
                        propertyGenerator.GenerateSetterCode(generator, fieldBuilder, backingSetMethod);
                        propertyBuilder.SetSetMethod(setMethodBuilder);
                    }
                }
                else if (getMethod != null)
                {
                    propertyBuilder.SetGetMethod(Create(typeBuilder, getMethod));
                }
                else
                {
                    throw new Exception("A property that is neither readable nor writable was encountered");
                }
            }

            foreach (var method in interfaceType.GetMethods())
            {
                if (!method.IsAbstract || method.IsSpecialName) continue;

                typeBuilder.DefineMethod(method.Name, method.Attributes | MethodAttributes.Public, method.ReturnType, method.GetParameters().Select(p => p.ParameterType).ToArray());
            }

            return typeBuilder.CreateType() ?? throw new Exception("no type?");
        }

        MethodBuilder Create(TypeBuilder typeBuilder, MethodInfo methodTemplate, Boolean isAbstract = true)
        {
            var attributes = methodTemplate.Attributes | MethodAttributes.Public;

            if (!isAbstract) attributes &= ~MethodAttributes.Abstract;

            var parameters = methodTemplate.GetParameters();

            var methodBuilder = typeBuilder.DefineMethod(
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

            return methodBuilder;
        }

        Type GetPropertyImplementationType(Type propertyType)
        {
            return typeof(Construction.GenericPropertyImplementation<>).MakeGenericType(propertyType);
        }
    }


}
