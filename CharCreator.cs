using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using Kawa.Json;

namespace SBCharCreator
{
	public partial class charCreatorForm : Form
	{
		private Dictionary<string, Clothing> clothing;
		private readonly List<Species> species;
		private bool drawLock;
		private readonly System.ComponentModel.BackgroundWorker loader;
		private bool stopLoadingDipshit;
		public static string SavePath { get; private set; }

		private JsonObj editPlayer;

		private static ToolStripStatusLabel statusLabel;
		public static void SetStatus(string text)
		{
			statusLabel.Text = text;
			statusLabel.GetCurrentParent().Refresh();
		}

		public charCreatorForm()
		{
			this.Icon = global::SBCharCreator.Properties.Resources.app;
			this.DoubleBuffered = true;
			InitializeComponent();
			toolStrip1.Visible = false;

			statusLabel = toolStripStatusLabel1;
			SetStatus(string.Empty);
			loadLabel.Dock = DockStyle.Fill;

			this.Show();
			this.Refresh();

			var sources = System.IO.File.ReadAllLines("sources.txt").Where(s => !(string.IsNullOrEmpty(s) || s.StartsWith(";"))).ToArray();
			if (sources.Length == 0)
			{
				MessageBox.Show("Nothing usable in sources.txt.", Application.ProductName);
				Application.Exit();
				return;
			}
			SavePath = sources[0];
			if (!Directory.Exists(SavePath))
			{
				MessageBox.Show("The path specified in sources.txt doesn't exist.", Application.ProductName);
				Application.Exit();
				return;
			}

			species = new List<Species>();

			var fuckedUp = false;
			loader = new System.ComponentModel.BackgroundWorker();
			loader.WorkerReportsProgress = true;

			var loadingFrames = global::SBCharCreator.Properties.Resources.loadinglogo;
			var loadingFrame = new Bitmap(41, 30);
			var loadingIndex = 0;
			var loadingGraphics = Graphics.FromImage(loadingFrame);
			var loadingTimer = new Timer();
			loadingTimer.Interval = 50;
			loadingTimer.Tick += (s, e) =>
			{
				loadingGraphics.Clear(Color.Transparent);
				loadingGraphics.DrawImage(loadingFrames, new Rectangle(0,0, 41, 30), 41 * loadingIndex, 0, 41, 30, GraphicsUnit.Pixel);
				loadLabel.Image = loadingFrame;
				loadingIndex++;
				if (loadingIndex == 30)
				{
					loadingIndex = 0;
				}
				loadLabel.Refresh();
			};
			loadingTimer.Start();

			loader.ProgressChanged += (snd, e) => {
				statusLabel.Text = string.Format("Loading {0}...", (string)e.UserState);
			};
			loader.DoWork += (snd, e) => {
				try
				{
					foreach (var source in sources.Skip(1))
					{
						loader.ReportProgress(0, source);
						Assets.AddSource(source);
					}
				}
				catch (AssetException aex)
				{
					MessageBox.Show(aex.Message, Application.ProductName);
					fuckedUp = true;
					return;
				}

				if (stopLoadingDipshit)
				{
					return;
				}

				Humanoid.Load((JsonObj)Assets.GetJson("/humanoid.config"));
				var charCreator = (JsonObj)Assets.GetJson("/interface/windowconfig/charcreation.config");
				var speciesOrdering = ((List<object>)charCreator["speciesOrdering"]).Cast<string>().Distinct();
				var clothingFiles = Assets.Files.Where(f => f.Name.EndsWith(".chest") || f.Name.EndsWith(".legs") /* || f.Name.EndsWith(".head") */).ToList();
				var totalShit = species.Count + clothingFiles.Count;
				var i = 0;

#if BLACKLIST
				var blacklist = new[] { "Locus Les Loupes", "Slimesapien", "CHANGELING" };
				var blacklisted = new List<string>();
#endif

				foreach (var s in speciesOrdering)
				{
					if (stopLoadingDipshit)
					{
						return;
					}
					loader.ReportProgress((int)(((float)i / (float)totalShit) * 100), s);
					i++;
					var newSpecies = new Species((JsonObj)Assets.GetJson("/species/" + s + ".species"));
#if BLACKLIST
					if (blacklist.Contains(newSpecies.Name))
					{
						blacklisted.Add(newSpecies.Name);
						continue;
					}
#endif
					speciesListbox.Items.Add(newSpecies);
					species.Add(newSpecies);
				}
#if BLACKLIST
				if (blacklisted.Count > 0)
				{
					var message = new StringBuilder();
					message.Append("The species ");
					if (blacklisted.Count == 1)
						message.AppendFormat("\"{0}\"", blacklisted[0]);
					else if (blacklisted.Count == 2)
						message.AppendFormat("\"{0}\" and \"{1}\"", blacklisted[0], blacklisted[1]);
					else
					{
						for (var b = 0; b < blacklisted.Count; b++)
						{
							message.AppendFormat("\"{0}\"", blacklisted[b]);
							if (b < blacklisted.Count - 1)
							{
								if (b < blacklisted.Count - 2)
									message.Append(", ");
								else
									message.Append(", and ");
							}
						}
					}
					if (blacklisted.Count == 1)
						message.Append(" is");
					else
						message.Append(" are");
					message.Append(" known to have issues that can't be worked around and won't be loaded.");
					MessageBox.Show(message.ToString(), Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
#endif
				clothing = new Dictionary<string, Clothing>();
				clothing.Add("emptychest", null);
				clothing.Add("emptylegs", null);
				foreach (var file in clothingFiles)
				{
					if (stopLoadingDipshit)
					{
						return;
					}
					loader.ReportProgress((int)(((float)i / (float)totalShit) * 100), Path.GetFileName(file.Name));
					i++;
					var newClothing = new Clothing((JsonObj)Assets.GetJson(file.Name), file.Name);
					if (clothing.ContainsKey(newClothing.ItemName))
					{
						continue;
					}
					clothing.Add(newClothing.ItemName, newClothing);
				}
			};
			loader.RunWorkerAsync();
			this.FormClosing += (s, e) =>
			{
				if (loader.IsBusy && !stopLoadingDipshit)
				{
					loadLabel.Text = "Woah!";
					stopLoadingDipshit = true;
					e.Cancel = true;
				}
			};

			while (loader.IsBusy)
			{
				Application.DoEvents();
			}
			if (fuckedUp || stopLoadingDipshit)
			{
				this.Close();
				return;
			}

			drawLock = true;
			Refresh();
			modeComboBox.SelectedIndex = 0;
			personalityTrackBar.Maximum = Humanoid.Personalities.Length - 1;
			var r = new Random();
			speciesListbox.SelectedIndex = r.Next(speciesListbox.Items.Count);
			if (r.NextDouble() > 0.5)
			{
				femaleGenderRadioButton.Checked = true;
			}
			randomizeButton_Click(null, null);
			loadLabel.Hide();
			identityPanel.Visible = true;
			characterPictureBox.Visible = true;
			speciesListbox.Visible = true;
			genderPanel.Visible = true;
			controlPanel.Visible = true;
			toolStrip1.Visible = true;

			loader = new System.ComponentModel.BackgroundWorker();
			loader.DoWork += (snd, e) =>
			{
				object o = species.FirstOrDefault(s => s.HairColor == null);
				while (o != null)
				{
					((Species)o).Finish();
					o = species.FirstOrDefault(s => s.HairColor == null);
				}
				o = clothing.FirstOrDefault(c => c.Value.ColorOptions == null);
				while (o != null)
				{
					((Clothing)o).Finish();
					o = clothing.FirstOrDefault(c => c.Value.ColorOptions == null);
				}
			};
			this.Enabled = true;
		}

		private void speciesListbox_DrawItem(object sender, DrawItemEventArgs e)
		{
			var theSpecies = speciesListbox.Items[e.Index] as Species;
			var g = e.Graphics;
			e.DrawBackground();
			g.DrawImage(theSpecies.Genders[1].CharacterImage, e.Bounds.Left + 14, e.Bounds.Top, 25, 24);
			g.DrawImage(theSpecies.Genders[0].CharacterImage, e.Bounds.Left, e.Bounds.Top, 25, 24);
			g.DrawString(theSpecies.Name, speciesListbox.Font, e.State == DrawItemState.Selected ? SystemBrushes.HighlightText : (theSpecies.Parts == null ? SystemBrushes.GrayText : SystemBrushes.ControlText), e.Bounds.Left + 40, e.Bounds.Top + 8);
			e.DrawFocusRectangle();
		}

		private void speciesListbox_SelectedIndexChanged(object sender, EventArgs e)
		{
			drawLock = true;
			UseWaitCursor = true;
			Cursor = Cursors.WaitCursor;
			identityPanel.Hide();

			var oldBodyColor = bodyColorComboBox.SelectedIndex;
			var oldUndyColor = undyColorComboBox.SelectedIndex;
			var oldHairColor = hairColorComboBox.SelectedIndex;

			var species = (Species)speciesListbox.SelectedItem;
			species.Finish();
			speciesListbox.Refresh();			

			var labels = new[] { bodyLabel, hairLabel, chestLabel, legsLabel, undyLabel, headLabel, chestColorLabel, legsColorLabel };
			for (var i = 0; i < 8; i++)
			{
				labels[i].Text = species.CharGenTextLabels[i];
			}
			personalityLabel.Text = species.CharGenTextLabels[9];

			var colorCombos = new[] { bodyColorComboBox, undyColorComboBox, hairColorComboBox };
			for (var i = 0; i < colorCombos.Length; i++)
			{
				colorCombos[i].Items.Clear();
			}

			if (oldBodyColor < 0)
			{
				oldBodyColor = 0;
			}
			bodyColorComboBox.Tag = species.BodyColor;
			bodyColorComboBox.Items.AddRange(species.BodyColor);
			if (oldBodyColor < bodyColorComboBox.Items.Count)
			{
				bodyColorComboBox.SelectedIndex = oldBodyColor;
			}
			else
			{
				bodyColorComboBox.SelectedIndex = 0;
			}

			if (oldUndyColor < 0)
			{
				oldUndyColor = 0;
			}
			undyColorComboBox.Tag = species.UndyColor;
			undyColorComboBox.Items.AddRange(species.UndyColor);
			if (oldUndyColor < undyColorComboBox.Items.Count)
			{
				undyColorComboBox.SelectedIndex = oldUndyColor;
			}
			else
			{
				undyColorComboBox.SelectedIndex = 0;
			}
			undyColorComboBox.Visible = species.AltOptionAsUndyColor && !string.IsNullOrEmpty(species.CharGenTextLabels[4]);
			undyOptionComboBox.Visible = !species.AltOptionAsUndyColor && !string.IsNullOrEmpty(species.CharGenTextLabels[4]);
			undyCheckBox.Visible = undyColorComboBox.Visible || undyOptionComboBox.Visible;

			if (oldHairColor < 0)
			{
				oldHairColor = 0;
			}
			hairColorComboBox.Tag = species.HairColor;
			hairColorComboBox.Items.AddRange(species.HairColor);
			if (oldHairColor < hairColorComboBox.Items.Count)
			{
				hairColorComboBox.SelectedIndex = oldHairColor;
			}
			else
			{
				hairColorComboBox.SelectedIndex = 0;
			}
			hairColorComboBox.Visible = !species.HeadOptionAsFacialhair && !string.IsNullOrEmpty(species.CharGenTextLabels[5]);

			headOptionComboBox.Visible = species.HeadOptionAsFacialhair && !string.IsNullOrEmpty(species.CharGenTextLabels[5]);

			maleGenderRadioButton.Image = species.Genders[0].Image;
			femaleGenderRadioButton.Image = species.Genders[1].Image;

			SelectGender();
			UseWaitCursor = false;
			Cursor = Cursors.Default;
			identityPanel.Show();
			if (!this.Enabled)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(nameTextBox.Text))
			{
				randomNameButton_Click(null, null);
			}

			RenderCharacter();
		}

		private void SelectGender()
		{
			identityPanel.Hide();

			var oldHairOption = hairOptionComboBox.SelectedIndex;
			var oldUndyOption = undyOptionComboBox.SelectedIndex;
			var oldHeadOption = headOptionComboBox.SelectedIndex;
			var oldChest = chestComboBox.SelectedItem;
			var oldLegs = legsComboBox.SelectedItem;
			var oldChestColor = chestColorComboBox.SelectedIndex;
			var oldLegsColor = legsColorComboBox.SelectedIndex;
			if (oldChestColor < 0)
			{
				oldChestColor = 0;
			}
			if (oldLegsColor < 0)
			{
				oldLegsColor = 0;
			}

			var species = (Species)speciesListbox.SelectedItem;
			var gender = species.Genders[maleGenderRadioButton.Checked ? 0 : 1];

			if (oldHairOption < 0)
			{
				oldHairOption = 0;
			}
			hairOptionComboBox.Items.Clear();
			hairOptionComboBox.Items.AddRange(gender.Hair.Keys.ToArray());
			if (oldHairOption < hairOptionComboBox.Items.Count)
			{
				hairOptionComboBox.SelectedIndex = oldHairOption;
			}
			else
			{
				hairOptionComboBox.SelectedIndex = 0;
			}

			if (species.AltOptionAsFacialMask)
			{
				if (oldUndyOption < 0)
				{
					oldUndyOption = 0;
				}
				undyOptionComboBox.Items.Clear();
				undyOptionComboBox.Items.AddRange(gender.FacialMask.Keys.ToArray());
				if (oldUndyOption < undyOptionComboBox.Items.Count)
				{
					undyOptionComboBox.SelectedIndex = oldUndyOption;
				}
				else
				{
					undyOptionComboBox.SelectedIndex = 0;
				}
			}

			if (species.HeadOptionAsFacialhair)
			{
				if (oldHeadOption < 0)
				{
					oldHeadOption = 0;
				}
				headOptionComboBox.Items.Clear();
				headOptionComboBox.Items.AddRange(gender.FacialHair.Keys.ToArray());
				if (oldHeadOption < headOptionComboBox.Items.Count)
				{
					headOptionComboBox.SelectedIndex = oldHeadOption;
				}
				else
				{
					headOptionComboBox.SelectedIndex = 0;
				}
			}

			chestComboBox.Items.Clear();
			legsComboBox.Items.Clear();
			if (allowAllClothingCheckBox.Checked)
			{
				chestComboBox.Items.AddRange(clothing.Where(c => c.Value != null && c.Value.Category.StartsWith("chest")).Select(c => c.Key).Distinct().Where(c => clothing.ContainsKey(c)).ToArray());
				legsComboBox.Items.AddRange(clothing.Where(c => c.Value != null && c.Value.Category.StartsWith("leg")).Select(c => c.Key).Distinct().Where(c => clothing.ContainsKey(c)).ToArray());
			}
			else
			{
				chestComboBox.Items.AddRange(gender.Shirt.Where(c => clothing.ContainsKey(c)).ToArray());
				legsComboBox.Items.AddRange(gender.Pants.Where(c => clothing.ContainsKey(c)).ToArray());
			}
			if (chestComboBox.Items.Count == 0)
			{
				chestComboBox.Items.Add("emptychest");
			}
			if (legsComboBox.Items.Count == 0)
			{
				legsComboBox.Items.Add("emptylegs");
			}

			if (oldChest != null && chestComboBox.Items.Contains(oldChest))
			{
				chestComboBox.SelectedItem = oldChest;
			}
			else
			{
				chestComboBox.SelectedIndex = 0;
			}
			if (oldLegs != null && legsComboBox.Items.Contains(oldLegs))
			{
				legsComboBox.SelectedItem = oldLegs;
			}
			else
			{
				legsComboBox.SelectedIndex = 0;
			}

			var itemName = (string)(chestComboBox.SelectedItem ?? (string)chestComboBox.Items[0]);
			var chest = clothing.ContainsKey(itemName) ? clothing[itemName] : null;
			if (chest != null)
			{
				chest.Finish();
				chestColorComboBox.Items.Clear();
				chestColorComboBox.Tag = chest.ColorOptions;
				chestColorComboBox.Items.AddRange(chest.ColorOptions);
				if (oldChestColor >= chest.ColorOptions.Length)
				{
					oldChestColor = 0;
				}
				chestColorComboBox.SelectedIndex = oldChestColor;
			}
			itemName = (string)(legsComboBox.SelectedItem ?? (string)legsComboBox.Items[0]);
			var legs = clothing.ContainsKey(itemName) ? clothing[itemName] : null;
			if (legs != null)
			{
				legs.Finish();
				legsColorComboBox.Items.Clear();
				legsColorComboBox.Tag = legs.ColorOptions;
				legsColorComboBox.Items.AddRange(legs.ColorOptions);
				if (oldLegsColor >= legs.ColorOptions.Length)
				{
					oldLegsColor = 0;
				}
				legsColorComboBox.SelectedIndex = oldLegsColor;
			}

			identityPanel.Show();
		}

		private static void Draw(Bitmap canvas, Bitmap sheet, int cellX, int cellY, int cellW, int cellH, JsonObj palette1 = null, JsonObj palette2 = null, JsonObj palette3 = null, int shiftX = 0, int shiftY = 0, Clothing headForMask = null)
		{
			var pal = new Dictionary<Color, Color>();
			var mask = headForMask == null ? null : headForMask.Mask;

			foreach (var palette in new[] { palette1, palette2, palette3 })
			{
				if (palette != null)
				{
					foreach (var pair in palette)
					{
						var cFrom = pair.Key;
						var cTo = (string)pair.Value;
						if (cFrom.Length == 8) //RGBA
						{
							cFrom = cFrom.Substring(6, 2) + cFrom.Substring(0, 6); //switch to ARGB
						}
						else //RGB
						{
							cFrom = "FF" + cFrom; //convert to ARGB
						}
						if (cTo.Length == 8)
						{
							cTo = cTo.Substring(6, 2) + cTo.Substring(0, 6);
						}
						else
						{
							cTo = "FF" + cTo;
						}
						var iTo = Color.FromArgb(int.Parse(cFrom, NumberStyles.HexNumber));
						pal[iTo] = Color.FromArgb(int.Parse(cTo, NumberStyles.HexNumber));
					}
				}
			}

			var key = sheet.GetPixel(0, 0);
			for (var y = 0; y < cellH; y++)
			{
				for (var x = 0; x < cellW; x++)
				{
					if (x + shiftX < 0 || y + shiftY < 0)
					{
						continue;
					}
					if (headForMask != null && mask.GetPixel(x, y).A == 0)
					{
						continue;
					}
					var c = sheet.GetPixel(cellX + x, cellY + y);
					if (c.A == 0 || c.Equals(key))
					{
						continue;
					}
					if (pal.ContainsKey(c))
					{
						c = pal[c];
					}
					if (c.A < 255)
					{
						var a = c.A / 255f;
						var o = canvas.GetPixel(x + shiftX, y + shiftY);
						c = Color.FromArgb(
							255,
							(int)((c.R * a) + o.R * (1 - a)),
							(int)((c.G * a) + o.G * (1 - a)),
							(int)((c.B * a) + o.B * (1 - a))
							);
					}

					canvas.SetPixel(x + shiftX, y + shiftY, c);
				}
			}
		}

		private static void Draw(Bitmap canvas, Bitmap sheet, Frames frames, string name, JsonObj palette1 = null, JsonObj palette2 = null, JsonObj palette3 = null, int shiftX = 0, int shiftY = 0, Clothing headForMask = null)
		{
			var frame = frames.Names[name];
			Draw(canvas, sheet, frame[0], frame[1], frame[2], frame[3], palette1, palette2, palette3, shiftX, shiftY, headForMask);
		}
		private static void Draw(Bitmap canvas, Bitmap sheet, Frames frames, string name, JsonObj palette1 = null, JsonObj palette2 = null, int shiftX = 0, int shiftY = 0, Clothing headForMask = null)
		{
			var frame = frames.Names[name];
			Draw(canvas, sheet, frame[0], frame[1], frame[2], frame[3], palette1, palette2, null, shiftX, shiftY, headForMask);
		}

		private void RenderCharacter(bool toExport = false)
		{
			drawLock = false;

			var species = (Species)speciesListbox.SelectedItem;
			var gender = species.Genders[maleGenderRadioButton.Checked ? 0 : 1];
			var genderName = maleGenderRadioButton.Checked ? "male" : "female";
			var personality = Humanoid.Personalities[personalityTrackBar.Value];
			var canvas = characterPictureBox.Image != null ? (Bitmap)characterPictureBox.Image : new Bitmap(43, 43);
			using (var gfx = Graphics.FromImage(canvas))
			{
				if (toExport)
				{
					gfx.Clear(Color.Transparent);
				}
				else if (previewOnMagentaToolStripButton.Checked)
				{
					gfx.Clear(Color.Magenta);
				}
				else
				{
					gfx.FillRectangle(new LinearGradientBrush(new Point(0, 0), new Point(43, 43), Color.Gray, Color.Black), 0, 0, 43, 43);
				}
			}

			var bodyColor = bodyColorComboBox.SelectedIndex;
			var undyColor = undyColorComboBox.SelectedIndex;
			var hairColor = hairColorComboBox.SelectedIndex;

			var itemName = (string)(chestComboBox.SelectedItem ?? (string)chestComboBox.Items[0]);
			JsonObj chestColor = null;
			JsonObj legsColor = null;
			var chest = clothing.ContainsKey(itemName) ? clothing[itemName] : null;
			if (chest != null)
			{
				chest.Finish();
				if (chestColorComboBox.SelectedIndex >= chest.ColorOptions.Length)
				{
					chestColorComboBox.SelectedIndex = 0;
				}
				chestColor = chest.ColorOptions[chestColorComboBox.SelectedIndex];
			}
			itemName = (string)(legsComboBox.SelectedItem ?? (string)legsComboBox.Items[0]);
			var legs = clothing.ContainsKey(itemName) ? clothing[itemName] : null;
			if (legs != null)
			{
				legs.Finish();
				if (legsColorComboBox.SelectedIndex >= legs.ColorOptions.Length)
				{
					legsColorComboBox.SelectedIndex = 0;
				}
				legsColor = legs.ColorOptions[legsColorComboBox.SelectedIndex];
			}

			var bodyFrame = (string)personality[0];
			var sleeveFrame = (string)personality[1];
			var headOffset = ((List<object>)personality[2]).Select(x => (int)((double)x)).ToArray();
			var sleeveOffset = ((List<object>)personality[3]).Select(x => (int)((double)x)).ToArray();
			var hairIndex = hairOptionComboBox.SelectedIndex;

			Draw(canvas, species.Parts["backarm"], species.PartFrames["backarm"], sleeveFrame, species.BodyColor[bodyColor], species.UndyColor[undyColor], species.HairColor[hairColor], sleeveOffset[0], sleeveOffset[1]);
			if (previewWithClothesToolStripButton.Checked && chest != null)
				Draw(canvas, chest.Parts[genderName + "backSleeve"], chest.PartFrames[genderName + "backSleeve"], sleeveFrame, chestColor, null, sleeveOffset[0], sleeveOffset[1]);
			Draw(canvas, species.Parts[genderName + "head"], species.PartFrames[genderName + "head"], "normal", species.BodyColor[bodyColor], species.UndyColor[undyColor], species.HairColor[hairColor], headOffset[0], headOffset[1]);
			var hairColorA = species.BodyColor[bodyColor];
			var hairColorB = species.BodyColor[bodyColor];
			if (species.HeadOptionAsHairColor)
				hairColorA = species.HairColor[hairColor];
			if (species.AltOptionAsHairColor)
				hairColorB = species.UndyColor[undyColor];
			var hair = gender.Hair.ElementAt(hairIndex);
			Draw(canvas, hair.Value, gender.HairFrames[hair.Key], "normal", hairColorA, hairColorB, headOffset[0], headOffset[1], null);
			if (species.HeadOptionAsFacialhair)
			{
				var facialHair = gender.FacialHair.ElementAt(headOptionComboBox.SelectedIndex);
				Draw(canvas, facialHair.Value, gender.FacialHairFrames[facialHair.Key], "normal", hairColorA, hairColorB, headOffset[0], headOffset[1]);
			}
			if (species.AltOptionAsFacialMask)
			{
				var facialMask = gender.FacialMask.ElementAt(undyOptionComboBox.SelectedIndex);
				Draw(canvas, facialMask.Value, gender.FacialMaskFrames[facialMask.Key], "normal", hairColorA, hairColorB, headOffset[0], headOffset[1]);
			}

			Draw(canvas, species.Parts[genderName + "body"], species.PartFrames[genderName + "body"], bodyFrame, species.BodyColor[bodyColor], species.UndyColor[undyColor], species.HairColor[hairColor], 0, 0);
			if (previewWithClothesToolStripButton.Checked)
			{
				if (legs != null)
				{
					Draw(canvas, legs.Parts[genderName + "Frames"], legs.PartFrames[genderName + "Frames"], bodyFrame, legsColor, null, 0, 0);
				}
				if (chest != null)
				{
					Draw(canvas, chest.Parts[genderName + "body"], chest.PartFrames[genderName + "body"], bodyFrame, chestColor, null, 0, 0);
				}
			}
			Draw(canvas, species.Parts["frontarm"], species.PartFrames["frontarm"], sleeveFrame, species.BodyColor[bodyColor], species.UndyColor[undyColor], species.HairColor[hairColor], sleeveOffset[0], sleeveOffset[1]);
			if (previewWithClothesToolStripButton.Checked && chest != null)
			{
				Draw(canvas, chest.Parts[genderName + "frontSleeve"], chest.PartFrames[genderName + "frontSleeve"], sleeveFrame, chestColor, null, sleeveOffset[0], sleeveOffset[1]);
			}

			if (toExport)
			{
				return;
			}

			characterPictureBox.Image = canvas;
		}

		private void personalityTrackBar_Scroll(object sender, EventArgs e)
		{
			if (drawLock)
				return; 
			RenderCharacter();
		}

		void colorComboBox_DrawItem(object sender, DrawItemEventArgs e)
		{
			if (((ComboBox)sender).Tag == null)
			{
				return;
			}
			if (e.Index == -1)
			{
				return;
			}
			var g = e.Graphics;
			var item = ((JsonObj[])((ComboBox)sender).Tag)[e.Index];
			var pal = item.Select(x =>
			{
				var c = (string)x.Value;
				if (c.Length == 8) //RGBA
				{
					c = c.Substring(6, 2) + c.Substring(0, 6); //switch to ARGB
				}
				else //RGB
				{
					c = "FF" + c; //convert to ARGB
				}
				return new SolidBrush(Color.FromArgb(int.Parse(c, NumberStyles.HexNumber)));
			}).ToArray();
			var rect = new Rectangle(e.Bounds.Left + 2, e.Bounds.Top + 2, (e.Bounds.Width - 4) / pal.Length, e.Bounds.Height - 4);
			e.DrawBackground();
			for (var i = 0; i < pal.Length; i++)
			{
				g.FillRectangle(pal[i], rect);
				rect.Offset(rect.Width, 0);
			}
			e.DrawFocusRectangle();
		}

		private void colorComboBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (drawLock)
				return;
			RenderCharacter();
		}

