using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace CMinus
{
    public class Bakery
    {
        TypeAttributes typeAttributes;
        ModuleBuilder moduleBuilder;

        public Bakery(String name, Boolean makeAbstract = true)
        {
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
            moduleBuilder = assemblyBuilder.DefineDynamicModule(name);
            typeAttributes = TypeAttributes.Public;
            if (makeAbstract) typeAttributes |= TypeAttributes.Abstract;
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

                    var backingPropertyImplementationType = GetPropertyImplementationType(property.PropertyType);
                    var fieldBuilder = typeBuilder.DefineField($"backing_{property.Name}", backingPropertyImplementationType, FieldAttributes.Private);
                    var backingProperty = backingPropertyImplementationType.GetProperty("Value");
                    var backingGetMethod = backingProperty?.GetGetMethod();
                    var backingSetMethod = backingProperty?.GetSetMethod();

                    if (backingGetMethod == null) throw new Exception("Type must have a readable 'Value' property");
                    if (backingSetMethod == null) throw new Exception("Type must have a writable 'Value' property");

                    {
                        var getMethodBuilder = Create(typeBuilder, getMethod, isAbstract: false);
                        var generator = getMethodBuilder.GetILGenerator();
                        generator.Emit(OpCodes.Ldarg_0);
                        generator.Emit(OpCodes.Ldflda, fieldBuilder);
                        generator.Emit(OpCodes.Call, backingGetMethod);
                        generator.Emit(OpCodes.Ret);
                    }
                    {
                        var setMethodBuilder = Create(typeBuilder, setMethod, isAbstract: false);
                        var generator = setMethodBuilder.GetILGenerator();
                        generator.Emit(OpCodes.Ldarg_0);
                        generator.Emit(OpCodes.Ldflda, fieldBuilder);
                        generator.Emit(OpCodes.Ldarg_1);
                        generator.Emit(OpCodes.Call, backingSetMethod);
                        generator.Emit(OpCodes.Ret);
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

            var methodBuilder = typeBuilder.DefineMethod(methodTemplate.Name, attributes, methodTemplate.ReturnType, methodTemplate.GetParameters().Select(p => p.ParameterType).ToArray());

            return methodBuilder;
        }

        Type GetPropertyImplementationType(Type propertyType)
        {
            return typeof(Construction.GenericPropertyImplementation<>).MakeGenericType(propertyType);
        }
    }


}
