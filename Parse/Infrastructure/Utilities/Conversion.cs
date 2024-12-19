using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Parse.Infrastructure.Utilities;

#pragma warning disable CS1030 // #warning directive
#warning Possibly should be refactored.

/// <summary>
/// A set of utilities for converting generic types between each other.
/// </summary>
public static class Conversion
#pragma warning restore CS1030 // #warning directive
{
    /// <summary>
    /// Converts a value to the requested type -- coercing primitives to
    /// the desired type, wrapping lists and dictionaries appropriately,
    /// or else returning null.
    ///
    /// This should be used on any containers that might be coming from a
    /// user to normalize the collection types. Collection types coming from
    /// JSON deserialization can be safely assumed to be lists or dictionaries of
    /// objects.
    /// </summary>
    public static T As<T>(object value) where T : class
    {
        return ConvertTo<T>(value) as T;
    }

    /// <summary>
    /// Converts a value to the requested type -- coercing primitives to
    /// the desired type, wrapping lists and dictionaries appropriately,
    /// or else throwing an exception.
    ///
    /// This should be used on any containers that might be coming from a
    /// user to normalize the collection types. Collection types coming from
    /// JSON deserialization can be safely assumed to be lists or dictionaries of
    /// objects.
    /// </summary>
    public static T To<T>(object value)
    {
        return (T) ConvertTo<T>(value);
    }
    internal static object ConvertTo<T>(object value)
    {
        if (value is T || value == null)
            return value;

        if (typeof(T).IsPrimitive)
        {
            // Special case for JSON deserialized strings that represent numbers
            if (value is string stringValue)
            {
                if (typeof(T) == typeof(float) && float.TryParse(stringValue, out float floatValue))
                    return floatValue;

                if (typeof(T) == typeof(double) && double.TryParse(stringValue, out double doubleValue))
                    return doubleValue;

                if (typeof(T) == typeof(int) && int.TryParse(stringValue, out int intValue))
                    return intValue;

                if (typeof(T) == typeof(long) && long.TryParse(stringValue, out long longValue))
                    return longValue;

                if (typeof(T) == typeof(decimal) && decimal.TryParse(stringValue, out decimal decimalValue))
                    return decimalValue;

                if (typeof(T) == typeof(short) && short.TryParse(stringValue, out short shortValue))
                    return shortValue;

                if (typeof(T) == typeof(byte) && byte.TryParse(stringValue, out byte byteValue))
                    return byteValue;

                if (typeof(T) == typeof(sbyte) && SByte.TryParse(stringValue, out sbyte sbyteValue))
                    return sbyteValue;

                if (typeof(T) == typeof(bool) && Boolean.TryParse(stringValue, out bool boolValue))
                    return boolValue;

                if (typeof(T) == typeof(char) && stringValue.Length == 1)
                    return stringValue[0]; // Returns the first character if the string length is 1
            }
         
                return (T) Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
        }
        if (typeof(T).IsConstructedGenericType)
        {
            if (typeof(T).CheckWrappedWithNullable() && typeof(T).GenericTypeArguments[0] is { IsPrimitive: true } innerType)
                return (T) Convert.ChangeType(value, innerType, System.Globalization.CultureInfo.InvariantCulture);

            if (GetInterfaceType(value.GetType(), typeof(IList<>)) is { } listType && typeof(T).GetGenericTypeDefinition() == typeof(IList<>))
                return Activator.CreateInstance(typeof(FlexibleListWrapper<,>).MakeGenericType(typeof(T).GenericTypeArguments[0], listType.GenericTypeArguments[0]), value);

            if (GetInterfaceType(value.GetType(), typeof(IDictionary<,>)) is { } dictType && typeof(T).GetGenericTypeDefinition() == typeof(IDictionary<,>))
                return Activator.CreateInstance(typeof(FlexibleDictionaryWrapper<,>).MakeGenericType(typeof(T).GenericTypeArguments[1], dictType.GenericTypeArguments[1]), value);
        }

        return value;
    }

    /// <summary>
    /// Holds a dictionary that maps a cache of interface types for related concrete types.
    /// The lookup is slow the first time for each type because it has to enumerate all interface
    /// on the object type, but made fast by the cache.
    ///
    /// The map is:
    ///    (object type, generic interface type) => constructed generic type
    /// </summary>
    static Dictionary<Tuple<Type, Type>, Type> InterfaceLookupCache { get; } = new Dictionary<Tuple<Type, Type>, Type>();

    static Type GetInterfaceType(Type objType, Type genericInterfaceType)
    {
        Tuple<Type, Type> cacheKey = new Tuple<Type, Type>(objType, genericInterfaceType);

        if (InterfaceLookupCache.ContainsKey(cacheKey))
            return InterfaceLookupCache[cacheKey];

        foreach (Type type in objType.GetInterfaces())
            if (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == genericInterfaceType)
                return InterfaceLookupCache[cacheKey] = type;

        return default;
    }
}