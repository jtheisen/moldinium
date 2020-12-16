using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CMinus
{
    public interface Requires<T>
    {
        T GetService();
        T GetService<S>() where S : T;
    }

    public interface Requires<D, R> : Requires<D>
        where R : Resolver
    {

    }

    public interface Implementation<T>
    {
    }

    // provider don't resolve types
    public interface Provider<T>
    {
        T Get();
    }

    public record DependencyRecord(Type interfaceType, Type implementationType, Func<Object> constructor);

    public interface Resolver
    {
        IEnumerable<DependencyRecord> GetRecords();
    }

    //public interface AssemblyTypeResolver<TypeInAssembly> : Resolver
    //{
    //    IEnumerable<DependencyRecord> GetRecords()
    //    {
    //        return typeof(TypeInAssembly).Assembly
    //    }
    //}

    public interface MappingResolver<Interface, Implementation> : Resolver, Requires<Implementation>
    {
        new IEnumerable<DependencyRecord> GetRecords()
        {
            yield return new DependencyRecord(typeof(Interface), typeof(Implementation), () => GetService());
        }
    }

    public interface DefaultConstructingResolver<T> : Resolver
        where T : class, new()
    {
        new IEnumerable<DependencyRecord> GetRecords()
        {
            yield return new DependencyRecord(typeof(T), typeof(T), () => new T());
        }
    }

    public interface Variable<T>
    {
        public T Value { get; set; }
    }
}