		private void clothingComboBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (drawLock)
			{
				return;
			}
			var itemName = (string)(((ComboBox)sender).SelectedItem ?? (string)((ComboBox)sender).Items[0]);
			var item = clothing.ContainsKey(itemName) ? clothing[itemName] : null;
			if (item == null)
			{
				if (sender == chestComboBox)
				{
					chestColorComboBox.Enabled = false;
				}
				else if (sender == legsComboBox)
				{
					legsColorComboBox.Enabled = false;
				}
				RenderCharacter();
				return;
			}
			item.Finish();
			if (sender == chestComboBox)
			{
				chestColorComboBox.Enabled = true;
				chestColorComboBox.Items.Clear();
				chestColorComboBox.Tag = item.ColorOptions;
				chestColorComboBox.Items.AddRange(item.ColorOptions);
				chestColorComboBox.SelectedIndex = 0;
			}
			else if (sender == legsComboBox)
			{
				legsColorComboBox.Enabled = true;
				legsColorComboBox.Items.Clear();
				legsColorComboBox.Tag = item.ColorOptions;
				legsColorComboBox.Items.AddRange(item.ColorOptions);
				legsColorComboBox.SelectedIndex = 0;
			}
			RenderCharacter();
		}

		private void genderRadioButton_CheckedChanged(object sender, EventArgs e)
		{
			if (drawLock)
				return;
			SelectGender();
			RenderCharacter();
		}

