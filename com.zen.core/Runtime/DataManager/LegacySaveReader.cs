using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Base
{
    /// <summary>
    /// Reads a save written by an older Base with BinaryFormatter (MS-NRBF) and turns it into JSON, without
    /// BinaryFormatter and without the old classes. Field names stay as they were in the old classes (the
    /// "BaseClass+" prefix of inherited fields is dropped), lists become arrays, enums become numbers, so
    /// JsonUtility.FromJsonOverwrite fills any class that kept the same field names.
    /// </summary>
    public static class LegacySaveReader
    {
        private const int MaxDepth = 64;

        private enum RecordType : byte
        {
            SerializedStreamHeader = 0,
            ClassWithId = 1,
            SystemClassWithMembers = 2,
            ClassWithMembers = 3,
            SystemClassWithMembersAndTypes = 4,
            ClassWithMembersAndTypes = 5,
            BinaryObjectString = 6,
            BinaryArray = 7,
            MemberPrimitiveTyped = 8,
            MemberReference = 9,
            ObjectNull = 10,
            MessageEnd = 11,
            BinaryLibrary = 12,
            ObjectNullMultiple256 = 13,
            ObjectNullMultiple = 14,
            ArraySinglePrimitive = 15,
            ArraySingleObject = 16,
            ArraySingleString = 17,
        }

        private enum BinaryType : byte
        {
            Primitive = 0,
            String = 1,
            Object = 2,
            SystemClass = 3,
            Class = 4,
            ObjectArray = 5,
            StringArray = 6,
            PrimitiveArray = 7,
        }

        private sealed class ClassInfo
        {
            public string name;
            public string[] memberNames;
            public BinaryType[] memberTypes;
            public byte[] primitiveTypes;
        }

        private sealed class ClassNode
        {
            public string name;
            public readonly List<KeyValuePair<string, object>> fields = new List<KeyValuePair<string, object>>();

            public object Get(string field)
            {
                foreach (var pair in fields)
                    if (pair.Key == field)
                        return pair.Value;
                return null;
            }
        }

        private sealed class Reference
        {
            public int id;
        }

        private sealed class NullRun
        {
            public int count;
        }

        /// <summary>True when the bytes start like a BinaryFormatter stream.</summary>
        public static bool IsBinaryFormatter(byte[] bytes)
        {
            return bytes != null && bytes.Length > 17 && bytes[0] == (byte)RecordType.SerializedStreamHeader;
        }

        /// <summary>Converts a BinaryFormatter save into JSON. False when the bytes are not a readable stream.</summary>
        public static bool TryReadJson(byte[] bytes, out string json, out string error)
        {
            json = null;
            error = null;
            if (!IsBinaryFormatter(bytes))
            {
                error = "not a BinaryFormatter stream";
                return false;
            }

            try
            {
                var parser = new Parser(bytes);
                var root = parser.Parse();
                var builder = new StringBuilder(bytes.Length * 2);
                new JsonWriter(parser.Objects, builder).Write(root, 0);
                json = builder.ToString();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private sealed class Parser
        {
            private readonly BinaryReader _reader;
            private readonly Dictionary<int, ClassInfo> _classes = new Dictionary<int, ClassInfo>();
            public readonly Dictionary<int, object> Objects = new Dictionary<int, object>();

            public Parser(byte[] bytes)
            {
                _reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8);
            }

            public object Parse()
            {
                if ((RecordType)_reader.ReadByte() != RecordType.SerializedStreamHeader)
                    throw new InvalidDataException("missing stream header");
                var rootId = _reader.ReadInt32();
                _reader.ReadInt32(); // header id
                _reader.ReadInt32(); // major version
                _reader.ReadInt32(); // minor version

                while (true)
                {
                    var record = ReadRecord(out var isEnd);
                    if (isEnd)
                        break;
                    if (record is NullRun)
                        throw new InvalidDataException("null run outside an array");
                }

                if (!Objects.TryGetValue(rootId, out var root))
                    throw new InvalidDataException("root object " + rootId + " not found");
                return root;
            }

            private object ReadRecord(out bool isEnd)
            {
                isEnd = false;
                while (true)
                {
                    var type = (RecordType)_reader.ReadByte();
                    switch (type)
                    {
                        case RecordType.BinaryLibrary:
                            _reader.ReadInt32();
                            _reader.ReadString();
                            continue;
                        case RecordType.MessageEnd:
                            isEnd = true;
                            return null;
                        case RecordType.ClassWithId:
                        {
                            var id = _reader.ReadInt32();
                            var metadataId = _reader.ReadInt32();
                            if (!_classes.TryGetValue(metadataId, out var info))
                                throw new InvalidDataException("unknown class metadata " + metadataId);
                            return ReadClassValues(id, info);
                        }
                        case RecordType.SystemClassWithMembers:
                            return ReadClass(withTypes: false, withLibrary: false);
                        case RecordType.ClassWithMembers:
                            return ReadClass(withTypes: false, withLibrary: true);
                        case RecordType.SystemClassWithMembersAndTypes:
                            return ReadClass(withTypes: true, withLibrary: false);
                        case RecordType.ClassWithMembersAndTypes:
                            return ReadClass(withTypes: true, withLibrary: true);
                        case RecordType.BinaryObjectString:
                        {
                            var id = _reader.ReadInt32();
                            var value = _reader.ReadString();
                            Objects[id] = value;
                            return value;
                        }
                        case RecordType.BinaryArray:
                            return ReadBinaryArray();
                        case RecordType.MemberPrimitiveTyped:
                            return ReadPrimitive(_reader.ReadByte());
                        case RecordType.MemberReference:
                            return new Reference { id = _reader.ReadInt32() };
                        case RecordType.ObjectNull:
                            return null;
                        case RecordType.ObjectNullMultiple256:
                            return new NullRun { count = _reader.ReadByte() };
                        case RecordType.ObjectNullMultiple:
                            return new NullRun { count = _reader.ReadInt32() };
                        case RecordType.ArraySinglePrimitive:
                        {
                            var id = _reader.ReadInt32();
                            var length = _reader.ReadInt32();
                            var primitive = _reader.ReadByte();
                            var list = new List<object>(length);
                            Objects[id] = list;
                            for (var i = 0; i < length; i++)
                                list.Add(ReadPrimitive(primitive));
                            return list;
                        }
                        case RecordType.ArraySingleObject:
                        case RecordType.ArraySingleString:
                        {
                            var id = _reader.ReadInt32();
                            var length = _reader.ReadInt32();
                            var list = new List<object>(length);
                            Objects[id] = list;
                            ReadRecordElements(list, length);
                            return list;
                        }
                        default:
                            throw new InvalidDataException("unsupported record " + (int)type);
                    }
                }
            }

            private ClassNode ReadClass(bool withTypes, bool withLibrary)
            {
                var id = _reader.ReadInt32();
                var info = new ClassInfo { name = _reader.ReadString() };
                var count = _reader.ReadInt32();
                info.memberNames = new string[count];
                for (var i = 0; i < count; i++)
                    info.memberNames[i] = _reader.ReadString();

                info.memberTypes = new BinaryType[count];
                info.primitiveTypes = new byte[count];
                if (withTypes)
                {
                    for (var i = 0; i < count; i++)
                        info.memberTypes[i] = (BinaryType)_reader.ReadByte();
                    for (var i = 0; i < count; i++)
                    {
                        switch (info.memberTypes[i])
                        {
                            case BinaryType.Primitive:
                            case BinaryType.PrimitiveArray:
                                info.primitiveTypes[i] = _reader.ReadByte();
                                break;
                            case BinaryType.SystemClass:
                                _reader.ReadString();
                                break;
                            case BinaryType.Class:
                                _reader.ReadString();
                                _reader.ReadInt32();
                                break;
                        }
                    }
                }
                else
                {
                    for (var i = 0; i < count; i++)
                        info.memberTypes[i] = BinaryType.Object;
                }

                if (withLibrary)
                    _reader.ReadInt32();

                _classes[id] = info;
                return ReadClassValues(id, info);
            }

            private ClassNode ReadClassValues(int id, ClassInfo info)
            {
                var node = new ClassNode { name = info.name };
                Objects[id] = node;
                for (var i = 0; i < info.memberNames.Length; i++)
                {
                    object value;
                    if (info.memberTypes[i] == BinaryType.Primitive)
                    {
                        value = ReadPrimitive(info.primitiveTypes[i]);
                    }
                    else
                    {
                        value = ReadRecord(out var isEnd);
                        if (isEnd || value is NullRun)
                            throw new InvalidDataException("unexpected record in " + info.name);
                    }
                    node.fields.Add(new KeyValuePair<string, object>(info.memberNames[i], value));
                }
                return node;
            }

            private List<object> ReadBinaryArray()
            {
                var id = _reader.ReadInt32();
                var arrayType = _reader.ReadByte();
                var rank = _reader.ReadInt32();
                var length = 1;
                for (var i = 0; i < rank; i++)
                    length *= _reader.ReadInt32();
                // SingleOffset, JaggedOffset, RectangularOffset carry lower bounds.
                if (arrayType >= 3 && arrayType <= 5)
                    for (var i = 0; i < rank; i++)
                        _reader.ReadInt32();

                var elementType = (BinaryType)_reader.ReadByte();
                byte primitive = 0;
                switch (elementType)
                {
                    case BinaryType.Primitive:
                    case BinaryType.PrimitiveArray:
                        primitive = _reader.ReadByte();
                        break;
                    case BinaryType.SystemClass:
                        _reader.ReadString();
                        break;
                    case BinaryType.Class:
                        _reader.ReadString();
                        _reader.ReadInt32();
                        break;
                }

                var list = new List<object>(length);
                Objects[id] = list;
                if (elementType == BinaryType.Primitive)
                {
                    for (var i = 0; i < length; i++)
                        list.Add(ReadPrimitive(primitive));
                }
                else
                {
                    ReadRecordElements(list, length);
                }
                return list;
            }

            private void ReadRecordElements(List<object> list, int length)
            {
                while (list.Count < length)
                {
                    var value = ReadRecord(out var isEnd);
                    if (isEnd)
                        throw new InvalidDataException("array ended early");
                    if (value is NullRun run)
                    {
                        for (var i = 0; i < run.count && list.Count < length; i++)
                            list.Add(null);
                    }
                    else
                    {
                        list.Add(value);
                    }
                }
            }

            private object ReadPrimitive(byte type)
            {
                switch (type)
                {
                    case 1: return _reader.ReadBoolean();
                    case 2: return _reader.ReadByte();
                    case 3: return _reader.ReadChar();
                    case 5: return decimal.Parse(_reader.ReadString(), CultureInfo.InvariantCulture);
                    case 6: return _reader.ReadDouble();
                    case 7: return _reader.ReadInt16();
                    case 8: return _reader.ReadInt32();
                    case 9: return _reader.ReadInt64();
                    case 10: return _reader.ReadSByte();
                    case 11: return _reader.ReadSingle();
                    case 12: return _reader.ReadInt64();                            // TimeSpan ticks
                    case 13: return _reader.ReadInt64() & 0x3FFFFFFFFFFFFFFF;       // DateTime ticks without Kind
                    case 14: return _reader.ReadUInt16();
                    case 15: return _reader.ReadUInt32();
                    case 16: return _reader.ReadUInt64();
                    case 17: return null;
                    case 18: return _reader.ReadString();
                    default: throw new InvalidDataException("unsupported primitive " + type);
                }
            }
        }

        private sealed class JsonWriter
        {
            private readonly Dictionary<int, object> _objects;
            private readonly StringBuilder _json;
            private readonly HashSet<object> _writing = new HashSet<object>();

            public JsonWriter(Dictionary<int, object> objects, StringBuilder json)
            {
                _objects = objects;
                _json = json;
            }

            public void Write(object value, int depth)
            {
                if (value is Reference reference)
                    value = _objects.TryGetValue(reference.id, out var target) ? target : null;

                if (depth > MaxDepth || (value != null && _writing.Contains(value)))
                {
                    _json.Append("null");
                    return;
                }

                switch (value)
                {
                    case null:
                        _json.Append("null");
                        return;
                    case string text:
                        WriteString(text);
                        return;
                    case char character:
                        WriteString(character.ToString());
                        return;
                    case bool flag:
                        _json.Append(flag ? "true" : "false");
                        return;
                    case float single:
                        if (float.IsNaN(single) || float.IsInfinity(single))
                            _json.Append('0');
                        else
                            _json.Append(single.ToString("R", CultureInfo.InvariantCulture));
                        return;
                    case double number:
                        WriteNumber(number);
                        return;
                    case decimal number:
                        _json.Append(number.ToString(CultureInfo.InvariantCulture));
                        return;
                    case IFormattable integer:
                        _json.Append(integer.ToString(null, CultureInfo.InvariantCulture));
                        return;
                }

                _writing.Add(value);
                if (value is List<object> list)
                    WriteArray(list, list.Count, depth);
                else if (value is ClassNode node)
                    WriteClass(node, depth);
                else
                    _json.Append("null");
                _writing.Remove(value);
            }

            private void WriteClass(ClassNode node, int depth)
            {
                // List<T>: the items array holds Capacity slots, only the first _size are real.
                if (node.name.StartsWith("System.Collections.Generic.List`1", StringComparison.Ordinal))
                {
                    var items = Resolve(node.Get("_items")) as List<object>;
                    var size = node.Get("_size") is int count ? count : items?.Count ?? 0;
                    if (items == null)
                        _json.Append("[]");
                    else
                        WriteArray(items, Math.Min(size, items.Count), depth);
                    return;
                }

                // Enums are written as a class with a single value__ member.
                if (node.fields.Count == 1 && node.fields[0].Key == "value__")
                {
                    Write(node.fields[0].Value, depth + 1);
                    return;
                }

                _json.Append('{');
                var written = new HashSet<string>();
                foreach (var pair in node.fields)
                {
                    var name = FieldName(pair.Key);
                    if (!written.Add(name))
                        continue;
                    if (written.Count > 1)
                        _json.Append(',');
                    WriteString(name);
                    _json.Append(':');
                    Write(pair.Value, depth + 1);
                }
                _json.Append('}');
            }

            private void WriteArray(List<object> items, int count, int depth)
            {
                _json.Append('[');
                for (var i = 0; i < count; i++)
                {
                    if (i > 0)
                        _json.Append(',');
                    Write(items[i], depth + 1);
                }
                _json.Append(']');
            }

            private object Resolve(object value)
            {
                if (value is Reference reference)
                    return _objects.TryGetValue(reference.id, out var target) ? target : null;
                return value;
            }

            // Inherited fields are written as "DeclaringClass+field".
            private static string FieldName(string memberName)
            {
                var plus = memberName.LastIndexOf('+');
                return plus >= 0 ? memberName.Substring(plus + 1) : memberName;
            }

            private void WriteNumber(double number)
            {
                if (double.IsNaN(number) || double.IsInfinity(number))
                    _json.Append('0');
                else
                    _json.Append(number.ToString("R", CultureInfo.InvariantCulture));
            }

            private void WriteString(string text)
            {
                _json.Append('"');
                foreach (var c in text)
                {
                    switch (c)
                    {
                        case '"': _json.Append("\\\""); break;
                        case '\\': _json.Append("\\\\"); break;
                        case '\n': _json.Append("\\n"); break;
                        case '\r': _json.Append("\\r"); break;
                        case '\t': _json.Append("\\t"); break;
                        default:
                            if (c < 0x20)
                                _json.Append("\\u").Append(((int)c).ToString("x4"));
                            else
                                _json.Append(c);
                            break;
                    }
                }
                _json.Append('"');
            }
        }
    }
}
