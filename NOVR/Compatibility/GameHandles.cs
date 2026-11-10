using System;
using System.Collections.Generic;
using System.Reflection;

namespace NOVR.Compatibility;

// Patches cache reflection handles to private game members in static readonly
// fields. A game update that renames one leaves the handle null (or makes a
// field-reference delegate throw in the type initializer), and the patch then
// throws inside the game's own per-frame code. Check the handles before a
// patch is installed instead, so a renamed member disables one VR adjustment
// rather than the method it was attached to.
internal static class GameHandles
{
    private const string NullableAttribute = "System.Runtime.CompilerServices.NullableAttribute";
    private const string NullableContextAttribute = "System.Runtime.CompilerServices.NullableContextAttribute";

    // Returns the first required handle that did not resolve on this type or
    // the types it is nested in (nested patch classes share the outer class's
    // handles), or null when every one resolved. Handles declared nullable are
    // optional: their users already cope with a missing member.
    internal static string? FirstUnresolved(Type type)
    {
        for (var owner = type; owner != null; owner = owner.DeclaringType)
        {
            foreach (var field in owner.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!field.IsInitOnly || !IsHandle(field.FieldType) || IsOptional(field))
                    continue;
                object? value;
                try
                {
                    value = field.GetValue(null);
                }
                catch (Exception error)
                {
                    // A failing type initializer: report its cause, not the wrapper.
                    while (error.InnerException != null)
                        error = error.InnerException;
                    return owner.Name + " (" + error.Message + ")";
                }
                if (value == null)
                    return owner.Name + "." + field.Name;
            }
        }
        return null;
    }

    private static bool IsHandle(Type type) =>
        typeof(MemberInfo).IsAssignableFrom(type) ||
        // HarmonyLib.AccessTools.FieldRef<,> and friends; matched by name so
        // this file stays free of a Harmony reference.
        typeof(Delegate).IsAssignableFrom(type) && type.Name.StartsWith("FieldRef", StringComparison.Ordinal);

    private static bool IsOptional(FieldInfo field)
    {
        var flag = Flag(field.CustomAttributes, NullableAttribute);
        for (var owner = field.DeclaringType; flag == null && owner != null; owner = owner.DeclaringType)
            flag = Flag(owner.CustomAttributes, NullableContextAttribute);
        return flag == 2;
    }

    // The compiler records nullability as 1 (not null) or 2 (nullable); for a
    // generic type the first element describes the field's own type.
    private static byte? Flag(IEnumerable<CustomAttributeData> attributes, string name)
    {
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeType.FullName != name || attribute.ConstructorArguments.Count != 1)
                continue;
            var argument = attribute.ConstructorArguments[0].Value;
            if (argument is byte single)
                return single;
            if (argument is IEnumerable<CustomAttributeTypedArgument> parts)
                foreach (var part in parts)
                    return part.Value as byte?;
        }
        return null;
    }
}
