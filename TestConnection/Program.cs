using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebDataLoader;

namespace WebDataLoaderTest
{
	class Program
	{
		static void OutputTable(DataTable dt)
		{
			foreach (DataColumn dc in dt.Columns)
			{
				Console.Write(dc.ColumnName + "\t");
			}
			Console.WriteLine("");

			foreach (DataRow dr in dt.Rows)
			{
				int cols = dt.Columns.Count;
				for (int i = 0; i < cols; i++)
				{
					if (dt.Columns[i].DataType == typeof(string))
					{
						Console.Write((dr[i] as string) + "\t");
					}
					else
					{
						if (dr[i] == System.DBNull.Value)
						{
							Console.Write(" \t");
						}
						else
						{
							Console.Write((Convert.ToDouble(dr[i])).ToString() + "\t");
						}
					}
				}
				Console.Write(Environment.NewLine);
			}
		}
		static void Main(string[] args)
		{
			WebImporter wi = new WebImporter();

			Console.WriteLine("Enter web address:");
			string web = Console.ReadLine();
			if (string.IsNullOrEmpty(web))
				web = @"http://localhost:27714/";

			Console.WriteLine("Enter user name:");
			string usr = Console.ReadLine();
			if (string.IsNullOrEmpty(usr))
				usr = "web";

			Console.WriteLine("Enter password:");
			string pas = Console.ReadLine();
			if (string.IsNullOrEmpty(pas))
				pas = "web";

			Console.WriteLine("Session ID:");
			Console.WriteLine(wi.GetSession(web, usr, pas));

			Console.WriteLine("Output name:");
			string output = Console.ReadLine();
			if (string.IsNullOrEmpty(output))
				output = "PetrelSource";

			Console.WriteLine("Output:");
			DataTable dt = wi.GetOutput(web, usr, pas, output);
			OutputTable(dt);

			Console.WriteLine("Well UWI:");
			string uwi = Console.ReadLine();
			if (string.IsNullOrEmpty(uwi))
				uwi = dt.Rows[0][dt.Columns["UWI"].Ordinal].ToString();

			Console.WriteLine("using uwi:" + uwi);
			string id = wi.GetWellID(web, usr, pas, uwi);
			Console.WriteLine("Found ID:" + id);

			Console.WriteLine("Survey - press key");
			Console.ReadKey();
			dt = wi.LoadSurvey(web, usr, pas, uwi);
			OutputTable(dt);

			Console.WriteLine("Production Monthly - press key");
			Console.ReadKey();
			dt = wi.LoadMonthlyProduction(web, usr, pas, uwi);
			OutputTable(dt);

			Console.WriteLine("Production Daily - press key");
			Console.ReadKey();
			dt = wi.LoadDailyProduction(web, usr, pas, uwi);
			OutputTable(dt);

			Console.ReadKey();
		}
	}
}
