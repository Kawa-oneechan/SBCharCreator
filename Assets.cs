using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using Kawa.Json;

namespace SBCharCreator
{
	public class Asset
	{
		public string Source { get; private set; }
		public string Name { get; private set; }
		public int Offset { get; private set; }
		public int Length { get; private set; }
		public bool FromPak { get { return Offset > 0; } }

		public Asset(string source, string name)
		{
			this.Source = source;
			this.Name = name;
		}

		public Asset(string source, string name, int offset, int length)
		{
			this.Source = source;
			this.Name = name;
			this.Offset = offset;
			this.Length = length;
		}

		public override string ToString()
		{
			if (this.FromPak)
				return string.Format("pak:{0}", this.Name);
			return this.Name;
		}

		public Stream Open()
		{
			if (this.FromPak)
			{
				using (var f = new BinaryReader(File.Open(this.Source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
				{
					f.BaseStream.Seek(this.Offset, SeekOrigin.Begin);
					var data = f.ReadBytes(this.Length);
					return new MemoryStream(data);
				}
			}
			return File.Open(this.Source, FileMode.Open);
		}
	}

	public sealed class AssetException : Exception
	{
		public AssetException(string message) : base(message)
		{
		}
	}

	public static class Assets
	{
		public static List<Asset> Files { get; private set; }
		private static Dictionary<string, object> cache;
		private static Dictionary<string, Frames> framesCache;
		private static Bitmap errorFallback;
		
		static Assets()
		{
			Files = new List<Asset>();
			cache = new Dictionary<string, object>();
			framesCache = new Dictionary<string, Frames>();
			errorFallback = global::SBCharCreator.Properties.Resources.errorFallback;
		}

		public static void AddSource(string path)
		{
			if (path.EndsWith(".pak"))
			{
				try
				{
					AddPak(path);
				}
				catch (AssetException)
				{
				}
			}
			if (Directory.Exists(path))
				AddAllFrom(path);
		}

		public static void AddAllFrom(string path)
		{
			var sources = new List<string>();
			if (File.Exists(Path.Combine(path, ".metadata")) || File.Exists(Path.Combine(path, "_metadata")))
				sources.Add(path);
			else
			{
				foreach (var source in Directory.EnumerateFiles(path, "*.pak", SearchOption.AllDirectories))
					sources.Add(source);
				foreach (var source in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
					if (File.Exists(Path.Combine(source, ".metadata")) || File.Exists(Path.Combine(source, "_metadata")))
						sources.Add(source);
			}
			sources.Sort();
			foreach (var source in sources)
				if (source.EndsWith(".pak"))
				{
					try
					{
						AddPak(source);
					}
					catch (AssetException) // aex)
					{
						//throw new AssetException(string.Format("Error loading {0}: {1}", source.Replace(path, ""), aex.Message));
						//Okay so we couldn't load this pak for whatever reason. WHAT FUCKING EVER.
					}
				}
				else
					AddDirectory(source);
		}

		private static void AddDirectory(string path)
		{
			foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
			{
				var f = file.Replace(path, string.Empty).Replace(Path.DirectorySeparatorChar, '/');
				if (f == ".metadata" || f == "_metadata")
					continue;
				Files.Add(new Asset(file, f));
			}
		}

		private static void AddPak(string path)
		{
			var pakFile = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
			pakFile.BaseStream.Seek(0xC, SeekOrigin.Begin);
			var indexOffset = pakFile.ReadMotoInt32();
			pakFile.BaseStream.Seek(indexOffset, SeekOrigin.Begin);
			var indexHeader = new string(pakFile.ReadChars(5));
			if (indexHeader != "INDEX")
				throw new AssetException("Expected an index.");
			var indexItems = pakFile.ReadVLQUnsigned();
			var index = new Dictionary<string, object>();
			while (indexItems-- > 0)
			{
				var key = pakFile.ReadProperString();
				var type = pakFile.ReadByte();
				if (type == 1)
				{
					throw new AssetException("Found a null value in the pak's metadata. There should not be any of those.");
				}
				else if (type == 2)
				{
					var moto8 = pakFile.ReadBytes(8);
					var intel8 = new[] { moto8[7], moto8[6], moto8[5], moto8[4], moto8[3], moto8[2], moto8[1], moto8[0] };
					var val = 0.0;
					using (var intel = new BinaryReader(new MemoryStream(intel8)))
						val = intel.ReadDouble();
					index[key] = val;
				}
				else if (type == 3)
				{
					throw new AssetException("Found a boolean value in the pak's metadata. There should not be any of those.");
				}
				else if (type == 4)
				{
					var val = pakFile.ReadVLQSigned();
					index[key] = val;
				}
				else if (type == 5)
				{
					var val = pakFile.ReadProperString();
					index[key] = val;
				}
				else if (type == 6) //array (assume of string)
				{
					var arrayItems = (int)pakFile.ReadVLQUnsigned();
					var array = new List<object>();
					while (arrayItems-- > 0)
					{
						type = pakFile.ReadByte();
						if (type != 5)
							throw new AssetException("Got an array with something other than string values in the pak's metadata. The only arrays in a Starbound asset's metadata should be \"requires\" and \"includes\", which should only take strings.");
						var val = pakFile.ReadProperString();
						array.Add(val);
					}
					index[key] = array;
				}
				else if (type == 7)
				{
					//Technically objects are allowed, but it's SO MUCH EASIER to just enforce Proper Metadata.
					if (key == "metadata")
						throw new AssetException("Yo dawg! We heard you like metadata so we put a metadata in your metadata so you can fail while you fail!");
					else
						throw new AssetException("Found an object (or dictionary if you prefer) in the pak's metadata. There should not be any of those.");
				}
				else
				{
					throw new AssetException(string.Format("Unknown type {0} in the pak's metadata. This shouldn't happen at all unless your pak file is malformed.", type));
				}
			}
			var fileCount = pakFile.ReadVLQUnsigned();
			for (ulong i = 0; i < fileCount; i++)
			{
				var name = pakFile.ReadProperString();
				var offset = pakFile.ReadMotoInt64();
				var length = pakFile.ReadMotoInt64();
				Files.Add(new Asset(path, name, (int)offset, (int)length));
			}
		}

		public static Stream Open(string path)
		{
			var hit = Files.LastOrDefault(f => f.Name.Equals(path, StringComparison.InvariantCultureIgnoreCase));
			if (hit == null) return null;
			return hit.Open();
		}

		public static string GetString(string path)
		{
			path = path.ToLowerInvariant();
			using (var file = new StreamReader(Open(path)))
			{
				return file.ReadToEnd();
			}
		}

		public static string GetString(Asset asset)
		{
			using (var file = new StreamReader(asset.Open()))
			{
				return file.ReadToEnd();
			}
		}

		public static object GetJson(string path)
		{
			path = path.ToLowerInvariant();
			if (cache.ContainsKey(path))
				return cache[path];
			var ret = Json5.Parse(Assets.GetString(path));
			if (ret is Dictionary<string, object>)
			{
				var patchPath = path + ".patch";
				foreach (var file in Assets.Files.Where(f => f.Name == patchPath))
				{
					var rawPatch = Assets.GetString(file);
					var patch = Json5.Parse(rawPatch);
					Kawa.Json.Patch.JsonPatch.Apply(ret, patch);
				}
			}
			cache.Add(path, ret);
			return ret;
		}

		public static Bitmap GetImage(string path)
		{
			path = path.ToLowerInvariant();
			Bitmap ret = null;
			using (var stream = Assets.Open(path))
			{
				if (stream == null) return errorFallback;
				ret = new Bitmap(stream);
			}
			return ret;
		}

		public static Frames GetFrames(string path)
		{
			path = path.ToLowerInvariant();
			if (framesCache.ContainsKey(path))
				return framesCache[path];
			var cachePath = path;
			Asset hit = null;
			var originalBaseName = path.Substring(path.LastIndexOf('/') + 1);
			while (path.Length > 1 && hit == null)
			{
				//First, try the actually asked for path
				hit = Files.LastOrDefault(f => f.Name == path);
				if (hit == null)
				{
					//Didn't find it here. Try default.
					path = path.Substring(0, path.LastIndexOf('/')) + "/default.frames";
					hit = Files.LastOrDefault(f => f.Name == path);
					if (hit == null)
					{
						//Still couldn't find it? Cut out the last bit of path and try again with the original basename.
						path = path.Substring(0, path.LastIndexOf('/'));
						path = path.Substring(0, path.LastIndexOf('/')) + "/" + originalBaseName;
					}
				}
			}
			if (hit != null)
			{
				var ret = new Frames((JsonObj)GetJson(path));
				framesCache.Add(cachePath, ret);
				return ret;
			}
			return null;
		}
	}

	static class MotoVLQExtensions
	{
		public static UInt16 ReadMotoInt16(this BinaryReader stream)
		{
			var moto2 = stream.ReadBytes(2);
			var intel2 = new[] { moto2[1], moto2[0] };
			var ret = (UInt16)0;
			using (var intel = new BinaryReader(new MemoryStream(intel2)))
				ret = intel.ReadUInt16();
			return ret;
		}
		public static UInt32 ReadMotoInt32(this BinaryReader stream)
		{
			var moto4 = stream.ReadBytes(4);
			var intel4 = new[] { moto4[3], moto4[2], moto4[1], moto4[0] };
			var ret = (UInt32)0;
			using (var intel = new BinaryReader(new MemoryStream(intel4)))
				ret = intel.ReadUInt32();
			return ret;
		}
		public static UInt64 ReadMotoInt64(this BinaryReader stream)
		{
			var moto8 = stream.ReadBytes(8);
			var intel8 = new[] { moto8[7], moto8[6], moto8[5], moto8[4], moto8[3], moto8[2], moto8[1], moto8[0] };
			var ret = (UInt64)0;
			using (var intel = new BinaryReader(new MemoryStream(intel8)))
				ret = intel.ReadUInt64();
			return ret;
		}
		public static ulong ReadVLQUnsigned(this BinaryReader stream)
		{
			ulong x = 0;
			for (var i = 0; i < 10; ++i)
			{
				var oct = stream.ReadByte();
				x = (ulong)(x << 7) | (ulong)((ulong)oct & 127);
				if ((oct & 128) == 0)
					return x;
			}
			throw new Exception("fucked up");
		}
		public static long ReadVLQSigned(this BinaryReader stream)
		{
			ulong source = ReadVLQUnsigned(stream);
			bool negative = (source & 1) == 1;
			if (negative)
				return -(long)(source >> 1) - 1;
			else
				return (long)(source >> 1);
		}
		public static string ReadProperString(this BinaryReader stream)
		{
			var len = (int)stream.ReadVLQUnsigned();
			var bytes = stream.ReadBytes(len);
			using (var str = new BinaryReader(new MemoryStream(bytes)))
			{
				return new string(str.ReadChars((int)str.BaseStream.Length));
			}
		}
	}
}
