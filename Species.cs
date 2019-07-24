using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using Kawa.Json;

namespace SBCharCreator
{
	public class Species
	{
		public string Kind { get; private set; }
		public JsonObj CharCreationTooltip { get; private set; }
		public string[] StatusEffects { get; private set; }
		public JsonObj HumanoidOverrides { get; private set; }
		public string[] NameGen { get; private set; }
		public string[] OuchNoises { get; private set; }
		public string[] CharGenTextLabels { get; private set; }
		public string Skull { get; private set; }
		public string EffectDirectives { get; private set; }
		public JsonObj DefaultBluePrints { get; private set; }
		public bool AltColorAsFacialMaskSubColor { get; private set; }
		public bool AltOptionAsFacialMask { get; private set; }
		public bool AltOptionAsHairColor { get; private set; }
		public bool AltOptionAsUndyColor { get; private set; }
		public bool BodyColorAsFacialMaskSubColor { get; private set; }
		public bool HairColorAsBodySubColor { get; private set; }
		public bool HeadOptionAsFacialhair { get; private set; }
		public bool HeadOptionAsHairColor { get; private set; }
		public Gender[] Genders { get; private set; }
		public JsonObj[] BodyColor { get; private set; }
		public JsonObj[] UndyColor { get; private set; }
		public JsonObj[] HairColor { get; private set; }

		public string Name { get; private set; }
		public Dictionary<string, Bitmap> Parts { get; private set; }
		public Dictionary<string, Frames> PartFrames { get; private set; }

		private readonly JsonObj source;

		public Species(JsonObj source)
		{
			this.Kind = source.Path<string>("/kind");
			this.CharGenTextLabels = source.Path<string[]>("/charGenTextLabels");
			this.Name = this.CharGenTextLabels[8];

			var genders = source.Path<JsonObj[]>("/genders");
			this.Genders = new[] { new Gender(genders[0], this.Kind), new Gender(genders[1], this.Kind) };

			this.source = source;
		}

		public void Finish()
		{
			if (this.HairColor != null)
			{
				return;
			}

			charCreatorForm.SetStatus(string.Format("Loading {0}...", this.Kind));

			this.CharCreationTooltip = (JsonObj)source["charCreationTooltip"];
			this.StatusEffects = source.Path<string[]>("/statusEffects", new string[]{});
			this.HumanoidOverrides = source.Path<JsonObj>("/humanoidOverrides", null);
			this.NameGen = source.Path<string[]>("/nameGen");
			this.OuchNoises = source.Path<string[]>("/ouchNoises");
			this.Skull = source.Path<string>("/skull", string.Empty);
			this.EffectDirectives = source.Path<string>("/effectDirectives", string.Empty);
			this.DefaultBluePrints = (JsonObj)source["defaultBlueprints"];
			this.AltColorAsFacialMaskSubColor = source.Path<bool>("/altColorAsFacialMask", false);
			this.AltOptionAsFacialMask = source.Path<bool>("/altOptionAsFacialMask", false);
			this.AltOptionAsHairColor = source.Path<bool>("/altOptionAsHairColor", false);
			this.AltOptionAsUndyColor = source.Path<bool>("/altOptionAsUndyColor", false);
			this.BodyColorAsFacialMaskSubColor = source.Path<bool>("/bodyColorAsFacialMaskSubColor", false);
			this.HairColorAsBodySubColor = source.Path<bool>("/hairColorAsBodySubColor", false);
			this.HeadOptionAsFacialhair = source.Path<bool>("/headOptionAsFacialhair", false);
			this.HeadOptionAsHairColor = source.Path<bool>("/headOptionAsHairColor", false);

			foreach (var gender in this.Genders)
			{
				gender.Finish();
			}

			var humanoidFolder = "/humanoid/" + this.Kind + "/";
			this.Parts = new Dictionary<string, Bitmap>();
			this.PartFrames = new Dictionary<string, Frames>();
			foreach (var part in new[] { "malebody", "femalebody", "malehead", "femalehead", "backarm", "frontarm" })
			{
				this.Parts.Add(part, Assets.GetImage(humanoidFolder + part + ".png"));
				this.PartFrames.Add(part, Assets.GetFrames(humanoidFolder + part + ".frames"));
			}

			BodyColor = source.Path<JsonObj[]>("/bodyColor");
			var dummyColor = new[] { (JsonObj)Json5.Parse("{ \"ff00ff00\" : \"ff00ff00\" }") };
			var undyColor = source.Path<List<object>>("/undyColor");
			var hairColor = source.Path<List<object>>("/hairColor");
			UndyColor = (undyColor[0] is string) ? dummyColor : undyColor.Cast<JsonObj>().ToArray();
			HairColor = (hairColor[0] is string) ? dummyColor : hairColor.Cast<JsonObj>().ToArray();

			charCreatorForm.SetStatus(string.Empty);
		}

