using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SBCharCreator
{
	static class Program
	{
		[STAThread]
		static void Main()
		{
			if (!System.IO.File.Exists("sources.txt"))
			{
				MessageBox.Show("No sources.txt found.", Application.ProductName);
				return;
			}

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			try
			{
				Application.Run(new charCreatorForm());
			}
			catch (ObjectDisposedException)
			{
				//Because Winforms is silly.
			}
		}
	}
}
