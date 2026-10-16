using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;

namespace TacticsECS
{
    /// <summary>
    /// 값만 가진 데이터 객체 그래프(Data/Core 계층: GridWorld, EntityWorld, EconomyWorld …)를 바이트로 바꾸고 되돌린다 — 저장·이어하기용(SaveSystem).
    /// 필드를 리플렉션으로 전부(비공개·readonly 포함, 정적 제외) 따라가므로 데이터 타입에 필드가 늘어도 이 코드는 그대로다.
    /// 같은 객체를 여러 곳에서 가리키면(GridWorld.Economy ↔ EconomyWorld) 복원 뒤에도 같은 객체다. 지원: 기본형·enum·string·struct·class·
    /// 배열·List/Dictionary/HashSet·Nullable·System.Type 값. 위임자(delegate)·Unity 오브젝트는 데이터에 있으면 안 되므로 예외를 던진다.
    /// </summary>
    public static class GraphSerializer
    {
        private const byte Null = 0, NewObject = 1, Reference = 2, StringValue = 3, TypeValue = 4;
        private const int Magic = 0x47534731; // "GSG1"

        public static byte[] Serialize(object root)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                var context = new WriteContext(writer);
                writer.Write(Magic);
                WriteValue(context, root, typeof(object), "root");
            }
            return stream.ToArray();
        }

        public static T Deserialize<T>(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            if (reader.ReadInt32() != Magic) throw new InvalidDataException("GraphSerializer: unknown format");
            return (T)ReadValue(new ReadContext(reader), typeof(object));
        }

        // ---------- 쓰기 ----------

        private sealed class WriteContext
        {
            public readonly BinaryWriter Writer;
            public readonly Dictionary<object, int> Ids = new Dictionary<object, int>(new IdentityComparer());
            public readonly Dictionary<Type, int> Types = new Dictionary<Type, int>();
            public WriteContext(BinaryWriter writer) => Writer = writer;
        }

        private static void WriteValue(WriteContext c, object value, Type declared, string path)
        {
            var w = c.Writer;
            if (IsInlineValue(declared))
            {
                WriteInline(c, value, declared, path);
                return;
            }
            if (value == null) { w.Write(Null); return; }
            if (value is string s) { w.Write(StringValue); w.Write(s); return; }
            if (value is Type t) { w.Write(TypeValue); w.Write(TypeName(t)); return; }

            var type = value.GetType();
            if (typeof(Delegate).IsAssignableFrom(type) || typeof(UnityEngine.Object).IsAssignableFrom(type))
                throw new NotSupportedException($"GraphSerializer: {path} is {type.Name} — not plain data");
            if (!type.IsValueType && c.Ids.TryGetValue(value, out int id)) { w.Write(Reference); w.Write(id); return; }

            w.Write(NewObject);
            WriteType(c, type);
            if (!type.IsValueType) c.Ids[value] = c.Ids.Count;
            WriteContent(c, value, type, path);
        }

        /// <summary>선언 타입이 기본형·enum·struct면 타입 정보 없이 바로 쓴다(값이 null일 수 없고 실제 타입 = 선언 타입).</summary>
        private static void WriteInline(WriteContext c, object value, Type type, string path)
        {
            var w = c.Writer;
            if (type.IsEnum) { w.Write(Convert.ToInt64(value)); return; }
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean: w.Write((bool)value); return;
                case TypeCode.Byte: w.Write((byte)value); return;
                case TypeCode.SByte: w.Write((sbyte)value); return;
                case TypeCode.Int16: w.Write((short)value); return;
                case TypeCode.UInt16: w.Write((ushort)value); return;
                case TypeCode.Char: w.Write((char)value); return;
                case TypeCode.Int32: w.Write((int)value); return;
                case TypeCode.UInt32: w.Write((uint)value); return;
                case TypeCode.Int64: w.Write((long)value); return;
                case TypeCode.UInt64: w.Write((ulong)value); return;
                case TypeCode.Single: w.Write((float)value); return;
                case TypeCode.Double: w.Write((double)value); return;
                case TypeCode.Decimal: w.Write((decimal)value); return;
            }
            var nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null)
            {
                w.Write(value != null);
                if (value != null) WriteInline(c, value, nullable, path);
                return;
            }
            WriteFields(c, value, type, path); // struct
        }

        private static void WriteContent(WriteContext c, object value, Type type, string path)
        {
            var w = c.Writer;
            if (type.IsArray)
            {
                var array = (Array)value;
                if (array.Rank != 1) throw new NotSupportedException($"GraphSerializer: {path} multi-dimensional array");
                var element = type.GetElementType();
                w.Write(array.Length);
                for (int i = 0; i < array.Length; i++) WriteValue(c, array.GetValue(i), element, path + "[" + i + "]");
                return;
            }
            if (IsGeneric(type, typeof(List<>)) || IsGeneric(type, typeof(HashSet<>)))
            {
                var element = type.GetGenericArguments()[0];
                var items = ((IEnumerable)value).Cast<object>().ToList();
                w.Write(items.Count);
                for (int i = 0; i < items.Count; i++) WriteValue(c, items[i], element, path + "[" + i + "]");
                return;
            }
            if (IsGeneric(type, typeof(Dictionary<,>)))
            {
                var args = type.GetGenericArguments();
                var dict = (IDictionary)value;
                w.Write(dict.Count);
                foreach (DictionaryEntry e in dict)
                {
                    WriteValue(c, e.Key, args[0], path + ".key");
                    WriteValue(c, e.Value, args[1], path + "[" + e.Key + "]");
                }
                return;
            }
            if (type.IsValueType && IsInlineValue(type)) { WriteInline(c, value, type, path); return; }
            WriteFields(c, value, type, path);
        }

        private static void WriteFields(WriteContext c, object value, Type type, string path)
        {
            foreach (var field in Fields(type))
                WriteValue(c, field.GetValue(value), field.FieldType, path + "." + field.Name);
        }

        private static void WriteType(WriteContext c, Type type)
        {
            if (c.Types.TryGetValue(type, out int id)) { c.Writer.Write(id); return; }
            id = c.Types.Count;
            c.Types[type] = id;
            c.Writer.Write(id);
            c.Writer.Write(TypeName(type));
        }

        // ---------- 읽기 ----------

        private sealed class ReadContext
        {
            public readonly BinaryReader Reader;
            public readonly List<object> Objects = new List<object>();
            public readonly List<Type> Types = new List<Type>();
            public ReadContext(BinaryReader reader) => Reader = reader;
        }

        private static object ReadValue(ReadContext c, Type declared)
        {
            if (IsInlineValue(declared)) return ReadInline(c, declared);
            var r = c.Reader;
            byte tag = r.ReadByte();
            switch (tag)
            {
                case Null: return null;
                case StringValue: return r.ReadString();
                case TypeValue: return ResolveType(r.ReadString());
                case Reference: return c.Objects[r.ReadInt32()];
                case NewObject:
                    var type = ReadType(c);
                    return ReadContent(c, type);
                default: throw new InvalidDataException("GraphSerializer: bad tag " + tag);
            }
        }

        private static object ReadInline(ReadContext c, Type type)
        {
            var r = c.Reader;
            if (type.IsEnum) return Enum.ToObject(type, r.ReadInt64());
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean: return r.ReadBoolean();
                case TypeCode.Byte: return r.ReadByte();
                case TypeCode.SByte: return r.ReadSByte();
                case TypeCode.Int16: return r.ReadInt16();
                case TypeCode.UInt16: return r.ReadUInt16();
                case TypeCode.Char: return r.ReadChar();
                case TypeCode.Int32: return r.ReadInt32();
                case TypeCode.UInt32: return r.ReadUInt32();
                case TypeCode.Int64: return r.ReadInt64();
                case TypeCode.UInt64: return r.ReadUInt64();
                case TypeCode.Single: return r.ReadSingle();
                case TypeCode.Double: return r.ReadDouble();
                case TypeCode.Decimal: return r.ReadDecimal();
            }
            var nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null) return r.ReadBoolean() ? ReadInline(c, nullable) : null;
            var boxed = FormatterServices.GetUninitializedObject(type);
            ReadFields(c, boxed, type);
            return boxed;
        }

        private static object ReadContent(ReadContext c, Type type)
        {
            var r = c.Reader;
            if (type.IsArray)
            {
                var element = type.GetElementType();
                var array = Array.CreateInstance(element, r.ReadInt32());
                c.Objects.Add(array);
                for (int i = 0; i < array.Length; i++) array.SetValue(ReadValue(c, element), i);
                return array;
            }
            if (IsGeneric(type, typeof(List<>)) || IsGeneric(type, typeof(HashSet<>)))
            {
                var element = type.GetGenericArguments()[0];
                var collection = Activator.CreateInstance(type);
                c.Objects.Add(collection);
                var add = type.GetMethod("Add", new[] { element });
                int count = r.ReadInt32();
                var args = new object[1];
                for (int i = 0; i < count; i++) { args[0] = ReadValue(c, element); add.Invoke(collection, args); }
                return collection;
            }
            if (IsGeneric(type, typeof(Dictionary<,>)))
            {
                var kv = type.GetGenericArguments();
                var dict = (IDictionary)Activator.CreateInstance(type);
                c.Objects.Add(dict);
                int count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    var key = ReadValue(c, kv[0]);
                    dict[key] = ReadValue(c, kv[1]);
                }
                return dict;
            }
            if (type.IsValueType) return ReadInline(c, type); // object 칸에 상자로 담긴 값
            var obj = FormatterServices.GetUninitializedObject(type);
            c.Objects.Add(obj);
            ReadFields(c, obj, type);
            return obj;
        }

        private static void ReadFields(ReadContext c, object target, Type type)
        {
            foreach (var field in Fields(type))
                field.SetValue(target, ReadValue(c, field.FieldType));
        }

        private static Type ReadType(ReadContext c)
        {
            int id = c.Reader.ReadInt32();
            if (id < c.Types.Count) return c.Types[id];
            var type = ResolveType(c.Reader.ReadString());
            c.Types.Add(type);
            return type;
        }

        // ---------- 공통 ----------

        private static bool IsInlineValue(Type type) => type.IsValueType;

        private static bool IsGeneric(Type type, Type definition) => type.IsGenericType && type.GetGenericTypeDefinition() == definition;

        /// <summary>인스턴스 필드 전부(상속 포함), 순서는 선언 타입 → 이름. 쓰기·읽기가 같은 순서를 쓴다.</summary>
        private static List<FieldInfo> Fields(Type type)
        {
            var fields = new List<FieldInfo>();
            for (var t = type; t != null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType)
                fields.AddRange(t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .OrderBy(f => f.Name, StringComparer.Ordinal));
            return fields;
        }

        private static string TypeName(Type type) => type.FullName + ", " + type.Assembly.GetName().Name;

        private static Type ResolveType(string name) =>
            Type.GetType(name) ?? throw new InvalidDataException("GraphSerializer: unknown type " + name);

        private sealed class IdentityComparer : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
        }
    }
}