		private void optionScrollBar_ValueChanged(object sender, EventArgs e)
		{
			if (drawLock)
			{
				return;
			}
			RenderCharacter();
		}

		private void hairOptionComboBox_DrawItem(object sender, DrawItemEventArgs e)
		{
			if (e.Index == -1)
			{
				return;
			}
			var g = e.Graphics;
			var species = (Species)speciesListbox.SelectedItem;
			var gender = species.Genders[maleGenderRadioButton.Checked ? 0 : 1];
			var set = gender.Hair;
			if (sender == headOptionComboBox)
			{
				set = gender.FacialHair;
			}
			else if (sender == undyOptionComboBox)
			{
				set = gender.FacialMask;
			}
			var hairStyle = set.ElementAt(e.Index);
			e.DrawBackground();
			g.DrawString(hairStyle.Key, hairOptionComboBox.Font, e.State == DrawItemState.Selected ? SystemBrushes.HighlightText : SystemBrushes.ControlText, e.Bounds.Left + 24, e.Bounds.Top);
			e.DrawFocusRectangle();
			g.DrawImage(hairStyle.Value, new Rectangle(0, e.Bounds.Top, 24, 16), new Rectangle(hairStyle.Value.Width > 43 ? 43 + 8 : 8, 8, 24, 16), GraphicsUnit.Pixel);
		}

