using System;
using System.Collections.Generic;
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
			Console.ReadKey();
		}
	}
}
