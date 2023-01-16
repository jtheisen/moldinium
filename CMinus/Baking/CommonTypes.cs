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
    Exception,
    Mixin,
    NestedPropertyImplementation
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

public interface IWrappingImplementation { }

public interface IImplementation { }