		private void characterPictureBox_Paint(object sender, PaintEventArgs e)
		{
			//STUDY: This doesn't seem to work on Mono/Linux.
			e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;

			var r1 = new Rectangle(0, 0, 43, 43);
			var r2 = new Rectangle(0, 0, 129, 129);

			e.Graphics.DrawImage(characterPictureBox.Image, r2, r1, GraphicsUnit.Pixel);
			e.Graphics.DrawRectangle(Pens.Gray, r2);
		}

		private void randomizeButton_Click(object sender, EventArgs e)
		{
			drawLock = true;
			var r = new Random();
			if (!personalityCheckBox.Checked)
			{
				personalityTrackBar.Value = r.Next(personalityTrackBar.Maximum);
			}
			if (!bodyCheckBox.Checked)
			{
				bodyColorComboBox.SelectedIndex = r.Next(bodyColorComboBox.Items.Count);
			}
			if (undyColorComboBox.Visible && !undyCheckBox.Checked)
			{
				undyColorComboBox.SelectedIndex = r.Next(undyColorComboBox.Items.Count);
			}
			if (hairColorComboBox.Visible && !headCheckBox.Checked)
			{
				hairColorComboBox.SelectedIndex = r.Next(hairColorComboBox.Items.Count);
			}
			if (!hairCheckBox.Checked)
			{
				hairOptionComboBox.SelectedIndex = r.Next(hairOptionComboBox.Items.Count);
			}
			if (undyOptionComboBox.Visible && !undyCheckBox.Checked)
			{
				undyOptionComboBox.SelectedIndex = r.Next(undyOptionComboBox.Items.Count);
			}
			if (headOptionComboBox.Visible && !headCheckBox.Checked)
			{
				headOptionComboBox.SelectedIndex = r.Next(headOptionComboBox.Items.Count);
			}
			if (!chestColorCheckBox.Checked)
			{
				chestColorComboBox.SelectedIndex = r.Next(chestColorComboBox.Items.Count);
			}
			if (!legsColorCheckBox.Checked)
			{
				legsColorComboBox.SelectedIndex = r.Next(legsColorComboBox.Items.Count);
			}
			if (!chestCheckBox.Checked)
			{
				chestComboBox.SelectedIndex = r.Next(chestComboBox.Items.Count);
			}
			if (!legsCheckBox.Checked)
			{
				legsComboBox.SelectedIndex = r.Next(legsComboBox.Items.Count);
			}
			RenderCharacter();
		}

