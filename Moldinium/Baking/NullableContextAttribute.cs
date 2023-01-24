using Moldinium;
using System.Reflection;
using System.Reflection.Emit;

[AttributeUsage(AttributeTargets.All)]
class NullableContextAttribute : Attribute
{
    private readonly byte flags;

    public NullableContextAttribute(Byte flags)
    {
        this.flags = flags;
    }

    public static ConstructorInfo ConstructorInfo { get; }

    public static CustomAttributeBuilder GetAttributeBuilder(Byte flags)
        => new CustomAttributeBuilder(ConstructorInfo, new Object[] { flags } );

    static NullableContextAttribute()
    {
        var ctor = typeof(NullableContextAttribute).GetConstructors().Single();

        ConstructorInfo = ctor;
    }
}

public static class TypeDefinedInNullableContext
{
    public static CustomAttributeBuilder GetAttributeBuilder() => customAttributeBuilder;

    static CustomAttributeBuilder customAttributeBuilder;

    static TypeDefinedInNullableContext()
    {
        var customAttributes = typeof(TypeDefinedInNullableContext).GetCustomAttributesData();

        var nullableContextAttribute = customAttributes
            .Where(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute")
            .Single($"Expected {nameof(TypeDefinedInNullableContext)} to have a NullableContextAttribute");

        var args = nullableContextAttribute.ConstructorArguments.Select(a => a.Value).ToArray();

        customAttributeBuilder = new CustomAttributeBuilder(nullableContextAttribute.Constructor, args);
    }
}

public static class CustomAttributeCopying
{
    public static void CopyCustomAttributes(Action<CustomAttributeBuilder> target, MemberInfo template)
    {
        foreach (var attribute in template.CustomAttributes)
        {
            var attributeType = attribute.AttributeType;

            if (!NullableAttributeNames.Contains(attributeType.FullName)) continue;

            var args = attribute.ConstructorArguments.Select(a => a.Value).ToArray();

            var builder = new CustomAttributeBuilder(attribute.Constructor, args);

            target(builder);
        }
    }

    public static String[] NullableAttributeNames = new[] { "System.Runtime.CompilerServices.NullableContextAttribute", "System.Runtime.CompilerServices.NullableAttribute" };
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
            .Where(a => CustomAttributeCopying.NullableAttributeNames.Contains(a.AttributeType.FullName))
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
