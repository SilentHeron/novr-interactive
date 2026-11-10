using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
    // Field-reference accessors with Harmony's own lookup and type rules: the
    // named instance field on T or a base type, returned as its exact type
    // (value types) or a type assignable from it (reference types). Anything
    // else throws, as it does in the game.
    public static partial class AccessTools
    {
        public delegate ref TField FieldRef<in T, TField>(T instance);

        public static FieldRef<T, TField> FieldRefAccess<T, TField>(string name)
        {
            FieldInfo? field = null;
            for (var type = typeof(T); type != null && field == null; type = type.BaseType)
                field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null)
                throw new MissingFieldException(typeof(T).Name, name);
            var declared = field.FieldType;
            if (declared != typeof(TField) && (declared.IsValueType || !typeof(TField).IsAssignableFrom(declared)))
                throw new ArgumentException($"FieldRefAccess return type {typeof(TField)} does not fit {typeof(T).Name}.{name} ({declared}).");
            var method = new DynamicMethod("FieldRef_" + name, typeof(TField).MakeByRefType(), new[] { typeof(T) }, typeof(T), true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldflda, field);
            il.Emit(OpCodes.Ret);
            return (FieldRef<T, TField>)method.CreateDelegate(typeof(FieldRef<T, TField>));
        }
    }
}