		private void allowAllClothingCheckBox_CheckedChanged(object sender, EventArgs e)
		{
			SelectGender();
			RenderCharacter();
		}

		private string MakeReplaceDirectives(JsonObj colors)
		{
			var directives = new StringBuilder();
			directives.Append("?replace");
			foreach (var pair in colors)
			{
				directives.AppendFormat(";{0}={1}", pair.Key, pair.Value);
			}
			return directives.ToString();
		}

		private void saveToPlayerFileToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(nameTextBox.Text))
			{
				MessageBox.Show("You must enter a name.");
				nameTextBox.Focus();
				return;
			}

			var species = (Species)speciesListbox.SelectedItem;
			var gender = species.Genders[maleGenderRadioButton.Checked ? 0 : 1];
			var personality = Humanoid.Personalities[personalityTrackBar.Value];

			var template = editPlayer ?? (JsonObj)Json5.Parse(global::SBCharCreator.Properties.Resources.player);
			var content = (JsonObj)template["__content"];

			var uuid = (string)content["uuid"];
			if (editPlayer == null)
			{
				uuid = Guid.NewGuid().ToString("N").ToLowerInvariant(); //4bc9b5640ed67e1b907f27c658ec9240
				content["uuid"] = uuid;
				((JsonObj)content["log"])["introComplete"] = skipIntroCheckBox.Checked;
				content["modetype"] = modeComboBox.SelectedItem.ToString().ToLowerInvariant(); //CHECKME

				var bluePrints = new List<string>();
				var playerBlueprints = ((JsonObj)Assets.GetJson("/player.config")).Path<List<object>>("/defaultBlueprints/tier1");
				playerBlueprints.AddRange((List<object>)(species.DefaultBluePrints["tier1"]));
				foreach (var item in playerBlueprints.Cast<JsonObj>())
				{
					try
					{
						var itemName = (string)item.ElementAt(0).Value;
						if (bluePrints.Contains(itemName))
						{
							continue;
						}
						bluePrints.Add(itemName);
					}
					catch (InvalidCastException)
					{
						//No.
					}
				}
				var knownBluePrints = content.Path<List<object>>("/blueprints/knownBlueprints");
				var newBluePrints = content.Path<List<object>>("/blueprints/newBlueprints");
				foreach (var item in bluePrints)
				{
					var newItem = Json5.Parse("{ \"name\": \"" + item + "\", \"count\": 1, \"parameters\": {} }");
					knownBluePrints.Add(newItem);
					newBluePrints.Add(newItem);
				}
			}

			var identity = (JsonObj)content["identity"];

			var bodyColor = MakeReplaceDirectives(species.BodyColor[bodyColorComboBox.SelectedIndex]);
			var undyColor = MakeReplaceDirectives(species.UndyColor[undyColorComboBox.SelectedIndex]);
			var hairColor = MakeReplaceDirectives(species.HairColor[hairColorComboBox.SelectedIndex]);
			identity["bodyDirectives"] = bodyColor + undyColor;
			if (species.HairColorAsBodySubColor)
			{
				identity["bodyDirectives"] = (string)identity["bodyDirectives"] + hairColor;
			}
			identity["emoteDirectives"] = bodyColor + undyColor;
			identity["facialHairGroup"] = gender.FacialHairGroup;
			if (species.HeadOptionAsFacialhair)
			{
				/* Apex and Avians both have this set.
				 * Apex: "facialHairDirectives" : "?replace;6f2919=212123;a85636=3f3e43;e0975c=595760",
				 * Avian: "facialHairDirectives" : "?replace;735e3a=977841;951500=5d6d69;f32200=f6fbfb;ffca8a=add068;be1b00=8fa7a3;6f2919=596809;d9c189=eacf60;a38d59=c1a24e;dc1f00=d7e8e8;a85636=6e8210;e0975c=85ac1b",
				 * These are BODY color.
				 */
				identity["facialHairDirectives"] = bodyColor;
				identity["facialHairType"] = gender.FacialHair.ElementAt(headOptionComboBox.SelectedIndex).Key;
			}
			identity["facialMaskGroup"] = gender.FacialMaskGroup;
			if (species.AltOptionAsFacialMask)
			{
				/* Avians have this.
				 * Avian: "facialMaskDirectives" : "?replace;735e3a=977841;951500=5d6d69;f32200=f6fbfb;ffca8a=add068;be1b00=8fa7a3;6f2919=596809;d9c189=eacf60;a38d59=c1a24e;dc1f00=d7e8e8;a85636=6e8210;e0975c=85ac1b",
				 * Again, that is BODY color.
				 */
				identity["facialHairDirectives"] = bodyColor;
				if (species.AltColorAsFacialMaskSubColor)
					identity["facialHairDirectives"] = (string)identity["facialHairDirectives"] + undyColor;
				identity["facialHairType"] = gender.FacialMask.ElementAt(undyOptionComboBox.SelectedIndex).Key;
			}
			identity["hairDirectives"] = bodyColor;
			if (species.HeadOptionAsHairColor)
			{
				identity["hairDirectives"] = hairColor;
			}
			if (species.AltOptionAsHairColor)
			{
				identity["hairDirectives"] = (string)identity["hairDirectives"] + undyColor;
			}
			identity["hairGroup"] = gender.HairGroup;
			identity["hairType"] = gender.Hair.ElementAt(hairOptionComboBox.SelectedIndex).Key;
			identity["name"] = nameTextBox.Text;
			identity["personalityIdle"] = personality[0];
			identity["personalityArmIdle"] = personality[1];
			identity["personalityHeadOffset"] = personality[2];
			identity["personalityArmOffset"] = personality[3];
			identity["species"] = species.Kind;
			content["description"] = descriptionTextBox.Text;
			content["color"] = ColorToJson(beamColorPanel.BackColor);

			if (editPlayer == null)
			{
				var inventory = (JsonObj)content["inventory"];
				var chestItem = inventory.Path("/chestSlot/content");
				var legsItem = inventory.Path("/legsSlot/content");
				var chest = (Clothing)clothing[(string)chestComboBox.SelectedItem ?? (string)chestComboBox.Items[0]];
				var legs = (Clothing)clothing[(string)legsComboBox.SelectedItem ?? (string)legsComboBox.Items[0]];
				chestItem["name"] = chest.ItemName;
				legsItem["name"] = legs.ItemName;
				chestItem["parameters"] = new JsonObj() { { "colorIndex", chestColorComboBox.SelectedIndex } };
				legsItem["parameters"] = new JsonObj() { { "colorIndex", legsColorComboBox.SelectedIndex } };
			}

			/*
			var versioning = (JsonObj)Assets.GetJson("/versioning.config");
			var playerEntity = (int)((double)versioning["PlayerEntity"]);
			if (playerEntity >= 27)
			{
				var itemBags = new JsonObj();
				inventory.Add("itemBags", itemBags);
				foreach (var bags in new[] { "mainBag", "materialBag", "objectBag", "reagentBag", "foodBag" })
				{
					itemBags.Add(bags, inventory[bags]);
					inventory.Remove(bags);
				}
			}
			*/

			if (species.StatusEffects.Length > 0)
			{
				var persistentEffectCategories = template.Path("/statusController/persistentEffectCategories");
				persistentEffectCategories.Clear();
				persistentEffectCategories.Add("species", species.StatusEffects);
			}

			using (var playerFile = new BinaryWriter(File.Open(Path.Combine(SavePath, uuid + ".player"), FileMode.Create)))
			{
				Json5.Serialize(playerFile, template);
			}

			MessageBox.Show(this, string.Format("Saved \"{0}\" as \"{1}.player\".", nameTextBox.Text, uuid), Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void loadPresetToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (openFileDialog.ShowDialog(this) == DialogResult.Cancel)
			{
				return;
			}
			var save = default(JsonObj);
			try
			{
				save = (JsonObj)Json5.Parse(File.ReadAllText(openFileDialog.FileName));
			}
			catch (JsonException jex)
			{
				MessageBox.Show(this, "Something went wrong reading the file: " + jex.Message, Application.ProductName);
				return;
			}
			if (!save.ContainsKey("sbCharCreator"))
			{
				MessageBox.Show(this, "This is not a valid character preset.", Application.ProductName);
				return;
			}
			var savedSpecies = species.Find(s => s.Kind == (string)save["species"]);
			if (savedSpecies == null)
			{
				MessageBox.Show(this, string.Format("Don't know what \"{0}\" is but it's not supported.", (string)save["species"]), Application.ProductName);
				return;
			}
			drawLock = true;
			identityPanel.Hide();
			nameTextBox.Text = (string)save["name"];
			if (save.ContainsKey("description"))
			{
				descriptionTextBox.Text = (string)save["description"];
			}
			else
			{
				descriptionTextBox.Text = "This guy seems to have nothing to say for himself.";
			}
			if (save.ContainsKey("color"))
				beamColorPanel.BackColor = ColorFromJson(save["color"]);
			else
				beamColorPanel.BackColor = Color.FromArgb(51, 117, 237);
			speciesListbox.SelectedIndex = species.IndexOf(savedSpecies);
			if (save.ContainsKey("gender"))
			{
				var gender = (int)((double)save["gender"]);
				if (gender > 0)
				{
					femaleGenderRadioButton.Checked = true;
				}
				else
				{
					maleGenderRadioButton.Checked = true;
				}
			}
			personalityTrackBar.Value = (int)((double)save["personality"]);
			bodyColorComboBox.SelectedIndex = (int)((double)save["bodyColor"]);
			undyColorComboBox.SelectedIndex = (int)((double)save["undyColor"]);
			if (undyOptionComboBox.Items.Count > 0)	undyOptionComboBox.SelectedIndex = (int)((double)save["undyOption"]);
			if (hairOptionComboBox.Items.Count > 0) hairOptionComboBox.SelectedIndex = (int)((double)save["hairOption"]);
			if (headOptionComboBox.Items.Count > 0) headOptionComboBox.SelectedIndex = (int)((double)save["headOption"]);
			hairColorComboBox.SelectedIndex = (int)((double)save["hairColor"]);
			var chest = (string)save["chest"];
			if (!chestComboBox.Items.Contains(chest))
				allowAllClothingCheckBox.Checked = true;
			if (chestComboBox.Items.Contains(chest))
				chestComboBox.SelectedItem = chest;
			chestColorComboBox.SelectedIndex = (int)((double)save["chestColor"]);
			var legs = (string)save["legs"];
			if (legsComboBox.Items.Contains(legs))
				legsComboBox.SelectedItem = legs;
			legsColorComboBox.SelectedIndex = (int)((double)save["legsColor"]);
			drawLock = false;
			identityPanel.Show();
			RenderCharacter();
		}

		private void savePresetToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(nameTextBox.Text))
			{
				saveFileDialog.FileName = nameTextBox.Text + ".config";
			}
			if (saveFileDialog.ShowDialog(this) == DialogResult.Cancel)
			{
				return;
			}
			var save = new JsonObj();
			save.Add("sbCharCreator", Application.ProductVersion);
			save.Add("name", nameTextBox.Text);
			save.Add("species", ((Species)speciesListbox.SelectedItem).Kind);
			save.Add("gender", femaleGenderRadioButton.Checked ? 1 : 0);
			save.Add("description", descriptionTextBox.Text);
			save.Add("color", ColorToJson(beamColorPanel.BackColor));
			save.Add("personality", personalityTrackBar.Value);
			save.Add("bodyColor", bodyColorComboBox.SelectedIndex);
			save.Add("undyColor", undyColorComboBox.SelectedIndex);
			save.Add("undyOption", undyOptionComboBox.SelectedIndex);
			save.Add("hairOption", hairOptionComboBox.SelectedIndex);
			save.Add("headOption", headOptionComboBox.SelectedIndex);
			save.Add("hairColor", hairColorComboBox.SelectedIndex);
			save.Add("chest", chestComboBox.SelectedItem);
			save.Add("chestColor", chestColorComboBox.SelectedIndex);
			save.Add("legs", legsComboBox.SelectedItem);
			save.Add("legsColor", legsColorComboBox.SelectedIndex);

			File.WriteAllText(saveFileDialog.FileName, save.Stringify());
		}

		private void randomNameButton_Click(object sender, EventArgs e)
		{
			var species = (Species)speciesListbox.SelectedItem;
			var gender = maleGenderRadioButton.Checked ? 0 : 1;
			var nameSource = species.NameGen[gender];
			var nameFile = nameSource.Substring(0, nameSource.IndexOf(':'));
			var nameKey = nameSource.Substring(nameSource.IndexOf(':') + 1);
			var names = (JsonObj)Assets.GetJson(nameFile);
			var name = NameGen.Generate(names, nameKey);
			nameTextBox.Text = name;
		}

		private void previewWithClothesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (drawLock)
			{
				return;
			}
			RenderCharacter();
		}

		private void exportImageToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var filters = saveFileDialog.Filter;
			saveFileDialog.Filter = "Portable Network Graphics (*.png)|*.png";
			saveFileDialog.FileName = nameTextBox.Text + ".png";
			if (saveFileDialog.ShowDialog(this) != DialogResult.Cancel)
			{
				RenderCharacter(true);
				characterPictureBox.Image.Save(saveFileDialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
				RenderCharacter(false);
			}
			saveFileDialog.Filter = filters;
		}

		private void quitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			this.Close();
		}

		private void beamColorButton_Click(object sender, EventArgs e)
		{
			colorDialog1.Color = beamColorPanel.BackColor;
			if (colorDialog1.ShowDialog(this) == DialogResult.OK)
			{
				beamColorPanel.BackColor = colorDialog1.Color;
			}
		}

		private Color ColorFromJson(object vector3)
		{
			if (vector3 is List<double>)
			{
				var v = vector3 as List<double>;
				if (v.Count != 3)
					throw new ArgumentException("Color value has to have three values.");
				var r = (int)((double)v[0]);
				var g = (int)((double)v[1]);
				var b = (int)((double)v[2]);
				return Color.FromArgb(r, g, b);
			}
			if (vector3 is List<object>)
			{
				var v = vector3 as List<object>;
				if (v.Count != 3)
					throw new ArgumentException("Color value has to have three values.");
				if (v[0] is double)
				{
					var r = (int)((double)v[0]);
					var g = (int)((double)v[1]);
					var b = (int)((double)v[2]);
					return Color.FromArgb(r, g, b);
				}
				else
				{
					var r = (int)((long)v[0]);
					var g = (int)((long)v[1]);
					var b = (int)((long)v[2]);
					return Color.FromArgb(r, g, b);
				}
			}
			throw new ArgumentException("Color must be an array of values.");
		}

		private List<double> ColorToJson(Color color)
		{
			return new List<double>() { color.R, color.G, color.B };
		}

		private void editPlayerToolStripButton_Click(object sender, EventArgs e)
		{
			openFileDialog1.InitialDirectory = SavePath;
			if (openFileDialog1.ShowDialog(this) == DialogResult.Cancel)
			{
				return;
			}
			using (var stream = new BinaryReader(openFileDialog1.OpenFile()))
			{
				editPlayer = (JsonObj)Json5.Deserialize(stream);
			}

			cancelEditToolStripButton.Visible = true;
			editPlayerToolStripButton.Visible = false;
			allowAllClothingCheckBox.Checked = true;
			allowAllClothingCheckBox.Enabled = false;

			var content = (JsonObj)editPlayer["__content"];
			var identity = (JsonObj)content["identity"];

			var savedSpecies = species.Find(s => s.Kind == (string)identity["species"]);
			if (savedSpecies == null)
			{
				MessageBox.Show(this, string.Format("Don't know what \"{0}\" is but it's not supported.", (string)identity["species"]), Application.ProductName);
				return;
			}

			var bodyDirectives = (string)identity["bodyDirectives"];
			var bodyColors = bodyDirectives.ToLowerInvariant();
			var undyColors = string.Empty;
			if (bodyColors.IndexOf("?replace", 2) > 0)
			{
				bodyColors = bodyColors.Substring(0, bodyDirectives.IndexOf("?replace", 2));
				undyColors = bodyDirectives.Substring(bodyDirectives.IndexOf("?replace", 2)).ToLowerInvariant();
				if (undyColors.IndexOf("?replace", 2) > 0)
				{
					//we have HairColorAsBodySubColor so there's a third that we'll ignore.
					undyColors = undyColors.Substring(0, undyColors.IndexOf("?replace", 2));
				}
			}
			var hairColors = ((string)identity["hairDirectives"]).ToLowerInvariant();

			drawLock = true;
			identityPanel.Hide();
			nameTextBox.Text = (string)identity["name"];
			if (content.ContainsKey("description"))
			{
				descriptionTextBox.Text = (string)content["description"];
			}
			else
			{
				descriptionTextBox.Text = "This guy seems to have nothing to say for himself.";
			}
			if (identity.ContainsKey("color"))
			{
				beamColorPanel.BackColor = ColorFromJson(identity["color"]);
			}
			else
			{
				beamColorPanel.BackColor = Color.FromArgb(51, 117, 237);
			}
			speciesListbox.SelectedIndex = species.IndexOf(savedSpecies);
			if (identity.ContainsKey("gender"))
			{
				if ((string)identity["gender"] == "female")
				{
					femaleGenderRadioButton.Checked = true;
				}
				else
				{
					maleGenderRadioButton.Checked = true;
				}
			}
			var gender = savedSpecies.Genders[maleGenderRadioButton.Checked ? 0 : 1];
			
			//personalityTrackBar.Value = (int)((long)identity["personality"]);
			var i = 0;
			var myIdle = (string)identity["personalityIdle"];
			var myArmIdle = (string)identity["personalityArmIdle"];
			var myHeadOffset = identity["personalityHeadOffset"].Stringify().Replace(" ", string.Empty).Replace("\n", string.Empty);
			var myArmOffset = identity["personalityArmOffset"].Stringify().Replace(" ", string.Empty).Replace("\n", string.Empty);
			foreach (var personality in Humanoid.Personalities)
			{
				var thisIdle = (string)personality[0];
				var thisArmIdle = (string)personality[1];
				var thisHeadOffset = personality[2].Stringify().Replace(" ", string.Empty).Replace("\n", string.Empty);
				var thisArmOffset = personality[3].Stringify().Replace(" ", string.Empty).Replace("\n", string.Empty);
				if (myIdle == thisIdle && myArmIdle == thisArmIdle && myHeadOffset == thisHeadOffset && myArmOffset == thisArmOffset)
				{
					personalityTrackBar.Value = i;
					break;
				}
				i++;
			}


			i = 0;
			foreach (var possibility in savedSpecies.BodyColor)
			{
				var asHex = MakeReplaceDirectives(possibility);
				asHex = asHex.Substring(asHex.IndexOf('=', asHex.Length > 26 ? 26 : 4), 7).ToLowerInvariant();
				if (bodyColors.Contains(asHex))
				{
					bodyColorComboBox.SelectedIndex = i;
					break;
				}
				i++;
			}
			i = 0;
			foreach (var possibility in savedSpecies.UndyColor)
			{
				var asHex = MakeReplaceDirectives(possibility);
				asHex = asHex.Substring(asHex.IndexOf('=', asHex.Length > 26 ? 26 : 4), 7).ToLowerInvariant();
				if (undyColors.Contains(asHex))
				{
					undyColorComboBox.SelectedIndex = i;
					break;
				}
				i++;
			}
			i = 0;
			foreach (var possibility in savedSpecies.HairColor)
			{
				var asHex = MakeReplaceDirectives(possibility);
				asHex = asHex.Substring(asHex.IndexOf('=', asHex.Length > 26 ? 26 : 4), 7).ToLowerInvariant();
				if (hairColors.Contains(asHex))
				{
					hairColorComboBox.SelectedIndex = i;
					break;
				}
				i++;
			}

			var hairType = (string)identity["hairType"];
			if (hairOptionComboBox.Items.Contains(hairType))
			{
				hairOptionComboBox.SelectedItem = hairType;
			}

			if (savedSpecies.AltOptionAsFacialMask)
			{
				var facialHairType = (string)identity["facialHairType"];
				if (undyOptionComboBox.Items.Contains(facialHairType))
				{
					undyOptionComboBox.SelectedItem = facialHairType;
				}
			}

			var inventory = (JsonObj)content["inventory"];
			var chestSlot = (JsonObj)inventory["chestCosmeticSlot"];
			if (chestSlot == null)
			{
				chestSlot = (JsonObj)inventory["chestSlot"];
			}
			if (chestSlot != null)
			{
				var chestContent = (JsonObj)chestSlot["content"];
				var chest = (string)chestContent["name"];
				if (chestComboBox.Items.Contains(chest))
				{
					chestComboBox.SelectedItem = chest;
				}
				if (((JsonObj)chestContent["parameters"]).Count > 0)
				{
					var index = 0;
					try
					{
						index = (int)((long)((JsonObj)chestContent["parameters"])["colorIndex"]);
					}
					catch
					{
						index = (int)((double)((JsonObj)chestContent["parameters"])["colorIndex"]);
					}
					index = index % 12;
					if (index < 0) index = 0;
					chestColorComboBox.SelectedIndex = index;
				}
				else
					chestColorComboBox.SelectedIndex = 0;
			}
			var legsSlot = (JsonObj)inventory["legsCosmeticSlot"];
			if (legsSlot == null)
			{
				legsSlot = (JsonObj)inventory["legsSlot"];
			}
			if (legsSlot != null)
			{
				var legsContent = (JsonObj)legsSlot["content"];
				var legs = (string)legsContent["name"];
				if (legsComboBox.Items.Contains(legs))
				{
					legsComboBox.SelectedItem = legs;
				}
				if (((JsonObj)legsContent["parameters"]).Count > 0)
				{
					var index = (int)((long)((JsonObj)legsContent["parameters"])["colorIndex"]);
					index = index % 12;
					if (index < 0) index = 0;
					legsColorComboBox.SelectedIndex = index;
				}
				else
					legsColorComboBox.SelectedIndex = 0;
			}

			chestCheckBox.Checked = true;
			chestCheckBox.Enabled = false;
			chestComboBox.Enabled = false;
			chestColorCheckBox.Checked = true;
			chestColorCheckBox.Enabled = false;
			chestColorComboBox.Enabled = false;
			legsCheckBox.Checked = true;
			legsCheckBox.Enabled = false;
			legsComboBox.Enabled = false;
			legsColorCheckBox.Checked = true;
			legsColorCheckBox.Enabled = false;
			legsColorComboBox.Enabled = false;
			modeComboBox.Enabled = false;
			saveToPlayerFileToolStripButton.Text = "Update .player file";

			drawLock = false;
			identityPanel.Show();
			RenderCharacter();
		}

		private void cancelEditToolStripButton_Click(object sender, EventArgs e)
		{
			cancelEditToolStripButton.Visible = false;
			editPlayerToolStripButton.Visible = true;
			allowAllClothingCheckBox.Enabled = true;
			chestCheckBox.Checked = false;
			chestCheckBox.Enabled = true;
			chestComboBox.Enabled = true;
			chestColorComboBox.Enabled = true;
			legsCheckBox.Checked = false;
			legsCheckBox.Enabled = true;
			legsComboBox.Enabled = true;
			legsColorComboBox.Enabled = true;
			modeComboBox.Enabled = true;
			saveToPlayerFileToolStripButton.Text = "Save to new .player file";
		}
	}
}
