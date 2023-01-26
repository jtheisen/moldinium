using System.Reflection.Emit;

namespace Moldinium.Common.Misc;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct)]
public class TypeClassCharacterAttribute : Attribute
{
    public readonly char c;

    public TypeClassCharacterAttribute(Char c)
	{
        this.c = c;
    }

    public static CustomAttributeBuilder MakeBuilder(Char c)
    {
        var constructor = typeof(TypeClassCharacterAttribute).GetConstructors().Single();

        return new CustomAttributeBuilder(constructor, new Object[] { c });
    }

    public static Char GetTypeClassCharacter(Type type)
    {
        var attribute = type.GetCustomAttribute<TypeClassCharacterAttribute>();

        if (attribute is not null)
        {
            return attribute.c;
        }
        else if (type.IsClass)
        {
            return 'c';
        }
        else if (type.IsInterface)
        {
            return 'i';
        }
        else if (type.IsValueType)
        {
            return 's';
        }
        else
        {
            return '?';
        }
    }
}
