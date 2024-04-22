using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;

namespace Rivers;

public static class HarmonyReflectionExtensions
{
    #region Fields

    /// <summary>
    ///     Gets a field within the calling instanced object. This can be an internal or private field within another assembly.
    /// </summary>
    /// <typeparam name="T">The type of field to return.</typeparam>
    /// <param name="instance">The instance in which the field resides.</param>
    /// <param name="fieldName">The name of the field to return.</param>
    /// <returns>An object containing the value of the field, reflected by this instance.</returns>
    public static T GetField<T>(this object instance, string fieldName)
    {
        return (T)AccessTools.Field(instance.GetType(), fieldName).GetValue(instance);
    }

    public static T GetStaticField<T>(this Type type, string fieldName)
    {
        return (T)AccessTools.Field(type, fieldName).GetValue(null);
    }

    /// <summary>
    ///     Gets an array of fields within the calling instanced object, of a specified Type. These can be an internal or private fields within another assembly.
    /// </summary>
    /// <typeparam name="T">The type of field to return.</typeparam>
    /// <param name="instance">The instance in which the field resides.</param>
    /// <returns>An array containing the values of the fields of a specified Type, reflected by this instance.</returns>
    public static T[] GetFields<T>(this object instance)
    {
        System.Collections.Generic.IEnumerable<FieldInfo> declaredFields =
            AccessTools.GetDeclaredFields(instance.GetType())?.Where(t => t.FieldType == typeof(T));
        return declaredFields?.Select(val => instance.GetField<T>(val.Name)).ToArray();
    }

    /// <summary>
    ///     Sets a field within the calling instanced object. This can be an internal or private field within another assembly.
    /// </summary>
    /// <param name="instance">The instance in which the field resides.</param>
    /// <param name="fieldName">The name of the field to set.</param>
    /// <param name="setVal">The value to set the field to.</param>
    public static void SetField(this object instance, string fieldName, object setVal)
    {
        AccessTools.Field(instance.GetType(), fieldName).SetValue(instance, setVal);
    }

