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
					Console.WriteLine(dr[i] as string);
				}
			}
			
			Console.ReadKey();
		}
	}
}
