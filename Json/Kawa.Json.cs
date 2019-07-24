using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kawa.Json
{
	[Flags]
	public enum FoldMode
	{
		/// <summary>
		/// Don't fold anything.
		/// </summary>
		None = 0,
		/// <summary>
		/// Fold short arrays into a single line.
		/// </summary>
		Arrays = 1,
		/// <summary>
		/// Fold short objects into a single line.
		/// </summary>
		Objects = 2,
		/// <summary>
		/// Fold both short arrays and short objects.
		/// </summary>
		Both = 3,
	}

	public sealed class JsonException : Exception
	{
		public JsonException(string message)
			: base(message)
		{
		}
		public JsonException(string message, int line)
			: base(string.Format("{0} at line {1}.", message, line))
		{ }
	}

	public partial class JsonObj : Dictionary<string, object>
	{
	}

	public static partial class Json5
	{
		/// <summary>
		/// The JSON standard does not normally allow NaN or Infinity values. JSON5 does.
		/// </summary>
		public static bool AllowNaN { get; set; }
		/// <summary>
		/// The JSON standard does not allow trailing commas. JSON5 does.
		/// </summary>
		public static bool AllowTrailingComma { get; set; }
		/// <summary>
		/// The JSON standard does not allow unquoted keys. JSON5 does, if the keys are valid identifiers.
		/// </summary>
		public static bool AllowBareKeys { get; set; }
		/// <summary>
		/// How to handle short arrays and objects.
		/// </summary>
		public static FoldMode FoldMode { get; set; }

		/// <summary>
		/// Parses an input string into an object. The input can be any well-formed JSON or JSON5.
		/// </summary>
		/// <param name="text">The string to parse.</param>
		/// <returns>A JsonObj, list, string, double...</returns>
		public static object Parse(string text)
		{
			var previousCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
			System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

			var at = 0; //The index of the current character
			var ch = ' '; //The current character
			var escapee = new Dictionary<char, string>
			{
				{ '\'', "\'" },
				{ '\"', "\"" },
				{ '\\', "\\" },
				{ '/', "/" },
				{ '\n', string.Empty }, //Replace escaped newlines in strings w/ empty string
				{ 'b', "\b" },
				{ 'f', "\f" },
				{ 'n', "\n" },
				{ 'r', "\r" },
				{ 't', "\t" },
			};
			Func<int> locateError = () =>
			{
				var line = 0;
				for (var i = 0; i < at; i++)
				{
					if (text[i] == '\r')
					{
						line++;
					}
				}
				return line;
			};
			Func<char> next = () =>
			{
				if (at >= text.Length)
				{
					ch = '\0';
				}
				else
				{
					ch = text[at];
					at++;
				}
				return ch;
			};
			Func<char, char> expect = (c) =>
			{
				if (c != ch)
				{
					throw new JsonException(string.Format("Expected '{1}' instead of '{0}'", ch, c), locateError());
				}
				return next();
			};
			#region identifier
			Func<string> identifier = () =>
			{
				var key = new StringBuilder();
				key.Append(ch);
				//Identifiers must start with a letter, _ or $.
				if ((ch != '_' && ch != '$') &&
					(ch < 'a' || ch > 'z') &&
					(ch < 'A' || ch > 'Z'))
				{
					throw new JsonException("Bad identifier", locateError());
				}
				//Subsequent characters can contain digits.
				while (next() != '\0' && (
					ch == '_' || ch == '$' ||
					(ch >= 'a' && ch <= 'z') ||
					(ch >= 'A' && ch <= 'Z') ||
					(ch >= '0' && ch <= '9')))
				{
					key.Append(ch);
				}
				return key.ToString();
			};
			#endregion
			#region number
			Func<object> number = () =>
			{
				var sign = '\0';
				var str = new StringBuilder();
				var bas = 10;
				if (ch == '-' || ch == '+')
				{
					sign = ch;
					str.Append(ch);
					expect(ch);
				}
				if (ch == 'I')
				{
					expect('I');
					expect('n');
					expect('f');
					expect('i');
					expect('n');
					expect('i');
					expect('t');
					expect('y');
					if (!AllowNaN)
					{
						throw new JsonException("Found an unallowed Infinity value.");
					}
					return (sign == '-') ? double.NegativeInfinity : double.PositiveInfinity;
				}
				if (ch == '0')
				{
					str.Append(ch);
					next();
					if (ch == 'x' || ch == 'X')
					{
						str.Append(ch);
						next();
						bas = 16;
					}
					else if (ch >= '0' && ch <= '9')
					{
						throw new JsonException("Octal literal", locateError());
					}
				}

				//https://github.com/aseemk/json5/issues/36
				if (bas == 16 && sign != '\0')
				{
					throw new JsonException("Signed hexadecimal literal", locateError());
				}

				switch (bas)
				{
					case 10:
						while (ch >= '0' && ch <= '9')
						{
							str.Append(ch);
							next();
						}
						if (ch == '.')
						{
							if (str.Length == 0)
							{
								str.Append('0');
							}
							str.Append(ch);
							while (next() != '\0' && ch >= '0' && ch <= '9')
							{
								str.Append(ch);
							}
						}
						if (ch == 'e' || ch == 'E')
						{
							str.Append(ch);
							next();
							if (ch == '-' || ch == '+')
							{
								str.Append(ch);
								next();
							}
							while (ch >= '0' && ch <= '9')
							{
								str.Append(ch);
								next();
							}
						}
						break;
					case 16:
						while (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f')
						{
							str.Append(ch);
							next();
						}
						break;
					default:
						throw new JsonException("Invalid number base, somehow.");
				}
				if (bas == 16)
				{
					return int.Parse(str.ToString().Substring(2), System.Globalization.NumberStyles.HexNumber);
				}
				try
				{
					return Double.Parse(str.ToString());
				}
				catch (OverflowException)
				{
					return (sign == '-') ? Double.MinValue : Double.MaxValue;
				}
			};
			#endregion
			#region string
			Func<string> @string = () =>
			{
				var hex = 0;
				var i = 0;
				var str = new StringBuilder();
				char delim;
				var uffff = 0;
				//When parsing for string values, we must look for ' or " and \ characters.
				if (ch == '"' || ch == '\'')
				{
					delim = ch;
					while (next() != '\0')
					{
						if (ch == delim)
						{
							next();
							return str.ToString();
						}
						else if (ch == '\\')
						{
							next();
							if (ch == '\r' || ch == '\n')
							{
								str.Append('\n');
								next();
								continue;
							}
							if (ch == 'u')
							{
								uffff = 0;
								for (i = 0; i < 4; i += 1)
								{
									hex = int.Parse(next().ToString(), System.Globalization.NumberStyles.HexNumber);
									uffff = uffff * 16 + hex;
								}
								str.Append((char)uffff);
							}
							else if (escapee.ContainsKey(ch))
							{
								str.Append(escapee[ch]);
							}
							else
							{
								break;
							}
						}
						else
						{
							str.Append(ch);
						}
					}
				}
				throw new JsonException("Bad string", locateError());
			};
			#endregion
			#region inlineComment
			Func<string> inlineComment = () =>
			{
				//Skip an inline comment, assuming this is one. The current character should
				//be the second / character in the // pair that begins this inline comment.
				//To finish the inline comment, we look for a newline or the end of the text.
				if (ch != '/')
				{
					throw new JsonException("Not an inline comment");
				}
				do
				{
					next();
					if (ch == '\n')
					{
						expect('\n');
						return string.Empty;
					}
				} while (ch != '\0');
				return string.Empty;
			};
			#endregion
			#region blockComment
			Func<string> blockComment = () =>
			{
				//Skip a block comment, assuming this is one. The current character should be
				//the * character in the /* pair that begins this block comment.
				//To finish the block comment, we look for an ending */ pair of characters,
				//but we also watch for the end of text before the comment is terminated.
				if (ch != '*')
				{
					throw new JsonException("Not a block comment");
				}
				do
				{
					next();
					while (ch == '*')
					{
						expect('*');
						if (ch == '/')
						{
							expect('/');
							return string.Empty;
						}
					}
				} while (ch != '\0');
				throw new JsonException("Unterminated block comment");
			};
			#endregion
			#region comment
			Func<string> comment = () =>
			{
				if (ch != '/')
				{
					throw new JsonException("Not a comment", locateError());
				}
				expect('/');
				if (ch == '/')
				{
					inlineComment();
				}
				else if (ch == '*')
				{
					blockComment();
				}
				else
				{
					throw new JsonException("Unrecognized comment", locateError());
				}
				return string.Empty;
			};
			#endregion
			#region white
			Func<string> white = () =>
			{
				//Skip whitespace and comments.
				//Note that we're detecting comments by only a single / character.
				//This works since regular expressions are not valid JSON(5), but this will
				//break if there are other valid values that begin with a / character!
				while (ch != '\0')
				{
					if (ch == '/')
					{
						comment();
					}
					else if (ch <= ' ')
					{
						next();
					}
					else
					{
						return string.Empty;
					}
				}
				return string.Empty;
			};
			#endregion
			#region word
			Func<bool?> word = () =>
			{
				//true, false, or null.
				switch (ch)
				{
					case 't':
						expect('t');
						expect('r');
						expect('u');
						expect('e');
						return true;
					case 'f':
						expect('f');
						expect('a');
						expect('l');
						expect('s');
						expect('e');
						return false;
					case 'n':
						expect('n');
						expect('u');
						expect('l');
						expect('l');
						return null;
					default:
						throw new JsonException(string.Format("Unexpected '{0}'", ch), locateError());
				}
			};
			#endregion
			Func<object> value = null; //Place holder for the value function.
			#region array
			Func<List<object>> @array = () =>
			{
				var justHadComma = false;
				var arr = new List<object>();
				if (ch == '[')
				{
					expect('[');
					white();
					while (ch != '\0')
					{
						if (ch == ']')
						{
							if (!AllowTrailingComma && justHadComma)
							{
								throw new JsonException("Superfluous trailing comma", locateError());
							}
							expect(ch);
							return arr; //.ToArray(); //Potentially empty array
						}
						//ES5 allows omitting elements in arrays, e.g. [,] and
						//[,null]. We don't allow this in JSON5.
						if (ch == ',')
						{
							throw new JsonException("Missing array element", locateError());
						}
						else
						{
							arr.Add(value());
						}
						white();
						//If there's no comma after this value, this needs to
						//be the end of the array.
						if (ch != ',')
						{
							expect(']');
							return arr;
						}
						expect(',');
						justHadComma = true;
						white();
					}
				}
				throw new JsonException("Bad array", locateError());
			};
			#endregion
			#region object
			Func<JsonObj> @object = () =>
			{
				//Parse an object value.
				var key = string.Empty;
				var obj = new JsonObj();
				if (ch == '{')
				{
					expect('{');
					white();
					while (ch != '\0')
					{
						if (ch == '}')
						{
							expect('}');
							return obj; //Potentially empty object
						}
						//Keys can be unquoted. If they are, they need to be
						//valid JS identifiers.
						if (ch == '\"' || ch == '\'')
						{
							key = @string();
						}
						else
						{
							key = identifier();
						}
						white();
						expect(':');
						if (obj.ContainsKey(key))
						{
							throw new JsonException(string.Format("Duplicate key \"{0}\"", key), locateError());
						}
						obj[key] = value();
						white();
						//If there's no comma after this pair, this needs to be
						//the end of the object.
						if (ch != ',')
						{
							expect('}');
							return obj;
						}
						expect(',');
						white();
					}
				}
				throw new JsonException("Bad object", locateError());
			};
			#endregion
			#region value
			value = () =>
			{
				//Parse a JSON value. It could be an object, an array, a string, a number,
				//or a word.
				white();
				switch (ch)
				{
					case '{':
						return @object();
					case '[':
						return @array();
					case '\"':
					case '\'':
						return @string();
					case '-':
					case '+':
					case '.':
						return number();
					case 'N':
						expect('N');
						expect('a');
						expect('N');
						if (!AllowNaN)
						{
							throw new JsonException("Found an unallowed NaN value.");
						}
						return double.NaN;
					case 'I':
						expect('I');
						expect('n');
						expect('f');
						expect('i');
						expect('n');
						expect('i');
						expect('t');
						expect('y');
						if (!AllowNaN)
						{
							throw new JsonException("Found an unallowed Infinity value.");
						}
						return double.PositiveInfinity;
					default:
						return ch >= '0' && ch <= '9' ? @number() : word();
				}
			};
			#endregion

			//wow.
			//much cheat.
			//so unexpected.
			//-- KAWA
			white();
			object ret = null;
			if (ch == '\0')
			{
				//Ret is null and stays null.
			}
			else if (ch == '[')
			{
				ret = @array();
			}
			else if (ch == '{')
			{
				ret = @object();
			}
			else
			{
				ret = value();
			}

			System.Threading.Thread.CurrentThread.CurrentCulture = previousCulture;
			return ret;
		}

		/// <summary>
		/// Creates a string representation of a JsonObj, list, string, or double that conforms to the JSON or JSON5 standards.
		/// </summary>
		/// <param name="object">The object to stringify.</param>
		/// <param name="space">How many spaces to indent each level.</param>
		/// <returns>A string representation of the input object.</returns>
		public static string Stringify(this object @object, int space)
		{
			var previousCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
			System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

			Func<char, bool> isWordChar = (c) =>
			{
				return (c >= 'a' && c <= 'z') ||
						(c >= 'A' && c <= 'Z') ||
						(c >= '0' && c <= '9') ||
						c == '_' || c == '$';
			};
			Func<char, bool> isWordStart = (c) =>
			{
				return (c >= 'a' && c <= 'z') ||
						(c >= 'A' && c <= 'Z') ||
						c == '_' || c == '$';
			};
			Func<object, bool> isWord = (key) =>
			{
				if (!AllowBareKeys)
				{
					return false;
				}
				if (!(key is string))
				{
					return false;
				}
				var k = key as string;
				if (!isWordStart(k[0]))
				{
					return false;
				}
				var i = 1;
				while (i < k.Length)
				{
					if (!isWordChar(k[i]))
					{
						return false;
					}
					i++;
				}
				return true;
			};

			var objStack = new Stack<object>();

			Action<object> checkForCircular = (obj) =>
			{
				if (objStack.Contains(obj))
				{
					throw new JsonException("Converting circular structure to JSON");
				}
			};

			Func<string, int, bool, string> makeIndent = (str, num, noNewLine) =>
			{
				if (string.IsNullOrEmpty(str))
				{
					return string.Empty;
				}
				var myStr = str;
				// indentation no more than 10 chars
				if (str.Length > 10)
				{
					myStr = str.Substring(0, 10);
				}

				var indent = new StringBuilder(noNewLine ? string.Empty : "\n");
				for (var i = 0; i < num; i++)
				{
					indent.Append(myStr);
				}

				return indent.ToString();
			};

			var indentStr = space == 0 ? string.Empty : makeIndent(" ", space, true);

			Func<string, string> escapeString = (str) =>
			{
				return '\"' + str + '\"';
			};

			Func<object, string> internalStringify = null;

			internalStringify = (obj_part) =>
			{
				var buffer = new StringBuilder();
				var singleBuffer = new StringBuilder();
				var res = string.Empty;
				var o = obj_part;
				if (o == null)
				{
					return "null";
				}
				if (o is bool)
				{
					return o.ToString().ToLowerInvariant();
				}
				if (o is double || o is float)
				{
					if (!AllowNaN && (double.IsNaN((double)o) || double.IsInfinity((double)o)))
					{
						throw new JsonException("Found an unallowed NaN or Infinity value.");
					}
					else
					{
						return o.ToString();
					}
				}
				if (o is int || o is long)
				{
					return o.ToString();
				}
				if (o is int[])
				{
					return internalStringify(((int[])o).Cast<object>().ToList());
				}
				if (o is long[])
				{
					return internalStringify(((long[])o).Cast<object>().ToList());
				}
				if (o is double[])
				{
					return internalStringify(((double[])o).Cast<object>().ToList());
				}
				if (o is float[])
				{
					return internalStringify(((float[])o).Cast<object>().ToList());
				}
				if (o is string[])
				{
					return internalStringify(((string[])o).Cast<object>().ToList());
				}
				if (o is List<int>)
				{
					return internalStringify(((List<int>)o).Cast<object>().ToList());
				}
				if (o is List<long>)
				{
					return internalStringify(((List<long>)o).Cast<object>().ToList());
				}
				if (o is List<double>)
				{
					return internalStringify(((List<double>)o).Cast<object>().ToList());
				}
				if (o is List<float>)
				{
					return internalStringify(((List<float>)o).Cast<object>().ToList());
				}
				if (o is List<string>)
				{
					return internalStringify(((List<string>)o).Cast<object>().ToList());
				}
				if (o is string)
				{
					return escapeString(o.ToString());
				}
				if (o is object)
				{
					if (o is object[])
					{
						o = ((object[])o).ToList();
					}
					if (o == null)
					{
						return "null";
					}
					else if (o is List<object>)
					{
						checkForCircular(o);
						var objPartAsArray = o as List<object>;
						if (objPartAsArray.Count == 0)
						{
							return "[]";
						}
						buffer.Append('[');
						singleBuffer.Append('[');
						objStack.Push(o);
						for (var i = 0; i < objPartAsArray.Count; i++)
						{
							res = internalStringify(objPartAsArray[i]);
							buffer.Append(makeIndent(indentStr, objStack.Count, false));
							singleBuffer.Append(' ');
							if (res == null)
							{
								buffer.Append("null");
								singleBuffer.Append("null");
							}
							else
							{
								buffer.Append(res);
								singleBuffer.Append(res);
							}
							if (i < objPartAsArray.Count - 1)
							{
								buffer.Append(',');
								singleBuffer.Append(',');
							}
							else if (string.IsNullOrEmpty(indentStr))
							{
								buffer.Append('\n');
							}
						}
						objStack.Pop();
						buffer.Append(makeIndent(indentStr, objStack.Count, false));
						buffer.Append(']');
						singleBuffer.Append(" ]");
						if (FoldMode.HasFlag(FoldMode.Arrays) && singleBuffer.Length < 70)
						{
							return singleBuffer.ToString();
						}
					}
					else if (o is JsonObj)
					{
						checkForCircular(o);
						buffer.Append('{');
						singleBuffer.Append('{');
						objStack.Push(o);
						var nonEmpty = false;
						var objPartAsDict = o as JsonObj;
						foreach (var pair in objPartAsDict)
						{
							var val = internalStringify(pair.Value);
							if (val != null)
							{
								buffer.Append(makeIndent(indentStr, objStack.Count, false));
								singleBuffer.Append(' ');
								nonEmpty = true;
								var key = isWord(pair.Key) ? pair.Key : escapeString(pair.Key);
								buffer.AppendFormat("{0} : {1},", key, val);
								singleBuffer.AppendFormat("{0} : {1},", key, val);
							}
						}
						objStack.Pop();
						if (nonEmpty)
						{
							if (FoldMode.HasFlag(FoldMode.Objects) && singleBuffer.Length < 70)
							{
								return singleBuffer.ToString().Substring(0, singleBuffer.Length - 1) + " }";
							}
							return buffer.ToString().Substring(0, buffer.Length - 1) + makeIndent(indentStr, objStack.Count, false) + '}';
						}
						return "{}";
					}
					return buffer.ToString();
				}
				else
				{
					return null;
				}
			};

			var ret = internalStringify(@object);
			System.Threading.Thread.CurrentThread.CurrentCulture = previousCulture;
			return ret;
		}

		/// <summary>
		/// Creates a string representation of a JsonObj, list, string, or double that conforms to the JSON or JSON5 standards.
		/// </summary>
		/// <param name="object">The object to stringify.</param>
		/// <returns>A string representation of the input object.</returns>
		public static string Stringify(this object @object)
		{
			return Stringify(@object, 2);
		}
	}
}