    public static void SetStaticField(this Type type, string fieldName, object setVal)
    {
        AccessTools.Field(type, fieldName).SetValue(null, setVal);
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets a property within the calling instanced object. This can be an internal or private property within another assembly.
    /// </summary>
    /// <typeparam name="T">The type of property to return.</typeparam>
    /// <param name="instance">The instance in which the property resides.</param>
    /// <param name="propertyName">The name of the property to return.</param>
    /// <returns>An object containing the value of the property, reflected by this instance.</returns>
    public static T GetProperty<T>(this object instance, string propertyName)
    {
        return (T)AccessTools.Property(instance.GetType(), propertyName).GetValue(instance);
    }

    /// <summary>
    ///     Sets a property within the calling instanced object. This can be an internal or private property within another assembly.
    /// </summary>
    /// <param name="instance">The instance in which the property resides.</param>
    /// <param name="propertyName">The name of the property to set.</param>
    /// <param name="setVal">The value to set the property to.</param>
    public static void SetProperty(this object instance, string propertyName, object setVal)
    {
        AccessTools.Property(instance.GetType(), propertyName).SetValue(instance, setVal);
    }

    /// <summary>
    ///     Gets a static property within the calling type. This can be an internal or private property within another assembly.
    /// </summary>
    /// <typeparam name="T">The type of property to return.</typeparam>
    /// <param name="type">The type in which the property resides.</param>
    /// <param name="propertyName">The name of the property to return.</param>
    /// <returns>An object containing the value of the property, reflected by this instance.</returns>
    public static T GetStaticProperty<T>(this Type type, string propertyName)
    {
        return (T)AccessTools.Property(type, propertyName).GetValue(null);
    }

    /// <summary>
    ///     Sets a static property within the calling type. This can be an internal or private property within another assembly.
    /// </summary>
    /// <param name="type">The type in which the property resides.</param>
    /// <param name="propertyName">The name of the property to set.</param>
    /// <param name="setVal">The value to set the property to.</param>
    public static void SetStaticProperty(this Type type, string propertyName, object setVal)
    {
        AccessTools.Property(type, propertyName).SetValue(null, setVal);
    }

    #endregion

    #region Methods

    /// <summary>
    ///     Calls a method within an instance of an object, via reflection. This can be an internal or private method within another assembly.
    /// </summary>
    /// <typeparam name="T">The return type, expected back from the method.</typeparam>
    /// <param name="instance">The instance to call the method from.</param>
    /// <param name="method">The name of the method to call.</param>
    /// <param name="args">The arguments to pass to the method.</param>
    /// <returns>The return value of the reflected method call.</returns>
    public static T CallMethod<T>(this object instance, string method, params object[] args)
    {
        return (T)AccessTools.Method(instance.GetType(), method).Invoke(instance, args);
    }

    public static T CallAmbigMethod<T>(this object instance, string method, Type[] parameters, params object[] args)
    {
        return (T)AccessTools.Method(instance.GetType(), method, parameters).Invoke(instance, args);
    }

    /// <summary>
    ///     Calls a method within an instance of an object, via reflection. This can be an internal or private method within another assembly.
    /// </summary>
    /// <param name="instance">The instance to call the method from.</param>
    /// <param name="method">The name of the method to call.</param>
    /// <param name="args">The arguments to pass to the method.</param>
    public static void CallMethod(this object instance, string method, params object[] args)
    {
        AccessTools.Method(instance.GetType(), method)?.Invoke(instance, args);
    }

    /// <summary>
    ///     Calls a method within an instance of an object, via reflection. This can be an internal or private method within another assembly.
    /// </summary>
    /// <param name="instance">The instance to call the method from.</param>
    /// <param name="method">The name of the method to call.</param>
    public static void CallMethod(this object instance, string method)
    {
        AccessTools.Method(instance.GetType(), method)?.Invoke(instance, null);
    }

    /// <summary>
    ///     Calls a static method within an object, via reflection. This can be an internal or private method within another assembly.
    /// </summary>
    /// <param name="instance">The instance to call the method from.</param>
    /// <param name="method">The name of the method to call.</param>
    public static void CallStaticMethod(this object instance, string method)
    {
        AccessTools.Method(instance.GetType(), method)?.Invoke(null, null);
    }

    /// <summary>
    ///     Calls a static method within a type, via reflection. This can be an internal or private method within another assembly.
    /// </summary>
    /// <param name="type">The type to call the method from.</param>
    /// <param name="method">The name of the method to call.</param>
    public static void CallStaticMethod(this Type type, string method)
    {
        AccessTools.Method(type, method)?.Invoke(null, null);
    }

    /// <summary>
    ///     Gets the <see cref="MethodInfo"/> for a method within an instance of a class, via reflection. This can be an internal or private method within another assembly.
    /// </summary>
    /// <param name="instance">The instance to get the method from.</param>
    /// <param name="method">The method to gather info about.</param>
    /// <param name="parameters">An itemised method signature, used to distinguish between overloads.</param>
    /// <param name="generics">An itemised array of generic parameters, used to distinguish between overloads.</param>
    /// <returns>Returns a <see cref="MethodInfo"/> for the specified method.</returns>
    public static MethodInfo GetMethod(this object instance, string method, Type[] parameters = null, Type[] generics = null)
    {
        return AccessTools.Method(instance.GetType(), method, parameters, generics);
    }

    #endregion

    #region Types

    /// <summary>
    ///     Creates the instance of a specified Type. Be aware that this will ignore all Service Providers, and attempt to directly instantiate a class.
    /// </summary>
    /// <param name="type">The type to create an instance of.</param>
    /// <returns>A new instance of the specified type.</returns>
    public static object CreateInstance(this Type type)
    {
        return AccessTools.CreateInstance(type);
    }

    /// <summary>
    ///     Gets the type of the class within an assembly, via reflection.
    /// </summary>
    /// <param name="assembly">The assembly the class resides in.</param>
    /// <param name="className">The name of the class.</param>
    /// <returns>The <see cref="Type"/> of the class, if found within the assembly, otherwise, returns <c>null</c>.</returns>
    public static Type GetClassType(this Assembly assembly, string className)
    {
        Type[] ts = AccessTools.GetTypesFromAssembly(assembly);
        return Array.Find(ts, t => t.Name == className);
    }

    #endregion

    #region Objects

    /// <summary>
    ///     Makes a deep copy of any object.
    /// </summary>
    /// <typeparam name="T">The type of the instance that should be created; for legacy reasons, this must be a class or interface.</typeparam>
    /// <param name="source">The original object.</param>
    /// <returns>A copy of the original object but of type <typeparamref name="T" /></returns>
    public static T DeepClone<T>(this T source) where T : class
    {
        return AccessTools.MakeDeepCopy<T>(source);
    }

    #endregion
}