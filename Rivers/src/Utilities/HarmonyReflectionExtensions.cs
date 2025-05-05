using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace Rivers;

public static class HarmonyReflectionExtensions
{
    // Network.

    public static void AppendHandler<T>(this Vintagestory.Client.NoObf.NetworkChannel channel, NetworkServerMessageHandler<T> handler)
    {
        if (!channel.GetField<Dictionary<Type, int>>("messageTypes").TryGetValue(typeof(T), out int value))
        {
            throw new Exception("No such message type " + typeof(T)?.ToString() + " registered. Did you forget to call RegisterMessageType?");
        }

        channel.GetField<Action<Packet_CustomPacket>[]>("handlers")[value] += delegate (Packet_CustomPacket p)
        {
            T packet;
            using (MemoryStream source = new(p.Data))
            {
                packet = Serializer.Deserialize<T>(source);
            }
            handler(packet);
        };
    }

    public static void AppendHandler<T>(this Vintagestory.Server.NetworkChannel channel, NetworkClientMessageHandler<T> handler)
    {
        if (!channel.GetField<Dictionary<Type, int>>("messageTypes").TryGetValue(typeof(T), out int value))
        {
            throw new Exception("No such message type " + typeof(T)?.ToString() + " registered. Did you forget to call RegisterMessageType?");
        }

        channel.GetField<Action<Packet_CustomPacket, IServerPlayer>[]>("handlers")[value] += delegate (Packet_CustomPacket p, IServerPlayer player)
        {
            T packet;
            using (MemoryStream source = new(p.Data))
            {
                packet = Serializer.Deserialize<T>(source);
            }
            handler(player, packet);
        };
    }

    // Fields.

    /// <summary>
    ///     Gets a field within the calling instanced object. This can be an internal or private field within another 
    ///     .
    /// </summary>
    /// <typeparam name="T">The type of field to return.</typeparam>
    /// <param name="instance">The instance in which the field resides.</param>
    /// <param name="fieldName">The name of the field to return.</param>
    /// <returns>An object containing the value of the field, reflected by this instance.</returns>
    public static T GetField<T>(this object instance, string fieldName)
    {
        return (T)AccessTools.Field(instance.GetType(), fieldName).GetValue(instance)!;
    }

    public static T GetStaticField<T>(this Type type, string fieldName)
    {
        return (T)AccessTools.Field(type, fieldName).GetValue(null)!;
    }

    /// <summary>
    ///     Gets an array of fields within the calling instanced object, of a specified Type. These can be an internal or private fields within another assembly.
    /// </summary>
    /// <typeparam name="T">The type of field to return.</typeparam>
    /// <param name="instance">The instance in which the field resides.</param>
    /// <returns>An array containing the values of the fields of a specified Type, reflected by this instance.</returns>
    public static T[] GetFields<T>(this object instance)
    {
        IEnumerable<FieldInfo> declaredFields = AccessTools.GetDeclaredFields(instance.GetType())?.Where(t => t.FieldType == typeof(T))!;
        return declaredFields?.Select(val => instance.GetField<T>(val.Name)).ToArray()!;
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

    // Properties.

    /// <summary>
    ///     Gets a property within the calling instanced object. This can be an internal or private property within another assembly.
    /// </summary>
    /// <typeparam name="T">The type of property to return.</typeparam>
    /// <param name="instance">The instance in which the property resides.</param>
    /// <param name="propertyName">The name of the property to return.</param>
    /// <returns>An object containing the value of the property, reflected by this instance.</returns>
    public static T GetProperty<T>(this object instance, string propertyName)
    {
        return (T)AccessTools.Property(instance.GetType(), propertyName).GetValue(instance)!;
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

    public static T GetStaticProperty<T>(this Type type, string propertyName)
    {
        return (T)AccessTools.Property(type, propertyName).GetValue(null)!;
    }

    public static void SetStaticProperty(this Type type, string propertyName, object setVal)
    {
        AccessTools.Property(type, propertyName).SetValue(null, setVal);
    }

    public static T CallMethod<T>(this object instance, string method, params object[] args)
    {
        return (T)AccessTools.Method(instance.GetType(), method).Invoke(instance, args)!;
    }

    public static T CallAmbigMethod<T>(this object instance, string method, Type[] parameters, params object[] args)
    {
        return (T)AccessTools.Method(instance.GetType(), method, parameters).Invoke(instance, args)!;
    }

    public static void CallMethod(this object instance, string method, params object[] args)
    {
        AccessTools.Method(instance.GetType(), method)?.Invoke(instance, args);
    }

    public static void CallMethod(this object instance, string method)
    {
        AccessTools.Method(instance.GetType(), method)?.Invoke(instance, null);
    }

    public static void CallStaticMethod(this object instance, string method)
    {
        AccessTools.Method(instance.GetType(), method)?.Invoke(null, null);
    }

    public static void CallStaticMethod(this Type type, string method)
    {
        AccessTools.Method(type, method)?.Invoke(null, null);
    }

    public static MethodInfo GetMethod(this object instance, string method, Type[] parameters = null!, Type[] generics = null!)
    {
        return AccessTools.Method(instance.GetType(), method, parameters, generics);
    }

    public static object CreateInstance(this Type type)
    {
        return AccessTools.CreateInstance(type);
    }

    public static Type GetClassType(this Assembly assembly, string className)
    {
        Type[] types = AccessTools.GetTypesFromAssembly(assembly);
        return Array.Find(types, t => t.Name == className)!;
    }

    /// <summary>
    /// Make a deep copy of any object.
    /// </summary>
    public static T DeepClone<T>(this T source) where T : class
    {
        return AccessTools.MakeDeepCopy<T>(source);
    }
}