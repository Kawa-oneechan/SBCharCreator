using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kawa.Json;

namespace SBCharCreator
{
	static class NameGen
	{
		private static Random rand;
		private static System.Globalization.TextInfo ti = System.Globalization.CultureInfo.InvariantCulture.TextInfo;

		private static string TitleCase(string text, JsonObj part)
		{
			if (part.ContainsKey("titleCase") && (bool)part["titleCase"])
			{
				return ti.ToTitleCase(text.ToLowerInvariant());
			}
			return text;
		}

		private static string Markov(JsonObj settings)
		{
			var order = (int)((double)settings["prefixSize"]);
			var minLength = (int)((double)settings["endSize"]);
			var samples = ((List<object>)settings["sourceNames"]).Cast<string>().ToList();
			var chains = new Dictionary<string, List<char>>();

			var getLetter = new Func<string, char>(token =>
			{
				if (!chains.ContainsKey(token))
				{
					return '?';
				}
				List<char> letters = chains[token];
				int n = rand.Next(letters.Count);
				return letters[n];
			});

			foreach (string word in samples)
			{
				for (int letter = 0; letter < word.Length - order; letter++)
				{
					var token = word.Substring(letter, order);
					List<char> entry = null;
					if (chains.ContainsKey(token))
					{
						entry = chains[token];
					}
					else
					{
						entry = new List<char>();
						chains[token] = entry;
					}
					entry.Add(word[letter + order]);
				}
			}

			var ret = string.Empty;
			do
			{
				var n = rand.Next(samples.Count);
				int nameLength = samples[n].Length;
				ret = (samples[n].Substring(rand.Next(0, samples[n].Length - order), order));
				while (ret.Length < nameLength)
				{
					string token = ret.Substring(ret.Length - order, order);
					char c = getLetter(token);
					if (c != '?')
					{
						ret += getLetter(token);}
					else{
						break;}
				}

				if (ret.Contains(" "))
				{
					string[] tokens = ret.Split(' ');
					ret = "";
					for (int t = 0; t < tokens.Length; t++)
					{
						if (tokens[t] == "")
						{
							continue;
						}
						if (tokens[t].Length == 1)
						{
							tokens[t] = tokens[t].ToUpperInvariant();
						}
						else
						{
							tokens[t] = tokens[t].Substring(0, 1) + tokens[t].Substring(1).ToLowerInvariant();
						}
						if (ret != "")
						{
							ret += " ";
						}
						ret += tokens[t];
					}
				}
				else
				{
					ret = ret.Substring(0, 1) + ret.Substring(1).ToLowerInvariant();
				}
			}
			while (ret.Length < minLength);
			return ret;
		}

		private static string Parse(List<object> part)
		{
			if (part[0] is JsonObj)
			{
				var mode = (string)((JsonObj)part[0])["mode"];
				if (mode == "alts")
				{
					var alt = part[rand.Next(1, part.Count)];
					if (alt is string)
					{
						return TitleCase((string)alt, (JsonObj)part[0]);
					}
					else
					{
						return TitleCase(Parse((List<object>)alt), (JsonObj)part[0]);
					}
				}
				else if (mode == "serie")
				{
					var sb = new System.Text.StringBuilder();
					for (var i = 1; i < part.Count; i++)
					{
						if (part[i] is string)
						{
							sb.Append(part[i]);
						}
						else
						{
							sb.Append(Parse((List<object>)part[i]));
						}
					}
					return TitleCase(sb.ToString(), (JsonObj)part[0]);
				}
				else if (mode == "markov")
				{
					var name = Markov((JsonObj)Assets.GetJson("/names/" + (string)((JsonObj)part[0])["source"] + ".namesource"));
					return TitleCase(name, (JsonObj)part[0]);
				}
			}
			else if (part is List<object> || part[0] is string)
			{
				//Assume alts
				var alt = part[rand.Next(0, part.Count)];
				if (alt is string)
				{
					return (string)alt;
				}
				else
				{
					return Parse((List<object>)alt);
				}
			}
			throw new InvalidCastException("Expected a mode object.");
		}

		public static string Generate(JsonObj source, string key)
		{
			if (rand == null)
			{
				rand = new Random();
			}
			var part = source[key];
			if (part is List<object>)
			{
				return Parse((List<object>)part);
			}
			throw new InvalidCastException("Expected a list.");
		}
	}
}
