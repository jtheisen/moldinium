using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMinus.Construction
{
    public interface IPropertyImplementation
            <ValueInterface, ContainerInterface, Value, Container, MixIn, Accessor>
            where Value : ValueInterface
            where Container : ContainerInterface
            where MixIn : struct
            where Accessor : IAccessor<Value, Container>
    {
        Value Get(
            Container self,
            ref MixIn mixIn
            );

        void Set(
            Container self,
            ref MixIn mixIn,
            Value value
            );
    }

    public interface IAccessor<Value, Container>
    {
        String GetPropertyName();
        Int32 GetIndex();
        Boolean IsVariable();

        Value Get(Container container);
        void Set(Container container, Value value);
    }

    public struct EmptyMixIn { }

    public struct GenericPropertyImplementation<Value, Container> : IPropertyImplementation<Value, Container, Value, Container, EmptyMixIn, IAccessor<Value, Container>>
    {
        Value value;

        public Value Get(Container self, ref EmptyMixIn mixIn) => value;

        public void Set(Container self, ref EmptyMixIn mixIn, Value value) => this.value = value;
    }

    public interface ISimplePropertyImplementation<T>
    {
        T Value { get; set; }
    }

    public struct GenericSimplePropertyImplementation<T>
    {
        public T Value { get; set; }
    }
}
