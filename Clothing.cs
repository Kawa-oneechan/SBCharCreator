using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using Kawa.Json;

namespace SBCharCreator
{
	public class Clothing
	{
		public string ItemName { get; private set; }
		public string ShortDescription { get; private set; }
		public string Category { get; private set; }
		public JsonObj[] ColorOptions { get; private set; }
		public Bitmap Mask { get; private set; }

		public Dictionary<string, Bitmap> Parts { get; private set; }
		public Dictionary<string, Frames> PartFrames { get; private set; }

		private readonly JsonObj source;
		private readonly string path;

		private static JsonObj[] colorOptionsFallback = new[] { new JsonObj { { "000000", "000000" } } };

		public Clothing(JsonObj source, string path)
		{
			var origPath = path;
			var myPath = path;
			if (!myPath.EndsWith("/"))
			{
				myPath = myPath.Substring(0, myPath.LastIndexOf('/') + 1);
			}

			this.ItemName = source.Path<string>("/itemName");
			this.ShortDescription = source.Path<string>("/shortdescription");
			this.Category = source.Path<string>("/category", GuessCategory(origPath));

			this.source = source;
			this.path = myPath;
		}

		private static string GuessCategory(string path)
		{
			if (path.Contains("tier"))
			{
				if (path.EndsWith(".head")) { return "headarmour"; }
				if (path.EndsWith(".chest")) { return "chestarmour"; }
				if (path.EndsWith(".legs")) { return "legarmour"; }
			}
			else
			{
				if (path.EndsWith(".head")) { return "headwear"; }
				if (path.EndsWith(".chest")) { return "chestwear"; }
				if (path.EndsWith(".legs")) { return "legwear"; }
				if (path.EndsWith(".back")) { return "backwear"; }
			}
			if (path.EndsWith(".back")) { return "backwear"; }
			return string.Empty;
		}

		public void Finish()
		{
			if (this.ColorOptions != null)
			{
				return;
			}

			charCreatorForm.SetStatus(string.Format("Loading {0}...", this.ItemName));

			this.Parts = new Dictionary<string, Bitmap>();
			this.PartFrames = new Dictionary<string, Frames>();
			if (source["maleFrames"] is string)
			{
				foreach (var part in new[] { "maleFrames", "femaleFrames" })
				{
					this.Parts.Add(part, Assets.GetImage(path + (string)source[part]));
					this.PartFrames.Add(part, Assets.GetFrames(path + System.IO.Path.ChangeExtension((string)source[part], ".frames")));
				}
			}
			else
			{
				foreach (var gender in new[] { "male", "female" })
				{
					foreach (var part in new[] { "body", "backSleeve", "frontSleeve" })
					{
						this.Parts.Add(gender + part, Assets.GetImage(path + (string)((JsonObj)source[gender + "Frames"])[part]));
						this.PartFrames.Add(gender + part, Assets.GetFrames(path + System.IO.Path.ChangeExtension((string)((JsonObj)source[gender + "Frames"])[part], ".frames")));
					}
				}
			}
			if (source.ContainsKey("mask"))
			{
				this.Mask = Assets.GetImage(path + (string)source["mask"]);
			}

			if (source.ContainsKey("colorOptions"))
			{
				this.ColorOptions = source.Path<JsonObj[]>("/colorOptions");
			}
			else
			{
				this.ColorOptions = colorOptionsFallback;
			}

			charCreatorForm.SetStatus(string.Empty);
		}

		public override string ToString()
		{
			return this.ShortDescription;
		}
	}
}
