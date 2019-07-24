using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Kawa.Json
{
	public static partial class Json5
	{
		/// <summary>
		/// Parses Starbound Versioned JSON from a stream into an object. The input stream can be headered or bare. If the input stream is headered, the resulting object will be wrapped to preserve versioning information.
		/// </summary>
		/// <param name="stream">The input stream to parse from.</param>
		/// <returns>A JsonObj, list, string, double...</returns>
		public static object Deserialize(BinaryReader stream)
		{
			Func<object> something = null; //placeholder for @array and @object

			Func<double> @double = () =>
			{
				var moto8 = stream.ReadBytes(8);
				var intel8 = new[] { moto8[7], moto8[6], moto8[5], moto8[4], moto8[3], moto8[2], moto8[1], moto8[0] };
				var ret = 0.0;
				using (var intel = new BinaryReader(new MemoryStream(intel8)))
					ret = intel.ReadDouble();
				return ret;
			};
			
			Func<bool> @bool = () =>
			{
				return stream.ReadBoolean();
			};

			Func<long> @int = () =>
			{
				return stream.ReadVLQSigned();
			};

			Func<string> @string = () =>
			{
				var len = (int)stream.ReadVLQUnsigned();
				var str = Encoding.UTF8.GetString(stream.ReadBytes(len));
				return str;
			};

			Func<object> @array = () =>
			{
				var count = (int)stream.ReadVLQUnsigned();
				var ret = new object[count];
				for (var i = 0; i < count; i++)
				{
					ret[i] = @something();
				}

				var allAreNull = ret.All(i => i == null);
				if (!allAreNull)
				{
					var allAreSameType = true;
					foreach (var i in ret)
					{
						if (i == null)
						{
							continue;
						}
						if (!(i is string))
						{
							allAreSameType = false;
							break;
						}
					}
					if (allAreSameType)
					{
						return ret.Cast<string>().ToList();
					}

					allAreSameType = true;
					foreach (var i in ret)
					{
						if (i == null)
						{
							continue;
						}
						if (!(i is int))
						{
							allAreSameType = false;
							break;
						}
					}
					if (allAreSameType)
					{
						return ret.Cast<int>().ToList();
					}

					allAreSameType = true;
					foreach (var i in ret)
					{
						if (i == null)
						{
							continue;
						}
						if (!(i is double))
						{
							allAreSameType = false;
							break;
						}
					}
					if (allAreSameType)
					{
						return ret.Cast<double>().ToList();
					}
				}

				return ret.ToList();
			};

			Func<JsonObj> @object = () =>
			{
				var count = (int)stream.ReadVLQUnsigned();
				var ret = new JsonObj();
				for (var i = 0; i < count; i++)
				{
					ret.Add(@string(), @something());
				}
				return ret;
			};

			something = () =>
			{
				var type = stream.ReadByte();
				switch (type)
				{
					case 1: return null;
					case 2: return @double();
					case 3: return @bool();
					case 4: return @int();
					case 5: return @string();
					case 6: return @array();
					case 7: return @object();
					default: throw new JsonException(string.Format("Unknown item type 0x{0:X2} while deserializing. Stream offset: 0x{1:X}", type, stream.BaseStream.Position));
				}
			};

			if ((char)stream.PeekChar() == 'S')
			{
				//SBVJ file?
				var sbvj = new string(stream.ReadChars(6));
				if (sbvj != "SBVJ01")
				{
					throw new JsonException("File does not start with a valid object identifier, nor is it a Starbound Versioned JSON file.");
				}
				var identifier = @string();
				var hasVersion = @bool();
				var version = 0;
				if (hasVersion)
				{
					var moto4 = stream.ReadBytes(4);
					var intel4 = new[] { moto4[3], moto4[2], moto4[1], moto4[0] };
					using (var intel = new BinaryReader(new MemoryStream(intel4)))
					{
						version = intel.ReadInt32();
					}
				}
				var wrapper = new JsonObj();
				wrapper["__id"] = identifier;
				if (hasVersion)
				{
					wrapper["__version"] = version;
				}
				wrapper["__content"] = something();
				return wrapper;
			}

			return something();
		}

		/// <summary>
		/// Converts an object fit for Parse into Starbound Versioned JSON. If the input was itself a headered SBVJ object, the header will be reconstructed.
		/// </summary>
		/// <param name="stream">The output stream to write to.</param>
		/// <param name="object">A JsonObj, list, string, double... likely a JsonObj.</param>
		public static void Serialize(BinaryWriter stream, object @object)
		{
			Action<object> internalSerialize = null;

			Action writeNull = () =>
			{
				stream.Write((byte)1);
			};

			Action<double> writeDouble = (d) =>
			{
				var intel8 = new MemoryStream(8);
				using (var intel = new BinaryWriter(intel8))
				{
					intel.Write(d);
				}
				var b = intel8.GetBuffer();
				var moto8 = new[] { b[7], b[6], b[5], b[4], b[3], b[2], b[1], b[0] };
				stream.Write((byte)2);
				stream.Write(moto8);
			};
			
			Action<bool> writeBool = (b) =>
			{
				stream.Write((byte)3);
				stream.Write(b);
			};

			Action<long> writeInt = (i) =>
			{
				stream.Write((byte)4);
				stream.WriteVLQ(i);
			};

			Action<string> writeString = (s) =>
			{
				stream.Write((byte)5);
				stream.WriteVLQ((ulong)Encoding.UTF8.GetByteCount(s));
				stream.Write(Encoding.UTF8.GetBytes(s));
			};

			Action<object[]> writeArray = (a) =>
			{
				stream.Write((byte)6);
				stream.WriteVLQ((ulong)a.Length);
				foreach (var item in a)
				{
					internalSerialize(item);
				}
			};

			Action<JsonObj> writeObj = (o) =>
			{
				stream.Write((byte)7);
				stream.WriteVLQ((ulong)o.Count);
				foreach (var item in o)
				{
					stream.WriteVLQ((ulong)Encoding.UTF8.GetByteCount(item.Key));
					stream.Write(Encoding.UTF8.GetBytes(item.Key));
					internalSerialize(item.Value);
				}
			};

			internalSerialize = (obj_part) =>
			{
				if (obj_part == null)
				{
					writeNull();
				}
				else if (obj_part is double || obj_part is float)
				{
					writeDouble((double)obj_part);
				}
				else if (obj_part is bool)
				{
					writeBool((bool)obj_part);
				}
				else if (obj_part is int)
				{
					writeInt((long)((int)obj_part));
				}
				else if (obj_part is long)
				{
					writeInt((long)obj_part);
				}
				else if (obj_part is string)
				{
					writeString((string)obj_part);
				}
				else if (obj_part is double[])
				{
					writeArray(((double[])obj_part).Cast<object>().ToArray());
				}
				else if (obj_part is int[])
				{
					writeArray(((int[])obj_part).Cast<object>().ToArray());
				}
				else if (obj_part is long[])
				{
					writeArray(((long[])obj_part).Cast<object>().ToArray());
				}
				else if (obj_part is float[])
				{
					writeArray(((float[])obj_part).Cast<object>().ToArray());
				}
				else if (obj_part is string[])
				{
					writeArray(((string[])obj_part).Cast<object>().ToArray());
				}
				else if (obj_part is List<double>)
				{
					writeArray(((List<double>)obj_part).Cast<object>().ToArray());
				}
				else if (obj_part is List<int>)
				{
					writeArray(((List<int>)obj_part).Cast<object>().ToArray());
				}
				else if (obj_part is List<long>)
				{
					writeArray(((List<long>)obj_part).Cast<object>().ToArray());
				}
				else if (obj_part is List<float>)
				{
					writeArray(((List<float>)obj_part).Cast<object>().ToArray());
				}
				else if (obj_part is List<string>)
				{
					writeArray(((List<string>)obj_part).Cast<object>().ToArray());
				}
				else if (obj_part is object[])
				{
					writeArray((object[])obj_part);
				}
				else if (obj_part is List<object>)
				{
					writeArray(((List<object>)obj_part).ToArray());
				}
				else if (obj_part is JsonObj)
				{
					writeObj((JsonObj)obj_part);
				}
				else
				{
					throw new JsonException(string.Format("Don't know how to serialize object of type \"{0}\".", obj_part.GetType().Name));
				}
			};

			if (@object is JsonObj && ((JsonObj)@object).ContainsKey("__id"))
			{
				var sbvj = (JsonObj)@object;
				stream.Write("SBVJ01".ToCharArray());
				//Can't use writeString because we don't want a prefix.
				stream.WriteVLQ((ulong)Encoding.UTF8.GetByteCount((string)sbvj["__id"]));
				stream.Write(Encoding.UTF8.GetBytes((string)sbvj["__id"]));
				if (sbvj.ContainsKey("__version"))
				{
					stream.Write((byte)1);
					var intel4 = new MemoryStream(4);
					using (var intel = new BinaryWriter(intel4))
					{
						try
						{
							intel.Write((int)((double)sbvj["__version"]));
						}
						catch (InvalidCastException)
						{
							intel.Write((int)sbvj["__version"]);
						}
					}
					var b = intel4.GetBuffer();
					var moto4 = new[] { b[3], b[2], b[1], b[0] };
					stream.Write(moto4);
				}
				else
				{
					stream.Write((byte)0);
				}
				internalSerialize(sbvj["__content"]);
				return;
			}

			internalSerialize(@object);
		}
	}

	public static class Extensions
	{
		public static void WriteVLQ(this BinaryWriter stream, ulong x)
		{
			var i = 0;
			for (i = 9; i > 0; --i)
			{
				if ((x & ((ulong)(127) << (i * 7))) > 0)
				{
					break;
				}
			}
			for (var j = 0; j < i; ++j)
			{
				var oct = ((x >> ((i - j) * 7)) & 127) | 128;
				stream.Write((byte)oct);
			}
			stream.Write((byte)(x & 127));
		}

		public static ulong ReadVLQUnsigned(this BinaryReader stream)
		{
			ulong x = 0;
			for (var i = 0; i < 10; ++i)
			{
				var oct = stream.ReadByte();
				x = (x << 7) | ((ulong)oct & 127);
				if ((oct & 128) == 0)
				{
					return x;
				}
			}
			throw new JsonException("Couldn't read VLQ number.");
		}

		public static void WriteVLQ(this BinaryWriter stream, long v)
		{
			ulong target = 0;
			if (v < 0)
			{
				target = (ulong)((-(v + 1)) << 1) | 1;
			}
			else
			{
				target = (ulong)v << 1;
			}
			WriteVLQ(stream, target);
		}

		public static long ReadVLQSigned(this BinaryReader stream)
		{
			ulong source = ReadVLQUnsigned(stream);
			bool negative = (source & 1) == 1;
			if (negative)
			{
				return -(long)(source >> 1) - 1;
			}
			else
			{
				return (long)(source >> 1);
			}
		}
	}
}
