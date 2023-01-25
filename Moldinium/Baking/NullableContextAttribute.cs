using Moldinium;
using System.Reflection;
using System.Reflection.Emit;

public enum NullableFlag
{
    Oblivious = 0,
    NotNullable = 1,
    Nullable = 2,
    Complex = 3
}

public static class NullabilityAttributesHelper
{
    public static CustomAttributeBuilder GetNullableContextAttributeBuilder(NullableFlag flag)
        => customAttributeBuilders[(int)flag];

    static CustomAttributeBuilder[] customAttributeBuilders;

    static NullabilityAttributesHelper()
    {
        var customAttributes = typeof(NullabilityAttributesHelper).GetCustomAttributesData();

        var nullableContextAttribute = customAttributes
            .Where(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute")
            .Single($"Expected {nameof(NullabilityAttributesHelper)} to have a NullableContextAttribute");

        customAttributeBuilders = new CustomAttributeBuilder[3];

        for (var i = 0; i < 3; ++i)
        {
            customAttributeBuilders[i] = new CustomAttributeBuilder(nullableContextAttribute.Constructor, new Object[] { (Byte)i });
        }
    }

    public static NullableFlag? GetFlag(Type type)
        => type.GetCustomAttributesData().GetFlag();

    public static NullableFlag? GetFlag(PropertyInfo property)
        => property.GetCustomAttributesData().GetFlag();

    static NullableFlag? GetFlag(this IList<CustomAttributeData> attributes)
    {
        CustomAttributeData? nullableAttribute = null, nullableContextAttribute = null;

        foreach (var attribute in attributes)
        {
            if (attribute.AttributeType.FullName == NullableAttributeName)
            {
                nullableAttribute = attribute;
            }
            else if(attribute.AttributeType.FullName == NullableContextAttributeName)
            {
                nullableContextAttribute = attribute;
            }
        }

        return nullableAttribute?.GetFlag() ?? nullableContextAttribute?.GetFlag();
    }

    static NullableFlag GetFlag(this CustomAttributeData attribute)
    {
        var argument = attribute.ConstructorArguments.Single();

        var type = argument.ArgumentType;

        if (type.IsValueType)
        {
            return (NullableFlag)(Byte)argument.Value!;
        }
        else if (type.IsArray)
        {
            return NullableFlag.Complex;
        }
        else
        {
            throw new InternalErrorException($"Unexpected {attribute.AttributeType} argument type {type}");
        }
    }

    public static void SetNullableAttributes(Action<CustomAttributeBuilder> target, IList<CustomAttributeData> attributesFromTemplate, NullableFlag flagFromInterface)
    {
        var flagFromTemplate = attributesFromTemplate.GetFlag();

        if (flagFromTemplate == NullableFlag.Complex)
        {
            foreach (var attribute in attributesFromTemplate)
            {
                var attributeType = attribute.AttributeType;

                var fullName = attributeType.FullName;

                if (fullName != NullableAttributeName && fullName != NullableContextAttributeName) continue;

                var arg = attribute.ConstructorArguments
                    .Select(a => a.Value)
                    .Single($"Expected {attribute.Constructor} to only take a single argument");

                if (arg is IReadOnlyCollection<CustomAttributeTypedArgument> moreArgs)
                {
                    arg = moreArgs.Select(a => (Byte)a.Value!).ToArray();
                }

                var builder = new CustomAttributeBuilder(attribute.Constructor, new Object[] { arg } );

                target(builder);
            }
        }
        else
        {
            target(GetNullableContextAttributeBuilder(flagFromTemplate ?? flagFromInterface));
        }
    }

    static String NullableAttributeName = "System.Runtime.CompilerServices.NullableAttribute";
    static String NullableContextAttributeName = "System.Runtime.CompilerServices.NullableContextAttribute";

    public static String[] NullableAttributeNames = new [] { NullableAttributeName, NullableContextAttributeName }; 
}

public class NullableAttributeReport
{
    StringWriter writer = new StringWriter();

    public void VisitType(Type type)
    {
        writer.WriteLine($"{type.Name} {GetNullabilityData(type.GetCustomAttributesData())}");

        foreach (var property in type.GetProperties())
        {
            VisitProperty(property);
        }
    }

    public void VisitProperty(PropertyInfo property)
    {
        writer.WriteLine($"  {property.Name} {GetNullabilityData(property.GetCustomAttributesData())}");

        VisitMethod(property.GetGetMethod());
        VisitMethod(property.GetSetMethod());
    }

    public void VisitMethod(MethodInfo? method)
    {
        if (method is null) return;

        writer.WriteLine($"    {method.Name} {GetNullabilityData(method.GetCustomAttributesData())}");
    }

    String GetNullabilityData(IList<CustomAttributeData> data)
    {
        return String.Join(", ", data
            .Where(a => NullabilityAttributesHelper.NullableAttributeNames.Contains(a.AttributeType.FullName))
            .Select(a => $"{a.AttributeType.Name} {a.ConstructorArguments[0]}")
        );            
    }

    public static String CreateReport(Type type)
    {
        var writer = new NullableAttributeReport();
        writer.VisitType(type);
        return writer.writer.ToString();
    }
}