		public override string ToString()
		{
			return this.Name;
		}
	}

	public class Gender
	{
		public string Name { get; private set; }
		public Bitmap Image { get; private set; }
		public Bitmap CharacterImage { get; private set; }
		public string HairGroup { get; private set; }
		public Dictionary<string, Bitmap> Hair { get; private set; }
		public Dictionary<string, Frames> HairFrames { get; private set; }
		public string[] Shirt { get; private set; }
		public string[] Pants { get; private set; }
		public string FacialHairGroup { get; private set; }
		public Dictionary<string, Bitmap> FacialHair { get; private set; }
		public Dictionary<string, Frames> FacialHairFrames { get; private set; }
		public string FacialMaskGroup { get; private set; }
		public Dictionary<string, Bitmap> FacialMask { get; private set; }
		public Dictionary<string, Frames> FacialMaskFrames { get; private set; }
		
		private readonly JsonObj source;
		private readonly string species;

		public Gender(JsonObj source, string species)
		{
			this.Name = source.Path<string>("/name");
			this.Image = Assets.GetImage(source.Path<string>("/image"));
			this.CharacterImage = Assets.GetImage(source.Path<string>("/characterImage"));

			this.source = source;
			this.species = species;
		}

		public void Finish()
		{
			if (this.Hair != null)
			{
				return;
			}

			charCreatorForm.SetStatus(string.Format("Loading {0} {1}...", species, this.Name));

			this.HairGroup = source.Path<string>("/hairGroup", "hair");
			var hairPath = "/humanoid/" + species + "/" + this.HairGroup + "/";
			var hair = source.Path<List<string>>("/hair").Distinct();
			this.Hair = new Dictionary<string,Bitmap>();
			this.HairFrames = new Dictionary<string, Frames>();
			foreach (var h in hair)
			{
				if (string.IsNullOrWhiteSpace(h))
				{
					continue;
				}
				this.Hair.Add(h, Assets.GetImage(hairPath + h + ".png"));
				this.HairFrames.Add(h, Assets.GetFrames(hairPath + h + ".frames"));
			}

			this.Shirt = source.Path<List<string>>("/shirt").Distinct().ToArray();
			this.Pants = source.Path<List<string>>("/pants").Distinct().ToArray();

			this.FacialHairGroup = source.Path<string>("/facialHairGroup", string.Empty);
			hairPath = "/humanoid/" + species + "/" + this.FacialHairGroup + "/";
			hair = source.Path<List<string>>("/facialHair");
			this.FacialHair = new Dictionary<string, Bitmap>();
			this.FacialHairFrames = new Dictionary<string, Frames>();
			
			//Now, what may happen is that some species like the Sanglar have a non-empty facialHairGroup, but the folder doesn't exist!
			//We catch this by trying to find hair[0].
			if (hair.Count() > 0 && !Assets.Files.Any(asset => asset.Name == hairPath + hair.ElementAt(0) + ".png"))
			{
				//Pretend it's all empty.
				this.FacialHairGroup = string.Empty;
				hair = new List<string>();
			}

			foreach (var h in hair)
			{
				if (string.IsNullOrWhiteSpace(h))
				{
					continue;
				}
				this.FacialHair.Add(h, Assets.GetImage(hairPath + h + ".png"));
				this.FacialHairFrames.Add(h, Assets.GetFrames(hairPath + h + ".frames"));
			}

			this.FacialMaskGroup = source.Path<string>("/facialMaskGroup", string.Empty);
			hairPath = "/humanoid/" + species + "/" + this.FacialMaskGroup + "/";
			hair = source.Path<List<string>>("/facialMask");
			this.FacialMask = new Dictionary<string, Bitmap>();
			this.FacialMaskFrames = new Dictionary<string, Frames>();

			//As above.
			if (hair.Count() > 0 && !Assets.Files.Any(asset => asset.Name == hairPath + hair.ElementAt(0) + ".png"))
			{
				this.FacialMaskGroup = string.Empty;
				hair = new List<string>();
			}

			foreach (var h in hair)
			{
				if (string.IsNullOrWhiteSpace(h))
				{
					continue;
				}
				this.FacialMask.Add(h, Assets.GetImage(hairPath + h + ".png"));
				this.FacialMaskFrames.Add(h, Assets.GetFrames(hairPath + h + ".frames"));
			}

			charCreatorForm.SetStatus(string.Empty);
		}
	}
}
