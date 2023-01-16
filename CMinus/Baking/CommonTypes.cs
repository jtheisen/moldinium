using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMinus;

public enum ImplementationTypeArgumentKind
{
    Value,
    Return,
    Handler,
    Exception,
    Container,
    Mixin
}

[AttributeUsage(AttributeTargets.GenericParameter)]
public class TypeKindAttribute : Attribute
{
    public TypeKindAttribute(ImplementationTypeArgumentKind type)
    {
        Kind = type;
    }

    public ImplementationTypeArgumentKind Kind { get; }
}

public interface IImplementation { }

public interface IEmptyImplementation : IImplementation { }

public interface IPropertyImplementation : IImplementation { }

public interface IPropertyWrapperImplementation : IImplementation { }

public interface IMethodWrapperImplementation : IImplementation { }

public interface IEventImplementation : IImplementation { }

public struct VoidDummy { }
