using System;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;

namespace CMinus.Baking
{
    // some important experiment with Ldvirtfn that shows the safest way to call private implementations is to use delegates
    internal static class CheekyCaller
    {
        static void Main()
        {
            Func<String> a = (new CInnocent() as IBase).Foo;

            // this is required for the below to work - I guess the delegate internally assures
            // that the private implementation is loaded
            a();

            Cheeky.CreateCaller<Func<IBase, String>>(typeof(IDerived), "Foo")(new CWithImpls());
        }

        public static class Cheeky
        {
            public static D CreateCaller<D>(Type type, String methodName)
                where D : Delegate
            {
                return CreateCaller<D>(type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == methodName || m.Name.EndsWith($".{methodName}")).Single());
            }

            public static D CreateCaller<D>(MethodInfo implementation)
                where D : Delegate
            {
                var cheekyCallerMethod = new DynamicMethod(
                    "ProxyDelegate",
                    typeof(String),
                    new Type[] { typeof(IBase) }
                );

                var il = cheekyCallerMethod.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldvirtftn, implementation);
                il.EmitCalli(OpCodes.Calli, CallingConventions.Any, implementation.ReturnType, implementation.GetParameters().Select(p => p.ParameterType).ToArray(), null);
                il.Emit(OpCodes.Ret);

                return (D)cheekyCallerMethod.CreateDelegate(typeof(D));
            }
        }

        public interface IBase
        {
            String Foo() => "I";
        }

        public interface IDerived : IBase
        {
            String IBase.Foo() => "D";
        }

        class CInnocent : IDerived { }

        class CWithImpls : IDerived
        {
            String IBase.Foo() => "C2";
        }
    }
}
