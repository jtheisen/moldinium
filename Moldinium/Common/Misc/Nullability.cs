using System.Reflection.Emit;

namespace Moldinium.Common.Misc;

// see https://github.com/dotnet/roslyn/blob/main/docs/features/nullable-metadata.md

public enum NullableFlag
{
    Oblivious = 0,
    NotNullable = 1,
    Nullable = 2,
    Complex = 3
}

public static class NullabilityHelper
{
    public static CustomAttributeBuilder GetNullableContextAttributeBuilder(NullableFlag flag)
        => customAttributeBuilders[(int)flag];

    static CustomAttributeBuilder[] customAttributeBuilders;

    static NullabilityHelper()
    {
        var customAttributes = typeof(NullabilityHelper).GetCustomAttributesData();

        var nullableContextAttribute = customAttributes
            .Where(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute")
            .Single($"Expected {nameof(NullabilityHelper)} to have a NullableContextAttribute");

        customAttributeBuilders = new CustomAttributeBuilder[3];

        for (var i = 0; i < 3; ++i)
        {
            customAttributeBuilders[i] = new CustomAttributeBuilder(nullableContextAttribute.Constructor, new object[] { (byte)i });
        }
    }

    public static NullableFlag? GetNullableContextFlag(this Type type)
        => GetOwnNullableContextFlag(type) ?? type.DeclaringType?.GetNullableContextFlag();

    public static NullableFlag? GetOwnNullableContextFlag(this Type type)
        => type.GetCustomAttributesData().GetNullableContextFlag();

    public static NullableFlag? GetOwnFlag(this PropertyInfo property, NullableFlag? contextFlag)
    {
        var type = property.PropertyType;

        if (type.IsValueType)
        {
            return Nullable.GetUnderlyingType(type) is not null ? NullableFlag.Nullable : NullableFlag.NotNullable;
        }
        else
        {
            var flag = property.CustomAttributes.GetOwnFlag() ?? contextFlag;

            return flag;
        }
    }

    public static Boolean? IsNullable(this NullableFlag flag) => flag switch
    {
        NullableFlag.Nullable => true,
        NullableFlag.NotNullable => false,
        _ => null
    };

    static NullableFlag? GetNullableContextFlag(this IEnumerable<CustomAttributeData> attributes)
    {
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeType.FullName == NullableContextAttributeName)
            {
                return attribute.GetFlag();
            }
        }

        return null;
    }

    static NullableFlag? GetOwnFlag(this IEnumerable<CustomAttributeData> attributes)
    {
        CustomAttributeData? nullableAttribute = null, nullableContextAttribute = null;

        foreach (var attribute in attributes)
        {
            if (attribute.AttributeType.FullName == NullableAttributeName)
            {
                nullableAttribute = attribute;
            }
            else if (attribute.AttributeType.FullName == NullableContextAttributeName)
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
            return (NullableFlag)(byte)argument.Value!;
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
        var flagFromTemplate = attributesFromTemplate.GetOwnFlag();

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
                    arg = moreArgs.Select(a => (byte)a.Value!).ToArray();
                }

                var builder = new CustomAttributeBuilder(attribute.Constructor, new object[] { arg! });

                target(builder);
            }
        }
        else
        {
            target(GetNullableContextAttributeBuilder(flagFromTemplate ?? flagFromInterface));
        }
    }

    static string NullableAttributeName = "System.Runtime.CompilerServices.NullableAttribute";
    static string NullableContextAttributeName = "System.Runtime.CompilerServices.NullableContextAttribute";

    public static string[] NullableAttributeNames = new[] { NullableAttributeName, NullableContextAttributeName };
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

    string GetNullabilityData(IList<CustomAttributeData> data)
    {
        return string.Join(", ", data
            .Where(a => NullabilityHelper.NullableAttributeNames.Contains(a.AttributeType.FullName))
            .Select(a => $"{a.AttributeType.Name} {a.ConstructorArguments[0]}")
        );
    }

    public static string CreateReport(Type type)
    {
        var writer = new NullableAttributeReport();
        writer.VisitType(type);
        return writer.writer.ToString();
    }
}
