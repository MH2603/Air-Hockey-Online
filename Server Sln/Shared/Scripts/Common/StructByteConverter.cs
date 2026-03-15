using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

public class StructByteConverter
{
    public static T ToStruct<T>(byte[] bytes, int offset = 0) where T : struct
    {
        try
        {
            int size = Marshal.SizeOf<T>();
            if (bytes.Length - offset < size)
                throw new ArgumentException($"Byte array {bytes.Length - offset} is too small for struct {typeof(T).Name}. Need {size} bytes.");

            return MemoryMarshal.Read<T>(bytes.AsSpan(offset));
        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message, ELogLevel.Error);
            return default;
        }
    }

    /// <summary>
    /// Converts a struct into a byte array by iterating its fields.
    /// </summary>
    public static byte[] ToBytes<T>(T value)
    {
        using var stream = new MemoryStream();
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        try
        {
            foreach (var field in fields)
            {
                object? fieldValue = field.GetValue(value);
                byte[] fieldBytes = ConvertFieldToBytes(fieldValue, field);
                stream.Write(fieldBytes, 0, fieldBytes.Length);
            }
        }
        catch (Exception e)
        {
            Logger.Log(e.Message, ELogLevel.Error);
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Converts a single field value into bytes depending on its type.
    /// </summary>
    private static byte[] ConvertFieldToBytes(object? value, FieldInfo field)
    {
        if (value == null)
            return Array.Empty<byte>();

        Type type = value.GetType();

        // Array
        if (type.IsArray)
        {
            Array arr = (Array)value;
            Type elemType = type.GetElementType()!;

            if (elemType == typeof(byte))
                return NormalizeByteArray((byte[])value, arr.Length);

            var list = new List<byte>();
            for (int i = 0; i < arr.Length; i++)
            {
                object elem = arr.GetValue(i)!;
                byte[] elemBytes = ToBytes(elem);
                list.AddRange(elemBytes);
            }
            return list.ToArray();
        }

        // Primitives
        if (type == typeof(byte)) return new[] { (byte)value };
        if (type == typeof(bool)) return BitConverter.GetBytes((bool)value);
        if (type == typeof(short)) return BitConverter.GetBytes((short)value);
        if (type == typeof(ushort)) return BitConverter.GetBytes((ushort)value);
        if (type == typeof(int)) return BitConverter.GetBytes((int)value);
        if (type == typeof(uint)) return BitConverter.GetBytes((uint)value);
        if (type == typeof(long)) return BitConverter.GetBytes((long)value);
        if (type == typeof(ulong)) return BitConverter.GetBytes((ulong)value);
        if (type == typeof(float)) return BitConverter.GetBytes((float)value);
        if (type == typeof(double)) return BitConverter.GetBytes((double)value);

        // Byte array
        if (type == typeof(byte[]))
        {
            int sizeConst = GetMarshalSizeConst(field);
            return sizeConst > 0 ? NormalizeByteArray((byte[])value, sizeConst) : (byte[])value;
        }

        // String (UTF8 with 2-byte length prefix)
        if (type == typeof(string))
        {
            byte[] strBytes = Encoding.UTF8.GetBytes((string)value);
            byte[] lengthBytes = BitConverter.GetBytes((ushort)strBytes.Length);
            return lengthBytes.Concat(strBytes).ToArray();
        }

        // Nested struct
        if (type.IsValueType && !type.IsPrimitive)
        {
            var toBytesMethod = typeof(StructByteConverter)
                .GetMethods()
                .FirstOrDefault(m => m.Name == nameof(ToBytes) && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1)
                ?? throw new NotSupportedException($"Could not find ToBytes method for type: {type}");
            var result = toBytesMethod.MakeGenericMethod(type).Invoke(null, new[] { value });
            return result is byte[] bytes ? bytes : Array.Empty<byte>();
        }

        throw new NotSupportedException($"Unsupported field type: {type}");
    }

    private static int GetMarshalSizeConst(FieldInfo field)
    {
        if (field == null) return -1;
        var attr = field.GetCustomAttribute<MarshalAsAttribute>();
        return attr?.SizeConst ?? -1;
    }

    private static byte[] NormalizeByteArray(byte[]? value, int size)
    {
        var result = new byte[size];
        if (value != null)
            Array.Copy(value, result, Math.Min(size, value.Length));
        return result;
    }
}
